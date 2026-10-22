#include "AI_STATE.h"
#include "AI_DEBUG.h"
#include "DEBUG_MENU.h"

#include <algorithm>
#include <atomic>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <map>
#include <memory>
#include <mutex>
#include <sstream>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <imgui.h>

/*
	The Characters group's State and Control tabs, for the character selected in the debug menu (see AI_STATE.h):

	- State: its orders (the commands a script and its own tasks gave it, the requests asked of its body and the states its
	  body is in), the action and secondary actions it is in, and its 105 state flags (those that changed in the last two
	  seconds shown brighter).
	- Control: its motivations as ticks (the game's own way of forcing an NPC into or out of a family of behaviour, with
	  a "force idle" among them), its mood (forced on the top mood layer, held there every AI frame until let go), its health (set,
	  damaged or set on fire, or killed as the game's own death actions do it, with its two
	  gauges and its stress shown), and despawning it and spawning it again through its script entity's own methods (the
	  game makes a respawned character again where the level placed its entity; the list follows how far it has got).

	Everything that changes a character is done on the AI thread (spawning on the entity thread), through the game's own
	functions, with the character found again by its unique id there. Each button has a command ("ai.motivation",
	"ai.kill"...) acting on the selected character, or on the one an "id=<unique id>" in the command's text names.
*/
namespace
{
	constexpr int kPlayerClass = 0;
	constexpr int kAlienClass = 1;
	constexpr int kTopMoodLayer = 3;
	constexpr float kForEver = -1.0f;

	// ---- What the tabs did last ----

	std::mutex g_statusMutex;
	std::string g_status;

	void SetStatus(const std::string& text)
	{
		{
			std::lock_guard<std::mutex> lock(g_statusMutex);
			g_status = text;
		}
		DevTools::Log("Debug menu: %s", text.c_str());
	}

	void DrawStatus()
	{
		std::lock_guard<std::mutex> lock(g_statusMutex);
		if (g_status.empty())
			return;
		ImGui::Separator();
		ImGui::PushTextWrapPos(0.0f);
		ImGui::TextDisabled("Last: %s", g_status.c_str());
		ImGui::PopTextWrapPos();
	}

	void Tooltip(const char* text)
	{
		if (!ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip | ImGuiHoveredFlags_AllowWhenDisabled))
			return;
		ImGui::BeginTooltip();
		ImGui::PushTextWrapPos(ImGui::GetFontSize() * 28.0f);
		ImGui::TextUnformatted(text);
		ImGui::PopTextWrapPos();
		ImGui::EndTooltip();
	}

	// ---- Command arguments ----

	struct Arguments
	{
		uint32_t id = 0;                 // from "id=<n>", else 0 (the selected character)
		std::vector<std::string> words;  // the rest, lower case
	};

	Arguments Parse(const std::string& text)
	{
		Arguments out;
		std::istringstream stream(text);
		std::string word;
		while (stream >> word)
		{
			for (char& c : word)
				c = static_cast<char>(tolower(static_cast<unsigned char>(c)));
			if (word.compare(0, 3, "id=") == 0)
				out.id = static_cast<uint32_t>(strtoul(word.c_str() + 3, nullptr, 0));
			else
				out.words.push_back(word);
		}
		return out;
	}

	// The two ids of a character's script entity (render thread, guarded): its entity, then the composite instance it is in
	bool ReadHandle(const uint8_t* character, uint32_t& entity, uint32_t& instance)
	{
		__try
		{
			entity = *reinterpret_cast<const uint32_t*>(character + 0x54);
			instance = *reinterpret_cast<const uint32_t*>(character + 0x58);
			return entity != 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// The character a command acts on, read on the render thread: the one named by id, else the selected one
	struct Target
	{
		uint32_t id = 0;
		std::string name;
		int characterClass = -1;
		bool player = false, dead = false;
		const uint8_t* object = nullptr;
		uint32_t entity = 0, instance = 0;
	};

	bool FindTarget(uint32_t id, Target& out)
	{
		if (!id)
			id = DEBUG_MENU::SelectedId();
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
			if (character.id == id)
			{
				out.id = character.id;
				out.name = character.name;
				out.characterClass = character.characterClass;
				out.player = character.player;
				out.dead = character.dead;
				out.object = character.object;
				return true;
			}
		return false;
	}

	DEBUG_MENU::CommandResult Fail(const std::string& message)
	{
		DEBUG_MENU::CommandResult result;
		result.ok = false;
		result.message = message;
		return result;
	}

	DEBUG_MENU::CommandResult Done(const std::string& message)
	{
		DEBUG_MENU::CommandResult result;
		result.message = message;
		return result;
	}

	// The target and its state, read on the render thread. What is read is kept by the work itself (a wait that times out
	// leaves the work to run later, after the command has returned).
	struct Read
	{
		Target target;
		AI_STATE::State state;
		bool found = false, read = false;
	};

	bool TargetFor(uint32_t id, Target& out, std::string& error, AI_STATE::State* state = nullptr)
	{
		std::shared_ptr<Read> shared = std::make_shared<Read>();
		const DEBUG_MENU::CommandResult answer = DEBUG_MENU::AnswerOnRenderThread([id, shared]() -> DEBUG_MENU::CommandResult
		{
			shared->found = FindTarget(id, shared->target);
			if (shared->found)
			{
				shared->read = AI_STATE::Read(shared->target.object, shared->state);
				ReadHandle(shared->target.object, shared->target.entity, shared->target.instance);
			}
			DEBUG_MENU::CommandResult result;
			result.ok = shared->found;
			return result;
		});
		if (!answer.ok)
		{
			error = answer.message.empty() ? (id ? "no character with that unique id" : "no character selected (select one, or give id=<unique id>)") : answer.message;
			return false;
		}
		out = shared->target;
		if (state)
		{
			if (!shared->read)
			{
				error = "its state could not be read";
				return false;
			}
			*state = shared->state;
		}
		return true;
	}

	// ---- Changes, on the AI thread ----

	// Runs a change on one character on the AI thread, by its unique id, and gives what happened
	using Change = bool (*)(uint8_t* character, int value);

	// What a change to a character's health came to, read straight after it on the AI thread
	std::string HealthAfter(const uint8_t* character)
	{
		AI_STATE::State state;
		if (!AI_STATE::Read(character, state))
			return "";
		char text[96];
		snprintf(text, sizeof(text), " (health now %d, %s%s)", state.health, AI_DEBUG::HealthName(state.healthState), state.noDamage ? ", set to take no damage" : "");
		return text;
	}

	DEBUG_MENU::CommandResult ChangeOnAiThread(uint32_t id, const std::string& what, const std::string& name, Change change, int value, bool health = false)
	{
		return DEBUG_MENU::AnswerOnAiThread([id, what, name, change, value, health]() -> DEBUG_MENU::CommandResult
		{
			uint8_t* character = AI_STATE::FindCharacter(id);
			if (!character)
				return Fail(what + " - " + name + ": it is gone");
			if (!change(character, value))
				return Fail(what + " - " + name + ": not done (it has no part for that, or this release of the game hasn't it)");
			return Done(what + " - " + name + ": done" + (health ? HealthAfter(character) : std::string()));
		});
	}

	void AskChange(uint32_t id, const std::string& what, const std::string& name, Change change, int value, bool health = false)
	{
		DEBUG_MENU::OnAiThread([id, what, name, change, value, health]()
		{
			uint8_t* character = AI_STATE::FindCharacter(id);
			if (!character)
				SetStatus(what + " - " + name + ": it is gone");
			else if (health)
				SetStatus(what + " - " + name + (change(character, value) ? ": done" + HealthAfter(character) : std::string(": not done (it has no part for that)")));
			else
				SetStatus(what + " - " + name + (change(character, value) ? ": done" : ": not done (it has no part for that)"));
		});
	}

	bool AllowMotivation(uint8_t* character, int motivation) { return AI_STATE::ApplyMotivation(character, motivation, 0); }
	bool ForbidMotivation(uint8_t* character, int motivation) { return AI_STATE::ApplyMotivation(character, motivation, 1); }
	bool DefaultMotivations(uint8_t* character, int) { return AI_STATE::ApplyMotivation(character, 1, 2); }
	bool SetHealth(uint8_t* character, int health) { return AI_STATE::SetHealth(character, health); }
	bool Damage(uint8_t* character, int amount) { return AI_STATE::Damage(character, amount); }
	bool Burn(uint8_t* character, int) { return AI_STATE::Burn(character); }
	bool Kill(uint8_t* character, int) { return AI_STATE::Kill(character); }

	// The damage amounts: a tenth of its most health, or all but one point of what it has
	int TenthOfHealth(const AI_STATE::State& state)
	{
		return (std::max)(1, static_cast<int>(state.maxHealth * state.healthModifier * 0.1f));
	}

	// ---- Forced moods ----

	// The forced moods, by unique id (any thread, under the lock); those to let go; whether the AI thread has work queued
	std::mutex g_moodMutex;
	std::map<uint32_t, int> g_forcedMoods;
	std::vector<uint32_t> g_moodsToRelease;
	std::atomic<bool> g_moodWorkQueued = false;

	void ForceMood(uint32_t id, int mood)
	{
		std::lock_guard<std::mutex> lock(g_moodMutex);
		if (mood < 0)
		{
			if (g_forcedMoods.erase(id))
				g_moodsToRelease.push_back(id);
		}
		else
			g_forcedMoods[id] = mood;
	}

	int ForcedMood(uint32_t id)
	{
		std::lock_guard<std::mutex> lock(g_moodMutex);
		const auto found = g_forcedMoods.find(id);
		return found == g_forcedMoods.end() ? -1 : found->second;
	}

	// Render thread, every frame: the forced moods held on the top layer (pushed afresh each AI frame, as the game's own
	// pushes on that layer last only their time), and those let go put back to no time left on it (the layer then counts
	// for nothing). Forced moods of characters that are gone are dropped.
	void ApplyMoods()
	{
		// (Copied out as a list: a map allocates as it is made, and this runs every frame)
		std::vector<std::pair<uint32_t, int>> forced;
		std::vector<uint32_t> release;
		{
			std::lock_guard<std::mutex> lock(g_moodMutex);
			if (g_forcedMoods.empty() && g_moodsToRelease.empty())
				return;
			for (auto it = g_forcedMoods.begin(); it != g_forcedMoods.end();)
			{
				bool alive = false;
				for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
					if (character.id == it->first && !character.dead)
						alive = true;
				it = alive ? std::next(it) : g_forcedMoods.erase(it);
			}
			if (g_moodWorkQueued)
				return;
			forced.assign(g_forcedMoods.begin(), g_forcedMoods.end());
			release.swap(g_moodsToRelease);
		}
		if (forced.empty() && release.empty())
			return;
		g_moodWorkQueued = true;
		DEBUG_MENU::OnAiThread([forced, release]()
		{
			for (const auto& entry : forced)
				if (uint8_t* character = AI_STATE::FindCharacter(entry.first))
					AI_STATE::PushMood(character, kTopMoodLayer, entry.second, kForEver);
			for (uint32_t id : release)
				if (uint8_t* character = AI_STATE::FindCharacter(id))
					AI_STATE::PushMood(character, kTopMoodLayer, 0, 0.0f);
			g_moodWorkQueued = false;
		});
	}

	// ---- Despawned characters, for spawning again ----

	// A character despawned here: its Character entity (the two ids its handle holds), and, once spawn has been asked of it
	// again, how far the game has got (read from the entity on the entity thread). The game makes the character again
	// where the level placed its Character entity, not where it was despawned, and only once it has a free character and
	// has loaded the character's assets; the entry goes when the entity says its character is in the level.
	struct Despawned
	{
		uint32_t entity = 0, instance = 0;
		std::string name;
		int characterClass = -1;
		bool respawnAsked = false;
		bool looked = false;             // its entity looked for at least once
		bool haveStage = false;          // false: the entity is not in the running level (or not looked for yet)
		AI_STATE::SpawnStage stage;
	};
	std::mutex g_despawnedMutex;
	std::vector<Despawned> g_despawned;
	std::atomic<bool> g_stagesQueued = false;
	double g_stagesAskedAt = -100.0;     // render thread

	bool SameEntity(const Despawned& a, const Despawned& b)
	{
		return a.entity == b.entity && a.instance == b.instance;
	}

	std::string StageText(const Despawned& who)
	{
		const std::string asked = who.respawnAsked ? "spawn asked" : "";
		if (!who.haveStage)
		{
			if (!who.looked)
				return who.respawnAsked ? asked : "despawned";
			return (who.respawnAsked ? asked + "; " : std::string()) + "its entity is not in the running level";
		}
		std::string text = who.respawnAsked ? asked + ": " + AI_STATE::SpawnStageName(who.stage.stage) : AI_STATE::SpawnStageName(who.stage.stage);
		if (who.respawnAsked && !who.stage.spawned)
			text += " (the spawn hasn't reached its entity)";
		if (!who.stage.active)
			text += " (its entity isn't active, so a spawn waits until the game makes it active again)";
		return text;
	}

	// Entity thread: each entry's stage read from its entity; those asked to spawn whose character is back in the level
	// leave the list
	void RefreshStages()
	{
		std::vector<Despawned> list;
		{
			std::lock_guard<std::mutex> lock(g_despawnedMutex);
			list = g_despawned;
		}
		for (Despawned& who : list)
			who.haveStage = AI_STATE::ReadSpawnStage(who.entity, who.instance, who.stage);
		std::vector<std::string> back;
		{
			std::lock_guard<std::mutex> lock(g_despawnedMutex);
			for (const Despawned& read : list)
				for (Despawned& kept : g_despawned)
					if (SameEntity(kept, read))
					{
						kept.looked = true;
						kept.haveStage = read.haveStage;
						kept.stage = read.stage;
					}
			for (const Despawned& who : g_despawned)
				if (who.respawnAsked && who.haveStage && who.stage.stage == AI_STATE::kStageInLevel)
					back.push_back(who.name);
			g_despawned.erase(std::remove_if(g_despawned.begin(), g_despawned.end(), [](const Despawned& who)
			{
				return who.respawnAsked && who.haveStage && who.stage.stage == AI_STATE::kStageInLevel;
			}), g_despawned.end());
		}
		for (const std::string& name : back)
			SetStatus("respawn - " + name + ": back in the level (where the level placed it, with a new unique id)");
	}

	// Render thread, every frame: the stages read again twice a second while anything is listed
	void AskStages()
	{
		{
			std::lock_guard<std::mutex> lock(g_despawnedMutex);
			if (g_despawned.empty())
				return;
		}
		const double now = ImGui::GetTime();
		if (g_stagesQueued || now - g_stagesAskedAt < 0.5)
			return;
		g_stagesAskedAt = now;
		g_stagesQueued = true;
		DEBUG_MENU::OnEntityThread([]()
		{
			RefreshStages();
			g_stagesQueued = false;
		});
	}

	// Entity thread: despawn or spawn asked of the entity, and the list kept up to date with what happened
	DEBUG_MENU::CommandResult CallSpawn(const Despawned& who, bool spawn)
	{
		std::string message;
		const bool done = AI_STATE::CallSpawnMethod(who.entity, who.instance, spawn, message);
		if (done)
		{
			std::lock_guard<std::mutex> lock(g_despawnedMutex);
			if (spawn)
			{
				for (Despawned& kept : g_despawned)
					if (SameEntity(kept, who))
						kept.respawnAsked = true;
				message += "; it comes back where the level placed it once the game has a free character and has loaded it"
					" (ai.list_despawned shows how far it has got)";
			}
			else
			{
				g_despawned.erase(std::remove_if(g_despawned.begin(), g_despawned.end(), [&who](const Despawned& d) { return SameEntity(d, who); }), g_despawned.end());
				Despawned added = who;
				added.respawnAsked = false;
				added.looked = false;
				added.haveStage = false;
				g_despawned.push_back(added);
			}
		}
		DEBUG_MENU::CommandResult result;
		result.ok = done;
		result.message = std::string(spawn ? "respawn" : "despawn") + " - " + who.name + ": " + message;
		return result;
	}

	// From the window: on the entity thread, the answer kept as the tab's last line
	void AskSpawn(const Despawned& who, bool spawn)
	{
		DEBUG_MENU::OnEntityThread([who, spawn]()
		{
			SetStatus(CallSpawn(who, spawn).message);
		});
	}

	DEBUG_MENU::CommandResult SpawnNow(const Despawned& who, bool spawn)
	{
		return DEBUG_MENU::AnswerOnEntityThread([who, spawn]() -> DEBUG_MENU::CommandResult
		{
			return CallSpawn(who, spawn);
		});
	}

	// ---- The State tab ----

	// When each flag of the selected character last changed (render thread)
	uint32_t g_flagsOf = 0;
	bool g_flagsSeen[AI_STATE::kStateFlags] = {};
	double g_flagChangedAt[AI_STATE::kStateFlags] = {};
	bool g_onlySetFlags = true;
	char g_flagFilter[48] = {};

	void TrackFlags(const AI_STATE::State& state)
	{
		const double now = ImGui::GetTime();
		if (state.id != g_flagsOf)
		{
			g_flagsOf = state.id;
			memcpy(g_flagsSeen, state.flags, sizeof(g_flagsSeen));
			for (double& at : g_flagChangedAt)
				at = -100.0;
			return;
		}
		for (int i = 0; i < AI_STATE::kStateFlags; i++)
			if (state.flags[i] != g_flagsSeen[i])
			{
				g_flagsSeen[i] = state.flags[i];
				g_flagChangedAt[i] = now;
			}
	}

	std::string CommandText(const AI_STATE::Command& command)
	{
		if (!command.have)
			return "none";
		char text[160];
		snprintf(text, sizeof(text), "%s%s%s%s, %s%s", command.name[0] ? command.name : "", command.name[0] ? " (" : "", AI_STATE::CommandTypeName(command.type),
			command.name[0] ? ")" : "", AI_STATE::CommandStatusName(command.status), command.persistent ? ", lasting" : "");
		return text;
	}

	void DrawApplicator(const char* label, const AI_STATE::Applicator& applicator)
	{
		ImGui::Text("%s: %s", label, CommandText(applicator.current).c_str());
		if (applicator.current.have && applicator.current.fromScript)
		{
			ImGui::SameLine();
			ImGui::TextDisabled("(entity 0x%08X in composite instance 0x%08X)", applicator.current.entity, applicator.current.instance);
		}
		if (applicator.next.have)
			ImGui::Text("    then: %s%s", CommandText(applicator.next).c_str(), applicator.queued ? " (queued)" : "");
		if (applicator.suspended)
			ImGui::TextDisabled("    suspended");
	}

	void DrawOrders(const AI_STATE::State& state)
	{
		ImGui::SeparatorText("Orders");
		DrawApplicator("From a script", state.scripted);
		Tooltip("The command a script gave it (a command entity of the level's scripts, by its name there): the one running and the one after it.");
		DrawApplicator("From its own tasks", state.task);
		Tooltip("The command its behaviour tree's tasks gave it.");
		if (state.underPlayerControl)
			ImGui::TextUnformatted("Under a player's control");

		auto pending = [](int now, int next, const char* (*name)(int)) -> std::string
		{
			// (a state the machine has no name for is its "nothing pending")
			return next >= 0 && next != now && strcmp(name(next), "?") != 0 ? std::string(name(now)) + " -> " + name(next) : std::string(name(now));
		};
		ImGui::Text("Body: %s; moving: %s; cover: %s", pending(state.highLevel, state.highLevelPending, AI_STATE::HighLevelName).c_str(),
			pending(state.general, state.generalPending, AI_STATE::GeneralName).c_str(), pending(state.cover, state.coverPending, AI_STATE::CoverName).c_str());
		Tooltip("The layered states its body's orders put it in: the overall one, the moving machine's and the cover machine's (an arrow: the state it is about to go to).");
		if (state.dead > 0 || (state.deadPending > 0 && state.deadPending < 4 && state.deadPending != state.dead))
			ImGui::Text("Dying: %s (%.1f s)", pending(state.dead, state.deadPending, AI_STATE::DeadName).c_str(), state.deadTime);
		ImGui::Text("Idles: %s (scripted: %s); idle job meter %.1f of %.1f", AI_STATE::IdleStyleName(state.currentIdle), AI_STATE::IdleStyleName(state.scriptedIdle), state.idleJobMeter, state.idleJobMeterMax);

		if (state.requestCount == 0)
		{
			ImGui::TextDisabled("Nothing asked of its body (%d request(s) finished).", state.finishedCount);
			return;
		}
		ImGui::Text("Asked of its body (%d, %d finished):", state.requestTotal, state.finishedCount);
		if (!ImGui::BeginTable("requests", 3, ImGuiTableFlags_RowBg | ImGuiTableFlags_BordersInnerV | ImGuiTableFlags_SizingStretchProp))
			return;
		ImGui::TableSetupColumn("Request");
		ImGui::TableSetupColumn("How it went");
		ImGui::TableSetupColumn("Where");
		ImGui::TableHeadersRow();
		for (int i = 0; i < state.requestCount; i++)
		{
			const AI_STATE::Request& request = state.requests[i];
			ImGui::TableNextRow();
			ImGui::TableSetColumnIndex(0);
			ImGui::TextUnformatted(AI_STATE::RequestTypeName(request.type));
			ImGui::TableSetColumnIndex(1);
			ImGui::TextUnformatted(AI_STATE::RequestStatusName(request.status));
			ImGui::TableSetColumnIndex(2);
			if (request.haveDestination)
				ImGui::Text("%.2f, %.2f, %.2f", request.destination[0], request.destination[1], request.destination[2]);
		}
		ImGui::EndTable();
	}

	void DrawBody(const AI_STATE::State& state)
	{
		ImGui::SeparatorText("Doing");
		if (state.action >= 0)
		{
			ImGui::Text("Action: %s (before: %s)%s%s%s", AI_STATE::ActionName(state.action), AI_STATE::ActionName(state.previousAction),
				state.actionStarted ? ", started" : "", state.actionFinished ? ", finished" : "", state.actionInterruptable ? ", can be interrupted" : "");
		}
		else
			ImGui::TextDisabled("No action.");
		char layers[512] = {};
		for (int layer = 0; layer < AI_STATE::kSecondaryLayers; layer++)
		{
			if (state.secondary[layer] < 0)
				continue;
			char part[96];
			snprintf(part, sizeof(part), "%s%s: %s", layers[0] ? "; " : "", AI_STATE::SecondaryLayerName(layer), AI_STATE::SecondaryActionName(state.secondary[layer]));
			strcat_s(layers, part);
		}
		ImGui::TextWrapped("Layered on it: %s", layers[0] ? layers : "nothing");
	}

	void DrawFlags(const AI_STATE::State& state)
	{
		ImGui::SeparatorText("State flags");
		ImGui::Text("Alliance: %s (at spawn: %s); traversal: %s; stress %d of %d", AI_STATE::AllianceName(state.alliance), AI_STATE::AllianceName(state.allianceAtSpawn),
			AI_STATE::TraversalName(state.traversal), state.stress, state.maxStress);
		ImGui::TextWrapped("Takes no damage: %s; doesn't react to hits: %s; very important: %s; untargetable: %s", AI_STATE::SwitchOwners(state.noDamage).c_str(),
			AI_STATE::SwitchOwners(state.noReact).c_str(), AI_STATE::SwitchOwners(state.important).c_str(), AI_STATE::SwitchOwners(state.untargetable).c_str());
		Tooltip("Each is on while any part of the game has it set, and says which: an action, an activity, combat, a script, a cutscene or debugging.");
		ImGui::Checkbox("Only those set", &g_onlySetFlags);
		ImGui::SameLine();
		ImGui::SetNextItemWidth(180.0f * DEBUG_MENU::Scale());
		ImGui::InputTextWithHint("##flagfilter", "filter", g_flagFilter, sizeof(g_flagFilter));
		ImGui::SameLine();
		ImGui::TextDisabled("(read only; brighter: changed in the last 2 s)");
		const double now = ImGui::GetTime();
		for (int set = AI_STATE::kLogic; set <= AI_STATE::kIndependent; set++)
		{
			if (!ImGui::CollapsingHeader(AI_STATE::FlagSetName(static_cast<AI_STATE::FlagSet>(set)), ImGuiTreeNodeFlags_DefaultOpen))
				continue;
			ImGui::PushID(set);
			if (ImGui::BeginTable("flags", 3, ImGuiTableFlags_SizingStretchSame))
			{
				for (int i = 0; i < AI_STATE::kStateFlags; i++)
				{
					const AI_STATE::FlagInfo& flag = AI_STATE::Flag(i);
					if (flag.set != set)
						continue;
					const bool recent = now - g_flagChangedAt[i] < 2.0;
					if (g_onlySetFlags && !state.flags[i] && !recent)
						continue;
					if (g_flagFilter[0])
					{
						std::string label = flag.label, filter = g_flagFilter;
						for (char& c : label) c = static_cast<char>(tolower(static_cast<unsigned char>(c)));
						for (char& c : filter) c = static_cast<char>(tolower(static_cast<unsigned char>(c)));
						if (label.find(filter) == std::string::npos)
							continue;
					}
					ImGui::TableNextColumn();
					const ImU32 colour = recent ? IM_COL32(255, 230, 120, 255) : state.flags[i] ? IM_COL32(230, 230, 230, 255) : IM_COL32(130, 130, 130, 255);
					ImGui::PushStyleColor(ImGuiCol_Text, colour);
					// (wrapped to its column: a long label goes on to a second line, lined up under the label's start rather
					// than under the tick, instead of running under the next column)
					ImGui::BeginGroup();
					ImGui::TextUnformatted(state.flags[i] ? "[x]" : "[ ]");
					ImGui::SameLine(0.0f, ImGui::CalcTextSize(" ").x);
					ImGui::TextWrapped("%s", flag.label);
					ImGui::EndGroup();
					ImGui::PopStyleColor();
					if (ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
						ImGui::SetTooltip("Reported over the Live Link as %s", flag.key);
				}
				ImGui::EndTable();
			}
			ImGui::PopID();
		}
	}

	void DrawStateTab()
	{
		DEBUG_MENU::CharacterPicker("Character");
		const DEBUG_MENU::Character* selected = DEBUG_MENU::Selected();
		if (!selected)
		{
			ImGui::TextDisabled("Select a character (here or in the Characters tab).");
			return;
		}
		AI_STATE::State state;
		if (!AI_STATE::Read(selected->object, state))
		{
			ImGui::TextDisabled("Its state could not be read.");
			return;
		}
		TrackFlags(state);
		ImGui::Text("%s - %s%s, unique id %u", selected->name.c_str(), DEBUG_MENU::ClassName(selected->characterClass), selected->dead ? " (dead)" : "", selected->id);
		DrawOrders(state);
		DrawBody(state);
		DrawFlags(state);
	}

	// ---- The Control tab ----

	// Ticks asked for that the character hasn't shown yet (render thread): the motivation, what was asked and when
	struct Asked
	{
		uint32_t id;
		int motivation;
		bool allowed;
		double at;
	};
	std::vector<Asked> g_asked;

	void DrawMotivations(const DEBUG_MENU::Character& character, const AI_STATE::State& state)
	{
		ImGui::SeparatorText("Motivations");
		if (!state.haveMotivations)
		{
			ImGui::TextDisabled("It has none (only NPCs have them).");
			return;
		}
		ImGui::PushTextWrapPos(0.0f);
		ImGui::TextDisabled("Each lets a family of its behaviour tree's branches run: untick one to keep it out of that behaviour.");
		ImGui::PopTextWrapPos();
		const double now = ImGui::GetTime();
		g_asked.erase(std::remove_if(g_asked.begin(), g_asked.end(), [&](const Asked& asked)
		{
			return now - asked.at > 1.5 || (asked.id == state.id && state.motivations[asked.motivation - 1] == asked.allowed);
		}), g_asked.end());
		const uint32_t id = character.id;
		const std::string name = character.name;
		if (ImGui::Button("Back to its defaults"))
			AskChange(id, "motivations back to defaults", name, DefaultMotivations, 0);
		Tooltip("Every motivation as it was when it spawned.");
		ImGui::SameLine();
		bool idle = state.motivations[AI_STATE::kForceIdleMotivation - 1];
		for (const Asked& asked : g_asked)
			if (asked.id == id && asked.motivation == AI_STATE::kForceIdleMotivation)
				idle = asked.allowed;
		if (ImGui::Checkbox("Force idle", &idle))
		{
			g_asked.push_back({ id, AI_STATE::kForceIdleMotivation, idle, now });
			AskChange(id, idle ? "force idle on" : "force idle off", name, idle ? AllowMotivation : ForbidMotivation, AI_STATE::kForceIdleMotivation);
		}
		Tooltip("A debug motivation: the behaviour tree's idle branch for it runs ahead of the rest while this is on (where the tree has it).");
		if (!ImGui::BeginTable("motivations", 3, ImGuiTableFlags_SizingStretchSame))
			return;
		for (int motivation = 1; motivation <= AI_STATE::kMotivations; motivation++)
		{
			if (motivation == AI_STATE::kForceIdleMotivation)
				continue;
			bool allowed = state.motivations[motivation - 1];
			for (const Asked& asked : g_asked)
				if (asked.id == id && asked.motivation == motivation)
					allowed = asked.allowed;
			ImGui::TableNextColumn();
			char label[96];
			snprintf(label, sizeof(label), "%s##motivation%d", AI_STATE::MotivationName(motivation), motivation);
			if (ImGui::Checkbox(label, &allowed))
			{
				g_asked.push_back({ id, motivation, allowed, now });
				AskChange(id, std::string(AI_STATE::MotivationName(motivation)) + (allowed ? " allowed" : " forbidden"), name, allowed ? AllowMotivation : ForbidMotivation, motivation);
			}
		}
		ImGui::EndTable();
	}

	void DrawMood(const DEBUG_MENU::Character& character, const AI_STATE::State& state)
	{
		ImGui::SeparatorText("Mood");
		if (!state.haveMood)
		{
			ImGui::TextDisabled("It has no moods.");
			return;
		}
		static const char* const kLayers[] = { "AI", "Script", "Other code", "Dialogue" };
		ImGui::Text("Shown: %s", AI_DEBUG::MoodName(state.mood));
		for (int layer = 0; layer < AI_STATE::kMoodLayers; layer++)
		{
			const float time = state.moodTime[layer];
			if (time < -0.5f)
				ImGui::BulletText("%s layer: %s, for good", kLayers[layer], AI_DEBUG::MoodName(state.moodLayer[layer]));
			else if (time > 0.001f)
				ImGui::BulletText("%s layer: %s, %.1f s left", kLayers[layer], AI_DEBUG::MoodName(state.moodLayer[layer]), time);
		}
		const int forced = ForcedMood(character.id);
		const char* preview = forced < 0 ? "(not forced)" : AI_DEBUG::MoodName(forced);
		ImGui::SetNextItemWidth(200.0f * DEBUG_MENU::Scale());
		if (ImGui::BeginCombo("Force mood", preview))
		{
			if (ImGui::Selectable("(not forced)", forced < 0))
			{
				ForceMood(character.id, -1);
				SetStatus("mood let go - " + character.name);
			}
			for (int mood = 0; mood < 7; mood++)
				if (ImGui::Selectable(AI_DEBUG::MoodName(mood), forced == mood))
				{
					ForceMood(character.id, mood);
					SetStatus(std::string("mood forced to ") + AI_DEBUG::MoodName(mood) + " - " + character.name);
				}
			ImGui::EndCombo();
		}
		Tooltip("Held on the top (dialogue) mood layer every AI frame until let go. The face and the voice follow the mood shown.");
	}

	void DrawGauge(const AI_STATE::Gauge& gauge, const char* name)
	{
		if (!gauge.have)
			return;
		char text[96];
		snprintf(text, sizeof(text), "%s: %.2f%s", name, gauge.amount, gauge.paused ? " (paused)" : "");
		const float scale = DEBUG_MENU::Scale();
		const ImVec2 corner = ImGui::GetCursorScreenPos();
		const ImVec2 size(ImGui::GetContentRegionAvail().x, ImGui::GetTextLineHeight() + 2.0f * scale);
		ImDrawList* drawList = ImGui::GetWindowDrawList();
		const float full = gauge.thresholds[5] > 0.0f ? gauge.thresholds[5] : 1.0f;
		const float fraction = (std::max)(0.0f, (std::min)(1.0f, gauge.amount / full));
		drawList->AddRectFilled(corner, ImVec2(corner.x + size.x, corner.y + size.y), IM_COL32(20, 20, 20, 200));
		drawList->AddRectFilled(corner, ImVec2(corner.x + size.x * fraction, corner.y + size.y), IM_COL32(255, 150, 60, 230));
		for (int i = 1; i < 5; i++)
		{
			const float tick = gauge.thresholds[i] / full;
			if (tick <= 0.0f || tick > 1.0f)
				continue;
			const float x = corner.x + size.x * tick;
			drawList->AddLine(ImVec2(x, corner.y - 2.0f), ImVec2(x, corner.y + size.y + 2.0f), IM_COL32(255, 255, 255, 220), 1.0f);
		}
		drawList->AddText(ImVec2(corner.x + 4.0f * scale, corner.y + scale), IM_COL32(255, 255, 255, 255), text);
		ImGui::Dummy(size);
	}

	void DrawHealth(const DEBUG_MENU::Character& character, const AI_STATE::State& state)
	{
		ImGui::SeparatorText("Health");
		const int most = (std::max)(1, static_cast<int>(state.maxHealth * state.healthModifier));
		ImGui::Text("%d of %d (%s)%s", state.health, most, AI_DEBUG::HealthName(state.healthState), state.noDamage ? " - set to take no damage" : "");
		if (character.player)
		{
			ImGui::TextDisabled("The player's health is in Characters > Player.");
			return;
		}
		const uint32_t id = character.id;
		const std::string name = character.name;
		const bool alive = !character.dead && state.health > 0;
		ImGui::BeginDisabled(!alive);
		// The slider keeps its own value while it is dragged, and hands it over when let go
		static int s_health = 0;
		static bool s_dragging = false;
		if (!s_dragging)
			s_health = state.health;
		ImGui::SetNextItemWidth(260.0f * DEBUG_MENU::Scale());
		ImGui::SliderInt("##health", &s_health, 1, most);
		s_dragging = ImGui::IsItemActive();
		if (ImGui::IsItemDeactivatedAfterEdit())
			AskChange(id, "health set to " + std::to_string(s_health), name, SetHealth, s_health, true);
		Tooltip("Sets its health outright (at least 1).");
		ImGui::SameLine();
		ImGui::TextUnformatted("Set health");
		if (ImGui::Button("Damage 10%"))
			AskChange(id, "damage", name, Damage, TenthOfHealth(state), true);
		Tooltip("A tenth of its most health as damage: it reacts as to a hit.");
		ImGui::SameLine();
		// (at 1 already there is nothing to take: a point of damage would kill it)
		ImGui::BeginDisabled(state.health <= 1);
		if (ImGui::Button("Down to 1"))
			AskChange(id, "damage down to 1", name, Damage, state.health - 1, true);
		ImGui::EndDisabled();
		Tooltip("All its health but one point as damage.");
		ImGui::SameLine();
		if (ImGui::Button("Set on fire"))
			AskChange(id, "set on fire", name, Burn, 0, true);
		Tooltip("Burns it for 5 s.");
		ImGui::SameLine();
		ImGui::BeginDisabled(character.characterClass == kAlienClass);
		if (ImGui::Button("Kill"))
			AskChange(id, "kill", name, Kill, 0, true);
		ImGui::EndDisabled();
		Tooltip(character.characterClass == kAlienClass ? "The alien isn't meant to die: the game has no death for it." : "Kills it as the game's own death actions do. A character set to take no damage doesn't die.");
		ImGui::EndDisabled();
		for (int i = 0; i < AI_STATE::kGauges; i++)
			DrawGauge(state.gauges[i], AI_STATE::GaugeName(i));
		ImGui::Text("Stress %d of %d", state.stress, state.maxStress);
	}

	void DrawSpawning(const DEBUG_MENU::Character& character)
	{
		ImGui::SeparatorText("Spawning");
		Despawned who;
		who.name = character.name;
		who.characterClass = character.characterClass;
		const bool haveHandle = ReadHandle(character.object, who.entity, who.instance);
		ImGui::BeginDisabled(character.player || !haveHandle);
		if (ImGui::Button("Despawn"))
			AskSpawn(who, false);
		ImGui::EndDisabled();
		Tooltip("Calls despawn on its Character entity, as a script would: it leaves the level, and can be spawned again below. Scripts waiting on it may stall.");
		std::vector<Despawned> despawned;
		{
			std::lock_guard<std::mutex> lock(g_despawnedMutex);
			despawned = g_despawned;
		}
		if (despawned.empty())
			return;
		ImGui::TextDisabled("Despawned here:");
		for (size_t i = 0; i < despawned.size(); i++)
		{
			ImGui::PushID(static_cast<int>(i));
			if (ImGui::SmallButton("Respawn"))
				AskSpawn(despawned[i], true);
			Tooltip("Calls spawn on its Character entity, as a level script would. The game makes the character again where the level "
				"placed that entity (not where it was despawned), once it has a free character and has loaded the character's assets.");
			ImGui::SameLine();
			ImGui::TextWrapped("%s (%s): %s - entity 0x%08X in 0x%08X", despawned[i].name.c_str(), DEBUG_MENU::ClassName(despawned[i].characterClass),
				StageText(despawned[i]).c_str(), despawned[i].entity, despawned[i].instance);
			ImGui::PopID();
		}
	}

	void DrawControlTab()
	{
		DEBUG_MENU::CharacterPicker("Character");
		const DEBUG_MENU::Character* selected = DEBUG_MENU::Selected();
		if (!selected)
		{
			ImGui::TextDisabled("Select a character (here or in the Characters tab).");
			DrawStatus();
			return;
		}
		AI_STATE::State state;
		if (!AI_STATE::Read(selected->object, state))
		{
			ImGui::TextDisabled("Its state could not be read.");
			DrawStatus();
			return;
		}
		ImGui::Text("%s - %s%s", selected->name.c_str(), DEBUG_MENU::ClassName(selected->characterClass), selected->dead ? " (dead)" : "");
		ImGui::TextDisabled("Changes happen on the game's AI thread: not while the game is paused.");
		if (!selected->player)
		{
			DrawMotivations(*selected, state);
			DrawMood(*selected, state);
		}
		DrawHealth(*selected, state);
		DrawSpawning(*selected);
		DrawStatus();
	}

	// ---- Commands ----

	std::string Lines(const AI_STATE::State& state, const Target& target)
	{
		std::ostringstream out;
		out << "character=" << target.name << " (" << target.id << ")\n";
		auto command = [&out](const char* label, const AI_STATE::Applicator& applicator)
		{
			out << label << "=" << CommandText(applicator.current);
			if (applicator.current.have && applicator.current.fromScript)
			{
				char ids[48];
				snprintf(ids, sizeof(ids), " entity=%08X instance=%08X", applicator.current.entity, applicator.current.instance);
				out << ids;
			}
			out << "\n" << label << ".next=" << CommandText(applicator.next) << (applicator.queued ? " (queued)" : "") << "\n";
			out << label << ".suspended=" << (applicator.suspended ? 1 : 0) << "\n";
		};
		command("scripted_command", state.scripted);
		command("task_command", state.task);
		out << "body=" << AI_STATE::HighLevelName(state.highLevel) << "\nbody.pending=" << AI_STATE::HighLevelName(state.highLevelPending) << "\n";
		out << "moving=" << AI_STATE::GeneralName(state.general) << "\nmoving.pending=" << AI_STATE::GeneralName(state.generalPending) << "\n";
		out << "cover=" << AI_STATE::CoverName(state.cover) << "\ndying=" << AI_STATE::DeadName(state.dead) << "\n";
		out << "idle=" << AI_STATE::IdleStyleName(state.currentIdle) << "\nidle.scripted=" << AI_STATE::IdleStyleName(state.scriptedIdle) << "\n";
		out << "idle_job_meter=" << state.idleJobMeter << "/" << state.idleJobMeterMax << "\n";
		for (int i = 0; i < state.requestCount; i++)
		{
			const AI_STATE::Request& request = state.requests[i];
			out << "request=" << AI_STATE::RequestTypeName(request.type) << ", " << AI_STATE::RequestStatusName(request.status);
			if (request.haveDestination)
				out << " at " << request.destination[0] << "," << request.destination[1] << "," << request.destination[2];
			out << "\n";
		}
		out << "action=" << (state.action >= 0 ? AI_STATE::ActionName(state.action) : "none") << "\n";
		for (int layer = 0; layer < AI_STATE::kSecondaryLayers; layer++)
			if (state.secondary[layer] >= 0)
				out << "secondary." << layer << "=" << AI_STATE::SecondaryLayerName(layer) << ": " << AI_STATE::SecondaryActionName(state.secondary[layer]) << "\n";
		return out.str();
	}

	// Reads the target's state on the render thread and hands it to a function making the answer
	template <typename Make>
	DEBUG_MENU::CommandResult Report(uint32_t id, Make make)
	{
		return DEBUG_MENU::AnswerOnRenderThread([id, make]() -> DEBUG_MENU::CommandResult
		{
			Target target;
			if (!FindTarget(id, target))
				return Fail(id ? "no character with that unique id" : "no character selected (select one, or give id=<unique id>)");
			AI_STATE::State state;
			if (!AI_STATE::Read(target.object, state))
				return Fail("its state could not be read");
			return Done(make(state, target));
		});
	}

	bool RegisterCommands()
	{
		DEBUG_MENU::AddCommand("ai.state", "[id=<unique id>] the selected character's orders and what its body is doing", [](const std::string& argument)
		{
			return Report(Parse(argument).id, [](const AI_STATE::State& state, const Target& target) { return Lines(state, target); });
		});
		DEBUG_MENU::AddCommand("ai.flags", "[all] [id=<unique id>] its 105 state flags (those set, or all as key=0|1)", [](const std::string& argument)
		{
			const Arguments args = Parse(argument);
			const bool all = std::find(args.words.begin(), args.words.end(), "all") != args.words.end();
			return Report(args.id, [all](const AI_STATE::State& state, const Target& target)
			{
				std::ostringstream out;
				out << "character=" << target.name << " (" << target.id << ")\n";
				out << "alliance=" << AI_STATE::AllianceName(state.alliance) << "\nstress=" << state.stress << "/" << state.maxStress << "\n";
				out << "no_damage=" << AI_STATE::SwitchOwners(state.noDamage) << "\nno_react=" << AI_STATE::SwitchOwners(state.noReact)
					<< "\nimportant=" << AI_STATE::SwitchOwners(state.important) << "\nuntargetable=" << AI_STATE::SwitchOwners(state.untargetable) << "\n";
				for (int i = 0; i < AI_STATE::kStateFlags; i++)
					if (all || state.flags[i])
						out << AI_STATE::Flag(i).key << "=" << (state.flags[i] ? 1 : 0) << "\n";
				return out.str();
			});
		});
		DEBUG_MENU::AddCommand("ai.motivations", "[id=<unique id>] its 35 motivations, key=0|1 (1: allowed)", [](const std::string& argument)
		{
			return Report(Parse(argument).id, [](const AI_STATE::State& state, const Target& target)
			{
				std::ostringstream out;
				out << "character=" << target.name << " (" << target.id << ")\n";
				if (!state.haveMotivations)
					return out.str() + "none\n";
				for (int motivation = 1; motivation <= AI_STATE::kMotivations; motivation++)
					out << AI_STATE::MotivationKey(motivation) << "=" << (state.motivations[motivation - 1] ? 1 : 0) << "\n";
				return out.str();
			});
		});
		DEBUG_MENU::AddCommand("ai.motivation", "<key> on|off, or defaults [id=<unique id>] - allows or forbids a motivation (AI thread)", [](const std::string& argument)
		{
			const Arguments args = Parse(argument);
			Target target;
			std::string error;
			if (!TargetFor(args.id, target, error))
				return Fail(error);
			if (target.player)
				return Fail("the player has no motivations");
			if (!args.words.empty() && args.words[0] == "defaults")
				return ChangeOnAiThread(target.id, "motivations back to defaults", target.name, DefaultMotivations, 0);
			if (args.words.size() < 2)
				return Fail("give a motivation key and on|off (ai.motivations lists the keys), or defaults");
			const int motivation = AI_STATE::MotivationFromKey(args.words[0]);
			if (!motivation)
				return Fail("no motivation called " + args.words[0] + " (ai.motivations lists them)");
			const bool on = args.words[1] == "on" || args.words[1] == "1";
			return ChangeOnAiThread(target.id, std::string(AI_STATE::MotivationKey(motivation)) + (on ? " allowed" : " forbidden"), target.name, on ? AllowMotivation : ForbidMotivation, motivation);
		});
		DEBUG_MENU::AddCommand("ai.force_idle", "on|off [id=<unique id>] - the force idle motivation (AI thread)", [](const std::string& argument)
		{
			const Arguments args = Parse(argument);
			Target target;
			std::string error;
			if (!TargetFor(args.id, target, error))
				return Fail(error);
			if (target.player)
				return Fail("the player has no motivations");
			const bool on = args.words.empty() || args.words[0] == "on" || args.words[0] == "1";
			return ChangeOnAiThread(target.id, on ? "force idle on" : "force idle off", target.name, on ? AllowMotivation : ForbidMotivation, AI_STATE::kForceIdleMotivation);
		});
		DEBUG_MENU::AddCommand("ai.mood", "[id=<unique id>] its mood: shown, on each layer, and forced", [](const std::string& argument)
		{
			return Report(Parse(argument).id, [](const AI_STATE::State& state, const Target& target)
			{
				std::ostringstream out;
				out << "character=" << target.name << " (" << target.id << ")\n";
				if (!state.haveMood)
					return out.str() + "none\n";
				out << "mood=" << AI_STATE::MoodKey(state.mood) << "\n";
				for (int layer = 0; layer < AI_STATE::kMoodLayers; layer++)
					out << "layer." << layer << "=" << AI_STATE::MoodKey(state.moodLayer[layer]) << " time=" << state.moodTime[layer] << "\n";
				const int forced = ForcedMood(target.id);
				out << "forced=" << (forced < 0 ? "no" : AI_STATE::MoodKey(forced)) << "\n";
				return out.str();
			});
		});
		DEBUG_MENU::AddCommand("ai.force_mood", "neutral|scared|angry|happy|sad|suspicious|injured|off [id=<unique id>] - holds its mood until off", [](const std::string& argument)
		{
			const Arguments args = Parse(argument);
			Target target;
			std::string error;
			if (!TargetFor(args.id, target, error))
				return Fail(error);
			if (target.player)
				return Fail("not for the player");
			if (args.words.empty())
				return Fail("give a mood, or off");
			if (args.words[0] == "off")
			{
				ForceMood(target.id, -1);
				return Done("mood let go - " + target.name);
			}
			const int mood = AI_STATE::MoodFromKey(args.words[0]);
			if (mood < 0)
				return Fail("no mood called " + args.words[0]);
			ForceMood(target.id, mood);
			return Done(std::string("mood forced to ") + AI_STATE::MoodKey(mood) + " - " + target.name + " (held from the next AI frame)");
		});

		// Health: each refused for the player (Characters > Player has the player's own)
		struct HealthCommand { const char* name; const char* help; int kind; };
		static const HealthCommand kHealth[] = {
			{ "ai.set_health", "<health> [id=<unique id>] - sets an NPC's health outright (1..its most)", 0 },
			{ "ai.damage", "[amount] [id=<unique id>] - damages an NPC (a tenth of its most when no amount)", 1 },
			{ "ai.damage_to_one", "[id=<unique id>] - damages an NPC down to 1 health", 2 },
			{ "ai.burn", "[id=<unique id>] - sets an NPC on fire for 5 s", 3 },
			{ "ai.kill", "[id=<unique id>] - kills an NPC as the game's death actions do (not the alien)", 4 },
		};
		for (const HealthCommand& entry : kHealth)
		{
			const int kind = entry.kind;
			DEBUG_MENU::AddCommand(entry.name, entry.help, [kind](const std::string& argument)
			{
				const Arguments args = Parse(argument);
				Target target;
				std::string error;
				AI_STATE::State state;
				if (!TargetFor(args.id, target, error, &state))
					return Fail(error);
				if (target.player)
					return Fail("not for the player (Characters > Player has the player's health)");
				if (target.dead || state.health <= 0)
					return Fail(target.name + " is dead");
				const int value = args.words.empty() ? 0 : atoi(args.words[0].c_str());
				switch (kind)
				{
				case 0:
					if (value <= 0)
						return Fail("give the health to set");
					return ChangeOnAiThread(target.id, "health set to " + std::to_string(value), target.name, SetHealth, value, true);
				case 1:
					return ChangeOnAiThread(target.id, "damage", target.name, Damage, value > 0 ? value : TenthOfHealth(state), true);
				case 2:
					if (state.health <= 1)
						return Fail(target.name + " is already at 1 health");
					return ChangeOnAiThread(target.id, "damage down to 1", target.name, Damage, state.health - 1, true);
				case 3:
					return ChangeOnAiThread(target.id, "set on fire", target.name, Burn, 0, true);
				default:
					if (target.characterClass == kAlienClass)
						return Fail("the alien isn't meant to die: the game has no death for it");
					return ChangeOnAiThread(target.id, "kill", target.name, Kill, 0, true);
				}
			});
		}

		DEBUG_MENU::AddCommand("ai.despawn", "[id=<unique id>] - calls despawn on an NPC's Character entity (entity thread)", [](const std::string& argument)
		{
			const Arguments args = Parse(argument);
			Target target;
			std::string error;
			if (!TargetFor(args.id, target, error))
				return Fail(error);
			if (target.player)
				return Fail("not the player");
			if (!target.entity)
				return Fail("its script entity could not be read");
			Despawned who;
			who.entity = target.entity;
			who.instance = target.instance;
			who.name = target.name;
			who.characterClass = target.characterClass;
			return SpawnNow(who, false);
		});
		DEBUG_MENU::AddCommand("ai.respawn", "[all | entity=<hex>] - calls spawn on characters despawned here (the last one when no argument); ai.list_despawned shows how far each has got", [](const std::string& argument)
		{
			std::vector<Despawned> list;
			{
				std::lock_guard<std::mutex> lock(g_despawnedMutex);
				list = g_despawned;
			}
			if (list.empty())
				return Fail("nothing despawned here to spawn again");
			const Arguments args = Parse(argument);
			std::vector<Despawned> chosen;
			if (!args.words.empty() && args.words[0] == "all")
				chosen = list;
			else if (!args.words.empty() && args.words[0].compare(0, 7, "entity=") == 0)
			{
				const uint32_t entity = static_cast<uint32_t>(strtoul(args.words[0].c_str() + 7, nullptr, 16));
				for (const Despawned& who : list)
					if (who.entity == entity)
						chosen.push_back(who);
			}
			else
				chosen.push_back(list.back());
			if (chosen.empty())
				return Fail("no despawned character has that entity (ai.list_despawned lists them)");
			std::string message;
			bool ok = true;
			for (const Despawned& who : chosen)
			{
				const DEBUG_MENU::CommandResult result = SpawnNow(who, true);
				ok = ok && result.ok;
				message += result.message + "\n";
			}
			DEBUG_MENU::CommandResult result;
			result.ok = ok;
			result.message = message;
			return result;
		});
		DEBUG_MENU::AddCommand("ai.list_despawned", "the characters despawned here, one a line: entity, composite instance, name, how far a spawn asked of it has got", [](const std::string&)
		{
			// The stages read afresh on the entity thread (the list as last read when it doesn't come round, the game paused)
			const DEBUG_MENU::CommandResult refreshed = DEBUG_MENU::AnswerOnEntityThread([]() -> DEBUG_MENU::CommandResult
			{
				RefreshStages();
				return Done("");
			});
			std::lock_guard<std::mutex> lock(g_despawnedMutex);
			std::string text = refreshed.ok ? "" : "(stages as last read: " + refreshed.message + ")\n";
			for (const Despawned& who : g_despawned)
			{
				char line[512];
				snprintf(line, sizeof(line), "entity=%08X instance=%08X %s (%s) respawn_asked=%d stage=%s (%d) spawned=%d active=%d updating=%d\n", who.entity, who.instance,
					who.name.c_str(), DEBUG_MENU::ClassName(who.characterClass), who.respawnAsked ? 1 : 0, StageText(who).c_str(), who.haveStage ? who.stage.stage : -1,
					who.haveStage && who.stage.spawned ? 1 : 0, who.haveStage && who.stage.active ? 1 : 0, who.haveStage && who.stage.updating ? 1 : 0);
				text += line;
			}
			return Done(g_despawned.empty() ? text + "none\n" : text);
		});
		return true;
	}

	// Render thread, every frame (the window open or not): the commands named on the first frame (as the other views name
	// theirs), then the forced moods held and the despawned characters' stages followed
	void ApplyOverlay(ImDrawList* /*drawList*/)
	{
		static const bool registered = RegisterCommands();
		(void)registered;
		ApplyMoods();
		AskStages();
	}

	const bool g_stateTab = DEBUG_MENU::AddTab("State", 12, DrawStateTab, "Characters");
	const bool g_controlTab = DEBUG_MENU::AddTab("Control", 14, DrawControlTab, "Characters");
	const bool g_overlay = DEBUG_MENU::AddOverlay(12, ApplyOverlay);
}

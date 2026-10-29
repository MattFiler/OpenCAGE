#include "SCRIPT_DEBUG.h"
#include "DEBUG_MENU.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <memory>
#include <mutex>
#include <string>
#include <vector>

#include <imgui.h>

/*
	Progression (debug menu: Scripts > Progression): what the level's scripts carry beyond the level - read only, nothing
	here is changed (what the game keeps here goes into the saves).

	- Saved variables: a script variable entity set to keep its value (an integer, number or true/false variable marked
	  persistent) writes it to one store the game keeps for the whole game, by the variable's entity id and composite
	  instance id, sorted; the store is saved with the game and every level reads from it, so a value set in one level is
	  seen in the next. The store does not say what kind of value each is: a variable in the running level is looked up
	  (with the game's own lookup of an entity by its two ids) to tell, and one from another level shows its raw value.
	- Objectives: the player's primary objective (its title and text, completed or not), up to ten secondary objectives
	  (each with a position, a marker kind, the level it belongs to and whether it is done) and the exit objective, as the
	  objective script entities set them. Their text is a key into the game's text database, shown with the line it gives.
	- Progression: what the game tracks across the campaign for achievements and the like - the crafting blueprints
	  unlocked, the downloadable missions' objectives done, the storyline's progress and completion, the mission number,
	  how many saves, humans killed, and the missions' "without doing X" achievement checks.

	Read on the entity thread (between frames of the level's scripts, so the store is not growing while it is read),
	about once a second while the tab is open, or when asked. The secondary objectives' positions can be marked over the
	game.

	Addresses are from the Steam retail AI.exe.
*/
namespace
{
	// ---- The game's ----
	constexpr uintptr_t kVariableStore = 0x01354674;   // a pointer to the saved variable store: an array, its first and last
	constexpr uint32_t kStoreFirst = 0x00;             // item (12 bytes each: entity id, instance id, the value)
	constexpr uint32_t kStoreLast = 0x04;
	constexpr uint32_t kMaxVariables = 4096;

	// The entity manager's lookup of an entity by its id and its instance's id: a counted reference, or none
	constexpr uintptr_t kFindEntity = 0x005236A0;
	typedef void** (__thiscall* t_find_entity)(void* manager, void** result, const uint32_t* handle);
	// Releases a counted reference to an entity
	constexpr uintptr_t kReleaseEntity = 0x00198230;
	typedef void(__thiscall* t_release_entity)(void** reference);

	// The game's text lookup (a key to its line in the game's language)
	constexpr uintptr_t kTranslate = 0x00374460;
	typedef bool(__cdecl* t_translate)(const char* key, char* out, uint32_t size);

	// The game's globals (a pointer to them): the player's inventory (a pointer), and the save logic (a pointer)
	constexpr uintptr_t kGameGlobals = 0x012F0C88;
	constexpr uint32_t kGlobalsInventory = 0x44;
	constexpr uint32_t kGlobalsSave = 0x78;

	// The inventory's objectives: the primary (title 128, text 256, completed, show its message), then ten secondaries
	// (title 128, text 128, position, marker kind, level, completed; 0x118 each), then the exit objective (as a secondary),
	// the last one triggered and whether the exit objective is set
	constexpr uint32_t kInventoryObjectives = 0x2DB4;
	constexpr uint32_t kPrimaryTitle = 0x000, kPrimaryText = 0x080, kPrimaryCompleted = 0x180, kPrimaryShowMessage = 0x181;
	constexpr uint32_t kSecondaries = 0x184, kSecondarySize = 0x118, kSecondaryCount = 10;
	constexpr uint32_t kSecondaryTitle = 0x000, kSecondaryText = 0x080, kSecondaryPosition = 0x100, kSecondaryMarker = 0x10C, kSecondaryLevel = 0x110,
		kSecondaryCompleted = 0x114;
	constexpr uint32_t kExitObjective = 0xC74, kLastTriggered = 0xD8C, kExitObjectiveSet = 0xD90;

	// The save logic's progression stats: eight crafting blueprints (an id, unlocked), the downloadable missions'
	// objectives (60 true/false), survivor mode opened, the furthest completion, hunted and avoided time, tutorial flags
	// and one-off events
	constexpr uint32_t kSaveProgression = 0xB48;
	constexpr uint32_t kBlueprints = 0x00, kBlueprintCount = 8, kBlueprintSize = 8;
	constexpr uint32_t kMissionObjectives = 0x90, kMissionObjectiveCount = 60;
	constexpr uint32_t kSurvivorOpened = 0xCC, kMaxCompletion = 0xD0, kHunted = 0xD8, kAvoided = 0xDC, kTutorialFlags = 0xE0, kOneOffEvents = 0xE4;
	// ... and its campaign stats: storyline progress (0 the start, 1 after the station's AI turns, 2 the end), completion,
	// the mission number, saves made, the active mission's level, humans killed, the missions' achievement checks (four
	// pairs of "being checked" and "failed") and iron man mode
	constexpr uint32_t kSaveCampaign = 0xC30;
	constexpr uint32_t kStoryline = 0x04, kCompletion = 0x08, kMission = 0x0C, kSaves = 0x10, kActiveLevel = 0x14, kHumansKilled = 0x18, kChecks = 0x1C, kIronMan = 0x24;

	// ---- What is read ----
	struct Variable
	{
		uint32_t entity = 0, instance = 0, value = 0;
		bool inLevel = false;                // found in the running level
		SCRIPT_DEBUG::EntityId id;           // when found
	};

	struct Objective
	{
		bool used = false;
		char title[128] = {}, text[256] = {};
		std::string titleLine, textLine;     // the lines the keys give (empty: none)
		bool completed = false, showMessage = false;
		float position[3] = {};
		int marker = 0;
		uint32_t level = 0;
	};

	struct Snapshot
	{
		bool ok = false;
		ULONGLONG tick = 0;
		std::vector<Variable> variables;
		uint32_t variableCount = 0;          // all of them (past the ones read)
		bool variablesRead = false;          // (the overlay reads only the objectives: the variables are kept from the last read that had them)
		bool objectivesRead = false;
		Objective primary;
		Objective secondary[kSecondaryCount];
		Objective exit;
		bool exitSet = false;
		uint32_t lastTriggered = 0;
		bool progressionRead = false;
		uint32_t blueprintId[kBlueprintCount] = {};
		bool blueprintDone[kBlueprintCount] = {};
		bool missionObjectives[kMissionObjectiveCount] = {};
		bool survivorOpened = false;
		float maxCompletion = 0.0f;
		uint32_t hunted = 0, avoided = 0, tutorialFlags = 0, oneOffEvents = 0;
		uint32_t storyline = 0, saves = 0, activeLevel = 0, humansKilled = 0;
		float completion = 0.0f, mission = 0.0f;
		uint8_t checks[8] = {};
		bool ironMan = false;
	};

	// The store's items, raw (guarded)
	bool ReadStore(uint32_t* out, uint32_t max, uint32_t& count, uint32_t& total)
	{
		__try
		{
			const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kVariableStore);
			const uint8_t* store = at ? *reinterpret_cast<const uint8_t* const*>(at) : nullptr;
			if (!store)
				return false;
			const uint32_t* first = *reinterpret_cast<const uint32_t* const*>(store + kStoreFirst);
			const uint32_t* last = *reinterpret_cast<const uint32_t* const*>(store + kStoreLast);
			if (!first || last < first)
				return false;
			total = static_cast<uint32_t>((last - first) / 3);
			count = (std::min)(total, max);
			memcpy(out, first, count * 12);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// Looks an entity up by its two ids in the running level and reads it (entity thread)
	void* FindGuarded(t_find_entity find, void* manager, const uint32_t* handle)
	{
		__try
		{
			void* record = nullptr;
			find(manager, &record, handle);
			return record;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	void ReleaseGuarded(t_release_entity release, void* record)
	{
		__try
		{
			if (record)
				release(&record);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
		}
	}

	bool ReadObjective(const uint8_t* at, bool primary, Objective& out)
	{
		__try
		{
			memcpy(out.title, at + (primary ? kPrimaryTitle : kSecondaryTitle), sizeof(out.title));
			out.title[sizeof(out.title) - 1] = '\0';
			const size_t textSize = primary ? 256 : 128;
			memcpy(out.text, at + (primary ? kPrimaryText : kSecondaryText), textSize);
			out.text[textSize - 1] = '\0';
			if (primary)
			{
				out.completed = at[kPrimaryCompleted] != 0;
				out.showMessage = at[kPrimaryShowMessage] != 0;
			}
			else
			{
				memcpy(out.position, at + kSecondaryPosition, sizeof(out.position));
				out.marker = *reinterpret_cast<const int*>(at + kSecondaryMarker);
				out.level = *reinterpret_cast<const uint32_t*>(at + kSecondaryLevel);
				out.completed = at[kSecondaryCompleted] != 0;
			}
			out.used = out.title[0] != '\0' || out.text[0] != '\0';
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	const uint8_t* GlobalsMember(uint32_t offset)
	{
		__try
		{
			const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kGameGlobals);
			const uint8_t* globals = at ? *reinterpret_cast<const uint8_t* const*>(at) : nullptr;
			return globals ? *reinterpret_cast<const uint8_t* const*>(globals + offset) : nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	bool ReadProgression(const uint8_t* save, Snapshot& out)
	{
		__try
		{
			const uint8_t* stats = save + kSaveProgression;
			for (uint32_t i = 0; i < kBlueprintCount; i++)
			{
				out.blueprintId[i] = *reinterpret_cast<const uint32_t*>(stats + kBlueprints + i * kBlueprintSize);
				out.blueprintDone[i] = stats[kBlueprints + i * kBlueprintSize + 4] != 0;
			}
			for (uint32_t i = 0; i < kMissionObjectiveCount; i++)
				out.missionObjectives[i] = stats[kMissionObjectives + i] != 0;
			out.survivorOpened = stats[kSurvivorOpened] != 0;
			out.maxCompletion = *reinterpret_cast<const float*>(stats + kMaxCompletion);
			out.hunted = *reinterpret_cast<const uint32_t*>(stats + kHunted);
			out.avoided = *reinterpret_cast<const uint32_t*>(stats + kAvoided);
			out.tutorialFlags = *reinterpret_cast<const uint32_t*>(stats + kTutorialFlags);
			out.oneOffEvents = *reinterpret_cast<const uint32_t*>(stats + kOneOffEvents);
			const uint8_t* campaign = save + kSaveCampaign;
			out.storyline = *reinterpret_cast<const uint32_t*>(campaign + kStoryline);
			out.completion = *reinterpret_cast<const float*>(campaign + kCompletion);
			out.mission = *reinterpret_cast<const float*>(campaign + kMission);
			out.saves = *reinterpret_cast<const uint32_t*>(campaign + kSaves);
			out.activeLevel = *reinterpret_cast<const uint32_t*>(campaign + kActiveLevel);
			out.humansKilled = *reinterpret_cast<const uint32_t*>(campaign + kHumansKilled);
			memcpy(out.checks, campaign + kChecks, sizeof(out.checks));
			out.ironMan = campaign[kIronMan] != 0;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool CopyBytes(const uint8_t* from, uint8_t* to, size_t count)
	{
		__try
		{
			memcpy(to, from, count);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool TranslateGuarded(t_translate translate, const char* key, char* out, uint32_t size)
	{
		__try
		{
			out[0] = '\0';
			const bool found = translate(key, out, size);
			out[size - 1] = '\0';
			return found;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	std::string Line(const char* key)
	{
		const t_translate translate = reinterpret_cast<t_translate>(DEVTOOLS_RELATIVE_ADDRESS(kTranslate));
		if (!translate || !key[0])
			return std::string();
		char line[512];
		if (!TranslateGuarded(translate, key, line, sizeof(line)) || !line[0] || strcmp(line, key) == 0)
			return std::string();
		return line;
	}

	// Entity thread: reads the objectives and progression, and the saved variables when asked
	void Read(Snapshot& out, bool variables = true)
	{
		out = Snapshot();
		out.tick = GetTickCount64();

		// The saved variables, each looked up in the running level
		std::vector<uint32_t> raw(variables ? kMaxVariables * 3 : 0);
		uint32_t count = 0;
		if (variables && ReadStore(raw.data(), kMaxVariables, count, out.variableCount))
		{
			void* manager = SCRIPT_DEBUG::LevelRunning() ? SCRIPT_DEBUG::EntityManager() : nullptr;
			const t_find_entity find = reinterpret_cast<t_find_entity>(DEVTOOLS_RELATIVE_ADDRESS(kFindEntity));
			const t_release_entity release = reinterpret_cast<t_release_entity>(DEVTOOLS_RELATIVE_ADDRESS(kReleaseEntity));
			out.variables.reserve(count);
			for (uint32_t i = 0; i < count; i++)
			{
				Variable variable;
				variable.entity = raw[i * 3];
				variable.instance = raw[i * 3 + 1];
				variable.value = raw[i * 3 + 2];
				if (manager && find && release)
				{
					const uint32_t handle[2] = { variable.entity, variable.instance };
					void* record = FindGuarded(find, manager, handle);
					if (record)
					{
						variable.inLevel = SCRIPT_DEBUG::ReadEntity(record, variable.id);
						ReleaseGuarded(release, record);
					}
				}
				out.variables.push_back(variable);
			}
			out.variablesRead = true;
		}

		// The objectives
		if (const uint8_t* inventory = GlobalsMember(kGlobalsInventory))
		{
			const uint8_t* objectives = inventory + kInventoryObjectives;
			bool ok = ReadObjective(objectives, true, out.primary);
			for (uint32_t i = 0; i < kSecondaryCount; i++)
				ok = ReadObjective(objectives + kSecondaries + i * kSecondarySize, false, out.secondary[i]) && ok;
			ok = ReadObjective(objectives + kExitObjective, false, out.exit) && ok;
			out.objectivesRead = ok;
			uint8_t bytes[8] = {};
			if (CopyBytes(objectives + kLastTriggered, bytes, sizeof(bytes)))
			{
				memcpy(&out.lastTriggered, bytes, sizeof(out.lastTriggered));
				out.exitSet = bytes[kExitObjectiveSet - kLastTriggered] != 0;
			}
			auto lines = [](Objective& objective)
			{
				if (!objective.used)
					return;
				objective.titleLine = Line(objective.title);
				objective.textLine = Line(objective.text);
			};
			lines(out.primary);
			for (Objective& objective : out.secondary)
				lines(objective);
			lines(out.exit);
		}

		// The progression stats
		if (const uint8_t* save = GlobalsMember(kGlobalsSave))
			out.progressionRead = ReadProgression(save, out);

		out.ok = true;
	}

	std::mutex g_mutex;
	std::shared_ptr<Snapshot> g_snapshot = std::make_shared<Snapshot>();
	std::atomic<bool> g_asked = false;
	std::atomic<bool> g_variablesWanted = false; // a read asked for while another was on its way still gets the variables
	std::atomic<bool> g_markers = false;

	void Ask(bool variables)
	{
		if (variables)
			g_variablesWanted = true;
		if (g_asked.exchange(true))
			return;
		DEBUG_MENU::OnEntityThread([]()
		{
			const bool variables = g_variablesWanted.exchange(false);
			std::shared_ptr<Snapshot> snapshot = std::make_shared<Snapshot>();
			Read(*snapshot, variables);
			{
				std::lock_guard<std::mutex> lock(g_mutex);
				if (!variables)
				{
					snapshot->variables = g_snapshot->variables;
					snapshot->variableCount = g_snapshot->variableCount;
					snapshot->variablesRead = g_snapshot->variablesRead;
				}
				g_snapshot = snapshot;
			}
			g_asked = false;
		});
	}

	std::shared_ptr<Snapshot> Latest()
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		return g_snapshot;
	}

	// What kind of value a variable holds, from its entity's type (when it is in the running level)
	enum class ValueKind { Unknown, Integer, Number, Bool };
	ValueKind KindOf(const Variable& variable)
	{
		if (!variable.inLevel)
			return ValueKind::Unknown;
		const std::string type = SCRIPT_DEBUG::TypeText(variable.id);
		if (type.find("Float") != std::string::npos)
			return ValueKind::Number;
		if (type.find("Bool") != std::string::npos)
			return ValueKind::Bool;
		return ValueKind::Integer;
	}

	std::string ValueText(const Variable& variable)
	{
		char text[96];
		float number;
		memcpy(&number, &variable.value, sizeof(number));
		switch (KindOf(variable))
		{
		case ValueKind::Number: snprintf(text, sizeof(text), "%g", number); break;
		case ValueKind::Bool: snprintf(text, sizeof(text), "%s", variable.value ? "true" : "false"); break;
		case ValueKind::Integer: snprintf(text, sizeof(text), "%d", static_cast<int>(variable.value)); break;
		default:
			if (std::isfinite(number) && number != 0.0f && (variable.value & 0x7F800000) != 0 && std::fabs(number) > 1e-6f && std::fabs(number) < 1e9f)
				snprintf(text, sizeof(text), "%d (or %g as a number)", static_cast<int>(variable.value), number);
			else
				snprintf(text, sizeof(text), "%d", static_cast<int>(variable.value));
			break;
		}
		return text;
	}

	std::string VariableText(const Variable& variable)
	{
		std::string text = "entity=" + SCRIPT_DEBUG::Hex(variable.entity) + " instance=" + SCRIPT_DEBUG::Hex(variable.instance) + " raw=" + SCRIPT_DEBUG::Hex(variable.value) +
			" value=" + ValueText(variable);
		if (variable.inLevel)
			text += " in_level type_name=" + SCRIPT_DEBUG::TypeText(variable.id) + " composite=" + (variable.id.compositeName[0] ? variable.id.compositeName : "(root)");
		else
			text += " not_in_level";
		return text;
	}

	std::string ObjectiveText(const char* what, const Objective& objective, bool primary)
	{
		if (!objective.used)
			return std::string(what) + ": (none)";
		std::string text = std::string(what) + ": " + objective.title + (objective.titleLine.empty() ? "" : " \"" + objective.titleLine + "\"") + " | " + objective.text +
			(objective.textLine.empty() ? "" : " \"" + objective.textLine + "\"") + " | completed=" + (objective.completed ? "1" : "0");
		if (!primary)
		{
			char extra[128];
			snprintf(extra, sizeof(extra), " position=%.2f,%.2f,%.2f marker=%d level=%08X", objective.position[0], objective.position[1], objective.position[2], objective.marker, objective.level);
			text += extra;
		}
		return text;
	}

	std::string ProgressionText(const Snapshot& snapshot)
	{
		if (!snapshot.progressionRead)
			return "progression=not_read";
		std::string text;
		char line[256];
		snprintf(line, sizeof(line), "storyline=%u completion=%.3f max_completion=%.3f mission=%g saves=%u active_level=%08X humans_killed=%u iron_man=%d\n",
			snapshot.storyline, snapshot.completion, snapshot.maxCompletion, snapshot.mission, snapshot.saves, snapshot.activeLevel, snapshot.humansKilled, snapshot.ironMan ? 1 : 0);
		text += line;
		snprintf(line, sizeof(line), "hunted=%u avoided=%u tutorial_flags=%08X one_off_events=%08X survivor_opened=%d\n", snapshot.hunted, snapshot.avoided, snapshot.tutorialFlags,
			snapshot.oneOffEvents, snapshot.survivorOpened ? 1 : 0);
		text += line;
		text += "blueprints=";
		for (uint32_t i = 0; i < kBlueprintCount; i++)
		{
			snprintf(line, sizeof(line), "%s%08X:%d", i ? "," : "", snapshot.blueprintId[i], snapshot.blueprintDone[i] ? 1 : 0);
			text += line;
		}
		text += "\nmission_objectives_done=";
		bool any = false;
		for (uint32_t i = 0; i < kMissionObjectiveCount; i++)
			if (snapshot.missionObjectives[i])
			{
				text += (any ? "," : "") + std::to_string(i);
				any = true;
			}
		text += any ? "\n" : "(none)\n";
		snprintf(line, sizeof(line), "checks=%d/%d,%d/%d,%d/%d,%d/%d (being checked/failed)", snapshot.checks[0], snapshot.checks[1], snapshot.checks[2], snapshot.checks[3],
			snapshot.checks[4], snapshot.checks[5], snapshot.checks[6], snapshot.checks[7]);
		return text + line;
	}

	bool Register()
	{
		DEBUG_MENU::AddSwitch("scripts.objective_markers", &g_markers);
		DEBUG_MENU::AddCommand("scripts.list_saved_variables", "the saved (persistent) script variables: ids, value, and type when the variable is in the running level; argument: a filter",
			[](const std::string& argument) -> DEBUG_MENU::CommandResult
			{
				return DEBUG_MENU::AnswerOnEntityThread([argument]() -> DEBUG_MENU::CommandResult
				{
					Snapshot snapshot;
					Read(snapshot);
					DEBUG_MENU::CommandResult result;
					result.message = "count=" + std::to_string(snapshot.variableCount) + "\n";
					for (const Variable& variable : snapshot.variables)
					{
						const std::string text = VariableText(variable);
						if (SCRIPT_DEBUG::Matches(text, argument.c_str()))
							result.message += text + "\n";
					}
					return result;
				}, 4000);
			});
		DEBUG_MENU::AddCommand("scripts.list_objectives", "the player's objectives: primary, secondaries and exit",
			[](const std::string&) -> DEBUG_MENU::CommandResult
			{
				return DEBUG_MENU::AnswerOnEntityThread([]() -> DEBUG_MENU::CommandResult
				{
					Snapshot snapshot;
					Read(snapshot);
					if (!snapshot.objectivesRead)
						return { false, "the objectives could not be read (no player inventory yet?)" };
					DEBUG_MENU::CommandResult result;
					result.message = ObjectiveText("primary", snapshot.primary, true) + "\n";
					for (uint32_t i = 0; i < kSecondaryCount; i++)
						if (snapshot.secondary[i].used)
							result.message += ObjectiveText(("secondary " + std::to_string(i)).c_str(), snapshot.secondary[i], false) + "\n";
					result.message += std::string("exit_set=") + (snapshot.exitSet ? "1 " : "0 ") + ObjectiveText("exit", snapshot.exit, false) + "\n";
					result.message += "last_triggered=" + SCRIPT_DEBUG::Hex(snapshot.lastTriggered);
					return result;
				});
			});
		DEBUG_MENU::AddCommand("scripts.list_progression", "the campaign progression the game tracks (storyline, completion, blueprints, mission objectives...)",
			[](const std::string&) -> DEBUG_MENU::CommandResult
			{
				return DEBUG_MENU::AnswerOnEntityThread([]() -> DEBUG_MENU::CommandResult
				{
					Snapshot snapshot;
					Read(snapshot);
					return { snapshot.progressionRead, ProgressionText(snapshot) };
				});
			});
		return true;
	}

	const bool g_registered = Register();

	// ---- The tab ----
	char g_filter[96] = "";
	bool g_onlyInLevel = false;
	ULONGLONG g_lastAsked = 0;          // by the tab or the overlay
	ULONGLONG g_lastAskedVariables = 0; // by the tab

	void DrawObjective(const char* what, const Objective& objective, bool primary)
	{
		if (!objective.used)
		{
			ImGui::TextDisabled("%s: none", what);
			return;
		}
		ImGui::TextColored(objective.completed ? ImVec4(0.5f, 0.9f, 0.5f, 1.0f) : ImVec4(1.0f, 0.9f, 0.6f, 1.0f), "%s%s:", what, objective.completed ? " (done)" : "");
		ImGui::SameLine();
		ImGui::TextWrapped("%s", objective.titleLine.empty() ? objective.title : objective.titleLine.c_str());
		if (!objective.titleLine.empty())
		{
			ImGui::SameLine();
			ImGui::TextDisabled("[%s]", objective.title);
		}
		if (objective.text[0])
		{
			ImGui::Indent();
			ImGui::TextWrapped("%s", objective.textLine.empty() ? objective.text : objective.textLine.c_str());
			if (!objective.textLine.empty())
				ImGui::TextDisabled("[%s]", objective.text);
			ImGui::Unindent();
		}
		if (!primary)
		{
			const std::string level = objective.level ? "id " + SCRIPT_DEBUG::Hex(objective.level) : std::string("none");
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextDisabled("    at %.2f, %.2f, %.2f - marker kind %d, level %s", objective.position[0], objective.position[1], objective.position[2], objective.marker, level.c_str());
			ImGui::PopTextWrapPos();
		}
	}

	void DrawVariables(const Snapshot& snapshot)
	{
		ImGui::SeparatorText("Saved variables");
		ImGui::TextWrapped("Script variables marked persistent: the game keeps their values for the whole game (and saves them), so later levels see them. One in the running level shows its type and where it is; one from another level shows its raw value.");
		ImGui::SetNextItemWidth(220.0f * DEBUG_MENU::Scale());
		ImGui::InputTextWithHint("##varfilter", "Filter", g_filter, sizeof(g_filter));
		ImGui::SameLine();
		ImGui::Checkbox("Only those in this level", &g_onlyInLevel);
		ImGui::SameLine();
		if (ImGui::Button("Copy##vars"))
		{
			std::string text;
			for (const Variable& variable : snapshot.variables)
				if (SCRIPT_DEBUG::Matches(VariableText(variable), g_filter))
					text += VariableText(variable) + "\n";
			ImGui::SetClipboardText(text.c_str());
		}
		int inLevel = 0;
		for (const Variable& variable : snapshot.variables)
			inLevel += variable.inLevel ? 1 : 0;
		if (!snapshot.variablesRead)
		{
			ImGui::TextDisabled("Not read yet.");
			return;
		}
		ImGui::Text("%u saved, %d of them in this level", snapshot.variableCount, inLevel);

		if (!SCRIPT_DEBUG::BeginTable("variables", 3, 220.0f * DEBUG_MENU::Scale()))
			return;
		ImGui::TableSetupScrollFreeze(0, 1);
		ImGui::TableSetupColumn("Value");
		ImGui::TableSetupColumn("Variable");
		ImGui::TableSetupColumn("Where");
		ImGui::TableHeadersRow();
		int row = 0;
		for (const Variable& variable : snapshot.variables)
		{
			if (g_onlyInLevel && !variable.inLevel)
				continue;
			const std::string text = VariableText(variable);
			if (!SCRIPT_DEBUG::Matches(text, g_filter) && !(variable.inLevel && SCRIPT_DEBUG::Matches(SCRIPT_DEBUG::Describe(variable.id), g_filter)))
				continue;
			ImGui::PushID(row++);
			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(ValueText(variable).c_str());
			ImGui::TableNextColumn();
			if (variable.inLevel)
				ImGui::Text("%s %s", SCRIPT_DEBUG::TypeText(variable.id).c_str(), SCRIPT_DEBUG::Guid(variable.entity).c_str());
			else
				ImGui::TextDisabled("%s", SCRIPT_DEBUG::Guid(variable.entity).c_str());
			if (ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
				ImGui::SetTooltip("Entity %s (0x%08X) in composite instance %s (0x%08X); raw value 0x%08X", SCRIPT_DEBUG::Guid(variable.entity).c_str(), variable.entity,
					SCRIPT_DEBUG::Guid(variable.instance).c_str(), variable.instance, variable.value);
			ImGui::TableNextColumn();
			// On one line, the instance (which tells the copies of one composite apart) last: the table scrolls sideways for it
			if (variable.inLevel)
			{
				const std::string where = SCRIPT_DEBUG::WhereText(variable.id);
				ImGui::Text("%s (instance %s)", where.empty() ? "the level root" : where.c_str(), SCRIPT_DEBUG::Guid(variable.instance).c_str());
			}
			else
				ImGui::TextDisabled("another level (instance %s)", SCRIPT_DEBUG::Guid(variable.instance).c_str());
			ImGui::PopID();
		}
		ImGui::EndTable();
	}

	void DrawObjectives(const Snapshot& snapshot)
	{
		ImGui::SeparatorText("Objectives");
		if (!snapshot.objectivesRead)
		{
			ImGui::TextDisabled("Not readable now (no player inventory yet?).");
			return;
		}
		bool markers = g_markers;
		if (ImGui::Checkbox("Mark the secondary objectives' positions", &markers))
			g_markers = markers;
		DrawObjective("Primary", snapshot.primary, true);
		for (uint32_t i = 0; i < kSecondaryCount; i++)
			if (snapshot.secondary[i].used)
				DrawObjective(("Secondary " + std::to_string(i)).c_str(), snapshot.secondary[i], false);
		if (snapshot.exitSet || snapshot.exit.used)
			DrawObjective("Exit", snapshot.exit, false);
	}

	void DrawProgression(const Snapshot& snapshot)
	{
		ImGui::SeparatorText("Campaign progression");
		if (!snapshot.progressionRead)
		{
			ImGui::TextDisabled("Not readable now.");
			return;
		}
		const char* storyline = snapshot.storyline == 0 ? "the start" : snapshot.storyline == 1 ? "after the station's AI turns" : snapshot.storyline == 2 ? "the end" : "?";
		// The furthest completion is the highest any game has reached (the game keeps the highest it has seen, beside the
		// blueprints); the active mission's level is an id (0: none)
		ImGui::TextWrapped("Storyline: %s (%u)   completion %.1f%% (the highest any game has reached: %.1f%%)", storyline, snapshot.storyline, snapshot.completion * 100.0f,
			snapshot.maxCompletion * 100.0f);
		const std::string activeLevel = snapshot.activeLevel ? "id " + SCRIPT_DEBUG::Hex(snapshot.activeLevel) : std::string("none");
		ImGui::TextWrapped("Mission %g, active mission's level %s, saves made %u, humans killed %u%s", snapshot.mission, activeLevel.c_str(), snapshot.saves, snapshot.humansKilled,
			snapshot.ironMan ? ", iron man mode" : "");
		// The time hunted and avoided are the seconds not yet counted into the game's stats (it adds them a whole minute at a time)
		ImGui::TextWrapped("Time hunted by the alien %u s, time spent avoiding it %u s (the seconds not yet added to the game's stats, a minute at a time); tutorial flags %08X, one-off events %08X%s",
			snapshot.hunted, snapshot.avoided, snapshot.tutorialFlags, snapshot.oneOffEvents, snapshot.survivorOpened ? ", survivor mode opened" : "");
		std::string blueprints;
		for (uint32_t i = 0; i < kBlueprintCount; i++)
			if (snapshot.blueprintId[i])
				blueprints += (blueprints.empty() ? "" : ", ") + SCRIPT_DEBUG::Hex(snapshot.blueprintId[i]) + (snapshot.blueprintDone[i] ? " (unlocked)" : "");
		ImGui::TextWrapped("Crafting blueprints: %s", blueprints.empty() ? "none" : blueprints.c_str());
		std::string done;
		for (uint32_t i = 0; i < kMissionObjectiveCount; i++)
			if (snapshot.missionObjectives[i])
				done += (done.empty() ? "" : ", ") + std::to_string(i);
		ImGui::TextWrapped("Downloadable missions' objectives done: %s", done.empty() ? "none" : done.c_str());
		ImGui::TextWrapped("Achievement checks (being checked / failed): %d/%d  %d/%d  %d/%d  %d/%d", snapshot.checks[0], snapshot.checks[1], snapshot.checks[2], snapshot.checks[3],
			snapshot.checks[4], snapshot.checks[5], snapshot.checks[6], snapshot.checks[7]);
	}

	void DrawTab()
	{
		if (GetTickCount64() - g_lastAskedVariables > 1000)
		{
			g_lastAsked = g_lastAskedVariables = GetTickCount64();
			Ask(true);
		}
		const std::shared_ptr<Snapshot> snapshot = Latest();
		ImGui::TextWrapped("What the level's scripts carry beyond the level, read only. Read again every second while this tab is open.");
		if (!snapshot->ok)
		{
			ImGui::TextDisabled("Not read yet (the game reads it between frames of the level's scripts).");
			return;
		}
		DrawObjectives(*snapshot);
		DrawVariables(*snapshot);
		DrawProgression(*snapshot);
	}

	// The secondary objectives' positions over the game
	void DrawOverlay(ImDrawList* drawList)
	{
		if (!g_markers)
			return;
		if (GetTickCount64() - g_lastAsked > 1000)
		{
			g_lastAsked = GetTickCount64();
			Ask(false);
		}
		const std::shared_ptr<Snapshot> snapshot = Latest();
		if (!snapshot->objectivesRead)
			return;
		for (uint32_t i = 0; i < kSecondaryCount; i++)
		{
			const Objective& objective = snapshot->secondary[i];
			if (!objective.used || (objective.position[0] == 0.0f && objective.position[1] == 0.0f && objective.position[2] == 0.0f))
				continue;
			const ImU32 colour = objective.completed ? IM_COL32(120, 220, 120, 255) : IM_COL32(255, 210, 90, 255);
			DEBUG_MENU::WorldCross(drawList, objective.position, 0.3f, colour);
			const std::string label = "Objective " + std::to_string(i) + ": " + (objective.titleLine.empty() ? std::string(objective.title) : objective.titleLine) +
				(objective.completed ? " (done)" : "");
			DEBUG_MENU::WorldText(drawList, objective.position, label.c_str(), colour, -12.0f);
		}
	}

	const bool g_added = DEBUG_MENU::AddTab("Progression", 120, DrawTab, "Scripts") && DEBUG_MENU::AddOverlay(96, DrawOverlay);
}

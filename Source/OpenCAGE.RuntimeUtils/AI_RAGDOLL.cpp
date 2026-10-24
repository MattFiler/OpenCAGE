#include "AI_STATE.h"
#include "DEBUG_MENU.h"

#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <memory>
#include <mutex>
#include <sstream>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <imgui.h>

/*
	Ragdoll, a section of the Characters group's Control tab: lets the selected NPC's body fall as a ragdoll and stands it
	back up, for checking a skeleton's ragdoll (an imported one above all) without killing anything. It is the game's own
	ragdoll switch for a living character, as the level scripts' ragdoll command uses it: the body goes limp under physics,
	pushed (if asked) away from the camera, and when let go its pose blends back to its animation over half a second.

	The ragdoll is the body's alone: nothing else of the character is told, so its behaviour carries on underneath - its
	tree keeps choosing, it keeps walking (the body lies where it fell while the character moves on), turning, aiming and
	talking, and when stood up it is wherever its behaviour took it. So by default its AI is held still meanwhile with the
	"force idle" motivation (the Control tab's tick), put back as it was when it is stood up.

	It is let go when unticked, and on its own: when it dies (the ragdoll then belongs to its death, so it is not stood
	up), when it leaves the level (despawned) or the level changes (nothing is left to stand up), or when something else
	stands it up first (a script's ragdoll command being reset). Changes are made on the AI thread, the character found
	again there by its unique id.

	Not for the alien: its ragdoll reads as on while it is alive and well, so it can't be told apart from one a script or
	its death turned on, and is not switched here.

	Layouts and addresses are the Steam retail AI.exe's (see GameAddresses.h for the GOG and Epic Games Store builds).
*/
namespace
{
	// ---- The game ----

	// A character's animation system; its ragdoll (a pointer, null when its skeleton has none) with whether it is on at
	// +0x138; the character's health state (4: dead); its motivations (a pointer: a bool each from +0xC)
	constexpr uint32_t kCharacterAnimation = 0x3F0;
	constexpr uint32_t kCharacterRagdoll = 0x714;
	constexpr uint32_t kRagdollOn = 0x138;
	constexpr uint32_t kCharacterHealthState = 0x32C;
	constexpr int kHealthDead = 4;
	constexpr uint32_t kCharacterMotivations = 0x3498;
	constexpr uint32_t kMotivationFirst = 0x0C;

	// The alien's class (as DEBUG_MENU::ClassName numbers them), and what it is told
	constexpr int kAlienClass = 1;
	const char* const kNotForAlien = "not for the alien: its ragdoll can't be switched from here";

	// The animation system's ragdoll on (the velocity it starts with, a second vector the scripts' command leaves at nought,
	// and false for a living character) and off (the pose blends back to the animation)
	typedef void(__thiscall* t_ragdoll_on)(void* animation, const float* velocity, const float* other, bool dying);
	const auto ragdoll_on = reinterpret_cast<t_ragdoll_on>(DEVTOOLS_RELATIVE_ADDRESS(0x00295540));
	typedef void(__thiscall* t_ragdoll_off)(void* animation);
	const auto ragdoll_off = reinterpret_cast<t_ragdoll_off>(DEVTOOLS_RELATIVE_ADDRESS(0x0028D110));

	bool Available()
	{
		return ragdoll_on && ragdoll_off;
	}

	// ---- Reading and calling (guarded) ----

	// Whether it has a ragdoll, and whether that is on (false: unreadable)
	bool ReadRagdoll(const uint8_t* character, bool& has, bool& on)
	{
		has = on = false;
		if (!character)
			return false;
		__try
		{
			const uint8_t* ragdoll = *reinterpret_cast<const uint8_t* const*>(character + kCharacterRagdoll);
			has = ragdoll != nullptr;
			on = ragdoll && ragdoll[kRagdollOn] != 0;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool IsDead(const uint8_t* character)
	{
		__try
		{
			return *reinterpret_cast<const int*>(character + kCharacterHealthState) == kHealthDead;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return true;
		}
	}

	// Whether its "force idle" motivation is allowed (false: unreadable)
	bool ReadForceIdle(const uint8_t* character, bool& allowed)
	{
		allowed = false;
		__try
		{
			const uint8_t* motivations = *reinterpret_cast<const uint8_t* const*>(character + kCharacterMotivations);
			if (!motivations)
				return false;
			allowed = motivations[kMotivationFirst + AI_STATE::kForceIdleMotivation - 1] != 0;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool CallOn(uint8_t* character, const float* velocity, const float* other)
	{
		__try
		{
			ragdoll_on(character + kCharacterAnimation, velocity, other, false);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool CallOff(uint8_t* character)
	{
		__try
		{
			ragdoll_off(character + kCharacterAnimation);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// ---- The characters down (any thread, under the lock) ----

	struct Down
	{
		uint32_t id = 0;
		std::string name;
		bool heldIdle = false;     // force idle was allowed by this, to be forbidden again when it is stood up
		ULONGLONG since = 0;
	};

	std::mutex g_downMutex;
	std::vector<Down> g_down;

	bool IsDown(uint32_t id)
	{
		std::lock_guard<std::mutex> lock(g_downMutex);
		for (const Down& down : g_down)
			if (down.id == id)
				return true;
		return false;
	}

	// Takes it off the list, giving what was kept of it
	bool Take(uint32_t id, Down& out)
	{
		std::lock_guard<std::mutex> lock(g_downMutex);
		for (auto it = g_down.begin(); it != g_down.end(); ++it)
			if (it->id == id)
			{
				out = *it;
				g_down.erase(it);
				return true;
			}
		return false;
	}

	std::vector<Down> AllDown()
	{
		std::lock_guard<std::mutex> lock(g_downMutex);
		return g_down;
	}

	// ---- Settings and status ----

	std::atomic<float> g_push = 0.0f;
	std::atomic<bool> g_holdAi = true;
	constexpr float kMaxPush = 10.0f;

	std::mutex g_statusMutex;
	std::string g_status;

	void SetStatus(const std::string& text)
	{
		{
			std::lock_guard<std::mutex> lock(g_statusMutex);
			g_status = text;
		}
		DevTools::Log("Debug menu: ragdoll: %s", text.c_str());
	}

	DEBUG_MENU::CommandResult Fail(const std::string& message)
	{
		return { false, message };
	}

	DEBUG_MENU::CommandResult Done(const std::string& message)
	{
		return { true, message };
	}

	// ---- Changes (AI thread) ----

	// The velocity it falls with (the game reads three floats of it)
	struct Velocity
	{
		float value[4] = {};
	};

	// Its force idle put back to forbidden, when this allowed it and it is still allowed
	void PutBackIdle(uint8_t* character)
	{
		bool allowed = false;
		if (ReadForceIdle(character, allowed) && allowed)
			AI_STATE::ApplyMotivation(character, AI_STATE::kForceIdleMotivation, 1);
	}

	DEBUG_MENU::CommandResult LetFall(uint32_t id, const std::string& name, const Velocity& velocity, bool holdAi)
	{
		if (!Available())
			return Fail("not available on this release of the game");
		uint8_t* character = AI_STATE::FindCharacter(id);
		if (!character)
			return Fail(name + ": it is gone");
		if (IsDead(character))
			return Fail(name + ": it is dead");
		if (IsDown(id))
			return Done(name + ": already down");
		bool has = false, on = false;
		if (!ReadRagdoll(character, has, on))
			return Fail(name + ": its ragdoll could not be read");
		if (!has)
			return Fail(name + ": its skeleton has no ragdoll");
		if (on)
			return Fail(name + ": its ragdoll is already on, but not by this (a script's or its death's): left alone");
		bool heldIdle = false;
		bool allowed = false;
		if (holdAi && ReadForceIdle(character, allowed) && !allowed && AI_STATE::ApplyMotivation(character, AI_STATE::kForceIdleMotivation, 0))
			heldIdle = true;
		const float nought[4] = {};
		if (!CallOn(character, velocity.value, nought))
		{
			if (heldIdle)
				PutBackIdle(character);
			return Fail(name + ": not done (the game's call faulted)");
		}
		Down down;
		down.id = id;
		down.name = name;
		down.heldIdle = heldIdle;
		down.since = GetTickCount64();
		{
			std::lock_guard<std::mutex> lock(g_downMutex);
			g_down.push_back(down);
		}
		return Done(name + ": down" + (heldIdle ? " (its AI held still with force idle)" : holdAi ? " (force idle was already on)" : " (its AI left running)"));
	}

	DEBUG_MENU::CommandResult StandUp(uint32_t id, const std::string& fallbackName)
	{
		Down down;
		const bool mine = Take(id, down);
		const std::string name = mine ? down.name : fallbackName;
		uint8_t* character = AI_STATE::FindCharacter(id);
		if (!character)
			return mine ? Done(name + ": gone (despawned or the level changed): nothing to stand up") : Fail(name + ": it is gone");
		if (IsDead(character))
			return Done(name + ": dead: its ragdoll belongs to its death now, left down");
		bool has = false, on = false;
		ReadRagdoll(character, has, on);
		if (!mine)
			return on ? Fail(name + ": its ragdoll is on, but not by this (a script's or its death's): left alone") : Fail(name + ": it isn't down");
		if (!Available() || !CallOff(character))
			return Fail(name + ": not stood up (the game's call faulted)");
		if (down.heldIdle)
			PutBackIdle(character);
		return Done(name + ": stood up" + (down.heldIdle ? ", force idle put back off" : ""));
	}

	// ---- From the window (render thread) ----

	// Away from the camera along the ground (backwards from where it faces when there is no camera), at the push speed
	Velocity PushFor(const DEBUG_MENU::Character& character)
	{
		Velocity out;
		const float speed = g_push;
		if (speed <= 0.0f)
			return out;
		float camera[3];
		float dx = 0.0f, dz = 0.0f;
		if (DEBUG_MENU::CameraPosition(camera))
		{
			dx = character.position[0] - camera[0];
			dz = character.position[2] - camera[2];
		}
		float length = std::sqrt(dx * dx + dz * dz);
		if (length < 0.05f)
		{
			dx = -std::sin(character.facing);
			dz = -std::cos(character.facing);
			length = 1.0f;
		}
		out.value[0] = dx / length * speed;
		out.value[2] = dz / length * speed;
		return out;
	}

	// What the window last asked for one character, shown until the AI thread has done it (or a moment has passed)
	uint32_t g_askedId = 0;
	bool g_askedOn = false;
	double g_askedAt = -10.0;

	void AskFall(const DEBUG_MENU::Character& character)
	{
		const uint32_t id = character.id;
		const std::string name = character.name;
		const Velocity velocity = PushFor(character);
		const bool holdAi = g_holdAi;
		g_askedId = id;
		g_askedOn = true;
		g_askedAt = ImGui::GetTime();
		SetStatus(name + ": asked to fall (at the next AI update)");
		DEBUG_MENU::OnAiThread([id, name, velocity, holdAi]() { SetStatus(LetFall(id, name, velocity, holdAi).message); });
	}

	void AskStandUp(uint32_t id, const std::string& name)
	{
		g_askedId = id;
		g_askedOn = false;
		g_askedAt = ImGui::GetTime();
		SetStatus(name + ": asked to stand up (at the next AI update)");
		DEBUG_MENU::OnAiThread([id, name]() { SetStatus(StandUp(id, name).message); });
	}

	// Every frame (the window open or not): those down are let go when they die, leave the level, or are stood up by
	// something else (the last putting back the force idle this allowed)
	void Watch(ImDrawList* /*drawList*/)
	{
		const std::vector<Down> down = AllDown();
		for (const Down& entry : down)
		{
			const DEBUG_MENU::Character* found = nullptr;
			for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
				if (character.id == entry.id)
					found = &character;
			Down taken;
			if (!found)
			{
				if (Take(entry.id, taken))
					SetStatus(entry.name + ": gone (despawned or the level changed): let go, nothing to stand up");
				continue;
			}
			if (found->dead)
			{
				if (Take(entry.id, taken))
					SetStatus(entry.name + ": died: its ragdoll belongs to its death now, let go");
				continue;
			}
			bool has = false, on = false;
			if (ReadRagdoll(found->object, has, on) && !on && Take(entry.id, taken))
			{
				SetStatus(entry.name + ": stood up by something else (a script?): let go");
				if (taken.heldIdle)
				{
					const uint32_t id = entry.id;
					DEBUG_MENU::OnAiThread([id]()
					{
						if (uint8_t* character = AI_STATE::FindCharacter(id))
							if (!IsDead(character))
								PutBackIdle(character);
					});
				}
			}
		}
	}

	void Tooltip(const char* text)
	{
		if (!ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip | ImGuiHoveredFlags_AllowWhenDisabled))
			return;
		ImGui::BeginTooltip();
		ImGui::PushTextWrapPos(ImGui::GetFontSize() * 30.0f);
		ImGui::TextUnformatted(text);
		ImGui::PopTextWrapPos();
		ImGui::EndTooltip();
	}

	void DrawSection()
	{
		ImGui::SeparatorText("Ragdoll");
		if (!Available())
		{
			ImGui::TextDisabled("Not available on this release of the game.");
			return;
		}
		const DEBUG_MENU::Character* selected = DEBUG_MENU::Selected();
		if (!selected)
			ImGui::TextDisabled("Select an NPC.");
		else if (selected->player)
			ImGui::TextDisabled("Not for the player.");
		else if (selected->characterClass == kAlienClass)
			ImGui::TextDisabled("Not for the alien: its ragdoll can't be switched from here.");
		else
		{
			bool has = false, on = false;
			const bool read = ReadRagdoll(selected->object, has, on);
			const bool mine = IsDown(selected->id);
			if (read && !has)
				ImGui::TextDisabled("%s has no ragdoll (its skeleton has none).", selected->name.c_str());
			else
			{
				bool ticked = mine;
				if (g_askedId == selected->id && ImGui::GetTime() - g_askedAt < 1.5 && g_askedOn != mine)
					ticked = g_askedOn;
				const bool someoneElses = on && !mine;
				ImGui::BeginDisabled(selected->dead || someoneElses);
				char label[96];
				snprintf(label, sizeof(label), "Ragdoll %s", selected->name.c_str());
				if (ImGui::Checkbox(label, &ticked))
				{
					if (ticked)
						AskFall(*selected);
					else
						AskStandUp(selected->id, selected->name);
				}
				ImGui::EndDisabled();
				Tooltip("Lets its body fall as a ragdoll - its skeleton's own, as the level scripts' ragdoll command does - and stands it back up when "
					"unticked, for checking a skeleton's ragdoll without killing anything. Let go by itself when it dies (the ragdoll is then its "
					"death's), leaves the level or the level changes.");
				if (someoneElses)
				{
					ImGui::SameLine();
					ImGui::TextDisabled("(its ragdoll is on, but not by this: a script's or its death's)");
				}
				else if (selected->dead)
				{
					ImGui::SameLine();
					ImGui::TextDisabled("(it is dead)");
				}
			}
		}
		float push = g_push;
		ImGui::SetNextItemWidth(220.0f * DEBUG_MENU::Scale());
		if (ImGui::SliderFloat("Push", &push, 0.0f, kMaxPush, push > 0.0f ? "%.1f m/s" : "none", ImGuiSliderFlags_AlwaysClamp))
			g_push = push;
		Tooltip("The speed it falls with, away from the camera along the ground (as the scripts' ragdoll command's impact velocity). None: it just "
			"goes limp where it stands.");
		bool holdAi = g_holdAi;
		if (ImGui::Checkbox("Hold its AI still while it is down", &holdAi))
			g_holdAi = holdAi;
		Tooltip("The ragdoll is only its body's: its behaviour carries on underneath (it walks on, turns, aims and talks while its body lies there, and "
			"stands up wherever that took it). This turns on its force idle motivation meanwhile (where its behaviour tree has the idle branch for it), "
			"put back off when it is stood up.");

		const std::vector<Down> down = AllDown();
		if (!down.empty())
		{
			ImGui::TextDisabled("Down:");
			const ULONGLONG now = GetTickCount64();
			for (const Down& entry : down)
			{
				ImGui::PushID(static_cast<int>(entry.id));
				if (ImGui::SmallButton("Stand up"))
					AskStandUp(entry.id, entry.name);
				ImGui::SameLine();
				ImGui::Text("%s (%u) - %.0f s%s", entry.name.c_str(), entry.id, (now - entry.since) / 1000.0, entry.heldIdle ? ", AI held still" : "");
				ImGui::PopID();
			}
			if (down.size() > 1 && ImGui::Button("Stand them all up"))
				for (const Down& entry : down)
					AskStandUp(entry.id, entry.name);
		}

		std::lock_guard<std::mutex> lock(g_statusMutex);
		if (!g_status.empty())
		{
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextDisabled("Last: %s", g_status.c_str());
			ImGui::PopTextWrapPos();
		}
	}

	// ---- Commands ----

	struct Asked
	{
		bool found = false, player = false, dead = false;
		int characterClass = -1;
		uint32_t id = 0;
		std::string name;
		Velocity velocity;
	};

	// The character a command names (by "id=", else the selected one), read on the render thread
	bool TargetFor(uint32_t id, Asked& out, std::string& error)
	{
		std::shared_ptr<Asked> shared = std::make_shared<Asked>();
		const DEBUG_MENU::CommandResult answer = DEBUG_MENU::AnswerOnRenderThread([id, shared]() -> DEBUG_MENU::CommandResult
		{
			const uint32_t wanted = id ? id : DEBUG_MENU::SelectedId();
			for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
				if (character.id == wanted)
				{
					shared->found = true;
					shared->id = character.id;
					shared->name = character.name;
					shared->player = character.player;
					shared->dead = character.dead;
					shared->characterClass = character.characterClass;
					shared->velocity = PushFor(character);
					break;
				}
			return { shared->found, "" };
		});
		if (!answer.ok)
		{
			error = !answer.message.empty() ? answer.message : id ? "no character with that unique id" : "no character selected (select one, or give id=<unique id>)";
			return false;
		}
		out = *shared;
		return true;
	}

	bool RegisterControls()
	{
		DEBUG_MENU::AddValue("ai.ragdoll_push", &g_push, 0.0f, kMaxPush);
		DEBUG_MENU::AddSwitch("ai.ragdoll_hold_ai", &g_holdAi);
		DEBUG_MENU::AddCommand("ai.ragdoll", "[on|off|toggle] [id=<unique id>] - lets the selected NPC (or the one id names) fall as a ragdoll, or stands it "
			"back up (AI thread; push and AI hold from ai.ragdoll_push and ai.ragdoll_hold_ai)", [](const std::string& argument) -> DEBUG_MENU::CommandResult
		{
			if (!Available())
				return Fail("not available on this release of the game");
			uint32_t id = 0;
			std::string mode = "on";
			std::istringstream stream(argument);
			std::string word;
			while (stream >> word)
			{
				for (char& c : word)
					c = static_cast<char>(tolower(static_cast<unsigned char>(c)));
				if (word.compare(0, 3, "id=") == 0)
					id = static_cast<uint32_t>(strtoul(word.c_str() + 3, nullptr, 0));
				else if (word == "on" || word == "1" || word == "off" || word == "0" || word == "toggle")
					mode = word == "1" ? "on" : word == "0" ? "off" : word;
				else
					return Fail("not understood: " + word + " (give on, off or toggle, and id=<unique id>)");
			}
			Asked target;
			std::string error;
			if (!TargetFor(id, target, error))
				return Fail(error);
			if (target.player)
				return Fail("not for the player");
			if (target.characterClass == kAlienClass)
				return Fail(target.name + ": " + kNotForAlien);
			if (mode == "toggle")
				mode = IsDown(target.id) ? "off" : "on";
			const bool holdAi = g_holdAi;
			if (mode == "on")
			{
				if (target.dead)
					return Fail(target.name + " is dead");
				return DEBUG_MENU::AnswerOnAiThread([target, holdAi]() -> DEBUG_MENU::CommandResult
				{
					const DEBUG_MENU::CommandResult result = LetFall(target.id, target.name, target.velocity, holdAi);
					SetStatus(result.message);
					return result;
				});
			}
			return DEBUG_MENU::AnswerOnAiThread([target]() -> DEBUG_MENU::CommandResult
			{
				const DEBUG_MENU::CommandResult result = StandUp(target.id, target.name);
				SetStatus(result.message);
				return result;
			});
		});
		DEBUG_MENU::AddCommand("ai.ragdolls", "the characters down as ragdolls by this, a line each: unique id, name, seconds down, whether their AI is held, "
			"whether the game has their ragdoll on", [](const std::string&) -> DEBUG_MENU::CommandResult
		{
			return DEBUG_MENU::AnswerOnRenderThread([]() -> DEBUG_MENU::CommandResult
			{
				std::string out;
				const ULONGLONG now = GetTickCount64();
				for (const Down& entry : AllDown())
				{
					bool has = false, on = false;
					for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
						if (character.id == entry.id)
							ReadRagdoll(character.object, has, on);
					char line[192];
					snprintf(line, sizeof(line), "%u %s seconds=%.1f ai_held=%d ragdoll_on=%d\n", entry.id, entry.name.c_str(), (now - entry.since) / 1000.0,
						entry.heldIdle ? 1 : 0, on ? 1 : 0);
					out += line;
				}
				return { true, out.empty() ? "none\n" : out };
			});
		});
		DEBUG_MENU::AddCommand("ai.ragdoll_release", "stands every character this has down back up (AI thread)", [](const std::string&) -> DEBUG_MENU::CommandResult
		{
			const std::vector<Down> down = AllDown();
			if (down.empty())
				return Done("none down");
			std::vector<std::pair<uint32_t, std::string>> list;
			for (const Down& entry : down)
				list.emplace_back(entry.id, entry.name);
			return DEBUG_MENU::AnswerOnAiThread([list]() -> DEBUG_MENU::CommandResult
			{
				std::string out;
				bool ok = true;
				for (const auto& entry : list)
				{
					const DEBUG_MENU::CommandResult result = StandUp(entry.first, entry.second);
					ok = ok && result.ok;
					out += result.message + "\n";
				}
				SetStatus(out);
				return { ok, out };
			});
		});
		return true;
	}

	const bool g_controls = RegisterControls();
	const bool g_added = DEBUG_MENU::AddTab("Control", 17, DrawSection, "Characters") && DEBUG_MENU::AddOverlay(14, Watch);
}

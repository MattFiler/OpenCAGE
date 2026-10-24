#include "DEBUG_MENU.h"
#include "LIVE_LINK.h"

#include <atomic>
#include <cmath>
#include <cstdio>
#include <mutex>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <detours.h>
#include <imgui.h>

/*
	Addresses and layouts are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).

	The Sound tab's AI hearing section: a switch ("sound.straight_line_hearing") that has the AI hear every sound by its
	straight-line distance, leaving the sound network out - a one-click check of a level's sound network.

	How loud a character hears a sound is worked out in one place (SOUND_DEBUG.h has the sum): from the shortest route
	through the sound network's open doorways when its caller asks for the network, else from the straight-line distance
	(the game itself does that for gunfire a squad hears, and for a character in the vents, where the distance also counts
	double). That call is hooked: while the switch is on, a check that asks for the network is worked out both ways -
	through the network, as the game would, then in a straight line - and the game gets the straight-line answer. The
	route through the network is never shorter than the straight line, so the AI hears at least as much as before: as if
	every doorway were open and every room linked.

	The two answers are compared and counted. A sound heard only in a straight line (not at all through the network) is
	one the network keeps from the character - what the switch is for: if an NPC hears the player with it on but not with
	it off, the network (a doorway stuck closed, a missing link) is the cause. The last few are kept, with who heard what
	and where. The counts start again each time the switch is turned on.

	Nothing is changed in the game; the switch goes off by itself when another level is loaded (a restart of the same
	level, or a checkpoint, leaves it on). The Sound tab's own hearing hook (SOUND_DEBUG.h) is on the function that calls
	this one, so its log still says what the game asked for (through the network); its "worked out again" column takes the
	straight line too while this switch is on.
*/
namespace
{
	// The game's working out of how loud a character (its behaviour manager) hears a sound
	constexpr uintptr_t kHeardIntensity = 0x002D7CD0;
	// The behaviour manager's character (a pointer); a character's unique id and position
	constexpr uint32_t kBehaviourCharacter = 0x128;
	constexpr uint32_t kCharacterId = 0x50;
	constexpr uint32_t kCharacterPosition = 0x70;

	// The entity manager (a pointer to it), with the running level's root composite id
	constexpr uintptr_t kEntityManager = 0x0134EF40;
	constexpr uint32_t kEntityRoot = 0x0C;

	// The sounds kept, newest last
	constexpr size_t kKept = 8;

	typedef float(__thiscall* t_heard_intensity)(void* behaviour, const float* position, float loudness, bool useNetwork, const void* sender, float distanceScale);
	t_heard_intensity g_heardIntensity = nullptr;
	std::atomic<bool> g_hooked = false;

	std::atomic<bool> g_on = false;
	bool g_wasOn = false;    // render thread: the switch as last seen
	uint32_t g_onRoot = 0;   // render thread: the level it was on in (0: none seen yet)
	std::string g_note;      // render thread: why it went off by itself

	// Since it was last turned on: the checks worked out in a straight line instead of through the network, those heard
	// only in a straight line, and those heard louder in a straight line than through the network
	std::atomic<uint32_t> g_checked = 0;
	std::atomic<uint32_t> g_onlyStraight = 0;
	std::atomic<uint32_t> g_louder = 0;

	// A sound heard only in a straight line
	struct Heard
	{
		ULONGLONG tick = 0;
		uint32_t listener = 0;   // the unique id of the character that heard it
		uint32_t sender = 0;     // and of the one that made it (0: no character)
		float position[3] = {};  // where it was made
		float distance = 0.0f;   // in a straight line from the listener
		float straight = 0.0f;   // how loud it was heard in a straight line (through the network: not at all)
	};
	std::mutex g_heardMutex;
	std::vector<Heard> g_heard;

	// ---- The hook ----

	// The hook's thread: who heard the sound, who made it and how far away it was (guarded: false when unreadable)
	bool ReadHeard(const void* behaviour, const float* position, const void* sender, Heard& out)
	{
		__try
		{
			for (int i = 0; i < 3; i++)
				out.position[i] = position[i];
			const uint8_t* listener = *reinterpret_cast<uint8_t* const*>(static_cast<const uint8_t*>(behaviour) + kBehaviourCharacter);
			if (!listener)
				return false;
			out.listener = *reinterpret_cast<const uint32_t*>(listener + kCharacterId);
			const float* at = reinterpret_cast<const float*>(listener + kCharacterPosition);
			const float dx = at[0] - position[0], dy = at[1] - position[1], dz = at[2] - position[2];
			out.distance = std::sqrt(dx * dx + dy * dy + dz * dz);
			out.sender = sender ? *reinterpret_cast<const uint32_t*>(static_cast<const uint8_t*>(sender) + kCharacterId) : 0;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// The hook's thread: the two answers compared, counted, and a sound heard only in a straight line kept
	void Note(const void* behaviour, const float* position, const void* sender, float network, float straight)
	{
		g_checked++;
		if (straight <= 0.0f || straight <= network)
			return;
		if (network > 0.0f)
		{
			g_louder++;
			return;
		}
		g_onlyStraight++;
		Heard heard;
		if (!ReadHeard(behaviour, position, sender, heard))
			return;
		heard.tick = GetTickCount64();
		heard.straight = straight;
		std::lock_guard<std::mutex> lock(g_heardMutex);
		if (g_heard.size() >= kKept)
			g_heard.erase(g_heard.begin());
		g_heard.push_back(heard);
	}

	// Hooked: a character working out how loud it heard a sound. A check that asks for the network, while the switch is
	// on, is worked out both ways and given the straight-line answer.
	float __fastcall HeardIntensity(void* behaviour, void* /*edx*/, const float* position, float loudness, bool useNetwork, const void* sender, float distanceScale)
	{
		if (!useNetwork || !g_on)
			return g_heardIntensity(behaviour, position, loudness, useNetwork, sender, distanceScale);
		const float network = g_heardIntensity(behaviour, position, loudness, true, sender, distanceScale);
		const float straight = g_heardIntensity(behaviour, position, loudness, false, sender, distanceScale);
		Note(behaviour, position, sender, network, straight);
		return straight;
	}

	void AttachHooks(bool attach)
	{
		if (attach)
		{
			g_heardIntensity = reinterpret_cast<t_heard_intensity>(DEVTOOLS_RELATIVE_ADDRESS(kHeardIntensity));
			if (g_heardIntensity)
				DetourAttach(&reinterpret_cast<PVOID&>(g_heardIntensity), reinterpret_cast<PVOID>(&HeardIntensity));
			g_hooked = g_heardIntensity != nullptr;
			DevTools::Log("Debug menu: straight-line hearing hook %s", g_heardIntensity ? "attached" : "not attached (no address in this build)");
		}
		else
		{
			g_hooked = false;
			g_on = false;
			if (g_heardIntensity)
				DetourDetach(&reinterpret_cast<PVOID&>(g_heardIntensity), reinterpret_cast<PVOID>(&HeardIntensity));
		}
	}

	// ---- The level ----

	uint32_t ReadRoot()
	{
		__try
		{
			const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kEntityManager);
			const uint8_t* manager = at ? *reinterpret_cast<uint8_t* const*>(at) : nullptr;
			return manager ? *reinterpret_cast<const uint32_t*>(manager + kEntityRoot) : 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	// The running level's root composite id (0: no level, or one loading)
	uint32_t LevelRoot()
	{
		__try
		{
			if (!LIVE_LINK::LevelRunning())
				return 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
		return ReadRoot();
	}

	// ---- Names ----

	// Render thread: a character's name by its unique id, from this frame's characters
	std::string NameOf(uint32_t id, const char* none)
	{
		if (!id)
			return none;
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
			if (character.id == id)
				return character.player ? character.name + " (the player)" : character.name;
		return "a character no longer there";
	}

	std::vector<Heard> HeardCopy()
	{
		std::lock_guard<std::mutex> lock(g_heardMutex);
		return g_heard;
	}

	// Render thread: a line for a sound heard only in a straight line
	std::string Describe(const Heard& heard)
	{
		char text[320];
		snprintf(text, sizeof(text), "%.1f s ago: %s heard %s at %.2f, %.1f m away in a straight line (through the network: not at all)",
			(GetTickCount64() - heard.tick) / 1000.0, NameOf(heard.listener, "?").c_str(), heard.sender ? (NameOf(heard.sender, "") + "'s sound").c_str() : "a sound",
			heard.straight, heard.distance);
		return text;
	}

	// ---- Every frame ----

	bool g_registered = false;

	void Register()
	{
		if (g_registered)
			return;
		g_registered = true;
		// The switch only where the hook is in (so it can't be set on a release without it)
		if (g_hooked)
			DEBUG_MENU::AddSwitch("sound.straight_line_hearing", &g_on);
		DEBUG_MENU::AddCommand("sound.straight_line_status", "AI hears in straight lines: on or off, then since it was turned on the checks worked out in a straight line, those heard only that way and those heard louder; then the last sounds heard only in a straight line, a line each", [](const std::string&)
		{
			if (!g_hooked)
				return DEBUG_MENU::CommandResult{ false, "not available on this game release" };
			return DEBUG_MENU::AnswerOnRenderThread([]() -> DEBUG_MENU::CommandResult
			{
				char text[256];
				snprintf(text, sizeof(text), "on=%d\nchecked=%u\nheard_only_straight=%u\nlouder_straight=%u\n", g_on ? 1 : 0, g_checked.load(), g_onlyStraight.load(), g_louder.load());
				std::string answer = text;
				if (!g_note.empty())
					answer += "note=" + g_note + "\n";
				const std::vector<Heard> heard = HeardCopy();
				for (auto it = heard.rbegin(); it != heard.rend(); ++it)
				{
					snprintf(text, sizeof(text), "age_s=%.1f listener=%s sender=%s distance=%.1f straight=%.3f network=0\n", (GetTickCount64() - it->tick) / 1000.0,
						NameOf(it->listener, "?").c_str(), NameOf(it->sender, "none").c_str(), it->distance, it->straight);
					answer += text;
				}
				return DEBUG_MENU::CommandResult{ true, answer };
			});
		});
	}

	// Render thread, every frame: the counts start again as the switch is turned on, and it goes off by itself when another
	// level is loaded
	void Apply(ImDrawList* /*drawList*/)
	{
		Register();
		const bool on = g_on;
		if (on && !g_wasOn)
		{
			g_checked = 0;
			g_onlyStraight = 0;
			g_louder = 0;
			{
				std::lock_guard<std::mutex> lock(g_heardMutex);
				g_heard.clear();
			}
			g_onRoot = LevelRoot();
			g_note.clear();
		}
		g_wasOn = on;
		if (!on)
			return;
		const uint32_t root = LevelRoot();
		if (!root)
			return;
		if (!g_onRoot)
			g_onRoot = root;
		else if (root != g_onRoot)
		{
			g_on = false;
			g_wasOn = false;
			g_note = "turned off: another level was loaded";
			DevTools::Log("Debug menu: AI hears in straight lines %s", g_note.c_str());
		}
	}

	// ---- The section ----

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

	void Hint(const char* text)
	{
		ImGui::PushTextWrapPos(0.0f);
		ImGui::TextDisabled("%s", text);
		ImGui::PopTextWrapPos();
	}

	void DrawSection()
	{
		Register();
		ImGui::SeparatorText("AI hearing");
		if (!g_hooked)
		{
			ImGui::TextDisabled("Hear in straight lines: not available on this game release.");
			return;
		}
		bool on = g_on;
		if (ImGui::Checkbox("AI hears in straight lines (leave out the sound network)", &on))
			g_on = on;
		Tooltip("Every character works out how loud it heard a sound by the straight-line distance to it, as if every doorway of the sound network were open and every room linked. The route through the network is never shorter, so the AI hears at least as much as with this off. It goes off by itself when another level is loaded.");
		Hint("A check of the level's sound network: if an NPC hears the player with this on but not with it off, the network is the cause (a doorway stuck closed, a missing link). Make a noise (Tools above) or walk near with it on and off.");
		if (!on && !g_checked && g_note.empty())
			return;
		if (!g_note.empty())
			ImGui::TextDisabled("(%s)", g_note.c_str());
		ImGui::Text("%s %u sounds checked in a straight line: %u heard only that way, %u heard louder.", on ? "Since turned on:" : "While it was on:", g_checked.load(), g_onlyStraight.load(), g_louder.load());
		Tooltip("Heard only that way: through the network the character would not have heard it at all. Heard louder: it would have, but less loudly (a longer way round, or through a part-open doorway).");
		const std::vector<Heard> heard = HeardCopy();
		for (auto it = heard.rbegin(); it != heard.rend(); ++it)
		{
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextUnformatted(Describe(*it).c_str());
			ImGui::PopTextWrapPos();
		}
		if (on && !heard.empty())
			Hint("What the AI hears, above, works each sound out again in a straight line too while this is on.");
	}

	const bool g_added = DEBUG_MENU::AddTab("Sound", 47, DrawSection, "Sound") && DEBUG_MENU::AddOverlay(42, Apply);
	const bool g_hooksAdded = DEBUG_MENU::AddHooks(AttachHooks);
}

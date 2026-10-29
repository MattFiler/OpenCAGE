#include "DEBUG_MENU.h"

#include <atomic>
#include <cctype>
#include <cfloat>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <mutex>
#include <sstream>
#include <string>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <detours.h>
#include <imgui.h>

/*
	Screen effects, a section of the Characters group's Player tab: the thirteen events the game sends the HUD when the
	player is hurt, strangled, healed or killed (low health on and off, strangle on and off, death on and off, a melee,
	bullet, acid or flashbang hit, a medkit's heal, a hit-and-run impact and its cancelling), sent by hand. The level's
	ScreenEffectEventMonitor entities pass each on to the HUD and damage-overlay composites, so this is for testing mods of
	those composites without being hurt.

	The three on/off pairs are states the game keeps: each change goes out once (an "on" while it is on sends nothing, as
	the game's own call checks). The game ends low health and death by itself on its next update unless the player's health
	is low or the player is dying; player.hold_screen_effects holds that off (the game's once-a-frame check that ends them
	is skipped meanwhile), so they stay until sent off. The hold lets go when switched off, and when the player changes (a
	checkpoint restart or a new level), after which the game's check ends whatever doesn't apply. Strangle is never ended
	by that check; a level change or restart clears all three.

	The events are sent on the entity thread (the monitors are script entities); the hold is a hook on the game's check.

	Layouts and addresses are the Steam retail AI.exe's (see GameAddresses.h for the GOG and Epic Games Store builds).
*/
namespace
{
	// ---- The game ----

	// The game's screen effects (a pointer to them): the monitors registered (eight entity handles of two ids each, from
	// +0), then whether strangle, low health and death are showing (a byte each)
	constexpr uintptr_t kScreenEffects = 0x012ED2FC;
	constexpr int kMonitors = 8;
	constexpr uint32_t kStrangleShowing = 0x40;
	constexpr uint32_t kLowHealthShowing = 0x41;
	constexpr uint32_t kDeathShowing = 0x42;

	// The game's sending of a screen-effect event to the monitors
	typedef void(__thiscall* t_screen_effect_event)(void* effects, int event);
	const auto screen_effect_event = reinterpret_cast<t_screen_effect_event>(DEVTOOLS_RELATIVE_ADDRESS(0x00346900));
	// The game's once-a-frame check on the player's health that ends low health and death when they don't apply (hooked)
	typedef void(__thiscall* t_health_screen_effects)(void* health);
	t_health_screen_effects health_screen_effects = reinterpret_cast<t_health_screen_effects>(DEVTOOLS_RELATIVE_ADDRESS(0x00390A60));

	struct Event
	{
		const char* key;
		const char* label;
		int showing;              // the state it turns on or off (-1: none)
		bool on;
		const char* what;
	};

	// In the game's order (an event's number is its place here)
	const Event kEvents[] = {
		{ "low_health_on", "Low health on", static_cast<int>(kLowHealthShowing), true,
			"The low-health effect starts, as when the player's health falls low. The game ends it on its next update unless the health is low: tick Hold to keep it." },
		{ "low_health_off", "Low health off", static_cast<int>(kLowHealthShowing), false, "The low-health effect ends." },
		{ "strangle_on", "Strangle on", static_cast<int>(kStrangleShowing), true,
			"The strangled effect starts, as when a human or an android grabs the player by the throat. It stays until Strangle off." },
		{ "strangle_off", "Strangle off", static_cast<int>(kStrangleShowing), false, "The strangled effect ends." },
		{ "death_on", "Death on", static_cast<int>(kDeathShowing), true,
			"The death effect starts, as when the player dies. The game ends it on its next update while the player is alive: tick Hold to keep it." },
		{ "death_off", "Death off", static_cast<int>(kDeathShowing), false, "The death effect ends." },
		{ "melee_hit", "Melee hit", -1, true, "A melee hit on the player." },
		{ "bullet_hit", "Bullet hit", -1, true, "A bullet hit on the player." },
		{ "medkit_heal", "Medkit heal", -1, true, "A medkit healing the player." },
		{ "acid_hit", "Acid hit", -1, true, "Acid on the player (the alien's blood)." },
		{ "flashbang_hit", "Flashbang hit", -1, true, "A flashbang going off by the player." },
		{ "hit_and_run", "Hit and run", -1, true, "A hit-and-run impact on the player starts." },
		{ "cancel_hit_and_run", "Cancel hit and run", -1, true, "The hit-and-run impact is cancelled." },
	};
	constexpr int kEventCount = static_cast<int>(sizeof(kEvents) / sizeof(kEvents[0]));

	std::atomic<bool> g_hold = false;
	bool g_hooked = false;

	// ---- Reading and sending (guarded) ----

	uint8_t* ScreenEffects()
	{
		const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kScreenEffects);
		if (!at)
			return nullptr;
		__try
		{
			return *reinterpret_cast<uint8_t* const*>(at);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	struct State
	{
		bool lowHealth, strangle, death;
		int monitors;
	};

	bool ReadState(State& out)
	{
		const uint8_t* effects = ScreenEffects();
		if (!effects)
			return false;
		__try
		{
			out.lowHealth = effects[kLowHealthShowing] != 0;
			out.strangle = effects[kStrangleShowing] != 0;
			out.death = effects[kDeathShowing] != 0;
			out.monitors = 0;
			const uint32_t* handles = reinterpret_cast<const uint32_t*>(effects);
			for (int i = 0; i < kMonitors; i++)
				if (handles[i * 2] || handles[i * 2 + 1])
					out.monitors++;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	enum class Fired { Sent, AlreadyOn, AlreadyOff, Unavailable, Faulted };

	// Entity thread
	Fired Fire(int event)
	{
		uint8_t* effects = ScreenEffects();
		if (!effects || !screen_effect_event || event < 0 || event >= kEventCount)
			return Fired::Unavailable;
		__try
		{
			const int showing = kEvents[event].showing;
			if (showing >= 0)
			{
				const bool on = effects[showing] != 0;
				if (kEvents[event].on && on)
					return Fired::AlreadyOn;
				if (!kEvents[event].on && !on)
					return Fired::AlreadyOff;
			}
			screen_effect_event(effects, event);
			return Fired::Sent;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return Fired::Faulted;
		}
	}

	std::string Outcome(int event, Fired fired)
	{
		const std::string name = kEvents[event].label;
		switch (fired)
		{
		case Fired::Sent: return name + ": sent";
		case Fired::AlreadyOn: return name + ": already showing, nothing sent (the game sends each change once)";
		case Fired::AlreadyOff: return name + ": not showing, nothing sent (the game sends each change once)";
		case Fired::Unavailable: return name + ": not available on this release of the game";
		default: return name + ": not sent (the game's call faulted)";
		}
	}

	// ---- The hold (the hook runs on the thread that updates the player's health) ----

	void __fastcall h_health_screen_effects(void* health, void* /*_EDX*/)
	{
		if (g_hold)
			return;
		health_screen_effects(health);
	}

	void AttachHooks(bool attach)
	{
		if (attach && health_screen_effects && !g_hooked)
		{
			DetourAttach(reinterpret_cast<void**>(&health_screen_effects), reinterpret_cast<void*>(&h_health_screen_effects));
			g_hooked = true;
		}
		else if (!attach && g_hooked)
		{
			g_hold = false;
			DetourDetach(reinterpret_cast<void**>(&health_screen_effects), reinterpret_cast<void*>(&h_health_screen_effects));
			g_hooked = false;
		}
	}

	// ---- Status ----

	std::mutex g_statusMutex;
	std::string g_status;

	void SetStatus(const std::string& text)
	{
		{
			std::lock_guard<std::mutex> lock(g_statusMutex);
			g_status = text;
		}
		DevTools::Log("Debug menu: screen effect: %s", text.c_str());
	}

	const DEBUG_MENU::Character* Player()
	{
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
			if (character.player)
				return &character;
		return nullptr;
	}

	// Render thread, every frame: the hold lets go with no player, or when the player changes (a checkpoint restart or a
	// new level)
	uint32_t g_holdPlayer = 0;

	void Apply(ImDrawList* /*drawList*/)
	{
		if (!g_hold)
		{
			g_holdPlayer = 0;
			return;
		}
		const DEBUG_MENU::Character* player = Player();
		if (!player || !g_hooked)
		{
			g_hold = false;
			g_holdPlayer = 0;
			SetStatus(g_hooked ? "hold let go: no player (no level running)" : "hold: not available on this release of the game");
			return;
		}
		if (!g_holdPlayer)
			g_holdPlayer = player->id;
		else if (g_holdPlayer != player->id)
		{
			g_hold = false;
			g_holdPlayer = 0;
			SetStatus("hold let go: the player changed (checkpoint restart or a new level)");
		}
	}

	// ---- The section ----

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

	void Ask(int event)
	{
		SetStatus(std::string(kEvents[event].label) + ": asked (sent at the scripts' next update)");
		DEBUG_MENU::OnEntityThread([event]() { SetStatus(Outcome(event, Fire(event))); });
	}

	void DrawSection()
	{
		ImGui::SeparatorText("Screen effects");
		if (!screen_effect_event || !ScreenEffects())
		{
			ImGui::TextDisabled(screen_effect_event ? "No level running." : "Not available on this release of the game.");
			return;
		}
		ImGui::PushTextWrapPos(0.0f);
		ImGui::TextDisabled("The events the game sends the HUD's screen-effect monitors when the player is hurt, strangled, healed or killed, sent by hand: "
			"for testing mods of the HUD and damage-overlay composites.");
		ImGui::PopTextWrapPos();
		State state = {};
		if (ReadState(state))
		{
			std::string showing;
			if (state.lowHealth)
				showing += "low health";
			if (state.strangle)
				showing += std::string(showing.empty() ? "" : ", ") + "strangle";
			if (state.death)
				showing += std::string(showing.empty() ? "" : ", ") + "death";
			ImGui::Text("Showing now: %s. Monitors registered: %d of %d.", showing.empty() ? "none" : showing.c_str(), state.monitors, kMonitors);
		}
		const bool havePlayer = Player() != nullptr;
		ImGui::BeginDisabled(!havePlayer);
		if (ImGui::BeginTable("screen_effects", 4, ImGuiTableFlags_SizingStretchSame))
		{
			for (int event = 0; event < kEventCount; event++)
			{
				ImGui::TableNextColumn();
				char label[64];
				snprintf(label, sizeof(label), "%s##event%d", kEvents[event].label, event);
				if (ImGui::Button(label, ImVec2(-FLT_MIN, 0.0f)))
					Ask(event);
				char tip[384];
				snprintf(tip, sizeof(tip), "%s\n\nEvent %d (%s). The level's ScreenEffectEventMonitor entities pass it on to the HUD and damage-overlay composites: "
					"for testing mods of those.", kEvents[event].what, event, kEvents[event].key);
				Tooltip(tip);
			}
			ImGui::EndTable();
		}
		ImGui::EndDisabled();
		if (!havePlayer)
			ImGui::TextDisabled("No player: the game sends these only while there is one.");
		ImGui::BeginDisabled(!g_hooked);
		bool hold = g_hold;
		if (ImGui::Checkbox("Hold low health and death", &hold))
			g_hold = hold;
		ImGui::EndDisabled();
		Tooltip("The game ends low health and death by itself once a frame unless the player's health is low or the player is dying. While this is on it "
			"doesn't, so they stay until sent off. Switching it off (or a checkpoint restart or new level) lets the game end whatever doesn't apply.");

		std::lock_guard<std::mutex> lock(g_statusMutex);
		if (!g_status.empty())
		{
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextDisabled("Last: %s", g_status.c_str());
			ImGui::PopTextWrapPos();
		}
	}

	// ---- Commands ----

	// An event by its name (as the list gives it, any case) or number, -1 when none
	int EventFromText(std::string text)
	{
		for (char& c : text)
			c = static_cast<char>(tolower(static_cast<unsigned char>(c)));
		if (text.empty())
			return -1;
		char* end = nullptr;
		const long number = strtol(text.c_str(), &end, 10);
		if (end && *end == '\0')
			return number >= 0 && number < kEventCount ? static_cast<int>(number) : -1;
		for (int event = 0; event < kEventCount; event++)
			if (text == kEvents[event].key)
				return event;
		return -1;
	}

	bool RegisterControls()
	{
		DEBUG_MENU::AddSwitch("player.hold_screen_effects", &g_hold);
		DEBUG_MENU::AddCommand("player.screen_effect", "<name|number> - sends a screen-effect event to the HUD's monitors, as the game does when the player is "
			"hurt, strangled, healed or killed (player.screen_effects lists them; entity thread)", [](const std::string& argument) -> DEBUG_MENU::CommandResult
		{
			std::istringstream stream(argument);
			std::string word;
			stream >> word;
			const int event = EventFromText(word);
			if (event < 0)
				return { false, "no screen-effect event called " + word + " (player.screen_effects lists them)" };
			if (!screen_effect_event)
				return { false, "not available on this release of the game" };
			const DEBUG_MENU::CommandResult player = DEBUG_MENU::AnswerOnRenderThread([]() -> DEBUG_MENU::CommandResult
			{
				return { Player() != nullptr, "no player (no level running?): the game sends these only while there is one" };
			});
			if (!player.ok)
				return player;
			return DEBUG_MENU::AnswerOnEntityThread([event]() -> DEBUG_MENU::CommandResult
			{
				const Fired fired = Fire(event);
				const std::string text = Outcome(event, fired);
				SetStatus(text);
				return { fired == Fired::Sent || fired == Fired::AlreadyOn || fired == Fired::AlreadyOff, text };
			});
		});
		DEBUG_MENU::AddCommand("player.screen_effects", "the screen-effect events (number and name, a line each), then which are showing, how many monitors "
			"are registered and whether the hold is on", [](const std::string&) -> DEBUG_MENU::CommandResult
		{
			std::string out;
			for (int event = 0; event < kEventCount; event++)
				out += std::to_string(event) + " " + kEvents[event].key + "\n";
			State state = {};
			if (ReadState(state))
			{
				char text[160];
				snprintf(text, sizeof(text), "low_health=%d\nstrangle=%d\ndeath=%d\nmonitors=%d\n", state.lowHealth ? 1 : 0, state.strangle ? 1 : 0, state.death ? 1 : 0,
					state.monitors);
				out += text;
			}
			else
				out += screen_effect_event ? "state=no level running\n" : "state=not available on this release of the game\n";
			out += std::string("hold=") + (g_hold ? "1" : "0") + (g_hooked ? "" : " (not available)") + "\n";
			return { true, out };
		});
		return true;
	}

	const bool g_controls = RegisterControls();
	const bool g_added = DEBUG_MENU::AddTab("Player", 35, DrawSection, "Characters") && DEBUG_MENU::AddOverlay(9, Apply);
	const bool g_hooks = DEBUG_MENU::AddHooks(AttachHooks);
}

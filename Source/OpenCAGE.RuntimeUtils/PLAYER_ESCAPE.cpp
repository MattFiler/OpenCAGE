#include "DEBUG_MENU.h"

#include <atomic>
#include <cstdio>
#include <string>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <detours.h>
#include <imgui.h>

/*
	Addresses and layouts are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).

	Break free of strangles ("player.auto_grapple_escape", a checkbox among the Player tab's cheats): a human or an android
	that grabs the player and strangles them lets go, as if the player had pressed the button to break free fast enough.

	While a person or an android holds someone in a strangle, the game runs the grab as the attacker's action, every
	frame. The player breaks free by pressing a button: each press is a break request carrying the player's own script
	entity handle. Once the victim may start breaking out, the grab counts each request whose handle is its victim's (as
	more than one in a long frame, at a low frame rate), pulses the game's grapple icon and fills its bar, and at the
	attacker's number of presses breaks the grab. The grab's frame is hooked: while the switch is on and the victim is the
	player, the grab is handed a break request with the player's handle every 0.2 s of game time, through the game's own
	handling of a press, before the grab's own frame runs - as if the player pressed the button five times a second. When
	the last request went, and to which grab, is kept here.

	Nothing is kept in the game: the presses counted are the game's own count for that one grab, gone with it. The alien's
	grab is another action (a kill, not a strangle) and is left alone - "The alien can't grab the player" is for that.
*/
namespace
{
	// The grab's frame, and its handling of a break request
	constexpr uintptr_t kGrabPerform = 0x00263A10;
	constexpr uintptr_t kGrabBreakRequest = 0x0025F300;
	// The game clock: the game time now (a double)
	constexpr uintptr_t kGameTime = 0x012410A8;

	// The grab's fields: the attacker (a pointer to its character), the victim (the same), whether the victim may start
	// breaking out yet, and the presses counted so far
	constexpr uint32_t kGrabAttacker = 0x04;
	constexpr uint32_t kGrabVictim = 0x44;
	constexpr uint32_t kGrabCanBreak = 0xBF;
	constexpr uint32_t kGrabPresses = 0xC0;
	// A character's script entity handle (two ids), its player data (a pointer: set at +0x48 while a player controls it),
	// and its constants (a pointer: how many presses break its grab at +0x2C4, a short)
	constexpr uint32_t kCharacterHandle = 0x54;
	constexpr uint32_t kCharacterPlayable = 0x31C;
	constexpr uint32_t kPlayableControlled = 0x48;
	constexpr uint32_t kCharacterConstants = 0x1154;
	constexpr uint32_t kConstantsBreakPresses = 0x2C4;

	// A press every this many seconds of game time
	constexpr double kInterval = 0.2;

	typedef void(__thiscall* t_grab_perform)(void* grab);
	typedef void(__thiscall* t_grab_break_request)(void* grab, const uint32_t* request);
	t_grab_perform g_grabPerform = nullptr;
	t_grab_break_request g_grabBreakRequest = nullptr;
	std::atomic<bool> g_hooked = false;

	std::atomic<bool> g_on = false;

	// The grab the last request went to and when (game time), and the grab last counted as broken (each only compared,
	// never followed)
	std::atomic<uintptr_t> g_lastGrab = 0;
	std::atomic<double> g_lastTime = 0.0;
	std::atomic<uintptr_t> g_brokenGrab = 0;

	// Since the game started: the requests sent and the grabs broken; when the last of each was (GetTickCount64), and the
	// presses counted against those needed after the last request
	std::atomic<uint32_t> g_requests = 0;
	std::atomic<uint32_t> g_escapes = 0;
	std::atomic<ULONGLONG> g_lastRequestTick = 0;
	std::atomic<ULONGLONG> g_lastEscapeTick = 0;
	std::atomic<int> g_lastPresses = 0;
	std::atomic<int> g_lastNeeded = 0;

	// ---- The hook ----

	struct Grab
	{
		bool victimIsPlayer = false;
		bool canBreak = false;
		uint32_t handle[2] = {};   // the victim's
		int presses = 0;
		int needed = 0;            // 0: not known
	};

	// The grab's thread: what the grab holds (guarded: false when unreadable)
	bool ReadGrab(const uint8_t* grab, Grab& out)
	{
		__try
		{
			const uint8_t* victim = *reinterpret_cast<uint8_t* const*>(grab + kGrabVictim);
			if (!victim)
				return false;
			const uint8_t* playable = *reinterpret_cast<uint8_t* const*>(victim + kCharacterPlayable);
			out.victimIsPlayer = playable && playable[kPlayableControlled];
			out.handle[0] = *reinterpret_cast<const uint32_t*>(victim + kCharacterHandle);
			out.handle[1] = *reinterpret_cast<const uint32_t*>(victim + kCharacterHandle + 4);
			out.canBreak = grab[kGrabCanBreak] != 0;
			out.presses = *reinterpret_cast<const int*>(grab + kGrabPresses);
			const uint8_t* attacker = *reinterpret_cast<uint8_t* const*>(grab + kGrabAttacker);
			const uint8_t* constants = attacker ? *reinterpret_cast<uint8_t* const*>(attacker + kCharacterConstants) : nullptr;
			out.needed = constants ? *reinterpret_cast<const short*>(constants + kConstantsBreakPresses) : 0;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool GameTime(double& out)
	{
		__try
		{
			const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kGameTime);
			if (!at)
				return false;
			out = *reinterpret_cast<const volatile double*>(at);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// The game's handling of a press: a break request with the victim's handle
	bool SendBreakRequest(void* grab, const uint32_t* handle)
	{
		__try
		{
			g_grabBreakRequest(grab, handle);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// The grab's thread, before its frame runs: a break request every interval while the player is its victim
	void Escape(void* grab)
	{
		Grab view;
		if (!ReadGrab(static_cast<const uint8_t*>(grab), view) || !view.victimIsPlayer || !view.canBreak || (!view.handle[0] && !view.handle[1]))
			return;
		double now = 0.0;
		if (!GameTime(now))
			return;
		const uintptr_t at = reinterpret_cast<uintptr_t>(grab);
		const double last = g_lastTime;
		// Another grab, or the clock gone back (a checkpoint, or another level): from the start. The same grab waits out
		// the interval.
		if (g_lastGrab != at || now < last)
		{
			g_lastGrab = at;
			g_brokenGrab = 0;
		}
		else if (now - last < kInterval)
			return;
		g_lastTime = now;
		if (!SendBreakRequest(grab, view.handle))
			return;
		g_requests++;
		g_lastRequestTick = GetTickCount64();
		Grab after;
		if (!ReadGrab(static_cast<const uint8_t*>(grab), after))
			return;
		g_lastPresses = after.presses;
		g_lastNeeded = after.needed;
		if (after.needed > 0 && after.presses >= after.needed && g_brokenGrab != at)
		{
			g_brokenGrab = at;
			g_escapes++;
			g_lastEscapeTick = GetTickCount64();
			DevTools::Log("Debug menu: broke free of a strangle (%d presses of %d)", after.presses, after.needed);
		}
	}

	// Hooked: the grab's frame
	void __fastcall GrabPerform(void* grab, void* /*edx*/)
	{
		if (g_on)
			Escape(grab);
		g_grabPerform(grab);
	}

	void AttachHooks(bool attach)
	{
		if (attach)
		{
			g_grabPerform = reinterpret_cast<t_grab_perform>(DEVTOOLS_RELATIVE_ADDRESS(kGrabPerform));
			g_grabBreakRequest = reinterpret_cast<t_grab_break_request>(DEVTOOLS_RELATIVE_ADDRESS(kGrabBreakRequest));
			if (g_grabPerform && g_grabBreakRequest && DEVTOOLS_RELATIVE_ADDRESS(kGameTime))
			{
				DetourAttach(&reinterpret_cast<PVOID&>(g_grabPerform), reinterpret_cast<PVOID>(&GrabPerform));
				g_hooked = true;
			}
			DevTools::Log("Debug menu: strangle escape hook %s", g_hooked ? "attached" : "not attached (no address in this build)");
		}
		else
		{
			g_on = false;
			if (g_hooked)
				DetourDetach(&reinterpret_cast<PVOID&>(g_grabPerform), reinterpret_cast<PVOID>(&GrabPerform));
			g_hooked = false;
		}
	}

	// ---- Named switch and command ----

	bool g_registered = false;

	std::string Ago(ULONGLONG tick)
	{
		if (!tick)
			return "never";
		char text[32];
		snprintf(text, sizeof(text), "%.1f", (GetTickCount64() - tick) / 1000.0);
		return text;
	}

	void Register()
	{
		if (g_registered)
			return;
		g_registered = true;
		// The switch only where the hook is in (the Player tab's checkbox is greyed out without it)
		if (g_hooked)
			DEBUG_MENU::AddSwitch("player.auto_grapple_escape", &g_on);
		DEBUG_MENU::AddCommand("player.auto_grapple_escape_status", "Break free of strangles: on or off, the break requests sent and strangles broken since the game started, seconds since the last of each, and the presses counted against those needed after the last request", [](const std::string&)
		{
			if (!g_hooked)
				return DEBUG_MENU::CommandResult{ false, "not available on this game release" };
			char text[320];
			snprintf(text, sizeof(text), "on=%d\nrequests=%u\nescapes=%u\nlast_request_s_ago=%s\nlast_escape_s_ago=%s\nlast_presses=%d\nlast_needed=%d\n", g_on ? 1 : 0,
				g_requests.load(), g_escapes.load(), Ago(g_lastRequestTick).c_str(), Ago(g_lastEscapeTick).c_str(), g_lastPresses.load(), g_lastNeeded.load());
			return DEBUG_MENU::CommandResult{ true, text };
		});
	}

	// Render thread, every frame: names it; turned off, it forgets the grab it last pressed for
	void Apply(ImDrawList* /*drawList*/)
	{
		Register();
		if (!g_on && g_lastGrab)
		{
			g_lastGrab = 0;
			g_brokenGrab = 0;
		}
	}

	const bool g_overlayAdded = DEBUG_MENU::AddOverlay(7, Apply);
	const bool g_hooksAdded = DEBUG_MENU::AddHooks(AttachHooks);
}

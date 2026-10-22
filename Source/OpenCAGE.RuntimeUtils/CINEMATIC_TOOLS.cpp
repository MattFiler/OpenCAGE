#include "AI_STATE.h"
#include "CINEMATIC_CAMERA.h"
#include "CINEMATIC_VISUALS.h"
#include "Config.h"
#include "DEBUG_MENU.h"
#include "LIVE_LINK.h"
#include "Menu.h"
#include "SCREENSHOT.h"

#include <algorithm>
#include <atomic>
#include <cstdio>
#include <cstring>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <imgui.h>

/*
	Addresses and layouts are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).

	The Cinematic group's Cast and Screenshots tabs.

	Cast (Cinematic Tools' character tools): the characters other than the player held still ("Freeze NPCs", Cinematic
	Tools' key F7) and let go of again; the player made invisible to the AI (the Game group's switch, which Cinematic
	Tools' invisibility stood for); the camera taken behind a character or locked to it, and the player moved to the camera
	(the Free Camera tab's, here too).

	Freeze NPCs: the game's own freeze of a character only holds one the player controls, so the characters are held
	through their behaviour trees instead. People get the "force idle" debug motivation
	(their behaviour trees' idle branch then runs ahead of everything else, scripted orders included), given on the AI
	thread to every character but the player that hasn't it, and taken away again only from those it was given to here.
	The alien's and the androids' trees have no such branch but each has a debug switch that idles them (AI > Tree
	Switches: "Disable All Aliens", "Force Android Idle"), turned on with the freeze and off again only when it was the
	freeze that turned it on. Their senses and minds keep running (they still react to being hurt), an android following a
	script's orders carries on with them, and a facehugger's tree has nothing to idle it. Newly spawned characters are
	held too (looked for twice a second), and the trees' switches, which every level load unticks, are ticked again. The
	game keeps force idle in its saves: a checkpoint saved while people are held brings them back idle - let go of with
	the rest when they come back while the freeze is on, otherwise only by hand (Characters > Control).

	Screenshots: the frame as the game drew it - without this window, its overlays or anything else RuntimeUtils draws -
	written as a .bmp into a Screenshots folder in the game folder (next to OpenCAGE.RuntimeUtils.ini), named after the
	level and the time. With "Hide the HUD for the shot" the in-game HUD is hidden for the frames it takes (the HUD tab's
	layers), then shown again. Its key (CinematicScreenshotKey, F9 by default) takes one with the window closed or open.
*/
namespace
{
	// ---- Holding the characters other than the player still (AI thread, and the trees' switches) ----

	// The game's globals (a pointer to them); the byte after that pointer is set while its characters can be read. In the
	// globals, the character manager, with its list of the characters alive that aren't players (a pointer to their
	// pointers, then how many)
	constexpr uintptr_t kGameGlobals = 0x012F0C88;
	constexpr uint32_t kCharactersValid = 0x04;
	constexpr uint32_t kGlobalsCharacters = 0x08;
	constexpr uint32_t kAlive = 0xD0;
	constexpr uint32_t kMaxPerList = 18;
	constexpr uint32_t kCharacterId = 0x50;
	// A character's motivations (a pointer to them; the player has none): one byte each, by number (1..35), from here on
	// (as AI_STATE reads them)
	constexpr uint32_t kCharacterMotivations = 0x3498;
	constexpr uint32_t kMotivationFirst = 0x0C;
	constexpr uintptr_t kEntityManager = 0x0134EF40;
	constexpr uint32_t kEntityRoot = 0x0C;

	// The trees' debug switches that idle the alien and the androids (AI > Tree Switches), by their dotted names
	const char* const kIdleSwitches[] = { "tree_switch.Disable All Aliens", "tree_switch.Force Android Idle" };
	constexpr int kIdleSwitchCount = static_cast<int>(sizeof(kIdleSwitches) / sizeof(kIdleSwitches[0]));
	// Character classes (as DEBUG_MENU::ClassName numbers them): the alien, held by the first switch; androids and heavy
	// androids, held by the second; facehuggers, held by nothing here (for the count)
	constexpr int kClassAlien = 1, kClassAndroid = 2, kClassFacehugger = 5, kClassHeavyAndroid = 7;

	std::atomic<bool> g_freezeNpcs = false;   // cinematic.freeze_npcs
	std::atomic<bool> g_freezeQueued = false;
	std::atomic<int> g_frozenCount = 0;       // characters held still, as of the last look (render thread)
	std::atomic<int> g_notHeldCount = 0;      // characters nothing here can hold (facehuggers)
	std::atomic<int> g_forceIdleCount = 0;    // characters given force idle here (AI thread)
	ULONGLONG g_freezeCheckedAt = 0;          // render thread

	// The trees' switches: those the freeze turned on (render thread), and as it last saw them (for the status: under the
	// lock, which is never held while another lock is taken)
	bool g_switchOurs[kIdleSwitchCount] = {};
	std::mutex g_switchMutex;
	int g_switchState[kIdleSwitchCount] = {};     // -1: no such switch (no tree had it yet), 0 off, 1 on
	bool g_switchOursShown[kIdleSwitchCount] = {};
	bool g_selectedOnly = false;                  // the trees' switches only count for the selected character

	struct Held
	{
		uint8_t* character;
		uint32_t id;
	};
	// The AI thread only: the characters given force idle here, and those that had it already when the freeze was turned
	// on (left be; noted once the characters can be read)
	std::vector<Held> g_held;
	uint32_t g_heldRoot = 0;
	std::vector<uint32_t> g_hadIt;
	bool g_hadItNoted = false;

	uint8_t* AliveList(uint32_t& count)
	{
		__try
		{
			count = 0;
			const uintptr_t globalsAt = DEVTOOLS_RELATIVE_ADDRESS(kGameGlobals);
			if (!globalsAt || !*reinterpret_cast<const uint8_t*>(globalsAt + kCharactersValid))
				return nullptr;
			uint8_t* globals = *reinterpret_cast<uint8_t**>(globalsAt);
			uint8_t* manager = globals ? *reinterpret_cast<uint8_t**>(globals + kGlobalsCharacters) : nullptr;
			if (!manager)
				return nullptr;
			count = *reinterpret_cast<const uint32_t*>(manager + kAlive + 4);
			return *reinterpret_cast<uint8_t**>(manager + kAlive);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			count = 0;
			return nullptr;
		}
	}

	// Whether a character has force idle: 1 or 0, -1 when it has no motivations (or they can't be read)
	int ReadForceIdle(const uint8_t* character)
	{
		__try
		{
			const uint8_t* motivations = *reinterpret_cast<const uint8_t* const*>(character + kCharacterMotivations);
			if (!motivations)
				return -1;
			return motivations[kMotivationFirst + AI_STATE::kForceIdleMotivation - 1] ? 1 : 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return -1;
		}
	}

	// The characters alive that aren't players, into out (at most max), with whether each has force idle (as
	// ReadForceIdle): the count, -1 when they can't be read
	int AliveCharacters(uint8_t** out, uint32_t* ids, int* idle, int max)
	{
		uint32_t count = 0;
		uint8_t* list = AliveList(count);
		if (!list)
			return -1;
		__try
		{
			int found = 0;
			uint8_t* const* entries = reinterpret_cast<uint8_t* const*>(list);
			for (uint32_t i = 0; i < count && i < kMaxPerList && found < max; i++)
			{
				uint8_t* character = entries[i];
				if (!character)
					continue;
				out[found] = character;
				ids[found] = *reinterpret_cast<const uint32_t*>(character + kCharacterId);
				found++;
			}
			for (int i = 0; i < found; i++)
				idle[i] = ReadForceIdle(out[i]);
			return found;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return -1;
		}
	}

	uint32_t RunningRoot()
	{
		__try
		{
			if (!LIVE_LINK::LevelRunning())
				return 0;
			const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kEntityManager);
			const uint8_t* manager = at ? *reinterpret_cast<uint8_t* const*>(at) : nullptr;
			return manager ? *reinterpret_cast<const uint32_t*>(manager + kEntityRoot) : 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	// AI thread: gives force idle to those without it (newly spawned ones too) while wanted, gives it again to those it was
	// given to here if something took it away (a script, the AI tab), and takes it away once not wanted - only from those
	// it was given to here, still there in the same level, and still with it. Characters that had it already when the
	// freeze was turned on are left be. The game keeps force idle in its saves, so characters that turn up already
	// holding it while the freeze is on (brought back by a checkpoint saved while they were held, or in a level loaded
	// meanwhile) are taken as given here, and let go of with the rest.
	void ReconcileNpcs()
	{
		g_freezeQueued = false;
		const bool wanted = g_freezeNpcs;
		const uint32_t root = RunningRoot();
		uint8_t* characters[kMaxPerList];
		uint32_t ids[kMaxPerList];
		int idle[kMaxPerList];
		const int count = root ? AliveCharacters(characters, ids, idle, kMaxPerList) : -1;
		if (root != g_heldRoot)
		{
			g_held.clear();
			g_hadIt.clear();
			g_heldRoot = root;
		}
		if (!wanted)
		{
			g_hadIt.clear();
			g_hadItNoted = false;
		}
		if (count < 0)
		{
			g_forceIdleCount = static_cast<int>(g_held.size());
			return;
		}
		if (wanted && !g_hadItNoted)
		{
			for (int i = 0; i < count; i++)
				if (idle[i] == 1)
					g_hadIt.push_back(ids[i]);
			g_hadItNoted = true;
		}
		auto present = [&](const Held& held, int& index)
		{
			for (int i = 0; i < count; i++)
				if (characters[i] == held.character && ids[i] == held.id)
				{
					index = i;
					return true;
				}
			return false;
		};
		std::vector<Held> kept;
		for (const Held& held : g_held)
		{
			int index = -1;
			if (!present(held, index))
				continue;
			if (wanted)
			{
				if (idle[index] == 0)
					AI_STATE::ApplyMotivation(held.character, AI_STATE::kForceIdleMotivation, 0);
				kept.push_back(held);
			}
			else if (idle[index] == 1)
				AI_STATE::ApplyMotivation(held.character, AI_STATE::kForceIdleMotivation, 1);
		}
		if (wanted)
			for (int i = 0; i < count; i++)
			{
				if (idle[i] < 0)
					continue;
				bool ours = false;
				for (const Held& held : kept)
					ours |= held.character == characters[i];
				if (ours)
					continue;
				if (idle[i] == 0)
				{
					if (AI_STATE::ApplyMotivation(characters[i], AI_STATE::kForceIdleMotivation, 0))
						kept.push_back({ characters[i], ids[i] });
				}
				else if (std::find(g_hadIt.begin(), g_hadIt.end(), ids[i]) == g_hadIt.end())
					kept.push_back({ characters[i], ids[i] });
			}
		g_held.swap(kept);
		g_forceIdleCount = static_cast<int>(g_held.size());
	}

	// Render thread: the trees' idle switches brought in line with the freeze - ticked while it is on (a level load unticks
	// them all), and unticked when it is turned off, only those it ticked
	void FollowSwitches(bool wanted)
	{
		int state[kIdleSwitchCount];
		for (int i = 0; i < kIdleSwitchCount; i++)
		{
			bool on = false;
			if (!DEBUG_MENU::GetSwitch(kIdleSwitches[i], on))
			{
				state[i] = -1;
				continue;
			}
			if (wanted && !on && DEBUG_MENU::SetSwitch(kIdleSwitches[i], true))
			{
				g_switchOurs[i] = true;
				on = true;
			}
			else if (!wanted && g_switchOurs[i])
			{
				if (on)
					DEBUG_MENU::SetSwitch(kIdleSwitches[i], false);
				g_switchOurs[i] = false;
				on = false;
			}
			state[i] = on ? 1 : 0;
		}
		bool selectedOnly = false;
		if (!DEBUG_MENU::GetSwitch("bt.switches_selected_only", selectedOnly))
			selectedOnly = false;
		std::lock_guard<std::mutex> lock(g_switchMutex);
		for (int i = 0; i < kIdleSwitchCount; i++)
		{
			g_switchState[i] = state[i];
			g_switchOursShown[i] = g_switchOurs[i];
		}
		g_selectedOnly = selectedOnly;
	}

	// Render thread: how many of this frame's characters (bar the player and the dead) are held still - with force idle,
	// or the alien or an android while its tree's switch is on for every character - and how many nothing here holds
	void CountHeld()
	{
		bool alien, android;
		{
			std::lock_guard<std::mutex> lock(g_switchMutex);
			alien = g_switchState[0] == 1 && !g_selectedOnly;
			android = g_switchState[1] == 1 && !g_selectedOnly;
		}
		int held = 0, notHeld = 0;
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
		{
			if (character.player || character.dead)
				continue;
			const int type = character.characterClass;
			if (type == kClassAlien)
				held += alien ? 1 : 0;
			else if (type == kClassAndroid || type == kClassHeavyAndroid)
				held += android ? 1 : 0;
			else if (type == kClassFacehugger)
				notHeld++;
			else
				held += ReadForceIdle(character.object) == 1 ? 1 : 0;
		}
		g_frozenCount = held;
		g_notHeldCount = notHeld;
	}

	// Render thread, every frame: while wanted, newly spawned characters are held too and the trees' switches kept ticked
	// (looked at twice a second); turned off, what was turned on here is turned off - looked at again twice a second until
	// those given force idle here are let go of (they can't be while a checkpoint loads)
	bool g_freezeWasOn = false;
	void FollowFreeze()
	{
		const bool wanted = g_freezeNpcs;
		const ULONGLONG now = GetTickCount64();
		const bool releasing = !wanted && g_forceIdleCount > 0;
		if (wanted == g_freezeWasOn && ((!wanted && !releasing) || now - g_freezeCheckedAt < 500))
			return;
		g_freezeWasOn = wanted;
		g_freezeCheckedAt = now;
		FollowSwitches(wanted);
		if (wanted)
			CountHeld();
		else
		{
			g_frozenCount = 0;
			g_notHeldCount = 0;
		}
		if (!g_freezeQueued.exchange(true))
			DEBUG_MENU::OnAiThread(ReconcileNpcs);
	}

	// Characters > Player's "invisible to the AI" switch (tools.invisible; player.invisible is looked for first in case it is renamed)
	const char* InvisibleSwitch()
	{
		bool value = false;
		if (DEBUG_MENU::GetSwitch("player.invisible", value))
			return "player.invisible";
		if (DEBUG_MENU::GetSwitch("tools.invisible", value))
			return "tools.invisible";
		return nullptr;
	}

	// ---- Screenshots (render thread) ----

	std::atomic<bool> g_shotHideHud = true;   // cinematic.screenshot_hide_hud

	enum class ShotState { Idle, Waiting, Done };
	struct Shot
	{
		ShotState state = ShotState::Idle;
		uint32_t id = 0;
		std::string path;        // UTF-8
		bool hideHud = false;
		bool holding = false;
		ULONGLONG askedAt = 0;   // when it was asked for
		int frames = 0;          // frames waited
		bool ok = false;
		std::string message;
	};
	std::mutex g_shotMutex;
	Shot g_shot;
	uint32_t g_nextShot = 1;
	std::string g_lastShot;

	std::string Utf8(const std::wstring& text)
	{
		if (text.empty())
			return std::string();
		const int length = WideCharToMultiByte(CP_UTF8, 0, text.c_str(), -1, nullptr, 0, nullptr, nullptr);
		std::string out(static_cast<size_t>(length > 0 ? length : 1), '\0');
		WideCharToMultiByte(CP_UTF8, 0, text.c_str(), -1, &out[0], length, nullptr, nullptr);
		out.resize(strlen(out.c_str()));
		return out;
	}

	std::wstring Wide(const std::string& text)
	{
		const int length = MultiByteToWideChar(CP_UTF8, 0, text.c_str(), -1, nullptr, 0);
		std::wstring out(static_cast<size_t>(length > 0 ? length : 1), L'\0');
		MultiByteToWideChar(CP_UTF8, 0, text.c_str(), -1, &out[0], length);
		out.resize(wcslen(out.c_str()));
		return out;
	}

	// The Screenshots folder in the game folder (made when missing), with a trailing slash; UTF-8
	std::string ShotFolder()
	{
		wchar_t exePath[MAX_PATH] = {};
		GetModuleFileNameW(nullptr, exePath, MAX_PATH);
		std::wstring folder = exePath;
		const size_t slash = folder.find_last_of(L"\\/");
		folder = (slash == std::wstring::npos ? std::wstring() : folder.substr(0, slash + 1)) + L"Screenshots\\";
		CreateDirectoryW(folder.c_str(), nullptr);
		return Utf8(folder);
	}

	// A file name for a shot: given (a plain name, .bmp added), or the level and the time
	bool ShotPath(const std::string& given, const std::string& level, std::string& path)
	{
		std::string name;
		for (char c : given)
			if (c != '"' && c != '\r' && c != '\n')
				name += c;
		while (!name.empty() && name.back() == ' ')
			name.pop_back();
		while (!name.empty() && name.front() == ' ')
			name.erase(name.begin());
		if (name.find_first_of("\\/:*?<>|") != std::string::npos || name.find("..") != std::string::npos)
			return false;
		if (name.empty())
		{
			std::string levelName = level.empty() ? std::string("frontend") : level;
			const size_t slash = levelName.find_last_of("\\/");
			if (slash != std::string::npos)
				levelName = levelName.substr(slash + 1);
			for (char& c : levelName)
				if (!isalnum(static_cast<unsigned char>(c)) && c != '_' && c != '-')
					c = '_';
			SYSTEMTIME time;
			GetLocalTime(&time);
			char stamp[64];
			sprintf_s(stamp, "_%04u-%02u-%02u_%02u-%02u-%02u-%03u", time.wYear, time.wMonth, time.wDay, time.wHour, time.wMinute, time.wSecond, time.wMilliseconds);
			name = levelName + stamp;
		}
		if (name.size() < 4 || _stricmp(name.c_str() + name.size() - 4, ".bmp") != 0)
			name += ".bmp";
		path = ShotFolder() + name;
		return true;
	}

	// Render thread: asks for a shot; its id (0: one is already being taken)
	uint32_t RequestShot(const std::string& path, bool hideHud)
	{
		std::lock_guard<std::mutex> lock(g_shotMutex);
		// One waiting is replaced only when it never started (no level drawn since it was asked for)
		if (g_shot.state == ShotState::Waiting && (g_shot.frames > 0 || GetTickCount64() - g_shot.askedAt <= 5000))
			return 0;
		g_shot = Shot();
		g_shot.state = ShotState::Waiting;
		g_shot.id = g_nextShot++;
		g_shot.path = path;
		g_shot.hideHud = hideHud;
		g_shot.askedAt = GetTickCount64();
		return g_shot.id;
	}

	// The screenshot key: none (0) when it is another helper's key or the free camera's, as both would act on it
	int ScreenshotKey()
	{
		static const int key = []()
		{
			const int wanted = Config::IniKey("CinematicScreenshotKey", VK_F9);
			if (!wanted || (!CINEMATIC_CAMERA::TakenKey(wanted) && wanted != CINEMATIC_CAMERA::ToggleKey()))
				return wanted;
			DevTools::Log("Debug menu: screenshot: CinematicScreenshotKey is the same as another helper's key: set another");
			return 0;
		}();
		return key;
	}

	bool GameHasFocus()
	{
		const HWND foreground = GetForegroundWindow();
		DWORD process = 0;
		if (foreground)
			GetWindowThreadProcessId(foreground, &process);
		return foreground && process == GetCurrentProcessId();
	}

	bool g_keyWasDown = false;
	std::string g_level; // render thread

	// Render thread, every frame, before this window and the overlays are drawn: the key, and a shot due
	void TakeShots()
	{
		g_level = LIVE_LINK::RunningLevelName();
		const int key = ScreenshotKey();
		const bool down = key && (GetAsyncKeyState(key) & 0x8000) != 0;
		if (down && !g_keyWasDown && GameHasFocus())
		{
			std::string path;
			if (ShotPath("", g_level, path))
				RequestShot(path, g_shotHideHud);
		}
		g_keyWasDown = down;

		std::lock_guard<std::mutex> lock(g_shotMutex);
		Shot& shot = g_shot;
		if (shot.state != ShotState::Waiting)
			return;
		// Asked for while no level was drawn (this runs with one only): dropped, not taken frames later of another scene
		if (shot.frames == 0 && GetTickCount64() - shot.askedAt > 5000)
		{
			shot.ok = false;
			shot.message = "not taken: no level was drawn when it was asked for";
			shot.state = ShotState::Done;
			g_lastShot = shot.message;
			return;
		}
		if (shot.hideHud && !shot.holding)
		{
			CINEMATIC_VISUALS::HoldHudHidden(true);
			shot.holding = true;
		}
		// The HUD's layers are hidden from the frame after they are asked to be: three frames' margin once they are (the game
		// may build a frame ahead of the one it presents)
		shot.frames++;
		const bool ready = !shot.hideHud ? true : (CINEMATIC_VISUALS::HudHidden() && shot.frames >= 4) || shot.frames >= 30;
		if (!ready)
			return;
		std::string error;
		shot.ok = SCREENSHOT::Capture(Menu::GetSwapChain(), shot.path, error);
		shot.message = shot.ok ? "saved " + shot.path : error;
		if (shot.holding)
			CINEMATIC_VISUALS::HoldHudHidden(false);
		shot.holding = false;
		shot.state = ShotState::Done;
		g_lastShot = shot.message;
		DevTools::Log("Debug menu: screenshot: %s", shot.message.c_str());
	}

	void Overlay(ImDrawList* /*drawList*/)
	{
		FollowFreeze();
		TakeShots();
	}

	// ---- Names and the tabs ----

	void Name()
	{
		DEBUG_MENU::AddSwitch("cinematic.freeze_npcs", &g_freezeNpcs);
		DEBUG_MENU::AddSwitch("cinematic.screenshot_hide_hud", &g_shotHideHud);
		DEBUG_MENU::AddCommand("cinematic.freeze_status", "how many characters are held still, given force idle here, or not held; the trees' idle switches", [](const std::string&)
		{
			std::string text = std::string("freeze_npcs=") + (g_freezeNpcs ? "1" : "0") + "\nfrozen=" + std::to_string(g_frozenCount.load()) +
				"\nforce_idle_given=" + std::to_string(g_forceIdleCount.load()) + "\nnot_held=" + std::to_string(g_notHeldCount.load());
			std::lock_guard<std::mutex> lock(g_switchMutex);
			for (int i = 0; i < kIdleSwitchCount; i++)
				text += std::string("\n") + kIdleSwitches[i] + "=" + (g_switchState[i] < 0 ? "none (no tree had it yet)" : g_switchState[i] == 0 ? "off" :
					g_switchOursShown[i] ? "on (by the freeze)" : "on");
			text += std::string("\nswitch_scope=") + (g_selectedOnly ? "the selected character only" : "every character");
			return DEBUG_MENU::CommandResult{ true, text };
		});
		DEBUG_MENU::AddCommand("cinematic.screenshot", "\"file\" (optional): the frame as the game drew it, a .bmp in the game folder's Screenshots folder; waits for it", [](const std::string& argument)
		{
			// The level's name is the render thread's to read
			const DEBUG_MENU::CommandResult asked = DEBUG_MENU::AnswerOnRenderThread([argument]()
			{
				std::string path;
				if (!ShotPath(argument, g_level, path))
					return DEBUG_MENU::CommandResult{ false, "give a plain file name (it goes in the game folder's Screenshots folder)" };
				const uint32_t id = RequestShot(path, g_shotHideHud);
				if (!id)
					return DEBUG_MENU::CommandResult{ false, "a screenshot is already being taken" };
				return DEBUG_MENU::CommandResult{ true, std::to_string(id) };
			});
			if (!asked.ok)
				return asked;
			const uint32_t id = static_cast<uint32_t>(strtoul(asked.message.c_str(), nullptr, 10));
			for (int waited = 0; waited < 300; waited++)
			{
				{
					std::lock_guard<std::mutex> lock(g_shotMutex);
					if (g_shot.id != id)
						return DEBUG_MENU::CommandResult{ false, "the screenshot was replaced" };
					if (g_shot.state == ShotState::Done)
						return DEBUG_MENU::CommandResult{ g_shot.ok, g_shot.message };
				}
				Sleep(10);
			}
			return DEBUG_MENU::CommandResult{ false, "timed out (no frame drawn?) - it is taken at the next frames drawn" };
		});
	}

	const bool g_named = (Name(), true);

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

	// The note under the Cast tab, set from the tab and from the thread a command runs on
	std::mutex g_noteMutex;
	std::string g_charactersNote;

	void SetNote(const std::string& text)
	{
		std::lock_guard<std::mutex> lock(g_noteMutex);
		g_charactersNote = text;
	}

	// Another view's command, off the render thread (it may wait on a game thread, or on this one)
	void RunAsync(const std::string& name, const std::string& argument)
	{
		std::thread([name, argument]()
		{
			DEBUG_MENU::CommandResult result;
			if (!DEBUG_MENU::RunCommand(name, argument, result))
				SetNote(name + " is not in this build");
			else
				SetNote(result.message);
		}).detach();
	}

	void DrawCharacters()
	{
		ImGui::SeparatorText("Characters");
		bool freeze = g_freezeNpcs;
		if (ImGui::Checkbox("Freeze NPCs", &freeze))
			g_freezeNpcs = freeze;
		Tooltip("Every character but the player stands idle where it is, newly spawned ones too, until turned off (Cinematic Tools' Freeze characters). People through the force idle motivation (their behaviour trees' idle branch runs ahead of everything else, scripted orders included); the alien and androids through their trees' debug switches (AI > Tree Switches: Disable All Aliens, Force Android Idle). Their senses keep running and they still react to being hurt; an android following a script's orders carries on with them, and facehuggers have nothing to idle them. Turned off, only what this turned on is turned off again. The game keeps force idle in its saves: a checkpoint saved while this is on brings people back idle (let go of when this is turned off if it was still on as they came back, otherwise only by hand in Characters > Control), so tick Game > Saves' \"Don't write saves\" while using it. For a world that stops altogether, freeze time in the Free Camera tab.");
		ImGui::SameLine();
		const int notHeld = g_notHeldCount;
		if (!g_freezeNpcs)
			ImGui::TextDisabled("off");
		else if (notHeld > 0)
			ImGui::TextDisabled("%d held still, %d not (facehuggers can't be)", g_frozenCount.load(), notHeld);
		else
			ImGui::TextDisabled("%d held still", g_frozenCount.load());
		bool selectedOnly;
		{
			std::lock_guard<std::mutex> lock(g_switchMutex);
			selectedOnly = g_selectedOnly;
		}
		if (g_freezeNpcs && selectedOnly)
		{
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextColored(ImVec4(1.0f, 0.75f, 0.3f, 1.0f), "AI > Tree Switches is set to the selected character only: the alien and androids are held only when selected.");
			ImGui::PopTextWrapPos();
		}

		const char* invisible = InvisibleSwitch();
		bool hidden = false;
		if (invisible)
			DEBUG_MENU::GetSwitch(invisible, hidden);
		ImGui::BeginDisabled(!invisible);
		if (ImGui::Checkbox("Player invisible to the AI", &hidden) && invisible)
			DEBUG_MENU::SetSwitch(invisible, hidden);
		ImGui::EndDisabled();
		Tooltip("Characters > Player's switch (tools.invisible): the AI's senses no longer pick up the player (what Cinematic Tools' invisibility was for; it never hid the player's model).");

		ImGui::SeparatorText("Camera and character");
		// (no label: the choice shows "Character: N" itself, under this heading)
		ImGui::SetNextItemWidth(320.0f * DEBUG_MENU::Scale());
		DEBUG_MENU::CharacterPicker("##cinematictoolscharacter");
		Tooltip("The character the buttons below act on: the same one selected in the Characters group and the Free Camera tab.");
		const DEBUG_MENU::Character* selected = DEBUG_MENU::Selected();
		ImGui::BeginDisabled(!selected);
		if (ImGui::Button("Camera behind it") && selected)
		{
			CINEMATIC_CAMERA::GoBehind(selected->position, selected->facing);
			SetNote("camera behind " + selected->name);
		}
		Tooltip("Turns the free camera on 3 m behind the character, looking at it.");
		ImGui::SameLine();
		if (ImGui::Button("Lock the camera to it") && selected)
		{
			CINEMATIC_CAMERA::LockTo(selected->id);
			SetNote("camera locked to " + selected->name);
		}
		Tooltip("The free camera moves with the character, kept where it is relative to it (while it is on).");
		ImGui::EndDisabled();
		ImGui::SameLine();
		if (ImGui::Button("Unlock"))
		{
			CINEMATIC_CAMERA::LockTo(0);
			SetNote("camera unlocked");
		}
		ImGui::BeginDisabled(!CINEMATIC_CAMERA::Active());
		if (ImGui::Button("Move the player to the camera"))
			RunAsync("cinematic.player_to_camera", "");
		ImGui::EndDisabled();
		Tooltip("The Characters group's teleport (player.teleport), to where the free camera is.");
		std::lock_guard<std::mutex> lock(g_noteMutex);
		if (!g_charactersNote.empty())
		{
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextDisabled("Last: %s", g_charactersNote.c_str());
			ImGui::PopTextWrapPos();
		}
	}

	void DrawScreenshots()
	{
		bool hide = g_shotHideHud;
		if (ImGui::Checkbox("Hide the HUD for the shot", &hide))
			g_shotHideHud = hide;
		Tooltip("What the HUD tab's Hide the HUD hides (the in-game HUD, the weapon HUD and, in a challenge map, its objectives while that tab's box for them is ticked) is hidden for the few frames a shot takes, then shown again.");
		bool waiting;
		{
			std::lock_guard<std::mutex> lock(g_shotMutex);
			waiting = g_shot.state == ShotState::Waiting && (g_shot.frames > 0 || GetTickCount64() - g_shot.askedAt <= 5000);
		}
		ImGui::BeginDisabled(waiting);
		if (ImGui::Button("Take a screenshot"))
		{
			std::string path;
			if (ShotPath("", g_level, path))
				RequestShot(path, g_shotHideHud);
		}
		ImGui::EndDisabled();
		ImGui::SameLine();
		if (ScreenshotKey())
			ImGui::TextDisabled("or %s, with this window closed or open", Config::KeyName(ScreenshotKey()));
		else
			ImGui::TextDisabled("(no key: CinematicScreenshotKey is another helper's key)");
		ImGui::TextWrapped("The frame as the game drew it, without this window or anything else RuntimeUtils draws, as a .bmp in the Screenshots folder of the game folder (CinematicScreenshotKey in OpenCAGE.RuntimeUtils.ini changes the key).");
		std::lock_guard<std::mutex> lock(g_shotMutex);
		if (!g_lastShot.empty())
		{
			// A shot saved: its file name on one line (the folder is the one above), the whole path when hovered, so the
			// path is never broken across lines
			const char* const saved = "saved ";
			const size_t slash = g_lastShot.find_last_of("\\/");
			if (g_lastShot.compare(0, strlen(saved), saved) == 0 && slash != std::string::npos)
			{
				ImGui::TextDisabled("Last: saved %s in the Screenshots folder", g_lastShot.c_str() + slash + 1);
				// (not the wrapped tooltip: that would break the path)
				if (ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
					ImGui::SetTooltip("%s", g_lastShot.c_str() + strlen(saved));
			}
			else
			{
				ImGui::PushTextWrapPos(0.0f);
				ImGui::TextDisabled("Last: %s", g_lastShot.c_str());
				ImGui::PopTextWrapPos();
			}
		}
	}

	const bool g_charactersAdded = DEBUG_MENU::AddTab("Cast", 94, DrawCharacters, "Cinematic");
	const bool g_screenshotsAdded = DEBUG_MENU::AddTab("Screenshots", 95, DrawScreenshots, "Cinematic");
	const bool g_overlayAdded = DEBUG_MENU::AddOverlay(93, Overlay);
}

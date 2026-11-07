#include "CINEMATIC_KEYS.h"
#include "CINEMATIC_CAMERA.h"
#include "Config.h"
#include "DEBUG_MENU.h"
#include "LIVE_LINK.h"

#include <atomic>
#include <cstdio>
#include <cstring>
#include <mutex>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <imgui.h>

/*
	The Cinematic group's keys: what each does, its key now and its default, kept in OpenCAGE.CinematicKeys.ini next to
	AI.exe (see CINEMATIC_KEYS.h). The defaults are Cinematic Tools' keys, bar Freeze NPCs: F7 there, the Debug Menu's own
	key here, so F5 (Cinematic Tools' key for its own window, which the Debug Menu stands in for). The free camera's key and
	the screenshot key default to CinematicCameraKey and CinematicScreenshotKey in OpenCAGE.RuntimeUtils.ini (F8, F9) when
	that file names them, as before this table.

	The keys that act any time a level is played are read here, once a frame (an overlay, on the render thread): freeze
	time, hide the HUD, invisibility and freeze NPCs each flip their switch, with a note at the top of the screen for a
	moment. Not while the game's pause menu or game over screen is up, nor while a box in the Debug Menu's window is typed
	in. The free camera reads its own (CINEMATIC_CAMERA.cpp), the screenshot key is CINEMATIC_TOOLS.cpp's.

	The Keys tab sets them: click a key, press the new one. Every key the keyboard has can be read that way (GetAsyncKeyState
	over the virtual-key codes), the mouse's middle and side buttons too, but not its left and right buttons (they click
	the window) nor Escape (it closes the window, which stops the setting).
*/
namespace
{
	struct ActionInfo
	{
		const char* name;      // in the file
		const char* label;     // in the tab
		int fallback;          // the default (bar ToggleCamera and Screenshot: see Defaults)
		bool anyTime;
	};

	using namespace CINEMATIC_KEYS;
	const ActionInfo kActions[kActionCount] = {
		{ "ToggleCamera", "Free camera on/off", VK_F8, true },
		{ "FreezeTime", "Freeze time", VK_DELETE, true },
		{ "HideHud", "Hide the HUD", VK_HOME, true },
		{ "Invisibility", "Player invisible to the AI", 'I', true },
		{ "FreezeNpcs", "Freeze NPCs", VK_F5, true },
		{ "Screenshot", "Screenshot", VK_F9, true },
		{ "MoveForward", "Move forward", 'W', false },
		{ "MoveBackward", "Move back", 'S', false },
		{ "MoveLeft", "Move left", 'A', false },
		{ "MoveRight", "Move right", 'D', false },
		{ "MoveUp", "Move up", VK_SPACE, false },
		{ "MoveDown", "Move down", VK_CONTROL, false },
		{ "MoveFaster", "Move four times faster (held)", VK_SHIFT, false },
		{ "LookLeft", "Turn left", VK_LEFT, false },
		{ "LookRight", "Turn right", VK_RIGHT, false },
		{ "LookUp", "Look up", VK_UP, false },
		{ "LookDown", "Look down", VK_DOWN, false },
		{ "RollLeft", "Roll left", 'Q', false },
		{ "RollRight", "Roll right", 'E', false },
		{ "FovWider", "Field of view wider", VK_ADD, false },
		{ "FovNarrower", "Field of view narrower", VK_SUBTRACT, false },
		{ "FocusFarther", "Focus distance farther", VK_NUMPAD6, false },
		{ "FocusNearer", "Focus distance nearer", VK_NUMPAD5, false },
		{ "FocalLonger", "Focal length longer", VK_NUMPAD8, false },
		{ "FocalShorter", "Focal length shorter", VK_NUMPAD9, false },
		{ "FNumberHigher", "F-number higher", VK_NUMPAD3, false },
		{ "FNumberLower", "F-number lower", VK_NUMPAD2, false },
		{ "TrackAddNode", "Add a track node", VK_F1, false },
		{ "TrackDeleteNode", "Delete the last track node", VK_F2, false },
		{ "TrackPlay", "Play/stop the track", VK_F3, false },
		{ "StepFrame", "Step one frame (while paused)", VK_END, false },
		{ "SpeedUp", "Move speed up", VK_PRIOR, false },
		{ "SpeedDown", "Move speed down", VK_NEXT, false },
	};

	// The keys now and their defaults (read from the file once, at first use)
	std::atomic<int> g_keys[kActionCount];
	int g_defaults[kActionCount] = {};
	std::once_flag g_loaded;
	std::mutex g_fileMutex;

	std::string FilePath()
	{
		char exePath[MAX_PATH] = {};
		GetModuleFileNameA(nullptr, exePath, MAX_PATH);
		std::string path = exePath;
		const size_t slash = path.find_last_of("\\/");
		path = (slash == std::string::npos) ? "" : path.substr(0, slash + 1);
		return path + "OpenCAGE.CinematicKeys.ini";
	}

	const char* Name(int key)
	{
		return key ? Config::KeyName(key) : "NONE";
	}

	void Load()
	{
		const std::string path = FilePath();
		for (int i = 0; i < kActionCount; i++)
		{
			int fallback = kActions[i].fallback;
			if (i == ToggleCamera)
				fallback = Config::IniKey("CinematicCameraKey", VK_F8);
			else if (i == Screenshot)
				fallback = Config::IniKey("CinematicScreenshotKey", VK_F9);
			g_defaults[i] = fallback;
			char value[64] = {};
			GetPrivateProfileStringA("Keys", kActions[i].name, "", value, sizeof(value), path.c_str());
			g_keys[i] = Config::ParseKeyText(value, fallback);
		}
		for (int i = 0; i < kActionCount; i++)
			if (const char* by = CINEMATIC_CAMERA::TakenBy(g_keys[i]))
				DevTools::Log("Debug menu: Cinematic keys: %s is %s, so it does nothing for %s (set another in Cinematic > Keys)", Config::KeyName(g_keys[i]), by, kActions[i].name);
	}

	void EnsureLoaded()
	{
		std::call_once(g_loaded, Load);
	}

	// The whole file, every key in it (so it says what each is), with how to write one
	bool Save(std::string& error)
	{
		std::lock_guard<std::mutex> lock(g_fileMutex);
		std::string text =
			"; The Debug Menu's Cinematic keys (OpenCAGE RuntimeUtils), set in game in Cinematic > Keys or here: a key name\r\n"
			"; (F1..F12, A..Z, 0..9, INSERT, DELETE, HOME, END, PAGEUP, PAGEDOWN, LEFT, RIGHT, UP, DOWN, SPACE, CTRL, SHIFT, ALT,\r\n"
			"; ADD, SUBTRACT, NUMPAD0..NUMPAD9, MOUSE4, MOUSE5...), a Windows virtual-key code (0x2D), or NONE for no key.\r\n"
			"; A line left out takes its default. OpenCAGE never writes this file.\r\n"
			"[Keys]\r\n";
		for (int i = 0; i < kActionCount; i++)
			text += std::string(kActions[i].name) + "=" + Name(g_keys[i]) + "\r\n";
		const std::string path = FilePath();
		FILE* file = nullptr;
		if (fopen_s(&file, path.c_str(), "wb") != 0 || !file)
		{
			error = "could not write " + path;
			return false;
		}
		const bool written = fwrite(text.data(), 1, text.size(), file) == text.size();
		fclose(file);
		if (!written)
			error = "could not write all of " + path;
		return written;
	}

	// ---- Setting a key in the tab (render thread) ----

	std::atomic<int> g_capturing = -1;    // the action whose key is being set, -1 none
	bool g_heldAtStart[256] = {};         // keys already down when it started (not taken until let go and pressed again)
	int g_tabDrawnFrame = -100;
	std::string g_note;                   // the tab's last note

	bool GameHasFocus()
	{
		const HWND foreground = GetForegroundWindow();
		DWORD process = 0;
		if (foreground)
			GetWindowThreadProcessId(foreground, &process);
		return foreground && process == GetCurrentProcessId();
	}

	// Keys the setting never takes: the mouse's left and right buttons (they click the window), the sided Shift, Ctrl and
	// Alt (the plain ones come first and stand for either side), the Windows keys and what IMEs send
	bool Takeable(int vk)
	{
		return vk != VK_LBUTTON && vk != VK_RBUTTON && vk != VK_CANCEL && !(vk >= VK_LSHIFT && vk <= VK_RMENU) &&
			vk != VK_LWIN && vk != VK_RWIN && vk != VK_APPS && vk != VK_PROCESSKEY && vk != VK_PACKET && vk != 0xFF;
	}

	void SetKey(int action, int key)
	{
		g_keys[action] = key;
		std::string error;
		if (!Save(error))
			g_note = error;
		else
			g_note = std::string(kActions[action].label) + ": " + Name(key);
		DevTools::Log("Debug menu: Cinematic keys: %s=%s", kActions[action].name, Name(key));
	}

	void StartCapture(int action)
	{
		for (int vk = 1; vk < 256; vk++)
			g_heldAtStart[vk] = (GetAsyncKeyState(vk) & 0x8000) != 0;
		g_capturing = action;
		g_note.clear();
	}

	// Once a frame while the tab is drawn: the first key pressed since setting started (Escape closes the window instead)
	void PollCapture()
	{
		const int action = g_capturing;
		if (action < 0)
			return;
		if (!DEBUG_MENU::IsOpen() || !GameHasFocus())
		{
			g_capturing = -1;
			return;
		}
		for (int vk = 1; vk < 256; vk++)
		{
			if (!Takeable(vk))
				continue;
			const bool down = (GetAsyncKeyState(vk) & 0x8000) != 0;
			if (!down)
			{
				g_heldAtStart[vk] = false;
				continue;
			}
			if (g_heldAtStart[vk])
				continue;
			g_capturing = -1;
			if (vk == VK_ESCAPE)
				return;
			SetKey(action, vk);
			return;
		}
	}

	// ---- The keys that act any time (render thread) ----

	bool g_was[kActionCount] = {};
	std::string g_toast;
	ULONGLONG g_toastAt = 0;
	constexpr ULONGLONG kToastMs = 1800;

	// Characters > Player's "invisible to the AI" switch (as the Cast tab finds it)
	const char* InvisibleSwitch()
	{
		bool value = false;
		if (DEBUG_MENU::GetSwitch("player.invisible", value))
			return "player.invisible";
		if (DEBUG_MENU::GetSwitch("tools.invisible", value))
			return "tools.invisible";
		return nullptr;
	}

	void Flip(const char* switchName, const char* what)
	{
		bool on = false;
		if (!switchName || !DEBUG_MENU::GetSwitch(switchName, on))
			g_toast = std::string(what) + " is not in this build";
		else
		{
			DEBUG_MENU::SetSwitch(switchName, !on);
			g_toast = std::string(what) + (on ? ": off" : ": on");
		}
		g_toastAt = GetTickCount64();
	}

	void AnyTimeKeys()
	{
		bool allowed = g_capturing < 0 && !DEBUG_MENU::TypingInWindow();
		const Action acting[] = { FreezeTime, HideHud, Invisibility, FreezeNpcs };
		bool pressed[kActionCount] = {};
		bool any = false;
		for (Action action : acting)
		{
			const bool down = Down(action);
			pressed[action] = down && !g_was[action];
			g_was[action] = down;
			any |= pressed[action];
		}
		// The rest is only looked at when a key was just pressed
		if (!any || !allowed)
			return;
		allowed = GameHasFocus() && LIVE_LINK::LevelRunning() && !CINEMATIC_CAMERA::GameMenuUp();
		if (!allowed)
			return;
		if (pressed[FreezeTime])
			Flip("game.paused", "Freeze time");
		if (pressed[HideHud])
			Flip("cinematic.hide_hud", "Hide the HUD");
		if (pressed[Invisibility])
			Flip(InvisibleSwitch(), "Player invisible to the AI");
		if (pressed[FreezeNpcs])
			Flip("cinematic.freeze_npcs", "Freeze NPCs");
	}

	void Overlay(ImDrawList* drawList)
	{
		EnsureLoaded();
		// Setting a key stops when its tab is no longer drawn (another tab, the window closed)
		if (g_capturing >= 0 && ImGui::GetFrameCount() - g_tabDrawnFrame > 1)
			g_capturing = -1;
		AnyTimeKeys();

		if (g_toast.empty() || GetTickCount64() - g_toastAt > kToastMs)
			return;
		const char* text = g_toast.c_str();
		const ImVec2 size = ImGui::CalcTextSize(text);
		const float width = ImGui::GetIO().DisplaySize.x;
		const ImVec2 at((width - size.x) * 0.5f, DEBUG_MENU::BannerTop(size.y + 8.0f) + 4.0f);
		drawList->AddRectFilled(ImVec2(at.x - 8.0f, at.y - 4.0f), ImVec2(at.x + size.x + 8.0f, at.y + size.y + 4.0f), IM_COL32(0, 0, 0, 150), 4.0f);
		drawList->AddText(at, IM_COL32(240, 200, 120, 255), text);
	}

	// ---- The tab ----

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

	void Row(int action)
	{
		const int key = g_keys[action];
		ImGui::TableNextRow();
		ImGui::TableNextColumn();
		ImGui::AlignTextToFramePadding();
		ImGui::TextUnformatted(kActions[action].label);
		ImGui::TableNextColumn();
		ImGui::PushID(action);
		const float width = 110.0f * DEBUG_MENU::Scale();
		if (g_capturing == action)
		{
			// (its own id: the key that ends the setting can't then press this button as it goes)
			if (ImGui::Button("press a key...###capture", ImVec2(width, 0.0f)))
				g_capturing = -1;
			Tooltip("Press the new key (click again to leave it as it was; Escape closes the window).");
		}
		else
		{
			char label[64];
			sprintf_s(label, "%s###key", key ? Config::KeyName(key) : "none");
			if (ImGui::Button(label, ImVec2(width, 0.0f)))
				StartCapture(action);
			Tooltip("Click, then press the new key.");
		}
		ImGui::SameLine();
		ImGui::BeginDisabled(key == 0);
		if (ImGui::SmallButton("Clear"))
			SetKey(action, 0);
		ImGui::EndDisabled();
		Tooltip("No key for this.");
		if (key != g_defaults[action])
		{
			ImGui::SameLine();
			char label[64];
			sprintf_s(label, "Default (%s)", g_defaults[action] ? Config::KeyName(g_defaults[action]) : "none");
			if (ImGui::SmallButton(label))
				SetKey(action, g_defaults[action]);
		}
		ImGui::TableNextColumn();
		const ImVec4 warn(1.0f, 0.75f, 0.3f, 1.0f);
		if (const char* by = CINEMATIC_CAMERA::TakenBy(key))
			ImGui::TextColored(warn, "does nothing: %s", by);
		else
		{
			std::string also;
			for (int other = 0; other < kActionCount; other++)
				if (other != action && key != 0 && g_keys[other] == key)
					also += std::string(also.empty() ? "also " : ", ") + kActions[other].label;
			if (!also.empty())
				ImGui::TextColored(warn, "%s", also.c_str());
		}
		ImGui::PopID();
	}

	void Section(const char* title, bool anyTime)
	{
		ImGui::SeparatorText(title);
		if (!ImGui::BeginTable(anyTime ? "##keys_any" : "##keys_flying", 3, ImGuiTableFlags_SizingStretchProp | ImGuiTableFlags_RowBg))
			return;
		// The key column fits its button, Clear and the widest "Default (PAGEDOWN)" (230 cut that off)
		ImGui::TableSetupColumn("What", ImGuiTableColumnFlags_WidthFixed, 250.0f * DEBUG_MENU::Scale());
		ImGui::TableSetupColumn("Key", ImGuiTableColumnFlags_WidthFixed, 340.0f * DEBUG_MENU::Scale());
		ImGui::TableSetupColumn("Note", ImGuiTableColumnFlags_WidthStretch, 1.0f);
		for (int action = 0; action < kActionCount; action++)
			if (kActions[action].anyTime == anyTime)
				Row(action);
		ImGui::EndTable();
	}

	void DrawTab()
	{
		EnsureLoaded();
		g_tabDrawnFrame = ImGui::GetFrameCount();
		PollCapture();

		ImGui::PushTextWrapPos(0.0f);
		ImGui::TextUnformatted("Cinematic Tools' keys, as it had them, bar Freeze NPCs (F7 there, the Debug Menu's own key here). Click a key, then press the new one. They are kept in OpenCAGE.CinematicKeys.ini next to AI.exe.");
		ImGui::PopTextWrapPos();
		Section("Any time a level is played", true);
		ImGui::TextDisabled("Not while the game's pause menu is up, nor while you type in this window.");
		Section("While the free camera flies", false);
		ImGui::TextDisabled("The mouse and a gamepad fly it too (the Free Camera tab's Keys list them).");

		if (ImGui::Button("Reset every key"))
		{
			for (int i = 0; i < kActionCount; i++)
				g_keys[i] = g_defaults[i];
			std::string error;
			g_note = Save(error) ? "every key back to its default" : error;
			g_capturing = -1;
		}
		Tooltip("Cinematic Tools' keys again (the free camera's and the screenshot's: CinematicCameraKey and CinematicScreenshotKey in OpenCAGE.RuntimeUtils.ini when it names them).");
		if (!g_note.empty())
		{
			ImGui::SameLine();
			ImGui::TextDisabled("Last: %s", g_note.c_str());
		}
	}

	// ---- Names ----

	int ActionByName(const std::string& name)
	{
		for (int i = 0; i < kActionCount; i++)
			if (_stricmp(kActions[i].name, name.c_str()) == 0)
				return i;
		return -1;
	}

	void NameCommands()
	{
		DEBUG_MENU::AddCommand("cinematic.set_key", "Action=KEY: sets one of the Cinematic keys and saves it (KEY: a key name, NONE, or DEFAULT); cinematic.keys lists them", [](const std::string& argument)
		{
			EnsureLoaded();
			const size_t equals = argument.find('=');
			const int action = equals == std::string::npos ? -1 : ActionByName(argument.substr(0, equals));
			if (action < 0)
				return DEBUG_MENU::CommandResult{ false, "give Action=KEY, the action one of cinematic.keys'" };
			std::string value = argument.substr(equals + 1);
			int key;
			if (_stricmp(value.c_str(), "DEFAULT") == 0)
				key = g_defaults[action];
			else
			{
				key = Config::ParseKeyText(value, -1);
				if (key < 0)
					return DEBUG_MENU::CommandResult{ false, "no key named " + value };
			}
			// The tab's note and the file are the render thread's
			return DEBUG_MENU::AnswerOnRenderThread([action, key]()
			{
				SetKey(action, key);
				return DEBUG_MENU::CommandResult{ true, std::string(kActions[action].name) + "=" + Name(key) };
			});
		});
		DEBUG_MENU::AddCommand("cinematic.key_capture", "the action whose key the Keys tab is setting now, or none", [](const std::string&)
		{
			const int action = g_capturing;
			return DEBUG_MENU::CommandResult{ true, action < 0 ? std::string("none") : std::string(kActions[action].name) };
		});
	}

	const bool g_named = (NameCommands(), true);
	const bool g_tabAdded = DEBUG_MENU::AddTab("Keys", 96, DrawTab, "Cinematic");
	const bool g_overlayAdded = DEBUG_MENU::AddOverlay(92, Overlay);
}

int CINEMATIC_KEYS::Key(Action action)
{
	if (action < 0 || action >= kActionCount)
		return 0;
	EnsureLoaded();
	const int key = g_keys[action];
	return CINEMATIC_CAMERA::TakenKey(key) ? 0 : key;
}

bool CINEMATIC_KEYS::Down(Action action)
{
	const int key = Key(action);
	return key && (GetAsyncKeyState(key) & 0x8000) != 0;
}

std::string CINEMATIC_KEYS::KeyText(Action action)
{
	const int key = Key(action);
	return key ? std::string(Config::KeyName(key)) : std::string("no key");
}

bool CINEMATIC_KEYS::Capturing()
{
	return g_capturing >= 0;
}

std::string CINEMATIC_KEYS::Describe()
{
	EnsureLoaded();
	std::string text;
	for (int i = 0; i < kActionCount; i++)
	{
		const int key = g_keys[i];
		text += std::string(kActions[i].name) + "=" + Name(key);
		if (const char* by = CINEMATIC_CAMERA::TakenBy(key))
			text += std::string(" (does nothing: ") + by + ")";
		text += "\n";
	}
	text += "any_time=";
	for (int i = 0, first = 1; i < kActionCount; i++)
		if (kActions[i].anyTime)
		{
			text += std::string(first ? "" : ",") + kActions[i].name;
			first = 0;
		}
	text += "\nfile=" + FilePath();
	return text;
}

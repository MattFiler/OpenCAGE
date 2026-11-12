#include "DEBUG_MENU.h"
#include "RENDER_COMMON.h"
#include "Menu.h"

#include <cctype>
#include <cstdio>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <imgui.h>

/*
	Display section (Overview > Info) and display.* commands: the size of the picture the game draws (the overlay is laid out in
	its pixels), the window it is shown in, and the window mode and resolutions from the graphics settings. display.mode and
	display.resolution switch these as the game's options do, without saving them.
	Addresses and layouts are for the Steam AI.exe (GOG and Epic builds in GameAddresses.h).

	Graphics settings: one setting object per kind, in a table by kind. Each holds its choices (a name such as "Windowed" or
	"1280x720" plus values) and the current choice's name hash, and notifies listeners on change. The display listens for window
	mode, resolutions and deep colour, and rebuilds at its next frame (back buffers resized, window restyled and moved).
*/
namespace
{
	// The graphics settings table (one pointer per kind), inside the display object, which starts 0x668 before it
	constexpr uintptr_t kSettings = 0x01358A50;
	constexpr uint32_t kDisplayFromSettings = 0x668;
	// Display fields: listening for setting changes, and a rebuild pending for its next frame
	constexpr uint32_t kDisplayListening = 0x05;
	constexpr uint32_t kDisplayRemakePending = 0x88;

	// Setting kinds used here (index in the table)
	constexpr int kDeepColour = 2;              // on: full screen resolution comes from the 30-bit colour list
	constexpr int kFullScreenResolution24 = 3;
	constexpr int kFullScreenResolution30 = 4;
	constexpr int kWindowedResolution = 5;
	constexpr int kWindowMode = 6;              // 0 "Windowed", 1 "WindowedFullScreen" (borderless), 2 "FullScreen"

	// Setting: choice array pointer and count, then the current choice's name hash. Each choice is kChoiceSize bytes: name hash,
	// name, int value and bool value
	constexpr uint32_t kSettingChoices = 0x10;
	constexpr uint32_t kSettingChoiceCount = 0x14;
	constexpr uint32_t kSettingCurrent = 0x1C;
	constexpr uint32_t kChoiceSize = 0x70;
	constexpr uint32_t kChoiceName = 0x04;
	constexpr uint32_t kChoiceNameSize = 0x40;
	constexpr uint32_t kChoiceNumber = 0x50;
	constexpr uint32_t kChoiceYesNo = 0x54;

	// Sets the current choice by name and notifies listeners, as the game's options do
	constexpr uintptr_t kSetChoiceByName = 0x005A84C0;
	typedef void(__thiscall* t_SetChoiceByName)(void* setting, const char* name);

	struct Choice
	{
		std::string name;
		int number = 0;
		bool yes = false;
	};
	struct Setting
	{
		uintptr_t at = 0;
		std::vector<Choice> choices;
		int current = -1;               // -1: none of them (or unreadable)
		const Choice* Current() const { return current >= 0 ? &choices[current] : nullptr; }
	};

	uintptr_t SettingsTable()
	{
		return RENDER_COMMON::Game(kSettings);
	}

	// Reads a setting and its choices guarded (no choices when unreadable)
	Setting ReadSetting(int kind)
	{
		Setting setting;
		const uintptr_t table = SettingsTable();
		setting.at = table ? RENDER_COMMON::Pointer(table + kind * 4) : 0;
		uintptr_t list = 0;
		uint32_t count = 0, current = 0;
		if (!setting.at || !RENDER_COMMON::Read(setting.at + kSettingChoices, list) || !RENDER_COMMON::Read(setting.at + kSettingChoiceCount, count) ||
			!RENDER_COMMON::Read(setting.at + kSettingCurrent, current) || !list || count > 1024)
			return setting;
		for (uint32_t i = 0; i < count; i++)
		{
			const uintptr_t at = list + i * kChoiceSize;
			uint32_t hash = 0;
			int number = 0;
			uint8_t yes = 0;
			if (!RENDER_COMMON::Read(at, hash) || !RENDER_COMMON::Read(at + kChoiceNumber, number) || !RENDER_COMMON::Read(at + kChoiceYesNo, yes))
				break;
			Choice choice;
			choice.name = RENDER_COMMON::Text(at + kChoiceName, kChoiceNameSize);
			choice.number = number;
			choice.yes = yes != 0;
			if (hash == current)
				setting.current = static_cast<int>(setting.choices.size());
			setting.choices.push_back(choice);
		}
		return setting;
	}

	std::string CurrentName(const Setting& setting)
	{
		const Choice* current = setting.Current();
		return current ? current->name : std::string("(unknown)");
	}

	// The resolution list the current window mode uses: windowed has its own; borderless and full screen use the full screen
	// list (the 30-bit one while deep colour is on)
	int ResolutionKind()
	{
		const Setting mode = ReadSetting(kWindowMode);
		if (mode.Current() && mode.Current()->number == 0)
			return kWindowedResolution;
		const Setting deep = ReadSetting(kDeepColour);
		return deep.Current() && deep.Current()->yes ? kFullScreenResolution30 : kFullScreenResolution24;
	}

	std::string ModeLabel(const std::string& name)
	{
		if (name == "Windowed")
			return "windowed";
		if (name == "WindowedFullScreen")
			return "borderless";
		if (name == "FullScreen")
			return "full screen";
		return name;
	}

	std::string Lower(std::string text)
	{
		for (char& c : text)
			c = static_cast<char>(std::tolower(static_cast<unsigned char>(c)));
		return text;
	}

	std::string ChoiceList(const Setting& setting)
	{
		std::string list;
		for (const Choice& choice : setting.choices)
			list += (list.empty() ? "" : ",") + choice.name;
		return list;
	}

	// Sets a setting to the named choice (case-insensitive) on the entity thread between frames, as the game's options do, and
	// waits for it. The display rebuilds at its next frame.
	DEBUG_MENU::CommandResult Choose(int kind, const std::string& wanted, const char* what)
	{
		const auto setChoice = reinterpret_cast<t_SetChoiceByName>(RENDER_COMMON::Game(kSetChoiceByName));
		const Setting setting = ReadSetting(kind);
		if (!setChoice || !setting.at || setting.choices.empty())
			return { false, std::string("the game's ") + what + " setting could not be read (not available on this game release, or not set up yet)" };
		std::string name;
		for (const Choice& choice : setting.choices)
			if (Lower(choice.name) == Lower(wanted))
				name = choice.name;
		if (name.empty())
			return { false, std::string("no ") + what + " \"" + wanted + "\"; the choices: " + ChoiceList(setting) };
		const std::string was = CurrentName(setting);
		const uintptr_t at = setting.at;
		const uintptr_t display = SettingsTable() - kDisplayFromSettings;
		return DEBUG_MENU::AnswerOnEntityThread([setChoice, at, name, was, what, display]() -> DEBUG_MENU::CommandResult
		{
			setChoice(reinterpret_cast<void*>(at), name.c_str());
			// The display only rebuilds while listening for setting changes (it is in play); otherwise mark it pending here,
			// as its listener would
			uint8_t listening = 0, pending = 0;
			RENDER_COMMON::Read(display + kDisplayListening, listening);
			RENDER_COMMON::Read(display + kDisplayRemakePending, pending);
			if (!pending)
				RENDER_COMMON::Write(display + kDisplayRemakePending, static_cast<uint8_t>(1));
			DevTools::Log("Display: %s set to %s (was %s); the game makes its picture again at its next frame%s", what, name.c_str(), was.c_str(), listening ? "" : " (marked here: the display was not taking notice)");
			return { true, std::string(what) + "=" + name + "\nwas=" + was + "\nnot saved: the game starts with its own settings next time" };
		}, 4000);
	}

	// Runs on the render thread, where the overlay's sizes are read
	std::string Status()
	{
		ImVec2 picture, window;
		Menu::PictureAndWindowSize(picture, window);
		const ImVec2 display = ImGui::GetIO().DisplaySize;
		const Setting mode = ReadSetting(kWindowMode);
		const Setting fullScreen = ReadSetting(ResolutionKind() == kFullScreenResolution30 ? kFullScreenResolution30 : kFullScreenResolution24);
		const Setting windowed = ReadSetting(kWindowedResolution);
		const uintptr_t table = SettingsTable();
		uint8_t listening = 0, pending = 0;
		if (table)
		{
			RENDER_COMMON::Read(table - kDisplayFromSettings + kDisplayListening, listening);
			RENDER_COMMON::Read(table - kDisplayFromSettings + kDisplayRemakePending, pending);
		}
		BOOL exclusive = FALSE;
		DXGI_SWAP_CHAIN_DESC desc = {};
		IDXGISwapChain* swapChain = Menu::GetSwapChain();
		if (swapChain)
		{
			swapChain->GetFullscreenState(&exclusive, nullptr);
			swapChain->GetDesc(&desc);
		}
		const LONG style = desc.OutputWindow ? GetWindowLongA(desc.OutputWindow, GWL_STYLE) : 0;
		// Overlay mouse position (overlay pixels), whether it is over an overlay window, and the debug menu's last drawn rect
		// (0s while closed)
		const ImGuiIO& io = ImGui::GetIO();
		ImVec2 menuMin(0.0f, 0.0f), menuMax(0.0f, 0.0f);
		DEBUG_MENU::WindowRect(menuMin, menuMax);
		char text[900];
		snprintf(text, sizeof(text),
			"picture=%.0fx%.0f\nwindow=%.0fx%.0f\noverlay=%.0fx%.0f\nwindow_to_picture=%.3f,%.3f\nmode=%s\nmode_label=%s\nexclusive_full_screen=%d\nwindow_border=%s\n"
			"full_screen_resolution=%s\nwindowed_resolution=%s\ndisplay_listening=%d\ndisplay_remake_pending=%d\nmouse=%.0f,%.0f\nmouse_over_overlay=%d\n"
			"menu_window=%.0f,%.0f,%.0f,%.0f\n",
			picture.x, picture.y, window.x, window.y, display.x, display.y, window.x > 0.0f ? picture.x / window.x : 0.0f, window.y > 0.0f ? picture.y / window.y : 0.0f,
			CurrentName(mode).c_str(), ModeLabel(CurrentName(mode)).c_str(), exclusive ? 1 : 0, (style & WS_CAPTION) == WS_CAPTION ? "bordered" : "none",
			CurrentName(fullScreen).c_str(), CurrentName(windowed).c_str(), listening, pending, io.MousePos.x, io.MousePos.y, io.WantCaptureMouse ? 1 : 0,
			menuMin.x, menuMin.y, menuMax.x, menuMax.y);
		return text;
	}

	const bool g_commandsAdded = []()
	{
		DEBUG_MENU::AddCommand("display.status", "the picture the game draws (the overlay is laid out in it) and the window it is shown in, their sizes, the window mode and the resolutions chosen in the game's graphics settings", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]() { return DEBUG_MENU::CommandResult{ true, Status() }; });
		});
		DEBUG_MENU::AddCommand("display.choices", "the choices the game's graphics settings offer: window modes, full screen resolutions (24- and 30-bit colour) and windowed resolutions", [](const std::string&)
		{
			return DEBUG_MENU::CommandResult{ true, "modes=" + ChoiceList(ReadSetting(kWindowMode)) + "\nfull_screen_resolutions=" + ChoiceList(ReadSetting(kFullScreenResolution24)) +
				"\nfull_screen_resolutions_30bit=" + ChoiceList(ReadSetting(kFullScreenResolution30)) + "\nwindowed_resolutions=" + ChoiceList(ReadSetting(kWindowedResolution)) };
		});
		DEBUG_MENU::AddCommand("display.mode", "argument windowed, borderless or fullscreen (or the game's own names: Windowed, WindowedFullScreen, FullScreen): switches the game's window mode as its options do, without saving it - for testing the overlay in each mode", [](const std::string& argument)
		{
			const std::string wanted = Lower(argument);
			const std::string name = wanted == "windowed" ? "Windowed" : wanted == "borderless" ? "WindowedFullScreen" : wanted == "fullscreen" || wanted == "full screen" ? "FullScreen" : argument;
			return Choose(kWindowMode, name, "window mode");
		});
		DEBUG_MENU::AddCommand("display.resolution", "argument WIDTHxHEIGHT (as display.choices lists them): the resolution of the current window mode (windowed, or borderless and full screen), set as the game's options do without saving it - for testing the overlay at each resolution", [](const std::string& argument)
		{
			std::string wanted;
			for (char c : argument)
				if (!std::isspace(static_cast<unsigned char>(c)))
					wanted += c == 'X' ? 'x' : c;
			const int kind = ResolutionKind();
			return Choose(kind, wanted, kind == kWindowedResolution ? "windowed resolution" : "full screen resolution");
		});
		return true;
	}();

	// Overview > Info: the sizes and the mode, read-only
	void DrawSection()
	{
		ImGui::SeparatorText("Display");
		ImVec2 picture, window;
		Menu::PictureAndWindowSize(picture, window);
		const Setting mode = ReadSetting(kWindowMode);
		const Setting resolution = ReadSetting(ResolutionKind());
		ImGui::Text("Picture %.0f x %.0f, shown in a window %.0f x %.0f inside", picture.x, picture.y, window.x, window.y);
		ImGui::Text("Window mode: %s, resolution %s", ModeLabel(CurrentName(mode)).c_str(), CurrentName(resolution).c_str());
		RENDER_COMMON::Note("The game draws its picture at the resolution chosen in its graphics settings; this window and the rest of the overlay are drawn into "
			"it, so they are stretched with it to fit the game's window (in borderless, the window covers the monitor whatever the resolution).");
	}

	const bool g_sectionAdded = DEBUG_MENU::AddTab("Info", 6, DrawSection, "Overview");
}

#include "RENDER_COMMON.h"
#include "Config.h"
#include "DEBUG_MENU.h"
#include "Menu.h"

#include <atomic>
#include <cstdio>
#include <cstdlib>
#include <deque>
#include <mutex>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <d3d11.h>
#include <d3d11sdklayers.h>

#include <imgui.h>

/*
	The debug menu's Rendering > Developer tab: the tools that need the game started a certain way, and the
	experimental ones.

	Direct3D debug layer. With D3DDebugLayer=1 in OpenCAGE.RuntimeUtils.ini, the game's graphics device is made with the
	Direct3D 11 debug layer, which checks every call the game (and everything drawn into it) makes and reports what is wrong:
	input layouts that don't match a model's vertices, formats a shader can't read, resources bound twice... - the messages
	broken imported models and shaders cause. The game makes its device once, as it starts, with no flags; as the hooks
	attach (long before that) the flags its call passes are changed to ask for the debug layer, and put back as the game
	closes. The layer is only asked for when its library is installed (Windows' optional "Graphics Tools" feature): without
	it the device could not be made at all. It makes the game much slower. Its messages are read from the device's message
	queue at each frame's presentation (the informational ones are left out as they are stored) and listed here.

	Shader hot reload. The game can still reload its shaders when a shader pack changes on disk (see the Rendering tab's
	render.shader_hot_reload, mirrored here with its warning).

	16 shadow-casting lights. With ExtraShadowLights=1 the game starts with its capture mode's shadow budget (see
	the Lights tab).

	Names: commands "render.d3d.status", "render.d3d.messages" (the last N, default 50), "render.d3d.clear".

	Addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).
*/
namespace
{
	using namespace RENDER_COMMON;

	// The push of the flags the game's device creation passes (push 0): its value byte is made the debug flag
	constexpr uintptr_t kDeviceFlagsPush = 0x006271B9;
	constexpr uint8_t kPushOpcode = 0x6A;
	constexpr uint8_t kNoFlags = 0x00;
	constexpr uint8_t kDebugFlag = static_cast<uint8_t>(D3D11_CREATE_DEVICE_DEBUG);

	// The shadow manager's most shadow-casting lights a frame
	constexpr uintptr_t kShadowManager = 0x012AEDB0;
	constexpr uint32_t kShadowMaxPerFrame = 0x8;

	constexpr size_t kMaxMessages = 1000;

	bool g_requested = false;        // D3DDebugLayer=1
	bool g_layersInstalled = false;
	bool g_patched = false;          // the flags push changed
	ID3D11InfoQueue* g_queue = nullptr; // render thread
	bool g_queueTried = false;
	std::mutex g_messagesMutex;
	std::deque<std::string> g_messages;
	uint32_t g_counts[5] = {};       // by severity: corruption, error, warning, info, message
	bool g_registered = false;
	bool g_showWarnings = true;
	char g_filter[64] = {};

	bool LayersInstalled()
	{
		wchar_t folder[MAX_PATH];
		const UINT length = GetSystemDirectoryW(folder, MAX_PATH);
		if (!length || length >= MAX_PATH - 32)
			return false;
		const std::wstring path = std::wstring(folder) + L"\\d3d11_1sdklayers.dll";
		return GetFileAttributesW(path.c_str()) != INVALID_FILE_ATTRIBUTES;
	}

	bool WriteCode(uintptr_t at, uint8_t value)
	{
		DWORD oldProtect;
		if (!VirtualProtect(reinterpret_cast<void*>(at), 1, PAGE_EXECUTE_READWRITE, &oldProtect))
			return false;
		const bool ok = Write(at, value);
		VirtualProtect(reinterpret_cast<void*>(at), 1, oldProtect, &oldProtect);
		FlushInstructionCache(GetCurrentProcess(), reinterpret_cast<void*>(at), 1);
		return ok;
	}

	// As the hooks attach (before the game makes its device): the debug layer asked for when wanted and installed
	void PatchFlags(bool attach)
	{
		const uintptr_t push = Game(kDeviceFlagsPush);
		if (attach)
		{
			g_requested = Config::IniInt("D3DDebugLayer", 0) != 0;
			if (!g_requested)
				return;
			g_layersInstalled = LayersInstalled();
			uint8_t code[2] = {};
			if (!push || !Read(reinterpret_cast<const void*>(push), code, sizeof(code)) || code[0] != kPushOpcode || code[1] != kNoFlags)
			{
				DevTools::Log("Debug menu: D3DDebugLayer=1 is not available on this game release.");
				return;
			}
			if (!g_layersInstalled)
			{
				DevTools::Log("Debug menu: D3DDebugLayer=1 asks for the Direct3D debug layer, but it isn't installed (Windows' optional feature \"Graphics Tools\"); the game starts without it.");
				return;
			}
			g_patched = WriteCode(push + 1, kDebugFlag);
			DevTools::Log(g_patched ? "Debug menu: the game's graphics device will be made with the Direct3D debug layer (D3DDebugLayer=1)." : "Debug menu: could not ask for the Direct3D debug layer.");
			return;
		}
		if (g_patched)
		{
			uint8_t now = 0;
			if (Read(push + 1, now) && now == kDebugFlag)
				WriteCode(push + 1, kNoFlags);
			g_patched = false;
		}
	}

	const char* SeverityName(D3D11_MESSAGE_SEVERITY severity)
	{
		switch (severity)
		{
		case D3D11_MESSAGE_SEVERITY_CORRUPTION: return "CORRUPTION";
		case D3D11_MESSAGE_SEVERITY_ERROR: return "ERROR";
		case D3D11_MESSAGE_SEVERITY_WARNING: return "WARNING";
		case D3D11_MESSAGE_SEVERITY_INFO: return "INFO";
		default: return "MESSAGE";
		}
	}

	// Render thread: the device's message queue, once (only there with the debug layer on)
	void OpenQueue()
	{
		if (g_queueTried)
			return;
		IDXGISwapChain* swapChain = Menu::GetSwapChain();
		if (!swapChain)
			return;
		g_queueTried = true;
		ID3D11Device* device = nullptr;
		if (FAILED(swapChain->GetDevice(__uuidof(ID3D11Device), reinterpret_cast<void**>(&device))) || !device)
			return;
		if (SUCCEEDED(device->QueryInterface(__uuidof(ID3D11InfoQueue), reinterpret_cast<void**>(&g_queue))) && g_queue)
		{
			// The informational messages are left out as they are stored, and the queue kept short
			D3D11_MESSAGE_SEVERITY deny[] = { D3D11_MESSAGE_SEVERITY_INFO, D3D11_MESSAGE_SEVERITY_MESSAGE };
			D3D11_INFO_QUEUE_FILTER filter = {};
			filter.DenyList.NumSeverities = 2;
			filter.DenyList.pSeverityList = deny;
			g_queue->PushStorageFilter(&filter);
			g_queue->SetMessageCountLimit(4096);
			DevTools::Log("Debug menu: the Direct3D debug layer is on; its messages are in Rendering > Developer.");
		}
		device->Release();
	}

	// Render thread, once a frame: the messages stored since the last frame
	void DrainQueue()
	{
		if (!g_patched)
			return;
		OpenQueue();
		if (!g_queue)
			return;
		const UINT64 count = g_queue->GetNumStoredMessages();
		std::vector<std::string> lines;
		for (UINT64 i = 0; i < count && i < 256; i++)
		{
			SIZE_T size = 0;
			if (FAILED(g_queue->GetMessage(i, nullptr, &size)) || size == 0 || size > 65536)
				continue;
			std::vector<uint8_t> buffer(size);
			D3D11_MESSAGE* message = reinterpret_cast<D3D11_MESSAGE*>(buffer.data());
			if (FAILED(g_queue->GetMessage(i, message, &size)))
				continue;
			if (message->Severity >= 0 && message->Severity < 5)
				g_counts[message->Severity]++;
			char head[48];
			snprintf(head, sizeof(head), "%s #%d: ", SeverityName(message->Severity), static_cast<int>(message->ID));
			lines.push_back(head + std::string(message->pDescription ? message->pDescription : "", message->DescriptionByteLength ? message->DescriptionByteLength - 1 : 0));
		}
		g_queue->ClearStoredMessages();
		if (lines.empty())
			return;
		std::lock_guard<std::mutex> lock(g_messagesMutex);
		for (std::string& line : lines)
		{
			// The same message frame after frame is kept once, with how often it came
			if (!g_messages.empty() && g_messages.back().rfind(line, 0) == 0)
			{
				std::string& last = g_messages.back();
				const size_t times = last.rfind(" (x");
				int n = times != std::string::npos && times > line.size() - 1 ? atoi(last.c_str() + times + 3) : 1;
				last = line + " (x" + std::to_string(n + 1) + ")";
				continue;
			}
			g_messages.push_back(std::move(line));
			if (g_messages.size() > kMaxMessages)
				g_messages.pop_front();
		}
	}

	std::string StatusText()
	{
		char text[320];
		uint32_t maxShadows = 0;
		Read(Game(kShadowManager) + kShadowMaxPerFrame, maxShadows);
		snprintf(text, sizeof(text), "d3d_debug_requested=%d\nd3d_layers_installed=%d\nd3d_debug_asked=%d\nd3d_debug_on=%d\ncorruption=%u\nerrors=%u\nwarnings=%u\nshadow_lights_per_frame=%u\nextra_shadow_lights_ini=%d",
			g_requested, g_layersInstalled, g_patched, g_queue != nullptr, g_counts[0], g_counts[1], g_counts[2], maxShadows, Config::IniInt("ExtraShadowLights", 0) != 0);
		bool reload = false;
		std::string out = text;
		if (DEBUG_MENU::GetSwitch("render.shader_hot_reload", reload))
			out += std::string("\nshader_hot_reload=") + (reload ? "1" : "0");
		return out;
	}

	void Register()
	{
		if (g_registered)
			return;
		g_registered = true;
		DEBUG_MENU::AddCommand("render.d3d.status", "the Direct3D debug layer (asked for, installed, on), its message counts, the shadow budget and shader hot reload", [](const std::string&) -> DEBUG_MENU::CommandResult
		{
			return DEBUG_MENU::AnswerOnRenderThread([]() -> DEBUG_MENU::CommandResult { return { true, StatusText() }; });
		});
		DEBUG_MENU::AddCommand("render.d3d.messages", "the Direct3D debug layer's last messages (argument: how many, 50 if none), oldest first", [](const std::string& argument) -> DEBUG_MENU::CommandResult
		{
			int wanted = atoi(argument.c_str());
			if (wanted <= 0)
				wanted = 50;
			std::lock_guard<std::mutex> lock(g_messagesMutex);
			std::string text;
			const size_t from = g_messages.size() > static_cast<size_t>(wanted) ? g_messages.size() - wanted : 0;
			for (size_t i = from; i < g_messages.size(); i++)
				text += g_messages[i] + "\n";
			return { true, text.empty() ? std::string(g_patched ? "(no messages)" : "(the debug layer is off: D3DDebugLayer=1 in OpenCAGE.RuntimeUtils.ini, then start the game)") : text };
		});
		DEBUG_MENU::AddCommand("render.d3d.clear", "clears the Direct3D debug layer's message list", [](const std::string&) -> DEBUG_MENU::CommandResult
		{
			std::lock_guard<std::mutex> lock(g_messagesMutex);
			g_messages.clear();
			return { true, "Cleared" };
		});
	}

	void Overlay(ImDrawList* /*drawList*/)
	{
		DrainQueue();
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

	void DrawTab()
	{
		Register();
		const ImVec4 warning(1.0f, 0.6f, 0.3f, 1.0f);

		ImGui::SeparatorText("Direct3D debug layer");
		ImGui::TextWrapped("The Direct3D 11 debug layer checks every call the game makes to the graphics card and reports what is wrong - the messages broken imported models and shaders cause. It makes the game much slower.");
		if (!g_requested)
			Note("Off. Set D3DDebugLayer=1 in OpenCAGE.RuntimeUtils.ini (next to AI.exe) and start the game again.");
		else if (!g_layersInstalled)
			ColouredNote(warning, "Asked for, but not installed: add Windows' optional feature \"Graphics Tools\", then start the game again.");
		else if (!g_patched)
			ColouredNote(warning, "Asked for, but not available on this game release.");
		else if (!g_queue)
			ColouredNote(warning, "Asked for, but the game's device has no message queue (the layer didn't load?).");
		else
		{
			ImGui::Text("On: %u corruption, %u errors, %u warnings so far", g_counts[0], g_counts[1], g_counts[2]);
			ImGui::Checkbox("Warnings too##d3d", &g_showWarnings);
			ImGui::SameLine();
			ImGui::SetNextItemWidth(ImGui::GetFontSize() * 12.0f);
			ImGui::InputText("Filter##d3d", g_filter, sizeof(g_filter));
			ImGui::SameLine();
			if (ImGui::Button("Clear##d3d"))
			{
				std::lock_guard<std::mutex> lock(g_messagesMutex);
				g_messages.clear();
			}
			ImGui::SameLine();
			if (ImGui::Button("Copy##d3d"))
			{
				std::lock_guard<std::mutex> lock(g_messagesMutex);
				std::string text;
				for (const std::string& line : g_messages)
					text += line + "\n";
				ImGui::SetClipboardText(text.c_str());
			}
			if (ImGui::BeginChild("messages##d3d", ImVec2(0.0f, ImGui::GetTextLineHeightWithSpacing() * 14.0f), ImGuiChildFlags_Borders, ImGuiWindowFlags_HorizontalScrollbar))
			{
				std::lock_guard<std::mutex> lock(g_messagesMutex);
				for (const std::string& line : g_messages)
				{
					const bool isWarning = line.rfind("WARNING", 0) == 0;
					if (isWarning && !g_showWarnings)
						continue;
					if (g_filter[0] && line.find(g_filter) == std::string::npos)
						continue;
					if (isWarning)
						ImGui::TextUnformatted(line.c_str());
					else
						ImGui::TextColored(ImVec4(1.0f, 0.5f, 0.45f, 1.0f), "%s", line.c_str());
				}
				if (ImGui::GetScrollY() >= ImGui::GetScrollMaxY())
					ImGui::SetScrollHereY(1.0f);
			}
			ImGui::EndChild();
		}

		ImGui::SeparatorText("Shader hot reload (experimental)");
		ColouredNote(warning, "The game never turns this on. A reload patches every material mid-game and can stall or crash it: pause the game and save first. New materials or permutations that aren't in the loaded tables won't appear.");
		ImGui::TextWrapped("With it on, every 120 frames the game checks whether a shader pack it loaded has changed on disk, and if one has, reloads every shader (materials, lights, radiosity, lens flares).");
		bool reload = false;
		const bool exists = DEBUG_MENU::GetSwitch("render.shader_hot_reload", reload);
		ImGui::BeginDisabled(!exists);
		if (ImGui::Checkbox("Shader hot reload##developer", &reload))
			DEBUG_MENU::SetSwitch("render.shader_hot_reload", reload);
		ImGui::EndDisabled();
		Tooltip("render.shader_hot_reload (also in the Rendering tab, Developer group).");

		ImGui::SeparatorText("Shadow budget");
		uint32_t maxShadows = 0;
		Read(Game(kShadowManager) + kShadowMaxPerFrame, maxShadows);
		ImGui::Text("%u shadow-casting lights a frame%s", maxShadows, maxShadows > 4 ? " (the game's capture mode)" : "");
		Note("ExtraShadowLights=1 in OpenCAGE.RuntimeUtils.ini raises it to 16 at the next start (up to four times the shadow work).");
	}

	void AttachHooks(bool attach)
	{
		// (The message queue is not let go of here: the game is closing, and its device may be gone already)
		PatchFlags(attach);
	}

	const bool g_tabAdded = DEBUG_MENU::AddTab("Developer", 77, DrawTab, "Rendering");
	const bool g_overlayAdded = DEBUG_MENU::AddOverlay(43, Overlay);
	const bool g_hooksAdded = DEBUG_MENU::AddHooks(AttachHooks);
	const bool g_namesAdded = (Register(), true);
}

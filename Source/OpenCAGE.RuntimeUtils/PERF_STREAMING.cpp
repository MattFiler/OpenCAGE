#include "PERF_STATS.h"
#include "DEBUG_MENU.h"
#include "DEBUG_MARKER.h"
#include "ZONE_LOADER.h"

#include <algorithm>
#include <cfloat>
#include <cstdarg>
#include <cstdio>
#include <cstring>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <imgui.h>

/*
	The Streaming tab (see PERF_STATS.h): texture and model streaming.

	A texture the game streams is held in two parts: a low-resolution copy always in memory, and the full-resolution mips,
	streamed into a heap of their own (a fixed budget) when a zone that uses them becomes active, and let go again when
	none does. A texture is drawn at full resolution once its streamed copy is in and current; until then it is drawn
	blurry. Models stream the same way into their own heap.

	Shown: each streaming heap (used, size, the most it has held), the requests and releases waiting to be handled,
	whether streaming is busy and its total time; the level's textures in at full resolution against those that are not;
	and the textures the main view draws this frame that are not in at full resolution yet, with their names - the
	blurry surfaces. All read only while the tab is shown (or a command asks), at most once a second.

	Addresses and layouts are the Steam retail AI.exe's (see GameAddresses.h for the GOG and Epic Games Store builds).
*/
namespace
{
	// The texture and model streaming: busy, the requests and releases waiting (each a list: its count at +4), the heap
	// they stream into (a pointer), the total time spent streaming (microseconds), and for textures a budget set in place
	// of the heap's size (0: none)
	constexpr uintptr_t kTextureStreaming = 0x015D5D10;
	constexpr uintptr_t kModelStreaming = 0x015D3C88;
	constexpr uint32_t kStreamingBusy = 0x18;
	constexpr uint32_t kStreamingInitialised = 0x19;
	constexpr uint32_t kWaitingRequests = 0x4C;
	constexpr uint32_t kWaitingReleases = 0x58;
	constexpr uint32_t kStreamingHeap = 0x60;
	constexpr uint32_t kStreamingTime = 0x2068;
	constexpr uint32_t kTextureBudget = 0x2090;
	// A heap: its size, its name, the bytes allocated now and the most ever
	constexpr uint32_t kHeapSize = 0x04;
	constexpr uint32_t kHeapName = 0x1D;
	constexpr uint32_t kHeapAllocated = 0x74;
	constexpr uint32_t kHeapHighest = 0x78;

	// The level's textures (in the engine's material database): an array of them and how many
	constexpr uint32_t kEngineTextures = 0x1804 + 0x08;
	constexpr uint32_t kEngineTextureCount = 0x1804 + 0x0C;
	constexpr uint32_t kTextureSize = 0x74;
	// A texture: its low-resolution size, its full-resolution size, the GPU textures of its streamed and current copies
	// (the same once streamed in), and its file's header: the bytes streamed, and its name
	constexpr uint32_t kTextureWidth = 0x0E;
	constexpr uint32_t kTextureHeight = 0x10;
	constexpr uint32_t kTextureFullWidth = 0x2E;
	constexpr uint32_t kTextureFullHeight = 0x30;
	constexpr uint32_t kTextureStreamed = 0x3C;
	constexpr uint32_t kTextureCurrent = 0x5C;
	constexpr uint32_t kTextureHeader = 0x68;
	constexpr uint32_t kHeaderStreamedBytes = 0x08;
	constexpr uint32_t kHeaderName = 0x28;

	// The main view (from ZONE_LOADER's view manager), its list of what to draw (how many, the items: 0x18 bytes each, the
	// material at +8), and a material's textures (12, 8 bytes each) and how many it uses
	constexpr uint32_t kMainView = 0x8C;
	constexpr uint32_t kViewListCount = 0xEC;
	constexpr uint32_t kViewListItems = 0xF0;
	constexpr uint32_t kItemSize = 0x18;
	constexpr uint32_t kItemMaterial = 0x08;
	constexpr uint32_t kMaterialTextures = 0x20;
	constexpr uint32_t kMaterialTextureCount = 0xB5;
	constexpr int kMaxMaterialTextures = 12;

	struct Provider
	{
		bool have = false;
		bool initialised = false, busy = false;
		uint32_t requests = 0, releases = 0;
		bool haveHeap = false;
		char heapName[65] = {};
		uint32_t heapSize = 0, heapUsed = 0, heapHighest = 0;
		uint64_t timeUs = 0;
		uint32_t budget = 0;
	};

	struct TextureInfo
	{
		const uint8_t* texture;
		bool streamable, streamedIn;
		uint32_t bytes;
		uint16_t width, height, fullWidth, fullHeight;
		char name[128];
	};

	struct Summary
	{
		bool have = false;
		uint32_t textures = 0, streamable = 0, streamedIn = 0;
		uint64_t bytesIn = 0, bytesAll = 0;
	};

	struct Drawn
	{
		bool have = false;
		uint32_t textures = 0, streamable = 0, streamedIn = 0;
		std::vector<TextureInfo> waiting;   // streamable, drawn, not in at full resolution: largest first
	};

	Provider g_textures, g_models;
	Summary g_summary;
	Drawn g_drawn;
	ULONGLONG g_read = 0;

	// ---- Reading ----

	bool ReadProvider(uintptr_t at, Provider& out, bool textures)
	{
		out = Provider();
		if (!at)
			return false;
		__try
		{
			const uint8_t* provider = reinterpret_cast<const uint8_t*>(at);
			out.initialised = provider[kStreamingInitialised] != 0;
			out.busy = provider[kStreamingBusy] != 0;
			out.requests = *reinterpret_cast<const uint32_t*>(provider + kWaitingRequests);
			out.releases = *reinterpret_cast<const uint32_t*>(provider + kWaitingReleases);
			out.timeUs = *reinterpret_cast<const uint64_t*>(provider + kStreamingTime);
			out.budget = textures ? *reinterpret_cast<const uint32_t*>(provider + kTextureBudget) : 0;
			const uint8_t* heap = *reinterpret_cast<const uint8_t* const*>(provider + kStreamingHeap);
			if (heap)
			{
				memcpy(out.heapName, heap + kHeapName, 64);
				out.heapName[64] = '\0';
				out.heapSize = *reinterpret_cast<const uint32_t*>(heap + kHeapSize);
				out.heapUsed = *reinterpret_cast<const uint32_t*>(heap + kHeapAllocated);
				out.heapHighest = *reinterpret_cast<const uint32_t*>(heap + kHeapHighest);
				out.haveHeap = true;
			}
			out.have = true;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			out.have = false;
			return false;
		}
	}

	bool ReadTexture(const uint8_t* texture, TextureInfo& out, bool withName)
	{
		memset(&out, 0, sizeof(out));
		out.texture = texture;
		__try
		{
			const uint8_t* header = *reinterpret_cast<const uint8_t* const*>(texture + kTextureHeader);
			out.bytes = header ? *reinterpret_cast<const uint32_t*>(header + kHeaderStreamedBytes) : 0;
			out.width = *reinterpret_cast<const uint16_t*>(texture + kTextureWidth);
			out.height = *reinterpret_cast<const uint16_t*>(texture + kTextureHeight);
			out.fullWidth = *reinterpret_cast<const uint16_t*>(texture + kTextureFullWidth);
			out.fullHeight = *reinterpret_cast<const uint16_t*>(texture + kTextureFullHeight);
			out.streamable = header && out.bytes > 0 && out.fullWidth > 0;
			const uint16_t streamed = *reinterpret_cast<const uint16_t*>(texture + kTextureStreamed);
			const uint16_t current = *reinterpret_cast<const uint16_t*>(texture + kTextureCurrent);
			out.streamedIn = streamed != 0 && streamed == current;
			if (withName && header)
			{
				const char* name = *reinterpret_cast<const char* const*>(header + kHeaderName);
				size_t length = 0;
				if (name)
					while (length < sizeof(out.name) - 1 && name[length])
					{
						out.name[length] = name[length];
						length++;
					}
				out.name[length] = '\0';
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool TextureArray(const uint8_t*& textures, uint32_t& count)
	{
		__try
		{
			if (!DEBUG_MARKER::engine)
				return false;
			textures = *reinterpret_cast<const uint8_t* const*>(DEBUG_MARKER::engine + kEngineTextures);
			count = *reinterpret_cast<const uint32_t*>(DEBUG_MARKER::engine + kEngineTextureCount);
			return textures && count > 0 && count < 200000;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	void ReadSummary()
	{
		g_summary = Summary();
		const uint8_t* textures = nullptr;
		uint32_t count = 0;
		if (!TextureArray(textures, count))
			return;
		for (uint32_t i = 0; i < count; i++)
		{
			TextureInfo info;
			if (!ReadTexture(textures + i * kTextureSize, info, false))
				return;
			g_summary.textures++;
			if (!info.streamable)
				continue;
			g_summary.streamable++;
			g_summary.bytesAll += info.bytes;
			if (info.streamedIn)
			{
				g_summary.streamedIn++;
				g_summary.bytesIn += info.bytes;
			}
		}
		g_summary.have = true;
	}

	// The main view's items' materials (into a buffer of up to max), guarded
	constexpr int kMaxItems = 32768;
	uintptr_t g_materials[kMaxItems];

	int ReadMaterials()
	{
		__try
		{
			const uint8_t* manager = ZONE_LOADER::view_manager_instance ? static_cast<const uint8_t*>(*ZONE_LOADER::view_manager_instance) : nullptr;
			const uint8_t* view = manager ? *reinterpret_cast<const uint8_t* const*>(manager + kMainView) : nullptr;
			if (!view)
				return -1;
			const uint32_t count = (std::min)(*reinterpret_cast<const uint32_t*>(view + kViewListCount), static_cast<uint32_t>(kMaxItems));
			const uint8_t* items = *reinterpret_cast<const uint8_t* const*>(view + kViewListItems);
			if (!items)
				return 0;
			for (uint32_t i = 0; i < count; i++)
				g_materials[i] = *reinterpret_cast<const uintptr_t*>(items + i * kItemSize + kItemMaterial);
			return static_cast<int>(count);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return -1;
		}
	}

	int ReadMaterialTextures(uintptr_t material, uintptr_t* out)
	{
		__try
		{
			const uint8_t* data = reinterpret_cast<const uint8_t*>(material);
			const int count = (std::min)(static_cast<int>(data[kMaterialTextureCount]), kMaxMaterialTextures);
			int found = 0;
			for (int i = 0; i < count; i++)
			{
				const uintptr_t texture = *reinterpret_cast<const uintptr_t*>(data + kMaterialTextures + i * 8);
				if (texture)
					out[found++] = texture;
			}
			return found;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	void ReadDrawn()
	{
		g_drawn = Drawn();
		const int items = ReadMaterials();
		if (items < 0)
			return;
		std::vector<uintptr_t> materials(g_materials, g_materials + items);
		std::sort(materials.begin(), materials.end());
		materials.erase(std::unique(materials.begin(), materials.end()), materials.end());
		std::vector<uintptr_t> textures;
		for (uintptr_t material : materials)
		{
			if (!material)
				continue;
			uintptr_t found[kMaxMaterialTextures];
			const int count = ReadMaterialTextures(material, found);
			textures.insert(textures.end(), found, found + count);
		}
		std::sort(textures.begin(), textures.end());
		textures.erase(std::unique(textures.begin(), textures.end()), textures.end());
		for (uintptr_t texture : textures)
		{
			TextureInfo info;
			if (!ReadTexture(reinterpret_cast<const uint8_t*>(texture), info, true))
				continue;
			g_drawn.textures++;
			if (!info.streamable)
				continue;
			g_drawn.streamable++;
			if (info.streamedIn)
				g_drawn.streamedIn++;
			else
				g_drawn.waiting.push_back(info);
		}
		std::sort(g_drawn.waiting.begin(), g_drawn.waiting.end(), [](const TextureInfo& a, const TextureInfo& b) { return a.bytes > b.bytes; });
		g_drawn.have = true;
	}

	void Read(bool now = false)
	{
		const ULONGLONG tick = GetTickCount64();
		if (!now && g_read && tick - g_read < 1000)
			return;
		g_read = tick;
		ReadProvider(DEVTOOLS_RELATIVE_ADDRESS(kTextureStreaming), g_textures, true);
		ReadProvider(DEVTOOLS_RELATIVE_ADDRESS(kModelStreaming), g_models, false);
		ReadSummary();
		ReadDrawn();
	}

	// ---- Text ----

	std::string Format(const char* format, ...)
	{
		char text[512];
		va_list args;
		va_start(args, format);
		vsnprintf(text, sizeof(text), format, args);
		va_end(args);
		return text;
	}

	float Percent(uint64_t used, uint64_t of)
	{
		return of ? 100.0f * static_cast<float>(static_cast<double>(used) / static_cast<double>(of)) : 0.0f;
	}

	std::string ProviderText(const char* prefix, const Provider& provider)
	{
		if (!provider.have)
			return Format("%s=unavailable\n", prefix);
		std::string text = Format("%s.initialised=%d\n%s.busy=%d\n%s.waiting_requests=%u\n%s.waiting_releases=%u\n%s.time_ms=%llu\n", prefix, provider.initialised ? 1 : 0,
			prefix, provider.busy ? 1 : 0, prefix, provider.requests, prefix, provider.releases, prefix, provider.timeUs / 1000);
		if (provider.haveHeap)
			text += Format("%s.heap=%s\n%s.heap_used=%u\n%s.heap_size=%u\n%s.heap_peak=%u\n", prefix, provider.heapName, prefix, provider.heapUsed, prefix, provider.heapSize, prefix, provider.heapHighest);
		if (provider.budget)
			text += Format("%s.budget=%u\n", prefix, provider.budget);
		return text;
	}

	std::string StreamingText()
	{
		Read(true);
		std::string text = ProviderText("textures", g_textures) + ProviderText("models", g_models);
		if (g_summary.have)
			text += Format("level_textures=%u\nstreamable=%u\nstreamed_in=%u\nstreamed_bytes=%llu\nstreamable_bytes=%llu\n", g_summary.textures, g_summary.streamable, g_summary.streamedIn,
				g_summary.bytesIn, g_summary.bytesAll);
		if (g_drawn.have)
			text += Format("drawn_textures=%u\ndrawn_streamable=%u\ndrawn_streamed_in=%u\ndrawn_waiting=%u\n", g_drawn.textures, g_drawn.streamable, g_drawn.streamedIn, static_cast<uint32_t>(g_drawn.waiting.size()));
		return text;
	}

	std::string WaitingText()
	{
		Read(true);
		if (!g_drawn.have)
			return "no main view (no level running)";
		std::string text = Format("waiting=%u\n", static_cast<uint32_t>(g_drawn.waiting.size()));
		for (const TextureInfo& info : g_drawn.waiting)
			text += Format("%s full=%ux%u now=%ux%u bytes=%u\n", info.name[0] ? info.name : "(no name)", info.fullWidth, info.fullHeight, info.width, info.height, info.bytes);
		return text;
	}

	// ---- Drawing ----

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

	void Bar(float percent)
	{
		char overlay[16];
		snprintf(overlay, sizeof(overlay), "%.0f%%", percent);
		ImGui::PushStyleColor(ImGuiCol_PlotHistogram, percent >= 95.0f ? ImVec4(0.85f, 0.25f, 0.2f, 1.0f) : percent >= 85.0f ? ImVec4(0.85f, 0.6f, 0.2f, 1.0f) : ImVec4(0.3f, 0.6f, 0.35f, 1.0f));
		ImGui::ProgressBar((std::min)(1.0f, percent / 100.0f), ImVec2(-FLT_MIN, 0.0f), overlay);
		ImGui::PopStyleColor();
	}

	void DrawProvider(const char* title, const Provider& provider)
	{
		ImGui::SeparatorText(title);
		if (!provider.have || !provider.initialised)
		{
			PERF_STATS::Note(provider.have ? "Not started (no level running)." : "Not available on this build of the game.");
			return;
		}
		if (provider.haveHeap)
		{
			ImGui::TextWrapped("Heap %s: %s used of %s (the most: %s)", provider.heapName, PERF_STATS::Bytes(provider.heapUsed).c_str(), PERF_STATS::Bytes(provider.heapSize).c_str(),
				PERF_STATS::Bytes(provider.heapHighest).c_str());
			Tooltip("The fixed budget streamed data goes into. When it is full, more can't stream in until something is let go.");
			Bar(Percent(provider.heapUsed, provider.heapSize));
		}
		if (provider.budget)
			ImGui::TextWrapped("Budget set in place of the heap's size: %s", PERF_STATS::Bytes(provider.budget).c_str());
		ImGui::TextWrapped("Waiting: %u to stream in, %u to let go; %s", provider.requests, provider.releases, provider.busy ? "streaming now" : "idle");
		PERF_STATS::Note("%.1f s spent streaming so far", static_cast<double>(provider.timeUs) / 1000000.0);
	}

	void DrawWaiting()
	{
		if (!g_drawn.have)
		{
			PERF_STATS::Note("No main view (no level running).");
			return;
		}
		ImGui::TextWrapped("The main view draws %u textures: %u stream, %u of them in at full resolution", g_drawn.textures, g_drawn.streamable, g_drawn.streamedIn);
		if (g_drawn.waiting.empty())
		{
			PERF_STATS::Note("Everything drawn is in at full resolution.");
			return;
		}
		ImGui::PushTextWrapPos(0.0f);
		ImGui::TextColored(ImVec4(1.0f, 0.75f, 0.3f, 1.0f), "%u drawn at low resolution, waiting to stream in (largest first):", static_cast<uint32_t>(g_drawn.waiting.size()));
		ImGui::PopTextWrapPos();
		// The sizes as wide as the widest they can be written, the texture's path taking the rest: on one line, cut at the
		// front when too long (the whole path in its tooltip)
		const float sizeWidth = (std::max)(PERF_STATS::Width("Full size"), PERF_STATS::Width("8192x8192"));
		const float bytesWidth = (std::max)(PERF_STATS::Width("To stream"), PERF_STATS::Width("1023.9 MB"));
		const float minWidth = PERF_STATS::Width("...") + ImGui::GetFontSize() * 12.0f + sizeWidth * 2.0f + bytesWidth;
		if (!PERF_STATS::BeginScrollingTable("waiting", 4, minWidth, static_cast<int>(g_drawn.waiting.size()), false, 260.0f * DEBUG_MENU::Scale()))
			return;
		ImGui::TableSetupScrollFreeze(0, 1);
		ImGui::TableSetupColumn("Texture", ImGuiTableColumnFlags_WidthStretch | ImGuiTableColumnFlags_NoHide);
		ImGui::TableSetupColumn("Full size", ImGuiTableColumnFlags_WidthFixed, sizeWidth);
		ImGui::TableSetupColumn("Now", ImGuiTableColumnFlags_WidthFixed, sizeWidth);
		ImGui::TableSetupColumn("To stream", ImGuiTableColumnFlags_WidthFixed, bytesWidth);
		ImGui::TableHeadersRow();
		for (const TextureInfo& info : g_drawn.waiting)
		{
			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			PERF_STATS::CellText(info.name[0] ? info.name : "(no name)");
			ImGui::TableNextColumn();
			ImGui::Text("%ux%u", info.fullWidth, info.fullHeight);
			ImGui::TableNextColumn();
			ImGui::Text("%ux%u", info.width, info.height);
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(PERF_STATS::Bytes(info.bytes).c_str());
		}
		PERF_STATS::EndTable();
	}

	void DrawStreamingTab()
	{
		PERF_STATS::EnsureRunning();
		Read();
		DrawProvider("Textures", g_textures);
		DrawProvider("Models", g_models);
		ImGui::SeparatorText("The level's textures");
		if (g_summary.have)
		{
			ImGui::TextWrapped("%u textures; %u stream, %u of them in at full resolution (%s of %s)", g_summary.textures, g_summary.streamable, g_summary.streamedIn,
				PERF_STATS::Bytes(g_summary.bytesIn).c_str(), PERF_STATS::Bytes(g_summary.bytesAll).c_str());
			Tooltip("Textures stream in for the zones that are active: most of a level's are out at any time.");
		}
		else
			PERF_STATS::Note("No textures (no level running).");
		ImGui::SeparatorText("Drawn now");
		DrawWaiting();
	}

	DEBUG_MENU::CommandResult OnRender(std::string (*make)())
	{
		PERF_STATS::EnsureRunning();
		return DEBUG_MENU::AnswerOnRenderThread([make]() -> DEBUG_MENU::CommandResult { return { true, make() }; });
	}

	bool Register()
	{
		DEBUG_MENU::AddCommand("perf.streaming", "texture and model streaming (heap used/size/peak, waiting requests and releases, busy, time), the level's textures in at full resolution, and the main view's, key=value",
			[](const std::string&) { return OnRender([]() { return StreamingText(); }); });
		DEBUG_MENU::AddCommand("perf.list_textures_waiting", "the textures the main view draws that are not in at full resolution yet, largest first: NAME full=WxH now=WxH bytes=",
			[](const std::string&) { return OnRender([]() { return WaitingText(); }); });
		return true;
	}

	const bool g_registered = Register();
	const bool g_added = DEBUG_MENU::AddTab("Streaming", 103, DrawStreamingTab, "Performance");
}

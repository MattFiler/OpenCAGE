#include "RENDER_COMMON.h"
#include "Config.h"
#include "DEBUG_MENU.h"
#include "LIVE_LINK.h"

#include <algorithm>
#include <array>
#include <atomic>
#include <cfloat>
#include <cstdio>
#include <string>
#include <vector>

#include <imgui.h>

/*
	The debug menu's Rendering > Radiosity tab: what the bounce light (radiosity) has loaded and is streaming - whether the
	level's radiosity data loaded at all (a level without it is lit only by its real-time lights and probes, and looks
	flat), which of the level's slices (the parts its bounce-light data is split into) are in the four slots the runtime
	keeps, their sizes, how the runtime ranks the slices to stream, and the objects lit from the bounce light's cube maps.
	An overlay draws the loaded slices' bounds (their probe volumes) and the cube maps' users, and a warning can show over
	the game while a level runs without its radiosity data.

	All of it is read (render thread, at the frame's presentation) from the radiosity object the engine keeps:
	- whether its data loaded, whether it has made what it draws with, the data file's name;
	- the slices: how many the level has; for each of the four slots the slice it holds, whether that is loaded, how many
	  frames it went without its slice loaded (the game counts up while a slot is not loaded and holds the count once it
	  is, so for a loaded slot it is how long its slice took to stream in), its streaming state, and from its header the
	  counts of its surface lights, scatter vertices, probe tree nodes, doors and door transfers; the slot's probe trees
	  and volume probe grid (bounds and dimensions). The header is the one the slot's streaming read in (its own "read"
	  flag), taken only when its tag and version are the game's own; the radiosity object also has a flag of headers
	  loaded per slot, but the shipped game never sets it;
	- the ranking of the slices to stream (a score each), which slot a slice is streaming into now and how long since one did;
	- the objects lit from the bounce light's cube maps this frame (up to 128: the instance and where it is).
	The flag the game sets when a level's radiosity data fails to load is read too (nothing in the shipped game shows it).

	Names: switches "render.radiosity.overlay", "render.radiosity.warning" (its default from RadiosityWarning in
	OpenCAGE.RuntimeUtils.ini, off when missing); command "render.radiosity.status".

	Addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).
*/
namespace
{
	using namespace RENDER_COMMON;

	// The radiosity object's fields
	constexpr uint32_t kLoaded = 0x4;
	constexpr uint32_t kResources = 0x5;
	constexpr uint32_t kFrameIndex = 0x47D4;
	constexpr uint32_t kActiveSlices = 0x4C00;      // four slots of 0x1A50
	constexpr uint32_t kActiveSliceSize = 0x1A50;
	constexpr uint32_t kSlotBindings = 0xB540;      // the slice each slot holds (u16)
	constexpr uint32_t kSlotLoaded = 0xB548;        // bool each
	constexpr uint32_t kStreamableInfo = 0xB54C;    // a pointer: the slices' file offsets (32), then how many slices
	constexpr uint32_t kInfoSliceCount = 0x80;
	constexpr uint32_t kSliceHeaders = 0xB570;      // four of 0xBC
	constexpr uint32_t kSliceHeaderSize = 0xBC;
	constexpr uint32_t kFramesUnused = 0xB864;      // int each: frames the slot has gone without its slice loaded
	constexpr uint32_t kTiledDirectLights = 0xB874;
	constexpr uint32_t kImportances = 0xB89C;       // 32 of { score, slice }
	constexpr uint32_t kStreamingInfo = 0xB99C;     // four of 0xC: reader, header read, header asked for, loading started, unloading state
	constexpr uint32_t kStreamingInfoSize = 0xC;
	constexpr uint32_t kStreamingHeaderRead = 0x4;
	constexpr uint32_t kStreamingHeaderAskedFor = 0x5;
	constexpr uint32_t kStreamingLoadingStarted = 0x6;
	constexpr uint32_t kStreamingUnloading = 0x8;
	constexpr uint32_t kStreamingFileOpen = 0xB9D0;
	constexpr uint32_t kCurrentlyStreaming = 0xB9D4;  // the slot being streamed into (-1: none)
	constexpr uint32_t kFramesSinceStreamed = 0xB9D8;
	constexpr uint32_t kDataFileName = 0xC020;      // a pointer to text
	constexpr int kSlots = 4;
	constexpr int kMaxSlices = 32;

	// A slice header: its tag ("rrds") and version, which the game checks before it loads the slice, then its counts
	constexpr uint32_t kHeaderTag = 0x0;
	constexpr uint32_t kHeaderVersion = 0x4;
	constexpr uint32_t kSliceTag = 0x73647272;
	constexpr uint32_t kSliceVersion = 0x2713;
	constexpr uint32_t kHeaderSurfaceLightVerts = 0x60;
	constexpr uint32_t kHeaderDynamicSurfaceLights = 0x64;
	constexpr uint32_t kHeaderVolumeHashItems = 0x6C;
	constexpr uint32_t kHeaderScatterVertices = 0x74;
	constexpr uint32_t kHeaderInputProbeNodes = 0x80;
	constexpr uint32_t kHeaderSurfaceProbeNodes = 0x88;
	constexpr uint32_t kHeaderDoorTransfers = 0xB0;
	constexpr uint32_t kHeaderDoors = 0xB4;

	// An active slot's slice: its probe trees (a pointer to the root node: min, max), its volume probe grid (a pointer:
	// bounds at +0x1C/+0x28, dimensions at +0x34), its doors (count at +0x900+0x1A) and whether it is valid
	constexpr uint32_t kSliceInputTree = 0x64;
	constexpr uint32_t kSliceSurfaceTree = 0x68;
	constexpr uint32_t kSliceVolumeHash = 0x6C;
	constexpr uint32_t kVolumeHashMin = 0x1C;
	constexpr uint32_t kVolumeHashMax = 0x28;
	constexpr uint32_t kVolumeHashDims = 0x34;
	constexpr uint32_t kVolumeHashItems = 0x10;
	constexpr uint32_t kSliceDoorCount = 0x900 + 0x1A;
	constexpr uint32_t kSliceValid = 0x1A48;

	// The bounce light's cube map users this frame: their instances (128 pointers), positions (128 of x, y, z) and count
	constexpr uintptr_t kCubemapUsers = 0x017DA9E0;
	constexpr uintptr_t kCubemapUserPositions = 0x017DABF0;
	constexpr uintptr_t kCubemapUserCount = 0x017DABE0;
	constexpr uint32_t kMaxCubemapUsers = 0x80;

	// Set by the game when a level's radiosity data fails to load (and never cleared)
	constexpr uintptr_t kNoRadiosityWarning = 0x012EC125;

	struct Slot
	{
		int slice = -1;
		bool loaded = false, valid = false;
		bool headerLoaded = false;  // the slot's streaming has read the header in
		bool headerValid = false;   // ... and it holds the game's tag and version (the counts below are read from it)
		int framesUnused = 0;
		bool reading = false, headerAskedFor = false, loadingStarted = false;
		int unloading = 0;
		uint32_t surfaceLightVerts = 0, dynamicSurfaceLights = 0, volumeItems = 0, scatterVertices = 0, inputNodes = 0, surfaceNodes = 0, doorTransfers = 0, doors = 0;
		bool haveVolume = false;
		float volumeMin[3] = {}, volumeMax[3] = {};
		uint32_t volumeDims[3] = {};
		bool haveInputTree = false, haveSurfaceTree = false;
		float inputMin[3] = {}, inputMax[3] = {}, surfaceMin[3] = {}, surfaceMax[3] = {};
	};

	struct Importance { float score; uint32_t slice; };

	struct State
	{
		bool engine = false;
		bool level = false;
		bool loaded = false, resources = false, tiledDirectLights = false, fileOpen = false;
		bool loadFailedThisRun = false;
		std::string fileName;
		uint32_t sliceCount = 0;
		uint32_t frameIndex = 0;
		int currentlyStreaming = -1;
		uint32_t framesSinceStreamed = 0;
		Slot slots[kSlots];
		std::vector<Importance> importances;
		uint32_t cubemapUsers = 0;
		std::vector<std::pair<uintptr_t, std::array<float, 3>>> users;
	};

	bool LevelRunning()
	{
		__try
		{
			return LIVE_LINK::LevelRunning();
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool ReadBounds(uintptr_t node, float min[3], float max[3])
	{
		return node && Read(reinterpret_cast<const void*>(node), min, sizeof(float) * 3) && Read(reinterpret_cast<const void*>(node + 12), max, sizeof(float) * 3);
	}

	void ReadState(State& out)
	{
		out = State();
		const uintptr_t engine = Engine();
		out.engine = engine != 0;
		out.level = LevelRunning();
		uint8_t flag = 0;
		const uintptr_t warning = Game(kNoRadiosityWarning);
		out.loadFailedThisRun = warning && Read(warning, flag) && flag;
		if (!engine)
			return;
		const uintptr_t radiosity = engine + kEngineRadiosity;
		out.loaded = Read(radiosity + kLoaded, flag) && flag;
		out.resources = Read(radiosity + kResources, flag) && flag;
		out.tiledDirectLights = Read(radiosity + kTiledDirectLights, flag) && flag;
		out.fileOpen = Read(radiosity + kStreamingFileOpen, flag) && flag;
		out.fileName = Text(Pointer(radiosity + kDataFileName), 260);
		Read(radiosity + kFrameIndex, out.frameIndex);
		const uintptr_t info = Pointer(radiosity + kStreamableInfo);
		if (!info || !Read(info + kInfoSliceCount, out.sliceCount) || out.sliceCount > kMaxSlices)
			out.sliceCount = 0;
		Read(radiosity + kCurrentlyStreaming, out.currentlyStreaming);
		Read(radiosity + kFramesSinceStreamed, out.framesSinceStreamed);
		for (int i = 0; i < kSlots; i++)
		{
			Slot& slot = out.slots[i];
			uint16_t binding = 0xFFFF;
			if (Read(radiosity + kSlotBindings + i * 2, binding) && binding != 0xFFFF)
				slot.slice = binding;
			slot.loaded = Read(radiosity + kSlotLoaded + i, flag) && flag;
			Read(radiosity + kFramesUnused + i * 4, slot.framesUnused);
			const uintptr_t streaming = radiosity + kStreamingInfo + i * kStreamingInfoSize;
			slot.reading = Pointer(streaming) != 0;
			slot.headerLoaded = Read(streaming + kStreamingHeaderRead, flag) && flag;
			slot.headerAskedFor = Read(streaming + kStreamingHeaderAskedFor, flag) && flag;
			slot.loadingStarted = Read(streaming + kStreamingLoadingStarted, flag) && flag;
			Read(streaming + kStreamingUnloading, slot.unloading);
			const uintptr_t header = radiosity + kSliceHeaders + i * kSliceHeaderSize;
			uint32_t tag = 0, version = 0;
			slot.headerValid = (slot.headerLoaded || slot.loaded) && Read(header + kHeaderTag, tag) && tag == kSliceTag &&
				Read(header + kHeaderVersion, version) && version == kSliceVersion;
			if (slot.headerValid)
			{
				Read(header + kHeaderSurfaceLightVerts, slot.surfaceLightVerts);
				Read(header + kHeaderDynamicSurfaceLights, slot.dynamicSurfaceLights);
				Read(header + kHeaderVolumeHashItems, slot.volumeItems);
				Read(header + kHeaderScatterVertices, slot.scatterVertices);
				Read(header + kHeaderInputProbeNodes, slot.inputNodes);
				Read(header + kHeaderSurfaceProbeNodes, slot.surfaceNodes);
				Read(header + kHeaderDoorTransfers, slot.doorTransfers);
				Read(header + kHeaderDoors, slot.doors);
			}
			const uintptr_t slice = radiosity + kActiveSlices + i * kActiveSliceSize;
			slot.valid = Read(slice + kSliceValid, flag) && flag;
			if (!slot.loaded)
				continue;
			const uintptr_t volume = Pointer(slice + kSliceVolumeHash);
			slot.haveVolume = volume && Read(reinterpret_cast<const void*>(volume + kVolumeHashMin), slot.volumeMin, sizeof(slot.volumeMin)) &&
				Read(reinterpret_cast<const void*>(volume + kVolumeHashMax), slot.volumeMax, sizeof(slot.volumeMax)) &&
				Read(reinterpret_cast<const void*>(volume + kVolumeHashDims), slot.volumeDims, sizeof(slot.volumeDims));
			slot.haveInputTree = slot.inputNodes > 0 && ReadBounds(Pointer(slice + kSliceInputTree), slot.inputMin, slot.inputMax);
			slot.haveSurfaceTree = slot.surfaceNodes > 0 && ReadBounds(Pointer(slice + kSliceSurfaceTree), slot.surfaceMin, slot.surfaceMax);
		}
		for (uint32_t i = 0; i < out.sliceCount; i++)
		{
			Importance importance = {};
			if (Read(reinterpret_cast<const void*>(radiosity + kImportances + i * 8), &importance, sizeof(importance)))
				out.importances.push_back(importance);
		}
		std::sort(out.importances.begin(), out.importances.end(), [](const Importance& a, const Importance& b) { return a.score > b.score; });

		const uintptr_t count = Game(kCubemapUserCount), users = Game(kCubemapUsers), positions = Game(kCubemapUserPositions);
		if (count && users && positions && Read(count, out.cubemapUsers) && out.cubemapUsers <= kMaxCubemapUsers)
			for (uint32_t i = 0; i < out.cubemapUsers; i++)
			{
				std::array<float, 3> position = {};
				if (Read(reinterpret_cast<const void*>(positions + i * 12), position.data(), sizeof(float) * 3))
					out.users.emplace_back(Pointer(users + i * 4), position);
			}
		else
			out.cubemapUsers = 0;
	}

	const char* UnloadingName(int state)
	{
		switch (state)
		{
		case 0: return "-";
		case 1: return "unloading";
		case 2: return "unloaded";
		default: return "?";
		}
	}

	std::string StatusText(const State& state)
	{
		char text[512];
		snprintf(text, sizeof(text), "level=%d\nloaded=%d\ndrawing=%d\nload_failed_this_run=%d\nfile=%s\nslices=%u\nstreaming_slot=%d\nframes_since_streamed=%u\ntiled_direct_lights=%d\ncubemap_users=%u",
			state.level, state.loaded, state.resources, state.loadFailedThisRun, state.fileName.c_str(), state.sliceCount, state.currentlyStreaming, state.framesSinceStreamed, state.tiledDirectLights, state.cubemapUsers);
		std::string out = text;
		for (int i = 0; i < kSlots; i++)
		{
			const Slot& slot = state.slots[i];
			snprintf(text, sizeof(text), "\nslot%d slice=%d loaded=%d header=%d header_valid=%d valid=%d frames_without_slice=%d reading=%d unloading=%s surface_light_verts=%u dynamic_surface_lights=%u scatter_vertices=%u probe_nodes=%u/%u volume_items=%u doors=%u door_transfers=%u volume=%u,%u,%u",
				i, slot.slice, slot.loaded, slot.headerLoaded, slot.headerValid, slot.valid, slot.framesUnused, slot.reading, UnloadingName(slot.unloading), slot.surfaceLightVerts, slot.dynamicSurfaceLights,
				slot.scatterVertices, slot.inputNodes, slot.surfaceNodes, slot.volumeItems, slot.doors, slot.doorTransfers, slot.volumeDims[0], slot.volumeDims[1], slot.volumeDims[2]);
			out += text;
		}
		for (const Importance& importance : state.importances)
		{
			snprintf(text, sizeof(text), "\nrank slice=%u score=%.3f", importance.slice, importance.score);
			out += text;
		}
		return out;
	}

	std::atomic<bool> g_overlay = false;
	std::atomic<bool> g_warning = false;
	bool g_registered = false;
	bool g_iniRead = false;
	State g_state;
	ULONGLONG g_tabShownAt = 0;
	bool g_slotsScroll = false;  // the slots' table was wider than the window last frame (so it has a scroll bar)

	void Register()
	{
		if (g_registered)
			return;
		g_registered = true;
		DEBUG_MENU::AddSwitch("render.radiosity.overlay", &g_overlay);
		DEBUG_MENU::AddSwitch("render.radiosity.warning", &g_warning);
		DEBUG_MENU::AddCommand("render.radiosity.status", "the bounce light's data: loaded or not, its file, the four slots' slices and sizes, the streaming ranking", [](const std::string&) -> DEBUG_MENU::CommandResult
		{
			return DEBUG_MENU::AnswerOnRenderThread([]() -> DEBUG_MENU::CommandResult
			{
				State state;
				ReadState(state);
				return { true, StatusText(state) };
			});
		});
	}

	const ImU32 kSlotColours[kSlots] = { IM_COL32(90, 200, 255, 220), IM_COL32(255, 170, 60, 220), IM_COL32(140, 255, 120, 220), IM_COL32(230, 110, 255, 220) };

	void Overlay(ImDrawList* drawList)
	{
		Register();
		// The warning's default, from the ini file once the game is running
		if (!g_iniRead)
		{
			g_iniRead = true;
			if (Config::IniInt("RadiosityWarning", 0) != 0)
				g_warning = true;
		}
		const bool shown = g_tabShownAt && GetTickCount64() - g_tabShownAt < 500;
		if (!g_overlay && !g_warning && !shown)
			return;
		ReadState(g_state);
		const State& state = g_state;

		if (g_warning && state.level && !state.loaded)
		{
			const char* text = "This level's radiosity (bounce light) data is not loaded: it is lit only by its real-time lights";
			const ImVec2 display = ImGui::GetIO().DisplaySize;
			const float size = 16.0f * DEBUG_MENU::Scale();
			const ImVec2 extent = ImGui::GetFont()->CalcTextSizeA(size, FLT_MAX, 0.0f, text);
			// Its box (4 pixels around the text) goes under the notes already along the top of the screen
			const ImVec2 at((display.x - extent.x) * 0.5f, DEBUG_MENU::BannerTop(extent.y + 8.0f) + 4.0f);
			drawList->AddRectFilled(ImVec2(at.x - 8.0f, at.y - 4.0f), ImVec2(at.x + extent.x + 8.0f, at.y + extent.y + 4.0f), IM_COL32(90, 20, 10, 200), 4.0f);
			drawList->AddText(ImGui::GetFont(), size, at, IM_COL32(255, 210, 150, 255), text);
		}
		if (!g_overlay)
			return;
		for (int i = 0; i < kSlots; i++)
		{
			const Slot& slot = state.slots[i];
			if (!slot.loaded)
				continue;
			char label[64];
			snprintf(label, sizeof(label), "radiosity slice %d (slot %d)", slot.slice, i);
			if (slot.haveVolume)
			{
				DEBUG_MENU::WorldBox(drawList, slot.volumeMin, slot.volumeMax, kSlotColours[i], 2.0f);
				const float top[3] = { (slot.volumeMin[0] + slot.volumeMax[0]) * 0.5f, slot.volumeMax[1], (slot.volumeMin[2] + slot.volumeMax[2]) * 0.5f };
				ScreenLabel(drawList, top, label, kSlotColours[i]);
			}
			const ImU32 faint = (kSlotColours[i] & 0x00FFFFFF) | 0x70000000;
			if (slot.haveInputTree)
				DEBUG_MENU::WorldBox(drawList, slot.inputMin, slot.inputMax, faint, 1.0f);
			if (slot.haveSurfaceTree)
				DEBUG_MENU::WorldBox(drawList, slot.surfaceMin, slot.surfaceMax, faint, 1.0f);
		}
		for (const auto& user : state.users)
		{
			DEBUG_MENU::WorldCross(drawList, user.second.data(), 0.25f, IM_COL32(255, 255, 120, 220), 1.5f);
		}
	}

	void DrawTab()
	{
		Register();
		g_tabShownAt = GetTickCount64();
		const State& state = g_state;
		ImGui::TextWrapped("What the bounce light (radiosity) has loaded and is streaming: the level's slices (the parts its bounce-light data is split into) in the runtime's four slots, their sizes, and the ranking it streams them by.");
		if (!state.engine)
		{
			ColouredNote(ImVec4(1.0f, 0.6f, 0.3f, 1.0f), "Not available on this game release.");
			return;
		}
		bool overlay = g_overlay, warning = g_warning;
		if (ImGui::Checkbox("Overlay##radiosity", &overlay))
			g_overlay = overlay;
		if (ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
			ImGui::SetTooltip("render.radiosity.overlay: each loaded slice's volume probe grid (a box in its slot's colour), its probe trees' bounds (faint), and the objects lit from the bounce light's cube maps (yellow crosses).");
		ImGui::SameLine();
		if (ImGui::Checkbox("Warn when a level has no radiosity data##radiosity", &warning))
			g_warning = warning;
		if (ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
			ImGui::SetTooltip("render.radiosity.warning: a warning over the game while a level runs without its radiosity data. RadiosityWarning=1 in OpenCAGE.RuntimeUtils.ini turns it on from the start.");

		if (!state.level)
		{
			ImGui::TextDisabled("No level running.");
			return;
		}
		if (!state.loaded)
			ColouredNote(ImVec4(1.0f, 0.45f, 0.3f, 1.0f), "This level's radiosity data is NOT loaded: it is lit only by its real-time lights.");
		else
			ImGui::TextWrapped("Radiosity data loaded: %s", state.fileName.empty() ? "(file name not read)" : state.fileName.c_str());
		if (state.loadFailedThisRun)
			ColouredNote(ImVec4(1.0f, 0.7f, 0.3f, 1.0f), "A level's radiosity data failed to load earlier in this run (the game's own flag, never cleared).");
		ImGui::TextWrapped("Slices in the level: %u. Bounce light applied to the frame: %s. Streaming: %s.", state.sliceCount, state.resources ? "yes" : "no", state.currentlyStreaming >= 0 && state.currentlyStreaming < kSlots ? ("into slot " + std::to_string(state.currentlyStreaming) + " (slice " + std::to_string(state.slots[state.currentlyStreaming].slice) + ")").c_str() : "nothing");
		Note("%u frames since a slice last streamed in; data file %s; %s; frame %u", state.framesSinceStreamed, state.fileOpen ? "open" : "closed",
			state.tiledDirectLights ? "tiled direct lights" : "untiled direct lights", state.frameIndex);

		// Scrolls sideways (the slot column kept in sight) rather than squeezing the columns when the window is narrower than
		// the table: as tall as its rows, plus the scroll bar while it has one
		const ImGuiTableFlags flags = ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_SizingFixedFit | ImGuiTableFlags_ScrollX;
		const float rowHeight = ImGui::GetTextLineHeight() + ImGui::GetStyle().CellPadding.y * 2.0f;
		const float tableHeight = rowHeight * (kSlots + 1) + (g_slotsScroll ? ImGui::GetStyle().ScrollbarSize : 0.0f) + 2.0f;
		if (ImGui::BeginTable("slots", 9, flags, ImVec2(0.0f, tableHeight)))
		{
			ImGui::TableSetupScrollFreeze(1, 0);
			ImGui::TableSetupColumn("Slot");
			ImGui::TableSetupColumn("Slice");
			ImGui::TableSetupColumn("State");
			ImGui::TableSetupColumn("Frames to load");
			ImGui::TableSetupColumn("Scatter verts");
			ImGui::TableSetupColumn("Surface lights");
			ImGui::TableSetupColumn("Probe tree nodes");
			ImGui::TableSetupColumn("Doors");
			ImGui::TableSetupColumn("Volume grid");
			ImGui::TableHeadersRow();
			for (int i = 0; i < kSlots; i++)
			{
				const Slot& slot = state.slots[i];
				ImGui::TableNextRow();
				ImGui::TableNextColumn();
				ImGui::TextColored(ImGui::ColorConvertU32ToFloat4(kSlotColours[i]), "%d", i);
				ImGui::TableNextColumn();
				if (slot.slice >= 0)
					ImGui::Text("%d", slot.slice);
				else
					ImGui::TextDisabled("-");
				ImGui::TableNextColumn();
				std::string text = slot.loaded ? "loaded" : slot.reading ? "reading" : slot.headerAskedFor ? "header asked" : "empty";
				if (slot.unloading)
					text += std::string(", ") + UnloadingName(slot.unloading);
				if (slot.loaded && !slot.valid)
					text += ", not valid";
				ImGui::TextUnformatted(text.c_str());
				ImGui::TableNextColumn();
				if (slot.slice >= 0)
					ImGui::Text("%d", slot.framesUnused);
				else
					ImGui::TextDisabled("-");
				// The header's counts, while the slot holds one the game would load
				if (slot.headerValid)
				{
					ImGui::TableNextColumn();
					ImGui::Text("%u", slot.scatterVertices);
					ImGui::TableNextColumn();
					ImGui::Text("%u verts, %u dynamic", slot.surfaceLightVerts, slot.dynamicSurfaceLights);
					ImGui::TableNextColumn();
					ImGui::Text("%u input, %u surface", slot.inputNodes, slot.surfaceNodes);
					ImGui::TableNextColumn();
					ImGui::Text("%u (%u transfers)", slot.doors, slot.doorTransfers);
				}
				else
					for (int column = 0; column < 4; column++)
					{
						ImGui::TableNextColumn();
						ImGui::TextDisabled("-");
					}
				ImGui::TableNextColumn();
				if (slot.haveVolume)
					ImGui::Text("%u x %u x %u", slot.volumeDims[0], slot.volumeDims[1], slot.volumeDims[2]);
				else
					ImGui::TextDisabled("-");
			}
			g_slotsScroll = ImGui::GetScrollMaxX() > 0.0f;
			ImGui::EndTable();
		}
		Note("Frames to load: how many frames the slot went without its slice loaded - the game stops counting once it loads, so for a loaded slot it is how long the slice took to stream in. Scatter verts: the points that carry its probes' light into the lightmaps. Surface lights: the vertices of its glowing surfaces, and how many of those can change. Probe tree nodes: its input and surface probe trees. Doors: its doors and the transfers of bounce light through them. Volume grid: its volume probe grid's cells along x, y and z.");

		if (ImGui::CollapsingHeader("Streaming ranking##radiosity"))
		{
			Note("The runtime keeps the highest-scoring slices loaded.");
			for (const Importance& importance : state.importances)
			{
				bool loaded = false;
				for (const Slot& slot : state.slots)
					loaded = loaded || (slot.loaded && slot.slice == static_cast<int>(importance.slice));
				ImGui::Text("slice %2u  score %8.3f%s", importance.slice, importance.score, loaded ? "  (loaded)" : "");
			}
		}
		if (ImGui::CollapsingHeader("Cube map users##radiosity"))
		{
			Note("Objects lit from the bounce light's cube maps this frame (moving objects and characters): %u of %u", state.cubemapUsers, kMaxCubemapUsers);
			for (const auto& user : state.users)
				ImGui::Text("%08X  at %.2f, %.2f, %.2f", static_cast<unsigned>(user.first), user.second[0], user.second[1], user.second[2]);
		}
	}

	const bool g_tabAdded = DEBUG_MENU::AddTab("Radiosity", 73, DrawTab, "Rendering");
	const bool g_overlayAdded = DEBUG_MENU::AddOverlay(41, Overlay);
	const bool g_namesAdded = (Register(), true);
}

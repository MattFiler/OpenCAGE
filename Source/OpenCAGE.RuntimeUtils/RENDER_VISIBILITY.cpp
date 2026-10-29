#include "RENDER_COMMON.h"
#include "DEBUG_MENU.h"
#include "ZONE_LOADER.h"

#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <mutex>
#include <string>
#include <vector>

#include <imgui.h>

/*
	The debug menu's Rendering > Visibility tab: why something is or isn't drawn - the zones the renderer treats as
	visible from the main view, a check of one object (by its mover index, as OpenCAGE knows it) through each step the
	renderer takes before drawing it, the level-of-detail distance factor (with an override), and the reflection maps the
	level's objects use (with an overlay of the objects that use one).

	Visible zones. The zone manager keeps, for each of the level's zones, which views see it (a bit per view) and its
	priority: the renderer draws the zones the main view's bit is set in whose priority is 80 or more (the same test as
	the game's own list of potentially visible zones, read here rather than called).

	One object. The instance database keeps every renderable instance by its mover index. An instance has a handle (its
	type in the low four bits, then the renderer's zone slot it is filed under, then its index in that slot), its flags
	(active...) and its position. The slot gives the two zones it belongs to and whether the slot's cache is active; the
	index gives its visibility record (the views it is kept out of, a size below which it is culled, its state bits - the
	game skips an instance with 0x01 or 0x08 set - and its type bits). Then: is it in the main view's render list this
	frame (with its models and their materials' draw priorities), how far is it from the camera. Each check is shown with
	what it means; the first that fails is why it isn't drawn.

	Level of detail. The game's range factor for its level-of-detail switches is three numbers multiplied (a per-frame
	factor the game sets every frame from a constant, the graphics settings' scale, and a scale the level's LODControls
	entities set); the override replaces the constant the per-frame factor is set from, so models switch to less detailed
	versions nearer (below 1) or further (above 1).

	Reflection maps. The reflection map manager keeps each map (its texture, priority, colour factor, the instances that
	use it, and whether it still needs baking) and each object's map.

	Names: values "render.lod_factor" (0 = the game's) and "render.envmap.selected" (-1 = none); switch
	"render.envmap.overlay"; commands "render.visible_zones", "render.why_not_drawn" (a mover index), "render.envmap.list".

	Addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).
*/
namespace
{
	using namespace RENDER_COMMON;

	// ---- Zones ----

	// A zone: priority, the views that see it (a bit each), its state (bit 0: loaded), id, name (a pointer to text)
	constexpr uint32_t kZonePriority = 0x0;
	constexpr uint32_t kZoneViewers = 0x4;
	constexpr uint32_t kZoneState = 0x8;
	constexpr uint32_t kZoneName = 0x30;
	constexpr int kVisiblePriority = 0x50;
	constexpr uintptr_t kZoneManager = 0x0134F860;   // where the game keeps its pointer to the zone manager
	// The view manager's main view (see ZONE_LOADER.h), a view's index (its bit in a zone's viewers), its render list
	// (count, then a pointer to its items of 0x18: instance, model, material, set-up, priority at +0x10)
	constexpr uint32_t kViewIndex = 0x34A;
	constexpr uint32_t kViewRenderList = 0xEC;
	constexpr uint32_t kRenderItemSize = 0x18;
	constexpr uint32_t kRenderItemModel = 0x4;
	constexpr uint32_t kRenderItemMaterial = 0x8;
	constexpr uint32_t kRenderItemPriority = 0x10;
	// A model: its file header, whose source file name and name are pointers
	constexpr uint32_t kModelSourceName = 0x0;
	constexpr uint32_t kModelName = 0x8;
	constexpr uint32_t kMaterialPriority = 0xBC;

	// ---- Instances ----

	constexpr uint32_t kInstanceHandle = 0x30;
	constexpr uint32_t kInstanceFlags = 0x34;
	// The renderer's zone slots: 512 records (pointers; the slot's cache active while +4 is not zero, its visibility
	// records at +0x24, 8 bytes each), and each slot's two zone ids
	constexpr uintptr_t kSlotRecords = 0x015CA010;
	constexpr uintptr_t kSlotZones = 0x015CA810;
	constexpr uint32_t kRecordActive = 0x4;
	constexpr uint32_t kRecordVisibility = 0x24;
	const char* const kInstanceTypes[] = { "light", "effect", "effect (own material)", "environment", "character", "misc", "planet", "environment extra", "fog sphere" };
	struct FlagName { uint16_t bit; const char* name; };
	const FlagName kInstanceFlagNames[] = {
		{ 0x1, "active" }, { 0x2, "stationary" }, { 0x4, "has moved" }, { 0x8, "temporary" }, { 0x10, "has script" },
		{ 0x20, "motion blur" }, { 0x40, "approximate lighting" }, { 0x80, "radiosity cube map" }, { 0x100, "usable-object glow" },
		{ 0x200, "occludes atmosphere" }, { 0x400, "flash script" },
	};

	// ---- Level of detail ----

	constexpr uintptr_t kLodManager = 0x012AE470;     // a static object
	constexpr uintptr_t kLodConstant = 0x0124F024;    // what its per-frame factor is set from every frame
	constexpr uint32_t kLodFrameFactor = 0x74;
	constexpr uint32_t kLodSettingsScale = 0x78;
	constexpr uint32_t kLodScriptedScale = 0x7C;
	constexpr uint32_t kLodDisabled = 0x80;

	// ---- Reflection maps ----

	// The manager: its maps (elements, count, capacity: 0x28 each - texture, priority, colour factor x4, instances
	// (elements, count, capacity), needs baking), the next free map, its objects (elements, count: instance, map index)
	constexpr uint32_t kMapsElements = 0x0;
	constexpr uint32_t kMapsCount = 0x4;
	constexpr uint32_t kMapSize = 0x28;
	constexpr uint32_t kMapPriority = 0x4;
	constexpr uint32_t kMapColour = 0x8;
	constexpr uint32_t kMapInstances = 0x18;
	constexpr uint32_t kMapNeedsBake = 0x24;
	constexpr uint32_t kEntitiesCount = 0x14;
	constexpr uint32_t kTextureHeader = 0x68;
	constexpr uint32_t kHeaderFileName = 0x28;

	std::atomic<float> g_lodFactor = 0.0f;   // 0: the game's
	std::atomic<float> g_selectedMap = -1.0f;
	std::atomic<bool> g_mapOverlay = false;
	bool g_lodApplied = false;
	float g_lodSaved = 1.0f;
	float g_lodWritten = 1.0f;
	bool g_registered = false;
	int g_mover = 0;           // the tab's mover index box
	std::string g_diagnosis;   // the tab's last check

	uintptr_t MainView()
	{
		const uintptr_t engine = Engine();
		const uintptr_t views = engine ? Pointer(engine + kEngineViews) : 0;
		return views ? Pointer(views + ZONE_LOADER::kMainViewOffset) : 0;
	}

	// ---- Zones ----

	struct Zone
	{
		uint32_t id = 0;
		int priority = 0;
		uint32_t viewers = 0, state = 0;
		std::string name;
		bool visible = false;
	};

	bool ReadZones(std::vector<Zone>& out, int& viewIndex)
	{
		out.clear();
		viewIndex = -1;
		const uintptr_t view = MainView();
		uint8_t index = 0;
		if (view && Read(view + kViewIndex, index))
			viewIndex = index;
		const uintptr_t manager = Pointer(Game(kZoneManager));
		const uintptr_t zones = manager ? Pointer(manager + ZONE_LOADER::kZoneArrayOffset) : 0;
		int count = 0;
		if (!zones || !Read(zones + ZONE_LOADER::kArrayCountOffset, count) || count <= 0 || count > 4096)
			return false;
		const uintptr_t allocations = Pointer(zones + ZONE_LOADER::kArrayDataOffset);
		for (int i = 0; allocations && i < count; i++)
		{
			const uintptr_t allocation = Pointer(allocations + i * 4);
			const uintptr_t object = allocation ? Pointer(allocation + ZONE_LOADER::kAllocationObjectOffset) : 0;
			if (!object)
				continue;
			Zone zone;
			Read(object + kZonePriority, zone.priority);
			Read(object + kZoneViewers, zone.viewers);
			Read(object + kZoneState, zone.state);
			Read(object + ZONE_LOADER::kZoneIdOffset, zone.id);
			zone.name = Text(Pointer(object + kZoneName), 96);
			zone.visible = viewIndex >= 0 && viewIndex < 32 && (zone.viewers & (1u << viewIndex)) && zone.priority >= kVisiblePriority;
			out.push_back(zone);
		}
		return true;
	}

	std::string ZoneName(const std::vector<Zone>& zones, uint32_t id)
	{
		for (const Zone& zone : zones)
			if (zone.id == id)
				return zone.name.empty() ? "(unnamed)" : zone.name;
		return id == 0 ? "the global zone" : "?";
	}

	std::string VisibleZonesText()
	{
		std::vector<Zone> zones;
		int viewIndex = -1;
		if (!ReadZones(zones, viewIndex))
			return "No level running";
		char line[256];
		snprintf(line, sizeof(line), "main_view_index=%d\n", viewIndex);
		std::string text = line;
		for (const Zone& zone : zones)
		{
			snprintf(line, sizeof(line), "zone=%08X visible=%d loaded=%d priority=%d viewers=%08X name=%s\n", zone.id, zone.visible, zone.state & 1, zone.priority, zone.viewers, zone.name.c_str());
			text += line;
		}
		return text;
	}

	// ---- One object ----

	uintptr_t InstanceOf(int mover)
	{
		const uintptr_t engine = Engine();
		uint32_t count = 0;
		if (!engine || mover < 0 || !Read(engine + kEngineInstances, count) || static_cast<uint32_t>(mover) >= count)
			return 0;
		const uintptr_t array = Pointer(engine + kEngineInstances + 8);
		return array ? Pointer(array + mover * 4) : 0;
	}

	std::string Diagnose(int mover)
	{
		char line[320];
		std::string text;
		const uintptr_t engine = Engine();
		uint32_t count = 0;
		if (!engine || !Read(engine + kEngineInstances, count))
			return "No level running";
		if (mover < 0 || static_cast<uint32_t>(mover) >= count)
		{
			snprintf(line, sizeof(line), "FAIL mover index %d is not in the level (it has %u instances).\n", mover, count);
			return line;
		}
		const uintptr_t instance = InstanceOf(mover);
		if (!instance)
			return "FAIL no renderable instance for this mover index: nothing of it is loaded to draw (a mover with no model, or its zone isn't loaded).\n";
		uint32_t handle = 0;
		uint16_t flags = 0;
		float matrix[12] = {};
		Read(instance + kInstanceHandle, handle);
		Read(instance + kInstanceFlags, flags);
		Read(reinterpret_cast<const void*>(instance), matrix, sizeof(matrix));
		const uint32_t type = handle & 0xF, slot = (handle >> 4) & 0x1FF, index = handle >> 13;
		snprintf(line, sizeof(line), "instance=%08X type=%s handle=%08X position=%.2f,%.2f,%.2f\n", static_cast<unsigned>(instance), type < 9 ? kInstanceTypes[type] : "?", handle, matrix[3], matrix[7], matrix[11]);
		text += line;
		std::string flagText;
		for (const FlagName& flag : kInstanceFlagNames)
			if (flags & flag.bit)
				flagText += std::string(flagText.empty() ? "" : ", ") + flag.name;
		snprintf(line, sizeof(line), "%s flags=%04X (%s)\n", (flags & 1) ? "ok  " : "FAIL", flags, flagText.empty() ? "none" : flagText.c_str());
		text += line;
		if (!(flags & 1))
			text += "     not active: the renderer skips it (hidden by its entity, or its zone isn't active)\n";

		std::vector<Zone> zones;
		int viewIndex = -1;
		ReadZones(zones, viewIndex);
		uint32_t pair[2] = {};
		const uintptr_t slotZones = Game(kSlotZones);
		if (slotZones && Read(reinterpret_cast<const void*>(slotZones + slot * 8), pair, sizeof(pair)))
		{
			for (int i = 0; i < 2; i++)
			{
				const Zone* zone = nullptr;
				for (const Zone& z : zones)
					if (z.id == pair[i])
						zone = &z;
				const bool ok = !zone || zone->visible || pair[i] == 0;
				snprintf(line, sizeof(line), "%s %s zone %08X %s%s\n", ok ? "ok  " : "FAIL", i == 0 ? "primary" : "secondary", pair[i], ZoneName(zones, pair[i]).c_str(),
					zone ? (zone->visible ? " (visible from the main view)" : (zone->state & 1) ? " (loaded but not visible from the main view)" : " (not loaded)") : "");
				text += line;
			}
		}
		const uintptr_t records = Game(kSlotRecords);
		const uintptr_t record = records ? Pointer(records + slot * 4) : 0;
		if (record)
		{
			uint32_t active = 0;
			Read(record + kRecordActive, active);
			snprintf(line, sizeof(line), "%s renderer slot %u cache %s\n", active ? "ok  " : "FAIL", slot, active ? "active" : "not active (its zones aren't being drawn)");
			text += line;
			const uintptr_t visibility = Pointer(record + kRecordVisibility);
			struct { uint32_t excluded; uint16_t sizeCull; uint8_t state; uint8_t typeBits; } record8 = {};
			if (visibility && Read(reinterpret_cast<const void*>(visibility + index * 8), &record8, sizeof(record8)))
			{
				const bool excludedMain = viewIndex >= 0 && viewIndex < 32 && (record8.excluded & (1u << viewIndex));
				snprintf(line, sizeof(line), "%s state bits %02X%s\n", (record8.state & 9) ? "FAIL" : "ok  ", record8.state, (record8.state & 9) ? " (0x01/0x08 set: the game skips it - hidden or disabled)" : "");
				text += line;
				snprintf(line, sizeof(line), "%s views kept out of %08X%s; size culling below %u; type bits %02X\n", excludedMain ? "FAIL" : "ok  ", record8.excluded, excludedMain ? " (including the main view)" : "", record8.sizeCull, record8.typeBits);
				text += line;
			}
		}
		else
			text += "FAIL no renderer slot record (not filed in any zone)\n";

		// In the main view's render list this frame?
		const uintptr_t view = MainView();
		uint32_t items = 0;
		const uintptr_t list = view ? Pointer(view + kViewRenderList + 4) : 0;
		int found = 0;
		std::string models;
		if (list && Read(view + kViewRenderList, items) && items < 200000)
		{
			std::vector<uint8_t> data(static_cast<size_t>(items) * kRenderItemSize);
			if (!data.empty() && Read(reinterpret_cast<const void*>(list), data.data(), data.size()))
				for (uint32_t i = 0; i < items; i++)
				{
					uintptr_t item;
					memcpy(&item, &data[i * kRenderItemSize], 4);
					if (item != instance)
						continue;
					found++;
					if (found <= 6)
					{
						uintptr_t model, material;
						memcpy(&model, &data[i * kRenderItemSize + kRenderItemModel], 4);
						memcpy(&material, &data[i * kRenderItemSize + kRenderItemMaterial], 4);
						const std::string name = Text(Pointer(model + kModelName), 96);
						const std::string source = Text(Pointer(model + kModelSourceName), 96);
						uint8_t priority = data[i * kRenderItemSize + kRenderItemPriority];
						uint8_t materialPriority = 0;
						Read(material + kMaterialPriority, materialPriority);
						snprintf(line, sizeof(line), "     %s (%s), draw priority %u, material priority %u\n", name.empty() ? "?" : name.c_str(), source.c_str(), priority, materialPriority);
						models += line;
					}
				}
			snprintf(line, sizeof(line), "%s in the main view's render list this frame: %d item(s) of %u\n", found ? "ok  " : "FAIL", found, items);
			text += line + models;
			if (!found)
				text += "     not drawn this frame: off screen, occluded (render.no_occlusion_culling shows whether that's why), beyond its last level of detail, or one of the above\n";
		}
		float camera[3];
		if (DEBUG_MENU::CameraPosition(camera))
		{
			const float dx = matrix[3] - camera[0], dy = matrix[7] - camera[1], dz = matrix[11] - camera[2];
			snprintf(line, sizeof(line), "     %.1f m from the camera\n", std::sqrt(dx * dx + dy * dy + dz * dz));
			text += line;
		}
		return text;
	}

	// ---- Level of detail ----

	struct Lod { bool have = false; float frame = 0, settings = 0, scripted = 0, constant = 0; bool disabled = false; };

	Lod ReadLod()
	{
		Lod lod;
		const uintptr_t manager = Game(kLodManager);
		uint8_t disabled = 0;
		lod.have = manager && Read(manager + kLodFrameFactor, lod.frame) && Read(manager + kLodSettingsScale, lod.settings) &&
			Read(manager + kLodScriptedScale, lod.scripted) && Read(manager + kLodDisabled, disabled) && Read(Game(kLodConstant), lod.constant);
		lod.disabled = disabled != 0;
		return lod;
	}

	void ApplyLod()
	{
		const uintptr_t constant = Game(kLodConstant);
		if (!constant)
			return;
		float now = 0.0f;
		if (!Read(constant, now))
			return;
		const float wanted = g_lodFactor;
		if (wanted > 0.0f && std::isfinite(wanted))
		{
			if (!g_lodApplied)
				g_lodSaved = now;
			else if (now != g_lodWritten)
				g_lodSaved = now;
			g_lodApplied = true;
			g_lodWritten = wanted;
			if (now != wanted)
				Write(constant, wanted);
		}
		else if (g_lodApplied)
		{
			g_lodApplied = false;
			if (now == g_lodWritten)
				Write(constant, g_lodSaved);
		}
	}

	// ---- Reflection maps ----

	struct Map
	{
		std::string texture;
		uint32_t priority = 0;
		float colour[4] = {};
		uint32_t instances = 0;
		uintptr_t instanceArray = 0;
		bool needsBake = false;
	};

	bool ReadMaps(std::vector<Map>& out, uint32_t& objects)
	{
		out.clear();
		objects = 0;
		const uintptr_t engine = Engine();
		if (!engine)
			return false;
		const uintptr_t manager = engine + kEngineEnvironmentMaps;
		const uintptr_t maps = Pointer(manager + kMapsElements);
		uint32_t count = 0;
		Read(manager + kEntitiesCount, objects);
		if (!maps || !Read(manager + kMapsCount, count) || count > 4096)
			return maps == 0;
		for (uint32_t i = 0; i < count; i++)
		{
			const uintptr_t at = maps + i * kMapSize;
			Map map;
			const uintptr_t texture = Pointer(at);
			const uintptr_t header = texture ? Pointer(texture + kTextureHeader) : 0;
			map.texture = header ? Text(Pointer(header + kHeaderFileName), 200) : (texture ? "(unnamed)" : "(none)");
			Read(at + kMapPriority, map.priority);
			Read(reinterpret_cast<const void*>(at + kMapColour), map.colour, sizeof(map.colour));
			map.instanceArray = Pointer(at + kMapInstances);
			Read(at + kMapInstances + 4, map.instances);
			uint8_t bake = 0;
			map.needsBake = Read(at + kMapNeedsBake, bake) && bake;
			out.push_back(map);
		}
		return true;
	}

	std::string MapsText()
	{
		std::vector<Map> maps;
		uint32_t objects = 0;
		if (!ReadMaps(maps, objects))
			return "No level running";
		std::string text = "objects=" + std::to_string(objects) + "\n";
		char line[400];
		for (size_t i = 0; i < maps.size(); i++)
		{
			const Map& map = maps[i];
			snprintf(line, sizeof(line), "%u texture=%s priority=%u colour=%.3f,%.3f,%.3f,%.3f instances=%u needs_bake=%d\n", static_cast<unsigned>(i), map.texture.c_str(), map.priority,
				map.colour[0], map.colour[1], map.colour[2], map.colour[3], map.instances, map.needsBake);
			text += line;
		}
		return text;
	}

	// ---- The view ----

	void Register()
	{
		if (g_registered)
			return;
		g_registered = true;
		DEBUG_MENU::AddValue("render.lod_factor", &g_lodFactor, 0.0f, 20.0f);
		DEBUG_MENU::AddValue("render.envmap.selected", &g_selectedMap, -1.0f, 4096.0f);
		DEBUG_MENU::AddSwitch("render.envmap.overlay", &g_mapOverlay);
		DEBUG_MENU::AddCommand("render.visible_zones", "the level's zones: id, visible from the main view, loaded, priority, the views that see it, name", [](const std::string&) -> DEBUG_MENU::CommandResult
		{
			return DEBUG_MENU::AnswerOnRenderThread([]() -> DEBUG_MENU::CommandResult { return { true, VisibleZonesText() }; });
		});
		DEBUG_MENU::AddCommand("render.why_not_drawn", "checks one object (its mover index, or mover:N) through each step before the renderer draws it; FAIL marks what stops it", [](const std::string& argument) -> DEBUG_MENU::CommandResult
		{
			std::string text = argument;
			const size_t colon = text.find(':');
			if (colon != std::string::npos)
				text = text.substr(colon + 1);
			char* end = nullptr;
			const long mover = strtol(text.c_str(), &end, 10);
			if (text.empty() || end == text.c_str())
				return { false, "Give a mover index" };
			return DEBUG_MENU::AnswerOnRenderThread([mover]() -> DEBUG_MENU::CommandResult { return { true, Diagnose(static_cast<int>(mover)) }; });
		});
		DEBUG_MENU::AddCommand("render.envmap.list", "the level's reflection maps: index, texture, priority, colour factor, how many objects use it, whether it needs baking", [](const std::string&) -> DEBUG_MENU::CommandResult
		{
			return DEBUG_MENU::AnswerOnRenderThread([]() -> DEBUG_MENU::CommandResult { return { true, MapsText() }; });
		});
	}

	void Overlay(ImDrawList* drawList)
	{
		Register();
		ApplyLod();
		const int selected = static_cast<int>(std::lround(static_cast<float>(g_selectedMap)));
		if (!g_mapOverlay || selected < 0)
			return;
		std::vector<Map> maps;
		uint32_t objects = 0;
		if (!ReadMaps(maps, objects) || selected >= static_cast<int>(maps.size()))
			return;
		const Map& map = maps[selected];
		float camera[3] = {};
		const bool haveCamera = DEBUG_MENU::CameraPosition(camera);
		const uint32_t shown = (std::min)(map.instances, 2048u);
		for (uint32_t i = 0; map.instanceArray && i < shown; i++)
		{
			const uintptr_t instance = Pointer(map.instanceArray + i * 4);
			float matrix[12];
			if (!instance || !Read(reinterpret_cast<const void*>(instance), matrix, sizeof(matrix)))
				continue;
			const float position[3] = { matrix[3], matrix[7], matrix[11] };
			if (haveCamera)
			{
				const float dx = position[0] - camera[0], dy = position[1] - camera[1], dz = position[2] - camera[2];
				if (dx * dx + dy * dy + dz * dz > 60.0f * 60.0f)
					continue;
			}
			DEBUG_MENU::WorldCross(drawList, position, 0.2f, IM_COL32(120, 220, 255, 230), 1.5f);
			char label[32];
			snprintf(label, sizeof(label), "map %d", selected);
			ScreenLabel(drawList, position, label, IM_COL32(120, 220, 255, 255), -10.0f);
		}
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

	void SwitchBox(const char* label, const char* name, const char* tooltip)
	{
		bool value = false;
		const bool exists = DEBUG_MENU::GetSwitch(name, value);
		ImGui::BeginDisabled(!exists);
		if (ImGui::Checkbox(label, &value))
			DEBUG_MENU::SetSwitch(name, value);
		ImGui::EndDisabled();
		Tooltip(tooltip);
	}

	void DrawTab()
	{
		Register();
		ImGui::TextWrapped("Why something is or isn't drawn: the zones the renderer treats as visible, one object checked through each step before it is drawn, the level-of-detail distance, and the reflection maps objects use.");
		if (!Engine())
		{
			ImGui::TextColored(ImVec4(1.0f, 0.6f, 0.3f, 1.0f), "Not available on this game release.");
			return;
		}

		if (ImGui::CollapsingHeader("Why isn't this drawn?##visibility", ImGuiTreeNodeFlags_DefaultOpen))
		{
			ImGui::SetNextItemWidth(ImGui::GetFontSize() * 8.0f);
			ImGui::InputInt("Mover index##visibility", &g_mover);
			ImGui::SameLine();
			if (ImGui::Button("Check##visibility"))
				g_diagnosis = Diagnose(g_mover);
			Tooltip("render.why_not_drawn: the object's mover index, as OpenCAGE shows it for a model's instance.");
			ImGui::SameLine();
			if (ImGui::Button("Copy##diagnosis"))
				ImGui::SetClipboardText(g_diagnosis.c_str());
			SwitchBox("No occlusion culling##visibility", "render.no_occlusion_culling", "render.no_occlusion_culling: nothing is left out for being hidden behind something else. If an object appears with this on, occlusion was why it wasn't drawn.");
			ImGui::SameLine();
			SwitchBox("Disable LODs##visibility", "render.disable_lods", "render.disable_lods: everything at its most detailed model at any distance.");
			if (!g_diagnosis.empty())
			{
				ImGui::PushTextWrapPos(0.0f);
				ImGui::TextUnformatted(g_diagnosis.c_str());
				ImGui::PopTextWrapPos();
			}
		}

		if (ImGui::CollapsingHeader("Level of detail##visibility", ImGuiTreeNodeFlags_DefaultOpen))
		{
			const Lod lod = ReadLod();
			if (!lod.have)
				ImGui::TextDisabled("Not readable (not available on this game release?).");
			else
			{
				const float product = lod.frame * lod.settings * lod.scripted;
				if (lod.disabled)
					ImGui::Text("Range factor: level of detail disabled (everything at its most detailed)");
				else
					ImGui::Text("Range factor %.3f = per-frame %.3f x settings %.3f x scripted %.3f", product, lod.frame, lod.settings, lod.scripted);
				float factor = g_lodFactor;
				// Unset, the slider shows the game's own per-frame factor in its place
				char unset[48];
				snprintf(unset, sizeof(unset), "the game's: %.2f", lod.frame);
				ImGui::SetNextItemWidth(ImGui::GetFontSize() * 12.0f);
				if (ImGui::SliderFloat("Per-frame factor##lod", &factor, 0.0f, 8.0f, factor > 0.0f ? "%.2f" : unset))
					g_lodFactor = (std::max)(0.0f, factor);
				Tooltip("render.lod_factor: replaces the game's per-frame factor (0: the game's). Below 1 models switch to their less detailed versions nearer, above 1 further away.");
				ImGui::SameLine();
				ImGui::BeginDisabled(factor <= 0.0f);
				if (ImGui::SmallButton("Reset##lod"))
					g_lodFactor = 0.0f;
				ImGui::EndDisabled();
				Tooltip("Back to the game's own per-frame factor.");
			}
		}

		if (ImGui::CollapsingHeader("Visible zones##visibility"))
		{
			std::vector<Zone> zones;
			int viewIndex = -1;
			if (!ReadZones(zones, viewIndex))
				ImGui::TextDisabled("No level running.");
			else
			{
				int visible = 0, loaded = 0;
				for (const Zone& zone : zones)
				{
					visible += zone.visible ? 1 : 0;
					loaded += (zone.state & 1) ? 1 : 0;
				}
				ImGui::Text("%d zones, %d loaded, %d visible from the main view (view %d)", static_cast<int>(zones.size()), loaded, visible, viewIndex);
				// Scrolls sideways (the zone column kept in sight) rather than squeezing long names
				if (ImGui::BeginTable("zones##visibility", 5, ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_SizingFixedFit | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY, ImVec2(0.0f, ImGui::GetTextLineHeightWithSpacing() * 14.0f)))
				{
					ImGui::TableSetupScrollFreeze(1, 1);
					ImGui::TableSetupColumn("Zone");
					ImGui::TableSetupColumn("Visible");
					ImGui::TableSetupColumn("Loaded");
					ImGui::TableSetupColumn("Priority");
					ImGui::TableSetupColumn("Name");
					ImGui::TableHeadersRow();
					for (const Zone& zone : zones)
					{
						ImGui::TableNextRow();
						ImGui::TableNextColumn();
						ImGui::Text("%08X", zone.id);
						ImGui::TableNextColumn();
						if (zone.visible)
							ImGui::TextColored(ImVec4(0.5f, 1.0f, 0.5f, 1.0f), "visible");
						else
							ImGui::TextDisabled("-");
						ImGui::TableNextColumn();
						ImGui::TextUnformatted((zone.state & 1) ? "loaded" : "-");
						ImGui::TableNextColumn();
						ImGui::Text("%d", zone.priority);
						ImGui::TableNextColumn();
						ImGui::TextUnformatted(zone.name.c_str());
					}
					ImGui::EndTable();
				}
			}
		}

		if (ImGui::CollapsingHeader("Reflection maps##visibility"))
		{
			std::vector<Map> maps;
			uint32_t objects = 0;
			if (!ReadMaps(maps, objects))
				ImGui::TextDisabled("Not readable.");
			else
			{
				ImGui::Text("%d reflection maps, %u objects given one", static_cast<int>(maps.size()), objects);
				bool overlay = g_mapOverlay;
				if (ImGui::Checkbox("Overlay the selected map's objects##envmap", &overlay))
					g_mapOverlay = overlay;
				Tooltip("render.envmap.overlay: a cross over each object (within 60 m) that uses the map selected below (render.envmap.selected).");
				const int selected = static_cast<int>(std::lround(static_cast<float>(g_selectedMap)));
				// Scrolls sideways (the map's number kept in sight) rather than squeezing long texture names
				if (ImGui::BeginTable("maps##visibility", 5, ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_SizingFixedFit | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY, ImVec2(0.0f, ImGui::GetTextLineHeightWithSpacing() * 12.0f)))
				{
					ImGui::TableSetupScrollFreeze(1, 1);
					ImGui::TableSetupColumn("#");
					ImGui::TableSetupColumn("Objects");
					ImGui::TableSetupColumn("Priority");
					ImGui::TableSetupColumn("Colour");
					ImGui::TableSetupColumn("Texture");
					ImGui::TableHeadersRow();
					for (int i = 0; i < static_cast<int>(maps.size()); i++)
					{
						const Map& map = maps[i];
						ImGui::TableNextRow();
						ImGui::TableNextColumn();
						char number[32];
						snprintf(number, sizeof(number), "%d##map%d", i, i);
						if (ImGui::Selectable(number, i == selected, ImGuiSelectableFlags_SpanAllColumns))
							g_selectedMap = i == selected ? -1.0f : static_cast<float>(i);
						ImGui::TableNextColumn();
						ImGui::Text("%u", map.instances);
						ImGui::TableNextColumn();
						ImGui::Text("%u", map.priority);
						ImGui::TableNextColumn();
						ImGui::Text("%.2f %.2f %.2f %.2f", map.colour[0], map.colour[1], map.colour[2], map.colour[3]);
						ImGui::TableNextColumn();
						ImGui::Text("%s%s", map.texture.c_str(), map.needsBake ? " (needs baking)" : "");
					}
					ImGui::EndTable();
				}
			}
		}
	}

	void AttachHooks(bool attach)
	{
		if (attach)
			return;
		// The game's level-of-detail constant put back as the game closes
		g_lodFactor = 0.0f;
		ApplyLod();
	}

	const bool g_tabAdded = DEBUG_MENU::AddTab("Visibility", 76, DrawTab, "Rendering");
	const bool g_overlayAdded = DEBUG_MENU::AddOverlay(42, Overlay);
	const bool g_hooksAdded = DEBUG_MENU::AddHooks(AttachHooks);
	const bool g_namesAdded = (Register(), true);
}

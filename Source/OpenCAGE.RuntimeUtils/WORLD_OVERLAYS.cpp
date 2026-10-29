#include "WORLD_DEBUG.h"
#include "DEBUG_MENU.h"
#include "LIVE_LINK.h"

#include <algorithm>
#include <atomic>
#include <cfloat>
#include <cmath>
#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <imgui.h>

/*
	Addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).

	The World group's Overlays tab and what it draws over the game, a switch each (world.overlay.*): the player's trigger
	and use-trigger boxes (flashing as they fire), splines, doors with their state and the zone link they open, and the
	interaction points the game's pickup handling keeps (items, weapons, ammo, doors, computers, save points). Each is
	drawn out to a set distance from the camera, nearest first up to a cap, with labels nearer still. The boxes, doors
	and splines come from the walk over the level's entities in WORLD_ENTITIES.cpp; the interaction points are read each
	frame from the pickup handling's own table.

	There is no depth test (the scene's depth is not known when the overlay draws): what is behind a wall is drawn too,
	so the distances keep it readable.
*/
namespace
{
	// ---- The pickup handling's table ----

	// The pickup handling (a pointer to it): 1024 slots from +0x3C, 0x9C bytes each - the name shown (64 characters), where
	// it is, the entity it is (two ids), how many, flag bits (bit 2: the slot holds something, bit 3: being used, bit 4: locked; second byte
	// bit 1: a weapon) and its category
	constexpr uintptr_t kPickupManager = 0x012F0DE8;
	constexpr uint32_t kSlots = 0x3C;
	constexpr int kSlotCount = 1024;
	constexpr uint32_t kSlotSize = 0x9C;
	constexpr uint32_t kSlotName = 0x00;
	constexpr uint32_t kSlotNameLength = 0x40;
	constexpr uint32_t kSlotLocation = 0x40;
	constexpr uint32_t kSlotEntity = 0x58;
	constexpr uint32_t kSlotQuantity = 0x80;
	constexpr uint32_t kSlotFlags = 0x96;
	constexpr uint32_t kSlotCategory = 0x99;
	constexpr uint8_t kFlagInViewRange = 0x01;
	constexpr uint8_t kFlagPopulated = 0x04;
	constexpr uint8_t kFlagInUse = 0x08;
	constexpr uint8_t kFlagLocked = 0x10;

	struct Pickup
	{
		char name[kSlotNameLength + 1];
		float location[3];
		uint32_t entity[2];
		int quantity;
		uint8_t flags;
		uint8_t category;   // the game's
		int shown;          // as shown (see ShownCategory)
		int slot;
	};

	// The game's own category names (0: "unknown", which most prompts have: doors, levers, lifts, containers...)
	const char* CategoryName(int category)
	{
		static const char* const kNames[] = { "unknown", "weapon", "ammo", "item", "kill trap", "door", "computer", "alien", "save point" };
		return category >= 0 && category < 9 ? kNames[category] : "?";
	}

	// What an interaction point is, as shown: its category, or for one with none what its prompt's name says (the name
	// is the prompt's text key, such as AI_UI_DOOR_LOCKED), else "interaction"
	int ShownCategory(const char* name, int category)
	{
		if (category != 0 || !name[0])
			return category;
		struct Word { const char* word; int category; };
		static const Word kWords[] = { { "DOOR", 5 }, { "WEAPON", 1 }, { "AMMO", 2 }, { "ITEM", 3 }, { "SAVE", 8 }, { "COMPUTER", 6 }, { "TERMINAL", 6 } };
		for (const Word& word : kWords)
			if (strstr(name, word.word))
				return word.category;
		return 0;
	}

	const char* ShownCategoryName(int category)
	{
		return category == 0 ? "interaction" : CategoryName(category);
	}

	ImU32 CategoryColour(int category)
	{
		switch (category)
		{
		case 1: return IM_COL32(255, 120, 90, 255);
		case 2: return IM_COL32(255, 190, 90, 255);
		case 3: return IM_COL32(120, 220, 255, 255);
		case 4: return IM_COL32(255, 90, 200, 255);
		case 5: return IM_COL32(200, 200, 120, 255);
		case 6: return IM_COL32(120, 255, 170, 255);
		case 8: return IM_COL32(255, 255, 255, 255);
		default: return IM_COL32(190, 190, 190, 255);
		}
	}

	// The slots in use, copied (plain data, guarded)
	int ReadPickups(Pickup* out, int max)
	{
		int found = 0;
		const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kPickupManager);
		if (!at)
			return 0;
		__try
		{
			const uint8_t* manager = *reinterpret_cast<const uint8_t* const*>(at);
			if (!manager)
				return 0;
			for (int i = 0; i < kSlotCount && found < max; i++)
			{
				const uint8_t* slot = manager + kSlots + i * kSlotSize;
				const uint8_t flags = slot[kSlotFlags];
				if (!(flags & kFlagPopulated))
					continue;
				Pickup& pickup = out[found];
				memcpy(pickup.name, slot + kSlotName, kSlotNameLength);
				pickup.name[kSlotNameLength] = '\0';
				memcpy(pickup.location, slot + kSlotLocation, sizeof(pickup.location));
				memcpy(pickup.entity, slot + kSlotEntity, sizeof(pickup.entity));
				memcpy(&pickup.quantity, slot + kSlotQuantity, sizeof(pickup.quantity));
				pickup.flags = flags;
				pickup.category = slot[kSlotCategory];
				pickup.shown = ShownCategory(pickup.name, pickup.category);
				pickup.slot = i;
				found++;
			}
			return found;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return found;
		}
	}

	bool PickupsAvailable()
	{
		return DEVTOOLS_RELATIVE_ADDRESS(kPickupManager) != 0;
	}

	// ---- Switches and values ----

	std::atomic<bool> g_triggers = false, g_useTriggers = false, g_splines = false, g_doors = false, g_pickups = false;
	std::atomic<bool> g_labels = true;
	std::atomic<bool> g_onlyEnabled = false;
	std::atomic<float> g_distance = 25.0f;
	std::atomic<float> g_labelDistance = 12.0f;
	constexpr int kMaxDrawn = 400;
	constexpr double kFlashSeconds = 1.5;

	bool AnyEntityOverlay()
	{
		return g_triggers || g_useTriggers || g_splines || g_doors;
	}

	// ---- Helpers ----

	std::string Format(const char* format, ...)
	{
		char text[512];
		va_list args;
		va_start(args, format);
		vsnprintf(text, sizeof(text), format, args);
		va_end(args);
		return text;
	}

	float Distance(const float a[3], const float b[3])
	{
		const float dx = a[0] - b[0], dy = a[1] - b[1], dz = a[2] - b[2];
		return std::sqrt(dx * dx + dy * dy + dz * dz);
	}

	void Translation(const float m[12], float out[3])
	{
		out[0] = m[3];
		out[1] = m[7];
		out[2] = m[11];
	}

	// The game's 3x4 (x' = m0 x + m1 y + m2 z + m3 ...) as the debug menu's box takes it: its axes as rows, then its centre
	void BoxMatrix(const float m[12], float out[16])
	{
		for (int axis = 0; axis < 3; axis++)
		{
			out[axis * 4 + 0] = m[axis];
			out[axis * 4 + 1] = m[4 + axis];
			out[axis * 4 + 2] = m[8 + axis];
			out[axis * 4 + 3] = 0.0f;
		}
		out[12] = m[3];
		out[13] = m[7];
		out[14] = m[11];
		out[15] = 1.0f;
	}

	ImU32 Faded(ImU32 colour, float distance, float range)
	{
		// Further away, fainter (down to a third)
		const float t = range > 0.0f ? (std::min)(1.0f, distance / range) : 0.0f;
		const float alpha = static_cast<float>((colour >> IM_COL32_A_SHIFT) & 0xFF) * (1.0f - 0.66f * t);
		return (colour & ~IM_COL32_A_MASK) | (static_cast<ImU32>(alpha) << IM_COL32_A_SHIFT);
	}

	// Text over a point as DEBUG_MENU::WorldText draws it, but kept on the screen: a label whose point is on screen is
	// moved in from the edges rather than cut off (a point off screen or behind the camera gets none)
	void Label(ImDrawList* drawList, const float world[3], const char* text, ImU32 colour, float yOffsetPixels = 0.0f)
	{
		ImVec2 at;
		if (!DEBUG_MENU::Project(world, at))
			return;
		const ImVec2 screen = ImGui::GetIO().DisplaySize;
		if (at.x < 0.0f || at.y < 0.0f || at.x > screen.x || at.y > screen.y)
			return;
		const float size = 15.0f * DEBUG_MENU::Scale();
		const ImVec2 extent = ImGui::GetFont()->CalcTextSizeA(size, FLT_MAX, 0.0f, text);
		ImVec2 corner(at.x - extent.x * 0.5f, at.y - extent.y + yOffsetPixels);
		corner.x = (std::max)(4.0f, (std::min)(corner.x, screen.x - extent.x - 4.0f));
		corner.y = (std::max)(2.0f, (std::min)(corner.y, screen.y - extent.y - 2.0f));
		drawList->AddRectFilled(ImVec2(corner.x - 4.0f, corner.y - 2.0f), ImVec2(corner.x + extent.x + 4.0f, corner.y + extent.y + 2.0f), IM_COL32(0, 0, 0, 150), 3.0f);
		drawList->AddText(ImGui::GetFont(), size, corner, colour, text);
	}

	// A trigger box as drawn: the game's matrix as the debug menu's box takes it, its centre half its height up its own up
	// axis (the game stands the box on its position)
	void TriggerBox(const WORLD_DEBUG::WorldEntity& entity, float matrix[16])
	{
		BoxMatrix(entity.matrix, matrix);
		for (int axis = 0; axis < 3; axis++)
			matrix[12 + axis] += matrix[4 + axis] * entity.halfSize[1];
	}

	// How far a point is from a trigger box (0 inside it)
	float BoxDistance(const WORLD_DEBUG::WorldEntity& entity, const float point[3])
	{
		float matrix[16];
		TriggerBox(entity, matrix);
		const float offset[3] = { point[0] - matrix[12], point[1] - matrix[13], point[2] - matrix[14] };
		float outside = 0.0f;
		for (int axis = 0; axis < 3; axis++)
		{
			const float along = offset[0] * matrix[axis * 4] + offset[1] * matrix[axis * 4 + 1] + offset[2] * matrix[axis * 4 + 2];
			const float beyond = std::fabs(along) - entity.halfSize[axis];
			if (beyond > 0.0f)
				outside += beyond * beyond;
		}
		return std::sqrt(outside);
	}

	// How far the camera is from an entity: a spline's nearest point, a trigger box's nearest side, else its position
	float EntityDistance(const WORLD_DEBUG::WorldEntity& entity, const float camera[3])
	{
		if (entity.kind == WORLD_DEBUG::Kind::Spline)
		{
			float distance = FLT_MAX;
			for (size_t p = 0; p + 2 < entity.points.size(); p += 3)
				distance = (std::min)(distance, Distance(camera, &entity.points[p]));
			return distance;
		}
		if ((entity.kind == WORLD_DEBUG::Kind::Trigger || entity.kind == WORLD_DEBUG::Kind::UseTrigger) &&
			(entity.halfSize[0] > 0.0f || entity.halfSize[1] > 0.0f || entity.halfSize[2] > 0.0f))
			return BoxDistance(entity, camera);
		float at[3];
		Translation(entity.matrix, at);
		return Distance(camera, at);
	}

	// Where a trigger box's label goes: its centre when that is on screen, else the first of its top's middle and its
	// corners that is (a big box with the camera inside it still gets its label)
	bool TriggerLabelPoint(const float matrix[16], const float half[3], float out[3])
	{
		const ImVec2 screen = ImGui::GetIO().DisplaySize;
		const float signs[10][3] = { { 0, 0, 0 }, { 0, 1, 0 }, { -1, -1, -1 }, { 1, -1, -1 }, { -1, -1, 1 }, { 1, -1, 1 }, { -1, 1, -1 }, { 1, 1, -1 }, { -1, 1, 1 }, { 1, 1, 1 } };
		for (const auto& sign : signs)
		{
			float point[3];
			for (int i = 0; i < 3; i++)
				point[i] = matrix[12 + i] + matrix[i] * sign[0] * half[0] + matrix[4 + i] * sign[1] * half[1] + matrix[8 + i] * sign[2] * half[2];
			ImVec2 at;
			if (DEBUG_MENU::Project(point, at) && at.x >= 0.0f && at.y >= 0.0f && at.x <= screen.x && at.y <= screen.y)
			{
				memcpy(out, point, sizeof(point));
				return true;
			}
		}
		return false;
	}

	// A collision row and its zone, when it has one
	std::string RowZoneText(int row, uint32_t zone)
	{
		if (zone == 0 || zone == WORLD_DEBUG::kNoZone)
			return Format("collision row %d", row);
		return Format("collision row %d, zone %s", row, WORLD_DEBUG::ZoneName(zone).c_str());
	}

	std::string StateText(uint32_t state)
	{
		std::string text;
		auto add = [&text](bool on, const char* word) { if (on) text += (text.empty() ? "" : ", ") + std::string(word); };
		add((state & WORLD_DEBUG::kStateEnabled) != 0, "enabled");
		add(!(state & WORLD_DEBUG::kStateEnabled), "disabled");
		add((state & WORLD_DEBUG::kStateLocked) != 0, "locked");
		add((state & WORLD_DEBUG::kStateSuspended) != 0, "suspended");
		add(!(state & WORLD_DEBUG::kStateZoneActive), "zone not active");
		return text;
	}

	std::string DoorText(uint32_t state)
	{
		if (state & WORLD_DEBUG::kStateOpening)
			return "opening";
		if (state & WORLD_DEBUG::kStateClosing)
			return "closing";
		if (state & WORLD_DEBUG::kStateOpened)
			return "open";
		if (state & WORLD_DEBUG::kStateClosed)
			return "closed";
		return "-";
	}

	ImU32 DoorColour(uint32_t state)
	{
		if (state & WORLD_DEBUG::kStateLocked)
			return IM_COL32(255, 80, 70, 255);
		if (state & (WORLD_DEBUG::kStateOpening | WORLD_DEBUG::kStateClosing))
			return IM_COL32(110, 220, 255, 255);
		if (state & WORLD_DEBUG::kStateOpened)
			return IM_COL32(110, 240, 120, 255);
		return IM_COL32(255, 210, 90, 255);
	}

	std::string LinkText(const void* zoneLink)
	{
		uint32_t a = 0, b = 0, state = 0;
		if (!WORLD_DEBUG::LinkState(zoneLink, a, b, state))
			return "no zone link";
		return Format("link %s - %s %s%s", WORLD_DEBUG::ZoneName(a).c_str(), WORLD_DEBUG::ZoneName(b).c_str(), (state & 1) ? "open" : "closed", (state & 2) ? ", locked" : "");
	}

	// ---- Drawn over the game ----

	struct Near
	{
		const WORLD_DEBUG::WorldEntity* entity;
		float distance;
	};

	std::vector<Near> Nearest(const WORLD_DEBUG::WorldScan& scan, const float camera[3], float range, bool (*wanted)(WORLD_DEBUG::Kind))
	{
		std::vector<Near> nearby;
		for (const WORLD_DEBUG::WorldEntity& entity : scan.entities)
		{
			if (!entity.placed || !wanted(entity.kind))
				continue;
			const float distance = EntityDistance(entity, camera);
			if (distance <= range)
				nearby.push_back({ &entity, distance });
		}
		std::sort(nearby.begin(), nearby.end(), [](const Near& a, const Near& b) { return a.distance < b.distance; });
		if (nearby.size() > kMaxDrawn)
			nearby.resize(kMaxDrawn);
		return nearby;
	}

	bool WantedKind(WORLD_DEBUG::Kind kind)
	{
		switch (kind)
		{
		case WORLD_DEBUG::Kind::Trigger: return g_triggers;
		case WORLD_DEBUG::Kind::UseTrigger: return g_useTriggers;
		case WORLD_DEBUG::Kind::Door: return g_doors;
		case WORLD_DEBUG::Kind::Spline: return g_splines;
		default: return false;
		}
	}

	void DrawTrigger(ImDrawList* drawList, const WORLD_DEBUG::WorldEntity& entity, float distance, float range, bool label)
	{
		uint32_t state = 0;
		const bool live = WORLD_DEBUG::LiveState(entity, state);
		const bool enabled = live && (state & WORLD_DEBUG::kStateEnabled);
		if (g_onlyEnabled && !enabled)
			return;
		double entered = -1.0, exited = -1.0;
		WORLD_DEBUG::LastFired(entity, entered, exited);
		ImU32 colour = entity.kind == WORLD_DEBUG::Kind::Trigger ? IM_COL32(90, 230, 130, 230) : IM_COL32(90, 200, 255, 230);
		if (!enabled)
			colour = IM_COL32(150, 150, 150, 170);
		float thickness = 1.5f;
		// A flash as it fires: yellow on entering, orange on leaving, pulsing
		const bool flashEnter = entered >= 0.0 && entered < kFlashSeconds;
		const bool flashExit = exited >= 0.0 && exited < kFlashSeconds && (!flashEnter || exited < entered);
		if (flashEnter || flashExit)
		{
			const double ago = flashExit ? exited : entered;
			const float pulse = 0.5f + 0.5f * std::cos(static_cast<float>(ago) * 12.0f);
			colour = flashExit ? IM_COL32(255, 150, 60, 255) : IM_COL32(255, 240, 80, 255);
			thickness = 2.0f + 2.0f * pulse;
		}
		colour = Faded(colour, distance, range);
		// The game stands the box on its position: its centre is half its height up its own up axis
		float matrix[16];
		TriggerBox(entity, matrix);
		float centre[3] = { matrix[12], matrix[13], matrix[14] };
		const bool sized = entity.halfSize[0] > 0.0f || entity.halfSize[1] > 0.0f || entity.halfSize[2] > 0.0f;
		if (sized)
			DEBUG_MENU::WorldOrientedBox(drawList, matrix, entity.halfSize, colour, thickness);
		else
			DEBUG_MENU::WorldCross(drawList, centre, 0.3f, colour, thickness);
		if (!label)
			return;
		if (sized && !TriggerLabelPoint(matrix, entity.halfSize, centre))
			return;
		std::string text = Format("%s (%s)", WORLD_DEBUG::KindName(entity.kind), entity.composite.c_str());
		if (live)
			text += "\n" + StateText(state);
		if (entity.hullRow >= 0)
			text += "\n" + RowZoneText(entity.hullRow, entity.zone);
		if (entered >= 0.0)
			text += Format("\nentered %.1f s ago", entered);
		if (exited >= 0.0)
			text += Format("%sleft %.1f s ago", entered >= 0.0 ? ", " : "\n", exited);
		Label(drawList, centre, text.c_str(), colour | IM_COL32_A_MASK);
	}

	void DrawDoor(ImDrawList* drawList, const WORLD_DEBUG::WorldEntity& entity, float distance, float range, bool label)
	{
		uint32_t state = 0;
		const bool live = WORLD_DEBUG::LiveState(entity, state);
		const ImU32 colour = Faded(live ? DoorColour(state) : IM_COL32(150, 150, 150, 200), distance, range);
		float base[3], top[3];
		Translation(entity.matrix, base);
		memcpy(top, base, sizeof(top));
		top[1] += 2.2f;
		DEBUG_MENU::WorldLine(drawList, base, top, colour, 2.5f);
		const float half[3] = { 0.15f, 0.15f, 0.15f };
		const float mid[3] = { base[0], base[1] + 1.1f, base[2] };
		const float lo[3] = { mid[0] - half[0], mid[1] - half[1], mid[2] - half[2] };
		const float hi[3] = { mid[0] + half[0], mid[1] + half[1], mid[2] + half[2] };
		DEBUG_MENU::WorldBox(drawList, lo, hi, colour, 2.0f);
		if (!label)
			return;
		std::string text = Format("door (%s): %s", entity.composite.c_str(), live ? DoorText(state).c_str() : "gone");
		if (live && (state & WORLD_DEBUG::kStateLocked))
			text += ", locked";
		if (entity.scripted)
			text += ", scripted";
		if (entity.zoneLink)
			text += "\n" + LinkText(entity.zoneLink);
		Label(drawList, mid, text.c_str(), colour | IM_COL32_A_MASK, -10.0f);
	}

	void DrawSpline(ImDrawList* drawList, const WORLD_DEBUG::WorldEntity& entity, float distance, float range, bool label)
	{
		const ImU32 colour = Faded(IM_COL32(200, 140, 255, 230), distance, range);
		const size_t count = entity.points.size() / 3;
		for (size_t p = 0; p + 1 < count; p++)
			DEBUG_MENU::WorldLine(drawList, &entity.points[p * 3], &entity.points[(p + 1) * 3], colour, 2.0f);
		if (entity.closed && count > 2)
			DEBUG_MENU::WorldLine(drawList, &entity.points[(count - 1) * 3], &entity.points[0], colour, 2.0f);
		for (size_t p = 0; p < count; p++)
			DEBUG_MENU::WorldCross(drawList, &entity.points[p * 3], 0.08f, colour, 1.5f);
		if (count && count > 1)
			DEBUG_MENU::WorldArrow(drawList, &entity.points[0], &entity.points[3], colour, 2.5f);
		if (label && count)
		{
			const std::string text = Format("spline (%s): %d points%s", entity.composite.c_str(), static_cast<int>(count), entity.closed ? ", closed" : "");
			Label(drawList, &entity.points[0], text.c_str(), colour | IM_COL32_A_MASK, -8.0f);
		}
	}

	void DrawPickups(ImDrawList* drawList, const float camera[3], float range, float labelRange)
	{
		static Pickup pickups[kSlotCount];
		const int count = ReadPickups(pickups, kSlotCount);
		std::vector<std::pair<float, int>> nearby;
		for (int i = 0; i < count; i++)
		{
			const float distance = Distance(camera, pickups[i].location);
			if (distance <= range)
				nearby.push_back({ distance, i });
		}
		std::sort(nearby.begin(), nearby.end());
		if (nearby.size() > kMaxDrawn)
			nearby.resize(kMaxDrawn);
		for (const auto& item : nearby)
		{
			const Pickup& pickup = pickups[item.second];
			const ImU32 colour = Faded(CategoryColour(pickup.shown), item.first, range);
			DEBUG_MENU::WorldCross(drawList, pickup.location, 0.15f, colour, 2.0f);
			if (!g_labels || item.first > labelRange)
				continue;
			std::string text = Format("%s (%s)", pickup.name[0] ? pickup.name : "unnamed", ShownCategoryName(pickup.shown));
			if (pickup.quantity > 1)
				text += Format(" x%d", pickup.quantity);
			if (pickup.flags & kFlagLocked)
				text += ", locked";
			if (pickup.flags & kFlagInUse)
				text += ", in use";
			Label(drawList, pickup.location, text.c_str(), colour | IM_COL32_A_MASK, -6.0f);
		}
	}

	void DrawOverlay(ImDrawList* drawList)
	{
		if (!AnyEntityOverlay() && !g_pickups)
			return;
		float camera[3];
		if (!DEBUG_MENU::CameraPosition(camera))
			return;
		const float range = g_distance;
		const float labelRange = (std::min)(range, g_labelDistance.load());
		if (AnyEntityOverlay())
		{
			WORLD_DEBUG::WantScan();
			const std::shared_ptr<const WORLD_DEBUG::WorldScan> scan = WORLD_DEBUG::Scan();
			for (const Near& item : Nearest(*scan, camera, range, WantedKind))
			{
				const bool label = g_labels && item.distance <= labelRange;
				switch (item.entity->kind)
				{
				case WORLD_DEBUG::Kind::Trigger:
				case WORLD_DEBUG::Kind::UseTrigger:
					DrawTrigger(drawList, *item.entity, item.distance, range, label);
					break;
				case WORLD_DEBUG::Kind::Door:
					DrawDoor(drawList, *item.entity, item.distance, range, label);
					break;
				case WORLD_DEBUG::Kind::Spline:
					DrawSpline(drawList, *item.entity, item.distance, range, label);
					break;
				}
			}
		}
		if (g_pickups)
			DrawPickups(drawList, camera, range, labelRange);
	}

	// ---- The tab ----

	void Switch(const char* label, std::atomic<bool>& value)
	{
		bool on = value;
		if (ImGui::Checkbox(label, &on))
			value = on;
	}

	void Slider(const char* label, std::atomic<float>& value, float min, float max, const char* format)
	{
		float v = value;
		ImGui::SetNextItemWidth(200.0f * DEBUG_MENU::Scale());
		if (ImGui::SliderFloat(label, &v, min, max, format, ImGuiSliderFlags_AlwaysClamp))
			value = v;
	}

	int g_listKind = 0;

	void DrawList(const WORLD_DEBUG::WorldScan& scan, const float camera[3], bool haveCamera)
	{
		static const char* const kKinds[] = { "Trigger boxes", "Use trigger boxes", "Doors", "Splines", "Interaction points" };
		ImGui::SetNextItemWidth(200.0f * DEBUG_MENU::Scale());
		ImGui::Combo("##listkind", &g_listKind, kKinds, 5);
		ImGui::SameLine();
		ImGui::PushTextWrapPos(0.0f);
		ImGui::TextDisabled("nearest the camera first, out to the distance above");
		ImGui::PopTextWrapPos();
		const float range = g_distance;
		// 14 rows, the header and the sideways scroll bar; the id before the long state text (which is what scrolls sideways),
		// always wide enough for the whole id
		const float rowHeight = ImGui::GetTextLineHeight() + ImGui::GetStyle().CellPadding.y * 2.0f;
		const ImGuiTableFlags flags = ImGuiTableFlags_RowBg | ImGuiTableFlags_BordersInnerV | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY | ImGuiTableFlags_SizingFixedFit;
		if (!ImGui::BeginTable("worldlist", 4, flags, ImVec2(0.0f, rowHeight * 15.0f + ImGui::GetStyle().ScrollbarSize + 4.0f)))
			return;
		ImGui::TableSetupScrollFreeze(0, 1);
		ImGui::TableSetupColumn("Distance");
		ImGui::TableSetupColumn("What");
		ImGui::TableSetupColumn("Id", ImGuiTableColumnFlags_WidthFixed | ImGuiTableColumnFlags_NoHide, ImGui::CalcTextSize(g_listKind == 4 ? "00000000 in 00000000" : "00000000").x);
		ImGui::TableSetupColumn("State");
		ImGui::TableHeadersRow();
		if (g_listKind == 4)
		{
			static Pickup pickups[kSlotCount];
			const int count = ReadPickups(pickups, kSlotCount);
			std::vector<std::pair<float, int>> nearby;
			for (int i = 0; i < count; i++)
			{
				const float distance = haveCamera ? Distance(camera, pickups[i].location) : 0.0f;
				if (distance <= range)
					nearby.push_back({ distance, i });
			}
			std::sort(nearby.begin(), nearby.end());
			for (const auto& item : nearby)
			{
				const Pickup& pickup = pickups[item.second];
				ImGui::TableNextRow();
				ImGui::TableSetColumnIndex(0);
				ImGui::Text("%.1f m", item.first);
				ImGui::TableSetColumnIndex(1);
				ImGui::TextColored(ImGui::ColorConvertU32ToFloat4(CategoryColour(pickup.shown)), "%s (%s)", pickup.name[0] ? pickup.name : "unnamed", ShownCategoryName(pickup.shown));
				ImGui::TableSetColumnIndex(2);
				ImGui::Text("%08X in %08X", pickup.entity[0], pickup.entity[1]);
				ImGui::TableSetColumnIndex(3);
				ImGui::Text("x%d%s%s", pickup.quantity, (pickup.flags & kFlagLocked) ? ", locked" : "", (pickup.flags & kFlagInUse) ? ", in use" : "");
			}
			ImGui::EndTable();
			return;
		}
		const WORLD_DEBUG::Kind kinds[] = { WORLD_DEBUG::Kind::Trigger, WORLD_DEBUG::Kind::UseTrigger, WORLD_DEBUG::Kind::Door, WORLD_DEBUG::Kind::Spline };
		const WORLD_DEBUG::Kind kind = kinds[g_listKind];
		std::vector<Near> nearby;
		for (const WORLD_DEBUG::WorldEntity& entity : scan.entities)
		{
			if (entity.kind != kind || !entity.placed)
				continue;
			const float distance = haveCamera ? EntityDistance(entity, camera) : 0.0f;
			if (distance <= range)
				nearby.push_back({ &entity, distance });
		}
		std::sort(nearby.begin(), nearby.end(), [](const Near& a, const Near& b) { return a.distance < b.distance; });
		for (const Near& item : nearby)
		{
			const WORLD_DEBUG::WorldEntity& entity = *item.entity;
			uint32_t state = 0;
			const bool live = WORLD_DEBUG::LiveState(entity, state);
			ImGui::TableNextRow();
			ImGui::TableSetColumnIndex(0);
			ImGui::Text("%.1f m", item.distance);
			ImGui::TableSetColumnIndex(1);
			ImGui::TextUnformatted(entity.composite.c_str());
			ImGui::TableSetColumnIndex(2);
			ImGui::Text("%08X", entity.guid);
			ImGui::TableSetColumnIndex(3);
			if (!live)
				ImGui::TextDisabled("gone");
			else if (kind == WORLD_DEBUG::Kind::Door)
				ImGui::Text("%s%s%s; %s", DoorText(state).c_str(), (state & WORLD_DEBUG::kStateLocked) ? ", locked" : "", entity.scripted ? ", scripted" : "", LinkText(entity.zoneLink).c_str());
			else if (kind == WORLD_DEBUG::Kind::Spline)
				ImGui::Text("%d points%s", static_cast<int>(entity.points.size() / 3), entity.closed ? ", closed" : "");
			else
			{
				double entered = -1.0, exited = -1.0;
				WORLD_DEBUG::LastFired(entity, entered, exited);
				std::string text = StateText(state) + Format("; %.1f x %.1f x %.1f m", entity.halfSize[0] * 2.0f, entity.halfSize[1] * 2.0f, entity.halfSize[2] * 2.0f);
				if (entity.hullRow >= 0)
					text += "; " + RowZoneText(entity.hullRow, entity.zone);
				if (entered >= 0.0)
					text += Format("; entered %.1f s ago", entered);
				ImGui::TextUnformatted(text.c_str());
			}
		}
		ImGui::EndTable();
	}

	void DrawTab()
	{
		ImGui::SeparatorText("Draw over the game");
		Switch("Trigger boxes (the player's)", g_triggers);
		ImGui::SameLine();
		Switch("Use trigger boxes", g_useTriggers);
		Switch("Doors, with their zone link", g_doors);
		ImGui::SameLine();
		Switch("Splines", g_splines);
		ImGui::SameLine();
		Switch("Interaction points (pickups, doors, computers)", g_pickups);
		Switch("Labels", g_labels);
		ImGui::SameLine();
		Switch("Only enabled boxes", g_onlyEnabled);
		Slider("Distance##world", g_distance, 2.0f, 200.0f, "%.0f m");
		Slider("Labels within##world", g_labelDistance, 1.0f, 100.0f, "%.0f m");
		ImGui::PushTextWrapPos(0.0f);
		ImGui::TextDisabled("Trigger boxes green, use trigger boxes blue, disabled ones grey; they flash yellow as the player enters them, orange as "
			"it leaves. Doors: yellow closed, green open, cyan opening or closing, red locked. Interaction points: a colour per kind. No depth "
			"test: walls do not hide them.");
		if (!PickupsAvailable())
			ImGui::TextDisabled("The interaction points are not available on this game release.");
		ImGui::PopTextWrapPos();

		ImGui::SeparatorText("Found in the level");
		// The lists keep the walk going while the tab is shown
		WORLD_DEBUG::WantScan();
		const std::shared_ptr<const WORLD_DEBUG::WorldScan> scan = WORLD_DEBUG::Scan();
		int counts[4] = {};
		for (const WORLD_DEBUG::WorldEntity& entity : scan->entities)
			counts[static_cast<int>(entity.kind)]++;
		ImGui::PushTextWrapPos(0.0f);
		if (!scan->available)
			ImGui::TextDisabled("The level's entities cannot be walked on this game release.");
		else if (!scan->generation)
			ImGui::TextDisabled("Looking (the level's entities are walked between script frames)...");
		else
		{
			// The walk's time on the same line when it fits, else under it whole (never split as "5.4" / "ms)")
			const std::string found = Format("%d trigger boxes, %d use trigger boxes, %d doors, %d splines among %d entities", counts[0], counts[1], counts[2], counts[3], scan->walked);
			const std::string walk = Format("(walk %.1f ms%s)", scan->walkMs, scan->unplaced ? Format("; %d still being placed", scan->unplaced).c_str() : "");
			if (ImGui::CalcTextSize((found + " " + walk).c_str()).x <= ImGui::GetContentRegionAvail().x)
				ImGui::TextUnformatted((found + " " + walk).c_str());
			else
			{
				ImGui::TextUnformatted(found.c_str());
				ImGui::TextUnformatted(walk.c_str());
			}
		}
		ImGui::PopTextWrapPos();
		if (ImGui::Button("Look again"))
			WORLD_DEBUG::Rescan();
		float camera[3];
		const bool haveCamera = DEBUG_MENU::CameraPosition(camera);
		DrawList(*scan, camera, haveCamera);
	}

	// ---- Named controls ----

	// Waits (socket thread) for the walk to have been made and every position read, up to three seconds
	std::shared_ptr<const WORLD_DEBUG::WorldScan> ScanNow()
	{
		std::shared_ptr<const WORLD_DEBUG::WorldScan> scan;
		for (int i = 0; i < 60; i++)
		{
			WORLD_DEBUG::WantScan();
			scan = WORLD_DEBUG::Scan();
			if (scan->generation && !scan->unplaced && GetTickCount64() - scan->walkedAt < 6000)
				break;
			Sleep(50);
		}
		return scan;
	}

	DEBUG_MENU::CommandResult ListKind(WORLD_DEBUG::Kind kind, const std::string& argument)
	{
		const float range = argument.empty() ? 1e9f : static_cast<float>(atof(argument.c_str()));
		const std::shared_ptr<const WORLD_DEBUG::WorldScan> scan = ScanNow();
		if (!scan->generation)
			return { false, "The level's entities have not been walked (no level running, or the game is paused)" };
		return DEBUG_MENU::AnswerOnRenderThread([scan, kind, range]() -> DEBUG_MENU::CommandResult
		{
			float camera[3] = {};
			const bool haveCamera = DEBUG_MENU::CameraPosition(camera);
			std::vector<Near> nearby;
			for (const WORLD_DEBUG::WorldEntity& entity : scan->entities)
			{
				if (entity.kind != kind)
					continue;
				const float distance = haveCamera && entity.placed ? EntityDistance(entity, camera) : 0.0f;
				if (distance <= range)
					nearby.push_back({ &entity, distance });
			}
			std::sort(nearby.begin(), nearby.end(), [](const Near& a, const Near& b) { return a.distance < b.distance; });
			std::string text = Format("count=%d\n", static_cast<int>(nearby.size()));
			for (const Near& item : nearby)
			{
				const WORLD_DEBUG::WorldEntity& entity = *item.entity;
				uint32_t state = 0;
				const bool live = WORLD_DEBUG::LiveState(entity, state);
				float at[3];
				Translation(entity.matrix, at);
				text += Format("%s entity=%08X composite=%s distance=%.2f at=%.3f,%.3f,%.3f state=%08X", WORLD_DEBUG::KindName(entity.kind), entity.guid, entity.composite.c_str(),
					item.distance, at[0], at[1], at[2], live ? state : 0u);
				if (!live)
					text += " gone=1";
				if (!entity.placed)
					text += " placed=0";
				if (kind == WORLD_DEBUG::Kind::Trigger || kind == WORLD_DEBUG::Kind::UseTrigger)
				{
					double entered = -1.0, exited = -1.0;
					WORLD_DEBUG::LastFired(entity, entered, exited);
					text += Format(" half_size=%.3f,%.3f,%.3f row=%d zone=%s entered_ago=%.1f exited_ago=%.1f", entity.halfSize[0], entity.halfSize[1], entity.halfSize[2], entity.hullRow,
						WORLD_DEBUG::ZoneName(entity.zone).c_str(), entered, exited);
				}
				else if (kind == WORLD_DEBUG::Kind::Door)
				{
					uint32_t a = 0, b = 0, linkState = 0;
					const bool link = WORLD_DEBUG::LinkState(entity.zoneLink, a, b, linkState);
					text += Format(" door=%s locked=%d scripted=%d", DoorText(state).c_str(), (state & WORLD_DEBUG::kStateLocked) ? 1 : 0, entity.scripted ? 1 : 0);
					if (link)
						text += Format(" link=%s|%s link_open=%d link_locked=%d", WORLD_DEBUG::ZoneName(a).c_str(), WORLD_DEBUG::ZoneName(b).c_str(), (linkState & 1) ? 1 : 0, (linkState & 2) ? 1 : 0);
				}
				else if (kind == WORLD_DEBUG::Kind::Spline)
					text += Format(" points=%d closed=%d", static_cast<int>(entity.points.size() / 3), entity.closed ? 1 : 0);
				text += "\n";
			}
			return { true, text };
		});
	}

	DEBUG_MENU::CommandResult ListPickups(const std::string& argument)
	{
		if (!PickupsAvailable())
			return { false, "The interaction points are not available on this game release" };
		const float range = argument.empty() ? 1e9f : static_cast<float>(atof(argument.c_str()));
		return DEBUG_MENU::AnswerOnRenderThread([range]() -> DEBUG_MENU::CommandResult
		{
			static Pickup pickups[kSlotCount];
			const int count = ReadPickups(pickups, kSlotCount);
			float camera[3] = {};
			const bool haveCamera = DEBUG_MENU::CameraPosition(camera);
			std::vector<std::pair<float, int>> nearby;
			for (int i = 0; i < count; i++)
			{
				const float distance = haveCamera ? Distance(camera, pickups[i].location) : 0.0f;
				if (distance <= range)
					nearby.push_back({ distance, i });
			}
			std::sort(nearby.begin(), nearby.end());
			std::string text = Format("count=%d\n", static_cast<int>(nearby.size()));
			for (const auto& item : nearby)
			{
				const Pickup& pickup = pickups[item.second];
				text += Format("slot=%d name=%s category=%s shown_as=%s quantity=%d distance=%.2f at=%.3f,%.3f,%.3f entity=%08X instance=%08X locked=%d in_use=%d\n", pickup.slot,
					pickup.name, CategoryName(pickup.category), ShownCategoryName(pickup.shown), pickup.quantity, item.first, pickup.location[0], pickup.location[1],
					pickup.location[2], pickup.entity[0], pickup.entity[1], (pickup.flags & kFlagLocked) ? 1 : 0, (pickup.flags & kFlagInUse) ? 1 : 0);
			}
			return { true, text };
		});
	}

	DEBUG_MENU::CommandResult ScanStatus()
	{
		const std::shared_ptr<const WORLD_DEBUG::WorldScan> scan = ScanNow();
		int counts[4] = {};
		for (const WORLD_DEBUG::WorldEntity& entity : scan->entities)
			counts[static_cast<int>(entity.kind)]++;
		return { true, Format("available=%d\ngeneration=%u\nwalked=%d\nwalk_ms=%.2f\nunplaced=%d\ntriggers=%d\nuse_triggers=%d\ndoors=%d\nsplines=%d", scan->available ? 1 : 0,
			scan->generation, scan->walked, scan->walkMs, scan->unplaced, counts[0], counts[1], counts[2], counts[3]) };
	}

	bool RegisterControls()
	{
		DEBUG_MENU::AddSwitch("world.overlay.triggers", &g_triggers);
		DEBUG_MENU::AddSwitch("world.overlay.use_triggers", &g_useTriggers);
		DEBUG_MENU::AddSwitch("world.overlay.doors", &g_doors);
		DEBUG_MENU::AddSwitch("world.overlay.splines", &g_splines);
		DEBUG_MENU::AddSwitch("world.overlay.pickups", &g_pickups);
		DEBUG_MENU::AddSwitch("world.overlay.labels", &g_labels);
		DEBUG_MENU::AddSwitch("world.overlay.only_enabled", &g_onlyEnabled);
		DEBUG_MENU::AddValue("world.overlay.distance", &g_distance, 2.0f, 200.0f);
		DEBUG_MENU::AddValue("world.overlay.label_distance", &g_labelDistance, 1.0f, 100.0f);
		DEBUG_MENU::AddCommand("world.rescan", "walk the level's entities again and read every position again (the Look again button)",
			[](const std::string&) -> DEBUG_MENU::CommandResult { WORLD_DEBUG::Rescan(); return { true, "Walking again" }; });
		DEBUG_MENU::AddCommand("world.scan_status", "how many trigger boxes, doors and splines the walk found, and how long it took",
			[](const std::string&) { return ScanStatus(); });
		DEBUG_MENU::AddCommand("world.list_triggers", "the player's trigger boxes, nearest the camera first (argument: the most metres away)",
			[](const std::string& argument) { return ListKind(WORLD_DEBUG::Kind::Trigger, argument); });
		DEBUG_MENU::AddCommand("world.list_use_triggers", "the use trigger boxes, nearest first (argument: the most metres away)",
			[](const std::string& argument) { return ListKind(WORLD_DEBUG::Kind::UseTrigger, argument); });
		DEBUG_MENU::AddCommand("world.list_doors", "the doors with their state and zone link, nearest first (argument: the most metres away)",
			[](const std::string& argument) { return ListKind(WORLD_DEBUG::Kind::Door, argument); });
		DEBUG_MENU::AddCommand("world.list_splines", "the spline paths, nearest first (argument: the most metres away)",
			[](const std::string& argument) { return ListKind(WORLD_DEBUG::Kind::Spline, argument); });
		DEBUG_MENU::AddCommand("world.list_pickups", "the interaction points in use (pickups, doors, computers...), nearest first (argument: the most metres away)",
			[](const std::string& argument) { return ListPickups(argument); });
		return true;
	}

	const bool g_controls = RegisterControls();
	const bool g_added = DEBUG_MENU::AddTab("Overlays", 115, DrawTab, "World") && DEBUG_MENU::AddOverlay(62, DrawOverlay);
}

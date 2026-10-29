#include "WORLD_DEBUG.h"
#include "DEBUG_MENU.h"
#include "DEBUG_MARKER.h"
#include "LIVE_LINK.h"

#include <algorithm>
#include <atomic>
#include <cstring>
#include <mutex>
#include <unordered_map>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <detours.h>

/*
	Addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).

	The script entities the World group's Overlays tab draws: the player's trigger boxes and use-trigger boxes, doors
	and splines. They are found by a walk over every composite instance of the running level, on the entity thread
	(between frames of the level's scripts, holding the entity manager's own lock), every few seconds while an overlay or
	a list wants them. An entity's kind is told by the type it was made as (the id the game's entity factory is keyed
	on), or for a door by its object's own vtable. Where each one is - and a box's size, a door's zone link, a spline's
	points - is then read a few at a time over the next frames with the game's own getters, so a big level's thousands
	of entities never hold up a frame; what was read is kept for an entity found again by the next walk. The render
	thread is handed the whole result (a new copy each time something changes).

	A trigger box's entered and exited outputs are noted as they fire (small hooks on the four functions that fire them,
	two for each kind of box), so the overlay can flash a box as the player walks in or out.
*/
namespace
{
	// ---- Where the game keeps it ----

	// The entity manager (a pointer to it): the running level's root composite id, and its composites - an offset (in
	// four-byte words from the start of the loaded COMMANDS.PAK, a pointer to which is kept at kPackFile) to the packed
	// offsets of each, and how many
	constexpr uintptr_t kEntityManager = 0x0134EF40;
	constexpr uintptr_t kPackFile = 0x0134EF44;
	constexpr uint32_t kManagerRoot = 0x0C;
	constexpr uint32_t kManagerTemplates = 0x18;
	constexpr uint32_t kManagerRootInstance = 0x44; // the level's top instance (a counted pointer)
	constexpr uint32_t kPackedNone = 0xFFFFFFFF;
	// A composite: its name (a pointer to the text, set at load) and its running instances (an array of counted pointers)
	constexpr uint32_t kTemplateName = 0x00;
	constexpr uint32_t kTemplateInstances = 0x64;
	// An instance's entities (an array of counted pointers) and the composite it was made from (a packed pointer)
	constexpr uint32_t kInstanceEntities = 0x2C;
	constexpr uint32_t kInstanceTemplate = 0x30;
	// A counted allocation: how many hold it, how many items (an array), then where they are (or the thing itself)
	constexpr uint32_t kAllocationReferences = 0x04;
	constexpr uint32_t kAllocationCount = 0x08;
	constexpr uint32_t kAllocationData = 0x0C;
	// An entity: its state bits, its id and the type it was made as
	constexpr uint32_t kEntityState = 0x04;
	constexpr uint32_t kEntityGuid = 0x0C;
	constexpr uint32_t kEntityType = 0x10;
	// An entity's virtual slot that gives its interface
	constexpr uint32_t kVGetInterface = 0x70;

	// The types, as the ids the game works out from their names at start-up (where it keeps each), and a door's vtable
	constexpr uintptr_t kTypeTriggerBox = 0x0134FA1C;
	constexpr uintptr_t kTypeUseTriggerBox = 0x0134FA30;
	constexpr uintptr_t kTypeSplinePath = 0x0134F948;
	constexpr uintptr_t kDoorVTable = 0x00DE04F0;
	// A trigger box (either kind) keeps the row of the collision map its box has at +0x24; a spline path keeps the spline
	// it made at start (a pointer) at +0x24
	constexpr uint32_t kTriggerHullRow = 0x24;
	constexpr uint32_t kSplinePathSpline = 0x24;
	// A spline: the position it was made at (twelve numbers), its points (an offset in four-byte words into the pack file,
	// and how many: a position and a normal each, six numbers) and whether it is closed
	constexpr uint32_t kSplineMatrix = 0x00;
	constexpr uint32_t kSplinePoints = 0x30;
	constexpr uint32_t kSplineClosed = 0x38;
	constexpr uint32_t kSplinePointFloats = 6;
	constexpr int kMaxSplinePoints = 1024;

	// The game's getters: a box's half size (each kind of box has its own, both read through the box's interface), the zone
	// link a door opens (a counted pointer, given back with its release) and whether a door is opened by script only
	constexpr uintptr_t kTriggerHalfSize = 0x004D83B0;
	constexpr uintptr_t kUseTriggerHalfSize = 0x004D8440;
	constexpr uintptr_t kDoorZoneLink = 0x004FCCC0;
	constexpr uintptr_t kZoneLinkRelease = 0x00004550;
	constexpr uintptr_t kDoorScripted = 0x004EBF50;
	// The entity manager's lock (taken and given back as the game's own access object does)
	constexpr uintptr_t kAccessLock = 0x0051E1B0;
	constexpr uintptr_t kAccessUnlock = 0x0051E1D0;
	// What fires a trigger box's entered and exited outputs (each kind of box has its own)
	constexpr uintptr_t kTriggerEntered = 0x004C5D50;
	constexpr uintptr_t kTriggerExited = 0x004C5D80;
	constexpr uintptr_t kUseTriggerEntered = 0x004C5DB0;
	constexpr uintptr_t kUseTriggerExited = 0x004C5DE0;

	// A zone link: its two zones (counted pointers to them, each zone's id at +0x0C) and its state
	constexpr uint32_t kLinkZoneA = 0x00;
	constexpr uint32_t kLinkZoneB = 0x04;
	constexpr uint32_t kLinkState = 0x10;
	constexpr uint32_t kZoneId = 0x0C;

	typedef float* (__thiscall* t_half_size)(void* boxInterface, float* out, const void* entity);
	typedef void** (__thiscall* t_door_zone_link)(void* doorInterface, void** out, const void* entity);
	typedef void(__thiscall* t_zone_link_release)(void** link);
	typedef bool(__thiscall* t_door_scripted)(void* doorInterface, const void* entity);
	typedef void(__thiscall* t_access)(void* access);
	typedef void* (__thiscall* t_get_interface)(const void* entity);
	typedef void(__thiscall* t_trigger_fired)(void* boxInterface, const void* entity, const void* info, double delay);

	// How often the walk is repeated while wanted, how many entities a frame have where they are read for the first time,
	// how many already read are read again (things move), and for how long a want lasts
	constexpr ULONGLONG kWalkEveryMs = 3000;
	constexpr int kPlaceNewPerFrame = 48;
	constexpr int kRefreshPerFrame = 8;
	constexpr ULONGLONG kWantedForMs = 1500;
	constexpr int kMaxTemplates = 16384;
	constexpr int kMaxInstances = 65536;
	constexpr int kMaxPerInstance = 8192;
	constexpr int kMaxEntities = 32768;

	// ---- Guarded reads and calls (plain data in and out) ----

	bool Read(const void* at, void* out, size_t size)
	{
		__try
		{
			memcpy(out, at, size);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	template <typename T> T Get(const void* base, uint32_t offset, T fallback = T())
	{
		T value;
		if (!base || !Read(static_cast<const uint8_t*>(base) + offset, &value, sizeof(value)))
			return fallback;
		return value;
	}

	void CopyText(const char* text, char* out, size_t size)
	{
		out[0] = '\0';
		__try
		{
			if (!text)
				return;
			size_t i = 0;
			for (; i + 1 < size && text[i]; i++)
				out[i] = text[i];
			out[i] = '\0';
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			out[0] = '\0';
		}
	}

	bool LevelUp()
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

	// The composites, as pointers into the pack file
	int ReadTemplates(const uint8_t* manager, uintptr_t pack, const uint8_t** out, int max)
	{
		__try
		{
			const uint32_t offset = *reinterpret_cast<const uint32_t*>(manager + kManagerTemplates);
			const uint32_t count = *reinterpret_cast<const uint32_t*>(manager + kManagerTemplates + 4);
			if (offset == kPackedNone || !pack)
				return 0;
			const uint32_t* offsets = reinterpret_cast<const uint32_t*>(pack + static_cast<uintptr_t>(offset) * 4u);
			int found = 0;
			for (uint32_t i = 0; i < count && found < max; i++)
				if (offsets[i] != kPackedNone)
					out[found++] = reinterpret_cast<const uint8_t*>(pack + static_cast<uintptr_t>(offsets[i]) * 4u);
			return found;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	// A composite's running instances (their objects)
	int ReadInstances(const uint8_t* composite, const uint8_t** out, int max)
	{
		int found = 0;
		__try
		{
			const uint8_t* array = *reinterpret_cast<const uint8_t* const*>(composite + kTemplateInstances);
			if (!array)
				return 0;
			const uint32_t count = *reinterpret_cast<const uint32_t*>(array + kAllocationCount);
			const uint8_t* const* data = *reinterpret_cast<const uint8_t* const* const*>(array + kAllocationData);
			for (uint32_t i = 0; data && i < count && found < max; i++)
			{
				const uint8_t* allocation = data[i];
				const uint8_t* object = allocation ? *reinterpret_cast<const uint8_t* const*>(allocation + kAllocationData) : nullptr;
				if (object)
					out[found++] = object;
			}
			return found;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return found;
		}
	}

	struct Types
	{
		bool haveTrigger, haveUseTrigger, haveSpline, haveDoor;
		uint32_t trigger, useTrigger, spline;
		uintptr_t doorVTable;
	};

	struct Found
	{
		const void* allocation;
		const uint8_t* object;
		uint32_t guid;
		WORLD_DEBUG::Kind kind;
	};

	// An instance's entities of the kinds wanted (and how many it has, in looked)
	int MatchEntities(const uint8_t* instance, const Types& types, Found* out, int max, int& looked)
	{
		int found = 0;
		__try
		{
			const uint8_t* array = *reinterpret_cast<const uint8_t* const*>(instance + kInstanceEntities);
			if (!array)
				return 0;
			const uint32_t count = *reinterpret_cast<const uint32_t*>(array + kAllocationCount);
			const uint8_t* const* data = *reinterpret_cast<const uint8_t* const* const*>(array + kAllocationData);
			for (uint32_t i = 0; data && i < count; i++)
			{
				const uint8_t* allocation = data[i];
				const uint8_t* object = allocation ? *reinterpret_cast<const uint8_t* const*>(allocation + kAllocationData) : nullptr;
				if (!object)
					continue;
				looked++;
				const uint32_t type = *reinterpret_cast<const uint32_t*>(object + kEntityType);
				WORLD_DEBUG::Kind kind;
				if (types.haveTrigger && type == types.trigger)
					kind = WORLD_DEBUG::Kind::Trigger;
				else if (types.haveUseTrigger && type == types.useTrigger)
					kind = WORLD_DEBUG::Kind::UseTrigger;
				else if (types.haveSpline && type == types.spline)
					kind = WORLD_DEBUG::Kind::Spline;
				else if (types.haveDoor && *reinterpret_cast<const uintptr_t*>(object) == types.doorVTable)
					kind = WORLD_DEBUG::Kind::Door;
				else
					continue;
				if (found < max)
					out[found++] = { allocation, object, *reinterpret_cast<const uint32_t*>(object + kEntityGuid), kind };
			}
			return found;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return found;
		}
	}

	void* InterfaceOf(const uint8_t* object)
	{
		__try
		{
			const t_get_interface get = reinterpret_cast<t_get_interface>((*reinterpret_cast<void* const* const*>(object))[kVGetInterface / 4]);
			return get(object);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	// Where an entity is (false when it has no position of its own)
	bool CallPosition(const void* allocation, float out[12])
	{
		__try
		{
			const void* entity = allocation;
			DEBUG_MARKER::Position position;
			DEBUG_MARKER::entity_position(&position, &entity, *DEBUG_MARKER::default_position);
			memcpy(out, position.m, sizeof(position.m));
			return memcmp(&position, DEBUG_MARKER::default_position, sizeof(position)) != 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool CallHalfSize(t_half_size getter, void* boxInterface, const void* allocation, float out[3])
	{
		__try
		{
			const void* entity = allocation;
			float value[3] = {};
			getter(boxInterface, value, &entity);
			memcpy(out, value, sizeof(value));
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool CallDoorLink(t_door_zone_link getter, t_zone_link_release release, void* doorInterface, const void* allocation, const void*& link)
	{
		__try
		{
			const void* entity = allocation;
			void* held = nullptr;
			getter(doorInterface, &held, &entity);
			link = held ? *reinterpret_cast<const void* const*>(static_cast<const uint8_t*>(held) + kAllocationData) : nullptr;
			if (held)
				release(&held);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool CallDoorScripted(t_door_scripted getter, void* doorInterface, const void* allocation, bool& scripted)
	{
		__try
		{
			const void* entity = allocation;
			scripted = getter(doorInterface, &entity);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// A spline path's spline: where it was made, its points (local, three numbers each) and whether it is closed
	bool ReadSpline(const uint8_t* object, uintptr_t pack, float matrix[12], float* points, int max, int& count, bool& closed)
	{
		count = 0;
		__try
		{
			const uint8_t* spline = *reinterpret_cast<const uint8_t* const*>(object + kSplinePathSpline);
			if (!spline || !pack)
				return false;
			memcpy(matrix, spline + kSplineMatrix, 12 * sizeof(float));
			const uint32_t offset = *reinterpret_cast<const uint32_t*>(spline + kSplinePoints);
			const uint32_t used = *reinterpret_cast<const uint32_t*>(spline + kSplinePoints + 4);
			closed = spline[kSplineClosed] != 0;
			if (offset == kPackedNone)
				return true;
			const float* data = reinterpret_cast<const float*>(pack + static_cast<uintptr_t>(offset) * 4u);
			const int n = static_cast<int>((std::min)(used, static_cast<uint32_t>(max)));
			for (int i = 0; i < n; i++)
				memcpy(points + i * 3, data + i * kSplinePointFloats, 3 * sizeof(float));
			count = n;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	void Lock(void* storage, bool lock)
	{
		const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(lock ? kAccessLock : kAccessUnlock);
		if (!at)
			return;
		__try
		{
			reinterpret_cast<t_access>(at)(storage);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
		}
	}

	// Still the same entity: something still holds its allocation, which still holds it, and it still has its id. Checked
	// (entity thread) before any of the game's getters is called on an entity, as it may have been removed since the walk
	// found it, and the getters call through its vtable and count references on its allocation
	bool StillThere(const WORLD_DEBUG::WorldEntity& entity)
	{
		return Get<int32_t>(entity.allocation, kAllocationReferences, 0) > 0 && Get<const void*>(entity.allocation, kAllocationData, nullptr) == entity.object
			&& Get<uint32_t>(entity.object, kEntityGuid, 0) == entity.guid;
	}

	void Transform(const float m[12], const float local[3], float out[3])
	{
		out[0] = m[0] * local[0] + m[1] * local[1] + m[2] * local[2] + m[3];
		out[1] = m[4] * local[0] + m[5] * local[1] + m[6] * local[2] + m[7];
		out[2] = m[8] * local[0] + m[9] * local[1] + m[10] * local[2] + m[11];
	}

	// ---- The entity thread's own copy, and what it hands out ----

	struct Working
	{
		WORLD_DEBUG::WorldScan scan;
		uint32_t root = 0;
		uintptr_t pack = 0;
		size_t refreshCursor = 0;
	};
	Working g_working;

	std::mutex g_publishMutex;
	std::shared_ptr<const WORLD_DEBUG::WorldScan> g_published = std::make_shared<WORLD_DEBUG::WorldScan>();

	std::atomic<bool> g_jobQueued = false;
	std::atomic<ULONGLONG> g_wantedAt = 0;
	std::atomic<bool> g_rescan = false;

	// The trigger boxes' fires, by the interface they fired from (tick counts)
	struct Fires
	{
		ULONGLONG entered = 0;
		ULONGLONG exited = 0;
	};
	std::mutex g_fireMutex;
	std::unordered_map<const void*, Fires> g_fires;

	void Publish()
	{
		g_working.scan.unplaced = 0;
		for (const WORLD_DEBUG::WorldEntity& entity : g_working.scan.entities)
			g_working.scan.unplaced += !entity.placed && entity.placeFailures < 3 ? 1 : 0;
		std::shared_ptr<const WORLD_DEBUG::WorldScan> copy = std::make_shared<WORLD_DEBUG::WorldScan>(g_working.scan);
		std::lock_guard<std::mutex> lock(g_publishMutex);
		g_published = std::move(copy);
	}

	void Forget()
	{
		const bool had = !g_working.scan.entities.empty();
		const uint32_t generation = g_working.scan.generation;
		g_working = Working();
		g_working.scan.generation = generation;
		{
			std::lock_guard<std::mutex> lock(g_fireMutex);
			g_fires.clear();
		}
		if (had)
			Publish();
	}

	Types ReadTypes()
	{
		Types types = {};
		const uintptr_t trigger = DEVTOOLS_RELATIVE_ADDRESS(kTypeTriggerBox);
		const uintptr_t useTrigger = DEVTOOLS_RELATIVE_ADDRESS(kTypeUseTriggerBox);
		const uintptr_t spline = DEVTOOLS_RELATIVE_ADDRESS(kTypeSplinePath);
		types.haveTrigger = trigger && Read(reinterpret_cast<const void*>(trigger), &types.trigger, 4);
		types.haveUseTrigger = useTrigger && Read(reinterpret_cast<const void*>(useTrigger), &types.useTrigger, 4);
		types.haveSpline = spline && Read(reinterpret_cast<const void*>(spline), &types.spline, 4);
		types.doorVTable = DEVTOOLS_RELATIVE_ADDRESS(kDoorVTable);
		types.haveDoor = types.doorVTable != 0;
		return types;
	}

	// Walks every instance for the entities wanted, keeping what was read of those found before
	void Walk(const uint8_t* manager)
	{
		const ULONGLONG started = GetTickCount64();
		LARGE_INTEGER frequency, begin, end;
		QueryPerformanceFrequency(&frequency);
		QueryPerformanceCounter(&begin);

		const Types types = ReadTypes();
		std::vector<WORLD_DEBUG::WorldEntity> entities;
		int looked = 0;
		if (types.haveTrigger || types.haveUseTrigger || types.haveSpline || types.haveDoor)
		{
			static std::vector<const uint8_t*> templates(kMaxTemplates), instances(kMaxInstances);
			static std::vector<Found> found(kMaxPerInstance);
			std::unordered_map<const void*, size_t> before;
			for (size_t i = 0; i < g_working.scan.entities.size(); i++)
				before[g_working.scan.entities[i].object] = i;

			// The entities wanted in one instance of a composite (its name read when the first is found)
			auto take = [&](const uint8_t* instance, const uint8_t* composite, std::string& name)
			{
				const int matches = MatchEntities(instance, types, found.data(), kMaxPerInstance, looked);
				for (int m = 0; m < matches && static_cast<int>(entities.size()) < kMaxEntities; m++)
				{
					if (name.empty())
					{
						char text[256];
						CopyText(Get<const char*>(composite, kTemplateName, nullptr), text, sizeof(text));
						const char* last = text;
						for (const char* c = text; *c; c++)
							if (*c == '\\' || *c == '/')
								last = c + 1;
						name = last[0] ? last : "?";
					}
					const Found& match = found[m];
					const auto previous = before.find(match.object);
					if (previous != before.end())
					{
						const WORLD_DEBUG::WorldEntity& old = g_working.scan.entities[previous->second];
						if (old.allocation == match.allocation && old.guid == match.guid && old.kind == match.kind)
						{
							entities.push_back(old);
							entities.back().composite = name;
							continue;
						}
					}
					WORLD_DEBUG::WorldEntity entity;
					entity.kind = match.kind;
					entity.allocation = match.allocation;
					entity.object = match.object;
					entity.guid = match.guid;
					entity.composite = name;
					entities.push_back(std::move(entity));
				}
			};

			alignas(8) uint8_t access[8] = {};
			Lock(access, true);
			// The level's own top instance first (in case its composite does not list it), then every composite's instances
			const uint8_t* rootAllocation = Get<const uint8_t*>(manager, kManagerRootInstance, nullptr);
			const uint8_t* root = rootAllocation ? Get<const uint8_t*>(rootAllocation, kAllocationData, nullptr) : nullptr;
			if (root)
			{
				const uint32_t packed = Get<uint32_t>(root, kInstanceTemplate, kPackedNone);
				const uint8_t* composite = packed != kPackedNone ? reinterpret_cast<const uint8_t*>(g_working.pack + static_cast<uintptr_t>(packed) * 4u) : nullptr;
				std::string name;
				take(root, composite, name);
			}
			const int templateCount = ReadTemplates(manager, g_working.pack, templates.data(), kMaxTemplates);
			for (int t = 0; t < templateCount && static_cast<int>(entities.size()) < kMaxEntities; t++)
			{
				const int instanceCount = ReadInstances(templates[t], instances.data(), kMaxInstances);
				std::string name;
				for (int i = 0; i < instanceCount; i++)
					if (instances[i] != root)
						take(instances[i], templates[t], name);
			}
			Lock(access, false);
		}

		QueryPerformanceCounter(&end);
		g_working.scan.entities.swap(entities);
		g_working.scan.generation++;
		g_working.scan.walked = looked;
		g_working.scan.walkMs = static_cast<float>(static_cast<double>(end.QuadPart - begin.QuadPart) * 1000.0 / static_cast<double>(frequency.QuadPart));
		g_working.scan.walkedAt = started;
		g_working.scan.available = types.haveTrigger || types.haveUseTrigger || types.haveSpline || types.haveDoor;
		g_working.refreshCursor = 0;
	}

	// Reads where one entity is (and the rest of what its kind shows); false when it could not be read
	bool Place(WORLD_DEBUG::WorldEntity& entity)
	{
		if (!StillThere(entity))
			return false;
		if (entity.kind == WORLD_DEBUG::Kind::Spline)
		{
			static std::vector<float> local(kMaxSplinePoints * 3);
			int count = 0;
			bool closed = false;
			float matrix[12];
			if (!ReadSpline(entity.object, g_working.pack, matrix, local.data(), kMaxSplinePoints, count, closed))
				return false;
			memcpy(entity.matrix, matrix, sizeof(matrix));
			entity.closed = closed;
			entity.points.resize(static_cast<size_t>(count) * 3);
			for (int i = 0; i < count; i++)
				Transform(matrix, &local[i * 3], &entity.points[i * 3]);
			return true;
		}

		if (!entity.iface)
			entity.iface = InterfaceOf(entity.object);
		float matrix[12];
		if (!CallPosition(entity.allocation, matrix))
			return false;
		memcpy(entity.matrix, matrix, sizeof(matrix));

		if (entity.kind == WORLD_DEBUG::Kind::Trigger || entity.kind == WORLD_DEBUG::Kind::UseTrigger)
		{
			const uintptr_t getter = DEVTOOLS_RELATIVE_ADDRESS(entity.kind == WORLD_DEBUG::Kind::Trigger ? kTriggerHalfSize : kUseTriggerHalfSize);
			if (getter && entity.iface)
				CallHalfSize(reinterpret_cast<t_half_size>(getter), const_cast<void*>(entity.iface), entity.allocation, entity.halfSize);
			entity.hullRow = Get<int>(entity.object, kTriggerHullRow, -1);
			WORLD_DEBUG::HullRow row;
			entity.zone = WORLD_DEBUG::ReadHullRow(entity.hullRow, row) ? row.zone : WORLD_DEBUG::kNoZone;
		}
		else if (entity.kind == WORLD_DEBUG::Kind::Door && entity.iface)
		{
			const uintptr_t link = DEVTOOLS_RELATIVE_ADDRESS(kDoorZoneLink);
			const uintptr_t release = DEVTOOLS_RELATIVE_ADDRESS(kZoneLinkRelease);
			const uintptr_t scripted = DEVTOOLS_RELATIVE_ADDRESS(kDoorScripted);
			if (link && release)
				CallDoorLink(reinterpret_cast<t_door_zone_link>(link), reinterpret_cast<t_zone_link_release>(release), const_cast<void*>(entity.iface), entity.allocation, entity.zoneLink);
			if (scripted)
				CallDoorScripted(reinterpret_cast<t_door_scripted>(scripted), const_cast<void*>(entity.iface), entity.allocation, entity.scripted);
		}
		return true;
	}

	// Reads where the newest entities are, and again a few of those read before; true when anything changed
	bool PlaceSome()
	{
		std::vector<WORLD_DEBUG::WorldEntity>& entities = g_working.scan.entities;
		if (entities.empty())
			return false;
		bool changed = false;
		alignas(8) uint8_t access[8] = {};
		Lock(access, true);
		int placed = 0;
		for (WORLD_DEBUG::WorldEntity& entity : entities)
		{
			if (placed >= kPlaceNewPerFrame)
				break;
			if (entity.placed || entity.placeFailures >= 3)
				continue;
			placed++;
			if (Place(entity))
				entity.placed = true;
			else
				entity.placeFailures++;
			changed = true;
		}
		// Things placed before, again (a box on a lift moves): published only when one moved
		for (int i = 0; i < kRefreshPerFrame && placed < kPlaceNewPerFrame; i++)
		{
			if (g_working.refreshCursor >= entities.size())
				g_working.refreshCursor = 0;
			WORLD_DEBUG::WorldEntity& entity = entities[g_working.refreshCursor++];
			if (!entity.placed || entity.kind == WORLD_DEBUG::Kind::Spline || !StillThere(entity))
				continue;
			float matrix[12];
			if (CallPosition(entity.allocation, matrix))
			{
				const float dx = matrix[3] - entity.matrix[3], dy = matrix[7] - entity.matrix[7], dz = matrix[11] - entity.matrix[11];
				if (dx * dx + dy * dy + dz * dz > 0.0001f)
				{
					memcpy(entity.matrix, matrix, sizeof(matrix));
					changed = true;
				}
			}
		}
		Lock(access, false);
		return changed;
	}

	// The entity thread's turn (queued by WantScan)
	void EntityFrame()
	{
		g_jobQueued = false;
		if (GetTickCount64() - g_wantedAt.load() > kWantedForMs)
			return;
		if (!LevelUp())
		{
			Forget();
			return;
		}
		const uintptr_t managerAt = DEVTOOLS_RELATIVE_ADDRESS(kEntityManager);
		const uintptr_t packAt = DEVTOOLS_RELATIVE_ADDRESS(kPackFile);
		const uint8_t* manager = managerAt ? Get<const uint8_t*>(reinterpret_cast<const void*>(managerAt), 0, nullptr) : nullptr;
		const uintptr_t pack = packAt ? Get<uintptr_t>(reinterpret_cast<const void*>(packAt), 0, 0) : 0;
		if (!manager || !pack)
			return;
		// Another level (or the same one loaded again): nothing found before holds
		const uint32_t root = Get<uint32_t>(manager, kManagerRoot, 0);
		if (root != g_working.root || pack != g_working.pack)
		{
			Forget();
			g_working.root = root;
			g_working.pack = pack;
		}

		bool changed = false;
		if (g_rescan.exchange(false))
		{
			for (WORLD_DEBUG::WorldEntity& entity : g_working.scan.entities)
			{
				entity.placed = false;
				entity.placeFailures = 0;
			}
			g_working.scan.walkedAt = 0;
		}
		if (GetTickCount64() - g_working.scan.walkedAt >= kWalkEveryMs)
		{
			Walk(manager);
			changed = true;
		}
		changed |= PlaceSome();
		if (changed)
			Publish();
	}

	// ---- The trigger boxes' fires ----

	t_trigger_fired g_triggerEntered = nullptr, g_triggerExited = nullptr, g_useTriggerEntered = nullptr, g_useTriggerExited = nullptr;
	bool g_firesHooked[4] = {};

	void NoteFire(const void* boxInterface, bool entered)
	{
		std::lock_guard<std::mutex> lock(g_fireMutex);
		if (g_fires.size() > 8192)
			g_fires.clear();
		Fires& fires = g_fires[boxInterface];
		(entered ? fires.entered : fires.exited) = GetTickCount64();
	}

	void __fastcall OnTriggerEntered(void* boxInterface, void* /*edx*/, const void* entity, const void* info, double delay)
	{
		NoteFire(boxInterface, true);
		g_triggerEntered(boxInterface, entity, info, delay);
	}

	void __fastcall OnTriggerExited(void* boxInterface, void* /*edx*/, const void* entity, const void* info, double delay)
	{
		NoteFire(boxInterface, false);
		g_triggerExited(boxInterface, entity, info, delay);
	}

	void __fastcall OnUseTriggerEntered(void* boxInterface, void* /*edx*/, const void* entity, const void* info, double delay)
	{
		NoteFire(boxInterface, true);
		g_useTriggerEntered(boxInterface, entity, info, delay);
	}

	void __fastcall OnUseTriggerExited(void* boxInterface, void* /*edx*/, const void* entity, const void* info, double delay)
	{
		NoteFire(boxInterface, false);
		g_useTriggerExited(boxInterface, entity, info, delay);
	}

	void AttachFireHooks(bool attach)
	{
		if (attach)
		{
			g_triggerEntered = reinterpret_cast<t_trigger_fired>(DEVTOOLS_RELATIVE_ADDRESS(kTriggerEntered));
			g_triggerExited = reinterpret_cast<t_trigger_fired>(DEVTOOLS_RELATIVE_ADDRESS(kTriggerExited));
			g_useTriggerEntered = reinterpret_cast<t_trigger_fired>(DEVTOOLS_RELATIVE_ADDRESS(kUseTriggerEntered));
			g_useTriggerExited = reinterpret_cast<t_trigger_fired>(DEVTOOLS_RELATIVE_ADDRESS(kUseTriggerExited));
			if ((g_firesHooked[0] = g_triggerEntered != nullptr))
				DEVTOOLS_DETOURS_ATTACH(g_triggerEntered, OnTriggerEntered);
			if ((g_firesHooked[1] = g_triggerExited != nullptr))
				DEVTOOLS_DETOURS_ATTACH(g_triggerExited, OnTriggerExited);
			if ((g_firesHooked[2] = g_useTriggerEntered != nullptr))
				DEVTOOLS_DETOURS_ATTACH(g_useTriggerEntered, OnUseTriggerEntered);
			if ((g_firesHooked[3] = g_useTriggerExited != nullptr))
				DEVTOOLS_DETOURS_ATTACH(g_useTriggerExited, OnUseTriggerExited);
			return;
		}
		if (g_firesHooked[0])
			DEVTOOLS_DETOURS_DETACH(g_triggerEntered, OnTriggerEntered);
		if (g_firesHooked[1])
			DEVTOOLS_DETOURS_DETACH(g_triggerExited, OnTriggerExited);
		if (g_firesHooked[2])
			DEVTOOLS_DETOURS_DETACH(g_useTriggerEntered, OnUseTriggerEntered);
		if (g_firesHooked[3])
			DEVTOOLS_DETOURS_DETACH(g_useTriggerExited, OnUseTriggerExited);
		g_firesHooked[0] = g_firesHooked[1] = g_firesHooked[2] = g_firesHooked[3] = false;
	}

	const bool g_hooks = DEBUG_MENU::AddHooks(AttachFireHooks);
}

const char* WORLD_DEBUG::KindName(Kind kind)
{
	switch (kind)
	{
	case Kind::Trigger: return "trigger box";
	case Kind::UseTrigger: return "use trigger box";
	case Kind::Door: return "door";
	case Kind::Spline: return "spline";
	default: return "?";
	}
}

std::shared_ptr<const WORLD_DEBUG::WorldScan> WORLD_DEBUG::Scan()
{
	std::lock_guard<std::mutex> lock(g_publishMutex);
	return g_published;
}

void WORLD_DEBUG::WantScan()
{
	g_wantedAt = GetTickCount64();
	if (!g_jobQueued.exchange(true))
		DEBUG_MENU::OnEntityThread([]() { EntityFrame(); });
}

void WORLD_DEBUG::Rescan()
{
	g_rescan = true;
	WantScan();
}

bool WORLD_DEBUG::LiveState(const WorldEntity& entity, uint32_t& state)
{
	// Still the same entity: its allocation still holds it, and it still has its id
	if (Get<const void*>(entity.allocation, kAllocationData, nullptr) != entity.object || Get<uint32_t>(entity.object, kEntityGuid, 0) != entity.guid)
		return false;
	return Read(entity.object + kEntityState, &state, sizeof(state));
}

void WORLD_DEBUG::LastFired(const WorldEntity& entity, double& enteredAgo, double& exitedAgo)
{
	enteredAgo = exitedAgo = -1.0;
	if (!entity.iface)
		return;
	const ULONGLONG now = GetTickCount64();
	std::lock_guard<std::mutex> lock(g_fireMutex);
	const auto found = g_fires.find(entity.iface);
	if (found == g_fires.end())
		return;
	if (found->second.entered)
		enteredAgo = static_cast<double>(now - found->second.entered) / 1000.0;
	if (found->second.exited)
		exitedAgo = static_cast<double>(now - found->second.exited) / 1000.0;
}

bool WORLD_DEBUG::LinkState(const void* zoneLink, uint32_t& zoneA, uint32_t& zoneB, uint32_t& state)
{
	if (!zoneLink)
		return false;
	const void* a = Get<const void*>(zoneLink, kLinkZoneA, nullptr);
	const void* b = Get<const void*>(zoneLink, kLinkZoneB, nullptr);
	const void* zoneObjectA = a ? Get<const void*>(a, kAllocationData, nullptr) : nullptr;
	const void* zoneObjectB = b ? Get<const void*>(b, kAllocationData, nullptr) : nullptr;
	zoneA = zoneObjectA ? Get<uint32_t>(zoneObjectA, kZoneId, kNoZone) : kNoZone;
	zoneB = zoneObjectB ? Get<uint32_t>(zoneObjectB, kZoneId, kNoZone) : kNoZone;
	return Read(static_cast<const uint8_t*>(zoneLink) + kLinkState, &state, sizeof(state));
}

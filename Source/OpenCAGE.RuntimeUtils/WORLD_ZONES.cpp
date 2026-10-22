#include "WORLD_DEBUG.h"
#include "DEBUG_MENU.h"
#include "LIVE_LINK.h"
#include "ZONE_LOADER.h"

#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <mutex>
#include <string>
#include <unordered_map>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <imgui.h>

/*
	Addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).

	The World group's Zones tab: every zone of the level as the zone streaming keeps it (loaded or not, streaming in,
	its priority this frame, which views see it, its links and exclusions, what is still loading in it and the composite
	instances that live in it), every link between two zones (open, locked, its cost), the viewpoints the views registered
	this frame, and the streaming as a whole: whether it has been stopped for running short of memory, what it is loading
	or suspending now, and whether a script holds the screen faded out.

	And why the camera is in the zone it is in. Each frame the game decides each view's zone: a view whose camera follows
	a character (the player's, usually) is handed that character's zone - the zone under the player's feet; any other
	casts a ray 500 m straight down from the camera, and the row of the level's collision map that ray hits says the
	zone. A wrong zone on one row, or a piece of collision under the camera that should not be there, puts the camera in
	the wrong zone and the zones around it never stream in. The game's own ray and its answer are noted where it asks
	for them (two calls in that per-frame check are pointed at this file), and the tab names the row: the entity and
	composite instance it belongs to, its layer and flags, and its zone. The four rays under the player's feet are shown
	the same way. Drawn over the game with world.overlay.zone_ray.
*/
namespace
{
	// ---- Where the game keeps it ----

	// The zone streaming (ZONE_LOADER.h has the pointer to it): its global zone (a counted pointer), the flag set when
	// streaming runs short of memory, the flag a script's screen fade holds until it fades in again, a flag to drop every
	// zone, two timers, a count the game raises while streaming is held up (200 = held up), its zones, links and
	// exclusions (arrays of counted pointers), the viewpoints registered this frame (an array of three numbers each), and
	// what it is loading and suspending now (a busy flag, the zone - a counted pointer - and how far it has got)
	constexpr uint32_t kManagerGlobalZone = 0x00;
	constexpr uint32_t kManagerBlocked = 0x08;
	constexpr uint32_t kManagerScreenFade = 0x09;
	constexpr uint32_t kManagerFlushAll = 0x0A;
	constexpr uint32_t kManagerLoadingTimer = 0x0C;
	constexpr uint32_t kManagerLoadingCounter = 0x10;
	constexpr uint32_t kManagerBlockedCounter = 0x14;
	constexpr uint32_t kManagerZones = 0x18;
	constexpr uint32_t kManagerLinks = 0x1C;
	constexpr uint32_t kManagerExclusions = 0x20;
	constexpr uint32_t kManagerViewpoints = 0x34;
	constexpr uint32_t kManagerLoader = 0x38;
	constexpr uint32_t kManagerSuspender = 0x44;
	constexpr uint32_t kWorkerZone = 0x04;
	constexpr uint32_t kWorkerProgress = 0x08;

	// A counted allocation the game keeps a thing or an array in: how many items (an array), then where they are (or the
	// thing itself)
	constexpr uint32_t kAllocationCount = 0x08;
	constexpr uint32_t kAllocationData = 0x0C;

	// A zone: its priority this frame, which views see it (a bit each), its state, id and index, its links, exclusions,
	// entities still loading and composite instances (arrays of counted pointers), the Zone entity that made it (its id
	// and the composite instance it is in) and its name (a pointer to the text)
	constexpr uint32_t kZonePriority = 0x00;
	constexpr uint32_t kZoneViewers = 0x04;
	constexpr uint32_t kZoneState = 0x08;
	constexpr uint32_t kZoneId = 0x0C;
	constexpr uint32_t kZoneIndex = 0x10;
	constexpr uint32_t kZoneLinks = 0x14;
	constexpr uint32_t kZoneExclusions = 0x18;
	constexpr uint32_t kZoneLoading = 0x1C;
	constexpr uint32_t kZoneComposites = 0x20;
	constexpr uint32_t kZoneEntity = 0x24;
	constexpr uint32_t kZoneName = 0x30;
	// A link: its two zones (counted pointers), the entity that made it (two ids), its state (bit 0 open, bit 1 locked) and
	// what crossing it costs (a zone's priority less this is its neighbour's through an open link)
	constexpr uint32_t kLinkZoneA = 0x00;
	constexpr uint32_t kLinkZoneB = 0x04;
	constexpr uint32_t kLinkEntity = 0x08;
	constexpr uint32_t kLinkState = 0x10;
	constexpr uint32_t kLinkCost = 0x14;
	constexpr uint32_t kLinkOpen = 0x01;
	constexpr uint32_t kLinkLocked = 0x02;
	// An exclusion: its two zones and whether it keeps them from streaming in together
	constexpr uint32_t kExclusionZoneA = 0x00;
	constexpr uint32_t kExclusionZoneB = 0x04;
	constexpr uint32_t kExclusionStreaming = 0x08;
	// A zone's state bits
	constexpr uint32_t kZoneLoaded = 0x01;
	constexpr uint32_t kZoneStreamingIn = 0x02;
	// The priority at which the streaming asks for a zone (a view's own zone gets 100, its neighbours less the links' cost)
	constexpr int kWantedPriority = 80;

	constexpr int kMaxZones = 4096;
	constexpr int kMaxLinks = 8192;
	constexpr int kMaxViewpoints = 512;

	// The views (ZONE_LOADER.h has the pointer to them and the main view's place): how many there are (a byte) and their
	// pointers; a view's kind (0 a standard view, 1 a reflection - it takes the main view's zone - 2 a shadow view), the
	// index its viewpoint is registered under (a byte) and a byte whose 0x30 bits are set while it is in use.
	// ZONE_LOADER.h names the view's zone and its two flag bytes.
	constexpr uint32_t kViewCount = 0x88;
	constexpr uint32_t kViews = 0x08;
	constexpr uint32_t kViewKind = 0xE8;
	constexpr uint32_t kViewViewer = 0x34A;
	constexpr uint32_t kViewInUse = 0x36B;
	constexpr uint8_t kViewInUseBits = 0x30;
	constexpr int kMaxViews = 16;

	// The level's collision map rows (a pointer to the first, and how many there are): 0x30 bytes each
	constexpr uintptr_t kHullRows = 0x01308838;
	constexpr uintptr_t kHullRowCount = 0x0130883C;
	constexpr uint32_t kHullRowSize = 0x30;
	// A row: its flags (the layer in the low five bits), its index in the collision data, the resource, the entity and
	// composite instance it belongs to (ids), and the zone
	constexpr uint32_t kRowFlags = 0x00;
	constexpr uint32_t kRowCollisionIndex = 0x04;
	constexpr uint32_t kRowResource = 0x08;
	constexpr uint32_t kRowEntity = 0x0C;
	constexpr uint32_t kRowInstance = 0x10;
	constexpr uint32_t kRowZone = 0x1C;

	// The characters' movement controllers (eighteen in a row, 0xB0 bytes each): the character it moves, the rows hit by
	// the four rays under its feet, the zones of those rows (-1: none), where it stands, and whether the rays have answered
	constexpr uintptr_t kControllers = 0x01307A48;
	constexpr int kControllerCount = 18;
	constexpr uint32_t kControllerSize = 0xB0;
	constexpr uint32_t kControllerOwner = 0x00;
	constexpr uint32_t kControllerFeetRows = 0x60;
	constexpr uint32_t kControllerFeetZones = 0x70;
	constexpr uint32_t kControllerSupport = 0x80;
	constexpr uint32_t kControllerFeetValid = 0xAB;
	constexpr uint32_t kControllerOverDrop = 0xAC;

	// The loaded COMMANDS.PAK (a pointer to it): packed pointers count four bytes from its start. A composite instance
	// keeps its composite (a packed pointer) at +0x30, whose name the game set at load (a pointer to the text) is its first
	// field.
	constexpr uintptr_t kPackFile = 0x0134EF44;
	constexpr uint32_t kInstanceTemplate = 0x30;

	// The per-frame zone check's two calls into the game's ray casting (in the function ZONE_LOADER.h hooks): the one that
	// reads the answer to last frame's ray of a view, and the one that casts this frame's; the functions they call; and
	// the views' ray handles (one each, four bytes, in the views' order)
	constexpr uintptr_t kZoneRayResultCall = 0x0034CC5E;
	constexpr uintptr_t kZoneRaySubmitCall = 0x0034CD70;
	constexpr uintptr_t kRayResult = 0x003EBA70;
	constexpr uintptr_t kRaySubmit = 0x003F9AD0;
	constexpr uintptr_t kZoneRayHandles = 0x012ED300;
	constexpr uint8_t kCallOpcode = 0xE8;
	// A ray's request: from, to (four numbers each); its answer: where it hit (four numbers), the surface's normal at
	// +0x30, the collision row hit at +0x48 (-1: none), how far along it at +0x50 and its status at +0x54 (2 nothing hit,
	// 3 blocked)
	constexpr uint32_t kQueryStart = 0x00;
	constexpr uint32_t kQueryEnd = 0x10;
	constexpr uint32_t kResultHit = 0x00;
	constexpr uint32_t kResultNormal = 0x30;
	constexpr uint32_t kResultRow = 0x48;
	constexpr uint32_t kResultFraction = 0x50;
	constexpr uint32_t kResultStatus = 0x54;
	constexpr int kStatusBlocked = 3;

	std::atomic<bool> g_rayOverlay = false;
	std::atomic<bool> g_rayOverlayAllViews = false;

	// ---- Guarded reads ----

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

	// The thing a counted pointer holds (null for none)
	const uint8_t* Held(const void* allocation)
	{
		return allocation ? Get<const uint8_t*>(allocation, kAllocationData, nullptr) : nullptr;
	}

	// An array the game keeps: its item pointers, up to max
	int ArrayItems(const void* allocation, const void** out, int max)
	{
		__try
		{
			if (!allocation)
				return 0;
			const uint32_t count = *reinterpret_cast<const uint32_t*>(static_cast<const uint8_t*>(allocation) + kAllocationCount);
			const void* const* data = *reinterpret_cast<const void* const* const*>(static_cast<const uint8_t*>(allocation) + kAllocationData);
			if (!data)
				return 0;
			const int n = static_cast<int>((std::min)(count, static_cast<uint32_t>(max)));
			for (int i = 0; i < n; i++)
				out[i] = data[i];
			return n;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	int ArrayCount(const void* allocation)
	{
		return allocation ? static_cast<int>(Get<uint32_t>(allocation, kAllocationCount, 0)) : 0;
	}

	// Text the game keeps, copied (empty when unreadable)
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

	const uint8_t* ZoneManager()
	{
		return Get<const uint8_t*>(ZONE_LOADER::zone_manager_instance, 0, nullptr);
	}

	const uint8_t* ViewManager()
	{
		return Get<const uint8_t*>(ZONE_LOADER::view_manager_instance, 0, nullptr);
	}

	// A zone's id from a counted pointer to it
	uint32_t ZoneIdOf(const void* allocation)
	{
		const uint8_t* zone = Held(allocation);
		return zone ? Get<uint32_t>(zone, kZoneId, WORLD_DEBUG::kNoZone) : WORLD_DEBUG::kNoZone;
	}

	std::string Hex(uint32_t value)
	{
		char text[16];
		snprintf(text, sizeof(text), "0x%08X", value);
		return text;
	}

	std::string Format(const char* format, ...)
	{
		char text[512];
		va_list args;
		va_start(args, format);
		vsnprintf(text, sizeof(text), format, args);
		va_end(args);
		return text;
	}

	bool ContainsIgnoringCase(const std::string& text, const char* part)
	{
		if (!part || !part[0])
			return true;
		auto lower = [](std::string s) { for (char& c : s) c = static_cast<char>(tolower(static_cast<unsigned char>(c))); return s; };
		return lower(text).find(lower(part)) != std::string::npos;
	}

	// ---- The snapshot ----

	WORLD_DEBUG::ZoneSnapshot g_snapshot;
	int g_snapshotFrame = -1;
	std::unordered_map<uint32_t, size_t> g_zoneById;

	void ReadZone(const uint8_t* object, WORLD_DEBUG::Zone& out)
	{
		out.object = object;
		out.priority = Get<int>(object, kZonePriority, 0);
		out.viewers = Get<uint32_t>(object, kZoneViewers, 0);
		out.state = Get<uint32_t>(object, kZoneState, 0);
		out.id = Get<uint32_t>(object, kZoneId, WORLD_DEBUG::kNoZone);
		out.index = Get<uint32_t>(object, kZoneIndex, 0);
		out.links = ArrayCount(Get<const void*>(object, kZoneLinks, nullptr));
		out.exclusions = ArrayCount(Get<const void*>(object, kZoneExclusions, nullptr));
		out.loading = ArrayCount(Get<const void*>(object, kZoneLoading, nullptr));
		out.composites = ArrayCount(Get<const void*>(object, kZoneComposites, nullptr));
		out.entityGuid = Get<uint32_t>(object, kZoneEntity, 0);
		out.entityInstance = Get<uint32_t>(object, kZoneEntity + 4, 0);
		char name[128];
		CopyText(Get<const char*>(object, kZoneName, nullptr), name, sizeof(name));
		out.name = name;
	}

	void ReadSnapshot(WORLD_DEBUG::ZoneSnapshot& out)
	{
		out = WORLD_DEBUG::ZoneSnapshot();
		g_zoneById.clear();
		if (!LevelUp())
			return;
		const uint8_t* manager = ZoneManager();
		if (!manager)
			return;

		static std::vector<const void*> items(kMaxLinks);
		const int zones = ArrayItems(Get<const void*>(manager, kManagerZones, nullptr), items.data(), kMaxZones);
		for (int i = 0; i < zones; i++)
		{
			const uint8_t* object = Held(items[i]);
			if (!object)
				continue;
			WORLD_DEBUG::Zone zone;
			ReadZone(object, zone);
			g_zoneById[zone.id] = out.zones.size();
			out.zones.push_back(std::move(zone));
		}

		const int links = ArrayItems(Get<const void*>(manager, kManagerLinks, nullptr), items.data(), kMaxLinks);
		for (int i = 0; i < links; i++)
		{
			const uint8_t* object = Held(items[i]);
			if (!object)
				continue;
			WORLD_DEBUG::ZoneLink link;
			link.object = object;
			link.zoneA = ZoneIdOf(Get<const void*>(object, kLinkZoneA, nullptr));
			link.zoneB = ZoneIdOf(Get<const void*>(object, kLinkZoneB, nullptr));
			link.entityGuid = Get<uint32_t>(object, kLinkEntity, 0);
			link.entityInstance = Get<uint32_t>(object, kLinkEntity + 4, 0);
			link.state = Get<uint32_t>(object, kLinkState, 0);
			link.cost = Get<uint32_t>(object, kLinkCost, 0);
			out.links.push_back(link);
		}

		const int exclusions = ArrayItems(Get<const void*>(manager, kManagerExclusions, nullptr), items.data(), kMaxLinks);
		for (int i = 0; i < exclusions; i++)
		{
			const uint8_t* object = Held(items[i]);
			if (!object)
				continue;
			WORLD_DEBUG::ZoneExclusion exclusion;
			exclusion.zoneA = ZoneIdOf(Get<const void*>(object, kExclusionZoneA, nullptr));
			exclusion.zoneB = ZoneIdOf(Get<const void*>(object, kExclusionZoneB, nullptr));
			exclusion.excludeStreaming = Get<uint8_t>(object, kExclusionStreaming, 0) != 0;
			out.exclusions.push_back(exclusion);
		}

		// The viewpoints are kept in place (three numbers each), not as pointers
		const void* viewpoints = Get<const void*>(manager, kManagerViewpoints, nullptr);
		const int viewpointCount = (std::min)(ArrayCount(viewpoints), kMaxViewpoints);
		const uint8_t* viewpointData = Held(viewpoints);
		for (int i = 0; viewpointData && i < viewpointCount; i++)
		{
			uint32_t values[3];
			if (!Read(viewpointData + i * sizeof(values), values, sizeof(values)))
				break;
			out.viewpoints.push_back({ values[0], values[1], values[2] });
		}

		out.globalZone = ZoneIdOf(Get<const void*>(manager, kManagerGlobalZone, nullptr));
		out.blocked = Get<uint8_t>(manager, kManagerBlocked, 0) != 0;
		out.screenFade = Get<uint8_t>(manager, kManagerScreenFade, 0) != 0;
		out.flushAll = Get<uint8_t>(manager, kManagerFlushAll, 0) != 0;
		out.loadingTimer = Get<int>(manager, kManagerLoadingTimer, 0);
		out.loadingCounter = Get<int>(manager, kManagerLoadingCounter, 0);
		out.blockedCounter = Get<int>(manager, kManagerBlockedCounter, 0);
		out.loaderBusy = Get<uint8_t>(manager, kManagerLoader, 0) != 0;
		out.loaderZone = ZoneIdOf(Get<const void*>(manager, kManagerLoader + kWorkerZone, nullptr));
		out.loaderProgress = Get<uint32_t>(manager, kManagerLoader + kWorkerProgress, 0);
		out.suspenderBusy = Get<uint8_t>(manager, kManagerSuspender, 0) != 0;
		out.suspenderZone = ZoneIdOf(Get<const void*>(manager, kManagerSuspender + kWorkerZone, nullptr));
		out.suspenderProgress = Get<uint32_t>(manager, kManagerSuspender + kWorkerProgress, 0);

		const uint8_t* views = ViewManager();
		const uint8_t* mainView = Get<const uint8_t*>(views, ZONE_LOADER::kMainViewOffset, nullptr);
		if (mainView)
		{
			out.cameraZone = Get<uint32_t>(mainView, ZONE_LOADER::kViewZoneOffset, WORLD_DEBUG::kNoZone);
			out.haveCameraZone = true;
		}
		out.read = true;
	}

	// ---- The views and why each is in its zone ----

	struct ViewReading
	{
		const uint8_t* view = nullptr;
		int index = 0;           // its place in the list of views (and of their ray handles)
		bool main = false;
		bool inUse = false;
		uint8_t zoneFlags = 0;
		uint8_t cameraFlags = 0;
		uint32_t kind = 0;
		uint32_t zone = WORLD_DEBUG::kNoZone;
		int viewer = 0;
	};

	enum class ZoneSource { Handed, NoneOfItsOwn, MainViews, Ray, Unused };

	ZoneSource SourceOf(const ViewReading& view)
	{
		if (!view.inUse)
			return ZoneSource::Unused;
		if (view.zoneFlags & ZONE_LOADER::kViewZoneHandedInFlag)
			return ZoneSource::Handed;
		if (view.cameraFlags & ZONE_LOADER::kViewNoZoneOfItsOwnFlag)
			return ZoneSource::NoneOfItsOwn;
		if (view.kind == 1)
			return ZoneSource::MainViews;
		return ZoneSource::Ray;
	}

	const char* SourceText(ZoneSource source)
	{
		switch (source)
		{
		case ZoneSource::Handed: return "handed to it by its camera: the zone of the character the camera follows (under the player's feet)";
		case ZoneSource::NoneOfItsOwn: return "none: its camera never expires and hands it no zone";
		case ZoneSource::MainViews: return "the first view's (a reflection view has no zone of its own)";
		case ZoneSource::Ray: return "the ground below the camera (a ray 500 m straight down)";
		default: return "not in use";
		}
	}

	const char* ViewKindWord(uint32_t kind)
	{
		switch (kind)
		{
		case 0: return "standard";
		case 1: return "reflection";
		case 2: return "shadow";
		default: return "other";
		}
	}

	std::string ViewKindText(uint32_t kind)
	{
		if (kind <= 2)
			return std::string("a ") + ViewKindWord(kind) + " view";
		char text[32];
		snprintf(text, sizeof(text), "a view of kind %u", kind);
		return text;
	}

	const char* SourceWord(ZoneSource source)
	{
		switch (source)
		{
		case ZoneSource::Handed: return "handed";
		case ZoneSource::NoneOfItsOwn: return "none";
		case ZoneSource::MainViews: return "first_view";
		case ZoneSource::Ray: return "ray";
		default: return "unused";
		}
	}

	int ReadViews(ViewReading* out, int max)
	{
		const uint8_t* manager = ViewManager();
		if (!manager)
			return 0;
		const int count = (std::min)(static_cast<int>(Get<uint8_t>(manager, kViewCount, 0)), max);
		const uint8_t* mainView = Get<const uint8_t*>(manager, ZONE_LOADER::kMainViewOffset, nullptr);
		int found = 0;
		for (int i = 0; i < count; i++)
		{
			ViewReading& view = out[found];
			view = ViewReading();
			view.index = i;
			view.view = Get<const uint8_t*>(manager, kViews + i * 4, nullptr);
			if (!view.view)
				continue;
			view.main = view.view == mainView;
			view.inUse = (Get<uint8_t>(view.view, kViewInUse, 0) & kViewInUseBits) != 0;
			view.zoneFlags = Get<uint8_t>(view.view, ZONE_LOADER::kViewZoneFlagsOffset, 0);
			view.cameraFlags = Get<uint8_t>(view.view, ZONE_LOADER::kViewCameraFlagsOffset, 0);
			view.kind = Get<uint32_t>(view.view, kViewKind, 0);
			view.zone = Get<uint32_t>(view.view, ZONE_LOADER::kViewZoneOffset, WORLD_DEBUG::kNoZone);
			view.viewer = Get<uint8_t>(view.view, kViewViewer, 0);
			found++;
		}
		return found;
	}

	// What the hooks noted of each view's ray (by the view's place in the list)
	struct RayRecord
	{
		bool haveQuery = false;
		float start[3] = {};
		float end[3] = {};
		ULONGLONG queryAt = 0;
		bool haveResult = false;
		float hit[3] = {};
		float normal[3] = {};
		float fraction = 0.0f;
		int status = 0;
		int row = -1;
		ULONGLONG resultAt = 0;
	};
	std::mutex g_rayMutex;
	RayRecord g_rays[kMaxViews];

	struct RayPatch
	{
		uintptr_t steam;
		uintptr_t expectedSteam;
		uint8_t* at = nullptr;
		int32_t original = 0;
		bool patched = false;
	};
	RayPatch g_resultPatch = { kZoneRayResultCall, kRayResult };
	RayPatch g_submitPatch = { kZoneRaySubmitCall, kRaySubmit };
	uintptr_t g_rayHandles = 0;
	typedef bool(__thiscall* t_ray_result)(void* handle, void* result);
	typedef void(__thiscall* t_ray_submit)(void* handle, const void* query);
	t_ray_result g_rayResult = nullptr;
	t_ray_submit g_raySubmit = nullptr;

	int ViewOfHandle(const void* handle)
	{
		if (!g_rayHandles)
			return -1;
		const intptr_t offset = reinterpret_cast<intptr_t>(handle) - static_cast<intptr_t>(g_rayHandles);
		if (offset < 0 || (offset & 3))
			return -1;
		const int index = static_cast<int>(offset / 4);
		return index < kMaxViews ? index : -1;
	}

	// The game's own answer for a view's ray, then a note of it
	bool __fastcall OnZoneRayResult(void* handle, void* /*edx*/, void* result)
	{
		const bool finished = g_rayResult(handle, result);
		const int index = ViewOfHandle(handle);
		if (finished && index >= 0)
		{
			RayRecord record;
			float hit[4], normal[4];
			if (Read(static_cast<const uint8_t*>(result) + kResultHit, hit, sizeof(hit)) && Read(static_cast<const uint8_t*>(result) + kResultNormal, normal, sizeof(normal)))
			{
				memcpy(record.hit, hit, sizeof(record.hit));
				memcpy(record.normal, normal, sizeof(record.normal));
				record.row = Get<int>(result, kResultRow, -1);
				record.fraction = Get<float>(result, kResultFraction, 0.0f);
				record.status = Get<int>(result, kResultStatus, 0);
				record.resultAt = GetTickCount64();
				std::lock_guard<std::mutex> lock(g_rayMutex);
				RayRecord& stored = g_rays[index];
				stored.haveResult = true;
				memcpy(stored.hit, record.hit, sizeof(stored.hit));
				memcpy(stored.normal, record.normal, sizeof(stored.normal));
				stored.row = record.row;
				stored.fraction = record.fraction;
				stored.status = record.status;
				stored.resultAt = record.resultAt;
			}
		}
		return finished;
	}

	// A note of the ray a view casts this frame, then the game's own cast
	void __fastcall OnZoneRaySubmit(void* handle, void* /*edx*/, const void* query)
	{
		const int index = ViewOfHandle(handle);
		float start[4], end[4];
		if (index >= 0 && Read(static_cast<const uint8_t*>(query) + kQueryStart, start, sizeof(start)) && Read(static_cast<const uint8_t*>(query) + kQueryEnd, end, sizeof(end)))
		{
			std::lock_guard<std::mutex> lock(g_rayMutex);
			RayRecord& stored = g_rays[index];
			stored.haveQuery = true;
			memcpy(stored.start, start, sizeof(stored.start));
			memcpy(stored.end, end, sizeof(stored.end));
			stored.queryAt = GetTickCount64();
		}
		g_raySubmit(handle, query);
	}

	uint8_t* CallTarget(uint8_t* call, int32_t distance)
	{
		return call + 5 + distance;
	}

	bool ReadCall(const uint8_t* at, int32_t& distance)
	{
		uint8_t bytes[5];
		if (!Read(at, bytes, sizeof(bytes)) || bytes[0] != kCallOpcode)
			return false;
		memcpy(&distance, bytes + 1, sizeof(distance));
		return true;
	}

	// Writes a call's four distance bytes (as one locked exchange where they sit inside one cache line)
	bool WriteDistance(uint8_t* call, int32_t distance)
	{
		uint8_t* where = call + 1;
		DWORD oldProtect;
		if (!VirtualProtect(where, sizeof(distance), PAGE_EXECUTE_READWRITE, &oldProtect))
			return false;
		const uintptr_t address = reinterpret_cast<uintptr_t>(where);
		if ((address & 63) <= 64 - sizeof(distance))
			InterlockedExchange(reinterpret_cast<volatile LONG*>(where), distance);
		else
			memcpy(where, &distance, sizeof(distance));
		VirtualProtect(where, sizeof(distance), oldProtect, &oldProtect);
		FlushInstructionCache(GetCurrentProcess(), call, 5);
		return true;
	}

	// Points a call at ours when it still calls what it should (original is set to what it called before the call is
	// changed, as ours calls on through it at once); false (and nothing changed) otherwise
	bool Patch(RayPatch& patch, void* replacement, void** original)
	{
		uint8_t* at = reinterpret_cast<uint8_t*>(DEVTOOLS_RELATIVE_ADDRESS(patch.steam));
		uint8_t* expected = reinterpret_cast<uint8_t*>(DEVTOOLS_RELATIVE_ADDRESS(patch.expectedSteam));
		int32_t distance = 0;
		if (!at || !expected || !ReadCall(at, distance) || CallTarget(at, distance) != expected)
			return false;
		*original = expected;
		const int32_t ours = static_cast<int32_t>(reinterpret_cast<uintptr_t>(replacement) - (reinterpret_cast<uintptr_t>(at) + 5));
		if (!WriteDistance(at, ours))
			return false;
		patch.at = at;
		patch.original = distance;
		patch.patched = true;
		return true;
	}

	void Unpatch(RayPatch& patch, void* replacement)
	{
		if (!patch.patched)
			return;
		int32_t distance = 0;
		// Only put back what is still ours (another tool may have pointed it elsewhere since)
		if (ReadCall(patch.at, distance) && CallTarget(patch.at, distance) == static_cast<uint8_t*>(replacement))
			WriteDistance(patch.at, patch.original);
		patch.patched = false;
	}

	void AttachRayHooks(bool attach)
	{
		if (!attach)
		{
			Unpatch(g_submitPatch, reinterpret_cast<void*>(&OnZoneRaySubmit));
			Unpatch(g_resultPatch, reinterpret_cast<void*>(&OnZoneRayResult));
			return;
		}
		g_rayHandles = DEVTOOLS_RELATIVE_ADDRESS(kZoneRayHandles);
		if (!g_rayHandles)
		{
			DevTools::Log("Debug menu: the zone ray inspector is not available on this game release");
			return;
		}
		Patch(g_resultPatch, reinterpret_cast<void*>(&OnZoneRayResult), reinterpret_cast<void**>(&g_rayResult));
		Patch(g_submitPatch, reinterpret_cast<void*>(&OnZoneRaySubmit), reinterpret_cast<void**>(&g_raySubmit));
		if (!g_resultPatch.patched || !g_submitPatch.patched)
			DevTools::Log("Debug menu: the zone check's ray calls are not where they were expected (answer %d, cast %d)", g_resultPatch.patched ? 1 : 0, g_submitPatch.patched ? 1 : 0);
	}

	bool RayHooksOn()
	{
		return g_resultPatch.patched || g_submitPatch.patched;
	}

	void CopyRays(RayRecord* out)
	{
		std::lock_guard<std::mutex> lock(g_rayMutex);
		for (int i = 0; i < kMaxViews; i++)
			out[i] = g_rays[i];
	}

	// The player's four rays under its feet
	struct Feet
	{
		bool have = false;
		int rows[4] = { -1, -1, -1, -1 };
		uint32_t zones[4] = {};
		float support[3] = {};
		bool valid = false;
		bool overDrop = false;
	};

	bool ReadFeet(const void* character, Feet& out)
	{
		out = Feet();
		const uintptr_t base = DEVTOOLS_RELATIVE_ADDRESS(kControllers);
		if (!base || !character)
			return false;
		for (int i = 0; i < kControllerCount; i++)
		{
			const uint8_t* controller = reinterpret_cast<const uint8_t*>(base + i * kControllerSize);
			if (Get<const void*>(controller, kControllerOwner, nullptr) != character)
				continue;
			if (!Read(controller + kControllerFeetRows, out.rows, sizeof(out.rows)) || !Read(controller + kControllerFeetZones, out.zones, sizeof(out.zones))
				|| !Read(controller + kControllerSupport, out.support, sizeof(out.support)))
				return false;
			out.valid = Get<uint8_t>(controller, kControllerFeetValid, 0) != 0;
			out.overDrop = Get<uint8_t>(controller, kControllerOverDrop, 0) != 0;
			out.have = true;
			return true;
		}
		return false;
	}

	const DEBUG_MENU::Character* Player()
	{
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
			if (character.player)
				return &character;
		return nullptr;
	}

	std::string RowText(int index)
	{
		WORLD_DEBUG::HullRow row;
		if (index < 0)
			return "no row";
		if (!WORLD_DEBUG::ReadHullRow(index, row))
			return Format("row %d (cannot be read)", index);
		// (most rows name no resource: the id only when there is one)
		return Format("row %d: %s, entity %08X in composite instance %08X%s, collision index %d; its zone %s", index,
			WORLD_DEBUG::CollisionFlagsText(row.flags).c_str(), row.entityGuid, row.instanceGuid, row.resource ? Format(", resource %08X", row.resource).c_str() : "",
			row.collisionIndex, WORLD_DEBUG::ZoneName(row.zone).c_str());
	}

	// ---- The Zones tab ----

	bool g_loadedOnly = false;
	bool g_wantedOnly = false;
	char g_zoneFilter[64] = "";
	uint32_t g_selectedZone = WORLD_DEBUG::kNoZone;

	std::string ViewerBits(uint32_t viewers)
	{
		std::string text;
		for (int bit = 0; bit < 32; bit++)
			if (viewers & (1u << bit))
				text += (text.empty() ? "" : ",") + std::to_string(bit);
		return text.empty() ? "-" : text;
	}

	ImU32 StateColour(const WORLD_DEBUG::Zone& zone)
	{
		if (zone.state & kZoneLoaded)
			return zone.viewers ? IM_COL32(120, 230, 120, 255) : IM_COL32(160, 200, 160, 255);
		if (zone.state & kZoneStreamingIn)
			return IM_COL32(240, 200, 90, 255);
		return IM_COL32(150, 150, 150, 255);
	}

	void DrawStatus(const WORLD_DEBUG::ZoneSnapshot& zones)
	{
		int loaded = 0, streaming = 0, wanted = 0, openLinks = 0;
		for (const WORLD_DEBUG::Zone& zone : zones.zones)
		{
			loaded += (zone.state & kZoneLoaded) ? 1 : 0;
			streaming += (zone.state & kZoneStreamingIn) && !(zone.state & kZoneLoaded) ? 1 : 0;
			wanted += zone.priority >= kWantedPriority ? 1 : 0;
		}
		for (const WORLD_DEBUG::ZoneLink& link : zones.links)
			openLinks += (link.state & kLinkOpen) ? 1 : 0;
		const auto counted = [](size_t count, const char* one, const char* many) { return Format("%d %s", static_cast<int>(count), count == 1 ? one : many); };
		ImGui::TextWrapped("%s: %d loaded, %d streaming in, %d wanted (priority %d or more); %s (%d open); %s; %s", counted(zones.zones.size(), "zone", "zones").c_str(),
			loaded, streaming, wanted, kWantedPriority, counted(zones.links.size(), "link", "links").c_str(), openLinks,
			counted(zones.exclusions.size(), "exclusion", "exclusions").c_str(), counted(zones.viewpoints.size(), "viewpoint", "viewpoints").c_str());
		if (zones.haveCameraZone)
			ImGui::Text("The camera is in: %s", WORLD_DEBUG::ZoneName(zones.cameraZone).c_str());
		ImGui::PushTextWrapPos(0.0f);
		if (zones.blocked)
			ImGui::TextColored(ImVec4(1.0f, 0.4f, 0.3f, 1.0f), "Streaming is stopped: it ran short of memory (more zones wanted than fit). Nothing more loads until zones unload.");
		else
			ImGui::TextUnformatted("Streaming is not stopped.");
		ImGui::SameLine();
		ImGui::TextDisabled("(held-up count %d of 200)", zones.blockedCounter);
		ImGui::PopTextWrapPos();
		if (zones.loaderBusy)
			ImGui::Text("Loading: %s (progress %u)", WORLD_DEBUG::ZoneName(zones.loaderZone).c_str(), zones.loaderProgress);
		if (zones.suspenderBusy)
			ImGui::Text("Suspending: %s (progress %u)", WORLD_DEBUG::ZoneName(zones.suspenderZone).c_str(), zones.suspenderProgress);
		if (zones.screenFade)
			ImGui::TextColored(ImVec4(1.0f, 0.8f, 0.3f, 1.0f), "A script holds the screen faded out (until a fade in).");
		if (zones.flushAll)
			ImGui::TextUnformatted("Every zone is being dropped.");
	}

	bool Shown(const WORLD_DEBUG::Zone& zone)
	{
		if (g_loadedOnly && !(zone.state & kZoneLoaded))
			return false;
		if (g_wantedOnly && zone.priority < kWantedPriority)
			return false;
		return ContainsIgnoringCase(zone.name, g_zoneFilter);
	}

	void DrawZoneTable(const WORLD_DEBUG::ZoneSnapshot& zones)
	{
		ImGui::Checkbox("Loaded only", &g_loadedOnly);
		ImGui::SameLine();
		ImGui::Checkbox("Wanted only", &g_wantedOnly);
		ImGui::SameLine();
		ImGui::SetNextItemWidth(160.0f * DEBUG_MENU::Scale());
		ImGui::InputTextWithHint("##zonefilter", "name contains", g_zoneFilter, sizeof(g_zoneFilter));

		std::vector<const WORLD_DEBUG::Zone*> rows;
		for (const WORLD_DEBUG::Zone& zone : zones.zones)
			if (Shown(zone))
				rows.push_back(&zone);
		std::sort(rows.begin(), rows.end(), [](const WORLD_DEBUG::Zone* a, const WORLD_DEBUG::Zone* b)
		{
			if (a->priority != b->priority)
				return a->priority > b->priority;
			return a->name < b->name;
		});

		// Up to 16 rows, the header and the sideways scroll bar; the short columns first, so the long state text is what scrolls
		// sideways, and the zone names (kept in view) as wide as the longest of them all, so the columns hold still as it scrolls
		const float rowHeight = ImGui::GetTextLineHeight() + ImGui::GetStyle().CellPadding.y * 2.0f;
		const float height = rowHeight * ((std::min)(static_cast<float>(rows.size()), 16.0f) + 1.0f) + ImGui::GetStyle().ScrollbarSize + 4.0f;
		float nameWidth = ImGui::CalcTextSize("(no name)").x;
		for (const WORLD_DEBUG::Zone& zone : zones.zones)
			nameWidth = (std::max)(nameWidth, ImGui::CalcTextSize(zone.name.c_str()).x);
		const ImGuiTableFlags flags = ImGuiTableFlags_RowBg | ImGuiTableFlags_BordersInnerV | ImGuiTableFlags_ScrollY | ImGuiTableFlags_ScrollX | ImGuiTableFlags_SizingFixedFit;
		if (!ImGui::BeginTable("zones", 8, flags, ImVec2(0.0f, height)))
			return;
		ImGui::TableSetupScrollFreeze(1, 1);
		ImGui::TableSetupColumn("Zone", ImGuiTableColumnFlags_WidthFixed | ImGuiTableColumnFlags_NoHide, nameWidth);
		ImGui::TableSetupColumn("Priority");
		ImGui::TableSetupColumn("Seen by views");
		ImGui::TableSetupColumn("State");
		ImGui::TableSetupColumn("Links");
		ImGui::TableSetupColumn("Loading");
		ImGui::TableSetupColumn("Composites");
		ImGui::TableSetupColumn("Id", ImGuiTableColumnFlags_WidthFixed, ImGui::CalcTextSize("00000000").x);
		ImGui::TableHeadersRow();
		for (const WORLD_DEBUG::Zone* zone : rows)
		{
			ImGui::TableNextRow();
			ImGui::TableSetColumnIndex(0);
			char label[160];
			snprintf(label, sizeof(label), "%s##%08X", zone->name.empty() ? "(no name)" : zone->name.c_str(), zone->id);
			ImGui::PushStyleColor(ImGuiCol_Text, StateColour(*zone));
			if (ImGui::Selectable(label, g_selectedZone == zone->id, ImGuiSelectableFlags_SpanAllColumns))
				g_selectedZone = g_selectedZone == zone->id ? WORLD_DEBUG::kNoZone : zone->id;
			ImGui::PopStyleColor();
			ImGui::TableSetColumnIndex(1);
			ImGui::Text("%d", zone->priority);
			ImGui::TableSetColumnIndex(2);
			ImGui::TextUnformatted(ViewerBits(zone->viewers).c_str());
			ImGui::TableSetColumnIndex(3);
			ImGui::TextUnformatted(WORLD_DEBUG::ZoneStateText(zone->state).c_str());
			ImGui::TableSetColumnIndex(4);
			ImGui::Text("%d", zone->links);
			ImGui::TableSetColumnIndex(5);
			if (zone->loading)
				ImGui::Text("%d", zone->loading);
			ImGui::TableSetColumnIndex(6);
			ImGui::Text("%d", zone->composites);
			ImGui::TableSetColumnIndex(7);
			ImGui::Text("%08X", zone->id);
		}
		ImGui::EndTable();
	}

	// The composite instances that live in a zone (read when the zone is chosen)
	std::vector<std::string> CompositesIn(const void* zoneObject, int max)
	{
		std::vector<std::string> names;
		std::vector<const void*> items(static_cast<size_t>(max));
		const int count = ArrayItems(Get<const void*>(zoneObject, kZoneComposites, nullptr), items.data(), max);
		for (int i = 0; i < count; i++)
		{
			const uint8_t* instance = Held(items[i]);
			if (!instance)
				continue;
			const std::string name = WORLD_DEBUG::CompositeName(instance);
			names.push_back(name.empty() ? Format("(instance %08X)", Get<uint32_t>(instance, 0x0C, 0)) : name);
		}
		return names;
	}

	void DrawSelectedZone(const WORLD_DEBUG::ZoneSnapshot& zones)
	{
		const WORLD_DEBUG::Zone* zone = nullptr;
		for (const WORLD_DEBUG::Zone& candidate : zones.zones)
			if (candidate.id == g_selectedZone)
				zone = &candidate;
		if (!zone)
		{
			ImGui::TextDisabled("Choose a zone above for its links and composites.");
			return;
		}
		ImGui::SeparatorText(zone->name.empty() ? "(no name)" : zone->name.c_str());
		ImGui::TextWrapped("Id %08X, index %u; made by the Zone entity %08X in composite instance %08X", zone->id, zone->index, zone->entityGuid, zone->entityInstance);
		ImGui::TextWrapped("State %s (bits %08X), priority %d, seen by views %s", WORLD_DEBUG::ZoneStateText(zone->state).c_str(), zone->state, zone->priority, ViewerBits(zone->viewers).c_str());
		if (zone->loading)
			ImGui::Text("%d entities still loading", zone->loading);
		int shown = 0;
		for (const WORLD_DEBUG::ZoneLink& link : zones.links)
		{
			if (link.zoneA != zone->id && link.zoneB != zone->id)
				continue;
			const uint32_t other = link.zoneA == zone->id ? link.zoneB : link.zoneA;
			ImGui::Bullet();
			ImGui::TextWrapped("Link to %s: %s%s, cost %u (entity %08X in %08X)", WORLD_DEBUG::ZoneName(other).c_str(), (link.state & kLinkOpen) ? "open" : "closed",
				(link.state & kLinkLocked) ? ", locked" : "", link.cost, link.entityGuid, link.entityInstance);
			shown++;
		}
		if (!shown)
			ImGui::TextDisabled("No links.");
		for (const WORLD_DEBUG::ZoneExclusion& exclusion : zones.exclusions)
			if (exclusion.zoneA == zone->id || exclusion.zoneB == zone->id)
				ImGui::BulletText("Excluded with %s%s", WORLD_DEBUG::ZoneName(exclusion.zoneA == zone->id ? exclusion.zoneB : exclusion.zoneA).c_str(),
					exclusion.excludeStreaming ? " (never streamed in together)" : "");
		if (ImGui::TreeNode("composites", "%d composite instances", zone->composites))
		{
			for (const std::string& name : CompositesIn(zone->object, 512))
				ImGui::BulletText("%s", name.c_str());
			ImGui::TreePop();
		}
	}

	void DrawLinks(const WORLD_DEBUG::ZoneSnapshot& zones)
	{
		if (!ImGui::CollapsingHeader("Links"))
			return;
		const float height = ImGui::GetTextLineHeightWithSpacing() * (std::min)(static_cast<float>(zones.links.size()) + 1.5f, 12.0f);
		const ImGuiTableFlags flags = ImGuiTableFlags_RowBg | ImGuiTableFlags_BordersInnerV | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY | ImGuiTableFlags_SizingFixedFit;
		if (!ImGui::BeginTable("links", 5, flags, ImVec2(0.0f, height)))
			return;
		ImGui::TableSetupScrollFreeze(0, 1);
		ImGui::TableSetupColumn("Zone");
		ImGui::TableSetupColumn("Zone");
		ImGui::TableSetupColumn("State");
		ImGui::TableSetupColumn("Cost");
		ImGui::TableSetupColumn("Entity");
		ImGui::TableHeadersRow();
		for (const WORLD_DEBUG::ZoneLink& link : zones.links)
		{
			ImGui::TableNextRow();
			ImGui::TableSetColumnIndex(0);
			ImGui::TextUnformatted(WORLD_DEBUG::ZoneName(link.zoneA).c_str());
			ImGui::TableSetColumnIndex(1);
			ImGui::TextUnformatted(WORLD_DEBUG::ZoneName(link.zoneB).c_str());
			ImGui::TableSetColumnIndex(2);
			ImGui::TextColored((link.state & kLinkOpen) ? ImVec4(0.5f, 0.9f, 0.5f, 1.0f) : ImVec4(0.9f, 0.6f, 0.4f, 1.0f), "%s%s", (link.state & kLinkOpen) ? "open" : "closed",
				(link.state & kLinkLocked) ? ", locked" : "");
			ImGui::TableSetColumnIndex(3);
			ImGui::Text("%u", link.cost);
			ImGui::TableSetColumnIndex(4);
			ImGui::Text("%08X in %08X", link.entityGuid, link.entityInstance);
		}
		ImGui::EndTable();
	}

	void DrawViewpoints(const WORLD_DEBUG::ZoneSnapshot& zones)
	{
		if (!ImGui::CollapsingHeader("Viewpoints this frame"))
			return;
		ImGui::PushTextWrapPos(0.0f);
		ImGui::TextDisabled("Each view registers the zone it is in; a zone a view is in gets priority 100, its neighbours less the links' cost.");
		ImGui::PopTextWrapPos();
		for (const WORLD_DEBUG::Viewpoint& viewpoint : zones.viewpoints)
			ImGui::BulletText("View %u: %s (time stamp %u)", viewpoint.view, WORLD_DEBUG::ZoneName(viewpoint.zone).c_str(), viewpoint.timeStamp);
		if (zones.exclusions.size() && ImGui::TreeNode("exclusions", "%d exclusions", static_cast<int>(zones.exclusions.size())))
		{
			for (const WORLD_DEBUG::ZoneExclusion& exclusion : zones.exclusions)
				ImGui::BulletText("%s and %s%s", WORLD_DEBUG::ZoneName(exclusion.zoneA).c_str(), WORLD_DEBUG::ZoneName(exclusion.zoneB).c_str(),
					exclusion.excludeStreaming ? " (never streamed in together)" : "");
			ImGui::TreePop();
		}
	}

	void DrawZonesTab()
	{
		const WORLD_DEBUG::ZoneSnapshot& zones = WORLD_DEBUG::Zones();
		if (!zones.read)
		{
			ImGui::TextDisabled("No level running (or its zones cannot be read).");
			return;
		}
		DrawStatus(zones);
		DrawZoneTable(zones);
		DrawSelectedZone(zones);
		DrawLinks(zones);
		DrawViewpoints(zones);
	}

	// ---- Why the camera is in its zone ----

	void DrawRaySection()
	{
		ImGui::SeparatorText("Why each view is in its zone");
		bool overlay = g_rayOverlay;
		if (ImGui::Checkbox("Draw the zone rays over the game", &overlay))
			g_rayOverlay = overlay;
		ImGui::SameLine();
		bool all = g_rayOverlayAllViews;
		if (ImGui::Checkbox("Every view (not only the first)", &all))
			g_rayOverlayAllViews = all;
		if (!RayHooksOn())
		{
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextDisabled("The game's zone rays cannot be followed on this game release; the views' zones are still shown.");
			ImGui::PopTextWrapPos();
		}
		if (!LevelUp())
		{
			ImGui::TextDisabled("No level running.");
			return;
		}

		ViewReading views[kMaxViews];
		const int count = ReadViews(views, kMaxViews);
		RayRecord rays[kMaxViews];
		CopyRays(rays);
		const ULONGLONG now = GetTickCount64();
		for (int i = 0; i < count; i++)
		{
			const ViewReading& view = views[i];
			const ZoneSource source = SourceOf(view);
			if (source == ZoneSource::Unused && !view.main)
				continue;
			ImGui::PushID(i);
			ImGui::TextWrapped("View %d%s (%s, viewpoint %d): %s", view.index, view.main ? ", the camera" : "", ViewKindText(view.kind).c_str(), view.viewer,
				WORLD_DEBUG::ZoneName(view.zone).c_str());
			ImGui::Indent();
			ImGui::TextWrapped("Its zone comes from %s.", SourceText(source));
			const RayRecord& ray = rays[view.index];
			if (ray.haveQuery && now - ray.queryAt < 2000)
			{
				// A view handed its zone casts no ray, unless a tool's camera away from the player (the Live Link camera, a
				// possessed character's, a cubemap capture) has it find its own zone for the moment
				if (source != ZoneSource::Ray)
				{
					ImGui::PushTextWrapPos(0.0f);
					ImGui::TextDisabled("A tool's camera had it cast its own ray this frame:");
					ImGui::PopTextWrapPos();
				}
				ImGui::Text("Ray from %.2f, %.2f, %.2f down to %.1f", ray.start[0], ray.start[1], ray.start[2], ray.end[1]);
				if (ray.haveResult && now - ray.resultAt < 2000)
				{
					if (ray.status == kStatusBlocked && ray.row >= 0)
					{
						ImGui::Text("Hit at %.2f, %.2f, %.2f (%.2f m below the camera)", ray.hit[0], ray.hit[1], ray.hit[2], ray.start[1] - ray.hit[1]);
						ImGui::TextWrapped("%s", RowText(ray.row).c_str());
					}
					else
						ImGui::TextUnformatted("Nothing hit: the view keeps the zone it had.");
				}
			}
			ImGui::Unindent();
			ImGui::PopID();
		}

		const DEBUG_MENU::Character* player = Player();
		Feet feet;
		if (player && ReadFeet(player->object, feet))
		{
			ImGui::TextWrapped("Under the player's feet (the zone handed to a camera that follows the player):");
			ImGui::Indent();
			for (int r = 0; r < 4; r++)
			{
				ImGui::Bullet();
				ImGui::TextWrapped("Ray %d: %s", r + 1, feet.rows[r] >= 0 ? RowText(feet.rows[r]).c_str() : "no row (or the global zone's)");
			}
			ImGui::TextWrapped("Standing at %.2f, %.2f, %.2f%s%s", feet.support[0], feet.support[1], feet.support[2], feet.valid ? "" : "; the rays have not answered yet",
				feet.overDrop ? "; over a drop" : "");
			ImGui::Unindent();
		}
		else
			ImGui::TextDisabled("No player (or its movement controller cannot be found).");
	}

	// ---- Drawn over the game ----

	void DrawRayOverlay(ImDrawList* drawList)
	{
		if (!g_rayOverlay || !LevelUp())
			return;
		ViewReading views[kMaxViews];
		const int count = ReadViews(views, kMaxViews);
		RayRecord rays[kMaxViews];
		CopyRays(rays);
		const ULONGLONG now = GetTickCount64();
		for (int i = 0; i < count; i++)
		{
			const ViewReading& view = views[i];
			if (!view.main && !g_rayOverlayAllViews)
				continue;
			// Any view that cast a ray this second (one handed its zone casts none, unless a tool's camera has it find its own)
			const RayRecord& ray = rays[view.index];
			if (!ray.haveQuery || now - ray.queryAt > 1000)
				continue;
			const bool hit = ray.haveResult && now - ray.resultAt < 1000 && ray.status == kStatusBlocked && ray.row >= 0;
			const float* end = hit ? ray.hit : ray.end;
			// Started a little below the camera, so a ray straight down from it can be seen at all
			const float start[3] = { ray.start[0], ray.start[1] - 0.3f, ray.start[2] };
			DEBUG_MENU::WorldLine(drawList, start, end, hit ? IM_COL32(255, 220, 80, 220) : IM_COL32(150, 150, 150, 160), 2.0f);
			if (!hit)
				continue;
			DEBUG_MENU::WorldCross(drawList, ray.hit, 0.25f, IM_COL32(255, 220, 80, 255), 2.0f);
			WORLD_DEBUG::HullRow row;
			std::string label = Format("view %d: %s", view.index, WORLD_DEBUG::ZoneName(view.zone).c_str());
			if (WORLD_DEBUG::ReadHullRow(ray.row, row))
				label += Format("\nrow %d (%s) in %s; entity %08X, instance %08X", ray.row, WORLD_DEBUG::CollisionLayerName(row.flags & 0x1F), WORLD_DEBUG::ZoneName(row.zone).c_str(),
					row.entityGuid, row.instanceGuid);
			DEBUG_MENU::WorldText(drawList, ray.hit, label.c_str(), IM_COL32(255, 230, 140, 255), -6.0f);
		}

		// The main view handed the player's zone: the feet, with the rows under them
		const ViewReading* mainView = nullptr;
		for (int i = 0; i < count; i++)
			if (views[i].main)
				mainView = &views[i];
		const DEBUG_MENU::Character* player = Player();
		Feet feet;
		if (mainView && SourceOf(*mainView) == ZoneSource::Handed && player && ReadFeet(player->object, feet))
		{
			std::string label = Format("camera's zone (from the player's feet): %s", WORLD_DEBUG::ZoneName(mainView->zone).c_str());
			for (int r = 0; r < 4; r++)
				if (feet.rows[r] >= 0)
					label += Format("\nray %d: row %d, %s", r + 1, feet.rows[r], WORLD_DEBUG::ZoneName(feet.zones[r]).c_str());
			DEBUG_MENU::WorldCross(drawList, feet.support, 0.2f, IM_COL32(140, 220, 255, 255), 2.0f);
			DEBUG_MENU::WorldText(drawList, feet.support, label.c_str(), IM_COL32(170, 230, 255, 255), 30.0f);
		}
	}

	// ---- Named controls ----

	std::string ZoneLine(const WORLD_DEBUG::Zone& zone)
	{
		return Format("%s id=%08X state=%08X (%s) priority=%d viewers=%s links=%d loading=%d composites=%d", zone.name.empty() ? "(no name)" : zone.name.c_str(), zone.id, zone.state,
			WORLD_DEBUG::ZoneStateText(zone.state).c_str(), zone.priority, ViewerBits(zone.viewers).c_str(), zone.links, zone.loading, zone.composites);
	}

	DEBUG_MENU::CommandResult NoLevel()
	{
		return { false, "No level running (or its zones cannot be read)" };
	}

	DEBUG_MENU::CommandResult ListZones(const std::string& argument)
	{
		const WORLD_DEBUG::ZoneSnapshot& zones = WORLD_DEBUG::Zones();
		if (!zones.read)
			return NoLevel();
		std::string text;
		for (const WORLD_DEBUG::Zone& zone : zones.zones)
		{
			if (argument == "loaded" && !(zone.state & kZoneLoaded))
				continue;
			if (argument == "wanted" && zone.priority < kWantedPriority)
				continue;
			if (argument != "loaded" && argument != "wanted" && !ContainsIgnoringCase(zone.name, argument.c_str()))
				continue;
			text += ZoneLine(zone) + "\n";
		}
		return { true, text.empty() ? "(none)" : text };
	}

	DEBUG_MENU::CommandResult ListLinks(const std::string& argument)
	{
		const WORLD_DEBUG::ZoneSnapshot& zones = WORLD_DEBUG::Zones();
		if (!zones.read)
			return NoLevel();
		std::string text;
		for (const WORLD_DEBUG::ZoneLink& link : zones.links)
		{
			const std::string a = WORLD_DEBUG::ZoneName(link.zoneA), b = WORLD_DEBUG::ZoneName(link.zoneB);
			if (!argument.empty() && !ContainsIgnoringCase(a, argument.c_str()) && !ContainsIgnoringCase(b, argument.c_str()))
				continue;
			text += Format("%s <-> %s open=%d locked=%d cost=%u entity=%08X instance=%08X\n", a.c_str(), b.c_str(), (link.state & kLinkOpen) ? 1 : 0,
				(link.state & kLinkLocked) ? 1 : 0, link.cost, link.entityGuid, link.entityInstance);
		}
		return { true, text.empty() ? "(none)" : text };
	}

	DEBUG_MENU::CommandResult ListViewpoints()
	{
		const WORLD_DEBUG::ZoneSnapshot& zones = WORLD_DEBUG::Zones();
		if (!zones.read)
			return NoLevel();
		std::string text;
		for (const WORLD_DEBUG::Viewpoint& viewpoint : zones.viewpoints)
			text += Format("view=%u zone=%s time=%u\n", viewpoint.view, WORLD_DEBUG::ZoneName(viewpoint.zone).c_str(), viewpoint.timeStamp);
		for (const WORLD_DEBUG::ZoneExclusion& exclusion : zones.exclusions)
			text += Format("exclusion=%s|%s streaming=%d\n", WORLD_DEBUG::ZoneName(exclusion.zoneA).c_str(), WORLD_DEBUG::ZoneName(exclusion.zoneB).c_str(), exclusion.excludeStreaming ? 1 : 0);
		return { true, text.empty() ? "(none)" : text };
	}

	DEBUG_MENU::CommandResult Status()
	{
		const WORLD_DEBUG::ZoneSnapshot& zones = WORLD_DEBUG::Zones();
		if (!zones.read)
			return { true, "level=0" };
		int loaded = 0, streaming = 0, wanted = 0, openLinks = 0;
		for (const WORLD_DEBUG::Zone& zone : zones.zones)
		{
			loaded += (zone.state & kZoneLoaded) ? 1 : 0;
			streaming += (zone.state & kZoneStreamingIn) && !(zone.state & kZoneLoaded) ? 1 : 0;
			wanted += zone.priority >= kWantedPriority ? 1 : 0;
		}
		for (const WORLD_DEBUG::ZoneLink& link : zones.links)
			openLinks += (link.state & kLinkOpen) ? 1 : 0;
		std::string text = Format("level=1\nzones=%d\nloaded=%d\nstreaming_in=%d\nwanted=%d\nlinks=%d\nopen_links=%d\nexclusions=%d\nviewpoints=%d\n",
			static_cast<int>(zones.zones.size()), loaded, streaming, wanted, static_cast<int>(zones.links.size()), openLinks, static_cast<int>(zones.exclusions.size()),
			static_cast<int>(zones.viewpoints.size()));
		text += Format("blocked=%d\nheld_up_count=%d\nscreen_fade=%d\nflush_all=%d\nloading_timer=%d\nloading_counter=%d\n", zones.blocked ? 1 : 0, zones.blockedCounter,
			zones.screenFade ? 1 : 0, zones.flushAll ? 1 : 0, zones.loadingTimer, zones.loadingCounter);
		text += Format("loader=%s\nloader_progress=%u\nsuspender=%s\nsuspender_progress=%u\nglobal_zone=%08X\n", zones.loaderBusy ? WORLD_DEBUG::ZoneName(zones.loaderZone).c_str() : "idle",
			zones.loaderProgress, zones.suspenderBusy ? WORLD_DEBUG::ZoneName(zones.suspenderZone).c_str() : "idle", zones.suspenderProgress, zones.globalZone);
		if (zones.haveCameraZone)
			text += Format("camera_zone=%s\ncamera_zone_id=%08X\n", WORLD_DEBUG::ZoneName(zones.cameraZone).c_str(), zones.cameraZone);
		return { true, text };
	}

	DEBUG_MENU::CommandResult ZoneDetail(const std::string& argument)
	{
		const WORLD_DEBUG::ZoneSnapshot& zones = WORLD_DEBUG::Zones();
		if (!zones.read)
			return NoLevel();
		if (argument.empty())
			return { false, "Give a zone's name (or part of it) or its id in hex" };
		const uint32_t asId = static_cast<uint32_t>(strtoul(argument.c_str(), nullptr, 16));
		const WORLD_DEBUG::Zone* zone = nullptr;
		for (const WORLD_DEBUG::Zone& candidate : zones.zones)
			if (_stricmp(candidate.name.c_str(), argument.c_str()) == 0 || (asId && candidate.id == asId))
				zone = &candidate;
		for (const WORLD_DEBUG::Zone& candidate : zones.zones)
			if (!zone && ContainsIgnoringCase(candidate.name, argument.c_str()))
				zone = &candidate;
		if (!zone)
			return { false, "No zone like that: " + argument };
		std::string text = ZoneLine(*zone) + Format("\nentity=%08X\ninstance=%08X\n", zone->entityGuid, zone->entityInstance);
		for (const WORLD_DEBUG::ZoneLink& link : zones.links)
			if (link.zoneA == zone->id || link.zoneB == zone->id)
				text += Format("link=%s open=%d locked=%d cost=%u\n", WORLD_DEBUG::ZoneName(link.zoneA == zone->id ? link.zoneB : link.zoneA).c_str(), (link.state & kLinkOpen) ? 1 : 0,
					(link.state & kLinkLocked) ? 1 : 0, link.cost);
		for (const std::string& name : CompositesIn(zone->object, 4096))
			text += "composite=" + name + "\n";
		return { true, text };
	}

	DEBUG_MENU::CommandResult RayReport()
	{
		if (!LevelUp())
			return { true, "level=0" };
		ViewReading views[kMaxViews];
		const int count = ReadViews(views, kMaxViews);
		RayRecord rays[kMaxViews];
		CopyRays(rays);
		const ULONGLONG now = GetTickCount64();
		std::string text = Format("hooks=%d\n", RayHooksOn() ? 1 : 0);
		for (int i = 0; i < count; i++)
		{
			const ViewReading& view = views[i];
			const ZoneSource source = SourceOf(view);
			text += Format("view=%d main=%d viewpoint=%d kind=%u kind_name=%s source=%s zone=%s zone_id=%08X\n", view.index, view.main ? 1 : 0, view.viewer, view.kind,
				ViewKindWord(view.kind), SourceWord(source), WORLD_DEBUG::ZoneName(view.zone).c_str(), view.zone);
			const RayRecord& ray = rays[view.index];
			if (ray.haveQuery && now - ray.queryAt < 2000)
				text += Format("  ray_from=%.3f,%.3f,%.3f ray_to=%.3f,%.3f,%.3f\n", ray.start[0], ray.start[1], ray.start[2], ray.end[0], ray.end[1], ray.end[2]);
			if (ray.haveResult && now - ray.resultAt < 2000)
			{
				WORLD_DEBUG::HullRow row;
				if (ray.status == kStatusBlocked && ray.row >= 0 && WORLD_DEBUG::ReadHullRow(ray.row, row))
					text += Format("  hit=1 at=%.3f,%.3f,%.3f row=%d flags=%08X layer=%s entity=%08X instance=%08X resource=%08X collision_index=%d row_zone=%s row_zone_id=%08X\n",
						ray.hit[0], ray.hit[1], ray.hit[2], ray.row, row.flags, WORLD_DEBUG::CollisionLayerName(row.flags & 0x1F), row.entityGuid, row.instanceGuid, row.resource,
						row.collisionIndex, WORLD_DEBUG::ZoneName(row.zone).c_str(), row.zone);
				else
					text += Format("  hit=0 status=%d\n", ray.status);
			}
		}
		const DEBUG_MENU::Character* player = Player();
		Feet feet;
		if (player && ReadFeet(player->object, feet))
		{
			text += Format("feet_valid=%d over_drop=%d standing=%.3f,%.3f,%.3f\n", feet.valid ? 1 : 0, feet.overDrop ? 1 : 0, feet.support[0], feet.support[1], feet.support[2]);
			for (int r = 0; r < 4; r++)
			{
				WORLD_DEBUG::HullRow row;
				if (feet.rows[r] >= 0 && WORLD_DEBUG::ReadHullRow(feet.rows[r], row))
					text += Format("feet_ray=%d row=%d entity=%08X instance=%08X layer=%s zone=%s zone_id=%08X\n", r + 1, feet.rows[r], row.entityGuid, row.instanceGuid,
						WORLD_DEBUG::CollisionLayerName(row.flags & 0x1F), WORLD_DEBUG::ZoneName(feet.zones[r]).c_str(), feet.zones[r]);
				else
					text += Format("feet_ray=%d row=-1\n", r + 1);
			}
		}
		else
			text += "feet=none\n";
		return { true, text };
	}

	DEBUG_MENU::CommandResult HullRowReport(const std::string& argument)
	{
		if (argument.empty())
			return { false, "Give a collision map row (a number)" };
		const int index = atoi(argument.c_str());
		WORLD_DEBUG::HullRow row;
		if (!WORLD_DEBUG::ReadHullRow(index, row))
			return { false, Format("Row %d cannot be read (there are %d)", index, WORLD_DEBUG::HullRowCount()) };
		return { true, Format("row=%d\nflags=%08X\nlayer=%s\nflag_words=%s\ncollision_index=%d\nresource=%08X\nentity=%08X\ninstance=%08X\nzone=%s\nzone_id=%08X", index, row.flags,
			WORLD_DEBUG::CollisionLayerName(row.flags & 0x1F), WORLD_DEBUG::CollisionFlagsText(row.flags).c_str(), row.collisionIndex, row.resource, row.entityGuid, row.instanceGuid,
			WORLD_DEBUG::ZoneName(row.zone).c_str(), row.zone) };
	}

	void NameControls()
	{
		static bool named = false;
		if (named)
			return;
		named = true;
		DEBUG_MENU::AddSwitch("world.overlay.zone_ray", &g_rayOverlay);
		DEBUG_MENU::AddSwitch("world.overlay.zone_ray_all_views", &g_rayOverlayAllViews);
		DEBUG_MENU::AddCommand("zones.status", "the zone streaming: counts, stopped for memory, loading/suspending now, the camera's zone",
			[](const std::string&) { return DEBUG_MENU::AnswerOnRenderThread([]() { return Status(); }); });
		DEBUG_MENU::AddCommand("zones.list_zones", "every zone (argument: loaded, wanted, or part of a name), one a line",
			[](const std::string& argument) { return DEBUG_MENU::AnswerOnRenderThread([argument]() { return ListZones(argument); }); });
		DEBUG_MENU::AddCommand("zones.list_links", "every zone link (argument: part of a zone's name), one a line",
			[](const std::string& argument) { return DEBUG_MENU::AnswerOnRenderThread([argument]() { return ListLinks(argument); }); });
		DEBUG_MENU::AddCommand("zones.list_viewpoints", "this frame's viewpoints and the exclusions",
			[](const std::string&) { return DEBUG_MENU::AnswerOnRenderThread([]() { return ListViewpoints(); }); });
		DEBUG_MENU::AddCommand("zones.zone", "one zone in detail (argument: its name, part of it, or its id in hex): links and composites",
			[](const std::string& argument) { return DEBUG_MENU::AnswerOnRenderThread([argument]() { return ZoneDetail(argument); }); });
		DEBUG_MENU::AddCommand("zones.ray", "why each view is in its zone: its ray and the collision row it hit, and the rows under the player's feet",
			[](const std::string&) { return DEBUG_MENU::AnswerOnRenderThread([]() { return RayReport(); }); });
		DEBUG_MENU::AddCommand("zones.hull_row", "one row of the level's collision map (argument: its number)",
			[](const std::string& argument) { return DEBUG_MENU::AnswerOnRenderThread([argument]() { return HullRowReport(argument); }); });
	}

	const bool g_controls = (NameControls(), true);
	const bool g_added = DEBUG_MENU::AddTab("Zones", 110, DrawZonesTab, "World") && DEBUG_MENU::AddTab("Zones", 111, DrawRaySection, "World")
		&& DEBUG_MENU::AddOverlay(60, DrawRayOverlay);
	const bool g_hooks = DEBUG_MENU::AddHooks(AttachRayHooks);
}

// ---- Shared with the other World views ----

const WORLD_DEBUG::ZoneSnapshot& WORLD_DEBUG::Zones()
{
	const int frame = ImGui::GetFrameCount();
	if (g_snapshotFrame != frame)
	{
		g_snapshotFrame = frame;
		ReadSnapshot(g_snapshot);
	}
	return g_snapshot;
}

std::string WORLD_DEBUG::ZoneName(uint32_t id)
{
	if (id == kNoZone)
		return "none";
	const ZoneSnapshot& zones = Zones();
	if (zones.read && id == zones.globalZone)
		return "global";
	// (a collision row in no zone - the player's trigger boxes' - holds 0)
	if (id == 0)
		return "none";
	const auto found = g_zoneById.find(id);
	if (found != g_zoneById.end() && found->second < zones.zones.size())
	{
		const Zone& zone = zones.zones[found->second];
		return zone.name.empty() ? Hex(id) : zone.name;
	}
	return Hex(id);
}

std::string WORLD_DEBUG::ZoneStateText(uint32_t state)
{
	std::string text;
	auto add = [&text](const char* word) { text += (text.empty() ? "" : ", ") + std::string(word); };
	if (state & kZoneLoaded)
		add("loaded");
	else if (state & kZoneStreamingIn)
		add("loading");
	else
		add("unloaded");
	if (state & 0x04)
		add("suspended");
	if (state & 0x10)
		add("load asked by script");
	if (state & 0x80)
		add("unload asked by script");
	if (state & 0x20)
		add("shown on load");
	if (state & 0x40)
		add("suspends on unload");
	return text;
}

bool WORLD_DEBUG::ReadHullRow(int index, HullRow& out)
{
	out = HullRow();
	const uintptr_t rowsAt = DEVTOOLS_RELATIVE_ADDRESS(kHullRows);
	const uintptr_t countAt = DEVTOOLS_RELATIVE_ADDRESS(kHullRowCount);
	if (!rowsAt || !countAt || index < 0)
		return false;
	const uint8_t* rows = Get<const uint8_t*>(reinterpret_cast<const void*>(rowsAt), 0, nullptr);
	const uint32_t count = Get<uint32_t>(reinterpret_cast<const void*>(countAt), 0, 0);
	if (!rows || static_cast<uint32_t>(index) >= count)
		return false;
	uint8_t row[kHullRowSize];
	if (!Read(rows + static_cast<size_t>(index) * kHullRowSize, row, sizeof(row)))
		return false;
	out.index = index;
	memcpy(&out.flags, row + kRowFlags, 4);
	memcpy(&out.collisionIndex, row + kRowCollisionIndex, 4);
	memcpy(&out.resource, row + kRowResource, 4);
	memcpy(&out.entityGuid, row + kRowEntity, 4);
	memcpy(&out.instanceGuid, row + kRowInstance, 4);
	memcpy(&out.zone, row + kRowZone, 4);
	return true;
}

int WORLD_DEBUG::HullRowCount()
{
	const uintptr_t countAt = DEVTOOLS_RELATIVE_ADDRESS(kHullRowCount);
	return countAt ? static_cast<int>(Get<uint32_t>(reinterpret_cast<const void*>(countAt), 0, 0)) : 0;
}

const char* WORLD_DEBUG::CollisionLayerName(int layer)
{
	static const char* const kNames[] = {
		"everything", "triggers", "camera", "standard", "transparent", "real character", "against dynamic simulated", "dynamic transparent",
		"debris", "ballistics", "line of sight", "ballistic test", "path open", "path closed", "dynamic simulated", "ragdoll",
		"water", "sound", "nothing", "ghosted", "interface", "interface test", "material test", "support test",
		"sound barrier", "player", "thrown", "player only", "high priority interface", "high priority interface test", "character triggers", "dynamic keyframed",
	};
	return layer >= 0 && layer < 32 ? kNames[layer] : "?";
}

std::string WORLD_DEBUG::CollisionFlagsText(int32_t flags)
{
	static const struct { uint32_t bit; const char* word; } kFlags[] = {
		{ 0x20, "landscape" }, { 0x40, "world" }, { 0x80, "ballistic" }, { 0x100, "fixed" }, { 0x200, "keyframed" }, { 0x400, "simulating" },
		{ 0x1000, "prebuilt" }, { 0x2000, "resource" }, { 0x4000, "system" }, { 0x20000, "reporting" },
		{ 0x40000, "force transparent" }, { 0x80000, "high priority" }, { 0x200000, "soft" },
		{ 0x400000, "reports sliding" }, { 0x800000, "submerged" }, { 0x1000000, "removed" }, { 0x2000000, "force keyframed" },
		{ 0x4000000, "ballistic only" }, { 0x8000000, "standard only" },
	};
	// Three states have a second bit for what they were before: one word for the two together, as the Inspect tab shows them
	static const struct { uint32_t now, before; const char* word; } kPairs[] = {
		{ 0x10000, 0x100000, "zero gravity" }, { 0x10000000, 0x20000000, "ghosted" }, { 0x40000000, 0x80000000, "frozen" },
	};
	std::string text = std::string("layer ") + CollisionLayerName(flags & 0x1F);
	for (const auto& flag : kFlags)
		if (static_cast<uint32_t>(flags) & flag.bit)
			text += std::string(", ") + flag.word;
	for (const auto& pair : kPairs)
	{
		const bool now = (static_cast<uint32_t>(flags) & pair.now) != 0, before = (static_cast<uint32_t>(flags) & pair.before) != 0;
		if (now || before)
			text += std::string(", ") + (now ? "" : "was ") + pair.word + (now && before ? " (now and before)" : "");
	}
	return text;
}

std::string WORLD_DEBUG::CompositeName(const void* instanceObject)
{
	const uintptr_t packAt = DEVTOOLS_RELATIVE_ADDRESS(kPackFile);
	if (!packAt || !instanceObject)
		return "";
	const uintptr_t pack = Get<uintptr_t>(reinterpret_cast<const void*>(packAt), 0, 0);
	const uint32_t packed = Get<uint32_t>(instanceObject, kInstanceTemplate, 0xFFFFFFFF);
	if (!pack || packed == 0xFFFFFFFF)
		return "";
	const void* composite = reinterpret_cast<const void*>(pack + static_cast<uintptr_t>(packed) * 4u);
	char name[256];
	CopyText(Get<const char*>(composite, 0, nullptr), name, sizeof(name));
	// The composites are named by where they were made (a path): only the last part is shown
	const char* last = name;
	for (const char* c = name; *c; c++)
		if (*c == '\\' || *c == '/')
			last = c + 1;
	return last;
}

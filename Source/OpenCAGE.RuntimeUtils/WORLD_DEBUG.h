#pragma once

#include "DevTools.h"
#include <cstdint>
#include <memory>
#include <string>
#include <vector>

/*
	What the debug menu's World views share (the Zones and Overlays tabs, and the Scripts group's Cameras tab): the
	level's zones as the zone streaming keeps them, the rows of the level's collision map, and the script entities the
	Overlays tab draws (trigger boxes, doors, splines), found by a walk over the level's entities on the entity thread.

	Addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).
*/
namespace WORLD_DEBUG
{
	// ---- Zones (WORLD_ZONES.cpp; render thread) ----

	// A zone: its streaming state (bits, see ZoneStateText), priority this frame, which views see it (a bit per view), and
	// how many links, exclusions, entities still loading and composite instances it has
	struct Zone
	{
		uint32_t id = 0;
		std::string name;
		int priority = 0;
		uint32_t viewers = 0;
		uint32_t state = 0;
		uint32_t index = 0;
		int links = 0;
		int exclusions = 0;
		int loading = 0;
		int composites = 0;
		uint32_t entityGuid = 0;     // the Zone entity it is made by: its id and the composite instance it is in
		uint32_t entityInstance = 0;
		const void* object = nullptr; // the game's zone (for reading more about one zone, render thread only)
	};

	// A link between two zones (a door, an opening): its state (bit 0 open, bit 1 locked) and cost
	struct ZoneLink
	{
		uint32_t zoneA = 0, zoneB = 0;
		uint32_t entityGuid = 0, entityInstance = 0;
		uint32_t state = 0;
		uint32_t cost = 0;
		const void* object = nullptr;
	};

	struct ZoneExclusion
	{
		uint32_t zoneA = 0, zoneB = 0;
		bool excludeStreaming = false;
	};

	struct Viewpoint
	{
		uint32_t view = 0, timeStamp = 0, zone = 0;
	};

	// The zone streaming as a whole
	struct ZoneSnapshot
	{
		bool read = false;           // false: no level, or the zones could not be read
		std::vector<Zone> zones;
		std::vector<ZoneLink> links;
		std::vector<ZoneExclusion> exclusions;
		std::vector<Viewpoint> viewpoints;
		uint32_t globalZone = 0;     // the level's global zone (always loaded)
		bool blocked = false;        // set when streaming ran short of memory
		bool screenFade = false;     // set while a script has the screen faded out (held until a fade in)
		bool flushAll = false;
		int loadingTimer = 0, loadingCounter = 0, blockedCounter = 0;
		bool loaderBusy = false, suspenderBusy = false;
		uint32_t loaderZone = 0, suspenderZone = 0;
		uint32_t loaderProgress = 0, suspenderProgress = 0;
		bool haveCameraZone = false; // the main view's zone
		uint32_t cameraZone = 0;
	};

	// This frame's zones (read at most once a frame)
	const ZoneSnapshot& Zones();
	// A zone's name by id ("global" for the global zone, "none" for -1 or 0, else the id in hex when unknown)
	std::string ZoneName(uint32_t id);
	// The state bits in words ("loaded, streaming in, ...")
	std::string ZoneStateText(uint32_t state);
	constexpr uint32_t kNoZone = 0xFFFFFFFF;

	// ---- The level's collision map rows (any thread, guarded) ----

	// A row of the collision map: what a piece of collision belongs to (an entity and the composite instance it is in), the
	// zone it puts what stands on it (or a camera above it) in, and its layer and flags
	struct HullRow
	{
		int index = -1;
		int32_t flags = 0;
		int32_t collisionIndex = 0;
		uint32_t resource = 0;
		uint32_t entityGuid = 0;
		uint32_t instanceGuid = 0;
		uint32_t zone = 0;
	};
	bool ReadHullRow(int index, HullRow& out);
	int HullRowCount();
	// The layer (flags & 0x1F) and the flags in words
	const char* CollisionLayerName(int layer);
	std::string CollisionFlagsText(int32_t flags);

	// The name of the composite an instance was made from (a composite instance entity's object), or empty
	std::string CompositeName(const void* instanceObject);

	// ---- Script entities for the overlays (WORLD_ENTITIES.cpp) ----

	enum class Kind : uint8_t { Trigger, UseTrigger, Door, Spline };
	const char* KindName(Kind kind);

	struct WorldEntity
	{
		Kind kind = Kind::Trigger;
		const void* allocation = nullptr; // the game's counted allocation of it (to check it is still the same entity)
		const uint8_t* object = nullptr;  // the entity
		const void* iface = nullptr;      // its interface (what its outputs fire from)
		uint32_t guid = 0;                // its id in its composite
		std::string composite;            // the composite it is in
		bool placed = false;              // where it is has been read
		int placeFailures = 0;            // how often reading where it is failed (not tried again after three)
		float matrix[12] = {};            // where it is, as the game keeps it: x' = m0 x + m1 y + m2 z + m3, and so on
		float halfSize[3] = {};           // trigger boxes: half the box's size along its own axes (the box stands on its
		                                  // position: its centre is half its height up its own up axis)
		int hullRow = -1;                 // trigger boxes: its row of the collision map
		uint32_t zone = 0;                // trigger boxes: the zone that row is in
		const void* zoneLink = nullptr;   // doors: the zone link it opens (the game's, kept by the zone streaming), or null
		bool scripted = false;            // doors: opened by script only
		std::vector<float> points;        // splines: world positions, three numbers a point
		bool closed = false;              // splines: the last point joins the first
	};

	struct WorldScan
	{
		uint32_t generation = 0;
		std::vector<WorldEntity> entities;
		int walked = 0;           // entities looked at by the last walk
		float walkMs = 0.0f;      // how long the last walk took
		int unplaced = 0;         // still waiting for their position to be read
		uint64_t walkedAt = 0;    // when (the tick count, milliseconds)
		bool available = true;    // false: this game release has none of the addresses the walk needs
	};

	// The latest scan (any thread; empty before the first, or with no level)
	std::shared_ptr<const WorldScan> Scan();
	// Keeps the scan going while something draws it (render thread, every frame it is wanted): the walk is repeated every
	// few seconds and positions are read a few at a time on the entity thread
	void WantScan();
	// Walks again at the next entity frame (and reads every position again)
	void Rescan();
	// The entity's state bits now (render thread), false when it is gone or cannot be read
	bool LiveState(const WorldEntity& entity, uint32_t& state);
	// When a trigger box last fired its entered / exited output (seconds ago; negative: never since the scan started)
	void LastFired(const WorldEntity& entity, double& enteredAgo, double& exitedAgo);
	// A door's zone link now (render thread): the zones it joins and whether it is open / locked
	bool LinkState(const void* zoneLink, uint32_t& zoneA, uint32_t& zoneB, uint32_t& state);

	// The entity state bits the views show
	constexpr uint32_t kStateOpened = 1u << 5, kStateClosed = 1u << 6, kStateEnabled = 1u << 7, kStateLocked = 1u << 9;
	constexpr uint32_t kStateOpening = 1u << 16, kStateClosing = 1u << 17, kStateSuspended = 1u << 19;
	constexpr uint32_t kStateZoneActive = 1u << 23, kStateLoaded = 1u << 26, kStateInitialised = 1u << 28;
}

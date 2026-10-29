#pragma once

#include "DevTools.h"
#include <cstdint>
#include <string>
#include <vector>

/*
	Navigation debugging, in the debug menu (see DEBUG_MENU.h): the nav mesh the game loaded for the level (what
	OpenCAGE's nav mesh baker wrote, as the game holds it), its doors' barriers, the path searches the AI makes, which
	parts of the mesh each kind of character can reach, and the game's own path search run on demand.

	How the game's navigation works, in short:
	- One nav mesh is loaded per level state (each level ships state_0, state_1... with a nav_mesh file each; the level's
	  scripts switch between them). It is one Detour tile, held behind the game's own lock (a critical section). Loading
	  a state first drops the mesh it has, then reads the file: if the file is missing, has the wrong version or will not
	  load, the level silently runs with no nav mesh at all. A counter goes up whenever the mesh changes.
	- Each polygon carries a word of flags: whether it is an off-mesh connection, its area id (the barrier it belongs to,
	  0 for none), whether it is enabled, which classes of character may cross it (player, alien, android, human NPC,
	  facehugger), the kind of link an off-mesh connection is (manual, teleport, traversal, wait, backstage), its height
	  class (standing, crouch, deep crouch) and its markup (normal, backstage - the vents' sheet - or expensive).
	- Doors are barriers: each door has a row in the level's barrier table (an area id, the classes it lets through now,
	  the door entity's id - which is also the id of the door's sound doorway - and its collision). As a door opens or
	  closes the game rewrites the classes and the enabled flag of every polygon with that area id.
	- The AI asks for paths through a queue, run on a worker thread; each finished search hands its result (succeeded,
	  failed, partial, the polygons crossed) to whoever asked: a character's movement, cover, a path length, a route
	  check... A search only crosses polygons its filter allows: enabled, admitting the class, an allowed link type and
	  speed for off-mesh connections, no excluded markup, and a height class in range.

	Hooks (installed with the debug menu): the loading of a nav mesh state (which state, and whether a mesh was left),
	the rewriting of a barrier's classes (the door log), and the six places a finished path search hands its result
	over (the path log). Each notes what it saw behind a lock of its own and calls the game's function.

	At each frame's presentation, while the tabs, an overlay or a command want it, the nav mesh is copied when it has
	changed (its counter, or the mesh itself), under the game's lock when that is free (otherwise last frame's copy is
	kept); the barrier table is read again each frame. Everything is read guarded: a level can be unloading.

	The path tester calls the game's own path query and its path queue, on the AI thread, as the game's own movement
	does, and lets go of the result there once it is read.
*/
namespace NAV_DEBUG
{
	// Hooked: a nav mesh state loading (the mesh dropped, then read from the state's file).
	typedef void(__thiscall* t_load_mesh_state)(void* navMesh, uint32_t state);
	inline auto load_mesh_state = reinterpret_cast<t_load_mesh_state>(DEVTOOLS_RELATIVE_ADDRESS(0x00058CA0));
	void __fastcall h_load_mesh_state(void* navMesh, void* _EDX, uint32_t state);

	// Hooked: the classes a barrier (an area id) lets through being written onto its polygons.
	typedef void(__thiscall* t_set_area_classes)(void* navMesh, uint16_t area, const uint32_t* classes);
	inline auto set_area_classes = reinterpret_cast<t_set_area_classes>(DEVTOOLS_RELATIVE_ADDRESS(0x00045900));
	void __fastcall h_set_area_classes(void* navMesh, void* _EDX, uint16_t area, const uint32_t* classes);

	// Hooked: a finished path search handed to what asked for it, one function per kind of result (all the same shape:
	// the result object, the search's result, the query).
	typedef void(__thiscall* t_populate)(void* result, const void* searchResult, const void* query);
	inline auto populate_movement = reinterpret_cast<t_populate>(DEVTOOLS_RELATIVE_ADDRESS(0x00063420));
	inline auto populate_cover = reinterpret_cast<t_populate>(DEVTOOLS_RELATIVE_ADDRESS(0x003364C0));
	inline auto populate_polys = reinterpret_cast<t_populate>(DEVTOOLS_RELATIVE_ADDRESS(0x00057050));
	inline auto populate_path = reinterpret_cast<t_populate>(DEVTOOLS_RELATIVE_ADDRESS(0x00056EC0));
	inline auto populate_length = reinterpret_cast<t_populate>(DEVTOOLS_RELATIVE_ADDRESS(0x00049450));
	inline auto populate_route = reinterpret_cast<t_populate>(DEVTOOLS_RELATIVE_ADDRESS(0x002CDEB0));
	void __fastcall h_populate_movement(void* result, void* _EDX, const void* searchResult, const void* query);
	void __fastcall h_populate_cover(void* result, void* _EDX, const void* searchResult, const void* query);
	void __fastcall h_populate_polys(void* result, void* _EDX, const void* searchResult, const void* query);
	void __fastcall h_populate_path(void* result, void* _EDX, const void* searchResult, const void* query);
	void __fastcall h_populate_length(void* result, void* _EDX, const void* searchResult, const void* query);
	void __fastcall h_populate_route(void* result, void* _EDX, const void* searchResult, const void* query);

	// Called by the game for the path tester (AI thread only): a path query built for two points and a character class
	// (the game's own, 0 player .. 9 melee human), and the path queue asked for a string pulled path (the result handed
	// back as the game's shared pointer: the object, then its count block).
	typedef void*(__thiscall* t_path_query)(void* query, const float* start, const float* end, int characterClass);
	inline auto path_query = reinterpret_cast<t_path_query>(DEVTOOLS_RELATIVE_ADDRESS(0x00044370));
	typedef void*(__thiscall* t_ask_path)(void* queue, void* resultOut, const void* query);
	inline auto ask_path = reinterpret_cast<t_ask_path>(DEVTOOLS_RELATIVE_ADDRESS(0x00013B90));

	void AttachHooks(bool attach);
	bool HooksAttached();

	// ---- A polygon's flags ----
	inline bool IsOffMesh(uint32_t area) { return (area & 1) != 0; }
	inline int AreaId(uint32_t area) { return static_cast<int>((area >> 1) & 0x1FF); }
	inline bool Enabled(uint32_t area) { return ((area >> 10) & 1) != 0; }
	inline uint32_t Admittance(uint32_t area) { return (area >> 11) & 0x1F; }
	inline int LinkType(uint32_t area) { return static_cast<int>((area >> 16) & 0xF); }
	inline int HeightClass(uint32_t area) { return static_cast<int>((area >> 20) & 3); }
	inline uint32_t Markup(uint32_t area) { return (area >> 22) & 7; }

	// Navigation classes (a bit each) and their names; markup bits; link types; height classes
	constexpr uint32_t kClassPlayer = 1, kClassAlien = 2, kClassAndroid = 4, kClassHuman = 8, kClassFacehugger = 0x10, kClassAll = 0x1F;
	constexpr uint32_t kMarkupNormal = 1, kMarkupBackstage = 2, kMarkupExpensive = 4;
	std::string ClassesText(uint32_t classes);     // "all", "none", or "player+alien"...
	const char* LinkTypeName(int type);
	const char* HeightName(int height);
	std::string MarkupText(uint32_t markup);
	const char* SpeedName(int speed);
	const char* ReasonName(int reason);           // how the game moved a query's start or end onto the mesh

	// ---- The mesh (render thread) ----
	struct Poly
	{
		uint32_t area = 0;
		uint32_t firstLink = 0xFFFFFFFF;
		uint16_t verts[6] = {};
		uint16_t neighbours[6] = {};
		int vertCount = 0;
		float centre[3] = {};
		float min[3] = {}, max[3] = {};
	};
	struct Link { uint32_t ref = 0, next = 0xFFFFFFFF; uint8_t edge = 0, side = 0; };
	struct OffMesh
	{
		float start[3] = {}, end[3] = {};
		float radius = 0.0f;
		int poly = -1;                // its polygon in the tile
		uint8_t flags = 0, side = 0;  // flags bit 0: both ways
		uint32_t ref = 0;             // what the game keeps for it (its polygon's id)
		uint32_t traversal = 0;       // the traversal it goes with (0: none)
		uint32_t entity = 0, parent = 0; // the script entity it belongs to, and the composite instance it is in
		float extraCost = 0.0f;
		int minSpeed = 0, maxSpeed = 0;
	};
	struct Mesh
	{
		bool level = false;           // a level is running
		bool haveObject = false;      // the game's nav mesh object exists
		bool have = false;            // a mesh is loaded (and was copied)
		int generation = 0;           // bumped each time the copy is made again
		uint32_t changeCounter = 0;
		uintptr_t object = 0, detour = 0, tile = 0;
		double lastUpdate = 0.0;
		// the tile's header
		int magic = 0, version = 0, tileX = 0, tileY = 0, layer = 0;
		uint32_t userId = 0;
		int polyCount = 0, vertCount = 0, maxLinkCount = 0, detailMeshCount = 0, detailVertCount = 0, detailTriCount = 0;
		int bvNodeCount = 0, offMeshConCount = 0, offMeshBase = 0;
		float walkableHeight = 0.0f, walkableRadius = 0.0f, walkableClimb = 0.0f;
		float bmin[3] = {}, bmax[3] = {};
		float bvQuantFactor = 0.0f;
		int dataSize = 0;
		// how a polygon's id is made
		int maxTiles = 0, tileIndex = 0;
		uint32_t saltBits = 0, tileBits = 0, polyBits = 0, tileSalt = 0;
		std::vector<float> verts;     // x, y, z each
		std::vector<Poly> polys;
		std::vector<Link> links;
		std::vector<OffMesh> offMesh;
		int lockMisses = 0;           // frames the game's lock was busy (the copy kept)
		int copies = 0;
	};
	const Mesh& Read();

	// A polygon's id (0: none), and the polygon an id names (-1: not this mesh's, or a stale id)
	uint32_t PolyRef(int poly);
	struct Decoded { uint32_t salt = 0, tile = 0, poly = 0; bool valid = false; std::string why; };
	Decoded Decode(uint32_t ref);
	int PolyIndex(uint32_t ref);
	// The polygon under a point (the nearest one below it, or just above, within 2 m), and the height there; -1 none.
	// Off-mesh connections are left out.
	int PolyAt(const float position[3], float* height = nullptr);
	// How long since a character last pathed through a polygon (in the game's ticks, 30 a second; 0xFFFF never) and
	// who (the character's number), read live; false when it cannot be read.
	bool Freshness(int poly, uint16_t& ticks, uint8_t& character);

	// ---- The barriers (render thread; read with the mesh) ----
	struct Barrier
	{
		int area = 0;
		uint32_t classes = 0;         // what it lets through now
		uint32_t entity = 0;          // the door's id (also its sound doorway's)
		uint32_t handleEntity = 0, handleParent = 0; // the barrier entity, and the composite instance it is in
		bool haveCollision = false;
		int collisionFlags = 0, collisionRow = -1;
		uint32_t zone = 0;
		int polys = 0;                // with its area id
		int enabledPolys = 0;
		float centre[3] = {};
	};
	const std::vector<Barrier>& Barriers();
	const Barrier* BarrierByArea(int area);

	// ---- What the hooks noted ----
	struct LoadRecord
	{
		float time = 0.0f;
		uint32_t state = 0;
		bool loaded = false;          // a mesh was there afterwards
		int polys = 0;
		int barriers = 0;             // barrier rows put back onto it as it loaded
	};
	struct BarrierRecord
	{
		float time = 0.0f;
		int area = 0;
		int before = -1;              // the classes it had (-1: not seen since the level loaded)
		uint32_t after = 0;
		bool atLoad = false;          // the state's loading putting every barrier back
		int sets = 1;
	};
	enum PathKind { kMovement, kCover, kPolys, kPath, kLength, kRoute, kPathKinds };
	const char* PathKindName(int kind);
	struct Filter
	{
		uint32_t include = kClassAll, linkTypes = 0x1F;
		int heightMax = 1, heightMin = 0;
		int speedMin = 0, speedMax = 3;
		uint32_t excludeMarkup = kMarkupBackstage;
	};
	struct PathRecord
	{
		uint32_t serial = 0;
		float time = 0.0f;
		int kind = 0;
		Filter filter;
		float start[3] = {}, end[3] = {};
		float originalStart[3] = {}, originalEnd[3] = {};
		int startReason = 0, endReason = 0;
		uint32_t startRef = 0, endRef = 0;
		uint32_t status = 0;          // the search's: bit 31 failed, bit 30 succeeded, bit 6 partial
		uint32_t resultStart = 0, resultEnd = 0;
		float resultStartPos[3] = {}, resultEndPos[3] = {};
		int length = 0;               // polygons along it
		uint32_t lastPoly = 0;
		bool failed = false, partial = false;
	};
	struct PathCounts { int total[kPathKinds] = {}, failed[kPathKinds] = {}, partial[kPathKinds] = {}; };
	void Hooked(std::vector<LoadRecord>& loads, std::vector<BarrierRecord>& barriers, std::vector<PathRecord>& paths, PathCounts& counts);
	void ClearLogs();
	int CurrentState();               // the state the last load was for (-1: none seen since the debug menu attached)
	std::string StatusText(uint32_t status);

	// ---- Who can get where ----
	// The game's own test of a polygon against a path search's filter
	bool PassFilter(const Filter& filter, int poly);
	// The filter the game builds for a class of character's path queries (its navigation class, standing and crouch
	// heights, backstage excluded), with the vents and deep crouch allowed or not
	Filter ClassFilter(uint32_t navigationClass, bool backstage, bool deepCrouch);
	struct Flood
	{
		bool done = false;
		int generation = -1;          // the mesh copy it was made from
		uint32_t changeCounter = 0;
		Filter filter;
		int seed = -1;
		std::vector<uint8_t> reached; // per polygon: 0 not passable, 1 passable but not reached, 2 reached from the seed
		std::vector<int> island;      // per polygon: its island (-1: not passable), largest first
		struct Island { int polys = 0; float area = 0.0f; float centre[3] = {}; bool hasSeed = false; };
		std::vector<Island> islands;
		int reachedCount = 0, passableCount = 0;
	};
	// Floods out from a polygon over the mesh's links with a filter (directed: one-way links one way only), and splits
	// the passable polygons into islands (joined by links either way).
	void RunFlood(Flood& flood, const Filter& filter, int seed);

	// ---- The path tester (the game's own search) ----
	struct PathTest
	{
		int state = 0;                // 0 idle, 1 asked for (AI thread), 2 waiting for the queue, 3 done, 4 failed to ask
		float start[3] = {}, end[3] = {};
		int characterClass = 1;
		bool backstage = false, deepCrouch = false;
		int status = 0;               // 1 succeeded, 2 failed (a partial path counts as failed)
		float length = 0.0f;
		std::vector<float> points;    // x, y, z each
		float askedAt = 0.0f;
		std::string message;
	};
	// Render thread: asks (the work goes to the AI thread), and follows the result each frame.
	void StartPathTest(const float start[3], const float end[3], int characterClass, bool backstage, bool deepCrouch);
	const PathTest& UpdatePathTest();
	// Whether the tester has anything to show (asked, waiting or answered)
	bool PathTestActive();
	void ClearPathTest();

	// The game time now (seconds; 0 when unknown)
	float Now();
}

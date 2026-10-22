#include "NAV_DEBUG.h"
#include "DEBUG_MENU.h"
#include "LIVE_LINK.h"

#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <deque>
#include <mutex>
#include <new>
#include <string>
#include <unordered_map>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <detours.h>
#include <imgui.h>

/*
	Addresses and layouts are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).

	See NAV_DEBUG.h for what is read and why. What the hooks note goes into lists behind g_hookMutex; the copy of the
	mesh and the barrier table are only touched on the render thread.
*/
namespace
{
	// ---- Where things are ----

	// Where the game keeps its pointer to its globals; in them, the nav mesh object and the path queue (pointers)
	constexpr uintptr_t kGameGlobals = 0x012F0C88;
	constexpr uint32_t kGlobalsNavMesh = 0x24;
	constexpr uint32_t kGlobalsPathQueue = 0x2C;
	// The game clock: the game time now (a double)
	constexpr uintptr_t kGameTime = 0x012410A8;

	// The nav mesh object: its lock (a critical section) first, the Detour mesh (a shared pointer: the object, then its
	// count block), the barrier table (a shared pointer to a list: its first and last rows), the change counter and the
	// time the mesh was last updated (a double)
	constexpr uint32_t kNavDetour = 0x18;
	constexpr uint32_t kNavBarriers = 0x34;
	constexpr uint32_t kNavCounter = 0x3C;
	constexpr uint32_t kNavUpdated = 0x60;

	// The Detour mesh: how many tiles it can hold, its tiles (0x40 each), and the bits of a polygon's id given to the
	// salt, the tile and the polygon
	constexpr uint32_t kDetourMaxTiles = 0x30;
	constexpr uint32_t kDetourTiles = 0x44;
	constexpr uint32_t kDetourSaltBits = 0x48;
	constexpr uint32_t kDetourTileBits = 0x4C;
	constexpr uint32_t kDetourPolyBits = 0x50;
	constexpr uint32_t kTileStride = 0x40;
	constexpr int kMaxTilesLooked = 64;

	// A tile: its salt, its header, its polygons, their run-time data (4 bytes each: ticks since a character last
	// pathed through, and which), its vertices, links and off-mesh connections, and the size of its data
	constexpr uint32_t kTileSalt = 0x00;
	constexpr uint32_t kTileHeader = 0x08;
	constexpr uint32_t kTilePolys = 0x0C;
	constexpr uint32_t kTileRuntime = 0x10;
	constexpr uint32_t kTileVerts = 0x14;
	constexpr uint32_t kTileLinks = 0x18;
	constexpr uint32_t kTileOffMesh = 0x2C;
	constexpr uint32_t kTileDataSize = 0x34;

	// The raw layouts as the game holds them
	struct RawHeader
	{
		int magic, version, x, y, layer;
		uint32_t userId;
		int polyCount, vertCount, maxLinkCount, detailMeshCount, detailVertCount, detailTriCount, bvNodeCount;
		int offMeshConCount, offMeshBase;
		float walkableHeight, walkableRadius, walkableClimb;
		float bmin[3], bmax[3];
		float bvQuantFactor;
	};
	static_assert(sizeof(RawHeader) == 0x64, "a tile header is 0x64 bytes");
	struct RawPoly
	{
		uint32_t firstLink;
		uint16_t verts[6];
		uint16_t neighbours[6];
		uint8_t vertCount;
		uint8_t pad[3];
		uint32_t area;
	};
	static_assert(sizeof(RawPoly) == 0x24, "a polygon is 0x24 bytes");
	struct RawLink
	{
		uint32_t ref, next;
		uint8_t edge, side, bmin, bmax;
	};
	static_assert(sizeof(RawLink) == 0x0C, "a link is 0x0C bytes");
	struct RawOffMesh
	{
		float pos[6];
		float radius;
		uint16_t poly;
		uint8_t flags, side;
		uint32_t ref, traversal, entity, parent;
		float extraCost;
		int minSpeed, maxSpeed;
	};
	static_assert(sizeof(RawOffMesh) == 0x3C, "an off-mesh connection is 0x3C bytes");

	// A barrier row (0x18): its area id (16 bits), the classes it lets through now, the door's id, the barrier entity's
	// two ids and its collision (a pointer to the collision row: its flags first, its row number at +0x1A, its zone at
	// +0x1C)
	struct RawBarrier
	{
		uint16_t area;
		uint16_t pad;
		uint32_t classes, entity, handleEntity, handleParent;
		uint32_t mapping;
	};
	static_assert(sizeof(RawBarrier) == 0x18, "a barrier row is 0x18 bytes");
	constexpr uint32_t kMappingFlags = 0x00;
	constexpr uint32_t kMappingRow = 0x1A;
	constexpr uint32_t kMappingZone = 0x1C;
	constexpr int kMaxBarriers = 4096;

	// A path query: its filter (what may be crossed: classes, link types, the highest and lowest height class, the
	// slowest and fastest speed, the markup left out), where it starts and ends (as asked, then as moved onto the mesh),
	// why each was moved and the polygons they are on
	constexpr uint32_t kQueryStart = 0x5C;
	constexpr uint32_t kQueryEnd = 0x68;
	constexpr uint32_t kQueryOriginalStart = 0x74;
	constexpr uint32_t kQueryOriginalEnd = 0x80;
	constexpr uint32_t kQueryStartReason = 0x8C;
	constexpr uint32_t kQueryEndReason = 0x90;
	constexpr uint32_t kQueryStartRef = 0x94;
	constexpr uint32_t kQueryEndRef = 0x98;
	constexpr uint32_t kQueryFilterHeightMax = 0x08;
	constexpr uint32_t kQueryFilterMarkup = 0x18;
	constexpr uint32_t kQuerySize = 0xAC;
	// A search's result: its status, the polygons it started and ended on, where, the polygons crossed and how many
	constexpr uint32_t kResultStatus = 0x08;
	constexpr uint32_t kResultStartRef = 0x0C;
	constexpr uint32_t kResultEndRef = 0x10;
	constexpr uint32_t kResultStartPos = 0x14;
	constexpr uint32_t kResultEndPos = 0x20;
	constexpr uint32_t kResultPath = 0x2C;
	constexpr uint32_t kResultLength = 0x42C;
	constexpr int kResultMaxPath = 256;
	// A finished string pulled path as the queue hands it back: done (1), its status (1 succeeded, 2 failed), its points
	// (a list of x, y, z: first and last) and its length
	constexpr uint32_t kPathDone = 0x04;
	constexpr uint32_t kPathStatus = 0x0C;
	constexpr uint32_t kPathPoints = 0x10;
	constexpr uint32_t kPathLength = 0x20;

	constexpr size_t kMaxLoads = 64;
	constexpr size_t kMaxBarrierLog = 512;
	constexpr size_t kMaxPaths = 512;

	// ---- What the hooks note ----

	std::mutex g_hookMutex;
	std::deque<NAV_DEBUG::LoadRecord> g_loads;
	std::deque<NAV_DEBUG::BarrierRecord> g_barrierLog;
	std::deque<NAV_DEBUG::PathRecord> g_paths;
	NAV_DEBUG::PathCounts g_counts;
	std::unordered_map<int, uint32_t> g_lastClasses;  // per area id, the classes last written
	uint32_t g_pathSerial = 0;
	int g_loadBarriers = 0;                           // barriers put back by the state loading now
	std::atomic<int> g_state = -1;
	std::atomic<int> g_loadSerial = 0;                // bumped by every state load
	std::atomic<DWORD> g_loadingThread = 0;           // the thread loading a state, while it does
	std::atomic<bool> g_attached = false;
	bool g_hooked[8] = {};

	// ---- The copy (render thread) ----

	NAV_DEBUG::Mesh g_mesh;
	int g_meshFrame = -1;
	uintptr_t g_runtime = 0;                          // the tile's per-polygon run-time data (read live)
	std::vector<NAV_DEBUG::Barrier> g_barriers;

	template <typename T> T At(uintptr_t base, uint32_t offset)
	{
		return *reinterpret_cast<const T*>(base + offset);
	}

	float ReadNow()
	{
		__try
		{
			const uintptr_t time = DEVTOOLS_RELATIVE_ADDRESS(kGameTime);
			return time ? static_cast<float>(*reinterpret_cast<const double*>(time)) : 0.0f;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0.0f;
		}
	}

	// A level being played: its entities up and the game in gameplay. A level closing (to the menu, the next level, or a
	// checkpoint reload) leaves gameplay before it frees the nav mesh and the cover system, while their pointers still
	// point at them - and the game's lock in the nav mesh must never be taken once it is freed.
	constexpr int kGameplay = 4;

	bool AnyLevel()
	{
		__try
		{
			return LIVE_LINK::LevelRunning() && LIVE_LINK::GameState() == kGameplay;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// The game's globals' field (a pointer), 0 when there is none
	uintptr_t GlobalsField(uint32_t offset)
	{
		__try
		{
			const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kGameGlobals);
			if (!at)
				return 0;
			const uintptr_t globals = *reinterpret_cast<const uintptr_t*>(at);
			return globals ? *reinterpret_cast<const uintptr_t*>(globals + offset) : 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	bool TryLock(uintptr_t object)
	{
		__try
		{
			return TryEnterCriticalSection(reinterpret_cast<LPCRITICAL_SECTION>(object)) != FALSE;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	void Unlock(uintptr_t object)
	{
		__try
		{
			LeaveCriticalSection(reinterpret_cast<LPCRITICAL_SECTION>(object));
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
		}
	}

	bool CopyBytes(void* to, uintptr_t from, size_t size)
	{
		if (!size)
			return true;
		if (!from)
			return false;
		__try
		{
			memcpy(to, reinterpret_cast<const void*>(from), size);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// What the mesh is now (with the game's lock held): the pointers, the counters and the tile's header
	struct Pointers
	{
		uintptr_t detour = 0, tile = 0, header = 0, polys = 0, runtime = 0, verts = 0, links = 0, offMesh = 0, barriers = 0;
		uint32_t counter = 0;
		double updated = 0.0;
		int maxTiles = 0, tileIndex = -1, dataSize = 0;
		uint32_t saltBits = 0, tileBits = 0, polyBits = 0, salt = 0;
		RawHeader header_ = {};
	};

	bool ReadPointers(uintptr_t object, Pointers& out)
	{
		__try
		{
			out.counter = At<uint32_t>(object, kNavCounter);
			out.updated = At<double>(object, kNavUpdated);
			out.barriers = At<uintptr_t>(object, kNavBarriers);
			out.detour = At<uintptr_t>(object, kNavDetour);
			if (!out.detour)
				return true;
			out.maxTiles = At<int>(out.detour, kDetourMaxTiles);
			out.saltBits = At<uint32_t>(out.detour, kDetourSaltBits);
			out.tileBits = At<uint32_t>(out.detour, kDetourTileBits);
			out.polyBits = At<uint32_t>(out.detour, kDetourPolyBits);
			const uintptr_t tiles = At<uintptr_t>(out.detour, kDetourTiles);
			if (!tiles || out.maxTiles <= 0 || out.polyBits > 31 || out.tileBits > 31 || out.saltBits > 31)
				return true;
			// The level's one tile: the first that holds a mesh
			for (int i = 0; i < out.maxTiles && i < kMaxTilesLooked; i++)
			{
				const uintptr_t tile = tiles + static_cast<uintptr_t>(i) * kTileStride;
				const uintptr_t header = At<uintptr_t>(tile, kTileHeader);
				if (!header)
					continue;
				out.tile = tile;
				out.tileIndex = i;
				out.header = header;
				out.salt = At<uint32_t>(tile, kTileSalt);
				out.polys = At<uintptr_t>(tile, kTilePolys);
				out.runtime = At<uintptr_t>(tile, kTileRuntime);
				out.verts = At<uintptr_t>(tile, kTileVerts);
				out.links = At<uintptr_t>(tile, kTileLinks);
				out.offMesh = At<uintptr_t>(tile, kTileOffMesh);
				out.dataSize = At<int>(tile, kTileDataSize);
				memcpy(&out.header_, reinterpret_cast<const void*>(header), sizeof(RawHeader));
				break;
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// The barrier table's rows (up to max), -1 when it cannot be read
	int ReadBarrierRows(uintptr_t storage, RawBarrier* out, int max)
	{
		__try
		{
			if (!storage)
				return 0;
			const uintptr_t first = At<uintptr_t>(storage, 0);
			const uintptr_t last = At<uintptr_t>(storage, 4);
			if (!first || last < first || (last - first) % sizeof(RawBarrier) != 0)
				return 0;
			const int count = (std::min)(static_cast<int>((last - first) / sizeof(RawBarrier)), max);
			memcpy(out, reinterpret_cast<const void*>(first), count * sizeof(RawBarrier));
			return count;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return -1;
		}
	}

	void ReadMapping(uintptr_t mapping, NAV_DEBUG::Barrier& out)
	{
		__try
		{
			if (!mapping)
				return;
			out.collisionFlags = At<int>(mapping, kMappingFlags);
			out.collisionRow = At<uint16_t>(mapping, kMappingRow);
			out.zone = At<uint32_t>(mapping, kMappingZone);
			out.haveCollision = true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			out.haveCollision = false;
		}
	}

	void ClearMesh(NAV_DEBUG::Mesh& mesh)
	{
		const int generation = mesh.generation;
		const int lockMisses = mesh.lockMisses;
		const int copies = mesh.copies;
		const bool level = mesh.level, haveObject = mesh.haveObject;
		const uintptr_t object = mesh.object;
		const bool had = mesh.have;
		mesh = NAV_DEBUG::Mesh();
		mesh.generation = had ? generation + 1 : generation;
		mesh.lockMisses = lockMisses;
		mesh.copies = copies;
		mesh.level = level;
		mesh.haveObject = haveObject;
		mesh.object = object;
		g_runtime = 0;
	}

	// The copy made again from what the game holds (with its lock held): false when it could not be read whole
	bool CopyMesh(const Pointers& p, NAV_DEBUG::Mesh& mesh)
	{
		const RawHeader& h = p.header_;
		if (h.polyCount < 0 || h.polyCount > 65535 || h.vertCount < 0 || h.vertCount > 65535 || h.maxLinkCount < 0 || h.maxLinkCount > (1 << 20) ||
			h.offMeshConCount < 0 || h.offMeshConCount > 65535 || !p.polys || !p.verts)
			return false;
		std::vector<RawPoly> polys(h.polyCount);
		std::vector<float> verts(static_cast<size_t>(h.vertCount) * 3);
		std::vector<RawLink> links(h.maxLinkCount);
		std::vector<RawOffMesh> offMesh(h.offMeshConCount);
		if (!CopyBytes(polys.data(), p.polys, polys.size() * sizeof(RawPoly)) || !CopyBytes(verts.data(), p.verts, verts.size() * sizeof(float)) ||
			(p.links && !CopyBytes(links.data(), p.links, links.size() * sizeof(RawLink))) ||
			(p.offMesh && !CopyBytes(offMesh.data(), p.offMesh, offMesh.size() * sizeof(RawOffMesh))))
			return false;
		if (!p.links)
			links.clear();
		if (!p.offMesh)
			offMesh.clear();

		mesh.have = true;
		mesh.detour = p.detour;
		mesh.tile = p.tile;
		mesh.changeCounter = p.counter;
		mesh.magic = h.magic;
		mesh.version = h.version;
		mesh.tileX = h.x;
		mesh.tileY = h.y;
		mesh.layer = h.layer;
		mesh.userId = h.userId;
		mesh.polyCount = h.polyCount;
		mesh.vertCount = h.vertCount;
		mesh.maxLinkCount = h.maxLinkCount;
		mesh.detailMeshCount = h.detailMeshCount;
		mesh.detailVertCount = h.detailVertCount;
		mesh.detailTriCount = h.detailTriCount;
		mesh.bvNodeCount = h.bvNodeCount;
		mesh.offMeshConCount = h.offMeshConCount;
		mesh.offMeshBase = h.offMeshBase;
		mesh.walkableHeight = h.walkableHeight;
		mesh.walkableRadius = h.walkableRadius;
		mesh.walkableClimb = h.walkableClimb;
		memcpy(mesh.bmin, h.bmin, sizeof(mesh.bmin));
		memcpy(mesh.bmax, h.bmax, sizeof(mesh.bmax));
		mesh.bvQuantFactor = h.bvQuantFactor;
		mesh.dataSize = p.dataSize;
		mesh.maxTiles = p.maxTiles;
		mesh.tileIndex = p.tileIndex;
		mesh.saltBits = p.saltBits;
		mesh.tileBits = p.tileBits;
		mesh.polyBits = p.polyBits;
		mesh.tileSalt = p.salt;
		mesh.verts.swap(verts);

		// Each polygon with its middle and bounds (from its vertices; an off-mesh connection's two are its ends)
		mesh.polys.resize(polys.size());
		const int vertCount = h.vertCount;
		for (size_t i = 0; i < polys.size(); i++)
		{
			const RawPoly& raw = polys[i];
			NAV_DEBUG::Poly& poly = mesh.polys[i];
			poly.area = raw.area;
			poly.firstLink = raw.firstLink;
			poly.vertCount = (std::min)(static_cast<int>(raw.vertCount), 6);
			memcpy(poly.verts, raw.verts, sizeof(poly.verts));
			memcpy(poly.neighbours, raw.neighbours, sizeof(poly.neighbours));
			int used = 0;
			for (int v = 0; v < poly.vertCount; v++)
			{
				if (raw.verts[v] >= vertCount)
					continue;
				const float* point = &mesh.verts[raw.verts[v] * 3];
				for (int axis = 0; axis < 3; axis++)
				{
					poly.centre[axis] += point[axis];
					poly.min[axis] = used ? (std::min)(poly.min[axis], point[axis]) : point[axis];
					poly.max[axis] = used ? (std::max)(poly.max[axis], point[axis]) : point[axis];
				}
				used++;
			}
			if (used)
				for (float& value : poly.centre)
					value /= static_cast<float>(used);
		}
		mesh.links.resize(links.size());
		for (size_t i = 0; i < links.size(); i++)
		{
			mesh.links[i].ref = links[i].ref;
			mesh.links[i].next = links[i].next;
			mesh.links[i].edge = links[i].edge;
			mesh.links[i].side = links[i].side;
		}
		mesh.offMesh.resize(offMesh.size());
		for (size_t i = 0; i < offMesh.size(); i++)
		{
			const RawOffMesh& raw = offMesh[i];
			NAV_DEBUG::OffMesh& out = mesh.offMesh[i];
			memcpy(out.start, raw.pos, sizeof(out.start));
			memcpy(out.end, raw.pos + 3, sizeof(out.end));
			out.radius = raw.radius;
			out.poly = h.offMeshBase + static_cast<int>(i);
			out.flags = raw.flags;
			out.side = raw.side;
			out.ref = raw.ref;
			out.traversal = raw.traversal;
			out.entity = raw.entity;
			out.parent = raw.parent;
			out.extraCost = raw.extraCost;
			out.minSpeed = raw.minSpeed;
			out.maxSpeed = raw.maxSpeed;
		}
		mesh.generation++;
		mesh.copies++;
		return true;
	}

	// The barriers' polygon counts and middles, from the copy
	void CountBarrierPolys(std::vector<NAV_DEBUG::Barrier>& barriers, const NAV_DEBUG::Mesh& mesh)
	{
		std::unordered_map<int, size_t> byArea;
		for (size_t i = 0; i < barriers.size(); i++)
			byArea.emplace(barriers[i].area, i);
		for (const NAV_DEBUG::Poly& poly : mesh.polys)
		{
			const int area = NAV_DEBUG::AreaId(poly.area);
			if (!area || NAV_DEBUG::IsOffMesh(poly.area))
				continue;
			const auto found = byArea.find(area);
			if (found == byArea.end())
				continue;
			NAV_DEBUG::Barrier& barrier = barriers[found->second];
			for (int axis = 0; axis < 3; axis++)
				barrier.centre[axis] += poly.centre[axis];
			barrier.polys++;
			if (NAV_DEBUG::Enabled(poly.area))
				barrier.enabledPolys++;
		}
		for (NAV_DEBUG::Barrier& barrier : barriers)
			if (barrier.polys)
				for (float& value : barrier.centre)
					value /= static_cast<float>(barrier.polys);
	}

	void ReadFrame()
	{
		NAV_DEBUG::Mesh& mesh = g_mesh;
		mesh.level = AnyLevel();
		const uintptr_t object = mesh.level ? GlobalsField(kGlobalsNavMesh) : 0;
		mesh.haveObject = object != 0;
		mesh.object = object;
		if (!object)
		{
			if (mesh.have)
				ClearMesh(mesh);
			g_barriers.clear();
			return;
		}
		if (!TryLock(object))
		{
			mesh.lockMisses++;
			return;
		}
		Pointers p;
		const bool read = ReadPointers(object, p);
		bool copied = false;
		if (read && p.detour && p.tile)
		{
			const bool changed = !mesh.have || p.detour != mesh.detour || p.tile != mesh.tile || p.counter != mesh.changeCounter ||
				p.salt != mesh.tileSalt || p.header_.polyCount != mesh.polyCount;
			if (changed)
			{
				// The game's lock is held: a copy too big for memory is given up, never thrown past the unlock
				bool whole = false;
				try
				{
					whole = CopyMesh(p, mesh);
				}
				catch (const std::bad_alloc&)
				{
					whole = false;
				}
				if (!whole)
					ClearMesh(mesh);
				else
					copied = true;
			}
			g_runtime = p.runtime;
		}
		else if (mesh.have)
			ClearMesh(mesh);
		if (read)
			mesh.lastUpdate = p.updated;

		// The barrier table, every frame (its classes change as doors open and close)
		static RawBarrier rows[kMaxBarriers];
		const int count = read ? ReadBarrierRows(p.barriers, rows, kMaxBarriers) : -1;
		Unlock(object);

		if (count >= 0)
		{
			const bool sameRows = static_cast<int>(g_barriers.size()) == count && !copied;
			if (!sameRows)
				g_barriers.assign(count, NAV_DEBUG::Barrier());
			for (int i = 0; i < count; i++)
			{
				NAV_DEBUG::Barrier& barrier = g_barriers[i];
				barrier.area = rows[i].area;
				barrier.classes = rows[i].classes;
				barrier.entity = rows[i].entity;
				barrier.handleEntity = rows[i].handleEntity;
				barrier.handleParent = rows[i].handleParent;
				if (!sameRows)
					ReadMapping(rows[i].mapping, barrier);
			}
			if (!sameRows && mesh.have)
				CountBarrierPolys(g_barriers, mesh);
		}
	}

	// ---- Polygons ----

	// Whether a point is inside a polygon seen from above (either winding)
	bool InsideXZ(const NAV_DEBUG::Mesh& mesh, const NAV_DEBUG::Poly& poly, float x, float z)
	{
		int sign = 0;
		for (int i = 0; i < poly.vertCount; i++)
		{
			const int a = poly.verts[i], b = poly.verts[(i + 1) % poly.vertCount];
			if (a * 3 + 2 >= static_cast<int>(mesh.verts.size()) || b * 3 + 2 >= static_cast<int>(mesh.verts.size()))
				return false;
			const float* pa = &mesh.verts[a * 3];
			const float* pb = &mesh.verts[b * 3];
			const float cross = (pb[0] - pa[0]) * (z - pa[2]) - (pb[2] - pa[2]) * (x - pa[0]);
			if (std::fabs(cross) < 1e-6f)
				continue;
			const int s = cross > 0.0f ? 1 : -1;
			if (sign && s != sign)
				return false;
			sign = s;
		}
		return true;
	}

	// The height of a polygon at a point inside it (seen from above), from the triangle of its fan holding the point
	float HeightAt(const NAV_DEBUG::Mesh& mesh, const NAV_DEBUG::Poly& poly, float x, float z)
	{
		const float* p0 = &mesh.verts[poly.verts[0] * 3];
		for (int i = 1; i + 1 < poly.vertCount; i++)
		{
			const float* p1 = &mesh.verts[poly.verts[i] * 3];
			const float* p2 = &mesh.verts[poly.verts[i + 1] * 3];
			const float d = (p1[2] - p2[2]) * (p0[0] - p2[0]) + (p2[0] - p1[0]) * (p0[2] - p2[2]);
			if (std::fabs(d) < 1e-9f)
				continue;
			const float a = ((p1[2] - p2[2]) * (x - p2[0]) + (p2[0] - p1[0]) * (z - p2[2])) / d;
			const float b = ((p2[2] - p0[2]) * (x - p2[0]) + (p0[0] - p2[0]) * (z - p2[2])) / d;
			const float c = 1.0f - a - b;
			if (a >= -0.01f && b >= -0.01f && c >= -0.01f)
				return a * p0[1] + b * p1[1] + c * p2[1];
		}
		return poly.centre[1];
	}

	bool ReadFreshness(uintptr_t runtime, int poly, uint16_t& ticks, uint8_t& character)
	{
		__try
		{
			if (!runtime)
				return false;
			const uintptr_t at = runtime + static_cast<uintptr_t>(poly) * 4;
			ticks = At<uint16_t>(at, 0);
			character = At<uint8_t>(at, 2);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// ---- The hooks ----

	bool ReadU32(const void* at, uint32_t& out)
	{
		__try
		{
			if (!at)
				return false;
			out = *static_cast<const uint32_t*>(at);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// After a state has loaded: whether a mesh was left, and its polygon count
	void ReadLoaded(uintptr_t object, bool& loaded, int& polys)
	{
		loaded = false;
		polys = 0;
		__try
		{
			const uintptr_t detour = At<uintptr_t>(object, kNavDetour);
			loaded = detour != 0;
			if (!detour)
				return;
			const uintptr_t tiles = At<uintptr_t>(detour, kDetourTiles);
			const int maxTiles = At<int>(detour, kDetourMaxTiles);
			for (int i = 0; tiles && i < maxTiles && i < kMaxTilesLooked; i++)
			{
				const uintptr_t header = At<uintptr_t>(tiles + static_cast<uintptr_t>(i) * kTileStride, kTileHeader);
				if (header)
				{
					polys = At<RawHeader>(header, 0).polyCount;
					break;
				}
			}
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
		}
	}

	bool ReadPathRecord(const void* searchResult, const void* query, NAV_DEBUG::PathRecord& out)
	{
		__try
		{
			const uintptr_t q = reinterpret_cast<uintptr_t>(query);
			const uintptr_t r = reinterpret_cast<uintptr_t>(searchResult);
			if (!q || !r)
				return false;
			out.filter.include = At<uint32_t>(q, 0x00);
			out.filter.linkTypes = At<uint32_t>(q, 0x04);
			out.filter.heightMax = At<int>(q, 0x08);
			out.filter.heightMin = At<int>(q, 0x0C);
			out.filter.speedMin = At<int>(q, 0x10);
			out.filter.speedMax = At<int>(q, 0x14);
			out.filter.excludeMarkup = At<uint32_t>(q, 0x18);
			memcpy(out.start, reinterpret_cast<const void*>(q + kQueryStart), sizeof(out.start));
			memcpy(out.end, reinterpret_cast<const void*>(q + kQueryEnd), sizeof(out.end));
			memcpy(out.originalStart, reinterpret_cast<const void*>(q + kQueryOriginalStart), sizeof(out.originalStart));
			memcpy(out.originalEnd, reinterpret_cast<const void*>(q + kQueryOriginalEnd), sizeof(out.originalEnd));
			out.startReason = At<int>(q, kQueryStartReason);
			out.endReason = At<int>(q, kQueryEndReason);
			out.startRef = At<uint32_t>(q, kQueryStartRef);
			out.endRef = At<uint32_t>(q, kQueryEndRef);
			out.status = At<uint32_t>(r, kResultStatus);
			out.resultStart = At<uint32_t>(r, kResultStartRef);
			out.resultEnd = At<uint32_t>(r, kResultEndRef);
			memcpy(out.resultStartPos, reinterpret_cast<const void*>(r + kResultStartPos), sizeof(out.resultStartPos));
			memcpy(out.resultEndPos, reinterpret_cast<const void*>(r + kResultEndPos), sizeof(out.resultEndPos));
			out.length = At<int>(r, kResultLength);
			if (out.length > 0 && out.length <= kResultMaxPath)
				out.lastPoly = At<uint32_t>(r, kResultPath + static_cast<uint32_t>(out.length - 1) * 4);
			out.failed = (out.status & 0x80000000u) != 0;
			out.partial = (out.status & 0x40u) != 0;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	void NotePath(int kind, const void* searchResult, const void* query)
	{
		if (!g_attached)
			return;
		NAV_DEBUG::PathRecord record;
		if (!ReadPathRecord(searchResult, query, record))
			return;
		record.kind = kind;
		record.time = ReadNow();
		std::lock_guard<std::mutex> lock(g_hookMutex);
		record.serial = ++g_pathSerial;
		g_counts.total[kind]++;
		if (record.failed)
			g_counts.failed[kind]++;
		if (record.partial)
			g_counts.partial[kind]++;
		g_paths.push_back(record);
		while (g_paths.size() > kMaxPaths)
			g_paths.pop_front();
	}

	// ---- The path tester ----

	struct Tester
	{
		std::mutex mutex;
		// written on the AI thread when asked, read on the render thread
		bool asked = false, askFailed = false;
		uintptr_t object = 0, rep = 0;
		uintptr_t detour = 0;
		int loadSerial = 0;
		int wanted = 0;                   // the ask the tester wants (each new one, or a clear, moves it on)
		std::string message;
	};
	Tester g_tester;
	NAV_DEBUG::PathTest g_test;

	// Asks the path queue for a string pulled path (AI thread); false when it could not
	bool AskPath(uintptr_t queue, const float start[3], const float end[3], int characterClass, bool backstage, bool deepCrouch, uintptr_t& object, uintptr_t& rep)
	{
		__try
		{
			alignas(16) uint8_t query[0xC0] = {};
			alignas(16) float from[4] = { start[0], start[1], start[2], 0.0f };
			alignas(16) float to[4] = { end[0], end[1], end[2], 0.0f };
			NAV_DEBUG::path_query(query, from, to, characterClass);
			if (backstage)
				*reinterpret_cast<uint32_t*>(query + kQueryFilterMarkup) &= ~NAV_DEBUG::kMarkupBackstage;
			if (deepCrouch)
				*reinterpret_cast<int*>(query + kQueryFilterHeightMax) = 2;
			uint32_t result[2] = {};
			NAV_DEBUG::ask_path(reinterpret_cast<void*>(queue), result, query);
			object = result[0];
			rep = result[1];
			return object != 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// Lets go of a result the queue handed back (AI thread): its count block's count down, the result destroyed and the
	// block freed when they reach nothing, as the game itself lets go of a shared pointer
	void ReleaseResult(uintptr_t rep)
	{
		typedef void(__thiscall* t_virtual)(void* self);
		__try
		{
			if (!rep)
				return;
			if (InterlockedDecrement(reinterpret_cast<volatile LONG*>(rep + 4)) != 0)
				return;
			void** vtable = *reinterpret_cast<void***>(rep);
			reinterpret_cast<t_virtual>(vtable[0])(reinterpret_cast<void*>(rep));
			if (InterlockedDecrement(reinterpret_cast<volatile LONG*>(rep + 8)) == 0)
				reinterpret_cast<t_virtual>(vtable[1])(reinterpret_cast<void*>(rep));
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
		}
	}

	uintptr_t CurrentDetour()
	{
		const uintptr_t object = GlobalsField(kGlobalsNavMesh);
		__try
		{
			return object ? At<uintptr_t>(object, kNavDetour) : 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	// Queues the release of what the tester holds (only while the same mesh is loaded: a result from before the level
	// or state changed is left alone - its memory may have gone with the level)
	void QueueRelease()
	{
		uintptr_t rep = 0, detour = 0;
		int serial = 0;
		{
			std::lock_guard<std::mutex> lock(g_tester.mutex);
			rep = g_tester.rep;
			detour = g_tester.detour;
			serial = g_tester.loadSerial;
			g_tester.object = g_tester.rep = 0;
			g_tester.asked = false;
		}
		if (!rep)
			return;
		DEBUG_MENU::OnAiThread([rep, detour, serial]()
		{
			if (g_loadSerial == serial && CurrentDetour() == detour)
				ReleaseResult(rep);
		});
	}

	struct PathRead
	{
		bool done = false;
		int status = 0;
		float length = 0.0f;
		int count = 0;
		float points[1024][3];
	};

	bool ReadPathResult(uintptr_t object, PathRead& out)
	{
		__try
		{
			out.done = At<uint32_t>(object, kPathDone) == 1;
			if (!out.done)
				return true;
			out.status = At<int>(object, kPathStatus);
			out.length = At<float>(object, kPathLength);
			const uintptr_t first = At<uintptr_t>(object, kPathPoints);
			const uintptr_t last = At<uintptr_t>(object, kPathPoints + 4);
			out.count = 0;
			if (first && last > first && (last - first) % 12 == 0)
			{
				out.count = (std::min)(static_cast<int>((last - first) / 12), 1024);
				memcpy(out.points, reinterpret_cast<const void*>(first), out.count * 12);
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}
}

// ---- Hooks ----

__declspec(noinline)
void __fastcall NAV_DEBUG::h_load_mesh_state(void* navMesh, void* /*_EDX*/, uint32_t state)
{
	if (g_attached)
	{
		std::lock_guard<std::mutex> lock(g_hookMutex);
		g_lastClasses.clear();
		g_loadBarriers = 0;
	}
	g_loadingThread = GetCurrentThreadId();
	load_mesh_state(navMesh, state);
	g_loadingThread = 0;
	g_loadSerial++;
	if (!g_attached)
		return;
	LoadRecord record;
	record.time = ReadNow();
	record.state = state;
	ReadLoaded(reinterpret_cast<uintptr_t>(navMesh), record.loaded, record.polys);
	{
		std::lock_guard<std::mutex> lock(g_hookMutex);
		record.barriers = g_loadBarriers;
	}
	g_state = static_cast<int>(state);
	DevTools::Log("Debug menu: nav mesh state %u loaded - %s (%d polygons)", state, record.loaded ? "a mesh is loaded" : "NO MESH (the level has no navigation)", record.polys);
	std::lock_guard<std::mutex> lock(g_hookMutex);
	g_loads.push_back(record);
	while (g_loads.size() > kMaxLoads)
		g_loads.pop_front();
}

__declspec(noinline)
void __fastcall NAV_DEBUG::h_set_area_classes(void* navMesh, void* /*_EDX*/, uint16_t area, const uint32_t* classes)
{
	uint32_t value = 0;
	const bool have = g_attached && ReadU32(classes, value);
	set_area_classes(navMesh, area, classes);
	if (!have)
		return;
	const float now = ReadNow();
	const bool atLoad = g_loadingThread == GetCurrentThreadId();
	std::lock_guard<std::mutex> lock(g_hookMutex);
	const auto found = g_lastClasses.find(area);
	const int before = found == g_lastClasses.end() ? -1 : static_cast<int>(found->second);
	g_lastClasses[area] = value;
	// The state's loading puts every barrier back: noted (as what each had), not logged one by one
	if (atLoad)
	{
		g_loadBarriers++;
		return;
	}
	// A door writing the same classes again soon after: one entry
	if (!g_barrierLog.empty())
	{
		BarrierRecord& last = g_barrierLog.back();
		if (last.area == area && last.after == value && now - last.time < 1.0f)
		{
			last.sets++;
			last.time = now;
			return;
		}
	}
	BarrierRecord record;
	record.time = now;
	record.area = area;
	record.before = before;
	record.after = value;
	g_barrierLog.push_back(record);
	while (g_barrierLog.size() > kMaxBarrierLog)
		g_barrierLog.pop_front();
}

__declspec(noinline)
void __fastcall NAV_DEBUG::h_populate_movement(void* result, void* /*_EDX*/, const void* searchResult, const void* query)
{
	NotePath(kMovement, searchResult, query);
	populate_movement(result, searchResult, query);
}

__declspec(noinline)
void __fastcall NAV_DEBUG::h_populate_cover(void* result, void* /*_EDX*/, const void* searchResult, const void* query)
{
	NotePath(kCover, searchResult, query);
	populate_cover(result, searchResult, query);
}

__declspec(noinline)
void __fastcall NAV_DEBUG::h_populate_polys(void* result, void* /*_EDX*/, const void* searchResult, const void* query)
{
	NotePath(kPolys, searchResult, query);
	populate_polys(result, searchResult, query);
}

__declspec(noinline)
void __fastcall NAV_DEBUG::h_populate_path(void* result, void* /*_EDX*/, const void* searchResult, const void* query)
{
	NotePath(kPath, searchResult, query);
	populate_path(result, searchResult, query);
}

__declspec(noinline)
void __fastcall NAV_DEBUG::h_populate_length(void* result, void* /*_EDX*/, const void* searchResult, const void* query)
{
	NotePath(kLength, searchResult, query);
	populate_length(result, searchResult, query);
}

__declspec(noinline)
void __fastcall NAV_DEBUG::h_populate_route(void* result, void* /*_EDX*/, const void* searchResult, const void* query)
{
	NotePath(kRoute, searchResult, query);
	populate_route(result, searchResult, query);
}

void NAV_DEBUG::AttachHooks(bool attach)
{
	struct Hook { void** original; void* detoured; };
	const Hook hooks[] = {
		{ reinterpret_cast<void**>(&load_mesh_state), reinterpret_cast<void*>(&h_load_mesh_state) },
		{ reinterpret_cast<void**>(&set_area_classes), reinterpret_cast<void*>(&h_set_area_classes) },
		{ reinterpret_cast<void**>(&populate_movement), reinterpret_cast<void*>(&h_populate_movement) },
		{ reinterpret_cast<void**>(&populate_cover), reinterpret_cast<void*>(&h_populate_cover) },
		{ reinterpret_cast<void**>(&populate_polys), reinterpret_cast<void*>(&h_populate_polys) },
		{ reinterpret_cast<void**>(&populate_path), reinterpret_cast<void*>(&h_populate_path) },
		{ reinterpret_cast<void**>(&populate_length), reinterpret_cast<void*>(&h_populate_length) },
		{ reinterpret_cast<void**>(&populate_route), reinterpret_cast<void*>(&h_populate_route) },
	};
	static_assert(sizeof(hooks) / sizeof(hooks[0]) == sizeof(g_hooked) / sizeof(g_hooked[0]), "a flag per hook");
	if (attach)
	{
		int attached = 0;
		// Hooked only where this build has the function (it always does, for the three known builds)
		for (size_t i = 0; i < sizeof(hooks) / sizeof(hooks[0]); i++)
			if ((g_hooked[i] = *hooks[i].original != nullptr))
			{
				DetourAttach(hooks[i].original, hooks[i].detoured);
				attached++;
			}
		DevTools::Log("Debug menu: %d of %d navigation hooks attached", attached, static_cast<int>(sizeof(hooks) / sizeof(hooks[0])));
		g_attached = true;
	}
	else
	{
		g_attached = false;
		for (size_t i = 0; i < sizeof(hooks) / sizeof(hooks[0]); i++)
			if (g_hooked[i])
				DetourDetach(hooks[i].original, hooks[i].detoured);
	}
}

bool NAV_DEBUG::HooksAttached()
{
	return g_attached;
}

namespace
{
	const bool g_hooksAdded = DEBUG_MENU::AddHooks(NAV_DEBUG::AttachHooks);
}

// ---- Names ----

std::string NAV_DEBUG::ClassesText(uint32_t classes)
{
	classes &= kClassAll;
	if (classes == kClassAll)
		return "all";
	if (!classes)
		return "none";
	static const char* const names[] = { "player", "alien", "android", "human", "facehugger" };
	std::string text;
	for (int i = 0; i < 5; i++)
		if (classes & (1u << i))
			text += (text.empty() ? "" : "+") + std::string(names[i]);
	return text;
}

const char* NAV_DEBUG::LinkTypeName(int type)
{
	switch (type)
	{
	case 0: return "manual";
	case 1: return "teleport";
	case 2: return "traversal";
	case 3: return "wait";
	case 4: return "backstage";
	default: return "?";
	}
}

const char* NAV_DEBUG::HeightName(int height)
{
	switch (height)
	{
	case 0: return "standing";
	case 1: return "crouch";
	case 2: return "deep crouch";
	default: return "?";
	}
}

std::string NAV_DEBUG::MarkupText(uint32_t markup)
{
	if (!markup)
		return "-";
	std::string text;
	if (markup & kMarkupNormal)
		text += "normal";
	if (markup & kMarkupBackstage)
		text += std::string(text.empty() ? "" : "+") + "backstage";
	if (markup & kMarkupExpensive)
		text += std::string(text.empty() ? "" : "+") + "expensive";
	return text;
}

const char* NAV_DEBUG::SpeedName(int speed)
{
	switch (speed)
	{
	case 0: return "slowest";
	case 1: return "slow";
	case 2: return "fast";
	case 3: return "fastest";
	default: return "?";
	}
}

const char* NAV_DEBUG::ReasonName(int reason)
{
	switch (reason)
	{
	case 0: return "as asked";
	case 1: return "clamped onto its polygon";
	case 2: return "moved to a sensible place nearby";
	case 3: return "moved to the closest polygon";
	default: return "?";
	}
}

const char* NAV_DEBUG::PathKindName(int kind)
{
	switch (kind)
	{
	case kMovement: return "movement";
	case kCover: return "cover";
	case kPolys: return "polygons";
	case kPath: return "path";
	case kLength: return "path length";
	case kRoute: return "route check";
	default: return "?";
	}
}

std::string NAV_DEBUG::StatusText(uint32_t status)
{
	std::string text = (status & 0x80000000u) ? "failed" : (status & 0x40000000u) ? "succeeded" : (status & 0x20000000u) ? "in progress" : "?";
	struct Bit { uint32_t bit; const char* name; };
	static const Bit bits[] = { { 0x40, "partial" }, { 0x20, "out of nodes" }, { 0x10, "buffer too small" }, { 0x08, "invalid parameter" },
		{ 0x04, "out of memory" }, { 0x02, "wrong version" }, { 0x01, "wrong magic" } };
	for (const Bit& bit : bits)
		if (status & bit.bit)
			text += std::string(", ") + bit.name;
	return text;
}

float NAV_DEBUG::Now()
{
	return ReadNow();
}

// ---- The mesh ----

const NAV_DEBUG::Mesh& NAV_DEBUG::Read()
{
	const int frame = ImGui::GetFrameCount();
	if (g_meshFrame != frame)
	{
		g_meshFrame = frame;
		ReadFrame();
	}
	return g_mesh;
}

uint32_t NAV_DEBUG::PolyRef(int poly)
{
	const Mesh& mesh = g_mesh;
	if (!mesh.have || poly < 0 || poly >= static_cast<int>(mesh.polys.size()))
		return 0;
	return (mesh.tileSalt << (mesh.polyBits + mesh.tileBits)) | (static_cast<uint32_t>(mesh.tileIndex) << mesh.polyBits) | static_cast<uint32_t>(poly);
}

NAV_DEBUG::Decoded NAV_DEBUG::Decode(uint32_t ref)
{
	Decoded out;
	const Mesh& mesh = g_mesh;
	if (!mesh.have)
	{
		out.why = "no nav mesh";
		return out;
	}
	const uint32_t polyMask = mesh.polyBits >= 32 ? 0xFFFFFFFFu : (1u << mesh.polyBits) - 1;
	const uint32_t tileMask = mesh.tileBits >= 32 ? 0xFFFFFFFFu : (1u << mesh.tileBits) - 1;
	const uint32_t saltMask = mesh.saltBits >= 32 ? 0xFFFFFFFFu : (1u << mesh.saltBits) - 1;
	out.poly = ref & polyMask;
	out.tile = (ref >> mesh.polyBits) & tileMask;
	out.salt = (ref >> (mesh.polyBits + mesh.tileBits)) & saltMask;
	if (!ref)
		out.why = "0 is no polygon";
	else if (static_cast<int>(out.tile) != mesh.tileIndex)
		out.why = "not this mesh's tile";
	else if (out.salt != mesh.tileSalt)
		out.why = "stale (the tile's salt has changed: from an earlier mesh)";
	else if (out.poly >= mesh.polys.size())
		out.why = "past the last polygon";
	else
		out.valid = true;
	return out;
}

int NAV_DEBUG::PolyIndex(uint32_t ref)
{
	// As Decode, without the words (called for every link by the flood)
	const Mesh& mesh = g_mesh;
	if (!mesh.have || !ref || mesh.polyBits >= 32 || mesh.tileBits >= 32 || mesh.saltBits >= 32)
		return -1;
	const uint32_t poly = ref & ((1u << mesh.polyBits) - 1);
	const uint32_t tile = (ref >> mesh.polyBits) & ((1u << mesh.tileBits) - 1);
	const uint32_t salt = (ref >> (mesh.polyBits + mesh.tileBits)) & ((1u << mesh.saltBits) - 1);
	if (static_cast<int>(tile) != mesh.tileIndex || salt != mesh.tileSalt || poly >= mesh.polys.size())
		return -1;
	return static_cast<int>(poly);
}

int NAV_DEBUG::PolyAt(const float position[3], float* height)
{
	const Mesh& mesh = g_mesh;
	if (!mesh.have)
		return -1;
	int best = -1;
	float bestScore = 1e9f, bestHeight = 0.0f;
	const float x = position[0], y = position[1], z = position[2];
	for (size_t i = 0; i < mesh.polys.size(); i++)
	{
		const Poly& poly = mesh.polys[i];
		if (IsOffMesh(poly.area) || poly.vertCount < 3)
			continue;
		if (x < poly.min[0] - 0.01f || x > poly.max[0] + 0.01f || z < poly.min[2] - 0.01f || z > poly.max[2] + 0.01f)
			continue;
		if (y < poly.min[1] - 2.0f || y > poly.max[1] + 2.0f)
			continue;
		if (!InsideXZ(mesh, poly, x, z))
			continue;
		const float h = HeightAt(mesh, poly, x, z);
		const float dy = y - h;
		// Below the point is better than above it (feet stand on the mesh, a little above or below it)
		const float score = dy >= -0.5f ? std::fabs(dy) : 1.0f + std::fabs(dy) * 2.0f;
		if (std::fabs(dy) > 2.0f || score >= bestScore)
			continue;
		best = static_cast<int>(i);
		bestScore = score;
		bestHeight = h;
	}
	if (height && best >= 0)
		*height = bestHeight;
	return best;
}

bool NAV_DEBUG::Freshness(int poly, uint16_t& ticks, uint8_t& character)
{
	if (!g_mesh.have || poly < 0 || poly >= static_cast<int>(g_mesh.polys.size()))
		return false;
	return ReadFreshness(g_runtime, poly, ticks, character);
}

const std::vector<NAV_DEBUG::Barrier>& NAV_DEBUG::Barriers()
{
	return g_barriers;
}

const NAV_DEBUG::Barrier* NAV_DEBUG::BarrierByArea(int area)
{
	for (const Barrier& barrier : g_barriers)
		if (barrier.area == area)
			return &barrier;
	return nullptr;
}

// ---- Logs ----

void NAV_DEBUG::Hooked(std::vector<LoadRecord>& loads, std::vector<BarrierRecord>& barriers, std::vector<PathRecord>& paths, PathCounts& counts)
{
	std::lock_guard<std::mutex> lock(g_hookMutex);
	loads.assign(g_loads.rbegin(), g_loads.rend());
	barriers.assign(g_barrierLog.rbegin(), g_barrierLog.rend());
	paths.assign(g_paths.rbegin(), g_paths.rend());
	counts = g_counts;
}

void NAV_DEBUG::ClearLogs()
{
	std::lock_guard<std::mutex> lock(g_hookMutex);
	g_barrierLog.clear();
	g_paths.clear();
	g_counts = PathCounts();
}

int NAV_DEBUG::CurrentState()
{
	return g_state;
}

// ---- Who can get where ----

bool NAV_DEBUG::PassFilter(const Filter& filter, int poly)
{
	const Mesh& mesh = g_mesh;
	if (poly < 0 || poly >= static_cast<int>(mesh.polys.size()))
		return false;
	const uint32_t area = mesh.polys[poly].area;
	if (!Enabled(area))
		return false;
	if (!(Admittance(area) & (filter.include & 0xFF)))
		return false;
	if (IsOffMesh(area))
	{
		if (!((1u << LinkType(area)) & filter.linkTypes))
			return false;
		const int index = poly - mesh.offMeshBase;
		if (index >= 0 && index < static_cast<int>(mesh.offMesh.size()))
		{
			const OffMesh& connection = mesh.offMesh[index];
			if (filter.speedMin > connection.maxSpeed || filter.speedMax < connection.minSpeed)
				return false;
		}
	}
	if (Markup(area) & filter.excludeMarkup & 7)
		return false;
	const int height = HeightClass(area);
	return height <= filter.heightMax && height >= filter.heightMin;
}

NAV_DEBUG::Filter NAV_DEBUG::ClassFilter(uint32_t navigationClass, bool backstage, bool deepCrouch)
{
	Filter filter;
	filter.include = navigationClass & kClassAll;
	if (backstage)
		filter.excludeMarkup &= ~kMarkupBackstage;
	if (deepCrouch)
		filter.heightMax = 2;
	return filter;
}

void NAV_DEBUG::RunFlood(Flood& flood, const Filter& filter, int seed)
{
	const Mesh& mesh = g_mesh;
	flood = Flood();
	flood.filter = filter;
	flood.seed = seed;
	flood.generation = mesh.generation;
	flood.changeCounter = mesh.changeCounter;
	flood.done = mesh.have;
	if (!mesh.have)
		return;
	const int count = static_cast<int>(mesh.polys.size());
	flood.reached.assign(count, 0);
	for (int i = 0; i < count; i++)
		if (PassFilter(filter, i))
		{
			flood.reached[i] = 1;
			flood.passableCount++;
		}

	auto linked = [&](int poly, auto&& visit)
	{
		uint32_t link = mesh.polys[poly].firstLink;
		for (int guard = 0; link != 0xFFFFFFFFu && link < mesh.links.size() && guard < 64; guard++)
		{
			const int other = PolyIndex(mesh.links[link].ref);
			if (other >= 0)
				visit(other);
			link = mesh.links[link].next;
		}
	};

	// Reached from the seed: over links as they run (a one-way off-mesh connection only one way)
	if (seed >= 0 && seed < count && flood.reached[seed])
	{
		std::vector<int> queue;
		queue.push_back(seed);
		flood.reached[seed] = 2;
		for (size_t at = 0; at < queue.size(); at++)
			linked(queue[at], [&](int other)
			{
				if (flood.reached[other] == 1)
				{
					flood.reached[other] = 2;
					queue.push_back(other);
				}
			});
		flood.reachedCount = static_cast<int>(queue.size());
	}

	// Islands: the passable polygons joined by links either way
	std::vector<int> parent(count);
	for (int i = 0; i < count; i++)
		parent[i] = i;
	auto find = [&](int i)
	{
		while (parent[i] != i)
		{
			parent[i] = parent[parent[i]];
			i = parent[i];
		}
		return i;
	};
	for (int i = 0; i < count; i++)
	{
		if (!flood.reached[i])
			continue;
		linked(i, [&](int other)
		{
			if (flood.reached[other])
			{
				const int a = find(i), b = find(other);
				if (a != b)
					parent[a] = b;
			}
		});
	}
	std::unordered_map<int, int> rootIsland;
	std::vector<Flood::Island> islands;
	std::vector<int> island(count, -1);
	for (int i = 0; i < count; i++)
	{
		if (!flood.reached[i])
			continue;
		const int root = find(i);
		auto found = rootIsland.find(root);
		if (found == rootIsland.end())
		{
			found = rootIsland.emplace(root, static_cast<int>(islands.size())).first;
			islands.push_back(Flood::Island());
		}
		island[i] = found->second;
		Flood::Island& item = islands[found->second];
		const Poly& poly = mesh.polys[i];
		item.polys++;
		for (int axis = 0; axis < 3; axis++)
			item.centre[axis] += poly.centre[axis];
		// Its area seen from above (a fan of triangles)
		const int vertTotal = static_cast<int>(mesh.verts.size() / 3);
		for (int v = 1; v + 1 < poly.vertCount; v++)
		{
			if (poly.verts[0] >= vertTotal || poly.verts[v] >= vertTotal || poly.verts[v + 1] >= vertTotal)
				break;
			const float* a = &mesh.verts[poly.verts[0] * 3];
			const float* b = &mesh.verts[poly.verts[v] * 3];
			const float* c = &mesh.verts[poly.verts[v + 1] * 3];
			item.area += std::fabs((b[0] - a[0]) * (c[2] - a[2]) - (c[0] - a[0]) * (b[2] - a[2])) * 0.5f;
		}
		if (i == seed)
			item.hasSeed = true;
	}
	for (Flood::Island& item : islands)
		if (item.polys)
			for (float& value : item.centre)
				value /= static_cast<float>(item.polys);
	// Largest first
	std::vector<int> order(islands.size());
	for (size_t i = 0; i < order.size(); i++)
		order[i] = static_cast<int>(i);
	std::stable_sort(order.begin(), order.end(), [&](int a, int b) { return islands[a].polys > islands[b].polys; });
	std::vector<int> rank(islands.size());
	for (size_t i = 0; i < order.size(); i++)
		rank[order[i]] = static_cast<int>(i);
	flood.islands.resize(islands.size());
	for (size_t i = 0; i < islands.size(); i++)
		flood.islands[rank[i]] = islands[i];
	for (int& value : island)
		if (value >= 0)
			value = rank[value];
	flood.island.swap(island);
}

// ---- The path tester ----

void NAV_DEBUG::StartPathTest(const float start[3], const float end[3], int characterClass, bool backstage, bool deepCrouch)
{
	// The ask this one replaces is moved on first, so one still on its way to the AI thread lets go of what it gets
	// rather than keeping it after the release below has passed
	int serial = 0;
	{
		std::lock_guard<std::mutex> lock(g_tester.mutex);
		serial = ++g_tester.wanted;
	}
	QueueRelease();
	g_test = PathTest();
	memcpy(g_test.start, start, sizeof(g_test.start));
	memcpy(g_test.end, end, sizeof(g_test.end));
	g_test.characterClass = characterClass;
	g_test.backstage = backstage;
	g_test.deepCrouch = deepCrouch;
	g_test.askedAt = ReadNow();
	if (!path_query || !ask_path)
	{
		g_test.state = 4;
		g_test.message = "not available on this game release";
		return;
	}
	g_test.state = 1;
	g_test.message = "waiting for the AI thread (it runs while a level plays)";
	{
		std::lock_guard<std::mutex> lock(g_tester.mutex);
		g_tester.asked = false;
		g_tester.askFailed = false;
		g_tester.message.clear();
	}
	const PathTest asked = g_test;
	DEBUG_MENU::OnAiThread([asked, serial]()
	{
		// A later ask (or a clear) has taken its place: nothing asked
		{
			std::lock_guard<std::mutex> lock(g_tester.mutex);
			if (g_tester.wanted != serial)
				return;
		}
		const uintptr_t queue = GlobalsField(kGlobalsPathQueue);
		const uintptr_t detour = CurrentDetour();
		uintptr_t object = 0, rep = 0;
		bool ok = false;
		const char* why = "";
		if (!queue)
			why = "the game has no path queue (no level?)";
		else if (!detour)
			why = "the level has no nav mesh";
		else
		{
			ok = AskPath(queue, asked.start, asked.end, asked.characterClass, asked.backstage, asked.deepCrouch, object, rep);
			if (!ok)
				why = "the game's path query could not be made";
		}
		std::lock_guard<std::mutex> lock(g_tester.mutex);
		if (g_tester.wanted != serial)
		{
			// Cleared while it was asked: let go of it now (on this thread, the mesh unchanged)
			if (ok)
				ReleaseResult(rep);
			return;
		}
		g_tester.asked = true;
		g_tester.askFailed = !ok;
		g_tester.object = object;
		g_tester.rep = rep;
		g_tester.detour = detour;
		g_tester.loadSerial = g_loadSerial;
		g_tester.message = why;
	});
}

bool NAV_DEBUG::PathTestActive()
{
	return g_test.state != 0;
}

const NAV_DEBUG::PathTest& NAV_DEBUG::UpdatePathTest()
{
	PathTest& test = g_test;
	if (test.state == 1 || test.state == 2)
		Read();
	if (test.state == 1)
	{
		std::lock_guard<std::mutex> lock(g_tester.mutex);
		if (g_tester.asked)
		{
			test.state = g_tester.askFailed ? 4 : 2;
			test.message = g_tester.askFailed ? g_tester.message : "asked; waiting for the path queue";
		}
	}
	if (test.state == 2)
	{
		uintptr_t object = 0, detour = 0;
		int serial = 0;
		{
			std::lock_guard<std::mutex> lock(g_tester.mutex);
			object = g_tester.object;
			detour = g_tester.detour;
			serial = g_tester.loadSerial;
		}
		// The level or state changed under it: let it go without touching it
		if (g_loadSerial != serial || g_mesh.detour != detour || !g_mesh.have)
		{
			std::lock_guard<std::mutex> lock(g_tester.mutex);
			g_tester.object = g_tester.rep = 0;
			test.state = 4;
			test.message = "the nav mesh changed before the answer came";
			return test;
		}
		static PathRead read;
		if (!ReadPathResult(object, read))
		{
			test.state = 4;
			test.message = "the result could not be read";
			QueueRelease();
		}
		else if (read.done)
		{
			test.state = 3;
			test.status = read.status;
			test.length = read.length;
			test.points.assign(&read.points[0][0], &read.points[0][0] + read.count * 3);
			test.message = read.status == 1 ? "found a path" : "no path (failed, or only part of the way)";
			QueueRelease();
		}
		else if (ReadNow() - test.askedAt > 10.0f)
		{
			test.state = 4;
			test.message = "no answer after 10 s of game time";
			QueueRelease();
		}
	}
	return test;
}

void NAV_DEBUG::ClearPathTest()
{
	{
		std::lock_guard<std::mutex> lock(g_tester.mutex);
		g_tester.wanted++;
	}
	QueueRelease();
	g_test = PathTest();
}

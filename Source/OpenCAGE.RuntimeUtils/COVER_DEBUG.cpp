#include "COVER_DEBUG.h"
#include "DEBUG_MENU.h"
#include "LIVE_LINK.h"

#include <algorithm>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <string>
#include <unordered_map>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <imgui.h>

/*
	Addresses and layouts are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).

	See COVER_DEBUG.h for what is read and why. Everything here runs on the render thread.
*/
namespace
{
	// Where the game keeps its pointer to its globals; in them, the cover system (a pointer)
	constexpr uintptr_t kGameGlobals = 0x012F0C88;
	constexpr uint32_t kGlobalsCover = 0x20;
	// Where the game keeps its pointer to the job centre
	constexpr uintptr_t kJobCentre = 0x012EE414;

	// The cover system: the characters it follows (18, 0x28 each), its slots, segments and traversals (each a pointer then
	// how many) and the bit that says the level's data is in
	constexpr uint32_t kCoverTracked = 0x2108;
	constexpr uint32_t kTrackedStride = 0x28;
	constexpr int kTrackedCount = 18;
	constexpr uint32_t kCoverSlots = 0x23D8;
	constexpr uint32_t kCoverSegments = 0x23E4;
	constexpr uint32_t kCoverTraversals = 0x23EC;
	constexpr uint32_t kCoverLoaded = 0x23F4;

	// A segment (0x60): its left and right ends and the way it faces (four floats each), its height and flags, its uid,
	// the uids of the segments round its corners and in line with it, its traversal's, its index in the level's file, the
	// script entity it came from, its length, whether it is enabled, and its slots (how many, the first)
	struct RawSegment
	{
		float left[4], right[4], normal[4];
		float height;
		uint32_t flags;
		uint16_t uid, leftCorner, rightCorner, leftColinear, rightColinear, traversal;
		int cathodeIndex;
		uint32_t entity, parent;
		float length;
		uint32_t enabled;
		uint16_t slotCount, pad;
		uint32_t firstSlot;
	};
	static_assert(sizeof(RawSegment) == 0x60, "a cover segment is 0x60 bytes");

	// A slot (0x24): how far along its segment, its flags and uid, who is in it, its state, its segment, the angles it
	// can aim at (two packed fields) and the alliance bits
	struct RawSlot
	{
		float along;
		uint32_t flags, uid;
		uint32_t occupantEntity, occupantParent;
		int status;
		uint16_t segment, aimHorizontal;
		uint32_t aimVertical;
		uint32_t alliance;
	};
	static_assert(sizeof(RawSlot) == 0x24, "a cover slot is 0x24 bytes");

	// A followed character (0x28): its script entity, the segment it is on and those round its corner and in line, its
	// slot and the two it has reserved, how it stands and faces, and bits: on this segment, each reservation held
	struct RawTracked
	{
		uint32_t entity, parent;
		uint16_t segment, corner, colinear, pad;
		uint32_t slot, reserved1, reserved2;
		int stance, facing;
		uint16_t interaction;
		uint8_t bits, pad2;
	};
	static_assert(sizeof(RawTracked) == 0x28, "a followed character is 0x28 bytes");

	// A traversal (0x10C): its uid and type, its index in the level's file, the script entity it came from, its entrance
	// and exit (two points each), a path of 16 points, the classes that may use it, its animation tree and whether it is
	// enabled
	struct RawTraversal
	{
		uint16_t uid;
		uint8_t type, pad;
		int cathodeIndex;
		uint32_t entity, parent;
		float entrance[2][3], exit[2][3];
		float path[16][3];
		uint32_t classes, animationTree, enabled;
	};
	static_assert(sizeof(RawTraversal) == 0x10C, "a traversal is 0x10C bytes");

	// The job centre: a job list per job type (15 pointers), then its baked job positions (a pointer). A job list keeps
	// its jobs at +0xC (pointers) with how many at +0x10.
	constexpr int kJobTypes = 15;
	constexpr uint32_t kCentreAuto = 0x3C;
	constexpr uint32_t kListJobs = 0x0C;
	constexpr uint32_t kListCount = 0x10;
	// The baked positions: three grids (spotting, crawl-space spotting, assault: pointers), whether each is used whole,
	// and each grid's active cells (a list: the cells' pointers at +8 from its start, how many at +0xC)
	constexpr uint32_t kAutoUseAll = 0x0C;
	constexpr uint32_t kAutoActive[3] = { 0x10, 0x24, 0x38 };
	// A grid: cells across and down, where it starts (x, z), the size of a cell, and its cells (8 bytes each: how many
	// positions, then a pointer to them)
	struct RawGrid
	{
		uint32_t xCells, zCells;
		float minX, minZ, unit;
		uint32_t cells;
	};
	// A job: its script entity, how its tasks are run, its type, its tasks (a list: its head node; each node next, back,
	// then the task's shared pointer) and whether all its tasks must be done
	constexpr uint32_t kJobEntity = 0x04;
	constexpr uint32_t kJobOpMode = 0x0C;
	constexpr uint32_t kJobType = 0x10;
	constexpr uint32_t kJobTasks = 0x14;
	constexpr uint32_t kJobAllTasks = 0x20;
	// A task: the character it is for, its owning and reserving roles, its state, position, orientation and bits (has an
	// orientation, starts in a vent); a role's type at +0xC and its character at +0x10
	constexpr uint32_t kTaskCharacter = 0x10;
	constexpr uint32_t kTaskOwningRole = 0x1C;
	constexpr uint32_t kTaskReservedBy = 0x20;
	constexpr uint32_t kTaskState = 0x24;
	constexpr uint32_t kTaskPosition = 0x28;
	constexpr uint32_t kTaskOrientation = 0x34;
	constexpr uint32_t kTaskBits = 0x44;
	constexpr uint32_t kRoleType = 0x0C;
	constexpr uint32_t kRoleCharacter = 0x10;
	constexpr int kMaxJobsPerList = 2048;
	constexpr int kMaxTasks = 32;
	constexpr int kMaxAutoJobs = 30000;

	// A character's script entity (two ids), and its traversal tracker: whether it can start one now, the closest (an
	// index, -1 none), the 24 it follows (references, script entities, directions - four floats each - and distances)
	// and the one it is on
	constexpr uint32_t kCharacterHandle = 0x54;
	constexpr uint32_t kCharacterTracker = 0x29E0;
	constexpr uint32_t kTrackerCanTransition = 0x04;
	constexpr uint32_t kTrackerClosest = 0x08;
	constexpr uint32_t kTrackerRefs = 0x0C;
	constexpr uint32_t kTrackerEntities = 0x6C;
	constexpr uint32_t kTrackerDirections = 0xD0;
	constexpr uint32_t kTrackerDistances = 0x250;
	constexpr uint32_t kTrackerOn = 0x2B0;
	constexpr int kTrackerCount = 24;

	COVER_DEBUG::Cover g_cover;
	int g_coverFrame = -1;
	int g_coverSlotsFrame = -1000;
	uintptr_t g_segmentsAt = 0, g_traversalsAt = 0;
	uint32_t g_segmentCount = 0, g_traversalCount = 0;

	COVER_DEBUG::Jobs g_jobs;
	int g_jobsFrame = -1;
	int g_jobsReadFrame = -1000;

	template <typename T> T At(uintptr_t base, uint32_t offset)
	{
		return *reinterpret_cast<const T*>(base + offset);
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

	uintptr_t CoverManager()
	{
		__try
		{
			const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kGameGlobals);
			if (!at)
				return 0;
			const uintptr_t globals = *reinterpret_cast<const uintptr_t*>(at);
			return globals ? At<uintptr_t>(globals, kGlobalsCover) : 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
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

	struct CoverHead
	{
		uintptr_t slots = 0, segments = 0, traversals = 0;
		uint32_t slotCount = 0, segmentCount = 0, traversalCount = 0;
		bool loaded = false;
	};

	bool ReadCoverHead(uintptr_t manager, CoverHead& out)
	{
		__try
		{
			out.slots = At<uintptr_t>(manager, kCoverSlots);
			out.slotCount = At<uint32_t>(manager, kCoverSlots + 4);
			out.segments = At<uintptr_t>(manager, kCoverSegments);
			out.segmentCount = At<uint32_t>(manager, kCoverSegments + 4);
			out.traversals = At<uintptr_t>(manager, kCoverTraversals);
			out.traversalCount = At<uint32_t>(manager, kCoverTraversals + 4);
			out.loaded = (At<uint8_t>(manager, kCoverLoaded) & 1) != 0;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	void CopySegments(uintptr_t at, uint32_t count, std::vector<COVER_DEBUG::Segment>& out)
	{
		std::vector<RawSegment> raw(count);
		if (!CopyBytes(raw.data(), at, raw.size() * sizeof(RawSegment)))
		{
			out.clear();
			return;
		}
		out.resize(count);
		for (uint32_t i = 0; i < count; i++)
		{
			const RawSegment& r = raw[i];
			COVER_DEBUG::Segment& s = out[i];
			memcpy(s.left, r.left, sizeof(s.left));
			memcpy(s.right, r.right, sizeof(s.right));
			memcpy(s.normal, r.normal, sizeof(s.normal));
			s.uid = r.uid;
			s.height = r.height;
			s.flags = r.flags;
			s.leftCorner = r.leftCorner;
			s.rightCorner = r.rightCorner;
			s.leftColinear = r.leftColinear;
			s.rightColinear = r.rightColinear;
			s.traversal = r.traversal;
			s.cathodeIndex = r.cathodeIndex;
			s.entity = r.entity;
			s.parent = r.parent;
			s.length = r.length;
			s.enabled = (r.enabled & 1) != 0;
			s.slotCount = r.slotCount;
			s.firstSlot = static_cast<int>(r.firstSlot);
		}
	}

	void CopyTraversals(uintptr_t at, uint32_t count, std::vector<COVER_DEBUG::Traversal>& out)
	{
		std::vector<RawTraversal> raw(count);
		if (!CopyBytes(raw.data(), at, raw.size() * sizeof(RawTraversal)))
		{
			out.clear();
			return;
		}
		out.resize(count);
		for (uint32_t i = 0; i < count; i++)
		{
			const RawTraversal& r = raw[i];
			COVER_DEBUG::Traversal& t = out[i];
			t.uid = r.uid;
			t.type = r.type;
			t.cathodeIndex = r.cathodeIndex;
			t.entity = r.entity;
			t.parent = r.parent;
			memcpy(t.entrance, r.entrance, sizeof(t.entrance));
			memcpy(t.exit, r.exit, sizeof(t.exit));
			memcpy(t.path, r.path, sizeof(t.path));
			t.classes = r.classes;
			t.animationTree = r.animationTree;
			t.enabled = (r.enabled & 1) != 0;
		}
	}

	void CopySlots(uintptr_t at, uint32_t count, std::vector<COVER_DEBUG::Slot>& out)
	{
		std::vector<RawSlot> raw(count);
		if (!CopyBytes(raw.data(), at, raw.size() * sizeof(RawSlot)))
		{
			out.clear();
			return;
		}
		out.resize(count);
		for (uint32_t i = 0; i < count; i++)
		{
			const RawSlot& r = raw[i];
			COVER_DEBUG::Slot& s = out[i];
			s.uid = static_cast<int>(r.uid);
			s.along = r.along;
			s.flags = r.flags;
			s.occupantEntity = r.occupantEntity;
			s.occupantParent = r.occupantParent;
			s.status = r.status;
			s.segment = r.segment;
			s.aimHorizontal = r.aimHorizontal;
			s.aimVertical = r.aimVertical;
			s.alliance = static_cast<int>(r.alliance & 3);
		}
	}

	void CopyTracked(uintptr_t manager, COVER_DEBUG::Cover& cover)
	{
		RawTracked raw[kTrackedCount];
		cover.trackedCount = 0;
		if (!CopyBytes(raw, manager + kCoverTracked, sizeof(raw)))
			return;
		for (int i = 0; i < kTrackedCount; i++)
		{
			const RawTracked& r = raw[i];
			if (!r.entity && !r.parent)
				continue;
			COVER_DEBUG::Tracked& t = cover.tracked[cover.trackedCount++];
			t.entity = r.entity;
			t.parent = r.parent;
			t.segment = r.segment;
			t.corner = r.corner;
			t.colinear = r.colinear;
			t.slot = r.slot;
			t.reserved[0] = r.reserved1;
			t.reserved[1] = r.reserved2;
			t.stance = r.stance;
			t.facing = r.facing;
			t.onPiece = (r.bits & 1) != 0;
			t.haveReserved[0] = (r.bits & 2) != 0;
			t.haveReserved[1] = (r.bits & 4) != 0;
			// Taking cover sets the slot, its segment and the on-it bit together; leaving it clears all three
			t.inCover = t.onPiece || (r.segment != 0 && r.slot != COVER_DEBUG::kNoSlot);
		}
	}

	void ReadCoverFrame(bool fast)
	{
		COVER_DEBUG::Cover& cover = g_cover;
		cover.level = AnyLevel();
		const uintptr_t manager = cover.level ? CoverManager() : 0;
		CoverHead head;
		cover.have = manager != 0 && ReadCoverHead(manager, head);
		if (!cover.have || !head.loaded)
		{
			cover.loaded = false;
			if (!cover.segments.empty() || !cover.traversals.empty())
				cover.generation++;
			cover.segments.clear();
			cover.slots.clear();
			cover.traversals.clear();
			cover.trackedCount = 0;
			g_segmentsAt = g_traversalsAt = 0;
			return;
		}
		cover.loaded = true;
		const uint32_t kMaxItems = 200000;
		if (head.segments != g_segmentsAt || head.segmentCount != g_segmentCount || head.traversals != g_traversalsAt || head.traversalCount != g_traversalCount)
		{
			g_segmentsAt = head.segments;
			g_segmentCount = head.segmentCount;
			g_traversalsAt = head.traversals;
			g_traversalCount = head.traversalCount;
			CopySegments(head.segments, (std::min)(head.segmentCount, kMaxItems), cover.segments);
			CopyTraversals(head.traversals, (std::min)(head.traversalCount, kMaxItems), cover.traversals);
			cover.generation++;
			g_coverSlotsFrame = -1000;
		}
		// What the AI changes: slots taken, segments switched on and off, who is followed
		const int frame = ImGui::GetFrameCount();
		if (fast || frame - g_coverSlotsFrame >= 30 || frame < g_coverSlotsFrame)
		{
			g_coverSlotsFrame = frame;
			CopySlots(head.slots, (std::min)(head.slotCount, kMaxItems), cover.slots);
			CopyTracked(manager, cover);
			// The segments' enabled bits
			std::vector<RawSegment> raw(cover.segments.size());
			if (!raw.empty() && CopyBytes(raw.data(), head.segments, raw.size() * sizeof(RawSegment)))
				for (size_t i = 0; i < raw.size(); i++)
					cover.segments[i].enabled = (raw[i].enabled & 1) != 0;
		}
	}

	// ---- Jobs ----

	uintptr_t JobCentre()
	{
		__try
		{
			const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kJobCentre);
			return at ? *reinterpret_cast<const uintptr_t*>(at) : 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	struct RawTaskRead
	{
		uintptr_t task;
		int state;
		float position[3];
		float orientation;
		uint8_t bits;
		uintptr_t owner, owningRole, reservedBy, owningCharacter, reservedCharacter;
		int owningRoleType, reservedRoleType;
	};

	bool ReadTask(uintptr_t task, RawTaskRead& out)
	{
		__try
		{
			out.task = task;
			out.owner = At<uintptr_t>(task, kTaskCharacter);
			out.owningRole = At<uintptr_t>(task, kTaskOwningRole);
			out.reservedBy = At<uintptr_t>(task, kTaskReservedBy);
			out.state = At<int>(task, kTaskState);
			memcpy(out.position, reinterpret_cast<const void*>(task + kTaskPosition), sizeof(out.position));
			out.orientation = At<float>(task, kTaskOrientation);
			out.bits = At<uint8_t>(task, kTaskBits);
			out.owningCharacter = out.owningRole ? At<uintptr_t>(out.owningRole, kRoleCharacter) : 0;
			out.owningRoleType = out.owningRole ? At<int>(out.owningRole, kRoleType) : -1;
			out.reservedCharacter = out.reservedBy ? At<uintptr_t>(out.reservedBy, kRoleCharacter) : 0;
			out.reservedRoleType = out.reservedBy ? At<int>(out.reservedBy, kRoleType) : -1;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// A job's own fields and its tasks' pointers (up to kMaxTasks); false when it cannot be read
	struct RawJobRead
	{
		int type, opMode;
		uint32_t entity, parent;
		bool allTasks;
		int taskCount;
		uintptr_t tasks[kMaxTasks];
		int tasksRead;
	};

	bool ReadJob(uintptr_t job, RawJobRead& out)
	{
		__try
		{
			out.entity = At<uint32_t>(job, kJobEntity);
			out.parent = At<uint32_t>(job, kJobEntity + 4);
			out.opMode = At<int>(job, kJobOpMode);
			out.type = At<int>(job, kJobType);
			out.allTasks = At<uint8_t>(job, kJobAllTasks) != 0;
			out.taskCount = At<int>(job, kJobTasks + 4);
			out.tasksRead = 0;
			const uintptr_t head = At<uintptr_t>(job, kJobTasks);
			if (!head)
				return true;
			uintptr_t node = At<uintptr_t>(head, 0);
			for (int guard = 0; node && node != head && guard < 1024; guard++)
			{
				if (out.tasksRead < kMaxTasks)
					out.tasks[out.tasksRead++] = At<uintptr_t>(node, 8);
				node = At<uintptr_t>(node, 0);
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// A job list's jobs (up to max), -1 when it cannot be read
	int ReadJobList(uintptr_t list, uintptr_t* out, int max)
	{
		__try
		{
			if (!list)
				return 0;
			const uintptr_t jobs = At<uintptr_t>(list, kListJobs);
			const int count = (std::min)(At<int>(list, kListCount), max);
			if (!jobs || count <= 0)
				return 0;
			memcpy(out, reinterpret_cast<const void*>(jobs), count * sizeof(uintptr_t));
			return count;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return -1;
		}
	}

	struct AutoHead
	{
		uintptr_t grids[3];
		bool useAll[3];
		uintptr_t active[3];
		int activeCount[3];
		uintptr_t lists[kJobTypes];
	};

	bool ReadAutoHead(uintptr_t centre, AutoHead& out)
	{
		__try
		{
			memset(&out, 0, sizeof(out));
			for (int i = 0; i < kJobTypes; i++)
				out.lists[i] = At<uintptr_t>(centre, static_cast<uint32_t>(i) * 4);
			const uintptr_t autoJobs = At<uintptr_t>(centre, kCentreAuto);
			if (!autoJobs)
				return true;
			for (int i = 0; i < 3; i++)
			{
				out.grids[i] = At<uintptr_t>(autoJobs, static_cast<uint32_t>(i) * 4);
				out.useAll[i] = At<uint8_t>(autoJobs, kAutoUseAll + i) != 0;
				out.active[i] = At<uintptr_t>(autoJobs, kAutoActive[i] + 8);
				out.activeCount[i] = At<int>(autoJobs, kAutoActive[i] + 0xC);
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// One grid's positions (appended), with which cells are active
	void ReadGrid(int kind, uintptr_t gridAt, uintptr_t activeAt, int activeCount, COVER_DEBUG::Jobs& jobs)
	{
		COVER_DEBUG::Grid& grid = jobs.grids[kind];
		grid = COVER_DEBUG::Grid();
		RawGrid raw = {};
		if (!gridAt || !CopyBytes(&raw, gridAt, sizeof(raw)))
			return;
		if (raw.xCells > 4096 || raw.zCells > 4096 || static_cast<uint64_t>(raw.xCells) * raw.zCells > (1u << 20) || !raw.cells)
			return;
		grid.have = true;
		grid.xCells = static_cast<int>(raw.xCells);
		grid.zCells = static_cast<int>(raw.zCells);
		grid.minX = raw.minX;
		grid.minZ = raw.minZ;
		grid.unit = raw.unit;
		const size_t cellCount = static_cast<size_t>(raw.xCells) * raw.zCells;
		std::vector<uint32_t> cells(cellCount * 2);
		if (!CopyBytes(cells.data(), raw.cells, cells.size() * sizeof(uint32_t)))
			return;
		// Which cells are near the player now
		std::vector<uint8_t> active(cellCount, 0);
		if (activeAt && activeCount > 0 && activeCount <= static_cast<int>(cellCount))
		{
			std::vector<uintptr_t> pointers(activeCount);
			if (CopyBytes(pointers.data(), activeAt, pointers.size() * sizeof(uintptr_t)))
				for (uintptr_t pointer : pointers)
					if (pointer >= raw.cells && (pointer - raw.cells) % 8 == 0 && (pointer - raw.cells) / 8 < cellCount)
					{
						active[(pointer - raw.cells) / 8] = 1;
						grid.activeCells++;
					}
		}
		const uint32_t stride = kind == COVER_DEBUG::kAssault ? 0x14 : 0x1C;
		std::vector<uint8_t> items;
		for (size_t c = 0; c < cellCount; c++)
		{
			const uint32_t count = cells[c * 2];
			const uintptr_t at = cells[c * 2 + 1];
			if (!count || !at || count > 4096 || jobs.autoJobs.size() + count > static_cast<size_t>(kMaxAutoJobs))
				continue;
			items.resize(static_cast<size_t>(count) * stride);
			if (!CopyBytes(items.data(), at, items.size()))
				continue;
			for (uint32_t i = 0; i < count; i++)
			{
				const uint8_t* item = items.data() + static_cast<size_t>(i) * stride;
				COVER_DEBUG::AutoJob job;
				job.kind = kind;
				job.cell = static_cast<int>(c);
				job.active = active[c] != 0;
				memcpy(job.position, item, sizeof(job.position));
				if (kind == COVER_DEBUG::kAssault)
				{
					memcpy(&job.yaw, item + 0xC, sizeof(float));
					job.job = *reinterpret_cast<const uint32_t*>(item + 0x10);
				}
				else
				{
					memcpy(job.task, item + 0xC, sizeof(job.task));
					job.job = *reinterpret_cast<const uint32_t*>(item + 0x18);
				}
				jobs.autoJobs.push_back(job);
				grid.items++;
			}
		}
	}

	// The jobs (and, with grids set, the baked positions: their grids are walked whole, so less often)
	void ReadJobsFrame(bool grids)
	{
		COVER_DEBUG::Jobs& jobs = g_jobs;
		jobs.level = AnyLevel();
		const uintptr_t centre = jobs.level ? JobCentre() : 0;
		AutoHead head;
		jobs.have = centre != 0 && ReadAutoHead(centre, head);
		jobs.jobs.clear();
		if (!jobs.have)
		{
			jobs.autoJobs.clear();
			for (COVER_DEBUG::Grid& grid : jobs.grids)
				grid = COVER_DEBUG::Grid();
			return;
		}
		if (grids)
		{
			jobs.autoJobs.clear();
			for (int kind = 0; kind < COVER_DEBUG::kAutoKinds; kind++)
			{
				jobs.useAll[kind] = head.useAll[kind];
				ReadGrid(kind, head.grids[kind], head.active[kind], head.activeCount[kind], jobs);
			}
		}
		static uintptr_t listed[kMaxJobsPerList];
		for (int type = 0; type < kJobTypes; type++)
		{
			const int count = ReadJobList(head.lists[type], listed, kMaxJobsPerList);
			for (int i = 0; i < count; i++)
			{
				RawJobRead raw;
				if (!listed[i] || !ReadJob(listed[i], raw))
					continue;
				COVER_DEBUG::Job job;
				job.job = listed[i];
				job.type = raw.type;
				job.entity = raw.entity;
				job.parent = raw.parent;
				job.opMode = raw.opMode;
				job.allTasks = raw.allTasks;
				job.taskCount = raw.taskCount;
				for (int t = 0; t < raw.tasksRead; t++)
				{
					RawTaskRead task;
					if (!raw.tasks[t] || !ReadTask(raw.tasks[t], task))
						continue;
					COVER_DEBUG::Task out;
					out.task = task.task;
					out.state = task.state;
					memcpy(out.position, task.position, sizeof(out.position));
					out.orientation = task.orientation;
					out.hasOrientation = (task.bits & 1) != 0;
					out.startsInVent = (task.bits & 8) != 0;
					out.owner = reinterpret_cast<const void*>(task.owner);
					out.owningRole = reinterpret_cast<const void*>(task.owningRole);
					out.reservedBy = reinterpret_cast<const void*>(task.reservedBy);
					out.owningCharacter = reinterpret_cast<const void*>(task.owningCharacter);
					out.reservedCharacter = reinterpret_cast<const void*>(task.reservedCharacter);
					out.owningRoleType = task.owningRoleType;
					out.reservedRoleType = task.reservedRoleType;
					job.tasks.push_back(out);
				}
				jobs.jobs.push_back(std::move(job));
			}
		}
	}

	struct RawTracker
	{
		uint8_t canTransition;
		int closest;
		uint32_t refs[kTrackerCount], entities[kTrackerCount];
		float directions[kTrackerCount][4];
		float distances[kTrackerCount];
		uint32_t on;
	};

	bool ReadRawTracker(uintptr_t character, RawTracker& out)
	{
		__try
		{
			const uintptr_t tracker = character + kCharacterTracker;
			out.canTransition = At<uint8_t>(tracker, kTrackerCanTransition);
			out.closest = At<int>(tracker, kTrackerClosest);
			memcpy(out.refs, reinterpret_cast<const void*>(tracker + kTrackerRefs), sizeof(out.refs));
			memcpy(out.entities, reinterpret_cast<const void*>(tracker + kTrackerEntities), sizeof(out.entities));
			memcpy(out.directions, reinterpret_cast<const void*>(tracker + kTrackerDirections), sizeof(out.directions));
			memcpy(out.distances, reinterpret_cast<const void*>(tracker + kTrackerDistances), sizeof(out.distances));
			out.on = At<uint32_t>(tracker, kTrackerOn);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool ReadHandle(uintptr_t character, uint32_t& entity, uint32_t& parent)
	{
		__try
		{
			entity = At<uint32_t>(character, kCharacterHandle);
			parent = At<uint32_t>(character, kCharacterHandle + 4);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}
}

const COVER_DEBUG::Cover& COVER_DEBUG::ReadCover(bool fast)
{
	// Once a frame (again when a later caller wants the slots fresh and they were not read this frame)
	const int frame = ImGui::GetFrameCount();
	if (g_coverFrame != frame || (fast && g_coverSlotsFrame != frame))
	{
		g_coverFrame = frame;
		ReadCoverFrame(fast);
	}
	return g_cover;
}

const COVER_DEBUG::Segment* COVER_DEBUG::SegmentByUid(int uid)
{
	if (uid <= 0 || uid > static_cast<int>(g_cover.segments.size()))
		return nullptr;
	return &g_cover.segments[uid - 1];
}

const COVER_DEBUG::Traversal* COVER_DEBUG::TraversalByUid(int uid)
{
	if (uid <= 0 || uid > static_cast<int>(g_cover.traversals.size()))
		return nullptr;
	return &g_cover.traversals[uid - 1];
}

bool COVER_DEBUG::SlotPosition(const Slot& slot, float out[3])
{
	const Segment* segment = SegmentByUid(slot.segment);
	if (!segment)
		return false;
	for (int axis = 0; axis < 3; axis++)
		out[axis] = segment->left[axis] + (segment->right[axis] - segment->left[axis]) * slot.along;
	return true;
}

const COVER_DEBUG::Jobs& COVER_DEBUG::ReadJobs(bool fast)
{
	const int frame = ImGui::GetFrameCount();
	if (g_jobsFrame != frame)
	{
		g_jobsFrame = frame;
		if (frame - g_jobsReadFrame >= 30 || frame < g_jobsReadFrame)
		{
			g_jobsReadFrame = frame;
			ReadJobsFrame(true);
		}
		else if (fast)
			ReadJobsFrame(false);
	}
	return g_jobs;
}

const COVER_DEBUG::Job* COVER_DEBUG::JobByAddress(uintptr_t job)
{
	if (!job)
		return nullptr;
	for (const Job& item : g_jobs.jobs)
		if (item.job == job)
			return &item;
	return nullptr;
}

bool COVER_DEBUG::ReadTracker(const void* character, Tracker& out)
{
	out = Tracker();
	if (!character)
		return false;
	RawTracker raw;
	if (!ReadRawTracker(reinterpret_cast<uintptr_t>(character), raw))
		return false;
	out.have = true;
	out.canTransition = raw.canTransition != 0;
	out.closest = raw.closest;
	out.onTraversal = raw.on;
	for (int i = 0; i < kTrackerCount; i++)
	{
		if (!raw.entities[i] && !raw.refs[i])
			continue;
		Tracker::Near item;
		item.index = i;
		item.entity = raw.entities[i];
		item.ref = raw.refs[i];
		item.distance = raw.distances[i];
		memcpy(item.direction, raw.directions[i], sizeof(item.direction));
		out.nearby.push_back(item);
	}
	return true;
}

bool COVER_DEBUG::CharacterHandle(const void* character, uint32_t& entity, uint32_t& parent)
{
	entity = parent = 0;
	return character && ReadHandle(reinterpret_cast<uintptr_t>(character), entity, parent);
}

// ---- Names ----

const char* COVER_DEBUG::TraversalTypeName(int type)
{
	switch (type)
	{
	case 0: return "climb under";
	case 1: return "vent";
	case 2: return "ledge";
	case 3: return "sidling";
	case 4: return "ladder";
	case 5: return "pipe";
	case 6: return "balance beam";
	case 7: return "tight gap";
	case 8: return "wall";
	default: return "?";
	}
}

const char* COVER_DEBUG::SlotStatusName(int status)
{
	switch (status)
	{
	case 0: return "free";
	case 1: return "reserved";
	case 2: return "occupied";
	default: return "?";
	}
}

const char* COVER_DEBUG::JobTypeName(int type)
{
	switch (type)
	{
	case 0: return "idle";
	case 1: return "despawn";
	case 2: return "area sweep";
	case 3: return "area sweep (flare)";
	case 4: return "systematic search target";
	case 5: return "systematic search";
	case 6: return "systematic search (flare)";
	case 7: return "spotting position";
	case 8: return "crawl-space spotting position";
	case 9: return "pick up weapon";
	case 10: return "assault";
	case 11: return "player hiding";
	case 12: return "follow";
	case 13: return "follow centre";
	case 14: return "panic";
	default: return "?";
	}
}

const char* COVER_DEBUG::TaskStateName(int state)
{
	switch (state)
	{
	case 0: return "uninitialised";
	case 1: return "not in use";
	case 2: return "assigned";
	case 3: return "about to move";
	case 4: return "moving";
	case 5: return "interrupted";
	case 6: return "interrupted, finished";
	case 7: return "performing";
	case 8: return "destroyed";
	default: return "?";
	}
}

const char* COVER_DEBUG::StanceName(int stance)
{
	switch (stance)
	{
	case 0: return "low";
	case 1: return "standing";
	default: return "?";
	}
}

const char* COVER_DEBUG::FacingName(int facing)
{
	switch (facing)
	{
	case 0: return "left";
	case 1: return "right";
	case 2: return "unset";
	default: return "?";
	}
}

const char* COVER_DEBUG::AutoKindName(int kind)
{
	switch (kind)
	{
	case kSpotting: return "spotting";
	case kCrawl: return "crawl-space spotting";
	case kAssault: return "assault";
	default: return "?";
	}
}

namespace
{
	std::string BitsText(uint32_t flags, const char* const* names, int count)
	{
		std::string text;
		for (int i = 0; i < count; i++)
			if (flags & (1u << i))
				text += (text.empty() ? "" : ", ") + std::string(names[i]);
		return text.empty() ? "-" : text;
	}

	const char* const kCoverBitNames[] = { "peek left", "peek right", "peek over", "left corner link", "right corner link", "left in-line link",
		"right in-line link", "left corner auto", "right corner auto", "left in-line auto", "right in-line auto", "left corner external",
		"right corner external", "low", "ends left", "ends right" };
}

std::string COVER_DEBUG::SegmentFlagsText(uint32_t flags)
{
	return BitsText(flags, kCoverBitNames, 16);
}

std::string COVER_DEBUG::SlotFlagsText(uint32_t flags)
{
	return BitsText(flags, kCoverBitNames, 16);
}

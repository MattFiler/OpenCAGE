#pragma once

#include "DevTools.h"
#include <cstdint>
#include <string>
#include <vector>

/*
	Cover, jobs and traversals, in the debug menu (see DEBUG_MENU.h): what OpenCAGE's cover, traversal and job position
	bakes give the game, as the game holds them, and what the AI is doing with them now.

	How the game holds them, in short:
	- Cover: the level state's cover segments (a wall the AI can take cover along: its two ends, the way it faces, its
	  height, low or standing, links to the segments round its corners and in line with it, the script entity it came
	  from) and their slots (places along a segment, each with the angles it can aim over and round, and who has
	  reserved or is in it). The cover system also follows up to 18 characters: the segment and slot each is on or has
	  reserved, how it stands and which way it faces.
	- Traversals (vaults, ladders, vents, ledges...): segments with an entrance and an exit (two points each), a path of
	  up to 16 points, the classes that may use them and whether they are enabled. A nav mesh off-mesh connection names
	  the traversal it goes with. Each character keeps the traversals near it it could use, and the one it is on.
	- Jobs: the AI's things to do. The level's spotting, crawl-space spotting and assault positions (baked) sit in three
	  grids of cells; a job is only made for a position when its cell is near the player (the active cells), unless the
	  level asks for all of them. Every job made - those and the scripted ones (idle, despawn, area sweeps, search,
	  follow, panic...) - is kept in a list per job type, each with its tasks: a position and orientation, a state (not in
	  use, assigned, moving, performing...), who it is reserved by and who is doing it.

	Everything is read on the render thread at the frame's presentation, guarded (a level can be unloading); nothing in
	the game is changed. The segments and traversals are copied when the level's data changes, and the slots, tracked
	characters and jobs read again every half second while wanted (every frame while an overlay needs them).
*/
namespace COVER_DEBUG
{
	struct Segment
	{
		int uid = 0;
		float left[3] = {}, right[3] = {}, normal[3] = {};
		float height = 0.0f;
		uint32_t flags = 0;
		int leftCorner = 0, rightCorner = 0, leftColinear = 0, rightColinear = 0, traversal = 0;
		int cathodeIndex = -1;
		uint32_t entity = 0, parent = 0;  // the script entity it came from, and the composite instance it is in
		float length = 0.0f;
		bool enabled = false;
		int slotCount = 0, firstSlot = 0;
	};
	struct Slot
	{
		int uid = 0;
		float along = 0.0f;               // how far along its segment, 0..1
		uint32_t flags = 0;
		uint32_t occupantEntity = 0, occupantParent = 0;
		int status = 0;                   // 0 free, 1 reserved, 2 occupied
		int segment = 0;
		uint16_t aimHorizontal = 0;
		uint32_t aimVertical = 0;
		int alliance = 0;
	};
	// A slot uid the game uses for none (a followed character in no slot, a reservation not held)
	constexpr uint32_t kNoSlot = 0xFFFFFFFFu;
	struct Tracked
	{
		uint32_t entity = 0, parent = 0;  // the character, by its script entity
		int segment = 0, corner = 0, colinear = 0; // segment uids (0: none)
		uint32_t slot = kNoSlot, reserved[2] = {};
		int stance = 0;                   // 0 low, 1 standing
		int facing = 2;                   // 0 left, 1 right, 2 unset
		bool onPiece = false, haveReserved[2] = {};
		// In cover: on a segment. Out of it the game leaves the segment at 0 and the slot at none, and its stance and facing
		// are left over from the last time (the facing is never cleared), so they mean nothing then.
		bool inCover = false;
	};
	struct Traversal
	{
		int uid = 0;
		int type = 0;
		int cathodeIndex = -1;
		uint32_t entity = 0, parent = 0;
		float entrance[2][3] = {}, exit[2][3] = {};
		float path[16][3] = {};
		uint32_t classes = 0;
		uint32_t animationTree = 0;
		bool enabled = false;
	};
	struct Cover
	{
		bool level = false;
		bool have = false;                // the cover system exists
		bool loaded = false;              // and has the level's data
		int generation = 0;               // bumped when the segments are copied again
		std::vector<Segment> segments;    // by uid - 1
		std::vector<Slot> slots;          // by uid
		std::vector<Traversal> traversals; // by uid - 1
		Tracked tracked[18];
		int trackedCount = 0;
	};
	// Render thread: the cover system now (the slots and tracked characters read again each half second, or every frame
	// with fast set).
	const Cover& ReadCover(bool fast = false);
	const Segment* SegmentByUid(int uid);
	const Traversal* TraversalByUid(int uid);
	// A slot's place in the world (along its segment), false when it has none
	bool SlotPosition(const Slot& slot, float out[3]);

	struct Task
	{
		uintptr_t task = 0;
		int state = 0;
		float position[3] = {};
		float orientation = 0.0f;
		bool hasOrientation = false, startsInVent = false;
		const void* owner = nullptr;      // the character it is for (the game's character)
		const void* owningRole = nullptr, *reservedBy = nullptr;
		const void* owningCharacter = nullptr, *reservedCharacter = nullptr;
		int owningRoleType = -1, reservedRoleType = -1;
	};
	struct Job
	{
		uintptr_t job = 0;
		int type = 0;
		uint32_t entity = 0, parent = 0;  // its script entity (a temporary id for one made from a baked position)
		int opMode = 0;
		bool allTasks = false;
		std::vector<Task> tasks;
		int taskCount = 0;                // before the list was cut short
	};
	enum AutoKind { kSpotting, kCrawl, kAssault, kAutoKinds };
	struct AutoJob
	{
		int kind = 0;
		float position[3] = {};
		float task[3] = {};               // where the AI stands to do it (the spotting kinds)
		float yaw = 0.0f;                 // the way to face (assault)
		uintptr_t job = 0;                // the job made for it (0: none yet)
		bool active = false;              // its cell is near the player
		int cell = -1;
	};
	struct Grid
	{
		bool have = false;
		int xCells = 0, zCells = 0;
		float minX = 0.0f, minZ = 0.0f, unit = 0.0f;
		int items = 0, activeCells = 0;
	};
	struct Jobs
	{
		bool level = false;
		bool have = false;                // the job centre exists
		Grid grids[kAutoKinds];
		bool useAll[kAutoKinds] = {};
		std::vector<AutoJob> autoJobs;
		std::vector<Job> jobs;
	};
	// Render thread: the jobs now (read again each half second while wanted, or every frame with fast set).
	const Jobs& ReadJobs(bool fast = false);
	const Job* JobByAddress(uintptr_t job);

	// A character's traversal tracker: the traversals near it it could use (their script entities, which way and how
	// far), the closest, whether it can start one now and the one it is on
	struct Tracker
	{
		bool have = false;
		bool canTransition = false;
		int closest = -1;
		struct Near { int index = 0; uint32_t entity = 0, ref = 0; float distance = 0.0f; float direction[3] = {}; };
		std::vector<Near> nearby;
		uint32_t onTraversal = 0;
	};
	bool ReadTracker(const void* character, Tracker& out);
	// A character's script entity (two ids), false when it cannot be read
	bool CharacterHandle(const void* character, uint32_t& entity, uint32_t& parent);

	const char* TraversalTypeName(int type);
	const char* SlotStatusName(int status);
	const char* JobTypeName(int type);
	const char* TaskStateName(int state);
	const char* StanceName(int stance);
	const char* FacingName(int facing);
	const char* AutoKindName(int kind);
	std::string SegmentFlagsText(uint32_t flags);
	std::string SlotFlagsText(uint32_t flags);
	constexpr uint32_t kLowCover = 1u << 13;
}

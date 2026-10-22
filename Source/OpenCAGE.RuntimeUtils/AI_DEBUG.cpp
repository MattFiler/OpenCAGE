#include "AI_DEBUG.h"
#include "DEBUG_MENU.h"

#include <atomic>
#include <cmath>
#include <cstring>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <imgui.h>

/*
	Layouts and addresses are the Steam retail AI.exe's (see GameAddresses.h for the GOG and Epic Games Store builds).
	Each structure below is found from a pointer the one before it holds, so only the globals and the functions called
	have addresses of their own.
*/
namespace
{
	// Where the game keeps its pointer to its globals; the byte after that pointer is set while its characters can be read
	constexpr uintptr_t kGameGlobals = 0x012F0C88;
	constexpr uint32_t kCharactersValid = 0x04;
	// In the globals (pointers): the live head poses, the view cone tables, the group of NPCs (their awareness of the
	// player), the alien setups, and the names of the view cone types
	constexpr uint32_t kGlobalsHeads = 0x10;
	constexpr uint32_t kGlobalsViewcones = 0x14;
	constexpr uint32_t kGlobalsGroup = 0x5C;
	constexpr uint32_t kGlobalsAlienSetups = 0x84;
	constexpr uint32_t kGlobalsConeTypeNames = 0x9C;

	// The game's clock: the time now (a double, seconds), as its AI times things
	constexpr uintptr_t kGameTime = 0x012410A8;
	// The game's constants: the light meter's dark and fully lit thresholds (on the scaled brightness)
	constexpr uintptr_t kGlobalConstants = 0x01241108;
	constexpr uint32_t kConstantsDark = 0xD0;
	constexpr uint32_t kConstantsLight = 0xD4;
	// The same two thresholds on the plain brightness, worked out by the light meter each update for the screen tint
	constexpr uintptr_t kTintDark = 0x017DA9D4;
	constexpr uintptr_t kTintLight = 0x017DA9D8;

	// A character's fields
	constexpr uint32_t kCharacterId = 0x50;            // its unique id
	constexpr uint32_t kCharacterEntity = 0x54;        // its entity's id, then the id of the composite instance it is in
	constexpr uint32_t kCharacterInVent = 0x2B4;       // set while in a vent
	constexpr uint32_t kCharacterCrouched = 0x2AF;
	constexpr uint32_t kCharacterTorch = 0x2BA;        // its torch is out
	constexpr uint32_t kCharacterStunned = 0x2D5;
	constexpr uint32_t kCharacterInvisible = 0x2D9;    // its enemies' senses ignore it
	constexpr uint32_t kCharacterBackstage = 0x2DA;    // moving through the vents as the game's own map of them
	constexpr uint32_t kCharacterBackstageMove = 0x2DB;// going into or out of them
	constexpr uint32_t kCharacterSensed = 0x2EC;       // how hard its enemies sense it: none, nearly, rising, detected
	constexpr uint32_t kCharacterAlienAwareness = 0x318;
	constexpr uint32_t kCharacterHealthState = 0x32C;  // full, damaged, injured, down, dead
	constexpr uint32_t kCharacterHealth = 0x33C;       // now and most (16 bits each)
	constexpr uint32_t kCharacterMaxHealth = 0x33E;
	constexpr uint32_t kCharacterAction = 0x36C;       // what it is doing now (a pointer)
	constexpr uint32_t kCharacterConstants = 0x1154;   // its class's constants (a pointer)
	constexpr uint32_t kCharacterHidingSince = 0x3494; // when the player started hiding
	constexpr uint32_t kCharacterSweepArea = 0x349C;   // the alien's area to sweep (a pointer)
	constexpr uint32_t kCharacterTargeting = 0x34A8;   // its targeting (a pointer): the memory of its target at +0x10
	constexpr uint32_t kCharacterMood = 0x34BC;        // its moods (a pointer): the one shown at +0x20
	constexpr uint32_t kCharacterAggravation = 0x34CC; // its alertness at +8 and aggression at +0xC (a pointer)
	constexpr uint32_t kCharacterMenace = 0x34D0;      // the alien's menace (a pointer)
	constexpr uint32_t kCharacterMemory = 0x34D4;      // its memory of its enemies (a pointer)
	constexpr uint32_t kCharacterTree = 0x34D8;        // its behaviour tree (a pointer): the branch it is in at +0x54
	constexpr uint32_t kCharacterAlienData = 0x34F0;   // the alien's own (a pointer)
	constexpr uint32_t kCharacterStealth = 0x3D30;     // the player's stealth factors
	constexpr uint32_t kCharacterTimers = 0x3E4C;      // 25 timers, 0x28 bytes each
	constexpr uint32_t kCharacterFlags = 0x423C;       // 40 flags, a byte each
	constexpr uint32_t kCharacterSenseSystem = 0x4274; // the set of senses in use: normal, heightened, set 2, set 3
	constexpr uint32_t kCharacterLightMeter = 0x42A0;  // the player's light meter (a pointer)
	constexpr uint32_t kCharacterAlienSetup = 0x42A4;  // the alien's setup, as a number in the setups' list
	constexpr uint32_t kCharacterLocomotion = 0x42A8;  // its locomotion (a pointer)

	constexpr uint32_t kConstantsTreeName = 0x1EC;     // the behaviour tree's name (100 characters)
	constexpr uint32_t kConstantsViewconeSets[4] = { 0x280, 0x468, 0x610, 0x7B8 }; // the view cone set for each sense set

	constexpr uint32_t kTreeBranch = 0x54;
	constexpr uint32_t kTreePreviousBranch = 0x58;

	// A timer: when it ends, how long it lasts, when it was paused (-1 when not), and whether it is running
	constexpr uint32_t kTimerSize = 0x28;
	constexpr uint32_t kTimerEnd = 0x04;
	constexpr uint32_t kTimerDuration = 0x08;
	constexpr uint32_t kTimerPausedAt = 0x0C;
	constexpr uint32_t kTimerRunning = 0x10;

	// The group of NPCs: its members (a pointer and a count), each 0x6C bytes: the character and its unique id, and its
	// awareness of the player
	constexpr uint32_t kGroupMembers = 0x18;
	constexpr uint32_t kGroupCount = 0x1C;
	constexpr uint32_t kGroupMemberSize = 0x6C;
	constexpr uint32_t kMemberCharacter = 0x04;
	constexpr uint32_t kMemberId = 0x08;
	constexpr uint32_t kMemberAwareness = 0x58;

	// The live head poses (refreshed every AI update): a pointer to the entries and how many; in each entry the
	// character and its unique id, the way the head faces and where it is
	constexpr uint32_t kHeadsList = 0x48;
	constexpr uint32_t kHeadsCount = 0x4C;
	constexpr uint32_t kHeadCharacter = 0x08;
	constexpr uint32_t kHeadId = 0x0C;
	constexpr uint32_t kHeadForward = 0x20;
	constexpr uint32_t kHeadPosition = 0x2C;
	constexpr int kMaxHeads = 18;

	// The view cone tables: one per view cone set, 0x14 bytes each - the cones (a pointer) and how many; each cone 0x50
	// bytes: its length, the cosines of half its width and height, its type, and whether it is in use
	constexpr uint32_t kViewconeSetSize = 0x14;
	constexpr uint32_t kSetCones = 0x04;
	constexpr uint32_t kSetConeCount = 0x08;
	constexpr uint32_t kConeSize = 0x50;
	constexpr uint32_t kConeLength = 0x00;
	constexpr uint32_t kConeHorizontalDot = 0x08;
	constexpr uint32_t kConeVerticalDot = 0x0C;
	constexpr uint32_t kConeType = 0x44;
	constexpr uint32_t kConeValid = 0x4C;
	// A list of names: the names (64 characters each, a pointer) and how many
	constexpr uint32_t kNamesList = 0x04;
	constexpr uint32_t kNamesCount = 0x08;
	constexpr uint32_t kNameSize = 0x40;

	// The memory of enemies: the first of its list of records (each list node: the record, the one before, the one after)
	constexpr uint32_t kMemoryFirst = 0x0C;
	constexpr uint32_t kNodeNext = 0x08;
	constexpr int kMaxRecords = 32;
	// A record of one enemy: its eight senses (0x74 bytes each), the enemy and its unique id, whether it is a threat, the
	// distance to it, how much of it the last line-of-sight checks saw, and whether it is a remembered one
	constexpr uint32_t kRecordSenses = 0x18;
	constexpr uint32_t kSenseSize = 0x74;
	constexpr uint32_t kSenseLastPositions = 0x00;     // four positions (x, y, z), one per threshold
	constexpr uint32_t kSenseLastTimes = 0x34;         // four times
	constexpr uint32_t kSenseLevel = 0x5C;
	constexpr uint32_t kSenseTriggered = 0x70;
	constexpr uint32_t kRecordEnemy = 0x42C;
	constexpr uint32_t kRecordEnemyId = 0x430;
	constexpr uint32_t kRecordThreat = 0x43C;
	constexpr uint32_t kRecordDistance = 0x45C;
	constexpr uint32_t kRecordHits = 0x474;
	constexpr uint32_t kRecordRemembered = 0x490;
	constexpr uint32_t kTargetingRecord = 0x10;

	// The locomotion: how it moves (still, on a path, along a direction...), the speeds wanted and now (0..3), time
	// without progress, and its corridor (a pointer)
	constexpr uint32_t kLocomotionStatus = 0x08;
	constexpr uint32_t kLocomotionSpeedWanted = 0x194;
	constexpr uint32_t kLocomotionSpeedNow = 0x198;
	constexpr uint32_t kLocomotionStuckFor = 0x1A8;
	constexpr uint32_t kLocomotionCorridor = 0x1CC;
	// Its path: a list of the path's nodes, the current one, a list of those passed, a list of the requests to go
	// somewhere, the request being pathfound, whether it is pathfinding afresh, and the end point asked for. A list is its
	// end node (whose next is the first) and a count; a node holds the next, the one before, then its item.
	constexpr uint32_t kPathNodes = 0xC4;
	constexpr uint32_t kPathCurrent = 0xD0;
	constexpr uint32_t kPathPassed = 0xD4;
	constexpr uint32_t kPathRequests = 0xEC;
	constexpr uint32_t kPathActiveRequest = 0x104;
	constexpr uint32_t kPathFullRepath = 0x10C;
	constexpr uint32_t kPathRequestedEnd = 0x114;
	constexpr uint32_t kListNodeItem = 0x08;
	constexpr uint32_t kPathNodeType = 0x04;
	constexpr uint32_t kPathNodePosition = 0x0C;
	// A request: where to, how far is left, how its pathfinding and following went, and whether the destination is in
	// the vents (a bit of the byte at +0x90)
	constexpr uint32_t kRequestDestination = 0x00;
	constexpr uint32_t kRequestRemaining = 0x24;
	constexpr uint32_t kRequestPathStatus = 0x40;
	constexpr uint32_t kRequestFollowStatus = 0x44;
	constexpr uint32_t kRequestBits = 0x90;
	constexpr uint8_t kRequestBackstageBit = 0x08;
	// The corridor: its gaps (each 0x28 bytes: the middle, the left and right edges) and the corners it steers by, two
	// arrays given by their first and past-the-last entries
	constexpr uint32_t kCorridorPortals = 0x00;
	constexpr uint32_t kCorridorCorners = 0x10;
	constexpr uint32_t kCornerSize = 0x28;
	constexpr uint32_t kCornerLeft = 0x0C;
	constexpr uint32_t kCornerRight = 0x18;

	// The alien's menace: the gauge (a pointer: six thresholds, how full, the level reached), the time until it next
	// menaces (a timer), whether it is menacing, paused, has been seen in the player's view, will withdraw after its next
	// job, and how many times it has menaced the player
	constexpr uint32_t kMenaceTimer = 0x19C;
	constexpr uint32_t kMenaceGauge = 0x1E0;
	constexpr uint32_t kMenaceState = 0x1E4;
	constexpr uint32_t kMenacePaused = 0x1F1;
	constexpr uint32_t kMenaceSeen = 0x1F3;
	constexpr uint32_t kMenaceWithdrawAfter = 0x1F4;
	constexpr uint32_t kMenaceCount = 0x1F5;
	constexpr uint32_t kGaugeThresholds = 0x04;
	constexpr uint32_t kGaugeAmount = 0x68;
	constexpr uint32_t kGaugeLevel = 0x74;
	// The alien's own: killtraps asked for by a script or by its tree, ambushing, its ambush decision, its withdrawal (a
	// pointer: the state first) and its vents (a pointer: the vents, 0x30 bytes each with their data at +0x28 - the top
	// of the vent first - and the best vents near the alien and near the player, two each)
	constexpr uint32_t kAlienScriptKilltraps = 0x0C;
	constexpr uint32_t kAlienTreeKilltraps = 0x0D;
	constexpr uint32_t kAlienAmbushing = 0x0E;
	constexpr uint32_t kAlienAmbushDecision = 0x10;
	constexpr uint32_t kAlienWithdrawal = 0x18;
	constexpr uint32_t kAlienVents = 0x24;
	constexpr uint32_t kVentsList = 0x08;
	constexpr uint32_t kVentsCount = 0x0C;
	constexpr uint32_t kVentSize = 0x30;
	constexpr uint32_t kVentData = 0x28;
	constexpr uint32_t kVentsNearAlien = 0x40;
	constexpr uint32_t kVentsNearPlayer = 0x48;
	// The alien setups: their names (a list of names, as above, at +0x14)
	constexpr uint32_t kSetupNames = 0x14;
	// The area to sweep: where, whether there is one, how it is swept and its radius
	constexpr uint32_t kSweepPosition = 0x00;
	constexpr uint32_t kSweepHave = 0x0C;
	constexpr uint32_t kSweepType = 0x10;
	constexpr uint32_t kSweepRadius = 0x14;
	// The box the game works out from it: its middle, half its width and length, and the way its length runs (a yaw)
	constexpr uint32_t kBoxCentre = 0x00;
	constexpr uint32_t kBoxHalfWidth = 0x10;
	constexpr uint32_t kBoxHalfLength = 0x14;
	constexpr uint32_t kBoxAngle = 0x18;
	// What a character is doing: its type at +0x1A; moving through the vents (75), where to at +0x70, and the same
	// without a path (76), where to at +0x80
	constexpr uint32_t kActionType = 0x1A;
	constexpr uint8_t kActionBackstageMove = 75;
	constexpr uint8_t kActionBackstageMoveNoPath = 76;
	constexpr uint32_t kBackstageMoveTarget = 0x70;
	constexpr uint32_t kBackstageMoveNoPathTarget = 0x80;

	// The player's light meter: three sample points and the light found at each (four floats apart), their average
	// brightness, that through the AI's light curve, and the level
	constexpr uint32_t kMeterSamplePositions = 0x20;
	constexpr uint32_t kMeterSamples = 0x50;
	constexpr uint32_t kMeterVectorSize = 0x10;
	constexpr uint32_t kMeterCombined = 0x90;
	constexpr uint32_t kMeterScaled = 0x94;
	constexpr uint32_t kMeterLevel = 0x98;
	// The player's stealth factors
	constexpr uint32_t kStealthExposure = 0x38;
	constexpr uint32_t kStealthStance = 0x3C;
	constexpr uint32_t kStealthCover = 0x40;
	constexpr uint32_t kStealthMovement = 0x44;
	constexpr uint32_t kStealthMeterEnabled = 0xD8;
	constexpr int kFlagPlayerHiding = 13;
	constexpr int kFlagInVent = 3;

	constexpr int kAlienClass = 1;

	// The game's own code that only reads, called guarded:
	// - the threshold a sense is measured against, for a class's constants, set of senses, sense and threshold
	typedef float(__cdecl* t_sense_threshold)(const void* constants, int senseSystem, int sense, int threshold);
	const auto sense_threshold = reinterpret_cast<t_sense_threshold>(DEVTOOLS_RELATIVE_ADDRESS(0x002CBFE0));
	// - how far a character hears (its constants and the difficulty's, at least 5 m)
	typedef float(__thiscall* t_hearing_distance)(const void* character);
	const auto hearing_distance = reinterpret_cast<t_hearing_distance>(DEVTOOLS_RELATIVE_ADDRESS(0x00375F10));
	// - the area the alien sweeps, as the box its stalking works within (written to the box given)
	typedef void(__cdecl* t_sweep_box)(const void* alien, void* box);
	const auto sweep_box = reinterpret_cast<t_sweep_box>(DEVTOOLS_RELATIVE_ADDRESS(0x003586A0));
	// - how many times an alien setup may menace the player at the difficulty played
	typedef unsigned(__thiscall* t_max_menaces)(const void* setups, unsigned setup);
	const auto max_menaces = reinterpret_cast<t_max_menaces>(DEVTOOLS_RELATIVE_ADDRESS(0x002E2D50));

	// The light meter's screen tint: the game's per-frame hand-over of the tint's numbers to the shaders, in which the
	// call at +0x12 asks whether it is on (always "off" in the shipped game) and, when it is, the call at +0x1B asks for
	// its opacity - read from the global below
	constexpr uintptr_t kTintUpload = 0x00619B70;
	constexpr uint32_t kTintAskOn = 0x12;
	constexpr uint32_t kTintAskOpacity = 0x1B;
	constexpr uintptr_t kTintOpacity = 0x0123E98C;

	AI_DEBUG::Snapshot g_snapshot;

	template <typename T> T At(const uint8_t* base, uint32_t offset)
	{
		return *reinterpret_cast<const T*>(base + offset);
	}

	const uint8_t* Pointer(const uint8_t* base, uint32_t offset)
	{
		return *reinterpret_cast<const uint8_t* const*>(base + offset);
	}

	void Copy3(float out[3], const uint8_t* from)
	{
		memcpy(out, from, sizeof(float) * 3);
	}

	// The game's globals, while its characters can be read (null otherwise)
	const uint8_t* Globals()
	{
		__try
		{
			const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kGameGlobals);
			if (!at || !*reinterpret_cast<const uint8_t*>(at + kCharactersValid))
				return nullptr;
			return *reinterpret_cast<const uint8_t* const*>(at);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	bool ReadClock(AI_DEBUG::Snapshot& out)
	{
		__try
		{
			const uintptr_t time = DEVTOOLS_RELATIVE_ADDRESS(kGameTime);
			const uintptr_t constants = DEVTOOLS_RELATIVE_ADDRESS(kGlobalConstants);
			const uintptr_t tintDark = DEVTOOLS_RELATIVE_ADDRESS(kTintDark);
			const uintptr_t tintLight = DEVTOOLS_RELATIVE_ADDRESS(kTintLight);
			if (!time)
				return false;
			out.now = static_cast<float>(*reinterpret_cast<const double*>(time));
			if (constants)
			{
				out.darkThreshold = *reinterpret_cast<const float*>(constants + kConstantsDark);
				out.lightThreshold = *reinterpret_cast<const float*>(constants + kConstantsLight);
			}
			if (tintDark && tintLight)
			{
				out.tintDarkThreshold = *reinterpret_cast<const float*>(tintDark);
				out.tintLightThreshold = *reinterpret_cast<const float*>(tintLight);
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// Who is who in the game's own lists: a character and its unique id, with what the list says of it
	struct Entry
	{
		const uint8_t* character;
		uint32_t id;
		int awareness;
		float forward[3];
		float position[3];
	};

	int ReadGroup(const uint8_t* globals, Entry* out, int max)
	{
		__try
		{
			const uint8_t* group = Pointer(globals, kGlobalsGroup);
			if (!group)
				return 0;
			const uint8_t* members = Pointer(group, kGroupMembers);
			const uint32_t count = At<uint32_t>(group, kGroupCount);
			int found = 0;
			for (uint32_t i = 0; members && i < count && found < max; i++)
			{
				const uint8_t* member = members + i * kGroupMemberSize;
				out[found].character = Pointer(member, kMemberCharacter);
				out[found].id = At<uint32_t>(member, kMemberId);
				out[found].awareness = At<int>(member, kMemberAwareness);
				found++;
			}
			return found;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	int ReadHeads(const uint8_t* globals, Entry* out, int max)
	{
		__try
		{
			const uint8_t* heads = Pointer(globals, kGlobalsHeads);
			if (!heads)
				return 0;
			const uint8_t* const* list = reinterpret_cast<const uint8_t* const*>(Pointer(heads, kHeadsList));
			const uint32_t count = At<uint32_t>(heads, kHeadsCount);
			int found = 0;
			for (uint32_t i = 0; list && i < count && i < kMaxHeads && found < max; i++)
			{
				const uint8_t* head = list[i];
				if (!head)
					continue;
				out[found].character = Pointer(head, kHeadCharacter);
				out[found].id = At<uint32_t>(head, kHeadId);
				Copy3(out[found].forward, head + kHeadForward);
				Copy3(out[found].position, head + kHeadPosition);
				found++;
			}
			return found;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	// A name from one of the game's lists of names (empty when there is none)
	void ReadListName(const uint8_t* names, uint32_t index, char* out, size_t size)
	{
		out[0] = '\0';
		__try
		{
			if (!names || index >= At<uint32_t>(names, kNamesCount))
				return;
			const uint8_t* list = Pointer(names, kNamesList);
			if (!list)
				return;
			const size_t length = size - 1 < kNameSize ? size - 1 : kNameSize;
			memcpy(out, list + index * kNameSize, length);
			out[length] = '\0';
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			out[0] = '\0';
		}
	}

	// The state every character has read for it: who it is, its health, its awareness, mood and branch, its flags and
	// timers, and what it targets (the pointer and id, matched to the list afterwards)
	bool ReadState(const uint8_t* character, float now, AI_DEBUG::Character& out, const uint8_t*& target, uint32_t& targetId)
	{
		target = nullptr;
		targetId = 0;
		__try
		{
			out.entityId = At<uint32_t>(character, kCharacterEntity);
			out.compositeInstanceId = At<uint32_t>(character, kCharacterEntity + 4);
			out.healthState = At<int>(character, kCharacterHealthState);
			out.health = At<uint16_t>(character, kCharacterHealth);
			out.maxHealth = At<uint16_t>(character, kCharacterMaxHealth);
			out.inVent = At<uint8_t>(character, kCharacterInVent) != 0;
			out.crouched = At<uint8_t>(character, kCharacterCrouched) != 0;
			out.torch = At<uint8_t>(character, kCharacterTorch) != 0;
			out.stunned = At<uint8_t>(character, kCharacterStunned) != 0;
			out.invisible = At<uint8_t>(character, kCharacterInvisible) != 0;
			out.backstage = At<uint8_t>(character, kCharacterBackstage) != 0;
			out.backstageTransition = At<uint8_t>(character, kCharacterBackstageMove) != 0;
			out.sensed = At<int>(character, kCharacterSensed);
			out.alienAwareness = At<int>(character, kCharacterAlienAwareness);
			out.senseSystem = At<int>(character, kCharacterSenseSystem);
			out.hidingSince = At<float>(character, kCharacterHidingSince);
			for (int i = 0; i < AI_DEBUG::kFlags; i++)
				out.flags[i] = At<uint8_t>(character, kCharacterFlags + i) != 0;

			const uint8_t* constants = Pointer(character, kCharacterConstants);
			if (constants)
			{
				memcpy(out.treeName, constants + kConstantsTreeName, sizeof(out.treeName) - 1);
				out.treeName[sizeof(out.treeName) - 1] = '\0';
				if (out.senseSystem >= 0 && out.senseSystem < 4)
					out.viewconeSet = At<int>(constants, kConstantsViewconeSets[out.senseSystem]);
			}

			const uint8_t* aggravation = Pointer(character, kCharacterAggravation);
			if (aggravation)
			{
				out.alertness = At<int>(aggravation, 0x08);
				out.aggro = At<int>(aggravation, 0x0C);
			}
			const uint8_t* mood = Pointer(character, kCharacterMood);
			if (mood)
				out.mood = At<int>(mood, 0x20);
			const uint8_t* tree = Pointer(character, kCharacterTree);
			if (tree)
			{
				out.branch = At<int>(tree, kTreeBranch);
				out.previousBranch = At<int>(tree, kTreePreviousBranch);
			}

			out.timerCount = 0;
			for (int i = 0; i < AI_DEBUG::kTimers; i++)
			{
				const uint8_t* timer = character + kCharacterTimers + i * kTimerSize;
				if (!At<uint8_t>(timer, kTimerRunning))
					continue;
				const float pausedAt = At<float>(timer, kTimerPausedAt);
				const bool paused = pausedAt != -1.0f;
				const float remaining = At<float>(timer, kTimerEnd) - (paused ? pausedAt : now);
				if (remaining <= 0.0f)
					continue;
				AI_DEBUG::Timer& shown = out.timers[out.timerCount++];
				shown.type = i;
				shown.remaining = remaining;
				shown.duration = At<float>(timer, kTimerDuration);
				shown.paused = paused;
			}

			const uint8_t* targeting = Pointer(character, kCharacterTargeting);
			const uint8_t* record = targeting ? Pointer(targeting, kTargetingRecord) : nullptr;
			if (record)
			{
				target = Pointer(record, kRecordEnemy);
				targetId = At<uint32_t>(record, kRecordEnemyId);
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// The view cones in use, and their type names
	bool ReadCones(const uint8_t* globals, AI_DEBUG::Character& out)
	{
		out.coneCount = 0;
		__try
		{
			const uint8_t* tables = Pointer(globals, kGlobalsViewcones);
			if (!tables || out.viewconeSet < 0 || out.viewconeSet >= 8)
				return false;
			const uint8_t* set = tables + out.viewconeSet * kViewconeSetSize;
			const uint8_t* cones = Pointer(set, kSetCones);
			const uint32_t count = At<uint32_t>(set, kSetConeCount);
			const uint8_t* names = Pointer(globals, kGlobalsConeTypeNames);
			for (uint32_t i = 0; cones && i < count && out.coneCount < AI_DEBUG::kMaxCones; i++)
			{
				const uint8_t* cone = cones + i * kConeSize;
				AI_DEBUG::Cone& shown = out.cones[out.coneCount++];
				shown.length = At<float>(cone, kConeLength);
				shown.horizontalDot = At<float>(cone, kConeHorizontalDot);
				shown.verticalDot = At<float>(cone, kConeVerticalDot);
				shown.valid = At<uint8_t>(cone, kConeValid) != 0;
				ReadListName(names, At<uint32_t>(cone, kConeType), shown.typeName, sizeof(shown.typeName));
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool ReadHearing(const uint8_t* character, float& out)
	{
		__try
		{
			if (!hearing_distance)
				return false;
			out = hearing_distance(character);
			return std::isfinite(out);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	float Threshold(const uint8_t* constants, int senseSystem, int sense, int threshold)
	{
		__try
		{
			return sense_threshold && constants ? sense_threshold(constants, senseSystem, sense, threshold) : 0.0f;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0.0f;
		}
	}

	// The memory of one enemy: the target's when there is one, or else the player's, or else the first; the enemy is
	// given back as a pointer and id, matched to the list afterwards
	bool ReadSenses(const uint8_t* character, const uint8_t* player, uint32_t playerId, AI_DEBUG::Character& out, const uint8_t*& enemy, uint32_t& enemyId)
	{
		enemy = nullptr;
		enemyId = 0;
		out.haveRecord = false;
		out.recordCount = 0;
		__try
		{
			const uint8_t* memory = Pointer(character, kCharacterMemory);
			const uint8_t* constants = Pointer(character, kCharacterConstants);
			if (!memory || !constants)
				return false;
			const uint8_t* targeting = Pointer(character, kCharacterTargeting);
			const uint8_t* targetRecord = targeting ? Pointer(targeting, kTargetingRecord) : nullptr;
			const uint8_t* playerRecord = nullptr;
			const uint8_t* firstRecord = nullptr;
			const uint8_t* node = Pointer(memory, kMemoryFirst);
			for (int i = 0; node && i < kMaxRecords; i++)
			{
				const uint8_t* record = Pointer(node, 0);
				if (record)
				{
					out.recordCount++;
					if (!firstRecord)
						firstRecord = record;
					if (player && Pointer(record, kRecordEnemy) == player && At<uint32_t>(record, kRecordEnemyId) == playerId)
						playerRecord = record;
				}
				node = Pointer(node, kNodeNext);
			}
			const uint8_t* record = targetRecord ? targetRecord : playerRecord ? playerRecord : firstRecord;
			if (!record)
				return false;
			out.recordIsTarget = record == targetRecord;
			enemy = Pointer(record, kRecordEnemy);
			enemyId = At<uint32_t>(record, kRecordEnemyId);
			out.recordDistance = At<float>(record, kRecordDistance);
			out.hitsFraction = At<float>(record, kRecordHits);
			out.threatStatus = At<int>(record, kRecordThreat);
			for (int s = 0; s < AI_DEBUG::kSenses; s++)
			{
				const uint8_t* sense = record + kRecordSenses + s * kSenseSize;
				AI_DEBUG::Sense& shown = out.senses[s];
				shown.level = At<float>(sense, kSenseLevel);
				shown.triggered = At<uint8_t>(sense, kSenseTriggered) != 0;
				memcpy(shown.lastPosition, sense + kSenseLastPositions, sizeof(shown.lastPosition));
				memcpy(shown.lastTime, sense + kSenseLastTimes, sizeof(shown.lastTime));
				for (int t = 0; t < AI_DEBUG::kThresholds; t++)
					shown.thresholds[t] = Threshold(constants, out.senseSystem, s, t);
			}
			out.haveRecord = true;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// How close a character is to spotting the player, as the game works it out for the music and the like: its target
	// must be the player, and it measures its combined meter against its activated threshold (-1 when it doesn't count)
	float ReadSpotting(const uint8_t* character, const uint8_t* player, uint32_t playerId, int senseSystem)
	{
		__try
		{
			if (!player || At<uint16_t>(character, kCharacterHealth) == 0 || At<int>(character, kCharacterHealthState) >= 3)
				return -1.0f;
			const uint8_t* targeting = Pointer(character, kCharacterTargeting);
			const uint8_t* record = targeting ? Pointer(targeting, kTargetingRecord) : nullptr;
			if (!record || !At<uint8_t>(record, kRecordRemembered))
				return -1.0f;
			if (Pointer(record, kRecordEnemy) != player || At<uint32_t>(record, kRecordEnemyId) != playerId)
				return -1.0f;
			const float level = At<float>(record, kRecordSenses + AI_DEBUG::kCombinedSense * kSenseSize + kSenseLevel);
			const float threshold = Threshold(Pointer(character, kCharacterConstants), senseSystem, AI_DEBUG::kCombinedSense, AI_DEBUG::kActivated);
			return threshold > 0.0f ? level / threshold : -1.0f;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return -1.0f;
		}
	}

	// One of the game's lists: the positions of its nodes' path nodes, in order (up to max), and how many it has
	int ReadPathList(const uint8_t* list, float (*out)[3], int* types, int max, const uint8_t* current, int* currentIndex, int* total)
	{
		const uint8_t* end = Pointer(list, 0);
		int count = 0, seen = 0;
		const uint8_t* node = end ? Pointer(end, 0) : nullptr;
		while (node && node != end && seen < 1024)
		{
			if (node == current && currentIndex)
				*currentIndex = count;
			const uint8_t* pathNode = Pointer(node, kListNodeItem);
			if (pathNode && count < max)
			{
				Copy3(out[count], pathNode + kPathNodePosition);
				if (types)
					types[count] = At<int>(pathNode, kPathNodeType);
				count++;
			}
			seen++;
			node = Pointer(node, 0);
		}
		if (total)
			*total = seen;
		return count;
	}

	bool ReadPath(const uint8_t* character, AI_DEBUG::Path& out)
	{
		out.have = false;
		__try
		{
			const uint8_t* actor = Pointer(character, kCharacterLocomotion);
			if (!actor)
				return false;
			out.status = At<int>(actor, kLocomotionStatus);
			out.speedWanted = At<int>(actor, kLocomotionSpeedWanted);
			out.speedNow = At<int>(actor, kLocomotionSpeedNow);
			out.stuckFor = At<float>(actor, kLocomotionStuckFor);
			out.fullRepath = At<uint8_t>(actor, kPathFullRepath) != 0;
			Copy3(out.requestedEnd, actor + kPathRequestedEnd);

			const uint8_t* current = Pointer(actor, kPathCurrent);
			out.currentNode = -1;
			out.nodeCount = ReadPathList(actor + kPathNodes, out.nodes, out.nodeTypes, AI_DEBUG::kMaxPath, current, &out.currentNode, &out.totalNodes);
			// The passed nodes: only the last of them are kept
			float passed[64][3];
			int passedTotal = 0;
			const int passedCount = ReadPathList(actor + kPathPassed, passed, nullptr, 64, nullptr, nullptr, &passedTotal);
			const int first = passedCount > AI_DEBUG::kMaxPassed ? passedCount - AI_DEBUG::kMaxPassed : 0;
			out.passedCount = passedCount - first;
			memcpy(out.passed, passed + first, sizeof(float) * 3 * out.passedCount);

			// The request it follows: the first of its requests, or else the one being pathfound
			const uint8_t* requestsEnd = Pointer(actor, kPathRequests);
			out.requestCount = At<int>(actor, kPathRequests + 4);
			const uint8_t* request = nullptr;
			if (requestsEnd)
			{
				const uint8_t* firstNode = Pointer(requestsEnd, 0);
				if (firstNode && firstNode != requestsEnd)
					request = Pointer(firstNode, kListNodeItem);
			}
			if (!request)
				request = Pointer(actor, kPathActiveRequest);
			out.haveRequest = request != nullptr;
			if (request)
			{
				Copy3(out.destination, request + kRequestDestination);
				out.remaining = At<float>(request, kRequestRemaining);
				out.pathStatus = At<int>(request, kRequestPathStatus);
				out.followStatus = At<int>(request, kRequestFollowStatus);
				out.destinationBackstage = (At<uint8_t>(request, kRequestBits) & kRequestBackstageBit) != 0;
			}

			out.portalCount = 0;
			out.cornerCount = 0;
			const uint8_t* corridor = Pointer(actor, kLocomotionCorridor);
			if (corridor)
			{
				const uint8_t* portal = Pointer(corridor, kCorridorPortals);
				const uint8_t* portalEnd = Pointer(corridor, kCorridorPortals + 4);
				for (; portal && portal < portalEnd && out.portalCount < AI_DEBUG::kMaxCorridor; portal += kCornerSize)
				{
					Copy3(out.portals[out.portalCount][0], portal + kCornerLeft);
					Copy3(out.portals[out.portalCount][1], portal + kCornerRight);
					out.portalCount++;
				}
				const uint8_t* corner = Pointer(corridor, kCorridorCorners);
				const uint8_t* cornerEnd = Pointer(corridor, kCorridorCorners + 4);
				for (; corner && corner < cornerEnd && out.cornerCount < AI_DEBUG::kMaxCorners; corner += kCornerSize)
					Copy3(out.corners[out.cornerCount++], corner);
			}
			out.have = true;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool ReadSweepBox(const uint8_t* alien, AI_DEBUG::Alien& out)
	{
		// Render thread only, so one box will do (the game writes its middle with aligned stores)
		alignas(16) static uint8_t box[0x20];
		memset(box, 0, sizeof(box));
		__try
		{
			if (!sweep_box)
				return false;
			sweep_box(alien, box);
			Copy3(out.boxCentre, box + kBoxCentre);
			out.boxHalfWidth = At<float>(box, kBoxHalfWidth);
			out.boxHalfLength = At<float>(box, kBoxHalfLength);
			out.boxAngle = At<float>(box, kBoxAngle);
			return std::isfinite(out.boxHalfWidth) && std::isfinite(out.boxHalfLength) && std::isfinite(out.boxAngle);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool ReadVent(const uint8_t* vents, uint32_t index, float out[3])
	{
		__try
		{
			if (index >= At<uint32_t>(vents, kVentsCount))
				return false;
			const uint8_t* list = Pointer(vents, kVentsList);
			const uint8_t* data = list ? Pointer(list + index * kVentSize, kVentData) : nullptr;
			if (!data)
				return false;
			Copy3(out, data);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool ReadAlien(const uint8_t* globals, const uint8_t* alien, float now, AI_DEBUG::Alien& out)
	{
		__try
		{
			const uint8_t* menace = Pointer(alien, kCharacterMenace);
			if (menace)
			{
				const uint8_t* gauge = Pointer(menace, kMenaceGauge);
				if (gauge)
				{
					memcpy(out.gaugeThresholds, gauge + kGaugeThresholds, sizeof(out.gaugeThresholds));
					out.gauge = At<float>(gauge, kGaugeAmount);
					out.gaugeLevel = At<int>(gauge, kGaugeLevel);
					out.haveGauge = true;
				}
				out.menacing = At<int>(menace, kMenaceState) != 0;
				out.paused = At<uint8_t>(menace, kMenacePaused) != 0;
				out.seenInCamera = At<uint8_t>(menace, kMenaceSeen) != 0;
				out.withdrawAfterNextJob = At<uint8_t>(menace, kMenaceWithdrawAfter) != 0;
				out.menaces = At<uint8_t>(menace, kMenaceCount);
				const uint8_t* timer = menace + kMenaceTimer;
				const float pausedAt = At<float>(timer, kTimerPausedAt);
				out.menaceTimerRemaining = At<float>(timer, kTimerEnd) - (pausedAt != -1.0f ? pausedAt : now);
				out.menaceTimerDuration = At<float>(timer, kTimerDuration);
			}

			const uint8_t* setups = Pointer(globals, kGlobalsAlienSetups);
			const uint32_t setup = At<uint32_t>(alien, kCharacterAlienSetup);
			if (setups)
			{
				ReadListName(setups + kSetupNames, setup, out.configName, sizeof(out.configName));
				if (max_menaces && setup != 0xFFFFFFFF)
					out.maxMenaces = static_cast<int>(max_menaces(setups, setup));
			}

			const uint8_t* data = Pointer(alien, kCharacterAlienData);
			if (data)
			{
				out.scriptKilltraps = At<uint8_t>(data, kAlienScriptKilltraps) != 0;
				out.treeKilltraps = At<uint8_t>(data, kAlienTreeKilltraps) != 0;
				out.ambushing = At<uint8_t>(data, kAlienAmbushing) != 0;
				out.ambushDecision = At<int>(data, kAlienAmbushDecision);
				const uint8_t* withdrawal = Pointer(data, kAlienWithdrawal);
				if (withdrawal)
					out.withdrawState = At<int>(withdrawal, 0);
				const uint8_t* vents = Pointer(data, kAlienVents);
				if (vents)
				{
					out.ventCount = At<int>(vents, kVentsCount);
					out.haveVentNearAlien = ReadVent(vents, At<uint32_t>(vents, kVentsNearAlien), out.ventNearAlien);
					out.haveVentNearPlayer = ReadVent(vents, At<uint32_t>(vents, kVentsNearPlayer), out.ventNearPlayer);
				}
			}

			const uint8_t* sweep = Pointer(alien, kCharacterSweepArea);
			if (sweep)
			{
				Copy3(out.sweepPosition, sweep + kSweepPosition);
				out.haveSweep = At<uint8_t>(sweep, kSweepHave) != 0;
				out.sweepType = At<int>(sweep, kSweepType);
				out.sweepRadius = At<float>(sweep, kSweepRadius);
				out.haveBox = ReadSweepBox(alien, out);
			}

			if (At<uint8_t>(alien, kCharacterBackstage))
			{
				const uint8_t* action = Pointer(alien, kCharacterAction);
				const uint8_t type = action ? At<uint8_t>(action, kActionType) : 0;
				if (type == kActionBackstageMove || type == kActionBackstageMoveNoPath)
				{
					Copy3(out.backstageTarget, action + (type == kActionBackstageMove ? kBackstageMoveTarget : kBackstageMoveNoPathTarget));
					out.haveBackstageTarget = true;
				}
			}
			out.read = true;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool ReadPlayer(const uint8_t* player, AI_DEBUG::Player& out)
	{
		__try
		{
			const uint8_t* meter = Pointer(player, kCharacterLightMeter);
			if (meter)
			{
				for (int i = 0; i < 3; i++)
				{
					Copy3(out.samplePositions[i], meter + kMeterSamplePositions + i * kMeterVectorSize);
					Copy3(out.samples[i], meter + kMeterSamples + i * kMeterVectorSize);
				}
				out.combined = At<float>(meter, kMeterCombined);
				out.scaled = At<float>(meter, kMeterScaled);
				out.level = At<int>(meter, kMeterLevel);
				out.haveLightMeter = true;
			}
			const uint8_t* stealth = player + kCharacterStealth;
			out.exposure = At<float>(stealth, kStealthExposure);
			out.stance = At<float>(stealth, kStealthStance);
			out.cover = At<float>(stealth, kStealthCover);
			out.movement = At<float>(stealth, kStealthMovement);
			out.meterEnabled = At<uint8_t>(stealth, kStealthMeterEnabled) != 0;
			out.haveStealth = true;
			out.hiding = At<uint8_t>(player, kCharacterFlags + kFlagPlayerHiding) != 0;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	float Distance(const float a[3], const float b[3])
	{
		const float dx = a[0] - b[0], dy = a[1] - b[1], dz = a[2] - b[2];
		return std::sqrt(dx * dx + dy * dy + dz * dz);
	}

	// The game's own test for whether a point is in a view cone, from the head's position and the way it faces: within
	// the cone's length across and up or down, then the way to the point, flattened, against the cone's width, and the
	// way to it in the head's upright plane against its height (cones wider than a half turn are tested as the game does)
	bool InCone(const AI_DEBUG::Cone& cone, const float head[3], const float forward[3], const float point[3])
	{
		const float dx = point[0] - head[0], dy = point[1] - head[1], dz = point[2] - head[2];
		const float across = std::sqrt(dx * dx + dz * dz);
		if (across > cone.length || std::fabs(dy) > cone.length)
			return false;
		const float length = std::sqrt(across * across + dy * dy);
		if (length <= 0.0f)
			return true;
		const float nx = dx / length, ny = dy / length, nz = dz / length;
		// Across: the flattened way to the point against the way the head faces
		float horizontal = 0.0f;
		if (across > 0.0f)
			horizontal = forward[0] * (dx / across) + forward[2] * (dz / across);
		// Upright: the way to the point with the head's sideways part taken out
		float rx = -forward[2], rz = forward[0];
		const float right = std::sqrt(rx * rx + rz * rz);
		float vertical = 1.0f;
		if (right > 0.0f)
		{
			rx /= right;
			rz /= right;
			const float side = rx * nx + rz * nz;
			float px = nx - rx * side, py = ny, pz = nz - rz * side;
			const float upright = std::sqrt(px * px + py * py + pz * pz);
			if (upright > 0.0f)
			{
				px /= upright;
				py /= upright;
				pz /= upright;
			}
			vertical = forward[0] * px + forward[1] * py + forward[2] * pz;
		}
		const float h = cone.horizontalDot, v = cone.verticalDot;
		if (h >= 0.0f && v >= 0.0f)
			return horizontal >= h && vertical >= v;
		if (h < 0.0f && v < 0.0f)
			return horizontal >= h || vertical >= v;
		if (h < 0.0f)
			return horizontal >= h && std::fabs(vertical) >= v;
		return std::fabs(horizontal) >= h && vertical >= v;
	}

	int IndexOf(const AI_DEBUG::Snapshot& snapshot, const uint8_t* object, uint32_t id)
	{
		if (!object)
			return -1;
		for (size_t i = 0; i < snapshot.characters.size(); i++)
			if (snapshot.characters[i].object == object && snapshot.characters[i].id == id)
				return static_cast<int>(i);
		return -1;
	}

	void ReadSnapshot(AI_DEBUG::Snapshot& snapshot)
	{
		snapshot.characters.clear();
		snapshot.aliens.clear();
		snapshot.player = AI_DEBUG::Player();
		const std::vector<DEBUG_MENU::Character>& list = DEBUG_MENU::Characters();
		const uint8_t* globals = list.empty() ? nullptr : Globals();
		snapshot.level = globals != nullptr;
		if (!globals)
			return;
		ReadClock(snapshot);

		float camera[3] = {};
		const bool haveCamera = DEBUG_MENU::CameraPosition(camera);
		const uint32_t selected = DEBUG_MENU::SelectedId();
		// With many characters, the full detail is read for those near the camera and the selected one only
		constexpr float kDetailRange = 60.0f;
		constexpr size_t kAllDetailedUpTo = 8;
		const bool allDetailed = list.size() <= kAllDetailedUpTo;

		snapshot.characters.resize(list.size());
		for (size_t i = 0; i < list.size(); i++)
		{
			const DEBUG_MENU::Character& from = list[i];
			AI_DEBUG::Character& out = snapshot.characters[i];
			out.object = from.object;
			out.id = from.id;
			out.characterClass = from.characterClass;
			out.player = from.player;
			out.dead = from.dead;
			out.alien = from.characterClass == kAlienClass;
			out.selected = from.id == selected;
			memcpy(out.position, from.position, sizeof(out.position));
			out.facing = from.facing;
			strncpy_s(out.name, from.name.c_str(), _TRUNCATE);
			out.distance = haveCamera ? Distance(camera, out.position) : 0.0f;
			out.detailed = !out.dead && (out.selected || allDetailed || out.distance <= kDetailRange);
			if (out.player && snapshot.player.character < 0)
				snapshot.player.character = static_cast<int>(i);
		}
		const AI_DEBUG::Character* player = snapshot.player.character >= 0 ? &snapshot.characters[snapshot.player.character] : nullptr;
		const uint8_t* playerObject = player ? player->object : nullptr;
		const uint32_t playerId = player ? player->id : 0;

		Entry group[64];
		const int groupCount = ReadGroup(globals, group, 64);
		Entry heads[kMaxHeads];
		const int headCount = ReadHeads(globals, heads, kMaxHeads);

		for (size_t i = 0; i < snapshot.characters.size(); i++)
		{
			AI_DEBUG::Character& out = snapshot.characters[i];
			const uint8_t* target = nullptr;
			uint32_t targetId = 0;
			out.read = ReadState(out.object, snapshot.now, out, target, targetId);
			if (!out.read)
				continue;
			out.target = IndexOf(snapshot, target, targetId);
			out.haveTarget = target != nullptr;
			for (int g = 0; g < groupCount; g++)
				if (group[g].character == out.object && group[g].id == out.id)
					out.awareness = group[g].awareness;
			for (int h = 0; h < headCount; h++)
				if (heads[h].character == out.object && heads[h].id == out.id)
				{
					memcpy(out.headPosition, heads[h].position, sizeof(out.headPosition));
					memcpy(out.headForward, heads[h].forward, sizeof(out.headForward));
					out.haveHead = true;
				}
			if (!out.player && !out.dead)
			{
				out.spotting = ReadSpotting(out.object, playerObject, playerId, out.senseSystem);
				if (out.alien)
					snapshot.player.alienSpotting = out.spotting;
				else if (out.spotting > snapshot.player.spotting)
				{
					snapshot.player.spotting = out.spotting;
					snapshot.player.spotter = static_cast<int>(i);
				}
			}
			if (!out.detailed)
				continue;

			ReadCones(globals, out);
			if (!out.player)
			{
				out.haveHearing = ReadHearing(out.object, out.hearing);
				const uint8_t* enemy = nullptr;
				uint32_t enemyId = 0;
				if (ReadSenses(out.object, playerObject, playerId, out, enemy, enemyId))
					out.recordEnemy = IndexOf(snapshot, enemy, enemyId);
			}
			ReadPath(out.object, out.path);

			// Whether the player is in its view cones (the first four count for sight, as the game tests them), at the
			// point the game tests: a metre above the player's feet, a third of one when crouched
			if (out.haveHead && player && !out.player)
			{
				const float point[3] = { player->position[0], player->position[1] + (player->crouched ? 0.333f : 1.0f), player->position[2] };
				for (int c = 0; c < out.coneCount; c++)
				{
					AI_DEBUG::Cone& cone = out.cones[c];
					cone.playerInside = cone.valid && InCone(cone, out.headPosition, out.headForward, point);
					if (cone.playerInside && c < 4)
						out.playerInCone = true;
				}
			}
		}

		if (player)
			ReadPlayer(player->object, snapshot.player);
		for (size_t i = 0; i < snapshot.characters.size(); i++)
		{
			const AI_DEBUG::Character& character = snapshot.characters[i];
			if (!character.alien || character.dead || !character.read)
				continue;
			AI_DEBUG::Alien alien;
			alien.character = static_cast<int>(i);
			ReadAlien(globals, character.object, snapshot.now, alien);
			snapshot.aliens.push_back(alien);
		}
	}

	// The light meter's tint
	std::atomic<bool> g_tintOn = false;
	std::atomic<float> g_tintOpacity = 0.4f;
	bool g_tintPatched = false;
	uint8_t* g_tintSite = nullptr;
	uint8_t g_tintOriginal[5] = {};
	float* g_tintOpacityAt = nullptr;

	// What the game's ask now reaches (the same as the switch it asked: no arguments, the answer in al)
	bool __cdecl TintAsked()
	{
		return g_tintOn.load(std::memory_order_relaxed);
	}

	const uint8_t* CallTarget(const uint8_t* call)
	{
		return call + 5 + *reinterpret_cast<const int32_t*>(call + 1);
	}

	// Whether the hand-over is as expected: the ask is a call to a function that only answers "off", followed by the
	// test of its answer and the call for the opacity, which reads the global we write
	bool TintSiteExpected(const uint8_t* upload, const float* opacity)
	{
		__try
		{
			const uint8_t* ask = upload + kTintAskOn;
			const uint8_t* askOpacity = upload + kTintAskOpacity;
			if (ask[0] != 0xE8 || askOpacity[0] != 0xE8)
				return false;
			const uint8_t* off = CallTarget(ask);
			if (off[0] != 0x32 || off[1] != 0xC0 || off[2] != 0xC3)
				return false;
			if (ask[5] != 0x84 || ask[6] != 0xC0 || ask[7] != 0x74)
				return false;
			const uint8_t* getter = CallTarget(askOpacity);
			if (getter[0] != 0xD9 || getter[1] != 0x05 || getter[6] != 0xC3)
				return false;
			return *reinterpret_cast<const float* const*>(getter + 2) == opacity;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	void WriteCode(uint8_t* at, const uint8_t* bytes, size_t size)
	{
		DWORD oldProtect;
		if (!VirtualProtect(at, size, PAGE_EXECUTE_READWRITE, &oldProtect))
			return;
		memcpy(at, bytes, size);
		VirtualProtect(at, size, oldProtect, &oldProtect);
		FlushInstructionCache(GetCurrentProcess(), at, size);
	}

	void WriteOpacity()
	{
		__try
		{
			if (g_tintOpacityAt)
				*reinterpret_cast<volatile float*>(g_tintOpacityAt) = g_tintOpacity.load(std::memory_order_relaxed);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
		}
	}
}

const AI_DEBUG::Snapshot& AI_DEBUG::Read()
{
	const int frame = ImGui::GetFrameCount();
	if (g_snapshot.frame != frame)
	{
		g_snapshot.frame = frame;
		ReadSnapshot(g_snapshot);
	}
	return g_snapshot;
}

void AI_DEBUG::PatchLightTint(bool attach)
{
	if (!attach)
	{
		if (g_tintPatched)
			WriteCode(g_tintSite, g_tintOriginal, sizeof(g_tintOriginal));
		g_tintPatched = false;
		return;
	}
	if (g_tintPatched)
		return;
	uint8_t* upload = reinterpret_cast<uint8_t*>(DEVTOOLS_RELATIVE_ADDRESS(kTintUpload));
	float* opacity = reinterpret_cast<float*>(DEVTOOLS_RELATIVE_ADDRESS(kTintOpacity));
	if (!upload || !opacity || !TintSiteExpected(upload, opacity))
	{
		DevTools::Log("Debug menu: the light meter's screen tint is not where it was expected, so it is left off");
		return;
	}
	g_tintSite = upload + kTintAskOn;
	memcpy(g_tintOriginal, g_tintSite, sizeof(g_tintOriginal));
	uint8_t call[5] = { 0xE8 };
	const int32_t relative = static_cast<int32_t>(reinterpret_cast<uintptr_t>(&TintAsked) - (reinterpret_cast<uintptr_t>(g_tintSite) + sizeof(call)));
	memcpy(call + 1, &relative, sizeof(relative));
	g_tintOpacityAt = opacity;
	WriteOpacity();
	WriteCode(g_tintSite, call, sizeof(call));
	g_tintPatched = true;
}

bool AI_DEBUG::LightTintAvailable()
{
	return g_tintPatched;
}

void AI_DEBUG::SetLightTint(bool on, float opacity)
{
	g_tintOpacity = opacity;
	if (g_tintPatched)
		WriteOpacity();
	g_tintOn = on && g_tintPatched;
}

namespace
{
	const char* Named(const char* const* names, int count, int index)
	{
		return index >= 0 && index < count ? names[index] : "?";
	}
	template <size_t N> const char* Named(const char* const (&names)[N], int index)
	{
		return Named(names, static_cast<int>(N), index);
	}
}

const char* AI_DEBUG::BranchName(int branch)
{
	static const char* const kNames[] = {
		"None", "Cinematic", "Attack", "Aim", "Despawn", "Follow", "Standard", "Search", "Area sweep", "Backstage area sweep",
		"Shot", "Suspect target response", "Threat aware", "Backstage ambush", "Idle job", "Use cover", "Assault", "Melee",
		"Retreat", "Close on target", "Mutual melee only", "Vent melee", "Assault not allowed", "In vent", "Close combat",
		"Draw weapon", "Pursue target", "Ranged attack", "Ranged combat", "Primary control response", "Dead", "Script",
		"Idle", "Down but not out", "Melee block", "Aggressive", "Alert", "Shooting", "Grapple break",
		"React to weapon fire", "In cover", "Suspicious item (high)", "Suspicious item (medium)",
		"Suspicious item (low)", "Aggression escalation", "Stun damage", "Breakout", "Suspend", "Target is NPC",
		"Player hiding", "Attack core", "Corpse trap", "Observe target", "Target in crawlspace",
		"Suspicious item: at least move close", "Threat aware: attack within close range",
		"Threat aware: attack within very close range", "Threat aware: attack, flamed", "Threat aware: weapon not aimed",
		"Threat aware: move to lost visual", "Threat aware: move to, before anim", "Threat aware: move to, after anim",
		"Threat aware: move to flanked vent", "Killtrap", "Panic", "Backstage alien response", "NPC vs alien",
		"Use cover vs alien", "In cover vs alien", "Repeated pathfind fails", "All search variants",
	};
	return Named(kNames, branch);
}

const char* AI_DEBUG::SenseName(int sense)
{
	static const char* const kNames[] = { "Visual", "Weapon sound", "Movement sound", "Damage", "Touch", "Flamethrower", "Torch seen", "Combined" };
	return Named(kNames, sense);
}

const char* AI_DEBUG::ThresholdName(int threshold)
{
	static const char* const kNames[] = { "Trace", "Lower", "Activated", "Upper" };
	return Named(kNames, threshold);
}

const char* AI_DEBUG::HealthName(int state)
{
	static const char* const kNames[] = { "Full", "Damaged", "Injured", "Down but not out", "Dead" };
	return Named(kNames, state);
}

const char* AI_DEBUG::AwarenessName(int state)
{
	static const char* const kNames[] = { "Dead", "Stunned", "Unaware", "Suspicious", "Searching area", "Searching last sensed", "Aware" };
	return state < 0 ? "-" : Named(kNames, state);
}

const char* AI_DEBUG::AlertnessName(int state)
{
	static const char* const kNames[] = { "Ignore player", "Alert", "Aggressive" };
	return state < 0 ? "-" : Named(kNames, state);
}

const char* AI_DEBUG::AggroName(int level)
{
	static const char* const kNames[] = { "None", "Stand down", "Interrogative", "Warning", "Last chance", "Aggressive" };
	return level < 0 ? "-" : Named(kNames, level);
}

const char* AI_DEBUG::MoodName(int mood)
{
	static const char* const kNames[] = { "Neutral", "Scared", "Angry", "Happy", "Sad", "Suspicious", "Injured" };
	return mood < 0 ? "-" : Named(kNames, mood);
}

const char* AI_DEBUG::SenseSystemName(int system)
{
	static const char* const kNames[] = { "Set 1 normal", "Set 1 heightened", "Set 2", "Set 3" };
	return Named(kNames, system);
}

const char* AI_DEBUG::ViewconeSetName(int set)
{
	static const char* const kNames[] = { "None", "Standard", "Standard heightened", "Human", "Human heightened", "Sleeping", "Android", "Android heightened" };
	return Named(kNames, set);
}

const char* AI_DEBUG::TimerName(int timer)
{
	static const char* const kNames[] = {
		"Suspect target response delay", "Threat aware timeout", "Threat aware duration", "Search timeout",
		"Backstage stalk timeout", "Ambush timeout", "Attack ban", "Melee attack ban", "Vent ban",
		"Stay in cover and shoot", "Just left combat", "Keep chasing", "Delay return to spawn point",
		"Target in crawlspace", "Since search", "Heightened senses", "Backstage stalk: pick killtrap",
		"Flanked vent attack", "Threat aware visual retention", "Response to backstage alien timeout", "Vent attract",
		"Seen player aim weapon", "Search ban", "Observe target", "Repeated pathfind failures",
	};
	return Named(kNames, timer);
}

const char* AI_DEBUG::FlagName(int flag)
{
	static const char* const kNames[] = {
		"Done breakout", "Should reset", "Do assault attack checks", "In vent", "Banned from vent",
		"Has done grapple break", "Has received damage over time", "Sitting", "Done escalation job", "Should breakout",
		"Should attack", "Should hit and run", "Done hit and run", "Player hiding", "Attack hiding player",
		"Alien always knows when in vent", "Corpse trap on start", "Should despawn", "Attack got within routing threshold",
		"Lock backstage stalk", "Totally blind in dark", "Player won hiding QTE", "Android inert", "Android showroom dummy",
		"Should ambush", "Never aggressive", "Mute dynamic dialogue", "Doing threat aware anim", "Done threat aware",
		"Block ambush and killtraps", "Prevent grapples", "Prevent all attacks", "Allow flanked vent attack",
		"Ignore player in vent behaviour", "Aimed stance for idle jobs", "Aimed low stance for idle jobs",
		"Close to backstage alien", "In exploitable area", "On ladder", "Repeated pathfind failures",
	};
	return Named(kNames, flag);
}

const char* AI_DEBUG::PathStatusName(int status)
{
	static const char* const kNames[] = { "Not started", "Queued", "In progress", "Found", "Failed", "Removed", "Removed (done)", "Removed (new path)", "Removed (shortened)" };
	return Named(kNames, status);
}

const char* AI_DEBUG::FollowStatusName(int status)
{
	static const char* const kNames[] = { "Pending", "Failed", "Following", "Arrived" };
	return Named(kNames, status);
}

const char* AI_DEBUG::MovementName(int status)
{
	static const char* const kNames[] = { "Still", "Immovable", "On a path", "Starting a path", "Animation driven", "Animation driven (off the mesh)", "Stopping", "Along a direction", "Starting along a direction", "Stopping along a direction" };
	return Named(kNames, status);
}

const char* AI_DEBUG::SpeedName(int speed)
{
	static const char* const kNames[] = { "Slowest", "Slow", "Fast", "Fastest" };
	return Named(kNames, speed);
}

const char* AI_DEBUG::LightLevelName(int level)
{
	static const char* const kNames[] = { "DARK", "PARTIALLY LIT", "FULLY LIT" };
	return Named(kNames, level);
}

const char* AI_DEBUG::WithdrawName(int state)
{
	static const char* const kNames[] = { "Not withdrawing", "Needs to withdraw", "Withdrawing" };
	return state < 0 ? "-" : Named(kNames, state);
}

const char* AI_DEBUG::AmbushName(int decision)
{
	static const char* const kNames[] = { "Waiting", "Ambush", "Killtrap", "Springing killtrap", "Aborting killtrap" };
	return decision < 0 ? "-" : Named(kNames, decision);
}

const char* AI_DEBUG::SweepTypeName(int type)
{
	static const char* const kNames[] = { "In and out between the player and the point", "Fixed radius round the point", "Round the player" };
	return Named(kNames, type);
}

const char* AI_DEBUG::GaugeLevelName(int level)
{
	static const char* const kNames[] = { "None", "Trace", "Lower", "Activated", "Upper", "Full" };
	return Named(kNames, level);
}

const char* AI_DEBUG::ThreatName(int status)
{
	static const char* const kNames[] = { "Unknown", "Known", "Forgotten" };
	return Named(kNames, status);
}

const char* AI_DEBUG::SensedName(int state)
{
	static const char* const kNames[] = { "Not sensed", "Nearly", "Rising", "Detected" };
	return Named(kNames, state);
}

const char* AI_DEBUG::AlienAwarenessName(int state)
{
	static const char* const kNames[] = { "Unaware", "Creep", "Stalk", "Charge" };
	return Named(kNames, state);
}

#pragma once

#include "DevTools.h"
#include <cstdint>
#include <vector>

/*
	The debug menu's views of the characters (see DEBUG_MENU.h): what each character the game runs knows and is doing,
	read from the game once a frame, shown in three tabs and drawn over the game.

	- Characters: a table of the level's characters (name, class, how far from the camera, health, how aware of the
	  player it is - or, for the alien, how close it is to menacing - how alert and aggressive, its mood and what its
	  behaviour tree is doing); a click selects one, and the selected one is shown in detail: who it is (with the ids
	  OpenCAGE could find its entity by), its state, its target, its eight sense meters against their four thresholds,
	  where each sense last placed its enemy and how long ago, its flags and running timers, its view cones, hearing and
	  path. Switches draw over the game, for the selected character or for all of them: a label over each head, the view
	  cones, the hearing range, the path, and the last known positions.
	- Player: the stealth light meter (how lit the player is, from three samples of the light around them), the other
	  stealth factors, whether they are hiding, and how close anyone is to spotting them; and the game's own screen
	  tint for the light meter, which the game never turns on.
	- Alien: its menace gauge, whether it is menacing or withdrawing, how many times it has menaced the player, its
	  setup, its ambush and killtrap decision, the area it sweeps and where it heads in the vents.

	Everything is read on the render thread as the frame is presented, after the game has finished updating its
	characters for the frame and before it starts on the next (see DEBUG_MENU.h), with every read guarded - a level can
	be unloading. Only plain reads are made of the game, and the only game code called is that which only reads: the
	thresholds of a sense, how far a character hears, how many times the alien may menace and the area it sweeps. Full
	detail (senses, cones, path) is read for the selected character and those near the camera when there are many.

	Addresses and layouts are the Steam retail AI.exe's (see GameAddresses.h for the GOG and Epic Games Store builds).
*/
namespace AI_DEBUG
{
	constexpr int kSenses = 8;       // visual, weapon sound, movement sound, damage, touch, flamethrower, torch, combined
	constexpr int kThresholds = 4;   // trace, lower, activated, upper
	constexpr int kCombinedSense = 7;
	constexpr int kActivated = 2;
	constexpr int kMaxCones = 8;
	constexpr int kMaxPassed = 24;
	constexpr int kMaxPath = 96;
	constexpr int kMaxCorridor = 48;
	constexpr int kMaxCorners = 16;
	constexpr int kTimers = 25;
	constexpr int kFlags = 40;
	constexpr int kGaugeLevels = 6;  // none, trace, lower, activated, upper, full

	struct Sense
	{
		float level = 0.0f;                         // the meter, 0..1
		float thresholds[kThresholds] = {};         // the levels it is measured against
		float lastPosition[kThresholds][3] = {};    // where the sense last placed its enemy at or above each threshold
		float lastTime[kThresholds] = {};           // when (game time; 0 when never)
		bool triggered = false;
	};

	struct Cone
	{
		float length = 0.0f;
		float horizontalDot = 1.0f;  // the cosine of half its width
		float verticalDot = 1.0f;    // the cosine of half its height
		char typeName[40] = {};
		bool valid = false;
		bool playerInside = false;   // the player's chest (or hips, crouched) is in it
	};

	struct Timer
	{
		int type = 0;
		float remaining = 0.0f;
		float duration = 0.0f;
		bool paused = false;
	};

	struct Path
	{
		bool have = false;
		int status = 0;              // how the character moves: still, on a path, along a direction...
		int speedNow = 0, speedWanted = 0;
		float stuckFor = 0.0f;       // seconds without getting anywhere
		bool fullRepath = false;
		int passedCount = 0;
		float passed[kMaxPassed][3] = {};
		int nodeCount = 0;           // the path's nodes, the current one at currentNode (-1: none)
		int currentNode = -1;
		float nodes[kMaxPath][3] = {};
		int nodeTypes[kMaxPath] = {};
		int totalNodes = 0;          // before kMaxPath cut it short
		int requestCount = 0;
		bool haveRequest = false;
		float destination[3] = {};
		float remaining = 0.0f;
		int pathStatus = 0, followStatus = 0;
		bool destinationBackstage = false;
		float requestedEnd[3] = {};
		int portalCount = 0;         // the corridor: the gaps it passes through, then the corners it steers by
		float portals[kMaxCorridor][2][3] = {};
		int cornerCount = 0;
		float corners[kMaxCorners][3] = {};
	};

	struct Character
	{
		// From the debug menu's list of this frame's characters
		const uint8_t* object = nullptr;
		uint32_t id = 0;
		int characterClass = -1;
		bool player = false, dead = false, alien = false, selected = false;
		float position[3] = {};
		float facing = 0.0f;
		char name[65] = {};
		float distance = 0.0f;       // from the camera
		bool detailed = false;       // the senses, cones and path were read
		bool read = false;           // the state below was read

		// Who it is: the ids of its entity (the entity, and the composite instance it is in)
		uint32_t entityId = 0, compositeInstanceId = 0;
		char treeName[101] = {};

		// Its state
		int healthState = 0, health = 0, maxHealth = 0;
		int awareness = -1;          // of the player, as the group of NPCs keeps it (-1: not in it, as the alien isn't)
		int alertness = -1, aggro = -1, mood = -1;
		int branch = -1, previousBranch = -1;
		int alienAwareness = 0;
		int sensed = 0;              // how hard this character is sensed by its enemies
		bool backstage = false, backstageTransition = false, inVent = false, crouched = false, torch = false;
		bool stunned = false, invisible = false;
		bool flags[kFlags] = {};
		int timerCount = 0;
		Timer timers[kTimers];
		float hidingSince = -1.0f;   // the player: when they started hiding

		// Its target (an index in the snapshot's characters, -1: none or not one of them)
		int target = -1;
		bool haveTarget = false;

		// Its senses: the set of senses in use, its view cones, where its head is and looks, how far it hears
		int senseSystem = 0, viewconeSet = 0;
		int coneCount = 0;
		Cone cones[kMaxCones];
		bool haveHead = false;
		float headPosition[3] = {}, headForward[3] = {};
		float hearing = 0.0f;
		bool haveHearing = false;
		bool playerInCone = false;

		// Its memory of one enemy - its target, or else the player: the eight meters and where they last placed it
		int recordCount = 0;
		bool haveRecord = false;
		int recordEnemy = -1;        // an index in the snapshot's characters
		bool recordIsTarget = false;
		float recordDistance = 0.0f;
		float hitsFraction = 0.0f;   // how much of the enemy its last line-of-sight checks saw
		int threatStatus = 0;
		Sense senses[kSenses];

		// How close it is to spotting the player: its combined meter on the player against its activated threshold
		float spotting = -1.0f;

		Path path;
	};

	struct Alien
	{
		int character = -1;          // an index in the snapshot's characters
		bool read = false;
		// The menace gauge: how full, its thresholds and the level it is at
		bool haveGauge = false;
		float gauge = 0.0f;
		float gaugeThresholds[kGaugeLevels] = {};
		int gaugeLevel = 0;
		bool menacing = false, paused = false, seenInCamera = false, withdrawAfterNextJob = false;
		int menaces = 0, maxMenaces = -1;
		float menaceTimerRemaining = 0.0f, menaceTimerDuration = 0.0f;
		// Withdrawal, ambushes and killtraps
		int withdrawState = -1;
		bool ambushing = false, scriptKilltraps = false, treeKilltraps = false;
		int ambushDecision = -1;
		char configName[65] = {};
		// The area it sweeps: where, how, and the box the game works out from it
		bool haveSweep = false;
		float sweepPosition[3] = {};
		int sweepType = 0;
		float sweepRadius = 0.0f;
		bool haveBox = false;
		float boxCentre[3] = {};
		float boxHalfWidth = 0.0f, boxHalfLength = 0.0f, boxAngle = 0.0f;
		// Where it heads in the vents
		bool haveBackstageTarget = false;
		float backstageTarget[3] = {};
		int ventCount = 0;
		bool haveVentNearAlien = false, haveVentNearPlayer = false;
		float ventNearAlien[3] = {}, ventNearPlayer[3] = {};
	};

	struct Player
	{
		int character = -1;
		bool haveLightMeter = false;
		float samplePositions[3][3] = {};
		float samples[3][3] = {};    // the light found at each, red, green, blue
		float combined = 0.0f;       // their average brightness
		float scaled = 0.0f;         // that through the AI's light curve: what the level is set from
		int level = 0;               // dark, partially lit, fully lit
		bool meterEnabled = false;   // the game turns it off for a while once the player is detected
		bool haveStealth = false;
		float exposure = 0.0f, stance = 0.0f, cover = 0.0f, movement = 0.0f;
		bool hiding = false;
		float spotting = -1.0f;      // the most any NPC is to spotting them (the alien not counted, as the game counts)
		int spotter = -1;            // who
		float alienSpotting = -1.0f; // the alien's own
	};

	struct Snapshot
	{
		int frame = -1;
		bool level = false;
		float now = 0.0f;            // game time
		float darkThreshold = 0.0f, lightThreshold = 0.0f;           // the light meter's, on the scaled brightness
		float tintDarkThreshold = 0.0f, tintLightThreshold = 0.0f;   // the same on the plain brightness, for the tint
		std::vector<Character> characters;
		std::vector<Alien> aliens;
		Player player;
	};

	// Render thread: this frame's snapshot (read on the first call each frame).
	const Snapshot& Read();

	// The game's screen tint for the light meter: the game asks whether it is on each frame, from a switch
	// it always answers "off"; that one ask is pointed at ours (patched when the hooks attach, put back when they detach).
	void PatchLightTint(bool attach);
	bool LightTintAvailable();
	void SetLightTint(bool on, float opacity);

	// Names to show for the game's numbers
	const char* BranchName(int branch);
	const char* SenseName(int sense);
	const char* ThresholdName(int threshold);
	const char* HealthName(int state);
	const char* AwarenessName(int state);
	const char* AlertnessName(int state);
	const char* AggroName(int level);
	const char* MoodName(int mood);
	const char* SenseSystemName(int system);
	const char* ViewconeSetName(int set);
	const char* TimerName(int timer);
	const char* FlagName(int flag);
	const char* PathStatusName(int status);
	const char* FollowStatusName(int status);
	const char* MovementName(int status);
	const char* SpeedName(int speed);
	const char* LightLevelName(int level);
	const char* WithdrawName(int state);
	const char* AmbushName(int decision);
	const char* SweepTypeName(int type);
	const char* GaugeLevelName(int level);
	const char* ThreatName(int status);
	const char* SensedName(int state);
	const char* AlienAwarenessName(int state);
}

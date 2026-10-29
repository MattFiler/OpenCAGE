#pragma once

#include "DevTools.h"
#include <cstdint>
#include <string>
#include <vector>

/*
	A character's own state beyond what AI_DEBUG reads (see AI_DEBUG.h): the debug menu's State and Control tabs
	(Characters group), and what the AI group's State Log and its breadcrumbs draw on.

	- Its orders: the command a script gave it and the one its own tasks gave it (each the one running and the one queued
	  behind it, with its name, kind and how far it has got), the moves, turns and the like asked of its body and how each
	  went, the layered states its body is in (the high-level one, moving, cover, dying) and the way it idles.
	- What it is doing with its body: the action it is in and the secondary actions layered on it (breathing, looking,
	  aiming...).
	- Its 105 state flags, as the game keeps them in three sets (only for the game's logic, shared with the animation, and
	  independent), with its stress, alliance and the owner bits of its "takes no damage", "doesn't react", "very important"
	  and "untargetable" switches.
	- What it may do (its 35 motivations: each lets a family of its behaviour tree's branches run), its mood on each of the
	  four layers moods are pushed on, its health and its two gauges (stunned, retreating).

	Everything here is read on the render thread as the frame is presented (see DEBUG_MENU.h), every read guarded. What
	changes a character is done by the callers on the AI thread, through the game's own functions; the helpers for that
	(finding a character again by its unique id, the game calls) are here too.

	The state-change log: the game still makes 29 calls, from the state machines of the characters' bodies (moving, cover,
	dying, the weapon, shooting), that hand a log line - which machine, the state it was in or wanted, the one it changes to
	and a third (the state it is in, for a change of what it wants; what it wants, for a change of state) - to the
	game's logger, which is a bare return. Each of those calls is pointed at our own logger
	while the hooks are attached (found by their code, so the same on every build of the game), which keeps the lines while
	switched on.

	Addresses and layouts are the Steam retail AI.exe's (see GameAddresses.h for the GOG and Epic Games Store builds).
*/
namespace AI_STATE
{
	constexpr int kMotivations = 35;
	constexpr int kStateFlags = 105;
	constexpr int kSecondaryLayers = 6;
	constexpr int kMaxRequests = 8;
	constexpr int kMoodLayers = 4;
	constexpr int kGauges = 2;
	constexpr int kForceIdleMotivation = 33; // the "force idle" debug motivation, as a number (1..35)

	struct Command
	{
		bool have = false;
		char name[33] = {};          // the script entity's name for a command a script gave, else empty
		int type = -1;
		int status = -1;
		bool fromScript = false, persistent = false, task = false;
		uint32_t entity = 0, instance = 0;   // a script's command: its own entity and the composite instance it is in
	};

	struct Applicator
	{
		Command current, next;
		bool queued = false, suspended = false;
	};

	struct Request
	{
		int type = 0, status = 0;
		bool haveDestination = false;
		float destination[3] = {};
	};

	struct Gauge
	{
		bool have = false;
		float amount = 0.0f;
		float thresholds[6] = {};
		int level = 0;
		bool paused = false;
	};

	struct State
	{
		bool read = false;
		uint32_t id = 0;

		// Orders
		Applicator scripted, task;
		bool underPlayerControl = false;
		int highLevel = -1, highLevelPending = -1;
		int general = -1, generalPending = -1;
		int cover = -1, coverPending = -1;
		int dead = -1, deadPending = -1;
		float deadTime = 0.0f;
		int scriptedIdle = -1, currentIdle = -1;
		float idleJobMeter = 0.0f, idleJobMeterMax = 0.0f;
		int requestCount = 0, requestTotal = 0, finishedCount = 0;
		Request requests[kMaxRequests];

		// The body
		int action = -1, previousAction = -1;
		bool actionStarted = false, actionFinished = false, actionInterruptable = false;
		int secondary[kSecondaryLayers];   // the secondary action on each layer (-1: none)

		// Flags
		bool flags[kStateFlags] = {};
		int stress = 0, maxStress = 0;
		int alliance = -1, allianceAtSpawn = -1, traversal = -1;
		uint8_t noDamage = 0, noReact = 0, important = 0, untargetable = 0;

		// What it may do, its mood, health and gauges
		bool haveMotivations = false;
		bool motivations[kMotivations] = {};
		bool haveMood = false;
		int moodLayer[kMoodLayers] = {};
		float moodTime[kMoodLayers] = {};
		int mood = -1;
		int healthState = 0, health = 0, maxHealth = 0;
		float healthModifier = 1.0f;
		Gauge gauges[kGauges];

		State() { for (int& layer : secondary) layer = -1; }
	};

	// Render thread: a character's state (the object from DEBUG_MENU::Characters(), read this frame)
	bool Read(const uint8_t* character, State& out);

	// The flags, as the menu names them
	enum FlagSet { kLogic, kShared, kIndependent };
	struct FlagInfo
	{
		FlagSet set;
		const char* key;     // the name a command reports it by
		const char* label;
		uint32_t offset;     // in the character
	};
	const FlagInfo& Flag(int index);
	const char* FlagSetName(FlagSet set);

	// Names to show for the game's numbers
	const char* CommandTypeName(int type);
	const char* CommandStatusName(int status);
	const char* RequestTypeName(int type);
	const char* RequestStatusName(int status);
	const char* HighLevelName(int state);
	const char* GeneralName(int state);
	const char* CoverName(int state);
	const char* DeadName(int state);
	const char* IdleStyleName(int style);
	const char* ActionName(int action);
	const char* SecondaryActionName(int action);
	const char* SecondaryLayerName(int layer);
	const char* MotivationName(int motivation);   // 1..35
	const char* MotivationKey(int motivation);    // 1..35, the name a command takes
	int MotivationFromKey(const std::string& key); // 0: none of that name
	const char* AllianceName(int alliance);
	const char* TraversalName(int traversal);
	const char* GaugeName(int gauge);
	const char* MoodKey(int mood);                // the name a command takes ("neutral"...)
	int MoodFromKey(const std::string& key);      // -1: none of that name
	// The parts of the game that set one of its four switches (no damage, no reaction, very important, untargetable): "no",
	// or "yes (a script, a cutscene...)"
	std::string SwitchOwners(uint8_t bits);

	// Any thread, guarded: the character the game runs with this unique id (null when it is gone). Use it on the thread the
	// work runs on (the AI thread for a change), not from one frame to the next.
	uint8_t* FindCharacter(uint32_t id);
	// The same for the render thread's own list (DEBUG_MENU::Characters()), and the selected character's id when id is 0
	const uint8_t* FrameCharacter(uint32_t id);

	// AI thread: the game's own changes. False when the character has no part for it or the call faulted.
	bool ApplyMotivation(uint8_t* character, int motivation, int change); // change: 0 allow, 1 forbid, 2 back to its defaults
	bool PushMood(uint8_t* character, int layer, int mood, float time);
	bool SetHealth(uint8_t* character, int health);                        // clamped 1..its most
	bool Damage(uint8_t* character, int amount);                           // it reacts as to a hit
	bool Burn(uint8_t* character);
	bool Kill(uint8_t* character);

	// Entity thread: calls the generic "despawn" or "spawn" method on a character's script entity (given by the two ids its
	// handle holds). The message says what happened.
	bool CallSpawnMethod(uint32_t entity, uint32_t instance, bool spawn, std::string& message);

	// Entity thread: how far the game has got with a character's script entity - whether the entity is active and
	// spawned, and the entity's own stage in making its character (a spawn asked for goes: waiting for a free character,
	// starting, loading its assets, in the level). False when the entity is not in the running level.
	struct SpawnStage
	{
		bool active = false, spawned = false, updating = false;
		int stage = -1;          // -1: not known (the entity isn't a Character entity)
	};
	bool ReadSpawnStage(uint32_t entity, uint32_t instance, SpawnStage& out);
	const char* SpawnStageName(int stage);
	constexpr int kStageInLevel = 3;

	// ---- The state-change log ----

	struct LogEntry
	{
		uint32_t sequence = 0;       // counts up from 1, for following the log
		float time = 0.0f;           // game time
		uint32_t id = 0;             // the character's unique id
		char character[33] = {};
		char machine[12] = {};
		char from[40] = {}, to[40] = {}, third[40] = {};
	};

	// Patches (attach) or puts back (detach) the 29 calls; called from the debug menu's hooks.
	void PatchStateLog(bool attach);
	int StateLogSites();             // how many calls were found and pointed at the logger (0: none, or not attached)
	void SetStateLogging(bool on);
	bool StateLogging();
	// The lines kept (the most recent last), and those after a sequence number
	std::vector<LogEntry> StateLog();
	std::vector<LogEntry> StateLogSince(uint32_t sequence);
	void ClearStateLog();
}

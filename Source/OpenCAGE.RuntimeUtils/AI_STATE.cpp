#include "AI_STATE.h"
#include "DEBUG_MENU.h"
#include "LIVE_LINK.h"

#include <algorithm>
#include <atomic>
#include <cctype>
#include <cstring>
#include <mutex>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

/*
	Layouts and addresses are the Steam retail AI.exe's (see GameAddresses.h for the GOG and Epic Games Store builds). Each
	structure is found from a pointer the one before it holds, so only the globals and the functions called have addresses of
	their own. Each game function called here is called as noted where it is declared.
*/
namespace
{
	// Where the game keeps its pointer to its globals; the byte after that pointer is set while its characters can be read
	constexpr uintptr_t kGameGlobals = 0x012F0C88;
	constexpr uint32_t kCharactersValid = 0x04;
	// In the globals: the character manager, whose lists of characters are a pointer to their pointers then how many - the
	// players, those alive that aren't players, and the dead
	constexpr uint32_t kGlobalsCharacters = 0x08;
	constexpr uint32_t kPlayers = 0x80;
	constexpr uint32_t kAlive = 0xD0;
	constexpr uint32_t kDead = 0x120;
	constexpr uint32_t kMaxPerList = 18;
	// The game's clock: the time now (a double, seconds)
	constexpr uintptr_t kGameTime = 0x012410A8;
	// The entity manager (a pointer to it)
	constexpr uintptr_t kEntityManager = 0x0134EF40;

	// A character's fields
	constexpr uint32_t kCharacterName = 0x10;          // its debug name (64 characters)
	constexpr uint32_t kCharacterId = 0x50;            // its unique id
	constexpr uint32_t kCharacterTraversal = 0x2E0;    // the traversal it is on
	constexpr uint32_t kCharacterAlliance = 0x2E4;
	constexpr uint32_t kCharacterNoDamage = 0x2E8;     // four bytes of owner bits: no damage, no reaction to hits, very
	constexpr uint32_t kCharacterNoReact = 0x2E9;      // important, untargetable (a bit for each part of the game that set it)
	constexpr uint32_t kCharacterImportant = 0x2EA;
	constexpr uint32_t kCharacterUntargetable = 0x2EB;
	constexpr uint32_t kCharacterStress = 0x2A0;       // its stress and most stress (16 bits each)
	constexpr uint32_t kCharacterMaxStress = 0x2A2;
	constexpr uint32_t kCharacterAllianceAtSpawn = 0x310;
	constexpr uint32_t kCharacterHealthState = 0x320;  // its health: the state (full, damaged...) at +0xC, health and most
	constexpr uint32_t kHealthState = 0x0C;            // at +0x1C and +0x1E (16 bits), and what the most is scaled by at +0x20
	constexpr uint32_t kHealthNow = 0x1C;
	constexpr uint32_t kHealthMost = 0x1E;
	constexpr uint32_t kHealthModifier = 0x20;
	constexpr uint32_t kCharacterAction = 0x36C;       // what it is doing (a pointer): its type at +0x1A, the one before at +0x1B
	constexpr uint32_t kActionBits = 0x08;             // started 0x08, finished 0x04, interruptable 0x02
	constexpr uint32_t kActionType = 0x1A;
	constexpr uint32_t kActionPrevious = 0x1B;
	constexpr uint32_t kCharacterSecondary = 0x370;    // its secondary actions: six layers of two (pointers: the networked one,
	constexpr uint32_t kSecondaryType = 0x1E;          // then the one kept on this machine only), the type of each at +0x1E
	                                                   // (16 bits). The game finds layer L's at (L * 2 + which) * 4.
	constexpr uint32_t kCharacterIdleMeter = 0x2CB8;   // the idle job meter and its most
	constexpr uint32_t kCharacterIdleMeterMax = 0x2CBC;
	// The orders given to its body (inside the character): the requests asked of it (a list), those finished (a list), the
	// state it is in and the one it is going to, the moving machine's state and the one it is going to, the cover machine's,
	// the dying machine's (and how long since it died), and the way it idles (as scripted, and now)
	constexpr uint32_t kHighLevel = 0x2CD0;
	constexpr uint32_t kHighRequests = kHighLevel + 0x04;
	constexpr uint32_t kHighFinished = kHighLevel + 0x10;
	constexpr uint32_t kHighState = kHighLevel + 0x20;
	constexpr uint32_t kHighPending = kHighLevel + 0xE4;
	constexpr uint32_t kGeneralState = kHighLevel + 0xF4;
	constexpr uint32_t kGeneralPending = kHighLevel + 0x1C8;
	constexpr uint32_t kCoverState = kHighLevel + 0x224;
	constexpr uint32_t kCoverPending = kHighLevel + 0x298;
	constexpr uint32_t kDeadState = kHighLevel + 0x2C0;
	constexpr uint32_t kDeadPending = kHighLevel + 0x314;
	constexpr uint32_t kDeadTime = kHighLevel + 0x318;
	constexpr uint32_t kScriptedIdle = kHighLevel + 0x31C;
	constexpr uint32_t kCurrentIdle = kHighLevel + 0x320;
	// A request: how it went, what it is, and where to (for a move)
	constexpr uint32_t kRequestStatus = 0x0C;
	constexpr uint32_t kRequestType = 0x10;
	constexpr uint32_t kRequestDestination = 0x1C;
	constexpr int kRequestMoveTo = 1;
	// The commands (inside the character): the one a script gave and the one its tasks gave, each the one running and the one
	// after it (pointers), whether that one is queued and whether it is suspended; then whether a player controls it
	constexpr uint32_t kScriptedCommands = 0x3570;
	constexpr uint32_t kTaskCommands = 0x3820;
	constexpr uint32_t kCommandCurrent = 0x00;
	constexpr uint32_t kCommandNext = 0x04;
	constexpr uint32_t kCommandQueued = 0x08;
	constexpr uint32_t kCommandSuspended = 0x09;
	constexpr uint32_t kUnderPlayerControl = 0x3AD5;
	// A command: its name, kind, how far it has got, whether it lasts, came from a script, is a task, and (from a script) its
	// own entity
	constexpr uint32_t kCommandName = 0x04;
	constexpr uint32_t kCommandType = 0x28;
	constexpr uint32_t kCommandStatus = 0x2C;
	constexpr uint32_t kCommandPersistent = 0x30;
	constexpr uint32_t kCommandFromScript = 0x31;
	constexpr uint32_t kCommandTask = 0x32;
	constexpr uint32_t kCommandEntity = 0x3C;
	// Its motivations (a pointer): 35 bools from +0xC; its moods (a pointer): the mood on each of four layers, the time left on
	// each, then the mood shown; its two gauges (pointers): the six thresholds, how full, the level reached, whether paused
	constexpr uint32_t kCharacterMotivations = 0x3498;
	constexpr uint32_t kMotivationFirst = 0x0C;
	constexpr uint32_t kCharacterMood = 0x34BC;
	constexpr uint32_t kMoodLayers = 0x00;
	constexpr uint32_t kMoodTimes = 0x10;
	constexpr uint32_t kMoodActive = 0x20;
	constexpr uint32_t kCharacterGauges = 0x4234;
	constexpr uint32_t kGaugeThresholds = 0x04;
	constexpr uint32_t kGaugeAmount = 0x68;
	constexpr uint32_t kGaugeLevel = 0x74;
	constexpr uint32_t kGaugePaused = 0x79;

	// The game's functions, called as noted
	// - a character's motivations changed (allow 0, forbid 1, back to defaults 2)
	typedef void(__thiscall* t_apply_motivation)(void* motivations, int motivation, int change);
	const auto apply_motivation = reinterpret_cast<t_apply_motivation>(DEVTOOLS_RELATIVE_ADDRESS(0x00323C90));
	// - a mood pushed on a layer for a time (below -0.5: for ever), as the game's dialogue and scripts push them
	typedef void(__thiscall* t_push_mood)(void* moods, int layer, int mood, float time);
	const auto push_mood = reinterpret_cast<t_push_mood>(DEVTOOLS_RELATIVE_ADDRESS(0x00007A30));
	// - a character's health set outright
	typedef void(__thiscall* t_force_set_health)(void* health, uint16_t value);
	const auto force_set_health = reinterpret_cast<t_force_set_health>(DEVTOOLS_RELATIVE_ADDRESS(0x003B4B50));
	// - damage dealt to a character (by itself, the kind of damage 0x16)
	typedef void(__thiscall* t_take_damage)(void* character, int amount, const void* attacker, bool a, bool b, bool c, bool d, uint32_t kind, bool e, bool f);
	const auto take_damage = reinterpret_cast<t_take_damage>(DEVTOOLS_RELATIVE_ADDRESS(0x003CC900));
	constexpr uint32_t kDamageKind = 0x16;
	// - a character set on fire (for 5 s)
	typedef void(__thiscall* t_burn)(void* health, float seconds, uint32_t unused, void* attacker);
	const auto burn = reinterpret_cast<t_burn>(DEVTOOLS_RELATIVE_ADDRESS(0x00390CA0));
	// - a character killed outright, as the game's own death actions do it (no killer)
	typedef void(__thiscall* t_force_kill)(void* character, const void* killer);
	const auto force_kill = reinterpret_cast<t_force_kill>(DEVTOOLS_RELATIVE_ADDRESS(0x003D0C10));

	// Calling a method on a script entity, as LIVE_LINK does it: the entity manager's access lock, its lookup of an entity
	// by its handle (a counted pointer), the queued method call and the release of the counted pointer
	struct Allocation
	{
		void** vtable;
		volatile LONG references;
		uint32_t count;
		void* data;
	};
	typedef void(__thiscall* t_access)(void* access);
	const auto access_lock = reinterpret_cast<t_access>(DEVTOOLS_RELATIVE_ADDRESS(0x0051E1B0));
	const auto access_unlock = reinterpret_cast<t_access>(DEVTOOLS_RELATIVE_ADDRESS(0x0051E1D0));
	typedef Allocation** (__thiscall* t_find_entity)(void* manager, Allocation** result, const uint32_t* handle);
	const auto find_entity = reinterpret_cast<t_find_entity>(DEVTOOLS_RELATIVE_ADDRESS(0x005236A0));
	typedef void(__thiscall* t_call_custom_method)(void* temporaryEntity, Allocation** entity, const uint32_t* method, uint32_t flags);
	const auto call_custom_method = reinterpret_cast<t_call_custom_method>(DEVTOOLS_RELATIVE_ADDRESS(0x005425A0));
	constexpr uint32_t kTemporaryEntityTarget = 0x24;
	typedef void(__thiscall* t_release)(Allocation** pointer);
	const auto release_entity = reinterpret_cast<t_release>(DEVTOOLS_RELATIVE_ADDRESS(0x00198230));
	// The generic method names, as the game hashes them ("despawn" and "spawn")
	constexpr uint32_t kMethodDespawn = 0x06DE470C;
	constexpr uint32_t kMethodSpawn = 0x38BD237A;

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

	// A fixed-size name, copied as far as its end or the first character that isn't printable
	void CopyName(char* out, size_t size, const uint8_t* from, size_t max)
	{
		size_t i = 0;
		for (; i + 1 < size && i < max; i++)
		{
			const char c = static_cast<char>(from[i]);
			if (c == '\0' || static_cast<unsigned char>(c) < 0x20 || static_cast<unsigned char>(c) > 0x7E)
				break;
			out[i] = c;
		}
		out[i] = '\0';
	}

	void ReadCommand(const uint8_t* command, AI_STATE::Command& out)
	{
		out = AI_STATE::Command();
		if (!command)
			return;
		out.have = true;
		out.type = At<int>(command, kCommandType);
		out.status = At<int>(command, kCommandStatus);
		out.persistent = At<uint8_t>(command, kCommandPersistent) != 0;
		out.fromScript = At<uint8_t>(command, kCommandFromScript) != 0;
		out.task = At<uint8_t>(command, kCommandTask) != 0;
		CopyName(out.name, sizeof(out.name), command + kCommandName, 0x20);
		if (out.fromScript)
		{
			out.entity = At<uint32_t>(command, kCommandEntity);
			out.instance = At<uint32_t>(command, kCommandEntity + 4);
		}
	}

	void ReadApplicator(const uint8_t* applicator, AI_STATE::Applicator& out)
	{
		ReadCommand(Pointer(applicator, kCommandCurrent), out.current);
		ReadCommand(Pointer(applicator, kCommandNext), out.next);
		out.queued = At<uint8_t>(applicator, kCommandQueued) != 0;
		out.suspended = At<uint8_t>(applicator, kCommandSuspended) != 0;
	}

	bool ReadOrders(const uint8_t* character, AI_STATE::State& out)
	{
		__try
		{
			ReadApplicator(character + kScriptedCommands, out.scripted);
			ReadApplicator(character + kTaskCommands, out.task);
			out.underPlayerControl = At<uint8_t>(character, kUnderPlayerControl) != 0;
			out.highLevel = At<int>(character, kHighState);
			out.highLevelPending = At<int>(character, kHighPending);
			out.general = At<int>(character, kGeneralState);
			out.generalPending = At<int>(character, kGeneralPending);
			out.cover = At<int>(character, kCoverState);
			out.coverPending = At<int>(character, kCoverPending);
			out.dead = At<int>(character, kDeadState);
			out.deadPending = At<int>(character, kDeadPending);
			out.deadTime = At<float>(character, kDeadTime);
			out.scriptedIdle = At<int>(character, kScriptedIdle);
			out.currentIdle = At<int>(character, kCurrentIdle);
			out.idleJobMeter = At<float>(character, kCharacterIdleMeter);
			out.idleJobMeterMax = At<float>(character, kCharacterIdleMeterMax);

			// The requests: a list (its end node, whose next is the first, then how many); each node holds the next, the one
			// before, then the request (a counted pointer, the object first)
			out.requestCount = 0;
			out.requestTotal = At<int>(character, kHighRequests + 4);
			out.finishedCount = At<int>(character, kHighFinished + 4);
			const uint8_t* end = Pointer(character, kHighRequests);
			const uint8_t* node = end ? Pointer(end, 0) : nullptr;
			for (int seen = 0; node && node != end && seen < 64; seen++, node = Pointer(node, 0))
			{
				const uint8_t* request = Pointer(node, 0x08);
				if (!request || out.requestCount >= AI_STATE::kMaxRequests)
					continue;
				AI_STATE::Request& shown = out.requests[out.requestCount++];
				shown.type = At<int>(request, kRequestType);
				shown.status = At<int>(request, kRequestStatus);
				shown.haveDestination = shown.type == kRequestMoveTo;
				if (shown.haveDestination)
					Copy3(shown.destination, request + kRequestDestination);
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool ReadBody(const uint8_t* character, AI_STATE::State& out)
	{
		__try
		{
			const uint8_t* action = Pointer(character, kCharacterAction);
			if (action)
			{
				out.action = At<uint8_t>(action, kActionType);
				out.previousAction = At<uint8_t>(action, kActionPrevious);
				const uint8_t bits = At<uint8_t>(action, kActionBits);
				out.actionStarted = (bits & 0x08) != 0;
				out.actionFinished = (bits & 0x04) != 0;
				out.actionInterruptable = (bits & 0x02) != 0;
			}
			for (int layer = 0; layer < AI_STATE::kSecondaryLayers; layer++)
			{
				out.secondary[layer] = -1;
				// The networked one first, then the one kept on this machine only (the two of a layer side by side)
				for (int row = 0; row < 2 && out.secondary[layer] < 0; row++)
				{
					const uint8_t* secondary = Pointer(character, kCharacterSecondary + (layer * 2 + row) * 4);
					if (secondary)
						out.secondary[layer] = At<uint16_t>(secondary, kSecondaryType);
				}
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool ReadFlags(const uint8_t* character, AI_STATE::State& out)
	{
		__try
		{
			for (int i = 0; i < AI_STATE::kStateFlags; i++)
				out.flags[i] = At<uint8_t>(character, AI_STATE::Flag(i).offset) != 0;
			out.stress = At<int16_t>(character, kCharacterStress);
			out.maxStress = At<int16_t>(character, kCharacterMaxStress);
			out.alliance = At<int>(character, kCharacterAlliance);
			out.allianceAtSpawn = At<int>(character, kCharacterAllianceAtSpawn);
			out.traversal = At<int>(character, kCharacterTraversal);
			out.noDamage = At<uint8_t>(character, kCharacterNoDamage);
			out.noReact = At<uint8_t>(character, kCharacterNoReact);
			out.important = At<uint8_t>(character, kCharacterImportant);
			out.untargetable = At<uint8_t>(character, kCharacterUntargetable);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool ReadMind(const uint8_t* character, AI_STATE::State& out)
	{
		__try
		{
			const uint8_t* motivations = Pointer(character, kCharacterMotivations);
			out.haveMotivations = motivations != nullptr;
			for (int i = 0; motivations && i < AI_STATE::kMotivations; i++)
				out.motivations[i] = At<uint8_t>(motivations, kMotivationFirst + i) != 0;
			const uint8_t* moods = Pointer(character, kCharacterMood);
			out.haveMood = moods != nullptr;
			if (moods)
			{
				for (int i = 0; i < AI_STATE::kMoodLayers; i++)
				{
					out.moodLayer[i] = At<int>(moods, kMoodLayers + i * 4);
					out.moodTime[i] = At<float>(moods, kMoodTimes + i * 4);
				}
				out.mood = At<int>(moods, kMoodActive);
			}
			const uint8_t* health = character + kCharacterHealthState;
			out.healthState = At<int>(health, kHealthState);
			out.health = At<uint16_t>(health, kHealthNow);
			out.maxHealth = At<uint16_t>(health, kHealthMost);
			out.healthModifier = At<float>(health, kHealthModifier);
			for (int i = 0; i < AI_STATE::kGauges; i++)
			{
				const uint8_t* gauge = Pointer(character, kCharacterGauges + i * 4);
				AI_STATE::Gauge& shown = out.gauges[i];
				shown.have = gauge != nullptr;
				if (!gauge)
					continue;
				memcpy(shown.thresholds, gauge + kGaugeThresholds, sizeof(shown.thresholds));
				shown.amount = At<float>(gauge, kGaugeAmount);
				shown.level = At<int>(gauge, kGaugeLevel);
				shown.paused = At<uint8_t>(gauge, kGaugePaused) != 0;
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	uint32_t ReadId(const uint8_t* character)
	{
		__try
		{
			return At<uint32_t>(character, kCharacterId);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	// ---- Names ----

	const char* Named(const char* const* names, int count, int index)
	{
		return index >= 0 && index < count ? names[index] : "?";
	}
	template <size_t N> const char* Named(const char* const (&names)[N], int index)
	{
		return Named(names, static_cast<int>(N), index);
	}

	const AI_STATE::FlagInfo kFlags[AI_STATE::kStateFlags] = {
		{ AI_STATE::kLogic, "player_controlled", "Player controlled", 0x284 },
		{ AI_STATE::kLogic, "just_ignored_hit", "Just ignored a hit", 0x285 },
		{ AI_STATE::kLogic, "takes_falling_damage", "Takes falling damage (never read by the game)", 0x286 },
		{ AI_STATE::kLogic, "got_damaged_by_non_melee_weapon", "Damaged by a weapon other than melee", 0x287 },
		{ AI_STATE::kLogic, "fire_target_only_when_killed_by_player", "Fires its target only when killed by the player", 0x288 },
		{ AI_STATE::kLogic, "killed_by_primary_character", "Killed by the player", 0x289 },
		{ AI_STATE::kLogic, "killed_using_a_fatality", "Killed by a finishing move", 0x28A },
		{ AI_STATE::kLogic, "was_killed_this_frame", "Killed this frame", 0x28B },
		{ AI_STATE::kLogic, "corpse_was_enemy", "Its corpse was an enemy", 0x28C },
		{ AI_STATE::kLogic, "on_fire", "On fire", 0x28D },
		{ AI_STATE::kLogic, "unreachable", "Unreachable", 0x28E },
		{ AI_STATE::kLogic, "death_effect_started", "Death effect started", 0x28F },
		{ AI_STATE::kLogic, "script_disabled_show_in_motion_tracker", "A script took it off the motion tracker", 0x290 },
		{ AI_STATE::kLogic, "locomotion_action_stopped_or_stopping", "Its movement action stopped or stopping", 0x291 },
		{ AI_STATE::kLogic, "ready_to_shoot", "Ready to shoot", 0x292 },
		{ AI_STATE::kLogic, "peeking", "Peeking", 0x293 },
		{ AI_STATE::kLogic, "in_radial_menu", "In the radial menu", 0x294 },
		{ AI_STATE::kLogic, "being_despawned", "Being despawned", 0x295 },
		{ AI_STATE::kLogic, "ragdoll_being_crushed", "Ragdoll being crushed by moving geometry", 0x296 },
		{ AI_STATE::kLogic, "ignore_ui_interactions", "Ignores interaction prompts", 0x297 },
		{ AI_STATE::kLogic, "hide_ui_interactions", "Hides interaction prompts", 0x298 },
		{ AI_STATE::kLogic, "hide_ui_tutorials", "Hides tutorials", 0x299 },
		{ AI_STATE::kLogic, "dead_man_walking", "Dead man walking", 0x29A },
		{ AI_STATE::kLogic, "trigger_rejected_during_dead_man_walking", "A trigger was refused while a dead man walking", 0x29B },
		{ AI_STATE::kLogic, "ui_breakout_scheduled", "Breakout prompt scheduled", 0x29C },
		{ AI_STATE::kLogic, "on_motion_tracker", "On the motion tracker", 0x29D },
		{ AI_STATE::kLogic, "only_ragdoll_deaths", "Only ragdoll deaths", 0x29E },
		{ AI_STATE::kShared, "has_been_dead_or_dying", "Has been dead or dying", 0x2A4 },
		{ AI_STATE::kShared, "active_container", "Active container", 0x2A5 },
		{ AI_STATE::kShared, "anchored", "Anchored", 0x2A6 },
		{ AI_STATE::kShared, "anchored_by_third_party", "Anchored by something else", 0x2A7 },
		{ AI_STATE::kShared, "repulsive", "Pushes others away", 0x2A8 },
		{ AI_STATE::kShared, "freeze_all_additive_anims", "Additive animations frozen", 0x2A9 },
		{ AI_STATE::kShared, "air_steering_allowed", "Steering in the air allowed", 0x2AA },
		{ AI_STATE::kShared, "build_from_matrix", "Pose built from its matrix", 0x2AB },
		{ AI_STATE::kShared, "should_cough", "Should cough", 0x2AC },
		{ AI_STATE::kShared, "using_traversal_mechanism", "Using a traversal", 0x2AD },
		{ AI_STATE::kShared, "enemy_of_the_player", "Enemy of the player", 0x2AE },
		{ AI_STATE::kShared, "crouching", "Crouching", 0x2AF },
		{ AI_STATE::kShared, "deep_crouching", "Deep crouching", 0x2B0 },
		{ AI_STATE::kShared, "alien_deep_crouching", "Alien deep crouching", 0x2B1 },
		{ AI_STATE::kShared, "cant_stand_up", "Can't stand up", 0x2B2 },
		{ AI_STATE::kShared, "entering_vent", "Entering a vent", 0x2B3 },
		{ AI_STATE::kShared, "in_vent", "In a vent", 0x2B4 },
		{ AI_STATE::kShared, "use_vent_camera", "Uses the vent camera", 0x2B5 },
		{ AI_STATE::kShared, "use_cover_camera", "Uses the cover camera", 0x2B6 },
		{ AI_STATE::kShared, "using_healthkit", "Using a medkit", 0x2B7 },
		{ AI_STATE::kShared, "sprinting", "Sprinting", 0x2B8 },
		{ AI_STATE::kShared, "breathing", "Breathing", 0x2B9 },
		{ AI_STATE::kShared, "torch_drawn", "Torch drawn", 0x2BA },
		{ AI_STATE::kShared, "player_can_get_weapons_out", "Player can get weapons out", 0x2BB },
		{ AI_STATE::kShared, "player_forced_unarmed", "Player forced unarmed", 0x2BC },
		{ AI_STATE::kShared, "first_attack_chainable", "First attack chainable", 0x2BD },
		{ AI_STATE::kShared, "second_attack_chainable", "Second attack chainable", 0x2BE },
		{ AI_STATE::kShared, "counter_strike_ready", "Counter strike ready", 0x2BF },
		{ AI_STATE::kShared, "combo_selection_active", "Combo selection active", 0x2C0 },
		{ AI_STATE::kShared, "can_take_damage_from_arrows", "Can take damage from \"arrows\" (the game's word)", 0x2C1 },
		{ AI_STATE::kShared, "can_be_jostled", "Can be jostled", 0x2C2 },
		{ AI_STATE::kShared, "aim_active", "Aiming", 0x2C3 },
		{ AI_STATE::kShared, "in_cover", "In cover", 0x2C4 },
		{ AI_STATE::kShared, "ragdoll_death", "Ragdoll death", 0x2C5 },
		{ AI_STATE::kShared, "can_shoot_from_cover", "Can shoot from cover", 0x2C6 },
		{ AI_STATE::kShared, "use_mirrored_camera", "Uses the mirrored camera", 0x2C7 },
		{ AI_STATE::kShared, "look_at_allowed", "Looking at things allowed", 0x2C8 },
		{ AI_STATE::kShared, "free_look_allowed", "Free look allowed", 0x2C9 },
		{ AI_STATE::kShared, "leading_look_allowed", "Looking ahead allowed", 0x2CA },
		{ AI_STATE::kShared, "look_around_allowed", "Looking around allowed", 0x2CB },
		{ AI_STATE::kShared, "procedural_look_at", "Procedural looking", 0x2CC },
		{ AI_STATE::kShared, "disable_auto_look_ats", "Automatic looking off", 0x2CD },
		{ AI_STATE::kShared, "can_react", "Can react", 0x2CE },
		{ AI_STATE::kShared, "has_been_hit", "Has been hit", 0x2CF },
		{ AI_STATE::kShared, "alien_quadrupedal", "Alien on all fours", 0x2D0 },
		{ AI_STATE::kShared, "allow_ducking", "Ducking allowed", 0x2D1 },
		{ AI_STATE::kShared, "has_important_state", "In an important state", 0x2D2 },
		{ AI_STATE::kShared, "secondary_impact_active", "Secondary impact playing", 0x2D3 },
		{ AI_STATE::kShared, "primary_impact_active", "Primary impact playing", 0x2D4 },
		{ AI_STATE::kShared, "stunned", "Stunned", 0x2D5 },
		{ AI_STATE::kShared, "show_in_motion_tracker", "Shown on the motion tracker", 0x2D6 },
		{ AI_STATE::kShared, "combat_stance", "Combat stance", 0x2D7 },
		{ AI_STATE::kShared, "was_combat_stance", "Was in combat stance", 0x2D8 },
		{ AI_STATE::kShared, "invisible", "Invisible to senses", 0x2D9 },
		{ AI_STATE::kShared, "backstage", "Backstage (in the vents)", 0x2DA },
		{ AI_STATE::kShared, "in_backstage_transition", "Going into or out of the vents", 0x2DB },
		{ AI_STATE::kShared, "backstage_transition_entering", "Going into the vents", 0x2DC },
		{ AI_STATE::kIndependent, "inactive", "Inactive", 0x2F8 },
		{ AI_STATE::kIndependent, "request_deactivate", "Asked to deactivate", 0x2F9 },
		{ AI_STATE::kIndependent, "in_world", "In the world", 0x2FA },
		{ AI_STATE::kIndependent, "activated", "Activated", 0x2FB },
		{ AI_STATE::kIndependent, "fully_spawned", "Fully spawned", 0x2FC },
		{ AI_STATE::kIndependent, "in_limbo", "In limbo", 0x2FD },
		{ AI_STATE::kIndependent, "interacting", "Interacting", 0x2FE },
		{ AI_STATE::kIndependent, "discontinuous", "Moved discontinuously", 0x2FF },
		{ AI_STATE::kIndependent, "requires_relinking", "Needs relinking to its network copy", 0x300 },
		{ AI_STATE::kIndependent, "build_disabled", "Pose building off", 0x301 },
		{ AI_STATE::kIndependent, "frozen", "Frozen", 0x302 },
		{ AI_STATE::kIndependent, "ready_for_deactivation", "Ready to deactivate", 0x303 },
		{ AI_STATE::kIndependent, "autonomic_response_waiting", "Automatic response waiting", 0x304 },
		{ AI_STATE::kIndependent, "starts_in_air", "Starts in the air", 0x305 },
		{ AI_STATE::kIndependent, "selected_in_developers_menu", "Selected for debugging", 0x306 },
		{ AI_STATE::kIndependent, "network_copy", "A network copy", 0x307 },
		{ AI_STATE::kIndependent, "using_network_copy_activity", "Using a network copy's activity", 0x308 },
		{ AI_STATE::kIndependent, "camera_control_for_torch", "Camera controls the torch", 0x309 },
		{ AI_STATE::kIndependent, "default_spawned_player", "The player spawned by default", 0x30A },
		{ AI_STATE::kIndependent, "can_auto_heal", "Heals by itself", 0x30B },
		{ AI_STATE::kIndependent, "uses_multiplayer_comms", "Uses multiplayer comms", 0x30C },
	};

	// The motivations: the families of behaviour each allows, as the behaviour tree editor's motivation checks name them
	struct Motivation
	{
		const char* key;
		const char* label;
	};
	const Motivation kMotivationNames[AI_STATE::kMotivations] = {
		{ "cinematic", "Cinematic" }, { "attack", "Attack" }, { "aim", "Aim" }, { "despawn", "Despawn" }, { "follow", "Follow" },
		{ "standard", "Standard" }, { "search_systematic", "Systematic search" }, { "stalk", "Stalk" },
		{ "backstage_stalk", "Backstage stalk" }, { "shot", "Shot" }, { "suspect_target_response", "Suspect target response" },
		{ "threat_aware", "Threat aware" }, { "backstage_ambush", "Backstage ambush" }, { "idle_job", "Idle job" },
		{ "use_cover", "Use cover" }, { "assault", "Assault" }, { "melee", "Melee" }, { "retreat", "Retreat" },
		{ "close_on_target", "Close on target" }, { "mutual_melee_only", "Mutual melee only" }, { "vent_melee", "Vent melee" },
		{ "multiplayer", "Multiplayer" }, { "react_to_weapon", "React to weapon" }, { "suspicious_item", "Suspicious item" },
		{ "aggression_escalation", "Aggression escalation" }, { "stun_damage", "Stun damage" }, { "breakout", "Breakout" },
		{ "player_hide", "Player hiding" }, { "observe_target", "Observe target" }, { "advancing", "Advancing" },
		{ "ambush", "Ambush" }, { "panic", "Panic" }, { "force_idle", "Force idle (debug)" },
		{ "backstage_alien_response", "Backstage alien response" }, { "escalation_prevents_search", "Escalation prevents search" },
	};

	const char* const kMoodKeys[] = { "neutral", "scared", "angry", "happy", "sad", "suspicious", "injured" };

	// ---- Finding characters ----

	uint8_t* CharacterManager()
	{
		__try
		{
			const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kGameGlobals);
			if (!at || !*reinterpret_cast<const uint8_t*>(at + kCharactersValid))
				return nullptr;
			uint8_t* globals = *reinterpret_cast<uint8_t**>(at);
			return globals ? *reinterpret_cast<uint8_t**>(globals + kGlobalsCharacters) : nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	// ---- The state-change log ----

	constexpr size_t kLogSize = 2000;
	std::mutex g_logMutex;
	std::vector<AI_STATE::LogEntry> g_log;   // a ring of kLogSize
	size_t g_logNext = 0;
	uint32_t g_logSequence = 0;
	std::atomic<bool> g_logging = false;

	struct Site
	{
		uint8_t* call;
		int32_t original;
	};
	std::vector<Site> g_sites;

	const char* const kMachines[] = { "COVER", "DEATH", "WEAPON", "GENERAL", "SHOOTING" };
	const char* const kMachineNames[] = { "Cover", "Dying", "Weapon", "Moving", "Shooting" };

	// A string the game handed over, copied guarded
	void CopyGameString(char* out, size_t size, const char* from)
	{
		out[0] = '\0';
		__try
		{
			if (!from)
				return;
			size_t i = 0;
			for (; i + 1 < size && from[i]; i++)
				out[i] = from[i];
			out[i] = '\0';
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			out[0] = '\0';
		}
	}

	bool FillEntry(AI_STATE::LogEntry& entry, const uint8_t* character, const char* machine, const char* from, const char* to, const char* third)
	{
		__try
		{
			const uintptr_t time = DEVTOOLS_RELATIVE_ADDRESS(kGameTime);
			entry.time = time ? static_cast<float>(*reinterpret_cast<const double*>(time)) : 0.0f;
			if (character)
			{
				entry.id = At<uint32_t>(character, kCharacterId);
				CopyName(entry.character, sizeof(entry.character), character + kCharacterName, 0x40);
			}
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
		// The machine by its plain name
		char raw[16];
		CopyGameString(raw, sizeof(raw), machine);
		strncpy_s(entry.machine, raw, _TRUNCATE);
		for (size_t i = 0; i < sizeof(kMachines) / sizeof(kMachines[0]); i++)
			if (strcmp(raw, kMachines[i]) == 0)
				strncpy_s(entry.machine, kMachineNames[i], _TRUNCATE);
		CopyGameString(entry.from, sizeof(entry.from), from);
		CopyGameString(entry.to, sizeof(entry.to), to);
		CopyGameString(entry.third, sizeof(entry.third), third);
		return true;
	}

	// What the 29 calls now reach: the same five arguments the game's empty logger takes (the character, the machine, then
	// three state names), the caller taking them off the stack
	void __cdecl StateLogged(const uint8_t* character, const char* machine, const char* from, const char* to, const char* third)
	{
		if (!g_logging.load(std::memory_order_relaxed))
			return;
		AI_STATE::LogEntry entry;
		if (!FillEntry(entry, character, machine, from, to, third))
			return;
		std::lock_guard<std::mutex> lock(g_logMutex);
		entry.sequence = ++g_logSequence;
		if (g_log.size() < kLogSize)
			g_log.push_back(entry);
		else
			g_log[g_logNext] = entry;
		g_logNext = (g_logNext + 1) % kLogSize;
	}

	bool StringIs(const char* at, uintptr_t imageStart, uintptr_t imageEnd, const char* expected)
	{
		__try
		{
			const uintptr_t address = reinterpret_cast<uintptr_t>(at);
			const size_t length = strlen(expected);
			if (address < imageStart || address + length + 1 > imageEnd)
				return false;
			return memcmp(at, expected, length + 1) == 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// Each of the 29 calls: "push <the machine's name>; push <the character>; call <a bare return>; add esp, 14h"
	void FindSites(uint8_t* textStart, uint8_t* textEnd, uintptr_t imageStart, uintptr_t imageEnd, std::vector<uint8_t*>& out)
	{
		__try
		{
			for (uint8_t* at = textStart + 6; at + 8 <= textEnd; at++)
			{
				if (at[0] != 0xE8 || at[-6] != 0x68 || (at[-1] & 0xF8) != 0x50 || at[5] != 0x83 || at[6] != 0xC4 || at[7] != 0x14)
					continue;
				const uint8_t* target = at + 5 + *reinterpret_cast<const int32_t*>(at + 1);
				if (target < textStart || target >= textEnd || *target != 0xC3)
					continue;
				const char* name = *reinterpret_cast<const char* const*>(at - 5);
				for (const char* machine : kMachines)
					if (StringIs(name, imageStart, imageEnd, machine))
					{
						out.push_back(at);
						break;
					}
			}
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
		}
	}

	void WriteRelative(uint8_t* call, int32_t relative)
	{
		DWORD oldProtect;
		if (!VirtualProtect(call, 5, PAGE_EXECUTE_READWRITE, &oldProtect))
			return;
		memcpy(call + 1, &relative, sizeof(relative));
		VirtualProtect(call, 5, oldProtect, &oldProtect);
		FlushInstructionCache(GetCurrentProcess(), call, 5);
	}

	int32_t RelativeTo(const uint8_t* call, const void* target)
	{
		return static_cast<int32_t>(reinterpret_cast<uintptr_t>(target) - (reinterpret_cast<uintptr_t>(call) + 5));
	}
}

// ---- Reading ----

bool AI_STATE::Read(const uint8_t* character, State& out)
{
	out = State();
	if (!character)
		return false;
	out.id = ReadId(character);
	const bool orders = ReadOrders(character, out);
	const bool body = ReadBody(character, out);
	const bool flags = ReadFlags(character, out);
	const bool mind = ReadMind(character, out);
	out.read = orders && body && flags && mind;
	return out.read;
}

const AI_STATE::FlagInfo& AI_STATE::Flag(int index)
{
	return kFlags[index < 0 ? 0 : index >= kStateFlags ? kStateFlags - 1 : index];
}

const char* AI_STATE::FlagSetName(FlagSet set)
{
	switch (set)
	{
	case kLogic: return "Logic only";
	case kShared: return "Shared with the animation";
	default: return "Independent";
	}
}

// ---- Names ----

const char* AI_STATE::CommandTypeName(int type)
{
	static const char* const kNames[] = { "None", "Idle", "Die", "Go to", "Go to cover", "Move towards", "Follow", "Play animation",
		"Shoot at", "Aim at its target", "Aim at", "Launch attack", "Dead", "Pick up weapon", "Moving to a job task", "Shoot from cover",
		"Shoot target", "Read the pad", "A network player's character", "Template" };
	return Named(kNames, type);
}

const char* AI_STATE::CommandStatusName(int status)
{
	static const char* const kNames[] = { "Running", "Shutdown started", "Shutdown completing", "Shutting down", "Shut down", "Not set" };
	return Named(kNames, status);
}

const char* AI_STATE::RequestTypeName(int type)
{
	static const char* const kNames[] = { "Invalid", "Move to", "Take cover", "Stop", "Turn to face", "Action", "Player control",
		"Stance change", "Teleport", "Move along a direction", "Dead", "Abort" };
	return Named(kNames, type);
}

const char* AI_STATE::RequestStatusName(int status)
{
	static const char* const kNames[] = { "Created", "Queued", "Active", "Succeeded", "Failed", "Interrupted", "Discarded" };
	return Named(kNames, status);
}

const char* AI_STATE::HighLevelName(int state)
{
	static const char* const kNames[] = { "Starting up", "Inactive", "Moving", "Waiting", "Cover", "Action control", "Player control",
		"Stance change", "Teleporting", "Dead", "Abort" };
	return Named(kNames, state);
}

const char* AI_STATE::GeneralName(int state)
{
	static const char* const kNames[] = { "Not in use", "Start moving", "Moving", "Stop moving", "Turn on the spot", "Small turn",
		"Manual node", "Traversing", "Waiting", "Teleport", "Abort", "Backstage" };
	return Named(kNames, state);
}

const char* AI_STATE::CoverName(int state)
{
	static const char* const kNames[] = { "Not in use", "Getting into cover", "Idle", "Shoot", "Leaving cover", "Abort" };
	return Named(kNames, state);
}

const char* AI_STATE::DeadName(int state)
{
	static const char* const kNames[] = { "Alive", "Health reached zero", "Dying", "Dead" };
	return Named(kNames, state);
}

const char* AI_STATE::IdleStyleName(int style)
{
	static const char* const kNames[] = { "No fidgets", "Normal", "Search", "Corpse trap" };
	return style == 0xFF ? "Unknown" : Named(kNames, style);
}

const char* AI_STATE::ActionName(int action)
{
	static const char* const kNames[] = {
		"Unused", "Idle", "Conversation idle", "Activate / deactivate", "Network client", "Network reaction", "Network attack",
		"Aimed movement", "Leap", "Fall", "Drop down", "Bullet impact", "Hit and run impact", "Bullet death",
		"Instant death (no animation)", "Ragdoll death", "Aimed idle", "Aimed idle (NPC)", "Play animation", "Paired attack",
		"Hidden teleport", "Pad test", "One-shot traversal", "Pipe traversal", "Pipe traversal (aiming)", "Ledge traversal",
		"Start sidling", "Sidling idle", "Sidling", "Stop sidling", "Beam idle", "Beam move", "Onto a beam", "Off a beam",
		"Ladder", "Vent traversal", "Into or out of a vent", "Climb under", "Idle in cover", "Aim from cover", "Vault from cover",
		"Cover to cover", "Cover movement (pad)", "Cover movement (auto)", "Move to cover", "Leave cover", "Standard movement",
		"Player melee", "Stealth knock-out", "Player melee intercept", "NPC melee intercept", "Player melee kill",
		"Killed in melee", "Step out of the way", "Exchange item", "Start moving", "Moving", "Turn on the spot",
		"Alien vent movement", "Stop moving", "Alien hit and run", "Hit and run victim", "Facehugger vent attack",
		"Death by facehugger", "Death by EMP", "Melee attack", "Melee grapple", "Block", "Melee victim", "Melee victim (blocking)",
		"Grapple break", "Grabbed", "Knocked to the ground", "Explosion victim", "Suspicious item", "Backstage move",
		"Backstage move (no path)", "Backstage idle", "Alien grapple", "Alien grapple victim", "Impact stumble", "Stumble",
		"Player impact", "Suspect response", "Search (deep crouch)", "Threat escalation", "Stunned", "Hit by the flamethrower",
		"Alien threat aware", "Play tree, face target", "Play tree", "Single step", "Place ready item" };
	return Named(kNames, action);
}

const char* AI_STATE::SecondaryActionName(int action)
{
	static const char* const kNames[] = {
		"Off", "Idle", "Idle noise", "Movement noise", "Recoil", "Charge", "Reload", "Holster", "Pick up", "Attach accessory",
		"Additive impact", "Aim gun", "Tail", "Duck", "Breathe", "Look (IK only)", "Look (moving)", "Look at the player", "Look at",
		"Look around", "Hand posing", "Face posing", "Tail posing", "Play animation", "Heal self", "Throw ready item",
		"Use ready item", "Conversation gesture", "Flamethrower recoil (alien)", "Hit and run", "Threat aware attack", "Gun held",
		"Cough", "Escalation noise", "Search noise", "Player melee" };
	return Named(kNames, action);
}

const char* AI_STATE::SecondaryLayerName(int layer)
{
	static const char* const kNames[] = { "Additive", "Breathing", "Looking", "Gun / tail", "Hands", "Face" };
	return Named(kNames, layer);
}

const char* AI_STATE::MotivationName(int motivation)
{
	return motivation >= 1 && motivation <= kMotivations ? kMotivationNames[motivation - 1].label : "?";
}

const char* AI_STATE::MotivationKey(int motivation)
{
	return motivation >= 1 && motivation <= kMotivations ? kMotivationNames[motivation - 1].key : "?";
}

int AI_STATE::MotivationFromKey(const std::string& key)
{
	for (int i = 0; i < kMotivations; i++)
		if (_stricmp(key.c_str(), kMotivationNames[i].key) == 0)
			return i + 1;
	return 0;
}

const char* AI_STATE::AllianceName(int alliance)
{
	static const char* const kNames[] = { "Neutral", "Player", "Player's ally", "Alien", "Android", "Civilian", "Security", "Dead", "Dead man walking" };
	return Named(kNames, alliance);
}

const char* AI_STATE::TraversalName(int traversal)
{
	static const char* const kNames[] = { "Climb under", "Vent", "Ledge", "Sidling", "Ladder", "Pipe", "Balance beam", "Tight gap", "Wall", "None" };
	return Named(kNames, traversal);
}

const char* AI_STATE::GaugeName(int gauge)
{
	static const char* const kNames[] = { "Retreat", "Stun damage" };
	return Named(kNames, gauge);
}

const char* AI_STATE::MoodKey(int mood)
{
	return Named(kMoodKeys, mood);
}

int AI_STATE::MoodFromKey(const std::string& key)
{
	for (int i = 0; i < static_cast<int>(sizeof(kMoodKeys) / sizeof(kMoodKeys[0])); i++)
		if (_stricmp(key.c_str(), kMoodKeys[i]) == 0)
			return i;
	return -1;
}

std::string AI_STATE::SwitchOwners(uint8_t bits)
{
	// A bit for each part of the game that set the switch; any bit set counts
	static const char* const kOwners[] = { "an action", "an activity", "combat", "a script", "a cutscene", "debugging" };
	if (!bits)
		return "no";
	std::string text = "yes (";
	bool first = true;
	for (int bit = 0; bit < 8; bit++)
	{
		if (!(bits & (1 << bit)))
			continue;
		text += first ? "" : ", ";
		text += bit < 6 ? kOwners[bit] : "other";
		first = false;
	}
	return text + ")";
}

// ---- Finding characters ----

uint8_t* AI_STATE::FindCharacter(uint32_t id)
{
	uint8_t* manager = CharacterManager();
	if (!manager || !id)
		return nullptr;
	const uint32_t lists[] = { kPlayers, kAlive, kDead };
	__try
	{
		for (uint32_t list : lists)
		{
			uint8_t* const* entries = *reinterpret_cast<uint8_t* const* const*>(manager + list);
			const uint32_t count = *reinterpret_cast<const uint32_t*>(manager + list + 4);
			for (uint32_t i = 0; entries && i < count && i < kMaxPerList; i++)
				if (entries[i] && *reinterpret_cast<const uint32_t*>(entries[i] + kCharacterId) == id)
					return entries[i];
		}
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
	}
	return nullptr;
}

const uint8_t* AI_STATE::FrameCharacter(uint32_t id)
{
	if (!id)
		id = DEBUG_MENU::SelectedId();
	for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
		if (character.id == id)
			return character.object;
	return nullptr;
}

// ---- Changes (AI thread) ----

bool AI_STATE::ApplyMotivation(uint8_t* character, int motivation, int change)
{
	if (!apply_motivation || motivation < 1 || motivation > kMotivations || change < 0 || change > 2)
		return false;
	__try
	{
		uint8_t* motivations = *reinterpret_cast<uint8_t**>(character + kCharacterMotivations);
		if (!motivations)
			return false;
		apply_motivation(motivations, motivation, change);
		return true;
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return false;
	}
}

bool AI_STATE::PushMood(uint8_t* character, int layer, int mood, float time)
{
	if (!push_mood || layer < 0 || layer >= kMoodLayers || mood < 0 || mood > 6)
		return false;
	__try
	{
		uint8_t* moods = *reinterpret_cast<uint8_t**>(character + kCharacterMood);
		if (!moods)
			return false;
		push_mood(moods, layer, mood, time);
		return true;
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return false;
	}
}

bool AI_STATE::SetHealth(uint8_t* character, int health)
{
	if (!force_set_health)
		return false;
	__try
	{
		uint8_t* state = character + kCharacterHealthState;
		const int most = static_cast<int>(At<uint16_t>(state, kHealthMost) * At<float>(state, kHealthModifier));
		const int value = (std::max)(1, (std::min)(health, (std::max)(1, most)));
		force_set_health(state, static_cast<uint16_t>(value));
		return true;
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return false;
	}
}

bool AI_STATE::Damage(uint8_t* character, int amount)
{
	if (!take_damage || amount <= 0)
		return false;
	__try
	{
		take_damage(character, static_cast<int16_t>((std::min)(amount, 0x7FFF)), character, false, false, true, false, kDamageKind, false, false);
		return true;
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return false;
	}
}

bool AI_STATE::Burn(uint8_t* character)
{
	if (!burn)
		return false;
	__try
	{
		burn(character + kCharacterHealthState, 5.0f, 0, nullptr);
		return true;
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return false;
	}
}

bool AI_STATE::Kill(uint8_t* character)
{
	if (!force_kill)
		return false;
	__try
	{
		force_kill(character, nullptr);
		return true;
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return false;
	}
}

// ---- Spawning (entity thread) ----

namespace
{
	enum class SpawnCall { Called, NotFound, Faulted };

	SpawnCall CallMethodGuarded(void* manager, uint32_t entity, uint32_t instance, uint32_t method)
	{
		uint8_t access[8] = {};
		Allocation* found = nullptr;
		volatile SpawnCall result = SpawnCall::NotFound;
		volatile bool locked = false;
		__try
		{
			access_lock(access);
			locked = true;
			const uint32_t handle[2] = { entity, instance };
			find_entity(manager, &found, handle);
			if (found && found->data)
			{
				// The game's call of a method on an entity only reads the entity pointer at +0x24 of the temporary record
				// it is given; a zeroed record with the entity there stands in for it (as LIVE_LINK does)
				alignas(8) uint8_t temporaryEntity[0x40] = {};
				*reinterpret_cast<Allocation**>(temporaryEntity + kTemporaryEntityTarget) = found;
				call_custom_method(temporaryEntity, &found, &method, 0);
				result = SpawnCall::Called;
			}
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			result = SpawnCall::Faulted;
		}
		__try
		{
			if (found)
				release_entity(&found);
			if (locked)
				access_unlock(access);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			result = SpawnCall::Faulted;
		}
		return result;
	}

	// An entity's state bits, after its table of functions (active 0x01 - a spawn only goes ahead while it is set - spawned
	// 0x02, updated every frame 0x40000; not the separate "switched on" bit 0x4000), and a Character entity's own stage in
	// making its character (read only from an entity whose table of functions is the Character entity's)
	constexpr uint32_t kEntityStateBits = 0x04;
	constexpr uint32_t kEntityActive = 0x01;
	constexpr uint32_t kEntitySpawned = 0x02;
	constexpr uint32_t kEntityUpdating = 0x40000;
	constexpr uint32_t kCharacterEntityStage = 0xF0;
	constexpr uintptr_t kCharacterEntityTable = 0x00DD08D8;

	bool ReadStageGuarded(void* manager, uint32_t entity, uint32_t instance, uintptr_t characterTable, bool& active, bool& spawned, bool& updating, int& stage)
	{
		uint8_t access[8] = {};
		Allocation* found = nullptr;
		volatile bool read = false;
		volatile bool locked = false;
		__try
		{
			access_lock(access);
			locked = true;
			const uint32_t handle[2] = { entity, instance };
			find_entity(manager, &found, handle);
			if (found && found->data)
			{
				const uint8_t* object = static_cast<const uint8_t*>(found->data);
				const uint32_t bits = *reinterpret_cast<const uint32_t*>(object + kEntityStateBits);
				active = (bits & kEntityActive) != 0;
				spawned = (bits & kEntitySpawned) != 0;
				updating = (bits & kEntityUpdating) != 0;
				const bool characterEntity = characterTable && *reinterpret_cast<const uintptr_t*>(object) == characterTable;
				stage = characterEntity ? *reinterpret_cast<const int*>(object + kCharacterEntityStage) : -1;
				read = true;
			}
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			read = false;
		}
		__try
		{
			if (found)
				release_entity(&found);
			if (locked)
				access_unlock(access);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
		}
		return read;
	}
}

bool AI_STATE::ReadSpawnStage(uint32_t entity, uint32_t instance, SpawnStage& out)
{
	out = SpawnStage();
	if (!access_lock || !access_unlock || !find_entity || !release_entity || !entity)
		return false;
	const uintptr_t managerAt = DEVTOOLS_RELATIVE_ADDRESS(kEntityManager);
	void* manager = managerAt ? *reinterpret_cast<void**>(managerAt) : nullptr;
	if (!manager)
		return false;
	bool active = false, spawned = false, updating = false;
	int stage = -1;
	if (!ReadStageGuarded(manager, entity, instance, DEVTOOLS_RELATIVE_ADDRESS(kCharacterEntityTable), active, spawned, updating, stage))
		return false;
	out.active = active;
	out.spawned = spawned;
	out.updating = updating;
	out.stage = stage;
	return true;
}

const char* AI_STATE::SpawnStageName(int stage)
{
	// The Character entity's stages: waiting for a free character (the game despawns a corpse to make room), starting,
	// loading the character's assets, in the level, dead, despawned, and not spawned since the level started
	static const char* const kNames[] = { "waiting for a free character slot", "starting to spawn", "loading its assets", "in the level", "dead",
		"despawned", "not spawned yet" };
	if (stage < 0)
		return "stage unknown (not a Character entity, or this release of the game isn't known)";
	return stage < static_cast<int>(sizeof(kNames) / sizeof(kNames[0])) ? kNames[stage] : "stage unknown";
}

bool AI_STATE::CallSpawnMethod(uint32_t entity, uint32_t instance, bool spawn, std::string& message)
{
	if (const char* why = LIVE_LINK::NotReadyForEdits())
	{
		message = why;
		return false;
	}
	if (!access_lock || !access_unlock || !find_entity || !call_custom_method || !release_entity)
	{
		message = "not available on this release of the game";
		return false;
	}
	const uintptr_t managerAt = DEVTOOLS_RELATIVE_ADDRESS(kEntityManager);
	void* manager = managerAt ? *reinterpret_cast<void**>(managerAt) : nullptr;
	if (!manager || !entity)
	{
		message = "no level running";
		return false;
	}
	switch (CallMethodGuarded(manager, entity, instance, spawn ? kMethodSpawn : kMethodDespawn))
	{
	case SpawnCall::Called:
		message = spawn ? "spawn called on its script entity" : "despawn called on its script entity";
		return true;
	case SpawnCall::NotFound:
		message = "its script entity is not in the running level";
		return false;
	default:
		message = "the call faulted";
		return false;
	}
}

// ---- The state-change log ----

void AI_STATE::PatchStateLog(bool attach)
{
	if (!attach)
	{
		for (const Site& site : g_sites)
		{
			// Only put back what is still ours
			int32_t now = 0;
			memcpy(&now, site.call + 1, sizeof(now));
			if (now == RelativeTo(site.call, reinterpret_cast<const void*>(reinterpret_cast<uintptr_t>(&StateLogged))))
				WriteRelative(site.call, site.original);
		}
		g_sites.clear();
		g_logging = false;
		return;
	}
	if (!g_sites.empty())
		return;
	const uintptr_t base = DevTools::GameProcess::GetBaseAddress();
	if (!base || DevTools::Build() == DevTools::GameBuild::Unknown)
		return;
	const IMAGE_DOS_HEADER* dos = reinterpret_cast<const IMAGE_DOS_HEADER*>(base);
	const IMAGE_NT_HEADERS* nt = reinterpret_cast<const IMAGE_NT_HEADERS*>(base + dos->e_lfanew);
	const IMAGE_SECTION_HEADER* section = IMAGE_FIRST_SECTION(nt);
	std::vector<uint8_t*> calls;
	for (WORD i = 0; i < nt->FileHeader.NumberOfSections; i++, section++)
	{
		if (memcmp(section->Name, ".text", 5) != 0)
			continue;
		uint8_t* start = reinterpret_cast<uint8_t*>(base + section->VirtualAddress);
		FindSites(start, start + section->Misc.VirtualSize, base, base + nt->OptionalHeader.SizeOfImage, calls);
	}
	for (uint8_t* call : calls)
	{
		Site site = { call, 0 };
		memcpy(&site.original, call + 1, sizeof(site.original));
		WriteRelative(call, RelativeTo(call, reinterpret_cast<const void*>(reinterpret_cast<uintptr_t>(&StateLogged))));
		g_sites.push_back(site);
	}
	DevTools::Log("Debug menu: %d state-change log calls found and pointed at the log", static_cast<int>(g_sites.size()));
}

int AI_STATE::StateLogSites()
{
	return static_cast<int>(g_sites.size());
}

void AI_STATE::SetStateLogging(bool on)
{
	g_logging = on && !g_sites.empty();
}

bool AI_STATE::StateLogging()
{
	return g_logging;
}

std::vector<AI_STATE::LogEntry> AI_STATE::StateLog()
{
	return StateLogSince(0);
}

std::vector<AI_STATE::LogEntry> AI_STATE::StateLogSince(uint32_t sequence)
{
	std::vector<LogEntry> out;
	std::lock_guard<std::mutex> lock(g_logMutex);
	const size_t count = g_log.size();
	// Oldest first: once the ring is full, the oldest is the one to be written next
	const size_t first = count < kLogSize ? 0 : g_logNext;
	for (size_t i = 0; i < count; i++)
	{
		const LogEntry& entry = g_log[(first + i) % count];
		if (entry.sequence > sequence)
			out.push_back(entry);
	}
	return out;
}

void AI_STATE::ClearStateLog()
{
	std::lock_guard<std::mutex> lock(g_logMutex);
	g_log.clear();
	g_logNext = 0;
}

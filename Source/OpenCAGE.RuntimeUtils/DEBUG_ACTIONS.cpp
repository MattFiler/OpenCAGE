#include "DEBUG_ACTIONS.h"
#include "DEBUG_MENU.h"
#include "GAME_DEBUG.h"
#include "GAME_SNAPSHOTS.h"
#include "LIVE_LINK.h"

#include <atomic>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <mutex>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <imgui.h>

/*
	Addresses and layouts are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).
	Each game function called here was read in the game for how it is called, and is called as the game itself calls it,
	or as noted:
	- the debug checkpoint jump hands the entity manager the checkpoint to load at its next tick, with the save check the
	  level scripts' "is any save in progress" test makes;
	- the free camera is the debug menu's cinematic camera (see Free camera below);
	- the alert builds the message with the game's own constructor, as the level scripts' fake-sense entity does;
	- the withdrawal as the level scripts' withdraw entity does it;
	- the menace as the level scripts' menace set-up entity does it, the NPC reset as the scripts' reset entity does it,
	  and the logic flags with the game's own setter, as the level scripts set them.
*/
namespace
{
	// Where the game keeps its pointer to its globals; the byte after that pointer is set while its characters can be read
	constexpr uintptr_t kGameGlobals = 0x012F0C88;
	constexpr uint32_t kCharactersValid = 0x04;
	// In the globals: the character manager, the camera manager and the saving of the game (pointers)
	constexpr uint32_t kGlobalsCharacters = 0x08;
	constexpr uint32_t kGlobalsCameras = 0x58;
	constexpr uint32_t kGlobalsSaving = 0x78;
	// The character manager's lists of characters, each a pointer to their pointers then how many: the players, those
	// alive that aren't players, and the dead
	constexpr uint32_t kPlayers = 0x80;
	constexpr uint32_t kAlive = 0xD0;
	constexpr uint32_t kDead = 0x120;
	constexpr uint32_t kMaxPerList = 18;

	// A character's fields
	constexpr uint32_t kCharacterId = 0x50;           // its unique id
	constexpr uint32_t kCharacterHandle = 0x54;       // the script entity it is: two ids
	constexpr uint32_t kCharacterSpawnedAs = 0x64;    // what it was spawned as (a pointer): its class at +4
	constexpr uint32_t kSpawnedAsClass = 0x04;
	constexpr uint32_t kSpawnedAsPlayer = 0x1C;       // set for the player's character
	constexpr uint32_t kSpawnedAsLocal = 0x1F;        // and for the one played here
	constexpr uint32_t kCharacterPosition = 0x70;     // x, y, z
	constexpr uint32_t kCharacterFrozen = 0x302;      // set while it is frozen
	constexpr uint32_t kCharacterPlayable = 0x31C;    // its player data (a pointer): set at +0x48 while a player controls it
	constexpr uint32_t kPlayableControlled = 0x48;
	constexpr uint32_t kCharacterBehaviour = 0x33E0;  // its behaviour manager (inside the character): its memory at +0xF4
	constexpr uint32_t kBehaviourMemory = 0xF4;
	constexpr uint32_t kCharacterMenace = 0x34D0;     // the alien's menace (a pointer): +0x1E4 is 1 while it is aggressive
	constexpr uint32_t kMenaceAggressive = 0x1E4;
	constexpr uint32_t kCharacterAlien = 0x34F0;      // the alien's own behaviour data (a pointer): its withdrawal at +0x18
	constexpr uint32_t kAlienWithdrawal = 0x18;       // the withdrawal: its state first (0 not, 1 needs to, 2 withdrawing)
	constexpr uint32_t kCharacterEvents = 0x3BA8;     // where what the character senses is posted for its senses to take in
	constexpr uint32_t kCharacterFlags = 0x423C;      // its logic flags, a byte each
	constexpr int kAlienClass = 1;

	// The entity manager (a pointer to it), and its fields: the running level's root composite id, the list of debug
	// checkpoints (a pointer: their count at +8), the checkpoint command waiting for its next tick, and a checkpoint load
	// pending
	constexpr uintptr_t kEntityManager = 0x0134EF40;
	constexpr uint32_t kEntityRoot = 0x0C;
	constexpr uint32_t kEntityCheckpoints = 0x40;
	constexpr uint32_t kCheckpointCount = 0x08;
	constexpr uint32_t kEntityCommand = 0xA4;
	constexpr uint32_t kEntityLoadPending = 0x10E;

	// The camera manager's fields: the camera it has made active, the free camera (pointers) and the byte that has it
	// render from the free camera. A camera's state (where it is, which way it looks, its field of view...) is at +0x2C.
	constexpr uint32_t kCamerasActive = 0x1D8;
	constexpr uint32_t kCamerasFree = 0x1F0;
	constexpr uint32_t kCamerasUseFree = 0x221;
	constexpr uint32_t kCameraState = 0x2C;

	// The input system (a pointer to it): in its player input, the byte that lets the movement stick through (the free
	// camera moves on the player's movement and look sticks)
	constexpr uintptr_t kInputSystem = 0x01359B44;
	constexpr uint32_t kInputMovementEnabled = 0x48 + 0x294E;

	// The game clock: the game time now and this frame's step (doubles), the time scale (a double) and the byte that
	// has the scale used
	constexpr uintptr_t kGameTime = 0x012410A8;
	constexpr uint32_t kTimeNow = 0x00;
	constexpr uint32_t kTimeStep = 0x18;
	constexpr uint32_t kTimeScale = 0x20;
	constexpr uint32_t kTimeScaled = 0x30;

	// What a sense activation holds: whether it is for a character it can pick out, and its owner record (whose third
	// virtual function gives the memory record of the character sensed: that character at +0x42C)
	constexpr uint32_t kActivationOfCharacter = 0x72;
	constexpr uint32_t kActivationOwner = 0x68;
	constexpr uint32_t kRecordFunction = 0x08;
	constexpr uint32_t kRecordCharacter = 0x42C;

	// A weapon's owner (a pointer to the character holding it)
	constexpr uint32_t kWeaponOwner = 0x28;

	// The scripted fake-sense message: "an enemy was heard moving", forcing the sense
	// past its "activated" threshold, the whole enemy seen
	constexpr int kMessageFakeMovementSound = 11;
	constexpr int kThresholdActivated = 2;
	constexpr size_t kMessageSize = 0x40;

	// The withdrawal: start withdrawing within 5 m, give up waiting at 15 m, force it after 10 s, and make it
	// permanent (until cancelled)
	constexpr float kWithdrawInitialRadius = 5.0f;
	constexpr float kWithdrawTimedOutRadius = 15.0f;
	constexpr float kWithdrawTimeToForce = 10.0f;

	typedef const char*(__thiscall* t_checkpoint_name)(void* entities, uint32_t index);
	const auto checkpoint_name = reinterpret_cast<t_checkpoint_name>(DEVTOOLS_RELATIVE_ADDRESS(0x005223B0));
	typedef void(__thiscall* t_jump_to_checkpoint)(void* entities, uint32_t index);
	const auto jump_to_checkpoint = reinterpret_cast<t_jump_to_checkpoint>(DEVTOOLS_RELATIVE_ADDRESS(0x005224C0));
	typedef bool(__thiscall* t_is_saving)(void* saving);
	const auto is_saving = reinterpret_cast<t_is_saving>(DEVTOOLS_RELATIVE_ADDRESS(0x0037B630));

	typedef void(__thiscall* t_camera_call)(void* camera);
	const auto camera_activate = reinterpret_cast<t_camera_call>(DEVTOOLS_RELATIVE_ADDRESS(0x00028D40));
	const auto camera_deactivate = reinterpret_cast<t_camera_call>(DEVTOOLS_RELATIVE_ADDRESS(0x00028DE0));
	typedef void(__thiscall* t_camera_set_state)(void* camera, const void* state, bool both);
	const auto camera_set_state = reinterpret_cast<t_camera_set_state>(DEVTOOLS_RELATIVE_ADDRESS(0x00029280));
	typedef void(__thiscall* t_freeze)(void* character, bool frozen);
	const auto character_freeze = reinterpret_cast<t_freeze>(DEVTOOLS_RELATIVE_ADDRESS(0x0038AFD0));
	typedef void(__thiscall* t_unfreeze)(void* character);
	const auto character_unfreeze = reinterpret_cast<t_unfreeze>(DEVTOOLS_RELATIVE_ADDRESS(0x0037A270));

	typedef void(__thiscall* t_make_sensed)(void* message, int type, uint32_t entity, uint32_t composite, const float* position, float seenFraction, bool throughGlass, int threshold, float time, float step);
	const auto make_sensed = reinterpret_cast<t_make_sensed>(DEVTOOLS_RELATIVE_ADDRESS(0x000AB520));
	typedef void(__thiscall* t_post_sensed)(void* events, const void* message, bool now);
	const auto post_sensed = reinterpret_cast<t_post_sensed>(DEVTOOLS_RELATIVE_ADDRESS(0x000E1C20));

	typedef void(__thiscall* t_behaviour_call)(void* behaviour);
	const auto reset_senses_and_memory = reinterpret_cast<t_behaviour_call>(DEVTOOLS_RELATIVE_ADDRESS(0x0032F900));
	typedef void(__thiscall* t_reset_npc)(void* behaviour, bool menaceStartsPassive, bool resetSenseLimits);
	const auto reset_npc = reinterpret_cast<t_reset_npc>(DEVTOOLS_RELATIVE_ADDRESS(0x0032F910));
	typedef void(__thiscall* t_request_withdraw)(void* withdrawal, float initialRadius, float timedOutRadius, float timeToForce, bool permanent);
	const auto request_withdraw = reinterpret_cast<t_request_withdraw>(DEVTOOLS_RELATIVE_ADDRESS(0x002D0620));
	typedef void(__thiscall* t_withdrawal_call)(void* withdrawal);
	const auto cancel_withdraw = reinterpret_cast<t_withdrawal_call>(DEVTOOLS_RELATIVE_ADDRESS(0x003349D0));
	typedef void(__thiscall* t_withdrawal_flag)(void* withdrawal, bool permanent);
	const auto make_withdrawal_permanent = reinterpret_cast<t_withdrawal_flag>(DEVTOOLS_RELATIVE_ADDRESS(0x002C5EF0));
	typedef void(__thiscall* t_withdrawal_state)(void* withdrawal, int state);
	const auto set_withdraw_state = reinterpret_cast<t_withdrawal_state>(DEVTOOLS_RELATIVE_ADDRESS(0x0032FC20));
	typedef void(__thiscall* t_menace_reset)(void* menace, bool startPassive);
	const auto reset_menace = reinterpret_cast<t_menace_reset>(DEVTOOLS_RELATIVE_ADDRESS(0x002FC3F0));
	typedef void(__thiscall* t_menace_force)(void* menace, float through, bool resetGauges);
	const auto force_aggressive = reinterpret_cast<t_menace_force>(DEVTOOLS_RELATIVE_ADDRESS(0x002F42D0));
	const auto force_passive = reinterpret_cast<t_menace_force>(DEVTOOLS_RELATIVE_ADDRESS(0x002F4520));
	typedef void(__thiscall* t_set_flag)(void* character, int flag, bool value);
	const auto set_flag = reinterpret_cast<t_set_flag>(DEVTOOLS_RELATIVE_ADDRESS(0x00385B10));

	typedef void*(__thiscall* t_record_of)(const void* owner);

	uintptr_t Address(uintptr_t steam) { return DEVTOOLS_RELATIVE_ADDRESS(steam); }

	// ---- Reading the game (every read guarded) ----

	uint8_t* CharacterManager()
	{
		__try
		{
			const uintptr_t globalsAt = Address(kGameGlobals);
			if (!globalsAt || !*reinterpret_cast<const uint8_t*>(globalsAt + kCharactersValid))
				return nullptr;
			uint8_t* globals = *reinterpret_cast<uint8_t**>(globalsAt);
			return globals ? *reinterpret_cast<uint8_t**>(globals + kGlobalsCharacters) : nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	uint8_t* GlobalsField(uint32_t offset)
	{
		__try
		{
			const uintptr_t globalsAt = Address(kGameGlobals);
			uint8_t* globals = globalsAt ? *reinterpret_cast<uint8_t**>(globalsAt) : nullptr;
			return globals ? *reinterpret_cast<uint8_t**>(globals + offset) : nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	// The character with this unique id among those the game runs (null when it is gone)
	uint8_t* FindCharacter(uint32_t id)
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

	// The player's character (the first of the players)
	uint8_t* Player()
	{
		uint8_t* manager = CharacterManager();
		if (!manager)
			return nullptr;
		__try
		{
			uint8_t* const* entries = *reinterpret_cast<uint8_t* const* const*>(manager + kPlayers);
			const uint32_t count = *reinterpret_cast<const uint32_t*>(manager + kPlayers + 4);
			return entries && count ? entries[0] : nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	template <typename T>
	bool Read(const uint8_t* at, T& value)
	{
		__try
		{
			value = *reinterpret_cast<const volatile T*>(at);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	template <typename T>
	bool Write(uint8_t* at, T value)
	{
		__try
		{
			*reinterpret_cast<volatile T*>(at) = value;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// A pointer field of an object (null when unreadable)
	uint8_t* PointerAt(const uint8_t* object, uint32_t offset)
	{
		uint8_t* value = nullptr;
		return object && Read(object + offset, value) ? value : nullptr;
	}

	uint8_t* EntityManager()
	{
		const uintptr_t at = Address(kEntityManager);
		return at ? PointerAt(reinterpret_cast<const uint8_t*>(at), 0) : nullptr;
	}

	bool LevelRunning()
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

	// The running level's root composite id (0: no level)
	uint32_t LevelRoot()
	{
		if (!LevelRunning())
			return 0;
		uint32_t root = 0;
		const uint8_t* entities = EntityManager();
		return entities && Read(entities + kEntityRoot, root) ? root : 0;
	}

	// The tab's status line: what the last action on a game thread came to
	std::mutex g_statusMutex;
	std::string g_status;

	void SetStatus(const std::string& text)
	{
		std::lock_guard<std::mutex> lock(g_statusMutex);
		g_status = text;
		DevTools::Log("Debug menu: %s", text.c_str());
	}

	// ---- Debug checkpoints ----

	struct CheckpointView
	{
		uint32_t root = 0;    // the level (0: none)
		uint32_t count = 0;
		bool pending = false; // a checkpoint load or command waiting
	};

	CheckpointView ReadCheckpoints()
	{
		CheckpointView view;
		view.root = LevelRoot();
		const uint8_t* entities = EntityManager();
		if (!view.root || !entities)
			return view;
		const uint8_t* list = PointerAt(entities, kEntityCheckpoints);
		if (list)
			Read(list + kCheckpointCount, view.count);
		uint8_t loadPending = 0;
		uint32_t command = 0;
		Read(entities + kEntityLoadPending, loadPending);
		Read(entities + kEntityCommand, command);
		view.pending = loadPending != 0 || command != 0;
		return view;
	}

	// The names the entity thread read, for the level and count they were read for
	std::mutex g_namesMutex;
	std::vector<std::string> g_names;
	uint32_t g_namesRoot = 0;
	uint32_t g_namesCount = 0;
	std::atomic<bool> g_namesAsked = false;
	ULONGLONG g_jumpAskedAt = 0; // render thread: a jump was asked for then (the buttons wait a moment for it to show)

	// Entity thread: a checkpoint's name (its section name), copied out
	bool CheckpointName(uint8_t* entities, uint32_t index, char* out, size_t size)
	{
		__try
		{
			const char* name = checkpoint_name(entities, index);
			if (!name)
				return false;
			size_t i = 0;
			for (; i + 1 < size && name[i]; i++)
				out[i] = name[i];
			out[i] = '\0';
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// Entity thread: reads the names of the running level's checkpoints
	void ReadCheckpointNames()
	{
		const CheckpointView view = ReadCheckpoints();
		uint8_t* entities = EntityManager();
		std::vector<std::string> names;
		if (view.root && entities && checkpoint_name)
		{
			for (uint32_t i = 0; i < view.count && i < 256; i++)
			{
				char name[128];
				names.push_back(CheckpointName(entities, i, name, sizeof(name)) ? std::string(name) : std::string());
			}
		}
		std::lock_guard<std::mutex> lock(g_namesMutex);
		g_names.swap(names);
		g_namesRoot = view.root;
		g_namesCount = view.count;
		g_namesAsked = false;
	}

	enum class JumpResult { Jumped, LevelChanged, Gone, Pending, Saving, Failed };

	// Entity thread: hands the entity manager the checkpoint to load at its next tick
	JumpResult Jump(uint32_t root, uint32_t index)
	{
		const CheckpointView view = ReadCheckpoints();
		if (!view.root || view.root != root)
			return JumpResult::LevelChanged;
		if (index >= view.count)
			return JumpResult::Gone;
		if (view.pending)
			return JumpResult::Pending;
		uint8_t* entities = EntityManager();
		uint8_t* saving = GlobalsField(kGlobalsSaving);
		__try
		{
			if (saving && is_saving(saving))
				return JumpResult::Saving;
			jump_to_checkpoint(entities, index);
			return JumpResult::Jumped;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return JumpResult::Failed;
		}
	}

	void AskJump(uint32_t root, uint32_t index, const std::string& name)
	{
		DEBUG_MENU::OnEntityThread([root, index, name]()
		{
			char text[256];
			switch (Jump(root, index))
			{
			case JumpResult::Jumped: snprintf(text, sizeof(text), "jumping to debug checkpoint %u (%s)", index + 1, name.c_str()); break;
			case JumpResult::LevelChanged: snprintf(text, sizeof(text), "no jump: the level changed"); break;
			case JumpResult::Gone: snprintf(text, sizeof(text), "no jump: debug checkpoint %u is gone", index + 1); break;
			case JumpResult::Pending: snprintf(text, sizeof(text), "no jump: a checkpoint load is already pending"); break;
			case JumpResult::Saving: snprintf(text, sizeof(text), "no jump: the game is saving"); break;
			default: snprintf(text, sizeof(text), "the jump to debug checkpoint %u failed", index + 1); break;
			}
			SetStatus(text);
		});
	}

	// ---- Game speed ----

	// The slowest and fastest the slider and "game.speed" allow
	constexpr float kSpeedMin = 0.05f;
	constexpr float kSpeedMax = 4.0f;

	std::atomic<float> g_speed = 1.0f;
	float g_speedApplied = 1.0f; // render thread

	// Render thread, every frame: the time scale while it is not 1 (written every frame, as the game's own slow-motion
	// moments put the scale back to 1 when they end), and the game's own again once it is set back to 1
	void ApplySpeed()
	{
		const uintptr_t time = Address(kGameTime);
		if (!time)
			return;
		float speed = g_speed;
		if (!std::isfinite(speed) || speed < kSpeedMin || speed > kSpeedMax)
		{
			speed = 1.0f;
			g_speed = speed;
		}
		uint8_t* clock = reinterpret_cast<uint8_t*>(time);
		if (speed != 1.0f)
		{
			Write<double>(clock + kTimeScale, speed);
			Write<uint8_t>(clock + kTimeScaled, 1);
		}
		else if (g_speedApplied != 1.0f)
		{
			Write<double>(clock + kTimeScale, 1.0);
			Write<uint8_t>(clock + kTimeScaled, 0);
		}
		g_speedApplied = speed;
	}

	// ---- Free camera ----
	// The debug menu's cinematic camera (CINEMATIC_CAMERA.h, the Cinematic group's Free Camera tab) is the free
	// camera here: this switch and checkbox turn that camera on and off, and follow it when it is turned
	// on or off elsewhere (its key, its tab, or refused: no level, a character possessed).

	std::atomic<bool> g_freeCamera = false; // ticked
	bool g_freeCameraSeen = false;          // render thread: the tick as last brought in line

	// Render thread, every frame: the tick and the cinematic camera brought in line, whichever changed
	void ApplyFreeCamera()
	{
		static const std::string kCinematicCamera("cinematic.camera");
		bool cinematic = false;
		if (!DEBUG_MENU::GetSwitch(kCinematicCamera, cinematic))
			return;
		const bool ticked = g_freeCamera;
		if (ticked != g_freeCameraSeen)
		{
			DEBUG_MENU::SetSwitch(kCinematicCamera, ticked);
			g_freeCameraSeen = ticked;
		}
		else if (cinematic != ticked)
		{
			g_freeCamera = cinematic;
			g_freeCameraSeen = cinematic;
		}
	}

	// ---- AI actions ----

	// AI thread: has every alien hear the player moving, forced past its "activated" threshold
	int AlertAliens()
	{
		uint8_t* manager = CharacterManager();
		uint8_t* player = Player();
		const uintptr_t clockAt = Address(kGameTime);
		if (!manager || !player || !clockAt)
			return -1;
		alignas(16) uint8_t message[kMessageSize] = {};
		int alerted = 0;
		const uint32_t lists[] = { kPlayers, kAlive };
		__try
		{
			const uint8_t* clock = reinterpret_cast<const uint8_t*>(clockAt);
			const float time = static_cast<float>(*reinterpret_cast<const double*>(clock + kTimeNow));
			const float step = static_cast<float>(*reinterpret_cast<const double*>(clock + kTimeStep));
			const uint32_t* handle = reinterpret_cast<const uint32_t*>(player + kCharacterHandle);
			const float* position = reinterpret_cast<const float*>(player + kCharacterPosition);
			for (uint32_t list : lists)
			{
				uint8_t* const* entries = *reinterpret_cast<uint8_t* const* const*>(manager + list);
				const uint32_t count = *reinterpret_cast<const uint32_t*>(manager + list + 4);
				for (uint32_t i = 0; entries && i < count && i < kMaxPerList; i++)
				{
					uint8_t* character = entries[i];
					const uint8_t* spawnedAs = character ? *reinterpret_cast<uint8_t* const*>(character + kCharacterSpawnedAs) : nullptr;
					if (!spawnedAs || *reinterpret_cast<const int*>(spawnedAs + kSpawnedAsClass) != kAlienClass)
						continue;
					make_sensed(message, kMessageFakeMovementSound, handle[0], handle[1], position, 1.0f, false, kThresholdActivated, time, step);
					post_sensed(character + kCharacterEvents, message, false);
					alerted++;
				}
			}
			return alerted;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return -1;
		}
	}

	enum class Action { Withdraw, CancelWithdraw, Aggressive, Passive, ResetNpc, ResetSenses };

	// AI thread: one action on a character, through the game's own functions. False when the character has no part for it
	// (only the alien withdraws or menaces) or the call faulted.
	bool DoAction(uint8_t* character, Action action)
	{
		uint8_t* behaviour = character + kCharacterBehaviour;
		uint8_t* alien = PointerAt(character, kCharacterAlien);
		uint8_t* withdrawal = PointerAt(alien, kAlienWithdrawal);
		uint8_t* menace = PointerAt(character, kCharacterMenace);
		const bool memory = PointerAt(behaviour, kBehaviourMemory) != nullptr;
		__try
		{
			switch (action)
			{
			case Action::Withdraw:
				if (!withdrawal || !memory)
					return false;
				reset_senses_and_memory(behaviour);
				request_withdraw(withdrawal, kWithdrawInitialRadius, kWithdrawTimedOutRadius, kWithdrawTimeToForce, true);
				if (*reinterpret_cast<const int*>(withdrawal) == 0)
					set_withdraw_state(withdrawal, 1);
				return true;
			case Action::CancelWithdraw:
				if (!withdrawal)
					return false;
				set_withdraw_state(withdrawal, 0);
				cancel_withdraw(withdrawal);
				make_withdrawal_permanent(withdrawal, false);
				if (menace)
					reset_menace(menace, false);
				return true;
			case Action::Aggressive:
				if (!menace)
					return false;
				force_aggressive(menace, 0.0f, true);
				return true;
			case Action::Passive:
				if (!menace)
					return false;
				force_passive(menace, 0.0f, true);
				return true;
			case Action::ResetNpc:
				reset_npc(behaviour, true, true);
				return true;
			case Action::ResetSenses:
				if (!memory)
					return false;
				reset_senses_and_memory(behaviour);
				return true;
			}
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
		}
		return false;
	}

	const char* ActionName(Action action)
	{
		switch (action)
		{
		case Action::Withdraw: return "withdraw";
		case Action::CancelWithdraw: return "cancel the withdrawal";
		case Action::Aggressive: return "menace aggressive";
		case Action::Passive: return "menace passive";
		case Action::ResetNpc: return "reset";
		case Action::ResetSenses: return "reset senses and memory";
		default: return "?";
		}
	}

	void AskAction(uint32_t id, const std::string& name, Action action)
	{
		DEBUG_MENU::OnAiThread([id, name, action]()
		{
			uint8_t* character = FindCharacter(id);
			std::string text = std::string(ActionName(action)) + " - " + name + ": ";
			if (!character)
				text += "it is gone";
			else
				text += DoAction(character, action) ? "done" : "not done (it has no part for that)";
			SetStatus(text);
		});
	}

	// ---- Logic flags ----

	struct Flag
	{
		const char* name;    // after "tools.flag." in its dotted name
		const char* label;
		const char* tooltip;
		int flag;            // the game's number for it
	};

	const Flag kFlags[] = {
		{ "prevent_all_attacks", "Prevent all attacks", "Only matters on the player: humans and androids don't hit or grab them, and facehuggers don't leap at them. The alien ignores it.", 0x1F },
		{ "prevent_grapples", "Prevent grapples", "Only matters on the player: humans and androids don't grab and strangle them. The alien ignores it.", 0x1E },
		{ "block_ambush_and_killtraps", "Block ambushes and kill traps", "The alien doesn't lie in wait or spring kill traps.", 0x1D },
		{ "never_aggressive", "Never aggressive", "It never turns aggressive.", 0x19 },
		{ "totally_blind_in_dark", "Totally blind in the dark", "It sees nothing in the dark.", 0x14 },
		{ "lock_backstage_stalk", "Lock backstage stalk", "The alien keeps stalking from backstage (in the vents). A permanent withdrawal sets this too.", 0x13 },
		{ "should_breakout", "Should break out", "Tells its behaviour tree to break out.", 0x09 },
		{ "should_attack", "Should attack", "Tells its behaviour tree to attack.", 0x0A },
		{ "should_hit_and_run", "Should hit and run", "Tells its behaviour tree to make a hit-and-run attack.", 0x0B },
		{ "should_ambush", "Should ambush", "Tells its behaviour tree to lie in wait.", 0x18 },
		{ "ignore_player_in_vent", "Ignore the player in vents", "It leaves out its behaviour for a player in a vent.", 0x21 },
		{ "always_knows_in_vent", "Always knows when in a vent", "The alien always knows when the player is in a vent.", 0x0F },
		{ "mute_dynamic_dialogue", "Mute dynamic dialogue", "It says none of its unscripted lines.", 0x1A },
	};
	constexpr size_t kFlagCount = sizeof(kFlags) / sizeof(kFlags[0]);

	// The flags as the window shows them, for one character: what is wanted (ticked, or set from outside), what was last
	// shown, and a change asked of the AI thread that hasn't been done yet
	std::atomic<bool> g_flagWanted[kFlagCount];
	bool g_flagShown[kFlagCount] = {};
	uint32_t g_flagAsked[kFlagCount] = {};     // render thread: the ticket of the change waiting (0: none)
	std::atomic<uint32_t> g_flagDone[kFlagCount];
	uint32_t g_nextTicket = 1;
	uint32_t g_flagCharacter = 0;              // whose flags they are (0: nobody's)
	std::string g_flagCharacterName;

	bool ReadFlag(const uint8_t* character, int flag, bool& value)
	{
		uint8_t byte = 0;
		if (!Read(character + kCharacterFlags + flag, byte))
			return false;
		value = byte != 0;
		return true;
	}

	bool SetFlag(uint8_t* character, int flag, bool value)
	{
		__try
		{
			set_flag(character, flag, value);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// The character the AI actions and flags act on: the selected one, unless that is nobody or the player, then the
	// first alien
	const DEBUG_MENU::Character* Target()
	{
		const DEBUG_MENU::Character* selected = DEBUG_MENU::Selected();
		if (selected && !selected->player)
			return selected;
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
			if (character.characterClass == kAlienClass && !character.dead)
				return &character;
		return nullptr;
	}

	const DEBUG_MENU::Character* FirstAlien()
	{
		const DEBUG_MENU::Character* target = Target();
		if (target && target->characterClass == kAlienClass)
			return target;
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
			if (character.characterClass == kAlienClass && !character.dead)
				return &character;
		return nullptr;
	}

	// Render thread, every frame: the flags follow the target character's own, and a flag ticked in the window or set from
	// outside is handed to the AI thread to set (the box keeps what was asked until that is done)
	void ApplyFlags()
	{
		const DEBUG_MENU::Character* target = Target();
		const uint32_t id = target ? target->id : 0;
		if (id != g_flagCharacter)
		{
			g_flagCharacter = id;
			g_flagCharacterName = target ? target->name : std::string();
			for (size_t i = 0; i < kFlagCount; i++)
			{
				bool value = false;
				if (target)
					ReadFlag(target->object, kFlags[i].flag, value);
				g_flagShown[i] = value;
				g_flagWanted[i] = value;
				g_flagAsked[i] = 0;
			}
			return;
		}
		if (!target)
			return;
		for (size_t i = 0; i < kFlagCount; i++)
		{
			if (g_flagAsked[i] && g_flagDone[i] != g_flagAsked[i])
				continue;
			g_flagAsked[i] = 0;
			const bool wanted = g_flagWanted[i];
			if (wanted != g_flagShown[i])
			{
				const uint32_t ticket = g_nextTicket++;
				g_flagAsked[i] = ticket;
				g_flagShown[i] = wanted;
				const int flag = kFlags[i].flag;
				const std::string name = target->name;
				DEBUG_MENU::OnAiThread([id, i, flag, wanted, ticket, name]()
				{
					uint8_t* character = FindCharacter(id);
					const bool done = character && SetFlag(character, flag, wanted);
					g_flagDone[i] = ticket;
					SetStatus(std::string(kFlags[i].label) + (wanted ? " on" : " off") + " - " + name + (done ? "" : ": not done (it is gone)"));
				});
				continue;
			}
			bool value = false;
			if (ReadFlag(target->object, kFlags[i].flag, value))
			{
				g_flagShown[i] = value;
				g_flagWanted[i] = value;
			}
		}
	}

	// ---- Player cheats ----

	std::atomic<bool> g_invisible = false;
	std::atomic<bool> g_invisibleButAudible = false;
	std::atomic<bool> g_infiniteAmmo = false;

	// Invisible but audible keeps back only the senses that see: sight itself and the player's torch light seen. The rest
	// get through: weapon and movement sounds, damage, touch and flame. So does the combined sense: every sense also feeds a
	// character's combined sense under its own kind, so the combined kind itself only comes from a squadmate passing on what
	// it sensed - which, with sight kept back, it heard or felt.
	constexpr int kSenseSight = 0;
	constexpr int kSenseTorchLight = 6;
	// Since the game started: the player's senses Invisible but audible kept back, and let through
	std::atomic<uint32_t> g_sightKeptBack = 0;
	std::atomic<uint32_t> g_othersLetThrough = 0;

	bool SightSense(int sense)
	{
		return sense == kSenseSight || sense == kSenseTorchLight;
	}

	// Whether a sense activation is about to pick up the player
	bool SensesPlayer(const void* activation)
	{
		__try
		{
			const uint8_t* bytes = static_cast<const uint8_t*>(activation);
			if (!bytes[kActivationOfCharacter])
				return false;
			const uint8_t* owner = *reinterpret_cast<uint8_t* const*>(bytes + kActivationOwner);
			if (!owner)
				return false;
			const auto record_of = *reinterpret_cast<const t_record_of*>(*reinterpret_cast<const uintptr_t*>(owner) + kRecordFunction);
			const uint8_t* record = static_cast<const uint8_t*>(record_of(owner));
			const uint8_t* sensed = record ? *reinterpret_cast<uint8_t* const*>(record + kRecordCharacter) : nullptr;
			const uint8_t* playable = sensed ? *reinterpret_cast<uint8_t* const*>(sensed + kCharacterPlayable) : nullptr;
			return playable && playable[kPlayableControlled];
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// Whether a weapon is the played character's
	bool PlayersWeapon(const void* weapon)
	{
		__try
		{
			const uint8_t* owner = *reinterpret_cast<uint8_t* const*>(static_cast<const uint8_t*>(weapon) + kWeaponOwner);
			const uint8_t* spawnedAs = owner ? *reinterpret_cast<uint8_t* const*>(owner + kCharacterSpawnedAs) : nullptr;
			return spawnedAs && spawnedAs[kSpawnedAsPlayer] && spawnedAs[kSpawnedAsLocal];
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// ---- The window ----

	bool g_registered = false;

	void RegisterSwitches()
	{
		if (g_registered)
			return;
		g_registered = true;
		DEBUG_MENU::AddValue("game.speed", &g_speed, kSpeedMin, kSpeedMax);
		DEBUG_MENU::AddSwitch("tools.free_camera", &g_freeCamera);
		DEBUG_MENU::AddSwitch("tools.invisible", &g_invisible);
		DEBUG_MENU::AddSwitch("tools.invisible_but_audible", &g_invisibleButAudible);
		DEBUG_MENU::AddCommand("tools.invisible_status", "Invisible to the AI and Invisible but audible: on or off, then (since the game started) the senses of the player Invisible but audible kept back and let through", [](const std::string&)
		{
			char text[160];
			snprintf(text, sizeof(text), "invisible=%d\ninvisible_but_audible=%d\nsight_kept_back=%u\nothers_let_through=%u\n", g_invisible ? 1 : 0, g_invisibleButAudible ? 1 : 0,
				g_sightKeptBack.load(), g_othersLetThrough.load());
			return DEBUG_MENU::CommandResult{ true, text };
		});
		DEBUG_MENU::AddSwitch("tools.infinite_ammo", &g_infiniteAmmo);
		for (size_t i = 0; i < kFlagCount; i++)
			DEBUG_MENU::AddSwitch(std::string("tools.flag.") + kFlags[i].name, &g_flagWanted[i]);
	}

	// Render thread, every frame (the window open or not)
	void Apply()
	{
		RegisterSwitches();
		ApplySpeed();
		ApplyFreeCamera();
		ApplyFlags();
	}

	void ApplyOverlay(ImDrawList* /*drawList*/)
	{
		Apply();
	}

	void Tooltip(const char* text)
	{
		if (!ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip | ImGuiHoveredFlags_AllowWhenDisabled))
			return;
		ImGui::BeginTooltip();
		ImGui::PushTextWrapPos(ImGui::GetFontSize() * 28.0f);
		ImGui::TextUnformatted(text);
		ImGui::PopTextWrapPos();
		ImGui::EndTooltip();
	}

	// A line of prose in grey, wrapped at the window's right edge rather than running past it
	void Hint(const char* text)
	{
		ImGui::PushTextWrapPos(0.0f);
		ImGui::TextDisabled("%s", text);
		ImGui::PopTextWrapPos();
	}

	// Whether this build has the game function (or hook) at all
	template <typename Function>
	bool Available(Function function)
	{
		return function != nullptr;
	}

	void DrawDebugCheckpoints()
	{
		ImGui::SeparatorText("Debug checkpoints");
		const CheckpointView view = ReadCheckpoints();
		if (!view.root)
		{
			ImGui::TextDisabled("No level running.");
			return;
		}
		if (!Available(checkpoint_name) || !Available(jump_to_checkpoint) || !Available(is_saving))
		{
			ImGui::TextDisabled("Not available on this build of the game.");
			return;
		}
		if (view.count == 0)
		{
			ImGui::TextWrapped("This level has no debug checkpoints active. They add themselves to the list when their level part activates them; levels with some include SCI_ANDROIDLAB, TECH_COMMS, HAB_AIRPORT, SCI_HUB, TECH_HUB, ENG_TOWPLATFORM and TECH_RND_HZDLAB.");
			return;
		}

		std::vector<std::string> names;
		{
			std::lock_guard<std::mutex> lock(g_namesMutex);
			if (g_namesRoot == view.root && g_namesCount == view.count)
				names = g_names;
		}
		if (names.size() != view.count && !g_namesAsked)
		{
			g_namesAsked = true;
			DEBUG_MENU::OnEntityThread(ReadCheckpointNames);
		}

		ImGui::PushStyleColor(ImGuiCol_Text, ImGui::GetStyleColorVec4(ImGuiCol_TextDisabled));
		ImGui::TextWrapped("Most of these start a part of a mission. A part that already ran since the level loaded doesn't start again: jumping to it leaves the screen black (load the level again first).");
		ImGui::PopStyleColor();
		Tooltip("A mission part's start spawns the player at its spawn point and fades in once the part has loaded, which it does once per level load. The level's own start runs a part too (with no progress, its ambient part). Load the level again to jump to that part.");
		const bool waiting = view.pending || GetTickCount64() - g_jumpAskedAt < 1500;
		if (view.pending)
			ImGui::TextDisabled("A checkpoint load is pending...");
		ImGui::BeginDisabled(waiting);
		for (uint32_t i = 0; i < view.count; i++)
		{
			ImGui::PushID(static_cast<int>(i));
			// Once the names are read, an empty one is a checkpoint the level's scripts gave no name (or one not readable)
			const std::string name = i < names.size() ? (names[i].empty() ? std::string("(no name)") : names[i]) : std::string("(name not read yet)");
			if (ImGui::SmallButton("Jump"))
			{
				g_jumpAskedAt = GetTickCount64();
				AskJump(view.root, i, name);
			}
			GAME_DEBUG::ListNumber(i + 1, view.count);
			ImGui::TextUnformatted(name.c_str());
			ImGui::PopID();
		}
		ImGui::EndDisabled();
	}

	// The debug checkpoints, then every checkpoint of the level and the reload (GAME_DEBUG.h), then the snapshot slots
	// (GAME_SNAPSHOTS.h)
	void DrawCheckpoints()
	{
		DrawDebugCheckpoints();
		GAME_DEBUG::DrawLevelCheckpoints();
		GAME_SNAPSHOTS::DrawSlots();
	}

	void DrawSpeed()
	{
		ImGui::SeparatorText("Game speed and pause");
		GAME_DEBUG::DrawTimeControls();
		if (!Address(kGameTime))
		{
			ImGui::TextDisabled("Game speed: not available on this build of the game.");
			return;
		}
		float speed = g_speed;
		ImGui::SetNextItemWidth(260.0f * DEBUG_MENU::Scale());
		if (ImGui::SliderFloat("##speed", &speed, kSpeedMin, kSpeedMax, "%.2fx", ImGuiSliderFlags_Logarithmic | ImGuiSliderFlags_AlwaysClamp))
			g_speed = speed;
		ImGui::SameLine();
		ImGui::BeginDisabled(speed == 1.0f);
		if (ImGui::Button("Reset##speed"))
			g_speed = 1.0f;
		ImGui::EndDisabled();
		Hint("The game clock's own time scale: everything the game times runs slower or faster.");
		Tooltip("Each frame moves the game on by the real time that passed times this. The game's own slow-motion moments use the same scale, and are overridden while it isn't 1. Cap FPS still paces the frames in real time.");
	}

	void DrawFreeCamera()
	{
		ImGui::SeparatorText("Free camera");
		bool on = g_freeCamera;
		if (ImGui::Checkbox("Free camera", &on))
			g_freeCamera = on;
		Tooltip("The Cinematic group's free camera: starts where the game camera is, the player frozen meanwhile. Close this window to fly it (the window keeps the keyboard and mouse while open); its key (CinematicCameraKey, F8 by default) turns it on and off, with this window open or closed. A level change turns it off.");
		ImGui::SameLine();
		if (ImGui::SmallButton("Free Camera tab"))
			DEBUG_MENU::ShowTab("Free Camera");
		Hint("W/A/S/D fly, the mouse turns it, Space/Ctrl up and down, Shift faster; a gamepad flies it too.");
	}

	void DrawAi()
	{
		ImGui::SeparatorText("AI");
		DEBUG_MENU::CharacterPicker("Character");
		const DEBUG_MENU::Character* target = Target();
		const DEBUG_MENU::Character* alien = FirstAlien();

		const bool canAlert = Available(make_sensed) && Available(post_sensed);
		ImGui::BeginDisabled(!canAlert || !alien);
		if (ImGui::Button("Alert the alien to the player"))
			DEBUG_MENU::OnAiThread([]()
			{
				const int alerted = AlertAliens();
				SetStatus(alerted < 0 ? "the alert failed" : alerted == 0 ? "no alien to alert" : "the alien heard the player");
			});
		ImGui::EndDisabled();
		Tooltip("Every alien hears the player moving, forced past its \"activated\" threshold: it comes to the player's position.");

		if (alien)
		{
			int withdrawal = -1, aggressive = -1;
			const uint8_t* alienData = PointerAt(alien->object, kCharacterAlien);
			const uint8_t* withdrawalAt = PointerAt(alienData, kAlienWithdrawal);
			const uint8_t* menaceAt = PointerAt(alien->object, kCharacterMenace);
			if (withdrawalAt)
				Read(withdrawalAt, withdrawal);
			if (menaceAt)
				Read(menaceAt + kMenaceAggressive, aggressive);
			const char* withdrawalText = withdrawal == 0 ? "not withdrawing" : withdrawal == 1 ? "needs to withdraw" : withdrawal == 2 ? "withdrawing" : "?";
			const char* menaceText = aggressive == 1 ? "aggressive" : aggressive == 0 ? "passive" : "?";
			ImGui::TextWrapped("Alien: %s - %s, menace %s", alien->name.c_str(), withdrawalText, menaceText);

			const uint32_t id = alien->id;
			const std::string name = alien->name;
			const bool canWithdraw = Available(request_withdraw) && Available(cancel_withdraw) && Available(make_withdrawal_permanent) &&
				Available(set_withdraw_state) && Available(reset_senses_and_memory) && Available(reset_menace);
			ImGui::BeginDisabled(!canWithdraw);
			if (ImGui::Button("Withdraw"))
				AskAction(id, name, Action::Withdraw);
			Tooltip("The alien forgets what it sensed and withdraws into the vents for good (until cancelled), as the level scripts' withdraw entity has it: at 5 m from where it can leave, giving up at 15 m, forced after 10 s.");
			ImGui::SameLine();
			if (ImGui::Button("Cancel withdrawal"))
				AskAction(id, name, Action::CancelWithdraw);
			Tooltip("Ends the withdrawal and resets the alien's menace, as the level scripts' withdraw entity does when cancelled.");
			ImGui::EndDisabled();

			ImGui::BeginDisabled(!Available(force_aggressive) || !Available(force_passive));
			if (ImGui::Button("Menace aggressive"))
				AskAction(id, name, Action::Aggressive);
			Tooltip("Forces the alien's menace into its aggressive phase from the start, gauges reset: it hunts the player more closely.");
			ImGui::SameLine();
			if (ImGui::Button("Menace passive"))
				AskAction(id, name, Action::Passive);
			Tooltip("Forces the alien's menace into its passive phase from the start, gauges reset: it backs off from the player.");
			ImGui::EndDisabled();
		}
		else
		{
			ImGui::TextDisabled("No alien in this level.");
		}

		if (target)
		{
			ImGui::Text("Character: %s (%s)", target->name.c_str(), DEBUG_MENU::ClassName(target->characterClass));
			const uint32_t id = target->id;
			const std::string name = target->name;
			ImGui::BeginDisabled(!Available(reset_npc) || !Available(reset_senses_and_memory) || target->dead);
			if (ImGui::Button("Reset"))
				AskAction(id, name, Action::ResetNpc);
			Tooltip("Resets it as the level scripts' reset entity does: its senses and memory, logic flags and timers (an alien's menace too).");
			ImGui::SameLine();
			if (ImGui::Button("Reset senses and memory"))
				AskAction(id, name, Action::ResetSenses);
			Tooltip("It forgets everything it has sensed (the player included).");
			ImGui::EndDisabled();

			ImGui::TextDisabled("Logic flags (%s):", g_flagCharacterName.c_str());
			ImGui::BeginDisabled(!Available(set_flag));
			for (size_t i = 0; i < kFlagCount; i++)
			{
				if (i % 2 == 1)
					ImGui::SameLine(280.0f * DEBUG_MENU::Scale());
				bool value = g_flagWanted[i];
				char label[96];
				snprintf(label, sizeof(label), "%s##flag%u", kFlags[i].label, static_cast<unsigned>(i));
				if (ImGui::Checkbox(label, &value))
					g_flagWanted[i] = value;
				Tooltip(kFlags[i].tooltip);
			}
			ImGui::EndDisabled();
		}
		else
		{
			Hint("Select a character (not the player) for the reset and the logic flags.");
		}
	}

	// The switches are shown with the other player cheats, in Characters > Player (by their names)
	void DrawPlayer()
	{
		ImGui::SeparatorText("Player");
		Hint("Invisible to the AI, infinite ammo and the other player cheats are in Characters > Player.");
	}

	void DrawTab()
	{
		Apply();
		DrawCheckpoints();
		DrawSpeed();
		DrawFreeCamera();
		DrawAi();
		DrawPlayer();

		ImGui::SeparatorText("Havok Visual Debugger");
		ImGui::TextWrapped("The game still serves Havok's Visual Debugger on localhost port 25001 while a level runs: a Havok 2012.2 Visual Debugger client connected to it shows the physics world (collision, rigid bodies) live.");

		std::lock_guard<std::mutex> lock(g_statusMutex);
		if (!g_status.empty())
		{
			ImGui::Separator();
			Hint(("Last: " + g_status).c_str());
		}
	}

	const bool g_tabAdded = DEBUG_MENU::AddTab("Tools", 80, DrawTab, "Game");
	const bool g_overlayAdded = DEBUG_MENU::AddOverlay(6, ApplyOverlay);
}

__declspec(noinline)
bool __fastcall DEBUG_ACTIONS::h_trigger_sense(void* _this, void* /*_EDX*/, const void* position, float amount, float distance, int sense, int modifier, int threshold, bool flagA, bool flagB, float time)
{
	if (g_invisible && SensesPlayer(_this))
		return false;
	// Invisible but audible: only the senses that see are kept from the player
	if (g_invisibleButAudible && SensesPlayer(_this))
	{
		if (SightSense(sense))
		{
			g_sightKeptBack++;
			return false;
		}
		g_othersLetThrough++;
	}
	return trigger_sense(_this, position, amount, distance, sense, modifier, threshold, flagA, flagB, time);
}

__declspec(noinline)
void __fastcall DEBUG_ACTIONS::h_consume_ammo(void* _this, void* /*_EDX*/, int amount)
{
	if (g_infiniteAmmo && PlayersWeapon(_this))
		return;
	consume_ammo(_this, amount);
}

#include "GAME_DEBUG.h"
#include "GAME_LEVEL_MANAGER.h"
#include "GAME_SAVES.h"
#include "DEBUG_MENU.h"
#include "LIVE_DEBUGGER.h"
#include "LIVE_LINK.h"
#include "PLAYER_DEBUG.h"

#include <algorithm>
#include <atomic>
#include <cctype>
#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <mutex>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <imgui.h>

/*
	Addresses and layouts are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).
	Each change to the game is made as the shipped game makes it, on the thread it makes it on:
	- the pause and the step are the game's own pause bits, set and cleared on the entity thread as the level scripts'
	  message entities pause the game (the pause's own update then turns them into the frame's pause, once a frame);
	- a checkpoint jump is what the entity manager's own jump by name does once it has found the checkpoint (the checkpoint
	  handed to it and the jump command set; its tick does the rest, as for the debug checkpoints), made by the checkpoint's
	  place in the list, so that one with no name, or with a name an earlier one has too, can be jumped to;
	- the reload is what the game's restart from a checkpoint does, as the scripts' debug load-checkpoint entity calls it:
	  only with a checkpoint in memory and no load already pending, the load marked pending and the flow's restart mode
	  set to "checkpoint" (its update then restarts from the checkpoint in memory, as after a death);
	- the difficulty is set with the game's own setter (as the pause menu sets it), on the AI thread, which tells every
	  character;
	- a level load is Live Link's own (the level manager's look-up by name, queue and request), on the render thread.
*/
namespace
{
	// Where the game keeps its pointer to its globals, and in them: the level manager, the game's flow, the saving and the
	// difficulty (pointers)
	constexpr uintptr_t kGameGlobals = 0x012F0C88;
	constexpr uint32_t kGlobalsLevelManager = 0x3C;
	constexpr uint32_t kGlobalsGameFlow = 0x48;
	constexpr uint32_t kGlobalsSaving = 0x78;
	constexpr uint32_t kGlobalsDifficulty = 0x88;

	// The game's flow: its state, its set-up state, a restart asked for, the play mode and a level waiting to load (text)
	constexpr uint32_t kFlowState = 0x08;
	constexpr uint32_t kFlowSetup = 0x0C;
	constexpr uint32_t kFlowRestart = 0x10;
	constexpr uint32_t kFlowPlayMode = 0x14;
	constexpr uint32_t kFlowPendingLevel = 0x48;
	constexpr uint32_t kPendingLevelLength = 0x32;
	constexpr int kStateGameplay = 4;
	constexpr int kStateGameOver = 5;
	constexpr int kRestartCheckpoint = 2;
	const char* const kFlowStates[] = { "starting up", "starting up (waiting)", "menu", "starting gameplay", "gameplay", "game over", "restarting", "unloading" };
	const char* const kRestartModes[] = { "none", "the level", "a checkpoint", "after a death" };

	// The level manager: the level being played (a pointer: its index into the level names at +4, its display name at +8),
	// the levels it knows (pointers) and how many
	constexpr uint32_t kManagerCurrent = 0x40;
	constexpr uint32_t kManagerLevels = 0x50;
	constexpr uint32_t kManagerLevelCount = 0x2D0;
	constexpr uint32_t kMaxLevels = 160;
	constexpr uint32_t kLevelIndex = 0x04;
	constexpr uint32_t kLevelDisplayName = 0x08;
	constexpr uint32_t kDisplayNameLength = 128;

	// The level names (a pointer to a pointer to them): a record each of its name (as the game loads it, e.g.
	// "PRODUCTION\BSP_TORRENS") then its type, how many, and the one being played
	constexpr uintptr_t kCommonGlobals = 0x017E1C4C;
	constexpr uint32_t kLevelNameSize = 0x46;
	constexpr uint32_t kLevelNameLength = 0x32;
	constexpr uint32_t kLevelTypeOffset = 0x32;
	constexpr uint32_t kLevelTypeLength = 0x14;
	constexpr uint32_t kLevelNameCount = 0x2BC0;

	// The entity manager (a pointer to it), and its fields: the running level's root composite id, the level's checkpoints
	// and its debug checkpoints (lists: a pointer, their count at +8 and their entries at +0xC), the checkpoint the level is
	// at (an entity reference), a save waiting (an entity handle: two ids), the command waiting for its next tick, the
	// script transport, the player's spawn point (a matrix: its position at +0xC, +0x1C and +0x2C) and whether it is set,
	// and a checkpoint load pending
	constexpr uintptr_t kEntityManager = 0x0134EF40;
	constexpr uint32_t kEntityRoot = 0x0C;
	constexpr uint32_t kEntityCheckpoints = 0x3C;
	constexpr uint32_t kEntityDebugCheckpoints = 0x40;
	constexpr uint32_t kListCount = 0x08;
	constexpr uint32_t kListEntries = 0x0C;
	constexpr uint32_t kEntityCurrentCheckpoint = 0x50;
	constexpr uint32_t kEntityPendingSave = 0x80;
	constexpr uint32_t kEntityCommand = 0xA4;
	constexpr uint32_t kEntityTransport = 0xBC;
	constexpr uint32_t kEntitySpawn = 0xC8;
	constexpr uint32_t kEntitySpawnValid = 0x107;
	constexpr uint32_t kEntityLoadPending = 0x10E;
	const char* const kTransports[] = { "stopped at a breakpoint", "paused", "pause menu", "stepping a cycle", "stepping a frame", "running" };
	const char* const kCommands[] = { "none", "reset the level", "restart the level", "restart the checkpoint", "jump to a checkpoint", "pause the level" };
	constexpr int kCommandJump = 4;

	// The pause (a reference: a pointer to it): the pause bits asked for, then those in force this frame
	constexpr uintptr_t kPauseMode = 0x012F194C;
	constexpr uint32_t kPauseAsked = 0x00;
	constexpr uint32_t kPauseNow = 0x04;
	constexpr uint32_t kPauseLiveLink = 0x100;       // the pause bit the game keeps for an editor connection: ours
	constexpr uint32_t kPauseSingleFrame = 0x20;     // let one frame through
	constexpr uint32_t kPauseMenuOpen = 0x400;       // the pause menu
	constexpr uint32_t kTimePausing = 0x179FF;       // the bits that stop the game clock
	struct PauseBit { uint32_t bit; const char* name; };
	const PauseBit kPauseBits[] = {
		{ 0x1, "game time" }, { 0x2, "game over" }, { 0x4, "network" }, { 0x8, "system" }, { 0x10, "movie" }, { 0x20, "frame step" },
		{ 0x40, "cycle step" }, { 0x80, "breakpoint" }, { 0x100, "debug menu (editor pause)" }, { 0x400, "pause menu" }, { 0x800, "for a time" },
		{ 0x1000, "operating system" }, { 0x2000, "controller" }, { 0x4000, "warning" }, { 0x8000, "front end" }, { 0x10000, "system UI" },
	};

	// The checkpoint in memory (a pointer to it): a death or a reload goes back to it. Its level, its checkpoint (an entity
	// handle), its mission, the game time it was captured at (a double), the deaths in a row and in all, and whether it is
	// only a mission's temporary state
	constexpr uintptr_t kLevelSnapshot = 0x0134EF84;
	constexpr uint32_t kSnapshotLevel = 0x20;
	constexpr uint32_t kSnapshotLevelLength = 0x80;
	constexpr uint32_t kSnapshotCheckpoint = 0x240;
	constexpr uint32_t kSnapshotMission = 0x280;
	constexpr uint32_t kSnapshotTime = 0x288;
	constexpr uint32_t kSnapshotDeathsInARow = 0x290;
	constexpr uint32_t kSnapshotDeaths = 0x294;
	constexpr uint32_t kSnapshotTemporary = 0x29A;

	// The game clock: the game time now (a double)
	constexpr uintptr_t kGameTime = 0x012410A8;

	// The difficulty: its settings by difficulty (a pointer to their pointers; none for a difficulty without settings), the
	// difficulty in force (its handle: the difficulty's number) and its number, the lowest used this campaign (what its
	// difficulty achievements go by) and whether it was ever changed
	constexpr uint32_t kDifficultySettings = 0x08;
	constexpr uint32_t kDifficultyHandle = 0x24;
	constexpr uint32_t kDifficultyValue = 0x28;
	constexpr uint32_t kDifficultyLowest = 0x2C;
	constexpr uint32_t kDifficultyChanged = 0x30;
	// The game's numbers for them, and the order the game's menus show them in
	enum Difficulty { Easy = 0, Medium = 1, Hard = 2, Nightmare = 3, Novice = 4, kDifficulties = 5 };
	const char* const kDifficultyNames[kDifficulties] = { "Easy", "Medium", "Hard", "Nightmare", "Novice" };
	const int kDifficultyOrder[kDifficulties] = { Novice, Easy, Medium, Hard, Nightmare };

	typedef const char*(__thiscall* t_checkpoint_name)(void* entities, uint32_t index);
	const auto checkpoint_name = reinterpret_cast<t_checkpoint_name>(DEVTOOLS_RELATIVE_ADDRESS(0x00522220));
	const auto debug_checkpoint_name = reinterpret_cast<t_checkpoint_name>(DEVTOOLS_RELATIVE_ADDRESS(0x005223B0));
	typedef bool(__thiscall* t_jump_to_named)(void* entities, const char* name);
	const auto jump_to_named_checkpoint = reinterpret_cast<t_jump_to_named>(DEVTOOLS_RELATIVE_ADDRESS(0x00522330));
	typedef void(__thiscall* t_jump_to_debug)(void* entities, uint32_t index);
	const auto jump_to_debug_checkpoint = reinterpret_cast<t_jump_to_debug>(DEVTOOLS_RELATIVE_ADDRESS(0x005224C0));
	// The game's copy of a counted reference to an entity (the old one let go, the new one counted), as its jump uses it to
	// hand the entity manager the checkpoint to load
	typedef void*(__thiscall* t_assign_reference)(void* reference, const void* from);
	const auto assign_entity_reference = reinterpret_cast<t_assign_reference>(DEVTOOLS_RELATIVE_ADDRESS(0x000673C0));
	typedef bool(__thiscall* t_is_saving)(void* saving);
	const auto is_saving = reinterpret_cast<t_is_saving>(DEVTOOLS_RELATIVE_ADDRESS(0x0037B630));
	typedef bool(__cdecl* t_snapshot_valid)();
	const auto snapshot_valid = reinterpret_cast<t_snapshot_valid>(DEVTOOLS_RELATIVE_ADDRESS(0x00533A10));
	const auto snapshot_valid_normal = reinterpret_cast<t_snapshot_valid>(DEVTOOLS_RELATIVE_ADDRESS(0x00533A80));
	typedef void(__thiscall* t_set_difficulty)(void* difficulty, const int* handle);
	const auto set_difficulty = reinterpret_cast<t_set_difficulty>(DEVTOOLS_RELATIVE_ADDRESS(0x002DCC30));

	uintptr_t Address(uintptr_t steam) { return DEVTOOLS_RELATIVE_ADDRESS(steam); }

	// ---- Reading the game (every read guarded) ----

	template <typename T>
	bool Read(const void* at, T& value)
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
	bool Write(void* at, T value)
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

	uint8_t* PointerAt(const void* object, uint32_t offset)
	{
		uint8_t* value = nullptr;
		return object && Read(static_cast<const uint8_t*>(object) + offset, value) ? value : nullptr;
	}

	// A pointer the game keeps at a fixed place (null when this build has no address for it)
	uint8_t* Global(uintptr_t steam)
	{
		const uintptr_t at = Address(steam);
		return at ? PointerAt(reinterpret_cast<const void*>(at), 0) : nullptr;
	}

	uint8_t* GlobalsField(uint32_t offset)
	{
		return PointerAt(Global(kGameGlobals), offset);
	}

	// Text in the game's memory, copied out (printable characters only)
	void CopyText(const void* at, char* out, size_t size, size_t limit)
	{
		out[0] = '\0';
		__try
		{
			const char* text = static_cast<const char*>(at);
			size_t i = 0;
			for (; i + 1 < size && i < limit && text[i]; i++)
				out[i] = text[i] >= 32 && text[i] < 127 ? text[i] : '?';
			out[i] = '\0';
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			out[0] = '\0';
		}
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

	uint8_t* EntityManager()
	{
		return Global(kEntityManager);
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

	bool CallBool(t_snapshot_valid function)
	{
		__try
		{
			return function && function();
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	double GameTime()
	{
		double time = 0.0;
		const uintptr_t at = Address(kGameTime);
		return at && Read(reinterpret_cast<const void*>(at), time) ? time : 0.0;
	}

	// The level names' record for a level number (null when there is none)
	const uint8_t* LevelNameRecord(uint32_t index)
	{
		const uint8_t* names = PointerAt(Global(kCommonGlobals), 0);
		uint32_t count = 0;
		if (!names || !Read(names + kLevelNameCount, count) || index >= count || index >= kMaxLevels)
			return nullptr;
		return names + index * kLevelNameSize;
	}

	struct LevelEntry
	{
		std::string name;      // as the game loads it
		std::string type;
		std::string display;
		bool current = false;
	};

	// Every level the level manager knows, as the game would load it
	std::vector<LevelEntry> ReadLevels()
	{
		std::vector<LevelEntry> levels;
		const uint8_t* manager = GlobalsField(kGlobalsLevelManager);
		int count = 0;
		if (!manager || !Read(manager + kManagerLevelCount, count) || count <= 0)
			return levels;
		const uint8_t* current = PointerAt(manager, kManagerCurrent);
		for (int i = 0; i < count && i < static_cast<int>(kMaxLevels); i++)
		{
			const uint8_t* level = PointerAt(manager, kManagerLevels + i * 4);
			uint32_t index = 0;
			if (!level || !Read(level + kLevelIndex, index))
				continue;
			LevelEntry entry;
			char text[kDisplayNameLength + 1];
			if (const uint8_t* record = LevelNameRecord(index))
			{
				CopyText(record, text, sizeof(text), kLevelNameLength);
				entry.name = text;
				CopyText(record + kLevelTypeOffset, text, sizeof(text), kLevelTypeLength);
				entry.type = text;
			}
			CopyText(level + kLevelDisplayName, text, sizeof(text), kDisplayNameLength);
			entry.display = text;
			entry.current = level == current;
			if (!entry.name.empty())
				levels.push_back(entry);
		}
		return levels;
	}

	// ---- What the game's flow is doing ----

	struct FlowReading
	{
		bool haveFlow = false;
		int state = -1, setup = 0, restart = 0, playMode = 0;
		char pendingLevel[kPendingLevelLength + 1] = {};
		char level[kLevelNameLength + 1] = {};
		char levelDisplay[kDisplayNameLength + 1] = {};
		bool haveEntities = false;
		uint32_t root = 0;
		int transport = -1, command = 0;
		bool loadPending = false;
		uint32_t pendingSave[2] = {};
		bool spawnValid = false;
		float spawn[3] = {};
		uint32_t checkpoints = 0, debugCheckpoints = 0;
		bool havePause = false;
		uint32_t pauseAsked = 0, pauseNow = 0;
		bool snapshotValid = false, snapshotNormal = false;
		char snapshotLevel[kSnapshotLevelLength + 1] = {};
		uint32_t snapshotCheckpoint[2] = {};
		uint32_t snapshotMission = 0, snapshotDeathsInARow = 0, snapshotDeaths = 0;
		double snapshotTime = 0.0;
		bool snapshotTemporary = false;
		double now = 0.0;
	};

	void ReadFlow(FlowReading& out)
	{
		if (const uint8_t* flow = GlobalsField(kGlobalsGameFlow))
		{
			out.haveFlow = Read(flow + kFlowState, out.state) && Read(flow + kFlowSetup, out.setup) && Read(flow + kFlowRestart, out.restart) &&
				Read(flow + kFlowPlayMode, out.playMode);
			CopyText(flow + kFlowPendingLevel, out.pendingLevel, sizeof(out.pendingLevel), kPendingLevelLength);
		}
		if (const uint8_t* manager = GlobalsField(kGlobalsLevelManager))
		{
			const uint8_t* current = PointerAt(manager, kManagerCurrent);
			uint32_t index = 0;
			if (current && Read(current + kLevelIndex, index))
			{
				if (const uint8_t* record = LevelNameRecord(index))
					CopyText(record, out.level, sizeof(out.level), kLevelNameLength);
				CopyText(current + kLevelDisplayName, out.levelDisplay, sizeof(out.levelDisplay), kDisplayNameLength);
			}
		}
		out.root = LevelRoot();
		if (const uint8_t* entities = out.root ? EntityManager() : nullptr)
		{
			uint8_t pending = 0, spawnValid = 0;
			out.haveEntities = Read(entities + kEntityTransport, out.transport) && Read(entities + kEntityCommand, out.command) &&
				Read(entities + kEntityLoadPending, pending) && Read(entities + kEntityPendingSave, out.pendingSave[0]) &&
				Read(entities + kEntityPendingSave + 4, out.pendingSave[1]) && Read(entities + kEntitySpawnValid, spawnValid);
			out.loadPending = pending != 0;
			out.spawnValid = spawnValid != 0;
			Read(entities + kEntitySpawn + 0x0C, out.spawn[0]);
			Read(entities + kEntitySpawn + 0x1C, out.spawn[1]);
			Read(entities + kEntitySpawn + 0x2C, out.spawn[2]);
			Read(PointerAt(entities, kEntityCheckpoints) + kListCount, out.checkpoints);
			Read(PointerAt(entities, kEntityDebugCheckpoints) + kListCount, out.debugCheckpoints);
		}
		if (const uint8_t* pause = Global(kPauseMode))
			out.havePause = Read(pause + kPauseAsked, out.pauseAsked) && Read(pause + kPauseNow, out.pauseNow);
		out.snapshotValid = CallBool(snapshot_valid);
		out.snapshotNormal = CallBool(snapshot_valid_normal);
		if (out.snapshotValid)
			if (const uint8_t* snapshot = Global(kLevelSnapshot))
			{
				uint8_t temporary = 0;
				CopyText(snapshot + kSnapshotLevel, out.snapshotLevel, sizeof(out.snapshotLevel), kSnapshotLevelLength);
				Read(snapshot + kSnapshotCheckpoint, out.snapshotCheckpoint[0]);
				Read(snapshot + kSnapshotCheckpoint + 4, out.snapshotCheckpoint[1]);
				Read(snapshot + kSnapshotMission, out.snapshotMission);
				Read(snapshot + kSnapshotTime, out.snapshotTime);
				Read(snapshot + kSnapshotDeathsInARow, out.snapshotDeathsInARow);
				Read(snapshot + kSnapshotDeaths, out.snapshotDeaths);
				Read(snapshot + kSnapshotTemporary, temporary);
				out.snapshotTemporary = temporary != 0;
			}
		out.now = GameTime();
	}

	const char* Named(const char* const* names, size_t count, int value)
	{
		return value >= 0 && static_cast<size_t>(value) < count ? names[value] : "?";
	}
#define NAMED(names, value) Named(names, sizeof(names) / sizeof(names[0]), value)

	std::string PauseBitsText(uint32_t bits)
	{
		std::string text;
		for (const PauseBit& bit : kPauseBits)
			if (bits & bit.bit)
				text += (text.empty() ? "" : ", ") + std::string(bit.name);
		return text.empty() ? "none" : text;
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

	std::string Status()
	{
		std::lock_guard<std::mutex> lock(g_statusMutex);
		return g_status;
	}

	// ---- Pause and step ----

	std::atomic<bool> g_paused = false;          // the switch
	std::atomic<bool> g_pauseWhileOpen = false;  // hold the game while the debug menu's window is open
	std::atomic<bool> g_pauseBusy = false;       // a change is queued for the entity thread
	std::atomic<int> g_stepsLeft = 0;
	std::atomic<bool> g_stepping = false;        // the entity thread is letting frames through
	uint32_t g_pauseRoot = 0;                    // render thread: the level the switch was last seen in

	// The pause bits asked for (null when this build has no address for them)
	uint8_t* PauseBits()
	{
		uint8_t* pause = Global(kPauseMode);
		return pause ? pause + kPauseAsked : nullptr;
	}

	bool ReadPaused(bool& paused)
	{
		uint32_t bits = 0;
		const uint8_t* at = PauseBits();
		if (!at || !Read(at, bits))
			return false;
		paused = (bits & kPauseLiveLink) != 0;
		return true;
	}

	// Entity thread: the pause bit set or cleared (the pause functions' own bit operations, made atomic)
	bool SetPauseBit(uint32_t bit, bool on)
	{
		uint8_t* at = PauseBits();
		if (!at)
			return false;
		__try
		{
			if (on)
				InterlockedOr(reinterpret_cast<volatile LONG*>(at), static_cast<LONG>(bit));
			else
				InterlockedAnd(reinterpret_cast<volatile LONG*>(at), ~static_cast<LONG>(bit));
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// Entity thread, once a frame while frames are asked for: lets one frame through (the step bit, which the pause's
	// update clears once it has let that frame through), then comes back the next frame for the next
	void StepFrame()
	{
		bool paused = false;
		uint32_t bits = 0;
		const uint8_t* at = PauseBits();
		if (!at || !Read(at, bits) || !ReadPaused(paused) || !paused || !LevelRunning())
		{
			g_stepsLeft = 0;
			g_stepping = false;
			return;
		}
		// A run of frames stops at a script breakpoint rather than going past it (the debugger steps from there)
		if (LIVE_DEBUGGER::Holding())
		{
			g_stepsLeft = 0;
			g_stepping = false;
			SetStatus("stepping stopped: a script breakpoint holds the scripts (step with the Debugger tab)");
			return;
		}
		if (g_stepsLeft <= 0)
		{
			g_stepping = false;
			// Frames asked for between the check and now would otherwise wait for the next step asked for
			if (g_stepsLeft > 0 && !g_stepping.exchange(true))
				DEBUG_MENU::OnEntityThread(StepFrame);
			return;
		}
		if (!(bits & kPauseSingleFrame) && SetPauseBit(kPauseSingleFrame, true))
			g_stepsLeft--;
		DEBUG_MENU::OnEntityThread(StepFrame);
	}

	bool Step(int frames, std::string& message)
	{
		bool paused = false;
		if (!LevelRoot())
		{
			message = "no level running";
			return false;
		}
		if (!ReadPaused(paused) || !paused)
		{
			message = "not paused (turn on game.paused first)";
			return false;
		}
		// While the script debugger holds the scripts, one frame is let through by the debugger itself, so that it knows
		// the held call has gone on (the frame count is not used)
		if (LIVE_DEBUGGER::Holding())
		{
			const std::string result = LIVE_DEBUGGER::Step(true);
			message = "the script debugger holds the scripts: " + result + " (one frame, through it)";
			return result.rfind("not", 0) != 0;
		}
		frames = (std::max)(1, (std::min)(frames, 600));
		g_stepsLeft += frames;
		if (!g_stepping.exchange(true))
			DEBUG_MENU::OnEntityThread(StepFrame);
		message = "stepping " + std::to_string(frames) + (frames == 1 ? " frame" : " frames");
		return true;
	}

	// Whether the game should be held now: the switch, or the window open with "pause while open"
	bool PauseWanted()
	{
		return g_paused || (g_pauseWhileOpen && DEBUG_MENU::IsOpen());
	}

	ULONGLONG g_pauseAskedAt = 0; // render thread: when the entity thread was last asked to change the pause

	// Render thread, every frame: the pause bit follows the switch (and the window, with "pause while open"); a level change
	// or no level turns the switch off. The change is made on the entity thread (which still runs while the game is held);
	// should that not come round within two seconds, letting go is done from here (the bit operation is atomic).
	void ApplyPause()
	{
		const uint32_t root = LevelRoot();
		if (root != g_pauseRoot)
		{
			g_pauseRoot = root;
			g_paused = false;
			g_stepsLeft = 0;
		}
		if (!root)
			g_paused = false;
		bool paused = false;
		if (!ReadPaused(paused))
			return;
		const bool wanted = root && PauseWanted();
		if (wanted == paused)
			return;
		if (g_pauseBusy)
		{
			if (!wanted && GetTickCount64() - g_pauseAskedAt > 2000)
				SetPauseBit(kPauseLiveLink, false);
			return;
		}
		g_pauseBusy = true;
		g_pauseAskedAt = GetTickCount64();
		DEBUG_MENU::OnEntityThread([]()
		{
			// What is wanted when the entity thread comes round (not when it was asked)
			const bool hold = LevelRoot() != 0 && PauseWanted();
			if (!SetPauseBit(kPauseLiveLink, hold))
				SetStatus("the pause could not be changed");
			g_pauseBusy = false;
		});
	}

	// ---- Checkpoints ----

	struct CheckpointList
	{
		uint32_t root = 0;    // the level they were read in
		uint32_t count = 0;   // how many the list had then
		std::vector<std::string> names;  // empty: the checkpoint has no name (or, see read, it could not be read)
		std::vector<bool> read;
		int current = -1;     // the one the level is at (-1: none of them)
	};

	// A checkpoint's name as a list shows it
	std::string ShownName(const CheckpointList& list, size_t index)
	{
		if (index >= list.names.size())
			return "?";
		if (!list.names[index].empty())
			return list.names[index];
		return index < list.read.size() && list.read[index] ? "(no name)" : "(name not readable)";
	}

	// Entity thread: a checkpoint's name, copied out (empty when it has none; false when it could not be read)
	bool CheckpointName(t_checkpoint_name function, uint8_t* entities, uint32_t index, char* out, size_t size)
	{
		out[0] = '\0';
		__try
		{
			const char* name = function(entities, index);
			if (!name)
				return true;
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

	// Entity thread: the names of a level's checkpoints (or debug checkpoints), and which one the level is at
	CheckpointList ReadCheckpointList(bool debug)
	{
		CheckpointList list;
		list.root = LevelRoot();
		uint8_t* entities = EntityManager();
		const t_checkpoint_name name = debug ? debug_checkpoint_name : checkpoint_name;
		if (!list.root || !entities || !name)
			return list;
		const uint8_t* array = PointerAt(entities, debug ? kEntityDebugCheckpoints : kEntityCheckpoints);
		if (!array || !Read(array + kListCount, list.count))
			return list;
		const uint8_t* entries = PointerAt(array, kListEntries);
		const uint8_t* current = PointerAt(entities, kEntityCurrentCheckpoint);
		for (uint32_t i = 0; i < list.count && i < 256; i++)
		{
			char text[128];
			const bool read = CheckpointName(name, entities, i, text, sizeof(text));
			list.names.push_back(read ? std::string(text) : std::string());
			list.read.push_back(read);
			if (!debug && current && entries && PointerAt(entries, i * 4) == current)
				list.current = static_cast<int>(i);
		}
		return list;
	}

	// What the window shows, read on the entity thread when asked
	std::mutex g_listMutex;
	CheckpointList g_list;
	std::atomic<bool> g_listAsked = false;
	ULONGLONG g_listReadAt = 0;       // render thread
	ULONGLONG g_jumpAskedAt = 0;      // render thread: a jump or reload was asked for then
	// Render thread: the level and checkpoint the level checkpoints list last scrolled to, so it brings the current one into
	// view the first time it shows it and each time it changes, and leaves the list where it is otherwise
	uint32_t g_scrolledRoot = 0;
	int g_scrolledCurrent = -1;

	// The height of a bordered list of rows of a line each (a small button and text) showing so many whole rows
	float ListHeight(size_t rows)
	{
		const ImGuiStyle& style = ImGui::GetStyle();
		return style.WindowPadding.y * 2.0f + ImGui::GetTextLineHeightWithSpacing() * static_cast<float>((std::max)(rows, static_cast<size_t>(1))) - style.ItemSpacing.y;
	}

	void AskList()
	{
		if (g_listAsked.exchange(true))
			return;
		DEBUG_MENU::OnEntityThread([]()
		{
			CheckpointList list = ReadCheckpointList(false);
			std::lock_guard<std::mutex> lock(g_listMutex);
			g_list = list;
			g_listAsked = false;
		});
	}

	// Entity thread: whether a jump or reload can be made now, and if not why
	const char* CannotJump(uint32_t root)
	{
		if (!LevelRunning() || !root || LevelRoot() != root)
			return "the level changed";
		uint8_t* entities = EntityManager();
		uint8_t pending = 0;
		uint32_t command = 0;
		if (!entities || !Read(entities + kEntityLoadPending, pending) || !Read(entities + kEntityCommand, command))
			return "the entity manager cannot be read";
		if (pending || command)
			return "a checkpoint load is already pending";
		uint8_t* saving = GlobalsField(kGlobalsSaving);
		bool busy = false;
		__try
		{
			busy = saving && is_saving && is_saving(saving);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return "the saving cannot be read";
		}
		return busy ? "the game is saving" : nullptr;
	}

	bool CallJumpNamed(uint8_t* entities, const char* name)
	{
		__try
		{
			return jump_to_named_checkpoint(entities, name);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool CallJumpDebug(uint8_t* entities, uint32_t index)
	{
		__try
		{
			jump_to_debug_checkpoint(entities, index);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// Entity thread: the level's checkpoint of this name (its first, as the game's jump finds it) loaded at the entity
	// manager's next tick
	DEBUG_MENU::CommandResult JumpNamed(uint32_t root, const std::string& name)
	{
		if (const char* why = CannotJump(root))
			return { false, std::string("no jump: ") + why };
		if (!jump_to_named_checkpoint)
			return { false, "not available on this game release" };
		if (!CallJumpNamed(EntityManager(), name.c_str()))
			return { false, "no jump: the level has no checkpoint called " + name };
		return { true, "jumping to checkpoint " + name };
	}

	// Entity thread: what the game's jump by name does once it has found the checkpoint - the checkpoint at this place in the
	// level's list handed to the entity manager, and the jump command set for its next tick
	bool CallJumpIndex(uint8_t* entities, uint32_t index)
	{
		__try
		{
			const uint8_t* array = *reinterpret_cast<uint8_t* const*>(entities + kEntityCheckpoints);
			if (!array || index >= *reinterpret_cast<const uint32_t*>(array + kListCount))
				return false;
			const uint8_t* entries = *reinterpret_cast<uint8_t* const*>(array + kListEntries);
			if (!entries || !*reinterpret_cast<void* const*>(entries + index * 4))
				return false;
			assign_entity_reference(entities + kEntityCurrentCheckpoint, entries + index * 4);
			*reinterpret_cast<volatile int*>(entities + kEntityCommand) = kCommandJump;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// Entity thread: the level's checkpoint at this place in its list (0 = the first), as long as it is still the one asked
	// for (the list grows as the level's scripts activate checkpoints)
	DEBUG_MENU::CommandResult JumpIndex(uint32_t root, uint32_t index, const std::string& expected)
	{
		if (const char* why = CannotJump(root))
			return { false, std::string("no jump: ") + why };
		if (!assign_entity_reference || !checkpoint_name)
			return { false, "not available on this game release" };
		const CheckpointList list = ReadCheckpointList(false);
		const std::string number = std::to_string(index + 1);
		if (index >= list.names.size())
			return { false, "no jump: the level has no checkpoint " + number };
		if (list.names[index] != expected)
			return { false, "no jump: checkpoint " + number + " is now " + ShownName(list, index) + " (the list changed)" };
		if (!CallJumpIndex(EntityManager(), index))
			return { false, "the jump failed" };
		return { true, "jumping to checkpoint " + number + " (" + ShownName(list, index) + ")" };
	}

	DEBUG_MENU::CommandResult JumpDebug(uint32_t root, uint32_t index)
	{
		if (const char* why = CannotJump(root))
			return { false, std::string("no jump: ") + why };
		if (!jump_to_debug_checkpoint)
			return { false, "not available on this game release" };
		const CheckpointList list = ReadCheckpointList(true);
		if (index >= list.count)
			return { false, "no jump: there is no debug checkpoint " + std::to_string(index + 1) };
		if (!CallJumpDebug(EntityManager(), index))
			return { false, "the jump failed" };
		return { true, "jumping to debug checkpoint " + std::to_string(index + 1) + " (" + (index < list.names.size() ? list.names[index] : std::string("?")) + ")" };
	}

	// Entity thread: the game's restart from the checkpoint in memory
	DEBUG_MENU::CommandResult Reload(uint32_t root)
	{
		if (const char* why = CannotJump(root))
			return { false, std::string("no reload: ") + why };
		uint8_t* flow = GlobalsField(kGlobalsGameFlow);
		int state = -1, restart = 0;
		if (!flow || !Read(flow + kFlowState, state) || !Read(flow + kFlowRestart, restart))
			return { false, "no reload: the game's flow cannot be read" };
		if (state == kStateGameOver)
			return { false, "no reload: the game is over (its own menu restarts from the checkpoint)" };
		if (state != kStateGameplay || restart != 0)
			return { false, "no reload: the game is not in gameplay (it is starting, restarting or unloading)" };
		if (!snapshot_valid_normal || !CallBool(snapshot_valid_normal))
			return { false, "no reload: there is no checkpoint in memory (none reached since the level loaded, or only a mission's temporary state)" };
		// The game's own restart closes the pause menu as it starts; this doesn't, so it waits for the menu to be closed
		uint32_t pauseBits = 0;
		const uint8_t* pauseAt = PauseBits();
		if (pauseAt && Read(pauseAt, pauseBits) && (pauseBits & kPauseMenuOpen))
			return { false, "no reload: close the pause menu first" };
		// A restart is a death's, which never happens paused: the debug menu's pause lets go first
		bool paused = false;
		if (ReadPaused(paused) && paused)
		{
			g_paused = false;
			SetPauseBit(kPauseLiveLink, false);
		}
		uint8_t* entities = EntityManager();
		if (!Write<uint8_t>(entities + kEntityLoadPending, 1) || !Write<int>(flow + kFlowRestart, kRestartCheckpoint))
			return { false, "the reload failed" };
		return { true, std::string("reloading the last checkpoint") + (paused ? " (the pause was let go)" : "") };
	}

	// ---- Difficulty ----

	struct DifficultyReading
	{
		bool have = false;
		int handle = -1, value = -1, lowest = -1;
		bool changed = false;
		bool available[kDifficulties] = {};
	};

	DifficultyReading ReadDifficulty()
	{
		DifficultyReading reading;
		const uint8_t* difficulty = GlobalsField(kGlobalsDifficulty);
		uint8_t changed = 0;
		if (!difficulty || !Read(difficulty + kDifficultyHandle, reading.handle) || !Read(difficulty + kDifficultyValue, reading.value) ||
			!Read(difficulty + kDifficultyLowest, reading.lowest) || !Read(difficulty + kDifficultyChanged, changed))
			return reading;
		reading.changed = changed != 0;
		const uint8_t* settings = PointerAt(difficulty, kDifficultySettings);
		for (int i = 0; i < kDifficulties; i++)
			reading.available[i] = settings && PointerAt(settings, i * 4) != nullptr;
		reading.have = true;
		return reading;
	}

	std::atomic<bool> g_keepLowest = true;

	bool CallSetDifficulty(uint8_t* difficulty, int handle)
	{
		__try
		{
			set_difficulty(difficulty, &handle);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// AI thread: the game's own setter (it tells every character), with the campaign's lowest difficulty put back after it
	// when asked (the setter lowers it, and the campaign's difficulty achievements go by it)
	DEBUG_MENU::CommandResult SetDifficulty(int value, bool keepLowest)
	{
		uint8_t* difficulty = GlobalsField(kGlobalsDifficulty);
		const DifficultyReading reading = ReadDifficulty();
		if (!difficulty || !reading.have)
			return { false, "the difficulty cannot be read" };
		if (!set_difficulty)
			return { false, "not available on this game release" };
		if (value < 0 || value >= kDifficulties || !reading.available[value])
			return { false, "the game has no settings for that difficulty" };
		if (reading.handle == value)
			return { true, std::string("already ") + kDifficultyNames[value] };
		if (!CallSetDifficulty(difficulty, value))
			return { false, "the change failed" };
		if (keepLowest)
		{
			Write<int>(difficulty + kDifficultyLowest, reading.lowest);
			Write<uint8_t>(difficulty + kDifficultyChanged, reading.changed ? 1 : 0);
		}
		return { true, std::string("difficulty ") + kDifficultyNames[value] + (keepLowest ? " (the campaign's lowest kept)" : "") };
	}

	int DifficultyFromName(const std::string& text)
	{
		std::string lower;
		for (char c : text)
			if (!isspace(static_cast<unsigned char>(c)))
				lower += static_cast<char>(tolower(static_cast<unsigned char>(c)));
		if (lower == "iron")
			return Nightmare;
		for (int i = 0; i < kDifficulties; i++)
		{
			std::string name = kDifficultyNames[i];
			for (char& c : name)
				c = static_cast<char>(tolower(static_cast<unsigned char>(c)));
			if (lower == name)
				return i;
		}
		if (!lower.empty() && isdigit(static_cast<unsigned char>(lower[0])))
		{
			const int number = atoi(lower.c_str());
			return number >= 0 && number < kDifficulties ? number : -1;
		}
		return -1;
	}

	// ---- Levels ----

	std::string Lower(std::string text)
	{
		for (char& c : text)
			c = static_cast<char>(tolower(static_cast<unsigned char>(c)));
		return text;
	}

	// The name a level is asked for by, as the game's look-up wants it: the whole name the level manager knows it by, in
	// any case. Taken as listed ("Production\BSP_Torrens", "Production\DLC\ChallengeMap1"), without its "Production\"
	// ("DLC\ChallengeMap1", as OpenCAGE shows a DLC level), or by its last part alone ("BSP_TORRENS", "ChallengeMap1"),
	// with / or \. A name that ends more than one level's comes back empty, those levels in "several"; one no level has is
	// passed on as asked ("Production\" before a bare one, as OpenCAGE's load level sends it), for the game to refuse.
	std::string LoadName(std::string level, std::string& several)
	{
		while (!level.empty() && isspace(static_cast<unsigned char>(level.back())))
			level.pop_back();
		while (!level.empty() && isspace(static_cast<unsigned char>(level.front())))
			level.erase(level.begin());
		std::replace(level.begin(), level.end(), '/', '\\');
		while (!level.empty() && level.front() == '\\')
			level.erase(level.begin());
		if (level.empty())
			return level;
		const std::vector<LevelEntry> levels = ReadLevels();
		const std::string asked = Lower(level);
		for (const std::string& whole : { asked, "production\\" + asked })
			for (const LevelEntry& entry : levels)
				if (Lower(entry.name) == whole)
					return entry.name;
		std::vector<std::string> endings;
		for (const LevelEntry& entry : levels)
		{
			const std::string name = Lower(entry.name);
			if (name.size() > asked.size() && name[name.size() - asked.size() - 1] == '\\' && name.compare(name.size() - asked.size(), asked.size(), asked) == 0)
				endings.push_back(entry.name);
		}
		if (endings.size() == 1)
			return endings.front();
		if (endings.size() > 1)
		{
			for (const std::string& name : endings)
				several += (several.empty() ? "" : ", ") + name;
			return std::string();
		}
		return level.find('\\') == std::string::npos ? "Production\\" + level : level;
	}

	bool CallLoadLevel(GAME_LEVEL_MANAGER::Instance* manager, const char* name)
	{
		__try
		{
			const int level = GAME_LEVEL_MANAGER::get_level_from_name(manager, const_cast<char*>(name));
			if (level == 0)
				return false;
			GAME_LEVEL_MANAGER::queue_level(manager, level);
			GAME_LEVEL_MANAGER::request_next_level(manager, false);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// Render thread: Live Link's level load
	DEBUG_MENU::CommandResult LoadLevel(const std::string& asked)
	{
		std::string several;
		const std::string name = LoadName(asked, several);
		if (!several.empty())
			return { false, "more than one level is called that: " + several + " (give the whole name)" };
		if (name.empty())
			return { false, "which level? (game.list_levels lists them)" };
		if (!GAME_LEVEL_MANAGER::get_level_from_name || !GAME_LEVEL_MANAGER::queue_level || !GAME_LEVEL_MANAGER::request_next_level)
			return { false, "not available on this game release" };
		GAME_LEVEL_MANAGER::Instance* manager = GAME_LEVEL_MANAGER::m_instance ? GAME_LEVEL_MANAGER::m_instance :
			reinterpret_cast<GAME_LEVEL_MANAGER::Instance*>(GlobalsField(kGlobalsLevelManager));
		if (!manager)
			return { false, "the level manager is not up yet" };
		if (!CallLoadLevel(manager, name.c_str()))
			return { false, "the game knows no level called " + name + " (game.list_levels lists them)" };
		return { true, "loading " + name + (GAME_SAVES::On() ? "" : " (Don't write saves is off: a campaign level saves as it loads)") };
	}

	// ---- Reports ----

	std::string StatusText()
	{
		FlowReading flow;
		ReadFlow(flow);
		std::string text;
		char line[512];
		snprintf(line, sizeof(line), "game_state=%d %s\nrestart=%d %s\nplay_mode=%d\npending_level=%s\nlevel=%s\nlevel_display=%s\nlevel_root=%08X\n",
			flow.state, NAMED(kFlowStates, flow.state), flow.restart, NAMED(kRestartModes, flow.restart), flow.playMode, flow.pendingLevel, flow.level,
			flow.levelDisplay, flow.root);
		text += line;
		// Whether the player's character exists: the flow says gameplay and the scripts run a while before it does after a
		// level load, so this is what says the level can be played
		const bool player = PLAYER_DEBUG::Player() != nullptr;
		snprintf(line, sizeof(line), "player=%d %s\n", player ? 1 : 0, player ? "in the level" : "not in the level (yet)");
		text += line;
		if (flow.haveEntities)
		{
			snprintf(line, sizeof(line), "transport=%d %s\ncommand=%d %s\ncheckpoint_load_pending=%d\nsave_waiting=%08X:%08X\ncheckpoints=%u\ndebug_checkpoints=%u\nspawn_point=%s%.2f,%.2f,%.2f\n",
				flow.transport, NAMED(kTransports, flow.transport), flow.command, NAMED(kCommands, flow.command), flow.loadPending ? 1 : 0,
				flow.pendingSave[0], flow.pendingSave[1], flow.checkpoints, flow.debugCheckpoints, flow.spawnValid ? "" : "(not set) ",
				flow.spawn[0], flow.spawn[1], flow.spawn[2]);
			text += line;
		}
		if (flow.havePause)
		{
			snprintf(line, sizeof(line), "pause_asked=%08X %s\npause_now=%08X %s\npaused_by_debug_menu=%d\n", flow.pauseAsked, PauseBitsText(flow.pauseAsked).c_str(),
				flow.pauseNow, PauseBitsText(flow.pauseNow).c_str(), (flow.pauseAsked & kPauseLiveLink) ? 1 : 0);
			text += line;
		}
		snprintf(line, sizeof(line), "checkpoint_in_memory=%d\n", flow.snapshotValid ? (flow.snapshotNormal ? 1 : 2) : 0);
		text += line;
		if (flow.snapshotValid)
		{
			snprintf(line, sizeof(line), "checkpoint_level=%s\ncheckpoint_entity=%08X:%08X\ncheckpoint_mission=%u\ncheckpoint_captured=%.1f (game time now %.1f)\ncheckpoint_deaths=%u in_a_row=%u\ncheckpoint_temporary=%d\n",
				flow.snapshotLevel, flow.snapshotCheckpoint[0], flow.snapshotCheckpoint[1], flow.snapshotMission, flow.snapshotTime, flow.now, flow.snapshotDeaths,
				flow.snapshotDeathsInARow, flow.snapshotTemporary ? 1 : 0);
			text += line;
		}
		const DifficultyReading difficulty = ReadDifficulty();
		if (difficulty.have)
		{
			snprintf(line, sizeof(line), "difficulty=%d %s\ndifficulty_lowest=%d %s\ndifficulty_changed=%d\n", difficulty.handle, NAMED(kDifficultyNames, difficulty.handle),
				difficulty.lowest, NAMED(kDifficultyNames, difficulty.lowest), difficulty.changed ? 1 : 0);
			text += line;
		}
		snprintf(line, sizeof(line), "dont_write_saves=%d\n", GAME_SAVES::On() ? 1 : 0);
		text += line;
		return text;
	}

	std::string ListText(const CheckpointList& list, bool debug)
	{
		if (!list.root)
			return "no level running";
		if (list.names.empty())
			return debug ? "no debug checkpoints active in this level" : "no checkpoints active in this level";
		std::string text;
		for (size_t i = 0; i < list.names.size(); i++)
		{
			char line[200];
			snprintf(line, sizeof(line), "%u %s%s\n", static_cast<unsigned>(i + 1), ShownName(list, i).c_str(), static_cast<int>(i) == list.current ? " (current)" : "");
			text += line;
		}
		return text;
	}

	// A checkpoint asked for by number (1 = the first) or by name
	bool ParseIndex(const std::string& argument, uint32_t& index)
	{
		const char* text = argument.c_str();
		while (*text == ' ' || *text == '#')
			text++;
		char* end = nullptr;
		const long number = strtol(text, &end, 10);
		if (end == text || number < 1)
			return false;
		while (*end == ' ')
			end++;
		if (*end)
			return false;
		index = static_cast<uint32_t>(number - 1);
		return true;
	}

	std::string Trim(std::string text)
	{
		while (!text.empty() && isspace(static_cast<unsigned char>(text.back())))
			text.pop_back();
		while (!text.empty() && isspace(static_cast<unsigned char>(text.front())))
			text.erase(text.begin());
		return text;
	}

	void RegisterOnce()
	{
		static bool registered = false;
		if (registered)
			return;
		registered = true;
		DEBUG_MENU::AddSwitch("game.paused", &g_paused);
		DEBUG_MENU::AddSwitch("game.pause_while_open", &g_pauseWhileOpen);
		DEBUG_MENU::AddSwitch("game.keep_lowest_difficulty", &g_keepLowest);
		DEBUG_MENU::AddCommand("game.status", "the game's flow, level, whether the player's character is in it yet, script transport, pause bits, checkpoint in memory and difficulty, a line each", [](const std::string&)
		{
			return DEBUG_MENU::CommandResult{ true, StatusText() };
		});
		DEBUG_MENU::AddCommand("game.step", "while game.paused is on: lets one frame through, or the number given (up to 600, one a frame)", [](const std::string& argument)
		{
			std::string message;
			const int frames = Trim(argument).empty() ? 1 : atoi(argument.c_str());
			const bool ok = Step(frames, message);
			return DEBUG_MENU::CommandResult{ ok, message };
		});
		DEBUG_MENU::AddCommand("game.list_checkpoints", "the level's checkpoints the level has activated: number, name, (current)", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnEntityThread([]() { return DEBUG_MENU::CommandResult{ true, ListText(ReadCheckpointList(false), false) }; });
		});
		DEBUG_MENU::AddCommand("game.jump_checkpoint", "jumps to a checkpoint of the level, by number (as game.list_checkpoints numbers them; any checkpoint, named or not) or by name (the first with that name, as the game's own jump finds it)", [](const std::string& argument)
		{
			const std::string asked = Trim(argument);
			const uint32_t root = LevelRoot();
			return DEBUG_MENU::AnswerOnEntityThread([asked, root]()
			{
				uint32_t index = 0;
				DEBUG_MENU::CommandResult result;
				if (ParseIndex(asked, index))
				{
					const CheckpointList list = ReadCheckpointList(false);
					if (index >= list.names.size())
						return DEBUG_MENU::CommandResult{ false, "no checkpoint " + asked + " (game.list_checkpoints lists them)" };
					result = JumpIndex(root, index, list.names[index]);
				}
				else if (asked.empty())
					return DEBUG_MENU::CommandResult{ false, "which checkpoint? (game.list_checkpoints lists them)" };
				else
					result = JumpNamed(root, asked);
				SetStatus(result.message);
				return result;
			});
		});
		DEBUG_MENU::AddCommand("game.reload_checkpoint", "restarts from the checkpoint in memory, as a death does", [](const std::string&)
		{
			const uint32_t root = LevelRoot();
			return DEBUG_MENU::AnswerOnEntityThread([root]()
			{
				const DEBUG_MENU::CommandResult result = Reload(root);
				SetStatus(result.message);
				return result;
			});
		});
		DEBUG_MENU::AddCommand("game.list_debug_checkpoints", "the level's debug checkpoints the level has activated: number, name", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnEntityThread([]() { return DEBUG_MENU::CommandResult{ true, ListText(ReadCheckpointList(true), true) }; });
		});
		DEBUG_MENU::AddCommand("game.jump_debug_checkpoint", "jumps to a debug checkpoint, by number (as game.list_debug_checkpoints numbers them) or name", [](const std::string& argument)
		{
			const std::string asked = Trim(argument);
			const uint32_t root = LevelRoot();
			return DEBUG_MENU::AnswerOnEntityThread([asked, root]()
			{
				uint32_t index = 0;
				if (!ParseIndex(asked, index))
				{
					const CheckpointList list = ReadCheckpointList(true);
					index = UINT32_MAX;
					for (size_t i = 0; i < list.names.size(); i++)
						if (_stricmp(list.names[i].c_str(), asked.c_str()) == 0)
						{
							index = static_cast<uint32_t>(i);
							break;
						}
					if (index == UINT32_MAX)
						return DEBUG_MENU::CommandResult{ false, "no debug checkpoint " + asked + " (game.list_debug_checkpoints lists them)" };
				}
				const DEBUG_MENU::CommandResult result = JumpDebug(root, index);
				SetStatus(result.message);
				return result;
			});
		});
		DEBUG_MENU::AddCommand("game.list_levels", "every level the game knows: its name as game.load_level takes it, its type, its display name, (current)", [](const std::string&)
		{
			std::string text;
			for (const LevelEntry& level : ReadLevels())
				text += level.name + " [" + level.type + "] " + level.display + (level.current ? " (current)" : "") + "\n";
			return DEBUG_MENU::CommandResult{ !text.empty(), text.empty() ? "the level manager is not up yet" : text };
		});
		DEBUG_MENU::AddCommand("game.load_level", "loads a level, as OpenCAGE's load level does: by its name as game.list_levels lists it (Production\\DLC\\ChallengeMap1), without its Production\\ (DLC\\ChallengeMap1) or by its last part alone (BSP_TORRENS, ChallengeMap1); any case, / or \\", [](const std::string& argument)
		{
			const std::string asked = argument;
			return DEBUG_MENU::AnswerOnRenderThread([asked]()
			{
				const DEBUG_MENU::CommandResult result = LoadLevel(asked);
				SetStatus(result.message);
				return result;
			});
		});
		DEBUG_MENU::AddCommand("game.difficulty", "with no argument: the difficulty and the campaign's lowest; with one (novice, easy, medium, hard, nightmare): sets it live (game.keep_lowest_difficulty keeps the campaign's lowest)", [](const std::string& argument)
		{
			const std::string asked = Trim(argument);
			if (asked.empty())
			{
				const DifficultyReading reading = ReadDifficulty();
				if (!reading.have)
					return DEBUG_MENU::CommandResult{ false, "the difficulty cannot be read" };
				char text[160];
				snprintf(text, sizeof(text), "difficulty=%s\nlowest=%s\nchanged=%d\n", NAMED(kDifficultyNames, reading.handle), NAMED(kDifficultyNames, reading.lowest), reading.changed ? 1 : 0);
				return DEBUG_MENU::CommandResult{ true, text };
			}
			const int value = DifficultyFromName(asked);
			if (value < 0)
				return DEBUG_MENU::CommandResult{ false, "which difficulty? novice, easy, medium, hard or nightmare" };
			const bool keepLowest = g_keepLowest;
			return DEBUG_MENU::AnswerOnAiThread([value, keepLowest]()
			{
				const DEBUG_MENU::CommandResult result = SetDifficulty(value, keepLowest);
				SetStatus(result.message);
				return result;
			});
		});
	}

	// ---- The window ----

	void Tooltip(const char* text)
	{
		if (!ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip | ImGuiHoveredFlags_AllowWhenDisabled))
			return;
		ImGui::BeginTooltip();
		ImGui::PushTextWrapPos(ImGui::GetFontSize() * 30.0f);
		ImGui::TextUnformatted(text);
		ImGui::PopTextWrapPos();
		ImGui::EndTooltip();
	}

	const ImVec4 kAmber(1.0f, 0.75f, 0.3f, 1.0f);

	// Prose in grey (or a colour), wrapped at the window's right edge rather than running past it
	void HintV(const ImVec4* colour, const char* format, va_list args)
	{
		ImGui::PushTextWrapPos(0.0f);
		if (colour)
			ImGui::TextColoredV(*colour, format, args);
		else
			ImGui::TextDisabledV(format, args);
		ImGui::PopTextWrapPos();
	}

	void Hint(const char* format, ...)
	{
		va_list args;
		va_start(args, format);
		HintV(nullptr, format, args);
		va_end(args);
	}

	void Warning(const char* format, ...)
	{
		va_list args;
		va_start(args, format);
		HintV(&kAmber, format, args);
		va_end(args);
	}

	void Apply()
	{
		RegisterOnce();
		ApplyPause();
	}

	void ApplyOverlay(ImDrawList* /*drawList*/)
	{
		Apply();
	}

	void DrawFlow(const FlowReading& flow)
	{
		ImGui::SeparatorText("Game flow");
		if (!flow.haveFlow)
		{
			ImGui::TextDisabled("The game's flow is not up yet.");
			return;
		}
		ImGui::Text("State: %s", NAMED(kFlowStates, flow.state));
		if (flow.restart)
		{
			ImGui::SameLine();
			ImGui::TextColored(kAmber, "- restarting from %s", NAMED(kRestartModes, flow.restart));
		}
		if (flow.level[0])
			ImGui::TextWrapped("Level: %s%s%s%s", flow.level, flow.levelDisplay[0] ? " (" : "", flow.levelDisplay, flow.levelDisplay[0] ? ")" : "");
		else
			ImGui::TextDisabled("Level: none");
		if (flow.pendingLevel[0] && _stricmp(flow.pendingLevel, flow.level) != 0)
			ImGui::TextWrapped("Level asked for next: %s", flow.pendingLevel);
		if (flow.haveEntities)
		{
			ImGui::Text("Level scripts: %s", NAMED(kTransports, flow.transport));
			Tooltip("The entity manager's script transport: paused from the level's load through its intro and opening cutscene, and while a cutscene or a message holds the game; running otherwise.");
		}
		if (flow.havePause)
		{
			ImGui::TextWrapped("Paused by: %s", PauseBitsText(flow.pauseAsked).c_str());
			Tooltip("The game's pause bits: anything but none stops the game clock (a frame step lets one frame through).");
		}
	}

	void DrawCheckpointState(const FlowReading& flow)
	{
		ImGui::SeparatorText("Checkpoint and respawn");
		if (!flow.root)
		{
			ImGui::TextDisabled("No level running.");
			return;
		}
		CheckpointList list;
		{
			std::lock_guard<std::mutex> lock(g_listMutex);
			list = g_list;
		}
		if ((list.root != flow.root || list.count != flow.checkpoints || GetTickCount64() - g_listReadAt > 2000))
		{
			g_listReadAt = GetTickCount64();
			AskList();
		}
		if (list.root == flow.root && list.current >= 0 && list.current < static_cast<int>(list.names.size()))
			ImGui::TextWrapped("Checkpoint the level is at: %s", ShownName(list, static_cast<size_t>(list.current)).c_str());
		else
			ImGui::TextDisabled("Checkpoint the level is at: none of its %u", flow.checkpoints);
		if (flow.snapshotValid)
		{
			const double ago = flow.now - flow.snapshotTime;
			ImGui::TextWrapped("In memory: mission %u, %s, captured %.0f s of game time ago%s", flow.snapshotMission, flow.snapshotLevel, ago > 0.0 ? ago : 0.0,
				flow.snapshotTemporary ? " (a mission's temporary state)" : "");
			Tooltip("The checkpoint a death or Reload last checkpoint goes back to (and the game's last checkpoint save was made from).");
			if (flow.level[0] && _stricmp(flow.snapshotLevel, flow.level) != 0)
				Warning("It is another level's: a reload loads that level.");
		}
		else
		{
			Hint("In memory: no checkpoint (none reached since the game started).");
		}
		if (flow.loadPending || flow.command)
			Warning("Waiting: %s%s", flow.loadPending ? "a checkpoint load " : "", flow.command ? NAMED(kCommands, flow.command) : "");
		if (flow.pendingSave[0] || flow.pendingSave[1])
		{
			ImGui::TextWrapped("A checkpoint save is waiting for the player (entity %08X).", flow.pendingSave[0]);
			Tooltip("The entity manager saves it once the player is in a state to save (not dying, grabbed or in a paired kill).");
		}
		if (flow.spawnValid)
		{
			ImGui::Text("Spawn point: %.2f, %.2f, %.2f", flow.spawn[0], flow.spawn[1], flow.spawn[2]);
			Tooltip("Where the level's last checkpoint (or mission start) puts the player.");
		}
		else
			ImGui::TextDisabled("Spawn point: not set");
	}

	void DrawDifficulty()
	{
		ImGui::SeparatorText("Difficulty");
		const DifficultyReading reading = ReadDifficulty();
		if (!reading.have)
		{
			ImGui::TextDisabled("The difficulty is not up yet.");
			return;
		}
		ImGui::Text("Now: %s", NAMED(kDifficultyNames, reading.handle));
		ImGui::SameLine();
		Hint("(the campaign's lowest: %s%s)", NAMED(kDifficultyNames, reading.lowest), reading.changed ? ", changed in this campaign" : "");
		Tooltip("The lowest difficulty the campaign was played at goes into the save and decides its difficulty achievements.");
		const bool available = set_difficulty != nullptr && LevelRoot() != 0;
		ImGui::BeginDisabled(!available);
		for (int i = 0; i < kDifficulties; i++)
		{
			const int value = kDifficultyOrder[i];
			if (i)
				ImGui::SameLine();
			ImGui::BeginDisabled(!reading.available[value] || reading.handle == value);
			if (ImGui::SmallButton(kDifficultyNames[value]))
			{
				const bool keepLowest = g_keepLowest;
				DEBUG_MENU::OnAiThread([value, keepLowest]() { SetStatus(SetDifficulty(value, keepLowest).message); });
			}
			ImGui::EndDisabled();
		}
		ImGui::EndDisabled();
		Tooltip(available ? "Set live with the game's own setter, as the pause menu does: every character takes up the new difficulty's settings (the alien's menace and senses...). Nightmare is meant for a new game: its HUD and resource changes may not all follow." :
			"No level running, or not available on this game release.");
		bool keep = g_keepLowest;
		if (ImGui::Checkbox("Keep the campaign's lowest difficulty", &keep))
			g_keepLowest = keep;
		Tooltip("Puts back the campaign's lowest difficulty after a change, so trying an easier one doesn't cost the campaign its difficulty achievements (the game itself would lower it).");
		Hint("While the game is paused, a change waits for it to run (it is made on the AI's own update).");
	}

	char g_levelFilter[64] = {};

	void DrawLevels()
	{
		ImGui::SeparatorText("Levels");
		const std::vector<LevelEntry> levels = ReadLevels();
		if (levels.empty())
		{
			ImGui::TextDisabled("The level manager is not up yet.");
			return;
		}
		if (!GAME_SAVES::On())
			Warning("Don't write saves is off: a campaign level saves as it loads.");
		ImGui::SetNextItemWidth(220.0f * DEBUG_MENU::Scale());
		ImGui::InputTextWithHint("##levelfilter", "Filter", g_levelFilter, sizeof(g_levelFilter));
		ImGui::SameLine();
		ImGui::TextDisabled("%u levels", static_cast<unsigned>(levels.size()));
		const bool available = GAME_LEVEL_MANAGER::get_level_from_name && GAME_LEVEL_MANAGER::queue_level && GAME_LEVEL_MANAGER::request_next_level;
		std::string filter = g_levelFilter;
		for (char& c : filter)
			c = static_cast<char>(tolower(static_cast<unsigned char>(c)));
		// Whole rows down to the window's bottom, with room left under it for the last action's line (at least ten rows, at
		// most every level), so a taller window shows more of them
		const float step = ImGui::GetTextLineHeightWithSpacing();
		const float room = ImGui::GetContentRegionAvail().y - ImGui::GetStyle().WindowPadding.y * 2.0f - step * 2.0f - ImGui::GetStyle().ItemSpacing.y * 2.0f;
		const size_t fits = room > step ? static_cast<size_t>((room + ImGui::GetStyle().ItemSpacing.y) / step) : 0;
		if (ImGui::BeginChild("##levels", ImVec2(0.0f, ListHeight((std::min)(levels.size(), (std::max)(fits, static_cast<size_t>(10))))), ImGuiChildFlags_Borders))
		{
			for (size_t i = 0; i < levels.size(); i++)
			{
				const LevelEntry& level = levels[i];
				if (!filter.empty())
				{
					std::string haystack = level.name + " " + level.display + " " + level.type;
					for (char& c : haystack)
						c = static_cast<char>(tolower(static_cast<unsigned char>(c)));
					if (haystack.find(filter) == std::string::npos)
						continue;
				}
				ImGui::PushID(static_cast<int>(i));
				ImGui::BeginDisabled(!available);
				if (ImGui::SmallButton("Load"))
				{
					const std::string name = level.name;
					DEBUG_MENU::OnRenderThread([name]() { SetStatus(LoadLevel(name).message); });
				}
				ImGui::EndDisabled();
				ImGui::SameLine();
				if (level.current)
					ImGui::TextColored(ImVec4(0.45f, 0.9f, 0.45f, 1.0f), "%s", level.name.c_str());
				else
					ImGui::TextUnformatted(level.name.c_str());
				ImGui::SameLine();
				ImGui::TextDisabled("%s%s%s", level.type.c_str(), level.display.empty() || level.display == level.name ? "" : " - ", level.display == level.name ? "" : level.display.c_str());
				ImGui::PopID();
			}
		}
		ImGui::EndChild();
	}

	void DrawTab()
	{
		Apply();
		FlowReading flow;
		ReadFlow(flow);
		DrawFlow(flow);
		DrawCheckpointState(flow);
		DrawDifficulty();
		DrawLevels();
		const std::string status = Status();
		if (!status.empty())
		{
			ImGui::Separator();
			Hint("Last: %s", status.c_str());
		}
	}

	const bool g_tabAdded = DEBUG_MENU::AddTab("Game flow", 72, DrawTab, "Game");
	const bool g_overlayAdded = DEBUG_MENU::AddOverlay(5, ApplyOverlay);
}

bool GAME_DEBUG::Paused()
{
	bool paused = false;
	return ReadPaused(paused) && paused;
}

bool GAME_DEBUG::RequestLevelLoad(const std::string& level, std::string& message)
{
	const DEBUG_MENU::CommandResult result = LoadLevel(level);
	message = result.message;
	return result.ok;
}

void GAME_DEBUG::DrawTimeControls()
{
	Apply();
	const bool available = Address(kPauseMode) != 0;
	const uint32_t root = LevelRoot();
	bool paused = false;
	ReadPaused(paused);
	ImGui::BeginDisabled(!available || !root);
	if (ImGui::Button(g_paused ? "Resume" : "Pause"))
		g_paused = !g_paused;
	Tooltip(available ? "Holds the whole game - its clock, the AI, physics, animation, zone streaming and the level's scripts - without the pause menu (the game's own pause for a connected editor). The game camera is updated with no time passing; the debug menu and its views keep working. OpenCAGE's edits, and changes made on the AI's update (the difficulty, the AI actions), wait until it resumes or steps." :
		"Not available on this game release.");
	ImGui::SameLine();
	ImGui::BeginDisabled(!paused);
	if (ImGui::Button("Step"))
	{
		std::string message;
		Step(1, message);
	}
	Tooltip("Lets exactly one frame of the game through, then holds it again.");
	ImGui::SameLine();
	if (ImGui::Button("Step 10"))
	{
		std::string message;
		Step(10, message);
	}
	Tooltip("Lets ten frames through, one a frame.");
	ImGui::EndDisabled();
	ImGui::EndDisabled();
	ImGui::SameLine();
	if (!root)
		ImGui::TextDisabled("No level running.");
	else if (paused)
		ImGui::TextColored(ImVec4(1.0f, 0.75f, 0.3f, 1.0f), g_stepping ? "Stepping..." : "Paused");
	else if (PauseWanted())
		ImGui::TextDisabled("Pausing...");
	bool whileOpen = g_pauseWhileOpen;
	if (ImGui::Checkbox("Pause while this window is open", &whileOpen))
		g_pauseWhileOpen = whileOpen;
	Tooltip("The game holds while the debug menu's window is open, and runs again when it closes (unless paused above).");
}

void GAME_DEBUG::DrawLevelCheckpoints()
{
	Apply();
	ImGui::SeparatorText("Level checkpoints");
	const uint32_t root = LevelRoot();
	if (!root)
	{
		ImGui::TextDisabled("No level running.");
		return;
	}
	const bool available = checkpoint_name && is_saving && (assign_entity_reference || jump_to_named_checkpoint);
	FlowReading flow;
	ReadFlow(flow);
	const bool waiting = flow.loadPending || flow.command || GetTickCount64() - g_jumpAskedAt < 1500;

	// Reload last checkpoint
	ImGui::BeginDisabled(waiting || !flow.snapshotNormal || flow.state != kStateGameplay);
	if (ImGui::Button("Reload last checkpoint"))
	{
		g_jumpAskedAt = GetTickCount64();
		DEBUG_MENU::OnEntityThread([root]() { SetStatus(Reload(root).message); });
	}
	ImGui::EndDisabled();
	Tooltip("Restarts from the checkpoint in memory, as a death does: the level goes back to how it was when that checkpoint was reached (the player, the AI, the scripts). Nothing is read from or written to the saves.");
	ImGui::SameLine();
	if (flow.snapshotNormal)
		Hint("mission %u, captured %.0f s of game time ago", flow.snapshotMission, (std::max)(0.0, flow.now - flow.snapshotTime));
	else
		Hint("no checkpoint in memory yet");

	CheckpointList list;
	{
		std::lock_guard<std::mutex> lock(g_listMutex);
		list = g_list;
	}
	if (list.root != root || list.count != flow.checkpoints || GetTickCount64() - g_listReadAt > 2000)
	{
		g_listReadAt = GetTickCount64();
		AskList();
	}
	if (!available)
	{
		ImGui::TextDisabled("Jumping is not available on this game release.");
		return;
	}
	if (flow.checkpoints == 0)
	{
		ImGui::TextDisabled("The level has activated none of its checkpoints yet.");
		return;
	}
	Hint("The level's own checkpoints, in the order its scripts activated them. A jump loads the checkpoint as the game would; if its mission part already ran since the level loaded, the part doesn't start again and the screen stays black.");
	Tooltip("A jump fires the checkpoint's own \"loaded\" outputs at the entity manager's next tick. A mission part's start fades in once per level load: load the level again to jump to a part that already ran.");
	if (!GAME_SAVES::On())
		Warning("Don't write saves is off: a checkpoint reached saves.");
	ImGui::BeginDisabled(waiting);
	// Up to twelve whole rows; the rest scroll
	if (ImGui::BeginChild("##levelcheckpoints", ImVec2(0.0f, ListHeight((std::min)(list.names.size(), static_cast<size_t>(12)))), ImGuiChildFlags_Borders))
	{
		// The current checkpoint brought into view the first time the list shows it, and again each time it changes
		const bool scrollToCurrent = list.root == root && list.current >= 0 && (g_scrolledRoot != root || g_scrolledCurrent != list.current);
		const unsigned largest = static_cast<unsigned>(list.names.size());
		for (size_t i = 0; i < list.names.size(); i++)
		{
			ImGui::PushID(static_cast<int>(i));
			const std::string name = list.names[i];
			const uint32_t index = static_cast<uint32_t>(i);
			// By its place in the list (any checkpoint), or by its name where this build has only the game's own jump
			const bool byIndex = assign_entity_reference != nullptr;
			ImGui::BeginDisabled(!byIndex && name.empty());
			if (ImGui::SmallButton("Jump"))
			{
				g_jumpAskedAt = GetTickCount64();
				if (byIndex)
					DEBUG_MENU::OnEntityThread([root, index, name]() { SetStatus(JumpIndex(root, index, name).message); });
				else
					DEBUG_MENU::OnEntityThread([root, name]() { SetStatus(JumpNamed(root, name).message); });
			}
			ImGui::EndDisabled();
			GAME_DEBUG::ListNumber(index + 1, largest);
			const std::string shown = ShownName(list, i);
			if (static_cast<int>(i) == list.current)
			{
				ImGui::TextColored(ImVec4(0.45f, 0.9f, 0.45f, 1.0f), "%s (current)", shown.c_str());
				if (scrollToCurrent)
				{
					// Centred in the list when it is out of view (or only partly in it); left alone when it can be seen
					const float top = ImGui::GetWindowPos().y + ImGui::GetStyle().WindowPadding.y;
					const float bottom = ImGui::GetWindowPos().y + ImGui::GetWindowHeight() - ImGui::GetStyle().WindowPadding.y;
					if (ImGui::GetItemRectMin().y < top || ImGui::GetItemRectMax().y > bottom)
						ImGui::SetScrollHereY(0.5f);
					g_scrolledRoot = root;
					g_scrolledCurrent = list.current;
				}
			}
			else if (name.empty())
				ImGui::TextDisabled("%s", shown.c_str());
			else
				ImGui::TextUnformatted(shown.c_str());
			if (name.empty())
				Tooltip(i < list.read.size() && list.read[i] ? "The level's scripts gave this checkpoint no name. Jump goes to it by its place in the list." :
					"Its name could not be read. Jump goes to it by its place in the list.");
			ImGui::PopID();
		}
	}
	ImGui::EndChild();
	ImGui::EndDisabled();
	if (waiting && (flow.loadPending || flow.command))
		ImGui::TextDisabled("A checkpoint load is pending...");
	const std::string status = Status();
	if (!status.empty())
		Hint("Last: %s", status.c_str());
}

void GAME_DEBUG::ListNumber(unsigned number, unsigned largest)
{
	char text[16], widest[16];
	snprintf(text, sizeof(text), "%u", number);
	snprintf(widest, sizeof(widest), "%u", (std::max)(number, largest));
	const float spacing = ImGui::GetStyle().ItemSpacing.x;
	ImGui::SameLine(0.0f, spacing + ImGui::CalcTextSize(widest).x - ImGui::CalcTextSize(text).x);
	ImGui::TextUnformatted(text);
	ImGui::SameLine(0.0f, spacing * 2.0f);
}

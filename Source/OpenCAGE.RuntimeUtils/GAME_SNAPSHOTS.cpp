#include "GAME_SNAPSHOTS.h"
#include "GAME_SAVES.h"
#include "DEBUG_MENU.h"
#include "LIVE_LINK.h"

#include <atomic>
#include <cctype>
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

	The checkpoint in memory is a block of 1 MB the game allocates once at startup; its header starts the block. The header
	says where the saved data starts (always 0x11DFC in), how much of the room after that is used, and holds a checksum of
	everything from its level name to the end of the data. The game writes the block only on the entity thread (a checkpoint
	save, in the entity manager's tick), and the game's save to disk reads it; a death adds to its death counts and works
	its checksum out again. A checkpoint restart (the game's flow restarting from "a checkpoint") checks that the block is
	the running level's, then restarts the level in place: it resets the game's managers, finds the checkpoint the block
	names, and reads the level's entities, its persistent variables and the managers' state back from the block.

	Everything that changes the game runs on the entity thread, between the level's frames: a save copies the block's used
	part, a restore copies a slot back and sets the reload going in the same step (so no checkpoint save can come between
	them). The slots are kept here in plain memory; nothing is written to or read from a file.
*/
namespace
{
	// The block (a pointer to it, and the pointer to its header, which the game sets to the same) and the game's walker of
	// it: while it is saving or loading the block, +0x814 is non-zero
	constexpr uintptr_t kMemory = 0x0134EF80;
	constexpr uintptr_t kSnapshot = 0x0134EF84;
	constexpr uintptr_t kWalker = 0x0134EF90;
	constexpr uint32_t kWalkerMode = 0x814;

	// The header: its mark ("IAAC"), checksum (two words), the room for data, where the data starts and how much is used,
	// the level name, the checkpoint (an entity handle: two ids), the mission, the game time it was captured at (a double),
	// the deaths in a row and in all, and whether it is only a mission's temporary state
	constexpr uint32_t kMark = 0x43414149;
	constexpr uint32_t kHeaderChecksum = 0x08;
	constexpr uint32_t kHeaderRoom = 0x14;
	constexpr uint32_t kHeaderDataAt = 0x18;
	constexpr uint32_t kHeaderUsed = 0x1C;
	constexpr uint32_t kHeaderLevel = 0x20;
	constexpr uint32_t kLevelLength = 0x80;
	constexpr uint32_t kHeaderCheckpoint = 0x240;
	constexpr uint32_t kHeaderMission = 0x280;
	constexpr uint32_t kHeaderTime = 0x288;
	constexpr uint32_t kHeaderDeathsInARow = 0x290;
	constexpr uint32_t kHeaderDeaths = 0x294;
	constexpr uint32_t kHeaderTemporary = 0x29A;
	constexpr uint32_t kDataAt = 0x11DFC;
	constexpr uint32_t kBlockSize = 0x100000;

	// Where the game keeps its pointer to its globals, and in them: the level manager, the game's flow and the saving
	constexpr uintptr_t kGameGlobals = 0x012F0C88;
	constexpr uint32_t kGlobalsLevelManager = 0x3C;
	constexpr uint32_t kGlobalsGameFlow = 0x48;
	constexpr uint32_t kGlobalsSaving = 0x78;

	// The game's flow: its state (4: gameplay) and a restart asked for (2: from the checkpoint in memory)
	constexpr uint32_t kFlowState = 0x08;
	constexpr uint32_t kFlowRestart = 0x10;
	constexpr int kStateGameplay = 4;
	constexpr int kRestartCheckpoint = 2;

	// The level manager: the level being played (a pointer: its index into the level names at +4); the level names (a
	// pointer to a pointer to them): a record each, the name first, and how many
	constexpr uint32_t kManagerCurrent = 0x40;
	constexpr uint32_t kLevelIndex = 0x04;
	constexpr uintptr_t kCommonGlobals = 0x017E1C4C;
	constexpr uint32_t kLevelNameSize = 0x46;
	constexpr uint32_t kLevelNameLength = 0x32;
	constexpr uint32_t kLevelNameCount = 0x2BC0;
	constexpr uint32_t kMaxLevels = 160;

	// The entity manager (a pointer to it): the running level's root composite id, the level's checkpoints (a list: a
	// pointer, their count at +8 and their entries at +0xC), the checkpoint the level is at, a save waiting for the player
	// (an entity handle), the command waiting for its next tick and a checkpoint load pending
	constexpr uintptr_t kEntityManager = 0x0134EF40;
	constexpr uint32_t kEntityRoot = 0x0C;
	constexpr uint32_t kEntityCheckpoints = 0x3C;
	constexpr uint32_t kListCount = 0x08;
	constexpr uint32_t kListEntries = 0x0C;
	constexpr uint32_t kEntityCurrentCheckpoint = 0x50;
	constexpr uint32_t kEntityPendingSave = 0x80;
	constexpr uint32_t kEntityCommand = 0xA4;
	constexpr uint32_t kEntityLoadPending = 0x10E;

	// The pause (a pointer to it): the pause bits asked for - the debug menu's own pause and the pause menu
	constexpr uintptr_t kPauseMode = 0x012F194C;
	constexpr uint32_t kPauseDebugMenu = 0x100;
	constexpr uint32_t kPauseMenuOpen = 0x400;

	// The game clock: the game time now (a double)
	constexpr uintptr_t kGameClock = 0x012410A8;

	// The slots, and the one that keeps the block a restore replaced
	constexpr int kSlots = 4;
	constexpr int kBefore = kSlots;

	typedef bool(__cdecl* t_check)();
	const auto snapshot_valid_normal = reinterpret_cast<t_check>(DEVTOOLS_RELATIVE_ADDRESS(0x00533A80));
	const auto checksum_up_to_date = reinterpret_cast<t_check>(DEVTOOLS_RELATIVE_ADDRESS(0x00375940));
	typedef void(__cdecl* t_recalc)();
	const auto work_out_checksum = reinterpret_cast<t_recalc>(DEVTOOLS_RELATIVE_ADDRESS(0x00534230));
	// The game's checksum of a block whose header is at the address given (worked out from the header alone)
	typedef uint64_t(__cdecl* t_checksum_of)(const void* header);
	const auto checksum_of = reinterpret_cast<t_checksum_of>(DEVTOOLS_RELATIVE_ADDRESS(0x00533C10));
	typedef bool(__thiscall* t_is_saving)(void* saving);
	const auto is_saving = reinterpret_cast<t_is_saving>(DEVTOOLS_RELATIVE_ADDRESS(0x0037B630));
	typedef const char*(__thiscall* t_checkpoint_name)(void* entities, uint32_t index);
	const auto checkpoint_name = reinterpret_cast<t_checkpoint_name>(DEVTOOLS_RELATIVE_ADDRESS(0x00522220));

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

	bool CopyGuarded(void* to, const void* from, size_t size)
	{
		__try
		{
			memcpy(to, from, size);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool SameGuarded(const void* a, const void* b, size_t size, bool& same)
	{
		__try
		{
			same = memcmp(a, b, size) == 0;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
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

	bool CallCheck(t_check function)
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

	bool CallRecalc()
	{
		__try
		{
			if (!work_out_checksum)
				return false;
			work_out_checksum();
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool CallChecksum(const void* header, uint64_t& out)
	{
		__try
		{
			if (!checksum_of)
				return false;
			out = checksum_of(header);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool CallIsSaving(void* saving, bool& busy)
	{
		__try
		{
			busy = saving && is_saving && is_saving(saving);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
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

	// The running level's name, as the game loads it and as the block names its level ("PRODUCTION\BSP_TORRENS")
	void RunningLevelName(char* out, size_t size)
	{
		out[0] = '\0';
		const uint8_t* manager = GlobalsField(kGlobalsLevelManager);
		const uint8_t* current = PointerAt(manager, kManagerCurrent);
		const uint8_t* names = PointerAt(Global(kCommonGlobals), 0);
		uint32_t index = 0, count = 0;
		if (!current || !names || !Read(current + kLevelIndex, index) || !Read(names + kLevelNameCount, count) || index >= count || index >= kMaxLevels)
			return;
		CopyText(names + index * kLevelNameSize, out, size, kLevelNameLength);
	}

	// The block: false when this build has no address for it or the game has not made it (the header and the block are the
	// same place in every build the game makes)
	uint8_t* Block()
	{
		uint8_t* memory = Global(kMemory);
		uint8_t* snapshot = Global(kSnapshot);
		return memory && memory == snapshot ? memory : nullptr;
	}

	bool Available()
	{
		return Address(kMemory) && Address(kSnapshot) && Address(kWalker) && snapshot_valid_normal && checksum_up_to_date && work_out_checksum && checksum_of && is_saving;
	}

	// The game saving or loading the block right now
	bool WalkerBusy()
	{
		const uintptr_t walker = Address(kWalker);
		uint32_t mode = 0;
		return !walker || !Read(reinterpret_cast<const uint8_t*>(walker) + kWalkerMode, mode) || mode != 0;
	}

	// What a header says (false when it is not a block the game would take)
	struct HeaderInfo
	{
		uint32_t size = 0;          // the header and the data in use
		uint32_t checksum[2] = {};
		char level[kLevelLength + 1] = {};
		uint32_t checkpoint[2] = {};
		uint32_t mission = 0, deathsInARow = 0, deaths = 0;
		double captured = 0.0;
		bool temporary = false;
	};

	// The game's own tests of a block (its mark, where its data starts, how much is used and that it fits, a checkpoint
	// named), on a header anywhere: the game's or a slot's copy of one (of 'available' bytes)
	bool ReadHeader(const uint8_t* header, size_t available, HeaderInfo& info)
	{
		uint32_t mark = 0, room = 0, dataAt = 0, used = 0;
		uint8_t temporary = 0;
		if (!header || available < kDataAt || !Read(header, mark) || mark != kMark || !Read(header + kHeaderRoom, room) || !Read(header + kHeaderDataAt, dataAt) ||
			!Read(header + kHeaderUsed, used))
			return false;
		if (dataAt != kDataAt || room == 0 || room > kBlockSize - kDataAt || used > room || dataAt + used > kBlockSize || dataAt + used > available)
			return false;
		Read(header + kHeaderChecksum, info.checksum[0]);
		Read(header + kHeaderChecksum + 4, info.checksum[1]);
		Read(header + kHeaderCheckpoint, info.checkpoint[0]);
		Read(header + kHeaderCheckpoint + 4, info.checkpoint[1]);
		if (!info.checkpoint[0] || !info.checkpoint[1])
			return false;
		Read(header + kHeaderMission, info.mission);
		Read(header + kHeaderTime, info.captured);
		Read(header + kHeaderDeathsInARow, info.deathsInARow);
		Read(header + kHeaderDeaths, info.deaths);
		Read(header + kHeaderTemporary, temporary);
		info.temporary = temporary != 0;
		CopyText(header + kHeaderLevel, info.level, sizeof(info.level), kLevelLength);
		info.size = dataAt + used;
		return true;
	}

	// Whether a block's checksum is the game's for it (worked out with the game's own function)
	bool ChecksumValid(const uint8_t* header, const HeaderInfo& info)
	{
		uint64_t sum = 0;
		return CallChecksum(header, sum) && static_cast<uint32_t>(sum) == info.checksum[0] && static_cast<uint32_t>(sum >> 32) == info.checksum[1];
	}

	// ---- The slots ----

	struct Slot
	{
		std::vector<uint8_t> block;  // the header and the data in use, as copied
		HeaderInfo info;
		bool checksumValid = false;  // the game's checksum held when it was copied
		uint32_t root = 0;           // the level and load it was taken in
		uint32_t session = 0;
		std::string checkpoint;      // the name of the checkpoint the level was at then (empty: not known)
		double takenAt = 0.0;        // the game time when it was copied
	};

	std::mutex g_mutex;
	Slot g_slots[kSlots + 1];

	// The level's loads, counted (render thread): a new one when another level runs, or none has for a while
	std::atomic<uint32_t> g_session = 1;
	uint32_t g_seenRoot = 0;
	ULONGLONG g_noLevelSince = 0;
	constexpr ULONGLONG kUnloadedAfter = 1500;

	// The tab's status line: what the last action came to
	std::mutex g_statusMutex;
	std::string g_status;

	void SetStatus(const std::string& text)
	{
		{
			std::lock_guard<std::mutex> lock(g_statusMutex);
			g_status = text;
		}
		DevTools::Log("Snapshot slots: %s", text.c_str());
	}

	std::string Status()
	{
		std::lock_guard<std::mutex> lock(g_statusMutex);
		return g_status;
	}

	// The restore last made, for the check once the restart has run: the block's checkpoint and capture time
	struct Expected
	{
		bool waiting = false;
		int slot = -1;
		uint32_t checkpoint[2] = {};
		double captured = 0.0;
		ULONGLONG at = 0;
	};
	std::mutex g_expectedMutex;
	Expected g_expected;

	void Empty(const char* why)
	{
		bool had = false;
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			for (Slot& slot : g_slots)
			{
				had = had || !slot.block.empty();
				slot = Slot();
			}
		}
		{
			std::lock_guard<std::mutex> lock(g_expectedMutex);
			g_expected = Expected();
		}
		if (had)
			SetStatus(std::string("slots emptied: ") + why);
	}

	// Render thread, every frame: a new load of a level empties the slots
	void TrackSession()
	{
		const uint32_t root = LevelRoot();
		if (root)
		{
			if (g_seenRoot && root != g_seenRoot)
			{
				g_session++;
				Empty("another level loaded");
			}
			g_seenRoot = root;
			g_noLevelSince = 0;
			return;
		}
		if (!g_seenRoot)
			return;
		const ULONGLONG now = GetTickCount64();
		if (!g_noLevelSince)
			g_noLevelSince = now;
		else if (now - g_noLevelSince > kUnloadedAfter)
		{
			g_session++;
			g_seenRoot = 0;
			g_noLevelSince = 0;
			Empty("the level unloaded");
		}
	}

	double GameTimeNow()
	{
		double time = 0.0;
		const uintptr_t at = Address(kGameClock);
		return at && Read(reinterpret_cast<const void*>(at), time) ? time : 0.0;
	}

	// Entity thread: a checkpoint's name, copied out (empty when it has none or it could not be read)
	void CheckpointNameAt(uint8_t* entities, uint32_t index, char* out, size_t size)
	{
		out[0] = '\0';
		__try
		{
			const char* text = checkpoint_name(entities, index);
			if (text)
				CopyText(text, out, size, size - 1);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			out[0] = '\0';
		}
	}

	// Entity thread: the name of the checkpoint the level is at (empty when not known)
	std::string CurrentCheckpointName()
	{
		uint8_t* entities = EntityManager();
		const uint8_t* list = PointerAt(entities, kEntityCheckpoints);
		const uint8_t* entries = PointerAt(list, kListEntries);
		const uint8_t* current = PointerAt(entities, kEntityCurrentCheckpoint);
		uint32_t count = 0;
		if (!entities || !entries || !current || !checkpoint_name || !Read(list + kListCount, count))
			return std::string();
		for (uint32_t i = 0; i < count && i < 256; i++)
		{
			if (PointerAt(entries, i * 4) != current)
				continue;
			char name[128];
			CheckpointNameAt(entities, i, name, sizeof(name));
			return name;
		}
		return std::string();
	}

	// The debug menu's own pause bit cleared (the pause functions' own bit operation, made atomic)
	void LetGoOfPause()
	{
		uint8_t* pause = Global(kPauseMode);
		if (!pause)
			return;
		__try
		{
			InterlockedAnd(reinterpret_cast<volatile LONG*>(pause), ~static_cast<LONG>(kPauseDebugMenu));
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
		}
	}

	// The game's own checks before a restart from the checkpoint in memory, as the reload makes them (entity thread): null
	// when it may go ahead
	const char* CannotRestart(uint8_t* entities, uint8_t* flow)
	{
		uint8_t pending = 0;
		uint32_t command = 0, save[2] = {};
		int state = -1, restart = 0;
		if (!entities || !Read(entities + kEntityLoadPending, pending) || !Read(entities + kEntityCommand, command) ||
			!Read(entities + kEntityPendingSave, save[0]) || !Read(entities + kEntityPendingSave + 4, save[1]))
			return "the entity manager cannot be read";
		if (!flow || !Read(flow + kFlowState, state) || !Read(flow + kFlowRestart, restart))
			return "the game's flow cannot be read";
		if (state != kStateGameplay || restart != 0)
			return "the game is not in gameplay (it is starting, restarting, over or unloading)";
		if (pending || command)
			return "a checkpoint load is already pending";
		if (save[0] || save[1])
			return "a checkpoint save is waiting for the player (it would be written over what is put back)";
		bool busy = false;
		if (!CallIsSaving(GlobalsField(kGlobalsSaving), busy))
			return "the saving cannot be read";
		if (busy)
			return "the game is saving";
		if (WalkerBusy())
			return "the game is saving or loading the checkpoint in memory";
		uint32_t bits = 0;
		uint8_t* pause = Global(kPauseMode);
		if (pause && Read(pause, bits) && (bits & kPauseMenuOpen))
			return "close the pause menu first";
		return nullptr;
	}

	// Entity thread: the block copied out (twice over, if the game changed it while it was being copied)
	bool CopyOut(const uint8_t* block, uint32_t size, std::vector<uint8_t>& out)
	{
		out.resize(size);
		for (int attempt = 0; attempt < 2; attempt++)
		{
			bool same = false;
			if (!CopyGuarded(out.data(), block, size) || !SameGuarded(out.data(), block, size, same))
				return false;
			if (same)
				return true;
		}
		return false;
	}

	std::string SlotName(int index)
	{
		return index == kBefore ? std::string("\"before the last restore\"") : "slot " + std::to_string(index + 1);
	}

	std::string Describe(const Slot& slot)
	{
		char text[320];
		snprintf(text, sizeof(text), "mission %u%s%s, captured at %.1f s of game time, %u deaths, %u KB%s", slot.info.mission, slot.checkpoint.empty() ? "" : ", ",
			slot.checkpoint.c_str(), slot.info.captured, slot.info.deaths, (slot.info.size + 1023) / 1024, slot.checksumValid ? "" : " (its checksum was not up to date)");
		return text;
	}

	// Entity thread: the checkpoint in memory copied into a slot
	DEBUG_MENU::CommandResult Save(int index, uint32_t root, uint32_t session)
	{
		if (!Available())
			return { false, "not available on this game release" };
		if (!root || LevelRoot() != root || g_session != session)
			return { false, "not saved: the level changed" };
		uint8_t* block = Block();
		HeaderInfo info;
		if (!block)
			return { false, "not saved: the game has no checkpoint memory" };
		if (WalkerBusy())
			return { false, "not saved: the game is saving or loading the checkpoint right now" };
		if (!CallCheck(snapshot_valid_normal) || !ReadHeader(block, kBlockSize, info) || info.temporary)
			return { false, "not saved: there is no checkpoint in memory (none reached since the level loaded, or only a mission's temporary state)" };
		char level[kLevelNameLength + 1];
		RunningLevelName(level, sizeof(level));
		if (!level[0] || _stricmp(level, info.level) != 0)
			return { false, std::string("not saved: the checkpoint in memory is another level's (") + info.level + ")" };
		Slot slot;
		if (!CopyOut(block, info.size, slot.block))
			return { false, "not saved: the checkpoint in memory kept changing while it was copied (try again)" };
		if (!ReadHeader(slot.block.data(), slot.block.size(), slot.info))
			return { false, "not saved: the copy is not a block the game would take" };
		slot.checksumValid = ChecksumValid(slot.block.data(), slot.info);
		slot.root = root;
		slot.session = session;
		slot.checkpoint = CurrentCheckpointName();
		slot.takenAt = GameTimeNow();
		const std::string text = Describe(slot);
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			g_slots[index] = std::move(slot);
		}
		return { true, "saved the checkpoint in memory to " + SlotName(index) + ": " + text };
	}

	// Entity thread: a slot copied back over the block, and the game's restart from the checkpoint in memory asked for
	DEBUG_MENU::CommandResult Restore(int index, uint32_t root, uint32_t session)
	{
		if (!Available())
			return { false, "not available on this game release" };
		if (!GAME_SAVES::On())
			return { false, "not restored: turn on Don't write saves first (Game > Saves) - the game writes the checkpoint in memory to the player's save on a death and at the next checkpoint" };
		if (!root || LevelRoot() != root || g_session != session)
			return { false, "not restored: the level changed" };
		std::vector<uint8_t> copy;
		HeaderInfo info;
		bool checksumValid = false;
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			const Slot& slot = g_slots[index];
			if (slot.block.empty())
				return { false, "not restored: " + SlotName(index) + " is empty" };
			if (slot.root != root || slot.session != session)
				return { false, "not restored: " + SlotName(index) + " is from another load of the level" };
			copy = slot.block;
			info = slot.info;
			checksumValid = slot.checksumValid;
		}
		HeaderInfo check;
		if (!ReadHeader(copy.data(), copy.size(), check) || check.size != copy.size())
			return { false, "not restored: " + SlotName(index) + " is not a block the game would take" };
		if (checksumValid && !ChecksumValid(copy.data(), check))
			return { false, "not restored: " + SlotName(index) + " no longer matches its checksum" };
		char level[kLevelNameLength + 1];
		RunningLevelName(level, sizeof(level));
		if (!level[0] || _stricmp(level, info.level) != 0)
			return { false, std::string("not restored: it is another level's (") + info.level + ") - the game would load that level" };
		uint8_t* block = Block();
		uint8_t* entities = EntityManager();
		uint8_t* flow = GlobalsField(kGlobalsGameFlow);
		if (!block)
			return { false, "not restored: the game has no checkpoint memory" };
		if (const char* why = CannotRestart(entities, flow))
			return { false, std::string("not restored: ") + why };

		// The block it replaces, kept to be put back the same way
		Slot before;
		HeaderInfo current;
		const bool keep = index != kBefore && CallCheck(snapshot_valid_normal) && ReadHeader(block, kBlockSize, current) && !current.temporary &&
			_stricmp(current.level, level) == 0 && CopyOut(block, current.size, before.block) && ReadHeader(before.block.data(), before.block.size(), before.info);

		if (!CopyGuarded(block, copy.data(), copy.size()))
		{
			// Put back what was there, as far as it was kept
			if (keep)
				CopyGuarded(block, before.block.data(), before.block.size());
			return { false, "the restore failed while copying (the checkpoint in memory was put back)" };
		}
		bool same = false;
		if (!SameGuarded(block, copy.data(), copy.size(), same) || !same)
			return { false, "the restore failed: the checkpoint in memory changed while it was copied (Reload last checkpoint would use whatever it now holds)" };
		bool recalculated = false;
		if (!CallCheck(checksum_up_to_date))
			recalculated = CallRecalc();
		if (keep)
		{
			before.checksumValid = ChecksumValid(before.block.data(), before.info);
			before.root = root;
			before.session = session;
			before.takenAt = GameTimeNow();
			std::lock_guard<std::mutex> lock(g_mutex);
			g_slots[kBefore] = std::move(before);
		}

		// The restart, as Reload last checkpoint asks for it: the debug menu's pause let go first (a restart never happens
		// paused), then the load marked pending and the flow's restart set to "checkpoint"
		bool paused = false;
		DEBUG_MENU::GetSwitch("game.paused", paused);
		if (paused)
			DEBUG_MENU::SetSwitch("game.paused", false);
		LetGoOfPause();
		if (!Write<uint8_t>(entities + kEntityLoadPending, 1) || !Write<int>(flow + kFlowRestart, kRestartCheckpoint))
			return { false, "the slot was put back, but the restart could not be asked for (Reload last checkpoint restarts from it)" };
		{
			std::lock_guard<std::mutex> lock(g_expectedMutex);
			g_expected.waiting = true;
			g_expected.slot = index;
			g_expected.checkpoint[0] = info.checkpoint[0];
			g_expected.checkpoint[1] = info.checkpoint[1];
			g_expected.captured = info.captured;
			g_expected.at = GetTickCount64();
		}
		char text[160];
		snprintf(text, sizeof(text), "restoring %s: mission %u, captured at %.1f s of game time%s%s", SlotName(index).c_str(), info.mission, info.captured,
			recalculated ? " (checksum worked out again)" : "", paused ? " (the pause was let go)" : "");
		return { true, std::string(text) + (keep ? "; the checkpoint it replaced is kept as \"before the last restore\"" : "") };
	}

	// Render thread, every frame: once the restart asked for has run, whether the checkpoint in memory is still the one put
	// back (a checkpoint the level reached in the meantime would have replaced it)
	void CheckRestore()
	{
		Expected expected;
		{
			std::lock_guard<std::mutex> lock(g_expectedMutex);
			expected = g_expected;
		}
		if (!expected.waiting)
			return;
		uint8_t* entities = EntityManager();
		uint8_t* flow = GlobalsField(kGlobalsGameFlow);
		uint8_t pending = 0;
		int state = -1, restart = 0;
		if (!entities || !flow || !Read(entities + kEntityLoadPending, pending) || !Read(flow + kFlowState, state) || !Read(flow + kFlowRestart, restart))
			return;
		if (pending || restart || state != kStateGameplay)
		{
			if (GetTickCount64() - expected.at < 20000)
				return;
			SetStatus("the restart asked for has not run after 20 s (is the game paused or in a menu?)");
		}
		else
		{
			HeaderInfo now;
			const uint8_t* block = Block();
			if (block && ReadHeader(block, kBlockSize, now) && now.checkpoint[0] == expected.checkpoint[0] && now.checkpoint[1] == expected.checkpoint[1] &&
				now.captured == expected.captured)
				SetStatus("restored " + SlotName(expected.slot) + ": the level restarted from it");
			else
				SetStatus("the level restarted, but the checkpoint in memory is not the one put back (a checkpoint save came first)");
		}
		std::lock_guard<std::mutex> lock(g_expectedMutex);
		g_expected.waiting = false;
	}

	// ---- Named for OpenCAGE and testing ----

	// A slot asked for by number (1 to 4), or "before" for the one a restore replaced; -1 when neither
	int ParseSlot(const std::string& argument, bool allowBefore)
	{
		std::string text;
		for (char c : argument)
			if (!isspace(static_cast<unsigned char>(c)))
				text += static_cast<char>(tolower(static_cast<unsigned char>(c)));
		if (allowBefore && (text == "before" || text == "b" || text == "0"))
			return kBefore;
		const int number = atoi(text.c_str());
		return number >= 1 && number <= kSlots ? number - 1 : -1;
	}

	std::string InfoText()
	{
		if (!Available())
			return "not available on this game release";
		std::string text;
		char line[512];
		HeaderInfo live;
		const uint8_t* block = Block();
		const bool valid = block && CallCheck(snapshot_valid_normal) && ReadHeader(block, kBlockSize, live);
		char level[kLevelNameLength + 1];
		RunningLevelName(level, sizeof(level));
		snprintf(line, sizeof(line), "level=%s\nlevel_root=%08X\nsession=%u\ndont_write_saves=%d\n", level, LevelRoot(), g_session.load(), GAME_SAVES::On() ? 1 : 0);
		text += line;
		if (valid)
		{
			snprintf(line, sizeof(line), "in_memory=mission %u level %s checkpoint %08X:%08X captured %.1f deaths %u in_a_row %u size %u temporary %d\n", live.mission, live.level,
				live.checkpoint[0], live.checkpoint[1], live.captured, live.deaths, live.deathsInARow, live.size, live.temporary ? 1 : 0);
			text += line;
		}
		else
			text += "in_memory=none\n";
		std::lock_guard<std::mutex> lock(g_mutex);
		for (int i = 0; i <= kSlots; i++)
		{
			const Slot& slot = g_slots[i];
			const std::string name = i == kBefore ? std::string("before") : std::to_string(i + 1);
			if (slot.block.empty())
				text += "slot_" + name + "=empty\n";
			else
			{
				snprintf(line, sizeof(line), "slot_%s=mission %u checkpoint %08X:%08X (%s) captured %.1f deaths %u size %u checksum %s taken_at %.1f session %u\n", name.c_str(),
					slot.info.mission, slot.info.checkpoint[0], slot.info.checkpoint[1], slot.checkpoint.empty() ? "?" : slot.checkpoint.c_str(), slot.info.captured,
					slot.info.deaths, slot.info.size, slot.checksumValid ? "valid" : "not up to date", slot.takenAt, slot.session);
				text += line;
			}
		}
		const std::string status = Status();
		if (!status.empty())
			text += "last=" + status + "\n";
		return text;
	}

	void RegisterOnce()
	{
		static bool registered = false;
		if (registered)
			return;
		registered = true;
		DEBUG_MENU::AddCommand("game.snapshot_info", "snapshot slots (experimental): the checkpoint in memory and each slot (1-4, and \"before\" the last restore), a line each", [](const std::string&)
		{
			return DEBUG_MENU::CommandResult{ true, InfoText() };
		});
		DEBUG_MENU::AddCommand("game.snapshot_save", "snapshot slots (experimental): copies the checkpoint in memory into a slot (1-4) - nothing is written to disk", [](const std::string& argument)
		{
			const int index = ParseSlot(argument, false);
			if (index < 0)
				return DEBUG_MENU::CommandResult{ false, "which slot? 1 to 4" };
			const uint32_t root = LevelRoot();
			const uint32_t session = g_session;
			return DEBUG_MENU::AnswerOnEntityThread([index, root, session]()
			{
				const DEBUG_MENU::CommandResult result = Save(index, root, session);
				SetStatus(result.message);
				return result;
			});
		});
		DEBUG_MENU::AddCommand("game.snapshot_restore", "snapshot slots (experimental): puts a slot (1-4, or \"before\" for the checkpoint the last restore replaced) back as the checkpoint in memory and restarts the level from it, as after a death (needs game.dont_write_saves on)", [](const std::string& argument)
		{
			const int index = ParseSlot(argument, true);
			if (index < 0)
				return DEBUG_MENU::CommandResult{ false, "which slot? 1 to 4, or before" };
			const uint32_t root = LevelRoot();
			const uint32_t session = g_session;
			return DEBUG_MENU::AnswerOnEntityThread([index, root, session]()
			{
				const DEBUG_MENU::CommandResult result = Restore(index, root, session);
				SetStatus(result.message);
				return result;
			});
		});
		DEBUG_MENU::AddCommand("game.snapshot_clear", "snapshot slots (experimental): empties a slot (1-4, before) or all of them (all)", [](const std::string& argument)
		{
			std::string text;
			for (char c : argument)
				if (!isspace(static_cast<unsigned char>(c)))
					text += static_cast<char>(tolower(static_cast<unsigned char>(c)));
			if (text == "all")
			{
				std::lock_guard<std::mutex> lock(g_mutex);
				for (Slot& slot : g_slots)
					slot = Slot();
				return DEBUG_MENU::CommandResult{ true, "emptied every slot" };
			}
			const int index = ParseSlot(text, true);
			if (index < 0)
				return DEBUG_MENU::CommandResult{ false, "which slot? 1 to 4, before or all" };
			std::lock_guard<std::mutex> lock(g_mutex);
			g_slots[index] = Slot();
			return DEBUG_MENU::CommandResult{ true, "emptied " + SlotName(index) };
		});
	}

	void Apply(ImDrawList* /*drawList*/)
	{
		RegisterOnce();
		TrackSession();
		CheckRestore();
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

	// A line of prose in grey (or amber), wrapped at the window's right edge rather than running past it
	void Hint(const char* text, bool warning = false)
	{
		ImGui::PushTextWrapPos(0.0f);
		if (warning)
			ImGui::TextColored(ImVec4(1.0f, 0.75f, 0.3f, 1.0f), "%s", text);
		else
			ImGui::TextDisabled("%s", text);
		ImGui::PopTextWrapPos();
	}

	ULONGLONG g_askedAt = 0; // render thread: a save or restore was asked for then (the buttons wait a moment)

	void AskSave(int index)
	{
		g_askedAt = GetTickCount64();
		const uint32_t root = LevelRoot();
		const uint32_t session = g_session;
		DEBUG_MENU::OnEntityThread([index, root, session]() { SetStatus(Save(index, root, session).message); });
	}

	void AskRestore(int index)
	{
		g_askedAt = GetTickCount64();
		const uint32_t root = LevelRoot();
		const uint32_t session = g_session;
		DEBUG_MENU::OnEntityThread([index, root, session]() { SetStatus(Restore(index, root, session).message); });
	}

	const bool g_overlayAdded = DEBUG_MENU::AddOverlay(9, Apply);
}

void GAME_SNAPSHOTS::DrawSlots()
{
	ImGui::SeparatorText("Snapshot slots (experimental)");
	if (!Available())
	{
		ImGui::TextDisabled("Not available on this game release.");
		return;
	}
	const uint32_t root = LevelRoot();
	if (!root)
	{
		ImGui::TextDisabled("No level running.");
		return;
	}
	Hint("Experimental. Save keeps a copy of the checkpoint in memory in a slot; Restore puts it back and restarts the level from it, as after a death. In memory only, for this load of this level.", true);
	Tooltip("For trying a scripted sequence again and again from an earlier checkpoint without real saves. The copy holds what a checkpoint restart reads back (the level's entities and persistent variables, the inventory, the AI's groups, the camera...). Not put back: what the game keeps on the player's profile (missions reached, statistics, achievements). The slots are emptied when the level unloads or another loads.");

	HeaderInfo live;
	const uint8_t* block = Block();
	const bool haveLive = block && !WalkerBusy() && CallCheck(snapshot_valid_normal) && ReadHeader(block, kBlockSize, live);
	char level[kLevelNameLength + 1];
	RunningLevelName(level, sizeof(level));
	const bool liveOurs = haveLive && !live.temporary && level[0] && _stricmp(level, live.level) == 0;
	const bool dontWrite = GAME_SAVES::On();
	const bool waiting = GetTickCount64() - g_askedAt < 1000;
	{
		std::lock_guard<std::mutex> lock(g_expectedMutex);
		if (g_expected.waiting)
			Hint("Restoring: waiting for the level to restart...");
	}
	if (!liveOurs)
		ImGui::TextDisabled("Nothing to save: no checkpoint of this level in memory yet.");
	if (!dontWrite)
		Hint("Restore needs Don't write saves on (Game > Saves): the game writes the checkpoint in memory to the player's save on a death and at the next checkpoint.", true);

	struct Row { int index; std::string text; bool empty; };
	std::vector<Row> rows;
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		for (int i = 0; i <= kSlots; i++)
		{
			const Slot& slot = g_slots[i];
			if (i == kBefore && slot.block.empty())
				continue;
			rows.push_back({ i, slot.block.empty() ? std::string("empty") : Describe(slot), slot.block.empty() });
		}
	}
	for (const Row& row : rows)
	{
		ImGui::PushID(row.index);
		if (row.index != kBefore)
		{
			ImGui::BeginDisabled(!liveOurs || waiting);
			if (ImGui::SmallButton("Save"))
				AskSave(row.index);
			ImGui::EndDisabled();
			Tooltip("Copies the checkpoint in memory (the last checkpoint the level reached) into this slot.");
			ImGui::SameLine();
		}
		ImGui::BeginDisabled(row.empty || !dontWrite || waiting);
		if (ImGui::SmallButton("Restore"))
			AskRestore(row.index);
		ImGui::EndDisabled();
		Tooltip("Puts this copy back as the checkpoint in memory and restarts the level from it, as after a death. The checkpoint it replaces is kept as \"before the last restore\".");
		ImGui::SameLine();
		ImGui::BeginDisabled(row.empty);
		if (ImGui::SmallButton("Clear"))
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			g_slots[row.index] = Slot();
		}
		ImGui::EndDisabled();
		ImGui::SameLine();
		const std::string label = row.index == kBefore ? "Before the last restore:" : std::to_string(row.index + 1) + ":";
		ImGui::PushTextWrapPos(0.0f);
		if (row.empty)
			ImGui::TextDisabled("%s %s", label.c_str(), row.text.c_str());
		else
			ImGui::Text("%s %s", label.c_str(), row.text.c_str());
		ImGui::PopTextWrapPos();
		ImGui::PopID();
	}
	const std::string status = Status();
	if (!status.empty())
		Hint(("Last: " + status).c_str());
}

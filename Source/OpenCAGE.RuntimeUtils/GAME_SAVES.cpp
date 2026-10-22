#include "GAME_SAVES.h"
#include "Config.h"
#include "DEBUG_MENU.h"

#include <atomic>
#include <cfloat>
#include <cstdio>
#include <cstring>
#include <mutex>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <detours.h>
#include <imgui.h>

/*
	Addresses and layouts are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).

	How the game saves, as read in the shipped build (the reasoning behind each level of the switch):
	- Its save calls (a checkpoint, the progression, the settings, the input settings) each check the game's own "saves
	  enabled" first and return at once when it is off - the path its save-error dialogs' "disable saving" takes. They
	  queue the save; the saving's per-frame update then hands it to the save system. The graphics settings are written
	  straight to a file in the player's own folder (the Steam user data folder on Steam).
	- The game also saves the progression from places that come before those checks: at boot, when there is no progression
	  file yet (it makes one) or the one there is damaged, and after each checkpoint. Out of disk space, it deletes old
	  saves. All of these reach the save system's queue.
	- The save system keeps one operation at a time: asked to save or delete, it waits for its next per-frame update,
	  which wakes its save thread to do it and, once it reports back, finishes the operation (noting the file in its list)
	  and makes the result the one the game reads. The update is the same code in all three builds; only the save thread
	  differs (the Steam Cloud on Steam, local files on GOG and the Epic Games Store).
	- Outside that queue, the Steam Cloud deletes or forgets files on its own (files it finds invalid as it lists the saves,
	  and when asked to delete them all), may import older saves, and a Survivor mode leaderboard uploads a file of its own.
	- Every file the game itself opens, deletes or moves goes through its imports of the Windows file functions (its C
	  runtime's, its own file classes' and its libraries'), which is how the GOG and Epic Games Store builds write their
	  saves.
*/
namespace
{
	// Where the game keeps its pointer to its globals; in them, the saving (a pointer)
	constexpr uintptr_t kGameGlobals = 0x012F0C88;
	constexpr uint32_t kGlobalsSaving = 0x78;

	// The saving's fields: its state (see kSaveStates), the game's own "saves enabled", a checkpoint save waiting, the
	// progression / settings / input saves waiting, the level last saved and its name's text id, the campaign's figures
	// (the storyline reached, the completion, the mission, the saves made, iron man), the last save's result (its status,
	// then the file's name at +0x10), and the player's deaths
	constexpr uint32_t kSaveState = 0x820;
	constexpr uint32_t kSavesEnabled = 0x824;
	constexpr uint32_t kCheckpointPending = 0xAC0;
	constexpr uint32_t kSettingsPending = 0xAC8;
	constexpr uint32_t kProgressionPending = 0xAC9;
	constexpr uint32_t kInputPending = 0xACA;
	constexpr uint32_t kLastSavedLevel = 0xAD6;
	constexpr uint32_t kSaveLevelNameId = 0xB16;
	constexpr uint32_t kCampaign = 0xC30;
	constexpr uint32_t kCampaignStoryline = 0x04;
	constexpr uint32_t kCampaignCompletion = 0x08;
	constexpr uint32_t kCampaignMission = 0x0C;
	constexpr uint32_t kCampaignSaves = 0x10;
	constexpr uint32_t kCampaignIronMan = 0x24;
	constexpr uint32_t kLastSave = 0xD00;
	constexpr uint32_t kTransferStatus = 0x00;
	constexpr uint32_t kTransferFileName = 0x10;
	constexpr uint32_t kDeaths = 0x4044;
	constexpr uint32_t kDeathsInARow = 0x4048;

	// The platform service (a pointer to it), whose +0x4F8 is the save system. The save system's fields: its operation
	// and its state (kPlatformSave... below), the operation's transfer (its status, then its file's name at +0x10 of it),
	// and what its save thread is asked to do and has reported
	constexpr uintptr_t kPlatformService = 0x017E4684;
	constexpr uint32_t kServiceSaveSystem = 0x4F8;
	constexpr uint32_t kPlatformOperation = 0x08;
	constexpr uint32_t kPlatformState = 0x0C;
	constexpr uint32_t kPlatformTransfer = 0x10;
	constexpr uint32_t kPlatformFileName = 0x20;
	constexpr uint32_t kPlatformThreadOperation = 0x2F4;
	constexpr uint32_t kPlatformThreadResult = 0x2F8;
	// Its states: a save asked for / waiting for the save thread, a delete asked for / waiting
	constexpr int kPlatformSave = 5;
	constexpr int kPlatformWaitSave = 6;
	constexpr int kPlatformDelete = 9;
	constexpr int kPlatformWaitDelete = 10;
	constexpr int kStatusSuccess = 1;

	// The game's own save states, for showing
	const char* const kSaveStates[] = {
		"idle", "setting up: loading the input settings", "setting up: waiting for the input settings", "setting up: listing the saves",
		"setting up: waiting for the list of saves", "setting up: loading the progression", "setting up: saving the progression",
		"asking: no save device", "asking: save damaged", "asking: save of another user", "asking: wrong save version",
		"asking: no space", "asking: no space (with size)", "asking: retry listing", "asking: retry loading", "asking: save error",
		"loading a checkpoint", "saving a checkpoint", "saving to the temporary buffer", "loading from the temporary buffer",
		"loading the input settings", "saving the progression (after a checkpoint)", "saving the progression", "saving the input settings",
		"deleting a checkpoint", "deleting files", "asking: checkpoint cache error", "asking: manual save", "asking: checkpoint save error",
		"asking: no space for the checkpoint", "asking: no space (deleting files)", "asking: progression save error",
		"asking: no space for the progression", "asking: no space for the progression (deleting files)", "saving the next checkpoint",
	};
	constexpr int kSaveStateCount = static_cast<int>(sizeof(kSaveStates) / sizeof(kSaveStates[0]));

	// ---- What was kept back ----

	// What a kept-back write was, and the level that stopped it
	enum class Kind
	{
		Progression, Settings, Input, Graphics,
		SavedCheckpoint, SavedMission, SavedCampaign, SavedProgression, SavedInput, SavedTemporary, Saved, Deleted,
		CloudWrite, CloudDelete, CloudForget, Leaderboard,
		File, kCount
	};
	enum Level { AtGameCall, AtSaveSystem, AtSteamCloud, AtFileGuard, kLevels };

	struct KindText { const char* name; Level level; };
	const KindText kKinds[] = {
		{ "progression", AtGameCall },
		{ "game settings", AtGameCall },
		{ "input settings", AtGameCall },
		{ "graphics settings", AtGameCall },
		{ "checkpoint", AtSaveSystem },
		{ "mission save", AtSaveSystem },
		{ "campaign save", AtSaveSystem },
		{ "progression", AtSaveSystem },
		{ "input settings", AtSaveSystem },
		{ "temporary mission state", AtSaveSystem },
		{ "save", AtSaveSystem },
		{ "delete", AtSaveSystem },
		{ "Steam Cloud write", AtSteamCloud },
		{ "Steam Cloud delete", AtSteamCloud },
		{ "Steam Cloud forget", AtSteamCloud },
		{ "leaderboard file upload", AtSteamCloud },
		{ "file", AtFileGuard },
	};
	static_assert(sizeof(kKinds) / sizeof(kKinds[0]) == static_cast<size_t>(Kind::kCount), "a text per kind");
	// How each level kept a write back, for the end of a note's line (the Steam Cloud's own say so in their names)
	const char* const kLevelNotes[kLevels] = { ", stopped at the game's save call", ", finished by the save system as if saved", "", ", refused by the file guard" };

	// What a save system's file is, by its name: the game names them PROGRESSION, CHSV<slot> (a checkpoint), MSSV<slot> (a
	// mission), SPSV<slot> (a single campaign), input.cfg, and AI_MLS.TMP / AI_TMP.TMP (a mission's temporary state)
	Kind SavedKind(const char* name)
	{
		if (_strnicmp(name, "CHSV", 4) == 0)
			return Kind::SavedCheckpoint;
		if (_strnicmp(name, "MSSV", 4) == 0)
			return Kind::SavedMission;
		if (_strnicmp(name, "SPSV", 4) == 0)
			return Kind::SavedCampaign;
		if (_strnicmp(name, "PROGRESSION", 11) == 0)
			return Kind::SavedProgression;
		if (_strnicmp(name, "input", 5) == 0)
			return Kind::SavedInput;
		if (_strnicmp(name, "AI_", 3) == 0)
			return Kind::SavedTemporary;
		return Kind::Saved;
	}

	struct Note
	{
		ULONGLONG tick = 0;
		Kind kind = Kind::Saved;
		char detail[160] = {};
	};

	constexpr size_t kNotes = 24;
	std::mutex g_notesMutex;
	Note g_notes[kNotes];
	size_t g_noteCount = 0;          // all time (the ring holds the last kNotes)
	std::atomic<uint32_t> g_total = 0;
	std::atomic<uint32_t> g_byLevel[kLevels] = {};
	std::atomic<ULONGLONG> g_lastTick = 0;

	std::atomic<bool> g_on = false;
	std::atomic<bool> g_notice = true;

	void Record(Kind kind, const char* detail)
	{
		{
			std::lock_guard<std::mutex> lock(g_notesMutex);
			Note& note = g_notes[g_noteCount % kNotes];
			note.tick = GetTickCount64();
			note.kind = kind;
			strncpy_s(note.detail, detail ? detail : "", _TRUNCATE);
			g_noteCount++;
		}
		g_total++;
		g_byLevel[kKinds[static_cast<int>(kind)].level]++;
		g_lastTick = GetTickCount64();
		DevTools::Log("Don't write saves: kept back %s%s%s", kKinds[static_cast<int>(kind)].name, detail && *detail ? " - " : "", detail ? detail : "");
	}

	std::vector<Note> RecentNotes()
	{
		std::lock_guard<std::mutex> lock(g_notesMutex);
		std::vector<Note> notes;
		const size_t count = g_noteCount < kNotes ? g_noteCount : kNotes;
		for (size_t i = 0; i < count; i++)
			notes.push_back(g_notes[(g_noteCount - 1 - i) % kNotes]); // newest first
		return notes;
	}

	// "checkpoint (CHSV01) - 12 s ago, finished by the save system as if saved"
	std::string NoteLine(const Note& note)
	{
		char line[300];
		const ULONGLONG ago = (GetTickCount64() - note.tick) / 1000;
		snprintf(line, sizeof(line), "%s%s%s%s - %llu s ago%s", kKinds[static_cast<int>(note.kind)].name, note.detail[0] ? " (" : "", note.detail,
			note.detail[0] ? ")" : "", ago, kLevelNotes[kKinds[static_cast<int>(note.kind)].level]);
		return line;
	}

	// ---- Reading and writing the game (guarded) ----

	uintptr_t Address(uintptr_t steam) { return DEVTOOLS_RELATIVE_ADDRESS(steam); }

	uint8_t* GameSave()
	{
		__try
		{
			const uintptr_t at = Address(kGameGlobals);
			uint8_t* globals = at ? *reinterpret_cast<uint8_t**>(at) : nullptr;
			return globals ? *reinterpret_cast<uint8_t**>(globals + kGlobalsSaving) : nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	uint8_t* SaveSystem()
	{
		__try
		{
			const uintptr_t at = Address(kPlatformService);
			uint8_t* service = at ? *reinterpret_cast<uint8_t**>(at) : nullptr;
			return service ? *reinterpret_cast<uint8_t**>(service + kServiceSaveSystem) : nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	void ClearByte(void* object, uint32_t offset)
	{
		__try
		{
			*(static_cast<volatile uint8_t*>(object) + offset) = 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
		}
	}

	// A file name in the game's memory, copied out
	void CopyName(const void* at, char* out, size_t size)
	{
		out[0] = '\0';
		__try
		{
			const char* text = static_cast<const char*>(at);
			size_t i = 0;
			for (; i + 1 < size && text[i]; i++)
				out[i] = text[i] >= 32 && text[i] < 127 ? text[i] : '?';
			out[i] = '\0';
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			out[0] = '\0';
		}
	}

	// The save system's update, before it runs: a save or delete waiting for it is finished as a success at once (the
	// result the save thread reports when it has done one), so the update completes it without waking the save thread.
	// 1 for a save, 2 for a delete, 0 for nothing.
	int CompleteQueued(uint8_t* platform, char* name, size_t size)
	{
		__try
		{
			if (*reinterpret_cast<volatile int*>(platform + kPlatformThreadOperation) != 0)
				return 0; // the save thread is busy (with something that started before the switch)
			const int state = *reinterpret_cast<volatile int*>(platform + kPlatformState);
			if (state != kPlatformSave && state != kPlatformDelete)
				return 0;
			CopyName(platform + kPlatformFileName, name, size);
			*reinterpret_cast<volatile int*>(platform + kPlatformThreadResult) = kStatusSuccess;
			*reinterpret_cast<volatile int*>(platform + kPlatformState) = state == kPlatformSave ? kPlatformWaitSave : kPlatformWaitDelete;
			return state == kPlatformSave ? 1 : 2;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	// The save thread's own save: it reports what it would on success, without writing
	void ReportSuccess(void* platform)
	{
		__try
		{
			*reinterpret_cast<volatile int*>(static_cast<uint8_t*>(platform) + kPlatformThreadResult) = kStatusSuccess;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
		}
	}

	// ---- The file guard: the game's imports of the Windows file functions ----

	// A path in one of the save folders: My Games\Alien Isolation (GOG), The Creative Assembly\Alien Isolation (Epic Games
	// Store), or the game's folder in the Steam user data (214490 is the game's Steam app id)
	template <typename Char>
	bool GuardedPath(const Char* path)
	{
		if (!path)
			return false;
		char lower[640];
		__try
		{
			size_t n = 0;
			for (; path[n] && n + 1 < sizeof(lower); n++)
			{
				unsigned int c = static_cast<unsigned int>(path[n]);
				if (c == '/')
					c = '\\';
				else if (c >= 'A' && c <= 'Z')
					c += 'a' - 'A';
				lower[n] = c < 128 ? static_cast<char>(c) : '?';
			}
			lower[n] = '\0';
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
		if (strstr(lower, "\\my games\\alien isolation") || strstr(lower, "\\the creative assembly\\alien isolation"))
			return true;
		const char* userData = strstr(lower, "\\userdata\\");
		return userData && strstr(userData, "\\214490\\");
	}

	// Whether an open creates, changes or deletes the file (anything but reading one that is there)
	bool WritesFile(DWORD access, DWORD disposition, DWORD flags)
	{
		const DWORD writing = GENERIC_WRITE | GENERIC_ALL | FILE_WRITE_DATA | FILE_APPEND_DATA | FILE_WRITE_EA | FILE_WRITE_ATTRIBUTES | DELETE | WRITE_DAC | WRITE_OWNER;
		return (access & writing) != 0 || disposition == CREATE_NEW || disposition == CREATE_ALWAYS || disposition == OPEN_ALWAYS ||
			disposition == TRUNCATE_EXISTING || (flags & FILE_FLAG_DELETE_ON_CLOSE) != 0;
	}

	void RecordFile(const char* what, const char* path)
	{
		char detail[160];
		snprintf(detail, sizeof(detail), "%s %s", what, path ? path : "?");
		Record(Kind::File, detail);
	}

	void RecordFile(const char* what, const wchar_t* path)
	{
		char narrow[260] = {};
		if (path)
			WideCharToMultiByte(CP_ACP, 0, path, -1, narrow, sizeof(narrow) - 1, nullptr, nullptr);
		RecordFile(what, narrow);
	}

	typedef HANDLE(WINAPI* t_CreateFileA)(LPCSTR, DWORD, DWORD, LPSECURITY_ATTRIBUTES, DWORD, DWORD, HANDLE);
	typedef HANDLE(WINAPI* t_CreateFileW)(LPCWSTR, DWORD, DWORD, LPSECURITY_ATTRIBUTES, DWORD, DWORD, HANDLE);
	typedef BOOL(WINAPI* t_PathA)(LPCSTR);
	typedef BOOL(WINAPI* t_PathW)(LPCWSTR);
	typedef BOOL(WINAPI* t_MoveA)(LPCSTR, LPCSTR);
	typedef BOOL(WINAPI* t_MoveW)(LPCWSTR, LPCWSTR);
	typedef BOOL(WINAPI* t_MoveExA)(LPCSTR, LPCSTR, DWORD);
	typedef BOOL(WINAPI* t_MoveExW)(LPCWSTR, LPCWSTR, DWORD);
	typedef BOOL(WINAPI* t_CopyA)(LPCSTR, LPCSTR, BOOL);
	typedef BOOL(WINAPI* t_CopyW)(LPCWSTR, LPCWSTR, BOOL);

	t_CreateFileA g_CreateFileA = nullptr;
	t_CreateFileW g_CreateFileW = nullptr;
	t_PathA g_DeleteFileA = nullptr;
	t_PathW g_DeleteFileW = nullptr;
	t_PathA g_RemoveDirectoryA = nullptr;
	t_PathW g_RemoveDirectoryW = nullptr;
	t_MoveA g_MoveFileA = nullptr;
	t_MoveW g_MoveFileW = nullptr;
	t_MoveExA g_MoveFileExA = nullptr;
	t_MoveExW g_MoveFileExW = nullptr;
	t_CopyA g_CopyFileA = nullptr;
	t_CopyW g_CopyFileW = nullptr;

	BOOL Refuse()
	{
		SetLastError(ERROR_ACCESS_DENIED);
		return FALSE;
	}

	HANDLE WINAPI GuardCreateFileA(LPCSTR path, DWORD access, DWORD share, LPSECURITY_ATTRIBUTES security, DWORD disposition, DWORD flags, HANDLE templateFile)
	{
		if (g_on && WritesFile(access, disposition, flags) && GuardedPath(path))
		{
			RecordFile("open for writing", path);
			SetLastError(ERROR_ACCESS_DENIED);
			return INVALID_HANDLE_VALUE;
		}
		return g_CreateFileA(path, access, share, security, disposition, flags, templateFile);
	}

	HANDLE WINAPI GuardCreateFileW(LPCWSTR path, DWORD access, DWORD share, LPSECURITY_ATTRIBUTES security, DWORD disposition, DWORD flags, HANDLE templateFile)
	{
		if (g_on && WritesFile(access, disposition, flags) && GuardedPath(path))
		{
			RecordFile("open for writing", path);
			SetLastError(ERROR_ACCESS_DENIED);
			return INVALID_HANDLE_VALUE;
		}
		return g_CreateFileW(path, access, share, security, disposition, flags, templateFile);
	}

	BOOL WINAPI GuardDeleteFileA(LPCSTR path)
	{
		if (g_on && GuardedPath(path))
		{
			RecordFile("delete", path);
			return Refuse();
		}
		return g_DeleteFileA(path);
	}

	BOOL WINAPI GuardDeleteFileW(LPCWSTR path)
	{
		if (g_on && GuardedPath(path))
		{
			RecordFile("delete", path);
			return Refuse();
		}
		return g_DeleteFileW(path);
	}

	BOOL WINAPI GuardRemoveDirectoryA(LPCSTR path)
	{
		if (g_on && GuardedPath(path))
		{
			RecordFile("remove folder", path);
			return Refuse();
		}
		return g_RemoveDirectoryA(path);
	}

	BOOL WINAPI GuardRemoveDirectoryW(LPCWSTR path)
	{
		if (g_on && GuardedPath(path))
		{
			RecordFile("remove folder", path);
			return Refuse();
		}
		return g_RemoveDirectoryW(path);
	}

	BOOL WINAPI GuardMoveFileA(LPCSTR from, LPCSTR to)
	{
		if (g_on && (GuardedPath(from) || GuardedPath(to)))
		{
			RecordFile("move", GuardedPath(to) ? to : from);
			return Refuse();
		}
		return g_MoveFileA(from, to);
	}

	BOOL WINAPI GuardMoveFileW(LPCWSTR from, LPCWSTR to)
	{
		if (g_on && (GuardedPath(from) || GuardedPath(to)))
		{
			RecordFile("move", GuardedPath(to) ? to : from);
			return Refuse();
		}
		return g_MoveFileW(from, to);
	}

	BOOL WINAPI GuardMoveFileExA(LPCSTR from, LPCSTR to, DWORD flags)
	{
		if (g_on && (GuardedPath(from) || GuardedPath(to)))
		{
			RecordFile("move", to && GuardedPath(to) ? to : from);
			return Refuse();
		}
		return g_MoveFileExA(from, to, flags);
	}

	BOOL WINAPI GuardMoveFileExW(LPCWSTR from, LPCWSTR to, DWORD flags)
	{
		if (g_on && (GuardedPath(from) || GuardedPath(to)))
		{
			RecordFile("move", to && GuardedPath(to) ? to : from);
			return Refuse();
		}
		return g_MoveFileExW(from, to, flags);
	}

	BOOL WINAPI GuardCopyFileA(LPCSTR from, LPCSTR to, BOOL failIfExists)
	{
		if (g_on && GuardedPath(to))
		{
			RecordFile("copy to", to);
			return Refuse();
		}
		return g_CopyFileA(from, to, failIfExists);
	}

	BOOL WINAPI GuardCopyFileW(LPCWSTR from, LPCWSTR to, BOOL failIfExists)
	{
		if (g_on && GuardedPath(to))
		{
			RecordFile("copy to", to);
			return Refuse();
		}
		return g_CopyFileW(from, to, failIfExists);
	}

	// The imports guarded: those AI.exe has are replaced in its import table (each build imports a few of them)
	struct Import
	{
		const char* function;
		void* guard;
		void** original;
		uintptr_t* slot;   // where the import table holds it (null: not imported, or not replaced)
	};
	Import g_imports[] = {
		{ "CreateFileA", reinterpret_cast<void*>(&GuardCreateFileA), reinterpret_cast<void**>(&g_CreateFileA), nullptr },
		{ "CreateFileW", reinterpret_cast<void*>(&GuardCreateFileW), reinterpret_cast<void**>(&g_CreateFileW), nullptr },
		{ "DeleteFileA", reinterpret_cast<void*>(&GuardDeleteFileA), reinterpret_cast<void**>(&g_DeleteFileA), nullptr },
		{ "DeleteFileW", reinterpret_cast<void*>(&GuardDeleteFileW), reinterpret_cast<void**>(&g_DeleteFileW), nullptr },
		{ "RemoveDirectoryA", reinterpret_cast<void*>(&GuardRemoveDirectoryA), reinterpret_cast<void**>(&g_RemoveDirectoryA), nullptr },
		{ "RemoveDirectoryW", reinterpret_cast<void*>(&GuardRemoveDirectoryW), reinterpret_cast<void**>(&g_RemoveDirectoryW), nullptr },
		{ "MoveFileA", reinterpret_cast<void*>(&GuardMoveFileA), reinterpret_cast<void**>(&g_MoveFileA), nullptr },
		{ "MoveFileW", reinterpret_cast<void*>(&GuardMoveFileW), reinterpret_cast<void**>(&g_MoveFileW), nullptr },
		{ "MoveFileExA", reinterpret_cast<void*>(&GuardMoveFileExA), reinterpret_cast<void**>(&g_MoveFileExA), nullptr },
		{ "MoveFileExW", reinterpret_cast<void*>(&GuardMoveFileExW), reinterpret_cast<void**>(&g_MoveFileExW), nullptr },
		{ "CopyFileA", reinterpret_cast<void*>(&GuardCopyFileA), reinterpret_cast<void**>(&g_CopyFileA), nullptr },
		{ "CopyFileW", reinterpret_cast<void*>(&GuardCopyFileW), reinterpret_cast<void**>(&g_CopyFileW), nullptr },
	};
	constexpr size_t kImportCount = sizeof(g_imports) / sizeof(g_imports[0]);

	// Where the main module's import table holds a kernel32 function (null when it doesn't import it by name)
	uintptr_t* FindImport(const char* function)
	{
		__try
		{
			uint8_t* base = reinterpret_cast<uint8_t*>(GetModuleHandleW(nullptr));
			const IMAGE_DOS_HEADER* dos = reinterpret_cast<const IMAGE_DOS_HEADER*>(base);
			const IMAGE_NT_HEADERS* nt = reinterpret_cast<const IMAGE_NT_HEADERS*>(base + dos->e_lfanew);
			const IMAGE_DATA_DIRECTORY& directory = nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
			if (!directory.VirtualAddress)
				return nullptr;
			for (const IMAGE_IMPORT_DESCRIPTOR* entry = reinterpret_cast<const IMAGE_IMPORT_DESCRIPTOR*>(base + directory.VirtualAddress); entry->Name; entry++)
			{
				if (_stricmp(reinterpret_cast<const char*>(base + entry->Name), "KERNEL32.dll") != 0 || !entry->OriginalFirstThunk)
					continue;
				const IMAGE_THUNK_DATA* names = reinterpret_cast<const IMAGE_THUNK_DATA*>(base + entry->OriginalFirstThunk);
				IMAGE_THUNK_DATA* slots = reinterpret_cast<IMAGE_THUNK_DATA*>(base + entry->FirstThunk);
				for (; names->u1.AddressOfData; names++, slots++)
				{
					if (IMAGE_SNAP_BY_ORDINAL(names->u1.Ordinal))
						continue;
					const IMAGE_IMPORT_BY_NAME* byName = reinterpret_cast<const IMAGE_IMPORT_BY_NAME*>(base + names->u1.AddressOfData);
					if (strcmp(reinterpret_cast<const char*>(byName->Name), function) == 0)
						return reinterpret_cast<uintptr_t*>(&slots->u1.Function);
				}
			}
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
		}
		return nullptr;
	}

	bool WriteSlot(uintptr_t* slot, uintptr_t value)
	{
		DWORD protection = 0;
		if (!VirtualProtect(slot, sizeof(*slot), PAGE_READWRITE, &protection))
			return false;
		*reinterpret_cast<volatile uintptr_t*>(slot) = value;
		VirtualProtect(slot, sizeof(*slot), protection, &protection);
		return true;
	}

	int GuardImports(bool guard)
	{
		int changed = 0;
		for (Import& import : g_imports)
		{
			if (guard)
			{
				uintptr_t* slot = FindImport(import.function);
				if (!slot || !*slot)
					continue;
				*import.original = reinterpret_cast<void*>(*slot);
				if (WriteSlot(slot, reinterpret_cast<uintptr_t>(import.guard)))
				{
					import.slot = slot;
					changed++;
				}
			}
			else if (import.slot)
			{
				// Put back only what is still ours
				if (*import.slot == reinterpret_cast<uintptr_t>(import.guard) && WriteSlot(import.slot, reinterpret_cast<uintptr_t>(*import.original)))
					changed++;
				import.slot = nullptr;
			}
		}
		return changed;
	}

	// A path that the guard refuses but that is nowhere near a real save folder (one that cannot exist, in the temporary
	// folder): opening it for writing through the game's own import tells whether the guard is in place - refused when
	// it is, "path not found" when it isn't. Nothing is written either way.
	std::string ProbeGuard()
	{
		const Import& createFile = g_imports[0];
		if (!createFile.slot)
			return "the file guard is not in place (the game's import of CreateFileA was not replaced)";
		char temp[MAX_PATH] = {};
		GetTempPathA(MAX_PATH, temp);
		char path[MAX_PATH + 96];
		snprintf(path, sizeof(path), "%sOpenCAGE guard probe %u\\My Games\\Alien Isolation\\probe.tmp", temp, GetCurrentProcessId());
		const t_CreateFileA viaGame = reinterpret_cast<t_CreateFileA>(*createFile.slot);
		const uint32_t before = g_byLevel[AtFileGuard];
		HANDLE handle = viaGame(path, GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
		const DWORD error = GetLastError();
		if (handle != INVALID_HANDLE_VALUE)
		{
			CloseHandle(handle);
			DeleteFileA(path);
			return "the file guard let a write through (unexpected)";
		}
		if (error == ERROR_ACCESS_DENIED && g_byLevel[AtFileGuard] != before)
			return "the file guard refused a write to a save folder path (probe in the temporary folder, nothing written)";
		char text[160];
		snprintf(text, sizeof(text), "the file guard did not refuse the probe (error %lu) - is the switch on?", error);
		return text;
	}

	// ---- The hooks ----

	struct Hook { void** original; void* detoured; const char* name; };
	const Hook kHooks[] = {
		{ reinterpret_cast<void**>(&GAME_SAVES::save_progression), reinterpret_cast<void*>(&GAME_SAVES::h_save_progression), "progression save" },
		{ reinterpret_cast<void**>(&GAME_SAVES::save_settings), reinterpret_cast<void*>(&GAME_SAVES::h_save_settings), "settings save" },
		{ reinterpret_cast<void**>(&GAME_SAVES::save_input_cfg), reinterpret_cast<void*>(&GAME_SAVES::h_save_input_cfg), "input settings save" },
		{ reinterpret_cast<void**>(&GAME_SAVES::save_engine_settings), reinterpret_cast<void*>(&GAME_SAVES::h_save_engine_settings), "graphics settings save" },
		{ reinterpret_cast<void**>(&GAME_SAVES::platform_update), reinterpret_cast<void*>(&GAME_SAVES::h_platform_update), "save system update" },
		{ reinterpret_cast<void**>(&GAME_SAVES::cloud_execute_save), reinterpret_cast<void*>(&GAME_SAVES::h_cloud_execute_save), "Steam Cloud save" },
		{ reinterpret_cast<void**>(&GAME_SAVES::cloud_execute_delete), reinterpret_cast<void*>(&GAME_SAVES::h_cloud_execute_delete), "Steam Cloud delete" },
		{ reinterpret_cast<void**>(&GAME_SAVES::cloud_immediate_delete), reinterpret_cast<void*>(&GAME_SAVES::h_cloud_immediate_delete), "Steam Cloud immediate delete" },
		{ reinterpret_cast<void**>(&GAME_SAVES::cloud_immediate_delete_all), reinterpret_cast<void*>(&GAME_SAVES::h_cloud_immediate_delete_all), "Steam Cloud delete all" },
		{ reinterpret_cast<void**>(&GAME_SAVES::cloud_immediate_delete_invalid), reinterpret_cast<void*>(&GAME_SAVES::h_cloud_immediate_delete_invalid), "Steam Cloud delete invalid" },
		{ reinterpret_cast<void**>(&GAME_SAVES::cloud_forget_invalid), reinterpret_cast<void*>(&GAME_SAVES::h_cloud_forget_invalid), "Steam Cloud forget invalid" },
		{ reinterpret_cast<void**>(&GAME_SAVES::cloud_delete_these), reinterpret_cast<void*>(&GAME_SAVES::h_cloud_delete_these), "Steam Cloud delete files" },
		{ reinterpret_cast<void**>(&GAME_SAVES::cloud_forget_these), reinterpret_cast<void*>(&GAME_SAVES::h_cloud_forget_these), "Steam Cloud forget files" },
		{ reinterpret_cast<void**>(&GAME_SAVES::cloud_import_save_file), reinterpret_cast<void*>(&GAME_SAVES::h_cloud_import_save_file), "Steam Cloud import" },
		{ reinterpret_cast<void**>(&GAME_SAVES::tus_execute_upload), reinterpret_cast<void*>(&GAME_SAVES::h_tus_execute_upload), "leaderboard file upload" },
	};
	constexpr size_t kHookCount = sizeof(kHooks) / sizeof(kHooks[0]);
	bool g_hooked[kHookCount] = {};
	bool g_attached = false;
	int g_guarded = 0; // imports replaced

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

	struct SaveReading
	{
		bool have = false;
		int state = 0;
		bool enabled = false;
		bool checkpointPending = false, settingsPending = false, progressionPending = false, inputPending = false;
		char lastLevel[65] = {};
		char levelNameId[33] = {};
		uint32_t deaths = 0, deathsInARow = 0;
		int storyline = 0;
		float completion = 0.0f, mission = 0.0f;
		uint32_t saves = 0;
		bool ironMan = false;
		int lastStatus = 0;
		char lastFile[33] = {};
		bool havePlatform = false;
		int platformOperation = 0, platformState = 0, threadOperation = 0;
	};

	void CopyText(const char* from, char* to, size_t size)
	{
		size_t i = 0;
		for (; i + 1 < size && from[i]; i++)
			to[i] = from[i] >= 32 && from[i] < 127 ? from[i] : '?';
		to[i] = '\0';
	}

	bool ReadSaves(SaveReading& out)
	{
		uint8_t* save = GameSave();
		uint8_t* platform = SaveSystem();
		__try
		{
			if (save)
			{
				out.state = *reinterpret_cast<const int*>(save + kSaveState);
				out.enabled = save[kSavesEnabled] != 0;
				out.checkpointPending = save[kCheckpointPending] != 0;
				out.settingsPending = save[kSettingsPending] != 0;
				out.progressionPending = save[kProgressionPending] != 0;
				out.inputPending = save[kInputPending] != 0;
				CopyText(reinterpret_cast<const char*>(save + kLastSavedLevel), out.lastLevel, sizeof(out.lastLevel));
				CopyText(reinterpret_cast<const char*>(save + kSaveLevelNameId), out.levelNameId, sizeof(out.levelNameId));
				out.deaths = *reinterpret_cast<const uint32_t*>(save + kDeaths);
				out.deathsInARow = *reinterpret_cast<const uint32_t*>(save + kDeathsInARow);
				const uint8_t* campaign = save + kCampaign;
				out.storyline = *reinterpret_cast<const int*>(campaign + kCampaignStoryline);
				out.completion = *reinterpret_cast<const float*>(campaign + kCampaignCompletion);
				out.mission = *reinterpret_cast<const float*>(campaign + kCampaignMission);
				out.saves = *reinterpret_cast<const uint32_t*>(campaign + kCampaignSaves);
				out.ironMan = campaign[kCampaignIronMan] != 0;
				out.lastStatus = *reinterpret_cast<const int*>(save + kLastSave + kTransferStatus);
				CopyText(reinterpret_cast<const char*>(save + kLastSave + kTransferFileName), out.lastFile, sizeof(out.lastFile));
				out.have = true;
			}
			if (platform)
			{
				out.platformOperation = *reinterpret_cast<const int*>(platform + kPlatformOperation);
				out.platformState = *reinterpret_cast<const int*>(platform + kPlatformState);
				out.threadOperation = *reinterpret_cast<const int*>(platform + kPlatformThreadOperation);
				out.havePlatform = true;
			}
			return out.have;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	const char* SaveStateName(int state)
	{
		return state >= 0 && state < kSaveStateCount ? kSaveStates[state] : "?";
	}

	const char* PlatformStateName(int state)
	{
		static const char* const names[] = { "idle", "choosing a device", "waiting for the device", "listing", "waiting for the list", "save asked",
			"saving", "load asked", "loading", "delete asked", "deleting", "system delete asked", "system deleting" };
		return state >= 0 && state < static_cast<int>(sizeof(names) / sizeof(names[0])) ? names[state] : "?";
	}

	const char* StorylineName(int storyline)
	{
		return storyline == 0 ? "start" : storyline == 1 ? "after the turn" : storyline == 2 ? "end" : "?";
	}

	std::string PendingText(const SaveReading& reading)
	{
		std::string text;
		auto add = [&](bool on, const char* name) { if (on) text += (text.empty() ? "" : ", ") + std::string(name); };
		add(reading.checkpointPending, "checkpoint");
		add(reading.progressionPending, "progression");
		add(reading.settingsPending, "settings");
		add(reading.inputPending, "input settings");
		return text.empty() ? "nothing" : text;
	}

	std::string StatusText()
	{
		std::string text;
		char line[320];
		snprintf(line, sizeof(line), "dont_write_saves=%d\nkept_back=%u\n", g_on ? 1 : 0, g_total.load());
		text += line;
		for (int level = 0; level < kLevels; level++)
		{
			snprintf(line, sizeof(line), "kept_back_at.%s=%u\n", level == AtGameCall ? "game_calls" : level == AtSaveSystem ? "save_system" : level == AtSteamCloud ? "steam_cloud" : "file_guard",
				g_byLevel[level].load());
			text += line;
		}
		int hooked = 0;
		for (bool on : g_hooked)
			hooked += on ? 1 : 0;
		snprintf(line, sizeof(line), "hooks=%d/%d\nfile_guard_imports=%d\n", hooked, static_cast<int>(kHookCount), g_guarded);
		text += line;
		SaveReading reading;
		if (ReadSaves(reading))
		{
			snprintf(line, sizeof(line), "save_state=%d %s\nsaves_enabled_by_game=%d\nwaiting=%s\nlast_saved_level=%s\nlast_save_file=%s status=%d\ndeaths=%u in_a_row=%u\n",
				reading.state, SaveStateName(reading.state), reading.enabled ? 1 : 0, PendingText(reading).c_str(), reading.lastLevel, reading.lastFile,
				reading.lastStatus, reading.deaths, reading.deathsInARow);
			text += line;
			if (reading.havePlatform)
			{
				snprintf(line, sizeof(line), "save_system=%d %s (operation %d, save thread %d)\n", reading.platformState, PlatformStateName(reading.platformState),
					reading.platformOperation, reading.threadOperation);
				text += line;
			}
		}
		else
		{
			text += "save_state=not readable (no saving yet)\n";
		}
		for (const Note& note : RecentNotes())
			text += "kept: " + NoteLine(note) + "\n";
		return text;
	}

	void RegisterOnce()
	{
		static bool registered = false;
		if (registered)
			return;
		registered = true;
		DEBUG_MENU::AddSwitch("game.dont_write_saves", &g_on);
		DEBUG_MENU::AddSwitch("game.save_notice", &g_notice);
		DEBUG_MENU::AddCommand("game.saves", "the saving's state, what Don't write saves kept back (newest first), and its hooks", [](const std::string&)
		{
			return DEBUG_MENU::CommandResult{ true, StatusText() };
		});
		DEBUG_MENU::AddCommand("game.saves_check", "checks the file guard: opens a save-folder-like path in the temporary folder for writing through the game's own import (refused when the guard is in place; nothing is written)", [](const std::string&)
		{
			return DEBUG_MENU::CommandResult{ true, ProbeGuard() };
		});
	}

	void DrawTab()
	{
		RegisterOnce();
		ImGui::SeparatorText("Don't write saves");
		const bool hooksIn = g_attached;
		bool on = g_on;
		ImGui::BeginDisabled(!hooksIn);
		if (ImGui::Checkbox("Don't write saves", &on))
			g_on = on;
		ImGui::EndDisabled();
		Tooltip("Nothing the game saves reaches the disk or the Steam Cloud while this is on: checkpoints and save stations, the progression (with the game settings), the input settings and the graphics settings. The game carries on as if it had saved, and loading still works (from what was saved before). DontWriteSaves=1 in OpenCAGE.RuntimeUtils.ini turns it on from the start, before the game first saves.");
		ImGui::SameLine();
		bool notice = g_notice;
		if (ImGui::Checkbox("Notice on screen", &notice))
			g_notice = notice;
		Tooltip("A line at the top of the screen for a few seconds each time a save is kept back.");
		ImGui::PushTextWrapPos(0.0f);
		if (!hooksIn)
			ImGui::TextDisabled("Not hooked: the debug menu's hooks are not attached.");

		const uint32_t total = g_total;
		if (g_on)
			ImGui::TextColored(ImVec4(0.45f, 0.9f, 0.45f, 1.0f), "On: nothing is written to the save folders or the Steam Cloud.");
		else
			ImGui::TextColored(ImVec4(1.0f, 0.75f, 0.3f, 1.0f), "Off: the game saves as usual (into the real saves).");
		ImGui::PopTextWrapPos();
		const std::vector<Note> notes = RecentNotes();
		if (total == 0)
			ImGui::TextDisabled("Nothing kept back yet.");
		else
		{
			ImGui::Text("Kept back: %u", total);
			ImGui::SameLine();
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextDisabled("(game calls %u, save system %u, Steam Cloud %u, file guard %u)", g_byLevel[AtGameCall].load(), g_byLevel[AtSaveSystem].load(),
				g_byLevel[AtSteamCloud].load(), g_byLevel[AtFileGuard].load());
			ImGui::PopTextWrapPos();
			if (!notes.empty())
				ImGui::TextWrapped("Last: %s", NoteLine(notes.front()).c_str());
			if (notes.size() > 1 && ImGui::CollapsingHeader("Everything kept back (newest first)"))
				for (const Note& note : notes)
					ImGui::BulletText("%s", NoteLine(note).c_str());
		}
		ImGui::PushStyleColor(ImGuiCol_Text, ImGui::GetStyleColorVec4(ImGuiCol_TextDisabled));
		ImGui::TextWrapped("Kept back at four levels: the game's own save calls, the save system's queue (finished as if saved), the Steam Cloud's own writes and deletes, and the game's file calls into its save folders (My Games\\Alien Isolation, The Creative Assembly\\Alien Isolation, its Steam user data).");
		ImGui::PopStyleColor();

		ImGui::SeparatorText("Saving");
		SaveReading reading;
		if (!ReadSaves(reading))
		{
			ImGui::TextDisabled("The game's saving is not up yet.");
			return;
		}
		ImGui::Text("State: %s", SaveStateName(reading.state));
		ImGui::SameLine();
		ImGui::TextDisabled(reading.enabled ? "(saving enabled by the game)" : "(saving disabled by the game)");
		Tooltip("The game's own switch, off after a save error's \"disable saving\" or before a profile is chosen. Don't write saves leaves it alone.");
		ImGui::Text("Waiting to save: %s", PendingText(reading).c_str());
		if (reading.havePlatform)
			ImGui::Text("Save system: %s", PlatformStateName(reading.platformState));
		if (reading.lastFile[0])
		{
			// A save Don't write saves finished as if saved reads as done to the game: say so
			bool keptBack = false;
			for (const Note& note : notes)
				if (g_on && kKinds[static_cast<int>(note.kind)].level == AtSaveSystem && _stricmp(note.detail, reading.lastFile) == 0)
				{
					keptBack = true;
					break;
				}
			ImGui::TextWrapped("Last save this run: %s%s", reading.lastFile, keptBack ? " - kept back (done, as far as the game knows)" :
				reading.lastStatus == 1 ? " - done" : reading.lastStatus == 2 ? " - failed" : "");
		}
		else
			ImGui::Text("Last save this run: none");
		if (reading.lastLevel[0])
		{
			ImGui::TextWrapped("Campaign's last saved level: %s", reading.lastLevel);
			Tooltip("The level of the checkpoint the campaign's progression was last saved with. It is kept in the progression, so it can be from an earlier run; DLC levels leave it as it is.");
		}
		ImGui::Text("Deaths: %u (%u in a row)", reading.deaths, reading.deathsInARow);
		ImGui::TextWrapped("Campaign: storyline %s, %.0f%% complete, mission %.1f, %u saves%s", StorylineName(reading.storyline), reading.completion * 100.0f, reading.mission,
			reading.saves, reading.ironMan ? ", iron man" : "");
		Tooltip("The campaign's figures as the game keeps them for its achievements and the save list.");
	}

	// Over the game: a line at the top for a few seconds after a save was kept back
	void DrawNotice(ImDrawList* drawList)
	{
		if (!g_notice || !g_on)
			return;
		const ULONGLONG last = g_lastTick;
		if (!last || GetTickCount64() - last > 4000)
			return;
		std::vector<Note> notes = RecentNotes();
		if (notes.empty())
			return;
		char text[200];
		snprintf(text, sizeof(text), "Kept back by Don't write saves: %s%s%s", kKinds[static_cast<int>(notes.front().kind)].name, notes.front().detail[0] ? " - " : "", notes.front().detail);
		const float size = ImGui::GetFontSize() * DEBUG_MENU::Scale();
		const ImVec2 extent = ImGui::GetFont()->CalcTextSizeA(size, FLT_MAX, 0.0f, text);
		const ImVec2 display = ImGui::GetIO().DisplaySize;
		// Its box (4 pixels around the text) goes under the notes already along the top of the screen
		const ImVec2 corner((display.x - extent.x) * 0.5f, DEBUG_MENU::BannerTop(extent.y + 8.0f) + 4.0f);
		drawList->AddRectFilled(ImVec2(corner.x - 8.0f, corner.y - 4.0f), ImVec2(corner.x + extent.x + 8.0f, corner.y + extent.y + 4.0f), IM_COL32(0, 0, 0, 170), 4.0f);
		drawList->AddText(ImGui::GetFont(), size, corner, IM_COL32(255, 210, 90, 255), text);
	}

	const bool g_tabAdded = DEBUG_MENU::AddTab("Saves", 74, DrawTab, "Game");
	const bool g_overlayAdded = DEBUG_MENU::AddOverlay(90, DrawNotice);
	const bool g_hooksAdded = DEBUG_MENU::AddHooks(GAME_SAVES::AttachHooks);
}

bool GAME_SAVES::On()
{
	return g_on;
}

uint32_t GAME_SAVES::SkippedCount()
{
	return g_total;
}

std::string GAME_SAVES::LastSkipped()
{
	const std::vector<Note> notes = RecentNotes();
	return notes.empty() ? std::string() : NoteLine(notes.front());
}

void GAME_SAVES::AttachHooks(bool attach)
{
	if (attach == g_attached)
		return;
	if (attach)
	{
		g_on = Config::IniInt("DontWriteSaves", 0) != 0;
		RegisterOnce();
		int attached = 0;
		// Hooked only where this build has the function (the Steam Cloud's are not in the GOG build)
		for (size_t i = 0; i < kHookCount; i++)
			if ((g_hooked[i] = *kHooks[i].original != nullptr))
			{
				DetourAttach(kHooks[i].original, kHooks[i].detoured);
				attached++;
			}
		g_guarded = GuardImports(true);
		g_attached = true;
		DevTools::Log("Don't write saves: %s; %d of %d save hooks attached, %d file imports guarded", g_on ? "on" : "off", attached, static_cast<int>(kHookCount), g_guarded);
	}
	else
	{
		g_attached = false;
		GuardImports(false);
		for (size_t i = 0; i < kHookCount; i++)
			if (g_hooked[i])
				DetourDetach(kHooks[i].original, kHooks[i].detoured);
	}
}

// ---- The game's save calls ----

__declspec(noinline)
void __fastcall GAME_SAVES::h_save_progression(void* _this, void* /*_EDX*/)
{
	if (g_on)
	{
		// The game's update asks again each frame while one is waiting: it is dropped, as a save would have taken it
		ClearByte(_this, kProgressionPending);
		ClearByte(_this, kSettingsPending);
		Record(Kind::Progression, "");
		return;
	}
	save_progression(_this);
}

__declspec(noinline)
void __fastcall GAME_SAVES::h_save_settings(void* _this, void* /*_EDX*/)
{
	if (g_on)
	{
		ClearByte(_this, kSettingsPending);
		Record(Kind::Settings, "kept in the progression file");
		return;
	}
	save_settings(_this);
}

__declspec(noinline)
void __fastcall GAME_SAVES::h_save_input_cfg(void* _this, void* /*_EDX*/)
{
	if (g_on)
	{
		ClearByte(_this, kInputPending);
		Record(Kind::Input, "");
		return;
	}
	save_input_cfg(_this);
}

__declspec(noinline)
void __fastcall GAME_SAVES::h_save_engine_settings(void* _this, void* /*_EDX*/)
{
	if (g_on)
	{
		Record(Kind::Graphics, "");
		return;
	}
	save_engine_settings(_this);
}

// ---- The save system ----

__declspec(noinline)
void __fastcall GAME_SAVES::h_platform_update(void* _this, void* /*_EDX*/)
{
	if (g_on)
	{
		char name[40];
		const int completed = CompleteQueued(static_cast<uint8_t*>(_this), name, sizeof(name));
		if (completed)
			Record(completed == 1 ? SavedKind(name) : Kind::Deleted, name);
	}
	platform_update(_this);
}

__declspec(noinline)
void __fastcall GAME_SAVES::h_cloud_execute_save(void* _this, void* /*_EDX*/)
{
	if (g_on)
	{
		char name[40];
		CopyName(static_cast<uint8_t*>(_this) + kPlatformFileName, name, sizeof(name));
		ReportSuccess(_this);
		Record(Kind::CloudWrite, name);
		return;
	}
	cloud_execute_save(_this);
}

__declspec(noinline)
void __fastcall GAME_SAVES::h_cloud_execute_delete(void* _this, void* /*_EDX*/)
{
	if (g_on)
	{
		char name[40];
		CopyName(static_cast<uint8_t*>(_this) + kPlatformFileName, name, sizeof(name));
		ReportSuccess(_this);
		Record(Kind::CloudDelete, name);
		return;
	}
	cloud_execute_delete(_this);
}

__declspec(noinline)
void __fastcall GAME_SAVES::h_cloud_immediate_delete(void* _this, void* /*_EDX*/, const char* file)
{
	if (g_on)
	{
		char name[40];
		CopyName(file, name, sizeof(name));
		Record(Kind::CloudDelete, name);
		return;
	}
	cloud_immediate_delete(_this, file);
}

__declspec(noinline)
void __fastcall GAME_SAVES::h_cloud_immediate_delete_all(void* _this, void* /*_EDX*/)
{
	if (g_on)
	{
		Record(Kind::CloudDelete, "every save");
		return;
	}
	cloud_immediate_delete_all(_this);
}

__declspec(noinline)
void __fastcall GAME_SAVES::h_cloud_immediate_delete_invalid(void* _this, void* /*_EDX*/)
{
	if (g_on)
	{
		Record(Kind::CloudDelete, "the files it finds invalid");
		return;
	}
	cloud_immediate_delete_invalid(_this);
}

__declspec(noinline)
void __fastcall GAME_SAVES::h_cloud_forget_invalid(void* _this, void* /*_EDX*/)
{
	if (g_on)
	{
		Record(Kind::CloudForget, "the files it finds invalid");
		return;
	}
	cloud_forget_invalid(_this);
}

__declspec(noinline)
void __fastcall GAME_SAVES::h_cloud_delete_these(void* _this, void* /*_EDX*/, const void* files)
{
	if (g_on)
	{
		Record(Kind::CloudDelete, "a list of files");
		return;
	}
	cloud_delete_these(_this, files);
}

__declspec(noinline)
void __fastcall GAME_SAVES::h_cloud_forget_these(void* _this, void* /*_EDX*/, const void* files)
{
	if (g_on)
	{
		Record(Kind::CloudForget, "a list of files");
		return;
	}
	cloud_forget_these(_this, files);
}

__declspec(noinline)
void __fastcall GAME_SAVES::h_cloud_import_save_file(void* _this, void* /*_EDX*/, const char* from, const char* to)
{
	if (g_on)
	{
		char name[40];
		CopyName(to, name, sizeof(name));
		Record(Kind::CloudWrite, name[0] ? name : "an imported save");
		return;
	}
	cloud_import_save_file(_this, from, to);
}

__declspec(noinline)
void __fastcall GAME_SAVES::h_tus_execute_upload(void* _this, void* /*_EDX*/)
{
	// Returning is what the upload does when its file write fails: nothing is shared or attached
	if (g_on)
	{
		Record(Kind::Leaderboard, "");
		return;
	}
	tus_execute_upload(_this);
}

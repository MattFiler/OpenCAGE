#include "AI_DEBUG.h"
#include "DEBUG_MENU.h"

#include <algorithm>
#include <atomic>
#include <cctype>
#include <cmath>
#include <cstddef>
#include <cstdio>
#include <cstring>
#include <mutex>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <imgui.h>

/*
	The game's AI config files read again while it runs: a section of the AI group's Tuning tab, and the command OpenCAGE's
	Alien Configs, Difficulty Settings and Viewcones editors send after each change (over the Live Link, with the Debug Menu
	on), so a change shows in the running game rather than the next time the game starts.

	The game reads three folders of DATA once, when it starts (not at each level load), and keeps what it read for as long as
	it runs:
	- DATA\ALIENCONFIGS: the alien setups (the director's numbers for each: the menace gauge, the sweeps, the vents, the
	  backstage roles), listed by ALIENCONFIGS.BML, each a file that may take its numbers from another (its template);
	- DATA\DIFFICULTYSETTINGS: each difficulty's changes to the alien setups' numbers, to the NPCs' senses, damage and
	  shooting, and to their view cones, listed the same way;
	- DATA\VIEW_CONE_SETS: the view cones of each set of senses (standard, human, sleeping, android), listed by
	  VIEWCONESETS.BML.
	Reading one again is what the game does as it starts: its own loader for the folder. Then, for the setups and the
	difficulty, the call the game makes on every character when the difficulty is changed, which has each alien work out
	its timings and its menace numbers again. The view cones need nothing more: the game looks them up each time it uses them.

	It runs on the AI thread, before a character's behaviour is updated: inside the game's AI step, which holds the lock the
	game's scripts and menus also take, so nothing of the game's uses these while they change. With care for what can go wrong:
	- the game keeps a cache in memory of the files it reads (in blocks, by name), which would hand it the old file: each
	  file's cached copy is dropped first, with the game's own call (as it does before it writes a file);
	- each file is checked first (it opens, and starts as a config file does): a file that is missing, or being written at
	  that moment, stops it with nothing changed;
	- the setups and the difficulties are numbered by name, the same for as long as the game runs, so what the game holds by
	  number (an alien's setup, the difficulty in force) stays right. The loader throws away every config first, so the old
	  ones are taken out of its reach beforehand and put back for any the files didn't give again (a file it couldn't read, a
	  name taken out of the list): no setup or difficulty in use is ever left empty. The rest are thrown away afterwards with
	  the game's own call;
	- the difficulty's loader also sets the difficulty in force, and the lowest played this campaign (what the difficulty
	  achievements go by), back to its start-up ones: both are put back as they were;
	- the view cones are read into the tables the game already has; a set left with no cones (a file it couldn't read) is
	  put back as it was.
	Each request is queued for the AI thread; requests made before it comes round are done together (paused, or with no
	level running, they wait for it). The loader keeps its list file in memory each time (a kilobyte or two, as the game's
	own loader does).

	RuntimeUtils' own readers of these (the Characters group's Alien tab, the view cone overlay) read them afresh each frame,
	guarded, and keep no pointer from one frame to the next.

	Addresses and layouts are the Steam retail AI.exe's (see GameAddresses.h for the GOG and Epic Games Store builds).
*/
namespace
{
	// Where the game keeps its pointer to its globals; the byte after it is set while its characters can be read
	constexpr uintptr_t kGameGlobals = 0x012F0C88;
	constexpr uint32_t kCharactersValid = 0x04;
	// In the globals (pointers): the characters, the view cone tables, the alien setups and the difficulties
	constexpr uint32_t kGlobalsCharacters = 0x08;
	constexpr uint32_t kGlobalsViewcones = 0x14;
	constexpr uint32_t kGlobalsSetups = 0x84;
	constexpr uint32_t kGlobalsDifficulty = 0x88;

	// The characters: two lists of them (a pointer to their pointers, then how many) - the players, then the others alive -
	// gone through in that order, as the game does when the difficulty changes
	constexpr uint32_t kPlayers = 0x80;
	constexpr uint32_t kAlive = 0xD0;
	constexpr uint32_t kListCount = 0x04;
	constexpr uint32_t kMaxPerList = 64;
	// A character's fields: what it was spawned as (a pointer: its class at +4) and its alien setup (a number in the setups)
	constexpr uint32_t kCharacterSpawnedAs = 0x64;
	constexpr uint32_t kSpawnedAsClass = 0x04;
	constexpr uint32_t kCharacterSetup = 0x42A4;
	constexpr int kAlienClass = 1;

	// A folder of configs (the alien setups, the difficulties): its own calls for the folder and its list file (in its table
	// of calls), its configs (a list: the pointers at +8, how many at +0xC, room for how many at +0x10) and their names (64
	// characters each, at +0x18; how many at +0x1C), each config numbered by its place in both
	constexpr uint32_t kCallListFile = 0x20;
	constexpr uint32_t kCallFolder = 0x24;
	constexpr uint32_t kConfigs = 0x08;
	constexpr uint32_t kConfigCount = 0x0C;
	constexpr uint32_t kConfigRoom = 0x10;
	constexpr uint32_t kNames = 0x18;
	constexpr uint32_t kNameCount = 0x1C;
	constexpr uint32_t kNameSize = 0x40;
	constexpr uint32_t kMaxConfigs = 64;
	// The difficulties, after their configs: the one in force (its number in the list), the number the game shows for it,
	// the lowest played this campaign, and whether it was ever changed (a byte)
	constexpr uint32_t kDifficultyHandle = 0x24;
	constexpr uint32_t kDifficultyValue = 0x28;
	constexpr uint32_t kDifficultyLowest = 0x2C;
	constexpr uint32_t kDifficultyChanged = 0x30;

	// The view cone tables: eight sets from +4, 0x14 bytes each - the cones (a pointer), how many, room for how many and
	// which is the longest; each cone 0x50 bytes: its length, the cosines of half its width and height, and whether it is
	// in use
	constexpr uint32_t kViewconeSets = 0x04;
	constexpr uint32_t kViewconeSetSize = 0x14;
	constexpr int kViewconeSetCount = 8;
	constexpr uint32_t kSetCones = 0x00;
	constexpr uint32_t kSetConeCount = 0x04;
	constexpr uint32_t kSetLongest = 0x0C;
	constexpr uint32_t kConeSize = 0x50;
	constexpr uint32_t kConeLength = 0x00;
	constexpr uint32_t kConeHorizontalDot = 0x08;
	constexpr uint32_t kConeVerticalDot = 0x0C;
	constexpr uint32_t kConeValid = 0x4C;
	constexpr uint32_t kMaxCones = 32;
	// The view cones' files, as the game names them: the list, and the four sets its loader takes from it
	const char* const kViewconeList = "data\\view_cone_sets\\ViewconeSets.bml";
	const char* const kViewconeFiles[] = {
		"data\\view_cone_sets\\VIEWCONESET_STANDARD.bml",
		"data\\view_cone_sets\\VIEWCONESET_HUMAN.bml",
		"data\\view_cone_sets\\VIEWCONESET_SLEEPING.bml",
		"data\\view_cone_sets\\VIEWCONESET_ANDROID.bml",
	};

	// The game's cache of the files it reads (a pointer to it)
	constexpr uintptr_t kFileCache = 0x0134B790;
	constexpr size_t kPathSize = 0x104;

	// The game's calls (each on the object given):
	typedef void(__thiscall* t_call)(void* object);
	// - read a folder of configs again from its list file (the setups' or the difficulties', by the folder's own calls)
	const auto load_configs = reinterpret_cast<t_call>(DEVTOOLS_RELATIVE_ADDRESS(0x002D0680));
	// - throw away every config of a list (each destroyed and freed as the game does it)
	const auto free_setups = reinterpret_cast<t_call>(DEVTOOLS_RELATIVE_ADDRESS(0x003956A0));
	const auto free_difficulties = reinterpret_cast<t_call>(DEVTOOLS_RELATIVE_ADDRESS(0x00326930));
	// - read the view cones again into the tables, then work out each set's longest
	const auto load_viewcones = reinterpret_cast<t_call>(DEVTOOLS_RELATIVE_ADDRESS(0x00311050));
	// - a character's alien setup changed: an alien works out its timings and its menace numbers again
	const auto setup_changed = reinterpret_cast<t_call>(DEVTOOLS_RELATIVE_ADDRESS(0x00385D50));
	// - the difficulty changed, for one character: an alien as above, then its menace numbers for the difficulty
	const auto difficulty_changed = reinterpret_cast<t_call>(DEVTOOLS_RELATIVE_ADDRESS(0x00393EA0));
	// - the name a file is read by (the game's own remapping, as it opens a file)
	typedef void(__cdecl* t_resolve)(char* out, const char* path, unsigned size);
	const auto resolve_file_name = reinterpret_cast<t_resolve>(DEVTOOLS_RELATIVE_ADDRESS(0x004B0290));
	// - drop a file's blocks from the cache, by that name
	typedef bool(__thiscall* t_forget)(void* cache, const char* name);
	const auto forget_cached = reinterpret_cast<t_forget>(DEVTOOLS_RELATIVE_ADDRESS(0x00466A60));
	// A folder's own call naming its folder or its list file (text the game keeps for as long as it runs)
	typedef const char*(__thiscall* t_namer)(const void* configs);

	// Pure reads for the report: the director's numbers for a setup at the difficulty in force, and how far a character hears
	typedef float(__thiscall* t_setup_number)(const void* setups, uint32_t setup);
	typedef unsigned(__thiscall* t_setup_count)(const void* setups, uint32_t setup);
	const auto max_menaces = reinterpret_cast<t_setup_count>(DEVTOOLS_RELATIVE_ADDRESS(0x002E2D50));
	struct Number
	{
		const char* key;
		uintptr_t steam;
	};
	const Number kNumbers[] = {
		{ "gauge_seconds_to_fill", 0x002E2DC0 },
		{ "menace_deemed_time", 0x002E2D00 },
		{ "sweep_box_half_length", 0x002E2E10 },
		{ "sweep_box_half_width", 0x002E2EF0 },
		{ "sweep_increase_duration", 0x002E2CB0 },
		{ "sweep_decrease_duration", 0x002E2C60 },
		{ "vent_attract_time_min", 0x002E2F40 },
		{ "vent_attract_time_max", 0x002E2F90 },
		{ "backstage_role_timeout_min", 0x002E2FE0 },
		{ "backstage_role_timeout_max", 0x002E3030 },
		{ "backstage_ambush_timeout", 0x002E3080 },
		{ "backstage_min_distance", 0x002E30D0 },
		{ "backstage_killtrap_time", 0x002E3120 },
	};
	typedef float(__thiscall* t_hearing_distance)(const void* character);
	const auto hearing_distance = reinterpret_cast<t_hearing_distance>(DEVTOOLS_RELATIVE_ADDRESS(0x00375F10));

	// What can be read again
	enum Part : uint32_t
	{
		kAlien = 1,
		kDifficulty = 2,
		kViewcones = 4,
		kAll = kAlien | kDifficulty | kViewcones,
	};

	// How a line about each part starts
	struct PartName
	{
		uint32_t part;
		const char* label;
	};
	const PartName kParts[] = {
		{ kAlien, "alien setups" },
		{ kDifficulty, "difficulty" },
		{ kViewcones, "view cones" },
	};

	// ---- Reading and writing the game (guarded: a level may be loading or unloading) ----

	template <typename T>
	bool Read(const void* at, T& out)
	{
		__try
		{
			out = *reinterpret_cast<const volatile T*>(at);
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

	bool CopyBytes(void* to, const void* from, size_t size)
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

	bool CallOn(t_call call, void* object)
	{
		__try
		{
			call(object);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	uint8_t* GlobalsField(uint32_t offset)
	{
		const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kGameGlobals);
		uint8_t* globals = nullptr;
		uint8_t* field = nullptr;
		if (!at || !Read(reinterpret_cast<const void*>(at), globals) || !globals || !Read(globals + offset, field))
			return nullptr;
		return field;
	}

	bool CharactersReadable()
	{
		const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kGameGlobals);
		uint8_t valid = 0;
		return at && Read(reinterpret_cast<const void*>(at + kCharactersValid), valid) && valid != 0;
	}

	// A folder's call naming its folder or its list file, copied
	bool CallNamer(const uint8_t* configs, uint32_t slot, char* out, size_t size)
	{
		__try
		{
			const void* const* calls = *reinterpret_cast<const void* const* const*>(configs);
			const t_namer namer = reinterpret_cast<t_namer>(calls[slot / sizeof(void*)]);
			const char* text = namer(configs);
			if (!text)
				return false;
			strncpy_s(out, size, text, _TRUNCATE);
			return out[0] != '\0';
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// A folder's configs: where their pointers are, how many, and room for how many
	struct ConfigList
	{
		uint8_t** configs = nullptr;
		uint32_t count = 0;
		uint32_t room = 0;
	};

	bool ReadConfigList(const uint8_t* owner, ConfigList& out)
	{
		out = ConfigList();
		return Read(owner + kConfigs, out.configs) && Read(owner + kConfigCount, out.count) && Read(owner + kConfigRoom, out.room) &&
			out.count <= kMaxConfigs && out.room >= out.count && (out.configs || out.count == 0);
	}

	uint8_t* ConfigAt(const ConfigList& list, uint32_t index)
	{
		uint8_t* config = nullptr;
		return index < list.count && Read(list.configs + index, config) ? config : nullptr;
	}

	// A config's name (as its file is named, without the folder or ".bml")
	std::string NameOf(const uint8_t* owner, uint32_t index)
	{
		uint8_t* names = nullptr;
		uint32_t count = 0;
		char name[kNameSize + 1] = {};
		if (!Read(owner + kNames, names) || !Read(owner + kNameCount, count) || !names || index >= count ||
			!CopyBytes(name, names + index * kNameSize, kNameSize))
			return std::string();
		name[kNameSize] = '\0';
		return name;
	}

	// ---- The files ----

	bool CallResolve(char* out, const char* path)
	{
		__try
		{
			resolve_file_name(out, path, static_cast<unsigned>(kPathSize));
			out[kPathSize - 1] = '\0';
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// The name the game reads a file by (its remapping applied, as it opens one)
	std::string Resolve(const std::string& path)
	{
		char out[kPathSize] = {};
		if (!resolve_file_name || !CallResolve(out, path.c_str()) || !out[0])
			return path;
		return out;
	}

	bool CallForget(void* cache, const char* name)
	{
		__try
		{
			forget_cached(cache, name);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// Drops the game's cached copy of a file (by the name it reads it by), so its loader reads the file itself
	void Forget(const std::string& resolved)
	{
		const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kFileCache);
		void* cache = nullptr;
		if (forget_cached && at && Read(reinterpret_cast<const void*>(at), cache) && cache)
			CallForget(cache, resolved.c_str());
	}

	// Whether a file opens and starts as the game's config files do ("xml", then where its three blocks start: in order, and
	// inside the file); why not when it doesn't. Busy: it is being written at that moment (worth asking again shortly).
	bool LooksLikeConfig(const std::string& path, std::string& why, bool& busy)
	{
		busy = false;
		HANDLE file = CreateFileA(path.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING,
			FILE_ATTRIBUTE_NORMAL, nullptr);
		if (file == INVALID_HANDLE_VALUE)
		{
			const DWORD error = GetLastError();
			busy = error == ERROR_SHARING_VIOLATION || error == ERROR_LOCK_VIOLATION;
			why = error == ERROR_FILE_NOT_FOUND || error == ERROR_PATH_NOT_FOUND ? "is missing" : busy ? "is being written" : "can't be opened (error " + std::to_string(error) + ")";
			return false;
		}
		LARGE_INTEGER size = {};
		uint8_t header[16] = {};
		DWORD got = 0;
		const bool read = GetFileSizeEx(file, &size) && ReadFile(file, header, sizeof(header), &got, nullptr) && got == sizeof(header);
		CloseHandle(file);
		if (!read)
		{
			why = "can't be read";
			return false;
		}
		uint32_t blocks[3] = {};
		memcpy(blocks, header + 4, sizeof(blocks));
		if (memcmp(header, "xml", 4) != 0 || blocks[0] < sizeof(header) || blocks[0] >= blocks[1] || blocks[1] >= blocks[2] ||
			static_cast<long long>(blocks[2]) >= size.QuadPart)
		{
			why = "isn't a config file the game can read";
			return false;
		}
		return true;
	}

	// Checks every file a loader will read (a file being written is asked about again a few times, briefly), then drops the
	// game's cached copies of them all. False, and why, when one won't do.
	bool CheckFiles(const std::vector<std::string>& files, std::string& why)
	{
		std::vector<std::string> resolved;
		for (const std::string& file : files)
		{
			const std::string name = Resolve(file);
			std::string problem;
			bool busy = false;
			bool good = LooksLikeConfig(name, problem, busy);
			for (int attempt = 0; !good && busy && attempt < 4; attempt++)
			{
				Sleep(15);
				good = LooksLikeConfig(name, problem, busy);
			}
			if (!good)
			{
				why = file + " " + problem;
				return false;
			}
			resolved.push_back(name);
		}
		for (const std::string& name : resolved)
			Forget(name);
		return true;
	}

	// ---- Reading again (AI thread) ----

	// What the game's call to throw away every config of a list reads of the list's owner: the configs at +8 and how many
	// at +0xC (and it sets how many to none when done) - given the old configs the new ones replaced
	struct Discard
	{
		uint32_t unused[2] = {};
		void** configs = nullptr;
		uint32_t count = 0;
	};
	static_assert(offsetof(Discard, configs) == kConfigs && offsetof(Discard, count) == kConfigCount, "laid out as the game's lists");

	struct ConfigsReloaded
	{
		bool done = false;
		std::string problem;          // why not, when not done
		int held = 0;                 // configs held after
		int renewed = 0;              // configs held before that the files gave again
		std::string kept;             // the names of those that kept their old numbers (the files didn't give them)
		bool loaderStopped = false;   // the game's loader stopped part way (a file it couldn't make sense of)
		std::string inForce;          // the difficulty: the one in force, by name
	};

	ConfigsReloaded ReloadConfigs(bool difficulty)
	{
		ConfigsReloaded out;
		uint8_t* owner = GlobalsField(difficulty ? kGlobalsDifficulty : kGlobalsSetups);
		const t_call freeAll = difficulty ? free_difficulties : free_setups;
		if (!load_configs || !freeAll || !resolve_file_name)
		{
			out.problem = "not available on this release of the game";
			return out;
		}
		char folder[kPathSize] = {}, listFile[kPathSize] = {};
		ConfigList before;
		if (!owner || !CallNamer(owner, kCallFolder, folder, sizeof(folder)) || !CallNamer(owner, kCallListFile, listFile, sizeof(listFile)) ||
			!ReadConfigList(owner, before))
		{
			out.problem = "the game's own couldn't be read";
			return out;
		}

		// The files the loader reads: the list, and each config held (a template among them), by its name
		std::vector<uint8_t*> old(before.count, nullptr);
		std::vector<std::string> names(before.count);
		std::vector<std::string> files = { std::string(folder) + listFile };
		for (uint32_t i = 0; i < before.count; i++)
		{
			old[i] = ConfigAt(before, i);
			names[i] = NameOf(owner, i);
			if (old[i] && !names[i].empty())
				files.push_back(std::string(folder) + names[i] + ".bml");
		}
		if (!CheckFiles(files, out.problem))
			return out;

		// The difficulty in force and the campaign's lowest, which its loader sets back to its start-up ones
		int handle = -1, value = -1, lowest = -1;
		uint8_t changed = 0;
		if (difficulty && (!Read(owner + kDifficultyHandle, handle) || !Read(owner + kDifficultyValue, value) ||
			!Read(owner + kDifficultyLowest, lowest) || !Read(owner + kDifficultyChanged, changed)))
		{
			out.problem = "the difficulty in force couldn't be read";
			return out;
		}

		// The old configs taken out of the loader's reach (it throws away every one it holds first)
		for (uint32_t i = 0; i < before.count; i++)
			if (old[i])
				Write<uint8_t*>(before.configs + i, nullptr);
		out.loaderStopped = !CallOn(load_configs, owner);

		ConfigList after;
		if (!ReadConfigList(owner, after))
			after = before;
		// A list left shorter than before (a name taken out of the list file) is made as long again, if it has the room
		if (after.count < before.count && after.configs && after.room >= before.count)
		{
			for (uint32_t i = after.count; i < before.count; i++)
				Write<uint8_t*>(after.configs + i, nullptr);
			if (Write<uint32_t>(owner + kConfigCount, before.count))
				after.count = before.count;
		}
		// Each old config the files gave again is thrown away; any they didn't is put back where it was
		std::vector<void*> discard;
		for (uint32_t i = 0; i < before.count; i++)
		{
			if (!old[i])
				continue;
			if (ConfigAt(after, i))
			{
				discard.push_back(old[i]);
				out.renewed++;
			}
			else if (i < after.count && Write<uint8_t*>(after.configs + i, old[i]))
				out.kept += (out.kept.empty() ? "" : ", ") + (names[i].empty() ? "#" + std::to_string(i) : names[i]);
		}
		if (!discard.empty())
		{
			Discard thrown;
			thrown.configs = discard.data();
			thrown.count = static_cast<uint32_t>(discard.size());
			CallOn(freeAll, &thrown);
		}
		for (uint32_t i = 0; i < after.count; i++)
			if (ConfigAt(after, i))
				out.held++;

		if (difficulty)
		{
			Write(owner + kDifficultyHandle, handle);
			Write(owner + kDifficultyValue, value);
			Write(owner + kDifficultyLowest, lowest);
			Write(owner + kDifficultyChanged, changed);
			out.inForce = handle >= 0 ? NameOf(owner, static_cast<uint32_t>(handle)) : std::string();
		}
		out.done = true;
		return out;
	}

	// A view cone set as it was: its cones (where, how many, the longest) and their bytes
	struct ConeSet
	{
		uint8_t* cones = nullptr;
		uint32_t count = 0;
		uint32_t longest = 0;
		std::vector<uint8_t> bytes;
		bool copied = false;
	};

	bool ReadConeSet(const uint8_t* tables, int set, ConeSet& out)
	{
		const uint8_t* at = tables + kViewconeSets + set * kViewconeSetSize;
		out.copied = false;
		if (!Read(at + kSetCones, out.cones) || !Read(at + kSetConeCount, out.count) || !Read(at + kSetLongest, out.longest))
			return false;
		if (out.count > kMaxCones || (!out.cones && out.count))
			return false;
		out.bytes.resize(out.count * kConeSize);
		out.copied = out.count == 0 || CopyBytes(out.bytes.data(), out.cones, out.bytes.size());
		return true;
	}

	struct ViewconesReloaded
	{
		bool done = false;
		std::string problem;
		int cones[kViewconeSetCount] = {};
		std::string restored;         // the sets put back as they were
		bool loaderStopped = false;
	};

	ViewconesReloaded ReloadViewcones()
	{
		ViewconesReloaded out;
		uint8_t* tables = GlobalsField(kGlobalsViewcones);
		if (!load_viewcones || !resolve_file_name)
		{
			out.problem = "not available on this release of the game";
			return out;
		}
		if (!tables)
		{
			out.problem = "the game's own couldn't be read";
			return out;
		}
		std::vector<std::string> files = { kViewconeList };
		for (const char* file : kViewconeFiles)
			files.push_back(file);
		ConeSet before[kViewconeSetCount];
		for (int set = 0; set < kViewconeSetCount; set++)
			if (!ReadConeSet(tables, set, before[set]))
			{
				out.problem = "the game's own couldn't be read";
				return out;
			}
		if (!CheckFiles(files, out.problem))
			return out;

		out.loaderStopped = !CallOn(load_viewcones, tables);
		for (int set = 0; set < kViewconeSetCount; set++)
		{
			ConeSet after;
			const bool read = ReadConeSet(tables, set, after);
			const bool emptied = before[set].count > 0 && (!read || after.count == 0);
			// Put back as it was (in the same place: the loader fills the tables it has)
			if ((out.loaderStopped || emptied) && before[set].copied && read && after.cones == before[set].cones)
			{
				uint8_t* at = tables + kViewconeSets + set * kViewconeSetSize;
				if ((before[set].count == 0 || CopyBytes(before[set].cones, before[set].bytes.data(), before[set].bytes.size())) &&
					Write(at + kSetConeCount, before[set].count) && Write(at + kSetLongest, before[set].longest))
				{
					out.restored += std::string(out.restored.empty() ? "" : ", ") + AI_DEBUG::ViewconeSetName(set);
					after.count = before[set].count;
				}
			}
			out.cones[set] = static_cast<int>(after.count);
		}
		out.done = true;
		return out;
	}

	// The characters the game runs: the players, then the others alive (as the game goes through them itself)
	int GameCharacters(uint8_t* out[], int max)
	{
		uint8_t* manager = GlobalsField(kGlobalsCharacters);
		if (!manager || !CharactersReadable())
			return 0;
		int found = 0;
		for (uint32_t list : { kPlayers, kAlive })
		{
			uint8_t** characters = nullptr;
			uint32_t count = 0;
			if (!Read(manager + list, characters) || !Read(manager + list + kListCount, count) || !characters)
				continue;
			for (uint32_t i = 0; i < count && i < kMaxPerList && found < max; i++)
			{
				uint8_t* character = nullptr;
				if (Read(characters + i, character) && character)
					out[found++] = character;
			}
		}
		return found;
	}

	bool IsAlien(const uint8_t* character)
	{
		const uint8_t* spawnedAs = nullptr;
		int characterClass = -1;
		return Read(character + kCharacterSpawnedAs, spawnedAs) && spawnedAs && Read(spawnedAs + kSpawnedAsClass, characterClass) &&
			characterClass == kAlienClass;
	}

	// An alien's setup is one the game holds (the calls work it out from it)
	bool SetupHeld(const uint8_t* alien)
	{
		uint8_t* setups = GlobalsField(kGlobalsSetups);
		uint32_t setup = 0;
		ConfigList list;
		return setups && Read(alien + kCharacterSetup, setup) && ReadConfigList(setups, list) && ConfigAt(list, setup) != nullptr;
	}

	// Has the characters take up what was read: with the difficulty, the game's call for a difficulty change on every one
	// (which covers the aliens' setups too); with the setups alone, the call for a setup change on every alien
	int TellCharacters(bool difficulty, int& aliens)
	{
		aliens = 0;
		const t_call call = difficulty ? difficulty_changed : setup_changed;
		if (!call)
			return -1;
		uint8_t* characters[2 * kMaxPerList] = {};
		const int count = GameCharacters(characters, 2 * kMaxPerList);
		int told = 0;
		for (int i = 0; i < count; i++)
		{
			const bool alien = IsAlien(characters[i]);
			if ((!difficulty && !alien) || (alien && !SetupHeld(characters[i])))
				continue;
			if (CallOn(call, characters[i]))
			{
				told++;
				if (alien)
					aliens++;
			}
		}
		return told;
	}

	// ---- Requests ----

	std::atomic<uint32_t> g_pending = 0;     // the parts asked for and not yet read again
	std::atomic<bool> g_reloading = false;   // AI thread: reading again now
	std::mutex g_lastMutex;
	DEBUG_MENU::CommandResult g_last = { true, "" };

	const char* LabelOf(uint32_t part)
	{
		for (const PartName& name : kParts)
			if (name.part == part)
				return name.label;
		return "?";
	}

	// AI thread: reads the parts again, and has the characters take them up. A line per part, and one for the characters.
	DEBUG_MENU::CommandResult Reload(uint32_t parts)
	{
		DEBUG_MENU::CommandResult result;
		std::string lines;
		auto line = [&lines](const std::string& text) { lines += (lines.empty() ? "" : "\n") + text; };
		bool setupsChanged = false, difficultyChanged = false;
		g_reloading = true;

		if (parts & kViewcones)
		{
			const ViewconesReloaded reloaded = ReloadViewcones();
			if (!reloaded.done)
			{
				line(std::string(LabelOf(kViewcones)) + ": not read again - " + reloaded.problem);
				result.ok = false;
			}
			else
			{
				std::string sets;
				for (int set = 0; set < kViewconeSetCount; set++)
					if (reloaded.cones[set] > 0)
						sets += (sets.empty() ? "" : ", ") + std::string(AI_DEBUG::ViewconeSetName(set)) + " " + std::to_string(reloaded.cones[set]);
				std::string text = std::string(LabelOf(kViewcones)) + ": read again (cones: " + (sets.empty() ? "none" : sets) + ")";
				if (reloaded.loaderStopped)
					text += "; the game's loader stopped part way through the files";
				if (!reloaded.restored.empty())
					text += "; kept as they were (their files gave none): " + reloaded.restored;
				line(text);
				if (reloaded.loaderStopped)
					result.ok = false;
			}
		}

		for (uint32_t part : { static_cast<uint32_t>(kAlien), static_cast<uint32_t>(kDifficulty) })
		{
			if (!(parts & part))
				continue;
			const bool difficulty = part == kDifficulty;
			const ConfigsReloaded reloaded = ReloadConfigs(difficulty);
			if (!reloaded.done)
			{
				line(std::string(LabelOf(part)) + ": not read again - " + reloaded.problem);
				result.ok = false;
				continue;
			}
			std::string text = std::string(LabelOf(part)) + ": read again (" + std::to_string(reloaded.held) + (difficulty ? " difficulties" : " setups");
			if (difficulty && !reloaded.inForce.empty())
				text += "; " + reloaded.inForce + " kept in force, and the campaign's lowest as it was";
			text += ")";
			if (reloaded.loaderStopped)
				text += "; the game's loader stopped part way through the files";
			if (!reloaded.kept.empty())
				text += "; kept their old numbers (their files gave none): " + reloaded.kept;
			line(text);
			if (reloaded.loaderStopped)
				result.ok = false;
			(difficulty ? difficultyChanged : setupsChanged) = true;
		}

		if (setupsChanged || difficultyChanged)
		{
			int aliens = 0;
			const int told = TellCharacters(difficultyChanged, aliens);
			if (told < 0)
				line("characters: not told (not available on this release of the game)");
			else if (told == 0)
				line(difficultyChanged ? "characters: none running to take them up" : "characters: no alien running to take them up");
			else
				line("characters: " + std::to_string(told) + " took them up (" + std::to_string(aliens) + (aliens == 1 ? " alien)" : " aliens)"));
		}

		g_reloading = false;
		result.message = lines;
		DevTools::Log("Debug menu: config files read again - %s", lines.c_str());
		return result;
	}

	// AI thread: everything asked for so far, at once. A request whose parts an earlier one's work already read again (both
	// queued before the AI thread came round) answers with what that came to.
	DEBUG_MENU::CommandResult RunPending()
	{
		const uint32_t parts = g_pending.exchange(0);
		if (!parts)
		{
			std::lock_guard<std::mutex> lock(g_lastMutex);
			return g_last;
		}
		const DEBUG_MENU::CommandResult result = Reload(parts);
		std::lock_guard<std::mutex> lock(g_lastMutex);
		g_last = result;
		return result;
	}

	std::string Lower(std::string text)
	{
		for (char& c : text)
			c = static_cast<char>(tolower(static_cast<unsigned char>(c)));
		return text;
	}

	// The parts named ("alien", "difficulty", "viewcones" or "all", or several; none: all); 0 and the word not known when one isn't
	uint32_t PartsFrom(const std::string& argument, std::string& unknown)
	{
		uint32_t parts = 0;
		std::string word;
		const std::string text = Lower(argument) + " ";
		for (char c : text)
		{
			if (!isspace(static_cast<unsigned char>(c)) && c != ',' && c != '+' && c != '|')
			{
				word += c;
				continue;
			}
			if (word.empty())
				continue;
			if (word == "all")
				parts |= kAll;
			else if (word == "alien" || word == "aliens" || word == "alienconfigs" || word == "alien_configs" || word == "setups")
				parts |= kAlien;
			else if (word == "difficulty" || word == "difficulties" || word == "difficultysettings" || word == "difficulty_settings")
				parts |= kDifficulty;
			else if (word == "viewcones" || word == "view_cones" || word == "viewcone" || word == "cones" || word == "view_cone_sets")
				parts |= kViewcones;
			else
			{
				unknown = word;
				return 0;
			}
			word.clear();
		}
		return parts ? parts : static_cast<uint32_t>(kAll);
	}

	// ---- The report (render thread: this frame's characters) ----

	float SetupNumber(uintptr_t steam, const void* setups, uint32_t setup, bool& ok)
	{
		const auto number = reinterpret_cast<t_setup_number>(DEVTOOLS_RELATIVE_ADDRESS(steam));
		__try
		{
			ok = number != nullptr;
			return number ? number(setups, setup) : 0.0f;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			ok = false;
			return 0.0f;
		}
	}

	bool SetupCount(const void* setups, uint32_t setup, unsigned& out)
	{
		__try
		{
			if (!max_menaces)
				return false;
			out = max_menaces(setups, setup);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool Hearing(const void* character, float& out)
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

	std::string Format(const char* format, double value)
	{
		char text[48];
		snprintf(text, sizeof(text), format, value);
		return text;
	}

	void ReportSetups(std::string& out)
	{
		const uint8_t* setups = GlobalsField(kGlobalsSetups);
		ConfigList list;
		if (!setups || !ReadConfigList(setups, list))
		{
			out += "alien setups: couldn't be read\n";
			return;
		}
		std::string lines;
		int held = 0;
		for (uint32_t i = 0; i < list.count; i++)
		{
			if (!ConfigAt(list, i))
				continue;
			held++;
			std::string row = NameOf(setups, i) + ":";
			for (const Number& number : kNumbers)
			{
				bool ok = false;
				const float value = SetupNumber(number.steam, setups, i, ok);
				if (ok && std::isfinite(value))
					row += std::string(" ") + number.key + "=" + Format("%g", value);
			}
			unsigned menaces = 0;
			if (SetupCount(setups, i, menaces))
				row += " max_menaces=" + std::to_string(menaces);
			lines += row + "\n";
		}
		out += "alien setups: " + std::to_string(held) + " (the director's numbers for each, at the difficulty in force)\n" + lines;
	}

	void ReportDifficulty(std::string& out)
	{
		const uint8_t* owner = GlobalsField(kGlobalsDifficulty);
		ConfigList list;
		int handle = -1, value = -1, lowest = -1;
		if (!owner || !ReadConfigList(owner, list) || !Read(owner + kDifficultyHandle, handle) || !Read(owner + kDifficultyValue, value) ||
			!Read(owner + kDifficultyLowest, lowest))
		{
			out += "difficulty: couldn't be read\n";
			return;
		}
		std::string held;
		for (uint32_t i = 0; i < list.count; i++)
			if (ConfigAt(list, i))
				held += (held.empty() ? "" : ", ") + NameOf(owner, i);
		out += "difficulty: " + (handle >= 0 ? NameOf(owner, static_cast<uint32_t>(handle)) : std::string("none")) + " in force (the game's number " +
			std::to_string(value) + ", the campaign's lowest " + std::to_string(lowest) + "); held: " + held + "\n";
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
		{
			float hears = 0.0f;
			if (character.player || character.dead || !Hearing(character.object, hears))
				continue;
			out += character.name + ": hears up to " + Format("%.1f", hears) + " m\n";
		}
	}

	void ReportViewcones(std::string& out)
	{
		const uint8_t* tables = GlobalsField(kGlobalsViewcones);
		if (!tables)
		{
			out += "view cones: couldn't be read\n";
			return;
		}
		out += "view cones: each set's cones - length, width x height, in use or not\n";
		for (int set = 0; set < kViewconeSetCount; set++)
		{
			ConeSet cones;
			if (!ReadConeSet(tables, set, cones) || !cones.copied)
				continue;
			std::string row = std::string(AI_DEBUG::ViewconeSetName(set)) + ": " + std::to_string(cones.count) + (cones.count == 1 ? " cone" : " cones");
			for (uint32_t i = 0; i < cones.count; i++)
			{
				const uint8_t* cone = cones.bytes.data() + i * kConeSize;
				float length = 0.0f, horizontal = 0.0f, vertical = 0.0f;
				memcpy(&length, cone + kConeLength, sizeof(float));
				memcpy(&horizontal, cone + kConeHorizontalDot, sizeof(float));
				memcpy(&vertical, cone + kConeVerticalDot, sizeof(float));
				const double toDegrees = 2.0 * 57.29577951308232;
				row += (i == 0 ? " - " : ", ") + Format("%.1f m ", length) +
					Format("%.0f", std::acos((std::max)(-1.0f, (std::min)(1.0f, horizontal))) * toDegrees) + "x" +
					Format("%.0f deg", std::acos((std::max)(-1.0f, (std::min)(1.0f, vertical))) * toDegrees) +
					(cone[kConeValid] ? "" : " (not in use)");
			}
			out += row + "\n";
		}
	}

	// ---- The section ----

	void Ask(uint32_t parts)
	{
		g_pending |= parts;
		DEBUG_MENU::OnAiThread([]()
		{
			RunPending();
		});
	}

	void DrawSection()
	{
		ImGui::SeparatorText("Config files");
		ImGui::TextWrapped("The game reads its alien setups, difficulty settings and view cones from DATA once, when it starts. "
			"Read them again from the files now, and have the characters take them up:");
		const bool available = load_configs && load_viewcones && resolve_file_name;
		ImGui::BeginDisabled(!available || g_reloading);
		if (ImGui::Button("Alien setups"))
			Ask(kAlien);
		ImGui::SameLine();
		if (ImGui::Button("Difficulty"))
			Ask(kDifficulty);
		ImGui::SameLine();
		if (ImGui::Button("View cones"))
			Ask(kViewcones);
		ImGui::SameLine();
		if (ImGui::Button("All"))
			Ask(kAll);
		ImGui::EndDisabled();
		if (!available)
			ImGui::TextDisabled("Not available on this release of the game.");
		ImGui::TextDisabled("OpenCAGE's Alien Configs, Difficulty Settings and Viewcones editors do this after each change, "
			"while Live Link is connected.");
		if (g_pending)
			ImGui::TextDisabled("Waiting for the game's AI to run (paused, or no level running?)");
		std::lock_guard<std::mutex> lock(g_lastMutex);
		if (!g_last.message.empty())
		{
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextDisabled("Last: %s", g_last.message.c_str());
			ImGui::PopTextWrapPos();
		}
	}

	bool Register()
	{
		DEBUG_MENU::AddCommand("ai.reload_configs", "read the AI config files again and have the characters take them up: alien, difficulty, viewcones "
			"or all (the default), or several; a line per part (waits for the game's AI to run)", [](const std::string& argument)
		{
			std::string unknown;
			const uint32_t parts = PartsFrom(argument, unknown);
			if (!parts)
				return DEBUG_MENU::CommandResult{ false, "not a part of the config files: " + unknown + " (alien, difficulty, viewcones or all)" };
			if (!load_configs || !load_viewcones || !resolve_file_name)
				return DEBUG_MENU::CommandResult{ false, "not available on this release of the game" };
			g_pending |= parts;
			return DEBUG_MENU::AnswerOnAiThread([]() { return RunPending(); }, 3000);
		});
		DEBUG_MENU::AddCommand("ai.configs", "what the game holds from its AI config files: alien (each setup's director numbers at the difficulty "
			"in force), difficulty (the one in force, how far each NPC hears), viewcones (each set's cones) or all", [](const std::string& argument)
		{
			std::string unknown;
			const uint32_t parts = PartsFrom(argument, unknown);
			if (!parts)
				return DEBUG_MENU::CommandResult{ false, "not a part of the config files: " + unknown + " (alien, difficulty, viewcones or all)" };
			return DEBUG_MENU::AnswerOnRenderThread([parts]()
			{
				if (g_reloading)
					return DEBUG_MENU::CommandResult{ false, "the config files are being read again: ask again" };
				std::string out;
				if (parts & kAlien)
					ReportSetups(out);
				if (parts & kDifficulty)
					ReportDifficulty(out);
				if (parts & kViewcones)
					ReportViewcones(out);
				return DEBUG_MENU::CommandResult{ true, out };
			});
		});
		return true;
	}

	const bool g_registered = Register();
	const bool g_section = DEBUG_MENU::AddTab("Tuning", 72, DrawSection, "AI");
}

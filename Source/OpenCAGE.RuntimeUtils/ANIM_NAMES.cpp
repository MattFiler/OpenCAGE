#include "ANIM_NAMES.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <algorithm>
#include <atomic>
#include <cstdio>
#include <cstring>
#include <string>
#include <thread>
#include <unordered_map>
#include <utility>
#include <vector>

/*
	Addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).
*/
namespace
{
	// The game's own table of names (a pointer to it): its hash index (a pointer) and the text the index points into.
	// The index holds pairs of {hash, slot}, sorted by hash with the largest first, the slots' offsets into the text, and
	// how many pairs there are (and are in use).
	constexpr uintptr_t kResidentTable = 0x017E1C40;
	constexpr uint32_t kTableIndex = 0x08;
	constexpr uint32_t kTableText = 0x0C;
	constexpr uint32_t kIndexPairs = 0x08;
	constexpr uint32_t kIndexOffsets = 0x0C;
	constexpr uint32_t kIndexCount = 0x10;
	constexpr uint32_t kIndexUsed = 0x14;

	// The full table, as the package holds it
	const char* const kPackage = "DATA\\GLOBAL\\ANIMATION.PAK";
	const char* const kEntry = "DATA\\ANIM_SYS\\ANIM_STRING_DB_DEBUG.BIN";

	// 0 not started, 1 loading, 2 in, 3 could not be read
	std::atomic<int> g_state = 0;
	const char* g_problem = nullptr;
	// Once in (state 2) these never change: the entry's bytes, and {hash, offset of its name in them} sorted by hash
	std::vector<char> g_blob;
	std::vector<std::pair<uint32_t, uint32_t>> g_index;

	// Render thread: names found so far, and whether they were found before the full table was in
	std::unordered_map<uint32_t, std::string> g_cache;
	bool g_cacheHasFull = false;

	bool ReadAt(HANDLE file, uint64_t at, void* into, uint32_t size)
	{
		LARGE_INTEGER position;
		position.QuadPart = static_cast<LONGLONG>(at);
		DWORD read = 0;
		return SetFilePointerEx(file, position, nullptr, FILE_BEGIN) && ReadFile(file, into, size, &read, nullptr) && read == size;
	}

	// The package's entry, read from its file. The package: "PAK2", where its list of entry ends starts (less 16), how
	// many entries, their alignment; then the entries' names, one after another; then each entry's end. An entry starts
	// where the one before it ended (the first right after the list), rounded up to the alignment.
	bool ReadEntry(const std::string& path, std::vector<char>& out, const char*& problem)
	{
		HANDLE file = CreateFileA(path.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
		if (file == INVALID_HANDLE_VALUE)
		{
			problem = "the animation package could not be opened";
			return false;
		}
		bool ok = false;
		do
		{
			LARGE_INTEGER size = {};
			GetFileSizeEx(file, &size);
			int32_t header[4] = {};
			if (!ReadAt(file, 0, header, sizeof(header)) || memcmp(header, "PAK2", 4) != 0)
			{
				problem = "the animation package is not one this reads";
				break;
			}
			const int64_t listStart = static_cast<int64_t>(header[1]) + 16;
			const int32_t count = header[2];
			const int32_t alignment = header[3];
			if (count <= 0 || count > 1000000 || listStart <= 16 || listStart + 4LL * count > size.QuadPart)
			{
				problem = "the animation package's header is not one this reads";
				break;
			}
			std::vector<char> names(static_cast<size_t>(listStart - 16));
			std::vector<int32_t> ends(static_cast<size_t>(count));
			if (!ReadAt(file, 16, names.data(), static_cast<uint32_t>(names.size())) ||
				!ReadAt(file, static_cast<uint64_t>(listStart), ends.data(), static_cast<uint32_t>(ends.size() * 4)))
			{
				problem = "the animation package could not be read";
				break;
			}
			// The entry's number, from its name
			int found = -1;
			size_t at = 0;
			for (int i = 0; i < count && at < names.size(); i++)
			{
				const char* name = names.data() + at;
				const size_t length = strnlen(name, names.size() - at);
				if (length == strlen(kEntry) && _stricmp(name, kEntry) == 0)
				{
					found = i;
					break;
				}
				at += length + 1;
			}
			if (found < 0)
			{
				problem = "the animation package has no full name table";
				break;
			}
			int64_t start = found == 0 ? listStart + 4LL * count : ends[found - 1];
			if (alignment > 1)
				start += (alignment - start % alignment) % alignment;
			const int64_t end = ends[found];
			if (start <= 0 || end <= start || end > size.QuadPart || end - start > 64 * 1024 * 1024)
			{
				problem = "the full name table's place in the animation package is not one this reads";
				break;
			}
			out.resize(static_cast<size_t>(end - start));
			if (!ReadAt(file, static_cast<uint64_t>(start), out.data(), static_cast<uint32_t>(out.size())))
			{
				problem = "the full name table could not be read";
				break;
			}
			ok = true;
		} while (false);
		CloseHandle(file);
		return ok;
	}

	// The table: how many entries and names; the entries ({hash, name number}); each name's offset; then the names.
	bool IndexTable(const std::vector<char>& blob, std::vector<std::pair<uint32_t, uint32_t>>& index, const char*& problem)
	{
		if (blob.size() < 8)
		{
			problem = "the full name table is empty";
			return false;
		}
		uint32_t entries = 0, strings = 0;
		memcpy(&entries, blob.data(), 4);
		memcpy(&strings, blob.data() + 4, 4);
		const uint64_t textStart = 8ULL + 8ULL * entries + 4ULL * strings;
		if (entries > 10000000 || strings > 10000000 || textStart > blob.size())
		{
			problem = "the full name table is not one this reads";
			return false;
		}
		const char* data = blob.data();
		index.reserve(entries);
		for (uint32_t i = 0; i < entries; i++)
		{
			uint32_t hash = 0, number = 0, offset = 0;
			memcpy(&hash, data + 8 + 8 * i, 4);
			memcpy(&number, data + 8 + 8 * i + 4, 4);
			if (number >= strings)
				continue;
			memcpy(&offset, data + 8 + 8ULL * entries + 4 * number, 4);
			const uint64_t at = textStart + offset;
			if (at >= blob.size())
				continue;
			index.emplace_back(hash, static_cast<uint32_t>(at));
		}
		std::sort(index.begin(), index.end());
		return true;
	}

	void Load()
	{
		char exe[MAX_PATH] = {};
		GetModuleFileNameA(nullptr, exe, MAX_PATH);
		std::string folder = exe;
		const size_t slash = folder.find_last_of("\\/");
		folder = slash == std::string::npos ? std::string() : folder.substr(0, slash + 1);

		std::vector<char> blob;
		std::vector<std::pair<uint32_t, uint32_t>> index;
		const char* problem = nullptr;
		if (!ReadEntry(folder + kPackage, blob, problem) || !IndexTable(blob, index, problem))
		{
			g_problem = problem;
			DevTools::Log("Animation names: %s; using the game's own table", problem);
			g_state = 3;
			return;
		}
		// The last byte is the end of the last name; a name running off the end would be cut by it
		blob.push_back('\0');
		g_blob.swap(blob);
		g_index.swap(index);
		DevTools::Log("Animation names: %u names read from the animation package", static_cast<unsigned>(g_index.size()));
		g_state = 2;
	}

	void StartLoading()
	{
		int expected = 0;
		if (g_state.compare_exchange_strong(expected, 1))
			std::thread(Load).detach();
	}

	const char* FullName(uint32_t hash)
	{
		if (g_state != 2)
			return nullptr;
		auto found = std::lower_bound(g_index.begin(), g_index.end(), std::make_pair(hash, 0u));
		return found != g_index.end() && found->first == hash ? g_blob.data() + found->second : nullptr;
	}

	// The game's own table (copied out: it lives in the game's memory)
	bool ResidentName(uint32_t hash, char* out, size_t size)
	{
		__try
		{
			const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kResidentTable);
			const uint8_t* table = at ? *reinterpret_cast<const uint8_t* const*>(at) : nullptr;
			if (!table)
				return false;
			const uint8_t* index = *reinterpret_cast<const uint8_t* const*>(table + kTableIndex);
			const char* text = *reinterpret_cast<const char* const*>(table + kTableText);
			if (!index || !text)
				return false;
			const uint32_t* pairs = *reinterpret_cast<const uint32_t* const*>(index + kIndexPairs);
			const uint32_t* offsets = *reinterpret_cast<const uint32_t* const*>(index + kIndexOffsets);
			const uint32_t count = *reinterpret_cast<const uint32_t*>(index + kIndexCount);
			if (!pairs || !offsets || !*reinterpret_cast<const uint32_t*>(index + kIndexUsed) || count > 1000000)
				return false;
			// Largest hash first
			uint32_t low = 0, high = count;
			while (low < high)
			{
				const uint32_t middle = low + (high - low) / 2;
				const uint32_t key = pairs[middle * 2];
				if (key == hash)
				{
					const char* name = text + offsets[pairs[middle * 2 + 1]];
					size_t i = 0;
					for (; i + 1 < size && name[i]; i++)
						out[i] = name[i];
					out[i] = '\0';
					return i > 0;
				}
				if (key > hash)
					low = middle + 1;
				else
					high = middle;
			}
			return false;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}
}

std::string ANIM_NAMES::Name(uint32_t hash)
{
	if (hash == 0)
		return std::string();
	StartLoading();
	// Names found before the full table was in are found again once it is
	const bool full = g_state == 2;
	if (full && !g_cacheHasFull)
	{
		g_cache.clear();
		g_cacheHasFull = true;
	}
	auto cached = g_cache.find(hash);
	if (cached != g_cache.end())
		return cached->second;

	std::string name;
	if (const char* fullName = FullName(hash))
		name = fullName;
	else
	{
		char resident[256];
		if (ResidentName(hash, resident, sizeof(resident)))
			name = resident;
		else
		{
			char text[16];
			snprintf(text, sizeof(text), "#%08X", hash);
			name = text;
		}
	}
	if (g_cache.size() > 50000)
		g_cache.clear();
	g_cache.emplace(hash, name);
	return name;
}

bool ANIM_NAMES::FullTableReady()
{
	return g_state == 2;
}

size_t ANIM_NAMES::FullTableCount()
{
	return g_state == 2 ? g_index.size() : 0;
}

const char* ANIM_NAMES::FullTableProblem()
{
	return g_state == 3 ? g_problem : nullptr;
}

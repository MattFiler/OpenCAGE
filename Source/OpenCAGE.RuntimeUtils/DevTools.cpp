#include "DevTools.h"
#include "GameAddresses.h"
#include <algorithm>
#include <cstdint>
#include <cstdarg>
#include <cstring>
#include <share.h>
#include <stdio.h>
#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

using namespace DevTools;

uintptr_t m_game_imageBaseAddress = NULL;

uintptr_t GameProcess::GetBaseAddress()
{
	if (m_game_imageBaseAddress == NULL)
	{
		m_game_imageBaseAddress = reinterpret_cast<uintptr_t>(GetModuleHandle(NULL));
	}

	return m_game_imageBaseAddress;
}

GameBuild DevTools::Build()
{
	static const GameBuild build = []
	{
		const uintptr_t base = GameProcess::GetBaseAddress();
		const IMAGE_DOS_HEADER* dos = reinterpret_cast<const IMAGE_DOS_HEADER*>(base);
		const IMAGE_NT_HEADERS* nt = reinterpret_cast<const IMAGE_NT_HEADERS*>(base + dos->e_lfanew);
		const DWORD stamp = nt->FileHeader.TimeDateStamp;
		const DWORD size = nt->OptionalHeader.SizeOfImage;
		if (stamp == 0x54E3199D && size == 0x01F81000)
			return GameBuild::Steam;
		if (stamp == 0x62952F34 && size == 0x01F9D000)
			return GameBuild::GOG;
		if (stamp == 0x5FD25676 && size == 0x01F9F000)
			return GameBuild::Epic;
		return GameBuild::Unknown;
	}();
	return build;
}

const char* DevTools::BuildName()
{
	switch (Build())
	{
	case GameBuild::Steam: return "Steam";
	case GameBuild::GOG: return "GOG";
	case GameBuild::Epic: return "Epic Games Store";
	default: return "unknown";
	}
}

uintptr_t DevTools::BuildOffset(uintptr_t steamOffset)
{
	const GameBuild build = Build();
	if (build == GameBuild::Steam)
		return steamOffset;
	if (build != GameBuild::GOG && build != GameBuild::Epic)
		return 0;

	const GameAddresses::Row* begin = std::begin(GameAddresses::kRows);
	const GameAddresses::Row* end = std::end(GameAddresses::kRows);
	const GameAddresses::Row* row = std::lower_bound(begin, end, steamOffset, [](const GameAddresses::Row& r, uintptr_t offset) { return r.steam < offset; });
	if (row != end && row->steam == steamOffset)
		return build == GameBuild::GOG ? row->gog : row->epic;

	//Every address the code uses has a row: one without is a row forgotten when the address was added
	Log("No address in the %s build for the Steam build's 0x%08X: add it to GameAddresses.h", BuildName(), static_cast<unsigned>(steamOffset));
	return 0;
}

uintptr_t DevTools::GameAddress(uintptr_t steamOffset)
{
	const uintptr_t offset = BuildOffset(steamOffset);
	return offset ? GameProcess::GetBaseAddress() + offset : 0;
}

bool DevTools::EnableEntity(uintptr_t slotOffset)
{
	const uintptr_t stub = DEVTOOLS_RELATIVE_ADDRESS(0x00599180);
	const uintptr_t real = DEVTOOLS_RELATIVE_ADDRESS(0x004c3010);
	uintptr_t* slot = reinterpret_cast<uintptr_t*>(DEVTOOLS_RELATIVE_ADDRESS(slotOffset));

	if (*slot == real)
		return true;
	if (*slot != stub)
		return false;

	DWORD oldProtect;
	VirtualProtect(slot, sizeof(*slot), PAGE_READWRITE, &oldProtect);
	*slot = real;
	VirtualProtect(slot, sizeof(*slot), oldProtect, &oldProtect);
	return true;
}

void DevTools::Log(const char* format, ...)
{
	//Logged to from the socket, entity and render threads: a function-local static is initialised once, thread-safely
	struct LogLock
	{
		CRITICAL_SECTION section;
		LogLock() { InitializeCriticalSection(&section); }
	};
	static LogLock lock;
	static FILE* file = nullptr;

	EnterCriticalSection(&lock.section);
	if (!file)
	{
		char path[MAX_PATH] = {};
		GetModuleFileNameA(nullptr, path, MAX_PATH);
		char* slash = strrchr(path, '\\');
		if (slash)
			strcpy_s(slash + 1, MAX_PATH - (slash + 1 - path), "OpenCAGE.RuntimeUtils.log");
		//Opened shared, so the log can be read while the game runs (the standard secure open would lock the file)
		file = _fsopen(path, "w", _SH_DENYNO);
	}
	if (file)
	{
		SYSTEMTIME time;
		GetLocalTime(&time);
		fprintf(file, "%02d:%02d:%02d.%03d  ", time.wHour, time.wMinute, time.wSecond, time.wMilliseconds);
		va_list args;
		va_start(args, format);
		vfprintf(file, format, args);
		va_end(args);
		fputc('\n', file);
		fflush(file);
	}
	LeaveCriticalSection(&lock.section);
}

namespace
{
	// Windows-1252's 0x80-0x9F as Unicode; the five it leaves unused (0) keep their own value, as Windows converts them
	constexpr uint16_t kCp1252High[32] = {
		0x20AC, 0, 0x201A, 0x0192, 0x201E, 0x2026, 0x2020, 0x2021, 0x02C6, 0x2030, 0x0160, 0x2039, 0x0152, 0, 0x017D, 0,
		0, 0x2018, 0x2019, 0x201C, 0x201D, 0x2022, 0x2013, 0x2014, 0x02DC, 0x2122, 0x0161, 0x203A, 0x0153, 0, 0x017E, 0x0178,
	};

	// One Windows-1252 byte as UTF-8 into out (room for 3); returns how many bytes
	size_t Cp1252ToUtf8(unsigned char c, char* out)
	{
		uint32_t code = c;
		if (c >= 0x80 && c < 0xA0 && kCp1252High[c - 0x80] != 0)
			code = kCp1252High[c - 0x80];
		if (code < 0x80)
		{
			out[0] = static_cast<char>(code);
			return 1;
		}
		if (code < 0x800)
		{
			out[0] = static_cast<char>(0xC0 | (code >> 6));
			out[1] = static_cast<char>(0x80 | (code & 0x3F));
			return 2;
		}
		out[0] = static_cast<char>(0xE0 | (code >> 12));
		out[1] = static_cast<char>(0x80 | ((code >> 6) & 0x3F));
		out[2] = static_cast<char>(0x80 | (code & 0x3F));
		return 3;
	}
}

std::string DevTools::GameTextToUtf8(const char* text)
{
	if (!text)
		return std::string();
	const char* at = text;
	while (*at && static_cast<unsigned char>(*at) < 0x80)
		at++;
	if (!*at)
		return std::string(text, at - text); // plain ASCII: the same either way
	std::string out(text, at - text);
	char bytes[3];
	for (; *at; at++)
		out.append(bytes, Cp1252ToUtf8(static_cast<unsigned char>(*at), bytes));
	return out;
}

void DevTools::GameTextToUtf8(const char* text, char* out, size_t size)
{
	if (!out || size == 0)
		return;
	size_t used = 0;
	char bytes[3];
	for (; text && *text; text++)
	{
		const size_t count = Cp1252ToUtf8(static_cast<unsigned char>(*text), bytes);
		if (used + count + 1 > size)
			break;
		for (size_t i = 0; i < count; i++)
			out[used++] = bytes[i];
	}
	out[used] = '\0';
}

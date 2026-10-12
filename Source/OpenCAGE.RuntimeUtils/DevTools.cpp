#include "DevTools.h"
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

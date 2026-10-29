#pragma once

#include "DevTools.h"

namespace GameFlow
{
	void __fastcall h_start_gameplay(void* _this, void* _EDX, int play_mode, const char* level_name);
	typedef void(__thiscall* t_start_gameplay)(void*, int, const char*);
	inline auto start_gameplay = reinterpret_cast<t_start_gameplay>(DEVTOOLS_RELATIVE_ADDRESS(0x003bcb00));
}

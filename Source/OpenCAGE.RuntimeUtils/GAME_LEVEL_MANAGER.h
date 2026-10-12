#pragma once

#include "DevTools.h"

namespace GAME_LEVEL_MANAGER
{
	struct Instance {
		char unk0[136]; // Skip unknown values in the struct.
		int current_index; // Current level index.
		unsigned int unk1;
	};

	// The game's level manager, captured the first time it looks a level up by name.
	inline Instance* m_instance = nullptr;

	int __fastcall h_get_level_from_name(Instance* _this, void* _EDX, char* level_name);
	typedef int(__thiscall* t_get_level_from_name)(Instance*, char*);
	inline auto get_level_from_name = reinterpret_cast<t_get_level_from_name>(DEVTOOLS_RELATIVE_ADDRESS(0x0037b340));

	int __fastcall h_get_current_level(Instance* _this, void* _EDX);
	typedef int(__thiscall* t_get_current_level)(Instance*);
	inline auto get_current_level = reinterpret_cast<t_get_current_level>(DEVTOOLS_RELATIVE_ADDRESS(0x00fc2ab0 - 0x00400000));

	void __fastcall h_queue_level(Instance* _this, void* _EDX, int level);
	typedef void(__thiscall* t_queue_level)(Instance*, int);
	inline auto queue_level = reinterpret_cast<t_queue_level>(DEVTOOLS_RELATIVE_ADDRESS(0x0037b320));

	void __fastcall h_request_next_level(Instance* _this, void* _EDX, bool is_part_of_playlist);
	typedef void(__thiscall* t_request_next_level)(Instance*, bool);
	inline auto request_next_level = reinterpret_cast<t_request_next_level>(DEVTOOLS_RELATIVE_ADDRESS(0x0039a650));
}

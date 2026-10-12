#include "GAME_LEVEL_MANAGER.h"

#include <stdio.h>

using namespace GAME_LEVEL_MANAGER;

__declspec(noinline)
int __fastcall GAME_LEVEL_MANAGER::h_get_level_from_name(Instance* _this, void* _EDX, char* level_name)
{
	if (m_instance == nullptr)
		m_instance = _this;

	const int level = get_level_from_name(_this, level_name);
	return level;
}

__declspec(noinline)
int __fastcall GAME_LEVEL_MANAGER::h_get_current_level(Instance* _this, void* _EDX)
{
	return get_current_level(_this);
}

__declspec(noinline)
void __fastcall GAME_LEVEL_MANAGER::h_queue_level(Instance* _this, void* _EDX, int level)
{
	queue_level(_this, level);
}

__declspec(noinline)
void __fastcall GAME_LEVEL_MANAGER::h_request_next_level(Instance* _this, void* _EDX, bool is_part_of_playlist)
{
	request_next_level(_this, is_part_of_playlist);
}

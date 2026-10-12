#include "GameFlow.h"

__declspec(noinline)
void __fastcall GameFlow::h_start_gameplay(void* _this, void* /*_EDX*/, int play_mode, const char* level_name)
{
	start_gameplay(_this, play_mode, level_name);
}

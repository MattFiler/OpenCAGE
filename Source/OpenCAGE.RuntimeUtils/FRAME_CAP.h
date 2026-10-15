#pragma once

#include "DevTools.h"

/*
	Cap FPS: the game runs at most CapFps frames a second.

	The game's clock takes each frame's real length as the time that passed, but never less than 1/360 of a second. Above
	360 frames a second - a near-empty level with V-Sync off, say - every frame still moves the game on by 1/360 of a
	second, so everything runs faster than real time (2.8x at 1,000 frames a second). The game has no frame limiter of its
	own besides V-Sync.

	So each frame waits at its start, before the game reads the clock, until its turn comes round: the frames keep to the
	cap, the clock sees the real time that passed, and the game runs at its proper speed. The wait sleeps on a
	high-resolution timer for all but the last moment and spins for that, so 90 or 120 holds as well as 30 or 60. A frame
	that runs long (a load, a hitch) is not caught up on afterwards.
*/
namespace FRAME_CAP
{
	// Hooked: the start of each of the game's frames, where it reads the clock.
	typedef void(__cdecl* t_frame_start)();
	inline auto frame_start = reinterpret_cast<t_frame_start>(DEVTOOLS_RELATIVE_ADDRESS(0x003ddae0));
	void __cdecl h_frame_start();

	// The cap, in frames a second (0 = none). Set once at startup, before the hook is attached.
	void SetLimit(int framesPerSecond);
}

#pragma once

#include "DevTools.h"

/*
	Fixed time step: the debug menu's Game > Tools section that has every frame move the game on by exactly 1/N of a
	second of game time, however long the frame took to draw ("game.fixed_step_fps": N, 0 = off). For smooth captures
	recorded slower than real time (each frame of the video is one step of the game) and for repeatable tests (the AI,
	physics and the level's scripts see the same steps every run).

	The game's real-time clock has a fixed step of its own, unused in the shipped game: a flag that has it hand out a stored
	step instead of the real time that passed, and the step itself. Both are plain writes, kept and put back when it goes
	off, when the level unloads or changes, and when the runtime utils unload. Physics follows the flag by itself: the
	game steps its physics world by the clock's frame rate, which the flag ties to the step.

	With the other time controls:
	- Game speed ("game.speed", and the game's own slow-motion moments, both the game clock's time scale) still applies: it
	  is folded into the step (speed/N a frame) and the clock's own scale is held off meanwhile, as physics follows only
	  the step and would otherwise run at full speed under a slowed game. The scale is handed back to the clock as it goes
	  off.
	- Pause ("game.paused") holds the game as before. Step ("game.step") lets exactly one step through: 1/N of a second of
	  game time (times the speed), the same every time.
	- Cap FPS paces the frames in real time and never touches the clock: with Cap FPS at N (or V-Sync at N) the game keeps to
	  real time. Drawing faster than N frames a second runs the game faster than real time, slower runs it slower.
	- Sound runs in real time, so it drifts against the picture while the step is on.
	When it goes off, the clock is made to take up real time again from where the game's time has got to, as it does
	after a hitch, rather than catch up (or wait) for the time the step gained or lost; and the physics rate is set to the
	frames being drawn, rather than left to creep there from the step's rate.
*/
namespace GAME_TIME_STEP
{
	// The section (render thread): drawn on the Tools tab, after its own sections.
	void DrawControls();

	// While the fixed step is in force: the game time each frame moves on by (the step with the game speed folded in, in
	// seconds), for views that should keep to the game's time rather than real time (a camera track while capturing, say).
	// False while it is off. Any thread.
	bool FrameStep(float& seconds);
}

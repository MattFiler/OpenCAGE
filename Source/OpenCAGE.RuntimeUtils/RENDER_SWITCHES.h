#pragma once

#include "DevTools.h"

/*
	Rendering switches: the debug menu's Rendering tab - the game's own switches for the renderer and the radiosity
	(the bounce light).

	The game keeps the code behind most of its rendering switches, but asks each switch whether it is
	on through a call to one of two tiny functions that many unrelated things share: one that always answers no and one
	that always answers yes (each switch's question is compiled down to its fixed answer). So a
	switch is turned by changing where its calls go. Each call is a five-byte call instruction whose last four bytes are
	the distance to the function it calls; those four bytes are rewritten so that the call reaches the other answer. Only
	the calls made by the switch's own code are changed - never the two answer functions, which other code uses.

	Before anything is changed, each of a switch's calls is checked: it must be a call instruction that reaches the answer
	expected there in this build, and the two answer functions must hold exactly the instructions they should. A switch
	with any call that doesn't check out is shown as not available on this build and is never changed. A call is changed
	back to exactly what it was when its switch is turned back, on Reset all, and when the hooks detach as the game closes.

	The calls are changed on the render thread at the frame's presentation, when the frame's own work is finished and the
	next frame's has not started, and each call's four bytes are written in one locked exchange wherever they sit inside
	one cache line (nearly all of them do).

	A switch whose code sets one of the game's shaders (the output views) is only turned on while that shader is loaded.

	Each switch has a dotted name ("render.no_radiosity", ...) so that OpenCAGE can set it over the Live Link (see
	DEBUG_MENU.h). Every frame the switches are compared with what was last applied, so one set from outside the window
	applies at the next frame.
*/
namespace RENDER_SWITCHES
{
	// Puts every changed call back to what the game had (when the hooks detach).
	void Restore();
}

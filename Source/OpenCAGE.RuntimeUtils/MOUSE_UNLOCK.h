#pragma once

#include "DevTools.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

/*
	Unlock Mouse: the game leaves the mouse cursor alone until a key (UnlockMouseKey, F4 by default) is pressed in its
	window, so OpenCAGE can be used beside the running game - its viewport driving the game camera over the Live Link,
	say - without the game taking the cursor. The key takes turns: pressed, the game has the mouse to look around with,
	as it normally does; pressed again, the cursor is free again. It starts free.

	While a level is played (rather than a menu shown), the game pins the cursor to the middle of its window, hides it,
	and turns the camera with the mouse whenever its window has the keyboard focus. While the cursor is free:
	- the game's routine that pins the cursor lets it go instead (what the game asked for is remembered, so the key
	  hands the cursor back to it as asked);
	- the cursor is shown over the game window;
	- the mouse's movement, buttons and wheel are kept from the game while its window has the focus, so moving the
	  cursor across it doesn't turn the camera (or fire);
	- the game never moves the cursor itself (it puts it back where it was when the level stops being played, a menu
	  opening say, which would pull it off whatever the cursor is over).
	Menus are left as they are: the game frees and shows the cursor for them itself, and clicks reach them.

	A cubemap capture holds the cursor free the same way while it runs (Hold), whether Unlock Mouse is on or not, so
	clicking into the game to watch one doesn't hand the game the mouse; afterwards it is as it was. The hooks are in
	for that whenever captures can be asked for (the live link's camera sync on), and only pass through otherwise.
*/
namespace MOUSE_UNLOCK
{
	// Hooked: the game's routine that pins the cursor to the middle of its window (grab) or lets it go (not grab).
	DEVTOOLS_DECLARE_CLASS_HOOK(void, grab_cursor, h_grab_cursor, t_grab_cursor, 0x006109A0, bool grab)

	// Hooked: Windows' moving of the cursor, which the game uses to put it back when a level stops being played.
	inline BOOL(WINAPI* set_cursor_pos)(int, int) = SetCursorPos;
	BOOL WINAPI h_set_cursor_pos(int x, int y);

	// The game window's messages, before the game has them (its window thread): the key (with Unlock Mouse on), the
	// cursor shown or not, and whether the message is kept from the game - true when it is, with what to answer it.
	bool OnWindowMessage(HWND window, UINT message, WPARAM wParam, LPARAM lParam, LRESULT& result);

	// Holds the cursor free (a cubemap capture running), or lets it go back to how Unlock Mouse and the game have it
	// (any thread; the game is told at the window's next message).
	void Hold(bool held);

	// The overlay (render thread): a note for a few seconds when the key is pressed, and the first time a level is
	// played with the cursor free.
	void DrawOverlay();
}

#pragma once

/*
	The Cinematic group's game capture (CINEMATIC_CAPTURE.cpp): the game's own stills, environment maps and image
	sequences. The game takes them from the frame as it ends up, so while one is under way nothing of ours is drawn over
	the game - not the Debug Menu's window, its overlays, nor Live Link's notes - or it would be in every file.
*/
namespace CINEMATIC_CAPTURE
{
	// While a capture is under way (from just before it is asked for until the game has finished it; for a sequence, the
	// whole recording): draw nothing over the game this frame. Render thread.
	bool HideDrawing();

	// The Debug Menu's key, pressed while a sequence records: nothing is drawn meanwhile (so there's no window to stop it
	// from), and the key stops the recording instead of opening the window. True when it did. Window thread.
	bool OnMenuKey();
}

#pragma once

#include "DevTools.h"
#include <cstdint>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

/*
	The cinematic camera: the debug menu's free camera for shots and video, in the Cinematic group (its Free Camera tab), standing
	in for Cinematic Tools' camera (and for the game's own free camera the Tools tab once turned on). Its camera and track
	behaviour is ported from Cinematic Tools, by Hattiwatti.

	It is flown with the debug menu's window closed: its key (CinematicCameraKey in OpenCAGE.RuntimeUtils.ini, F8 by
	default) turns it on and off, as does the Free Camera tab's checkbox and the switch "cinematic.camera". It starts where the
	frame was drawn from, looking the same way, with the same field of view (or where it was left, in the same level, unless
	"Start from the game camera" is ticked). While it is on:
	- W/A/S/D fly it, Space and Ctrl take it up and down (along the world's up), Shift goes four times faster, Page Up and
	  Page Down change its speed; the mouse and the arrow keys turn it, Q and E roll it, the mouse wheel and the numeric pad's + and -
	  change its field of view; the numeric pad's 6/5, 8/9 and 3/2 change the depth of field's focus distance, focal length
	  and f-number (with the Visuals tab's depth of field on); F1 adds a track node where it is, F2 deletes the last one,
	  F3 plays or stops the track; Home hides the HUD; Delete pauses the game and End steps it one frame while paused (the
	  game's own pause and step, from the Game group). These are Cinematic Tools' keys, bar Q/E, Shift, Page Up/Down, End and the wheel.
	  Any of the camera's keys that the ini gives another helper too (the debug menu, Possess, Unlock Mouse, hot reload) is
	  left out, with a line in the log.
	- A gamepad (XInput) flies it too, as in Cinematic Tools: the left stick moves, the triggers go up and down, the right
	  stick turns, the shoulder buttons roll, the stick clicks change the field of view, the d-pad's up and down change how
	  fast it turns and B and Y how fast it moves.
	- the player is frozen (the game's own freeze, as its scripts freeze a character), and the game's keyboard and mouse
	  are kept from it (bar Escape, for the pause menu), as is the gamepad: so the player stands still, and keys and buttons
	  do nothing in the game. "Keep the game's input" off instead lets the game have its keyboard, mouse and gamepad - the
	  player plays on, unfrozen, while the camera holds where it is (or follows its track, or the character it is locked to).
	  While the game's pause menu (Escape) or game over screen is up, the game has its keys, mouse and pad back and the
	  camera holds still.
	- the zones around the camera are streamed in (the zone loader's cinematic request), as the game only streams those
	  around the player - except while the game is paused, when nothing streams.
	The debug menu's window open, its keys and mouse are the window's: the camera holds still meanwhile - unless "Fly with
	this window open" (cinematic.fly_with_menu) is on: then the keys still fly it (not while a box in the window is typed in),
	the mouse turns it while its right button is held away from the window, and the wheel zooms away from the window.

	It can be locked to a character: it then moves with the character (it is kept relative to the character's own position
	and facing), as Cinematic Tools' "Lock to character" did. A track playing moves it in the world, unlocked.

	It renders through the camera hand-over hook (LIVE_CAMERA.cpp), which asks it for its pose each frame. Order there: a
	cubemap capture first, then this camera, then OpenCAGE's viewport camera (Live Link camera sync), then Possess's camera
	behind a possessed character. So turning it on takes the camera from OpenCAGE's viewport, which gets it back when it is
	turned off (OpenCAGE's pose waits meanwhile); and OpenCAGE following the game's camera (CAMERA_GET) follows this camera
	while it is on. It does not turn on while a character is possessed, and turns itself off when one is (its keys and
	Possess's are the same): whichever was asked for last has the keys and the camera.

	A level change turns it off. Settings sets (speeds, field of view, depth of field) are kept as named profiles in
	OpenCAGE.CinematicProfiles.ini, next to OpenCAGE.RuntimeUtils.ini.

	The mouse and the keeping of the game's input come through the game window's messages, which reach the camera whenever
	DebugMenu=1: directly, or handed on by the cubemap capture's handler while Live Link camera sync is on and no capture
	runs. With Unlock Mouse on, the mouse belongs to the free cursor and is kept from the camera until Unlock Mouse's key
	hands it to the game; the keys and a gamepad fly it meanwhile.
*/
namespace CINEMATIC_CAMERA
{
	// Hooked: the game's reading of the gamepad, once a frame. While the camera is on and keeps the game's input, the game's
	// pad is not read (it keeps the state it had), so the pad flies the camera and nothing else.
	DEVTOOLS_DECLARE_CLASS_HOOK(void, gamepad_update, h_gamepad_update, t_gamepad_update, 0x0060EE30)

	// A camera pose in game world space: the rotation as the game's cameras keep it (a quaternion x, y, z, w; the rows it
	// makes are right, up and forward, as DirectXMath makes them), the vertical field of view in degrees.
	struct Pose
	{
		float position[3] = {};
		float rotation[4] = { 0.0f, 0.0f, 0.0f, 1.0f };
		float fov = 60.0f;
	};

	// Camera update task (the camera hand-over hook), every frame: moves the camera on (its key, its input, a track) and
	// says whether it has the game camera this frame, with the pose to render from. mayApply is false when the frame
	// cannot be drawn from it (no camera chosen, or a cubemap capture owning it): it then holds still and is not drawn.
	bool CameraPose(Pose& pose, bool mayApply);

	// Camera update task: the game camera's own pose this frame (before any pose was written over it) and the pose the
	// frame was drawn from (whatever placed it), every frame a camera is chosen.
	void FrameDrawn(const Pose& game, const Pose& drawn);

	// The game window's messages, before the game has them (its window thread): the mouse's movement for the camera, and
	// the keyboard and mouse kept from the game while it flies - true when the message is kept, with what to answer it.
	bool OnWindowMessage(HWND window, UINT message, WPARAM wParam, LPARAM lParam, LRESULT& result);

	// Whether the cinematic camera had the game camera at its last frame.
	bool Active();

	// The camera's pose as of its last frame (false while it is off).
	bool Current(Pose& pose);

	// Puts the camera at a pose (turning it on): a track's node gone to, a timeline scrubbed. Any thread; taken at the
	// camera's next frame.
	void Place(const Pose& pose);

	// The field of view the camera flies with now (vertical degrees).
	float Fov();

	// Locks the camera to a character by its unique id (0 unlocks), from the camera's next frame. Any thread.
	void LockTo(uint32_t characterId);

	// Whether a key is one the ini gives another helper (the debug menu, Possess, Unlock Mouse, hot reload). The camera's
	// keys and the screenshot key are read straight from the keyboard, so one of those would act twice: it is left out.
	bool TakenKey(int key);

	// The key that turns the camera on and off (CinematicCameraKey; 0, no key, when it is another helper's).
	int ToggleKey();

	// Turns the camera on behind a character (where it stands, its facing as a yaw in radians: forward = sin, 0, cos),
	// looking at it. Any thread.
	void GoBehind(const float position[3], float facing);
}

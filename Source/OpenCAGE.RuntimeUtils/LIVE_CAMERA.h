#pragma once

#include "DevTools.h"
#include "LIVE_LINK.h"
#include <cstdint>

/*
	Camera sync: the game renders from OpenCAGE's viewport camera while OpenCAGE asks it to (the live link's CAMERA), and
	OpenCAGE can read the game's own camera to put its viewport there (CAMERA_GET).

	Once a frame, on the camera update task (after the camera manager's own update and before the render viewpoints are
	updated from their cameras), the camera manager hands the camera the game picked to the engine: the main render
	viewpoint's view and projection, the engine's camera settings and the sound listener. Of that camera it reads only
	its state block (rotation, position, vertical field of view, near, far). The hook writes OpenCAGE's pose there,
	calls the original, and puts back exactly what was there - so the frame is rendered from OpenCAGE's camera while the
	camera behaviours (and whatever reads the camera itself) keep the game's own. What the original handed on stays
	OpenCAGE's until the next frame, though: the main render viewpoint, the engine's camera settings and the listener -
	and game logic that reads those follows the viewport too: camera viewcone triggers (TriggerCameraViewCone),
	GetCurrentCameraFov entities, the alien's "is it in the player's view" checks (the menace manager) and behaviour tree
	conditions on the player's camera direction. Keeping them on the game's camera would need the render viewpoint
	swapped around the rendering itself, not this hook.

	The pose is stored by the socket thread (the live link server, answered at once) and read by the hook every frame. It
	applies while the OpenCAGE connection that sent it lasts and the level it was sent for is running: a disconnect
	drops it; while no level, or another level, is running it waits, and applies again when its level is back (OpenCAGE
	still holds it as taken). A release, or a pose for another level, replaces it. Every zone is streamed in while it
	applies (the zone loader's live camera request), as the game only streams the zones around the player.

	The other way, the hook keeps the chosen camera's own state block every frame, before any pose is written over it
	(a snapshot, keyed to the running level's root and timed), and CAMERA_GET answers from the latest on the socket
	thread: the game's camera as of its last frame, never OpenCAGE's pose. No camera is chosen while a level loads, so
	no snapshot is taken then.

	Cinematic Tools hooks the same function later (MinHook over this Detours hook): the two chain, and CT's pose is
	written first, so while OpenCAGE drives the camera its pose wins - and what CAMERA_GET reads while CT's camera is on
	is CT's. The hook stays attached while the game runs.
*/
namespace LIVE_CAMERA
{
	// Hooked: the camera manager's hand-over of the chosen camera to the engine, once a frame on the camera update task.
	DEVTOOLS_DECLARE_CLASS_HOOK(void, synchronize_with_engine, h_synchronize_with_engine, t_synchronize_with_engine, 0x00032300)

	// Socket thread: a CAMERA request from the given connection (the live link server's current connection id when it
	// came in). Active: checks and stores the pose (game world space; fov in vertical degrees, <= 0 keeps the game's
	// own). Not active: drops it. Returns the answer.
	LIVE_LINK::Result SetPose(uint32_t connection, uint32_t root, bool active, const float position[3], const float forward[3], const float up[3], float fov);

	// Socket thread: a CAMERA_GET request. The game camera's own pose as of the last frame drawn from it, in game world
	// space ("position=..\nforward=..\nup=..\nfov=..\nframe=..", see CAMERA_GET in LIVE_LINK_SERVER.h), or why there is none.
	LIVE_LINK::Result GetPose(uint32_t root);

	// Whether the game is rendering from OpenCAGE's pose now (STATUS camera_sync, the overlay).
	bool Applying();
}

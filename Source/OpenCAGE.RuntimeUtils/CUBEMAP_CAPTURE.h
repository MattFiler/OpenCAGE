#pragma once

#include "DevTools.h"
#include "LIVE_LINK.h"
#include <cstdint>
#include <functional>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

struct IDXGISwapChain;

/*
	Cubemap capture: OpenCAGE asks for the six views from a point (an EnvironmentMap entity's position, as a reflection
	cube), and gets them back as square pictures to build a cube texture from (the live link's CAPTURE_CUBEMAP).

	While a capture runs it owns the game camera: the camera hook (LIVE_CAMERA.cpp) asks here first, every frame, and
	renders from the face being captured - the capture point, a 90 degree vertical field of view, looking along +X, -X,
	+Y, -Y, +Z then -Z of game world space with the up each cube face has (so face pictures line up the way the game's
	cube lookups expect). The zones around the point are streamed in (the zone loader's capture request), and the first
	face waits until what is loaded has stopped changing for a while. Then each face is held for some frames (so the
	frames in flight, motion blur and the like settle on the new view) and its picture is taken from the finished frame,
	at the D3D11 frame presentation, before RuntimeUtils draws its own overlay. The middle square of the frame is exactly
	the face (the field of view is vertical, so the square as tall as the frame spans 90 degrees both ways), and is scaled
	down to the size asked for by averaging the pixels it covers.

	For as long as it runs, the game's HUD and other flash user interface is hidden (each piece's own hide flag, put back
	afterwards), and the post-processing that would not belong in a reflection is turned off frame by frame: film grain,
	vignette, chromatic aberration, lens distortion and depth of field. Colour grading, bloom and tone mapping are left as
	the game has them: the faces are what the player would see from there.

	With the debug menu on, a capture is turned away while any of its settings that change how the game draws is on (a
	rendering switch, a debug view, a light muted or a kind of light left out, a cinematic look override...), as the faces
	go into the level's reflection maps: the answer names them, to be turned off first. kKeepDebugLook captures with them.

	One capture at a time, for the connection that asked: it ends when the six faces are taken (answered with them), or
	early when OpenCAGE disconnects, the level changes, the game is paused, or nothing is drawn for a while (answered with
	why). Anything else that drives the camera (OpenCAGE's viewport camera, Possess) waits until it is over.

	Meanwhile the game's keyboard and mouse are kept from it, so the player stands still (and doesn't turn, fire or open
	anything) even if the game window is clicked into, and the cursor is held free of the window, shown over it, as
	Unlock Mouse frees it (MOUSE_UNLOCK::Hold). Escape still reaches the game: its pause menu ends the capture, so it is
	the way to stop one from the game. A gamepad is left alone.
*/
namespace CUBEMAP_CAPTURE
{
	// Hooked: the post-processing settings' per-frame update. After it, the debug menu's cinematic look is written over the
	// level's (CINEMATIC_VISUALS.h), then the effects named above are turned off while a capture runs. With the debug menu
	// on and Live Link's camera sync off, CINEMATIC_VISUALS attaches it (the DLL attaches it with camera sync on).
	DEVTOOLS_DECLARE_CLASS_HOOK(int, post_process_update, h_post_process_update, t_post_process_update, 0x00608c50)

	// What a capture is asked for (see CAPTURE_CUBEMAP in LIVE_LINK_SERVER.h)
	struct Request
	{
		uint32_t connection = 0;   // the live link connection that asked
		uint32_t root = 0;         // the level it was asked for (0: any)
		float position[3] = {};    // game world space
		uint32_t size = 256;       // face size in pixels
		uint32_t settleFrames = 8; // frames each face is held before its picture is taken
		uint32_t streamTimeoutMs = 30000; // the longest the first face waits for zones to finish streaming in
		uint32_t flags = 0;        // kKeepHud: leave the HUD showing; kKeepPostEffects: leave the post-processing alone;
		                           // kKeepDebugLook: capture with the debug menu's rendering changes (not turned away)
	};
	constexpr uint32_t kKeepHud = 1u << 0;
	constexpr uint32_t kKeepPostEffects = 1u << 1;
	constexpr uint32_t kKeepDebugLook = 1u << 2;

	// Called when the capture ends, from the render thread: ok, a message, and (when ok) the answer's payload.
	using Done = std::function<void(bool ok, const std::string& message, std::vector<uint8_t>&& payload)>;

	// Socket thread: starts a capture (returns ok and "Capturing...") or refuses it at once (and done is never called).
	LIVE_LINK::Result Start(const Request& request, Done done);

	// Whether a capture is running (any thread)
	bool Capturing();

	// Camera hook: whether a capture owns the camera this frame, and if so the pose to render from (rotation as the
	// camera's quaternion, x y z w; position; vertical field of view in degrees).
	bool CameraPose(float rotation[4], float position[3], float& fov);

	// D3D11 frame presentation, before the overlay draws: moves the capture on, and takes a face's picture when it is due.
	void OnPresent(IDXGISwapChain* swapChain);

	// The overlay's line while a capture runs (empty otherwise).
	std::string OverlayText();

	// The game window's messages, before the game has them (its window thread): whether the message is kept from the
	// game - the keyboard (bar Escape) and mouse while a capture runs - true when it is, with what to answer it. With no
	// capture running, the debug menu's cinematic camera has them (CINEMATIC_CAMERA::OnWindowMessage).
	bool OnWindowMessage(HWND window, UINT message, WPARAM wParam, LPARAM lParam, LRESULT& result);
}

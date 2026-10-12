#pragma once

#include <cstdint>
#include <string>
#include <vector>

struct IDXGISwapChain;

/*
	The live link connection: a WebSocket server on 127.0.0.1 (port 8765 by default) that OpenCAGE connects to.

	Binary messages carry requests, little-endian:
	  u32 magic 'OCLL', u16 version (1), u16 command, u32 request id, then the command's payload.
	Every request is answered with the same header (command | 0x8000), then u8 ok, u32 length + UTF-8 message, then any
	reply payload. Text messages are the older JSON packets ({"version":1,"load_level":"..."}), which get no reply.
	root is the root composite of the level OpenCAGE has open: a request for another level is refused on the entity
	thread. Connections that carry an Origin header (web pages) are refused, and one client is served at a time (another
	is turned away at once).

	Requests on entities are carried out on the entity thread (from the hook on the entity manager's per-frame
	processing), screenshots and level loads on the render thread (the hook where D3D11 shows each finished frame), so the
	socket thread never touches the game. CAMERA and CAMERA_GET are the exceptions: the socket thread only checks which
	level is running (plain reads of the entity manager) and stores the pose, or reads the camera hook's last snapshot of
	the game camera, answering at once - they are never queued behind edits held while a level starts - and the camera
	hook applies the pose (see LIVE_CAMERA.cpp). ANIMATION and ANIMATION_GET are answered at once the same way: the
	request is stored, or the entity thread's last snapshot of what it did with it read, and the entity thread carries
	it out every frame (see LIVE_ANIMATION.h). So are TRACE and TRACE_GET: what to trace is stored, or what the trace hooks
	gathered handed over, and the hooks gather it on whichever threads run the level's scripts (see LIVE_TRACE.h).
*/
namespace LIVE_LINK_SERVER
{
	enum Command : uint16_t
	{
		STATUS = 1,          // -> message "running=0|1\nroot=..\nroot_name=..\ntemplates=..\nstate=..\nplaying=0|1\nloading=0|1[\nloading_reason=..]\ncamera=x,y,z\ncamera_forward=..\ncamera_up=..\ncamera_sync=0|1\nanimation=0|1\ntrace=0|1"
		                     //    (CALL_METHOD and APPLY_COMPOSITE are held while loading=1 - scripts paused, or running again for under 5 s:
		                     //    level starting, pause menu - and turned away after 20 s; camera_sync=1 while the game renders from CAMERA's pose;
		                     //    animation=1 while the game has a CAGEAnimation taken for ANIMATION - held, played, or waiting to be given back
		                     //    - as of the last frame; trace=1 while this connection has watches set by TRACE)
		CALL_METHOD = 2,     // u32 root (0: any), u32 composite, u32 entity, u32 method, u32 path count, u32 path[count] (instance entity ids from the root)
		APPLY_COMPOSITE = 3, // u32 root (0: any), u32 composite, u32 image size, image, u32 relocation count, u32 relocations[count],
		                     //    then (optional - older OpenCAGE builds stop here) u32 data pin count, { u32 entity, u32 parameter,
		                     //    u32 direction (1: into that entity, 2: out of it, into the entity it links to) }[count]: the
		                     //    pins of the composite's data links, by the entity each link is kept on. Whatever a data link that
		                     //    came or went feeds is live edited, as if its parameter had been edited.
		LOAD_LEVEL = 4,      // u32 length, level name (e.g. "PRODUCTION\\BSP_TORRENS" or "BSP_TORRENS")
		SCREENSHOT = 5,      // u32 length, path of a .bmp to write (nothing else is written)
		DESCRIBE = 6,        // u32 composite -> message listing its running instances
		CAMERA = 7,          // u32 root (0: any), u8 active (1: render from this pose, 0: give the game its camera back), then only if active:
		                     //    f32 position[3], f32 forward[3], f32 up[3], f32 fov (vertical degrees; <= 0 keeps the game's own) - game world
		                     //    space (Y up, left-handed). -> "Camera following OpenCAGE" / "Camera released". The pose applies while this
		                     //    connection lasts and the level stays the one it was sent for; zones are streamed in while it does.
		CAMERA_GET = 8,      // u32 root (0: any) -> message "position=x,y,z\nforward=x,y,z\nup=x,y,z\nfov=deg\nframe=N": the game's own camera
		                     //    as of its last frame (never CAMERA's pose), in game world space - forward and up unit, fov vertical
		                     //    degrees, 4 decimals whatever the locale; frame is bumped by every frame the hook keeps. Refused with
		                     //    no level running, another level running, LiveLinkCamera=0, or no frame of the running level drawn from a
		                     //    camera in the last second ("The game has not drawn a frame from its camera yet": a level loading).
		ANIMATION = 9,       // u32 root (0: any), u8 mode (0: give the animation back, 1: hold it at a time, 2: play it from a time), then only if
		                     //    mode != 0: u32 composite, u32 entity (the CAGEAnimation), u32 path count (at most 64), u32 path[count] (instance
		                     //    entity ids from the root; none: every running instance of the composite), f32 time (seconds), f32 rate (play:
		                     //    times the game's own frame time, 1 = real time; hold: ignored), u8 flags (1: loop - play wraps to 0 rather
		                     //    than stopping just short of the end; 2: events - play runs the event tracks its frames pass over, as the game's
		                     //    own playback does; a hold never runs them), u32 sequence (OpenCAGE's count, given back by ANIMATION_GET once a
		                     //    frame has applied the request; play starts again from its time only when the sequence changes).
		                     //    -> message in ANIMATION_GET's form, as of the last frame (the request applies from the next one). Stored and
		                     //    answered at once, never queued behind edits; carried out by the entity thread every frame while this connection
		                     //    lasts. One animation per connection: one for another target gives the previous one back first; a disconnect
		                     //    gives it back; another level starting drops it (its entities have gone) and it waits for its own level. A
		                     //    release is always taken. Refused only when it cannot be read, for a mode above 2, a path longer than 64, or
		                     //    while another level runs; while no level runs, or edits must wait, it is stored and ANIMATION_GET says it is
		                     //    waiting, and why.
		ANIMATION_GET = 10,  // u32 root (0: any) -> message "state=released|waiting|held|playing|ended|not_found|not_animation|disabled|no_data|cinematic
		                     //    \nreason=..\ntime=s\nlength=s\nsequence=N\nframe=N\ninstances=applied/found\nwas_playing=0|1" - 4 decimals whatever
		                     //    the locale; length is the game's own (the animation's length parameter: 10 when unset, clamped 0.01..10000);
		                     //    sequence is the last request a frame applied - the one whose time the game shows (0 while nothing is taken),
		                     //    so a newer request that waits leaves it as it was; frame is bumped by every entity frame that wrote it;
		                     //    was_playing=1 if the game was advancing the animation when it was taken. Never refused (a malformed request
		                     //    aside).
		TRACE = 11,          // u32 root (0: any level), u8 on (0: stop tracing - nothing follows; 1: trace the watches that follow), then only if on:
		                     //    u32 watch count (1..512), per watch: u32 composite, u32 path count (0xFFFFFFFF: every instance of the composite;
		                     //    otherwise at most 64), u32 path[count] (instance entity ids from the root to the watched instance; none: the
		                     //    root itself). -> "Tracing N composite(s)" / "Tracing stopped"; refused "Malformed request" (sizes, more than 512
		                     //    watches, a path over 64; a game from before 512 refuses more than 32 this way) or "Another level is running"
		                     //    (root not 0, a level running, and its root another - which also stops the trace that was running).
		                     //    While no level runs it is stored, and applies when the level with that root runs. While on, the game gathers
		                     //    the script activity in the watched instances (see LIVE_TRACE.h) for TRACE_GET to take. A new TRACE replaces the
		                     //    previous one (anything not yet taken is dropped); it belongs to this connection: a disconnect stops it.
		TRACE_GET = 12,      // u32 root (0: any) -> message "records=N dropped=D" and a payload: u32 format (1), u32 batch (every take since the
		                     //    game started), u32 dropped (distinct activities not kept since the last take because 4096 were), u32 record
		                     //    count, then per record: u8 kind (1: an entity fired one of its outputs; 2: an entity read a parameter through
		                     //    a data link; 3: an entity sent a value out through a data link, or looked up what a pin is attached to; 4: a
		                     //    method was called through a logic link), u8 path count, u8 source path count (kinds 2-4; else 0), u8 0, u32
		                     //    count (times since the last take, saturating), u32 age (ms since it last happened, as of this answer), u32
		                     //    composite (the one holding the entity: its owner instance's; 0 for the root instance), u32 entity (its id
		                     //    there; kind 4: the entity whose method was called), u32 pin (kind 1: the output fired; 2: the parameter read; 3:
		                     //    the pin written through; 4: the method), u32 self (the composite the entity is an instance of; else 0), kinds
		                     //    2-4 only: u32 source composite, u32 source entity, u32 source pin, u32 source self (kinds 2 and 3: the link's
		                     //    other end - where a read came from, where a write went; kind 4: the entity that called and the output it
		                     //    fired, all 0 for a call CALL_METHOD made; the same way), then u32 path[path count] (instance entity ids
		                     //    from the root to the instance holding the entity), kinds 2-4 only: u32 source path[source path count].
		                     //    Everything handed over is cleared. Not tracing: no records. Refused only when malformed or while another level
		                     //    runs.
	};

	void Start(uint16_t port);
	void Stop();

	// Entity thread: carries out the queued entity requests.
	void ProcessEntityRequests();

	// Render thread, after the overlay has drawn: carries out queued screenshots and level loads.
	void ProcessRenderRequests(IDXGISwapChain* swapChain);

	// Whether OpenCAGE is connected, and a line about the last thing done (if it was recent), for the overlay.
	bool Connected();
	std::string LastActivity(unsigned int withinMs);

	// Which connection is current: bumped when OpenCAGE connects and when it goes, so something a request left behind
	// (the camera pose, the animation drive) can tell its connection has ended.
	uint32_t CurrentConnection();

	// Sets the overlay's activity line (for things done outside the server, e.g. the camera hook).
	void ShowActivity(const std::string& text);
}

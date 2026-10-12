#pragma once

#include "LIVE_LINK.h"
#include <cstdint>
#include <string>
#include <vector>

/*
	Animation drive: OpenCAGE's CAGEAnimation editor, in Animation Mode, drives one CAGEAnimation in the running game - it
	holds it at the editor's playhead while that is scrubbed, plays it, and gives it back afterwards (the live link's
	ANIMATION; ANIMATION_GET follows it).

	The request is stored here by the socket thread and answered at once, never queued behind edits held while a level
	starts. The entity thread carries it out every frame (LIVE_LINK.cpp, straight after the queued requests and before the
	game's own processing of its entities, so an edit pushed in the same frame is already in): it finds the animation by
	the level's root and the instance path, as a method call does, takes the animation's clock from the game if the game is
	advancing it (that flag alone is cleared: nothing is stopped and nothing fires), and applies the time with the game's
	own function that applies an animation at a time - so every track the level binds behaves as it does in the level. A
	hold never runs the event tracks; play runs those its frames pass over only when asked to; and neither ever reaches
	the animation's length (there the game would finish it, and fire its finished and interrupted outputs into the level).

	Giving it back puts back the times and flags it had - as the level's own logic left them, if that started, stopped or
	rebound it meanwhile - applies its own time again and gives the game its clock back; but if the level's logic started
	or stopped it since the drive last looked at it, it is left just as that left it. One the game showed nothing of when
	it was taken (not started, not advanced, at its start: one it never applied, say) also has the entities it moves put
	back as they were then - a transform as it was, and a track's number under what its entity shows - since its pose at
	its start is not what they showed (another animation may have moved them). That happens on a release, when another
	animation is asked for, and when the connection that asked goes; when its level goes it is forgotten instead (its
	entities go with it). No reference to a game object is kept from one frame to the next: the animation is found again
	every frame. While the level's scripts are not running (a level starting, the pause menu, a cutscene or a message on
	screen) nothing is applied or given back: it waits, and ANIMATION_GET says why. An animation whose zone is not loaded
	is not taken; one already taken is driven on through up to a second of that (with the game camera away from the
	player, the zone's flag drops for a frame or two at a time), and waits only after it. The drive itself stays stored
	until it is released or its connection goes, so it is taken up again when its level is played again.

	What the entity thread did each frame is kept as a snapshot, which ANIMATION_GET (and ANIMATION's answer) reads.
*/
namespace LIVE_ANIMATION
{
	constexpr uint32_t kMaxPath = 64; // the longest instance path taken

	enum Mode : uint8_t { Release = 0, Hold = 1, Play = 2 };
	enum Flags : uint8_t { Loop = 1, Events = 2 };

	// What OpenCAGE asked for (see ANIMATION in LIVE_LINK_SERVER.h)
	struct Drive
	{
		uint32_t connection = 0;     // the connection that sent it (the live link server's current connection id when it came in)
		uint32_t root = 0;           // the level it was sent for (0: any)
		uint8_t mode = Release;
		uint32_t composite = 0;      // the composite the animation is in
		uint32_t entity = 0;         // the animation's entity id in it
		std::vector<uint32_t> path;  // composite-instance entity ids from the root (none: every running instance of the composite)
		float time = 0.0f;           // seconds
		float rate = 1.0f;           // play: times the game's own frame time
		uint8_t flags = 0;           // Loop, Events
		uint32_t sequence = 0;       // OpenCAGE's count, given back once a frame has applied the request
	};

	// The snapshot's state word, as ANIMATION_GET spells it
	enum class State { Released, Waiting, Held, Playing, Ended, NotFound, NotAnimation, Disabled, NoData, Cinematic };
	const char* StateName(State state);

	// What the entity thread did with the drive, as of its last frame
	struct Snapshot
	{
		State state = State::Released;
		std::string reason;
		double time = 0.0;       // the time shown (or asked for, while it is not shown)
		double length = 0.0;     // the animation's own length, as the game reads it
		uint32_t sequence = 0;   // the request whose time the game shows: the last one a frame applied (0 while nothing is taken)
		uint32_t applied = 0;    // instances held at the time
		uint32_t found = 0;      // instances the animation was found in
		bool taken = false;      // an animation is taken from the game (held, played, or waiting to be given back)
		bool wasPlaying = false; // the game was advancing it when it was taken
	};

	// Socket thread: an ANIMATION request, read (pathCount as sent; the path itself only when it is not too long). Stored,
	// or refused; the answer carries the snapshot as of the last frame.
	LIVE_LINK::Result SetDrive(const Drive& drive, uint32_t pathCount);

	// Socket thread: refuses an ANIMATION request that could not be read (logged once while the same reason repeats).
	LIVE_LINK::Result Refuse(const std::string& message);

	// Socket thread: an ANIMATION_GET request.
	LIVE_LINK::Result GetState(uint32_t root);

	// Whether the game has an animation taken for OpenCAGE as of the last frame - holding or playing it, or waiting to give it
	// back (STATUS animation).
	bool Driving();

	// Entity thread: whether a drive is stored - the first thing checked every frame, so nothing more runs when none ever was.
	bool Wanted();

	// Entity thread: a copy of the stored drive, and its generation (bumped by every request).
	uint32_t CurrentDrive(Drive& drive);

	// Entity thread: drops the stored drive (its connection has gone), unless a newer request has replaced it since.
	void DropDrive(uint32_t generation);

	// Entity thread: what this frame did (bumps the snapshot's frame count).
	void Publish(const Snapshot& snapshot);
}

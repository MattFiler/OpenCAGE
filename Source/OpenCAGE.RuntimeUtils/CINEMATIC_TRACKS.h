#pragma once

#include "CINEMATIC_CAMERA.h"

/*
	Camera tracks for the cinematic camera (the Cinematic group's Tracks tab), ported from Cinematic Tools' tracks, by
	Hattiwatti: a track is a list of nodes, each a pose of the camera (where it is, which way it looks, its field of view
	and the depth of field's focus) at a time on the track. Played, the camera moves along a Catmull-Rom spline through the
	nodes' positions (and their rotations, field of view and focus, as the track's locks say), its pace eased the same way
	through the nodes' times, in real time (so a paused or slowed game doesn't slow it) at the playback speed, once or
	looping. "Play manually" moves along the track with the camera's up and down keys instead.

	Nodes are added where the camera is (each the node gap after the last), deleted, updated to where the camera is, gone
	to, and moved in time (kept in order, a tenth of a second apart at least); the timeline scrubs the camera along the
	track while it isn't playing. Several tracks can be kept, one chosen. "Show the track" draws the path and the nodes
	over the game while it isn't playing.

	Tracks are saved to and loaded from a text file in the game folder, next to OpenCAGE.RuntimeUtils.ini
	(OpenCAGE.CinematicTracks.txt unless another name is given): every track, with the level it was made in, its nodes'
	times and poses (positions in game world space). Loading replaces the tracks kept.
*/
namespace CINEMATIC_TRACKS
{
	// Camera update task, each frame the cinematic camera is on: while a track plays, moves it on by dt seconds (playing
	// manually: by dt times scrub, -1..1) and puts the track's pose in pose as its locks say (the position always; the
	// rotation, field of view and depth of field when locked). Returns whether a track is playing.
	bool Advance(float dt, float scrub, CINEMATIC_CAMERA::Pose& pose);

	// Whether a track is playing.
	bool Playing();

	// The cinematic camera's keys while it flies (any thread): add a node where the camera is, delete the last one, and
	// play or stop.
	void AddNodeHere();
	void DeleteLastNode();
	void TogglePlay();
}

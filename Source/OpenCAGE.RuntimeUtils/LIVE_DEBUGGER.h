#pragma once

#include "DevTools.h"
#include <cstdint>
#include <string>
#include <vector>

/*
	Script breakpoints: OpenCAGE (or the Debug Menu's Debugger tab) stops the level's scripts when one of its entities is
	about to have a method called, and the game holds that call until it is told to go on.

	The game still has most of a script debugger of its own. The entity manager keeps a list of breakpoints (empty in
	the shipped game: nothing fills it), each an entity - by its id and the id of the composite instance it is in, as the
	game names entities in its own records - and a method, sorted, and it looks every method call it is about to make up
	in that list, for the entity the call goes to and for the entity behind it when that is a proxy. When one is there it
	does not make the call: it keeps it, ends the round of calls it was making, and calls a function with the breakpoint's
	number - a function the shipped game leaves empty, so nothing stops. The next time the
	game makes its calls it makes the one it kept first, without looking it up again (so it does not stop on it twice),
	then carries on.

	Here, that list is filled from the breakpoints set (in each running instance a breakpoint names, an entry per method),
	and the game's call of the empty function is pointed at our own, which notes what was hit and pauses the game with
	the game's own pause state's "breakpoint" reason, which stops the level's scripts and the
	game's clock (the game shows its pause as it does for its menu: the HUD's pause handling, the mouse cursor), and with
	the level's scripts held at once rather than from the game's next frame. Going on takes that reason away. A step sets the
	pause state's "single cycle" reason as well, which lets the scripts make the held call and the rest of its round of
	calls (those due at the same moment) and stop again, each step the next round; a frame step sets its "single frame"
	reason, which lets one whole frame of the game run.

	The list is kept in line on the entity thread (the frame after a breakpoint is set or taken away, after a level loads,
	and every second, which takes in instances spawned since), and only while the level a breakpoint was set for runs. It
	is written under the game's own lock on its entity manager, which the game holds while it makes its calls. A level
	that unloads, or is loaded again, while held has the pause taken away.

	Also here, for OpenCAGE's flowgraphs: the state flags of the entities of a composite instance, and the value an
	entity sees for one of its parameters (through its links, as the entity itself would read it).
*/
namespace LIVE_DEBUGGER
{
	// Entity thread, once a frame (from the hook on the entity manager's per-frame processing, before the game's own):
	// keeps the game's breakpoint list in line with the breakpoints set, and lets go of a pause the debugger holds on a
	// level that has gone.
	void EntityFrame();

	// Whether the debugger holds the level's scripts (at a breakpoint, or paused by it), and why, for the Live Link's
	// wait message (null while it does not). Any thread.
	const char* HeldReason();
	bool Holding();

	// What the Debugger tab shows (render thread)
	struct BreakpointView
	{
		int id = 0;
		uint32_t root = 0;
		uint32_t composite = 0;
		std::string compositeName;
		bool anyInstance = false;
		std::vector<uint32_t> path;
		uint32_t entity = 0;
		std::vector<uint32_t> methods;
		std::string label;
		bool enabled = true;
		uint32_t hits = 0;
		int instances = 0;   // running instances it was found in
		int records = 0;     // entries it has in the game's list
		std::string problem; // why it is in no running instance, when it is not
	};
	struct HitView
	{
		bool held = false;           // the scripts are held by the debugger
		bool atBreakpoint = false;   // ... at a breakpoint (not just paused)
		uint32_t sequence = 0;       // counts up with every hit
		int breakpoint = 0;
		uint32_t entity = 0, method = 0, type = 0, composite = 0, instance = 0;
		std::string compositeName;
		std::vector<uint32_t> path;
		double time = 0.0;
		uint32_t steps = 0;
		struct Link { uint32_t entity, method, composite; std::string compositeName; std::vector<uint32_t> path; };
		std::vector<Link> chain;     // the calls that led to it, latest first
	};
	std::vector<BreakpointView> Breakpoints();
	HitView Hit();
	bool Patched();                  // the game's breakpoint call reaches ours
	int RecordsInGame();             // entries in the game's list now
	std::string Status();            // a line on what the list is waiting for, if anything

	// The tab's buttons (any thread): what each does, as its command does
	std::string Continue();
	std::string Step(bool frame);
	std::string Pause();
	std::string Remove(int id);
	std::string RemoveAll();
	std::string Enable(int id, bool enabled);

	// The text an id is shown as (as OpenCAGE shows a ShortGuid: its four bytes in hex, lowest first)
	std::string IdText(uint32_t id);
}

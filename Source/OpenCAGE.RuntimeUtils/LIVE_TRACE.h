#pragma once

#include "LIVE_LINK.h"
#include <cstdint>
#include <vector>

/*
	Script activity: while OpenCAGE asks (the live link's TRACE), the game notes which links its scripts follow in the
	composite instances OpenCAGE watches, and OpenCAGE takes what was noted every so often (TRACE_GET) to light those links
	up in its flowgraphs.

	Four kinds of activity are noted, each from a hook on the game's own code that only reads:
	  - an entity fired one of its outputs, and so followed every logic link out of it. Every relay goes through the game's
	    one function that fires an output: a composite instance firing one of its own pins does too (a link from an entity
	    to its composite's variable fires that pin of the instance in turn), and a proxy fires through its own call of it.
	    An output with no name (id 0, which the game fires for its own purposes) is not noted.
	  - an entity read one of its parameters through a data link. Each of the game's readers of a kind of value is hooked;
	    once it has read, the links the reader's parameter cache holds for that parameter (read under the game's own lock
	    on that cache) are the data links the read followed, and each gives one activity: the reader and its parameter, and
	    the entity and pin at the link's other end. A parameter with no link is read from the entity's own value, and gives
	    nothing. (The game stops at the first link that gives it a value, so a parameter with several links notes them all.)
	    At most 64 links of one pin are noted for each read (retail's busiest data pin has 25); a pin with more is
	    counted, and the count is in the log line when the trace stops.
	  - an entity sent a value out through a data link, or looked up what one of its pins is attached to. An entity writes
	    an output by asking for the entities attached to that pin, and setting each; the game's one lookup of them is
	    hooked, and once it has looked, the links the entity's cache holds for the pin give one activity each, the same way
	    as a read (at most 64 of them, likewise): the entity and its pin, and the entity and pin the value went to. Where a link goes on to a pin that is
	    not an entity's own value (a composite instance's pin, say), the game looks up that entity's attached entities in
	    turn, through the same lookup - so each step the value takes is noted, the instance's as well. A few lookups are not
	    writes (what objects a pin is attached to, for instance): they are uses of the links too, and noted the same way.
	    A link from an entity to its own composite's variable is not in the entity's cache, so a write through it is not
	    seen (nor does the value go on through the instance's links outside).
	  - a method was called through a link. The calls a fired output makes on the entities its logic links lead to are
	    queued through one function of the entity manager, which is hooked: the entity whose method was called and the
	    method, and the entity that called it and the output it fired. A link from an entity to its own composite's pin is
	    not queued (it fires the instance's pin at once, see the first kind). A few other things in the game queue calls
	    through it too. A call OpenCAGE makes (the live link's CALL_METHOD) is queued another way: it is noted where the
	    live link makes it, as a call with no caller.
	An entity is told by its id, the composite that holds it (its owner instance's; none for the level's root instance),
	the path of instance entity ids from the level's root to that owner and, if it is itself a composite instance, the
	composite it is an instance of.

	A watch is a composite and an instance path (or every instance of the composite). An activity is kept when an end of it
	is in a watched instance: the entity sits in it, or the entity IS it (an instance firing or reading one of its own pins,
	whose own path is its owner's path and its id); for the kinds with two ends (a read, a write, a call), either end.
	Kept activities are added up until OpenCAGE takes them: one record for each distinct kind, entity, pin, linked entity
	and its pin, with how many times it happened and when it last did. At most kMaxRecords are kept between takes; after
	that new ones are counted as dropped (told apart by a hash, so the count is close but not exact), as is one whose
	instance paths no longer fit in the room set aside for them (12 steps a record on average). That room is all set
	aside when tracing starts, so keeping a record never allocates. A take hands over everything kept and starts afresh.

	While nothing is traced each hook costs one check of a flag before or after it calls the game's own function, and
	allocates nothing. While tracing, a hook first checks the running level and the composites of the entity's owner (and
	of the entity, if it is an instance) against the watched ones - a read or write whose entity is not watched also reads
	the entity's cached links, to check the other ends - and only an activity that passes has its instance paths read and
	the watches applied exactly. The watched composites are kept in a hash set, and each one's watches in a list of its
	own: the first look takes no longer with more watches, and the exact one only goes through the watches of the
	composites its ends are in. The hooks run on several of the game's threads: the records are behind a reader/writer
	lock (counting one already kept takes the shared side, so threads do not wait on each other - as does counting one
	as dropped once the store is full: only adding a record takes the lock to itself), and every read of game
	memory is guarded, a fault dropping that one activity (counted, and logged once per trace). Tracing belongs to the connection
	that asked for it: a new TRACE replaces it (dropping anything not yet taken), and a disconnect stops it and drops what
	was gathered. Each start and stop is logged, the stop with how many hook calls were looked at and the time they took.
*/
namespace LIVE_TRACE
{
	constexpr uint32_t kMaxWatches = 512;           // watches in one TRACE
	constexpr uint32_t kMaxPath = 64;               // the longest instance path (a watch's, or a record's)
	constexpr uint32_t kAnyInstance = 0xFFFFFFFF;   // a watch's path count for every instance of its composite
	constexpr uint32_t kMaxRecords = 4096;          // distinct activities kept between takes
	constexpr uint32_t kFormat = 1;                 // the TRACE_GET payload's format

	enum Kind : uint8_t { Fired = 1, Read = 2, Sent = 3, Called = 4 };

	// What OpenCAGE watches (see TRACE in LIVE_LINK_SERVER.h)
	struct Watch
	{
		uint32_t composite = 0;
		bool any = false;            // every instance of the composite (path unused)
		std::vector<uint32_t> path;  // instance entity ids from the root to the watched instance (none: the root itself)
	};

	// Socket thread: a TRACE request from the given connection (the live link server's current connection id when it came
	// in). Replaces what was traced: stops tracing, or traces the watches (at most kMaxWatches, paths at most kMaxPath -
	// checked by the caller) while the level with this root runs (0: any). Refused while another level runs.
	LIVE_LINK::Result Set(uint32_t connection, uint32_t root, bool on, std::vector<Watch>&& watches);

	// Socket thread: a TRACE_GET request. Hands over what was gathered since the last take as the reply payload (see
	// TRACE_GET in LIVE_LINK_SERVER.h) and starts afresh; refused while another level runs.
	LIVE_LINK::Result Take(uint32_t connection, uint32_t root, std::vector<uint8_t>& payload);

	// Socket thread: the connection has gone - its tracing stops, and what it gathered is dropped.
	void ConnectionEnded(uint32_t connection);

	// Whether the current connection has watches set (STATUS trace).
	bool Tracing();

	// Entity thread: OpenCAGE has had a method called on an entity (the live link's CALL_METHOD), which the game queues
	// another way than a link's call. While tracing, it is noted like one, with no caller (the source fields all 0).
	void NoteOwnCall(void* entity, uint32_t method);

	// Attaches (or detaches) the hooks, inside the caller's hook transaction. They pass straight through until a TRACE.
	void AttachHooks(bool attach);
}

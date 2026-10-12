#pragma once

#include "DevTools.h"
#include <cstdint>
#include <string>
#include <vector>

/*
	Live link: OpenCAGE edits the scripting of a level while the game runs it.

	The game keeps the level's COMMANDS.PAK in memory exactly as it is on disk (the entity manager's loader reads the
	whole file into one block). A composite template is the PAK's composite record, whose arrays are each stored as an
	offset, in 4-byte words from the start of that block, and a count. OpenCAGE sends a composite as a standalone PAK
	image plus the words in it that are offsets; the image is rebased onto the block (32-bit wrap-around makes any
	address reachable), its parameters are turned into the game's variable objects the way that loader does, and the
	template record is pointed at the new arrays. The running instances of that composite are then brought in
	line: entities that went are shut down and removed, new ones are constructed and initialised, entities whose
	parameters changed are live edited in place (the game's own live edit of an entity's state - what CA's own live link
	used, which reinitialises the entity and lets it react to the edit itself), and every entity's parameter/link cache
	is flushed.

	Method calls (e.g. "start" on a DebugTextStacking) are queued as triggers, the way the game's own code does.

	Everything here runs on the thread that processes entities (from the hook on the entity manager's per-frame
	processing, below).
*/
namespace LIVE_LINK
{
	// The composite template, as laid out in COMMANDS.PAK and in memory.
	struct PackedArray { uint32_t offset; uint32_t count; };
	struct CompositeTemplate
	{
		const char* debug_name;     // 0x00 - set at load, from template_name
		uint32_t template_name;     // 0x04 - the composite's name (a string)
		PackedArray shared_path;    // 0x08
		uint32_t reference;         // 0x10 - the composite's ShortGuid
		PackedArray links;          // 0x14 - the links between its entities
		PackedArray params;         // 0x1C - its entities' parameters
		PackedArray aliases;        // 0x24
		PackedArray mappings;       // 0x2C
		PackedArray connectors;     // 0x34
		PackedArray proxies;        // 0x3C
		PackedArray entities;       // 0x44 - its entities, one record each of what to create
		PackedArray resources;      // 0x4C
		PackedArray animations;     // 0x54
		PackedArray sequences;      // 0x5C
		void* instances;            // 0x64 - every live instance, an array of pointers to them (runtime)
		void* shared;               // 0x68 - a pointer to an entity (runtime)
		uint32_t begin_xref;        // 0x6C
		uint32_t end_xref;          // 0x70
	};
	static_assert(sizeof(CompositeTemplate) == 0x74, "CompositeTemplate layout");

	struct Result
	{
		bool ok = false;
		std::string message;
	};

	// Hooked: the entity manager's per-frame processing, on the entity thread. Drains the queued live link requests.
	DEVTOOLS_DECLARE_CLASS_HOOK(void, process, h_process, t_process, 0x0052fd30)

	// Whether a level's entities are up (the entity manager exists, is initialised and has a root instance).
	bool LevelRunning();

	// Whether the game is running the level with this root composite (0 matches any).
	bool RunningLevelIs(uint32_t root);

	// The game's state (4 = gameplay, 3 = starting gameplay while a level loads), -1 if it cannot be read.
	int GameState();
	const char* GameStateName(int state);

	// Why edits and method calls cannot be taken at all now (no level, or the level unloading), or null.
	const char* NotReadyForEdits();

	// Why edits and method calls should wait a while (the level is still starting, or is over and restarting from a
	// checkpoint, the game is paused, or the level's scripts are stopped), or null. The server holds them.
	const char* EditsMustWait();

	// Called every entity frame: follows the level's script transport, which must have been running for a few seconds
	// before the check above lets edits through.
	void TrackTransport();

	// Called every entity frame, after the queued requests and before the game's own processing: holds or plays the
	// CAGEAnimation OpenCAGE asked for (the live link's ANIMATION, stored by LIVE_ANIMATION), and gives it back. Returns at
	// once while nothing is asked for or held.
	void DriveAnimation();

	// The game's level manager, from the game's globals (null before it exists).
	void* LevelManager();

	// A pin a data link goes through, as OpenCAGE sees it (the image cannot say which links carry data): the entity the
	// link is kept on, its parameter, and which way the data goes - into that entity, which reads its value through the
	// link, or out of it, into the entity at the other end of the link.
	enum DataDirection : uint32_t { DATA_INTO_OWNER = 1, DATA_OUT_OF_OWNER = 2 };
	struct DataPin { uint32_t entity; uint32_t parameter; uint32_t direction; };
	static_assert(sizeof(DataPin) == 12, "DataPin layout");

	// Replaces a composite's scripting with the given PAK image (as CathodeLib writes it for the live link) and brings
	// its running instances in line.
	// root: the root composite of the level OpenCAGE has open (0: any) - refused if the game is running another level.
	// dataPins: the pins of the composite's data links, so that what a changed one feeds reads its value afresh.
	Result ApplyComposite(uint32_t root, uint32_t compositeGuid, const uint8_t* image, uint32_t imageSize, const uint32_t* relocations, uint32_t relocationCount,
		const std::vector<DataPin>& dataPins);

	// Calls a method on an entity: in the instance at the given path of composite-instance entity ids from the root, or in
	// every instance of the composite when the path is empty.
	Result CallMethod(uint32_t root, uint32_t compositeGuid, uint32_t entityGuid, uint32_t methodGuid, const std::vector<uint32_t>& path);

	// Summary of a composite's running instances, for checking an edit landed.
	Result Describe(uint32_t compositeGuid);

	// Root composite, template count and the like.
	Result Status();
}

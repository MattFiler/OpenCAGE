#pragma once

#include "DevTools.h"
#include <cstddef>
#include <cstdint>
#include <string>

/*
	Names for the animation system's name hashes: bones, rigs, trees, tree nodes, clips, tree parameters and contexts.

	The game keeps only a 32-bit hash of each of these names (FNV-1a over the name's characters): a tree, a node or a clip
	has no name in memory. The animation package (DATA\GLOBAL\ANIMATION.PAK) ships two tables of the names behind the
	hashes. The game loads the shorter one and keeps it in memory: it has the bones, the rigs, float slots and a few poses
	and events, but no tree, node, context or (nearly) clip names. The longer one has every name the shorter one has and
	all of those too, with the same hashes, and stays in the package.

	So the longer table is read from the package next to AI.exe, once, on a thread of its own, the first time a name is
	asked for (a few megabytes; a mod that changes the package changes the names with it). Until it is in, and for any
	hash it does not have, the game's own table is tried, and then the hash is shown as "#XXXXXXXX".
*/
namespace ANIM_NAMES
{
	// A hash's name ("" for 0). Render thread: names are kept once found (and found again once the full table is in).
	std::string Name(uint32_t hash);

	// The full table: whether it is in yet, how many names it has, and why it could not be read (nullptr while loading or
	// once in).
	bool FullTableReady();
	size_t FullTableCount();
	const char* FullTableProblem();
}

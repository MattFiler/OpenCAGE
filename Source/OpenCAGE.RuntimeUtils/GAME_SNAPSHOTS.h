#pragma once

#include "DevTools.h"

/*
	Snapshot slots (experimental): the debug menu's Game > Tools section, under the level checkpoints, that keeps copies of
	the checkpoint in memory - the state the game goes back to on a death or Reload last checkpoint - in slots of its own,
	and puts one back later and restarts the level from it, as after a death. So a scripted sequence can be tried again and
	again from an earlier checkpoint, without a save being read or written. Nothing here touches the disk.

	The checkpoint in memory is one block the game keeps for the whole session: a header (its level, its checkpoint, the
	mission, when it was captured, the death counts, a checksum) then the level's persistent variables and the saved state
	of the level's entities and of the game's managers (inventory, pickups, the camera, the NPC groups, alliances, difficulty,
	sound...). A checkpoint restart resets what is not saved and reads everything else back from that block, finding the
	checkpoint by the block's own record of it, so a block put back whole is restarted from as if it were the game's own.
	- Save copies the block (the part in use) into a slot: only a normal checkpoint of the running level, checked the way
	  the game checks it, copied twice over if the game was changing it meanwhile, its checksum checked with the game's own.
	- Restore copies a slot back over the block, has its checksum worked out again with the game's own function when it
	  was not up to date, and asks for the game's restart from the checkpoint in memory (as Reload last checkpoint does),
	  in the same step on the entity thread. The block it replaced is kept in a slot of its own ("before the last restore")
	  so it can be put back the same way.
	- Only within one load of one level: the slots are emptied when the level unloads or another loads, and a slot is only
	  put back into the level and load it was taken in.
	- Restore needs Don't write saves on: the game writes the checkpoint in memory over the player's current checkpoint
	  save when the player dies, and the next checkpoint saves as usual; with saves kept back, nothing older reaches the disk.
	- Refused while the game is saving or loading, a checkpoint save waits for the player, a restart is under way, the game
	  is not in gameplay or the pause menu is open. The debug menu's own pause is let go, as the reload does.
	Outside the block, and not put back: what the game keeps on the player's profile (missions reached, statistics,
	achievements) and the checkpoint the level was last at (the Game flow tab may still name the later one).

	Named for OpenCAGE and testing: commands "game.snapshot_info", "game.snapshot_save", "game.snapshot_restore",
	"game.snapshot_clear".
*/
namespace GAME_SNAPSHOTS
{
	// The section (render thread): drawn on the Tools tab after its level checkpoints.
	void DrawSlots();
}

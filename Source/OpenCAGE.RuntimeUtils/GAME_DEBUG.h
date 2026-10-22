#pragma once

#include "DevTools.h"

/*
	The debug menu's Game group, beyond the saves (GAME_SAVES.h) and the achievements: the game's own flow and time.

	- Game flow tab: what the game's flow is doing (its state, the level, a level waiting to load, a restart asked for,
	  the level's script transport and the pause bits), the checkpoint in memory that a death or a reload goes back to
	  (its level, mission, when it was captured), the checkpoint the level is at, a save waiting for the player to be in a
	  state to save, the player's spawn point; the difficulty, shown and changed live (as the pause menu changes it); and
	  every level the game knows, each with a Load button (as OpenCAGE's load level does it).
	- Tools tab, under the game speed: pause and single-frame step - the game's own pause bit for a connected editor,
	  which stops the game clock, the AI, physics and the level's scripts
	  without a menu, and its single-frame step bit, which lets exactly one frame through. The game speed slider beside
	  them is the "game.speed" value.
	- Tools tab, after the debug checkpoints: every checkpoint of the level (its Checkpoint entities, not just the debug
	  ones), each with a Jump button - the same jump as for debug checkpoints, at the entity
	  manager's next tick, made by the checkpoint's place in the list (so one with no name, or a name shared with an
	  earlier one, can be jumped to) - and Reload last checkpoint: the game's own restart from the checkpoint in memory (as
	  a death and the scripts' debug load-checkpoint entity do it).
	- A level loads by the name the game knows it by ("Production\DLC\ChallengeMap1"), by that name without its
	  "Production\" ("DLC\ChallengeMap1") or by its last part alone ("ChallengeMap1").

	Named for OpenCAGE and testing: switches "game.paused", "game.pause_while_open", "game.keep_lowest_difficulty";
	commands "game.status", "game.step", "game.list_checkpoints", "game.jump_checkpoint", "game.reload_checkpoint",
	"game.list_debug_checkpoints", "game.jump_debug_checkpoint", "game.list_levels", "game.load_level", "game.difficulty".
*/
namespace GAME_DEBUG
{
	// The Tools tab's pause and step, drawn under its game speed slider (render thread).
	void DrawTimeControls();

	// The Tools tab's level checkpoints and reload, drawn after its debug checkpoints (render thread).
	void DrawLevelCheckpoints();

	// After a list row's button: the row's number, right-aligned in a column as wide as the list's largest number, then the
	// row's text goes on the same line - so the texts line up past 9 (render thread).
	void ListNumber(unsigned number, unsigned largest);

	// Whether the game is held by the debug menu's pause now.
	bool Paused();
}

#pragma once

#include "DevTools.h"

/*
	Debug actions: the debug menu's Tools tab - tools for the level's checkpoints, the game speed, the camera, the AI and
	the player, driven from the window:

	- Debug checkpoints: the level's debug checkpoints (script entities placed in the level to start it part-way
	  through, which add themselves to the entity manager's list when they are activated), each with a Jump button:
	  on the entity thread, unless a save is being written, it hands the entity manager the checkpoint to load, and the
	  entity manager loads it at its next tick. The buttons wait while a load is pending. After them, every checkpoint of
	  the level and Reload last checkpoint (GAME_DEBUG.h).
	- Game speed: slow motion and fast forward through the game clock's own time scale (the one its slow-motion moments
	  use): each frame's game time is the real time that passed times the scale ("game.speed", 0.05 to 4). It works
	  alongside Cap FPS, which only paces the frames in real time and never touches the scale. Pause and single-frame step
	  sit above it (GAME_DEBUG.h).
	- Free camera: turns the Cinematic group's free camera (CINEMATIC_CAMERA.h) on and off, and follows it.
	- AI actions on the selected character, or the alien when no character (or the player) is selected: alert the alien to
	  the player (with the scripted fake-sense message), withdraw it or cancel a withdrawal (as the level scripts' withdraw
	  entity does), force its menace aggressive or passive, reset an NPC or its senses and memory, and its logic flags as
	  checkboxes that read the character's own flags. Everything that changes a character runs on the AI thread, through
	  the game's own functions, with the character found again by its unique id there.
	- Player cheats: invisible to the AI (a sense that would pick up the player is not triggered) and infinite ammo (a
	  player's weapon uses no ammo when fired).

	The on/off options have dotted names ("tools.free_camera", "tools.invisible", "tools.infinite_ammo",
	"tools.flag.prevent_all_attacks", ...) so that OpenCAGE can set them over the Live Link (see DEBUG_MENU.h); they are
	compared with what was applied every frame.
*/
namespace DEBUG_ACTIONS
{
	// Hooked: the AI's handling of something one of a character's senses picks up (the sense's activation is raised from
	// it). Invisible returns before anything happens when what is sensed is the player.
	DEVTOOLS_DECLARE_CLASS_HOOK(bool, trigger_sense, h_trigger_sense, t_trigger_sense, 0x00329120, const void*, float, float, int, int, int, bool, bool, float)

	// Hooked: a weapon using up ammo from its clip as it fires. Infinite ammo returns at once for a player's weapon.
	DEVTOOLS_DECLARE_CLASS_HOOK(void, consume_ammo, h_consume_ammo, t_consume_ammo, 0x003A06F0, int)
}

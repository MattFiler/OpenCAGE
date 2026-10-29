#pragma once

#include "DevTools.h"
#include "LIVE_LINK.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <cstdint>
#include <string>

/*
	Possess Characters: one of the level's characters - the alien, an android, a human - is moved by the player instead of
	by its own mind, with the camera following it from behind.

	Each character the game runs has a behaviour tree that decides what it does, and below that a layer that carries out
	orders: go to a place, turn to face something, move in a direction, crouch or stand. The game has an order for moving
	in a direction (its scripted "move towards" command and one of its debug behaviours give it), and the character
	carries it out with its own animations, speeds and navigation mesh - so the alien moves as the alien moves. While a
	character is possessed its behaviour tree is not run (its senses, memory, aiming and the rest still are), and once a
	frame, where the tree would have run, it is given a move-in-direction order from the keys: W/A/S/D relative to the
	camera, Shift to go faster, Ctrl to go slower. Letting go of the keys cuts the order short, so the character comes to
	a stop. C crouches or stands it up again, with the game's own order for that (as its scripted idle command gives
	it); it moves again once it has. Let go of, its order is taken back and its behaviour tree runs again.

	The key (PossessKey, F6 by default) opens a menu over the game listing the characters the game runs (the one nearest
	the middle of the view first, then the nearest): one is taken by clicking it, or with the arrow keys and Enter, and
	the menu also lets go of the one possessed. The key again, or Escape, closes it. While it is open the mouse cursor is
	shown and freed, and the keyboard and mouse are kept from the game.

	While a character is possessed:
	- the game camera is put behind it, turned with the mouse and brought closer or taken further away (1.5 to 10 m) with
	  its wheel (unless OpenCAGE's viewport camera is driving the game's);
	- the zones around the camera are streamed in, as the game only streams those around the player;
	- the keyboard and mouse are kept from the game, bar Escape, so the player stands still.

	OpenCAGE (the Live Link's POSSESS) can take one by name, give it a stick to move with in place of the keys, crouch it
	and open the menu. Going to another level, or the character going, lets go of it.
*/
namespace POSSESS
{
	// Hooked: a character's behaviour update, once a frame for each character the game runs: its senses, memory, aiming
	// and mood, then its behaviour tree.
	DEVTOOLS_DECLARE_CLASS_HOOK(void, behaviour_update, h_behaviour_update, t_behaviour_update, 0x003342A0, float, float)

	// Hooked: the running of a behaviour tree, from the behaviour update.
	DEVTOOLS_DECLARE_CLASS_HOOK(void, tree_update, h_tree_update, t_tree_update, 0x0032B880, bool)

	// Socket thread (the Live Link's POSSESS): take the character whose name has this in it (empty: the one nearest the
	// middle of the view), or let go. Stored: the next frame acts on it.
	LIVE_LINK::Result Take(uint32_t root, const std::string& name);
	LIVE_LINK::Result Release();

	// Socket thread: a stick from the given connection, used in place of the keys until it runs out (0 ms: until another
	// comes) or that connection goes. x is right, y forward, relative to the camera, each -1..1; speed 0 (slowest) to 3.
	// A stick of 0, 0 stops the character; a negative speed gives the keys back.
	LIVE_LINK::Result SetStick(uint32_t connection, float x, float y, int speed, uint32_t milliseconds);

	// Socket thread: crouch the possessed character, or stand it up (as C does).
	LIVE_LINK::Result SetCrouch(bool crouched);

	// Socket thread: open or close the menu (as the key does).
	LIVE_LINK::Result SetMenu(bool open);

	// Socket thread (the Live Link's POSSESS_GET): the state as of the last frame, and the level's characters.
	LIVE_LINK::Result GetState(uint32_t root);

	// The game window's messages, before the game has them (its window thread): the key, the menu's cursor and input,
	// and the keyboard and mouse kept from the game while a character is possessed - true when the message is kept, with
	// what to answer it.
	bool OnWindowMessage(HWND window, UINT message, WPARAM wParam, LPARAM lParam, LRESULT& result);

	// Camera update task: where the game camera goes this frame, while a character is possessed (game world space). False
	// leaves the camera to the game.
	bool CameraPose(float position[3], float forward[3], float up[3]);

	// Camera update task: where the frame is drawn from (whoever placed the camera), for the keys to move relative to and
	// the menu to put the character nearest the middle of the view first - and whether it was the camera behind the character.
	void SeenFrom(const float position[3], const float forward[3], bool behindCharacter);

	// Whether a character is possessed now.
	bool Possessing();

	// The overlay (render thread): the menu while it is open, and what is possessed with its keys.
	void DrawOverlay();
}

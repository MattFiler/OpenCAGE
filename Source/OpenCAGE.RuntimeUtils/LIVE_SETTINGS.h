#pragma once

#include "LIVE_LINK.h"
#include <cstdint>
#include <string>
#include <vector>

/*
	Live settings: OpenCAGE's config editors change the game's rendering and stealth settings in the running game as they
	are edited (the live link's SETTINGS), rather than at the next level load.

	The game reads each of these settings files whenever a level opens, into numbers of its own that it hands the shaders
	(or the gameplay that uses them) afresh every frame and keeps no other copy of - so writing one of those numbers shows
	on the next frame, with nothing else to redo:
	- DATA\RADIOSITY_SETTINGS.TXT: six numbers for the radiosity (the bounce light), the first of which also scales how
	  brightly emissive surfaces glow. The bounce ones work inside the radiosity's feedback, so the picture settles over a
	  number of frames rather than snapping. Its last two numbers are read but never used by the game.
	- DATA\SKIN_SHADING_SETTINGS.TXT: the two numbers for how light scatters under skin.
	- DATA\HAIR_SHADING_SETTINGS.TXT: the twenty numbers for hair shading the game reads (four more keys the file can hold
	  are never read by the game).
	- DATA\UI\SELECTIONOVERLAYPARAMS.BIN: the colour, pulse rate and power of the glow round things the player can use.
	- DATA\GLOBALCONSTANTS.BML: the stealth light meter's thresholds and timeout, how near the player must be to use
	  something, the time between a squad's shots, and how long reactions to a suspicious item loop.

	A value written lasts until the next level load, when the game reads the file again (by then OpenCAGE's editor has
	saved the same value to it).
*/
namespace LIVE_SETTINGS
{
	// The settings files (the live link's numbering)
	enum File : uint8_t
	{
		RADIOSITY = 1,
		SKIN = 2,
		HAIR = 3,
		SELECTION_OVERLAY = 4,
		GLOBAL_CONSTANTS = 5,
	};

	struct Value
	{
		std::string key; // as the file spells it
		float value;
	};

	// Socket thread: writes the values into the running game. The message has a line per key, in the order given:
	// "key=value live" (written; the game uses it from the next frame), "key=value inert" (the game reads the key from
	// the file but never uses it), "key unknown" (not one of the file's keys), "key refused" (not a finite number) or
	// "key unavailable" (this build of the game has no address for it). ok when every key was live or inert.
	LIVE_LINK::Result Set(uint8_t file, const std::vector<Value>& values);

	// Socket thread: every key of the file the game holds a number for, as "key=value" lines.
	LIVE_LINK::Result Get(uint8_t file);
}

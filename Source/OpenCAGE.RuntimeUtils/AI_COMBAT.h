#pragma once

#include "DevTools.h"
#include <cstdint>

/*
	Damage and death switches for testing combat and scripted deaths, in the debug menu's Characters group:

	- Control tab, "Invulnerable" section: the selected NPC takes no damage (the game's own debug bit in its "takes no
	  damage" flags, as god mode sets it for the player), kept on every frame until turned off, the level changes or the
	  character is gone. Several NPCs can have it at once; the section lists them.
	- Control tab, "Damage to every NPC" section: one choice for the damage every non-player character takes - as the game
	  deals it, every hit kills, every hit leaves 1 health, every hit does at most 1 damage, or nobody dies (a hit that
	  would kill leaves 1 health). Applied as the game applies a hit to a character's health, after the difficulty and
	  stun scaling, where the game's own debug switches for the same did it. The alien still never goes below 1 health.
	- The same section: the game no longer ends when the player hurts or kills an ally (the level's "player killed an
	  ally" monitor isn't told), for tests that kill characters.

	The Player tab's Stench of evil (PLAYER_STENCH.cpp) reads the two below: it leaves out the characters made invulnerable
	here, and holds off while the damage choice keeps a hit from killing outright.

	Named for OpenCAGE and testing: switches "ai.one_shot_kill", "ai.one_shot_maim", "ai.max_damage_1", "ai.nobody_dies"
	(one at a time: turning one on turns the others off) and "ai.no_ally_kill_fail"; commands "ai.damage_mode",
	"ai.ally_kill_reports", "ai.invulnerable", "ai.list_invulnerable", "ai.clear_invulnerable" and "ai.hit_from_player"
	(damages an NPC as a hit from the player, for testing these without aiming a weapon).

	A level change or the level ending turns them all off.
*/
namespace AI_COMBAT
{
	// Any thread: whether this character (by unique id) was made invulnerable in the Control tab
	bool Invulnerable(uint32_t id);

	// Any thread: whether the damage choice keeps a hit from killing an NPC outright just now (one-shot maim, at most 1
	// damage, nobody dies)
	bool NpcsKeptAlive();

	// Any thread, guarded: the running level's root composite id (0: no level)
	uint32_t LevelRoot();

	// Render thread, once a frame per watcher: true once when the level has changed, or has been gone for two seconds (a
	// short gap, as some reloads may make, doesn't count)
	struct LevelWatch
	{
		uint32_t root = 0;
		unsigned long long goneSince = 0;
		bool Changed();
	};
}

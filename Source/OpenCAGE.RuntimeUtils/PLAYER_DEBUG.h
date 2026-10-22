#pragma once

#include "DevTools.h"
#include <cstdint>
#include <string>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

/*
	The player's views in the debug menu, in its Characters group: sections of the Player tab (cheats, teleport, hiding,
	what is under the player's feet, objectives and the motion tracker's route; drawn round the tab's light and stealth
	section), the Inventory tab (the current weapon, the inventory, giving items, blueprints, gating tools, keycards and
	infinite items) and the Input tab (what the game thinks is pressed). This is what they share: finding the player's
	character, inventory and the game's item list, guarded reads and writes, and the lines saying what the last action in
	each section came to.

	Threads: the tabs read the game on the render thread. What changes a character (health, the cheats' flags) runs on the
	AI thread; what changes the inventory or shows a popup runs on the entity thread, where the level's own scripts give
	items and set objectives; and a teleport is done in the physics' pass over the characters at the end of each frame,
	where the game itself puts back a character that fell out of the world.

	Every switch, value and command is named under "player." so OpenCAGE can drive it over the Live Link (see
	DEBUG_MENU.h); "player.status", "player.list_inventory" and the other list commands report what the tabs show.
*/
namespace PLAYER_DEBUG
{
	// ---- Reads of the game (any thread; every read guarded, so a level unloading meanwhile reads as nothing) ----

	// The game's globals (null before they exist)
	uint8_t* Globals();
	bool LevelRunning();

	// The player's character (null: no level, or no player yet)
	uint8_t* Player();
	// A character's unique id (0: none)
	uint32_t CharacterId(const uint8_t* character);
	// Its class (as DEBUG_MENU::ClassName names them; -1: unreadable)
	int CharacterClass(const uint8_t* character);
	// The characters the game runs, the players first, then the living others (up to max); how many
	int Characters(uint8_t** out, int max);
	// The character of this unique id among those (null: gone)
	uint8_t* FindCharacter(uint32_t id);
	// One of a character's logic flags (the byte per flag the level scripts set)
	bool LogicFlag(const uint8_t* character, int flag);

	// The player's inventory and the game's item templates (null: none)
	uint8_t* Inventory();
	uint8_t* ItemDatabase();

	// The game clock: the game time now, in seconds (0 when unreadable)
	double GameTime();

	template <typename T>
	bool Read(const void* at, T& value)
	{
		if (!at)
			return false;
		__try
		{
			value = *reinterpret_cast<const volatile T*>(at);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	template <typename T>
	bool Write(void* at, T value)
	{
		if (!at)
			return false;
		__try
		{
			*reinterpret_cast<volatile T*>(at) = value;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// A pointer field of an object (null when unreadable)
	uint8_t* PointerAt(const void* object, uint32_t offset);
	// A string of the game's copied out, at most max characters (empty when unreadable): one in place, or one a field
	// points to
	std::string Text(const void* text, size_t max = 128);
	std::string TextAt(const void* object, uint32_t offset, size_t max = 128);
	// The game's text for a text key, in the language it runs in (empty: no such key, or not available on this release).
	// A plain lookup in the game's loaded text, safe on any thread
	std::string Translated(const std::string& key);

	// ---- For the tabs ----

	// The sections an action can be done from: each keeps what its own last action came to, shown under it
	enum class StatusArea { Health, Teleport, Objectives, Weapon, Inventory, Give, Collected, Tools, Count };

	// What the last action from a section came to (any thread), shown under that section and logged
	void SetStatus(StatusArea area, const std::string& text);
	std::string Status(StatusArea area);

	// Render thread: a tooltip for the last item; a checkbox for any view's named switch (greyed out while nobody has
	// named it); a section's status line; greyed-out text (printf style) that wraps at the window's edge instead of running
	// off it
	void Tooltip(const char* text);
	void SwitchBox(const char* label, const char* name, const char* tooltip);
	void DrawStatus(StatusArea area);
	void WrappedDisabled(const char* format, ...);

	// Numbers out of a command's text ("1.5, 2, -3" or "1.5 2 -3"): true when count of them were read
	bool ParseFloats(const std::string& text, float* out, int count);
	std::string Vector(const float value[3]);

	// ---- Hooks (attached with the debug menu's own, see DEBUG_MENU::AddHooks) ----

	// Hooked: the player's inventory using up the item in the player's hand (as it is thrown, placed, used or, for some,
	// equipped). Infinite items puts one more of it in the inventory first (PLAYER_INVENTORY.cpp).
	DEVTOOLS_DECLARE_CLASS_HOOK(void, consume_held_item, h_consume_held_item, t_consume_held_item, 0x0038EB50)

	// Hooked: the physics' pass over the characters at the end of each frame, which also puts back a character that fell
	// out of the world. A teleport asked for is done after it, the same way (PLAYER_CHEATS.cpp).
	typedef void(__cdecl* t_post_process_characters)();
	inline auto post_process_characters = reinterpret_cast<t_post_process_characters>(DEVTOOLS_RELATIVE_ADDRESS(0x004031B0));
	void __cdecl h_post_process_characters();
}

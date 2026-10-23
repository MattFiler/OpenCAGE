#include "PLAYER_DEBUG.h"
#include "DEBUG_MENU.h"

#include <atomic>
#include <cstdio>
#include <memory>
#include <string>

#include <detours.h>
#include <imgui.h>

/*
	Addresses and layouts are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).

	Keeping the player alive against the kills that aren't damage: "The alien can't grab the player" ("player.no_grabs", a
	checkbox among the Player tab's cheats), and god mode ("player.god_mode", PLAYER_CHEATS.cpp), which does the same and
	also keeps facehuggers off the player.

	God mode sets the game's own debug bit in the player's "won't take damage" flags, and every damage call, the game's
	forced kill among them, does nothing while any of those flags is set. But the alien's grab (from in front, from behind
	or from the side, out in the open or in a vent) clears the victim's whole set of those flags the moment it starts and
	then calls the forced kill, in the same call: the kill animation that follows plays on a player who is already dead. A
	facehugger's leap does the same. God mode's bit, put back every AI frame, comes back too late. So these kills are kept
	from starting instead:

	- The alien's attacks. Every AI frame the alien works out which kinds of attack it could make on its target from where
	  it is; its behaviour only attacks when it has one, and its attack gives up at the start without one. That frame is
	  hooked: while either switch is on and the alien's target is the player, it is left with none, as the game itself
	  leaves it when it can't get at the player. The alien still sees, hears, hunts and chases the player and closes in on
	  them (and may go into the vents to come at them from another side); it never grabs them. The player stays its target.
	- The vent ambushes. An alien waiting in a vent drops on the player passing close by, and one coming out of a vent next
	  to the player kills them as it comes out. Both start in one place, which is hooked: while either switch is on and the
	  victim is the player, nothing starts. (These two don't clear the flags, so god mode alone already kept the player
	  alive, but not out of the kill animation.) The waiting alien stays in its vent; the one coming out stays in the vent
	  while the player is right by its way out, and comes out once they aren't.
	- Facehuggers. A facehugger asks whether it may attack its target before it leaps: the check a level script's "prevent
	  all attacks" flag on the player answers no to. The check is hooked: with god mode on, a facehugger whose target is
	  the player is told no, and waits beside the player.

	All three act as the game asks, so they cover every alien and facehugger, those that appear after a switch is set too.
	Nothing is kept in the game: the alien's attacks are worked out again every frame and the other two are only asked as
	they happen, so with the switches off everything is as before at once. The switches are read on the render thread once
	a frame into the flags the hooks read (on the AI thread).

	Not covered: deaths a level's own scripts cause - a script ordering the alien to attack the player (that order doesn't
	ask which attacks the alien has), or the script command that kills a character. Whether a hiding spot's "the alien has
	found you" sequence is one of those is up to the level's scripts.
*/
namespace
{
	// The alien's attack search, run every AI frame: its owner (the alien's behaviour, a pointer) and whether it has each
	// kind of attack on its target now (seven bytes, the last one "any")
	constexpr uintptr_t kAttackSearch = 0x00310D10;
	constexpr uint32_t kSearchOwner = 0x04;
	constexpr uint32_t kSearchHasAttack = 0x08;
	constexpr uint32_t kAttackKinds = 7;
	// The behaviour's targeting (a pointer), and the character's own pointer to it (the behaviour is part of the character)
	constexpr uint32_t kBehaviourTargeting = 0xC8;
	constexpr uint32_t kCharacterTargeting = 0x34A8;
	// The targeting's current target (a pointer to the record it keeps of it), and the record's character (a pointer)
	constexpr uint32_t kTargetingCurrent = 0x10;
	constexpr uint32_t kRecordCharacter = 0x42C;

	// Where a vent ambush on a character starts (the alien's ambushes, the vent, the victim)
	constexpr uintptr_t kAmbushStart = 0x0000B8F0;

	// The check a facehugger makes before it leaps: whether it may attack its target (1 yes, 0 no)
	constexpr uintptr_t kMayAttack = 0x002F5770;
	constexpr int kAlienClass = 1;
	constexpr int kFacehuggerClass = 5;

	typedef void(__thiscall* t_attack_search)(void* search);
	typedef void(__thiscall* t_ambush_start)(void* ambushes, const void* vent, void* victim);
	typedef int(__thiscall* t_may_attack)(const void* check, void* character, void* node);
	t_attack_search g_attackSearch = nullptr;
	t_ambush_start g_ambushStart = nullptr;
	t_may_attack g_mayAttack = nullptr;
	std::atomic<bool> g_searchHooked = false;
	std::atomic<bool> g_ambushHooked = false;
	std::atomic<bool> g_mayAttackHooked = false;

	// The switch, and what the hooks act on (set on the render thread every frame): the alien (either switch) and
	// facehuggers (god mode)
	std::atomic<bool> g_noGrabs = false;
	std::atomic<bool> g_guardAlien = false;
	std::atomic<bool> g_guardFacehuggers = false;

	// Since the game started: AI frames an alien had an attack on the player taken away, ambushes on the player not
	// started, and facehuggers told no; when the last of each was (GetTickCount64)
	std::atomic<uint32_t> g_attacksTaken = 0;
	std::atomic<uint32_t> g_ambushesStopped = 0;
	std::atomic<uint32_t> g_facehuggersStopped = 0;
	std::atomic<ULONGLONG> g_lastAttackTaken = 0;
	std::atomic<ULONGLONG> g_lastAmbushStopped = 0;
	std::atomic<ULONGLONG> g_lastFacehuggerStopped = 0;

	// ---- The hooks (AI thread) ----

	// Guarded: the character a targeting is aimed at now (null: none)
	const uint8_t* TargetOf(const uint8_t* targeting)
	{
		if (!targeting)
			return nullptr;
		__try
		{
			const uint8_t* record = *reinterpret_cast<uint8_t* const*>(targeting + kTargetingCurrent);
			return record ? *reinterpret_cast<uint8_t* const*>(record + kRecordCharacter) : nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	// Guarded: the targeting of the alien an attack search is for (null: none)
	const uint8_t* SearchTargeting(const uint8_t* search)
	{
		__try
		{
			const uint8_t* behaviour = *reinterpret_cast<uint8_t* const*>(search + kSearchOwner);
			return behaviour ? *reinterpret_cast<uint8_t* const*>(behaviour + kBehaviourTargeting) : nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	// Guarded: leaves an attack search with no attacks, as the game does when the alien can't get at its target; whether
	// it had any
	bool TakeAttacks(uint8_t* search, bool& had)
	{
		__try
		{
			had = false;
			for (uint32_t i = 0; i < kAttackKinds; i++)
			{
				had = had || search[kSearchHasAttack + i] != 0;
				search[kSearchHasAttack + i] = 0;
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// Hooked: the alien's attack search. It runs first; then, while guarded and the alien's target is the player, the
	// alien is left with no attack.
	void __fastcall AttackSearch(void* search, void* /*edx*/)
	{
		g_attackSearch(search);
		if (!g_guardAlien || !search)
			return;
		const uint8_t* player = PLAYER_DEBUG::Player();
		bool had = false;
		if (player && TargetOf(SearchTargeting(static_cast<const uint8_t*>(search))) == player && TakeAttacks(static_cast<uint8_t*>(search), had) && had)
		{
			g_attacksTaken++;
			g_lastAttackTaken = GetTickCount64();
		}
	}

	// Hooked: a vent ambush starting. On the player while guarded, it doesn't.
	void __fastcall AmbushStart(void* ambushes, void* /*edx*/, const void* vent, void* victim)
	{
		if (g_guardAlien && victim && victim == PLAYER_DEBUG::Player())
		{
			// A waiting alien tries again every frame while the player stays close: one log line for each stretch of tries
			const ULONGLONG now = GetTickCount64();
			if (now - g_lastAmbushStopped > 2000)
				DevTools::Log("Debug menu: kept the alien from springing a vent ambush on the player");
			g_ambushesStopped++;
			g_lastAmbushStopped = now;
			return;
		}
		g_ambushStart(ambushes, vent, victim);
	}

	// Hooked: whether a character may attack its target. A facehugger whose target is the player is told no while god
	// mode is on; everything else gets the game's own answer.
	int __fastcall MayAttack(void* check, void* /*edx*/, void* character, void* node)
	{
		if (g_guardFacehuggers && character && PLAYER_DEBUG::CharacterClass(static_cast<const uint8_t*>(character)) == kFacehuggerClass)
		{
			const uint8_t* player = PLAYER_DEBUG::Player();
			if (player && TargetOf(PLAYER_DEBUG::PointerAt(character, kCharacterTargeting)) == player)
			{
				g_facehuggersStopped++;
				g_lastFacehuggerStopped = GetTickCount64();
				return 0;
			}
		}
		return g_mayAttack(check, character, node);
	}

	void AttachHooks(bool attach)
	{
		if (attach)
		{
			g_attackSearch = reinterpret_cast<t_attack_search>(DEVTOOLS_RELATIVE_ADDRESS(kAttackSearch));
			g_ambushStart = reinterpret_cast<t_ambush_start>(DEVTOOLS_RELATIVE_ADDRESS(kAmbushStart));
			g_mayAttack = reinterpret_cast<t_may_attack>(DEVTOOLS_RELATIVE_ADDRESS(kMayAttack));
			if (g_attackSearch)
			{
				DetourAttach(&reinterpret_cast<PVOID&>(g_attackSearch), reinterpret_cast<PVOID>(&AttackSearch));
				g_searchHooked = true;
			}
			if (g_ambushStart)
			{
				DetourAttach(&reinterpret_cast<PVOID&>(g_ambushStart), reinterpret_cast<PVOID>(&AmbushStart));
				g_ambushHooked = true;
			}
			if (g_mayAttack)
			{
				DetourAttach(&reinterpret_cast<PVOID&>(g_mayAttack), reinterpret_cast<PVOID>(&MayAttack));
				g_mayAttackHooked = true;
			}
			DevTools::Log("Debug menu: kill guard hooks: alien attacks %s, vent ambushes %s, facehuggers %s", g_searchHooked ? "attached" : "not attached",
				g_ambushHooked ? "attached" : "not attached", g_mayAttackHooked ? "attached" : "not attached");
		}
		else
		{
			g_noGrabs = false;
			g_guardAlien = false;
			g_guardFacehuggers = false;
			if (g_searchHooked)
				DetourDetach(&reinterpret_cast<PVOID&>(g_attackSearch), reinterpret_cast<PVOID>(&AttackSearch));
			if (g_ambushHooked)
				DetourDetach(&reinterpret_cast<PVOID&>(g_ambushStart), reinterpret_cast<PVOID>(&AmbushStart));
			if (g_mayAttackHooked)
				DetourDetach(&reinterpret_cast<PVOID&>(g_mayAttack), reinterpret_cast<PVOID>(&MayAttack));
			g_searchHooked = false;
			g_ambushHooked = false;
			g_mayAttackHooked = false;
		}
	}

	// ---- Named switch and command ----

	bool g_registered = false;

	std::string Ago(ULONGLONG tick)
	{
		if (!tick)
			return "never";
		char text[32];
		snprintf(text, sizeof(text), "%.1f", (GetTickCount64() - tick) / 1000.0);
		return text;
	}

	// Render thread: the aliens and facehuggers there are, and which of them have the player as their target
	std::string Hunters()
	{
		const uint8_t* player = PLAYER_DEBUG::Player();
		uint8_t* characters[36];
		const int count = PLAYER_DEBUG::Characters(characters, 36);
		std::string text;
		for (int i = 0; i < count; i++)
		{
			const int characterClass = PLAYER_DEBUG::CharacterClass(characters[i]);
			if (characterClass != kAlienClass && characterClass != kFacehuggerClass)
				continue;
			const bool targetsPlayer = player && TargetOf(PLAYER_DEBUG::PointerAt(characters[i], kCharacterTargeting)) == player;
			char line[96];
			snprintf(line, sizeof(line), "%s %u targets_player=%d\n", characterClass == kAlienClass ? "alien" : "facehugger",
				PLAYER_DEBUG::CharacterId(characters[i]), targetsPlayer ? 1 : 0);
			text += line;
		}
		return text.empty() ? "no aliens or facehuggers\n" : text;
	}

	void Register()
	{
		if (g_registered)
			return;
		g_registered = true;
		// The switch only where its hook is in (the Player tab's checkbox is greyed out without it)
		if (g_searchHooked)
			DEBUG_MENU::AddSwitch("player.no_grabs", &g_noGrabs);
		DEBUG_MENU::AddCommand("player.kill_guard_status", "What keeps the player alive against the alien's grab, its vent ambushes and facehuggers: whether each guard is on and hooked, how often each turned one away since the game started and seconds since the last, then every alien and facehugger and whether the player is its target", [](const std::string&)
		{
			const std::shared_ptr<std::string> hunters = std::make_shared<std::string>();
			const DEBUG_MENU::CommandResult read = DEBUG_MENU::AnswerOnRenderThread([hunters]() -> DEBUG_MENU::CommandResult
			{
				*hunters = PLAYER_DEBUG::Player() ? Hunters() : "no player\n";
				return { true, "" };
			});
			char text[640];
			snprintf(text, sizeof(text), "alien_guard=%d\nfacehugger_guard=%d\nno_grabs=%d\nattack_hook=%d\nambush_hook=%d\nfacehugger_hook=%d\n"
				"attacks_taken=%u\nlast_attack_taken_s_ago=%s\nambushes_stopped=%u\nlast_ambush_stopped_s_ago=%s\nfacehuggers_stopped=%u\n"
				"last_facehugger_stopped_s_ago=%s\n",
				g_guardAlien ? 1 : 0, g_guardFacehuggers ? 1 : 0, g_noGrabs ? 1 : 0, g_searchHooked ? 1 : 0, g_ambushHooked ? 1 : 0,
				g_mayAttackHooked ? 1 : 0, g_attacksTaken.load(), Ago(g_lastAttackTaken).c_str(), g_ambushesStopped.load(),
				Ago(g_lastAmbushStopped).c_str(), g_facehuggersStopped.load(), Ago(g_lastFacehuggerStopped).c_str());
			return DEBUG_MENU::CommandResult{ true, std::string(text) + (read.ok ? *hunters : read.message + "\n") };
		});
	}

	// Render thread, every frame: names the switch, and hands the hooks what to guard (god mode is PLAYER_CHEATS.cpp's)
	void Apply(ImDrawList* /*drawList*/)
	{
		Register();
		bool god = false;
		DEBUG_MENU::GetSwitch("player.god_mode", god);
		g_guardAlien = god || g_noGrabs;
		g_guardFacehuggers = god;
	}

	const bool g_overlayAdded = DEBUG_MENU::AddOverlay(7, Apply);
	const bool g_hooksAdded = DEBUG_MENU::AddHooks(AttachHooks);
}

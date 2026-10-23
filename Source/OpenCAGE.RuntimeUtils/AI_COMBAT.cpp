#include "AI_COMBAT.h"
#include "AI_STATE.h"
#include "DEBUG_MENU.h"
#include "PLAYER_DEBUG.h"

#include <algorithm>
#include <atomic>
#include <cctype>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <memory>
#include <mutex>
#include <sstream>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <detours.h>
#include <imgui.h>

/*
	The Control tab's Invulnerable and Damage to every NPC sections (see AI_COMBAT.h).

	Addresses and layouts are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).

	- Invulnerable: the debug bit in the character's "takes no damage" flags (a bit for each part of the game that wants
	  it; with any bit set the game's damage call does nothing), put back every frame on the AI thread, as the game clears
	  the whole set on some deaths and when a level script turns its own invincibility off. A hit that still gets through
	  in a frame where the game had cleared it does no damage (the hook below).
	- The damage choice: the game's damage call works out the damage (the difficulty's scaling, an android's extra damage
	  while stunned by an EMP, a hit that mustn't kill held at 1 health) and then hands it to the step that takes it off
	  the character's health - that step's only caller. The step is hooked, and the damage it is handed is changed for a
	  character that isn't a player before it runs. The step itself keeps the alien at 1 health or more, whatever the
	  damage. Androids marked as showroom dummies (a logic flag the level's scripts set; the game never lets them die)
	  are left as the game deals with them.
	- The ally report: the game tells the level's "player killed an ally" monitor (which the levels use to end the game)
	  through one call, made when the player damages a character that isn't an enemy and when such a character dies.
	  That call is hooked and returns at once while the switch is on. Each report is counted, let through or not.
*/
namespace
{
	using namespace PLAYER_DEBUG;

	// A character's fields: its unique id, its debug name (64 characters), what it was spawned as (a pointer: its class at
	// +4, and set at +0x1C for a player's character), its "takes no damage" flags (a bit per owner; the debug bit is the
	// debug menu's own), its player data (a pointer: set at +0x48 while a player controls it), its logic flags (a byte
	// each), and its health, the most health it can have and what that is scaled by
	constexpr uint32_t kCharacterId = 0x50;
	constexpr uint32_t kCharacterName = 0x10;
	constexpr uint32_t kCharacterSpawnedAs = 0x64;
	constexpr uint32_t kSpawnedAsClass = 0x04;
	constexpr uint32_t kSpawnedAsPlayer = 0x1C;
	constexpr uint32_t kWontTakeDamage = 0x2E8;
	constexpr uint8_t kDebugBit = 0x20;
	constexpr uint32_t kCharacterPlayable = 0x31C;
	constexpr uint32_t kPlayableControlled = 0x48;
	constexpr uint32_t kCharacterFlags = 0x423C;
	constexpr int kFlagShowroomDummy = 0x17;
	constexpr int kAlienClass = 1;
	constexpr uint32_t kHealth = 0x33C;
	constexpr uint32_t kMaxHealth = 0x33E;
	constexpr uint32_t kMaxHealthModifier = 0x340;

	// A character's health state (what the damage step works on): its character (a pointer) and its health now
	constexpr uint32_t kHealthOwner = 0x24;
	constexpr uint32_t kHealthNow = 0x1C;

	// The entity manager (a pointer to it): the running level's root composite id
	constexpr uintptr_t kEntityManager = 0x0134EF40;
	constexpr uint32_t kEntityRoot = 0x0C;

	constexpr int kMaxInvulnerable = 36;
	constexpr size_t kReportsKept = 8;

	// The step of the game's damage call that takes the damage off a character's health (on its health state): the damage
	// (16 bits), the attacker, then the call's own switches and the kind of damage, a stack slot each
	typedef void(__thiscall* t_apply_damage)(void* health, uint32_t damage, const void* attacker, uint32_t a, uint32_t b, uint32_t c, uint32_t kind, uint32_t d);
	t_apply_damage apply_damage = reinterpret_cast<t_apply_damage>(DEVTOOLS_RELATIVE_ADDRESS(0x003C44F0));
	void __fastcall h_apply_damage(void* health, void* edx, uint32_t damage, const void* attacker, uint32_t a, uint32_t b, uint32_t c, uint32_t kind, uint32_t d);

	// The report to the level's "player killed an ally" monitor (on the NPC group manager; the character hurt or killed)
	typedef void(__thiscall* t_report_ally)(void* groups, const void* character);
	t_report_ally report_ally = reinterpret_cast<t_report_ally>(DEVTOOLS_RELATIVE_ADDRESS(0x002CAC00));
	void __fastcall h_report_ally(void* groups, void* edx, const void* character);

	// The game's damage call on a character (as the Control tab's Damage makes it, the kind of damage 0x16), here with the
	// player as the attacker: for testing the two above without aiming a weapon
	typedef void(__thiscall* t_take_damage)(void* character, int amount, const void* attacker, bool a, bool b, bool c, bool d, uint32_t kind, bool e, bool f);
	const auto take_damage = reinterpret_cast<t_take_damage>(DEVTOOLS_RELATIVE_ADDRESS(0x003CC900));
	constexpr uint32_t kDamageKind = 0x16;

	std::atomic<bool> g_damageHooked = false;
	std::atomic<bool> g_allyHooked = false;

	// ---- What the section did last ----

	std::mutex g_statusMutex;
	std::string g_status;

	void SetStatus(const std::string& text)
	{
		{
			std::lock_guard<std::mutex> lock(g_statusMutex);
			g_status = text;
		}
		DevTools::Log("Debug menu: %s", text.c_str());
	}

	std::string Status()
	{
		std::lock_guard<std::mutex> lock(g_statusMutex);
		return g_status;
	}

	// ---- The damage choice ----

	enum Mode { Normal, Kill, Maim, MaxOne, NobodyDies, kModes };
	std::atomic<bool> g_kill = false;
	std::atomic<bool> g_maim = false;
	std::atomic<bool> g_maxOne = false;
	std::atomic<bool> g_nobodyDies = false;
	std::atomic<bool>* const kModeSwitches[kModes] = { nullptr, &g_kill, &g_maim, &g_maxOne, &g_nobodyDies };
	const char* const kModeKeys[kModes] = { "normal", "kill", "maim", "max1", "nobody_dies" };
	const char* const kModeLabels[kModes] = { "As the game deals it", "One-shot kill", "One-shot maim", "At most 1 damage", "Nobody dies" };
	const char* const kModeTooltips[kModes] = {
		"NPCs take damage as the game deals it.",
		"Any damage to an NPC kills it (its whole health as the damage). The alien still stays at 1 health or more: the game never lets it die.",
		"Any damage to an NPC leaves it at 1 health (a hit at 1 health does nothing), for injured animations and behaviour. The alien is kept at 1 health or more anyway.",
		"Any damage to an NPC is 1 at most: a fight lasts, and hit reactions can be watched hit after hit.",
		"A hit that would kill an NPC leaves it at 1 health (at 1 health, it does nothing): keeps story NPCs alive during tests. The game's own forced kill goes through the same damage call, so the level's scripted deaths and the Kill button above are held at 1 health too." };

	// The choice in force: the first switch on (the render thread keeps only one on; the hook may see two for a frame)
	int CurrentMode()
	{
		for (int mode = Kill; mode < kModes; mode++)
			if (*kModeSwitches[mode])
				return mode;
		return Normal;
	}

	void SetMode(int mode)
	{
		for (int other = Kill; other < kModes; other++)
			if (other != mode)
				*kModeSwitches[other] = false;
		if (mode > Normal && mode < kModes)
			*kModeSwitches[mode] = true;
	}

	// Render thread, every frame: a switch newly turned on (from the window or the Live Link) turns the others off
	bool g_modeWas[kModes] = {};

	void KeepOneMode()
	{
		int keep = Normal;
		for (int mode = Kill; mode < kModes && keep == Normal; mode++)
			if (*kModeSwitches[mode] && !g_modeWas[mode])
				keep = mode;
		if (keep == Normal)
			keep = CurrentMode();
		for (int mode = Kill; mode < kModes; mode++)
		{
			if (mode != keep && *kModeSwitches[mode])
				*kModeSwitches[mode] = false;
			g_modeWas[mode] = *kModeSwitches[mode];
		}
	}

	// The hits changed, and the last (the character's id, the damage the game worked out and what it became)
	std::atomic<uint32_t> g_hitsChanged = 0;
	std::atomic<uint32_t> g_lastHitId = 0;
	std::atomic<int> g_lastHitFrom = 0;
	std::atomic<int> g_lastHitTo = 0;

	// The damage a hit does under a choice, from what the game worked out and the health the character has now
	int ModeDamage(int mode, int damage, int health)
	{
		switch (mode)
		{
		case Kill: return (std::min)(health, 0x7FFF);
		case Maim: return health > 1 ? health - 1 : 0;
		case MaxOne: return (std::min)(damage, 1);
		case NobodyDies: return damage >= health ? (health > 1 ? health - 1 : 0) : damage;
		default: return damage;
		}
	}

	// ---- Invulnerable NPCs ----

	// The characters made invulnerable (by unique id; 0: a free place), read on any thread without a lock; their names and
	// classes for the list, and every change, under the lock
	std::atomic<uint32_t> g_invulnerable[kMaxInvulnerable];
	std::atomic<bool> g_anyInvulnerable = false;
	std::mutex g_invulnerableMutex;
	std::string g_invulnerableNames[kMaxInvulnerable];
	int g_invulnerableClasses[kMaxInvulnerable] = {};

	// AI thread only: the characters the debug bit was set on (to take it off again)
	std::vector<uint32_t> g_bitSet;
	std::atomic<bool> g_anyBitSet = false;
	std::atomic<bool> g_tickQueued = false;

	struct Listed
	{
		uint32_t id = 0;
		std::string name;
		int characterClass = -1;
	};

	// Under the lock: whether any place is taken
	void RecountInvulnerable()
	{
		bool any = false;
		for (int i = 0; i < kMaxInvulnerable; i++)
			any = any || g_invulnerable[i] != 0;
		g_anyInvulnerable = any;
	}

	// Any thread: false when the list is full
	bool AddInvulnerable(uint32_t id, const std::string& name, int characterClass)
	{
		if (!id)
			return false;
		std::lock_guard<std::mutex> lock(g_invulnerableMutex);
		for (int i = 0; i < kMaxInvulnerable; i++)
			if (g_invulnerable[i] == id)
				return true;
		for (int i = 0; i < kMaxInvulnerable; i++)
			if (g_invulnerable[i] == 0)
			{
				g_invulnerableNames[i] = name;
				g_invulnerableClasses[i] = characterClass;
				g_invulnerable[i] = id;
				g_anyInvulnerable = true;
				return true;
			}
		return false;
	}

	// Any thread: false when it wasn't listed
	bool RemoveInvulnerable(uint32_t id)
	{
		std::lock_guard<std::mutex> lock(g_invulnerableMutex);
		bool found = false;
		for (int i = 0; i < kMaxInvulnerable; i++)
			if (id && g_invulnerable[i] == id)
			{
				g_invulnerable[i] = 0;
				g_invulnerableNames[i].clear();
				found = true;
			}
		RecountInvulnerable();
		return found;
	}

	int ClearInvulnerable()
	{
		std::lock_guard<std::mutex> lock(g_invulnerableMutex);
		int count = 0;
		for (int i = 0; i < kMaxInvulnerable; i++)
			if (g_invulnerable[i])
			{
				g_invulnerable[i] = 0;
				g_invulnerableNames[i].clear();
				count++;
			}
		g_anyInvulnerable = false;
		return count;
	}

	std::vector<Listed> ListInvulnerable()
	{
		std::lock_guard<std::mutex> lock(g_invulnerableMutex);
		std::vector<Listed> list;
		for (int i = 0; i < kMaxInvulnerable; i++)
			if (g_invulnerable[i])
				list.push_back({ g_invulnerable[i], g_invulnerableNames[i], g_invulnerableClasses[i] });
		return list;
	}

	// AI thread: sets or clears the debug bit, guarded
	void SetDebugBit(uint8_t* character, bool on)
	{
		uint8_t flags = 0;
		if (!Read(character + kWontTakeDamage, flags))
			return;
		if (on && !(flags & kDebugBit))
			Write<uint8_t>(character + kWontTakeDamage, flags | kDebugBit);
		else if (!on && (flags & kDebugBit))
			Write<uint8_t>(character + kWontTakeDamage, flags & ~kDebugBit);
	}

	// AI thread, every frame while any is listed or has the bit: the bit on each listed character still here (one gone
	// from the level comes off the list), and off those taken off the list
	void Tick()
	{
		uint32_t wanted[kMaxInvulnerable];
		int count = 0;
		for (int i = 0; i < kMaxInvulnerable; i++)
			if (const uint32_t id = g_invulnerable[i])
				wanted[count++] = id;
		// Only while the characters can be read (a character not found then is gone for good: ids aren't given out twice)
		const bool readable = Player() != nullptr;
		for (int i = 0; i < count; i++)
		{
			uint8_t* character = AI_STATE::FindCharacter(wanted[i]);
			if (!character)
			{
				if (readable && RemoveInvulnerable(wanted[i]))
					SetStatus("invulnerable: the character with unique id " + std::to_string(wanted[i]) + " is gone, so it was taken off the list");
				continue;
			}
			SetDebugBit(character, true);
			if (std::find(g_bitSet.begin(), g_bitSet.end(), wanted[i]) == g_bitSet.end())
				g_bitSet.push_back(wanted[i]);
		}
		for (auto it = g_bitSet.begin(); it != g_bitSet.end();)
		{
			if (std::find(wanted, wanted + count, *it) != wanted + count)
			{
				++it;
				continue;
			}
			if (uint8_t* character = AI_STATE::FindCharacter(*it))
				SetDebugBit(character, false);
			it = g_bitSet.erase(it);
		}
		g_anyBitSet = !g_bitSet.empty();
		g_tickQueued = false;
	}

	// ---- The ally reports ----

	struct AllyReport
	{
		uint32_t id = 0;
		std::string name;
		bool blocked = false;
	};

	std::atomic<bool> g_noAllyFail = false;
	std::atomic<uint32_t> g_reportsBlocked = 0;
	std::atomic<uint32_t> g_reportsPassed = 0;
	std::mutex g_reportMutex;
	std::vector<AllyReport> g_reports; // the latest last, at most kReportsKept

	// The character's id and debug name, guarded (plain data out)
	bool ReadCharacterName(const void* character, uint32_t& id, char* name, size_t size)
	{
		name[0] = '\0';
		id = 0;
		if (!character)
			return false;
		__try
		{
			const uint8_t* at = static_cast<const uint8_t*>(character);
			id = *reinterpret_cast<const uint32_t*>(at + kCharacterId);
			size_t i = 0;
			for (; i + 1 < size && i < 64 && at[kCharacterName + i]; i++)
				name[i] = static_cast<char>(at[kCharacterName + i]);
			name[i] = '\0';
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			name[0] = '\0';
			return false;
		}
	}

	void NoteReport(const void* character, bool blocked)
	{
		AllyReport report;
		char name[72];
		ReadCharacterName(character, report.id, name, sizeof(name));
		report.name = name[0] ? name : "?";
		report.blocked = blocked;
		(blocked ? g_reportsBlocked : g_reportsPassed)++;
		std::lock_guard<std::mutex> lock(g_reportMutex);
		g_reports.push_back(report);
		if (g_reports.size() > kReportsKept)
			g_reports.erase(g_reports.begin());
	}

	std::vector<AllyReport> Reports()
	{
		std::lock_guard<std::mutex> lock(g_reportMutex);
		return g_reports;
	}

	// ---- The hook on the damage step ----

	struct Victim
	{
		uint32_t id = 0;
		int health = 0;
		bool player = false;
		bool dummy = false;
	};

	// The character a health state belongs to, guarded (plain data out)
	bool ReadVictim(const void* health, Victim& out)
	{
		__try
		{
			const uint8_t* state = static_cast<const uint8_t*>(health);
			const uint8_t* character = *reinterpret_cast<uint8_t* const*>(state + kHealthOwner);
			if (!character)
				return false;
			const uint8_t* spawnedAs = *reinterpret_cast<uint8_t* const*>(character + kCharacterSpawnedAs);
			const uint8_t* playable = *reinterpret_cast<uint8_t* const*>(character + kCharacterPlayable);
			out.player = (spawnedAs && spawnedAs[kSpawnedAsPlayer]) || (playable && playable[kPlayableControlled]);
			out.dummy = character[kCharacterFlags + kFlagShowroomDummy] != 0;
			out.id = *reinterpret_cast<const uint32_t*>(character + kCharacterId);
			out.health = *reinterpret_cast<const uint16_t*>(state + kHealthNow);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// Any thread the game deals damage on: the damage changed for an NPC by the choice in force (none for one made
	// invulnerable here), then the game's own step
	void __fastcall h_apply_damage(void* health, void* /*edx*/, uint32_t damage, const void* attacker, uint32_t a, uint32_t b, uint32_t c, uint32_t kind, uint32_t d)
	{
		const int mode = CurrentMode();
		const int given = static_cast<int16_t>(damage & 0xFFFF);
		Victim victim;
		if ((mode != Normal || g_anyInvulnerable) && given > 0 && ReadVictim(health, victim) && !victim.player)
		{
			int wanted = given;
			if (AI_COMBAT::Invulnerable(victim.id))
				wanted = 0;
			else if (mode != Normal && !victim.dummy && victim.health > 0)
				wanted = ModeDamage(mode, given, victim.health);
			if (wanted != given)
			{
				damage = (damage & 0xFFFF0000u) | static_cast<uint16_t>(wanted);
				g_hitsChanged++;
				g_lastHitId = victim.id;
				g_lastHitFrom = given;
				g_lastHitTo = wanted;
			}
		}
		apply_damage(health, damage, attacker, a, b, c, kind, d);
	}

	// The game's threads: the report noted, and kept from the level while the switch is on
	void __fastcall h_report_ally(void* groups, void* /*edx*/, const void* character)
	{
		const bool block = g_noAllyFail;
		NoteReport(character, block);
		if (!block)
			report_ally(groups, character);
	}

	void AttachHooks(bool attach)
	{
		if (apply_damage)
		{
			if (attach)
				DEVTOOLS_DETOURS_ATTACH(apply_damage, h_apply_damage);
			else
				DEVTOOLS_DETOURS_DETACH(apply_damage, h_apply_damage);
			g_damageHooked = attach;
		}
		if (report_ally)
		{
			if (attach)
				DEVTOOLS_DETOURS_ATTACH(report_ally, h_report_ally);
			else
				DEVTOOLS_DETOURS_DETACH(report_ally, h_report_ally);
			g_allyHooked = attach;
		}
	}

	const bool g_hooks = DEBUG_MENU::AddHooks(AttachHooks);

	// ---- Shared helpers ----

	DEBUG_MENU::CommandResult Fail(const std::string& message)
	{
		DEBUG_MENU::CommandResult result;
		result.ok = false;
		result.message = message;
		return result;
	}

	DEBUG_MENU::CommandResult Done(const std::string& message)
	{
		DEBUG_MENU::CommandResult result;
		result.message = message;
		return result;
	}

	// Render thread: a name for a character this frame by its id (empty: not seen this frame)
	std::string FrameName(uint32_t id, bool* dead = nullptr)
	{
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
			if (character.id == id)
			{
				if (dead)
					*dead = character.dead;
				return character.name;
			}
		return std::string();
	}

	// AI thread: the game's damage call with the player as the attacker, guarded
	bool CallTakeDamage(uint8_t* character, const uint8_t* player, int amount)
	{
		__try
		{
			take_damage(character, static_cast<int16_t>(amount), player, false, false, true, false, kDamageKind, false, false);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// AI thread: a hit from the player on an NPC, and what came of it (the health, the damage choice, the ally report)
	DEBUG_MENU::CommandResult HitFromPlayer(uint8_t* character, const uint8_t* player, int amount)
	{
		uint16_t health = 0, most = 0;
		float modifier = 1.0f;
		if (!Read(character + kHealth, health) || health == 0)
			return Fail("it is dead");
		Read(character + kMaxHealth, most);
		Read(character + kMaxHealthModifier, modifier);
		if (amount <= 0)
			amount = (std::max)(1, static_cast<int>(most * (modifier > 0.0f ? modifier : 1.0f)) / 10);
		amount = (std::min)(amount, 0x7FFF);
		const uint32_t changedBefore = g_hitsChanged, blockedBefore = g_reportsBlocked, passedBefore = g_reportsPassed;
		if (!CallTakeDamage(character, player, amount))
			return Fail("the damage call failed");
		uint16_t after = 0;
		Read(character + kHealth, after);
		char text[256];
		snprintf(text, sizeof(text), "hit from the player: %d damage, health %u -> %u", amount, health, after);
		std::string message = text;
		if (g_hitsChanged != changedBefore)
			message += "\nthe damage choice made it " + std::to_string(g_lastHitTo.load());
		if (g_reportsBlocked != blockedBefore)
			message += "\nthe level's \"player killed an ally\" monitor wasn't told (ai.no_ally_kill_fail)";
		if (g_reportsPassed != passedBefore)
			message += "\nthe level's \"player killed an ally\" monitor was told";
		SetStatus(message);
		return Done(message);
	}

	std::string ModeLine()
	{
		const int mode = CurrentMode();
		char text[200];
		snprintf(text, sizeof(text), "mode=%s\nhits_changed=%u\nlast_hit=id %u, %d -> %d", kModeKeys[mode], g_hitsChanged.load(), g_lastHitId.load(),
			g_lastHitFrom.load(), g_lastHitTo.load());
		return text;
	}

	std::string ReportLines()
	{
		std::ostringstream out;
		out << "no_ally_kill_fail=" << (g_noAllyFail ? 1 : 0) << "\nblocked=" << g_reportsBlocked << "\nlet_through=" << g_reportsPassed << "\n";
		for (const AllyReport& report : Reports())
			out << "report=" << report.name << " (" << report.id << ")" << (report.blocked ? " blocked" : " let through") << "\n";
		return out.str();
	}

	// The level watch's work: everything off, and the invulnerable list emptied (the AI thread takes the bits off those
	// still found)
	AI_COMBAT::LevelWatch g_watch;

	void TurnAllOff(const char* why)
	{
		const bool anything = CurrentMode() != Normal || g_noAllyFail || g_anyInvulnerable;
		SetMode(Normal);
		g_noAllyFail = false;
		ClearInvulnerable();
		if (anything)
			SetStatus(std::string("NPC damage switches and invulnerable NPCs off: ") + why);
	}

	// ---- Named switches and commands ----

	struct Found
	{
		bool found = false;
		uint32_t id = 0;
		std::string name;
		int characterClass = -1;
		bool player = false, dead = false;
	};

	// The character a command acts on, read on the render thread: the one named by "id=<n>", else the selected one
	bool FindTarget(const std::string& argument, Found& out, std::string& words, std::string& error)
	{
		uint32_t id = 0;
		std::istringstream stream(argument);
		std::string word;
		while (stream >> word)
		{
			for (char& c : word)
				c = static_cast<char>(tolower(static_cast<unsigned char>(c)));
			if (word.compare(0, 3, "id=") == 0)
				id = static_cast<uint32_t>(strtoul(word.c_str() + 3, nullptr, 0));
			else
				words += (words.empty() ? "" : " ") + word;
		}
		const std::shared_ptr<Found> shared = std::make_shared<Found>();
		const DEBUG_MENU::CommandResult answer = DEBUG_MENU::AnswerOnRenderThread([id, shared]() -> DEBUG_MENU::CommandResult
		{
			const uint32_t wanted = id ? id : DEBUG_MENU::SelectedId();
			for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
				if (character.id == wanted && wanted)
				{
					shared->found = true;
					shared->id = character.id;
					shared->name = character.name;
					shared->characterClass = character.characterClass;
					shared->player = character.player;
					shared->dead = character.dead;
				}
			DEBUG_MENU::CommandResult result;
			result.ok = shared->found;
			return result;
		});
		if (!answer.ok)
		{
			error = !answer.message.empty() ? answer.message : (id ? "no character with that unique id" : "no character selected (select one, or give id=<unique id>)");
			return false;
		}
		out = *shared;
		return true;
	}

	bool g_registered = false;

	void Register()
	{
		if (g_registered)
			return;
		g_registered = true;
		DEBUG_MENU::AddSwitch("ai.one_shot_kill", &g_kill);
		DEBUG_MENU::AddSwitch("ai.one_shot_maim", &g_maim);
		DEBUG_MENU::AddSwitch("ai.max_damage_1", &g_maxOne);
		DEBUG_MENU::AddSwitch("ai.nobody_dies", &g_nobodyDies);
		DEBUG_MENU::AddSwitch("ai.no_ally_kill_fail", &g_noAllyFail);

		DEBUG_MENU::AddCommand("ai.damage_mode", "[normal|kill|maim|max1|nobody_dies] - the damage every NPC takes (no argument: which, and the hits it changed)", [](const std::string& argument)
		{
			std::string key = argument;
			key.erase(0, key.find_first_not_of(" \t"));
			key.erase(key.find_last_not_of(" \t\r\n") + 1);
			for (char& c : key)
				c = static_cast<char>(tolower(static_cast<unsigned char>(c)));
			if (!apply_damage)
				return Fail("not available on this game release");
			if (key.empty())
				return Done(ModeLine());
			for (int mode = Normal; mode < kModes; mode++)
				if (key == kModeKeys[mode])
				{
					SetMode(mode);
					SetStatus(std::string("damage to every NPC: ") + kModeLabels[mode]);
					return Done(ModeLine() + (g_damageHooked ? "" : "\n(the debug menu's hooks aren't attached: no effect)"));
				}
			return Fail("give normal, kill, maim, max1 or nobody_dies");
		});
		DEBUG_MENU::AddCommand("ai.ally_kill_reports", "the reports to the level's \"player killed an ally\" monitor: blocked, let through, the latest", [](const std::string&)
		{
			if (!report_ally)
				return Fail("not available on this game release");
			return Done(ReportLines());
		});
		DEBUG_MENU::AddCommand("ai.invulnerable", "on|off [id=<unique id>] - the selected NPC takes no damage (kept every frame until off)", [](const std::string& argument)
		{
			Found target;
			std::string words, error;
			if (!FindTarget(argument, target, words, error))
				return Fail(error);
			const bool on = words.empty() || words == "on" || words == "1";
			if (!on && words != "off" && words != "0")
				return Fail("give on or off");
			if (target.player)
				return Fail("not for the player: use player.god_mode");
			const std::string who = target.name + " (" + std::to_string(target.id) + ")";
			if (!on)
				return RemoveInvulnerable(target.id) ? Done("invulnerable off - " + who) : Done("not invulnerable - " + who);
			if (target.dead)
				return Fail("it is dead - " + who);
			if (!AddInvulnerable(target.id, target.name, target.characterClass))
				return Fail("the list is full (" + std::to_string(kMaxInvulnerable) + ")");
			SetStatus("invulnerable on - " + who);
			return Done("invulnerable on - " + who + " (from the next AI frame)");
		});
		DEBUG_MENU::AddCommand("ai.list_invulnerable", "the NPCs made invulnerable: unique id, name, class", [](const std::string&)
		{
			std::ostringstream out;
			const std::vector<Listed> list = ListInvulnerable();
			for (const Listed& entry : list)
				out << entry.id << " " << entry.name << " (" << DEBUG_MENU::ClassName(entry.characterClass) << ")\n";
			return Done(list.empty() ? "none" : out.str());
		});
		DEBUG_MENU::AddCommand("ai.clear_invulnerable", "makes every NPC made invulnerable here take damage again", [](const std::string&)
		{
			const int count = ClearInvulnerable();
			SetStatus("invulnerable off for all (" + std::to_string(count) + ")");
			return Done("invulnerable off for " + std::to_string(count));
		});
		DEBUG_MENU::AddCommand("ai.hit_from_player", "[amount] [id=<unique id>] - damages an NPC as a hit from the player (a tenth of its most health when no amount): "
			"for testing the damage choice and the ally switch. A kill counts as the player's", [](const std::string& argument)
		{
			if (!take_damage)
				return Fail("not available on this game release");
			Found target;
			std::string words, error;
			if (!FindTarget(argument, target, words, error))
				return Fail(error);
			if (target.player)
				return Fail("not for the player");
			if (target.dead)
				return Fail("it is dead - " + target.name);
			const int amount = words.empty() ? 0 : atoi(words.c_str());
			if (!words.empty() && amount <= 0)
				return Fail("give a damage amount above 0, or none");
			const uint32_t id = target.id;
			const uint32_t playerId = CharacterId(Player());
			return DEBUG_MENU::AnswerOnAiThread([id, playerId, amount]() -> DEBUG_MENU::CommandResult
			{
				uint8_t* character = AI_STATE::FindCharacter(id);
				uint8_t* player = Player();
				if (!character)
					return Fail("it is gone");
				if (!player || CharacterId(player) != playerId)
					return Fail("no player, or the player has changed since this was asked");
				return HitFromPlayer(character, player, amount);
			});
		});
	}

	const bool g_named = (Register(), true);

	// Render thread, every frame: the level watch, one damage choice at a time, and the AI thread's work for the
	// invulnerable NPCs while any is listed or has the bit
	void Apply(ImDrawList* /*drawList*/)
	{
		Register();
		if (g_watch.Changed())
			TurnAllOff("the level changed");
		KeepOneMode();
		const bool wanted = g_anyInvulnerable || g_anyBitSet;
		if (!wanted || g_tickQueued)
			return;
		g_tickQueued = true;
		DEBUG_MENU::OnAiThread(Tick);
	}

	// ---- The section ----

	void DrawInvulnerable()
	{
		ImGui::SeparatorText("Invulnerable");
		const DEBUG_MENU::Character* selected = DEBUG_MENU::Selected();
		if (!selected)
			ImGui::TextDisabled("Select an NPC to make it invulnerable.");
		else if (selected->player)
			ImGui::TextDisabled("The player's own is God mode, in Characters > Player.");
		else
		{
			bool on = AI_COMBAT::Invulnerable(selected->id);
			ImGui::BeginDisabled(selected->dead && !on);
			if (ImGui::Checkbox("Invulnerable", &on))
			{
				const std::string who = selected->name + " (" + std::to_string(selected->id) + ")";
				if (!on)
				{
					RemoveInvulnerable(selected->id);
					SetStatus("invulnerable off - " + who);
				}
				else if (AddInvulnerable(selected->id, selected->name, selected->characterClass))
					SetStatus("invulnerable on - " + who);
				else
					SetStatus("invulnerable: the list is full");
			}
			ImGui::EndDisabled();
			Tooltip("This NPC takes no damage: the game's own debug bit in its \"takes no damage\" flags, as God mode sets it for the player, put back every frame "
				"(the game clears the flags on some deaths and when a level script turns its own invincibility off). It still reacts to hits. Stench of evil and the "
				"damage choice below leave it alone. The game's forced kill (scripted deaths, the Kill button above) doesn't kill it either. Off again when "
				"unticked, when the character leaves the level, or when the level changes.");
		}
		const std::vector<Listed> list = ListInvulnerable();
		if (list.empty())
			return;
		ImGui::TextDisabled("Invulnerable now:");
		for (const Listed& entry : list)
		{
			ImGui::PushID(static_cast<int>(entry.id));
			if (ImGui::SmallButton("Off"))
			{
				RemoveInvulnerable(entry.id);
				SetStatus("invulnerable off - " + entry.name + " (" + std::to_string(entry.id) + ")");
			}
			Tooltip("It takes damage again.");
			ImGui::SameLine();
			bool dead = false;
			const std::string seen = FrameName(entry.id, &dead);
			ImGui::Text("%s (%s) - id %u%s", entry.name.c_str(), DEBUG_MENU::ClassName(entry.characterClass), entry.id,
				seen.empty() ? " - not running this frame" : (dead ? " - dead" : ""));
			ImGui::PopID();
		}
	}

	void DrawDamage()
	{
		ImGui::SeparatorText("Damage to every NPC");
		if (!apply_damage)
			ImGui::TextDisabled("Not available on this game release.");
		else
		{
			// Two lines: as the game deals it and the two one-shots, then the two that hold the damage down
			int mode = CurrentMode();
			for (int choice = Normal; choice < kModes; choice++)
			{
				if (choice != Normal && choice != MaxOne)
					ImGui::SameLine();
				if (ImGui::RadioButton(kModeLabels[choice], mode == choice))
				{
					mode = choice;
					SetMode(choice);
					SetStatus(std::string("damage to every NPC: ") + kModeLabels[choice]);
				}
				Tooltip(kModeTooltips[choice]);
			}
			if (!g_damageHooked)
				ImGui::TextDisabled("The debug menu's hooks aren't attached: no effect.");
			if (const uint32_t changed = g_hitsChanged)
			{
				const std::string name = FrameName(g_lastHitId);
				WrappedDisabled("Hits changed: %u. Last: %s, %d damage made %d.", changed, name.empty() ? ("id " + std::to_string(g_lastHitId.load())).c_str() : name.c_str(),
					g_lastHitFrom.load(), g_lastHitTo.load());
			}
			WrappedDisabled("Only characters that aren't players. Showroom dummy androids are left as the game deals with them. A level change sets it back to "
				"as the game deals it.");
		}

		ImGui::BeginDisabled(!report_ally);
		bool noFail = g_noAllyFail;
		if (ImGui::Checkbox("Don't end the game when an ally is hurt or killed", &noFail))
		{
			g_noAllyFail = noFail;
			SetStatus(noFail ? "the level's \"player killed an ally\" monitor isn't told any more" : "the level's \"player killed an ally\" monitor is told again");
		}
		ImGui::EndDisabled();
		Tooltip(report_ally ? "The game tells the level's \"player killed an ally\" monitor (which ends the game in the levels that have one) when the player damages "
			"a character that isn't an enemy, and when one dies. While this is on, it isn't told. For tests that kill characters (Kill, Stench of evil, "
			"one-shot kill...). Off again when the level changes." : "Not available on this game release.");
		const uint32_t blocked = g_reportsBlocked, passed = g_reportsPassed;
		if (blocked || passed)
		{
			const std::vector<AllyReport> reports = Reports();
			const AllyReport last = reports.empty() ? AllyReport() : reports.back();
			WrappedDisabled("Reports: %u kept from the level, %u let through. Last: %s (%s).", blocked, passed, last.name.c_str(), last.blocked ? "kept" : "let through");
		}
	}

	void DrawSection()
	{
		Register();
		DrawInvulnerable();
		DrawDamage();
		const std::string status = Status();
		if (!status.empty())
			WrappedDisabled("Last: %s", status.c_str());
	}

	const bool g_added = DEBUG_MENU::AddTab("Control", 15, DrawSection, "Characters") && DEBUG_MENU::AddOverlay(13, Apply);
}

bool AI_COMBAT::Invulnerable(uint32_t id)
{
	if (!id || !g_anyInvulnerable)
		return false;
	for (int i = 0; i < kMaxInvulnerable; i++)
		if (g_invulnerable[i] == id)
			return true;
	return false;
}

bool AI_COMBAT::NpcsKeptAlive()
{
	const int mode = CurrentMode();
	return g_damageHooked && (mode == Maim || mode == MaxOne || mode == NobodyDies);
}

uint32_t AI_COMBAT::LevelRoot()
{
	if (!LevelRunning())
		return 0;
	const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kEntityManager);
	const uint8_t* manager = at ? PointerAt(reinterpret_cast<const void*>(at), 0) : nullptr;
	uint32_t root = 0;
	return manager && Read(manager + kEntityRoot, root) ? root : 0;
}

bool AI_COMBAT::LevelWatch::Changed()
{
	const uint32_t now = LevelRoot();
	if (now)
	{
		const bool changed = root && now != root;
		root = now;
		goneSince = 0;
		return changed;
	}
	if (!root)
		return false;
	const ULONGLONG tick = GetTickCount64();
	if (!goneSince)
	{
		goneSince = tick;
		return false;
	}
	if (tick - goneSince < 2000)
		return false;
	root = 0;
	goneSince = 0;
	return true;
}

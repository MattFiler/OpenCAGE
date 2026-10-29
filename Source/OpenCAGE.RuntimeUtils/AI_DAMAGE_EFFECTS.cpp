#include "AI_STATE.h"
#include "DEBUG_MENU.h"

#include <algorithm>
#include <atomic>
#include <climits>
#include <cmath>
#include <cstddef>
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

#include <imgui.h>

/*
	Damage effects, a section of the Characters group's Control tab: stun, EMP, blind, acid, gas or fire on the selected
	NPC, or on every character within a radius of the player, to see how each kind of character takes them (an android's
	EMP stun, a human's stunned, gassed and blinded reactions...) without throwing anything.

	Each is the game's own explosion damage message, sent from the player as a thrown grenade's is. The message names an
	ammo type and one range of that ammo's damage table (the range_damage lists of the ammo files, nearest first), and the
	effects of that range (Damage_1..3) decide what the character's damage handling does - the same reactions and the same
	rules as for a real grenade. What each does depends on who takes it: a human is stunned, gassed or blinded and only
	flinches at fire or acid; an android takes EMP, blind and fire (it is set alight) but not stun or gas; the player's
	allies shrug off stun, gas, blind and EMP; the alien's stun gauge fills from a range that stuns it. By default the
	message carries no damage, so it is the effect alone; with ai.damage_effect_with_damage on it carries the damage that
	range does to that kind of character (the vs_NPC, vsAndroid... columns), which can kill.

	The ammo for each effect is looked up in the running game's ammo table, so edited ammo files are what is tested: the
	usual one first (the stun grenade's nearest range for stun and its middle range for blind, the large EMP burst's outer
	range, the small acid burst, the smoke grenade, the fire grenade's middle range), else whichever range carries the
	effect with the fewest other effects. The command also takes any ammo type and range by name or number.

	Sent on the AI thread (where the game's own gas clouds send theirs), each character found again by its unique id
	there. Nothing is left changed: each effect runs its course as the game's would (a stun wears off...); only damage,
	when asked for, stays.

	Layouts and addresses are the Steam retail AI.exe's (see GameAddresses.h for the GOG and Epic Games Store builds).
*/
namespace
{
	// ---- The game ----

	// Where the game keeps its pointer to its globals; the byte after that pointer is set while its characters can be read.
	// The first field of the globals is the damage dispatcher: its explosion handler is at +0x30.
	constexpr uintptr_t kGameGlobals = 0x012F0C88;
	constexpr uint32_t kCharactersValid = 0x04;
	constexpr uint32_t kGlobalsDamage = 0x00;
	constexpr uint32_t kExplosionHandler = 0x30;

	// The game's ammo table (a pointer to it): a pointer to its entries (a pointer each, by ammo type, null until loaded)
	// and how many there are. An entry holds up to eight ranges of 0x30 bytes from +0x4C, nearest first, and how many it
	// has (16 bits) at +0x1CC.
	constexpr uintptr_t kAmmoTable = 0x012F182C;
	constexpr uint32_t kAmmoEntries = 0x20C;
	constexpr uint32_t kAmmoCount = 0x210;
	constexpr uint32_t kAmmoRanges = 0x4C;
	constexpr uint32_t kAmmoRangeCount = 0x1CC;
	constexpr uint32_t kRangeSize = 0x30;
	constexpr int kMaxRanges = 8;
	constexpr int kMaxAmmo = 64;
	// A range: how far it reaches (a float), the damage it does to each kind of character (16 bits each: the player, an NPC,
	// the alien, an android, a heavy android, a facehugger), how much it adds to the alien's stun gauge, how long it stuns,
	// EMPs and blinds (seconds), how hard a ragdoll it gives, and its effects (bits)
	constexpr uint32_t kRangeDistance = 0x00;
	constexpr uint32_t kRangeDamagePlayer = 0x08;
	constexpr uint32_t kRangeDamageNpc = 0x0A;
	constexpr uint32_t kRangeDamageAlien = 0x0C;
	constexpr uint32_t kRangeDamageAndroid = 0x0E;
	constexpr uint32_t kRangeDamageHeavy = 0x10;
	constexpr uint32_t kRangeDamageFacehugger = 0x12;
	constexpr uint32_t kRangeAlienStun = 0x18;
	constexpr uint32_t kRangeStunTime = 0x1C;
	constexpr uint32_t kRangeEmpTime = 0x20;
	constexpr uint32_t kRangeBlindTime = 0x24;
	constexpr uint32_t kRangeRagdoll = 0x28;
	constexpr uint32_t kRangeEffects = 0x2C;

	// The effects a range can carry (as the ammo files name them)
	constexpr uint32_t kIncendiary = 0x10000;
	constexpr uint32_t kStun = 0x20000;
	constexpr uint32_t kBlind = 0x40000;
	constexpr uint32_t kEmp = 0x80000;
	constexpr uint32_t kAcid = 0x100000;
	constexpr uint32_t kGas = 0x800000;
	constexpr uint32_t kImpact = 0x2000000;
	constexpr uint32_t kEffectBits = kIncendiary | kStun | kBlind | kEmp | kAcid | kGas | kImpact;

	// A character's fields: its script entity's handle (its entity, then the composite instance it is in), what it was
	// spawned as (a pointer: its class at +4) and its health state (4: dead)
	constexpr uint32_t kCharacterHandle = 0x54;
	constexpr uint32_t kCharacterSpawnedAs = 0x64;
	constexpr uint32_t kSpawnedAsClass = 0x04;
	constexpr uint32_t kCharacterHealthState = 0x32C;
	constexpr int kHealthDead = 4;
	constexpr int kClassAlien = 1, kClassAndroid = 2, kClassFacehugger = 5, kClassHeavyAndroid = 7;

	// The explosion damage message, as the game's constructor lays it out and a thrown grenade fills it: the attacker's and
	// the victim's entity handles, the weapon's (none here), the ammo type and the range of its table, whether a level
	// script's take-damage sent it, where it went off, the point hit, the damage and the bone hit (-1: none). The game copies
	// 0x3C bytes of it.
	struct ExplosionMessage
	{
		uint32_t attacker[2];
		uint32_t victim[2];
		uint32_t weapon[2];
		uint16_t ammo;
		int8_t range;
		uint8_t padding1;
		uint8_t fromScript;
		uint8_t padding2[3];
		float position[3];
		float hitPosition[3];
		int16_t damage;
		int16_t bone;
	};
	static_assert(sizeof(ExplosionMessage) == 0x3C, "the game's message is 0x3C bytes");
	static_assert(offsetof(ExplosionMessage, ammo) == 0x18 && offsetof(ExplosionMessage, fromScript) == 0x1C, "the ammo handle at +0x18");
	static_assert(offsetof(ExplosionMessage, position) == 0x20 && offsetof(ExplosionMessage, damage) == 0x38, "position +0x20, damage +0x38");

	// The game's sending of an explosion damage message to its victim (the handler, the message, false as the game passes)
	typedef void(__thiscall* t_send_explosion)(void* handler, const void* message, bool reflect);
	const auto send_explosion = reinterpret_cast<t_send_explosion>(DEVTOOLS_RELATIVE_ADDRESS(0x00094BD0));

	// The ammo types, by number (as the ammo files and the scripts name them)
	const char* const kAmmoNames[] = {
		"PISTOL_NORMAL", "PISTOL_DUM_DUM", "SMG_NORMAL", "SMG_DUM_DUM", "SHOTGUN_NORMAL", "SHOTGUN_SLUG", "FLAMETHROWER_NORMAL",
		"FLAMETHROWER_AERATED", "FLAMETHROWER_HIGH_DAMAGE", "SHOTGUN_INCENDIARY", "PISTOL_NORMAL_NPC", "SHOTGUN_NORMAL_NPC", "GRENADE_HE",
		"GRENADE_FIRE", "CATALYST_HE_SMALL", "CATALYST_HE_LARGE", "CATALYST_FIRE_SMALL", "CATALYST_FIRE_LARGE", "ACID_BURST_SMALL",
		"ACID_BURST_LARGE", "EMP_BURST_SMALL", "EMP_BURST_LARGE", "IMPACT", "GRENADE_STUN", "GRENADE_SMOKE", "PISTOL_TAZER",
		"MELEE_CROW_AXE", "BOLTGUN_NORMAL", "PUSH", "CATTLEPROD_POWERPACK", "EMP_BURST_LARGE_TIER2", "EMP_BURST_LARGE_TIER3",
		"GRENADE_FIRE_TIER2", "GRENADE_FIRE_TIER3", "GRENADE_HE_TIER2", "GRENADE_HE_TIER3", "GRENADE_STUN_TIER2", "GRENADE_STUN_TIER3",
		"ENVIRONMENT_FLAME",
	};
	constexpr int kAmmoNameCount = static_cast<int>(sizeof(kAmmoNames) / sizeof(kAmmoNames[0]));

	std::string AmmoName(int ammo)
	{
		if (ammo >= 0 && ammo < kAmmoNameCount)
			return kAmmoNames[ammo];
		return "ammo " + std::to_string(ammo);
	}

	// ---- The effects ----

	struct Effect
	{
		const char* key;
		const char* label;
		uint32_t flag;
		int ammo, range;          // the usual ammo and range for it
		const char* what;
	};

	const Effect kEffects[] = {
		{ "stun", "Stun", kStun, 23, 0, "A stun grenade's nearest range: stunned (and blinded with it) for its stun time. Androids don't take stun; the alien's stun gauge fills." },
		{ "emp", "EMP", kEmp, 21, 1, "An EMP burst: androids are stunned by it (an android stunned by EMP takes more damage meanwhile). Humans don't take EMP." },
		{ "blind", "Blind", kBlind, 23, 1, "A stun grenade's middle range: blinded, without the stun." },
		{ "acid", "Acid", kAcid, 18, 0, "The small acid burst: a flinch, and its damage when that is on." },
		{ "gas", "Gas", kGas, 24, 0, "The smoke grenade: humans are gassed. Androids don't take gas." },
		{ "fire", "Fire", kIncendiary, 13, 1, "The fire grenade's middle range: an android is set alight; a human only flinches unless the damage is on (the "
			"Control tab's Set on fire burns anyone)." },
	};

	const Effect* FindEffect(const std::string& key)
	{
		for (const Effect& effect : kEffects)
			if (key == effect.key)
				return &effect;
		return nullptr;
	}

	// ---- Reading the ammo table (any thread; every read guarded: the table is loaded with each level) ----

	struct RangeInfo
	{
		float distance;
		int16_t damage[6];        // the player, an NPC, the alien, an android, a heavy android, a facehugger
		float alienStun, stunTime, empTime, blindTime, ragdoll;
		uint32_t effects;
	};

	const uint8_t* AmmoTable()
	{
		const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kAmmoTable);
		if (!at)
			return nullptr;
		__try
		{
			return *reinterpret_cast<const uint8_t* const*>(at);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	// How many ammo types the table has (0: none loaded)
	int AmmoCount()
	{
		const uint8_t* table = AmmoTable();
		if (!table)
			return 0;
		__try
		{
			const uint32_t count = *reinterpret_cast<const uint32_t*>(table + kAmmoCount);
			return *reinterpret_cast<const void* const*>(table + kAmmoEntries) && count <= kMaxAmmo ? static_cast<int>(count) : 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	// An ammo type's entry and how many ranges it has (null: none, or not loaded)
	const uint8_t* AmmoEntry(int ammo, int& ranges)
	{
		ranges = 0;
		const uint8_t* table = AmmoTable();
		if (!table || ammo < 0 || ammo >= AmmoCount())
			return nullptr;
		__try
		{
			const uint8_t* const* entries = *reinterpret_cast<const uint8_t* const* const*>(table + kAmmoEntries);
			const uint8_t* entry = entries ? entries[ammo] : nullptr;
			if (!entry)
				return nullptr;
			const int count = *reinterpret_cast<const uint16_t*>(entry + kAmmoRangeCount);
			if (count < 1 || count > kMaxRanges)
				return nullptr;
			ranges = count;
			return entry;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	int RangeCount(int ammo)
	{
		int ranges = 0;
		AmmoEntry(ammo, ranges);
		return ranges;
	}

	bool ReadRange(int ammo, int range, RangeInfo& out)
	{
		int ranges = 0;
		const uint8_t* entry = AmmoEntry(ammo, ranges);
		if (!entry || range < 0 || range >= ranges)
			return false;
		const uint8_t* data = entry + kAmmoRanges + range * kRangeSize;
		__try
		{
			out.distance = *reinterpret_cast<const float*>(data + kRangeDistance);
			const uint32_t damages[6] = { kRangeDamagePlayer, kRangeDamageNpc, kRangeDamageAlien, kRangeDamageAndroid, kRangeDamageHeavy, kRangeDamageFacehugger };
			for (int i = 0; i < 6; i++)
				out.damage[i] = *reinterpret_cast<const int16_t*>(data + damages[i]);
			out.alienStun = *reinterpret_cast<const float*>(data + kRangeAlienStun);
			out.stunTime = *reinterpret_cast<const float*>(data + kRangeStunTime);
			out.empTime = *reinterpret_cast<const float*>(data + kRangeEmpTime);
			out.blindTime = *reinterpret_cast<const float*>(data + kRangeBlindTime);
			out.ragdoll = *reinterpret_cast<const float*>(data + kRangeRagdoll);
			out.effects = *reinterpret_cast<const uint32_t*>(data + kRangeEffects);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	int Bits(uint32_t value)
	{
		int count = 0;
		for (; value; value &= value - 1)
			count++;
		return count;
	}

	// The ammo and range an effect uses now: its usual one when that still carries the effect, else the range in the whole
	// table carrying it with the fewest other effects, then the least damage to an NPC
	struct Choice
	{
		int ammo = -1, range = -1;
		RangeInfo info = {};
	};

	bool PickAmmo(const Effect& effect, Choice& out)
	{
		RangeInfo info = {};
		if (ReadRange(effect.ammo, effect.range, info) && (info.effects & effect.flag))
		{
			out.ammo = effect.ammo;
			out.range = effect.range;
			out.info = info;
			return true;
		}
		int best = INT_MAX;
		const int count = AmmoCount();
		for (int ammo = 0; ammo < count; ammo++)
		{
			const int ranges = RangeCount(ammo);
			for (int range = 0; range < ranges; range++)
			{
				if (!ReadRange(ammo, range, info) || !(info.effects & effect.flag))
					continue;
				const int score = Bits(info.effects & kEffectBits & ~effect.flag) * 100000 + (std::max)(0, static_cast<int>(info.damage[1]));
				if (score < best)
				{
					best = score;
					out.ammo = ammo;
					out.range = range;
					out.info = info;
				}
			}
		}
		return best != INT_MAX;
	}

	// An ammo type by name (any case) or number, -1 when none
	int AmmoFromText(const std::string& text)
	{
		if (text.empty())
			return -1;
		char* end = nullptr;
		const long number = strtol(text.c_str(), &end, 10);
		if (end && *end == '\0')
			return number >= 0 && number < kMaxAmmo ? static_cast<int>(number) : -1;
		for (int i = 0; i < kAmmoNameCount; i++)
			if (_stricmp(text.c_str(), kAmmoNames[i]) == 0)
				return i;
		return -1;
	}

	std::string EffectNames(uint32_t effects)
	{
		struct Name { uint32_t bit; const char* name; };
		static const Name kNames[] = { { kStun, "stun" }, { kBlind, "blind" }, { kEmp, "EMP" }, { kAcid, "acid" }, { kGas, "gas" },
			{ kIncendiary, "fire" }, { kImpact, "impact" }, { 0x4000000, "collision" }, { 0x8000000, "melee" }, { 0x10000000, "sliding" } };
		std::string out;
		for (const Name& name : kNames)
			if (effects & name.bit)
				out += (out.empty() ? "" : " + ") + std::string(name.name);
		return out.empty() ? "no effect" : out;
	}

	// A line saying what an ammo's range is, for tooltips and the commands
	std::string Describe(int ammo, int range, const RangeInfo& info)
	{
		char text[320];
		snprintf(text, sizeof(text), "%s, range %d (within %.1f m): %s; damage %d to an NPC, %d to an android, %d to the alien; stun %.1f s, EMP %.1f s, blind %.1f s, alien stun %.0f",
			AmmoName(ammo).c_str(), range, info.distance, EffectNames(info.effects).c_str(), info.damage[1], info.damage[3], info.damage[2], info.stunTime,
			info.empTime, info.blindTime, info.alienStun);
		return text;
	}

	// ---- Who it goes to (render thread) ----

	std::atomic<bool> g_withDamage = false;
	std::atomic<float> g_radius = 20.0f;
	constexpr float kMinRadius = 1.0f, kMaxRadius = 50.0f;

	struct Target
	{
		uint32_t id = 0;
		std::string name;
		float origin[3] = {};     // where its explosion goes off: a metre from it towards the player
	};

	struct Plan
	{
		uint32_t player = 0;
		std::vector<Target> targets;
	};

	const DEBUG_MENU::Character* Player()
	{
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
			if (character.player)
				return &character;
		return nullptr;
	}

	// A metre from the character towards the player along the ground (in front of it when the player is on top of it), so
	// that it reacts as to a blast from the player's side
	void OriginFor(const DEBUG_MENU::Character& character, const DEBUG_MENU::Character& player, float out[3])
	{
		float dx = player.position[0] - character.position[0], dz = player.position[2] - character.position[2];
		const float length = std::sqrt(dx * dx + dz * dz);
		if (length > 0.05f)
		{
			dx /= length;
			dz /= length;
		}
		else
		{
			dx = std::sin(character.facing);
			dz = std::cos(character.facing);
		}
		out[0] = character.position[0] + dx;
		out[1] = character.position[1];
		out[2] = character.position[2] + dz;
	}

	float Distance(const float a[3], const float b[3])
	{
		const float dx = a[0] - b[0], dy = a[1] - b[1], dz = a[2] - b[2];
		return std::sqrt(dx * dx + dy * dy + dz * dz);
	}

	// The characters it goes to: the one with this unique id (the selected one for 0), or with radius > 0 every character
	// but the player within that distance of the player (straight through walls, as a blast's radius is)
	bool MakePlan(uint32_t id, float radius, Plan& out, std::string& error)
	{
		const DEBUG_MENU::Character* player = Player();
		if (!player)
		{
			error = "no player (no level running?)";
			return false;
		}
		out.player = player->id;
		if (radius > 0.0f)
		{
			for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
			{
				if (character.player || character.dead || Distance(character.position, player->position) > radius)
					continue;
				Target target;
				target.id = character.id;
				target.name = character.name;
				OriginFor(character, *player, target.origin);
				out.targets.push_back(target);
			}
			if (out.targets.empty())
			{
				char text[96];
				snprintf(text, sizeof(text), "nobody within %.0f m of the player", radius);
				error = text;
				return false;
			}
			return true;
		}
		if (!id)
			id = DEBUG_MENU::SelectedId();
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
			if (character.id == id)
			{
				if (character.player)
				{
					error = "not the player (Characters > Player has the player's screen effects)";
					return false;
				}
				if (character.dead)
				{
					error = character.name + " is dead";
					return false;
				}
				Target target;
				target.id = character.id;
				target.name = character.name;
				OriginFor(character, *player, target.origin);
				out.targets.push_back(target);
				return true;
			}
		error = id ? "no character with that unique id" : "no character selected (select one, or give id=<unique id>)";
		return false;
	}

	// ---- Sending (AI thread) ----

	enum class Sent { Done, Dead, Faulted };

	// The damage the range does to this kind of character (as the game's grenade reads it)
	int16_t DamageFor(const uint8_t* character, const RangeInfo& info)
	{
		int characterClass = -1;
		__try
		{
			const uint8_t* spawnedAs = *reinterpret_cast<const uint8_t* const*>(character + kCharacterSpawnedAs);
			characterClass = spawnedAs ? *reinterpret_cast<const int*>(spawnedAs + kSpawnedAsClass) : -1;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
		switch (characterClass)
		{
		case kClassAlien: return info.damage[2];
		case kClassAndroid: return info.damage[3];
		case kClassHeavyAndroid: return info.damage[4];
		case kClassFacehugger: return info.damage[5];
		default: return info.damage[1];
		}
	}

	Sent SendOne(const uint8_t* player, const uint8_t* victim, const float origin[3], int ammo, int range, int16_t damage)
	{
		const uintptr_t globalsAt = DEVTOOLS_RELATIVE_ADDRESS(kGameGlobals);
		ExplosionMessage message = {};
		__try
		{
			if (!globalsAt || !*reinterpret_cast<const uint8_t*>(globalsAt + kCharactersValid))
				return Sent::Faulted;
			if (*reinterpret_cast<const int*>(victim + kCharacterHealthState) == kHealthDead)
				return Sent::Dead;
			const uint8_t* globals = *reinterpret_cast<const uint8_t* const*>(globalsAt);
			uint8_t* dispatcher = globals ? *reinterpret_cast<uint8_t* const*>(globals + kGlobalsDamage) : nullptr;
			if (!dispatcher)
				return Sent::Faulted;
			memcpy(message.attacker, player + kCharacterHandle, sizeof(message.attacker));
			memcpy(message.victim, victim + kCharacterHandle, sizeof(message.victim));
			message.ammo = static_cast<uint16_t>(ammo);
			message.range = static_cast<int8_t>(range);
			memcpy(message.position, origin, sizeof(message.position));
			message.damage = damage;
			message.bone = -1;
			send_explosion(dispatcher + kExplosionHandler, &message, false);
			return Sent::Done;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return Sent::Faulted;
		}
	}

	std::string Send(const Plan& plan, int ammo, int range, bool withDamage, bool& ok)
	{
		ok = false;
		if (!send_explosion)
			return "not available on this release of the game";
		RangeInfo info = {};
		if (!ReadRange(ammo, range, info))
			return "the ammo table has no " + AmmoName(ammo) + " range " + std::to_string(range) + " (no level loaded?)";
		const uint8_t* player = AI_STATE::FindCharacter(plan.player);
		if (!player)
			return "the player has changed since this was asked (checkpoint restart or a new level)";
		std::ostringstream out;
		out << Describe(ammo, range, info) << "\n";
		int done = 0;
		for (const Target& target : plan.targets)
		{
			const uint8_t* victim = AI_STATE::FindCharacter(target.id);
			int16_t damage = 0;
			if (victim && withDamage)
				damage = (std::max)(static_cast<int16_t>(0), DamageFor(victim, info));
			const Sent sent = victim ? SendOne(player, victim, target.origin, ammo, range, damage) : Sent::Faulted;
			out << target.name << " (" << target.id << "): ";
			if (!victim)
				out << "gone";
			else if (sent == Sent::Dead)
				out << "dead, left alone";
			else if (sent == Sent::Faulted)
				out << "not sent (the game's damage handling could not be reached)";
			else
			{
				out << "sent" << (withDamage ? " with " + std::to_string(damage) + " damage" : std::string());
				done++;
			}
			out << "\n";
		}
		ok = done > 0;
		return out.str();
	}

	// ---- Status (any thread) ----

	std::mutex g_statusMutex;
	std::string g_status;

	void SetStatus(const std::string& text)
	{
		{
			std::lock_guard<std::mutex> lock(g_statusMutex);
			g_status = text;
		}
		DevTools::Log("Debug menu: damage effect: %s", text.c_str());
	}

	// ---- What was asked ----

	struct Request
	{
		const Effect* effect = nullptr;
		int ammo = -1, range = 0;
		bool radius = false;
		float radiusMetres = 0.0f;
		uint32_t id = 0;
		int damage = -1;           // -1: as the switch says; 0 none; 1 its damage
	};

	// The ammo and range a request goes out with, or why it can't
	bool Resolve(const Request& request, int& ammo, int& range, std::string& error)
	{
		if (request.effect)
		{
			Choice choice;
			if (!PickAmmo(*request.effect, choice))
			{
				error = std::string("no ammo in the game's table carries ") + request.effect->label + " (no level loaded?)";
				return false;
			}
			ammo = choice.ammo;
			range = choice.range;
			return true;
		}
		RangeInfo info = {};
		if (!ReadRange(request.ammo, request.range, info))
		{
			const int ranges = RangeCount(request.ammo);
			error = ranges ? AmmoName(request.ammo) + " has ranges 0 to " + std::to_string(ranges - 1) : "the game's ammo table has no " + AmmoName(request.ammo) + " (no level loaded?)";
			return false;
		}
		ammo = request.ammo;
		range = request.range;
		return true;
	}

	std::string Label(const Request& request)
	{
		std::string what = request.effect ? request.effect->label : AmmoName(request.ammo) + " range " + std::to_string(request.range);
		if (request.radius)
		{
			char text[48];
			snprintf(text, sizeof(text), " within %.0f m", request.radiusMetres);
			what += text;
		}
		return what;
	}

	// The ring drawn round the player after a send within a radius (render thread: ImGui time it stops)
	double g_ringUntil = 0.0;
	bool g_ringHover = false;

	// From the window (render thread): who it goes to is read now, the sending done on the AI thread
	void Ask(const Request& request)
	{
		int ammo = -1, range = -1;
		std::string error;
		Plan plan;
		if (!Resolve(request, ammo, range, error) || !MakePlan(request.id, request.radius ? request.radiusMetres : 0.0f, plan, error))
		{
			SetStatus(Label(request) + ": " + error);
			return;
		}
		if (request.radius)
			g_ringUntil = ImGui::GetTime() + 2.0;
		const bool withDamage = request.damage < 0 ? g_withDamage.load() : request.damage > 0;
		const std::string label = Label(request);
		SetStatus(label + ": asked (sent at the next AI update)");
		DEBUG_MENU::OnAiThread([plan, ammo, range, withDamage, label]()
		{
			bool ok = false;
			const std::string result = Send(plan, ammo, range, withDamage, ok);
			SetStatus(label + ": " + result);
		});
	}

	// ---- The section ----

	void Tooltip(const char* text)
	{
		if (!ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip | ImGuiHoveredFlags_AllowWhenDisabled))
			return;
		ImGui::BeginTooltip();
		ImGui::PushTextWrapPos(ImGui::GetFontSize() * 30.0f);
		ImGui::TextUnformatted(text);
		ImGui::PopTextWrapPos();
		ImGui::EndTooltip();
	}

	std::string EffectTooltip(const Effect& effect)
	{
		std::string text = effect.what;
		Choice choice;
		if (PickAmmo(effect, choice))
			text += "\n\nNow: " + Describe(choice.ammo, choice.range, choice.info) + ".";
		else
			text += "\n\nNo ammo in the game's table carries it now (no level loaded?).";
		return text;
	}

	void DrawSection()
	{
		ImGui::SeparatorText("Damage effects");
		if (!send_explosion)
		{
			ImGui::TextDisabled("Not available on this release of the game.");
			return;
		}
		ImGui::PushTextWrapPos(0.0f);
		ImGui::TextDisabled("The game's explosion damage from the player, carrying one effect as a grenade would: the character takes it as it would a real one.");
		ImGui::PopTextWrapPos();

		const DEBUG_MENU::Character* selected = DEBUG_MENU::Selected();
		const bool canSelected = selected && !selected->player && !selected->dead;
		ImGui::AlignTextToFramePadding();
		ImGui::TextUnformatted("The selected:");
		ImGui::BeginDisabled(!canSelected);
		for (const Effect& effect : kEffects)
		{
			ImGui::SameLine();
			char label[48];
			snprintf(label, sizeof(label), "%s##selected", effect.label);
			if (ImGui::Button(label) && selected)
			{
				Request request;
				request.effect = &effect;
				request.id = selected->id;
				Ask(request);
			}
			if (ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip | ImGuiHoveredFlags_AllowWhenDisabled))
				Tooltip(EffectTooltip(effect).c_str());
		}
		ImGui::EndDisabled();
		if (!canSelected)
		{
			ImGui::SameLine();
			ImGui::TextDisabled(!selected ? "(select an NPC)" : selected->player ? "(not the player)" : "(it is dead)");
		}

		ImGui::AlignTextToFramePadding();
		ImGui::TextUnformatted("Within the radius:");
		for (const Effect& effect : kEffects)
		{
			ImGui::SameLine();
			char label[48];
			snprintf(label, sizeof(label), "%s##radius", effect.label);
			if (ImGui::Button(label))
			{
				Request request;
				request.effect = &effect;
				request.radius = true;
				request.radiusMetres = g_radius;
				Ask(request);
			}
			g_ringHover = g_ringHover || ImGui::IsItemHovered();
			if (ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
				Tooltip(EffectTooltip(effect).c_str());
		}
		float radius = g_radius;
		ImGui::SetNextItemWidth(220.0f * DEBUG_MENU::Scale());
		if (ImGui::SliderFloat("Radius around the player", &radius, kMinRadius, kMaxRadius, "%.0f m", ImGuiSliderFlags_AlwaysClamp))
			g_radius = radius;
		g_ringHover = g_ringHover || ImGui::IsItemHovered() || ImGui::IsItemActive();
		Tooltip("Every character but the player within this distance of the player, measured straight (through walls and floors, as a blast's radius is). Shown as a ring round the player.");

		bool withDamage = g_withDamage;
		if (ImGui::Checkbox("With its damage", &withDamage))
			g_withDamage = withDamage;
		Tooltip("Off: the effect alone, no damage. On: the damage that range does to that kind of character too (as the ammo file gives it), which can kill - "
			"a kill counts as the player's, and the next checkpoint keeps it (Game > Saves' Don't write saves keeps that off the disk).");
		bool allyGuard = false;
		if (DEBUG_MENU::GetSwitch("ai.no_ally_kill_fail", allyGuard))
		{
			ImGui::SameLine();
			if (ImGui::Checkbox("Don't end the game for hurting an ally", &allyGuard))
				DEBUG_MENU::SetSwitch("ai.no_ally_kill_fail", allyGuard);
			Tooltip("With the damage on, hurting one of the player's allies can fail the mission as the level's ally monitor sees it. This holds that off "
				"(the same switch as elsewhere in the menu).");
		}

		std::lock_guard<std::mutex> lock(g_statusMutex);
		if (!g_status.empty())
		{
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextDisabled("Last: %s", g_status.c_str());
			ImGui::PopTextWrapPos();
		}
	}

	// Render thread, every frame: the ring round the player while the radius is being chosen and for a moment after a send
	void DrawOverlay(ImDrawList* drawList)
	{
		const bool hover = g_ringHover;
		g_ringHover = false;
		if (!hover && ImGui::GetTime() > g_ringUntil)
			return;
		const DEBUG_MENU::Character* player = Player();
		if (!player)
			return;
		const float radius = g_radius;
		DEBUG_MENU::WorldCircle(drawList, player->position, radius, IM_COL32(255, 160, 60, 220), (std::max)(32, (std::min)(160, static_cast<int>(radius * 8.0f))), 2.0f);
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
			if (!character.player && !character.dead && Distance(character.position, player->position) <= radius)
				DEBUG_MENU::WorldCross(drawList, character.position, 0.4f, IM_COL32(255, 160, 60, 240), 2.0f);
	}

	// ---- Commands ----

	DEBUG_MENU::CommandResult Fail(const std::string& message)
	{
		return { false, message };
	}

	DEBUG_MENU::CommandResult Run(const Request& request)
	{
		int ammo = -1, range = -1;
		std::string error;
		if (!Resolve(request, ammo, range, error))
			return Fail(error);
		std::shared_ptr<Plan> plan = std::make_shared<Plan>();
		const DEBUG_MENU::CommandResult planned = DEBUG_MENU::AnswerOnRenderThread([request, plan]() -> DEBUG_MENU::CommandResult
		{
			std::string why;
			if (!MakePlan(request.id, request.radius ? request.radiusMetres : 0.0f, *plan, why))
				return { false, why };
			if (request.radius)
				g_ringUntil = ImGui::GetTime() + 2.0;
			return { true, "" };
		});
		if (!planned.ok)
			return planned;
		const Plan copy = *plan;
		const bool withDamage = request.damage < 0 ? g_withDamage.load() : request.damage > 0;
		const std::string label = Label(request);
		return DEBUG_MENU::AnswerOnAiThread([copy, ammo, range, withDamage, label]() -> DEBUG_MENU::CommandResult
		{
			bool ok = false;
			const std::string result = Send(copy, ammo, range, withDamage, ok);
			SetStatus(label + ": " + result);
			return { ok, result };
		});
	}

	std::string Lower(std::string text)
	{
		for (char& c : text)
			c = static_cast<char>(tolower(static_cast<unsigned char>(c)));
		return text;
	}

	bool RegisterControls()
	{
		DEBUG_MENU::AddSwitch("ai.damage_effect_with_damage", &g_withDamage);
		DEBUG_MENU::AddValue("ai.damage_effect_radius", &g_radius, kMinRadius, kMaxRadius);
		DEBUG_MENU::AddCommand("ai.damage_effect",
			"<stun|emp|blind|acid|gas|fire | ammo=<name|number> [range=<n>]> [selected | radius[=<m>]] [damage|nodamage] [id=<unique id>] - the game's explosion "
			"damage from the player carrying that effect, on the selected NPC (or the one id names) or on every character within the radius of the player "
			"(ai.damage_effect_radius when no number); damage as ai.damage_effect_with_damage says unless given (AI thread)",
			[](const std::string& argument) -> DEBUG_MENU::CommandResult
		{
			if (!send_explosion)
				return Fail("not available on this release of the game");
			Request request;
			bool haveWhat = false;
			std::istringstream stream(argument);
			std::string word;
			while (stream >> word)
			{
				const std::string lower = Lower(word);
				if (lower.compare(0, 3, "id=") == 0)
					request.id = static_cast<uint32_t>(strtoul(lower.c_str() + 3, nullptr, 0));
				else if (lower.compare(0, 5, "ammo=") == 0)
				{
					request.ammo = AmmoFromText(word.substr(5));
					if (request.ammo < 0)
						return Fail("no ammo type called " + word.substr(5) + " (ai.damage_effects all lists them)");
					request.effect = nullptr;
					haveWhat = true;
				}
				else if (lower.compare(0, 6, "range=") == 0)
					request.range = atoi(lower.c_str() + 6);
				else if (lower == "selected")
					request.radius = false;
				else if (lower == "radius" || lower.compare(0, 7, "radius=") == 0)
				{
					request.radius = true;
					request.radiusMetres = lower.size() > 7 ? static_cast<float>(atof(lower.c_str() + 7)) : g_radius.load();
					if (request.radiusMetres <= 0.0f)
						return Fail("give the radius in metres (radius=20)");
				}
				else if (lower == "damage")
					request.damage = 1;
				else if (lower == "nodamage")
					request.damage = 0;
				else if (const Effect* effect = FindEffect(lower))
				{
					request.effect = effect;
					haveWhat = true;
				}
				else
					return Fail("not understood: " + word + " (give stun, emp, blind, acid, gas, fire or ammo=<name|number>)");
			}
			if (!haveWhat)
				return Fail("give an effect: stun, emp, blind, acid, gas or fire (or ammo=<name|number> range=<n>)");
			return Run(request);
		});
		DEBUG_MENU::AddCommand("ai.damage_effects", "[all] - the ammo and range each effect uses now, a line each (all: every range of every ammo type in the game's table)",
			[](const std::string& argument) -> DEBUG_MENU::CommandResult
		{
			std::string out;
			if (Lower(argument).find("all") != std::string::npos)
			{
				const int count = AmmoCount();
				for (int ammo = 0; ammo < count; ammo++)
				{
					const int ranges = RangeCount(ammo);
					for (int range = 0; range < ranges; range++)
					{
						RangeInfo info = {};
						if (ReadRange(ammo, range, info))
							out += std::to_string(ammo) + " " + Describe(ammo, range, info) + "\n";
					}
				}
				return out.empty() ? Fail("the game's ammo table is not loaded (no level running?)") : DEBUG_MENU::CommandResult{ true, out };
			}
			for (const Effect& effect : kEffects)
			{
				Choice choice;
				out += std::string(effect.key) + "=" + (PickAmmo(effect, choice) ? Describe(choice.ammo, choice.range, choice.info) : std::string("none in the game's table")) + "\n";
			}
			char text[96];
			snprintf(text, sizeof(text), "with_damage=%d\nradius=%.1f\n", g_withDamage ? 1 : 0, g_radius.load());
			return { true, out + text };
		});
		return true;
	}

	const bool g_controls = RegisterControls();
	const bool g_added = DEBUG_MENU::AddTab("Control", 16, DrawSection, "Characters") && DEBUG_MENU::AddOverlay(13, DrawOverlay);
}

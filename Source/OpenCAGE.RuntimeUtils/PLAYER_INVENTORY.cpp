#include "PLAYER_DEBUG.h"
#include "DEBUG_MENU.h"

#include <algorithm>
#include <atomic>
#include <cctype>
#include <cfloat>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <memory>
#include <mutex>
#include <string>
#include <vector>

#include <detours.h>
#include <imgui.h>

/*
	The Inventory tab (see PLAYER_DEBUG.h): the weapon in the player's hands, the inventory, and giving the player
	items, done with the game's own calls, on the entity thread:

	- Giving an item is what the level scripts' "add to inventory" entity does: the game's check that there is room for
	  it, then its add without the pickup notification (as many as asked for, capped at what can be carried). "Give all"
	  is a fixed list (medikits, flares, every IED and every crafting part, as many as can be carried) and every
	  blueprint. The game's item list is read from its own database, so modded items show too. An item can be asked for
	  by the game's own name for it (small_medikit), the name the game shows (Medikit), a name players use (medkit), or
	  enough of either to tell which.
	- Ammo for a weapon: each ammo type the game's database has for that weapon (a shotgun takes three), filled up.
	- Blueprints: the game's own "make available" on each, which also marks it collected.
	- Gating tools (the key, axe, crowbar, gas mask, cutting tool, hacking tool, rewire and cable clamps) and keycards are
	  plain values in the inventory, written as the game's own setters write them. Every tool is given at level 50
	  by default. A hack's difficulty follows the hacking tool's level through a table in the game's database (a level in no row
	  of it, such as 50, opens every hack); the tab shows that table.
	- Infinite items: as the player's inventory uses up the item in hand (thrown, placed, used or equipped), one more of it
	  is added first, so the count stays put and the item stays selected.
	- Never run out of torch batteries: keeps the inventory full of torch batteries (the battery in the torch drains by
	  the level's scripts; a new one is taken from the inventory).

	All of these change what the next save holds.
*/
namespace
{
	// The inventory: its 34 slots (each the item, its equipment, how many, a property mask and whether locked), the 8
	// gating tools (each a level and whether the player has it), the 30 keycards (each an id and whether collected), the
	// item in the player's hand, the item selected, and the power left in the torch's battery (copied there by the torch's
	// script entity for the HUD)
	constexpr uint32_t kSlots = 0x68;
	constexpr int kSlotCount = 34;
	constexpr uint32_t kSlotSize = 0x14;
	constexpr uint32_t kSlotItem = 0x00;
	constexpr uint32_t kSlotCountOf = 0x08;
	constexpr uint32_t kSlotLocked = 0x10;
	constexpr uint32_t kGatingTools = 0x374;
	constexpr int kGatingToolCount = 8;
	constexpr uint32_t kGatingToolSize = 0x08;
	constexpr uint32_t kGatingToolAvailable = 0x04;
	constexpr uint32_t kKeycards = 0x3B4;
	constexpr int kKeycardCount = 30;
	constexpr uint32_t kKeycardSize = 0x08;
	constexpr uint32_t kKeycardCollected = 0x04;
	constexpr uint32_t kHeldItem = 0x3BC8;
	constexpr uint32_t kSelectedItem = 0x3BEC;
	constexpr uint32_t kTorchPower = 0x3D20;

	// An item template: the text key of the name the game shows for it (its own name when it has none), its name, its
	// kind, how many come at a time, how many can be carried; for ammo, the weapon it is for
	constexpr uint32_t kItemShownName = 0x04;
	constexpr uint32_t kItemName = 0x08;
	constexpr uint32_t kItemKind = 0x0C;
	constexpr uint32_t kItemBatch = 0x18;
	constexpr uint32_t kItemCarry = 0x1C;
	constexpr uint32_t kAmmoWeapon = 0x50;
	enum ItemKind { kNone, kWeapon, kCraftingPart, kExplosive, kUsable, kAmmo, kMotionTracker, kHealth, kLight, kKinds };

	// The item database's lists: each a linked list whose first node is at this offset, a node holding its entry first and
	// the next node at +8 - the item templates, the blueprints and the keycards
	constexpr uint32_t kDatabaseItems = 0x04;
	constexpr uint32_t kDatabaseBlueprints = 0x10;
	constexpr uint32_t kDatabaseKeycards = 0x5C;
	constexpr uint32_t kDatabaseToolLevels = 0xBC;
	constexpr uint32_t kNodeNext = 0x08;
	// A row of the hacking tool's levels: the level, and the hardest hack it opens (a level in no row opens every one)
	constexpr uint32_t kToolLevelHardest = 0x04;
	constexpr int kMaxListed = 1024;
	// A blueprint: the name of what it makes (in place), and whether it is available; a keycard: its id, then its name
	constexpr uint32_t kBlueprintName = 0x1C;
	constexpr uint32_t kBlueprintAvailable = 0xA0;
	constexpr uint32_t kKeycardEntryName = 0x04;

	// The player's weapon control (a pointer): its equipment (11 slots), what kind of item is active, the shooter state,
	// whether firing, the current slot and whether it is equipped, and whether the torch is on
	constexpr uint32_t kCharacterWeapons = 0x1100;
	constexpr int kEquipmentSlots = 11;
	constexpr uint32_t kActiveKind = 0x2C;
	constexpr uint32_t kFiring = 0xA7;
	constexpr uint32_t kCurrentSlot = 0x150C;
	constexpr uint32_t kCurrentEquipped = 0x1510;
	constexpr uint32_t kTorchOn = 0x1574;
	// A piece of equipment: its item; its sixth virtual function gives it as an aimed weapon (null when it isn't one); an
	// aimed weapon's ammo type, weapon type, overheating state, ammo set aside for it, ammo in the clip, clip size,
	// overheating charge and charge
	constexpr uint32_t kEquipmentItem = 0x44;
	constexpr uint32_t kAsAimedWeapon = 0x14;
	constexpr uint32_t kWeaponAmmoType = 0xD0;
	constexpr uint32_t kWeaponType = 0xD4;
	constexpr uint32_t kWeaponOverheatState = 0xD8;
	constexpr uint32_t kWeaponAmmoAside = 0xDC;
	constexpr uint32_t kWeaponClip = 0xE0;
	constexpr uint32_t kWeaponClipSize = 0xEC;
	constexpr uint32_t kWeaponOverheat = 0x114;
	constexpr uint32_t kWeaponCharge = 0x128;

	// "As many as can be carried" (the game caps it)
	constexpr int kFill = 0x7FFFFFFF;
	constexpr int kDevelopersToolLevel = 50;

	typedef bool(__thiscall* t_is_room)(void* inventory, void* item, int count);
	const auto is_room_for_item = reinterpret_cast<t_is_room>(DEVTOOLS_RELATIVE_ADDRESS(0x0038E6B0));
	typedef bool(__thiscall* t_add_item)(void* inventory, void* item, int count);
	const auto add_item = reinterpret_cast<t_add_item>(DEVTOOLS_RELATIVE_ADDRESS(0x003AA4E0));
	typedef void(__thiscall* t_set_available)(void* blueprint, bool available);
	const auto set_blueprint_available = reinterpret_cast<t_set_available>(DEVTOOLS_RELATIVE_ADDRESS(0x0038F230));
	typedef void*(__thiscall* t_ammo_for_weapon)(void* database, void* weapon, int index);
	const auto ammo_for_weapon = reinterpret_cast<t_ammo_for_weapon>(DEVTOOLS_RELATIVE_ADDRESS(0x003A1B60));
	typedef void*(__thiscall* t_as_aimed)(void* equipment);

	using namespace PLAYER_DEBUG;

	std::atomic<bool> g_infiniteItems = false;
	std::atomic<bool> g_torchBatteries = false;

	const char* KindName(int kind)
	{
		static const char* const names[] = { "none", "weapon", "crafting part", "explosive", "usable", "ammo", "motion tracker", "health", "light" };
		return kind >= 0 && kind < kKinds ? names[kind] : "?";
	}

	const char* WeaponTypeName(int type)
	{
		static const char* const names[] = { "none", "flamethrower", "pistol", "shotgun", "melee", "bolt gun", "cattle prod" };
		return type >= 0 && type < 7 ? names[type] : "?";
	}

	const char* AmmoTypeName(int type)
	{
		static const char* const names[] = {
			"pistol", "pistol dum-dum", "SMG", "SMG dum-dum", "shotgun", "shotgun slug", "flamethrower", "flamethrower aerated",
			"flamethrower high damage", "shotgun incendiary", "pistol (NPC)", "shotgun (NPC)", "grenade", "fire grenade",
			"catalyst HE small", "catalyst HE large", "catalyst fire small", "catalyst fire large", "acid burst small",
			"acid burst large", "EMP burst small", "EMP burst large", "impact", "stun grenade", "smoke grenade", "taser",
			"axe", "bolt gun", "push", "cattle prod power pack", "EMP burst large (tier 2)", "EMP burst large (tier 3)",
			"fire grenade (tier 2)", "fire grenade (tier 3)", "grenade (tier 2)", "grenade (tier 3)", "stun grenade (tier 2)",
			"stun grenade (tier 3)", "environment flame" };
		return type >= 0 && type < static_cast<int>(sizeof(names) / sizeof(names[0])) ? names[type] : "?";
	}

	// The gating tools, in the game's order: a name for commands and a label
	struct Tool { const char* name; const char* label; };
	const Tool kTools[kGatingToolCount] = {
		{ "key", "Key" }, { "axe", "Axe" }, { "crowbar", "Crowbar (maintenance jack)" }, { "gas_mask", "Gas mask" },
		{ "cutting_tool", "Cutting tool (ion torch)" }, { "hacking_tool", "Hacking tool" }, { "rewire", "Rewire tool" },
		{ "cable_clamps", "Cable clamps" } };

	bool EqualsIgnoringCase(const std::string& a, const std::string& b)
	{
		if (a.size() != b.size())
			return false;
		for (size_t i = 0; i < a.size(); i++)
			if (tolower(static_cast<unsigned char>(a[i])) != tolower(static_cast<unsigned char>(b[i])))
				return false;
		return true;
	}

	std::string Trim(const std::string& text)
	{
		size_t start = 0, end = text.size();
		while (start < end && isspace(static_cast<unsigned char>(text[start])))
			start++;
		while (end > start && isspace(static_cast<unsigned char>(text[end - 1])))
			end--;
		return text.substr(start, end - start);
	}

	// ---- Reading (any thread) ----

	// The entries of one of the database's lists
	int ListEntries(uint32_t list, uint8_t** out, int max)
	{
		const uint8_t* node = PointerAt(ItemDatabase(), list);
		int count = 0;
		while (node && count < max)
		{
			uint8_t* entry = PointerAt(node, 0);
			if (entry)
				out[count++] = entry;
			node = PointerAt(node, kNodeNext);
		}
		return count;
	}

	struct Template
	{
		uint8_t* item = nullptr;
		std::string name;
		std::string shown;   // the name the game shows for it, in its language (empty: none of its own)
		int kind = 0, batch = 0, carry = 0;
	};

	// A name reduced to its letters and digits in lower case, so "Pipe Bomb", "pipe_bomb" and "PIPE-BOMB" read the same
	std::string Key(const std::string& text)
	{
		std::string key;
		for (const char c : text)
			if (isalnum(static_cast<unsigned char>(c)))
				key += static_cast<char>(tolower(static_cast<unsigned char>(c)));
		return key;
	}

	// The game's item list starts with a stand-in for "no item": never listed or given (it would go into a save)
	bool IsPlaceholderItem(const std::string& name)
	{
		return EqualsIgnoringCase(name, "INVALID_ITEM");
	}

	std::vector<Template> ReadTemplates()
	{
		std::vector<Template> templates;
		std::vector<uint8_t*> items(kMaxListed);
		const int count = ListEntries(kDatabaseItems, items.data(), kMaxListed);
		for (int i = 0; i < count; i++)
		{
			Template entry;
			entry.item = items[i];
			entry.name = TextAt(items[i], kItemName);
			// Only a name the game shows that reads differently from its own (the medikit's is "Medikit" in English)
			entry.shown = Translated(TextAt(items[i], kItemShownName));
			if (Key(entry.shown) == Key(entry.name))
				entry.shown.clear();
			Read(items[i] + kItemKind, entry.kind);
			Read(items[i] + kItemBatch, entry.batch);
			Read(items[i] + kItemCarry, entry.carry);
			if (!entry.name.empty() && !IsPlaceholderItem(entry.name))
				templates.push_back(entry);
		}
		std::sort(templates.begin(), templates.end(), [](const Template& a, const Template& b) { return a.kind != b.kind ? a.kind < b.kind : a.name < b.name; });
		return templates;
	}

	uint8_t* FindTemplate(const std::string& name)
	{
		std::vector<uint8_t*> items(kMaxListed);
		const int count = ListEntries(kDatabaseItems, items.data(), kMaxListed);
		for (int i = 0; i < count; i++)
			if (EqualsIgnoringCase(TextAt(items[i], kItemName), name) && !IsPlaceholderItem(name))
				return items[i];
		return nullptr;
	}

	// What players call items, in English, where the game's own names differ (the names the game shows are in the
	// language it runs in; these work whatever that is)
	struct Alias { const char* said; const char* item; };
	const Alias kAliases[] = {
		{ "medikit", "small_medikit" }, { "medkit", "small_medikit" }, { "large medkit", "large_medikit" }, { "revolver", "pistol" },
		{ "revolver ammo", "9mm_pistol_ammo" }, { "stun baton", "cattleprod" }, { "stun baton charge", "charge" }, { "axe", "emergency_axe" },
		{ "bolt gun ammo", "bolt" }, { "bolts", "bolt" }, { "flamethrower fuel", "flamer_fuel" }, { "fuel", "flamer_fuel" },
		{ "slugs", "shotgun_solid_slug_shells" }, { "incendiary shells", "shotgun_incendiary_shells" }, { "molotov", "molotov_cocktail" },
		{ "emp", "emp_mine" }, { "smoke", "smoke_bomb" }, { "flashlight batteries", "torch_battery" }, { "flashlight battery", "torch_battery" },
		{ "charge pack", "high_voltage_battery" }, { "blasting cap", "explosive" }, { "sensor", "pipe" }, { "compound b", "gel" },
		{ "bonding agent", "adhesive" }, { "scj injector", "sharp_blade" }, { "blade", "sharp_blade" }, { "ethanol", "gasoline" },
		{ "id card", "memento_dogtag" }, { "oscillator", "mission_oscillator" } };

	// "name (shown name)" for a message
	std::string Described(const Template& entry)
	{
		return entry.shown.empty() ? entry.name : entry.name + " (" + entry.shown + ")";
	}

	// The item a player means by a name: the game's own name, the name the game shows, a name players use for it, or (when
	// only one item fits) part of either - "flares" and "medikits" too; null with why not (none fits, or several do)
	uint8_t* FindItem(const std::string& said, std::string& why)
	{
		if (uint8_t* exact = FindTemplate(said))
			return exact;
		const std::vector<Template> templates = ReadTemplates();
		if (templates.empty())
		{
			why = "the game's item list isn't loaded (no level running)";
			return nullptr;
		}
		std::string key = Key(said);
		for (int pass = 0; pass < 2 && !key.empty(); pass++)
		{
			// Its own name or the name shown, then a name players use, then part of either
			for (const Template& entry : templates)
				if (Key(entry.name) == key || (!entry.shown.empty() && Key(entry.shown) == key))
					return entry.item;
			for (const Alias& alias : kAliases)
				if (Key(alias.said) == key)
					for (const Template& entry : templates)
						if (entry.name == alias.item)
							return entry.item;
			if (key.size() >= 3)
			{
				std::vector<const Template*> fits;
				for (const Template& entry : templates)
					if (Key(entry.name).find(key) != std::string::npos || (!entry.shown.empty() && Key(entry.shown).find(key) != std::string::npos))
						fits.push_back(&entry);
				if (fits.size() == 1)
					return fits[0]->item;
				if (fits.size() > 1)
				{
					why = "\"" + said + "\" could be any of: ";
					for (size_t i = 0; i < fits.size(); i++)
						why += (i ? ", " : "") + Described(*fits[i]);
					why += " - give one of these names";
					return nullptr;
				}
			}
			// Then again without a plural's "s"
			if (key.size() < 2 || key.back() != 's')
				break;
			key.pop_back();
		}
		why = "no item called \"" + said + "\". The game's items (as they are shown in brackets): ";
		for (size_t i = 0; i < templates.size(); i++)
			why += (i ? ", " : "") + Described(templates[i]);
		return nullptr;
	}

	// The game's table of hacking tool levels: "level -> hardest hack" for each row
	std::string ToolLevels()
	{
		uint8_t* rows[64];
		const int count = ListEntries(kDatabaseToolLevels, rows, 64);
		std::string text;
		for (int i = 0; i < count; i++)
		{
			int level = 0, hardest = 0;
			if (Read(rows[i], level) && Read(rows[i] + kToolLevelHardest, hardest))
				text += (text.empty() ? "" : ", ") + std::to_string(level) + " -> " + std::to_string(hardest);
		}
		return text;
	}

	struct Slot
	{
		int index = 0;
		uint8_t* item = nullptr;
		std::string name;
		int kind = 0, count = 0, carry = 0;
		bool locked = false, held = false, selected = false;
	};

	std::vector<Slot> ReadSlots(const uint8_t* inventory)
	{
		std::vector<Slot> slots;
		if (!inventory)
			return slots;
		uint8_t* held = PointerAt(inventory, kHeldItem);
		uint8_t* selected = PointerAt(inventory, kSelectedItem);
		for (int i = 0; i < kSlotCount; i++)
		{
			const uint8_t* at = inventory + kSlots + i * kSlotSize;
			Slot slot;
			slot.index = i;
			slot.item = PointerAt(at, kSlotItem);
			if (!slot.item)
				continue;
			slot.name = TextAt(slot.item, kItemName);
			Read(slot.item + kItemKind, slot.kind);
			Read(slot.item + kItemCarry, slot.carry);
			Read(at + kSlotCountOf, slot.count);
			Read(at + kSlotLocked, slot.locked);
			slot.held = slot.item == held;
			slot.selected = slot.item == selected;
			slots.push_back(slot);
		}
		return slots;
	}

	int CountOf(const uint8_t* inventory, const uint8_t* item)
	{
		int total = 0;
		for (int i = 0; inventory && item && i < kSlotCount; i++)
		{
			const uint8_t* at = inventory + kSlots + i * kSlotSize;
			int count = 0;
			if (PointerAt(at, kSlotItem) == item && Read(at + kSlotCountOf, count))
				total += count;
		}
		return total;
	}

	// The weapon in the player's hands
	struct Weapon
	{
		bool any = false;      // something is equipped
		bool aimed = false;    // a gun (or flamethrower, bolt gun, cattle prod)
		uint8_t* item = nullptr;
		std::string name;
		int activeKind = 3, weaponType = 0, ammoType = 0, overheatState = 0;
		int clip = 0, clipSize = 0, aside = 0, carried = 0;
		float overheat = 0.0f, charge = 0.0f;
		bool firing = false, torchOn = false;
	};

	void* AsAimedWeapon(void* equipment)
	{
		__try
		{
			const uintptr_t* table = *reinterpret_cast<const uintptr_t* const*>(equipment);
			const auto as_aimed = reinterpret_cast<t_as_aimed>(table[kAsAimedWeapon / 4]);
			return as_aimed ? as_aimed(equipment) : nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	Weapon ReadWeapon(const uint8_t* player, const uint8_t* inventory)
	{
		Weapon weapon;
		const uint8_t* control = PointerAt(player, kCharacterWeapons);
		if (!control)
			return weapon;
		Read(control + kActiveKind, weapon.activeKind);
		Read(control + kFiring, weapon.firing);
		Read(control + kTorchOn, weapon.torchOn);
		uint32_t slot = 0;
		bool equipped = false;
		if (!Read(control + kCurrentSlot, slot) || !Read(control + kCurrentEquipped, equipped) || !equipped || slot >= kEquipmentSlots)
			return weapon;
		uint8_t* equipment = PointerAt(control, slot * 4);
		if (!equipment)
			return weapon;
		weapon.any = true;
		weapon.item = PointerAt(equipment, kEquipmentItem);
		weapon.name = TextAt(weapon.item, kItemName);
		const uint8_t* aimed = static_cast<const uint8_t*>(AsAimedWeapon(equipment));
		if (!aimed)
			return weapon;
		weapon.aimed = true;
		Read(aimed + kWeaponAmmoType, weapon.ammoType);
		Read(aimed + kWeaponType, weapon.weaponType);
		Read(aimed + kWeaponOverheatState, weapon.overheatState);
		Read(aimed + kWeaponAmmoAside, weapon.aside);
		Read(aimed + kWeaponClip, weapon.clip);
		Read(aimed + kWeaponClipSize, weapon.clipSize);
		Read(aimed + kWeaponOverheat, weapon.overheat);
		Read(aimed + kWeaponCharge, weapon.charge);
		// Carried: the ammo in the inventory for this weapon, and what is set aside for it (as the HUD counts it)
		weapon.carried = weapon.aside;
		for (const Slot& each : ReadSlots(inventory))
			if (each.kind == kAmmo && PointerAt(each.item, kAmmoWeapon) == weapon.item)
				weapon.carried += each.count;
		return weapon;
	}

	// ---- Changing the inventory (entity thread) ----

	enum class AddResult { Added, NoRoom, Failed };

	// The level scripts' way: the game's check for room, then its add without the pickup notification
	AddResult AddGuarded(uint8_t* inventory, uint8_t* item, int count)
	{
		__try
		{
			if (!is_room_for_item(inventory, item, 1))
				return AddResult::NoRoom;
			add_item(inventory, item, count);
			return AddResult::Added;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return AddResult::Failed;
		}
	}

	bool CanGive()
	{
		return is_room_for_item && add_item;
	}

	// Gives count of an item (kFill: as many as can be carried); how many more the inventory holds after, or -1 (no room)
	// or -2 (failed)
	int GiveItem(uint8_t* inventory, uint8_t* item, int count)
	{
		const int before = CountOf(inventory, item);
		switch (AddGuarded(inventory, item, count))
		{
		case AddResult::NoRoom: return -1;
		case AddResult::Failed: return -2;
		default: return CountOf(inventory, item) - before;
		}
	}

	DEBUG_MENU::CommandResult GiveByName(const std::string& name, int count, StatusArea area = StatusArea::Give)
	{
		uint8_t* inventory = Inventory();
		if (!inventory)
			return { false, "no player inventory (no level running)" };
		if (!CanGive())
			return { false, "not available on this game release" };
		std::string why;
		uint8_t* item = FindItem(name, why);
		if (!item)
			return { false, why };
		// Named in messages by its own name (what player.give and player.list_items use)
		const std::string itemName = TextAt(item, kItemName);
		const int added = GiveItem(inventory, item, count);
		char text[256];
		if (added == -1)
			snprintf(text, sizeof(text), "no room for %s (the most that can be carried, or no free slot)", itemName.c_str());
		else if (added < 0)
			snprintf(text, sizeof(text), "giving %s failed", itemName.c_str());
		else
			snprintf(text, sizeof(text), "gave %d %s (now %d)", added, itemName.c_str(), CountOf(inventory, item));
		SetStatus(area, text);
		return { added >= 0, text };
	}

	// The game's index-th ammo type for a weapon item (null: no more)
	uint8_t* AmmoForWeapon(uint8_t* database, uint8_t* weapon, int index)
	{
		__try
		{
			return static_cast<uint8_t*>(ammo_for_weapon(database, weapon, index));
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	// Every ammo type the game has for a weapon item, filled up; how many added in all
	int FillAmmoFor(uint8_t* inventory, uint8_t* weapon, std::string& names)
	{
		uint8_t* database = ItemDatabase();
		int added = 0;
		for (int i = 0; database && weapon && i < 8; i++)
		{
			uint8_t* ammo = AmmoForWeapon(database, weapon, i);
			if (!ammo)
				break;
			const int given = GiveItem(inventory, ammo, kFill);
			if (given > 0)
			{
				added += given;
				names += (names.empty() ? "" : ", ") + TextAt(ammo, kItemName);
			}
		}
		return added;
	}

	DEBUG_MENU::CommandResult GiveAmmo(bool everyWeapon)
	{
		uint8_t* inventory = Inventory();
		uint8_t* player = Player();
		if (!inventory || !player)
			return { false, "no player inventory (no level running)" };
		if (!CanGive() || !ammo_for_weapon)
			return { false, "not available on this game release" };
		std::vector<uint8_t*> weapons;
		if (everyWeapon)
		{
			for (const Slot& slot : ReadSlots(inventory))
				if (slot.kind == kWeapon)
					weapons.push_back(slot.item);
		}
		else
		{
			const Weapon weapon = ReadWeapon(player, inventory);
			if (!weapon.aimed || !weapon.item)
				return { false, "the player isn't holding a weapon that takes ammo" };
			weapons.push_back(weapon.item);
		}
		std::string names;
		int added = 0;
		for (uint8_t* weapon : weapons)
			added += FillAmmoFor(inventory, weapon, names);
		const std::string text = added ? "gave " + std::to_string(added) + " ammo (" + names + ")" : "no ammo added (full already, or nothing takes ammo)";
		SetStatus(StatusArea::Weapon, text);
		return { true, text };
	}

	// "Give all": these as many as can be carried, then every blueprint
	const char* const kGiveAll[] = { "small_medikit", "flare", "pipe_bomb", "molotov_cocktail", "emp_mine", "smoke_bomb", "flashbang",
		"noisemaker", "gasoline", "high_voltage_battery", "explosive", "pipe", "sharp_blade", "gel", "adhesive", "scrap" };

	bool MakeAvailable(uint8_t* blueprint)
	{
		__try
		{
			set_blueprint_available(blueprint, true);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	int GiveBlueprintsNow()
	{
		std::vector<uint8_t*> blueprints(kMaxListed);
		const int count = ListEntries(kDatabaseBlueprints, blueprints.data(), kMaxListed);
		int given = 0;
		for (int i = 0; i < count && set_blueprint_available; i++)
		{
			bool available = true;
			if (Read(blueprints[i] + kBlueprintAvailable, available) && !available && MakeAvailable(blueprints[i]))
				given++;
		}
		return given;
	}

	DEBUG_MENU::CommandResult GiveAll()
	{
		uint8_t* inventory = Inventory();
		if (!inventory)
			return { false, "no player inventory (no level running)" };
		if (!CanGive())
			return { false, "not available on this game release" };
		int items = 0;
		std::string missing;
		for (const char* name : kGiveAll)
		{
			uint8_t* item = FindTemplate(name);
			if (!item)
			{
				missing += (missing.empty() ? "" : ", ") + std::string(name);
				continue;
			}
			const int added = GiveItem(inventory, item, kFill);
			if (added > 0)
				items += added;
		}
		const int blueprints = GiveBlueprintsNow();
		std::string text = "gave " + std::to_string(items) + " items and " + std::to_string(blueprints) + " blueprints";
		if (!missing.empty())
			text += " (not in this game's items: " + missing + ")";
		SetStatus(StatusArea::Give, text);
		return { true, text };
	}

	DEBUG_MENU::CommandResult GiveWeapons()
	{
		uint8_t* inventory = Inventory();
		if (!inventory)
			return { false, "no player inventory (no level running)" };
		if (!CanGive())
			return { false, "not available on this game release" };
		std::string names, ammo;
		int ammoAdded = 0;
		for (const Template& entry : ReadTemplates())
		{
			if (entry.kind != kWeapon)
				continue;
			if (GiveItem(inventory, entry.item, 1) > 0)
				names += (names.empty() ? "" : ", ") + entry.name;
			if (ammo_for_weapon)
				ammoAdded += FillAmmoFor(inventory, entry.item, ammo);
		}
		const std::string text = (names.empty() ? std::string("no new weapons") : "gave " + names) + "; " + std::to_string(ammoAdded) + " ammo";
		SetStatus(StatusArea::Give, text);
		return { true, text };
	}

	DEBUG_MENU::CommandResult GiveBlueprints()
	{
		if (!Inventory())
			return { false, "no player inventory (no level running)" };
		if (!set_blueprint_available)
			return { false, "not available on this game release" };
		const std::string text = "made " + std::to_string(GiveBlueprintsNow()) + " blueprints available";
		SetStatus(StatusArea::Collected, text);
		return { true, text };
	}

	// A gating tool's level and whether the player has it (as the game's own setters write them)
	DEBUG_MENU::CommandResult SetTool(int tool, int level, bool available)
	{
		uint8_t* inventory = Inventory();
		if (!inventory)
			return { false, "no player inventory (no level running)" };
		if (tool < 0 || tool >= kGatingToolCount)
			return { false, "no such gating tool" };
		uint8_t* at = inventory + kGatingTools + tool * kGatingToolSize;
		if (!Write<int>(at, level) || !Write<uint8_t>(at + kGatingToolAvailable, available ? 1 : 0))
			return { false, "the gating tool could not be written" };
		char text[128];
		snprintf(text, sizeof(text), "%s: %s, level %d", kTools[tool].name, available ? "given" : "taken away", level);
		SetStatus(StatusArea::Tools, text);
		return { true, text };
	}

	DEBUG_MENU::CommandResult GiveTools(int level)
	{
		for (int i = 0; i < kGatingToolCount; i++)
		{
			const DEBUG_MENU::CommandResult result = SetTool(i, level, true);
			if (!result.ok)
				return result;
		}
		const std::string text = "gave every gating tool at level " + std::to_string(level);
		SetStatus(StatusArea::Tools, text);
		return { true, text };
	}

	// Marks a keycard collected, as the game's own setter does: in its row, else the first free one
	bool CollectKeycard(uint8_t* inventory, int id)
	{
		for (int pass = 0; pass < 2; pass++)
			for (int i = 0; i < kKeycardCount; i++)
			{
				uint8_t* at = inventory + kKeycards + i * kKeycardSize;
				int uid = 0;
				if (!Read(at, uid))
					return false;
				if (pass == 0 ? uid != id : uid != 0)
					continue;
				return Write<int>(at, id) && Write<uint8_t>(at + kKeycardCollected, 1);
			}
		return false;
	}

	DEBUG_MENU::CommandResult GiveKeycards()
	{
		uint8_t* inventory = Inventory();
		if (!inventory)
			return { false, "no player inventory (no level running)" };
		std::vector<uint8_t*> keycards(kMaxListed);
		const int count = ListEntries(kDatabaseKeycards, keycards.data(), kMaxListed);
		int given = 0;
		for (int i = 0; i < count; i++)
		{
			int id = 0;
			if (Read(keycards[i], id) && id && CollectKeycard(inventory, id))
				given++;
		}
		const std::string text = "marked " + std::to_string(given) + " keycards collected";
		SetStatus(StatusArea::Collected, text);
		return { true, text };
	}

	// Fills everything in the inventory (weapons left alone) to what can be carried
	DEBUG_MENU::CommandResult TopUp()
	{
		uint8_t* inventory = Inventory();
		if (!inventory)
			return { false, "no player inventory (no level running)" };
		if (!CanGive())
			return { false, "not available on this game release" };
		int added = 0;
		for (const Slot& slot : ReadSlots(inventory))
			if (slot.kind != kWeapon && slot.kind != kMotionTracker && slot.count < slot.carry)
			{
				const int given = GiveItem(inventory, slot.item, kFill);
				if (given > 0)
					added += given;
			}
		const std::string text = "topped up the inventory: " + std::to_string(added) + " items added";
		SetStatus(StatusArea::Inventory, text);
		return { true, text };
	}

	// Entity thread, now and then while switched on: the inventory full of torch batteries
	void KeepTorchBatteries()
	{
		uint8_t* inventory = Inventory();
		uint8_t* battery = inventory ? FindTemplate("torch_battery") : nullptr;
		int carry = 0;
		if (!battery || !CanGive() || !Read(battery + kItemCarry, carry))
			return;
		if (CountOf(inventory, battery) < carry)
			AddGuarded(inventory, battery, kFill);
	}

	// ---- Infinite items (the hook) ----

	// One more of the item in the player's hand, before the game uses one up; false when there was no room for it (at
	// the most that can be carried)
	bool AddOneMore(void* inventory, uint8_t*& held)
	{
		held = PointerAt(inventory, kHeldItem);
		if (!held || !CanGive())
			return false;
		return AddGuarded(static_cast<uint8_t*>(inventory), held, 1) == AddResult::Added;
	}

	// ---- What the tab shows (render thread) ----

	std::vector<Template> g_templates;
	const uint8_t* g_templatesFrom = nullptr;
	int g_templatesFrame = -1000;

	const std::vector<Template>& Templates()
	{
		const uint8_t* database = ItemDatabase();
		const int frame = ImGui::GetFrameCount();
		if (database != g_templatesFrom || frame - g_templatesFrame > 120)
		{
			g_templates = ReadTemplates();
			g_templatesFrom = database;
			g_templatesFrame = frame;
		}
		return g_templates;
	}

	// Entity-thread work from the window, its result shown under the section it was asked from
	void Ask(DEBUG_MENU::CommandResult (*work)(), StatusArea area)
	{
		DEBUG_MENU::OnEntityThread([work, area]()
		{
			const DEBUG_MENU::CommandResult result = work();
			if (!result.ok)
				SetStatus(area, result.message);
		});
	}

	void AskGive(const std::string& name, int count, StatusArea area = StatusArea::Give)
	{
		DEBUG_MENU::OnEntityThread([name, count, area]()
		{
			const DEBUG_MENU::CommandResult result = GiveByName(name, count, area);
			if (!result.ok)
				SetStatus(area, result.message);
		});
	}

	void AskTool(int tool, int level, bool available)
	{
		DEBUG_MENU::OnEntityThread([tool, level, available]()
		{
			const DEBUG_MENU::CommandResult result = SetTool(tool, level, available);
			if (!result.ok)
				SetStatus(StatusArea::Tools, result.message);
		});
	}

	// "name", "name,3", "name,max" (no count: a batch, as the game gives one)
	bool ParseGive(const std::string& argument, std::string& name, int& count)
	{
		const size_t comma = argument.find(',');
		name = Trim(argument.substr(0, comma));
		count = 0;
		if (comma != std::string::npos)
		{
			const std::string amount = Trim(argument.substr(comma + 1));
			if (EqualsIgnoringCase(amount, "max") || EqualsIgnoringCase(amount, "fill"))
				count = kFill;
			else
				count = atoi(amount.c_str());
			if (count <= 0)
				return false;
		}
		return !name.empty();
	}

	int ToolIndex(const std::string& name)
	{
		for (int i = 0; i < kGatingToolCount; i++)
			if (EqualsIgnoringCase(name, kTools[i].name))
				return i;
		const int index = atoi(name.c_str());
		return !name.empty() && isdigit(static_cast<unsigned char>(name[0])) && index >= 0 && index < kGatingToolCount ? index : -1;
	}

	bool g_registered = false;

	void Register()
	{
		if (g_registered)
			return;
		g_registered = true;
		DEBUG_MENU::AddSwitch("player.infinite_items", &g_infiniteItems);
		DEBUG_MENU::AddSwitch("player.torch_batteries", &g_torchBatteries);

		DEBUG_MENU::AddCommand("player.list_inventory", "the player's inventory: slot, item, kind, count/carry limit, held/selected/locked", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnEntityThread([]() -> DEBUG_MENU::CommandResult
			{
				uint8_t* inventory = Inventory();
				if (!inventory)
					return { false, "no player inventory (no level running)" };
				std::string text;
				for (const Slot& slot : ReadSlots(inventory))
				{
					char line[200];
					snprintf(line, sizeof(line), "%d=%s %s %d/%d%s%s%s\n", slot.index, slot.name.c_str(), KindName(slot.kind), slot.count, slot.carry,
						slot.held ? " held" : "", slot.selected ? " selected" : "", slot.locked ? " locked" : "");
					text += line;
				}
				return { true, text.empty() ? "(empty)" : text };
			});
		});
		DEBUG_MENU::AddCommand("player.list_items", "every item the game has: name, kind, batch, carry limit, then the name the game shows when it reads differently", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnEntityThread([]() -> DEBUG_MENU::CommandResult
			{
				std::string text;
				for (const Template& entry : ReadTemplates())
					text += entry.name + " " + KindName(entry.kind) + " " + std::to_string(entry.batch) + " " + std::to_string(entry.carry) + (entry.shown.empty() ? "" : " " + entry.shown) + "\n";
				return { !text.empty(), text.empty() ? "no item database" : text };
			});
		});
		DEBUG_MENU::AddCommand("player.list_blueprints", "the blueprints: name, available 0|1", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnEntityThread([]() -> DEBUG_MENU::CommandResult
			{
				std::vector<uint8_t*> blueprints(kMaxListed);
				const int count = ListEntries(kDatabaseBlueprints, blueprints.data(), kMaxListed);
				std::string text;
				for (int i = 0; i < count; i++)
				{
					bool available = false;
					Read(blueprints[i] + kBlueprintAvailable, available);
					text += Text(blueprints[i] + kBlueprintName, 127) + " " + (available ? "1" : "0") + "\n";
				}
				return { true, text.empty() ? "(none)" : text };
			});
		});
		DEBUG_MENU::AddCommand("player.list_gating_tools", "the gating tools: name, level, available 0|1", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnEntityThread([]() -> DEBUG_MENU::CommandResult
			{
				uint8_t* inventory = Inventory();
				if (!inventory)
					return { false, "no player inventory (no level running)" };
				std::string text;
				for (int i = 0; i < kGatingToolCount; i++)
				{
					int level = 0;
					uint8_t available = 0;
					Read(inventory + kGatingTools + i * kGatingToolSize, level);
					Read(inventory + kGatingTools + i * kGatingToolSize + kGatingToolAvailable, available);
					text += std::string(kTools[i].name) + " " + std::to_string(level) + " " + (available ? "1" : "0") + "\n";
				}
				text += "hacking_levels=" + ToolLevels();
				return { true, text };
			});
		});
		DEBUG_MENU::AddCommand("player.list_keycards", "the game's keycards: id, name, collected 0|1", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnEntityThread([]() -> DEBUG_MENU::CommandResult
			{
				uint8_t* inventory = Inventory();
				if (!inventory)
					return { false, "no player inventory (no level running)" };
				std::vector<uint8_t*> keycards(kMaxListed);
				const int count = ListEntries(kDatabaseKeycards, keycards.data(), kMaxListed);
				std::string text;
				for (int i = 0; i < count; i++)
				{
					int id = 0;
					Read(keycards[i], id);
					bool collected = false;
					for (int k = 0; k < kKeycardCount; k++)
					{
						int uid = 0;
						uint8_t done = 0;
						if (Read(inventory + kKeycards + k * kKeycardSize, uid) && uid == id && Read(inventory + kKeycards + k * kKeycardSize + kKeycardCollected, done))
							collected = done != 0;
					}
					text += std::to_string(id) + " " + TextAt(keycards[i], kKeycardEntryName) + " " + (collected ? "1" : "0") + "\n";
				}
				return { true, text.empty() ? "(none)" : text };
			});
		});
		DEBUG_MENU::AddCommand("player.weapon", "the weapon in the player's hands", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]() -> DEBUG_MENU::CommandResult
			{
				uint8_t* player = Player();
				if (!player)
					return { false, "no player" };
				const Weapon weapon = ReadWeapon(player, Inventory());
				if (!weapon.any)
					return { true, "equipped=0" };
				char text[400];
				snprintf(text, sizeof(text), "equipped=1\nitem=%s\naimed=%d\nweapon_type=%s\nammo_type=%s\nclip=%d\nclip_size=%d\ncarried=%d\noverheat=%.2f\ncharge=%.2f\nfiring=%d\ntorch=%d",
					weapon.name.c_str(), weapon.aimed ? 1 : 0, WeaponTypeName(weapon.weaponType), weapon.aimed ? AmmoTypeName(weapon.ammoType) : "-",
					weapon.clip, weapon.clipSize, weapon.carried, weapon.overheat, weapon.charge, weapon.firing ? 1 : 0, weapon.torchOn ? 1 : 0);
				return { true, text };
			});
		});
		DEBUG_MENU::AddCommand("player.give", "give the player an item: \"name[,count|max]\" (no count: a batch); the game's name, the name it shows (medikit, revolver...) or enough of either to tell", [](const std::string& argument)
		{
			std::string name;
			int count = 0;
			if (!ParseGive(argument, name, count))
				return DEBUG_MENU::CommandResult{ false, "give an item name, e.g. flare,3 or medikit,max (player.list_items lists them)" };
			return DEBUG_MENU::AnswerOnEntityThread([name, count]() { return GiveByName(name, count); });
		});
		DEBUG_MENU::AddCommand("player.give_all", "give all: medikits, flares, IEDs and crafting parts to the carry limit, and every blueprint", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnEntityThread(GiveAll);
		});
		DEBUG_MENU::AddCommand("player.give_weapons", "every weapon the game has, with its ammo", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnEntityThread(GiveWeapons);
		});
		DEBUG_MENU::AddCommand("player.give_ammo", "fill the ammo for the weapon in the player's hands (\"all\": for every weapon carried)", [](const std::string& argument)
		{
			const bool every = EqualsIgnoringCase(Trim(argument), "all");
			return DEBUG_MENU::AnswerOnEntityThread([every]() { return GiveAmmo(every); });
		});
		DEBUG_MENU::AddCommand("player.give_blueprints", "make every blueprint available", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnEntityThread(GiveBlueprints);
		});
		DEBUG_MENU::AddCommand("player.give_gating_tools", "give every gating tool: \"[level]\" (50 when no level is given)", [](const std::string& argument)
		{
			const int level = Trim(argument).empty() ? kDevelopersToolLevel : atoi(argument.c_str());
			return DEBUG_MENU::AnswerOnEntityThread([level]() { return GiveTools(level); });
		});
		DEBUG_MENU::AddCommand("player.set_gating_tool", "set one gating tool: \"name,level[,available 0|1]\" (key, axe, crowbar, gas_mask, cutting_tool, hacking_tool, rewire, cable_clamps)", [](const std::string& argument)
		{
			const size_t comma = argument.find(',');
			const int tool = ToolIndex(Trim(argument.substr(0, comma)));
			if (tool < 0 || comma == std::string::npos)
				return DEBUG_MENU::CommandResult{ false, "give a tool and a level, e.g. hacking_tool,3" };
			const std::string rest = argument.substr(comma + 1);
			const int level = atoi(rest.c_str());
			const size_t second = rest.find(',');
			const bool available = second == std::string::npos || atoi(rest.c_str() + second + 1) != 0;
			return DEBUG_MENU::AnswerOnEntityThread([tool, level, available]() { return SetTool(tool, level, available); });
		});
		DEBUG_MENU::AddCommand("player.give_keycards", "mark every keycard the game has collected", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnEntityThread(GiveKeycards);
		});
		DEBUG_MENU::AddCommand("player.top_up_items", "fill everything in the inventory (weapons left alone) to its carry limit", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnEntityThread(TopUp);
		});
	}

	// Render thread, every frame: the torch batteries, now and then while switched on
	std::atomic<bool> g_batteriesQueued = false;
	int g_batteriesFrame = 0;

	void Apply(ImDrawList* /*drawList*/)
	{
		Register();
		const int frame = ImGui::GetFrameCount();
		if (!g_torchBatteries || g_batteriesQueued || frame - g_batteriesFrame < 30)
			return;
		g_batteriesFrame = frame;
		g_batteriesQueued = true;
		DEBUG_MENU::OnEntityThread([]()
		{
			KeepTorchBatteries();
			g_batteriesQueued = false;
		});
	}

	// ---- The tab ----

	char g_filter[64] = {};
	std::string g_chosen;
	int g_count = 1;
	int g_toolLevel = kDevelopersToolLevel;

	void DrawWeapon(const uint8_t* player, const uint8_t* inventory)
	{
		ImGui::SeparatorText("Current weapon");
		const Weapon weapon = ReadWeapon(player, inventory);
		float torch = 0.0f;
		Read(inventory + kTorchPower, torch);
		if (!weapon.any)
			ImGui::TextDisabled("Nothing in the player's hands.");
		else if (!weapon.aimed)
			ImGui::Text("%s", weapon.name.empty() ? "(unnamed)" : weapon.name.c_str());
		else
		{
			ImGui::Text("%s (%s), %s ammo", weapon.name.c_str(), WeaponTypeName(weapon.weaponType), AmmoTypeName(weapon.ammoType));
			ImGui::Text("Clip %d of %d, %d more carried%s", weapon.clip, weapon.clipSize, weapon.carried, weapon.firing ? " - firing" : "");
			if (weapon.overheat > 0.0f || weapon.overheatState)
				ImGui::Text("Overheating %.2f (state %d)", weapon.overheat, weapon.overheatState);
			if (weapon.charge > 0.0f)
				ImGui::Text("Charge %.0f%%", weapon.charge * 100.0f);
		}
		ImGui::Text("Torch %s, battery %.0f%%", weapon.torchOn ? "on" : "off", torch * 100.0f);
		const bool canAmmo = CanGive() && ammo_for_weapon;
		ImGui::BeginDisabled(!canAmmo || !weapon.aimed);
		if (ImGui::Button("Fill this weapon's ammo"))
			Ask([]() { return GiveAmmo(false); }, StatusArea::Weapon);
		ImGui::EndDisabled();
		Tooltip("Every ammo type the game has for this weapon, as much as can be carried.");
		ImGui::SameLine();
		ImGui::BeginDisabled(!canAmmo);
		if (ImGui::Button("Fill ammo for every weapon carried"))
			Ask([]() { return GiveAmmo(true); }, StatusArea::Weapon);
		ImGui::EndDisabled();
		DrawStatus(StatusArea::Weapon);
	}

	void DrawInventory(const uint8_t* inventory)
	{
		ImGui::SeparatorText("Inventory");
		const std::vector<Slot> slots = ReadSlots(inventory);
		if (slots.empty())
			ImGui::TextDisabled("Empty.");
		else if (ImGui::BeginTable("##inventory", 4, ImGuiTableFlags_RowBg | ImGuiTableFlags_BordersInnerV | ImGuiTableFlags_SizingStretchProp))
		{
			ImGui::TableSetupColumn("Item", ImGuiTableColumnFlags_WidthStretch, 3.0f);
			ImGui::TableSetupColumn("Kind", ImGuiTableColumnFlags_WidthStretch, 2.0f);
			ImGui::TableSetupColumn("Count", ImGuiTableColumnFlags_WidthStretch, 1.5f);
			ImGui::TableSetupColumn("", ImGuiTableColumnFlags_WidthFixed, 60.0f * DEBUG_MENU::Scale());
			ImGui::TableHeadersRow();
			for (const Slot& slot : slots)
			{
				ImGui::PushID(slot.index);
				ImGui::TableNextRow();
				ImGui::TableNextColumn();
				ImGui::Text("%s%s%s%s", slot.name.c_str(), slot.held ? " (in hand)" : "", slot.selected ? " (selected)" : "", slot.locked ? " (locked)" : "");
				ImGui::TableNextColumn();
				ImGui::TextUnformatted(KindName(slot.kind));
				ImGui::TableNextColumn();
				ImGui::Text("%d / %d", slot.count, slot.carry);
				ImGui::TableNextColumn();
				ImGui::BeginDisabled(!CanGive() || slot.count >= slot.carry);
				if (ImGui::SmallButton("Fill"))
					AskGive(slot.name, kFill, StatusArea::Inventory);
				ImGui::EndDisabled();
				ImGui::PopID();
			}
			ImGui::EndTable();
		}
		ImGui::BeginDisabled(!CanGive());
		if (ImGui::Button("Top up everything"))
			Ask(TopUp, StatusArea::Inventory);
		ImGui::EndDisabled();
		Tooltip("Fills everything in the inventory but the weapons to the most that can be carried.");
		DrawStatus(StatusArea::Inventory);
	}

	void DrawGive()
	{
		ImGui::SeparatorText("Give");
		const std::vector<Template>& templates = Templates();
		if (templates.empty())
		{
			ImGui::TextDisabled("The game's item list isn't loaded.");
			return;
		}
		ImGui::SetNextItemWidth(200.0f * DEBUG_MENU::Scale());
		ImGui::InputTextWithHint("##filter", "find an item", g_filter, sizeof(g_filter));
		ImGui::SameLine();
		ImGui::TextDisabled("%d items", static_cast<int>(templates.size()));
		// Found by the game's name or the name it shows, ignoring case, spaces and underscores
		const std::string filter = Key(g_filter);
		// The list on the window's own dark background with a border (a list box is filled with the input fields' blue,
		// which reads as every row being selected), so the chosen row stands out
		ImGui::PushStyleColor(ImGuiCol_FrameBg, ImGui::GetStyle().Colors[ImGuiCol_WindowBg]);
		ImGui::PushStyleVar(ImGuiStyleVar_FrameBorderSize, 1.0f);
		const bool listShown = ImGui::BeginListBox("##items", ImVec2(-FLT_MIN, 8.0f * ImGui::GetTextLineHeightWithSpacing()));
		ImGui::PopStyleVar();
		ImGui::PopStyleColor();
		if (listShown)
		{
			for (const Template& entry : templates)
			{
				if (!filter.empty() && Key(entry.name).find(filter) == std::string::npos && Key(entry.shown).find(filter) == std::string::npos)
					continue;
				char label[260];
				snprintf(label, sizeof(label), "%s%s%s  (%s, carry %d)", entry.name.c_str(), entry.shown.empty() ? "" : " - ", entry.shown.c_str(), KindName(entry.kind), entry.carry);
				if (ImGui::Selectable(label, entry.name == g_chosen))
					g_chosen = entry.name;
				if (ImGui::IsItemHovered() && ImGui::IsMouseDoubleClicked(ImGuiMouseButton_Left))
					AskGive(entry.name, 0);
			}
			ImGui::EndListBox();
		}
		ImGui::BeginDisabled(!CanGive() || g_chosen.empty());
		if (ImGui::Button("Give"))
			AskGive(g_chosen, g_count);
		ImGui::SameLine();
		ImGui::SetNextItemWidth(100.0f * DEBUG_MENU::Scale());
		ImGui::InputInt("##count", &g_count);
		g_count = (std::max)(1, (std::min)(999, g_count));
		ImGui::SameLine();
		if (ImGui::Button("Fill up"))
			AskGive(g_chosen, kFill);
		ImGui::EndDisabled();
		Tooltip("As the level scripts give items: capped at the most that can be carried, and only with a free slot. Double-click an item to give a batch.");
		ImGui::BeginDisabled(!CanGive());
		if (ImGui::Button("Give all"))
			Ask(GiveAll, StatusArea::Give);
		Tooltip("Medikits, flares, every IED and every crafting part, as many as can be carried, and every blueprint.");
		ImGui::SameLine();
		if (ImGui::Button("Give every weapon"))
			Ask(GiveWeapons, StatusArea::Give);
		Tooltip("Every weapon the game's item list has, through the same add as a pickup, each with its ammo filled. Untested in game: whether each lands in the weapon wheel as a picked-up one does.");
		ImGui::EndDisabled();
		DrawStatus(StatusArea::Give);
	}

	void DrawBlueprints()
	{
		std::vector<uint8_t*> blueprints(kMaxListed);
		const int count = ListEntries(kDatabaseBlueprints, blueprints.data(), kMaxListed);
		int available = 0;
		for (int i = 0; i < count; i++)
		{
			bool on = false;
			if (Read(blueprints[i] + kBlueprintAvailable, on) && on)
				available++;
		}
		char header[96];
		snprintf(header, sizeof(header), "Blueprints: %d of %d available###blueprints", available, count);
		if (ImGui::CollapsingHeader(header))
		{
			for (int i = 0; i < count; i++)
			{
				bool on = false;
				Read(blueprints[i] + kBlueprintAvailable, on);
				ImGui::BulletText("%s%s", Text(blueprints[i] + kBlueprintName, 127).c_str(), on ? "" : " (not yet)");
			}
		}
		ImGui::BeginDisabled(!set_blueprint_available || available == count);
		if (ImGui::Button("Give every blueprint"))
			Ask(GiveBlueprints, StatusArea::Collected);
		ImGui::EndDisabled();
		Tooltip("The game's own \"make available\" on each, which also marks it collected.");
	}

	void DrawTools(const uint8_t* inventory)
	{
		ImGui::SeparatorText("Gating tools");
		const std::string levels = ToolLevels();
		if (!levels.empty())
		{
			WrappedDisabled("Hacking tool level -> hardest hack it opens: %s; any other level opens every one.", levels.c_str());
			Tooltip("The game's own table: a hack (and the level scripts' \"has access at difficulty\" checks) compares its difficulty with the hacking tool's row. Level 50 is in no row, so it opens everything.");
		}
		// Each column as wide as what it holds and the table no wider than its columns, with headers and lines between the
		// columns so a short name's row (Key) reads across to its level box
		if (ImGui::BeginTable("##tools", 3, ImGuiTableFlags_SizingFixedFit | ImGuiTableFlags_NoHostExtendX | ImGuiTableFlags_RowBg | ImGuiTableFlags_BordersInnerV))
		{
			ImGui::TableSetupColumn("Tool (ticked: carried)");
			ImGui::TableSetupColumn("Set its level");
			ImGui::TableSetupColumn("Level now");
			ImGui::TableHeadersRow();
			for (int i = 0; i < kGatingToolCount; i++)
			{
				const uint8_t* at = inventory + kGatingTools + i * kGatingToolSize;
				int level = 0;
				uint8_t has = 0;
				Read(at, level);
				Read(at + kGatingToolAvailable, has);
				ImGui::PushID(i);
				ImGui::TableNextRow();
				ImGui::TableNextColumn();
				bool available = has != 0;
				if (ImGui::Checkbox(kTools[i].label, &available))
					AskTool(i, available && level <= 0 ? 1 : level, available);
				ImGui::TableNextColumn();
				int edited = level;
				ImGui::SetNextItemWidth(110.0f * DEBUG_MENU::Scale());
				if (ImGui::InputInt("##level", &edited, 1, 10, ImGuiInputTextFlags_EnterReturnsTrue))
					AskTool(i, (std::max)(0, edited), has != 0);
				Tooltip("Type a level and press Enter, or step it with - and +.");
				ImGui::TableNextColumn();
				ImGui::TextDisabled("%d", level);
				ImGui::PopID();
			}
			ImGui::EndTable();
		}
		if (ImGui::Button("Give every tool at level"))
		{
			const int level = g_toolLevel;
			DEBUG_MENU::OnEntityThread([level]()
			{
				const DEBUG_MENU::CommandResult result = GiveTools(level);
				if (!result.ok)
					SetStatus(StatusArea::Tools, result.message);
			});
		}
		Tooltip("Gives every tool at the level beside it (50 by default). A hack's or rewire's difficulty follows the tool's level.");
		ImGui::SameLine();
		ImGui::SetNextItemWidth(110.0f * DEBUG_MENU::Scale());
		ImGui::InputInt("##toollevel", &g_toolLevel);
		g_toolLevel = (std::max)(0, (std::min)(1000, g_toolLevel));
		DrawStatus(StatusArea::Tools);
	}

	void DrawKeycards(const uint8_t* inventory)
	{
		std::vector<uint8_t*> keycards(kMaxListed);
		const int count = ListEntries(kDatabaseKeycards, keycards.data(), kMaxListed);
		int collected = 0;
		std::vector<std::string> lines;
		for (int i = 0; i < count; i++)
		{
			int id = 0;
			Read(keycards[i], id);
			bool done = false;
			for (int k = 0; k < kKeycardCount; k++)
			{
				int uid = 0;
				uint8_t flag = 0;
				if (Read(inventory + kKeycards + k * kKeycardSize, uid) && uid == id && Read(inventory + kKeycards + k * kKeycardSize + kKeycardCollected, flag))
					done = flag != 0;
			}
			collected += done ? 1 : 0;
			lines.push_back(std::to_string(id) + "  " + TextAt(keycards[i], kKeycardEntryName) + (done ? "  (collected)" : ""));
		}
		char header[96];
		snprintf(header, sizeof(header), "Keycards: %d of %d collected###keycards", collected, count);
		if (ImGui::CollapsingHeader(header))
			for (const std::string& line : lines)
				ImGui::BulletText("%s", line.c_str());
		ImGui::BeginDisabled(count == 0 || collected == count);
		if (ImGui::Button("Collect every keycard"))
			Ask(GiveKeycards, StatusArea::Collected);
		ImGui::EndDisabled();
		DrawStatus(StatusArea::Collected);
	}

	void DrawTab()
	{
		Register();
		uint8_t* player = Player();
		uint8_t* inventory = Inventory();
		if (!player || !inventory)
		{
			ImGui::TextDisabled("No player (no level running).");
			return;
		}
		bool dontWrite = false;
		if (DEBUG_MENU::GetSwitch("game.dont_write_saves", dontWrite) && !dontWrite)
		{
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextColored(ImVec4(1.0f, 0.75f, 0.3f, 1.0f), "Changes here go into the next save: turn on \"Don't write saves\" to keep them out.");
			ImGui::PopTextWrapPos();
		}
		DrawWeapon(player, inventory);
		DrawInventory(inventory);
		DrawGive();
		ImGui::SeparatorText("Blueprints and keycards");
		DrawBlueprints();
		DrawKeycards(inventory);
		DrawTools(inventory);
		ImGui::SeparatorText("Infinite items");
		ImGui::BeginDisabled(!CanGive() || !PLAYER_DEBUG::consume_held_item);
		bool infinite = g_infiniteItems;
		if (ImGui::Checkbox("Infinite items", &infinite))
			g_infiniteItems = infinite;
		ImGui::EndDisabled();
		Tooltip("Throwing, placing, using or equipping an item that gets used up (flares, IEDs, noisemakers, medikits...) leaves as many in the inventory: one more is added just before the game takes one. Untested in game.");
		ImGui::SameLine();
		bool batteries = g_torchBatteries;
		ImGui::BeginDisabled(!CanGive());
		if (ImGui::Checkbox("Never run out of torch batteries", &batteries))
			g_torchBatteries = batteries;
		ImGui::EndDisabled();
		Tooltip("Keeps the inventory full of torch batteries. The battery in the torch still drains (its power is the level's scripts'); a new one is taken from the inventory.");
	}

	const bool g_added = DEBUG_MENU::AddTab("Inventory", 35, DrawTab, "Characters") && DEBUG_MENU::AddOverlay(8, Apply);

	void AttachHooks(bool attach)
	{
		if (!PLAYER_DEBUG::consume_held_item)
			return;
		if (attach)
			DetourAttach(reinterpret_cast<void**>(&PLAYER_DEBUG::consume_held_item), reinterpret_cast<void*>(&PLAYER_DEBUG::h_consume_held_item));
		else
			DetourDetach(reinterpret_cast<void**>(&PLAYER_DEBUG::consume_held_item), reinterpret_cast<void*>(&PLAYER_DEBUG::h_consume_held_item));
	}

	const bool g_hooks = DEBUG_MENU::AddHooks(AttachHooks);
}

__declspec(noinline)
void __fastcall PLAYER_DEBUG::h_consume_held_item(void* _this, void* /*_EDX*/)
{
	if (!g_infiniteItems)
	{
		consume_held_item(_this);
		return;
	}
	// One more first, so the count never reaches nothing and the item stays selected; when there was no room for one
	// more (the most that can be carried), the one used up is put back after
	uint8_t* held = nullptr;
	const bool added = AddOneMore(_this, held);
	consume_held_item(_this);
	if (held && !added && CanGive())
		AddGuarded(static_cast<uint8_t*>(_this), held, 1);
}

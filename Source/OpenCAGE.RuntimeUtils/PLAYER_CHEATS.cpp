#include "PLAYER_DEBUG.h"
#include "DEBUG_MENU.h"

#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstdio>
#include <memory>
#include <mutex>
#include <string>
#include <vector>

#include <detours.h>
#include <imgui.h>

/*
	The Player tab's cheats and teleport sections (see PLAYER_DEBUG.h).

	- God mode and untargetable set the game's own debug bit in two of the character's per-owner flag sets: one the
	  game's damage checks read (any bit set and no damage is taken), one its choice of whom to attack reads. The bit is
	  put back every frame on the AI thread, as the game clears the whole set on some deaths and when a level script turns
	  its own invincibility off. The alien's grab kill and the facehugger don't hurt the player, they kill outright, so god
	  mode doesn't stop them; "the alien can't grab the player" sets the alien's own "prevent grapples" logic flag.
	- Infinite sprint holds the sprint's time left at its limit every frame (and clears its cooldown).
	- Health: a full heal heals by the maximum, times its modifier, less the health now; setting
	  the health uses the game's own forced set, and the kill is the game's own
	  forced kill (as its scripted deaths are), on the AI thread.
	- Teleport: the game's own way of putting a character back into the world when it falls out of it (its animation
	  repositioned, its physics body moved and woken with no velocity, its last valid positions reset, then idle), done for
	  the player at the end of the physics' pass over the characters, where the game does it.
*/
namespace
{
	// A character's per-owner flag sets: "won't take damage" (any bit: no damage) and "can't be targeted"; the
	// debug bit in each, and the level scripts' invincibility bit
	constexpr uint32_t kWontTakeDamage = 0x2E8;
	constexpr uint32_t kUntargetable = 0x2EB;
	constexpr uint8_t kDebugBit = 0x20;

	// A character's health: its health state (the object the game's health calls take), the last hit's damage and time
	// (the game notes them for its own debug display), the state (full, damaged, injured, down, dead), the health, the
	// maximum and the maximum's modifier, and whether it is on fire
	constexpr uint32_t kHealthState = 0x320;
	constexpr uint32_t kLastHitDamage = 0x320;
	constexpr uint32_t kLastHitTime = 0x324;
	constexpr uint32_t kHealthStateKind = 0x32C;
	constexpr uint32_t kHealth = 0x33C;
	constexpr uint32_t kMaxHealth = 0x33E;
	constexpr uint32_t kMaxHealthModifier = 0x340;
	constexpr uint32_t kOnFire = 0x354;

	// The sprint: its time limit, the time left, whether its cooldown has started and is running, and whether sprinting
	constexpr uint32_t kSprint = 0x3E18;
	constexpr uint32_t kSprintLimit = 0x00;
	constexpr uint32_t kSprintLeft = 0x0C;
	constexpr uint32_t kSprintCooldownPassed = 0x10;
	constexpr uint32_t kSprintCooldownActive = 0x11;
	constexpr uint32_t kSprintSprinting = 0x12;

	// Its current action (a pointer), its position, and the index of its physics controller (-1: none)
	constexpr uint32_t kCharacterAction = 0x36C;
	constexpr uint32_t kCharacterPosition = 0x70;
	constexpr uint32_t kControllerHandle = 0x60;

	// The physics controllers: one per character, each its character (a pointer) first and a byte set once it is ready
	constexpr uintptr_t kControllers = 0x01307A48;
	constexpr int kMaxControllers = 18;
	constexpr uint32_t kControllerSize = 0xB0;
	constexpr uint32_t kControllerReady = 0xA8;

	// The alien's logic flag that keeps it from grabbing the player
	constexpr int kFlagPreventGrapples = 0x1E;
	constexpr int kAlienClass = 1;

	typedef void(__thiscall* t_heal)(void* health, uint16_t amount);
	const auto heal = reinterpret_cast<t_heal>(DEVTOOLS_RELATIVE_ADDRESS(0x003BBBB0));
	typedef void(__thiscall* t_force_set_health)(void* health, uint16_t value);
	const auto force_set_health = reinterpret_cast<t_force_set_health>(DEVTOOLS_RELATIVE_ADDRESS(0x003B4B50));
	typedef void(__thiscall* t_force_kill)(void* character, const void* killer);
	const auto force_kill = reinterpret_cast<t_force_kill>(DEVTOOLS_RELATIVE_ADDRESS(0x003D0C10));
	typedef void(__thiscall* t_set_flag)(void* character, int flag, bool value);
	const auto set_flag = reinterpret_cast<t_set_flag>(DEVTOOLS_RELATIVE_ADDRESS(0x00385B10));
	typedef void(__thiscall* t_reposition)(void* controller, const float* position);
	const auto reposition_into_world = reinterpret_cast<t_reposition>(DEVTOOLS_RELATIVE_ADDRESS(0x003ECCA0));
	typedef void(__thiscall* t_action_call)(void* action);
	const auto change_to_idle = reinterpret_cast<t_action_call>(DEVTOOLS_RELATIVE_ADDRESS(0x0025BDD0));

	using namespace PLAYER_DEBUG;

	// ---- The switches ----

	std::atomic<bool> g_god = false;
	std::atomic<bool> g_untargetable = false;
	std::atomic<bool> g_sprint = false;
	std::atomic<bool> g_noGrabs = false;

	// The AI thread's own record of what it changed, to undo (ids of the characters), and whether a frame's work is queued
	std::atomic<uint32_t> g_godOn = 0;
	std::atomic<uint32_t> g_untargetableOn = 0;
	std::vector<uint32_t> g_grabsPrevented; // AI thread only
	std::atomic<bool> g_anyGrabsPrevented = false;
	std::atomic<bool> g_tickQueued = false;

	// ---- The health ----

	struct Health
	{
		bool read = false;
		uint16_t health = 0, maximum = 0;
		float modifier = 1.0f;
		int state = 0;
		bool onFire = false;
		uint16_t lastHit = 0;
		float lastHitTime = 0.0f;
		uint8_t wontTakeDamage = 0, untargetable = 0;
		float sprintLimit = 0.0f, sprintLeft = 0.0f;
		bool sprintCooldown = false, sprinting = false;
	};

	Health ReadHealth(const uint8_t* player)
	{
		Health health;
		if (!player)
			return health;
		health.read = Read(player + kHealth, health.health) && Read(player + kMaxHealth, health.maximum);
		Read(player + kMaxHealthModifier, health.modifier);
		Read(player + kHealthStateKind, health.state);
		Read(player + kOnFire, health.onFire);
		Read(player + kLastHitDamage, health.lastHit);
		Read(player + kLastHitTime, health.lastHitTime);
		Read(player + kWontTakeDamage, health.wontTakeDamage);
		Read(player + kUntargetable, health.untargetable);
		Read(player + kSprint + kSprintLimit, health.sprintLimit);
		Read(player + kSprint + kSprintLeft, health.sprintLeft);
		Read(player + kSprint + kSprintCooldownActive, health.sprintCooldown);
		Read(player + kSprint + kSprintSprinting, health.sprinting);
		if (!std::isfinite(health.modifier) || health.modifier <= 0.0f)
			health.modifier = 1.0f;
		return health;
	}

	// The most health the player can have (the maximum times its modifier, as the game's heal caps it)
	int FullHealth(const Health& health)
	{
		return static_cast<int>(health.maximum * health.modifier);
	}

	// The health state's name, as the Characters tabs show it
	const char* HealthStateName(int state)
	{
		switch (state)
		{
		case 0: return "Full";
		case 1: return "Damaged";
		case 2: return "Injured";
		case 3: return "Down but not out";
		case 4: return "Dead";
		default: return "?";
		}
	}

	// AI thread: a call on the player's health state, guarded
	enum class HealthCall { Heal, Set, Kill };

	bool CallHealth(uint8_t* player, HealthCall call, uint16_t amount)
	{
		__try
		{
			switch (call)
			{
			case HealthCall::Heal: heal(player + kHealthState, amount); break;
			case HealthCall::Set: force_set_health(player + kHealthState, amount); break;
			case HealthCall::Kill: force_kill(player, nullptr); break;
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// AI thread: heals the player fully, sets its health to a share of the most it can have, or kills it; what came of it.
	// Only the player it was asked for (its id; 0: whoever it is): the AI thread's queue waits while the game is paused,
	// and a checkpoint restart or a new level in the meantime brings a new player, which is left alone
	DEBUG_MENU::CommandResult DoHealth(HealthCall call, float percent, uint32_t expectedId)
	{
		DEBUG_MENU::CommandResult result;
		uint8_t* player = Player();
		if (expectedId && CharacterId(player) != expectedId)
			return { false, "not done: the player has changed since this was asked (checkpoint restart or a new level)" };
		const Health health = ReadHealth(player);
		if (!player || !health.read)
			return { false, "no player" };
		if (health.health == 0)
			return { false, "the player is dead" };
		const int full = FullHealth(health);
		char text[160];
		switch (call)
		{
		case HealthCall::Heal:
		{
			const int amount = full - health.health;
			if (amount <= 0)
				return { true, "the player's health is already full" };
			if (!heal || !CallHealth(player, HealthCall::Heal, static_cast<uint16_t>(amount)))
				return { false, "the heal failed" };
			snprintf(text, sizeof(text), "healed the player: %d -> %d", health.health, full);
			break;
		}
		case HealthCall::Set:
		{
			if (!std::isfinite(percent))
				return { false, "give a percentage" };
			const int value = (std::max)(1, (std::min)(full, static_cast<int>(std::lround(full * percent / 100.0f))));
			if (!force_set_health || !CallHealth(player, HealthCall::Set, static_cast<uint16_t>(value)))
				return { false, "setting the health failed" };
			snprintf(text, sizeof(text), "set the player's health: %d -> %d of %d", health.health, value, full);
			break;
		}
		case HealthCall::Kill:
		{
			// God mode's bit off first (it comes back next frame if god mode stays on; the player is dead by then)
			uint8_t flags = 0;
			Read(player + kWontTakeDamage, flags);
			Write<uint8_t>(player + kWontTakeDamage, flags & ~kDebugBit);
			Read(player + kWontTakeDamage, flags);
			if (flags != 0)
				return { false, "not killed: the level's scripts keep the player from taking damage just now" };
			if (!force_kill || !CallHealth(player, HealthCall::Kill, 0))
				return { false, "the kill failed" };
			snprintf(text, sizeof(text), "killed the player");
			break;
		}
		}
		result.message = text;
		SetStatus(StatusArea::Health, text);
		return result;
	}

	// Render thread (the window's buttons): asked for the player there now
	void AskHealth(HealthCall call, float percent)
	{
		const uint32_t id = CharacterId(Player());
		if (!id)
		{
			SetStatus(StatusArea::Health, "no player");
			return;
		}
		DEBUG_MENU::OnAiThread([call, percent, id]()
		{
			const DEBUG_MENU::CommandResult result = DoHealth(call, percent, id);
			if (!result.ok)
				SetStatus(StatusArea::Health, result.message);
		});
	}

	// A command's: the player's id read on the render thread first (into a shared block, as the work may still run after
	// a wait times out), then the call made on the AI thread for that player only
	DEBUG_MENU::CommandResult HealthCommand(HealthCall call, float percent)
	{
		const std::shared_ptr<uint32_t> id = std::make_shared<uint32_t>(0);
		const DEBUG_MENU::CommandResult who = DEBUG_MENU::AnswerOnRenderThread([id]() -> DEBUG_MENU::CommandResult
		{
			*id = CharacterId(Player());
			return *id ? DEBUG_MENU::CommandResult{ true, "" } : DEBUG_MENU::CommandResult{ false, "no player" };
		});
		if (!who.ok)
			return who;
		return DEBUG_MENU::AnswerOnAiThread([call, percent, idValue = *id]() { return DoHealth(call, percent, idValue); });
	}

	// ---- The frame's work on the AI thread ----

	// Sets the debug bit in a flag set while wanted; takes it off the character it was set on once not
	void ApplyBit(uint8_t* player, uint32_t offset, bool wanted, std::atomic<uint32_t>& appliedTo)
	{
		const uint32_t id = CharacterId(player);
		if (wanted && player && id)
		{
			// Another character is the player now (one possessed): the bit comes off the one it was set on first
			if (appliedTo && appliedTo != id)
				ApplyBit(nullptr, offset, false, appliedTo);
			uint8_t flags = 0;
			if (Read(player + offset, flags) && !(flags & kDebugBit))
				Write<uint8_t>(player + offset, flags | kDebugBit);
			appliedTo = id;
			return;
		}
		if (!wanted && appliedTo)
		{
			uint8_t* character = FindCharacter(appliedTo);
			uint8_t flags = 0;
			if (character && Read(character + offset, flags) && (flags & kDebugBit))
				Write<uint8_t>(character + offset, flags & ~kDebugBit);
			appliedTo = 0;
		}
	}

	// Holds the sprint's time left at its limit, its cooldown off
	void FillSprint(uint8_t* player)
	{
		uint8_t* sprint = player + kSprint;
		float limit = 0.0f;
		if (!Read(sprint + kSprintLimit, limit) || !std::isfinite(limit) || limit <= 0.0f)
			return;
		Write<float>(sprint + kSprintLeft, limit);
		Write<uint8_t>(sprint + kSprintCooldownPassed, 0);
		Write<uint8_t>(sprint + kSprintCooldownActive, 0);
	}

	bool SetFlag(uint8_t* character, int flag, bool value)
	{
		__try
		{
			set_flag(character, flag, value);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// Every alien gets its "prevent grapples" flag while wanted (noting those that didn't have it); once not, the flag
	// comes off those again
	void ApplyNoGrabs()
	{
		if (!set_flag)
			return;
		if (g_noGrabs)
		{
			uint8_t* characters[36];
			const int count = Characters(characters, 36);
			for (int i = 0; i < count; i++)
			{
				if (CharacterClass(characters[i]) != kAlienClass || LogicFlag(characters[i], kFlagPreventGrapples))
					continue;
				const uint32_t id = CharacterId(characters[i]);
				if (id && SetFlag(characters[i], kFlagPreventGrapples, true) && std::find(g_grabsPrevented.begin(), g_grabsPrevented.end(), id) == g_grabsPrevented.end())
					g_grabsPrevented.push_back(id);
			}
		}
		else
		{
			for (uint32_t id : g_grabsPrevented)
			{
				uint8_t* character = FindCharacter(id);
				if (character && LogicFlag(character, kFlagPreventGrapples))
					SetFlag(character, kFlagPreventGrapples, false);
			}
			g_grabsPrevented.clear();
		}
		g_anyGrabsPrevented = !g_grabsPrevented.empty();
	}

	void Tick()
	{
		uint8_t* player = Player();
		ApplyBit(player, kWontTakeDamage, g_god, g_godOn);
		ApplyBit(player, kUntargetable, g_untargetable, g_untargetableOn);
		if (g_sprint && player)
			FillSprint(player);
		ApplyNoGrabs();
		g_tickQueued = false;
	}

	// ---- Teleport ----

	// The teleport asked for (one at a time: a new one replaces one not done yet) and what came of the last
	std::mutex g_teleportMutex;
	bool g_teleportPending = false;
	float g_teleportTo[3] = {};
	uint32_t g_teleportTicket = 0;
	std::string g_teleportResult;
	std::atomic<bool> g_teleportAny = false;
	std::atomic<uint32_t> g_teleportDone = 0;
	// Where the player was before the last teleport (render thread and the pass, under the lock)
	bool g_haveBack = false;
	float g_back[3] = {};

	enum class TeleportResult { Done, NoPlayer, Dead, NoController, Failed };

	// The physics' pass: moves the player there as the game moves back a character that fell out of the world
	TeleportResult TeleportNow(const float to[3], float from[3])
	{
		uint8_t* player = Player();
		if (!player)
			return TeleportResult::NoPlayer;
		uint16_t health = 0;
		if (!Read(player + kHealth, health) || health == 0)
			return TeleportResult::Dead;
		int handle = -1;
		const uintptr_t controllers = DEVTOOLS_RELATIVE_ADDRESS(kControllers);
		if (!controllers || !Read(player + kControllerHandle, handle) || handle < 0 || handle >= kMaxControllers)
			return TeleportResult::NoController;
		uint8_t* controller = reinterpret_cast<uint8_t*>(controllers) + handle * kControllerSize;
		alignas(16) float position[4] = { to[0], to[1], to[2], 0.0f };
		__try
		{
			if (*reinterpret_cast<uint8_t**>(controller) != player || !controller[kControllerReady])
				return TeleportResult::NoController;
			for (int i = 0; i < 3; i++)
				from[i] = reinterpret_cast<const float*>(player + kCharacterPosition)[i];
			reposition_into_world(controller, position);
			uint8_t* action = *reinterpret_cast<uint8_t**>(player + kCharacterAction);
			if (action)
				change_to_idle(action);
			return TeleportResult::Done;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return TeleportResult::Failed;
		}
	}

	void DoPendingTeleport()
	{
		float to[3];
		uint32_t ticket = 0;
		{
			std::lock_guard<std::mutex> lock(g_teleportMutex);
			if (!g_teleportPending)
				return;
			g_teleportPending = false;
			g_teleportAny = false;
			for (int i = 0; i < 3; i++)
				to[i] = g_teleportTo[i];
			ticket = g_teleportTicket;
		}
		float from[3] = {};
		const TeleportResult result = TeleportNow(to, from);
		std::string text;
		switch (result)
		{
		case TeleportResult::Done: text = "teleported the player to " + Vector(to) + " (from " + Vector(from) + ")"; break;
		case TeleportResult::NoPlayer: text = "no teleport: no player"; break;
		case TeleportResult::Dead: text = "no teleport: the player is dead"; break;
		case TeleportResult::NoController: text = "no teleport: the player has no physics body just now"; break;
		default: text = "the teleport failed"; break;
		}
		{
			std::lock_guard<std::mutex> lock(g_teleportMutex);
			g_teleportResult = text;
			if (result == TeleportResult::Done)
			{
				g_haveBack = true;
				for (int i = 0; i < 3; i++)
					g_back[i] = from[i];
			}
		}
		g_teleportDone = ticket;
		SetStatus(StatusArea::Teleport, text);
	}

	uint32_t QueueTeleport(const float to[3])
	{
		std::lock_guard<std::mutex> lock(g_teleportMutex);
		for (int i = 0; i < 3; i++)
			g_teleportTo[i] = to[i];
		g_teleportPending = true;
		g_teleportTicket++;
		if (g_teleportTicket == 0)
			g_teleportTicket = 1;
		g_teleportAny = true;
		return g_teleportTicket;
	}

	bool TeleportAvailable()
	{
		return post_process_characters && reposition_into_world && change_to_idle && DEVTOOLS_RELATIVE_ADDRESS(kControllers);
	}

	// A command's teleport: asks for it and waits (up to 2 s) for the physics' pass to do it
	DEBUG_MENU::CommandResult TeleportAndWait(const float to[3])
	{
		if (!TeleportAvailable())
			return { false, "not available on this game release" };
		for (int i = 0; i < 3; i++)
			if (!std::isfinite(to[i]))
				return { false, "not a position" };
		const uint32_t ticket = QueueTeleport(to);
		const ULONGLONG until = GetTickCount64() + 2000;
		while (GetTickCount64() < until)
		{
			if (g_teleportDone == ticket)
			{
				std::lock_guard<std::mutex> lock(g_teleportMutex);
				return { g_teleportResult.rfind("teleported", 0) == 0, g_teleportResult };
			}
			Sleep(5);
		}
		return { true, "queued: teleport to " + Vector(to) + " at the next physics frame (is the game paused or a level loading?)" };
	}

	bool Back(float out[3])
	{
		std::lock_guard<std::mutex> lock(g_teleportMutex);
		if (!g_haveBack)
			return false;
		for (int i = 0; i < 3; i++)
			out[i] = g_back[i];
		return true;
	}

	// Render thread: where the crosshair points (a little above it), when the inspector's crosshair provides it
	bool CrosshairSpot(float out[3])
	{
		DEBUG_MENU::PickResult pick;
		if (!DEBUG_MENU::Pick(pick))
			return false;
		for (int i = 0; i < 3; i++)
			out[i] = pick.position[i] + pick.normal[i] * 0.05f;
		out[1] += 0.05f;
		return true;
	}

	// ---- Named switches and commands ----

	struct Spot
	{
		float position[3] = {};
	};

	bool g_registered = false;

	void Register()
	{
		if (g_registered)
			return;
		g_registered = true;
		DEBUG_MENU::AddSwitch("player.god_mode", &g_god);
		DEBUG_MENU::AddSwitch("player.untargetable", &g_untargetable);
		DEBUG_MENU::AddSwitch("player.infinite_sprint", &g_sprint);
		DEBUG_MENU::AddSwitch("player.no_grabs", &g_noGrabs);

		DEBUG_MENU::AddCommand("player.status", "the player's health, flags, sprint and position", [](const std::string&) -> DEBUG_MENU::CommandResult
		{
			return DEBUG_MENU::AnswerOnRenderThread([]() -> DEBUG_MENU::CommandResult
			{
				uint8_t* player = Player();
				const Health health = ReadHealth(player);
				if (!player || !health.read)
					return { false, "no player" };
				float position[3] = {};
				for (int i = 0; i < 3; i++)
					Read(player + kCharacterPosition + i * 4, position[i]);
				char text[512];
				snprintf(text, sizeof(text), "health=%d\nmax=%d\nstate=%s\non_fire=%d\nwont_take_damage=0x%02X\nuntargetable=0x%02X\n"
					"sprint_left=%.2f\nsprint_limit=%.2f\nsprint_cooldown=%d\nposition=%s\nid=%u",
					health.health, FullHealth(health), HealthStateName(health.state), health.onFire ? 1 : 0, health.wontTakeDamage,
					health.untargetable, health.sprintLeft, health.sprintLimit, health.sprintCooldown ? 1 : 0, Vector(position).c_str(),
					CharacterId(player));
				return { true, text };
			});
		});
		DEBUG_MENU::AddCommand("player.heal", "heal the player fully", [](const std::string&)
		{
			return HealthCommand(HealthCall::Heal, 0.0f);
		});
		DEBUG_MENU::AddCommand("player.set_health", "set the player's health: \"<percent of the most it can be>\"", [](const std::string& argument)
		{
			float percent = 0.0f;
			if (!ParseFloats(argument, &percent, 1))
				return DEBUG_MENU::CommandResult{ false, "give a percentage, e.g. 25" };
			return HealthCommand(HealthCall::Set, percent);
		});
		DEBUG_MENU::AddCommand("player.kill", "kill the player (the game's own forced kill)", [](const std::string&)
		{
			return HealthCommand(HealthCall::Kill, 0.0f);
		});
		DEBUG_MENU::AddCommand("player.teleport", "move the player: \"x,y,z\" (game world space)", [](const std::string& argument)
		{
			float to[3];
			if (!ParseFloats(argument, to, 3))
				return DEBUG_MENU::CommandResult{ false, "give a position: x,y,z" };
			return TeleportAndWait(to);
		});
		// The spot is read on the render thread into a shared block (the work may still run after a wait times out)
		DEBUG_MENU::AddCommand("player.teleport_to_camera", "move the player to where the camera is", [](const std::string&)
		{
			const std::shared_ptr<Spot> to = std::make_shared<Spot>();
			const DEBUG_MENU::CommandResult camera = DEBUG_MENU::AnswerOnRenderThread([to]() -> DEBUG_MENU::CommandResult
			{
				return DEBUG_MENU::CameraPosition(to->position) ? DEBUG_MENU::CommandResult{ true, "" } : DEBUG_MENU::CommandResult{ false, "no camera" };
			});
			return camera.ok ? TeleportAndWait(to->position) : camera;
		});
		DEBUG_MENU::AddCommand("player.teleport_to_crosshair", "move the player to where the inspector's crosshair points", [](const std::string&)
		{
			const std::shared_ptr<Spot> to = std::make_shared<Spot>();
			const DEBUG_MENU::CommandResult spot = DEBUG_MENU::AnswerOnRenderThread([to]() -> DEBUG_MENU::CommandResult
			{
				return CrosshairSpot(to->position) ? DEBUG_MENU::CommandResult{ true, "" } : DEBUG_MENU::CommandResult{ false, "nothing under the crosshair (turn on the crosshair in World > Inspect, or the game is paused)" };
			});
			return spot.ok ? TeleportAndWait(to->position) : spot;
		});
		DEBUG_MENU::AddCommand("player.teleport_back", "move the player back to where the last teleport took it from", [](const std::string&)
		{
			float to[3];
			return Back(to) ? TeleportAndWait(to) : DEBUG_MENU::CommandResult{ false, "no teleport to go back from" };
		});
	}

	// Render thread, every frame: the AI thread's work for the cheats, while any is on or has something to undo
	void Apply(ImDrawList* /*drawList*/)
	{
		Register();
		const bool wanted = g_god || g_untargetable || g_sprint || g_noGrabs || g_godOn || g_untargetableOn || g_anyGrabsPrevented;
		if (!wanted || g_tickQueued)
			return;
		g_tickQueued = true;
		DEBUG_MENU::OnAiThread(Tick);
	}

	// ---- The sections ----

	void Box(const char* label, std::atomic<bool>& value, bool available, const char* tooltip)
	{
		bool on = value;
		ImGui::BeginDisabled(!available);
		if (ImGui::Checkbox(label, &on))
			value = on;
		ImGui::EndDisabled();
		Tooltip(available ? tooltip : "Not available on this game release.");
	}

	float g_percent = 25.0f;

	// The cheats' checkboxes: our own switches, or (value null) another view's named switch
	struct Cheat
	{
		const char* label;
		std::atomic<bool>* value;
		const char* name;
		const char* tooltip;
	};

	const Cheat kCheats[] = {
		{ "God mode", &g_god, nullptr, "The player takes no damage (the game's own debug switch, kept on every frame). The alien's grab kill and the facehugger still kill: turn on \"The alien can't grab the player\" for the alien." },
		{ "Untargetable", &g_untargetable, nullptr, "The AI doesn't choose the player as a target to attack (the game's own debug switch). They can still see and hear the player; \"Invisible to the AI\" stops that." },
		{ "Invisible to the AI", nullptr, "tools.invisible", "The AI's senses no longer pick up the player. One already hunting the player keeps what it last sensed until it loses track. To keep the AI from attacking the player too, turn on Untargetable." },
		{ "The alien can't grab the player", &g_noGrabs, nullptr, "Sets every alien's \"prevent grapples\" logic flag (and takes it off again when turned off): its grab kills even in god mode." },
		{ "Infinite sprint", &g_sprint, nullptr, "The sprint never runs out (its time left is held at its limit every frame)." },
		{ "Infinite ammo", nullptr, "tools.infinite_ammo", "The player's weapons use no ammo from the clip when fired (turn it on with ammo in the clip). Thrown items: Infinite items." },
		{ "Infinite items", nullptr, "player.infinite_items", "Throwing, placing or using an item (flares, IEDs, medikits...) leaves as many in the inventory (Inventory tab)." },
		{ "Never run out of torch batteries", nullptr, "player.torch_batteries", "Keeps the inventory full of torch batteries (Inventory tab). The battery in the torch still drains: that is the level's scripts." } };

	void DrawCheats()
	{
		Register();
		uint8_t* player = Player();
		const Health health = ReadHealth(player);
		ImGui::SeparatorText("Cheats");
		if (!player || !health.read)
		{
			ImGui::TextDisabled("No player (no level running).");
			return;
		}
		// In as many columns (up to three) as fit across the window, each as wide as the longest label, so none is cut off
		const ImGuiStyle& style = ImGui::GetStyle();
		float widest = 0.0f;
		for (const Cheat& cheat : kCheats)
			widest = (std::max)(widest, ImGui::CalcTextSize(cheat.label).x);
		const float cell = ImGui::GetFrameHeight() + style.ItemInnerSpacing.x + widest + style.CellPadding.x * 2.0f + style.ItemSpacing.x;
		const int columns = (std::max)(1, (std::min)(3, static_cast<int>(ImGui::GetContentRegionAvail().x / cell)));
		if (ImGui::BeginTable("##cheats", columns, ImGuiTableFlags_SizingStretchSame))
		{
			for (const Cheat& cheat : kCheats)
			{
				ImGui::TableNextColumn();
				if (cheat.value)
					Box(cheat.label, *cheat.value, cheat.value != &g_noGrabs || set_flag != nullptr, cheat.tooltip);
				else
					SwitchBox(cheat.label, cheat.name, cheat.tooltip);
			}
			ImGui::EndTable();
		}

		// Health
		const int full = FullHealth(health);
		char text[128];
		snprintf(text, sizeof(text), "%d of %d (%s)", health.health, full, HealthStateName(health.state));
		ImGui::AlignTextToFramePadding();
		ImGui::TextUnformatted("Health");
		ImGui::SameLine();
		ImGui::PushStyleColor(ImGuiCol_PlotHistogram, health.health * 3 < full ? IM_COL32(220, 60, 40, 255) : IM_COL32(80, 190, 90, 255));
		ImGui::ProgressBar(full > 0 ? static_cast<float>(health.health) / full : 0.0f, ImVec2(260.0f * DEBUG_MENU::Scale(), 0.0f), text);
		ImGui::PopStyleColor();
		if (health.maximum != full)
		{
			ImGui::SameLine();
			ImGui::TextDisabled("(maximum %d x %.2f)", health.maximum, health.modifier);
		}
		if (health.onFire)
		{
			ImGui::SameLine();
			ImGui::TextColored(ImVec4(1.0f, 0.5f, 0.1f, 1.0f), "on fire");
		}
		if (health.wontTakeDamage)
			ImGui::TextDisabled("Takes no damage: %s%s", (health.wontTakeDamage & kDebugBit) ? "god mode" : "", (health.wontTakeDamage & ~kDebugBit) ? ((health.wontTakeDamage & kDebugBit) ? " and the level's scripts" : "the level's scripts") : "");
		if (health.lastHit)
			ImGui::TextDisabled("Last hit: %u damage at %.1f s", health.lastHit, health.lastHitTime);

		const bool dead = health.health == 0;
		ImGui::BeginDisabled(dead);
		ImGui::BeginDisabled(!heal);
		if (ImGui::Button("Full heal"))
			AskHealth(HealthCall::Heal, 0.0f);
		ImGui::EndDisabled();
		Tooltip("Heals the player to the most health it can have.");
		ImGui::SameLine();
		ImGui::BeginDisabled(!force_set_health);
		if (ImGui::Button("Set health to"))
			AskHealth(HealthCall::Set, g_percent);
		ImGui::EndDisabled();
		Tooltip("Sets the player's health with the game's own forced set: handy for checking injured animations and medikits.");
		ImGui::SameLine();
		ImGui::SetNextItemWidth(120.0f * DEBUG_MENU::Scale());
		ImGui::SliderFloat("##percent", &g_percent, 1.0f, 100.0f, "%.0f%%", ImGuiSliderFlags_AlwaysClamp);
		ImGui::SameLine();
		ImGui::BeginDisabled(!force_kill);
		if (ImGui::Button("Kill the player"))
			AskHealth(HealthCall::Kill, 0.0f);
		ImGui::EndDisabled();
		Tooltip("The game's own forced kill (as its scripted deaths use): the death and game-over flow follows. God mode is lifted for it.");
		ImGui::EndDisabled();
		DrawStatus(StatusArea::Health);

		// Sprint
		if (health.sprintLimit > 0.0f)
		{
			if (health.sprintCooldown)
				ImGui::Text("Sprint: exhausted, cooling down (%.1f s of %.1f s)", health.sprintLeft, health.sprintLimit);
			else
				ImGui::Text("Sprint: %.1f s of %.1f s left%s", health.sprintLeft, health.sprintLimit, health.sprinting ? ", sprinting" : "");
		}
	}

	float g_to[3] = {};
	bool g_toSet = false;

	void DrawTeleport()
	{
		Register();
		uint8_t* player = Player();
		ImGui::SeparatorText("Teleport");
		if (!player)
		{
			ImGui::TextDisabled("No player (no level running).");
			return;
		}
		if (!TeleportAvailable())
		{
			ImGui::TextDisabled("Not available on this game release.");
			return;
		}
		float position[3] = {};
		for (int i = 0; i < 3; i++)
			Read(player + kCharacterPosition + i * 4, position[i]);
		if (!g_toSet)
		{
			for (int i = 0; i < 3; i++)
				g_to[i] = position[i];
			g_toSet = true;
		}
		ImGui::Text("The player is at %.2f, %.2f, %.2f", position[0], position[1], position[2]);
		ImGui::SameLine();
		if (ImGui::SmallButton("Copy"))
		{
			for (int i = 0; i < 3; i++)
				g_to[i] = position[i];
			ImGui::SetClipboardText(Vector(position).c_str());
		}
		Tooltip("Puts the player's position in the box below and on the clipboard.");
		ImGui::SetNextItemWidth(300.0f * DEBUG_MENU::Scale());
		ImGui::InputFloat3("##to", g_to, "%.2f");
		ImGui::SameLine();
		if (ImGui::Button("Teleport"))
			QueueTeleport(g_to);
		Tooltip("Moves the player there, as the game moves back a character that fell out of the world (done at the end of the next physics frame). Somewhere without loaded floor, the player falls and the game puts them back where they last stood.");

		float to[3];
		const bool haveCamera = DEBUG_MENU::CameraPosition(to);
		ImGui::BeginDisabled(!haveCamera);
		if (ImGui::Button("To the camera") && haveCamera)
			QueueTeleport(to);
		ImGui::EndDisabled();
		Tooltip("Moves the player to where the camera is (fly the free camera there first). The player's feet go to the camera, so they drop the height of their eyes.");
		ImGui::SameLine();
		const bool haveCrosshair = CrosshairSpot(to);
		ImGui::BeginDisabled(!haveCrosshair);
		if (ImGui::Button("To the crosshair") && haveCrosshair)
			QueueTeleport(to);
		ImGui::EndDisabled();
		Tooltip(haveCrosshair ? "Moves the player to the point the crosshair is on." : "Nothing under the crosshair: turn on the crosshair in World > Inspect, or the game is paused. Then this moves the player to the point it is on.");
		ImGui::SameLine();
		float back[3];
		const bool haveBack = Back(back);
		ImGui::BeginDisabled(!haveBack);
		if (ImGui::Button("Back") && haveBack)
			QueueTeleport(back);
		ImGui::EndDisabled();
		Tooltip("Moves the player back to where the last teleport took them from.");
		DrawStatus(StatusArea::Teleport);
	}

	const bool g_added = DEBUG_MENU::AddTab("Player", 28, DrawCheats, "Characters") && DEBUG_MENU::AddTab("Player", 29, DrawTeleport, "Characters") &&
		DEBUG_MENU::AddOverlay(7, Apply);

	void AttachHooks(bool attach)
	{
		if (!PLAYER_DEBUG::post_process_characters)
			return;
		if (attach)
			DetourAttach(reinterpret_cast<void**>(&PLAYER_DEBUG::post_process_characters), reinterpret_cast<void*>(&PLAYER_DEBUG::h_post_process_characters));
		else
			DetourDetach(reinterpret_cast<void**>(&PLAYER_DEBUG::post_process_characters), reinterpret_cast<void*>(&PLAYER_DEBUG::h_post_process_characters));
	}

	const bool g_hooks = DEBUG_MENU::AddHooks(AttachHooks);
}

__declspec(noinline)
void __cdecl PLAYER_DEBUG::h_post_process_characters()
{
	post_process_characters();
	if (g_teleportAny)
		DoPendingTeleport();
}

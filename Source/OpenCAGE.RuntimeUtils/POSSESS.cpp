#include "POSSESS.h"
#include "LIVE_LINK_SERVER.h"
#include "ZONE_LOADER.h"
#include "Config.h"

#include <algorithm>
#include <atomic>
#include <cctype>
#include <cfloat>
#include <clocale>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <mutex>
#include <vector>

#include <imgui.h>

extern IMGUI_IMPL_API LRESULT ImGui_ImplWin32_WndProcHandler(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam);

/*
	Layouts are for the Steam retail AI.exe. The move-in-direction order is filled in as CA's debug behaviour that moves a
	character with the gamepad fills it in (VA 0x0040E580): created on the character's order layer, queued in place of
	whatever was queued, then given a heading, a quarter turn and a distance ahead, flagged as changed - and the speed set
	on the character's locomotion. That behaviour only runs under a debug camera retail never turns on. The crouch order
	is filled in as the scripted idle command fills it in (VA 0x006853B8): the stance it wants, then queued.
*/
namespace
{
	// Where the game keeps its pointer to its globals, and in them its character manager (a pointer)
	constexpr uintptr_t kGameGlobals = 0x012F0C88;
	constexpr uint32_t kGlobalsCharacters = 0x08;
	// The character manager's list of the characters it runs that aren't players: a pointer to their pointers, then how many
	constexpr uint32_t kManagerNpcs = 0xD0;
	constexpr uint32_t kManagerNpcCount = 0xD4;

	// A character's fields
	constexpr uint32_t kCharacterName = 0x10;         // its debug name (64 characters)
	constexpr uint32_t kNameLength = 0x40;
	constexpr uint32_t kCharacterSpawnedAs = 0x64;    // what it was spawned as (a pointer): its class at +4
	constexpr uint32_t kSpawnedAsClass = 0x04;
	constexpr uint32_t kCharacterPosition = 0x70;     // where it is: x, y, z
	constexpr uint32_t kCharacterFacing = 0x174;      // the way it faces: a yaw in radians, forward = (sin, 0, cos)
	constexpr uint32_t kCharacterCrouched = 0x2AF;    // set while it crouches (as the behaviour trees' crouch test reads it)
	constexpr uint32_t kCharacterOrders = 0x2CD0;     // the layer that carries out its orders
	constexpr uint32_t kCharacterLocomotion = 0x42A8; // its locomotion (a pointer)
	// Its locomotion state: what scripts let it do (walk, sprint, crouch, aim, injured, in a space suit - a byte each), then
	// what they make it do, then what it does. The game's crouch only crouches a character its scripts let crouch (a
	// script's locomotion modifier sets that), so the crouch byte of the first is set while it is crouched by C.
	constexpr uint32_t kCharacterCanCrouch = 0x3E2C + 0x2;

	// The behaviour update's own field: the character it is for
	constexpr uint32_t kBehaviourCharacter = 0x128;

	// The order layer's fields: the state it is in (0 starting, 1 idle, 2 moving, 3 waiting, 4 in cover, 5 an action,
	// 6 player control, 7 changing stance, 8 teleporting, 9 dead, 10 aborting) and the one it goes to next
	constexpr uint32_t kOrdersState = 0x20;
	constexpr uint32_t kOrdersNextState = 0xE4;
	// The locomotion's own: how it is moving (0 still, 1 immovable, 2 on a path, 3 starting one, 4/5 driven by animation,
	// 6 stopping a path, 7 along a direction, 8 starting one, 9 stopping one), its speed now (0..3) and how long it has
	// gone without getting anywhere (s)
	constexpr uint32_t kLocomotionStatus = 0x08;
	constexpr uint32_t kLocomotionSpeedNow = 0x198;
	constexpr uint32_t kLocomotionStuckFor = 0x1A8;
	// Set while the character is backstage: the alien in the vents, moving through them as the game's own abstract map of
	// them, not the world - no order to move in a direction takes it anywhere there, so it is not taken then
	constexpr uint32_t kLocomotionBackstage = 0x1E3;

	// An order's fields
	constexpr uint32_t kOrderStatus = 0x0C;   // 3 to 6 once it is over, one way or another
	constexpr uint32_t kOrderType = 0x10;     // which order it is
	constexpr uint32_t kOrderGiven = 0x18;    // set by whoever gives it
	constexpr uint32_t kOrderHeading = 0x1C;  // the world yaw to move along (radians, -pi..pi)
	constexpr uint32_t kOrderTurn = 0x20;     // a quarter turn, as CA's gamepad behaviour sets it
	constexpr uint32_t kOrderDistance = 0x24; // how much further to move (m)
	constexpr uint32_t kOrderChanged = 0x34;  // set when the fields above change
	constexpr int kMoveInDirection = 9;
	// A crouch order's: the stance it wants (1 standing, 2 crouched)
	constexpr uint32_t kStanceWanted = 0x1C;
	constexpr int kStanceChange = 7;
	constexpr int kStanding = 1, kCrouched = 2;
	// The locomotion's fields
	constexpr uint32_t kLocomotionSpeed = 0x194;     // how fast to move: 0 (slowest) to 3
	constexpr uint32_t kLocomotionSpeedFrom = 0x190; // set to 0 with it

	// While a direction is held the order runs 10 m ahead; let go, it stops within 2 m
	constexpr float kAheadWhileHeld = 10.0f;
	constexpr float kAheadToStop = 2.0f;
	constexpr float kQuarterTurn = 1.5707964f;
	constexpr float kPi = 3.14159265f;
	// A crouch order that has not finished after this long is taken back, and the character moves on as it is
	constexpr ULONGLONG kStanceGiveUpMs = 3000;

	// The camera behind the character
	constexpr float kCameraDistance = 4.0f; // how far behind it at first; the mouse wheel moves it from nearest to furthest
	constexpr float kCameraNearest = 1.5f, kCameraFurthest = 10.0f;
	constexpr float kWheelStep = 0.5f;      // metres a notch of the mouse wheel
	constexpr float kCameraHeight = 1.3f;   // above the character's feet, where the camera looks
	constexpr float kCameraPitch = -0.25f;  // looking a little down at first
	constexpr float kPitchLowest = -1.2f, kPitchHighest = 0.6f;
	constexpr float kMouseTurn = 0.0025f;   // radians a mouse count

	// How often the characters are listed (the menu, POSSESS_GET, and whether the possessed one is still there)
	constexpr ULONGLONG kListEveryMs = 250;
	constexpr int kMaxCharacters = 256;

	// The running level's root composite id, as LIVE_LINK.cpp reads it
	constexpr uintptr_t kEntityManagerInstance = 0x0134EF40;
	constexpr uint32_t kEntityManagerRootGuid = 0x0C;

	// A shared pointer as the game keeps one: the object, then its counts - the strong count at +4 and the weak at +8,
	// dropped through the counts' own table (its first entry frees the object, its second the counts)
	struct Shared
	{
		uint8_t* object = nullptr;
		uint8_t* counts = nullptr;
	};

	typedef Shared*(__thiscall* t_create_order)(void* orders, Shared* out);
	const auto create_move_order = reinterpret_cast<t_create_order>(DEVTOOLS_RELATIVE_ADDRESS(0x002823B0));
	const auto create_stance_order = reinterpret_cast<t_create_order>(DEVTOOLS_RELATIVE_ADDRESS(0x002823F0));
	typedef bool(__thiscall* t_queue_order)(void* order); // queued in place of whatever was queued
	const auto queue_order = reinterpret_cast<t_queue_order>(DEVTOOLS_RELATIVE_ADDRESS(0x00283CE0));
	typedef void(__thiscall* t_withdraw_order)(void* order);
	const auto withdraw_order = reinterpret_cast<t_withdraw_order>(DEVTOOLS_RELATIVE_ADDRESS(0x00283DB0));
	typedef void(__thiscall* t_counts_entry)(void* counts);

	// What was asked for (the socket thread, the menu), acted on by the next behaviour update: under g_mutex, with its
	// generation also in g_wishGeneration so the behaviour updates can see a new one without the lock
	std::mutex g_mutex;
	struct Wish
	{
		bool take = false;
		uint8_t* character = nullptr; // this one (from the menu), rather than one by name
		bool nearest = false;         // the character nearest the middle of the view, rather than one by name
		std::string name;             // in upper case
	};
	Wish g_wish;
	std::atomic<uint32_t> g_wishGeneration = 0;

	// A stick from OpenCAGE, used in place of the keys (under g_mutex)
	struct Stick
	{
		bool set = false;
		uint32_t connection = 0;
		float x = 0.0f, y = 0.0f;
		int speed = 2;
		ULONGLONG until = 0; // 0: until another comes
	};
	Stick g_stick;

	// Whether the possessed character should be crouched (C, OpenCAGE): -1 as it is, 0 standing, 1 crouched
	std::atomic<int> g_crouchWanted = -1;

	struct Listed
	{
		uint8_t* character; // only ever compared, or handed back to the behaviour updates to look for
		char name[kNameLength + 1];
		float position[3];
		int characterClass; // see ClassName
		bool backstage;     // in the vents: not to be taken
	};

	// A character's class, as the menu shows it (the game's own list: the player, the alien, an android...)
	const char* ClassName(int characterClass)
	{
		switch (characterClass)
		{
		case 0: return "Player";
		case 1: return "Alien";
		case 2: return "Android";
		case 3: return "Human";
		case 4: return "Security";
		case 5: return "Facehugger";
		case 6: return "Innocent";
		case 7: return "Heavy android";
		case 8: return "Tracker";
		case 9: return "Human (melee)";
		default: return "";
		}
	}

	// What the last frame did, for the menu, POSSESS_GET, the camera and the overlay (under g_mutex)
	struct Seen
	{
		bool possessing = false;
		uint8_t* character = nullptr;
		char name[kNameLength + 1] = {};
		float position[3] = {};
		float facing = 0.0f;
		float heading = 0.0f;
		int speed = 0;
		bool moving = false;
		bool fromStick = false;
		bool crouched = false;
		uint32_t frames = 0;
		ULONGLONG at = 0;
		// How the orders are going: the order's status (0 made, 1 queued, 2 carried out, 3 done, 4 failed, 5 interrupted,
		// 6 discarded; -1 none), how many have been given, the order layer's state, and the locomotion's
		int orderStatus = -1;
		uint32_t ordersGiven = 0;
		int layerState = -1, layerNext = -1;
		int walkStatus = -1, speedNow = -1;
		float stuckFor = 0.0f;
		std::string letGo; // why the last character was let go of, or why none was taken
		std::vector<Listed> characters;
	};
	Seen g_seen;

	// The behaviour updates' own (their thread only)
	uint8_t* g_character = nullptr; // the possessed character
	uint32_t g_root = 0;            // the level it was taken in
	uint32_t g_handled = 0;         // the wish generation acted on
	Shared g_order;                 // the move order it was last given: kept, to update it and take it back
	bool g_stopping = false;        // the order was cut short to stop: a direction held again gets a fresh one
	Shared g_stanceOrder;           // the crouch order it is carrying out, if any
	ULONGLONG g_stanceAt = 0;
	int g_crouchGiven = -1;         // the stance last asked of it: 0 standing, 1 crouched (-1: none yet)
	bool g_crouchKeyDown = false;
	int g_canCrouchWas = -1;        // what its scripts let it do about crouching before C did (-1: untouched)
	ULONGLONG g_listedAt = 0;
	ULONGLONG g_loggedAt = 0;
	thread_local bool t_inPossessed = false; // inside the possessed character's behaviour update
	uint32_t g_ordersGiven = 0;

	std::atomic<bool> g_possessing = false;
	std::atomic<HWND> g_window = nullptr;

	// The menu: whether it is open (any thread asks, the window thread follows with the cursor), and the window thread's
	// own record of what it did with the cursor for it
	std::atomic<bool> g_menuOpen = false;
	bool g_menuCursor = false;
	bool g_menuClipped = false;
	RECT g_menuClip = {};

	// Where the frame is drawn from (the camera task), under g_viewMutex
	std::mutex g_viewMutex;
	float g_viewPosition[3] = {};
	float g_viewForward[3] = { 0.0f, 0.0f, 1.0f };
	bool g_viewKnown = false;
	// The camera behind the character: its turn (the mouse), and whether it drew the last frame
	std::atomic<float> g_orbitYaw = 0.0f;
	std::atomic<float> g_orbitPitch = kCameraPitch;
	std::atomic<float> g_orbitDistance = kCameraDistance; // kept from one character to the next
	std::atomic<bool> g_behind = false;

	float Wrap(float angle)
	{
		while (angle > kPi) angle -= 2.0f * kPi;
		while (angle <= -kPi) angle += 2.0f * kPi;
		return angle;
	}

	std::string Upper(const std::string& text)
	{
		std::string upper;
		for (char c : text)
			upper += static_cast<char>(toupper(static_cast<unsigned char>(c)));
		return upper;
	}

	// Plain reads of the entity manager, guarded: it can go while a level unloads
	uint32_t RunningRoot()
	{
		__try
		{
			if (!LIVE_LINK::LevelRunning())
				return 0;
			const uint8_t* manager = *reinterpret_cast<uint8_t**>(DEVTOOLS_RELATIVE_ADDRESS(kEntityManagerInstance));
			return *reinterpret_cast<const uint32_t*>(manager + kEntityManagerRootGuid);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	enum class Level { None, Other, Same };
	Level RunningLevel(uint32_t root)
	{
		__try
		{
			if (!LIVE_LINK::LevelRunning())
				return Level::None;
			return LIVE_LINK::RunningLevelIs(root) ? Level::Same : Level::Other;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return Level::None;
		}
	}

	// The characters the game runs now that aren't players (up to max), guarded: the lists change between levels
	int Characters(uint8_t** out, int max)
	{
		__try
		{
			const uint8_t* globals = *reinterpret_cast<uint8_t**>(DEVTOOLS_RELATIVE_ADDRESS(kGameGlobals));
			if (!globals)
				return 0;
			const uint8_t* manager = *reinterpret_cast<uint8_t* const*>(globals + kGlobalsCharacters);
			if (!manager)
				return 0;
			uint8_t* const* list = *reinterpret_cast<uint8_t* const* const*>(manager + kManagerNpcs);
			const uint32_t count = *reinterpret_cast<const uint32_t*>(manager + kManagerNpcCount);
			int found = 0;
			for (uint32_t i = 0; list && i < count && found < max; i++)
				if (list[i])
					out[found++] = list[i];
			return found;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	bool ReadCharacter(const uint8_t* character, char name[kNameLength + 1], float position[3], float* facing)
	{
		__try
		{
			memcpy(name, character + kCharacterName, kNameLength);
			name[kNameLength] = '\0';
			memcpy(position, character + kCharacterPosition, sizeof(float) * 3);
			if (facing)
				*facing = *reinterpret_cast<const float*>(character + kCharacterFacing);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	int ReadClass(const uint8_t* character)
	{
		__try
		{
			const uint8_t* spawnedAs = *reinterpret_cast<uint8_t* const*>(character + kCharacterSpawnedAs);
			return spawnedAs ? *reinterpret_cast<const int*>(spawnedAs + kSpawnedAsClass) : -1;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return -1;
		}
	}

	bool ReadBackstage(const uint8_t* character)
	{
		__try
		{
			const uint8_t* locomotion = *reinterpret_cast<uint8_t* const*>(character + kCharacterLocomotion);
			return locomotion && locomotion[kLocomotionBackstage] != 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool ReadCrouched(const uint8_t* character)
	{
		__try
		{
			return character[kCharacterCrouched] != 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// How the orders are going (see Seen), guarded
	struct OrdersSeen
	{
		int status = -1, state = -1, next = -1, walk = -1, speedNow = -1;
		float stuckFor = 0.0f;
	};

	void ReadOrders(const uint8_t* character, const uint8_t* order, OrdersSeen& seen)
	{
		__try
		{
			seen.status = order ? *reinterpret_cast<const int*>(order + kOrderStatus) : -1;
			const uint8_t* orders = character + kCharacterOrders;
			seen.state = *reinterpret_cast<const int*>(orders + kOrdersState);
			seen.next = *reinterpret_cast<const int*>(orders + kOrdersNextState);
			const uint8_t* locomotion = *reinterpret_cast<uint8_t* const*>(character + kCharacterLocomotion);
			if (locomotion)
			{
				seen.walk = *reinterpret_cast<const int*>(locomotion + kLocomotionStatus);
				seen.speedNow = *reinterpret_cast<const int*>(locomotion + kLocomotionSpeedNow);
				seen.stuckFor = *reinterpret_cast<const float*>(locomotion + kLocomotionStuckFor);
			}
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			seen = OrdersSeen();
		}
	}

	uint8_t* CharacterOf(void* behaviour)
	{
		__try
		{
			return *reinterpret_cast<uint8_t**>(static_cast<uint8_t*>(behaviour) + kBehaviourCharacter);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	// Lets go of a shared pointer as the game does
	void Drop(Shared& shared)
	{
		uint8_t* counts = shared.counts;
		shared = Shared();
		if (!counts)
			return;
		if (InterlockedDecrement(reinterpret_cast<volatile LONG*>(counts + 4)) == 0)
		{
			void** table = *reinterpret_cast<void***>(counts);
			reinterpret_cast<t_counts_entry>(table[0])(counts);
			if (InterlockedDecrement(reinterpret_cast<volatile LONG*>(counts + 8)) == 0)
				reinterpret_cast<t_counts_entry>(table[1])(counts);
		}
	}

	bool OrderOver(const uint8_t* order)
	{
		const int status = *reinterpret_cast<const int*>(order + kOrderStatus);
		return status >= 3 && status <= 6;
	}

	// Gives the character a move-in-direction order, or updates the one it has. False if the game would not have one.
	bool Steer(uint8_t* character, float heading, int speed)
	{
		__try
		{
			if (g_order.object && (*reinterpret_cast<const int*>(g_order.object + kOrderType) != kMoveInDirection || OrderOver(g_order.object)))
				Drop(g_order);
			if (!g_order.object)
			{
				Shared fresh;
				create_move_order(character + kCharacterOrders, &fresh);
				if (!fresh.object || *reinterpret_cast<const int*>(fresh.object + kOrderType) != kMoveInDirection)
				{
					Drop(fresh);
					return false;
				}
				queue_order(fresh.object);
				fresh.object[kOrderGiven] = 1;
				g_order = fresh;
				g_ordersGiven++;
			}
			*reinterpret_cast<float*>(g_order.object + kOrderHeading) = heading;
			*reinterpret_cast<float*>(g_order.object + kOrderTurn) = kQuarterTurn;
			*reinterpret_cast<float*>(g_order.object + kOrderDistance) = kAheadWhileHeld;
			g_order.object[kOrderChanged] = 1;
			uint8_t* locomotion = *reinterpret_cast<uint8_t**>(character + kCharacterLocomotion);
			if (locomotion)
			{
				*reinterpret_cast<int*>(locomotion + kLocomotionSpeed) = speed;
				*reinterpret_cast<int*>(locomotion + kLocomotionSpeedFrom) = 0;
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			g_order = Shared();
			return false;
		}
	}

	// No direction held: the order is cut short, so the character comes to a stop
	void Coast()
	{
		__try
		{
			if (!g_order.object || OrderOver(g_order.object))
				return;
			float& ahead = *reinterpret_cast<float*>(g_order.object + kOrderDistance);
			if (ahead > kAheadToStop)
			{
				ahead = kAheadToStop;
				g_order.object[kOrderChanged] = 1;
			}
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			g_order = Shared();
		}
	}

	// An order is taken back (the behaviour tree gives its own again) - or only forgotten, when the character or its level
	// has gone, and the order's memory with it
	void Withdraw(Shared& order, bool gone)
	{
		if (gone)
		{
			order = Shared();
			return;
		}
		__try
		{
			if (order.object && !OrderOver(order.object))
				withdraw_order(order.object);
			Drop(order);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			order = Shared();
		}
	}

	// Gives the character a crouch order (or a stand one), in place of whatever it was doing - the move order with it.
	// False if the game would not have one.
	bool Crouch(uint8_t* character, bool crouched)
	{
		__try
		{
			Shared fresh;
			create_stance_order(character + kCharacterOrders, &fresh);
			if (!fresh.object || *reinterpret_cast<const int*>(fresh.object + kOrderType) != kStanceChange)
			{
				Drop(fresh);
				return false;
			}
			*reinterpret_cast<int*>(fresh.object + kStanceWanted) = crouched ? kCrouched : kStanding;
			queue_order(fresh.object);
			Drop(g_order);
			g_stanceOrder = fresh;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			g_stanceOrder = Shared();
			return false;
		}
	}

	// Lets the character crouch whatever its scripts say (kept from before, to put back), or puts back what they said
	void AllowCrouch(uint8_t* character, bool allow)
	{
		__try
		{
			if (allow)
			{
				if (g_canCrouchWas < 0)
					g_canCrouchWas = character[kCharacterCanCrouch];
				character[kCharacterCanCrouch] = 1;
			}
			else if (g_canCrouchWas >= 0)
			{
				character[kCharacterCanCrouch] = static_cast<uint8_t>(g_canCrouchWas);
				g_canCrouchWas = -1;
			}
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			g_canCrouchWas = -1;
		}
	}

	// Whether the crouch order is still being carried out
	bool StanceUnderway()
	{
		__try
		{
			return g_stanceOrder.object && !OrderOver(g_stanceOrder.object);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			g_stanceOrder = Shared();
			return false;
		}
	}

	void CloseMenu()
	{
		if (!g_menuOpen.exchange(false))
			return;
		// The window thread gives the cursor back to the game on its next message: one is sent now
		const HWND window = g_window;
		if (window)
			PostMessage(window, WM_NULL, 0, 0);
	}

	void OpenMenu()
	{
		if (g_menuOpen.exchange(true))
			return;
		const HWND window = g_window;
		if (window)
			PostMessage(window, WM_NULL, 0, 0);
	}

	// The window thread: the cursor shown and freed while the menu is open (the game pins it to the middle of its window
	// and hides it while a level is played), and given back as it was once the menu closes. The cursor is only moved or
	// freed while the game window is in front (the menu opened from OpenCAGE leaves it alone where it is), and freed
	// again if the game pins it while the menu is open (its window brought to the front).
	void FollowMenuCursor(HWND window)
	{
		const bool open = g_menuOpen;
		const bool inFront = GetForegroundWindow() == window;
		if (open == g_menuCursor)
		{
			RECT clip;
			if (open && inFront && GetClipCursor(&clip) && clip.right - clip.left <= 2 && clip.bottom - clip.top <= 2)
			{
				g_menuClip = clip;
				g_menuClipped = true;
				ClipCursor(nullptr);
			}
			return;
		}
		g_menuCursor = open;
		if (open)
		{
			g_menuClipped = false;
			ShowCursor(TRUE);
			if (inFront)
			{
				g_menuClipped = GetClipCursor(&g_menuClip) != FALSE;
				ClipCursor(nullptr);
				RECT client;
				if (GetClientRect(window, &client))
				{
					POINT middle = { (client.left + client.right) / 2, (client.top + client.bottom) / 2 };
					ClientToScreen(window, &middle);
					SetCursorPos(middle.x, middle.y);
				}
			}
		}
		else
		{
			ShowCursor(FALSE);
			if (g_menuClipped && GetForegroundWindow() == window)
				ClipCursor(&g_menuClip);
			g_menuClipped = false;
		}
	}

	void LetGo(const char* why, bool gone)
	{
		if (!g_character)
			return;
		Withdraw(g_order, gone);
		Withdraw(g_stanceOrder, gone);
		if (!gone)
			AllowCrouch(g_character, false);
		g_canCrouchWas = -1;
		g_stopping = false;
		g_character = nullptr;
		g_possessing = false;
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			g_seen.possessing = false;
			g_seen.character = nullptr;
			g_seen.moving = false;
			g_seen.letGo = why;
		}
		DevTools::Log("Possess: let go (%s)", why);
		LIVE_LINK_SERVER::ShowActivity(std::string("Let go of the character (") + why + ")");
	}

	// The yaw the frame is drawn along: what the keys move relative to
	float ViewYaw()
	{
		if (g_behind)
			return g_orbitYaw;
		std::lock_guard<std::mutex> lock(g_viewMutex);
		return atan2f(g_viewForward[0], g_viewForward[2]);
	}

	// How near the middle of the view a character is: the cosine of its angle from the view's direction, if it is within
	// about 35 degrees of it and 60 m; otherwise -2
	float InView(const float position[3], const float viewPosition[3], const float viewForward[3])
	{
		const float to[3] = { position[0] - viewPosition[0], position[1] + 1.0f - viewPosition[1], position[2] - viewPosition[2] };
		const float distance = sqrtf(to[0] * to[0] + to[1] * to[1] + to[2] * to[2]);
		if (distance < 0.5f || distance > 60.0f)
			return -2.0f;
		const float cosine = (to[0] * viewForward[0] + to[1] * viewForward[1] + to[2] * viewForward[2]) / distance;
		return cosine >= 0.82f ? cosine : -2.0f;
	}

	// Takes the character asked for, from those the game runs now
	void Choose(const Wish& wish, uint32_t root)
	{
		uint8_t* list[kMaxCharacters];
		const int count = Characters(list, kMaxCharacters);

		float viewPosition[3], viewForward[3];
		bool viewKnown;
		{
			std::lock_guard<std::mutex> lock(g_viewMutex);
			memcpy(viewPosition, g_viewPosition, sizeof(viewPosition));
			memcpy(viewForward, g_viewForward, sizeof(viewForward));
			viewKnown = g_viewKnown;
		}

		uint8_t* chosen = nullptr;
		float best = -2.0f;
		char chosenName[kNameLength + 1] = {};
		float chosenFacing = 0.0f;
		bool backstage = false;
		for (int i = 0; i < count; i++)
		{
			char name[kNameLength + 1];
			float position[3], facing;
			if (!ReadCharacter(list[i], name, position, &facing))
				continue;
			// One in the vents is passed over (its behaviour tree brings it out; held, it would stay there)
			if (ReadBackstage(list[i]))
			{
				if (wish.character == list[i] || (!wish.character && !wish.nearest && Upper(name).find(wish.name) != std::string::npos))
					backstage = true;
				continue;
			}
			float score;
			if (wish.character)
			{
				if (list[i] != wish.character)
					continue;
				score = 0.0f;
			}
			else if (wish.nearest)
			{
				if (!viewKnown)
					break;
				score = InView(position, viewPosition, viewForward);
				if (score < -1.0f)
					continue;
			}
			else
			{
				if (Upper(name).find(wish.name) == std::string::npos)
					continue;
				score = 0.0f; // the first by name
			}
			if (score > best)
			{
				best = score;
				chosen = list[i];
				memcpy(chosenName, name, sizeof(chosenName));
				chosenFacing = facing;
			}
		}

		if (!chosen)
		{
			const char* why = backstage ? "the character is in the vents: it can be taken once it comes out" :
				wish.character ? "the character chosen is gone" : wish.nearest ? (viewKnown ? "nobody near the middle of the view" : "the camera has not drawn a frame yet") : "no character has that name";
			{
				std::lock_guard<std::mutex> lock(g_mutex);
				g_seen.letGo = why;
			}
			DevTools::Log("Possess: nobody taken (%s)", why);
			return;
		}
		if (chosen == g_character)
			return;
		if (g_character)
			LetGo("another character was taken", false);

		g_character = chosen;
		g_root = root;
		g_stopping = false;
		// It stays as it stands until C is pressed
		g_crouchGiven = ReadCrouched(chosen) ? 1 : 0;
		g_crouchWanted = g_crouchGiven;
		// The camera starts behind it, looking the way it faces
		g_orbitYaw = chosenFacing;
		g_orbitPitch = kCameraPitch;
		g_possessing = true;
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			g_seen.possessing = true;
			g_seen.character = chosen;
			memcpy(g_seen.name, chosenName, sizeof(g_seen.name));
			g_seen.frames = 0;
			g_seen.at = GetTickCount64();
			g_seen.letGo.clear();
		}
		DevTools::Log("Possess: took %s", chosenName);
		LIVE_LINK_SERVER::ShowActivity(std::string("Possessing ") + chosenName);
	}

	// Once in every behaviour update (they all run on the one thread): the level, the wish, the list of characters
	void Act()
	{
		const uint32_t root = RunningRoot();
		if (g_character && root != g_root)
			LetGo("the level changed", true);

		const uint32_t generation = g_wishGeneration;
		if (generation != g_handled)
		{
			g_handled = generation;
			Wish wish;
			{
				std::lock_guard<std::mutex> lock(g_mutex);
				wish = g_wish;
			}
			if (!wish.take)
				LetGo("let go", false);
			else if (root)
				Choose(wish, root);
		}

		const ULONGLONG now = GetTickCount64();
		if (now - g_listedAt < kListEveryMs)
			return;
		g_listedAt = now;
		uint8_t* list[kMaxCharacters];
		const int count = Characters(list, kMaxCharacters);
		std::vector<Listed> characters;
		bool stillThere = false;
		for (int i = 0; i < count; i++)
		{
			Listed listed;
			listed.character = list[i];
			if (!ReadCharacter(list[i], listed.name, listed.position, nullptr))
				continue;
			listed.characterClass = ReadClass(list[i]);
			listed.backstage = ReadBackstage(list[i]);
			characters.push_back(listed);
			stillThere = stillThere || list[i] == g_character;
		}
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			g_seen.characters = std::move(characters);
		}
		if (g_character && !stillThere)
			LetGo("the character is gone", true);
	}

	struct Input
	{
		float x = 0.0f, y = 0.0f;
		int speed = 2;
		bool fromStick = false;
	};

	// The stick OpenCAGE gave while it lasts, else the keys while the game window is in front (and the menu is not open)
	Input ReadInput()
	{
		Input input;
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			if (g_stick.set)
			{
				const bool expired = g_stick.until != 0 && GetTickCount64() > g_stick.until;
				const bool connectionGone = !LIVE_LINK_SERVER::Connected() || g_stick.connection != LIVE_LINK_SERVER::CurrentConnection();
				if (expired || connectionGone)
					g_stick.set = false;
			}
			if (g_stick.set)
			{
				input.x = g_stick.x;
				input.y = g_stick.y;
				input.speed = g_stick.speed;
				input.fromStick = true;
				return input;
			}
		}
		const HWND window = g_window;
		if (!window || GetForegroundWindow() != window || g_menuOpen)
		{
			g_crouchKeyDown = false;
			return input;
		}
		auto down = [](int key) { return (GetAsyncKeyState(key) & 0x8000) != 0; };
		if (down('W')) input.y += 1.0f;
		if (down('S')) input.y -= 1.0f;
		if (down('D')) input.x += 1.0f;
		if (down('A')) input.x -= 1.0f;
		input.speed = down(VK_SHIFT) ? 3 : down(VK_CONTROL) ? 0 : 2;
		const float length = sqrtf(input.x * input.x + input.y * input.y);
		if (length > 1.0f)
		{
			input.x /= length;
			input.y /= length;
		}
		// C crouches, or stands it up again
		const bool crouchKey = down('C');
		if (crouchKey && !g_crouchKeyDown)
			g_crouchWanted = g_crouchWanted == 1 ? 0 : 1;
		g_crouchKeyDown = crouchKey;
		return input;
	}

	// After the possessed character's behaviour update: its crouch and its move orders from the stick or keys
	void Drive(uint8_t* character)
	{
		const Input input = ReadInput();
		const float magnitude = sqrtf(input.x * input.x + input.y * input.y);
		const ULONGLONG now = GetTickCount64();
		float heading = 0.0f;
		bool moving = false;

		// A crouch or stand asked for: given in place of the move order, and carried out before it moves again
		const int crouch = g_crouchWanted;
		if (crouch >= 0 && crouch != g_crouchGiven && !g_stanceOrder.object)
		{
			if (crouch == 1)
				AllowCrouch(character, true);
			if (Crouch(character, crouch == 1))
			{
				g_stanceAt = now;
				g_stopping = false;
				DevTools::Log("Possess: %s", crouch == 1 ? "crouching" : "standing up");
			}
			g_crouchGiven = crouch;
		}
		// Stood up again: its scripts' say on crouching goes back once it has
		if (g_crouchGiven == 0 && !g_stanceOrder.object && g_canCrouchWas >= 0)
			AllowCrouch(character, false);
		if (g_stanceOrder.object)
		{
			if (!StanceUnderway())
				Drop(g_stanceOrder);
			else if (now - g_stanceAt > kStanceGiveUpMs)
			{
				DevTools::Log("Possess: the %s did not finish; moving on", g_crouchGiven == 1 ? "crouch" : "stand");
				Withdraw(g_stanceOrder, false);
			}
		}

		if (g_stanceOrder.object)
		{
			// Still crouching or standing up
		}
		else if (magnitude > 0.1f)
		{
			// An order cut short to stop is not taken up again once it has stopped (one held against a wall never ends):
			// it is taken back, and a fresh one given
			if (g_stopping)
			{
				Withdraw(g_order, false);
				g_stopping = false;
			}
			heading = Wrap(ViewYaw() + atan2f(input.x, input.y));
			moving = Steer(character, heading, input.speed);
		}
		else if (g_order.object)
		{
			Coast();
			g_stopping = true;
		}

		char name[kNameLength + 1];
		float position[3], facing;
		if (!ReadCharacter(character, name, position, &facing))
			return;
		const bool crouched = ReadCrouched(character);
		OrdersSeen orders;
		ReadOrders(character, g_order.object, orders);
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			g_seen.orderStatus = orders.status;
			g_seen.ordersGiven = g_ordersGiven;
			g_seen.layerState = orders.state;
			g_seen.layerNext = orders.next;
			g_seen.walkStatus = orders.walk;
			g_seen.speedNow = orders.speedNow;
			g_seen.stuckFor = orders.stuckFor;
			memcpy(g_seen.position, position, sizeof(position));
			g_seen.facing = facing;
			g_seen.heading = heading;
			g_seen.speed = input.speed;
			g_seen.moving = moving;
			g_seen.fromStick = input.fromStick;
			g_seen.crouched = crouched;
			g_seen.frames++;
			g_seen.at = now;
		}
		if (moving && now - g_loggedAt >= 1000)
		{
			g_loggedAt = now;
			DevTools::Log("Possess: %s at %.2f, %.2f, %.2f facing %.2f, moving along %.2f at speed %d%s (%s); order status %d, %u given, layer %d (next %d), "
				"locomotion %d at speed %d, %.1f s without progress", name, position[0], position[1], position[2], facing, heading, input.speed,
				crouched ? " crouched" : "", input.fromStick ? "OpenCAGE's stick" : "the keys", orders.status, g_ordersGiven, orders.state, orders.next,
				orders.walk, orders.speedNow, orders.stuckFor);
		}
	}

	void Ask(bool take, const std::string& name, uint8_t* character = nullptr)
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		g_wish.take = take;
		g_wish.character = take ? character : nullptr;
		g_wish.nearest = take && !character && name.empty();
		g_wish.name = Upper(name);
		g_wishGeneration++;
	}

	// The menu (render thread): the characters the game runs, the one nearest the middle of the view first and then the
	// nearest, to take one; and letting go of the one possessed
	void DrawMenu()
	{
		static bool wasOpen = false;
		if (!g_menuOpen)
		{
			wasOpen = false;
			return;
		}

		std::vector<Listed> characters;
		bool possessing;
		uint8_t* possessed;
		char possessedName[kNameLength + 1];
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			characters = g_seen.characters;
			possessing = g_seen.possessing;
			possessed = g_seen.character;
			memcpy(possessedName, g_seen.name, sizeof(possessedName));
		}
		float viewPosition[3], viewForward[3];
		{
			std::lock_guard<std::mutex> lock(g_viewMutex);
			memcpy(viewPosition, g_viewPosition, sizeof(viewPosition));
			memcpy(viewForward, g_viewForward, sizeof(viewForward));
		}
		struct Row
		{
			const Listed* listed;
			float distance;
			float inView;
		};
		std::vector<Row> rows;
		for (const Listed& listed : characters)
		{
			const float d[3] = { listed.position[0] - viewPosition[0], listed.position[1] - viewPosition[1], listed.position[2] - viewPosition[2] };
			rows.push_back(Row{ &listed, sqrtf(d[0] * d[0] + d[1] * d[1] + d[2] * d[2]), InView(listed.position, viewPosition, viewForward) });
		}
		// The one nearest the middle of the view first, then the nearest
		const auto looked = std::max_element(rows.begin(), rows.end(), [](const Row& a, const Row& b) { return a.inView < b.inView; });
		const Listed* lookedAt = looked != rows.end() && looked->inView > -1.0f ? looked->listed : nullptr;
		std::sort(rows.begin(), rows.end(), [lookedAt](const Row& a, const Row& b)
		{
			if ((a.listed == lookedAt) != (b.listed == lookedAt))
				return a.listed == lookedAt;
			return a.distance < b.distance;
		});

		const ImGuiIO& io = ImGui::GetIO();
		// Readable over the game at its usual resolutions (the overlay's font is small)
		const float scale = io.DisplaySize.y >= 1000.0f ? 1.4f : 1.15f;
		ImGui::SetNextWindowPos(ImVec2(io.DisplaySize.x * 0.5f, io.DisplaySize.y * 0.5f), ImGuiCond_Always, ImVec2(0.5f, 0.5f));
		ImGui::SetNextWindowSizeConstraints(ImVec2(380.0f * scale, 0.0f), ImVec2(io.DisplaySize.x * 0.9f, io.DisplaySize.y * 0.8f));
		if (!wasOpen)
			ImGui::SetNextWindowFocus();
		ImGui::Begin("Possess a Character", nullptr, ImGuiWindowFlags_AlwaysAutoResize | ImGuiWindowFlags_NoCollapse | ImGuiWindowFlags_NoSavedSettings | ImGuiWindowFlags_NoMove);
		ImGui::SetWindowFontScale(scale);
		if (possessing)
		{
			ImGui::Text("Possessing %s", possessedName);
			ImGui::SameLine();
			if (ImGui::Button("Let go"))
			{
				Ask(false, "");
				CloseMenu();
			}
			ImGui::Separator();
		}
		if (rows.empty())
			ImGui::TextDisabled("No characters are running near you.");
		for (size_t i = 0; i < rows.size(); i++)
		{
			const Listed& listed = *rows[i].listed;
			char label[160];
			snprintf(label, sizeof(label), "%s%s##%u", listed.name, listed.backstage ? "  (in the vents)" : &listed == lookedAt ? "  (ahead)" : "", static_cast<unsigned>(i));
			const bool current = possessing && listed.character == possessed;
			// One in the vents cannot be taken until it comes out (see Choose)
			if (ImGui::Selectable(label, current, listed.backstage && !current ? ImGuiSelectableFlags_Disabled : 0, ImVec2(0.0f, 0.0f)))
			{
				if (!current)
					Ask(true, "", listed.character);
				CloseMenu();
			}
			if (!wasOpen && i == 0)
				ImGui::SetItemDefaultFocus();
			ImGui::SameLine(190.0f * scale);
			ImGui::TextDisabled("%s", ClassName(listed.characterClass));
			ImGui::SameLine(320.0f * scale);
			ImGui::TextDisabled("%.0f m", rows[i].distance);
		}
		ImGui::Separator();
		ImGui::TextDisabled("W/A/S/D move, Shift faster, Ctrl slower, C crouch, the mouse looks, the wheel zooms");
		ImGui::TextDisabled("%s or Escape closes this", Config::KeyName(Config::Get().possessKey));
		ImGui::End();
		wasOpen = true;
	}
}

__declspec(noinline)
void __fastcall POSSESS::h_behaviour_update(void* _this, void* /*_EDX*/, float elapsed, float time)
{
	Act();
	uint8_t* character = CharacterOf(_this);
	const bool possessed = character && character == g_character;
	t_inPossessed = possessed;
	behaviour_update(_this, elapsed, time);
	t_inPossessed = false;
	if (possessed && character == g_character)
		Drive(character);
}

__declspec(noinline)
void __fastcall POSSESS::h_tree_update(void* _this, void* /*_EDX*/, bool full)
{
	// The possessed character's mind is not run: the keys give its orders instead
	if (t_inPossessed)
		return;
	tree_update(_this, full);
}

LIVE_LINK::Result POSSESS::Take(uint32_t root, const std::string& name)
{
	if (!Config::Get().possess)
		return LIVE_LINK::Result{ false, "Possess Characters is off in the game (Possess=0 in OpenCAGE.RuntimeUtils.ini)" };
	switch (RunningLevel(root))
	{
	case Level::None:
		return LIVE_LINK::Result{ false, "No level is running" };
	case Level::Other:
		return LIVE_LINK::Result{ false, "The game is running a different level - save, and load this one in the game" };
	default:
		break;
	}
	Ask(true, name);
	return LIVE_LINK::Result{ true, name.empty() ? "Taking the character nearest the middle of the view" : "Taking the character named like " + name };
}

LIVE_LINK::Result POSSESS::Release()
{
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		g_stick.set = false;
	}
	Ask(false, "");
	return LIVE_LINK::Result{ true, "Letting go of the character" };
}

LIVE_LINK::Result POSSESS::SetStick(uint32_t connection, float x, float y, int speed, uint32_t milliseconds)
{
	if (!std::isfinite(x) || !std::isfinite(y) || x < -1.0f || x > 1.0f || y < -1.0f || y > 1.0f || speed > 3)
		return LIVE_LINK::Result{ false, "The stick cannot be used (x and y go from -1 to 1, the speed from 0 to 3)" };
	std::lock_guard<std::mutex> lock(g_mutex);
	if (speed < 0)
	{
		g_stick.set = false;
		return LIVE_LINK::Result{ true, "The keys move the character again" };
	}
	g_stick.set = true;
	g_stick.connection = connection;
	g_stick.x = x;
	g_stick.y = y;
	g_stick.speed = speed;
	g_stick.until = milliseconds ? GetTickCount64() + milliseconds : 0;
	return LIVE_LINK::Result{ true, "Stick taken" };
}

LIVE_LINK::Result POSSESS::SetCrouch(bool crouched)
{
	if (!g_possessing)
		return LIVE_LINK::Result{ false, "No character is possessed" };
	g_crouchWanted = crouched ? 1 : 0;
	return LIVE_LINK::Result{ true, crouched ? "Crouching" : "Standing up" };
}

LIVE_LINK::Result POSSESS::SetMenu(bool open)
{
	if (!Config::Get().possess)
		return LIVE_LINK::Result{ false, "Possess Characters is off in the game (Possess=0 in OpenCAGE.RuntimeUtils.ini)" };
	if (open)
		OpenMenu();
	else
		CloseMenu();
	return LIVE_LINK::Result{ true, open ? "Menu open" : "Menu closed" };
}

LIVE_LINK::Result POSSESS::GetState(uint32_t root)
{
	if (RunningLevel(root) == Level::Other)
		return LIVE_LINK::Result{ false, "The game is running a different level - save, and load this one in the game" };
	static const _locale_t invariant = _create_locale(LC_NUMERIC, "C");
	std::lock_guard<std::mutex> lock(g_mutex);
	const bool fresh = g_seen.possessing && GetTickCount64() - g_seen.at <= 1000;
	const char* state = !g_seen.possessing ? "none" : fresh ? "possessing" : "waiting";
	char text[1024];
	_snprintf_s_l(text, sizeof(text), _TRUNCATE, "state=%s\ncharacter=%s\nposition=%.3f,%.3f,%.3f\nfacing=%.4f\nheading=%.4f\nspeed=%d\nmoving=%d\nstick=%d\n"
		"crouched=%d\ncrouch_wanted=%d\nmenu=%d\nframes=%u\norder_status=%d\norders_given=%u\nlayer_state=%d\nlayer_next=%d\nwalk=%d\nspeed_now=%d\nstuck_for=%.2f\n"
		"let_go=%s\ncharacters=",
		invariant, state, g_seen.possessing ? g_seen.name : "", g_seen.position[0], g_seen.position[1], g_seen.position[2], g_seen.facing,
		g_seen.heading, g_seen.speed, g_seen.moving ? 1 : 0, g_seen.fromStick ? 1 : 0, g_seen.crouched ? 1 : 0, g_crouchWanted.load(), g_menuOpen ? 1 : 0,
		g_seen.frames, g_seen.orderStatus, g_seen.ordersGiven, g_seen.layerState, g_seen.layerNext, g_seen.walkStatus, g_seen.speedNow, g_seen.stuckFor,
		g_seen.letGo.c_str());
	std::string message = text;
	for (size_t i = 0; i < g_seen.characters.size(); i++)
	{
		const Listed& listed = g_seen.characters[i];
		_snprintf_s_l(text, sizeof(text), _TRUNCATE, "%s%s (%s%s)@%.2f,%.2f,%.2f", invariant, i ? ";" : "", listed.name, ClassName(listed.characterClass),
			listed.backstage ? ", in the vents" : "", listed.position[0], listed.position[1], listed.position[2]);
		message += text;
	}
	return LIVE_LINK::Result{ true, message };
}

bool POSSESS::OnWindowMessage(HWND window, UINT message, WPARAM wParam, LPARAM lParam, LRESULT& result)
{
	g_window = window;
	if (message == WM_KEYUP && wParam == static_cast<WPARAM>(Config::Get().possessKey))
	{
		if (g_menuOpen)
			CloseMenu();
		else
			OpenMenu();
	}
	FollowMenuCursor(window);

	// While the menu is open the keyboard and mouse are its, not the game's: Escape closes it
	if (g_menuOpen)
	{
		if (message == WM_KEYDOWN && wParam == VK_ESCAPE)
		{
			CloseMenu();
			FollowMenuCursor(window);
			result = 0;
			return true;
		}
		if (message == WM_INPUT)
		{
			result = DefWindowProc(window, message, wParam, lParam);
			return true;
		}
		const bool keyboard = message >= WM_KEYFIRST && message <= WM_KEYLAST;
		const bool mouse = message >= WM_MOUSEFIRST && message <= WM_MOUSELAST;
		if (keyboard || mouse)
		{
			ImGui_ImplWin32_WndProcHandler(window, message, wParam, lParam);
			result = 0;
			return true;
		}
		return false;
	}

	// While a character is possessed, the keyboard (bar Escape, for the pause menu) and the mouse are kept from the game,
	// so the player stands still; the mouse turns the camera behind the character instead
	if (!g_possessing || message != WM_INPUT || GetFocus() != window)
		return false;
	RAWINPUT input = {};
	UINT size = sizeof(input);
	if (GetRawInputData(reinterpret_cast<HRAWINPUT>(lParam), RID_INPUT, &input, &size, sizeof(RAWINPUTHEADER)) == static_cast<UINT>(-1))
		return false;
	if (input.header.dwType == RIM_TYPEMOUSE)
	{
		if (!(input.data.mouse.usFlags & MOUSE_MOVE_ABSOLUTE))
		{
			g_orbitYaw = Wrap(g_orbitYaw + input.data.mouse.lLastX * kMouseTurn);
			float pitch = g_orbitPitch - input.data.mouse.lLastY * kMouseTurn;
			g_orbitPitch = pitch < kPitchLowest ? kPitchLowest : pitch > kPitchHighest ? kPitchHighest : pitch;
		}
		// The wheel brings the camera closer (rolled forward) or takes it further away
		if (input.data.mouse.usButtonFlags & RI_MOUSE_WHEEL)
		{
			const float notches = static_cast<short>(input.data.mouse.usButtonData) / static_cast<float>(WHEEL_DELTA);
			const float distance = g_orbitDistance - notches * kWheelStep;
			g_orbitDistance = distance < kCameraNearest ? kCameraNearest : distance > kCameraFurthest ? kCameraFurthest : distance;
		}
		result = DefWindowProc(window, message, wParam, lParam);
		return true;
	}
	if (input.header.dwType == RIM_TYPEKEYBOARD && input.data.keyboard.VKey != VK_ESCAPE)
	{
		result = DefWindowProc(window, message, wParam, lParam);
		return true;
	}
	return false;
}

bool POSSESS::CameraPose(float position[3], float forward[3], float up[3])
{
	if (!g_possessing)
		return false;
	float at[3];
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		if (!g_seen.possessing || g_seen.frames == 0 || GetTickCount64() - g_seen.at > 1000)
			return false;
		memcpy(at, g_seen.position, sizeof(at));
	}
	const float yaw = g_orbitYaw, pitch = g_orbitPitch;
	forward[0] = sinf(yaw) * cosf(pitch);
	forward[1] = sinf(pitch);
	forward[2] = cosf(yaw) * cosf(pitch);
	for (int i = 0; i < 3; i++)
		position[i] = at[i] + (i == 1 ? kCameraHeight : 0.0f) - forward[i] * g_orbitDistance;
	up[0] = 0.0f;
	up[1] = 1.0f;
	up[2] = 0.0f;
	return true;
}

void POSSESS::SeenFrom(const float position[3], const float forward[3], bool behindCharacter)
{
	{
		std::lock_guard<std::mutex> lock(g_viewMutex);
		memcpy(g_viewPosition, position, sizeof(g_viewPosition));
		memcpy(g_viewForward, forward, sizeof(g_viewForward));
		g_viewKnown = true;
	}
	if (behindCharacter != g_behind.exchange(behindCharacter))
		ZONE_LOADER::SetForced(ZONE_LOADER::Source::Possess, behindCharacter);
}

bool POSSESS::Possessing()
{
	return g_possessing;
}

void POSSESS::DrawOverlay()
{
	DrawMenu();
	if (!g_possessing || g_menuOpen)
		return;

	char name[kNameLength + 1];
	bool crouched;
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		memcpy(name, g_seen.name, sizeof(name));
		crouched = g_seen.crouched;
	}
	char text[256];
	snprintf(text, sizeof(text), "Possessing %s%s - W/A/S/D move, Shift faster, Ctrl slower, C crouch, %s for the menu", name, crouched ? " (crouched)" : "",
		Config::KeyName(Config::Get().possessKey));

	ImDrawList* drawList = ImGui::GetForegroundDrawList();
	const float size = 18.0f;
	const ImVec2 textSize = ImGui::GetFont()->CalcTextSizeA(size, FLT_MAX, 0.0f, text);
	const ImVec2 display = ImGui::GetIO().DisplaySize;
	const ImVec2 place(display.x * 0.5f - textSize.x * 0.5f, display.y - textSize.y - 24.0f);
	drawList->AddRectFilled(ImVec2(place.x - 10.0f, place.y - 5.0f), ImVec2(place.x + textSize.x + 10.0f, place.y + textSize.y + 5.0f), IM_COL32(0, 0, 0, 150), 4.0f);
	drawList->AddText(ImGui::GetFont(), size, place, IM_COL32(235, 235, 235, 240), text);
}

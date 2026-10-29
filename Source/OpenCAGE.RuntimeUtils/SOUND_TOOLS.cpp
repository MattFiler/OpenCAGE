#include "SOUND_DEBUG.h"
#include "DEBUG_MENU.h"
#include "LIVE_LINK.h"

#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <mutex>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <detours.h>
#include <imgui.h>

/*
	Addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).

	Tools in the Sound tab, a section after the tab's own views (SOUND_DEBUG_VIEWS.cpp):

	- The listener held still. The game moves its listener (where sounds are heard from) with the camera every frame, so
	  flying a free camera takes the hearing with it. The game's one call that sets it is hooked: while "frozen" it is
	  given the pose it had when it was frozen, and while "at the player" the player's head and the way the player faces,
	  instead of the camera's (with the camera's movement given as none). Nothing else in the sound changes.
	- A noise the AI hears, for testing hearing: gunfire made by the player, as the game's own weapon notifier scripts make
	  it - a stimulus with a position, a loudness from 0 to 1 and the player as who made it (the AI listens only to
	  characters' sounds), published on the entity thread as the game publishes them. The Sound tab's "What the AI hears"
	  then shows who checked it and how loud they heard it. A noise at the crosshair point is still worked out through the
	  sound network from the player's own place (the game starts the route at the maker), but the AI goes to the point.
	- Any sound event played by name at the camera, the player or the crosshair point, on a short-lived sound object as
	  the game plays a sound with none (through the game's own play function, so the Event log shows it), and stopped
	  again. The game makes its table of events again with each level (from the level's own files), so the list is read
	  again when the level changes and the event is looked up by its id in the table loaded when it plays. An event plays
	  only when its sound bank is loaded.
	- The squads' dynamic dialogue: each squad's members and its dialogue state (idle, searching, escalating, in combat,
	  and the state within that), its call and response, and whether the level lets NPCs talk at all.

	Every switch, value and button is named for OpenCAGE (sound.*), and the lists the tab shows have report commands.
*/
namespace
{
	// ---- Where things are ----

	constexpr uintptr_t kSoundInterface = 0x012B1758;   // a pointer to the sound interface
	constexpr uintptr_t kGameGlobals = 0x012F0C88;      // a pointer to the game's globals
	constexpr uintptr_t kGameTime = 0x012410A8;         // the game time now (a double)
	constexpr uintptr_t kPlaceListener = 0x002B69B0;   // the listener set from the camera, once a frame (hooked)
	constexpr uintptr_t kPlayEvent = 0x00456580;        // the play function (the Sound tab hooks it: called through that)
	constexpr uintptr_t kActionOnEvent = 0x0082D240;    // the sound engine's action on an event (here: stop one playing id)
	constexpr uintptr_t kStimulusMessage = 0x00046850;  // builds a sound stimulus message for the AI
	constexpr uintptr_t kPublishStimulus = 0x001772F0;  // publishes a sound stimulus message

	// The sound interface: the listener's target (the way it faces) and position, the playback layer, the table of events
	// (a pointer to {events, count}; each event 0x14: its id, its name), whether sound is on, whether the level is closing
	constexpr uint32_t kListenerTarget = 0xA0;
	constexpr uint32_t kListenerPosition = 0xB0;
	constexpr uint32_t kPlayback = 0xC0;
	constexpr uint32_t kEventTable = 0xC4;
	constexpr uint32_t kSoundOn = 0xE5;
	constexpr uint32_t kClosing = 0x1FF;
	constexpr uint32_t kEventStride = 0x14;
	constexpr uint32_t kEventName = 0x04;

	// The game's globals: the publisher of the AI's stimuli, the NPC group manager
	constexpr uint32_t kGlobalsPublisher = 0x18;
	constexpr uint32_t kGlobalsGroups = 0x5C;
	// The group manager: whether NPCs may talk (dynamic dialogue) and how far, the squad manager (its first field is the
	// list of squads: a ring of nodes {next, previous, squad, its count})
	constexpr uint32_t kGroupsDialogueAllowed = 0x00;
	constexpr uint32_t kGroupsDialogueRange = 0x04;
	constexpr uint32_t kGroupsSquads = 0x24;
	constexpr uint32_t kNodeNext = 0x00;
	constexpr uint32_t kNodeSquad = 0x08;
	// A squad: its id, its members (an array of 0x44 each, the character at +4), its aggression and its dialogue manager
	constexpr uint32_t kSquadId = 0x10;
	constexpr uint32_t kSquadMembers = 0x1C;
	constexpr uint32_t kSquadMemberCount = 0x20;
	constexpr uint32_t kMemberStride = 0x44;
	constexpr uint32_t kMemberCharacter = 0x04;
	constexpr uint32_t kSquadAggro = 0xB0;
	constexpr uint32_t kSquadDialogue = 0x98;
	// A squad's dialogue manager: when anyone last spoke, whether the alien was seen, its state (and the one it goes to
	// next), the state of the search, escalation and combat dialogue within it, and the call and response
	constexpr uint32_t kDialogueLastLine = 0x04;
	constexpr uint32_t kDialogueAlienSighted = 0x14;
	constexpr uint32_t kDialogueState = 0x1C;
	constexpr uint32_t kDialoguePending = 0x70;
	constexpr uint32_t kDialogueSearch = 0x7C;
	constexpr uint32_t kDialogueEscalation = 0x160;
	constexpr uint32_t kDialogueCombat = 0x1E0;
	constexpr uint32_t kCallState = 0x360;
	constexpr uint32_t kCallEvent = 0x368;
	constexpr uint32_t kCallCharacter = 0x36C;
	constexpr uint32_t kCallStart = 0x370;

	// A character: the script entity it is (two ids), where its head is (the head's world matrix's last row)
	constexpr uint32_t kCharacterHandle = 0x54;
	constexpr uint32_t kCharacterHead = 0x470;

	// The playing parameters (0xC4): position, orientation, whether it sets the object's position, whether it can be
	// occluded (the rest - arguments, properties - left empty)
	constexpr uint32_t kParamsSize = 0xC4;
	constexpr uint32_t kParamsPosition = 0x00;
	constexpr uint32_t kParamsOrientation = 0x10;
	constexpr uint32_t kParamsUpdatePosition = 0x20;
	constexpr uint32_t kParamsOccludable = 0x21;

	constexpr int kWeaponFire = 1;                      // the stimulus type for gunfire
	constexpr uint32_t kStop = 0;                       // the sound engine's "stop" action
	constexpr uint32_t kAnyObject = 0xFFFFFFFF;
	constexpr uint32_t kLinearCurve = 4;
	constexpr int kMaxSquads = 64;
	constexpr int kMaxMembers = 32;

	struct Vector3 { float x, y, z; };

	typedef void(__thiscall* t_place_listener)(void* sound, const float* position, const float* rotation, Vector3 turning, Vector3 moving, bool playerCamera);
	t_place_listener place_listener = reinterpret_cast<t_place_listener>(DEVTOOLS_RELATIVE_ADDRESS(kPlaceListener));

	typedef uint32_t(__thiscall* t_play)(void* playback, const void* event, const void* params, void* object, void* callback, bool keepOpen);
	const auto play = reinterpret_cast<t_play>(DEVTOOLS_RELATIVE_ADDRESS(kPlayEvent));

	typedef int(__cdecl* t_action_on_event)(uint32_t eventId, uint32_t action, uint32_t object, int fadeMs, uint32_t curve, uint32_t playingId);
	const auto action_on_event = reinterpret_cast<t_action_on_event>(DEVTOOLS_RELATIVE_ADDRESS(kActionOnEvent));

	typedef void*(__thiscall* t_stimulus_message)(void* message, int type, const Vector3* position, float time, const uint32_t* sender, float loudness);
	const auto stimulus_message = reinterpret_cast<t_stimulus_message>(DEVTOOLS_RELATIVE_ADDRESS(kStimulusMessage));

	typedef void(__thiscall* t_publish_stimulus)(void* publisher, const void* message, bool clearFirst);
	const auto publish_stimulus = reinterpret_cast<t_publish_stimulus>(DEVTOOLS_RELATIVE_ADDRESS(kPublishStimulus));

	// ---- Guarded reads ----

	template <typename T>
	bool Read(uintptr_t at, T& out)
	{
		__try
		{
			out = *reinterpret_cast<const volatile T*>(at);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	uintptr_t Pointer(uintptr_t at)
	{
		uintptr_t value = 0;
		return at && Read(at, value) ? value : 0;
	}

	uintptr_t SoundInterface()
	{
		const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kSoundInterface);
		return at ? Pointer(at) : 0;
	}

	uintptr_t Globals()
	{
		const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kGameGlobals);
		return at ? Pointer(at) : 0;
	}

	float Now()
	{
		const uintptr_t clock = DEVTOOLS_RELATIVE_ADDRESS(kGameTime);
		double now = 0.0;
		return clock && Read(clock, now) ? static_cast<float>(now) : 0.0f;
	}

	bool LevelRunning()
	{
		__try
		{
			return LIVE_LINK::LevelRunning();
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	float Distance(const float a[3], const float b[3])
	{
		const float dx = a[0] - b[0], dy = a[1] - b[1], dz = a[2] - b[2];
		return std::sqrt(dx * dx + dy * dy + dz * dz);
	}

	bool ContainsIgnoringCase(const std::string& text, const std::string& part)
	{
		if (part.empty())
			return true;
		auto lower = [](std::string s) { for (char& c : s) c = static_cast<char>(tolower(static_cast<unsigned char>(c))); return s; };
		return lower(text).find(lower(part)) != std::string::npos;
	}

	std::string Trim(const std::string& text)
	{
		const size_t first = text.find_first_not_of(" \t\r\n");
		if (first == std::string::npos)
			return "";
		const size_t last = text.find_last_not_of(" \t\r\n");
		return text.substr(first, last - first + 1);
	}

	bool ParsePoint(const std::string& text, float out[3])
	{
		return sscanf_s(text.c_str(), "%f , %f , %f", &out[0], &out[1], &out[2]) == 3 && std::isfinite(out[0]) && std::isfinite(out[1]) && std::isfinite(out[2]);
	}

	std::string Format(const char* format, ...)
	{
		char text[512];
		va_list arguments;
		va_start(arguments, format);
		vsnprintf(text, sizeof(text), format, arguments);
		va_end(arguments);
		return text;
	}

	void Tooltip(const char* text)
	{
		if (!ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip | ImGuiHoveredFlags_AllowWhenDisabled))
			return;
		ImGui::BeginTooltip();
		ImGui::PushTextWrapPos(ImGui::GetFontSize() * 28.0f);
		ImGui::TextUnformatted(text);
		ImGui::PopTextWrapPos();
		ImGui::EndTooltip();
	}

	void SwitchBox(const char* label, std::atomic<bool>& on)
	{
		bool value = on;
		if (ImGui::Checkbox(label, &value))
			on = value;
	}

	// The status line under the tools (the last thing a button or command did)
	std::mutex g_statusMutex;
	std::string g_status;

	void SetStatus(const std::string& text)
	{
		std::lock_guard<std::mutex> lock(g_statusMutex);
		g_status = text;
		DevTools::Log("Debug menu: %s", text.c_str());
	}

	std::string Status()
	{
		std::lock_guard<std::mutex> lock(g_statusMutex);
		return g_status;
	}

	// ---- The listener ----

	std::atomic<bool> g_frozen = false, g_atPlayer = false;
	std::atomic<bool> g_listenerHooked = false;

	// The poses the hook gives the game: the camera's last (kept while frozen), and the player's head (from the render
	// thread each frame). A position is x, y, z, w; a rotation x, y, z, w.
	struct ListenerPose
	{
		alignas(16) float position[4] = {};
		alignas(16) float rotation[4] = { 0.0f, 0.0f, 0.0f, 1.0f };
		bool have = false;
	};
	std::mutex g_listenerMutex;
	ListenerPose g_cameraPose, g_frozenPose, g_playerPose;
	std::atomic<int> g_listenerMode = 0;   // what the hook did last: 0 the camera, 1 frozen, 2 at the player
	std::atomic<bool> g_forgetFrozen = false; // set when the level changes: the frozen pose was another level's

	bool CopyPose(const float* position, const float* rotation, ListenerPose& out)
	{
		__try
		{
			memcpy(out.position, position, sizeof(out.position));
			memcpy(out.rotation, rotation, sizeof(out.rotation));
			out.have = true;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	__declspec(noinline)
	void __fastcall h_place_listener(void* sound, void* /*_EDX*/, const float* position, const float* rotation, Vector3 turning, Vector3 moving, bool playerCamera)
	{
		ListenerPose given;
		bool replace = false;
		int mode = 0;
		{
			std::lock_guard<std::mutex> lock(g_listenerMutex);
			if (position && rotation)
				CopyPose(position, rotation, g_cameraPose);
			// Frozen at the camera as it is the first time it is asked for (and again once the level changes)
			if (!g_frozen || g_forgetFrozen.exchange(false))
				g_frozenPose.have = false;
			else if (!g_frozenPose.have)
				g_frozenPose = g_cameraPose;
			if (g_atPlayer && g_playerPose.have)
			{
				given = g_playerPose;
				replace = true;
				mode = 2;
			}
			else if (g_frozen && g_frozenPose.have)
			{
				given = g_frozenPose;
				replace = true;
				mode = 1;
			}
		}
		g_listenerMode = mode;
		if (replace)
			place_listener(sound, given.position, given.rotation, Vector3{}, Vector3{}, playerCamera);
		else
			place_listener(sound, position, rotation, turning, moving, playerCamera);
	}

	void AttachListenerHook(bool attach)
	{
		if (attach)
		{
			if (place_listener)
			{
				DEVTOOLS_DETOURS_ATTACH(place_listener, h_place_listener);
				g_listenerHooked = true;
			}
		}
		else if (g_listenerHooked)
		{
			DEVTOOLS_DETOURS_DETACH(place_listener, h_place_listener);
			g_listenerHooked = false;
		}
	}

	const bool g_hooks = DEBUG_MENU::AddHooks(AttachListenerHook);

	bool ReadHead(const uint8_t* character, float out[3])
	{
		__try
		{
			memcpy(out, character + kCharacterHead, sizeof(float) * 3);
			return std::isfinite(out[0]) && std::isfinite(out[1]) && std::isfinite(out[2]) && (out[0] != 0.0f || out[1] != 0.0f || out[2] != 0.0f);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	const DEBUG_MENU::Character* Player()
	{
		for (const DEBUG_MENU::Character& c : DEBUG_MENU::Characters())
			if (c.player)
				return &c;
		return nullptr;
	}

	// Render thread, every frame: the player's head and facing, for the hook while "at the player" is on (and a frozen
	// pose let go of once the level changes)
	std::string g_listenerLevel;

	void UpdatePlayerPose()
	{
		const std::string level = LIVE_LINK::RunningLevelName();
		if (level != g_listenerLevel)
		{
			g_listenerLevel = level;
			g_forgetFrozen = true;
		}
		ListenerPose pose;
		if (g_atPlayer)
			if (const DEBUG_MENU::Character* player = Player())
			{
				float head[3];
				if (!ReadHead(player->object, head))
				{
					memcpy(head, player->position, sizeof(head));
					head[1] += 1.6f;
				}
				memcpy(pose.position, head, sizeof(head));
				// A turn about the up axis by its facing (forward = (sin, 0, cos), as the game's rotations give it)
				pose.rotation[0] = 0.0f;
				pose.rotation[1] = std::sin(player->facing * 0.5f);
				pose.rotation[2] = 0.0f;
				pose.rotation[3] = std::cos(player->facing * 0.5f);
				pose.have = true;
			}
		std::lock_guard<std::mutex> lock(g_listenerMutex);
		g_playerPose = pose;
	}

	struct ListenerState
	{
		bool have = false;
		float position[3] = {}, target[3] = {};
	};

	ListenerState ReadListener()
	{
		ListenerState out;
		const uintptr_t sound = SoundInterface();
		__try
		{
			if (sound)
			{
				memcpy(out.position, reinterpret_cast<const void*>(sound + kListenerPosition), sizeof(out.position));
				memcpy(out.target, reinterpret_cast<const void*>(sound + kListenerTarget), sizeof(out.target));
				out.have = true;
			}
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			out.have = false;
		}
		return out;
	}

	const char* ListenerModeName(int mode)
	{
		switch (mode)
		{
		case 1: return "frozen";
		case 2: return "at the player";
		default: return "the camera";
		}
	}

	// ---- Events by name ----

	struct EventEntry
	{
		uint32_t index = 0, id = 0;
		std::string name;
	};
	// The level's table of events, sorted by name. The game makes the table again with each level, from the level's own
	// files (a different set, in a different order), so it is read again whenever the table, its count or the level changes.
	std::vector<EventEntry> g_events;
	uintptr_t g_eventsTable = 0;
	uint32_t g_eventsCount = 0;
	std::string g_eventsLevel;

	bool ReadEventAt(uintptr_t table, uint32_t index, uint32_t& id, char* name, size_t size)
	{
		__try
		{
			const uint8_t* event = reinterpret_cast<const uint8_t*>(table + index * kEventStride);
			id = *reinterpret_cast<const uint32_t*>(event);
			const char* text = *reinterpret_cast<const char* const*>(event + kEventName);
			size_t i = 0;
			for (; text && i + 1 < size && text[i]; i++)
				name[i] = text[i];
			name[i] = '\0';
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			name[0] = '\0';
			return false;
		}
	}

	// Render thread: empty while no level's table is loaded
	const std::vector<EventEntry>& Events()
	{
		const uintptr_t sound = SoundInterface();
		const uintptr_t database = sound ? Pointer(sound + kEventTable) : 0;
		const uintptr_t table = database ? Pointer(database) : 0;
		uint32_t count = 0;
		if (!LevelRunning() || !table || !Read(database + 4, count) || count == 0 || count > 100000)
		{
			g_events.clear();
			g_eventsTable = 0;
			return g_events;
		}
		const std::string level = LIVE_LINK::RunningLevelName();
		if (table == g_eventsTable && count == g_eventsCount && level == g_eventsLevel)
			return g_events;
		g_events.clear();
		g_events.reserve(count);
		char name[160];
		for (uint32_t i = 0; i < count; i++)
		{
			uint32_t id = 0;
			if (ReadEventAt(table, i, id, name, sizeof(name)) && id && name[0])
				g_events.push_back({ i, id, name });
		}
		std::sort(g_events.begin(), g_events.end(), [](const EventEntry& a, const EventEntry& b) { return _stricmp(a.name.c_str(), b.name.c_str()) < 0; });
		g_eventsTable = table;
		g_eventsCount = count;
		g_eventsLevel = level;
		return g_events;
	}

	// Render thread: the event of that name (ignoring case), else the only one whose name holds it; null when none
	const EventEntry* FindEvent(const std::string& name, std::string& problem)
	{
		const std::vector<EventEntry>& events = Events();
		if (events.empty())
		{
			problem = "no level running (its table of sound events is not loaded)";
			return nullptr;
		}
		const EventEntry* partial = nullptr;
		int partials = 0;
		for (const EventEntry& event : events)
		{
			if (_stricmp(event.name.c_str(), name.c_str()) == 0)
				return &event;
			if (ContainsIgnoringCase(event.name, name))
			{
				partial = &event;
				partials++;
			}
		}
		if (partials == 1)
			return partial;
		problem = partials ? Format("%d events have \"%s\" in their name; give the whole name", partials, name.c_str()) : "this level has no sound event called \"" + name + "\"";
		return nullptr;
	}

	// The last event played from here, to stop it again
	std::mutex g_playedMutex;
	uint32_t g_playedId = 0, g_playedEvent = 0;
	std::string g_playedName;

	uint32_t PlayGuarded(void* playback, const void* event, const void* params)
	{
		__try
		{
			return play(playback, event, params, nullptr, nullptr, false);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	// The place of the event with that id in the table (count events), false when it has none
	bool FindEventId(uintptr_t table, uint32_t count, uint32_t id, uint32_t& index)
	{
		__try
		{
			for (uint32_t i = 0; i < count; i++)
				if (*reinterpret_cast<const uint32_t*>(table + i * kEventStride) == id)
				{
					index = i;
					return true;
				}
			return false;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// Why an event could not be played
	enum class PlayProblem { None, NotAvailable, NoLevel, SoundOff, Closing, NoTable, NotInLevel };

	const char* PlayProblemText(PlayProblem problem)
	{
		switch (problem)
		{
		case PlayProblem::NotAvailable: return "not available on this game release";
		case PlayProblem::NoLevel: return "no level running";
		case PlayProblem::SoundOff: return "the game's sound is off";
		case PlayProblem::Closing: return "the level is closing";
		case PlayProblem::NoTable: return "the level's table of sound events is not loaded";
		case PlayProblem::NotInLevel: return "the level now running has no such event (the level changed?)";
		default: return "";
		}
	}

	// Entity thread: plays the event with that id at a point, facing a way, from the table the game has loaded now (at
	// index when it is still there, else wherever it is); the playing id (0: it did not start), or why not
	uint32_t PlayEvent(uint32_t index, uint32_t id, const float at[3], const float facing[3], bool occludable, PlayProblem& problem)
	{
		problem = PlayProblem::None;
		if (!play)
		{
			problem = PlayProblem::NotAvailable;
			return 0;
		}
		const uintptr_t sound = SoundInterface();
		if (!sound || !LevelRunning())
		{
			problem = PlayProblem::NoLevel;
			return 0;
		}
		uint8_t on = 0, closing = 1;
		if (!Read(sound + kSoundOn, on) || !on)
		{
			problem = PlayProblem::SoundOff;
			return 0;
		}
		if (!Read(sound + kClosing, closing) || closing)
		{
			problem = PlayProblem::Closing;
			return 0;
		}
		const uintptr_t playback = Pointer(sound + kPlayback);
		const uintptr_t database = Pointer(sound + kEventTable);
		const uintptr_t table = database ? Pointer(database) : 0;
		uint32_t count = 0, found = 0;
		if (!playback || !table || !Read(database + 4, count) || count == 0 || count > 100000)
		{
			problem = PlayProblem::NoTable;
			return 0;
		}
		if ((index >= count || !Read(table + index * kEventStride, found) || found != id) && !FindEventId(table, count, id, index))
		{
			problem = PlayProblem::NotInLevel;
			return 0;
		}
		alignas(16) uint8_t params[kParamsSize + 12] = {};
		float* position = reinterpret_cast<float*>(params + kParamsPosition);
		float* orientation = reinterpret_cast<float*>(params + kParamsOrientation);
		memcpy(position, at, sizeof(float) * 3);
		memcpy(orientation, facing, sizeof(float) * 3);
		params[kParamsUpdatePosition] = 1;
		params[kParamsOccludable] = occludable ? 1 : 0;
		return PlayGuarded(reinterpret_cast<void*>(playback), reinterpret_cast<const void*>(table + index * kEventStride), params);
	}

	int StopGuarded(uint32_t eventId, uint32_t playingId)
	{
		__try
		{
			return action_on_event(eventId, kStop, kAnyObject, 200, kLinearCurve, playingId);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return -1;
		}
	}

	// Render thread: where to play or make something - "camera", "player", "pick" (the crosshair point), or "x,y,z"
	bool Where(const std::string& where, float at[3], float facing[3], std::string& problem)
	{
		facing[0] = 0.0f;
		facing[1] = 1.0f;
		facing[2] = 0.0f;
		const std::string w = Trim(where);
		if (w.empty() || _stricmp(w.c_str(), "camera") == 0)
		{
			if (!DEBUG_MENU::CameraPosition(at))
			{
				problem = "no camera";
				return false;
			}
			DEBUG_MENU::CameraForward(facing);
			return true;
		}
		if (_stricmp(w.c_str(), "player") == 0)
		{
			const DEBUG_MENU::Character* player = Player();
			if (!player)
			{
				problem = "no player";
				return false;
			}
			memcpy(at, player->position, sizeof(float) * 3);
			at[1] += 1.0f;
			facing[0] = std::sin(player->facing);
			facing[1] = 0.0f;
			facing[2] = std::cos(player->facing);
			return true;
		}
		if (_stricmp(w.c_str(), "pick") == 0)
		{
			DEBUG_MENU::PickResult pick;
			if (!DEBUG_MENU::Pick(pick))
			{
				problem = "nothing under the crosshair (turn on the crosshair in World > Inspect, or the game is paused)";
				return false;
			}
			memcpy(at, pick.position, sizeof(float) * 3);
			return true;
		}
		if (ParsePoint(w, at))
			return true;
		problem = "where is \"camera\", \"player\", \"pick\" or \"x,y,z\", not \"" + w + "\"";
		return false;
	}

	// Render thread: plays an event by name somewhere, on the entity thread; what happened
	DEBUG_MENU::CommandResult RequestPlay(const std::string& name, const std::string& where)
	{
		DEBUG_MENU::CommandResult result;
		std::string problem;
		const EventEntry* event = FindEvent(Trim(name), problem);
		float at[3], facing[3];
		if (!event || !Where(where, at, facing, problem))
		{
			result.ok = false;
			result.message = problem;
			return result;
		}
		const uint32_t index = event->index, id = event->id;
		const std::string eventName = event->name;
		const bool occludable = !(where.empty() || _stricmp(Trim(where).c_str(), "camera") == 0);
		const float x = at[0], y = at[1], z = at[2], fx = facing[0], fy = facing[1], fz = facing[2];
		DEBUG_MENU::OnEntityThread([index, id, eventName, x, y, z, fx, fy, fz, occludable]()
		{
			const float point[3] = { x, y, z }, way[3] = { fx, fy, fz };
			PlayProblem problem = PlayProblem::None;
			const uint32_t playing = PlayEvent(index, id, point, way, occludable, problem);
			if (problem != PlayProblem::None)
			{
				SetStatus("could not play " + eventName + ": " + PlayProblemText(problem));
				return;
			}
			if (playing == 0)
			{
				SetStatus(eventName + " did not start (its sound bank may not be loaded in this level)");
				return;
			}
			{
				std::lock_guard<std::mutex> lock(g_playedMutex);
				g_playedId = playing;
				g_playedEvent = id;
				g_playedName = eventName;
			}
			SetStatus(Format("playing %s (playing id %u) at %.2f, %.2f, %.2f", eventName.c_str(), playing, x, y, z));
		});
		result.message = Format("event=%s\nid=%u\nat=%.3f,%.3f,%.3f\nqueued=1 (see sound.last_played)", eventName.c_str(), id, x, y, z);
		return result;
	}

	DEBUG_MENU::CommandResult RequestStop()
	{
		DEBUG_MENU::CommandResult result;
		uint32_t playingId = 0, eventId = 0;
		std::string name;
		{
			std::lock_guard<std::mutex> lock(g_playedMutex);
			playingId = g_playedId;
			eventId = g_playedEvent;
			name = g_playedName;
		}
		if (!playingId || !action_on_event)
		{
			result.ok = false;
			result.message = action_on_event ? "nothing played from here to stop (or it was stopped already)" : "not available on this game release";
			return result;
		}
		DEBUG_MENU::OnEntityThread([eventId, playingId, name]()
		{
			// The sound engine's answer: 1 when it took the stop (it fades the sound out over 0.2 s)
			const int done = StopGuarded(eventId, playingId);
			if (done != 1)
			{
				SetStatus(Format("stopping %s failed (the sound engine answered %d)", name.c_str(), done));
				return;
			}
			{
				std::lock_guard<std::mutex> lock(g_playedMutex);
				if (g_playedId == playingId)
					g_playedId = 0;
			}
			SetStatus(Format("stopped %s (playing id %u)", name.c_str(), playingId));
		});
		result.message = Format("stopping=%s\nplaying_id=%u", name.c_str(), playingId);
		return result;
	}

	// ---- A noise the AI hears ----

	std::atomic<float> g_noiseLoudness = 0.6f;

	bool PublishGuarded(void* publisher, const Vector3& position, float time, const uint32_t sender[2], float loudness)
	{
		__try
		{
			alignas(16) uint8_t message[0x60] = {};
			stimulus_message(message, kWeaponFire, &position, time, sender, loudness);
			publish_stimulus(publisher, message, false);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// Render thread: gunfire the player makes at a point, published on the entity thread
	DEBUG_MENU::CommandResult RequestNoise(const std::string& where, float loudness)
	{
		DEBUG_MENU::CommandResult result;
		if (!stimulus_message || !publish_stimulus)
		{
			result.ok = false;
			result.message = "not available on this game release";
			return result;
		}
		const DEBUG_MENU::Character* player = Player();
		uint32_t sender[2] = {};
		if (!player || player->dead || !Read(reinterpret_cast<uintptr_t>(player->object) + kCharacterHandle, sender[0]) ||
			!Read(reinterpret_cast<uintptr_t>(player->object) + kCharacterHandle + 4, sender[1]))
		{
			result.ok = false;
			result.message = "no living player to make it (the AI only listens to sounds characters make)";
			return result;
		}
		float at[3], facing[3];
		std::string problem;
		if (!Where(where.empty() ? "player" : where, at, facing, problem))
		{
			result.ok = false;
			result.message = problem;
			return result;
		}
		loudness = (std::max)(0.0f, (std::min)(1.0f, loudness));
		const Vector3 position{ at[0], at[1], at[2] };
		const uint32_t guid = sender[0], instance = sender[1];
		DEBUG_MENU::OnEntityThread([position, guid, instance, loudness]()
		{
			const uintptr_t globals = Globals();
			const uintptr_t publisher = globals ? Pointer(globals + kGlobalsPublisher) : 0;
			const uint32_t handle[2] = { guid, instance };
			if (!LevelRunning() || !publisher)
			{
				SetStatus("no noise made: no level running");
				return;
			}
			if (!PublishGuarded(reinterpret_cast<void*>(publisher), position, Now(), handle, loudness))
			{
				SetStatus("making the noise failed");
				return;
			}
			SetStatus(Format("gunfire of loudness %.2f made at %.2f, %.2f, %.2f (see What the AI hears)", loudness, position.x, position.y, position.z));
		});
		result.message = Format("noise=gunfire\nloudness=%.2f\nat=%.3f,%.3f,%.3f\nmade_by=player (%s)\nqueued=1", loudness, at[0], at[1], at[2], player->name.c_str());
		return result;
	}

	// ---- Squads' dialogue ----

	struct Squad
	{
		uint32_t id = 0;
		int aggro = 0;
		std::vector<const void*> members;
		bool haveDialogue = false;
		int state = 0, pending = 0, search = 0, escalation = 0, combat = 0;
		float lastLine = 0.0f;
		bool alienSighted = false;
		int callState = 0, callEvent = 0;
		const void* callCharacter = nullptr;
		float callStart = 0.0f;
	};

	struct Squads
	{
		bool have = false;
		bool dialogueAllowed = false;
		float dialogueRange = 0.0f;
		std::vector<Squad> squads;
	};

	bool ReadSquad(uintptr_t at, Squad& out)
	{
		__try
		{
			const uint8_t* squad = reinterpret_cast<const uint8_t*>(at);
			out.id = *reinterpret_cast<const uint32_t*>(squad + kSquadId);
			out.aggro = *reinterpret_cast<const int*>(squad + kSquadAggro);
			const uint8_t* members = *reinterpret_cast<const uint8_t* const*>(squad + kSquadMembers);
			const uint32_t count = *reinterpret_cast<const uint32_t*>(squad + kSquadMemberCount);
			for (uint32_t i = 0; members && i < count && i < static_cast<uint32_t>(kMaxMembers); i++)
				out.members.push_back(*reinterpret_cast<const void* const*>(members + i * kMemberStride + kMemberCharacter));
			const uint8_t* dialogue = *reinterpret_cast<const uint8_t* const*>(squad + kSquadDialogue);
			if (dialogue)
			{
				out.haveDialogue = true;
				out.state = *reinterpret_cast<const int*>(dialogue + kDialogueState);
				out.pending = *reinterpret_cast<const int*>(dialogue + kDialoguePending);
				out.search = *reinterpret_cast<const int*>(dialogue + kDialogueSearch);
				out.escalation = *reinterpret_cast<const int*>(dialogue + kDialogueEscalation);
				out.combat = *reinterpret_cast<const int*>(dialogue + kDialogueCombat);
				out.lastLine = *reinterpret_cast<const float*>(dialogue + kDialogueLastLine);
				out.alienSighted = dialogue[kDialogueAlienSighted] != 0;
				out.callState = *reinterpret_cast<const int*>(dialogue + kCallState);
				out.callEvent = *reinterpret_cast<const int*>(dialogue + kCallEvent);
				out.callCharacter = *reinterpret_cast<const void* const*>(dialogue + kCallCharacter);
				out.callStart = *reinterpret_cast<const float*>(dialogue + kCallStart);
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// Render thread
	Squads ReadSquads()
	{
		Squads out;
		if (!LevelRunning())
			return out;
		const uintptr_t globals = Globals();
		const uintptr_t groups = globals ? Pointer(globals + kGlobalsGroups) : 0;
		if (!groups)
			return out;
		uint8_t allowed = 0;
		Read(groups + kGroupsDialogueAllowed, allowed);
		Read(groups + kGroupsDialogueRange, out.dialogueRange);
		out.dialogueAllowed = allowed != 0;
		const uintptr_t manager = Pointer(groups + kGroupsSquads);
		const uintptr_t head = manager ? Pointer(manager) : 0;
		if (!head)
			return out;
		out.have = true;
		uintptr_t node = Pointer(head + kNodeNext);
		for (int i = 0; node && node != head && i < kMaxSquads; i++)
		{
			const uintptr_t squad = Pointer(node + kNodeSquad);
			Squad read;
			if (squad && ReadSquad(squad, read))
				out.squads.push_back(std::move(read));
			node = Pointer(node + kNodeNext);
		}
		return out;
	}

	const char* DialogueStateName(int state)
	{
		static const char* const kNames[] = { "idle", "searching", "escalating", "in combat" };
		return state >= 0 && state < 4 ? kNames[state] : "?";
	}

	const char* SearchStateName(int state)
	{
		static const char* const kNames[] = { "-", "suspicious warning", "missing buddy", "search started", "search loop", "search completed",
			"detected an enemy", "suspicious warning failed", "suspicious item (first)", "suspicious item (close)", "alerting the squad" };
		return state >= 0 && state < 11 ? kNames[state] : "?";
	}

	const char* EscalationStateName(int state)
	{
		static const char* const kNames[] = { "-", "questioning", "warning", "last chance", "standing down" };
		return state >= 0 && state < 5 ? kNames[state] : "?";
	}

	const char* CombatStateName(int state)
	{
		static const char* const kNames[] = { "-", "attack", "going to cover", "no cover", "shooting from cover", "cover broken", "last stand",
			"panic", "lost the enemy", "block", "hit by a weapon", "my death", "melee", "advance", "detected an enemy", "an ally died",
			"incoming IED", "alerting the squad", "alien sighted", "alien heard in the vents" };
		return state >= 0 && state < 20 ? kNames[state] : "?";
	}

	const char* AggroName(int level)
	{
		static const char* const kNames[] = { "none", "standing down", "questioning", "warning", "last chance", "aggressive" };
		return level >= 0 && level < 6 ? kNames[level] : "?";
	}

	const char* CallStateName(int state)
	{
		static const char* const kNames[] = { "none", "calling", "waiting for a response", "responding" };
		return state >= 0 && state < 4 ? kNames[state] : "?";
	}

	std::string SubState(const Squad& squad)
	{
		switch (squad.state)
		{
		case 1: return SearchStateName(squad.search);
		case 2: return EscalationStateName(squad.escalation);
		case 3: return CombatStateName(squad.combat);
		default: return "-";
		}
	}

	std::string CharacterName(const void* character)
	{
		if (!character)
			return "-";
		const DEBUG_MENU::Character* c = SOUND_DEBUG::CharacterByObject(character);
		return c ? c->name : std::string("(not running)");
	}

	std::string MemberNames(const Squad& squad)
	{
		std::string names;
		for (const void* member : squad.members)
			names += (names.empty() ? "" : ", ") + CharacterName(member);
		return names.empty() ? std::string("(none)") : names;
	}

	// ---- Names for OpenCAGE ----

	void Register()
	{
		static bool registered = false;
		if (registered)
			return;
		registered = true;
		DEBUG_MENU::AddSwitch("sound.listener_frozen", &g_frozen);
		DEBUG_MENU::AddSwitch("sound.listener_at_player", &g_atPlayer);
		DEBUG_MENU::AddValue("sound.noise_loudness", &g_noiseLoudness, 0.0f, 1.0f);

		DEBUG_MENU::AddCommand("sound.listener", "where the sound listener is, and whether it follows the camera, is frozen or is at the player", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]()
			{
				DEBUG_MENU::CommandResult result;
				const ListenerState listener = ReadListener();
				if (!listener.have)
				{
					result.ok = false;
					result.message = "no sound interface";
					return result;
				}
				result.message = Format("hooked=%d\nmode=%s\nfrozen=%d\nat_player=%d\nposition=%.3f,%.3f,%.3f\nfacing=%.3f,%.3f,%.3f", g_listenerHooked ? 1 : 0,
					ListenerModeName(g_listenerMode), g_frozen ? 1 : 0, g_atPlayer ? 1 : 0, listener.position[0], listener.position[1], listener.position[2],
					listener.target[0], listener.target[1], listener.target[2]);
				return result;
			});
		});
		DEBUG_MENU::AddCommand("sound.make_noise", "gunfire the AI hears, made by the player: \"player\" (default), \"pick\" (the crosshair point), \"camera\" or \"x,y,z\"; then optionally \";loudness\" (0..1, else sound.noise_loudness)", [](const std::string& argument)
		{
			const size_t semicolon = argument.find(';');
			const std::string where = Trim(argument.substr(0, semicolon));
			const float loudness = semicolon == std::string::npos ? g_noiseLoudness.load() : static_cast<float>(atof(argument.substr(semicolon + 1).c_str()));
			return DEBUG_MENU::AnswerOnRenderThread([where, loudness]() { return RequestNoise(where, loudness); });
		});
		DEBUG_MENU::AddCommand("sound.play_event", "plays a sound event by name: \"name\" or \"name;where\" (where: \"camera\" (default), \"player\", \"pick\" or \"x,y,z\")", [](const std::string& argument)
		{
			const size_t semicolon = argument.find(';');
			const std::string name = Trim(argument.substr(0, semicolon));
			const std::string where = semicolon == std::string::npos ? std::string() : argument.substr(semicolon + 1);
			if (name.empty())
				return DEBUG_MENU::CommandResult{ false, "give the event's name" };
			return DEBUG_MENU::AnswerOnRenderThread([name, where]() { return RequestPlay(name, where); });
		});
		DEBUG_MENU::AddCommand("sound.stop_event", "stops the event last played with sound.play_event (or the Play buttons)", [](const std::string&)
		{
			return RequestStop();
		});
		DEBUG_MENU::AddCommand("sound.last_played", "the event last played from the Sound tab's tools and what the last tool did", [](const std::string&)
		{
			DEBUG_MENU::CommandResult result;
			std::lock_guard<std::mutex> lock(g_playedMutex);
			result.message = Format("event=%s\nplaying_id=%u\nstatus=%s", g_playedName.c_str(), g_playedId, Status().c_str());
			return result;
		});
		DEBUG_MENU::AddCommand("sound.list_events", "the game's sound events whose name holds the text given (all with none), a line each: name, id; at most 500", [](const std::string& argument)
		{
			const std::string filter = Trim(argument);
			return DEBUG_MENU::AnswerOnRenderThread([filter]()
			{
				DEBUG_MENU::CommandResult result;
				const std::vector<EventEntry>& events = Events();
				if (events.empty())
				{
					result.ok = false;
					result.message = "no level running (its table of sound events is not loaded)";
					return result;
				}
				int shown = 0, matched = 0;
				for (const EventEntry& event : events)
				{
					if (!ContainsIgnoringCase(event.name, filter))
						continue;
					matched++;
					if (shown < 500)
					{
						result.message += Format("%s %u\n", event.name.c_str(), event.id);
						shown++;
					}
				}
				result.message += Format("count=%d of %d", matched, static_cast<int>(events.size()));
				return result;
			});
		});
		DEBUG_MENU::AddCommand("sound.list_squads", "the squads and their dialogue: a line each (id, aggression, dialogue state and sub-state, call and response, members)", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]()
			{
				DEBUG_MENU::CommandResult result;
				const Squads squads = ReadSquads();
				if (!squads.have)
				{
					result.ok = false;
					result.message = "no squads (no level running)";
					return result;
				}
				const float now = Now();
				result.message = Format("dynamic_dialogue=%d\nrange=%.1f\n", squads.dialogueAllowed ? 1 : 0, squads.dialogueRange);
				for (const Squad& squad : squads.squads)
					result.message += Format("squad %u: aggro=%s state=%s sub=%s alien_sighted=%d last_line=%.1fs_ago call=%s members=%s\n", squad.id,
						AggroName(squad.aggro), squad.haveDialogue ? DialogueStateName(squad.state) : "-", squad.haveDialogue ? SubState(squad).c_str() : "-",
						squad.alienSighted ? 1 : 0, squad.lastLine > 0.0f ? now - squad.lastLine : -1.0f, CallStateName(squad.callState), MemberNames(squad).c_str());
				result.message += Format("count=%d", static_cast<int>(squads.squads.size()));
				return result;
			});
		});
		DEBUG_MENU::AddCommand("sound.status", "the Sound tab's overview: listener, space, alien, threat, music, counts", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]()
			{
				DEBUG_MENU::CommandResult result;
				if (!SOUND_DEBUG::HooksAttached())
					return DEBUG_MENU::CommandResult{ false, "not available on this build of the game" };
				const SOUND_DEBUG::Snapshot& s = SOUND_DEBUG::Read();
				if (!s.have)
					return DEBUG_MENU::CommandResult{ false, "no level running" };
				const std::vector<SOUND_DEBUG::Network>& networks = SOUND_DEBUG::Networks();
				const std::string space = s.listenerNetwork >= 0 && s.listenerNetwork < static_cast<int>(networks.size()) ? networks[s.listenerNetwork].name : std::string("-");
				int speaking = 0;
				for (const SOUND_DEBUG::Speech& line : s.speech)
					speaking += line.status == 4 ? 1 : 0;
				result.message = Format("enabled=%d\npaused=%d\nlistener=%.3f,%.3f,%.3f\nspace=%s\nnetwork=%d\nocclusion=%d\nreverb=%d\nalien_state=%d\nalien_distance=%.2f\nthreat=%.3f\nstealth=%.3f\nmusic=%s\nmusic_value=%.3f\nobjects=%d\nplaying=%d\nspeech_playing=%d\nspeech_waiting=%d\nlock_misses=%d",
					s.enabled ? 1 : 0, s.paused ? 1 : 0, s.listener[0], s.listener[1], s.listener[2], space.c_str(), s.listenerNetwork, s.occlusion ? 1 : 0,
					s.reverb ? 1 : 0, s.alienState, s.alienDistance, s.threat, s.stealth, SOUND_DEBUG::EventName(s.musicEvent).c_str(), s.musicCurrent,
					static_cast<int>(s.objects.size()), static_cast<int>(s.playing.size()), speaking, static_cast<int>(s.speech.size()) - speaking, s.lockMisses);
				return result;
			});
		});
		DEBUG_MENU::AddCommand("sound.list_playing", "what is playing now, a line each: event, voices, owner, distance to the listener (m, -1 not in the world)", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]()
			{
				DEBUG_MENU::CommandResult result;
				if (!SOUND_DEBUG::HooksAttached())
					return DEBUG_MENU::CommandResult{ false, "not available on this build of the game" };
				const SOUND_DEBUG::Snapshot& s = SOUND_DEBUG::Read();
				if (!s.have)
					return DEBUG_MENU::CommandResult{ false, "no level running" };
				for (const SOUND_DEBUG::Playing& playing : s.playing)
				{
					const SOUND_DEBUG::SoundObject* object = playing.object >= 0 && playing.object < static_cast<int>(s.objects.size()) ? &s.objects[playing.object] : nullptr;
					const std::string event = SOUND_DEBUG::EventName(playing.eventId);
					const std::string owner = playing.objectId == 0xFFFFFFFF ? std::string("global") : object ? (object->owner.empty() ? std::string("(unnamed object)") : object->owner) : std::string("(gone)");
					const float distance = object && !(object->status & 4) ? Distance(object->position, s.listener) : -1.0f;
					result.message += Format("%s voices=%d owner=%s distance=%.1f playing_id=%u\n", event.empty() ? Format("#%08X", playing.eventId).c_str() : event.c_str(),
						playing.voices, owner.c_str(), distance, playing.playingId);
				}
				result.message += Format("count=%d", static_cast<int>(s.playing.size()));
				return result;
			});
		});
		DEBUG_MENU::AddCommand("sound.list_doorways", "the sound network's doorways, a line each: number, id, state (0 open .. 1 closed), times set, the two networks", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]()
			{
				DEBUG_MENU::CommandResult result;
				if (!SOUND_DEBUG::HooksAttached())
					return DEBUG_MENU::CommandResult{ false, "not available on this build of the game" };
				const SOUND_DEBUG::Snapshot& s = SOUND_DEBUG::Read();
				if (!s.have || !s.haveNetwork)
					return DEBUG_MENU::CommandResult{ false, "no sound network read for this level" };
				const std::vector<SOUND_DEBUG::Network>& networks = SOUND_DEBUG::Networks();
				const std::vector<SOUND_DEBUG::Barrier>& barriers = SOUND_DEBUG::Barriers();
				auto name = [&](int n) { return n >= 0 && n < static_cast<int>(networks.size()) ? networks[n].name : std::string("-"); };
				for (size_t b = 0; b < barriers.size(); b++)
					result.message += Format("%d id=0x%08X state=%.2f set=%d between=%s|%s\n", static_cast<int>(b), barriers[b].instanceId, barriers[b].state, barriers[b].timesSet,
						name(barriers[b].networks[0]).c_str(), name(barriers[b].networks[1]).c_str());
				result.message += Format("count=%d", static_cast<int>(barriers.size()));
				return result;
			});
		});
	}

	// ---- Drawing ----

	// Every frame (window open or not): names the tools, and keeps the player's pose for the listener hook
	void DrawOverlay(ImDrawList* drawList)
	{
		Register();
		UpdatePlayerPose();

		// While the listener is not at the camera, where it is and the way it faces
		const int mode = g_listenerMode;
		if (mode == 0 || (!g_frozen && !g_atPlayer))
			return;
		const ListenerState listener = ReadListener();
		if (!listener.have)
			return;
		const ImU32 colour = IM_COL32(120, 255, 200, 230);
		const float ahead[3] = { listener.position[0] + listener.target[0] * 0.5f, listener.position[1] + listener.target[1] * 0.5f, listener.position[2] + listener.target[2] * 0.5f };
		DEBUG_MENU::WorldCross(drawList, listener.position, 0.12f, colour);
		DEBUG_MENU::WorldArrow(drawList, listener.position, ahead, colour);
		SOUND_DEBUG::Label(drawList, listener.position, mode == 1 ? "listener (frozen)" : "listener (at the player)", colour, -14.0f * DEBUG_MENU::Scale());
	}

	// A sentence of help, greyed and wrapped to the window
	void Hint(const char* text)
	{
		ImGui::PushTextWrapPos(0.0f);
		ImGui::TextDisabled("%s", text);
		ImGui::PopTextWrapPos();
	}

	// The height of a table that scrolls (sideways too) for so many rows of a line each under its header, with room for its
	// sideways scrollbar, and no taller than most
	float TableHeight(size_t rows, float most)
	{
		const ImGuiStyle& style = ImGui::GetStyle();
		const float row = ImGui::GetFontSize() + style.CellPadding.y * 2.0f;
		return (std::min)(most, row * static_cast<float>(rows + 1) + style.ScrollbarSize + style.CellPadding.y * 2.0f + 2.0f);
	}

	void DrawListener()
	{
		if (!g_listenerHooked)
		{
			ImGui::TextDisabled("Not available on this release of the game.");
			return;
		}
		bool frozen = g_frozen;
		if (ImGui::Checkbox("Freeze where it is", &frozen))
		{
			g_frozen = frozen;
			if (frozen)
				g_atPlayer = false;
		}
		Tooltip("The listener stays where it was when this was switched on, facing the same way, wherever the camera goes: fly a free camera away and still hear what was heard there.");
		ImGui::SameLine();
		bool atPlayer = g_atPlayer;
		if (ImGui::Checkbox("At the player", &atPlayer))
		{
			g_atPlayer = atPlayer;
			if (atPlayer)
				g_frozen = false;
		}
		Tooltip("The listener is at the player's head, facing the way the player faces, wherever the camera goes: hear what the player hears while flying a free camera.");
		const ListenerState listener = ReadListener();
		if (listener.have)
			ImGui::Text("Listener: %.2f, %.2f, %.2f (%s)", listener.position[0], listener.position[1], listener.position[2], ListenerModeName(g_listenerMode));
		if (g_atPlayer && !Player())
			Hint("No player: the listener follows the camera.");
		Hint("Normally the listener follows the camera. Occlusion is still heard from the player's head, as the game does.");
	}

	void DrawNoise()
	{
		if (!stimulus_message || !publish_stimulus)
		{
			ImGui::TextDisabled("Not available on this release of the game.");
			return;
		}
		float loudness = g_noiseLoudness;
		ImGui::SetNextItemWidth(160.0f * DEBUG_MENU::Scale());
		if (ImGui::SliderFloat("Loudness", &loudness, 0.0f, 1.0f, "%.2f"))
			g_noiseLoudness = loudness;
		Tooltip("0 to 1. A character hears it at (1 - distance / its hearing distance) - (1 - loudness): at 1 everything in hearing range hears it, at 0.5 only those within half their range. The game's weapon notifiers use 0.6 and 1.");
		const bool player = Player() != nullptr;
		ImGui::BeginDisabled(!player);
		if (ImGui::Button("Gunfire at the player"))
		{
			const DEBUG_MENU::CommandResult result = RequestNoise("player", g_noiseLoudness);
			if (!result.ok)
				SetStatus(result.message);
		}
		ImGui::SameLine();
		if (ImGui::Button("Gunfire at the crosshair"))
		{
			const DEBUG_MENU::CommandResult result = RequestNoise("pick", g_noiseLoudness);
			if (!result.ok)
				SetStatus(result.message);
		}
		Tooltip("At the point under the crosshair (World > Inspect). It is still made by the player: the game works out how loud it is through the sound network from where the player is, but the AI goes to the point.");
		ImGui::EndDisabled();
		Hint("Gunfire made by the player, as the game's weapon notifiers make it. Who heard it shows in What the AI hears above.");
	}

	char g_eventFilter[96] = "";
	uint32_t g_eventSelected = 0;   // by id (the same in every level; 0: none)

	void DrawEvents()
	{
		if (!play)
		{
			ImGui::TextDisabled("Not available on this release of the game.");
			return;
		}
		const std::vector<EventEntry>& events = Events();
		if (events.empty())
		{
			ImGui::TextDisabled("No level running (its table of sound events is not loaded).");
			return;
		}
		ImGui::SetNextItemWidth(260.0f * DEBUG_MENU::Scale());
		ImGui::InputTextWithHint("##eventfilter", "Filter (part of the name)", g_eventFilter, sizeof(g_eventFilter));
		ImGui::SameLine();
		ImGui::TextDisabled("%d events", static_cast<int>(events.size()));
		const EventEntry* selected = nullptr;
		if (ImGui::BeginChild("events", ImVec2(0.0f, 160.0f * DEBUG_MENU::Scale()), ImGuiChildFlags_Borders))
		{
			int shown = 0;
			for (const EventEntry& event : events)
			{
				if (!ContainsIgnoringCase(event.name, g_eventFilter))
					continue;
				if (event.id == g_eventSelected)
					selected = &event;
				if (shown++ >= 400)
					continue;
				ImGui::PushID(static_cast<int>(event.index));
				if (ImGui::Selectable(event.name.c_str(), event.id == g_eventSelected))
					g_eventSelected = event.id;
				ImGui::PopID();
			}
			if (shown > 400)
				ImGui::TextDisabled("... and %d more: narrow the filter", shown - 400);
		}
		ImGui::EndChild();
		ImGui::BeginDisabled(!selected);
		const std::string name = selected ? selected->name : std::string();
		if (ImGui::Button("Play at the camera"))
		{
			const DEBUG_MENU::CommandResult result = RequestPlay(name, "camera");
			if (!result.ok)
				SetStatus(result.message);
		}
		ImGui::SameLine();
		if (ImGui::Button("At the player##play"))
		{
			const DEBUG_MENU::CommandResult result = RequestPlay(name, "player");
			if (!result.ok)
				SetStatus(result.message);
		}
		ImGui::SameLine();
		if (ImGui::Button("At the crosshair"))
		{
			const DEBUG_MENU::CommandResult result = RequestPlay(name, "pick");
			if (!result.ok)
				SetStatus(result.message);
		}
		ImGui::EndDisabled();
		ImGui::SameLine();
		uint32_t playing = 0;
		{
			std::lock_guard<std::mutex> lock(g_playedMutex);
			playing = g_playedId;
		}
		ImGui::BeginDisabled(!playing || !action_on_event);
		if (ImGui::Button("Stop it"))
			RequestStop();
		ImGui::EndDisabled();
		Tooltip("Stops the event last played from here (a looping one plays until stopped).");
		Hint("On a short-lived sound object, as the game plays a sound with none. It plays only if its sound bank is loaded in this level (the list is this level's events).");
	}

	void DrawSquads()
	{
		const Squads squads = ReadSquads();
		if (!squads.have)
		{
			ImGui::TextDisabled("No squads (no level running).");
			return;
		}
		ImGui::Text("NPC dynamic dialogue %s, within %.1f m", squads.dialogueAllowed ? "allowed" : "not allowed", squads.dialogueRange);
		if (squads.squads.empty())
		{
			ImGui::TextDisabled("No squads.");
			return;
		}
		const float now = Now();
		// Scrolls sideways rather than cutting off the members when the names are long
		if (!ImGui::BeginTable("squads", 6, ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_Resizable | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY |
			ImGuiTableFlags_SizingFixedFit, ImVec2(0.0f, TableHeight(squads.squads.size(), 220.0f * DEBUG_MENU::Scale()))))
			return;
		ImGui::TableSetupScrollFreeze(0, 1);
		ImGui::TableSetupColumn("Squad");
		ImGui::TableSetupColumn("Aggression");
		ImGui::TableSetupColumn("Dialogue");
		ImGui::TableSetupColumn("Within it");
		ImGui::TableSetupColumn("Last line");
		ImGui::TableSetupColumn("Members");
		ImGui::TableHeadersRow();
		for (const Squad& squad : squads.squads)
		{
			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			ImGui::Text("%u", squad.id);
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(AggroName(squad.aggro));
			ImGui::TableNextColumn();
			if (!squad.haveDialogue)
				ImGui::TextDisabled("-");
			else
			{
				ImGui::Text("%s%s", DialogueStateName(squad.state), squad.alienSighted ? ", alien seen" : "");
				if (squad.callState != 0 && ImGui::IsItemHovered())
					ImGui::SetTooltip("Call and response: %s, %s (%s), %.1f s ago", CallStateName(squad.callState), SOUND_DEBUG::DialogueEventName(squad.callEvent),
						CharacterName(squad.callCharacter).c_str(), now - squad.callStart);
				if (squad.pending != squad.state)
				{
					ImGui::SameLine();
					ImGui::TextDisabled("(going %s)", DialogueStateName(squad.pending));
				}
			}
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(squad.haveDialogue ? SubState(squad).c_str() : "-");
			ImGui::TableNextColumn();
			if (squad.haveDialogue && squad.lastLine > 0.0f)
				ImGui::Text("%.1f s ago", now - squad.lastLine);
			else
				ImGui::TextDisabled("-");
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(MemberNames(squad).c_str());
		}
		ImGui::EndTable();
		Hint("What each squad's NPCs talk about: the state picks which lines they may bark (the barks themselves are in NPC dynamic dialogue above).");
	}

	void DrawTab()
	{
		Register();
		ImGui::SeparatorText("Tools");
		if (ImGui::CollapsingHeader("Listener", ImGuiTreeNodeFlags_DefaultOpen))
			DrawListener();
		if (ImGui::CollapsingHeader("Make a noise for the AI"))
			DrawNoise();
		if (ImGui::CollapsingHeader("Play a sound event"))
			DrawEvents();
		if (ImGui::CollapsingHeader("Squad dialogue"))
			DrawSquads();
		const std::string status = Status();
		if (!status.empty())
			ImGui::TextWrapped("%s", status.c_str());
	}

	const bool g_added = DEBUG_MENU::AddTab("Sound", 46, DrawTab, "Sound") && DEBUG_MENU::AddOverlay(41, DrawOverlay);
}

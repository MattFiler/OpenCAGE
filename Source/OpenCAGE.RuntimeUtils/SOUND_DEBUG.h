#pragma once

#include "DevTools.h"
#include <cstdint>
#include <string>
#include <vector>

namespace DEBUG_MENU { struct Character; }
struct ImDrawList;

/*
	Sound debugging, in the debug menu (see DEBUG_MENU.h): what the game is playing and where, what the AI hears and how
	the sound reaches it, speech and subtitles, the level's sound network and the music, which the game
	itself does not draw (its sound debug drawers are empty).

	How the game's sound works, in short:
	- One sound interface lives for the whole session. It plays every sound through one function (an event, the playing
	  parameters - position, dialogue arguments - and a sound object), keeps the table of the level's events by name and
	  id (made again from each level's own files as the level loads: an event's id is the same in every level, its place
	  in the table is not), the listener (the camera), and its parts by number: the ambience grid, the music, the sound
	  network, the sound objects and the speech.
	- Sound objects are what the sound engine plays sounds on: each has an id, a position, the last event played on it, how
	  many it is playing, its nearest sound network node and whether it is occluded. Characters own four (speech, general,
	  breathing, unoccluded), every script Sound entity one; a sound played on no object gets a short-lived one named after
	  its event. They are kept in a list, behind their own lock.
	- The sound network is loaded with each level: nodes (a position, the network they belong to, links to other nodes with
	  a distance and an obstruction, each pair stored on the lower-numbered node), networks (rooms: their ambience, room
	  type, reverb, doorways and the baked doorway sequences between networks) and barriers - the doorways, with a node on
	  each side and a state from 0 (open) to 1 (closed). Doors set their barrier's state as they open and close, and Sound
	  Barrier entities do too. The listener is in the network of the node nearest it, which drives the room type, reverb
	  and the network's ambience.
	- What the AI hears is separate: footsteps, gunfire and impacts are published as stimuli (a position, a loudness from 0
	  to 1, who made it), and each character works out how loud it heard one from the shortest route through the network's
	  doorways: heard = (1 - distance / its hearing distance) - (1 - loudness), nothing when that is below zero.
	- Speech is a queue with three playing slots; a line's subtitle arrives as a key from the sound engine, which the text
	  database turns into the line on screen. Music is a controller playing the level's music event and driving one
	  parameter from the threat level, the stealth level or the alien's distance.

	Hooks (installed with the debug menu): the play function (every sound: the event log and the age of what is playing),
	a character working out how loud it heard a stimulus, the publishing of a stimulus, a barrier's state being set (the
	door log), the sound network loading (its file's network names, which the game does not keep), the sound engine
	turning a name into an id (names for ids), sound objects being added to and taken out of the list (their names), a
	subtitle being scheduled, a script Sound entity starting (to tag what it plays) and an NPC's dynamic dialogue. Each
	calls the game's own function and notes what it saw behind a lock of its own; nothing the game locks is touched.

	At each frame's presentation, while the Sound tab or an overlay wants it: the game's lock around the sound update is
	tried (and the sound object list's, and the sound engine's table of playing sounds), and the state copied when it is
	free; otherwise last frame's copy is kept. The network's nodes, links, networks and barriers are copied once per level;
	only the barrier states, the listener's nodes and the networks' ambience are read again each frame.
*/
namespace SOUND_DEBUG
{
	// Hooked: the play function - every sound the game plays (returns the sound engine's playing id).
	typedef uint32_t(__thiscall* t_play_event)(void* playback, const void* event, const void* params, void* object, void* callback, bool keepOpen);
	inline auto play_event = reinterpret_cast<t_play_event>(DEVTOOLS_RELATIVE_ADDRESS(0x00456580));
	uint32_t __fastcall h_play_event(void* playback, void* _EDX, const void* event, const void* params, void* object, void* callback, bool keepOpen);

	// Hooked: a character (its behaviour manager) working out how loud it heard a stimulus.
	typedef float(__thiscall* t_heard_intensity)(void* behaviour, const void* sender, const float* position, float loudness, bool useNetwork, float distanceScale);
	inline auto heard_intensity = reinterpret_cast<t_heard_intensity>(DEVTOOLS_RELATIVE_ADDRESS(0x002E9F10));
	float __fastcall h_heard_intensity(void* behaviour, void* _EDX, const void* sender, const float* position, float loudness, bool useNetwork, float distanceScale);

	// Hooked: a stimulus published for the AI (footstep, gunfire, impact...).
	typedef void(__thiscall* t_record_stimulus)(void* publisher, void* message, bool clearFirst);
	inline auto record_stimulus = reinterpret_cast<t_record_stimulus>(DEVTOOLS_RELATIVE_ADDRESS(0x0004A110));
	void __fastcall h_record_stimulus(void* publisher, void* _EDX, void* message, bool clearFirst);

	// Hooked: a barrier's (doorway's) state being set, by its instance id: 0 open .. 1 closed.
	typedef void(__thiscall* t_set_barrier_state)(void* network, uint32_t instanceId, float state);
	inline auto set_barrier_state = reinterpret_cast<t_set_barrier_state>(DEVTOOLS_RELATIVE_ADDRESS(0x002B99E0));
	void __fastcall h_set_barrier_state(void* network, void* _EDX, uint32_t instanceId, float state);

	// Hooked: the sound network loading from its file, as a level opens.
	typedef void(__thiscall* t_load_network)(void* network, const char* path);
	inline auto load_network = reinterpret_cast<t_load_network>(DEVTOOLS_RELATIVE_ADDRESS(0x002C0FF0));
	void __fastcall h_load_network(void* network, void* _EDX, const char* path);

	// Hooked: the sound engine turning a name into its id (every state, switch, argument, parameter and bus name).
	typedef uint32_t(__cdecl* t_name_to_id)(const char* name);
	inline auto name_to_id = reinterpret_cast<t_name_to_id>(DEVTOOLS_RELATIVE_ADDRESS(0x0082C320));
	uint32_t __cdecl h_name_to_id(const char* name);

	// Hooked: a sound object added to the game's list (with its name), and taken out of it.
	typedef void(__thiscall* t_register_object)(void* object, const char* name);
	inline auto register_object = reinterpret_cast<t_register_object>(DEVTOOLS_RELATIVE_ADDRESS(0x00456B00));
	void __fastcall h_register_object(void* object, void* _EDX, const char* name);
	typedef void(__thiscall* t_unregister_object)(void* object);
	inline auto unregister_object = reinterpret_cast<t_unregister_object>(DEVTOOLS_RELATIVE_ADDRESS(0x00456B90));
	void __fastcall h_unregister_object(void* object, void* _EDX);

	// Hooked: a subtitle scheduled (its text key, the speaking sound object's id, its priority; an empty key clears it).
	typedef void(__thiscall* t_schedule_subtitle)(void* screen, const char* key, uint32_t originId, uint32_t priority);
	inline auto schedule_subtitle = reinterpret_cast<t_schedule_subtitle>(DEVTOOLS_RELATIVE_ADDRESS(0x00406440));
	void __fastcall h_schedule_subtitle(void* screen, void* _EDX, const char* key, uint32_t originId, uint32_t priority);

	// Hooked: a script Sound entity starting to play (what it plays is tagged with the entity).
	typedef bool(__thiscall* t_script_sound_start)(void* sound, void* entity, void* trigger);
	inline auto script_sound_start = reinterpret_cast<t_script_sound_start>(DEVTOOLS_RELATIVE_ADDRESS(0x000DF6F0));
	bool __fastcall h_script_sound_start(void* sound, void* _EDX, void* entity, void* trigger);

	// Hooked: an NPC's dynamic dialogue (a squad's dialogue manager asking a character to bark an event).
	typedef void(__thiscall* t_npc_dialogue)(void* manager, void* character, int dialogueEvent, int dialogueState);
	inline auto npc_dialogue = reinterpret_cast<t_npc_dialogue>(DEVTOOLS_RELATIVE_ADDRESS(0x002DF220));
	void __fastcall h_npc_dialogue(void* manager, void* _EDX, void* character, int dialogueEvent, int dialogueState);

	// The game's text lookup (a key to the line in the game's language), called from the subtitle hook only.
	typedef bool(__cdecl* t_translate_text)(const char* key, char* out, uint32_t size);
	inline auto translate_text = reinterpret_cast<t_translate_text>(DEVTOOLS_RELATIVE_ADDRESS(0x00374460));

	void AttachHooks(bool attach);

	// ---- What the views read (render thread) ----

	struct Node
	{
		float position[3] = {};
		int network = -1;
		struct Link { uint16_t other; uint8_t distance, obstruction; };
		std::vector<Link> links;     // to higher-numbered nodes only (as the game stores them)
	};

	struct Network
	{
		std::string name;             // from the level's sound network file ("network N" when it could not be read)
		std::vector<int> nodes;
		std::vector<int> barriers;    // its doorways
		struct Path { int to; std::vector<int> barriers; };
		std::vector<Path> paths;      // the baked doorway sequences to other networks (on the lower-numbered network)
		uint32_t roomType = 0;        // the room type's value id
		int reverb = -1;              // in the reverb list
		uint32_t enterEvent = 0;      // its ambience's event id (0: none)
		float boundsMin[3] = {}, boundsMax[3] = {};
		float centre[3] = {};
		// read again every frame: whether its ambience plays, and the blend the game gives it while the listener is near one
		// of its doorways (0 to 1: the room the listener is in goes from 1 down to 0.5 nearing the doorway, the room beyond
		// from 0 up to 0.5, less the more closed the doorway is; below 0 when it has not been blended since it started)
		bool ambienceActive = false;
		float ambienceBlend = -1.0f;
	};

	struct Barrier
	{
		uint32_t instanceId = 0;      // what doors and Sound Barrier entities set it by
		int networks[2] = { -1, -1 };
		int nodes[2] = { -1, -1 };
		float position[3] = {};
		float state = 1.0f;           // read again every frame: 0 open .. 1 closed
		int timesSet = 0;             // since the level loaded (the door log)
	};

	struct SoundObject
	{
		const void* object = nullptr;
		uint32_t id = 0;
		uint32_t status = 0;          // 1 playing, 4 not in the world (2D), 0x10 occluded, 0x20 obstructed, 0x40 several positions
		uint32_t lastEvent = 0;       // id of the last event played on it
		float position[3] = {};
		int playing = 0;              // events playing on it
		int node = -1;                // its nearest sound network node
		float distance = 0.0f;        // to the listener
		std::string owner;            // who it belongs to, as the views label it
	};

	struct Playing
	{
		uint32_t playingId = 0, eventId = 0, objectId = 0;
		int voices = 0;
		float startedAt = -1.0f;      // game time it was played (from the play hook; < 0 unknown)
		int object = -1;              // in Snapshot::objects (-1: none, or the global object)
	};

	struct Speech
	{
		int type = 0;                 // 1 low, 2 medium, 3 high, 4 streamed combat, 5 combat, 6 death
		uint32_t eventId = 0;
		const void* character = nullptr;
		uint32_t objectId = 0;
		int status = 0;               // 1 queued, 2-3 waiting for lip sync, 4 playing, 5 finished
		int subStatus = 0;            // 1 opening radio squawk, 2 closing
		uint32_t playingId = 0;
		float timeout = 0.0f;         // game time it gives up waiting
		std::string arguments;
		bool inSlot = false;
	};

	struct Snapshot
	{
		bool have = false;            // a level's sound state was read (this frame or before)
		int frame = -1;
		float now = 0.0f;             // game time
		int generation = 0;           // bumped when the network is loaded again
		// the sound interface
		bool enabled = false, paused = false, closing = false;
		float listener[3] = {}, listenerForward[3] = {};
		int alienState = 0;           // 0 not present, 1 in the level, 2 backstage (in the vents)
		float alienDistance = -1.0f, stealth = 0.0f, threat = 0.0f;
		uint32_t stateHistory[16][2] = {};
		int stateCount = 0;
		// the network (what changes each frame)
		bool haveNetwork = false;
		bool networkEnabled = false, occlusion = false, reverb = false, listenerIsPlayerHead = false;
		int listenerNodes[2] = { -1, -1 };
		int listenerNetwork = -1;
		uint32_t roomType = 0;
		int activeBarrier = -1;
		// sound objects, what is playing, speech, music
		std::vector<SoundObject> objects;
		std::vector<Playing> playing;
		std::vector<Speech> speech;
		bool haveMusic = false, musicEnabled = false;
		uint32_t musicEvent = 0, musicPlayingId = 0, musicRequestEvent = 0;
		int musicState = 0, musicMode = 0, musicQueue = 0;
		float musicTarget = 0.0f, musicCurrent = 0.0f;
		struct Bank { std::string name; int references; bool loading, loaded; };
		std::vector<Bank> banks;
		int lockMisses = 0;           // frames the game's lock was busy (the copy kept)
	};

	// Render thread: this frame's state (read on the first call each frame, or kept from before when the game's lock was
	// busy). The network's nodes, networks and barriers are copied once per level.
	const Snapshot& Read();
	const std::vector<Node>& Nodes();
	const std::vector<Network>& Networks();
	const std::vector<Barrier>& Barriers();
	const std::vector<uint32_t>& Reverbs();   // the reverb list's bus ids (a network's reverb is a place in it)

	// This frame's characters (see DEBUG_MENU::Characters) by the game's character, by the script entity it is (two ids),
	// and by the id of one of its sound objects (which: 0 speech, 1 sounds, 2 breathing, 3 unoccluded); null when none.
	const DEBUG_MENU::Character* CharacterByObject(const void* character);
	const DEBUG_MENU::Character* CharacterByHandle(uint32_t entity, uint32_t instance);
	const DEBUG_MENU::Character* CharacterBySoundObject(uint32_t objectId, int* which = nullptr);
	// The sound network node a character is heard from (its sounds object's nearest node; -1 none)
	int CharacterNode(const void* character);

	// Names for ids (events from the game's table; the rest from the names the game turned into ids), or "" when unknown.
	std::string EventName(uint32_t id);
	float EventMaxDistance(uint32_t id);
	std::string Name(uint32_t id);

	// The shortest route through the network's doorways between two nodes, as the AI works it out for what it hears.
	struct Route
	{
		bool found = false;           // false: no way through (a closed doorway, or no path)
		float occlusion = 1.0f;       // 1 - the product of (1 - each doorway's state)
		float distance = 0.0f;        // metres along the route
		float obstruction = 0.0f;
		std::vector<int> points;      // nodes from the source to the listener (doorways' near and far nodes between)
		std::vector<int> barriers;    // the doorways crossed
	};
	Route ShortestRoute(int fromNode, int toNode);
	// The nearest node to a point (within 10 m, in a network whose bounds hold it), as the game finds one; -1 none.
	int NearestNode(const float position[3]);

	// What the hooks noted (copied out under their lock).
	struct PlayRecord
	{
		uint32_t serial = 0;
		float time = 0.0f;
		uint32_t playingId = 0, eventId = 0, objectId = 0;
		const void* object = nullptr; // as handed to the play function (null: a short-lived object)
		float position[3] = {};
		bool havePosition = false;
		std::string arguments;
		uint32_t scriptEntity = 0, scriptInstance = 0; // the script Sound entity that played it (0: none)
	};
	struct HeardRecord
	{
		uint32_t serial = 0, stimulus = 0;          // the stimulus it was for (0: not found)
		float time = 0.0f;
		const void* listener = nullptr;
		const void* sender = nullptr;
		float position[3] = {}, listenerPosition[3] = {};
		float loudness = 0.0f, scale = 1.0f, result = 0.0f, maxHearing = 0.0f;
		bool network = false;
		int listenerNode = -1, senderNode = -1;
	};
	struct Stimulus
	{
		uint32_t serial = 0;
		float time = 0.0f;
		int type = 0;                 // 0 footstep, 1 gunfire, 2 gunfire hitting the level, 3 body impact, 4 projectile impact
		float position[3] = {};
		uint32_t senderEntity = 0, senderInstance = 0;
		float loudness = 0.0f;
		bool motionTracker = false;
	};
	struct DoorRecord
	{
		float time = 0.0f;
		uint32_t instanceId = 0;
		float before = -1.0f, after = 0.0f;
		int barrier = -1;             // -1: no barrier has that id (it sets nothing)
		int sets = 1;                 // how many sets in a row this stands for (a moving door sets its doorway each frame)
	};
	struct DialogueRecord
	{
		float time = 0.0f;
		const void* character = nullptr;
		int dialogueEvent = 0, dialogueState = 0;
	};
	struct Subtitle
	{
		float time = 0.0f;
		std::string key, text;
		uint32_t originId = 0, priority = 0;
	};
	void Hooked(std::vector<PlayRecord>& plays, std::vector<HeardRecord>& heard, std::vector<Stimulus>& stimuli,
		std::vector<DoorRecord>& doors, std::vector<DialogueRecord>& dialogue, std::vector<Subtitle>& subtitles, Subtitle& current);
	void ClearLogs();
	bool HooksAttached();
	std::string NetworkFile();        // the sound network file the level loaded ("" before one)

	// The game's occlusion and reverb debug switches (written on the entity thread).
	void SetOcclusion(bool on);
	void SetReverb(bool on);

	// Render thread, for the overlays: text over a point as DEBUG_MENU::WorldText draws it, but kept inside the screen when
	// the point is on screen and the text would run off an edge (nothing when the point is off screen or behind the camera).
	void Label(ImDrawList* drawList, const float world[3], const char* text, uint32_t colour, float yOffsetPixels = 0.0f);

	const char* StimulusName(int type);
	const char* DialogueEventName(int dialogueEvent);
	const char* DialogueStateName(int dialogueState);
	const char* SpeechTypeName(int type);
	const char* SpeechStatusName(int status);
	const char* MusicModeName(int mode);
}

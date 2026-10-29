#include "SOUND_DEBUG.h"
#include "DEBUG_MENU.h"
#include "LIVE_LINK.h"

#include <algorithm>
#include <atomic>
#include <cfloat>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <memory>
#include <mutex>
#include <string>
#include <unordered_map>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <detours.h>
#include <imgui.h>

/*
	Addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).

	See SOUND_DEBUG.h for what is read and why. Everything the hooks note goes into rings and maps behind g_hookMutex
	(and the names behind g_namesMutex); the snapshot is only touched on the render thread.
*/
namespace
{
	// ---- Where things are ----

	constexpr uintptr_t kSoundInterface = 0x012B1758; // a pointer to the sound interface
	constexpr uintptr_t kUpdateMutex = 0x0134A7EC;    // the game's lock around the sound update (a critical section)
	constexpr uintptr_t kPlayingTable = 0x017F9698;   // a pointer to the sound engine's table of playing sounds
	constexpr uintptr_t kGameTime = 0x012410A8;       // the game clock: the game time now (a double)
	constexpr uintptr_t kHearingDistance = 0x00375F10; // a character's hearing distance (pure)

	// The sound interface: the last 16 states set (pairs of group and value ids, the count after them), the listener, the
	// table of events, the list of its parts (the first list node: {part, previous, next}; a part's number at +4), whether
	// it is paused and on, the alien's state and distance through the network, the stealth and threat levels, whether a
	// level is closing, its global sound object (inside it) and the sound object list
	constexpr uint32_t kStates = 0x10;
	constexpr uint32_t kStateCount = 0x94;
	constexpr uint32_t kListenerForward = 0xA0;
	constexpr uint32_t kListenerPosition = 0xB0;
	constexpr uint32_t kEventTable = 0xC4;
	constexpr uint32_t kParts = 0xD0;
	constexpr uint32_t kPartNumber = 0x04;
	constexpr uint32_t kPaused = 0xE4;
	constexpr uint32_t kEnabled = 0xE5;
	constexpr uint32_t kAlienDistance = 0x18C;
	constexpr uint32_t kAlienState = 0x190;
	constexpr uint32_t kStealth = 0x1D4;
	constexpr uint32_t kThreat = 0x1D8;
	constexpr uint32_t kClosing = 0x1FF;
	constexpr uint32_t kGlobalObject = 0x4A0;
	constexpr uint32_t kObjectList = 0x4E0;
	constexpr uint32_t kPartMusic = 3, kPartNetwork = 7, kPartSpeech = 11;

	// The table of events: the events (each 0x14: id, name, metadata text, bank id, how far it is heard), how many, and the
	// list of sound banks (first node at +0x10: {bank, previous, next}; a bank: its name, id, references, loading, loaded)
	constexpr uint32_t kEventStride = 0x14;
	constexpr uint32_t kEventName = 0x04;
	constexpr uint32_t kEventMaxDistance = 0x10;
	constexpr uint32_t kBanks = 0x10;

	// A sound object (0x40): its id, status, last event, position, events playing, nearest node, distance to the listener
	// and the next in the list. The list: its first object, its lock and its count.
	constexpr uint32_t kObjectStatus = 0x04;
	constexpr uint32_t kObjectEvent = 0x08;
	constexpr uint32_t kObjectPosition = 0x10;
	constexpr uint32_t kObjectPlaying = 0x30;
	constexpr uint32_t kObjectNode = 0x34;
	constexpr uint32_t kObjectDistance = 0x38;
	constexpr uint32_t kObjectNext = 0x3C;
	constexpr uint32_t kListFirst = 0x5C;
	constexpr uint32_t kListLock = 0x88;
	constexpr uint32_t kListCount = 0xA4;

	// The sound network: on, the reverb list (each a bus id), the barriers, networks and nodes (each an array: elements,
	// count), the listener's two nearest nodes (pointers), whether the player's head is the listener for occlusion, the
	// occlusion and reverb debug switches, the doorway crossfading now and the room type set
	constexpr uint32_t kNetworkOn = 0x18;
	constexpr uint32_t kReverbs = 0x1C;
	constexpr uint32_t kBarriers = 0x2C;
	constexpr uint32_t kNetworks = 0x38;
	constexpr uint32_t kNodes = 0x44;
	constexpr uint32_t kListenerNode0 = 0x58;
	constexpr uint32_t kListenerNode1 = 0x60;
	constexpr uint32_t kListenerIsHead = 0x68;
	constexpr uint32_t kOcclusionOn = 0x69;
	constexpr uint32_t kReverbOn = 0x6A;
	constexpr uint32_t kActiveBarrier = 0xAC;
	constexpr uint32_t kRoomType = 0xB4;

	// A node (0x30): its position, its network (a pointer) and its links (elements, count; each {other node, distance in
	// metres, obstruction}, stored on the lower-numbered node of the pair)
	constexpr uint32_t kNodeStride = 0x30;
	constexpr uint32_t kNodePosition = 0x10;
	constexpr uint32_t kNodeNetwork = 0x20;
	constexpr uint32_t kNodeLinks = 0x24;

	// A network (0x90): its ambience event, room type, reverb (0xFFFF none), doorways (u16 barrier numbers), the baked
	// doorway sequences (each 0x10: the other network, then an array of barrier pointers), its bounds (grown by 10 m), its
	// ambience object while it plays (a pointer to {sound object, ...}), the ambience's doorway blend (-1 until it is first
	// blended) and whether it plays
	constexpr uint32_t kNetworkStride = 0x90;
	constexpr uint32_t kNetworkEnterEvent = 0x04;
	constexpr uint32_t kNetworkRoomType = 0x0C;
	constexpr uint32_t kNetworkReverb = 0x10;
	constexpr uint32_t kNetworkDoorways = 0x18;
	constexpr uint32_t kNetworkPaths = 0x30;
	constexpr uint32_t kPathStride = 0x10;
	constexpr uint32_t kNetworkMin = 0x60;
	constexpr uint32_t kNetworkMax = 0x70;
	constexpr uint32_t kNetworkAmbienceObject = 0x80;
	constexpr uint32_t kNetworkAmbienceBlend = 0x84;
	constexpr uint32_t kNetworkAmbienceOn = 0x88;

	// A barrier (0x30): its instance id, the two networks and the node on each side (u16s), its state and position
	constexpr uint32_t kBarrierStride = 0x30;
	constexpr uint32_t kBarrierInstance = 0x04;
	constexpr uint32_t kBarrierNetworks = 0x08;
	constexpr uint32_t kBarrierNodes = 0x0C;
	constexpr uint32_t kBarrierState = 0x10;
	constexpr uint32_t kBarrierPosition = 0x20;

	// Speech: the pool of requests (a pointer to {requests, ...}), how many, the playing slots (a pointer) and how many.
	// A request (0x110): type, event, sound object, speaker, the playing parameters (dialogue arguments at +0x34, 16 of
	// {name, id}; their count at +0xCD), status, squawk, playing id and the game time it gives up.
	constexpr uint32_t kSpeechPool = 0x130;
	constexpr uint32_t kSpeechMax = 0x134;
	constexpr uint32_t kSpeechSlots = 0x138;
	constexpr uint32_t kSpeechSlotCount = 0x13C;
	constexpr uint32_t kRequestStride = 0x110;
	constexpr uint32_t kRequestEvent = 0x04;
	constexpr uint32_t kRequestObject = 0x08;
	constexpr uint32_t kRequestCharacter = 0x0C;
	constexpr uint32_t kRequestArguments = 0x34;
	constexpr uint32_t kRequestArgumentCount = 0xCD;
	constexpr uint32_t kRequestStatus = 0xE0;
	constexpr uint32_t kRequestSubStatus = 0xE4;
	constexpr uint32_t kRequestPlaying = 0xE8;
	constexpr uint32_t kRequestTimeout = 0x100;

	// Music: its event, its queue's count, its sound object (inside it), playing id, state, the parameter's target and
	// value, on, the current request's event and the mode driving the parameter
	constexpr uint32_t kMusicEvent = 0x14;
	constexpr uint32_t kMusicQueue = 0x48;
	constexpr uint32_t kMusicObject = 0x50;
	constexpr uint32_t kMusicPlaying = 0x9C;
	constexpr uint32_t kMusicState = 0xA0;
	constexpr uint32_t kMusicTarget = 0xAC;
	constexpr uint32_t kMusicCurrent = 0xB0;
	constexpr uint32_t kMusicOn = 0xB8;
	constexpr uint32_t kMusicRequestEvent = 0xC0;
	constexpr uint32_t kMusicMode = 0x120;

	// The sound engine's table of playing sounds: 31 lists, its count, its lock. An entry: voices playing, event id, the
	// sound object's id (all ones: the global one), playing id, next.
	constexpr uint32_t kTableBuckets = 31;
	constexpr uint32_t kTableLock = 0x80;
	constexpr uint32_t kItemEvent = 0x08;
	constexpr uint32_t kItemObject = 0x0C;
	constexpr uint32_t kItemPlaying = 0x20;
	constexpr uint32_t kItemNext = 0x34;

	// The playing parameters: the position (when it updates the object's position), the dialogue arguments (16 of {name,
	// id}) and their count
	constexpr uint32_t kParamsUpdatePosition = 0x20;
	constexpr uint32_t kParamsArguments = 0x24;
	constexpr uint32_t kParamsArgumentCount = 0xBD;

	// A character: where it is, the script entity it is (two ids), its four sound objects (speech, general - whose nearest
	// node is the one the AI hears it from - breathing, unoccluded); a behaviour manager's character
	constexpr uint32_t kCharacterPosition = 0x70;
	constexpr uint32_t kCharacterHandle = 0x54;
	constexpr uint32_t kCharacterSounds = 0x1320;
	constexpr uint32_t kBehaviourCharacter = 0x128;

	// A stimulus: its type, position, time, who made it (two ids), loudness; footsteps have the motion tracker flag
	constexpr uint32_t kStimulusType = 0x0C;
	constexpr uint32_t kStimulusPosition = 0x18;
	constexpr uint32_t kStimulusSender = 0x28;
	constexpr uint32_t kStimulusLoudness = 0x30;
	constexpr uint32_t kStimulusMotionTracker = 0x3A;

	// The subtitles screen: the key it shows, the speaking object's id and the priority
	constexpr uint32_t kSubtitleKey = 0xC8;
	constexpr uint32_t kSubtitleOrigin = 0x14C;
	constexpr uint32_t kSubtitlePriority = 0x150;

	// An entity (through its counted pointer's allocation, the object at +0xC): its id and the instance holding it
	constexpr uint32_t kAllocationObject = 0x0C;
	constexpr uint32_t kEntityOwner = 0x08;
	constexpr uint32_t kEntityGuid = 0x0C;

	constexpr float kNodeSnapSquared = 100.0f;        // a point snaps to a node within 10 m

	typedef float(__thiscall* t_hearing_distance)(const void* character);
	const auto hearing_distance = reinterpret_cast<t_hearing_distance>(DEVTOOLS_RELATIVE_ADDRESS(kHearingDistance));

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

	bool CopyText(const char* text, char* out, size_t size)
	{
		__try
		{
			size_t i = 0;
			for (; text && i + 1 < size && text[i]; i++)
				out[i] = text[i];
			out[i] = '\0';
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			out[0] = '\0';
			return false;
		}
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

	uintptr_t SoundInterface()
	{
		const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kSoundInterface);
		return at ? Pointer(at) : 0;
	}

	// One of the sound interface's parts by number (0: none)
	uintptr_t Part(uintptr_t sound, uint32_t number)
	{
		uintptr_t node = Pointer(sound + kParts);
		for (int i = 0; node && i < 64; i++)
		{
			const uintptr_t part = Pointer(node);
			uint32_t id = 0;
			if (part && Read(part + kPartNumber, id) && id == number)
				return part;
			node = Pointer(node + 8);
		}
		return 0;
	}

	// ---- What the hooks note ----

	template <typename T, size_t N>
	struct Ring
	{
		std::vector<T> items;
		size_t next = 0;
		void Push(const T& item)
		{
			if (items.size() < N)
				items.push_back(item);
			else
				items[next] = item;
			next = (next + 1) % N;
		}
		// The newest first
		void Copy(std::vector<T>& out) const
		{
			out.clear();
			const size_t count = items.size();
			if (!count)
				return;
			out.reserve(count);
			const size_t newest = (next + count - 1) % count;
			for (size_t i = 0; i < count; i++)
				out.push_back(items[(newest + count - i) % count]);
		}
		// The i-th newest
		const T& Newest(size_t i) const
		{
			const size_t count = items.size();
			return items[((next + count - 1) % count + count - i) % count];
		}
		void Clear()
		{
			items.clear();
			next = 0;
		}
		// The newest, to change in place (null when empty)
		T* Last()
		{
			return items.empty() ? nullptr : &items[(next + items.size() - 1) % items.size()];
		}
	};

	std::atomic<bool> g_attached = false;
	bool g_hooked[11] = {};
	std::mutex g_hookMutex;
	uint32_t g_serial = 0;
	Ring<SOUND_DEBUG::PlayRecord, 400> g_plays;
	Ring<SOUND_DEBUG::HeardRecord, 400> g_heard;
	Ring<SOUND_DEBUG::Stimulus, 200> g_stimuli;
	Ring<SOUND_DEBUG::DoorRecord, 200> g_doors;
	Ring<SOUND_DEBUG::DialogueRecord, 100> g_dialogue;
	Ring<SOUND_DEBUG::Subtitle, 40> g_subtitles;
	SOUND_DEBUG::Subtitle g_subtitle;                    // the one on screen
	std::unordered_map<uint32_t, float> g_playStarts;    // playing id -> game time it was played
	std::unordered_map<uint32_t, int> g_barrierSets;     // instance id -> times set since the network loaded
	std::string g_networkFile;
	std::vector<std::string> g_networkNames;             // from the file, by network number
	std::atomic<int> g_generation = 0;

	std::mutex g_namesMutex;
	std::unordered_map<uint32_t, std::string> g_names;   // id -> the name the game turned into it
	std::unordered_map<const void*, std::string> g_objectNames; // sound object -> the name it was added to the list with

	// The script Sound entity playing a sound on this thread (set around its start)
	thread_local uint32_t t_scriptEntity = 0;
	thread_local uint32_t t_scriptInstance = 0;

	// The name hash the sound engine uses (lower-cased A-Z, then FNV-1)
	uint32_t Hash(const char* name)
	{
		uint32_t hash = 0x811C9DC5u;
		for (; *name; name++)
		{
			char c = *name;
			if (c >= 'A' && c <= 'Z')
				c = static_cast<char>(c + 32);
			hash = hash * 0x01000193u ^ static_cast<uint8_t>(c);
		}
		return hash;
	}

	// Names the game uses that may be met before it turns them into ids itself
	void SeedNames()
	{
		static const char* const kKnown[] = {
			"ROOM_TYPE", "small_room", "vent", "corridor", "medium_room", "large_room",
			"Alien_State", "Alien_Not_Present", "Alien_In_Level", "Alien_Backstage",
			"Action_State", "Cutscene_Cue", "Music_State", "Scripted_Cue", "Subliminal_Cue", "Terror_States", "Debug_State", "On", "Off",
			"CAMERA_ANGULAR_VELOCITY", "CAMERA_LINEAR_VELOCITY", "ACCELERATION", "DISTANCE_TO_ALIEN", "DISTANCE_TO_ALIEN_UNOBSTRUCTED",
			"STEALTH_LEVEL", "THREAT_LEVEL", "DISTANCE_TO_LISTENER", "AMBIENCE_VOLUME", "OBSTRUCTED_DISTANCE", "OCCLUSION", "OBSTRUCTION",
			"HORIZONTAL_ANGLE_TO_LISTENER", "VERTICAL_ANGLE_TO_LISTENER", "ANIM_WEIGHT",
			"Character_Type", "Character_Voice", "Foley_NPC_Torso", "Foley_NPC_Leg",
			"roomverb_corridor_padded", "roomverb_medium_room", "roomverb_detachable_lab", "roomverb_vent", "roomverb_corridor", "Tannoy_Verb",
			"Warehouse_Reverb", "roomverb_huge_room", "roomverb_Ladder_shaft", "Locker_Reverb", "roomverb_large_room",
			"roomverb_multistory_largeroom", "roomverb_long_hallway", "roomverb_bathroom", "OutSide_EVA_SUIT", "roomverb_LV426_MainRoom",
			"Planetview_Room_Reverb_01", "Planetview_Room_Reverb", "roomverb_staircase", "roomverb_small_room",
			"Carpet", "Linoleum", "LV426_Builder_Corridor", "LV426_Builder_Material", "LV426_Gravel", "LV426_Rock", "Metal_Grate",
			"Metal_Grate_Loose", "Metal_Solid", "Metal_Vent", "Plastic_Solid", "Rubber_Solid", "Plastic_Sheeting", "Water", "Alien_Hive", "Trainers",
		};
		std::lock_guard<std::mutex> lock(g_namesMutex);
		for (const char* name : kKnown)
			g_names.emplace(Hash(name), name);
	}

	// -- The play function --

	struct RawPlay
	{
		uint32_t eventId = 0, objectId = 0;
		char event[96] = {};
		float position[3] = {};
		bool havePosition = false;
		char arguments[160] = {};
	};

	bool ReadPlay(const uint8_t* event, const uint8_t* params, const uint8_t* object, RawPlay& out)
	{
		__try
		{
			if (event)
			{
				out.eventId = *reinterpret_cast<const uint32_t*>(event);
				const char* name = *reinterpret_cast<const char* const*>(event + kEventName);
				size_t i = 0;
				for (; name && i + 1 < sizeof(out.event) && name[i]; i++)
					out.event[i] = name[i];
				out.event[i] = '\0';
			}
			if (object)
			{
				out.objectId = *reinterpret_cast<const uint32_t*>(object);
				memcpy(out.position, object + kObjectPosition, sizeof(out.position));
				out.havePosition = true;
			}
			else if (params && params[kParamsUpdatePosition])
			{
				memcpy(out.position, params, sizeof(out.position));
				out.havePosition = true;
			}
			if (params)
			{
				const uint8_t count = params[kParamsArgumentCount];
				size_t length = 0;
				for (uint8_t a = 0; a < count && a < 16; a++)
				{
					const char* name = *reinterpret_cast<const char* const*>(params + kParamsArguments + a * 8);
					if (!name)
						continue;
					if (length && length + 2 < sizeof(out.arguments))
					{
						out.arguments[length++] = ',';
						out.arguments[length++] = ' ';
					}
					for (size_t c = 0; name[c] && length + 1 < sizeof(out.arguments); c++)
						out.arguments[length++] = name[c];
				}
				out.arguments[length] = '\0';
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	void NotePlay(const void* event, const void* params, const void* object, uint32_t playingId)
	{
		RawPlay raw;
		if (!ReadPlay(static_cast<const uint8_t*>(event), static_cast<const uint8_t*>(params), static_cast<const uint8_t*>(object), raw))
			return;
		SOUND_DEBUG::PlayRecord record;
		record.time = Now();
		record.playingId = playingId;
		record.eventId = raw.eventId;
		record.objectId = raw.objectId;
		record.object = object;
		memcpy(record.position, raw.position, sizeof(record.position));
		record.havePosition = raw.havePosition;
		record.arguments = raw.arguments;
		record.scriptEntity = t_scriptEntity;
		record.scriptInstance = t_scriptInstance;
		std::lock_guard<std::mutex> lock(g_hookMutex);
		record.serial = ++g_serial;
		g_plays.Push(record);
		// When the times are never read (the Sound tab never shown), they are let go of now and then
		if (g_playStarts.size() > 32768)
			g_playStarts.clear();
		if (playingId)
			g_playStarts[playingId] = record.time;
	}

	// -- What the AI hears --

	struct RawHeard
	{
		const void* listener = nullptr;
		float position[3] = {}, listenerPosition[3] = {};
		int listenerNode = -1, senderNode = -1;
		float maxHearing = 0.0f;
	};

	int NodeOf(uintptr_t character, uint32_t slot)
	{
		const uintptr_t object = Pointer(character + kCharacterSounds + slot * 4);
		uint32_t node = 0xFFFFFFFF;
		if (!object || !Read(object + kObjectNode, node) || node >= 0xFFFF)
			return -1;
		return static_cast<int>(node);
	}

	bool ReadHeard(const uint8_t* behaviour, const uint8_t* sender, const float* position, RawHeard& out)
	{
		__try
		{
			const uint8_t* listener = *reinterpret_cast<const uint8_t* const*>(behaviour + kBehaviourCharacter);
			out.listener = listener;
			memcpy(out.position, position, sizeof(out.position));
			if (listener)
			{
				memcpy(out.listenerPosition, listener + kCharacterPosition, sizeof(out.listenerPosition));
				out.listenerNode = NodeOf(reinterpret_cast<uintptr_t>(listener), 1);
				if (hearing_distance)
					out.maxHearing = hearing_distance(listener);
			}
			if (sender)
				out.senderNode = NodeOf(reinterpret_cast<uintptr_t>(sender), 1);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	void NoteHeard(const void* behaviour, const void* sender, const float* position, float loudness, bool network, float scale, float result)
	{
		RawHeard raw;
		if (!behaviour || !position || !ReadHeard(static_cast<const uint8_t*>(behaviour), static_cast<const uint8_t*>(sender), position, raw))
			return;
		SOUND_DEBUG::HeardRecord record;
		record.time = Now();
		record.listener = raw.listener;
		record.sender = sender;
		memcpy(record.position, raw.position, sizeof(record.position));
		memcpy(record.listenerPosition, raw.listenerPosition, sizeof(record.listenerPosition));
		record.loudness = loudness;
		record.network = network;
		record.scale = scale;
		record.result = result;
		record.listenerNode = raw.listenerNode;
		record.senderNode = raw.senderNode;
		record.maxHearing = raw.maxHearing;
		std::lock_guard<std::mutex> lock(g_hookMutex);
		record.serial = ++g_serial;
		// The stimulus it was for: the newest one there in the last few seconds
		for (size_t i = 0; i < g_stimuli.items.size(); i++)
		{
			const SOUND_DEBUG::Stimulus& stimulus = g_stimuli.Newest(i);
			if (record.time - stimulus.time > 5.0f || stimulus.time > record.time + 0.5f)
				continue;
			if (std::fabs(stimulus.position[0] - record.position[0]) < 0.01f && std::fabs(stimulus.position[1] - record.position[1]) < 0.01f &&
				std::fabs(stimulus.position[2] - record.position[2]) < 0.01f)
			{
				record.stimulus = stimulus.serial;
				break;
			}
		}
		g_heard.Push(record);
	}

	bool ReadStimulus(const uint8_t* message, SOUND_DEBUG::Stimulus& out)
	{
		__try
		{
			out.type = *reinterpret_cast<const int*>(message + kStimulusType);
			if (out.type < 0 || out.type > 4)
				return false;
			memcpy(out.position, message + kStimulusPosition, sizeof(out.position));
			out.senderEntity = *reinterpret_cast<const uint32_t*>(message + kStimulusSender);
			out.senderInstance = *reinterpret_cast<const uint32_t*>(message + kStimulusSender + 4);
			out.loudness = *reinterpret_cast<const float*>(message + kStimulusLoudness);
			out.motionTracker = out.type == 0 && message[kStimulusMotionTracker] != 0;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	void NoteStimulus(const void* message)
	{
		SOUND_DEBUG::Stimulus stimulus;
		if (!message || !ReadStimulus(static_cast<const uint8_t*>(message), stimulus))
			return;
		stimulus.time = Now();
		std::lock_guard<std::mutex> lock(g_hookMutex);
		stimulus.serial = ++g_serial;
		g_stimuli.Push(stimulus);
	}

	// -- Doors --

	bool ReadBarrierBefore(const uint8_t* network, uint32_t instanceId, int& index, float& state)
	{
		__try
		{
			const uint8_t* barriers = *reinterpret_cast<const uint8_t* const*>(network + kBarriers);
			const uint32_t count = *reinterpret_cast<const uint32_t*>(network + kBarriers + 4);
			for (uint32_t i = 0; barriers && i < count && i < 65535; i++)
				if (*reinterpret_cast<const uint32_t*>(barriers + i * kBarrierStride + kBarrierInstance) == instanceId)
				{
					index = static_cast<int>(i);
					state = *reinterpret_cast<const float*>(barriers + i * kBarrierStride + kBarrierState);
					return true;
				}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// -- The network's file: only the names are taken (the game reads and drops them) --

	bool ReadNetworkNames(const std::string& path, std::vector<std::string>& names)
	{
		FILE* file = nullptr;
		if (fopen_s(&file, path.c_str(), "rb") != 0 || !file)
		{
			char exe[MAX_PATH] = {};
			GetModuleFileNameA(nullptr, exe, MAX_PATH);
			std::string folder = exe;
			const size_t slash = folder.find_last_of("\\/");
			folder = slash == std::string::npos ? std::string() : folder.substr(0, slash + 1);
			if (fopen_s(&file, (folder + path).c_str(), "rb") != 0 || !file)
				return false;
		}
		std::vector<uint8_t> data;
		uint8_t buffer[65536];
		size_t got = 0;
		while ((got = fread(buffer, 1, sizeof(buffer), file)) > 0 && data.size() < 64u * 1024 * 1024)
			data.insert(data.end(), buffer, buffer + got);
		fclose(file);

		size_t at = 0;
		auto take = [&](void* out, size_t size) -> bool
		{
			if (at + size > data.size())
				return false;
			memcpy(out, data.data() + at, size);
			at += size;
			return true;
		};
		uint32_t version = 0;
		uint16_t nodeCount = 0, networkCount = 0;
		if (!take(&version, 4) || !take(&nodeCount, 2) || !take(&networkCount, 2))
			return false;
		names.clear();
		for (uint16_t n = 0; n < networkCount; n++)
		{
			uint16_t length = 0;
			if (!take(&length, 2) || at + length > data.size())
				return false;
			names.emplace_back(reinterpret_cast<const char*>(data.data() + at), length);
			at += length;
			// reverb, enter and exit events (u16s), room type (u32), scalar (float), bounds (6 floats), a u16
			at += 2 + 2 + 2 + 4 + 4 + 24 + 2;
			uint16_t links = 0;
			if (!take(&links, 2))
				return false;
			at += links * 10u;
			uint16_t paths = 0;
			if (!take(&paths, 2))
				return false;
			for (uint16_t p = 0; p < paths; p++)
			{
				uint16_t to = 0, count = 0;
				if (!take(&to, 2) || !take(&count, 2))
					return false;
				at += count * 4u;
			}
		}
		return true;
	}

	// -- Subtitles --

	struct RawSubtitle
	{
		char key[130] = {};
		uint32_t origin = 0, priority = 0;
		char text[512] = {};
	};

	bool ReadSubtitle(const uint8_t* screen, RawSubtitle& out)
	{
		__try
		{
			size_t i = 0;
			for (; i + 1 < sizeof(out.key) && i < 0x80 && screen[kSubtitleKey + i]; i++)
				out.key[i] = static_cast<char>(screen[kSubtitleKey + i]);
			out.key[i] = '\0';
			out.origin = *reinterpret_cast<const uint32_t*>(screen + kSubtitleOrigin);
			out.priority = *reinterpret_cast<const uint32_t*>(screen + kSubtitlePriority);
			if (out.key[0] && SOUND_DEBUG::translate_text)
			{
				SOUND_DEBUG::translate_text(out.key, out.text, sizeof(out.text));
				out.text[sizeof(out.text) - 1] = '\0';
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// -- Script entities --

	bool ReadEntityIds(const void* entity, uint32_t& guid, uint32_t& instance)
	{
		__try
		{
			guid = instance = 0;
			const uint8_t* allocation = *static_cast<const uint8_t* const*>(entity);
			const uint8_t* object = allocation ? *reinterpret_cast<const uint8_t* const*>(allocation + kAllocationObject) : nullptr;
			if (!object)
				return false;
			guid = *reinterpret_cast<const uint32_t*>(object + kEntityGuid);
			const uint8_t* owner = *reinterpret_cast<const uint8_t* const*>(object + kEntityOwner);
			const uint8_t* ownerObject = owner ? *reinterpret_cast<const uint8_t* const*>(owner + kAllocationObject) : nullptr;
			instance = ownerObject ? *reinterpret_cast<const uint32_t*>(ownerObject + kEntityGuid) : 0;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// ---- The snapshot (render thread) ----

	SOUND_DEBUG::Snapshot g_snapshot;
	std::vector<SOUND_DEBUG::Node> g_nodes;
	std::vector<SOUND_DEBUG::Network> g_networks;
	std::vector<SOUND_DEBUG::Barrier> g_barriers;
	struct CacheKey
	{
		uintptr_t nodes = 0, networks = 0, barriers = 0;
		uint32_t nodeCount = 0, networkCount = 0, barrierCount = 0;
		int generation = -1;
		bool operator==(const CacheKey& other) const
		{
			return nodes == other.nodes && networks == other.networks && barriers == other.barriers && nodeCount == other.nodeCount &&
				networkCount == other.networkCount && barrierCount == other.barrierCount && generation == other.generation;
		}
	};
	CacheKey g_cacheKey;
	std::vector<uint32_t> g_reverbs;   // the reverb list's bus ids

	// The event table, copied when a level's is first seen (the game makes it again with each level, from the level's own
	// files): names by id, kept from level to level as an event's id is the same in every level
	struct EventInfo
	{
		std::string name;
		float maxDistance;
	};
	std::unordered_map<uint32_t, EventInfo> g_events;
	uintptr_t g_eventsTable = 0;   // the table last copied, its count and the network load it was copied after
	uint32_t g_eventsCount = 0;
	int g_eventsGeneration = -1;

	struct RawEvent
	{
		uint32_t id = 0;
		char name[128] = {};
		float maxDistance = 0.0f;
	};

	bool ReadEvent(uintptr_t table, uint32_t index, RawEvent& out)
	{
		__try
		{
			const uint8_t* event = reinterpret_cast<const uint8_t*>(table + index * kEventStride);
			out.id = *reinterpret_cast<const uint32_t*>(event);
			out.maxDistance = *reinterpret_cast<const float*>(event + kEventMaxDistance);
			const char* name = *reinterpret_cast<const char* const*>(event + kEventName);
			size_t i = 0;
			for (; name && i + 1 < sizeof(out.name) && name[i]; i++)
				out.name[i] = name[i];
			out.name[i] = '\0';
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	void ReadEvents(uintptr_t sound)
	{
		const uintptr_t database = Pointer(sound + kEventTable);
		const uintptr_t table = database ? Pointer(database) : 0;
		uint32_t count = 0;
		if (!table || !Read(database + 4, count) || count == 0 || count > 100000)
			return;
		const int generation = g_generation;
		if (table == g_eventsTable && count == g_eventsCount && generation == g_eventsGeneration)
			return;
		RawEvent raw;
		for (uint32_t i = 0; i < count; i++)
			if (ReadEvent(table, i, raw) && raw.id)
				g_events[raw.id] = { raw.name, raw.maxDistance };
		g_eventsTable = table;
		g_eventsCount = count;
		g_eventsGeneration = generation;
	}

	struct RawInterface
	{
		bool enabled, paused, closing;
		float listener[3], forward[3];
		int alienState;
		float alienDistance, stealth, threat;
		uint32_t states[16][2];
		int stateCount;
	};

	bool ReadInterface(uintptr_t sound, RawInterface& out)
	{
		__try
		{
			const uint8_t* s = reinterpret_cast<const uint8_t*>(sound);
			out.enabled = s[kEnabled] != 0;
			out.paused = s[kPaused] != 0;
			out.closing = s[kClosing] != 0;
			memcpy(out.listener, s + kListenerPosition, sizeof(out.listener));
			memcpy(out.forward, s + kListenerForward, sizeof(out.forward));
			out.alienState = *reinterpret_cast<const int*>(s + kAlienState);
			out.alienDistance = *reinterpret_cast<const float*>(s + kAlienDistance);
			out.stealth = *reinterpret_cast<const float*>(s + kStealth);
			out.threat = *reinterpret_cast<const float*>(s + kThreat);
			memcpy(out.states, s + kStates, sizeof(out.states));
			out.stateCount = *reinterpret_cast<const int*>(s + kStateCount);
			if (out.stateCount < 0 || out.stateCount > 16)
				out.stateCount = 0;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	struct RawNetworkState
	{
		bool on, occlusion, reverb, head;
		uintptr_t listener[2], active;
		uint32_t roomType;
		CacheKey key;
		uintptr_t reverbs;
		uint32_t reverbCount;
	};

	bool ReadNetworkState(uintptr_t network, RawNetworkState& out)
	{
		__try
		{
			const uint8_t* n = reinterpret_cast<const uint8_t*>(network);
			out.on = n[kNetworkOn] != 0;
			out.occlusion = n[kOcclusionOn] != 0;
			out.reverb = n[kReverbOn] != 0;
			out.head = n[kListenerIsHead] != 0;
			out.listener[0] = *reinterpret_cast<const uintptr_t*>(n + kListenerNode0);
			out.listener[1] = *reinterpret_cast<const uintptr_t*>(n + kListenerNode1);
			out.active = *reinterpret_cast<const uintptr_t*>(n + kActiveBarrier);
			out.roomType = *reinterpret_cast<const uint32_t*>(n + kRoomType);
			out.key.barriers = *reinterpret_cast<const uintptr_t*>(n + kBarriers);
			out.key.barrierCount = *reinterpret_cast<const uint32_t*>(n + kBarriers + 4);
			out.key.networks = *reinterpret_cast<const uintptr_t*>(n + kNetworks);
			out.key.networkCount = *reinterpret_cast<const uint32_t*>(n + kNetworks + 4);
			out.key.nodes = *reinterpret_cast<const uintptr_t*>(n + kNodes);
			out.key.nodeCount = *reinterpret_cast<const uint32_t*>(n + kNodes + 4);
			out.reverbs = *reinterpret_cast<const uintptr_t*>(n + kReverbs);
			out.reverbCount = *reinterpret_cast<const uint32_t*>(n + kReverbs + 4);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// One node, into plain arrays (links cut at 256)
	struct RawNode
	{
		float position[3];
		uintptr_t network;
		uint32_t linkCount;
		SOUND_DEBUG::Node::Link links[256];
	};

	bool ReadNode(uintptr_t node, RawNode& out)
	{
		__try
		{
			const uint8_t* n = reinterpret_cast<const uint8_t*>(node);
			memcpy(out.position, n + kNodePosition, sizeof(out.position));
			out.network = *reinterpret_cast<const uintptr_t*>(n + kNodeNetwork);
			const uint8_t* links = *reinterpret_cast<const uint8_t* const*>(n + kNodeLinks);
			const uint32_t count = *reinterpret_cast<const uint32_t*>(n + kNodeLinks + 4);
			out.linkCount = links ? (std::min)(count, 256u) : 0;
			for (uint32_t i = 0; i < out.linkCount; i++)
			{
				out.links[i].other = *reinterpret_cast<const uint16_t*>(links + i * 4);
				out.links[i].distance = links[i * 4 + 2];
				out.links[i].obstruction = links[i * 4 + 3];
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	struct RawNetwork
	{
		uintptr_t enterEvent;
		uint32_t roomType;
		uint16_t reverb;
		uint32_t doorwayCount;
		uint16_t doorways[256];
		uintptr_t paths;
		uint32_t pathCount;
		float boundsMin[3], boundsMax[3];
	};

	bool ReadNetwork(uintptr_t network, RawNetwork& out)
	{
		__try
		{
			const uint8_t* n = reinterpret_cast<const uint8_t*>(network);
			out.enterEvent = *reinterpret_cast<const uintptr_t*>(n + kNetworkEnterEvent);
			out.roomType = *reinterpret_cast<const uint32_t*>(n + kNetworkRoomType);
			out.reverb = *reinterpret_cast<const uint16_t*>(n + kNetworkReverb);
			const uint16_t* doorways = *reinterpret_cast<const uint16_t* const*>(n + kNetworkDoorways);
			const uint32_t count = *reinterpret_cast<const uint32_t*>(n + kNetworkDoorways + 4);
			out.doorwayCount = doorways ? (std::min)(count, 256u) : 0;
			for (uint32_t i = 0; i < out.doorwayCount; i++)
				out.doorways[i] = doorways[i];
			out.paths = *reinterpret_cast<const uintptr_t*>(n + kNetworkPaths);
			out.pathCount = *reinterpret_cast<const uint32_t*>(n + kNetworkPaths + 4);
			memcpy(out.boundsMin, n + kNetworkMin, sizeof(out.boundsMin));
			memcpy(out.boundsMax, n + kNetworkMax, sizeof(out.boundsMax));
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// One baked doorway sequence: the other network and its barriers (as numbers, from their pointers)
	bool ReadPath(uintptr_t paths, uint32_t index, uintptr_t barriers, uint16_t& to, uint32_t& count, int* out, uint32_t capacity)
	{
		__try
		{
			const uint8_t* path = reinterpret_cast<const uint8_t*>(paths + index * kPathStride);
			to = *reinterpret_cast<const uint16_t*>(path);
			const uintptr_t* list = *reinterpret_cast<const uintptr_t* const*>(path + 4);
			const uint32_t total = *reinterpret_cast<const uint32_t*>(path + 8);
			count = list ? (std::min)(total, capacity) : 0;
			for (uint32_t i = 0; i < count; i++)
				out[i] = list[i] >= barriers ? static_cast<int>((list[i] - barriers) / kBarrierStride) : -1;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	struct RawBarrier
	{
		uint32_t instance;
		uint16_t networks[2], nodes[2];
		float position[3];
	};

	bool ReadBarrier(uintptr_t barrier, RawBarrier& out)
	{
		__try
		{
			const uint8_t* b = reinterpret_cast<const uint8_t*>(barrier);
			out.instance = *reinterpret_cast<const uint32_t*>(b + kBarrierInstance);
			memcpy(out.networks, b + kBarrierNetworks, sizeof(out.networks));
			memcpy(out.nodes, b + kBarrierNodes, sizeof(out.nodes));
			memcpy(out.position, b + kBarrierPosition, sizeof(out.position));
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// The per-frame part of the network: barrier states, the networks' ambience
	bool ReadNetworkLive(const CacheKey& key, float* states, uint32_t barrierCount, bool* ambienceOn, float* ambienceBlend, uintptr_t* ambienceObjects,
		uint32_t networkCount)
	{
		__try
		{
			for (uint32_t i = 0; i < barrierCount; i++)
				states[i] = *reinterpret_cast<const float*>(key.barriers + i * kBarrierStride + kBarrierState);
			for (uint32_t i = 0; i < networkCount; i++)
			{
				const uint8_t* n = reinterpret_cast<const uint8_t*>(key.networks + i * kNetworkStride);
				ambienceOn[i] = n[kNetworkAmbienceOn] != 0;
				ambienceBlend[i] = *reinterpret_cast<const float*>(n + kNetworkAmbienceBlend);
				const uintptr_t holder = *reinterpret_cast<const uintptr_t*>(n + kNetworkAmbienceObject);
				ambienceObjects[i] = holder ? *reinterpret_cast<const uintptr_t*>(holder) : 0;
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool ReadReverbs(uintptr_t reverbs, uint32_t count, uint32_t* out)
	{
		__try
		{
			for (uint32_t i = 0; i < count; i++)
				out[i] = reinterpret_cast<const uint32_t*>(reverbs)[i];
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	void BuildNetworkCache(const CacheKey& key, uintptr_t reverbs, uint32_t reverbCount)
	{
		g_nodes.clear();
		g_networks.clear();
		g_barriers.clear();
		g_reverbs.clear();
		g_cacheKey = key;
		if (!key.nodes || !key.networks || key.nodeCount > 65535 || key.networkCount > 4096 || key.barrierCount > 65535)
			return;

		g_networks.resize(key.networkCount);
		static RawNetwork rawNetwork;
		static int pathBarriers[512];
		for (uint32_t i = 0; i < key.networkCount; i++)
		{
			SOUND_DEBUG::Network& network = g_networks[i];
			char name[32];
			snprintf(name, sizeof(name), "network %u", i);
			network.name = name;
			if (!ReadNetwork(key.networks + i * kNetworkStride, rawNetwork))
				continue;
			network.roomType = rawNetwork.roomType;
			network.reverb = rawNetwork.reverb == 0xFFFF ? -1 : rawNetwork.reverb;
			uint32_t enterEvent = 0;
			network.enterEvent = rawNetwork.enterEvent && Read(rawNetwork.enterEvent, enterEvent) ? enterEvent : 0;
			for (uint32_t d = 0; d < rawNetwork.doorwayCount; d++)
				network.barriers.push_back(rawNetwork.doorways[d]);
			memcpy(network.boundsMin, rawNetwork.boundsMin, sizeof(network.boundsMin));
			memcpy(network.boundsMax, rawNetwork.boundsMax, sizeof(network.boundsMax));
			for (uint32_t p = 0; rawNetwork.paths && p < rawNetwork.pathCount && p < 4096; p++)
			{
				uint16_t to = 0;
				uint32_t count = 0;
				if (!ReadPath(rawNetwork.paths, p, key.barriers, to, count, pathBarriers, 512))
					break;
				SOUND_DEBUG::Network::Path path;
				path.to = to;
				path.barriers.assign(pathBarriers, pathBarriers + count);
				network.paths.push_back(std::move(path));
			}
		}

		g_nodes.resize(key.nodeCount);
		static RawNode rawNode;
		for (uint32_t i = 0; i < key.nodeCount; i++)
		{
			SOUND_DEBUG::Node& node = g_nodes[i];
			if (!ReadNode(key.nodes + i * kNodeStride, rawNode))
				continue;
			memcpy(node.position, rawNode.position, sizeof(node.position));
			if (rawNode.network >= key.networks && rawNode.network < key.networks + key.networkCount * kNetworkStride)
				node.network = static_cast<int>((rawNode.network - key.networks) / kNetworkStride);
			node.links.assign(rawNode.links, rawNode.links + rawNode.linkCount);
			if (node.network >= 0)
				g_networks[node.network].nodes.push_back(static_cast<int>(i));
		}
		for (SOUND_DEBUG::Network& network : g_networks)
		{
			float sum[3] = {};
			for (int n : network.nodes)
				for (int a = 0; a < 3; a++)
					sum[a] += g_nodes[n].position[a];
			if (!network.nodes.empty())
				for (int a = 0; a < 3; a++)
					network.centre[a] = sum[a] / network.nodes.size();
		}

		g_barriers.resize(key.barrierCount);
		RawBarrier rawBarrier;
		for (uint32_t i = 0; key.barriers && i < key.barrierCount; i++)
		{
			SOUND_DEBUG::Barrier& barrier = g_barriers[i];
			if (!ReadBarrier(key.barriers + i * kBarrierStride, rawBarrier))
				continue;
			barrier.instanceId = rawBarrier.instance;
			for (int s = 0; s < 2; s++)
			{
				barrier.networks[s] = rawBarrier.networks[s] < key.networkCount ? rawBarrier.networks[s] : -1;
				barrier.nodes[s] = rawBarrier.nodes[s] < key.nodeCount ? rawBarrier.nodes[s] : -1;
			}
			memcpy(barrier.position, rawBarrier.position, sizeof(barrier.position));
		}

		if (reverbs && reverbCount && reverbCount < 1024)
		{
			g_reverbs.resize(reverbCount);
			if (!ReadReverbs(reverbs, reverbCount, g_reverbs.data()))
				g_reverbs.clear();
		}
	}

	void ApplyNetworkNames()
	{
		std::lock_guard<std::mutex> lock(g_hookMutex);
		for (size_t i = 0; i < g_networks.size() && i < g_networkNames.size(); i++)
			if (!g_networkNames[i].empty())
				g_networks[i].name = g_networkNames[i];
	}

	struct RawObject
	{
		uintptr_t object;
		uint32_t id, status, playing, node;
		uintptr_t event;
		float position[3];
		float distance;
	};

	// The sound object list, behind its own lock (tried, not waited for)
	uint32_t ReadObjects(uintptr_t list, RawObject* out, uint32_t capacity)
	{
		__try
		{
			const uint8_t* l = reinterpret_cast<const uint8_t*>(list);
			const uint32_t count = *reinterpret_cast<const uint32_t*>(l + kListCount);
			uintptr_t object = *reinterpret_cast<const uintptr_t*>(l + kListFirst);
			uint32_t got = 0;
			for (uint32_t i = 0; object && i < count && got < capacity; i++)
			{
				const uint8_t* o = reinterpret_cast<const uint8_t*>(object);
				RawObject& r = out[got++];
				r.object = object;
				r.id = *reinterpret_cast<const uint32_t*>(o);
				r.status = *reinterpret_cast<const uint32_t*>(o + kObjectStatus);
				r.event = *reinterpret_cast<const uintptr_t*>(o + kObjectEvent);
				memcpy(r.position, o + kObjectPosition, sizeof(r.position));
				r.playing = *reinterpret_cast<const uint32_t*>(o + kObjectPlaying);
				r.node = *reinterpret_cast<const uint32_t*>(o + kObjectNode);
				r.distance = *reinterpret_cast<const float*>(o + kObjectDistance);
				object = *reinterpret_cast<const uintptr_t*>(o + kObjectNext);
			}
			return got;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	struct RawItem
	{
		int voices;
		uint32_t eventId, objectId, playingId;
	};

	uint32_t ReadPlayingTable(uintptr_t table, RawItem* out, uint32_t capacity)
	{
		__try
		{
			uint32_t got = 0;
			for (uint32_t b = 0; b < kTableBuckets; b++)
			{
				uintptr_t item = reinterpret_cast<const uintptr_t*>(table)[b];
				for (int guard = 0; item && guard < 4096 && got < capacity; guard++)
				{
					const uint8_t* i = reinterpret_cast<const uint8_t*>(item);
					RawItem& r = out[got++];
					r.voices = *reinterpret_cast<const int*>(i);
					r.eventId = *reinterpret_cast<const uint32_t*>(i + kItemEvent);
					r.objectId = *reinterpret_cast<const uint32_t*>(i + kItemObject);
					r.playingId = *reinterpret_cast<const uint32_t*>(i + kItemPlaying);
					item = *reinterpret_cast<const uintptr_t*>(i + kItemNext);
				}
			}
			return got;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	struct RawSpeech
	{
		int type, status, subStatus;
		uint32_t eventId, objectId, playingId;
		uintptr_t character, request;
		float timeout;
		char arguments[160];
	};

	uint32_t ReadSpeech(uintptr_t speech, RawSpeech* out, uint32_t capacity, uintptr_t* slots, uint32_t& slotCount)
	{
		__try
		{
			const uint8_t* s = reinterpret_cast<const uint8_t*>(speech);
			const uintptr_t pool = *reinterpret_cast<const uintptr_t*>(s + kSpeechPool);
			const uintptr_t requests = pool ? *reinterpret_cast<const uintptr_t*>(pool) : 0;
			const uint32_t max = (std::min)(*reinterpret_cast<const uint32_t*>(s + kSpeechMax), 64u);
			const uintptr_t* slotList = *reinterpret_cast<const uintptr_t* const*>(s + kSpeechSlots);
			slotCount = slotList ? (std::min)(*reinterpret_cast<const uint32_t*>(s + kSpeechSlotCount), 8u) : 0;
			for (uint32_t i = 0; i < slotCount; i++)
				slots[i] = slotList[i];
			uint32_t got = 0;
			for (uint32_t i = 0; requests && i < max && got < capacity; i++)
			{
				const uint8_t* r = reinterpret_cast<const uint8_t*>(requests + i * kRequestStride);
				const int status = *reinterpret_cast<const int*>(r + kRequestStatus);
				if (status <= 0 || status > 5)
					continue;
				RawSpeech& o = out[got++];
				o.request = reinterpret_cast<uintptr_t>(r);
				o.type = *reinterpret_cast<const int*>(r);
				o.status = status;
				o.subStatus = *reinterpret_cast<const int*>(r + kRequestSubStatus);
				const uintptr_t event = *reinterpret_cast<const uintptr_t*>(r + kRequestEvent);
				o.eventId = event ? *reinterpret_cast<const uint32_t*>(event) : 0;
				const uintptr_t object = *reinterpret_cast<const uintptr_t*>(r + kRequestObject);
				o.objectId = object ? *reinterpret_cast<const uint32_t*>(object) : 0;
				o.character = *reinterpret_cast<const uintptr_t*>(r + kRequestCharacter);
				o.playingId = *reinterpret_cast<const uint32_t*>(r + kRequestPlaying);
				o.timeout = *reinterpret_cast<const float*>(r + kRequestTimeout);
				const uint8_t count = r[kRequestArgumentCount];
				size_t length = 0;
				for (uint8_t a = 0; a < count && a < 16; a++)
				{
					const char* name = *reinterpret_cast<const char* const*>(r + kRequestArguments + a * 8);
					if (!name)
						continue;
					if (length && length + 2 < sizeof(o.arguments))
					{
						o.arguments[length++] = ',';
						o.arguments[length++] = ' ';
					}
					for (size_t c = 0; name[c] && length + 1 < sizeof(o.arguments); c++)
						o.arguments[length++] = name[c];
				}
				o.arguments[length] = '\0';
			}
			return got;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	struct RawMusic
	{
		bool on;
		uint32_t event, playing, requestEvent;
		int state, mode, queue;
		float target, current;
		uintptr_t object;
	};

	bool ReadMusic(uintptr_t music, RawMusic& out)
	{
		__try
		{
			const uint8_t* m = reinterpret_cast<const uint8_t*>(music);
			const uintptr_t event = *reinterpret_cast<const uintptr_t*>(m + kMusicEvent);
			out.event = event ? *reinterpret_cast<const uint32_t*>(event) : 0;
			const uintptr_t request = *reinterpret_cast<const uintptr_t*>(m + kMusicRequestEvent);
			out.requestEvent = request ? *reinterpret_cast<const uint32_t*>(request) : 0;
			out.queue = *reinterpret_cast<const int*>(m + kMusicQueue);
			out.playing = *reinterpret_cast<const uint32_t*>(m + kMusicPlaying);
			out.state = *reinterpret_cast<const int*>(m + kMusicState);
			out.target = *reinterpret_cast<const float*>(m + kMusicTarget);
			out.current = *reinterpret_cast<const float*>(m + kMusicCurrent);
			out.on = m[kMusicOn] != 0;
			out.mode = *reinterpret_cast<const int*>(m + kMusicMode);
			out.object = music + kMusicObject;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	struct RawBank
	{
		char name[96];
		int references;
		bool loading, loaded;
	};

	uint32_t ReadBanks(uintptr_t database, RawBank* out, uint32_t capacity)
	{
		__try
		{
			uintptr_t node = *reinterpret_cast<const uintptr_t*>(database + kBanks);
			uint32_t got = 0;
			for (int guard = 0; node && guard < 1024 && got < capacity; guard++)
			{
				const uint8_t* bank = *reinterpret_cast<const uint8_t* const*>(node);
				if (bank)
				{
					RawBank& r = out[got++];
					const char* name = *reinterpret_cast<const char* const*>(bank);
					size_t i = 0;
					for (; name && i + 1 < sizeof(r.name) && name[i]; i++)
						r.name[i] = name[i];
					r.name[i] = '\0';
					r.references = *reinterpret_cast<const int*>(bank + 8);
					r.loading = bank[0x0C] != 0;
					r.loaded = bank[0x0D] != 0;
				}
				node = *reinterpret_cast<const uintptr_t*>(node + 8);
			}
			return got;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	// A character's sound objects and script entity ids
	struct RawCharacterSounds
	{
		uintptr_t objects[4];
		uint32_t ids[4];
		uint32_t handle[2];
	};

	bool ReadCharacterSounds(const uint8_t* character, RawCharacterSounds& out)
	{
		__try
		{
			for (int i = 0; i < 4; i++)
			{
				out.objects[i] = *reinterpret_cast<const uintptr_t*>(character + kCharacterSounds + i * 4);
				out.ids[i] = out.objects[i] ? *reinterpret_cast<const uint32_t*>(out.objects[i]) : 0;
			}
			out.handle[0] = *reinterpret_cast<const uint32_t*>(character + kCharacterHandle);
			out.handle[1] = *reinterpret_cast<const uint32_t*>(character + kCharacterHandle + 4);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// Keeps the game's lock for the rest of a scope, once it is had
	struct LockHeld
	{
		CRITICAL_SECTION* lock;
		~LockHeld() { if (lock) LeaveCriticalSection(lock); }
	};

	bool TryLock(uintptr_t at, CRITICAL_SECTION*& lock)
	{
		lock = nullptr;
		if (!at)
			return false;
		__try
		{
			if (TryEnterCriticalSection(reinterpret_cast<CRITICAL_SECTION*>(at)))
			{
				lock = reinterpret_cast<CRITICAL_SECTION*>(at);
				return true;
			}
			return false;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	void ReadSnapshot()
	{
		SOUND_DEBUG::Snapshot& s = g_snapshot;
		s.now = Now();
		const uintptr_t sound = SoundInterface();
		if (!sound || !LevelRunning())
		{
			s.have = false;
			s.haveNetwork = false;
			s.objects.clear();
			s.playing.clear();
			s.speech.clear();
			if (!g_nodes.empty() || !g_networks.empty())
			{
				g_nodes.clear();
				g_networks.clear();
				g_barriers.clear();
				g_cacheKey = CacheKey();
			}
			return;
		}

		CRITICAL_SECTION* held = nullptr;
		if (!TryLock(DEVTOOLS_RELATIVE_ADDRESS(kUpdateMutex), held))
		{
			s.lockMisses++;
			return;
		}
		LockHeld unlock{ held };

		ReadEvents(sound);
		RawInterface iface;
		if (!ReadInterface(sound, iface) || iface.closing)
		{
			s.closing = true;
			return;
		}
		s.have = true;
		s.enabled = iface.enabled;
		s.paused = iface.paused;
		s.closing = iface.closing;
		memcpy(s.listener, iface.listener, sizeof(s.listener));
		memcpy(s.listenerForward, iface.forward, sizeof(s.listenerForward));
		s.alienState = iface.alienState;
		s.alienDistance = iface.alienDistance;
		s.stealth = iface.stealth;
		s.threat = iface.threat;
		memcpy(s.stateHistory, iface.states, sizeof(s.stateHistory));
		s.stateCount = iface.stateCount;
		s.generation = g_generation;

		// The network: copied once per level, then its live parts
		s.haveNetwork = false;
		const uintptr_t network = Part(sound, kPartNetwork);
		RawNetworkState state;
		std::vector<uintptr_t> ambienceObjects;
		if (network && ReadNetworkState(network, state))
		{
			state.key.generation = g_generation;
			if (!(state.key == g_cacheKey))
			{
				BuildNetworkCache(state.key, state.reverbs, state.reverbCount);
				ApplyNetworkNames();
			}
			s.haveNetwork = !g_nodes.empty();
			s.networkEnabled = state.on;
			s.occlusion = state.occlusion;
			s.reverb = state.reverb;
			s.listenerIsPlayerHead = state.head;
			s.roomType = state.roomType;
			for (int i = 0; i < 2; i++)
				s.listenerNodes[i] = state.listener[i] >= state.key.nodes && state.listener[i] < state.key.nodes + state.key.nodeCount * kNodeStride ?
					static_cast<int>((state.listener[i] - state.key.nodes) / kNodeStride) : -1;
			s.listenerNetwork = s.listenerNodes[0] >= 0 && s.listenerNodes[0] < static_cast<int>(g_nodes.size()) ? g_nodes[s.listenerNodes[0]].network : -1;
			s.activeBarrier = state.active >= state.key.barriers && state.active < state.key.barriers + state.key.barrierCount * kBarrierStride ?
				static_cast<int>((state.active - state.key.barriers) / kBarrierStride) : -1;
			if (s.haveNetwork)
			{
				std::vector<float> states(g_barriers.size());
				std::unique_ptr<bool[]> on(new bool[g_networks.size() + 1]);
				std::vector<float> blend(g_networks.size());
				ambienceObjects.resize(g_networks.size());
				if (ReadNetworkLive(g_cacheKey, states.data(), static_cast<uint32_t>(states.size()), on.get(), blend.data(), ambienceObjects.data(),
					static_cast<uint32_t>(g_networks.size())))
				{
					for (size_t i = 0; i < g_barriers.size(); i++)
						g_barriers[i].state = states[i];
					for (size_t i = 0; i < g_networks.size(); i++)
					{
						g_networks[i].ambienceActive = on[i];
						g_networks[i].ambienceBlend = blend[i];
					}
				}
				std::lock_guard<std::mutex> lock(g_hookMutex);
				for (SOUND_DEBUG::Barrier& barrier : g_barriers)
				{
					const auto found = g_barrierSets.find(barrier.instanceId);
					barrier.timesSet = found == g_barrierSets.end() ? 0 : found->second;
				}
			}
		}

		// Who owns which sound object
		std::unordered_map<uintptr_t, std::string> owners;
		owners[sound + kGlobalObject] = "Global";
		const uintptr_t music = Part(sound, kPartMusic);
		RawMusic rawMusic = {};
		s.haveMusic = music && ReadMusic(music, rawMusic);
		if (s.haveMusic)
		{
			owners[rawMusic.object] = "Music";
			s.musicEnabled = rawMusic.on;
			s.musicEvent = rawMusic.event;
			s.musicPlayingId = rawMusic.playing;
			s.musicRequestEvent = rawMusic.requestEvent;
			s.musicState = rawMusic.state;
			s.musicMode = rawMusic.mode;
			s.musicQueue = rawMusic.queue;
			s.musicTarget = rawMusic.target;
			s.musicCurrent = rawMusic.current;
		}
		for (size_t i = 0; i < ambienceObjects.size(); i++)
			if (ambienceObjects[i])
				owners[ambienceObjects[i]] = "Ambience of " + g_networks[i].name;
		// A character's four, by what they play ("Player" rather than the name the game gives the player, "Character: 0")
		static const char* const kSlotNames[4] = { "speech", "general sounds", "breathing", "unoccluded sounds" };
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
		{
			RawCharacterSounds sounds;
			if (!ReadCharacterSounds(character.object, sounds))
				continue;
			const std::string who = character.player ? std::string("Player") : character.name;
			for (int i = 0; i < 4; i++)
				if (sounds.objects[i])
					owners[sounds.objects[i]] = who + " (" + kSlotNames[i] + ")";
		}

		// The sound objects
		s.objects.clear();
		const uintptr_t list = Pointer(sound + kObjectList);
		if (list)
		{
			CRITICAL_SECTION* listHeld = nullptr;
			if (TryLock(list + kListLock, listHeld))
			{
				LockHeld unlockList{ listHeld };
				static RawObject rawObjects[4096];
				const uint32_t count = ReadObjects(list, rawObjects, 4096);
				std::lock_guard<std::mutex> lock(g_namesMutex);
				for (uint32_t i = 0; i < count; i++)
				{
					const RawObject& r = rawObjects[i];
					SOUND_DEBUG::SoundObject object;
					object.object = reinterpret_cast<const void*>(r.object);
					object.id = r.id;
					object.status = r.status;
					uint32_t eventId = 0;
					object.lastEvent = r.event && Read(r.event, eventId) ? eventId : 0;
					memcpy(object.position, r.position, sizeof(object.position));
					object.playing = static_cast<int>(r.playing);
					object.node = r.node < 0xFFFF ? static_cast<int>(r.node) : -1;
					object.distance = r.distance;
					const auto owner = owners.find(r.object);
					if (owner != owners.end())
						object.owner = owner->second;
					else
					{
						const auto named = g_objectNames.find(object.object);
						if (named != g_objectNames.end())
							object.owner = named->second;
					}
					s.objects.push_back(std::move(object));
				}
			}
		}

		// Speech
		const uintptr_t speech = Part(sound, kPartSpeech);
		s.speech.clear();
		if (speech)
		{
			static RawSpeech rawSpeech[64];
			uintptr_t slots[8] = {};
			uint32_t slotCount = 0;
			const uint32_t count = ReadSpeech(speech, rawSpeech, 64, slots, slotCount);
			for (uint32_t i = 0; i < count; i++)
			{
				SOUND_DEBUG::Speech line;
				line.type = rawSpeech[i].type;
				line.eventId = rawSpeech[i].eventId;
				line.character = reinterpret_cast<const void*>(rawSpeech[i].character);
				line.objectId = rawSpeech[i].objectId;
				line.status = rawSpeech[i].status;
				line.subStatus = rawSpeech[i].subStatus;
				line.playingId = rawSpeech[i].playingId;
				line.timeout = rawSpeech[i].timeout;
				line.arguments = rawSpeech[i].arguments;
				for (uint32_t k = 0; k < slotCount; k++)
					line.inSlot |= slots[k] == rawSpeech[i].request;
				s.speech.push_back(std::move(line));
			}
			std::stable_sort(s.speech.begin(), s.speech.end(), [](const SOUND_DEBUG::Speech& a, const SOUND_DEBUG::Speech& b) { return a.status > b.status; });
		}

		// Sound banks
		const uintptr_t database = Pointer(sound + kEventTable);
		s.banks.clear();
		if (database)
		{
			static RawBank rawBanks[512];
			const uint32_t count = ReadBanks(database, rawBanks, 512);
			for (uint32_t i = 0; i < count; i++)
				s.banks.push_back({ rawBanks[i].name, rawBanks[i].references, rawBanks[i].loading, rawBanks[i].loaded });
		}

		unlock.lock = nullptr;
		LeaveCriticalSection(held);

		// What the sound engine is playing, behind its own lock
		s.playing.clear();
		const uintptr_t table = Pointer(DEVTOOLS_RELATIVE_ADDRESS(kPlayingTable));
		if (table)
		{
			CRITICAL_SECTION* tableHeld = nullptr;
			static RawItem items[2048];
			uint32_t count = 0;
			if (TryLock(table + kTableLock, tableHeld))
			{
				count = ReadPlayingTable(table, items, 2048);
				LeaveCriticalSection(tableHeld);
			}
			std::unordered_map<uint32_t, int> byId;
			for (size_t i = 0; i < s.objects.size(); i++)
				byId[s.objects[i].id] = static_cast<int>(i);
			std::lock_guard<std::mutex> lock(g_hookMutex);
			for (uint32_t i = 0; i < count; i++)
			{
				SOUND_DEBUG::Playing playing;
				playing.playingId = items[i].playingId;
				playing.eventId = items[i].eventId;
				playing.objectId = items[i].objectId;
				playing.voices = items[i].voices;
				const auto object = byId.find(items[i].objectId);
				playing.object = object == byId.end() ? -1 : object->second;
				const auto started = g_playStarts.find(items[i].playingId);
				playing.startedAt = started == g_playStarts.end() ? -1.0f : started->second;
				s.playing.push_back(playing);
			}
			// What has finished playing is forgotten now and then
			if (g_playStarts.size() > 4096)
			{
				std::unordered_map<uint32_t, float> kept;
				for (const SOUND_DEBUG::Playing& playing : s.playing)
				{
					const auto started = g_playStarts.find(playing.playingId);
					if (started != g_playStarts.end())
						kept.emplace(*started);
				}
				g_playStarts.swap(kept);
			}
		}
	}

	bool WriteByte(uintptr_t at, uint8_t value)
	{
		__try
		{
			*reinterpret_cast<volatile uint8_t*>(at) = value;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	void SetNetworkByte(uint32_t offset, bool on)
	{
		DEBUG_MENU::OnEntityThread([offset, on]()
		{
			const uintptr_t sound = SoundInterface();
			const uintptr_t network = sound ? Part(sound, kPartNetwork) : 0;
			if (network)
				WriteByte(network + offset, on ? 1 : 0);
		});
	}
}

// ---- Hooks ----

__declspec(noinline)
uint32_t __fastcall SOUND_DEBUG::h_play_event(void* playback, void* /*_EDX*/, const void* event, const void* params, void* object, void* callback, bool keepOpen)
{
	const uint32_t id = play_event(playback, event, params, object, callback, keepOpen);
	if (g_attached)
		NotePlay(event, params, object, id);
	return id;
}

__declspec(noinline)
float __fastcall SOUND_DEBUG::h_heard_intensity(void* behaviour, void* /*_EDX*/, const void* sender, const float* position, float loudness, bool useNetwork, float distanceScale)
{
	const float result = heard_intensity(behaviour, sender, position, loudness, useNetwork, distanceScale);
	if (g_attached)
		NoteHeard(behaviour, sender, position, loudness, useNetwork, distanceScale, result);
	return result;
}

__declspec(noinline)
void __fastcall SOUND_DEBUG::h_record_stimulus(void* publisher, void* /*_EDX*/, void* message, bool clearFirst)
{
	if (g_attached)
		NoteStimulus(message);
	record_stimulus(publisher, message, clearFirst);
}

__declspec(noinline)
void __fastcall SOUND_DEBUG::h_set_barrier_state(void* network, void* /*_EDX*/, uint32_t instanceId, float state)
{
	DoorRecord record;
	const bool note = g_attached && network && ReadBarrierBefore(static_cast<const uint8_t*>(network), instanceId, record.barrier, record.before);
	set_barrier_state(network, instanceId, state);
	if (!note)
		return;
	record.time = Now();
	record.instanceId = instanceId;
	record.after = state;
	std::lock_guard<std::mutex> lock(g_hookMutex);
	g_barrierSets[instanceId]++;
	// A door moving sets its doorway every frame: sets of the same doorway within a second of each other are one entry
	SOUND_DEBUG::DoorRecord* last = g_doors.Last();
	if (last && last->instanceId == instanceId && record.time - last->time < 1.0f)
	{
		last->after = state;
		last->time = record.time;
		last->sets++;
		return;
	}
	g_doors.Push(record);
}

__declspec(noinline)
void __fastcall SOUND_DEBUG::h_load_network(void* network, void* /*_EDX*/, const char* path)
{
	load_network(network, path);
	if (!g_attached)
		return;
	char copied[MAX_PATH] = {};
	CopyText(path, copied, sizeof(copied));
	std::vector<std::string> names;
	const bool read = copied[0] && ReadNetworkNames(copied, names);
	DevTools::Log("Debug menu: sound network %s loaded (%s)", copied, read ? "names read" : "its names could not be read");
	std::lock_guard<std::mutex> lock(g_hookMutex);
	g_networkFile = copied;
	g_networkNames.swap(names);
	g_barrierSets.clear();
	g_doors.Clear();
	g_generation++;
}

__declspec(noinline)
uint32_t __cdecl SOUND_DEBUG::h_name_to_id(const char* name)
{
	const uint32_t id = name_to_id(name);
	if (g_attached && id && name)
	{
		char copied[128];
		if (CopyText(name, copied, sizeof(copied)) && copied[0])
		{
			std::lock_guard<std::mutex> lock(g_namesMutex);
			if (g_names.size() < 200000)
				g_names.emplace(id, copied);
		}
	}
	return id;
}

__declspec(noinline)
void __fastcall SOUND_DEBUG::h_register_object(void* object, void* /*_EDX*/, const char* name)
{
	register_object(object, name);
	if (g_attached && object && name)
	{
		char copied[96];
		if (CopyText(name, copied, sizeof(copied)) && copied[0])
		{
			std::lock_guard<std::mutex> lock(g_namesMutex);
			g_objectNames[object] = copied;
		}
	}
}

__declspec(noinline)
void __fastcall SOUND_DEBUG::h_unregister_object(void* object, void* /*_EDX*/)
{
	if (g_attached && object)
	{
		std::lock_guard<std::mutex> lock(g_namesMutex);
		g_objectNames.erase(object);
	}
	unregister_object(object);
}

__declspec(noinline)
void __fastcall SOUND_DEBUG::h_schedule_subtitle(void* screen, void* /*_EDX*/, const char* key, uint32_t originId, uint32_t priority)
{
	schedule_subtitle(screen, key, originId, priority);
	if (!g_attached || !screen)
		return;
	RawSubtitle raw;
	if (!ReadSubtitle(static_cast<const uint8_t*>(screen), raw))
		return;
	std::lock_guard<std::mutex> lock(g_hookMutex);
	if (!raw.key[0])
	{
		g_subtitle = Subtitle();
		return;
	}
	if (g_subtitle.key == raw.key && g_subtitle.originId == raw.origin)
		return;
	g_subtitle.time = Now();
	g_subtitle.key = raw.key;
	g_subtitle.text = raw.text;
	g_subtitle.originId = raw.origin;
	g_subtitle.priority = raw.priority;
	g_subtitles.Push(g_subtitle);
}

__declspec(noinline)
bool __fastcall SOUND_DEBUG::h_script_sound_start(void* sound, void* /*_EDX*/, void* entity, void* trigger)
{
	const uint32_t savedEntity = t_scriptEntity, savedInstance = t_scriptInstance;
	if (g_attached && entity)
		ReadEntityIds(entity, t_scriptEntity, t_scriptInstance);
	const bool result = script_sound_start(sound, entity, trigger);
	t_scriptEntity = savedEntity;
	t_scriptInstance = savedInstance;
	return result;
}

__declspec(noinline)
void __fastcall SOUND_DEBUG::h_npc_dialogue(void* manager, void* /*_EDX*/, void* character, int dialogueEvent, int dialogueState)
{
	if (g_attached)
	{
		DialogueRecord record;
		record.time = Now();
		record.character = character;
		record.dialogueEvent = dialogueEvent;
		record.dialogueState = dialogueState;
		std::lock_guard<std::mutex> lock(g_hookMutex);
		g_dialogue.Push(record);
	}
	npc_dialogue(manager, character, dialogueEvent, dialogueState);
}

void SOUND_DEBUG::AttachHooks(bool attach)
{
	struct Hook { void** original; void* detoured; };
	const Hook hooks[] = {
		{ reinterpret_cast<void**>(&play_event), reinterpret_cast<void*>(&h_play_event) },
		{ reinterpret_cast<void**>(&heard_intensity), reinterpret_cast<void*>(&h_heard_intensity) },
		{ reinterpret_cast<void**>(&record_stimulus), reinterpret_cast<void*>(&h_record_stimulus) },
		{ reinterpret_cast<void**>(&set_barrier_state), reinterpret_cast<void*>(&h_set_barrier_state) },
		{ reinterpret_cast<void**>(&load_network), reinterpret_cast<void*>(&h_load_network) },
		{ reinterpret_cast<void**>(&name_to_id), reinterpret_cast<void*>(&h_name_to_id) },
		{ reinterpret_cast<void**>(&register_object), reinterpret_cast<void*>(&h_register_object) },
		{ reinterpret_cast<void**>(&unregister_object), reinterpret_cast<void*>(&h_unregister_object) },
		{ reinterpret_cast<void**>(&schedule_subtitle), reinterpret_cast<void*>(&h_schedule_subtitle) },
		{ reinterpret_cast<void**>(&script_sound_start), reinterpret_cast<void*>(&h_script_sound_start) },
		{ reinterpret_cast<void**>(&npc_dialogue), reinterpret_cast<void*>(&h_npc_dialogue) },
	};
	static_assert(sizeof(hooks) / sizeof(hooks[0]) == sizeof(g_hooked) / sizeof(g_hooked[0]), "a flag per hook");
	if (attach)
	{
		SeedNames();
		int attached = 0;
		// Hooked only where this build has the function (it always does, for the three known builds)
		for (size_t i = 0; i < sizeof(hooks) / sizeof(hooks[0]); i++)
			if ((g_hooked[i] = *hooks[i].original != nullptr))
			{
				DetourAttach(hooks[i].original, hooks[i].detoured);
				attached++;
			}
		DevTools::Log("Debug menu: %d of %d sound hooks attached", attached, static_cast<int>(sizeof(hooks) / sizeof(hooks[0])));
		g_attached = true;
	}
	else
	{
		g_attached = false;
		for (size_t i = 0; i < sizeof(hooks) / sizeof(hooks[0]); i++)
			if (g_hooked[i])
				DetourDetach(hooks[i].original, hooks[i].detoured);
	}
}

// ---- For the views ----

const SOUND_DEBUG::Snapshot& SOUND_DEBUG::Read()
{
	const int frame = ImGui::GetFrameCount();
	if (g_snapshot.frame != frame)
	{
		g_snapshot.frame = frame;
		ReadSnapshot();
	}
	return g_snapshot;
}

const std::vector<SOUND_DEBUG::Node>& SOUND_DEBUG::Nodes()
{
	return g_nodes;
}

const std::vector<SOUND_DEBUG::Network>& SOUND_DEBUG::Networks()
{
	return g_networks;
}

const std::vector<SOUND_DEBUG::Barrier>& SOUND_DEBUG::Barriers()
{
	return g_barriers;
}

const std::vector<uint32_t>& SOUND_DEBUG::Reverbs()
{
	return g_reverbs;
}

const DEBUG_MENU::Character* SOUND_DEBUG::CharacterByObject(const void* character)
{
	if (!character)
		return nullptr;
	for (const DEBUG_MENU::Character& c : DEBUG_MENU::Characters())
		if (c.object == character)
			return &c;
	return nullptr;
}

const DEBUG_MENU::Character* SOUND_DEBUG::CharacterByHandle(uint32_t entity, uint32_t instance)
{
	if (!entity)
		return nullptr;
	for (const DEBUG_MENU::Character& c : DEBUG_MENU::Characters())
	{
		RawCharacterSounds sounds;
		if (ReadCharacterSounds(c.object, sounds) && sounds.handle[0] == entity && sounds.handle[1] == instance)
			return &c;
	}
	return nullptr;
}

const DEBUG_MENU::Character* SOUND_DEBUG::CharacterBySoundObject(uint32_t objectId, int* which)
{
	if (!objectId)
		return nullptr;
	for (const DEBUG_MENU::Character& c : DEBUG_MENU::Characters())
	{
		RawCharacterSounds sounds;
		if (!ReadCharacterSounds(c.object, sounds))
			continue;
		for (int i = 0; i < 4; i++)
			if (sounds.ids[i] == objectId)
			{
				if (which)
					*which = i;
				return &c;
			}
	}
	return nullptr;
}

int SOUND_DEBUG::CharacterNode(const void* character)
{
	return character ? NodeOf(reinterpret_cast<uintptr_t>(character), 1) : -1;
}

std::string SOUND_DEBUG::EventName(uint32_t id)
{
	const auto found = g_events.find(id);
	return found == g_events.end() ? std::string() : found->second.name;
}

float SOUND_DEBUG::EventMaxDistance(uint32_t id)
{
	const auto found = g_events.find(id);
	return found == g_events.end() ? 0.0f : found->second.maxDistance;
}

std::string SOUND_DEBUG::Name(uint32_t id)
{
	const auto event = g_events.find(id);
	if (event != g_events.end())
		return event->second.name;
	std::lock_guard<std::mutex> lock(g_namesMutex);
	const auto found = g_names.find(id);
	return found == g_names.end() ? std::string() : found->second;
}

int SOUND_DEBUG::NearestNode(const float position[3])
{
	int best = -1;
	float bestSquared = kNodeSnapSquared;
	for (const Network& network : g_networks)
	{
		if (network.nodes.size() < 2)
			continue;
		bool inside = true;
		for (int a = 0; a < 3; a++)
			inside &= network.boundsMin[a] <= position[a] && position[a] <= network.boundsMax[a];
		if (!inside)
			continue;
		for (int n : network.nodes)
		{
			const float dx = g_nodes[n].position[0] - position[0], dy = g_nodes[n].position[1] - position[1], dz = g_nodes[n].position[2] - position[2];
			const float squared = dx * dx + dy * dy + dz * dz;
			if (squared < bestSquared)
			{
				bestSquared = squared;
				best = n;
			}
		}
	}
	return best;
}

namespace
{
	// The direct link between two nodes (on the lower-numbered one), added to a distance and obstruction; false when
	// there is none (the same node counts as linked, at no cost)
	bool AddLink(int a, int b, float& distance, float& obstruction)
	{
		if (a == b)
			return true;
		const int low = (std::min)(a, b), high = (std::max)(a, b);
		for (const SOUND_DEBUG::Node::Link& link : g_nodes[low].links)
			if (link.other == high)
			{
				distance += link.distance;
				obstruction += link.obstruction;
				return true;
			}
		return false;
	}
}

SOUND_DEBUG::Route SOUND_DEBUG::ShortestRoute(int fromNode, int toNode)
{
	Route route;
	const int nodeCount = static_cast<int>(g_nodes.size());
	if (fromNode < 0 || toNode < 0 || fromNode >= nodeCount || toNode >= nodeCount)
		return route;

	// As the game searches: one entry per doorway, then the start and the end, opened in order of distance
	struct Entry
	{
		int state = 0;            // 0 unused, 1 open, 2 closed
		float product = 1.0f;     // of (1 - each doorway's state) so far
		float distance = 0.0f, obstruction = 0.0f;
		int parent = -1, node = -1;
	};
	const int barrierCount = static_cast<int>(g_barriers.size());
	const int start = barrierCount, end = barrierCount + 1;
	std::vector<Entry> entries(barrierCount + 2);
	std::vector<int> open;
	auto insert = [&](int entry)
	{
		auto at = open.begin();
		while (at != open.end() && !(entries[entry].distance < entries[*at].distance))
			++at;
		open.insert(at, entry);
	};
	auto relax = [&](int entry, const Entry& candidate)
	{
		Entry& current = entries[entry];
		if (current.state == 1)
		{
			if (current.distance < candidate.distance)
				return;
			open.erase(std::find(open.begin(), open.end(), entry));
		}
		current = candidate;
		current.state = 1;
		insert(entry);
	};

	entries[start].state = 1;
	entries[start].node = fromNode;
	open.push_back(start);
	const int endNetwork = g_nodes[toNode].network;
	for (int guard = 0; !open.empty() && guard < 100000; guard++)
	{
		const int p = open.front();
		const Entry parent = entries[p];
		if (parent.node == toNode)
		{
			route.found = true;
			route.occlusion = 1.0f - parent.product;
			route.distance = parent.distance;
			route.obstruction = parent.obstruction;
			// The way back: each doorway's far then near node
			std::vector<int> points;
			points.push_back(toNode);
			for (int e = entries[p].parent; e >= 0 && e != start; e = entries[e].parent)
			{
				const Barrier& barrier = g_barriers[e];
				points.push_back(entries[e].node);
				points.push_back(barrier.nodes[0] == entries[e].node ? barrier.nodes[1] : barrier.nodes[0]);
				route.barriers.insert(route.barriers.begin(), e);
			}
			points.push_back(fromNode);
			route.points.assign(points.rbegin(), points.rend());
			return route;
		}
		open.erase(open.begin());
		entries[p].state = 2;
		const int network = g_nodes[parent.node].network;
		if (network < 0)
			continue;
		if (network == endNetwork)
		{
			Entry candidate;
			AddLink(parent.node, toNode, candidate.distance, candidate.obstruction);
			candidate.distance += parent.distance;
			candidate.obstruction += parent.obstruction;
			candidate.product = parent.product;
			candidate.parent = p;
			candidate.node = toNode;
			relax(end, candidate);
		}
		for (int b : g_networks[network].barriers)
		{
			if (b < 0 || b >= barrierCount)
				continue;
			const Barrier& barrier = g_barriers[b];
			if (barrier.state == 1.0f || entries[b].state == 2)
				continue;
			const int nearNode = barrier.networks[0] == network ? barrier.nodes[0] : barrier.nodes[1];
			const int farNode = barrier.nodes[0] == nearNode ? barrier.nodes[1] : barrier.nodes[0];
			if (nearNode < 0 || farNode < 0)
				continue;
			Entry candidate;
			AddLink(parent.node, nearNode, candidate.distance, candidate.obstruction);
			if (AddLink(nearNode, farNode, candidate.distance, candidate.obstruction) && nearNode != farNode)
				candidate.product *= 1.0f - barrier.state;
			candidate.distance += parent.distance;
			candidate.obstruction += parent.obstruction;
			candidate.product *= parent.product;
			candidate.parent = p;
			candidate.node = farNode;
			relax(b, candidate);
		}
	}
	return route;
}

void SOUND_DEBUG::Hooked(std::vector<PlayRecord>& plays, std::vector<HeardRecord>& heard, std::vector<Stimulus>& stimuli,
	std::vector<DoorRecord>& doors, std::vector<DialogueRecord>& dialogue, std::vector<Subtitle>& subtitles, Subtitle& current)
{
	std::lock_guard<std::mutex> lock(g_hookMutex);
	g_plays.Copy(plays);
	g_heard.Copy(heard);
	g_stimuli.Copy(stimuli);
	g_doors.Copy(doors);
	g_dialogue.Copy(dialogue);
	g_subtitles.Copy(subtitles);
	current = g_subtitle;
}

void SOUND_DEBUG::ClearLogs()
{
	std::lock_guard<std::mutex> lock(g_hookMutex);
	g_plays.Clear();
	g_heard.Clear();
	g_stimuli.Clear();
	g_doors.Clear();
	g_dialogue.Clear();
	g_subtitles.Clear();
}

bool SOUND_DEBUG::HooksAttached()
{
	return g_attached;
}

std::string SOUND_DEBUG::NetworkFile()
{
	std::lock_guard<std::mutex> lock(g_hookMutex);
	return g_networkFile;
}

void SOUND_DEBUG::SetOcclusion(bool on)
{
	SetNetworkByte(kOcclusionOn, on);
}

void SOUND_DEBUG::SetReverb(bool on)
{
	SetNetworkByte(kReverbOn, on);
}

void SOUND_DEBUG::Label(ImDrawList* drawList, const float world[3], const char* text, uint32_t colour, float yOffsetPixels)
{
	ImVec2 at;
	if (!text || !text[0] || !DEBUG_MENU::Project(world, at))
		return;
	// Only for a point on screen: labels of things off to the side would pile up along the edge
	const ImVec2 display = ImGui::GetIO().DisplaySize;
	if (at.x < 0.0f || at.y < 0.0f || at.x > display.x || at.y > display.y)
		return;
	// As DEBUG_MENU::WorldText draws it, then moved in to keep a small margin from each edge (the left and top win when the
	// text is wider or taller than the screen)
	constexpr float kMargin = 6.0f;
	const float size = 15.0f * DEBUG_MENU::Scale();
	const ImVec2 extent = ImGui::GetFont()->CalcTextSizeA(size, FLT_MAX, 0.0f, text);
	ImVec2 corner(at.x - extent.x * 0.5f, at.y - extent.y + yOffsetPixels);
	corner.x = (std::max)((std::min)(corner.x, display.x - kMargin - extent.x), kMargin);
	corner.y = (std::max)((std::min)(corner.y, display.y - kMargin - extent.y), kMargin);
	drawList->AddRectFilled(ImVec2(corner.x - 4.0f, corner.y - 2.0f), ImVec2(corner.x + extent.x + 4.0f, corner.y + extent.y + 2.0f), IM_COL32(0, 0, 0, 150), 3.0f);
	drawList->AddText(ImGui::GetFont(), size, corner, colour, text);
}

const char* SOUND_DEBUG::StimulusName(int type)
{
	switch (type)
	{
	case 0: return "Footstep";
	case 1: return "Gunfire";
	case 2: return "Gunfire hitting the level";
	case 3: return "Body impact";
	case 4: return "Projectile impact";
	default: return "?";
	}
}

const char* SOUND_DEBUG::DialogueEventName(int dialogueEvent)
{
	static const char* const kNames[] = {
		"Suspicious warning", "Suspicious warning failed", "Missing buddy", "Search started", "Search loop", "Search completed",
		"Detected enemy", "Interrogative", "Warning", "Last chance", "Stand down", "Go to cover", "No cover", "Shoot from cover",
		"Cover broken", "Alamo", "Panic", "Attack", "Hit by weapon", "Block", "Final hit", "Ally death", "Incoming IED", "Alert squad",
		"Idle (passive)", "Idle (aggressive)", "Enter grapple", "Grapple from cover", "Player observed", "Suspicious item (initial)",
		"Suspicious item (close)", "Melee", "Advance", "My death", "Alien heard backstage", "Alien sighted", "Alien attacking me",
	};
	return dialogueEvent >= 0 && dialogueEvent < static_cast<int>(sizeof(kNames) / sizeof(kNames[0])) ? kNames[dialogueEvent] : "?";
}

const char* SOUND_DEBUG::DialogueStateName(int dialogueState)
{
	switch (dialogueState)
	{
	case 0: return "Idle";
	case 1: return "Search";
	case 2: return "Escalation";
	case 3: return "Combat";
	default: return "?";
	}
}

const char* SOUND_DEBUG::SpeechTypeName(int type)
{
	switch (type)
	{
	case 1: return "Low";
	case 2: return "Medium";
	case 3: return "High";
	case 4: return "Streamed combat";
	case 5: return "Combat";
	case 6: return "Death";
	default: return "?";
	}
}

const char* SOUND_DEBUG::SpeechStatusName(int status)
{
	switch (status)
	{
	case 1: return "Queued";
	case 2: return "Preparing lip sync";
	case 3: return "Waiting for lip sync";
	case 4: return "Playing";
	case 5: return "Finished";
	default: return "?";
	}
}

const char* SOUND_DEBUG::MusicModeName(int mode)
{
	switch (mode)
	{
	case 0: return "Unchanged";
	case 1: return "Player threat";
	case 2: return "Player stealth";
	case 3: return "Alien distance";
	case 4: return "Manual";
	case 5: return "Object distance";
	default: return "?";
	}
}

#include "LIVE_DEBUGGER.h"
#include "LIVE_LINK.h"
#include "LIVE_TRACE.h"
#include "DEBUG_MENU.h"
#include "Config.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <algorithm>
#include <atomic>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <mutex>
#include <string>
#include <vector>

/*
	Addresses and layouts are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).
	See LIVE_DEBUGGER.h for what this does.
*/
namespace
{
	uintptr_t Address(uintptr_t rva) { return DEVTOOLS_RELATIVE_ADDRESS(rva); }

	// ---- Game layouts ----
	constexpr uintptr_t kEntityManager = 0x0134ef40;   // a pointer to the entity manager
	constexpr uintptr_t kPackFile = 0x0134ef44;        // the loaded COMMANDS.PAK, which packed offsets count from (in 4-byte words)
	constexpr uintptr_t kPauseState = 0x012f194c;      // a pointer to the game's pause state, whose reasons to be paused (bits) come first
	constexpr uintptr_t kStringTable = 0x0134ef78;     // a pointer to the level's string table

	// The entity manager's fields
	constexpr uint32_t kManagerCalls = 0x00;           // the calls it is making (a counted pointer)
	constexpr uint32_t kManagerRootGuid = 0x0C;        // the running level's root composite id
	constexpr uint32_t kManagerTemplates = 0x18;       // the level's composite templates (an array of packed pointers)
	constexpr uint32_t kManagerRoot = 0x44;            // the root entity (the level's top instance)
	constexpr uint32_t kManagerClock = 0x90;           // the scripts' clock (seconds, a double)
	constexpr uint32_t kManagerBreakpoints = 0xB4;     // the breakpoint list (a counted array of the records below)
	constexpr uint32_t kManagerTransport = 0xBC;       // the scripts' transport: 1 paused, 5 running
	constexpr uint32_t kManagerInitialised = 0x105;
	constexpr uint32_t kManagerShuttingDown = 0x106;
	constexpr uint32_t kManagerUseBreakpoints = 0x109; // whether the list is looked in (set when the manager is made)
	constexpr uint32_t kTransportPaused = 1;

	// The calls being made: this round's (a counted array of pointers to call records, the one being made first)
	constexpr uint32_t kCallsRound = 0x10;
	// A call record: the method, its delay, the entity it goes to (a counted pointer), the record of the call whose
	// method fired the output that made it (a pointer, none for a call nothing caused), and when it is due (a double)
	constexpr uint32_t kCallMethod = 0x00;
	constexpr uint32_t kCallTarget = 0x08;
	constexpr uint32_t kCallCause = 0x0C;
	constexpr uint32_t kCallDue = 0x18;

	// A record of the breakpoint list, and the list's allocation's virtual slots: resize its storage (to a size in bytes),
	// and count one more or one fewer record
	struct GameRecord
	{
		int32_t id;
		uint32_t entity;    // the entity's id
		uint32_t instance;  // the id of the composite instance holding it, as the game names it in its records
		uint32_t method;
	};
	static_assert(sizeof(GameRecord) == 16, "breakpoint record layout");
	constexpr uint32_t kVResize = 0x24;
	constexpr uint32_t kVIncrement = 0x1C;
	constexpr uint32_t kVDecrement = 0x20;
	constexpr int kMaxRecords = 256;

	// An entity's fields: its state flags, its owner (the instance holding it, a counted pointer; none for the level's root
	// instance), its id and its type
	constexpr uint32_t kEntityState = 0x04;
	constexpr uint32_t kEntityOwner = 0x08;
	constexpr uint32_t kEntityGuid = 0x0C;
	constexpr uint32_t kEntityType = 0x10;
	// An instance's: its entities (an array) and the composite template it was made from (a packed pointer)
	constexpr uintptr_t kCompositeInstanceVTable = 0x00fe1d2c;
	constexpr uint32_t kInstanceEntities = 0x2C;
	constexpr uint32_t kInstanceTemplate = 0x30;
	// A template's: its name, its composite id and its running instances (an array)
	constexpr uint32_t kTemplateName = 0x00;
	constexpr uint32_t kTemplateGuid = 0x10;
	constexpr uint32_t kTemplateInstances = 0x64;
	// An entity's virtual slots: an instance's lookup of an entity by id (its children, then its aliases), and the
	// entity's interface (what its parameters are read through)
	constexpr uint32_t kVFindEntity = 0x0C;
	constexpr uint32_t kVGetInterface = 0x70;

	// The pause state's reasons this sets: a breakpoint, one round of script calls, one frame
	constexpr LONG kPauseBreakpoint = 0x80;
	constexpr LONG kPauseSingleCycle = 0x40;
	constexpr LONG kPauseSingleFrame = 0x20;

	// The game's call of the empty function a breakpoint reaches (in the check made before each script call), and the
	// empty function itself (shared by many callers: only the call is changed, never it)
	constexpr uintptr_t kHitCall = 0x00526af4;
	constexpr uintptr_t kEmptyFunction = 0x00669c90;

	// The readers of a parameter as one kind of value (through its links, as the entity itself reads it): the entity's
	// interface, the entity (a counted pointer, by reference), the parameter, where the value goes; true when found
	typedef bool(__thiscall* t_read)(void* iface, const void* entity, const uint32_t* parameter, void* out);
	constexpr uintptr_t kReadText = 0x001991a0;
	constexpr uintptr_t kReadBool = 0x00230590;
	constexpr uintptr_t kReadGuid = 0x002306a0;
	constexpr uintptr_t kReadInt = 0x00396570;
	constexpr uintptr_t kReadVector = 0x004d63c0;
	constexpr uintptr_t kReadFloat = 0x004d6510;
	constexpr uintptr_t kReadEnum = 0x004d6620;
	typedef const char* (__thiscall* t_string_from_offset)(void* table, uint32_t offset);
	constexpr uintptr_t kStringFromOffset = 0x00532160;

	// The entity manager's access lock (the game holds it while it makes its script calls)
	typedef void(__thiscall* t_access)(void* access);
	constexpr uintptr_t kAccessLock = 0x0051e1b0;
	constexpr uintptr_t kAccessUnlock = 0x0051e1d0;

	// Releases a counted reference to an entity
	typedef void(__thiscall* t_entity_release)(void* reference);
	constexpr uintptr_t kEntityRelease = 0x00198230;

	// The game's counted allocation: for a single item, data is the object; for an array, count and data are the array
	struct Allocation
	{
		void** vtable;
		volatile LONG references;
		volatile LONG count;
		void* data;
	};

	struct ManagerAccess
	{
		uint8_t storage[8] = {};
		bool held = false;
		ManagerAccess()
		{
			const auto lock = reinterpret_cast<t_access>(Address(kAccessLock));
			if (lock && Address(kAccessUnlock))
			{
				lock(storage);
				held = true;
			}
		}
		~ManagerAccess()
		{
			if (held)
				reinterpret_cast<t_access>(Address(kAccessUnlock))(storage);
		}
	};

	// ---- What is set ----
	struct Breakpoint
	{
		int id = 0;
		uint32_t root = 0;                 // the level it is for (0: any)
		uint32_t composite = 0;
		bool anyInstance = false;
		std::vector<uint32_t> path;        // instance entity ids from the root to the instance (none: the root itself)
		uint32_t entity = 0;
		std::vector<uint32_t> methods;
		std::string label;
		bool enabled = true;
		uint32_t hits = 0;
		int instances = 0;
		int records = 0;
		std::string problem;
		std::string compositeName;
	};

	// What each record written to the game stands for (its id is its place here, from 1)
	struct WrittenRecord
	{
		int breakpoint;
		uint32_t entity, instance, method;
	};

	// Everything below is behind g_mutex. The game's list is only written (and read back) while holding the game's own
	// lock as well, taken first: a hit, which comes while the game holds it, finds the records as they were written.
	std::mutex g_mutex;
	std::vector<Breakpoint> g_breakpoints;
	int g_nextId = 1;
	std::vector<WrittenRecord> g_written;
	std::string g_status;
	LIVE_DEBUGGER::HitView g_hit;

	std::atomic<bool> g_dirty{ true };        // the breakpoints changed: the game's list is written again next frame
	std::atomic<bool> g_patched{ false };
	std::atomic<bool> g_held{ false };        // the debugger set the pause state's breakpoint reason
	std::atomic<bool> g_atBreakpoint{ false };
	std::atomic<uint32_t> g_sequence{ 0 };
	std::atomic<uint32_t> g_version{ 1 };     // counts up when the breakpoints change (set, taken away, a hit counted)
	std::atomic<int> g_recordsInGame{ 0 };
	std::atomic<uint32_t> g_syncs{ 0 };       // the entity thread's passes over the list (a command that changed it waits for the next)
	std::atomic<bool> g_valuesWaiting{ false }; // a debugger.values request is queued for the entity thread and has not run yet

	// Named for OpenCAGE: whether breakpoints are in the game at all, and (read only - a frame puts them back) what OpenCAGE
	// polls cheaply in the Debug Menu's report: held 0/1, the hit count, the list's version
	std::atomic<bool> g_enabled{ true };
	std::atomic<float> g_heldValue{ 0.0f };
	std::atomic<float> g_hitValue{ 0.0f };
	std::atomic<float> g_versionValue{ 1.0f };

	// The entity thread's own
	uint32_t g_syncedRoot = 0;
	const void* g_syncedList = nullptr;
	ULONGLONG g_lastSync = 0;
	constexpr ULONGLONG kResyncMs = 1000;

	// The patched call (attach/detach only)
	uint8_t* g_site = nullptr;
	int32_t g_originalDistance = 0;

	void Publish()
	{
		g_heldValue = g_held ? 1.0f : 0.0f;
		g_hitValue = static_cast<float>(g_sequence.load() % 1000000u);
		g_versionValue = static_cast<float>(g_version.load() % 1000000u);
	}

	void Changed()
	{
		g_version++;
		g_dirty = true;
		Publish();
	}

	// ---- Ids as text ----
	std::string Hex8(uint32_t value)
	{
		char text[16];
		snprintf(text, sizeof(text), "%08X", value);
		return text;
	}

	std::string Path(const std::vector<uint32_t>& path)
	{
		std::string text;
		for (uint32_t step : path)
			text += (text.empty() ? "" : ",") + Hex8(step);
		return text;
	}

	// An id given as 8 hex digits (optionally 0x before them) or as OpenCAGE writes a ShortGuid ("AB-CD-EF-01", lowest byte first)
	bool ParseId(std::string text, uint32_t& value)
	{
		while (!text.empty() && isspace(static_cast<unsigned char>(text.back())))
			text.pop_back();
		while (!text.empty() && isspace(static_cast<unsigned char>(text.front())))
			text.erase(text.begin());
		if (text.empty())
			return false;
		if (text.find('-') != std::string::npos)
		{
			unsigned int b[4];
			char tail = 0;
			if (sscanf_s(text.c_str(), "%2x-%2x-%2x-%2x%c", &b[0], &b[1], &b[2], &b[3], &tail, 1) != 4)
				return false;
			value = b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
			return true;
		}
		if (text.size() > 2 && text[0] == '0' && (text[1] == 'x' || text[1] == 'X'))
			text = text.substr(2);
		if (text.empty() || text.size() > 8)
			return false;
		char* end = nullptr;
		const unsigned long parsed = strtoul(text.c_str(), &end, 16);
		if (!end || *end)
			return false;
		value = static_cast<uint32_t>(parsed);
		return true;
	}

	bool ParseIds(const std::string& text, std::vector<uint32_t>& ids)
	{
		ids.clear();
		size_t start = 0;
		while (start <= text.size())
		{
			const size_t comma = text.find(',', start);
			const std::string part = text.substr(start, comma == std::string::npos ? std::string::npos : comma - start);
			if (!part.empty())
			{
				uint32_t id = 0;
				if (!ParseId(part, id))
					return false;
				ids.push_back(id);
			}
			if (comma == std::string::npos)
				break;
			start = comma + 1;
		}
		return true;
	}

	// "key=value key=value ...": a key's value (to the next space; "label" takes the rest of the line), and whether it was there
	bool Field(const std::string& argument, const char* key, std::string& value)
	{
		const std::string wanted = std::string(key) + "=";
		size_t at = 0;
		while ((at = argument.find(wanted, at)) != std::string::npos)
		{
			if (at == 0 || isspace(static_cast<unsigned char>(argument[at - 1])))
			{
				const size_t begin = at + wanted.size();
				if (strcmp(key, "label") == 0)
				{
					value = argument.substr(begin);
					return true;
				}
				size_t end = begin;
				while (end < argument.size() && !isspace(static_cast<unsigned char>(argument[end])))
					end++;
				value = argument.substr(begin, end - begin);
				return true;
			}
			at += wanted.size();
		}
		return false;
	}

	// ---- The game's combining of two ids into one (how it makes a composite instance's id from the ids on its way down
	// from the level's root) ----
	uint32_t Combine(uint32_t a, uint32_t b)
	{
		if (b == 0)
			return a;
		if (a == 0)
			return b;
		uint64_t hash = (static_cast<uint64_t>(a) << 32) | b;
		hash = ~hash + (hash << 18);
		hash = (hash ^ (hash >> 31)) * 21;
		hash = (hash ^ (hash >> 11)) * 65;
		return static_cast<uint32_t>(hash >> 22) ^ static_cast<uint32_t>(hash);
	}

	// ---- Reading the game (each guarded, plain data in and out: a level can unload at any time) ----
	uint8_t* Manager()
	{
		const uintptr_t at = Address(kEntityManager);
		return at ? *reinterpret_cast<uint8_t**>(at) : nullptr;
	}

	uintptr_t PackFile()
	{
		const uintptr_t at = Address(kPackFile);
		return at ? *reinterpret_cast<uintptr_t*>(at) : 0;
	}

	const uint8_t* Object(const Allocation* allocation) { return allocation ? static_cast<const uint8_t*>(allocation->data) : nullptr; }

	bool IsInstance(const uint8_t* object)
	{
		return object && *reinterpret_cast<const uintptr_t*>(object) == Address(kCompositeInstanceVTable);
	}

	const uint8_t* Template(const uint8_t* instance)
	{
		const uint32_t packed = *reinterpret_cast<const uint32_t*>(instance + kInstanceTemplate);
		if (packed == 0xFFFFFFFF || !PackFile())
			return nullptr;
		return reinterpret_cast<const uint8_t*>(PackFile() + packed * 4u);
	}

	const uint8_t* FindTemplate(uint8_t* manager, uint32_t composite)
	{
		const uint32_t* templates = reinterpret_cast<const uint32_t*>(manager + kManagerTemplates);
		if (!PackFile() || templates[0] == 0xFFFFFFFF)
			return nullptr;
		const uint32_t* offsets = reinterpret_cast<const uint32_t*>(PackFile() + templates[0] * 4u);
		for (uint32_t i = 0; i < templates[1]; i++)
		{
			const uint8_t* candidate = reinterpret_cast<const uint8_t*>(PackFile() + offsets[i] * 4u);
			if (*reinterpret_cast<const uint32_t*>(candidate + kTemplateGuid) == composite)
				return candidate;
		}
		return nullptr;
	}

	Allocation* FindChild(const uint8_t* instance, uint32_t id)
	{
		const Allocation* entities = *reinterpret_cast<Allocation* const*>(instance + kInstanceEntities);
		if (!entities || !entities->data)
			return nullptr;
		Allocation* const* items = static_cast<Allocation* const*>(entities->data);
		for (LONG i = 0; i < entities->count; i++)
			if (items[i] && items[i]->data && *reinterpret_cast<const uint32_t*>(Object(items[i]) + kEntityGuid) == id)
				return items[i];
		return nullptr;
	}

	// The composite instance's id as the game's records name entities in it: its own id combined with each of its owners'
	// up to (and with) the level's root instance's
	uint32_t InstanceId(const Allocation* instance)
	{
		const uint8_t* object = Object(instance);
		uint32_t id = *reinterpret_cast<const uint32_t*>(object + kEntityGuid);
		for (const Allocation* owner = *reinterpret_cast<Allocation* const*>(object + kEntityOwner); owner && owner->data;
			owner = *reinterpret_cast<Allocation* const*>(Object(owner) + kEntityOwner))
			id = Combine(id, *reinterpret_cast<const uint32_t*>(Object(owner) + kEntityGuid));
		return id;
	}

	// Where an entity is: its id, the instance holding it (the game's id for it, the composite it is of, that composite's
	// name, and the path of instance entity ids to it from the root), its type and its state
	struct Place
	{
		uint32_t entity = 0, instance = 0, composite = 0, type = 0, state = 0;
		uint32_t path[64] = {};
		uint32_t pathCount = 0;
		char compositeName[96] = {};
	};

	void ReadPlace(const Allocation* entity, Place& place)
	{
		const uint8_t* object = Object(entity);
		place.entity = *reinterpret_cast<const uint32_t*>(object + kEntityGuid);
		place.type = *reinterpret_cast<const uint32_t*>(object + kEntityType);
		place.state = *reinterpret_cast<const uint32_t*>(object + kEntityState);
		const Allocation* owner = *reinterpret_cast<Allocation* const*>(object + kEntityOwner);
		place.instance = owner && owner->data ? InstanceId(owner) : 0;
		if (owner && owner->data && IsInstance(Object(owner)))
		{
			const uint8_t* composite = Template(Object(owner));
			if (composite)
			{
				place.composite = *reinterpret_cast<const uint32_t*>(composite + kTemplateGuid);
				const char* name = *reinterpret_cast<const char* const*>(composite + kTemplateName);
				size_t i = 0;
				for (; name && i + 1 < sizeof(place.compositeName) && name[i]; i++)
					place.compositeName[i] = name[i];
				place.compositeName[i] = '\0';
			}
		}
		// The path: the owner's id, its owner's, ... up to (not with) the root, read the other way round
		uint32_t reversed[64];
		uint32_t count = 0;
		for (const Allocation* step = owner; step && step->data; )
		{
			const Allocation* up = *reinterpret_cast<Allocation* const*>(Object(step) + kEntityOwner);
			if (!up)
				break;
			if (count == 64)
				break;
			reversed[count++] = *reinterpret_cast<const uint32_t*>(Object(step) + kEntityGuid);
			step = up;
		}
		for (uint32_t i = 0; i < count; i++)
			place.path[i] = reversed[count - 1 - i];
		place.pathCount = count;
	}

	bool LevelRunning(uint8_t* manager)
	{
		return manager && manager[kManagerInitialised] && !manager[kManagerShuttingDown] && *reinterpret_cast<Allocation**>(manager + kManagerRoot) && PackFile();
	}

	// The running level's root and the game's breakpoint list (false with no level)
	bool ReadLevel(uint32_t& root, const void*& list)
	{
		__try
		{
			uint8_t* manager = Manager();
			if (!LevelRunning(manager))
				return false;
			root = *reinterpret_cast<uint32_t*>(manager + kManagerRootGuid);
			list = *reinterpret_cast<void**>(manager + kManagerBreakpoints);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// The running instance with this id (as the game's records name it): among the composite's instances, or every
	// composite's when none is given (the root's own too). Its path from the root and its composite; false when none has it.
	bool FindInstanceById(uint32_t composite, uint32_t id, uint32_t* path, uint32_t& pathCount, uint32_t& found)
	{
		__try
		{
			uint8_t* manager = Manager();
			if (!LevelRunning(manager))
				return false;
			const Allocation* match = nullptr;
			const Allocation* root = *reinterpret_cast<Allocation* const*>(manager + kManagerRoot);
			if (root && root->data && InstanceId(root) == id)
				match = root;
			const uint32_t* templates = reinterpret_cast<const uint32_t*>(manager + kManagerTemplates);
			if (!match && PackFile() && templates[0] != 0xFFFFFFFF)
			{
				const uint32_t* offsets = reinterpret_cast<const uint32_t*>(PackFile() + templates[0] * 4u);
				for (uint32_t t = 0; t < templates[1] && !match; t++)
				{
					const uint8_t* candidate = reinterpret_cast<const uint8_t*>(PackFile() + offsets[t] * 4u);
					if (composite && *reinterpret_cast<const uint32_t*>(candidate + kTemplateGuid) != composite)
						continue;
					const Allocation* instances = *reinterpret_cast<Allocation* const*>(candidate + kTemplateInstances);
					if (!instances || !instances->data)
						continue;
					Allocation* const* items = static_cast<Allocation* const*>(instances->data);
					for (LONG i = 0; i < instances->count; i++)
						if (items[i] && items[i]->data && InstanceId(items[i]) == id)
						{
							match = items[i];
							break;
						}
				}
			}
			if (!match || !IsInstance(Object(match)))
				return false;
			const uint8_t* matchTemplate = Template(Object(match));
			if (!matchTemplate)
				return false;
			found = *reinterpret_cast<const uint32_t*>(matchTemplate + kTemplateGuid);
			// Its path: its own id, its owner's, ... up to (not with) the root, read the other way round
			uint32_t reversed[64];
			uint32_t count = 0;
			for (const Allocation* step = match; step && step->data; )
			{
				const Allocation* up = *reinterpret_cast<Allocation* const*>(Object(step) + kEntityOwner);
				if (!up)
					break;
				if (count == 64)
					return false;
				reversed[count++] = *reinterpret_cast<const uint32_t*>(Object(step) + kEntityGuid);
				step = up;
			}
			for (uint32_t i = 0; i < count; i++)
				path[i] = reversed[count - 1 - i];
			pathCount = count;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// A composite's name in the running level ("" when it is not in it)
	void ReadTemplateName(uint32_t composite, char* out, size_t size)
	{
		__try
		{
			out[0] = '\0';
			uint8_t* manager = Manager();
			if (!LevelRunning(manager))
				return;
			const uint8_t* found = FindTemplate(manager, composite);
			const char* name = found ? *reinterpret_cast<const char* const*>(found + kTemplateName) : nullptr;
			size_t i = 0;
			for (; name && i + 1 < size && name[i]; i++)
				out[i] = name[i];
			out[i] = '\0';
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			out[0] = '\0';
		}
	}

	// The instances a breakpoint names: the one at its path (checked to be of its composite), or every running one of its
	// composite. Returns how many (at most max), or -1 with why.
	int FindInstances(uint32_t composite, bool any, const uint32_t* path, uint32_t pathCount, const Allocation** out, int max, char* problem, size_t problemSize)
	{
		__try
		{
			uint8_t* manager = Manager();
			if (!LevelRunning(manager))
			{
				strcpy_s(problem, problemSize, "no level is running");
				return -1;
			}
			const uint8_t* wanted = FindTemplate(manager, composite);
			if (!wanted)
			{
				strcpy_s(problem, problemSize, "its composite is not in the running level");
				return -1;
			}
			if (any)
			{
				const Allocation* instances = *reinterpret_cast<Allocation* const*>(wanted + kTemplateInstances);
				int found = 0;
				if (instances && instances->data)
				{
					Allocation* const* items = static_cast<Allocation* const*>(instances->data);
					for (LONG i = 0; i < instances->count && found < max; i++)
						if (items[i] && items[i]->data)
							out[found++] = items[i];
				}
				if (!found)
					strcpy_s(problem, problemSize, "no instance of its composite is running");
				return found;
			}
			const Allocation* instance = *reinterpret_cast<Allocation* const*>(manager + kManagerRoot);
			for (uint32_t i = 0; i < pathCount; i++)
			{
				instance = instance && instance->data ? FindChild(Object(instance), path[i]) : nullptr;
				if (!instance || !IsInstance(Object(instance)))
				{
					sprintf_s(problem, problemSize, "its instance path does not resolve in the running level (at %08X)", path[i]);
					return -1;
				}
			}
			if (!instance || !instance->data || Template(Object(instance)) != wanted)
			{
				strcpy_s(problem, problemSize, "the instance at its path is not of its composite");
				return -1;
			}
			out[0] = instance;
			return 1;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			strcpy_s(problem, problemSize, "the game raised an exception while it was looked up");
			return -1;
		}
	}

	// An instance's entity by id - a child, or what an alias of the composite points at (the instance's own lookup) - as the
	// game's records name it: its id, and its instance's id. False when it is not there.
	bool ResolveHandle(const Allocation* instance, uint32_t id, uint32_t& entity, uint32_t& holder)
	{
		__try
		{
			const uint8_t* object = Object(instance);
			const Allocation* found = FindChild(object, id);
			Allocation* counted = nullptr;
			if (!found)
			{
				void* instanceObject = const_cast<uint8_t*>(object);
				const auto find = reinterpret_cast<Allocation** (__thiscall*)(void*, Allocation**, const uint32_t*)>((*reinterpret_cast<void***>(instanceObject))[kVFindEntity / 4]);
				find(instanceObject, &counted, &id);
				found = counted;
			}
			bool ok = false;
			if (found && found->data)
			{
				entity = *reinterpret_cast<const uint32_t*>(Object(found) + kEntityGuid);
				const Allocation* owner = *reinterpret_cast<Allocation* const*>(Object(found) + kEntityOwner);
				holder = owner && owner->data ? InstanceId(owner) : 0;
				ok = true;
			}
			if (counted && Address(kEntityRelease))
				reinterpret_cast<t_entity_release>(Address(kEntityRelease))(&counted);
			return ok;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// How many records the game's breakpoint list holds now (-1 with no level)
	LONG ReadListCount()
	{
		__try
		{
			uint8_t* manager = Manager();
			if (!LevelRunning(manager))
				return -1;
			const Allocation* list = *reinterpret_cast<Allocation* const*>(manager + kManagerBreakpoints);
			return list ? list->count : 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return -1;
		}
	}

	// Writes the game's breakpoint list (sorted already) through the list's own storage functions. False when it could not.
	bool WriteList(const GameRecord* records, LONG count)
	{
		__try
		{
			uint8_t* manager = Manager();
			if (!LevelRunning(manager))
				return false;
			Allocation* list = *reinterpret_cast<Allocation**>(manager + kManagerBreakpoints);
			// Made with the level, with storage of its own (the game's resize cannot grow a list without it)
			if (!list || !list->vtable || !list->data)
				return false;
			void** vtable = list->vtable;
			if (count > list->count)
			{
				const auto resize = reinterpret_cast<bool(__thiscall*)(void*, uint32_t)>(vtable[kVResize / 4]);
				if (!resize(list, static_cast<uint32_t>(count) * sizeof(GameRecord)) || !list->data)
					return false;
			}
			const auto increment = reinterpret_cast<bool(__thiscall*)(void*)>(vtable[kVIncrement / 4]);
			const auto decrement = reinterpret_cast<bool(__thiscall*)(void*)>(vtable[kVDecrement / 4]);
			while (list->count > count)
				decrement(list);
			if (count > 0)
				memcpy(list->data, records, static_cast<size_t>(count) * sizeof(GameRecord));
			while (list->count < count)
				increment(list);
			// Looked in from when the manager is made; set again in case anything cleared it
			manager[kManagerUseBreakpoints] = 1;
			return list->count == count;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// ---- Holding the scripts ----
	volatile LONG* PauseReasons()
	{
		__try
		{
			const uintptr_t at = Address(kPauseState);
			return at ? *reinterpret_cast<volatile LONG**>(at) : nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	bool SetReason(LONG reason, bool on)
	{
		__try
		{
			volatile LONG* reasons = PauseReasons();
			if (!reasons)
				return false;
			if (on)
				InterlockedOr(reasons, reason);
			else
				InterlockedAnd(reasons, ~reason);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool HasReason(LONG reason)
	{
		__try
		{
			volatile LONG* reasons = PauseReasons();
			return reasons && (*reasons & reason) != 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	void HoldTransport()
	{
		__try
		{
			uint8_t* manager = Manager();
			if (manager)
				*reinterpret_cast<volatile uint32_t*>(manager + kManagerTransport) = kTransportPaused;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
		}
	}

	int ReadTransport()
	{
		__try
		{
			uint8_t* manager = Manager();
			return manager ? static_cast<int>(*reinterpret_cast<uint32_t*>(manager + kManagerTransport)) : -1;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return -1;
		}
	}

	// ---- A hit ----
	struct HeldCall
	{
		bool ok = false;
		uint32_t method = 0;
		Place target;
		double due = 0.0;
		double clock = 0.0;
		struct Cause { uint32_t method; Place place; };
		Cause chain[8];
		int chainCount = 0;
	};

	// The call the game is holding: the first of this round's calls
	void ReadHeldCall(HeldCall& held)
	{
		__try
		{
			uint8_t* manager = Manager();
			if (!manager)
				return;
			held.clock = *reinterpret_cast<const double*>(manager + kManagerClock);
			const Allocation* calls = *reinterpret_cast<Allocation* const*>(manager + kManagerCalls);
			if (!calls || !calls->data)
				return;
			const Allocation* round = *reinterpret_cast<Allocation* const*>(Object(calls) + kCallsRound);
			if (!round || !round->data || round->count < 1)
				return;
			const uint8_t* call = *static_cast<const uint8_t* const*>(round->data);
			if (!call)
				return;
			held.method = *reinterpret_cast<const uint32_t*>(call + kCallMethod);
			held.due = *reinterpret_cast<const double*>(call + kCallDue);
			const Allocation* target = *reinterpret_cast<Allocation* const*>(call + kCallTarget);
			if (target && target->data)
				ReadPlace(target, held.target);
			held.ok = true;
			// What caused it, back to where it started (at most 8 calls back)
			for (const uint8_t* cause = *reinterpret_cast<const uint8_t* const*>(call + kCallCause); cause && held.chainCount < 8;
				cause = *reinterpret_cast<const uint8_t* const*>(cause + kCallCause))
			{
				HeldCall::Cause& link = held.chain[held.chainCount++];
				link.method = *reinterpret_cast<const uint32_t*>(cause + kCallMethod);
				const Allocation* causeTarget = *reinterpret_cast<Allocation* const*>(cause + kCallTarget);
				if (causeTarget && causeTarget->data)
					ReadPlace(causeTarget, link.place);
			}
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
		}
	}

	std::vector<uint32_t> PlacePath(const Place& place)
	{
		return std::vector<uint32_t>(place.path, place.path + place.pathCount);
	}

	// The game's call of the empty function, pointed here: a breakpoint (by the id of its record) was hit. On the thread
	// making the script calls, inside the game's lock on its entity manager.
	void __cdecl OnHit(int id)
	{
		// Held at once: the game's pause state turns its next frames' script transport to "paused", but the scripts may
		// come round before it does; the transport is set here so they do not
		SetReason(kPauseBreakpoint, true);
		HoldTransport();
		g_held = true;
		g_atBreakpoint = true;

		HeldCall held;
		ReadHeldCall(held);
		const uint32_t sequence = ++g_sequence;
		int breakpoint = 0;
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			if (id >= 1 && id <= static_cast<int>(g_written.size()))
				breakpoint = g_written[id - 1].breakpoint;
			for (Breakpoint& bp : g_breakpoints)
				if (bp.id == breakpoint)
					bp.hits++;
			LIVE_DEBUGGER::HitView hit;
			hit.held = true;
			hit.atBreakpoint = true;
			hit.sequence = sequence;
			hit.breakpoint = breakpoint;
			hit.method = held.method;
			hit.entity = held.target.entity;
			hit.type = held.target.type;
			hit.composite = held.target.composite;
			hit.compositeName = held.target.compositeName;
			hit.instance = held.target.instance;
			hit.path = PlacePath(held.target);
			hit.time = held.due;
			for (int i = 0; i < held.chainCount; i++)
				hit.chain.push_back({ held.chain[i].place.entity, held.chain[i].method, held.chain[i].place.composite, held.chain[i].place.compositeName, PlacePath(held.chain[i].place) });
			g_hit = std::move(hit);
		}
		g_version++;
		Publish();
		DevTools::Log("Debugger: breakpoint %d hit (record %d): method %08X on %08X in %s (instance %08X), %d calls led to it", breakpoint, id, held.method, held.target.entity,
			held.target.compositeName[0] ? held.target.compositeName : "?", held.target.instance, held.chainCount);
	}

	void Patch(bool attach)
	{
		if (attach)
		{
			uint8_t* site = reinterpret_cast<uint8_t*>(Address(kHitCall));
			const uintptr_t empty = Address(kEmptyFunction);
			if (!site || !empty)
			{
				DevTools::Log("Debugger: breakpoints are not available on this game release");
				return;
			}
			int32_t distance = 0;
			memcpy(&distance, site + 1, sizeof(distance));
			if (site[0] != 0xE8 || reinterpret_cast<uintptr_t>(site) + 5 + distance != empty)
			{
				DevTools::Log("Debugger: the game's breakpoint call is not as expected - breakpoints are off");
				return;
			}
			const int32_t ours = static_cast<int32_t>(reinterpret_cast<uintptr_t>(&OnHit) - (reinterpret_cast<uintptr_t>(site) + 5));
			DWORD protection = 0;
			if (!VirtualProtect(site + 1, sizeof(ours), PAGE_EXECUTE_READWRITE, &protection))
				return;
			memcpy(site + 1, &ours, sizeof(ours));
			VirtualProtect(site + 1, sizeof(ours), protection, &protection);
			FlushInstructionCache(GetCurrentProcess(), site, 5);
			g_site = site;
			g_originalDistance = distance;
			g_patched = true;
			return;
		}
		if (!g_site)
			return;
		// Detached as the game closes: its threads may have stopped while holding its locks, so neither its list nor its
		// lock is touched - only the call is put back as it was
		DWORD protection = 0;
		if (VirtualProtect(g_site + 1, sizeof(g_originalDistance), PAGE_EXECUTE_READWRITE, &protection))
		{
			memcpy(g_site + 1, &g_originalDistance, sizeof(g_originalDistance));
			VirtualProtect(g_site + 1, sizeof(g_originalDistance), protection, &protection);
			FlushInstructionCache(GetCurrentProcess(), g_site, 5);
		}
		g_site = nullptr;
		g_patched = false;
	}

	// ---- Keeping the game's list in line (entity thread) ----
	bool RecordLess(const GameRecord& a, const GameRecord& b)
	{
		if (a.entity != b.entity)
			return a.entity < b.entity;
		if (a.instance != b.instance)
			return a.instance < b.instance;
		return a.method < b.method;
	}

	struct Resolved
	{
		int id;
		int instances = 0;
		int records = 0;
		std::string problem;
		std::string compositeName;
	};

	void Sync(uint32_t root)
	{
		std::vector<Breakpoint> breakpoints;
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			breakpoints = g_breakpoints;
		}
		ManagerAccess access;
		std::vector<GameRecord> records;
		std::vector<WrittenRecord> written;
		std::vector<Resolved> resolved;
		bool full = false;
		for (const Breakpoint& bp : breakpoints)
		{
			Resolved result;
			result.id = bp.id;
			char name[96] = "";
			ReadTemplateName(bp.composite, name, sizeof(name));
			result.compositeName = name;
			if (!bp.enabled || !g_enabled)
				result.problem = !bp.enabled ? "switched off" : "breakpoints are switched off";
			else if (bp.root != 0 && bp.root != root)
				result.problem = "set for another level";
			else
			{
				const Allocation* instances[64] = {};
				char problem[160] = "";
				const int found = FindInstances(bp.composite, bp.anyInstance, bp.path.data(), static_cast<uint32_t>(bp.path.size()), instances, 64, problem, sizeof(problem));
				if (found < 0)
					result.problem = problem;
				for (int i = 0; i < found; i++)
				{
					uint32_t entity = 0, holder = 0;
					if (!ResolveHandle(instances[i], bp.entity, entity, holder))
						continue;
					result.instances++;
					for (uint32_t method : bp.methods)
					{
						if (static_cast<int>(records.size()) >= kMaxRecords)
						{
							full = true;
							break;
						}
						written.push_back({ bp.id, entity, holder, method });
						records.push_back({ static_cast<int32_t>(written.size()), entity, holder, method });
						result.records++;
					}
				}
				if (found >= 0 && result.instances == 0 && result.problem.empty())
					result.problem = found == 0 ? problem : "the entity is not in its running instance(s)";
			}
			resolved.push_back(result);
		}
		std::stable_sort(records.begin(), records.end(), RecordLess);

		// Written only when it changed (each record's id is where it is in our list, so compare what they stand for) - or when
		// the game's list no longer holds what was written
		const LONG inGame = ReadListCount();
		bool same = false;
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			if (g_written.size() == written.size() && g_recordsInGame == static_cast<int>(records.size()) && inGame == static_cast<LONG>(records.size()))
			{
				same = true;
				for (size_t i = 0; i < written.size() && same; i++)
					same = g_written[i].breakpoint == written[i].breakpoint && g_written[i].entity == written[i].entity &&
						g_written[i].instance == written[i].instance && g_written[i].method == written[i].method;
			}
		}
		bool wrote = same;
		if (!same)
		{
			wrote = WriteList(records.data(), static_cast<LONG>(records.size()));
			if (wrote)
				DevTools::Log("Debugger: %zu breakpoint record(s) written to the game", records.size());
		}

		std::lock_guard<std::mutex> lock(g_mutex);
		g_syncs++;
		if (wrote)
		{
			g_written = written;
			g_recordsInGame = static_cast<int>(records.size());
		}
		g_status = !wrote ? "The game's breakpoint list could not be written (is the level still loading?)" :
			full ? "More than 256 method calls to stop on: the rest are left out" : "";
		for (Breakpoint& bp : g_breakpoints)
			for (const Resolved& result : resolved)
				if (result.id == bp.id)
				{
					bp.instances = result.instances;
					bp.records = result.records;
					bp.problem = result.problem;
					if (!result.compositeName.empty())
						bp.compositeName = result.compositeName;
				}
	}

	// The level's breakpoint list has gone (no level, another level, or the level made again): nothing is in it
	void Forget()
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		g_written.clear();
		g_recordsInGame = 0;
	}

	void Release(const char* why)
	{
		if (!g_held && !HasReason(kPauseBreakpoint | kPauseSingleCycle))
			return;
		SetReason(kPauseBreakpoint | kPauseSingleCycle, false);
		g_held = false;
		g_atBreakpoint = false;
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			g_hit.held = false;
			g_hit.atBreakpoint = false;
		}
		g_version++;
		Publish();
		DevTools::Log("Debugger: scripts let go (%s)", why);
	}

	// ---- Commands ----
	DEBUG_MENU::CommandResult Error(const std::string& message)
	{
		return { false, message };
	}

	std::string Describe(const Breakpoint& bp)
	{
		std::string methods;
		for (uint32_t method : bp.methods)
			methods += (methods.empty() ? "" : ",") + Hex8(method);
		return "id=" + std::to_string(bp.id) + " composite=" + Hex8(bp.composite) + " path=" + (bp.anyInstance ? "*" : Path(bp.path)) + " entity=" + Hex8(bp.entity) +
			" methods=" + methods + " root=" + Hex8(bp.root) + " enabled=" + (bp.enabled ? "1" : "0") + " hits=" + std::to_string(bp.hits) + " instances=" + std::to_string(bp.instances) +
			" records=" + std::to_string(bp.records) + (bp.problem.empty() ? "" : " problem=" + bp.problem) + (bp.label.empty() ? "" : " label=" + bp.label);
	}

	// Waits (a moment) for the entity thread to have gone over the list after a change, so the answer can say where it is
	std::string AppliedText(int id, uint32_t syncsBefore)
	{
		bool synced = false;
		// With no level the entity thread does not go over the list: no wait (it would hold up Live Link's socket thread a
		// second for every breakpoint OpenCAGE sends)
		uint32_t root = 0;
		const void* list = nullptr;
		const bool level = ReadLevel(root, list);
		for (int i = 0; level && i < 40 && !synced; i++)
		{
			Sleep(25);
			synced = g_syncs != syncsBefore && !g_dirty;
		}
		std::lock_guard<std::mutex> lock(g_mutex);
		for (const Breakpoint& bp : g_breakpoints)
			if (bp.id == id)
			{
				if (!synced)
					return "the game has not put it in yet (no level running, or the game is loading) - it is put in once a level runs";
				if (bp.records > 0)
					return "in " + std::to_string(bp.instances) + " running instance(s), " + std::to_string(bp.records) + " method call(s) to stop on";
				return "in no running instance yet: " + (bp.problem.empty() ? std::string("not found") : bp.problem) + " (looked for again every second)";
			}
		return "";
	}

	DEBUG_MENU::CommandResult AddBreakpoint(const std::string& argument)
	{
		Breakpoint bp;
		std::string text;
		// The instance by its id, as the game names it in its records (what inspect.pick gives), in place of a path: found
		// among the running instances now, and kept as its path
		uint32_t instanceId = 0;
		const bool byInstance = Field(argument, "instance", text);
		if (byInstance && !ParseId(text, instanceId))
			return Error("instance must be a composite instance id (8 hex digits)");
		if (Field(argument, "composite", text) && !ParseId(text, bp.composite))
			return Error("composite must be a composite id");
		if (!bp.composite && !byInstance)
			return Error("composite=<id> is needed (the composite holding the entity), or instance=<id>");
		if (!Field(argument, "entity", text) || !ParseId(text, bp.entity))
			return Error("entity=<id> is needed");
		if (!Field(argument, "methods", text) && !Field(argument, "method", text))
			return Error("methods=<id,id...> is needed (the method pins to stop on)");
		if (!ParseIds(text, bp.methods) || bp.methods.empty())
			return Error("methods must be ids (8 hex digits, or as OpenCAGE writes them: AB-CD-EF-01), separated by commas");
		if (bp.methods.size() > 64)
			return Error("at most 64 methods on one breakpoint");
		if (Field(argument, "path", text))
		{
			if (text == "*")
				bp.anyInstance = true;
			else if (!ParseIds(text, bp.path))
				return Error("path must be instance entity ids from the root, separated by commas (empty: the root itself), or * for every instance");
		}
		else
			bp.anyInstance = true;
		if (bp.path.size() > 64)
			return Error("the instance path is too long (at most 64 steps)");
		if (Field(argument, "root", text) && !text.empty() && !ParseId(text, bp.root))
			return Error("root must be the level's root composite id");
		Field(argument, "label", bp.label);
		if (byInstance)
		{
			uint32_t path[64] = {};
			uint32_t pathCount = 0, composite = 0;
			bool found = false;
			{
				ManagerAccess access;
				found = FindInstanceById(bp.composite, instanceId, path, pathCount, composite);
			}
			if (!found)
				return Error("no running instance" + std::string(bp.composite ? " of that composite" : "") + " has the id " + Hex8(instanceId));
			bp.composite = composite;
			bp.anyInstance = false;
			bp.path.assign(path, path + pathCount);
		}

		int id = 0;
		const uint32_t syncsBefore = g_syncs;
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			// The same place and entity again: its methods are added to the one there
			for (Breakpoint& existing : g_breakpoints)
			{
				if (existing.composite == bp.composite && existing.entity == bp.entity && existing.anyInstance == bp.anyInstance && existing.path == bp.path && existing.root == bp.root)
				{
					for (uint32_t method : bp.methods)
						if (std::find(existing.methods.begin(), existing.methods.end(), method) == existing.methods.end())
							existing.methods.push_back(method);
					if (!bp.label.empty())
						existing.label = bp.label;
					existing.enabled = true;
					id = existing.id;
					break;
				}
			}
			if (!id)
			{
				if (g_breakpoints.size() >= 128)
					return Error("at most 128 breakpoints");
				bp.id = id = g_nextId++;
				g_breakpoints.push_back(bp);
			}
		}
		Changed();
		return { true, "id=" + std::to_string(id) + "\n" + AppliedText(id, syncsBefore) };
	}

	DEBUG_MENU::CommandResult RemoveBreakpoint(const std::string& argument)
	{
		std::string text = argument;
		while (!text.empty() && isspace(static_cast<unsigned char>(text.back())))
			text.pop_back();
		if (text == "all" || text == "*")
			return { true, LIVE_DEBUGGER::RemoveAll() };
		// By id, or by where it is (composite, path, entity: every breakpoint there; with methods, just those methods)
		std::string field;
		if (!Field(argument, "composite", field))
		{
			const int id = atoi(text.c_str());
			if (id <= 0)
				return Error("give a breakpoint's id (debugger.list), all, or composite=<id> path=<ids|*> entity=<id> [methods=<ids>]");
			const std::string result = LIVE_DEBUGGER::Remove(id);
			return { result.rfind("removed", 0) == 0, result };
		}
		uint32_t composite = 0, entity = 0;
		if (!ParseId(field, composite) || !Field(argument, "entity", field) || !ParseId(field, entity))
			return Error("composite=<id> and entity=<id> are needed");
		bool any = true;
		std::vector<uint32_t> path, methods;
		if (Field(argument, "path", field))
		{
			any = field == "*";
			if (!any && !ParseIds(field, path))
				return Error("path must be ids separated by commas, or *");
		}
		const bool someMethods = Field(argument, "methods", field) || Field(argument, "method", field);
		if (someMethods && !ParseIds(field, methods))
			return Error("methods must be ids separated by commas");
		int removed = 0;
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			for (auto it = g_breakpoints.begin(); it != g_breakpoints.end(); )
			{
				if (it->composite == composite && it->entity == entity && it->anyInstance == any && (any || it->path == path))
				{
					if (someMethods)
					{
						for (uint32_t method : methods)
							it->methods.erase(std::remove(it->methods.begin(), it->methods.end(), method), it->methods.end());
						if (!it->methods.empty())
						{
							++it;
							removed++;
							continue;
						}
					}
					it = g_breakpoints.erase(it);
					removed++;
					continue;
				}
				++it;
			}
		}
		if (removed)
			Changed();
		return { removed > 0, removed ? "removed " + std::to_string(removed) : "no breakpoint there" };
	}

	DEBUG_MENU::CommandResult List()
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		std::string text = "version=" + std::to_string(g_version.load()) + "\ncount=" + std::to_string(g_breakpoints.size());
		for (const Breakpoint& bp : g_breakpoints)
			text += "\n" + Describe(bp);
		return { true, text };
	}

	std::string StateText()
	{
		uint32_t root = 0;
		const void* list = nullptr;
		const bool level = ReadLevel(root, list);
		LIVE_DEBUGGER::HitView hit = LIVE_DEBUGGER::Hit();
		std::string text = std::string("level=") + (level ? "1" : "0") + "\nroot=" + Hex8(root) + "\npatched=" + (g_patched ? "1" : "0") +
			"\nheld=" + (g_held ? "1" : "0") + "\nat_breakpoint=" + (g_atBreakpoint ? "1" : "0") + "\ntransport=" + std::to_string(ReadTransport()) +
			"\nhit=" + std::to_string(hit.sequence) + "\nversion=" + std::to_string(g_version.load()) + "\nrecords=" + std::to_string(g_recordsInGame.load());
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			text += "\nbreakpoints=" + std::to_string(g_breakpoints.size());
			if (!g_status.empty())
				text += "\nstatus=" + g_status;
		}
		if (hit.sequence == 0)
			return text;
		char time[32];
		snprintf(time, sizeof(time), "%.3f", hit.time);
		text += "\nbreakpoint=" + std::to_string(hit.breakpoint) + "\nentity=" + Hex8(hit.entity) + "\nmethod=" + Hex8(hit.method) + "\ntype=" + Hex8(hit.type) +
			"\ncomposite=" + Hex8(hit.composite) + "\ncomposite_name=" + hit.compositeName + "\ninstance=" + Hex8(hit.instance) + "\npath=" + Path(hit.path) +
			"\ntime=" + time + "\nsteps=" + std::to_string(hit.steps) + "\nchain=" + std::to_string(hit.chain.size());
		for (size_t i = 0; i < hit.chain.size(); i++)
			text += "\nchain." + std::to_string(i + 1) + "=entity:" + Hex8(hit.chain[i].entity) + " method:" + Hex8(hit.chain[i].method) + " composite:" + Hex8(hit.chain[i].composite) +
				" path:" + Path(hit.chain[i].path);
		return text;
	}

	// ---- Entity states and values, for OpenCAGE's flowgraphs ----
	struct StateEntry { uint32_t entity; uint32_t state; };

	// The state flags of an instance's entities (its children), or of the ones asked for (children, or what an alias of the
	// composite points at). Returns how many were read, -1 if it faulted.
	int ReadStates(const Allocation* instance, const uint32_t* wanted, int wantedCount, StateEntry* out, int max)
	{
		__try
		{
			const uint8_t* object = Object(instance);
			int count = 0;
			if (wantedCount == 0)
			{
				const Allocation* entities = *reinterpret_cast<Allocation* const*>(object + kInstanceEntities);
				if (!entities || !entities->data)
					return 0;
				Allocation* const* items = static_cast<Allocation* const*>(entities->data);
				for (LONG i = 0; i < entities->count && count < max; i++)
					if (items[i] && items[i]->data)
						out[count++] = { *reinterpret_cast<const uint32_t*>(Object(items[i]) + kEntityGuid), *reinterpret_cast<const uint32_t*>(Object(items[i]) + kEntityState) };
				return count;
			}
			for (int i = 0; i < wantedCount && count < max; i++)
			{
				const Allocation* found = FindChild(object, wanted[i]);
				Allocation* counted = nullptr;
				if (!found)
				{
					void* instanceObject = const_cast<uint8_t*>(object);
					uint32_t id = wanted[i];
					const auto find = reinterpret_cast<Allocation** (__thiscall*)(void*, Allocation**, const uint32_t*)>((*reinterpret_cast<void***>(instanceObject))[kVFindEntity / 4]);
					find(instanceObject, &counted, &id);
					found = counted;
				}
				if (found && found->data)
					out[count++] = { wanted[i], *reinterpret_cast<const uint32_t*>(Object(found) + kEntityState) };
				if (counted && Address(kEntityRelease))
					reinterpret_cast<t_entity_release>(Address(kEntityRelease))(&counted);
			}
			return count;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return -1;
		}
	}

	bool ReadInstanceId(const Allocation* instance, uint32_t& id)
	{
		__try
		{
			id = InstanceId(instance);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			id = 0;
			return false;
		}
	}

	bool ParsePlace(const std::string& argument, uint32_t& composite, bool& any, std::vector<uint32_t>& path, std::string& error)
	{
		std::string text;
		if (!Field(argument, "composite", text) || !ParseId(text, composite))
		{
			error = "composite=<id> is needed";
			return false;
		}
		any = true;
		path.clear();
		if (Field(argument, "path", text))
		{
			any = text == "*";
			if (!any && !ParseIds(text, path))
			{
				error = "path must be instance entity ids from the root, separated by commas (empty: the root itself), or *";
				return false;
			}
		}
		if (path.size() > 64)
		{
			error = "the instance path is too long";
			return false;
		}
		return true;
	}

	DEBUG_MENU::CommandResult EntityStates(const std::string& argument)
	{
		uint32_t composite = 0;
		bool any = true;
		std::vector<uint32_t> path, wanted;
		std::string error, text;
		if (!ParsePlace(argument, composite, any, path, error))
			return Error(error);
		if (Field(argument, "entities", text) && !ParseIds(text, wanted))
			return Error("entities must be ids separated by commas");
		if (wanted.size() > 1024)
			return Error("at most 1024 entities at once");

		std::vector<StateEntry> states(wanted.empty() ? 4096 : wanted.size());
		int instanceCount = 0, read = 0;
		uint32_t instanceId = 0;
		char problem[160] = "";
		{
			ManagerAccess access;
			const Allocation* instances[2] = {};
			instanceCount = FindInstances(composite, any, path.data(), static_cast<uint32_t>(path.size()), instances, 2, problem, sizeof(problem));
			// Every instance asked for: only the one there is (more than one can't be shown as one)
			if (instanceCount == 1)
			{
				read = ReadStates(instances[0], wanted.data(), static_cast<int>(wanted.size()), states.data(), static_cast<int>(states.size()));
				ReadInstanceId(instances[0], instanceId);
			}
		}
		if (instanceCount < 0)
			return Error(problem);
		std::string result = "instances=" + std::to_string(instanceCount);
		if (instanceCount > 1)
			return { true, result + "\nproblem=more than one running instance: open the composite from the level's root to see one" };
		if (instanceCount == 0)
			return { true, result + "\nproblem=" + std::string(problem) };
		if (read < 0)
			return Error("the game raised an exception while the states were read");
		result += "\ninstance=" + Hex8(instanceId);
		for (int i = 0; i < read; i++)
			result += "\n" + Hex8(states[i].entity) + "=" + Hex8(states[i].state);
		return { true, result };
	}

	// A value asked for: the entity, the parameter, and what kind of value to read it as
	enum class Kind { Bool, Int, Float, Text, Vector, Enum, Guid };
	struct ValueAsk
	{
		uint32_t entity, parameter;
		Kind kind;
	};

	// Reads one parameter of an entity of the instance as the entity itself would (entity thread). Text in out.
	bool ReadValue(const Allocation* instance, const ValueAsk& ask, char* out, size_t size)
	{
		__try
		{
			const uint8_t* object = Object(instance);
			Allocation* entity = FindChild(object, ask.entity);
			Allocation* counted = nullptr;
			if (!entity)
			{
				void* instanceObject = const_cast<uint8_t*>(object);
				uint32_t id = ask.entity;
				const auto find = reinterpret_cast<Allocation** (__thiscall*)(void*, Allocation**, const uint32_t*)>((*reinterpret_cast<void***>(instanceObject))[kVFindEntity / 4]);
				find(instanceObject, &counted, &id);
				entity = counted;
			}
			bool ok = false;
			if (entity && entity->data)
			{
				void* entityObject = entity->data;
				void* iface = reinterpret_cast<void* (__thiscall*)(void*)>((*reinterpret_cast<void***>(entityObject))[kVGetInterface / 4])(entityObject);
				const uint32_t parameter = ask.parameter;
				switch (ask.kind)
				{
				case Kind::Bool:
				{
					bool value = false;
					const auto read = reinterpret_cast<t_read>(Address(kReadBool));
					if (iface && read && read(iface, &entity, &parameter, &value))
						ok = sprintf_s(out, size, "%s", value ? "true" : "false") > 0;
					break;
				}
				case Kind::Int:
				{
					int value = 0;
					const auto read = reinterpret_cast<t_read>(Address(kReadInt));
					if (iface && read && read(iface, &entity, &parameter, &value))
						ok = sprintf_s(out, size, "%d", value) > 0;
					break;
				}
				case Kind::Float:
				{
					float value = 0.0f;
					const auto read = reinterpret_cast<t_read>(Address(kReadFloat));
					if (iface && read && read(iface, &entity, &parameter, &value))
						ok = sprintf_s(out, size, "%g", value) > 0;
					break;
				}
				case Kind::Vector:
				{
					alignas(16) float value[4] = {};
					const auto read = reinterpret_cast<t_read>(Address(kReadVector));
					if (iface && read && read(iface, &entity, &parameter, value))
						ok = sprintf_s(out, size, "%g,%g,%g", value[0], value[1], value[2]) > 0;
					break;
				}
				case Kind::Enum:
				{
					struct { uint32_t type; int index; } value = {};
					const auto read = reinterpret_cast<t_read>(Address(kReadEnum));
					if (iface && read && read(iface, &entity, &parameter, &value))
						ok = sprintf_s(out, size, "%d", value.index) > 0;
					break;
				}
				case Kind::Guid:
				{
					uint32_t value = 0;
					const auto read = reinterpret_cast<t_read>(Address(kReadGuid));
					if (iface && read && read(iface, &entity, &parameter, &value))
						ok = sprintf_s(out, size, "%08X", value) > 0;
					break;
				}
				case Kind::Text:
				{
					uint32_t offset = 0xFFFFFFFF;
					const auto read = reinterpret_cast<t_read>(Address(kReadText));
					const auto lookup = reinterpret_cast<t_string_from_offset>(Address(kStringFromOffset));
					void* table = Address(kStringTable) ? *reinterpret_cast<void**>(Address(kStringTable)) : nullptr;
					if (iface && read && lookup && read(iface, &entity, &parameter, &offset))
					{
						const char* value = offset != 0xFFFFFFFF && table ? lookup(table, offset) : "";
						size_t i = 0;
						for (; value && i + 1 < size && value[i]; i++)
							out[i] = (value[i] == '\n' || value[i] == '\r') ? ' ' : value[i];
						out[i] = '\0';
						ok = true;
					}
					break;
				}
				}
			}
			if (counted && Address(kEntityRelease))
				reinterpret_cast<t_entity_release>(Address(kEntityRelease))(&counted);
			return ok;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool ParseKind(const std::string& text, Kind& kind)
	{
		const char c = text.empty() ? 0 : static_cast<char>(tolower(static_cast<unsigned char>(text[0])));
		switch (c)
		{
		case 'b': kind = Kind::Bool; return true;
		case 'i': kind = Kind::Int; return true;
		case 'f': kind = Kind::Float; return true;
		case 's': kind = Kind::Text; return true;
		case 'v': kind = Kind::Vector; return true;
		case 'e': kind = Kind::Enum; return true;
		case 'g': kind = Kind::Guid; return true;
		default: return false;
		}
	}

	DEBUG_MENU::CommandResult EntityValues(const std::string& argument)
	{
		uint32_t composite = 0;
		bool any = true;
		std::vector<uint32_t> path;
		std::string error, text;
		if (!ParsePlace(argument, composite, any, path, error))
			return Error(error);
		if (!Field(argument, "values", text))
			return Error("values=<entity>:<parameter>:<kind>,... is needed (kind: bool, int, float, string, vector, enum or guid)");
		std::vector<ValueAsk> asks;
		size_t start = 0;
		while (start < text.size())
		{
			size_t comma = text.find(',', start);
			if (comma == std::string::npos)
				comma = text.size();
			const std::string item = text.substr(start, comma - start);
			start = comma + 1;
			const size_t first = item.find(':'), second = item.find(':', first == std::string::npos ? 0 : first + 1);
			ValueAsk ask = {};
			if (first == std::string::npos || second == std::string::npos || !ParseId(item.substr(0, first), ask.entity) ||
				!ParseId(item.substr(first + 1, second - first - 1), ask.parameter) || !ParseKind(item.substr(second + 1), ask.kind))
				return Error("each value is <entity>:<parameter>:<kind> (kind: bool, int, float, string, vector, enum or guid): " + item);
			asks.push_back(ask);
		}
		if (asks.empty() || asks.size() > 256)
			return Error("1 to 256 values at once");

		// One at a time: a request that timed out still runs when the entity thread comes round, so polls made while it does
		// not (a level loading) would otherwise pile up and all run at once
		if (g_valuesWaiting.exchange(true))
			return Error("the last values asked for are still waiting for the game's entity thread");
		// The readers follow links and fill the entities' caches as they go: on the entity thread only
		return DEBUG_MENU::AnswerOnEntityThread([composite, any, path, asks]() -> DEBUG_MENU::CommandResult
		{
			struct Done { ~Done() { g_valuesWaiting = false; } } done;
			ManagerAccess access;
			// These reads are ours, not the scripts': the script activity trace leaves them out
			LIVE_TRACE::OwnReads own;
			const Allocation* instances[2] = {};
			char problem[160] = "";
			const int found = FindInstances(composite, any, path.data(), static_cast<uint32_t>(path.size()), instances, 2, problem, sizeof(problem));
			if (found < 0)
				return { false, problem };
			if (found != 1)
				return { true, "instances=" + std::to_string(found) + "\nproblem=" + (found == 0 ? std::string(problem) : std::string("more than one running instance: open the composite from the level's root to see one")) };
			std::string result = "instances=1";
			for (const ValueAsk& ask : asks)
			{
				char value[256] = "";
				const bool ok = ReadValue(instances[0], ask, value, sizeof(value));
				result += "\n" + Hex8(ask.entity) + "." + Hex8(ask.parameter) + (ok ? "=" + std::string(value) : "?");
			}
			return { true, result };
		}, 1500);
	}

	// ---- Registration ----
	void AttachHooks(bool attach)
	{
		Patch(attach);
	}

	bool Register()
	{
		DEBUG_MENU::AddSwitch("debugger.breakpoints", &g_enabled);
		DEBUG_MENU::AddValue("debugger.held", &g_heldValue, 0.0f, 1.0f);
		DEBUG_MENU::AddValue("debugger.hit", &g_hitValue, 0.0f, 1000000.0f);
		DEBUG_MENU::AddValue("debugger.version", &g_versionValue, 0.0f, 1000000.0f);
		DEBUG_MENU::AddCommand("debugger.add_breakpoint",
			"composite=<id> path=<ids from the root, comma separated; empty: the root; *: every instance> (or instance=<composite instance id, as inspect.pick gives it>) entity=<id> methods=<ids> [root=<level root id>] [label=<text>]: stop when one of those methods is called on the entity",
			[](const std::string& argument) { return AddBreakpoint(argument); });
		DEBUG_MENU::AddCommand("debugger.remove_breakpoint", "<id> | all | composite=<id> path=<ids|*> entity=<id> [methods=<ids>]",
			[](const std::string& argument) { return RemoveBreakpoint(argument); });
		DEBUG_MENU::AddCommand("debugger.clear", "takes every breakpoint away",
			[](const std::string&) { return DEBUG_MENU::CommandResult{ true, LIVE_DEBUGGER::RemoveAll() }; });
		DEBUG_MENU::AddCommand("debugger.enable_breakpoint", "<id> <0|1>: switches a breakpoint off or on (kept either way)",
			[](const std::string& argument) -> DEBUG_MENU::CommandResult
			{
				int id = 0, on = 1;
				if (sscanf_s(argument.c_str(), "%d %d", &id, &on) < 1 || id <= 0)
					return Error("<id> <0|1>");
				const std::string result = LIVE_DEBUGGER::Enable(id, on != 0);
				return { result.find("no breakpoint") == std::string::npos, result };
			});
		DEBUG_MENU::AddCommand("debugger.list", "every breakpoint, one a line: id, composite, path, entity, methods, hits, where it is in the game",
			[](const std::string&) { return List(); });
		DEBUG_MENU::AddCommand("debugger.list_breakpoints", "the same as debugger.list (the name the other views' lists follow)",
			[](const std::string&) { return List(); });
		DEBUG_MENU::AddCommand("debugger.state", "whether the scripts are held, and the last breakpoint hit: entity, method, where, and the calls that led to it",
			[](const std::string&) { return DEBUG_MENU::CommandResult{ true, StateText() }; });
		DEBUG_MENU::AddCommand("debugger.continue", "lets the held scripts go on",
			[](const std::string&) -> DEBUG_MENU::CommandResult
			{
				const std::string result = LIVE_DEBUGGER::Continue();
				return { result.rfind("not", 0) != 0, result };
			});
		DEBUG_MENU::AddCommand("debugger.step", "[cycle|frame]: while held, makes the held call and the rest of the calls due with it - one round (cycle, the default), or runs one whole frame (frame), then holds again",
			[](const std::string& argument) -> DEBUG_MENU::CommandResult
			{
				const std::string result = LIVE_DEBUGGER::Step(argument.find("frame") != std::string::npos);
				return { result.rfind("not", 0) != 0, result };
			});
		DEBUG_MENU::AddCommand("debugger.pause", "holds the level's scripts now (and the game, as a breakpoint does), until debugger.continue",
			[](const std::string&) -> DEBUG_MENU::CommandResult
			{
				const std::string result = LIVE_DEBUGGER::Pause();
				return { result.rfind("not", 0) != 0, result };
			});
		DEBUG_MENU::AddCommand("debugger.entities", "composite=<id> path=<ids|*> [entities=<ids>]: the state flags of the instance's entities (hex), one a line",
			[](const std::string& argument) { return EntityStates(argument); });
		DEBUG_MENU::AddCommand("debugger.values", "composite=<id> path=<ids|*> values=<entity>:<parameter>:<bool|int|float|string|vector|enum|guid>,...: what each entity reads for each parameter now",
			[](const std::string& argument) { return EntityValues(argument); });
		return true;
	}

	const bool g_registered = Register();
	const bool g_hooks = DEBUG_MENU::AddHooks(AttachHooks);
}

void LIVE_DEBUGGER::EntityFrame()
{
	uint32_t root = 0;
	const void* list = nullptr;
	if (!ReadLevel(root, list))
	{
		// The level went (or is not up yet): what was written went with it, and a hold must not carry on into the next one
		if (g_syncedRoot || g_syncedList)
		{
			g_syncedRoot = 0;
			g_syncedList = nullptr;
			Forget();
			g_dirty = true;
		}
		Release("the level is not running");
		return;
	}
	if (root != g_syncedRoot || list != g_syncedList)
	{
		// Another level, or the same one loaded again (a checkpoint restart makes the list afresh): a hold on the last one
		// must not carry on into it (its scripts would never start)
		if (g_syncedRoot || g_syncedList)
			Release(root != g_syncedRoot ? "another level" : "the level was loaded again");
		g_syncedRoot = root;
		g_syncedList = list;
		Forget();
		g_dirty = true;
	}
	// The pause state's breakpoint reason taken away by something else (another view, the game): no longer held
	if (g_held && !HasReason(kPauseBreakpoint))
	{
		g_held = false;
		g_atBreakpoint = false;
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			g_hit.held = false;
			g_hit.atBreakpoint = false;
		}
		g_version++;
		Publish();
	}

	bool any = false;
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		any = !g_breakpoints.empty() || !g_written.empty();
	}
	const ULONGLONG now = GetTickCount64();
	if (!g_dirty && (!any || now - g_lastSync < kResyncMs))
		return;
	if (!g_patched && any)
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		g_status = "Breakpoints are not available on this game release";
		g_dirty = false;
		g_syncs++;
		return;
	}
	g_dirty = false;
	g_lastSync = now;
	Sync(root);
}

const char* LIVE_DEBUGGER::HeldReason()
{
	if (!g_held)
		return nullptr;
	return g_atBreakpoint ? "The level's scripts are stopped at a breakpoint (Continue in OpenCAGE or in the Debug Menu's Debugger tab)"
		: "The level's scripts are stopped by the debugger (Continue in OpenCAGE or in the Debug Menu's Debugger tab)";
}

bool LIVE_DEBUGGER::Holding()
{
	return g_held;
}

std::vector<LIVE_DEBUGGER::BreakpointView> LIVE_DEBUGGER::Breakpoints()
{
	std::lock_guard<std::mutex> lock(g_mutex);
	std::vector<BreakpointView> views;
	for (const Breakpoint& bp : g_breakpoints)
	{
		BreakpointView view;
		view.id = bp.id;
		view.root = bp.root;
		view.composite = bp.composite;
		view.compositeName = bp.compositeName;
		view.anyInstance = bp.anyInstance;
		view.path = bp.path;
		view.entity = bp.entity;
		view.methods = bp.methods;
		view.label = bp.label;
		view.enabled = bp.enabled;
		view.hits = bp.hits;
		view.instances = bp.instances;
		view.records = bp.records;
		view.problem = bp.problem;
		views.push_back(view);
	}
	return views;
}

LIVE_DEBUGGER::HitView LIVE_DEBUGGER::Hit()
{
	std::lock_guard<std::mutex> lock(g_mutex);
	HitView hit = g_hit;
	hit.held = g_held;
	hit.atBreakpoint = g_atBreakpoint;
	return hit;
}

bool LIVE_DEBUGGER::Patched()
{
	return g_patched;
}

int LIVE_DEBUGGER::RecordsInGame()
{
	return g_recordsInGame;
}

std::string LIVE_DEBUGGER::Status()
{
	std::lock_guard<std::mutex> lock(g_mutex);
	return g_status;
}

std::string LIVE_DEBUGGER::Continue()
{
	if (!g_held && !HasReason(kPauseBreakpoint))
		return "not held: the scripts are running";
	if (!SetReason(kPauseBreakpoint | kPauseSingleCycle, false))
		return "not available on this game release";
	g_held = false;
	g_atBreakpoint = false;
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		g_hit.held = false;
		g_hit.atBreakpoint = false;
	}
	g_version++;
	Publish();
	DevTools::Log("Debugger: continue");
	return "continued";
}

std::string LIVE_DEBUGGER::Step(bool frame)
{
	if (!g_held)
		return "not held: a step goes on from a breakpoint (or a pause)";
	if (!SetReason(frame ? kPauseSingleFrame : kPauseSingleCycle, true))
		return "not available on this game release";
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		g_hit.steps++;
		g_hit.atBreakpoint = false;
	}
	g_atBreakpoint = false;
	g_version++;
	Publish();
	return frame ? "stepping one frame" : "stepping one round of script calls (the held call and those due with it)";
}

std::string LIVE_DEBUGGER::Pause()
{
	uint32_t root = 0;
	const void* list = nullptr;
	if (!ReadLevel(root, list))
		return "not paused: no level is running";
	if (g_held)
		return "already held";
	if (!SetReason(kPauseBreakpoint, true))
		return "not available on this game release";
	g_held = true;
	g_atBreakpoint = false;
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		g_hit.held = true;
		g_hit.atBreakpoint = false;
	}
	g_version++;
	Publish();
	DevTools::Log("Debugger: scripts paused");
	return "paused";
}

std::string LIVE_DEBUGGER::Remove(int id)
{
	bool removed = false;
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		for (auto it = g_breakpoints.begin(); it != g_breakpoints.end(); ++it)
			if (it->id == id)
			{
				g_breakpoints.erase(it);
				removed = true;
				break;
			}
	}
	if (!removed)
		return "no breakpoint " + std::to_string(id);
	Changed();
	return "removed " + std::to_string(id);
}

std::string LIVE_DEBUGGER::RemoveAll()
{
	size_t count = 0;
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		count = g_breakpoints.size();
		g_breakpoints.clear();
	}
	Changed();
	return "removed " + std::to_string(count);
}

std::string LIVE_DEBUGGER::Enable(int id, bool enabled)
{
	bool found = false;
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		for (Breakpoint& bp : g_breakpoints)
			if (bp.id == id)
			{
				bp.enabled = enabled;
				found = true;
			}
	}
	if (!found)
		return "no breakpoint " + std::to_string(id);
	Changed();
	return std::string(enabled ? "on " : "off ") + std::to_string(id);
}

std::string LIVE_DEBUGGER::IdText(uint32_t id)
{
	char text[16];
	snprintf(text, sizeof(text), "%02X-%02X-%02X-%02X", id & 0xFF, (id >> 8) & 0xFF, (id >> 16) & 0xFF, id >> 24);
	return text;
}

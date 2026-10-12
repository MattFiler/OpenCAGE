#include "LIVE_TRACE.h"
#include "LIVE_LINK_SERVER.h"
#include "DevTools.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <intrin.h>
#include <detours.h>

#include <algorithm>
#include <atomic>
#include <cstdio>
#include <cstring>
#include <mutex>
#include <string>
#include <utility>
#include <vector>

using LIVE_TRACE::kMaxPath;
using LIVE_TRACE::kMaxRecords;
using LIVE_TRACE::kMaxWatches;

/*
	Addresses are RVAs in the Steam retail AI.exe. See LIVE_TRACE.h for what is traced and how.
*/
namespace
{
	// ---- Game layouts ----
	constexpr uintptr_t kEntityManager = 0x0134ef40; // a pointer to the entity manager
	constexpr uintptr_t kPackFile = 0x0134ef44;      // the loaded COMMANDS.PAK, which packed offsets count from (in 4-byte words)
	constexpr uint32_t kManagerRootGuid = 0x0C;      // the running level's root composite id

	// An entity's fields
	constexpr uint32_t kEntityOwner = 0x08;          // the instance holding it (a counted pointer; none for the level's root instance)
	constexpr uint32_t kEntityGuid = 0x0C;           // its id in that instance's composite
	// A composite instance's
	constexpr uintptr_t kCompositeInstanceVTable = 0x00fe1d2c; // the vtable an instance object starts with
	constexpr uint32_t kInstanceTemplate = 0x30;     // the composite template it was made from (a packed pointer)
	constexpr uint32_t kTemplateGuid = 0x10;         // that template's composite id

	// An entity interface's parameter cache: a counted array of entries { parameter, value, first cached link }. An entry
	// whose parameter is linked has links and no value of its own; a cached link is { the linked entity's pin, the link's
	// delay, the linked entity (a counted pointer), the next link }.
	constexpr uint32_t kInterfaceCache = 0x04;
	struct CacheEntry
	{
		uint32_t pin;
		void* value;
		const uint8_t* links;
	};
	constexpr uint32_t kLinkPin = 0x00;
	constexpr uint32_t kLinkTarget = 0x08;
	constexpr uint32_t kLinkNext = 0x0C;
	constexpr int kMaxLinks = 64; // links read for one parameter (retail's most on one data pin is 25); any past it are counted, for the log

	// The game's counted allocation: for a single item, data is the object
	struct Allocation
	{
		void** vtable;
		volatile LONG references;
		uint32_t count;
		void* data;
	};

	// ---- Hooked game functions ----
	// An entity fires one of its outputs and follows every link out of it, on the entity's interface: the entity, the output,
	// the record that goes with the call, a delay and a flag. Every relay goes through it.
	typedef void(__thiscall* t_fire_output)(void* iface, Allocation** entity, const uint32_t* pin, void* info, double delay, bool flag);
	constexpr uintptr_t kFireOutput = 0x004be0d0;
	t_fire_output g_fireOutput = nullptr;

	// An entity reads one of its parameters as one kind of value, following the parameter's data link if it has one (to read
	// the linked entity's pin in turn), on the entity's interface: the entity, the parameter, where the value goes. Says
	// whether it found a value.
	typedef bool(__thiscall* t_read_parameter)(void* iface, Allocation** entity, const uint32_t* pin, void* out);
	constexpr uintptr_t kParameterReaders[] = {
		0x001991a0, // text
		0x00230590, // true/false
		0x002306a0, // an id
		0x00396570, // a whole number
		0x004d5f30, // a position
		0x004d6130, // a file path
		0x004d6260, // a spline
		0x004d63c0, // a direction
		0x004d6510, // a number
		0x004d6620, // an enum value
		0x004d7200, // a resource id
		0x004f8930, 0x004f8b50, 0x004f8d80, 0x004fcaa0, 0x0050b0c0, // object references of five kinds
	};
	constexpr int kParameterReaderCount = static_cast<int>(sizeof(kParameterReaders) / sizeof(kParameterReaders[0]));
	static_assert(kParameterReaderCount == 16, "one hook per reader");

	// The game's lock on an entity's parameter cache (held while the cache is read, so it is not dropped and rebuilt
	// meanwhile): made from the entity, and released. The lock itself is a counted pointer to the entity.
	typedef void* (__thiscall* t_cache_lock)(void* lock, Allocation** entity);
	typedef void(__thiscall* t_cache_unlock)(void* lock);
	constexpr uintptr_t kCacheLock = 0x004bca90;
	constexpr uintptr_t kCacheUnlock = 0x004bcbb0;

	// An entity's interface adds the entities attached to one of its pins to a list - what an entity writing an output sets,
	// or what a pin is attached to - going on, through itself, to an attached entity's own attached entities when the link
	// reaches a pin of it that is not its value itself (a composite instance's pin, say): the entity, the pin, the list.
	// Every write of an output through its data links goes through it (the game's lookup of them only starts it off).
	typedef void(__thiscall* t_attached_lookup)(void* iface, Allocation** entity, const uint32_t* pin, void* list);
	constexpr uintptr_t kAttachedLookup = 0x004be5c0;
	t_attached_lookup g_attachedLookup = nullptr;

	// The entity manager queues the call of a method that a fired output's logic link makes: the entity that fired, the
	// output, the cached link (the method, the link's delay, the entity it is called on), the record that goes with the call
	// (passed by value - the callee lets go of it), a delay and a flag.
	typedef void(__thiscall* t_queue_call)(void* manager, Allocation** source, const uint32_t* pin, const void* link, void* info, double delay, bool flag);
	constexpr uintptr_t kQueueCall = 0x005271c0;
	t_queue_call g_queueCall = nullptr;

	// Absolute addresses, worked out once when the hooks are attached (the hooks use them on every call)
	uintptr_t g_instanceVTable = 0;
	const uintptr_t* g_packFile = nullptr;
	uint8_t* const* g_entityManager = nullptr;
	t_cache_lock g_cacheLock = nullptr;
	t_cache_unlock g_cacheUnlock = nullptr;

	// ---- What is traced ----
	// Changed by the socket thread under g_lock (exclusive). The hooks look at the flag, the root and the composites without
	// it - a quick first look, which may be a moment out of date - and at the watches and the records under it.
	SRWLOCK g_lock = SRWLOCK_INIT;
	std::atomic<bool> g_on{ false };
	std::atomic<uint32_t> g_root{ 0 };                    // the level the trace is for (0: any)
	std::atomic<uint32_t> g_connection{ 0 };              // the connection that asked
	std::atomic<uint32_t> g_generation{ 0 };              // bumped by every TRACE
	std::vector<LIVE_TRACE::Watch> g_watches;             // under g_lock, in order of composite

	// The watched composites: a hash set (open addressing; 0 is an empty slot) the hooks look in without g_lock for the
	// first look, and - under it - where each composite's watches are in g_watches
	constexpr uint32_t kCompositeBits = 11;
	constexpr uint32_t kCompositeSlots = 1u << kCompositeBits;
	static_assert(kCompositeSlots >= 4 * kMaxWatches, "a quarter full at most, so a look takes a probe or two");
	std::atomic<uint32_t> g_composites[kCompositeSlots] = {};
	struct WatchRange
	{
		uint32_t first;
		uint32_t count;
	};
	WatchRange g_watchRanges[kCompositeSlots] = {};       // under g_lock

	// Socket thread requests (TRACE, TRACE_GET, a disconnect) one at a time
	std::mutex g_controlMutex;

	// ---- The records ----
	constexpr uint32_t kSlots = 8192;                     // the records' hash table: a power of two, twice kMaxRecords
	constexpr uint16_t kEmptySlot = 0xFFFF;
	constexpr uint32_t kDroppedHashBits = 65536;          // dropped activities are told apart by this many bits of their hash
	static_assert(kMaxRecords < kEmptySlot && kSlots >= 2 * kMaxRecords && (kSlots & (kSlots - 1)) == 0, "record table sizes");

	struct Record
	{
		// What happened (what tells records apart)
		uint8_t kind;
		uint8_t pathCount;
		uint8_t sourcePathCount;
		Allocation* entity;
		Allocation* source;
		uint32_t id, pin;
		uint32_t sourceId, sourcePin;
		// Where its ends sit
		uint32_t composite, self;
		uint32_t sourceComposite, sourceSelf;
		uint32_t pathAt;   // its paths in the store's path list: its own, then the source's
		// How often since the last take, and when last (written under the shared side of g_lock)
		LONG count;
		LONG last;         // the system tick count (ms), low 32 bits
	};

	// Everything a store holds is set aside when it is made (on the socket thread, outside g_lock): adding a record under
	// the lock never allocates, so nothing there can fail and leave the lock held. A record whose instance paths do not
	// fit in what is left of the path list is dropped like one the full store has no room for.
	struct Store
	{
		std::vector<Record> records;
		std::vector<uint16_t> slots;        // indexes into records, kEmptySlot where none
		std::vector<uint32_t> paths;
		std::vector<LONG> droppedSeen;      // a bit per dropped hash (set by the hooks under either side of g_lock)
		volatile LONG dropped = 0;          // likewise

		void Allocate()
		{
			records.reserve(kMaxRecords);
			slots.assign(kSlots, kEmptySlot);
			paths.reserve(kMaxRecords * 12);
			droppedSeen.assign(kDroppedHashBits / 32, 0);
			dropped = 0;
		}
		void Clear()
		{
			records.clear();
			std::fill(slots.begin(), slots.end(), kEmptySlot);
			paths.clear();
			std::fill(droppedSeen.begin(), droppedSeen.end(), 0);
			dropped = 0;
		}
		void Release()
		{
			std::vector<Record>().swap(records);
			std::vector<uint16_t>().swap(slots);
			std::vector<uint32_t>().swap(paths);
			std::vector<LONG>().swap(droppedSeen);
			dropped = 0;
		}
	};
	// The hooks add to the active store (under g_lock); a take swaps the spare in and reads the other out, outside the lock
	Store g_stores[2];
	Store* g_active = nullptr; // under g_lock; null while not tracing
	Store* g_spare = nullptr;  // the socket thread's

	// ---- Statistics, for the log ----
	std::atomic<uint32_t> g_faults{ 0 };
	std::atomic<uint32_t> g_firstFault{ 0 };  // the first fault's exception code since tracing started
	std::atomic<uint32_t> g_linksCut{ 0 };    // reads and writes whose pin had more links than are read (the rest not noted), since tracing started
	uint32_t g_batch = 0;                     // every take since the game started
	// The current trace's (socket thread)
	struct Session
	{
		LARGE_INTEGER startCounter = {};
		uint64_t startCycles = 0;
		uint32_t takes = 0;
		uint64_t taken = 0;
		uint64_t dropped = 0;
		uint32_t faultsAtStart = 0;
		bool faultLogged = false;
	};
	Session g_session;
	// The hooks' own time while tracing: counted per thread and added in now and then, so they do not all write one counter
	struct ThreadCost
	{
		uint32_t generation;
		uint32_t calls;
		uint64_t cycles;
	};
	thread_local ThreadCost t_cost = {};
	constexpr uint32_t kCostBatch = 256;
	std::atomic<uint64_t> g_costCycles{ 0 };
	std::atomic<uint64_t> g_costCalls{ 0 };

	int Fault(unsigned int code)
	{
		g_faults.fetch_add(1, std::memory_order_relaxed);
		uint32_t none = 0;
		g_firstFault.compare_exchange_strong(none, code, std::memory_order_relaxed);
		return EXCEPTION_EXECUTE_HANDLER;
	}

	void Account(uint64_t startCycles)
	{
		ThreadCost& cost = t_cost;
		const uint32_t generation = g_generation.load(std::memory_order_relaxed);
		if (cost.generation != generation)
			cost = { generation, 0, 0 };
		cost.cycles += __rdtsc() - startCycles;
		if (++cost.calls == kCostBatch)
		{
			g_costCycles.fetch_add(cost.cycles, std::memory_order_relaxed);
			g_costCalls.fetch_add(cost.calls, std::memory_order_relaxed);
			cost.calls = 0;
			cost.cycles = 0;
		}
	}

	std::string Hex(uint32_t value)
	{
		char buffer[16];
		snprintf(buffer, sizeof(buffer), "%02X-%02X-%02X-%02X", value & 0xFF, (value >> 8) & 0xFF, (value >> 16) & 0xFF, value >> 24);
		return buffer;
	}

	// ---- Reading the game (guarded: a fault means "not noted") ----
	// One end of an activity
	struct End
	{
		Allocation* entity;
		Allocation* owner;  // the instance holding it (null for the level's root instance)
		uint32_t id;
		uint32_t composite; // the composite of the instance holding it (0: none, or the holder is not an instance)
		uint32_t self;      // the composite it is an instance of (0: it is not an instance)
	};

	struct Activity
	{
		uint8_t kind;
		uint32_t pin;       // the output fired, the parameter read, the pin written through, or the method called
		uint32_t sourcePin; // a read or a write: the pin at the link's other end; a call: the output that called
		End entity;         // the entity that fired, read or wrote, or whose method was called
		End source;         // a read or a write: the entity at the link's other end; a call: the entity that called
	};

	// Whether a kind of activity has a second end (the source fields of its record)
	bool HasSource(uint8_t kind)
	{
		return kind != LIVE_TRACE::Fired;
	}

	bool IsInstance(const void* object)
	{
		return *static_cast<const uintptr_t*>(object) == g_instanceVTable;
	}

	uint32_t InstanceComposite(const uint8_t* instance)
	{
		const uint32_t packed = *reinterpret_cast<const uint32_t*>(instance + kInstanceTemplate);
		if (packed == 0xFFFFFFFF)
			return 0;
		return *reinterpret_cast<const uint32_t*>(static_cast<uintptr_t>(*g_packFile + packed * 4u) + kTemplateGuid);
	}

	bool ReadEnd(Allocation* entity, End& end)
	{
		__try
		{
			if (!entity || !entity->data)
				return false;
			const uint8_t* object = static_cast<const uint8_t*>(entity->data);
			end.entity = entity;
			end.id = *reinterpret_cast<const uint32_t*>(object + kEntityGuid);
			end.self = IsInstance(object) ? InstanceComposite(object) : 0;
			end.owner = *reinterpret_cast<Allocation* const*>(object + kEntityOwner);
			end.composite = end.owner && end.owner->data && IsInstance(end.owner->data) ? InstanceComposite(static_cast<const uint8_t*>(end.owner->data)) : 0;
			return true;
		}
		__except (Fault(GetExceptionCode()))
		{
			return false;
		}
	}

	// What a hook was handed: the entity and the pin
	bool ReadCall(Allocation* const* entity, const uint32_t* pin, Allocation*& entityOut, uint32_t& pinOut)
	{
		__try
		{
			if (!entity || !pin)
				return false;
			entityOut = *entity;
			pinOut = *pin;
			return entityOut != nullptr;
		}
		__except (Fault(GetExceptionCode()))
		{
			return false;
		}
	}

	// The running level's root composite (0: none)
	uint32_t RunningRoot()
	{
		__try
		{
			const uint8_t* manager = *g_entityManager;
			return manager ? *reinterpret_cast<const uint32_t*>(manager + kManagerRootGuid) : 0;
		}
		__except (Fault(GetExceptionCode()))
		{
			return 0;
		}
	}

	// Whether the running level is the one traced; root is set to it
	bool LevelTraced(uint32_t& root)
	{
		root = RunningRoot();
		const uint32_t traced = g_root.load(std::memory_order_relaxed);
		return root != 0 && (traced == 0 || traced == root);
	}

	// The instance path to an owner: the ids of it, its owner, ... up to (not including) the level's root instance, from the
	// root down. False if it is longer than a path may be, or an instance on the way has gone.
	bool ReadPath(Allocation* owner, uint32_t* path, uint32_t& count)
	{
		__try
		{
			uint32_t reversed[kMaxPath];
			count = 0;
			for (Allocation* step = owner; step; )
			{
				const uint8_t* object = static_cast<const uint8_t*>(step->data);
				if (!object)
					return false;
				Allocation* up = *reinterpret_cast<Allocation* const*>(object + kEntityOwner);
				if (!up)
					break; // the root
				if (count == kMaxPath)
					return false;
				reversed[count++] = *reinterpret_cast<const uint32_t*>(object + kEntityGuid);
				step = up;
			}
			for (uint32_t i = 0; i < count; i++)
				path[i] = reversed[count - 1 - i];
			return true;
		}
		__except (Fault(GetExceptionCode()))
		{
			return false;
		}
	}

	bool LockCache(void* lock, Allocation** entity)
	{
		__try
		{
			g_cacheLock(lock, entity);
			return true;
		}
		__except (Fault(GetExceptionCode()))
		{
			return false;
		}
	}

	void UnlockCache(void* lock)
	{
		__try
		{
			g_cacheUnlock(lock);
		}
		__except (Fault(GetExceptionCode()))
		{
		}
	}

	struct FollowedLink
	{
		uint32_t pin;
		End source;
	};

	// The links the reader's cache holds for one parameter, with who is at their other end (read under the game's lock on
	// that cache, so nothing in it goes meanwhile): the first 64 of them (a pin with more is counted, for the log); -1 if
	// anything faulted
	int ReadFollowedLinks(const void* iface, uint32_t pin, FollowedLink* out)
	{
		__try
		{
			const Allocation* cache = *reinterpret_cast<Allocation* const*>(static_cast<const uint8_t*>(iface) + kInterfaceCache);
			if (!cache || !cache->data)
				return 0;
			const CacheEntry* entries = static_cast<const CacheEntry*>(cache->data);
			for (uint32_t i = 0; i < cache->count; i++)
			{
				if (entries[i].pin != pin)
					continue;
				int count = 0;
				const uint8_t* link = entries[i].links;
				for (; link && count < kMaxLinks; link = *reinterpret_cast<const uint8_t* const*>(link + kLinkNext))
				{
					out[count].pin = *reinterpret_cast<const uint32_t*>(link + kLinkPin);
					if (ReadEnd(*reinterpret_cast<Allocation* const*>(link + kLinkTarget), out[count].source))
						count++;
				}
				if (link)
					g_linksCut.fetch_add(1, std::memory_order_relaxed); // more links than are read: those past them are not noted
				return count;
			}
			return 0;
		}
		__except (Fault(GetExceptionCode()))
		{
			return -1;
		}
	}

	// ---- The first look: is a composite watched? ----
	uint32_t FirstSlot(uint32_t composite)
	{
		return (composite * 0x9E3779B1u) >> (32 - kCompositeBits);
	}

	// The slot of a watched composite, or -1. Without g_lock (the first look) a TRACE changing the set meanwhile may make it
	// miss or find one for a moment; the exact look, under the lock, settles it.
	int FindComposite(uint32_t composite)
	{
		if (!composite)
			return -1;
		uint32_t slot = FirstSlot(composite);
		for (uint32_t probe = 0; probe < kCompositeSlots; probe++, slot = (slot + 1) & (kCompositeSlots - 1))
		{
			const uint32_t held = g_composites[slot].load(std::memory_order_relaxed);
			if (held == composite)
				return static_cast<int>(slot);
			if (!held)
				return -1;
		}
		return -1;
	}

	bool EndWanted(const End& end)
	{
		return FindComposite(end.composite) >= 0 || FindComposite(end.self) >= 0;
	}

	// Under g_lock (exclusive): puts the watches in order of composite and the composites in the set
	void IndexWatchesLocked()
	{
		for (std::atomic<uint32_t>& slot : g_composites)
			slot.store(0, std::memory_order_relaxed);
		std::sort(g_watches.begin(), g_watches.end(), [](const LIVE_TRACE::Watch& a, const LIVE_TRACE::Watch& b) { return a.composite < b.composite; });
		const uint32_t count = static_cast<uint32_t>(g_watches.size());
		uint32_t end = 0;
		for (uint32_t first = 0; first < count; first = end)
		{
			const uint32_t composite = g_watches[first].composite;
			end = first + 1;
			while (end < count && g_watches[end].composite == composite)
				end++;
			if (!composite)
				continue; // a watch of composite 0 never matches
			uint32_t slot = FirstSlot(composite);
			while (g_composites[slot].load(std::memory_order_relaxed))
				slot = (slot + 1) & (kCompositeSlots - 1);
			g_watchRanges[slot] = { first, end - first };
			g_composites[slot].store(composite, std::memory_order_release);
		}
	}

	// ---- The exact look (under g_lock) ----
	bool SamePath(const std::vector<uint32_t>& watched, const uint32_t* path, uint32_t count)
	{
		return watched.size() == count && std::equal(watched.begin(), watched.end(), path);
	}

	// Whether a watch keeps an end: it sits in a watched instance, or it is one (its own path is its owner's and its id,
	// or none for the level's root instance). Only the watches of the end's composites are looked at.
	bool EndWatched(const End& end, const uint32_t* path, uint32_t count)
	{
		const int sitsIn = FindComposite(end.composite);
		if (sitsIn >= 0)
		{
			const WatchRange range = g_watchRanges[sitsIn];
			for (uint32_t i = range.first; i < range.first + range.count; i++)
				if (g_watches[i].any || SamePath(g_watches[i].path, path, count))
					return true;
		}
		const int is = FindComposite(end.self);
		if (is >= 0)
		{
			const WatchRange range = g_watchRanges[is];
			for (uint32_t i = range.first; i < range.first + range.count; i++)
			{
				const LIVE_TRACE::Watch& watch = g_watches[i];
				if (watch.any)
					return true;
				if (!end.owner)
				{
					if (watch.path.empty())
						return true;
				}
				else if (watch.path.size() == count + 1 && std::equal(path, path + count, watch.path.begin()) && watch.path[count] == end.id)
				{
					return true;
				}
			}
		}
		return false;
	}

	// ---- Records ----
	uint32_t Hash(const Activity& activity)
	{
		uint32_t hash = 2166136261u;
		auto mix = [&hash](uint32_t value) { hash = (hash ^ value) * 16777619u; };
		mix(activity.kind);
		mix(static_cast<uint32_t>(reinterpret_cast<uintptr_t>(activity.entity.entity)));
		mix(activity.entity.id);
		mix(activity.pin);
		mix(static_cast<uint32_t>(reinterpret_cast<uintptr_t>(activity.source.entity)));
		mix(activity.source.id);
		mix(activity.sourcePin);
		hash ^= hash >> 16;
		hash *= 0x85ebca6bu;
		hash ^= hash >> 13;
		hash *= 0xc2b2ae35u;
		hash ^= hash >> 16;
		return hash;
	}

	bool Same(const Record& record, const Activity& activity)
	{
		return record.kind == activity.kind && record.entity == activity.entity.entity && record.id == activity.entity.id && record.pin == activity.pin &&
			record.source == activity.source.entity && record.sourceId == activity.source.id && record.sourcePin == activity.sourcePin;
	}

	// Under either side of g_lock
	Record* Find(Store& store, const Activity& activity, uint32_t hash)
	{
		for (uint32_t slot = hash & (kSlots - 1); store.slots[slot] != kEmptySlot; slot = (slot + 1) & (kSlots - 1))
		{
			Record& record = store.records[store.slots[slot]];
			if (Same(record, activity))
				return &record;
		}
		return nullptr;
	}

	// Under either side of g_lock: once more, saturating, and when
	void Count(Record& record, LONG now)
	{
		for (LONG count = record.count; static_cast<uint32_t>(count) != 0xFFFFFFFFu; count = record.count)
			if (InterlockedCompareExchange(&record.count, count + 1, count) == count)
				break;
		InterlockedExchange(&record.last, now);
	}

	// Under either side of g_lock: the store has no room for an activity - counted as dropped, once for its hash
	void Drop(Store& store, uint32_t hash)
	{
		const uint32_t bit = hash & (kDroppedHashBits - 1);
		const LONG mask = static_cast<LONG>(1u << (bit % 32));
		if (!(InterlockedOr(&store.droppedSeen[bit / 32], mask) & mask))
			InterlockedIncrement(&store.dropped);
	}

	// Under either side of g_lock: whether the store is full, and an activity with this hash has been counted as dropped
	// already (another of it would change nothing)
	bool DroppedAlready(const Store& store, uint32_t hash)
	{
		if (store.records.size() < kMaxRecords)
			return false;
		const uint32_t bit = hash & (kDroppedHashBits - 1);
		const volatile LONG& word = store.droppedSeen[bit / 32];
		return (word & static_cast<LONG>(1u << (bit % 32))) != 0;
	}

	// Under either side of g_lock: whether the store has room for one more record, with this many instance path steps
	bool Fits(const Store& store, uint32_t pathSteps)
	{
		return store.records.size() < kMaxRecords && store.paths.size() + pathSteps <= store.paths.capacity();
	}

	// Under g_lock (exclusive)
	void Add(Store& store, const Activity& activity, uint32_t hash, LONG now, const uint32_t* path, uint32_t pathCount, const uint32_t* sourcePath, uint32_t sourcePathCount)
	{
		if (!Fits(store, pathCount + sourcePathCount))
		{
			Drop(store, hash);
			return;
		}
		Record record = {};
		record.kind = activity.kind;
		record.pathCount = static_cast<uint8_t>(pathCount);
		record.sourcePathCount = static_cast<uint8_t>(sourcePathCount);
		record.entity = activity.entity.entity;
		record.source = activity.source.entity;
		record.id = activity.entity.id;
		record.pin = activity.pin;
		record.sourceId = activity.source.id;
		record.sourcePin = activity.sourcePin;
		record.composite = activity.entity.composite;
		record.self = activity.entity.self;
		record.sourceComposite = activity.source.composite;
		record.sourceSelf = activity.source.self;
		record.pathAt = static_cast<uint32_t>(store.paths.size());
		record.count = 1;
		record.last = now;
		store.paths.insert(store.paths.end(), path, path + pathCount);
		store.paths.insert(store.paths.end(), sourcePath, sourcePath + sourcePathCount);
		uint32_t slot = hash & (kSlots - 1);
		while (store.slots[slot] != kEmptySlot)
			slot = (slot + 1) & (kSlots - 1);
		store.slots[slot] = static_cast<uint16_t>(store.records.size());
		store.records.push_back(record);
	}

	// Whether the trace (as it is now, under g_lock) is for this level
	bool RootTracedLocked(uint32_t root)
	{
		const uint32_t traced = g_root.load(std::memory_order_relaxed);
		return traced == 0 || traced == root;
	}

	// An activity that passed the first look: counted if it is already kept, else its paths are read and, if a watch keeps
	// it, it is added. Once the store is full nothing more is added until the next take, and the shared side of g_lock is
	// all that takes (counting one as dropped included), so a full store never holds the hooks up on each other.
	void Keep(const Activity& activity, uint32_t root)
	{
		const uint32_t hash = Hash(activity);
		const LONG now = static_cast<LONG>(GetTickCount());

		AcquireSRWLockShared(&g_lock);
		bool done = false;
		if (g_active && RootTracedLocked(root))
		{
			if (Record* record = Find(*g_active, activity, hash))
			{
				Count(*record, now);
				done = true;
			}
			else
			{
				done = DroppedAlready(*g_active, hash);
			}
		}
		ReleaseSRWLockShared(&g_lock);
		if (done)
			return;

		uint32_t path[kMaxPath], sourcePath[kMaxPath];
		uint32_t pathCount = 0, sourcePathCount = 0;
		const bool twoEnds = HasSource(activity.kind);
		if (!ReadPath(activity.entity.owner, path, pathCount))
			return;
		if (twoEnds && !ReadPath(activity.source.owner, sourcePath, sourcePathCount))
			return;

		AcquireSRWLockShared(&g_lock);
		const bool kept = g_active && RootTracedLocked(root) && (EndWatched(activity.entity, path, pathCount) ||
			(twoEnds && EndWatched(activity.source, sourcePath, sourcePathCount)));
		// No room for it: another thread may have added it meanwhile, else it is dropped
		const bool full = kept && !Fits(*g_active, pathCount + sourcePathCount);
		if (full)
		{
			if (Record* again = Find(*g_active, activity, hash))
				Count(*again, now);
			else
				Drop(*g_active, hash);
		}
		ReleaseSRWLockShared(&g_lock);
		if (!kept || full)
			return;

		AcquireSRWLockExclusive(&g_lock);
		// Looked at again: the trace may have changed, or another thread added it, since
		if (g_active && RootTracedLocked(root) && (EndWatched(activity.entity, path, pathCount) ||
			(twoEnds && EndWatched(activity.source, sourcePath, sourcePathCount))))
		{
			if (Record* again = Find(*g_active, activity, hash))
				Count(*again, now);
			else
				Add(*g_active, activity, hash, now, path, pathCount, sourcePath, sourcePathCount);
		}
		ReleaseSRWLockExclusive(&g_lock);
	}

	// ---- The hooks ----
	__declspec(noinline) void NoteFired(Allocation** entity, const uint32_t* pin)
	{
		const uint64_t start = __rdtsc();
		Activity activity = {};
		activity.kind = LIVE_TRACE::Fired;
		Allocation* fired = nullptr;
		uint32_t root = 0;
		if (LevelTraced(root) && ReadCall(entity, pin, fired, activity.pin) && activity.pin != 0 && ReadEnd(fired, activity.entity) && EndWanted(activity.entity))
			Keep(activity, root);
		Account(start);
	}

	// A read or a write: each link the entity's cache holds for the pin is one activity (the entity and its pin; the entity
	// and pin at the link's other end)
	__declspec(noinline) void NoteLinks(uint8_t kind, void* iface, Allocation** entity, const uint32_t* pin)
	{
		const uint64_t start = __rdtsc();
		Activity activity = {};
		activity.kind = kind;
		Allocation* owner = nullptr;
		uint32_t root = 0;
		if (iface && LevelTraced(root) && ReadCall(entity, pin, owner, activity.pin) && ReadEnd(owner, activity.entity))
		{
			const bool entityWanted = EndWanted(activity.entity);
			FollowedLink links[kMaxLinks];
			alignas(4) uint8_t lock[16] = {};
			int count = -1;
			if (LockCache(lock, entity))
			{
				count = ReadFollowedLinks(iface, activity.pin, links);
				UnlockCache(lock);
			}
			for (int i = 0; i < count; i++)
			{
				if (!entityWanted && !EndWanted(links[i].source))
					continue;
				activity.sourcePin = links[i].pin;
				activity.source = links[i].source;
				Keep(activity, root);
			}
		}
		Account(start);
	}

	// A cached link's pin (the method it calls) and the entity it leads to
	bool ReadLinkTarget(const void* link, uint32_t& pin, Allocation*& target)
	{
		__try
		{
			if (!link)
				return false;
			pin = *reinterpret_cast<const uint32_t*>(static_cast<const uint8_t*>(link) + kLinkPin);
			target = *reinterpret_cast<Allocation* const*>(static_cast<const uint8_t*>(link) + kLinkTarget);
			return target != nullptr;
		}
		__except (Fault(GetExceptionCode()))
		{
			return false;
		}
	}

	// A call queued through a link: the entity whose method is called (and the method), and the entity that called (and
	// its output) - none if the call came from nothing (its end is then left empty)
	__declspec(noinline) void NoteCall(Allocation** source, const uint32_t* pin, const void* link)
	{
		const uint64_t start = __rdtsc();
		Activity activity = {};
		activity.kind = LIVE_TRACE::Called;
		Allocation* target = nullptr;
		Allocation* caller = nullptr;
		uint32_t root = 0;
		if (LevelTraced(root) && ReadLinkTarget(link, activity.pin, target) && ReadEnd(target, activity.entity))
		{
			bool read = true;
			if (ReadCall(source, pin, caller, activity.sourcePin))
				read = ReadEnd(caller, activity.source);
			if (read && (EndWanted(activity.entity) || EndWanted(activity.source)))
				Keep(activity, root);
		}
		Account(start);
	}

	void __fastcall FireOutputHook(void* iface, void* /*edx*/, Allocation** entity, const uint32_t* pin, void* info, double delay, bool flag)
	{
		if (g_on.load(std::memory_order_acquire))
			NoteFired(entity, pin);
		g_fireOutput(iface, entity, pin, info, delay, flag);
	}

	// The lookup notes after it has looked (its cache is then up to date); a lookup that goes on to an attached entity's
	// own attached entities comes back through here for that step first
	void __fastcall AttachedLookupHook(void* iface, void* /*edx*/, Allocation** entity, const uint32_t* pin, void* list)
	{
		g_attachedLookup(iface, entity, pin, list);
		if (g_on.load(std::memory_order_acquire))
			NoteLinks(LIVE_TRACE::Sent, iface, entity, pin);
	}

	// Noted before the call is queued: the link and both entities are the caller's, and stay as they are either way
	void __fastcall QueueCallHook(void* manager, void* /*edx*/, Allocation** source, const uint32_t* pin, const void* link, void* info, double delay, bool flag)
	{
		if (g_on.load(std::memory_order_acquire))
			NoteCall(source, pin, link);
		g_queueCall(manager, source, pin, link, info, delay, flag);
	}

	// One hook per reader (each has its own original to call)
	template<int N> struct ReaderHook
	{
		static inline t_read_parameter original = nullptr;
		static bool __fastcall Hook(void* iface, void* /*edx*/, Allocation** entity, const uint32_t* pin, void* out)
		{
			const bool found = original(iface, entity, pin, out);
			if (g_on.load(std::memory_order_acquire))
				NoteLinks(LIVE_TRACE::Read, iface, entity, pin);
			return found;
		}
	};

	template<int N> void AttachReader(bool attach)
	{
		if (attach)
		{
			ReaderHook<N>::original = reinterpret_cast<t_read_parameter>(DEVTOOLS_RELATIVE_ADDRESS(kParameterReaders[N]));
			DEVTOOLS_DETOURS_ATTACH(ReaderHook<N>::original, ReaderHook<N>::Hook);
		}
		else
		{
			DEVTOOLS_DETOURS_DETACH(ReaderHook<N>::original, ReaderHook<N>::Hook);
		}
	}
	template<int... N> void AttachReaders(bool attach, std::integer_sequence<int, N...>)
	{
		(AttachReader<N>(attach), ...);
	}

	// ---- Socket thread ----
	enum class Level { None, Other, Same };

	// Plain reads of the entity manager (fine from the socket thread), guarded: it can go while a level unloads
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

	constexpr const char* kOtherLevel = "Another level is running";

	// Under g_controlMutex: logs the first fault of this trace, once
	void LogFaultsLocked()
	{
		const uint32_t faults = g_faults.load() - g_session.faultsAtStart;
		if (faults && !g_session.faultLogged)
		{
			g_session.faultLogged = true;
			DevTools::Log("LiveLink: reading the game faulted while tracing (exception 0x%08X) - what it was reading was not noted", g_firstFault.load());
		}
	}

	// Under g_controlMutex: logs what the trace that is ending did
	void LogSessionLocked(const char* why)
	{
		LARGE_INTEGER now, frequency;
		QueryPerformanceCounter(&now);
		QueryPerformanceFrequency(&frequency);
		const double seconds = static_cast<double>(now.QuadPart - g_session.startCounter.QuadPart) / static_cast<double>(frequency.QuadPart);
		const double cyclesPerSecond = seconds > 0 ? static_cast<double>(__rdtsc() - g_session.startCycles) / seconds : 0.0;
		const double hookMs = cyclesPerSecond > 0 ? static_cast<double>(g_costCycles.load()) / cyclesPerSecond * 1000.0 : 0.0;
		LogFaultsLocked();
		DevTools::Log("LiveLink: tracing %s after %.1f s: about %llu hook calls looked at, taking %.2f ms (%.3f ms a second); %llu records taken in %u takes, %llu dropped, %u reads or writes through a pin with more than %d links (the rest not noted), %u faults",
			why, seconds, static_cast<unsigned long long>(g_costCalls.load()), hookMs, seconds > 0 ? hookMs / seconds : 0.0,
			static_cast<unsigned long long>(g_session.taken), g_session.takes, static_cast<unsigned long long>(g_session.dropped),
			g_linksCut.load(), kMaxLinks, g_faults.load() - g_session.faultsAtStart);
	}

	// Under g_controlMutex: stops tracing, dropping what was gathered (and the memory it took, unless another trace follows)
	void StopLocked(const char* why, bool keepMemory)
	{
		if (!g_on.load())
			return;
		AcquireSRWLockExclusive(&g_lock);
		g_on.store(false, std::memory_order_release);
		g_watches.clear();
		IndexWatchesLocked();
		g_active = nullptr;
		g_spare = nullptr;
		g_generation.fetch_add(1, std::memory_order_relaxed);
		ReleaseSRWLockExclusive(&g_lock);
		for (Store& store : g_stores)
		{
			if (keepMemory)
				store.Clear();
			else
				store.Release();
		}
		LogSessionLocked(why);
	}
}

LIVE_LINK::Result LIVE_TRACE::Set(uint32_t connection, uint32_t root, bool on, std::vector<Watch>&& watches)
{
	std::lock_guard<std::mutex> control(g_controlMutex);
	if (!on)
	{
		// Taken whatever is running
		StopLocked("stopped", false);
		return LIVE_LINK::Result{ true, "Tracing stopped" };
	}
	// What was traced stops either way: a new TRACE replaces it, and one for another level means OpenCAGE has moved on
	if (RunningLevel(root) == Level::Other)
	{
		StopLocked("stopped (OpenCAGE asked for another level)", false);
		return LIVE_LINK::Result{ false, kOtherLevel };
	}
	StopLocked("replaced", true);

	for (Store& store : g_stores)
	{
		if (store.slots.empty())
			store.Allocate();
		else
			store.Clear();
	}
	// The log names the first few watches
	constexpr size_t kWatchesNamed = 16;
	std::string described;
	for (size_t i = 0; i < watches.size() && i < kWatchesNamed; i++)
	{
		const Watch& watch = watches[i];
		described += (described.empty() ? "" : ", ") + Hex(watch.composite) + (watch.any ? " (every instance)" : watch.path.empty() ? " (the root)" : " (" + std::to_string(watch.path.size()) + "-step path)");
	}
	if (watches.size() > kWatchesNamed)
		described += " and " + std::to_string(watches.size() - kWatchesNamed) + " more";
	const size_t count = watches.size();

	AcquireSRWLockExclusive(&g_lock);
	g_watches = std::move(watches);
	IndexWatchesLocked();
	g_root.store(root, std::memory_order_relaxed);
	g_connection.store(connection, std::memory_order_relaxed);
	g_generation.fetch_add(1, std::memory_order_relaxed);
	g_active = &g_stores[0];
	g_spare = &g_stores[1];
	g_on.store(true, std::memory_order_release);
	ReleaseSRWLockExclusive(&g_lock);

	g_session = Session();
	QueryPerformanceCounter(&g_session.startCounter);
	g_session.startCycles = __rdtsc();
	g_session.faultsAtStart = g_faults.load();
	g_firstFault.store(0);
	g_linksCut.store(0);
	g_costCycles.store(0);
	g_costCalls.store(0);
	DevTools::Log("LiveLink: tracing %u composite(s) in %s: %s", static_cast<unsigned>(count), root ? ("level " + Hex(root)).c_str() : "any level", described.c_str());
	return LIVE_LINK::Result{ true, "Tracing " + std::to_string(count) + " composite(s)" };
}

LIVE_LINK::Result LIVE_TRACE::Take(uint32_t connection, uint32_t root, std::vector<uint8_t>& payload)
{
	std::lock_guard<std::mutex> control(g_controlMutex);
	if (RunningLevel(root) == Level::Other)
		return LIVE_LINK::Result{ false, kOtherLevel };

	// The spare goes in, and what was gathered comes out: no hook can be in it once the lock has been held
	Store* taken = nullptr;
	AcquireSRWLockExclusive(&g_lock);
	if (g_active && g_connection.load(std::memory_order_relaxed) == connection)
	{
		taken = g_active;
		g_active = g_spare;
		g_spare = taken;
	}
	ReleaseSRWLockExclusive(&g_lock);

	const uint32_t batch = ++g_batch;
	const uint32_t dropped = taken ? static_cast<uint32_t>(taken->dropped) : 0;
	const uint32_t count = taken ? static_cast<uint32_t>(taken->records.size()) : 0;
	size_t size = 16;
	for (uint32_t i = 0; i < count; i++)
	{
		const Record& record = taken->records[i];
		size += 28 + (HasSource(record.kind) ? 16 : 0) + 4u * (record.pathCount + record.sourcePathCount);
	}
	payload.resize(size);
	uint8_t* out = payload.data();
	auto u8 = [&out](uint8_t value) { *out++ = value; };
	auto u32 = [&out](uint32_t value) { memcpy(out, &value, 4); out += 4; };
	u32(kFormat);
	u32(batch);
	u32(dropped);
	u32(count);
	const uint32_t now = GetTickCount();
	for (uint32_t i = 0; i < count; i++)
	{
		const Record& record = taken->records[i];
		const bool twoEnds = HasSource(record.kind);
		u8(record.kind);
		u8(record.pathCount);
		u8(twoEnds ? record.sourcePathCount : 0);
		u8(0);
		u32(static_cast<uint32_t>(record.count));
		u32(now - static_cast<uint32_t>(record.last));
		u32(record.composite);
		u32(record.id);
		u32(record.pin);
		u32(record.self);
		if (twoEnds)
		{
			u32(record.sourceComposite);
			u32(record.sourceId);
			u32(record.sourcePin);
			u32(record.sourceSelf);
		}
		const uint32_t* path = taken->paths.data() + record.pathAt;
		for (uint32_t step = 0; step < record.pathCount; step++)
			u32(path[step]);
		if (twoEnds)
			for (uint32_t step = 0; step < record.sourcePathCount; step++)
				u32(path[record.pathCount + step]);
	}
	if (taken)
	{
		taken->Clear();
		g_session.takes++;
		g_session.taken += count;
		g_session.dropped += dropped;
		LogFaultsLocked();
	}
	return LIVE_LINK::Result{ true, "records=" + std::to_string(count) + " dropped=" + std::to_string(dropped) };
}

void LIVE_TRACE::ConnectionEnded(uint32_t connection)
{
	std::lock_guard<std::mutex> control(g_controlMutex);
	if (g_on.load() && g_connection.load() == connection)
		StopLocked("stopped (OpenCAGE disconnected)", false);
}

bool LIVE_TRACE::Tracing()
{
	return g_on.load() && g_connection.load() == LIVE_LINK_SERVER::CurrentConnection();
}

void LIVE_TRACE::NoteOwnCall(void* entity, uint32_t method)
{
	if (!g_on.load(std::memory_order_acquire))
		return;
	Activity activity = {};
	activity.kind = Called;
	activity.pin = method;
	uint32_t root = 0;
	if (LevelTraced(root) && ReadEnd(static_cast<Allocation*>(entity), activity.entity) && EndWanted(activity.entity))
		Keep(activity, root);
}

void LIVE_TRACE::AttachHooks(bool attach)
{
	if (attach)
	{
		g_instanceVTable = DEVTOOLS_RELATIVE_ADDRESS(kCompositeInstanceVTable);
		g_packFile = reinterpret_cast<const uintptr_t*>(DEVTOOLS_RELATIVE_ADDRESS(kPackFile));
		g_entityManager = reinterpret_cast<uint8_t* const*>(DEVTOOLS_RELATIVE_ADDRESS(kEntityManager));
		g_cacheLock = reinterpret_cast<t_cache_lock>(DEVTOOLS_RELATIVE_ADDRESS(kCacheLock));
		g_cacheUnlock = reinterpret_cast<t_cache_unlock>(DEVTOOLS_RELATIVE_ADDRESS(kCacheUnlock));
		g_fireOutput = reinterpret_cast<t_fire_output>(DEVTOOLS_RELATIVE_ADDRESS(kFireOutput));
		g_attachedLookup = reinterpret_cast<t_attached_lookup>(DEVTOOLS_RELATIVE_ADDRESS(kAttachedLookup));
		g_queueCall = reinterpret_cast<t_queue_call>(DEVTOOLS_RELATIVE_ADDRESS(kQueueCall));
		DEVTOOLS_DETOURS_ATTACH(g_fireOutput, FireOutputHook);
		DEVTOOLS_DETOURS_ATTACH(g_attachedLookup, AttachedLookupHook);
		DEVTOOLS_DETOURS_ATTACH(g_queueCall, QueueCallHook);
	}
	else
	{
		DEVTOOLS_DETOURS_DETACH(g_fireOutput, FireOutputHook);
		DEVTOOLS_DETOURS_DETACH(g_attachedLookup, AttachedLookupHook);
		DEVTOOLS_DETOURS_DETACH(g_queueCall, QueueCallHook);
	}
	AttachReaders(attach, std::make_integer_sequence<int, kParameterReaderCount>{});
}

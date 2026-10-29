#include "SCRIPT_DEBUG.h"
#include "DEBUG_MENU.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <intrin.h>

#include <algorithm>
#include <atomic>
#include <cstdarg>
#include <cstdio>
#include <cstring>
#include <string>
#include <unordered_map>
#include <vector>

#include <imgui.h>

/*
	Script errors (debug menu: Scripts > Errors).

	The game's code checks that the level's script entities are set up as they should be - a pickup spawner that names
	no inventory item, a door with no animation, a speech entity with nothing to speak through, an animation set missing
	the animation asked for, a zone link assigned the wrong way - and reports what it finds, with the entity, in its
	authors' own words: each is a call with the entity, the result of the check and the message. In the shipped game the
	function those calls reach does nothing (the same empty function is shared by hundreds of other unrelated calls, so
	it is never changed itself). Here each of those calls (173 of them, listed below) is pointed at a
	function of ours instead, which keeps what it is given. There are two kinds: a check (the entity, whether it passed,
	the message and its values - noted only when it failed) and an error (the entity, the message and its values).

	Also kept: the calls the game drops without a word. A method called through a link is only run while the entity that
	started the chain of calls it belongs to is still set up; once that entity is shut down (its zone unloaded, say, or
	the entity deleted) every call still waiting from its chain is thrown away. The game's check is made in two places -
	as a waiting call comes due, and again just before it runs - and both calls of the check are pointed at a function of
	ours that makes it and notes the calls it turns away.

	What is kept: a log of the latest occurrences, and a list with one entry per message and entity (how many times, when
	first and last). The entity is told by its id, the id of the composite instance it sits in and its type (as OpenCAGE
	finds it), and the name of the composite it sits in. Both start afresh when another level loads.

	Every call is checked before it is changed (a call to the empty function, or to the game's check), on the GOG and Epic
	Games Store builds at their own addresses (see GameAddresses.h); one that is not as it should be is left alone, and
	the tab says how many were changed.

	Addresses are from the Steam retail AI.exe.
*/
namespace
{
	// ---- The calls ----
	// The empty function the checks and errors call in the shipped game
	constexpr uintptr_t kEmptyFunction = 0x00669C90;

	enum SiteKind : uint8_t { Check, Error };
	struct Site
	{
		uint32_t at;     // the call instruction
		SiteKind kind;
	};
	const Site kSites[] = {
		{ 0x0008CEE9, Check }, { 0x0008D11E, Check }, { 0x0008FFE9, Check }, { 0x00091032, Check },
		{ 0x0009254E, Check }, { 0x00096E61, Check }, { 0x00096E7D, Check }, { 0x00097040, Check },
		{ 0x00099541, Check }, { 0x00099B8D, Check }, { 0x00099EF8, Check }, { 0x0009A610, Check },
		{ 0x0009B099, Check }, { 0x0009B828, Check }, { 0x0009BD08, Check }, { 0x0009C638, Check },
		{ 0x0009CDC5, Check }, { 0x0009D338, Check }, { 0x0009D7E8, Check }, { 0x0009DD68, Check },
		{ 0x0009EBBE, Check }, { 0x0009F358, Check }, { 0x000A227A, Check }, { 0x000A2BB9, Check },
		{ 0x000A2FC9, Check }, { 0x000A58F5, Check }, { 0x000A62DC, Check }, { 0x000AAE8B, Check },
		{ 0x000AD774, Error }, { 0x000BF7FD, Error }, { 0x000CCD35, Check }, { 0x000CCFC7, Check },
		{ 0x000CD0EE, Check }, { 0x000CDB5A, Check }, { 0x000CDB86, Check }, { 0x000D6CA4, Check },
		{ 0x000D6CC7, Check }, { 0x000D7B8D, Check }, { 0x000D8751, Check }, { 0x000D8778, Check },
		{ 0x000D8D11, Check }, { 0x000D90C3, Check }, { 0x000D99B8, Check }, { 0x000DD473, Check },
		{ 0x000DD79D, Check }, { 0x000E026F, Error }, { 0x000E33BD, Check }, { 0x000E4661, Error },
		{ 0x000E4798, Error }, { 0x000E4E9B, Error }, { 0x000E508C, Check }, { 0x000E62D3, Check },
		{ 0x000E6B96, Check }, { 0x000E756C, Check }, { 0x000E78E1, Check }, { 0x000E8237, Check },
		{ 0x0010C598, Check }, { 0x0010D633, Check }, { 0x0010D67B, Check }, { 0x0010D6F5, Check },
		{ 0x0010EC63, Check }, { 0x0010ED58, Check }, { 0x0010F7F2, Check }, { 0x0010F82E, Check },
		{ 0x0010F86C, Check }, { 0x00111208, Check }, { 0x00111A5E, Check }, { 0x00111C9E, Check },
		{ 0x00111EED, Check }, { 0x00113DFA, Check }, { 0x0011429A, Check }, { 0x00114477, Check },
		{ 0x00114B2A, Check }, { 0x00115861, Check }, { 0x00117C3A, Check }, { 0x001184BF, Check },
		{ 0x0011B9B9, Check }, { 0x0011BCC9, Check }, { 0x0011BFE9, Check }, { 0x0011C30F, Check },
		{ 0x0011C769, Check }, { 0x0011CA92, Check }, { 0x0011D104, Check }, { 0x0011D489, Check },
		{ 0x0011D739, Check }, { 0x0011DAD9, Check }, { 0x0011DF8A, Check }, { 0x0011E22F, Check },
		{ 0x0011E692, Check }, { 0x0011E9C2, Check }, { 0x0011ED94, Check }, { 0x0011F132, Check },
		{ 0x0011F462, Check }, { 0x0011F789, Check }, { 0x0011FB22, Check }, { 0x0011FF07, Check },
		{ 0x0012027B, Check }, { 0x001224F2, Check }, { 0x0012360B, Check }, { 0x001246D4, Check },
		{ 0x00124AE7, Check }, { 0x00126B78, Check }, { 0x00126C12, Check }, { 0x0012A94D, Check },
		{ 0x0012AF4D, Check }, { 0x0012B497, Check }, { 0x0012B938, Check }, { 0x0012B95F, Check },
		{ 0x0012B97E, Check }, { 0x0012C1A9, Error }, { 0x0012C710, Check }, { 0x0012CD0E, Check },
		{ 0x0012D877, Error }, { 0x0012D896, Error }, { 0x0012DB1B, Error }, { 0x0012DB48, Error },
		{ 0x001303AB, Check }, { 0x00130BC9, Check }, { 0x00130F14, Check }, { 0x00130FEF, Check },
		{ 0x00133017, Check }, { 0x00148898, Check }, { 0x00149E33, Check }, { 0x00149EFB, Check },
		{ 0x00149F57, Check }, { 0x00160CE4, Check }, { 0x00161169, Check }, { 0x0016D55B, Error },
		{ 0x0016DAE2, Check }, { 0x00170330, Check }, { 0x001706A2, Check }, { 0x001709F2, Check },
		{ 0x00171BFF, Check }, { 0x001754FD, Check }, { 0x00175725, Check }, { 0x00175938, Check },
		{ 0x00175B05, Check }, { 0x00175D99, Error }, { 0x00175E08, Error }, { 0x001760C2, Check },
		{ 0x001761FD, Check }, { 0x00179568, Error }, { 0x0017969C, Error }, { 0x00179E00, Error },
		{ 0x00179F21, Error }, { 0x0017A031, Error }, { 0x0017A4B5, Error }, { 0x0017A57B, Check },
		{ 0x0017A593, Check }, { 0x0017A5BB, Check }, { 0x0017A691, Check }, { 0x0017A6C0, Check },
		{ 0x0017A8C5, Check }, { 0x0017AC4B, Check }, { 0x0017AC63, Check }, { 0x0017AC8B, Check },
		{ 0x0017AD70, Check }, { 0x0017AD9F, Check }, { 0x0017B159, Check }, { 0x0017B21E, Check },
		{ 0x0017B41C, Check }, { 0x0017B434, Check }, { 0x0017B45C, Check }, { 0x0017B539, Check },
		{ 0x0017B568, Check }, { 0x001D9C6D, Check }, { 0x002484D9, Check }, { 0x0024858E, Check },
		{ 0x003F8E93, Error }, { 0x003F8EB9, Error }, { 0x003F8FAD, Error }, { 0x00400EAD, Error },
		{ 0x004014A4, Error }
	};
	constexpr size_t kSiteCount = sizeof(kSites) / sizeof(kSites[0]);
	static_assert(kSiteCount == 173, "every call");

	// The game's check that a waiting call may still run (the entity that started its chain is still set up), on the call's
	// record; and where it is made: as the call comes due, and just before it runs
	constexpr uintptr_t kStillValid = 0x005264F0;
	constexpr uintptr_t kStillValidAsDue = 0x0052CC5F;
	constexpr uintptr_t kStillValidBeforeRun = 0x00526A8C;
	typedef bool(__thiscall* t_still_valid)(const void* info);
	t_still_valid g_stillValid = nullptr;

	// A call's record (a pointer to it is what the game passes): the method, the delay and the entity it is called on, then
	// the record of the call that led to it, and when it is due (script clock)
	constexpr uint32_t kInfoMethod = 0x00;
	constexpr uint32_t kInfoDelay = 0x04;
	constexpr uint32_t kInfoTarget = 0x08;
	constexpr uint32_t kInfoCause = 0x0C;
	constexpr uint32_t kInfoDue = 0x18;
	constexpr int kMaxChain = 256;

	constexpr uint32_t kStateInitialised = 1u << 28;
	constexpr uint32_t kManagerRootGuid = 0x0C;

	// ---- What is kept ----
	constexpr size_t kMessageSize = 320;
	constexpr size_t kLogSize = 256;        // the latest occurrences
	constexpr size_t kMaxEntries = 1024;    // one per message and entity
	constexpr size_t kMaxDroppedEntries = 512;
	constexpr int kMaxLogLines = 200;       // first occurrences written to the RuntimeUtils log, per level
	constexpr ULONGLONG kRepeatMs = 250;     // the same message about the same entity sooner than this after the last: only counted

	struct Occurrence
	{
		ULONGLONG tick = 0;
		double scriptTime = 0.0;
		const char* format = nullptr;      // tells messages apart (the game's own text, which stays put)
		SiteKind kind = Check;
		int site = -1;
		char message[kMessageSize] = {};
		SCRIPT_DEBUG::EntityId entity;
	};

	struct Entry
	{
		Occurrence latest;
		uint32_t count = 0;
		ULONGLONG first = 0;
	};

	// Why a waiting call was dropped
	enum DropReason : uint8_t { OriginGone, OriginShutDown };
	struct Dropped
	{
		uint32_t count = 0;
		ULONGLONG first = 0, last = 0;
		double scriptTime = 0.0;
		uint32_t method = 0;
		float delay = 0.0f;
		DropReason reason = OriginGone;
		bool beforeRun = false;            // dropped just before it would have run (else as it came due)
		int chain = 0;                     // calls in its chain, it included
		SCRIPT_DEBUG::EntityId target;
		SCRIPT_DEBUG::EntityId origin;
	};

	SRWLOCK g_lock = SRWLOCK_INIT;
	Occurrence g_log[kLogSize];
	size_t g_logNext = 0;
	size_t g_logCount = 0;
	std::vector<Entry> g_entries;          // reserved once; never grows past kMaxEntries
	std::vector<Dropped> g_dropped;
	std::unordered_map<uint64_t, size_t> g_repeats; // the message and the entity object to its entry, for repeats
	uint64_t g_total = 0;
	uint64_t g_overflow = 0;               // occurrences with no room left for their own entry
	uint64_t g_droppedTotal = 0;
	uint64_t g_droppedOverflow = 0;
	uint32_t g_root = 0;                   // the level what is kept belongs to
	int g_logLines = 0;
	std::atomic<uint32_t> g_generation = 0; // bumped by every change, so the tab copies only when something changed
	uint32_t g_siteHits[kSiteCount] = {};

	// The newest error, for the overlay
	std::atomic<ULONGLONG> g_toastTick = 0;

	// What OpenCAGE's Error Log reads (Live Link's ERRORS_GET): every occurrence noted (not the repeats only counted), each
	// numbered, the latest kRelaySize of them. Kept across levels: OpenCAGE starts its log afresh itself, when it launches the
	// game. Under g_lock; made when the hooks attach.
	constexpr size_t kRelaySize = 2048;
	constexpr uint32_t kRelayBatch = 512;   // the most one answer carries
	struct Relayed
	{
		uint32_t sequence = 0;
		uint32_t root = 0;                  // the level it happened in (the id of its root composite)
		SiteKind kind = Check;
		float scriptTime = 0.0f;
		SCRIPT_DEBUG::EntityId entity;
		char message[kMessageSize] = {};
	};
	std::vector<Relayed> g_relay;
	size_t g_relayNext = 0;
	uint32_t g_relaySequence = 0;          // the last number given out

	// ---- Switches ----
	std::atomic<bool> g_record = true;
	std::atomic<bool> g_recordDropped = true;
	std::atomic<bool> g_toasts = false;

	// ---- The patched calls ----
	SCRIPT_DEBUG::PatchedCall g_patched[kSiteCount];
	SCRIPT_DEBUG::PatchedCall g_patchedAsDue, g_patchedBeforeRun;
	int g_patchedChecks = 0, g_patchedErrors = 0, g_skipped = 0;
	bool g_validPatched = false;
	bool g_attached = false;
	std::string g_skipReason;
	std::vector<uintptr_t> g_siteAddresses; // the patched calls' return addresses, sorted, for telling which call it was
	std::vector<int> g_siteOrder;

	int SiteOf(uintptr_t returnAddress)
	{
		auto it = std::lower_bound(g_siteAddresses.begin(), g_siteAddresses.end(), returnAddress);
		if (it == g_siteAddresses.end() || *it != returnAddress)
			return -1;
		return g_siteOrder[it - g_siteAddresses.begin()];
	}

	// Copies the game's message text (guarded), then fills in its values (guarded too: a value the message names that
	// cannot be read leaves the message as it is written)
	bool CopyText(const char* from, char* to, size_t size)
	{
		__try
		{
			size_t i = 0;
			for (; from && i + 1 < size && from[i]; i++)
				to[i] = from[i];
			to[i] = '\0';
			return from != nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			to[0] = '\0';
			return false;
		}
	}

	bool FormatValues(char* out, size_t size, const char* format, va_list args)
	{
		__try
		{
			vsnprintf(out, size, format, args);
			out[size - 1] = '\0';
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	uint32_t ReadRoot()
	{
		__try
		{
			const uint8_t* manager = SCRIPT_DEBUG::EntityManager();
			return manager ? *reinterpret_cast<const uint32_t*>(manager + kManagerRootGuid) : 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	// A new level: what was kept belongs to the last one (under g_lock)
	void ForgetIfNewLevel()
	{
		const uint32_t root = ReadRoot();
		if (root == 0 || root == g_root)
			return;
		g_root = root;
		g_entries.clear();
		g_repeats.clear();
		g_dropped.clear();
		g_logNext = g_logCount = 0;
		g_total = g_overflow = g_droppedTotal = g_droppedOverflow = 0;
		g_logLines = 0;
	}

	// The entity object a pointer to a counted record leads to (guarded), to tell repeats apart cheaply
	const void* ObjectAt(const void* entityRef)
	{
		__try
		{
			const uint8_t* record = entityRef ? *static_cast<const uint8_t* const*>(entityRef) : nullptr;
			return record ? *reinterpret_cast<const void* const*>(record + 0x0C) : nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	// fromGame: the game's own message, whose text (and the string values put into it) is Windows-1252; ours is UTF-8
	void Note(uintptr_t returnAddress, SiteKind kind, const void* entityRef, bool fromGame, const char* format, va_list args)
	{
		const ULONGLONG now = GetTickCount64();
		const int site = SiteOf(returnAddress);

		// The same message about the same entity again within kRepeatMs (a check that fails every frame): only counted
		const uint64_t key = (static_cast<uint64_t>(reinterpret_cast<uintptr_t>(format)) << 32) | reinterpret_cast<uintptr_t>(ObjectAt(entityRef));
		AcquireSRWLockExclusive(&g_lock);
		ForgetIfNewLevel();
		auto repeat = g_repeats.find(key);
		if (repeat != g_repeats.end() && repeat->second < g_entries.size() && now - g_entries[repeat->second].latest.tick < kRepeatMs)
		{
			g_entries[repeat->second].count++;
			g_total++;
			if (site >= 0)
				g_siteHits[site]++;
			g_generation++;
			ReleaseSRWLockExclusive(&g_lock);
			return;
		}
		ReleaseSRWLockExclusive(&g_lock);

		Occurrence occurrence;
		occurrence.tick = now;
		occurrence.scriptTime = SCRIPT_DEBUG::ScriptTime();
		occurrence.format = format;
		occurrence.kind = kind;
		occurrence.site = site;

		char text[kMessageSize];
		if (!CopyText(format, text, sizeof(text)))
			strcpy_s(text, "(the message could not be read)");
		if (!strchr(text, '%') || !FormatValues(occurrence.message, sizeof(occurrence.message), text, args))
			strcpy_s(occurrence.message, text);
		if (fromGame)
		{
			strcpy_s(text, occurrence.message);
			DevTools::GameTextToUtf8(text, occurrence.message, sizeof(occurrence.message));
		}
		SCRIPT_DEBUG::ReadEntityAt(entityRef, occurrence.entity);

		bool first = false;
		AcquireSRWLockExclusive(&g_lock);
		ForgetIfNewLevel();
		g_total++;
		if (site >= 0)
			g_siteHits[site]++;
		g_log[g_logNext] = occurrence;
		g_logNext = (g_logNext + 1) % kLogSize;
		g_logCount = (std::min)(g_logCount + 1, kLogSize);
		if (!g_relay.empty())
		{
			Relayed& relayed = g_relay[g_relayNext];
			g_relayNext = (g_relayNext + 1) % g_relay.size();
			relayed.sequence = ++g_relaySequence;
			relayed.root = g_root;
			relayed.kind = kind;
			relayed.scriptTime = static_cast<float>(occurrence.scriptTime);
			relayed.entity = occurrence.entity;
			strcpy_s(relayed.message, occurrence.message);
		}
		Entry* entry = nullptr;
		for (Entry& existing : g_entries)
			if (existing.latest.format == format && existing.latest.entity.entity == occurrence.entity.entity && existing.latest.entity.instance == occurrence.entity.instance)
			{
				entry = &existing;
				break;
			}
		if (!entry && g_entries.size() < kMaxEntries)
		{
			g_entries.emplace_back();
			entry = &g_entries.back();
			entry->first = occurrence.tick;
			first = g_logLines < kMaxLogLines;
			g_logLines += first ? 1 : 0;
		}
		if (entry)
		{
			entry->latest = occurrence;
			entry->count++;
			if (g_repeats.size() < kMaxEntries * 4)
				g_repeats[key] = static_cast<size_t>(entry - g_entries.data());
		}
		else
		{
			g_overflow++;
		}
		g_generation++;
		ReleaseSRWLockExclusive(&g_lock);

		g_toastTick = occurrence.tick;
		if (first)
			DevTools::Log("Script %s: %s - %s", kind == Check ? "check failed" : "error", occurrence.message, SCRIPT_DEBUG::Describe(occurrence.entity).c_str());
	}

	// The game's checks and errors, pointed here (the same arguments, the caller clears them)
	void __cdecl OnCheck(const void* entityRef, int passed, const char* format, ...)
	{
		// The game passes whether the check passed as a byte
		if ((passed & 0xFF) != 0 || !g_record)
			return;
		const uintptr_t returnAddress = reinterpret_cast<uintptr_t>(_ReturnAddress());
		va_list args;
		va_start(args, format);
		Note(returnAddress, Check, entityRef, true, format, args);
		va_end(args);
	}

	void __cdecl OnError(const void* entityRef, const char* format, ...)
	{
		if (!g_record)
			return;
		const uintptr_t returnAddress = reinterpret_cast<uintptr_t>(_ReturnAddress());
		va_list args;
		va_start(args, format);
		Note(returnAddress, Error, entityRef, true, format, args);
		va_end(args);
	}

	// Our own notes about script entities (the debug script entities this debug menu runs), made the same way
	void NoteOwn(const void* entityRef, const char* format, ...)
	{
		const uintptr_t returnAddress = reinterpret_cast<uintptr_t>(_ReturnAddress());
		va_list args;
		va_start(args, format);
		Note(returnAddress, Error, entityRef, false, format, args);
		va_end(args);
	}

	// ---- Dropped calls ----
	struct DropRead
	{
		uint32_t method = 0;
		float delay = 0.0f;
		const void* target = nullptr;
		const void* origin = nullptr;  // the counted record of the entity that started the chain
		int chain = 0;
	};

	bool ReadDrop(const uint8_t* info, DropRead& out)
	{
		__try
		{
			out.method = *reinterpret_cast<const uint32_t*>(info + kInfoMethod);
			out.delay = *reinterpret_cast<const float*>(info + kInfoDelay);
			out.target = *reinterpret_cast<const void* const*>(info + kInfoTarget);
			// The chain back to its first call: that call's entity started it
			const uint8_t* at = info;
			int chain = 1;
			while (chain < kMaxChain)
			{
				const uint8_t* cause = *reinterpret_cast<const uint8_t* const*>(at + kInfoCause);
				if (!cause)
					break;
				at = cause;
				chain++;
			}
			out.origin = *reinterpret_cast<const void* const*>(at + kInfoTarget);
			out.chain = chain;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	void NoteDropped(const void* info, bool beforeRun)
	{
		DropRead read;
		if (!ReadDrop(static_cast<const uint8_t*>(info), read))
			return;
		Dropped drop;
		drop.method = read.method;
		drop.delay = read.delay;
		drop.chain = read.chain;
		drop.beforeRun = beforeRun;
		drop.scriptTime = SCRIPT_DEBUG::ScriptTime();
		drop.last = drop.first = GetTickCount64();
		SCRIPT_DEBUG::ReadEntity(read.target, drop.target);
		const bool originRead = read.origin && SCRIPT_DEBUG::ReadEntity(read.origin, drop.origin);
		drop.reason = originRead ? OriginShutDown : OriginGone;
		if (originRead && (drop.origin.state & kStateInitialised))
			return; // not dropped for its origin after all (the game's check said no for a reason of its own)

		AcquireSRWLockExclusive(&g_lock);
		ForgetIfNewLevel();
		g_droppedTotal++;
		Dropped* entry = nullptr;
		for (Dropped& existing : g_dropped)
			if (existing.method == drop.method && existing.target.entity == drop.target.entity && existing.target.instance == drop.target.instance &&
				existing.reason == drop.reason && existing.beforeRun == drop.beforeRun)
			{
				entry = &existing;
				break;
			}
		if (entry)
		{
			const uint32_t count = entry->count + 1;
			const ULONGLONG first = entry->first;
			*entry = drop;
			entry->count = count;
			entry->first = first;
		}
		else if (g_dropped.size() < kMaxDroppedEntries)
		{
			drop.count = 1;
			g_dropped.push_back(drop);
		}
		else
		{
			g_droppedOverflow++;
		}
		g_generation++;
		ReleaseSRWLockExclusive(&g_lock);
	}

	// The game's check, made by the game's calls of it (on the call's record), and noted when it says no
	bool __fastcall OnStillValidAsDue(const void* info, void* /*_EDX*/)
	{
		const bool valid = g_stillValid(info);
		if (!valid && g_recordDropped)
			NoteDropped(info, false);
		return valid;
	}

	bool __fastcall OnStillValidBeforeRun(const void* info, void* /*_EDX*/)
	{
		const bool valid = g_stillValid(info);
		if (valid)
		{
			// The call held at a breakpoint is checked again as it is let go, and it was already counted when it was held
			if (!SCRIPT_DEBUG::RunningHeldCall())
				SCRIPT_DEBUG::CountTrigger(info);
		}
		else if (g_recordDropped)
			NoteDropped(info, true);
		return valid;
	}

	// ---- Hooks ----
	void Attach(bool attach)
	{
		if (attach == g_attached)
			return;
		g_attached = attach;
		if (attach)
		{
			{
				AcquireSRWLockExclusive(&g_lock);
				g_entries.reserve(kMaxEntries);
				g_dropped.reserve(kMaxDroppedEntries);
				if (g_relay.empty())
					g_relay.resize(kRelaySize);
				ReleaseSRWLockExclusive(&g_lock);
			}
			// Where each call returns to, sorted (so the handlers can tell which call it was), made before any call is changed
			std::vector<std::pair<uintptr_t, int>> order;
			for (size_t i = 0; i < kSiteCount; i++)
				if (const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kSites[i].at))
					order.emplace_back(at + 5, static_cast<int>(i));
			std::sort(order.begin(), order.end());
			for (const auto& item : order)
			{
				g_siteAddresses.push_back(item.first);
				g_siteOrder.push_back(item.second);
			}

			int notInBuild = 0, notAsExpected = 0;
			for (size_t i = 0; i < kSiteCount; i++)
			{
				const char* why = nullptr;
				const void* replacement = kSites[i].kind == Check ? reinterpret_cast<const void*>(&OnCheck) : reinterpret_cast<const void*>(&OnError);
				if (SCRIPT_DEBUG::PatchCall(kSites[i].at, kEmptyFunction, replacement, g_patched[i], why))
				{
					(kSites[i].kind == Check ? g_patchedChecks : g_patchedErrors)++;
				}
				else
				{
					g_skipped++;
					(DEVTOOLS_RELATIVE_ADDRESS(kSites[i].at) ? notAsExpected : notInBuild)++;
					if (g_skipReason.empty())
						g_skipReason = why ? why : "";
				}
			}

			g_stillValid = reinterpret_cast<t_still_valid>(DEVTOOLS_RELATIVE_ADDRESS(kStillValid));
			const char* whyDue = nullptr;
			const char* whyRun = nullptr;
			const bool due = g_stillValid && SCRIPT_DEBUG::PatchCall(kStillValidAsDue, kStillValid, reinterpret_cast<const void*>(&OnStillValidAsDue), g_patchedAsDue, whyDue);
			const bool run = g_stillValid && SCRIPT_DEBUG::PatchCall(kStillValidBeforeRun, kStillValid, reinterpret_cast<const void*>(&OnStillValidBeforeRun), g_patchedBeforeRun, whyRun);
			g_validPatched = due && run;
			if (!g_validPatched)
			{
				// Both or neither, so the two lists agree
				SCRIPT_DEBUG::RestoreCall(g_patchedAsDue);
				SCRIPT_DEBUG::RestoreCall(g_patchedBeforeRun);
			}
			DevTools::Log("Debug menu: script errors - %d checks and %d errors of the game's %d followed (%d not in this build, %d not as expected%s%s); dropped calls %s.",
				g_patchedChecks, g_patchedErrors, static_cast<int>(kSiteCount), notInBuild, notAsExpected, g_skipReason.empty() ? "" : ": ", g_skipReason.c_str(),
				g_validPatched ? "followed" : "not followed (the game's check is not as expected)");
		}
		else
		{
			int failed = 0;
			for (size_t i = 0; i < kSiteCount; i++)
				failed += SCRIPT_DEBUG::RestoreCall(g_patched[i]) ? 0 : 1;
			failed += SCRIPT_DEBUG::RestoreCall(g_patchedAsDue) ? 0 : 1;
			failed += SCRIPT_DEBUG::RestoreCall(g_patchedBeforeRun) ? 0 : 1;
			if (failed)
				DevTools::Log("Debug menu: script errors - %d changed calls could not be put back (changed again since).", failed);
		}
	}

	// ---- Reading what is kept (any thread) ----
	struct Snapshot
	{
		uint32_t generation = ~0u;
		std::vector<Entry> entries;
		std::vector<Occurrence> log;       // newest first
		std::vector<Dropped> dropped;
		uint64_t total = 0, overflow = 0, droppedTotal = 0, droppedOverflow = 0;
	};

	void TakeSnapshot(Snapshot& out)
	{
		const uint32_t generation = g_generation;
		if (generation == out.generation)
			return;
		AcquireSRWLockShared(&g_lock);
		out.generation = g_generation;
		out.entries = g_entries;
		out.dropped = g_dropped;
		out.log.clear();
		for (size_t i = 0; i < g_logCount; i++)
			out.log.push_back(g_log[(g_logNext + kLogSize - 1 - i) % kLogSize]);
		out.total = g_total;
		out.overflow = g_overflow;
		out.droppedTotal = g_droppedTotal;
		out.droppedOverflow = g_droppedOverflow;
		ReleaseSRWLockShared(&g_lock);
	}

	void Clear()
	{
		AcquireSRWLockExclusive(&g_lock);
		g_entries.clear();
		g_repeats.clear();
		g_dropped.clear();
		g_logNext = g_logCount = 0;
		g_total = g_overflow = g_droppedTotal = g_droppedOverflow = 0;
		g_generation++;
		ReleaseSRWLockExclusive(&g_lock);
		g_toastTick = 0;
	}

	std::string Ago(ULONGLONG tick, ULONGLONG now)
	{
		char text[32];
		const double seconds = static_cast<double>(now - tick) / 1000.0;
		if (seconds < 60.0)
			snprintf(text, sizeof(text), "%.1f s ago", seconds);
		else if (seconds < 3600.0)
			snprintf(text, sizeof(text), "%.0f min ago", seconds / 60.0);
		else
			snprintf(text, sizeof(text), "%.1f h ago", seconds / 3600.0);
		return text;
	}

	std::string EntryText(const Entry& entry, ULONGLONG now)
	{
		return std::to_string(entry.count) + "x " + (entry.latest.kind == Check ? "check" : "error") + " | " + entry.latest.message + " | " +
			SCRIPT_DEBUG::Fields(entry.latest.entity) + " | last " + Ago(entry.latest.tick, now);
	}

	std::string DroppedText(const Dropped& drop, ULONGLONG now)
	{
		return std::to_string(drop.count) + "x method=" + SCRIPT_DEBUG::Hex(drop.method) + " method_name=" + SCRIPT_DEBUG::MethodText(drop.method) + " | " + SCRIPT_DEBUG::Fields(drop.target) + " | " +
			(drop.reason == OriginGone ? "chain origin gone" : "chain origin shut down") + " (" + SCRIPT_DEBUG::Fields(drop.origin) + ") | " +
			(drop.beforeRun ? "about to run" : "as it came due") + " | chain " + std::to_string(drop.chain) + " | last " + Ago(drop.last, now);
	}

	bool EntryMatches(const Entry& entry, const char* filter)
	{
		if (!filter || !filter[0])
			return true;
		return SCRIPT_DEBUG::Matches(entry.latest.message, filter) || SCRIPT_DEBUG::Matches(SCRIPT_DEBUG::Describe(entry.latest.entity), filter) ||
			SCRIPT_DEBUG::Matches(SCRIPT_DEBUG::Fields(entry.latest.entity), filter);
	}

	bool DroppedMatches(const Dropped& drop, const char* filter)
	{
		if (!filter || !filter[0])
			return true;
		return SCRIPT_DEBUG::Matches(DroppedText(drop, 0), filter) || SCRIPT_DEBUG::Matches(SCRIPT_DEBUG::Describe(drop.target), filter) ||
			SCRIPT_DEBUG::Matches(SCRIPT_DEBUG::Describe(drop.origin), filter);
	}

	std::string StatusText()
	{
		if (!g_attached)
			return "The debug menu's hooks are not attached.";
		int sitesHit = 0;
		AcquireSRWLockShared(&g_lock);
		for (uint32_t hits : g_siteHits)
			sitesHit += hits ? 1 : 0;
		ReleaseSRWLockShared(&g_lock);
		char text[256];
		snprintf(text, sizeof(text), "checks=%d errors=%d of=%d skipped=%d sites_hit=%d dropped_calls=%s build=%s", g_patchedChecks, g_patchedErrors,
			static_cast<int>(kSiteCount), g_skipped, sitesHit, g_validPatched ? "followed" : "not_followed", DevTools::BuildName());
		return text;
	}

	// ---- Commands and switches (registered at load, so OpenCAGE can use them before the window is opened) ----
	bool Register()
	{
		DEBUG_MENU::AddSwitch("scripts.errors", &g_record);
		DEBUG_MENU::AddSwitch("scripts.dropped_calls", &g_recordDropped);
		DEBUG_MENU::AddSwitch("scripts.error_toasts", &g_toasts);
		DEBUG_MENU::AddCommand("scripts.list_errors", "the script errors and failed checks, one line per message and entity (newest first); argument: a filter",
			[](const std::string& argument) -> DEBUG_MENU::CommandResult
			{
				Snapshot snapshot;
				TakeSnapshot(snapshot);
				const ULONGLONG now = GetTickCount64();
				std::vector<const Entry*> entries;
				for (const Entry& entry : snapshot.entries)
					if (EntryMatches(entry, argument.c_str()))
						entries.push_back(&entry);
				std::sort(entries.begin(), entries.end(), [](const Entry* a, const Entry* b) { return a->latest.tick > b->latest.tick; });
				DEBUG_MENU::CommandResult result;
				result.message = "total=" + std::to_string(snapshot.total) + " entries=" + std::to_string(entries.size()) + (snapshot.overflow ? " overflow=" + std::to_string(snapshot.overflow) : "") + "\n";
				for (const Entry* entry : entries)
					result.message += EntryText(*entry, now) + "\n";
				return result;
			});
		DEBUG_MENU::AddCommand("scripts.list_error_log", "the latest script error occurrences, newest first; argument: how many (default 50)",
			[](const std::string& argument) -> DEBUG_MENU::CommandResult
			{
				Snapshot snapshot;
				TakeSnapshot(snapshot);
				const ULONGLONG now = GetTickCount64();
				const size_t wanted = argument.empty() ? 50 : static_cast<size_t>((std::max)(1, atoi(argument.c_str())));
				DEBUG_MENU::CommandResult result;
				for (size_t i = 0; i < snapshot.log.size() && i < wanted; i++)
				{
					const Occurrence& occurrence = snapshot.log[i];
					char time[32];
					snprintf(time, sizeof(time), "%.2f", occurrence.scriptTime);
					result.message += std::string("script_time=") + time + " | " + (occurrence.kind == Check ? "check" : "error") + " | " + occurrence.message + " | " +
						SCRIPT_DEBUG::Fields(occurrence.entity) + " | " + Ago(occurrence.tick, now) + "\n";
				}
				if (result.message.empty())
					result.message = "(none)";
				return result;
			});
		DEBUG_MENU::AddCommand("scripts.list_error_messages", "each script error message with how often it happened, across all entities",
			[](const std::string&) -> DEBUG_MENU::CommandResult
			{
				Snapshot snapshot;
				TakeSnapshot(snapshot);
				std::vector<std::pair<const Entry*, uint32_t>> messages;
				for (const Entry& entry : snapshot.entries)
				{
					auto it = std::find_if(messages.begin(), messages.end(), [&](const std::pair<const Entry*, uint32_t>& m) { return m.first->latest.format == entry.latest.format; });
					if (it == messages.end())
						messages.emplace_back(&entry, entry.count);
					else
						it->second += entry.count;
				}
				std::sort(messages.begin(), messages.end(), [](const std::pair<const Entry*, uint32_t>& a, const std::pair<const Entry*, uint32_t>& b) { return a.second > b.second; });
				DEBUG_MENU::CommandResult result;
				for (const auto& m : messages)
				{
					char format[kMessageSize];
					if (!CopyText(m.first->latest.format, format, sizeof(format)))
						strcpy_s(format, m.first->latest.message);
					result.message += std::to_string(m.second) + "x | " + format + "\n";
				}
				if (result.message.empty())
					result.message = "(none)";
				return result;
			});
		DEBUG_MENU::AddCommand("scripts.clear_errors", "forgets the script errors and dropped calls kept so far",
			[](const std::string&) -> DEBUG_MENU::CommandResult
			{
				Clear();
				return { true, "cleared" };
			});
		DEBUG_MENU::AddCommand("scripts.error_status", "how many of the game's script checks and errors are followed in this build",
			[](const std::string&) -> DEBUG_MENU::CommandResult
			{
				return { true, StatusText() };
			});
		DEBUG_MENU::AddCommand("scripts.list_dropped", "calls the game dropped because the entity that started their chain was shut down; argument: a filter",
			[](const std::string& argument) -> DEBUG_MENU::CommandResult
			{
				Snapshot snapshot;
				TakeSnapshot(snapshot);
				const ULONGLONG now = GetTickCount64();
				std::vector<const Dropped*> drops;
				for (const Dropped& drop : snapshot.dropped)
					if (DroppedMatches(drop, argument.c_str()))
						drops.push_back(&drop);
				std::sort(drops.begin(), drops.end(), [](const Dropped* a, const Dropped* b) { return a->last > b->last; });
				DEBUG_MENU::CommandResult result;
				result.message = "total=" + std::to_string(snapshot.droppedTotal) + " entries=" + std::to_string(drops.size()) + (g_validPatched ? "" : " (not followed in this build)") + "\n";
				for (const Dropped* drop : drops)
					result.message += DroppedText(*drop, now) + "\n";
				return result;
			});
		DEBUG_MENU::AddCommand("scripts.test_error", "notes a made-up script error on the level's root instance (to test the list and the on-screen note, which shows only while scripts.error_toasts is on; it is marked as a test); argument: its text",
			[](const std::string& argument) -> DEBUG_MENU::CommandResult
			{
				if (!SCRIPT_DEBUG::LevelRunning())
					return { false, "no level running" };
				if (!g_record)
					return { false, "not noted: keeping script errors is off (scripts.errors)" };
				const std::string text = argument.empty() ? std::string("a test") : argument;
				return DEBUG_MENU::AnswerOnEntityThread([text]() -> DEBUG_MENU::CommandResult
				{
					uint8_t* manager = SCRIPT_DEBUG::EntityManager();
					if (!manager)
						return { false, "no level running" };
					// The root instance's counted record (the entity manager keeps it at +0x44), as the game's own calls pass one
					const void* rootRecord = manager + 0x44;
					const uint32_t before = g_generation;
					OnError(rootRecord, "(test from the debug menu) %s", text.c_str());
					if (g_generation == before)
						return { false, "not noted: keeping script errors is off (scripts.errors)" };
					// A test made again within a quarter of a second is only counted, as a repeat is: shown on screen all the same
					g_toastTick = GetTickCount64();
					// The on-screen note is drawn only while scripts.error_toasts is on (off unless switched on)
					if (!g_toasts)
						return { true, "noted; not shown on screen: the on-screen note of new script errors is off (scripts.error_toasts = 0; tick \"Show new ones on screen\" in Scripts > Errors)" };
					return { true, "noted; shown at the top of the screen for 6 s (scripts.error_toasts is on)" };
				});
			});
		return true;
	}

	const bool g_registered = Register();
	// The hooks are attached by the DLL itself (SCRIPT_ERRORS::AttachHooks): with the debug menu, or for the Error Log alone

	// ---- The tab ----
	Snapshot g_view;
	char g_filter[96] = "";
	int g_selected = -1;           // the selected entry, by its index in g_view.entries
	bool g_showLog = false;
	char g_droppedFilter[96] = "";

	void HelpMarker(const char* text)
	{
		ImGui::SameLine();
		ImGui::TextDisabled("(?)");
		if (ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
		{
			ImGui::BeginTooltip();
			ImGui::PushTextWrapPos(ImGui::GetFontSize() * 32.0f);
			ImGui::TextUnformatted(text);
			ImGui::PopTextWrapPos();
			ImGui::EndTooltip();
		}
	}

	void Checkbox(const char* label, std::atomic<bool>& value)
	{
		bool ticked = value;
		if (ImGui::Checkbox(label, &ticked))
			value = ticked;
	}

	// The message column's width (set before its table begins): messages are sentences, so they wrap in it, while the
	// rest of a row keeps to one line and the table scrolls sideways when that is wider than the window
	float MessageWidth()
	{
		return (std::max)(ImGui::GetFontSize() * 16.0f, ImGui::GetContentRegionAvail().x * 0.35f);
	}

	void EntityTooltip(const SCRIPT_DEBUG::EntityId& id)
	{
		if (!ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
			return;
		ImGui::BeginTooltip();
		if (!id.valid)
		{
			ImGui::TextUnformatted("The entity could not be read.");
		}
		else
		{
			ImGui::Text("%s %s (0x%08X)", SCRIPT_DEBUG::TypeText(id).c_str(), SCRIPT_DEBUG::Guid(id.entity).c_str(), id.entity);
			ImGui::Text("Type id %s (0x%08X)", SCRIPT_DEBUG::Guid(id.type).c_str(), id.type);
			const std::string where = SCRIPT_DEBUG::WhereText(id);
			if (where.empty())
				ImGui::TextUnformatted("In no composite: it is the level root itself");
			else
				ImGui::Text("Composite %s (%s)", where.c_str(), SCRIPT_DEBUG::Guid(id.composite).c_str());
			ImGui::Text("Composite instance %s (0x%08X)", SCRIPT_DEBUG::Guid(id.instance).c_str(), id.instance);
			ImGui::TextDisabled("OpenCAGE finds the entity from its id and the instance's id.");
		}
		ImGui::EndTooltip();
	}

	void DrawEntries(ULONGLONG now)
	{
		std::vector<int> shown;
		for (int i = 0; i < static_cast<int>(g_view.entries.size()); i++)
			if (EntryMatches(g_view.entries[i], g_filter))
				shown.push_back(i);
		std::sort(shown.begin(), shown.end(), [](int a, int b) { return g_view.entries[a].latest.tick > g_view.entries[b].latest.tick; });

		ImGui::Text("%llu noted, %d shown (one line per message and entity)", static_cast<unsigned long long>(g_view.total), static_cast<int>(shown.size()));
		if (g_view.overflow)
		{
			ImGui::SameLine();
			ImGui::TextColored(ImVec4(1.0f, 0.6f, 0.3f, 1.0f), "- %llu more with no room left in the list", static_cast<unsigned long long>(g_view.overflow));
		}
		const float messageWidth = MessageWidth();
		if (!SCRIPT_DEBUG::BeginTable("errors", 4, 240.0f * DEBUG_MENU::Scale()))
			return;
		ImGui::TableSetupScrollFreeze(0, 1);
		ImGui::TableSetupColumn("Count");
		ImGui::TableSetupColumn("Message", ImGuiTableColumnFlags_WidthFixed, messageWidth);
		ImGui::TableSetupColumn("Entity");
		ImGui::TableSetupColumn("Last");
		ImGui::TableHeadersRow();
		for (int index : shown)
		{
			const Entry& entry = g_view.entries[index];
			ImGui::PushID(index);
			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			if (ImGui::Selectable(std::to_string(entry.count).c_str(), g_selected == index, ImGuiSelectableFlags_SpanAllColumns))
				g_selected = index;
			ImGui::TableNextColumn();
			ImGui::TextColored(entry.latest.kind == Check ? ImVec4(1.0f, 0.85f, 0.4f, 1.0f) : ImVec4(1.0f, 0.5f, 0.45f, 1.0f), "%s", entry.latest.kind == Check ? "Check" : "Error");
			ImGui::SameLine();
			ImGui::TextWrapped("%s", entry.latest.message);
			ImGui::TableNextColumn();
			if (entry.latest.entity.valid)
			{
				// Its type and id, then the composite it sits in on a line of its own (the root instance sits in none)
				const std::string where = SCRIPT_DEBUG::WhereText(entry.latest.entity);
				ImGui::BeginGroup();
				ImGui::Text("%s %s", SCRIPT_DEBUG::TypeText(entry.latest.entity).c_str(), SCRIPT_DEBUG::Guid(entry.latest.entity.entity).c_str());
				if (!where.empty())
					ImGui::Text("in %s", where.c_str());
				ImGui::EndGroup();
			}
			else
				ImGui::TextDisabled("(unreadable)");
			EntityTooltip(entry.latest.entity);
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(Ago(entry.latest.tick, now).c_str());
			ImGui::PopID();
		}
		ImGui::EndTable();
	}

	// How often each message came, whatever the entity (the game's own text, before its values are filled in)
	void DrawMessages()
	{
		if (!ImGui::CollapsingHeader("By message"))
			return;
		std::vector<std::pair<const Entry*, uint32_t>> messages;
		for (const Entry& entry : g_view.entries)
		{
			auto it = std::find_if(messages.begin(), messages.end(), [&](const std::pair<const Entry*, uint32_t>& m) { return m.first->latest.format == entry.latest.format; });
			if (it == messages.end())
				messages.emplace_back(&entry, entry.count);
			else
				it->second += entry.count;
		}
		std::sort(messages.begin(), messages.end(), [](const std::pair<const Entry*, uint32_t>& a, const std::pair<const Entry*, uint32_t>& b) { return a.second > b.second; });
		for (const auto& m : messages)
		{
			char format[kMessageSize];
			if (!CopyText(m.first->latest.format, format, sizeof(format)))
				strcpy_s(format, m.first->latest.message);
			if (!SCRIPT_DEBUG::Matches(format, g_filter))
				continue;
			ImGui::Text("%6u", m.second);
			ImGui::SameLine();
			ImGui::TextWrapped("%s", format);
		}
	}

	void DrawSelected(ULONGLONG now)
	{
		if (g_selected < 0 || g_selected >= static_cast<int>(g_view.entries.size()))
			return;
		const Entry& entry = g_view.entries[g_selected];
		ImGui::SeparatorText("Selected");
		ImGui::TextWrapped("%s", entry.latest.message);
		ImGui::TextWrapped("%s", SCRIPT_DEBUG::Describe(entry.latest.entity).c_str());
		ImGui::Text("%u times, first %s, last %s (script clock %.2f s)", entry.count, Ago(entry.first, now).c_str(), Ago(entry.latest.tick, now).c_str(), entry.latest.scriptTime);
		if (ImGui::Button("Copy ids"))
			ImGui::SetClipboardText(SCRIPT_DEBUG::Fields(entry.latest.entity).c_str());
		ImGui::SameLine();
		if (ImGui::Button("Copy line"))
			ImGui::SetClipboardText(EntryText(entry, now).c_str());
	}

	void DrawLog(ULONGLONG now)
	{
		const float messageWidth = MessageWidth();
		if (!SCRIPT_DEBUG::BeginTable("errorlog", 3, 200.0f * DEBUG_MENU::Scale()))
			return;
		ImGui::TableSetupScrollFreeze(0, 1);
		ImGui::TableSetupColumn("When");
		ImGui::TableSetupColumn("Message", ImGuiTableColumnFlags_WidthFixed, messageWidth);
		ImGui::TableSetupColumn("Entity");
		ImGui::TableHeadersRow();
		for (size_t i = 0; i < g_view.log.size(); i++)
		{
			const Occurrence& occurrence = g_view.log[i];
			if (!SCRIPT_DEBUG::Matches(occurrence.message, g_filter) && !SCRIPT_DEBUG::Matches(SCRIPT_DEBUG::Describe(occurrence.entity), g_filter))
				continue;
			ImGui::PushID(static_cast<int>(i));
			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			ImGui::Text("%.2f", occurrence.scriptTime);
			if (ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
				ImGui::SetTooltip("Script clock, seconds (%s)", Ago(occurrence.tick, now).c_str());
			ImGui::TableNextColumn();
			ImGui::TextWrapped("%s", occurrence.message);
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(occurrence.entity.valid ? (SCRIPT_DEBUG::TypeText(occurrence.entity) + " " + SCRIPT_DEBUG::Guid(occurrence.entity.entity)).c_str() : "(unreadable)");
			EntityTooltip(occurrence.entity);
			ImGui::PopID();
		}
		ImGui::EndTable();
	}

	void DrawDropped(ULONGLONG now)
	{
		ImGui::SeparatorText("Dropped calls");
		ImGui::TextWrapped("Methods called through links that the game threw away without running them: the entity that started their chain of calls had been shut down (its zone unloaded, or it was deleted) by the time they came due. Often meant to happen - but a delayed link that never fires shows here.");
		if (!g_validPatched)
		{
			ImGui::TextColored(ImVec4(1.0f, 0.6f, 0.3f, 1.0f), g_attached ? "Not available on this game release." : "The debug menu's hooks are not attached.");
			return;
		}
		Checkbox("Keep dropped calls##dropped", g_recordDropped);
		ImGui::SameLine();
		ImGui::SetNextItemWidth(220.0f * DEBUG_MENU::Scale());
		ImGui::InputTextWithHint("##droppedfilter", "Filter", g_droppedFilter, sizeof(g_droppedFilter));
		ImGui::SameLine();
		if (ImGui::Button("Copy##dropped"))
		{
			std::string text;
			for (const Dropped& drop : g_view.dropped)
				if (DroppedMatches(drop, g_droppedFilter))
					text += DroppedText(drop, now) + "\n";
			ImGui::SetClipboardText(text.c_str());
		}
		std::vector<const Dropped*> drops;
		for (const Dropped& drop : g_view.dropped)
			if (DroppedMatches(drop, g_droppedFilter))
				drops.push_back(&drop);
		std::sort(drops.begin(), drops.end(), [](const Dropped* a, const Dropped* b) { return a->last > b->last; });
		ImGui::Text("%llu dropped, %d shown", static_cast<unsigned long long>(g_view.droppedTotal), static_cast<int>(drops.size()));
		if (!SCRIPT_DEBUG::BeginTable("dropped", 5, 180.0f * DEBUG_MENU::Scale()))
			return;
		ImGui::TableSetupScrollFreeze(0, 1);
		ImGui::TableSetupColumn("Count");
		ImGui::TableSetupColumn("Method");
		ImGui::TableSetupColumn("Called on");
		ImGui::TableSetupColumn("Started by");
		ImGui::TableSetupColumn("Last");
		ImGui::TableHeadersRow();
		int row = 0;
		for (const Dropped* drop : drops)
		{
			ImGui::PushID(row++);
			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			ImGui::Text("%u", drop->count);
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(SCRIPT_DEBUG::MethodText(drop->method).c_str());
			if (ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
				ImGui::SetTooltip("The method's id (0x%08X); delay %.2f s; %d calls in its chain; dropped %s.", drop->method, drop->delay, drop->chain,
					drop->beforeRun ? "just before it would have run" : "as it came due");
			ImGui::TableNextColumn();
			if (drop->target.valid)
				ImGui::TextUnformatted(SCRIPT_DEBUG::ShortText(drop->target).c_str());
			else
				ImGui::TextUnformatted("?");
			EntityTooltip(drop->target);
			ImGui::TableNextColumn();
			if (drop->reason == OriginGone)
				ImGui::TextDisabled("(gone)");
			else
				ImGui::Text("%s (shut down)", SCRIPT_DEBUG::ShortText(drop->origin).c_str());
			EntityTooltip(drop->origin);
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(Ago(drop->last, now).c_str());
			ImGui::PopID();
		}
		ImGui::EndTable();
	}

	void DrawTab()
	{
		// Copied at most four times a second (a check failing every frame changes the counts every frame)
		const ULONGLONG now = GetTickCount64();
		static ULONGLONG s_copied = 0;
		if (now - s_copied >= 250)
		{
			s_copied = now;
			TakeSnapshot(g_view);
		}

		ImGui::TextWrapped("The game's own checks of the level's script entities, which the game makes but never reports: each failed check or error, with the entity it was about. Hover over an entity for its ids - OpenCAGE finds it from them.");
		if (!g_attached)
			ImGui::TextColored(ImVec4(1.0f, 0.6f, 0.3f, 1.0f), "The debug menu's hooks are not attached.");
		else if (g_patchedChecks + g_patchedErrors == 0)
			ImGui::TextColored(ImVec4(1.0f, 0.6f, 0.3f, 1.0f), "Not available on this game release.");
		else
		{
			// How many of the places in the game's code that report a problem are watched (each a check or an error)
			ImGui::PushTextWrapPos(0.0f);
			if (g_skipped)
				ImGui::TextDisabled("Watching %d of the %d places in the game's code that report a script problem (%d checks of an entity's setup, %d errors); the other %d are not where they should be in this release of the game, and are left alone.",
					g_patchedChecks + g_patchedErrors, static_cast<int>(kSiteCount), g_patchedChecks, g_patchedErrors, g_skipped);
			else
				ImGui::TextDisabled("Watching all %d places in the game's code that report a script problem: %d checks of an entity's setup and %d errors.", static_cast<int>(kSiteCount),
					g_patchedChecks, g_patchedErrors);
			ImGui::PopTextWrapPos();
		}

		Checkbox("Keep script errors", g_record);
		ImGui::SameLine();
		Checkbox("Show new ones on screen", g_toasts);
		if (ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
			ImGui::SetTooltip("A note at the top of the screen for 6 s with each new script error, whether this window is open or not (scripts.error_toasts; off unless ticked).");
		ImGui::SameLine();
		if (ImGui::Button("Clear"))
		{
			Clear();
			g_selected = -1;
		}
		ImGui::SameLine();
		if (ImGui::Button("Copy"))
		{
			std::string text;
			for (const Entry& entry : g_view.entries)
				if (EntryMatches(entry, g_filter))
					text += EntryText(entry, now) + "\n";
			ImGui::SetClipboardText(text.c_str());
		}
		ImGui::SetNextItemWidth(280.0f * DEBUG_MENU::Scale());
		ImGui::InputTextWithHint("##filter", "Filter (message, entity id or composite)", g_filter, sizeof(g_filter));
		ImGui::SameLine();
		ImGui::Checkbox("Every occurrence", &g_showLog);
		HelpMarker("The latest occurrences one by one (the list shows one line per message and entity).");

		if (g_showLog)
			DrawLog(now);
		else
		{
			DrawEntries(now);
			DrawSelected(now);
			DrawMessages();
		}
		DrawDropped(now);
	}

	// The newest script error, at the top of the screen for a few seconds
	void DrawOverlay(ImDrawList* drawList)
	{
		if (!g_toasts)
			return;
		const ULONGLONG tick = g_toastTick;
		const ULONGLONG now = GetTickCount64();
		constexpr ULONGLONG kShowMs = 6000;
		if (tick == 0 || now - tick > kShowMs)
			return;
		Occurrence latest;
		size_t count = 0;
		AcquireSRWLockShared(&g_lock);
		if (g_logCount > 0)
			latest = g_log[(g_logNext + kLogSize - 1) % kLogSize];
		count = static_cast<size_t>(g_total);
		ReleaseSRWLockShared(&g_lock);
		if (latest.tick == 0)
			return;
		const std::string text = std::string(latest.kind == Check ? "Script check failed: " : "Script error: ") + latest.message + "\n" + SCRIPT_DEBUG::Describe(latest.entity) +
			(count > 1 ? "  (" + std::to_string(count) + " so far - Debug Menu > Scripts > Errors)" : "");
		const float size = 15.0f * DEBUG_MENU::Scale();
		const ImVec2 display = ImGui::GetIO().DisplaySize;
		const float wrap = display.x * 0.6f;
		const ImVec2 extent = ImGui::GetFont()->CalcTextSizeA(size, FLT_MAX, wrap, text.c_str());
		const ImVec2 corner((display.x - extent.x) * 0.5f, DEBUG_MENU::BannerTop(extent.y + 8.0f) + 4.0f); // in the stack of notes along the top
		const float fade = (std::min)(1.0f, static_cast<float>(kShowMs - (now - tick)) / 1000.0f);
		drawList->AddRectFilled(ImVec2(corner.x - 8.0f, corner.y - 4.0f), ImVec2(corner.x + extent.x + 8.0f, corner.y + extent.y + 4.0f), IM_COL32(40, 0, 0, static_cast<int>(190 * fade)), 4.0f);
		drawList->AddText(ImGui::GetFont(), size, corner, IM_COL32(255, 200, 190, static_cast<int>(255 * fade)), text.c_str(), nullptr, wrap);
	}

	const bool g_added = DEBUG_MENU::AddTab("Errors", 100, DrawTab, "Scripts") && DEBUG_MENU::AddOverlay(95, DrawOverlay);
}

void SCRIPT_DEBUG::NoteScriptError(const void* entityRef, const char* message)
{
	if (g_record)
		NoteOwn(entityRef, "%s", message);
}

void SCRIPT_ERRORS::AttachHooks(bool attach)
{
	Attach(attach);
}

bool SCRIPT_ERRORS::Following()
{
	return g_attached && g_patchedChecks + g_patchedErrors > 0;
}

bool SCRIPT_ERRORS::TakeSince(uint32_t after, std::string& message, std::vector<uint8_t>& payload)
{
	if (!Following())
	{
		message = g_attached ? "None of the game's script checks could be followed on this game release"
			: "The game's script errors are not being noted: launch it from OpenCAGE with Error log (or the Debug Menu) ticked";
		return false;
	}
	auto u8 = [&payload](uint8_t value) { payload.push_back(value); };
	auto u32 = [&payload](uint32_t value) { for (int i = 0; i < 4; i++) payload.push_back(static_cast<uint8_t>(value >> (i * 8))); };
	auto f32 = [&u32](float value) { uint32_t bits; memcpy(&bits, &value, 4); u32(bits); };
	auto text = [&payload, &u32](const char* value, size_t size)
	{
		const size_t length = strnlen(value, size);
		u32(static_cast<uint32_t>(length));
		payload.insert(payload.end(), value, value + length);
	};

	payload.clear();
	AcquireSRWLockShared(&g_lock);
	const size_t kept = (std::min)(static_cast<size_t>(g_relaySequence), g_relay.size());
	const size_t oldestAt = (g_relayNext + g_relay.size() - kept) % (std::max)(g_relay.size(), static_cast<size_t>(1));
	const uint32_t oldest = kept == 0 ? 0 : g_relay[oldestAt].sequence;
	const uint32_t newest = g_relaySequence;
	u32(1); // format
	u32(oldest);
	u32(newest);
	u32(SCRIPT_DEBUG::RunningRoot());
	const size_t countAt = payload.size();
	u32(0);
	uint32_t count = 0;
	bool more = false;
	for (size_t i = 0; i < kept; i++)
	{
		const Relayed& relayed = g_relay[(oldestAt + i) % g_relay.size()];
		if (relayed.sequence <= after)
			continue;
		if (count == kRelayBatch)
		{
			more = true;
			break;
		}
		count++;
		u32(relayed.sequence);
		u8(relayed.kind == Check ? 1 : 2);
		u8(relayed.entity.valid ? 1 : 0);
		u8(0);
		u8(0);
		f32(relayed.scriptTime);
		u32(relayed.root);
		u32(relayed.entity.entity);
		u32(relayed.entity.instance);
		u32(relayed.entity.type);
		u32(relayed.entity.composite);
		text(relayed.message, sizeof(relayed.message));
		text(relayed.entity.compositeName, sizeof(relayed.entity.compositeName));
		text(relayed.entity.instanceOf, sizeof(relayed.entity.instanceOf));
	}
	ReleaseSRWLockShared(&g_lock);
	for (int i = 0; i < 4; i++)
		payload[countAt + i] = static_cast<uint8_t>(count >> (i * 8));

	message = "errors=" + std::to_string(count) + "\nnewest=" + std::to_string(newest) + "\noldest=" + std::to_string(oldest) + "\nmore=" + (more ? "1" : "0");
	return true;
}

#include "SCRIPT_DEBUG.h"
#include "DEBUG_MENU.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <algorithm>
#include <atomic>
#include <cstdio>
#include <cstring>
#include <memory>
#include <mutex>
#include <string>
#include <unordered_map>
#include <vector>

#include <imgui.h>

/*
	The trigger queue (debug menu: Scripts > Triggers): what the level's scripts are about to do, and how often they
	do it.

	When an entity fires an output, each method its links call is queued with the entity manager as a call record: the
	method, the entity it is called on, a delay, when it is due (on the script clock), and the record of the call whose
	method fired the output (so a call can be followed back to the start of its chain). The manager keeps the waiting
	calls in a queue ordered by when they are due; once a frame, after its entities have updated, it takes the calls that
	are due and runs them, in cycles, until none are left that are due (a call may queue more for the same frame). A link
	with no delay is still queued, so the queue between frames holds the calls with a delay that are still waiting.

	Waiting calls: the queue is read on the entity thread, at the start of a frame (when nothing is being processed), up to
	kMaxPending calls, each with the entity it is for, the call that queued it and the chain's first call. Only calls
	queued through links show here: some entities time things themselves (a delay entity's own list of pending outputs, a
	timer's own clock), and those are not in the queue.

	Trigger counts: while switched on, every call the manager is about to run is counted, by method and entity it is called
	on (from SCRIPT_ERRORS's patched check, just before the call runs), with how many ran in the last full second - a
	trigger storm (an entity calling itself through a loop, a looping timer at no delay) shows at the top.

	Addresses are from the Steam retail AI.exe.
*/
namespace
{
	// The entity manager's current trigger context (a counted record), whose object holds the waiting calls (a counted array
	// of pointers to call records, a priority queue by due time) and the calls of the cycle being run (likewise, sorted)
	constexpr uint32_t kManagerContext = 0x00;
	constexpr uint32_t kRecordObject = 0x0C;
	constexpr uint32_t kContextWaiting = 0x0C;
	constexpr uint32_t kContextThisCycle = 0x10;
	constexpr uint32_t kArrayCount = 0x08;
	constexpr uint32_t kArrayData = 0x0C;

	// A call record: the method, the delay and the entity it is called on, then the record of the call that led to it, and
	// when it is due (script clock)
	constexpr uint32_t kInfoMethod = 0x00;
	constexpr uint32_t kInfoDelay = 0x04;
	constexpr uint32_t kInfoTarget = 0x08;
	constexpr uint32_t kInfoCause = 0x0C;
	constexpr uint32_t kInfoDue = 0x18;

	constexpr uint32_t kMaxPending = 2000;
	constexpr int kMaxChain = 256;
	constexpr size_t kMaxCounts = 4096;

	// ---- Waiting calls ----
	struct Pending
	{
		double due = 0.0;              // on the script clock
		uint32_t method = 0;
		float delay = 0.0f;
		bool thisCycle = false;        // in the cycle being run (rather than waiting)
		int chain = 0;                 // calls in its chain, it included
		uint32_t causeMethod = 0;      // the call that led to it: its method and entity (none for a call made from outside)
		SCRIPT_DEBUG::EntityId target, cause, origin;
	};

	struct PendingRaw
	{
		double due;
		uint32_t method;
		float delay;
		const void* target;
		const void* cause;             // the cause's record
		uint32_t causeMethod;
		const void* causeTarget;
		const void* originTarget;
		int chain;
	};

	bool ReadPending(const uint8_t* info, PendingRaw& out)
	{
		__try
		{
			out.method = *reinterpret_cast<const uint32_t*>(info + kInfoMethod);
			out.delay = *reinterpret_cast<const float*>(info + kInfoDelay);
			out.target = *reinterpret_cast<const void* const*>(info + kInfoTarget);
			out.due = *reinterpret_cast<const double*>(info + kInfoDue);
			const uint8_t* cause = *reinterpret_cast<const uint8_t* const*>(info + kInfoCause);
			out.cause = cause;
			out.causeMethod = cause ? *reinterpret_cast<const uint32_t*>(cause + kInfoMethod) : 0;
			out.causeTarget = cause ? *reinterpret_cast<const void* const*>(cause + kInfoTarget) : nullptr;
			const uint8_t* at = info;
			int chain = 1;
			while (chain < kMaxChain)
			{
				const uint8_t* next = *reinterpret_cast<const uint8_t* const*>(at + kInfoCause);
				if (!next)
					break;
				at = next;
				chain++;
			}
			out.originTarget = *reinterpret_cast<const void* const*>(at + kInfoTarget);
			out.chain = chain;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// The call records of one of the context's arrays (up to max), guarded
	int ReadArray(const uint8_t* context, uint32_t offset, const uint8_t** out, int max)
	{
		__try
		{
			const uint8_t* array = *reinterpret_cast<const uint8_t* const*>(context + offset);
			if (!array)
				return 0;
			const uint32_t count = *reinterpret_cast<const uint32_t*>(array + kArrayCount);
			const uint8_t* const* data = *reinterpret_cast<const uint8_t* const* const*>(array + kArrayData);
			int found = 0;
			for (uint32_t i = 0; data && i < count && found < max; i++)
				if (data[i])
					out[found++] = data[i];
			return found;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	const uint8_t* CurrentContext()
	{
		__try
		{
			const uint8_t* manager = SCRIPT_DEBUG::EntityManager();
			const uint8_t* record = manager ? *reinterpret_cast<const uint8_t* const*>(manager + kManagerContext) : nullptr;
			return record ? *reinterpret_cast<const uint8_t* const*>(record + kRecordObject) : nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	struct PendingSnapshot
	{
		bool ok = false;               // a level was running and the queue was read
		double now = 0.0;              // the script clock when read
		ULONGLONG tick = 0;
		uint32_t waiting = 0;          // how many calls were waiting (all of them, past the ones read)
		uint32_t thisCycle = 0;
		std::vector<Pending> calls;    // by when they are due
	};

	// Entity thread: reads the queue
	void ReadQueue(PendingSnapshot& out)
	{
		out = PendingSnapshot();
		out.tick = GetTickCount64();
		if (!SCRIPT_DEBUG::LevelRunning())
			return;
		const uint8_t* context = CurrentContext();
		if (!context)
			return;
		out.now = SCRIPT_DEBUG::ScriptTime();
		std::vector<const uint8_t*> records(kMaxPending);
		for (int pass = 0; pass < 2; pass++)
		{
			const int count = ReadArray(context, pass == 0 ? kContextWaiting : kContextThisCycle, records.data(), static_cast<int>(kMaxPending - out.calls.size()));
			(pass == 0 ? out.waiting : out.thisCycle) = static_cast<uint32_t>(count);
			for (int i = 0; i < count; i++)
			{
				PendingRaw raw;
				if (!ReadPending(records[i], raw))
					continue;
				Pending call;
				call.due = raw.due;
				call.method = raw.method;
				call.delay = raw.delay;
				call.thisCycle = pass == 1;
				call.chain = raw.chain;
				call.causeMethod = raw.causeMethod;
				SCRIPT_DEBUG::ReadEntity(raw.target, call.target);
				if (raw.causeTarget)
					SCRIPT_DEBUG::ReadEntity(raw.causeTarget, call.cause);
				if (raw.chain > 1 && raw.originTarget)
					SCRIPT_DEBUG::ReadEntity(raw.originTarget, call.origin);
				out.calls.push_back(call);
			}
		}
		std::stable_sort(out.calls.begin(), out.calls.end(), [](const Pending& a, const Pending& b) { return a.due < b.due; });
		out.ok = true;
	}

	std::mutex g_pendingMutex;
	std::shared_ptr<PendingSnapshot> g_pending = std::make_shared<PendingSnapshot>();
	std::atomic<bool> g_pendingAsked = false;

	void AskForQueue()
	{
		if (g_pendingAsked.exchange(true))
			return;
		DEBUG_MENU::OnEntityThread([]()
		{
			std::shared_ptr<PendingSnapshot> snapshot = std::make_shared<PendingSnapshot>();
			ReadQueue(*snapshot);
			{
				std::lock_guard<std::mutex> lock(g_pendingMutex);
				g_pending = snapshot;
			}
			g_pendingAsked = false;
		});
	}

	std::shared_ptr<PendingSnapshot> LatestQueue()
	{
		std::lock_guard<std::mutex> lock(g_pendingMutex);
		return g_pending;
	}

	std::string PendingText(const Pending& call, double now)
	{
		char due[48];
		snprintf(due, sizeof(due), "due_in=%.2f delay=%.2f", call.due - now, call.delay);
		std::string text = std::string(due) + (call.thisCycle ? " this_cycle" : "") + " method=" + SCRIPT_DEBUG::Hex(call.method) + " method_name=" +
			SCRIPT_DEBUG::MethodText(call.method) + " | " + SCRIPT_DEBUG::Fields(call.target);
		if (call.cause.valid)
			text += " | from method=" + SCRIPT_DEBUG::MethodText(call.causeMethod) + " " + SCRIPT_DEBUG::Fields(call.cause);
		if (call.chain > 2 && call.origin.valid)
			text += " | chain=" + std::to_string(call.chain) + " started_by " + SCRIPT_DEBUG::Fields(call.origin);
		return text;
	}

	bool PendingMatches(const Pending& call, const char* filter)
	{
		if (!filter || !filter[0])
			return true;
		return SCRIPT_DEBUG::Matches(PendingText(call, 0.0), filter) || SCRIPT_DEBUG::Matches(SCRIPT_DEBUG::Describe(call.target), filter) ||
			SCRIPT_DEBUG::Matches(SCRIPT_DEBUG::Describe(call.cause), filter);
	}

	// ---- Trigger counts ----
	std::atomic<bool> g_counting = false;

	struct CountKey
	{
		uint32_t entity, instance, method;
		bool operator==(const CountKey& other) const { return entity == other.entity && instance == other.instance && method == other.method; }
	};
	struct CountKeyHash
	{
		size_t operator()(const CountKey& key) const { return key.entity * 0x9E3779B1u ^ key.instance * 0x85EBCA77u ^ key.method * 0xC2B2AE3Du; }
	};
	struct Count
	{
		SCRIPT_DEBUG::EntityId target;
		uint32_t method = 0;
		uint64_t total = 0;
		uint32_t second = 0;           // the second (tick count / 1000) the window is for
		uint32_t window = 0;           // how many in that second so far
		uint32_t previous = 0;         // how many in the second before it
	};

	SRWLOCK g_countLock = SRWLOCK_INIT;
	std::unordered_map<CountKey, Count, CountKeyHash> g_counts;
	uint64_t g_countedTotal = 0;
	uint64_t g_countOverflow = 0;
	uint32_t g_countRoot = 0;
	ULONGLONG g_countingSince = 0;

	struct CountRead
	{
		uint32_t method;
		const void* target;
	};

	bool ReadCount(const uint8_t* info, CountRead& out)
	{
		__try
		{
			out.method = *reinterpret_cast<const uint32_t*>(info + kInfoMethod);
			out.target = *reinterpret_cast<const void* const*>(info + kInfoTarget);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// How many in the last full second, as of now
	uint32_t PerSecond(const Count& count, uint32_t nowSecond)
	{
		if (count.second == nowSecond)
			return count.previous;
		if (count.second + 1 == nowSecond)
			return count.window;
		return 0;
	}

	struct CountView
	{
		Count count;
		uint32_t perSecond;
	};

	std::vector<CountView> TakeCounts(uint64_t& total, uint64_t& overflow, ULONGLONG& since)
	{
		const uint32_t nowSecond = static_cast<uint32_t>(GetTickCount64() / 1000);
		std::vector<CountView> out;
		AcquireSRWLockShared(&g_countLock);
		out.reserve(g_counts.size());
		for (const auto& entry : g_counts)
			out.push_back({ entry.second, PerSecond(entry.second, nowSecond) });
		total = g_countedTotal;
		overflow = g_countOverflow;
		since = g_countingSince;
		ReleaseSRWLockShared(&g_countLock);
		std::sort(out.begin(), out.end(), [](const CountView& a, const CountView& b)
		{
			return a.perSecond != b.perSecond ? a.perSecond > b.perSecond : a.count.total > b.count.total;
		});
		return out;
	}

	void ClearCounts()
	{
		AcquireSRWLockExclusive(&g_countLock);
		g_counts.clear();
		g_countedTotal = g_countOverflow = 0;
		g_countingSince = GetTickCount64();
		ReleaseSRWLockExclusive(&g_countLock);
	}

	std::string CountText(const CountView& view)
	{
		return std::to_string(view.perSecond) + "/s total=" + std::to_string(view.count.total) + " method=" + SCRIPT_DEBUG::Hex(view.count.method) + " method_name=" +
			SCRIPT_DEBUG::MethodText(view.count.method) + " | " + SCRIPT_DEBUG::Fields(view.count.target);
	}

	// ---- Commands and switches ----
	bool Register()
	{
		DEBUG_MENU::AddSwitch("scripts.trigger_counts", &g_counting);
		DEBUG_MENU::AddCommand("scripts.list_pending", "the calls waiting in the level's trigger queue, soonest first; argument: a filter",
			[](const std::string& argument) -> DEBUG_MENU::CommandResult
			{
				return DEBUG_MENU::AnswerOnEntityThread([argument]() -> DEBUG_MENU::CommandResult
				{
					PendingSnapshot snapshot;
					ReadQueue(snapshot);
					if (!snapshot.ok)
						return { false, "no level running" };
					char head[96];
					snprintf(head, sizeof(head), "script_time=%.2f waiting=%u this_cycle=%u\n", snapshot.now, snapshot.waiting, snapshot.thisCycle);
					DEBUG_MENU::CommandResult result;
					result.message = head;
					for (const Pending& call : snapshot.calls)
						if (PendingMatches(call, argument.c_str()))
							result.message += PendingText(call, snapshot.now) + "\n";
					return result;
				});
			});
		DEBUG_MENU::AddCommand("scripts.list_trigger_counts", "calls run by method and entity, busiest first (scripts.trigger_counts must be on); argument: how many (default 40)",
			[](const std::string& argument) -> DEBUG_MENU::CommandResult
			{
				uint64_t total = 0, overflow = 0;
				ULONGLONG since = 0;
				const std::vector<CountView> counts = TakeCounts(total, overflow, since);
				const size_t wanted = argument.empty() ? 40 : static_cast<size_t>((std::max)(1, atoi(argument.c_str())));
				char head[128];
				snprintf(head, sizeof(head), "counting=%d total=%llu entries=%u seconds=%.1f%s\n", g_counting ? 1 : 0, static_cast<unsigned long long>(total),
					static_cast<unsigned>(counts.size()), since ? static_cast<double>(GetTickCount64() - since) / 1000.0 : 0.0, overflow ? " (list full: some not counted)" : "");
				DEBUG_MENU::CommandResult result;
				result.message = head;
				for (size_t i = 0; i < counts.size() && i < wanted; i++)
					result.message += CountText(counts[i]) + "\n";
				return result;
			});
		DEBUG_MENU::AddCommand("scripts.clear_trigger_counts", "starts the trigger counts afresh",
			[](const std::string&) -> DEBUG_MENU::CommandResult
			{
				ClearCounts();
				return { true, "cleared" };
			});
		return true;
	}

	const bool g_registered = Register();

	// ---- The tab ----
	char g_pendingFilter[96] = "";
	char g_countFilter[96] = "";
	bool g_pendingLive = true;
	ULONGLONG g_lastAsked = 0;

	void Checkbox(const char* label, std::atomic<bool>& value)
	{
		bool ticked = value;
		if (ImGui::Checkbox(label, &ticked))
			value = ticked;
	}

	void EntityCell(const SCRIPT_DEBUG::EntityId& id)
	{
		if (!id.valid)
		{
			ImGui::TextDisabled("-");
			return;
		}
		// On one line (a composite instance shows its composite's whole path; the table scrolls sideways for it)
		ImGui::Text("%s %s", SCRIPT_DEBUG::TypeText(id).c_str(), SCRIPT_DEBUG::Guid(id.entity).c_str());
		if (ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
		{
			const std::string where = SCRIPT_DEBUG::WhereText(id);
			ImGui::BeginTooltip();
			ImGui::Text("Entity %s (0x%08X), type id %s", SCRIPT_DEBUG::Guid(id.entity).c_str(), id.entity, SCRIPT_DEBUG::Guid(id.type).c_str());
			if (where.empty())
				ImGui::TextUnformatted("The level root itself (in no composite)");
			else
				ImGui::Text("In %s (%s), composite instance %s (0x%08X)", where.c_str(), SCRIPT_DEBUG::Guid(id.composite).c_str(), SCRIPT_DEBUG::Guid(id.instance).c_str(), id.instance);
			ImGui::EndTooltip();
		}
	}

	void DrawPending()
	{
		ImGui::SeparatorText("Waiting calls");
		ImGui::TextWrapped("Methods queued through links that have not run yet, soonest first (a link with a delay waits here). Some entities time things themselves (delays, timers) and do not show here.");
		ImGui::Checkbox("Keep reading", &g_pendingLive);
		ImGui::SameLine();
		if (ImGui::Button("Read now") || (g_pendingLive && GetTickCount64() - g_lastAsked > 250))
		{
			g_lastAsked = GetTickCount64();
			AskForQueue();
		}
		const std::shared_ptr<PendingSnapshot> snapshot = LatestQueue();
		ImGui::SameLine();
		if (ImGui::Button("Copy##pending"))
		{
			std::string text;
			for (const Pending& call : snapshot->calls)
				if (PendingMatches(call, g_pendingFilter))
					text += PendingText(call, snapshot->now) + "\n";
			ImGui::SetClipboardText(text.c_str());
		}
		ImGui::SameLine();
		ImGui::SetNextItemWidth(240.0f * DEBUG_MENU::Scale());
		ImGui::InputTextWithHint("##pendingfilter", "Filter (method, entity, composite)", g_pendingFilter, sizeof(g_pendingFilter));
		if (!snapshot->ok)
		{
			ImGui::TextDisabled(snapshot->tick ? "No level running." : "Not read yet.");
			return;
		}
		ImGui::Text("%u waiting%s, script clock %.2f s", snapshot->waiting, snapshot->waiting >= kMaxPending ? " (the first shown)" : "", snapshot->now);

		if (!SCRIPT_DEBUG::BeginTable("pending", 4, 220.0f * DEBUG_MENU::Scale()))
			return;
		ImGui::TableSetupScrollFreeze(0, 1);
		ImGui::TableSetupColumn("Due in");
		ImGui::TableSetupColumn("Method");
		ImGui::TableSetupColumn("Called on");
		ImGui::TableSetupColumn("Queued by");
		ImGui::TableHeadersRow();
		int row = 0;
		for (const Pending& call : snapshot->calls)
		{
			if (!PendingMatches(call, g_pendingFilter))
				continue;
			ImGui::PushID(row++);
			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			ImGui::Text("%.2f s%s", call.due - snapshot->now, call.thisCycle ? " (now)" : "");
			if (ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
				ImGui::SetTooltip("Due at %.2f on the script clock; the link's delay %.2f s; %d calls in its chain.", call.due, call.delay, call.chain);
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(SCRIPT_DEBUG::MethodText(call.method).c_str());
			ImGui::TableNextColumn();
			EntityCell(call.target);
			ImGui::TableNextColumn();
			// All on one line: the method and entity that queued it, then where its chain of calls started
			if (call.cause.valid)
			{
				ImGui::Text("%s:", SCRIPT_DEBUG::MethodText(call.causeMethod).c_str());
				ImGui::SameLine();
				EntityCell(call.cause);
			}
			else
			{
				ImGui::TextDisabled("(from outside the scripts)");
			}
			if (call.chain > 2 && call.origin.valid)
			{
				ImGui::SameLine();
				ImGui::TextDisabled("- chain of %d, started by", call.chain);
				ImGui::SameLine();
				EntityCell(call.origin);
			}
			ImGui::PopID();
		}
		ImGui::EndTable();
	}

	void DrawCounts()
	{
		ImGui::SeparatorText("Trigger counts");
		ImGui::TextWrapped("Every call the level's scripts run, counted by method and entity, busiest first: an entity calling itself in a loop, or a timer firing far more often than meant, shows at the top.");
		Checkbox("Count", g_counting);
		if (ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
			ImGui::SetTooltip("Counts every call the level's scripts run while ticked (scripts.trigger_counts).");
		ImGui::SameLine();
		if (ImGui::Button("Clear##counts"))
			ClearCounts();
		// Read again at most four times a second (the list can be long)
		static std::vector<CountView> s_counts;
		static uint64_t s_total = 0, s_overflow = 0;
		static ULONGLONG s_since = 0, s_read = 0;
		if (GetTickCount64() - s_read >= 250)
		{
			s_read = GetTickCount64();
			s_counts = TakeCounts(s_total, s_overflow, s_since);
		}
		const std::vector<CountView>& counts = s_counts;
		const uint64_t total = s_total, overflow = s_overflow;
		const ULONGLONG since = s_since;
		ImGui::SameLine();
		if (ImGui::Button("Copy##counts"))
		{
			std::string text;
			for (const CountView& view : counts)
				if (SCRIPT_DEBUG::Matches(CountText(view), g_countFilter) || SCRIPT_DEBUG::Matches(SCRIPT_DEBUG::Describe(view.count.target), g_countFilter))
					text += CountText(view) + "\n";
			ImGui::SetClipboardText(text.c_str());
		}
		ImGui::SameLine();
		ImGui::SetNextItemWidth(240.0f * DEBUG_MENU::Scale());
		ImGui::InputTextWithHint("##countfilter", "Filter", g_countFilter, sizeof(g_countFilter));
		ImGui::Text("%llu counted over %.1f s, %u methods on entities%s", static_cast<unsigned long long>(total), since ? static_cast<double>(GetTickCount64() - since) / 1000.0 : 0.0,
			static_cast<unsigned>(counts.size()), overflow ? " (the list is full: new ones are not counted)" : "");
		if (counts.empty())
		{
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextDisabled(g_counting ? "Counting: nothing has run yet (no level running, or its scripts are paused)." :
				"Tick Count to count every call the level's scripts run; the busiest show here.");
			ImGui::PopTextWrapPos();
			return;
		}

		if (!SCRIPT_DEBUG::BeginTable("counts", 4, 200.0f * DEBUG_MENU::Scale()))
			return;
		ImGui::TableSetupScrollFreeze(0, 1);
		ImGui::TableSetupColumn("Last second");
		ImGui::TableSetupColumn("Total");
		ImGui::TableSetupColumn("Method");
		ImGui::TableSetupColumn("Called on");
		ImGui::TableHeadersRow();
		int row = 0;
		for (const CountView& view : counts)
		{
			if (g_countFilter[0] && !SCRIPT_DEBUG::Matches(CountText(view), g_countFilter) && !SCRIPT_DEBUG::Matches(SCRIPT_DEBUG::Describe(view.count.target), g_countFilter))
				continue;
			if (row >= 500)
				break;
			ImGui::PushID(row++);
			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			ImGui::Text("%u", view.perSecond);
			ImGui::TableNextColumn();
			ImGui::Text("%llu", static_cast<unsigned long long>(view.count.total));
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(SCRIPT_DEBUG::MethodText(view.count.method).c_str());
			ImGui::TableNextColumn();
			EntityCell(view.count.target);
			const std::string where = SCRIPT_DEBUG::WhereText(view.count.target);
			if (view.count.target.valid && !where.empty())
			{
				ImGui::SameLine();
				ImGui::TextDisabled("in %s", where.c_str());
			}
			ImGui::PopID();
		}
		ImGui::EndTable();
	}

	void DrawTab()
	{
		DrawPending();
		DrawCounts();
	}

	const bool g_added = DEBUG_MENU::AddTab("Triggers", 110, DrawTab, "Scripts");
}

void SCRIPT_DEBUG::CountTrigger(const void* info)
{
	if (!g_counting)
		return;
	CountRead read;
	if (!ReadCount(static_cast<const uint8_t*>(info), read))
		return;
	EntityId target;
	ReadEntity(read.target, target);
	const CountKey key = { target.entity, target.instance, read.method };
	const uint32_t nowSecond = static_cast<uint32_t>(GetTickCount64() / 1000);
	const uint32_t root = RunningRoot();

	AcquireSRWLockExclusive(&g_countLock);
	if (root != 0 && root != g_countRoot)
	{
		// Another level: its entities are others
		g_countRoot = root;
		g_counts.clear();
		g_countedTotal = g_countOverflow = 0;
		g_countingSince = GetTickCount64();
	}
	if (!g_countingSince)
		g_countingSince = GetTickCount64();
	g_countedTotal++;
	auto it = g_counts.find(key);
	if (it == g_counts.end() && g_counts.size() < kMaxCounts)
	{
		Count count;
		count.target = target;
		count.method = read.method;
		count.second = nowSecond;
		it = g_counts.emplace(key, count).first;
	}
	if (it != g_counts.end())
	{
		Count& count = it->second;
		if (count.second != nowSecond)
		{
			count.previous = count.second + 1 == nowSecond ? count.window : 0;
			count.window = 0;
			count.second = nowSecond;
		}
		count.window++;
		count.total++;
	}
	else
	{
		g_countOverflow++;
	}
	ReleaseSRWLockExclusive(&g_countLock);
}

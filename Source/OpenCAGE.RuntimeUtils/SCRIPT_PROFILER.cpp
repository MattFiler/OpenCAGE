#include "SCRIPT_DEBUG.h"
#include "DEBUG_MENU.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <algorithm>
#include <atomic>
#include <cstdio>
#include <cstring>
#include <map>
#include <string>
#include <unordered_map>
#include <vector>

#include <imgui.h>

/*
	Script profiler (debug menu: Scripts > Profiler): where the level's scripts spend the game's time, by entity,
	by entity type and by composite.

	Each frame the entity manager updates every entity marked as updated every frame (a composite instance updates the
	entities in it the same way, inside its own update), and runs the methods called through links. While the profiler is
	on, the game's calls of an entity's update (the two places it makes them) and of a method on an entity (the two
	places the trigger processing makes them) are pointed at functions that time the call and pass it on; an update that
	runs others inside it (an instance's) is counted for its own time, the others' taken off. The times are added up by
	entity (its ids read the first time it is seen), and shown as milliseconds per second of play, busiest first.

	While it is off each changed call costs a check of a flag or two. The counts start afresh when it is switched on (here
	or over Live Link), and when another level loads; the time they are per second of is the time they were taken over.

	Addresses are from the Steam retail AI.exe.
*/
namespace
{
	// ---- The game's calls ----
	// An entity's update (on its state, at the entity + 4): the entity (a pointer to its counted record), the call record
	constexpr uintptr_t kProcess = 0x00536940;
	constexpr uintptr_t kProcessSites[] = {
		0x005303AB, // the entity manager's update of the entities it updates every frame
		0x00562571, // a composite instance's update of the entities in it
	};
	typedef bool(__thiscall* t_process)(void* state, void* entity, const void* info);
	t_process g_process = nullptr;

	// A method run on an entity (by the entity manager): the entity (twice: as resolved, and as called), the call record
	constexpr uintptr_t kCallMethod = 0x00526680;
	constexpr uintptr_t kCallMethodSites[] = {
		0x00526B42, // a method called through a link
		0x00526B9F, // a callback handed on to the entities that asked for it
	};
	typedef bool(__thiscall* t_call_method)(void* manager, void* entity, void* called, const void* info);
	t_call_method g_callMethod = nullptr;

	constexpr uint32_t kRecordObject = 0x0C;
	constexpr uint32_t kInfoMethod = 0x00;
	constexpr size_t kMaxEntities = 16384;
	constexpr int kMaxDepth = 64;

	// ---- What is counted ----
	struct Accumulator
	{
		SCRIPT_DEBUG::EntityId id;
		uint64_t updates = 0;
		int64_t updateSelf = 0;        // counter ticks
		int64_t updateTotal = 0;
		uint64_t calls = 0;
		int64_t callSelf = 0;
		uint32_t lastMethod = 0;
	};

	std::atomic<bool> g_on = false;
	std::atomic<bool> g_wasOn = false;     // what the timed calls last saw (switched on since: the counts start afresh)
	SRWLOCK g_lock = SRWLOCK_INIT;
	std::unordered_map<const void*, size_t> g_index;
	std::vector<Accumulator> g_accumulators;
	uint64_t g_overflow = 0;
	ULONGLONG g_since = 0;
	ULONGLONG g_last = 0;                  // the last timing added (the counts are per second of g_since..g_last)
	uint32_t g_root = 0;
	int64_t g_frequency = 1;

	thread_local int t_depth = 0;
	thread_local int64_t t_children[kMaxDepth];

	int64_t Now()
	{
		LARGE_INTEGER counter;
		QueryPerformanceCounter(&counter);
		return counter.QuadPart;
	}

	const void* ObjectOf(const void* entityRef)
	{
		__try
		{
			const uint8_t* record = entityRef ? *static_cast<const uint8_t* const*>(entityRef) : nullptr;
			return record ? *reinterpret_cast<const void* const*>(record + kRecordObject) : nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	uint32_t MethodOf(const void* infoRef)
	{
		__try
		{
			const uint8_t* info = infoRef ? *static_cast<const uint8_t* const*>(infoRef) : nullptr;
			return info ? *reinterpret_cast<const uint32_t*>(info + kInfoMethod) : 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	// Adds a timing to the entity's accumulator (made, with the entity's ids, the first time it is seen)
	void Add(const void* entityRef, bool call, int64_t self, int64_t total, uint32_t method)
	{
		const void* object = ObjectOf(entityRef);
		if (!object)
			return;
		const uint32_t root = SCRIPT_DEBUG::RunningRoot();
		const ULONGLONG now = GetTickCount64();
		AcquireSRWLockExclusive(&g_lock);
		if (!g_since)
			g_since = now;
		if (root != 0 && root != g_root)
		{
			g_root = root;
			g_index.clear();
			g_accumulators.clear();
			g_overflow = 0;
			g_since = now;
		}
		g_last = now;
		auto it = g_index.find(object);
		size_t index;
		if (it != g_index.end())
		{
			index = it->second;
		}
		else if (g_accumulators.size() < kMaxEntities)
		{
			index = g_accumulators.size();
			g_accumulators.emplace_back();
			SCRIPT_DEBUG::ReadEntityAt(entityRef, g_accumulators.back().id);
			g_index.emplace(object, index);
		}
		else
		{
			g_overflow++;
			ReleaseSRWLockExclusive(&g_lock);
			return;
		}
		Accumulator& accumulator = g_accumulators[index];
		if (call)
		{
			accumulator.calls++;
			accumulator.callSelf += self;
			accumulator.lastMethod = method;
		}
		else
		{
			accumulator.updates++;
			accumulator.updateSelf += self;
			accumulator.updateTotal += total;
		}
		ReleaseSRWLockExclusive(&g_lock);
	}

	void Reset()
	{
		AcquireSRWLockExclusive(&g_lock);
		// Let go of the buffers as well (a busy level's counts grow them to a few MB), and make room up front while on, so
		// growing them does not land inside a timed call
		std::vector<Accumulator>().swap(g_accumulators);
		std::unordered_map<const void*, size_t>().swap(g_index);
		if (g_on)
		{
			g_accumulators.reserve(4096);
			g_index.reserve(4096);
		}
		g_overflow = 0;
		g_since = g_last = GetTickCount64();
		ReleaseSRWLockExclusive(&g_lock);
	}

	// Whether the timed calls time: the first to see the profiler switched on starts the counts afresh
	bool Timing()
	{
		if (!g_on)
		{
			if (g_wasOn.load(std::memory_order_relaxed))
				g_wasOn = false;
			return false;
		}
		if (!g_wasOn.load(std::memory_order_relaxed) && !g_wasOn.exchange(true))
			Reset();
		return true;
	}

	// The timed calls: the time taken, less that of the timed calls inside, is the call's own
	bool __fastcall OnProcess(void* state, void* /*_EDX*/, void* entity, const void* info)
	{
		if (!Timing() || t_depth >= kMaxDepth)
			return g_process(state, entity, info);
		const int depth = t_depth++;
		t_children[depth] = 0;
		const int64_t start = Now();
		const bool result = g_process(state, entity, info);
		const int64_t elapsed = Now() - start;
		t_depth--;
		if (depth > 0)
			t_children[depth - 1] += elapsed;
		Add(entity, false, elapsed - t_children[depth], elapsed, 0);
		return result;
	}

	bool __fastcall OnCallMethod(void* manager, void* /*_EDX*/, void* entity, void* called, const void* info)
	{
		if (!Timing() || t_depth >= kMaxDepth)
			return g_callMethod(manager, entity, called, info);
		const int depth = t_depth++;
		t_children[depth] = 0;
		const int64_t start = Now();
		const bool result = g_callMethod(manager, entity, called, info);
		const int64_t elapsed = Now() - start;
		t_depth--;
		if (depth > 0)
			t_children[depth - 1] += elapsed;
		Add(entity, true, elapsed - t_children[depth], elapsed, MethodOf(info));
		return result;
	}

	// ---- Hooks ----
	SCRIPT_DEBUG::PatchedCall g_processPatches[2], g_callPatches[2];
	int g_patched = 0;
	bool g_attached = false;

	void Attach(bool attach)
	{
		if (attach == g_attached)
			return;
		g_attached = attach;
		if (!attach)
		{
			for (SCRIPT_DEBUG::PatchedCall& call : g_processPatches)
				SCRIPT_DEBUG::RestoreCall(call);
			for (SCRIPT_DEBUG::PatchedCall& call : g_callPatches)
				SCRIPT_DEBUG::RestoreCall(call);
			return;
		}
		LARGE_INTEGER frequency;
		QueryPerformanceFrequency(&frequency);
		g_frequency = frequency.QuadPart > 0 ? frequency.QuadPart : 1;
		g_process = reinterpret_cast<t_process>(DEVTOOLS_RELATIVE_ADDRESS(kProcess));
		g_callMethod = reinterpret_cast<t_call_method>(DEVTOOLS_RELATIVE_ADDRESS(kCallMethod));
		const char* why = nullptr;
		for (int i = 0; i < 2; i++)
		{
			if (g_process && SCRIPT_DEBUG::PatchCall(kProcessSites[i], kProcess, reinterpret_cast<const void*>(&OnProcess), g_processPatches[i], why))
				g_patched++;
			if (g_callMethod && SCRIPT_DEBUG::PatchCall(kCallMethodSites[i], kCallMethod, reinterpret_cast<const void*>(&OnCallMethod), g_callPatches[i], why))
				g_patched++;
		}
		DevTools::Log("Debug menu: script profiler - %d of 4 calls followed.", g_patched);
	}

	const bool g_hooks = DEBUG_MENU::AddHooks(Attach);

	// ---- Reports ----
	struct Row
	{
		std::string name;              // an entity's description, a type's or a composite's name
		SCRIPT_DEBUG::EntityId id;     // for an entity
		double updateMs = 0.0, callMs = 0.0, totalMs = 0.0; // per second of play
		double updates = 0.0, calls = 0.0;                  // per second
	};

	enum class GroupBy { Entity, Type, Composite };

	std::vector<Row> Report(GroupBy by, double& seconds, uint64_t& overflow, size_t& entities)
	{
		std::vector<Accumulator> accumulators;
		ULONGLONG since = 0, last = 0;
		AcquireSRWLockShared(&g_lock);
		accumulators = g_accumulators;
		since = g_since;
		last = g_last;
		overflow = g_overflow;
		ReleaseSRWLockShared(&g_lock);
		entities = accumulators.size();
		// Per second of the time the counts were taken over (not of the time since, which goes on while switched off)
		seconds = since && last > since ? (std::max)(0.001, static_cast<double>(last - since) / 1000.0) : 0.001;
		const double toMs = 1000.0 / static_cast<double>(g_frequency) / seconds;
		std::map<std::string, Row> groups;
		std::vector<Row> rows;
		for (const Accumulator& accumulator : accumulators)
		{
			Row row;
			row.id = accumulator.id;
			row.updateMs = static_cast<double>(accumulator.updateSelf) * toMs;
			row.callMs = static_cast<double>(accumulator.callSelf) * toMs;
			row.totalMs = row.updateMs + row.callMs;
			row.updates = static_cast<double>(accumulator.updates) / seconds;
			row.calls = static_cast<double>(accumulator.calls) / seconds;
			if (by == GroupBy::Entity)
			{
				row.name = SCRIPT_DEBUG::Describe(accumulator.id);
				rows.push_back(row);
				continue;
			}
			const std::string where = SCRIPT_DEBUG::WhereText(accumulator.id);
			const std::string key = by == GroupBy::Type ? SCRIPT_DEBUG::TypeText(accumulator.id) : (where.empty() ? std::string("the level root itself") : where);
			Row& group = groups[key];
			group.name = key;
			group.updateMs += row.updateMs;
			group.callMs += row.callMs;
			group.totalMs += row.totalMs;
			group.updates += row.updates;
			group.calls += row.calls;
		}
		for (auto& entry : groups)
			rows.push_back(entry.second);
		std::sort(rows.begin(), rows.end(), [](const Row& a, const Row& b) { return a.totalMs > b.totalMs; });
		return rows;
	}

	std::string RowText(const Row& row, GroupBy by)
	{
		char text[160];
		snprintf(text, sizeof(text), "ms_per_s=%.3f update_ms=%.3f call_ms=%.3f updates_per_s=%.1f calls_per_s=%.1f | ", row.totalMs, row.updateMs, row.callMs, row.updates, row.calls);
		return text + (by == GroupBy::Entity ? SCRIPT_DEBUG::Fields(row.id) : row.name);
	}

	bool Register()
	{
		DEBUG_MENU::AddSwitch("scripts.profiler", &g_on);
		DEBUG_MENU::AddCommand("scripts.list_profile", "the script profiler's busiest, ms per second of play (scripts.profiler on); arguments: [how many] [entity|type|composite]",
			[](const std::string& argument) -> DEBUG_MENU::CommandResult
			{
				size_t wanted = 30;
				GroupBy by = GroupBy::Entity;
				char word[32] = {};
				int number = 0;
				if (sscanf_s(argument.c_str(), "%d %31s", &number, word, static_cast<unsigned>(sizeof(word))) >= 1 && number > 0)
					wanted = static_cast<size_t>(number);
				else
					sscanf_s(argument.c_str(), "%31s", word, static_cast<unsigned>(sizeof(word)));
				if (strcmp(word, "type") == 0)
					by = GroupBy::Type;
				else if (strcmp(word, "composite") == 0)
					by = GroupBy::Composite;
				double seconds = 0.0;
				uint64_t overflow = 0;
				size_t entities = 0;
				const std::vector<Row> rows = Report(by, seconds, overflow, entities);
				char head[160];
				snprintf(head, sizeof(head), "on=%d calls_followed=%d seconds=%.1f entities=%u%s\n", g_on ? 1 : 0, g_patched, seconds, static_cast<unsigned>(entities), overflow ? " (list full)" : "");
				DEBUG_MENU::CommandResult result;
				result.message = head;
				for (size_t i = 0; i < rows.size() && i < wanted; i++)
					result.message += RowText(rows[i], by) + "\n";
				return result;
			});
		DEBUG_MENU::AddCommand("scripts.clear_profile", "starts the script profiler's counts afresh",
			[](const std::string&) -> DEBUG_MENU::CommandResult
			{
				Reset();
				return { true, "cleared" };
			});
		return true;
	}

	const bool g_registered = Register();

	// ---- The tab ----
	int g_groupBy = 0;
	char g_filter[96] = "";

	void DrawTab()
	{
		const bool on = g_on;

		ImGui::TextWrapped("Where the level's scripts spend the game's time: each entity's own updates (the entities updated every frame) and the methods run on it, in milliseconds per second of play. Costs a little while on.");
		if (g_patched < 4)
		{
			ImGui::TextColored(ImVec4(1.0f, 0.6f, 0.3f, 1.0f), g_attached ? "Only %d of the game's 4 calls are followed in this release of the game." : "The debug menu's hooks are not attached.", g_patched);
			if (g_patched == 0)
				return;
		}
		bool ticked = on;
		if (ImGui::Checkbox("Profile", &ticked))
			g_on = ticked;
		if (ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
			ImGui::SetTooltip("Times the level's scripts while ticked (scripts.profiler).");
		ImGui::SameLine();
		if (ImGui::Button("Start again"))
			Reset();
		ImGui::SameLine();
		ImGui::RadioButton("Entities", &g_groupBy, 0);
		ImGui::SameLine();
		ImGui::RadioButton("Types", &g_groupBy, 1);
		ImGui::SameLine();
		ImGui::RadioButton("Composites", &g_groupBy, 2);
		const GroupBy by = g_groupBy == 1 ? GroupBy::Type : g_groupBy == 2 ? GroupBy::Composite : GroupBy::Entity;
		// Worked out again at most twice a second (or when the grouping changes)
		static std::vector<Row> s_rows;
		static double s_seconds = 0.0;
		static uint64_t s_overflow = 0;
		static size_t s_entities = 0;
		static ULONGLONG s_read = 0;
		static int s_groupBy = -1;
		if (GetTickCount64() - s_read >= 500 || s_groupBy != g_groupBy)
		{
			s_read = GetTickCount64();
			s_groupBy = g_groupBy;
			s_rows = Report(by, s_seconds, s_overflow, s_entities);
		}
		const std::vector<Row>& rows = s_rows;
		const double seconds = s_seconds;
		const uint64_t overflow = s_overflow;
		const size_t entities = s_entities;
		ImGui::SameLine();
		if (ImGui::Button("Copy"))
		{
			std::string text;
			for (const Row& row : rows)
				text += RowText(row, by) + "\n";
			ImGui::SetClipboardText(text.c_str());
		}
		ImGui::SetNextItemWidth(240.0f * DEBUG_MENU::Scale());
		ImGui::InputTextWithHint("##filter", "Filter", g_filter, sizeof(g_filter));
		double total = 0.0;
		for (const Row& row : rows)
			total += row.totalMs;
		ImGui::TextWrapped("%.1f s profiled, %u entities: %.2f ms of script time per second of play%s", seconds, static_cast<unsigned>(entities), total, overflow ? " (the list is full)" : "");
		if (rows.empty())
		{
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextDisabled(on ? "Profiling: nothing timed yet (no level running, or its scripts are paused)." :
				"Tick Profile to time the level's scripts: the entities, types or composites that take the most time show here, busiest first.");
			ImGui::PopTextWrapPos();
			return;
		}

		if (!SCRIPT_DEBUG::BeginTable("profile", 5, 380.0f * DEBUG_MENU::Scale()))
			return;
		ImGui::TableSetupScrollFreeze(0, 1);
		// Milliseconds of the game's time per second of play: in all, in its own updates, in the methods run on it
		ImGui::TableSetupColumn("Total ms/s");
		ImGui::TableSetupColumn("Updates ms/s");
		ImGui::TableSetupColumn("Methods ms/s");
		ImGui::TableSetupColumn("Runs a second");
		ImGui::TableSetupColumn(by == GroupBy::Entity ? "Entity" : by == GroupBy::Type ? "Type" : "Composite");
		ImGui::TableHeadersRow();
		int shown = 0;
		for (const Row& row : rows)
		{
			if (!SCRIPT_DEBUG::Matches(row.name, g_filter))
				continue;
			if (shown >= 500)
				break;
			ImGui::PushID(shown++);
			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			ImGui::Text("%.3f", row.totalMs);
			ImGui::TableNextColumn();
			ImGui::Text("%.3f", row.updateMs);
			ImGui::TableNextColumn();
			ImGui::Text("%.3f", row.callMs);
			ImGui::TableNextColumn();
			ImGui::Text("%.0f / %.0f", row.updates, row.calls);
			if (ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
				ImGui::SetTooltip("Updates and methods run, per second");
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(row.name.c_str());
			if (by == GroupBy::Entity && ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
				ImGui::SetTooltip("%s", SCRIPT_DEBUG::Fields(row.id).c_str());
			ImGui::PopID();
		}
		ImGui::EndTable();
	}

	const bool g_added = DEBUG_MENU::AddTab("Profiler", 160, DrawTab, "Scripts");
}

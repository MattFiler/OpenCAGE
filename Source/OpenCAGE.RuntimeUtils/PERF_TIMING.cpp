#include "PERF_TIMING.h"
#include "PERF_STATS.h"
#include "DEBUG_MENU.h"
#include "LIVE_LINK.h"
#include "Menu.h"

#include <algorithm>
#include <atomic>
#include <cfloat>
#include <cstdarg>
#include <cstdio>
#include <cstring>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <d3d11.h>

#include <imgui.h>

/*
	CPU time per task and GPU time per render stage, and the Timing tab (see PERF_TIMING.h).

	Addresses and layouts are the Steam retail AI.exe's (see GameAddresses.h for the GOG and Epic Games Store builds).
	The two game functions called here take and give back the renderer's ownership of its device (and with it the
	device's context): they only note which thread has it, counting a thread's takes (taking it fails, without waiting,
	while another thread has it; giving it back does nothing on a thread that doesn't have it) - the render stages and
	the frame's own drawing take it the same way. The game has a second pair for its other contexts (a view's own, the
	one a shadow's list of drawing is built in), which keeps the note inside the context it is given: that pair is not
	the one to call with the device.
*/
namespace
{
	// The task table: a task's entry is 0x128 bytes, its function first (0 for a task the game handles itself); how many
	// tasks there are; and their names, 0x40 characters each
	constexpr uintptr_t kTaskTable = 0x015A7790;
	constexpr uint32_t kTaskEntrySize = 0x128;
	constexpr uintptr_t kTaskCount = 0x015B9FF0;
	constexpr uintptr_t kTaskNames = 0x017DB1F8;
	constexpr uint32_t kTaskNameLength = 0x40;
	constexpr int kMaxTasks = 256;

	// The renderer's device (a pointer), and the taking and giving back of its ownership (the device's pair, as the game's
	// render stages and its frame call it with this pointer)
	constexpr uintptr_t kGfxDevice = 0x017DF5CC;
	constexpr uintptr_t kTakeContext = 0x00623530;
	constexpr uintptr_t kGiveContext = 0x006235D0;
	using TakeContext = bool(__cdecl*)(void* device);
	using GiveContext = void(__cdecl*)(void* device);

	// A task's function: its data, and the worker running it (run again for as long as it returns other than 0)
	using TaskFunction = int(__cdecl*)(void* data, int worker);

	// The render stages, in the order the frame runs them (each the task of that name)
	struct Stage
	{
		const char* task;
		const char* label;
		const char* tooltip;
	};
	const Stage kStages[] = {
		{ "RENDER_BEGIN_SCENE", "Scene set-up", "The frame's shared constants and the occlusion volumes." },
		{ "RENDER_EXTRACT_PROBES", "Bounce-light probes", "The bounce light's (radiosity's) surface and object probes." },
		{ "RENDER_PRE_MAIN_POV", "Views before the main one", "The views drawn before the player's: reflections and the like." },
		{ "RENDER_BEGIN_MAIN_POV", "Main view set-up", "The start of the player's view: its targets cleared." },
		{ "RENDER_GATHER_LIGHTS_MAIN_POV", "Light gathering", "The lights of the player's view gathered for drawing." },
		{ "RENDER_SOLIDS_MAIN_POV", "Solid geometry", "The opaque geometry of the player's view, into the G-buffer." },
		{ "RENDER_LIGHTS_MAIN_POV", "Lighting", "Everything after the solids up to transparency: the real-time lights and their shadow maps, the bounce light, decals, the sky..." },
		{ "RENDER_DISTORSION_MAIN_POV", "Distortion", "Heat haze and the other screen distortion." },
		{ "RENDER_VELOCITY_MAIN_POV", "Velocity", "The motion vectors (for motion blur and temporal anti-aliasing)." },
		{ "RENDER_ALPHA_MAIN_POV", "Transparency", "Glass, particles, smoke and the other transparent geometry." },
		{ "RENDER_END_MAIN_POV", "Post-processing", "Exposure, the end of the 3D frame (post-processing and anti-aliasing) and the reflection mips." },
		{ "RENDER_STEREO_EYE", "Second eye", "Only in stereo." },
		{ "RENDER_END_SCENE", "Scene end", "The occlusion queries' results read back." },
	};
	constexpr int kStageCount = sizeof(kStages) / sizeof(kStages[0]);

	// ---- Switches ----

	std::atomic<bool> g_cpuTiming = false;
	std::atomic<bool> g_gpuTiming = false;

	// ---- The task table ----

	struct Task
	{
		std::atomic<TaskFunction> original = nullptr; // the game's function (kept for stubs still running)
		std::atomic<uint32_t> ticks = 0;              // this frame's time, in performance counter ticks
		std::atomic<uint32_t> runs = 0;
		std::atomic<uint32_t> thread = 0;             // the thread it last ran on
		int stage = -1;                               // its render stage (set before its stub is put in)
		// Render thread
		bool patched = false;
		char name[kTaskNameLength + 1] = {};
		float lastMs = 0.0f;
		uint32_t lastRuns = 0;
		uint32_t lastThread = 0;
		float history[60] = {};
		int historyNext = 0;
		int historyFilled = 0;
	};
	Task g_tasks[kMaxTasks];
	int g_taskCount = 0;      // render thread: the tasks the table held when last patched
	bool g_patched = false;   // render thread
	bool g_unavailable = false;
	double g_ticksPerMs = 0.0;
	uint32_t g_mainThread = 0;

	// The stubs, one per task: each calls TimedTask with its task's index, the task's data and the worker
	constexpr int kStubSize = 32;
	uint8_t* g_stubs = nullptr;

	int __cdecl TimedTask(int index, void* data, int worker);
	void Stamp(int stage, int end);

	uintptr_t Stub(int index)
	{
		return reinterpret_cast<uintptr_t>(g_stubs + index * kStubSize);
	}

	bool MakeStubs()
	{
		if (g_stubs)
			return true;
		const SIZE_T size = kMaxTasks * kStubSize;
		uint8_t* page = static_cast<uint8_t*>(VirtualAlloc(nullptr, size, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE));
		if (!page)
			return false;
		memset(page, 0xCC, size);
		for (int i = 0; i < kMaxTasks; i++)
		{
			uint8_t* stub = page + i * kStubSize;
			// push [esp+8] (the worker), push [esp+8] (the data), push index, call TimedTask, add esp 12, ret
			const uint8_t code[] = { 0xFF, 0x74, 0x24, 0x08, 0xFF, 0x74, 0x24, 0x08, 0x68, 0, 0, 0, 0, 0xE8, 0, 0, 0, 0, 0x83, 0xC4, 0x0C, 0xC3 };
			memcpy(stub, code, sizeof(code));
			const int32_t index = i;
			memcpy(stub + 9, &index, sizeof(index));
			const int32_t distance = static_cast<int32_t>(reinterpret_cast<uintptr_t>(&TimedTask) - reinterpret_cast<uintptr_t>(stub + 18));
			memcpy(stub + 14, &distance, sizeof(distance));
		}
		DWORD oldProtect;
		if (!VirtualProtect(page, size, PAGE_EXECUTE_READ, &oldProtect))
		{
			VirtualFree(page, 0, MEM_RELEASE);
			return false;
		}
		FlushInstructionCache(GetCurrentProcess(), page, size);
		g_stubs = page;
		return true;
	}

	struct Table
	{
		uint8_t* entries = nullptr;
		const char* names = nullptr;
		int count = 0;
	};

	// Where the table, its count and the names are in this build (looked up once)
	uintptr_t g_tableAt = 0, g_countAt = 0, g_namesAt = 0;
	bool g_tableLookedUp = false;

	bool ReadTable(Table& out)
	{
		if (!g_tableLookedUp)
		{
			g_tableLookedUp = true;
			g_tableAt = DEVTOOLS_RELATIVE_ADDRESS(kTaskTable);
			g_countAt = DEVTOOLS_RELATIVE_ADDRESS(kTaskCount);
			g_namesAt = DEVTOOLS_RELATIVE_ADDRESS(kTaskNames);
		}
		const uintptr_t table = g_tableAt, count = g_countAt, names = g_namesAt;
		if (!table || !count || !names)
			return false;
		__try
		{
			out.entries = reinterpret_cast<uint8_t*>(table);
			out.names = reinterpret_cast<const char*>(names);
			out.count = static_cast<int>(*reinterpret_cast<const uint32_t*>(count));
			return out.count > 0 && out.count <= kMaxTasks;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	uintptr_t ReadFunction(const Table& table, int index)
	{
		__try
		{
			return *reinterpret_cast<const volatile uintptr_t*>(table.entries + index * kTaskEntrySize);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	// Swaps a task's function in the table only while it is still the one expected (one locked write, which a task being
	// released on another thread sees whole: the old function or the new)
	bool SwapFunction(const Table& table, int index, uintptr_t expected, uintptr_t replacement)
	{
		__try
		{
			volatile LONG* slot = reinterpret_cast<volatile LONG*>(table.entries + index * kTaskEntrySize);
			return static_cast<uintptr_t>(InterlockedCompareExchange(slot, static_cast<LONG>(replacement), static_cast<LONG>(expected))) == expected;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	void ReadName(const Table& table, int index, char* out)
	{
		__try
		{
			memcpy(out, table.names + index * kTaskNameLength, kTaskNameLength);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			out[0] = '\0';
		}
		out[kTaskNameLength] = '\0';
	}

	int StageOf(const char* name)
	{
		for (int i = 0; i < kStageCount; i++)
			if (strcmp(name, kStages[i].task) == 0)
				return i;
		return -1;
	}

	// Render thread: every task with a function gets its stub (and again if the game sets a task's function anew)
	void Patch()
	{
		Table table;
		if (!MakeStubs() || !ReadTable(table))
		{
			if (!g_unavailable)
				DevTools::Log("Debug menu: task timing is not available (the task table could not be read, or no memory for the stubs).");
			g_unavailable = true;
			return;
		}
		g_taskCount = table.count;
		for (int i = 0; i < table.count; i++)
		{
			Task& task = g_tasks[i];
			// Switched on afresh: the averages start again
			if (!g_patched)
			{
				task.historyFilled = 0;
				task.historyNext = 0;
				task.ticks = 0;
				task.runs = 0;
			}
			const uintptr_t function = ReadFunction(table, i);
			if (!function)
				continue;
			// Still ours (its swap back failed when last switched off): timed again
			if (function == Stub(i))
			{
				task.patched = task.original.load() != nullptr;
				continue;
			}
			// A function of its own (first patch, or the game set a new one): kept, then the stub put in
			ReadName(table, i, task.name);
			task.stage = StageOf(task.name);
			task.original = reinterpret_cast<TaskFunction>(function);
			if (SwapFunction(table, i, function, Stub(i)))
				task.patched = true;
		}
		if (!g_patched)
			DevTools::Log("Debug menu: task timing on (%d tasks).", table.count);
		g_patched = true;
	}

	// Render thread (or as the DLL detaches): the game's own functions back in the table
	void Unpatch()
	{
		if (!g_patched)
			return;
		Table table;
		if (ReadTable(table))
			for (int i = 0; i < table.count && i < kMaxTasks; i++)
			{
				Task& task = g_tasks[i];
				if (task.patched && task.original)
					SwapFunction(table, i, Stub(i), reinterpret_cast<uintptr_t>(task.original.load()));
				task.patched = false;
			}
		g_patched = false;
		DevTools::Log("Debug menu: task timing off.");
	}

	// ---- GPU timestamps ----

	struct GpuSet
	{
		ID3D11Query* disjoint = nullptr;
		ID3D11Query* start = nullptr;
		ID3D11Query* end = nullptr;
		ID3D11Query* stages[kStageCount][2] = {};
		std::atomic<uint32_t> issued[kStageCount] = {}; // bit 0 the stage's start written, bit 1 its end
		bool open = false;      // its disjoint query begun, the frame's stamps going into it
		bool waiting = false;   // ended, not read yet
		uint32_t frame = 0;     // the frame it ended on
		uint32_t opened = 0;    // the frame it was opened on (a set left open over more than one frame is not used)
	};
	constexpr int kSets = 6;
	GpuSet g_sets[kSets];
	std::atomic<int> g_openSet = -1;       // the set the render stages write into (-1: none)
	std::atomic<uint32_t> g_skippedStamps = 0;      // stamps left out: another thread had the device
	ID3D11Device* g_device = nullptr;
	ID3D11DeviceContext* g_context = nullptr;
	bool g_queriesMade = false;
	bool g_queriesFailed = false;
	uint32_t g_frame = 0;
	TakeContext g_take = nullptr;
	GiveContext g_give = nullptr;
	uintptr_t g_gfxDeviceAt = 0;

	// The last measured frames' GPU times (render thread)
	struct GpuFrame
	{
		float frameMs = 0.0f;
		float stageMs[kStageCount] = {};
		bool stageMeasured[kStageCount] = {};
		float afterMs = -1.0f;   // from the end of the scene to the presentation (the HUD, menus)
	};
	constexpr int kGpuHistory = 60;
	GpuFrame g_gpuHistory[kGpuHistory];
	int g_gpuNext = 0;
	int g_gpuFilled = 0;
	// Since GPU timing was last switched on (render thread): frames opened and measured; frames let go - the GPU had not
	// finished one five frames on, its timestamps were disjoint (the GPU's clock changed), or it was left open over more
	// than one frame; and presentations at which another thread had the device
	uint32_t g_gpuOpened = 0, g_gpuMeasured = 0, g_dropLate = 0, g_dropDisjoint = 0, g_dropOther = 0, g_presentSkipped = 0;
	bool g_gpuWasOn = false;

	uint32_t Dropped()
	{
		return g_dropLate + g_dropDisjoint + g_dropOther;
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

	void* GfxDevice()
	{
		__try
		{
			return g_gfxDeviceAt ? *reinterpret_cast<void* const*>(g_gfxDeviceAt) : nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	bool MakeQueries()
	{
		if (g_queriesMade)
			return true;
		if (g_queriesFailed)
			return false;
		g_take = reinterpret_cast<TakeContext>(DEVTOOLS_RELATIVE_ADDRESS(kTakeContext));
		g_give = reinterpret_cast<GiveContext>(DEVTOOLS_RELATIVE_ADDRESS(kGiveContext));
		g_gfxDeviceAt = DEVTOOLS_RELATIVE_ADDRESS(kGfxDevice);
		IDXGISwapChain* swapChain = Menu::GetSwapChain();
		if (!g_take || !g_give || !g_gfxDeviceAt || !swapChain || FAILED(Menu::GetDeviceAndContextFromSwapChain(swapChain, &g_device, &g_context)) || !g_device || !g_context)
		{
			g_queriesFailed = true;
			DevTools::Log("Debug menu: GPU timing is not available (no device, or not on this build of the game).");
			return false;
		}
		D3D11_QUERY_DESC timestamp = { D3D11_QUERY_TIMESTAMP, 0 };
		D3D11_QUERY_DESC disjoint = { D3D11_QUERY_TIMESTAMP_DISJOINT, 0 };
		bool made = true;
		for (GpuSet& set : g_sets)
		{
			made = made && SUCCEEDED(g_device->CreateQuery(&disjoint, &set.disjoint));
			made = made && SUCCEEDED(g_device->CreateQuery(&timestamp, &set.start));
			made = made && SUCCEEDED(g_device->CreateQuery(&timestamp, &set.end));
			for (int i = 0; i < kStageCount; i++)
				for (int j = 0; j < 2; j++)
					made = made && SUCCEEDED(g_device->CreateQuery(&timestamp, &set.stages[i][j]));
		}
		if (!made)
		{
			g_queriesFailed = true;
			DevTools::Log("Debug menu: GPU timing is not available (its queries could not be made).");
			return false;
		}
		g_queriesMade = true;
		return true;
	}

	// Any thread (the render stages' tasks): a timestamp at the start or end of a stage, inside the stage's own task (the
	// stage before it has finished, the next not started; the frame gives up the device while its render stages run),
	// written only while the device can be taken (never while another thread has it)
	void Stamp(int stage, int end)
	{
		const int index = g_openSet.load(std::memory_order_acquire);
		if (index < 0 || !g_context || !g_take || !g_give)
			return;
		GpuSet& set = g_sets[index];
		ID3D11Query* query = set.stages[stage][end];
		void* device = GfxDevice();
		if (!query || !device)
			return;
		if (!g_take(device))
		{
			g_skippedStamps++;
			return;
		}
		g_context->End(query);
		g_give(device);
		set.issued[stage].fetch_or(1u << end);
	}

	float Milliseconds(UINT64 from, UINT64 to, UINT64 frequency)
	{
		return to >= from && frequency ? static_cast<float>(static_cast<double>(to - from) * 1000.0 / static_cast<double>(frequency)) : 0.0f;
	}

	bool GetStamp(ID3D11Query* query, UINT64& out)
	{
		return g_context->GetData(query, &out, sizeof(out), D3D11_ASYNC_GETDATA_DONOTFLUSH) == S_OK;
	}

	// Reads an ended set, never waiting: false while the GPU hasn't finished it
	bool ReadSet(GpuSet& set)
	{
		D3D11_QUERY_DATA_TIMESTAMP_DISJOINT disjoint = {};
		if (g_context->GetData(set.disjoint, &disjoint, sizeof(disjoint), D3D11_ASYNC_GETDATA_DONOTFLUSH) != S_OK)
			return false;
		UINT64 start = 0, end = 0;
		if (disjoint.Disjoint || !disjoint.Frequency)
		{
			g_dropDisjoint++;
			return true;
		}
		if (set.frame - set.opened != 1 || !GetStamp(set.start, start) || !GetStamp(set.end, end))
		{
			g_dropOther++;
			return true;
		}
		GpuFrame frame;
		frame.frameMs = Milliseconds(start, end, disjoint.Frequency);
		for (int i = 0; i < kStageCount; i++)
		{
			UINT64 from = 0, to = 0;
			if (set.issued[i].load() == 3 && GetStamp(set.stages[i][0], from) && GetStamp(set.stages[i][1], to) && from >= start && to <= end)
			{
				frame.stageMs[i] = Milliseconds(from, to, disjoint.Frequency);
				frame.stageMeasured[i] = true;
				if (i == kStageCount - 1)
					frame.afterMs = Milliseconds(to, end, disjoint.Frequency);
			}
		}
		g_gpuHistory[g_gpuNext] = frame;
		g_gpuNext = (g_gpuNext + 1) % kGpuHistory;
		g_gpuFilled = (std::min)(g_gpuFilled + 1, kGpuHistory);
		g_gpuMeasured++;
		return true;
	}

	// Render thread, at the presentation: the open set ended (its last stamp and its disjoint query), older ones read
	// when the GPU has them, and the next opened. Nothing while switched off, or with no level running (loading screens
	// present from another thread).
	void GpuOnFrame(bool on)
	{
		g_frame++;
		// Switched on afresh: the counts start again
		if (on && !g_gpuWasOn)
		{
			g_gpuOpened = g_gpuMeasured = g_dropLate = g_dropDisjoint = g_dropOther = g_presentSkipped = 0;
			g_skippedStamps = 0;
		}
		g_gpuWasOn = on;
		const int open = g_openSet.load();
		const bool level = on && LevelRunning();
		if (!level)
		{
			g_gpuFilled = 0;
			g_gpuNext = 0;
		}
		// Nothing to do while off with nothing in flight
		bool waiting = false;
		for (const GpuSet& set : g_sets)
			waiting = waiting || set.waiting;
		if (!level && open < 0 && !waiting)
			return;
		if (!g_queriesMade && !MakeQueries())
			return;
		void* device = GfxDevice();
		if (!device)
			return;
		// In a level the game's frame has the device here (on the thread presenting), so taking it again only counts one more
		// take, given back below
		if (!g_take(device))
		{
			g_presentSkipped++;
			return;
		}
		// The open set ended (stamps stop going into it first)
		if (open >= 0)
		{
			g_openSet = -1;
			GpuSet& set = g_sets[open];
			g_context->End(set.end);
			g_context->End(set.disjoint);
			set.open = false;
			set.waiting = true;
			set.frame = g_frame;
		}
		// The ended ones read once two frames old (the GPU may run up to three frames behind), or let go if the GPU still
		// hasn't finished one five frames on
		for (GpuSet& set : g_sets)
		{
			const uint32_t age = g_frame - set.frame;
			if (!set.waiting || age < 2)
				continue;
			if (ReadSet(set))
				set.waiting = false;
			else if (age >= 5)
			{
				set.waiting = false;
				g_dropLate++;
			}
		}
		// The next opened, when one is free
		if (level)
			for (int i = 0; i < kSets; i++)
			{
				GpuSet& set = g_sets[i];
				if (set.waiting || set.open)
					continue;
				for (std::atomic<uint32_t>& issued : set.issued)
					issued = 0;
				g_context->Begin(set.disjoint);
				g_context->End(set.start);
				set.open = true;
				set.opened = g_frame;
				g_openSet = i;
				g_gpuOpened++;
				break;
			}
		g_give(device);
	}

	// The averages of the last measured frames
	GpuFrame GpuAverage(int& frames)
	{
		GpuFrame average;
		frames = g_gpuFilled;
		if (!frames)
			return average;
		int stageFrames[kStageCount] = {};
		int afterFrames = 0;
		float after = 0.0f;
		for (int i = 0; i < frames; i++)
		{
			const GpuFrame& frame = g_gpuHistory[i];
			average.frameMs += frame.frameMs;
			for (int s = 0; s < kStageCount; s++)
				if (frame.stageMeasured[s])
				{
					average.stageMs[s] += frame.stageMs[s];
					stageFrames[s]++;
				}
			if (frame.afterMs >= 0.0f)
			{
				after += frame.afterMs;
				afterFrames++;
			}
		}
		average.frameMs /= frames;
		for (int s = 0; s < kStageCount; s++)
		{
			average.stageMeasured[s] = stageFrames[s] > 0;
			if (stageFrames[s])
				average.stageMs[s] /= stageFrames[s];
		}
		average.afterMs = afterFrames ? after / afterFrames : -1.0f;
		return average;
	}

	// ---- CPU times ----

	int __cdecl TimedTask(int index, void* data, int worker)
	{
		Task& task = g_tasks[index];
		const TaskFunction original = task.original.load(std::memory_order_acquire);
		if (!original)
			return 0;
		const int stage = task.stage;
		const bool gpu = stage >= 0 && g_openSet.load(std::memory_order_relaxed) >= 0;
		if (gpu)
			Stamp(stage, 0);
		LARGE_INTEGER start, end;
		QueryPerformanceCounter(&start);
		const int result = original(data, worker);
		QueryPerformanceCounter(&end);
		if (gpu)
			Stamp(stage, 1);
		task.ticks.fetch_add(static_cast<uint32_t>(end.QuadPart - start.QuadPart), std::memory_order_relaxed);
		task.runs.fetch_add(1, std::memory_order_relaxed);
		task.thread.store(GetCurrentThreadId(), std::memory_order_relaxed);
		return result;
	}

	void TakeCpuTimes()
	{
		if (g_ticksPerMs == 0.0)
		{
			LARGE_INTEGER frequency;
			QueryPerformanceFrequency(&frequency);
			g_ticksPerMs = static_cast<double>(frequency.QuadPart) / 1000.0;
		}
		for (int i = 0; i < g_taskCount; i++)
		{
			Task& task = g_tasks[i];
			if (!task.patched)
				continue;
			const uint32_t ticks = task.ticks.exchange(0);
			task.lastRuns = task.runs.exchange(0);
			task.lastThread = task.thread.load();
			task.lastMs = static_cast<float>(ticks / g_ticksPerMs);
			task.history[task.historyNext] = task.lastMs;
			task.historyNext = (task.historyNext + 1) % 60;
			task.historyFilled = (std::min)(task.historyFilled + 1, 60);
		}
	}

	float AverageMs(const Task& task)
	{
		if (!task.historyFilled)
			return 0.0f;
		float total = 0.0f;
		for (int i = 0; i < task.historyFilled; i++)
			total += task.history[i];
		return total / task.historyFilled;
	}

	// The threads tasks ran on, numbered as first seen (render thread)
	uint32_t g_threads[32] = {};
	int g_threadCount = 0;

	std::string ThreadName(uint32_t id)
	{
		if (!id)
			return "-";
		if (id == g_mainThread)
			return "main";
		for (int i = 0; i < g_threadCount; i++)
			if (g_threads[i] == id)
				return "worker " + std::to_string(i + 1);
		if (g_threadCount < 32)
		{
			g_threads[g_threadCount++] = id;
			return "worker " + std::to_string(g_threadCount);
		}
		return "worker";
	}

	// ---- Text ----

	std::string Format(const char* format, ...)
	{
		char text[512];
		va_list args;
		va_start(args, format);
		vsnprintf(text, sizeof(text), format, args);
		va_end(args);
		return text;
	}

	std::vector<int> TasksBySlowest()
	{
		std::vector<int> order;
		for (int i = 0; i < g_taskCount; i++)
			if (g_tasks[i].patched || g_tasks[i].historyFilled)
				order.push_back(i);
		std::stable_sort(order.begin(), order.end(), [](int a, int b) { return AverageMs(g_tasks[a]) > AverageMs(g_tasks[b]); });
		return order;
	}

	std::string TasksText()
	{
		if (g_unavailable)
			return "task timing is not available on this build of the game";
		if (!g_patched || !g_cpuTiming)
			return "task timing is off (switch perf.cpu_timing on)";
		std::string text;
		float main = 0.0f, workers = 0.0f;
		for (int index : TasksBySlowest())
		{
			const Task& task = g_tasks[index];
			const float average = AverageMs(task);
			const std::string thread = ThreadName(task.lastThread);
			(thread == "main" ? main : workers) += average;
			text += Format("%s ms=%.3f last=%.3f runs=%u thread=%s\n", task.name[0] ? task.name : "(no name)", average, task.lastMs, task.lastRuns, thread.c_str());
		}
		return Format("main_thread_ms=%.2f\nworker_threads_ms=%.2f\n", main, workers) + text;
	}

	// How the measuring has gone since GPU timing was switched on
	std::string CountsText()
	{
		return Format("frames_opened=%u\nframes_measured=%u\ndropped_frames=%u (late=%u disjoint=%u other=%u)\npresentations_skipped=%u\nskipped_stamps=%u\n",
			g_gpuOpened, g_gpuMeasured, Dropped(), g_dropLate, g_dropDisjoint, g_dropOther, g_presentSkipped, g_skippedStamps.load());
	}

	std::string GpuText()
	{
		if (g_queriesFailed || g_unavailable)
			return "GPU timing is not available (no device, or not on this build of the game)";
		if (!g_gpuTiming)
			return "GPU timing is off (switch perf.gpu_timing on)";
		int frames = 0;
		const GpuFrame average = GpuAverage(frames);
		if (!frames)
		{
			if (!LevelRunning())
				return "no level running: the GPU is timed while a level runs";
			return "measuring: no frame measured yet\n" + CountsText();
		}
		std::string text = Format("frames=%d\nframe_ms=%.3f\n", frames, average.frameMs) + CountsText();
		float total = 0.0f;
		for (int s = 0; s < kStageCount; s++)
			if (average.stageMeasured[s])
			{
				text += Format("%s ms=%.3f (%s)\n", kStages[s].task, average.stageMs[s], kStages[s].label);
				total += average.stageMs[s];
			}
			else
				text += Format("%s ms=- (%s)\n", kStages[s].task, kStages[s].label);
		if (average.afterMs >= 0.0f)
			text += Format("AFTER_SCENE ms=%.3f (HUD, menus, to the presentation)\n", average.afterMs);
		text += Format("stages_ms=%.3f\n", total);
		return text;
	}

	// ---- Drawing ----

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

	void DrawGpu()
	{
		bool on = g_gpuTiming;
		ImGui::BeginDisabled(g_queriesFailed || g_unavailable);
		if (ImGui::Checkbox("Time each render stage on the GPU", &on))
			g_gpuTiming = on;
		ImGui::EndDisabled();
		Tooltip("Writes a GPU timestamp around each render stage (about 30 a frame) and reads them a few frames later, never waiting for the GPU (perf.gpu_timing). It uses the CPU timing's stubs: the task table is patched while either is on.");
		if (g_queriesFailed || g_unavailable)
		{
			PERF_STATS::Note("Not available on this build of the game.");
			return;
		}
		if (!on)
		{
			PERF_STATS::Note("When ticked: a table of how long the GPU takes over each render stage of the frame (scene set-up, the views before the main one, solid geometry, "
				"lighting, transparency, post-processing...) in milliseconds, with each stage's share of the frame, and the whole frame's GPU time - averaged over the last 60 "
				"frames measured, while a level runs. The GPU frame time also shows on the Frame tab, on the graph over the game and in the Info lines. It costs about 30 "
				"timestamps a frame.");
			return;
		}
		int frames = 0;
		const GpuFrame average = GpuAverage(frames);
		if (!frames)
		{
			if (!LevelRunning())
				PERF_STATS::Note("Measured while a level runs.");
			else
				PERF_STATS::Note("Measuring... %u frames opened, %u measured, %u let go, %u presentations skipped (the context was another thread's).", g_gpuOpened, g_gpuMeasured,
					Dropped(), g_presentSkipped);
			return;
		}
		ImGui::TextWrapped("GPU frame: %.2f ms (presentation to presentation, averaged over the last %d frames)", average.frameMs, frames);
		if (!PERF_STATS::BeginTable("stages", 3))
			return;
		ImGui::TableSetupColumn("Stage", ImGuiTableColumnFlags_WidthFixed | ImGuiTableColumnFlags_NoHide);
		ImGui::TableSetupColumn("ms", ImGuiTableColumnFlags_WidthFixed, PERF_STATS::Width("000.00"));
		ImGui::TableSetupColumn("of the frame", ImGuiTableColumnFlags_WidthStretch);
		ImGui::TableHeadersRow();
		float total = 0.0f;
		auto row = [&](const char* label, const char* tooltip, bool measured, float ms)
		{
			// The text level with the bar's
			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			ImGui::AlignTextToFramePadding();
			ImGui::TextUnformatted(label);
			Tooltip(tooltip);
			ImGui::TableNextColumn();
			ImGui::AlignTextToFramePadding();
			if (measured)
				ImGui::Text("%.2f", ms);
			else
				ImGui::TextDisabled("-");
			ImGui::TableNextColumn();
			if (measured && average.frameMs > 0.0f)
			{
				char overlay[16];
				snprintf(overlay, sizeof(overlay), "%.0f%%", 100.0f * ms / average.frameMs);
				ImGui::ProgressBar((std::min)(1.0f, ms / average.frameMs), ImVec2(-FLT_MIN, 0.0f), overlay);
			}
		};
		for (int s = 0; s < kStageCount; s++)
		{
			row(kStages[s].label, kStages[s].tooltip, average.stageMeasured[s], average.stageMs[s]);
			if (average.stageMeasured[s])
				total += average.stageMs[s];
		}
		row("After the scene", "From the end of the scene to the presentation: the HUD and menus.", average.afterMs >= 0.0f, average.afterMs);
		PERF_STATS::EndTable();
		const float other = average.frameMs - total - (average.afterMs >= 0.0f ? average.afterMs : 0.0f);
		PERF_STATS::Note("Stages %.2f ms; the rest (%.2f ms) is the GPU's work between stages and its waiting for the CPU.", total, (std::max)(0.0f, other));
		if (g_skippedStamps || Dropped())
			PERF_STATS::Note("%u stamps left out (the context was another thread's), %u frames let go (%u not finished by the GPU in time, %u with the GPU's clock changing).",
				g_skippedStamps.load(), Dropped(), g_dropLate, g_dropDisjoint);
	}

	char g_taskFilter[64] = "";

	void DrawCpu()
	{
		bool on = g_cpuTiming;
		ImGui::BeginDisabled(g_unavailable);
		if (ImGui::Checkbox("Time each task on the CPU", &on))
			g_cpuTiming = on;
		ImGui::EndDisabled();
		Tooltip("Times every task of the game's frame (perf.cpu_timing): two reads of the clock per task per frame.");
		if (g_unavailable)
		{
			PERF_STATS::Note("Not available on this build of the game.");
			return;
		}
		if (!on)
		{
			PERF_STATS::Note("When ticked: a table of every task of the game's frame (the scripts, the AI, physics, building and sorting the render lists, the render "
				"stages...), slowest first, with its average and last time in milliseconds, how many times it ran in the frame and which thread ran it, and the main and "
				"worker threads' totals. It costs two reads of the clock per task per frame.");
			return;
		}
		if (!g_patched)
			return;
		PERF_STATS::Note("A task's time includes any task it ran while waiting for another. Averages over 60 frames.");
		ImGui::SetNextItemWidth(200.0f * DEBUG_MENU::Scale());
		ImGui::InputTextWithHint("##taskfilter", "Filter", g_taskFilter, sizeof(g_taskFilter));
		float main = 0.0f, workers = 0.0f;
		const std::vector<int> order = TasksBySlowest();
		for (int index : order)
			(g_tasks[index].lastThread == g_mainThread ? main : workers) += AverageMs(g_tasks[index]);
		ImGui::TextWrapped("Main thread %.2f ms, worker threads %.2f ms a frame", main, workers);
		// The tasks the filter lets through; each task's name in full, the numbers as wide as the widest they can be written
		std::string filter = g_taskFilter;
		for (char& c : filter)
			c = static_cast<char>(toupper(static_cast<unsigned char>(c)));
		std::vector<int> shown;
		float nameWidth = PERF_STATS::Width("Task");
		for (int index : order)
		{
			const Task& task = g_tasks[index];
			if (!filter.empty() && !strstr(task.name, filter.c_str()))
				continue;
			shown.push_back(index);
			nameWidth = (std::max)(nameWidth, PERF_STATS::Width(task.name[0] ? task.name : "(no name)"));
		}
		const float msWidth = PERF_STATS::Width("0000.000");
		const float runsWidth = (std::max)(PERF_STATS::Width("Runs"), PERF_STATS::Width("00000"));
		const float threadWidth = (std::max)(PERF_STATS::Width("Thread"), PERF_STATS::Width("worker 32"));
		if (!PERF_STATS::BeginScrollingTable("tasks", 5, nameWidth + msWidth * 2.0f + runsWidth + threadWidth, static_cast<int>(shown.size()), false, 320.0f * DEBUG_MENU::Scale()))
			return;
		ImGui::TableSetupScrollFreeze(1, 1);
		ImGui::TableSetupColumn("Task", ImGuiTableColumnFlags_WidthStretch | ImGuiTableColumnFlags_NoHide);
		ImGui::TableSetupColumn("ms", ImGuiTableColumnFlags_WidthFixed, msWidth);
		ImGui::TableSetupColumn("Last", ImGuiTableColumnFlags_WidthFixed, msWidth);
		ImGui::TableSetupColumn("Runs", ImGuiTableColumnFlags_WidthFixed, runsWidth);
		ImGui::TableSetupColumn("Thread", ImGuiTableColumnFlags_WidthFixed, threadWidth);
		ImGui::TableHeadersRow();
		for (int index : shown)
		{
			const Task& task = g_tasks[index];
			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(task.name[0] ? task.name : "(no name)");
			ImGui::TableNextColumn();
			ImGui::Text("%.3f", AverageMs(task));
			ImGui::TableNextColumn();
			ImGui::Text("%.3f", task.lastMs);
			ImGui::TableNextColumn();
			ImGui::Text("%u", task.lastRuns);
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(ThreadName(task.lastThread).c_str());
		}
		PERF_STATS::EndTable();
	}

	void DrawTimingTab()
	{
		PERF_STATS::EnsureRunning();
		if (ImGui::CollapsingHeader("GPU time per render stage", ImGuiTreeNodeFlags_DefaultOpen))
			DrawGpu();
		if (ImGui::CollapsingHeader("CPU time per task", ImGuiTreeNodeFlags_DefaultOpen))
			DrawCpu();
	}

	// The switches turned on from outside the window start the reading too (it does the patching)
	void WatchSwitches(ImDrawList* /*drawList*/)
	{
		if (g_cpuTiming || g_gpuTiming)
			PERF_STATS::EnsureRunning();
	}

	void AttachHooks(bool attach)
	{
		// Nothing is changed as the DLL attaches (the table is patched only while switched on); put back as it detaches
		if (!attach)
			Unpatch();
	}

	DEBUG_MENU::CommandResult OnRender(std::string (*make)())
	{
		PERF_STATS::EnsureRunning();
		return DEBUG_MENU::AnswerOnRenderThread([make]() -> DEBUG_MENU::CommandResult { return { true, make() }; });
	}

	bool Register()
	{
		DEBUG_MENU::AddSwitch("perf.cpu_timing", &g_cpuTiming);
		DEBUG_MENU::AddSwitch("perf.gpu_timing", &g_gpuTiming);
		DEBUG_MENU::AddCommand("perf.list_tasks", "CPU time per task (needs perf.cpu_timing): main/worker totals, then one task a line, slowest first: NAME ms=<average> last= runs= thread=",
			[](const std::string&) { return OnRender([]() { return TasksText(); }); });
		DEBUG_MENU::AddCommand("perf.list_gpu_passes", "GPU time per render stage (needs perf.gpu_timing): frame_ms, then one stage a line: TASK ms=<average> (label)",
			[](const std::string&) { return OnRender([]() { return GpuText(); }); });
		return true;
	}

	const bool g_registered = Register();
	const bool g_added = DEBUG_MENU::AddTab("Timing", 101, DrawTimingTab, "Performance") && DEBUG_MENU::AddOverlay(96, WatchSwitches);
	const bool g_hooks = DEBUG_MENU::AddHooks(AttachHooks);
}

void PERF_TIMING::OnFrame()
{
	g_mainThread = GetCurrentThreadId();
	const bool wanted = (g_cpuTiming || g_gpuTiming) && !g_unavailable;
	if (wanted)
		Patch();
	else
		Unpatch();
	if (g_patched)
		TakeCpuTimes();
	GpuOnFrame(g_gpuTiming && g_patched);
}

bool PERF_TIMING::Busy()
{
	if (g_patched || g_cpuTiming || g_gpuTiming || g_openSet.load() >= 0)
		return true;
	for (const GpuSet& set : g_sets)
		if (set.waiting)
			return true;
	return false;
}

bool PERF_TIMING::GpuTimingOn()
{
	return g_gpuTiming && !g_queriesFailed && !g_unavailable;
}

bool PERF_TIMING::GpuFrameMs(float& ms)
{
	if (!g_gpuTiming || !g_gpuFilled)
		return false;
	int frames = 0;
	ms = GpuAverage(frames).frameMs;
	return frames > 0;
}

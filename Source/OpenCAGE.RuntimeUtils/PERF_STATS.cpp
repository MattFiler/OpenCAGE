#include "PERF_STATS.h"
#include "PERF_TIMING.h"
#include "DEBUG_MENU.h"
#include "DEBUG_MARKER.h"
#include "LIVE_LINK.h"
#include "Menu.h"
#include "ZONE_LOADER.h"

#include <algorithm>
#include <atomic>
#include <cfloat>
#include <cmath>
#include <cstdarg>
#include <cstdio>
#include <cstring>
#include <mutex>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <psapi.h>
#include <dxgi1_4.h>

#include <imgui.h>

/*
	The Frame tab, the frame-time graph over the game, and the reading once a frame the other Performance views share
	(see PERF_STATS.h).

	The renderer keeps running totals - of the draw calls it makes and the primitives they draw, of the buffers it
	writes and their bytes - and a count of the textures, render targets and depth buffers it holds; nothing in the
	shipped game reads them. Read as each frame is presented, one frame's numbers are the difference from the last.
	The writes to them are not locked against each other (shadow maps are recorded on other threads), so they can be a
	few short.

	The other numbers are read from where the renderer keeps them: the views it renders and what each will draw (the main
	view's list is sorted by material priority, with where each priority starts), the free slots of its GPU resource
	pools, the lights the last view gathered, the shadow-casting light budget and the effects' instance counts.

	Addresses and layouts are the Steam retail AI.exe's (see GameAddresses.h for the GOG and Epic Games Store builds).
*/
namespace
{
	// The renderer's running totals: the textures, render targets and depth buffers it holds now, then draw calls,
	// primitives drawn, buffers written and the bytes written (each counting up from the start of the game)
	constexpr uintptr_t kGpuResources = 0x017E1BB4;
	constexpr uintptr_t kDrawCalls = 0x017E1BB8;
	constexpr uintptr_t kPrimitives = 0x017E1BBC;
	constexpr uintptr_t kBufferUpdates = 0x017E1BC0;
	constexpr uintptr_t kBufferBytes = 0x017E1BC4;

	// The renderer's device (a pointer): its pools of GPU resources each end with how many of their slots are free (16
	// bits); a pool that is full makes the next resource of its kind fail to be made
	constexpr uintptr_t kGfxDevice = 0x017DF5CC;
	struct Pool { const char* name; uint32_t freeSlots; uint32_t capacity; };
	const Pool kPools[] = {
		{ "Textures", 0x1CD23C, 4096 },
		{ "Render targets", 0x1DF240, 4096 },
		{ "Depth buffers", 0x1DF5C4, 64 },
		{ "Buffers", 0x1B5234, 64000 },
		{ "Resource views", 0x1BF238, 4096 },
		{ "Vertex shaders", 0x1761C, 4096 },
		{ "Pixel shaders", 0x1D620, 4096 },
		{ "Compute shaders", 0x1EE30, 256 },
		{ "Vertex formats", 0x11618, 256 },
		{ "Queries", 0x1E73D8, 2048 },
		{ "Sampler states", 0x1DF8C8, 128 },
		{ "Blend states", 0x1E00D0, 128 },
	};
	constexpr int kPoolCount = sizeof(kPools) / sizeof(kPools[0]);

	// The views the renderer keeps (from ZONE_LOADER's view manager): up to 32 views, how many, and the main one
	constexpr uint32_t kViews = 0x08;
	constexpr uint32_t kViewCount = 0x88;
	constexpr uint32_t kMainView = 0x8C;
	constexpr int kMaxViews = 32;
	// A view: what kind it is, its list of what to draw (how many, the items, room for how many, then where each material
	// priority starts in it - sorted highest priority first), its size and its name; and a byte of flags (switched on,
	// rendered this frame, the main camera's)
	constexpr uint32_t kViewType = 0xE8;
	constexpr uint32_t kViewListCount = 0xEC;
	constexpr uint32_t kViewListSize = 0xF4;
	constexpr uint32_t kViewPriorityStarts = 0xFE;
	constexpr uint32_t kViewScale = 0x318;
	constexpr uint32_t kViewWidth = 0x33C;
	constexpr uint32_t kViewHeight = 0x340;
	constexpr uint32_t kViewName = 0x34B;
	constexpr uint32_t kViewNameLength = 0x20;
	constexpr uint32_t kViewFlags = 0x36B;
	constexpr uint8_t kViewEnabled = 0x01;
	constexpr uint8_t kViewRenderThisFrame = 0x10;
	constexpr uint8_t kViewIsMain = 0x80;

	// In the engine: the deferred lighting (a pointer, in the material database): the shadow-casting lights the last view
	// gathered (of 64) and the others (of 2048)
	constexpr uint32_t kEngineDeferredLighting = 0x17F8;
	constexpr uint32_t kShadowLights = 0x504;
	constexpr uint32_t kOtherLights = 0xA508;
	constexpr uint32_t kMaxShadowLights = 64;
	constexpr uint32_t kMaxOtherLights = 2048;

	// The shadow-casting spotlights: how many may cast shadows a frame, the shadow map size, how many were chosen this
	// frame, and how many more wanted to but were left without (kept up to 16)
	constexpr uintptr_t kShadowManager = 0x012AEDB0;
	constexpr uint32_t kShadowBudget = 0x08;
	constexpr uint32_t kShadowResolution = 0x0C;
	constexpr uint32_t kShadowChosen = 0x30;
	constexpr uint32_t kShadowLeftOut = 0x1874;

	// The effects (a pointer to them): ribbons (of 40) and particle systems (of 200) running, the shared vertex buffer's
	// bytes written this frame and its size, ribbon and particle vertices this frame, the particle buffer written this
	// frame (in 4-byte units, of 0x2D000), and the GPU particles' two queues of spawn requests (of 64 each)
	constexpr uintptr_t kEffectsPointer = 0x0124FED4;
	constexpr uint32_t kRibbons = 0x4530;
	constexpr uint32_t kParticleSystems = 0x4534;
	constexpr uint32_t kVertexBytes = 0xA010;
	constexpr uint32_t kVertexBytesMax = 0xA014;
	constexpr uint32_t kRibbonVertices = 0xA020;
	constexpr uint32_t kParticleVertices = 0xA024;
	constexpr uint32_t kParticleBuffer = 0x172030;
	constexpr uint32_t kParticleBufferMax = 0x2D000;
	constexpr uint32_t kSpawnQueue1 = 0x2014;
	constexpr uint32_t kSpawnQueue2 = 0x4024;
	constexpr uint32_t kMaxRibbons = 40;
	constexpr uint32_t kMaxParticleSystems = 200;
	constexpr uint32_t kMaxSpawnRequests = 64;

	// ---- Switches ----

	std::atomic<bool> g_graph = false;     // the frame-time graph over the game

	// ---- The reading once a frame ----

	struct Addresses
	{
		bool resolved = false;
		uintptr_t gpuResources = 0, drawCalls = 0, primitives = 0, bufferUpdates = 0, bufferBytes = 0;
		uintptr_t device = 0, shadowManager = 0, effects = 0;
	};
	Addresses g_at;

	const Addresses& At()
	{
		if (!g_at.resolved)
		{
			g_at.resolved = true;
			g_at.gpuResources = DEVTOOLS_RELATIVE_ADDRESS(kGpuResources);
			g_at.drawCalls = DEVTOOLS_RELATIVE_ADDRESS(kDrawCalls);
			g_at.primitives = DEVTOOLS_RELATIVE_ADDRESS(kPrimitives);
			g_at.bufferUpdates = DEVTOOLS_RELATIVE_ADDRESS(kBufferUpdates);
			g_at.bufferBytes = DEVTOOLS_RELATIVE_ADDRESS(kBufferBytes);
			g_at.device = DEVTOOLS_RELATIVE_ADDRESS(kGfxDevice);
			g_at.shadowManager = DEVTOOLS_RELATIVE_ADDRESS(kShadowManager);
			g_at.effects = DEVTOOLS_RELATIVE_ADDRESS(kEffectsPointer);
		}
		return g_at;
	}

	struct Totals { uint32_t gpuResources, drawCalls, primitives, bufferUpdates, bufferBytes; };

	bool ReadTotals(const Addresses& at, Totals& out)
	{
		if (!at.gpuResources || !at.drawCalls || !at.primitives || !at.bufferUpdates || !at.bufferBytes)
			return false;
		__try
		{
			out.gpuResources = *reinterpret_cast<const volatile uint32_t*>(at.gpuResources);
			out.drawCalls = *reinterpret_cast<const volatile uint32_t*>(at.drawCalls);
			out.primitives = *reinterpret_cast<const volatile uint32_t*>(at.primitives);
			out.bufferUpdates = *reinterpret_cast<const volatile uint32_t*>(at.bufferUpdates);
			out.bufferBytes = *reinterpret_cast<const volatile uint32_t*>(at.bufferBytes);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// The last frames (render thread), newest at g_next - 1
	constexpr int kHistory = 600;
	PERF_STATS::FrameNumbers g_history[kHistory];
	int g_next = 0;
	int g_filled = 0;
	PERF_STATS::FrameNumbers g_last;
	Totals g_lastTotals = {};
	bool g_haveTotals = false;
	LARGE_INTEGER g_lastTick = {};
	double g_ticksPerMs = 0.0;
	std::atomic<bool> g_running = false;
	std::atomic<ULONGLONG> g_wantedAt = 0;  // when the reading was last asked for (any thread)

	void Tick();

	void ReadFrame()
	{
		LARGE_INTEGER now;
		QueryPerformanceCounter(&now);
		if (g_ticksPerMs == 0.0)
		{
			LARGE_INTEGER frequency;
			QueryPerformanceFrequency(&frequency);
			g_ticksPerMs = static_cast<double>(frequency.QuadPart) / 1000.0;
		}
		PERF_STATS::FrameNumbers frame;
		if (g_lastTick.QuadPart)
			frame.frameMs = static_cast<float>(static_cast<double>(now.QuadPart - g_lastTick.QuadPart) / g_ticksPerMs);
		g_lastTick = now;

		Totals totals;
		const bool read = ReadTotals(At(), totals);
		if (read)
		{
			frame.gpuResources = totals.gpuResources;
			if (g_haveTotals)
			{
				frame.have = true;
				frame.drawCalls = totals.drawCalls - g_lastTotals.drawCalls;
				frame.primitives = totals.primitives - g_lastTotals.primitives;
				frame.bufferUpdates = totals.bufferUpdates - g_lastTotals.bufferUpdates;
				frame.bufferBytes = totals.bufferBytes - g_lastTotals.bufferBytes;
			}
			g_lastTotals = totals;
		}
		g_haveTotals = read;
		g_last = frame;
		if (frame.frameMs > 0.0f)
		{
			g_history[g_next] = frame;
			g_next = (g_next + 1) % kHistory;
			g_filled = (std::min)(g_filled + 1, kHistory);
		}
	}

	// Asked for in the last few seconds (the time asked read first, so a newer one can't make it look long ago)
	bool Wanted()
	{
		const ULONGLONG wantedAt = g_wantedAt;
		return GetTickCount64() - wantedAt <= 5000;
	}

	// Render thread, at the start of each frame's overlay: queued again for the next frame, until nothing has asked for
	// it for a few seconds and the timing has nothing patched or in flight (asked for again just as it stopped: carried
	// on)
	void Tick()
	{
		ReadFrame();
		PERF_TIMING::OnFrame();
		if (!Wanted() && !PERF_TIMING::Busy())
		{
			g_running = false;
			if (!Wanted() || g_running.exchange(true))
				return;
		}
		DEBUG_MENU::OnRenderThread(Tick);
	}

	// ---- The renderer's views, lists and limits ----

	struct View
	{
		char name[kViewNameLength + 1];
		int type;
		bool enabled, renderThisFrame, main;
		uint32_t items, room, width, height;
		float scale;
	};

	struct Views
	{
		bool have = false;
		int count = 0;
		View views[kMaxViews] = {};
		bool haveMainList = false;
		uint32_t mainItems = 0;
		uint32_t mainRoom = 0;
		uint16_t priorityStarts[256] = {};
	};

	bool ReadViews(Views& out)
	{
		__try
		{
			const uint8_t* manager = ZONE_LOADER::view_manager_instance ? static_cast<const uint8_t*>(*ZONE_LOADER::view_manager_instance) : nullptr;
			if (!manager)
				return false;
			const uint8_t* main = *reinterpret_cast<const uint8_t* const*>(manager + kMainView);
			const int count = (std::min)(static_cast<int>(manager[kViewCount]), kMaxViews);
			for (int i = 0; i < count; i++)
			{
				const uint8_t* view = *reinterpret_cast<const uint8_t* const*>(manager + kViews + i * 4);
				if (!view)
					continue;
				View& entry = out.views[out.count++];
				memcpy(entry.name, view + kViewName, kViewNameLength);
				entry.name[kViewNameLength] = '\0';
				entry.type = *reinterpret_cast<const int*>(view + kViewType);
				const uint8_t flags = view[kViewFlags];
				entry.enabled = (flags & kViewEnabled) != 0;
				entry.renderThisFrame = (flags & kViewRenderThisFrame) != 0;
				entry.main = view == main || (flags & kViewIsMain) != 0;
				entry.items = *reinterpret_cast<const uint32_t*>(view + kViewListCount);
				entry.room = *reinterpret_cast<const uint32_t*>(view + kViewListSize);
				entry.width = *reinterpret_cast<const uint32_t*>(view + kViewWidth);
				entry.height = *reinterpret_cast<const uint32_t*>(view + kViewHeight);
				entry.scale = *reinterpret_cast<const float*>(view + kViewScale);
			}
			if (main)
			{
				out.mainItems = *reinterpret_cast<const uint32_t*>(main + kViewListCount);
				out.mainRoom = *reinterpret_cast<const uint32_t*>(main + kViewListSize);
				memcpy(out.priorityStarts, main + kViewPriorityStarts, sizeof(out.priorityStarts));
				out.haveMainList = true;
			}
			out.have = true;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// How many of the main view's items have each priority, from where each starts: highest first, so a priority's items
	// run from its start to the start of the next lower one (or the end); false when the starts don't make sense (the
	// list being rebuilt as it was read)
	bool PriorityCounts(const Views& views, uint32_t counts[256])
	{
		if (!views.haveMainList || views.mainItems > views.mainRoom || views.mainItems > 1000000)
			return false;
		for (int p = 0; p < 256; p++)
		{
			const uint32_t start = views.priorityStarts[p];
			const uint32_t end = p == 0 ? views.mainItems : views.priorityStarts[p - 1];
			if (start > end || end > views.mainItems)
				return false;
			counts[p] = end - start;
		}
		return true;
	}

	// What a priority holds: the material families measured in the shipped levels, and which passes draw what
	const char* PriorityName(int priority)
	{
		switch (priority)
		{
		case 70: return "solid: environment and characters";
		case 59: return "skin and eyes";
		case 58: return "hair";
		case 56: return "solid decals";
		case 52: return "decals and effect overlays";
		case 47: return "decals on normals and shine";
		case 45: return "lights";
		case 39: return "particles, ribbons and fog (transparent)";
		case 31: return "first-person overlay";
		case 29: return "visor";
		case 10: return "refraction (glass)";
		default: return priority >= 49 && priority <= 55 ? "decals" : "";
		}
	}

	struct Limits
	{
		bool havePools = false;
		uint32_t used[kPoolCount] = {};
		bool haveLights = false;
		uint32_t shadowLights = 0, otherLights = 0;
		bool haveShadows = false;
		uint32_t shadowBudget = 0, shadowResolution = 0, shadowChosen = 0, shadowLeftOut = 0;
		bool haveEffects = false;
		uint32_t ribbons = 0, particleSystems = 0, vertexBytes = 0, vertexBytesMax = 0, ribbonVertices = 0, particleVertices = 0;
		uint32_t particleBuffer = 0, spawnQueue = 0;
	};

	void ReadPools(uintptr_t deviceAt, Limits& out)
	{
		__try
		{
			const uint8_t* device = deviceAt ? *reinterpret_cast<const uint8_t* const*>(deviceAt) : nullptr;
			if (!device)
				return;
			for (int i = 0; i < kPoolCount; i++)
			{
				const uint32_t free = *reinterpret_cast<const uint16_t*>(device + kPools[i].freeSlots);
				out.used[i] = free <= kPools[i].capacity ? kPools[i].capacity - free : 0;
			}
			out.havePools = true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			out.havePools = false;
		}
	}

	void ReadLights(Limits& out)
	{
		__try
		{
			const uint8_t* lighting = DEBUG_MARKER::engine ? *reinterpret_cast<const uint8_t* const*>(DEBUG_MARKER::engine + kEngineDeferredLighting) : nullptr;
			if (!lighting)
				return;
			out.shadowLights = *reinterpret_cast<const uint32_t*>(lighting + kShadowLights);
			out.otherLights = *reinterpret_cast<const uint32_t*>(lighting + kOtherLights);
			out.haveLights = out.shadowLights <= kMaxShadowLights && out.otherLights <= kMaxOtherLights;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			out.haveLights = false;
		}
	}

	void ReadShadows(uintptr_t managerAt, Limits& out)
	{
		if (!managerAt)
			return;
		__try
		{
			const uint8_t* manager = reinterpret_cast<const uint8_t*>(managerAt);
			out.shadowBudget = *reinterpret_cast<const uint32_t*>(manager + kShadowBudget);
			out.shadowResolution = *reinterpret_cast<const uint32_t*>(manager + kShadowResolution);
			out.shadowChosen = *reinterpret_cast<const uint32_t*>(manager + kShadowChosen);
			out.shadowLeftOut = *reinterpret_cast<const uint32_t*>(manager + kShadowLeftOut);
			// Made the first time the game asks for it: nothing yet until then
			out.haveShadows = out.shadowBudget > 0 && out.shadowBudget <= 64;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			out.haveShadows = false;
		}
	}

	void ReadEffects(uintptr_t pointerAt, Limits& out)
	{
		if (!pointerAt)
			return;
		__try
		{
			const uint8_t* effects = *reinterpret_cast<const uint8_t* const*>(pointerAt);
			if (!effects)
				return;
			out.ribbons = *reinterpret_cast<const uint32_t*>(effects + kRibbons);
			out.particleSystems = *reinterpret_cast<const uint32_t*>(effects + kParticleSystems);
			out.vertexBytes = *reinterpret_cast<const uint32_t*>(effects + kVertexBytes);
			out.vertexBytesMax = *reinterpret_cast<const uint32_t*>(effects + kVertexBytesMax);
			out.ribbonVertices = *reinterpret_cast<const uint32_t*>(effects + kRibbonVertices);
			out.particleVertices = *reinterpret_cast<const uint32_t*>(effects + kParticleVertices);
			out.particleBuffer = *reinterpret_cast<const uint32_t*>(effects + kParticleBuffer);
			out.spawnQueue = (std::max)(*reinterpret_cast<const uint32_t*>(effects + kSpawnQueue1), *reinterpret_cast<const uint32_t*>(effects + kSpawnQueue2));
			out.haveEffects = out.ribbons <= kMaxRibbons && out.particleSystems <= kMaxParticleSystems;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			out.haveEffects = false;
		}
	}

	Limits ReadLimits()
	{
		const Addresses& at = At();
		Limits limits;
		ReadPools(at.device, limits);
		ReadLights(limits);
		ReadShadows(at.shadowManager, limits);
		ReadEffects(at.effects, limits);
		return limits;
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

	float Percent(uint32_t used, uint32_t of)
	{
		return of ? 100.0f * static_cast<float>(used) / static_cast<float>(of) : 0.0f;
	}

	// The frame times shown: the average, the slowest and the 1% slowest frames over the history
	struct FrameSummary
	{
		int frames = 0;
		float average = 0.0f, fastest = 0.0f, slowest = 0.0f, slowestPercent = 0.0f;
	};

	FrameSummary Summarise(const float* times, int count)
	{
		FrameSummary summary;
		if (count <= 0)
			return summary;
		std::vector<float> sorted(times, times + count);
		std::sort(sorted.begin(), sorted.end());
		double total = 0.0;
		for (float time : sorted)
			total += time;
		summary.frames = count;
		summary.average = static_cast<float>(total / count);
		summary.fastest = sorted.front();
		summary.slowest = sorted.back();
		summary.slowestPercent = sorted[static_cast<size_t>((count - 1) * 0.99f)];
		return summary;
	}

	std::string FrameText()
	{
		const PERF_STATS::FrameNumbers& last = PERF_STATS::LastFrame();
		const PERF_STATS::FrameNumbers average = PERF_STATS::Average();
		float times[kHistory];
		const int count = PERF_STATS::FrameTimes(times, kHistory);
		const FrameSummary summary = Summarise(times, count);
		std::string text;
		text += Format("fps=%.1f\n", average.frameMs > 0.0f ? 1000.0f / average.frameMs : 0.0f);
		text += Format("frame_ms=%.2f\n", average.frameMs);
		text += Format("frame_ms_last=%.2f\n", last.frameMs);
		text += Format("frame_ms_slowest=%.2f\n", summary.slowest);
		text += Format("frame_ms_1pct=%.2f\n", summary.slowestPercent);
		text += Format("frames=%d\n", summary.frames);
		text += Format("counted=%d\n", last.have ? 1 : 0);
		text += Format("draw_calls=%u\n", last.drawCalls);
		text += Format("draw_calls_avg=%u\n", average.drawCalls);
		text += Format("primitives=%u\n", last.primitives);
		text += Format("primitives_avg=%u\n", average.primitives);
		text += Format("buffer_writes=%u\n", last.bufferUpdates);
		text += Format("buffer_bytes=%u\n", last.bufferBytes);
		text += Format("gpu_resources=%u\n", last.gpuResources);
		float gpu = 0.0f;
		if (PERF_TIMING::GpuFrameMs(gpu))
			text += Format("gpu_ms=%.2f\n", gpu);
		return text;
	}

	std::string ViewsText(bool listOnly)
	{
		Views views;
		if (!ReadViews(views))
			return "no views (no level running)";
		std::string text;
		if (listOnly)
		{
			uint32_t counts[256];
			if (!PriorityCounts(views, counts))
				return "the main view's list could not be read (being rebuilt?) - try again";
			text += Format("items=%u\nroom=%u\n", views.mainItems, views.mainRoom);
			for (int p = 255; p >= 0; p--)
				if (counts[p])
					text += Format("priority=%d items=%u %s\n", p, counts[p], PriorityName(p));
			return text;
		}
		text += Format("views=%d of %d\n", views.count, kMaxViews);
		for (int i = 0; i < views.count; i++)
		{
			const View& view = views.views[i];
			text += Format("%s%s enabled=%d this_frame=%d items=%u room=%u size=%ux%u type=%d\n", view.name[0] ? view.name : "(no name)", view.main ? " (main)" : "",
				view.enabled ? 1 : 0, view.renderThisFrame ? 1 : 0, view.items, view.room, view.width, view.height, view.type);
		}
		return text;
	}

	std::string LimitsText(bool effectsOnly)
	{
		const Limits limits = ReadLimits();
		std::string text;
		if (!effectsOnly)
		{
			if (limits.havePools)
				for (int i = 0; i < kPoolCount; i++)
					text += Format("pool.%s=%u/%u\n", kPools[i].name, limits.used[i], kPools[i].capacity);
			else
				text += "pools=unavailable\n";
			if (limits.haveLights)
				text += Format("lights.shadowed=%u/%u\nlights.other=%u/%u\n", limits.shadowLights, kMaxShadowLights, limits.otherLights, kMaxOtherLights);
			else
				text += "lights=unavailable\n";
			if (limits.haveShadows)
				text += Format("shadows.chosen=%u/%u\nshadows.left_out=%u\nshadows.resolution=%u\n", limits.shadowChosen, limits.shadowBudget, limits.shadowLeftOut, limits.shadowResolution);
			else
				text += "shadows=unavailable\n";
			Views views;
			if (ReadViews(views))
				text += Format("views=%d/%d\n", views.count, kMaxViews);
		}
		if (limits.haveEffects)
		{
			text += Format("effects.particle_systems=%u/%u\n", limits.particleSystems, kMaxParticleSystems);
			text += Format("effects.ribbons=%u/%u\n", limits.ribbons, kMaxRibbons);
			text += Format("effects.particle_vertices=%u\neffects.ribbon_vertices=%u\n", limits.particleVertices, limits.ribbonVertices);
			text += Format("effects.vertex_buffer_bytes=%u/%u\n", limits.vertexBytes, limits.vertexBytesMax);
			text += Format("effects.particle_buffer=%u/%u\n", limits.particleBuffer, kParticleBufferMax);
			text += Format("effects.gpu_spawn_requests=%u/%u\n", limits.spawnQueue, kMaxSpawnRequests);
		}
		else
			text += "effects=unavailable\n";
		return text;
	}

	// ---- Drawing ----

	ImVec4 UseColour(float percent)
	{
		if (percent >= 95.0f)
			return ImVec4(1.0f, 0.35f, 0.3f, 1.0f);
		if (percent >= 75.0f)
			return ImVec4(1.0f, 0.75f, 0.3f, 1.0f);
		return ImGui::GetStyleColorVec4(ImGuiCol_Text);
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

	// A row of a limits table: what, used of how many, and a bar (the text level with the bar's)
	void LimitRow(const char* name, uint32_t used, uint32_t of, const char* tooltip = nullptr)
	{
		ImGui::TableNextRow();
		ImGui::TableNextColumn();
		ImGui::AlignTextToFramePadding();
		ImGui::TextUnformatted(name);
		if (tooltip)
			Tooltip(tooltip);
		ImGui::TableNextColumn();
		ImGui::AlignTextToFramePadding();
		const float percent = Percent(used, of);
		ImGui::TextColored(UseColour(percent), "%u of %u", used, of);
		ImGui::TableNextColumn();
		char overlay[32];
		snprintf(overlay, sizeof(overlay), "%.0f%%", percent);
		ImGui::PushStyleColor(ImGuiCol_PlotHistogram, percent >= 95.0f ? ImVec4(0.85f, 0.25f, 0.2f, 1.0f) : percent >= 75.0f ? ImVec4(0.85f, 0.6f, 0.2f, 1.0f) : ImVec4(0.3f, 0.6f, 0.35f, 1.0f));
		ImGui::ProgressBar((std::min)(1.0f, percent / 100.0f), ImVec2(-FLT_MIN, 0.0f), overlay);
		ImGui::PopStyleColor();
	}

	void DrawFrameTime()
	{
		float times[kHistory];
		const int count = PERF_STATS::FrameTimes(times, kHistory);
		const FrameSummary summary = Summarise(times, count);
		const PERF_STATS::FrameNumbers average = PERF_STATS::Average();
		if (count == 0)
		{
			PERF_STATS::Note("Measuring...");
			return;
		}
		ImGui::TextWrapped("%.1f fps (%.2f ms a frame, over the last second)", average.frameMs > 0.0f ? 1000.0f / average.frameMs : 0.0f, average.frameMs);
		ImGui::TextWrapped("Last %d frames: fastest %.2f ms, slowest %.2f ms, 1%% slowest %.2f ms", summary.frames, summary.fastest, summary.slowest, summary.slowestPercent);
		float gpu = 0.0f;
		if (PERF_TIMING::GpuFrameMs(gpu))
			ImGui::TextWrapped("GPU: %.2f ms a frame (Timing tab)", gpu);
		// The graph's scale follows the frames shown: the frame time of 120 fps (8.33 ms), else of 60, 30, 15 fps and so on
		// (the rate halving each step), whichever is the first above the slowest frame - so the line uses the graph's height
		float top = 1000.0f / 120.0f;
		while (top < summary.slowest * 1.05f && top < 10000.0f)
			top *= 2.0f;
		char overlay[96];
		if (top < 100.0f)
			snprintf(overlay, sizeof(overlay), "frame time (graph from 0 to %.3g ms, %.3g fps at the top)", top, 1000.0f / top);
		else // whole milliseconds from 133 ms up (a long hitch), never "1.07e+03"
			snprintf(overlay, sizeof(overlay), "frame time (graph from 0 to %.0f ms, %.3g fps at the top)", top, 1000.0f / top);
		ImGui::PlotLines("##frametimes", times, count, 0, overlay, 0.0f, top, ImVec2(-FLT_MIN, 80.0f * DEBUG_MENU::Scale()));
		bool graph = g_graph;
		if (ImGui::Checkbox("Graph over the game", &graph))
			g_graph = graph;
		Tooltip("Draws the last few seconds' frame times over the game whether this window is open or not (perf.graph): under the Info lines in the top left corner, or just to the right of this window while it covers that corner. Its lines mark 60 and 30 fps.");
	}

	void DrawCounts()
	{
		const PERF_STATS::FrameNumbers& last = PERF_STATS::LastFrame();
		const PERF_STATS::FrameNumbers average = PERF_STATS::Average();
		if (!last.have)
		{
			PERF_STATS::Note("Not available on this build of the game, or measuring...");
			return;
		}
		// The highest of each over the frames kept, and the resources held on average over the last second (as Average
		// takes the others; it gives the resources held now)
		PERF_STATS::FrameNumbers highest;
		for (int i = 0; i < g_filled; i++)
		{
			const PERF_STATS::FrameNumbers& frame = g_history[i];
			if (!frame.have)
				continue;
			highest.drawCalls = (std::max)(highest.drawCalls, frame.drawCalls);
			highest.primitives = (std::max)(highest.primitives, frame.primitives);
			highest.bufferUpdates = (std::max)(highest.bufferUpdates, frame.bufferUpdates);
			highest.bufferBytes = (std::max)(highest.bufferBytes, frame.bufferBytes);
			highest.gpuResources = (std::max)(highest.gpuResources, frame.gpuResources);
		}
		double resources = 0.0, ms = 0.0;
		int counted = 0;
		for (int i = 0; i < g_filled && ms < 1000.0; i++)
		{
			const PERF_STATS::FrameNumbers& frame = g_history[(g_next - 1 - i + kHistory) % kHistory];
			ms += frame.frameMs;
			if (!frame.have)
				continue;
			resources += frame.gpuResources;
			counted++;
		}
		const uint32_t averageResources = counted ? static_cast<uint32_t>(resources / counted + 0.5) : last.gpuResources;
		if (!PERF_STATS::BeginTable("counts", 4))
			return;
		char highestLabel[48];
		snprintf(highestLabel, sizeof(highestLabel), "Highest (last %d frames)", g_filled);
		ImGui::TableSetupColumn("", ImGuiTableColumnFlags_WidthFixed | ImGuiTableColumnFlags_NoHide);
		ImGui::TableSetupColumn("Last frame", ImGuiTableColumnFlags_WidthStretch);
		ImGui::TableSetupColumn("Average (last second)", ImGuiTableColumnFlags_WidthStretch);
		ImGui::TableSetupColumn(highestLabel, ImGuiTableColumnFlags_WidthStretch);
		ImGui::TableHeadersRow();
		auto row = [](const char* name, const std::string& a, const std::string& b, const std::string& c, const char* tooltip)
		{
			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(name);
			Tooltip(tooltip);
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(a.c_str());
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(b.c_str());
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(c.c_str());
		};
		row("Draw calls", std::to_string(last.drawCalls), std::to_string(average.drawCalls), std::to_string(highest.drawCalls),
			"Every draw the renderer made in the frame: every view, shadow maps included (shadow maps are recorded on other threads, so this can be a few short).");
		row("Primitives", PERF_STATS::Count(last.primitives), PERF_STATS::Count(average.primitives), PERF_STATS::Count(highest.primitives),
			"Triangles (and lines and points) drawn in the frame.");
		row("Buffer writes", std::to_string(last.bufferUpdates), std::to_string(average.bufferUpdates), std::to_string(highest.bufferUpdates),
			"Vertex, index and constant buffers written by the CPU in the frame.");
		row("Bytes written", PERF_STATS::Bytes(last.bufferBytes), PERF_STATS::Bytes(average.bufferBytes), PERF_STATS::Bytes(highest.bufferBytes),
			"How much those buffer writes moved to the GPU.");
		row("GPU resources held", std::to_string(last.gpuResources), std::to_string(averageResources), std::to_string(highest.gpuResources),
			"Textures, render targets and depth buffers the renderer holds (a count of what it holds at the time, not of what a frame made).");
		PERF_STATS::EndTable();
	}

	void DrawMainList(const Views& views)
	{
		uint32_t counts[256];
		if (!PriorityCounts(views, counts))
		{
			PERF_STATS::Note(views.haveMainList ? "Being rebuilt as it was read..." : "No main view (no level running).");
			return;
		}
		ImGui::TextWrapped("%u items to draw in the main view (room for %u)", views.mainItems, views.mainRoom);
		PERF_STATS::Note("Each model's material priority picks the passes that draw it.");
		if (!PERF_STATS::BeginTable("priorities", 3))
			return;
		ImGui::TableSetupColumn("Priority", ImGuiTableColumnFlags_WidthFixed | ImGuiTableColumnFlags_NoHide);
		ImGui::TableSetupColumn("Items", ImGuiTableColumnFlags_WidthFixed);
		ImGui::TableSetupColumn("Usually", ImGuiTableColumnFlags_WidthStretch);
		ImGui::TableHeadersRow();
		for (int p = 255; p >= 0; p--)
		{
			if (!counts[p])
				continue;
			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			ImGui::Text("%d", p);
			ImGui::TableNextColumn();
			ImGui::Text("%u", counts[p]);
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(PriorityName(p));
		}
		PERF_STATS::EndTable();
	}

	void DrawViews(const Views& views)
	{
		if (!views.have)
		{
			PERF_STATS::Note("No views (no level running).");
			return;
		}
		ImGui::TextWrapped("%d views (the renderer has room for %d)", views.count, kMaxViews);
		if (!PERF_STATS::BeginTable("views", 5))
			return;
		ImGui::TableSetupColumn("View", ImGuiTableColumnFlags_WidthFixed | ImGuiTableColumnFlags_NoHide);
		ImGui::TableSetupColumn("On", ImGuiTableColumnFlags_WidthFixed);
		ImGui::TableSetupColumn("This frame", ImGuiTableColumnFlags_WidthFixed);
		ImGui::TableSetupColumn("Items", ImGuiTableColumnFlags_WidthFixed);
		ImGui::TableSetupColumn("Size", ImGuiTableColumnFlags_WidthStretch);
		ImGui::TableHeadersRow();
		for (int i = 0; i < views.count; i++)
		{
			const View& view = views.views[i];
			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			ImGui::Text("%s%s", view.name[0] ? view.name : "(no name)", view.main ? " (main)" : "");
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(view.enabled ? "yes" : "no");
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(view.renderThisFrame ? "drawn" : "-");
			ImGui::TableNextColumn();
			ImGui::Text("%u", view.items);
			ImGui::TableNextColumn();
			ImGui::Text("%ux%u", view.width, view.height);
		}
		PERF_STATS::EndTable();
	}

	void DrawLimits(const Views& views)
	{
		const Limits limits = ReadLimits();
		// The names and the counts as wide as the widest of them, the bars taking the rest
		if (!PERF_STATS::BeginTable("limits", 3))
			return;
		ImGui::TableSetupColumn("Limit", ImGuiTableColumnFlags_WidthFixed | ImGuiTableColumnFlags_NoHide);
		ImGui::TableSetupColumn("In use", ImGuiTableColumnFlags_WidthFixed | ImGuiTableColumnFlags_NoHide);
		ImGui::TableSetupColumn("", ImGuiTableColumnFlags_WidthStretch);
		if (limits.haveLights)
		{
			LimitRow("Shadowed lights gathered", limits.shadowLights, kMaxShadowLights, "Shadow-casting lights the last view gathered to draw. Lights past this are not drawn.");
			LimitRow("Other lights gathered", limits.otherLights, kMaxOtherLights, "Lights without shadows the last view gathered to draw. Lights past this are not drawn.");
		}
		if (limits.haveShadows)
		{
			LimitRow("Shadow-casting lights", limits.shadowChosen, limits.shadowBudget, "The spotlights chosen to cast shadows this frame, of how many a frame may have. The rest are drawn without shadows.");
			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			ImGui::AlignTextToFramePadding();
			ImGui::TextUnformatted("Shadows left out");
			Tooltip("Spotlights that wanted to cast shadows this frame but were over the budget, so were drawn without (counted up to 16).");
			ImGui::TableNextColumn();
			ImGui::AlignTextToFramePadding();
			ImGui::TextColored(limits.shadowLeftOut ? ImVec4(1.0f, 0.75f, 0.3f, 1.0f) : ImGui::GetStyleColorVec4(ImGuiCol_Text), "%u", limits.shadowLeftOut);
			ImGui::TableNextColumn();
			ImGui::AlignTextToFramePadding();
			ImGui::TextDisabled(limits.shadowLeftOut ? "over the budget this frame" : "none over the budget");
		}
		if (limits.haveEffects)
		{
			LimitRow("Particle systems", limits.particleSystems, kMaxParticleSystems, "Particle systems running. Past this, new ones don't start.");
			LimitRow("Ribbons", limits.ribbons, kMaxRibbons, "Ribbon effects running. Past this, new ones don't start.");
			LimitRow("Effects' vertex buffer", limits.vertexBytes, limits.vertexBytesMax, "Bytes of the vertex buffer the particles and ribbons share, written this frame. When it is full, the rest are not drawn this frame.");
			LimitRow("Particle buffer", limits.particleBuffer, kParticleBufferMax, "The CPU particles' working buffer, used this frame (in 4-byte units). When it is full, emitters stop adding particles this frame.");
			LimitRow("GPU particle spawns queued", limits.spawnQueue, kMaxSpawnRequests, "Requests to spawn GPU particles waiting in the queue.");
		}
		if (views.have)
			LimitRow("Views", static_cast<uint32_t>(views.count), kMaxViews, "Views the renderer keeps: the player's camera, reflections, shadow-casting spotlights...");
		if (limits.havePools)
			for (int i = 0; i < kPoolCount; i++)
			{
				char name[64];
				snprintf(name, sizeof(name), "GPU pool: %s", kPools[i].name);
				LimitRow(name, limits.used[i], kPools[i].capacity, "One of the renderer's fixed pools of GPU resources. When a pool is full, the next one of its kind fails to be made.");
			}
		PERF_STATS::EndTable();
		if (limits.haveShadows)
			PERF_STATS::Note("Each shadow-casting light draws into a shadow map of %ux%u.", limits.shadowResolution, limits.shadowResolution);
		if (limits.haveEffects)
			PERF_STATS::Note("This frame: %s particle vertices, %s ribbon vertices.", PERF_STATS::Count(limits.particleVertices).c_str(), PERF_STATS::Count(limits.ribbonVertices).c_str());
		if (!limits.havePools && !limits.haveLights && !limits.haveEffects)
			PERF_STATS::Note("Not available (no level running, or not on this build of the game).");
	}

	void DrawFrameTab()
	{
		PERF_STATS::EnsureRunning();
		if (ImGui::CollapsingHeader("Frame time", ImGuiTreeNodeFlags_DefaultOpen))
			DrawFrameTime();
		if (ImGui::CollapsingHeader("Renderer counts", ImGuiTreeNodeFlags_DefaultOpen))
			DrawCounts();
		Views views;
		ReadViews(views);
		if (ImGui::CollapsingHeader("Limits", ImGuiTreeNodeFlags_DefaultOpen))
			DrawLimits(views);
		if (ImGui::CollapsingHeader("Main view by material priority"))
			DrawMainList(views);
		if (ImGui::CollapsingHeader("Views"))
			DrawViews(views);
	}

	// The Info lines' panel this frame (see NoteInfoPanel), for the graph to go under
	float g_infoLeft = 0.0f, g_infoBottom = 0.0f;
	int g_infoFrame = -1;

	// The graph over the game: the frame times (up to 50 ms), with lines at 60 and 30 fps. It goes under the Info lines in
	// the top left corner (or in the corner itself while they are off), and beside the window while that is open over it;
	// the game's own HUD keeps to the bottom corners (the health bar bottom left, the item in hand bottom right).
	void DrawGraphOverlay(ImDrawList* drawList)
	{
		if (!g_graph)
			return;
		PERF_STATS::EnsureRunning();
		float times[kHistory];
		const int count = PERF_STATS::FrameTimes(times, 240);
		if (count < 2)
			return;
		const float scale = DEBUG_MENU::Scale();
		const ImVec2 display = ImGui::GetIO().DisplaySize;
		const ImVec2 size(260.0f * scale, 70.0f * scale);
		const float label = 18.0f * scale;    // the line of text over the graph
		const float margin = 10.0f * scale;
		ImVec2 corner(margin, margin + label);
		if (g_infoFrame == ImGui::GetFrameCount())
			corner = ImVec2(g_infoLeft, g_infoBottom + margin + label);
		corner.y = (std::max)(margin + label, (std::min)(corner.y, display.y - size.y - margin));
		ImVec2 windowMin, windowMax;
		if (DEBUG_MENU::WindowRect(windowMin, windowMax) && windowMin.x < corner.x + size.x && windowMax.x > corner.x && windowMin.y < corner.y + size.y && windowMax.y > corner.y - label)
			corner.x = windowMax.x + 12.0f * scale;
		const float top = 50.0f;
		drawList->AddRectFilled(ImVec2(corner.x - 4.0f, corner.y - label), ImVec2(corner.x + size.x + 4.0f, corner.y + size.y + 4.0f), IM_COL32(0, 0, 0, 150), 3.0f);
		const float guides[] = { 1000.0f / 60.0f, 1000.0f / 30.0f };
		const char* guideNames[] = { "60 fps", "30 fps" };
		const float guideSize = 11.0f * scale;
		for (int i = 0; i < 2; i++)
		{
			const float y = corner.y + size.y * (1.0f - guides[i] / top);
			drawList->AddLine(ImVec2(corner.x, y), ImVec2(corner.x + size.x, y), IM_COL32(255, 255, 255, 50));
			const float width = ImGui::GetFont()->CalcTextSizeA(guideSize, FLT_MAX, 0.0f, guideNames[i]).x;
			drawList->AddText(ImGui::GetFont(), guideSize, ImVec2(corner.x + size.x - width - 2.0f, y - guideSize - 1.0f), IM_COL32(255, 255, 255, 110), guideNames[i]);
		}
		std::vector<ImVec2> points;
		points.reserve(count);
		for (int i = 0; i < count; i++)
		{
			const float value = (std::min)(times[i], top);
			points.push_back(ImVec2(corner.x + size.x * static_cast<float>(i) / static_cast<float>(count - 1), corner.y + size.y * (1.0f - value / top)));
		}
		drawList->AddPolyline(points.data(), static_cast<int>(points.size()), IM_COL32(120, 220, 140, 230), 0, 1.5f);
		const PERF_STATS::FrameNumbers average = PERF_STATS::Average();
		char text[96];
		float gpu = 0.0f;
		if (PERF_TIMING::GpuFrameMs(gpu))
			snprintf(text, sizeof(text), "%.1f fps  %.2f ms  GPU %.2f ms", average.frameMs > 0.0f ? 1000.0f / average.frameMs : 0.0f, average.frameMs, gpu);
		else
			snprintf(text, sizeof(text), "%.1f fps  %.2f ms", average.frameMs > 0.0f ? 1000.0f / average.frameMs : 0.0f, average.frameMs);
		drawList->AddText(ImGui::GetFont(), 14.0f * scale, ImVec2(corner.x, corner.y - 16.0f * scale), IM_COL32(235, 235, 235, 255), text);
	}

	// ---- Names for OpenCAGE ----

	DEBUG_MENU::CommandResult OnRender(std::string (*make)())
	{
		PERF_STATS::EnsureRunning();
		return DEBUG_MENU::AnswerOnRenderThread([make]() -> DEBUG_MENU::CommandResult { return { true, make() }; });
	}

	bool Register()
	{
		DEBUG_MENU::AddSwitch("perf.graph", &g_graph);
		DEBUG_MENU::AddCommand("perf.frame", "the last frame's and the last second's numbers, key=value: fps, frame times, draw calls, primitives, buffer writes, GPU resources (and gpu_ms while GPU timing is on)",
			[](const std::string&) { return OnRender([]() { return FrameText(); }); });
		DEBUG_MENU::AddCommand("perf.list_render_list", "the main view's items by material priority, one priority a line",
			[](const std::string&) { return OnRender([]() { return ViewsText(true); }); });
		DEBUG_MENU::AddCommand("perf.list_views", "the renderer's views (name, on, drawn this frame, items, size), one a line",
			[](const std::string&) { return OnRender([]() { return ViewsText(false); }); });
		DEBUG_MENU::AddCommand("perf.limits", "each fixed limit in use, name=used/of: GPU pools, lights, shadow casters, effects, views",
			[](const std::string&) { return OnRender([]() { return LimitsText(false); }); });
		DEBUG_MENU::AddCommand("perf.effects", "particle systems, ribbons and the effects' buffers, name=used/of",
			[](const std::string&) { return OnRender([]() { return LimitsText(true); }); });
		return true;
	}

	const bool g_registered = Register();
	const bool g_added = DEBUG_MENU::AddTab("Frame", 100, DrawFrameTab, "Performance") && DEBUG_MENU::AddOverlay(95, DrawGraphOverlay);
}

void PERF_STATS::EnsureRunning()
{
	g_wantedAt = GetTickCount64();
	if (!g_running.exchange(true))
	{
		// Started again after a gap: the first frame read is not one long frame. Nothing reads these while it is stopped.
		g_lastTick = {};
		g_haveTotals = false;
		DEBUG_MENU::OnRenderThread(Tick);
	}
}

const PERF_STATS::FrameNumbers& PERF_STATS::LastFrame()
{
	return g_last;
}

PERF_STATS::FrameNumbers PERF_STATS::Average()
{
	FrameNumbers average;
	double ms = 0.0, draws = 0.0, primitives = 0.0, updates = 0.0, bytes = 0.0;
	int frames = 0, counted = 0;
	for (int i = 0; i < g_filled && ms < 1000.0; i++)
	{
		const FrameNumbers& frame = g_history[(g_next - 1 - i + kHistory) % kHistory];
		ms += frame.frameMs;
		frames++;
		if (frame.have)
		{
			draws += frame.drawCalls;
			primitives += frame.primitives;
			updates += frame.bufferUpdates;
			bytes += frame.bufferBytes;
			counted++;
		}
	}
	if (frames)
		average.frameMs = static_cast<float>(ms / frames);
	if (counted)
	{
		average.have = true;
		average.drawCalls = static_cast<uint32_t>(draws / counted + 0.5);
		average.primitives = static_cast<uint32_t>(primitives / counted + 0.5);
		average.bufferUpdates = static_cast<uint32_t>(updates / counted + 0.5);
		average.bufferBytes = static_cast<uint32_t>(bytes / counted + 0.5);
	}
	average.gpuResources = g_last.gpuResources;
	return average;
}

int PERF_STATS::FrameTimes(float* out, int max)
{
	const int count = (std::min)(g_filled, max);
	for (int i = 0; i < count; i++)
		out[i] = g_history[(g_next - count + i + kHistory) % kHistory].frameMs;
	return count;
}

namespace
{
	// The process's address space, walked a region at a time: what is free, and the largest free piece
	void WalkAddressSpace(PERF_STATS::ProcessNumbers& out)
	{
		SYSTEM_INFO system;
		GetSystemInfo(&system);
		uintptr_t at = reinterpret_cast<uintptr_t>(system.lpMinimumApplicationAddress);
		const uintptr_t end = reinterpret_cast<uintptr_t>(system.lpMaximumApplicationAddress);
		out.addressTotal = static_cast<uint64_t>(end) + 1;
		out.addressFree = 0;
		out.largestFree = 0;
		out.freePieces = 0;
		MEMORY_BASIC_INFORMATION region;
		while (at < end && VirtualQuery(reinterpret_cast<LPCVOID>(at), &region, sizeof(region)) == sizeof(region))
		{
			if (region.State == MEM_FREE)
			{
				out.addressFree += region.RegionSize;
				out.largestFree = (std::max)(out.largestFree, static_cast<uint64_t>(region.RegionSize));
				out.freePieces++;
			}
			const uintptr_t next = reinterpret_cast<uintptr_t>(region.BaseAddress) + region.RegionSize;
			if (next <= at)
				break;
			at = next;
		}
	}

	// The walk runs on a thread of its own, not in the frame (it takes 10-20 ms with the game's memory in use): one at a
	// time, again five seconds after the last one began; the last walk's figures kept under the lock
	std::mutex g_walkMutex;
	PERF_STATS::ProcessNumbers g_walked;
	bool g_haveWalk = false;
	std::atomic<bool> g_walking = false;
	ULONGLONG g_walkStarted = 0;  // render thread

	DWORD WINAPI WalkWorker(void*)
	{
		PERF_STATS::ProcessNumbers numbers;
		WalkAddressSpace(numbers);
		{
			std::lock_guard<std::mutex> lock(g_walkMutex);
			g_walked.addressTotal = numbers.addressTotal;
			g_walked.addressFree = numbers.addressFree;
			g_walked.largestFree = numbers.largestFree;
			g_walked.freePieces = numbers.freePieces;
			g_haveWalk = true;
		}
		g_walking = false;
		return 0;
	}

	// A walk's thread gets a small stack (64 KB kept for it), so it can still be made when the address space is too broken
	// up for a usual thread's - just when this view matters most (a thread of the pool, wanting the usual stack, may then
	// never start, leaving the walk waiting for good). When not even that can be made, the walk runs here, in the frame.
	void StartWalk()
	{
		const HANDLE thread = CreateThread(nullptr, 64 * 1024, WalkWorker, nullptr, STACK_SIZE_PARAM_IS_A_RESERVATION, nullptr);
		if (thread)
			CloseHandle(thread);
		else
			WalkWorker(nullptr);
	}

	// The graphics card's memory, from the device the game draws with (Windows 10 and later)
	IDXGIAdapter3* g_adapter = nullptr;
	bool g_adapterTried = false;

	void ReadVideoMemory(PERF_STATS::ProcessNumbers& out)
	{
		if (!g_adapterTried)
		{
			g_adapterTried = true;
			IDXGISwapChain* swapChain = Menu::GetSwapChain();
			IDXGIDevice* dxgiDevice = nullptr;
			IDXGIAdapter* adapter = nullptr;
			if (swapChain && SUCCEEDED(swapChain->GetDevice(__uuidof(IDXGIDevice), reinterpret_cast<void**>(&dxgiDevice))) && dxgiDevice)
			{
				if (SUCCEEDED(dxgiDevice->GetAdapter(&adapter)) && adapter)
				{
					adapter->QueryInterface(__uuidof(IDXGIAdapter3), reinterpret_cast<void**>(&g_adapter));
					adapter->Release();
				}
				dxgiDevice->Release();
			}
		}
		DXGI_QUERY_VIDEO_MEMORY_INFO info = {};
		out.haveVideo = g_adapter && SUCCEEDED(g_adapter->QueryVideoMemoryInfo(0, DXGI_MEMORY_SEGMENT_GROUP_LOCAL, &info));
		if (out.haveVideo)
		{
			out.videoUsed = info.CurrentUsage;
			out.videoBudget = info.Budget;
		}
	}

	PERF_STATS::ProcessNumbers g_process;
	ULONGLONG g_processRead = 0;
}

const PERF_STATS::ProcessNumbers& PERF_STATS::Process()
{
	const ULONGLONG now = GetTickCount64();
	if (!g_processRead || now - g_processRead >= 1000)
	{
		g_processRead = now;
		ProcessNumbers numbers;
		PROCESS_MEMORY_COUNTERS_EX counters = {};
		counters.cb = sizeof(counters);
		if (K32GetProcessMemoryInfo(GetCurrentProcess(), reinterpret_cast<PROCESS_MEMORY_COUNTERS*>(&counters), sizeof(counters)))
		{
			numbers.privateBytes = counters.PrivateUsage;
			numbers.workingSet = counters.WorkingSetSize;
		}
		ReadVideoMemory(numbers);
		numbers.have = true;
		g_process = numbers;
	}
	// The address space: the next walk started when one is due, and the last walk's figures, taken at every call (so a
	// walk's result shows the frame it finishes, not up to a second later)
	if (!g_walking && (!g_walkStarted || now - g_walkStarted >= 5000))
	{
		g_walking = true;
		g_walkStarted = now;
		StartWalk();
	}
	{
		std::lock_guard<std::mutex> lock(g_walkMutex);
		g_process.haveAddress = g_haveWalk;
		g_process.addressTotal = g_walked.addressTotal;
		g_process.addressFree = g_walked.addressFree;
		g_process.largestFree = g_walked.largestFree;
		g_process.freePieces = g_walked.freePieces;
	}
	return g_process;
}

std::string PERF_STATS::Bytes(uint64_t bytes)
{
	char text[32];
	if (bytes >= 1024ull * 1024 * 1024)
		snprintf(text, sizeof(text), "%.2f GB", static_cast<double>(bytes) / (1024.0 * 1024.0 * 1024.0));
	else if (bytes >= 1024ull * 1024)
		snprintf(text, sizeof(text), "%.1f MB", static_cast<double>(bytes) / (1024.0 * 1024.0));
	else if (bytes >= 1024ull)
		snprintf(text, sizeof(text), "%.1f KB", static_cast<double>(bytes) / 1024.0);
	else
		snprintf(text, sizeof(text), "%llu B", static_cast<unsigned long long>(bytes));
	return text;
}

std::string PERF_STATS::Count(uint64_t count)
{
	char text[32];
	if (count >= 1000000ull)
		snprintf(text, sizeof(text), "%.2f M", static_cast<double>(count) / 1000000.0);
	else if (count >= 10000ull)
		snprintf(text, sizeof(text), "%.1f K", static_cast<double>(count) / 1000.0);
	else
		snprintf(text, sizeof(text), "%llu", static_cast<unsigned long long>(count));
	return text;
}

void PERF_STATS::Note(const char* format, ...)
{
	char text[1024];
	va_list args;
	va_start(args, format);
	vsnprintf(text, sizeof(text), format, args);
	va_end(args);
	ImGui::PushTextWrapPos(0.0f);
	ImGui::TextDisabled("%s", text);
	ImGui::PopTextWrapPos();
}

void PERF_STATS::NoteInfoPanel(float left, float bottom)
{
	g_infoLeft = left;
	g_infoBottom = bottom;
	g_infoFrame = ImGui::GetFrameCount();
}

namespace
{
	// The gap either side of a table's column dividers: about half a character at the menu's text size, so neighbouring
	// columns' text never runs together
	float CellPaddingX()
	{
		return (std::max)(ImGui::GetStyle().CellPadding.x, ImGui::GetFontSize() * 0.4f);
	}
}

bool PERF_STATS::BeginTable(const char* id, int columns)
{
	ImGui::PushStyleVar(ImGuiStyleVar_CellPadding, ImVec2(CellPaddingX(), ImGui::GetStyle().CellPadding.y));
	if (ImGui::BeginTable(id, columns, ImGuiTableFlags_RowBg | ImGuiTableFlags_BordersInnerV | ImGuiTableFlags_SizingFixedFit))
		return true;
	ImGui::PopStyleVar();
	return false;
}

bool PERF_STATS::BeginScrollingTable(const char* id, int columns, float minWidth, int rows, bool bars, float maxHeight)
{
	const float padding = CellPaddingX();
	const float paddingY = ImGui::GetStyle().CellPadding.y;
	const float scrollbar = ImGui::GetStyle().ScrollbarSize;
	ImGui::PushStyleVar(ImGuiStyleVar_CellPadding, ImVec2(padding, paddingY));
	// As tall as its rows (the header, then rows of text or of bars, with a few pixels to spare), up to maxHeight
	const float rowHeight = (bars ? ImGui::GetFrameHeight() : ImGui::GetTextLineHeight()) + paddingY * 2.0f;
	const float rowsHeight = ImGui::GetTextLineHeight() + paddingY * 2.0f + rowHeight * static_cast<float>((std::max)(rows, 0)) + 4.0f;
	const bool scrollsDown = rowsHeight > maxHeight;
	// The columns are laid out in the room there is (less the scrollbar down its side when it scrolls down), or in what
	// they need at their least when that is more, and then it scrolls sideways
	const float need = minWidth + static_cast<float>(columns) * (padding * 2.0f + 1.0f);
	const float room = ImGui::GetContentRegionAvail().x - (scrollsDown ? scrollbar : 0.0f);
	const bool scrollsAcross = need > room;
	const float height = (scrollsDown ? maxHeight : rowsHeight) + (scrollsAcross ? scrollbar : 0.0f);
	const ImGuiTableFlags flags = ImGuiTableFlags_RowBg | ImGuiTableFlags_BordersInnerV | ImGuiTableFlags_SizingFixedFit | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY;
	if (ImGui::BeginTable(id, columns, flags, ImVec2(0.0f, height), (std::max)(1.0f, (std::max)(need, room))))
		return true;
	ImGui::PopStyleVar();
	return false;
}

void PERF_STATS::EndTable()
{
	ImGui::EndTable();
	ImGui::PopStyleVar();
}

float PERF_STATS::Width(const char* text)
{
	return ImGui::CalcTextSize(text).x;
}

float PERF_STATS::BarWidth()
{
	return ImGui::GetFontSize() * 5.0f;
}

void PERF_STATS::CellText(const char* text)
{
	const float room = ImGui::GetContentRegionAvail().x;
	const char* end = text + strlen(text);
	if (ImGui::CalcTextSize(text, end).x <= room)
	{
		ImGui::TextUnformatted(text, end);
		return;
	}
	// The fewest characters to cut from the front for the rest to fit after "..." (the widths only grow with what is
	// kept, so a halving search finds it)
	const float dots = ImGui::CalcTextSize("...").x;
	size_t low = 0, high = static_cast<size_t>(end - text);
	while (low < high)
	{
		const size_t cut = (low + high) / 2;
		if (dots + ImGui::CalcTextSize(text + cut, end).x <= room)
			high = cut;
		else
			low = cut + 1;
	}
	ImGui::Text("...%s", text + low);
	if (ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
		ImGui::SetTooltip("%s", text);
}

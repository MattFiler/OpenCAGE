#include "COLLISION_INFO.h"
#include "DEBUG_MENU.h"
#include "LIVE_LINK.h"

#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstdarg>
#include <cstdio>
#include <cstring>
#include <map>
#include <mutex>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <intrin.h>

#include <detours.h>
#include <imgui.h>

/*
	Addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).

	Two logs for the debug menu's World > Physics tab, each from a hook that notes what it sees and passes through:

	- Rays: every ray the game's queue of rays works out in a frame - the AI's sight checks, weapons, pickups, the zone and
	  footstep rays, the inspector's own - as the game's task for them finishes the frame's batch. Each ray carries where
	  in the game's code it was asked for (a source file and line, kept with the query for the game's own debugging), so
	  the rays group by what asked for them. The tab lists the last frame's by source with how many hit something, and an
	  overlay draws them (green: clear, red: blocked at the point it hit). Only noted while switched on (or asked for by
	  physics.list_raycasts); the hook then copies a few numbers per ray behind a lock of its own.
	- Collision on and off: every time a script entity turns a row's collision on or off (doors' path blockers, collision
	  barriers, trigger volumes, model references, water...) - the row, which way, when (the game clock), and what kind of
	  entity did it (told by where the call came from). Kept always while the hooks are in (it happens rarely).
*/
namespace
{
	// ---- Hooked game functions ----
	// The task that works out the frame's rays: the requests (an array of pointers) and how many; returns how many it did.
	// A request: its query (start +0, end +0x10, layer +0x2C, the asking source file +0x38 and line +0x3C), its status
	// (+0x50: 1 waiting, 2 nothing hit, 3 hit) and its answer (+0x60: the point hit, the row hit +0x48, how far +0x50).
	typedef uint32_t(__cdecl* t_process_rays)(uint8_t** requests, uint32_t count);
	constexpr uintptr_t kProcessRays = 0x003F2CB0;
	t_process_rays g_processRays = nullptr;

	// Turning a row's collision on or off: the row, and on or off
	typedef void(__cdecl* t_enable_collision)(uint8_t* row, bool enable);
	constexpr uintptr_t kEnableCollision = 0x003FD5E0;
	t_enable_collision g_enableCollision = nullptr;

	constexpr uintptr_t kGameTime = 0x012410A8;

	// What kind of entity turned a row's collision on or off, from where its call came from: the start of the entity's
	// handler (an address known in every build) and the call's return address within it in the Steam build (the handlers
	// are the same code in each build)
	struct Caller
	{
		uintptr_t start;
		uint32_t returnOffset;
		const char* what;
	};
	const Caller kCallers[] = {
		{ 0x000258F0, 0x85, "a camera collision box enabled" },
		{ 0x00025990, 0x86, "a camera collision box disabled" },
		{ 0x000DBAD0, 0x82, "water enabled" },
		{ 0x000DBAD0, 0x9C, "water enabled" },
		{ 0x000DBB90, 0x83, "water disabled" },
		{ 0x000DBB90, 0x9E, "water disabled" },
		{ 0x00143D10, 0x85, "a collision sphere enabled" },
		{ 0x00143DB0, 0x86, "a collision sphere disabled" },
		{ 0x0015FF10, 0x85, "a collision box enabled" },
		{ 0x0015FFB0, 0x86, "a collision box disabled" },
		{ 0x00160570, 0x85, "an interface selection box enabled" },
		{ 0x00160610, 0x86, "an interface selection box disabled" },
		{ 0x00160EB0, 0x8B, "a collision barrier enabled" },
		{ 0x00160F60, 0x8C, "a collision barrier disabled" },
		{ 0x001619D0, 0x85, "a player trigger box enabled" },
		{ 0x00161A70, 0x86, "a player trigger box disabled" },
		{ 0x001621D0, 0x85, "a player use trigger enabled" },
		{ 0x00162270, 0x86, "a player use trigger disabled" },
		{ 0x0016EAE0, 0x85, "an interface selection sphere enabled" },
		{ 0x0016EB80, 0x86, "an interface selection sphere disabled" },
		{ 0x0022F930, 0xCA, "a model reference enabled" },
		{ 0x0022FA20, 0xCB, "a model reference disabled" },
		{ 0x00244D60, 0xF8, "a nav mesh barrier closing (a door)" },
		{ 0x00244EC0, 0xF8, "a nav mesh barrier opening (a door)" },
		{ 0x002452B0, 0x273, "a nav mesh barrier starting up" },
	};
	constexpr uint32_t kCallerReach = 0x400; // how far into a handler a call may be when the exact return address is not met

	// ---- Switches and values ----
	std::atomic<bool> g_rayLog = false;
	std::atomic<bool> g_rayOverlay = false;
	std::atomic<bool> g_rayHold = false;          // keep showing the frame shown now
	std::atomic<bool> g_collisionOverlay = false; // boxes of rows switched in the last few seconds
	std::atomic<float> g_rayLayer = -1.0f;        // only rays of this layer (-1: all)
	std::atomic<float> g_rayDistance = 60.0f;     // only rays starting this near the camera (drawing)
	std::atomic<ULONGLONG> g_rayDemandUntil = 0;  // physics.list_raycasts wants rays noted until then

	// ---- The ray log ----
	struct Ray
	{
		uint32_t batch;
		float start[3];
		float end[3];
		float point[3];
		uint32_t filter;
		const char* file;
		uint32_t line;
		int32_t row;
		int status;
	};
	constexpr size_t kRayRing = 8192;
	std::mutex g_rayMutex;
	std::vector<Ray> g_rays;    // the ring, made the first time a ray is noted
	uint64_t g_rayWritten = 0;  // how many have been noted (the ring holds the last kRayRing)
	uint32_t g_rayBatch = 0;
	std::atomic<bool> g_rayHooked = false;

	// The last frame's rays (render thread)
	std::vector<Ray> g_frameRays;
	uint64_t g_frameMark = 0;

	bool NotingRays()
	{
		return g_rayLog || g_rayOverlay || GetTickCount64() < g_rayDemandUntil.load();
	}

	// Which requests are waiting, before the game works them out (only those are new this batch)
	int WaitingRequests(uint8_t** requests, uint32_t count, uint16_t* out, int max)
	{
		__try
		{
			int found = 0;
			for (uint32_t i = 0; i < count && found < max; i++)
				if (requests[i] && *reinterpret_cast<const int*>(requests[i] + 0x50) == 1)
					out[found++] = static_cast<uint16_t>(i);
			return found;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	bool ReadRequest(const uint8_t* request, Ray& out)
	{
		__try
		{
			memcpy(out.start, request + 0x00, sizeof(out.start));
			memcpy(out.end, request + 0x10, sizeof(out.end));
			out.filter = *reinterpret_cast<const uint32_t*>(request + 0x2C);
			out.file = *reinterpret_cast<const char* const*>(request + 0x38);
			out.line = *reinterpret_cast<const uint32_t*>(request + 0x3C);
			out.status = *reinterpret_cast<const int*>(request + 0x50);
			memcpy(out.point, request + 0x60, sizeof(out.point));
			out.row = *reinterpret_cast<const int32_t*>(request + 0x60 + 0x48);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	uint32_t __cdecl ProcessRays(uint8_t** requests, uint32_t count)
	{
		if (!NotingRays() || !requests)
			return g_processRays(requests, count);
		uint16_t waiting[1024];
		const int waitingCount = WaitingRequests(requests, count, waiting, 1024);
		const uint32_t done = g_processRays(requests, count);
		std::lock_guard<std::mutex> lock(g_rayMutex);
		if (g_rays.empty())
			g_rays.resize(kRayRing);
		const uint32_t batch = ++g_rayBatch;
		for (int i = 0; i < waitingCount; i++)
		{
			Ray ray = {};
			ray.batch = batch;
			if (!ReadRequest(requests[waiting[i]], ray) || ray.status < 2)
				continue;
			g_rays[g_rayWritten % kRayRing] = ray;
			g_rayWritten++;
		}
		return done;
	}

	// ---- The collision on/off log ----
	struct Change
	{
		double gameTime;
		ULONGLONG tick;
		int row;
		bool enable;
		uint32_t flagsBefore;
		uint32_t entity;
		uint32_t instance;
		const char* caller;
		uintptr_t returnOffset; // where the call came from (an offset in AI.exe), for callers not known
	};
	constexpr size_t kChangeRing = 512;
	std::mutex g_changeMutex;
	std::vector<Change> g_changes(kChangeRing);
	uint64_t g_changesWritten = 0;

	double GameTime()
	{
		__try
		{
			const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kGameTime);
			return at ? *reinterpret_cast<const double*>(at) : 0.0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0.0;
		}
	}

	bool ReadChangedRow(const uint8_t* row, uint32_t& flags, uint32_t& entity, uint32_t& instance)
	{
		__try
		{
			flags = *reinterpret_cast<const uint32_t*>(row + 0x00);
			entity = *reinterpret_cast<const uint32_t*>(row + 0x0C);
			instance = *reinterpret_cast<const uint32_t*>(row + 0x10);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	const char* CallerOf(uintptr_t returnAddress)
	{
		const char* nearest = nullptr;
		uintptr_t nearestDistance = kCallerReach;
		for (const Caller& caller : kCallers)
		{
			const uintptr_t start = DEVTOOLS_RELATIVE_ADDRESS(caller.start);
			if (!start)
				continue;
			if (returnAddress == start + caller.returnOffset)
				return caller.what;
			if (returnAddress > start && returnAddress - start < nearestDistance)
			{
				nearestDistance = returnAddress - start;
				nearest = caller.what;
			}
		}
		return nearest;
	}

	void __cdecl EnableCollision(uint8_t* row, bool enable)
	{
		const uintptr_t returnAddress = reinterpret_cast<uintptr_t>(_ReturnAddress());
		Change change = {};
		change.enable = enable;
		change.gameTime = GameTime();
		change.tick = GetTickCount64();
		change.row = COLLISION_INFO::RowIndexOf(row);
		ReadChangedRow(row, change.flagsBefore, change.entity, change.instance);
		change.caller = CallerOf(returnAddress);
		change.returnOffset = returnAddress - DevTools::GameProcess::GetBaseAddress();
		{
			std::lock_guard<std::mutex> lock(g_changeMutex);
			g_changes[g_changesWritten % kChangeRing] = change;
			g_changesWritten++;
		}
		g_enableCollision(row, enable);
	}

	std::vector<Change> RecentChanges(size_t max)
	{
		std::vector<Change> out;
		std::lock_guard<std::mutex> lock(g_changeMutex);
		const uint64_t available = (std::min)(g_changesWritten, static_cast<uint64_t>(kChangeRing));
		for (uint64_t i = 0; i < available && out.size() < max; i++)
			out.push_back(g_changes[(g_changesWritten - 1 - i) % kChangeRing]);
		return out;
	}

	// ---- Hooks ----
	void AttachHooks(bool attach)
	{
		if (attach)
		{
			g_processRays = reinterpret_cast<t_process_rays>(DEVTOOLS_RELATIVE_ADDRESS(kProcessRays));
			g_enableCollision = reinterpret_cast<t_enable_collision>(DEVTOOLS_RELATIVE_ADDRESS(kEnableCollision));
			if (g_processRays)
				DetourAttach(&reinterpret_cast<PVOID&>(g_processRays), reinterpret_cast<PVOID>(&ProcessRays));
			if (g_enableCollision)
				DetourAttach(&reinterpret_cast<PVOID&>(g_enableCollision), reinterpret_cast<PVOID>(&EnableCollision));
			g_rayHooked = g_processRays != nullptr;
			DevTools::Log("Debug menu: physics log hooks %s", g_processRays && g_enableCollision ? "attached" : "not all attached (no address in this build)");
		}
		else
		{
			g_rayHooked = false;
			if (g_processRays)
				DetourDetach(&reinterpret_cast<PVOID&>(g_processRays), reinterpret_cast<PVOID>(&ProcessRays));
			if (g_enableCollision)
				DetourDetach(&reinterpret_cast<PVOID&>(g_enableCollision), reinterpret_cast<PVOID>(&EnableCollision));
		}
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

	// The asking source: the file's last part and the line
	std::string Source(const Ray& ray)
	{
		char file[160] = "";
		if (!COLLISION_INFO::CopyText(ray.file, file, sizeof(file)))
			return "(no source)";
		const char* name = file;
		for (const char* c = file; *c; c++)
			if (*c == '\\' || *c == '/')
				name = c + 1;
		return Format("%s:%u", name, ray.line);
	}

	bool RayWanted(const Ray& ray)
	{
		const int layer = static_cast<int>(std::floor(g_rayLayer.load() + 0.5f));
		return layer < 0 || static_cast<int>(ray.filter & 0x1F) == layer;
	}

	// At each frame's presentation: the rays noted since the last (unless held)
	void TakeFrameRays()
	{
		if (g_rayHold)
			return;
		std::lock_guard<std::mutex> lock(g_rayMutex);
		const uint64_t end = g_rayWritten;
		uint64_t begin = g_frameMark;
		if (end - begin > kRayRing)
			begin = end - kRayRing;
		g_frameMark = end;
		if (end == begin)
			return; // nothing new: keep showing the last frame that had rays (the game may be paused)
		g_frameRays.clear();
		for (uint64_t i = begin; i < end; i++)
			g_frameRays.push_back(g_rays[i % kRayRing]);
	}

	struct SourceCount
	{
		int rays = 0;
		int blocked = 0;
		uint32_t layers = 0;
	};

	std::vector<std::pair<std::string, SourceCount>> BySource(const std::vector<Ray>& rays)
	{
		std::map<std::string, SourceCount> sources;
		for (const Ray& ray : rays)
		{
			if (!RayWanted(ray))
				continue;
			SourceCount& count = sources[Source(ray)];
			count.rays++;
			if (ray.status == 3)
				count.blocked++;
			count.layers |= 1u << (ray.filter & 0x1F);
		}
		std::vector<std::pair<std::string, SourceCount>> sorted(sources.begin(), sources.end());
		std::sort(sorted.begin(), sorted.end(), [](const auto& a, const auto& b) { return a.second.rays > b.second.rays; });
		return sorted;
	}

	std::string LayersText(uint32_t layers)
	{
		std::string text;
		for (int i = 0; i < 32; i++)
			if (layers & (1u << i))
				text += (text.empty() ? "" : ", ") + std::string(COLLISION_INFO::LayerName(i));
		return text;
	}

	// ---- Overlay ----

	void DrawRays(ImDrawList* drawList)
	{
		float camera[3];
		if (!DEBUG_MENU::CameraPosition(camera))
			return;
		const float distance = (std::max)(1.0f, g_rayDistance.load());
		int drawn = 0;
		for (const Ray& ray : g_frameRays)
		{
			if (drawn >= 500)
				break;
			if (!RayWanted(ray))
				continue;
			const float dx = ray.start[0] - camera[0], dy = ray.start[1] - camera[1], dz = ray.start[2] - camera[2];
			if (dx * dx + dy * dy + dz * dz > distance * distance)
				continue;
			drawn++;
			if (ray.status == 3)
			{
				DEBUG_MENU::WorldLine(drawList, ray.start, ray.point, IM_COL32(255, 90, 90, 200), 1.0f);
				DEBUG_MENU::WorldLine(drawList, ray.point, ray.end, IM_COL32(255, 90, 90, 50), 1.0f);
				DEBUG_MENU::WorldCross(drawList, ray.point, 0.05f, IM_COL32(255, 60, 60, 255), 1.5f);
			}
			else
				DEBUG_MENU::WorldLine(drawList, ray.start, ray.end, IM_COL32(90, 255, 120, 140), 1.0f);
		}
	}

	void DrawChanges(ImDrawList* drawList)
	{
		const ULONGLONG now = GetTickCount64();
		for (const Change& change : RecentChanges(64))
		{
			if (now - change.tick > 5000 || change.row < 0)
				continue;
			COLLISION_INFO::Row row;
			COLLISION_INFO::StaticInstance instance;
			float matrix[16], half[3];
			if (!COLLISION_INFO::ReadRow(change.row, row) || !COLLISION_INFO::RowStaticInstance(row, instance, nullptr) ||
				!COLLISION_INFO::StaticInstanceBox(instance, matrix, half))
				continue;
			const int alpha = static_cast<int>(255.0f * (1.0f - static_cast<float>(now - change.tick) / 5000.0f));
			const ImU32 colour = change.enable ? IM_COL32(90, 255, 120, alpha) : IM_COL32(255, 90, 90, alpha);
			DEBUG_MENU::WorldOrientedBox(drawList, matrix, half, colour, 2.0f);
			const std::string text = Format("row %d %s", change.row, change.enable ? "on" : "off");
			DEBUG_MENU::WorldText(drawList, matrix + 12, text.c_str(), colour);
		}
	}

	void DrawOverlay(ImDrawList* drawList)
	{
		TakeFrameRays();
		if (g_rayOverlay)
			DrawRays(drawList);
		if (g_collisionOverlay)
			DrawChanges(drawList);
	}

	// ---- Tab sections ----

	void Checkbox(const char* label, std::atomic<bool>& value)
	{
		bool on = value;
		if (ImGui::Checkbox(label, &on))
			value = on;
	}

	void DrawRaySection()
	{
		ImGui::SeparatorText("Rays the game casts");
		if (!g_rayHooked)
		{
			ImGui::TextDisabled("Not available on this game release.");
			return;
		}
		Checkbox("Note them", g_rayLog);
		ImGui::SameLine();
		Checkbox("Draw them", g_rayOverlay);
		ImGui::SameLine();
		Checkbox("Hold this frame", g_rayHold);
		int layer = static_cast<int>(std::floor(g_rayLayer.load() + 0.5f));
		char preview[64];
		snprintf(preview, sizeof(preview), "%s", layer < 0 ? "all layers" : Format("%d: %s", layer, COLLISION_INFO::LayerName(layer)).c_str());
		ImGui::SetNextItemWidth(220.0f * DEBUG_MENU::Scale());
		if (ImGui::BeginCombo("Layer##rays", preview))
		{
			if (ImGui::Selectable("all layers", layer < 0))
				g_rayLayer = -1.0f;
			for (int i = 0; i < 32; i++)
			{
				char text[64];
				snprintf(text, sizeof(text), "%d: %s", i, COLLISION_INFO::LayerName(i));
				if (ImGui::Selectable(text, i == layer))
					g_rayLayer = static_cast<float>(i);
			}
			ImGui::EndCombo();
		}
		ImGui::SameLine();
		float distance = g_rayDistance;
		ImGui::SetNextItemWidth(160.0f * DEBUG_MENU::Scale());
		if (ImGui::SliderFloat("Drawn within", &distance, 5.0f, 200.0f, "%.0f m"))
			g_rayDistance = distance;
		if (!NotingRays())
		{
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextDisabled("Switch on \"Note them\" to see the rays of each frame, grouped by what asked for them.");
			ImGui::PopTextWrapPos();
			return;
		}
		int blocked = 0;
		for (const Ray& ray : g_frameRays)
			if (ray.status == 3)
				blocked++;
		ImGui::Text("Last frame: %d rays, %d hit something", static_cast<int>(g_frameRays.size()), blocked);
		const auto sources = BySource(g_frameRays);
		if (ImGui::BeginTable("raysources", 4, ImGuiTableFlags_Borders | ImGuiTableFlags_RowBg | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY | ImGuiTableFlags_SizingFixedFit,
			ImVec2(0.0f, 200.0f * DEBUG_MENU::Scale())))
		{
			ImGui::TableSetupScrollFreeze(0, 1);
			ImGui::TableSetupColumn("Asked for by");
			ImGui::TableSetupColumn("Rays");
			ImGui::TableSetupColumn("Hit");
			ImGui::TableSetupColumn("Layers");
			ImGui::TableHeadersRow();
			for (const auto& source : sources)
			{
				ImGui::TableNextRow();
				ImGui::TableNextColumn();
				ImGui::TextUnformatted(source.first.c_str());
				ImGui::TableNextColumn();
				ImGui::Text("%d", source.second.rays);
				ImGui::TableNextColumn();
				ImGui::Text("%d", source.second.blocked);
				ImGui::TableNextColumn();
				ImGui::TextUnformatted(LayersText(source.second.layers).c_str());
			}
			ImGui::EndTable();
		}
	}

	std::string ChangeText(const Change& change)
	{
		COLLISION_INFO::CompositeInstance composite;
		COLLISION_INFO::FindCompositeInstance(change.instance, composite);
		std::string text = Format("%.2f s: row %d %s, entity %08X in %08X (%s), %s", change.gameTime, change.row, change.enable ? "on" : "off", change.entity, change.instance,
			composite.found ? composite.name : "no running instance", COLLISION_INFO::LayerName(change.flagsBefore & 0x1F));
		if (change.caller)
			text += Format(", by %s", change.caller);
		else
			text += Format(", called from AI.exe+%X", static_cast<unsigned>(change.returnOffset));
		return text;
	}

	void DrawCollisionSection()
	{
		ImGui::SeparatorText("Collision turned on and off by script");
		if (!g_enableCollision)
		{
			ImGui::TextDisabled("Not available on this game release.");
			return;
		}
		Checkbox("Show the rows switched in the last 5 seconds", g_collisionOverlay);
		const std::vector<Change> changes = RecentChanges(200);
		if (changes.empty())
		{
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextDisabled("Nothing yet (doors, collision barriers, triggers and models switch their collision as the level plays).");
			ImGui::PopTextWrapPos();
			return;
		}
		ImGui::SameLine();
		if (ImGui::SmallButton("Clear"))
		{
			std::lock_guard<std::mutex> lock(g_changeMutex);
			g_changesWritten = 0;
		}
		if (ImGui::BeginChild("collisionchanges", ImVec2(0.0f, 180.0f * DEBUG_MENU::Scale()), true, ImGuiWindowFlags_HorizontalScrollbar))
		{
			ImGuiListClipper clipper;
			clipper.Begin(static_cast<int>(changes.size()));
			while (clipper.Step())
				for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
				{
					const Change& change = changes[i];
					ImGui::PushStyleColor(ImGuiCol_Text, change.enable ? IM_COL32(150, 255, 160, 255) : IM_COL32(255, 160, 150, 255));
					ImGui::TextUnformatted(ChangeText(change).c_str());
					ImGui::PopStyleColor();
				}
		}
		ImGui::EndChild();
	}

	// ---- Commands ----

	DEBUG_MENU::CommandResult ListRaysCommand(const std::string& argument)
	{
		if (!g_rayHooked)
			return { false, "Not available on this game release" };
		// Rays noted for a moment if nothing notes them now, then the last frame's
		if (!g_rayLog && !g_rayOverlay)
		{
			g_rayDemandUntil = GetTickCount64() + 1500;
			Sleep(300);
		}
		const bool verbose = argument == "all";
		return DEBUG_MENU::AnswerOnRenderThread([verbose]()
		{
			int blocked = 0;
			for (const Ray& ray : g_frameRays)
				if (ray.status == 3)
					blocked++;
			std::string text = Format("rays=%d\nblocked=%d", static_cast<int>(g_frameRays.size()), blocked);
			for (const auto& source : BySource(g_frameRays))
				text += Format("\n%s rays=%d blocked=%d layers=%s", source.first.c_str(), source.second.rays, source.second.blocked, LayersText(source.second.layers).c_str());
			if (verbose)
			{
				int listed = 0;
				for (const Ray& ray : g_frameRays)
				{
					if (!RayWanted(ray) || listed++ >= 300)
						continue;
					text += Format("\nray %s layer %d %s from %.2f,%.2f,%.2f to %.2f,%.2f,%.2f", Source(ray).c_str(), static_cast<int>(ray.filter & 0x1F),
						ray.status == 3 ? "hit" : "clear", ray.start[0], ray.start[1], ray.start[2], ray.end[0], ray.end[1], ray.end[2]);
					if (ray.status == 3)
						text += Format(" at %.2f,%.2f,%.2f row %d", ray.point[0], ray.point[1], ray.point[2], ray.row);
				}
			}
			return DEBUG_MENU::CommandResult{ true, text };
		});
	}

	DEBUG_MENU::CommandResult ListChangesCommand(const std::string&)
	{
		if (!g_enableCollision)
			return { false, "Not available on this game release" };
		return DEBUG_MENU::AnswerOnRenderThread([]()
		{
			const std::vector<Change> changes = RecentChanges(200);
			std::string text = Format("count=%d", static_cast<int>(changes.size()));
			for (const Change& change : changes)
				text += "\n" + ChangeText(change);
			return DEBUG_MENU::CommandResult{ true, text };
		});
	}

	void NameControls()
	{
		DEBUG_MENU::AddSwitch("physics.raycast_log", &g_rayLog);
		DEBUG_MENU::AddSwitch("physics.raycast_overlay", &g_rayOverlay);
		DEBUG_MENU::AddSwitch("physics.raycast_hold", &g_rayHold);
		DEBUG_MENU::AddValue("physics.raycast_layer", &g_rayLayer, -1.0f, 31.0f);
		DEBUG_MENU::AddValue("physics.raycast_distance", &g_rayDistance, 5.0f, 200.0f);
		DEBUG_MENU::AddSwitch("physics.collision_log_overlay", &g_collisionOverlay);
		DEBUG_MENU::AddCommand("physics.list_raycasts", "the last frame's rays grouped by what asked for them, with how many hit (argument: all - every ray too)", ListRaysCommand);
		DEBUG_MENU::AddCommand("physics.list_collision_changes", "rows whose collision script turned on or off, newest first", ListChangesCommand);
		DEBUG_MENU::AddCommand("physics.clear_collision_changes", "empty the log of collision turned on and off", [](const std::string&)
		{
			std::lock_guard<std::mutex> lock(g_changeMutex);
			g_changesWritten = 0;
			return DEBUG_MENU::CommandResult{ true, "Cleared" };
		});
	}

	const bool g_controls = (NameControls(), true);
	const bool g_added = DEBUG_MENU::AddTab("Physics", 131, DrawRaySection, "World") && DEBUG_MENU::AddTab("Physics", 132, DrawCollisionSection, "World") &&
		DEBUG_MENU::AddOverlay(56, DrawOverlay);
	const bool g_hooks = DEBUG_MENU::AddHooks(AttachHooks);
}

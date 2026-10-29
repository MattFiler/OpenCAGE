#include "AI_DEBUG.h"
#include "DEBUG_MENU.h"
#include "LIVE_LINK.h"
#include "PERF_STATS.h"
#include "PERF_TIMING.h"
#include "ZONE_LOADER.h"

#include <algorithm>
#include <atomic>
#include <cfloat>
#include <cmath>
#include <cstdarg>
#include <cstdio>
#include <cstring>
#include <string>
#include <vector>

#include <imgui.h>

/*
	Addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).

	The debug menu's Info tab: lines of information drawn in the top left corner of the game, whether the window is open
	or not - the level, the zone the camera is in, where the player is, how fast they move and their state, the camera,
	the frame rate, the game clock, the nearest alien and the characters, and from the Performance group (see PERF_STATS.h)
	the renderer's draw calls, the GPU's frame time and the process's memory. Each line is a named switch ("info.zone"...),
	so OpenCAGE can set them too.
*/
namespace
{
	constexpr float kPi = 3.14159265f;

	// The game clock: the game time now (a double), the time scale (a double) and the byte that has the scale used
	constexpr uintptr_t kGameTime = 0x012410A8;
	constexpr uint32_t kTimeScale = 0x20;
	constexpr uint32_t kTimeScaled = 0x30;

	// A zone: its state (bits; the lowest is set while it is loaded) and its name (a pointer to text: the Zone entity's)
	constexpr uint32_t kZoneState = 0x08;
	constexpr uint32_t kZoneLoaded = 0x01;
	constexpr uint32_t kZoneName = 0x30;

	struct Line
	{
		const char* name;    // after "info." in its switch's name
		const char* label;
		std::atomic<bool> on;
	};
	Line g_lines[] = {
		{ "level", "Level", false },
		{ "zone", "Zone the camera is in", false },
		{ "zones_loaded", "Zones loaded, by name", false },
		{ "position", "Player position and facing", false },
		{ "speed", "Player speed", false },
		{ "state", "Player health, stance and light", false },
		{ "camera", "Camera position and direction", false },
		{ "frame", "Frame rate", false },
		{ "clock", "Game clock", false },
		{ "alien", "Nearest alien", false },
		{ "characters", "Characters", false },
		{ "draws", "Draw calls and primitives", false },
		{ "gpu", "GPU frame time (with GPU timing on)", false },
		{ "memory", "Memory and address space", false },
	};
	enum LineIndex { Level, Zone, ZonesLoaded, Position, Speed, State, Camera, Frame, Clock, AlienLine, Characters, Draws, Gpu, Memory, kLineCount };
	static_assert(sizeof(g_lines) / sizeof(g_lines[0]) == kLineCount, "a line per index");

	void NameSwitches()
	{
		static bool named = false;
		if (named)
			return;
		named = true;
		for (Line& line : g_lines)
			DEBUG_MENU::AddSwitch(std::string("info.") + line.name, &line.on);
	}

	bool AnyOn()
	{
		for (const Line& line : g_lines)
			if (line.on)
				return true;
		return false;
	}

	// ---- What the lines show ----

	struct ZoneReading
	{
		bool have = false;
		uint32_t id = 0;
		char name[96] = {};
		int zones = 0;           // in the level
		int loaded = 0;
		char loadedNames[1536] = {}; // their names, comma separated (cut short when long)
	};

	void Append(char* text, size_t size, const char* more)
	{
		size_t length = strnlen(text, size);
		for (size_t i = 0; more[i] && length + 1 < size; i++)
			text[length++] = more[i];
		text[length] = '\0';
	}

	// The camera's zone (the main view's), looked up in the zone list for its name, and which zones are loaded
	bool ReadZone(ZoneReading& out)
	{
		__try
		{
			char* views = static_cast<char*>(*ZONE_LOADER::view_manager_instance);
			char* view = views ? *reinterpret_cast<char**>(views + ZONE_LOADER::kMainViewOffset) : nullptr;
			if (!view)
				return false;
			out.id = *reinterpret_cast<uint32_t*>(view + ZONE_LOADER::kViewZoneOffset);
			out.have = out.id != ZONE_LOADER::kNoZone;
			char* zoneManager = static_cast<char*>(*ZONE_LOADER::zone_manager_instance);
			char* zones = zoneManager ? *reinterpret_cast<char**>(zoneManager + ZONE_LOADER::kZoneArrayOffset) : nullptr;
			if (!zones)
				return true;
			const int count = *reinterpret_cast<int*>(zones + ZONE_LOADER::kArrayCountOffset);
			char** allocations = *reinterpret_cast<char***>(zones + ZONE_LOADER::kArrayDataOffset);
			if (!allocations || count <= 0 || count > 4096)
				return true;
			out.zones = count;
			for (int i = 0; i < count; i++)
			{
				char* zone = allocations[i] ? *reinterpret_cast<char**>(allocations[i] + ZONE_LOADER::kAllocationObjectOffset) : nullptr;
				if (!zone)
					continue;
				const char* name = *reinterpret_cast<const char**>(zone + kZoneName);
				if (*reinterpret_cast<uint32_t*>(zone + kZoneState) & kZoneLoaded)
				{
					if (out.loaded++)
						Append(out.loadedNames, sizeof(out.loadedNames), ", ");
					Append(out.loadedNames, sizeof(out.loadedNames), name && name[0] ? name : "(no name)");
				}
				if (*reinterpret_cast<uint32_t*>(zone + ZONE_LOADER::kZoneIdOffset) == out.id && name)
				{
					out.name[0] = '\0';
					Append(out.name, sizeof(out.name), name);
				}
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool ReadClock(double& now, double& scale, bool& scaled)
	{
		__try
		{
			const uint8_t* clock = reinterpret_cast<const uint8_t*>(DEVTOOLS_RELATIVE_ADDRESS(kGameTime));
			now = *reinterpret_cast<const double*>(clock);
			scale = *reinterpret_cast<const double*>(clock + kTimeScale);
			scaled = clock[kTimeScaled] != 0;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// The player's speed: how far they went over the last half second of game time
	struct Sample
	{
		float time;
		float position[3];
	};
	std::vector<Sample> g_samples;
	uint32_t g_samplesFor = 0;

	bool PlayerSpeed(const AI_DEBUG::Snapshot& snapshot, const AI_DEBUG::Character& player, float& horizontal, float& vertical)
	{
		if (player.id != g_samplesFor || (!g_samples.empty() && snapshot.now < g_samples.back().time))
		{
			g_samples.clear();
			g_samplesFor = player.id;
		}
		if (g_samples.empty() || snapshot.now > g_samples.back().time)
			g_samples.push_back({ snapshot.now, { player.position[0], player.position[1], player.position[2] } });
		while (g_samples.size() > 2 && snapshot.now - g_samples[1].time >= 0.5f)
			g_samples.erase(g_samples.begin());
		const Sample& first = g_samples.front();
		const Sample& last = g_samples.back();
		const float seconds = last.time - first.time;
		if (seconds < 0.05f)
			return false;
		const float dx = last.position[0] - first.position[0], dy = last.position[1] - first.position[1], dz = last.position[2] - first.position[2];
		horizontal = std::sqrt(dx * dx + dz * dz) / seconds;
		vertical = dy / seconds;
		return true;
	}

	float Degrees(float radians)
	{
		float degrees = radians * 180.0f / kPi;
		while (degrees < 0.0f)
			degrees += 360.0f;
		while (degrees >= 360.0f)
			degrees -= 360.0f;
		return degrees;
	}

	float Distance(const float a[3], const float b[3])
	{
		const float dx = a[0] - b[0], dy = a[1] - b[1], dz = a[2] - b[2];
		return std::sqrt(dx * dx + dy * dy + dz * dz);
	}

	// The level's name without the build tree it was made in
	std::string ShortLevelName(const std::string& name)
	{
		const size_t levels = name.find("\\LEVELS\\");
		return levels == std::string::npos ? name : name.substr(levels + 8);
	}

	void Add(std::vector<std::string>& lines, const char* format, ...)
	{
		char text[256];
		va_list args;
		va_start(args, format);
		vsnprintf(text, sizeof(text), format, args);
		va_end(args);
		lines.push_back(text);
	}

	std::vector<std::string> Lines()
	{
		std::vector<std::string> lines;
		const AI_DEBUG::Snapshot& snapshot = AI_DEBUG::Read();
		const AI_DEBUG::Character* player = snapshot.player.character >= 0 && snapshot.player.character < static_cast<int>(snapshot.characters.size())
			? &snapshot.characters[snapshot.player.character] : nullptr;

		if (g_lines[Level].on)
		{
			const std::string level = LIVE_LINK::RunningLevelName();
			Add(lines, "Level: %s", level.empty() ? "none running" : ShortLevelName(level).c_str());
		}
		if (g_lines[Zone].on || g_lines[ZonesLoaded].on)
		{
			static ZoneReading zone;
			zone = ZoneReading();
			const bool read = ReadZone(zone);
			if (g_lines[Zone].on)
			{
				if (!read)
					Add(lines, "Zone: could not be read");
				else if (!zone.have)
					Add(lines, "Zone: none; %d of %d zones loaded", zone.loaded, zone.zones);
				else
					Add(lines, "Zone: %s (0x%08X); %d of %d zones loaded", zone.name[0] ? zone.name : "(no name)", zone.id, zone.loaded, zone.zones);
			}
			if (g_lines[ZonesLoaded].on && read)
			{
				// Wrapped at the commas into lines of about a hundred characters
				std::string line = "Loaded: ";
				const std::string names = zone.loadedNames;
				size_t start = 0;
				while (start < names.size())
				{
					size_t end = names.find(", ", start);
					const std::string name = names.substr(start, end == std::string::npos ? std::string::npos : end - start);
					if (line.size() + name.size() > 100 && line.size() > 8)
					{
						lines.push_back(line);
						line = "        ";
					}
					line += name;
					if (end == std::string::npos)
						break;
					line += ", ";
					start = end + 2;
				}
				lines.push_back(line);
			}
		}
		if (g_lines[Position].on)
		{
			if (player)
				Add(lines, "Player: %.2f, %.2f, %.2f facing %.0f deg", player->position[0], player->position[1], player->position[2], Degrees(player->facing));
			else
				Add(lines, "Player: none");
		}
		if (g_lines[Speed].on)
		{
			float horizontal = 0.0f, vertical = 0.0f;
			if (player && PlayerSpeed(snapshot, *player, horizontal, vertical))
				Add(lines, "Speed: %.2f m/s across, %+.2f m/s up", horizontal, vertical);
			else
				Add(lines, "Speed: -");
		}
		if (g_lines[State].on)
		{
			if (player && player->read)
			{
				std::string stance;
				auto add = [&stance](bool on, const char* text) { if (on) { stance += stance.empty() ? "" : ", "; stance += text; } };
				add(player->crouched, "crouched");
				add(snapshot.player.hiding, "hiding");
				add(player->inVent, "in a vent");
				add(player->torch, "torch out");
				add(player->stunned, "stunned");
				const char* light = snapshot.player.haveLightMeter ? AI_DEBUG::LightLevelName(snapshot.player.level) : "no light meter";
				Add(lines, "Health %d of %d; %s; light: %s%s", player->health, player->maxHealth, stance.empty() ? "standing" : stance.c_str(), light,
					snapshot.player.haveLightMeter && !snapshot.player.meterEnabled ? " (meter off)" : "");
			}
			else
				Add(lines, "Player state: -");
		}
		if (g_lines[Camera].on)
		{
			float position[3], forward[3];
			if (DEBUG_MENU::CameraPosition(position) && DEBUG_MENU::CameraForward(forward))
			{
				const float length = std::sqrt(forward[0] * forward[0] + forward[1] * forward[1] + forward[2] * forward[2]);
				const float fy = length > 0.0f ? forward[1] / length : 0.0f;
				Add(lines, "Camera: %.2f, %.2f, %.2f facing %.0f deg, pitch %+.0f deg", position[0], position[1], position[2],
					Degrees(std::atan2(forward[0], forward[2])), std::asin((std::max)(-1.0f, (std::min)(1.0f, fy))) * 180.0f / kPi);
			}
			else
				Add(lines, "Camera: -");
		}
		if (g_lines[Frame].on)
		{
			const float fps = ImGui::GetIO().Framerate;
			Add(lines, "Frame rate: %.1f fps (%.1f ms)", fps, fps > 0.0f ? 1000.0f / fps : 0.0f);
		}
		if (g_lines[Clock].on)
		{
			double now = 0.0, scale = 1.0;
			bool scaled = false;
			if (ReadClock(now, scale, scaled))
			{
				if (scaled && scale != 1.0)
					Add(lines, "Game clock: %.1f s, running at %.2fx", now, scale);
				else
					Add(lines, "Game clock: %.1f s", now);
			}
			else
				Add(lines, "Game clock: -");
		}
		if (g_lines[AlienLine].on)
		{
			const AI_DEBUG::Alien* nearest = nullptr;
			float best = 0.0f;
			for (const AI_DEBUG::Alien& alien : snapshot.aliens)
			{
				if (alien.character < 0 || alien.character >= static_cast<int>(snapshot.characters.size()) || !player)
					continue;
				const float distance = Distance(snapshot.characters[alien.character].position, player->position);
				if (!nearest || distance < best)
				{
					nearest = &alien;
					best = distance;
				}
			}
			if (!nearest)
				Add(lines, "Alien: none");
			else
			{
				const AI_DEBUG::Character& alien = snapshot.characters[nearest->character];
				char menace[64] = "";
				if (nearest->haveGauge)
					snprintf(menace, sizeof(menace), ", menace %.0f%%%s", nearest->gauge * 100.0f, nearest->menacing ? " (menacing)" : "");
				Add(lines, "Alien: %.1f m away, %s%s%s", best, AI_DEBUG::BranchName(alien.branch), alien.backstage ? ", in the vents" : "", menace);
			}
		}
		if (g_lines[Characters].on)
		{
			int alive = 0, dead = 0;
			const AI_DEBUG::Character* nearest = nullptr;
			float best = 0.0f;
			for (const AI_DEBUG::Character& character : snapshot.characters)
			{
				if (character.player)
					continue;
				if (character.dead)
				{
					dead++;
					continue;
				}
				alive++;
				if (!player || character.alien)
					continue;
				const float distance = Distance(character.position, player->position);
				if (!nearest || distance < best)
				{
					nearest = &character;
					best = distance;
				}
			}
			if (nearest)
				Add(lines, "Characters: %d alive, %d dead; nearest %s (%s) %.1f m, %s", alive, dead, nearest->name, DEBUG_MENU::ClassName(nearest->characterClass), best,
					AI_DEBUG::BranchName(nearest->branch));
			else
				Add(lines, "Characters: %d alive, %d dead", alive, dead);
		}
		if (g_lines[Draws].on || g_lines[Gpu].on || g_lines[Memory].on)
			PERF_STATS::EnsureRunning();
		if (g_lines[Draws].on)
		{
			const PERF_STATS::FrameNumbers average = PERF_STATS::Average();
			if (average.have)
				Add(lines, "Draw calls: %u a frame, %s primitives, %u GPU resources held", average.drawCalls, PERF_STATS::Count(average.primitives).c_str(), average.gpuResources);
			else
				Add(lines, "Draw calls: -");
		}
		if (g_lines[Gpu].on)
		{
			float gpu = 0.0f;
			if (PERF_TIMING::GpuFrameMs(gpu))
				Add(lines, "GPU: %.2f ms a frame", gpu);
			else if (PERF_TIMING::GpuTimingOn())
				Add(lines, "GPU: measuring... (timed while a level runs)");
			else
				Add(lines, "GPU: - (turn on GPU timing in Performance > Timing)");
		}
		if (g_lines[Memory].on)
		{
			const PERF_STATS::ProcessNumbers& process = PERF_STATS::Process();
			if (process.have && process.haveAddress)
				Add(lines, "Memory: %s committed; address space %s free of %s, largest free piece %s", PERF_STATS::Bytes(process.privateBytes).c_str(),
					PERF_STATS::Bytes(process.addressFree).c_str(), PERF_STATS::Bytes(process.addressTotal).c_str(), PERF_STATS::Bytes(process.largestFree).c_str());
			else if (process.have)
				Add(lines, "Memory: %s committed; address space reading...", PERF_STATS::Bytes(process.privateBytes).c_str());
			else
				Add(lines, "Memory: -");
		}
		return lines;
	}

	// ---- Drawing ----

	void DrawOverlay(ImDrawList* drawList)
	{
		NameSwitches();
		if (!AnyOn())
			return;
		const std::vector<std::string> lines = Lines();
		if (lines.empty())
			return;
		const float scale = DEBUG_MENU::Scale();
		const float size = 15.0f * scale;
		const float lineHeight = size + 2.0f;
		float width = 0.0f;
		for (const std::string& line : lines)
			width = (std::max)(width, ImGui::GetFont()->CalcTextSizeA(size, FLT_MAX, 0.0f, line.c_str()).x);
		// The top left corner, or just right of the window while it covers that corner
		ImVec2 corner(10.0f * scale, 10.0f * scale);
		ImVec2 windowMin, windowMax;
		if (DEBUG_MENU::WindowRect(windowMin, windowMax) && windowMin.x < corner.x + width && windowMin.y < corner.y + lineHeight * lines.size())
			corner.x = windowMax.x + 12.0f * scale;
		const float bottom = corner.y + lineHeight * lines.size() + 2.0f;
		drawList->AddRectFilled(ImVec2(corner.x - 6.0f, corner.y - 4.0f), ImVec2(corner.x + width + 6.0f, bottom), IM_COL32(0, 0, 0, 150), 3.0f);
		for (size_t i = 0; i < lines.size(); i++)
			drawList->AddText(ImGui::GetFont(), size, ImVec2(corner.x, corner.y + lineHeight * i), IM_COL32(235, 235, 235, 255), lines[i].c_str());
		// The frame graph over the game (Performance > Frame) goes under these lines
		PERF_STATS::NoteInfoPanel(corner.x, bottom);
	}

	void DrawTab()
	{
		NameSwitches();
		ImGui::SeparatorText("Show in the top left corner of the game");
		for (Line& line : g_lines)
		{
			bool value = line.on;
			if (ImGui::Checkbox(line.label, &value))
				line.on = value;
		}
		if (ImGui::Button("All"))
			for (Line& line : g_lines)
				line.on = true;
		ImGui::SameLine();
		if (ImGui::Button("None"))
			for (Line& line : g_lines)
				line.on = false;
		PERF_STATS::Note("Drawn over the game whether this window is open or not; while this window covers the top left corner, the lines move to just right of it. The frame graph from Performance > Frame goes under these lines.");
	}

	const bool g_added = DEBUG_MENU::AddTab("Info", 5, DrawTab, "Overview") && DEBUG_MENU::AddOverlay(90, DrawOverlay);
}

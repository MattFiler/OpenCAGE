#include "AI_STATE.h"
#include "AI_DEBUG.h"
#include "DEBUG_MENU.h"

#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <deque>
#include <map>
#include <mutex>
#include <sstream>
#include <string>
#include <vector>

#include <imgui.h>

/*
	The AI group's State Log tab, and the breadcrumbs drawn over the game.

	- The state-change log (see AI_STATE.h): while switched on, every change the state machines of the characters' bodies
	  log - moving, cover, dying, the weapon, shooting - with the time, the character, the machine, the state it was in (or
	  wanted) and the one it goes to, and the third the game logs with them. Kept to the last 2000 lines.
	- Breadcrumbs, a trail of what each character did where, built from what the debug menu reads every
	  frame: a trail of where it went, and a crumb with a label wherever something changed - its behaviour tree branch, its
	  awareness, alertness or aggression, its mood, the state of its body, its action, the command a script gave it, its
	  health, going into or out of the vents, its death - and, with the log on, each state change logged for it. For the
	  selected character or all of them, kept for a time (60 s unless set), fading with age.

	Everything is read and drawn on the render thread; the log's lines are written on the AI threads, under a lock.
*/
namespace
{
	std::atomic<bool> g_logOn = false;
	std::atomic<bool> g_logSelectedOnly = false;
	std::atomic<bool> g_crumbs = false;
	std::atomic<bool> g_crumbsAll = false;
	std::atomic<float> g_crumbSeconds = 60.0f;

	// ---- Breadcrumbs ----

	enum class Kind { Trail, Branch, Awareness, Mood, Body, Action, Command, Health, Vents, Death, State };

	struct Crumb
	{
		float time = 0.0f;
		float position[3] = {};
		Kind kind = Kind::Trail;
		char text[72] = {};
	};

	// What was seen of a character last frame, to tell what changed
	struct Seen
	{
		bool have = false;
		int branch = -1, awareness = -1, alertness = -1, aggro = -1, mood = -1;
		int body = -1, action = -1, command = -1;
		char commandName[33] = {};
		int health = 0;
		bool backstage = false, dead = false;
		float lastTrail[3] = {};
		float lastTrailTime = -100.0f;
	};

	struct Trail
	{
		std::string name;
		std::deque<Crumb> crumbs;
		Seen seen;
	};

	std::mutex g_crumbsMutex;              // the render thread writes them, commands read them
	std::map<uint32_t, Trail> g_trails;
	float g_lastNow = 0.0f;
	uint32_t g_logSeen = 0;
	constexpr size_t kMaxCrumbs = 600;

	ImU32 KindColour(Kind kind, int alpha)
	{
		switch (kind)
		{
		case Kind::Branch: return IM_COL32(150, 220, 255, alpha);
		case Kind::Awareness: return IM_COL32(255, 220, 120, alpha);
		case Kind::Mood: return IM_COL32(220, 160, 255, alpha);
		case Kind::Body: return IM_COL32(140, 255, 160, alpha);
		case Kind::Action: return IM_COL32(200, 255, 200, alpha);
		case Kind::Command: return IM_COL32(255, 255, 255, alpha);
		case Kind::Health: return IM_COL32(255, 120, 100, alpha);
		case Kind::Vents: return IM_COL32(200, 120, 255, alpha);
		case Kind::Death: return IM_COL32(255, 60, 60, alpha);
		case Kind::State: return IM_COL32(120, 220, 200, alpha);
		default: return IM_COL32(180, 180, 180, alpha);
		}
	}

	const char* KindName(Kind kind)
	{
		switch (kind)
		{
		case Kind::Branch: return "branch";
		case Kind::Awareness: return "awareness";
		case Kind::Mood: return "mood";
		case Kind::Body: return "body";
		case Kind::Action: return "action";
		case Kind::Command: return "command";
		case Kind::Health: return "health";
		case Kind::Vents: return "vents";
		case Kind::Death: return "death";
		case Kind::State: return "state";
		default: return "trail";
		}
	}

	void Add(Trail& trail, float now, const float position[3], Kind kind, const char* format, ...)
	{
		Crumb crumb;
		crumb.time = now;
		memcpy(crumb.position, position, sizeof(crumb.position));
		crumb.kind = kind;
		va_list args;
		va_start(args, format);
		vsnprintf(crumb.text, sizeof(crumb.text), format, args);
		va_end(args);
		trail.crumbs.push_back(crumb);
		while (trail.crumbs.size() > kMaxCrumbs)
			trail.crumbs.pop_front();
	}

	float Distance(const float a[3], const float b[3])
	{
		const float dx = a[0] - b[0], dy = a[1] - b[1], dz = a[2] - b[2];
		return std::sqrt(dx * dx + dy * dy + dz * dz);
	}

	// Render thread, every frame while breadcrumbs are on: what changed for each character since last frame
	void Sample()
	{
		const AI_DEBUG::Snapshot& snapshot = AI_DEBUG::Read();
		const float now = snapshot.now;
		std::lock_guard<std::mutex> lock(g_crumbsMutex);
		// A clock gone back is a new level (or a checkpoint loaded): the old trails go
		if (now + 1.0f < g_lastNow)
			g_trails.clear();
		g_lastNow = now;
		const float keep = (std::max)(5.0f, g_crumbSeconds.load());
		for (auto it = g_trails.begin(); it != g_trails.end();)
		{
			std::deque<Crumb>& crumbs = it->second.crumbs;
			while (!crumbs.empty() && now - crumbs.front().time > keep)
				crumbs.pop_front();
			bool present = false;
			for (const AI_DEBUG::Character& character : snapshot.characters)
				present = present || character.id == it->first;
			it = crumbs.empty() && !present ? g_trails.erase(it) : std::next(it);
		}

		const bool all = g_crumbsAll;
		for (const AI_DEBUG::Character& character : snapshot.characters)
		{
			// Every NPC, or the selected character only (the player only when selected)
			if (!character.read || (character.player && !character.selected) || (!all && !character.selected))
				continue;
			Trail& trail = g_trails[character.id];
			trail.name = character.name;
			Seen& seen = trail.seen;
			AI_STATE::State state;
			const bool haveState = AI_STATE::Read(character.object, state);
			const float* at = character.position;
			if (!seen.have)
			{
				seen.have = true;
				Add(trail, now, at, Kind::Trail, "start");
				memcpy(seen.lastTrail, at, sizeof(seen.lastTrail));
				seen.lastTrailTime = now;
				seen.branch = character.branch;
				seen.awareness = character.awareness;
				seen.alertness = character.alertness;
				seen.aggro = character.aggro;
				seen.mood = character.mood;
				seen.health = character.health;
				seen.backstage = character.backstage;
				seen.dead = character.dead;
				if (haveState)
				{
					seen.body = state.highLevel;
					seen.action = state.action;
					seen.command = state.scripted.current.have ? state.scripted.current.type : -1;
					strcpy_s(seen.commandName, state.scripted.current.name);
				}
				continue;
			}
			if (Distance(at, seen.lastTrail) > 1.0f || (now - seen.lastTrailTime > 2.0f && Distance(at, seen.lastTrail) > 0.1f))
			{
				Add(trail, now, at, Kind::Trail, "");
				memcpy(seen.lastTrail, at, sizeof(seen.lastTrail));
				seen.lastTrailTime = now;
			}
			if (character.dead && !seen.dead)
				Add(trail, now, at, Kind::Death, "Died");
			seen.dead = character.dead;
			if (character.dead)
				continue;
			if (!character.player && character.branch != seen.branch)
				Add(trail, now, at, Kind::Branch, "Branch: %s", AI_DEBUG::BranchName(character.branch));
			if (character.awareness != seen.awareness && character.awareness >= 0)
				Add(trail, now, at, Kind::Awareness, "Awareness: %s", AI_DEBUG::AwarenessName(character.awareness));
			if ((character.alertness != seen.alertness || character.aggro != seen.aggro) && character.alertness >= 0)
				Add(trail, now, at, Kind::Awareness, "%s, %s", AI_DEBUG::AlertnessName(character.alertness), AI_DEBUG::AggroName(character.aggro));
			if (character.mood != seen.mood && character.mood >= 0)
				Add(trail, now, at, Kind::Mood, "Mood: %s", AI_DEBUG::MoodName(character.mood));
			if (character.health < seen.health)
				Add(trail, now, at, Kind::Health, "Hurt: %d -> %d", seen.health, character.health);
			if (character.backstage != seen.backstage)
				Add(trail, now, at, Kind::Vents, character.backstage ? "Into the vents" : "Out of the vents");
			seen.branch = character.branch;
			seen.awareness = character.awareness;
			seen.alertness = character.alertness;
			seen.aggro = character.aggro;
			seen.mood = character.mood;
			seen.health = character.health;
			seen.backstage = character.backstage;
			if (!haveState)
				continue;
			if (state.highLevel != seen.body)
				Add(trail, now, at, Kind::Body, "Body: %s", AI_STATE::HighLevelName(state.highLevel));
			if (state.action != seen.action && state.action >= 0)
				Add(trail, now, at, Kind::Action, "Action: %s", AI_STATE::ActionName(state.action));
			const int command = state.scripted.current.have ? state.scripted.current.type : -1;
			if (command != seen.command || strcmp(seen.commandName, state.scripted.current.name) != 0)
			{
				if (command >= 0)
				{
					const char* name = state.scripted.current.name;
					Add(trail, now, at, Kind::Command, "Command: %s%s%s", name[0] ? name : AI_STATE::CommandTypeName(command), name[0] ? " - " : "", name[0] ? AI_STATE::CommandTypeName(command) : "");
				}
				else
					Add(trail, now, at, Kind::Command, "Command ended");
			}
			seen.body = state.highLevel;
			seen.action = state.action;
			seen.command = command;
			strcpy_s(seen.commandName, state.scripted.current.name);
		}

		// The state changes logged since last frame, each at its character's place now
		if (AI_STATE::StateLogging())
		{
			const std::vector<AI_STATE::LogEntry> entries = AI_STATE::StateLogSince(g_logSeen);
			for (const AI_STATE::LogEntry& entry : entries)
			{
				g_logSeen = (std::max)(g_logSeen, entry.sequence);
				const auto found = g_trails.find(entry.id);
				if (found == g_trails.end())
					continue;
				for (const AI_DEBUG::Character& character : snapshot.characters)
					if (character.id == entry.id)
						Add(found->second, now, character.position, Kind::State, "%s: %s -> %s", entry.machine, entry.from, entry.to);
			}
		}
	}

	void DrawCrumbs(ImDrawList* drawList)
	{
		std::lock_guard<std::mutex> lock(g_crumbsMutex);
		const float now = g_lastNow;
		const float keep = (std::max)(5.0f, g_crumbSeconds.load());
		float camera[3] = {};
		const bool haveCamera = DEBUG_MENU::CameraPosition(camera);
		for (const auto& entry : g_trails)
		{
			const std::deque<Crumb>& crumbs = entry.second.crumbs;
			const Crumb* previous = nullptr;
			int labels = 0;
			// The trail first, oldest to newest, fading with age
			for (const Crumb& crumb : crumbs)
			{
				const float age = (std::min)(1.0f, (std::max)(0.0f, (now - crumb.time) / keep));
				const int alpha = static_cast<int>(40 + 200 * (1.0f - age));
				if (previous)
					DEBUG_MENU::WorldLine(drawList, previous->position, crumb.position, IM_COL32(180, 180, 180, alpha / 2), 1.0f);
				previous = &crumb;
			}
			// The crumbs with something to say, newest first, labelled while near the camera (at most a dozen a character)
			for (auto it = crumbs.rbegin(); it != crumbs.rend(); ++it)
			{
				const Crumb& crumb = *it;
				if (crumb.kind == Kind::Trail)
					continue;
				const float age = (std::min)(1.0f, (std::max)(0.0f, (now - crumb.time) / keep));
				const int alpha = static_cast<int>(60 + 195 * (1.0f - age));
				ImVec2 screen;
				if (!DEBUG_MENU::Project(crumb.position, screen))
					continue;
				drawList->AddCircleFilled(screen, 4.0f, KindColour(crumb.kind, alpha));
				const bool nearCamera = !haveCamera || Distance(camera, crumb.position) < 30.0f;
				if (nearCamera && labels < 12)
				{
					const float above[3] = { crumb.position[0], crumb.position[1] + 0.25f + 0.22f * (labels % 4), crumb.position[2] };
					char text[112];
					snprintf(text, sizeof(text), "%s (%.0f s)", crumb.text, now - crumb.time);
					DEBUG_MENU::WorldText(drawList, above, text, KindColour(crumb.kind, alpha));
					labels++;
				}
			}
		}
	}

	// ---- The tab ----

	char g_logFilter[48] = {};

	void SwitchBox(const char* label, std::atomic<bool>& value)
	{
		bool on = value;
		if (ImGui::Checkbox(label, &on))
			value = on;
	}

	bool Matches(const AI_STATE::LogEntry& entry, const char* filter)
	{
		if (!filter[0])
			return true;
		std::string line = std::string(entry.character) + " " + entry.machine + " " + entry.from + " " + entry.to + " " + entry.third;
		std::string wanted = filter;
		for (char& c : line) c = static_cast<char>(tolower(static_cast<unsigned char>(c)));
		for (char& c : wanted) c = static_cast<char>(tolower(static_cast<unsigned char>(c)));
		return line.find(wanted) != std::string::npos;
	}

	void DrawTab()
	{
		ImGui::SeparatorText("State-change log");
		const int sites = AI_STATE::StateLogSites();
		if (!sites)
		{
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextDisabled("Not available: the game's logging calls were not found in this release of the game.");
			ImGui::PopTextWrapPos();
		}
		else
		{
			SwitchBox("Log state changes", g_logOn);
			ImGui::SameLine();
			SwitchBox("Only the selected character", g_logSelectedOnly);
			ImGui::SameLine();
			if (ImGui::SmallButton("Clear"))
				AI_STATE::ClearStateLog();
			ImGui::SetNextItemWidth(220.0f * DEBUG_MENU::Scale());
			ImGui::InputTextWithHint("##logfilter", "filter", g_logFilter, sizeof(g_logFilter));
			ImGui::SameLine();
			ImGui::TextDisabled("(heard from %d places in the game's code)", sites);
			if (ImGui::IsItemHovered())
				ImGui::SetTooltip("The game still hands a log line to a logger that throws it away from %d places in its characters' state\n"
					"machines (moving, cover, dying, the weapon, shooting); while this is on, each of those lines comes here.", sites);
			const std::vector<AI_STATE::LogEntry> entries = AI_STATE::StateLog();
			const uint32_t selected = DEBUG_MENU::SelectedId();
			int shown = 0;
			for (const AI_STATE::LogEntry& entry : entries)
				if ((!g_logSelectedOnly || entry.id == selected) && Matches(entry, g_logFilter))
					shown++;
			// As tall as its lines (a short box while there are none), up to 13 before it scrolls, with room for the sideways
			// scrollbar; each column as wide as what it holds, so a long change scrolls sideways rather than being cut off
			const ImGuiStyle& style = ImGui::GetStyle();
			const float rows = (std::min)(14.0f, (std::max)(3.0f, static_cast<float>(shown) + 1.0f));
			const float height = ImGui::GetTextLineHeightWithSpacing() * rows + style.ScrollbarSize + style.CellPadding.y * 2.0f;
			if (ImGui::BeginTable("statelog", 5, ImGuiTableFlags_RowBg | ImGuiTableFlags_BordersInnerV | ImGuiTableFlags_ScrollY | ImGuiTableFlags_ScrollX | ImGuiTableFlags_SizingFixedFit,
				ImVec2(0.0f, height)))
			{
				ImGui::TableSetupScrollFreeze(1, 1);
				ImGui::TableSetupColumn("Time");
				ImGui::TableSetupColumn("Character");
				ImGui::TableSetupColumn("Machine");
				ImGui::TableSetupColumn("Change");
				ImGui::TableSetupColumn("With");
				ImGui::TableHeadersRow();
				for (const AI_STATE::LogEntry& entry : entries)
				{
					if ((g_logSelectedOnly && entry.id != selected) || !Matches(entry, g_logFilter))
						continue;
					ImGui::TableNextRow();
					ImGui::TableSetColumnIndex(0);
					ImGui::Text("%.2f", entry.time);
					ImGui::TableSetColumnIndex(1);
					ImGui::TextUnformatted(entry.character);
					ImGui::TableSetColumnIndex(2);
					ImGui::TextUnformatted(entry.machine);
					ImGui::TableSetColumnIndex(3);
					ImGui::Text("%s -> %s", entry.from, entry.to);
					ImGui::TableSetColumnIndex(4);
					ImGui::TextDisabled("%s", entry.third);
				}
				if (shown == 0)
				{
					ImGui::TableNextRow();
					ImGui::TableSetColumnIndex(1);
					ImGui::TextDisabled(!entries.empty() ? "Nothing matches." : g_logOn ? "Nothing logged yet." : "Nothing logged (switch on Log state changes).");
				}
				// Kept scrolled to the newest while it is at the bottom
				if (ImGui::GetScrollY() >= ImGui::GetScrollMaxY())
					ImGui::SetScrollHereY(1.0f);
				ImGui::EndTable();
			}
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextDisabled("\"With\": the state it is in, for a change of what it wants; what it wants, for a change of state.");
			ImGui::PopTextWrapPos();
		}

		ImGui::SeparatorText("Breadcrumbs");
		SwitchBox("Draw breadcrumbs", g_crumbs);
		ImGui::SameLine();
		SwitchBox("For every character", g_crumbsAll);
		float seconds = g_crumbSeconds;
		ImGui::SetNextItemWidth(220.0f * DEBUG_MENU::Scale());
		if (ImGui::SliderFloat("Kept for", &seconds, 10.0f, 600.0f, "%.0f s", ImGuiSliderFlags_Logarithmic))
			g_crumbSeconds = seconds;
		ImGui::SameLine();
		if (ImGui::SmallButton("Clear##crumbs"))
		{
			std::lock_guard<std::mutex> lock(g_crumbsMutex);
			g_trails.clear();
		}
		ImGui::PushTextWrapPos(0.0f);
		ImGui::TextDisabled("Otherwise for the selected character only. A trail of where it went, and a labelled crumb where its branch, "
			"awareness, mood, body state, action, script command or health changed (and each state change, with the log on).");
		ImGui::PopTextWrapPos();
		std::lock_guard<std::mutex> lock(g_crumbsMutex);
		size_t count = 0;
		for (const auto& entry : g_trails)
			count += entry.second.crumbs.size();
		ImGui::Text("%zu crumb(s) for %zu character(s)", count, g_trails.size());
	}

	// ---- Commands, hooks and the overlay ----

	DEBUG_MENU::CommandResult Answer(bool ok, const std::string& message)
	{
		DEBUG_MENU::CommandResult result;
		result.ok = ok;
		result.message = message;
		return result;
	}

	bool Register()
	{
		DEBUG_MENU::AddSwitch("ai.state_log", &g_logOn);
		DEBUG_MENU::AddSwitch("ai.state_log.selected_only", &g_logSelectedOnly);
		DEBUG_MENU::AddSwitch("ai.crumbs", &g_crumbs);
		DEBUG_MENU::AddSwitch("ai.crumbs.all", &g_crumbsAll);
		DEBUG_MENU::AddValue("ai.crumbs.seconds", &g_crumbSeconds, 10.0f, 600.0f);
		DEBUG_MENU::AddCommand("ai.list_state_log", "[n] [id=<unique id>] the last n (50) state changes logged, one a line: time character machine from -> to (with)", [](const std::string& argument)
		{
			std::istringstream stream(argument);
			std::string word;
			size_t count = 50;
			uint32_t id = 0;
			while (stream >> word)
			{
				if (word.compare(0, 3, "id=") == 0)
					id = static_cast<uint32_t>(strtoul(word.c_str() + 3, nullptr, 0));
				else if (atoi(word.c_str()) > 0)
					count = static_cast<size_t>(atoi(word.c_str()));
			}
			if (!AI_STATE::StateLogSites())
				return Answer(false, "the game's logging calls were not found in this release of the game");
			const std::vector<AI_STATE::LogEntry> entries = AI_STATE::StateLog();
			std::vector<const AI_STATE::LogEntry*> chosen;
			for (const AI_STATE::LogEntry& entry : entries)
				if (!id || entry.id == id)
					chosen.push_back(&entry);
			const size_t first = chosen.size() > count ? chosen.size() - count : 0;
			std::string text = std::string("logging=") + (AI_STATE::StateLogging() ? "1" : "0") + " sites=" + std::to_string(AI_STATE::StateLogSites()) + "\n";
			for (size_t i = first; i < chosen.size(); i++)
			{
				char line[256];
				snprintf(line, sizeof(line), "%.2f %s(%u) %s %s -> %s (%s)\n", chosen[i]->time, chosen[i]->character, chosen[i]->id, chosen[i]->machine, chosen[i]->from, chosen[i]->to, chosen[i]->third);
				text += line;
			}
			return Answer(true, text);
		});
		DEBUG_MENU::AddCommand("ai.state_log.clear", "empties the state-change log", [](const std::string&)
		{
			AI_STATE::ClearStateLog();
			return Answer(true, "cleared");
		});
		DEBUG_MENU::AddCommand("ai.list_crumbs", "[id=<unique id>] the breadcrumbs with a label, one a line: character time kind text @x,y,z", [](const std::string& argument)
		{
			uint32_t id = 0;
			const size_t at = argument.find("id=");
			if (at != std::string::npos)
				id = static_cast<uint32_t>(strtoul(argument.c_str() + at + 3, nullptr, 0));
			std::lock_guard<std::mutex> lock(g_crumbsMutex);
			std::string text;
			for (const auto& entry : g_trails)
			{
				if (id && entry.first != id)
					continue;
				for (const Crumb& crumb : entry.second.crumbs)
				{
					if (crumb.kind == Kind::Trail)
						continue;
					char line[256];
					snprintf(line, sizeof(line), "%s(%u) %.2f %s %s @%.2f,%.2f,%.2f\n", entry.second.name.c_str(), entry.first, crumb.time, KindName(crumb.kind), crumb.text,
						crumb.position[0], crumb.position[1], crumb.position[2]);
					text += line;
				}
			}
			return Answer(true, text.empty() ? "none\n" : text);
		});
		DEBUG_MENU::AddCommand("ai.crumbs.clear", "drops every breadcrumb", [](const std::string&)
		{
			std::lock_guard<std::mutex> lock(g_crumbsMutex);
			g_trails.clear();
			return Answer(true, "cleared");
		});
		return true;
	}

	// Render thread, every frame (the window open or not): the names on the first frame, the log switched as asked, and the
	// breadcrumbs gathered and drawn while on
	void Overlay(ImDrawList* drawList)
	{
		static const bool registered = Register();
		(void)registered;
		AI_STATE::SetStateLogging(g_logOn);
		if (!g_crumbs)
			return;
		Sample();
		DrawCrumbs(drawList);
	}

	void AttachHooks(bool attach)
	{
		AI_STATE::PatchStateLog(attach);
	}

	const bool g_tab = DEBUG_MENU::AddTab("State Log", 65, DrawTab, "AI");
	const bool g_overlay = DEBUG_MENU::AddOverlay(15, Overlay);
	const bool g_hooks = DEBUG_MENU::AddHooks(AttachHooks);
}

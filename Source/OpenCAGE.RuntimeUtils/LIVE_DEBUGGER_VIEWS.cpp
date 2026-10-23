#include "LIVE_DEBUGGER.h"
#include "DEBUG_MENU.h"
#include "SCRIPT_DEBUG.h"

#include <cfloat>
#include <cstdio>
#include <mutex>
#include <string>
#include <vector>

#include <imgui.h>

/*
	The Debug Menu's Debugger tab (group Scripts): whether the level's scripts are held, the breakpoint hit last - the
	entity, the method, where it is and the calls that led to it - with Continue and the steps, and every breakpoint set,
	each with its switch and a button to take it away. Breakpoints are set from OpenCAGE's flowgraphs (a node's menu, or
	F9 there); this tab shows what the game has. Every button is a command too (debugger.continue, debugger.step,
	debugger.pause, debugger.enable_breakpoint, debugger.remove_breakpoint, debugger.clear), as is every list
	(debugger.state, debugger.list).
*/
namespace
{
	std::mutex g_statusMutex;
	std::string g_lastAction;

	void Did(const std::string& what)
	{
		std::lock_guard<std::mutex> lock(g_statusMutex);
		g_lastAction = what;
	}

	// A composite by its name (the level's root, named by the path the level was built from, as "the level root (NAME)"),
	// else its id
	std::string CompositeText(uint32_t composite, const std::string& name)
	{
		return name.empty() ? LIVE_DEBUGGER::IdText(composite) : SCRIPT_DEBUG::CompositeText(composite, name.c_str());
	}

	void Tooltip(const char* text)
	{
		if (ImGui::IsItemHovered(ImGuiHoveredFlags_AllowWhenDisabled))
			ImGui::SetTooltip("%s", text);
	}

	// A method as words when its name is known (as the Scripts tabs name it), with its id
	std::string MethodText(uint32_t method)
	{
		const char* name = SCRIPT_DEBUG::MethodName(method);
		return name ? std::string(name) + " (" + LIVE_DEBUGGER::IdText(method) + ")" : LIVE_DEBUGGER::IdText(method);
	}

	// An entity's name as OpenCAGE gave it when it set a breakpoint on it - the part of the breakpoint's label after the
	// composite, without the id OpenCAGE puts after it - or "" (the game itself keeps no entity names)
	std::string EntityName(uint32_t entity, const std::vector<LIVE_DEBUGGER::BreakpointView>& breakpoints)
	{
		for (const LIVE_DEBUGGER::BreakpointView& bp : breakpoints)
		{
			if (bp.entity != entity || bp.label.empty())
				continue;
			std::string name = bp.label;
			const size_t split = name.find(" : ");
			if (split != std::string::npos)
				name = name.substr(split + 3);
			const size_t id = name.rfind(" [");
			if (id != std::string::npos && name.back() == ']')
				name = name.substr(0, id);
			if (!name.empty() && name.front() != '[')
				return name;
		}
		return std::string();
	}

	// An entity as words when its name is known, with its id (as MethodText)
	std::string EntityText(uint32_t entity, const std::vector<LIVE_DEBUGGER::BreakpointView>& breakpoints)
	{
		const std::string name = EntityName(entity, breakpoints);
		return name.empty() ? LIVE_DEBUGGER::IdText(entity) : name + " (" + LIVE_DEBUGGER::IdText(entity) + ")";
	}

	std::string PathText(const std::vector<uint32_t>& path, bool any)
	{
		if (any)
			return "every instance";
		if (path.empty())
			return "the level's root";
		std::string text;
		for (uint32_t step : path)
			text += (text.empty() ? "" : " > ") + LIVE_DEBUGGER::IdText(step);
		return text;
	}

	void DrawHeld()
	{
		const LIVE_DEBUGGER::HitView hit = LIVE_DEBUGGER::Hit();
		// What the scripts are doing, then (wrapped) what holding them does
		if (hit.held)
			ImGui::TextColored(ImVec4(1.0f, 0.55f, 0.35f, 1.0f), hit.atBreakpoint ? "Held at a breakpoint" : hit.steps ? "Held after a step" : "Paused by the debugger");
		else
			ImGui::TextColored(ImVec4(0.55f, 0.85f, 0.55f, 1.0f), "Scripts running");
		ImGui::SameLine();
		ImGui::PushTextWrapPos(0.0f);
		ImGui::TextDisabled(hit.held ? "- the game is paused with its scripts, as for its pause menu" : "- a breakpoint hit, or Pause scripts, pauses the game with them");
		ImGui::PopTextWrapPos();

		ImGui::BeginDisabled(!hit.held);
		if (ImGui::Button("Continue"))
			Did(LIVE_DEBUGGER::Continue());
		Tooltip("The scripts go on from the held call (debugger.continue).");
		ImGui::SameLine();
		if (ImGui::Button("Step"))
			Did(LIVE_DEBUGGER::Step(false));
		Tooltip("Makes the held call and the rest of the calls due at the same moment (one round), then holds again; the next step makes the next round (debugger.step).");
		ImGui::SameLine();
		if (ImGui::Button("Step frame"))
			Did(LIVE_DEBUGGER::Step(true));
		Tooltip("Lets one whole frame of the game run - the scripts and everything else - then holds again (debugger.step frame).");
		ImGui::EndDisabled();
		ImGui::SameLine();
		ImGui::BeginDisabled(hit.held);
		if (ImGui::Button("Pause scripts"))
			Did(LIVE_DEBUGGER::Pause());
		Tooltip("Holds the level's scripts (and the game) now, as a breakpoint does, until Continue (debugger.pause).");
		ImGui::EndDisabled();

		if (hit.sequence == 0)
		{
			ImGui::TextDisabled("No breakpoint has been hit yet.");
			return;
		}
		ImGui::SeparatorText(hit.atBreakpoint ? "The held call" : "The last breakpoint hit");
		const std::vector<LIVE_DEBUGGER::BreakpointView> breakpoints = LIVE_DEBUGGER::Breakpoints();
		// Its lines wrap to the window (a composite's path can be long)
		ImGui::PushTextWrapPos(0.0f);
		ImGui::Text("Method %s on entity %s", MethodText(hit.method).c_str(), EntityText(hit.entity, breakpoints).c_str());
		ImGui::Text("In %s (instance %s)", CompositeText(hit.composite, hit.compositeName).c_str(), LIVE_DEBUGGER::IdText(hit.instance).c_str());
		ImGui::TextDisabled("Path: %s", PathText(hit.path, false).c_str());
		ImGui::TextDisabled("Breakpoint %d, hit %u, due at %.3f s on the scripts' clock, %u step(s) since", hit.breakpoint, hit.sequence, hit.time, hit.steps);
		if (!hit.chain.empty())
		{
			ImGui::TextDisabled("Called because of (latest first):");
			for (const LIVE_DEBUGGER::HitView::Link& link : hit.chain)
			{
				// A bullet, then text that wraps as the lines above do (a bulleted text line does not wrap)
				ImGui::Bullet();
				ImGui::Text("%s on %s in %s", MethodText(link.method).c_str(), EntityText(link.entity, breakpoints).c_str(), CompositeText(link.composite, link.compositeName).c_str());
			}
		}
		else
			ImGui::TextDisabled("Nothing in the scripts called it (the game, or a timer, queued it).");
		ImGui::TextDisabled("OpenCAGE shows it by name on its flowgraph while Live Link is connected.");
		ImGui::PopTextWrapPos();
	}

	void DrawBreakpoints()
	{
		const std::vector<LIVE_DEBUGGER::BreakpointView> breakpoints = LIVE_DEBUGGER::Breakpoints();
		// How many are set, and how many entries the game's own list holds for them (one per running instance and method)
		char title[96];
		snprintf(title, sizeof(title), "Breakpoints: %zu set, %d placed in the running level", breakpoints.size(), LIVE_DEBUGGER::RecordsInGame());
		ImGui::SeparatorText(title);
		if (ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
			ImGui::SetTooltip("Each breakpoint is placed in the game's own list once for each running instance and method it names.");
		bool on = true;
		DEBUG_MENU::GetSwitch("debugger.breakpoints", on);
		if (ImGui::Checkbox("Stop at breakpoints", &on))
			DEBUG_MENU::SetSwitch("debugger.breakpoints", on);
		Tooltip("Off, the breakpoints are kept but none stops the scripts (debugger.breakpoints).");
		ImGui::SameLine();
		ImGui::BeginDisabled(breakpoints.empty());
		if (ImGui::Button("Remove all"))
			Did(LIVE_DEBUGGER::RemoveAll());
		ImGui::EndDisabled();

		const std::string status = LIVE_DEBUGGER::Status();
		if (!status.empty())
		{
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextColored(ImVec4(1.0f, 0.7f, 0.3f, 1.0f), "%s", status.c_str());
			ImGui::PopTextWrapPos();
		}
		if (breakpoints.empty())
		{
			ImGui::TextWrapped("None set. Set them in OpenCAGE: right-click a node in a composite's flowgraph and choose Set Breakpoint (or press F9 over it), "
				"with the game launched from OpenCAGE with Live Link and the Debug Menu on.");
			return;
		}
		if (!ImGui::BeginTable("##breakpoints", 6, ImGuiTableFlags_RowBg | ImGuiTableFlags_BordersInnerV | ImGuiTableFlags_SizingFixedFit | ImGuiTableFlags_ScrollY,
			ImVec2(0.0f, 260.0f * DEBUG_MENU::Scale())))
			return;
		ImGui::TableSetupColumn("On");
		ImGui::TableSetupColumn("Breakpoint", ImGuiTableColumnFlags_WidthStretch);
		ImGui::TableSetupColumn("Methods");
		ImGui::TableSetupColumn("In game");
		ImGui::TableSetupColumn("Hits");
		ImGui::TableSetupColumn("");
		ImGui::TableHeadersRow();
		for (const LIVE_DEBUGGER::BreakpointView& bp : breakpoints)
		{
			ImGui::PushID(bp.id);
			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			bool enabled = bp.enabled;
			if (ImGui::Checkbox("##on", &enabled))
				Did(LIVE_DEBUGGER::Enable(bp.id, enabled));
			ImGui::TableNextColumn();
			const std::string where = CompositeText(bp.composite, bp.compositeName);
			if (!bp.label.empty())
				ImGui::Text("%d  %s", bp.id, bp.label.c_str());
			else
				ImGui::Text("%d  %s in %s", bp.id, LIVE_DEBUGGER::IdText(bp.entity).c_str(), where.c_str());
			if (ImGui::IsItemHovered())
				ImGui::SetTooltip("Entity %s in %s\nInstance: %s", LIVE_DEBUGGER::IdText(bp.entity).c_str(), where.c_str(), PathText(bp.path, bp.anyInstance).c_str());
			ImGui::TableNextColumn();
			ImGui::Text("%zu", bp.methods.size());
			if (ImGui::IsItemHovered() && !bp.methods.empty())
			{
				std::string methods;
				for (uint32_t method : bp.methods)
					methods += (methods.empty() ? "" : "\n") + MethodText(method);
				ImGui::SetTooltip("%s", methods.c_str());
			}
			ImGui::TableNextColumn();
			if (bp.records > 0)
				ImGui::Text("%d instance(s)", bp.instances);
			else
			{
				ImGui::TextDisabled("not in");
				if (ImGui::IsItemHovered() && !bp.problem.empty())
					ImGui::SetTooltip("%s", bp.problem.c_str());
			}
			ImGui::TableNextColumn();
			ImGui::Text("%u", bp.hits);
			ImGui::TableNextColumn();
			if (ImGui::SmallButton("Remove"))
				Did(LIVE_DEBUGGER::Remove(bp.id));
			ImGui::PopID();
		}
		ImGui::EndTable();
	}

	void DrawTab()
	{
		if (!LIVE_DEBUGGER::Patched())
		{
			ImGui::TextDisabled("Script breakpoints are not available on this game release.");
			return;
		}
		DrawHeld();
		DrawBreakpoints();
		std::lock_guard<std::mutex> lock(g_statusMutex);
		if (!g_lastAction.empty())
		{
			ImGui::Separator();
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextDisabled("Last: %s", g_lastAction.c_str());
			ImGui::PopTextWrapPos();
		}
	}

	// While the scripts are held, a line along the top of the screen says so (the game looks frozen otherwise)
	void DrawOverlay(ImDrawList* drawList)
	{
		if (!LIVE_DEBUGGER::Holding())
			return;
		const LIVE_DEBUGGER::HitView hit = LIVE_DEBUGGER::Hit();
		char text[256];
		// The method and the entity by their names alone when known (their ids otherwise), so the line stays clear of the
		// notes at the screen's corners
		if (hit.atBreakpoint)
		{
			const std::string name = EntityName(hit.entity, LIVE_DEBUGGER::Breakpoints());
			snprintf(text, sizeof(text), "Scripts held at breakpoint %d (%s on %s) - Continue in OpenCAGE, or the Debug Menu's Scripts > Debugger", hit.breakpoint,
				SCRIPT_DEBUG::MethodText(hit.method).c_str(), name.empty() ? LIVE_DEBUGGER::IdText(hit.entity).c_str() : name.c_str());
		}
		else
			snprintf(text, sizeof(text), "Scripts held by the debugger - Continue in OpenCAGE, or the Debug Menu's Scripts > Debugger");
		const float size = 18.0f * DEBUG_MENU::Scale();
		const ImVec2 extent = ImGui::GetFont()->CalcTextSizeA(size, FLT_MAX, 0.0f, text);
		const ImVec2 display = ImGui::GetIO().DisplaySize;
		const float pad = 6.0f * DEBUG_MENU::Scale();
		// Its box (half a pad above and below the text) goes under the notes already along the top of the screen
		const ImVec2 at((display.x - extent.x) * 0.5f, DEBUG_MENU::BannerTop(extent.y + pad) + pad * 0.5f);
		drawList->AddRectFilled(ImVec2(at.x - pad, at.y - pad * 0.5f), ImVec2(at.x + extent.x + pad, at.y + extent.y + pad * 0.5f), IM_COL32(60, 20, 10, 210), 4.0f);
		drawList->AddText(ImGui::GetFont(), size, at, IM_COL32(255, 170, 120, 255), text);
	}

	const bool g_tabAdded = DEBUG_MENU::AddTab("Debugger", 90, DrawTab, "Scripts") && DEBUG_MENU::AddOverlay(98, DrawOverlay);
}

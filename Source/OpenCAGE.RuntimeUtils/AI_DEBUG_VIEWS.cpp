#include "AI_DEBUG.h"
#include "DEBUG_MENU.h"

#include <algorithm>
#include <atomic>
#include <cfloat>
#include <cmath>
#include <cstdio>
#include <cstring>

#include <imgui.h>

/*
	The tabs and overlays for AI_DEBUG's snapshot (see AI_DEBUG.h): Characters, Player and Alien, and the drawing over the
	game they switch on. Every switch is a named one (DEBUG_MENU::AddSwitch), so OpenCAGE can set them too; they are
	named the first time the overlay or a tab runs.
*/
namespace
{
	constexpr float kPi = 3.14159265f;

	// What is drawn over the game: each for the selected character, or for all of them with ".all" on
	std::atomic<bool> g_labels = false, g_labelsAll = true;
	std::atomic<bool> g_cones = false, g_conesAll = false;
	std::atomic<bool> g_hearing = false, g_hearingAll = false;
	std::atomic<bool> g_paths = false, g_pathsAll = false;
	std::atomic<bool> g_lastKnown = false, g_lastKnownAll = false;
	std::atomic<bool> g_sweepArea = false, g_backstageTarget = false;
	std::atomic<bool> g_lightSamples = false;
	// The light meter's screen tint, and how strongly it shows
	std::atomic<bool> g_lightTint = false;
	float g_tintOpacity = 0.4f;

	void NameSwitches()
	{
		static bool named = false;
		if (named)
			return;
		named = true;
		DEBUG_MENU::AddSwitch("characters.labels", &g_labels);
		DEBUG_MENU::AddSwitch("characters.labels.all", &g_labelsAll);
		DEBUG_MENU::AddSwitch("characters.view_cones", &g_cones);
		DEBUG_MENU::AddSwitch("characters.view_cones.all", &g_conesAll);
		DEBUG_MENU::AddSwitch("characters.hearing", &g_hearing);
		DEBUG_MENU::AddSwitch("characters.hearing.all", &g_hearingAll);
		DEBUG_MENU::AddSwitch("characters.paths", &g_paths);
		DEBUG_MENU::AddSwitch("characters.paths.all", &g_pathsAll);
		DEBUG_MENU::AddSwitch("characters.last_known", &g_lastKnown);
		DEBUG_MENU::AddSwitch("characters.last_known.all", &g_lastKnownAll);
		DEBUG_MENU::AddSwitch("player.light_samples", &g_lightSamples);
		DEBUG_MENU::AddSwitch("player.light_tint", &g_lightTint);
		DEBUG_MENU::AddSwitch("alien.sweep_area", &g_sweepArea);
		DEBUG_MENU::AddSwitch("alien.backstage_target", &g_backstageTarget);
	}

	bool Shown(const AI_DEBUG::Character& character, const std::atomic<bool>& on, const std::atomic<bool>& all)
	{
		return on && (all || character.selected);
	}

	// Colours: a view cone's by its place in the set, and a meter's by the thresholds it has
	// passed
	ImU32 ConeColour(int index, int alpha)
	{
		switch (index)
		{
		case 0: return IM_COL32(255, 255, 0, alpha);
		case 1: return IM_COL32(255, 174, 17, alpha);
		case 2: return IM_COL32(0, 255, 0, alpha);
		case 3: return IM_COL32(255, 0, 255, alpha);
		default: return IM_COL32(0, 200, 200, alpha);
		}
	}

	ImU32 LevelColour(float level, const float thresholds[AI_DEBUG::kThresholds], int alpha = 255)
	{
		if (level >= thresholds[3] && thresholds[3] > 0.0f)
			return IM_COL32(255, 70, 255, alpha);
		if (level >= thresholds[2] && thresholds[2] > 0.0f)
			return IM_COL32(255, 60, 40, alpha);
		if (level >= thresholds[1] && thresholds[1] > 0.0f)
			return IM_COL32(255, 170, 40, alpha);
		if (level >= thresholds[0] && thresholds[0] > 0.0f)
			return IM_COL32(230, 230, 80, alpha);
		return IM_COL32(160, 160, 160, alpha);
	}

	ImU32 ThresholdColour(int threshold, int alpha = 255)
	{
		switch (threshold)
		{
		case 0: return IM_COL32(230, 230, 80, alpha);
		case 1: return IM_COL32(255, 170, 40, alpha);
		case 2: return IM_COL32(255, 60, 40, alpha);
		default: return IM_COL32(255, 70, 255, alpha);
		}
	}

	float Clamp01(float value)
	{
		return value < 0.0f ? 0.0f : value > 1.0f ? 1.0f : value;
	}

	// A line in the world, cut into pieces so that the part in front of the camera still shows when an end is behind it
	void Line(ImDrawList* drawList, const float a[3], const float b[3], ImU32 colour, float thickness = 1.5f)
	{
		const float dx = b[0] - a[0], dy = b[1] - a[1], dz = b[2] - a[2];
		const float length = std::sqrt(dx * dx + dy * dy + dz * dz);
		const int pieces = (std::min)(16, (std::max)(1, static_cast<int>(length / 2.0f)));
		float from[3] = { a[0], a[1], a[2] };
		for (int i = 1; i <= pieces; i++)
		{
			const float t = static_cast<float>(i) / pieces;
			const float to[3] = { a[0] + dx * t, a[1] + dy * t, a[2] + dz * t };
			DEBUG_MENU::WorldLine(drawList, from, to, colour, thickness);
			memcpy(from, to, sizeof(from));
		}
	}

	void Cross(ImDrawList* drawList, const float at[3], float size, ImU32 colour, float thickness = 2.0f)
	{
		const float a[3] = { at[0] - size, at[1], at[2] - size }, b[3] = { at[0] + size, at[1], at[2] + size };
		const float c[3] = { at[0] - size, at[1], at[2] + size }, d[3] = { at[0] + size, at[1], at[2] - size };
		const float e[3] = { at[0], at[1] - size, at[2] }, f[3] = { at[0], at[1] + size, at[2] };
		DEBUG_MENU::WorldLine(drawList, a, b, colour, thickness);
		DEBUG_MENU::WorldLine(drawList, c, d, colour, thickness);
		DEBUG_MENU::WorldLine(drawList, e, f, colour, thickness);
	}

	void Dot(ImDrawList* drawList, const float at[3], float radius, ImU32 colour)
	{
		ImVec2 screen;
		if (DEBUG_MENU::Project(at, screen))
			drawList->AddCircleFilled(screen, radius, colour);
	}

	float TextSize()
	{
		return 15.0f * DEBUG_MENU::Scale();
	}

	// Where a label goes over a character: above its head, or above where a head would be
	void LabelPoint(const AI_DEBUG::Character& character, float out[3])
	{
		if (character.haveHead)
		{
			out[0] = character.headPosition[0];
			out[1] = character.headPosition[1] + 0.45f;
			out[2] = character.headPosition[2];
		}
		else
		{
			out[0] = character.position[0];
			out[1] = character.position[1] + 2.1f;
			out[2] = character.position[2];
		}
	}

	const char* NameOf(const AI_DEBUG::Snapshot& snapshot, int index)
	{
		return index >= 0 && index < static_cast<int>(snapshot.characters.size()) ? snapshot.characters[index].name : "?";
	}

	const AI_DEBUG::Alien* AlienOf(const AI_DEBUG::Snapshot& snapshot, int character)
	{
		for (const AI_DEBUG::Alien& alien : snapshot.aliens)
			if (alien.character == character)
				return &alien;
		return nullptr;
	}

	// A meter drawn on a draw list: the level filled in, a tick at each threshold
	void DrawMeter(ImDrawList* drawList, ImVec2 corner, ImVec2 size, float level, const float* ticks, int tickCount, ImU32 fill)
	{
		drawList->AddRectFilled(corner, ImVec2(corner.x + size.x, corner.y + size.y), IM_COL32(20, 20, 20, 200));
		drawList->AddRectFilled(corner, ImVec2(corner.x + size.x * Clamp01(level), corner.y + size.y), fill);
		for (int i = 0; i < tickCount; i++)
		{
			if (ticks[i] <= 0.0f || ticks[i] > 1.0f)
				continue;
			const float x = corner.x + size.x * ticks[i];
			drawList->AddLine(ImVec2(x, corner.y - 2.0f), ImVec2(x, corner.y + size.y + 2.0f), IM_COL32(255, 255, 255, 220), 1.0f);
		}
		drawList->AddRect(corner, ImVec2(corner.x + size.x, corner.y + size.y), IM_COL32(255, 255, 255, 90));
	}

	// The same in the window, taking a line, with text over it
	void Meter(float level, const float* ticks, int tickCount, ImU32 fill, const char* text, float width = -1.0f)
	{
		const float scale = DEBUG_MENU::Scale();
		const ImVec2 corner = ImGui::GetCursorScreenPos();
		const float available = ImGui::GetContentRegionAvail().x;
		const ImVec2 size(width > 0.0f ? (std::min)(width, available) : available, ImGui::GetTextLineHeight() + 2.0f * scale);
		ImDrawList* drawList = ImGui::GetWindowDrawList();
		DrawMeter(drawList, corner, size, level, ticks, tickCount, fill);
		if (text)
			drawList->AddText(ImVec2(corner.x + 4.0f * scale, corner.y + scale), IM_COL32(255, 255, 255, 255), text);
		ImGui::Dummy(size);
	}

	void SwitchRow(const char* label, std::atomic<bool>& on, std::atomic<bool>* all)
	{
		bool value = on;
		if (ImGui::Checkbox(label, &value))
			on = value;
		if (all)
		{
			ImGui::SameLine(ImGui::CalcTextSize("Last known positions").x + ImGui::GetFrameHeight() * 3.0f);
			bool every = *all;
			char id[64];
			snprintf(id, sizeof(id), "For every character##%s", label);
			if (ImGui::Checkbox(id, &every))
				*all = every;
		}
	}

	void SwitchBox(const char* label, std::atomic<bool>& on)
	{
		bool value = on;
		if (ImGui::Checkbox(label, &value))
			on = value;
	}

	// ---- Overlays ----

	// placed: the boxes of the labels already drawn this frame (left, top, right, bottom), which this one keeps clear of
	void DrawLabel(ImDrawList* drawList, const AI_DEBUG::Snapshot& snapshot, const AI_DEBUG::Character& character, std::vector<ImVec4>& placed)
	{
		float at[3];
		LabelPoint(character, at);
		ImVec2 anchor;
		if (!DEBUG_MENU::Project(at, anchor))
			return;

		char lines[5][128];
		ImU32 colours[5];
		int count = 0;
		snprintf(lines[count], sizeof(lines[0]), "%s (%s)%s", character.name, DEBUG_MENU::ClassName(character.characterClass), character.dead ? " - dead" : "");
		colours[count++] = character.dead ? IM_COL32(160, 160, 160, 255) : IM_COL32(255, 255, 255, 255);
		if (!character.dead && character.read && !character.player)
		{
			snprintf(lines[count], sizeof(lines[0]), "%s", AI_DEBUG::BranchName(character.branch));
			colours[count++] = IM_COL32(150, 220, 255, 255);
			const AI_DEBUG::Alien* alien = character.alien ? AlienOf(snapshot, static_cast<int>(&character - snapshot.characters.data())) : nullptr;
			if (alien && alien->haveGauge)
				snprintf(lines[count], sizeof(lines[0]), "Menace %.0f%% (%s)%s%s", alien->gauge * 100.0f, AI_DEBUG::GaugeLevelName(alien->gaugeLevel), alien->menacing ? ", menacing" : "", alien->withdrawState > 0 ? ", withdrawing" : "");
			else
				snprintf(lines[count], sizeof(lines[0]), "%s | %s, %s", AI_DEBUG::AwarenessName(character.awareness), AI_DEBUG::AlertnessName(character.alertness), AI_DEBUG::AggroName(character.aggro));
			colours[count++] = IM_COL32(255, 230, 160, 255);
			if (character.haveTarget)
			{
				snprintf(lines[count], sizeof(lines[0]), "Target: %s", character.target >= 0 ? NameOf(snapshot, character.target) : "(not a character)");
				colours[count++] = IM_COL32(255, 150, 150, 255);
			}
		}
		else if (character.player && character.read)
		{
			snprintf(lines[count], sizeof(lines[0]), "%s%s", snapshot.player.haveLightMeter ? AI_DEBUG::LightLevelName(snapshot.player.level) : "", snapshot.player.hiding ? ", hiding" : "");
			colours[count++] = IM_COL32(255, 230, 160, 255);
		}

		const float size = TextSize();
		const float scale = DEBUG_MENU::Scale();
		const bool bar = !character.dead && character.haveRecord;
		float width = bar ? 120.0f * scale : 0.0f;
		for (int i = 0; i < count; i++)
			width = (std::max)(width, ImGui::GetFont()->CalcTextSizeA(size, FLT_MAX, 0.0f, lines[i]).x);
		const float lineHeight = size + 1.0f;
		const float barHeight = bar ? 7.0f * scale + 4.0f : 0.0f;
		const float height = lineHeight * count + barHeight;
		// Two characters close together on screen would print over each other: a label that would cover one already drawn
		// goes up above it, with a line back down to its character
		ImVec2 corner(anchor.x - width * 0.5f, anchor.y - height);
		bool moved = false;
		for (int tries = 0; tries < 8; tries++)
		{
			bool clear = true;
			for (const ImVec4& box : placed)
			{
				if (corner.x - 5.0f < box.z && corner.x + width + 5.0f > box.x && corner.y - 3.0f < box.w && corner.y + height + 3.0f > box.y)
				{
					corner.y = box.y - height - 8.0f;
					moved = true;
					clear = false;
					break;
				}
			}
			if (clear)
				break;
		}
		placed.push_back(ImVec4(corner.x - 5.0f, corner.y - 3.0f, corner.x + width + 5.0f, corner.y + height + 3.0f));
		if (moved)
			drawList->AddLine(anchor, ImVec2(anchor.x, corner.y + height + 3.0f), IM_COL32(0, 0, 0, 200), 1.5f);
		drawList->AddRectFilled(ImVec2(corner.x - 5.0f, corner.y - 3.0f), ImVec2(corner.x + width + 5.0f, corner.y + height + 3.0f), IM_COL32(0, 0, 0, 160), 3.0f);
		if (character.selected)
			drawList->AddRect(ImVec2(corner.x - 5.0f, corner.y - 3.0f), ImVec2(corner.x + width + 5.0f, corner.y + height + 3.0f), IM_COL32(255, 255, 255, 160), 3.0f);
		for (int i = 0; i < count; i++)
			drawList->AddText(ImGui::GetFont(), size, ImVec2(corner.x, corner.y + lineHeight * i), colours[i], lines[i]);
		if (bar)
		{
			// Suspicion: the combined meter against its four thresholds
			const AI_DEBUG::Sense& combined = character.senses[AI_DEBUG::kCombinedSense];
			DrawMeter(drawList, ImVec2(corner.x, corner.y + lineHeight * count + 3.0f), ImVec2(width, 7.0f * scale), combined.level, combined.thresholds, AI_DEBUG::kThresholds, LevelColour(combined.level, combined.thresholds));
		}
	}

	void DrawCones(ImDrawList* drawList, const AI_DEBUG::Character& character)
	{
		if (!character.haveHead)
			return;
		const float* head = character.headPosition;
		const float* forward = character.headForward;
		const float yaw = std::atan2(forward[0], forward[2]);
		const float pitch = std::asin((std::max)(-1.0f, (std::min)(1.0f, forward[1])));
		constexpr int kSegments = 16;
		for (int c = 0; c < character.coneCount; c++)
		{
			const AI_DEBUG::Cone& cone = character.cones[c];
			if (!cone.valid || cone.length <= 0.0f)
				continue;
			const ImU32 colour = ConeColour(c, cone.playerInside ? 255 : 170);
			const float thickness = cone.playerInside ? 3.0f : 1.5f;
			const float half = std::acos((std::max)(-1.0f, (std::min)(1.0f, cone.horizontalDot)));
			// The sector across, at the head's height
			float arc[kSegments + 1][3];
			for (int i = 0; i <= kSegments; i++)
			{
				const float angle = yaw - half + 2.0f * half * i / kSegments;
				arc[i][0] = head[0] + cone.length * std::sin(angle);
				arc[i][1] = head[1];
				arc[i][2] = head[2] + cone.length * std::cos(angle);
			}
			if (cone.playerInside && half <= kPi * 0.5f)
			{
				ImVec2 points[kSegments + 2];
				bool all = DEBUG_MENU::Project(head, points[0]);
				for (int i = 0; all && i <= kSegments; i++)
					all = DEBUG_MENU::Project(arc[i], points[i + 1]);
				if (all)
					drawList->AddConvexPolyFilled(points, kSegments + 2, ConeColour(c, 45));
			}
			Line(drawList, head, arc[0], colour, thickness);
			Line(drawList, head, arc[kSegments], colour, thickness);
			for (int i = 0; i < kSegments; i++)
				DEBUG_MENU::WorldLine(drawList, arc[i], arc[i + 1], colour, thickness);
			// Its height: the top and bottom edges in the head's upright plane
			const float upright = std::acos((std::max)(-1.0f, (std::min)(1.0f, cone.verticalDot)));
			for (int sign = -1; sign <= 1; sign += 2)
			{
				const float tilt = pitch + sign * upright;
				const float end[3] = { head[0] + cone.length * std::cos(tilt) * std::sin(yaw), head[1] + cone.length * std::sin(tilt), head[2] + cone.length * std::cos(tilt) * std::cos(yaw) };
				Line(drawList, head, end, ConeColour(c, 90), 1.0f);
			}
		}
	}

	void DrawHearing(ImDrawList* drawList, const AI_DEBUG::Character& character)
	{
		if (!character.haveHearing || character.hearing <= 0.0f)
			return;
		const float centre[3] = { character.position[0], character.position[1] + 0.1f, character.position[2] };
		DEBUG_MENU::WorldCircle(drawList, centre, character.hearing, IM_COL32(110, 180, 255, 200), 48, 1.5f);
	}

	void DrawPath(ImDrawList* drawList, const AI_DEBUG::Snapshot& snapshot, const AI_DEBUG::Character& character)
	{
		const AI_DEBUG::Path& path = character.path;
		if (!path.have)
			return;
		const ImU32 passed = IM_COL32(140, 140, 140, 200);
		const ImU32 future = IM_COL32(0, 220, 255, 230);
		const ImU32 current = IM_COL32(255, 255, 255, 255);
		const ImU32 corridor = IM_COL32(60, 200, 90, 120);
		const ImU32 corners = IM_COL32(80, 255, 120, 220);

		// The corridor: each gap it passes through, its edges, and the corners steered by
		for (int i = 0; i < path.portalCount; i++)
		{
			Line(drawList, path.portals[i][0], path.portals[i][1], corridor, 1.0f);
			if (i > 0)
			{
				Line(drawList, path.portals[i - 1][0], path.portals[i][0], corridor, 1.0f);
				Line(drawList, path.portals[i - 1][1], path.portals[i][1], corridor, 1.0f);
			}
		}
		const float* from = character.position;
		for (int i = 0; i < path.cornerCount; i++)
		{
			Line(drawList, from, path.corners[i], corners, 1.5f);
			from = path.corners[i];
		}

		// The nodes passed, then those of the path: before the current one grey, the way to it white, the rest blue
		for (int i = 1; i < path.passedCount; i++)
			Line(drawList, path.passed[i - 1], path.passed[i], passed, 1.5f);
		for (int i = 0; i < path.passedCount; i++)
			Dot(drawList, path.passed[i], 2.5f, passed);
		const int currentNode = path.currentNode >= 0 && path.currentNode < path.nodeCount ? path.currentNode : path.nodeCount;
		for (int i = 1; i < path.nodeCount; i++)
			Line(drawList, path.nodes[i - 1], path.nodes[i], i <= currentNode ? passed : future, 2.0f);
		if (currentNode < path.nodeCount)
		{
			Line(drawList, character.position, path.nodes[currentNode], current, 2.5f);
			Dot(drawList, path.nodes[currentNode], 5.0f, current);
		}
		for (int i = 0; i < path.nodeCount; i++)
			if (i != currentNode)
				Dot(drawList, path.nodes[i], 3.5f, i < currentNode ? passed : future);

		if (path.haveRequest)
		{
			Cross(drawList, path.destination, 0.4f, IM_COL32(255, 220, 0, 255));
			char text[128];
			snprintf(text, sizeof(text), "%s: %s, %.1f m", character.name, AI_DEBUG::PathStatusName(path.pathStatus), path.remaining);
			const float above[3] = { path.destination[0], path.destination[1] + 0.6f, path.destination[2] };
			DEBUG_MENU::WorldText(drawList, above, text, IM_COL32(255, 220, 0, 255));
		}
		(void)snapshot;
	}

	void DrawLastKnown(ImDrawList* drawList, const AI_DEBUG::Snapshot& snapshot, const AI_DEBUG::Character& character)
	{
		if (!character.haveRecord)
			return;
		char text[128];
		// The combined sense at each threshold
		const AI_DEBUG::Sense& combined = character.senses[AI_DEBUG::kCombinedSense];
		for (int t = 0; t < AI_DEBUG::kThresholds; t++)
		{
			if (combined.lastTime[t] <= 0.0f)
				continue;
			Cross(drawList, combined.lastPosition[t], 0.25f + 0.05f * t, ThresholdColour(t));
			snprintf(text, sizeof(text), "%s: %s, %.1f s ago", character.name, AI_DEBUG::ThresholdName(t), snapshot.now - combined.lastTime[t]);
			const float above[3] = { combined.lastPosition[t][0], combined.lastPosition[t][1] + 0.35f + 0.3f * t, combined.lastPosition[t][2] };
			DEBUG_MENU::WorldText(drawList, above, text, ThresholdColour(t));
		}
		// Each other sense at the highest threshold it has placed the enemy at
		for (int s = 0; s < AI_DEBUG::kCombinedSense; s++)
		{
			const AI_DEBUG::Sense& sense = character.senses[s];
			for (int t = AI_DEBUG::kThresholds - 1; t >= 0; t--)
			{
				if (sense.lastTime[t] <= 0.0f)
					continue;
				Cross(drawList, sense.lastPosition[t], 0.15f, IM_COL32(200, 200, 255, 220), 1.5f);
				snprintf(text, sizeof(text), "%s (%s) %.1f s", AI_DEBUG::SenseName(s), AI_DEBUG::ThresholdName(t), snapshot.now - sense.lastTime[t]);
				const float above[3] = { sense.lastPosition[t][0], sense.lastPosition[t][1] - 0.25f, sense.lastPosition[t][2] };
				DEBUG_MENU::WorldText(drawList, above, text, IM_COL32(200, 200, 255, 230));
				break;
			}
		}
	}

	void DrawAlien(ImDrawList* drawList, const AI_DEBUG::Snapshot& snapshot, const AI_DEBUG::Alien& alien)
	{
		const AI_DEBUG::Character& character = snapshot.characters[alien.character];
		if (g_sweepArea && alien.haveBox)
		{
			// The box its stalking works within: its length along its angle, its width across
			const ImU32 colour = IM_COL32(255, 140, 40, 230);
			const float along[3] = { std::sin(alien.boxAngle), 0.0f, std::cos(alien.boxAngle) };
			const float across[3] = { along[2], 0.0f, -along[0] };
			float corners[4][3];
			const float signs[4][2] = { { 1, 1 }, { 1, -1 }, { -1, -1 }, { -1, 1 } };
			for (int i = 0; i < 4; i++)
				for (int k = 0; k < 3; k++)
					corners[i][k] = alien.boxCentre[k] + along[k] * alien.boxHalfLength * signs[i][0] + across[k] * alien.boxHalfWidth * signs[i][1] + (k == 1 ? 0.1f : 0.0f);
			for (int i = 0; i < 4; i++)
				Line(drawList, corners[i], corners[(i + 1) % 4], colour, 2.0f);
			if (alien.haveSweep)
			{
				Cross(drawList, alien.sweepPosition, 0.5f, colour);
				if (alien.sweepType == 1 && alien.sweepRadius > 0.0f)
					DEBUG_MENU::WorldCircle(drawList, alien.sweepPosition, alien.sweepRadius, IM_COL32(255, 140, 40, 150), 48, 1.0f);
				const float above[3] = { alien.sweepPosition[0], alien.sweepPosition[1] + 0.8f, alien.sweepPosition[2] };
				char text[128];
				snprintf(text, sizeof(text), "Sweep area: %s", AI_DEBUG::SweepTypeName(alien.sweepType));
				DEBUG_MENU::WorldText(drawList, above, text, colour);
			}
		}
		if (g_backstageTarget)
		{
			const ImU32 colour = IM_COL32(200, 120, 255, 240);
			if (alien.haveBackstageTarget)
			{
				Line(drawList, character.position, alien.backstageTarget, colour, 2.5f);
				Cross(drawList, alien.backstageTarget, 0.5f, colour, 2.5f);
				const float above[3] = { alien.backstageTarget[0], alien.backstageTarget[1] + 0.7f, alien.backstageTarget[2] };
				DEBUG_MENU::WorldText(drawList, above, "Heading for (in the vents)", colour);
			}
			if (alien.haveVentNearAlien)
			{
				Cross(drawList, alien.ventNearAlien, 0.35f, IM_COL32(200, 120, 255, 180));
				const float above[3] = { alien.ventNearAlien[0], alien.ventNearAlien[1] + 0.5f, alien.ventNearAlien[2] };
				DEBUG_MENU::WorldText(drawList, above, "Best vent near the alien", IM_COL32(200, 120, 255, 220));
			}
			if (alien.haveVentNearPlayer)
			{
				Cross(drawList, alien.ventNearPlayer, 0.35f, IM_COL32(200, 120, 255, 180));
				const float above[3] = { alien.ventNearPlayer[0], alien.ventNearPlayer[1] + 0.8f, alien.ventNearPlayer[2] };
				DEBUG_MENU::WorldText(drawList, above, "Best vent near the player", IM_COL32(200, 120, 255, 220));
			}
		}
	}

	void DrawLightSamples(ImDrawList* drawList, const AI_DEBUG::Player& player)
	{
		if (!player.haveLightMeter)
			return;
		for (int i = 0; i < 3; i++)
		{
			const float* rgb = player.samples[i];
			const ImU32 colour = IM_COL32(static_cast<int>(Clamp01(rgb[0]) * 255), static_cast<int>(Clamp01(rgb[1]) * 255), static_cast<int>(Clamp01(rgb[2]) * 255), 255);
			ImVec2 screen;
			if (!DEBUG_MENU::Project(player.samplePositions[i], screen))
				continue;
			drawList->AddCircleFilled(screen, 6.0f, colour);
			drawList->AddCircle(screen, 7.0f, IM_COL32(255, 255, 255, 200), 0, 1.5f);
		}
	}

	void DrawOverlays(ImDrawList* drawList)
	{
		NameSwitches();
		AI_DEBUG::SetLightTint(g_lightTint, g_tintOpacity);
		const bool any = g_labels || g_cones || g_hearing || g_paths || g_lastKnown || g_sweepArea || g_backstageTarget || g_lightSamples;
		if (!any)
			return;
		const AI_DEBUG::Snapshot& snapshot = AI_DEBUG::Read();
		for (const AI_DEBUG::Alien& alien : snapshot.aliens)
			if (alien.read)
				DrawAlien(drawList, snapshot, alien);
		for (const AI_DEBUG::Character& character : snapshot.characters)
		{
			if (!character.read || character.dead || !character.detailed)
				continue;
			if (Shown(character, g_paths, g_pathsAll))
				DrawPath(drawList, snapshot, character);
			if (Shown(character, g_cones, g_conesAll) && !character.player)
				DrawCones(drawList, character);
			if (Shown(character, g_hearing, g_hearingAll) && !character.player)
				DrawHearing(drawList, character);
			if (Shown(character, g_lastKnown, g_lastKnownAll))
				DrawLastKnown(drawList, snapshot, character);
		}
		if (g_lightSamples)
			DrawLightSamples(drawList, snapshot.player);
		// Labels last, over everything else; with many characters, only those near enough to read in detail. The player's
		// only when selected, as its label would sit just over the camera.
		std::vector<ImVec4> placed;
		for (const AI_DEBUG::Character& character : snapshot.characters)
			if (Shown(character, g_labels, g_labelsAll) && (character.detailed || character.selected) && (!character.player || character.selected))
				DrawLabel(drawList, snapshot, character, placed);
	}

	// ---- The Characters tab ----

	void DrawTable(const AI_DEBUG::Snapshot& snapshot)
	{
		// The header and a row per character (up to ten, then it scrolls), with room under them for the sideways scrollbar
		const ImGuiStyle& style = ImGui::GetStyle();
		const float rows = (std::min<float>)(static_cast<float>(snapshot.characters.size()) + 1.0f, 11.0f);
		const float height = ImGui::GetTextLineHeightWithSpacing() * rows + style.ScrollbarSize + style.CellPadding.y * 2.0f;
		const ImGuiTableFlags flags = ImGuiTableFlags_RowBg | ImGuiTableFlags_BordersInnerV | ImGuiTableFlags_ScrollY | ImGuiTableFlags_ScrollX | ImGuiTableFlags_SizingFixedFit;
		// Twice the usual gap either side of each column's line, so a name doesn't run up against the next column
		ImGui::PushStyleVar(ImGuiStyleVar_CellPadding, ImVec2(style.CellPadding.x * 2.0f, style.CellPadding.y));
		const bool shown = ImGui::BeginTable("characters", 8, flags, ImVec2(0.0f, height));
		ImGui::PopStyleVar();
		if (!shown)
			return;
		ImGui::TableSetupScrollFreeze(1, 1);
		ImGui::TableSetupColumn("Name");
		ImGui::TableSetupColumn("Class");
		ImGui::TableSetupColumn("Distance");
		ImGui::TableSetupColumn("Health");
		ImGui::TableSetupColumn("Awareness / menace");
		ImGui::TableSetupColumn("Alertness, aggression");
		ImGui::TableSetupColumn("Mood");
		ImGui::TableSetupColumn("Branch");
		ImGui::TableHeadersRow();
		for (size_t i = 0; i < snapshot.characters.size(); i++)
		{
			const AI_DEBUG::Character& character = snapshot.characters[i];
			ImGui::TableNextRow();
			ImGui::TableSetColumnIndex(0);
			char label[96];
			snprintf(label, sizeof(label), "%s##%u", character.name, character.id);
			if (character.dead)
				ImGui::PushStyleColor(ImGuiCol_Text, IM_COL32(150, 150, 150, 255));
			if (ImGui::Selectable(label, character.selected, ImGuiSelectableFlags_SpanAllColumns))
				DEBUG_MENU::Select(character.selected ? 0 : character.id);
			ImGui::TableSetColumnIndex(1);
			ImGui::TextUnformatted(DEBUG_MENU::ClassName(character.characterClass));
			ImGui::TableSetColumnIndex(2);
			ImGui::Text("%.1f m", character.distance);
			ImGui::TableSetColumnIndex(3);
			if (character.read)
				ImGui::Text("%d/%d", character.health, character.maxHealth);
			ImGui::TableSetColumnIndex(4);
			const AI_DEBUG::Alien* alien = character.alien ? AlienOf(snapshot, static_cast<int>(i)) : nullptr;
			if (alien && alien->haveGauge)
				ImGui::Text("Menace %.0f%%%s", alien->gauge * 100.0f, alien->menacing ? " (menacing)" : "");
			else if (character.read && !character.player)
				ImGui::TextUnformatted(AI_DEBUG::AwarenessName(character.awareness));
			ImGui::TableSetColumnIndex(5);
			if (character.read && !character.player)
				ImGui::Text("%s, %s", AI_DEBUG::AlertnessName(character.alertness), AI_DEBUG::AggroName(character.aggro));
			ImGui::TableSetColumnIndex(6);
			if (character.read && !character.player)
				ImGui::TextUnformatted(AI_DEBUG::MoodName(character.mood));
			ImGui::TableSetColumnIndex(7);
			if (character.read && !character.player)
				ImGui::TextUnformatted(AI_DEBUG::BranchName(character.branch));
			if (character.dead)
				ImGui::PopStyleColor();
		}
		ImGui::EndTable();
	}

	void Vector(const char* label, const float v[3])
	{
		ImGui::Text("%s: %.2f, %.2f, %.2f", label, v[0], v[1], v[2]);
	}

	void DrawSenses(const AI_DEBUG::Snapshot& snapshot, const AI_DEBUG::Character& character)
	{
		ImGui::Text("Senses in use: %s; view cones: %s", AI_DEBUG::SenseSystemName(character.senseSystem), AI_DEBUG::ViewconeSetName(character.viewconeSet));
		if (!character.haveRecord)
		{
			ImGui::TextDisabled(character.recordCount ? "No memory read." : "It remembers no enemies.");
			return;
		}
		ImGui::Text("Its memory of %s%s (one of %d): %.1f m away, %s, %.0f%% of it seen by its last checks", NameOf(snapshot, character.recordEnemy),
			character.recordIsTarget ? ", its target" : "", character.recordCount, character.recordDistance, AI_DEBUG::ThreatName(character.threatStatus), character.hitsFraction * 100.0f);
		const float labelWidth = ImGui::CalcTextSize("Movement sound  ").x;
		for (int s = 0; s < AI_DEBUG::kSenses; s++)
		{
			const AI_DEBUG::Sense& sense = character.senses[s];
			ImGui::TextUnformatted(AI_DEBUG::SenseName(s));
			ImGui::SameLine(labelWidth);
			char text[96];
			snprintf(text, sizeof(text), "%.2f%s", sense.level, sense.triggered ? "  (triggered)" : "");
			Meter(sense.level, sense.thresholds, AI_DEBUG::kThresholds, LevelColour(sense.level, sense.thresholds), text);
			if (ImGui::IsItemHovered())
				ImGui::SetTooltip("Thresholds: trace %.2f, lower %.2f, activated %.2f, upper %.2f", sense.thresholds[0], sense.thresholds[1], sense.thresholds[2], sense.thresholds[3]);
		}
	}

	void DrawLastKnownTable(const AI_DEBUG::Snapshot& snapshot, const AI_DEBUG::Character& character)
	{
		if (!character.haveRecord)
			return;
		if (!ImGui::BeginTable("lastknown", 5, ImGuiTableFlags_RowBg | ImGuiTableFlags_BordersInnerV | ImGuiTableFlags_SizingStretchProp))
			return;
		ImGui::TableSetupColumn("Sense");
		for (int t = 0; t < AI_DEBUG::kThresholds; t++)
			ImGui::TableSetupColumn(AI_DEBUG::ThresholdName(t));
		ImGui::TableHeadersRow();
		for (int s = 0; s < AI_DEBUG::kSenses; s++)
		{
			const AI_DEBUG::Sense& sense = character.senses[s];
			ImGui::TableNextRow();
			ImGui::TableSetColumnIndex(0);
			ImGui::TextUnformatted(AI_DEBUG::SenseName(s));
			for (int t = 0; t < AI_DEBUG::kThresholds; t++)
			{
				ImGui::TableSetColumnIndex(1 + t);
				if (sense.lastTime[t] <= 0.0f)
				{
					ImGui::TextDisabled("-");
					continue;
				}
				ImGui::Text("%.1f s ago", snapshot.now - sense.lastTime[t]);
				if (ImGui::IsItemHovered())
					ImGui::SetTooltip("At %.2f, %.2f, %.2f", sense.lastPosition[t][0], sense.lastPosition[t][1], sense.lastPosition[t][2]);
			}
		}
		ImGui::EndTable();
	}

	void DrawConesAndHearing(const AI_DEBUG::Character& character)
	{
		if (character.haveHead)
		{
			Vector("Head", character.headPosition);
			Vector("Looking", character.headForward);
		}
		else
			ImGui::TextDisabled("No head pose (the game only keeps them for the living).");
		if (character.haveHearing)
			ImGui::Text("Hears up to %.1f m", character.hearing);
		if (character.coneCount == 0)
		{
			ImGui::TextDisabled("No view cones.");
			return;
		}
		if (!ImGui::BeginTable("cones", 6, ImGuiTableFlags_RowBg | ImGuiTableFlags_BordersInnerV | ImGuiTableFlags_SizingStretchProp))
			return;
		ImGui::TableSetupColumn("Cone");
		ImGui::TableSetupColumn("Type");
		ImGui::TableSetupColumn("Length");
		ImGui::TableSetupColumn("Width");
		ImGui::TableSetupColumn("Height");
		ImGui::TableSetupColumn("Player in it");
		ImGui::TableHeadersRow();
		for (int c = 0; c < character.coneCount; c++)
		{
			const AI_DEBUG::Cone& cone = character.cones[c];
			ImGui::TableNextRow();
			ImGui::TableSetColumnIndex(0);
			ImGui::PushStyleColor(ImGuiCol_Text, ConeColour(c, 255));
			ImGui::Text("%d%s", c, cone.valid ? "" : " (off)");
			ImGui::PopStyleColor();
			ImGui::TableSetColumnIndex(1);
			ImGui::TextUnformatted(cone.typeName[0] ? cone.typeName : "-");
			ImGui::TableSetColumnIndex(2);
			ImGui::Text("%.1f m", cone.length);
			ImGui::TableSetColumnIndex(3);
			ImGui::Text("%.0f deg", 2.0f * std::acos((std::max)(-1.0f, (std::min)(1.0f, cone.horizontalDot))) * 180.0f / kPi);
			ImGui::TableSetColumnIndex(4);
			ImGui::Text("%.0f deg", 2.0f * std::acos((std::max)(-1.0f, (std::min)(1.0f, cone.verticalDot))) * 180.0f / kPi);
			ImGui::TableSetColumnIndex(5);
			ImGui::TextUnformatted(cone.playerInside ? (c < 4 ? "yes" : "yes (not counted for sight)") : "");
		}
		ImGui::EndTable();
	}

	void DrawPathDetails(const AI_DEBUG::Character& character)
	{
		const AI_DEBUG::Path& path = character.path;
		if (!path.have)
		{
			ImGui::TextDisabled("No locomotion.");
			return;
		}
		ImGui::Text("Moving: %s, speed %s (wants %s)%s", AI_DEBUG::MovementName(path.status), AI_DEBUG::SpeedName(path.speedNow), AI_DEBUG::SpeedName(path.speedWanted), path.fullRepath ? ", pathfinding afresh" : "");
		if (path.stuckFor > 0.0f)
			ImGui::Text("Without progress for %.1f s", path.stuckFor);
		ImGui::Text("Path: %d nodes%s, the current one %d; %d passed", path.totalNodes, path.totalNodes > path.nodeCount ? " (the first drawn)" : "", path.currentNode, path.passedCount);
		if (path.haveRequest)
		{
			ImGui::Text("Going to %.2f, %.2f, %.2f%s: %.1f m left", path.destination[0], path.destination[1], path.destination[2], path.destinationBackstage ? " (in the vents)" : "", path.remaining);
			ImGui::Text("Pathfinding: %s; following: %s; %d request(s)", AI_DEBUG::PathStatusName(path.pathStatus), AI_DEBUG::FollowStatusName(path.followStatus), path.requestCount);
		}
		else
			ImGui::TextDisabled("Not asked to go anywhere.");
		ImGui::Text("Corridor: %d gaps, %d corners", path.portalCount, path.cornerCount);
	}

	void DrawDetails(const AI_DEBUG::Snapshot& snapshot, const AI_DEBUG::Character& character)
	{
		const float now = snapshot.now;
		if (ImGui::CollapsingHeader("Who", ImGuiTreeNodeFlags_DefaultOpen))
		{
			ImGui::Text("%s - %s%s", character.name, DEBUG_MENU::ClassName(character.characterClass), character.dead ? " (dead)" : "");
			ImGui::Text("Unique id %u; entity 0x%08X in composite instance 0x%08X", character.id, character.entityId, character.compositeInstanceId);
			if (character.treeName[0])
				ImGui::Text("Behaviour tree: %s", character.treeName);
			Vector("Position", character.position);
			ImGui::Text("%.1f m from the camera", character.distance);
		}
		if (!character.read)
		{
			ImGui::TextDisabled("Its state could not be read.");
			return;
		}
		if (ImGui::CollapsingHeader("State", ImGuiTreeNodeFlags_DefaultOpen))
		{
			ImGui::Text("Health %d of %d (%s)", character.health, character.maxHealth, AI_DEBUG::HealthName(character.healthState));
			if (!character.player)
			{
				ImGui::Text("Awareness of the player: %s", character.awareness >= 0 ? AI_DEBUG::AwarenessName(character.awareness) : "- (not in the group of NPCs)");
				ImGui::Text("Alertness: %s; aggression: %s; mood: %s", AI_DEBUG::AlertnessName(character.alertness), AI_DEBUG::AggroName(character.aggro), AI_DEBUG::MoodName(character.mood));
				ImGui::Text("Branch: %s (before: %s)", AI_DEBUG::BranchName(character.branch), AI_DEBUG::BranchName(character.previousBranch));
				if (character.alien)
					ImGui::Text("Alien awareness: %s", AI_DEBUG::AlienAwarenessName(character.alienAwareness));
				if (character.spotting >= 0.0f)
					ImGui::Text("Spotting the player: %.0f%% of the way", character.spotting * 100.0f);
			}
			ImGui::Text("Sensed by its enemies: %s", AI_DEBUG::SensedName(character.sensed));
			char state[256] = {};
			auto add = [&state](bool on, const char* text) { if (on) { if (state[0]) strcat_s(state, ", "); strcat_s(state, text); } };
			add(character.crouched, "crouched");
			add(character.torch, "torch out");
			add(character.backstage, "backstage (in the vents)");
			add(character.backstageTransition, "going into or out of the vents");
			add(character.inVent, "in a vent");
			add(character.stunned, "stunned");
			add(character.invisible, "invisible to senses");
			if (state[0])
				ImGui::TextWrapped("Now: %s", state);
			if (character.player && character.flags[13] && character.hidingSince >= 0.0f)
				ImGui::Text("Hiding for %.1f s", now - character.hidingSince);
		}
		if (!character.player && ImGui::CollapsingHeader("Target", ImGuiTreeNodeFlags_DefaultOpen))
		{
			if (!character.haveTarget)
				ImGui::TextDisabled("No target.");
			else
				ImGui::Text("Target: %s", character.target >= 0 ? NameOf(snapshot, character.target) : "(not one of the level's characters)");
		}
		if (!character.detailed)
		{
			ImGui::TextDisabled("Too far from the camera for the rest to be read; it is read while it is selected.");
			return;
		}
		if (!character.player && ImGui::CollapsingHeader("Senses", ImGuiTreeNodeFlags_DefaultOpen))
			DrawSenses(snapshot, character);
		if (!character.player && ImGui::CollapsingHeader("Last known positions##details"))
			DrawLastKnownTable(snapshot, character);
		if (!character.player && ImGui::CollapsingHeader("View cones and hearing"))
			DrawConesAndHearing(character);
		if (ImGui::CollapsingHeader("Path"))
			DrawPathDetails(character);
		if (ImGui::CollapsingHeader("Flags and timers"))
		{
			int flags = 0;
			for (int i = 0; i < AI_DEBUG::kFlags; i++)
				if (character.flags[i])
				{
					ImGui::BulletText("%s", AI_DEBUG::FlagName(i));
					flags++;
				}
			if (!flags)
				ImGui::TextDisabled("No flags set.");
			ImGui::Separator();
			for (int i = 0; i < character.timerCount; i++)
			{
				const AI_DEBUG::Timer& timer = character.timers[i];
				ImGui::BulletText("%s: %.1f s of %.1f%s", AI_DEBUG::TimerName(timer.type), timer.remaining, timer.duration, timer.paused ? " (paused)" : "");
			}
			if (!character.timerCount)
				ImGui::TextDisabled("No timers running.");
		}
	}

	void DrawCharactersTab()
	{
		NameSwitches();
		const AI_DEBUG::Snapshot& snapshot = AI_DEBUG::Read();
		if (snapshot.characters.empty())
		{
			ImGui::TextDisabled("No characters (no level running).");
			return;
		}

		ImGui::SeparatorText("Draw over the game");
		SwitchRow("Labels over heads", g_labels, &g_labelsAll);
		SwitchRow("View cones", g_cones, &g_conesAll);
		SwitchRow("Hearing range", g_hearing, &g_hearingAll);
		SwitchRow("Paths", g_paths, &g_pathsAll);
		SwitchRow("Last known positions", g_lastKnown, &g_lastKnownAll);
		ImGui::TextDisabled("Otherwise for the selected character only.");

		ImGui::SeparatorText("Characters (click to select)");
		DrawTable(snapshot);

		const AI_DEBUG::Character* selected = nullptr;
		for (const AI_DEBUG::Character& character : snapshot.characters)
			if (character.selected)
				selected = &character;
		ImGui::SeparatorText("Selected");
		if (!selected)
		{
			ImGui::TextDisabled("Click a character above to see it in detail.");
			return;
		}
		DrawDetails(snapshot, *selected);
	}

	// ---- The Player tab ----

	void DrawPlayerTab()
	{
		NameSwitches();
		const AI_DEBUG::Snapshot& snapshot = AI_DEBUG::Read();
		const AI_DEBUG::Player& player = snapshot.player;
		if (player.character < 0)
		{
			ImGui::TextDisabled("No player (no level running).");
			return;
		}

		ImGui::SeparatorText("Light");
		if (player.haveLightMeter)
		{
			const ImU32 levelColours[] = { IM_COL32(90, 110, 255, 255), IM_COL32(230, 200, 60, 255), IM_COL32(255, 250, 200, 255) };
			const ImU32 colour = player.level >= 0 && player.level < 3 ? levelColours[player.level] : IM_COL32(200, 200, 200, 255);
			ImGui::PushStyleColor(ImGuiCol_Text, colour);
			ImGui::Text("%s", AI_DEBUG::LightLevelName(player.level));
			ImGui::PopStyleColor();
			if (!player.meterEnabled)
			{
				ImGui::SameLine();
				ImGui::PushTextWrapPos(0.0f);
				ImGui::TextDisabled("(the meter is off for now: the game turns it off a while after the player is detected)");
				ImGui::PopTextWrapPos();
			}
			const float ticks[2] = { snapshot.darkThreshold, snapshot.lightThreshold };
			char text[64];
			snprintf(text, sizeof(text), "%.3f", player.scaled);
			Meter(player.scaled, ticks, 2, colour, text);
			ImGui::Text("Brightness %.3f, through the AI's light curve %.3f", player.combined, player.scaled);
			ImGui::Text("Thresholds: dark below %.3f, fully lit from %.3f (plain brightness %.3f and %.3f)", snapshot.darkThreshold, snapshot.lightThreshold, snapshot.tintDarkThreshold, snapshot.tintLightThreshold);
			// Each sample: the light found there, and how high above the player's feet it was taken
			const float feet = snapshot.characters[player.character].position[1];
			for (int i = 0; i < 3; i++)
			{
				const float* rgb = player.samples[i];
				char id[32];
				snprintf(id, sizeof(id), "sample%d", i);
				ImGui::ColorButton(id, ImVec4(Clamp01(rgb[0]), Clamp01(rgb[1]), Clamp01(rgb[2]), 1.0f), ImGuiColorEditFlags_NoTooltip);
				ImGui::SameLine();
				ImGui::Text("At %+.2f m: %.3f, %.3f, %.3f (brightness %.3f)", player.samplePositions[i][1] - feet, rgb[0], rgb[1], rgb[2], 0.299f * rgb[0] + 0.587f * rgb[1] + 0.114f * rgb[2]);
			}
			SwitchBox("Show the sample points", g_lightSamples);
		}
		else
			ImGui::TextDisabled("No light meter (the game gives the player one a moment after they spawn).");

		ImGui::SeparatorText("Light meter tint");
		if (AI_DEBUG::LightTintAvailable())
		{
			SwitchBox("Tint the screen by light level", g_lightTint);
			ImGui::SliderFloat("Opacity", &g_tintOpacity, 0.0f, 1.0f, "%.2f");
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextDisabled("The game's own debug view: the screen tinted by how lit it is for the AI.");
			ImGui::PopTextWrapPos();
		}
		else
			ImGui::TextDisabled("Not available on this build of the game.");

		ImGui::SeparatorText("Stealth");
		if (player.haveStealth)
		{
			ImGui::Text("Exposure %.2f, stance %.2f, cover %.2f, movement %.2f", player.exposure, player.stance, player.cover, player.movement);
		}
		ImGui::Text("Hiding: %s", player.hiding ? "yes" : "no");
		if (player.hiding)
		{
			const AI_DEBUG::Character& character = snapshot.characters[player.character];
			if (character.hidingSince >= 0.0f)
			{
				ImGui::SameLine();
				ImGui::Text("(for %.1f s)", snapshot.now - character.hidingSince);
			}
		}
		const float ticks[1] = { 1.0f };
		char text[128];
		if (player.spotting >= 0.0f)
			snprintf(text, sizeof(text), "%.0f%% - %s", player.spotting * 100.0f, NameOf(snapshot, player.spotter));
		else
			snprintf(text, sizeof(text), "nobody targets the player");
		ImGui::TextUnformatted("Closest to spotting the player (1 = spotted):");
		Meter((std::max)(0.0f, player.spotting), ticks, 1, player.spotting >= 1.0f ? IM_COL32(255, 60, 40, 255) : IM_COL32(230, 200, 60, 255), text);
		if (player.alienSpotting >= 0.0f)
			ImGui::Text("The alien (not counted by the game here): %.0f%%", player.alienSpotting * 100.0f);
	}

	// ---- The Alien tab ----

	void DrawAlienTab()
	{
		NameSwitches();
		const AI_DEBUG::Snapshot& snapshot = AI_DEBUG::Read();
		if (snapshot.aliens.empty())
		{
			ImGui::TextDisabled("No alien (none in this level, or not spawned yet).");
			return;
		}
		// The selected alien, or the first
		const AI_DEBUG::Alien* alien = &snapshot.aliens[0];
		for (const AI_DEBUG::Alien& each : snapshot.aliens)
			if (snapshot.characters[each.character].selected)
				alien = &each;
		const AI_DEBUG::Character& character = snapshot.characters[alien->character];
		ImGui::Text("%s%s", character.name, snapshot.aliens.size() > 1 ? " (select another alien in the Characters tab to see it)" : "");
		if (!alien->read)
		{
			ImGui::TextDisabled("Could not be read.");
			return;
		}
		ImGui::Text("Setup: %s; branch: %s", alien->configName[0] ? alien->configName : "?", AI_DEBUG::BranchName(character.branch));

		ImGui::SeparatorText("Menace");
		if (alien->haveGauge)
		{
			char text[96];
			snprintf(text, sizeof(text), "%.0f%% (%s)", alien->gauge * 100.0f, AI_DEBUG::GaugeLevelName(alien->gaugeLevel));
			Meter(alien->gauge, alien->gaugeThresholds + 1, AI_DEBUG::kGaugeLevels - 1, alien->menacing ? IM_COL32(255, 60, 40, 255) : IM_COL32(255, 170, 40, 255), text);
			ImGui::Text("Thresholds: %.2f, %.2f, %.2f, %.2f, %.2f", alien->gaugeThresholds[1], alien->gaugeThresholds[2], alien->gaugeThresholds[3], alien->gaugeThresholds[4], alien->gaugeThresholds[5]);
		}
		ImGui::Text("%s%s%s", alien->menacing ? "Menacing" : "Not menacing", alien->paused ? " (paused)" : "", alien->seenInCamera ? ", has been in the player's view" : "");
		if (alien->menaceTimerRemaining > 0.0f)
			ImGui::Text("Menace timer: %.1f s of %.1f left", alien->menaceTimerRemaining, alien->menaceTimerDuration);
		if (alien->maxMenaces >= 0)
			ImGui::Text("Menaced the player %d times of %d", alien->menaces, alien->maxMenaces);
		else
			ImGui::Text("Menaced the player %d times", alien->menaces);
		ImGui::Text("Withdrawal: %s%s", AI_DEBUG::WithdrawName(alien->withdrawState), alien->withdrawAfterNextJob ? " (withdraws after its next job)" : "");

		ImGui::SeparatorText("Ambushes and killtraps");
		ImGui::Text("Decision: %s%s", AI_DEBUG::AmbushName(alien->ambushDecision), alien->ambushing ? ", ambushing" : "");
		if (alien->scriptKilltraps || alien->treeKilltraps)
			ImGui::Text("Killtraps asked for by %s", alien->scriptKilltraps && alien->treeKilltraps ? "a script and its tree" : alien->scriptKilltraps ? "a script" : "its tree");

		ImGui::SeparatorText("Sweep area");
		if (alien->haveSweep)
		{
			ImGui::Text("%s", AI_DEBUG::SweepTypeName(alien->sweepType));
			Vector("Point", alien->sweepPosition);
			if (alien->sweepType == 1)
				ImGui::Text("Radius %.1f m", alien->sweepRadius);
		}
		else
			ImGui::TextDisabled("No point to sweep: it sweeps round the player.");
		if (alien->haveBox)
			ImGui::Text("Box: %.1f m by %.1f m round %.1f, %.1f, %.1f", alien->boxHalfLength * 2.0f, alien->boxHalfWidth * 2.0f, alien->boxCentre[0], alien->boxCentre[1], alien->boxCentre[2]);
		SwitchBox("Draw the sweep area", g_sweepArea);

		ImGui::SeparatorText("Vents");
		ImGui::Text("%s; %d vents known", character.backstage ? "Backstage (in the vents)" : "Out of the vents", alien->ventCount);
		if (alien->haveBackstageTarget)
			Vector("Heading for", alien->backstageTarget);
		if (alien->haveVentNearAlien)
			Vector("Best vent near the alien", alien->ventNearAlien);
		if (alien->haveVentNearPlayer)
			Vector("Best vent near the player", alien->ventNearPlayer);
		SwitchBox("Draw where it heads in the vents, and the best vents", g_backstageTarget);
	}

	const bool g_overlayAdded = DEBUG_MENU::AddOverlay(10, DrawOverlays);
	const bool g_charactersTab = DEBUG_MENU::AddTab("Characters", 10, DrawCharactersTab, "Characters");
	const bool g_playerTab = DEBUG_MENU::AddTab("Player", 30, DrawPlayerTab, "Characters");
	const bool g_alienTab = DEBUG_MENU::AddTab("Alien", 40, DrawAlienTab, "Characters");
}

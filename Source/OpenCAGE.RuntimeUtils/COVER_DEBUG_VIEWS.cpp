#include "COVER_DEBUG.h"
#include "DEBUG_MENU.h"
#include "NAV_DEBUG.h"

#include <algorithm>
#include <atomic>
#include <cfloat>
#include <cmath>
#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>
#include <unordered_map>
#include <vector>

#include <imgui.h>

/*
	The Cover & Jobs tab (in the World group) and the drawing over the game it switches on, from what COVER_DEBUG reads
	(see COVER_DEBUG.h): the level's cover segments and slots and who is in them, the characters the cover system
	follows, the baked spotting, crawl-space spotting and assault positions and the jobs made from them, and every job
	the AI has with its tasks and who is doing them. Every switch and number is a named one and every list has a command
	(all "nav."), so OpenCAGE can drive the tab and the main session can test it over the Live Link.
*/
namespace
{
	// ---- Named switches and numbers ----
	std::atomic<bool> g_cover = false;           // nav.cover: cover segments near the camera
	std::atomic<bool> g_slots = false;           // nav.cover_slots: their slots, by who has them
	std::atomic<bool> g_links = false;           // nav.cover_links: the links round corners and in line
	std::atomic<bool> g_coverLabels = false;     // nav.cover_labels: segment uids and heights
	std::atomic<bool> g_spotting = false;        // nav.jobs_spotting: baked spotting positions
	std::atomic<bool> g_crawl = false;           // nav.jobs_crawl: baked crawl-space spotting positions
	std::atomic<bool> g_assault = false;         // nav.jobs_assault: baked assault positions
	std::atomic<bool> g_activeOnly = false;      // nav.jobs_active_only: only positions in cells near the player
	std::atomic<bool> g_tasks = false;           // nav.jobs_tasks: every job's tasks, by state, with who does them
	std::atomic<float> g_radius = 25.0f;         // nav.cover_radius: how far from the camera to draw

	int g_selectedSegment = 0;                   // a segment chosen in the tab (its uid)
	int g_jobType = -1;                          // the job list's type shown (-1 all)
	char g_jobFilter[32] = "";

	void SwitchBox(const char* label, std::atomic<bool>& on)
	{
		bool value = on;
		if (ImGui::Checkbox(label, &value))
			on = value;
	}

	std::string Format(const char* format, ...)
	{
		char text[2048];
		va_list args;
		va_start(args, format);
		vsnprintf(text, sizeof(text), format, args);
		va_end(args);
		return text;
	}

	float Distance(const float a[3], const float b[3])
	{
		const float dx = a[0] - b[0], dy = a[1] - b[1], dz = a[2] - b[2];
		return std::sqrt(dx * dx + dy * dy + dz * dz);
	}

	ImU32 Rgb(int r, int g, int b, int a = 255)
	{
		return IM_COL32(r, g, b, a);
	}

	ImVec4 Colour4(ImU32 colour)
	{
		return ImGui::ColorConvertU32ToFloat4(colour);
	}

	void Middle(const COVER_DEBUG::Segment& segment, float out[3])
	{
		for (int axis = 0; axis < 3; axis++)
			out[axis] = (segment.left[axis] + segment.right[axis]) * 0.5f;
	}

	const DEBUG_MENU::Character* CharacterByObject(const void* object)
	{
		if (!object)
			return nullptr;
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
			if (character.object == object)
				return &character;
		return nullptr;
	}

	// This frame's characters by their script entity (two ids)
	const DEBUG_MENU::Character* CharacterByHandle(uint32_t entity, uint32_t parent)
	{
		if (!entity && !parent)
			return nullptr;
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
		{
			uint32_t e = 0, p = 0;
			if (COVER_DEBUG::CharacterHandle(character.object, e, p) && e == entity && p == parent)
				return &character;
		}
		return nullptr;
	}

	// A character as the other tabs name it: its name and class
	std::string CharacterLabel(const DEBUG_MENU::Character& character)
	{
		return character.name + " (" + DEBUG_MENU::ClassName(character.characterClass) + ")";
	}

	std::string Who(const DEBUG_MENU::Character* character)
	{
		return character ? CharacterLabel(*character) : std::string();
	}

	std::string WhoByHandle(uint32_t entity, uint32_t parent)
	{
		if (!entity && !parent)
			return "-";
		const DEBUG_MENU::Character* character = CharacterByHandle(entity, parent);
		return character ? CharacterLabel(*character) : Format("0x%08X/0x%08X", entity, parent);
	}

	std::string WhoByObject(const void* object)
	{
		if (!object)
			return "-";
		const DEBUG_MENU::Character* character = CharacterByObject(object);
		return character ? CharacterLabel(*character) : std::string("(not a character now)");
	}

	// A segment uid (0 is none: they count from 1), a slot uid (none is all bits set) and a reservation, as words
	std::string UidText(int uid)
	{
		return uid ? std::to_string(uid) : std::string("-");
	}

	std::string SlotUidText(uint32_t slot)
	{
		return slot == COVER_DEBUG::kNoSlot ? std::string("none") : std::to_string(slot);
	}

	std::string ReservedText(const COVER_DEBUG::Tracked& tracked)
	{
		std::string text;
		for (int i = 0; i < 2; i++)
			if (tracked.haveReserved[i] && tracked.reserved[i] != COVER_DEBUG::kNoSlot)
				text += (text.empty() ? "" : ", ") + std::to_string(tracked.reserved[i]);
		return text.empty() ? std::string("none") : text;
	}

	// The segments one is linked to, as words: "corner: left 12; in line: right 490" (only the links it has)
	std::string LinksText(const COVER_DEBUG::Segment& segment)
	{
		const auto sides = [](int left, int right)
		{
			std::string text;
			if (left)
				text += Format("left %d", left);
			if (right)
				text += (text.empty() ? "" : ", ") + Format("right %d", right);
			return text;
		};
		const std::string corner = sides(segment.leftCorner, segment.rightCorner), inLine = sides(segment.leftColinear, segment.rightColinear);
		std::string text;
		if (!corner.empty())
			text += "corner: " + corner;
		if (!inLine.empty())
			text += (text.empty() ? "" : "; ") + std::string("in line: ") + inLine;
		return text.empty() ? std::string("none") : text;
	}

	// The segment a followed character is on, with the ones round its corner and in line with it
	std::string TrackedSegmentText(const COVER_DEBUG::Tracked& tracked)
	{
		if (!tracked.inCover)
			return "not in cover";
		std::string text = std::to_string(tracked.segment);
		if (tracked.corner)
			text += Format(", corner %d", tracked.corner);
		if (tracked.colinear)
			text += Format(", in line %d", tracked.colinear);
		return text;
	}

	ImU32 SlotColour(int status)
	{
		return status == 0 ? Rgb(70, 210, 90) : status == 1 ? Rgb(255, 170, 40) : Rgb(240, 60, 60);
	}

	// A segment's colour, kept clear of the nav mesh's blues (drawn with it): pink standing, cyan low, grey disabled, white
	// the one chosen in the tab
	ImU32 SegmentColour(const COVER_DEBUG::Segment& segment, bool selected)
	{
		if (selected)
			return Rgb(255, 255, 255);
		if (!segment.enabled)
			return Rgb(130, 130, 130, 200);
		return (segment.flags & COVER_DEBUG::kLowCover) ? Rgb(60, 220, 230) : Rgb(255, 110, 190);
	}

	// Labels wait until the overlay's lines are all drawn, so no line crosses one
	struct Label
	{
		float at[3];
		std::string text;
		ImU32 colour;
		float offset;
	};
	std::vector<Label> g_labels;

	void QueueText(const float at[3], const std::string& text, ImU32 colour, float offset = 0.0f)
	{
		Label label;
		memcpy(label.at, at, sizeof(label.at));
		label.text = text;
		label.colour = colour;
		label.offset = offset;
		g_labels.push_back(std::move(label));
	}

	void DrawQueuedText(ImDrawList* drawList)
	{
		for (const Label& label : g_labels)
			DEBUG_MENU::WorldText(drawList, label.at, label.text.c_str(), label.colour, label.offset);
		g_labels.clear();
	}

	ImU32 TaskColour(int state)
	{
		switch (state)
		{
		case 1: return Rgb(150, 150, 150);        // not in use
		case 2: return Rgb(255, 210, 60);         // assigned
		case 3:
		case 4: return Rgb(60, 200, 255);         // about to move, moving
		case 5:
		case 6: return Rgb(255, 120, 40);         // interrupted
		case 7: return Rgb(70, 230, 100);         // performing
		default: return Rgb(110, 110, 110);
		}
	}

	ImU32 AutoColour(int kind, bool active)
	{
		const int a = active ? 255 : 120;
		return kind == COVER_DEBUG::kSpotting ? Rgb(80, 160, 255, a) : kind == COVER_DEBUG::kCrawl ? Rgb(190, 100, 255, a) : Rgb(255, 150, 50, a);
	}

	// How many slots of a segment are free, reserved and occupied
	void SlotCounts(const COVER_DEBUG::Cover& cover, const COVER_DEBUG::Segment& segment, int counts[3])
	{
		counts[0] = counts[1] = counts[2] = 0;
		for (int i = 0; i < segment.slotCount; i++)
		{
			const int index = segment.firstSlot + i;
			if (index < 0 || index >= static_cast<int>(cover.slots.size()))
				break;
			const int status = cover.slots[index].status;
			if (status >= 0 && status < 3)
				counts[status]++;
		}
	}

	std::string SegmentText(const COVER_DEBUG::Cover& cover, const COVER_DEBUG::Segment& segment, bool slots)
	{
		int counts[3];
		SlotCounts(cover, segment, counts);
		// The level's cover is made by the bake, not placed in its scripts: no shipped segment has a script entity
		const std::string entity = segment.entity || segment.parent ? Format("entity 0x%08X instance 0x%08X", segment.entity, segment.parent) : std::string("no script entity");
		const std::string fileIndex = segment.cathodeIndex >= 0 ? std::to_string(segment.cathodeIndex) : std::string("none");
		std::string text = Format("segment %d: %s, height %.2f m, length %.2f m, %s, flags 0x%04X (%s), slots %d (free %d, reserved %d, occupied %d), "
			"linked segments (%s), traversal %s, file index %s, %s, left %.2f,%.2f,%.2f right %.2f,%.2f,%.2f normal %.2f,%.2f,%.2f\n",
			segment.uid, (segment.flags & COVER_DEBUG::kLowCover) ? "low" : "standing", segment.height, segment.length, segment.enabled ? "enabled" : "DISABLED",
			segment.flags, COVER_DEBUG::SegmentFlagsText(segment.flags).c_str(), segment.slotCount, counts[0], counts[1], counts[2], LinksText(segment).c_str(),
			segment.traversal ? std::to_string(segment.traversal).c_str() : "none", fileIndex.c_str(), entity.c_str(), segment.left[0], segment.left[1], segment.left[2],
			segment.right[0], segment.right[1], segment.right[2], segment.normal[0], segment.normal[1], segment.normal[2]);
		if (slots)
			for (int i = 0; i < segment.slotCount; i++)
			{
				const int index = segment.firstSlot + i;
				if (index < 0 || index >= static_cast<int>(cover.slots.size()))
					break;
				const COVER_DEBUG::Slot& slot = cover.slots[index];
				text += Format("  slot %d at %.2f: %s, flags 0x%04X (%s), aim 0x%04X/0x%08X, by %s\n", slot.uid, slot.along, COVER_DEBUG::SlotStatusName(slot.status), slot.flags,
					COVER_DEBUG::SlotFlagsText(slot.flags).c_str(), slot.aimHorizontal, slot.aimVertical, WhoByHandle(slot.occupantEntity, slot.occupantParent).c_str());
			}
		return text;
	}

	// ---- Drawing over the game ----

	void DrawCover(ImDrawList* drawList, const COVER_DEBUG::Cover& cover, const float centre[3])
	{
		const float radius = g_radius;
		int labels = 0;
		for (const COVER_DEBUG::Segment& segment : cover.segments)
		{
			float middle[3];
			Middle(segment, middle);
			const float d = Distance(centre, middle);
			if (d > radius + segment.length * 0.5f)
				continue;
			const bool low = (segment.flags & COVER_DEBUG::kLowCover) != 0;
			const bool selected = segment.uid == g_selectedSegment;
			const ImU32 colour = SegmentColour(segment, selected);
			const float thickness = selected ? 3.0f : 1.5f;
			if (g_cover)
			{
				// The wall: its foot, its two ends up to its height, its top
				const float leftTop[3] = { segment.left[0], segment.left[1] + segment.height, segment.left[2] };
				const float rightTop[3] = { segment.right[0], segment.right[1] + segment.height, segment.right[2] };
				DEBUG_MENU::WorldLine(drawList, segment.left, segment.right, colour, thickness);
				DEBUG_MENU::WorldLine(drawList, segment.left, leftTop, colour, 1.0f);
				DEBUG_MENU::WorldLine(drawList, segment.right, rightTop, colour, 1.0f);
				DEBUG_MENU::WorldLine(drawList, leftTop, rightTop, colour, thickness);
				// Which way it faces: the side the AI takes cover on
				const float from[3] = { middle[0], middle[1] + 0.1f, middle[2] };
				const float to[3] = { from[0] + segment.normal[0] * 0.5f, from[1] + segment.normal[1] * 0.5f, from[2] + segment.normal[2] * 0.5f };
				DEBUG_MENU::WorldArrow(drawList, from, to, colour, 1.0f);
				if ((g_coverLabels || selected) && labels < 60 && d < 15.0f)
				{
					labels++;
					const float at[3] = { middle[0], middle[1] + segment.height + 0.2f, middle[2] };
					QueueText(at, Format("#%d %s %.1f m%s", segment.uid, low ? "low" : "standing", segment.height, segment.enabled ? "" : " (off)"), colour);
				}
			}
			if (g_links)
			{
				struct LinkEnd { int uid; const float* from; ImU32 colour; };
				const LinkEnd ends[] = { { segment.leftCorner, segment.left, Rgb(255, 220, 60) }, { segment.rightCorner, segment.right, Rgb(255, 220, 60) },
					{ segment.leftColinear, segment.left, Rgb(70, 220, 90) }, { segment.rightColinear, segment.right, Rgb(70, 220, 90) } };
				for (const LinkEnd& end : ends)
				{
					const COVER_DEBUG::Segment* other = COVER_DEBUG::SegmentByUid(end.uid);
					if (!other)
						continue;
					const float* to = Distance(end.from, other->left) < Distance(end.from, other->right) ? other->left : other->right;
					const float a[3] = { end.from[0], end.from[1] + 0.3f, end.from[2] };
					const float b[3] = { to[0], to[1] + 0.3f, to[2] };
					DEBUG_MENU::WorldLine(drawList, a, b, end.colour, 2.0f);
				}
			}
			if (g_slots)
				for (int i = 0; i < segment.slotCount; i++)
				{
					const int index = segment.firstSlot + i;
					if (index < 0 || index >= static_cast<int>(cover.slots.size()))
						break;
					const COVER_DEBUG::Slot& slot = cover.slots[index];
					float at[3];
					if (!COVER_DEBUG::SlotPosition(slot, at))
						continue;
					at[1] += 0.05f;
					DEBUG_MENU::WorldCircle(drawList, at, 0.2f, SlotColour(slot.status), 12, slot.status ? 2.5f : 1.5f);
					if (slot.status && d < 15.0f)
						QueueText(at, Format("%s %s", COVER_DEBUG::SlotStatusName(slot.status), WhoByHandle(slot.occupantEntity, slot.occupantParent).c_str()), SlotColour(slot.status), -12.0f);
				}
		}
		// The characters the cover system follows, to their slots
		if (g_slots)
			for (int i = 0; i < cover.trackedCount; i++)
			{
				const COVER_DEBUG::Tracked& tracked = cover.tracked[i];
				const DEBUG_MENU::Character* character = CharacterByHandle(tracked.entity, tracked.parent);
				if (!character || !tracked.inCover || tracked.slot >= cover.slots.size())
					continue;
				float at[3];
				if (!COVER_DEBUG::SlotPosition(cover.slots[tracked.slot], at))
					continue;
				const float from[3] = { character->position[0], character->position[1] + 1.0f, character->position[2] };
				DEBUG_MENU::WorldLine(drawList, from, at, Rgb(255, 255, 255, 200), 1.0f);
			}
	}

	void DrawAutoJobs(ImDrawList* drawList, const COVER_DEBUG::Jobs& jobs, const float centre[3])
	{
		const float radius = g_radius;
		int labels = 0;
		for (const COVER_DEBUG::AutoJob& job : jobs.autoJobs)
		{
			if ((job.kind == COVER_DEBUG::kSpotting && !g_spotting) || (job.kind == COVER_DEBUG::kCrawl && !g_crawl) || (job.kind == COVER_DEBUG::kAssault && !g_assault))
				continue;
			if (g_activeOnly && !job.active && !jobs.useAll[job.kind])
				continue;
			const float d = Distance(centre, job.position);
			if (d > radius)
				continue;
			const ImU32 colour = AutoColour(job.kind, job.active || jobs.useAll[job.kind]);
			DEBUG_MENU::WorldCross(drawList, job.position, 0.25f, colour, 2.0f);
			if (job.kind == COVER_DEBUG::kAssault)
			{
				// Facing its yaw (forward = sine, 0, cosine, as the characters face)
				const float to[3] = { job.position[0] + std::sin(job.yaw) * 0.8f, job.position[1], job.position[2] + std::cos(job.yaw) * 0.8f };
				DEBUG_MENU::WorldArrow(drawList, job.position, to, colour, 1.5f);
			}
			else
				DEBUG_MENU::WorldLine(drawList, job.position, job.task, colour, 1.0f);
			if (job.job)
				DEBUG_MENU::WorldCircle(drawList, job.position, 0.35f, colour, 12, 2.0f);
			if (labels < 40 && d < 12.0f)
			{
				labels++;
				const COVER_DEBUG::Job* made = COVER_DEBUG::JobByAddress(job.job);
				std::string text = COVER_DEBUG::AutoKindName(job.kind);
				if (made && !made->tasks.empty())
					text += Format(": %s", COVER_DEBUG::TaskStateName(made->tasks.front().state));
				else if (job.job)
					text += ": job made";
				QueueText(job.position, text, colour, -10.0f);
			}
		}
	}

	void DrawTasks(ImDrawList* drawList, const COVER_DEBUG::Jobs& jobs, const float centre[3])
	{
		int labels = 0;
		for (const COVER_DEBUG::Job& job : jobs.jobs)
		{
			if (g_jobType >= 0 && job.type != g_jobType)
				continue;
			for (const COVER_DEBUG::Task& task : job.tasks)
			{
				const float d = Distance(centre, task.position);
				if (d > g_radius)
					continue;
				const ImU32 colour = TaskColour(task.state);
				DEBUG_MENU::WorldCircle(drawList, task.position, 0.25f, colour, 4, 2.0f);
				if (task.hasOrientation)
				{
					const float to[3] = { task.position[0] + std::sin(task.orientation) * 0.6f, task.position[1], task.position[2] + std::cos(task.orientation) * 0.6f };
					DEBUG_MENU::WorldArrow(drawList, task.position, to, colour, 1.0f);
				}
				const DEBUG_MENU::Character* doer = CharacterByObject(task.owningCharacter ? task.owningCharacter : task.reservedCharacter);
				if (doer)
				{
					const float from[3] = { doer->position[0], doer->position[1] + 1.0f, doer->position[2] };
					DEBUG_MENU::WorldLine(drawList, from, task.position, colour, 1.0f);
				}
				if (labels < 40 && (d < 12.0f || doer))
				{
					labels++;
					QueueText(task.position, Format("%s: %s%s%s", COVER_DEBUG::JobTypeName(job.type), COVER_DEBUG::TaskStateName(task.state), doer ? " - " : "",
						doer ? CharacterLabel(*doer).c_str() : ""), colour, -10.0f);
				}
			}
		}
	}

	void DrawOverlay(ImDrawList* drawList)
	{
		const bool coverWanted = g_cover || g_slots || g_links;
		const bool jobsWanted = g_spotting || g_crawl || g_assault || g_tasks;
		if (!coverWanted && !jobsWanted)
			return;
		float centre[3];
		if (!DEBUG_MENU::CameraPosition(centre))
			return;
		g_labels.clear();
		if (coverWanted)
			DrawCover(drawList, COVER_DEBUG::ReadCover(g_slots), centre);
		if (jobsWanted)
		{
			const COVER_DEBUG::Jobs& jobs = COVER_DEBUG::ReadJobs(g_tasks);
			DrawAutoJobs(drawList, jobs, centre);
			if (g_tasks)
				DrawTasks(drawList, jobs, centre);
		}
		DrawQueuedText(drawList);
	}

	// ---- The tab ----

	void DrawCoverSection()
	{
		const COVER_DEBUG::Cover& cover = COVER_DEBUG::ReadCover();
		SwitchBox("Segments", g_cover);
		ImGui::SameLine();
		SwitchBox("Slots", g_slots);
		ImGui::SameLine();
		SwitchBox("Links", g_links);
		ImGui::SameLine();
		SwitchBox("Labels", g_coverLabels);
		float radius = g_radius;
		ImGui::SetNextItemWidth(160.0f * DEBUG_MENU::Scale());
		if (ImGui::SliderFloat("Draw distance", &radius, 2.0f, 100.0f, "%.0f m"))
			g_radius = radius;
		if (!cover.have)
		{
			ImGui::TextDisabled("The game has no cover system (no level running).");
			return;
		}
		if (!cover.loaded)
		{
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextColored(ImVec4(1.0f, 0.4f, 0.4f, 1.0f), "The cover system has not loaded the level's data (its state's cover file is missing or would not load).");
			ImGui::PopTextWrapPos();
			return;
		}
		int low = 0, disabled = 0, slotCounts[3] = {};
		bool anyEntity = false;
		for (const COVER_DEBUG::Segment& segment : cover.segments)
		{
			low += (segment.flags & COVER_DEBUG::kLowCover) != 0;
			disabled += !segment.enabled;
			anyEntity = anyEntity || segment.entity || segment.parent;
		}
		for (const COVER_DEBUG::Slot& slot : cover.slots)
			if (slot.status >= 0 && slot.status < 3)
				slotCounts[slot.status]++;
		ImGui::TextWrapped("%d segments (%d low, %d standing, %d disabled), %d slots (%d free, %d reserved, %d occupied), %d traversals.", static_cast<int>(cover.segments.size()),
			low, static_cast<int>(cover.segments.size()) - low, disabled, static_cast<int>(cover.slots.size()), slotCounts[0], slotCounts[1], slotCounts[2],
			static_cast<int>(cover.traversals.size()));
		ImGui::PushTextWrapPos(0.0f);
		ImGui::TextDisabled("Pink: standing cover, cyan: low, grey: disabled, white: the one chosen below; the arrow is the side it is used from. Slots: green free, "
			"amber reserved, red occupied. The list holds the segments nearest the camera (up to 200), nearest first, whatever the draw distance: that only limits "
			"the drawing.");
		ImGui::PopTextWrapPos();

		float centre[3] = {};
		if (DEBUG_MENU::CameraPosition(centre))
		{
			struct Row { const COVER_DEBUG::Segment* segment; float distance; };
			std::vector<Row> rows;
			for (const COVER_DEBUG::Segment& segment : cover.segments)
			{
				float middle[3];
				Middle(segment, middle);
				rows.push_back({ &segment, Distance(centre, middle) });
			}
			std::stable_sort(rows.begin(), rows.end(), [](const Row& a, const Row& b) { return a.distance < b.distance; });
			// The script entity column only when a segment has one (the shipped levels' cover is baked, none has). 14 rows, the
			// header and a sideways scroll bar for when the columns are wider than the window (the uid kept in view)
			const float rowHeight = ImGui::GetTextLineHeight() + ImGui::GetStyle().CellPadding.y * 2.0f + 1.0f;
			const ImGuiTableFlags flags = ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_Resizable | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY | ImGuiTableFlags_SizingFixedFit;
			if (ImGui::BeginTable("segments", anyEntity ? 8 : 7, flags, ImVec2(0.0f, rowHeight * 15.0f + ImGui::GetStyle().ScrollbarSize + 4.0f)))
			{
				ImGui::TableSetupScrollFreeze(1, 1);
				ImGui::TableSetupColumn("Uid");
				ImGui::TableSetupColumn("Kind");
				ImGui::TableSetupColumn("Height / length");
				ImGui::TableSetupColumn("Slots free/res/occ");
				ImGui::TableSetupColumn("Linked segments");
				ImGui::TableSetupColumn("Distance");
				if (anyEntity)
					ImGui::TableSetupColumn("Entity / instance");
				ImGui::TableSetupColumn("Flags");
				ImGui::TableHeadersRow();
				for (size_t i = 0; i < rows.size() && i < 200; i++)
				{
					const COVER_DEBUG::Segment& segment = *rows[i].segment;
					int counts[3];
					SlotCounts(cover, segment, counts);
					ImGui::TableNextRow();
					ImGui::TableNextColumn();
					ImGui::PushID(segment.uid);
					if (ImGui::Selectable(std::to_string(segment.uid).c_str(), g_selectedSegment == segment.uid, ImGuiSelectableFlags_SpanAllColumns))
						g_selectedSegment = g_selectedSegment == segment.uid ? 0 : segment.uid;
					ImGui::PopID();
					ImGui::TableNextColumn();
					ImGui::Text("%s%s", (segment.flags & COVER_DEBUG::kLowCover) ? "low" : "standing", segment.enabled ? "" : " (off)");
					ImGui::TableNextColumn();
					ImGui::Text("%.2f / %.2f m", segment.height, segment.length);
					ImGui::TableNextColumn();
					ImGui::Text("%d/%d/%d", counts[0], counts[1], counts[2]);
					ImGui::TableNextColumn();
					if (segment.leftCorner || segment.rightCorner || segment.leftColinear || segment.rightColinear)
						ImGui::TextUnformatted(LinksText(segment).c_str());
					else
						ImGui::TextDisabled("none");
					ImGui::TableNextColumn();
					ImGui::Text("%.1f m", rows[i].distance);
					if (anyEntity)
					{
						ImGui::TableNextColumn();
						if (segment.entity || segment.parent)
							ImGui::Text("0x%08X / 0x%08X", segment.entity, segment.parent);
						else
							ImGui::TextDisabled("-");
					}
					ImGui::TableNextColumn();
					ImGui::Text("0x%04X", segment.flags);
				}
				ImGui::EndTable();
			}
		}
		if (const COVER_DEBUG::Segment* segment = COVER_DEBUG::SegmentByUid(g_selectedSegment))
			ImGui::TextWrapped("%s", SegmentText(cover, *segment, true).c_str());

		ImGui::Text("Characters the cover system follows: %d", cover.trackedCount);
		if (cover.trackedCount && ImGui::BeginTable("tracked", 6, ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_SizingFixedFit))
		{
			ImGui::TableSetupColumn("Character");
			ImGui::TableSetupColumn("Segment");
			ImGui::TableSetupColumn("Slot");
			ImGui::TableSetupColumn("Reserved slots");
			ImGui::TableSetupColumn("Stance");
			ImGui::TableSetupColumn("Facing");
			ImGui::TableHeadersRow();
			for (int i = 0; i < cover.trackedCount; i++)
			{
				const COVER_DEBUG::Tracked& tracked = cover.tracked[i];
				ImGui::TableNextRow();
				ImGui::TableNextColumn();
				ImGui::TextUnformatted(WhoByHandle(tracked.entity, tracked.parent).c_str());
				ImGui::TableNextColumn();
				if (tracked.inCover)
					ImGui::TextUnformatted(TrackedSegmentText(tracked).c_str());
				else
					ImGui::TextDisabled("not in cover");
				ImGui::TableNextColumn();
				if (tracked.slot == COVER_DEBUG::kNoSlot)
					ImGui::TextDisabled("none");
				else
					ImGui::Text("%u", tracked.slot);
				ImGui::TableNextColumn();
				const std::string reserved = ReservedText(tracked);
				if (reserved == "none")
					ImGui::TextDisabled("none");
				else
					ImGui::TextUnformatted(reserved.c_str());
				// Out of cover the game keeps the last stance and facing it had (or none ever set): not shown
				ImGui::TableNextColumn();
				if (tracked.inCover)
					ImGui::TextUnformatted(COVER_DEBUG::StanceName(tracked.stance));
				else
					ImGui::TextDisabled("-");
				ImGui::TableNextColumn();
				if (tracked.inCover)
					ImGui::TextUnformatted(COVER_DEBUG::FacingName(tracked.facing));
				else
					ImGui::TextDisabled("-");
			}
			ImGui::EndTable();
		}
	}

	void DrawPositionsSection()
	{
		const COVER_DEBUG::Jobs& jobs = COVER_DEBUG::ReadJobs();
		SwitchBox("Spotting", g_spotting);
		ImGui::SameLine();
		SwitchBox("Crawl-space spotting", g_crawl);
		ImGui::SameLine();
		SwitchBox("Assault", g_assault);
		ImGui::SameLine();
		SwitchBox("Active cells only", g_activeOnly);
		if (!jobs.have)
		{
			ImGui::TextDisabled("The game has no job centre (no level running).");
			return;
		}
		for (int kind = 0; kind < COVER_DEBUG::kAutoKinds; kind++)
		{
			const COVER_DEBUG::Grid& grid = jobs.grids[kind];
			int made = 0;
			for (const COVER_DEBUG::AutoJob& job : jobs.autoJobs)
				made += job.kind == kind && job.job != 0;
			ImGui::TextColored(Colour4(AutoColour(kind, true)), "%s:", COVER_DEBUG::AutoKindName(kind));
			ImGui::SameLine();
			if (!grid.have)
				ImGui::TextDisabled("none loaded");
			else
				ImGui::TextWrapped("%d positions in %d x %d cells of %.1f m from %.1f, %.1f; %d cells active%s; %d jobs made", grid.items, grid.xCells, grid.zCells, grid.unit,
					grid.minX, grid.minZ, grid.activeCells, jobs.useAll[kind] ? " (the level uses them all)" : "", made);
		}
		ImGui::PushTextWrapPos(0.0f);
		ImGui::TextDisabled("A job is made for a position only while its cell is near the player (unless the level uses them all). Bright: in an active cell; "
			"a ring: a job is made; the line goes to where the AI stands to look.");
		ImGui::PopTextWrapPos();
		float centre[3] = {};
		if (!DEBUG_MENU::CameraPosition(centre))
			return;
		struct Row { const COVER_DEBUG::AutoJob* job; float distance; };
		std::vector<Row> rows;
		for (const COVER_DEBUG::AutoJob& job : jobs.autoJobs)
			rows.push_back({ &job, Distance(centre, job.position) });
		std::stable_sort(rows.begin(), rows.end(), [](const Row& a, const Row& b) { return a.distance < b.distance; });
		const ImGuiTableFlags flags = ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_Resizable | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY | ImGuiTableFlags_SizingFixedFit;
		if (!ImGui::BeginTable("positions", 5, flags, ImVec2(0.0f, 180.0f * DEBUG_MENU::Scale())))
			return;
		ImGui::TableSetupScrollFreeze(1, 1);
		ImGui::TableSetupColumn("Kind");
		ImGui::TableSetupColumn("Distance");
		ImGui::TableSetupColumn("Cell");
		ImGui::TableSetupColumn("Job");
		ImGui::TableSetupColumn("Who");
		ImGui::TableHeadersRow();
		for (size_t i = 0; i < rows.size() && i < 150; i++)
		{
			const COVER_DEBUG::AutoJob& job = *rows[i].job;
			const COVER_DEBUG::Job* made = COVER_DEBUG::JobByAddress(job.job);
			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			ImGui::TextColored(Colour4(AutoColour(job.kind, true)), "%s", COVER_DEBUG::AutoKindName(job.kind));
			ImGui::TableNextColumn();
			ImGui::Text("%.1f m", rows[i].distance);
			ImGui::TableNextColumn();
			ImGui::Text("%d%s", job.cell, job.active ? " (active)" : "");
			ImGui::TableNextColumn();
			if (made && !made->tasks.empty())
				ImGui::TextUnformatted(COVER_DEBUG::TaskStateName(made->tasks.front().state));
			else
				ImGui::TextDisabled(job.job ? "made" : "-");
			ImGui::TableNextColumn();
			std::string who;
			if (made)
				for (const COVER_DEBUG::Task& task : made->tasks)
				{
					const std::string name = Who(CharacterByObject(task.owningCharacter ? task.owningCharacter : task.reservedCharacter));
					if (!name.empty())
						who += (who.empty() ? "" : ", ") + name;
				}
			ImGui::TextUnformatted(who.empty() ? "-" : who.c_str());
		}
		ImGui::EndTable();
	}

	void DrawJobsSection()
	{
		const COVER_DEBUG::Jobs& jobs = COVER_DEBUG::ReadJobs();
		SwitchBox("Draw the tasks", g_tasks);
		if (!jobs.have)
		{
			ImGui::TextDisabled("The game has no job centre (no level running).");
			return;
		}
		int byType[16] = {};
		for (const COVER_DEBUG::Job& job : jobs.jobs)
			if (job.type >= 0 && job.type < 16)
				byType[job.type]++;
		std::string summary;
		for (int type = 0; type < 15; type++)
			if (byType[type])
				summary += (summary.empty() ? "" : ", ") + Format("%d %s", byType[type], COVER_DEBUG::JobTypeName(type));
		ImGui::TextWrapped("%d jobs: %s", static_cast<int>(jobs.jobs.size()), summary.empty() ? "none" : summary.c_str());
		const char* types[16];
		types[0] = "All types";
		for (int i = 0; i < 15; i++)
			types[i + 1] = COVER_DEBUG::JobTypeName(i);
		int shownType = g_jobType + 1;
		ImGui::SetNextItemWidth(220.0f * DEBUG_MENU::Scale());
		if (ImGui::Combo("Type", &shownType, types, 16))
			g_jobType = shownType - 1;
		ImGui::SameLine();
		ImGui::SetNextItemWidth(160.0f * DEBUG_MENU::Scale());
		ImGui::InputTextWithHint("##jobfilter", "Filter (who)", g_jobFilter, sizeof(g_jobFilter));
		float centre[3] = {};
		const bool haveCentre = DEBUG_MENU::CameraPosition(centre);
		const ImGuiTableFlags flags = ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_Resizable | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY | ImGuiTableFlags_SizingFixedFit;
		if (!ImGui::BeginTable("jobs", 6, flags, ImVec2(0.0f, 240.0f * DEBUG_MENU::Scale())))
			return;
		ImGui::TableSetupScrollFreeze(1, 1);
		ImGui::TableSetupColumn("Job");
		ImGui::TableSetupColumn("Entity / instance");
		ImGui::TableSetupColumn("Task state");
		ImGui::TableSetupColumn("Doing it");
		ImGui::TableSetupColumn("Reserved by");
		ImGui::TableSetupColumn("Distance");
		ImGui::TableHeadersRow();
		int rows = 0;
		for (const COVER_DEBUG::Job& job : jobs.jobs)
		{
			if (g_jobType >= 0 && job.type != g_jobType)
				continue;
			for (size_t t = 0; t < job.tasks.size() || (t == 0 && job.tasks.empty()); t++)
			{
				if (rows >= 400)
					break;
				const COVER_DEBUG::Task* task = t < job.tasks.size() ? &job.tasks[t] : nullptr;
				const std::string doer = task ? Who(CharacterByObject(task->owningCharacter)) : std::string();
				const std::string reserver = task ? Who(CharacterByObject(task->reservedCharacter)) : std::string();
				if (g_jobFilter[0] && doer.find(g_jobFilter) == std::string::npos && reserver.find(g_jobFilter) == std::string::npos)
					continue;
				rows++;
				ImGui::TableNextRow();
				ImGui::TableNextColumn();
				if (t == 0)
					ImGui::Text("%s%s", COVER_DEBUG::JobTypeName(job.type), job.taskCount > static_cast<int>(job.tasks.size()) ? Format(" (%d tasks)", job.taskCount).c_str() : "");
				else
					ImGui::TextDisabled("  task %d", static_cast<int>(t) + 1);
				ImGui::TableNextColumn();
				if (t == 0 && (job.entity || job.parent))
					ImGui::Text("0x%08X / 0x%08X", job.entity, job.parent);
				else
					ImGui::TextDisabled("-");
				ImGui::TableNextColumn();
				if (task)
					ImGui::TextColored(Colour4(TaskColour(task->state)), "%s", COVER_DEBUG::TaskStateName(task->state));
				else
					ImGui::TextDisabled("no tasks");
				ImGui::TableNextColumn();
				ImGui::TextUnformatted(doer.empty() ? "-" : doer.c_str());
				ImGui::TableNextColumn();
				ImGui::TextUnformatted(reserver.empty() ? "-" : reserver.c_str());
				ImGui::TableNextColumn();
				if (task && haveCentre)
					ImGui::Text("%.1f m", Distance(centre, task->position));
				else
					ImGui::TextDisabled("-");
			}
		}
		ImGui::EndTable();
	}

	void DrawCharacterJobsSection()
	{
		const COVER_DEBUG::Jobs& jobs = COVER_DEBUG::ReadJobs();
		bool any = false;
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
		{
			if (character.player || character.dead)
				continue;
			std::string line;
			for (const COVER_DEBUG::Job& job : jobs.jobs)
				for (const COVER_DEBUG::Task& task : job.tasks)
				{
					const bool doing = task.owningCharacter == character.object;
					const bool reserved = task.reservedCharacter == character.object;
					if (!doing && !reserved)
						continue;
					line += (line.empty() ? "" : "; ") + Format("%s %s (%s, %.1f m away)", doing ? "doing" : "reserved", COVER_DEBUG::JobTypeName(job.type),
						COVER_DEBUG::TaskStateName(task.state), Distance(character.position, task.position));
				}
			any = true;
			ImGui::Bullet();
			ImGui::TextWrapped("%s: %s", CharacterLabel(character).c_str(), line.empty() ? "no job" : line.c_str());
		}
		if (!any)
			ImGui::TextDisabled("No NPCs.");
	}

	void DrawTab()
	{
		const COVER_DEBUG::Cover& cover = COVER_DEBUG::ReadCover();
		if (!cover.level)
		{
			ImGui::TextDisabled("No level running.");
			return;
		}
		if (ImGui::CollapsingHeader("Cover", ImGuiTreeNodeFlags_DefaultOpen))
			DrawCoverSection();
		if (ImGui::CollapsingHeader("Baked job positions (spotting, crawl-space, assault)"))
			DrawPositionsSection();
		if (ImGui::CollapsingHeader("Jobs and their tasks"))
			DrawJobsSection();
		if (ImGui::CollapsingHeader("Each NPC's job"))
			DrawCharacterJobsSection();
	}

	// ---- Commands ----

	float FirstNumber(const std::string& text, float fallback)
	{
		const char* at = text.c_str();
		while (*at && !(isdigit(static_cast<unsigned char>(*at)) || *at == '.' || *at == '-'))
			at++;
		if (!*at)
			return fallback;
		return strtof(at, nullptr);
	}

	DEBUG_MENU::CommandResult Ok(const std::string& message)
	{
		return { true, message };
	}

	DEBUG_MENU::CommandResult Error(const std::string& message)
	{
		return { false, message };
	}

	void NameControls()
	{
		DEBUG_MENU::AddSwitch("nav.cover", &g_cover);
		DEBUG_MENU::AddSwitch("nav.cover_slots", &g_slots);
		DEBUG_MENU::AddSwitch("nav.cover_links", &g_links);
		DEBUG_MENU::AddSwitch("nav.cover_labels", &g_coverLabels);
		DEBUG_MENU::AddSwitch("nav.jobs_spotting", &g_spotting);
		DEBUG_MENU::AddSwitch("nav.jobs_crawl", &g_crawl);
		DEBUG_MENU::AddSwitch("nav.jobs_assault", &g_assault);
		DEBUG_MENU::AddSwitch("nav.jobs_active_only", &g_activeOnly);
		DEBUG_MENU::AddSwitch("nav.jobs_tasks", &g_tasks);
		DEBUG_MENU::AddValue("nav.cover_radius", &g_radius, 2.0f, 100.0f);

		DEBUG_MENU::AddCommand("nav.cover_status", "the cover system: loaded or not, counts of segments (low, standing, disabled), slots (free, reserved, occupied), traversals", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]()
			{
				const COVER_DEBUG::Cover& cover = COVER_DEBUG::ReadCover(true);
				int low = 0, disabled = 0, slotCounts[3] = {};
				for (const COVER_DEBUG::Segment& segment : cover.segments)
				{
					low += (segment.flags & COVER_DEBUG::kLowCover) != 0;
					disabled += !segment.enabled;
				}
				for (const COVER_DEBUG::Slot& slot : cover.slots)
					if (slot.status >= 0 && slot.status < 3)
						slotCounts[slot.status]++;
				return Ok(Format("level=%d\nhave=%d\nloaded=%d\nsegments=%d\nlow=%d\ndisabled=%d\nslots=%d\nfree=%d\nreserved=%d\noccupied=%d\ntraversals=%d\ntracked=%d\n",
					cover.level ? 1 : 0, cover.have ? 1 : 0, cover.loaded ? 1 : 0, static_cast<int>(cover.segments.size()), low, disabled, static_cast<int>(cover.slots.size()),
					slotCounts[0], slotCounts[1], slotCounts[2], static_cast<int>(cover.traversals.size()), cover.trackedCount));
			});
		});
		DEBUG_MENU::AddCommand("nav.list_cover", "cover segments within a distance of the camera (default 15 m), nearest first, one line each", [](const std::string& argument)
		{
			const float radius = FirstNumber(argument, 15.0f);
			return DEBUG_MENU::AnswerOnRenderThread([radius]()
			{
				const COVER_DEBUG::Cover& cover = COVER_DEBUG::ReadCover(true);
				float centre[3];
				if (!cover.loaded || !DEBUG_MENU::CameraPosition(centre))
					return Error("no cover data or camera");
				struct Row { const COVER_DEBUG::Segment* segment; float distance; };
				std::vector<Row> rows;
				for (const COVER_DEBUG::Segment& segment : cover.segments)
				{
					float middle[3];
					Middle(segment, middle);
					const float d = Distance(centre, middle);
					if (d <= radius)
						rows.push_back({ &segment, d });
				}
				std::stable_sort(rows.begin(), rows.end(), [](const Row& a, const Row& b) { return a.distance < b.distance; });
				std::string text;
				for (size_t i = 0; i < rows.size() && i < 100; i++)
					text += Format("%.1fm ", rows[i].distance) + SegmentText(cover, *rows[i].segment, false);
				return Ok(text.empty() ? std::string("none within range") : text);
			});
		});
		DEBUG_MENU::AddCommand("nav.cover_segment", "one cover segment by uid, with its slots and who has them; it is also selected (drawn in white)", [](const std::string& argument)
		{
			const int uid = atoi(argument.c_str());
			return DEBUG_MENU::AnswerOnRenderThread([uid]()
			{
				const COVER_DEBUG::Cover& cover = COVER_DEBUG::ReadCover(true);
				const COVER_DEBUG::Segment* segment = COVER_DEBUG::SegmentByUid(uid);
				if (!segment)
					return Error(Format("no segment %d (there are %d)", uid, static_cast<int>(cover.segments.size())));
				g_selectedSegment = uid;
				return Ok(SegmentText(cover, *segment, true));
			});
		});
		DEBUG_MENU::AddCommand("nav.list_cover_tracked", "the characters the cover system follows: not in cover, or the segment (corner, in line), slot, stance and facing; and the slots each has reserved", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]()
			{
				const COVER_DEBUG::Cover& cover = COVER_DEBUG::ReadCover(true);
				std::string text;
				for (int i = 0; i < cover.trackedCount; i++)
				{
					const COVER_DEBUG::Tracked& t = cover.tracked[i];
					if (t.inCover)
						text += Format("%s: segment %d (corner %s, in line %s) slot %s stance %s facing %s reserved %s\n", WhoByHandle(t.entity, t.parent).c_str(), t.segment,
							UidText(t.corner).c_str(), UidText(t.colinear).c_str(), SlotUidText(t.slot).c_str(), COVER_DEBUG::StanceName(t.stance), COVER_DEBUG::FacingName(t.facing),
							ReservedText(t).c_str());
					else
						text += Format("%s: not in cover, reserved %s\n", WhoByHandle(t.entity, t.parent).c_str(), ReservedText(t).c_str());
				}
				return Ok(text.empty() ? std::string("no characters followed") : text);
			});
		});
		DEBUG_MENU::AddCommand("nav.jobs_status", "the baked job positions: per kind, the grid, positions, active cells, whether all are used, jobs made; and job counts per type", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]()
			{
				const COVER_DEBUG::Jobs& jobs = COVER_DEBUG::ReadJobs(true);
				if (!jobs.have)
					return Error("no job centre (no level?)");
				std::string text;
				for (int kind = 0; kind < COVER_DEBUG::kAutoKinds; kind++)
				{
					const COVER_DEBUG::Grid& grid = jobs.grids[kind];
					int made = 0;
					for (const COVER_DEBUG::AutoJob& job : jobs.autoJobs)
						made += job.kind == kind && job.job != 0;
					text += Format("%s: loaded=%d positions=%d cells=%dx%d unit=%.2f min=%.1f,%.1f active_cells=%d use_all=%d jobs_made=%d\n", COVER_DEBUG::AutoKindName(kind),
						grid.have ? 1 : 0, grid.items, grid.xCells, grid.zCells, grid.unit, grid.minX, grid.minZ, grid.activeCells, jobs.useAll[kind] ? 1 : 0, made);
				}
				int byType[16] = {};
				for (const COVER_DEBUG::Job& job : jobs.jobs)
					if (job.type >= 0 && job.type < 16)
						byType[job.type]++;
				for (int type = 0; type < 15; type++)
					text += Format("jobs %s=%d\n", COVER_DEBUG::JobTypeName(type), byType[type]);
				return Ok(text);
			});
		});
		DEBUG_MENU::AddCommand("nav.list_job_positions", "baked job positions within a distance of the camera (default 20 m): kind, distance, cell, active, job state", [](const std::string& argument)
		{
			const float radius = FirstNumber(argument, 20.0f);
			return DEBUG_MENU::AnswerOnRenderThread([radius]()
			{
				const COVER_DEBUG::Jobs& jobs = COVER_DEBUG::ReadJobs(true);
				float centre[3];
				if (!jobs.have || !DEBUG_MENU::CameraPosition(centre))
					return Error("no job centre or camera");
				std::string text;
				int lines = 0;
				for (const COVER_DEBUG::AutoJob& job : jobs.autoJobs)
				{
					const float d = Distance(centre, job.position);
					if (d > radius || lines >= 200)
						continue;
					lines++;
					const COVER_DEBUG::Job* made = COVER_DEBUG::JobByAddress(job.job);
					text += Format("%s at %.2f,%.2f,%.2f (%.1f m) cell %d%s job=%s\n", COVER_DEBUG::AutoKindName(job.kind), job.position[0], job.position[1], job.position[2], d, job.cell,
						job.active ? " active" : "", made && !made->tasks.empty() ? COVER_DEBUG::TaskStateName(made->tasks.front().state) : job.job ? "made" : "none");
				}
				return Ok(text.empty() ? std::string("none within range") : text);
			});
		});
		DEBUG_MENU::AddCommand("nav.list_jobs", "every job (a type name narrows it): type, entity, each task's state and who does or reserves it", [](const std::string& argument)
		{
			return DEBUG_MENU::AnswerOnRenderThread([argument]()
			{
				const COVER_DEBUG::Jobs& jobs = COVER_DEBUG::ReadJobs(true);
				if (!jobs.have)
					return Error("no job centre (no level?)");
				std::string text;
				int lines = 0;
				for (const COVER_DEBUG::Job& job : jobs.jobs)
				{
					const std::string type = COVER_DEBUG::JobTypeName(job.type);
					if (!argument.empty() && type.find(argument) == std::string::npos)
						continue;
					if (lines++ >= 300)
						break;
					text += Format("%s entity=0x%08X instance=0x%08X tasks=%d:", type.c_str(), job.entity, job.parent, job.taskCount);
					for (const COVER_DEBUG::Task& task : job.tasks)
						text += Format(" [%s at %.1f,%.1f,%.1f doing=%s reserved=%s]", COVER_DEBUG::TaskStateName(task.state), task.position[0], task.position[1], task.position[2],
							WhoByObject(task.owningCharacter).c_str(), WhoByObject(task.reservedCharacter).c_str());
					text += "\n";
				}
				return Ok(text.empty() ? std::string("no jobs") : text);
			});
		});
		DEBUG_MENU::AddCommand("nav.list_character_jobs", "each NPC's job tasks (doing or reserved)", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]()
			{
				const COVER_DEBUG::Jobs& jobs = COVER_DEBUG::ReadJobs(true);
				std::string text;
				for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
				{
					if (character.player || character.dead)
						continue;
					text += CharacterLabel(character) + ":";
					bool any = false;
					for (const COVER_DEBUG::Job& job : jobs.jobs)
						for (const COVER_DEBUG::Task& task : job.tasks)
						{
							const bool doing = task.owningCharacter == character.object;
							if (!doing && task.reservedCharacter != character.object)
								continue;
							any = true;
							text += Format(" [%s %s %s]", doing ? "doing" : "reserved", COVER_DEBUG::JobTypeName(job.type), COVER_DEBUG::TaskStateName(task.state));
						}
					text += any ? "\n" : " no job\n";
				}
				return Ok(text.empty() ? std::string("no NPCs") : text);
			});
		});
	}

	const bool g_named = (NameControls(), true);
	const bool g_added = DEBUG_MENU::AddTab("Cover & Jobs", 125, DrawTab, "World") && DEBUG_MENU::AddOverlay(31, DrawOverlay);
}

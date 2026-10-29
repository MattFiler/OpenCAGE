#include "ANIM_DEBUG.h"
#include "ANIM_NAMES.h"
#include "DEBUG_MENU.h"
#include "LIVE_LINK.h"

#include <algorithm>
#include <atomic>
#include <cfloat>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <string>
#include <unordered_map>
#include <vector>

#include <imgui.h>

/*
	The Animation tab and the skeletons drawn over the game, for ANIM_DEBUG's reads (see ANIM_DEBUG.h). Every switch is a
	named one (DEBUG_MENU::AddSwitch), so OpenCAGE can set them too; they are named the first time the overlay or the tab
	runs.
*/
namespace
{
	// What is drawn over the game: for the selected character, or every character with "anim.all" on
	std::atomic<bool> g_skeletons = false, g_all = false;
	std::atomic<bool> g_boneNames = false;   // the selected character's only
	std::atomic<bool> g_axes = false, g_helpers = false, g_velocity = false, g_ik = false, g_labels = false;
	// The tab: only the trees' nodes with weight
	std::atomic<bool> g_onlyContributing = true;
	// Which pose the skeletons show (the final one, or a layer's)
	std::atomic<int> g_pose = ANIM_DEBUG::kFinalPose;

	void NameSwitches()
	{
		static bool named = false;
		if (named)
			return;
		named = true;
		DEBUG_MENU::AddSwitch("anim.skeletons", &g_skeletons);
		DEBUG_MENU::AddSwitch("anim.all", &g_all);
		DEBUG_MENU::AddSwitch("anim.bone_names", &g_boneNames);
		DEBUG_MENU::AddSwitch("anim.bone_axes", &g_axes);
		DEBUG_MENU::AddSwitch("anim.helper_bones", &g_helpers);
		DEBUG_MENU::AddSwitch("anim.velocity", &g_velocity);
		DEBUG_MENU::AddSwitch("anim.ik_targets", &g_ik);
		DEBUG_MENU::AddSwitch("anim.labels", &g_labels);
		DEBUG_MENU::AddSwitch("anim.only_contributing", &g_onlyContributing);
	}

	// ---- Names ----

	// A rig's bone names and which side each bone is on (1 left, -1 right, 0 the middle), kept per rig name and bone count
	// until the level changes (and made again once the full name table is in)
	struct RigNames
	{
		std::vector<std::string> shortNames, fullNames;
		std::vector<int8_t> sides;
		bool full = false;
	};
	std::unordered_map<uint64_t, RigNames> g_rigNames;
	std::string g_rigNamesLevel;

	const RigNames* NamesFor(const ANIM_DEBUG::Skeleton& skeleton)
	{
		if (!skeleton.names)
			return nullptr;
		const std::string level = LIVE_LINK::RunningLevelName();
		if (level != g_rigNamesLevel)
		{
			g_rigNames.clear();
			g_rigNamesLevel = level;
		}
		const uint64_t key = (static_cast<uint64_t>(skeleton.rig) << 32) | static_cast<uint32_t>(skeleton.bones);
		auto found = g_rigNames.find(key);
		const bool full = ANIM_NAMES::FullTableReady();
		if (found != g_rigNames.end() && (found->second.full || !full))
			return &found->second;
		RigNames& names = g_rigNames[key];
		names = RigNames();
		names.full = full;
		names.shortNames.resize(skeleton.bones);
		names.fullNames.resize(skeleton.bones);
		names.sides.resize(skeleton.bones);
		for (int i = 0; i < skeleton.bones; i++)
		{
			names.shortNames[i] = ANIM_NAMES::Name(skeleton.shortNames[i]);
			names.fullNames[i] = ANIM_NAMES::Name(skeleton.fullNames[i]);
			std::string side = names.shortNames[i];
			if (side.empty())
			{
				const size_t colon = names.fullNames[i].find(':');
				side = colon == std::string::npos ? names.fullNames[i] : names.fullNames[i].substr(colon + 1);
			}
			names.sides[i] = side.rfind("LEFT", 0) == 0 ? 1 : side.rfind("RIGHT", 0) == 0 ? -1 : 0;
		}
		return &names;
	}

	// A clip's label without the folders it was made in
	std::string ClipName(uint32_t hash)
	{
		std::string name = ANIM_NAMES::Name(hash);
		const size_t slash = name.find_last_of("\\/");
		return slash == std::string::npos ? name : name.substr(slash + 1);
	}

	std::string NameOr(uint32_t hash, const char* none)
	{
		return hash ? ANIM_NAMES::Name(hash) : std::string(none);
	}

	// ---- Drawing over the game ----

	constexpr float kNearCamera = 0.35f;   // closer than this to the camera, a bone is left out (the player's own head)

	bool NearCamera(const float camera[3], const float point[3])
	{
		const float dx = point[0] - camera[0], dy = point[1] - camera[1], dz = point[2] - camera[2];
		return dx * dx + dy * dy + dz * dz < kNearCamera * kNearCamera;
	}

	ImU32 SideColour(int side, int alpha)
	{
		return side > 0 ? IM_COL32(90, 200, 255, alpha) : side < 0 ? IM_COL32(255, 150, 80, alpha) : IM_COL32(235, 235, 235, alpha);
	}

	void Add(const float a[3], const float b[3], float scale, float out[3])
	{
		for (int k = 0; k < 3; k++)
			out[k] = a[k] + b[k] * scale;
	}

	void Cross(ImDrawList* drawList, const float at[3], float size, ImU32 colour)
	{
		for (int axis = 0; axis < 3; axis++)
		{
			float a[3] = { at[0], at[1], at[2] }, b[3] = { at[0], at[1], at[2] };
			a[axis] -= size;
			b[axis] += size;
			DEBUG_MENU::WorldLine(drawList, a, b, colour, 1.5f);
		}
	}

	// Small text by a point, without the dark backing the bigger labels have
	void SmallText(ImDrawList* drawList, const float world[3], const char* text, ImU32 colour)
	{
		ImVec2 at;
		if (!DEBUG_MENU::Project(world, at))
			return;
		drawList->AddText(ImGui::GetFont(), 11.0f * DEBUG_MENU::Scale(), ImVec2(at.x + 4.0f, at.y - 6.0f), colour, text);
	}

	// The bones (lines from each to its parent, and a dot at each), then its axes and names when they are on
	void DrawSkeleton(ImDrawList* drawList, const ANIM_DEBUG::Skeleton& skeleton, const RigNames* names, bool selected, bool lines, bool haveCamera, const float camera[3])
	{
		const float scale = DEBUG_MENU::Scale();
		const int alpha = skeleton.sampled ? 230 : 140;
		const float thickness = selected ? 2.0f : 1.5f;
		for (int i = 0; lines && i < skeleton.bones; i++)
		{
			const int parent = skeleton.parents[i];
			if (parent < 0 || (haveCamera && (NearCamera(camera, skeleton.positions[i]) || NearCamera(camera, skeleton.positions[parent]))))
				continue;
			// The rig's root is the point on the floor the character's movement is taken from: its link is dimmed
			const ImU32 colour = parent == 0 ? IM_COL32(150, 150, 150, 90) : SideColour(names ? names->sides[i] : 0, alpha);
			DEBUG_MENU::WorldLine(drawList, skeleton.positions[parent], skeleton.positions[i], colour, thickness);
		}
		for (int i = 1; lines && i < skeleton.bones; i++)
		{
			ImVec2 at;
			if ((haveCamera && NearCamera(camera, skeleton.positions[i])) || !DEBUG_MENU::Project(skeleton.positions[i], at))
				continue;
			drawList->AddCircleFilled(at, (selected ? 2.5f : 2.0f) * scale, SideColour(names ? names->sides[i] : 0, alpha));
		}

		if (g_axes)
		{
			static const float kUnits[3][3] = { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
			const ImU32 kAxisColours[3] = { IM_COL32(255, 70, 70, 220), IM_COL32(70, 255, 70, 220), IM_COL32(80, 120, 255, 220) };
			const float* m = skeleton.world;
			for (int i = 1; i < skeleton.bones; i++)
			{
				if (haveCamera && NearCamera(camera, skeleton.positions[i]))
					continue;
				for (int axis = 0; axis < 3; axis++)
				{
					// The bone's axis relative to the character, then turned as the character is
					const float* q = skeleton.rotations[i];
					const float* v = kUnits[axis];
					const float tx = 2.0f * (q[1] * v[2] - q[2] * v[1]), ty = 2.0f * (q[2] * v[0] - q[0] * v[2]), tz = 2.0f * (q[0] * v[1] - q[1] * v[0]);
					const float d[3] = { v[0] + q[3] * tx + (q[1] * tz - q[2] * ty), v[1] + q[3] * ty + (q[2] * tx - q[0] * tz), v[2] + q[3] * tz + (q[0] * ty - q[1] * tx) };
					float world[3];
					for (int k = 0; k < 3; k++)
						world[k] = d[0] * m[k] + d[1] * m[4 + k] + d[2] * m[8 + k];
					float end[3];
					Add(skeleton.positions[i], world, 0.06f, end);
					DEBUG_MENU::WorldLine(drawList, skeleton.positions[i], end, kAxisColours[axis], 1.0f);
				}
			}
		}

		if (g_boneNames && selected && names)
			for (int i = 1; i < skeleton.bones; i++)
				if (!(haveCamera && NearCamera(camera, skeleton.positions[i])) && !names->shortNames[i].empty())
					SmallText(drawList, skeleton.positions[i], names->shortNames[i].c_str(), IM_COL32(255, 255, 200, 230));
	}

	void DrawExtras(ImDrawList* drawList, const ANIM_DEBUG::Extras& extras, bool haveCamera, const float camera[3])
	{
		char text[160];
		if (g_helpers)
			for (int i = 0; i < ANIM_DEBUG::kHelpers; i++)
			{
				if (haveCamera && NearCamera(camera, extras.helpers[i]))
					continue;
				Cross(drawList, extras.helpers[i], 0.05f, IM_COL32(255, 220, 0, 230));
				SmallText(drawList, extras.helpers[i], ANIM_DEBUG::HelperName(i), IM_COL32(255, 220, 0, 230));
			}

		if (g_velocity)
		{
			// From the hips, a second's worth: what the animations give, what the movement asks for, and what it is warped to
			const struct { const float* velocity; float yaw; const char* name; ImU32 colour; } arrows[] = {
				{ extras.blended, extras.blendedYaw, "animated", IM_COL32(80, 255, 80, 230) },
				{ extras.desired, extras.desiredYaw, "asked for", IM_COL32(255, 230, 60, 230) },
				{ extras.warped, extras.warpedYaw, "warped", IM_COL32(255, 70, 70, 230) },
			};
			float rise = 0.0f;
			for (const auto& arrow : arrows)
			{
				const float* v = arrow.velocity;
				const float speed = std::sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
				if (speed < 0.01f && std::fabs(arrow.yaw) < 0.01f)
					continue;
				float from[3] = { extras.helpers[1][0], extras.helpers[1][1] + rise, extras.helpers[1][2] }, to[3];
				Add(from, v, 1.0f, to);
				DEBUG_MENU::WorldLine(drawList, from, to, arrow.colour, 2.0f);
				snprintf(text, sizeof(text), "%s %.2f m/s, turning %.0f deg/s", arrow.name, speed, arrow.yaw * 57.29578f);
				SmallText(drawList, to, text, arrow.colour);
				rise += 0.04f;
			}
		}

		if (g_ik)
			for (int i = 0; i < extras.ikCount; i++)
			{
				const ANIM_DEBUG::IkTarget& target = extras.ik[i];
				Cross(drawList, target.position, 0.07f, IM_COL32(255, 80, 255, 230));
				snprintf(text, sizeof(text), "IK: %s (gain %.2f)", ANIM_DEBUG::IkTargetName(target.target), target.gain);
				SmallText(drawList, target.position, text, IM_COL32(255, 80, 255, 230));
			}

		if (g_labels)
		{
			const float above[3] = { extras.helpers[0][0], extras.helpers[0][1] + 0.3f, extras.helpers[0][2] };
			if (!(haveCamera && NearCamera(camera, extras.helpers[0])))
			{
				if (extras.labelClip)
				{
					snprintf(text, sizeof(text), "%s  %.2f / %.2f s", ClipName(extras.labelClip).c_str(), extras.labelTime, extras.labelDuration);
					DEBUG_MENU::WorldText(drawList, above, text, IM_COL32(200, 230, 255, 255), 0.0f);
				}
				if (extras.labelTree)
				{
					snprintf(text, sizeof(text), "%s  %.2f", ANIM_NAMES::Name(extras.labelTree).c_str(), extras.labelTreeWeight);
					DEBUG_MENU::WorldText(drawList, above, text, IM_COL32(255, 255, 255, 255), -20.0f * DEBUG_MENU::Scale());
				}
			}
		}
	}

	void DrawOverlay(ImDrawList* drawList)
	{
		NameSwitches();
		if (!g_skeletons && !g_axes && !g_boneNames && !g_helpers && !g_velocity && !g_ik && !g_labels)
			return;
		float camera[3] = {};
		const bool haveCamera = DEBUG_MENU::CameraPosition(camera);
		const uint32_t selected = DEBUG_MENU::SelectedId();
		static ANIM_DEBUG::Skeleton skeleton;
		static ANIM_DEBUG::Extras read;
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
		{
			const bool isSelected = character.id == selected;
			if (!isSelected && !g_all)
				continue;
			if ((g_skeletons || g_axes || (g_boneNames && isSelected)) && ANIM_DEBUG::ReadSkeleton(character.object, g_pose, true, skeleton))
				DrawSkeleton(drawList, skeleton, NamesFor(skeleton), isSelected, g_skeletons, haveCamera, camera);
			if ((g_helpers || g_velocity || g_ik || g_labels) && ANIM_DEBUG::ReadExtras(character.object, read))
				DrawExtras(drawList, read, haveCamera, camera);
		}
	}

	// ---- The tab ----

	void SwitchBox(const char* label, std::atomic<bool>& on)
	{
		bool value = on;
		if (ImGui::Checkbox(label, &value))
			on = value;
	}

	void Tooltip(const char* text)
	{
		if (ImGui::IsItemHovered())
			ImGui::SetTooltip("%s", text);
	}

	const ImVec4 kGrey(0.55f, 0.55f, 0.55f, 1.0f);

	// The height of a table that scrolls (sideways too) for so many rows under its header (a line of text each, or as tall as
	// a framed widget such as a bar), with room for its sideways scrollbar, and no taller than most
	float TableHeight(int rows, float most, bool framedRows = false)
	{
		const ImGuiStyle& style = ImGui::GetStyle();
		const float header = ImGui::GetFontSize() + style.CellPadding.y * 2.0f;
		const float row = (framedRows ? ImGui::GetFrameHeight() : ImGui::GetFontSize()) + style.CellPadding.y * 2.0f;
		return (std::min)(most, header + row * static_cast<float>(rows) + style.ScrollbarSize + style.CellPadding.y * 2.0f + 2.0f);
	}

	// The name of the k-th child of the node at index (the nodes are listed each before its children)
	std::string ChildName(const ANIM_DEBUG::Tree& tree, size_t index, int child)
	{
		const int depth = tree.nodes[index].depth;
		int seen = -1;
		for (size_t i = index + 1; i < tree.nodes.size() && tree.nodes[i].depth > depth; i++)
			if (tree.nodes[i].depth == depth + 1 && ++seen == child)
				return tree.nodes[i].name ? ANIM_NAMES::Name(tree.nodes[i].name) : std::string(ANIM_DEBUG::NodeTypeName(tree.nodes[i].type));
		char text[24];
		snprintf(text, sizeof(text), "child %d", child + 1);
		return text;
	}

	// What a node's type adds, as text
	std::string NodeDetail(const ANIM_DEBUG::Tree& tree, size_t index)
	{
		const ANIM_DEBUG::Node& node = tree.nodes[index];
		char text[256] = "";
		const std::string picks = node.activeChild >= 0 ? ChildName(tree, index, node.activeChild) : std::string("none");
		switch (node.type)
		{
		case 0:
			if (node.haveController)
				snprintf(text, sizeof(text), "%s  %.2f / %.2f s  speed %.2f  clip weight %.2f%s%s", node.clip ? ClipName(node.clip).c_str() : "(no clip)",
					node.time, node.duration, node.speed, node.clipWeight, node.mirrored ? "  mirrored" : "", node.inUse ? "" : "  (not in use)");
			else
				snprintf(text, sizeof(text), "asks for %s (not playing)", node.requested ? ClipName(node.requested).c_str() : "nothing");
			break;
		case 1: snprintf(text, sizeof(text), "picked %s, %d loop(s) left", picks.c_str(), node.loopsLeft); break;
		case 2: snprintf(text, sizeof(text), "value %.0f, picks %s", node.values[0], picks.c_str()); break;
		case 12: snprintf(text, sizeof(text), "value %s, picks %s", NameOr(node.valueHash, "(none)").c_str(), picks.c_str()); break;
		case 11: snprintf(text, sizeof(text), "value %.2f (%.2f to %.2f), picks %s", node.values[0], node.minimum, node.maximum, picks.c_str()); break;
		case 13: snprintf(text, sizeof(text), "picks %s", picks.c_str()); break;
		case 3:
		case 17: snprintf(text, sizeof(text), "value %.2f (%.2f to %.2f)", node.values[0], node.minimum, node.maximum); break;
		case 4:
			if (node.count == 3)
				snprintf(text, sizeof(text), "values %.2f, %.2f, %.2f; set %s", node.values[0], node.values[1], node.values[2], NameOr(node.valueHash, "?").c_str());
			else if (node.count == 2)
				snprintf(text, sizeof(text), "values %.2f, %.2f; set %s", node.values[0], node.values[1], NameOr(node.valueHash, "?").c_str());
			else
				snprintf(text, sizeof(text), "value %.2f; set %s", node.values[0], NameOr(node.valueHash, "?").c_str());
			break;
		case 5: snprintf(text, sizeof(text), "values %.2f, %.2f, %.2f, %.2f", node.values[0], node.values[1], node.values[2], node.values[3]); break;
		case 7:
		case 8: snprintf(text, sizeof(text), "values %.2f, %.2f (%.2f to %.2f)", node.values[0], node.values[1], node.minimum, node.maximum); break;
		case 9: snprintf(text, sizeof(text), "additive weight %.2f", node.values[0]); break;
		case 10: snprintf(text, sizeof(text), "additive weight %.2f, value %.2f", node.values[0], node.values[1]); break;
		case 14: snprintf(text, sizeof(text), "bone groups 0x%05X", node.mask); break;
		case 16: snprintf(text, sizeof(text), "vector %.2f, %.2f, %.2f", node.values[0], node.values[1], node.values[2]); break;
		default: break;
		}
		return text;
	}

	const char* ParameterTypeName(int type)
	{
		static const char* const names[] = { "whole number", "number", "name", "matrix", "vector" };
		return type >= 0 && type < 5 ? names[type] : "?";
	}

	void DrawTree(const ANIM_DEBUG::Tree& tree, int number)
	{
		const std::string name = NameOr(tree.name, "(no name)");
		ImGui::PushID(number);
		if (tree.global <= 0.0f)
			ImGui::PushStyleColor(ImGuiCol_Text, kGrey);
		const bool open = ImGui::TreeNodeEx("tree", 0, "%s: %s  weight %.2f (own %.2f, ease %.2f)", ANIM_DEBUG::LayerName(tree.layer), name.c_str(), tree.global, tree.local, tree.ease);
		if (tree.global <= 0.0f)
			ImGui::PopStyleColor();
		if (open)
		{
			ImGui::TextDisabled("Tree set %s; eases over %.2f s%s%s", NameOr(tree.treeSet, "?").c_str(), tree.easeTime,
				tree.flags & 0x01 ? "; its movement stops as it eases out" : "", tree.flags & 0x08 ? "; foot IK allowed" : "");
			if (!tree.parameters.empty() && ImGui::TreeNode("parameters", "Parameters (%d)", static_cast<int>(tree.parameters.size())))
			{
				if (ImGui::BeginTable("parameters", 3, ImGuiTableFlags_RowBg | ImGuiTableFlags_BordersInnerV | ImGuiTableFlags_SizingFixedFit))
				{
					ImGui::TableSetupColumn("Name");
					ImGui::TableSetupColumn("Type");
					ImGui::TableSetupColumn("Value");
					ImGui::TableHeadersRow();
					for (const ANIM_DEBUG::Parameter& parameter : tree.parameters)
					{
						ImGui::TableNextRow();
						ImGui::TableNextColumn();
						ImGui::TextUnformatted(NameOr(parameter.name, "?").c_str());
						ImGui::TableNextColumn();
						ImGui::TextUnformatted(ParameterTypeName(parameter.type));
						ImGui::TableNextColumn();
						switch (parameter.type)
						{
						case 0: ImGui::Text("%u", parameter.whole); break;
						case 1: ImGui::Text("%.3f", parameter.number); break;
						case 2: ImGui::TextUnformatted(NameOr(parameter.text, "(none)").c_str()); break;
						case 3: ImGui::Text("at %.2f, %.2f, %.2f", parameter.vector[0], parameter.vector[1], parameter.vector[2]); break;
						case 4: ImGui::Text("%.2f, %.2f, %.2f", parameter.vector[0], parameter.vector[1], parameter.vector[2]); break;
						default: ImGui::TextDisabled("-"); break;
						}
					}
					ImGui::EndTable();
				}
				ImGui::TreePop();
			}
			// The nodes, each indented under its parent; with only contributing on, a weightless node and all below it are left out
			int skipBelow = -1;
			for (size_t i = 1; i < tree.nodes.size(); i++)
			{
				const ANIM_DEBUG::Node& node = tree.nodes[i];
				if (skipBelow >= 0)
				{
					if (node.depth > skipBelow)
						continue;
					skipBelow = -1;
				}
				if (g_onlyContributing && node.global <= 0.0f)
				{
					skipBelow = node.depth;
					continue;
				}
				const std::string nodeName = node.name ? ANIM_NAMES::Name(node.name) : std::string();
				const std::string detail = NodeDetail(tree, i);
				const ImVec4 colour = node.global <= 0.0f ? kGrey : node.type == 0 ? ImVec4(0.75f, 0.9f, 1.0f, 1.0f) : ImVec4(1.0f, 1.0f, 1.0f, 1.0f);
				ImGui::TextColored(colour, "%*s%s %s  w %.2f (%.2f, ease %.2f)%s%s%s", node.depth * 2, "", ANIM_DEBUG::NodeTypeName(node.type),
					nodeName.c_str(), node.global, node.local, node.ease, detail.empty() ? "" : "  ", detail.c_str(), node.cut ? "  (more not shown)" : "");
			}
			if (tree.cut)
				ImGui::TextDisabled("(more nodes than are shown)");
			ImGui::TreePop();
		}
		ImGui::PopID();
	}

	void DrawClipRow(const ANIM_DEBUG::Clip& clip)
	{
		ImGui::TableNextRow();
		ImGui::TableNextColumn();
		ImGui::TextUnformatted(ANIM_DEBUG::LayerName(clip.layer));
		ImGui::TableNextColumn();
		const std::string name = clip.clip ? ClipName(clip.clip) : std::string("(none)");
		ImGui::TextUnformatted(name.c_str());
		if (ImGui::IsItemHovered() && clip.clip)
			ImGui::SetTooltip("%s", ANIM_NAMES::Name(clip.clip).c_str());
		ImGui::TableNextColumn();
		ImGui::TextUnformatted(NameOr(clip.tree, "-").c_str());
		ImGui::TableNextColumn();
		char overlay[48];
		snprintf(overlay, sizeof(overlay), "%.2f / %.2f s", clip.time, clip.duration);
		ImGui::ProgressBar(clip.duration > 0.0f ? (std::max)(0.0f, (std::min)(1.0f, clip.time / clip.duration)) : 0.0f, ImVec2(150.0f * DEBUG_MENU::Scale(), 0.0f), overlay);
		ImGui::TableNextColumn();
		ImGui::Text("%.2f", clip.weight);
		ImGui::TableNextColumn();
		ImGui::Text("%.2f", clip.speed);
		ImGui::TableNextColumn();
		if (clip.maxCycles > 0)
			ImGui::Text("%d of %d", clip.loops, clip.maxCycles);
		else
			ImGui::Text("%d", clip.loops);
		ImGui::TableNextColumn();
		ImGui::TextUnformatted(ANIM_DEBUG::EaseStateName(clip.easeState));
		ImGui::TableNextColumn();
		ImGui::TextUnformatted(clip.mirrored ? "yes" : "");
	}

	void DrawClips(const std::vector<ANIM_DEBUG::Clip>& clips, bool weighted)
	{
		// Scrolls sideways rather than cutting off the last columns when the clip and tree names are long
		int rows = 0;
		for (const ANIM_DEBUG::Clip& clip : clips)
			rows += (clip.weight > 0.0f) == weighted ? 1 : 0;
		if (!ImGui::BeginTable(weighted ? "clips" : "idle", 9, ImGuiTableFlags_RowBg | ImGuiTableFlags_BordersInnerV | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY |
			ImGuiTableFlags_SizingFixedFit, ImVec2(0.0f, TableHeight(rows, 420.0f * DEBUG_MENU::Scale(), true))))
			return;
		ImGui::TableSetupScrollFreeze(0, 1);
		ImGui::TableSetupColumn("Layer");
		ImGui::TableSetupColumn("Clip");
		ImGui::TableSetupColumn("Tree");
		ImGui::TableSetupColumn("Time");
		ImGui::TableSetupColumn("Weight");
		ImGui::TableSetupColumn("Speed");
		ImGui::TableSetupColumn("Loops");
		ImGui::TableSetupColumn("Ease");
		ImGui::TableSetupColumn("Mirrored");
		ImGui::TableHeadersRow();
		if (!weighted)
			ImGui::PushStyleColor(ImGuiCol_Text, kGrey);
		for (const ANIM_DEBUG::Clip& clip : clips)
			if ((clip.weight > 0.0f) == weighted)
				DrawClipRow(clip);
		if (!weighted)
			ImGui::PopStyleColor();
		ImGui::EndTable();
	}

	void Vector(const char* label, const float v[3])
	{
		ImGui::Text("%s: %.3f, %.3f, %.3f (%.3f)", label, v[0], v[1], v[2], std::sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]));
	}

	void DrawTab()
	{
		NameSwitches();
		DEBUG_MENU::CharacterPicker("Character");

		ImGui::SeparatorText("Draw over the game");
		SwitchBox("Skeletons", g_skeletons);
		ImGui::SameLine(220.0f * DEBUG_MENU::Scale());
		SwitchBox("For every character", g_all);
		Tooltip("Otherwise the drawing below is for the selected character only.");
		SwitchBox("Bone names (the selected character)", g_boneNames);
		SwitchBox("Bone axes", g_axes);
		ImGui::SameLine(220.0f * DEBUG_MENU::Scale());
		SwitchBox("Helper bones", g_helpers);
		Tooltip("The head, hips, root, hands and feet the game works out each frame for its own use (aiming, looking, IK).");
		SwitchBox("Velocity", g_velocity);
		Tooltip("From the hips, a second's worth: green what the animations give, yellow what the character's movement asks for, red what the animations are warped to.");
		ImGui::SameLine(220.0f * DEBUG_MENU::Scale());
		SwitchBox("IK targets", g_ik);
		SwitchBox("Tree and clip over heads", g_labels);
		Tooltip("The full-body layer's heaviest tree, and its heaviest clip and where it is in it.");
		{
			const int pose = g_pose;
			char preview[48];
			snprintf(preview, sizeof(preview), pose == ANIM_DEBUG::kFinalPose ? "The final pose" : "Layer %d: %s", pose, ANIM_DEBUG::LayerName(pose));
			ImGui::SetNextItemWidth(220.0f * DEBUG_MENU::Scale());
			if (ImGui::BeginCombo("Pose drawn", preview))
			{
				if (ImGui::Selectable("The final pose", pose == ANIM_DEBUG::kFinalPose))
					g_pose = ANIM_DEBUG::kFinalPose;
				for (int l = 0; l < ANIM_DEBUG::kLayers - 1; l++)
				{
					char item[48];
					snprintf(item, sizeof(item), "Layer %d: %s", l, ANIM_DEBUG::LayerName(l));
					if (ImGui::Selectable(item, pose == l))
						g_pose = l;
				}
				ImGui::EndCombo();
			}
			Tooltip("The final pose is the one the game skins the mesh with. A layer's own pose is on its own rig, before the layers above it are added (blank when the character does not use the layer).");
		}
		ImGui::TextDisabled("Blue: left, orange: right. Dimmer: not posed this frame (far away or out of view).");
		if (const char* problem = ANIM_NAMES::FullTableProblem())
			ImGui::TextDisabled("Names: %s; only the game's own (bones and rigs) are known.", problem);
		else if (!ANIM_NAMES::FullTableReady())
			ImGui::TextDisabled("Names: reading the animation package's names...");

		const DEBUG_MENU::Character* character = DEBUG_MENU::Selected();
		if (!character)
		{
			ImGui::TextDisabled("Pick a character to see its animation in detail.");
			return;
		}
		static ANIM_DEBUG::Detail detail;
		if (!ANIM_DEBUG::ReadDetail(character->object, detail))
		{
			ImGui::TextDisabled("Its animation system is not set up.");
			return;
		}
		const ANIM_DEBUG::Header& header = detail.header;

		// (an id of its own: the character picker above is labelled the same)
		if (ImGui::CollapsingHeader("Character##animation", ImGuiTreeNodeFlags_DefaultOpen))
		{
			ImGui::Text("Skeleton %s: gameplay rig %s (%d bones), cinematic rig %s (%d bones)", NameOr(header.skeleton, "?").c_str(),
				NameOr(header.gameplay.name, "-").c_str(), header.gameplay.bones, NameOr(header.cinematic.name, "-").c_str(), header.cinematic.bones);
			ImGui::Text("Animation set %s; tree set %s", NameOr(header.animationSet, "?").c_str(), NameOr(header.treeSet, "?").c_str());
			for (int i = 0; i < 3; i++)
				ImGui::Text("%s: %s%s", ANIM_DEBUG::ContextName(i), header.contextCounts[i] > 0 ? NameOr(header.contexts[i], "none").c_str() : "none",
					header.contextCounts[i] > 1 ? " (held more than once)" : "");
			std::string flags;
			for (int bit = 0; bit < 8; bit++)
				if (header.flags & (1 << bit))
					flags += (flags.empty() ? "" : ", ") + std::string(ANIM_DEBUG::FlagName(bit));
			ImGui::TextWrapped("Switches: %s%s", flags.empty() ? "none" : flags.c_str(), header.custom ? "; made of several models" : "");
			ImGui::Text("Posed %s; %s this frame; %s", ANIM_DEBUG::SamplingName(header.sampling), header.sampledThisFrame ? "posed" : "not posed",
				header.inMainView ? "in the main view" : "not in the main view");
			if (header.haveRagdoll)
				ImGui::Text("Ragdoll: %s%s%s; tail %s%s", header.ragdollActive ? "active" : "not active", header.ragdollFinished ? ", finished" : "",
					header.ragdollDriving ? ", driving to a pose" : "", ANIM_DEBUG::TailStateName(header.tailState), header.facehugger ? "; a facehugger's" : "");
		}

		if (ImGui::CollapsingHeader("Layers", ImGuiTreeNodeFlags_DefaultOpen) &&
			ImGui::BeginTable("layers", 6, ImGuiTableFlags_RowBg | ImGuiTableFlags_BordersInnerV | ImGuiTableFlags_SizingFixedFit))
		{
			ImGui::TableSetupColumn("Layer");
			ImGui::TableSetupColumn("Used");
			ImGui::TableSetupColumn("Level of detail");
			ImGui::TableSetupColumn("Weight");
			ImGui::TableSetupColumn("Trees");
			ImGui::TableSetupColumn("Clips");
			ImGui::TableHeadersRow();
			for (int l = 0; l < ANIM_DEBUG::kLayers; l++)
			{
				const ANIM_DEBUG::Layer& layer = detail.layers[l];
				if (!layer.present)
					ImGui::PushStyleColor(ImGuiCol_Text, kGrey);
				ImGui::TableNextRow();
				ImGui::TableNextColumn();
				ImGui::Text("%d %s", l, ANIM_DEBUG::LayerName(l));
				ImGui::TableNextColumn();
				ImGui::TextUnformatted(layer.present ? "yes" : "no");
				ImGui::TableNextColumn();
				ImGui::TextUnformatted(layer.lod == 0 ? "on" : "off");
				ImGui::TableNextColumn();
				ImGui::Text("%.2f", layer.weight);
				ImGui::TableNextColumn();
				ImGui::Text("%d", layer.trees);
				ImGui::TableNextColumn();
				ImGui::Text("%d", layer.clips);
				if (!layer.present)
					ImGui::PopStyleColor();
			}
			ImGui::EndTable();
		}

		if (ImGui::CollapsingHeader("Trees", ImGuiTreeNodeFlags_DefaultOpen))
		{
			SwitchBox("Only nodes with weight", g_onlyContributing);
			if (detail.trees.empty())
				ImGui::TextDisabled("No trees running.");
			for (size_t i = 0; i < detail.trees.size(); i++)
				DrawTree(detail.trees[i], static_cast<int>(i));
			if (detail.treesCut)
				ImGui::TextDisabled("(more trees than are shown)");
		}

		if (ImGui::CollapsingHeader("Clips", ImGuiTreeNodeFlags_DefaultOpen))
		{
			int weighted = 0;
			for (const ANIM_DEBUG::Clip& clip : detail.clips)
				weighted += clip.weight > 0.0f ? 1 : 0;
			ImGui::TextDisabled("%d playing with weight, %d with none.", weighted, static_cast<int>(detail.clips.size()) - weighted);
			DrawClips(detail.clips, true);
			if (static_cast<int>(detail.clips.size()) > weighted && ImGui::TreeNode("Without weight"))
			{
				DrawClips(detail.clips, false);
				ImGui::TreePop();
			}
		}

		if (ImGui::CollapsingHeader("Movement"))
		{
			const ANIM_DEBUG::Extras& extras = detail.extras;
			Vector("Animated velocity", extras.blended);
			Vector("Asked-for velocity", extras.desired);
			Vector("Warped velocity", extras.warped);
			ImGui::Text("Turning: animated %.0f, asked for %.0f, warped %.0f deg/s", extras.blendedYaw * 57.29578f, extras.desiredYaw * 57.29578f, extras.warpedYaw * 57.29578f);
			ImGui::Text("Velocity warping: %s; turning warping: %s", ANIM_DEBUG::WarpingUsageName(extras.linearWarping), ANIM_DEBUG::WarpingUsageName(extras.angularWarping));
			if (detail.motion.read)
			{
				Vector("Movement from animation this frame", detail.motion.movement[0]);
				Vector("Still to come", detail.motion.movement[1]);
				Vector("In all", detail.motion.movement[2]);
				ImGui::Text("Turning from animation: %.1f this frame, %.1f to come, %.1f in all (deg)", detail.motion.orientation[0] * 57.29578f,
					detail.motion.orientation[1] * 57.29578f, detail.motion.orientation[2] * 57.29578f);
			}
		}

		if (ImGui::CollapsingHeader("Reference locator") && detail.locator.read)
		{
			const ANIM_DEBUG::Locator& locator = detail.locator;
			ImGui::Text("%s (component %d, convergence %d, orientation %d)", ANIM_DEBUG::LocatorUsageName(locator.usage), locator.component, locator.convergence, locator.orientationModel);
			ImGui::Text("Weight %.2f, orientation weight %.2f", locator.weight, locator.orientationWeight);
			ImGui::Text("At %.2f, %.2f, %.2f", locator.position[0], locator.position[1], locator.position[2]);
			ImGui::TextDisabled("Matched animations (kills, doors, ladders...) line the character up with this point.");
		}

		if (ImGui::CollapsingHeader("IK"))
		{
			const ANIM_DEBUG::Extras& extras = detail.extras;
			ImGui::Text("Feet placed by themselves: %s (solver %d)", extras.autoFootIk ? "yes" : "no", extras.footSolver);
			if (extras.ikCount == 0)
				ImGui::TextDisabled("No IK asked for.");
			else if (ImGui::BeginTable("ik", 7, ImGuiTableFlags_RowBg | ImGuiTableFlags_BordersInnerV | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY | ImGuiTableFlags_SizingFixedFit,
				ImVec2(0.0f, TableHeight(extras.ikCount, 220.0f * DEBUG_MENU::Scale()))))
			{
				ImGui::TableSetupScrollFreeze(0, 1);
				ImGui::TableSetupColumn("Layer");
				ImGui::TableSetupColumn("Target");
				ImGui::TableSetupColumn("Solver");
				ImGui::TableSetupColumn("Control");
				ImGui::TableSetupColumn("Where");
				ImGui::TableSetupColumn("Gain");
				ImGui::TableSetupColumn("Correction");
				ImGui::TableHeadersRow();
				for (int i = 0; i < extras.ikCount; i++)
				{
					const ANIM_DEBUG::IkTarget& target = extras.ik[i];
					ImGui::TableNextRow();
					ImGui::TableNextColumn();
					ImGui::TextUnformatted(ANIM_DEBUG::LayerName(target.layer));
					ImGui::TableNextColumn();
					ImGui::TextUnformatted(ANIM_DEBUG::IkTargetName(target.target));
					ImGui::TableNextColumn();
					ImGui::TextUnformatted(ANIM_DEBUG::IkSolverTypeName(target.type));
					ImGui::TableNextColumn();
					ImGui::TextUnformatted(ANIM_DEBUG::IkControlName(target.control));
					ImGui::TableNextColumn();
					ImGui::Text("%.2f, %.2f, %.2f", target.position[0], target.position[1], target.position[2]);
					ImGui::TableNextColumn();
					ImGui::Text("%.2f", target.gain);
					ImGui::TableNextColumn();
					ImGui::Text("up to %.2f m (full from %.2f m)", target.maxCorrection, target.fullCorrectionDistance);
				}
				ImGui::EndTable();
			}
		}
	}

	const bool g_added = DEBUG_MENU::AddTab("Animation", 25, DrawTab, "Characters") && DEBUG_MENU::AddOverlay(15, DrawOverlay);
}

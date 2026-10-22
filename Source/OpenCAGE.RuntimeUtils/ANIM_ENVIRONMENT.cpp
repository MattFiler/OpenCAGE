#include "ANIM_NAMES.h"
#include "DEBUG_MENU.h"
#include "LIVE_LINK.h"

#include <algorithm>
#include <atomic>
#include <cfloat>
#include <cmath>
#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <imgui.h>

/*
	Addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).

	Environment animations in the Animation tab, a section after the tab's own views: the animated objects of the level
	(doors, lifts, machinery, anything a Play Environment Animation entity plays) listed with what they play, and their
	skeletons drawn over the game.

	The game keeps these in one environment animation system for the level: a list of animated models, each with the
	script entity it belongs to, its rig and its pose (every bone relative to the model, worked out after each update), the
	matrix that places it in the world, the clip it plays and whether it is playing, paused or visible. All of it is read
	at the frame's presentation with every read guarded (a level can be unloading; a model is only drawn once loaded).
*/
namespace
{
	// ---- Where things are ----

	constexpr uintptr_t kGameGlobals = 0x012F0C88;   // a pointer to the game's globals
	constexpr uint32_t kGlobalsEnvironment = 0x7C;   // the environment animation system
	// The system: its models (an array of pointers, then how many)
	constexpr uint32_t kModels = 0x08;
	constexpr uint32_t kModelCount = 0x0C;
	constexpr uint32_t kMaxModels = 4096;

	// A model: the matrix placing it in the world (3 rows of 4: each row an axis' x, y or z part, then the position's),
	// the script entity it belongs to (two ids), its pose, its rig's name hash, its two clip controllers, a word of bits
	// (8: the controller in use, 9-10: loaded - 2 when done) and a byte of bits (0 playing, 1 paused, 3 visible,
	// 6 attached to something, 7 a character's)
	constexpr uint32_t kModelMatrix = 0x10;
	constexpr uint32_t kModelEntity = 0x9C;
	constexpr uint32_t kModelPose = 0xB4;
	constexpr uint32_t kModelSkeletonName = 0xC0;
	constexpr uint32_t kModelControllers = 0xD4;
	constexpr uint32_t kModelBits = 0x11C;
	constexpr uint32_t kModelFlags = 0x120;
	constexpr uint32_t kLoaded = 2;

	// A pose (see ANIM_DEBUG.cpp): its rig, local and model-space bones (48 bytes each: translation, rotation x y z w,
	// scale) and whether each is up to date; a rig's parents and bone count
	constexpr uint32_t kPoseRig = 0x00;
	constexpr uint32_t kPoseLocal = 0x04;
	constexpr uint32_t kPoseLocalCount = 0x08;
	constexpr uint32_t kPoseModel = 0x10;
	constexpr uint32_t kPoseModelCount = 0x14;
	constexpr uint32_t kPoseModelInSync = 0x28;
	constexpr uint32_t kPoseLocalInSync = 0x29;
	constexpr uint32_t kRigParents = 0x0C;
	constexpr uint32_t kRigParentCount = 0x10;
	constexpr uint32_t kRigBoneCount = 0x1C;
	constexpr uint32_t kBoneSize = 48;
	constexpr uint32_t kBoneRotation = 0x10;
	constexpr int kMaxBones = 512;

	// A clip controller: where it is in its clip, the clip (its label hash at +0x10; its length through its binding's
	// animation, at +0x18 then +0xC then +0xC)
	constexpr uint32_t kControllerTime = 0x08;
	constexpr uint32_t kControllerClip = 0xA0;
	constexpr uint32_t kClipLabel = 0x10;
	constexpr uint32_t kClipBinding = 0x18;
	constexpr uint32_t kBindingAnimation = 0x0C;
	constexpr uint32_t kAnimationDuration = 0x0C;

	std::atomic<bool> g_skeletons = false, g_labels = true;
	std::atomic<float> g_maxDistance = 40.0f;

	// ---- Reading ----

	struct Model
	{
		uintptr_t address = 0;
		bool loaded = false, playing = false, paused = false, visible = false, attached = false, character = false;
		uint32_t entity = 0, instance = 0, skeleton = 0, clip = 0;
		float time = 0.0f, duration = 0.0f;
		float matrix[12] = {};
		float position[3] = {};
		int bones = 0;
		bool haveBones = false;
	};

	struct Bones
	{
		int count = 0;
		int16_t parents[kMaxBones];
		float positions[kMaxBones][3];
	};

	uintptr_t Pointer(uintptr_t at)
	{
		__try
		{
			return at ? *reinterpret_cast<const uintptr_t*>(at) : 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
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

	// The system's models (their addresses), false when there is none
	bool ModelList(std::vector<uintptr_t>& out)
	{
		out.clear();
		const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kGameGlobals);
		const uintptr_t globals = at ? Pointer(at) : 0;
		const uintptr_t system = globals ? Pointer(globals + kGlobalsEnvironment) : 0;
		if (!system)
			return false;
		const uintptr_t models = Pointer(system + kModels);
		const uint32_t count = static_cast<uint32_t>(Pointer(system + kModelCount));
		if (!models || count > kMaxModels)
			return false;
		out.resize(count);
		__try
		{
			memcpy(out.data(), reinterpret_cast<const void*>(models), count * sizeof(uintptr_t));
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool ReadModel(uintptr_t address, Model& out)
	{
		__try
		{
			const uint8_t* m = reinterpret_cast<const uint8_t*>(address);
			if (!m)
				return false;
			out.address = address;
			const uint32_t bits = *reinterpret_cast<const uint32_t*>(m + kModelBits);
			const uint8_t flags = m[kModelFlags];
			out.loaded = ((bits >> 9) & 3) == kLoaded;
			out.playing = (flags & 0x01) != 0;
			out.paused = (flags & 0x02) != 0;
			out.visible = (flags & 0x08) != 0;
			out.attached = (flags & 0x40) != 0;
			out.character = (flags & 0x80) != 0;
			out.entity = *reinterpret_cast<const uint32_t*>(m + kModelEntity);
			out.instance = *reinterpret_cast<const uint32_t*>(m + kModelEntity + 4);
			out.skeleton = *reinterpret_cast<const uint32_t*>(m + kModelSkeletonName);
			memcpy(out.matrix, m + kModelMatrix, sizeof(out.matrix));
			out.position[0] = out.matrix[3];
			out.position[1] = out.matrix[7];
			out.position[2] = out.matrix[11];
			const uint8_t* controller = *reinterpret_cast<const uint8_t* const*>(m + kModelControllers + 4 * ((bits >> 8) & 1));
			if (controller)
			{
				out.time = *reinterpret_cast<const float*>(controller + kControllerTime);
				const uint8_t* clip = *reinterpret_cast<const uint8_t* const*>(controller + kControllerClip);
				if (clip)
				{
					out.clip = *reinterpret_cast<const uint32_t*>(clip + kClipLabel);
					const uint8_t* binding = *reinterpret_cast<const uint8_t* const*>(clip + kClipBinding);
					const uint8_t* animation = binding ? *reinterpret_cast<const uint8_t* const*>(binding + kBindingAnimation) : nullptr;
					if (animation)
						out.duration = *reinterpret_cast<const float*>(animation + kAnimationDuration);
				}
			}
			const uint8_t* pose = *reinterpret_cast<const uint8_t* const*>(m + kModelPose);
			const uint8_t* rig = pose ? *reinterpret_cast<const uint8_t* const*>(pose + kPoseRig) : nullptr;
			if (rig)
				out.bones = *reinterpret_cast<const int*>(rig + kRigBoneCount);
			for (float value : out.matrix)
				if (!std::isfinite(value))
					return false;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// v rotated by the unit quaternion q (x, y, z, w)
	void Rotate(const float q[4], const float v[3], float out[3])
	{
		const float tx = 2.0f * (q[1] * v[2] - q[2] * v[1]);
		const float ty = 2.0f * (q[2] * v[0] - q[0] * v[2]);
		const float tz = 2.0f * (q[0] * v[1] - q[1] * v[0]);
		out[0] = v[0] + q[3] * tx + (q[1] * tz - q[2] * ty);
		out[1] = v[1] + q[3] * ty + (q[2] * tx - q[0] * tz);
		out[2] = v[2] + q[3] * tz + (q[0] * ty - q[1] * tx);
	}

	void Multiply(const float a[4], const float b[4], float out[4])
	{
		out[0] = a[3] * b[0] + a[0] * b[3] + a[1] * b[2] - a[2] * b[1];
		out[1] = a[3] * b[1] - a[0] * b[2] + a[1] * b[3] + a[2] * b[0];
		out[2] = a[3] * b[2] + a[0] * b[1] - a[1] * b[0] + a[2] * b[3];
		out[3] = a[3] * b[3] - a[0] * b[0] - a[1] * b[1] - a[2] * b[2];
	}

	// The bones relative to the model as they are (or the local ones, when the model ones are not up to date)
	struct RawBones
	{
		bool modelInSync = false, localInSync = false;
		float model[kMaxBones][2][4];
		float local[kMaxBones][2][4];
	};

	bool ReadRawBones(uintptr_t address, Bones& out, RawBones& raw)
	{
		__try
		{
			const uint8_t* m = reinterpret_cast<const uint8_t*>(address);
			const uint8_t* pose = *reinterpret_cast<const uint8_t* const*>(m + kModelPose);
			const uint8_t* rig = pose ? *reinterpret_cast<const uint8_t* const*>(pose + kPoseRig) : nullptr;
			if (!rig)
				return false;
			const int bones = *reinterpret_cast<const int*>(rig + kRigBoneCount);
			if (bones < 1 || bones > kMaxBones || *reinterpret_cast<const int*>(rig + kRigParentCount) != bones)
				return false;
			const int16_t* parents = *reinterpret_cast<const int16_t* const*>(rig + kRigParents);
			if (!parents)
				return false;
			raw.modelInSync = pose[kPoseModelInSync] != 0;
			raw.localInSync = pose[kPoseLocalInSync] != 0;
			const uint8_t* model = *reinterpret_cast<const uint8_t* const*>(pose + kPoseModel);
			const uint8_t* local = *reinterpret_cast<const uint8_t* const*>(pose + kPoseLocal);
			if (raw.modelInSync && (!model || *reinterpret_cast<const int*>(pose + kPoseModelCount) != bones))
				raw.modelInSync = false;
			if (raw.localInSync && (!local || *reinterpret_cast<const int*>(pose + kPoseLocalCount) != bones))
				raw.localInSync = false;
			if (!raw.modelInSync && !raw.localInSync)
				return false;
			out.count = bones;
			for (int i = 0; i < bones; i++)
			{
				const int16_t parent = parents[i];
				out.parents[i] = parent >= 0 && parent < i ? parent : -1;
				const uint8_t* bone = raw.modelInSync ? model + kBoneSize * i : local + kBoneSize * i;
				float(&into)[2][4] = raw.modelInSync ? raw.model[i] : raw.local[i];
				memcpy(into[0], bone, sizeof(float) * 4);
				memcpy(into[1], bone + kBoneRotation, sizeof(float) * 4);
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool ReadBones(const Model& model, Bones& out)
	{
		static RawBones raw;
		if (!ReadRawBones(model.address, out, raw))
			return false;
		// Relative to the model, worked out from the local bones when the game has not (parents come first)
		if (!raw.modelInSync)
			for (int i = 0; i < out.count; i++)
			{
				const int parent = out.parents[i];
				if (parent < 0)
				{
					memcpy(raw.model[i], raw.local[i], sizeof(raw.model[i]));
					continue;
				}
				float moved[3];
				Rotate(raw.model[parent][1], raw.local[i][0], moved);
				for (int k = 0; k < 3; k++)
					raw.model[i][0][k] = raw.model[parent][0][k] + moved[k];
				Multiply(raw.model[parent][1], raw.local[i][1], raw.model[i][1]);
			}
		// Into the world through the model's matrix
		const float* m = model.matrix;
		for (int i = 0; i < out.count; i++)
		{
			const float x = raw.model[i][0][0], y = raw.model[i][0][1], z = raw.model[i][0][2];
			out.positions[i][0] = m[0] * x + m[1] * y + m[2] * z + m[3];
			out.positions[i][1] = m[4] * x + m[5] * y + m[6] * z + m[7];
			out.positions[i][2] = m[8] * x + m[9] * y + m[10] * z + m[11];
			if (!std::isfinite(out.positions[i][0]) || !std::isfinite(out.positions[i][1]) || !std::isfinite(out.positions[i][2]))
				return false;
		}
		return true;
	}

	float Distance(const float a[3], const float b[3])
	{
		const float dx = a[0] - b[0], dy = a[1] - b[1], dz = a[2] - b[2];
		return std::sqrt(dx * dx + dy * dy + dz * dz);
	}

	std::string Format(const char* format, ...)
	{
		char text[512];
		va_list arguments;
		va_start(arguments, format);
		vsnprintf(text, sizeof(text), format, arguments);
		va_end(arguments);
		return text;
	}

	std::string StateText(const Model& model)
	{
		if (!model.loaded)
			return "loading";
		std::string state = model.playing ? (model.paused ? "paused" : "playing") : "stopped";
		if (model.attached)
			state += ", attached";
		if (model.character)
			state += ", a character's";
		return state;
	}

	// A clip's label without the folders it was made in
	std::string ClipName(uint32_t hash)
	{
		if (!hash)
			return "-";
		std::string name = ANIM_NAMES::Name(hash);
		const size_t slash = name.find_last_of("\\/");
		return slash == std::string::npos ? name : name.substr(slash + 1);
	}

	// This frame's models (read once a frame, when something wants them)
	struct Frame
	{
		int frame = -1;
		bool have = false;
		std::vector<Model> models;
	} g_frame;

	const Frame& Models()
	{
		const int frame = ImGui::GetFrameCount();
		if (g_frame.frame == frame)
			return g_frame;
		g_frame.frame = frame;
		g_frame.models.clear();
		std::vector<uintptr_t> list;
		g_frame.have = LevelRunning() && ModelList(list);
		for (uintptr_t address : list)
		{
			Model model;
			if (address && ReadModel(address, model))
				g_frame.models.push_back(model);
		}
		return g_frame;
	}

	uintptr_t g_selectedModel = 0;

	// ---- Names for OpenCAGE ----

	void Register()
	{
		static bool registered = false;
		if (registered)
			return;
		registered = true;
		DEBUG_MENU::AddSwitch("anim.env_skeletons", &g_skeletons);
		DEBUG_MENU::AddSwitch("anim.env_labels", &g_labels);
		DEBUG_MENU::AddValue("anim.env_distance", &g_maxDistance, 5.0f, 300.0f);
		DEBUG_MENU::AddCommand("anim.list_env_models", "the level's animated objects, a line each: number, rig, bones, state, clip, time / length, position, distance from the camera, script entity (id, instance)", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]()
			{
				DEBUG_MENU::CommandResult result;
				const Frame& frame = Models();
				if (!frame.have)
					return DEBUG_MENU::CommandResult{ false, "no environment animations (no level running)" };
				float camera[3] = {};
				const bool haveCamera = DEBUG_MENU::CameraPosition(camera);
				for (size_t i = 0; i < frame.models.size(); i++)
				{
					const Model& m = frame.models[i];
					result.message += Format("%d rig=%s bones=%d state=%s clip=%s time=%.2f/%.2f at=%.2f,%.2f,%.2f distance=%.1f entity=0x%08X instance=0x%08X\n",
						static_cast<int>(i), ANIM_NAMES::Name(m.skeleton).c_str(), m.bones, StateText(m).c_str(), ClipName(m.clip).c_str(), m.time, m.duration,
						m.position[0], m.position[1], m.position[2], haveCamera ? Distance(m.position, camera) : -1.0f, m.entity, m.instance);
				}
				result.message += Format("count=%d", static_cast<int>(frame.models.size()));
				return result;
			});
		});
		DEBUG_MENU::AddCommand("anim.select_env_model", "picks out an animated object by its number in anim.list_env_models (drawn highlighted, whatever its distance); none with -1", [](const std::string& argument)
		{
			const int index = atoi(argument.c_str());
			return DEBUG_MENU::AnswerOnRenderThread([index]()
			{
				DEBUG_MENU::CommandResult result;
				const Frame& frame = Models();
				if (index < 0)
				{
					g_selectedModel = 0;
					result.message = "selected=none";
					return result;
				}
				if (index >= static_cast<int>(frame.models.size()))
					return DEBUG_MENU::CommandResult{ false, Format("no animated object %d (there are %d)", index, static_cast<int>(frame.models.size())) };
				g_selectedModel = frame.models[index].address;
				result.message = Format("selected=%d\nrig=%s", index, ANIM_NAMES::Name(frame.models[index].skeleton).c_str());
				return result;
			});
		});
	}

	// ---- Drawing over the game ----

	void DrawModel(ImDrawList* drawList, const Model& model, bool selected)
	{
		static Bones bones;
		const int alpha = model.playing && !model.paused ? 230 : 150;
		const ImU32 colour = selected ? IM_COL32(255, 230, 80, 255) : model.playing ? IM_COL32(120, 230, 255, alpha) : IM_COL32(190, 190, 190, alpha);
		if (model.loaded && ReadBones(model, bones))
		{
			const float scale = DEBUG_MENU::Scale();
			for (int i = 0; i < bones.count; i++)
				if (bones.parents[i] >= 0)
					DEBUG_MENU::WorldLine(drawList, bones.positions[bones.parents[i]], bones.positions[i], colour, selected ? 2.0f : 1.5f);
			for (int i = 0; i < bones.count; i++)
			{
				ImVec2 at;
				if (DEBUG_MENU::Project(bones.positions[i], at))
					drawList->AddCircleFilled(at, (selected ? 2.5f : 2.0f) * scale, colour);
			}
		}
		else
			DEBUG_MENU::WorldCross(drawList, model.position, 0.15f, colour);
		if (g_labels || selected)
		{
			char text[200];
			snprintf(text, sizeof(text), "%s: %s %.1f / %.1f s (%s)", ANIM_NAMES::Name(model.skeleton).c_str(), ClipName(model.clip).c_str(), model.time, model.duration, StateText(model).c_str());
			DEBUG_MENU::WorldText(drawList, model.position, text, colour, -12.0f * DEBUG_MENU::Scale());
		}
	}

	void DrawOverlay(ImDrawList* drawList)
	{
		Register();
		if (!g_skeletons)
			return;
		float camera[3];
		if (!DEBUG_MENU::CameraPosition(camera))
			return;
		const Frame& frame = Models();
		const float range = g_maxDistance;
		int drawn = 0;
		for (const Model& model : frame.models)
		{
			const bool selected = model.address == g_selectedModel;
			if (!selected && (Distance(model.position, camera) > range || drawn >= 64))
				continue;
			DrawModel(drawList, model, selected);
			drawn++;
		}
	}

	// ---- The tab ----

	void DrawTab()
	{
		Register();
		if (!ImGui::CollapsingHeader("Environment animations"))
			return;
		bool on = g_skeletons;
		if (ImGui::Checkbox("Draw their skeletons", &on))
			g_skeletons = on;
		ImGui::SameLine();
		bool labels = g_labels;
		if (ImGui::Checkbox("With labels", &labels))
			g_labels = labels;
		ImGui::SameLine();
		float range = g_maxDistance;
		ImGui::SetNextItemWidth(140.0f * DEBUG_MENU::Scale());
		if (ImGui::SliderFloat("Within", &range, 5.0f, 300.0f, "%.0f m"))
			g_maxDistance = range;
		const Frame& frame = Models();
		if (!frame.have)
		{
			ImGui::TextDisabled("No environment animations (no level running).");
			return;
		}
		if (frame.models.empty())
		{
			ImGui::TextDisabled("This level has no animated objects loaded.");
			return;
		}
		float camera[3] = {};
		const bool haveCamera = DEBUG_MENU::CameraPosition(camera);
		struct Row { const Model* model; float distance; };
		std::vector<Row> rows;
		for (const Model& model : frame.models)
			rows.push_back({ &model, haveCamera ? Distance(model.position, camera) : 0.0f });
		std::stable_sort(rows.begin(), rows.end(), [](const Row& a, const Row& b) { return a.distance < b.distance; });
		ImGui::TextDisabled("%d animated objects, nearest first. Click one to pick it out (drawn whatever its distance).", static_cast<int>(rows.size()));
		// Scrolls sideways rather than cutting off the last columns when the rig and clip names are long
		if (!ImGui::BeginTable("envmodels", 6, ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_Resizable | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY |
			ImGuiTableFlags_SizingFixedFit, ImVec2(0.0f, 220.0f * DEBUG_MENU::Scale())))
			return;
		ImGui::TableSetupScrollFreeze(0, 1);
		ImGui::TableSetupColumn("Rig");
		ImGui::TableSetupColumn("Clip");
		ImGui::TableSetupColumn("Time");
		ImGui::TableSetupColumn("State");
		ImGui::TableSetupColumn("Distance");
		ImGui::TableSetupColumn("Script entity");
		ImGui::TableHeadersRow();
		for (const Row& row : rows)
		{
			const Model& model = *row.model;
			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			ImGui::PushID(reinterpret_cast<const void*>(model.address));
			const bool selected = model.address == g_selectedModel;
			const std::string rig = Format("%s (%d bones)", ANIM_NAMES::Name(model.skeleton).c_str(), model.bones);
			if (ImGui::Selectable(rig.c_str(), selected, ImGuiSelectableFlags_SpanAllColumns))
				g_selectedModel = selected ? 0 : model.address;
			ImGui::PopID();
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(ClipName(model.clip).c_str());
			ImGui::TableNextColumn();
			ImGui::Text("%.2f / %.2f s", model.time, model.duration);
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(StateText(model).c_str());
			ImGui::TableNextColumn();
			ImGui::Text("%.1f m", row.distance);
			ImGui::TableNextColumn();
			ImGui::Text("0x%08X", model.entity);
			if (ImGui::IsItemHovered())
				ImGui::SetTooltip("Entity id 0x%08X in the composite instance with id 0x%08X (as OpenCAGE shows them).", model.entity, model.instance);
		}
		ImGui::EndTable();
	}

	const bool g_added = DEBUG_MENU::AddTab("Animation", 27, DrawTab, "Characters") && DEBUG_MENU::AddOverlay(17, DrawOverlay);
}

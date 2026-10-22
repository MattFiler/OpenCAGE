#include "ANIM_DEBUG.h"
#include "ANIM_NAMES.h"
#include "DEBUG_MENU.h"

#include <algorithm>
#include <atomic>
#include <cctype>
#include <cmath>
#include <cstdarg>
#include <cstdio>
#include <cstring>
#include <string>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <detours.h>
#include <imgui.h>

/*
	Addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).

	Switches in the Animation tab, a section after the tab's own views (ANIM_DEBUG_VIEWS.cpp) - and two more
	views: the reference locator drawn over the game, and how much each layer weighs on each group of bones.

	Each switch works through one of the game's own steps in posing a character, hooked: the step runs as it always does
	unless its switch is on (for every character, or only the selected one).
	- IK off: the step that solves a layer's IK (feet on the ground, hands on things, looking at things) is left out.
	- Velocity and turning warping off: a character's animations give it a velocity, and the game warps that towards the
	  velocity its movement asks for. The game has an "off" setting of its own for each (the warped velocity is then the
	  animated one): it is given for the step, and the character's own settings put back after it.
	- Level of detail off: far characters have their secondary layers turned off and are posed less often. The game's
	  answer for each layer is overridden to "on at full weight", and the character is posed every frame (except while
	  the game has it out of sight, in a zone that is not drawn, when it is still not posed).
	- A layer off: the opposite of the above for one layer - the game's own "turned off for distance" answer (off, no
	  weight), which leaves the layers below it as they are. Only the layers the game itself turns off for distance
	  (general additive, breathing, look-at, hand, face).
	- Reference pose: after each layer is posed, its pose is put in the rig's reference pose (the pose it was modelled in),
	  so the character stands in it (IK still applies unless it is off too).
	- Ragdoll pose off: the step that runs a ragdoll and copies its pose onto the character still runs (so the ragdoll
	  carries on as it would), but the character's own pose from before it is put back after it: the mesh shows what the
	  animations give instead of what the ragdoll does.
	All of these run on the game's animation workers, for one character each, and only read the switches.
*/
namespace
{
	// ---- Where things are ----

	constexpr uintptr_t kIkForLayer = 0x002A7890;        // solves one layer's IK (an IK object's)
	constexpr uintptr_t kWarpMovement = 0x00291F60;      // warps the frame's animated velocity and turning (an animation system's)
	constexpr uintptr_t kLayerLod = 0x00292600;          // whether a layer is on for its distance, and its weight
	constexpr uintptr_t kSampleThisFrame = 0x002893F0;   // whether the character is posed this frame (by its posing rate)
	constexpr uintptr_t kBuildLayerPose = 0x002A1EF0;    // poses one layer from its animations
	constexpr uintptr_t kPoseToReference = 0x007B9CD0;   // puts a pose in its rig's reference pose
	constexpr uintptr_t kFinalPoseFromRagdoll = 0x0029CAC0; // runs the ragdoll and copies its pose onto the final pose

	// A character's animation system is inside it: the hooked steps are given that, or its IK object
	constexpr uint32_t kAnimationSystem = 0x3F0;
	constexpr uint32_t kCharacterIk = 0x710;
	constexpr uint32_t kCharacterRagdoll = 0x714;
	constexpr uint32_t kRagdollActive = 0x138;
	constexpr uint32_t kCharacterAnimated = 0x600;       // a pointer per layer (null: the layer is not used)
	constexpr uint32_t kCharacterSetUp = 0xF4C;          // a byte: the animation system is set up
	constexpr uint32_t kGroupWeights = 0xB24;            // per layer, 19 floats: how much the layer weighs on each group of bones
	constexpr uint32_t kGroupWeightStride = 0x4C;
	constexpr int kGroups = 19;
	// The reference locator: its matrix (rows: axes, then where it is), how it is used (0 not), its weight
	constexpr uint32_t kLocator = 0xF90;
	constexpr uint32_t kLocatorUsage = 0xFF4;
	constexpr uint32_t kLocatorWeight = 0x1004;

	// In the animation system: the poses per layer, the linear and turning warping settings (2: off), posed this frame
	constexpr uint32_t kPoses = 0x230;
	constexpr uint32_t kFinalPose = 0x24C;
	constexpr uint32_t kLinearWarping = 0xC5C;
	constexpr uint32_t kAngularWarping = 0xC60;
	constexpr int kWarpingOff = 2;
	constexpr uint32_t kSampledThisFrame = 0xD00;
	constexpr int kLayerOn = 0, kLayerOff = 1;

	// A pose: its rig, its bones relative to their parents (data, count) and relative to the character (data, count), the
	// bones' flags (data, count), and whether each set is up to date. A rig's bone count.
	constexpr uint32_t kPoseRig = 0x00;
	constexpr uint32_t kPoseLocal = 0x04;
	constexpr uint32_t kPoseLocalCount = 0x08;
	constexpr uint32_t kPoseModel = 0x10;
	constexpr uint32_t kPoseModelCount = 0x14;
	constexpr uint32_t kPoseFlags = 0x1C;
	constexpr uint32_t kPoseFlagCount = 0x20;
	constexpr uint32_t kPoseModelInSync = 0x28;
	constexpr uint32_t kPoseLocalInSync = 0x29;
	constexpr uint32_t kRigBoneCount = 0x1C;
	constexpr uint32_t kBoneSize = 48;
	constexpr int kMaxBones = 512;

	typedef void(__thiscall* t_ik_for_layer)(void* ik, uint32_t layer, int rig, void* pose, void* transform);
	t_ik_for_layer ik_for_layer = reinterpret_cast<t_ik_for_layer>(DEVTOOLS_RELATIVE_ADDRESS(kIkForLayer));
	typedef void(__thiscall* t_warp_movement)(void* animation);
	t_warp_movement warp_movement = reinterpret_cast<t_warp_movement>(DEVTOOLS_RELATIVE_ADDRESS(kWarpMovement));
	typedef int(__thiscall* t_layer_lod)(void* animation, int layer, float* weight);
	t_layer_lod layer_lod = reinterpret_cast<t_layer_lod>(DEVTOOLS_RELATIVE_ADDRESS(kLayerLod));
	typedef void(__thiscall* t_sample_this_frame)(void* animation);
	t_sample_this_frame sample_this_frame = reinterpret_cast<t_sample_this_frame>(DEVTOOLS_RELATIVE_ADDRESS(kSampleThisFrame));
	typedef void(__thiscall* t_build_layer_pose)(void* animation, int layer);
	t_build_layer_pose build_layer_pose = reinterpret_cast<t_build_layer_pose>(DEVTOOLS_RELATIVE_ADDRESS(kBuildLayerPose));
	typedef void(__thiscall* t_pose_to_reference)(void* pose);
	const auto pose_to_reference = reinterpret_cast<t_pose_to_reference>(DEVTOOLS_RELATIVE_ADDRESS(kPoseToReference));
	typedef void(__thiscall* t_final_pose_from_ragdoll)(void* animation);
	t_final_pose_from_ragdoll final_pose_from_ragdoll = reinterpret_cast<t_final_pose_from_ragdoll>(DEVTOOLS_RELATIVE_ADDRESS(kFinalPoseFromRagdoll));

	// ---- The switches ----

	std::atomic<bool> g_noIk = false, g_noLinearWarping = false, g_noAngularWarping = false, g_noLod = false;
	std::atomic<bool> g_referencePose = false, g_noRagdollPose = false, g_selectedOnly = false;
	std::atomic<bool> g_locatorOverlay = false;

	// The layers that can be switched off (those the game itself turns off for distance), and their switches
	struct LayerSwitch { int layer; const char* name; const char* label; std::atomic<bool> off; };
	LayerSwitch g_layerSwitches[] = {
		{ 1, "anim.layer_off.additive", "General additive", false },
		{ 2, "anim.layer_off.breathing", "Breathing", false },
		{ 3, "anim.layer_off.look_at", "Look-at", false },
		{ 5, "anim.layer_off.hand", "Hand", false },
		{ 6, "anim.layer_off.face", "Face", false },
	};
	std::atomic<uint32_t> g_layersOff = 0;   // a bit per layer, from the switches (kept each frame)

	// The selected character (and its IK object), for "only the selected character" (from the render thread each frame)
	std::atomic<uintptr_t> g_selectedCharacter = 0, g_selectedIk = 0;
	bool g_hooked[6] = {};

	// Whether the switches apply to this animation system's character
	bool Applies(const void* animation)
	{
		return !g_selectedOnly || reinterpret_cast<uintptr_t>(animation) - kAnimationSystem == g_selectedCharacter;
	}

	// ---- Guarded reads and writes ----

	template <typename T>
	bool Read(uintptr_t at, T& out)
	{
		__try
		{
			out = *reinterpret_cast<const volatile T*>(at);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	template <typename T>
	bool Write(uintptr_t at, const T& value)
	{
		__try
		{
			*reinterpret_cast<volatile T*>(at) = value;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool Copy(void* to, const void* from, size_t size)
	{
		__try
		{
			memcpy(to, from, size);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// ---- The hooks (animation workers) ----

	// Whether the game has the character out of sight (in a zone that is not drawn): it then turns every layer off, the
	// base one too, and does not pose it - left so with level of detail off
	bool OutOfSight(void* animation)
	{
		float weight = 0.0f;
		return layer_lod && layer_lod(animation, 0, &weight) == kLayerOff;
	}

	__declspec(noinline)
	void __fastcall h_ik_for_layer(void* ik, void* /*_EDX*/, uint32_t layer, int rig, void* pose, void* transform)
	{
		if (g_noIk && (!g_selectedOnly || reinterpret_cast<uintptr_t>(ik) == g_selectedIk))
			return;
		ik_for_layer(ik, layer, rig, pose, transform);
	}

	__declspec(noinline)
	void __fastcall h_warp_movement(void* animation, void* /*_EDX*/)
	{
		const bool linear = g_noLinearWarping && Applies(animation), angular = g_noAngularWarping && Applies(animation);
		const uintptr_t at = reinterpret_cast<uintptr_t>(animation);
		int linearWas = 0, angularWas = 0;
		if ((!linear && !angular) || !Read(at + kLinearWarping, linearWas) || !Read(at + kAngularWarping, angularWas))
		{
			warp_movement(animation);
			return;
		}
		// The game's own "off" for the step, and the character's settings back after it (only those changed)
		if (linear)
			Write(at + kLinearWarping, kWarpingOff);
		if (angular)
			Write(at + kAngularWarping, kWarpingOff);
		warp_movement(animation);
		if (linear)
			Write(at + kLinearWarping, linearWas);
		if (angular)
			Write(at + kAngularWarping, angularWas);
	}

	__declspec(noinline)
	int __fastcall h_layer_lod(void* animation, void* /*_EDX*/, int layer, float* weight)
	{
		int result = layer_lod(animation, layer, weight);
		if ((g_noLod || g_layersOff) && weight && Applies(animation))
		{
			if (layer >= 0 && layer < 32 && (g_layersOff & (1u << layer)))
			{
				Write(reinterpret_cast<uintptr_t>(weight), 0.0f);
				result = kLayerOff;
			}
			else if (g_noLod && (result == kLayerOn || !OutOfSight(animation)))
			{
				Write(reinterpret_cast<uintptr_t>(weight), 1.0f);
				result = kLayerOn;
			}
		}
		return result;
	}

	__declspec(noinline)
	void __fastcall h_sample_this_frame(void* animation, void* /*_EDX*/)
	{
		sample_this_frame(animation);
		if (g_noLod && Applies(animation) && !OutOfSight(animation))
			Write<uint8_t>(reinterpret_cast<uintptr_t>(animation) + kSampledThisFrame, 1);
	}

	__declspec(noinline)
	void __fastcall h_build_layer_pose(void* animation, void* /*_EDX*/, int layer)
	{
		build_layer_pose(animation, layer);
		if (!g_referencePose || !pose_to_reference || layer < 0 || layer >= ANIM_DEBUG::kLayers || !Applies(animation))
			return;
		uintptr_t pose = 0;
		if (Read(reinterpret_cast<uintptr_t>(animation) + kPoses + 4 * layer, pose) && pose)
			pose_to_reference(reinterpret_cast<void*>(pose));
	}

	// The final pose kept across the ragdoll's step: a few buffers, one taken per character being posed at once (the
	// workers pose several characters together); when all are busy, that character's pose is left as the ragdoll made it
	constexpr int kPoseSaves = 8;
	struct PoseSave
	{
		std::atomic<bool> busy = false;
		uint8_t local[kMaxBones * kBoneSize];
		uint8_t model[kMaxBones * kBoneSize];
		uint32_t flags[kMaxBones];
	};
	PoseSave g_poseSaves[kPoseSaves];

	struct PoseArrays
	{
		uint8_t* pose;
		uint8_t* local;
		uint8_t* model;
		uint32_t* flags;
		int bones;
		uint8_t modelInSync, localInSync;
	};

	// The final pose's arrays, when they are all there and the same size (false otherwise)
	bool FinalPoseArrays(void* animation, PoseArrays& out)
	{
		__try
		{
			uint8_t* pose = *reinterpret_cast<uint8_t**>(static_cast<uint8_t*>(animation) + kFinalPose);
			if (!pose)
				return false;
			const uint8_t* rig = *reinterpret_cast<uint8_t**>(pose + kPoseRig);
			if (!rig)
				return false;
			out.pose = pose;
			out.bones = *reinterpret_cast<const int*>(rig + kRigBoneCount);
			out.local = *reinterpret_cast<uint8_t**>(pose + kPoseLocal);
			out.model = *reinterpret_cast<uint8_t**>(pose + kPoseModel);
			out.flags = *reinterpret_cast<uint32_t**>(pose + kPoseFlags);
			out.modelInSync = pose[kPoseModelInSync];
			out.localInSync = pose[kPoseLocalInSync];
			return out.bones > 0 && out.bones <= kMaxBones && out.local && out.model && out.flags &&
				*reinterpret_cast<const int*>(pose + kPoseLocalCount) == out.bones && *reinterpret_cast<const int*>(pose + kPoseModelCount) == out.bones &&
				*reinterpret_cast<const int*>(pose + kPoseFlagCount) == out.bones;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool RagdollActive(void* animation)
	{
		__try
		{
			const uint8_t* character = static_cast<uint8_t*>(animation) - kAnimationSystem;
			const uint8_t* ragdoll = *reinterpret_cast<const uint8_t* const*>(character + kCharacterRagdoll);
			return ragdoll && ragdoll[kRagdollActive];
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	__declspec(noinline)
	void __fastcall h_final_pose_from_ragdoll(void* animation, void* /*_EDX*/)
	{
		if (!g_noRagdollPose || !Applies(animation) || !RagdollActive(animation))
		{
			final_pose_from_ragdoll(animation);
			return;
		}
		PoseArrays before;
		PoseSave* save = nullptr;
		if (FinalPoseArrays(animation, before))
			for (PoseSave& slot : g_poseSaves)
				if (!slot.busy.exchange(true))
				{
					save = &slot;
					break;
				}
		if (save && (!Copy(save->local, before.local, before.bones * kBoneSize) || !Copy(save->model, before.model, before.bones * kBoneSize) ||
			!Copy(save->flags, before.flags, before.bones * sizeof(uint32_t))))
		{
			save->busy = false;
			save = nullptr;
		}
		if (!save)
		{
			final_pose_from_ragdoll(animation);
			return;
		}
		final_pose_from_ragdoll(animation);
		// Put back only into the same pose and arrays (the step does not make new ones)
		PoseArrays after;
		if (FinalPoseArrays(animation, after) && after.pose == before.pose && after.local == before.local && after.model == before.model &&
			after.flags == before.flags && after.bones == before.bones)
		{
			Copy(after.local, save->local, after.bones * kBoneSize);
			Copy(after.model, save->model, after.bones * kBoneSize);
			Copy(after.flags, save->flags, after.bones * sizeof(uint32_t));
			Write(reinterpret_cast<uintptr_t>(after.pose) + kPoseModelInSync, before.modelInSync);
			Write(reinterpret_cast<uintptr_t>(after.pose) + kPoseLocalInSync, before.localInSync);
		}
		save->busy = false;
	}

	void AttachHooks(bool attach)
	{
		struct Hook { void** original; void* detoured; };
		const Hook hooks[] = {
			{ reinterpret_cast<void**>(&ik_for_layer), reinterpret_cast<void*>(&h_ik_for_layer) },
			{ reinterpret_cast<void**>(&warp_movement), reinterpret_cast<void*>(&h_warp_movement) },
			{ reinterpret_cast<void**>(&layer_lod), reinterpret_cast<void*>(&h_layer_lod) },
			{ reinterpret_cast<void**>(&sample_this_frame), reinterpret_cast<void*>(&h_sample_this_frame) },
			{ reinterpret_cast<void**>(&build_layer_pose), reinterpret_cast<void*>(&h_build_layer_pose) },
			{ reinterpret_cast<void**>(&final_pose_from_ragdoll), reinterpret_cast<void*>(&h_final_pose_from_ragdoll) },
		};
		static_assert(sizeof(hooks) / sizeof(hooks[0]) == sizeof(g_hooked) / sizeof(g_hooked[0]), "a flag per hook");
		for (size_t i = 0; i < sizeof(hooks) / sizeof(hooks[0]); i++)
		{
			if (attach && (g_hooked[i] = *hooks[i].original != nullptr))
				DetourAttach(hooks[i].original, hooks[i].detoured);
			else if (!attach && g_hooked[i])
			{
				DetourDetach(hooks[i].original, hooks[i].detoured);
				g_hooked[i] = false;
			}
		}
	}

	const bool g_hooks = DEBUG_MENU::AddHooks(AttachHooks);

	// ---- Reads for the views (render thread) ----

	struct LocatorView
	{
		bool read = false;
		int usage = 0;
		float weight = 0.0f;
		float matrix[16] = {};
	};

	bool ReadLocator(const uint8_t* character, LocatorView& out)
	{
		__try
		{
			if (!character || !character[kCharacterSetUp])
				return false;
			memcpy(out.matrix, character + kLocator, sizeof(out.matrix));
			out.usage = *reinterpret_cast<const int*>(character + kLocatorUsage);
			out.weight = *reinterpret_cast<const float*>(character + kLocatorWeight);
			out.read = true;
			for (float value : out.matrix)
				if (!std::isfinite(value))
					out.read = false;
			return out.read;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	struct GroupWeights
	{
		bool read = false;
		bool present[ANIM_DEBUG::kLayers] = {};
		float weights[ANIM_DEBUG::kLayers][kGroups] = {};
	};

	bool ReadGroupWeights(const uint8_t* character, GroupWeights& out)
	{
		__try
		{
			if (!character || !character[kCharacterSetUp])
				return false;
			for (int l = 0; l < ANIM_DEBUG::kLayers; l++)
			{
				out.present[l] = *reinterpret_cast<const void* const*>(character + kCharacterAnimated + 4 * l) != nullptr;
				memcpy(out.weights[l], character + kGroupWeights + kGroupWeightStride * l, sizeof(out.weights[l]));
			}
			out.read = true;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	const char* GroupName(int group)
	{
		static const char* const kNames[kGroups] = { "Hips", "Torso", "Neck", "Head", "Face", "Left leg", "Right leg", "Left arm", "Right arm",
			"Left hand", "Right hand", "Left fingers", "Right fingers", "Tail", "Lips", "Eyes", "Left shoulder", "Right shoulder", "Root" };
		return group >= 0 && group < kGroups ? kNames[group] : "?";
	}

	const char* LocatorUsage(int usage)
	{
		switch (usage)
		{
		case 0: return "not in use";
		case 1: return "in use";
		case 2: return "clamped to a line";
		default: return "?";
		}
	}

	std::string Format(const char* format, ...)
	{
		char text[640];
		va_list arguments;
		va_start(arguments, format);
		vsnprintf(text, sizeof(text), format, arguments);
		va_end(arguments);
		return text;
	}

	// ---- Names for OpenCAGE ----

	std::string Report(const DEBUG_MENU::Character& character)
	{
		static ANIM_DEBUG::Detail detail;
		if (!ANIM_DEBUG::ReadDetail(character.object, detail))
			return "";
		const ANIM_DEBUG::Header& h = detail.header;
		std::string out = Format("character=%s\nskeleton=%s\nanimation_set=%s\ntree_set=%s\n", character.name.c_str(), ANIM_NAMES::Name(h.skeleton).c_str(),
			ANIM_NAMES::Name(h.animationSet).c_str(), ANIM_NAMES::Name(h.treeSet).c_str());
		for (int i = 0; i < 3; i++)
			out += Format("%s=%s\n", ANIM_DEBUG::ContextName(i), h.contextCounts[i] > 0 ? ANIM_NAMES::Name(h.contexts[i]).c_str() : "none");
		out += Format("flags=0x%02X\nsampling=%s\nsampled_this_frame=%d\nragdoll_active=%d\n", h.flags, ANIM_DEBUG::SamplingName(h.sampling), h.sampledThisFrame ? 1 : 0,
			h.ragdollActive ? 1 : 0);
		for (int l = 0; l < ANIM_DEBUG::kLayers; l++)
		{
			const ANIM_DEBUG::Layer& layer = detail.layers[l];
			out += Format("layer %d %s: used=%d lod=%s weight=%.2f trees=%d clips=%d\n", l, ANIM_DEBUG::LayerName(l), layer.present ? 1 : 0, layer.lod == 0 ? "on" : "off",
				layer.weight, layer.trees, layer.clips);
		}
		for (const ANIM_DEBUG::Tree& tree : detail.trees)
			out += Format("tree layer=%d name=%s weight=%.2f ease=%.2f\n", tree.layer, ANIM_NAMES::Name(tree.name).c_str(), tree.global, tree.ease);
		for (const ANIM_DEBUG::Clip& clip : detail.clips)
			out += Format("clip layer=%d name=%s time=%.2f/%.2f weight=%.2f speed=%.2f%s\n", clip.layer, ANIM_NAMES::Name(clip.clip).c_str(), clip.time, clip.duration,
				clip.weight, clip.speed, clip.mirrored ? " mirrored" : "");
		out += Format("warping linear=%s angular=%s", ANIM_DEBUG::WarpingUsageName(detail.extras.linearWarping), ANIM_DEBUG::WarpingUsageName(detail.extras.angularWarping));
		return out;
	}

	void Register()
	{
		static bool registered = false;
		if (registered)
			return;
		registered = true;
		DEBUG_MENU::AddSwitch("anim.no_ik", &g_noIk);
		DEBUG_MENU::AddSwitch("anim.no_linear_warping", &g_noLinearWarping);
		DEBUG_MENU::AddSwitch("anim.no_angular_warping", &g_noAngularWarping);
		DEBUG_MENU::AddSwitch("anim.no_lod", &g_noLod);
		DEBUG_MENU::AddSwitch("anim.reference_pose", &g_referencePose);
		DEBUG_MENU::AddSwitch("anim.no_ragdoll_pose", &g_noRagdollPose);
		DEBUG_MENU::AddSwitch("anim.switches_selected_only", &g_selectedOnly);
		DEBUG_MENU::AddSwitch("anim.reference_locator", &g_locatorOverlay);
		for (LayerSwitch& layer : g_layerSwitches)
			DEBUG_MENU::AddSwitch(layer.name, &layer.off);

		DEBUG_MENU::AddCommand("anim.report", "the selected character's animation (or the one named): rigs, sets, contexts, layers, trees, clips, a line each", [](const std::string& argument)
		{
			return DEBUG_MENU::AnswerOnRenderThread([argument]()
			{
				DEBUG_MENU::CommandResult result;
				// The one named (the first whose name holds the text, ignoring case; it is selected too), else the selected one
				const DEBUG_MENU::Character* character = argument.empty() ? DEBUG_MENU::Selected() : nullptr;
				if (!argument.empty())
				{
					auto lower = [](std::string s) { for (char& c : s) c = static_cast<char>(tolower(static_cast<unsigned char>(c))); return s; };
					for (const DEBUG_MENU::Character& c : DEBUG_MENU::Characters())
						if (lower(c.name).find(lower(argument)) != std::string::npos)
						{
							character = &c;
							DEBUG_MENU::Select(c.id);
							break;
						}
				}
				if (!character)
					return DEBUG_MENU::CommandResult{ false, argument.empty() ? "no character selected" : "no character's name holds \"" + argument + "\"" };
				result.message = Report(*character);
				if (result.message.empty())
					return DEBUG_MENU::CommandResult{ false, "its animation system is not set up" };
				return result;
			});
		});
		DEBUG_MENU::AddCommand("anim.group_weights", "the selected character's weight of each used layer on each group of bones: a line per group", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]()
			{
				DEBUG_MENU::CommandResult result;
				const DEBUG_MENU::Character* character = DEBUG_MENU::Selected();
				GroupWeights weights;
				if (!character || !ReadGroupWeights(character->object, weights))
					return DEBUG_MENU::CommandResult{ false, "no character selected, or its animation system is not set up" };
				for (int g = 0; g < kGroups; g++)
				{
					result.message += GroupName(g);
					for (int l = 0; l < ANIM_DEBUG::kLayers; l++)
						if (weights.present[l])
							result.message += Format(" %d=%.2f", l, weights.weights[l][g]);
					result.message += "\n";
				}
				return result;
			});
		});
		DEBUG_MENU::AddCommand("anim.hooks", "which of the animation switches' hooks this game release has (1) or not (0)", [](const std::string&)
		{
			DEBUG_MENU::CommandResult result;
			result.message = Format("ik=%d\nwarping=%d\nlayer_lod=%d\nsampling=%d\nlayer_pose=%d\nreference_pose=%d\nragdoll=%d", g_hooked[0] ? 1 : 0, g_hooked[1] ? 1 : 0,
				g_hooked[2] ? 1 : 0, g_hooked[3] ? 1 : 0, g_hooked[4] ? 1 : 0, pose_to_reference ? 1 : 0, g_hooked[5] ? 1 : 0);
			return result;
		});
	}

	// ---- Drawing over the game ----

	void DrawLocator(ImDrawList* drawList, const DEBUG_MENU::Character& character)
	{
		LocatorView locator;
		if (!ReadLocator(character.object, locator) || locator.usage == 0)
			return;
		const float* m = locator.matrix;
		const float at[3] = { m[12], m[13], m[14] };
		static const ImU32 kAxisColours[3] = { IM_COL32(255, 70, 70, 230), IM_COL32(70, 255, 70, 230), IM_COL32(80, 120, 255, 230) };
		for (int axis = 0; axis < 3; axis++)
		{
			const float end[3] = { at[0] + m[axis * 4] * 0.3f, at[1] + m[axis * 4 + 1] * 0.3f, at[2] + m[axis * 4 + 2] * 0.3f };
			DEBUG_MENU::WorldLine(drawList, at, end, kAxisColours[axis], 2.0f);
		}
		DEBUG_MENU::WorldLine(drawList, character.position, at, IM_COL32(255, 220, 120, 160), 1.0f);
		char text[128];
		snprintf(text, sizeof(text), "reference locator: %s, weight %.2f", LocatorUsage(locator.usage), locator.weight);
		DEBUG_MENU::WorldText(drawList, at, text, IM_COL32(255, 220, 120, 255), -14.0f * DEBUG_MENU::Scale());
	}

	// Every frame (window open or not): names the switches, keeps the selected character for the hooks, draws the locator
	void DrawOverlay(ImDrawList* drawList)
	{
		Register();
		uint32_t off = 0;
		for (const LayerSwitch& layer : g_layerSwitches)
			if (layer.off)
				off |= 1u << layer.layer;
		g_layersOff = off;
		const DEBUG_MENU::Character* selected = DEBUG_MENU::Selected();
		uintptr_t ik = 0;
		if (selected)
			Read(reinterpret_cast<uintptr_t>(selected->object) + kCharacterIk, ik);
		g_selectedCharacter = selected ? reinterpret_cast<uintptr_t>(selected->object) : 0;
		g_selectedIk = ik;

		if (!g_locatorOverlay)
			return;
		bool all = false;
		DEBUG_MENU::GetSwitch("anim.all", all);
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
			if (all || character.id == DEBUG_MENU::SelectedId())
				DrawLocator(drawList, character);
	}

	// ---- The tab ----

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

	void SwitchBox(const char* label, std::atomic<bool>& on, bool available, const char* tip)
	{
		ImGui::BeginDisabled(!available);
		bool value = on;
		if (ImGui::Checkbox(label, &value))
			on = value;
		ImGui::EndDisabled();
		Tooltip(available ? tip : "Not available on this release of the game.");
	}

	void DrawSwitches()
	{
		SwitchBox("Only the selected character", g_selectedOnly, true, "The switches below change only the character selected above (none when none is); otherwise every character.");
		SwitchBox("IK off", g_noIk, g_hooked[0], "Leaves out IK: feet placed on the ground, hands on what they hold or touch, heads turned to look.");
		ImGui::SameLine();
		SwitchBox("Velocity warping off", g_noLinearWarping, g_hooked[1], "The velocity a character's animations give is no longer warped towards the one its movement asks for: it moves only as its animations move it (and can slide off its path).");
		ImGui::SameLine();
		SwitchBox("Turning warping off", g_noAngularWarping, g_hooked[1], "The same for turning.");
		SwitchBox("Level of detail off", g_noLod, g_hooked[2] && g_hooked[3], "Every layer on at full weight, and the character posed every frame, however far it is (characters out of sight are still not posed).");
		ImGui::SameLine();
		SwitchBox("Reference pose", g_referencePose, g_hooked[4] && pose_to_reference != nullptr, "Every layer's pose is put in its rig's reference pose (the pose it was modelled in) after it is posed. IK still applies unless it is off.");
		ImGui::SameLine();
		SwitchBox("Ragdoll pose off", g_noRagdollPose, g_hooked[5], "A character in ragdoll keeps the pose its animations give: the ragdoll still runs (its body falls), but its pose is not copied onto the character.");
		ImGui::TextDisabled("Layers off (as the game turns them off for distance: no weight, the layers below left as they are):");
		for (size_t i = 0; i < sizeof(g_layerSwitches) / sizeof(g_layerSwitches[0]); i++)
		{
			if (i)
				ImGui::SameLine();
			SwitchBox(g_layerSwitches[i].label, g_layerSwitches[i].off, g_hooked[2], "This layer is given no weight: what the layers below it pose stays.");
		}
	}

	void DrawLocatorSection(const DEBUG_MENU::Character* character)
	{
		bool on = g_locatorOverlay;
		if (ImGui::Checkbox("Draw the reference locator", &on))
			g_locatorOverlay = on;
		Tooltip("While a matched animation (a kill, a door, a ladder...) lines the character up with a point, its axes are drawn there, with a line from the character (every character with All characters on).");
		LocatorView locator;
		if (character && ReadLocator(character->object, locator))
			ImGui::Text("%s: %s, weight %.2f, at %.2f, %.2f, %.2f", character->name.c_str(), LocatorUsage(locator.usage), locator.weight, locator.matrix[12], locator.matrix[13], locator.matrix[14]);
	}

	void DrawGroupWeights(const DEBUG_MENU::Character* character)
	{
		GroupWeights weights;
		if (!character || !ReadGroupWeights(character->object, weights))
		{
			ImGui::TextDisabled("Pick a character.");
			return;
		}
		int columns = 1;
		for (int l = 0; l < ANIM_DEBUG::kLayers; l++)
			columns += weights.present[l] ? 1 : 0;
		if (columns == 1 || !ImGui::BeginTable("groupweights", columns, ImGuiTableFlags_RowBg | ImGuiTableFlags_BordersInnerV | ImGuiTableFlags_SizingFixedFit))
			return;
		ImGui::TableSetupColumn("Bones");
		for (int l = 0; l < ANIM_DEBUG::kLayers; l++)
			if (weights.present[l])
				ImGui::TableSetupColumn(ANIM_DEBUG::LayerName(l));
		ImGui::TableHeadersRow();
		for (int g = 0; g < kGroups; g++)
		{
			ImGui::TableNextRow();
			ImGui::TableNextColumn();
			ImGui::TextUnformatted(GroupName(g));
			for (int l = 0; l < ANIM_DEBUG::kLayers; l++)
			{
				if (!weights.present[l])
					continue;
				ImGui::TableNextColumn();
				const float w = weights.weights[l][g];
				if (w <= 0.0f)
					ImGui::TextDisabled("0");
				else
					ImGui::Text("%.2f", w);
			}
		}
		ImGui::EndTable();
		ImGui::TextDisabled("How much each layer's pose counts on each group of bones (its masks times its weight), as the game combines the layers.");
	}

	void DrawTab()
	{
		Register();
		ImGui::SeparatorText("Switches");
		DrawSwitches();
		const DEBUG_MENU::Character* character = DEBUG_MENU::Selected();
		if (ImGui::CollapsingHeader("Reference locator##tools"))
			DrawLocatorSection(character);
		if (ImGui::CollapsingHeader("Layer weights by bone group"))
			DrawGroupWeights(character);
	}

	const bool g_added = DEBUG_MENU::AddTab("Animation", 26, DrawTab, "Characters") && DEBUG_MENU::AddOverlay(16, DrawOverlay);
}

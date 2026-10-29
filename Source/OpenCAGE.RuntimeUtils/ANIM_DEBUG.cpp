#include "ANIM_DEBUG.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <cmath>
#include <cstring>

/*
	Addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds); every one
	used here is a place inside a character or the objects it points to, the same in all three. See ANIM_DEBUG.h for what
	is read and when.
*/
namespace
{
	// ---- In a character (the animation system it carries) ----

	constexpr uint32_t kSetUp = 0xF4C;                // a byte: the animation system is set up (cleared first when it closes)
	constexpr uint32_t kWorld = 0x400;                // the matrix the renderer places the mesh with (16 floats, rows)
	constexpr uint32_t kHelperMatrices = 0x440;       // the helper bones' world matrices, 0x40 apart (head first)
	constexpr uint32_t kHelperStride = 0x40;
	constexpr uint32_t kAnimatedSkeletons = 0x600;    // a pointer per layer (null: the layer is not used)
	constexpr uint32_t kPoses = 0x620;                // a pose per layer; the last is the final pose
	constexpr uint32_t kHeadBone = 0x690;             // a short: the head's bone
	constexpr uint32_t kTreeLists = 0x704;            // a pointer to eight lists (one per layer) of the trees it runs
	constexpr uint32_t kTreeListStride = 0x10;        // each: first, last
	constexpr uint32_t kIk = 0x710;
	constexpr uint32_t kRagdoll = 0x714;
	constexpr uint32_t kSkeletonDefinition = 0x718;
	constexpr uint32_t kAnimationDatabase = 0x720;
	constexpr uint32_t kContexts = 0x728;
	constexpr uint32_t kMovement = 0x730;             // three vectors: this frame's movement from animation, still to come, in all
	constexpr uint32_t kOrientation = 0x760;          // the same three for turning
	constexpr uint32_t kFlags = 0x7BC;                // a byte of switches (see FlagName)
	constexpr uint32_t kCustom = 0x7BD;
	constexpr uint32_t kLocator = 0xF90;              // the reference locator's matrix, then how it is used
	constexpr uint32_t kLocatorUsage = 0xFF4;
	constexpr uint32_t kLocatorComponent = 0xFF8;
	constexpr uint32_t kLocatorConvergence = 0xFFC;
	constexpr uint32_t kLocatorOrientationModel = 0x1000;
	constexpr uint32_t kLocatorWeight = 0x1004;
	constexpr uint32_t kLocatorOrientationWeight = 0x1008;
	constexpr uint32_t kDesiredVelocity = 0x1030;
	constexpr uint32_t kDesiredYaw = 0x1044;
	constexpr uint32_t kLinearWarping = 0x104C;
	constexpr uint32_t kAngularWarping = 0x1050;
	constexpr uint32_t kBlendedVelocity = 0x1060;
	constexpr uint32_t kWarpedVelocity = 0x1070;
	constexpr uint32_t kBlendedYaw = 0x1080;
	constexpr uint32_t kWarpedYaw = 0x1084;
	constexpr uint32_t kLayerLod = 0x1088;            // an int per layer
	constexpr uint32_t kLayerWeight = 0x10A8;         // a float per layer
	constexpr uint32_t kSampling = 0x10EC;
	constexpr uint32_t kSampledThisFrame = 0x10F0;
	constexpr uint32_t kInMainView = 0x10F1;

	// ---- A pose: its rig, its bones relative to their parents ("local") and to the character ("model"), and whether each
	// set is up to date. A bone is 48 bytes: translation, rotation (x, y, z, w), scale.

	constexpr uint32_t kPoseRig = 0x00;
	constexpr uint32_t kPoseLocal = 0x04;
	constexpr uint32_t kPoseLocalCount = 0x08;
	constexpr uint32_t kPoseModel = 0x10;
	constexpr uint32_t kPoseModelCount = 0x14;
	constexpr uint32_t kPoseModelInSync = 0x28;
	constexpr uint32_t kPoseLocalInSync = 0x29;
	constexpr uint32_t kBoneSize = 48;
	constexpr uint32_t kBoneRotation = 0x10;

	// A rig: its bones' parents (shorts) and how many bones
	constexpr uint32_t kRigParents = 0x0C;
	constexpr uint32_t kRigParentCount = 0x10;
	constexpr uint32_t kRigBoneCount = 0x1C;

	// The skeleton definition: its name (a hash), and its two skeletons (gameplay, cinematic; each a pointer and a count).
	// A skeleton: its name, the rig, and two maps of bone name hashes to bones (with the rig's name, and without): pairs
	// of {hash, slot}, then each slot's bone, then how many pairs.
	constexpr uint32_t kDefinitionName = 0x10;
	constexpr uint32_t kDefinitionGameplay = 0x1C;
	constexpr uint32_t kDefinitionCinematic = 0x24;
	constexpr uint32_t kSkeletonName = 0x20;
	constexpr uint32_t kSkeletonRig = 0x38;
	constexpr uint32_t kFullNamePairs = 0x50;
	constexpr uint32_t kFullNameBones = 0x54;
	constexpr uint32_t kFullNameCount = 0x58;
	constexpr uint32_t kShortNamePairs = 0x78;
	constexpr uint32_t kShortNameBones = 0x7C;
	constexpr uint32_t kShortNameCount = 0x80;

	// The animation database: the tree set's and the animation set's names (each a pointer, its name hash at +0x10)
	constexpr uint32_t kDatabaseTreeSet = 0x08;
	constexpr uint32_t kDatabaseAnimationSet = 0x10;
	constexpr uint32_t kNameHash = 0x10;

	// The context stack: three names (hash at +0x10, 0x10 apart) and how many hold each
	constexpr uint32_t kContextNames = 0x10;
	constexpr uint32_t kContextCounts = 0x38;

	// The ragdoll
	constexpr uint32_t kRagdollTail = 0xF0;
	constexpr uint32_t kRagdollActive = 0x138;
	constexpr uint32_t kRagdollFinished = 0x139;
	constexpr uint32_t kRagdollFacehugger = 0x140;
	constexpr uint32_t kRagdollDriving = 0x146;

	// A layer's animated skeleton: its clip controllers (a pointer to pointers, and how many)
	constexpr uint32_t kControllers = 0x10;
	constexpr uint32_t kControllerCount = 0x14;

	// A clip controller
	constexpr uint32_t kControllerTime = 0x08;
	constexpr uint32_t kControllerWeight = 0x0C;
	constexpr uint32_t kControllerMasterWeight = 0x3C;
	constexpr uint32_t kControllerSpeed = 0x40;
	constexpr uint32_t kControllerLoops = 0x44;
	constexpr uint32_t kControllerMaxCycles = 0x4C;
	constexpr uint32_t kControllerEase = 0x78;
	constexpr uint32_t kControllerCropStart = 0x7C;
	constexpr uint32_t kControllerCropEnd = 0x80;
	constexpr uint32_t kControllerClip = 0xA0;
	constexpr uint32_t kControllerParameters = 0xB8;  // the parameter block of the tree playing it
	constexpr uint32_t kControllerTreeSpeed = 0xC4;
	constexpr uint32_t kControllerAnimSpeed = 0xC8;
	constexpr uint32_t kControllerPlayCount = 0xCC;
	constexpr uint32_t kControllerStart = 0xD0;
	constexpr uint32_t kControllerEnd = 0xD4;
	constexpr uint32_t kControllerFlags = 0x118;      // bit 1: mirrored

	// A clip: its label (hash at +0x10) and its binding, whose animation holds the clip's length
	constexpr uint32_t kClipLabel = 0x10;
	constexpr uint32_t kClipBinding = 0x18;
	constexpr uint32_t kBindingAnimation = 0x0C;
	constexpr uint32_t kAnimationDuration = 0x0C;

	// A blend node (a tree is one too)
	constexpr uint32_t kNodeLocal = 0x10;
	constexpr uint32_t kNodeGlobal = 0x14;
	constexpr uint32_t kNodeEase = 0x18;
	constexpr uint32_t kNodeChildren = 0x20;          // first, last
	constexpr uint32_t kNodeName = 0x38;
	constexpr uint32_t kNodeType = 0x40;
	constexpr uint32_t kNodeBlendMode = 0x44;
	// A tree
	constexpr uint32_t kTreeParameters = 0x50;
	constexpr uint32_t kTreeLayer = 0x54;
	constexpr uint32_t kTreeEaseTime = 0x58;
	constexpr uint32_t kTreeFlags = 0x60;
	// Past the common part, what each type holds starts here
	constexpr uint32_t kNodeOwn = 0x50;

	// A tree's parameter block: a typed array each for whole numbers, numbers, names (16 bytes, hash at +8), matrices (64
	// bytes) and vectors (16 bytes); how many parameters; their names, types and places in their type's array; the
	// tree's name and set
	constexpr uint32_t kParamWhole = 0x08;
	constexpr uint32_t kParamNumber = 0x0C;
	constexpr uint32_t kParamText = 0x10;
	constexpr uint32_t kParamMatrix = 0x14;
	constexpr uint32_t kParamVector = 0x18;
	constexpr uint32_t kParamCount = 0x64;
	constexpr uint32_t kParamNames = 0x9C;
	constexpr uint32_t kParamTypes = 0xA0;
	constexpr uint32_t kParamPlaces = 0xA4;
	constexpr uint32_t kParamTreeName = 0xD0;
	constexpr uint32_t kParamTreeSet = 0xE0;

	// IK: its solvers (first, last), whether the feet are placed by themselves, and how
	constexpr uint32_t kIkSolvers = 0x08;
	constexpr uint32_t kIkAutoFoot = 0x18;
	constexpr uint32_t kIkFootSolver = 0x6C;
	// A solver
	constexpr uint32_t kSolverLayer = 0x08;
	constexpr uint32_t kSolverTarget = 0x0C;
	constexpr uint32_t kSolverControl = 0x10;
	constexpr uint32_t kSolverType = 0x14;
	constexpr uint32_t kSolverRotationPreservation = 0x18;
	constexpr uint32_t kSolverPosition = 0x50;        // the target matrix's last row (the matrix starts at +0x20)
	constexpr uint32_t kSolverMaxCorrection = 0x60;
	constexpr uint32_t kSolverFullCorrection = 0x64;
	constexpr uint32_t kSolverGain = 0x68;
	constexpr uint32_t kSolverReferenceBone = 0x6C;

	constexpr int kLeafType = 0;
	constexpr int kMaxDepth = 32;
	constexpr int kMaxNodes = 512;
	constexpr int kMaxChildren = 64;
	constexpr int kMaxControllers = 64;
	constexpr int kMaxTreesPerLayer = 32;
	constexpr int kMaxParameters = 128;

	template <typename T>
	T Get(const uint8_t* at)
	{
		return *reinterpret_cast<const T*>(at);
	}

	const uint8_t* Pointer(const uint8_t* at)
	{
		return *reinterpret_cast<const uint8_t* const*>(at);
	}

	void Copy3(float out[3], const uint8_t* at)
	{
		memcpy(out, at, sizeof(float) * 3);
	}

	// ---- Maths ----

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

	// ---- The skeleton ----

	// The pose's bones as they are (translations into positions for now, rotations), the rig's parents, the character's
	// matrix, and the local bones too when the pose relative to the character is not up to date
	struct RawPose
	{
		const uint8_t* rig = nullptr;
		bool modelInSync = false, localInSync = false;
		float local[ANIM_DEBUG::kMaxBones][2][4];   // translation, rotation
	};

	bool ReadPoseRaw(const uint8_t* character, int pose, ANIM_DEBUG::Skeleton& out, RawPose& raw)
	{
		__try
		{
			if (!character || !character[kSetUp])
				return false;
			const int slot = pose == ANIM_DEBUG::kFinalPose ? ANIM_DEBUG::kLayers - 1 : pose;
			if (slot < 0 || slot >= ANIM_DEBUG::kLayers)
				return false;
			const uint8_t* posed = Pointer(character + kPoses + 4 * slot);
			if (!posed)
				return false;
			const uint8_t* rig = Pointer(posed + kPoseRig);
			if (!rig)
				return false;
			const int bones = Get<int>(rig + kRigBoneCount);
			if (bones < 1 || bones > ANIM_DEBUG::kMaxBones || Get<int>(rig + kRigParentCount) != bones || Get<int>(posed + kPoseModelCount) != bones)
				return false;
			const int16_t* parents = reinterpret_cast<const int16_t*>(Pointer(rig + kRigParents));
			const uint8_t* model = Pointer(posed + kPoseModel);
			if (!parents || !model)
				return false;
			raw.rig = rig;
			raw.modelInSync = posed[kPoseModelInSync] != 0;
			raw.localInSync = posed[kPoseLocalInSync] != 0;
			out.bones = bones;
			for (int i = 0; i < bones; i++)
			{
				const int16_t parent = parents[i];
				out.parents[i] = parent >= 0 && parent < i ? parent : -1;
				const uint8_t* bone = model + kBoneSize * i;
				Copy3(out.positions[i], bone);
				memcpy(out.rotations[i], bone + kBoneRotation, sizeof(float) * 4);
			}
			if (!raw.modelInSync && raw.localInSync && Get<int>(posed + kPoseLocalCount) == bones)
			{
				const uint8_t* local = Pointer(posed + kPoseLocal);
				if (local)
					for (int i = 0; i < bones; i++)
					{
						memcpy(raw.local[i][0], local + kBoneSize * i, sizeof(float) * 4);
						memcpy(raw.local[i][1], local + kBoneSize * i + kBoneRotation, sizeof(float) * 4);
					}
				else
					raw.localInSync = false;
			}
			memcpy(out.world, character + kWorld, sizeof(out.world));
			out.sampled = character[kSampledThisFrame] != 0;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// The bones' name hashes, from whichever of the character's skeletons has the pose's rig
	bool ReadBoneNames(const uint8_t* character, const uint8_t* rig, ANIM_DEBUG::Skeleton& out)
	{
		__try
		{
			const uint8_t* definition = Pointer(character + kSkeletonDefinition);
			if (!definition)
				return false;
			const uint8_t* skeleton = nullptr;
			for (uint32_t at : { kDefinitionCinematic, kDefinitionGameplay })
			{
				const uint8_t* candidate = Pointer(definition + at);
				if (candidate && Pointer(candidate + kSkeletonRig) == rig)
				{
					skeleton = candidate;
					break;
				}
			}
			if (!skeleton)
				return false;
			out.rig = Get<uint32_t>(skeleton + kSkeletonName);
			memset(out.shortNames, 0, sizeof(out.shortNames));
			memset(out.fullNames, 0, sizeof(out.fullNames));
			const struct { uint32_t pairs, bones, count; uint32_t* into; } maps[] = {
				{ kShortNamePairs, kShortNameBones, kShortNameCount, out.shortNames },
				{ kFullNamePairs, kFullNameBones, kFullNameCount, out.fullNames },
			};
			for (const auto& map : maps)
			{
				const uint32_t* pairs = reinterpret_cast<const uint32_t*>(Pointer(skeleton + map.pairs));
				const int32_t* bones = reinterpret_cast<const int32_t*>(Pointer(skeleton + map.bones));
				const uint32_t count = Get<uint32_t>(skeleton + map.count);
				if (!pairs || !bones || count > 4096)
					continue;
				// A slot's bone is looked up in the second array (it is not the slot itself)
				for (uint32_t i = 0; i < count; i++)
				{
					const uint32_t hash = pairs[i * 2], slot = pairs[i * 2 + 1];
					if (!hash || slot >= count)
						continue;
					const int32_t bone = bones[slot];
					if (bone >= 0 && bone < out.bones)
						map.into[bone] = hash;
				}
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// ---- The extras ----

	bool ReadExtrasRaw(const uint8_t* character, ANIM_DEBUG::Extras& out)
	{
		__try
		{
			if (!character || !character[kSetUp])
				return false;
			for (int i = 0; i < ANIM_DEBUG::kHelpers; i++)
				Copy3(out.helpers[i], character + kHelperMatrices + kHelperStride * i + 0x30);
			out.headBone = Get<int16_t>(character + kHeadBone);
			Copy3(out.desired, character + kDesiredVelocity);
			Copy3(out.blended, character + kBlendedVelocity);
			Copy3(out.warped, character + kWarpedVelocity);
			out.desiredYaw = Get<float>(character + kDesiredYaw);
			out.blendedYaw = Get<float>(character + kBlendedYaw);
			out.warpedYaw = Get<float>(character + kWarpedYaw);
			out.linearWarping = Get<int>(character + kLinearWarping);
			out.angularWarping = Get<int>(character + kAngularWarping);

			out.ikCount = 0;
			const uint8_t* ik = Pointer(character + kIk);
			if (ik)
			{
				out.autoFootIk = ik[kIkAutoFoot] != 0;
				out.footSolver = Get<int>(ik + kIkFootSolver);
				const uint8_t* const* first = reinterpret_cast<const uint8_t* const*>(Pointer(ik + kIkSolvers));
				const uint8_t* const* last = reinterpret_cast<const uint8_t* const*>(Pointer(ik + kIkSolvers + 4));
				for (const uint8_t* const* at = first; at && at < last && out.ikCount < ANIM_DEBUG::kMaxIk; at++)
				{
					const uint8_t* solver = *at;
					if (!solver)
						continue;
					ANIM_DEBUG::IkTarget& target = out.ik[out.ikCount++];
					target.layer = Get<int>(solver + kSolverLayer);
					target.target = Get<int>(solver + kSolverTarget);
					target.control = Get<int>(solver + kSolverControl);
					target.type = Get<int>(solver + kSolverType);
					target.rotationPreservation = Get<float>(solver + kSolverRotationPreservation);
					Copy3(target.position, solver + kSolverPosition);
					target.maxCorrection = Get<float>(solver + kSolverMaxCorrection);
					target.fullCorrectionDistance = Get<float>(solver + kSolverFullCorrection);
					target.gain = Get<float>(solver + kSolverGain);
					target.referenceBone = Get<int16_t>(solver + kSolverReferenceBone);
				}
			}

			// The label: the full-body layer's heaviest tree and heaviest clip
			out.labelTree = out.labelClip = 0;
			out.labelTreeWeight = out.labelClipWeight = -1.0f;
			const uint8_t* lists = Pointer(character + kTreeLists);
			if (lists)
			{
				const uint8_t* const* first = reinterpret_cast<const uint8_t* const*>(Pointer(lists));
				const uint8_t* const* last = reinterpret_cast<const uint8_t* const*>(Pointer(lists + 4));
				int n = 0;
				for (const uint8_t* const* at = first; at && at < last && n < kMaxTreesPerLayer; at++, n++)
					if (*at && Get<float>(*at + kNodeGlobal) > out.labelTreeWeight)
					{
						out.labelTreeWeight = Get<float>(*at + kNodeGlobal);
						out.labelTree = Get<uint32_t>(*at + kNodeName);
					}
			}
			const uint8_t* animated = Pointer(character + kAnimatedSkeletons);
			if (animated)
			{
				const uint8_t* const* controllers = reinterpret_cast<const uint8_t* const*>(Pointer(animated + kControllers));
				const int count = Get<int>(animated + kControllerCount);
				for (int i = 0; controllers && i < count && i < kMaxControllers; i++)
				{
					const uint8_t* controller = controllers[i];
					if (!controller || Get<float>(controller + kControllerWeight) <= out.labelClipWeight)
						continue;
					const uint8_t* clip = Pointer(controller + kControllerClip);
					if (!clip)
						continue;
					out.labelClipWeight = Get<float>(controller + kControllerWeight);
					out.labelClip = Get<uint32_t>(clip + kClipLabel);
					out.labelTime = Get<float>(controller + kControllerTime);
					const uint8_t* binding = Pointer(clip + kClipBinding);
					const uint8_t* animation = binding ? Pointer(binding + kBindingAnimation) : nullptr;
					out.labelDuration = animation ? Get<float>(animation + kAnimationDuration) : 0.0f;
				}
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// ---- The detail ----

	bool ReadHeaderRaw(const uint8_t* character, ANIM_DEBUG::Header& out, ANIM_DEBUG::Layer layers[ANIM_DEBUG::kLayers])
	{
		__try
		{
			if (!character || !character[kSetUp])
				return false;
			const uint8_t* definition = Pointer(character + kSkeletonDefinition);
			if (definition)
			{
				out.skeleton = Get<uint32_t>(definition + kDefinitionName);
				const struct { uint32_t at; ANIM_DEBUG::Rig* into; } rigs[] = { { kDefinitionGameplay, &out.gameplay }, { kDefinitionCinematic, &out.cinematic } };
				for (const auto& rig : rigs)
				{
					const uint8_t* skeleton = Pointer(definition + rig.at);
					if (!skeleton)
						continue;
					rig.into->name = Get<uint32_t>(skeleton + kSkeletonName);
					const uint8_t* bones = Pointer(skeleton + kSkeletonRig);
					rig.into->bones = bones ? Get<int>(bones + kRigBoneCount) : 0;
				}
			}
			const uint8_t* database = Pointer(character + kAnimationDatabase);
			if (database)
			{
				const uint8_t* treeSet = Pointer(database + kDatabaseTreeSet);
				const uint8_t* animationSet = Pointer(database + kDatabaseAnimationSet);
				out.treeSet = treeSet ? Get<uint32_t>(treeSet + kNameHash) : 0;
				out.animationSet = animationSet ? Get<uint32_t>(animationSet + kNameHash) : 0;
			}
			const uint8_t* contexts = Pointer(character + kContexts);
			if (contexts)
				for (int i = 0; i < 3; i++)
				{
					out.contexts[i] = Get<uint32_t>(contexts + kContextNames + 0x10 * i);
					out.contextCounts[i] = Get<int>(contexts + kContextCounts + 4 * i);
				}
			out.flags = character[kFlags];
			out.custom = character[kCustom] != 0;
			out.sampling = Get<int>(character + kSampling);
			out.sampledThisFrame = character[kSampledThisFrame] != 0;
			out.inMainView = character[kInMainView] != 0;
			const uint8_t* ragdoll = Pointer(character + kRagdoll);
			out.haveRagdoll = ragdoll != nullptr;
			if (ragdoll)
			{
				out.ragdollActive = ragdoll[kRagdollActive] != 0;
				out.ragdollFinished = ragdoll[kRagdollFinished] != 0;
				out.ragdollDriving = ragdoll[kRagdollDriving] != 0;
				out.facehugger = ragdoll[kRagdollFacehugger] != 0;
				out.tailState = Get<int>(ragdoll + kRagdollTail);
			}
			for (int l = 0; l < ANIM_DEBUG::kLayers; l++)
			{
				const uint8_t* animated = Pointer(character + kAnimatedSkeletons + 4 * l);
				layers[l].present = animated != nullptr;
				layers[l].lod = Get<int>(character + kLayerLod + 4 * l);
				layers[l].weight = Get<float>(character + kLayerWeight + 4 * l);
				layers[l].clips = animated ? Get<int>(animated + kControllerCount) : 0;
			}
			out.read = true;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool ReadMotionRaw(const uint8_t* character, ANIM_DEBUG::Motion& motion, ANIM_DEBUG::Locator& locator)
	{
		__try
		{
			for (int i = 0; i < 3; i++)
			{
				Copy3(motion.movement[i], character + kMovement + 0x10 * i);
				motion.orientation[i] = Get<float>(character + kOrientation + 4 * i);
			}
			motion.read = true;
			locator.usage = Get<int>(character + kLocatorUsage);
			locator.component = Get<int>(character + kLocatorComponent);
			locator.convergence = Get<int>(character + kLocatorConvergence);
			locator.orientationModel = Get<int>(character + kLocatorOrientationModel);
			locator.weight = Get<float>(character + kLocatorWeight);
			locator.orientationWeight = Get<float>(character + kLocatorOrientationWeight);
			for (int i = 0; i < 3; i++)
				Copy3(locator.axes[i], character + kLocator + 0x10 * i);
			Copy3(locator.position, character + kLocator + 0x30);
			locator.read = true;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// A layer's tree list (pointers copied out)
	int ReadTreeList(const uint8_t* character, int layer, const uint8_t* trees[kMaxTreesPerLayer], bool& cut)
	{
		__try
		{
			const uint8_t* lists = Pointer(character + kTreeLists);
			if (!lists)
				return 0;
			const uint8_t* const* first = reinterpret_cast<const uint8_t* const*>(Pointer(lists + kTreeListStride * layer));
			const uint8_t* const* last = reinterpret_cast<const uint8_t* const*>(Pointer(lists + kTreeListStride * layer + 4));
			int count = 0;
			for (const uint8_t* const* at = first; at && at < last; at++)
			{
				if (count == kMaxTreesPerLayer)
				{
					cut = true;
					break;
				}
				if (*at)
					trees[count++] = *at;
			}
			return count;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	float FloatAt(const uint8_t* pointerAt)
	{
		const float* value = reinterpret_cast<const float*>(Pointer(pointerAt));
		return value ? *value : 0.0f;
	}

	// One node (its own fields and its children's pointers). The active child is handed back as a pointer.
	bool ReadNodeRaw(const uint8_t* node, ANIM_DEBUG::Node& out, const uint8_t* children[kMaxChildren], int& childCount, const uint8_t*& active)
	{
		__try
		{
			out.name = Get<uint32_t>(node + kNodeName);
			out.type = Get<int>(node + kNodeType);
			out.local = Get<float>(node + kNodeLocal);
			out.global = Get<float>(node + kNodeGlobal);
			out.ease = Get<float>(node + kNodeEase);
			out.blendMode = Get<int>(node + kNodeBlendMode);
			const uint8_t* const* first = reinterpret_cast<const uint8_t* const*>(Pointer(node + kNodeChildren));
			const uint8_t* const* last = reinterpret_cast<const uint8_t* const*>(Pointer(node + kNodeChildren + 4));
			childCount = 0;
			out.children = first && last > first ? static_cast<int>(last - first) : 0;
			for (const uint8_t* const* at = first; at && at < last && childCount < kMaxChildren; at++)
				children[childCount++] = *at;
			out.cut = out.children > childCount;

			active = nullptr;
			const uint8_t* own = node + kNodeOwn;
			switch (out.type)
			{
			case kLeafType:
			{
				const uint8_t* controller = Pointer(own);
				out.requested = Get<uint32_t>(node + 0x60);
				out.inUse = (node[0x80] & 0x02) != 0;
				out.mirrored = (node[0x80] & 0x01) != 0;
				if (controller)
				{
					out.haveController = true;
					out.time = Get<float>(controller + kControllerTime);
					out.speed = Get<float>(controller + kControllerSpeed);
					out.clipWeight = Get<float>(controller + kControllerWeight);
					const uint8_t* clip = Pointer(controller + kControllerClip);
					if (clip)
					{
						out.clip = Get<uint32_t>(clip + kClipLabel);
						const uint8_t* binding = Pointer(clip + kClipBinding);
						const uint8_t* animation = binding ? Pointer(binding + kBindingAnimation) : nullptr;
						out.duration = animation ? Get<float>(animation + kAnimationDuration) : 0.0f;
					}
				}
				break;
			}
			case 1: // randomised leaf
				active = Pointer(own);
				out.loopsLeft = Get<int>(node + 0x64);
				break;
			case 2: // selector: a whole-number parameter
			{
				const uint32_t* value = reinterpret_cast<const uint32_t*>(Pointer(own));
				out.values[0] = value ? static_cast<float>(*value) : 0.0f;
				out.count = value ? 1 : 0;
				active = Pointer(node + 0x54);
				break;
			}
			case 12: // enumerated selector: a name parameter
			{
				const uint8_t* value = Pointer(own);
				out.valueHash = value ? Get<uint32_t>(value + 8) : 0;
				active = Pointer(node + 0x54);
				break;
			}
			case 11: // ranged selector
				out.values[0] = FloatAt(own);
				out.count = 1;
				out.minimum = Get<float>(node + 0x74);
				out.maximum = Get<float>(node + 0x78);
				active = Pointer(node + 0x54);
				break;
			case 13: // foot-sync selector
				active = Pointer(own);
				break;
			case 3:  // parametric
			case 17: // weighted
				out.values[0] = FloatAt(own);
				out.count = 1;
				out.minimum = Get<float>(node + 0x54);
				out.maximum = Get<float>(node + 0x58);
				break;
			case 4: // blend set: up to three parameters, and the set's name
			{
				for (int i = 0; i < 3; i++)
					if (Pointer(own + 4 * i))
						out.values[out.count++] = FloatAt(own + 4 * i);
				const uint8_t* set = Pointer(node + 0x5C);
				out.valueHash = set ? Get<uint32_t>(set + kNameHash) : 0;
				break;
			}
			case 5: // four-parameter blend set
				for (int i = 0; i < 4; i++)
					out.values[out.count++] = FloatAt(own + 4 * i);
				break;
			case 7: // bilinear
			case 8: // low-detail bilinear
				out.values[0] = FloatAt(own);
				out.values[1] = FloatAt(own + 4);
				out.count = 2;
				out.minimum = Get<float>(node + 0x58);
				out.maximum = Get<float>(node + 0x5C);
				break;
			case 9:  // additive: its weight
				out.values[0] = Get<float>(node + 0x58);
				out.count = 1;
				break;
			case 10: // parametric additive: its weight and its parameter
				out.values[0] = Get<float>(node + 0x58);
				out.values[1] = FloatAt(node + 0x64);
				out.count = 2;
				break;
			case 14: // bone mask
				out.mask = Get<uint32_t>(own);
				break;
			case 16: // spherical: a vector
			{
				const float* vector = reinterpret_cast<const float*>(Pointer(own));
				if (vector)
				{
					memcpy(out.values, vector, sizeof(float) * 3);
					out.count = 3;
				}
				break;
			}
			default:
				break;
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool ReadTreeFields(const uint8_t* tree, ANIM_DEBUG::Tree& out, const uint8_t*& parameters)
	{
		__try
		{
			out.name = Get<uint32_t>(tree + kNodeName);
			out.local = Get<float>(tree + kNodeLocal);
			out.global = Get<float>(tree + kNodeGlobal);
			out.ease = Get<float>(tree + kNodeEase);
			out.layer = Get<int>(tree + kTreeLayer);
			out.easeTime = Get<float>(tree + kTreeEaseTime);
			out.flags = tree[kTreeFlags];
			parameters = Pointer(tree + kTreeParameters);
			out.treeSet = parameters ? Get<uint32_t>(parameters + kParamTreeSet) : 0;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	int ReadParameters(const uint8_t* block, ANIM_DEBUG::Parameter out[kMaxParameters])
	{
		__try
		{
			const uint32_t count = Get<uint32_t>(block + kParamCount);
			const uint32_t* names = reinterpret_cast<const uint32_t*>(Pointer(block + kParamNames));
			const int32_t* types = reinterpret_cast<const int32_t*>(Pointer(block + kParamTypes));
			const uint32_t* places = reinterpret_cast<const uint32_t*>(Pointer(block + kParamPlaces));
			if (!names || !types || !places || count > 1024)
				return 0;
			int read = 0;
			for (uint32_t i = 0; i < count && read < kMaxParameters; i++)
			{
				ANIM_DEBUG::Parameter& parameter = out[read++];
				parameter = ANIM_DEBUG::Parameter();
				parameter.name = names[i];
				parameter.type = types[i];
				const uint32_t place = places[i];
				switch (parameter.type)
				{
				case 0: { const uint32_t* values = reinterpret_cast<const uint32_t*>(Pointer(block + kParamWhole)); if (values) parameter.whole = values[place]; break; }
				case 1: { const float* values = reinterpret_cast<const float*>(Pointer(block + kParamNumber)); if (values) parameter.number = values[place]; break; }
				case 2: { const uint8_t* values = Pointer(block + kParamText); if (values) parameter.text = Get<uint32_t>(values + 16 * place + 8); break; }
				case 3: { const uint8_t* values = Pointer(block + kParamMatrix); if (values) Copy3(parameter.vector, values + 64 * place + 0x30); break; }
				case 4: { const uint8_t* values = Pointer(block + kParamVector); if (values) Copy3(parameter.vector, values + 16 * place); break; }
				default: break;
				}
			}
			return read;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	int ReadClips(const uint8_t* character, int layer, ANIM_DEBUG::Clip out[kMaxControllers])
	{
		__try
		{
			const uint8_t* animated = Pointer(character + kAnimatedSkeletons + 4 * layer);
			if (!animated)
				return 0;
			const uint8_t* const* controllers = reinterpret_cast<const uint8_t* const*>(Pointer(animated + kControllers));
			const int count = Get<int>(animated + kControllerCount);
			int read = 0;
			for (int i = 0; controllers && i < count && i < kMaxControllers; i++)
			{
				const uint8_t* controller = controllers[i];
				if (!controller)
					continue;
				ANIM_DEBUG::Clip& clip = out[read++];
				clip = ANIM_DEBUG::Clip();
				clip.layer = layer;
				clip.time = Get<float>(controller + kControllerTime);
				clip.weight = Get<float>(controller + kControllerWeight);
				clip.masterWeight = Get<float>(controller + kControllerMasterWeight);
				clip.speed = Get<float>(controller + kControllerSpeed);
				clip.loops = Get<int>(controller + kControllerLoops);
				clip.maxCycles = Get<int>(controller + kControllerMaxCycles);
				clip.easeState = Get<int>(controller + kControllerEase);
				clip.cropStart = Get<float>(controller + kControllerCropStart);
				clip.cropEnd = Get<float>(controller + kControllerCropEnd);
				clip.treeSpeed = Get<float>(controller + kControllerTreeSpeed);
				clip.animSpeed = Get<float>(controller + kControllerAnimSpeed);
				clip.playCount = Get<int>(controller + kControllerPlayCount);
				clip.startTime = Get<float>(controller + kControllerStart);
				clip.endTime = Get<float>(controller + kControllerEnd);
				clip.mirrored = (controller[kControllerFlags] & 0x02) != 0;
				const uint8_t* parameters = Pointer(controller + kControllerParameters);
				clip.tree = parameters ? Get<uint32_t>(parameters + kParamTreeName) : 0;
				const uint8_t* animationClip = Pointer(controller + kControllerClip);
				if (animationClip)
				{
					clip.clip = Get<uint32_t>(animationClip + kClipLabel);
					const uint8_t* binding = Pointer(animationClip + kClipBinding);
					const uint8_t* animation = binding ? Pointer(binding + kBindingAnimation) : nullptr;
					clip.duration = animation ? Get<float>(animation + kAnimationDuration) : 0.0f;
				}
			}
			return read;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	// A tree, each node before its children, down to the caps
	void ReadTree(const uint8_t* root, int layer, ANIM_DEBUG::Tree& tree)
	{
		const uint8_t* parameters = nullptr;
		if (!ReadTreeFields(root, tree, parameters))
			return;
		tree.layer = layer;
		if (parameters)
		{
			static ANIM_DEBUG::Parameter read[kMaxParameters];
			const int count = ReadParameters(parameters, read);
			tree.parameters.assign(read, read + count);
		}
		struct Pending { const uint8_t* node; int depth, index; };
		std::vector<Pending> stack;
		stack.push_back({ root, 0, 0 });
		const uint8_t* children[kMaxChildren];
		while (!stack.empty())
		{
			const Pending next = stack.back();
			stack.pop_back();
			if (static_cast<int>(tree.nodes.size()) >= kMaxNodes)
			{
				tree.cut = true;
				break;
			}
			ANIM_DEBUG::Node node;
			int childCount = 0;
			const uint8_t* active = nullptr;
			if (!ReadNodeRaw(next.node, node, children, childCount, active))
				continue;
			node.depth = next.depth;
			node.index = next.index;
			for (int i = 0; active && i < childCount; i++)
				if (children[i] == active)
					node.activeChild = i;
			if (next.depth + 1 >= kMaxDepth && childCount > 0)
			{
				node.cut = true;
				childCount = 0;
			}
			tree.nodes.push_back(node);
			// Children pushed last first, so they come out in order
			for (int i = childCount - 1; i >= 0; i--)
				if (children[i])
					stack.push_back({ children[i], next.depth + 1, i });
		}
	}

	const char* const kLayerNames[] = { "Full body", "General additive", "Breathing", "Look at", "Gun / tail", "Hand", "Face", "Final" };
	const char* const kHelperNames[] = { "head", "hips", "root", "left hand", "right hand", "left foot", "right foot" };
	const char* const kNodeTypes[] = { "Leaf", "Randomised leaf", "Selector", "Parametric", "Blend set", "4D blend set", "Sequence", "Bilinear",
		"Low-detail bilinear", "Additive", "Parametric additive", "Ranged selector", "Enumerated selector", "Foot-sync selector", "Bone mask", "IK",
		"Spherical", "Weighted", "Tree" };
	const char* const kEaseStates[] = { "easing in", "in", "easing out", "out" };
	const char* const kIkTargets[] = { "left foot", "right foot", "left hand", "right hand", "left foot reference", "right foot reference",
		"left hand reference", "right hand reference", "head", "eyes", "spine", "tail", "left weapon bone", "right weapon bone", "hips" };
	const char* const kIkTypes[] = { "analytical", "look at", "chain (CCD)", "single bone" };
	const char* const kIkControls[] = { "reposition", "planar push only", "root-assisted push only", "root-assisted pull only", "root-assisted push and pull" };
	const char* const kContextLabels[] = { "Stance", "Mood", "Equipment" };
	const char* const kWarping[] = { "on (absolute)", "on (with metadata)", "off" };
	const char* const kLocatorUsages[] = { "not in use", "in use", "clamped to its line" };
	const char* const kSamplingLabels[] = { "every frame", "10 times a second", "15 times a second", "20 times a second" };
	const char* const kTailStates[] = { "normal", "still", "shot", "alert" };
	const char* const kFlagNames[] = { "IK on", "foot IK off from outside", "secondary layers on", "cinematic", "cinematic only", "ragdoll on",
		"male IK points", "sampling on" };

	template <size_t N>
	const char* Pick(const char* const (&names)[N], int index)
	{
		return index >= 0 && index < static_cast<int>(N) ? names[index] : "?";
	}
}

const char* ANIM_DEBUG::LayerName(int layer) { return Pick(kLayerNames, layer); }
const char* ANIM_DEBUG::HelperName(int helper) { return Pick(kHelperNames, helper); }
const char* ANIM_DEBUG::NodeTypeName(int type) { return Pick(kNodeTypes, type); }
const char* ANIM_DEBUG::EaseStateName(int state) { return Pick(kEaseStates, state); }
const char* ANIM_DEBUG::IkTargetName(int target) { return Pick(kIkTargets, target); }
const char* ANIM_DEBUG::IkSolverTypeName(int type) { return Pick(kIkTypes, type); }
const char* ANIM_DEBUG::IkControlName(int control) { return Pick(kIkControls, control); }
const char* ANIM_DEBUG::ContextName(int slot) { return Pick(kContextLabels, slot); }
const char* ANIM_DEBUG::WarpingUsageName(int usage) { return Pick(kWarping, usage); }
const char* ANIM_DEBUG::LocatorUsageName(int usage) { return Pick(kLocatorUsages, usage); }
const char* ANIM_DEBUG::SamplingName(int frequency) { return Pick(kSamplingLabels, frequency); }
const char* ANIM_DEBUG::TailStateName(int state) { return Pick(kTailStates, state); }
const char* ANIM_DEBUG::FlagName(int bit) { return Pick(kFlagNames, bit); }

bool ANIM_DEBUG::ReadSkeleton(const uint8_t* character, int pose, bool names, Skeleton& out)
{
	out.read = false;
	out.names = false;
	out.rig = 0;
	static RawPose raw;
	if (!ReadPoseRaw(character, pose, out, raw))
		return false;

	// The bones relative to the character, worked out from the local ones when the game has not (parents come first)
	if (!raw.modelInSync && raw.localInSync)
		for (int i = 0; i < out.bones; i++)
		{
			const int parent = out.parents[i];
			if (parent < 0)
			{
				memcpy(out.positions[i], raw.local[i][0], sizeof(float) * 3);
				memcpy(out.rotations[i], raw.local[i][1], sizeof(float) * 4);
				continue;
			}
			float moved[3];
			Rotate(out.rotations[parent], raw.local[i][0], moved);
			for (int k = 0; k < 3; k++)
				out.positions[i][k] = out.positions[parent][k] + moved[k];
			Multiply(out.rotations[parent], raw.local[i][1], out.rotations[i]);
		}

	// Into the world, through the character's matrix
	const float* m = out.world;
	for (int i = 0; i < out.bones; i++)
	{
		const float x = out.positions[i][0], y = out.positions[i][1], z = out.positions[i][2];
		for (int k = 0; k < 3; k++)
			out.positions[i][k] = x * m[k] + y * m[4 + k] + z * m[8 + k] + m[12 + k];
	}
	for (int i = 0; i < out.bones; i++)
		if (!std::isfinite(out.positions[i][0]) || !std::isfinite(out.positions[i][1]) || !std::isfinite(out.positions[i][2]))
			return false;

	if (names)
		out.names = ReadBoneNames(character, raw.rig, out);
	out.read = true;
	return true;
}

bool ANIM_DEBUG::ReadExtras(const uint8_t* character, Extras& out)
{
	out = Extras();
	out.read = ReadExtrasRaw(character, out);
	return out.read;
}

bool ANIM_DEBUG::ReadDetail(const uint8_t* character, Detail& out)
{
	out = Detail();
	if (!ReadHeaderRaw(character, out.header, out.layers))
		return false;
	const uint8_t* trees[kMaxTreesPerLayer];
	for (int l = 0; l < kLayers; l++)
	{
		const int count = ReadTreeList(character, l, trees, out.treesCut);
		out.layers[l].trees = count;
		for (int i = 0; i < count; i++)
		{
			out.trees.emplace_back();
			ReadTree(trees[i], l, out.trees.back());
		}
		static Clip clips[kMaxControllers];
		const int clipCount = ReadClips(character, l, clips);
		out.clips.insert(out.clips.end(), clips, clips + clipCount);
	}
	ReadMotionRaw(character, out.motion, out.locator);
	ReadExtras(character, out.extras);
	return true;
}

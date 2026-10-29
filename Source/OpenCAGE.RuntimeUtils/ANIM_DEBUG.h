#pragma once

#include "DevTools.h"
#include <cstdint>
#include <vector>

/*
	Animation debugging, in the debug menu (see DEBUG_MENU.h): each character's posed skeleton drawn over the game, and what
	its animation system is doing (the skeleton, bone axes and helper drawing, the trees and clips each layer runs, velocities, the reference locator and IK).

	Every character carries its animation system inside it. That system poses the character in eight layers (the full
	body, additive moves, breathing, looking at things, the gun or the alien's tail, hands, the face, and the final pose the
	renderer skins), each from the animation trees it runs: trees of blend nodes (selectors, blends, additives...) whose
	leaves play clips. Each layer's pose holds every bone as a translation, a rotation and a scale, relative to the
	character (its "model space"), and the character's matrix - the one the renderer places the mesh with - takes that into
	the world. The bones' parents come from the rig the pose is on (a gameplay rig, or a finer cinematic one with a face for
	named characters). All of this is written by the game's animation jobs during the frame and finished before the frame
	ends, so it is read at the frame's presentation, with every read guarded (a level can be unloading; a character's
	system says when it is set up, and clears that first when it closes).

	The game keeps no names here, only name hashes: see ANIM_NAMES.h for how they are named.
*/
namespace ANIM_DEBUG
{
	constexpr int kMaxBones = 512;
	constexpr int kLayers = 8;
	constexpr int kFinalPose = -1;   // the pose the renderer skins (otherwise a layer's own pose, 0 to 6)
	constexpr int kHelpers = 7;      // head, hips, root, left hand, right hand, left foot, right foot
	constexpr int kMaxIk = 16;

	const char* LayerName(int layer);
	const char* HelperName(int helper);
	const char* NodeTypeName(int type);
	const char* EaseStateName(int state);
	const char* IkTargetName(int target);
	const char* IkSolverTypeName(int type);
	const char* IkControlName(int control);
	const char* ContextName(int slot);
	const char* WarpingUsageName(int usage);
	const char* LocatorUsageName(int usage);
	const char* SamplingName(int frequency);
	const char* TailStateName(int state);

	// ---- For the overlay: every character drawn, each frame ----

	struct Skeleton
	{
		bool read = false;
		bool sampled = false;              // posed this frame (far or unseen characters are posed less often, or not at all)
		int bones = 0;
		uint32_t rig = 0;                  // the name hash of the rig the pose is on, when the character's skeleton names it
		bool names = false;                // shortNames and fullNames were read (only when asked for)
		int16_t parents[kMaxBones];        // -1: the rig's root
		float positions[kMaxBones][3];     // where each bone is in the world
		float rotations[kMaxBones][4];     // its rotation relative to the character (x, y, z, w)
		float world[16];                   // the character's matrix: rows are its axes, the last row where it is
		uint32_t shortNames[kMaxBones];    // name hashes ("LEFTFOREARM"), 0 for a bone the rig names none
		uint32_t fullNames[kMaxBones];     // and with the rig's name ("MALE:LEFTFOREARM")
	};

	// One character's pose (kFinalPose or a layer's), read into out. With names, the bones' name hashes too.
	bool ReadSkeleton(const uint8_t* character, int pose, bool names, Skeleton& out);

	struct IkTarget
	{
		int layer = 0, target = 0, control = 0, type = 0, referenceBone = -1;
		float position[3] = {};            // in the world
		float gain = 0.0f, maxCorrection = 0.0f, fullCorrectionDistance = 0.0f, rotationPreservation = 0.0f;
	};

	struct Extras
	{
		bool read = false;
		float helpers[kHelpers][3] = {};   // the helper bones (the final pose's), in the world
		int16_t headBone = -1;
		// Velocity, in the world: what the character's movement asks for, what its animations give, and what they are warped to
		float desired[3] = {}, blended[3] = {}, warped[3] = {};
		float desiredYaw = 0.0f, blendedYaw = 0.0f, warpedYaw = 0.0f; // turning, radians a second
		int linearWarping = 0, angularWarping = 0;
		// IK: whether the feet are placed by themselves, and the solvers asked for
		bool autoFootIk = false;
		int footSolver = 0;
		int ikCount = 0;
		IkTarget ik[kMaxIk];
		// For a label: the full-body layer's heaviest tree, and its heaviest clip and where it is in it
		uint32_t labelTree = 0, labelClip = 0;
		float labelTreeWeight = 0.0f, labelClipWeight = 0.0f, labelTime = 0.0f, labelDuration = 0.0f;
	};

	bool ReadExtras(const uint8_t* character, Extras& out);

	// ---- For the tab: the selected character in detail ----

	struct Rig
	{
		uint32_t name = 0;
		int bones = 0;
	};

	struct Header
	{
		bool read = false;
		uint32_t skeleton = 0;             // the skeleton definition's name
		Rig gameplay, cinematic;
		uint32_t animationSet = 0, treeSet = 0;
		uint32_t contexts[3] = {};         // stance, mood, equipment
		int contextCounts[3] = {};
		uint8_t flags = 0;                 // see FlagName
		bool custom = false;               // made of several models
		int sampling = 0;
		bool sampledThisFrame = false, inMainView = false;
		bool haveRagdoll = false, ragdollActive = false, ragdollFinished = false, ragdollDriving = false, facehugger = false;
		int tailState = 0;
	};
	const char* FlagName(int bit);

	struct Layer
	{
		bool present = false;              // the layer has an animated skeleton (it is used by this character)
		int lod = 0;                       // 0 on, 1 off for level of detail
		float weight = 0.0f;
		int trees = 0, clips = 0;
	};

	struct Node
	{
		int depth = 0;                     // 0: the tree itself
		int index = 0;                     // its place among its parent's children
		uint32_t name = 0;
		int type = -1;
		float local = 0.0f, global = 0.0f, ease = 0.0f;
		int blendMode = 0;
		int children = 0;
		bool cut = false;                  // more children (or depth) than are shown
		// What its type adds (see the tab for which apply)
		int activeChild = -1;
		int count = 0;                     // how many of values hold something
		float values[4] = {};
		uint32_t valueHash = 0;            // an enumerated selector's value, a blend set's name
		float minimum = 0.0f, maximum = 0.0f;
		uint32_t mask = 0;
		int loopsLeft = 0;
		// A leaf: the clip it asked for, and what its controller is playing
		uint32_t requested = 0, clip = 0;
		bool inUse = false, mirrored = false, haveController = false;
		float time = 0.0f, duration = 0.0f, speed = 0.0f, clipWeight = 0.0f;
	};

	struct Parameter
	{
		uint32_t name = 0;
		int type = 0;                      // 0 a whole number, 1 a number, 2 a name, 3 a matrix, 4 a vector
		uint32_t whole = 0;
		float number = 0.0f;
		uint32_t text = 0;
		float vector[3] = {};
	};

	struct Tree
	{
		int layer = 0;
		uint32_t name = 0, treeSet = 0;
		float local = 0.0f, global = 0.0f, ease = 0.0f, easeTime = 0.0f;
		uint8_t flags = 0;
		std::vector<Node> nodes;           // the tree first, then its nodes, each before its children
		bool cut = false;                  // more nodes than are shown
		std::vector<Parameter> parameters;
	};

	struct Clip
	{
		int layer = 0;
		uint32_t clip = 0, tree = 0;
		float time = 0.0f, duration = 0.0f, weight = 0.0f, masterWeight = 0.0f, speed = 0.0f, treeSpeed = 0.0f, animSpeed = 0.0f;
		float cropStart = 0.0f, cropEnd = 0.0f, startTime = 0.0f, endTime = 0.0f;
		int loops = 0, maxCycles = 0, easeState = 0, playCount = 0;
		bool mirrored = false;
	};

	struct Motion
	{
		bool read = false;
		float movement[3][3] = {};         // this frame, still to come, in all
		float orientation[3] = {};         // the same for turning
	};

	struct Locator
	{
		bool read = false;
		int usage = 0, component = 0, convergence = 0, orientationModel = 0;
		float weight = 0.0f, orientationWeight = 0.0f;
		float position[3] = {};
		float axes[3][3] = {};
	};

	struct Detail
	{
		Header header;
		Layer layers[kLayers];
		std::vector<Tree> trees;
		bool treesCut = false;
		std::vector<Clip> clips;
		Motion motion;
		Locator locator;
		Extras extras;
	};

	// Render thread: the character in detail (false when its animation system is not set up)
	bool ReadDetail(const uint8_t* character, Detail& out);
}

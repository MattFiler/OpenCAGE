#include "WORLD_DEBUG.h"
#include "DEBUG_MENU.h"
#include "LIVE_LINK.h"
#include "SCRIPT_DEBUG.h"

#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstdarg>
#include <cstdio>
#include <cstring>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <DirectXMath.h>

#include <imgui.h>

/*
	Addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).

	The Scripts group's Cameras tab: the game's cameras as its camera handling keeps them - the stack of cameras (the
	player's, scripted fixed cameras, cutscene cameras...) with which one won, each camera's name, where it is, its field
	of view and the script entity that controls it, and its behaviours (shakes, paths, look-ats and the like, with their
	priority and how far they have blended in); the camera handling's own behaviours that apply to every camera; the
	transition between two cameras in progress; the depth of field's focus; the camera animations loaded; and the camera
	paths. The paths, and the cameras themselves, can be drawn over the game (scripts.cameras.paths,
	scripts.cameras.cameras).

	Everything is read at the frame's presentation, after the game has updated its cameras.
*/
namespace
{
	// ---- Where the game keeps it ----

	// The game's globals (a pointer to them): the camera handling (a pointer) at +0x58
	constexpr uintptr_t kGameGlobals = 0x012F0C88;
	constexpr uint32_t kGlobalsCameras = 0x58;
	// The camera handling: every camera made (an array: items, how many, room), every camera path made (the same), the
	// stack of cameras (sixteen pointers in place, then a pointer to them and how many), the behaviours that apply to every
	// camera (thirty-two the same way), the active, player, trigger, transition and deactivating cameras (pointers), the
	// transition in progress, the free camera, the camera animations loaded and the depth of field's focus (pointers),
	// a custom field of view and its ratio, the zone the camera was in when the level was restored, and a paused flag
	constexpr uint32_t kCameraPool = 0x60;
	constexpr uint32_t kPathPool = 0xA0;
	constexpr uint32_t kStack = 0xAC;
	constexpr uint32_t kStackUsed = 0xF0;
	constexpr int kStackSize = 16;
	constexpr uint32_t kGlobalBehaviours = 0x13C;
	constexpr uint32_t kGlobalBehavioursUsed = 0x1C0;
	constexpr int kGlobalBehavioursSize = 32;
	constexpr uint32_t kActiveCamera = 0x1D8;
	constexpr uint32_t kPlayerCamera = 0x1DC;
	constexpr uint32_t kTriggerCamera = 0x1E0;
	constexpr uint32_t kTransitionCamera = 0x1E4;
	constexpr uint32_t kDeactivatingCamera = 0x1E8;
	constexpr uint32_t kActiveTransition = 0x1EC;
	constexpr uint32_t kFreeCamera = 0x1F0;
	constexpr uint32_t kAnimations = 0x1FC;
	constexpr uint32_t kDepthOfField = 0x200;
	constexpr uint32_t kCustomFov = 0x20C;
	constexpr uint32_t kCustomFovRatio = 0x210;
	constexpr uint32_t kRestoredZone = 0x21C;
	constexpr uint32_t kCamerasPaused = 0x226;
	// An array kept as items, how many, room
	constexpr uint32_t kArrayItems = 0x00;
	constexpr uint32_t kArrayUsed = 0x04;

	// A camera: its state (a quaternion, where it is, its vertical field of view in degrees, near and far), its behaviours
	// (sixteen pointers in place, then a pointer to them and how many), its name (32 characters), where it looks at, the
	// script entity that controls it (two ids), its transition in progress, the character it is linked to and its flags
	constexpr uint32_t kCameraState = 0x2C;
	constexpr uint32_t kCameraBehaviours = 0x7C;
	constexpr uint32_t kCameraBehavioursUsed = 0xC0;
	constexpr int kCameraBehavioursSize = 16;
	constexpr uint32_t kCameraName = 0xEC;
	constexpr uint32_t kCameraNameLength = 0x20;
	constexpr uint32_t kCameraFocalPoint = 0x10C;
	constexpr uint32_t kCameraController = 0x130;
	constexpr uint32_t kCameraTransition = 0x140;
	constexpr uint32_t kCameraCharacter = 0x148;
	constexpr uint32_t kCameraFlags = 0x14C; // attached, active, ghost, expired, never expires, enter and exit transitions, focal point, moved by the player, first person, a transition's, peeking
	constexpr int kCameraFlagCount = 12;

	// A behaviour: the camera it belongs to, its name (32 characters), priority, threshold, blend in, blend out and duration
	// (seconds), how far it is blended in and its blend timer, its blend state (0 blending in, 1 in, 2 blending out) and its
	// flags (active, never expires, paused, additive, global, then which parts of the camera it changes)
	constexpr uint32_t kBehaviourCamera = 0x34;
	constexpr uint32_t kBehaviourName = 0x38;
	constexpr uint32_t kBehaviourNameLength = 0x20;
	constexpr uint32_t kBehaviourPriority = 0x5C;
	constexpr uint32_t kBehaviourBlendIn = 0x64;
	constexpr uint32_t kBehaviourBlendOut = 0x68;
	constexpr uint32_t kBehaviourDuration = 0x6C;
	constexpr uint32_t kBehaviourRatio = 0x74;
	constexpr uint32_t kBehaviourTimer = 0x78;
	constexpr uint32_t kBehaviourState = 0x7C;
	constexpr uint32_t kBehaviourFlags = 0x80;
	constexpr int kBehaviourFlagCount = 10;

	// A transition: the camera it leaves and the one it goes to, its duration, ease in and out, its timer and whole
	// duration (pieces and all), and its flags (active, ended, linear, instant)
	constexpr uint32_t kTransitionSource = 0x2C;
	constexpr uint32_t kTransitionTarget = 0x30;
	constexpr uint32_t kTransitionDuration = 0x34;
	constexpr uint32_t kTransitionEaseIn = 0x38;
	constexpr uint32_t kTransitionEaseOut = 0x3C;
	constexpr uint32_t kTransitionTimer = 0x70;
	constexpr uint32_t kTransitionWhole = 0x74;
	constexpr uint32_t kTransitionFlags = 0x78;

	// A camera path: its placing (twelve numbers), its spline (a pointer: its points are an offset in four-byte words into
	// the pack file at +0x30 and how many at +0x34, six numbers a point), whether it is straight lines or curved, what it is
	// for, its duration and length, and its flags (relative to what follows it, a loop, closed, valid)
	constexpr uint32_t kPathMatrix = 0x00;
	constexpr uint32_t kPathSpline = 0x30;
	constexpr uint32_t kPathType = 0x38;
	constexpr uint32_t kPathClass = 0x3C;
	constexpr uint32_t kPathDuration = 0x44;
	constexpr uint32_t kPathLength = 0x48;
	constexpr uint32_t kPathFlags = 0x4C;
	constexpr uint32_t kSplinePoints = 0x30;
	constexpr uint32_t kSplinePointFloats = 6;
	constexpr int kMaxPathPoints = 512;
	constexpr uintptr_t kPackFile = 0x0134EF44;

	// The depth of field's focus: focal length, focal plane and f-number, where it focuses, the character it follows, and
	// its flags (focus on the point, on the camera's target, on the character; active)
	constexpr uint32_t kDofFocal = 0x00;
	constexpr uint32_t kDofFocusPoint = 0x30;
	constexpr uint32_t kDofCharacter = 0x48;
	constexpr uint32_t kDofFlags = 0x50;
	// The camera animations loaded: an array of records, whose file name (64 characters) is at +0x18
	constexpr uint32_t kAnimationFile = 0x18;
	constexpr uint32_t kAnimationFileLength = 0x40;

	constexpr int kMaxCameras = 256;
	constexpr int kMaxPaths = 256;

	std::atomic<bool> g_pathOverlay = false;
	std::atomic<bool> g_cameraOverlay = false;
	std::atomic<bool> g_pathLabels = true;

	// ---- Guarded reads ----

	bool Read(const void* at, void* out, size_t size)
	{
		__try
		{
			memcpy(out, at, size);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	template <typename T> T Get(const void* base, uint32_t offset, T fallback = T())
	{
		T value;
		if (!base || !Read(static_cast<const uint8_t*>(base) + offset, &value, sizeof(value)))
			return fallback;
		return value;
	}

	std::string Text(const void* base, uint32_t offset, uint32_t length)
	{
		char text[128] = {};
		const uint32_t n = (std::min)(length, static_cast<uint32_t>(sizeof(text) - 1));
		if (!base || !Read(static_cast<const uint8_t*>(base) + offset, text, n))
			return "";
		text[n] = '\0';
		return text;
	}

	bool LevelUp()
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

	std::string Format(const char* format, ...)
	{
		char text[512];
		va_list args;
		va_start(args, format);
		vsnprintf(text, sizeof(text), format, args);
		va_end(args);
		return text;
	}

	const uint8_t* CameraManager()
	{
		const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kGameGlobals);
		const uint8_t* globals = at ? Get<const uint8_t*>(reinterpret_cast<const void*>(at), 0, nullptr) : nullptr;
		return globals ? Get<const uint8_t*>(globals, kGlobalsCameras, nullptr) : nullptr;
	}

	// ---- What is read ----

	struct Behaviour
	{
		const void* object = nullptr;
		std::string name;
		int priority = 0;
		float blendIn = 0.0f, blendOut = 0.0f, duration = 0.0f, ratio = 0.0f, timer = 0.0f;
		int state = 0;
		uint8_t flags[kBehaviourFlagCount] = {};
	};

	struct Camera
	{
		const void* object = nullptr;
		std::string name;
		float rotation[4] = {};
		float position[3] = {};
		float fov = 0.0f, nearPlane = 0.0f, farPlane = 0.0f;
		float focalPoint[3] = {};
		uint32_t controller[2] = {};
		const void* transition = nullptr;
		const void* character = nullptr;
		uint8_t flags[kCameraFlagCount] = {};
		std::vector<Behaviour> behaviours;
	};

	struct Path
	{
		const void* object = nullptr;
		float matrix[12] = {};
		int type = 0, pathClass = 0;
		float duration = 0.0f, length = 0.0f;
		bool local = false, loop = false, closed = false, valid = false;
		std::vector<float> points; // world positions (or local ones for a local path), three numbers each
	};

	struct Transition
	{
		bool have = false;
		const void* source = nullptr;
		const void* target = nullptr;
		float duration = 0.0f, easeIn = 0.0f, easeOut = 0.0f, timer = 0.0f, whole = 0.0f;
		uint8_t flags[4] = {};
	};

	struct Reading
	{
		bool read = false;
		std::vector<const void*> stack;
		std::vector<Camera> cameras; // every camera made (the stack's first)
		std::vector<Behaviour> global;
		const void* active = nullptr, * player = nullptr, * trigger = nullptr, * transitionCamera = nullptr, * deactivating = nullptr, * freeCamera = nullptr;
		Transition transition;
		std::vector<Path> paths;
		bool haveDof = false;
		float focal[3] = {};
		float focusPoint[3] = {};
		const void* focusCharacter = nullptr;
		uint8_t dofFlags[4] = {};
		std::vector<std::string> animations;
		float customFov = 0.0f, customFovRatio = 0.0f;
		uint32_t restoredZone = 0;
		bool paused = false;
	};

	Behaviour ReadBehaviour(const uint8_t* object)
	{
		Behaviour behaviour;
		behaviour.object = object;
		behaviour.name = Text(object, kBehaviourName, kBehaviourNameLength);
		behaviour.priority = Get<int>(object, kBehaviourPriority, 0);
		behaviour.blendIn = Get<float>(object, kBehaviourBlendIn, 0.0f);
		behaviour.blendOut = Get<float>(object, kBehaviourBlendOut, 0.0f);
		behaviour.duration = Get<float>(object, kBehaviourDuration, 0.0f);
		behaviour.ratio = Get<float>(object, kBehaviourRatio, 0.0f);
		behaviour.timer = Get<float>(object, kBehaviourTimer, 0.0f);
		behaviour.state = Get<int>(object, kBehaviourState, 0);
		Read(object + kBehaviourFlags, behaviour.flags, sizeof(behaviour.flags));
		return behaviour;
	}

	Camera ReadCamera(const uint8_t* object)
	{
		Camera camera;
		camera.object = object;
		camera.name = Text(object, kCameraName, kCameraNameLength);
		float state[10] = {};
		if (Read(object + kCameraState, state, sizeof(state)))
		{
			memcpy(camera.rotation, state, sizeof(camera.rotation));
			memcpy(camera.position, state + 4, sizeof(camera.position));
			camera.fov = state[7];
			camera.nearPlane = state[8];
			camera.farPlane = state[9];
		}
		Read(object + kCameraFocalPoint, camera.focalPoint, sizeof(camera.focalPoint));
		Read(object + kCameraController, camera.controller, sizeof(camera.controller));
		camera.transition = Get<const void*>(object, kCameraTransition, nullptr);
		camera.character = Get<const void*>(object, kCameraCharacter, nullptr);
		Read(object + kCameraFlags, camera.flags, sizeof(camera.flags));
		const int used = (std::min)(static_cast<int>(Get<uint32_t>(object, kCameraBehavioursUsed, 0)), kCameraBehavioursSize);
		for (int i = 0; i < used; i++)
			if (const uint8_t* behaviour = Get<const uint8_t*>(object, kCameraBehaviours + i * 4, nullptr))
				camera.behaviours.push_back(ReadBehaviour(behaviour));
		return camera;
	}

	void ReadPathPoints(const uint8_t* spline, Path& path)
	{
		const uintptr_t packAt = DEVTOOLS_RELATIVE_ADDRESS(kPackFile);
		const uintptr_t pack = packAt ? Get<uintptr_t>(reinterpret_cast<const void*>(packAt), 0, 0) : 0;
		const uint32_t offset = Get<uint32_t>(spline, kSplinePoints, 0xFFFFFFFF);
		const int count = (std::min)(static_cast<int>(Get<uint32_t>(spline, kSplinePoints + 4, 0)), kMaxPathPoints);
		if (!pack || offset == 0xFFFFFFFF || count <= 0)
			return;
		const float* data = reinterpret_cast<const float*>(pack + static_cast<uintptr_t>(offset) * 4u);
		std::vector<float> local(static_cast<size_t>(count) * kSplinePointFloats);
		if (!Read(data, local.data(), local.size() * sizeof(float)))
			return;
		path.points.resize(static_cast<size_t>(count) * 3);
		for (int i = 0; i < count; i++)
		{
			const float* p = &local[static_cast<size_t>(i) * kSplinePointFloats];
			float* out = &path.points[static_cast<size_t>(i) * 3];
			if (path.local)
			{
				memcpy(out, p, 3 * sizeof(float));
				continue;
			}
			// Placed as the game places a path's points: x' = m0 x + m1 y + m2 z + m3, and so on
			const float* m = path.matrix;
			out[0] = m[0] * p[0] + m[1] * p[1] + m[2] * p[2] + m[3];
			out[1] = m[4] * p[0] + m[5] * p[1] + m[6] * p[2] + m[7];
			out[2] = m[8] * p[0] + m[9] * p[1] + m[10] * p[2] + m[11];
		}
	}

	std::vector<const uint8_t*> ArrayOf(const uint8_t* manager, uint32_t offset, int max)
	{
		std::vector<const uint8_t*> items;
		const uint8_t* const* data = Get<const uint8_t* const*>(manager, offset + kArrayItems, nullptr);
		const int used = (std::min)(static_cast<int>(Get<uint32_t>(manager, offset + kArrayUsed, 0)), max);
		if (!data || used <= 0)
			return items;
		items.resize(static_cast<size_t>(used));
		if (!Read(data, items.data(), items.size() * sizeof(const uint8_t*)))
			items.clear();
		return items;
	}

	void ReadAll(Reading& out)
	{
		out = Reading();
		if (!LevelUp())
			return;
		const uint8_t* manager = CameraManager();
		if (!manager)
			return;

		const int stackUsed = (std::min)(static_cast<int>(Get<uint32_t>(manager, kStackUsed, 0)), kStackSize);
		for (int i = 0; i < stackUsed; i++)
			if (const void* camera = Get<const void*>(manager, kStack + i * 4, nullptr))
				out.stack.push_back(camera);
		// The stack's cameras first, then any other camera made
		std::vector<const uint8_t*> cameras;
		for (const void* camera : out.stack)
			cameras.push_back(static_cast<const uint8_t*>(camera));
		for (const uint8_t* camera : ArrayOf(manager, kCameraPool, kMaxCameras))
			if (camera && std::find(cameras.begin(), cameras.end(), camera) == cameras.end())
				cameras.push_back(camera);
		for (const uint8_t* camera : cameras)
			out.cameras.push_back(ReadCamera(camera));

		const int globalUsed = (std::min)(static_cast<int>(Get<uint32_t>(manager, kGlobalBehavioursUsed, 0)), kGlobalBehavioursSize);
		for (int i = 0; i < globalUsed; i++)
			if (const uint8_t* behaviour = Get<const uint8_t*>(manager, kGlobalBehaviours + i * 4, nullptr))
				out.global.push_back(ReadBehaviour(behaviour));

		out.active = Get<const void*>(manager, kActiveCamera, nullptr);
		out.player = Get<const void*>(manager, kPlayerCamera, nullptr);
		out.trigger = Get<const void*>(manager, kTriggerCamera, nullptr);
		out.transitionCamera = Get<const void*>(manager, kTransitionCamera, nullptr);
		out.deactivating = Get<const void*>(manager, kDeactivatingCamera, nullptr);
		out.freeCamera = Get<const void*>(manager, kFreeCamera, nullptr);

		if (const uint8_t* transition = Get<const uint8_t*>(manager, kActiveTransition, nullptr))
		{
			out.transition.have = true;
			out.transition.source = Get<const void*>(transition, kTransitionSource, nullptr);
			out.transition.target = Get<const void*>(transition, kTransitionTarget, nullptr);
			out.transition.duration = Get<float>(transition, kTransitionDuration, 0.0f);
			out.transition.easeIn = Get<float>(transition, kTransitionEaseIn, 0.0f);
			out.transition.easeOut = Get<float>(transition, kTransitionEaseOut, 0.0f);
			out.transition.timer = Get<float>(transition, kTransitionTimer, 0.0f);
			out.transition.whole = Get<float>(transition, kTransitionWhole, 0.0f);
			Read(transition + kTransitionFlags, out.transition.flags, sizeof(out.transition.flags));
		}

		for (const uint8_t* object : ArrayOf(manager, kPathPool, kMaxPaths))
		{
			if (!object)
				continue;
			Path path;
			path.object = object;
			Read(object + kPathMatrix, path.matrix, sizeof(path.matrix));
			path.type = Get<int>(object, kPathType, 0);
			path.pathClass = Get<int>(object, kPathClass, 0);
			path.duration = Get<float>(object, kPathDuration, 0.0f);
			path.length = Get<float>(object, kPathLength, 0.0f);
			uint8_t flags[4] = {};
			Read(object + kPathFlags, flags, sizeof(flags));
			path.local = flags[0] != 0;
			path.loop = flags[1] != 0;
			path.closed = flags[2] != 0;
			path.valid = flags[3] != 0;
			if (const uint8_t* spline = Get<const uint8_t*>(object, kPathSpline, nullptr))
				ReadPathPoints(spline, path);
			out.paths.push_back(std::move(path));
		}

		if (const uint8_t* dof = Get<const uint8_t*>(manager, kDepthOfField, nullptr))
		{
			out.haveDof = Read(dof + kDofFocal, out.focal, sizeof(out.focal));
			Read(dof + kDofFocusPoint, out.focusPoint, sizeof(out.focusPoint));
			out.focusCharacter = Get<const void*>(dof, kDofCharacter, nullptr);
			Read(dof + kDofFlags, out.dofFlags, sizeof(out.dofFlags));
		}
		if (const uint8_t* animations = Get<const uint8_t*>(manager, kAnimations, nullptr))
			for (const uint8_t* record : ArrayOf(animations, 0, 512))
				if (record)
					out.animations.push_back(Text(record, kAnimationFile, kAnimationFileLength));

		out.customFov = Get<float>(manager, kCustomFov, 0.0f);
		out.customFovRatio = Get<float>(manager, kCustomFovRatio, 0.0f);
		out.restoredZone = Get<uint32_t>(manager, kRestoredZone, 0);
		out.paused = Get<uint8_t>(manager, kCamerasPaused, 0) != 0;
		out.read = true;
	}

	Reading g_reading;
	int g_readingFrame = -1;

	const Reading& Cameras()
	{
		const int frame = ImGui::GetFrameCount();
		if (g_readingFrame != frame)
		{
			g_readingFrame = frame;
			ReadAll(g_reading);
		}
		return g_reading;
	}

	// ---- Words for it ----

	const char* const kCameraFlagNames[kCameraFlagCount] = { "attached", "active", "ghost", "expired", "never expires", "enter transition", "exit transition",
		"focal point", "moved by the player", "first person", "a transition's", "peeking" };
	const char* const kBehaviourFlagNames[kBehaviourFlagCount] = { "active", "never expires", "paused", "additive", "global", "rotation", "position", "field of view",
		"near", "far" };

	std::string FlagWords(const uint8_t* flags, const char* const* names, int count, int from = 0)
	{
		std::string text;
		for (int i = from; i < count; i++)
			if (flags[i])
				text += (text.empty() ? "" : ", ") + std::string(names[i]);
		return text;
	}

	const char* BlendStateName(int state)
	{
		switch (state)
		{
		case 0: return "blending in";
		case 1: return "in";
		case 2: return "blending out";
		default: return "?";
		}
	}

	const char* PathClassName(int pathClass)
	{
		switch (pathClass)
		{
		case 0: return "general";
		case 1: return "camera position";
		case 2: return "camera target";
		case 3: return "reference";
		default: return "?";
		}
	}

	ImU32 PathColour(int pathClass)
	{
		switch (pathClass)
		{
		case 1: return IM_COL32(90, 220, 255, 230);
		case 2: return IM_COL32(255, 110, 220, 230);
		case 3: return IM_COL32(255, 220, 110, 230);
		default: return IM_COL32(230, 230, 230, 230);
		}
	}

	std::string CameraLabel(const Reading& reading, const void* object)
	{
		if (!object)
			return "none";
		for (const Camera& camera : reading.cameras)
			if (camera.object == object)
				return camera.name.empty() ? std::string("(unnamed)") : camera.name;
		// One the camera handling keeps for itself (the transition's camera is not among the cameras made): its own name,
		// when it reads as one
		const std::string name = Text(object, kCameraName, kCameraNameLength);
		const bool printable = std::all_of(name.begin(), name.end(), [](char c) { return c >= 0x20 && c <= 0x7E; });
		return name.empty() || !printable ? std::string("(unnamed)") : name;
	}

	std::string Roles(const Reading& reading, const void* object)
	{
		std::string text;
		auto add = [&text](bool on, const char* word) { if (on) text += (text.empty() ? "" : ", ") + std::string(word); };
		add(object == reading.active, "ACTIVE");
		add(object == reading.player, "player's");
		add(object == reading.trigger, "trigger's");
		add(object == reading.transitionCamera, "transition's");
		add(object == reading.deactivating, "deactivating");
		add(object == reading.freeCamera, "free camera");
		return text;
	}

	bool Forward(const float rotation[4], float forward[3])
	{
		using namespace DirectX;
		XMFLOAT4X4 rows;
		XMStoreFloat4x4(&rows, XMMatrixRotationQuaternion(XMVectorSet(rotation[0], rotation[1], rotation[2], rotation[3])));
		const float length = std::sqrt(rows._31 * rows._31 + rows._32 * rows._32 + rows._33 * rows._33);
		if (!(length > 1e-6f))
			return false;
		forward[0] = rows._31 / length;
		forward[1] = rows._32 / length;
		forward[2] = rows._33 / length;
		return true;
	}

	// ---- The tab ----

	const void* g_selectedCamera = nullptr;

	void DrawBehaviour(const Behaviour& behaviour)
	{
		ImGui::Bullet();
		ImGui::TextWrapped("%s: priority %d, %s %.0f%% (timer %.2f s); in %.2f s, out %.2f s, lasts %s%s%s", behaviour.name.empty() ? "(unnamed)" : behaviour.name.c_str(),
			behaviour.priority, BlendStateName(behaviour.state), behaviour.ratio * 100.0f, behaviour.timer, behaviour.blendIn, behaviour.blendOut,
			behaviour.duration > 0.0f ? Format("%.2f s", behaviour.duration).c_str() : "until stopped", FlagWords(behaviour.flags, kBehaviourFlagNames, 5).empty() ? "" : "; ",
			FlagWords(behaviour.flags, kBehaviourFlagNames, 5).c_str());
	}

	void DrawCamerasTab()
	{
		const Reading& reading = Cameras();
		if (!reading.read)
		{
			ImGui::TextDisabled("No level running (or its cameras cannot be read).");
			return;
		}
		ImGui::Text("Active camera: %s", CameraLabel(reading, reading.active).c_str());
		ImGui::TextWrapped("Player's: %s; trigger's: %s; transition's: %s", CameraLabel(reading, reading.player).c_str(), CameraLabel(reading, reading.trigger).c_str(),
			CameraLabel(reading, reading.transitionCamera).c_str());
		if (reading.paused)
			ImGui::TextColored(ImVec4(1.0f, 0.8f, 0.3f, 1.0f), "The cameras are paused.");
		if (reading.transition.have)
		{
			const Transition& t = reading.transition;
			ImGui::TextWrapped("Transition: %s to %s, %.2f of %.2f s (ease in %.2f, out %.2f)%s%s", CameraLabel(reading, t.source).c_str(), CameraLabel(reading, t.target).c_str(), t.timer,
				t.whole > 0.0f ? t.whole : t.duration, t.easeIn, t.easeOut, t.flags[2] ? ", linear" : "", t.flags[3] ? ", instant" : "");
		}
		else
			ImGui::TextDisabled("No transition in progress.");

		ImGui::SeparatorText("The stack (last is on top)");
		bool cameras = g_cameraOverlay;
		if (ImGui::Checkbox("Draw the cameras over the game", &cameras))
			g_cameraOverlay = cameras;
		// Room for every row (up to 12 lines), and for the sideways scroll bar when the columns are wider than the window
		const float height = ImGui::GetTextLineHeightWithSpacing() * (std::min)(static_cast<float>(reading.cameras.size()) + 1.5f, 12.0f) + ImGui::GetStyle().ScrollbarSize;
		if (SCRIPT_DEBUG::BeginTable("cameras", 6, height))
		{
			ImGui::TableSetupScrollFreeze(1, 1);
			ImGui::TableSetupColumn("Camera");
			ImGui::TableSetupColumn("In stack");
			ImGui::TableSetupColumn("Role");
			ImGui::TableSetupColumn("Field of view (degrees)");
			ImGui::TableSetupColumn("Behaviours");
			ImGui::TableSetupColumn("Controlled by");
			ImGui::TableHeadersRow();
			for (const Camera& camera : reading.cameras)
			{
				const auto inStack = std::find(reading.stack.begin(), reading.stack.end(), camera.object);
				ImGui::TableNextRow();
				ImGui::TableSetColumnIndex(0);
				char label[96];
				snprintf(label, sizeof(label), "%s##%p", camera.name.empty() ? "(unnamed)" : camera.name.c_str(), camera.object);
				if (camera.object == reading.active)
					ImGui::PushStyleColor(ImGuiCol_Text, IM_COL32(120, 240, 120, 255));
				if (ImGui::Selectable(label, g_selectedCamera == camera.object, ImGuiSelectableFlags_SpanAllColumns))
					g_selectedCamera = g_selectedCamera == camera.object ? nullptr : camera.object;
				if (camera.object == reading.active)
					ImGui::PopStyleColor();
				ImGui::TableSetColumnIndex(1);
				if (inStack != reading.stack.end())
					ImGui::Text("%d", static_cast<int>(inStack - reading.stack.begin()));
				else
					ImGui::TextDisabled("-");
				ImGui::TableSetColumnIndex(2);
				ImGui::TextUnformatted(Roles(reading, camera.object).c_str());
				ImGui::TableSetColumnIndex(3);
				ImGui::Text("%.1f", camera.fov);
				ImGui::TableSetColumnIndex(4);
				ImGui::Text("%d", static_cast<int>(camera.behaviours.size()));
				ImGui::TableSetColumnIndex(5);
				if (camera.controller[0])
					ImGui::Text("%s in %s", SCRIPT_DEBUG::Guid(camera.controller[0]).c_str(), SCRIPT_DEBUG::Guid(camera.controller[1]).c_str());
				else
					ImGui::TextDisabled("-");
			}
			ImGui::EndTable();
		}
		for (const Camera& camera : reading.cameras)
		{
			if (camera.object != g_selectedCamera)
				continue;
			ImGui::SeparatorText(camera.name.empty() ? "(unnamed)" : camera.name.c_str());
			ImGui::TextWrapped("At %.2f, %.2f, %.2f; field of view %.1f degrees, near %.2f m, far %.1f m", camera.position[0], camera.position[1], camera.position[2], camera.fov, camera.nearPlane,
				camera.farPlane);
			const std::string words = FlagWords(camera.flags, kCameraFlagNames, kCameraFlagCount);
			ImGui::TextWrapped("%s", words.empty() ? "No flags." : words.c_str());
			if (camera.flags[7])
				ImGui::Text("Looks at %.2f, %.2f, %.2f", camera.focalPoint[0], camera.focalPoint[1], camera.focalPoint[2]);
			if (camera.controller[0])
				ImGui::TextWrapped("Controlled by the script entity %s in composite instance %s", SCRIPT_DEBUG::Guid(camera.controller[0]).c_str(), SCRIPT_DEBUG::Guid(camera.controller[1]).c_str());
			for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
				if (character.object == camera.character)
					ImGui::Text("Linked to %s", character.name.c_str());
			if (camera.behaviours.empty())
				ImGui::TextDisabled("No behaviours.");
			for (const Behaviour& behaviour : camera.behaviours)
				DrawBehaviour(behaviour);
		}

		if (ImGui::CollapsingHeader("Behaviours on every camera"))
		{
			if (reading.global.empty())
				ImGui::TextDisabled("None.");
			for (const Behaviour& behaviour : reading.global)
				DrawBehaviour(behaviour);
		}

		if (ImGui::CollapsingHeader("Camera paths"))
		{
			bool paths = g_pathOverlay;
			if (ImGui::Checkbox("Draw the paths over the game", &paths))
				g_pathOverlay = paths;
			ImGui::SameLine();
			bool labels = g_pathLabels;
			if (ImGui::Checkbox("With labels", &labels))
				g_pathLabels = labels;
			for (const Path& path : reading.paths)
			{
				ImGui::Bullet();
				ImGui::TextWrapped("%s path, %s: %d points, %.2f s, %.1f m%s%s%s%s", PathClassName(path.pathClass), path.type ? "curved" : "straight", static_cast<int>(path.points.size() / 3),
					path.duration, path.length, path.local ? ", relative to what it follows" : "", path.loop ? ", loops" : "", path.closed ? ", closed" : "", path.valid ? "" : ", not valid");
			}
			if (reading.paths.empty())
				ImGui::TextDisabled("No camera paths made.");
		}

		if (ImGui::CollapsingHeader("Depth of field, animations"))
		{
			if (reading.haveDof)
				ImGui::TextWrapped("Depth of field%s: focal length %.1f, focal plane %.2f, f-number %.1f; focus point %.2f, %.2f, %.2f%s%s", reading.dofFlags[3] ? " (on)" : " (off)",
					reading.focal[0], reading.focal[1], reading.focal[2], reading.focusPoint[0], reading.focusPoint[1], reading.focusPoint[2], reading.dofFlags[2] ? ", on a character" : "",
					reading.dofFlags[1] ? ", on the camera's target" : "");
			ImGui::TextWrapped("Custom field of view %.2f (ratio %.2f); zone when restored %s", reading.customFov, reading.customFovRatio, WORLD_DEBUG::ZoneName(reading.restoredZone).c_str());
			ImGui::Text("%d camera animations loaded", static_cast<int>(reading.animations.size()));
			for (const std::string& animation : reading.animations)
				ImGui::BulletText("%s", animation.c_str());
		}
	}

	// ---- Drawn over the game ----

	void DrawOverlay(ImDrawList* drawList)
	{
		if (!g_pathOverlay && !g_cameraOverlay)
			return;
		const Reading& reading = Cameras();
		if (!reading.read)
			return;
		if (g_pathOverlay)
		{
			for (const Path& path : reading.paths)
			{
				if (path.local || path.points.size() < 6)
					continue;
				const ImU32 colour = PathColour(path.pathClass);
				const size_t count = path.points.size() / 3;
				for (size_t p = 0; p + 1 < count; p++)
					DEBUG_MENU::WorldLine(drawList, &path.points[p * 3], &path.points[(p + 1) * 3], colour, 2.0f);
				if (path.closed && count > 2)
					DEBUG_MENU::WorldLine(drawList, &path.points[(count - 1) * 3], &path.points[0], colour, 2.0f);
				for (size_t p = 0; p < count; p++)
					DEBUG_MENU::WorldCross(drawList, &path.points[p * 3], 0.08f, colour, 1.5f);
				DEBUG_MENU::WorldArrow(drawList, &path.points[0], &path.points[3], colour, 2.5f);
				if (g_pathLabels)
				{
					const std::string text = Format("camera path: %s, %s, %.1f s", PathClassName(path.pathClass), path.type ? "curved (its control points)" : "straight", path.duration);
					DEBUG_MENU::WorldText(drawList, &path.points[0], text.c_str(), colour | IM_COL32_A_MASK, -8.0f);
				}
			}
		}
		if (g_cameraOverlay)
		{
			float eye[3] = {};
			const bool haveEye = DEBUG_MENU::CameraPosition(eye);
			for (const Camera& camera : reading.cameras)
			{
				// The camera being looked through sits on the eye: left out
				if (haveEye && std::fabs(camera.position[0] - eye[0]) + std::fabs(camera.position[1] - eye[1]) + std::fabs(camera.position[2] - eye[2]) < 0.05f)
					continue;
				const bool active = camera.object == reading.active;
				const ImU32 colour = active ? IM_COL32(120, 255, 120, 240) : (camera.flags[1] ? IM_COL32(255, 220, 120, 230) : IM_COL32(170, 170, 170, 200));
				DEBUG_MENU::WorldCross(drawList, camera.position, 0.2f, colour, 2.0f);
				float forward[3];
				if (Forward(camera.rotation, forward))
				{
					const float tip[3] = { camera.position[0] + forward[0], camera.position[1] + forward[1], camera.position[2] + forward[2] };
					DEBUG_MENU::WorldArrow(drawList, camera.position, tip, colour, 2.0f);
				}
				const std::string roles = Roles(reading, camera.object);
				const std::string text = Format("%s%s%s", camera.name.empty() ? "(camera)" : camera.name.c_str(), roles.empty() ? "" : " - ", roles.c_str());
				DEBUG_MENU::WorldText(drawList, camera.position, text.c_str(), colour | IM_COL32_A_MASK, -10.0f);
			}
		}
	}

	// ---- Named controls ----

	DEBUG_MENU::CommandResult ListCameras()
	{
		const Reading& reading = Cameras();
		if (!reading.read)
			return { false, "No level running (or its cameras cannot be read)" };
		std::string text = Format("active=%s\nplayer=%s\ntrigger=%s\ntransition_camera=%s\npaused=%d\nstack=%d\n", CameraLabel(reading, reading.active).c_str(),
			CameraLabel(reading, reading.player).c_str(), CameraLabel(reading, reading.trigger).c_str(), CameraLabel(reading, reading.transitionCamera).c_str(), reading.paused ? 1 : 0,
			static_cast<int>(reading.stack.size()));
		if (reading.transition.have)
			text += Format("transition=%s>%s timer=%.2f duration=%.2f\n", CameraLabel(reading, reading.transition.source).c_str(), CameraLabel(reading, reading.transition.target).c_str(),
				reading.transition.timer, reading.transition.whole > 0.0f ? reading.transition.whole : reading.transition.duration);
		for (const Camera& camera : reading.cameras)
		{
			const auto inStack = std::find(reading.stack.begin(), reading.stack.end(), camera.object);
			text += Format("camera=%s stack=%d roles=%s at=%.3f,%.3f,%.3f fov=%.2f controller=%08X instance=%08X behaviours=%d flags=%s\n", camera.name.empty() ? "(unnamed)" : camera.name.c_str(),
				inStack != reading.stack.end() ? static_cast<int>(inStack - reading.stack.begin()) : -1, Roles(reading, camera.object).c_str(), camera.position[0], camera.position[1],
				camera.position[2], camera.fov, camera.controller[0], camera.controller[1], static_cast<int>(camera.behaviours.size()),
				FlagWords(camera.flags, kCameraFlagNames, kCameraFlagCount).c_str());
		}
		// The tab's last section: the depth of field's focus, the custom field of view, the zone when restored, the animations
		if (reading.haveDof)
			text += Format("dof=%d focal_length=%.2f focal_plane=%.3f fnumber=%.2f focus=%.3f,%.3f,%.3f on_target=%d on_character=%d\n", reading.dofFlags[3] ? 1 : 0, reading.focal[0],
				reading.focal[1], reading.focal[2], reading.focusPoint[0], reading.focusPoint[1], reading.focusPoint[2], reading.dofFlags[1] ? 1 : 0, reading.dofFlags[2] ? 1 : 0);
		text += Format("custom_fov=%.3f custom_fov_ratio=%.3f restored_zone=%s\n", reading.customFov, reading.customFovRatio, WORLD_DEBUG::ZoneName(reading.restoredZone).c_str());
		for (const std::string& animation : reading.animations)
			text += "animation=" + animation + "\n";
		return { true, text };
	}

	DEBUG_MENU::CommandResult ListBehaviours()
	{
		const Reading& reading = Cameras();
		if (!reading.read)
			return { false, "No level running (or its cameras cannot be read)" };
		std::string text;
		auto line = [&text](const char* owner, const Behaviour& behaviour)
		{
			text += Format("camera=%s behaviour=%s priority=%d state=%s ratio=%.3f timer=%.2f in=%.2f out=%.2f duration=%.2f flags=%s\n", owner,
				behaviour.name.empty() ? "(unnamed)" : behaviour.name.c_str(), behaviour.priority, BlendStateName(behaviour.state), behaviour.ratio, behaviour.timer, behaviour.blendIn,
				behaviour.blendOut, behaviour.duration, FlagWords(behaviour.flags, kBehaviourFlagNames, kBehaviourFlagCount).c_str());
		};
		for (const Behaviour& behaviour : reading.global)
			line("(every camera)", behaviour);
		for (const Camera& camera : reading.cameras)
			for (const Behaviour& behaviour : camera.behaviours)
				line(camera.name.empty() ? "(unnamed)" : camera.name.c_str(), behaviour);
		return { true, text.empty() ? "(none)" : text };
	}

	DEBUG_MENU::CommandResult ListPaths()
	{
		const Reading& reading = Cameras();
		if (!reading.read)
			return { false, "No level running (or its cameras cannot be read)" };
		std::string text = Format("count=%d\n", static_cast<int>(reading.paths.size()));
		for (const Path& path : reading.paths)
		{
			text += Format("path class=%s type=%s points=%d duration=%.2f length=%.2f local=%d loop=%d closed=%d valid=%d", PathClassName(path.pathClass), path.type ? "curved" : "straight",
				static_cast<int>(path.points.size() / 3), path.duration, path.length, path.local ? 1 : 0, path.loop ? 1 : 0, path.closed ? 1 : 0, path.valid ? 1 : 0);
			if (path.points.size() >= 3)
				text += Format(" first=%.3f,%.3f,%.3f", path.points[0], path.points[1], path.points[2]);
			text += "\n";
		}
		return { true, text };
	}

	bool RegisterControls()
	{
		DEBUG_MENU::AddSwitch("scripts.cameras.paths", &g_pathOverlay);
		DEBUG_MENU::AddSwitch("scripts.cameras.path_labels", &g_pathLabels);
		DEBUG_MENU::AddSwitch("scripts.cameras.cameras", &g_cameraOverlay);
		DEBUG_MENU::AddCommand("scripts.cameras.list_cameras", "the camera stack, the active / player / trigger cameras, the transition, every camera made, the depth of field and the camera animations",
			[](const std::string&) { return DEBUG_MENU::AnswerOnRenderThread([]() { return ListCameras(); }); });
		DEBUG_MENU::AddCommand("scripts.cameras.list_behaviours", "every camera behaviour (on each camera, and those on every camera)",
			[](const std::string&) { return DEBUG_MENU::AnswerOnRenderThread([]() { return ListBehaviours(); }); });
		DEBUG_MENU::AddCommand("scripts.cameras.list_paths", "every camera path made, with its first point",
			[](const std::string&) { return DEBUG_MENU::AnswerOnRenderThread([]() { return ListPaths(); }); });
		return true;
	}

	const bool g_controls = RegisterControls();
	const bool g_added = DEBUG_MENU::AddTab("Cameras", 165, DrawCamerasTab, "Scripts") && DEBUG_MENU::AddOverlay(64, DrawOverlay);
}

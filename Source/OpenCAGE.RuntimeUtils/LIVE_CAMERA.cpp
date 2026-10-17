#include "LIVE_CAMERA.h"
#include "LIVE_LINK_SERVER.h"
#include "ZONE_LOADER.h"
#include "POSSESS.h"
#include "Config.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <DirectXMath.h>
#include <atomic>
#include <clocale>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <mutex>
#include <string>

/*
	Layouts and addresses are from the Steam retail AI.exe, and what the hooked camera hand-over does is from it (VA
	0x00432300). The GOG and Epic Games Store builds lay things out the same, at addresses looked up from these.
*/
namespace
{
	// The camera manager's fields
	constexpr uint32_t kManagerActiveCamera = 0x1D8; // the camera the game has made active (a pointer)
	constexpr uint32_t kManagerFreeCamera = 0x1F0;   // the debug free camera (a pointer)
	constexpr uint32_t kManagerDebugCamera = 0x221;  // set while the debug free camera is on
	// A camera's fields
	constexpr uint32_t kCameraBaseData = 0x2C;       // the camera's state block (below): all of the camera the hand-over to the engine reads, bar flags
	constexpr uint32_t kCameraIsActive = 0x14D;      // set while the camera is active
	constexpr uint32_t kCameraIsExpired = 0x14F;     // set once the camera has expired

	// A camera's state block. The hand-over to the engine turns the rotation into rows right, up, forward exactly as
	// DirectXMath's quaternion-to-matrix conversion does, and builds its view looking along forward, left-handed
	// (right = up x forward). The field of view is vertical, in degrees, clamped to 1..160, and the world is projected
	// with it as it is (the main render viewpoint's projection, from the game's projection matrix builder: y scale
	// 1/tan(fov/2), x scale that times height/width). Only the secondary projection (by the look of it the first-person
	// view model's) divides it by the camera manager's custom field of view ratio, for the player and transition
	// cameras, whose follow behaviour multiplied the FOV setting in; so a requested fov is written as it is.
	struct CameraState
	{
		float rotation[4]; // a quaternion: x, y, z, w
		float position[3];
		float fov;
		float nearPlane;
		float farPlane;
	};
	static_assert(sizeof(CameraState) == 0x28, "CameraStateData layout");

	// The pose OpenCAGE sent: written by the socket thread, read by the hook, under g_poseMutex
	struct Pose
	{
		bool active = false;
		uint32_t connection = 0; // the connection that sent it
		uint32_t root = 0;       // the level it was sent for (0: any)
		uint32_t generation = 0; // bumped by every request, so the hook only drops the pose it looked at
		float rotation[4] = {};
		float position[3] = {};
		float fov = 0.0f;        // <= 0: the game's own
	};
	std::mutex g_poseMutex;
	Pose g_pose;
	// Refusals are logged once, not for every pose OpenCAGE sends while it is being refused
	std::string g_lastRefusal;

	// Where the hook is with the pose (its thread only); whether it is applying (g_applying) is shared with STATUS and the overlay
	enum class State { Applying, Released, Disconnected, LevelChanged, NoLevel };
	State g_state = State::Released;
	std::atomic<bool> g_applying = false;
	std::atomic<ULONGLONG> g_frameAt = 0;
	constexpr ULONGLONG kStaleMs = 1000; // no camera frame for this long (between levels): not rendering from it

	// The game's own camera as the hook last handed it on, before any pose was written over it (CAMERA_GET): written by
	// the hook every frame a camera is chosen in a running level, read by the socket thread, under g_snapshotMutex
	struct Snapshot
	{
		bool taken = false;
		uint32_t root = 0;  // the level running when it was taken
		ULONGLONG at = 0;   // the system tick count (ms) when it was taken
		uint32_t frame = 0; // bumped by every snapshot
		CameraState state = {};
	};
	std::mutex g_snapshotMutex;
	Snapshot g_snapshot;
	// As g_lastRefusal, for CAMERA_GET (which OpenCAGE asks for every frame while the viewport follows the game)
	std::string g_lastReadRefusal;

	enum class Level { None, Other, Same };

	// Plain reads of the entity manager (fine from the socket thread), guarded: it can go while a level unloads
	Level RunningLevel(uint32_t root)
	{
		__try
		{
			if (!LIVE_LINK::LevelRunning())
				return Level::None;
			return LIVE_LINK::RunningLevelIs(root) ? Level::Same : Level::Other;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return Level::None;
		}
	}

	// The running level's root composite id (0: none), read from the entity manager as LIVE_LINK.cpp reads it to tell
	// which level is running: a snapshot is keyed to it, so one from the level before is never taken for this one's camera
	constexpr uintptr_t kEntityManagerInstance = 0x0134EF40; // where the game keeps its pointer to the entity manager
	constexpr uint32_t kEntityManagerRootGuid = 0x0C;        // the running level's root composite id
	uint32_t RunningRoot()
	{
		__try
		{
			if (!LIVE_LINK::LevelRunning())
				return 0;
			const uint8_t* manager = *reinterpret_cast<uint8_t**>(DEVTOOLS_RELATIVE_ADDRESS(kEntityManagerInstance));
			return *reinterpret_cast<const uint32_t*>(manager + kEntityManagerRootGuid);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	bool Finite(const float* values, int count)
	{
		for (int i = 0; i < count; i++)
			if (!std::isfinite(values[i]))
				return false;
		return true;
	}

	// The quaternion the hand-over to the engine turns back into these rows: forward, right = up x forward (left-handed,
	// as its view is built) and up made square to them - so the view it builds looks along forward with that up.
	bool RotationFromAxes(const float forward[3], const float up[3], float rotation[4])
	{
		using namespace DirectX;
		if (!Finite(forward, 3) || !Finite(up, 3))
			return false;
		XMVECTOR f = XMVectorSet(forward[0], forward[1], forward[2], 0.0f);
		XMVECTOR u = XMVectorSet(up[0], up[1], up[2], 0.0f);
		const float forwardLength = XMVectorGetX(XMVector3Length(f));
		const float upLength = XMVectorGetX(XMVector3Length(u));
		if (!(forwardLength > 1e-6f) || !(upLength > 1e-6f))
			return false;
		f = XMVectorScale(f, 1.0f / forwardLength);
		u = XMVectorScale(u, 1.0f / upLength);
		XMVECTOR r = XMVector3Cross(u, f);
		const float rightLength = XMVectorGetX(XMVector3Length(r));
		if (!(rightLength > 1e-4f)) // up (almost) along forward: the roll cannot be told
			return false;
		r = XMVectorScale(r, 1.0f / rightLength);
		u = XMVector3Cross(f, r);
		const XMMATRIX axes(r, u, f, XMVectorSet(0.0f, 0.0f, 0.0f, 1.0f));
		XMFLOAT4 quaternion;
		XMStoreFloat4(&quaternion, XMQuaternionNormalize(XMQuaternionRotationMatrix(axes)));
		rotation[0] = quaternion.x;
		rotation[1] = quaternion.y;
		rotation[2] = quaternion.z;
		rotation[3] = quaternion.w;
		return Finite(rotation, 4);
	}

	// The other way (CAMERA_GET): the rows the hand-over to the engine makes of a camera's rotation, as DirectXMath's
	// quaternion-to-matrix conversion does - up the second, forward the third - made unit (they are, for the unit
	// rotation of a camera the game set up).
	bool AxesFromRotation(const float rotation[4], float forward[3], float up[3])
	{
		using namespace DirectX;
		XMFLOAT4X4 rows;
		XMStoreFloat4x4(&rows, XMMatrixRotationQuaternion(XMVectorSet(rotation[0], rotation[1], rotation[2], rotation[3])));
		const XMVECTOR f = XMVectorSet(rows._31, rows._32, rows._33, 0.0f);
		const XMVECTOR u = XMVectorSet(rows._21, rows._22, rows._23, 0.0f);
		const float forwardLength = XMVectorGetX(XMVector3Length(f));
		const float upLength = XMVectorGetX(XMVector3Length(u));
		if (!(forwardLength > 1e-6f) || !(upLength > 1e-6f))
			return false;
		XMFLOAT3 axis;
		XMStoreFloat3(&axis, XMVectorScale(f, 1.0f / forwardLength));
		forward[0] = axis.x;
		forward[1] = axis.y;
		forward[2] = axis.z;
		XMStoreFloat3(&axis, XMVectorScale(u, 1.0f / upLength));
		up[0] = axis.x;
		up[1] = axis.y;
		up[2] = axis.z;
		return Finite(forward, 3) && Finite(up, 3);
	}

	// Called under g_poseMutex
	LIVE_LINK::Result Refuse(const std::string& message)
	{
		if (message != g_lastRefusal)
			DevTools::Log("LiveLink: camera refused: %s", message.c_str());
		g_lastRefusal = message;
		return LIVE_LINK::Result{ false, message };
	}

	// Called under g_snapshotMutex
	LIVE_LINK::Result RefuseRead(const std::string& message)
	{
		if (message != g_lastReadRefusal)
			DevTools::Log("LiveLink: camera read refused: %s", message.c_str());
		g_lastReadRefusal = message;
		return LIVE_LINK::Result{ false, message };
	}

	// The camera the camera manager hands to the engine: the active camera while it is active and not expired, or
	// the debug free camera when that is on. None (during a load) leaves the frame alone.
	uint8_t* ChooseCamera(void* manager)
	{
		__try
		{
			uint8_t* cameraManager = static_cast<uint8_t*>(manager);
			uint8_t* camera = nullptr;
			uint8_t* active = *reinterpret_cast<uint8_t**>(cameraManager + kManagerActiveCamera);
			if (active && active[kCameraIsActive] && !active[kCameraIsExpired])
				camera = active;
			if (cameraManager[kManagerDebugCamera])
			{
				uint8_t* freeCamera = *reinterpret_cast<uint8_t**>(cameraManager + kManagerFreeCamera);
				if (freeCamera)
					camera = freeCamera;
			}
			return camera;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	bool ReadState(const uint8_t* camera, CameraState& state)
	{
		__try
		{
			memcpy(&state, camera + kCameraBaseData, sizeof(state));
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	void RestoreState(uint8_t* camera, const CameraState& saved)
	{
		__try
		{
			memcpy(camera + kCameraBaseData, &saved, sizeof(saved));
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
		}
	}

	// Keeps the camera's own state for CAMERA_GET, keyed to the running level; none without a level to key it to, or
	// from a camera that is not set up yet
	void TakeSnapshot(const CameraState& state)
	{
		const uint32_t root = RunningRoot();
		if (!root || !Finite(state.rotation, 4) || !Finite(state.position, 3) || !std::isfinite(state.fov))
			return;
		std::lock_guard<std::mutex> lock(g_snapshotMutex);
		g_snapshot.taken = true;
		g_snapshot.root = root;
		g_snapshot.at = GetTickCount64();
		g_snapshot.frame++;
		g_snapshot.state = state;
	}

	// Writes the pose over the camera's state (near and far stay the game's); saved (its own state) goes back if that fails
	bool WritePose(uint8_t* camera, const Pose& pose, const CameraState& saved)
	{
		__try
		{
			CameraState* state = reinterpret_cast<CameraState*>(camera + kCameraBaseData);
			memcpy(state->rotation, pose.rotation, sizeof(state->rotation));
			memcpy(state->position, pose.position, sizeof(state->position));
			if (pose.fov > 0.0f)
				state->fov = pose.fov;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			RestoreState(camera, saved);
			return false;
		}
	}

	// Follows the pose's state from frame to frame: zone streaming, the log and the overlay change with it
	void Transition(State state)
	{
		if (state == g_state)
			return;
		const State previous = g_state;
		g_state = state;
		const bool applying = state == State::Applying;
		ZONE_LOADER::SetForced(ZONE_LOADER::Source::LiveCamera, applying);
		g_applying = applying;

		const char* text = nullptr;
		if (applying)
			text = "Camera following OpenCAGE";
		else if (previous == State::Applying || previous == State::NoLevel)
		{
			switch (state)
			{
			case State::Released: text = "Camera released"; break;
			case State::Disconnected: text = "Camera released (OpenCAGE disconnected)"; break;
			case State::LevelChanged: text = "Camera waiting: another level is running"; break;
			case State::NoLevel: text = "Camera waiting: no level is running"; break;
			default: break;
			}
		}
		if (text)
		{
			DevTools::Log("LiveLink: %s", text);
			LIVE_LINK_SERVER::ShowActivity(text);
		}
	}
}

LIVE_LINK::Result LIVE_CAMERA::SetPose(uint32_t connection, uint32_t root, bool active, const float position[3], const float forward[3], const float up[3], float fov)
{
	std::lock_guard<std::mutex> lock(g_poseMutex);
	if (!active)
	{
		// Taken whatever is running: the hook gives the game its camera back on its next frame
		g_pose.active = false;
		g_pose.generation++;
		g_lastRefusal.clear();
		return LIVE_LINK::Result{ true, "Camera released" };
	}
	// A pose for another level from the connection holding one means OpenCAGE has moved on to that level: the pose it sent
	// for this one is stale whether or not the new one can be taken (turned away, it must not leave the old one running)
	if (g_pose.active && g_pose.connection == connection && g_pose.root != root)
	{
		g_pose.active = false;
		g_pose.generation++;
	}
	if (!Config::Get().liveLinkCamera)
		return Refuse("Camera sync is off in the game (LiveLinkCamera=0 in OpenCAGE.RuntimeUtils.ini)");
	switch (RunningLevel(root))
	{
	case Level::None:
		return Refuse("No level is running");
	case Level::Other:
		return Refuse("The game is running a different level - save, and load this one in the game");
	default:
		break;
	}
	float rotation[4];
	if (!Finite(position, 3) || !std::isfinite(fov) || !RotationFromAxes(forward, up, rotation))
		return Refuse("The camera pose cannot be used (it needs a finite position, and forward and up vectors that are not zero or parallel)");

	g_pose.active = true;
	g_pose.connection = connection;
	g_pose.root = root;
	g_pose.generation++;
	memcpy(g_pose.rotation, rotation, sizeof(rotation));
	memcpy(g_pose.position, position, sizeof(g_pose.position));
	g_pose.fov = fov;
	g_lastRefusal.clear();
	return LIVE_LINK::Result{ true, "Camera following OpenCAGE" };
}

bool LIVE_CAMERA::Applying()
{
	return g_applying && GetTickCount64() - g_frameAt <= kStaleMs;
}

__declspec(noinline)
void __fastcall LIVE_CAMERA::h_synchronize_with_engine(void* _this, void* /*_EDX*/)
{
	Pose pose;
	{
		std::lock_guard<std::mutex> lock(g_poseMutex);
		pose = g_pose;
	}

	// Checked every frame: the connection that sent the pose must still be OpenCAGE's, and the level it was sent for
	// still the one running
	State state = State::Applying;
	if (!pose.active)
		state = State::Released;
	else if (!LIVE_LINK_SERVER::Connected() || pose.connection != LIVE_LINK_SERVER::CurrentConnection())
		state = State::Disconnected;
	else
	{
		// Kept while no level or another level runs, and applied again when its level is back (a reload, or a trip to
		// another level and back): OpenCAGE still holds it as taken. A pose for another level from OpenCAGE replaces it
		// (when its CAMERA request comes in), and so does a release.
		switch (RunningLevel(pose.root))
		{
		case Level::None:
			state = State::NoLevel;
			break;
		case Level::Other:
			state = State::LevelChanged;
			break;
		default:
			break;
		}
	}
	if (state == State::Disconnected)
	{
		// Over for good: nobody is left to take it back - unless a newer request replaced it since
		std::lock_guard<std::mutex> lock(g_poseMutex);
		if (g_pose.generation == pose.generation)
			g_pose.active = false;
	}
	Transition(state);
	g_frameAt = GetTickCount64();

	// The camera's own state is kept every frame before any pose goes over it, so CAMERA_GET always reads the game's
	// camera. The original renders the frame from the pose, then the camera gets its own state back before anything else
	// sees it.
	uint8_t* camera = ChooseCamera(_this);
	CameraState saved = {};
	if (camera && !ReadState(camera, saved))
		camera = nullptr;
	if (camera)
		TakeSnapshot(saved);
	bool written = false, behind = false;
	if (camera && state == State::Applying)
		written = WritePose(camera, pose, saved);
	else if (camera && Config::Get().possess)
	{
		// While a character is possessed (and OpenCAGE's camera isn't driving the game's), the camera behind it
		float position[3], forward[3], up[3];
		Pose follow;
		if (POSSESS::CameraPose(position, forward, up) && RotationFromAxes(forward, up, follow.rotation))
		{
			memcpy(follow.position, position, sizeof(follow.position));
			written = behind = WritePose(camera, follow, saved);
		}
	}
	// Where the frame is drawn from, for a possessed character's keys to move it relative to
	if (camera && Config::Get().possess)
	{
		CameraState drawn;
		float forward[3], up[3];
		if (ReadState(camera, drawn) && AxesFromRotation(drawn.rotation, forward, up))
			POSSESS::SeenFrom(drawn.position, forward, behind);
	}
	synchronize_with_engine(_this);
	if (written)
		RestoreState(camera, saved);
}

LIVE_LINK::Result LIVE_CAMERA::GetPose(uint32_t root)
{
	std::lock_guard<std::mutex> lock(g_snapshotMutex);
	// Refused as a CAMERA request refuses a pose (the hook is not attached with LiveLinkCamera=0, so there would be no snapshot)
	if (!Config::Get().liveLinkCamera)
		return RefuseRead("Camera sync is off in the game (LiveLinkCamera=0 in OpenCAGE.RuntimeUtils.ini)");
	switch (RunningLevel(root))
	{
	case Level::None:
		return RefuseRead("No level is running");
	case Level::Other:
		return RefuseRead("The game is running a different level - save, and load this one in the game");
	default:
		break;
	}
	// Only a snapshot from the last second, of the level running now: no camera is chosen while a level loads (which
	// takes longer than that, reloading this one too), and one from the level before is not this one's camera
	const uint32_t running = RunningRoot();
	float forward[3], up[3];
	if (!g_snapshot.taken || !running || g_snapshot.root != running || GetTickCount64() - g_snapshot.at > kStaleMs ||
		!AxesFromRotation(g_snapshot.state.rotation, forward, up))
		return RefuseRead("The game has not drawn a frame from its camera yet");
	g_lastReadRefusal.clear();

	// Invariant whatever locale the process sets: OpenCAGE parses it with the invariant culture
	static const _locale_t invariant = _create_locale(LC_NUMERIC, "C");
	const float* position = g_snapshot.state.position;
	// As it is drawn: the hand-over to the engine clamps the field of view to 1..160 before projecting with it
	const float fov = g_snapshot.state.fov < 1.0f ? 1.0f : g_snapshot.state.fov > 160.0f ? 160.0f : g_snapshot.state.fov;
	char text[1024];
	_snprintf_s_l(text, sizeof(text), _TRUNCATE, "position=%.4f,%.4f,%.4f\nforward=%.4f,%.4f,%.4f\nup=%.4f,%.4f,%.4f\nfov=%.4f\nframe=%u", invariant,
		position[0], position[1], position[2], forward[0], forward[1], forward[2], up[0], up[1], up[2], fov, g_snapshot.frame);
	return LIVE_LINK::Result{ true, text };
}

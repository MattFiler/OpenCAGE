#include "CINEMATIC_CAMERA.h"
#include "CINEMATIC_TRACKS.h"
#include "CINEMATIC_VISUALS.h"
#include "Config.h"
#include "DEBUG_MENU.h"
#include "LIVE_LINK.h"
#include "POSSESS.h"
#include "ZONE_LOADER.h"

#include <detours.h>

#include <DirectXMath.h>
#include <Xinput.h>
#include <algorithm>
#include <atomic>
#include <clocale>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

#include <imgui.h>

/*
	Addresses and layouts are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).

	The flying (speeds, keys, gamepad mapping, the eased keys and smoothed mouse) follows Cinematic Tools' camera, by
	Hattiwatti. Its orientation is kept as yaw (about the world's up), pitch and roll, so the horizon stays level unless it
	is rolled, and pitch stops short of straight up and down.
*/
namespace
{
	constexpr float kPi = 3.14159265f;
	constexpr float kDegrees = 180.0f / kPi;
	constexpr float kPitchLimit = 89.5f / kDegrees;

	// The game's globals (a pointer to them); the byte after that pointer is set while its characters can be read. In the
	// globals, the character manager (a pointer), with its lists of characters: each a pointer to their pointers then how
	// many - the players, those alive that aren't players, and the dead
	constexpr uintptr_t kGameGlobals = 0x012F0C88;
	constexpr uint32_t kCharactersValid = 0x04;
	constexpr uint32_t kGlobalsCharacters = 0x08;
	constexpr uint32_t kPlayers = 0x80;
	constexpr uint32_t kAlive = 0xD0;
	constexpr uint32_t kDead = 0x120;
	constexpr uint32_t kMaxPerList = 18;

	// A character's fields: its unique id, its world matrix (rows 0-2 its axes, row 3 where it is) and the byte set while
	// it is frozen
	constexpr uint32_t kCharacterId = 0x50;
	constexpr uint32_t kCharacterMatrix = 0x400;
	constexpr uint32_t kCharacterFrozen = 0x302;

	// The entity manager (a pointer to it): the running level's root composite id, and the level scripts' transport (2
	// while the pause menu is open)
	constexpr uintptr_t kEntityManager = 0x0134EF40;
	constexpr uint32_t kEntityRoot = 0x0C;
	constexpr uint32_t kEntityTransport = 0xBC;
	constexpr uint32_t kTransportPauseMenu = 2;

	// The pause (a pointer to it): the reasons the game is paused, as bits. The game over screen pauses it for a reason of
	// its own, which the transport above doesn't show as the pause menu.
	constexpr uintptr_t kPauseReasons = 0x012F194C;
	constexpr uint32_t kPauseGameOver = 0x2;
	constexpr uint32_t kPauseMenu = 0x400;

	// The game's freezing of a character (as its scripts freeze one: it stops acting on its orders, and a player's input
	// stops reaching it), and the undoing of that
	typedef void(__thiscall* t_freeze)(void* character, bool frozen);
	const auto character_freeze = reinterpret_cast<t_freeze>(DEVTOOLS_RELATIVE_ADDRESS(0x0038AFD0));
	typedef void(__thiscall* t_unfreeze)(void* character);
	const auto character_unfreeze = reinterpret_cast<t_unfreeze>(DEVTOOLS_RELATIVE_ADDRESS(0x0037A270));

	// Cinematic Tools' defaults: 3 m/s, 45 degrees a second (keys and stick), rolls at 22.5, field of view by 5 a second,
	// mouse sensitivity 0.5
	constexpr float kMouseRadiansPerCount = 0.003f; // at sensitivity 1
	constexpr float kWheelDegreesPerNotch = 2.0f;
	constexpr float kEaseSeconds = 0.2f;            // keys and sticks ease in and out over this (as Cinematic Tools' did)
	constexpr float kMouseSmoothSeconds = 0.06f;
	constexpr float kFovMin = 2.0f, kFovMax = 150.0f;

	// ---- Switches and values (named for OpenCAGE and testing) ----

	std::atomic<bool> g_wanted = false;        // cinematic.camera
	std::atomic<bool> g_keepInput = true;      // cinematic.keep_input
	std::atomic<bool> g_flyWithMenu = false;   // cinematic.fly_with_menu: the keys and mouse fly it with the Debug Menu open too
	std::atomic<bool> g_keepGamepad = true;    // cinematic.keep_gamepad
	std::atomic<bool> g_freezePlayer = true;   // cinematic.freeze_player
	std::atomic<bool> g_smoothMouse = true;    // cinematic.smooth_mouse
	std::atomic<bool> g_startFromGame = false; // cinematic.start_from_game
	std::atomic<float> g_moveSpeed = 3.0f;     // m/s
	std::atomic<float> g_turnSpeed = 45.0f;    // degrees a second
	std::atomic<float> g_rollSpeed = 22.5f;    // degrees a second
	std::atomic<float> g_fovSpeed = 5.0f;      // degrees a second
	std::atomic<float> g_mouseSensitivity = 0.5f;
	std::atomic<float> g_fov = 60.0f;          // vertical degrees (follows the camera; set from outside, the camera takes it)
	std::atomic<float> g_roll = 0.0f;          // degrees (likewise)
	std::atomic<float> g_pad = -1.0f;          // the XInput pad that flies it: 0..3, or -1 for the first connected

	// The character the camera is wanted locked to (unique id, 0: none) - taken by the camera at its next frame
	std::atomic<uint32_t> g_lockWanted = 0;

	// ---- Shared between the camera task, the window, the render thread and commands (under g_mutex) ----

	struct Camera
	{
		bool on = false;
		uint32_t root = 0;          // the level it was turned on in
		bool havePose = false;      // a pose to go back to when it is turned on again in the same level
		float position[3] = {};     // in the world, or relative to the character it is locked to
		float yaw = 0.0f, pitch = 0.0f, roll = 0.0f; // radians; relative to the character when locked
		float fov = 60.0f;
		uint32_t lockId = 0;        // the character it is locked to (0: the world)
	};
	std::mutex g_mutex;
	Camera g_camera;
	bool g_havePlace = false;
	CINEMATIC_CAMERA::Pose g_place;           // a pose to go to at the next frame
	bool g_haveGame = false, g_haveDrawn = false;
	CINEMATIC_CAMERA::Pose g_game, g_drawn;   // the game camera's own pose, and the drawn one, at the last frame
	ULONGLONG g_drawnAt = 0;
	CINEMATIC_CAMERA::Pose g_last;            // the pose it rendered from at its last frame
	std::string g_status = "off";
	ULONGLONG g_onAt = 0;                     // when it was last turned on (for the overlay's note)

	std::atomic<bool> g_active = false;       // it had the camera at its last frame
	std::atomic<ULONGLONG> g_activeAt = 0;
	std::atomic<bool> g_holdInput = false;    // it keeps the game's input (on, keeping it, none of the game's menus up)
	std::atomic<HWND> g_window = nullptr;

	// The mouse's movement and wheel since the camera last took them (window thread -> camera task)
	std::atomic<long> g_mouseX = 0, g_mouseY = 0, g_wheel = 0;

	// The player frozen (the entity thread only), and what is wanted (the camera task)
	std::atomic<bool> g_freezeWanted = false;
	bool g_freezeQueuedFor = false;           // camera task
	uint8_t* g_frozenPlayer = nullptr;
	uint32_t g_frozenId = 0;
	uint32_t g_frozenRoot = 0;
	std::atomic<bool> g_playerFrozen = false; // for reports: the player is frozen by the camera

	void SetStatus(const std::string& text)
	{
		// Called under g_mutex
		if (text != g_status)
			DevTools::Log("Debug menu: cinematic camera: %s", text.c_str());
		g_status = text;
	}

	std::string Number(float value, int decimals = 3)
	{
		static const _locale_t invariant = _create_locale(LC_NUMERIC, "C");
		char text[48];
		_snprintf_s_l(text, sizeof(text), _TRUNCATE, "%.*f", invariant, decimals, value);
		return text;
	}

	bool Finite(const float* values, int count)
	{
		for (int i = 0; i < count; i++)
			if (!std::isfinite(values[i]))
				return false;
		return true;
	}

	// Numbers from a command or a file, in any locale ("1.5", not "1,5")
	bool ParseNumbers(const std::string& text, float* out, int most, int& count)
	{
		static const _locale_t invariant = _create_locale(LC_NUMERIC, "C");
		count = 0;
		const char* at = text.c_str();
		while (*at && count < most)
		{
			while (*at == ' ' || *at == ',' || *at == ';' || *at == '\t')
				at++;
			if (!*at)
				break;
			char* end = nullptr;
			const double value = _strtod_l(at, &end, invariant);
			if (end == at || !std::isfinite(value))
				return false;
			out[count++] = static_cast<float>(value);
			at = end;
		}
		return true;
	}

	// ---- Reading the game (every read guarded) ----

	template <typename T>
	bool Read(const uint8_t* at, T& value)
	{
		__try
		{
			value = *reinterpret_cast<const volatile T*>(at);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool ReadMatrix(const uint8_t* character, float matrix[16])
	{
		__try
		{
			memcpy(matrix, character + kCharacterMatrix, sizeof(float) * 16);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
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

	// The running level's root composite id (0: no level)
	uint32_t RunningRoot()
	{
		if (!LevelRunning())
			return 0;
		const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kEntityManager);
		uint8_t* manager = nullptr;
		uint32_t root = 0;
		if (!at || !Read(reinterpret_cast<const uint8_t*>(at), manager) || !manager || !Read(manager + kEntityRoot, root))
			return 0;
		return root;
	}

	// Whether the game's pause menu is open (its keys and mouse are then the game's: the camera holds still)
	bool PauseMenuOpen()
	{
		const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kEntityManager);
		uint8_t* manager = nullptr;
		uint32_t transport = 0;
		return at && Read(reinterpret_cast<const uint8_t*>(at), manager) && manager && Read(manager + kEntityTransport, transport) &&
			transport == kTransportPauseMenu;
	}

	// Whether one of the game's menus is up that its keys, mouse and pad must reach: the pause menu, or the game over
	// screen (the camera holds still meanwhile, and keeps nothing from the game)
	bool GameMenuOpen()
	{
		if (PauseMenuOpen())
			return true;
		const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kPauseReasons);
		uint8_t* pause = nullptr;
		uint32_t reasons = 0;
		return at && Read(reinterpret_cast<const uint8_t*>(at), pause) && pause && Read(pause, reasons) &&
			(reasons & (kPauseGameOver | kPauseMenu)) != 0;
	}

	uint8_t* CharacterManager()
	{
		__try
		{
			const uintptr_t globalsAt = DEVTOOLS_RELATIVE_ADDRESS(kGameGlobals);
			if (!globalsAt || !*reinterpret_cast<const uint8_t*>(globalsAt + kCharactersValid))
				return nullptr;
			uint8_t* globals = *reinterpret_cast<uint8_t**>(globalsAt);
			return globals ? *reinterpret_cast<uint8_t**>(globals + kGlobalsCharacters) : nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	// The character with this unique id among those the game runs (null when it is gone)
	uint8_t* FindCharacter(uint32_t id)
	{
		uint8_t* manager = CharacterManager();
		if (!manager || !id)
			return nullptr;
		const uint32_t lists[] = { kPlayers, kAlive, kDead };
		__try
		{
			for (uint32_t list : lists)
			{
				uint8_t* const* entries = *reinterpret_cast<uint8_t* const* const*>(manager + list);
				const uint32_t count = *reinterpret_cast<const uint32_t*>(manager + list + 4);
				for (uint32_t i = 0; entries && i < count && i < kMaxPerList; i++)
					if (entries[i] && *reinterpret_cast<const uint32_t*>(entries[i] + kCharacterId) == id)
						return entries[i];
			}
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
		}
		return nullptr;
	}

	uint8_t* Player()
	{
		uint8_t* manager = CharacterManager();
		if (!manager)
			return nullptr;
		__try
		{
			uint8_t* const* entries = *reinterpret_cast<uint8_t* const* const*>(manager + kPlayers);
			const uint32_t count = *reinterpret_cast<const uint32_t*>(manager + kPlayers + 4);
			return entries && count ? entries[0] : nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	// ---- Orientation ----

	using namespace DirectX;

	XMVECTOR Rotation(float yaw, float pitch, float roll)
	{
		return XMQuaternionRotationRollPitchYaw(pitch, yaw, roll);
	}

	// Yaw, pitch and roll back from a rotation (the rows it makes: right, up, forward)
	void Angles(FXMVECTOR rotation, float& yaw, float& pitch, float& roll)
	{
		XMFLOAT4X4 rows;
		XMStoreFloat4x4(&rows, XMMatrixRotationQuaternion(XMQuaternionNormalize(rotation)));
		const float fx = rows._31, fy = rows._32, fz = rows._33;
		yaw = atan2f(fx, fz);
		const float clamped = fy < -1.0f ? -1.0f : fy > 1.0f ? 1.0f : fy;
		pitch = -asinf(clamped);
		roll = atan2f(rows._12, rows._22);
		if (pitch > kPitchLimit) pitch = kPitchLimit;
		if (pitch < -kPitchLimit) pitch = -kPitchLimit;
	}

	// A character's frame from its matrix: the rotation (its axes made square and unit) and where it is
	bool CharacterFrame(const float matrix[16], XMVECTOR& rotation, XMVECTOR& position)
	{
		for (int i = 0; i < 16; i++)
			if (!std::isfinite(matrix[i]))
				return false;
		XMVECTOR r = XMVectorSet(matrix[0], matrix[1], matrix[2], 0.0f);
		XMVECTOR f = XMVectorSet(matrix[8], matrix[9], matrix[10], 0.0f);
		if (XMVectorGetX(XMVector3LengthSq(r)) < 1e-8f || XMVectorGetX(XMVector3LengthSq(f)) < 1e-8f)
			return false;
		f = XMVector3Normalize(f);
		XMVECTOR u = XMVector3Cross(f, r);
		if (XMVectorGetX(XMVector3LengthSq(u)) < 1e-8f)
			return false;
		u = XMVector3Normalize(u);
		r = XMVector3Cross(u, f);
		const XMMATRIX axes(r, u, f, XMVectorSet(0.0f, 0.0f, 0.0f, 1.0f));
		rotation = XMQuaternionNormalize(XMQuaternionRotationMatrix(axes));
		position = XMVectorSet(matrix[12], matrix[13], matrix[14], 1.0f);
		return true;
	}

	// The camera's pose in the world (called under g_mutex): relative to the character it is locked to, when it is
	bool WorldPose(const Camera& camera, CINEMATIC_CAMERA::Pose& pose, bool& targetGone)
	{
		targetGone = false;
		XMVECTOR rotation = Rotation(camera.yaw, camera.pitch, camera.roll);
		XMVECTOR position = XMVectorSet(camera.position[0], camera.position[1], camera.position[2], 1.0f);
		if (camera.lockId)
		{
			float matrix[16];
			XMVECTOR targetRotation, targetPosition;
			const uint8_t* character = FindCharacter(camera.lockId);
			if (!character || !ReadMatrix(character, matrix) || !CharacterFrame(matrix, targetRotation, targetPosition))
			{
				targetGone = true;
				return false;
			}
			position = XMVectorAdd(targetPosition, XMVector3Rotate(XMVectorSetW(position, 0.0f), targetRotation));
			rotation = XMQuaternionMultiply(rotation, targetRotation);
		}
		XMFLOAT4 q;
		XMStoreFloat4(&q, XMQuaternionNormalize(rotation));
		pose.rotation[0] = q.x; pose.rotation[1] = q.y; pose.rotation[2] = q.z; pose.rotation[3] = q.w;
		XMFLOAT3 p;
		XMStoreFloat3(&p, position);
		pose.position[0] = p.x; pose.position[1] = p.y; pose.position[2] = p.z;
		pose.fov = camera.fov;
		return true;
	}

	// Puts the camera at a world pose (called under g_mutex): relative to the character it is locked to, when it is
	bool SetWorldPose(Camera& camera, const CINEMATIC_CAMERA::Pose& pose)
	{
		XMVECTOR rotation = XMVectorSet(pose.rotation[0], pose.rotation[1], pose.rotation[2], pose.rotation[3]);
		XMVECTOR position = XMVectorSet(pose.position[0], pose.position[1], pose.position[2], 1.0f);
		if (camera.lockId)
		{
			float matrix[16];
			XMVECTOR targetRotation, targetPosition;
			const uint8_t* character = FindCharacter(camera.lockId);
			if (!character || !ReadMatrix(character, matrix) || !CharacterFrame(matrix, targetRotation, targetPosition))
				return false;
			const XMVECTOR inverse = XMQuaternionInverse(targetRotation);
			position = XMVector3Rotate(XMVectorSetW(XMVectorSubtract(position, targetPosition), 0.0f), inverse);
			rotation = XMQuaternionMultiply(rotation, inverse);
		}
		XMFLOAT3 p;
		XMStoreFloat3(&p, position);
		camera.position[0] = p.x; camera.position[1] = p.y; camera.position[2] = p.z;
		Angles(rotation, camera.yaw, camera.pitch, camera.roll);
		if (std::isfinite(pose.fov) && pose.fov > 0.0f)
			camera.fov = (std::max)(kFovMin, (std::min)(kFovMax, pose.fov));
		camera.havePose = true;
		return true;
	}

	// A pose from where it is, which way it looks (yaw, pitch, roll in degrees) and a field of view
	CINEMATIC_CAMERA::Pose PoseFrom(const float position[3], float yawDegrees, float pitchDegrees, float rollDegrees, float fov)
	{
		CINEMATIC_CAMERA::Pose pose;
		memcpy(pose.position, position, sizeof(pose.position));
		XMFLOAT4 q;
		XMStoreFloat4(&q, Rotation(yawDegrees / kDegrees, pitchDegrees / kDegrees, rollDegrees / kDegrees));
		pose.rotation[0] = q.x; pose.rotation[1] = q.y; pose.rotation[2] = q.z; pose.rotation[3] = q.w;
		pose.fov = fov;
		return pose;
	}

	void Axes(const CINEMATIC_CAMERA::Pose& pose, float forward[3], float up[3])
	{
		XMFLOAT4X4 rows;
		XMStoreFloat4x4(&rows, XMMatrixRotationQuaternion(XMVectorSet(pose.rotation[0], pose.rotation[1], pose.rotation[2], pose.rotation[3])));
		forward[0] = rows._31; forward[1] = rows._32; forward[2] = rows._33;
		up[0] = rows._21; up[1] = rows._22; up[2] = rows._23;
	}

	// ---- Input ----

	bool GameHasFocus()
	{
		const HWND foreground = GetForegroundWindow();
		if (!foreground)
			return false;
		DWORD process = 0;
		GetWindowThreadProcessId(foreground, &process);
		return process == GetCurrentProcessId();
	}

	// Key 0 is no key: never down
	bool Down(int key)
	{
		return key && (GetAsyncKeyState(key) & 0x8000) != 0;
	}

	// Edges of the keys that do something once per press (the camera task only)
	struct KeyEdge
	{
		int key;
		bool was;
	};
	KeyEdge g_edges[] = {
		{ 0, false },          // the camera's own key (filled in when first read)
		{ VK_F1, false }, { VK_F2, false }, { VK_F3, false }, { VK_HOME, false }, { VK_DELETE, false }, { VK_END, false },
		{ VK_PRIOR, false }, { VK_NEXT, false },
	};
	enum EdgeIndex { EdgeToggle, EdgeAddNode, EdgeDeleteNode, EdgePlay, EdgeHud, EdgePause, EdgeStep, EdgeFaster, EdgeSlower, kEdgeCount };
	bool g_keysChecked = false; // camera task: the keys filled in, any another helper has left out

	// The camera's key: none (0) when it is another helper's key, as both would act on it
	int CameraKey()
	{
		static const int key = []()
		{
			const int wanted = Config::IniKey("CinematicCameraKey", VK_F8);
			if (!CINEMATIC_CAMERA::TakenKey(wanted))
				return wanted;
			DevTools::Log("Debug menu: cinematic camera: CinematicCameraKey is the same as another helper's key: set another");
			return 0;
		}();
		return key;
	}

	// A key's name for the tabs and notes ("no key" for none)
	const char* KeyText(int key)
	{
		return key ? Config::KeyName(key) : "no key";
	}

	// Pressed since the last frame (each key's edge is followed every frame, whether or not it is acted on)
	bool Pressed(int index, bool allowed)
	{
		KeyEdge& edge = g_edges[index];
		const bool down = Down(edge.key);
		const bool pressed = down && !edge.was;
		edge.was = down;
		return pressed && allowed;
	}

	// XInput, from whichever version the game loaded (else the first there is)
	typedef DWORD(WINAPI* t_xinput_get_state)(DWORD, XINPUT_STATE*);
	t_xinput_get_state XInputState()
	{
		static const t_xinput_get_state function = []() -> t_xinput_get_state
		{
			const wchar_t* dlls[] = { L"xinput1_3.dll", L"xinput1_4.dll", L"xinput9_1_0.dll" };
			for (const wchar_t* dll : dlls)
				if (HMODULE module = GetModuleHandleW(dll))
					if (FARPROC address = GetProcAddress(module, "XInputGetState"))
						return reinterpret_cast<t_xinput_get_state>(address);
			for (const wchar_t* dll : dlls)
				if (HMODULE module = LoadLibraryW(dll))
					if (FARPROC address = GetProcAddress(module, "XInputGetState"))
						return reinterpret_cast<t_xinput_get_state>(address);
			return nullptr;
		}();
		return function;
	}

	struct PadState
	{
		bool present = false;
		float leftX = 0, leftY = 0, rightX = 0, rightY = 0, triggers = 0;
		WORD buttons = 0;
	};

	// The camera task only: the pad in use, and when pads were last looked for
	int g_padInUse = -1;
	ULONGLONG g_padSearchedAt = 0;

	// A stick past its dead zone, its push squared for finer control near the middle (as Cinematic Tools had it)
	void Stick(SHORT rawX, SHORT rawY, int deadZone, float& x, float& y)
	{
		const float fx = rawX, fy = rawY;
		float magnitude = sqrtf(fx * fx + fy * fy);
		x = y = 0.0f;
		if (magnitude <= deadZone)
			return;
		const float nx = fx / magnitude, ny = fy / magnitude;
		magnitude = (std::min)(magnitude, 32767.0f);
		float push = (magnitude - deadZone) / (32767.0f - deadZone);
		push *= push;
		x = nx * push;
		y = ny * push;
	}

	PadState ReadPad()
	{
		PadState state;
		const t_xinput_get_state get = XInputState();
		if (!get)
			return state;
		const int wanted = static_cast<int>(g_pad.load());
		const ULONGLONG now = GetTickCount64();
		XINPUT_STATE raw = {};
		if (g_padInUse < 0 || (wanted >= 0 && g_padInUse != wanted))
		{
			// Looking for a pad is slow for the slots with none: once a second at most
			if (now - g_padSearchedAt < 1000)
				return state;
			g_padSearchedAt = now;
			g_padInUse = -1;
			for (int i = 0; i < 4 && g_padInUse < 0; i++)
				if ((wanted < 0 || i == wanted) && get(i, &raw) == ERROR_SUCCESS)
					g_padInUse = i;
			if (g_padInUse < 0)
				return state;
		}
		else if (get(g_padInUse, &raw) != ERROR_SUCCESS)
		{
			g_padInUse = -1;
			g_padSearchedAt = now;
			return state;
		}
		state.present = true;
		Stick(raw.Gamepad.sThumbLX, raw.Gamepad.sThumbLY, XINPUT_GAMEPAD_LEFT_THUMB_DEADZONE, state.leftX, state.leftY);
		Stick(raw.Gamepad.sThumbRX, raw.Gamepad.sThumbRY, XINPUT_GAMEPAD_RIGHT_THUMB_DEADZONE, state.rightX, state.rightY);
		int delta = static_cast<int>(raw.Gamepad.bRightTrigger) - static_cast<int>(raw.Gamepad.bLeftTrigger);
		const int half = XINPUT_GAMEPAD_TRIGGER_THRESHOLD / 2;
		delta = delta > half ? delta - half : delta < -half ? delta + half : 0;
		state.triggers = delta / (255.0f - half);
		state.buttons = raw.Gamepad.wButtons;
		return state;
	}

	// The eased keys and sticks (camera task): each moves towards what is pushed by 1 every kEaseSeconds
	enum Axis { MoveX, MoveY, MoveZ, TurnYaw, TurnPitch, Roll, Zoom, kAxes };
	float g_axes[kAxes] = {};
	float g_mouseVelocity[2] = {}; // smoothed counts a second

	void Ease(float& value, float target, float dt)
	{
		const float step = dt / kEaseSeconds;
		if (value < target)
			value = (std::min)(target, value + step);
		else if (value > target)
			value = (std::max)(target, value - step);
	}

	// Runs a named command off the game's threads, for buttons and keys whose command may wait on a game thread
	void RunCommandAsync(const std::string& name, const std::string& argument)
	{
		std::thread([name, argument]()
		{
			DEBUG_MENU::CommandResult result;
			if (!DEBUG_MENU::RunCommand(name, argument, result))
				DevTools::Log("Debug menu: cinematic camera: no command %s in this build", name.c_str());
			else if (!result.ok)
				DevTools::Log("Debug menu: cinematic camera: %s: %s", name.c_str(), result.message.c_str());
		}).detach();
	}

	void ToggleSwitch(const char* name)
	{
		bool value = false;
		if (DEBUG_MENU::GetSwitch(name, value))
			DEBUG_MENU::SetSwitch(name, !value);
	}

	// ---- The player frozen while it flies (entity thread) ----

	bool Freeze(uint8_t* player)
	{
		__try
		{
			character_freeze(player, true);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	void Unfreeze(uint8_t* player)
	{
		__try
		{
			character_unfreeze(player);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
		}
	}

	// Entity thread: freezes the player or lets them go as wanted now (only the player it froze, in the level it froze
	// them in, and only if they are still frozen - the game may have let them go itself)
	void ReconcileFreeze()
	{
		if (!character_freeze || !character_unfreeze)
			return;
		const bool wanted = g_freezeWanted;
		const uint32_t root = RunningRoot();
		uint8_t* player = Player();
		if (wanted && !g_frozenPlayer && player && root)
		{
			uint8_t frozen = 1;
			uint32_t id = 0;
			if (Read(player + kCharacterFrozen, frozen) && !frozen && Read(player + kCharacterId, id) && Freeze(player))
			{
				g_frozenPlayer = player;
				g_frozenId = id;
				g_frozenRoot = root;
			}
		}
		else if (!wanted && g_frozenPlayer)
		{
			uint8_t frozen = 0;
			uint32_t id = 0;
			if (player == g_frozenPlayer && root == g_frozenRoot && Read(player + kCharacterId, id) && id == g_frozenId &&
				Read(player + kCharacterFrozen, frozen) && frozen)
				Unfreeze(player);
			g_frozenPlayer = nullptr;
		}
		g_playerFrozen = g_frozenPlayer != nullptr;
	}

	// ---- The camera task's frame ----

	LARGE_INTEGER g_lastTick = {};

	float FrameSeconds()
	{
		static LARGE_INTEGER frequency = {};
		if (!frequency.QuadPart)
			QueryPerformanceFrequency(&frequency);
		LARGE_INTEGER now;
		QueryPerformanceCounter(&now);
		float dt = g_lastTick.QuadPart ? static_cast<float>(static_cast<double>(now.QuadPart - g_lastTick.QuadPart) / frequency.QuadPart) : 0.0f;
		g_lastTick = now;
		return dt < 0.0f ? 0.0f : dt > 0.1f ? 0.1f : dt;
	}

	// Values set from outside (the tab, OpenCAGE, a profile) since the camera last wrote them: the camera takes them. Set
	// before it is first turned on, they apply then; untouched, it starts with the game camera's field of view.
	float g_fovWritten = 60.0f, g_rollWritten = 0.0f;

	// Under g_mutex: turns it on in this level, from where it was left or where the frame was drawn from
	bool TurnOn(uint32_t root)
	{
		const bool resume = g_camera.havePose && g_camera.root == root && !g_startFromGame;
		if (!resume)
		{
			const ULONGLONG now = GetTickCount64();
			if (!g_haveDrawn || now - g_drawnAt > 1000)
			{
				SetStatus("off: the game has not drawn a frame from its camera yet");
				return false;
			}
			g_camera.lockId = 0;
			g_lockWanted = 0;
			SetWorldPose(g_camera, g_drawn);
		}
		g_camera.on = true;
		g_camera.root = root;
		g_onAt = GetTickCount64();
		ZONE_LOADER::SetForced(ZONE_LOADER::Source::Cinematic, true);
		for (float& axis : g_axes)
			axis = 0.0f;
		g_mouseVelocity[0] = g_mouseVelocity[1] = 0.0f;
		g_mouseX = 0;
		g_mouseY = 0;
		g_wheel = 0;
		SetStatus(resume ? "on (where it was left)" : "on");
		return true;
	}

	// Under g_mutex
	void TurnOff(const std::string& why)
	{
		g_camera.on = false;
		ZONE_LOADER::SetForced(ZONE_LOADER::Source::Cinematic, false);
		SetStatus(why);
	}

	// Under g_mutex: locks to (or unlocks from) the character wanted, keeping the camera where it is in the world
	void FollowLock()
	{
		const uint32_t wanted = g_lockWanted;
		if (wanted == g_camera.lockId)
			return;
		// Where it is in the world now (from the character it was locked to; where it last was, if that one is gone)
		CINEMATIC_CAMERA::Pose pose;
		bool gone = false;
		if (!WorldPose(g_camera, pose, gone))
			pose = g_last;
		g_camera.lockId = wanted;
		if (!SetWorldPose(g_camera, pose))
		{
			g_camera.lockId = 0;
			g_lockWanted = 0;
			SetWorldPose(g_camera, pose);
			SetStatus("on: the character to lock to is gone");
			return;
		}
		SetStatus(wanted ? "on, locked to a character" : "on");
	}
}

bool CINEMATIC_CAMERA::CameraPose(Pose& out, bool mayApply)
{
	// Part of the debug menu: nothing without it (the camera hand-over hook can be in for Live Link or Possess alone)
	if (!Config::Get().debugMenu)
		return false;
	const float dt = FrameSeconds();

	// The camera's keys, the first time: any that another helper has too is left out (no key, never pressed)
	if (!g_keysChecked)
	{
		g_keysChecked = true;
		g_edges[EdgeToggle].key = CameraKey();
		for (int i = EdgeAddNode; i < kEdgeCount; i++)
			if (TakenKey(g_edges[i].key))
			{
				DevTools::Log("Debug menu: cinematic camera: %s is another helper's key: the camera leaves it alone", Config::KeyName(g_edges[i].key));
				g_edges[i].key = 0;
			}
	}

	// The camera's key, every frame (the window open or not); the focus is only looked at while it is on or wanted, or its
	// key was just pressed
	const bool active = g_active;
	const bool toggleDown = Down(g_edges[EdgeToggle].key);
	const bool togglePressed = toggleDown && !g_edges[EdgeToggle].was;
	g_edges[EdgeToggle].was = toggleDown;
	const bool focus = (active || g_wanted || togglePressed) ? GameHasFocus() : false;
	if (togglePressed && focus)
		g_wanted = !g_wanted;
	// The Debug Menu's window has the keys while it is open, unless the camera flies with it open (then only a text box in
	// it being typed in keeps them)
	const bool windowOpen = DEBUG_MENU::IsOpen() && (!g_flyWithMenu || DEBUG_MENU::TypingInWindow());
	const bool pauseMenu = (active || g_wanted) ? GameMenuOpen() : false;

	// The keys that act once per press, read only while it flies (otherwise taken as held, so a key already down as it
	// turns on doesn't act)
	if (active)
	{
		const bool flyingKeys = focus && !windowOpen && !pauseMenu && g_keepInput;
		const bool addNode = Pressed(EdgeAddNode, flyingKeys);
		const bool deleteNode = Pressed(EdgeDeleteNode, flyingKeys);
		const bool play = Pressed(EdgePlay, flyingKeys);
		const bool hud = Pressed(EdgeHud, flyingKeys);
		const bool pause = Pressed(EdgePause, flyingKeys);
		const bool step = Pressed(EdgeStep, flyingKeys);
		const bool faster = Pressed(EdgeFaster, flyingKeys);
		const bool slower = Pressed(EdgeSlower, flyingKeys);
		if (addNode) CINEMATIC_TRACKS::AddNodeHere();
		if (deleteNode) CINEMATIC_TRACKS::DeleteLastNode();
		if (play) CINEMATIC_TRACKS::TogglePlay();
		if (hud) ToggleSwitch("cinematic.hide_hud");
		if (pause) ToggleSwitch("game.paused");
		if (step) RunCommandAsync("game.step", "");
		if (faster) g_moveSpeed = (std::min)(100.0f, g_moveSpeed * 1.25f);
		if (slower) g_moveSpeed = (std::max)(0.05f, g_moveSpeed / 1.25f);
	}
	else
	{
		for (int i = EdgeAddNode; i < kEdgeCount; i++)
			g_edges[i].was = true;
	}

	const uint32_t root = RunningRoot();
	const bool possessing = POSSESS::Possessing();
	bool applied = false, cameraOn = false;
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		bool wanted = g_wanted;
		if (wanted && !root)
		{
			wanted = false;
			if (g_camera.on)
				TurnOff("off: the level ended");
			else
				SetStatus("off: no level is running");
		}
		else if (wanted && g_camera.on && root != g_camera.root)
		{
			wanted = false;
			g_camera.havePose = false;
			TurnOff("off: the level changed");
		}
		else if (wanted && possessing)
		{
			wanted = false;
			if (g_camera.on)
				TurnOff("off: a character was possessed (Possess has the camera and the keys)");
			else
				SetStatus("off: let go of the possessed character first (Possess has the camera and the keys)");
		}
		if (!wanted && g_camera.on)
			TurnOff("off");
		if (wanted && !g_camera.on && !TurnOn(root))
			wanted = false;
		if (!wanted)
			g_wanted = false;
		// A place asked for while it couldn't turn on is dropped, not kept for the next time it is turned on
		if (!g_camera.on)
			g_havePlace = false;
		cameraOn = g_camera.on;

		if (g_camera.on)
		{
			FollowLock();
			if (g_havePlace)
			{
				g_havePlace = false;
				if (!SetWorldPose(g_camera, g_place))
				{
					// Locked to a character that is gone: placed in the world instead
					g_camera.lockId = 0;
					g_lockWanted = 0;
					SetWorldPose(g_camera, g_place);
				}
			}

			// Values set from outside since the camera last wrote them
			if (g_fov != g_fovWritten)
				g_camera.fov = (std::max)(kFovMin, (std::min)(kFovMax, g_fov.load()));
			if (g_roll != g_rollWritten)
				g_camera.roll = g_roll / kDegrees;

			// What is pushed: the keys, the mouse and a pad, while the game window has the focus and the debug menu's
			// window is closed (it has the keys and mouse while open)
			float target[kAxes] = {};
			float mouse[2] = {};
			float wheel = 0.0f;
			float dofFocal = 0.0f, dofFocus = 0.0f, dofFnumber = 0.0f;
			const bool input = mayApply && focus && !windowOpen && !pauseMenu && g_keepInput;
			if (input)
			{
				target[MoveX] = (Down('D') ? 1.0f : 0.0f) - (Down('A') ? 1.0f : 0.0f);
				target[MoveY] = (Down(VK_SPACE) ? 1.0f : 0.0f) - (Down(VK_CONTROL) ? 1.0f : 0.0f);
				target[MoveZ] = (Down('W') ? 1.0f : 0.0f) - (Down('S') ? 1.0f : 0.0f);
				target[TurnYaw] = (Down(VK_RIGHT) ? 1.0f : 0.0f) - (Down(VK_LEFT) ? 1.0f : 0.0f);
				target[TurnPitch] = (Down(VK_DOWN) ? 1.0f : 0.0f) - (Down(VK_UP) ? 1.0f : 0.0f);
				target[Roll] = (Down('Q') ? 1.0f : 0.0f) - (Down('E') ? 1.0f : 0.0f);
				target[Zoom] = (Down(VK_ADD) ? 1.0f : 0.0f) - (Down(VK_SUBTRACT) ? 1.0f : 0.0f);
				dofFocus = (Down(VK_NUMPAD6) ? 1.0f : 0.0f) - (Down(VK_NUMPAD5) ? 1.0f : 0.0f);
				dofFocal = (Down(VK_NUMPAD8) ? 1.0f : 0.0f) - (Down(VK_NUMPAD9) ? 1.0f : 0.0f);
				dofFnumber = (Down(VK_NUMPAD3) ? 1.0f : 0.0f) - (Down(VK_NUMPAD2) ? 1.0f : 0.0f);
				if (Down(VK_SHIFT))
					for (int axis : { MoveX, MoveY, MoveZ })
						target[axis] *= 4.0f;
			}
			const long mouseX = g_mouseX.exchange(0), mouseY = g_mouseY.exchange(0), wheelCounts = g_wheel.exchange(0);
			if (input)
			{
				mouse[0] = static_cast<float>(mouseX);
				mouse[1] = static_cast<float>(mouseY);
				wheel = wheelCounts / static_cast<float>(WHEEL_DELTA);
			}
			const PadState pad = input ? ReadPad() : PadState();
			if (pad.present)
			{
				target[MoveX] += pad.leftX;
				target[MoveZ] += pad.leftY;
				target[MoveY] += pad.triggers;
				target[TurnYaw] += pad.rightX;
				target[TurnPitch] -= pad.rightY;
				target[Roll] += ((pad.buttons & XINPUT_GAMEPAD_LEFT_SHOULDER) ? 1.0f : 0.0f) - ((pad.buttons & XINPUT_GAMEPAD_RIGHT_SHOULDER) ? 1.0f : 0.0f);
				target[Zoom] += ((pad.buttons & XINPUT_GAMEPAD_LEFT_THUMB) ? 1.0f : 0.0f) - ((pad.buttons & XINPUT_GAMEPAD_RIGHT_THUMB) ? 1.0f : 0.0f);
				const float turnChange = ((pad.buttons & XINPUT_GAMEPAD_DPAD_UP) ? 1.0f : 0.0f) - ((pad.buttons & XINPUT_GAMEPAD_DPAD_DOWN) ? 1.0f : 0.0f);
				const float moveChange = ((pad.buttons & XINPUT_GAMEPAD_B) ? 1.0f : 0.0f) - ((pad.buttons & XINPUT_GAMEPAD_Y) ? 1.0f : 0.0f);
				if (turnChange != 0.0f)
					g_turnSpeed = (std::max)(1.0f, (std::min)(360.0f, g_turnSpeed + turnChange * 57.3f * dt));
				if (moveChange != 0.0f)
					g_moveSpeed = (std::max)(0.05f, (std::min)(100.0f, g_moveSpeed + moveChange * dt));
			}
			for (int axis = 0; axis < kAxes; axis++)
				Ease(g_axes[axis], target[axis], dt);

			// The mouse: counts this frame, or their pace smoothed over a few hundredths of a second
			const float sensitivity = g_mouseSensitivity * kMouseRadiansPerCount;
			float turnX = mouse[0] * sensitivity, turnY = mouse[1] * sensitivity;
			if (g_smoothMouse && dt > 0.0f)
			{
				const float blend = 1.0f - expf(-dt / kMouseSmoothSeconds);
				for (int i = 0; i < 2; i++)
					g_mouseVelocity[i] += (mouse[i] / dt - g_mouseVelocity[i]) * blend;
				turnX = g_mouseVelocity[0] * dt * sensitivity;
				turnY = g_mouseVelocity[1] * dt * sensitivity;
			}

			const float turn = g_turnSpeed / kDegrees, rollSpeed = g_rollSpeed / kDegrees;
			g_camera.yaw += g_axes[TurnYaw] * turn * dt + turnX;
			if (g_camera.yaw > kPi) g_camera.yaw -= 2.0f * kPi;
			if (g_camera.yaw < -kPi) g_camera.yaw += 2.0f * kPi;
			g_camera.pitch = (std::max)(-kPitchLimit, (std::min)(kPitchLimit, g_camera.pitch + g_axes[TurnPitch] * turn * dt + turnY));
			g_camera.roll += g_axes[Roll] * rollSpeed * dt;
			if (g_camera.roll > kPi) g_camera.roll -= 2.0f * kPi;
			if (g_camera.roll < -kPi) g_camera.roll += 2.0f * kPi;
			g_camera.fov = (std::max)(kFovMin, (std::min)(kFovMax, g_camera.fov + g_axes[Zoom] * g_fovSpeed * dt - wheel * kWheelDegreesPerNotch));

			// The depth of field's keys (with the Visuals tab's depth of field on), at Cinematic Tools' paces
			if ((dofFocal != 0.0f || dofFocus != 0.0f || dofFnumber != 0.0f) && CINEMATIC_VISUALS::DofOn())
			{
				CINEMATIC_VISUALS::Dof dof = CINEMATIC_VISUALS::GetDof();
				dof.focalLength = (std::max)(0.0f, dof.focalLength + dofFocal * 0.01f * dt);
				dof.focus = (std::max)(0.01f, dof.focus + dofFocus * dt);
				dof.fnumber = (std::max)(0.01f, dof.fnumber + dofFnumber * dt);
				CINEMATIC_VISUALS::SetDof(dof);
			}

			// Moving: along the camera's own right and forward, and the world's (or the locked character's) up
			const XMVECTOR rotation = Rotation(g_camera.yaw, g_camera.pitch, g_camera.roll);
			const XMMATRIX rows = XMMatrixRotationQuaternion(rotation);
			const float speed = g_moveSpeed * dt;
			XMVECTOR position = XMVectorSet(g_camera.position[0], g_camera.position[1], g_camera.position[2], 0.0f);
			const bool playing = CINEMATIC_TRACKS::Playing();
			position = XMVectorAdd(position, XMVectorScale(rows.r[0], g_axes[MoveX] * speed));
			position = XMVectorAdd(position, XMVectorScale(rows.r[2], g_axes[MoveZ] * speed));
			if (!playing)
				position = XMVectorAdd(position, XMVectorSet(0.0f, g_axes[MoveY] * speed, 0.0f, 0.0f));
			XMFLOAT3 moved;
			XMStoreFloat3(&moved, position);
			g_camera.position[0] = moved.x; g_camera.position[1] = moved.y; g_camera.position[2] = moved.z;

			// A track playing moves it in the world (unlocked), as its locks say; playing manually, up and down move along it
			Pose pose;
			bool gone = false;
			if (playing && g_camera.lockId)
			{
				WorldPose(g_camera, pose, gone);
				g_camera.lockId = 0;
				g_lockWanted = 0;
				SetWorldPose(g_camera, pose);
			}
			if (!WorldPose(g_camera, pose, gone) && gone)
			{
				// The character it was locked to is gone: it stays in the world where it last was
				g_camera.lockId = 0;
				g_lockWanted = 0;
				SetWorldPose(g_camera, g_last);
				WorldPose(g_camera, pose, gone);
				SetStatus("on: the character it was locked to is gone");
			}
			const float scrub = (std::max)(-1.0f, (std::min)(1.0f, g_axes[MoveY]));
			Pose played = pose;
			if (mayApply && CINEMATIC_TRACKS::Advance(dt, scrub, played))
			{
				// The camera stays where the track takes it (unlocked while it plays): its turning is only replaced when
				// the track's rotation is locked
				if (memcmp(played.rotation, pose.rotation, sizeof(pose.rotation)) != 0)
					SetWorldPose(g_camera, played);
				else
				{
					memcpy(g_camera.position, played.position, sizeof(g_camera.position));
					if (std::isfinite(played.fov) && played.fov > 0.0f)
						g_camera.fov = (std::max)(kFovMin, (std::min)(kFovMax, played.fov));
				}
				pose = played;
			}

			// A pose that can't be drawn from (a damaged track node, say) is never handed to the game: the camera stays
			// where it last was
			if (!Finite(pose.position, 3) || !Finite(pose.rotation, 4) || !std::isfinite(pose.fov))
			{
				g_camera.lockId = 0;
				g_lockWanted = 0;
				SetWorldPose(g_camera, g_last);
				pose = g_last;
			}

			g_fov = g_camera.fov;
			g_fovWritten = g_camera.fov;
			g_roll = g_camera.roll * kDegrees;
			g_rollWritten = g_roll;
			g_camera.havePose = true;
			if (mayApply)
			{
				g_last = pose;
				out = pose;
				applied = true;
			}
		}
	}

	// The player frozen while it is on and keeps the game's input (a frame it can't be drawn from - no camera chosen, a
	// cubemap capture - doesn't let the player go and freeze them again)
	const bool freeze = cameraOn && g_freezePlayer && g_keepInput;
	g_freezeWanted = freeze;
	if (freeze != g_freezeQueuedFor)
	{
		g_freezeQueuedFor = freeze;
		DEBUG_MENU::OnEntityThread(ReconcileFreeze);
	}
	if (applied)
		g_activeAt = GetTickCount64();
	g_active = applied;
	g_holdInput = applied && g_keepInput && !pauseMenu;
	return applied;
}

void CINEMATIC_CAMERA::FrameDrawn(const Pose& game, const Pose& drawn)
{
	std::lock_guard<std::mutex> lock(g_mutex);
	g_game = game;
	g_drawn = drawn;
	g_haveGame = g_haveDrawn = true;
	g_drawnAt = GetTickCount64();
}

bool CINEMATIC_CAMERA::OnWindowMessage(HWND window, UINT message, WPARAM wParam, LPARAM lParam, LRESULT& result)
{
	g_window = window;
	// While it flies the game's keyboard and mouse are kept from it (bar Escape, for the pause menu, and while one of the
	// game's menus is up), only while its window has the focus. The game takes its keys from their key messages, a key down
	// from its press until its release: only presses are kept, so no key is left down in the game, and RuntimeUtils' own
	// keys (acted on as they are released) and Alt's keys still work. The camera reads its keys from the keyboard itself,
	// so it loses none of them. Answered as the game answers them.
	if (message == WM_KEYDOWN && wParam != VK_ESCAPE && g_holdInput && CINEMATIC_CAMERA::Active() && !DEBUG_MENU::IsOpen() &&
		GetFocus() == window && !GameMenuOpen())
	{
		result = 0;
		return true;
	}
	// With the Debug Menu open (which keeps the keyboard and mouse from the game itself), and flying with it open: the mouse
	// turns the camera while its right button is held away from the window, and the wheel zooms away from it
	if (DEBUG_MENU::IsOpen())
	{
		if (message == WM_INPUT && g_flyWithMenu && g_holdInput && CINEMATIC_CAMERA::Active() && GetFocus() == window && !GameMenuOpen() &&
			!DEBUG_MENU::MouseOverWindow() && !DEBUG_MENU::TypingInWindow())
		{
			RAWINPUT input = {};
			UINT size = sizeof(input);
			if (GetRawInputData(reinterpret_cast<HRAWINPUT>(lParam), RID_INPUT, &input, &size, sizeof(RAWINPUTHEADER)) != static_cast<UINT>(-1) &&
				input.header.dwType == RIM_TYPEMOUSE)
			{
				if (!(input.data.mouse.usFlags & MOUSE_MOVE_ABSOLUTE) && Down(VK_RBUTTON))
				{
					g_mouseX += input.data.mouse.lLastX;
					g_mouseY += input.data.mouse.lLastY;
				}
				if (input.data.mouse.usButtonFlags & RI_MOUSE_WHEEL)
					g_wheel += static_cast<short>(input.data.mouse.usButtonData);
			}
		}
		return false;
	}
	// ... and its mouse, which the game reads as raw input: its movement and wheel turn and zoom the camera instead. The
	// keyboard's raw input, which the game leaves alone, goes with it.
	if (message != WM_INPUT || !g_holdInput || !CINEMATIC_CAMERA::Active() || DEBUG_MENU::IsOpen() || GetFocus() != window || GameMenuOpen())
		return false;
	RAWINPUT input = {};
	UINT size = sizeof(input);
	if (GetRawInputData(reinterpret_cast<HRAWINPUT>(lParam), RID_INPUT, &input, &size, sizeof(RAWINPUTHEADER)) == static_cast<UINT>(-1))
		return false;
	if (input.header.dwType == RIM_TYPEMOUSE)
	{
		if (!(input.data.mouse.usFlags & MOUSE_MOVE_ABSOLUTE))
		{
			g_mouseX += input.data.mouse.lLastX;
			g_mouseY += input.data.mouse.lLastY;
		}
		if (input.data.mouse.usButtonFlags & RI_MOUSE_WHEEL)
			g_wheel += static_cast<short>(input.data.mouse.usButtonData);
		result = DefWindowProc(window, message, wParam, lParam);
		return true;
	}
	if (input.header.dwType == RIM_TYPEKEYBOARD && input.data.keyboard.VKey != VK_ESCAPE)
	{
		result = DefWindowProc(window, message, wParam, lParam);
		return true;
	}
	return false;
}

bool CINEMATIC_CAMERA::Active()
{
	return g_active && GetTickCount64() - g_activeAt <= 1000;
}

bool CINEMATIC_CAMERA::Current(Pose& pose)
{
	if (!Active())
		return false;
	std::lock_guard<std::mutex> lock(g_mutex);
	pose = g_last;
	return true;
}

void CINEMATIC_CAMERA::Place(const Pose& pose)
{
	std::lock_guard<std::mutex> lock(g_mutex);
	g_place = pose;
	g_havePlace = true;
	g_wanted = true;
}

float CINEMATIC_CAMERA::Fov()
{
	return g_fov;
}

void CINEMATIC_CAMERA::LockTo(uint32_t characterId)
{
	g_lockWanted = characterId;
}

bool CINEMATIC_CAMERA::TakenKey(int key)
{
	const Config::Settings& config = Config::Get();
	return key != 0 && (key == config.debugMenuKey || (config.possess && key == config.possessKey) ||
		(config.unlockMouse && key == config.unlockMouseKey) || (config.hotReload && key == config.hotReloadKey));
}

int CINEMATIC_CAMERA::ToggleKey()
{
	return CameraKey();
}

__declspec(noinline)
void __fastcall CINEMATIC_CAMERA::h_gamepad_update(void* _this, void* /*_EDX*/)
{
	// The game's pad is not read while the camera flies and keeps the game's input: it keeps the state it had. One of the
	// game's menus up is looked at here too, as what keeps the input is only brought up to date while the camera runs.
	if (g_holdInput && g_keepGamepad && CINEMATIC_CAMERA::Active() && !GameMenuOpen())
		return;
	gamepad_update(_this);
}

namespace
{
	// ---- Profiles: the camera's settings by name, in OpenCAGE.CinematicProfiles.ini next to OpenCAGE.RuntimeUtils.ini ----

	std::string ProfilesPath()
	{
		char exePath[MAX_PATH] = {};
		GetModuleFileNameA(nullptr, exePath, MAX_PATH);
		std::string path = exePath;
		const size_t slash = path.find_last_of("\\/");
		path = (slash == std::string::npos) ? "" : path.substr(0, slash + 1);
		return path + "OpenCAGE.CinematicProfiles.ini";
	}

	bool ValidProfileName(const std::string& name)
	{
		if (name.empty() || name.size() > 64)
			return false;
		for (char c : name)
			if (c == '[' || c == ']' || c == '=' || c == ';' || static_cast<unsigned char>(c) < 32)
				return false;
		return true;
	}

	std::vector<std::string> ListProfiles()
	{
		std::vector<std::string> names;
		std::vector<char> buffer(16384);
		const DWORD length = GetPrivateProfileSectionNamesA(buffer.data(), static_cast<DWORD>(buffer.size()), ProfilesPath().c_str());
		for (DWORD at = 0; at < length && buffer[at];)
		{
			const std::string name(buffer.data() + at);
			names.push_back(name);
			at += static_cast<DWORD>(name.size()) + 1;
		}
		return names;
	}

	struct ProfileField
	{
		const char* key;
		std::atomic<float>* value; // null: the depth of field's
		int dof;                   // 0 focal length, 1 focus, 2 f-number
	};
	const ProfileField kProfileFields[] = {
		{ "MoveSpeed", &g_moveSpeed, -1 }, { "TurnSpeed", &g_turnSpeed, -1 }, { "RollSpeed", &g_rollSpeed, -1 },
		{ "FovSpeed", &g_fovSpeed, -1 }, { "Fov", &g_fov, -1 }, { "MouseSensitivity", &g_mouseSensitivity, -1 },
		{ "DofFocalLength", nullptr, 0 }, { "DofFocus", nullptr, 1 }, { "DofFNumber", nullptr, 2 },
	};

	bool SaveProfile(const std::string& name)
	{
		if (!ValidProfileName(name))
			return false;
		const std::string path = ProfilesPath();
		const CINEMATIC_VISUALS::Dof dof = CINEMATIC_VISUALS::GetDof();
		bool ok = true;
		for (const ProfileField& field : kProfileFields)
		{
			const float value = field.value ? field.value->load() : field.dof == 0 ? dof.focalLength : field.dof == 1 ? dof.focus : dof.fnumber;
			ok &= WritePrivateProfileStringA(name.c_str(), field.key, Number(value, 4).c_str(), path.c_str()) != FALSE;
		}
		return ok;
	}

	bool LoadProfile(const std::string& name)
	{
		const std::vector<std::string> names = ListProfiles();
		if (std::find(names.begin(), names.end(), name) == names.end())
			return false;
		const std::string path = ProfilesPath();
		CINEMATIC_VISUALS::Dof dof = CINEMATIC_VISUALS::GetDof();
		for (const ProfileField& field : kProfileFields)
		{
			char text[64] = {};
			GetPrivateProfileStringA(name.c_str(), field.key, "", text, sizeof(text), path.c_str());
			float value = 0.0f;
			int count = 0;
			if (!text[0] || !ParseNumbers(text, &value, 1, count) || count != 1)
				continue;
			if (field.value)
				DEBUG_MENU::SetValue(std::string("cinematic.") + (field.value == &g_moveSpeed ? "move_speed" : field.value == &g_turnSpeed ? "turn_speed" :
					field.value == &g_rollSpeed ? "roll_speed" : field.value == &g_fovSpeed ? "fov_speed" : field.value == &g_fov ? "fov" : "mouse_sensitivity"), value);
			else if (field.dof == 0)
				dof.focalLength = (std::max)(0.0f, value);
			else if (field.dof == 1)
				dof.focus = (std::max)(0.01f, value);
			else
				dof.fnumber = (std::max)(0.01f, value);
		}
		CINEMATIC_VISUALS::SetDof(dof);
		return true;
	}

	bool DeleteProfile(const std::string& name)
	{
		return ValidProfileName(name) && WritePrivateProfileStringA(name.c_str(), nullptr, nullptr, ProfilesPath().c_str()) != FALSE;
	}

	// ---- Commands and the tab ----

	std::string Trim(const std::string& text)
	{
		size_t start = text.find_first_not_of(" \t\r\n"), end = text.find_last_not_of(" \t\r\n");
		return start == std::string::npos ? std::string() : text.substr(start, end - start + 1);
	}

	bool ContainsIgnoringCase(const std::string& text, const std::string& part)
	{
		if (part.empty())
			return false;
		auto lower = [](std::string value) { for (char& c : value) c = static_cast<char>(tolower(static_cast<unsigned char>(c))); return value; };
		return lower(text).find(lower(part)) != std::string::npos;
	}

	// Render thread: the character named (by id, or the first whose name or class has the text in it)
	const DEBUG_MENU::Character* FindByName(const std::string& text)
	{
		const std::vector<DEBUG_MENU::Character>& characters = DEBUG_MENU::Characters();
		char* end = nullptr;
		const unsigned long id = strtoul(text.c_str(), &end, 0);
		if (end && *end == '\0' && !text.empty())
			for (const DEBUG_MENU::Character& character : characters)
				if (character.id == id)
					return &character;
		for (int pass = 0; pass < 2; pass++)
			for (const DEBUG_MENU::Character& character : characters)
				if (ContainsIgnoringCase(pass == 0 ? character.name : std::string(DEBUG_MENU::ClassName(character.characterClass)), text))
					return &character;
		return nullptr;
	}

	// A pose 3 m behind a character and a little above, looking at it (from where it stands and its facing)
	CINEMATIC_CAMERA::Pose PoseBehind(const float at[3], float facing)
	{
		const float distance = 3.0f, height = 1.8f, lookHeight = 1.2f;
		const float forward[3] = { sinf(facing), 0.0f, cosf(facing) };
		float position[3];
		for (int i = 0; i < 3; i++)
			position[i] = at[i] - forward[i] * distance + (i == 1 ? height : 0.0f);
		const float to[3] = { at[0] - position[0], at[1] + lookHeight - position[1], at[2] - position[2] };
		const float yaw = atan2f(to[0], to[2]) * kDegrees;
		const float pitch = -atan2f(to[1], sqrtf(to[0] * to[0] + to[2] * to[2])) * kDegrees;
		return PoseFrom(position, yaw, pitch, 0.0f, CINEMATIC_CAMERA::Fov());
	}

	CINEMATIC_CAMERA::Pose BehindCharacter(const DEBUG_MENU::Character& character)
	{
		return PoseBehind(character.position, character.facing);
	}

	std::string StatusText()
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		return g_status;
	}

	std::string PoseText(const CINEMATIC_CAMERA::Pose& pose)
	{
		float forward[3], up[3];
		Axes(pose, forward, up);
		float yaw, pitch, roll;
		Angles(XMVectorSet(pose.rotation[0], pose.rotation[1], pose.rotation[2], pose.rotation[3]), yaw, pitch, roll);
		return "position=" + Number(pose.position[0]) + "," + Number(pose.position[1]) + "," + Number(pose.position[2]) +
			"\nforward=" + Number(forward[0], 4) + "," + Number(forward[1], 4) + "," + Number(forward[2], 4) +
			"\nup=" + Number(up[0], 4) + "," + Number(up[1], 4) + "," + Number(up[2], 4) +
			"\nyaw=" + Number(yaw * kDegrees, 2) + "\npitch=" + Number(pitch * kDegrees, 2) + "\nroll=" + Number(roll * kDegrees, 2) +
			"\nfov=" + Number(pose.fov, 2);
	}

	const char* kKeysText =
		"W/A/S/D fly, Space/Ctrl up/down, Shift x4 faster, Page Up/Down change the speed\n"
		"Mouse or arrows turn, Q/E roll, wheel or numpad +/- field of view\n"
		"Numpad 6/5 focus distance, 8/9 focal length, 3/2 f-number (depth of field on)\n"
		"F1 add a track node, F2 delete the last, F3 play/stop the track\n"
		"Home hide the HUD, Delete pause the game, End step one frame while paused\n"
		"Pad: left stick flies, triggers up/down, right stick turns, LB/RB roll,\n"
		"stick clicks field of view, d-pad up/down turn speed, B/Y move speed";

	void Name()
	{
		DEBUG_MENU::AddSwitch("cinematic.camera", &g_wanted);
		DEBUG_MENU::AddSwitch("cinematic.keep_input", &g_keepInput);
		DEBUG_MENU::AddSwitch("cinematic.fly_with_menu", &g_flyWithMenu);
		DEBUG_MENU::AddSwitch("cinematic.keep_gamepad", &g_keepGamepad);
		DEBUG_MENU::AddSwitch("cinematic.freeze_player", &g_freezePlayer);
		DEBUG_MENU::AddSwitch("cinematic.smooth_mouse", &g_smoothMouse);
		DEBUG_MENU::AddSwitch("cinematic.start_from_game", &g_startFromGame);
		DEBUG_MENU::AddValue("cinematic.move_speed", &g_moveSpeed, 0.05f, 100.0f);
		DEBUG_MENU::AddValue("cinematic.turn_speed", &g_turnSpeed, 1.0f, 360.0f);
		DEBUG_MENU::AddValue("cinematic.roll_speed", &g_rollSpeed, 1.0f, 360.0f);
		DEBUG_MENU::AddValue("cinematic.fov_speed", &g_fovSpeed, 0.5f, 90.0f);
		DEBUG_MENU::AddValue("cinematic.mouse_sensitivity", &g_mouseSensitivity, 0.01f, 4.0f);
		DEBUG_MENU::AddValue("cinematic.fov", &g_fov, kFovMin, kFovMax);
		DEBUG_MENU::AddValue("cinematic.roll", &g_roll, -180.0f, 180.0f);
		DEBUG_MENU::AddValue("cinematic.pad", &g_pad, -1.0f, 3.0f);

		DEBUG_MENU::AddCommand("cinematic.status", "the cinematic camera: on, why not, its lock, and the pose the last frame was drawn from", [](const std::string&)
		{
			const bool active = CINEMATIC_CAMERA::Active();
			CINEMATIC_CAMERA::Pose pose;
			bool drawn = false;
			std::string text = std::string("on=") + (active ? "1" : "0") + "\nstatus=" + StatusText();
			{
				std::lock_guard<std::mutex> lock(g_mutex);
				text += "\nlocked=" + std::to_string(g_camera.lockId);
				// While it is on: the pose the game drew the last frame from, as read back from its camera once the cinematic
				// pose was written (a track's place, and its field of view when the track's is locked), else the pose last
				// handed over
				if (active)
				{
					drawn = g_haveDrawn && GetTickCount64() - g_drawnAt <= 1000;
					pose = drawn ? g_drawn : g_last;
				}
			}
			text += std::string("\ntrack_playing=") + (CINEMATIC_TRACKS::Playing() ? "1" : "0");
			text += std::string("\nplayer_frozen=") + (g_playerFrozen ? "1" : "0") + "\nkeeping_input=" + (g_holdInput ? "1" : "0");
			text += std::string("\nkey=") + Config::KeyName(CameraKey());
			if (active)
				text += std::string("\npose=") + (drawn ? "drawn" : "handed over") + "\n" + PoseText(pose);
			return DEBUG_MENU::CommandResult{ true, text };
		});
		DEBUG_MENU::AddCommand("cinematic.camera_get", "the cinematic camera's pose (position, forward, up, yaw, pitch, roll, fov), or the game camera's while it is off", [](const std::string&)
		{
			CINEMATIC_CAMERA::Pose pose;
			if (CINEMATIC_CAMERA::Current(pose))
				return DEBUG_MENU::CommandResult{ true, "camera=cinematic\n" + PoseText(pose) };
			std::lock_guard<std::mutex> lock(g_mutex);
			if (!g_haveGame || GetTickCount64() - g_drawnAt > 1000)
				return DEBUG_MENU::CommandResult{ false, "no camera drawn yet (no level running?)" };
			return DEBUG_MENU::CommandResult{ true, "camera=game\n" + PoseText(g_game) };
		});
		DEBUG_MENU::AddCommand("cinematic.camera_set", "\"x,y,z[,yaw,pitch,roll[,fov]]\" (degrees): turns the cinematic camera on there", [](const std::string& argument)
		{
			float numbers[7];
			int count = 0;
			if (!ParseNumbers(argument, numbers, 7, count) || (count != 3 && count != 6 && count != 7))
				return DEBUG_MENU::CommandResult{ false, "give x,y,z or x,y,z,yaw,pitch,roll or x,y,z,yaw,pitch,roll,fov" };
			float yaw = 0.0f, pitch = 0.0f, roll = 0.0f;
			CINEMATIC_CAMERA::Pose current;
			if (count == 3)
			{
				std::lock_guard<std::mutex> lock(g_mutex);
				const CINEMATIC_CAMERA::Pose& from = CINEMATIC_CAMERA::Active() ? g_last : g_drawn;
				float y, p, r;
				Angles(XMVectorSet(from.rotation[0], from.rotation[1], from.rotation[2], from.rotation[3]), y, p, r);
				yaw = y * kDegrees; pitch = p * kDegrees; roll = r * kDegrees;
			}
			else
			{
				yaw = numbers[3]; pitch = numbers[4]; roll = numbers[5];
			}
			const float fov = count == 7 ? numbers[6] : CINEMATIC_CAMERA::Fov();
			CINEMATIC_CAMERA::Place(PoseFrom(numbers, yaw, pitch, roll, fov));
			return DEBUG_MENU::CommandResult{ true, "placed (at the camera's next frame; cinematic.status says whether it is on)" };
		});
		DEBUG_MENU::AddCommand("cinematic.reset", "puts the cinematic camera back where the game's own camera is", [](const std::string&)
		{
			CINEMATIC_CAMERA::Pose game;
			{
				std::lock_guard<std::mutex> lock(g_mutex);
				if (!g_haveGame)
					return DEBUG_MENU::CommandResult{ false, "the game has not drawn a frame from its camera yet" };
				game = g_game;
			}
			g_lockWanted = 0;
			CINEMATIC_CAMERA::Place(game);
			return DEBUG_MENU::CommandResult{ true, "back at the game camera" };
		});
		DEBUG_MENU::AddCommand("cinematic.lock", "\"name or id\": locks the cinematic camera to that character (moves with it); empty unlocks", [](const std::string& argument)
		{
			const std::string name = Trim(argument);
			if (name.empty())
			{
				g_lockWanted = 0;
				return DEBUG_MENU::CommandResult{ true, "unlocked" };
			}
			return DEBUG_MENU::AnswerOnRenderThread([name]()
			{
				const DEBUG_MENU::Character* character = FindByName(name);
				if (!character)
					return DEBUG_MENU::CommandResult{ false, "no character called " + name };
				g_lockWanted = character->id;
				return DEBUG_MENU::CommandResult{ true, "locked to " + character->name + " (" + std::to_string(character->id) + ")" };
			});
		});
		DEBUG_MENU::AddCommand("cinematic.go_to", "\"name or id\": the cinematic camera behind that character, looking at it", [](const std::string& argument)
		{
			const std::string name = Trim(argument);
			return DEBUG_MENU::AnswerOnRenderThread([name]()
			{
				const DEBUG_MENU::Character* character = name.empty() ? DEBUG_MENU::Selected() : FindByName(name);
				if (!character)
					return DEBUG_MENU::CommandResult{ false, name.empty() ? "no character selected" : "no character called " + name };
				CINEMATIC_CAMERA::Place(BehindCharacter(*character));
				return DEBUG_MENU::CommandResult{ true, "behind " + character->name };
			});
		});
		DEBUG_MENU::AddCommand("cinematic.player_to_camera", "moves the player to where the cinematic camera is (player.teleport)", [](const std::string&)
		{
			CINEMATIC_CAMERA::Pose pose;
			if (!CINEMATIC_CAMERA::Current(pose))
				return DEBUG_MENU::CommandResult{ false, "the cinematic camera is off" };
			DEBUG_MENU::CommandResult result;
			const std::string where = Number(pose.position[0]) + "," + Number(pose.position[1]) + "," + Number(pose.position[2]);
			if (!DEBUG_MENU::RunCommand("player.teleport", where, result))
				return DEBUG_MENU::CommandResult{ false, "player.teleport is not in this build" };
			return result;
		});
		DEBUG_MENU::AddCommand("cinematic.profile_save", "\"name\": saves the camera's speeds, field of view and depth of field as a profile", [](const std::string& argument)
		{
			const std::string name = Trim(argument);
			if (!SaveProfile(name))
				return DEBUG_MENU::CommandResult{ false, "could not save profile \"" + name + "\" (a name without [ ] = ; up to 64 characters)" };
			return DEBUG_MENU::CommandResult{ true, "saved " + name + " to " + ProfilesPath() };
		});
		DEBUG_MENU::AddCommand("cinematic.profile_load", "\"name\": loads a camera profile", [](const std::string& argument)
		{
			const std::string name = Trim(argument);
			if (!LoadProfile(name))
				return DEBUG_MENU::CommandResult{ false, "no profile called \"" + name + "\"" };
			return DEBUG_MENU::CommandResult{ true, "loaded " + name };
		});
		DEBUG_MENU::AddCommand("cinematic.profile_delete", "\"name\": deletes a camera profile", [](const std::string& argument)
		{
			const std::string name = Trim(argument);
			const std::vector<std::string> names = ListProfiles();
			if (std::find(names.begin(), names.end(), name) == names.end() || !DeleteProfile(name))
				return DEBUG_MENU::CommandResult{ false, "no profile called \"" + name + "\"" };
			return DEBUG_MENU::CommandResult{ true, "deleted " + name };
		});
		DEBUG_MENU::AddCommand("cinematic.list_profiles", "the camera profiles saved, one a line", [](const std::string&)
		{
			std::string text;
			for (const std::string& name : ListProfiles())
				text += name + "\n";
			return DEBUG_MENU::CommandResult{ true, text };
		});
		DEBUG_MENU::AddCommand("cinematic.keys", "the cinematic camera's keys", [](const std::string&)
		{
			return DEBUG_MENU::CommandResult{ true, std::string("toggle: ") + KeyText(CameraKey()) + "\n" + kKeysText };
		});
	}

	const bool g_named = (Name(), true);

	void AttachHooks(bool attach)
	{
		// The game's pad reading (the debug menu's hooks are attached once, when the debug menu is on)
		if (!CINEMATIC_CAMERA::gamepad_update)
			return;
		if (attach)
			DEVTOOLS_DETOURS_ATTACH(CINEMATIC_CAMERA::gamepad_update, CINEMATIC_CAMERA::h_gamepad_update);
		else
			DEVTOOLS_DETOURS_DETACH(CINEMATIC_CAMERA::gamepad_update, CINEMATIC_CAMERA::h_gamepad_update);
	}
	const bool g_hooks = DEBUG_MENU::AddHooks(AttachHooks);

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

	// A slider's grab drawn see-through (until PopSeeThroughGrab), so the value written across the slider stays readable
	// where the grab passes over it
	void PushSeeThroughGrab()
	{
		ImVec4 grab = ImGui::GetStyleColorVec4(ImGuiCol_SliderGrab);
		ImVec4 active = ImGui::GetStyleColorVec4(ImGuiCol_SliderGrabActive);
		grab.w *= 0.35f;
		active.w *= 0.5f;
		ImGui::PushStyleColor(ImGuiCol_SliderGrab, grab);
		ImGui::PushStyleColor(ImGuiCol_SliderGrabActive, active);
	}

	void PopSeeThroughGrab()
	{
		ImGui::PopStyleColor(2);
	}

	bool SliderValue(const char* label, std::atomic<float>& value, float min, float max, const char* format, bool logarithmic = false)
	{
		float v = value;
		ImGui::SetNextItemWidth(220.0f * DEBUG_MENU::Scale());
		PushSeeThroughGrab();
		const bool changed = ImGui::SliderFloat(label, &v, min, max, format, ImGuiSliderFlags_AlwaysClamp | (logarithmic ? ImGuiSliderFlags_Logarithmic : 0));
		PopSeeThroughGrab();
		if (changed)
			value = v;
		return changed;
	}

	bool Checkbox(const char* label, std::atomic<bool>& value)
	{
		bool v = value;
		if (ImGui::Checkbox(label, &v))
		{
			value = v;
			return true;
		}
		return false;
	}

	char g_profileName[65] = "";
	int g_profileSelected = -1;
	std::vector<std::string> g_profiles;
	ULONGLONG g_profilesReadAt = 0;
	std::string g_tabNote;

	void DrawTime()
	{
		ImGui::SeparatorText("Time");
		bool paused = false;
		float speed = 1.0f;
		const bool havePause = DEBUG_MENU::GetSwitch("game.paused", paused);
		const bool haveSpeed = DEBUG_MENU::GetValue("game.speed", speed);
		if (!havePause && !haveSpeed)
		{
			ImGui::TextDisabled("The game's pause and speed (the Game group's) are not in this build.");
			return;
		}
		ImGui::BeginDisabled(!havePause);
		if (ImGui::Checkbox("Freeze time (pause the game)", &paused))
			DEBUG_MENU::SetSwitch("game.paused", paused);
		Tooltip("The game's own pause (game.paused): the world stops, the camera still flies. Nothing streams in while paused, so fly where you want before pausing. Delete toggles it while the camera flies.");
		ImGui::SameLine();
		ImGui::BeginDisabled(!paused);
		if (ImGui::Button("Step one frame"))
			RunCommandAsync("game.step", "");
		ImGui::EndDisabled();
		Tooltip("The game moves on one frame and pauses again (game.step). End does this while the camera flies.");
		ImGui::EndDisabled();
		ImGui::BeginDisabled(!haveSpeed);
		ImGui::SetNextItemWidth(220.0f * DEBUG_MENU::Scale());
		PushSeeThroughGrab();
		if (ImGui::SliderFloat("Time scale", &speed, 0.05f, 4.0f, "%.2fx", ImGuiSliderFlags_Logarithmic | ImGuiSliderFlags_AlwaysClamp))
			DEBUG_MENU::SetValue("game.speed", speed);
		PopSeeThroughGrab();
		ImGui::SameLine();
		if (ImGui::Button("1x"))
			DEBUG_MENU::SetValue("game.speed", 1.0f);
		Tooltip("The game clock's own time scale (game.speed), as Cinematic Tools' timescale: slow motion and fast forward. Camera tracks play in real time whatever it is.");
		ImGui::EndDisabled();
	}

	void DrawProfiles()
	{
		ImGui::SeparatorText("Profiles");
		const ULONGLONG now = GetTickCount64();
		if (now - g_profilesReadAt > 2000)
		{
			g_profiles = ListProfiles();
			g_profilesReadAt = now;
		}
		ImGui::SetNextItemWidth(180.0f * DEBUG_MENU::Scale());
		const char* preview = g_profileSelected >= 0 && g_profileSelected < static_cast<int>(g_profiles.size()) ? g_profiles[g_profileSelected].c_str() : "(choose)";
		if (ImGui::BeginCombo("##profile", preview))
		{
			for (int i = 0; i < static_cast<int>(g_profiles.size()); i++)
				if (ImGui::Selectable(g_profiles[i].c_str(), i == g_profileSelected))
					g_profileSelected = i;
			ImGui::EndCombo();
		}
		ImGui::SameLine();
		const bool chosen = g_profileSelected >= 0 && g_profileSelected < static_cast<int>(g_profiles.size());
		ImGui::BeginDisabled(!chosen);
		if (ImGui::Button("Load"))
			g_tabNote = LoadProfile(g_profiles[g_profileSelected]) ? "loaded " + g_profiles[g_profileSelected] : "could not load it";
		ImGui::SameLine();
		if (ImGui::Button("Delete##profile"))
		{
			g_tabNote = DeleteProfile(g_profiles[g_profileSelected]) ? "deleted " + g_profiles[g_profileSelected] : "could not delete it";
			g_profileSelected = -1;
			g_profilesReadAt = 0;
		}
		ImGui::EndDisabled();
		ImGui::SetNextItemWidth(180.0f * DEBUG_MENU::Scale());
		ImGui::InputTextWithHint("##profilename", "profile name", g_profileName, sizeof(g_profileName));
		ImGui::SameLine();
		if (ImGui::Button("Save profile"))
		{
			g_tabNote = SaveProfile(g_profileName) ? std::string("saved ") + g_profileName : "give it a name without [ ] = ;";
			g_profilesReadAt = 0;
		}
		Tooltip("Saves the speeds, the mouse sensitivity, the field of view and the depth of field as a profile, in OpenCAGE.CinematicProfiles.ini next to OpenCAGE.RuntimeUtils.ini (as Cinematic Tools' camera profiles).");
	}

	void DrawTab()
	{
		const bool active = CINEMATIC_CAMERA::Active();
		bool on = g_wanted;
		char label[96];
		sprintf_s(label, "Cinematic camera (%s)", KeyText(CameraKey()));
		if (ImGui::Checkbox(label, &on))
			g_wanted = on;
		Tooltip("Turns the free camera on and off; its key does too, with this window open or closed (CinematicCameraKey in OpenCAGE.RuntimeUtils.ini). Close this window to fly it - the window has the keys and mouse while it is open - or tick \"Fly with this window open\" below.");
		ImGui::SameLine();
		ImGui::PushTextWrapPos(0.0f);
		ImGui::TextDisabled("%s", StatusText().c_str());
		ImGui::PopTextWrapPos();

		ImGui::BeginDisabled(!active);
		if (ImGui::Button("Back to the game camera"))
		{
			DEBUG_MENU::CommandResult result;
			DEBUG_MENU::RunCommand("cinematic.reset", "", result);
		}
		ImGui::SameLine();
		if (ImGui::Button("Move the player here"))
		{
			CINEMATIC_CAMERA::Pose pose;
			if (CINEMATIC_CAMERA::Current(pose))
				RunCommandAsync("player.teleport", Number(pose.position[0]) + "," + Number(pose.position[1]) + "," + Number(pose.position[2]));
		}
		Tooltip("The player is moved to where the camera is (the Characters group's teleport, player.teleport).");
		ImGui::EndDisabled();
		ImGui::SameLine();
		const DEBUG_MENU::Character* selected = DEBUG_MENU::Selected();
		ImGui::BeginDisabled(!selected);
		if (ImGui::Button("Go to the selected character") && selected)
			CINEMATIC_CAMERA::Place(BehindCharacter(*selected));
		ImGui::EndDisabled();
		Tooltip("Turns the camera on behind the character selected (the Characters group, the Cast tab or the picker below), looking at it.");

		CINEMATIC_CAMERA::Pose pose;
		if (CINEMATIC_CAMERA::Current(pose))
		{
			float yaw, pitch, roll;
			Angles(XMVectorSet(pose.rotation[0], pose.rotation[1], pose.rotation[2], pose.rotation[3]), yaw, pitch, roll);
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextDisabled("At %.2f, %.2f, %.2f - yaw %.1f, pitch %.1f, roll %.1f deg, field of view %.1f deg", pose.position[0], pose.position[1], pose.position[2],
				yaw * kDegrees, pitch * kDegrees, roll * kDegrees, pose.fov);
			ImGui::PopTextWrapPos();
		}

		ImGui::SeparatorText("Lens");
		SliderValue("Field of view", g_fov, kFovMin, kFovMax, "%.1f deg");
		Tooltip("Vertical, in degrees. The mouse wheel and the numeric pad's + and - change it while flying.");
		SliderValue("Roll", g_roll, -180.0f, 180.0f, "%.1f deg");
		ImGui::SameLine();
		if (ImGui::Button("Level"))
			g_roll = 0.0f;
		Tooltip("Q and E roll it while flying.");

		ImGui::SeparatorText("Lock to a character");
		// (no label: the choice shows "Character: N" itself, under this heading)
		ImGui::SetNextItemWidth(320.0f * DEBUG_MENU::Scale());
		DEBUG_MENU::CharacterPicker("##cinematiccharacter");
		Tooltip("The character to lock to: the same one selected in the Characters group and the Cast tab.");
		uint32_t lockedId = 0;
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			lockedId = g_camera.lockId;
		}
		bool locked = g_lockWanted != 0;
		ImGui::BeginDisabled(!selected && !locked);
		if (ImGui::Checkbox("Lock to the selected character", &locked))
			g_lockWanted = locked && selected ? selected->id : 0;
		ImGui::EndDisabled();
		Tooltip("The camera moves with the character, kept where it is relative to the character's position and facing (Cinematic Tools' \"Lock to character\"). Flying moves it relative to the character. A track playing unlocks it.");
		if (lockedId)
		{
			const DEBUG_MENU::Character* target = nullptr;
			for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
				if (character.id == lockedId)
					target = &character;
			ImGui::SameLine();
			ImGui::TextDisabled("locked to %s", target ? target->name.c_str() : "a character");
		}

		ImGui::SeparatorText("Flying");
		SliderValue("Move speed", g_moveSpeed, 0.05f, 100.0f, "%.2f m/s", true);
		SliderValue("Turn speed", g_turnSpeed, 1.0f, 360.0f, "%.0f deg/s", true);
		Tooltip("For the arrow keys and the pad's right stick.");
		SliderValue("Roll speed", g_rollSpeed, 1.0f, 360.0f, "%.1f deg/s", true);
		SliderValue("Field of view speed", g_fovSpeed, 0.5f, 90.0f, "%.1f deg/s", true);
		SliderValue("Mouse sensitivity", g_mouseSensitivity, 0.01f, 4.0f, "%.2f", true);
		Checkbox("Smooth mouse", g_smoothMouse);
		Tooltip("The mouse's turning is smoothed over a few hundredths of a second (Cinematic Tools' \"Smooth mouse\").");
		ImGui::SameLine();
		Checkbox("Start from the game camera", g_startFromGame);
		Tooltip("Each time it is turned on it starts where the game's camera is (Cinematic Tools' \"Reset camera automatically\"). Off, it goes back to where it was left in this level.");
		Checkbox("Keep the game's input", g_keepInput);
		Tooltip("On: the keys, mouse and pad fly the camera and are kept from the game (bar Escape). Off: the game has them - play on while the camera holds where it is, follows its track or its character.");
		ImGui::SameLine();
		ImGui::BeginDisabled(!g_keepInput);
		Checkbox("and its gamepad", g_keepGamepad);
		Tooltip("The game's gamepad is not read while the camera flies (Cinematic Tools' \"Disable player gamepad input\").");
		ImGui::SameLine();
		Checkbox("Freeze the player", g_freezePlayer);
		Tooltip("The player is frozen while the camera flies (the game's own freeze, as its scripts freeze a character).");
		Checkbox("Fly with this window open", g_flyWithMenu);
		Tooltip("On: the keys keep flying the camera while the Debug Menu is open (not while you type in one of its boxes), the mouse turns it while you hold the right button away from the window, and the wheel zooms away from the window. Off: the window has the keys and mouse while it is open.");
		ImGui::EndDisabled();
		float pad = g_pad;
		ImGui::SetNextItemWidth(120.0f * DEBUG_MENU::Scale());
		int padIndex = static_cast<int>(pad);
		PushSeeThroughGrab();
		if (ImGui::SliderInt("Gamepad", &padIndex, -1, 3, padIndex < 0 ? "first connected" : "pad %d"))
			g_pad = static_cast<float>(padIndex);
		PopSeeThroughGrab();
		Tooltip("Which XInput pad flies the camera (Cinematic Tools' controller id).");
		if (Config::Get().unlockMouse)
		{
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextColored(ImVec4(1.0f, 0.75f, 0.3f, 1.0f), "Unlock Mouse is on: while the cursor is free the mouse doesn't reach the camera - press %s in the game to turn it with the mouse.", Config::KeyName(Config::Get().unlockMouseKey));
			ImGui::PopTextWrapPos();
		}

		DrawTime();
		DrawProfiles();
		if (ImGui::CollapsingHeader("Keys"))
		{
			if (CameraKey())
				ImGui::Text("%s turns it on and off.", Config::KeyName(CameraKey()));
			else
				ImGui::TextWrapped("No key turns it on and off: CinematicCameraKey is another helper's key (set another in OpenCAGE.RuntimeUtils.ini).");
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextUnformatted(kKeysText);
			ImGui::PopTextWrapPos();
		}
		if (!g_tabNote.empty())
		{
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextDisabled("Last: %s", g_tabNote.c_str());
			ImGui::PopTextWrapPos();
		}
	}

	// A note over the game for a few seconds after it is turned on, with the window closed
	void DrawOverlay(ImDrawList* drawList)
	{
		if (!CINEMATIC_CAMERA::Active() || DEBUG_MENU::IsOpen())
			return;
		ULONGLONG onAt;
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			onAt = g_onAt;
		}
		const ULONGLONG shown = GetTickCount64() - onAt;
		if (shown > 4000)
			return;
		char text[128];
		const int key = CameraKey();
		const int length = key ? sprintf_s(text, "Cinematic camera - %s to leave", Config::KeyName(key)) : sprintf_s(text, "Cinematic camera - the Free Camera tab turns it off");
		if (Config::Get().unlockMouse && length > 0)
			sprintf_s(text + length, sizeof(text) - length, " - %s gives it the mouse", Config::KeyName(Config::Get().unlockMouseKey));
		const ImVec2 size = ImGui::CalcTextSize(text);
		const float width = ImGui::GetIO().DisplaySize.x;
		const int alpha = shown > 3000 ? static_cast<int>(220 * (4000 - shown) / 1000) : 220;
		// Its box (4 pixels around the text) goes under the notes already along the top of the screen
		const ImVec2 at((width - size.x) * 0.5f, DEBUG_MENU::BannerTop(size.y + 8.0f) + 4.0f);
		drawList->AddRectFilled(ImVec2(at.x - 8.0f, at.y - 4.0f), ImVec2(at.x + size.x + 8.0f, at.y + size.y + 4.0f), IM_COL32(0, 0, 0, alpha / 2), 4.0f);
		drawList->AddText(at, IM_COL32(240, 200, 120, alpha), text);
	}

	const bool g_tabAdded = DEBUG_MENU::AddTab("Free Camera", 90, DrawTab, "Cinematic");
	const bool g_overlayAdded = DEBUG_MENU::AddOverlay(90, DrawOverlay);
}

void CINEMATIC_CAMERA::GoBehind(const float position[3], float facing)
{
	Place(PoseBehind(position, facing));
}

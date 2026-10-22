#include "COLLISION_INFO.h"
#include "DEBUG_MARKER.h"
#include "DEBUG_MENU.h"
#include "LIVE_LINK.h"

#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstdarg>
#include <cstddef>
#include <cstdio>
#include <cstring>
#include <mutex>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <intrin.h>

#include <imgui.h>

/*
	Addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).

	The debug menu's World > Inspect tab: what the camera is looking at. A ray is cast from the camera along the way it
	looks, through the game's own queue of rays - the one its zone streaming, footsteps and AI sight use. The ray is handed
	in at a frame's presentation (the queue takes a lock of its own, so this is safe from any thread), the game works it
	out during the next frame, and its answer is collected at the presentation after that, so what is shown is a frame
	old. The ray is only cast while something wants it: the crosshair switched on, another view asking for the pick
	(DEBUG_MENU::Pick), or OpenCAGE asking with inspect.pick; once nothing does, the game's slot for it is handed back.
	While the game is held (game.paused, the debugger's hold, the pause menu) it works out no rays: the last answer is
	given only while the camera still looks along the ray it was cast for, and nothing is waited for.

	What the ray hit is a row of the level's collision map (see COLLISION_INFO.h), which says what the surface is: the
	script entity and composite instance it belongs to (shown with the composite's name and the instance's path of entity
	ids from the level's root, as OpenCAGE addresses it), the zone it streams with, its collision layer and flags, its
	render and physical material, the static collision instance it is stored as, and the model instance drawn in its
	place. For that model instance (only when it is one of the level's movers - the instances the level loads, which live
	until it unloads): its models, LOD ranges, materials and their textures, its environment map, its radiosity slice
	and its tint, and its mover index (the index OpenCAGE's mover list uses).

	The model hit can be shown with the game's own "usable object" glow - the bit the pickups set on the instance they
	are looking at. The bit is set with a locked operation, so a write of the game's to the same flags is never lost, and
	only on an instance that is still the level's mover at its index; it is put back when the pick moves on, unless the
	game had it set already. Otherwise (or as well) the model's box is drawn.

	PickResult (DEBUG_MENU::Pick) is filled from the latest answer for other views, and inspect.pick reports it to
	OpenCAGE (whose "Select from game" uses it).
*/
namespace
{
	// ---- The game's queue of rays ----
	// A ray is asked for with a query and followed with a handle (4 bytes: its slot in the queue, the queue, the kind of
	// ray); its answer is read once its status says it is done, and the slot handed back with a reset.
	constexpr uintptr_t kRaySubmit = 0x003F9AD0; // hands a query in (or moves the ray, while it is still waiting)
	constexpr uintptr_t kRayResult = 0x003EBA70; // copies the answer out, true once there is one
	constexpr uintptr_t kRayStatus = 0x003EB960;
	constexpr uintptr_t kRayReset = 0x003F2510;  // hands the slot back
	typedef void(__thiscall* t_ray_submit)(uint32_t* handle, const void* query);
	typedef bool(__thiscall* t_ray_result)(const uint32_t* handle, void* result);
	typedef int(__thiscall* t_ray_status)(const uint32_t* handle);
	typedef void(__thiscall* t_ray_reset)(uint32_t* handle);

	constexpr uint32_t kNoRay = 0x00FFFFFF;     // a handle holding no ray: its slot bits all set
	constexpr int kRayInvalid = 0;              // statuses: no such ray, waiting, nothing hit, something hit
	constexpr int kRayWaiting = 1;
	constexpr int kRayClear = 2;
	constexpr int kRayBlocked = 3;
	constexpr uint32_t kRayPriority = 2;        // the queue the game's own zone ray uses (high)
	constexpr float kRayLimit = 512.0f;         // the queue refuses a ray starting further than this from the origin, on any axis

	// The game clock (a double), for the query's time stamp
	constexpr uintptr_t kGameTime = 0x012410A8;

	// The game works out the queue's rays only while none of these pause bits is in force (game.paused, the debugger's
	// hold, the pause menu, a frame step...), unless its entity manager is reloading. The pause is a reference (a pointer
	// to it), its bits in force this frame at +4; the entity manager too (a pointer to it), its state at +0xB8
	constexpr uintptr_t kPause = 0x012F194C;
	constexpr uint32_t kPauseNow = 0x04;
	constexpr uint32_t kPauseStopsRays = 0x179FF;
	constexpr uintptr_t kEntityManager = 0x0134EF40;
	constexpr uint32_t kEntityManagerState = 0xB8;
	constexpr int kEntityManagerReloading = 2;

	// The ray starts this far ahead of the camera
	constexpr float kRayNudge = 0.05f;

	struct alignas(16) RayQuery
	{
		float start[4];
		float end[4];
		uint32_t kind;          // 0: a thin ray
		float radius;
		uint32_t priority;      // which queue: 0 low .. 3 critical
		uint32_t filter;        // the collision layer the ray is tested as
		const void* ignore;     // a physics object to ignore
		uint32_t hits;          // how many hits to test for
		const char* file;       // where it was asked from, for the game's own debugging
		uint32_t line;
		float time;
		uint32_t padding[3];
	};
	static_assert(offsetof(RayQuery, filter) == 0x2C && offsetof(RayQuery, time) == 0x40, "ray query layout");

	struct alignas(16) RayResult
	{
		float point[4];
		float contactNormal[4];
		float offset[4];
		float normal[4];
		const uint8_t* body;    // the physics body hit
		bool fixed;
		int32_t row;            // the collision map row hit (-1: none)
		uint32_t material;      // the render material hit
		float fraction;         // how far along the ray
		int32_t status;
		uint32_t filter;
		uint32_t padding;
	};
	static_assert(offsetof(RayResult, row) == 0x48 && offsetof(RayResult, status) == 0x54, "ray result layout");

	// ---- The level's model instances ----
	// In the engine object (DEBUG_MARKER::engine): the level's model instances by mover index (a pointer to an array of
	// pointers, then how many), and the environment maps (environments: a pointer and count, 0x28 each; the instances'
	// entries at +0x10: a pointer and count, 8 each - an instance and its environment's index)
	constexpr uint32_t kEngineMovers = 0x18A4;
	constexpr uint32_t kEngineEnvironmentMaps = 0xE668;

	// A model instance: a 3x4 matrix (axis i is (m[i], m[4 + i], m[8 + i]), its position (m[3], m[7], m[11])), a handle (its
	// kind in the low four bits: 3 is level geometry), its flags, and its elements (a pointer and a count byte)
	constexpr uint32_t kInstanceHandle = 0x30;
	constexpr uint32_t kInstanceFlags = 0x34;
	constexpr uint16_t kInstanceGlow = 0x100;   // drawn with the "usable object" glow
	constexpr uint32_t kInstanceElements = 0x38;
	constexpr uint32_t kKindLevelGeometry = 3;
	// Level geometry only: its colour scalars (vertex colour, diffuse - four numbers each), its environment map entry, its
	// radiosity slice (0xFF: lit from the probes, as moving things are)
	constexpr uint32_t kInstanceVertexScalars = 0x40;
	constexpr uint32_t kInstanceDiffuseScalars = 0x50;
	constexpr uint32_t kInstanceEnvironment = 0x90;
	constexpr uint32_t kInstanceRadiositySlice = 0x96;
	// An element (0x10 each): its model, its material, and its LOD's elements (a pointer and a count byte)
	constexpr uint32_t kElementSize = 0x10;
	// A model: its source file and name (64-bit pointers), its bounds (each with a LOD distance as the fourth number), and
	// its vertex and index counts
	constexpr uint32_t kModelSource = 0x00;
	constexpr uint32_t kModelName = 0x08;
	constexpr uint32_t kModelMin = 0x10;
	constexpr uint32_t kModelMax = 0x20;
	constexpr uint32_t kModelVertices = 0x5A;
	constexpr uint32_t kModelIndices = 0x5C;
	// A material: its textures (64-bit pointers), how many, its physical material and its priority (drawing pass)
	constexpr uint32_t kMaterialTextures = 0x20;
	constexpr uint32_t kMaterialTextureCount = 0xB5;
	constexpr uint32_t kMaterialPhysical = 0xBA;
	constexpr uint32_t kMaterialPriority = 0xBC;
	// A texture: its header (width, height, its file's name as a 64-bit pointer)
	constexpr uint32_t kTextureHeader = 0x68;

	// A physics body: its user data (its row, for bodies of their own), shape, collision filter, name, motion type,
	// transform (a rotation's columns, then the position) and velocity
	constexpr uint32_t kBodyUserData = 0x0C;
	constexpr uint32_t kBodyShape = 0x10;
	constexpr uint32_t kBodyFilter = 0x2C;
	constexpr uint32_t kBodyName = 0x78;
	constexpr uint32_t kBodyMotionType = 0xE8;

	// ---- Switches and values ----
	std::atomic<bool> g_crosshair = false;  // cast the ray every frame and draw the crosshair
	std::atomic<bool> g_label = true;       // a line saying what is hit, under the crosshair
	std::atomic<bool> g_box = true;         // the model hit's box
	std::atomic<bool> g_collisionBox = false; // the static collision instance hit's box
	std::atomic<bool> g_glow = false;       // the model hit glows as a usable object does
	std::atomic<bool> g_freeze = false;     // keep the last answer, cast nothing
	std::atomic<float> g_layer = static_cast<float>(COLLISION_INFO::kLayerMaterialTest);
	std::atomic<float> g_distance = 100.0f;

	// Someone outside the tab wants a ray cast (another view's Pick, inspect.pick) until then (GetTickCount64)
	std::atomic<ULONGLONG> g_demandUntil = 0;

	// ---- What was hit (render thread) ----
	struct Details
	{
		int row = -1;
		bool haveRow = false;
		COLLISION_INFO::Row rowData;
		COLLISION_INFO::CompositeInstance composite;
		char zone[96] = {};
		const uint8_t* instance = nullptr; // the model instance (only one of the level's movers)
		int mover = -1;
		bool haveModelBox = false;
		float modelMatrix[16] = {};
		float modelHalf[3] = {};
		char modelName[96] = {};
		bool haveCollisionBox = false;
		float collisionMatrix[16] = {};
		float collisionHalf[3] = {};
		std::string summary;
		std::vector<std::string> lines;
	};

	struct Pick
	{
		bool answered = false;     // a ray's answer has arrived since casting started
		bool hit = false;
		float from[3] = {}, to[3] = {};
		float point[3] = {}, normal[3] = {};
		float distance = 0.0f;
		int layer = 0;
		const uint8_t* body = nullptr;
		int32_t row = -1;
		uint32_t material = 0xFFFFFFFF;
		Details details;
		ULONGLONG at = 0;          // when the answer arrived (GetTickCount64)
	};

	uint32_t g_handle = kNoRay;
	Pick g_pick;
	const char* g_rayProblem = nullptr;    // why no ray is being cast, for the tab
	int32_t g_detailsRow = -2;             // the row g_pick.details was made for, and when (to remake it now and then)
	const uint8_t* g_detailsBody = nullptr;
	ULONGLONG g_detailsAt = 0;
	const uint8_t* g_rowsSeen = nullptr;   // the collision map the answers are from (a new one: a new level)

	// A row looked at by number in the tab
	int g_rowWanted = 0;
	Details g_rowDetails;
	bool g_rowShown = false;
	std::atomic<bool> g_rowDraw = false; // draw its boxes (the model's and its static collision's)

	// The glow: the instance it is on, its mover index, and whether it was this that set it
	const uint8_t* g_glowInstance = nullptr;
	int g_glowMover = -1;
	bool g_glowOurs = false;

	// What is published for other threads (inspect.pick, other views' Pick)
	std::mutex g_publishMutex;
	DEBUG_MENU::PickResult g_published;
	bool g_havePublished = false;
	ULONGLONG g_publishedAt = 0;
	std::string g_publishedExtra;
	std::vector<std::string> g_publishedLines;
	float g_publishedFrom[3] = {};         // the ray it was cast for: where it started, and its way (a unit vector)
	float g_publishedDir[3] = {};

	// The camera as the latest frame had it, for the server thread (copied by Cast on the render thread)
	struct CameraCopy
	{
		bool have = false;
		float position[3] = {};
		float forward[3] = {};
	};
	std::mutex g_cameraMutex;
	CameraCopy g_camera;

	// ---- Guarded game calls and reads (plain data in and out) ----

	uintptr_t Address(uintptr_t steam)
	{
		return DEVTOOLS_RELATIVE_ADDRESS(steam);
	}

	int RayStatus(const uint32_t* handle)
	{
		__try
		{
			const auto status = reinterpret_cast<t_ray_status>(Address(kRayStatus));
			return status ? status(handle) : kRayInvalid;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return kRayInvalid;
		}
	}

	bool RayAnswer(const uint32_t* handle, RayResult* result)
	{
		__try
		{
			const auto answer = reinterpret_cast<t_ray_result>(Address(kRayResult));
			return answer && answer(handle, result);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	void RayReset(uint32_t* handle)
	{
		__try
		{
			const auto reset = reinterpret_cast<t_ray_reset>(Address(kRayReset));
			if (reset)
				reset(handle);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
		}
		*handle |= kNoRay;
	}

	bool RaySubmit(uint32_t* handle, const RayQuery* query)
	{
		__try
		{
			const auto submit = reinterpret_cast<t_ray_submit>(Address(kRaySubmit));
			if (!submit)
				return false;
			submit(handle, query);
			return (*handle & kNoRay) != kNoRay;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			*handle |= kNoRay;
			return false;
		}
	}

	bool HavePhysicsWorld()
	{
		__try
		{
			const uintptr_t at = Address(COLLISION_INFO::kWorld);
			return at && *reinterpret_cast<void* const*>(at) != nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	const uint8_t* RowsPointer()
	{
		__try
		{
			const uintptr_t at = Address(COLLISION_INFO::kRows);
			return at ? *reinterpret_cast<const uint8_t* const*>(at) : nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	float GameTime()
	{
		__try
		{
			const uintptr_t at = Address(kGameTime);
			return at ? static_cast<float>(*reinterpret_cast<const double*>(at)) : 0.0f;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0.0f;
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

	// Whether the game is held so that it works out no rays (any thread): false when that can't be read
	bool GameHeld()
	{
		__try
		{
			const uintptr_t pauseAt = Address(kPause);
			const uintptr_t managerAt = Address(kEntityManager);
			if (!pauseAt || !managerAt)
				return false;
			const uint8_t* pause = *reinterpret_cast<const uint8_t* const*>(pauseAt);
			const uint8_t* manager = *reinterpret_cast<const uint8_t* const*>(managerAt);
			if (!pause || !manager)
				return false;
			if ((*reinterpret_cast<const uint32_t*>(pause + kPauseNow) & kPauseStopsRays) == 0)
				return false;
			return *reinterpret_cast<const int*>(manager + kEntityManagerState) != kEntityManagerReloading;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// The mover index of a model instance (-1: not one of the level's movers)
	int MoverIndex(const uint8_t* instance)
	{
		__try
		{
			if (!instance || !DEBUG_MARKER::engine)
				return -1;
			const uint8_t* const* movers = *reinterpret_cast<const uint8_t* const* const*>(DEBUG_MARKER::engine + kEngineMovers);
			const uint32_t count = *reinterpret_cast<const uint32_t*>(DEBUG_MARKER::engine + kEngineMovers + 4);
			if (!movers || count > 0x200000)
				return -1;
			for (uint32_t i = 0; i < count; i++)
				if (movers[i] == instance)
					return static_cast<int>(i);
			return -1;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return -1;
		}
	}

	// Whether an instance is still the level's mover at that index (the only instances whose flags are written)
	bool StillMover(const uint8_t* instance, int mover)
	{
		__try
		{
			if (!instance || mover < 0 || !DEBUG_MARKER::engine)
				return false;
			const uint8_t* const* movers = *reinterpret_cast<const uint8_t* const* const*>(DEBUG_MARKER::engine + kEngineMovers);
			const uint32_t count = *reinterpret_cast<const uint32_t*>(DEBUG_MARKER::engine + kEngineMovers + 4);
			return movers && static_cast<uint32_t>(mover) < count && movers[mover] == instance;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// The glow bit: read, set or cleared with a locked operation
	bool GlowBit(const uint8_t* instance, int change, bool& wasSet)
	{
		__try
		{
			short volatile* flags = reinterpret_cast<short volatile*>(const_cast<uint8_t*>(instance) + kInstanceFlags);
			short before;
			if (change > 0)
				before = _InterlockedOr16(flags, static_cast<short>(kInstanceGlow));
			else if (change < 0)
				before = _InterlockedAnd16(flags, static_cast<short>(~kInstanceGlow));
			else
				before = *flags;
			wasSet = (before & kInstanceGlow) != 0;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	const uint8_t* ReadPointer(const uint8_t* at)
	{
		__try
		{
			return at ? *reinterpret_cast<const uint8_t* const*>(at) : nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	struct ElementRead
	{
		const uint8_t* model;
		const uint8_t* material;
		const uint8_t* lodFirst;
		int lodCount;
	};

	struct InstanceRead
	{
		float matrix[12];
		uint32_t kind;
		uint32_t zoneBits;
		uint16_t flags;
		int elementCount;
		ElementRead elements[8];
		bool levelGeometry;
		float vertexScalars[4];
		float diffuseScalars[4];
		uint16_t environment;
		uint8_t slice;
	};

	bool ReadInstance(const uint8_t* instance, InstanceRead& out)
	{
		memset(&out, 0, sizeof(out));
		__try
		{
			memcpy(out.matrix, instance, sizeof(out.matrix));
			const uint32_t handle = *reinterpret_cast<const uint32_t*>(instance + kInstanceHandle);
			out.kind = handle & 0xF;
			out.zoneBits = (handle >> 4) & 0x1FF;
			out.flags = *reinterpret_cast<const uint16_t*>(instance + kInstanceFlags);
			const uint8_t* elements = *reinterpret_cast<const uint8_t* const*>(instance + kInstanceElements);
			const int count = instance[kInstanceElements + 4];
			out.elementCount = elements ? count : 0;
			for (int i = 0; i < out.elementCount && i < 8; i++)
			{
				const uint8_t* element = elements + i * kElementSize;
				out.elements[i].model = *reinterpret_cast<const uint8_t* const*>(element + 0x0);
				out.elements[i].material = *reinterpret_cast<const uint8_t* const*>(element + 0x4);
				out.elements[i].lodFirst = *reinterpret_cast<const uint8_t* const*>(element + 0x8);
				out.elements[i].lodCount = element[0xC];
			}
			out.levelGeometry = out.kind == kKindLevelGeometry;
			if (out.levelGeometry)
			{
				memcpy(out.vertexScalars, instance + kInstanceVertexScalars, sizeof(out.vertexScalars));
				memcpy(out.diffuseScalars, instance + kInstanceDiffuseScalars, sizeof(out.diffuseScalars));
				out.environment = *reinterpret_cast<const uint16_t*>(instance + kInstanceEnvironment);
				out.slice = instance[kInstanceRadiositySlice];
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	struct ModelRead
	{
		const char* name;
		const char* source;
		float min[4];
		float max[4];
		uint16_t vertices;
		uint16_t indices;
	};

	bool ReadModel(const uint8_t* model, ModelRead& out)
	{
		memset(&out, 0, sizeof(out));
		__try
		{
			if (!model)
				return false;
			out.source = *reinterpret_cast<const char* const*>(model + kModelSource);
			out.name = *reinterpret_cast<const char* const*>(model + kModelName);
			memcpy(out.min, model + kModelMin, sizeof(out.min));
			memcpy(out.max, model + kModelMax, sizeof(out.max));
			out.vertices = *reinterpret_cast<const uint16_t*>(model + kModelVertices);
			out.indices = *reinterpret_cast<const uint16_t*>(model + kModelIndices);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	struct MaterialRead
	{
		int textureCount;
		const uint8_t* textures[12];
		int physical;
		int priority;
	};

	bool ReadMaterial(const uint8_t* material, MaterialRead& out)
	{
		memset(&out, 0, sizeof(out));
		__try
		{
			if (!material)
				return false;
			out.textureCount = (std::min)(static_cast<int>(material[kMaterialTextureCount]), 12);
			for (int i = 0; i < out.textureCount; i++)
				out.textures[i] = *reinterpret_cast<const uint8_t* const*>(material + kMaterialTextures + i * 8);
			out.physical = material[kMaterialPhysical];
			out.priority = material[kMaterialPriority];
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	struct TextureRead
	{
		const char* name;
		int width;
		int height;
	};

	bool ReadTexture(const uint8_t* texture, TextureRead& out)
	{
		memset(&out, 0, sizeof(out));
		__try
		{
			const uint8_t* header = texture ? *reinterpret_cast<const uint8_t* const*>(texture + kTextureHeader) : nullptr;
			if (!header)
				return false;
			out.width = *reinterpret_cast<const int16_t*>(header + 0x10);
			out.height = *reinterpret_cast<const int16_t*>(header + 0x12);
			out.name = *reinterpret_cast<const char* const*>(header + 0x28);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	struct EnvironmentRead
	{
		int environment;
		const uint8_t* texture;
		uint32_t priority;
		float colour[4];
	};

	bool ReadEnvironment(uint16_t entry, EnvironmentRead& out)
	{
		memset(&out, 0, sizeof(out));
		out.environment = -1;
		__try
		{
			if (entry == 0xFFFF || !DEBUG_MARKER::engine)
				return false;
			const uint8_t* manager = reinterpret_cast<const uint8_t*>(DEBUG_MARKER::engine + kEngineEnvironmentMaps);
			const uint8_t* entries = *reinterpret_cast<const uint8_t* const*>(manager + 0x10);
			const uint32_t entryCount = *reinterpret_cast<const uint32_t*>(manager + 0x14);
			if (!entries || entry >= entryCount)
				return false;
			out.environment = *reinterpret_cast<const int*>(entries + entry * 8 + 4);
			const uint8_t* environments = *reinterpret_cast<const uint8_t* const*>(manager + 0x00);
			const uint32_t environmentCount = *reinterpret_cast<const uint32_t*>(manager + 0x04);
			if (!environments || out.environment < 0 || static_cast<uint32_t>(out.environment) >= environmentCount)
				return true;
			const uint8_t* environment = environments + out.environment * 0x28;
			out.texture = *reinterpret_cast<const uint8_t* const*>(environment + 0x00);
			out.priority = *reinterpret_cast<const uint32_t*>(environment + 0x04);
			memcpy(out.colour, environment + 0x08, sizeof(out.colour));
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	struct BodyRead
	{
		uint32_t userData;
		const uint8_t* shape;
		uint32_t filter;
		const char* name;
		int motionType;
		float position[3];
	};

	bool ReadBody(const uint8_t* body, BodyRead& out)
	{
		memset(&out, 0, sizeof(out));
		__try
		{
			if (!body)
				return false;
			out.userData = *reinterpret_cast<const uint32_t*>(body + kBodyUserData);
			out.shape = *reinterpret_cast<const uint8_t* const*>(body + kBodyShape);
			out.filter = *reinterpret_cast<const uint32_t*>(body + kBodyFilter);
			out.name = reinterpret_cast<const char*>(*reinterpret_cast<const uintptr_t*>(body + kBodyName) & ~static_cast<uintptr_t>(1));
			out.motionType = body[kBodyMotionType];
			memcpy(out.position, body + 0x120, sizeof(out.position));
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// ---- Text ----

	std::string Format(const char* format, ...)
	{
		char text[512];
		va_list args;
		va_start(args, format);
		vsnprintf(text, sizeof(text), format, args);
		va_end(args);
		return text;
	}

	std::string Text(const char* gameText, const char* fallback)
	{
		char copy[192];
		return COLLISION_INFO::CopyText(gameText, copy, sizeof(copy)) ? std::string(copy) : std::string(fallback);
	}

	// The last part of a file path
	std::string BaseName(const std::string& path)
	{
		const size_t slash = path.find_last_of("\\/");
		return slash == std::string::npos ? path : path.substr(slash + 1);
	}

	// ---- Working out what was hit ----

	// The model instance's details (only for one of the level's movers)
	void DescribeInstance(Details& details, const uint8_t* instance, const float* cameraPosition)
	{
		details.instance = nullptr;
		details.mover = MoverIndex(instance);
		if (!instance)
			return;
		if (details.mover < 0)
		{
			details.lines.push_back(Format("Model instance: %p (not one of the level's movers, so not read further)", instance));
			return;
		}
		InstanceRead read;
		if (!ReadInstance(instance, read))
		{
			details.lines.push_back(Format("Model instance: mover %d (could not be read)", details.mover));
			return;
		}
		details.instance = instance;
		static const char* const kKinds[] = { "light", "effect", "effect (own material)", "level geometry", "character", "other",
			"planet", "level geometry (extra)", "fog sphere" };
		const char* kind = read.kind < sizeof(kKinds) / sizeof(kKinds[0]) ? kKinds[read.kind] : "?";
		std::string flags;
		static const struct { uint16_t bit; const char* name; } kFlags[] = { { 0x1, "active" }, { 0x2, "stationary" }, { 0x4, "moved" },
			{ 0x8, "temporary" }, { 0x10, "scripted" }, { 0x20, "motion blur" }, { 0x40, "approximate lighting" },
			{ 0x80, "radiosity cubemap forced" }, { 0x100, "glowing" }, { 0x200, "occludes atmosphere" }, { 0x400, "flash script" } };
		for (const auto& flag : kFlags)
			if (read.flags & flag.bit)
				flags += (flags.empty() ? "" : ", ") + std::string(flag.name);
		const float position[3] = { read.matrix[3], read.matrix[7], read.matrix[11] };
		details.lines.push_back(Format("Model instance: mover %d, %s, at %.2f, %.2f, %.2f; %s", details.mover, kind, position[0], position[1], position[2],
			flags.empty() ? "no flags" : flags.c_str()));

		// Each element: its model (and LOD range) and material, with the material's textures
		float distance = 0.0f;
		if (cameraPosition)
		{
			const float dx = position[0] - cameraPosition[0], dy = position[1] - cameraPosition[1], dz = position[2] - cameraPosition[2];
			distance = std::sqrt(dx * dx + dy * dy + dz * dz);
		}
		bool haveBounds = false;
		float boundsMin[3] = { 1e30f, 1e30f, 1e30f }, boundsMax[3] = { -1e30f, -1e30f, -1e30f };
		for (int i = 0; i < read.elementCount && i < 8; i++)
		{
			const ElementRead& element = read.elements[i];
			ModelRead model;
			std::string modelText = "(no model)";
			if (ReadModel(element.model, model))
			{
				const std::string name = Text(model.name, "(unnamed)");
				const std::string source = BaseName(Text(model.source, ""));
				if (i == 0)
					snprintf(details.modelName, sizeof(details.modelName), "%s", name.c_str());
				modelText = Format("%s%s%s, %u vertices, %u triangles, LOD %.0f..%.0f m", name.c_str(), source.empty() ? "" : " from ", source.c_str(),
					model.vertices, model.indices / 3, model.min[3], model.max[3]);
				bool sane = true;
				for (int axis = 0; axis < 3; axis++)
					sane = sane && std::isfinite(model.min[axis]) && std::isfinite(model.max[axis]) && model.max[axis] >= model.min[axis];
				if (sane)
				{
					haveBounds = true;
					for (int axis = 0; axis < 3; axis++)
					{
						boundsMin[axis] = (std::min)(boundsMin[axis], model.min[axis]);
						boundsMax[axis] = (std::max)(boundsMax[axis], model.max[axis]);
					}
				}
				if (cameraPosition && (distance < model.min[3] || distance > model.max[3]) && model.max[3] > 0.0f)
					modelText += " (out of this LOD's range at this distance)";
			}
			char materialName[128];
			const bool namedMaterial = COLLISION_INFO::MaterialName(element.material, materialName, sizeof(materialName));
			MaterialRead material;
			const bool haveMaterial = ReadMaterial(element.material, material);
			details.lines.push_back(Format("  Element %d: %s", i, modelText.c_str()));
			if (haveMaterial)
			{
				char physical[64] = "-";
				COLLISION_INFO::PhysicalMaterialName(material.physical, physical, sizeof(physical));
				details.lines.push_back(Format("    Material: %s (pass priority %d, physical material %d %s)", namedMaterial ? materialName : "(unnamed)",
					material.priority, material.physical, physical));
				for (int t = 0; t < material.textureCount; t++)
				{
					TextureRead texture;
					if (!material.textures[t] || !ReadTexture(material.textures[t], texture))
						continue;
					details.lines.push_back(Format("    Texture %d: %s (%dx%d)", t, Text(texture.name, "(unnamed)").c_str(), texture.width, texture.height));
				}
			}
			if (element.lodFirst && element.lodCount > 0)
			{
				// The next LOD's first model
				ModelRead lod;
				if (ReadModel(ReadPointer(element.lodFirst), lod))
					details.lines.push_back(Format("    Next LOD: %d element(s), first %s, LOD %.0f..%.0f m", element.lodCount, Text(lod.name, "(unnamed)").c_str(),
						lod.min[3], lod.max[3]));
			}
		}
		if (read.elementCount > 8)
			details.lines.push_back(Format("  (%d more elements)", read.elementCount - 8));

		if (read.levelGeometry)
		{
			EnvironmentRead environment;
			if (read.environment == 0xFFFF)
				details.lines.push_back("Environment map: none");
			else if (ReadEnvironment(read.environment, environment) && environment.texture)
			{
				TextureRead texture;
				ReadTexture(environment.texture, texture);
				details.lines.push_back(Format("Environment map: %d (%s), priority %u, colour %.2f %.2f %.2f", environment.environment, Text(texture.name, "(unnamed)").c_str(),
					environment.priority, environment.colour[0], environment.colour[1], environment.colour[2]));
			}
			else
				details.lines.push_back(Format("Environment map: entry %u (could not be read)", read.environment));
			if (read.slice == 0xFF)
				details.lines.push_back("Radiosity: none of its own (lit from the probes, as moving things are)");
			else
				details.lines.push_back(Format("Radiosity: slice %u", read.slice));
			details.lines.push_back(Format("Tint: vertex colour %.2f %.2f %.2f %.2f, diffuse %.2f %.2f %.2f %.2f", read.vertexScalars[0], read.vertexScalars[1], read.vertexScalars[2],
				read.vertexScalars[3], read.diffuseScalars[0], read.diffuseScalars[1], read.diffuseScalars[2], read.diffuseScalars[3]));
		}

		// Its box: the models' bounds in its own space, placed by its matrix
		if (haveBounds)
		{
			float centre[3];
			for (int axis = 0; axis < 3; axis++)
			{
				centre[axis] = (boundsMin[axis] + boundsMax[axis]) * 0.5f;
				details.modelHalf[axis] = (boundsMax[axis] - boundsMin[axis]) * 0.5f;
			}
			memset(details.modelMatrix, 0, sizeof(details.modelMatrix));
			for (int axis = 0; axis < 3; axis++)
				for (int j = 0; j < 3; j++)
					details.modelMatrix[axis * 4 + j] = read.matrix[j * 4 + axis];
			for (int j = 0; j < 3; j++)
			{
				details.modelMatrix[12 + j] = read.matrix[j * 4 + 3];
				for (int axis = 0; axis < 3; axis++)
					details.modelMatrix[12 + j] += read.matrix[j * 4 + axis] * centre[axis];
			}
			details.modelMatrix[15] = 1.0f;
			details.haveModelBox = std::isfinite(details.modelMatrix[12]) && std::isfinite(details.modelMatrix[13]) && std::isfinite(details.modelMatrix[14]);
		}
	}

	// A row's details: what it says, the composite instance and zone, its materials and static instance, its model
	void DescribeRow(Details& details, int rowIndex, const float* cameraPosition)
	{
		details = Details();
		details.row = rowIndex;
		COLLISION_INFO::Row& row = details.rowData;
		if (!COLLISION_INFO::ReadRow(rowIndex, row))
		{
			details.summary = Format("row %d (could not be read)", rowIndex);
			details.lines.push_back(details.summary);
			return;
		}
		details.haveRow = true;
		const int layer = static_cast<int>(row.flags & COLLISION_INFO::kLayerMask);
		const bool spare = rowIndex >= COLLISION_INFO::FileRowCount();
		details.lines.push_back(Format("Collision map row %d%s", rowIndex, spare ? " (a spare row: collision made while the level runs)" : " (from COLLISION.MAP)"));
		details.lines.push_back("Flags: " + Format("0x%08X: ", row.flags) + COLLISION_INFO::FlagsText(row.flags));
		details.lines.push_back(Format("Entity %08X in composite instance %08X", row.entityGuid, row.instanceGuid));
		if (COLLISION_INFO::FindCompositeInstance(row.instanceGuid, details.composite))
			details.lines.push_back(Format("Composite: %s (%08X); instance path from the root: %s", details.composite.name, details.composite.compositeGuid,
				COLLISION_INFO::PathText(details.composite).c_str()));
		else
			details.lines.push_back("Composite: no running instance has that id");
		if (!COLLISION_INFO::ZoneName(row.zoneId, details.zone, sizeof(details.zone)))
			snprintf(details.zone, sizeof(details.zone), "%s", row.zoneId == 0 ? "none" : "not in the level's zone list");
		details.lines.push_back(Format("Zone: %08X (%s)", row.zoneId, details.zone));

		// Materials: the render material the row names (a material mapping may remap it), and the physical material the
		// game finds for it (through that mapping)
		char materialName[128] = "";
		const uint8_t* material = COLLISION_INFO::MaterialData(row.materialId);
		std::string materialExtra;
		if (row.mappingIndex != 0xFFFF)
			materialExtra += Format(", remapped by material mapping %u", row.mappingIndex);
		if (row.proxyIndex != 0xFFFF)
			materialExtra += Format(", material proxy %u", row.proxyIndex);
		if (row.materialId == 0xFFFFFFFF)
			details.lines.push_back("Render material: none");
		else if (COLLISION_INFO::MaterialName(material, materialName, sizeof(materialName)))
			details.lines.push_back(Format("Render material: %s (%08X)%s", materialName, row.materialId, materialExtra.c_str()));
		else
			details.lines.push_back(Format("Render material: %08X (no name)%s", row.materialId, materialExtra.c_str()));
		const int physical = COLLISION_INFO::PhysicalMaterial(row);
		char physicalName[64] = "";
		if (physical >= 0 && COLLISION_INFO::PhysicalMaterialName(physical, physicalName, sizeof(physicalName)))
			details.lines.push_back(Format("Physical material: %d %s (footsteps and impacts)", physical, physicalName));
		else if (physical >= 0)
			details.lines.push_back(Format("Physical material: %d (not in the level's list of physical materials)", physical));
		else
			details.lines.push_back(row.materialId == 0xFFFFFFFF ? "Physical material: none (no render material)" : "Physical material: none");
		if (row.filterExtra)
			details.lines.push_back(Format("Collision filter, high bits: %08X", row.filterExtra));

		// Where its collision is: its static instance, or its own body
		COLLISION_INFO::StaticInstance staticInstance;
		const char* compound = "";
		if (COLLISION_INFO::RowStaticInstance(row, staticInstance, &compound))
		{
			const int shapeType = COLLISION_INFO::ShapeType(staticInstance.shape);
			details.lines.push_back(Format("Static collision: instance %d of the %s compound, %s, filter %08X (layer %d %s)%s, at %.2f, %.2f, %.2f",
				row.collisionIndex, compound, COLLISION_INFO::ShapeTypeName(shapeType), staticInstance.filter, static_cast<int>(staticInstance.filter & 0x1F),
				COLLISION_INFO::LayerName(staticInstance.filter & 0x1F), (staticInstance.instanceFlags & 0x10) ? ", disabled" : "",
				staticInstance.translation[0], staticInstance.translation[1], staticInstance.translation[2]));
			if (staticInstance.row != rowIndex)
				details.lines.push_back(Format("  (that instance says it belongs to row %d)", staticInstance.row));
			details.haveCollisionBox = COLLISION_INFO::StaticInstanceBox(staticInstance, details.collisionMatrix, details.collisionHalf);
		}
		else if ((row.flags & COLLISION_INFO::kStorageMask) != 0)
			details.lines.push_back(Format("Static collision: instance %d of the %s compound (could not be read)", row.collisionIndex, COLLISION_INFO::StorageName(row.flags)));
		if (row.body)
		{
			BodyRead body;
			if (ReadBody(row.body, body))
				details.lines.push_back(Format("Physics body: %s, %s, layer %d (%s)%s%s", COLLISION_INFO::MotionTypeName(body.motionType),
					COLLISION_INFO::ShapeTypeName(COLLISION_INFO::ShapeType(body.shape)), static_cast<int>(body.filter & 0x1F), COLLISION_INFO::LayerName(body.filter & 0x1F),
					body.name ? ", named " : "", body.name ? Text(body.name, "?").c_str() : ""));
		}

		DescribeInstance(details, row.instance, cameraPosition);

		// The line for showing
		std::string summary = Format("%s row %d (%s)", COLLISION_INFO::StorageName(row.flags), rowIndex, COLLISION_INFO::LayerName(layer));
		if (row.flags & COLLISION_INFO::kGhosted)
			summary += ", collision off";
		if (details.composite.found)
			summary += Format(", in %s", details.composite.name);
		else
			summary += Format(", entity %08X", row.entityGuid);
		summary += Format(", zone %s", details.zone);
		if (details.modelName[0])
			summary += Format(", model %s", details.modelName);
		if (physicalName[0])
			summary += Format(", %s", physicalName);
		details.summary = summary;
	}

	// What a hit with no row is: the physics body
	void DescribeBody(Details& details, const uint8_t* bodyPointer)
	{
		details = Details();
		BodyRead body;
		if (!bodyPointer || !ReadBody(bodyPointer, body))
		{
			details.summary = "something with no collision map row";
			details.lines.push_back("Hit something with no collision map row, and no body to read");
			return;
		}
		const int layer = static_cast<int>(body.filter & 0x1F);
		details.summary = Format("a %s body with no collision map row (%s, layer %s)", COLLISION_INFO::MotionTypeName(body.motionType),
			COLLISION_INFO::ShapeTypeName(COLLISION_INFO::ShapeType(body.shape)), COLLISION_INFO::LayerName(layer));
		details.lines.push_back("No collision map row");
		details.lines.push_back(Format("Physics body: %s, %s, layer %d (%s), user data %08X%s%s, at %.2f, %.2f, %.2f", COLLISION_INFO::MotionTypeName(body.motionType),
			COLLISION_INFO::ShapeTypeName(COLLISION_INFO::ShapeType(body.shape)), layer, COLLISION_INFO::LayerName(layer), body.userData,
			body.name ? ", named " : "", body.name ? Text(body.name, "?").c_str() : "", body.position[0], body.position[1], body.position[2]));
	}

	// ---- Publishing ----

	void Publish()
	{
		DEBUG_MENU::PickResult result;
		result.hit = g_pick.answered && g_pick.hit;
		memcpy(result.position, g_pick.point, sizeof(result.position));
		memcpy(result.normal, g_pick.normal, sizeof(result.normal));
		result.distance = g_pick.distance;
		const Details& details = g_pick.details;
		if (result.hit && details.haveRow)
		{
			result.entityGuid = details.rowData.entityGuid;
			result.instanceGuid = details.rowData.instanceGuid;
			result.collisionRow = details.row;
			result.zoneId = details.rowData.zoneId;
		}
		snprintf(result.description, sizeof(result.description), "%s", result.hit ? details.summary.c_str() : "nothing hit");

		std::string extra = Format("layer=%d\n", g_pick.layer);
		if (result.hit && details.haveRow)
		{
			extra += Format("flags=%08X\n", details.rowData.flags);
			extra += Format("composite=%s\n", details.composite.found ? details.composite.name : "");
			extra += Format("composite_id=%08X\n", details.composite.compositeGuid);
			extra += "path=" + COLLISION_INFO::PathText(details.composite) + "\n";
			extra += Format("zone_name=%s\n", details.zone);
		}
		extra += Format("mover=%d\nmodel=%s", result.hit ? details.mover : -1, result.hit ? details.modelName : "");

		// The ray this answer was cast for
		float way[3];
		for (int i = 0; i < 3; i++)
			way[i] = g_pick.to[i] - g_pick.from[i];
		const float length = std::sqrt(way[0] * way[0] + way[1] * way[1] + way[2] * way[2]);

		std::lock_guard<std::mutex> lock(g_publishMutex);
		g_published = result;
		g_havePublished = true;
		g_publishedAt = GetTickCount64();
		g_publishedExtra = extra;
		g_publishedLines = result.hit ? details.lines : std::vector<std::string>{ "Nothing hit" };
		for (int i = 0; i < 3; i++)
		{
			g_publishedFrom[i] = g_pick.from[i];
			g_publishedDir[i] = length > 1e-4f ? way[i] / length : 0.0f;
		}
	}

	void Unpublish()
	{
		std::lock_guard<std::mutex> lock(g_publishMutex);
		g_havePublished = false;
	}

	// Whether the camera still looks along the ray the published answer was cast for (its start within a centimetre, its
	// way within a fifth of a degree), so that answer still holds while the game is held and works out no rays. Any
	// thread, with g_publishMutex held.
	bool CameraStillOnRay()
	{
		CameraCopy camera;
		{
			std::lock_guard<std::mutex> lock(g_cameraMutex);
			camera = g_camera;
		}
		if (!camera.have)
			return false;
		const float length = std::sqrt(camera.forward[0] * camera.forward[0] + camera.forward[1] * camera.forward[1] + camera.forward[2] * camera.forward[2]);
		if (length < 1e-4f)
			return false;
		float way[3], apart = 0.0f, along = 0.0f;
		for (int i = 0; i < 3; i++)
		{
			way[i] = camera.forward[i] / length;
			const float gap = camera.position[i] + way[i] * kRayNudge - g_publishedFrom[i];
			apart += gap * gap;
			along += way[i] * g_publishedDir[i];
		}
		// The angle between the two ways from their cross product (its length is the angle's sine)
		const float cross[3] = {
			way[1] * g_publishedDir[2] - way[2] * g_publishedDir[1],
			way[2] * g_publishedDir[0] - way[0] * g_publishedDir[2],
			way[0] * g_publishedDir[1] - way[1] * g_publishedDir[0],
		};
		const float sine = std::sqrt(cross[0] * cross[0] + cross[1] * cross[1] + cross[2] * cross[2]);
		return apart <= 0.01f * 0.01f && along > 0.0f && sine <= 0.0035f;
	}

	// ---- The glow ----

	void SetGlow(const uint8_t* wanted, int mover)
	{
		// While no level runs (one closing or opening) nothing is written: its instances are being freed with it
		if (!LevelRunning())
		{
			g_glowInstance = nullptr;
			g_glowMover = -1;
			g_glowOurs = false;
			return;
		}
		if (!g_glow)
			wanted = nullptr;
		if (g_glowInstance && g_glowInstance != wanted)
		{
			bool wasSet = false;
			if (g_glowOurs && StillMover(g_glowInstance, g_glowMover))
				GlowBit(g_glowInstance, -1, wasSet);
			g_glowInstance = nullptr;
			g_glowMover = -1;
			g_glowOurs = false;
		}
		if (!wanted || !StillMover(wanted, mover))
			return;
		bool wasSet = false;
		if (g_glowInstance != wanted)
		{
			// Ours only when the game had not set it itself (a pickup it is looking at)
			if (!GlowBit(wanted, 0, wasSet))
				return;
			g_glowInstance = wanted;
			g_glowMover = mover;
			g_glowOurs = !wasSet;
		}
		// Again every frame, in case the game cleared it
		if (g_glowOurs)
			GlowBit(wanted, 1, wasSet);
	}

	// ---- Casting ----

	bool CastingWanted()
	{
		if (g_freeze)
			return false;
		return g_crosshair || GetTickCount64() < g_demandUntil.load();
	}

	void HandBack()
	{
		if ((g_handle & kNoRay) != kNoRay)
			RayReset(&g_handle);
		g_handle = kNoRay;
	}

	void TakeAnswer(const RayResult& result)
	{
		g_pick.answered = true;
		g_pick.at = GetTickCount64();
		g_pick.hit = result.status == kRayBlocked;
		memcpy(g_pick.point, result.point, sizeof(g_pick.point));
		const float* normal = result.normal;
		if (std::fabs(normal[0]) + std::fabs(normal[1]) + std::fabs(normal[2]) < 1e-4f)
			normal = result.contactNormal;
		memcpy(g_pick.normal, normal, sizeof(g_pick.normal));
		const float length = std::sqrt((g_pick.to[0] - g_pick.from[0]) * (g_pick.to[0] - g_pick.from[0]) + (g_pick.to[1] - g_pick.from[1]) * (g_pick.to[1] - g_pick.from[1]) +
			(g_pick.to[2] - g_pick.from[2]) * (g_pick.to[2] - g_pick.from[2]));
		g_pick.distance = std::isfinite(result.fraction) ? result.fraction * length : 0.0f;
		g_pick.body = result.body;
		g_pick.row = result.row;
		g_pick.material = result.material;
		if (!g_pick.hit)
		{
			g_pick.details = Details();
			g_detailsRow = -2;
			return;
		}
		// Worked out again when the row changes, and twice a second (its state can change: collision off, a model shown)
		const ULONGLONG now = GetTickCount64();
		if (result.row != g_detailsRow || result.body != g_detailsBody || now - g_detailsAt > 500)
		{
			float camera[3];
			const bool haveCamera = DEBUG_MENU::CameraPosition(camera);
			if (result.row >= 0)
				DescribeRow(g_pick.details, result.row, haveCamera ? camera : nullptr);
			else
				DescribeBody(g_pick.details, result.body);
			g_detailsRow = result.row;
			g_detailsBody = result.body;
			g_detailsAt = now;
		}
	}

	void Cast()
	{
		// The camera, every frame, for the server thread's answers kept while the game is held
		{
			CameraCopy camera;
			camera.have = DEBUG_MENU::CameraPosition(camera.position) && DEBUG_MENU::CameraForward(camera.forward);
			std::lock_guard<std::mutex> lock(g_cameraMutex);
			g_camera = camera;
		}

		if (!CastingWanted())
		{
			// A ray still waiting is left until it is answered (the game's task may be working it out right now, and would
			// write its answer into a slot handed back to the queue), then handed back at a later frame
			if ((g_handle & kNoRay) != kNoRay && RayStatus(&g_handle) != kRayWaiting)
				HandBack();
			g_rayProblem = nullptr;
			// An answer kept from a level that has since closed (or been replaced) describes that level: it is dropped
			if (g_pick.answered && !g_freeze && (!LevelRunning() || RowsPointer() != g_rowsSeen))
			{
				g_pick = Pick();
				g_detailsRow = -2;
			}
			return;
		}
		// No level (or one closing or opening): the game empties its queues as levels close and open, so a handle held then
		// is simply dropped
		const uint8_t* rows = RowsPointer();
		if (!LevelRunning() || !HavePhysicsWorld() || !rows)
		{
			g_handle = kNoRay;
			g_rayProblem = "no level running";
			g_pick = Pick();
			g_detailsRow = -2;
			g_rowsSeen = nullptr;
			Unpublish();
			return;
		}
		if (rows != g_rowsSeen)
		{
			g_rowsSeen = rows;
			g_pick = Pick();
			g_detailsRow = -2;
			g_rowShown = false;
		}

		// Last frame's ray: its answer if it has one, then its slot handed back
		if ((g_handle & kNoRay) != kNoRay)
		{
			const int status = RayStatus(&g_handle);
			if (status == kRayInvalid)
				g_handle = kNoRay;
			else if (status >= kRayClear)
			{
				RayResult result = {};
				if (RayAnswer(&g_handle, &result))
				{
					TakeAnswer(result);
					Publish();
				}
				HandBack();
			}
		}

		// This frame's (moving last frame's instead while that one still waits)
		float position[3], forward[3];
		if (!DEBUG_MENU::CameraPosition(position) || !DEBUG_MENU::CameraForward(forward))
		{
			g_rayProblem = "no camera";
			return;
		}
		const float length = std::sqrt(forward[0] * forward[0] + forward[1] * forward[1] + forward[2] * forward[2]);
		if (length < 1e-4f)
			return;
		const float distance = (std::max)(1.0f, (std::min)(g_distance.load(), 500.0f));
		RayQuery query = {};
		for (int i = 0; i < 3; i++)
		{
			query.start[i] = position[i] + forward[i] / length * kRayNudge;
			query.end[i] = query.start[i] + forward[i] / length * distance;
		}
		if (std::fabs(query.start[0]) >= kRayLimit || std::fabs(query.start[1]) >= kRayLimit || std::fabs(query.start[2]) >= kRayLimit)
		{
			g_rayProblem = "the camera is too far from the level's origin for the game's rays";
			return;
		}
		const int layer = (std::max)(0, (std::min)(31, static_cast<int>(g_layer.load() + 0.5f)));
		query.kind = 0;
		query.radius = 0.0f;
		query.priority = kRayPriority;
		query.filter = static_cast<uint32_t>(layer);
		query.ignore = nullptr;
		query.hits = 1;
		query.file = "OpenCAGE debug menu: inspector";
		query.line = 0;
		query.time = GameTime();
		g_rayProblem = RaySubmit(&g_handle, &query) ? nullptr : "the game's queue of rays did not take the ray";
		memcpy(g_pick.from, query.start, sizeof(g_pick.from));
		memcpy(g_pick.to, query.end, sizeof(g_pick.to));
		g_pick.layer = layer;

		// While the game is held the ray waits: said, unless the answer kept is for where the camera still looks
		if (!g_rayProblem && GameHeld())
		{
			std::lock_guard<std::mutex> lock(g_publishMutex);
			if (!g_havePublished || !CameraStillOnRay())
				g_rayProblem = "the game is paused: rays are cast once it runs again";
		}
	}

	// ---- Drawing ----

	void DrawBoxes(ImDrawList* drawList, const Details& details, bool model, bool collision, ImU32 modelColour, ImU32 collisionColour)
	{
		if (model && details.haveModelBox)
			DEBUG_MENU::WorldOrientedBox(drawList, details.modelMatrix, details.modelHalf, modelColour, 2.0f);
		if (collision && details.haveCollisionBox)
			DEBUG_MENU::WorldOrientedBox(drawList, details.collisionMatrix, details.collisionHalf, collisionColour, 1.5f);
	}

	void DrawOverlay(ImDrawList* drawList)
	{
		Cast();

		// The glow follows the pick while it is a hit on a mover's model
		const bool frozen = g_freeze;
		const bool showing = g_crosshair || frozen;
		SetGlow(showing && g_pick.answered && g_pick.hit ? g_pick.details.instance : nullptr, g_pick.details.mover);

		if (g_rowShown && g_rowDraw)
			DrawBoxes(drawList, g_rowDetails, true, true, IM_COL32(255, 160, 255, 220), IM_COL32(120, 220, 255, 200));

		if (!showing)
			return;
		const ImVec2 display = ImGui::GetIO().DisplaySize;
		const ImVec2 centre(display.x * 0.5f, display.y * 0.5f);
		const bool hit = g_pick.answered && g_pick.hit;
		const ImU32 colour = hit ? IM_COL32(120, 255, 120, 230) : IM_COL32(220, 220, 220, 200);
		const float scale = DEBUG_MENU::Scale();
		const float gap = 4.0f * scale, arm = 10.0f * scale;
		if (!frozen)
		{
			const ImVec2 offsets[4][2] = { { { -gap - arm, 0 }, { -gap, 0 } }, { { gap, 0 }, { gap + arm, 0 } }, { { 0, -gap - arm }, { 0, -gap } }, { { 0, gap }, { 0, gap + arm } } };
			for (const auto& line : offsets)
			{
				const ImVec2 a(centre.x + line[0].x, centre.y + line[0].y), b(centre.x + line[1].x, centre.y + line[1].y);
				drawList->AddLine(a, b, IM_COL32(0, 0, 0, 160), 3.5f);
				drawList->AddLine(a, b, colour, 1.5f);
			}
		}

		if (hit)
		{
			DrawBoxes(drawList, g_pick.details, g_box, g_collisionBox, IM_COL32(255, 220, 80, 220), IM_COL32(80, 220, 255, 200));
			DEBUG_MENU::WorldCross(drawList, g_pick.point, 0.08f, IM_COL32(255, 80, 80, 255), 2.0f);
			float tip[3];
			for (int i = 0; i < 3; i++)
				tip[i] = g_pick.point[i] + g_pick.normal[i] * 0.4f;
			DEBUG_MENU::WorldArrow(drawList, g_pick.point, tip, IM_COL32(255, 120, 120, 230), 1.5f);
		}

		if (g_label)
		{
			std::string text;
			if (g_rayProblem)
				text = g_rayProblem;
			else if (!g_pick.answered)
				text = "waiting for the game to cast the ray";
			else if (!hit)
				text = Format("nothing within %.0f m (layer: %s)", g_distance.load(), COLLISION_INFO::LayerName(g_pick.layer));
			else
				text = Format("%.2f m: %s", g_pick.distance, g_pick.details.summary.c_str());
			if (frozen)
				text = "[frozen] " + text;
			const float size = 15.0f * scale;
			const ImVec2 extent = ImGui::GetFont()->CalcTextSizeA(size, display.x * 0.6f, display.x * 0.6f, text.c_str());
			const ImVec2 corner(centre.x - extent.x * 0.5f, centre.y + gap + arm + 8.0f * scale);
			drawList->AddRectFilled(ImVec2(corner.x - 5.0f, corner.y - 3.0f), ImVec2(corner.x + extent.x + 5.0f, corner.y + extent.y + 3.0f), IM_COL32(0, 0, 0, 160), 3.0f);
			drawList->AddText(ImGui::GetFont(), size, corner, hit ? IM_COL32(200, 255, 200, 255) : IM_COL32(230, 230, 230, 255), text.c_str(), nullptr, display.x * 0.6f);
		}
	}

	// ---- The tab ----

	const struct { int layer; const char* use; } kUsefulLayers[] = {
		{ COLLISION_INFO::kLayerMaterialTest, "what the zone and footstep rays use" },
		{ 0x0A, "what AI sight uses" },
		{ 0x09, "ballistic collision" },
		{ 0x0B, "ballistic test rays" },
		{ 0x03, "standard" },
		{ 0x02, "the camera's" },
		{ 0x01, "triggers" },
		{ 0x1E, "character triggers" },
		{ 0x11, "sound" },
		{ 0x0D, "closed paths (door blockers)" },
		{ 0x19, "the player's" },
		{ 0x00, "everything" },
	};

	// The lines wrap at the window's edge; an indented line (an element, its material, its textures) wraps to its own indent
	void DrawDetailsLines(const std::vector<std::string>& lines)
	{
		const float space = ImGui::CalcTextSize(" ").x;
		for (const std::string& line : lines)
		{
			const size_t text = (std::min)(line.find_first_not_of(' '), line.size());
			if (text)
				ImGui::Indent(space * static_cast<float>(text));
			ImGui::TextWrapped("%s", line.c_str() + text);
			if (text)
				ImGui::Unindent(space * static_cast<float>(text));
		}
	}

	void DrawTab()
	{
		bool crosshair = g_crosshair;
		if (ImGui::Checkbox("Crosshair inspector", &crosshair))
			g_crosshair = crosshair;
		ImGui::SameLine();
		bool freeze = g_freeze;
		if (ImGui::Checkbox("Freeze", &freeze))
			g_freeze = freeze;
		ImGui::SameLine();
		ImGui::TextDisabled("(keeps the last answer)");
		bool label = g_label, box = g_box, collisionBox = g_collisionBox, glow = g_glow;
		if (ImGui::Checkbox("Label", &label))
			g_label = label;
		ImGui::SameLine();
		if (ImGui::Checkbox("Model box", &box))
			g_box = box;
		ImGui::SameLine();
		if (ImGui::Checkbox("Collision box", &collisionBox))
			g_collisionBox = collisionBox;
		ImGui::SameLine();
		if (ImGui::Checkbox("Glow", &glow))
			g_glow = glow;
		if (ImGui::IsItemHovered())
			ImGui::SetTooltip("The model hit is drawn with the game's own glow for usable objects");

		const int layer = (std::max)(0, (std::min)(31, static_cast<int>(g_layer.load() + 0.5f)));
		char preview[96];
		snprintf(preview, sizeof(preview), "%d: %s", layer, COLLISION_INFO::LayerName(layer));
		ImGui::SetNextItemWidth(260.0f * DEBUG_MENU::Scale());
		if (ImGui::BeginCombo("Ray layer", preview))
		{
			for (const auto& useful : kUsefulLayers)
			{
				char text[128];
				snprintf(text, sizeof(text), "%d: %s - %s", useful.layer, COLLISION_INFO::LayerName(useful.layer), useful.use);
				if (ImGui::Selectable(text, useful.layer == layer))
					g_layer = static_cast<float>(useful.layer);
			}
			ImGui::Separator();
			for (int i = 0; i < 32; i++)
			{
				char text[64];
				snprintf(text, sizeof(text), "%d: %s##all", i, COLLISION_INFO::LayerName(i));
				if (ImGui::Selectable(text, i == layer))
					g_layer = static_cast<float>(i);
			}
			ImGui::EndCombo();
		}
		float distance = g_distance;
		ImGui::SetNextItemWidth(260.0f * DEBUG_MENU::Scale());
		if (ImGui::SliderFloat("Ray length (m)", &distance, 1.0f, 500.0f, "%.0f"))
			g_distance = distance;

		ImGui::SeparatorText("Under the crosshair");
		ImGui::PushTextWrapPos(0.0f);
		const bool casting = g_crosshair || g_freeze || CastingWanted();
		if (!casting && g_pick.answered)
			ImGui::TextColored(ImVec4(1.0f, 0.8f, 0.3f, 1.0f), "The last answer, from %.0f s ago - the crosshair inspector is off (switch it on for a new one):",
				static_cast<double>(GetTickCount64() - g_pick.at) / 1000.0);
		else if (!casting)
			ImGui::TextDisabled("Switch the crosshair inspector on to cast a ray from the camera.");
		else if (g_freeze && g_pick.answered)
			ImGui::TextDisabled("Frozen: the last answer is kept.");
		else if (g_rayProblem)
			ImGui::TextDisabled("%s", g_rayProblem);
		ImGui::PopTextWrapPos();
		if (g_pick.answered)
		{
			if (!g_pick.hit)
				ImGui::Text("Nothing hit within %.0f m on layer %d (%s)", g_distance.load(), g_pick.layer, COLLISION_INFO::LayerName(g_pick.layer));
			else
			{
				// (the normal's tiny parts as 0: no "-0.00")
				const auto shown = [](float value) { return std::fabs(value) <= 0.005f ? 0.0f : value; };
				ImGui::TextWrapped("Hit at %.2f, %.2f, %.2f, %.2f m away; normal %.2f, %.2f, %.2f", g_pick.point[0], g_pick.point[1], g_pick.point[2], g_pick.distance,
					shown(g_pick.normal[0]), shown(g_pick.normal[1]), shown(g_pick.normal[2]));
				if (ImGui::SmallButton("Copy"))
				{
					std::string text;
					for (const std::string& line : g_pick.details.lines)
						text += line + "\n";
					ImGui::SetClipboardText(text.c_str());
				}
				ImGui::SameLine();
				ImGui::TextDisabled("(OpenCAGE's \"Select from game\" reads this through inspect.pick)");
				DrawDetailsLines(g_pick.details.lines);
			}
		}

		ImGui::SeparatorText("Any row of the collision map");
		const int rows = COLLISION_INFO::RowCount();
		ImGui::Text("%d rows from COLLISION.MAP, room for %d", COLLISION_INFO::FileRowCount(), rows);
		ImGui::SetNextItemWidth(140.0f * DEBUG_MENU::Scale());
		ImGui::InputInt("Row", &g_rowWanted);
		g_rowWanted = (std::max)(0, g_rowWanted);
		ImGui::SameLine();
		if (ImGui::Button("Show"))
		{
			float camera[3];
			const bool haveCamera = DEBUG_MENU::CameraPosition(camera);
			DescribeRow(g_rowDetails, g_rowWanted, haveCamera ? camera : nullptr);
			g_rowShown = true;
		}
		ImGui::SameLine();
		bool rowDraw = g_rowDraw;
		if (ImGui::Checkbox("Draw it", &rowDraw))
			g_rowDraw = rowDraw;
		if (g_pick.answered && g_pick.hit && g_pick.row >= 0)
		{
			ImGui::SameLine();
			if (ImGui::Button("The one hit"))
				g_rowWanted = g_pick.row;
		}
		if (g_rowShown)
			DrawDetailsLines(g_rowDetails.lines);
	}

	// ---- The pick, for other views and OpenCAGE ----

	bool ProvidePick(DEBUG_MENU::PickResult& out)
	{
		// Whoever asks keeps the ray going a while, crosshair or not
		const ULONGLONG now = GetTickCount64();
		g_demandUntil = now + 1500;
		const bool held = GameHeld();
		std::lock_guard<std::mutex> lock(g_publishMutex);
		if (!g_havePublished)
			return false;
		// While the game is held it works out no rays: the answer kept only while the camera still looks along its ray (the
		// free camera may have flown away), unless the inspector is frozen on it
		if (held && !g_freeze && !CameraStillOnRay())
			return false;
		// Not an answer from long ago (the crosshair off and nobody asking since): the ray just asked for answers in a frame
		if (!held && !g_crosshair && !g_freeze && now - g_publishedAt > 1000)
			return false;
		out = g_published;
		return out.hit;
	}

	// A fresh answer: the latest when the crosshair has been casting, else one cast for this (waiting up to a second and a
	// half for it). While the game is held nothing is waited for: the answer kept is given (with paused=1) only when the
	// camera still looks along its ray
	bool WaitForPick(DEBUG_MENU::PickResult& result, std::string& extra, std::vector<std::string>* lines, std::string& problem)
	{
		const ULONGLONG start = GetTickCount64();
		g_demandUntil = start + 2500;
		for (;;)
		{
			const bool held = GameHeld();
			{
				std::lock_guard<std::mutex> lock(g_publishMutex);
				const bool fresh = g_havePublished && (held ? g_freeze || CameraStillOnRay() :
					g_publishedAt >= start || (g_crosshair && start - g_publishedAt < 500) || g_freeze);
				if (fresh)
				{
					result = g_published;
					extra = g_publishedExtra;
					if (held)
						extra += "\npaused=1";
					if (lines)
						*lines = g_publishedLines;
					return true;
				}
			}
			if (held)
			{
				problem = "the game is paused, and it only casts rays while it runs (Continue in the debugger, or turn game.paused / game.pause_while_open off)";
				return false;
			}
			if (GetTickCount64() - start > 1500)
			{
				problem = g_rayProblem ? g_rayProblem : "no answer from the game (no level running, or the game is paused)";
				return false;
			}
			Sleep(15);
		}
	}

	DEBUG_MENU::CommandResult PickCommand(const std::string&)
	{
		DEBUG_MENU::PickResult result;
		std::string extra, problem;
		if (!WaitForPick(result, extra, nullptr, problem))
		{
			// No answer at all (not a miss): said with answered=0
			result = DEBUG_MENU::PickResult();
			snprintf(result.description, sizeof(result.description), "%s", problem.c_str());
			extra = "answered=0";
		}
		std::string text = Format("hit=%d\nposition=%.3f,%.3f,%.3f\nnormal=%.3f,%.3f,%.3f\ndistance=%.3f\nentity=%08X\ninstance=%08X\ncollision_row=%d\nzone=%08X\n",
			result.hit ? 1 : 0, result.position[0], result.position[1], result.position[2], result.normal[0], result.normal[1], result.normal[2], result.distance,
			result.entityGuid, result.instanceGuid, result.collisionRow, result.zoneId);
		text += "description=" + std::string(result.description);
		if (!extra.empty())
			text += "\n" + extra;
		return { true, text };
	}

	DEBUG_MENU::CommandResult DetailsCommand(const std::string&)
	{
		DEBUG_MENU::PickResult result;
		std::string extra, problem;
		std::vector<std::string> lines;
		if (!WaitForPick(result, extra, &lines, problem))
			return { false, problem };
		std::string text = Format("hit=%d", result.hit ? 1 : 0);
		if (result.hit)
			text += Format("\nposition=%.3f,%.3f,%.3f\ndistance=%.3f", result.position[0], result.position[1], result.position[2], result.distance);
		for (const std::string& line : lines)
			text += "\n" + line;
		return { true, text };
	}

	DEBUG_MENU::CommandResult RowCommand(const std::string& argument)
	{
		char* end = nullptr;
		const long row = strtol(argument.c_str(), &end, 0);
		if (argument.empty() || end == argument.c_str() || row < 0)
			return { false, "Give a row number (from 0)" };
		return DEBUG_MENU::AnswerOnRenderThread([row]()
		{
			if (row >= COLLISION_INFO::RowCount())
				return DEBUG_MENU::CommandResult{ false, Format("No row %ld: the level's collision map has room for %d", row, COLLISION_INFO::RowCount()) };
			Details details;
			float camera[3];
			const bool haveCamera = DEBUG_MENU::CameraPosition(camera);
			DescribeRow(details, static_cast<int>(row), haveCamera ? camera : nullptr);
			// Shown in the tab too (and drawn while inspect.row_draw is on)
			g_rowWanted = static_cast<int>(row);
			g_rowDetails = details;
			g_rowShown = true;
			std::string text = "description=" + details.summary;
			for (const std::string& line : details.lines)
				text += "\n" + line;
			return DEBUG_MENU::CommandResult{ true, text };
		});
	}

	DEBUG_MENU::CommandResult LayersCommand(const std::string&)
	{
		std::string text;
		for (int i = 0; i < 32; i++)
			text += Format("%s%d=%s", i ? "\n" : "", i, COLLISION_INFO::LayerName(i));
		return { true, text };
	}

	void NameControls()
	{
		DEBUG_MENU::AddSwitch("inspect.crosshair", &g_crosshair);
		DEBUG_MENU::AddSwitch("inspect.label", &g_label);
		DEBUG_MENU::AddSwitch("inspect.box", &g_box);
		DEBUG_MENU::AddSwitch("inspect.collision_box", &g_collisionBox);
		DEBUG_MENU::AddSwitch("inspect.glow", &g_glow);
		DEBUG_MENU::AddSwitch("inspect.freeze", &g_freeze);
		DEBUG_MENU::AddSwitch("inspect.row_draw", &g_rowDraw);
		DEBUG_MENU::AddValue("inspect.layer", &g_layer, 0.0f, 31.0f);
		DEBUG_MENU::AddValue("inspect.distance", &g_distance, 1.0f, 500.0f);
		DEBUG_MENU::AddCommand("inspect.pick", "what the camera looks at: hit, position, normal, distance, entity, instance, collision_row, zone, description, then more (casts a ray for it when the crosshair is off)",
			PickCommand);
		DEBUG_MENU::AddCommand("inspect.details", "everything the inspector shows for what the camera looks at, a line each", DetailsCommand);
		DEBUG_MENU::AddCommand("inspect.row", "one row of the level's collision map in full, also shown in the tab (argument: its number; inspect.row_draw draws it)", RowCommand);
		DEBUG_MENU::AddCommand("inspect.list_layers", "the collision layers, number=name, for inspect.layer", LayersCommand);
		DEBUG_MENU::SetPickProvider(ProvidePick);
	}

	// Detaching: the glow is taken off (if this set it) and the ray's slot handed back
	void AttachHooks(bool attach)
	{
		if (attach)
			return;
		g_glow = false;
		SetGlow(nullptr, -1);
		if (LevelRunning() && HavePhysicsWorld())
			HandBack();
	}

	const bool g_controls = (NameControls(), true);
	const bool g_added = DEBUG_MENU::AddTab("Inspect", 100, DrawTab, "World") && DEBUG_MENU::AddOverlay(3, DrawOverlay);
	const bool g_hooks = DEBUG_MENU::AddHooks(AttachHooks);
}

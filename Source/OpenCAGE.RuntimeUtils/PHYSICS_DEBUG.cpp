#include "COLLISION_INFO.h"
#include "DEBUG_MENU.h"
#include "LIVE_LINK.h"

#include <algorithm>
#include <atomic>
#include <cfloat>
#include <cmath>
#include <cstdarg>
#include <cstdio>
#include <cstring>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <imgui.h>

/*
	Addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).

	The debug menu's World > Physics tab: what the physics engine holds for the level, read at each frame's presentation
	(when the game's own frame work is done) with every read guarded. See COLLISION_INFO.h for how the game keeps it.

	- Static collision: a count of the four static compound shapes' instances (by layer, disabled, without a row, not
	  matching their row) against the collision map's rows, and an overlay of the instances near the camera as boxes -
	  their shape's box placed by the instance's transform - coloured by layer, zone, compound or state. The boxes are
	  worked out once per level (and again when the compounds change) and their layer read again as they are drawn, so
	  collision turned off shows at once.
	- "Show collision meshes": the game's own switch for drawing the render models flagged as collision in place of the
	  normal ones. It is only read as a model is shown, so it takes effect for models shown after it is set (reload a
	  checkpoint to see it everywhere).
	- Bodies: every physics body in the world's islands near the camera (the static compound bodies left out) - moving,
	  sleeping, keyframed, fixed and triggers - as boxes with their velocity, and the world's phantoms.
	- Character capsules: the game's 18 character controllers, each the capsule of a character, with its state (on the
	  ground, in the air, climbing...), its capsule kind, where it stands, and whether it is over a drop. The "capsule" is
	  an upright prism (its foot narrowed for stepping), one per capsule kind; the one of the kind in use is drawn.
	- A key to the colours of whatever is switched on, in the tab and at the right edge of the screen.
	- Physics systems (PHYSICS.MAP): every dynamic physics system the level made from its map - its entity, composite
	  instance and transform - and its bodies where they are now.
	- Collision layers: the world's collision filter, which layers collide with which (the table rays and bodies are
	  tested with).
*/
namespace
{
	// The character controllers: 18 of 0xB0 bytes - the character, the capsule body (its physics body at +0x10), the
	// controller state (its state at +0x10), where it stands, its capsule kind, and flags
	constexpr uintptr_t kControllers = 0x01307A48;
	constexpr int kControllerCount = 18;
	constexpr uint32_t kControllerSize = 0xB0;

	// The level's dynamic physics systems from PHYSICS.MAP: a pointer to them and how many, 0x50 each
	constexpr uintptr_t kSystems = 0x01308840;
	constexpr uintptr_t kSystemCount = 0x01308844;
	constexpr uint32_t kSystemSize = 0x50;

	// The game's switch for showing the render models flagged as collision (a byte)
	constexpr uintptr_t kShowCollisionMeshes = 0x012AEAE1;

	// The physics world: its fixed island, its active and inactive islands (arrays: data, count) and its phantoms (an
	// array), and its collision filter
	constexpr uint32_t kWorldFixedIsland = 0x20;
	constexpr uint32_t kWorldActiveIslands = 0x28;
	constexpr uint32_t kWorldInactiveIslands = 0x34;
	constexpr uint32_t kWorldFilter = 0x74;
	constexpr uint32_t kWorldPhantoms = 0x114;
	// An island's bodies (an array: data, count)
	constexpr uint32_t kIslandBodies = 0x4C;
	// A body (or phantom): its user data (its row), shape, collision filter, name; a body's motion type, transform (a
	// rotation's columns, then its position) and velocity
	constexpr uint32_t kObjectUserData = 0x0C;
	constexpr uint32_t kObjectShape = 0x10;
	constexpr uint32_t kObjectFilter = 0x2C;
	constexpr uint32_t kObjectName = 0x78;
	constexpr uint32_t kBodyMotionType = 0xE8;
	constexpr uint32_t kBodyTransform = 0xF0;
	constexpr uint32_t kBodyVelocity = 0x1B0;
	// A phantom: a box (min, max) for those with no shape, a transform for those with one
	constexpr uint32_t kPhantomBox = 0xA0;
	constexpr uint32_t kPhantomTransform = 0xA0;
	// A collision filter: its kind (2: by group and layer; 5 and 6 wrap another, at +0x3C), and a group filter's table: for
	// each layer, a bit for each layer it collides with
	constexpr uint32_t kFilterType = 0x20;
	constexpr uint32_t kFilterTable = 0x34;
	constexpr uint32_t kFilterChild = 0x3C;

	constexpr int kMaxBodies = 4096;
	constexpr int kMaxDrawnBoxes = 600;

	// ---- Switches and values ----
	std::atomic<bool> g_staticBoxes = false;
	std::atomic<bool> g_staticLabels = false;
	std::atomic<bool> g_bodies = false;
	std::atomic<bool> g_bodyLabels = false;
	std::atomic<bool> g_triggers = true;     // with the bodies: trigger volumes too
	std::atomic<bool> g_phantoms = false;
	std::atomic<bool> g_capsules = false;
	std::atomic<bool> g_systems = false;
	std::atomic<bool> g_legend = true;       // the key to the colours drawn over the game with the overlays
	std::atomic<bool> g_showCollisionMeshes = false;
	std::atomic<float> g_staticRadius = 15.0f;
	std::atomic<float> g_staticColour = 0.0f; // 0 layer, 1 zone, 2 compound, 3 state
	std::atomic<float> g_bodiesRadius = 25.0f;
	bool g_showCollisionMeshesSet = false;   // render thread: whether the game's switch was set from here

	std::string Format(const char* format, ...)
	{
		char text[512];
		va_list args;
		va_start(args, format);
		vsnprintf(text, sizeof(text), format, args);
		va_end(args);
		return text;
	}

	template<typename T> T Read(const uint8_t* at, uint32_t offset)
	{
		T value;
		memcpy(&value, at + offset, sizeof(T));
		return value;
	}

	const uint8_t* Pointer(const uint8_t* at, uint32_t offset)
	{
		return Read<const uint8_t*>(at, offset);
	}

	const uint8_t* World()
	{
		__try
		{
			const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(COLLISION_INFO::kWorld);
			return at ? *reinterpret_cast<const uint8_t* const*>(at) : nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
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

	float Distance(const float a[3], const float b[3])
	{
		const float dx = a[0] - b[0], dy = a[1] - b[1], dz = a[2] - b[2];
		return std::sqrt(dx * dx + dy * dy + dz * dz);
	}

	ImU32 Hue(float hue, int alpha)
	{
		float r, g, b;
		ImGui::ColorConvertHSVtoRGB(hue - std::floor(hue), 0.65f, 1.0f, r, g, b);
		return IM_COL32(static_cast<int>(r * 255), static_cast<int>(g * 255), static_cast<int>(b * 255), alpha);
	}

	ImU32 LayerColour(int layer, int alpha)
	{
		return Hue(static_cast<float>(layer) * 0.618034f, alpha);
	}

	ImU32 HashColour(uint32_t value, int alpha)
	{
		value ^= value >> 16;
		value *= 0x7FEB352Du;
		value ^= value >> 15;
		return Hue(static_cast<float>(value & 0xFFFF) / 65536.0f, alpha);
	}

	// An oriented box from a local box and a transform (the rotation's columns, then the position)
	void PlaceBox(const float min[3], const float max[3], const float axes[3][3], const float position[3], float matrix[16], float half[3])
	{
		float centre[3];
		for (int i = 0; i < 3; i++)
		{
			centre[i] = (min[i] + max[i]) * 0.5f;
			half[i] = (max[i] - min[i]) * 0.5f;
		}
		memset(matrix, 0, 16 * sizeof(float));
		for (int i = 0; i < 3; i++)
			for (int j = 0; j < 3; j++)
				matrix[i * 4 + j] = axes[i][j];
		for (int j = 0; j < 3; j++)
		{
			matrix[12 + j] = position[j];
			for (int i = 0; i < 3; i++)
				matrix[12 + j] += axes[i][j] * centre[i];
		}
		matrix[15] = 1.0f;
	}

	// ======== Static collision ========

	struct Compound
	{
		uintptr_t address;
		const char* name;
		ImU32 colour;
	};
	const Compound kCompounds[4] = {
		{ COLLISION_INFO::kWorldCompound, "static world", IM_COL32(230, 230, 230, 255) },
		{ COLLISION_INFO::kBallisticCompound, "ballistic only", IM_COL32(255, 160, 60, 255) },
		{ COLLISION_INFO::kLandscapeCompound, "landscape", IM_COL32(120, 230, 120, 255) },
		{ COLLISION_INFO::kPathCompound, "closed paths", IM_COL32(255, 80, 80, 255) },
	};

	struct StaticBox
	{
		int compound;
		int index;
		int row;
		bool haveBox;
		float matrix[16];
		float half[3];
		float radius;
	};

	// The boxes, worked out once per set of compounds (render thread)
	std::vector<StaticBox> g_staticCache;
	const uint8_t* g_staticCacheFor[4] = {};
	int g_staticCacheCounts[4] = {};

	void BuildStaticCache()
	{
		const uint8_t* compounds[4];
		int counts[4];
		bool same = true;
		for (int c = 0; c < 4; c++)
		{
			compounds[c] = COLLISION_INFO::Compound(kCompounds[c].address);
			counts[c] = COLLISION_INFO::CompoundInstanceCount(compounds[c]);
			same = same && compounds[c] == g_staticCacheFor[c] && counts[c] == g_staticCacheCounts[c];
		}
		if (same)
			return;
		g_staticCache.clear();
		for (int c = 0; c < 4; c++)
		{
			g_staticCacheFor[c] = compounds[c];
			g_staticCacheCounts[c] = counts[c];
			for (int i = 0; i < counts[c]; i++)
			{
				COLLISION_INFO::StaticInstance instance;
				if (!COLLISION_INFO::ReadCompoundInstance(compounds[c], i, instance))
					continue;
				StaticBox box = {};
				box.compound = c;
				box.index = i;
				box.row = instance.row;
				box.haveBox = COLLISION_INFO::StaticInstanceBox(instance, box.matrix, box.half);
				if (!box.haveBox)
				{
					memcpy(box.matrix + 12, instance.translation, sizeof(instance.translation));
					box.matrix[0] = box.matrix[5] = box.matrix[10] = box.matrix[15] = 1.0f;
				}
				box.radius = std::sqrt(box.half[0] * box.half[0] + box.half[1] * box.half[1] + box.half[2] * box.half[2]);
				g_staticCache.push_back(box);
			}
		}
	}

	// What an instance is now: its layer and flags (read again as it is drawn)
	bool InstanceState(int compound, int index, uint32_t& filter, uint32_t& instanceFlags)
	{
		COLLISION_INFO::StaticInstance instance;
		if (!COLLISION_INFO::ReadCompoundInstance(g_staticCacheFor[compound], index, instance))
			return false;
		filter = instance.filter;
		instanceFlags = instance.instanceFlags;
		return true;
	}

	struct Census
	{
		bool made = false;
		int rows = 0, fileRows = 0;
		int rowsByStorage[4] = {}; // its own body, landscape, world, ballistic
		int ghosted = 0, withModel = 0, withBody = 0, missingInstance = 0;
		struct Part { int instances, disabled, noRow, mismatched, off; int layers[32]; } parts[4] = {};
		std::vector<std::string> lines;
	};
	Census g_census;

	void MakeCensus(Census& census)
	{
		census = Census();
		census.made = true;
		census.rows = COLLISION_INFO::RowCount();
		census.fileRows = COLLISION_INFO::FileRowCount();
		for (int c = 0; c < 4; c++)
		{
			const uint8_t* compound = COLLISION_INFO::Compound(kCompounds[c].address);
			const int count = COLLISION_INFO::CompoundInstanceCount(compound);
			Census::Part& part = census.parts[c];
			part.instances = count;
			for (int i = 0; i < count; i++)
			{
				COLLISION_INFO::StaticInstance instance;
				if (!COLLISION_INFO::ReadCompoundInstance(compound, i, instance))
					continue;
				part.layers[instance.filter & 0x1F]++;
				if (instance.instanceFlags & 0x10)
					part.disabled++;
				if ((instance.filter & 0x1F) == COLLISION_INFO::kLayerNothing)
					part.off++;
				COLLISION_INFO::Row row;
				if (instance.row < 0 || !COLLISION_INFO::ReadRow(instance.row, row))
					part.noRow++;
				else if (row.collisionIndex != i)
					part.mismatched++;
			}
		}
		for (int r = 0; r < census.rows; r++)
		{
			COLLISION_INFO::Row row;
			if (!COLLISION_INFO::ReadRow(r, row) || (r >= census.fileRows && row.flags == 0 && row.entityGuid == 0))
				continue;
			const uint32_t storage = row.flags & COLLISION_INFO::kStorageMask;
			census.rowsByStorage[storage == COLLISION_INFO::kStorageLandscape ? 1 : storage == COLLISION_INFO::kStorageWorld ? 2 : storage == COLLISION_INFO::kStorageBallistic ? 3 : 0]++;
			if (row.flags & COLLISION_INFO::kGhosted)
				census.ghosted++;
			if (row.instance)
				census.withModel++;
			if (row.body)
				census.withBody++;
			COLLISION_INFO::StaticInstance instance;
			if (storage && !COLLISION_INFO::RowStaticInstance(row, instance, nullptr))
				census.missingInstance++;
		}

		census.lines.push_back(Format("Collision map: %d rows from the file, room for %d (spares for collision made while the level runs)", census.fileRows, census.rows));
		census.lines.push_back(Format("Rows in use: %d with a body of their own, %d landscape, %d static world, %d ballistic only", census.rowsByStorage[0], census.rowsByStorage[1],
			census.rowsByStorage[2], census.rowsByStorage[3]));
		census.lines.push_back(Format("Rows with collision off: %d; with a model instance: %d; with a body: %d; stored statically but with no instance there: %d", census.ghosted,
			census.withModel, census.withBody, census.missingInstance));
		for (int c = 0; c < 4; c++)
		{
			const Census::Part& part = census.parts[c];
			census.lines.push_back(Format("%s compound: %d instances, %d disabled, %d on the \"nothing\" layer (collision off), %d with no row, %d whose row points elsewhere",
				kCompounds[c].name, part.instances, part.disabled, part.off, part.noRow, part.mismatched));
			std::string layers;
			for (int l = 0; l < 32; l++)
				if (part.layers[l])
					layers += Format("%s%s %d", layers.empty() ? "" : ", ", COLLISION_INFO::LayerName(l), part.layers[l]);
			if (!layers.empty())
				census.lines.push_back("  by layer: " + layers);
		}
	}

	ImU32 StaticColour(const StaticBox& box, uint32_t filter, uint32_t instanceFlags, uint32_t zoneId, int mode, int alpha)
	{
		const int layer = static_cast<int>(filter & 0x1F);
		switch (mode)
		{
		case 1:
			return HashColour(zoneId, alpha);
		case 2:
			return (kCompounds[box.compound].colour & 0x00FFFFFF) | (static_cast<ImU32>(alpha) << 24);
		case 3:
			if (instanceFlags & 0x10)
				return IM_COL32(255, 60, 255, alpha);
			if (layer == COLLISION_INFO::kLayerNothing)
				return IM_COL32(255, 60, 60, alpha);
			if (box.row < 0)
				return IM_COL32(255, 255, 0, alpha);
			return IM_COL32(170, 170, 170, alpha);
		default:
			return LayerColour(layer, alpha);
		}
	}

	struct Near
	{
		float distance;
		int index;
	};

	// The boxes near the camera, nearest first (up to max)
	void NearStatic(const float camera[3], float radius, std::vector<Near>& out, size_t max)
	{
		out.clear();
		for (size_t i = 0; i < g_staticCache.size(); i++)
		{
			const StaticBox& box = g_staticCache[i];
			const float distance = (std::max)(0.0f, Distance(box.matrix + 12, camera) - box.radius);
			if (distance <= radius)
				out.push_back({ distance, static_cast<int>(i) });
		}
		const size_t keep = (std::min)(out.size(), max);
		std::partial_sort(out.begin(), out.begin() + keep, out.end(), [](const Near& a, const Near& b) { return a.distance < b.distance; });
		out.resize(keep);
	}

	void DrawStatic(ImDrawList* drawList, const float camera[3])
	{
		BuildStaticCache();
		const float radius = (std::max)(1.0f, g_staticRadius.load());
		const int mode = static_cast<int>(g_staticColour.load() + 0.5f);
		std::vector<Near> nearby;
		NearStatic(camera, radius, nearby, kMaxDrawnBoxes);
		int labels = 0;
		for (const Near& entry : nearby)
		{
			const StaticBox& box = g_staticCache[entry.index];
			uint32_t filter = 0, instanceFlags = 0;
			if (!InstanceState(box.compound, box.index, filter, instanceFlags))
				continue;
			COLLISION_INFO::Row row;
			const bool haveRow = box.row >= 0 && COLLISION_INFO::ReadRow(box.row, row);
			const int alpha = static_cast<int>(230.0f * (1.0f - 0.7f * entry.distance / radius));
			const ImU32 colour = StaticColour(box, filter, instanceFlags, haveRow ? row.zoneId : 0, mode, alpha);
			if (box.haveBox)
				DEBUG_MENU::WorldOrientedBox(drawList, box.matrix, box.half, colour, 1.0f);
			else
				DEBUG_MENU::WorldCross(drawList, box.matrix + 12, 0.2f, colour, 1.0f);
			if (g_staticLabels && labels < 40)
			{
				labels++;
				std::string text = Format("row %d %s", box.row, COLLISION_INFO::LayerName(filter & 0x1F));
				if (instanceFlags & 0x10)
					text += " (disabled)";
				DEBUG_MENU::WorldText(drawList, box.matrix + 12, text.c_str(), colour);
			}
		}
	}

	// ======== Bodies and phantoms ========

	struct Body
	{
		const uint8_t* object;
		int island;          // 0 fixed, 1 active, 2 sleeping
		int shapeType;
		int motionType;
		uint32_t filter;
		uint32_t userData;
		const char* name;
		float axes[3][3];
		float position[3];
		float velocity[3];
		bool haveBox;
		float min[3], max[3];
	};

	bool ReadBodyAt(const uint8_t* object, int island, Body& out)
	{
		memset(&out, 0, sizeof(out));
		__try
		{
			out.object = object;
			out.island = island;
			const uint8_t* shape = Pointer(object, kObjectShape);
			out.shapeType = shape ? shape[0x08] : -1;
			out.motionType = object[kBodyMotionType];
			out.filter = Read<uint32_t>(object, kObjectFilter);
			out.userData = Read<uint32_t>(object, kObjectUserData);
			out.name = reinterpret_cast<const char*>(Read<uintptr_t>(object, kObjectName) & ~static_cast<uintptr_t>(1));
			for (int i = 0; i < 3; i++)
				memcpy(out.axes[i], object + kBodyTransform + i * 0x10, sizeof(out.axes[i]));
			memcpy(out.position, object + kBodyTransform + 0x30, sizeof(out.position));
			memcpy(out.velocity, object + kBodyVelocity, sizeof(out.velocity));
			return std::isfinite(out.position[0]) && std::isfinite(out.position[1]) && std::isfinite(out.position[2]);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// An island's bodies, and an array of islands
	int IslandBodies(const uint8_t* island, const uint8_t** out, int max)
	{
		__try
		{
			if (!island)
				return 0;
			const uint8_t* const* bodies = reinterpret_cast<const uint8_t* const*>(Pointer(island, kIslandBodies));
			const int count = Read<int>(island, kIslandBodies + 4);
			int found = 0;
			for (int i = 0; bodies && i < count && found < max; i++)
				if (bodies[i])
					out[found++] = bodies[i];
			return found;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	int Islands(const uint8_t* world, uint32_t offset, const uint8_t** out, int max)
	{
		__try
		{
			const uint8_t* const* islands = reinterpret_cast<const uint8_t* const*>(Pointer(world, offset));
			const int count = Read<int>(world, offset + 4);
			int found = 0;
			for (int i = 0; islands && i < count && found < max; i++)
				if (islands[i])
					out[found++] = islands[i];
			return found;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	const uint8_t* FixedIsland(const uint8_t* world)
	{
		__try
		{
			return Pointer(world, kWorldFixedIsland);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	// Every body of the world (the static compound bodies left out), with a count of each kind
	struct BodyCounts
	{
		int fixed = 0, active = 0, sleeping = 0, triggers = 0, staticCompounds = 0, activeIslands = 0, sleepingIslands = 0;
	};

	bool IsTrigger(int shapeType)
	{
		return shapeType == 0x1E || shapeType == 0x20;
	}

	void ReadBodies(std::vector<Body>& out, BodyCounts& counts)
	{
		out.clear();
		counts = BodyCounts();
		const uint8_t* world = World();
		if (!world)
			return;
		static const uint8_t* islands[1024];
		static const uint8_t* bodies[kMaxBodies];
		for (int kind = 0; kind < 3; kind++)
		{
			int islandCount = 0;
			if (kind == 0)
			{
				islands[0] = FixedIsland(world);
				islandCount = islands[0] ? 1 : 0;
			}
			else
				islandCount = Islands(world, kind == 1 ? kWorldActiveIslands : kWorldInactiveIslands, islands, 1024);
			if (kind == 1)
				counts.activeIslands = islandCount;
			if (kind == 2)
				counts.sleepingIslands = islandCount;
			for (int i = 0; i < islandCount; i++)
			{
				const int bodyCount = IslandBodies(islands[i], bodies, kMaxBodies);
				for (int b = 0; b < bodyCount && out.size() < kMaxBodies; b++)
				{
					Body body;
					if (!ReadBodyAt(bodies[b], kind, body))
						continue;
					if (body.shapeType == 0x10)
					{
						counts.staticCompounds++;
						continue;
					}
					body.haveBox = COLLISION_INFO::ShapeLocalBox(Pointer(bodies[b], kObjectShape), body.min, body.max);
					if (IsTrigger(body.shapeType))
						counts.triggers++;
					else if (kind == 0)
						counts.fixed++;
					else if (kind == 1)
						counts.active++;
					else
						counts.sleeping++;
					out.push_back(body);
				}
			}
		}
	}

	ImU32 BodyColour(const Body& body, int alpha)
	{
		if (IsTrigger(body.shapeType))
			return IM_COL32(80, 220, 255, alpha);
		if (body.motionType == 7)
			return IM_COL32(120, 255, 120, alpha);
		if (body.motionType == 4)
			return IM_COL32(200, 120, 255, alpha);
		if (body.island == 0 || body.motionType == 5)
			return IM_COL32(160, 160, 160, alpha);
		if (body.island == 2)
			return IM_COL32(110, 140, 200, alpha);
		return IM_COL32(255, 170, 60, alpha);
	}

	const char* IslandName(int island)
	{
		return island == 0 ? "fixed" : island == 1 ? "awake" : "asleep";
	}

	std::string BodyRowText(uint32_t userData)
	{
		COLLISION_INFO::Row row;
		if (userData == 0xFFFFFFFF || static_cast<int>(userData) >= COLLISION_INFO::RowCount() || !COLLISION_INFO::ReadRow(static_cast<int>(userData), row))
			return Format("user data %08X", userData);
		return Format("row %u (entity %08X in %08X)", userData, row.entityGuid, row.instanceGuid);
	}

	void DrawBodies(ImDrawList* drawList, const float camera[3])
	{
		std::vector<Body> bodies;
		BodyCounts counts;
		ReadBodies(bodies, counts);
		const float radius = (std::max)(1.0f, g_bodiesRadius.load());
		int drawn = 0, labels = 0;
		for (const Body& body : bodies)
		{
			if (drawn >= kMaxDrawnBoxes)
				break;
			if (IsTrigger(body.shapeType) && !g_triggers)
				continue;
			const float distance = Distance(body.position, camera);
			if (distance > radius)
				continue;
			drawn++;
			const int alpha = static_cast<int>(230.0f * (1.0f - 0.6f * distance / radius));
			const ImU32 colour = BodyColour(body, alpha);
			if (body.haveBox)
			{
				float matrix[16], half[3];
				PlaceBox(body.min, body.max, body.axes, body.position, matrix, half);
				DEBUG_MENU::WorldOrientedBox(drawList, matrix, half, colour, IsTrigger(body.shapeType) ? 1.0f : 1.5f);
			}
			else
				DEBUG_MENU::WorldCross(drawList, body.position, 0.2f, colour, 1.5f);
			const float speed = std::sqrt(body.velocity[0] * body.velocity[0] + body.velocity[1] * body.velocity[1] + body.velocity[2] * body.velocity[2]);
			if (body.island == 1 && speed > 0.05f && std::isfinite(speed))
			{
				float tip[3];
				for (int i = 0; i < 3; i++)
					tip[i] = body.position[i] + body.velocity[i] * 0.5f;
				DEBUG_MENU::WorldArrow(drawList, body.position, tip, IM_COL32(255, 255, 120, alpha), 1.5f);
			}
			if (g_bodyLabels && labels < 40)
			{
				labels++;
				const std::string text = Format("%s %s, %s", IslandName(body.island), IsTrigger(body.shapeType) ? "trigger" : COLLISION_INFO::MotionTypeName(body.motionType),
					COLLISION_INFO::LayerName(body.filter & 0x1F));
				DEBUG_MENU::WorldText(drawList, body.position, text.c_str(), colour);
			}
		}
	}

	struct Phantom
	{
		bool haveShape;
		uint32_t filter;
		float min[3], max[3];
		float axes[3][3];
		float position[3];
	};

	int ReadPhantoms(Phantom* out, int max)
	{
		__try
		{
			const uint8_t* world = World();
			if (!world)
				return 0;
			const uint8_t* const* phantoms = reinterpret_cast<const uint8_t* const*>(Pointer(world, kWorldPhantoms));
			const int count = Read<int>(world, kWorldPhantoms + 4);
			int found = 0;
			for (int i = 0; phantoms && i < count && found < max; i++)
			{
				const uint8_t* phantom = phantoms[i];
				if (!phantom)
					continue;
				Phantom& entry = out[found];
				const uint8_t* shape = Pointer(phantom, kObjectShape);
				entry.filter = Read<uint32_t>(phantom, kObjectFilter);
				entry.haveShape = shape != nullptr;
				if (shape)
				{
					for (int a = 0; a < 3; a++)
						memcpy(entry.axes[a], phantom + kPhantomTransform + a * 0x10, sizeof(entry.axes[a]));
					memcpy(entry.position, phantom + kPhantomTransform + 0x30, sizeof(entry.position));
					if (!COLLISION_INFO::ShapeLocalBox(shape, entry.min, entry.max))
						continue;
				}
				else
				{
					memcpy(entry.min, phantom + kPhantomBox, sizeof(entry.min));
					memcpy(entry.max, phantom + kPhantomBox + 0x10, sizeof(entry.max));
				}
				found++;
			}
			return found;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	void DrawPhantoms(ImDrawList* drawList, const float camera[3])
	{
		static Phantom phantoms[512];
		const int count = ReadPhantoms(phantoms, 512);
		const float radius = (std::max)(1.0f, g_bodiesRadius.load());
		for (int i = 0; i < count; i++)
		{
			const Phantom& phantom = phantoms[i];
			const ImU32 colour = IM_COL32(255, 120, 220, 200);
			if (phantom.haveShape)
			{
				if (Distance(phantom.position, camera) > radius)
					continue;
				float matrix[16], half[3];
				PlaceBox(phantom.min, phantom.max, phantom.axes, phantom.position, matrix, half);
				DEBUG_MENU::WorldOrientedBox(drawList, matrix, half, colour, 1.0f);
			}
			else
			{
				const float centre[3] = { (phantom.min[0] + phantom.max[0]) * 0.5f, (phantom.min[1] + phantom.max[1]) * 0.5f, (phantom.min[2] + phantom.max[2]) * 0.5f };
				if (Distance(centre, camera) > radius * 2.0f)
					continue;
				DEBUG_MENU::WorldBox(drawList, phantom.min, phantom.max, colour, 1.0f);
			}
		}
	}

	// ======== Character capsules ========

	// A character's body holds a list of shapes, one for each capsule kind (only the controller's own kind collides; the
	// rest are on the "nothing" layer), each a convex hull - an upright prism, its foot narrowed for stepping - moved up
	// by a transform so its foot is at the body's position. Shape kinds (the byte at +8): 1 cylinder, 4 capsule, 5 convex
	// hull, 8 list, 14 transformed.
	constexpr uint8_t kShapeCylinder = 0x01;
	constexpr uint8_t kShapeCapsule = 0x04;
	constexpr uint8_t kShapeConvexHull = 0x05;
	constexpr uint8_t kShapeList = 0x08;
	constexpr uint8_t kShapeTransformed = 0x0E;
	// A list's shapes (an array: data, count), 0x10 bytes each with the shape first
	constexpr uint32_t kListChildren = 0x18;
	constexpr uint32_t kListChildSize = 0x10;
	// A transformed shape: the shape it moves, and its transform (a rotation's columns, then its position at +0x30)
	constexpr uint32_t kTransformedChild = 0x14;
	constexpr uint32_t kTransformedTransform = 0x30;
	// A convex shape's radius (a capsule's own; the rounding of a hull or cylinder), a cylinder's radius, a capsule's or
	// cylinder's two ends, and a convex hull's box (half its size, then its centre; without the rounding)
	constexpr uint32_t kConvexRadius = 0x10;
	constexpr uint32_t kCylinderRadius = 0x14;
	constexpr uint32_t kEndA = 0x20;
	constexpr uint32_t kEndB = 0x30;
	constexpr uint32_t kHullHalfSize = 0x20;
	constexpr uint32_t kHullCentre = 0x30;

	struct Capsule
	{
		const uint8_t* owner;
		bool initialised;
		int capsuleType;
		int state;
		float support[3];
		bool couldNotBeJostled;
		bool overDrop;
		bool haveShape;
		bool rounded;        // a capsule (round ends); otherwise an upright prism or cylinder (flat ends)
		int shapeType;
		float a[3], b[3];    // the bottom and top of its axis (a capsule: its two ends' centres)
		float radius;
		float position[3];
	};

	bool ReadCapsule(int index, Capsule& out)
	{
		memset(&out, 0, sizeof(out));
		__try
		{
			const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kControllers);
			if (!at)
				return false;
			const uint8_t* controller = reinterpret_cast<const uint8_t*>(at) + index * kControllerSize;
			out.owner = Pointer(controller, 0x00);
			if (!out.owner)
				return false;
			out.initialised = controller[0xA8] != 0;
			out.capsuleType = Read<int>(controller, 0x8C);
			memcpy(out.support, controller + 0x80, sizeof(out.support));
			out.couldNotBeJostled = controller[0xA9] != 0;
			out.overDrop = controller[0xAC] != 0;
			const uint8_t* context = Pointer(controller, 0x10);
			out.state = context ? Read<int>(context, 0x10) : -1;
			const uint8_t* capsuleBody = Pointer(controller, 0x0C);
			const uint8_t* body = capsuleBody ? Pointer(capsuleBody, 0x10) : nullptr;
			if (!body)
				return true;
			float axes[3][3];
			for (int i = 0; i < 3; i++)
				memcpy(axes[i], body + kBodyTransform + i * 0x10, sizeof(axes[i]));
			memcpy(out.position, body + kBodyTransform + 0x30, sizeof(out.position));
			const uint8_t* shape = Pointer(body, kObjectShape);
			out.shapeType = shape ? shape[0x08] : -1;
			// The list: the shape of the controller's capsule kind
			if (shape && shape[0x08] == kShapeList)
			{
				const uint8_t* children = Pointer(shape, kListChildren);
				const int count = Read<int>(shape, kListChildren + 4);
				shape = children && out.capsuleType >= 0 && out.capsuleType < count && count <= 64 ? Pointer(children, out.capsuleType * kListChildSize) : nullptr;
			}
			// Its transform within the body (none: as the body)
			float local[3][3] = { { 1.0f, 0.0f, 0.0f }, { 0.0f, 1.0f, 0.0f }, { 0.0f, 0.0f, 1.0f } };
			float offset[3] = {};
			if (shape && shape[0x08] == kShapeTransformed)
			{
				for (int i = 0; i < 3; i++)
					memcpy(local[i], shape + kTransformedTransform + i * 0x10, sizeof(local[i]));
				memcpy(offset, shape + kTransformedTransform + 0x30, sizeof(offset));
				shape = Pointer(shape, kTransformedChild);
			}
			if (!shape)
				return true;
			out.shapeType = shape[0x08];
			float a[3], b[3];
			if (out.shapeType == kShapeCapsule || out.shapeType == kShapeCylinder)
			{
				memcpy(a, shape + kEndA, sizeof(a));
				memcpy(b, shape + kEndB, sizeof(b));
				out.radius = Read<float>(shape, kConvexRadius) + (out.shapeType == kShapeCylinder ? Read<float>(shape, kCylinderRadius) : 0.0f);
				out.rounded = out.shapeType == kShapeCapsule;
			}
			else if (out.shapeType == kShapeConvexHull)
			{
				// An upright prism: its box's middle, from the box's bottom to its top, as wide as the box (with the rounding)
				float half[3], centre[3];
				memcpy(half, shape + kHullHalfSize, sizeof(half));
				memcpy(centre, shape + kHullCentre, sizeof(centre));
				const float rounding = Read<float>(shape, kConvexRadius);
				for (int j = 0; j < 3; j++)
					a[j] = b[j] = centre[j];
				a[1] -= half[1] + rounding;
				b[1] += half[1] + rounding;
				out.radius = (std::max)(half[0], half[2]) + rounding;
			}
			else
				return true;
			if (!std::isfinite(out.radius) || out.radius <= 0.0f || out.radius > 10.0f)
				return true;
			// Into the body's space, then the world's
			float bodyA[3], bodyB[3];
			for (int j = 0; j < 3; j++)
			{
				bodyA[j] = offset[j] + local[0][j] * a[0] + local[1][j] * a[1] + local[2][j] * a[2];
				bodyB[j] = offset[j] + local[0][j] * b[0] + local[1][j] * b[1] + local[2][j] * b[2];
			}
			for (int j = 0; j < 3; j++)
			{
				out.a[j] = out.position[j] + axes[0][j] * bodyA[0] + axes[1][j] * bodyA[1] + axes[2][j] * bodyA[2];
				out.b[j] = out.position[j] + axes[0][j] * bodyB[0] + axes[1][j] * bodyB[1] + axes[2][j] * bodyB[2];
			}
			out.haveShape = true;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	const char* CapsuleTypeName(int type)
	{
		static const char* const kNames[] = { "standing", "crouched", "player crouched", "deep crouch", "vent", "traversal", "facehugger", "NPC", "NPC crouched" };
		return type >= 0 && type < 9 ? kNames[type] : "?";
	}

	const char* CharacterStateName(int state)
	{
		static const char* const kNames[] = { "on the ground", "jumping", "in the air", "climbing", "flying", "custom 0", "custom 1", "custom 2", "custom 3", "custom 4", "custom 5" };
		return state >= 0 && state < 11 ? kNames[state] : "?";
	}

	std::string OwnerName(const uint8_t* owner)
	{
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
			if (character.object == owner)
				return character.name + " (" + DEBUG_MENU::ClassName(character.characterClass) + ")";
		return Format("character %p", owner);
	}

	void DrawCapsules(ImDrawList* drawList)
	{
		for (int i = 0; i < kControllerCount; i++)
		{
			Capsule capsule;
			if (!ReadCapsule(i, capsule) || !capsule.initialised)
				continue;
			const ImU32 colour = capsule.overDrop ? IM_COL32(255, 90, 90, 230) : IM_COL32(120, 255, 160, 230);
			if (capsule.haveShape)
			{
				DEBUG_MENU::WorldCircle(drawList, capsule.a, capsule.radius, colour, 20, 1.5f);
				DEBUG_MENU::WorldCircle(drawList, capsule.b, capsule.radius, colour, 20, 1.5f);
				const float offsets[4][2] = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };
				for (const auto& offset : offsets)
				{
					const float a[3] = { capsule.a[0] + offset[0] * capsule.radius, capsule.a[1], capsule.a[2] + offset[1] * capsule.radius };
					const float b[3] = { capsule.b[0] + offset[0] * capsule.radius, capsule.b[1], capsule.b[2] + offset[1] * capsule.radius };
					DEBUG_MENU::WorldLine(drawList, a, b, colour, 1.5f);
				}
			}
			DEBUG_MENU::WorldCross(drawList, capsule.support, 0.1f, IM_COL32(255, 255, 120, 230), 1.5f);
			const float top[3] = { capsule.position[0], (std::max)(capsule.a[1], capsule.b[1]) + (capsule.rounded ? capsule.radius : 0.0f) + 0.15f, capsule.position[2] };
			const std::string text = Format("%s, %s%s", CharacterStateName(capsule.state), CapsuleTypeName(capsule.capsuleType), capsule.overDrop ? ", over a drop" : "");
			DEBUG_MENU::WorldText(drawList, capsule.haveShape ? top : capsule.position, text.c_str(), colour);
		}
	}

	// ======== Physics systems (PHYSICS.MAP) ========

	struct System
	{
		int index;
		uint32_t systemIndex;
		uint32_t resource;
		uint32_t instance;
		uint32_t entity;
		uint32_t parent;
		float matrix[12];
		const uint8_t* system;
		const char* name;
		bool active;
		int bodies;
		float bodyPositions[8][3];
		float bodyAxes[8][3][3];
	};

	int SystemCount()
	{
		__try
		{
			const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kSystemCount);
			const uintptr_t data = DEVTOOLS_RELATIVE_ADDRESS(kSystems);
			if (!at || !data || !*reinterpret_cast<const void* const*>(data))
				return 0;
			const uint32_t count = *reinterpret_cast<const uint32_t*>(at);
			return count > 0x10000 ? 0 : static_cast<int>(count);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	bool ReadSystem(int index, System& out)
	{
		memset(&out, 0, sizeof(out));
		__try
		{
			const uint8_t* systems = *reinterpret_cast<const uint8_t* const*>(DEVTOOLS_RELATIVE_ADDRESS(kSystems));
			if (!systems)
				return false;
			const uint8_t* entry = systems + index * kSystemSize;
			out.index = index;
			out.systemIndex = Read<uint32_t>(entry, 0x00);
			out.resource = Read<uint32_t>(entry, 0x08);
			out.instance = Read<uint32_t>(entry, 0x0C);
			out.entity = Read<uint32_t>(entry, 0x10);
			out.parent = Read<uint32_t>(entry, 0x14);
			memcpy(out.matrix, entry + 0x18, sizeof(out.matrix));
			out.system = Pointer(entry, 0x48);
			if (!out.system)
				return true;
			out.name = reinterpret_cast<const char*>(Read<uintptr_t>(out.system, 0x38) & ~static_cast<uintptr_t>(1));
			out.active = out.system[0x40] != 0;
			const uint8_t* const* bodies = reinterpret_cast<const uint8_t* const*>(Pointer(out.system, 0x08));
			out.bodies = Read<int>(out.system, 0x0C);
			for (int b = 0; bodies && b < out.bodies && b < 8; b++)
			{
				const uint8_t* body = bodies[b];
				if (!body)
					continue;
				for (int i = 0; i < 3; i++)
					memcpy(out.bodyAxes[b][i], body + kBodyTransform + i * 0x10, sizeof(out.bodyAxes[b][i]));
				memcpy(out.bodyPositions[b], body + kBodyTransform + 0x30, sizeof(out.bodyPositions[b]));
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// The map's transform: axis i is (m[i], m[4 + i], m[8 + i]), the position (m[3], m[7], m[11])
	void SystemAxes(const System& system, float axes[3][3], float position[3])
	{
		for (int i = 0; i < 3; i++)
			for (int j = 0; j < 3; j++)
				axes[i][j] = system.matrix[j * 4 + i];
		for (int j = 0; j < 3; j++)
			position[j] = system.matrix[j * 4 + 3];
	}

	void DrawAxes(ImDrawList* drawList, const float position[3], const float axes[3][3], float length, int alpha)
	{
		const ImU32 colours[3] = { IM_COL32(255, 80, 80, alpha), IM_COL32(80, 255, 80, alpha), IM_COL32(80, 140, 255, alpha) };
		for (int i = 0; i < 3; i++)
		{
			float tip[3];
			for (int j = 0; j < 3; j++)
				tip[j] = position[j] + axes[i][j] * length;
			DEBUG_MENU::WorldLine(drawList, position, tip, colours[i], 2.0f);
		}
	}

	void DrawSystems(ImDrawList* drawList, const float camera[3])
	{
		const int count = SystemCount();
		const float radius = (std::max)(1.0f, g_bodiesRadius.load()) * 2.0f;
		for (int i = 0; i < count; i++)
		{
			System system;
			if (!ReadSystem(i, system))
				continue;
			float axes[3][3], position[3];
			SystemAxes(system, axes, position);
			if (!std::isfinite(position[0]) || Distance(position, camera) > radius)
				continue;
			DrawAxes(drawList, position, axes, 0.5f, 230);
			const std::string text = Format("physics system %d (entity %08X)", i, system.entity);
			DEBUG_MENU::WorldText(drawList, position, text.c_str(), IM_COL32(255, 230, 160, 255));
			for (int b = 0; b < system.bodies && b < 8; b++)
			{
				DEBUG_MENU::WorldLine(drawList, position, system.bodyPositions[b], IM_COL32(255, 230, 160, 120), 1.0f);
				DrawAxes(drawList, system.bodyPositions[b], system.bodyAxes[b], 0.25f, 200);
			}
		}
	}

	// ======== Collision layers ========

	bool ReadLayerTable(uint32_t table[32], int& filterType)
	{
		filterType = -1;
		__try
		{
			const uint8_t* world = World();
			const uint8_t* filter = world ? Pointer(world, kWorldFilter) : nullptr;
			for (int depth = 0; filter && depth < 3; depth++)
			{
				filterType = Read<int>(filter, kFilterType);
				if (filterType == 2)
				{
					memcpy(table, filter + kFilterTable, 32 * sizeof(uint32_t));
					return true;
				}
				if (filterType != 5 && filterType != 6)
					return false;
				filter = Pointer(filter, kFilterChild);
			}
			return false;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// ======== The game's "show collision meshes" switch ========

	bool ReadShowCollisionMeshes(bool& value)
	{
		__try
		{
			const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kShowCollisionMeshes);
			if (!at)
				return false;
			value = *reinterpret_cast<const uint8_t*>(at) != 0;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	void WriteShowCollisionMeshes(bool value)
	{
		__try
		{
			const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kShowCollisionMeshes);
			if (at)
				*reinterpret_cast<volatile uint8_t*>(at) = value ? 1 : 0;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
		}
	}

	void ApplyShowCollisionMeshes()
	{
		bool current = false;
		if (!ReadShowCollisionMeshes(current))
			return;
		const bool wanted = g_showCollisionMeshes;
		if (wanted != current && (wanted || g_showCollisionMeshesSet))
		{
			WriteShowCollisionMeshes(wanted);
			g_showCollisionMeshesSet = wanted;
		}
	}

	// ======== The key to the colours ========

	struct LegendItem
	{
		ImU32 colour;
		const char* text;
	};

	// What the colours of the overlays switched on mean (the tab shows it, and a key is drawn over the game)
	std::vector<LegendItem> LegendItems()
	{
		std::vector<LegendItem> items;
		if (g_staticBoxes)
		{
			switch (static_cast<int>(g_staticColour.load() + 0.5f))
			{
			case 1:
				items.push_back({ HashColour(0x4FCD64D5u, 255), "static collision: a colour per zone" });
				break;
			case 2:
				for (const Compound& compound : kCompounds)
					items.push_back({ compound.colour, compound.name });
				break;
			case 3:
				items.push_back({ IM_COL32(170, 170, 170, 255), "static collision on" });
				items.push_back({ IM_COL32(255, 60, 60, 255), "collision off" });
				items.push_back({ IM_COL32(255, 60, 255, 255), "disabled" });
				items.push_back({ IM_COL32(255, 255, 0, 255), "no row" });
				break;
			default:
				items.push_back({ LayerColour(3, 255), "static collision: a colour per layer (labels name it)" });
				break;
			}
		}
		if (g_bodies)
		{
			items.push_back({ IM_COL32(255, 170, 60, 255), "awake" });
			items.push_back({ IM_COL32(110, 140, 200, 255), "asleep" });
			items.push_back({ IM_COL32(200, 120, 255, 255), "keyframed" });
			items.push_back({ IM_COL32(160, 160, 160, 255), "fixed" });
			items.push_back({ IM_COL32(120, 255, 120, 255), "character bodies" });
			if (g_triggers)
				items.push_back({ IM_COL32(80, 220, 255, 255), "trigger volumes" });
			items.push_back({ IM_COL32(255, 255, 120, 255), "arrows: velocity" });
		}
		if (g_phantoms)
			items.push_back({ IM_COL32(255, 120, 220, 255), "phantoms" });
		if (g_capsules)
		{
			items.push_back({ IM_COL32(120, 255, 160, 255), "character capsules" });
			items.push_back({ IM_COL32(255, 90, 90, 255), "capsule over a drop" });
			items.push_back({ IM_COL32(255, 255, 120, 255), "cross: where it stands" });
		}
		if (g_systems)
			items.push_back({ IM_COL32(255, 230, 160, 255), "physics systems (lines to their bodies)" });
		return items;
	}

	// In the tab: a swatch and its words each, as many to a line as fit the window
	void DrawLegend(const std::vector<LegendItem>& items)
	{
		const float swatch = 11.0f * DEBUG_MENU::Scale();
		const float spacing = ImGui::GetStyle().ItemInnerSpacing.x;
		const float right = ImGui::GetCursorScreenPos().x + ImGui::GetContentRegionAvail().x;
		ImDrawList* drawList = ImGui::GetWindowDrawList();
		for (size_t i = 0; i < items.size(); i++)
		{
			const float width = swatch + spacing + ImGui::CalcTextSize(items[i].text).x;
			if (i)
			{
				ImGui::SameLine(0.0f, spacing * 3.0f);
				if (ImGui::GetCursorScreenPos().x + width > right)
					ImGui::NewLine();
			}
			const ImVec2 at = ImGui::GetCursorScreenPos();
			const float top = at.y + (ImGui::GetTextLineHeight() - swatch) * 0.5f;
			drawList->AddRectFilled(ImVec2(at.x, top), ImVec2(at.x + swatch, top + swatch), items[i].colour | IM_COL32_A_MASK, 2.0f);
			ImGui::Dummy(ImVec2(swatch, ImGui::GetTextLineHeight()));
			ImGui::SameLine(0.0f, spacing);
			ImGui::TextUnformatted(items[i].text);
		}
	}

	// Over the game: the same key, in a panel at the right edge of the screen (left out where the window covers it)
	void DrawLegendOverlay(ImDrawList* drawList)
	{
		if (!g_legend)
			return;
		const std::vector<LegendItem> items = LegendItems();
		if (items.empty())
			return;
		const float scale = DEBUG_MENU::Scale();
		const float size = 14.0f * scale;
		const float swatch = 10.0f * scale;
		const float gap = 6.0f * scale;
		const float line = size + 3.0f * scale;
		ImFont* font = ImGui::GetFont();
		const char* const title = "Physics overlay";
		float width = font->CalcTextSizeA(size, FLT_MAX, 0.0f, title).x;
		for (const LegendItem& item : items)
			width = (std::max)(width, swatch + gap + font->CalcTextSizeA(size, FLT_MAX, 0.0f, item.text).x);
		const ImVec2 screen = ImGui::GetIO().DisplaySize;
		const float height = line * static_cast<float>(items.size() + 1);
		const ImVec2 panelMin(screen.x - width - 2.0f * gap - 12.0f * scale, (std::max)(0.0f, (screen.y - height) * 0.5f));
		const ImVec2 panelMax(panelMin.x + width + 2.0f * gap, panelMin.y + height + gap);
		ImVec2 windowMin, windowMax;
		if (DEBUG_MENU::WindowRect(windowMin, windowMax) && windowMin.x < panelMax.x && windowMax.x > panelMin.x && windowMin.y < panelMax.y && windowMax.y > panelMin.y)
			return;
		drawList->AddRectFilled(panelMin, panelMax, IM_COL32(0, 0, 0, 150), 4.0f);
		float y = panelMin.y + gap * 0.5f;
		drawList->AddText(font, size, ImVec2(panelMin.x + gap, y), IM_COL32(230, 230, 230, 255), title);
		for (const LegendItem& item : items)
		{
			y += line;
			const float top = y + (size - swatch) * 0.5f;
			drawList->AddRectFilled(ImVec2(panelMin.x + gap, top), ImVec2(panelMin.x + gap + swatch, top + swatch), item.colour | IM_COL32_A_MASK, 2.0f);
			drawList->AddText(font, size, ImVec2(panelMin.x + gap + swatch + gap, y), IM_COL32(230, 230, 230, 255), item.text);
		}
	}

	// ======== Overlay and tab ========

	void DrawOverlay(ImDrawList* drawList)
	{
		ApplyShowCollisionMeshes();
		if (!g_staticBoxes && !g_bodies && !g_phantoms && !g_capsules && !g_systems)
			return;
		float camera[3];
		if (!DEBUG_MENU::CameraPosition(camera) || !LevelRunning() || !World())
			return;
		if (g_staticBoxes)
			DrawStatic(drawList, camera);
		if (g_bodies)
			DrawBodies(drawList, camera);
		if (g_phantoms)
			DrawPhantoms(drawList, camera);
		if (g_capsules)
			DrawCapsules(drawList);
		if (g_systems)
			DrawSystems(drawList, camera);
		DrawLegendOverlay(drawList);
	}

	void Checkbox(const char* label, std::atomic<bool>& value)
	{
		bool on = value;
		if (ImGui::Checkbox(label, &on))
			value = on;
	}

	void Slider(const char* label, std::atomic<float>& value, float min, float max, const char* format)
	{
		float number = value;
		ImGui::SetNextItemWidth(200.0f * DEBUG_MENU::Scale());
		if (ImGui::SliderFloat(label, &number, min, max, format))
			value = number;
	}

	void DrawTab()
	{
		if (!LevelRunning() || !World())
		{
			ImGui::TextDisabled("No level running.");
			return;
		}

		ImGui::SeparatorText("Static collision (COLLISION.HKX and COLLISION.MAP)");
		if (ImGui::Button("Count"))
			MakeCensus(g_census);
		ImGui::SameLine();
		ImGui::TextDisabled("(reads every instance and row once)");
		if (g_census.made)
			for (const std::string& line : g_census.lines)
				ImGui::TextWrapped("%s", line.c_str());
		Checkbox("Static collision boxes near the camera", g_staticBoxes);
		ImGui::SameLine();
		Checkbox("Labels##static", g_staticLabels);
		Slider("Distance##static", g_staticRadius, 2.0f, 60.0f, "%.0f m");
		const char* const colourModes[] = { "by layer", "by zone", "by compound", "by state (red: collision off, magenta: disabled, yellow: no row)" };
		int mode = (std::max)(0, (std::min)(3, static_cast<int>(g_staticColour.load() + 0.5f)));
		ImGui::SetNextItemWidth(200.0f * DEBUG_MENU::Scale());
		if (ImGui::Combo("Colour", &mode, colourModes, 4))
			g_staticColour = static_cast<float>(mode);
		Checkbox("Show the models flagged as collision (the game's own switch)", g_showCollisionMeshes);
		if (ImGui::IsItemHovered())
			ImGui::SetTooltip("Read by the game as each model is shown: reload a checkpoint to see it everywhere");

		ImGui::SeparatorText("Bodies");
		{
			std::vector<Body> bodies;
			BodyCounts counts;
			ReadBodies(bodies, counts);
			ImGui::TextWrapped("%d awake (%d %s), %d asleep (%d %s), %d fixed, %d triggers, %d static compound bodies", counts.active, counts.activeIslands,
				counts.activeIslands == 1 ? "island" : "islands", counts.sleeping, counts.sleepingIslands, counts.sleepingIslands == 1 ? "island" : "islands", counts.fixed,
				counts.triggers, counts.staticCompounds);
		}
		Checkbox("Bodies near the camera", g_bodies);
		ImGui::SameLine();
		Checkbox("Triggers", g_triggers);
		ImGui::SameLine();
		Checkbox("Labels##bodies", g_bodyLabels);
		ImGui::SameLine();
		Checkbox("Phantoms", g_phantoms);
		Slider("Distance##bodies", g_bodiesRadius, 2.0f, 100.0f, "%.0f m");
		Checkbox("Character capsules", g_capsules);
		ImGui::SameLine();
		Checkbox("Key on screen", g_legend);
		if (ImGui::IsItemHovered())
			ImGui::SetTooltip("A key to the colours, at the right edge of the screen while an overlay here is on");
		const std::vector<LegendItem> legend = LegendItems();
		if (legend.empty())
		{
			ImGui::PushTextWrapPos(0.0f);
			ImGui::TextDisabled("Switch an overlay on for a key to its colours here.");
			ImGui::PopTextWrapPos();
		}
		else
			DrawLegend(legend);
		for (int i = 0; i < kControllerCount; i++)
		{
			Capsule capsule;
			if (!ReadCapsule(i, capsule) || !capsule.initialised)
				continue;
			const std::string shape = capsule.haveShape ? Format(", radius %.2f m, %.2f m tall", capsule.radius,
				Distance(capsule.a, capsule.b) + (capsule.rounded ? 2.0f * capsule.radius : 0.0f)) : std::string();
			ImGui::Bullet();
			ImGui::TextWrapped("%s: %s, %s capsule%s%s%s", OwnerName(capsule.owner).c_str(), CharacterStateName(capsule.state), CapsuleTypeName(capsule.capsuleType),
				shape.c_str(), capsule.overDrop ? ", over a drop" : "", capsule.couldNotBeJostled ? ", could not be jostled" : "");
		}

		ImGui::SeparatorText("Physics systems (PHYSICS.MAP)");
		const int systems = SystemCount();
		ImGui::Text("%d systems", systems);
		ImGui::SameLine();
		Checkbox("Show them (axes at the map's transform, lines to their bodies)", g_systems);
		// 12 rows, the header and the sideways scroll bar. The composite's path comes second, whole on one line (the table scrolls
		// sideways for a long one, the row number kept in view), the ids last at their full width. Only the rows in view are
		// read, so the path's column takes the widest path seen yet (for this many systems), and so holds still as it scrolls
		static float s_compositeWidth = 0.0f;
		static int s_compositeWidthFor = -1;
		if (s_compositeWidthFor != systems)
		{
			s_compositeWidth = ImGui::CalcTextSize("Composite").x;
			s_compositeWidthFor = systems;
		}
		const float rowHeight = ImGui::GetTextLineHeight() + ImGui::GetStyle().CellPadding.y * 2.0f + 1.0f;
		if (systems > 0 && ImGui::BeginTable("systems", 6, ImGuiTableFlags_Borders | ImGuiTableFlags_RowBg | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY | ImGuiTableFlags_SizingFixedFit,
			ImVec2(0.0f, rowHeight * 13.0f + ImGui::GetStyle().ScrollbarSize + 4.0f)))
		{
			const float idWidth = ImGui::CalcTextSize("00000000").x;
			ImGui::TableSetupScrollFreeze(1, 1);
			ImGui::TableSetupColumn("#");
			ImGui::TableSetupColumn("Composite", ImGuiTableColumnFlags_WidthFixed, s_compositeWidth);
			ImGui::TableSetupColumn("Bodies");
			ImGui::TableSetupColumn("Position");
			ImGui::TableSetupColumn("Entity", ImGuiTableColumnFlags_WidthFixed, idWidth);
			ImGui::TableSetupColumn("Composite instance");
			ImGui::TableHeadersRow();
			ImGuiListClipper clipper;
			clipper.Begin(systems);
			while (clipper.Step())
				for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
				{
					System system;
					if (!ReadSystem(i, system))
						continue;
					float axes[3][3], position[3];
					SystemAxes(system, axes, position);
					COLLISION_INFO::CompositeInstance composite;
					COLLISION_INFO::FindCompositeInstance(system.parent, composite);
					ImGui::TableNextRow();
					ImGui::TableNextColumn();
					ImGui::Text("%d", i);
					ImGui::TableNextColumn();
					ImGui::TextUnformatted(composite.found ? composite.name : "-");
					if (composite.found)
					{
						s_compositeWidth = (std::max)(s_compositeWidth, ImGui::CalcTextSize(composite.name).x);
						if (ImGui::IsItemHovered())
							ImGui::SetTooltip("%s", composite.name);
					}
					ImGui::TableNextColumn();
					ImGui::Text("%d%s", system.bodies, system.system && !system.active ? " (inactive)" : "");
					ImGui::TableNextColumn();
					ImGui::Text("%.2f, %.2f, %.2f", position[0], position[1], position[2]);
					ImGui::TableNextColumn();
					ImGui::Text("%08X", system.entity);
					ImGui::TableNextColumn();
					ImGui::Text("%08X", system.parent);
				}
			ImGui::EndTable();
		}

		ImGui::SeparatorText("Collision layers");
		uint32_t table[32];
		int filterType = -1;
		const bool haveTable = ReadLayerTable(table, filterType);
		ImGui::PushTextWrapPos(0.0f);
		if (!haveTable)
			ImGui::TextDisabled("The world's collision filter is not a layer filter (kind %d)", filterType);
		else
			ImGui::TextDisabled("Which layers collide (green) - rows and columns are layers 0 to 31; hover for names");
		ImGui::PopTextWrapPos();
		if (haveTable)
		{
			const float cell = 11.0f * DEBUG_MENU::Scale();
			const ImVec2 origin = ImGui::GetCursorScreenPos();
			ImDrawList* drawList = ImGui::GetWindowDrawList();
			ImGui::InvisibleButton("layers", ImVec2(cell * 32.0f + 1.0f, cell * 32.0f + 1.0f));
			const bool hovered = ImGui::IsItemHovered();
			const ImVec2 mouse = ImGui::GetIO().MousePos;
			for (int a = 0; a < 32; a++)
				for (int b = 0; b < 32; b++)
				{
					const ImVec2 cellMin(origin.x + b * cell, origin.y + a * cell), cellMax(cellMin.x + cell - 1.0f, cellMin.y + cell - 1.0f);
					const bool collide = (table[a] >> b) & 1;
					drawList->AddRectFilled(cellMin, cellMax, collide ? IM_COL32(70, 170, 90, 255) : IM_COL32(60, 60, 60, 255));
					if (hovered && mouse.x >= cellMin.x && mouse.x < cellMax.x + 1.0f && mouse.y >= cellMin.y && mouse.y < cellMax.y + 1.0f)
						ImGui::SetTooltip("%d %s and %d %s: %s", a, COLLISION_INFO::LayerName(a), b, COLLISION_INFO::LayerName(b), collide ? "collide" : "do not collide");
				}
		}
	}

	// ======== Commands ========

	DEBUG_MENU::CommandResult CensusCommand(const std::string&)
	{
		return DEBUG_MENU::AnswerOnRenderThread([]()
		{
			if (!LevelRunning() || !World())
				return DEBUG_MENU::CommandResult{ false, "No level running" };
			Census census;
			MakeCensus(census);
			std::string text;
			for (const std::string& line : census.lines)
				text += (text.empty() ? "" : "\n") + line;
			return DEBUG_MENU::CommandResult{ true, text };
		}, 8000);
	}

	float RadiusArgument(const std::string& argument, float fallback)
	{
		const float value = static_cast<float>(atof(argument.c_str()));
		return value > 0.0f && std::isfinite(value) ? value : fallback;
	}

	DEBUG_MENU::CommandResult ListStaticCommand(const std::string& argument)
	{
		const float radius = RadiusArgument(argument, 10.0f);
		return DEBUG_MENU::AnswerOnRenderThread([radius]()
		{
			float camera[3];
			if (!LevelRunning() || !World() || !DEBUG_MENU::CameraPosition(camera))
				return DEBUG_MENU::CommandResult{ false, "No level running" };
			BuildStaticCache();
			std::vector<Near> nearby;
			NearStatic(camera, radius, nearby, 200);
			std::string text = Format("count=%d", static_cast<int>(nearby.size()));
			for (const Near& entry : nearby)
			{
				const StaticBox& box = g_staticCache[entry.index];
				uint32_t filter = 0, instanceFlags = 0;
				InstanceState(box.compound, box.index, filter, instanceFlags);
				COLLISION_INFO::Row row;
				const bool haveRow = box.row >= 0 && COLLISION_INFO::ReadRow(box.row, row);
				text += Format("\n%s %d: row %d, layer %d %s%s, %.1f m, at %.2f,%.2f,%.2f, entity %08X, instance %08X, zone %08X", kCompounds[box.compound].name, box.index, box.row,
					static_cast<int>(filter & 0x1F), COLLISION_INFO::LayerName(filter & 0x1F), (instanceFlags & 0x10) ? " (disabled)" : "", entry.distance, box.matrix[12], box.matrix[13],
					box.matrix[14], haveRow ? row.entityGuid : 0, haveRow ? row.instanceGuid : 0, haveRow ? row.zoneId : 0);
			}
			return DEBUG_MENU::CommandResult{ true, text };
		});
	}

	DEBUG_MENU::CommandResult ListBodiesCommand(const std::string& argument)
	{
		const float radius = RadiusArgument(argument, 25.0f);
		return DEBUG_MENU::AnswerOnRenderThread([radius]()
		{
			float camera[3];
			if (!LevelRunning() || !World() || !DEBUG_MENU::CameraPosition(camera))
				return DEBUG_MENU::CommandResult{ false, "No level running" };
			std::vector<Body> bodies;
			BodyCounts counts;
			ReadBodies(bodies, counts);
			std::string text = Format("awake=%d\nasleep=%d\nfixed=%d\ntriggers=%d\nstatic_compound_bodies=%d", counts.active, counts.sleeping, counts.fixed, counts.triggers,
				counts.staticCompounds);
			int listed = 0;
			for (const Body& body : bodies)
			{
				const float distance = Distance(body.position, camera);
				if (distance > radius || listed >= 200)
					continue;
				listed++;
				char name[64] = "";
				COLLISION_INFO::CopyText(body.name, name, sizeof(name));
				text += Format("\n%s %s, %s, layer %d %s, %.1f m, at %.2f,%.2f,%.2f, %s%s%s", IslandName(body.island),
					IsTrigger(body.shapeType) ? "trigger" : COLLISION_INFO::MotionTypeName(body.motionType), COLLISION_INFO::ShapeTypeName(body.shapeType),
					static_cast<int>(body.filter & 0x1F), COLLISION_INFO::LayerName(body.filter & 0x1F), distance, body.position[0], body.position[1], body.position[2],
					BodyRowText(body.userData).c_str(), name[0] ? ", named " : "", name);
			}
			return DEBUG_MENU::CommandResult{ true, text };
		});
	}

	DEBUG_MENU::CommandResult ListCapsulesCommand(const std::string&)
	{
		return DEBUG_MENU::AnswerOnRenderThread([]()
		{
			std::string text;
			int count = 0;
			for (int i = 0; i < kControllerCount; i++)
			{
				Capsule capsule;
				if (!ReadCapsule(i, capsule) || !capsule.initialised)
					continue;
				count++;
				std::string shape = "shape not read";
				if (capsule.haveShape)
				{
					const float length = Distance(capsule.a, capsule.b);
					shape = Format("%s, radius %.2f m, %.2f m tall, %s %.2f,%.2f,%.2f to %.2f,%.2f,%.2f", COLLISION_INFO::ShapeTypeName(capsule.shapeType), capsule.radius,
						length + (capsule.rounded ? 2.0f * capsule.radius : 0.0f), capsule.rounded ? "ends" : "from", capsule.a[0], capsule.a[1], capsule.a[2], capsule.b[0],
						capsule.b[1], capsule.b[2]);
				}
				text += Format("\n%d %s: %s, %s capsule, %s, stands at %.2f,%.2f,%.2f%s%s", i, OwnerName(capsule.owner).c_str(), CharacterStateName(capsule.state),
					CapsuleTypeName(capsule.capsuleType), shape.c_str(), capsule.support[0], capsule.support[1], capsule.support[2], capsule.overDrop ? ", over a drop" : "",
					capsule.couldNotBeJostled ? ", could not be jostled" : "");
			}
			return DEBUG_MENU::CommandResult{ true, Format("count=%d", count) + text };
		});
	}

	DEBUG_MENU::CommandResult ListSystemsCommand(const std::string&)
	{
		return DEBUG_MENU::AnswerOnRenderThread([]()
		{
			const int count = SystemCount();
			std::string text = Format("count=%d", count);
			for (int i = 0; i < count && i < 1000; i++)
			{
				System system;
				if (!ReadSystem(i, system))
					continue;
				float axes[3][3], position[3];
				SystemAxes(system, axes, position);
				COLLISION_INFO::CompositeInstance composite;
				COLLISION_INFO::FindCompositeInstance(system.parent, composite);
				text += Format("\n%d: system %u, entity %08X, composite instance %08X (%s), instance id %08X, %d bodies%s, at %.2f,%.2f,%.2f, x axis %.3f,%.3f,%.3f", i,
					system.systemIndex, system.entity, system.parent, composite.found ? composite.name : "-", system.instance, system.bodies,
					system.system && !system.active ? " (inactive)" : "", position[0], position[1], position[2], axes[0][0], axes[0][1], axes[0][2]);
			}
			return DEBUG_MENU::CommandResult{ true, text };
		});
	}

	DEBUG_MENU::CommandResult LayersCommand(const std::string&)
	{
		return DEBUG_MENU::AnswerOnRenderThread([]()
		{
			uint32_t table[32];
			int filterType = -1;
			if (!ReadLayerTable(table, filterType))
				return DEBUG_MENU::CommandResult{ false, Format("The world's collision filter is not a layer filter (kind %d), or no level is running", filterType) };
			std::string text;
			for (int a = 0; a < 32; a++)
			{
				std::string with;
				for (int b = 0; b < 32; b++)
					if ((table[a] >> b) & 1)
						with += Format("%s%d", with.empty() ? "" : ",", b);
				text += Format("%s%d %s: %08X collides with %s", a ? "\n" : "", a, COLLISION_INFO::LayerName(a), table[a], with.empty() ? "nothing" : with.c_str());
			}
			return DEBUG_MENU::CommandResult{ true, text };
		});
	}

	void NameControls()
	{
		DEBUG_MENU::AddSwitch("physics.static_boxes", &g_staticBoxes);
		DEBUG_MENU::AddSwitch("physics.static_labels", &g_staticLabels);
		DEBUG_MENU::AddValue("physics.static_radius", &g_staticRadius, 2.0f, 60.0f);
		DEBUG_MENU::AddValue("physics.static_colour", &g_staticColour, 0.0f, 3.0f);
		DEBUG_MENU::AddSwitch("physics.show_collision_meshes", &g_showCollisionMeshes);
		DEBUG_MENU::AddSwitch("physics.bodies", &g_bodies);
		DEBUG_MENU::AddSwitch("physics.body_labels", &g_bodyLabels);
		DEBUG_MENU::AddSwitch("physics.triggers", &g_triggers);
		DEBUG_MENU::AddSwitch("physics.phantoms", &g_phantoms);
		DEBUG_MENU::AddValue("physics.bodies_radius", &g_bodiesRadius, 2.0f, 100.0f);
		DEBUG_MENU::AddSwitch("physics.capsules", &g_capsules);
		DEBUG_MENU::AddSwitch("physics.systems", &g_systems);
		DEBUG_MENU::AddSwitch("physics.legend", &g_legend);
		DEBUG_MENU::AddCommand("physics.census", "count the static collision instances (by compound and layer) against the collision map's rows", CensusCommand);
		DEBUG_MENU::AddCommand("physics.list_static", "the static collision instances near the camera, nearest first (argument: distance in metres, default 10)", ListStaticCommand);
		DEBUG_MENU::AddCommand("physics.list_bodies", "the physics bodies near the camera, with counts of every kind (argument: distance in metres, default 25)", ListBodiesCommand);
		DEBUG_MENU::AddCommand("physics.list_capsules", "the characters' capsules: state, kind, shape (radius, height, bottom and top), where each stands", ListCapsulesCommand);
		DEBUG_MENU::AddCommand("physics.list_systems", "the level's dynamic physics systems from PHYSICS.MAP", ListSystemsCommand);
		DEBUG_MENU::AddCommand("physics.layers", "which collision layers collide with which, a layer a line", LayersCommand);
	}

	// Detaching: the game's switch is put back if it was set from here
	void AttachHooks(bool attach)
	{
		if (!attach && g_showCollisionMeshesSet)
		{
			WriteShowCollisionMeshes(false);
			g_showCollisionMeshesSet = false;
		}
	}

	const bool g_controls = (NameControls(), true);
	const bool g_added = DEBUG_MENU::AddTab("Physics", 130, DrawTab, "World") && DEBUG_MENU::AddOverlay(55, DrawOverlay);
	const bool g_hooks = DEBUG_MENU::AddHooks(AttachHooks);
}

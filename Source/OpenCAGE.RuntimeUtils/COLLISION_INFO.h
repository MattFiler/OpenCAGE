#pragma once

#include "DevTools.h"
#include <cstdint>
#include <string>

/*
	Reading the level's collision, for the debug menu's World > Inspect and World > Physics tabs (INSPECT.cpp,
	PHYSICS_DEBUG.cpp, PHYSICS_LOG.cpp). Every read is guarded and safe from any thread; callers read at a frame's
	presentation, when the game's own frame work is done.

	How the game keeps it:
	- The level's collision map (COLLISION.MAP) is loaded into one array of rows, with 3000 spare rows after the file's for
	  collision made while the level runs (trigger boxes and the like). A row says what a piece of collision is: its flags
	  (its collision layer in the low five bits, how it is stored, how it moves, where it came from, and state such as
	  "collision off"), its index in the static collision it is stored in, the script entity and composite instance it
	  belongs to (the game's own entity handle), its render material (and a material proxy), the zone it streams with, its
	  physics body when it has one of its own, and the model instance drawn in its place once that model is set up.
	- The level's static collision (COLLISION.HKX) is four static compound shapes of the physics engine: the world's, the
	  ballistic-only one, the landscape's and the one for closed paths. Each is a list of instances - a shape (usually a
	  compressed mesh) placed with a translation, rotation and scale, a collision filter (its layer) and the row it belongs
	  to. A row stored in one of them gives its instance's number there; turning a row's collision off sets its layer to
	  "nothing" rather than taking the instance out.
	- Everything else is a physics body in the physics world: its shape, its transform, its motion type and its row (in
	  the body's own user data). Moving and sleeping bodies sit in the world's active and inactive islands, fixed ones in
	  its fixed island.
	- A ray the game casts reports the row it hit, so the row is what links a surface to the level's data.
*/
namespace COLLISION_INFO
{
	// The level's collision map: a pointer to its rows, how many there is room for (the file's rows, then the spares), and
	// how many came from the file
	constexpr uintptr_t kRows = 0x01308838;
	constexpr uintptr_t kRowCount = 0x0130883C;
	constexpr uintptr_t kFileRowCount = 0x01307958;
	constexpr uint32_t kRowSize = 0x30;

	// The physics world (a pointer) and the four static compound shapes of the level's static collision (pointers)
	constexpr uintptr_t kWorld = 0x0130791C;
	constexpr uintptr_t kPathCompound = 0x01307938;
	constexpr uintptr_t kWorldCompound = 0x0130793C;
	constexpr uintptr_t kBallisticCompound = 0x01307940;
	constexpr uintptr_t kLandscapeCompound = 0x01307944;

	// Row flags
	constexpr uint32_t kLayerMask = 0x1F;
	constexpr uint32_t kStorageMask = 0xE0;     // landscape 0x20, world 0x40, ballistic 0x80
	constexpr uint32_t kStorageLandscape = 0x20;
	constexpr uint32_t kStorageWorld = 0x40;
	constexpr uint32_t kStorageBallistic = 0x80;
	constexpr uint32_t kMotionMask = 0xF00;     // fixed 0x100, keyframed 0x200, simulating 0x400
	constexpr uint32_t kSourceMask = 0xF000;    // made with the level 0x1000, from a resource 0x2000, by the game 0x4000, by script 0x8000
	constexpr uint32_t kGhosted = 0x10000000;   // collision turned off
	constexpr int kLayerNothing = 0x12;         // the layer collision is put on while it is off
	constexpr int kLayerMaterialTest = 0x16;    // the layer the game's own surface rays use (zones, footsteps)

	struct Row
	{
		int index = -1;
		const uint8_t* address = nullptr;
		uint32_t flags = 0;
		int32_t collisionIndex = -1;       // its instance in the static compound its flags store it in
		uint32_t filterExtra = 0;          // the file's resource id, cleared once the row is registered as a resource; then
		                                   // the high bits of its collision filter
		uint32_t entityGuid = 0;           // the script entity it belongs to, and the composite instance holding it
		uint32_t instanceGuid = 0;
		uint32_t materialId = 0xFFFFFFFF;  // its render material
		uint16_t proxyIndex = 0xFFFF;      // its material proxy (none: 0xFFFF)
		uint16_t mappingIndex = 0xFFFF;    // the material mapping remapping its material (none: 0xFFFF)
		uint32_t zoneId = 0;
		const uint8_t* body = nullptr;     // its own physics body, when it has one
		const uint8_t* instance = nullptr; // the model instance drawn in its place, once that model is set up
	};

	// How many rows there is room for (0 with no level loaded), and how many came from the file
	int RowCount();
	int FileRowCount();
	bool ReadRow(int index, Row& out);
	// The row a pointer to one is, or -1
	int RowIndexOf(const void* row);

	// Names for showing
	const char* LayerName(int layer);
	const char* StorageName(uint32_t flags);
	const char* MotionName(uint32_t flags);
	const char* SourceName(uint32_t flags);
	std::string StateText(uint32_t flags);
	std::string FlagsText(uint32_t flags);

	// A zone's name (false when there is no zone of that id loaded in the level's zone list)
	bool ZoneName(uint32_t zoneId, char* out, size_t size);

	// The composite instance of an id (as an entity handle holds it): its composite's name and id, and its path - the ids of
	// the composite instance entities from the level's root down to it, as OpenCAGE addresses an instance
	struct CompositeInstance
	{
		bool found = false;
		char name[128] = {};
		uint32_t compositeGuid = 0;
		uint32_t path[32] = {};
		int pathLength = 0;
	};
	bool FindCompositeInstance(uint32_t instanceGuid, CompositeInstance& out);
	std::string PathText(const CompositeInstance& instance);

	// A render material's data from its id (null when there is none), and a material's name
	const uint8_t* MaterialData(uint32_t materialId);
	bool MaterialName(const uint8_t* material, char* out, size_t size);
	// The physical material of a row (its render material's, through its material mapping) and that material's name; -1
	// when none
	int PhysicalMaterial(const Row& row);
	bool PhysicalMaterialName(int index, char* out, size_t size);

	// Text from the game that may be null or not text: copied (cut short), false when there is none
	bool CopyText(const char* text, char* out, size_t size);

	// Physics engine shapes
	const char* ShapeTypeName(int type);
	int ShapeType(const uint8_t* shape);
	// A shape's box in its own space (false for a shape this does not know the extent of)
	bool ShapeLocalBox(const uint8_t* shape, float min[3], float max[3]);
	// The axes of a rotation (a quaternion x, y, z, w): axes[i] is where the shape's own axis i points
	void QuaternionAxes(const float q[4], float axes[3][3]);
	const char* MotionTypeName(int type);

	// One instance of a static compound shape
	struct StaticInstance
	{
		const uint8_t* shape = nullptr;
		float translation[3] = {};
		float rotation[4] = { 0.0f, 0.0f, 0.0f, 1.0f };
		float scale[3] = { 1.0f, 1.0f, 1.0f };
		uint32_t instanceFlags = 0; // the shape's own flags: 0x10 disabled
		uint32_t filter = 0;        // its collision filter (the layer in the low five bits)
		int32_t row = -1;           // the collision map row it belongs to
		const uint8_t* compound = nullptr;
	};
	// A static compound shape (one of the four above, read through its pointer): how many instances, and one of them
	const uint8_t* Compound(uintptr_t which);
	int CompoundInstanceCount(const uint8_t* compound);
	bool ReadCompoundInstance(const uint8_t* compound, int index, StaticInstance& out);
	// The instance a row is stored as (false for rows not stored in a static compound)
	bool RowStaticInstance(const Row& row, StaticInstance& out, const char** compoundName);
	// An oriented box around a static instance, as DEBUG_MENU::WorldOrientedBox takes it (false when its shape's extent is
	// not known)
	bool StaticInstanceBox(const StaticInstance& instance, float matrix[16], float halfSize[3]);
	// Where the body holding a compound puts it (false when it leaves it where it is - the level's own sit at the origin)
	bool CompoundTransform(const uint8_t* compound, float axes[3][3], float position[3]);
}

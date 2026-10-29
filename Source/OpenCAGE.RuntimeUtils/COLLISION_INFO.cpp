#include "COLLISION_INFO.h"
#include "DEBUG_MARKER.h"
#include "ZONE_LOADER.h"

#include <algorithm>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <mutex>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

/*
	Addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds). See
	COLLISION_INFO.h for what is read.
*/
namespace
{
	// The game's list of composite instances by id (a pointer to it): a counted array of { the instance (a counted
	// pointer), its id }
	constexpr uintptr_t kCompositeDatabase = 0x01354678;
	// The loaded COMMANDS.PAK (a pointer), which a composite instance's template is a packed offset into (in 4-byte words)
	constexpr uintptr_t kPackFile = 0x0134EF44;
	// An entity: the instance holding it (a counted pointer; none for the level's root instance) and its id; a composite
	// instance's template (packed); a template's name (set at load) and its composite id
	constexpr uint32_t kEntityOwner = 0x08;
	constexpr uint32_t kEntityGuid = 0x0C;
	constexpr uint32_t kInstanceTemplate = 0x30;
	constexpr uint32_t kTemplateName = 0x00;
	constexpr uint32_t kTemplateGuid = 0x10;

	// In the engine object (DEBUG_MARKER::engine): the material database, whose platform part (+4) turns a material id into
	// its data through a table per kind of id, and whose names (+0x10) are by each material's runtime index
	constexpr uint32_t kEngineMaterials = 0x17A8;
	constexpr uint32_t kMaterialsPlatform = 0x04;
	constexpr uint32_t kMaterialsNames = 0x10;
	constexpr uint32_t kPlatformData = 0x3C;
	constexpr uint32_t kMaterialSize = 0xC8;
	constexpr uint32_t kMaterialRuntimeIndex = 0xB8;

	// The physical materials (a pointer and a count): 0x340 each, the name first
	constexpr uintptr_t kPhysicalMaterials = 0x0134A3E0;
	constexpr uintptr_t kPhysicalMaterialCount = 0x0134A3E4;
	constexpr uint32_t kPhysicalMaterialSize = 0x340;

	// The game's lookup of a row's physical material: its render material's, remapped first through the row's material
	// mapping when it has one (the material id at +0x14, the mapping index at +0x1A - as the game's own callers pass them;
	// -1 when none, 255 when the material names none)
	typedef int(__cdecl* t_physical_material)(const void* materialId, const void* mappingIndex);
	constexpr uintptr_t kPhysicalMaterialOf = 0x003EBEE0;

	// A zone: its name (a pointer to text: the Zone entity's)
	constexpr uint32_t kZoneName = 0x30;

	// Physics engine shapes: the type byte, and the parts read for each type's extent
	constexpr uint32_t kShapeType = 0x08;
	constexpr uint32_t kConvexRadius = 0x10;
	constexpr int kMaxShapeDepth = 4;

	// A static compound shape: its instances (an array: data, count), 0x40 each
	constexpr uint32_t kCompoundInstances = 0x20;
	constexpr uint32_t kCompoundInstanceSize = 0x40;

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

	const uint8_t* GlobalPointer(uintptr_t steamAddress)
	{
		const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(steamAddress);
		return at ? *reinterpret_cast<const uint8_t* const*>(at) : nullptr;
	}

	uint32_t GlobalU32(uintptr_t steamAddress)
	{
		const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(steamAddress);
		return at ? *reinterpret_cast<const uint32_t*>(at) : 0;
	}

	void Expand(float min[3], float max[3], const float point[3])
	{
		for (int i = 0; i < 3; i++)
		{
			min[i] = (std::min)(min[i], point[i]);
			max[i] = (std::max)(max[i], point[i]);
		}
	}

	// A box moved by a rotation (columns, as the physics engine keeps them: axes[i] is where axis i goes) and a translation
	void TransformBox(float min[3], float max[3], const float axes[3][3], const float translation[3])
	{
		float centre[3], half[3], outCentre[3], outHalf[3];
		for (int i = 0; i < 3; i++)
		{
			centre[i] = (min[i] + max[i]) * 0.5f;
			half[i] = (max[i] - min[i]) * 0.5f;
		}
		for (int j = 0; j < 3; j++)
		{
			outCentre[j] = translation[j];
			outHalf[j] = 0.0f;
			for (int i = 0; i < 3; i++)
			{
				outCentre[j] += axes[i][j] * centre[i];
				outHalf[j] += std::fabs(axes[i][j]) * half[i];
			}
		}
		for (int i = 0; i < 3; i++)
		{
			min[i] = outCentre[i] - outHalf[i];
			max[i] = outCentre[i] + outHalf[i];
		}
	}

	bool LocalBox(const uint8_t* shape, float min[3], float max[3], int depth)
	{
		if (!shape || depth > kMaxShapeDepth)
			return false;
		const int type = shape[kShapeType];
		const float radius = Read<float>(shape, kConvexRadius);
		auto points = [&](const uint32_t* offsets, int count, float extra)
		{
			for (int i = 0; i < 3; i++)
			{
				min[i] = 1e30f;
				max[i] = -1e30f;
			}
			for (int p = 0; p < count; p++)
			{
				float point[3];
				memcpy(point, shape + offsets[p], sizeof(point));
				Expand(min, max, point);
			}
			for (int i = 0; i < 3; i++)
			{
				min[i] -= extra;
				max[i] += extra;
			}
			return true;
		};
		auto centred = [&](uint32_t centreOffset, uint32_t halfOffset, float extra)
		{
			float centre[3] = {}, half[3];
			if (centreOffset)
				memcpy(centre, shape + centreOffset, sizeof(centre));
			memcpy(half, shape + halfOffset, sizeof(half));
			for (int i = 0; i < 3; i++)
			{
				min[i] = centre[i] - std::fabs(half[i]) - extra;
				max[i] = centre[i] + std::fabs(half[i]) + extra;
			}
			return true;
		};
		switch (type)
		{
		case 0x00: // sphere
			for (int i = 0; i < 3; i++)
			{
				min[i] = -radius;
				max[i] = radius;
			}
			return true;
		case 0x01: // cylinder: its two ends, and its own radius on top of the convex radius
		{
			const uint32_t ends[] = { 0x20, 0x30 };
			return points(ends, 2, radius + Read<float>(shape, 0x14));
		}
		case 0x02: // triangle
		{
			const uint32_t corners[] = { 0x20, 0x30, 0x40 };
			return points(corners, 3, radius);
		}
		case 0x03: // box: half its size
			return centred(0, 0x20, radius);
		case 0x04: // capsule: its two ends
		{
			const uint32_t ends[] = { 0x20, 0x30 };
			return points(ends, 2, radius);
		}
		case 0x05: // convex hull: a box kept with it
			return centred(0x30, 0x20, radius);
		case 0x08: // list of shapes: a box kept with it
			return centred(0x40, 0x30, 0.0f);
		case 0x09: // tree over another shape: that shape's
			return LocalBox(Pointer(shape, 0x34), min, max, depth + 1);
		case 0x0A: // another shape moved
		{
			if (!LocalBox(Pointer(shape, 0x18), min, max, depth + 1))
				return false;
			float translation[3];
			memcpy(translation, shape + 0x20, sizeof(translation));
			for (int i = 0; i < 3; i++)
			{
				min[i] += translation[i];
				max[i] += translation[i];
			}
			return true;
		}
		case 0x0B: // another shape moved, turned and scaled
		{
			if (!LocalBox(Pointer(shape, 0x18), min, max, depth + 1))
				return false;
			float translation[3], rotation[4], scale[3], axes[3][3];
			memcpy(translation, shape + 0x20, sizeof(translation));
			memcpy(rotation, shape + 0x30, sizeof(rotation));
			memcpy(scale, shape + 0x40, sizeof(scale));
			COLLISION_INFO::QuaternionAxes(rotation, axes);
			for (int i = 0; i < 3; i++)
			{
				const float a = min[i] * scale[i], b = max[i] * scale[i];
				min[i] = (std::min)(a, b);
				max[i] = (std::max)(a, b);
			}
			TransformBox(min, max, axes, translation);
			return true;
		}
		case 0x0E: // another shape transformed (a rotation's columns, then a translation)
		{
			if (!LocalBox(Pointer(shape, 0x14), min, max, depth + 1))
				return false;
			float axes[3][3], translation[3];
			for (int i = 0; i < 3; i++)
				memcpy(axes[i], shape + 0x30 + i * 0x10, sizeof(axes[i]));
			memcpy(translation, shape + 0x60, sizeof(translation));
			TransformBox(min, max, axes, translation);
			return true;
		}
		case 0x10: // static compound: the box its tree covers
			memcpy(min, shape + 0x50, 3 * sizeof(float));
			memcpy(max, shape + 0x60, 3 * sizeof(float));
			return true;
		case 0x11: // compressed mesh: the box its tree covers
			memcpy(min, shape + 0x60, 3 * sizeof(float));
			memcpy(max, shape + 0x70, 3 * sizeof(float));
			return true;
		case 0x1E: // a shape inside a bounding shape (a trigger): the bounding shape's
			return LocalBox(Pointer(shape, 0x10), min, max, depth + 1);
		default:
			return false;
		}
	}

	bool ReadLocalBox(const uint8_t* shape, float min[3], float max[3])
	{
		__try
		{
			if (!LocalBox(shape, min, max, 0))
				return false;
			for (int i = 0; i < 3; i++)
				if (!std::isfinite(min[i]) || !std::isfinite(max[i]) || max[i] < min[i] || max[i] - min[i] > 1e5f)
					return false;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	int CallPhysicalMaterial(const uint8_t* row)
	{
		__try
		{
			const auto physicalMaterialOf = reinterpret_cast<t_physical_material>(DEVTOOLS_RELATIVE_ADDRESS(kPhysicalMaterialOf));
			if (!physicalMaterialOf || !row)
				return -1;
			const int material = physicalMaterialOf(row + 0x14, row + 0x1A);
			return material == 0xFF ? -1 : material;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return -1;
		}
	}

	const char* const kLayerNames[32] = {
		"everything", "triggers", "camera", "standard", "transparent", "characters", "against moving physics",
		"moving transparent", "debris", "ballistics", "line of sight", "ballistic test", "path open", "path closed",
		"moving physics", "ragdolls", "water", "sound", "nothing", "ghosted", "interface", "interface test",
		"surface test", "support test", "sound barrier", "player", "thrown", "player only", "high priority interface",
		"high priority interface test", "character triggers", "keyframed physics",
	};
}

int COLLISION_INFO::RowCount()
{
	__try
	{
		if (!GlobalPointer(kRows))
			return 0;
		const uint32_t count = GlobalU32(kRowCount);
		return count > 0x100000 ? 0 : static_cast<int>(count);
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return 0;
	}
}

int COLLISION_INFO::FileRowCount()
{
	__try
	{
		const uint32_t count = GlobalU32(kFileRowCount);
		return count > 0x100000 ? 0 : static_cast<int>(count);
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return 0;
	}
}

bool COLLISION_INFO::ReadRow(int index, Row& out)
{
	out = Row();
	__try
	{
		const uint8_t* rows = GlobalPointer(kRows);
		const uint32_t count = GlobalU32(kRowCount);
		if (!rows || index < 0 || static_cast<uint32_t>(index) >= count)
			return false;
		const uint8_t* row = rows + static_cast<uint32_t>(index) * kRowSize;
		out.index = index;
		out.address = row;
		out.flags = Read<uint32_t>(row, 0x00);
		out.collisionIndex = Read<int32_t>(row, 0x04);
		out.filterExtra = Read<uint32_t>(row, 0x08);
		out.entityGuid = Read<uint32_t>(row, 0x0C);
		out.instanceGuid = Read<uint32_t>(row, 0x10);
		out.materialId = Read<uint32_t>(row, 0x14);
		out.proxyIndex = Read<uint16_t>(row, 0x18);
		out.mappingIndex = Read<uint16_t>(row, 0x1A);
		out.zoneId = Read<uint32_t>(row, 0x1C);
		out.body = Pointer(row, 0x20);     // (stored as 64-bit pointers: the low half is the pointer)
		out.instance = Pointer(row, 0x28);
		return true;
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return false;
	}
}

int COLLISION_INFO::RowIndexOf(const void* row)
{
	__try
	{
		const uint8_t* rows = GlobalPointer(kRows);
		const uint32_t count = GlobalU32(kRowCount);
		const uint8_t* at = static_cast<const uint8_t*>(row);
		if (!rows || at < rows || at >= rows + count * kRowSize || (at - rows) % kRowSize)
			return -1;
		return static_cast<int>((at - rows) / kRowSize);
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return -1;
	}
}

const char* COLLISION_INFO::LayerName(int layer)
{
	return layer >= 0 && layer < 32 ? kLayerNames[layer] : "?";
}

const char* COLLISION_INFO::StorageName(uint32_t flags)
{
	switch (flags & kStorageMask)
	{
	case 0: return "its own body";
	case kStorageLandscape: return "landscape";
	case kStorageWorld: return "static world";
	case kStorageBallistic: return "ballistic only";
	default: return "?";
	}
}

const char* COLLISION_INFO::MotionName(uint32_t flags)
{
	switch (flags & kMotionMask)
	{
	case 0: return "-";
	case 0x100: return "fixed";
	case 0x200: return "keyframed";
	case 0x400: return "simulating";
	default: return "mixed";
	}
}

const char* COLLISION_INFO::SourceName(uint32_t flags)
{
	switch (flags & kSourceMask)
	{
	case 0: return "-";
	case 0x1000: return "built with the level";
	case 0x2000: return "a resource";
	case 0x4000: return "made by the game";
	case 0x8000: return "made by script";
	default: return "mixed";
	}
}

std::string COLLISION_INFO::StateText(uint32_t flags)
{
	struct Bit { uint32_t bit; const char* name; };
	static const Bit kBits[] = {
		{ 0x20000, "reporting" }, { 0x40000, "forced transparent" }, { 0x80000, "high priority" },
		{ 0x200000, "soft collision" }, { 0x400000, "reports sliding" },
		{ 0x800000, "submerged" }, { 0x1000000, "removed" }, { 0x2000000, "forced keyframed" }, { 0x4000000, "ballistic only" },
		{ 0x8000000, "standard only" },
	};
	// Three states have a second bit for what they were before: one word for the two together ("frozen (now and before)"), or
	// "was frozen" for the earlier one alone
	static const struct { uint32_t now, before; const char* name; } kPairs[] = {
		{ 0x10000, 0x100000, "no gravity" }, { 0x10000000, 0x20000000, "collision off" }, { 0x40000000, 0x80000000u, "frozen" },
	};
	std::string text;
	for (const Bit& bit : kBits)
		if (flags & bit.bit)
			text += (text.empty() ? "" : ", ") + std::string(bit.name);
	for (const auto& pair : kPairs)
	{
		const bool now = (flags & pair.now) != 0, before = (flags & pair.before) != 0;
		if (now || before)
			text += std::string(text.empty() ? "" : ", ") + (now ? "" : "was ") + pair.name + (now && before ? " (now and before)" : "");
	}
	return text.empty() ? "-" : text;
}

std::string COLLISION_INFO::FlagsText(uint32_t flags)
{
	char text[256];
	snprintf(text, sizeof(text), "layer %d (%s); stored: %s; motion: %s; from: %s; state: %s", static_cast<int>(flags & kLayerMask), LayerName(flags & kLayerMask),
		StorageName(flags), MotionName(flags), SourceName(flags), StateText(flags).c_str());
	return text;
}

bool COLLISION_INFO::CopyText(const char* text, char* out, size_t size)
{
	if (!out || size == 0)
		return false;
	out[0] = '\0';
	__try
	{
		if (!text)
			return false;
		size_t i = 0;
		for (; i + 1 < size && text[i]; i++)
		{
			const unsigned char c = static_cast<unsigned char>(text[i]);
			if (c < 0x20 || c > 0x7E)
			{
				out[0] = '\0';
				return false;
			}
			out[i] = static_cast<char>(c);
		}
		out[i] = '\0';
		return i > 0;
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		out[0] = '\0';
		return false;
	}
}

bool COLLISION_INFO::ZoneName(uint32_t zoneId, char* out, size_t size)
{
	if (size)
		out[0] = '\0';
	const char* name = nullptr;
	__try
	{
		const uint8_t* zoneManager = static_cast<const uint8_t*>(*ZONE_LOADER::zone_manager_instance);
		const uint8_t* zones = zoneManager ? Pointer(zoneManager, ZONE_LOADER::kZoneArrayOffset) : nullptr;
		if (!zones)
			return false;
		const int count = Read<int>(zones, ZONE_LOADER::kArrayCountOffset);
		const uint8_t* const* allocations = reinterpret_cast<const uint8_t* const*>(Pointer(zones, ZONE_LOADER::kArrayDataOffset));
		if (!allocations || count <= 0 || count > 4096)
			return false;
		for (int i = 0; i < count && !name; i++)
		{
			const uint8_t* zone = allocations[i] ? Pointer(allocations[i], ZONE_LOADER::kAllocationObjectOffset) : nullptr;
			if (zone && Read<uint32_t>(zone, ZONE_LOADER::kZoneIdOffset) == zoneId)
			{
				name = reinterpret_cast<const char*>(Pointer(zone, kZoneName));
				if (!name)
					name = "";
			}
		}
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return false;
	}
	if (!name)
		return false;
	if (!CopyText(name, out, size))
		snprintf(out, size, "(no name)");
	return true;
}

namespace
{
	struct InstanceRead
	{
		bool found;
		const char* name;
		uint32_t compositeGuid;
		uint32_t reversed[32];
		int length;
	};

	// The instance of an id, by a walk of the game's list of composite instances, and the walk up its owners
	bool ReadInstance(uint32_t instanceGuid, InstanceRead& out)
	{
		out.found = false;
		out.name = nullptr;
		out.compositeGuid = 0;
		out.length = 0;
		__try
		{
			const uint8_t* database = GlobalPointer(kCompositeDatabase);
			const uint8_t* map = database ? Pointer(database, 0x00) : nullptr;
			if (!map)
				return false;
			const uint32_t count = Read<uint32_t>(map, 0x08);
			const uint8_t* keys = Pointer(map, 0x0C);
			if (!keys || count > 0x100000)
				return false;
			const uint8_t* instance = nullptr;
			for (uint32_t i = 0; i < count && !instance; i++)
				if (Read<uint32_t>(keys, i * 8 + 4) == instanceGuid)
				{
					const uint8_t* allocation = Pointer(keys, i * 8);
					instance = allocation ? Pointer(allocation, 0x0C) : nullptr;
				}
			if (!instance)
				return false;
			out.found = true;
			const uint32_t packed = Read<uint32_t>(instance, kInstanceTemplate);
			const uintptr_t pack = DEVTOOLS_RELATIVE_ADDRESS(kPackFile);
			if (packed != 0xFFFFFFFF && pack && *reinterpret_cast<const uintptr_t*>(pack))
			{
				const uint8_t* compositeTemplate = reinterpret_cast<const uint8_t*>(*reinterpret_cast<const uintptr_t*>(pack) + packed * 4u);
				out.name = reinterpret_cast<const char*>(Pointer(compositeTemplate, kTemplateName));
				out.compositeGuid = Read<uint32_t>(compositeTemplate, kTemplateGuid);
			}
			// The path: this instance's id, its owner's, ... up to (not including) the level's root instance
			for (const uint8_t* step = instance; step && out.length < 32; )
			{
				const uint8_t* up = Pointer(step, kEntityOwner);
				if (!up)
					break;
				out.reversed[out.length++] = Read<uint32_t>(step, kEntityGuid);
				step = Pointer(up, 0x0C);
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return out.found;
		}
	}
}

bool COLLISION_INFO::FindCompositeInstance(uint32_t instanceGuid, CompositeInstance& out)
{
	out = CompositeInstance();
	InstanceRead read;
	if (!ReadInstance(instanceGuid, read))
		return false;
	out.found = true;
	out.compositeGuid = read.compositeGuid;
	if (!CopyText(read.name, out.name, sizeof(out.name)))
		snprintf(out.name, sizeof(out.name), "(composite %08X)", read.compositeGuid);
	out.pathLength = read.length;
	for (int i = 0; i < read.length; i++)
		out.path[i] = read.reversed[read.length - 1 - i];
	return true;
}

std::string COLLISION_INFO::PathText(const CompositeInstance& instance)
{
	if (!instance.found)
		return "";
	if (instance.pathLength == 0)
		return "(the level's root)";
	std::string text;
	for (int i = 0; i < instance.pathLength; i++)
	{
		char part[16];
		snprintf(part, sizeof(part), "%s%08X", i ? "/" : "", instance.path[i]);
		text += part;
	}
	return text;
}

const uint8_t* COLLISION_INFO::MaterialData(uint32_t materialId)
{
	__try
	{
		if (materialId == 0xFFFFFFFF || !DEBUG_MARKER::engine)
			return nullptr;
		const uint8_t* platform = reinterpret_cast<const uint8_t*>(DEBUG_MARKER::engine + kEngineMaterials + kMaterialsPlatform);
		const uint32_t table = (materialId >> 17) * 3 + 9;
		const int16_t* indices = reinterpret_cast<const int16_t*>(Pointer(platform, table * 4));
		const uint8_t* data = Pointer(platform, kPlatformData);
		if (!indices || !data)
			return nullptr;
		const int16_t index = indices[materialId & 0xFFFF];
		return index < 0 ? nullptr : data + static_cast<uint32_t>(index) * kMaterialSize;
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return nullptr;
	}
}

bool COLLISION_INFO::MaterialName(const uint8_t* material, char* out, size_t size)
{
	if (size)
		out[0] = '\0';
	const char* name = nullptr;
	__try
	{
		if (!material || !DEBUG_MARKER::engine)
			return false;
		const uint16_t index = Read<uint16_t>(material, kMaterialRuntimeIndex);
		const char* const* names = reinterpret_cast<const char* const*>(Pointer(reinterpret_cast<const uint8_t*>(DEBUG_MARKER::engine + kEngineMaterials), kMaterialsNames));
		if (index == 0xFFFF || !names)
			return false;
		name = names[index];
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return false;
	}
	return CopyText(name, out, size);
}

int COLLISION_INFO::PhysicalMaterial(const Row& row)
{
	return row.address ? CallPhysicalMaterial(row.address) : -1;
}

bool COLLISION_INFO::PhysicalMaterialName(int index, char* out, size_t size)
{
	if (size)
		out[0] = '\0';
	const char* name = nullptr;
	__try
	{
		const uint8_t* materials = GlobalPointer(kPhysicalMaterials);
		const uint32_t count = GlobalU32(kPhysicalMaterialCount);
		if (!materials || index < 0 || static_cast<uint32_t>(index) >= count)
			return false;
		name = reinterpret_cast<const char*>(materials + static_cast<uint32_t>(index) * kPhysicalMaterialSize);
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return false;
	}
	char copy[0x33];
	if (!CopyText(name, copy, sizeof(copy)))
		return false;
	snprintf(out, size, "%s", copy);
	return true;
}

const char* COLLISION_INFO::ShapeTypeName(int type)
{
	static const char* const kNames[] = {
		"sphere", "cylinder", "triangle", "box", "capsule", "convex hull", "height field (sampled)", "height field (tree)",
		"list", "tree over a shape", "moved convex", "transformed convex", "height field", "mesh", "transformed", "compressed mesh (old)",
		"static compound", "compressed mesh", "collection", "custom 0", "custom 1", "custom 2", "tree", "convex", "convex piece",
		"spheres", "convex list", "triangles", "height field", "sphere set", "bounding shape (trigger)", "plane", "trigger callback",
		"rays",
	};
	return type >= 0 && type < static_cast<int>(sizeof(kNames) / sizeof(kNames[0])) ? kNames[type] : "?";
}

int COLLISION_INFO::ShapeType(const uint8_t* shape)
{
	__try
	{
		return shape ? shape[kShapeType] : -1;
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return -1;
	}
}

bool COLLISION_INFO::ShapeLocalBox(const uint8_t* shape, float min[3], float max[3])
{
	return ReadLocalBox(shape, min, max);
}

void COLLISION_INFO::QuaternionAxes(const float q[4], float axes[3][3])
{
	const float x = q[0], y = q[1], z = q[2], w = q[3];
	axes[0][0] = 1.0f - 2.0f * (y * y + z * z);
	axes[0][1] = 2.0f * (x * y + w * z);
	axes[0][2] = 2.0f * (x * z - w * y);
	axes[1][0] = 2.0f * (x * y - w * z);
	axes[1][1] = 1.0f - 2.0f * (x * x + z * z);
	axes[1][2] = 2.0f * (y * z + w * x);
	axes[2][0] = 2.0f * (x * z + w * y);
	axes[2][1] = 2.0f * (y * z - w * x);
	axes[2][2] = 1.0f - 2.0f * (x * x + y * y);
}

const char* COLLISION_INFO::MotionTypeName(int type)
{
	switch (type)
	{
	case 1: return "dynamic";
	case 2: return "dynamic (sphere inertia)";
	case 3: return "dynamic (box inertia)";
	case 4: return "keyframed";
	case 5: return "fixed";
	case 6: return "dynamic (thin box inertia)";
	case 7: return "character";
	default: return "?";
	}
}

const uint8_t* COLLISION_INFO::Compound(uintptr_t which)
{
	__try
	{
		return GlobalPointer(which);
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return nullptr;
	}
}

int COLLISION_INFO::CompoundInstanceCount(const uint8_t* compound)
{
	__try
	{
		if (!compound || compound[kShapeType] != 0x10 || !Pointer(compound, kCompoundInstances))
			return 0;
		const int count = Read<int>(compound, kCompoundInstances + 4);
		return count < 0 || count > 0x200000 ? 0 : count;
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return 0;
	}
}

bool COLLISION_INFO::ReadCompoundInstance(const uint8_t* compound, int index, StaticInstance& out)
{
	out = StaticInstance();
	__try
	{
		const uint8_t* instances = compound ? Pointer(compound, kCompoundInstances) : nullptr;
		if (!instances || index < 0 || index >= Read<int>(compound, kCompoundInstances + 4))
			return false;
		const uint8_t* instance = instances + static_cast<uint32_t>(index) * kCompoundInstanceSize;
		memcpy(out.translation, instance + 0x00, sizeof(out.translation));
		out.instanceFlags = Read<uint32_t>(instance, 0x0C) & 0x7F; // kept in the low bits of the translation's fourth number
		memcpy(out.rotation, instance + 0x10, sizeof(out.rotation));
		memcpy(out.scale, instance + 0x20, sizeof(out.scale));
		out.shape = Pointer(instance, 0x30);
		out.filter = Read<uint32_t>(instance, 0x34);
		out.row = Read<int32_t>(instance, 0x3C);
		out.compound = compound;
		return true;
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return false;
	}
}

bool COLLISION_INFO::RowStaticInstance(const Row& row, StaticInstance& out, const char** compoundName)
{
	uintptr_t which = 0;
	const char* name = "";
	switch (row.flags & kStorageMask)
	{
	case kStorageWorld: which = kWorldCompound; name = "static world"; break;
	case kStorageBallistic: which = kBallisticCompound; name = "ballistic only"; break;
	case kStorageLandscape: which = kLandscapeCompound; name = "landscape"; break;
	default: return false;
	}
	if (compoundName)
		*compoundName = name;
	return row.collisionIndex >= 0 && ReadCompoundInstance(Compound(which), row.collisionIndex, out);
}

bool COLLISION_INFO::StaticInstanceBox(const StaticInstance& instance, float matrix[16], float halfSize[3])
{
	float min[3], max[3];
	if (!ShapeLocalBox(instance.shape, min, max))
		return false;
	float axes[3][3];
	QuaternionAxes(instance.rotation, axes);
	float centre[3];
	for (int i = 0; i < 3; i++)
	{
		const float scale = std::isfinite(instance.scale[i]) && instance.scale[i] != 0.0f ? instance.scale[i] : 1.0f;
		centre[i] = (min[i] + max[i]) * 0.5f * scale;
		halfSize[i] = (max[i] - min[i]) * 0.5f * std::fabs(scale);
	}
	float position[3];
	for (int j = 0; j < 3; j++)
	{
		position[j] = instance.translation[j];
		for (int i = 0; i < 3; i++)
			position[j] += axes[i][j] * centre[i];
	}
	// Placed by the body that holds the compound (the level's own bodies sit at the origin, unturned, but this does not
	// count on it)
	float bodyAxes[3][3], bodyPosition[3];
	if (CompoundTransform(instance.compound, bodyAxes, bodyPosition))
	{
		float turned[3][3], moved[3];
		for (int j = 0; j < 3; j++)
		{
			moved[j] = bodyPosition[j];
			for (int k = 0; k < 3; k++)
				moved[j] += bodyAxes[k][j] * position[k];
			for (int i = 0; i < 3; i++)
			{
				turned[i][j] = 0.0f;
				for (int k = 0; k < 3; k++)
					turned[i][j] += bodyAxes[k][j] * axes[i][k];
			}
		}
		memcpy(axes, turned, sizeof(axes));
		memcpy(position, moved, sizeof(position));
	}
	memset(matrix, 0, 16 * sizeof(float));
	for (int i = 0; i < 3; i++)
		for (int j = 0; j < 3; j++)
			matrix[i * 4 + j] = axes[i][j];
	for (int j = 0; j < 3; j++)
		matrix[12 + j] = position[j];
	matrix[15] = 1.0f;
	return std::isfinite(matrix[12]) && std::isfinite(matrix[13]) && std::isfinite(matrix[14]);
}

namespace
{
	// The body holding a compound: looked for among the bodies of the world's fixed island, and its transform read
	bool FindCompoundBody(const uint8_t* compound, float axes[3][3], float position[3])
	{
		__try
		{
			const uint8_t* world = GlobalPointer(COLLISION_INFO::kWorld);
			const uint8_t* island = world ? Pointer(world, 0x20) : nullptr;
			const uint8_t* const* bodies = island ? reinterpret_cast<const uint8_t* const*>(Pointer(island, 0x4C)) : nullptr;
			const int count = island ? Read<int>(island, 0x50) : 0;
			for (int i = 0; bodies && i < count && i < 100000; i++)
			{
				const uint8_t* body = bodies[i];
				if (!body || Pointer(body, 0x10) != compound)
					continue;
				for (int a = 0; a < 3; a++)
					memcpy(axes[a], body + 0xF0 + a * 0x10, sizeof(axes[a]));
				memcpy(position, body + 0x120, 3 * sizeof(float));
				return true;
			}
			return false;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	struct CompoundPlacement
	{
		const uint8_t* compound;
		const uint8_t* world;
		bool found;
		bool identity;
		float axes[3][3];
		float position[3];
	};
	CompoundPlacement g_placements[8] = {};
	std::mutex g_placementsMutex;
}

bool COLLISION_INFO::CompoundTransform(const uint8_t* compound, float axes[3][3], float position[3])
{
	if (!compound)
		return false;
	const uint8_t* world = Compound(kWorld);
	std::lock_guard<std::mutex> lock(g_placementsMutex);
	CompoundPlacement* slot = nullptr;
	for (CompoundPlacement& placement : g_placements)
	{
		if (placement.compound == compound && placement.world == world)
		{
			if (!placement.found || placement.identity)
				return false;
			memcpy(axes, placement.axes, sizeof(placement.axes));
			memcpy(position, placement.position, sizeof(placement.position));
			return true;
		}
		if (!slot && (!placement.compound || placement.world != world))
			slot = &placement;
	}
	CompoundPlacement placement = {};
	placement.compound = compound;
	placement.world = world;
	placement.found = FindCompoundBody(compound, placement.axes, placement.position);
	placement.identity = true;
	for (int i = 0; i < 3; i++)
	{
		placement.identity = placement.identity && std::fabs(placement.position[i]) < 1e-4f;
		for (int j = 0; j < 3; j++)
			placement.identity = placement.identity && std::fabs(placement.axes[i][j] - (i == j ? 1.0f : 0.0f)) < 1e-4f;
	}
	if (!slot)
		slot = &g_placements[0];
	*slot = placement;
	if (!placement.found || placement.identity)
		return false;
	memcpy(axes, placement.axes, sizeof(placement.axes));
	memcpy(position, placement.position, sizeof(placement.position));
	return true;
}

#include "SCRIPT_DEBUG.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <algorithm>
#include <cctype>
#include <cstdio>
#include <cstring>

#include <imgui.h>

/*
	Addresses are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).
*/
namespace
{
	constexpr uintptr_t kEntityManager = 0x0134EF40; // a pointer to the entity manager
	constexpr uintptr_t kPackFile = 0x0134EF44;      // the loaded COMMANDS.PAK, which packed offsets count from (in 4-byte words)

	// The entity manager's fields
	constexpr uint32_t kManagerRootGuid = 0x0C;      // the running level's root composite id
	constexpr uint32_t kManagerRoot = 0x44;          // the root instance (a counted record)
	constexpr uint32_t kManagerClock = 0x90;         // the script clock, seconds (a double)
	constexpr uint32_t kManagerInitialised = 0x105;
	constexpr uint32_t kManagerShuttingDown = 0x106;
	constexpr uint32_t kManagerRunningHeld = 0x10A;  // set while the game runs again the call it held at a breakpoint

	// The game's counted record: the object it holds at +0x0C
	constexpr uint32_t kRecordObject = 0x0C;

	// An entity's fields
	constexpr uint32_t kEntityState = 0x04;
	constexpr uint32_t kEntityOwner = 0x08;          // the instance holding it (a counted record; none for the root instance)
	constexpr uint32_t kEntityGuid = 0x0C;
	constexpr uint32_t kEntityType = 0x10;
	// An instance's: the composite it was made from (a packed pointer), whose id is at +0x10 and name (text) at +0x00
	constexpr uint32_t kInstanceTemplate = 0x30;
	constexpr uint32_t kTemplateName = 0x00;
	constexpr uint32_t kTemplateGuid = 0x10;

	constexpr uintptr_t kInstanceVTable = 0x00FE1D2C; // the vtable a composite instance starts with

	constexpr int kMaxDepth = 64;                    // instances above an entity, at most (the deepest level is far less)
	constexpr uint8_t kCallOpcode = 0xE8;

	// A composite's name (packed: its record in the loaded COMMANDS.PAK), copied out; its id returned
	uint32_t ReadComposite(uintptr_t packFile, uint32_t packed, char* name, size_t size)
	{
		if (!packFile || packed == 0xFFFFFFFF)
			return 0;
		const uint8_t* composite = reinterpret_cast<const uint8_t*>(packFile + packed * 4u);
		const char* text = *reinterpret_cast<const char* const*>(composite + kTemplateName);
		size_t i = 0;
		for (; text && i + 1 < size && text[i]; i++)
			name[i] = text[i];
		name[i] = '\0';
		return *reinterpret_cast<const uint32_t*>(composite + kTemplateGuid);
	}

	bool ReadEntityGuarded(const uint8_t* record, SCRIPT_DEBUG::EntityId& out)
	{
		__try
		{
			const uint8_t* entity = record ? *reinterpret_cast<const uint8_t* const*>(record + kRecordObject) : nullptr;
			if (!entity)
				return false;
			out.state = *reinterpret_cast<const uint32_t*>(entity + kEntityState);
			out.entity = *reinterpret_cast<const uint32_t*>(entity + kEntityGuid);
			out.type = *reinterpret_cast<const uint32_t*>(entity + kEntityType);
			const uintptr_t packFile = *reinterpret_cast<const uintptr_t*>(DEVTOOLS_RELATIVE_ADDRESS(kPackFile));

			// A composite instance itself: the composite it is an instance of
			const uintptr_t instanceVTable = DEVTOOLS_RELATIVE_ADDRESS(kInstanceVTable);
			if (instanceVTable && *reinterpret_cast<const uintptr_t*>(entity) == instanceVTable)
				ReadComposite(packFile, *reinterpret_cast<const uint32_t*>(entity + kInstanceTemplate), out.instanceOf, sizeof(out.instanceOf));

			// The instance it sits in: that instance's own id, combined with each instance's above it
			const uint8_t* ownerRecord = *reinterpret_cast<const uint8_t* const*>(entity + kEntityOwner);
			const uint8_t* owner = ownerRecord ? *reinterpret_cast<const uint8_t* const*>(ownerRecord + kRecordObject) : nullptr;
			if (owner)
			{
				uint32_t id = *reinterpret_cast<const uint32_t*>(owner + kEntityGuid);
				const uint8_t* above = owner;
				for (int depth = 0; depth < kMaxDepth; depth++)
				{
					const uint8_t* aboveRecord = *reinterpret_cast<const uint8_t* const*>(above + kEntityOwner);
					above = aboveRecord ? *reinterpret_cast<const uint8_t* const*>(aboveRecord + kRecordObject) : nullptr;
					if (!above)
						break;
					id = SCRIPT_DEBUG::Combine(id, *reinterpret_cast<const uint32_t*>(above + kEntityGuid));
				}
				out.instance = id;

				// The composite it sits in, and its name
				out.composite = ReadComposite(packFile, *reinterpret_cast<const uint32_t*>(owner + kInstanceTemplate), out.compositeName, sizeof(out.compositeName));
			}
			out.valid = true;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	const uint8_t* RecordAt(const void* pointer)
	{
		__try
		{
			return pointer ? *static_cast<const uint8_t* const*>(pointer) : nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	bool ReadCall(const uint8_t* at, uint8_t bytes[5])
	{
		__try
		{
			memcpy(bytes, at, 5);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	int32_t DistanceTo(const uint8_t* call, uintptr_t target)
	{
		return static_cast<int32_t>(target - (reinterpret_cast<uintptr_t>(call) + 5));
	}

	// Writes a call's four distance bytes: as one locked exchange where they sit inside one 64-byte cache line (which other
	// processors see whole), plainly across a line
	bool WriteDistance(uint8_t* call, int32_t distance)
	{
		uint8_t* where = call + 1;
		DWORD oldProtect;
		if (!VirtualProtect(where, sizeof(distance), PAGE_EXECUTE_READWRITE, &oldProtect))
			return false;
		const uintptr_t address = reinterpret_cast<uintptr_t>(where);
		if ((address & 63) <= 64 - sizeof(distance))
			InterlockedExchange(reinterpret_cast<volatile LONG*>(where), distance);
		else
			memcpy(where, &distance, sizeof(distance));
		VirtualProtect(where, sizeof(distance), oldProtect, &oldProtect);
		FlushInstructionCache(GetCurrentProcess(), call, 5);
		return true;
	}

	// The level's root composite as words, from its name: the level, the last part of the path it was built from
	std::string LevelRootText(const char* name)
	{
		if (!name || !name[0])
			return "the level root";
		const char* last = name;
		for (const char* c = name; *c; c++)
			if ((*c == '\\' || *c == '/') && c[1])
				last = c + 1;
		return std::string("the level root (") + last + ")";
	}

	// The level's root instance: the one entity held by no instance
	bool IsRootInstance(const SCRIPT_DEBUG::EntityId& id)
	{
		return id.composite == 0 && id.instance == 0;
	}
}

bool SCRIPT_DEBUG::ReadEntity(const void* record, EntityId& out)
{
	out = EntityId();
	return ReadEntityGuarded(static_cast<const uint8_t*>(record), out);
}

bool SCRIPT_DEBUG::ReadEntityAt(const void* recordPointer, EntityId& out)
{
	return ReadEntity(RecordAt(recordPointer), out);
}

const uint8_t* SCRIPT_DEBUG::EntityObject(const void* record)
{
	return record ? RecordAt(static_cast<const uint8_t*>(record) + kRecordObject) : nullptr;
}

uint32_t SCRIPT_DEBUG::Combine(uint32_t a, uint32_t b)
{
	// One id taken as given when the other is none; otherwise both, as the high and low halves of 64 bits, mixed down to 32
	if (b == 0)
		return a;
	if (a == 0)
		return b;
	uint64_t key = (static_cast<uint64_t>(a) << 32) | b;
	key = ~key + (key << 18);
	key ^= key >> 31;
	key *= 21;
	key ^= key >> 11;
	key += key << 6;
	key ^= key >> 22;
	return static_cast<uint32_t>(key);
}

std::string SCRIPT_DEBUG::Guid(uint32_t value)
{
	char buffer[16];
	snprintf(buffer, sizeof(buffer), "%02X-%02X-%02X-%02X", value & 0xFF, (value >> 8) & 0xFF, (value >> 16) & 0xFF, value >> 24);
	return buffer;
}

std::string SCRIPT_DEBUG::Hex(uint32_t value)
{
	char buffer[16];
	snprintf(buffer, sizeof(buffer), "%08X", value);
	return buffer;
}

std::string SCRIPT_DEBUG::TypeText(const EntityId& id)
{
	if (const char* name = TypeName(id.type))
		return name;
	if (id.instanceOf[0])
		return IsRootInstance(id) ? LevelRootText(id.instanceOf) : CompositeText(0, id.instanceOf);
	return Guid(id.type);
}

std::string SCRIPT_DEBUG::MethodText(uint32_t method)
{
	const char* name = MethodName(method);
	return name ? std::string(name) : Guid(method);
}

std::string SCRIPT_DEBUG::CompositeText(uint32_t composite, const char* name)
{
	// A level's root composite is named by the path the level was built from (a drive first: "P:\...")
	const bool named = name && name[0];
	const bool builtPath = named && std::isalpha(static_cast<unsigned char>(name[0])) && name[1] == ':';
	if (builtPath || (composite != 0 && composite == RunningRoot()))
		return LevelRootText(name);
	return named ? std::string(name) : Guid(composite);
}

std::string SCRIPT_DEBUG::WhereText(const EntityId& id)
{
	return IsRootInstance(id) ? std::string() : CompositeText(id.composite, id.compositeName);
}

std::string SCRIPT_DEBUG::Describe(const EntityId& id)
{
	if (!id.valid)
		return "(entity not readable)";
	if (IsRootInstance(id))
		return TypeText(id) + " " + Guid(id.entity) + (id.instanceOf[0] ? "" : " (the level's root instance)");
	return ShortText(id) + " (instance " + Guid(id.instance) + ")";
}

std::string SCRIPT_DEBUG::ShortText(const EntityId& id)
{
	if (!id.valid)
		return "(entity not readable)";
	const std::string where = WhereText(id);
	return TypeText(id) + " " + Guid(id.entity) + (where.empty() ? "" : " in " + where);
}

std::string SCRIPT_DEBUG::Fields(const EntityId& id)
{
	if (!id.valid)
		return "entity=none";
	const char* typeName = TypeName(id.type);
	return "entity=" + Hex(id.entity) + " instance=" + Hex(id.instance) + " type=" + Hex(id.type) + " type_name=" +
		(typeName ? std::string(typeName) : id.instanceOf[0] ? std::string(id.instanceOf) : std::string("?")) +
		" composite=" + (id.compositeName[0] ? std::string(id.compositeName) : id.composite ? Hex(id.composite) : std::string("(root)"));
}

uint8_t* SCRIPT_DEBUG::EntityManager()
{
	__try
	{
		const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kEntityManager);
		return at ? *reinterpret_cast<uint8_t**>(at) : nullptr;
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return nullptr;
	}
}

bool SCRIPT_DEBUG::LevelRunning()
{
	__try
	{
		const uint8_t* manager = EntityManager();
		const uintptr_t packAt = DEVTOOLS_RELATIVE_ADDRESS(kPackFile);
		return manager && manager[kManagerInitialised] && !manager[kManagerShuttingDown] && *reinterpret_cast<void* const*>(manager + kManagerRoot) &&
			packAt && *reinterpret_cast<const uintptr_t*>(packAt);
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return false;
	}
}

bool SCRIPT_DEBUG::RunningHeldCall()
{
	__try
	{
		const uint8_t* manager = EntityManager();
		return manager && manager[kManagerRunningHeld] != 0;
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return false;
	}
}

double SCRIPT_DEBUG::ScriptTime()
{
	__try
	{
		const uint8_t* manager = EntityManager();
		return manager ? *reinterpret_cast<const double*>(manager + kManagerClock) : 0.0;
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return 0.0;
	}
}

uint32_t SCRIPT_DEBUG::RunningRoot()
{
	__try
	{
		if (!LevelRunning())
			return 0;
		return *reinterpret_cast<const uint32_t*>(EntityManager() + kManagerRootGuid);
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return 0;
	}
}

bool SCRIPT_DEBUG::Matches(const std::string& text, const char* part)
{
	if (!part || !part[0])
		return true;
	std::string a = text, b = part;
	for (char& c : a)
		c = static_cast<char>(tolower(static_cast<unsigned char>(c)));
	for (char& c : b)
		c = static_cast<char>(tolower(static_cast<unsigned char>(c)));
	return a.find(b) != std::string::npos;
}

bool SCRIPT_DEBUG::BeginTable(const char* id, int columns, float height)
{
	// Not resizable, so each column is fitted to its cells every frame (a longer cell that comes later is not cut short),
	// and none stretched to the window's width (a stretched column cuts what does not fit in it). The room each side of a
	// cell's text grows with the text (the style's was made for smaller text); it is read as the table begins.
	const ImVec2 padding = ImGui::GetStyle().CellPadding;
	ImGui::PushStyleVar(ImGuiStyleVar_CellPadding, ImVec2((std::max)(padding.x, ImGui::GetFontSize() * 0.45f), padding.y));
	const bool open = ImGui::BeginTable(id, columns, ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY |
		ImGuiTableFlags_SizingFixedFit, ImVec2(0.0f, height));
	ImGui::PopStyleVar();
	return open;
}

namespace
{
	// The entity manager's composite templates: a packed array (offset, count) of packed pointers to them
	constexpr uint32_t kManagerTemplates = 0x18;
	// A template's entities (a packed array of { id, type }) and its running instances (a counted array of the instances'
	// records); an instance's entities (a counted array of their records)
	constexpr uint32_t kTemplateEntities = 0x44;
	constexpr uint32_t kTemplateInstances = 0x64;
	constexpr uint32_t kInstanceChildren = 0x2C;
	constexpr uint32_t kArrayCount = 0x08;
	constexpr uint32_t kArrayData = 0x0C;
	constexpr int kMaxWantedPerComposite = 128;

	bool ReadTemplates(const uint8_t* manager, uintptr_t packFile, const uint32_t*& offsets, uint32_t& count)
	{
		__try
		{
			const uint32_t offset = *reinterpret_cast<const uint32_t*>(manager + kManagerTemplates);
			count = *reinterpret_cast<const uint32_t*>(manager + kManagerTemplates + 4);
			offsets = offset == 0xFFFFFFFF ? nullptr : reinterpret_cast<const uint32_t*>(packFile + offset * 4u);
			return offsets != nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// One composite: the entities of the wanted types in each of its running instances (guarded; what was found before a
	// fault is kept)
	int ScanComposite(uintptr_t packFile, const uint32_t* offsets, uint32_t index, const uint32_t* types, int typeCount, SCRIPT_DEBUG::FoundEntity* out, int max)
	{
		volatile int found = 0;
		__try
		{
			const uint32_t packed = offsets[index];
			if (packed == 0xFFFFFFFF)
				return 0;
			const uint8_t* composite = reinterpret_cast<const uint8_t*>(packFile + packed * 4u);
			const uint32_t entitiesOffset = *reinterpret_cast<const uint32_t*>(composite + kTemplateEntities);
			const uint32_t entityCount = *reinterpret_cast<const uint32_t*>(composite + kTemplateEntities + 4);
			if (entitiesOffset == 0xFFFFFFFF || entityCount == 0)
				return 0;
			const uint32_t* entities = reinterpret_cast<const uint32_t*>(packFile + entitiesOffset * 4u);
			uint32_t ids[kMaxWantedPerComposite], idTypes[kMaxWantedPerComposite];
			int idCount = 0;
			for (uint32_t i = 0; i < entityCount && idCount < kMaxWantedPerComposite; i++)
				for (int t = 0; t < typeCount; t++)
					if (entities[i * 2 + 1] == types[t])
					{
						ids[idCount] = entities[i * 2];
						idTypes[idCount++] = types[t];
						break;
					}
			if (idCount == 0)
				return 0;
			const uint8_t* instances = *reinterpret_cast<const uint8_t* const*>(composite + kTemplateInstances);
			if (!instances)
				return 0;
			const uint32_t instanceCount = *reinterpret_cast<const uint32_t*>(instances + kArrayCount);
			const uint8_t* const* instanceRecords = *reinterpret_cast<const uint8_t* const* const*>(instances + kArrayData);
			for (uint32_t i = 0; instanceRecords && i < instanceCount && found < max; i++)
			{
				const uint8_t* instance = instanceRecords[i] ? *reinterpret_cast<const uint8_t* const*>(instanceRecords[i] + kRecordObject) : nullptr;
				if (!instance)
					continue;
				const uint8_t* children = *reinterpret_cast<const uint8_t* const*>(instance + kInstanceChildren);
				if (!children)
					continue;
				const uint32_t childCount = *reinterpret_cast<const uint32_t*>(children + kArrayCount);
				const uint8_t* const* childRecords = *reinterpret_cast<const uint8_t* const* const*>(children + kArrayData);
				for (uint32_t c = 0; childRecords && c < childCount && found < max; c++)
				{
					const uint8_t* child = childRecords[c] ? *reinterpret_cast<const uint8_t* const*>(childRecords[c] + kRecordObject) : nullptr;
					if (!child)
						continue;
					const uint32_t id = *reinterpret_cast<const uint32_t*>(child + kEntityGuid);
					for (int w = 0; w < idCount; w++)
						if (ids[w] == id)
						{
							out[found].record = childRecords[c];
							out[found].type = idTypes[w];
							found = found + 1;
							break;
						}
				}
			}
			return found;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return found;
		}
	}
}

bool SCRIPT_DEBUG::FindEntities(const uint32_t* types, int typeCount, std::vector<FoundEntity>& out, size_t max)
{
	out.clear();
	if (!LevelRunning())
		return false;
	const uint8_t* manager = EntityManager();
	const uintptr_t packAt = DEVTOOLS_RELATIVE_ADDRESS(kPackFile);
	const uintptr_t packFile = packAt ? *reinterpret_cast<const uintptr_t*>(packAt) : 0;
	const uint32_t* offsets = nullptr;
	uint32_t count = 0;
	if (!manager || !packFile || !ReadTemplates(manager, packFile, offsets, count))
		return false;
	std::vector<FoundEntity> found(max);
	size_t total = 0;
	for (uint32_t i = 0; i < count && total < max; i++)
		total += ScanComposite(packFile, offsets, i, types, typeCount, found.data() + total, static_cast<int>(max - total));
	found.resize(total);
	out.swap(found);
	return true;
}

bool SCRIPT_DEBUG::PatchCall(uintptr_t steamSite, uintptr_t steamTarget, const void* replacement, PatchedCall& out, const char*& why)
{
	const uintptr_t target = DEVTOOLS_RELATIVE_ADDRESS(steamTarget);
	if (!target)
	{
		out = PatchedCall();
		why = "what it calls is not known in this build";
		return false;
	}
	return PatchCallTo(steamSite, target, replacement, out, why);
}

bool SCRIPT_DEBUG::PatchCallTo(uintptr_t steamSite, uintptr_t target, const void* replacement, PatchedCall& out, const char*& why)
{
	out = PatchedCall();
	uint8_t* at = reinterpret_cast<uint8_t*>(DEVTOOLS_RELATIVE_ADDRESS(steamSite));
	if (!at || !target)
	{
		why = "not known in this build";
		return false;
	}
	uint8_t bytes[5];
	if (!ReadCall(at, bytes) || bytes[0] != kCallOpcode)
	{
		why = "not a call";
		return false;
	}
	int32_t distance;
	memcpy(&distance, bytes + 1, sizeof(distance));
	if (distance != DistanceTo(at, target))
	{
		why = "does not call what it should (changed by another tool?)";
		return false;
	}
	const int32_t patched = DistanceTo(at, reinterpret_cast<uintptr_t>(replacement));
	if (!WriteDistance(at, patched))
	{
		why = "could not be written";
		return false;
	}
	out.at = at;
	out.original = distance;
	out.patched = patched;
	why = nullptr;
	return true;
}

namespace
{
	bool ReadSlot(const uintptr_t* at, uintptr_t& value)
	{
		__try
		{
			value = *at;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool WriteSlot(uintptr_t* at, uintptr_t value)
	{
		DWORD oldProtect;
		if (!VirtualProtect(at, sizeof(*at), PAGE_READWRITE, &oldProtect))
			return false;
		InterlockedExchange(reinterpret_cast<volatile LONG*>(at), static_cast<LONG>(value));
		VirtualProtect(at, sizeof(*at), oldProtect, &oldProtect);
		return true;
	}
}

bool SCRIPT_DEBUG::PatchSlot(uintptr_t steamTable, uint32_t offset, uintptr_t steamExpected, const void* replacement, PatchedSlot& out, const char*& why)
{
	out = PatchedSlot();
	const uintptr_t table = DEVTOOLS_RELATIVE_ADDRESS(steamTable);
	const uintptr_t expected = DEVTOOLS_RELATIVE_ADDRESS(steamExpected);
	if (!table || !expected)
	{
		why = "not known in this build";
		return false;
	}
	uintptr_t* at = reinterpret_cast<uintptr_t*>(table + offset);
	uintptr_t value = 0;
	if (!ReadSlot(at, value) || value != expected)
	{
		why = "does not hold what it should (changed by another tool?)";
		return false;
	}
	if (!WriteSlot(at, reinterpret_cast<uintptr_t>(replacement)))
	{
		why = "could not be written";
		return false;
	}
	out.at = at;
	out.original = value;
	out.patched = reinterpret_cast<uintptr_t>(replacement);
	why = nullptr;
	return true;
}

bool SCRIPT_DEBUG::RestoreSlot(PatchedSlot& slot)
{
	if (!slot.at)
		return true;
	uintptr_t value = 0;
	if (!ReadSlot(slot.at, value) || value != slot.patched || !WriteSlot(slot.at, slot.original))
		return false;
	slot = PatchedSlot();
	return true;
}

bool SCRIPT_DEBUG::RestoreCall(PatchedCall& call)
{
	if (!call.at)
		return true;
	uint8_t bytes[5];
	int32_t now = 0;
	if (!ReadCall(call.at, bytes) || bytes[0] != kCallOpcode)
		return false;
	memcpy(&now, bytes + 1, sizeof(now));
	if (now != call.patched || !WriteDistance(call.at, call.original))
		return false;
	call = PatchedCall();
	return true;
}

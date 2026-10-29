#include "AI_DEBUG.h"
#include "DEBUG_MENU.h"

#include <algorithm>
#include <atomic>
#include <cstdio>
#include <cstring>
#include <sstream>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <imgui.h>

/*
	The AI group's Squads tab: how the game's group of NPCs is organised, as its director keeps it.

	- The group: every NPC in it, with the objective the group handed it (a place to move to, its start, cover or a safe
	  point) and the nearest safe point, its target and how far, whether it may melee the player (and is), whether it may
	  pursue, and its awareness of the player and the highest it has reached.
	- The squads the group made of them: each squad's centre, its aggression, its cover search (idle, waiting for the cover
	  manager, choosing) and its members' cover requests (the slot asked for and the one chosen), the targets it shares and
	  how many of its members search.
	- Drawn over the game when switched on: each squad's centre with lines to its members, each member's objective, and the
	  level's safe points.

	Everything is read on the render thread as the frame is presented, every read guarded (see DEBUG_MENU.h). Addresses and
	layouts are the Steam retail AI.exe's.
*/
namespace
{
	constexpr uintptr_t kGameGlobals = 0x012F0C88;
	constexpr uint32_t kCharactersValid = 0x04;
	constexpr uint32_t kGlobalsGroup = 0x5C;
	constexpr uint32_t kCharacterId = 0x50;

	// The group: its members (an array: the entries at +0x18, how many at +0x1C, 0x6C bytes each), its squads (a pointer to
	// the squad manager, whose list of squads is first), and the safe points (an array: entries at +0x11C, how many at
	// +0x120, 0x14 bytes each: an entity's two ids, then where)
	constexpr uint32_t kGroupMembers = 0x18;
	constexpr uint32_t kGroupMemberCount = 0x1C;
	constexpr uint32_t kGroupMemberSize = 0x6C;
	constexpr uint32_t kGroupSquads = 0x24;
	constexpr uint32_t kGroupSafePoints = 0x11C;
	constexpr uint32_t kGroupSafePointCount = 0x120;
	constexpr uint32_t kSafePointSize = 0x14;
	constexpr uint32_t kSafePointPosition = 0x08;
	// A member: the character (a checked pointer: the character at +4, its unique id at +8), its objective, the objective's
	// type, the nearest safe point, its target (a checked pointer), melee permission and melee, pursuit permission, the
	// distance to its target, and its awareness of the player and the highest it has reached
	constexpr uint32_t kMemberCharacter = 0x00;
	constexpr uint32_t kMemberObjective = 0x0C;
	constexpr uint32_t kMemberObjectiveType = 0x18;
	constexpr uint32_t kMemberSafePoint = 0x28;
	constexpr uint32_t kMemberTarget = 0x2C;
	constexpr uint32_t kMemberMeleeAllowed = 0x44;
	constexpr uint32_t kMemberMeleeing = 0x45;
	constexpr uint32_t kMemberPursuitAllowed = 0x4C;
	constexpr uint32_t kMemberDistance = 0x50;
	constexpr uint32_t kMemberAwareness = 0x58;
	constexpr uint32_t kMemberWatermark = 0x5C;
	// A checked pointer: the character, then its unique id
	constexpr uint32_t kCheckedCharacter = 0x04;
	constexpr uint32_t kCheckedId = 0x08;
	// A squad: its centre, id, members (an array: entries at +0x1C, how many at +0x20, 0x44 bytes each: the character as a
	// checked pointer, the cover request's state at +0xC, the slot asked for at +0x38 and the one chosen at +0x3C), its cover
	// search state, its targets (an array: entries at +0x88, how many at +0x8C, 0x18 bytes each: the target's two ids, then
	// how many members target it), its aggression and how many members search
	constexpr uint32_t kSquadCentre = 0x04;
	constexpr uint32_t kSquadId = 0x10;
	constexpr uint32_t kSquadMembers = 0x1C;
	constexpr uint32_t kSquadMemberCount = 0x20;
	constexpr uint32_t kSquadMemberSize = 0x44;
	constexpr uint32_t kSquadMemberCover = 0x0C;
	constexpr uint32_t kSquadMemberPending = 0x38;
	constexpr uint32_t kSquadMemberSelected = 0x3C;
	constexpr uint32_t kSquadCoverSearch = 0x38;
	constexpr uint32_t kSquadTargets = 0x88;
	constexpr uint32_t kSquadTargetCount = 0x8C;
	constexpr uint32_t kSquadTargetSize = 0x18;
	constexpr uint32_t kSquadTargetMembers = 0x08;
	constexpr uint32_t kSquadAggro = 0xB0;
	constexpr uint32_t kSquadSearching = 0x138;

	constexpr int kMaxMembers = 64;
	constexpr int kMaxSquads = 16;
	constexpr int kMaxSquadMembers = 12;
	constexpr int kMaxTargets = 8;
	constexpr int kMaxSafePoints = 256;

	struct Member
	{
		const uint8_t* character = nullptr;
		uint32_t id = 0;
		float objective[3] = {};
		int objectiveType = 255;
		int safePoint = -1;
		const uint8_t* target = nullptr;
		uint32_t targetId = 0;
		bool meleeAllowed = false, meleeing = false, pursuitAllowed = false;
		float distance = 0.0f;
		int awareness = -1, watermark = -1;
	};

	struct SquadMember
	{
		const uint8_t* character = nullptr;
		uint32_t id = 0;
		int coverState = 0;
		uint32_t pendingSlot = 0, selectedSlot = 0;
	};

	struct SquadTarget
	{
		uint32_t entity = 0, instance = 0;
		int members = 0;
	};

	struct Squad
	{
		uint32_t id = 0;
		float centre[3] = {};
		int aggro = 0, coverSearch = 0, searching = 0;
		int memberCount = 0, memberTotal = 0;
		SquadMember members[kMaxSquadMembers];
		int targetCount = 0;
		SquadTarget targets[kMaxTargets];
	};

	struct Snapshot
	{
		int frame = -1;
		bool have = false;
		int memberCount = 0;
		Member members[kMaxMembers];
		int squadCount = 0;
		Squad squads[kMaxSquads];
		int safePointCount = 0, safePointTotal = 0;
		float safePoints[kMaxSafePoints][3] = {};
	};
	Snapshot g_snapshot;

	std::atomic<bool> g_draw = false, g_drawSafePoints = false;

	template <typename T> T At(const uint8_t* base, uint32_t offset)
	{
		return *reinterpret_cast<const T*>(base + offset);
	}

	const uint8_t* Pointer(const uint8_t* base, uint32_t offset)
	{
		return *reinterpret_cast<const uint8_t* const*>(base + offset);
	}

	// A checked pointer's character, when it still is the one it was set to
	void Checked(const uint8_t* at, const uint8_t*& character, uint32_t& id)
	{
		character = Pointer(at, kCheckedCharacter);
		id = At<uint32_t>(at, kCheckedId);
		if (character && At<uint32_t>(character, kCharacterId) != id)
			character = nullptr;
	}

	const uint8_t* Group()
	{
		__try
		{
			const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kGameGlobals);
			if (!at || !*reinterpret_cast<const uint8_t*>(at + kCharactersValid))
				return nullptr;
			const uint8_t* globals = *reinterpret_cast<const uint8_t* const*>(at);
			return globals ? Pointer(globals, kGlobalsGroup) : nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	bool ReadMembers(const uint8_t* group, Snapshot& out)
	{
		out.memberCount = 0;
		__try
		{
			const uint8_t* members = Pointer(group, kGroupMembers);
			const uint32_t count = At<uint32_t>(group, kGroupMemberCount);
			for (uint32_t i = 0; members && i < count && out.memberCount < kMaxMembers; i++)
			{
				const uint8_t* member = members + i * kGroupMemberSize;
				Member& shown = out.members[out.memberCount++];
				Checked(member + kMemberCharacter, shown.character, shown.id);
				memcpy(shown.objective, member + kMemberObjective, sizeof(shown.objective));
				shown.objectiveType = At<int>(member, kMemberObjectiveType);
				shown.safePoint = At<int16_t>(member, kMemberSafePoint);
				Checked(member + kMemberTarget, shown.target, shown.targetId);
				shown.meleeAllowed = At<uint8_t>(member, kMemberMeleeAllowed) != 0;
				shown.meleeing = At<uint8_t>(member, kMemberMeleeing) != 0;
				shown.pursuitAllowed = At<uint8_t>(member, kMemberPursuitAllowed) != 0;
				shown.distance = At<float>(member, kMemberDistance);
				shown.awareness = At<int>(member, kMemberAwareness);
				shown.watermark = At<int>(member, kMemberWatermark);
			}
			const uint8_t* points = Pointer(group, kGroupSafePoints);
			out.safePointTotal = At<int>(group, kGroupSafePointCount);
			out.safePointCount = 0;
			for (int i = 0; points && i < out.safePointTotal && out.safePointCount < kMaxSafePoints; i++)
				memcpy(out.safePoints[out.safePointCount++], points + i * kSafePointSize + kSafePointPosition, sizeof(float) * 3);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool ReadSquad(const uint8_t* squad, Squad& out)
	{
		__try
		{
			out.id = At<uint32_t>(squad, kSquadId);
			memcpy(out.centre, squad + kSquadCentre, sizeof(out.centre));
			out.aggro = At<int>(squad, kSquadAggro);
			out.coverSearch = At<int>(squad, kSquadCoverSearch);
			out.searching = At<int>(squad, kSquadSearching);
			const uint8_t* members = Pointer(squad, kSquadMembers);
			out.memberTotal = At<int>(squad, kSquadMemberCount);
			out.memberCount = 0;
			for (int i = 0; members && i < out.memberTotal && out.memberCount < kMaxSquadMembers; i++)
			{
				const uint8_t* member = members + i * kSquadMemberSize;
				SquadMember& shown = out.members[out.memberCount++];
				Checked(member, shown.character, shown.id);
				shown.coverState = At<int>(member, kSquadMemberCover);
				shown.pendingSlot = At<uint32_t>(member, kSquadMemberPending);
				shown.selectedSlot = At<uint32_t>(member, kSquadMemberSelected);
			}
			const uint8_t* targets = Pointer(squad, kSquadTargets);
			const int targetCount = At<int>(squad, kSquadTargetCount);
			out.targetCount = 0;
			for (int i = 0; targets && i < targetCount && out.targetCount < kMaxTargets; i++)
			{
				const uint8_t* target = targets + i * kSquadTargetSize;
				SquadTarget& shown = out.targets[out.targetCount++];
				shown.entity = At<uint32_t>(target, 0);
				shown.instance = At<uint32_t>(target, 4);
				shown.members = At<int>(target, kSquadTargetMembers);
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// The squads: a list (its end node, whose next is the first; each node the next, the one before, then the squad as a
	// counted pointer, the object first)
	int ReadSquadPointers(const uint8_t* group, const uint8_t** out, int max)
	{
		__try
		{
			const uint8_t* manager = Pointer(group, kGroupSquads);
			if (!manager)
				return 0;
			const uint8_t* end = Pointer(manager, 0);
			const uint8_t* node = end ? Pointer(end, 0) : nullptr;
			int found = 0;
			for (int seen = 0; node && node != end && seen < 64 && found < max; seen++, node = Pointer(node, 0))
				if (const uint8_t* squad = Pointer(node, 0x08))
					out[found++] = squad;
			return found;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	const Snapshot& Read()
	{
		const int frame = ImGui::GetFrameCount();
		if (g_snapshot.frame == frame)
			return g_snapshot;
		g_snapshot.frame = frame;
		g_snapshot.have = false;
		g_snapshot.memberCount = g_snapshot.squadCount = g_snapshot.safePointCount = 0;
		const uint8_t* group = DEBUG_MENU::Characters().empty() ? nullptr : Group();
		if (!group)
			return g_snapshot;
		g_snapshot.have = ReadMembers(group, g_snapshot);
		const uint8_t* squads[kMaxSquads];
		const int count = ReadSquadPointers(group, squads, kMaxSquads);
		for (int i = 0; i < count; i++)
			if (ReadSquad(squads[i], g_snapshot.squads[g_snapshot.squadCount]))
				g_snapshot.squadCount++;
		return g_snapshot;
	}

	// ---- Names ----

	const DEBUG_MENU::Character* CharacterOf(const uint8_t* object, uint32_t id)
	{
		if (!object)
			return nullptr;
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
			if (character.object == object && character.id == id)
				return &character;
		return nullptr;
	}

	std::string NameOf(const uint8_t* object, uint32_t id)
	{
		const DEBUG_MENU::Character* character = CharacterOf(object, id);
		return character ? character->name : object ? "(not in the list)" : "-";
	}

	const char* ObjectiveName(int type)
	{
		switch (type)
		{
		case 0: return "Move";
		case 1: return "Its start";
		case 2: return "Cover";
		case 3: return "Safe point";
		default: return "None";
		}
	}

	const char* CoverRequestName(int state)
	{
		static const char* const kNames[] = { "None", "Pending", "In progress", "Choosing", "Finished", "Failed" };
		return state >= 0 && state < 6 ? kNames[state] : "?";
	}

	const char* CoverSearchName(int state)
	{
		static const char* const kNames[] = { "Idle", "Waiting for the cover manager", "Choosing cover" };
		return state >= 0 && state < 3 ? kNames[state] : "?";
	}

	// A cover slot's id, or "none" for the game's "no slot" (all bits set)
	std::string SlotText(uint32_t slot)
	{
		return slot == 0xFFFFFFFFu ? std::string("none") : std::to_string(slot);
	}

	// ---- The overlay ----

	bool Register();

	// Render thread, every frame: the switches and commands named on the first frame, as the other views name theirs
	void DrawOverlay(ImDrawList* drawList)
	{
		static const bool registered = Register();
		(void)registered;
		if (!g_draw && !g_drawSafePoints)
			return;
		const Snapshot& snapshot = Read();
		if (!snapshot.have)
			return;
		if (g_drawSafePoints)
			for (int i = 0; i < snapshot.safePointCount; i++)
				DEBUG_MENU::WorldCircle(drawList, snapshot.safePoints[i], 0.4f, IM_COL32(90, 230, 120, 200), 12, 1.5f);
		if (!g_draw)
			return;
		for (int i = 0; i < snapshot.memberCount; i++)
		{
			const Member& member = snapshot.members[i];
			const DEBUG_MENU::Character* character = CharacterOf(member.character, member.id);
			if (!character || character->dead || member.objectiveType == 255)
				continue;
			DEBUG_MENU::WorldArrow(drawList, character->position, member.objective, IM_COL32(120, 200, 255, 200), 1.5f);
			const float above[3] = { member.objective[0], member.objective[1] + 0.4f, member.objective[2] };
			char text[96];
			snprintf(text, sizeof(text), "%s: %s", character->name.c_str(), ObjectiveName(member.objectiveType));
			DEBUG_MENU::WorldText(drawList, above, text, IM_COL32(120, 200, 255, 230));
		}
		for (int s = 0; s < snapshot.squadCount; s++)
		{
			const Squad& squad = snapshot.squads[s];
			const ImU32 colour = IM_COL32(255, 200, 80, 230);
			DEBUG_MENU::WorldCross(drawList, squad.centre, 0.5f, colour, 2.0f);
			for (int m = 0; m < squad.memberCount; m++)
				if (const DEBUG_MENU::Character* character = CharacterOf(squad.members[m].character, squad.members[m].id))
					DEBUG_MENU::WorldLine(drawList, squad.centre, character->position, IM_COL32(255, 200, 80, 120), 1.0f);
			const float above[3] = { squad.centre[0], squad.centre[1] + 1.0f, squad.centre[2] };
			char text[96];
			snprintf(text, sizeof(text), "Squad %u: %s, %d member(s)", squad.id, AI_DEBUG::AggroName(squad.aggro), squad.memberTotal);
			DEBUG_MENU::WorldText(drawList, above, text, colour);
		}
	}

	// ---- The tab ----

	void Switch(const char* label, std::atomic<bool>& value)
	{
		bool on = value;
		if (ImGui::Checkbox(label, &on))
			value = on;
	}

	void DrawTab()
	{
		const Snapshot& snapshot = Read();
		if (!snapshot.have)
		{
			ImGui::TextDisabled("No group of NPCs (no level running).");
			return;
		}
		ImGui::SeparatorText("Draw over the game");
		Switch("Squads and objectives", g_draw);
		ImGui::SameLine();
		Switch("Safe points", g_drawSafePoints);

		ImGui::SeparatorText("The group of NPCs");
		// Each column as wide as what it holds, scrolled sideways (the names kept in view) when they don't all fit: the header
		// and a row per NPC, up to ten, with room under them for the sideways scrollbar
		const float rows = (std::min<float>)(static_cast<float>(snapshot.memberCount) + 1.0f, 11.0f);
		const float height = ImGui::GetTextLineHeightWithSpacing() * rows + ImGui::GetStyle().ScrollbarSize + ImGui::GetStyle().CellPadding.y * 2.0f;
		if (snapshot.memberCount == 0)
			ImGui::TextDisabled("No NPCs in it now (the alien is never part of it).");
		else if (ImGui::BeginTable("group", 7, ImGuiTableFlags_RowBg | ImGuiTableFlags_BordersInnerV | ImGuiTableFlags_SizingFixedFit | ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY,
			ImVec2(0.0f, height)))
		{
			ImGui::TableSetupScrollFreeze(1, 1);
			ImGui::TableSetupColumn("NPC");
			ImGui::TableSetupColumn("Awareness (highest)");
			ImGui::TableSetupColumn("Objective");
			ImGui::TableSetupColumn("Safe point");
			ImGui::TableSetupColumn("Target");
			ImGui::TableSetupColumn("Melee");
			ImGui::TableSetupColumn("Pursuit");
			ImGui::TableHeadersRow();
			for (int i = 0; i < snapshot.memberCount; i++)
			{
				const Member& member = snapshot.members[i];
				ImGui::TableNextRow();
				ImGui::TableSetColumnIndex(0);
				ImGui::TextUnformatted(NameOf(member.character, member.id).c_str());
				ImGui::TableSetColumnIndex(1);
				ImGui::Text("%s (%s)", AI_DEBUG::AwarenessName(member.awareness), AI_DEBUG::AwarenessName(member.watermark));
				ImGui::TableSetColumnIndex(2);
				if (member.objectiveType == 255)
					ImGui::TextDisabled("None");
				else
					ImGui::Text("%s at %.1f, %.1f, %.1f", ObjectiveName(member.objectiveType), member.objective[0], member.objective[1], member.objective[2]);
				ImGui::TableSetColumnIndex(3);
				if (member.safePoint >= 0)
					ImGui::Text("%d", member.safePoint);
				ImGui::TableSetColumnIndex(4);
				if (member.target)
					ImGui::Text("%s, %.1f m", NameOf(member.target, member.targetId).c_str(), member.distance);
				ImGui::TableSetColumnIndex(5);
				ImGui::TextUnformatted(member.meleeing ? "attacking" : member.meleeAllowed ? "allowed" : "-");
				ImGui::TableSetColumnIndex(6);
				ImGui::TextUnformatted(member.pursuitAllowed ? "allowed" : "-");
			}
			ImGui::EndTable();
		}
		ImGui::Text("%d safe point(s)", snapshot.safePointTotal);

		ImGui::SeparatorText("Squads");
		if (snapshot.squadCount == 0)
		{
			ImGui::TextDisabled("No squads now (the game makes them as NPCs fight together).");
			return;
		}
		for (int s = 0; s < snapshot.squadCount; s++)
		{
			const Squad& squad = snapshot.squads[s];
			char header[128];
			snprintf(header, sizeof(header), "Squad %u: %s, %d member(s), %d searching###squad%d", squad.id, AI_DEBUG::AggroName(squad.aggro), squad.memberTotal, squad.searching, s);
			if (!ImGui::CollapsingHeader(header, ImGuiTreeNodeFlags_DefaultOpen))
				continue;
			ImGui::Text("Centre %.1f, %.1f, %.1f; cover search: %s", squad.centre[0], squad.centre[1], squad.centre[2], CoverSearchName(squad.coverSearch));
			for (int m = 0; m < squad.memberCount; m++)
			{
				const SquadMember& member = squad.members[m];
				ImGui::BulletText("%s: cover %s (cover slot asked for: %s; chosen: %s)", NameOf(member.character, member.id).c_str(), CoverRequestName(member.coverState),
					SlotText(member.pendingSlot).c_str(), SlotText(member.selectedSlot).c_str());
			}
			for (int t = 0; t < squad.targetCount; t++)
				ImGui::BulletText("Target: entity 0x%08X in 0x%08X, %d member(s) on it", squad.targets[t].entity, squad.targets[t].instance, squad.targets[t].members);
		}
	}

	// ---- Commands ----

	DEBUG_MENU::CommandResult Answer(bool ok, const std::string& message)
	{
		DEBUG_MENU::CommandResult result;
		result.ok = ok;
		result.message = message;
		return result;
	}

	bool Register()
	{
		DEBUG_MENU::AddSwitch("ai.squads.draw", &g_draw);
		DEBUG_MENU::AddSwitch("ai.squads.safe_points", &g_drawSafePoints);
		DEBUG_MENU::AddCommand("ai.list_group", "the group of NPCs, one a line: name awareness(highest) objective target melee pursuit", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]() -> DEBUG_MENU::CommandResult
			{
				const Snapshot& snapshot = Read();
				if (!snapshot.have)
					return Answer(false, "no group of NPCs (no level running)");
				std::ostringstream out;
				for (int i = 0; i < snapshot.memberCount; i++)
				{
					const Member& member = snapshot.members[i];
					out << NameOf(member.character, member.id) << " awareness=" << AI_DEBUG::AwarenessName(member.awareness) << "(" << AI_DEBUG::AwarenessName(member.watermark) << ")"
						<< " objective=" << ObjectiveName(member.objectiveType);
					if (member.objectiveType != 255)
						out << "@" << member.objective[0] << "," << member.objective[1] << "," << member.objective[2];
					out << " target=" << NameOf(member.target, member.targetId) << " melee=" << (member.meleeing ? "attacking" : member.meleeAllowed ? "allowed" : "no")
						<< " pursuit=" << (member.pursuitAllowed ? "allowed" : "no") << "\n";
				}
				out << "safe_points=" << snapshot.safePointTotal << "\n";
				return Answer(true, out.str());
			});
		});
		DEBUG_MENU::AddCommand("ai.list_squads", "the squads, one a line: id aggression members searching cover-search, then each member's cover", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]() -> DEBUG_MENU::CommandResult
			{
				const Snapshot& snapshot = Read();
				if (!snapshot.have)
					return Answer(false, "no group of NPCs (no level running)");
				std::ostringstream out;
				for (int s = 0; s < snapshot.squadCount; s++)
				{
					const Squad& squad = snapshot.squads[s];
					out << "squad=" << squad.id << " aggro=" << AI_DEBUG::AggroName(squad.aggro) << " members=" << squad.memberTotal << " searching=" << squad.searching
						<< " cover_search=" << CoverSearchName(squad.coverSearch) << " centre=" << squad.centre[0] << "," << squad.centre[1] << "," << squad.centre[2] << "\n";
					for (int m = 0; m < squad.memberCount; m++)
						out << "  " << NameOf(squad.members[m].character, squad.members[m].id) << " cover=" << CoverRequestName(squad.members[m].coverState)
							<< " slot=" << SlotText(squad.members[m].selectedSlot) << "\n";
				}
				if (snapshot.squadCount == 0)
					out << "none\n";
				return Answer(true, out.str());
			});
		});
		return true;
	}

	const bool g_tab = DEBUG_MENU::AddTab("Squads", 30, DrawTab, "AI");
	const bool g_overlay = DEBUG_MENU::AddOverlay(13, DrawOverlay);
}

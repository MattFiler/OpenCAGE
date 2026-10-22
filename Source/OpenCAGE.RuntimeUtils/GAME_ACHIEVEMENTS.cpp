#include "DEBUG_MENU.h"
#include "Config.h"

#include <atomic>
#include <cctype>
#include <cstdio>
#include <cstring>
#include <mutex>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <imgui.h>

/*
	Addresses and layouts are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).

	The Saves tab's achievements section: the game's achievements as it holds them (each one's name and whether the
	player has it), and a switch ("game.block_achievements", on from the start with BlockAchievements=1 in the ini) that
	keeps the game from awarding achievements or changing their stats on the player's profile - so a modded level, a
	debug checkpoint or a cheat can't unlock one or move its progress.

	The achievements are kept by one object of the platform's own kind (Steam's, GOG's or the Epic Games Store's), all
	made from the same base, whose virtual functions do every write to the profile: award an achievement, add to a stat,
	raise a stat to a value, and take one or all achievements back (the GOG build's table has two more entries before the
	award, so its award is two further on). Those entries of its virtual function table are pointed at functions here that
	return at once while the switch is on (and pass the call on otherwise), as soon as the object exists; they are put
	back as the hooks detach. The game's own note of an achievement as won may still change in memory (it goes into the
	next save, which Don't write saves keeps back too).
*/
namespace
{
	// The platform service (a pointer to it), whose +0x4F4 is the achievements
	constexpr uintptr_t kPlatformService = 0x017E4684;
	constexpr uint32_t kServiceAchievements = 0x4F4;

	// The achievements: their records (a pointer to them, how many) and the stats' (the same). An achievement's record: its
	// name (as the platform knows it) then, at +0x30, whether the player has it (1 no, 2 yes, 0 not known yet) and the
	// count it unlocks at
	constexpr uint32_t kAchievementsData = 0x08;
	constexpr uint32_t kAchievementsCount = 0x0C;
	constexpr uint32_t kStatsCount = 0x1C;
	constexpr uint32_t kAchievementSize = 0x38;
	constexpr uint32_t kAchievementNameLength = 0x20;
	constexpr uint32_t kAchievementState = 0x30;
	constexpr uint32_t kAchievementLimit = 0x34;
	constexpr uint32_t kMaxAchievements = 256;
	constexpr int kStateNo = 1;
	constexpr int kStateYes = 2;

	// The entries of the achievements' virtual function table that write to the profile
	constexpr int kSlotUnaward = 1;
	constexpr int kSlotAddToStat = 3;
	constexpr int kSlotRaiseStat = 4;
	constexpr int kSlotUnawardAll = 5;
	constexpr int kSlotAward = 13;
	constexpr int kSlotAwardGog = 15; // the GOG build's (its entries 12 and 13 are empty ones of its own)

	typedef void(__thiscall* t_by_name)(void* self, const char* name);
	typedef void(__thiscall* t_by_name_value)(void* self, const char* name, int value);
	typedef void(__thiscall* t_no_arguments)(void* self);
	typedef void(__thiscall* t_by_record)(void* self, const void* record);

	std::atomic<bool> g_block = false;
	std::atomic<uint32_t> g_blocked = 0;
	std::mutex g_lastMutex;
	std::string g_last;
	ULONGLONG g_lastTick = 0;

	void Note(const char* what, const char* name, int value, bool withValue)
	{
		char text[160];
		if (withValue)
			snprintf(text, sizeof(text), "%s %s %d", what, name ? name : "?", value);
		else
			snprintf(text, sizeof(text), "%s %s", what, name ? name : "");
		g_blocked++;
		{
			std::lock_guard<std::mutex> lock(g_lastMutex);
			g_last = text;
			g_lastTick = GetTickCount64();
		}
		DevTools::Log("Block achievements: kept back %s", text);
	}

	// A name in the game's memory, copied out
	void CopyName(const void* at, char* out, size_t size)
	{
		out[0] = '\0';
		__try
		{
			const char* text = static_cast<const char*>(at);
			size_t i = 0;
			for (; i + 1 < size && i < kAchievementNameLength && text[i]; i++)
				out[i] = text[i] >= 32 && text[i] < 127 ? text[i] : '?';
			out[i] = '\0';
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			out[0] = '\0';
		}
	}

	// ---- The table entries ----

	struct Slot
	{
		int index;
		void* replacement;
		void* original;      // the game's function the entry held
		void** entry;        // the entry (null: not replaced)
	};

	void __fastcall BlockUnaward(void* self, void* edx, const char* name);
	void __fastcall BlockAddToStat(void* self, void* edx, const char* name, int value);
	void __fastcall BlockRaiseStat(void* self, void* edx, const char* name, int value);
	void __fastcall BlockUnawardAll(void* self, void* edx);
	void __fastcall BlockAward(void* self, void* edx, const void* record);

	Slot g_slots[] = {
		{ kSlotUnaward, reinterpret_cast<void*>(&BlockUnaward), nullptr, nullptr },
		{ kSlotAddToStat, reinterpret_cast<void*>(&BlockAddToStat), nullptr, nullptr },
		{ kSlotRaiseStat, reinterpret_cast<void*>(&BlockRaiseStat), nullptr, nullptr },
		{ kSlotUnawardAll, reinterpret_cast<void*>(&BlockUnawardAll), nullptr, nullptr },
		{ kSlotAward, reinterpret_cast<void*>(&BlockAward), nullptr, nullptr },
	};
	enum SlotIndex { Unaward, AddToStat, RaiseStat, UnawardAll, Award };

	void __fastcall BlockUnaward(void* self, void* /*edx*/, const char* name)
	{
		if (g_block)
		{
			char text[40];
			CopyName(name, text, sizeof(text));
			Note("taking back", text, 0, false);
			return;
		}
		reinterpret_cast<t_by_name>(g_slots[Unaward].original)(self, name);
	}

	void __fastcall BlockAddToStat(void* self, void* /*edx*/, const char* name, int value)
	{
		if (g_block)
		{
			char text[40];
			CopyName(name, text, sizeof(text));
			Note("adding to stat", text, value, true);
			return;
		}
		reinterpret_cast<t_by_name_value>(g_slots[AddToStat].original)(self, name, value);
	}

	void __fastcall BlockRaiseStat(void* self, void* /*edx*/, const char* name, int value)
	{
		if (g_block)
		{
			char text[40];
			CopyName(name, text, sizeof(text));
			Note("raising stat", text, value, true);
			return;
		}
		reinterpret_cast<t_by_name_value>(g_slots[RaiseStat].original)(self, name, value);
	}

	void __fastcall BlockUnawardAll(void* self, void* /*edx*/)
	{
		if (g_block)
		{
			Note("taking back every achievement", "", 0, false);
			return;
		}
		reinterpret_cast<t_no_arguments>(g_slots[UnawardAll].original)(self);
	}

	void __fastcall BlockAward(void* self, void* /*edx*/, const void* record)
	{
		if (g_block)
		{
			char text[40];
			CopyName(record, text, sizeof(text));
			Note("awarding", text, 0, false);
			return;
		}
		reinterpret_cast<t_by_record>(g_slots[Award].original)(self, record);
	}

	uint8_t* Achievements()
	{
		__try
		{
			const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kPlatformService);
			uint8_t* service = at ? *reinterpret_cast<uint8_t**>(at) : nullptr;
			return service ? *reinterpret_cast<uint8_t**>(service + kServiceAchievements) : nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	void** VirtualTable(const uint8_t* object)
	{
		__try
		{
			return object ? *reinterpret_cast<void** const*>(object) : nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	void* ReadEntry(void** entry)
	{
		__try
		{
			return *entry;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	bool WriteEntry(void** entry, void* value)
	{
		DWORD protection = 0;
		if (!VirtualProtect(entry, sizeof(*entry), PAGE_READWRITE, &protection))
			return false;
		*reinterpret_cast<void* volatile*>(entry) = value;
		VirtualProtect(entry, sizeof(*entry), protection, &protection);
		return true;
	}

	std::atomic<bool> g_attached = false;   // the debug menu's hooks are in
	bool g_patched = false;                 // render thread (and the detach)
	std::mutex g_patchMutex;

	// Render thread, until done: the table entries pointed here once the achievements exist
	void Patch()
	{
		if (g_patched || !g_attached)
			return;
		uint8_t* achievements = Achievements();
		void** table = VirtualTable(achievements);
		if (!table)
			return;
		std::lock_guard<std::mutex> lock(g_patchMutex);
		g_slots[Award].index = DevTools::Build() == DevTools::GameBuild::GOG ? kSlotAwardGog : kSlotAward;
		for (Slot& slot : g_slots)
		{
			void** entry = table + slot.index;
			void* original = ReadEntry(entry);
			if (!original || original == slot.replacement)
				continue;
			slot.original = original;
			if (WriteEntry(entry, slot.replacement))
				slot.entry = entry;
		}
		g_patched = true;
		DevTools::Log("Block achievements: the achievements' table entries are pointed at the switch (%s)", g_block ? "on" : "off");
	}

	void Unpatch()
	{
		std::lock_guard<std::mutex> lock(g_patchMutex);
		for (Slot& slot : g_slots)
		{
			if (slot.entry && *slot.entry == slot.replacement)
				WriteEntry(slot.entry, slot.original);
			slot.entry = nullptr;
		}
		g_patched = false;
	}

	// ---- Reading the list ----

	struct Achievement
	{
		char name[kAchievementNameLength + 1] = {};
		int state = 0;
		uint32_t limit = 0;
	};

	bool ReadAchievement(const uint8_t* record, Achievement& out)
	{
		CopyName(record, out.name, sizeof(out.name));
		__try
		{
			out.state = *reinterpret_cast<const int*>(record + kAchievementState);
			out.limit = *reinterpret_cast<const uint32_t*>(record + kAchievementLimit);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool ReadCounts(const uint8_t* achievements, const uint8_t*& records, uint32_t& count, uint32_t& stats)
	{
		__try
		{
			records = *reinterpret_cast<const uint8_t* const*>(achievements + kAchievementsData);
			count = *reinterpret_cast<const uint32_t*>(achievements + kAchievementsCount);
			stats = *reinterpret_cast<const uint32_t*>(achievements + kStatsCount);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	std::vector<Achievement> ReadList(uint32_t& stats)
	{
		std::vector<Achievement> list;
		stats = 0;
		const uint8_t* achievements = Achievements();
		const uint8_t* records = nullptr;
		uint32_t count = 0;
		if (!achievements || !ReadCounts(achievements, records, count, stats) || !records)
			return list;
		for (uint32_t i = 0; i < count && i < kMaxAchievements; i++)
		{
			Achievement achievement;
			if (ReadAchievement(records + i * kAchievementSize, achievement))
				list.push_back(achievement);
		}
		return list;
	}

	const char* StateName(int state)
	{
		return state == kStateYes ? "unlocked" : state == kStateNo ? "locked" : "not known yet";
	}

	// ---- The window ----

	void Tooltip(const char* text)
	{
		if (!ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip | ImGuiHoveredFlags_AllowWhenDisabled))
			return;
		ImGui::BeginTooltip();
		ImGui::PushTextWrapPos(ImGui::GetFontSize() * 30.0f);
		ImGui::TextUnformatted(text);
		ImGui::PopTextWrapPos();
		ImGui::EndTooltip();
	}

	std::string LastText()
	{
		std::lock_guard<std::mutex> lock(g_lastMutex);
		if (g_last.empty())
			return std::string();
		char text[220];
		snprintf(text, sizeof(text), "%s (%llu s ago)", g_last.c_str(), (GetTickCount64() - g_lastTick) / 1000);
		return text;
	}

	void RegisterOnce()
	{
		static bool registered = false;
		if (registered)
			return;
		registered = true;
		DEBUG_MENU::AddSwitch("game.block_achievements", &g_block);
		DEBUG_MENU::AddCommand("game.list_achievements", "the game's achievements: name, unlocked / locked / not known yet; then what was blocked", [](const std::string&)
		{
			uint32_t stats = 0;
			const std::vector<Achievement> list = ReadList(stats);
			std::string text;
			for (const Achievement& achievement : list)
				text += std::string(achievement.name) + " " + StateName(achievement.state) + "\n";
			char line[200];
			snprintf(line, sizeof(line), "achievements=%u stats=%u\nblock_achievements=%d\nentries_replaced=%d\nblocked=%u\n", static_cast<unsigned>(list.size()), stats,
				g_block ? 1 : 0, g_patched ? 1 : 0, g_blocked.load());
			text += line;
			const std::string last = LastText();
			if (!last.empty())
				text += "last_blocked=" + last + "\n";
			return DEBUG_MENU::CommandResult{ !list.empty(), list.empty() ? std::string("the achievements are not up yet\n") + line : text };
		});
	}

	void Apply(ImDrawList* /*drawList*/)
	{
		RegisterOnce();
		Patch();
	}

	char g_filter[48] = {};

	void DrawSection()
	{
		RegisterOnce();
		ImGui::SeparatorText("Achievements");
		bool block = g_block;
		ImGui::BeginDisabled(!g_attached);
		if (ImGui::Checkbox("Block awards and stat writes", &block))
			g_block = block;
		ImGui::EndDisabled();
		Tooltip("The game awards no achievement and changes no achievement stat on the player's profile while this is on (its own note of an achievement as won may still change in memory). BlockAchievements=1 in OpenCAGE.RuntimeUtils.ini turns it on from the start.");
		ImGui::SameLine();
		ImGui::PushTextWrapPos(0.0f);
		if (!g_patched)
			ImGui::TextDisabled("(waiting for the achievements to be loaded)");
		else if (g_blocked)
			ImGui::TextDisabled("Blocked: %u - last %s", g_blocked.load(), LastText().c_str());
		else
			ImGui::TextDisabled("Nothing blocked yet.");
		ImGui::PopTextWrapPos();

		uint32_t stats = 0;
		const std::vector<Achievement> list = ReadList(stats);
		if (list.empty())
		{
			ImGui::TextDisabled("The achievements are not up yet.");
			return;
		}
		int unlocked = 0;
		for (const Achievement& achievement : list)
			unlocked += achievement.state == kStateYes ? 1 : 0;
		// The counts in the header's own label (its id stays the same as they change)
		char header[96];
		snprintf(header, sizeof(header), "The game's achievements (%d of %u unlocked, %u stats)###achievements", unlocked, static_cast<unsigned>(list.size()), stats);
		if (!ImGui::CollapsingHeader(header))
			return;
		ImGui::SetNextItemWidth(200.0f * DEBUG_MENU::Scale());
		ImGui::InputTextWithHint("##achievementfilter", "Filter", g_filter, sizeof(g_filter));
		std::string filter = g_filter;
		for (char& c : filter)
			c = static_cast<char>(tolower(static_cast<unsigned char>(c)));
		if (ImGui::BeginChild("##achievements", ImVec2(0.0f, 200.0f * DEBUG_MENU::Scale()), ImGuiChildFlags_Borders))
		{
			for (const Achievement& achievement : list)
			{
				std::string name = achievement.name;
				for (char& c : name)
					c = static_cast<char>(tolower(static_cast<unsigned char>(c)));
				if (!filter.empty() && name.find(filter) == std::string::npos)
					continue;
				if (achievement.state == kStateYes)
					ImGui::TextColored(ImVec4(0.45f, 0.9f, 0.45f, 1.0f), "%s", achievement.name);
				else
					ImGui::TextUnformatted(achievement.name);
				ImGui::SameLine();
				if (achievement.limit > 1)
					ImGui::TextDisabled("%s (at %u)", StateName(achievement.state), achievement.limit);
				else
					ImGui::TextDisabled("%s", StateName(achievement.state));
			}
		}
		ImGui::EndChild();
	}

	void AttachHooks(bool attach)
	{
		if (attach)
		{
			g_block = Config::IniInt("BlockAchievements", 0) != 0;
			RegisterOnce();
			g_attached = true;
		}
		else
		{
			g_attached = false;
			Unpatch();
		}
	}

	const bool g_sectionAdded = DEBUG_MENU::AddTab("Saves", 76, DrawSection, "Game");
	const bool g_overlayAdded = DEBUG_MENU::AddOverlay(6, Apply);
	const bool g_hooksAdded = DEBUG_MENU::AddHooks(AttachHooks);
}

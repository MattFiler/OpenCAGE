#include "AI_STATE.h"
#include "DEBUG_MENU.h"

#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <memory>
#include <mutex>
#include <sstream>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <imgui.h>

/*
	The alien's learning, setup and director numbers, and drawing it to a point: a section of the Characters group's Alien
	tab (after wave 1's menace, ambush and vent views).

	- Learning: the game keeps which of the alien's tricks it has unlocked (threat aware, closing in through the vents, kill
	  traps, flanking, flanking from threat aware, ambushes, searching lockers, searching under things), what has been asked
	  for, and the counters that unlock them (how many times it was driven off with fire, has searched for the player, the
	  player hid from it, hid from it in a deep crouch), each trick unlocking when its counter reaches its number (lower
	  numbers above Novice; on Nightmare every trick is on). Shown with what the behaviour tree's own check answers for each;
	  each trick can be given or taken away and one of the game's four stages set (with the game's own call). The game keeps all of
	  this in memory only - nothing of it goes into a save, and loading a save leaves it as it is. Level scripts set a stage
	  at points in the campaign, and the alien's first spawn in one of the first three missions starts it over; past those
	  it only notes the mission. So a change here lasts until a script next sets a stage, or until the game is closed (no
	  need for saving to be off).
	- Setup: the alien setup in use, with a list of the level's setups to switch to (the game's own call, as the level
	  scripts' alien setup entity makes it), and the numbers the game's director works the setup out to at the difficulty
	  played (how fast the menace gauge fills, how many menaces, the sweep box, sweep and vent timings, kill trap time).
	- Draw the alien to a point: every alien hears the player moving at a point - where the crosshair hits (World > Inspect),
	  where the camera is, or the player - forced past its suspicious or its alerted threshold.

	Changes run on the AI thread. Addresses and layouts are the Steam retail AI.exe's (see GameAddresses.h for the GOG and
	Epic Games Store builds).
*/
namespace
{
	constexpr uintptr_t kGameGlobals = 0x012F0C88;
	constexpr uint32_t kCharactersValid = 0x04;
	// In the globals (pointers): the alien setups, the difficulty and the alien's learning
	constexpr uint32_t kGlobalsSetups = 0x84;
	constexpr uint32_t kGlobalsDifficulty = 0x88;
	constexpr uint32_t kGlobalsLearning = 0xA8;
	// The difficulty: the one played and the lowest played this campaign
	constexpr uint32_t kDifficultyValue = 0x28;
	constexpr uint32_t kDifficultyLowest = 0x2C;
	constexpr int kNovice = 4;
	constexpr int kNightmare = 3;
	// The setups: their numbers (a list: the entries at +8, how many at +0xC) and their names (a list of 64-character names:
	// the names at +0x18, how many at +0x1C)
	constexpr uint32_t kSetupsCount = 0x0C;
	constexpr uint32_t kSetupNames = 0x18;
	constexpr uint32_t kSetupNameCount = 0x1C;
	constexpr uint32_t kSetupNameSize = 0x40;
	// The alien's learning: the tricks asked for and those it has (bit masks), its four counters and the furthest campaign
	// mission the alien has spawned in (0: none). (After them the structure has room for a checkpoint and how many times it was loaded, but the
	// shipped game never sets them: the count is whatever the memory held.)
	constexpr uint32_t kLearningRequested = 0x00;
	constexpr uint32_t kLearningAbilities = 0x04;
	constexpr uint32_t kLearningCounters = 0x08;
	constexpr uint32_t kLearningMission = 0x18;
	// A character's fields: its unique id, its script entity (two ids), where it is, its alien setup and how it sees and
	// hears what is posted to it
	constexpr uint32_t kCharacterId = 0x50;
	constexpr uint32_t kCharacterHandle = 0x54;
	constexpr uint32_t kCharacterSetup = 0x42A4;
	constexpr uint32_t kCharacterEvents = 0x3BA8;
	// The game's clock: the time now and this frame's step (doubles)
	constexpr uintptr_t kGameTime = 0x012410A8;
	constexpr uint32_t kTimeStep = 0x18;
	// The scripted fake-sense message: "an enemy was heard moving", the enemy fully seen, past the threshold given
	constexpr int kMessageFakeMovementSound = 11;
	constexpr int kThresholdLower = 1;
	constexpr int kThresholdActivated = 2;
	constexpr size_t kMessageSize = 0x40;

	constexpr int kAlienClass = 1;

	// The game's functions:
	// - whether the alien may use a trick: the behaviour tree's own check (a pure read)
	typedef bool(__thiscall* t_has_ability)(const void* learning, const void* alien, uint32_t mask);
	const auto has_ability = reinterpret_cast<t_has_ability>(DEVTOOLS_RELATIVE_ADDRESS(0x00011770));
	// - the four stages: what is asked for and what it has both set to the stage's tricks, then the counters
	//   brought up to them
	typedef void(__thiscall* t_set_abilities)(void* learning, uint32_t stage);
	const auto set_abilities = reinterpret_cast<t_set_abilities>(DEVTOOLS_RELATIVE_ADDRESS(0x00011BC0));
	// - an alien's setup changed, as the level scripts' alien setup entity changes it (telling the alien)
	typedef void(__thiscall* t_set_alien_config)(void* alien, const uint32_t* setup, bool notify);
	const auto set_alien_config = reinterpret_cast<t_set_alien_config>(DEVTOOLS_RELATIVE_ADDRESS(0x00393E70));
	// - the director's numbers for a setup at the difficulty played (each a pure read of the setup and the difficulty's)
	typedef float(__thiscall* t_setup_number)(const void* setups, uint32_t setup);
	typedef unsigned(__thiscall* t_setup_count)(const void* setups, uint32_t setup);
	const auto max_menaces = reinterpret_cast<t_setup_count>(DEVTOOLS_RELATIVE_ADDRESS(0x002E2D50));
	struct Number
	{
		const char* key;
		const char* label;
		uintptr_t steam;
		const char* unit;
	};
	const Number kNumbers[] = {
		{ "gauge_seconds_to_fill", "Menace gauge: seconds to fill", 0x002E2DC0, "s" },
		{ "menace_deemed_time", "A menace counts after", 0x002E2D00, "s" },
		{ "sweep_box_half_length", "Sweep box: half its length", 0x002E2E10, "m" },
		{ "sweep_box_half_width", "Sweep box: half its width", 0x002E2EF0, "m" },
		{ "sweep_increase_duration", "Sweep: time to widen", 0x002E2CB0, "s" },
		{ "sweep_decrease_duration", "Sweep: time to narrow", 0x002E2C60, "s" },
		{ "vent_attract_time_min", "Drawn to vents after (least)", 0x002E2F40, "s" },
		{ "vent_attract_time_max", "Drawn to vents after (most)", 0x002E2F90, "s" },
		{ "backstage_role_timeout_min", "Backstage sweep: time on it (least)", 0x002E2FE0, "s" },
		{ "backstage_role_timeout_max", "Backstage sweep: time on it (most)", 0x002E3030, "s" },
		{ "backstage_ambush_timeout", "Backstage sweep: ambush gives up after", 0x002E3080, "s" },
		{ "backstage_min_distance", "Backstage sweep: least distance", 0x002E30D0, "m" },
		{ "backstage_killtrap_time", "Backstage sweep: kill trap time", 0x002E3120, "s" },
	};
	constexpr int kNumberCount = sizeof(kNumbers) / sizeof(kNumbers[0]);
	// - the sensed message made and posted, as the level scripts' fake-sense entity does
	typedef void(__thiscall* t_make_sensed)(void* message, int type, uint32_t entity, uint32_t composite, const float* position, float seenFraction, bool throughGlass, int threshold, float time, float step);
	const auto make_sensed = reinterpret_cast<t_make_sensed>(DEVTOOLS_RELATIVE_ADDRESS(0x000AB520));
	typedef void(__thiscall* t_post_sensed)(void* events, const void* message, bool now);
	const auto post_sensed = reinterpret_cast<t_post_sensed>(DEVTOOLS_RELATIVE_ADDRESS(0x000E1C20));

	// The tricks, with the counter that unlocks each and the number it unlocks at (above Novice, and on Novice); a counter of
	// -1: none (given only by a stage)
	struct Ability
	{
		uint32_t mask;
		const char* key;
		const char* label;
		int counter;
		int threshold, noviceThreshold;
	};
	const Ability kAbilities[] = {
		{ 0x002, "threat_aware", "Threat aware", 0, 1, 1 },
		{ 0x004, "close_via_backstage", "Closes in through the vents", 0, 4, 7 },
		{ 0x008, "killtrap", "Kill traps", 0, 6, 10 },
		{ 0x010, "flank", "Flanks", 0, 8, 12 },
		{ 0x020, "flank_from_threat_aware", "Flanks from threat aware", 0, 11, 16 },
		{ 0x040, "ambush", "Ambushes", -1, 0, 0 },
		{ 0x080, "search_lockers", "Searches lockers", 2, 5, 10 },
		{ 0x100, "search_under", "Searches under things", 3, 4, 4 },
	};
	constexpr int kAbilityCount = sizeof(kAbilities) / sizeof(kAbilities[0]);
	const char* const kCounterNames[] = { "Driven off with fire", "Searched for the player", "The player hid from it", "The player hid in a deep crouch" };

	// The four stages of the alien's learning
	struct Stage
	{
		uint32_t mask;
		const char* key;
		const char* label;
	};
	const Stage kStages[] = {
		{ 0x000, "naive", "Naive" },
		{ 0x002, "threat_aware", "Threat aware" },
		{ 0x18E, "getting_sneaky", "Getting sneaky" },
		{ 0x1DE, "really_sneaky", "Really sneaky" },
	};

	const char* DifficultyName(int value)
	{
		static const char* const kNames[] = { "Easy", "Normal", "Hard", "Nightmare", "Novice" };
		return value >= 0 && value < 5 ? kNames[value] : "?";
	}

	// ---- Reading (render thread, guarded) ----

	const uint8_t* GlobalsField(uint32_t offset)
	{
		__try
		{
			const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kGameGlobals);
			if (!at || !*reinterpret_cast<const uint8_t*>(at + kCharactersValid))
				return nullptr;
			const uint8_t* globals = *reinterpret_cast<const uint8_t* const*>(at);
			return globals ? *reinterpret_cast<const uint8_t* const*>(globals + offset) : nullptr;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	struct Learning
	{
		bool have = false;
		uint32_t requested = 0, abilities = 0;
		int counters[4] = {};
		uint32_t mission = 0;
		int difficulty = -1, lowest = -1;
		bool effective[kAbilityCount] = {};
	};

	bool ReadLearning(const uint8_t* alien, Learning& out)
	{
		const uint8_t* learning = GlobalsField(kGlobalsLearning);
		const uint8_t* difficulty = GlobalsField(kGlobalsDifficulty);
		__try
		{
			if (!learning)
				return false;
			out.requested = *reinterpret_cast<const uint32_t*>(learning + kLearningRequested);
			out.abilities = *reinterpret_cast<const uint32_t*>(learning + kLearningAbilities);
			memcpy(out.counters, learning + kLearningCounters, sizeof(out.counters));
			out.mission = *reinterpret_cast<const uint32_t*>(learning + kLearningMission);
			if (difficulty)
			{
				out.difficulty = *reinterpret_cast<const int*>(difficulty + kDifficultyValue);
				out.lowest = *reinterpret_cast<const int*>(difficulty + kDifficultyLowest);
			}
			for (int i = 0; i < kAbilityCount; i++)
				out.effective[i] = has_ability && difficulty ? has_ability(learning, alien, kAbilities[i].mask) : (out.abilities & kAbilities[i].mask) != 0;
			out.have = true;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	struct Setups
	{
		int count = 0;
		char names[24][65] = {};
		int current = -1;            // the alien's
		bool haveNumbers = false;
		float numbers[kNumberCount] = {};
		int maxMenaces = -1;
	};

	bool ReadSetups(const uint8_t* alien, Setups& out)
	{
		const uint8_t* setups = GlobalsField(kGlobalsSetups);
		__try
		{
			if (!setups)
				return false;
			const uint32_t names = *reinterpret_cast<const uint32_t*>(setups + kSetupNameCount);
			const uint32_t configs = *reinterpret_cast<const uint32_t*>(setups + kSetupsCount);
			const uint8_t* list = *reinterpret_cast<const uint8_t* const*>(setups + kSetupNames);
			out.count = static_cast<int>((std::min)((std::min)(names, configs), 24u));
			for (int i = 0; list && i < out.count; i++)
			{
				memcpy(out.names[i], list + i * kSetupNameSize, 64);
				out.names[i][64] = '\0';
			}
			if (!alien)
				return true;
			const uint32_t setup = *reinterpret_cast<const uint32_t*>(alien + kCharacterSetup);
			if (setup >= configs)
				return true;
			out.current = static_cast<int>(setup);
			for (int i = 0; i < kNumberCount; i++)
			{
				const auto number = reinterpret_cast<t_setup_number>(DEVTOOLS_RELATIVE_ADDRESS(kNumbers[i].steam));
				out.numbers[i] = number ? number(setups, setup) : 0.0f;
			}
			if (max_menaces)
				out.maxMenaces = static_cast<int>(max_menaces(setups, setup));
			out.haveNumbers = true;
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// The alien shown: the selected one, or the first alive
	const DEBUG_MENU::Character* ShownAlien()
	{
		const DEBUG_MENU::Character* selected = DEBUG_MENU::Selected();
		if (selected && selected->characterClass == kAlienClass)
			return selected;
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
			if (character.characterClass == kAlienClass && !character.dead)
				return &character;
		return nullptr;
	}

	// ---- Changes (AI thread) ----

	// What the last change came to, kept for the section it was made in (and shown under that section)
	enum Section { kLearningSection, kSetupSection, kDrawSection, kSections };
	std::mutex g_statusMutex;
	std::string g_status[kSections];

	void SetStatus(Section section, const std::string& text)
	{
		{
			std::lock_guard<std::mutex> lock(g_statusMutex);
			g_status[section] = text;
		}
		DevTools::Log("Debug menu: %s", text.c_str());
	}

	// Render thread: the section's last result, under it
	void DrawStatus(Section section)
	{
		std::lock_guard<std::mutex> lock(g_statusMutex);
		if (g_status[section].empty())
			return;
		ImGui::PushTextWrapPos(0.0f);
		ImGui::TextDisabled("Last: %s", g_status[section].c_str());
		ImGui::PopTextWrapPos();
	}

	// (The learning is changed whatever the save switch says: the game never writes it to a save)
	uint8_t* LearningForChange()
	{
		return const_cast<uint8_t*>(GlobalsField(kGlobalsLearning));
	}

	bool SetAbility(uint32_t mask, bool on)
	{
		uint8_t* learning = LearningForChange();
		__try
		{
			if (!learning)
				return false;
			uint32_t& abilities = *reinterpret_cast<uint32_t*>(learning + kLearningAbilities);
			abilities = on ? (abilities | mask) : (abilities & ~mask);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool SetStage(uint32_t stage)
	{
		uint8_t* learning = LearningForChange();
		__try
		{
			if (!learning || !set_abilities)
				return false;
			set_abilities(learning, stage);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	bool SetSetup(uint8_t* alien, uint32_t setup)
	{
		__try
		{
			if (!set_alien_config)
				return false;
			set_alien_config(alien, &setup, true);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// The characters a draw reaches, found again on the AI thread by their unique ids
	struct Characters
	{
		uint8_t* player = nullptr;
		uint8_t* aliens[8] = {};
		int alienCount = 0;
	};

	// Every alien hears the player moving at the point (the player's own entity sent as what moved, so it comes looking for
	// the player there), past the threshold given
	bool PostToAliens(const Characters& found, const float point[3], int threshold, int& heard)
	{
		alignas(16) uint8_t message[kMessageSize] = {};
		heard = 0;
		if (!make_sensed || !post_sensed || !DEVTOOLS_RELATIVE_ADDRESS(kGameTime))
			return false;
		__try
		{
			const uint8_t* clock = reinterpret_cast<const uint8_t*>(DEVTOOLS_RELATIVE_ADDRESS(kGameTime));
			const float time = static_cast<float>(*reinterpret_cast<const double*>(clock));
			const float step = static_cast<float>(*reinterpret_cast<const double*>(clock + kTimeStep));
			const uint32_t* handle = reinterpret_cast<const uint32_t*>(found.player + kCharacterHandle);
			for (int i = 0; i < found.alienCount; i++)
			{
				make_sensed(message, kMessageFakeMovementSound, handle[0], handle[1], point, 1.0f, false, threshold, time, step);
				post_sensed(found.aliens[i] + kCharacterEvents, message, false);
				heard++;
			}
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// ---- Asking ----

	struct Point
	{
		bool have = false;
		float position[3] = {};
		std::string from;
	};

	// Render thread: the point named ("pick", "camera", "player", or "x,y,z")
	Point ResolvePoint(const std::string& what)
	{
		Point point;
		point.from = what;
		if (what == "pick" || what == "crosshair")
		{
			DEBUG_MENU::PickResult pick;
			if (DEBUG_MENU::Pick(pick))
			{
				memcpy(point.position, pick.position, sizeof(point.position));
				point.have = true;
			}
			point.from = "the crosshair point";
		}
		else if (what == "camera")
		{
			point.have = DEBUG_MENU::CameraPosition(point.position);
			point.from = "the camera";
		}
		else if (what == "player")
		{
			for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
				if (character.player)
				{
					memcpy(point.position, character.position, sizeof(point.position));
					point.have = true;
					break;
				}
			point.from = "the player";
		}
		else
		{
			float x = 0.0f, y = 0.0f, z = 0.0f;
			if (sscanf_s(what.c_str(), "%f,%f,%f", &x, &y, &z) == 3)
			{
				point.position[0] = x;
				point.position[1] = y;
				point.position[2] = z;
				point.have = true;
			}
		}
		return point;
	}

	// Render thread: the unique ids of the player and the aliens alive now
	void Ids(uint32_t& player, std::vector<uint32_t>& aliens)
	{
		player = 0;
		aliens.clear();
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
		{
			if (character.player && !player)
				player = character.id;
			else if (character.characterClass == kAlienClass && !character.dead)
				aliens.push_back(character.id);
		}
	}

	// AI thread: the characters found again by id, then the message posted to each alien
	std::string DrawTo(uint32_t playerId, const std::vector<uint32_t>& alienIds, const float point[3], int threshold, bool& ok)
	{
		ok = false;
		Characters found;
		found.player = AI_STATE::FindCharacter(playerId);
		for (uint32_t id : alienIds)
			if (found.alienCount < 8)
				if (uint8_t* alien = AI_STATE::FindCharacter(id))
					found.aliens[found.alienCount++] = alien;
		if (!found.player)
			return "no player";
		if (!found.alienCount)
			return "no alien to draw";
		int heard = 0;
		if (!PostToAliens(found, point, threshold, heard))
			return "the message could not be posted";
		ok = true;
		char text[160];
		snprintf(text, sizeof(text), "%d alien(s) heard the player at %.2f, %.2f, %.2f (%s)", heard, point[0], point[1], point[2], threshold == kThresholdLower ? "suspicious" : "alerted");
		return text;
	}

	// ---- The section ----

	int g_threshold = kThresholdLower;   // render thread: the threshold the buttons use
	int g_setupChoice = -1;

	void AskDraw(const std::string& what)
	{
		const Point point = ResolvePoint(what);
		if (!point.have)
		{
			SetStatus(kDrawSection, what == "pick" ? "nothing under the crosshair: turn on the crosshair in World > Inspect, or the game is paused" : "no point to draw the alien to");
			return;
		}
		uint32_t player = 0;
		std::vector<uint32_t> aliens;
		Ids(player, aliens);
		const int threshold = g_threshold;
		const std::string from = point.from;
		float position[3];
		memcpy(position, point.position, sizeof(position));
		DEBUG_MENU::OnAiThread([player, aliens, position, threshold, from]()
		{
			bool ok = false;
			SetStatus(kDrawSection, "draw the alien to " + from + ": " + DrawTo(player, aliens, position, threshold, ok));
		});
	}

	void DrawLearning(const DEBUG_MENU::Character* alien)
	{
		ImGui::SeparatorText("What the alien has learnt");
		Learning learning;
		if (!ReadLearning(alien ? alien->object : nullptr, learning))
		{
			ImGui::TextDisabled("Not readable now (no level running).");
			return;
		}
		if (learning.mission)
			ImGui::TextWrapped("Difficulty %s (lowest played: %s); the furthest mission the alien has spawned in this run: %u", DifficultyName(learning.difficulty), DifficultyName(learning.lowest), learning.mission);
		else
			ImGui::TextWrapped("Difficulty %s (lowest played: %s); the alien hasn't spawned in a campaign mission yet this run", DifficultyName(learning.difficulty), DifficultyName(learning.lowest));
		if (learning.difficulty == kNightmare)
			ImGui::TextDisabled("On Nightmare every trick is on, whatever is unlocked.");
		const bool novice = learning.difficulty == kNovice;
		for (int i = 0; i < 4; i++)
			ImGui::BulletText("%s: %d", kCounterNames[i], learning.counters[i]);
		ImGui::PushTextWrapPos(0.0f);
		ImGui::TextDisabled("This is the game's, not one alien's: the tricks and stages below can be changed with no alien in the level, and "
			"every alien uses them. Kept in memory only, never in a save. Level scripts set a stage at points in the campaign (and the alien's "
			"first spawn in one of the first three missions starts it over), so a change here lasts until a script next sets one, or until the game is closed.");
		ImGui::PopTextWrapPos();
		if (!ImGui::BeginTable("abilities", 4, ImGuiTableFlags_RowBg | ImGuiTableFlags_BordersInnerV | ImGuiTableFlags_SizingStretchProp))
			return;
		ImGui::TableSetupColumn("Trick");
		ImGui::TableSetupColumn("Unlocked");
		ImGui::TableSetupColumn("Its tree's check");
		ImGui::TableSetupColumn("Unlocks at");
		ImGui::TableHeadersRow();
		for (int i = 0; i < kAbilityCount; i++)
		{
			const Ability& ability = kAbilities[i];
			ImGui::TableNextRow();
			// (the name lined up with the checkbox's row, as the columns after the checkbox already are)
			ImGui::TableSetColumnIndex(0);
			ImGui::AlignTextToFramePadding();
			ImGui::TextUnformatted(ability.label);
			ImGui::TableSetColumnIndex(1);
			bool on = (learning.abilities & ability.mask) != 0;
			char id[48];
			snprintf(id, sizeof(id), "##ability%d", i);
			if (ImGui::Checkbox(id, &on))
			{
				const uint32_t mask = ability.mask;
				const std::string label = ability.label;
				DEBUG_MENU::OnAiThread([mask, on, label]()
				{
					SetStatus(kLearningSection, label + (on ? " given" : " taken away") + (SetAbility(mask, on) ? "" : ": not done"));
				});
			}
			if (learning.requested & ability.mask)
			{
				ImGui::SameLine();
				ImGui::TextDisabled("(asked for)");
			}
			ImGui::TableSetColumnIndex(2);
			ImGui::TextUnformatted(learning.effective[i] ? "yes" : "no");
			ImGui::TableSetColumnIndex(3);
			if (ability.counter < 0)
				ImGui::TextDisabled("a stage only");
			else
			{
				const int threshold = novice ? ability.noviceThreshold : ability.threshold;
				const int counter = learning.counters[ability.counter];
				ImGui::Text("%s %d of %d", kCounterNames[ability.counter], counter, threshold);
				if (counter >= threshold && !(learning.abilities & ability.mask))
				{
					ImGui::SameLine();
					ImGui::TextDisabled("(comes back when it next counts)");
				}
			}
		}
		ImGui::EndTable();
		ImGui::TextUnformatted("Set a stage:");
		ImGui::BeginDisabled(!set_abilities);
		for (const Stage& stage : kStages)
		{
			ImGui::SameLine();
			if (ImGui::SmallButton(stage.label))
			{
				const uint32_t mask = stage.mask;
				const std::string label = stage.label;
				DEBUG_MENU::OnAiThread([mask, label]()
				{
					SetStatus(kLearningSection, std::string("stage ") + label + (SetStage(mask) ? " set" : ": not done"));
				});
			}
		}
		ImGui::EndDisabled();
		DrawStatus(kLearningSection);
	}

	void DrawSetup(const DEBUG_MENU::Character* alien)
	{
		// (with no alien there is nothing to show: the tab says so at its top)
		if (!alien)
			return;
		ImGui::SeparatorText("Setup and the director's numbers");
		Setups setups;
		if (!ReadSetups(alien->object, setups))
		{
			ImGui::TextDisabled("The setups could not be read.");
			return;
		}
		const char* current = setups.current >= 0 && setups.current < setups.count ? setups.names[setups.current] : "?";
		ImGui::SetNextItemWidth(260.0f * DEBUG_MENU::Scale());
		ImGui::BeginDisabled(!set_alien_config);
		if (ImGui::BeginCombo("Setup", current))
		{
			for (int i = 0; i < setups.count; i++)
				if (ImGui::Selectable(setups.names[i], i == setups.current))
				{
					const uint32_t id = alien->id;
					const uint32_t setup = static_cast<uint32_t>(i);
					const std::string name = setups.names[i];
					DEBUG_MENU::OnAiThread([id, setup, name]()
					{
						uint8_t* character = AI_STATE::FindCharacter(id);
						SetStatus(kSetupSection, "alien setup " + name + (character && SetSetup(character, setup) ? " set" : ": not done (the alien is gone)"));
					});
				}
			ImGui::EndCombo();
		}
		ImGui::EndDisabled();
		ImGui::SameLine();
		ImGui::TextDisabled("(as the level scripts' alien setup entity sets it)");
		DrawStatus(kSetupSection);
		if (!setups.haveNumbers)
			return;
		if (ImGui::BeginTable("numbers", 2, ImGuiTableFlags_RowBg | ImGuiTableFlags_SizingStretchProp))
		{
			for (int i = 0; i < kNumberCount; i++)
			{
				ImGui::TableNextRow();
				ImGui::TableSetColumnIndex(0);
				ImGui::TextUnformatted(kNumbers[i].label);
				ImGui::TableSetColumnIndex(1);
				ImGui::Text("%.2f %s", setups.numbers[i], kNumbers[i].unit);
			}
			ImGui::TableNextRow();
			ImGui::TableSetColumnIndex(0);
			ImGui::TextUnformatted("Most menaces before it withdraws");
			ImGui::TableSetColumnIndex(1);
			ImGui::Text("%d", setups.maxMenaces);
			ImGui::EndTable();
		}
	}

	void DrawDraw(const DEBUG_MENU::Character* alien)
	{
		ImGui::SeparatorText("Draw the alien to a point");
		const bool available = make_sensed && post_sensed;
		// No alien: every control off (the tab says so at its top)
		ImGui::BeginDisabled(!available || !alien);
		ImGui::RadioButton("Suspicious", &g_threshold, kThresholdLower);
		ImGui::SameLine();
		ImGui::RadioButton("Alerted", &g_threshold, kThresholdActivated);
		const char* why = !alien ? "Needs an alien in the level." : !available ? "Not available on this release of the game." : nullptr;
		const char* const kButtons[][2] = { { "To the crosshair point", "pick" }, { "To the camera", "camera" }, { "To the player", "player" } };
		for (int i = 0; i < 3; i++)
		{
			if (i)
				ImGui::SameLine();
			if (ImGui::Button(kButtons[i][0]))
				AskDraw(kButtons[i][1]);
			if (why && ImGui::IsItemHovered(ImGuiHoveredFlags_AllowWhenDisabled))
				ImGui::SetTooltip("%s", why);
		}
		ImGui::EndDisabled();
		ImGui::PushTextWrapPos(0.0f);
		ImGui::TextDisabled("Every alien hears the player moving there: it comes to search (suspicious) or to hunt (alerted).");
		ImGui::PopTextWrapPos();
		DrawStatus(kDrawSection);
	}

	void DrawAlienSection()
	{
		const DEBUG_MENU::Character* alien = ShownAlien();
		DrawLearning(alien);
		DrawSetup(alien);
		DrawDraw(alien);
	}

	// ---- Commands ----

	DEBUG_MENU::CommandResult Result(bool ok, const std::string& message)
	{
		DEBUG_MENU::CommandResult result;
		result.ok = ok;
		result.message = message;
		return result;
	}

	std::vector<std::string> Words(const std::string& text)
	{
		std::vector<std::string> words;
		std::istringstream stream(text);
		std::string word;
		while (stream >> word)
		{
			for (char& c : word)
				c = static_cast<char>(tolower(static_cast<unsigned char>(c)));
			words.push_back(word);
		}
		return words;
	}

	bool RegisterCommands()
	{
		DEBUG_MENU::AddCommand("ai.alien.learning", "the alien's tricks (key=unlocked,check), counters, difficulty", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]() -> DEBUG_MENU::CommandResult
			{
				const DEBUG_MENU::Character* alien = ShownAlien();
				Learning learning;
				if (!ReadLearning(alien ? alien->object : nullptr, learning))
					return Result(false, "not readable now (no level running)");
				std::ostringstream out;
				out << "difficulty=" << DifficultyName(learning.difficulty) << "\nlowest=" << DifficultyName(learning.lowest) << "\n";
				char masks[64];
				snprintf(masks, sizeof(masks), "abilities=0x%03X\nrequested=0x%03X\n", learning.abilities, learning.requested);
				out << masks;
				for (int i = 0; i < 4; i++)
					out << "counter." << i << "=" << learning.counters[i] << " (" << kCounterNames[i] << ")\n";
				out << "mission=" << (learning.mission ? std::to_string(learning.mission) : std::string("none")) << "\nkept=memory only (never saved)\n";
				for (int i = 0; i < kAbilityCount; i++)
					out << kAbilities[i].key << "=" << ((learning.abilities & kAbilities[i].mask) ? 1 : 0) << "," << (learning.effective[i] ? 1 : 0) << "\n";
				return Result(true, out.str());
			});
		});
		DEBUG_MENU::AddCommand("ai.alien.ability", "<key> on|off - gives or takes away a trick (in memory only: the game never saves the learning)", [](const std::string& argument)
		{
			const std::vector<std::string> words = Words(argument);
			if (words.size() < 2)
				return Result(false, "give a trick (ai.alien.learning lists them) and on|off");
			const Ability* ability = nullptr;
			for (const Ability& each : kAbilities)
				if (words[0] == each.key)
					ability = &each;
			if (!ability)
				return Result(false, "no trick called " + words[0]);
			const bool on = words[1] == "on" || words[1] == "1";
			const uint32_t mask = ability->mask;
			const std::string label = ability->label;
			return DEBUG_MENU::AnswerOnAiThread([mask, on, label]() -> DEBUG_MENU::CommandResult
			{
				const bool done = SetAbility(mask, on);
				return Result(done, label + (on ? " given" : " taken away") + (done ? "" : ": not done (no level)"));
			});
		});
		DEBUG_MENU::AddCommand("ai.alien.stage", "naive|threat_aware|getting_sneaky|really_sneaky - one of the alien's four learning stages (in memory only: the game never saves the learning)", [](const std::string& argument)
		{
			const std::vector<std::string> words = Words(argument);
			if (words.empty())
				return Result(false, "give a stage");
			for (const Stage& stage : kStages)
				if (words[0] == stage.key)
				{
					const uint32_t mask = stage.mask;
					const std::string label = stage.label;
					return DEBUG_MENU::AnswerOnAiThread([mask, label]() -> DEBUG_MENU::CommandResult
					{
						const bool done = SetStage(mask);
						return Result(done, std::string("stage ") + label + (done ? " set" : ": not done"));
					});
				}
			return Result(false, "no stage called " + words[0]);
		});
		DEBUG_MENU::AddCommand("ai.alien.list_setups", "the level's alien setups, one a line (index name), * the alien's", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]() -> DEBUG_MENU::CommandResult
			{
				const DEBUG_MENU::Character* alien = ShownAlien();
				Setups setups;
				if (!ReadSetups(alien ? alien->object : nullptr, setups))
					return Result(false, "the setups could not be read (no level running)");
				std::string text;
				for (int i = 0; i < setups.count; i++)
					text += std::to_string(i) + " " + setups.names[i] + (i == setups.current ? " *" : "") + "\n";
				return Result(true, text);
			});
		});
		DEBUG_MENU::AddCommand("ai.alien.setup", "<index or name> - switches the alien's setup (AI thread)", [](const std::string& argument)
		{
			std::string wanted = argument;
			while (!wanted.empty() && wanted.back() == ' ')
				wanted.pop_back();
			struct Choice { uint32_t id = 0; int setup = -1; std::string name; };
			std::shared_ptr<Choice> choice = std::make_shared<Choice>();
			const DEBUG_MENU::CommandResult read = DEBUG_MENU::AnswerOnRenderThread([wanted, choice]() -> DEBUG_MENU::CommandResult
			{
				const DEBUG_MENU::Character* alien = ShownAlien();
				Setups setups;
				if (!alien || !ReadSetups(alien->object, setups))
					return Result(false, "no alien, or its setups could not be read");
				choice->id = alien->id;
				char* end = nullptr;
				const long index = strtol(wanted.c_str(), &end, 10);
				if (!wanted.empty() && end && *end == '\0' && index >= 0 && index < setups.count)
					choice->setup = static_cast<int>(index);
				for (int i = 0; i < setups.count && choice->setup < 0; i++)
					if (_stricmp(setups.names[i], wanted.c_str()) == 0)
						choice->setup = i;
				if (choice->setup < 0)
					return Result(false, "no setup " + wanted + " (ai.alien.list_setups lists them)");
				choice->name = setups.names[choice->setup];
				return Result(true, "");
			});
			if (!read.ok)
				return read;
			const uint32_t id = choice->id;
			const uint32_t setup = static_cast<uint32_t>(choice->setup);
			const std::string name = choice->name;
			return DEBUG_MENU::AnswerOnAiThread([id, setup, name]() -> DEBUG_MENU::CommandResult
			{
				uint8_t* alien = AI_STATE::FindCharacter(id);
				if (!alien)
					return Result(false, "the alien is gone");
				const bool done = SetSetup(alien, setup);
				return Result(done, "alien setup " + name + (done ? " set" : ": not done"));
			});
		});
		DEBUG_MENU::AddCommand("ai.alien.director", "the director's numbers for the alien's setup at the difficulty played, key=value", [](const std::string&)
		{
			return DEBUG_MENU::AnswerOnRenderThread([]() -> DEBUG_MENU::CommandResult
			{
				const DEBUG_MENU::Character* alien = ShownAlien();
				Setups setups;
				if (!alien || !ReadSetups(alien->object, setups) || !setups.haveNumbers)
					return Result(false, "no alien, or its setup could not be read");
				std::ostringstream out;
				// (only the first 24 names are kept: a setup past them has its number)
				if (setups.current < setups.count)
					out << "setup=" << setups.names[setups.current] << "\n";
				else
					out << "setup=" << setups.current << "\n";
				for (int i = 0; i < kNumberCount; i++)
					out << kNumbers[i].key << "=" << setups.numbers[i] << "\n";
				out << "max_menaces=" << setups.maxMenaces << "\n";
				return Result(true, out.str());
			});
		});
		DEBUG_MENU::AddCommand("ai.alien.draw_to", "pick|camera|player|x,y,z [suspicious|alerted] - every alien hears the player moving there", [](const std::string& argument)
		{
			const std::vector<std::string> words = Words(argument);
			if (words.empty())
				return Result(false, "give pick, camera, player or x,y,z");
			const int threshold = words.size() > 1 && (words[1] == "alerted" || words[1] == "activated") ? kThresholdActivated : kThresholdLower;
			struct Ask { Point point; uint32_t player = 0; std::vector<uint32_t> aliens; };
			std::shared_ptr<Ask> ask = std::make_shared<Ask>();
			const std::string what = words[0];
			const DEBUG_MENU::CommandResult read = DEBUG_MENU::AnswerOnRenderThread([what, ask]() -> DEBUG_MENU::CommandResult
			{
				ask->point = ResolvePoint(what);
				Ids(ask->player, ask->aliens);
				if (ask->aliens.empty())
					return Result(false, "no alien (none in this level, or not spawned yet)");
				if (!ask->point.have)
					return Result(false, what == "pick" ? "nothing under the crosshair: turn on the crosshair in World > Inspect (inspect.pick), or the game is paused" : "no such point");
				return Result(true, "");
			});
			if (!read.ok)
				return read;
			const uint32_t player = ask->player;
			const std::vector<uint32_t> aliens = ask->aliens;
			float position[3];
			memcpy(position, ask->point.position, sizeof(position));
			return DEBUG_MENU::AnswerOnAiThread([player, aliens, position, threshold]() -> DEBUG_MENU::CommandResult
			{
				bool ok = false;
				const std::string message = DrawTo(player, aliens, position, threshold, ok);
				return Result(ok, message);
			});
		});
		return true;
	}

	// Render thread, every frame: the commands named on the first frame, as the other views name theirs
	void NameCommands(ImDrawList* /*drawList*/)
	{
		static const bool registered = RegisterCommands();
		(void)registered;
	}

	const bool g_section = DEBUG_MENU::AddTab("Alien", 41, DrawAlienSection, "Characters");
	const bool g_overlay = DEBUG_MENU::AddOverlay(14, NameCommands);
}

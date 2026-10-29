#include "AI_COMBAT.h"
#include "AI_STATE.h"
#include "DEBUG_MENU.h"
#include "PLAYER_DEBUG.h"

#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <mutex>
#include <sstream>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <imgui.h>

/*
	Stench of evil, a section at the bottom of the Characters group's Player tab (its tick box is among the cheats at the
	top too): every enemy of the player within a radius of them (5 m unless set) dies, every frame while it is on. A super
	power the game still has the code for, which nothing in the shipped game turns on.

	Done here as the game's own code does it, on the AI thread every frame (where the game runs it), with the same tests
	and the same message:
	- the characters within the radius of the player (a straight line in 3D, so through walls and floors) whose alliance
	  is an enemy of the player's in the game's live alliance table (the level's scripts change it), whose body has
	  started, that aren't already dying or dead and aren't controlled by a player;
	- each has its whole "takes no damage" and "doesn't react to hits" flags cleared (every owner's bit, the level's
	  scripts' invincibility included), then the game's damage message that also forces a reaction is sent: from the
	  player, damage equal to the character's health, and a shot death from the player's direction as the reaction. The
	  game's damage handling does the rest: the player is noted as the attacker, the damage is taken (the difficulty may
	  shave it, and the next frame finishes the job), the death reaction is forced, and the death is a normal one - the
	  level's scripts hear of it and the kill is the player's.

	What is different: the alien is always left out (the game keeps it at 1 health whatever the damage, so it can't die of
	this, and hitting it anyway showed nothing in game); androids marked as showroom dummies (which the game never lets
	die) are left out; the radius is a value; "Spare the script-invincible" leaves characters whose "takes no damage" flags
	are set alone instead of clearing them; characters made invulnerable in Characters > Control are always spared;
	nothing is sent while the NPC damage choice there keeps a hit from killing outright (the death reaction would be forced
	on characters the hit left alive); and a character whose flags were cleared and is still alive when this is turned off
	gets them back.

	The game takes the damage after the message is handled, so a kill is counted when a character hit here is seen dying or
	dead in a later frame (within 10 s of its last hit, in the same level).

	A faint ring on the ground round the player shows the radius while it is on.

	Addresses and layouts are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).

	Named for OpenCAGE and testing: switches "player.stench_of_evil", "player.stench_spare_invincible" and
	"player.stench_ring"; value "player.stench_radius"; command "player.stench_status". A level change turns it off.
*/
namespace
{
	using namespace PLAYER_DEBUG;

	// In the game's globals: its damage messages (a pointer: the sender of the message used here at +0x60), and the
	// alliance table (a pointer: a word per alliance, bit n set when alliance n is its enemy)
	constexpr uint32_t kGlobalsDamage = 0x00;
	constexpr uint32_t kDamageWithReaction = 0x60;
	constexpr uint32_t kGlobalsAlliances = 0x34;
	constexpr int kAlliances = 9;

	// A character's fields: its debug name (64 characters), unique id, script entity (two ids), position, what it was
	// spawned as (a pointer: its class at +4), alliance, its "takes no damage" and "doesn't react to hits" flags (a byte
	// of owner bits each, side by side; the debug bit is the debug menu's own), its player data (a pointer: set at +0x48
	// while a player controls it), its health, its body's high-level state machine (whether it has started, its state
	// and the state it is going to: 9 is dead) and its dying machine's state (1 to 3: dying or dead), and its logic flags
	// (a byte each)
	constexpr uint32_t kCharacterName = 0x10;
	constexpr uint32_t kCharacterId = 0x50;
	constexpr uint32_t kCharacterHandle = 0x54;
	constexpr uint32_t kCharacterPosition = 0x70;
	constexpr uint32_t kCharacterSpawnedAs = 0x64;
	constexpr uint32_t kSpawnedAsClass = 0x04;
	constexpr uint32_t kCharacterAlliance = 0x2E4;
	constexpr uint32_t kNoDamageFlags = 0x2E8;
	constexpr uint8_t kDebugBit = 0x20;
	constexpr uint32_t kCharacterPlayable = 0x31C;
	constexpr uint32_t kPlayableControlled = 0x48;
	constexpr uint32_t kHealth = 0x33C;
	constexpr uint32_t kHighLevelStarted = 0x2CF4;
	constexpr uint32_t kHighLevelState = 0x2CF0;
	constexpr uint32_t kHighLevelPending = 0x2DB4;
	constexpr uint32_t kDyingState = 0x2F90;
	constexpr int kHighLevelDead = 9;
	constexpr uint32_t kCharacterFlags = 0x423C;
	constexpr int kFlagShowroomDummy = 0x17;
	constexpr int kAlienClass = 1;

	// The message: the attacker's and the victim's script entities (two ids each), the reaction forced on the victim, the
	// damage, and whether it is the level scripts' "die" command's (0: an ordinary hit)
	struct DamageMessage
	{
		uint32_t attacker[2];
		uint32_t victim[2];
		uint32_t reaction;
		uint16_t damage;
		uint8_t byCommand;
		uint8_t unused;
	};
	static_assert(sizeof(DamageMessage) == 0x18, "the game's message is 0x18 bytes");
	constexpr uint32_t kReactionShotDeath = 0x1A;

	// The sender's send (as the game sends it: the message, then false)
	typedef void(__thiscall* t_send_damage)(const void* sender, const DamageMessage* message, bool flag);
	const auto send_damage = reinterpret_cast<t_send_damage>(DEVTOOLS_RELATIVE_ADDRESS(0x00091C00));

	constexpr float kDefaultRadius = 5.0f;
	constexpr float kMinRadius = 1.0f;
	constexpr float kMaxRadius = 50.0f;
	constexpr int kMaxCharacters = 36;
	constexpr size_t kKilledKept = 8;
	constexpr ULONGLONG kHitKeptMs = 10000;

	// ---- The switches ----

	std::atomic<bool> g_on = false;
	std::atomic<bool> g_spareInvincible = false;
	std::atomic<bool> g_ring = true;
	std::atomic<float> g_radius = kDefaultRadius;
	std::atomic<bool> g_tickQueued = false;

	float Radius()
	{
		const float radius = g_radius;
		return std::isfinite(radius) ? (std::max)(kMinRadius, (std::min)(kMaxRadius, radius)) : kDefaultRadius;
	}

	// ---- What the section did last ----

	std::mutex g_statusMutex;
	std::string g_status;

	void SetStatus(const std::string& text)
	{
		{
			std::lock_guard<std::mutex> lock(g_statusMutex);
			g_status = text;
		}
		DevTools::Log("Debug menu: %s", text.c_str());
	}

	std::string Status()
	{
		std::lock_guard<std::mutex> lock(g_statusMutex);
		return g_status;
	}

	// ---- What each frame's pass found (written on the AI thread, read by the tab and the status command) ----

	enum class Verdict { Hit, NotEnemy, NotStarted, Dying, PlayerControlled, Alien, Dummy, Invulnerable, ScriptInvincible, KeptAlive, Failed };

	const char* VerdictText(Verdict verdict)
	{
		switch (verdict)
		{
		case Verdict::Hit: return "hit";
		case Verdict::NotEnemy: return "not an enemy of the player";
		case Verdict::NotStarted: return "not started yet";
		case Verdict::Dying: return "dying or dead";
		case Verdict::PlayerControlled: return "controlled by a player";
		case Verdict::Alien: return "left out: the alien";
		case Verdict::Dummy: return "left out: a showroom dummy";
		case Verdict::Invulnerable: return "spared: invulnerable (Control tab)";
		case Verdict::ScriptInvincible: return "spared: set to take no damage";
		case Verdict::KeptAlive: return "held: the NPC damage choice keeps a hit from killing";
		default: return "the message failed";
		}
	}

	struct InRange
	{
		uint32_t id = 0;
		std::string name;
		int characterClass = -1;
		int alliance = -1;
		float distance = 0.0f;
		Verdict verdict = Verdict::NotEnemy;
	};

	struct Pass
	{
		bool ran = false;
		std::string problem;     // why nothing could be done (no player...), else empty
		std::vector<InRange> inRange;  // the characters within the radius
	};

	std::mutex g_passMutex;
	Pass g_pass;
	std::atomic<uint32_t> g_kills = 0;
	std::vector<std::string> g_killed; // under g_passMutex: the latest last, at most kKilledKept

	// AI thread only: the characters counted as killed (each once), and the flags cleared on each (to give back to those
	// still alive when it is turned off)
	std::vector<uint32_t> g_countedIds;
	struct Cleared
	{
		uint32_t id;
		uint16_t flags;
	};
	std::vector<Cleared> g_cleared;
	std::atomic<bool> g_anyCleared = false;

	// The characters hit and not yet seen dying (AI thread only), each with when it was last hit and the level it was hit
	// in: counted as a kill once seen dying or dead, forgotten after kHitKeptMs or in another level. The render thread
	// counts the levels.
	struct Hit
	{
		uint32_t id;
		ULONGLONG at;
		uint32_t level;
	};
	std::vector<Hit> g_hits;
	std::atomic<bool> g_anyHits = false;
	std::atomic<uint32_t> g_level = 0;
	uint32_t g_countedLevel = 0; // AI thread: the level the counted ids are of

	// ---- Reads and the send, guarded ----

	struct Candidate
	{
		uint32_t id = 0;
		uint32_t handle[2] = {};
		float position[3] = {};
		int characterClass = -1;
		int alliance = -1;
		uint16_t noDamageFlags = 0;  // "takes no damage" in the low byte, "doesn't react to hits" in the high byte
		bool playerControlled = false;
		int health = 0;
		bool started = false;
		bool dying = false;
		bool dummy = false;
		char name[65] = {};
	};

	bool ReadCandidate(const uint8_t* character, Candidate& out)
	{
		__try
		{
			out.id = *reinterpret_cast<const uint32_t*>(character + kCharacterId);
			out.handle[0] = *reinterpret_cast<const uint32_t*>(character + kCharacterHandle);
			out.handle[1] = *reinterpret_cast<const uint32_t*>(character + kCharacterHandle + 4);
			for (int i = 0; i < 3; i++)
				out.position[i] = reinterpret_cast<const float*>(character + kCharacterPosition)[i];
			const uint8_t* spawnedAs = *reinterpret_cast<uint8_t* const*>(character + kCharacterSpawnedAs);
			out.characterClass = spawnedAs ? *reinterpret_cast<const int*>(spawnedAs + kSpawnedAsClass) : -1;
			out.alliance = *reinterpret_cast<const int*>(character + kCharacterAlliance);
			out.noDamageFlags = *reinterpret_cast<const uint16_t*>(character + kNoDamageFlags);
			const uint8_t* playable = *reinterpret_cast<uint8_t* const*>(character + kCharacterPlayable);
			out.playerControlled = playable && playable[kPlayableControlled];
			out.health = *reinterpret_cast<const uint16_t*>(character + kHealth);
			out.started = character[kHighLevelStarted] != 0;
			const int state = *reinterpret_cast<const int*>(character + kHighLevelState);
			const int pending = *reinterpret_cast<const int*>(character + kHighLevelPending);
			const int dying = *reinterpret_cast<const int*>(character + kDyingState);
			out.dying = (state == kHighLevelDead || pending == kHighLevelDead) && dying >= 1 && dying <= 3;
			out.dummy = character[kCharacterFlags + kFlagShowroomDummy] != 0;
			size_t i = 0;
			for (; i + 1 < sizeof(out.name) && character[kCharacterName + i]; i++)
				out.name[i] = static_cast<char>(character[kCharacterName + i]);
			out.name[i] = '\0';
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// The game's own pass: the flags cleared (unless spared), then the message sent through the sender
	bool Send(uint8_t* character, const void* sender, const DamageMessage& message, bool clearFlags)
	{
		__try
		{
			if (clearFlags)
				*reinterpret_cast<volatile uint16_t*>(character + kNoDamageFlags) = 0;
			send_damage(sender, &message, false);
			return true;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return false;
		}
	}

	// ---- The pass, on the AI thread ----

	void NoteKill(const Candidate& them)
	{
		if (std::find(g_countedIds.begin(), g_countedIds.end(), them.id) != g_countedIds.end())
			return;
		g_countedIds.push_back(them.id);
		if (g_countedIds.size() > 512)
			g_countedIds.erase(g_countedIds.begin());
		g_kills++;
		std::lock_guard<std::mutex> lock(g_passMutex);
		g_killed.push_back(std::string(them.name) + " (" + std::to_string(them.id) + ")");
		if (g_killed.size() > kKilledKept)
			g_killed.erase(g_killed.begin());
	}

	void NoteHit(const Candidate& them)
	{
		const ULONGLONG now = GetTickCount64();
		const uint32_t level = g_level;
		for (Hit& hit : g_hits)
			if (hit.id == them.id)
			{
				hit.at = now;
				hit.level = level;
				return;
			}
		g_hits.push_back({ them.id, now, level });
		g_anyHits = true;
	}

	// The characters hit that are now dying or dead counted as kills (AI thread, each frame while any is waiting: the game
	// takes the damage after the message, so the character the message killed is not dead yet when it is sent). Its body
	// may be among the dead by then, so it is found among all the game's characters.
	void SettleHits()
	{
		const ULONGLONG now = GetTickCount64();
		const uint32_t level = g_level;
		for (auto it = g_hits.begin(); it != g_hits.end();)
		{
			bool done = it->level != level || now - it->at > kHitKeptMs;
			if (!done)
			{
				Candidate them;
				const uint8_t* character = AI_STATE::FindCharacter(it->id);
				if (character && ReadCandidate(character, them) && (them.dying || them.health == 0))
				{
					NoteKill(them);
					done = true;
				}
			}
			it = done ? g_hits.erase(it) : it + 1;
		}
		g_anyHits = !g_hits.empty();
	}

	void NoteCleared(const Candidate& them)
	{
		if (!(them.noDamageFlags & ~kDebugBit))
			return;
		for (const Cleared& cleared : g_cleared)
			if (cleared.id == them.id)
				return;
		g_cleared.push_back({ them.id, them.noDamageFlags });
		g_anyCleared = true;
	}

	// Turned off: the flags given back to the characters still alive that had them cleared
	void GiveFlagsBack()
	{
		int given = 0;
		for (const Cleared& cleared : g_cleared)
		{
			uint8_t* character = AI_STATE::FindCharacter(cleared.id);
			uint16_t health = 0, flags = 0;
			if (!character || !Read(character + kHealth, health) || health == 0 || !Read(character + kNoDamageFlags, flags))
				continue;
			if (Write<uint16_t>(character + kNoDamageFlags, static_cast<uint16_t>(flags | cleared.flags)))
				given++;
		}
		g_cleared.clear();
		g_anyCleared = false;
		if (given)
			SetStatus("stench of evil off: the \"takes no damage\" flags given back to " + std::to_string(given) + " character(s) still alive");
	}

	void Store(Pass& pass)
	{
		std::lock_guard<std::mutex> lock(g_passMutex);
		g_pass = std::move(pass);
	}

	void RunPass()
	{
		Pass pass;
		pass.ran = true;
		uint8_t* player = Player();
		Candidate me;
		if (!player || !ReadCandidate(player, me) || me.health == 0)
		{
			pass.problem = player ? "the player is dead" : "no player";
			Store(pass);
			return;
		}
		uint8_t* globals = Globals();
		const uint8_t* table = PointerAt(globals, kGlobalsAlliances);
		uint8_t* messages = PointerAt(globals, kGlobalsDamage);
		if (!table || !messages || me.alliance < 0 || me.alliance >= kAlliances)
		{
			pass.problem = "the game's alliances or damage messages aren't readable just now";
			Store(pass);
			return;
		}
		const void* sender = messages + kDamageWithReaction;
		const float radius = Radius();
		const bool spare = g_spareInvincible;
		const bool keptAlive = AI_COMBAT::NpcsKeptAlive();

		uint8_t* characters[kMaxCharacters];
		const int count = Characters(characters, kMaxCharacters);
		for (int i = 0; i < count; i++)
		{
			uint8_t* character = characters[i];
			Candidate them;
			if (character == player || !ReadCandidate(character, them))
				continue;
			float squared = 0.0f;
			for (int axis = 0; axis < 3; axis++)
				squared += (them.position[axis] - me.position[axis]) * (them.position[axis] - me.position[axis]);
			if (!(squared < radius * radius))
				continue;

			InRange entry;
			entry.id = them.id;
			entry.name = them.name;
			entry.characterClass = them.characterClass;
			entry.alliance = them.alliance;
			entry.distance = std::sqrt(squared);
			uint32_t enemies = 0;
			const bool enemy = them.alliance >= 0 && them.alliance < kAlliances && Read(table + them.alliance * 4, enemies) &&
				(enemies & (1u << me.alliance)) != 0;
			const uint8_t noDamage = static_cast<uint8_t>(them.noDamageFlags & 0xFF);
			// A body first: a character's alliance changes as it dies, so the enemy test would call it no enemy
			if (them.started && (them.dying || them.health == 0))
				entry.verdict = Verdict::Dying;
			else if (!enemy)
				entry.verdict = Verdict::NotEnemy;
			else if (!them.started)
				entry.verdict = Verdict::NotStarted;
			else if (them.playerControlled)
				entry.verdict = Verdict::PlayerControlled;
			else if (them.characterClass == kAlienClass)
				entry.verdict = Verdict::Alien;
			else if (them.dummy)
				entry.verdict = Verdict::Dummy;
			else if ((noDamage & kDebugBit) || AI_COMBAT::Invulnerable(them.id))
				entry.verdict = Verdict::Invulnerable;
			else if (spare && noDamage)
				entry.verdict = Verdict::ScriptInvincible;
			else if (keptAlive)
				entry.verdict = Verdict::KeptAlive;
			else
			{
				DamageMessage message = {};
				message.attacker[0] = me.handle[0];
				message.attacker[1] = me.handle[1];
				message.victim[0] = them.handle[0];
				message.victim[1] = them.handle[1];
				message.reaction = kReactionShotDeath;
				message.damage = static_cast<uint16_t>(them.health);
				NoteCleared(them);
				entry.verdict = Send(character, sender, message, true) ? Verdict::Hit : Verdict::Failed;
				// A kill counted now when the damage is taken already, else once it is seen dying (it usually is taken later)
				uint16_t health = 1;
				if (entry.verdict == Verdict::Hit && Read(character + kHealth, health) && health == 0)
					NoteKill(them);
				else if (entry.verdict == Verdict::Hit)
					NoteHit(them);
			}
			pass.inRange.push_back(entry);
		}
		Store(pass);
	}

	// AI thread, every frame while it is on (or has flags to give back, or hits to count)
	void Tick()
	{
		// Another level: its characters' ids are new, so those counted before don't stand in the way
		if (g_countedLevel != g_level)
		{
			g_countedLevel = g_level;
			g_countedIds.clear();
		}
		if (g_on)
			RunPass();
		else if (g_anyCleared)
			GiveFlagsBack();
		if (g_anyHits)
			SettleHits();
		g_tickQueued = false;
	}

	// ---- Named switches and commands ----

	std::string StatusLines()
	{
		std::ostringstream out;
		out << "on=" << (g_on ? 1 : 0) << "\nradius=" << Radius() << "\nspare_invincible=" << (g_spareInvincible ? 1 : 0)
			<< "\nkills=" << g_kills << "\n";
		if (AI_COMBAT::NpcsKeptAlive())
			out << "held=the NPC damage choice keeps a hit from killing\n";
		std::lock_guard<std::mutex> lock(g_passMutex);
		for (const std::string& killed : g_killed)
			out << "killed=" << killed << "\n";
		if (!g_pass.problem.empty())
			out << "problem=" << g_pass.problem << "\n";
		for (const InRange& entry : g_pass.inRange)
		{
			char distance[32];
			snprintf(distance, sizeof(distance), "%.1f m", entry.distance);
			out << "near=" << entry.name << " (" << entry.id << ") " << DEBUG_MENU::ClassName(entry.characterClass) << ", "
				<< AI_STATE::AllianceName(entry.alliance) << ", " << distance << ": " << VerdictText(entry.verdict) << "\n";
		}
		return out.str();
	}

	bool g_registered = false;

	void Register()
	{
		if (g_registered)
			return;
		g_registered = true;
		DEBUG_MENU::AddSwitch("player.stench_of_evil", &g_on);
		DEBUG_MENU::AddSwitch("player.stench_spare_invincible", &g_spareInvincible);
		DEBUG_MENU::AddSwitch("player.stench_ring", &g_ring);
		DEBUG_MENU::AddValue("player.stench_radius", &g_radius, kMinRadius, kMaxRadius);
		DEBUG_MENU::AddCommand("player.stench_status", "Stench of evil: on, its options, the kills, and the characters within its radius at the last AI frame (what it did to each)", [](const std::string&)
		{
			DEBUG_MENU::CommandResult result;
			if (!send_damage)
			{
				result.ok = false;
				result.message = "not available on this game release";
				return result;
			}
			result.message = StatusLines();
			return result;
		});
	}

	const bool g_named = (Register(), true);

	// ---- The ring, and the frame's work ----

	AI_COMBAT::LevelWatch g_watch;
	bool g_wasOn = false; // render thread

	// Render thread, every frame: a level change turns it off; the AI thread's pass queued while it is on (or has flags to
	// give back, or hits to count); the ring round the player
	void DrawOverlay(ImDrawList* drawList)
	{
		Register();
		if (g_watch.Changed())
		{
			if (g_on)
				SetStatus("stench of evil off: the level changed");
			g_on = false;
			g_level++;
			std::lock_guard<std::mutex> lock(g_passMutex);
			g_pass = Pass();
		}
		if (g_on && !send_damage)
			g_on = false;
		const bool on = g_on;
		if (on != g_wasOn)
		{
			g_wasOn = on;
			if (on)
				SetStatus("stench of evil on");
		}
		if ((on || g_anyCleared || g_anyHits) && !g_tickQueued)
		{
			g_tickQueued = true;
			DEBUG_MENU::OnAiThread(Tick);
		}
		if (!on || !g_ring)
			return;
		for (const DEBUG_MENU::Character& character : DEBUG_MENU::Characters())
			if (character.player)
			{
				DEBUG_MENU::WorldCircle(drawList, character.position, Radius(), IM_COL32(150, 205, 70, 90), 48, 1.5f);
				break;
			}
	}

	// ---- The section ----

	void DrawSection()
	{
		Register();
		ImGui::SeparatorText("Stench of evil");
		if (!send_damage)
		{
			ImGui::TextDisabled("Not available on this game release.");
			return;
		}
		bool on = g_on;
		if (ImGui::Checkbox("Stench of evil##section", &on))
			g_on = on;
		Tooltip("Every enemy of the player within the radius dies, every frame while it's on: a super power the game still has the code for, done as the game's own "
			"code does it. The radius is a straight line, through walls and floors. Its targets have their \"takes no damage\" flags cleared, then get damage equal "
			"to their health from the player and a shot death. The kills count as the player's (achievements and stats): turn on Block awards and stat writes and "
			"Don't write saves (Game > Saves) first. Killing a character a mission needs can stall the level. A level change turns it off.");
		ImGui::SameLine();
		float radius = Radius();
		ImGui::SetNextItemWidth(160.0f * DEBUG_MENU::Scale());
		if (ImGui::SliderFloat("Radius", &radius, kMinRadius, kMaxRadius, "%.1f m", ImGuiSliderFlags_AlwaysClamp))
			g_radius = radius;
		Tooltip("How far from the player it reaches (the game's own is 5 m).");

		bool spare = g_spareInvincible;
		if (ImGui::Checkbox("Spare the script-invincible", &spare))
			g_spareInvincible = spare;
		Tooltip("Off by default, which is how the game's own does it: every \"takes no damage\" flag on its targets is cleared, invincibility the level's scripts "
			"set included, so they die too (a character still alive when this is turned off gets its flags back). On: characters set to take no damage are left "
			"alone. Characters made invulnerable in Characters > Control are always left alone, and so is the alien (the game never lets it die of this).");
		ImGui::SameLine();
		bool ring = g_ring;
		if (ImGui::Checkbox("Show the ring", &ring))
			g_ring = ring;
		Tooltip("A faint ring on the ground round the player at the radius, while it's on.");

		if (g_on && AI_COMBAT::NpcsKeptAlive())
			ImGui::TextColored(ImVec4(1.0f, 0.75f, 0.3f, 1.0f), "Held: the NPC damage choice in Characters > Control keeps a hit from killing outright.");

		Pass pass;
		std::vector<std::string> killed;
		{
			std::lock_guard<std::mutex> lock(g_passMutex);
			pass = g_pass;
			killed = g_killed;
		}
		if (g_on && pass.ran)
		{
			if (!pass.problem.empty())
				WrappedDisabled("Nothing done: %s.", pass.problem.c_str());
			else if (pass.inRange.empty())
				ImGui::TextDisabled("Nobody within %.1f m.", Radius());
			for (const InRange& entry : pass.inRange)
				WrappedDisabled("%s (%s, %s) %.1f m: %s", entry.name.c_str(), DEBUG_MENU::ClassName(entry.characterClass), AI_STATE::AllianceName(entry.alliance),
					entry.distance, VerdictText(entry.verdict));
		}
		if (const uint32_t kills = g_kills)
		{
			std::string latest;
			for (auto it = killed.rbegin(); it != killed.rend(); ++it)
				latest += (latest.empty() ? "" : ", ") + *it;
			WrappedDisabled("Killed: %u. Latest: %s.", kills, latest.c_str());
		}
		const std::string status = Status();
		if (!status.empty())
			WrappedDisabled("Last: %s", status.c_str());
	}

	const bool g_added = DEBUG_MENU::AddTab("Player", 34, DrawSection, "Characters") && DEBUG_MENU::AddOverlay(9, DrawOverlay);
}

#include "PLAYER_DEBUG.h"
#include "DEBUG_MENU.h"
#include "LIVE_LINK.h"

#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <mutex>

#include <imgui.h>

/*
	Addresses and layouts are from the Steam retail AI.exe (see GameAddresses.h for the GOG and Epic Games Store builds).
*/
namespace
{
	// Where the game keeps its pointer to its globals; the byte after that pointer is set while its characters can be read
	constexpr uintptr_t kGameGlobals = 0x012F0C88;
	constexpr uint32_t kCharactersValid = 0x04;
	// In the globals: the character manager, the item templates and the player's inventory (pointers)
	constexpr uint32_t kGlobalsCharacters = 0x08;
	constexpr uint32_t kGlobalsItems = 0x40;
	constexpr uint32_t kGlobalsInventory = 0x44;
	// The character manager's lists of characters, each a pointer to their pointers then how many: the players and those
	// alive that aren't players
	constexpr uint32_t kPlayers = 0x80;
	constexpr uint32_t kAlive = 0xD0;
	constexpr uint32_t kMaxPerList = 18;

	// A character's fields: its unique id, what it was spawned as (a pointer: its class at +4) and its logic flags (a byte
	// each)
	constexpr uint32_t kCharacterId = 0x50;
	constexpr uint32_t kCharacterSpawnedAs = 0x64;
	constexpr uint32_t kSpawnedAsClass = 0x04;
	constexpr uint32_t kCharacterFlags = 0x423C;
	constexpr int kFlagCount = 40;

	// The game clock: the game time now (a double)
	constexpr uintptr_t kGameTime = 0x012410A8;

	// The game's text lookup: a text key's text in the game's language, into a buffer (false: no such key)
	typedef bool(__cdecl* t_translate)(const char* key, char* out, uint32_t size);
	const auto translate_text = reinterpret_cast<t_translate>(DEVTOOLS_RELATIVE_ADDRESS(0x00374460));

	std::mutex g_statusMutex;
	std::string g_status[static_cast<int>(PLAYER_DEBUG::StatusArea::Count)];

	uint8_t* GlobalsGuarded(bool needCharacters)
	{
		const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kGameGlobals);
		if (!at)
			return nullptr;
		__try
		{
			if (needCharacters && !*reinterpret_cast<const uint8_t*>(at + kCharactersValid))
				return nullptr;
			return *reinterpret_cast<uint8_t**>(at);
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return nullptr;
		}
	}

	// One list of the character manager's (up to max), guarded
	int ReadList(const uint8_t* manager, uint32_t offset, uint8_t** out, int max)
	{
		__try
		{
			uint8_t* const* list = *reinterpret_cast<uint8_t* const* const*>(manager + offset);
			const uint32_t count = *reinterpret_cast<const uint32_t*>(manager + offset + 4);
			int found = 0;
			for (uint32_t i = 0; list && i < count && i < kMaxPerList && found < max; i++)
				if (list[i])
					out[found++] = list[i];
			return found;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			return 0;
		}
	}

	uint8_t* CharacterManager()
	{
		return PLAYER_DEBUG::PointerAt(GlobalsGuarded(true), kGlobalsCharacters);
	}

	// Copies a string of the game's out, guarded (plain data in and out)
	size_t CopyText(const char* text, char* out, size_t size)
	{
		out[0] = '\0';
		if (!text)
			return 0;
		__try
		{
			size_t i = 0;
			for (; i + 1 < size && text[i]; i++)
				out[i] = text[i];
			out[i] = '\0';
			return i;
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			out[0] = '\0';
			return 0;
		}
	}

	// The game's text for a key, guarded (false: none)
	bool TranslateGuarded(const char* key, char* out, uint32_t size)
	{
		out[0] = '\0';
		__try
		{
			const bool found = translate_text(key, out, size);
			out[size - 1] = '\0';
			return found && out[0];
		}
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
			out[0] = '\0';
			return false;
		}
	}
}

uint8_t* PLAYER_DEBUG::Globals()
{
	return GlobalsGuarded(false);
}

bool PLAYER_DEBUG::LevelRunning()
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

uint8_t* PLAYER_DEBUG::Player()
{
	if (!LevelRunning())
		return nullptr;
	uint8_t* manager = CharacterManager();
	uint8_t* players[1] = {};
	return manager && ReadList(manager, kPlayers, players, 1) == 1 ? players[0] : nullptr;
}

uint32_t PLAYER_DEBUG::CharacterId(const uint8_t* character)
{
	uint32_t id = 0;
	return character && Read(character + kCharacterId, id) ? id : 0;
}

int PLAYER_DEBUG::CharacterClass(const uint8_t* character)
{
	const uint8_t* spawnedAs = PointerAt(character, kCharacterSpawnedAs);
	int characterClass = -1;
	return spawnedAs && Read(spawnedAs + kSpawnedAsClass, characterClass) ? characterClass : -1;
}

int PLAYER_DEBUG::Characters(uint8_t** out, int max)
{
	if (!LevelRunning())
		return 0;
	uint8_t* manager = CharacterManager();
	if (!manager)
		return 0;
	int count = ReadList(manager, kPlayers, out, max);
	uint8_t* alive[kMaxPerList];
	const int others = ReadList(manager, kAlive, alive, kMaxPerList);
	for (int i = 0; i < others && count < max; i++)
	{
		// The player can be in the living list too: listed once
		bool listed = false;
		for (int j = 0; j < count; j++)
			listed = listed || out[j] == alive[i];
		if (!listed)
			out[count++] = alive[i];
	}
	return count;
}

uint8_t* PLAYER_DEBUG::FindCharacter(uint32_t id)
{
	if (!id)
		return nullptr;
	uint8_t* characters[kMaxPerList * 2];
	const int count = Characters(characters, kMaxPerList * 2);
	for (int i = 0; i < count; i++)
		if (CharacterId(characters[i]) == id)
			return characters[i];
	return nullptr;
}

bool PLAYER_DEBUG::LogicFlag(const uint8_t* character, int flag)
{
	uint8_t value = 0;
	return character && flag >= 0 && flag < kFlagCount && Read(character + kCharacterFlags + flag, value) && value != 0;
}

uint8_t* PLAYER_DEBUG::Inventory()
{
	return LevelRunning() ? PointerAt(Globals(), kGlobalsInventory) : nullptr;
}

uint8_t* PLAYER_DEBUG::ItemDatabase()
{
	return PointerAt(Globals(), kGlobalsItems);
}

double PLAYER_DEBUG::GameTime()
{
	const uintptr_t at = DEVTOOLS_RELATIVE_ADDRESS(kGameTime);
	double time = 0.0;
	return at && Read(reinterpret_cast<const void*>(at), time) ? time : 0.0;
}

uint8_t* PLAYER_DEBUG::PointerAt(const void* object, uint32_t offset)
{
	uint8_t* value = nullptr;
	return object && Read(static_cast<const uint8_t*>(object) + offset, value) ? value : nullptr;
}

std::string PLAYER_DEBUG::Text(const void* text, size_t max)
{
	char buffer[260];
	const size_t size = max + 1 < sizeof(buffer) ? max + 1 : sizeof(buffer);
	CopyText(static_cast<const char*>(text), buffer, size);
	return buffer;
}

std::string PLAYER_DEBUG::TextAt(const void* object, uint32_t offset, size_t max)
{
	return Text(PointerAt(object, offset), max);
}

std::string PLAYER_DEBUG::Translated(const std::string& key)
{
	char text[512];
	if (key.empty() || !translate_text || !TranslateGuarded(key.c_str(), text, sizeof(text)))
		return std::string();
	return text;
}

void PLAYER_DEBUG::SetStatus(StatusArea area, const std::string& text)
{
	std::lock_guard<std::mutex> lock(g_statusMutex);
	g_status[static_cast<int>(area)] = text;
	DevTools::Log("Debug menu: %s", text.c_str());
}

std::string PLAYER_DEBUG::Status(StatusArea area)
{
	std::lock_guard<std::mutex> lock(g_statusMutex);
	return g_status[static_cast<int>(area)];
}

void PLAYER_DEBUG::Tooltip(const char* text)
{
	if (!ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip | ImGuiHoveredFlags_AllowWhenDisabled))
		return;
	ImGui::BeginTooltip();
	ImGui::PushTextWrapPos(ImGui::GetFontSize() * 28.0f);
	ImGui::TextUnformatted(text);
	ImGui::PopTextWrapPos();
	ImGui::EndTooltip();
}

void PLAYER_DEBUG::SwitchBox(const char* label, const char* name, const char* tooltip)
{
	bool value = false;
	const bool named = DEBUG_MENU::GetSwitch(name, value);
	ImGui::BeginDisabled(!named);
	if (ImGui::Checkbox(label, &value))
		DEBUG_MENU::SetSwitch(name, value);
	ImGui::EndDisabled();
	Tooltip(tooltip);
}

void PLAYER_DEBUG::DrawStatus(StatusArea area)
{
	const std::string status = Status(area);
	if (status.empty())
		return;
	WrappedDisabled("Last: %s", status.c_str());
}

void PLAYER_DEBUG::WrappedDisabled(const char* format, ...)
{
	va_list args;
	va_start(args, format);
	ImGui::PushTextWrapPos(0.0f);
	ImGui::PushStyleColor(ImGuiCol_Text, ImGui::GetStyle().Colors[ImGuiCol_TextDisabled]);
	ImGui::TextV(format, args);
	ImGui::PopStyleColor();
	ImGui::PopTextWrapPos();
	va_end(args);
}

bool PLAYER_DEBUG::ParseFloats(const std::string& text, float* out, int count)
{
	const char* at = text.c_str();
	for (int i = 0; i < count; i++)
	{
		while (*at == ' ' || *at == ',' || *at == '\t' || *at == ';')
			at++;
		char* end = nullptr;
		const float value = strtof(at, &end);
		if (end == at)
			return false;
		out[i] = value;
		at = end;
	}
	return true;
}

std::string PLAYER_DEBUG::Vector(const float value[3])
{
	char text[96];
	snprintf(text, sizeof(text), "%.2f,%.2f,%.2f", value[0], value[1], value[2]);
	return text;
}

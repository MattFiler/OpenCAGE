#include "DEBUG_TEXT.h"
#include "DEBUG_MARKER.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <imgui.h>

#include <cfloat>
#include <cstdio>
#include <mutex>
#include <string>
#include <vector>

namespace
{
	struct TextEntry
	{
		void* owner;
		std::string text;
		int alignment; 
		float size;
		ImU32 colour;
		bool stacking;
		ULONGLONG expiresAt;
		ULONGLONG refreshedAt;
	};

	std::mutex g_mutex;
	std::vector<TextEntry> g_entries;

	constexpr int kDefaultSize = 20;
	constexpr uint32_t kInvalidString = 0xFFFFFFFF;
	constexpr size_t kMaxStackEntries = 5;
	constexpr ULONGLONG kRefreshIntervalMs = 100; 

	struct ParameterGuids
	{
		uintptr_t text, size, colour;
		uintptr_t textInput, intInput, floatInput, boolInput, vectorInput, enumInput;
	};

	const ParameterGuids kDebugText = {
		0x01352244, 0x0135224c, 0x01352250,
		0x01352240, 0x01352230, 0x0135222c, 0x01352234, 0x01352238, 0x0135223c,
	};
	const ParameterGuids kDebugTextStacking = {
		0x0135220c, 0x01352214, 0x01352218,
		0,          0x013521fc, 0x013521f8, 0x01352200, 0x01352204, 0x01352208,
	};

	const void* Guid(uintptr_t offset)
	{
		return reinterpret_cast<const void*>(DEVTOOLS_RELATIVE_ADDRESS(offset));
	}

	std::string ResolveString(uint32_t offset)
	{
		if (offset == kInvalidString)
			return "";
		void* table = *DEBUG_TEXT::string_table_instance;
		if (!table)
			return "";
		const char* text = DEBUG_TEXT::string_from_offset(table, offset);
		return text ? text : "";
	}

	std::string ReadText(void* entityInterface, const void* entity, const void* guid)
	{
		uint32_t offset = kInvalidString;
		if (!DEBUG_TEXT::find_parameter_string(entityInterface, entity, guid, &offset))
			return "";
		return ResolveString(offset);
	}

	int ReadSize(void* entityInterface, const void* entity, const void* guid)
	{
		int size = kDefaultSize;
		DEBUG_TEXT::find_parameter_int(entityInterface, entity, guid, &size);
		return size > 0 ? size : kDefaultSize;
	}

	ImU32 ReadColour(void* entityInterface, const void* entity, const void* guid)
	{
		alignas(16) float value[4] = { 255.0f, 255.0f, 255.0f, 0.0f };
		if (!DEBUG_TEXT::find_parameter_vector(entityInterface, entity, guid, value))
			return IM_COL32_WHITE;

		auto channel = [](float v) { return static_cast<int>(v < 0.0f ? 0.0f : v > 255.0f ? 255.0f : v); };
		return IM_COL32(channel(value[0]), channel(value[1]), channel(value[2]), 255);
	}

	bool IsLinked(void* entityInterface, const void* entity, const void* guid)
	{
		alignas(16) unsigned char lock[16] = {};
		DEBUG_TEXT::cache_lock_ctor(lock, entity);
		const void* entry = DEBUG_TEXT::find_entry(entityInterface, entity, guid);
		const bool linked = entry != nullptr && *reinterpret_cast<void* const*>(static_cast<const char*>(entry) + 8) != nullptr;
		DEBUG_TEXT::cache_lock_dtor(lock);
		return linked;
	}

	std::string FormatFloat(float value)
	{
		char buffer[32];
		snprintf(buffer, sizeof(buffer), "%g", value);
		return buffer;
	}

	std::string ReadInputs(void* entityInterface, const void* entity, const ParameterGuids& guids)
	{
		std::string out;
		auto append = [&out](const std::string& value) { out += " ["; out += value; out += "]"; };

		if (guids.textInput)
		{
			uint32_t offset = kInvalidString;
			if (DEBUG_TEXT::find_parameter_string(entityInterface, entity, Guid(guids.textInput), &offset))
			{
				const std::string value = ResolveString(offset);
				if (!value.empty() || IsLinked(entityInterface, entity, Guid(guids.textInput)))
					append(value);
			}
		}
		{
			int value = 0;
			if (DEBUG_TEXT::find_parameter_int(entityInterface, entity, Guid(guids.intInput), &value))
				if (value != 0 || IsLinked(entityInterface, entity, Guid(guids.intInput)))
					append(std::to_string(value));
		}
		{
			float value = 0.0f;
			if (DEBUG_TEXT::find_parameter_float(entityInterface, entity, Guid(guids.floatInput), &value))
				if (value != 0.0f || IsLinked(entityInterface, entity, Guid(guids.floatInput)))
					append(FormatFloat(value));
		}
		{
			bool value = false;
			if (DEBUG_TEXT::find_parameter_bool(entityInterface, entity, Guid(guids.boolInput), &value))
				if (value || IsLinked(entityInterface, entity, Guid(guids.boolInput)))
					append(value ? "true" : "false");
		}
		{
			alignas(16) float value[4] = {};
			if (DEBUG_TEXT::find_parameter_vector(entityInterface, entity, Guid(guids.vectorInput), value))
				if (value[0] != 0.0f || value[1] != 0.0f || value[2] != 0.0f || IsLinked(entityInterface, entity, Guid(guids.vectorInput)))
					append(FormatFloat(value[0]) + ", " + FormatFloat(value[1]) + ", " + FormatFloat(value[2]));
		}
		{
			DEBUG_TEXT::Enum value = { 0, -1 };
			if (DEBUG_TEXT::find_parameter_enum(entityInterface, entity, Guid(guids.enumInput), &value))
				if (value.index != -1 || IsLinked(entityInterface, entity, Guid(guids.enumInput)))
					append(std::to_string(value.index));
		}
		return out;
	}

	void RemoveEntry(void* owner)
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		for (auto it = g_entries.begin(); it != g_entries.end();)
			it = (it->owner == owner) ? g_entries.erase(it) : it + 1;
	}

	void RemoveAnchored(int alignment /* -1 for all */)
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		for (auto it = g_entries.begin(); it != g_entries.end();)
			it = (!it->stacking && (alignment < 0 || it->alignment == alignment)) ? g_entries.erase(it) : it + 1;
	}

	void RemoveStacking(bool lastOnly)
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		for (auto it = g_entries.end(); it != g_entries.begin();)
		{
			--it;
			if (!it->stacking)
				continue;
			it = g_entries.erase(it);
			if (lastOnly)
				return;
		}
	}
}

bool DEBUG_TEXT::EnableEntities(bool text, bool stacking)
{
	bool ok = true;
	if (text)
		ok = DevTools::EnableEntity(0x00e57768) && ok;
	if (stacking)
		ok = DevTools::EnableEntity(0x00e57280) && ok;
	return ok;
}

void DEBUG_TEXT::ClearAll()
{
	std::lock_guard<std::mutex> lock(g_mutex);
	g_entries.clear();
}

__declspec(noinline)
void __fastcall DEBUG_TEXT::h_level_close(void* _this, void* /*_EDX*/)
{
	ClearAll();
	DEBUG_MARKER::ClearAll();
	level_close(_this);
}

__declspec(noinline)
void __fastcall DEBUG_TEXT::h_destructor(void* _this, void* /*_EDX*/)
{
	RemoveEntry(_this);
	destructor(_this);
}

__declspec(noinline)
void __fastcall DEBUG_TEXT::h_stacking_destructor(void* _this, void* /*_EDX*/)
{
	RemoveEntry(_this);
	stacking_destructor(_this);
}

__declspec(noinline)
bool __fastcall DEBUG_TEXT::h_on_start(void* _this, void* /*_EDX*/, const void* entity, const void* trigger)
{
	const std::string text = ReadText(_this, entity, Guid(kDebugText.text)) + ReadInputs(_this, entity, kDebugText);
	DevTools::Log("DebugText: %s", text.c_str());
	const int alignment = get_alignment(_this, entity);
	const int size = ReadSize(_this, entity, Guid(kDebugText.size));
	const ImU32 colour = ReadColour(_this, entity, Guid(kDebugText.colour));
	const float duration = get_duration(_this, entity);

	RemoveEntry(_this);
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		const ULONGLONG now = GetTickCount64();
		const ULONGLONG expiresAt = duration > 0.0f ? now + static_cast<ULONGLONG>(duration * 1000.0f) : 0; // <= 0: stays until stopped.
		g_entries.push_back({ _this, text, alignment, static_cast<float>(size), colour, false, expiresAt, now });
	}

	const bool result = on_start(_this, entity, trigger);

	start_updating(static_cast<char*>(_this) + kEntityStateOffset, entity, trigger, true);

	return result;
}

__declspec(noinline)
bool __fastcall DEBUG_TEXT::h_on_update(void* _this, void* /*_EDX*/, const void* entity, const void* trigger)
{
	bool refresh = false;
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		for (const TextEntry& entry : g_entries)
			if (entry.owner == _this && GetTickCount64() - entry.refreshedAt >= kRefreshIntervalMs)
				refresh = true;
	}

	if (refresh)
	{
		const std::string text = ReadText(_this, entity, Guid(kDebugText.text)) + ReadInputs(_this, entity, kDebugText);
		const int alignment = get_alignment(_this, entity);
		const int size = ReadSize(_this, entity, Guid(kDebugText.size));
		const ImU32 colour = ReadColour(_this, entity, Guid(kDebugText.colour));

		std::lock_guard<std::mutex> lock(g_mutex);
		for (TextEntry& entry : g_entries)
		{
			if (entry.owner != _this)
				continue;
			entry.text = text;
			entry.alignment = alignment;
			entry.size = static_cast<float>(size);
			entry.colour = colour;
			entry.refreshedAt = GetTickCount64();
		}
	}

	return on_update(_this, entity, trigger);
}

__declspec(noinline)
bool __fastcall DEBUG_TEXT::h_on_stop(void* _this, void* /*_EDX*/, const void* entity, const void* trigger)
{
	RemoveEntry(_this);
	return on_stop(_this, entity, trigger);
}

__declspec(noinline)
bool __fastcall DEBUG_TEXT::h_on_clear_all(void* _this, void* /*_EDX*/, const void* entity, const void* trigger)
{
	RemoveAnchored(-1);
	return on_clear_all(_this, entity, trigger);
}

__declspec(noinline)
bool __fastcall DEBUG_TEXT::h_on_clear_of_alignment(void* _this, void* /*_EDX*/, const void* entity, const void* trigger)
{
	RemoveAnchored(get_alignment(_this, entity));
	return on_clear_of_alignment(_this, entity, trigger);
}

__declspec(noinline)
bool __fastcall DEBUG_TEXT::h_stacking_on_start(void* _this, void* /*_EDX*/, const void* entity, const void* trigger)
{
	const std::string text = ReadText(_this, entity, Guid(kDebugTextStacking.text)) + ReadInputs(_this, entity, kDebugTextStacking);
	DevTools::Log("DebugTextStacking: %s", text.c_str());
	const int size = ReadSize(_this, entity, Guid(kDebugTextStacking.size));
	const ImU32 colour = ReadColour(_this, entity, Guid(kDebugTextStacking.colour));

	{
		std::lock_guard<std::mutex> lock(g_mutex);
		const ULONGLONG now = GetTickCount64();
		g_entries.push_back({ _this, text, 3 /* middle left */, static_cast<float>(size), colour, true, 0, now });

		// Keep the stack to its last few entries, dropping the oldest.
		size_t stacked = 0;
		for (const TextEntry& entry : g_entries)
			stacked += entry.stacking ? 1 : 0;
		for (auto it = g_entries.begin(); it != g_entries.end() && stacked > kMaxStackEntries;)
		{
			if (!it->stacking) { ++it; continue; }
			it = g_entries.erase(it);
			--stacked;
		}
	}

	return stacking_on_start(_this, entity, trigger);
}

__declspec(noinline)
bool __fastcall DEBUG_TEXT::h_stacking_on_clear_all(void* _this, void* /*_EDX*/, const void* entity, const void* trigger)
{
	RemoveStacking(false);
	return stacking_on_clear_all(_this, entity, trigger);
}

__declspec(noinline)
bool __fastcall DEBUG_TEXT::h_stacking_on_clear_last(void* _this, void* /*_EDX*/, const void* entity, const void* trigger)
{
	RemoveStacking(true);
	return stacking_on_clear_last(_this, entity, trigger);
}

void DEBUG_TEXT::DrawOverlay()
{
	std::lock_guard<std::mutex> lock(g_mutex);

	const ULONGLONG now = GetTickCount64();
	for (auto it = g_entries.begin(); it != g_entries.end();)
		it = (it->expiresAt != 0 && now >= it->expiresAt) ? g_entries.erase(it) : it + 1;

	if (g_entries.empty())
		return;

	ImDrawList* drawList = ImGui::GetForegroundDrawList();
	ImFont* font = ImGui::GetFont();
	const ImVec2 screen = ImGui::GetIO().DisplaySize;
	constexpr float margin = 40.0f;
	constexpr float lineGap = 4.0f;

	auto drawText = [&](const ImVec2& pos, const TextEntry& entry)
	{
		drawList->AddText(font, entry.size, ImVec2(pos.x + 1.0f, pos.y + 1.0f), IM_COL32(0, 0, 0, 200), entry.text.c_str());
		drawList->AddText(font, entry.size, pos, entry.colour, entry.text.c_str());
	};

	float cellOffset[9] = {};
	for (const TextEntry& entry : g_entries)
	{
		if (entry.stacking)
			continue;

		const int cell = (entry.alignment >= 0 && entry.alignment < 9) ? entry.alignment : 0;
		const int column = cell % 3;
		const int row = cell / 3;
		const ImVec2 textSize = font->CalcTextSizeA(entry.size, FLT_MAX, 0.0f, entry.text.c_str());

		const float x = column == 0 ? margin : column == 1 ? (screen.x - textSize.x) * 0.5f : screen.x - margin - textSize.x;
		const float y = row == 0 ? margin : row == 1 ? (screen.y - textSize.y) * 0.5f : screen.y - margin - textSize.y;

		drawText(ImVec2(x, y + cellOffset[cell]), entry);
		cellOffset[cell] += textSize.y + lineGap;
	}

	float stackHeight = 0.0f;
	for (const TextEntry& entry : g_entries)
		if (entry.stacking)
			stackHeight += font->CalcTextSizeA(entry.size, FLT_MAX, 0.0f, entry.text.c_str()).y + lineGap;

	float stackY = (screen.y - stackHeight) * 0.5f + cellOffset[3];
	for (const TextEntry& entry : g_entries)
	{
		if (!entry.stacking)
			continue;

		drawText(ImVec2(margin, stackY), entry);
		stackY += font->CalcTextSizeA(entry.size, FLT_MAX, 0.0f, entry.text.c_str()).y + lineGap;
	}
}

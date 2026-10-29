#include "RENDER_COMMON.h"
#include "DEBUG_MENU.h"

#include <algorithm>
#include <cfloat>
#include <cstdarg>
#include <cstring>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

namespace
{
	constexpr uint8_t kCallOpcode = 0xE8;

	int32_t DistanceTo(const uint8_t* call, uintptr_t target)
	{
		return static_cast<int32_t>(target - (reinterpret_cast<uintptr_t>(call) + 5));
	}

	// Writes a call's four distance bytes: as one locked exchange where they sit inside one 64-byte cache line (so other
	// processors see them whole), plainly otherwise
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
}

uintptr_t RENDER_COMMON::Engine()
{
	return DEVTOOLS_RELATIVE_ADDRESS(kEngine);
}

uintptr_t RENDER_COMMON::Game(uintptr_t steam)
{
	return DEVTOOLS_RELATIVE_ADDRESS(steam);
}

bool RENDER_COMMON::Read(const void* at, void* out, size_t size)
{
	if (!at)
		return false;
	__try
	{
		memcpy(out, at, size);
		return true;
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return false;
	}
}

bool RENDER_COMMON::Write(void* at, const void* value, size_t size)
{
	if (!at)
		return false;
	__try
	{
		memcpy(at, value, size);
		return true;
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return false;
	}
}

uintptr_t RENDER_COMMON::Pointer(uintptr_t at)
{
	uintptr_t value = 0;
	return Read(at, value) ? value : 0;
}

std::string RENDER_COMMON::Text(uintptr_t at, size_t size)
{
	if (!at || size < 2)
		return {};
	char buffer[512] = {};
	if (size > sizeof(buffer))
		size = sizeof(buffer);
	// Read a character at a time up to the end, so text near the end of readable memory is still read
	size_t length = 0;
	for (; length + 1 < size; length++)
	{
		char c;
		if (!Read(reinterpret_cast<const void*>(at + length), &c, 1) || c == '\0')
			break;
		buffer[length] = c;
	}
	return std::string(buffer, length);
}

bool RENDER_COMMON::Check(Call& call, uintptr_t expectedTarget, const char* what)
{
	if (call.checked)
		return call.ok;
	call.checked = true;
	call.ok = false;
	call.at = reinterpret_cast<uint8_t*>(DEVTOOLS_RELATIVE_ADDRESS(call.steam));
	uint8_t bytes[5];
	if (!call.at || !Read(call.at, bytes, sizeof(bytes)) || bytes[0] != kCallOpcode)
	{
		DevTools::Log("Debug menu: %s is not available - the call for 0x%08X is not where it should be.", what, call.steam);
		return false;
	}
	memcpy(&call.own, bytes + 1, sizeof(call.own));
	call.target = reinterpret_cast<uintptr_t>(call.at) + 5 + call.own;
	if (expectedTarget)
	{
		const uintptr_t expected = DEVTOOLS_RELATIVE_ADDRESS(expectedTarget);
		if (!expected || expected != call.target)
		{
			DevTools::Log("Debug menu: %s is not available - the call for 0x%08X does not reach the expected function.", what, call.steam);
			return false;
		}
	}
	call.ok = true;
	return true;
}

bool RENDER_COMMON::Turn(Call& call, const void* to)
{
	if (!call.ok)
		return false;
	uint8_t bytes[5];
	int32_t now;
	if (!Read(call.at, bytes, sizeof(bytes)) || bytes[0] != kCallOpcode)
		return false;
	memcpy(&now, bytes + 1, sizeof(now));
	static_assert(sizeof(uintptr_t) == 4, "a 32-bit game");
	// Where it reaches now must be where it was left: its own function, or ours
	const uintptr_t reaches = reinterpret_cast<uintptr_t>(call.at) + 5 + now;
	if (reaches != (call.turned ? call.ours : call.target))
		return false;
	const int32_t distance = to ? DistanceTo(call.at, reinterpret_cast<uintptr_t>(to)) : call.own;
	if (!WriteDistance(call.at, distance))
		return false;
	call.turned = to != nullptr;
	call.ours = reinterpret_cast<uintptr_t>(to);
	return true;
}

void RENDER_COMMON::Note(const char* format, ...)
{
	va_list args;
	va_start(args, format);
	ImGui::PushStyleColor(ImGuiCol_Text, ImGui::GetStyleColorVec4(ImGuiCol_TextDisabled));
	ImGui::TextWrappedV(format, args);
	ImGui::PopStyleColor();
	va_end(args);
}

void RENDER_COMMON::ColouredNote(const ImVec4& colour, const char* format, ...)
{
	va_list args;
	va_start(args, format);
	ImGui::PushStyleColor(ImGuiCol_Text, colour);
	ImGui::TextWrappedV(format, args);
	ImGui::PopStyleColor();
	va_end(args);
}

void RENDER_COMMON::ScreenLabel(ImDrawList* drawList, const float world[3], const char* text, ImU32 colour, float yOffsetPixels)
{
	ImVec2 at;
	if (!drawList || !DEBUG_MENU::Project(world, at))
		return;
	const ImVec2 display = ImGui::GetIO().DisplaySize;
	if (at.x < 0.0f || at.y < 0.0f || at.x > display.x || at.y > display.y)
		return;
	// Laid out as the debug menu's own labels (centred over the point, 4 x 2 pixels of backing round the text), then
	// moved in from any edge it would cross
	const float size = 15.0f * DEBUG_MENU::Scale();
	const ImVec2 extent = ImGui::GetFont()->CalcTextSizeA(size, FLT_MAX, 0.0f, text);
	const float margin = 4.0f;
	ImVec2 corner(at.x - extent.x * 0.5f, at.y - extent.y + yOffsetPixels);
	corner.x = (std::max)(margin + 4.0f, (std::min)(corner.x, display.x - margin - 4.0f - extent.x));
	corner.y = (std::max)(margin + 2.0f, (std::min)(corner.y, display.y - margin - 2.0f - extent.y));
	drawList->AddRectFilled(ImVec2(corner.x - 4.0f, corner.y - 2.0f), ImVec2(corner.x + extent.x + 4.0f, corner.y + extent.y + 2.0f), IM_COL32(0, 0, 0, 150), 3.0f);
	drawList->AddText(ImGui::GetFont(), size, corner, colour, text);
}

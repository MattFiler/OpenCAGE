#include "MOUSE_UNLOCK.h"
#include "Config.h"

#include <atomic>
#include <cfloat>
#include <cstdio>
#include <intrin.h>
#include <mutex>

#include <imgui.h>

/*
	For the retail AI.exe (in the Steam build the cursor routine is at VA 0x00A109A0: it reads its one argument as a
	byte, clips the cursor to the single pixel in the middle of the window when it is set and frees it when it isn't,
	and keeps it on the mouse).
*/
namespace
{
	// Whether the cursor is free: the key takes turns, and it starts free
	std::atomic<bool> g_free = true;

	// What the game last asked of the cursor routine, and for which mouse: the routine is called from the window thread
	// and the game's own, so these (and the calls to it) are under g_mutex
	std::mutex g_mutex;
	void* g_mouse = nullptr;
	bool g_grabAsked = false;
	// The same, read without the lock by the window thread and the overlay
	std::atomic<bool> g_grabWanted = false;

	// Whether the cursor is shown by this over the game window (the window thread only): the game hides it while it
	// grabs it, and shows and hides it only when its own state changes, so this keeps its own count beside the game's
	bool g_shown = false;

	// The overlay's note: shown for a few seconds from g_noteAt, saying whether the cursor was freed
	constexpr ULONGLONG kNoteMs = 4000;
	std::atomic<ULONGLONG> g_noteAt = 0;
	std::atomic<bool> g_noteFree = true;
	std::atomic<bool> g_hinted = false;

	// Whether an address is in the game's own code (not a DLL's - another tool's, Windows')
	bool InGame(uintptr_t address)
	{
		static const uintptr_t start = DevTools::GameProcess::GetBaseAddress();
		static const uintptr_t end = [] {
			const IMAGE_DOS_HEADER* dos = reinterpret_cast<const IMAGE_DOS_HEADER*>(start);
			const IMAGE_NT_HEADERS* nt = reinterpret_cast<const IMAGE_NT_HEADERS*>(start + dos->e_lfanew);
			return start + nt->OptionalHeader.SizeOfImage;
		}();
		return address >= start && address < end;
	}

	// The key: frees the cursor, or hands it back to the game (grabbed again if the game wants it now)
	void Toggle()
	{
		bool free;
		{
			std::lock_guard<std::mutex> lock(g_mutex);
			free = !g_free;
			g_free = free;
			if (g_mouse && g_grabAsked)
				MOUSE_UNLOCK::grab_cursor(g_mouse, !free);
		}
		g_noteFree = free;
		g_noteAt = GetTickCount64();
		g_hinted = true;
		DevTools::Log("Unlock Mouse: %s", free ? "the cursor is free" : "the game has the mouse");
	}
}

__declspec(noinline)
void __fastcall MOUSE_UNLOCK::h_grab_cursor(void* _this, void* /*_EDX*/, bool grab)
{
	{
		std::lock_guard<std::mutex> lock(g_mutex);
		g_mouse = _this;
		g_grabAsked = grab;
		g_grabWanted = grab;
		grab_cursor(_this, grab && !g_free);
	}

	// The first time a level is played with the cursor free, say how to look around
	if (grab && g_free && !g_hinted.exchange(true))
	{
		g_noteFree = true;
		g_noteAt = GetTickCount64();
	}
}

BOOL WINAPI MOUSE_UNLOCK::h_set_cursor_pos(int x, int y)
{
	// The game putting the cursor back: it stays where it was left
	if (g_free && InGame(reinterpret_cast<uintptr_t>(_ReturnAddress())))
		return TRUE;
	return set_cursor_pos(x, y);
}

bool MOUSE_UNLOCK::OnWindowMessage(HWND window, UINT message, WPARAM wParam, LPARAM lParam, LRESULT& result)
{
	if (message == WM_KEYUP && wParam == static_cast<WPARAM>(Config::Get().unlockMouseKey))
		Toggle();

	const bool free = g_free;
	const bool grabWanted = g_grabWanted;

	const bool show = free && grabWanted;
	if (show != g_shown)
	{
		ShowCursor(show ? TRUE : FALSE);
		g_shown = show;
	}

	// The mouse kept from a level being played with the cursor free (the game reads it only while its window has the
	// focus). Its raw input is answered as the system needs it answered.
	if (message == WM_INPUT && free && grabWanted && GetFocus() == window)
	{
		RAWINPUTHEADER header = {};
		UINT size = sizeof(header);
		if (GetRawInputData(reinterpret_cast<HRAWINPUT>(lParam), RID_HEADER, &header, &size, sizeof(RAWINPUTHEADER)) == sizeof(header) && header.dwType == RIM_TYPEMOUSE)
		{
			result = DefWindowProc(window, message, wParam, lParam);
			return true;
		}
	}
	return false;
}

void MOUSE_UNLOCK::DrawOverlay()
{
	const ULONGLONG at = g_noteAt;
	if (at == 0 || GetTickCount64() - at > kNoteMs)
		return;

	char text[128];
	const char* key = Config::KeyName(Config::Get().unlockMouseKey);
	if (g_noteFree)
		snprintf(text, sizeof(text), "Mouse unlocked - press %s in the game to look around", key);
	else
		snprintf(text, sizeof(text), "Mouse locked to the game - press %s to unlock it", key);

	ImDrawList* drawList = ImGui::GetForegroundDrawList();
	const float size = 18.0f;
	const ImVec2 textSize = ImGui::GetFont()->CalcTextSizeA(size, FLT_MAX, 0.0f, text);
	const ImVec2 place(ImGui::GetIO().DisplaySize.x * 0.5f - textSize.x * 0.5f, 14.0f);
	drawList->AddRectFilled(ImVec2(place.x - 10.0f, place.y - 5.0f), ImVec2(place.x + textSize.x + 10.0f, place.y + textSize.y + 5.0f), IM_COL32(0, 0, 0, 150), 4.0f);
	drawList->AddText(ImGui::GetFont(), size, place, IM_COL32(235, 235, 235, 240), text);
}

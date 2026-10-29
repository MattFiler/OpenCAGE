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
	// Whether the key has the cursor free (with Unlock Mouse on): it takes turns, and starts free
	std::atomic<bool> g_free = true;
	// How many hold the cursor free (a cubemap capture, the debug menu while it is open)
	std::atomic<int> g_held = 0;
	// The hold as the game's routine was last told of it (the window thread only)
	bool g_heldPassedOn = false;

	// Whether the cursor is free now: by the key, or held
	bool Free()
	{
		return (g_free && Config::Get().unlockMouse) || g_held > 0;
	}

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
	// Its text size, and its box: centred along the top, the text this far down, padded this much around it
	constexpr float kNoteTextSize = 18.0f;
	constexpr float kNoteTop = 14.0f;
	constexpr float kNotePadX = 10.0f, kNotePadY = 5.0f;

	// The note's text while it shows, false once it has gone (or when there is none)
	bool NoteText(char* text, size_t size)
	{
		const ULONGLONG at = g_noteAt;
		if (at == 0 || GetTickCount64() - at > kNoteMs)
			return false;
		const char* key = Config::KeyName(Config::Get().unlockMouseKey);
		if (g_noteFree)
			snprintf(text, size, "Mouse unlocked - press %s in the game to look around", key);
		else
			snprintf(text, size, "Mouse locked to the game - press %s to unlock it", key);
		return true;
	}

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
				MOUSE_UNLOCK::grab_cursor(g_mouse, !Free());
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
		grab_cursor(_this, grab && !Free());
	}

	// The first time a level is played with the cursor free, say how to look around
	if (grab && g_free && Config::Get().unlockMouse && !g_hinted.exchange(true))
	{
		g_noteFree = true;
		g_noteAt = GetTickCount64();
	}
}

BOOL WINAPI MOUSE_UNLOCK::h_set_cursor_pos(int x, int y)
{
	// The game putting the cursor back: it stays where it was left
	if (Free() && InGame(reinterpret_cast<uintptr_t>(_ReturnAddress())))
		return TRUE;
	return set_cursor_pos(x, y);
}

bool MOUSE_UNLOCK::OnWindowMessage(HWND window, UINT message, WPARAM wParam, LPARAM lParam, LRESULT& result)
{
	if (Config::Get().unlockMouse && message == WM_KEYUP && wParam == static_cast<WPARAM>(Config::Get().unlockMouseKey))
		Toggle();

	// A hold starting or ending: the game's routine lets the cursor go or takes it back, as the key has it do
	const bool held = g_held > 0;
	if (held != g_heldPassedOn)
	{
		g_heldPassedOn = held;
		std::lock_guard<std::mutex> lock(g_mutex);
		if (g_mouse && g_grabAsked)
			grab_cursor(g_mouse, !Free());
	}

	const bool free = Free();
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

void MOUSE_UNLOCK::Hold(bool held)
{
	int holders = g_held.load();
	while (!g_held.compare_exchange_weak(holders, held ? holders + 1 : (holders > 0 ? holders - 1 : 0)))
	{
	}
	if (holders == 0 && held)
		DevTools::Log("Unlock Mouse: the cursor is held free");
	else if (holders == 1 && !held)
		DevTools::Log("Unlock Mouse: the cursor is no longer held free");
}

void MOUSE_UNLOCK::DrawOverlay()
{
	char text[128];
	if (!NoteText(text, sizeof(text)))
		return;

	ImDrawList* drawList = ImGui::GetForegroundDrawList();
	const ImVec2 textSize = ImGui::GetFont()->CalcTextSizeA(kNoteTextSize, FLT_MAX, 0.0f, text);
	const ImVec2 place(ImGui::GetIO().DisplaySize.x * 0.5f - textSize.x * 0.5f, kNoteTop);
	drawList->AddRectFilled(ImVec2(place.x - kNotePadX, place.y - kNotePadY), ImVec2(place.x + textSize.x + kNotePadX, place.y + textSize.y + kNotePadY), IM_COL32(0, 0, 0, 150), 4.0f);
	drawList->AddText(ImGui::GetFont(), kNoteTextSize, place, IM_COL32(235, 235, 235, 240), text);
}

float MOUSE_UNLOCK::NoteBottom()
{
	// The note is drawn only with Unlock Mouse on; worked out the way DrawOverlay places it, whether or not it has been
	// drawn yet this frame
	char text[128];
	if (!Config::Get().unlockMouse || !NoteText(text, sizeof(text)))
		return 0.0f;
	const ImVec2 textSize = ImGui::GetFont()->CalcTextSizeA(kNoteTextSize, FLT_MAX, 0.0f, text);
	return kNoteTop + textSize.y + kNotePadY;
}

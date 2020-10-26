#pragma once

#include <vector>
#include "imgui/imgui.h"

enum WindowsKey {
	BACKSPACE = 8,
	TAB = 9,
	ENTER = 13,
	SHIFT = 16,
	CTRL = 17,
	ALT = 18,
	PAUSE_BREAK = 19,
	CAPS_LOCK = 20,
	ESCAPE = 27,
	PAGE_UP = 33,
	PAGE_DOWN = 34,
	END = 35,
	HOME = 36,
	LEFT_ARROW = 37,
	UP_ARROW = 38,
	RIGHT_ARROW = 39,
	DOWN_ARROW = 40,
	INSERT = 45,
	//DELETE = 46,
	A0 = 48,
	A1 = 49,
	A2 = 50,
	A3 = 51,
	A4 = 52,
	A5 = 53,
	A6 = 54,
	A7 = 55,
	A8 = 56,
	A9 = 57,
	A = 65,
	B = 66,
	C = 67,
	D = 68,
	E = 69,
	F = 70,
	G = 71,
	H = 72,
	I = 73,
	J = 74,
	K = 75,
	L = 76,
	M = 77,
	N = 78,
	O = 79,
	P = 80,
	Q = 81,
	R = 82,
	S = 83,
	T = 84,
	U = 85,
	V = 86,
	W = 87,
	X = 88,
	Y = 89,
	Z = 90,
	LEFT_WINDOW_KEY = 91,
	RIGHT_WINDOW_KEY = 92,
	SELECT_KEY = 93,
	NUMPAD_0 = 96,
	NUMPAD_1 = 97,
	NUMPAD_2 = 98,
	NUMPAD_3 = 99,
	NUMPAD_4 = 100,
	NUMPAD_5 = 101,
	NUMPAD_6 = 102,
	NUMPAD_7 = 103,
	NUMPAD_8 = 104,
	NUMPAD_9 = 105,
	MULTIPLY = 106,
	ADD = 107,
	SUBTRACT = 109,
	DECIMAL_POINT = 110,
	DIVIDE = 111,
	F1 = 112,
	F2 = 113,
	F3 = 114,
	F4 = 115,
	F5 = 116,
	F6 = 117,
	F7 = 118,
	F8 = 119,
	F9 = 120,
	F10 = 121,
	F11 = 122,
	F12 = 123,
	NUM_LOCK = 144,
	SCROLL_LOCK = 145,
	SEMI_COLON = 186,
	EQUAL_SIGN = 187,
	COMMA = 188,
	DASH = 189,
	PERIOD = 190,
	FORWARD_SLASH = 191,
	GRAVE_ACCENT = 192,
	OPEN_BRACKET = 219,
	BACK_SLASH = 220,
	CLOSE_BRAKET = 221,
	SINGLE_QUOTE = 222
};

enum WindowsMouse {
	LEFT_CLICK,
	RIGHT_CLICK,
	MIDDLE_MOUSE,
	MOUSE_BUTTON_1,
	MOUSE_BUTTON_2,
	MOUSE_BUTTON_3
};

/* A simple wrapper around ImGui's IO, for keyboard and mouse controls */
class InputHandler
{
public:
	InputHandler() = default;
	~InputHandler() = default;

	/* Get ImGUI IO ref */
	static void Setup()
	{
		io = &ImGui::GetIO();
	}

	/* Is a key currently down? */
	static bool KeyDown(WindowsKey _key)
	{
		if (!enabled) return false;
		return io->KeysDown[(int)_key];
	}

	/* Is a mouse button down? */
	static bool MouseDown(WindowsMouse _btn)
	{
		if (!enabled) return false;
		return io->MouseDown[(int)_btn];
	}

	/* Enable/disable input */
	static void EnableInput(bool _e) { enabled = _e; }
	static void ToggleInput() { enabled = !enabled; }

protected:
	static ImGuiIO* io;
	static bool enabled;
};


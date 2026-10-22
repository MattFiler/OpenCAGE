#pragma once

#include "DevTools.h"
#include <cstdint>
#include <string>

#include <imgui.h>

/*
	What the debug menu's rendering views share (the Rendering group's tabs beyond the switches): guarded reads and writes
	of the game's memory, where the engine's own objects are, and calls in the game's code turned to one of ours.

	Every read or write of the game goes through Read / Write, which come back false instead of crashing when the memory
	has gone (a level unloading, a light let go of...).

	A redirected call is a five-byte call instruction in the game's code whose last four bytes (the distance to what it
	calls) are rewritten so that it calls a function of ours instead; ours calls on to the game's own function (whose
	address it was given when the call was checked) or does something else. Before anything is changed the call is
	checked: it must be a call instruction, reaching the function expected there when one is named. It is turned on the
	render thread at the frame's presentation (when the frame's own work is done and the next frame's has not started),
	its four bytes written in one locked exchange wherever they sit inside one cache line, and it is only turned back if it
	is still exactly as it was left. Each view turns its calls back when the hooks detach as the game closes.

	Addresses are the Steam build's (see GameAddresses.h for the GOG and Epic Games Store builds).
*/
namespace RENDER_COMMON
{
	// The engine object (the root of the renderer's objects), and the offsets in it the views use
	constexpr uintptr_t kEngine = 0x013583A0;
	constexpr uint32_t kEngineMaterials = 0x17A8;          // the material database; its deferred-light renderer (a pointer) at +0x50
	constexpr uint32_t kEngineDeferredLights = 0x17A8 + 0x50;
	constexpr uint32_t kEngineInstances = 0x1854 + 0x48;    // the instance database: count, then (at +8) the instances by mover index
	constexpr uint32_t kEngineRadiosity = 0x18C0;          // the radiosity (bounce light) object
	constexpr uint32_t kEngineLightManager = 0xD8F0;       // the light manager
	constexpr uint32_t kEngineViews = 0xE660;              // the view manager (a pointer)
	constexpr uint32_t kEngineEnvironmentMaps = 0xE668;    // the reflection map manager

	// The post-processing object (a static object), and its G-buffer manager
	constexpr uintptr_t kPostProcessing = 0x015D0970;
	constexpr uint32_t kPostGBuffer = 0xAFC;

	// The engine object in this game (0 when this build has no address for it)
	uintptr_t Engine();
	// A Steam address in this game (0 when this build has none)
	uintptr_t Game(uintptr_t steam);

	// Guarded copies from and to the game's memory: false when it can't be read or written
	bool Read(const void* at, void* out, size_t size);
	bool Write(void* at, const void* value, size_t size);
	template<typename T> bool Read(uintptr_t at, T& out) { return at && Read(reinterpret_cast<const void*>(at), &out, sizeof(T)); }
	template<typename T> bool Write(uintptr_t at, const T& value) { return at && Write(reinterpret_cast<void*>(at), &value, sizeof(T)); }
	// A pointer read from the game (0 when unreadable or null)
	uintptr_t Pointer(uintptr_t at);
	// Text the game keeps (up to size - 1 characters, cut at its end), "" when unreadable
	std::string Text(uintptr_t at, size_t size = 128);

	// A call instruction in the game's code, turned to one of ours and back (render thread only)
	struct Call
	{
		uint32_t steam = 0;       // where it is in the Steam build
		uint8_t* at = nullptr;    // where it is in this game, once checked
		int32_t own = 0;          // its own distance
		uintptr_t target = 0;     // the game's function it calls
		uintptr_t ours = 0;       // ours, while it is turned
		bool checked = false;
		bool ok = false;          // checked and found as expected
		bool turned = false;      // reaches ours now
	};
	// Checks a call (once): a call instruction, reaching expectedTarget when that is given (a Steam address, looked up in
	// this game). Logs why when it isn't.
	bool Check(Call& call, uintptr_t expectedTarget = 0, const char* what = "");
	// Turns a checked call to ours (to) or back to its own (to = nullptr); false (nothing written) when the call is not as
	// it was left
	bool Turn(Call& call, const void* to);

	// The Lights tab (RENDER_LIGHTS.cpp): how many lights it leaves out by hand now - each muted, and one more while one is
	// played solo (any thread)
	int LightsLeftOut();

	// Prose in a tab (a note, a hint, a legend, a warning), wrapped at the window's right edge instead of running off it:
	// greyed out, or in a colour (render thread, inside a window)
	void Note(const char* format, ...);
	void ColouredNote(const ImVec4& colour, const char* format, ...);

	// An overlay's label over a point in the world, as the debug menu's own labels look (text on a dark backing), kept
	// whole on the screen: nothing when the point is behind the camera or off the screen, and a label that would run off
	// an edge is moved in to it (render thread)
	void ScreenLabel(ImDrawList* drawList, const float world[3], const char* text, ImU32 colour, float yOffsetPixels = 0.0f);
}

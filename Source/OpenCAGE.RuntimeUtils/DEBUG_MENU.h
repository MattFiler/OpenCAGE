#pragma once

#include "DevTools.h"
#include <atomic>
#include <cstdint>
#include <functional>
#include <string>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <imgui.h>

/*
	The debug menu: a window over the game, opened and closed with a key (DebugMenuKey, F7 by default), of debug views and
	switches - the characters and their behaviour trees, senses and paths, rendering switches and the like.

	Each view lives in its own file and adds itself here from a static initializer (AddTab, AddOverlay, and AddHooks for
	any hooks it needs). A tab is drawn inside the window while it is open; an overlay is drawn over the game every frame
	whether the window is open or not (labels over characters, view cones...) - each overlay draws nothing unless switched
	on in its tab. Its switches, values and commands are named so OpenCAGE can drive them over the Live Link.

	While the window is open the keyboard and mouse are its: the cursor is held free of the game window and shown, and the
	game's own keyboard and mouse input is kept from it, so the player stands still. Escape or the key closes it. Overlays
	keep drawing once it is closed, so the game can be played with them on.

	Everything here runs on the render thread (the overlay drawn at each frame's presentation) except OnWindowMessage, which
	runs on the game window's thread. The characters are read once a frame, when the game has finished updating them and
	before it starts on the next frame, with every read of the game guarded (a level can be unloading).
*/
namespace DEBUG_MENU
{
	using DrawTab = void (*)();
	using DrawOverlay = void (*)(ImDrawList* drawList);

	// Adds a tab to the window (lowest order leftmost), in a group of tabs (a row of groups along the top, each with its row
	// of tabs: Overview, Characters, AI, World, Scripts, Sound, Rendering, Performance, Game, Cinematic, then any other). A
	// tab named again in the same group adds a section to it, drawn after the first (so a view can add to another's tab
	// without touching its file); the same name in another group is a tab of its own. Call from a static initializer in the
	// view's own file.
	bool AddTab(const char* name, int order, DrawTab draw, const char* group = "Debug");

	// Adds something drawn over the game every frame (lowest order first). Call from a static initializer.
	bool AddOverlay(int order, DrawOverlay draw);

	// The game window's messages, before the game has them (window thread): the key; while the window is open, the
	// keyboard and mouse go to it (at its next frame) and are kept from the game, bar key releases, so no key stays down in
	// the game. True when the message is kept, with what to answer it.
	bool OnWindowMessage(HWND window, UINT message, WPARAM wParam, LPARAM lParam, LRESULT& result);

	// Render thread, as the overlay draws: reads this frame's characters and camera, draws the overlays, then the window.
	void Draw();

	// Render thread, as the overlay draws, in place of Draw while the debug menu is off (DebugMenu=0) but something outside
	// it still uses its world drawing (Project, WorldText...) or its render-thread work - the debug script entities: reads
	// this frame's camera and runs the queued render-thread work. No characters, overlays or window.
	void KeepUpWithoutWindow();

	bool IsOpen();
	// While it is open (any thread; as of the last frame drawn): a text box in it has the keyboard, and the mouse is over it
	// (or dragging something in it). For views that read the keyboard or mouse themselves while the window is open.
	bool TypingInWindow();
	bool MouseOverWindow();

	// Where the window is on screen (as last drawn), false while it is closed: for overlays that keep out of its way.
	bool WindowRect(ImVec2& min, ImVec2& max);

	// The text scale the window uses (bigger on bigger screens), for tabs that open windows of their own.
	float Scale();

	// Notes along the top of the screen (saves kept from the disk, a script error, the scripts held...), stacked so none
	// covers another (render thread only). Each frame the stack starts again, under the Unlock Mouse note while that shows;
	// an overlay's note asks BannerTop where its top goes, giving its height in pixels, and the next note goes under it.
	void ResetBanners(float top);
	float BannerTop(float height);
	// Where the stack of notes ends this frame (where the next would go), 0 while none shows: for text kept clear of them
	// (read after the overlays have drawn).
	float BannersBottom();

	// This frame's camera: game world space (Y up) to overlay pixels, false when behind the camera; where it is, and the
	// way it looks (a unit vector).
	bool Project(const float world[3], ImVec2& screen);
	bool CameraPosition(float out[3]);
	bool CameraForward(float out[3]);

	// World-space drawing helpers on top of Project: a line (cut where it passes behind the camera), a circle on the
	// horizontal plane, and text centred over a point with a dark backing (only for a point on screen, the label kept inside
	// the screen's edges).
	void WorldLine(ImDrawList* drawList, const float a[3], const float b[3], ImU32 colour, float thickness = 1.5f);
	void WorldCircle(ImDrawList* drawList, const float centre[3], float radius, ImU32 colour, int segments = 32, float thickness = 1.5f);
	void WorldText(ImDrawList* drawList, const float world[3], const char* text, ImU32 colour, float yOffsetPixels = 0.0f);
	// A flat convex polygon (cut where it passes behind the camera), filled and/or outlined (a colour of 0 leaves either out);
	// a box along the world axes; a box turned by a matrix (rows 0-2 its axes, row 3 its centre, as the game keeps them) with
	// half its size along each axis; an arrow; a cross of three lines.
	void WorldPolygon(ImDrawList* drawList, const float (*points)[3], int count, ImU32 fill, ImU32 outline, float thickness = 1.5f);
	void WorldBox(ImDrawList* drawList, const float min[3], const float max[3], ImU32 colour, float thickness = 1.5f);
	void WorldOrientedBox(ImDrawList* drawList, const float matrix[16], const float halfSize[3], ImU32 colour, float thickness = 1.5f);
	void WorldArrow(ImDrawList* drawList, const float from[3], const float to[3], ImU32 colour, float thickness = 1.5f);
	void WorldCross(ImDrawList* drawList, const float at[3], float size, ImU32 colour, float thickness = 1.5f);

	// The characters the game runs this frame (the player first), read once a frame. Only use object within the frame,
	// and read it guarded.
	struct Character
	{
		const uint8_t* object = nullptr; // the game's character
		uint32_t id = 0;                 // its unique id, the same for as long as it lives
		std::string name;
		int characterClass = -1;         // see ClassName
		float position[3] = {};
		float facing = 0.0f;             // yaw in radians: forward = (sin, 0, cos)
		bool player = false;
		bool dead = false;
	};
	const std::vector<Character>& Characters();
	const char* ClassName(int characterClass);

	// The character chosen in the window (0: none), by unique id: the tabs show it in detail.
	uint32_t SelectedId();
	void Select(uint32_t id);
	const Character* Selected();
	// A drop-down to choose the selected character, for tabs that show one; false when there are none.
	bool CharacterPicker(const char* label);

	// Work for the game's own threads, queued from the window (render thread) and run there once each, in order: the
	// entity thread, between frames of the level's scripts (where checkpoints and the like are safe to touch), and the AI
	// thread, before the characters' behaviour is updated (where the characters' own state is safe to change).
	using Work = std::function<void()>;
	void OnEntityThread(Work work);
	void OnAiThread(Work work);

	// Called by the hooks on those threads: run what is queued (returning at once when nothing is).
	void RunEntityThreadWork();
	void RunAiThreadWork();

	// Switches the tabs show (overlays on or off, the trees' debug switches...) named so they can also be set from
	// outside the window - OpenCAGE over the live link, for its own tools and for testing. A tab names each of its
	// switches once (any thread), e.g. "characters.view_cones", and keeps reading and writing the flag itself. Switches,
	// values and commands may be named from a static initializer too (their lists are made on first use, and the lock that
	// guards them needs no construction), so they exist before the first frame is drawn. A switch can name a function told
	// of each SetSwitch on it just before the flag takes the value (any thread, under the menu's lock: it must be quick and
	// call nothing here), for switches that need to know which of them was set last.
	using SwitchSetter = void (*)(bool value);
	void AddSwitch(const std::string& name, std::atomic<bool>* value, SwitchSetter beforeSet = nullptr);
	bool SetSwitch(const std::string& name, bool value);
	// What a switch holds (false when there is none of that name), for a view that drives another's switch.
	bool GetSwitch(const std::string& name, bool& value);

	// Named numbers the same way (game speed, a light's brightness...), kept between min and max when set from outside.
	void AddValue(const std::string& name, std::atomic<float>* value, float min, float max);
	bool SetValue(const std::string& name, float value);
	bool GetValue(const std::string& name, float& value);

	// Named commands: what a button does (give an item, jump to a checkpoint...) or what a view can report (a list, the
	// thing under the crosshair...), for OpenCAGE and for testing. Called on the live link's socket thread with the text
	// OpenCAGE sent: queue game work onto the game's threads (OnEntityThread, OnAiThread, or the Answer helpers below) and
	// say so, or read what is safe to read from any thread. What it returns is the reply; ok = false makes it an error.
	struct CommandResult
	{
		bool ok = true;
		std::string message;
	};
	using Command = std::function<CommandResult(const std::string& argument)>;
	void AddCommand(const std::string& name, const char* help, Command command);
	// Runs it ("help" lists them all with their help); false when there is none of that name.
	bool RunCommand(const std::string& name, const std::string& argument, CommandResult& result);

	// For a command that needs a game thread (or this frame's characters and camera, on the render thread): runs the work
	// there and waits for its answer, up to timeoutMs - no level may be running, or the game paused, and the work then still
	// runs when that thread next comes round; the answer says it timed out. A thread that hasn't come round for a while (a
	// level loading, the game paused) is waited for a moment only: a wait holds up the Live Link requests behind it, so full
	// waits for a thread that isn't running would add up while polls keep coming. So work may run after the level has restarted
	// or changed: work that names a character by its unique id then finds nothing, as the game never gives an id out twice,
	// and does no harm. Work that acts on the player should carry the player's id for the same reason, rather than look the
	// player up when it runs (which would find the new level's player).
	using Answer = std::function<CommandResult()>;
	CommandResult AnswerOnEntityThread(Answer work, int timeoutMs = 2000);
	CommandResult AnswerOnAiThread(Answer work, int timeoutMs = 2000);
	CommandResult AnswerOnRenderThread(Answer work, int timeoutMs = 2000);
	// Work for the render thread, run at the start of the next frame's draw (after the characters and camera are read).
	void OnRenderThread(Work work);

	// What the camera is looking at: the view that can tell (the inspector's crosshair ray) provides it, others ask for it
	// (render thread; the latest result, which may lag a frame). False when nothing provides it or nothing was hit.
	struct PickResult
	{
		bool hit = false;
		float position[3] = {};          // the point hit, game world space
		float normal[3] = {};
		float distance = 0.0f;           // from the camera
		uint32_t entityGuid = 0;         // the script entity it belongs to, when known (0: none): its id and the
		uint32_t instanceGuid = 0;       // composite instance it is in, as the game's own entity handles hold them
		int collisionRow = -1;           // its row of the level's collision map, when it is level geometry (-1: none)
		uint32_t zoneId = 0;             // the zone that row puts it in
		char description[160] = {};      // a line saying what it is, for showing
	};
	using PickProvider = bool (*)(PickResult& out);
	void SetPickProvider(PickProvider provider);
	bool Pick(PickResult& out);

	// Hooks a view needs (detours, call-site patches), attached and detached with the rest when the debug menu is on: each
	// view adds its function from a static initializer, and the DLL calls AttachHooks once when it attaches and once when it
	// detaches.
	using HookAttacher = void (*)(bool attach);
	bool AddHooks(HookAttacher attacher);
	void AttachHooks(bool attach);

	// From outside the window (any thread): open or close it, bring a tab to the front by name (the tab of that name, else
	// the first whose name contains it, else the first group whose name does, ignoring case), select a character by name (at
	// the next frame: the first whose name contains it, ignoring case, else the first whose class does, or none).
	void Show(bool open);
	void ShowTab(const std::string& name);
	void SelectByName(const std::string& name);

	// What the window holds, for the live link: open or not, the group and tab in front, the selected character, the tabs,
	// the groups, the commands ("commands=name,name..."), the values ("values=name:1.25,...") and every switch as name=0|1,
	// a line each.
	std::string Describe();
}

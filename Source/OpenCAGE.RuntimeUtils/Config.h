#pragma once

#include <string>

/*
	Runtime settings, read once from OpenCAGE.RuntimeUtils.ini in the game folder (next to AI.exe). Every key is
	optional; missing keys keep their defaults. Example:

	  [RuntimeUtils]
	  HotReload=1                 ; 1/0 - reload the current level on a key press
	  HotReloadKey=INSERT         ; a key name (INSERT, DELETE, HOME, END, PAGEUP, PAGEDOWN, F1..F12, A..Z, 0..9)
	                              ; or a Windows virtual-key code, decimal or hex (e.g. 0x2D)
	  UnlockMouse=0               ; 1/0 - the game leaves the cursor free until the key below is pressed in its window
	  UnlockMouseKey=F4           ; the key that hands the mouse to the game and frees it again (named as HotReloadKey)
	  CapFps=0                    ; the most frames a second the game runs at (30, 60, 90, 120...); 0 = no cap
	  DebugText=1               ; 1/0 - DebugText script entities draw their text on screen
	  DebugTextStacking=1         ; 1/0 - DebugTextStacking script entities draw their text on screen
	  DebugEnvironmentMarker=1    ; 1/0 - DebugEnvironmentMarker script entities draw their text at a world position
	  DebugPositionMarker=1       ; 1/0 - DebugPositionMarker script entities draw axes at a world position
	  DebugGraph=0                ; 1/0 - DebugGraph script entities plot their inputs over the game (see SCRIPT_ENTITIES.cpp)
	  DebugMenuToggle=0           ; 1/0 - DebugMenuToggle script entities set debug menu switches (with DebugMenu=1)
	  DebugObjectMarker=0         ; 1/0 - DebugObjectMarker script entities name their marked object over the game
	  DebugCaptureScreenShot=0    ; 1/0 - DebugCaptureScreenShot script entities save a screenshot when triggered
	  ScriptErrors=0              ; 1/0 - note the game's script errors and failed checks even without the debug menu, for
	                              ; OpenCAGE's Error Log (Live Link's ERRORS_GET); the debug menu notes them anyway
	  LoadAllZones=0             ; 1/0 - stream every zone of the level in, instead of only those around the player
	  LiveLink=1                  ; 1/0 - let OpenCAGE edit the running level's scripting and call entity methods
	  LiveLinkPort=8765           ; the local port OpenCAGE connects to
	  LiveLinkCamera=1            ; 1/0 - let OpenCAGE's viewport camera drive the game camera when it asks (camera sync)
	  LiveLinkLabel=1             ; 1/0 - "OpenCAGE Live Link" in the top right while OpenCAGE is connected (OpenCAGE's Options >
	                              ; Misc > Hide Live Link Label In-Game; it changes it over Live Link too, see OVERLAY)
	  Possess=0                   ; 1/0 - Possess Characters: the key below opens a menu to take a character (the alien...) and move it, see POSSESS.h
	  PossessKey=F6               ; the key that opens and closes that menu (named as HotReloadKey)
	  DebugMenu=0                 ; 1/0 - the debug menu: the key below opens a window of AI and other debug views, see DEBUG_MENU.h
	  DebugMenuKey=F7             ; the key that opens and closes it (named as HotReloadKey)
*/
namespace Config
{
	struct Settings
	{
		bool hotReload = true;
		int hotReloadKey = 0x2D; // the Insert key
		bool unlockMouse = false;
		int unlockMouseKey = 0x73; // the F4 key
		int capFps = 0; // 0 = no cap
		bool debugText = true;
		bool debugTextStacking = true;
		bool debugEnvironmentMarker = true;
		bool debugPositionMarker = true;
		bool debugGraph = false;
		bool debugMenuToggle = false;
		bool debugObjectMarker = false;
		bool debugCaptureScreenShot = false;
		bool scriptErrors = false;
		bool loadAllZones = false;
		bool liveLink = true;
		int liveLinkPort = 8765;
		bool liveLinkCamera = true;
		bool liveLinkLabel = true;
		bool possess = false;
		int possessKey = 0x75; // the F6 key
		bool debugMenu = false;
		int debugMenuKey = 0x76; // the F7 key
	};

	// Loads the settings on first use.
	const Settings& Get();

	// Whether any of the debug entity helpers is enabled.
	bool AnyDebug();

	// Whether any of the debug script entities SCRIPT_ENTITIES makes work again is enabled (DebugGraph, DebugMenuToggle,
	// DebugObjectMarker, DebugCaptureScreenShot): they run with or without the debug menu.
	bool AnyScriptEntity();

	// Human-readable name of a virtual-key code, for logging.
	const char* KeyName(int virtualKey);

	// Any other key of the [RuntimeUtils] section, read when asked (for the debug menu's views, which each name their own
	// keys, e.g. DontWriteSaves=1): the number or text there, or the fallback when it is missing. A key name is parsed as
	// HotReloadKey's are.
	int IniInt(const char* key, int fallback);
	std::string IniString(const char* key, const char* fallback);
	int IniKey(const char* key, int fallback);
}

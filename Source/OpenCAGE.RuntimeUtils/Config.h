#pragma once

/*
	Runtime settings, read once from OpenCAGE.RuntimeUtils.ini in the game folder (next to AI.exe). Every key is
	optional; missing keys keep their defaults. Example:

	  [RuntimeUtils]
	  HotReload=1                 ; 1/0 - reload the current level on a key press
	  HotReloadKey=INSERT         ; a key name (INSERT, DELETE, HOME, END, PAGEUP, PAGEDOWN, F1..F12, A..Z, 0..9)
	                              ; or a Windows virtual-key code, decimal or hex (e.g. 0x2D)
	  DebugText=1                 ; 1/0 - DebugText script entities draw their text on screen
	  DebugTextStacking=1         ; 1/0 - DebugTextStacking script entities draw their text on screen
	  DebugEnvironmentMarker=1    ; 1/0 - DebugEnvironmentMarker script entities draw their text at a world position
	  DebugPositionMarker=1       ; 1/0 - DebugPositionMarker script entities draw axes at a world position
	  LoadAllZones=0              ; 1/0 - stream every zone of the level in, instead of only those around the player
	  LiveLink=1                  ; 1/0 - let OpenCAGE edit the running level's scripting and call entity methods
	  LiveLinkPort=8765           ; the local port OpenCAGE connects to
	  LiveLinkCamera=1            ; 1/0 - let OpenCAGE's viewport camera drive the game camera when it asks (camera sync)
*/
namespace Config
{
	struct Settings
	{
		bool hotReload = true;
		int hotReloadKey = 0x2D; // the Insert key
		bool debugText = true;
		bool debugTextStacking = true;
		bool debugEnvironmentMarker = true;
		bool debugPositionMarker = true;
		bool loadAllZones = false;
		bool liveLink = true;
		int liveLinkPort = 8765;
		bool liveLinkCamera = true;
	};

	// Loads the settings on first use.
	const Settings& Get();

	// Whether any of the debug entity helpers is enabled.
	bool AnyDebug();

	// Human-readable name of a virtual-key code, for logging.
	const char* KeyName(int virtualKey);
}

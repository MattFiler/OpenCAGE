#pragma once

#include "DevTools.h"

#include <cstdint>
#include <string>

/*
	Don't write saves: the debug menu's Game > Saves tab switch ("game.dont_write_saves", on from the start when the ini
	has DontWriteSaves=1) that keeps the game from writing anything of the player's - checkpoints and mission saves, the
	progression file (which holds the game settings too), the input settings and the graphics settings - while loading
	keeps working. A modded level, a debug checkpoint or a cheat can then be tried without touching a real campaign.

	It works at four levels, each catching what the one above lets through:
	- the game's own save calls for the progression, the game settings, the input settings and the graphics settings
	  return at once, as they do when the game's own "saves enabled" is off, and drop the save they had waiting;
	- the save system's queue: a save or a delete it is handed is finished at once as a success, on the frame's own
	  update, without its save thread ever being woken to write it - so the game carries on as if it had saved. Checkpoint
	  and mission saves are kept back here rather than at their call: a checkpoint's "saved to disk" output (which level
	  scripts can wait on) only fires once the save system reports the save done;
	- the Steam Cloud calls that write, delete or forget a file outside that queue (Steam and Epic Games Store builds), and
	  the upload of a leaderboard's file, return at once;
	- the game's own file calls (its imports of the Windows functions that open, delete or move files) refuse to create,
	  write, delete or move a file in a save folder: My Games\Alien Isolation (GOG), The Creative Assembly\Alien Isolation
	  (Epic Games Store) and the game's own Steam user data folder (its Steam Cloud folder, and the graphics settings).
	Each write kept back is counted and noted (what and when) for the tab, the on-screen notice and "game.saves".

	The hooks are added to the debug menu's (attached with them when DebugMenu=1); AttachHooks can also be called on its
	own, once each way, for DontWriteSaves without the debug menu.
*/
namespace GAME_SAVES
{
	// Whether saves are kept from being written now (the switch).
	bool On();

	// Attaches or detaches the hooks (inside the DLL's Detours transaction) and the file guard; does nothing the second
	// time in the same direction. Reads DontWriteSaves from the ini as it attaches.
	void AttachHooks(bool attach);

	// What has been kept back so far: how many, and a line for the last one (empty when none).
	uint32_t SkippedCount();
	std::string LastSkipped();

	// Hooked: the game's save calls for the progression, the game settings, the input settings and the graphics settings.
	// Each returns at once while saves are kept back.
	DEVTOOLS_DECLARE_CLASS_HOOK(void, save_progression, h_save_progression, t_save_progression, 0x003A8D30)
	DEVTOOLS_DECLARE_CLASS_HOOK(void, save_settings, h_save_settings, t_save_settings, 0x003A8DD0)
	DEVTOOLS_DECLARE_CLASS_HOOK(void, save_input_cfg, h_save_input_cfg, t_save_input_cfg, 0x0039ACB0)
	DEVTOOLS_DECLARE_CLASS_HOOK(void, save_engine_settings, h_save_engine_settings, t_save_engine_settings, 0x0039B660)

	// Hooked: the save system's per-frame update, which hands a queued save or delete to its save thread.
	DEVTOOLS_DECLARE_CLASS_HOOK(void, platform_update, h_platform_update, t_platform_update, 0x006637C0)

	// Hooked: the Steam Cloud's own writes, deletes and forgets (the Steam and Epic Games Store builds), and the upload of a
	// leaderboard's file.
	DEVTOOLS_DECLARE_CLASS_HOOK(void, cloud_execute_save, h_cloud_execute_save, t_cloud_execute_save, 0x00663BD0)
	DEVTOOLS_DECLARE_CLASS_HOOK(void, cloud_execute_delete, h_cloud_execute_delete, t_cloud_execute_delete, 0x00654D30)
	DEVTOOLS_DECLARE_CLASS_HOOK(void, cloud_immediate_delete, h_cloud_immediate_delete, t_cloud_immediate_delete, 0x00654DB0, const char*)
	DEVTOOLS_DECLARE_CLASS_HOOK(void, cloud_immediate_delete_all, h_cloud_immediate_delete_all, t_cloud_immediate_delete_all, 0x006616F0)
	DEVTOOLS_DECLARE_CLASS_HOOK(void, cloud_immediate_delete_invalid, h_cloud_immediate_delete_invalid, t_cloud_immediate_delete_invalid, 0x00661A20)
	DEVTOOLS_DECLARE_CLASS_HOOK(void, cloud_forget_invalid, h_cloud_forget_invalid, t_cloud_forget_invalid, 0x00661970)
	DEVTOOLS_DECLARE_CLASS_HOOK(void, cloud_delete_these, h_cloud_delete_these, t_cloud_delete_these, 0x0065D5A0, const void*)
	DEVTOOLS_DECLARE_CLASS_HOOK(void, cloud_forget_these, h_cloud_forget_these, t_cloud_forget_these, 0x0065D550, const void*)
	DEVTOOLS_DECLARE_CLASS_HOOK(void, cloud_import_save_file, h_cloud_import_save_file, t_cloud_import_save_file, 0x00654E50, const char*, const char*)
	DEVTOOLS_DECLARE_CLASS_HOOK(void, tus_execute_upload, h_tus_execute_upload, t_tus_execute_upload, 0x006590F0)
}

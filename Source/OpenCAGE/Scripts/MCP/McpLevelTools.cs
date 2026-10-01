using CATHODE;
using CathodeLib;
using Newtonsoft.Json.Linq;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.RuntimeUtilsConnection;
using OpenCAGE.Popups;
using OpenCAGE.Popups.UserControls;
using OpenCAGE.Undo;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// The game and the editor around the level: closing the game, how it is launched (Launch Game window),
    /// which installs OpenCAGE knows (Manage Game Directories), the editor's Options menu, the runtime utils
    /// link to a running game, and (where built in) the mod manager.
    /// </summary>
    internal static class McpLevelTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "close_game",
                Title = "Close the game",
                Description = "Close Alien: Isolation (AI.exe) and Cinematic Tools if they are running, as OpenCAGE does before a save or launch. Unsaved game progress is lost. Changes nothing on disk.",
                Destructive = true,
                Idempotent = true,
                Run = call =>
                {
                    List<string> closed = CloseGame();
                    return new JObject() { ["closed"] = new JArray(closed), ["was_running"] = closed.Count != 0 };
                },
            };

            yield return new McpTool()
            {
                Name = "launch_options",
                Title = "Game launch options",
                Description = "Read or set the Launch Game window's options. Scripting helpers and cinematic_tools are editor settings launch_game applies. AI.exe patches (no_ui...) and UI mods (debug_checkpoints...) are written to the game at once, for every level; not undoable. No option given: report them. dry_run checks without writing.",
                InputSchema = McpSchema.Object(LaunchOptionProps().ToArray()),
                Idempotent = true,
                Run = LaunchOptions,
            };

            yield return new McpTool()
            {
                Name = "game_directories",
                Title = "Game installs",
                Description = "The Alien: Isolation installs OpenCAGE knows (Options > Manage Game Directories): list them, register another (an absolute folder or AI.exe path), or set the default OpenCAGE opens at its next start. Saved in OpenCAGE's settings at once; the install open now does not change.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("action", "list (default), register, or set_default.", options: new[] { "list", "register", "set_default" }),
                    McpSchema.String("path", "register / set_default: the install folder (or its AI.exe), as an absolute path.")),
                Idempotent = true,
                Run = GameDirectories,
            };

            yield return new McpTool()
            {
                Name = "editor_options",
                Title = "Editor options",
                Description = "Read or change the editor's preferences from the Options menu (Composite Display, Entity Display, Misc, Dark Mode, numeric step) through the menu's own setters, or reset the UI layout. Saved in OpenCAGE's settings at once; not undoable; nothing in the game or level changes. No argument: report them.",
                InputSchema = McpSchema.Object(EditorOptionProps().ToArray()),
                Idempotent = true,
                Run = EditorOptions,
            };

            yield return new McpTool()
            {
                Name = "runtime_utils",
                Title = "Live link to the game",
                Description = "The live link to a running game (the toolbar's Live Link button, ws://127.0.0.1:8765, served by the runtime utils ASI when the game is launched with launch_options live_link on - which also connects by itself): status, connect, disconnect; game_status (is a level running, which, where the game camera is and faces - world space, for placing new entities in view - and camera_sync, whether it is following the viewport's camera: set_viewport_view live_link_camera 'viewport_to_game'); push a composite's scripting into the running level (entities added/removed/re-parameterised in every running instance, no reload); call_method on an entity (e.g. start); animate a CAGEAnimation in the running game as Animation Mode's In game does (the game holds it at 'time', or plays it, evaluating every track itself - no level data changes; it keeps it until release, Animation Mode takes the drive over, or OpenCAGE disconnects; get reports it, and game_status too); describe a composite's running instances; screenshot the game; load_level. The game must be running the level open here, as saved - push sends edits made since.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("action", "status (default), connect, disconnect, game_status, push, call_method, animate, describe, screenshot, or load_level.", options: new[] { "status", "connect", "disconnect", "game_status", "push", "call_method", "animate", "describe", "screenshot", "load_level" }),
                    McpSchema.String("level", "load_level: the level, as list_levels names it ('menu' for the frontend)."),
                    McpSchema.String("composite", "push, call_method, animate, describe: the composite (default: the one open in the editor)."),
                    McpSchema.String("entity", "call_method: the entity to call; animate: the CAGEAnimation - in that composite (name or id)."),
                    McpSchema.String("method", "call_method: the method pin to call, e.g. start, stop, trigger."),
                    McpSchema.Strings("instance_path", "call_method, animate: ids of the composite instance entities from the level's root down to the instance to call in (animate: the placement to drive). Default: the path the open composite was reached through from the root, else every running instance of the composite."),
                    McpSchema.Number("time", "animate: seconds into the animation to hold it at, or play it from (the game keeps it just short of its end)."),
                    McpSchema.Boolean("play", "animate: play it from 'time' on the game's own clock instead of holding it."),
                    McpSchema.Boolean("loop", "animate with play: go round again from the start rather than ending."),
                    McpSchema.Boolean("events", "animate with play: run the event tracks the play passes over, which reach the level's scripts (a hold never runs them)."),
                    McpSchema.Boolean("release", "animate: give the game its animation back."),
                    McpSchema.Boolean("get", "animate: only report the drive and what the game does with it."),
                    McpSchema.String("path", "screenshot: an absolute path for the .bmp.")),
                Run = RuntimeUtils,
            };

#if ENABLE_MOD_PACKAGES
            yield return new McpTool()
            {
                Name = "mods",
                Title = "Mod manager",
                Description = "The mod manager: list the library, import an .omp package, remove one, apply (the enabled mods, in priority order - later wins - restoring everything else to its baseline), repair, recover an interrupted apply, or export changed files as an .omp. Game files are rewritten at once; not undoable. dry_run reports first.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("action", "list (default), import, remove, apply, repair, recover, or export.", options: new[] { "list", "import", "remove", "apply", "repair", "recover", "export" }),
                    McpSchema.String("path", "import: the .omp package; export: where to write it. An absolute path."),
                    McpSchema.String("id", "remove: the mod's id (from list)."),
                    McpSchema.Strings("enabled", "apply: the ids of the mods to have enabled, lowest priority first (default: those enabled now)."),
                    McpSchema.Boolean("adopt_current_files", "apply: take files that are neither vanilla nor stored as their restore point instead of refusing."),
                    McpSchema.String("name", "export: the mod's name."),
                    McpSchema.String("author", "export: its author."),
                    McpSchema.String("version", "export: its version (default 1.0)."),
                    McpSchema.String("description", "export: its description."),
                    McpSchema.Strings("levels", "export: only changed files of these levels (as list_levels names them)."),
                    McpSchema.Strings("files", "export: only these changed files, as game-relative paths (e.g. DATA/ENV/PRODUCTION/X/WORLD/COMMANDS.PAK)."),
                    McpSchema.Boolean("include_configs", "export: include changed config (.BML) values (default true)."),
                    McpSchema.Integer("limit", "list/export: at most this many entries in the report (default 100)."),
                    McpSchema.Boolean("dry_run", "Report what would happen, changing nothing.")),
                Destructive = true,
                Run = Mods,
            };
#endif
        }

        #region Game
        /// <summary>Close the game and Cinematic Tools as the Launch Game window does; the names of what was running.</summary>
        internal static List<string> CloseGame()
        {
            List<string> running = new List<string>();
            foreach (string name in new[] { "AI", "CinematicTools", "CinematicToolsInjector" })
                if (ProcessRunning(name))
                    running.Add(name == "AI" ? "AI.exe" : name);
            if (running.Count != 0)
                EditorUtils.CloseAI(new List<string>() { "CinematicTools", "CinematicToolsInjector" });
            return running;
        }

        internal static bool ProcessRunning(string name)
        {
            Process[] found = Process.GetProcessesByName(name);
            foreach (Process process in found) process.Dispose();
            return found.Length != 0;
        }
        #endregion

        #region Launch options
        private sealed class LaunchOption
        {
            public string Arg, Key, Kind, Description;
            //AI.exe patches: writes the patch (true) or the original bytes (false)
            public Func<PatchManager.Platform, string, bool, bool> Patch;
        }

        private const string KindHelper = "helper", KindPatch = "patch", KindUiMod = "ui_mod";

        private static readonly LaunchOption[] LaunchOptionList =
        {
            new LaunchOption() { Arg = "hot_reload", Key = Settings.ScriptingHelpersHotReload, Kind = KindHelper, Description = "Scripting helper: reload the current level in game with a key press." },
            new LaunchOption() { Arg = "debug_text", Key = Settings.ScriptingHelpersDebugText, Kind = KindHelper, Description = "Scripting helper: DebugText entities draw their text in game." },
            new LaunchOption() { Arg = "debug_text_stacking", Key = Settings.ScriptingHelpersDebugTextStacking, Kind = KindHelper, Description = "Scripting helper: DebugTextStacking entities draw their text in game." },
            new LaunchOption() { Arg = "debug_environment_marker", Key = Settings.ScriptingHelpersDebugEnvironmentMarker, Kind = KindHelper, Description = "Scripting helper: DebugEnvironmentMarker entities draw their text in the world." },
            new LaunchOption() { Arg = "debug_position_marker", Key = Settings.ScriptingHelpersDebugPositionMarker, Kind = KindHelper, Description = "Scripting helper: DebugPositionMarker entities draw axes in the world." },
            new LaunchOption() { Arg = "live_link", Key = Settings.ScriptingHelpersLiveLink, Kind = KindHelper, Description = "Scripting helper (Enable Live Link): the game serves the live link, and OpenCAGE connects to it by itself once the game is up (runtime_utils) - script edits reach the running level without a reload, entity methods can be called, and the viewport's camera and the game's can follow each other (set_viewport_view live_link_camera)." },
            new LaunchOption() { Arg = "cinematic_tools", Key = Settings.CinematicTools, Kind = KindHelper, Description = "Start Cinematic Tools (free camera) with the game. Steam only." },
            new LaunchOption() { Arg = "no_ui", Key = Settings.HudDisabled, Kind = KindPatch, Patch = PatchManager.PatchNoUIFlag, Description = "AI.exe patch: no HUD or menus (clean screenshots)." },
            new LaunchOption() { Arg = "skip_frontend", Key = Settings.SkipFrontend, Kind = KindPatch, Patch = PatchManager.PatchSkipFrontendFlag, Description = "AI.exe patch: skip the frontend (returning to the menu then misbehaves)." },
            new LaunchOption() { Arg = "ui_memory_overlay", Key = Settings.UiEnabledUiPerf, Kind = KindPatch, Patch = PatchManager.PatchUIPerfFlag, Description = "AI.exe patch: the UI memory overlay." },
            new LaunchOption() { Arg = "memory_logging", Key = Settings.MemReplayLogs, Kind = KindPatch, Patch = PatchManager.PatchMemReplayLogFlag, Description = "AI.exe patch: memory replay logging." },
            new LaunchOption() { Arg = "patch_current_gen_optimisations", Key = Settings.PatchCurrentGen, Kind = KindPatch, Patch = PatchManager.DisableCurrentGenOptimisations, Description = "AI.exe patch: disable the current-gen script optimisations." },
            new LaunchOption() { Arg = "render_constant_ambient", Key = Settings.RenderConstantAmbient, Kind = KindPatch, Patch = PatchManager.PatchRenderConstantAmbientFlag, Description = "AI.exe patch: render a constant ambient light." },
            new LaunchOption() { Arg = "debug_checkpoints", Key = Settings.UiModPauseMenu, Kind = KindUiMod, Description = "UI mod (UI.PAK): debug checkpoints in the pause menu." },
            new LaunchOption() { Arg = "debug_loadscreen", Key = Settings.UiModLoadingScreen, Kind = KindUiMod, Description = "UI mod (UI.PAK): the debug loading screen (shows the level name)." },
            new LaunchOption() { Arg = "level_selection", Key = Settings.UiModNewFrontendMenu, Kind = KindUiMod, Description = "UI mod (UI.PAK): level selection in the main menu, custom levels included." },
            new LaunchOption() { Arg = "quit_to_menu_on_death", Key = Settings.UiModGameOverMenu, Kind = KindUiMod, Description = "UI mod (UI.PAK): a Quit To Menu option on the game-over screen." },
        };

        private static IEnumerable<McpSchema.Prop> LaunchOptionProps()
        {
            foreach (LaunchOption option in LaunchOptionList)
            {
                yield return McpSchema.Boolean(option.Arg, option.Description);
                if (option.Arg == "hot_reload")
                    yield return McpSchema.String("hot_reload_key", "Scripting helper: the hot reload key.", options: global::OpenCAGE.LaunchGame.HotReloadKeys);
            }
            yield return McpSchema.Boolean("close_game", "If the game is running, close it first so AI.exe / UI.PAK can be written (default false: refuse while it runs).");
            yield return McpSchema.Boolean("dry_run", "Check and report what would change, writing nothing.");
        }

        private static JObject LaunchOptionState()
        {
            JObject state = new JObject();
            foreach (LaunchOption option in LaunchOptionList)
            {
                state[option.Arg] = SettingsManager.GetBool(option.Key);
                if (option.Arg == "hot_reload")
                    state["hot_reload_key"] = global::OpenCAGE.LaunchGame.HotReloadKeys[global::OpenCAGE.LaunchGame.HotReloadKeyIndex(SettingsManager.GetString(Settings.ScriptingHelpersHotReloadKey, global::OpenCAGE.LaunchGame.DefaultHotReloadKey))];
            }
            return state;
        }

        private static object LaunchOptions(McpCall call)
        {
            bool dryRun = call.Bool("dry_run");
            PatchManager.Platform platform = Singleton.Platform;
            JObject before = LaunchOptionState();

            //What was asked for, checked before anything is written
            List<KeyValuePair<LaunchOption, bool>> asked = new List<KeyValuePair<LaunchOption, bool>>();
            foreach (LaunchOption option in LaunchOptionList)
                if (call.Has(option.Arg))
                    asked.Add(new KeyValuePair<LaunchOption, bool>(option, call.Bool(option.Arg)));
            string hotReloadKey = null;
            if (call.Has("hot_reload_key"))
            {
                string wanted = call.Str("hot_reload_key").Trim();
                hotReloadKey = global::OpenCAGE.LaunchGame.HotReloadKeys.FirstOrDefault(o => string.Equals(o, wanted, StringComparison.OrdinalIgnoreCase))
                    ?? throw new McpError("'" + wanted + "' is not a hot reload key. It takes: " + string.Join(", ", global::OpenCAGE.LaunchGame.HotReloadKeys) + ".");
            }

            JObject result = new JObject();
            if (asked.Count == 0 && hotReloadKey == null)
            {
                result["options"] = before;
                result["platform"] = platform.ToString();
                result["scripting_helpers_available"] = global::OpenCAGE.LaunchGame.ScriptingHelpersAvailable();
                result["cinematic_tools_available"] = global::OpenCAGE.LaunchGame.CinematicToolsAvailable();
                if (call.Has("close_game"))
                    call.Note("close_game only matters when an AI.exe patch or UI mod is being changed.");
                return result;
            }

            //AI.exe patches are re-written whenever given (cheap, and it repairs an exe that verifying the game reset);
            //the UI mods rewrite all of UI.PAK, so only when they change. Settings only when they change.
            List<KeyValuePair<LaunchOption, bool>> patches = asked.Where(o => o.Key.Kind == KindPatch).ToList();
            List<KeyValuePair<LaunchOption, bool>> uiMods = asked.Where(o => o.Key.Kind == KindUiMod && SettingsManager.GetBool(o.Key.Key) != o.Value).ToList();
            List<KeyValuePair<LaunchOption, bool>> helpers = asked.Where(o => o.Key.Kind == KindHelper && SettingsManager.GetBool(o.Key.Key) != o.Value).ToList();
            bool keyChanges = hotReloadKey != null && hotReloadKey != (string)before["hot_reload_key"];

            if (patches.Count != 0 && platform != PatchManager.Platform.STEAM && platform != PatchManager.Platform.EPIC_GAMES_STORE && platform != PatchManager.Platform.GOG)
                throw new McpError("AI.exe patches exist only for the Steam, Epic and GOG builds (this install is " + platform + "). Nothing was changed.");
            string uiPak = Path.Combine(Singleton.PathToAI, "DATA", "UI.PAK");
            if (uiMods.Count != 0 && !File.Exists(uiPak))
                throw new McpError("DATA/UI.PAK is missing from this install, so the UI mods cannot be applied. Nothing was changed.");
            if (asked.Any(o => (o.Key.Arg == "cinematic_tools" && o.Value && !global::OpenCAGE.LaunchGame.CinematicToolsAvailable())))
                call.Note("Cinematic Tools only work with the Steam build and a complete OpenCAGE install: the setting is kept, but launch_game will not start them here.");
            if ((helpers.Any(o => o.Key.Arg != "cinematic_tools" && o.Value) || keyChanges) && !global::OpenCAGE.LaunchGame.ScriptingHelpersAvailable())
                call.Note("The scripting helpers only work with the Steam build (and OpenCAGE's runtimeutils folder): the settings are kept, but launch_game will not install them here.");

            JArray changes = new JArray();
            foreach (var change in helpers) changes.Add(new JObject() { ["option"] = change.Key.Arg, ["to"] = change.Value, ["writes"] = "OpenCAGE settings (applied at launch_game)" });
            if (keyChanges) changes.Add(new JObject() { ["option"] = "hot_reload_key", ["to"] = hotReloadKey, ["writes"] = "OpenCAGE settings (applied at launch_game)" });
            foreach (var change in patches) changes.Add(new JObject() { ["option"] = change.Key.Arg, ["to"] = change.Value, ["writes"] = "AI.exe" });
            foreach (var change in uiMods) changes.Add(new JObject() { ["option"] = change.Key.Arg, ["to"] = change.Value, ["writes"] = "DATA/UI.PAK" });

            bool writesGame = patches.Count != 0 || uiMods.Count != 0;
            bool running = ProcessRunning("AI");
            if (dryRun)
            {
                result["dry_run"] = true;
                result["would_change"] = changes;
                result["options"] = before;
                if (writesGame && running)
                    call.Note("The game is running: a real call needs close_game: true (or the game closed) to write AI.exe / UI.PAK.");
                return result;
            }

            if (writesGame)
            {
                if (running && !call.Bool("close_game"))
                    throw new McpError("Alien: Isolation is running, and AI.exe / UI.PAK cannot be written while it is. Pass close_game: true to close it first, or close it yourself. Nothing was changed.");
                //A save's release-build tail re-applies these same patches to AI.exe: never write alongside it
                McpEditor.UI(() =>
                {
                    if (UndoStack.Current.Blocked)
                        throw new McpError("OpenCAGE is saving or writing a level, which also patches AI.exe. Try again when it has finished. Nothing was changed.");
                });
                if (running)
                {
                    List<string> closed = CloseGame();
                    if (closed.Count != 0) call.Note("Closed the running game (" + string.Join(", ", closed) + ") to write the game files.");
                }
            }

            //The Launch Game window only follows settings edited outside OpenCAGE: if it is open, it keeps showing the old ones
            bool launchWindowOpen = false;
            try { launchWindowOpen = McpEditor.UI(() => Application.OpenForms.OfType<global::OpenCAGE.LaunchGame>().Any()); }
            catch (McpError) { }

            List<string> failures = new List<string>();
            foreach (var change in helpers)
                SettingsManager.SetBool(change.Key.Key, change.Value);
            if (keyChanges)
                SettingsManager.SetString(Settings.ScriptingHelpersHotReloadKey, hotReloadKey);
            foreach (var change in patches)
            {
                //As the Launch Game window: the setting is kept even if the write fails (a later launch or save re-applies it)
                SettingsManager.SetBool(change.Key.Key, change.Value);
                if (!change.Key.Patch(platform, Singleton.PathToAI, change.Value))
                    failures.Add(change.Key.Arg + ": AI.exe could not be written (is the game still open, or the file read-only?)");
            }
            if (uiMods.Count != 0)
            {
                using (McpEditorTools.Heartbeat(call, "Writing DATA/UI.PAK"))
                {
                    try
                    {
                        PAK2 pak = new PAK2(uiPak);
                        foreach (var change in uiMods)
                        {
                            if (!pak.Entries.Any(o => o.Filename == "DATA/UI/" + change.Key.Key + ".GFX"))
                            {
                                failures.Add(change.Key.Arg + ": UI.PAK has no DATA/UI/" + change.Key.Key + ".GFX to replace (verify the game files)");
                                continue;
                            }
                            global::OpenCAGE.LaunchGame.ApplyUIMod(pak, change.Key.Key, change.Value);
                        }
                    }
                    catch (Exception e)
                    {
                        failures.Add("UI.PAK could not be written: " + e.Message);
                    }
                }
            }

            if (launchWindowOpen)
                call.Note("The Launch Game window is open and still shows the old options: close and reopen it before using it.");
            result["changed"] = changes;
            result["options"] = LaunchOptionState();
            if (failures.Count != 0)
                result["failed"] = new JArray(failures);
            if (helpers.Count != 0 || keyChanges)
                call.Note("Scripting helper and Cinematic Tools settings take effect at the next launch_game.");
            return result;
        }
        #endregion

        #region Game directories
        private static object GameDirectories(McpCall call)
        {
            string action = (call.Str("action") ?? "list").Trim().ToLowerInvariant();
            if (action != "list" && action != "register" && action != "set_default")
                throw new McpError("'action' must be list, register or set_default.");
            if (action == "list")
            {
                if (call.Has("path"))
                    throw new McpError("'path' is for register and set_default.");
                return ListDirectories(null);
            }

            string path = InstallFolder(call.Str("path", required: true));
            List<string> directories = GameDirectoryManager.RegisteredDirectories();
            string existing = directories.FirstOrDefault(o => SamePath(o, path));
            JObject result;
            if (action == "register")
            {
                if (existing != null)
                {
                    //The install open now is listed even when it was never saved (started with -pathToAI): registering saves it
                    bool saved = SettingsManager.GetStringArray(Settings.GameDirectories).Any(o => SamePath(o, existing));
                    if (!saved)
                        SettingsManager.SetStringArray(Settings.GameDirectories, directories.ToArray());
                    result = ListDirectories(existing);
                    result["registered"] = existing;
                    if (saved)
                        call.Note("That install was already registered.");
                    return result;
                }
                directories.Add(path);
                SettingsManager.SetStringArray(Settings.GameDirectories, directories.ToArray());
                result = ListDirectories(path);
                result["registered"] = path;
                return result;
            }

            //set_default: first in the list is the one OpenCAGE opens (registering it if need be)
            string chosen = existing ?? path;
            directories.RemoveAll(o => SamePath(o, chosen));
            directories.Insert(0, chosen);
            SettingsManager.SetStringArray(Settings.GameDirectories, directories.ToArray());
            result = ListDirectories(chosen);
            result["default"] = chosen;
            if (!SamePath(chosen, Singleton.PathToAI))
                call.Note("OpenCAGE opens this install the next time it starts (unless started with -pathToAI); the one open now is unchanged.");
            return result;
        }

        private static JObject ListDirectories(string highlight)
        {
            List<string> directories = GameDirectoryManager.RegisteredDirectories();
            JArray installs = new JArray();
            for (int i = 0; i < directories.Count; i++)
            {
                string path = directories[i];
                installs.Add(new JObject()
                {
                    ["path"] = path,
                    ["platform"] = PatchManager.GetPlatform(path).ToString(),
                    ["default"] = i == 0,
                    ["open_now"] = SamePath(path, Singleton.PathToAI),
                    ["editor_instance_open"] = ChildInstanceManager.GetProcess(path) != null,
                });
            }
            return new JObject() { ["installs"] = installs };
        }

        /// <summary>An install folder from an absolute folder or AI.exe path, checked as the Register New picker checks it.</summary>
        private static string InstallFolder(string given)
        {
            string path = given.Trim().Trim('"');
            if (!IsAbsolute(path))
                throw new McpError("'path' must be an absolute path (e.g. 'C:\\Program Files (x86)\\Steam\\steamapps\\common\\Alien Isolation'), not '" + given + "'.");
            string full;
            try { full = Path.GetFullPath(path); }
            catch (Exception e) { throw new McpError("'" + given + "' is not a usable path (" + e.Message + ")."); }
            if (File.Exists(full) && string.Equals(Path.GetExtension(full), ".exe", StringComparison.OrdinalIgnoreCase))
                full = Path.GetDirectoryName(full);
            full = full.TrimEnd('\\', '/');
            if (!Directory.Exists(full))
                throw new McpError("There is no folder '" + full + "'.");
            if (!Utilities.IsGameDirectoryValid(full))
                throw new McpError("'" + full + "' is not an Alien: Isolation install (it needs DATA/ENV, DATA/TASKS.TXT, DATA/GBL_ITEM.BML and DATA/PACKAGES/MAIN.PKG). Give the folder AI.exe is in.");
            return full;
        }

        /// <summary>A full path: a drive and a root ("C:\..."), or a network share - not relative, and not "C:folder" or "\folder".</summary>
        private static bool IsAbsolute(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (path.StartsWith("\\\\") || path.StartsWith("//")) return true;
            return path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' && (path[2] == '\\' || path[2] == '/');
        }

        private static bool SamePath(string a, string b)
        {
            if (a == null || b == null) return false;
            try { return string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }
        #endregion

        #region Editor options
        private sealed class BoolOption
        {
            public string Arg, Key, Description;
        }

        private static readonly BoolOption[] BoolOptions =
        {
            new BoolOption() { Arg = "search_only_composite_names", Key = Settings.CompNameOnlyOpt, Description = "Composite Display > Search Only Composite Names: browser searches match names, not whole paths." },
            new BoolOption() { Arg = "composite_previews_in_trees", Key = Settings.CompositePreviewsInTrees, Description = "Composite Display > Show Composite Previews In Tree Views." },
            new BoolOption() { Arg = "show_short_guids", Key = Settings.ShowShortGuids, Description = "Entity Display > Show ShortGuids: ids beside entity names in the editor's lists." },
            new BoolOption() { Arg = "populate_all_pins_on_create", Key = Settings.PopulateAllPinsOnCreateNode, Description = "Entity Display > Populate All Node Pins When Created." },
            new BoolOption() { Arg = "focus_canvas_on_new_node", Key = Settings.FocusCanvasOnNewNode, Description = "Entity Display > Focus Canvas On Newly Created Node." },
            new BoolOption() { Arg = "confirm_node_delete", Key = Settings.AskBeforeDeletingNode, Description = "Entity Display > Show Confirmation When Deleting Node." },
            new BoolOption() { Arg = "confirm_on_save", Key = Settings.ShowSavedMsgOpt, Description = "Misc > Show Confirmation When Saving (save_level never shows it)." },
            new BoolOption() { Arg = "prompt_save_on_close", Key = Settings.PromptSaveOnClose, Description = "Misc > Prompt to Save on Close." },
            new BoolOption() { Arg = "textured_model_view", Key = Settings.ShowTexOpt, Description = "Misc > Use Textured Model View." },
            new BoolOption() { Arg = "keep_uses_window_open", Key = Settings.KeepUsesWindowOpen, Description = "Misc > Keep Global Search Window Open." },
            new BoolOption() { Arg = "open_game_on_save", Key = Settings.LaunchGameWhenSaved, Description = "Misc > Open Game On Save (release builds; save_level never launches the game)." },
            new BoolOption() { Arg = "show_game_platform", Key = Settings.ShowGamePlatform, Description = "Misc > Show Game Platform in the title bar." },
            new BoolOption() { Arg = "reset_render_filters_on_load", Key = Settings.ResetRenderFilters, Description = "Misc > Reset Render Filters On Load: viewport filters go back to default at each level load." },
        };

        private static readonly Dictionary<string, CompositeBrowserMode> BrowserModes = new Dictionary<string, CompositeBrowserMode>()
        {
            ["tree_only"] = CompositeBrowserMode.TreeOnly,
            ["tree_and_browser"] = CompositeBrowserMode.TreeAndBrowser,
            ["tree_and_preview"] = CompositeBrowserMode.TreeAndPreview,
        };

        private static readonly int[] PreviewScales = { 50, 75, 100, 150, 200 };

        private static IEnumerable<McpSchema.Prop> EditorOptionProps()
        {
            yield return McpSchema.String("composite_browser_mode", "Composite Display > Composite Browser Mode.", options: BrowserModes.Keys);
            yield return McpSchema.Integer("composite_preview_scale", "Composite Display > Composite Preview Scale, in percent: 50, 75, 100, 150 or 200.");
            foreach (BoolOption option in BoolOptions)
                yield return McpSchema.Boolean(option.Arg, option.Description);
            yield return McpSchema.Boolean("dark_mode", "Options > Dark Mode. The docked panels are rebuilt in place; the level stays open.");
            yield return McpSchema.Number("numeric_step", "Set Numeric Step: the inspector's spinner step for positions (more than 0, at most 100).");
            yield return McpSchema.Number("numeric_step_rotation", "Set Numeric Step: the inspector's spinner step for rotations, in degrees (more than 0, at most 100).");
            yield return McpSchema.Boolean("reset_ui_layouts", "true: Misc > Reset UI Layouts - window size and docked panel sizes and places back to default.");
        }

        private static JObject EditorOptionState()
        {
            JObject state = new JObject()
            {
                ["composite_browser_mode"] = BrowserModes.First(o => o.Value == CompositeBrowserModes.Current).Key,
                ["composite_preview_scale"] = SettingsManager.GetInteger(Settings.CompositePreviewScale, 100),
            };
            foreach (BoolOption option in BoolOptions)
                state[option.Arg] = SettingsManager.GetBool(option.Key);
            state["dark_mode"] = Theming.ThemeManager.IsDark;
            state["numeric_step"] = Math.Round(SettingsManager.GetFloat(Settings.NumericStep, 0.1f), 6);
            state["numeric_step_rotation"] = Math.Round(SettingsManager.GetFloat(Settings.NumericStepRot, 1.0f), 6);
            return state;
        }

        private static object EditorOptions(McpCall call)
        {
            //Checked before anything changes
            CompositeBrowserMode? mode = null;
            if (call.Has("composite_browser_mode"))
            {
                string wanted = call.Str("composite_browser_mode").Trim().ToLowerInvariant();
                if (!BrowserModes.TryGetValue(wanted, out CompositeBrowserMode parsed))
                    throw new McpError("'composite_browser_mode' must be one of: " + string.Join(", ", BrowserModes.Keys) + ".");
                mode = parsed;
            }
            int? scale = null;
            if (call.Has("composite_preview_scale"))
            {
                int wanted = call.Int("composite_preview_scale");
                if (!PreviewScales.Contains(wanted))
                    throw new McpError("'composite_preview_scale' must be one of " + string.Join(", ", PreviewScales) + " (percent).");
                scale = wanted;
            }
            float? step = ReadStep(call, "numeric_step"), rotationStep = ReadStep(call, "numeric_step_rotation");
            bool resetLayouts = call.Bool("reset_ui_layouts");
            bool anything = mode != null || scale != null || step != null || rotationStep != null || resetLayouts || call.Has("dark_mode")
                || BoolOptions.Any(o => call.Has(o.Arg));

            return McpEditor.UI(() =>
            {
                CommandsEditor editor = McpEditor.Editor;
                if (!anything)
                    return new JObject() { ["options"] = EditorOptionState() };
                //Several of these rebuild panels or reopen the composite on screen: never under a save or a level load
                if (UndoStack.Current.Blocked)
                    throw new McpError("OpenCAGE is saving the level. Change options when it has finished.");
                if (editor.IsLevelLoadInProgress)
                    throw new McpError("A level is loading. Change options when it is open.");

                List<string> changed = new List<string>();
                List<string> keys = new List<string>();
                foreach (BoolOption option in BoolOptions)
                {
                    if (!call.Has(option.Arg)) continue;
                    bool value = call.Bool(option.Arg);
                    if (SettingsManager.GetBool(option.Key) == value) continue;
                    SettingsManager.SetBool(option.Key, value);
                    keys.Add(option.Key);
                    changed.Add(option.Arg);
                }
                if (mode != null && CompositeBrowserModes.Current != mode.Value)
                {
                    CompositeBrowserModes.Set(mode.Value);
                    keys.Add(Settings.CompositeBrowserMode);
                    changed.Add("composite_browser_mode");
                }
                if (scale != null && SettingsManager.GetInteger(Settings.CompositePreviewScale, 100) != scale.Value)
                {
                    SettingsManager.SetInteger(Settings.CompositePreviewScale, scale.Value);
                    keys.Add(Settings.CompositePreviewScale);
                    changed.Add("composite_preview_scale");
                }
                if (step != null && Math.Abs(SettingsManager.GetFloat(Settings.NumericStep, 0.1f) - step.Value) > 1e-7)
                {
                    SettingsManager.SetFloat(Settings.NumericStep, step.Value);
                    keys.Add(Settings.NumericStep);
                    changed.Add("numeric_step");
                }
                if (rotationStep != null && Math.Abs(SettingsManager.GetFloat(Settings.NumericStepRot, 1.0f) - rotationStep.Value) > 1e-7)
                {
                    SettingsManager.SetFloat(Settings.NumericStepRot, rotationStep.Value);
                    keys.Add(Settings.NumericStepRot);
                    changed.Add("numeric_step_rotation");
                }
                //What the menu does after it writes a setting (ToggleBoolSetting -> ApplySettingEffects): menu ticks,
                //list refreshes, the browser's layout, the title bar, the inspector's spinners
                if (keys.Count != 0)
                    editor.ApplySnapIncrementChange(keys.ToArray());

                //Dark mode and the layout reset rebuild the docked panels: through the menu items themselves
                if (call.Has("dark_mode") && Theming.ThemeManager.IsDark != call.Bool("dark_mode"))
                {
                    MenuItem(editor, "darkModeToolStripMenuItem").PerformClick();
                    changed.Add("dark_mode");
                }
                if (resetLayouts)
                {
                    MenuItem(editor, "resetUILayoutsToolStripMenuItem").PerformClick();
                    changed.Add("reset_ui_layouts");
                }
                return new JObject() { ["changed"] = new JArray(changed), ["options"] = EditorOptionState() };
            });
        }

        private static float? ReadStep(McpCall call, string name)
        {
            if (!call.Has(name)) return null;
            double value = call.Num(name);
            if (!(value > 0) || value > 100)
                throw new McpError("'" + name + "' must be more than 0 and at most 100 (got " + value + ").");
            return (float)value;
        }

        /// <summary>One of the main window's menu items, by its designer name.</summary>
        private static ToolStripItem MenuItem(CommandsEditor editor, string name)
        {
            foreach (ToolStrip strip in editor.Controls.OfType<ToolStrip>())
            {
                ToolStripItem found = FindItem(strip.Items, name);
                if (found != null) return found;
            }
            throw new McpError("OpenCAGE's '" + name + "' menu item was not found (this build's menu differs).");
        }

        private static ToolStripItem FindItem(ToolStripItemCollection items, string name)
        {
            foreach (ToolStripItem item in items)
            {
                if (item.Name == name) return item;
                if (item is ToolStripDropDownItem dropDown && dropDown.HasDropDownItems)
                {
                    ToolStripItem found = FindItem(dropDown.DropDownItems, name);
                    if (found != null) return found;
                }
            }
            return null;
        }
        #endregion

        #region Runtime utils
        private static object RuntimeUtils(McpCall call)
        {
            string action = (call.Str("action") ?? "status").Trim().ToLowerInvariant();
            if (action != "load_level" && call.Has("level"))
                throw new McpError("'level' is for action load_level.");
            switch (action)
            {
                case "status":
                    return RuntimeUtilsState();
                case "connect":
                case "disconnect":
                    {
                        bool connect = action == "connect";
                        if (connect && Singleton.Platform != PatchManager.Platform.STEAM)
                            throw new McpError("The live link needs the Steam version of the game (the runtime utils that serve it support no other); this install is " + Singleton.Platform + ".");
                        McpEditor.UI(() =>
                        {
                            SettingsManager.SetBool(Settings.RuntimeUtilsOpt, connect);
                            //As the menu item: the setting's effect starts or stops the link (in the background)
                            McpEditor.Editor.ApplySnapIncrementChange(new[] { Settings.RuntimeUtilsOpt });
                        });
                        //Here, off the UI thread, for a definite answer (each waits for a connect already under way)
                        if (!connect)
                            global::OpenCAGE.RuntimeUtilsConnection.Send.Stop();
                        if (connect && !global::OpenCAGE.RuntimeUtilsConnection.Send.Start())
                            throw new McpError("Could not connect to the live link (ws://127.0.0.1:8765) yet. The game serves it only when launched from OpenCAGE with launch_options live_link on: launch_game now (Connect to Game stays on, and OpenCAGE connects once the game is up), or restart a game that was launched without it.");
                        return McpEditor.UI(() => RuntimeUtilsState());
                    }
                case "game_status":
                    {
                        RequireLiveLink();
                        LiveLink.GameStatus status = LiveLink.Status().Result;
                        if (!status.Reply.Ok)
                            throw new McpError(status.Reply.Message);
                        Composite root = McpEditor.UI(() => Singleton.Editor?.CompositeDisplay?.Content?.Level?.Commands?.EntryPoints?[0]);
                        JObject result = RuntimeUtilsState();
                        result["level_running"] = status.LevelRunning;
                        result["root_composite"] = status.RootCompositeName;
                        result["running_level_open_here"] = status.LevelRunning && root != null && root.shortGUID == status.RootComposite;
                        //Edits and calls are held (then turned away after 20 s) while the level's scripts are paused: a level starting, the pause menu
                        result["playing"] = status.Playing;
                        if (!string.IsNullOrEmpty(status.LoadingReason))
                            result["loading"] = status.LoadingReason;
                        //World space, for putting new entities where the player can see them
                        if (status.CameraPosition.HasValue && status.CameraForward.HasValue)
                        {
                            JObject camera = new JObject()
                            {
                                ["position"] = new JArray(status.CameraPosition.Value.X, status.CameraPosition.Value.Y, status.CameraPosition.Value.Z),
                                ["forward"] = new JArray(status.CameraForward.Value.X, status.CameraForward.Value.Y, status.CameraForward.Value.Z),
                            };
                            if (status.CameraUp.HasValue)
                                camera["up"] = new JArray(status.CameraUp.Value.X, status.CameraUp.Value.Y, status.CameraUp.Value.Z);
                            result["camera"] = camera;
                        }
                        //Whether the game is rendering from the viewport's camera (set_viewport_view live_link_camera 'viewport_to_game')
                        result["camera_sync"] = status.CameraSync;
                        //Whether the game has a CAGEAnimation taken for OpenCAGE (held, played, or still to be given back), and the drive
                        result["animation_taken"] = status.Animation;
                        result["animation_drive"] = AnimationDriveState();
                        return result;
                    }
                case "animate":
                    return Animate(call);
                case "push":
                    {
                        RequireLiveLink();
                        Commands commands = null;
                        Composite composite = null;
                        McpEditor.UI(() =>
                        {
                            commands = McpEditor.RequireCommands(false);
                            composite = LiveLinkComposite(call, commands);
                        });
                        RequireRunningLevel(commands);
                        //After any auto push in flight, and recorded as sent, so the auto push knows what the game now has
                        //(started on the UI thread, where the composite is written; awaited here)
                        LiveLink.Reply reply = McpEditor.UI(() => LiveLink.PushNow(commands, composite)).Result;
                        if (!reply.Ok)
                            throw new McpError(reply.Message);
                        return new JObject()
                        {
                            ["composite"] = composite.name,
                            ["bytes"] = reply.Bytes,
                            ["result"] = reply.Message,
                        };
                    }
                case "call_method":
                    {
                        RequireLiveLink();
                        Commands commands = null;
                        Composite composite = null;
                        Entity entity = null;
                        List<ShortGuid> path = null;
                        string method = call.Str("method", required: true).Trim();
                        bool isMethod = McpEditor.UI(() =>
                        {
                            commands = McpEditor.RequireCommands(false);
                            composite = LiveLinkComposite(call, commands);
                            entity = McpScript.FindEntity(commands, composite, call.Str("entity", required: true));
                            if (call.Has("instance_path"))
                                path = call.StrList("instance_path").Select(o => McpScript.ParseId(o) ?? throw new McpError("'" + o + "' in 'instance_path' is not an entity id.")).ToList();
                            else if (Singleton.Editor?.CompositeDisplay?.Composite == composite)
                                path = LiveLink.InstancePath(Singleton.Editor.CompositeDisplay, commands);
                            return LiveLink.Methods(commands, entity, composite).Contains(ShortGuidUtils.Generate(method));
                        });
                        RequireRunningLevel(commands);
                        if (!isMethod)
                            call.Note("'" + method + "' is not one of this entity's method pins; it is sent anyway (a composite's own pins, for instance, are called like this).");
                        //After the edits made before it (the auto push's queue and any push in flight), so it cannot overtake them
                        LiveLink.Reply reply = McpEditor.UI(() => LiveLink.CallAfterEdits(commands, composite, entity, ShortGuidUtils.Generate(method), path)).Result;
                        if (!reply.Ok)
                            throw new McpError(reply.Message);
                        return new JObject()
                        {
                            ["called"] = method,
                            ["entity"] = McpUI(() => McpScript.EntityName(commands, composite, entity)),
                            ["instances"] = path == null ? "every running instance of " + composite.name : (path.Count == 0 ? "the root" : string.Join(" > ", path.Select(o => McpScript.Id(o)))),
                            ["result"] = reply.Message,
                        };
                    }
                case "describe":
                    {
                        RequireLiveLink();
                        Composite composite = McpEditor.UI(() => LiveLinkComposite(call, McpEditor.RequireCommands(false)));
                        LiveLink.Reply reply = LiveLink.Describe(composite).Result;
                        if (!reply.Ok)
                            throw new McpError(reply.Message);
                        return new JObject() { ["composite"] = composite.name, ["running"] = new JArray(reply.Message.Split('\n').Select(o => o.TrimEnd())) };
                    }
                case "screenshot":
                    {
                        RequireLiveLink();
                        string path = call.Str("path", required: true);
                        if (!Path.IsPathRooted(path) || !path.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                            throw new McpError("'path' must be an absolute path ending .bmp.");
                        LiveLink.Reply reply = LiveLink.Screenshot(path).Result;
                        if (!reply.Ok)
                            throw new McpError(reply.Message);
                        return new JObject() { ["written"] = path };
                    }
                case "load_level":
                    {
                        string level = call.Str("level", required: true).Replace('\\', '/').Trim().Trim('/');
                        if (string.Equals(level, "menu", StringComparison.OrdinalIgnoreCase) || EditorUtils.IsFrontend(level) || EditorUtils.IsFrontend("PRODUCTION/" + level))
                            level = EditorUtils.FrontendLevel;
                        else
                            level = McpPortingTools.Normalise(level);
                        if (!global::OpenCAGE.RuntimeUtilsConnection.Send.Connected)
                            throw new McpError("Not connected to the runtime utils link: runtime_utils {action: 'connect'} first, with the game running.");
                        //The live link's own request, which the game answers (the old text message had no reply, so a level
                        //the game could not load looked the same as one it did)
                        LiveLink.Reply reply = LiveLink.LoadLevel(level).Result;
                        if (!reply.Ok)
                            throw new McpError(reply.Message);
                        JObject result = RuntimeUtilsState();
                        result["sent"] = new JObject() { ["load_level"] = level };
                        result["game"] = reply.Message;
                        call.Note("The game loads what it has on disk: save_level (build=true after script changes) first.");
                        return result;
                    }
            }
            throw new McpError("'action' must be status, connect, disconnect, game_status, push, call_method, animate, describe, screenshot or load_level.");
        }

        internal static void RequireLiveLink()
        {
            if (!global::OpenCAGE.RuntimeUtilsConnection.Send.Connected)
                throw new McpError("Not connected to the game: runtime_utils {action: 'connect'} first, with the game running.");
        }

        //Sent to another level, a composite's edits would land in whatever has the same id there
        internal static void RequireRunningLevel(Commands commands)
        {
            LiveLink.GameStatus status = LiveLink.Status().Result;
            if (!status.Reply.Ok)
                throw new McpError(status.Reply.Message);
            if (!status.LevelRunning)
                throw new McpError("The game is not running a level yet (still loading, or at a loading screen).");
            if (!status.IsRunning(commands))
                throw new McpError("The game is running a different level (root composite " + status.RootCompositeName + ").");
        }

        private static Composite LiveLinkComposite(McpCall call, Commands commands)
        {
            if (call.Has("composite"))
                return McpScript.FindComposite(commands, call.Str("composite"));
            Composite open = Singleton.Editor?.CompositeDisplay?.Composite;
            if (open == null)
                throw new McpError("No composite is open: pass 'composite'.");
            return open;
        }

        private static T McpUI<T>(Func<T> work) => McpEditor.UI(work);

        private static JObject RuntimeUtilsState() => new JObject()
        {
            ["connected"] = global::OpenCAGE.RuntimeUtilsConnection.Send.Connected,
            ["connect_at_start"] = SettingsManager.GetBool(Settings.RuntimeUtilsOpt),
            //This install's game: the live link only connects to that (a game from another install is left alone)
            ["game_running"] = CommandsEditor.GameRunning(),
            //launch_options live_link: whether launch_game has the game serve it
            ["served_at_launch"] = SettingsManager.GetBool(Settings.ScriptingHelpersLiveLink),
        };

        /// <summary>
        /// runtime_utils animate: the running game drives a CAGEAnimation as Animation Mode's "In game" does - held at a time,
        /// or played - through the same drive (LiveLinkAnimationDrive), which keeps it until it is released, Animation Mode
        /// takes it over, or OpenCAGE disconnects. An animate that fails gives it back.
        /// </summary>
        private static object Animate(McpCall call)
        {
            RequireLiveLink();
            if (call.Bool("get"))
            {
                JObject state = new JObject() { ["animation_drive"] = AnimationDriveState() };
                //Asked afresh: while the drive is off, nothing else asks the game
                LiveLinkAnimationDrive.Target current = LiveLinkAnimationDrive.Current;
                uint root = current?.Root ?? McpEditor.UI(() => LiveLink.RootOf(Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands));
                LiveLink.GameAnimation now = LiveLink.GetAnimation(root).Result;
                state["game_now"] = now.Valid ? (JToken)DescribeGameAnimation(now) : now.Reply.Message;
                return state;
            }

            if (call.Bool("release"))
            {
                bool was = false;
                McpEditor.UI(() =>
                {
                    if (AnimationModeSession.Current != null && AnimationModeSession.Current.DrivesGame)
                        throw new McpError("Animation Mode in a CAGEAnimation editor window drives the game's animation: leave Animation Mode there (or untick In game) to give it back.");
                    //Given back in the step that checked, so a window entering Animation Mode meanwhile keeps its drive
                    was = LiveLinkAnimationDrive.Active;
                    LiveLinkAnimationDrive.Release();
                });
                if (!McpEditor.WaitFor(call, () => !LiveLinkAnimationDrive.Releasing, TimeSpan.FromSeconds(5), "Giving the game its animation back"))
                    throw new McpError("The game has not answered the give-back yet (it is sent again until it does) - " + LiveLinkAnimationDrive.Status);
                return new JObject() { ["released"] = was, ["animation_drive"] = AnimationDriveState() };
            }

            if (!call.Has("time"))
                throw new McpError("animate needs 'time' (seconds into the animation) - or release, or get.");
            float time = (float)call.Num("time");
            if (float.IsNaN(time) || float.IsInfinity(time) || time < 0f)
                throw new McpError("'time' must be a number of seconds, 0 or more.");
            bool play = call.Bool("play");
            if (!play && (call.Bool("loop") || call.Bool("events")))
                call.Note("'loop' and 'events' only apply with play: a hold runs no events.");

            Commands commands = null;
            LiveLinkAnimationDrive.Target target = null;
            McpEditor.UI(() =>
            {
                //Animation Mode owns the drive while it is on (the game drives one animation at a time)
                if (AnimationModeSession.Active)
                    throw new McpError("Animation Mode is on in a CAGEAnimation editor window, and owns the game's animation drive: leave it there first.");
                commands = McpEditor.RequireCommands(false);
                Composite composite = LiveLinkComposite(call, commands);
                Entity entity = McpScript.FindEntity(commands, composite, call.Str("entity", required: true));
                string name = McpScript.EntityName(commands, composite, entity);
                if (!(entity is CAGEAnimation))
                    throw new McpError(name + " is a " + McpScript.TypeName(commands, composite, entity) + ", not a CAGEAnimation (find_cage_animations lists them).");
                //The placement as call_method takes it
                List<ShortGuid> path = null;
                if (call.Has("instance_path"))
                    path = call.StrList("instance_path").Select(o => McpScript.ParseId(o) ?? throw new McpError("'" + o + "' in 'instance_path' is not an entity id.")).ToList();
                else if (Singleton.Editor?.CompositeDisplay?.Composite == composite)
                    path = LiveLink.InstancePath(Singleton.Editor.CompositeDisplay, commands);
                target = new LiveLinkAnimationDrive.Target()
                {
                    Root = LiveLink.RootOf(commands),
                    Composite = composite.shortGUID,
                    Entity = entity.shortGUID,
                    Path = path ?? new List<ShortGuid>(),
                    Label = name,
                };
            });
            RequireRunningLevel(commands);

            LiveLink.GameAnimation game;
            try
            {
                uint sequence = 0;
                McpEditor.UI(() =>
                {
                    //Checked again in the step that takes the drive: a window may have entered Animation Mode while the game was asked
                    if (AnimationModeSession.Active)
                        throw new McpError("Animation Mode was turned on in a CAGEAnimation editor window meanwhile, and owns the game's animation drive: leave it there first.");
                    sequence = LiveLinkAnimationDrive.Begin(target, time);
                    if (play)
                        sequence = LiveLinkAnimationDrive.Play(time, 1f, call.Bool("loop"), call.Bool("events"));
                });
                game = AwaitGameAnimation(call, sequence, TimeSpan.FromSeconds(15));
            }
            catch (Exception ex)
            {
                //Left on, the drive would go on asking the game, and could take the animation after this has said it failed
                if (LiveLinkAnimationDrive.Release(target) && ex is McpError)
                    throw new McpError(ex.Message + " (The drive is off again: the game is given its animation back.)");
                throw;
            }
            call.Note("The game keeps driving it until runtime_utils {action: 'animate', release: true}, Animation Mode takes the drive over, or OpenCAGE disconnects.");
            return new JObject()
            {
                ["animation"] = target.Label,
                ["placements"] = target.Path.Count == 0 ? "every running instance of the composite" : string.Join(" > ", target.Path.Select(o => McpScript.Id(o))),
                [play ? "playing_from" : "held_at"] = time,
                ["game"] = DescribeGameAnimation(game),
            };
        }

        //The game cannot drive the animation at all in these states (it waits through the others, or shows it)
        private static readonly string[] CannotDrive = { "not_found", "not_animation", "disabled", "no_data", "cinematic" };

        /// <summary>
        /// Wait until a game frame has applied this change of the animation drive, and return what the game showed. When the
        /// game turned it away, or cannot drive that animation (not in the running level, disabled, a cinematic), says why.
        /// </summary>
        internal static LiveLink.GameAnimation AwaitGameAnimation(McpCall call, uint sequence, TimeSpan timeout)
        {
            Stopwatch waited = Stopwatch.StartNew();
            int reported = -1;
            while (true)
            {
                call.ThrowIfCancelled();
                if (LiveLinkAnimationDrive.Unsupported)
                    throw new McpError("The game's runtime utils are from before the animation drive: relaunch the game from OpenCAGE (launch_game) to update them.");
                string refusal = LiveLinkAnimationDrive.RefusalOf(sequence);
                if (refusal != null)
                    throw new McpError("The game turned it away: " + refusal);
                LiveLink.GameAnimation game = LiveLinkAnimationDrive.AnswerTo(sequence);
                if (game != null && game.Shows(sequence))
                    return game;
                if (game != null && CannotDrive.Contains(game.State))
                    throw new McpError("The game cannot drive it (" + game.State + "): " + game.Reason);
                if (!LiveLink.Connected)
                    throw new McpError("The game disconnected (it gives the animation back by itself).");
                if (waited.Elapsed > timeout)
                    throw new McpError("No game frame applied it within " + (int)timeout.TotalSeconds + " s - " + LiveLinkAnimationDrive.Status + ".");
                int seconds = (int)waited.Elapsed.TotalSeconds;
                if (seconds != reported)
                {
                    reported = seconds;
                    call.Progress("Waiting for the game to apply it (" + seconds + " s)", seconds, timeout.TotalSeconds);
                }
                System.Threading.Thread.Sleep(30);
            }
        }

        internal static JObject DescribeGameAnimation(LiveLink.GameAnimation game)
        {
            if (game == null || !game.Valid)
                return null;
            JObject described = new JObject()
            {
                ["state"] = game.State,
                ["time"] = double.IsNaN(game.Time) ? JValue.CreateNull() : (JToken)Math.Round(game.Time, 4),
                ["length"] = double.IsNaN(game.Length) ? JValue.CreateNull() : (JToken)Math.Round(game.Length, 4),
                ["sequence"] = game.Sequence,
                ["frame"] = game.Frame,
                ["instances_applied"] = game.Applied,
                ["instances_found"] = game.Found,
                ["was_playing"] = game.WasPlaying,
            };
            if (game.Reason.Length != 0)
                described["reason"] = game.Reason;
            return described;
        }

        /// <summary>The animation drive: what the game is to do, for whom, and what it last said it does.</summary>
        private static JObject AnimationDriveState()
        {
            LiveLinkAnimationDrive.Target target = LiveLinkAnimationDrive.Current;
            JObject drive = new JObject() { ["on"] = target != null };
            if (target != null)
            {
                bool byMode = McpEditor.UI(() => AnimationModeSession.Current != null && AnimationModeSession.Current.DrivesGame);
                drive["by"] = byMode ? "Animation Mode (a CAGEAnimation editor window, or preview_cage_animation)" : "runtime_utils animate";
                drive["animation"] = target.Label;
                drive["composite"] = McpScript.Id(target.Composite);
                drive["entity"] = McpScript.Id(target.Entity);
                drive["instance_path"] = target.Path.Count == 0 ? (JToken)"every placement" : new JArray(target.Path.Select(o => McpScript.Id(o)));
                drive["mode"] = LiveLinkAnimationDrive.Playing ? "play" : "hold";
                drive["time"] = Math.Round(LiveLinkAnimationDrive.WantedTime, 4);
                drive["sequence"] = LiveLinkAnimationDrive.Sequence;
            }
            else if (LiveLinkAnimationDrive.Releasing)
                drive["giving_back"] = true;
            string status = LiveLinkAnimationDrive.Status;
            if (status.Length != 0)
                drive["status"] = status;
            JObject game = DescribeGameAnimation(LiveLinkAnimationDrive.LastGame);
            if (game != null)
                drive["game"] = game;
            return drive;
        }
        #endregion

#if ENABLE_MOD_PACKAGES
        #region Mods
        private static object Mods(McpCall call)
        {
            string action = (call.Str("action") ?? "list").Trim().ToLowerInvariant();
            Modding.ModInstaller installer = Modding.ModServices.Installer;
            Modding.ModState state = Modding.ModServices.State;
            if (installer == null || state == null)
                throw new McpError("The mod manager is not available: no game install is set.");
            bool dryRun = call.Bool("dry_run");
            int limit = Math.Max(1, call.Int("limit", 100));
            switch (action)
            {
                case "list":
                    return ModList(installer, state, limit);

                case "import":
                    {
                        string path = AbsoluteFile(call.Str("path", required: true), "path");
                        if (!File.Exists(path))
                            throw new McpError("There is no file '" + path + "'.");
                        if (dryRun)
                        {
                            Modding.ModPackage package = Modding.ModPackage.Read(path);
                            return new JObject() { ["dry_run"] = true, ["id"] = package.Info.Id, ["name"] = package.Info.Name, ["version"] = package.Info.Version, ["entries"] = package.Info.Entries.Count, ["levels"] = new JArray(package.Info.Levels) };
                        }
                        Modding.ModState.InstalledMod mod;
                        try { mod = installer.ImportPackage(path); }
                        catch (Exception e) { throw new McpError("Could not import the package: " + e.Message); }
                        call.Note("'" + mod.Name + "' is in the library, disabled. apply with its id in 'enabled' to install it.");
                        return new JObject() { ["imported"] = mod.Id, ["name"] = mod.Name, ["library"] = ModList(installer, state, limit) };
                    }

                case "remove":
                    {
                        string id = call.Str("id", required: true);
                        Modding.ModState.InstalledMod mod = state.FindMod(id) ?? throw new McpError("There is no mod '" + id + "' in the library (action list shows them).");
                        if (mod.Applied.Count != 0 || mod.Enabled)
                            throw new McpError("'" + mod.Name + "' is enabled. apply without it first, then remove it.");
                        if (dryRun)
                            return new JObject() { ["dry_run"] = true, ["would_remove"] = mod.Id, ["name"] = mod.Name };
                        try { installer.RemoveFromLibrary(mod.Id); }
                        catch (Exception e) { throw new McpError(e.Message); }
                        return new JObject() { ["removed"] = mod.Id, ["name"] = mod.Name, ["library"] = ModList(installer, state, limit) };
                    }

                case "apply":
                case "repair":
                    {
                        List<string> desired = action == "apply" && call.Has("enabled")
                            ? call.StrList("enabled")
                            : state.ModsInPriorityOrder().Where(o => o.Enabled).Select(o => o.Id).ToList();
                        List<string> unknown = desired.Where(o => state.FindMod(o) == null).ToList();
                        if (unknown.Count != 0)
                            throw new McpError("No mod " + string.Join(", ", unknown) + " in the library (action list shows them).");
                        List<Modding.ModState.InstalledMod> mods = desired.Select(o => state.FindMod(o)).ToList();
                        JArray conflicts = new JArray(installer.FindConflicts(mods).Take(limit).Select(o => new JObject() { ["mod_a"] = o.ModA, ["mod_b"] = o.ModB, ["target"] = o.Target, ["detail"] = o.Detail }));
                        if (dryRun)
                            return new JObject() { ["dry_run"] = true, ["enabled_in_order"] = new JArray(mods.Select(o => o.Name)), ["conflicts"] = conflicts };
                        if (ProcessRunning("AI"))
                            throw new McpError("Alien: Isolation is running: close it (close_game) before applying mods.");
                        McpEditor.UI(() =>
                        {
                            if (UndoStack.Current.Blocked || Singleton.CurrentBackupState != Singleton.BackupState.NONE)
                                throw new McpError("A save or backup is running. Try again when it has finished.");
                        });
                        Modding.TransactionResult result;
                        using (McpEditorTools.Heartbeat(call, "Applying mod changes"))
                        {
                            try { result = installer.ApplyConfiguration(desired, call.Bool("adopt_current_files")); }
                            catch (Exception e) { throw new McpError("Applying failed: " + e.Message); }
                        }
                        if (!result.Success)
                            throw new McpError("Apply failed, and no changes were made: " + result.Error + (result.Error != null && result.Error.Contains("aren't vanilla") ? " Pass adopt_current_files: true to keep those files' current bytes as their restore point." : ""));
                        JObject done = new JObject() { ["applied"] = new JArray(mods.Select(o => o.Name)), ["library"] = ModList(installer, state, limit) };
                        if (conflicts.Count != 0) done["conflicts"] = conflicts;
                        if (result.Warnings.Count != 0) done["warnings"] = new JArray(result.Warnings.Take(limit));
                        call.Note("An open level that a mod changed shows the old content until it is reloaded (load_level).");
                        return done;
                    }

                case "recover":
                    {
                        if (!installer.HasCrashJournal())
                            return new JObject() { ["recovered"] = false, ["reason"] = "No interrupted mod operation to recover." };
                        if (dryRun)
                            return new JObject() { ["dry_run"] = true, ["would_recover"] = true };
                        try { installer.RecoverCrashJournal(); }
                        catch (Exception e) { throw new McpError("Recovery failed: " + e.Message); }
                        return new JObject() { ["recovered"] = true };
                    }

                case "export":
                    return ExportMod(call, installer, state, limit, dryRun);
            }
            throw new McpError("'action' must be list, import, remove, apply, repair, recover or export.");
        }

        private static JObject ModList(Modding.ModInstaller installer, Modding.ModState state, int limit)
        {
            List<string> stale = new List<string>();
            try { stale = installer.PathsNeedingRepair(); } catch { }
            List<Modding.ModState.InstalledMod> mods = state.ModsInPriorityOrder();
            JArray list = new JArray();
            foreach (Modding.ModState.InstalledMod mod in mods.Take(limit))
            {
                string status;
                if (!mod.Enabled) status = mod.Applied.Count != 0 ? "disabling pending" : "disabled";
                else if (mod.Applied.Count == 0) status = "enabled, not applied";
                else if (mod.Applied.Keys.Any(o => stale.Contains(o))) status = "needs repair";
                else status = "active";
                list.Add(new JObject() { ["id"] = mod.Id, ["name"] = mod.Name, ["version"] = mod.Version, ["author"] = mod.Author, ["enabled"] = mod.Enabled, ["priority"] = mod.Priority, ["status"] = status, ["files_applied"] = mod.Applied.Count });
            }
            return new JObject()
            {
                ["total"] = mods.Count,
                ["mods"] = list,
                ["needs_repair"] = stale.Count,
                ["interrupted_operation"] = installer.HasCrashJournal(),
                ["vanilla_manifest"] = Modding.ModServices.ManifestAvailable,
            };
        }

        /// <summary>Export Mod: package what differs from vanilla (files, and config values) as an .omp, as the exporter window does.</summary>
        private static object ExportMod(McpCall call, Modding.ModInstaller installer, Modding.ModState state, int limit, bool dryRun)
        {
            string name = call.Str("name");
            string output = call.Has("path") ? AbsoluteFile(call.Str("path"), "path") : null;
            if (!dryRun)
            {
                if (string.IsNullOrWhiteSpace(name)) throw new McpError("'name' is required: the mod's name.");
                if (output == null) throw new McpError("'path' is required: where to write the package (an absolute path ending " + ModdingExtension + ").");
            }
            if (output != null && !output.EndsWith(ModdingExtension, StringComparison.OrdinalIgnoreCase))
                throw new McpError("'path' must end with " + ModdingExtension + ".");

            Modding.ScanResult scan;
            using (McpEditorTools.Heartbeat(call, "Scanning the install against the vanilla manifest"))
            {
                Modding.InstallScanner scanner = Modding.ModServices.NewScanner() ?? throw new McpError("The install cannot be scanned (no vanilla manifest for this build).");
                scan = scanner.Scan(null, (done, total) => call.Progress("Scanning the install", done, total));
            }
            List<string> changed = scan.WithStatus(Modding.FileStatus.Modified).Concat(scan.WithStatus(Modding.FileStatus.Foreign)).ToList();
            HashSet<string> changedSet = new HashSet<string>(changed);
            //A .META sidecar ships with its file (ModExportBuilder.AddFile): only list it alone when its file is unchanged
            changed = changed.Where(o => !Modding.ModExportBuilder.IsSidecar(o) || !changedSet.Contains(Modding.ModExportBuilder.SidecarParent(o))).OrderBy(o => o).ToList();

            List<string> levels = call.StrList("levels").Select(McpPortingTools.Normalise).ToList();
            List<string> files = call.StrList("files").Select(o => Modding.ModToolkit.Normalise(o)).ToList();
            bool configs = call.Bool("include_configs", true);
            List<string> chosenFiles = new List<string>();
            List<KeyValuePair<string, KeyValuePair<List<Modding.BmlPatchOp>, byte[]>>> chosenConfigs = new List<KeyValuePair<string, KeyValuePair<List<Modding.BmlPatchOp>, byte[]>>>();
            foreach (string path in changed)
            {
                string level = Modding.ModToolkit.LevelOf(path);
                if (levels.Count != 0 || files.Count != 0)
                {
                    bool wanted = (level != null && levels.Any(o => string.Equals(o, level, StringComparison.OrdinalIgnoreCase) || o.EndsWith("/" + level, StringComparison.OrdinalIgnoreCase)))
                        || files.Contains(path);
                    if (!wanted && !(configs && level == null && path.EndsWith(".BML"))) continue;
                }
                if (level == null && path.EndsWith(".BML"))
                {
                    if (!configs) continue;
                    byte[] vanilla = Modding.VanillaConfigs.GetBest(path);
                    List<Modding.BmlPatchOp> ops = vanilla == null ? null : Modding.ConfigDiff.Diff(Modding.ModServices.GameRoot, path, vanilla);
                    if (ops != null && ops.Count != 0)
                    {
                        chosenConfigs.Add(new KeyValuePair<string, KeyValuePair<List<Modding.BmlPatchOp>, byte[]>>(path, new KeyValuePair<List<Modding.BmlPatchOp>, byte[]>(ops, vanilla)));
                        continue;
                    }
                    if (levels.Count != 0 || files.Count != 0) { if (!files.Contains(path)) continue; }
                }
                chosenFiles.Add(path);
            }
            List<string> missing = files.Where(o => !changedSet.Contains(o)).ToList();
            if (missing.Count != 0)
                call.Note("Not changed from vanilla, so not included: " + string.Join(", ", missing.Take(10)) + (missing.Count > 10 ? ", ..." : "") + ".");
            if (chosenFiles.Count == 0 && chosenConfigs.Count == 0)
                throw new McpError(changed.Count == 0 ? "Nothing in the install differs from vanilla: there is nothing to export." : "Nothing matched 'levels' / 'files' among the " + changed.Count + " changed files.");

            JObject result = new JObject()
            {
                ["files"] = chosenFiles.Count,
                ["file_list"] = new JArray(chosenFiles.Take(limit)),
                ["config_files"] = new JArray(chosenConfigs.Select(o => o.Key + " (" + o.Value.Key.Count + " change" + (o.Value.Key.Count == 1 ? "" : "s") + ")").Take(limit)),
            };
            List<string> overlaps = new List<string>();
            foreach (Modding.ModState.InstalledMod mod in state.Mods.Where(o => o.Enabled))
                foreach (string path in chosenFiles)
                    if (mod.Applied.ContainsKey(path))
                        overlaps.Add(path + " (from '" + mod.Name + "')");
            if (overlaps.Count != 0)
                call.Note("Some files are supplied by other enabled mods, whose content would be baked into this package: " + string.Join(", ", overlaps.Take(8)) + (overlaps.Count > 8 ? ", ..." : "") + ".");
            if (dryRun)
            {
                result["dry_run"] = true;
                return result;
            }

            Modding.ExportResult written;
            using (McpEditorTools.Heartbeat(call, "Building the package"))
            {
                try
                {
                    Modding.ModExportBuilder builder = new Modding.ModExportBuilder(Modding.ModServices.GameRoot, Modding.ModServices.Manifest, Modding.ModServices.Cache, installer);
                    builder.Info.Name = name.Trim();
                    builder.Info.Author = (call.Str("author") ?? "").Trim();
                    builder.Info.Version = (call.Str("version") ?? "1.0").Trim();
                    builder.Info.Description = call.Str("description") ?? "";
                    builder.Info.OpenCageVersion = Singleton.Version;
                    builder.AddFiles(chosenFiles);
                    foreach (var config in chosenConfigs)
                        builder.AddConfigPatch(config.Key, config.Value.Key, config.Value.Value);
                    Directory.CreateDirectory(Path.GetDirectoryName(output));
                    written = builder.Write(output);
                }
                catch (Exception e) { throw new McpError("Export failed: " + e.Message); }
            }
            result["written"] = output;
            result["entries"] = written.Entries.Count;
            result["as_patches"] = written.Entries.Count(o => o.Kind == Modding.ModPackageEntry.KindDelta);
            result["package_size"] = Modding.ModExportBuilder.PrettySize(written.PackageSize);
            if (written.Warnings.Count != 0) result["warnings"] = new JArray(written.Warnings.Take(limit));
            return result;
        }

        private static string ModdingExtension => Modding.ModToolkit.PackageExtension;

        private static string AbsoluteFile(string given, string name)
        {
            string path = (given ?? "").Trim().Trim('"');
            if (!IsAbsolute(path))
                throw new McpError("'" + name + "' must be an absolute path, not '" + given + "'.");
            try { return Path.GetFullPath(path); }
            catch (Exception e) { throw new McpError("'" + given + "' is not a usable path (" + e.Message + ")."); }
        }
        #endregion
#endif
    }
}

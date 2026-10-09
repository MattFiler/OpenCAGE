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
using System.Numerics;
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
                Description = "Close Alien: Isolation and Cinematic Tools if they are running, as OpenCAGE does before a save or launch: this install's AI.exe (the game of another install is left running, and listed). Unsaved game progress is lost; Live Link disconnects. Changes nothing on disk.",
                Destructive = true,
                Idempotent = true,
                Run = call =>
                {
                    List<string> closed = CloseGame();
                    JObject result = new JObject() { ["closed"] = new JArray(closed), ["was_running"] = closed.Count != 0 };
                    List<string> others = EditorUtils.GameProcesses(false);
                    if (others.Count != 0)
                    {
                        result["left_running"] = new JArray(others);
                        call.Note("An AI.exe that is not this install's game (" + Singleton.PathToAI + ") was left running.");
                    }
                    return result;
                },
            };

            yield return new McpTool()
            {
                Name = "launch_options",
                Title = "Game launch options",
                Description = "Read or set the Launch Game window's options. Scripting helpers and cinematic_tools are editor settings launch_game applies. AI.exe patches (no_ui, skip_frontend...) are written to the game at once and again at every launch_game; UI mods (debug_checkpoints...) swap a whole movie in DATA/UI.PAK, written when the setting changes or UI.PAK holds the other movie (as after verifying the game files) - a movie edited with edit_ui_pak is kept unless reapply. For every level; not undoable. No option given: report them, with which movie UI.PAK holds (ui_pak_movies). dry_run checks without writing.",
                InputSchema = McpSchema.Object(LaunchOptionProps().ToArray()),
                Idempotent = true,
                Run = LaunchOptions,
            };

            yield return new McpTool()
            {
                Name = "game_directories",
                Title = "Game installs",
                Description = "The Alien: Isolation installs OpenCAGE knows (Options > Manage Game Directories): list them, register another (an absolute folder or AI.exe path), set the default OpenCAGE opens at its next start, or open one in a second OpenCAGE window for the user (as the window's Open In Editor). Saved in OpenCAGE's settings at once; the install open here does not change, and these tools keep working on this editor, not the second one.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("action", "list (default), register, set_default, or open.", options: new[] { "list", "register", "set_default", "open" }),
                    McpSchema.String("path", "register / set_default / open: the install folder (or its AI.exe), as an absolute path.")),
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
                Title = "Live Link to the game",
                Description = "Live Link to a running game (the toolbar's Live Link button, ws://127.0.0.1:8765, served by the runtime utils ASI when the game is launched with launch_options live_link on - which also connects by itself): status, connect, disconnect; game_status (is a level running, which, where the game camera is and faces - world space, for placing new entities in view - and camera_sync, whether it is following the viewport's camera: set_viewport_view live_link_camera 'viewport_to_game'); push a composite's scripting into the running level (entities added/removed/re-parameterised in every running instance, no reload); call_method on an entity (e.g. start); animate a CAGEAnimation in the running game as Animation Mode's In game does (the game holds it at 'time', or plays it, evaluating every track itself - no level data changes; it keeps it until release, Animation Mode takes the drive over, or OpenCAGE disconnects; get reports it, and game_status too); describe a composite's running instances; screenshot the game; load_level; activity - the composite display's Show Activity, which lights up the open composite's flowgraph links as the running game uses them (a logic link when its entity fires the output, a data link when its value is read or sent out through it) and counts how often each output fired and each method was called (shown beside the pins); in the instance navigated to from the root, or every instance when opened from the browser: mode on/off switches it (a saved setting; off stops the game tracing), clear forgets what has lit up and the counts (as Clear Activity), get (default) reports what is watched, every link used since the last clear - from and to (entity name and id, pin; the way the value went for a data link: from the entity it was read from or sent by, to the entity that read it or it was sent to), kind logic/data, direction read/write (data links: the last use), count, seconds_since_last and glowing (used within the last 1.5 s) - and pins: every output fired (side fired) and method called (side called), with entity, id, pin, count and seconds_since_last. The game must be running the level open here, as saved - push sends edits made since. The 3D viewport draws no lighting: to see a light edit, push it, call_method its 'refresh' (lights take new values on refresh), then screenshot. Where the player is: game_status' camera position, which check_zones near turns into the zones there. Saving the level closes the game.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("action", "status (default), connect, disconnect, game_status, push, call_method, animate, describe, screenshot, load_level, or activity.", options: new[] { "status", "connect", "disconnect", "game_status", "push", "call_method", "animate", "describe", "screenshot", "load_level", "activity" }),
                    McpSchema.String("level", "load_level: the level, as list_levels names it ('menu' for the frontend)."),
                    McpSchema.String("composite", "push, call_method, animate, describe: the composite (default: the one open in the editor); activity: must be the one open (activity is shown on the open composite)."),
                    McpSchema.String("mode", "activity: on, off, clear, or get (default).", options: new[] { "on", "off", "clear", "get" }),
                    McpSchema.Integer("limit", "activity get: at most this many links, and this many pins, most recently used first (default 200)."),
                    McpSchema.String("entity", "call_method: the entity to call; animate: the CAGEAnimation - in that composite (name or id)."),
                    McpSchema.String("method", "call_method: the method pin to call, e.g. start, stop, trigger."),
                    McpSchema.Any("instance_path", "call_method, animate: the composite instance entities (ids or names; an array, a string split on '/', or a path a result gives) from the level's root down to the placement of the composite to call in (animate: the placement to drive); checked against the script first. Default: the path the open composite was reached through from the root, else every running instance of the composite."),
                    McpSchema.Number("time", "animate: seconds into the animation to hold it at, or play it from (the game keeps it just short of its end)."),
                    McpSchema.Boolean("play", "animate: play it from 'time' on the game's own clock instead of holding it."),
                    McpSchema.Boolean("loop", "animate with play: go round again from the start rather than ending."),
                    McpSchema.Boolean("events", "animate with play: run the event tracks the play passes over, which reach the level's scripts (a hold never runs them)."),
                    McpSchema.Boolean("release", "animate: give the game its animation back."),
                    McpSchema.Boolean("get", "animate: only report the drive and what the game does with it."),
                    McpSchema.String("path", "screenshot: an absolute path for the .bmp.")),
                Run = RuntimeUtils,
            };

            yield return new McpTool()
            {
                Name = "get_player_setup",
                Title = "Get player start",
                Description = "How the open level starts the player: its spawn points (instances of Archetypes\\Script\\Mission\\SpawnPositionSelect, with world position and rotation and spawn_on_reset - the one with it true is where a fresh start puts the player), checkpoints, the player Character and the display model it wears, and the entities that set up the player's loadout and state (WEAPON_GiveToPlayer, AddToInventory, RemoveWeaponsFromPlayer, CHR_SetHealth, SetPlayerHasKeycard, SetPlayerHasGatingTool...) with their values and what triggers them. Also the config records that shape the player and are shared by every level (get_config_record). set_player_start moves the start.",
                InputSchema = McpSchema.Object(McpSchema.Limit(50, "entities of each kind")),
                ReadOnly = true,
                Idempotent = true,
                Run = call => McpEditor.UI(() => PlayerSetup(call)),
            };

            yield return new McpTool()
            {
                Name = "set_player_start",
                Title = "Set player start",
                Description = "Choose where a fresh start of the open level puts the player, as one undo step: spawn names a placed SpawnPositionSelect (its instance path from the root, as get_player_setup lists it), or position + rotation places a new one there (in 'into', default the root: world space). Its spawn_on_reset is set true and every other SpawnPositionSelect instance's false. The level needs SpawnPositionSelect (port_composites it, with the player's display models, from a campaign level). Then save_level; launch_game starts there.",
                InputSchema = McpSchema.Object(
                    McpSchema.Any("spawn", "An existing spawn: the instance entities (names or ids) from the root down to the SpawnPositionSelect instance - an array, a string split on '/', or the path get_player_setup gives it."),
                    McpSchema.Position("position", "A new spawn's position, in 'into's space."),
                    McpSchema.Rotation("rotation", "A new spawn's rotation (the player faces its +Z: yaw is the heading)."),
                    McpSchema.String("into", "Where a new spawn goes. " + McpSchema.CompositeDefaultRoot),
                    McpSchema.String("name", "A new spawn's name (default numbered).")),
                Run = SetPlayerStart,
            };

#if ENABLE_MOD_PACKAGES
            yield return new McpTool()
            {
                Name = "mods",
                Title = "Mod manager",
                Description = "The mod manager: list the library, import an .omp package, remove one, apply (the enabled mods, in priority order, restoring everything else to how it is without mods - mods changing the same file are combined: configs and behaviour trees value by value, text string by string, PAKs entry by entry, a level entity by entity and then rebuilt; where two change the same thing the later wins and it's reported. The user's own changes to files no mod had changed are the starting point mods are combined onto, and come back when the mods are removed), repair, recover an interrupted apply, or export changed files as an .omp (a release for the mod manager; export_composite_package's .ocp is composites to import into a level instead). Game files are rewritten at once; not undoable. dry_run reports the clashes first, and files changed since the mods were installed (which apply refuses to replace until told keep or discard).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("action", "list (default), import, remove, apply, repair, recover, or export.", options: new[] { "list", "import", "remove", "apply", "repair", "recover", "export" }),
                    McpSchema.String("path", "import: the .omp package; export: where to write it. An absolute path."),
                    McpSchema.String("id", "remove: the mod's id (from list)."),
                    McpSchema.Strings("enabled", "apply: the ids of the mods to have enabled, lowest priority first (default: those enabled now)."),
                    McpSchema.Boolean("adopt_current_files", "apply: take files that are neither vanilla nor stored as their restore point instead of refusing."),
                    McpSchema.String("edited_files", "apply: files installed mods had changed that were changed again since - keep (as a new mod at the end of the list) or discard. Without it apply refuses while there are any.", options: new[] { "keep", "discard" }),
                    McpSchema.String("name", "export: the mod's name."),
                    McpSchema.String("author", "export: its author."),
                    McpSchema.String("version", "export: its version (default 1.0)."),
                    McpSchema.String("description", "export: its description."),
                    McpSchema.Strings("levels", "export: only changed files of these levels (as list_levels names them; custom levels and a Nostromo level's _PATCH folder included)."),
                    McpSchema.Strings("files", "export: only these changed files, as game-relative paths (e.g. DATA/ENV/PRODUCTION/X/WORLD/COMMANDS.PAK)."),
                    McpSchema.Strings("include_shared", "export with levels/files: also changed files every level shares - text (DATA/TEXT), configs, ui (UI.PAK), animation (ANIMATION.PAK, which holds its tree layouts too), all, or game-relative paths."),
                    McpSchema.Boolean("include_configs", "export: include changed config (.BML) values (default true)."),
                    McpSchema.Integer("limit", "list/export: at most this many entries in the report (default 100)."),
                    McpSchema.Boolean("dry_run", "Report what would happen, changing nothing.")),
                Destructive = true,
                Run = Mods,
            };
#endif
        }

        #region Game
        /// <summary>Close this install's game and Cinematic Tools as the Launch Game window does; what was running ("AI.exe (path)", names).</summary>
        internal static List<string> CloseGame()
        {
            return EditorUtils.CloseAI(new List<string>() { "CinematicTools", "CinematicToolsInjector" }, thisInstallOnly: true);
        }

        /// <summary>Whether a process runs: for "AI", this install's game only (an AI.exe of another install does not hold these files).</summary>
        internal static bool ProcessRunning(string name)
        {
            if (string.Equals(name, "AI", StringComparison.OrdinalIgnoreCase))
                return EditorUtils.ThisInstallsGameRunning();
            Process[] found = Process.GetProcessesByName(name);
            foreach (Process process in found) process.Dispose();
            return found.Length != 0;
        }

        /// <summary>
        /// The launch_options AI.exe patches as they are set, written at every launch_game: a verified or replaced AI.exe has
        /// lost them, and the setting is what launch_options reports. skipFrontend overrides that one for this launch. Also the
        /// file integrity check and popup patches every launch needs. Returns what could not be written.
        /// </summary>
        internal static List<string> ApplyPatchSettings(PatchManager.Platform platform, bool? skipFrontend)
        {
            List<string> failed = new List<string>();
            if (!PatchManager.PatchFileIntegrityCheck(platform, Singleton.PathToAI)) failed.Add("file integrity check");
            if (!PatchManager.PatchPopupMessage(platform, Singleton.PathToAI)) failed.Add("popup message");
            foreach (LaunchOption option in LaunchOptionList.Where(o => o.Kind == KindPatch))
                if (!option.Patch(platform, Singleton.PathToAI, PatchValue(option, skipFrontend)))
                    failed.Add(option.Arg);
            return failed;
        }

        /// <summary>The AI.exe patches launch_game would write, by launch_options name.</summary>
        internal static JObject PatchSettings(bool? skipFrontend)
        {
            JObject patches = new JObject();
            foreach (LaunchOption option in LaunchOptionList.Where(o => o.Kind == KindPatch))
                patches[option.Arg] = PatchValue(option, skipFrontend);
            return patches;
        }

        private static bool PatchValue(LaunchOption option, bool? skipFrontend) =>
            option.Arg == "skip_frontend" && skipFrontend != null ? skipFrontend.Value : SettingsManager.GetBool(option.Key, option.Default);
        #endregion

        #region Launch options
        private sealed class LaunchOption
        {
            public string Arg, Key, Kind, Description;
            //AI.exe patches: writes the patch (true) or the original bytes (false)
            public Func<PatchManager.Platform, string, bool, bool> Patch;
            //What it is when never set: the current-gen patch is one every launch made (the recommended patches)
            public bool Default;
        }

        private const string KindHelper = "helper", KindPatch = "patch", KindUiMod = "ui_mod";

        private static readonly LaunchOption[] LaunchOptionList =
        {
            new LaunchOption() { Arg = "hot_reload", Key = Settings.ScriptingHelpersHotReload, Kind = KindHelper, Description = "Scripting helper: reload the current level in game with a key press." },
            new LaunchOption() { Arg = "debug_text", Key = Settings.ScriptingHelpersDebugText, Kind = KindHelper, Description = "Scripting helper: DebugText entities draw their text in game." },
            new LaunchOption() { Arg = "debug_text_stacking", Key = Settings.ScriptingHelpersDebugTextStacking, Kind = KindHelper, Description = "Scripting helper: DebugTextStacking entities draw their text in game." },
            new LaunchOption() { Arg = "debug_environment_marker", Key = Settings.ScriptingHelpersDebugEnvironmentMarker, Kind = KindHelper, Description = "Scripting helper: DebugEnvironmentMarker entities draw their text in the world." },
            new LaunchOption() { Arg = "debug_position_marker", Key = Settings.ScriptingHelpersDebugPositionMarker, Kind = KindHelper, Description = "Scripting helper: DebugPositionMarker entities draw axes in the world." },
            new LaunchOption() { Arg = "live_link", Key = Settings.ScriptingHelpersLiveLink, Kind = KindHelper, Description = "Scripting helper (Enable Live Link): the game serves Live Link, and OpenCAGE connects to it by itself once the game is up (runtime_utils) - script edits reach the running level without a reload, entity methods can be called, and the viewport's camera and the game's can follow each other (set_viewport_view live_link_camera)." },
            new LaunchOption() { Arg = "cinematic_tools", Key = Settings.CinematicTools, Kind = KindHelper, Description = "Start Cinematic Tools (free camera) with the game. Steam only." },
            new LaunchOption() { Arg = "no_ui", Key = Settings.HudDisabled, Kind = KindPatch, Patch = PatchManager.PatchNoUIFlag, Description = "AI.exe patch: no HUD or menus (clean screenshots)." },
            new LaunchOption() { Arg = "skip_frontend", Key = Settings.SkipFrontend, Kind = KindPatch, Patch = PatchManager.PatchSkipFrontendFlag, Description = "AI.exe patch: skip the frontend (returning to the menu then misbehaves)." },
            new LaunchOption() { Arg = "ui_memory_overlay", Key = Settings.UiEnabledUiPerf, Kind = KindPatch, Patch = PatchManager.PatchUIPerfFlag, Description = "AI.exe patch: the UI memory overlay." },
            new LaunchOption() { Arg = "memory_logging", Key = Settings.MemReplayLogs, Kind = KindPatch, Patch = PatchManager.PatchMemReplayLogFlag, Description = "AI.exe patch: memory replay logging." },
            new LaunchOption() { Arg = "patch_current_gen_optimisations", Key = Settings.PatchCurrentGen, Kind = KindPatch, Patch = PatchManager.DisableCurrentGenOptimisations, Default = true, Description = "AI.exe patch: disable the current-gen script optimisations (on unless switched off: edited levels need it)." },
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
            yield return McpSchema.Boolean("reapply", "UI mods given: write their movie into UI.PAK even over one edited with edit_ui_pak. Without it a UI mod is written when its setting changes or UI.PAK holds the other stock movie (as after verifying the game files), and an edited movie is left alone.");
            yield return McpSchema.Boolean("close_game", "If the game is running, close it first so AI.exe / UI.PAK can be written (default false: refuse while it runs).");
            yield return McpSchema.Boolean("dry_run", "Check and report what would change, writing nothing.");
        }

        /// <summary>Which movie UI.PAK holds for a UI mod: "mod", "vanilla", or "edited" (neither, e.g. written with edit_ui_pak); null when it has none.</summary>
        internal static string UiMovieState(PAK2 pak, string file)
        {
            PAK2.File entry = pak.Entries.FirstOrDefault(o => o.Filename == "DATA/UI/" + file + ".GFX");
            if (entry?.Content == null) return null;
            if (entry.Content.SequenceEqual(UiResource("UI_Mods/" + file + "_MOD.GFX") ?? new byte[0])) return "mod";
            if (entry.Content.SequenceEqual(UiResource("UI_Mods/" + file + ".GFX") ?? new byte[0])) return "vanilla";
            return "edited";
        }

        /// <summary>The UI mod a UI.PAK path belongs to (its launch_options name), or null.</summary>
        internal static string UiModOf(string pakPath)
        {
            string path = (pakPath ?? "").Replace('\\', '/').TrimStart('/');
            LaunchOption option = LaunchOptionList.FirstOrDefault(o => o.Kind == KindUiMod && string.Equals(path, "DATA/UI/" + o.Key + ".GFX", StringComparison.OrdinalIgnoreCase));
            return option?.Arg;
        }

        private static readonly Dictionary<string, byte[]> _uiResources = new Dictionary<string, byte[]>();
        private static byte[] UiResource(string path)
        {
            lock (_uiResources)
            {
                if (_uiResources.TryGetValue(path, out byte[] known)) return known;
                System.Reflection.Assembly assembly = typeof(global::OpenCAGE.LaunchGame).Assembly;
                string wanted = path.Replace('/', '.');
                string name = assembly.GetManifestResourceNames().FirstOrDefault(o => o.Contains(wanted));
                byte[] bytes = null;
                if (name != null)
                    using (Stream stream = assembly.GetManifestResourceStream(name))
                    using (MemoryStream copy = new MemoryStream())
                    {
                        stream.CopyTo(copy);
                        bytes = copy.ToArray();
                    }
                _uiResources[path] = bytes;
                return bytes;
            }
        }

        private static JObject LaunchOptionState()
        {
            JObject state = new JObject();
            foreach (LaunchOption option in LaunchOptionList)
            {
                state[option.Arg] = SettingsManager.GetBool(option.Key, option.Default);
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
            string uiPak = Path.Combine(Singleton.PathToAI, "DATA", "UI.PAK");
            //What UI.PAK holds for each UI mod (a verify of the game files puts the stock movies back whatever the settings say)
            Dictionary<string, string> movies = new Dictionary<string, string>();
            if (File.Exists(uiPak) && (asked.Any(o => o.Key.Kind == KindUiMod) || (asked.Count == 0 && hotReloadKey == null)))
            {
                try
                {
                    PAK2 pak = new PAK2(uiPak);
                    foreach (LaunchOption option in LaunchOptionList.Where(o => o.Kind == KindUiMod))
                        movies[option.Arg] = UiMovieState(pak, option.Key);
                }
                catch (Exception) { }
            }
            if (asked.Count == 0 && hotReloadKey == null)
            {
                result["options"] = before;
                if (movies.Count != 0)
                    result["ui_pak_movies"] = new JObject(movies.Select(o => new JProperty(o.Key, o.Value)));
                List<string> differ = LaunchOptionList.Where(o => o.Kind == KindUiMod && movies.TryGetValue(o.Arg, out string state) && state != null && state != (SettingsManager.GetBool(o.Key) ? "mod" : "vanilla")).Select(o => o.Arg).ToList();
                if (differ.Count != 0)
                    call.Note("UI.PAK does not hold what these UI mod settings say: " + string.Join(", ", differ) + " (ui_pak_movies). Passing the option again writes it (reapply: true also over an edited movie).");
                result["platform"] = platform.ToString();
                result["scripting_helpers_available"] = global::OpenCAGE.LaunchGame.ScriptingHelpersAvailable();
                result["cinematic_tools_available"] = global::OpenCAGE.LaunchGame.CinematicToolsAvailable();
                if (call.Has("close_game"))
                    call.Note("close_game only matters when an AI.exe patch or UI mod is being changed.");
                return result;
            }

            //AI.exe patches are re-written whenever given (cheap, and it repairs an exe that verifying the game reset);
            //the UI mods rewrite all of UI.PAK: when their setting changes, or UI.PAK holds the other stock movie, but never
            //over an edited movie unless reapply. Settings only when they change.
            bool reapply = call.Bool("reapply");
            List<KeyValuePair<LaunchOption, bool>> patches = asked.Where(o => o.Key.Kind == KindPatch).ToList();
            List<KeyValuePair<LaunchOption, bool>> uiMods = new List<KeyValuePair<LaunchOption, bool>>();
            foreach (KeyValuePair<LaunchOption, bool> mod in asked.Where(o => o.Key.Kind == KindUiMod))
            {
                movies.TryGetValue(mod.Key.Arg, out string onDisk);
                bool settingChanges = SettingsManager.GetBool(mod.Key.Key) != mod.Value;
                bool diskDiffers = onDisk != null && onDisk != (mod.Value ? "mod" : "vanilla");
                if (onDisk == "edited" && !reapply)
                {
                    call.Note("UI.PAK's " + mod.Key.Key + ".GFX was edited (not the stock or mod movie): " + mod.Key.Arg + " leaves it alone" + (settingChanges ? " and only records the setting" : "") + ". reapply: true replaces it with the " + (mod.Value ? "mod" : "stock") + " movie.");
                    if (settingChanges && !dryRun) SettingsManager.SetBool(mod.Key.Key, mod.Value);
                    continue;
                }
                if (settingChanges || diskDiffers || reapply)
                {
                    uiMods.Add(mod);
                    if (onDisk == "edited")
                        call.Note(mod.Key.Arg + " replaces UI.PAK's edited " + mod.Key.Key + ".GFX with the " + (mod.Value ? "mod" : "stock") + " movie (the edit is lost).");
                    else if (!settingChanges && diskDiffers)
                        call.Note("UI.PAK held the " + onDisk + " " + mod.Key.Key + ".GFX although the setting said otherwise (a verify of the game files puts the stock movies back): it is written again.");
                }
            }
            List<KeyValuePair<LaunchOption, bool>> helpers = asked.Where(o => o.Key.Kind == KindHelper && SettingsManager.GetBool(o.Key.Key) != o.Value).ToList();
            bool keyChanges = hotReloadKey != null && hotReloadKey != (string)before["hot_reload_key"];

            if (patches.Count != 0 && platform != PatchManager.Platform.STEAM && platform != PatchManager.Platform.EPIC_GAMES_STORE && platform != PatchManager.Platform.GOG)
                throw new McpError("AI.exe patches exist only for the Steam, Epic and GOG builds (this install is " + platform + "). Nothing was changed.");
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
            if (action != "list" && action != "register" && action != "set_default" && action != "open")
                throw McpError.Invalid("'action' must be list, register, set_default or open.");
            if (action == "list")
            {
                if (call.Has("path"))
                    throw McpError.Invalid("'path' is for register, set_default and open.");
                return ListDirectories(null);
            }

            string path = InstallFolder(call.Str("path", required: true));
            List<string> directories = GameDirectoryManager.RegisteredDirectories();
            string existing = directories.FirstOrDefault(o => SamePath(o, path));
            JObject result;
            if (action == "open")
            {
                if (SamePath(path, Singleton.PathToAI))
                    throw new McpError(McpErrorCodes.Refused, "That is the install open in this editor already.");
                string key = existing ?? path;
                Process running = McpEditor.UI(() => ChildInstanceManager.GetProcess(key));
                if (running != null && !running.HasExited)
                {
                    result = ListDirectories(key);
                    result["opened"] = key;
                    result["pid"] = running.Id;
                    call.Note("An OpenCAGE window on that install was already open.");
                    return result;
                }
                Process started = McpEditor.UI(() => ChildInstanceManager.Start(key));
                result = ListDirectories(key);
                result["opened"] = key;
                result["pid"] = started?.Id;
                call.Note("A second OpenCAGE window opens on that install for the user. These tools stay with this editor (" + Singleton.PathToAI + "): they cannot drive the new window.");
                return result;
            }
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
                            throw new McpError("Live Link needs the Steam version of the game (the runtime utils that serve it support no other); this install is " + Singleton.Platform + ".");
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
                            throw new McpError("Could not connect Live Link (ws://127.0.0.1:8765) yet. The game serves it only when launched from OpenCAGE with launch_options live_link on: launch_game now (Connect to Game stays on, and OpenCAGE connects once the game is up), or restart a game that was launched without it.");
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
                        //Whether the game is gathering script activity for OpenCAGE (the composite display's Show Activity: runtime_utils activity)
                        result["activity_tracing"] = status.Trace;
                        return result;
                    }
                case "animate":
                    return Animate(call);
                case "activity":
                    return Activity(call);
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
                                path = ReadInstancePath(call, commands, composite);
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
                            level = McpLevels.Resolve(level, call: call);
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
                        call.Note("The game loads what is on disk, without unsaved edits: push sends script edits to the running level instead. Saving closes the game, so after save_level start it again with launch_game.");
                        return result;
                    }
            }
            throw new McpError("'action' must be status, connect, disconnect, game_status, push, call_method, animate, describe, screenshot, load_level or activity.");
        }

        /// <summary>
        /// runtime_utils activity: the composite display's Show Activity (LiveLinkTrace) - the open composite's flowgraph links
        /// light up as the running game uses them. Switch it on or off, clear what the open composite has lit, or report it.
        /// </summary>
        private static object Activity(McpCall call)
        {
            string mode = (call.Str("mode") ?? "get").Trim().ToLowerInvariant();
            switch (mode)
            {
                case "on":
                case "off":
                    {
                        bool on = mode == "on";
                        //As the toolbar's Show Activity button: remembered, and in every display
                        McpEditor.UI(() => LiveLinkTrace.SetShowActivity(on));
                        if (on && !global::OpenCAGE.RuntimeUtilsConnection.Send.Connected)
                            call.Note("Not connected to the game: links light up once Live Link connects (runtime_utils {action: 'connect'}, with the game running).");
                        return McpEditor.UI(() => DescribeActivity(call, false));
                    }
                case "clear":
                    return McpEditor.UI(() =>
                    {
                        //As the toolbar's Clear Activity button
                        ActivityDisplay(call).LiveLinkActivity.Clear();
                        return DescribeActivity(call, false);
                    });
                case "get":
                    return McpEditor.UI(() => DescribeActivity(call, true));
            }
            throw new McpError("'mode' must be on, off, clear or get.");
        }

        /// <summary>The open composite display, which 'composite' (if given) must name. UI thread.</summary>
        private static global::OpenCAGE.DockPanels.CompositeDisplay ActivityDisplay(McpCall call)
        {
            global::OpenCAGE.DockPanels.CompositeDisplay display = Singleton.Editor?.CompositeDisplay;
            if (display == null || display.IsDisposed || !display.Populated || display.LiveLinkActivity == null)
                throw new McpError("No composite is open (open_composite first): activity is shown on the open composite's flowgraph.");
            if (call.Has("composite"))
            {
                Composite wanted = McpScript.FindComposite(McpEditor.RequireCommands(false), call.Str("composite"));
                if (wanted != display.Composite)
                    throw new McpError(wanted.name + " is not the composite open (" + display.Composite.name + "): activity is shown on the open composite - open_composite it first.");
            }
            return display;
        }

        /// <summary>Activity as it stands: on or not, what the game is asked to watch, and (with links) every link used since the last clear. UI thread.</summary>
        private static JObject DescribeActivity(McpCall call, bool links)
        {
            JObject result = new JObject()
            {
                ["show_activity"] = LiveLinkTrace.ShowActivity,
                ["connected"] = global::OpenCAGE.RuntimeUtilsConnection.Send.Connected,
                //The game has taken what the open composite needs watched (and is gathering it)
                ["tracing"] = LiveLinkTrace.Tracing,
                ["watches"] = LiveLinkTrace.WatchCount,
                ["batches"] = LiveLinkTrace.Batches,
            };
            if (LiveLinkTrace.WatchesLeftOut > 0)
                result["watches_left_out"] = LiveLinkTrace.WatchesLeftOut;
            //A game whose runtime utils take fewer at once (until a newer one connects)
            if (LiveLinkTrace.WatchLimit < LiveLink.MaxTraceWatches)
                result["watch_limit"] = LiveLinkTrace.WatchLimit;
            double lastBatch = LiveLinkTrace.LastBatchMs;
            if (!double.IsNaN(lastBatch))
                result["last_batch_seconds_ago"] = Math.Round((LiveLinkTrace.NowMs - lastBatch) / 1000.0, 2);
            if (LiveLinkTrace.Dropped > 0)
                result["dropped"] = LiveLinkTrace.Dropped;
            if (LiveLinkTrace.Unsupported)
                result["unsupported"] = "The game's OpenCAGE_Utils.asi is from before activity tracing: relaunch the game from OpenCAGE (launch_game) to update it.";
            if (!string.IsNullOrEmpty(LiveLinkTrace.LastStatus))
                result["status"] = LiveLinkTrace.LastStatus;

            global::OpenCAGE.DockPanels.CompositeDisplay display = Singleton.Editor?.CompositeDisplay;
            if (call.Has("composite") || links)
                display = ActivityDisplay(call);
            if (display == null || !display.Populated || display.LiveLinkActivity == null)
                return result;
            //What the display shows now (a navigation a moment ago included)
            LiveLinkTrace.Refresh();
            LiveLinkActivity activity = display.LiveLinkActivity;
            Composite composite = activity.Composite ?? display.Composite;
            Commands commands = activity.Commands ?? display.Content?.Level?.Commands;
            result["watching"] = new JObject()
            {
                ["composite"] = composite.name,
                ["composite_id"] = McpScript.Id(composite.shortGUID),
                ["instance_path"] = activity.Path == null ? (JToken)"every instance" : new JArray(activity.Path.Select(o => McpScript.Id(o))),
            };
            result["lit_links"] = activity.Count;
            result["counted_pins"] = activity.PinCount;
            if (!links)
                return result;

            int limit = Math.Max(1, Math.Min(5000, call.Int("limit", 200)));
            double now = LiveLinkTrace.NowMs;
            JArray list = new JArray();
            foreach (KeyValuePair<LinkKey, LinkActivity> pair in activity.Links.OrderByDescending(o => o.Value.LastMs).Take(limit))
            {
                LinkKey key = pair.Key;
                LinkActivity link = pair.Value;
                JObject owner = ActivityEnd(commands, composite, key.Owner, key.OwnerPin);
                JObject linked = ActivityEnd(commands, composite, key.Linked, key.LinkedPin);
                //A data link is kept on the entity at its top: its value comes in from the other end when that entity reads
                //it, and goes out to the other end when it sends it - from and to follow the value
                bool inward = link.Data && !link.Write;
                JObject entry = new JObject()
                {
                    ["kind"] = link.Data ? "data" : "logic",
                    ["from"] = inward ? linked : owner,
                    ["to"] = inward ? owner : linked,
                };
                if (link.Data)
                    entry["direction"] = link.Write ? "write" : "read";
                entry["count"] = link.Count;
                entry["seconds_since_last"] = Math.Round((now - link.LastMs) / 1000.0, 2);
                entry["glowing"] = now - link.LastMs < LiveLinkTrace.GlowMilliseconds;
                list.Add(entry);
            }
            result["links"] = list;
            if (activity.Count > limit)
                call.Note(activity.Count + " links have been used; the " + limit + " used most recently are listed ('limit' for more).");

            //The counts the pages show beside the pins: "name [n]" for an output fired, "[n] name" for a method called
            JArray pins = new JArray();
            foreach (KeyValuePair<PinKey, PinActivity> pair in activity.Pins.OrderByDescending(o => o.Value.LastMs).Take(limit))
            {
                JObject entry = ActivityEnd(commands, composite, pair.Key.Entity, pair.Key.Pin);
                entry["side"] = pair.Key.Use == PinUse.Fired ? "fired" : "called";
                entry["count"] = pair.Value.Count;
                entry["seconds_since_last"] = Math.Round((now - pair.Value.LastMs) / 1000.0, 2);
                pins.Add(entry);
            }
            result["pins"] = pins;
            if (activity.PinCount > limit)
                call.Note(activity.PinCount + " pins have been used; the " + limit + " used most recently are listed ('limit' for more).");
            return result;
        }

        private static JObject ActivityEnd(Commands commands, Composite composite, uint id, uint pin)
        {
            ShortGuid entityId = new ShortGuid(id);
            Entity entity = composite.GetEntityByID(entityId);
            return new JObject()
            {
                ["entity"] = entity == null || commands == null ? McpScript.Id(entityId) : McpScript.EntityName(commands, composite, entity),
                ["id"] = McpScript.Id(entityId),
                ["pin"] = McpScript.ParamName(new ShortGuid(pin)),
            };
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

        /// <summary>
        /// 'instance_path' as call_method and animate take it - the instance entities from the level's root down to one placement of
        /// <paramref name="composite"/>, as ids or names or a path a result gives - checked against the script before it goes to the
        /// game: every step an instance, the last placing that composite. Empty for the root composite itself. UI thread.
        /// </summary>
        private static List<ShortGuid> ReadInstancePath(McpCall call, Commands commands, Composite composite)
        {
            Composite root = commands.EntryPoints[0];
            JToken token = call.Token("instance_path");
            if (token is JObject given && given["from"]?.Type == JTokenType.String && McpScript.FindComposite(commands, (string)given["from"]) != root)
                throw McpError.Invalid("'instance_path' runs from the level's root composite; that path starts in " + (string)given["from"] + ".");
            if (token is JArray empty && empty.Count == 0)
            {
                if (composite != root)
                    throw McpError.Invalid("An empty 'instance_path' is the root composite, but the entity is in " + composite.name + ": give the instances from the root down to a placement of it (get_placements lists them).");
                return new List<ShortGuid>();
            }
            List<Entity> chain = McpScript.ChainFrom(commands, root, McpScript.PathSteps(token, "instance_path"));
            Composite reached = McpScript.InstancedComposite(commands, chain[chain.Count - 1]);
            if (reached != composite)
                throw McpError.Invalid("'instance_path' leads to " + (reached == null ? McpScript.WithArticle(McpScript.TypeName(commands, chain.Count > 1 ? McpScript.InstancedComposite(commands, chain[chain.Count - 2]) : root, chain[chain.Count - 1])) + ", not an instance" : "an instance of " + reached.name) +
                    ", not to a placement of " + composite.name + " (where the entity is). get_placements lists its placements with their ids.");
            return chain.Select(o => o.shortGUID).ToList();
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
                    path = ReadInstancePath(call, commands, composite);
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

        #region Player start
        private static readonly ShortGuid SpawnOnReset = ShortGuidUtils.Generate("spawn_on_reset");

        //The entity types that give the player things or set their state at the start
        private static readonly string[] LoadoutTypes =
        {
            "WEAPON_GiveToPlayer", "AddToInventory", "RemoveWeaponsFromPlayer", "CHR_SetHealth", "CHR_SetInvincibility", "SetPlayerHasKeycard",
            "SetPlayerHasGatingTool", "PlayerTorch", "PlayerWeaponMonitor",
        };

        /// <summary>The composites that are spawn points (SpawnPositionSelect, wherever its folder).</summary>
        private static HashSet<ShortGuid> SpawnComposites(Commands commands) =>
            new HashSet<ShortGuid>(commands.Entries.Where(o => o != null && McpScript.NormalisePath(o.name).EndsWith("SpawnPositionSelect", StringComparison.OrdinalIgnoreCase)).Select(o => o.shortGUID));

        private static JObject DescribeStep(Commands commands, Composite root, McpPlacements.Step step)
        {
            JObject described = new JObject()
            {
                ["path"] = new JArray(step.Chain.Select((o, i) => McpScript.EntityName(commands, i == 0 ? root : McpScript.InstancedComposite(commands, step.Chain[i - 1]) ?? root, o))),
                ["ids"] = new JArray(step.Chain.Select(o => McpScript.Id(o.shortGUID))),
                ["composite"] = step.Composite.name,
            };
            if (step.World != null)
            {
                described["position"] = McpValues.Vector(step.World.position);
                described["rotation"] = McpValues.Vector(step.World.rotation);
                described["space"] = "world";
            }
            if (!step.Real) described["not_placed_by_game"] = step.Deleted ? "deleted" : step.Template ? "template" : "shared repeat";
            return described;
        }

        /// <summary>get_player_setup: one walk of everything the root places. UI thread.</summary>
        private static JObject PlayerSetup(McpCall call)
        {
            Commands commands = McpEditor.RequireCommands(forEditing: false);
            Composite root = commands.EntryPoints[0];
            int limit = McpPaging.Limit(call, 50);
            HashSet<ShortGuid> spawnComposites = SpawnComposites(commands);
            HashSet<ShortGuid> loadout = new HashSet<ShortGuid>(LoadoutTypes.Select(o => Enum.TryParse(o, out FunctionType type) ? new ShortGuid((uint)type) : ShortGuid.Invalid).Where(o => o != ShortGuid.Invalid));
            ShortGuid checkpoint = new ShortGuid((uint)FunctionType.Checkpoint), character = new ShortGuid((uint)FunctionType.Character);

            JArray spawns = new JArray(), checkpoints = new JArray(), players = new JArray(), setup = new JArray();
            int spawnCount = 0, checkpointCount = 0, setupCount = 0;
            McpPlacements walker = new McpPlacements(commands) { PlaceUnpositioned = true };
            walker.Walk(root, step =>
            {
                if (!(step.Entity is FunctionEntity function)) return true;
                if (!function.function.IsFunctionType && spawnComposites.Contains(function.function))
                {
                    spawnCount++;
                    if (spawns.Count >= limit) return true;
                    JObject spawn = DescribeStep(commands, root, step);
                    bool? own = (function.GetParameter(SpawnOnReset)?.content as cBool)?.value;
                    bool? effective = step.Flag(SpawnOnReset);
                    if (effective == null)
                    {
                        //Not set on the instance: the composite's own variable's default
                        VariableEntity variable = commands.GetComposite(function.function)?.variables.FirstOrDefault(o => o.name == SpawnOnReset);
                        bool? fallback = (variable?.GetParameter(SpawnOnReset)?.content as cBool)?.value;
                        spawn["spawn_on_reset"] = fallback ?? false;
                        spawn["spawn_on_reset_from"] = fallback != null ? "the composite's default" : "not set (false)";
                    }
                    else
                    {
                        spawn["spawn_on_reset"] = effective.Value;
                        if (effective != own) spawn["spawn_on_reset_from"] = "an alias override above it";
                    }
                    spawns.Add(spawn);
                }
                else if (function.function == checkpoint)
                {
                    if (++checkpointCount <= limit)
                    {
                        JObject point = DescribeStep(commands, root, step);
                        point["name"] = McpScript.EntityName(commands, step.Composite, function);
                        checkpoints.Add(point);
                    }
                }
                else if (function.function == character && (function.GetParameter("is_player")?.content as cBool)?.value == true)
                {
                    if (players.Count < limit)
                    {
                        JObject player = DescribeStep(commands, root, step);
                        string model = (function.GetParameter("display_model")?.content as cString)?.value;
                        player["display_model"] = model;
                        if (string.Equals(model, "PLAYER_FP", StringComparison.OrdinalIgnoreCase))
                            player["display_model_note"] = "PLAYER_FP is whichever of GLOBAL's suits is worn";
                        players.Add(player);
                    }
                }
                else if (loadout.Contains(function.function))
                {
                    if (++setupCount <= limit)
                    {
                        JObject entry = DescribeStep(commands, root, step);
                        entry["type"] = function.function.AsFunctionType.ToString();
                        entry["name"] = McpScript.EntityName(commands, step.Composite, function);
                        JObject values = new JObject();
                        foreach (Parameter parameter in function.parameters.Where(o => o?.content != null && o.content.dataType != DataType.TRANSFORM && o.name != ShortGuids.name))
                        {
                            try { values[McpScript.ParamName(parameter.name)] = McpValues.ToJson(parameter.content, commands); } catch { }
                            if (values.Count >= 12) break;
                        }
                        entry["values"] = values;
                        //What sets it off: links into its method pins from its own composite
                        JArray triggers = new JArray();
                        foreach (Entity other in step.Composite.GetEntities())
                            foreach (EntityConnector link in other.childLinks)
                                if (link.linkedEntityID == function.shortGUID && triggers.Count < 6)
                                    triggers.Add(McpScript.EntityName(commands, step.Composite, other) + "." + McpScript.ParamName(link.thisParamID) + " -> " + McpScript.ParamName(link.linkedParamID));
                        entry["triggered_by"] = triggers;
                        setup.Add(entry);
                    }
                }
                return true;
            }, null, call.Cancel);

            JObject result = new JObject()
            {
                ["spawns"] = spawns,
                ["spawn_count"] = spawnCount,
                ["player_characters"] = players,
                ["checkpoints"] = checkpoints,
                ["checkpoint_count"] = checkpointCount,
                ["loadout_and_state"] = setup,
                ["loadout_count"] = setupCount,
                ["shared_by_every_level"] = new JArray(
                    new JObject() { ["what"] = "the player's attributes, senses and locomotion", ["read"] = "get_config_record {kind: 'attributes', name: 'THE_PLAYER'}" },
                    new JObject() { ["what"] = "difficulty settings", ["read"] = "get_config_record {kind: 'difficulty'}" },
                    new JObject() { ["what"] = "inventory items and ammo", ["read"] = "get_config_record {kind: 'inventory_item'} / {kind: 'ammo'}" }),
            };
            if (spawnComposites.Count == 0)
                call.Note("This level has no SpawnPositionSelect composite: port_composites 'Archetypes\\Script\\Mission\\SpawnPositionSelect' from a campaign level (it brings the player's display models), then set_player_start.");
            else if (spawnCount == 0)
                call.Note("SpawnPositionSelect is in the level but not placed: set_player_start with position places one.");
            else if (!spawns.Any(o => (bool)o["spawn_on_reset"]))
                call.Note("No spawn point has spawn_on_reset true: the level's mission script spawns the player (links into a spawn's SpawnPlayer), or nothing does - set_player_start makes one spawn the player on a fresh start.");
            if (walker.Truncated)
                call.Note("The level is too big to walk in full: some placements may be missing.");
            return result;
        }

        private static object SetPlayerStart(McpCall call)
        {
            bool byPath = call.Has("spawn");
            bool byPosition = call.Has("position") || call.Has("rotation");
            if (byPath == byPosition)
                throw McpError.Invalid(byPath ? "Give spawn (an existing spawn) or position/rotation (a new one), not both." : "Give spawn (an existing spawn point's instance path, from get_player_setup) or position (+ rotation) for a new one.");
            if (byPath && (call.Has("into") || call.Has("name")))
                throw McpError.Invalid("'into' and 'name' are for a new spawn (position).");

            Commands commands = null;
            Composite focus = null, spawnComposite = null;
            Entity chosen = null;
            List<Tuple<Composite, FunctionEntity>> others = new List<Tuple<Composite, FunctionEntity>>();
            List<string> aliased = new List<string>();
            JObject result = new JObject();
            McpEditor.UI(() =>
            {
                commands = McpEditor.RequireCommands();
                HashSet<ShortGuid> spawnComposites = SpawnComposites(commands);
                if (spawnComposites.Count == 0)
                    throw new McpError(McpErrorCodes.NotFound, "This level has no SpawnPositionSelect composite. port_composites {level: 'PRODUCTION/SCI_ANDROIDLAB', composites: ['Archetypes\\\\Script\\\\Mission\\\\SpawnPositionSelect']} brings it (with the player's display models); then call this again.");
                if (byPath)
                {
                    List<Entity> chain = new List<Entity>();
                    Composite root = commands.EntryPoints[0];
                    Composite end = McpEditorTools.ResolveInstancePath(commands, root, McpScript.PathSteps(call.Token("spawn"), "spawn"), chain);
                    if (!spawnComposites.Contains(end.shortGUID))
                        throw McpError.Invalid("'spawn' leads to an instance of " + end.name + ", not a SpawnPositionSelect (get_player_setup lists the spawn points).");
                    chosen = chain[chain.Count - 1];
                    focus = chain.Count == 1 ? root : McpScript.InstancedComposite(commands, chain[chain.Count - 2]);
                }
                else
                {
                    focus = McpScript.FindComposite(commands, call.Str("into") ?? "root");
                    spawnComposite = commands.GetComposite(spawnComposites.First());
                }
                foreach (Composite composite in commands.Entries.Where(o => o != null))
                    foreach (FunctionEntity function in composite.functions)
                        if (!function.function.IsFunctionType && spawnComposites.Contains(function.function) && function != chosen)
                            others.Add(Tuple.Create(composite, function));
                //An alias override of spawn_on_reset outranks the instance's own value
                foreach (Composite composite in commands.Entries.Where(o => o != null))
                    foreach (AliasEntity alias in composite.aliases)
                        if (alias.GetParameter(SpawnOnReset) != null)
                            aliased.Add(composite.name + ": alias " + McpScript.EntityName(commands, composite, alias));
            });

            FunctionEntity made = null;
            McpScriptEdit.Outcome outcome = McpScriptEdit.Run(byPath ? "Set the player start" : "Add a player start", focus, edit =>
            {
                if (byPosition)
                {
                    made = edit.AddInstance(focus, spawnComposite, call.Str("name"));
                    Vector3 position = call.Has("position") ? McpValues.ReadVector(call.Token("position"), "position", null) : Vector3.Zero;
                    Vector3 rotation = call.Has("rotation") ? McpValues.ReadVector(call.Token("rotation"), "rotation", null) : Vector3.Zero;
                    edit.SetParameter(focus, made, "position", new JObject() { ["position"] = McpValues.Vector(position), ["rotation"] = McpValues.Vector(rotation) });
                    chosen = made;
                }
                edit.SetParameter(focus, chosen, "spawn_on_reset", true, allowCustom: true);
                foreach (Tuple<Composite, FunctionEntity> other in others)
                {
                    bool? on = (other.Item2.GetParameter(SpawnOnReset)?.content as cBool)?.value;
                    if (on == false) continue;
                    edit.SetParameter(other.Item1, other.Item2, "spawn_on_reset", false, allowCustom: true);
                }
            });

            result["start"] = McpEditor.UI(() => new JObject() { ["id"] = McpScript.Id(chosen.shortGUID), ["name"] = McpScript.EntityName(commands, focus, chosen), ["composite"] = focus.name });
            result["other_spawns_off"] = others.Count;
            result["undo_step"] = "AI: " + outcome.Label;
            if (made != null)
                result["space"] = McpEditor.UI(() => focus == commands.EntryPoints[0]) ? "world" : "composite";
            if (aliased.Count != 0)
                call.Note("Alias overrides of spawn_on_reset outrank the instances' own values and were left as they are: " + string.Join("; ", aliased.Take(6)) + ". get_player_setup shows the effective value per spawn.");
            //A spawn inside a composite placed more than once is the same entity in every placement
            List<string> repeated = McpEditor.UI(() =>
            {
                Dictionary<ShortGuid, int> placements = commands.Entries.Where(o => o != null).SelectMany(o => o.functions).Where(o => !o.function.IsFunctionType)
                    .GroupBy(o => o.function).ToDictionary(o => o.Key, o => o.Count());
                return new[] { focus }.Concat(others.Select(o => o.Item1)).Distinct()
                    .Where(o => o != commands.EntryPoints[0] && placements.TryGetValue(o.shortGUID, out int count) && count > 1).Select(o => o.name).ToList();
            });
            if (repeated.Count != 0)
                call.Note("These composites holding spawns are placed more than once, so the change applies in every placement: " + string.Join(", ", repeated.Take(5)) + ".");
            call.Note("save_level (get_editor_state level.build says whether this needs a build), then launch_game starts there.");
            return result;
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
                        Modding.ModAnalysis analysis = installer.Analyze(desired);
                        JArray conflicts = new JArray(analysis.Conflicts.Take(limit).Select(o => new JObject() { ["kept"] = o.Kept, ["lost"] = o.Lost, ["target"] = o.Target, ["where"] = o.Where, ["text"] = o.Describe() }));
                        if (dryRun)
                        {
                            JObject plan = new JObject() { ["dry_run"] = true, ["enabled_in_order"] = new JArray(mods.Select(o => o.Name)), ["conflicts"] = conflicts };
                            if (analysis.CombinedLevels.Count != 0) plan["combined_levels"] = new JArray(analysis.CombinedLevels);
                            if (analysis.Problems.Count != 0) plan["problems"] = new JArray(analysis.Problems);
                            if (analysis.EditedFiles.Count != 0) plan["edited_since_installed"] = new JArray(analysis.EditedFiles.Take(limit));
                            if (analysis.OwnChanges.Count != 0) plan["own_changes_combined"] = new JArray(analysis.OwnChanges.Take(limit));
                            return plan;
                        }
                        string editedChoice = (call.Str("edited_files") ?? "").Trim().ToLowerInvariant();
                        Modding.EditedFilesChoice edited = editedChoice == "keep" ? Modding.EditedFilesChoice.KeepAsMod : editedChoice == "discard" ? Modding.EditedFilesChoice.Discard : Modding.EditedFilesChoice.Ask;
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
                            try { result = installer.ApplyConfiguration(desired, call.Bool("adopt_current_files"), null, edited); }
                            catch (Exception e) { throw new McpError("Applying failed: " + e.Message); }
                        }
                        if (!result.Success && result.EditedFiles.Count != 0)
                            throw new McpError("Nothing was applied: " + result.Error + " Pass edited_files: keep or discard. Changed: " + string.Join(", ", result.EditedFiles.Take(20)));
                        if (!result.Success)
                            throw new McpError("Apply failed, and no changes were made: " + result.Error + (result.Error != null && result.Error.Contains("no copy of the originals") ? " Pass adopt_current_files: true to keep those files' current bytes as their restore point." : ""));
                        JObject done = new JObject() { ["applied"] = new JArray(mods.Select(o => o.Name)), ["library"] = ModList(installer, state, limit) };
                        if (result.Conflicts.Count != 0) done["conflicts"] = new JArray(result.Conflicts.Take(limit).Select(o => new JObject() { ["kept"] = o.Kept, ["lost"] = o.Lost, ["target"] = o.Target, ["where"] = o.Where, ["text"] = o.Describe() }));
                        if (result.CombinedLevels.Count != 0) done["combined_levels"] = new JArray(result.CombinedLevels);
                        if (result.Warnings.Count != 0) done["warnings"] = new JArray(result.Warnings.Take(limit));
                        if (result.KeptAsMod != null) done["kept_as_mod"] = result.KeptAsMod;
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
            //A sidecar (.META) ships with its file (ModExportBuilder.AddFile): only list it alone when its file is unchanged
            changed = changed.Where(o => !Modding.ModExportBuilder.ShipsWithParent(o, changedSet)).OrderBy(o => o).ToList();

            List<string> levels = call.StrList("levels").Select(o => McpLevels.Resolve(o, call: call, argument: "levels")).ToList();
            List<string> files = call.StrList("files").Select(o => Modding.ModToolkit.Normalise(o)).ToList();
            bool configs = call.Bool("include_configs", true);
            //A level's files by where they are: DATA/ENV/<level>/ (custom levels too), and a Nostromo level's _PATCH companion
            List<string> levelPrefixes = levels.SelectMany(o => new[] { "DATA/ENV/" + o + "/", "DATA/ENV/" + o + "_PATCH/" }).ToList();
            bool InLevels(string path) => levelPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
            bool UnderEnv(string path) => path.StartsWith("DATA/ENV/", StringComparison.OrdinalIgnoreCase);
            //Files every level shares that a filtered export would otherwise leave out
            List<string> shared = call.StrList("include_shared").Select(o => o.Trim().ToLowerInvariant()).ToList();
            foreach (string kind in shared)
                if (kind != "text" && kind != "configs" && kind != "ui" && kind != "animation" && kind != "all" && !kind.Contains("/") && !kind.Contains("."))
                    throw McpError.Invalid("include_shared takes text, configs, ui, animation, all, or game-relative paths (got '" + kind + "').");
            bool SharedWanted(string path)
            {
                if (shared.Count == 0 || UnderEnv(path)) return false;
                if (shared.Contains("all")) return true;
                if (shared.Contains("text") && path.StartsWith("DATA/TEXT/", StringComparison.OrdinalIgnoreCase)) return true;
                if (shared.Contains("ui") && (path.StartsWith("DATA/UI", StringComparison.OrdinalIgnoreCase))) return true;
                if (shared.Contains("animation") && path.IndexOf("ANIMATION", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (shared.Contains("configs") && (path.EndsWith(".XML", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".BML", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".TXT", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".BIN", StringComparison.OrdinalIgnoreCase))) return true;
                return shared.Any(o => string.Equals(Modding.ModToolkit.Normalise(o), path, StringComparison.OrdinalIgnoreCase));
            }
            List<string> leftOut = new List<string>();
            List<string> chosenFiles = new List<string>();
            List<KeyValuePair<string, KeyValuePair<List<Modding.BmlPatchOp>, byte[]>>> chosenConfigs = new List<KeyValuePair<string, KeyValuePair<List<Modding.BmlPatchOp>, byte[]>>>();
            foreach (string path in changed)
            {
                string level = UnderEnv(path) ? "" : null;
                if (levels.Count != 0 || files.Count != 0)
                {
                    bool wanted = InLevels(path) || files.Contains(path) || SharedWanted(path);
                    if (!wanted && !(configs && level == null && path.EndsWith(".BML")))
                    {
                        if (level == null) leftOut.Add(path);
                        continue;
                    }
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
            if (leftOut.Count != 0)
                call.Note(leftOut.Count + " changed file(s) every level shares were left out by the filter (" + string.Join(", ", leftOut.Take(8)) + (leftOut.Count > 8 ? ", ..." : "") + "): a level relying on them (its subtitles in DATA/TEXT, a config, UI.PAK, ANIMATION.PAK with its tree layouts) needs them too - include_shared: ['text', 'configs', 'ui', 'animation'] or their paths.");
            //What the editor holds but the disk does not: an export packs the files as they are
            McpEditor.UI(() =>
            {
                string open = Singleton.Editor?.CompositeBrowser?.Content?.IsLevelDataLoaded == true ? Singleton.Editor.CompositeBrowser.Content.Level.Name : null;
                if (open == null || (levels.Count != 0 && !levels.Contains(open, StringComparer.OrdinalIgnoreCase)) || (levels.Count == 0 && files.Count != 0)) return;
                if (DirtyTracker.IsDirty)
                    call.Note(open + " has unsaved changes in the editor, which are not in the package: save_level first.");
                if (BuildTracker.NeedsBuild)
                    call.Note(open + " needs a Save & Build (get_editor_state level.build): the package would carry data older than its script.");
            });
            if (chosenFiles.Count == 0 && chosenConfigs.Count == 0)
                throw new McpError(McpErrorCodes.NotFound, changed.Count == 0 ? "Nothing in the install differs from vanilla: there is nothing to export." : "Nothing matched 'levels' / 'files' among the " + changed.Count + " changed files.");

            JObject result = new JObject()
            {
                ["files"] = chosenFiles.Count,
                ["file_list"] = new JArray(chosenFiles.Take(limit)),
                ["config_files"] = new JArray(chosenConfigs.Select(o => o.Key + " (" + o.Value.Key.Count + " change" + (o.Value.Key.Count == 1 ? "" : "s") + ")").Take(limit)),
            };
            //The sidecars that go with the files count too; the user's own version of a file is what ships, not the mods'
            List<string> overlaps = new List<string>();
            List<string> shipped = chosenFiles.SelectMany(o => new[] { o }.Concat(Modding.ModExportBuilder.SidecarsFor(o))).Distinct().ToList();
            foreach (Modding.ModState.InstalledMod mod in state.Mods.Where(o => o.Enabled))
                foreach (string path in shipped)
                    if (mod.Applied.ContainsKey(path) && !installer.TryGetOwnVersion(path, out _))
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

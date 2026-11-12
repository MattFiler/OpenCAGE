using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// A level's own UI movies (LevelUiOverrides): listing and setting them, and launching an installed mod into its level.
    /// Registered from McpEditorTools.Tools(); the 'game' toolset.
    /// </summary>
    internal static class McpUiOverrideTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "list_ui_overrides",
                Title = "List a level's UI overrides",
                Description = "A level's own versions of the game's UI movies - the radial menu (RADIALMENU), the HUD (AGGREGATE), the weapon HUD (WEAPONSTUFF), the motion tracker's screen (MOTIONTRACKER), the hub (HUB), the pause menu (PAUSEMENU) and the rest the game opens by name: for each, whether the level brings its own. They are kept with the level as DATA/ENV/<level>/UI/<the game's file name> (a mod carries them as files of the level). launch_game / Launch Game / launch_mod into the level put them into UI.PAK under OpenCAGE's own names and point AI.exe at them for that launch only; they come out again once the game closes or anything else is launched, so every other level (the retail campaign too) keeps the game's UI. A level's movie is used over the matching UI mod (launch_options debug_checkpoints, level_selection, debug_loadscreen, quit_to_menu_on_death). in_use says which level's movies the game is set up with now.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("level", "The level, as list_levels names it (defaults to the open one; 'menu' for the main menu's FRONTEND)."),
                    McpSchema.Boolean("only_own", "List only the movies the level brings (default false: all the game's UI movies).")),
                ReadOnly = true,
                Idempotent = true,
                Toolset = "game",
                Run = List,
            };

            yield return new McpTool()
            {
                Name = "set_ui_override",
                Title = "Set a level's UI override",
                Description = "Give a level its own version of one of the game's UI movies (file: a Scaleform .gfx, or the .swf it is made from, copied into DATA/ENV/<level>/UI/ under the game's file name), or take it away (remove: true - the game's own is used again). export_game_to saves the game's own version from UI.PAK first, as a starting point to edit. Used from the next launch into the level (launch_game, Launch Game, launch_mod); not while a game launched before runs. Writes the level's folder at once: not undoable.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("movie", "The movie: its name (RADIALMENU), file name (RADIALMENU.GFX) or path as the game opens it (DATA/UI/RADIALMENU.GFX). list_ui_overrides lists them.", required: true),
                    McpSchema.String("level", "The level (defaults to the open one; 'menu' for the main menu's FRONTEND)."),
                    McpSchema.String("file", "An absolute path to the .gfx (or .swf) to use on this level."),
                    McpSchema.Boolean("remove", "Take the level's own version out."),
                    McpSchema.String("export_game_to", "An absolute path to save the game's own version of the movie to (from UI.PAK). Alone, or before file/remove.")),
                Destructive = true,
                Toolset = "game",
                Run = Set,
            };

#if ENABLE_MOD_PACKAGES
            yield return new McpTool()
            {
                Name = "launch_mod",
                Title = "Launch a mod",
                Description = "The Mod Manager's Launch Mod: start the game for an installed mod (ticked and applied, its files as installed - mods action list / apply / repair) into the level it starts in: as its package says (Create a Mod's 'Launch Mod starts it in'), else its only level; 'level' picks one (required when the mod has several and says none). That level's own UI movies go in for this launch (list_ui_overrides; they come out again once the game closes or anything else is launched) and the launch options the package asks for (no_ui, patch_current_gen_optimisations, render_constant_ambient, larger_animation_memory) are written over the player's for this launch only; the rest is as launch_game does with launch_options as set (closes a running game first; Steam starts its registered install). Use game_status / runtime_utils to wait for the level.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("mod", "The mod's id or name (mods action list shows them).", required: true),
                    McpSchema.String("level", "Start in this level instead of the mod's own choice ('menu' for the main menu)."),
                    McpSchema.Boolean("dry_run", "Say what would be launched (level, its UI movies, the options), launching and writing nothing.")),
                Destructive = true,
                Toolset = "game",
                Run = LaunchMod,
            };
#endif
        }

#if ENABLE_MOD_PACKAGES
        /// <summary>
        /// The mods tool's export: how Launch Mod starts the package's game (launch_level, launch_options), checked - or null when
        /// neither is given (the package then says nothing: its only level, or the player picks).
        /// </summary>
        internal static Modding.ModLaunchInfo LaunchArgs(McpCall call)
        {
            string level = call.Str("launch_level");
            JObject options = call.Has("launch_options") ? call.Args["launch_options"] as JObject : null;
            if (call.Has("launch_options") && options == null)
                throw McpError.Invalid("'launch_options' is an object of launch options and true/false, e.g. {\"patch_current_gen_optimisations\": true}.");
            if (level == null && (options == null || options.Count == 0))
                return null;
            Modding.ModLaunchInfo launch = new Modding.ModLaunchInfo();
            if (level != null)
                launch.Level = Modding.ModLaunch.IsMenu(level) ? Modding.ModLaunch.Menu : McpLevels.Resolve(level, call: call, argument: "launch_level");
            if (options != null && options.Count != 0)
            {
                launch.Options = new Dictionary<string, bool>();
                foreach (JProperty option in options.Properties())
                {
                    Modding.ModLaunch.Option known = Modding.ModLaunch.Options.FirstOrDefault(o => string.Equals(o.Name, option.Name, StringComparison.OrdinalIgnoreCase))
                        ?? throw McpError.Invalid("'" + option.Name + "' is not a launch option a mod can ask for. It takes: " + string.Join(", ", Modding.ModLaunch.Options.Select(o => o.Name)) + ".");
                    if (option.Value.Type != JTokenType.Boolean)
                        throw McpError.Invalid("launch_options " + known.Name + " takes true or false.");
                    launch.Options[known.Name] = (bool)option.Value;
                }
            }
            return launch;
        }

        private static object LaunchMod(McpCall call)
        {
            Modding.ModState state = Modding.ModServices.State;
            if (Modding.ModServices.Installer == null || state == null)
                throw new McpError("The mod manager is not available: no game install is set.");
            string wanted = call.Str("mod", required: true).Trim();
            Modding.ModState.InstalledMod mod = state.FindMod(wanted) ?? state.Mods.FirstOrDefault(o => string.Equals(o.Name, wanted, StringComparison.OrdinalIgnoreCase))
                ?? throw McpError.NotFound("mods", wanted, state.Mods.Select(o => o.Name + " (" + o.Id + ")"), "mods action list shows them.");
            string notReady = Modding.ModLaunch.NotReady(mod);
            if (notReady != null)
                throw new McpError(McpErrorCodes.Refused, "The game can't be launched for this mod yet: " + notReady + " (mods action apply / repair). Nothing was launched.");
            Modding.ModPackageInfo info = Modding.ModServices.Installer.OpenPackage(mod).Info;
            string level = Modding.ModLaunch.StartLevel(info, out List<string> choices);
            string given = call.Str("level");
            if (given != null)
                level = Modding.ModLaunch.IsMenu(given) ? Modding.ModLaunch.Menu : McpLevels.Resolve(given, call: call);
            if (level == null)
                throw McpError.Invalid("'" + mod.Name + "' has several levels and its package doesn't say which it starts in: give 'level' (one of " + string.Join(", ", choices) + ", or 'menu').");
            string startIn = Modding.ModLaunch.IsMenu(level) ? EditorUtils.FrontendLevel : level;

            JObject result = new JObject() { ["mod"] = mod.Name, ["id"] = mod.Id, ["level"] = Modding.ModLaunch.IsMenu(level) ? "menu" : level };
            if (info.Launch?.Options != null)
                result["options"] = JObject.FromObject(info.Launch.Options);
            if (call.Bool("dry_run"))
            {
                result["dry_run"] = true;
                result["level_ui"] = new JArray(LevelUiOverrides.Of(Singleton.PathToAI, startIn).Keys.Select(o => o.Name));
                result["game_running"] = EditorUtils.ThisInstallsGameRunning();
                return result;
            }
            McpEditor.UI(() =>
            {
                if (Singleton.CurrentBackupState != Singleton.BackupState.NONE)
                    throw McpError.Busy(Singleton.BusyText + ". Launch when it has finished.");
            });
            Modding.ModLaunch.Result launched;
            try
            {
                launched = Modding.ModLaunch.Launch(level, info.Launch?.Options);
            }
            catch (InvalidOperationException e)
            {
                throw new McpError(McpErrorCodes.Refused, e.Message + " Nothing was launched.");
            }
            result["launched"] = true;
            result["level_ui"] = new JArray(launched.LevelUi.Select(o => o.Name));
            if (launched.Closed.Count != 0)
                call.Note("The game was already running (" + string.Join(", ", launched.Closed) + ") and was closed first.");
            foreach (string problem in launched.Problems)
                call.Note(problem);
            launched.Started?.Dispose();
            return result;
        }
#endif

        /* The level a call names (or the open one): as list_levels names it, the main menu's FRONTEND allowed */
        private static string LevelOf(McpCall call)
        {
            string given = call.Str("level");
            if (given == null)
            {
                string open = McpEditor.UI(() => Singleton.Editor?.CompositeBrowser?.Content?.Level?.Name);
                if (open == null)
                    throw McpError.Invalid("No level is open: give 'level'.");
                return open;
            }
            if (string.Equals(given.Trim(), "menu", StringComparison.OrdinalIgnoreCase))
                return EditorUtils.FrontendLevel;
            return McpLevels.Resolve(given, allowFrontend: true, call: call);
        }

        private static JObject Describe(string pathToAI, string level, LevelUiOverrides.Movie movie, Dictionary<LevelUiOverrides.Movie, string> own)
        {
            JObject row = new JObject()
            {
                ["movie"] = movie.Name,
                ["what"] = movie.Label,
                ["game_path"] = movie.GamePath,
                ["own"] = own.ContainsKey(movie),
            };
            if (own.TryGetValue(movie, out string file))
            {
                FileInfo info = new FileInfo(file);
                row["file"] = file;
                row["size"] = info.Length;
                row["modified"] = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
                row["launch_path"] = movie.LevelPath;
                if (movie.Tweak != null)
                    row["used_over_ui_mod"] = McpLevelTools.LaunchOptionArg(movie.Tweak.Key);
            }
            return row;
        }

        private static object List(McpCall call)
        {
            string pathToAI = Singleton.PathToAI;
            string level = LevelOf(call);
            Dictionary<LevelUiOverrides.Movie, string> own = LevelUiOverrides.Of(pathToAI, level);
            IEnumerable<LevelUiOverrides.Movie> movies = call.Bool("only_own") ? LevelUiOverrides.All.Where(own.ContainsKey) : LevelUiOverrides.All;
            JObject result = new JObject()
            {
                ["level"] = level,
                ["folder"] = LevelUiOverrides.Folder(pathToAI, level),
                ["own_count"] = own.Count,
                ["movies"] = new JArray(movies.Select(o => Describe(pathToAI, level, o, own))),
            };
            List<string> unused = LevelUiOverrides.Unused(pathToAI, level);
            if (unused.Count != 0)
            {
                result["unused_files"] = new JArray(unused.Select(Path.GetFileName));
                call.Note("The level's UI folder has files that are not named as one of the game's movies, so nothing uses them: " + string.Join(", ", unused.Select(Path.GetFileName)) + ".");
            }
            List<LevelUiOverrides.Movie> inUse = LevelUiOverrides.InUse(pathToAI);
            result["in_use"] = inUse.Count == 0 ? (JToken)JValue.CreateNull() : new JObject()
            {
                ["level"] = LevelUiOverrides.InUseFor(pathToAI),
                ["movies"] = new JArray(inUse.Select(o => o.Name)),
            };
            return result;
        }

        private static object Set(McpCall call)
        {
            string pathToAI = Singleton.PathToAI;
            string level = LevelOf(call);
            string name = call.Str("movie", required: true);
            LevelUiOverrides.Movie movie = LevelUiOverrides.Find(name)
                ?? throw McpError.NotFound("UI movies", name, LevelUiOverrides.All.Select(o => o.Name), "list_ui_overrides lists them.");
            string file = call.Str("file");
            bool remove = call.Bool("remove");
            string exportTo = call.Str("export_game_to");
            if (file != null && remove)
                throw McpError.Invalid("Give 'file' or 'remove', not both.");
            if (file == null && !remove && exportTo == null)
                throw McpError.Invalid("Give 'file' (the level's own version), 'remove' (take it out) or 'export_game_to' (save the game's).");

            JObject result = new JObject() { ["level"] = level };
            if (exportTo != null)
            {
                if (!Path.IsPathRooted(exportTo))
                    throw McpError.Invalid("'export_game_to' must be an absolute path.");
                byte[] game = LevelUiOverrides.GameVersion(pathToAI, movie)
                    ?? throw new McpError(McpErrorCodes.NotFound, "UI.PAK has no " + movie.OriginalEntry + " (check the game's files). Nothing was changed.");
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(exportTo)));
                File.WriteAllBytes(exportTo, game);
                result["exported_game_version"] = exportTo;
                result["exported_size"] = game.Length;
            }
            if (file != null || remove)
            {
                if (file != null && (!Path.IsPathRooted(file) || !File.Exists(file)))
                    throw McpError.Invalid("'file' must be an absolute path to an existing .gfx (or .swf).");
                bool had = LevelUiOverrides.Of(pathToAI, level).ContainsKey(movie);
                if (remove && !had)
                    call.Note(level + " had no " + movie.FileName + " of its own: nothing to take out.");
                string problem = LevelUiOverrides.Set(pathToAI, level, movie, remove ? null : file);
                if (problem != null)
                    throw new McpError(McpErrorCodes.Failed, "The level's " + movie.Name + " could not be " + (remove ? "taken out" : "set") + ": " + problem + ".");
                result["changed"] = remove ? had : true;
            }
            result["movie"] = Describe(pathToAI, level, movie, LevelUiOverrides.Of(pathToAI, level));
            if (string.Equals(LevelUiOverrides.InUseFor(pathToAI), level, StringComparison.OrdinalIgnoreCase) && EditorUtils.ThisInstallsGameRunning())
                call.Note("The game launched into " + level + " runs with the movies it was launched with: the change shows from the next launch.");
            if (movie.Tweak != null && UiMovieOverrides.IsOn(movie.Tweak) && !remove && file != null)
                call.Note("The UI mod " + McpLevelTools.LaunchOptionArg(movie.Tweak.Key) + " is on: launches into " + level + " use the level's " + movie.Name + " instead of it.");
            return result;
        }
    }
}

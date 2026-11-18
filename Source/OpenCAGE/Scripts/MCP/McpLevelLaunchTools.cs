using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// A level's launch settings (LevelLaunchSettings): reading and setting them. Registered from McpEditorTools.Tools(); the 'game' toolset.
    /// </summary>
    internal static class McpLevelLaunchTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "get_level_launch_settings",
                Title = "Get a level's launch settings",
                Description = "What a level needs from the game while it is played, kept with it as DATA/ENV/<level>/LAUNCH_SETTINGS.JSON (a mod carries it as a file of the level): texture_heap_mb (the streaming texture heap is at least this many MB; null: the player's own DATA\\ENGINE_SETTINGS.XML value), npc_collisions and player_npc_collisions (false: those characters walk through each other; shots, melee, the flamethrower, grabs and attacks still hit), loading_playlist (the GBL_ITEM.BML movie or image playlist that plays as the level loads; game_playlist is the one it plays as the game ships), loading_movies (the level's own .usm movies, kept in DATA/ENV/<level>/MOVIES and carried with it; used over loading_playlist: a launch copies them in under OpenCAGE's own names and adds a playlist of them to GBL_ITEM.BML for that launch only; movie_files lists the folder), loading_movies_to_end (true: they play to the end and the level waits for them - the launch changes the game's loading screen to wait, as in_use.loading_screen_changed says - and once it has loaded, holding a button skips; else they loop until the level has loaded), larger_animation_memory (true: the game's animation memory is doubled, as launch_options larger_animation_memory does for every level). launch_game, Launch Game and launch_mod into the level write them into AI.exe for that launch; they come out once the game closes or anything else is launched. in_use says which level's settings AI.exe holds now. Also says how the character limit (18 at once) is handled: in OpenCAGE's launches a spawn past it waits, or is left out, instead of crashing.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("level", "The level, as list_levels names it (defaults to the open one)."),
                    McpSchema.Boolean("list_playlists", "Also list the playlists GBL_ITEM.BML has.")),
                ReadOnly = true,
                Idempotent = true,
                Toolset = "game",
                Run = Get,
            };

            yield return new McpTool()
            {
                Name = "set_level_launch_settings",
                Title = "Set a level's launch settings",
                Description = "Change a level's launch settings (get_level_launch_settings says what each does); only the arguments given change. texture_heap_mb " + LevelLaunchSettings.MinHeapMB + "-" + LevelLaunchSettings.MaxHeapMB + " (0 or null: the player's own), npc_collisions / player_npc_collisions true or false, loading_playlist a playlist name ('' or null: the game's own), loading_movies the level's own movies in play order (absolute paths to .usm files are copied into DATA/ENV/<level>/MOVIES; bare names are files already there; [] for none), loading_movies_to_end true or false, larger_animation_memory true or false. Writes DATA/ENV/<level>/LAUNCH_SETTINGS.JSON at once (removed when every setting is the game's own): not undoable. Used from the next launch into the level; not while a game launched before runs.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("level", "The level, as list_levels names it (defaults to the open one)."),
                    McpSchema.Integer("texture_heap_mb", "The streaming texture heap is at least this (MB); 0: the player's own."),
                    McpSchema.Boolean("npc_collisions", "false: NPCs walk through each other; true: as the game."),
                    McpSchema.Boolean("player_npc_collisions", "false: the player and NPCs walk through each other; true: as the game."),
                    McpSchema.String("loading_playlist", "A movie or image playlist in DATA\\GBL_ITEM.BML (get_config_record movie_playlist lists them); '': the game's own."),
                    McpSchema.Strings("loading_movies", "The level's own loading movies, in the order they play (at most " + LevelLoadingMovies.MaxMovies + "): an absolute path to a .usm (CRI video, as DATA/UI/MOVIES holds) is copied into the level's MOVIES folder; a bare file name is one already there; [] for none."),
                    McpSchema.Boolean("loading_movies_to_end", "true: the level's own movies play to the end and the level waits for them (the launch changes the game's loading screen to wait, for that launch only; once the level has loaded, holding a button skips); false: they loop until the level has loaded."),
                    McpSchema.Boolean("larger_animation_memory", "true: the game's animation memory is doubled for launches into the level (custom animation sets, new animation contexts)."),
                    McpSchema.Boolean("dry_run", "Check and say what would be written, writing nothing.")),
                Destructive = true,
                Toolset = "game",
                Run = Set,
            };
        }

        private static string LevelOf(McpCall call)
        {
            string given = call.Str("level");
            if (given != null)
                return McpLevels.Resolve(given, call: call);
            string open = McpEditor.UI(() => Singleton.Editor?.CompositeBrowser?.Content?.Level?.Name);
            return open ?? throw McpError.Invalid("No level is open: give 'level'.");
        }

        private static JObject Describe(string pathToAI, string level, LevelLaunchSettings settings)
        {
            JObject result = new JObject()
            {
                ["level"] = level,
                ["file"] = LevelLaunchSettings.PathOf(pathToAI, level),
                ["has_file"] = System.IO.File.Exists(LevelLaunchSettings.PathOf(pathToAI, level)),
                ["texture_heap_mb"] = settings.TextureHeapMB,
                ["npc_collisions"] = settings.NpcCollisions != false,
                ["player_npc_collisions"] = settings.PlayerNpcCollisions != false,
                ["loading_playlist"] = string.IsNullOrWhiteSpace(settings.LoadingPlaylist) ? null : settings.LoadingPlaylist.Trim(),
                ["game_playlist"] = LevelLaunchSettings.GamePlaylist(level),
                ["loading_movies"] = settings.HasMovies ? new JArray(settings.LoadingMovies.Where(o => !string.IsNullOrWhiteSpace(o))) : null,
                ["loading_movies_to_end"] = settings.LoadingMoviesToEnd == true,
                ["movies_folder"] = LevelLoadingMovies.Folder(pathToAI, level),
                ["movie_files"] = new JArray(LevelLoadingMovies.Files(pathToAI, level)),
                ["larger_animation_memory"] = settings.LargerAnimationMemory == true,
            };
            List<string> warnings = settings.Warnings(pathToAI, level);
            if (warnings.Count != 0)
                result["warnings"] = new JArray(warnings);
            return result;
        }

        private static object Get(McpCall call)
        {
            string pathToAI = Singleton.PathToAI;
            string level = LevelOf(call);
            JObject result = Describe(pathToAI, level, LevelLaunchSettings.Of(pathToAI, level));
            LevelLaunchPatches.State state = LevelLaunchPatches.Read(Singleton.Platform, pathToAI);
            result["in_use"] = new JObject()
            {
                ["level"] = LevelLaunch.InUseFor(pathToAI),
                ["texture_heap_mb"] = state.HeapMB,
                ["npc_collisions"] = state.NpcCollisions,
                ["player_npc_collisions"] = state.PlayerCollisions,
                ["loading_playlist"] = state.Playlist,
                ["own_movies_in"] = LevelLoadingMovies.InUse(pathToAI),
                ["loading_screen_changed"] = LevelLoadingMovies.ScreenChanged(pathToAI),
                ["spawn_guard"] = state.SpawnGuard,
                ["readable"] = state.Readable,
            };
            result["character_limit"] = "The game holds " + CharacterCapRule.Pool + " characters at once (the player and corpses not yet despawned included); it can't be raised. In OpenCAGE's launches a spawn with no free slot waits for one (the game despawns a corpse to make room), and characters spawning in the same moment that find none left are left out (they don't appear), instead of crashing" +
                                        (LevelLaunch.SpawnGuardOn ? "." : " - turned off in this OpenCAGE's settings.");
            if (!LevelLaunchPatches.Supports(Singleton.Platform))
                call.Note("This version of the game (" + Singleton.Platform + ") can't take launch settings: launches ignore them.");
            if (call.Bool("list_playlists"))
                result["playlists"] = new JArray(LevelLaunchSettings.Playlists(pathToAI));
            return result;
        }

        private static object Set(McpCall call)
        {
            string pathToAI = Singleton.PathToAI;
            string level = LevelOf(call);
            LevelLaunchSettings settings = LevelLaunchSettings.Of(pathToAI, level);
            if (call.Args.ContainsKey("texture_heap_mb"))
            {
                int mb = call.Has("texture_heap_mb") ? call.Int("texture_heap_mb") : 0;
                settings.TextureHeapMB = mb <= 0 ? (int?)null : mb;
            }
            if (call.Has("npc_collisions"))
                settings.NpcCollisions = call.Bool("npc_collisions") ? (bool?)null : false;
            if (call.Has("player_npc_collisions"))
                settings.PlayerNpcCollisions = call.Bool("player_npc_collisions") ? (bool?)null : false;
            if (call.Has("larger_animation_memory"))
                settings.LargerAnimationMemory = call.Bool("larger_animation_memory") ? true : (bool?)null;
            if (call.Args.ContainsKey("loading_playlist"))
                settings.LoadingPlaylist = string.IsNullOrWhiteSpace(call.Str("loading_playlist")) ? null : call.Str("loading_playlist").Trim();
            if (call.Has("loading_movies_to_end"))
                settings.LoadingMoviesToEnd = call.Bool("loading_movies_to_end") ? true : (bool?)null;
            List<string> toCopy = new List<string>();
            if (call.Args.ContainsKey("loading_movies"))
            {
                settings.LoadingMovies = new List<string>();
                foreach (JToken given in call.Args["loading_movies"] as JArray ?? new JArray())
                {
                    string movie = given.ToString().Trim();
                    if (movie.Length == 0) continue;
                    if (System.IO.Path.IsPathRooted(movie))
                    {
                        if (!System.IO.File.Exists(movie) || !LevelLoadingMovies.LooksLikeUsm(movie))
                            throw McpError.Invalid("Nothing was changed: " + movie + " is not a .usm movie (the CRI video the game plays).");
                        toCopy.Add(movie);
                        string name = System.IO.Path.GetFileName(movie).ToUpperInvariant();
                        settings.LoadingMovies.Add(name.EndsWith(".USM") ? name : System.IO.Path.GetFileNameWithoutExtension(name) + ".USM");
                    }
                    else
                    {
                        string name = System.IO.Path.GetFileName(movie);
                        if (!LevelLoadingMovies.Files(pathToAI, level).Contains(name, StringComparer.OrdinalIgnoreCase))
                            throw McpError.Invalid("Nothing was changed: " + level + "'s " + LevelLoadingMovies.FolderName + " folder has no " + name + " (give an absolute path to copy a movie in). It has: " + string.Join(", ", LevelLoadingMovies.Files(pathToAI, level)) + ".");
                        settings.LoadingMovies.Add(name);
                    }
                }
            }

            List<string> problems = settings.Problems(pathToAI);
            if (problems.Count != 0)
                throw McpError.Invalid("Nothing was changed: " + string.Join("; ", problems) + ".");
            JObject result = Describe(pathToAI, level, settings);
            if (call.Bool("dry_run"))
            {
                result["dry_run"] = true;
                return result;
            }
            foreach (string movie in toCopy)
                LevelLoadingMovies.Add(pathToAI, level, movie);
            string problem = LevelLaunchSettings.Save(pathToAI, level, settings);
            if (problem != null)
                throw new McpError(McpErrorCodes.Failed, "The launch settings could not be written: " + problem + ".");
            result = Describe(pathToAI, level, settings);
            if (string.Equals(LevelLaunch.InUseFor(pathToAI), level, StringComparison.OrdinalIgnoreCase) && EditorUtils.ThisInstallsGameRunning())
                call.Note("The game launched into " + level + " runs with the settings it was launched with: the change shows from the next launch.");
            return result;
        }
    }
}

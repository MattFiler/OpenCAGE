using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace OpenCAGE
{
    /// <summary>
    /// A level's launch settings, kept in its folder as LAUNCH_SETTINGS.JSON so a mod carries them with the level: the streaming texture
    /// heap it needs, whether characters collide with each other, and the loading playlist that plays as it loads. A launch into the level
    /// writes them into AI.exe (<see cref="LevelLaunch"/>); everything else runs with the game's own.
    /// </summary>
    public class LevelLaunchSettings
    {
        public const string FileName = "LAUNCH_SETTINGS.JSON";

        /// <summary>The heap sizes a level can ask for (MB). The game is 32-bit: the heap is reserved from its own memory at startup.</summary>
        public const int MinHeapMB = 128, MaxHeapMB = 1024;

        /// <summary>The streaming texture heap is at least this (MB); null: the player's own (DATA\ENGINE_SETTINGS.XML).</summary>
        [JsonProperty("texture_heap_mb", NullValueHandling = NullValueHandling.Ignore)] public int? TextureHeapMB;
        /// <summary>false: NPCs pass through each other (shots, melee and attacks still hit); null or true: as the game.</summary>
        [JsonProperty("npc_collisions", NullValueHandling = NullValueHandling.Ignore)] public bool? NpcCollisions;
        /// <summary>false: the player and NPCs pass through each other; null or true: as the game.</summary>
        [JsonProperty("player_npc_collisions", NullValueHandling = NullValueHandling.Ignore)] public bool? PlayerNpcCollisions;
        /// <summary>The movie (or image) playlist from DATA\GBL_ITEM.BML that plays as the level loads; null: the game's choice.</summary>
        [JsonProperty("loading_playlist", NullValueHandling = NullValueHandling.Ignore)] public string LoadingPlaylist;
        /// <summary>The level's own loading movies, by file name in its MOVIES folder (LevelLoadingMovies), in the order they play; used over LoadingPlaylist.</summary>
        [JsonProperty("loading_movies", NullValueHandling = NullValueHandling.Ignore)] public List<string> LoadingMovies;
        /// <summary>true: the level's own movies play to the end (the player can skip) instead of stopping once the level has loaded.</summary>
        [JsonProperty("loading_movies_to_end", NullValueHandling = NullValueHandling.Ignore)] public bool? LoadingMoviesToEnd;
        /// <summary>true: the game's animation memory is doubled (Launch Game's Allow larger animation memory) for launches into the level.</summary>
        [JsonProperty("larger_animation_memory", NullValueHandling = NullValueHandling.Ignore)] public bool? LargerAnimationMemory;

        [JsonIgnore] public bool IsEmpty => TextureHeapMB == null && NpcCollisions != false && PlayerNpcCollisions != false && string.IsNullOrWhiteSpace(LoadingPlaylist) && LargerAnimationMemory != true && !HasMovies;
        [JsonIgnore] public bool HasMovies => LoadingMovies != null && LoadingMovies.Any(o => !string.IsNullOrWhiteSpace(o));

        /// <summary>The settings file of a level: DATA/ENV/&lt;level&gt;/LAUNCH_SETTINGS.JSON.</summary>
        public static string PathOf(string pathToAI, string level)
        {
            return Path.Combine(pathToAI ?? "", "DATA", "ENV", (level ?? "").Replace('/', Path.DirectorySeparatorChar).Trim(Path.DirectorySeparatorChar), FileName);
        }

        /// <summary>A level's settings (empty when it has none, or its file can't be read).</summary>
        public static LevelLaunchSettings Of(string pathToAI, string level)
        {
            if (string.IsNullOrWhiteSpace(level))
                return new LevelLaunchSettings();
            string path = PathOf(pathToAI, level);
            try
            {
                if (File.Exists(path))
                    return JsonConvert.DeserializeObject<LevelLaunchSettings>(File.ReadAllText(path)) ?? new LevelLaunchSettings();
            }
            catch (Exception e)
            {
                Console.WriteLine("LevelLaunchSettings::Of - " + path + ": " + e.Message);
            }
            return new LevelLaunchSettings();
        }

        /// <summary>Write a level's settings (the file goes when they are all the game's own). Returns what went wrong, or null.</summary>
        public static string Save(string pathToAI, string level, LevelLaunchSettings settings)
        {
            string path = PathOf(pathToAI, level);
            if (string.IsNullOrWhiteSpace(level) || !Directory.Exists(Path.GetDirectoryName(path)))
                return "there is no level " + level;
            List<string> problems = settings?.Problems(pathToAI) ?? new List<string>();
            if (problems.Count != 0)
                return string.Join("; ", problems);
            try
            {
                if (settings == null || settings.IsEmpty)
                {
                    if (File.Exists(path)) File.Delete(path);
                    return null;
                }
                if (settings.NpcCollisions == true) settings.NpcCollisions = null;
                if (settings.PlayerNpcCollisions == true) settings.PlayerNpcCollisions = null;
                if (settings.LargerAnimationMemory == false) settings.LargerAnimationMemory = null;
                if (!settings.HasMovies) { settings.LoadingMovies = null; settings.LoadingMoviesToEnd = null; }
                else if (settings.LoadingMoviesToEnd == false) settings.LoadingMoviesToEnd = null;
                if (string.IsNullOrWhiteSpace(settings.LoadingPlaylist)) settings.LoadingPlaylist = null;
                File.WriteAllText(path, JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented));
                return null;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        private static readonly Regex PlaylistName = new Regex("^[A-Za-z0-9_]+$");

        /// <summary>What can't be used as it is (nothing is written while there is any).</summary>
        public List<string> Problems(string pathToAI)
        {
            List<string> problems = new List<string>();
            if (TextureHeapMB != null && (TextureHeapMB < MinHeapMB || TextureHeapMB > MaxHeapMB))
                problems.Add("the texture heap is " + MinHeapMB + " to " + MaxHeapMB + " MB");
            string playlist = LoadingPlaylist?.Trim();
            if (!string.IsNullOrEmpty(playlist))
            {
                if (!PlaylistName.IsMatch(playlist))
                    problems.Add("a playlist name is letters, digits and underscores");
                else if (playlist == LevelLoadingMovies.PlaylistName)
                    problems.Add(playlist + " is the playlist a launch makes of the level's own movies: list them as its loading movies instead");
                else if (playlist.Length > LevelLaunchPatches.MaxPlaylistName && !LevelLaunchPatches.Table.Any(o => o.Playlist == playlist))
                    problems.Add("a playlist name that isn't one of the game's level playlists is at most " + LevelLaunchPatches.MaxPlaylistName + " characters");
            }
            if (HasMovies && LoadingMovies.Count(o => !string.IsNullOrWhiteSpace(o)) > LevelLoadingMovies.MaxMovies)
                problems.Add("a level has at most " + LevelLoadingMovies.MaxMovies + " loading movies of its own");
            return problems;
        }

        /// <summary>What may not work as meant: a movie or playlist the game doesn't have, movies a mod carries unplayed, a heap past what is tested. Never stops a save.</summary>
        public List<string> Warnings(string pathToAI, string level = null)
        {
            List<string> warnings = new List<string>();
            if (level != null)
            {
                List<string> have = LevelLoadingMovies.Files(pathToAI, level);
                List<string> listed = HasMovies ? LoadingMovies.Where(o => !string.IsNullOrWhiteSpace(o)).ToList() : new List<string>();
                List<string> missing = listed.Where(o => !have.Contains(o, StringComparer.OrdinalIgnoreCase)).ToList();
                if (missing.Count != 0)
                    warnings.Add("the level's " + LevelLoadingMovies.FolderName + " folder has no " + string.Join(", ", missing) + ", so " + (missing.Count == 1 ? "it isn't" : "they aren't") + " played");
                List<string> unused = have.Where(o => !listed.Contains(o, StringComparer.OrdinalIgnoreCase)).ToList();
                if (unused.Count != 0)
                    warnings.Add("the level's " + LevelLoadingMovies.FolderName + " folder " + (listed.Count != 0 ? "also " : "") + "has " + string.Join(", ", unused) + ", which " + (unused.Count == 1 ? "isn't" : "aren't") + " played but a mod made from the level carries");
            }
            //Launch Game's Quicker Loadscreen is a loading screen without movies (a level's own loading screen goes over it)
            if ((HasMovies || !string.IsNullOrWhiteSpace(LoadingPlaylist)) && SettingsManager.GetBool(Settings.UiModLoadingScreen) &&
                (level == null || !LevelUiOverrides.Of(pathToAI, level).Keys.Any(o => o.Name == "LOADINGSCREEN")))
                warnings.Add("Launch Game's Quicker Loadscreen is on: its loading screen plays no movies, so none show while the level loads");
            string playlist = HasMovies ? null : LoadingPlaylist?.Trim();
            if (!string.IsNullOrEmpty(playlist))
            {
                List<string> known = Playlists(pathToAI);
                if (known.Count != 0 && !known.Contains(playlist))
                    warnings.Add("DATA\\GBL_ITEM.BML has no playlist " + playlist + ", so nothing plays while the level loads (add it with Configurations > Loadscreen Movies, or a mod's GBL_ITEM.BML)");
            }
            if (TextureHeapMB > 896)
                warnings.Add("a texture heap over 896 MB leaves the 32-bit game little memory of its own: test the level in game");
            return warnings;
        }

        /// <summary>The movie and image playlists DATA\GBL_ITEM.BML has, by name (empty when it can't be read).</summary>
        public static List<string> Playlists(string pathToAI)
        {
            List<string> names = new List<string>();
            try
            {
                string path = Path.Combine(pathToAI ?? "", "DATA", "GBL_ITEM.BML");
                if (!File.Exists(path))
                    return names;
                XmlDocument document = new CATHODE.BML(path).Content;
                foreach (string list in new[] { "movie_playlists", "image_playlists" })
                {
                    XmlElement playlists = document?["item_database"]?[list];
                    if (playlists == null) continue;
                    names.AddRange(playlists.ChildNodes.OfType<XmlElement>().Select(o => o.GetAttribute("playlist_name")).Where(o => !string.IsNullOrEmpty(o) && o != LevelLoadingMovies.PlaylistName));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("LevelLaunchSettings::Playlists - " + e.Message);
            }
            return names.Distinct().ToList();
        }

        /// <summary>The playlist the game plays for a level as it ships (its entry in the game's table).</summary>
        public static string GamePlaylist(string level) => LevelLaunchPatches.Table[LevelLaunchPatches.TableIndexOf(level)].Playlist;

        /// <summary>The settings in words, one line each (none when the level uses the game's own).</summary>
        public List<string> Describe()
        {
            List<string> lines = new List<string>();
            if (TextureHeapMB != null) lines.Add("texture heap at least " + TextureHeapMB + " MB");
            if (NpcCollisions == false) lines.Add("NPCs pass through each other");
            if (PlayerNpcCollisions == false) lines.Add("the player and NPCs pass through each other");
            if (HasMovies) lines.Add("own loading movies " + string.Join(", ", LoadingMovies.Where(o => !string.IsNullOrWhiteSpace(o))) + (LoadingMoviesToEnd == true ? " (played to the end)" : ""));
            else if (!string.IsNullOrWhiteSpace(LoadingPlaylist)) lines.Add("loading playlist " + LoadingPlaylist.Trim());
            if (LargerAnimationMemory == true) lines.Add("animation memory doubled");
            return lines;
        }
    }
}

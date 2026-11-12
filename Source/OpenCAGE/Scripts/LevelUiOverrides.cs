using CATHODE;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenCAGE
{
    /// <summary>
    /// A level's own versions of the game's UI movies (the radial menu, the HUD, the pause menu and the rest the binary opens by path), kept
    /// as DATA/ENV/&lt;level&gt;/UI/&lt;the game's file name&gt;. Launching into the level puts each into UI.PAK under a same-length name of
    /// OpenCAGE's own ("DATA/UI/RADIALM_LV.GFX") and points the binary's path at it, as <see cref="UiMovieOverrides"/> does; launching
    /// anything else, the game closing or OpenCAGE starting takes them out again. A level's movie is used over a UI tweak for the same movie.
    /// The entries are OpenCAGE's, not the user's work (<see cref="UiMovieOverrides.IsOwnedEntry"/>).
    /// </summary>
    public static class LevelUiOverrides
    {
        public class Movie
        {
            public string GamePath;     //as the game's binary holds it
            public string LevelPath;    //the same length: what the binary is pointed at while a level's own version is in
            public string Label;        //what it is, in words

            /// <summary>The game's file name, as a level's UI folder names its version: "RADIALMENU.GFX".</summary>
            public string FileName => GamePath.Substring(GamePath.LastIndexOf('/') + 1).ToUpperInvariant();
            /// <summary>The name the tools use for it: "RADIALMENU".</summary>
            public string Name => Path.GetFileNameWithoutExtension(FileName);
            public string OriginalEntry => GamePath.ToUpperInvariant();
            public string Entry => LevelPath.ToUpperInvariant();

            /// <summary>The Launch Game UI tweak that replaces this movie too, or null.</summary>
            public UiMovieOverrides.Movie Tweak => UiMovieOverrides.All.FirstOrDefault(o => string.Equals(o.GamePath, GamePath, StringComparison.OrdinalIgnoreCase));
        }

        /* The movie's own name, the same length: the last three letters of its name become _LV (_lv in a name written in small
           letters), so none is the game's or a UI tweak's (_OC) */
        private static Movie Make(string gamePath, string label)
        {
            int slash = gamePath.LastIndexOf('/'), dot = gamePath.LastIndexOf('.');
            string stem = gamePath.Substring(slash + 1, dot - slash - 1);
            string suffix = char.IsLower(stem[stem.Length - 1]) ? "_lv" : "_LV";
            string own = gamePath.Substring(0, slash + 1) + stem.Substring(0, stem.Length - 3) + suffix + gamePath.Substring(dot);
            return new Movie() { GamePath = gamePath, LevelPath = own, Label = label };
        }

        /// <summary>
        /// Every movie the game opens by a path in its binary (the same 26 in the Steam, GOG and Epic builds, each once), in the
        /// order a list shows them. Movies a movie loads itself (the hub's pages, the crafting components) are part of it.
        /// </summary>
        public static readonly IReadOnlyList<Movie> All = new List<Movie>()
        {
            Make("DATA/UI/RADIALMENU.GFX", "Radial menu (hold Q: inventory, items)"),
            Make("DATA/UI/AGGREGATE.GFX", "HUD (prompts, pickups, health, messages)"),
            Make("DATA/UI/WeaponStuff.gfx", "Weapon HUD (crosshair, ammo, reloading)"),
            Make("DATA/UI/MOTIONTRACKER.GFX", "Motion tracker screen"),
            Make("DATA/UI/HUB.GFX", "Hub (Tab: map, inventory, logs, missions)"),
            Make("DATA/UI/CRAFTINGSCREEN.GFX", "Crafting screen"),
            Make("DATA/UI/SUBTITLES.GFX", "Subtitles"),
            Make("DATA/UI/PAUSEMENU.GFX", "Pause menu"),
            Make("DATA/UI/GAMEOVERMENU.GFX", "Game-over screen"),
            Make("data/UI/loadingscreen.gfx", "Loading screen"),
            Make("DATA/UI/TERMINALINTERACTION.GFX", "Computer terminals"),
            Make("DATA/UI/TERMINALINTERACTIONATTRACT.GFX", "Computer terminals (idle screen)"),
            Make("DATA/UI/KEYPAD_CONTROL.GFX", "Keypad (entering a code)"),
            Make("DATA/UI/KEYPAD_DISPLAY.GFX", "Keypad display"),
            Make("DATA/UI/HACKINGV4.GFX", "Access tuner (hacking)"),
            Make("DATA/UI/HackingUpgrade.GFX", "Access tuner upgrade"),
            Make("DATA/UI/screenHackingTerminal.gfx", "Hacking terminal screen"),
            Make("DATA/UI/REWIREV2.GFX", "Rewire"),
            Make("DATA/UI/REACTION_GAME.GFX", "Reaction game (button prompts)"),
            Make("DATA/UI/MINIGAMES.GFX", "Minigames"),
            Make("DATA/UI/MOVIEPLAYER_RTT.GFX", "Video player (on in-world screens)"),
            Make("DATA/UI/NEWFRONTENDMENU.GFX", "Main menu"),
            Make("DATA/UI/ATTRACTSCREEN.GFX", "Title screen (press any button)"),
            Make("DATA/UI/SPLASHSCREEN.GFX", "Splash screen"),
            Make("DATA/UI/GAMMASCREEN.GFX", "Brightness screen"),
            Make("DATA/UI/CREDITSSCREEN.GFX", "Credits"),
        };

        /// <summary>A movie by its name ("RADIALMENU"), file name ("RADIALMENU.GFX") or path as the game holds it; null when none.</summary>
        public static Movie Find(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;
            string wanted = name.Replace('\\', '/').Trim().Trim('/');
            string file = wanted.Substring(wanted.LastIndexOf('/') + 1);
            return All.FirstOrDefault(o => string.Equals(o.GamePath, wanted, StringComparison.OrdinalIgnoreCase) || string.Equals(o.FileName, file, StringComparison.OrdinalIgnoreCase)
                || string.Equals(o.Name, file, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>An entry of UI.PAK that is a level's movie put in by a launch (OpenCAGE's own, not the game's or the user's).</summary>
        public static bool IsOwnedEntry(string entryName)
        {
            string name = (entryName ?? "").Replace('\\', '/').TrimStart('/');
            return All.Any(o => string.Equals(name, o.Entry, StringComparison.OrdinalIgnoreCase));
        }

        #region THE LEVEL'S FILES
        /// <summary>The folder in a level's folder that holds its own UI movies.</summary>
        public const string FolderName = "UI";

        /// <summary>A level's UI folder: DATA/ENV/&lt;level&gt;/UI ("PRODUCTION/BSP_TORRENS", "MYLEVEL").</summary>
        public static string Folder(string pathToAI, string level)
        {
            return Path.Combine(pathToAI ?? "", "DATA", "ENV", (level ?? "").Replace('/', Path.DirectorySeparatorChar).Trim(Path.DirectorySeparatorChar), FolderName);
        }

        /// <summary>The level's own movies: each movie it has a file for, and the file.</summary>
        public static Dictionary<Movie, string> Of(string pathToAI, string level)
        {
            Dictionary<Movie, string> found = new Dictionary<Movie, string>();
            if (string.IsNullOrWhiteSpace(level))
                return found;
            string folder = Folder(pathToAI, level);
            if (!Directory.Exists(folder))
                return found;
            foreach (string file in Directory.GetFiles(folder))
            {
                Movie movie = All.FirstOrDefault(o => string.Equals(o.FileName, Path.GetFileName(file), StringComparison.OrdinalIgnoreCase));
                if (movie != null && !found.ContainsKey(movie))
                    found.Add(movie, file);
            }
            return found;
        }

        /// <summary>Files in the level's UI folder that are not one of the game's movies (so nothing uses them).</summary>
        public static List<string> Unused(string pathToAI, string level)
        {
            string folder = Folder(pathToAI, level);
            if (string.IsNullOrWhiteSpace(level) || !Directory.Exists(folder))
                return new List<string>();
            return Directory.GetFiles(folder).Where(file => !All.Any(o => string.Equals(o.FileName, Path.GetFileName(file), StringComparison.OrdinalIgnoreCase))).ToList();
        }

        /// <summary>Whether bytes look like a Scaleform movie (GFX, or the SWF it is made from; either compressed or not).</summary>
        public static bool LooksLikeMovie(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 8)
                return false;
            string magic = Encoding.ASCII.GetString(bytes, 0, 3);
            return magic == "GFX" || magic == "CFX" || magic == "FWS" || magic == "CWS";
        }

        /// <summary>
        /// Give a level its own version of a movie (the file copied into its UI folder) - or, with null, take it away. Used from
        /// the next launch into the level. Returns what went wrong, or null.
        /// </summary>
        public static string Set(string pathToAI, string level, Movie movie, string sourceFile)
        {
            if (movie == null)
                return "not one of the game's UI movies";
            if (string.IsNullOrWhiteSpace(level) || !Directory.Exists(Path.GetDirectoryName(Folder(pathToAI, level))))
                return "there is no level " + level;
            string folder = Folder(pathToAI, level);
            try
            {
                //A movie the game can't make a root object from crashes it on its first call to it: refused before anything is
                //touched, so the level's own version stays as it was (Hud\HudMovieCheck.cs)
                if (sourceFile != null)
                {
                    byte[] candidate = File.ReadAllBytes(sourceFile);
                    if (LooksLikeMovie(candidate))
                    {
                        Hud.MovieCheckResult check = Hud.HudMovieCheck.Check(candidate);
                        if (!check.Ok)
                            return Path.GetFileName(sourceFile) + " was refused - the game could not run it and would crash on its first call to it: " + check.Errors[0];
                    }
                }
                //Whatever spelling the folder has it in now goes (one file per movie)
                foreach (KeyValuePair<Movie, string> had in Of(pathToAI, level).Where(o => o.Key == movie))
                    File.Delete(had.Value);
                if (sourceFile == null)
                {
                    if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                        Directory.Delete(folder);
                    return null;
                }
                byte[] bytes = File.ReadAllBytes(sourceFile);
                if (!LooksLikeMovie(bytes))
                    return Path.GetFileName(sourceFile) + " is not a Scaleform movie (a .gfx, or the .swf it is made from)";
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, movie.FileName), bytes);
                return null;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        /// <summary>The game's own version of a movie, from UI.PAK (null when it has none) - a starting point for a level's own.</summary>
        public static byte[] GameVersion(string pathToAI, Movie movie)
        {
            string uiPak = Path.Combine(pathToAI ?? "", "DATA", "UI.PAK");
            if (movie == null || !File.Exists(uiPak))
                return null;
            PAK2 pak = new PAK2(uiPak);
            return pak.Loaded ? EntryOf(pak, movie.OriginalEntry)?.Content : null;
        }
        #endregion

        #region WHAT THE GAME USES
        private static PAK2.File EntryOf(PAK2 pak, string name) =>
            pak.Entries.FirstOrDefault(o => string.Equals(o.Filename, name, StringComparison.OrdinalIgnoreCase));

        //The movie paths the binary holds now, read once per version of it (where, size, written)
        private static readonly object _exeLock = new object();
        private static string _exeSeen;
        private static HashSet<string> _exePaths = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Every UI movie path ("DATA/UI/..." or "data/UI/...", whole and zero-ended) the game's binary holds now.</summary>
        private static HashSet<string> ExeMoviePaths(string pathToAI)
        {
            string exe = Path.Combine(pathToAI ?? "", "AI.exe");
            lock (_exeLock)
            {
                FileInfo info = new FileInfo(exe);
                if (!info.Exists)
                    return new HashSet<string>(StringComparer.Ordinal);
                string seen = info.FullName.ToUpperInvariant() + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks;
                if (seen == _exeSeen)
                    return _exePaths;
                HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
                byte[] binary = File.ReadAllBytes(exe);
                byte[] upper = Encoding.ASCII.GetBytes("DATA/UI/"), lower = Encoding.ASCII.GetBytes("data/UI/");
                for (int i = 1; i + 8 < binary.Length; i++)
                {
                    if (binary[i - 1] != 0 || (binary[i] != 'D' && binary[i] != 'd'))
                        continue;
                    byte[] prefix = binary[i] == 'D' ? upper : lower;
                    int j = 1;
                    while (j < prefix.Length && binary[i + j] == prefix[j]) j++;
                    if (j != prefix.Length)
                        continue;
                    int end = i + prefix.Length;
                    while (end < binary.Length && end - i < 128 && binary[end] >= 0x20 && binary[end] < 0x7F) end++;
                    if (end < binary.Length && binary[end] == 0)
                        paths.Add(Encoding.ASCII.GetString(binary, i, end - i));
                }
                _exeSeen = seen;
                _exePaths = paths;
                return paths;
            }
        }

        /// <summary>Whether the game is pointed at a level's version of this movie (the path in its binary is OpenCAGE's).</summary>
        public static bool PointsAtLevelMovie(string pathToAI, Movie movie)
        {
            try
            {
                return movie != null && ExeMoviePaths(pathToAI).Contains(movie.LevelPath);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>The level movie the game is pointed at for a path as the binary holds it (a UI tweak's movie, say), or null.</summary>
        public static Movie PointedAtFor(string pathToAI, string gamePath)
        {
            Movie movie = All.FirstOrDefault(o => string.Equals(o.GamePath, gamePath, StringComparison.OrdinalIgnoreCase));
            return movie != null && PointsAtLevelMovie(pathToAI, movie) ? movie : null;
        }

        /// <summary>The movies the game is pointed at a level's version of now.</summary>
        public static List<Movie> InUse(string pathToAI)
        {
            try
            {
                HashSet<string> paths = ExeMoviePaths(pathToAI);
                return All.Where(o => paths.Contains(o.LevelPath)).ToList();
            }
            catch
            {
                return new List<Movie>();
            }
        }

        /// <summary>The level whose movies the last launch put in (as recorded then), or null when none are in.</summary>
        public static string InUseFor(string pathToAI)
        {
            try
            {
                string record = Path.Combine(CacheFolder(pathToAI), RecordName);
                return File.Exists(record) && InUse(pathToAI).Count != 0 ? File.ReadAllText(record).Trim() : null;
            }
            catch
            {
                return null;
            }
        }

        /* Copies of the movies a launch put in, kept where the Mod Manager never looks (DATA/MODTOOLS), so it can put them back
           in UI.PAK when it writes the file while they are in use, and the level they came from */
        private static string CacheFolder(string pathToAI) => Path.Combine(pathToAI ?? "", "DATA", "MODTOOLS", "UI_OVERRIDES");
        private const string RecordName = "LEVEL.TXT";
        private static string CachedCopy(string pathToAI, Movie movie) => Path.Combine(CacheFolder(pathToAI), Path.GetFileName(movie.Entry));
        #endregion

        #region APPLY
        //Launches, the watch on the game and OpenCAGE starting all write the same two files: one at a time, and a launch makes
        //any watch from before it stand down
        private static readonly object _lock = new object();
        private static int _generation;

        /// <summary>
        /// What a launch into a level writes for the UI: the level's own movies in, the movies of an earlier launch out, then the
        /// Launch Game UI tweaks as their settings say (which leave a movie the level replaces to the level). level null: none
        /// of a level's (the main menu, or a put-back). Returns what went wrong, or null; applied lists the level's movies now in.
        /// </summary>
        public static string ApplyForLaunch(string pathToAI, string level, out List<Movie> applied)
        {
            lock (_lock)
            {
                _generation++;
                List<string> problems = new List<string>();
                string own = Write(pathToAI, Of(pathToAI, level), level, out applied);
                if (own != null) problems.Add(own);
                string tweaks = UiMovieOverrides.Sync(pathToAI);
                if (tweaks != null) problems.Add(tweaks);
                return problems.Count == 0 ? null : string.Join("; ", problems);
            }
        }

        /// <summary>
        /// A launch's level movies taken out and the game pointed back (to the game's own, or a UI tweak's where one is on).
        /// Returns what went wrong, or null.
        /// </summary>
        public static string PutBack(string pathToAI)
        {
            lock (_lock)
            {
                _generation++;
                return PutBackLocked(pathToAI);
            }
        }

        private static string PutBackLocked(string pathToAI)
        {
            List<string> problems = new List<string>();
            string own = Write(pathToAI, new Dictionary<Movie, string>(), null, out _);
            if (own != null) problems.Add(own);
            string tweaks = UiMovieOverrides.Sync(pathToAI);
            if (tweaks != null) problems.Add(tweaks);
            return problems.Count == 0 ? null : string.Join("; ", problems);
        }

        /* The level's movies (wanted) in UI.PAK and the binary pointed at them; every other movie's level version out, the binary
           pointed back to the game's own path first (so it never points at an entry that isn't there) */
        private static string Write(string pathToAI, Dictionary<Movie, string> wanted, string level, out List<Movie> applied)
        {
            applied = new List<Movie>();
            List<string> problems = new List<string>();
            string uiPak = Path.Combine(pathToAI, "DATA", "UI.PAK");
            if (!File.Exists(uiPak))
                return wanted.Count == 0 ? null : "the game has no DATA/UI.PAK";
            try
            {
                List<Movie> inUse = InUse(pathToAI);
                PAK2 pak = new PAK2(uiPak);
                if (!pak.Loaded)
                    return "UI.PAK could not be read";

                //The level's movies, read and checked first
                Dictionary<Movie, byte[]> bytes = new Dictionary<Movie, byte[]>();
                foreach (KeyValuePair<Movie, string> movie in wanted)
                {
                    byte[] content = File.ReadAllBytes(movie.Value);
                    if (!LooksLikeMovie(content))
                    {
                        problems.Add(Path.GetFileName(movie.Value) + " in " + level + "'s UI folder is not a Scaleform movie, so the game's own " + movie.Key.Name + " is used");
                        continue;
                    }
                    bytes.Add(movie.Key, content);
                }

                //Out: the binary pointed back first, then the entry taken out of UI.PAK (left in when the binary couldn't be)
                bool pakChanged = false;
                foreach (Movie movie in All.Where(o => !bytes.ContainsKey(o)))
                {
                    if (inUse.Contains(movie) && !PatchManager.PatchFilePath(pathToAI, movie.GamePath, movie.LevelPath, false))
                    {
                        problems.Add("AI.exe could not be pointed back to the game's " + movie.Name + " (is the game open?)");
                        continue;
                    }
                    PAK2.File entry = EntryOf(pak, movie.Entry);
                    if (entry != null)
                    {
                        pak.Entries.Remove(entry);
                        pakChanged = true;
                    }
                }

                //In: the entries first (the binary is pointed at them once they are on disk)
                foreach (KeyValuePair<Movie, byte[]> movie in bytes)
                {
                    PAK2.File entry = EntryOf(pak, movie.Key.Entry);
                    if (entry == null)
                    {
                        pak.Entries.Add(new PAK2.File() { Filename = movie.Key.Entry, Content = movie.Value });
                        pakChanged = true;
                    }
                    else if (entry.Content == null || !entry.Content.AsSpan().SequenceEqual(movie.Value))
                    {
                        entry.Content = movie.Value;
                        pakChanged = true;
                    }
                }
                if (pakChanged)
                {
                    //Kept first if it's still as shipped - but this isn't the user's edit (the Mod Manager looks past these entries)
                    Modding.ModServices.CaptureBeforeWrite(uiPak, usersEdit: false);
                    if (!pak.Save())
                    {
                        problems.Add("UI.PAK could not be written (is the game open?), so the game uses its own UI");
                        return string.Join("; ", problems);
                    }
                }

                foreach (KeyValuePair<Movie, byte[]> movie in bytes)
                {
                    //A UI tweak pointing the same path at its movie gives way: the level's is used
                    UiMovieOverrides.Movie tweak = movie.Key.Tweak;
                    if (tweak != null && PatchManager.HasFilePath(pathToAI, tweak.OwnPath))
                        PatchManager.PatchFilePath(pathToAI, tweak.GamePath, tweak.OwnPath, false);
                    if (!PatchManager.PatchFilePath(pathToAI, movie.Key.GamePath, movie.Key.LevelPath, true))
                    {
                        problems.Add("AI.exe could not be pointed at " + level + "'s " + movie.Key.Name + " (is the game open?)");
                        continue;
                    }
                    applied.Add(movie.Key);
                }

                List<Movie> pointed = applied;
                KeepCopies(pathToAI, bytes.Where(o => pointed.Contains(o.Key)).ToDictionary(o => o.Key, o => o.Value), pointed.Count != 0 ? level : null);
            }
            catch (Exception e)
            {
                problems.Add("the level's UI: " + e.Message);
            }
            return problems.Count == 0 ? null : string.Join("; ", problems);
        }

        /* The copies the Mod Manager puts back from, and the level they came from: one for each movie the game is pointed at
           now (written: what was just put in; kept: one that couldn't be taken out). Level: the launch's, null to keep the record. */
        private static void KeepCopies(string pathToAI, Dictionary<Movie, byte[]> written, string level)
        {
            try
            {
                string folder = CacheFolder(pathToAI);
                List<Movie> inUse = InUse(pathToAI);
                if (inUse.Count == 0)
                {
                    if (Directory.Exists(folder))
                        Directory.Delete(folder, true);
                    return;
                }
                Directory.CreateDirectory(folder);
                foreach (Movie movie in All)
                {
                    string copy = CachedCopy(pathToAI, movie);
                    if (written.TryGetValue(movie, out byte[] content))
                        File.WriteAllBytes(copy, content);
                    else if (!inUse.Contains(movie) && File.Exists(copy))
                        File.Delete(copy);
                }
                if (level != null)
                    File.WriteAllText(Path.Combine(folder, RecordName), level);
            }
            catch { } //only the Mod Manager's putting them back needs these
        }

        /// <summary>
        /// For the Mod Manager, whose own work on UI.PAK is without OpenCAGE's entries: the level movies the game is pointed at
        /// put back in an open UI.PAK (from the copies the launch kept). Returns whether it changed.
        /// </summary>
        public static bool AddInUse(PAK2 pak, string pathToAI)
        {
            bool changed = false;
            foreach (Movie movie in InUse(pathToAI))
            {
                string copy = CachedCopy(pathToAI, movie);
                if (!File.Exists(copy))
                    continue;
                byte[] content = File.ReadAllBytes(copy);
                PAK2.File entry = EntryOf(pak, movie.Entry);
                if (entry == null)
                    pak.Entries.Add(new PAK2.File() { Filename = movie.Entry, Content = content });
                else if (entry.Content == null || !entry.Content.AsSpan().SequenceEqual(content))
                    entry.Content = content;
                else
                    continue;
                changed = true;
            }
            return changed;
        }
        #endregion

        #region PUTTING BACK
        /// <summary>
        /// After a launch that put a level's movies in: once this install's game has come and gone (closed for 15 seconds - the
        /// Epic build restarts itself through its launcher), they come out again, so the game started any other way (from Steam,
        /// say) has its own UI. A later launch makes this stand down. Never while the game runs, or while the Mod Manager or a
        /// save writes the game files.
        /// </summary>
        public static void PutBackWhenGameCloses(string pathToAI)
        {
            //Nothing of a level's in: nothing to take out
            if (InUse(pathToAI).Count == 0)
                return;
            int generation;
            lock (_lock) generation = _generation;
            Task.Run(async () =>
            {
                try
                {
                    Stopwatch waited = Stopwatch.StartNew();
                    //The game shows up within a few seconds (Steam starts it itself, after its own checks): three minutes at most
                    while (waited.Elapsed < TimeSpan.FromMinutes(3) && !GameRunning())
                    {
                        if (Volatile.Read(ref _generation) != generation) return;
                        await Task.Delay(2000);
                    }
                    Stopwatch gone = null;
                    while (true)
                    {
                        if (Volatile.Read(ref _generation) != generation) return;
                        if (GameRunning())
                            gone = null;
                        else if (gone == null)
                            gone = Stopwatch.StartNew();
                        else if (gone.Elapsed > TimeSpan.FromSeconds(15) && !Busy())
                            break;
                        await Task.Delay(2000);
                    }
                    lock (_lock)
                    {
                        if (_generation != generation || GameRunning() || !string.Equals(Singleton.PathToAI, pathToAI, StringComparison.OrdinalIgnoreCase))
                            return;
                        _generation++;
                        if (InUse(pathToAI).Count != 0)
                        {
                            string problem = PutBackLocked(pathToAI);
                            Console.WriteLine("LevelUiOverrides: game closed, the level's UI movies taken out" + (problem != null ? " (" + problem + ")" : ""));
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("LevelUiOverrides::PutBackWhenGameCloses - " + e);
                }
            });
        }

        /// <summary>
        /// As OpenCAGE starts: level movies an earlier launch left in (OpenCAGE closed before the game did) come out, unless the
        /// game is running. Returns whether any did.
        /// </summary>
        public static bool PutBackAtStartup(string pathToAI)
        {
            try
            {
                if (InUse(pathToAI).Count == 0 || EditorUtils.ThisInstallsGameRunning())
                    return false;
                PutBack(pathToAI);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool GameRunning()
        {
            try
            {
                return EditorUtils.ThisInstallsGameRunning();
            }
            catch
            {
                return true;
            }
        }

        /* The Mod Manager applying, or a backup / level write: not now */
        private static bool Busy() => Singleton.CurrentBackupState != Singleton.BackupState.NONE;
        #endregion

        #region DESCRIBING
        /// <summary>
        /// A line for Launch Game about a level's own movies: which, and which UI tweak each one is used over. Null when the level
        /// has none.
        /// </summary>
        public static string Describe(string pathToAI, string level)
        {
            Dictionary<Movie, string> own = Of(pathToAI, level);
            if (own.Count == 0)
                return null;
            List<string> names = own.Keys.OrderBy(o => All.ToList().IndexOf(o)).Select(o => o.Label.Split(new[] { " (" }, StringSplitOptions.None)[0]).ToList();
            string text = "This level's own UI: " + string.Join(", ", names) + ".";
            List<string> beaten = own.Keys.Where(o => o.Tweak != null && UiMovieOverrides.IsOn(o.Tweak)).Select(o => TweakName(o.Tweak)).ToList();
            if (beaten.Count != 0)
                text += " Used instead of " + string.Join(", ", beaten) + ".";
            return text;
        }

        /// <summary>The Launch Game name of a UI tweak.</summary>
        public static string TweakName(UiMovieOverrides.Movie tweak)
        {
            switch (tweak?.Key)
            {
                case Settings.UiModPauseMenu: return "Debug Checkpoints";
                case Settings.UiModNewFrontendMenu: return "Level Select";
                case Settings.UiModLoadingScreen: return "Quicker Loadscreen";
                case Settings.UiModGameOverMenu: return "Quit Option On Deathscreen";
                default: return tweak?.Resource;
            }
        }
        #endregion
    }
}

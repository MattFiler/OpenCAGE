using CATHODE;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OpenCAGE
{
    /// <summary>
    /// The Launch Game UI options - debug checkpoints in the pause menu, level selection in the main menu, the quicker loading
    /// screen with the level's name, and quit to menu on the game-over screen - as OpenCAGE's own movies in UI.PAK, BESIDE the
    /// game's rather than over them. (An option Launch Game drops is flagged <see cref="Movie.Removed"/>; none is now - the
    /// first two were dropped on 22 Oct 2026 and brought back on 25 Oct.)
    ///
    /// The game opens each of these movies by a path held in its binary ("DATA/UI/PAUSEMENU.GFX"). OpenCAGE used to write
    /// its version over that entry, which made UI.PAK read as the user's own change everywhere the Mod Manager looks, and
    /// fought any mod that changed the same movie. Now the movie goes in under a name of its own, the same length as the
    /// game's ("DATA/UI/PAUSEM_OC.GFX"), and the path in the binary is pointed at it while the option is on. The shipped
    /// entry is never touched, so turning an option off is just pointing the path back.
    ///
    /// <see cref="IsOwnedEntry"/> is the rule the rest of OpenCAGE keys off: those entries are OpenCAGE's, not the user's
    /// work - the Mod Manager doesn't count them as a change, Create a Mod doesn't offer them, and the UI editor hides them.
    /// </summary>
    public static class UiMovieOverrides
    {
        public class Movie
        {
            public string Key;          //the setting
            public string Resource;     //the embedded movies' name: "PAUSEMENU" (UI_Mods/PAUSEMENU.GFX as shipped, PAUSEMENU_MOD.GFX OpenCAGE's)
            public string GamePath;     //as the game's binary holds it
            public string OwnPath;      //the same length: what the binary is pointed at while the option is on

            //SHA-256 of OpenCAGE's versions of this movie before the one embedded now: older OpenCAGE wrote them over the game's
            public string[] OlderVersions = new string[0];

            //An option Launch Game no longer has (the Debug Menu does it now): always off, whatever its old setting says, so a
            //launch takes out the movie an older OpenCAGE left in. Still listed, so its entry is known as OpenCAGE's own.
            public bool Removed;

            public string OriginalEntry => GamePath.ToUpperInvariant();
            public string Entry => OwnPath.ToUpperInvariant();
        }

        public static readonly IReadOnlyList<Movie> All = new List<Movie>()
        {
            //The pause menu's was rebuilt in 2026: OpenCAGE shipped the one before from 2020 until then (and the separate
            //LaunchGame tool before it). The other three have only ever had one version (OpenCAGE, LaunchGame and
            //CommandsEditor histories, 17 Oct 2026)
            //The pause menu's debug checkpoints and the main menu's level selection were dropped from Launch Game on 22 Oct 2026
            //(for the Debug Menu's Game > Tools and Game > Game flow) and brought back on 25 Oct, beside them
            new Movie() { Key = Settings.UiModPauseMenu, Resource = "PAUSEMENU", GamePath = "DATA/UI/PAUSEMENU.GFX", OwnPath = "DATA/UI/PAUSEM_OC.GFX",
                          OlderVersions = new[] { "b7403a238e91de6274de9aee86288b001e188a2cb358f3690c68b591b97cfbff" } },
            new Movie() { Key = Settings.UiModLoadingScreen, Resource = "LOADINGSCREEN", GamePath = "data/UI/loadingscreen.gfx", OwnPath = "data/UI/loadingscr_oc.gfx" },
            new Movie() { Key = Settings.UiModNewFrontendMenu, Resource = "NEWFRONTENDMENU", GamePath = "DATA/UI/NEWFRONTENDMENU.GFX", OwnPath = "DATA/UI/NEWFRONTENDM_OC.GFX" },
            new Movie() { Key = Settings.UiModGameOverMenu, Resource = "GAMEOVERMENU", GamePath = "DATA/UI/GAMEOVERMENU.GFX", OwnPath = "DATA/UI/GAMEOVERM_OC.GFX" },
        };

        public static Movie Find(string key) => All.FirstOrDefault(o => o.Key == key);

        /// <summary>Whether an option is on: as its setting says, except one Launch Game no longer has, which is always off.</summary>
        public static bool IsOn(Movie movie) => !movie.Removed && SettingsManager.GetBool(movie.Key);

        /// <summary>An entry of UI.PAK that is OpenCAGE's own (one of the option movies), not the game's or the user's.</summary>
        public static bool IsOwnedEntry(string entryName)
        {
            string name = (entryName ?? "").Replace('\\', '/').TrimStart('/');
            return All.Any(o => string.Equals(name, o.Entry, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>UI.PAK, as a path relative to the game folder the way the Mod Manager names files.</summary>
        public static bool IsUiPak(string normalisedPath) => string.Equals(normalisedPath, "DATA/UI.PAK", StringComparison.OrdinalIgnoreCase);

        #region MOVIES
        private static readonly Dictionary<string, byte[]> _resources = new Dictionary<string, byte[]>();

        /// <summary>The game's own movie (as shipped) or OpenCAGE's version of it, from the copies embedded in OpenCAGE.</summary>
        public static byte[] Embedded(Movie movie, bool modded)
        {
            string path = "UI_Mods." + movie.Resource + (modded ? "_MOD" : "") + ".GFX";
            lock (_resources)
            {
                if (_resources.TryGetValue(path, out byte[] known)) return known;
                Assembly assembly = typeof(UiMovieOverrides).Assembly;
                string name = assembly.GetManifestResourceNames().FirstOrDefault(o => o.EndsWith("." + path, StringComparison.OrdinalIgnoreCase));
                byte[] bytes = null;
                if (name != null)
                    using (Stream stream = assembly.GetManifestResourceStream(name))
                    using (MemoryStream copy = new MemoryStream())
                    {
                        stream.CopyTo(copy);
                        bytes = copy.ToArray();
                    }
                _resources[path] = bytes;
                return bytes;
            }
        }

        private static PAK2.File EntryOf(PAK2 pak, string name) =>
            pak.Entries.FirstOrDefault(o => string.Equals(o.Filename, name, StringComparison.OrdinalIgnoreCase));

        private static bool Same(byte[] a, byte[] b) => a != null && b != null && a.AsSpan().SequenceEqual(b);
        #endregion

        #region STATE
        /// <summary>
        /// Where one option stands: "on" (OpenCAGE's movie is in and the game is pointed at it), "off" (neither), or "partly"
        /// (one without the other - a check of the game files puts the binary back, say). "edited" when OpenCAGE's entry
        /// holds something other than its movie.
        /// </summary>
        public static string State(PAK2 pak, string pathToAI, Movie movie)
        {
            PAK2.File own = EntryOf(pak, movie.Entry);
            if (own != null && !Same(own.Content, Embedded(movie, true)))
                return "edited";
            bool redirected = PatchManager.HasFilePath(pathToAI, movie.OwnPath);
            if (own != null && redirected) return "on";
            if (own == null && !redirected && !OverwrittenByOldVersion(pak, movie)) return "off";
            return "partly";
        }

        /* Older OpenCAGE wrote its movie over the game's own entry */
        private static bool OverwrittenByOldVersion(PAK2 pak, Movie movie) => IsOpenCageMovie(movie, EntryOf(pak, movie.OriginalEntry)?.Content);

        /* Any version of OpenCAGE's movie that's ever shipped: the one embedded now, or an older one */
        private static bool IsOpenCageMovie(Movie movie, byte[] content)
        {
            if (content == null)
                return false;
            if (Same(content, Embedded(movie, true)))
                return true;
            if (movie.OlderVersions.Length == 0)
                return false;
            using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
            {
                string hex = BitConverter.ToString(sha.ComputeHash(content)).Replace("-", "").ToLowerInvariant();
                return movie.OlderVersions.Contains(hex);
            }
        }
        #endregion

        #region APPLY
        /// <summary>
        /// Put one option's movie in place (or take it out) in an open UI.PAK, and point the game at it (or back). The PAK is
        /// changed in memory only; the caller saves it. Also puts back the game's own movie where an older OpenCAGE wrote over
        /// it. Returns what went wrong, or null.
        /// </summary>
        public static string Apply(PAK2 pak, string pathToAI, Movie movie, bool on, out bool pakChanged)
        {
            pakChanged = false;
            PAK2.File original = EntryOf(pak, movie.OriginalEntry);
            if (original == null)
                return "UI.PAK has no " + movie.OriginalEntry + " (check the game's files)";
            if (OverwrittenByOldVersion(pak, movie))
            {
                byte[] shipped = Embedded(movie, false);
                if (shipped == null)
                    return "OpenCAGE's copy of the game's own " + movie.Resource + " movie is missing";
                original.Content = shipped;
                pakChanged = true;
            }

            PAK2.File own = EntryOf(pak, movie.Entry);
            if (on)
            {
                byte[] modded = Embedded(movie, true);
                if (modded == null)
                    return "OpenCAGE's " + movie.Resource + " movie is missing";
                if (own == null)
                {
                    pak.Entries.Add(new PAK2.File() { Filename = movie.Entry, Content = modded });
                    pakChanged = true;
                }
                else if (!Same(own.Content, modded))
                {
                    own.Content = modded;
                    pakChanged = true;
                }
            }
            else if (own != null)
            {
                pak.Entries.Remove(own);
                pakChanged = true;
            }

            if (!PatchManager.PatchFilePath(pathToAI, movie.GamePath, movie.OwnPath, on))
                return "AI.exe could not be " + (on ? "pointed at" : "pointed back from") + " OpenCAGE's " + movie.Resource + " movie (is the game open?)";
            return null;
        }

        /// <summary>
        /// Every option as its setting says: in place or not, and the game pointed accordingly. For when the game files may
        /// have been checked or the binary replaced since (before a launch, after a save) and to move an install an older
        /// OpenCAGE wrote over the game's movies onto the new way. Writes UI.PAK only when something in it has to change.
        /// Returns what went wrong, or null.
        /// </summary>
        public static string Sync(string pathToAI)
        {
            string uiPak = Path.Combine(pathToAI, "DATA", "UI.PAK");
            if (!File.Exists(uiPak))
                return null;
            try
            {
                PutBackOldMenus(pathToAI);
                PAK2 pak = new PAK2(uiPak);
                if (!pak.Loaded)
                    return "UI.PAK could not be read";
                List<string> problems = new List<string>();
                bool changed = false;
                foreach (Movie movie in All)
                {
                    bool on = IsOn(movie);
                    //An entry of OpenCAGE's name holding something else was put there by hand: left alone
                    if (on && State(pak, pathToAI, movie) == "edited")
                        continue;
                    string problem = Apply(pak, pathToAI, movie, on, out bool pakChanged);
                    if (problem != null) problems.Add(problem);
                    changed |= pakChanged;
                }
                if (changed)
                {
                    //Its original kept first if it's still as shipped - but this isn't the user's edit (the Mod Manager looks past these entries)
                    Modding.ModServices.CaptureBeforeWrite(uiPak, usersEdit: false);
                    if (!pak.Save())
                        problems.Add("UI.PAK could not be written (is the game open?)");
                }
                return problems.Count == 0 ? null : string.Join("; ", problems);
            }
            catch (Exception e)
            {
                return "UI.PAK: " + e.Message;
            }
        }

        /// <summary>
        /// Put the game's own menus back wherever an older OpenCAGE wrote its movies over them in UI.PAK - in the file, and in
        /// what the Mod Manager keeps of it (the original it adopted, the user's own version under mods), so nothing reads as
        /// anyone's change. Goes by what the entries hold (any version of OpenCAGE's movies there's been), not by the settings:
        /// the old ones can have been turned off since, or set by another copy of OpenCAGE. A UI.PAK is checked once per
        /// version of it (where it is, its size and when it was written). True when something was put back.
        /// </summary>
        public static bool PutBackOldMenus(string pathToAI)
        {
            try
            {
                string uiPak = Path.GetFullPath(Path.Combine(pathToAI ?? "", "DATA", "UI.PAK"));
                FileInfo info = new FileInfo(uiPak);
                if (!info.Exists)
                    return false;
                string Seen() { info.Refresh(); return uiPak.ToUpperInvariant() + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks; }
                if (SettingsManager.GetString(Settings.UiOptionsCheckedPak) == Seen())
                    return false;
                bool putBack = Modding.ModServices.RewriteEverywhere(uiPak, WithoutOldMenus);
                SettingsManager.SetString(Settings.UiOptionsCheckedPak, Seen());
                return putBack;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>UI.PAK's bytes with the game's own menus back where OpenCAGE's (any version) were written over them, or null when there are none.</summary>
        public static byte[] WithoutOldMenus(byte[] uiPak)
        {
            try
            {
                PAK2 pak = new PAK2(uiPak);
                if (!pak.Loaded)
                    return null;
                bool changed = false;
                foreach (Movie movie in All)
                {
                    PAK2.File entry = EntryOf(pak, movie.OriginalEntry);
                    byte[] shipped = Embedded(movie, false);
                    if (entry == null || shipped == null || !IsOpenCageMovie(movie, entry.Content))
                        continue;
                    entry.Content = shipped;
                    changed = true;
                }
                return changed ? BytesOf(pak) : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// UI.PAK's bytes as they'd be without OpenCAGE's own entries, or null when it has none (or can't be read) - so the
        /// Mod Manager can tell whether what's left differs from the game's own.
        /// </summary>
        public static byte[] WithoutOwnedEntries(byte[] uiPak)
        {
            try
            {
                PAK2 pak = new PAK2(uiPak);
                if (!pak.Loaded || !pak.Entries.Any(o => IsOwnedEntry(o.Filename)))
                    return null;
                pak.Entries.RemoveAll(o => IsOwnedEntry(o.Filename));
                return BytesOf(pak);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// UI.PAK's bytes with OpenCAGE's own entries as the settings say - for the Mod Manager, whose own work on UI.PAK is
        /// without them - or null when they're already so (or it can't be read). The game's pointing at them is the binary's
        /// business, which the Mod Manager never touches.
        /// </summary>
        public static byte[] WithOwnedEntries(byte[] uiPak)
        {
            try
            {
                PAK2 pak = new PAK2(uiPak);
                if (!pak.Loaded)
                    return null;
                bool changed = false;
                foreach (Movie movie in All)
                {
                    PAK2.File own = EntryOf(pak, movie.Entry);
                    byte[] modded = IsOn(movie) && EntryOf(pak, movie.OriginalEntry) != null ? Embedded(movie, true) : null;
                    if (modded != null && own == null)
                        pak.Entries.Add(new PAK2.File() { Filename = movie.Entry, Content = modded });
                    else if (modded != null && !Same(own.Content, modded))
                        own.Content = modded;
                    else if (modded == null && own != null)
                        pak.Entries.Remove(own);
                    else
                        continue;
                    changed = true;
                }
                return changed ? BytesOf(pak) : null;
            }
            catch
            {
                return null;
            }
        }

        private static byte[] BytesOf(PAK2 pak)
        {
            string temp = Path.GetTempFileName();
            try
            {
                return pak.Save(temp) ? File.ReadAllBytes(temp) : null;
            }
            finally
            {
                File.Delete(temp);
            }
        }
        #endregion
    }
}

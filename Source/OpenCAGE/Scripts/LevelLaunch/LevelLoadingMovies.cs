using CATHODE;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace OpenCAGE
{
    /// <summary>
    /// A level's own loading movies: .usm files kept in DATA/ENV/&lt;level&gt;/MOVIES (a mod carries them as files of the level). A launch
    /// into the level copies them into DATA/UI/MOVIES under OpenCAGE's own names and adds a playlist of them to DATA\GBL_ITEM.BML, which
    /// the launch's loading playlist then names; any other launch, the game closing or OpenCAGE starting takes both out again. The
    /// playlist and the copies are OpenCAGE's, not the user's work: the Mod Manager looks past them.
    /// </summary>
    public static class LevelLoadingMovies
    {
        /// <summary>The folder in a level's folder that holds its own loading movies.</summary>
        public const string FolderName = "MOVIES";
        /// <summary>The playlist a launch adds to GBL_ITEM.BML (the game plays a playlist by name).</summary>
        public const string PlaylistName = "OC_LEVEL_LOADING";
        /// <summary>The copies a launch puts in DATA/UI/MOVIES start with this.</summary>
        public const string CopyPrefix = "OCLVL_";
        /// <summary>The most movies a level can give.</summary>
        public const int MaxMovies = 8;

        /// <summary>A level's movies folder: DATA/ENV/&lt;level&gt;/MOVIES.</summary>
        public static string Folder(string pathToAI, string level) =>
            Path.Combine(pathToAI ?? "", "DATA", "ENV", (level ?? "").Replace('/', Path.DirectorySeparatorChar).Trim(Path.DirectorySeparatorChar), FolderName);

        /// <summary>The .usm files in a level's movies folder, by file name.</summary>
        public static List<string> Files(string pathToAI, string level)
        {
            string folder = Folder(pathToAI, level);
            if (string.IsNullOrWhiteSpace(level) || !Directory.Exists(folder))
                return new List<string>();
            return Directory.GetFiles(folder, "*.usm").Select(Path.GetFileName).OrderBy(o => o, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Whether bytes look like a movie the game plays: a CRI USM container.</summary>
        public static bool LooksLikeUsm(string file)
        {
            try
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    byte[] magic = new byte[4];
                    return stream.Read(magic, 0, 4) == 4 && Encoding.ASCII.GetString(magic) == "CRID";
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Copy a movie into a level's movies folder (its file name kept, upper case). Returns the name it has there, or throws with why it
        /// can't be used.
        /// </summary>
        public static string Add(string pathToAI, string level, string sourceFile)
        {
            if (!File.Exists(sourceFile))
                throw new FileNotFoundException("there is no file " + sourceFile);
            if (!LooksLikeUsm(sourceFile))
                throw new InvalidDataException(Path.GetFileName(sourceFile) + " is not a .usm movie (the CRI video the game plays)");
            if (!Directory.Exists(Path.GetDirectoryName(Folder(pathToAI, level))))
                throw new DirectoryNotFoundException("there is no level " + level);
            string name = Path.GetFileName(sourceFile).ToUpperInvariant();
            if (!name.EndsWith(".USM")) name = Path.GetFileNameWithoutExtension(name) + ".USM";
            Directory.CreateDirectory(Folder(pathToAI, level));
            string target = Path.Combine(Folder(pathToAI, level), name);
            if (!string.Equals(Path.GetFullPath(sourceFile), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
                File.Copy(sourceFile, target, true);
            return name;
        }

        /// <summary>Take a movie out of a level's movies folder (the folder goes when it is empty).</summary>
        public static void Remove(string pathToAI, string level, string name)
        {
            string folder = Folder(pathToAI, level);
            string file = Path.Combine(folder, Path.GetFileName(name ?? ""));
            if (File.Exists(file)) File.Delete(file);
            if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
        }

        #region IN THE GAME'S FILES
        private static readonly object _lock = new object();

        private static string GblItem(string pathToAI) => Path.Combine(pathToAI ?? "", "DATA", "GBL_ITEM.BML");
        private static string MoviesFolder(string pathToAI) => Path.Combine(pathToAI ?? "", "DATA", "UI", "MOVIES");
        /* What GBL_ITEM.BML was before the playlist went in, and the hash of the file as written with it: the file is put back to exactly
           those bytes (the BML writer doesn't reproduce a file byte for byte). Kept where the Mod Manager never looks. */
        private static string KeptFolder(string pathToAI) => Path.Combine(pathToAI ?? "", "DATA", "MODTOOLS", "LEVEL_MOVIES");
        private static string KeptOriginal(string pathToAI) => Path.Combine(KeptFolder(pathToAI), "GBL_ITEM.BML");
        private static string KeptHash(string pathToAI) => Path.Combine(KeptFolder(pathToAI), "GBL_ITEM.SHA256");

        private static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
        }

        private static bool HasPlaylistText(byte[] bytes) => bytes != null && IndexOf(bytes, Encoding.ASCII.GetBytes(PlaylistName)) >= 0;

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            for (int i = 0; i + needle.Length <= haystack.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && haystack[i + j] == needle[j]) j++;
                if (j == needle.Length) return i;
            }
            return -1;
        }

        /* GBL_ITEM.BML's bytes with the playlist set as given (null: none) */
        private static byte[] WithPlaylist(byte[] gblItem, List<string> clips, bool toEnd)
        {
            BML bml = new BML(gblItem);
            XmlDocument document = bml.Content;
            XmlElement playlists = document?["item_database"]?["movie_playlists"] ?? throw new InvalidDataException("GBL_ITEM.BML has no movie playlists");
            foreach (XmlElement old in playlists.ChildNodes.OfType<XmlElement>().Where(o => o.GetAttribute("playlist_name") == PlaylistName).ToList())
                playlists.RemoveChild(old);
            if (clips != null)
            {
                XmlElement playlist = document.CreateElement("movie_playlist", playlists.NamespaceURI);
                playlist.SetAttribute("playlist_name", PlaylistName);
                //Played to the end: the level waits behind it and the player can skip, as the game's own cutscene playlists are
                playlist.SetAttribute("terminate_on_load_completed", toEnd ? "false" : "true");
                playlist.SetAttribute("allow_player_to_skip", toEnd ? "true" : "false");
                playlist.SetAttribute("shuffle_playlist", "false");
                playlist.SetAttribute("loop_playlist", toEnd ? "false" : "true");
                for (int i = 0; i < clips.Count; i++)
                {
                    XmlElement clip = document.CreateElement("clip", playlists.NamespaceURI);
                    clip.SetAttribute("clip_name", clips[i]);
                    clip.SetAttribute("clip_index", i.ToString());
                    playlist.AppendChild(clip);
                }
                playlists.AppendChild(playlist);
            }
            bml.Content = document;
            return bml.ToBytes();
        }

        /// <summary>
        /// A launch into a level: its movies copied in and the playlist added, or - when it has none - an earlier launch's taken out.
        /// Returns the playlist to play (null: none of the level's), and what went wrong in problem.
        /// </summary>
        public static string Apply(string pathToAI, string level, LevelLaunchSettings settings, out string problem)
        {
            problem = null;
            List<string> wanted = (settings?.LoadingMovies ?? new List<string>()).Where(o => !string.IsNullOrWhiteSpace(o)).Take(MaxMovies).ToList();
            if (wanted.Count == 0 || string.IsNullOrWhiteSpace(level))
            {
                problem = PutBack(pathToAI);
                return null;
            }
            lock (_lock)
            {
                try
                {
                    //The copies, then the playlist naming them
                    List<string> clips = new List<string>();
                    foreach (string name in wanted)
                    {
                        string source = Path.Combine(Folder(pathToAI, level), Path.GetFileName(name));
                        if (!File.Exists(source) || !LooksLikeUsm(source))
                        {
                            problem = (problem != null ? problem + "; " : "") + level + " has no movie " + name + " in its " + FolderName + " folder";
                            continue;
                        }
                        string copy = CopyPrefix + clips.Count;
                        File.Copy(source, Path.Combine(MoviesFolder(pathToAI), copy + ".USM"), true);
                        clips.Add("Movies/" + copy + ".usm");
                    }
                    RemoveCopies(pathToAI, clips.Count);
                    if (clips.Count == 0)
                    {
                        foreach (string back in new[] { PutBackScreen(pathToAI), PutBackPlaylist(pathToAI) })
                            if (back != null) problem = (problem != null ? problem + "; " : "") + back;
                        return null;
                    }
                    bool toEnd = settings.LoadingMoviesToEnd == true;
                    WriteGblItem(pathToAI, original => WithPlaylist(original, clips, toEnd));
                    //Played to the end only with the loading screen changed to wait for them
                    string screen = toEnd ? PutScreenIn(pathToAI) : PutBackScreen(pathToAI);
                    if (screen != null) problem = (problem != null ? problem + "; " : "") + screen;
                    return PlaylistName;
                }
                catch (Exception e)
                {
                    problem = (problem != null ? problem + "; " : "") + "the level's loading movies could not be put in: " + e.Message;
                    return null;
                }
            }
        }

        /* GBL_ITEM.BML as the game's own version plus what make() adds: the original kept, the file written, its hash kept */
        private static void WriteGblItem(string pathToAI, Func<byte[], byte[]> make)
        {
            string path = GblItem(pathToAI);
            byte[] current = File.ReadAllBytes(path);
            byte[] original = OriginalOf(pathToAI, current);
            byte[] written = make(original);
            Directory.CreateDirectory(KeptFolder(pathToAI));
            File.WriteAllBytes(KeptOriginal(pathToAI), original);
            File.WriteAllText(KeptHash(pathToAI), Hash(written));
            //Kept first if it's still as shipped - but this isn't the user's edit (the Mod Manager looks past the playlist)
            Modding.ModServices.CaptureBeforeWrite(path, usersEdit: false);
            File.WriteAllBytes(path, written);
        }

        /* The file without the playlist: the kept original when the file is still as written, else the playlist taken out */
        private static byte[] OriginalOf(string pathToAI, byte[] current)
        {
            if (File.Exists(KeptHash(pathToAI)) && File.Exists(KeptOriginal(pathToAI)) && File.ReadAllText(KeptHash(pathToAI)).Trim() == Hash(current))
                return File.ReadAllBytes(KeptOriginal(pathToAI));
            return HasPlaylistText(current) ? WithPlaylist(current, null, false) : current;
        }

        private static void RemoveCopies(string pathToAI, int keep)
        {
            string folder = MoviesFolder(pathToAI);
            if (!Directory.Exists(folder)) return;
            foreach (string file in Directory.GetFiles(folder, CopyPrefix + "*.USM"))
            {
                string number = Path.GetFileNameWithoutExtension(file).Substring(CopyPrefix.Length);
                if (!int.TryParse(number, out int index) || index >= keep)
                    File.Delete(file);
            }
        }

        private static string PutBackPlaylist(string pathToAI)
        {
            try
            {
                string path = GblItem(pathToAI);
                if (File.Exists(path))
                {
                    byte[] current = File.ReadAllBytes(path);
                    byte[] original = OriginalOf(pathToAI, current);
                    if (original != current)
                    {
                        Modding.ModServices.CaptureBeforeWrite(path, usersEdit: false);
                        File.WriteAllBytes(path, original);
                    }
                }
                if (File.Exists(KeptOriginal(pathToAI))) File.Delete(KeptOriginal(pathToAI));
                if (File.Exists(KeptHash(pathToAI))) File.Delete(KeptHash(pathToAI));
                RemoveKeptFolderIfEmpty(pathToAI);
                return null;
            }
            catch (Exception e)
            {
                return "GBL_ITEM.BML could not be put back (is the game open?): " + e.Message;
            }
        }

        /// <summary>The copies and the playlist taken out again. Returns what went wrong, or null.</summary>
        public static string PutBack(string pathToAI)
        {
            lock (_lock)
            {
                try
                {
                    RemoveCopies(pathToAI, 0);
                }
                catch (Exception e)
                {
                    return "the level's loading movies could not be taken out of DATA/UI/MOVIES (is the game open?): " + e.Message;
                }
                string screen = PutBackScreen(pathToAI);
                string playlist = PutBackPlaylist(pathToAI);
                return screen != null && playlist != null ? screen + "; " + playlist : screen ?? playlist;
            }
        }

        /// <summary>Whether a launch's movies are in the game's files now.</summary>
        public static bool InUse(string pathToAI)
        {
            try
            {
                return File.Exists(KeptHash(pathToAI)) || File.Exists(ScreenRecord(pathToAI)) ||
                       (Directory.Exists(MoviesFolder(pathToAI)) && Directory.GetFiles(MoviesFolder(pathToAI), CopyPrefix + "*.USM").Length != 0);
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region THE LOADING SCREEN
        /* The game's loading screen movie (UI.PAK) is done 5 s into any movie playlist, by a timer it starts with the first clip, and
           the game ends the screen once it is done and the level has loaded: its playlist flags never hold the screen. For movies
           played to the end, a launch changes two instructions of the loading screen the game is pointed at, in place: the playlist
           starts without that timer (and without the tip box over the movies), and a playlist that doesn't loop is done once its
           last clip has played, as the boot sequence is. Each: the bytes around it, where in them, the game's byte, the changed one. */
        private static readonly byte[][] _screenAround =
        {
            new byte[] { 0xD0, 0x4F, 0xAE, 0x01, 0x00, 0xD0, 0x4F, 0xC6, 0x01, 0x00, 0x47 },
            new byte[] { 0xD0, 0x66, 0xC5, 0x01, 0x26, 0x14, 0x0E, 0x00, 0x00, 0xD0, 0x26, 0x68, 0xA4, 0x01, 0xD0, 0x27, 0x68, 0xC5, 0x01, 0x10, 0x05, 0x00, 0x00 },
        };
        private static readonly int[] _screenAt = { 7, 4 };
        private static readonly byte[] _screenGame = { 0xC6, 0x26 };
        private static readonly byte[] _screenChanged = { 0xEB, 0x2A };

        /* The UI.PAK entry a launch changed (kept apart from the playlist's files, which go first) */
        private static string ScreenRecord(string pathToAI) => Path.Combine(pathToAI ?? "", "DATA", "MODTOOLS", "LEVEL_MOVIES_SCREEN.TXT");
        private static string UiPak(string pathToAI) => Path.Combine(pathToAI ?? "", "DATA", "UI.PAK");

        private static void RemoveKeptFolderIfEmpty(string pathToAI)
        {
            if (Directory.Exists(KeptFolder(pathToAI)) && !Directory.EnumerateFileSystemEntries(KeptFolder(pathToAI)).Any())
                Directory.Delete(KeptFolder(pathToAI));
        }

        /* Where each entry of a PAK2's bytes is: [start, end) by name, or null when they aren't a PAK2 */
        private static Dictionary<string, KeyValuePair<int, int>> PakEntries(byte[] pak)
        {
            if (pak == null || pak.Length < 16 || Encoding.ASCII.GetString(pak, 0, 4) != "PAK2")
                return null;
            int offsets = BitConverter.ToInt32(pak, 4) + 16, count = BitConverter.ToInt32(pak, 8), align = Math.Max(1, BitConverter.ToInt32(pak, 12));
            if (count < 0 || offsets + 4L * count > pak.Length)
                return null;
            Dictionary<string, KeyValuePair<int, int>> entries = new Dictionary<string, KeyValuePair<int, int>>(StringComparer.OrdinalIgnoreCase);
            int name = 16, previous = offsets + 4 * count;
            for (int i = 0; i < count; i++)
            {
                int zero = Array.IndexOf(pak, (byte)0, name);
                if (zero < 0 || zero >= offsets) return null;
                string file = Encoding.ASCII.GetString(pak, name, zero - name);
                name = zero + 1;
                int end = BitConverter.ToInt32(pak, offsets + 4 * i);
                int start = (previous + align - 1) / align * align;
                if (end < start || end > pak.Length) return null;
                entries[file] = new KeyValuePair<int, int>(start, end);
                previous = end;
            }
            return entries;
        }

        /* Where the two instructions are in an entry, and whether they are changed: 1 changed, 0 as the game's, -1 not this movie */
        private static int ScreenState(byte[] pak, KeyValuePair<int, int> entry, out int[] at)
        {
            at = new int[_screenAround.Length];
            int state = -2;
            for (int i = 0; i < _screenAround.Length; i++)
            {
                int found = -1, foundState = -1, hits = 0;
                for (int changed = 0; changed < 2; changed++)
                {
                    byte[] look = (byte[])_screenAround[i].Clone();
                    look[_screenAt[i]] = changed == 1 ? _screenChanged[i] : _screenGame[i];
                    for (int p = IndexOf(pak, look, entry.Key, entry.Value); p >= 0; p = IndexOf(pak, look, p + 1, entry.Value))
                    {
                        found = p + _screenAt[i];
                        foundState = changed;
                        hits++;
                    }
                }
                if (hits != 1 || (state != -2 && state != foundState))
                    return -1;
                at[i] = found;
                state = foundState;
            }
            return state;
        }

        private static int IndexOf(byte[] haystack, byte[] needle, int from, int to)
        {
            for (int i = Math.Max(0, from); i + needle.Length <= to; i++)
            {
                int j = 0;
                while (j < needle.Length && haystack[i + j] == needle[j]) j++;
                if (j == needle.Length) return i;
            }
            return -1;
        }

        /* UI.PAK's entries the game's loading screen can come from: the game's own, and a level's own (LevelUiOverrides) */
        private static List<string> ScreenEntries()
        {
            LevelUiOverrides.Movie movie = LevelUiOverrides.Find("LOADINGSCREEN");
            return movie == null ? new List<string>() : new List<string>() { movie.OriginalEntry, movie.Entry };
        }

        /* UI.PAK changed in place while held: change(bytes) returns the offsets and the bytes to write there, or null for none */
        private static void ChangeUiPak(string pathToAI, Func<byte[], List<KeyValuePair<int, byte>>> change)
        {
            using (FileStream stream = new FileStream(UiPak(pathToAI), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                byte[] bytes = new byte[stream.Length];
                int read = 0;
                while (read < bytes.Length)
                {
                    int got = stream.Read(bytes, read, bytes.Length - read);
                    if (got <= 0) throw new EndOfStreamException();
                    read += got;
                }
                foreach (KeyValuePair<int, byte> write in change(bytes) ?? new List<KeyValuePair<int, byte>>())
                {
                    stream.Seek(write.Key, SeekOrigin.Begin);
                    stream.WriteByte(write.Value);
                }
            }
        }

        /* The loading screen the game is pointed at changed to wait for the playlist; what went wrong, or null */
        private static string PutScreenIn(string pathToAI)
        {
            try
            {
                LevelUiOverrides.Movie movie = LevelUiOverrides.Find("LOADINGSCREEN");
                string entry = movie == null ? null : PatchManager.HasFilePath(pathToAI, movie.GamePath) ? movie.OriginalEntry :
                               PatchManager.HasFilePath(pathToAI, movie.LevelPath) ? movie.Entry : null;
                if (entry == null)
                    return "the game is pointed at another loading screen (Launch Game's Quicker Loadscreen?), which plays no loading movies";
                if (!File.Exists(UiPak(pathToAI)))
                    return "the game has no DATA/UI.PAK";
                //Kept first if it's still as shipped - but this isn't the user's edit (the Mod Manager looks past the change)
                if (entry == movie.OriginalEntry)
                    Modding.ModServices.CaptureBeforeWrite(UiPak(pathToAI), usersEdit: false);
                string problem = null;
                Directory.CreateDirectory(Path.GetDirectoryName(ScreenRecord(pathToAI)));
                File.WriteAllText(ScreenRecord(pathToAI), entry);
                ChangeUiPak(pathToAI, bytes =>
                {
                    Dictionary<string, KeyValuePair<int, int>> entries = PakEntries(bytes);
                    if (entries == null || !entries.TryGetValue(entry, out KeyValuePair<int, int> range))
                    {
                        problem = "UI.PAK has no " + entry;
                        return null;
                    }
                    int state = ScreenState(bytes, range, out int[] at);
                    if (state < 0)
                    {
                        problem = (entry == movie.OriginalEntry ? "the game's loading screen" : "the level's own loading screen") + " isn't the one OpenCAGE knows";
                        return null;
                    }
                    return state == 1 ? null : at.Select((o, i) => new KeyValuePair<int, byte>(o, _screenChanged[i])).ToList();
                });
                if (problem != null)
                    PutBackScreen(pathToAI);
                return problem == null ? null : problem + ", so the movies stop once the level has loaded";
            }
            catch (Exception e)
            {
                return "the loading screen could not be changed to play the movies to the end (is the game open?): " + e.Message;
            }
        }

        /* Every loading screen a launch changed back as the game's; what went wrong, or null */
        private static string PutBackScreen(string pathToAI)
        {
            try
            {
                if (File.Exists(UiPak(pathToAI)))
                    ChangeUiPak(pathToAI, bytes =>
                    {
                        Dictionary<string, KeyValuePair<int, int>> entries = PakEntries(bytes);
                        List<KeyValuePair<int, byte>> writes = new List<KeyValuePair<int, byte>>();
                        foreach (string entry in ScreenEntries())
                            if (entries != null && entries.TryGetValue(entry, out KeyValuePair<int, int> range) && ScreenState(bytes, range, out int[] at) == 1)
                                writes.AddRange(at.Select((o, i) => new KeyValuePair<int, byte>(o, _screenGame[i])));
                        return writes;
                    });
                if (File.Exists(ScreenRecord(pathToAI)))
                    File.Delete(ScreenRecord(pathToAI));
                return null;
            }
            catch (Exception e)
            {
                return "the game's loading screen could not be put back (is the game open?): " + e.Message;
            }
        }

        /// <summary>Whether a launch has a loading screen changed now (the level's own movies played to the end).</summary>
        public static bool ScreenChanged(string pathToAI) => File.Exists(ScreenRecord(pathToAI));

        /// <summary>For the Mod Manager: UI.PAK's bytes with the game's loading screen as the game's, or null when it isn't changed.</summary>
        public static byte[] WithoutScreenChange(byte[] uiPak)
        {
            try
            {
                Dictionary<string, KeyValuePair<int, int>> entries = PakEntries(uiPak);
                string entry = ScreenEntries().FirstOrDefault();
                if (entries == null || entry == null || !entries.TryGetValue(entry, out KeyValuePair<int, int> range) || ScreenState(uiPak, range, out int[] at) != 1)
                    return null;
                byte[] copy = (byte[])uiPak.Clone();
                for (int i = 0; i < at.Length; i++) copy[at[i]] = _screenGame[i];
                return copy;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>For the Mod Manager writing UI.PAK while a launch has the loading screen changed: its content changed again, or null.</summary>
        public static byte[] WithScreenChangeInUse(byte[] uiPak)
        {
            try
            {
                string pathToAI = Singleton.PathToAI;
                if (!File.Exists(ScreenRecord(pathToAI)))
                    return null;
                string entry = File.ReadAllText(ScreenRecord(pathToAI)).Trim();
                Dictionary<string, KeyValuePair<int, int>> entries = PakEntries(uiPak);
                if (entries == null || !entries.TryGetValue(entry, out KeyValuePair<int, int> range) || ScreenState(uiPak, range, out int[] at) != 0)
                    return null;
                byte[] copy = (byte[])uiPak.Clone();
                for (int i = 0; i < at.Length; i++) copy[at[i]] = _screenChanged[i];
                return copy;
            }
            catch
            {
                return null;
            }
        }
        #endregion

        #region THE MOD MANAGER
        /// <summary>Whether a game file can hold the playlist (the Mod Manager looks past it there).</summary>
        public static bool MayHold(string normalisedPath) => string.Equals(normalisedPath, "DATA/GBL_ITEM.BML", StringComparison.OrdinalIgnoreCase);

        /// <summary>GBL_ITEM.BML's bytes without the playlist (exactly as they were before it went in), or null when it holds none.</summary>
        public static byte[] WithoutPlaylist(byte[] gblItem)
        {
            try
            {
                if (!HasPlaylistText(gblItem)) return null;
                lock (_lock)
                {
                    byte[] original = OriginalOf(Singleton.PathToAI, gblItem);
                    return original == gblItem ? null : original;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// For the Mod Manager writing GBL_ITEM.BML while a launch's playlist is in: its content with the playlist put back (the content
        /// kept as the file to return to), or null when none is in.
        /// </summary>
        public static byte[] WithPlaylistInUse(byte[] content)
        {
            try
            {
                string pathToAI = Singleton.PathToAI;
                lock (_lock)
                {
                    if (!File.Exists(KeptHash(pathToAI)) || !File.Exists(KeptOriginal(pathToAI)))
                        return null;
                    XmlElement playlist = new BML(File.ReadAllBytes(GblItem(pathToAI))).Content?["item_database"]?["movie_playlists"]?.ChildNodes.OfType<XmlElement>().FirstOrDefault(o => o.GetAttribute("playlist_name") == PlaylistName);
                    if (playlist == null)
                        return null;
                    List<string> clips = playlist.ChildNodes.OfType<XmlElement>().Select(o => o.GetAttribute("clip_name")).ToList();
                    bool toEnd = playlist.GetAttribute("terminate_on_load_completed") == "false";
                    byte[] written = WithPlaylist(content, clips, toEnd);
                    File.WriteAllBytes(KeptOriginal(pathToAI), content);
                    File.WriteAllText(KeptHash(pathToAI), Hash(written));
                    return written;
                }
            }
            catch
            {
                return null;
            }
        }
        #endregion
    }
}

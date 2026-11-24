using CathodeLib;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OpenCAGE
{
    /// <summary>
    /// A custom level's own saves, kept apart from the campaign's. The game keeps every save in a few files: two checkpoint files used
    /// in turn (the load screen's current and previous save), one mission save per mission number, a progression file (which checkpoint
    /// is current, the last level saved in, the player's game options) and two working files. A save made in any level overwrites the
    /// campaign's older checkpoint and rewrites its progression, so a custom level played in the same game eats a campaign's saves. A
    /// launch into a level that keeps its saves apart gives those files names of the level's own for that launch (LevelLaunchPatches),
    /// so the campaign's files are never written and the level's own are what Continue and Load Game show while it is played.
    /// </summary>
    public static class LevelSaveSlots
    {
        /// <summary>The game's names, in the order LevelLaunchPatches keeps them: checkpoints, mission saves, progression, two working files.</summary>
        public static readonly string[] GameNames = { "CHSV", "MSSV", "PROGRESSION", "AI_MLS.TMP", "AI_TMP.TMP" };

        //Tags that would give a name the game already uses (CHSV, MSSV, AI_MLS.TMP, AI_TMP.TMP)
        private static readonly HashSet<string> ReservedTags = new HashSet<string>() { "HSV", "SSV", "MLS", "TMP" };
        private const string TagChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        /// <summary>One of the game's own levels (everything it ships is under PRODUCTION).</summary>
        public static bool IsGameLevel(string level) => Normalise(level).StartsWith("PRODUCTION/", StringComparison.OrdinalIgnoreCase);

        /// <summary>Whether a level keeps its saves apart when its settings don't say: yes for every level the game doesn't ship.</summary>
        public static bool DefaultFor(string level) => !string.IsNullOrWhiteSpace(level) && !EditorUtils.IsFrontend(level) && !IsGameLevel(level);

        /// <summary>Whether a launch into a level keeps its saves apart.</summary>
        public static bool Own(string level, LevelLaunchSettings settings)
        {
            if (string.IsNullOrWhiteSpace(level) || EditorUtils.IsFrontend(level))
                return false;
            return settings?.OwnSaves ?? DefaultFor(level);
        }

        private static string Normalise(string level) => (level ?? "").Replace('\\', '/').Trim('/').ToUpperInvariant();

        /// <summary>The level's three character tag, from its path (the same level always gets the same one).</summary>
        public static string TagFor(string level)
        {
            string key = Normalise(level);
            using (SHA1 sha = SHA1.Create())
            {
                for (int salt = 0; ; salt++)
                {
                    byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(salt == 0 ? key : key + "#" + salt));
                    uint n = BitConverter.ToUInt32(hash, 0) % (36 * 36 * 36);
                    string tag = new string(new[] { TagChars[(int)(n / 1296)], TagChars[(int)(n / 36 % 36)], TagChars[(int)(n % 36)] });
                    if (!ReservedTags.Contains(tag))
                        return tag;
                }
            }
        }

        /// <summary>The names a tag gives, each as long as the game's own: C&lt;tag&gt;, M&lt;tag&gt;, PROG_&lt;tag&gt;_OC, AI_&lt;tag&gt;.TMP, AT_&lt;tag&gt;.TMP.</summary>
        public static string[] NamesFor(string tag) => new[] { "C" + tag, "M" + tag, "PROG_" + tag + "_OC", "AI_" + tag + ".TMP", "AT_" + tag + ".TMP" };

        /// <summary>The tag a set of names was made from, or null when they aren't one.</summary>
        public static string TagOf(string[] names)
        {
            if (names == null || names.Length != GameNames.Length || names[0] == null || names[0].Length != 4 || names[0][0] != 'C')
                return null;
            string tag = names[0].Substring(1);
            return tag.All(o => TagChars.IndexOf(o) >= 0) && NamesFor(tag).SequenceEqual(names) ? tag : null;
        }

        /// <summary>The level's save files as the game names them (".ais" files in the save folder).</summary>
        public static List<string> FileNames(string level)
        {
            string[] names = NamesFor(TagFor(level));
            return new List<string>() { names[0] + "00.ais", names[0] + "01.ais", names[1] + "<mission>.ais", names[2] + ".ais" };
        }

        /// <summary>Another level of this install that keeps its saves apart under the same tag (so the two would share save files), or null.</summary>
        public static string SharesWith(string pathToAI, string level)
        {
            try
            {
                string tag = TagFor(level), env = Path.Combine(pathToAI ?? "", "DATA", "ENV");
                if (!Directory.Exists(env)) return null;
                foreach (string world in Directory.EnumerateDirectories(env, "WORLD", SearchOption.AllDirectories))
                {
                    if (!File.Exists(Path.Combine(world, "COMMANDS.PAK"))) continue;
                    string other = Path.GetDirectoryName(world).Substring(env.Length).Replace('\\', '/').Trim('/');
                    if (string.Equals(Normalise(other), Normalise(level), StringComparison.Ordinal) || TagFor(other) != tag) continue;
                    if (Own(other, LevelLaunchSettings.Of(pathToAI, other)))
                        return other;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("LevelSaveSlots::SharesWith - " + e.Message);
            }
            return null;
        }

        #region THE SAVE FOLDERS
        /// <summary>Where this build of the game keeps its saves (Steam: each Steam user's cloud folder for the game).</summary>
        public static List<string> SaveFolders(PatchManager.Platform platform)
        {
            List<string> folders = new List<string>();
            try
            {
                switch (platform)
                {
                    case PatchManager.Platform.STEAM:
                        string steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
                        string users = string.IsNullOrEmpty(steam) ? null : Path.Combine(steam.Replace('/', '\\'), "userdata");
                        if (users != null && Directory.Exists(users))
                            foreach (string user in Directory.GetDirectories(users))
                                if (Directory.Exists(Path.Combine(user, "214490", "remote")))
                                    folders.Add(Path.Combine(user, "214490", "remote"));
                        break;
                    case PatchManager.Platform.GOG:
                        folders.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Alien Isolation"));
                        break;
                    case PatchManager.Platform.EPIC_GAMES_STORE:
                        folders.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "The Creative Assembly", "Alien Isolation"));
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("LevelSaveSlots::SaveFolders - " + e.Message);
            }
            return folders;
        }

        /// <summary>One save file found: where, when, and the name the load screen shows for it.</summary>
        public sealed class SaveFile
        {
            public string Path;
            public DateTime Written;
            public string Level;
            public string Shown;
        }

        /// <summary>The level's own save files in the save folders (read only).</summary>
        public static List<SaveFile> Find(PatchManager.Platform platform, string level)
        {
            List<SaveFile> found = new List<SaveFile>();
            string[] names = NamesFor(TagFor(level));
            foreach (string folder in SaveFolders(platform).Where(Directory.Exists))
            {
                foreach (string file in Directory.GetFiles(folder, "*.ais"))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    bool numbered = name.Length == 6 && (name.StartsWith(names[0], StringComparison.OrdinalIgnoreCase) || name.StartsWith(names[1], StringComparison.OrdinalIgnoreCase)) && char.IsDigit(name[4]) && char.IsDigit(name[5]);
                    if (!numbered && !string.Equals(name, names[2], StringComparison.OrdinalIgnoreCase))
                        continue;
                    SaveFile save = new SaveFile() { Path = file, Written = File.GetLastWriteTime(file) };
                    if (numbered)
                        ReadHeader(file, save);
                    found.Add(save);
                }
            }
            return found.OrderBy(o => o.Path, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /* A save's header: the level at 0x20, the name shown at 0xA0 (UTF-16) */
        private static void ReadHeader(string file, SaveFile save)
        {
            try
            {
                byte[] head = new byte[0x1A0];
                using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    if (stream.Read(head, 0, head.Length) != head.Length) return;
                save.Level = Encoding.ASCII.GetString(head, 0x20, 0x80).Split('\0')[0];
                save.Shown = Encoding.Unicode.GetString(head, 0xA0, 0x100).Split('\0')[0];
            }
            catch { }
        }
        #endregion
    }
}

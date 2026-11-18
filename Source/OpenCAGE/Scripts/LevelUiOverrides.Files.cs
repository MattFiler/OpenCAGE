using CATHODE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenCAGE
{
    /// <summary>
    /// The files a level's own UI movies load themselves (a map page, its floors), kept in the level's UI\FILES folder: a launch into
    /// the level puts each into UI.PAK as DATA/UI/LV/&lt;its path in the folder&gt; along with the level's movies, and takes them out
    /// with them. The entries are OpenCAGE's (<see cref="IsOwnedFileEntry"/>), so the Mod Manager looks past them.
    /// </summary>
    public static partial class LevelUiOverrides
    {
        /// <summary>The folder in a level's UI folder holding the files its movies load.</summary>
        public const string FilesFolderName = "FILES";
        /// <summary>Where they go in UI.PAK; a movie loads them as "LV/&lt;path&gt;" (from DATA/UI).</summary>
        public const string FilesEntryPrefix = "DATA/UI/LV/";

        /// <summary>A level's files for its movies: UI.PAK entry name -> the file.</summary>
        public static SortedDictionary<string, string> FilesOf(string pathToAI, string level)
        {
            SortedDictionary<string, string> found = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(level))
                return found;
            string folder = Path.Combine(Folder(pathToAI, level), FilesFolderName);
            if (!Directory.Exists(folder))
                return found;
            foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(folder.Length).Replace('\\', '/').Trim('/');
                found[(FilesEntryPrefix + relative).ToUpperInvariant()] = file;
            }
            return found;
        }

        /// <summary>An entry of UI.PAK that is one of a level's files put in by a launch.</summary>
        public static bool IsOwnedFileEntry(string entryName)
        {
            string name = (entryName ?? "").Replace('\\', '/').TrimStart('/');
            return name.StartsWith(FilesEntryPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string FilesCacheFolder(string pathToAI) => Path.Combine(CacheFolder(pathToAI), FilesFolderName);

        /* The level's files in (level null: every one out), the copies the Mod Manager puts back from kept beside the movies' own.
           Returns whether UI.PAK changed. */
        private static bool WriteFiles(PAK2 pak, string pathToAI, string level, List<string> problems)
        {
            bool changed = false;
            SortedDictionary<string, string> wanted = level == null ? new SortedDictionary<string, string>() : FilesOf(pathToAI, level);
            changed |= pak.Entries.RemoveAll(o => IsOwnedFileEntry(o.Filename) && !wanted.ContainsKey(o.Filename.Replace('\\', '/').ToUpperInvariant())) != 0;
            Dictionary<string, byte[]> written = new Dictionary<string, byte[]>();
            foreach (KeyValuePair<string, string> file in wanted)
            {
                byte[] content;
                try { content = File.ReadAllBytes(file.Value); }
                catch (Exception e) { problems.Add(Path.GetFileName(file.Value) + " in " + level + "'s UI files can't be read: " + e.Message); continue; }
                written[file.Key] = content;
                PAK2.File entry = pak.Entries.FirstOrDefault(o => string.Equals(o.Filename, file.Key, StringComparison.OrdinalIgnoreCase));
                if (entry == null)
                    pak.Entries.Add(new PAK2.File() { Filename = file.Key, Content = content });
                else if (entry.Content == null || !entry.Content.AsSpan().SequenceEqual(content))
                    entry.Content = content;
                else
                    continue;
                changed = true;
            }
            try
            {
                string cache = FilesCacheFolder(pathToAI);
                if (Directory.Exists(cache))
                    Directory.Delete(cache, true);
                if (written.Count != 0)
                {
                    Directory.CreateDirectory(cache);
                    foreach (KeyValuePair<string, byte[]> file in written)
                    {
                        string copy = Path.Combine(cache, file.Key.Substring(FilesEntryPrefix.Length).Replace('/', '\\'));
                        Directory.CreateDirectory(Path.GetDirectoryName(copy));
                        File.WriteAllBytes(copy, file.Value);
                    }
                }
            }
            catch { } //only the Mod Manager's putting them back needs these
            return changed;
        }

        /* For the Mod Manager: the files of the level whose movies are in, put back in an open UI.PAK from the kept copies */
        private static bool AddFilesInUse(PAK2 pak, string pathToAI)
        {
            string cache = FilesCacheFolder(pathToAI);
            if (InUse(pathToAI).Count == 0 || !Directory.Exists(cache))
                return false;
            bool changed = false;
            foreach (string file in Directory.GetFiles(cache, "*", SearchOption.AllDirectories))
            {
                string name = (FilesEntryPrefix + file.Substring(cache.Length).Replace('\\', '/').Trim('/')).ToUpperInvariant();
                byte[] content = File.ReadAllBytes(file);
                PAK2.File entry = pak.Entries.FirstOrDefault(o => string.Equals(o.Filename, name, StringComparison.OrdinalIgnoreCase));
                if (entry == null)
                    pak.Entries.Add(new PAK2.File() { Filename = name, Content = content });
                else if (entry.Content == null || !entry.Content.AsSpan().SequenceEqual(content))
                    entry.Content = content;
                else
                    continue;
                changed = true;
            }
            return changed;
        }
    }
}

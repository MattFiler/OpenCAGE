#if ENABLE_MOD_PACKAGES
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenCAGE.Modding
{
    public enum FileStatus
    {
        Vanilla,   //Matches the shipped bytes
        Managed,   //Matches what an enabled mod last wrote there
        Modified,  //A shipped file with unrecognised bytes (unexported OpenCAGE work, or a hand-installed mod)
        Foreign,   //A file the shipped game doesn't have, and no mod claims
        Missing,   //A shipped file that isn't on disk
        Copy,      //A file the shipped game doesn't have there, byte for byte one it ships elsewhere under the same name (a game folder renamed and put back, a backup): not a change
    }

    public class ScanResult
    {
        public Dictionary<string, FileStatus> Files = new Dictionary<string, FileStatus>();
        /* Each Copy, and the shipped file it's a copy of */
        public Dictionary<string, string> Copies = new Dictionary<string, string>();
        public DateTime CompletedUtc;

        public IEnumerable<string> WithStatus(FileStatus status)
        {
            return Files.Where(o => o.Value == status).Select(o => o.Key);
        }

        public int CountOf(FileStatus status)
        {
            return Files.Count(o => o.Value == status);
        }

        /// <summary>
        /// Levels containing at least one file of the given status.
        /// </summary>
        public List<string> LevelsWith(FileStatus status)
        {
            return WithStatus(status).Select(ModToolkit.LevelOf).Where(o => o != null).Distinct().OrderBy(o => o).ToList();
        }

        public List<string> FilesOfLevel(string level)
        {
            string prefix = ModToolkit.LevelFolder(level);
            return Files.Keys.Where(o => o.StartsWith(prefix)).OrderBy(o => o).ToList();
        }
    }

    /* Compares the install against the vanilla manifest and the mod state, file by file, using the
     * hash cache so only files that changed since last time are actually re-read. */
    public class InstallScanner
    {
        private readonly string _gameRoot;
        private readonly VanillaManifest _manifest;
        private readonly HashCache _cache;
        private readonly ModState _state;

        public InstallScanner(string gameRoot, VanillaManifest manifest, HashCache cache, ModState state)
        {
            _gameRoot = gameRoot;
            _manifest = manifest;
            _cache = cache;
            _state = state;
        }

        /// <summary>
        /// Scan the whole DATA folder. Reports (bytes read, bytes to read) through the callback if given.
        /// </summary>
        public ScanResult ScanAll(Action<long, long> progress = null)
        {
            return Scan(null, progress);
        }

        /// <summary>
        /// Scan only paths under the given normalised prefix (e.g. one level's folder), or
        /// everything when the prefix is null. Reports (bytes read, bytes to read) through the callback if given: only files
        /// changed since they were last read are read (the first scan reads everything).
        /// </summary>
        public ScanResult Scan(string normalisedPrefix, Action<long, long> progress = null)
        {
            //The union of what ships and what's on disk, so additions and deletions both show
            HashSet<string> paths = new HashSet<string>();
            foreach (CathodeLib.FileHashTable.Entry entry in _manifest.Entries)
                paths.Add(entry.Path);

            string dataDir = Path.Combine(_gameRoot, "DATA");
            if (Directory.Exists(dataDir))
            {
                foreach (string file in Directory.GetFiles(dataDir, "*", SearchOption.AllDirectories))
                {
                    string normalised = ModToolkit.NormaliseFull(_gameRoot, file);
                    if (normalised != null)
                        paths.Add(normalised);
                }
            }

            List<string> filtered = paths
                .Where(o => !ModToolkit.IsExcluded(o))
                .Where(o => normalisedPrefix == null || o.StartsWith(normalisedPrefix))
                .OrderBy(o => o)
                .ToList();

            //What the last apply wrote (worked-out files like the level list included), for the Managed classification
            Dictionary<string, HashSet<string>> managedHashes = new Dictionary<string, HashSet<string>>();
            foreach (KeyValuePair<string, ModState.ComposedFile> composed in _state.Composition)
                if (composed.Value.Sha256Hex != null)
                    managedHashes[composed.Key] = new HashSet<string>() { composed.Value.Sha256Hex };
            foreach (ModState.InstalledMod mod in _state.Mods.Where(o => o.Enabled))
            {
                foreach (KeyValuePair<string, string> applied in mod.Applied)
                {
                    HashSet<string> hashes;
                    if (!managedHashes.TryGetValue(applied.Key, out hashes))
                        managedHashes[applied.Key] = hashes = new HashSet<string>();
                    hashes.Add(applied.Value);
                }
            }

            /* Every file read (those changed since last time - every one, the first time), several at a time. Even those whose
               status their size gives away: what's worked out from the scan next (what to put in a mod, what the user changed
               themselves) needs their hashes, and would otherwise read them one by one, on the UI thread */
            _cache.HashAll(filtered, progress);

            ScanResult result = new ScanResult();
            foreach (string path in filtered)
            {
                byte[] hash = _cache.Hash(path);
                FileStatus status;
                if (hash == null)
                    status = FileStatus.Missing;
                else
                {
                    string hex = ModToolkit.ToHex(hash);
                    HashSet<string> managed;
                    if (_manifest.IsVanilla(path, hash))
                        status = FileStatus.Vanilla;
                    else if (managedHashes.TryGetValue(path, out managed) && managed.Contains(hex))
                        status = FileStatus.Managed;
                    else if (_manifest.Contains(path))
                        status = FileStatus.Modified;
                    else if (_manifest.CopyOf(path, hash) is string original)
                    {
                        status = FileStatus.Copy;
                        result.Copies[path] = original;
                    }
                    else
                        status = FileStatus.Foreign;
                }
                result.Files[path] = status;
            }

            /* In a level, copies are copies only in one the game ships that has nothing else of the user's in it (a folder of it
               renamed and put back). A level the game doesn't ship is the user's whole: one made in OpenCAGE starts out with
               copies of another level's files (its sound banks, its galaxy), and a duplicated level is nothing but copies until
               it's changed - they're as much a part of it as the rest. So is any copy in a level the user changed. */
            HashSet<string> levelsChanged = new HashSet<string>(result.Files.Where(o => o.Value != FileStatus.Vanilla && o.Value != FileStatus.Copy)
                .Select(o => ModInstaller.LevelUnitOf(o.Key)).Where(o => o != null));
            HashSet<string> levelsShipped = result.Copies.Count == 0 ? new HashSet<string>()
                : new HashSet<string>(_manifest.Entries.Select(o => ModInstaller.LevelUnitOf(o.Path)).Where(o => o != null));
            foreach (string path in result.Copies.Keys.ToList())
            {
                string level = ModInstaller.LevelUnitOf(path);
                if (level == null || (levelsShipped.Contains(level) && !levelsChanged.Contains(level)))
                    continue;
                result.Files[path] = FileStatus.Foreign;
                result.Copies.Remove(path);
            }

            _cache.Save();
            result.CompletedUtc = DateTime.UtcNow;
            return result;
        }
    }
}
#endif

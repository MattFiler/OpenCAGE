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
    }

    public class ScanResult
    {
        public Dictionary<string, FileStatus> Files = new Dictionary<string, FileStatus>();
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
            string prefix = "DATA/ENV/PRODUCTION/" + level + "/";
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
        /// Scan the whole DATA folder. Reports (done, total) through the callback if given.
        /// </summary>
        public ScanResult ScanAll(Action<int, int> progress = null)
        {
            return Scan(null, progress);
        }

        /// <summary>
        /// Scan only paths under the given normalised prefix (e.g. one level's folder), or
        /// everything when the prefix is null.
        /// </summary>
        public ScanResult Scan(string normalisedPrefix, Action<int, int> progress = null)
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

            //What each enabled mod last wrote, for the Managed classification
            Dictionary<string, HashSet<string>> managedHashes = new Dictionary<string, HashSet<string>>();
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

            ScanResult result = new ScanResult();
            int done = 0;
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
                    else
                        status = FileStatus.Foreign;
                }
                result.Files[path] = status;

                done++;
                if (progress != null && (done % 25 == 0 || done == filtered.Count))
                    progress(done, filtered.Count);
            }

            _cache.Save();
            result.CompletedUtc = DateTime.UtcNow;
            return result;
        }
    }
}
#endif

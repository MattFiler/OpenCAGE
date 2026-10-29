using CATHODE.Scripting.Internal;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.Modding
{
    /* The shipped truth: which files a clean install has and what their bytes hash to.
     *
     * Backed by the FILE_HASHES CustomTable in CathodeLib's embedded info.dat (generated from
     * verified installs by VanillaManifestGenerator). Everything that needs to tell "vanilla"
     * from "modified" goes through here.
     *
     * Builds are identified by CathodeLib.PatchManager.Platform (the same enum PatchManager
     * detects an install with), not by ad-hoc strings: the table stores each distinct hash once
     * with a bitmask of every platform that ships those bytes, and this class is one platform's
     * view of it. */
    public class VanillaManifest
    {
        private readonly FileHashTable _table;
        public PatchManager.Platform Platform { get; private set; }
        private readonly int _bit;

        /// <summary>
        /// Whether the table has this platform's files: the table holds every build's, and not every build has been
        /// recorded (none of mobile or Mac/Linux, say), so a table with rows can still know nothing of this one.
        /// </summary>
        public bool Available { get; private set; }

        /// <summary>
        /// Why mods can't be installed or made for this build, or null when they can. Mods are combined with the game's
        /// files and taken out again by putting those back: without this build's hashes, nothing tells its files from
        /// changed ones - every file would look like one a mod added.
        /// </summary>
        public string Unsupported
        {
            get
            {
                if (Available)
                    return null;
                if (Platform == PatchManager.Platform.UNKNOWN)
                    return "OpenCAGE can't tell which version of the game this is, so it can't install or make mods for it.";
                return "OpenCAGE has no record of the " + ModInstaller.PlatformName(Platform) + " version's original files, so it can't install or make mods for it.";
            }
        }

        public VanillaManifest(PatchManager.Platform platform = PatchManager.Platform.STEAM)
            : this(Shipped, platform) { }

        //The hash table comes from the info.dat CathodeLib SHIPS, never from a local override -
        //this is the record we tell modified data from, so a project-supplied file would let the
        //thing being checked define what "vanilla" means. Parsed once; it is ~11,700 paths.
        private static FileHashTable _shipped;
        private static readonly object _shippedLock = new object();
        private static FileHashTable Shipped
        {
            get
            {
                lock (_shippedLock)
                {
                    if (_shipped == null)
                        _shipped = CustomTable.ReadEmbeddedTable(CustomTableType.FILE_HASHES) as FileHashTable ?? new FileHashTable();
                    return _shipped;
                }
            }
        }

        public VanillaManifest(FileHashTable table, PatchManager.Platform platform = PatchManager.Platform.STEAM)
        {
            _table = table;
            Platform = platform;
            _bit = FileHashTable.PlatformBit(platform);
            using (IEnumerator<FileHashTable.Entry> entries = Entries.GetEnumerator())
                Available = entries.MoveNext();
        }

        public FileHashTable.Entry Lookup(string normalisedPath)
        {
            if (_table == null)
                return null;
            return _table.Lookup(Platform, normalisedPath);
        }

        public bool Contains(string normalisedPath)
        {
            return Lookup(normalisedPath) != null;
        }

        /// <summary>
        /// Does this hash match the bytes this platform ships for the path?
        /// </summary>
        public bool IsVanilla(string normalisedPath, byte[] sha256)
        {
            FileHashTable.Entry entry = Lookup(normalisedPath);
            return entry != null && entry.Sha256 != null && sha256 != null && SameBytes(entry.Sha256, sha256);
        }

        /* Every file this platform ships, by the hash of its bytes - made the first time a copy is looked for */
        private Dictionary<string, List<string>> _pathsByContent;
        private readonly object _pathsByContentLock = new object();

        /// <summary>
        /// The shipped file this one is a copy of: a file of the same name somewhere else in the game, with the very same
        /// bytes - or null. A game folder renamed and then put back (by the store's file check, say), or a backup of one,
        /// leaves nothing but these behind: they're the game's own files, not changes.
        /// </summary>
        public string CopyOf(string normalisedPath, byte[] sha256)
        {
            if (normalisedPath == null || sha256 == null || _table == null)
                return null;
            lock (_pathsByContentLock)
            {
                if (_pathsByContent == null)
                {
                    _pathsByContent = new Dictionary<string, List<string>>();
                    foreach (FileHashTable.Entry entry in Entries)
                    {
                        if (entry.Sha256 == null)
                            continue;
                        string key = ModToolkit.ToHex(entry.Sha256);
                        if (!_pathsByContent.TryGetValue(key, out List<string> paths))
                            _pathsByContent[key] = paths = new List<string>();
                        paths.Add(entry.Path);
                    }
                }
            }
            if (!_pathsByContent.TryGetValue(ModToolkit.ToHex(sha256), out List<string> same))
                return null;
            string name = normalisedPath.Substring(normalisedPath.LastIndexOf('/') + 1);
            return same.FirstOrDefault(o => o != normalisedPath && o.Substring(o.LastIndexOf('/') + 1) == name);
        }

        private static bool SameBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i])
                    return false;
            return true;
        }

        /// <summary>
        /// Every file this platform's build ships (one entry per path - the variant carrying
        /// this platform's bit).
        /// </summary>
        public IEnumerable<FileHashTable.Entry> Entries
        {
            get
            {
                if (_table == null)
                    yield break;
                foreach (KeyValuePair<string, List<FileHashTable.Entry>> file in _table.files)
                    for (int i = 0; i < file.Value.Count; i++)
                        if ((file.Value[i].Platforms & _bit) != 0)
                        {
                            yield return file.Value[i];
                            break;
                        }
            }
        }

        public int Count
        {
            get
            {
                int count = 0;
                foreach (FileHashTable.Entry entry in Entries)
                    count++;
                return count;
            }
        }
    }
}

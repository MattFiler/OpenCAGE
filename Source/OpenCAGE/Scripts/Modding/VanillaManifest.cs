using CATHODE.Scripting.Internal;
using CathodeLib;
using System;
using System.Collections.Generic;

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

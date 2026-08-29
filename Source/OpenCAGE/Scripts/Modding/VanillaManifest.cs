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

        public bool Available { get { return _table != null && _table.files.Count != 0; } }

        public VanillaManifest(PatchManager.Platform platform = PatchManager.Platform.STEAM)
            : this(CustomTable.Vanilla.FileHashes, platform) { }

        public VanillaManifest(FileHashTable table, PatchManager.Platform platform = PatchManager.Platform.STEAM)
        {
            _table = table;
            Platform = platform;
            _bit = FileHashTable.PlatformBit(platform);
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

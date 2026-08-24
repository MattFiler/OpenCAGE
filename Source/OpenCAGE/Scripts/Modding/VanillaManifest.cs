using CathodeLib;
using System;
using System.Collections.Generic;

namespace OpenCAGE.Modding
{
    /* The shipped truth: which files a clean install has and what their bytes hash to.
     *
     * Backed by the FILE_HASHES CustomTable in CathodeLib's embedded info.dat (generated from a
     * verified install by VanillaManifestGenerator). Everything that needs to tell "vanilla" from
     * "modified" goes through here. */
    public class VanillaManifest
    {
        public const string DefaultSet = "STEAM_PC";

        private readonly Dictionary<string, FileHashTable.Entry> _entries;
        public string SetName { get; private set; }
        public bool Available { get { return _entries != null; } }
        public int Count { get { return _entries == null ? 0 : _entries.Count; } }

        public VanillaManifest(string setName = DefaultSet) : this(CustomTable.Vanilla.FileHashes, setName) { }

        public VanillaManifest(FileHashTable table, string setName = DefaultSet)
        {
            SetName = setName;
            Dictionary<string, FileHashTable.Entry> entries = null;
            if (table != null && !table.sets.TryGetValue(setName, out entries) && table.sets.Count != 0)
            {
                //Fall back to whichever set we do have rather than claiming no manifest at all
                foreach (KeyValuePair<string, Dictionary<string, FileHashTable.Entry>> set in table.sets)
                {
                    SetName = set.Key;
                    entries = set.Value;
                    break;
                }
            }
            _entries = entries;
        }

        public FileHashTable.Entry Lookup(string normalisedPath)
        {
            if (_entries == null)
                return null;
            FileHashTable.Entry entry;
            _entries.TryGetValue(normalisedPath, out entry);
            return entry;
        }

        public bool Contains(string normalisedPath)
        {
            return Lookup(normalisedPath) != null;
        }

        /// <summary>
        /// Does this hash match the shipped bytes for the path?
        /// </summary>
        public bool IsVanilla(string normalisedPath, byte[] sha256)
        {
            FileHashTable.Entry entry = Lookup(normalisedPath);
            if (entry == null || entry.Sha256 == null || sha256 == null || entry.Sha256.Length != sha256.Length)
                return false;
            for (int i = 0; i < sha256.Length; i++)
                if (entry.Sha256[i] != sha256[i])
                    return false;
            return true;
        }

        public IEnumerable<FileHashTable.Entry> Entries
        {
            get { return _entries == null ? (IEnumerable<FileHashTable.Entry>)new FileHashTable.Entry[0] : _entries.Values; }
        }
    }
}

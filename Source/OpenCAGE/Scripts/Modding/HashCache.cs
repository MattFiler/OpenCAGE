using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace OpenCAGE.Modding
{
    /* Hashing 15GB of game data is a one-off cost, not a recurring one: this is a git-style index
     * of (size, mtime) -> sha256 so a rescan only re-reads files that actually moved. */
    public class HashCache
    {
        public class Record
        {
            [JsonProperty("s")] public long Size;
            [JsonProperty("m")] public long MTimeTicks;
            [JsonProperty("h")] public string Sha256Hex;
        }

        private class FileModel
        {
            [JsonProperty("version")] public int Version = 1;
            [JsonProperty("entries")] public Dictionary<string, Record> Entries = new Dictionary<string, Record>();
        }

        private readonly string _gameRoot;
        private readonly string _cachePath;
        private FileModel _model;
        private bool _dirty;

        public HashCache(string gameRoot)
        {
            _gameRoot = gameRoot;
            _cachePath = ModToolkit.HashCacheFile(gameRoot);
            Load();
        }

        private void Load()
        {
            _model = null;
            try
            {
                if (File.Exists(_cachePath))
                    _model = JsonConvert.DeserializeObject<FileModel>(File.ReadAllText(_cachePath));
            }
            catch { }
            if (_model == null || _model.Entries == null)
                _model = new FileModel();
        }

        public void Save()
        {
            if (!_dirty)
                return;
            Directory.CreateDirectory(Path.GetDirectoryName(_cachePath));
            File.WriteAllText(_cachePath, JsonConvert.SerializeObject(_model));
            _dirty = false;
        }

        /// <summary>
        /// The file's current content hash, re-read only if size or mtime moved since last time.
        /// Returns null if the file does not exist.
        /// </summary>
        public byte[] Hash(string normalisedPath)
        {
            string fullPath = ModToolkit.Denormalise(_gameRoot, normalisedPath);
            FileInfo info = new FileInfo(fullPath);
            if (!info.Exists)
            {
                if (_model.Entries.Remove(normalisedPath))
                    _dirty = true;
                return null;
            }

            Record record;
            if (_model.Entries.TryGetValue(normalisedPath, out record)
                && record.Size == info.Length && record.MTimeTicks == info.LastWriteTimeUtc.Ticks)
                return ModToolkit.FromHex(record.Sha256Hex);

            byte[] hash = ModToolkit.Sha256File(fullPath);
            _model.Entries[normalisedPath] = new Record()
            {
                Size = info.Length,
                MTimeTicks = info.LastWriteTimeUtc.Ticks,
                Sha256Hex = ModToolkit.ToHex(hash),
            };
            _dirty = true;
            return hash;
        }

        /// <summary>
        /// Forget a path, forcing a re-hash next time (call after writing a file).
        /// </summary>
        public void Invalidate(string normalisedPath)
        {
            if (_model.Entries.Remove(normalisedPath))
                _dirty = true;
        }
    }
}

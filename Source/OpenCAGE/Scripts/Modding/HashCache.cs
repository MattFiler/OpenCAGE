using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Threading;

namespace OpenCAGE.Modding
{
    /* Hashing 30GB of game data is a one-off cost, not a recurring one: this is a git-style index
     * of (size, mtime) -> sha256 so a rescan only re-reads files that actually moved. Anything that
     * writes a file - a game file check, a mod, OpenCAGE, another tool - gives it a new time, so it
     * is read again; a file still as it was isn't.
     *
     * Safe to use from several threads at once (the Mod Manager's check of the game files can run beside
     * the capture of small files at startup, and an apply): the index is locked, files are read outside it. */
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
        private readonly object _lock = new object();
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

        /// <summary>Keep the index on disk. It's only ever a shortcut: when it can't be written, files are read again next time.</summary>
        public void Save()
        {
            lock (_lock)
            {
                if (!_dirty)
                    return;
                //Written beside and swapped in, so a check stopped part-way through a write keeps the last whole index (named
                //for this write: another index of the same game - the shader database keeps one - may be saving too)
                string temp = _cachePath + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_cachePath));
                    File.WriteAllText(temp, JsonConvert.SerializeObject(_model));
                    if (File.Exists(_cachePath)) File.Delete(_cachePath);
                    File.Move(temp, _cachePath);
                    _dirty = false;
                }
                catch
                {
                    try { File.Delete(temp); } catch { }
                }
            }
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
                Forget(normalisedPath);
                return null;
            }

            byte[] cached = Cached(normalisedPath, info);
            if (cached != null)
                return cached;
            //Of the file as the mod system sees it: without what OpenCAGE adds for its own use (ModToolkit.WithoutOwnContent)
            byte[] hash = ModToolkit.ContentSha(normalisedPath, fullPath);
            //Changed while it was read (written beside a check): kept out of the index, so it's read again next time
            if (Unchanged(info, fullPath))
                Remember(normalisedPath, info, hash);
            return hash;
        }

        /* The file is still as it was before it was read (same size and time): what was read is all of one version of it */
        private static bool Unchanged(FileInfo before, string fullPath)
        {
            FileInfo after = new FileInfo(fullPath);
            return after.Exists && after.Length == before.Length && after.LastWriteTimeUtc == before.LastWriteTimeUtc;
        }

        /// <summary>
        /// Make sure the hash of every one of these files is known, reading the ones that changed since they were last read
        /// (or never were) several at a time - as many as the drive they're on reads well at once. Reports (bytes read, bytes
        /// to read) as it goes, and keeps what it has every few seconds, so a check stopped part-way doesn't start again from
        /// nothing. Throws, once the files being read when it happened are done, if one can't be read.
        /// </summary>
        public void HashAll(IEnumerable<string> normalisedPaths, Action<long, long> progress = null)
        {
            List<(string Path, string Full, FileInfo Info)> todo = new List<(string, string, FileInfo)>();
            foreach (string path in normalisedPaths.Distinct())
            {
                string fullPath = ModToolkit.Denormalise(_gameRoot, path);
                FileInfo info = new FileInfo(fullPath);
                if (!info.Exists)
                    Forget(path);
                else if (Cached(path, info) == null)
                    todo.Add((path, fullPath, info));
            }
            long total = todo.Sum(o => o.Info.Length), done = 0;
            progress?.Invoke(0, total);
            if (todo.Count == 0)
                return;

            int readers = Math.Min(todo.Count, ModToolkit.ReadersFor(_gameRoot));
            //One at a time (a spinning disk) in folder order, as the files lie on it; several, biggest first, so no big file
            //is left reading alone at the end
            todo = readers == 1 ? todo.OrderBy(o => o.Path, StringComparer.Ordinal).ToList() : todo.OrderByDescending(o => o.Info.Length).ToList();

            int next = -1;
            Exception failure = null;
            Stopwatch sinceReport = Stopwatch.StartNew(), sinceSave = Stopwatch.StartNew();
            object reporting = new object();
            void Read(long bytes)
            {
                long now = Interlocked.Add(ref done, bytes);
                if (progress == null || sinceReport.ElapsedMilliseconds < 100)
                    return;
                lock (reporting)
                {
                    if (sinceReport.ElapsedMilliseconds < 100)
                        return;
                    sinceReport.Restart();
                    //A file grown since the list was made reads more than it said
                    progress(Math.Min(now, total), total);
                }
            }

            Thread[] workers = new Thread[readers];
            for (int t = 0; t < readers; t++)
            {
                workers[t] = new Thread(() =>
                {
                    try
                    {
                        using (SHA256 sha = ModToolkit.NewSha256())
                        {
                            byte[] buffer = new byte[1 << 20];
                            int i;
                            while (Volatile.Read(ref failure) == null && (i = Interlocked.Increment(ref next)) < todo.Count)
                            {
                                //As the file is now (it may have changed since the list was made), and kept only if it didn't change while it was read
                                (string path, string fullPath, _) = todo[i];
                                FileInfo info = new FileInfo(fullPath);
                                byte[] hash = null;
                                if (info.Exists)
                                {
                                    try { hash = ModToolkit.ContentSha(sha, path, fullPath, buffer, Read); }
                                    catch (FileNotFoundException) { }
                                    catch (DirectoryNotFoundException) { }
                                }
                                if (hash == null) Forget(path);
                                else if (Unchanged(info, fullPath)) Remember(path, info, hash);

                                if (sinceSave.ElapsedMilliseconds > 10000)
                                {
                                    sinceSave.Restart();
                                    Save();
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Interlocked.CompareExchange(ref failure, e, null);
                    }
                })
                { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "Game file check" };
                workers[t].Start();
            }
            foreach (Thread worker in workers)
                worker.Join();
            progress?.Invoke(total, total);
            Save();
            if (failure != null)
                ExceptionDispatchInfo.Capture(failure).Throw();
        }

        /// <summary>
        /// Forget a path, forcing a re-hash next time (call after writing a file).
        /// </summary>
        public void Invalidate(string normalisedPath)
        {
            Forget(normalisedPath);
        }

        /* The hash recorded for the file as it is now (same size and time), or null */
        private byte[] Cached(string normalisedPath, FileInfo info)
        {
            lock (_lock)
                return _model.Entries.TryGetValue(normalisedPath, out Record record) && record.Size == info.Length && record.MTimeTicks == info.LastWriteTimeUtc.Ticks
                    ? ModToolkit.FromHex(record.Sha256Hex) : null;
        }

        /* Recorded against the size and time from before the file was read: one changed while it was read doesn't match
           them next time, and is read again */
        private void Remember(string normalisedPath, FileInfo before, byte[] hash)
        {
            lock (_lock)
            {
                _model.Entries[normalisedPath] = new Record()
                {
                    Size = before.Length,
                    MTimeTicks = before.LastWriteTimeUtc.Ticks,
                    Sha256Hex = ModToolkit.ToHex(hash),
                };
                _dirty = true;
            }
        }

        private void Forget(string normalisedPath)
        {
            lock (_lock)
                if (_model.Entries.Remove(normalisedPath))
                    _dirty = true;
        }
    }
}

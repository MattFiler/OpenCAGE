#if ENABLE_MOD_PACKAGES
using CATHODE;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace OpenCAGE.Modding
{
    public class ModConflict
    {
        public string ModA;
        public string ModB;
        public string Target;
        public string Detail;
    }

    public class TransactionResult
    {
        public bool Success;
        public string Error;
        public List<string> Warnings = new List<string>();
    }

    /* All installs, uninstalls, reorders and repairs are the same operation: put every affected
     * file back to its baseline bytes, then apply the enabled mods over it in priority order. That
     * one shape makes ordering, disabling and uninstalling trivially consistent - and because every
     * write is journalled first, a failure puts the install back exactly as it was. */
    public class ModInstaller
    {
        private readonly string _gameRoot;
        private readonly VanillaManifest _manifest;
        private readonly HashCache _cache;
        private readonly ModState _state;
        private readonly BaselineStore _store;

        /* Captures run from editor save paths and a background thread while the manager UI can be
         * mid-transaction - one mutex covers every state-touching entry point */
        private readonly object _mutex = new object();

        public ModState State { get { return _state; } }
        public BaselineStore Store { get { return _store; } }

        public ModInstaller(string gameRoot, VanillaManifest manifest, HashCache cache, ModState state, BaselineStore store)
        {
            _gameRoot = gameRoot;
            _manifest = manifest;
            _cache = cache;
            _state = state;
            _store = store;
        }

        #region LIBRARY
        /// <summary>
        /// Bring a package into the library (disabled). An existing mod with the same id is
        /// updated in place; returns the state record either way.
        /// </summary>
        public ModState.InstalledMod ImportPackage(string packagePath)
        {
            ModPackage package = ModPackage.Read(packagePath);
            if (package.Info.Platform != "PC")
                throw new Exception("This package targets " + package.Info.Platform + " game data, not the PC build.");

            string libraryFileName = package.Info.Id + ModToolkit.PackageExtension;
            string libraryPath = Path.Combine(ModToolkit.LibraryDir(_gameRoot), libraryFileName);
            Directory.CreateDirectory(ModToolkit.LibraryDir(_gameRoot));
            if (!string.Equals(Path.GetFullPath(packagePath), Path.GetFullPath(libraryPath), StringComparison.OrdinalIgnoreCase))
                File.Copy(packagePath, libraryPath, true);

            ModState.InstalledMod record = _state.FindMod(package.Info.Id);
            if (record == null)
            {
                record = new ModState.InstalledMod()
                {
                    Id = package.Info.Id,
                    Enabled = false,
                    Priority = _state.Mods.Count == 0 ? 0 : _state.Mods.Max(o => o.Priority) + 1,
                };
                _state.Mods.Add(record);
            }
            record.Name = package.Info.Name;
            record.Version = package.Info.Version;
            record.Author = package.Info.Author;
            record.Description = package.Info.Description;
            record.PackageFileName = libraryFileName;
            _state.Save();
            return record;
        }

        public ModPackage OpenPackage(ModState.InstalledMod mod)
        {
            return ModPackage.Read(Path.Combine(ModToolkit.LibraryDir(_gameRoot), mod.PackageFileName));
        }

        /// <summary>
        /// Drop a mod from the library. Refuses while it's applied - disable it first.
        /// </summary>
        public void RemoveFromLibrary(string id)
        {
            ModState.InstalledMod mod = _state.FindMod(id);
            if (mod == null)
                return;
            if (mod.Applied.Count != 0)
                throw new Exception("'" + mod.Name + "' is currently applied - disable it before removing it.");
            string libraryPath = Path.Combine(ModToolkit.LibraryDir(_gameRoot), mod.PackageFileName ?? "");
            if (File.Exists(libraryPath))
                File.Delete(libraryPath);
            _state.Mods.Remove(mod);
            _state.Save();
        }
        #endregion

        #region CONFLICTS
        /// <summary>
        /// Pairwise overlaps between the given mods' claims. Order in the list decides who wins;
        /// these are warnings for the user, not blockers.
        /// </summary>
        public List<ModConflict> FindConflicts(List<ModState.InstalledMod> mods)
        {
            List<ModConflict> conflicts = new List<ModConflict>();
            List<ModPackage> packages = new List<ModPackage>();
            foreach (ModState.InstalledMod mod in mods)
            {
                try { packages.Add(OpenPackage(mod)); }
                catch { packages.Add(null); }
            }

            for (int a = 0; a < mods.Count; a++)
            {
                for (int b = a + 1; b < mods.Count; b++)
                {
                    if (packages[a] == null || packages[b] == null)
                        continue;
                    foreach (ModPackageEntry entryA in packages[a].Info.Entries)
                    {
                        foreach (ModPackageEntry entryB in packages[b].Info.Entries)
                        {
                            if (entryA.Target != entryB.Target)
                                continue;

                            if (entryA.Kind == ModPackageEntry.KindBml && entryB.Kind == ModPackageEntry.KindBml)
                            {
                                //Config vs config only collides when they touch the same values
                                List<string> shared = entryA.Claims.Intersect(entryB.Claims).ToList();
                                if (shared.Count != 0)
                                    conflicts.Add(new ModConflict()
                                    {
                                        ModA = mods[a].Name,
                                        ModB = mods[b].Name,
                                        Target = entryA.Target,
                                        Detail = "Both change " + shared.Count + " of the same config value" + (shared.Count == 1 ? "" : "s") + " (e.g. " + shared[0] + ")",
                                    });
                            }
                            else
                            {
                                conflicts.Add(new ModConflict()
                                {
                                    ModA = mods[a].Name,
                                    ModB = mods[b].Name,
                                    Target = entryA.Target,
                                    Detail = "Both change this file - whichever loads later wins",
                                });
                            }
                        }
                    }
                }
            }
            return conflicts;
        }
        #endregion

        #region SNAPSHOTS
        private class Snapshot
        {
            [JsonProperty("id")] public string Id;
            [JsonProperty("name")] public string Name;
            [JsonProperty("dateUtc")] public DateTime DateUtc;
            /* PATH -> sha256 hex held in the store, or null when the file did not exist */
            [JsonProperty("files")] public Dictionary<string, string> Files = new Dictionary<string, string>();
        }

        private string SnapshotPath(string id)
        {
            return Path.Combine(ModToolkit.SnapshotsDir(_gameRoot), id + ".json");
        }

        /// <summary>
        /// Record the current bytes of the given paths so they can be put back later. Returns the
        /// snapshot id.
        /// </summary>
        public string CreateSnapshot(string name, IEnumerable<string> normalisedPaths)
        {
            Snapshot snapshot = new Snapshot()
            {
                Id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                Name = name,
                DateUtc = DateTime.UtcNow,
            };
            foreach (string path in normalisedPaths.Distinct())
            {
                string fullPath = ModToolkit.Denormalise(_gameRoot, path);
                snapshot.Files[path] = File.Exists(fullPath) ? _store.Store(File.ReadAllBytes(fullPath)) : null;
            }
            Directory.CreateDirectory(ModToolkit.SnapshotsDir(_gameRoot));
            File.WriteAllText(SnapshotPath(snapshot.Id), JsonConvert.SerializeObject(snapshot, Newtonsoft.Json.Formatting.Indented));
            return snapshot.Id;
        }

        public void RestoreSnapshot(string id, bool delete = false)
        {
            Snapshot snapshot = JsonConvert.DeserializeObject<Snapshot>(File.ReadAllText(SnapshotPath(id)));
            foreach (KeyValuePair<string, string> file in snapshot.Files)
            {
                string fullPath = ModToolkit.Denormalise(_gameRoot, file.Key);
                if (file.Value == null)
                {
                    if (File.Exists(fullPath))
                        File.Delete(fullPath);
                }
                else
                {
                    byte[] content = _store.Retrieve(file.Value);
                    if (content == null)
                        throw new Exception("Snapshot content for " + file.Key + " is missing from the baseline store.");
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                    File.WriteAllBytes(fullPath, content);
                }
                _cache.Invalidate(file.Key);
            }
            _cache.Save();
            if (delete)
                File.Delete(SnapshotPath(id));
        }

        public List<string> SnapshotFiles(string id)
        {
            Snapshot snapshot = JsonConvert.DeserializeObject<Snapshot>(File.ReadAllText(SnapshotPath(id)));
            return snapshot.Files.Keys.ToList();
        }
        #endregion

        #region BASELINE
        /// <summary>
        /// If the file's current bytes are vanilla, tuck a copy into the baseline store so exports
        /// can diff against it and uninstalls can restore it - forever, however the file changes
        /// later. Cheap when already captured. Never throws: a failed capture must not break a save.
        /// </summary>
        public void CaptureVanillaBaseline(string normalisedPath, bool saveState = true)
        {
            try
            {
                lock (_mutex)
                {
                    ModState.BaselineRecord existing;
                    if (_state.Baseline.TryGetValue(normalisedPath, out existing) && existing.IsVanilla)
                        return;

                    byte[] hash = _cache.Hash(normalisedPath);
                    if (hash == null || !_manifest.IsVanilla(normalisedPath, hash))
                        return;

                    string sha = _store.StoreFile(ModToolkit.Denormalise(_gameRoot, normalisedPath));
                    _state.Baseline[normalisedPath] = new ModState.BaselineRecord() { Sha256Hex = sha, IsVanilla = true };
                    if (saveState)
                    {
                        _state.Save();
                        _cache.Save();
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Flush state after a run of CaptureVanillaBaseline(path, false) calls.
        /// </summary>
        public void SaveState()
        {
            lock (_mutex)
            {
                _state.Save();
                _cache.Save();
            }
        }

        /// <summary>
        /// The vanilla bytes for a path, when the baseline store holds them.
        /// </summary>
        public byte[] VanillaBytes(string normalisedPath)
        {
            ModState.BaselineRecord record;
            if (!_state.Baseline.TryGetValue(normalisedPath, out record) || !record.IsVanilla || record.Sha256Hex == null)
                return null;
            return _store.Retrieve(record.Sha256Hex);
        }

        /* Make sure every affected path has some baseline to restore to. Returns the paths that
         * cannot get one (shipped files with unrecognised bytes) unless adoption is allowed. */
        private List<string> PrepareBaselines(HashSet<string> paths, bool adoptUnknown)
        {
            List<string> blocked = new List<string>();
            foreach (string path in paths)
            {
                ModState.BaselineRecord record;
                if (_state.Baseline.TryGetValue(path, out record)
                    && (record.Sha256Hex == null || _store.Has(record.Sha256Hex)))
                    continue;

                string fullPath = ModToolkit.Denormalise(_gameRoot, path);
                if (!File.Exists(fullPath))
                {
                    //Nothing there now: baseline is "absent" (covers files that mods add)
                    _state.Baseline[path] = new ModState.BaselineRecord() { Sha256Hex = null, IsVanilla = !_manifest.Contains(path) };
                    continue;
                }

                byte[] hash = _cache.Hash(path);
                if (_manifest.IsVanilla(path, hash))
                {
                    string sha = _store.StoreFile(fullPath);
                    _state.Baseline[path] = new ModState.BaselineRecord() { Sha256Hex = sha, IsVanilla = true };
                }
                else if (adoptUnknown || !_manifest.Contains(path))
                {
                    //Unknown bytes adopted as the restore point (or a foreign file with no shipped truth)
                    string sha = _store.StoreFile(fullPath);
                    _state.Baseline[path] = new ModState.BaselineRecord() { Sha256Hex = sha, IsVanilla = false };
                }
                else
                    blocked.Add(path);
            }
            return blocked;
        }
        #endregion

        #region TRANSACTION
        public bool HasCrashJournal()
        {
            return File.Exists(ModToolkit.JournalFile(_gameRoot));
        }

        /// <summary>
        /// Put back the bytes a crashed transaction journalled, if any.
        /// </summary>
        public void RecoverCrashJournal()
        {
            string journalPath = ModToolkit.JournalFile(_gameRoot);
            if (!File.Exists(journalPath))
                return;
            string id = File.ReadAllText(journalPath).Trim();
            RestoreSnapshot(id, true);
            File.Delete(journalPath);
        }

        /// <summary>
        /// Move the whole install to the desired configuration: which mods are enabled, in which
        /// order. Everything else - install, uninstall, reorder, repair - is a call to this.
        /// </summary>
        public TransactionResult ApplyConfiguration(List<string> enabledIdsInOrder, bool adoptUnknownBaselines = false)
        {
            lock (_mutex)
                return ApplyConfigurationLocked(enabledIdsInOrder, adoptUnknownBaselines);
        }

        private TransactionResult ApplyConfigurationLocked(List<string> enabledIdsInOrder, bool adoptUnknownBaselines)
        {
            TransactionResult result = new TransactionResult();

            //Which mods are involved: everything applied now, plus everything wanted
            List<ModState.InstalledMod> desired = new List<ModState.InstalledMod>();
            foreach (string id in enabledIdsInOrder)
            {
                ModState.InstalledMod mod = _state.FindMod(id);
                if (mod == null)
                {
                    result.Error = "No mod with id " + id + " in the library.";
                    return result;
                }
                desired.Add(mod);
            }

            Dictionary<ModState.InstalledMod, ModPackage> packages = new Dictionary<ModState.InstalledMod, ModPackage>();
            foreach (ModState.InstalledMod mod in desired)
            {
                try { packages[mod] = OpenPackage(mod); }
                catch (Exception e)
                {
                    result.Error = "Could not open the package for '" + mod.Name + "': " + e.Message;
                    return result;
                }
            }

            HashSet<string> affected = new HashSet<string>();
            foreach (ModState.InstalledMod mod in _state.Mods)
                foreach (string path in mod.Applied.Keys)
                    affected.Add(path);
            foreach (ModPackage package in packages.Values)
                foreach (ModPackageEntry entry in package.Info.Entries)
                    affected.Add(entry.Target);

            //Baselines for everything we're about to touch
            List<string> blocked = PrepareBaselines(affected, adoptUnknownBaselines);
            if (blocked.Count != 0)
            {
                result.Error = "These files aren't vanilla and no pristine copy of them is stored, so they can't be safely restored later:\n  "
                    + string.Join("\n  ", blocked.Take(10).ToArray())
                    + (blocked.Count > 10 ? "\n  ...and " + (blocked.Count - 10) + " more" : "")
                    + "\n\nUse 'Capture pristine data' in the Mod Manager first, or allow OpenCAGE to adopt the current bytes as the restore point.";
                return result;
            }
            _state.Save();

            //Journal the current bytes so any failure can put things back exactly
            string journalId = CreateSnapshot("transaction journal", affected);
            File.WriteAllText(ModToolkit.JournalFile(_gameRoot), journalId);

            try
            {
                //Everything back to baseline
                foreach (string path in affected)
                {
                    ModState.BaselineRecord record = _state.Baseline[path];
                    string fullPath = ModToolkit.Denormalise(_gameRoot, path);
                    if (record.Sha256Hex == null)
                    {
                        if (File.Exists(fullPath))
                            File.Delete(fullPath);
                    }
                    else
                    {
                        byte[] content = _store.Retrieve(record.Sha256Hex);
                        if (content == null)
                            throw new Exception("The baseline store no longer holds the bytes for " + path + ".");
                        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                        File.WriteAllBytes(fullPath, content);
                    }
                    _cache.Invalidate(path);
                }

                //Wipe applied records; they're rebuilt below
                foreach (ModState.InstalledMod mod in _state.Mods)
                {
                    mod.Applied.Clear();
                    mod.Enabled = false;
                }

                //Apply the desired mods in order
                Dictionary<string, string> lastWriter = new Dictionary<string, string>();
                for (int i = 0; i < desired.Count; i++)
                {
                    ModState.InstalledMod mod = desired[i];
                    mod.Enabled = true;
                    mod.Priority = i;
                    ApplyPackage(mod, packages[mod], lastWriter, result.Warnings);
                }

                _state.Save();
                _cache.Save();
                File.Delete(ModToolkit.JournalFile(_gameRoot));
                File.Delete(SnapshotPath(journalId));
                result.Success = true;
                return result;
            }
            catch (Exception e)
            {
                try
                {
                    RestoreSnapshot(journalId, true);
                    File.Delete(ModToolkit.JournalFile(_gameRoot));
                }
                catch (Exception restoreError)
                {
                    result.Error = "Applying mods failed (" + e.Message + "), and rolling back also failed: " + restoreError.Message
                        + "\nThe journalled bytes are still in the baseline store; restart OpenCAGE to recover.";
                    return result;
                }
                result.Error = e.Message;
                return result;
            }
        }

        private void ApplyPackage(ModState.InstalledMod mod, ModPackage package, Dictionary<string, string> lastWriter, List<string> warnings)
        {
            foreach (ModPackageEntry entry in package.Info.Entries)
            {
                string fullPath = ModToolkit.Denormalise(_gameRoot, entry.Target);
                byte[] payload = package.ReadPayload(entry);
                byte[] written;

                switch (entry.Kind)
                {
                    case ModPackageEntry.KindFile:
                        written = payload;
                        break;

                    case ModPackageEntry.KindDelta:
                        {
                            byte[] current = File.Exists(fullPath) ? File.ReadAllBytes(fullPath) : new byte[0];

                            /* The patch may have been made against this file plus another file's
                             * pristine bytes (a texture pak that absorbed global textures patches
                             * against vanilla-level + vanilla-global) - rebuild the same source */
                            if (entry.SourceAlsoTarget != null)
                            {
                                string alsoPath = ModToolkit.Normalise(entry.SourceAlsoTarget);
                                CaptureVanillaBaseline(alsoPath);
                                byte[] also = VanillaBytes(alsoPath);
                                if (also == null)
                                    throw new Exception("'" + mod.Name + "' patches " + entry.Target + " against the pristine bytes of "
                                        + alsoPath + ", but no pristine copy of that file is stored. Use 'Capture pristine data' in the Mod Manager first.");
                                current = ModExportBuilder.Concat(current, also);
                            }

                            try
                            {
                                written = DeltaCodec.Apply(payload, current);
                            }
                            catch (Exception e)
                            {
                                string writer;
                                if (lastWriter.TryGetValue(entry.Target, out writer))
                                    throw new Exception("'" + mod.Name + "' patches " + entry.Target + ", but '" + writer
                                        + "' has already changed that file. These two mods conflict - disable one, or reorder them.");
                                throw new Exception("'" + mod.Name + "' could not patch " + entry.Target + ": " + e.Message);
                            }
                            break;
                        }

                    case ModPackageEntry.KindBml:
                        {
                            if (!File.Exists(fullPath))
                                throw new Exception("'" + mod.Name + "' changes config values in " + entry.Target + ", which this install doesn't have.");
                            BML bml = new BML(File.ReadAllBytes(fullPath));
                            XmlDocument document = bml.Content;
                            List<BmlPatchOp> failedOps = BmlPatch.Apply(document, BmlPatch.Deserialise(System.Text.Encoding.UTF8.GetString(payload)));
                            foreach (BmlPatchOp failedOp in failedOps)
                                warnings.Add("'" + mod.Name + "': config change '" + failedOp.Claim + "' in " + entry.Target + " no longer applies and was skipped.");
                            bml.Content = document;

                            string temp = Path.GetTempFileName();
                            try
                            {
                                if (!bml.Save(temp))
                                    throw new Exception("'" + mod.Name + "': failed to rebuild " + entry.Target + " after applying config changes.");
                                written = File.ReadAllBytes(temp);
                            }
                            finally
                            {
                                File.Delete(temp);
                            }
                            break;
                        }

                    default:
                        throw new Exception("'" + mod.Name + "' contains an entry of unknown kind '" + entry.Kind + "'. Update OpenCAGE.");
                }

                //For whole files and deltas the recorded hash must match what we produced
                if (entry.TargetShaHex != null && entry.Kind != ModPackageEntry.KindBml)
                {
                    string sha = ModToolkit.ToHex(ModToolkit.Sha256(written));
                    if (sha != entry.TargetShaHex)
                        throw new Exception("'" + mod.Name + "': payload for " + entry.Target + " doesn't match its recorded hash. The package is corrupt.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllBytes(fullPath, written);
                _cache.Invalidate(entry.Target);

                mod.Applied[entry.Target] = ModToolkit.ToHex(ModToolkit.Sha256(written));
                lastWriter[entry.Target] = mod.Name;
            }
        }

        /// <summary>
        /// Do any enabled mods' files no longer hold the bytes we put there? True after a Steam
        /// verify wiped them, or after something else overwrote them.
        /// </summary>
        public List<string> PathsNeedingRepair()
        {
            //The expected final bytes per path come from the last enabled writer
            Dictionary<string, string> expected = new Dictionary<string, string>();
            foreach (ModState.InstalledMod mod in _state.ModsInPriorityOrder().Where(o => o.Enabled))
                foreach (KeyValuePair<string, string> applied in mod.Applied)
                    expected[applied.Key] = applied.Value;

            List<string> stale = new List<string>();
            foreach (KeyValuePair<string, string> path in expected)
            {
                byte[] hash = _cache.Hash(path.Key);
                if (hash == null || ModToolkit.ToHex(hash) != path.Value)
                    stale.Add(path.Key);
            }
            _cache.Save();
            return stale;
        }
        #endregion
    }
}
#endif

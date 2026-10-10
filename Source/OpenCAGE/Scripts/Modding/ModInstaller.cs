#if ENABLE_MOD_PACKAGES
using CATHODE;
using CathodeLib;
using Newtonsoft.Json;
using OpenCAGE.Modding.Merging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace OpenCAGE.Modding
{
    public class TransactionResult
    {
        public bool Success;
        public string Error;
        public List<string> Warnings = new List<string>();
        /// <summary>Where two mods wanted different things, and which one was used.</summary>
        public List<MergeConflict> Conflicts = new List<MergeConflict>();
        /// <summary>Levels that more than one mod changes, rebuilt from all of them together.</summary>
        public List<string> CombinedLevels = new List<string>();
        /// <summary>
        /// Set when nothing was done because files installed mods had changed have been changed again since: apply
        /// again saying whether to keep those changes (as a mod of their own) or throw them away.
        /// </summary>
        public List<string> EditedFiles = new List<string>();
        /// <summary>The mod the changes made since the last apply were kept as, if they were.</summary>
        public string KeptAsMod;
    }

    /// <summary>What applying a list of mods would do, worked out without touching the game files.</summary>
    public class ModAnalysis
    {
        /// <summary>Where mods - or a mod and the user's own changes - want different things, and which is used.</summary>
        public List<MergeConflict> Conflicts = new List<MergeConflict>();
        /// <summary>Levels that will be rebuilt from several versions (mods, or a mod and the user's own changes); their clashes are found then.</summary>
        public List<string> CombinedLevels = new List<string>();
        /// <summary>Mods that can't be read or applied, in words.</summary>
        public List<string> Problems = new List<string>();
        /// <summary>Files installed mods had changed that have been changed again since: applying needs telling what to do with them.</summary>
        public List<string> EditedFiles = new List<string>();
        /// <summary>Files with the user's own changes that mods will be combined with.</summary>
        public List<string> OwnChanges = new List<string>();
        /// <summary>Of <see cref="CombinedLevels"/>, those already combined from exactly these versions and still as they were left: applying leaves them be.</summary>
        public List<string> UnchangedLevels = new List<string>();
    }

    /// <summary>What to do with files installed mods had changed that have been changed again since they were applied.</summary>
    public enum EditedFilesChoice
    {
        /// <summary>Do nothing, and report them (<see cref="TransactionResult.EditedFiles"/>).</summary>
        Ask,
        /// <summary>Keep them exactly as they are now, as a new mod at the end of the list.</summary>
        KeepAsMod,
        /// <summary>Throw those changes away.</summary>
        Discard,
    }

    /// <summary>One mod's version of a level: every file of the level folder it changes, as it has them.</summary>
    public class LevelVersion
    {
        public string ModId;
        public string ModName;
        /* Normalised path -> the file as this mod has it (null: the mod deletes it) */
        public Dictionary<string, byte[]> Files = new Dictionary<string, byte[]>();
    }

    public class LevelMergeResult
    {
        public List<MergeConflict> Conflicts = new List<MergeConflict>();
        public List<string> Warnings = new List<string>();
    }

    /// <summary>
    /// Combines several mods' versions of one level into the level folder (which holds the level's baseline when
    /// this is called). Lives with the level tools; set by whoever builds the installer.
    /// </summary>
    public interface ILevelMerger
    {
        LevelMergeResult Merge(string gameRoot, string level, IList<LevelVersion> versions, Action<string> progress);
    }

    /* All installs, uninstalls, reorders and repairs are the same operation: put every affected file back to its
     * baseline bytes, then build each one from the enabled mods that change it, in list order. One mod's file is
     * used exactly as it ships; several mods' versions of the same file are merged by what the file is (see
     * FileMerger), and several mods' versions of one level are combined into one level (see ILevelMerger). The
     * files worked out from others - the level list, each level's behaviour tree list - are regenerated last.
     * Every write is journalled first, so a failure puts the install back exactly as it was. */
    public class ModInstaller
    {
        private readonly string _gameRoot;
        private readonly VanillaManifest _manifest;
        private readonly HashCache _cache;
        private readonly ModState _state;
        private readonly BaselineStore _store;

        /// <summary>How a level more than one mod changes is combined. Without one, the last mod's level is used and that's reported.</summary>
        public ILevelMerger LevelMerger;

        /* Captures run from editor save paths and a background thread while the manager UI can be
         * mid-transaction - one mutex covers every state-touching entry point */
        private readonly object _mutex = new object();

        public ModState State { get { return _state; } }
        public BaselineStore Store { get { return _store; } }
        public string GameRoot { get { return _gameRoot; } }

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
            /* Deltas are measured against one build's vanilla bytes and whole-file entries carry that
             * build's file outright, so a package from another build does not belong here. STEAM, EPIC
             * and GOG ship identical bytes for all but the localised UI.TXT files, but WINDOWS_STORE
             * and the console builds differ widely. UNKNOWN means we could not identify one end, and
             * there is nothing to compare. */
            if (!SamePlatformFamily(package.Info.HashSet, _manifest.Platform))
                throw new Exception("This mod was made for the " + PlatformName(package.Info.HashSet) + " version of the game, and this is the "
                    + PlatformName(_manifest.Platform) + " version. Its changes are measured against that version's files, so it can't be installed here.");

            string libraryFileName = package.Info.Id + ModToolkit.PackageExtension;
            string libraryPath = Path.Combine(ModToolkit.LibraryDir(_gameRoot), libraryFileName);
            Directory.CreateDirectory(ModToolkit.LibraryDir(_gameRoot));
            if (!string.Equals(Path.GetFullPath(packagePath), Path.GetFullPath(libraryPath), StringComparison.OrdinalIgnoreCase))
                File.Copy(packagePath, libraryPath, true);

            lock (_mutex)
            {
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
        }

        /* Steam, Epic and GOG ship the same bytes for everything but a handful of text files */
        private static bool SamePlatformFamily(PatchManager.Platform a, PatchManager.Platform b)
        {
            if (a == b || a == PatchManager.Platform.UNKNOWN || b == PatchManager.Platform.UNKNOWN) return true;
            bool pcA = a == PatchManager.Platform.STEAM || a == PatchManager.Platform.EPIC_GAMES_STORE || a == PatchManager.Platform.GOG;
            bool pcB = b == PatchManager.Platform.STEAM || b == PatchManager.Platform.EPIC_GAMES_STORE || b == PatchManager.Platform.GOG;
            return pcA && pcB;
        }

        public static string PlatformName(PatchManager.Platform platform)
        {
            switch (platform)
            {
                case PatchManager.Platform.STEAM: return "Steam";
                case PatchManager.Platform.EPIC_GAMES_STORE: return "Epic Games Store";
                case PatchManager.Platform.GOG: return "GOG";
                case PatchManager.Platform.WINDOWS_STORE: return "Microsoft Store";
                case PatchManager.Platform.SWITCH: return "Nintendo Switch";
                case PatchManager.Platform.IOS_ANDROID: return "mobile";
                case PatchManager.Platform.MAC_LINUX: return "Mac/Linux";
                default: return "unknown";
            }
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
            lock (_mutex)
            {
                ModState.InstalledMod mod = _state.FindMod(id);
                if (mod == null)
                    return;
                if (mod.Enabled || mod.Applied.Count != 0)
                    throw new Exception("'" + mod.Name + "' is currently installed - turn it off and apply before removing it.");
                string libraryPath = Path.Combine(ModToolkit.LibraryDir(_gameRoot), mod.PackageFileName ?? "");
                if (File.Exists(libraryPath))
                    File.Delete(libraryPath);
                _state.Mods.Remove(mod);
                _state.Save();
            }
        }
        #endregion

        #region PLAN
        private class Planned
        {
            public ModState.InstalledMod Mod;
            public ModPackage Package;
            public ModPackageEntry Entry;
        }

        private class Plan
        {
            public Dictionary<string, List<Planned>> Files = new Dictionary<string, List<Planned>>();
            /* Level name -> per mod (list order), that mod's entries in the level */
            public Dictionary<string, List<List<Planned>>> Levels = new Dictionary<string, List<List<Planned>>>();
            public bool TreesAffected;
            public bool NewLevels;
        }

        /// <summary>
        /// The level a file belongs to for combining purposes: the two Nostromo levels keep their scripts in a
        /// _PATCH folder beside them, which is the same level.
        /// </summary>
        public static string LevelUnitOf(string normalisedPath)
        {
            string level = ModToolkit.LevelOf(normalisedPath);
            if (level != null && level.EndsWith("_PATCH"))
                level = level.Substring(0, level.Length - "_PATCH".Length);
            return level;
        }

        private Plan BuildPlan(List<ModState.InstalledMod> mods, Dictionary<ModState.InstalledMod, ModPackage> packages)
        {
            Plan plan = new Plan();
            foreach (ModState.InstalledMod mod in mods)
            {
                ModPackage package = packages[mod];
                Dictionary<string, List<Planned>> byLevel = new Dictionary<string, List<Planned>>();
                foreach (ModPackageEntry entry in package.Info.Entries)
                {
                    string target = ModToolkit.Normalise(entry.Target);
                    if (ModToolkit.IsExcluded(target) || ModToolkit.IsRegenerated(target))
                        continue;
                    if (ModToolkit.AffectsBehaviourTrees(target))
                        plan.TreesAffected = true;

                    Planned planned = new Planned() { Mod = mod, Package = package, Entry = entry };
                    string level = LevelUnitOf(target);
                    if (level == null)
                    {
                        if (!plan.Files.TryGetValue(target, out List<Planned> list))
                            plan.Files[target] = list = new List<Planned>();
                        list.Add(planned);
                    }
                    else
                    {
                        if (!ShipsLevel(level))
                            plan.NewLevels = true;
                        if (!byLevel.TryGetValue(level, out List<Planned> list))
                            byLevel[level] = list = new List<Planned>();
                        list.Add(planned);
                    }
                }
                foreach (KeyValuePair<string, List<Planned>> level in byLevel)
                {
                    if (!plan.Levels.TryGetValue(level.Key, out List<List<Planned>> perMod))
                        plan.Levels[level.Key] = perMod = new List<List<Planned>>();
                    perMod.Add(level.Value);
                }
            }
            return plan;
        }

        /* Every file in a level's folder(s) on disk now, plus every one the baseline knows - a combined level is
           rebuilt whole, so all of it is in play */
        private IEnumerable<string> LevelFiles(string level)
        {
            HashSet<string> files = new HashSet<string>();
            foreach (string folder in new[] { level, level + "_PATCH" })
            {
                string dir = ModToolkit.Denormalise(_gameRoot, ModToolkit.LevelFolder(folder));
                if (Directory.Exists(dir))
                    foreach (string file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                    {
                        string normalised = ModToolkit.NormaliseFull(_gameRoot, file);
                        if (normalised != null && !ModToolkit.IsExcluded(normalised))
                            files.Add(normalised);
                    }
                string prefix = ModToolkit.LevelFolder(folder);
                foreach (string known in _state.Baseline.Keys)
                    if (known.StartsWith(prefix))
                        files.Add(known);
            }
            return files;
        }

        /* Each level's behaviour tree list, which the trees and character configs decide - the lists there are, and the one
           a level a mod adds needs (packages never carry one) */
        private IEnumerable<string> TreeListFiles()
        {
            HashSet<string> lists = new HashSet<string>();
            string env = Path.Combine(_gameRoot, "DATA", "ENV", "PRODUCTION");
            if (!Directory.Exists(env))
                return lists;
            foreach (string file in Directory.GetFiles(env, "BEHAVIOR_TREE.DB", SearchOption.AllDirectories))
            {
                string normalised = ModToolkit.NormaliseFull(_gameRoot, file);
                if (normalised != null && !ModToolkit.IsExcluded(normalised))
                    lists.Add(normalised);
            }
            foreach (string level in Level.GetLevels(_gameRoot))
                lists.Add(ModToolkit.Normalise("DATA/ENV/" + level + "/WORLD/BEHAVIOR_TREE.DB"));
            return lists;
        }
        #endregion

        #region ANALYSIS
        /// <summary>
        /// What would clash if these mods were applied together, in this order - worked out without touching the
        /// game files. Levels more than one mod changes are listed in <paramref name="combinedLevels"/>: they're
        /// combined at apply, and what clashes inside them is reported then.
        /// </summary>
        public ModAnalysis Analyze(List<string> enabledIdsInOrder)
        {
            ModAnalysis analysis = new ModAnalysis();
            lock (_mutex)
            {
                List<ModState.InstalledMod> mods = enabledIdsInOrder.Select(o => _state.FindMod(o)).Where(o => o != null).ToList();
                Dictionary<ModState.InstalledMod, ModPackage> packages = new Dictionary<ModState.InstalledMod, ModPackage>();
                foreach (ModState.InstalledMod mod in mods)
                {
                    try { packages[mod] = OpenPackage(mod); }
                    catch (Exception e) { analysis.Problems.Add("'" + mod.Name + "' can't be read: " + e.Message); }
                }
                mods = mods.Where(packages.ContainsKey).ToList();
                Plan plan = BuildPlan(mods, packages);
                analysis.EditedFiles = EditedSinceApplied();

                //The user's own versions, as they'll be when applying starts: what's recorded, updated by what's on disk now
                Dictionary<string, string> own = new Dictionary<string, string>(_state.Own);
                HashSet<string> touched = new HashSet<string>(plan.Files.Keys);
                foreach (string level in plan.Levels.Keys)
                    touched.UnionWith(LevelFiles(level));
                foreach (KeyValuePair<string, string> found in OwnChangesOnDisk(touched))
                {
                    if (found.Value == _state.Baseline[found.Key].Sha256Hex) own.Remove(found.Key);
                    else own[found.Key] = found.Value;
                }
                byte[] OwnBytes(string path)
                {
                    string sha = own[path];
                    if (sha == null) return null;
                    if (_state.Own.TryGetValue(path, out string kept) && kept == sha) return _store.Retrieve(sha);
                    return File.ReadAllBytes(ModToolkit.Denormalise(_gameRoot, path));
                }

                foreach (KeyValuePair<string, List<Planned>> file in plan.Files)
                {
                    bool hasOwn = own.ContainsKey(file.Key);
                    if (hasOwn) analysis.OwnChanges.Add(file.Key);
                    if (file.Value.Count + (hasOwn ? 1 : 0) < 2) continue;
                    try
                    {
                        List<(string Name, byte[] Bytes)> sources = new List<(string, byte[])>();
                        if (hasOwn) sources.Add((MergeConflict.OwnChanges, OwnBytes(file.Key)));
                        foreach (Planned planned in file.Value)
                            sources.Add((planned.Mod.Name, Resolve(planned)));
                        int lastDelete = sources.FindLastIndex(o => o.Bytes == null);
                        List<Contribution> versions = sources.Skip(lastDelete + 1).Select(o => new Contribution() { ModName = o.Name, Bytes = o.Bytes }).ToList();
                        if (lastDelete >= 0 && sources.Count > 1)
                            analysis.Conflicts.Add(new MergeConflict()
                            {
                                Target = file.Key, Where = "the whole file", Kind = ConflictKind.RemovedVsChanged,
                                Kept = versions.Count != 0 ? versions.Last().ModName : sources[lastDelete].Name,
                                Lost = versions.Count != 0 ? sources[lastDelete].Name : sources.First(o => o.Bytes != null).Name,
                                Detail = "one deletes it and another changes it",
                            });
                        if (versions.Count > 1)
                            analysis.Conflicts.AddRange(FileMerger.Merge(file.Key, BaseBytes(file.Key), versions).Conflicts);
                    }
                    catch (Exception e) { analysis.Problems.Add(e.Message); }
                }
                foreach (KeyValuePair<string, List<List<Planned>>> level in plan.Levels)
                {
                    if (!ShipsLevel(level.Key))
                    {
                        //A level a mod adds: the last mod adding it is used whole
                        for (int i = 0; i < level.Value.Count - 1; i++)
                            analysis.Conflicts.Add(new MergeConflict() { Target = level.Key, Where = "the whole level", Kind = ConflictKind.WholeFile, Kept = level.Value.Last()[0].Mod.Name, Lost = level.Value[i][0].Mod.Name, Detail = "both add a level of that name, which can't be combined" });
                        continue;
                    }
                    List<string> ownFiles = LevelFiles(level.Key).Where(own.ContainsKey).ToList();
                    analysis.OwnChanges.AddRange(ownFiles);
                    if (level.Value.Count > 1 || ownFiles.Count != 0)
                    {
                        analysis.CombinedLevels.Add(level.Key);
                        if (_state.LevelSignatures.TryGetValue(level.Key, out string last) && last == LevelSignature(level.Key, level.Value, ownFiles.Count != 0, false)
                            && _state.Composition.Where(o => LevelUnitOf(o.Key) == level.Key && !IsDerived(o.Key)).All(o => OnDiskAsComposed(o.Key, o.Value)))
                            analysis.UnchangedLevels.Add(level.Key);
                    }
                    string missing = ownFiles.FirstOrDefault(o => !VanillaKnown(o)) ?? AdoptedLevelFiles(level.Key).FirstOrDefault()
                        ?? LevelFiles(level.Key).FirstOrDefault(o => !_state.Baseline.ContainsKey(o) && !_state.Composition.ContainsKey(o) && IsChangedShippedFile(o));
                    if (missing != null)
                        analysis.Problems.Add(CantCombineOwnLevel(level.Key, level.Value.Select(o => o[0].Mod.Name).ToList()));
                }
            }
            return analysis;
        }
        #endregion

        #region SIGNATURES
        /* Bumped whenever how files or levels are combined changes, so what an older OpenCAGE made is made again (files: 2,
           animation tree layouts follow the version of each tree that's installed) */
        private const int CombineVersion = 1;
        private const int FileCombineVersion = 2;

        /* A mod's version of one file, as an identity: the exact bytes it produces, or its deletion */
        private static string EntryIdentity(Planned planned)
        {
            ModPackageEntry entry = planned.Entry;
            if (entry.Kind == ModPackageEntry.KindDelete)
                return "delete";
            return entry.Kind + ":" + (entry.TargetShaHex ?? planned.Package.Info.CreatedUtc.Ticks + "/" + entry.Payload);
        }

        private static string Hash(System.Text.StringBuilder text)
        {
            return ModToolkit.ToHex(ModToolkit.Sha256(System.Text.Encoding.UTF8.GetBytes(text.ToString())));
        }

        /* What a file outside the levels is made from: its original, the user's own version, and each mod's, in order */
        private string FileSignature(string target, List<Planned> contributors)
        {
            //A file one mod changes alone goes in exactly as it ships, whatever version combines files: it stays as it was made
            bool combined = _state.Own.ContainsKey(target) || contributors.Count > 1;
            System.Text.StringBuilder text = new System.Text.StringBuilder("file|" + (combined ? FileCombineVersion : CombineVersion) + "|" + target + "|");
            text.Append(_state.Own.TryGetValue(target, out string own) ? "own=" + (own ?? "none") : "-").Append('|');
            text.Append(_state.Baseline.TryGetValue(target, out ModState.BaselineRecord baseline) ? baseline.Sha256Hex ?? "none" : "?").Append('|');
            foreach (Planned planned in contributors)
                text.Append(planned.Mod.Id).Append('=').Append(EntryIdentity(planned)).Append(';');
            return Hash(text);
        }

        /* What a level is made from: the user's own files of it (when they're combined), and each mod's files, in order */
        private string LevelSignature(string level, List<List<Planned>> perMod, bool withOwn, bool whole)
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder("level|" + CombineVersion + "|" + level + "|" + (whole ? "whole" : "") + "|");
            if (withOwn)
                foreach (string path in LevelFiles(level).Where(_state.Own.ContainsKey).OrderBy(o => o))
                    text.Append(path).Append('=').Append(_state.Own[path] ?? "none").Append(';');
            foreach (List<Planned> entries in perMod)
            {
                text.Append('|').Append(entries[0].Mod.Id).Append(':');
                foreach (Planned planned in entries.OrderBy(o => o.Entry.Target))
                    text.Append(ModToolkit.Normalise(planned.Entry.Target)).Append('=').Append(EntryIdentity(planned)).Append(',');
            }
            return Hash(text);
        }

        /* A file the last apply wrote still holds exactly what it wrote */
        private bool OnDiskAsComposed(string path, ModState.ComposedFile composed)
        {
            byte[] hash = _cache.Hash(path);
            return (hash == null ? null : ModToolkit.ToHex(hash)) == composed.Sha256Hex;
        }

        /* Worked out again after every apply from everything else (each level's behaviour tree list, the level list) */
        private static bool IsDerived(string path)
        {
            return path == ModToolkit.LevelListPath || path.EndsWith("/WORLD/BEHAVIOR_TREE.DB");
        }
        #endregion

        #region OWN CHANGES
        /* What a file holds with no mods applied: the user's own version of it if they have one, else its baseline */
        private string UnmoddedSha(string path)
        {
            if (_state.Own.TryGetValue(path, out string own))
                return own;
            return _state.Baseline.TryGetValue(path, out ModState.BaselineRecord record) ? record.Sha256Hex : null;
        }

        /* A shipped file that isn't on disk as it shipped */
        private bool IsChangedShippedFile(string path)
        {
            if (ModToolkit.IsRegenerated(path) || !_manifest.Contains(path))
                return false;
            byte[] hash = _cache.Hash(path);
            return hash == null || !_manifest.IsVanilla(path, hash);
        }

        /* Files of a level whose restore point is the user's own bytes, kept because OpenCAGE had no original of them */
        private IEnumerable<string> AdoptedLevelFiles(string level)
        {
            return LevelFiles(level).Where(o => !ModToolkit.IsRegenerated(o) && _state.Baseline.TryGetValue(o, out ModState.BaselineRecord record) && !record.IsVanilla && record.Sha256Hex != null && _manifest.Contains(o));
        }

        /* Whether the game ships a level of this name (a mod can add one) */
        private bool ShipsLevel(string level)
        {
            string folder = ModToolkit.LevelFolder(level);
            return _manifest.Entries.Any(o => o.Path.StartsWith(folder));
        }

        /// <summary>Whether OpenCAGE holds the original of a game file - so the user's changes to it can be combined with mods.</summary>
        public bool HasOriginal(string normalisedPath)
        {
            lock (_mutex)
                return VanillaKnown(normalisedPath);
        }

        /* Whether the shipped version of a file is known - stored, or known not to exist */
        private bool VanillaKnown(string path)
        {
            return _state.Baseline.TryGetValue(path, out ModState.BaselineRecord record) && record.IsVanilla && (record.Sha256Hex == null || _store.Has(record.Sha256Hex));
        }

        private static string CantCombineOwnLevel(string level, List<string> mods)
        {
            return level + " has changes of your own, and OpenCAGE doesn't have all of that level's original files to combine them with "
                + string.Join(", ", mods.Select(o => "'" + o + "'")) + ". Use 'Keep those changes safe...' at the top of the Mod Manager first.";
        }

        /// <summary>
        /// Files the last apply wrote that have been changed since - edited in OpenCAGE, say - and so hold work that
        /// applying again would replace. A file a store's file check put back to its original isn't one, and neither is
        /// a deleted one: neither holds anything to lose.
        /// </summary>
        public List<string> EditedSinceApplied()
        {
            List<string> edited = new List<string>();
            lock (_mutex)
            {
                foreach (KeyValuePair<string, ModState.ComposedFile> composed in _state.Composition)
                {
                    if (ModToolkit.IsRegenerated(composed.Key))
                        continue;
                    byte[] hash = _cache.Hash(composed.Key);
                    string sha = hash == null ? null : ModToolkit.ToHex(hash);
                    if (sha == null || sha == composed.Value.Sha256Hex || sha == UnmoddedSha(composed.Key) || _manifest.IsVanilla(composed.Key, hash))
                        continue;
                    edited.Add(composed.Key);
                }
            }
            return edited;
        }

        /* Files no mod has changed whose bytes on disk aren't the user's version as last recorded: path -> what they
           hold now (null: deleted). Files never seen before aren't included - their bytes now become their baseline. */
        private Dictionary<string, string> OwnChangesOnDisk(IEnumerable<string> paths)
        {
            Dictionary<string, string> changed = new Dictionary<string, string>();
            foreach (string path in paths)
            {
                if (ModToolkit.IsRegenerated(path) || _state.Composition.ContainsKey(path) || !_state.Baseline.ContainsKey(path))
                    continue;
                byte[] hash = _cache.Hash(path);
                string sha = hash == null ? null : ModToolkit.ToHex(hash);
                if (sha != UnmoddedSha(path))
                    changed[path] = sha;
            }
            return changed;
        }

        /* Keep the user's versions found on disk: mods are combined onto them, and removing every mod puts them back */
        private void RecordOwn(Dictionary<string, string> found)
        {
            foreach (KeyValuePair<string, string> own in found)
            {
                if (own.Value == _state.Baseline[own.Key].Sha256Hex)
                    _state.Own.Remove(own.Key);
                else
                    _state.Own[own.Key] = own.Value == null ? null : _store.StoreFile(ModToolkit.Denormalise(_gameRoot, own.Key));
            }
        }

        /* The user's own version of a file: (true, bytes) - bytes null when their version is "no file" - or (false, null) */
        private (bool Has, byte[] Bytes) OwnVersion(string path)
        {
            if (!_state.Own.TryGetValue(path, out string sha))
                return (false, null);
            if (sha == null)
                return (true, null);
            byte[] bytes = _store.Retrieve(sha);
            if (bytes == null)
                throw new Exception("OpenCAGE's copy of your own version of " + path + " is missing, so it can't be combined with mods.");
            return (true, bytes);
        }

        /// <summary>
        /// The user's own version of a file that's combined with mods on disk - what to export as their work, rather than
        /// the combined file. False when they have none apart from what's on disk. Bytes null: their version deletes it.
        /// </summary>
        public bool TryGetOwnVersion(string normalisedPath, out byte[] bytes)
        {
            bytes = null;
            lock (_mutex)
            {
                if (!_state.Own.TryGetValue(normalisedPath, out string sha) || !_state.Composition.ContainsKey(normalisedPath))
                    return false;
                if (sha == null)
                    return true;
                bytes = _store.Retrieve(sha);
                return bytes != null;
            }
        }

        /// <summary>Files whose user's own version is kept apart from the combined file on disk (see <see cref="TryGetOwnVersion"/>).</summary>
        public List<string> OwnVersionsUnderMods()
        {
            lock (_mutex)
                return _state.Own.Keys.Where(_state.Composition.ContainsKey).ToList();
        }

        /* Changed files of installed mods kept as a mod of their own, holding them exactly as they are now - a level's
           whole folder for a level file, as a level only makes sense whole */
        private ModState.InstalledMod KeepEditsAsMod(List<string> edited)
        {
            DateTime now = DateTime.Now;
            //What's on disk now, not the user's own version kept from before the mods went in: the edits are only on disk
            ModExportBuilder builder = new ModExportBuilder(_gameRoot, _manifest, _cache, this) { AsOnDisk = true };
            builder.Info.Id = "my-changes-" + now.ToString("yyyyMMdd-HHmmss");
            builder.Info.Name = "My changes (" + now.ToString("d MMM yyyy, HH:mm") + ")";
            builder.Info.Author = "You";
            builder.Info.Version = "1.0";
            builder.Info.Description = "Changes you made to files that installed mods had changed, kept as a mod of their own when the mods were next applied. "
                + "It holds those files as they were then - including what the mods had changed in them - so it can stay installed after those mods are turned off.";
            foreach (string level in edited.Select(LevelUnitOf).Where(o => o != null).Distinct().ToList())
                builder.AddFiles(LevelFiles(level));
            foreach (string path in edited.Where(o => LevelUnitOf(o) == null))
                builder.AddFile(path);
            Directory.CreateDirectory(ModToolkit.LibraryDir(_gameRoot));
            string package = Path.Combine(ModToolkit.LibraryDir(_gameRoot), builder.Info.Id + ModToolkit.PackageExtension);
            builder.Write(package);
            return ImportPackage(package);
        }

        /* The store keeps only what something still needs: baselines, the user's own versions, and saved snapshots */
        private void PruneStore()
        {
            try
            {
                HashSet<string> keep = new HashSet<string>(_state.Baseline.Values.Select(o => o.Sha256Hex).Where(o => o != null));
                keep.UnionWith(_state.Own.Values.Where(o => o != null));
                string snapshots = ModToolkit.SnapshotsDir(_gameRoot);
                if (Directory.Exists(snapshots))
                    foreach (string file in Directory.GetFiles(snapshots, "*.json"))
                    {
                        Snapshot snapshot = JsonConvert.DeserializeObject<Snapshot>(File.ReadAllText(file));
                        keep.UnionWith(snapshot.Files.Values.Where(o => o != null));
                    }
                _store.Prune(keep);
            }
            catch { }
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
            /* PATH -> where the file was moved to keep it (a transaction journal's files the store doesn't hold: moving one
               aside on the same drive is instant, where storing it means reading and compressing it) */
            [JsonProperty("moved")] public Dictionary<string, string> Moved = new Dictionary<string, string>();
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
                if (!File.Exists(fullPath))
                {
                    snapshot.Files[path] = null;
                    continue;
                }
                //A file whose bytes the store already has (its baseline, say) needn't be read and stored again
                byte[] hash = _cache.Hash(path);
                string sha = hash == null ? null : ModToolkit.ToHex(hash);
                snapshot.Files[path] = sha != null && _store.Has(sha) ? sha : _store.Store(File.ReadAllBytes(fullPath));
            }
            Directory.CreateDirectory(ModToolkit.SnapshotsDir(_gameRoot));
            File.WriteAllText(SnapshotPath(snapshot.Id), JsonConvert.SerializeObject(snapshot, Newtonsoft.Json.Formatting.Indented));
            return snapshot.Id;
        }

        /// <summary>
        /// A transaction's journal: like a snapshot, but a file whose bytes the store doesn't already hold is moved aside
        /// (instant on one drive) rather than stored. The journal is written before anything moves, so whatever point a
        /// failure or a crash comes at, every file is either still in place or in the journal folder.
        /// </summary>
        private string CreateJournal(IEnumerable<string> normalisedPaths)
        {
            Snapshot snapshot = new Snapshot()
            {
                Id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                Name = "transaction journal",
                DateUtc = DateTime.UtcNow,
            };
            string folder = JournalFolder(snapshot.Id);
            foreach (string path in normalisedPaths.Distinct())
            {
                string fullPath = ModToolkit.Denormalise(_gameRoot, path);
                if (!File.Exists(fullPath))
                {
                    snapshot.Files[path] = null;
                    continue;
                }
                byte[] hash = _cache.Hash(path);
                string sha = hash == null ? null : ModToolkit.ToHex(hash);
                if (sha != null && _store.Has(sha))
                    snapshot.Files[path] = sha;
                else
                    snapshot.Moved[path] = Path.Combine(folder, path.Replace('/', Path.DirectorySeparatorChar));
            }
            Directory.CreateDirectory(ModToolkit.SnapshotsDir(_gameRoot));
            File.WriteAllText(SnapshotPath(snapshot.Id), JsonConvert.SerializeObject(snapshot, Newtonsoft.Json.Formatting.Indented));
            File.WriteAllText(ModToolkit.JournalFile(_gameRoot), snapshot.Id);
            try
            {
                foreach (KeyValuePair<string, string> moved in snapshot.Moved)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(moved.Value));
                    File.Move(ModToolkit.Denormalise(_gameRoot, moved.Key), moved.Value);
                    _cache.Invalidate(moved.Key);
                }
            }
            catch
            {
                //A file that couldn't move (in use, say): everything that did goes straight back before saying so
                RestoreSnapshot(snapshot.Id, true);
                File.Delete(ModToolkit.JournalFile(_gameRoot));
                throw;
            }
            return snapshot.Id;
        }

        private string JournalFolder(string id)
        {
            return Path.Combine(ModToolkit.SnapshotsDir(_gameRoot), id);
        }

        public void RestoreSnapshot(string id, bool delete = false)
        {
            Snapshot snapshot = JsonConvert.DeserializeObject<Snapshot>(File.ReadAllText(SnapshotPath(id)));
            foreach (KeyValuePair<string, string> moved in snapshot.Moved ?? new Dictionary<string, string>())
            {
                //Not there: it never moved (the failure came first), and the file is where it was
                if (!File.Exists(moved.Value))
                    continue;
                string fullPath = ModToolkit.Denormalise(_gameRoot, moved.Key);
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.Move(moved.Value, fullPath);
                _cache.Invalidate(moved.Key);
            }
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
                DeleteSnapshot(id);
        }

        private void DeleteSnapshot(string id)
        {
            File.Delete(SnapshotPath(id));
            if (Directory.Exists(JournalFolder(id)))
                Directory.Delete(JournalFolder(id), true);
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

        /* What a file holds with no mods applied: its baseline (vanilla, or the user's own bytes adopted as the
           restore point), or null when it doesn't exist then */
        private byte[] BaselineBytes(string normalisedPath)
        {
            if (!_state.Baseline.TryGetValue(normalisedPath, out ModState.BaselineRecord record) || record.Sha256Hex == null)
                return null;
            return _store.Retrieve(record.Sha256Hex);
        }

        /* The original mods are merged against: vanilla when known, else the baseline */
        private byte[] BaseBytes(string normalisedPath)
        {
            return VanillaBytes(normalisedPath) ?? BaselineBytes(normalisedPath);
        }

        /* Files OpenCAGE itself keeps up to date - the level list - don't have to be vanilla to be put back */
        private static bool AdoptsAutomatically(string normalisedPath)
        {
            return normalisedPath == ModToolkit.LevelListPath || normalisedPath.EndsWith("/WORLD/BEHAVIOR_TREE.DB");
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
                else if (adoptUnknown || !_manifest.Contains(path) || AdoptsAutomatically(path))
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
        /// <param name="edited">What to do with files installed mods had changed that have been changed again since
        /// they were applied. Left at Ask, nothing is done while there are any: they're listed in the result.</param>
        public TransactionResult ApplyConfiguration(List<string> enabledIdsInOrder, bool adoptUnknownBaselines = false, Action<string> progress = null, EditedFilesChoice edited = EditedFilesChoice.Ask)
        {
            lock (_mutex)
            {
                _sources = new Dictionary<string, byte[]>();
                try
                {
                    return ApplyConfigurationLocked(enabledIdsInOrder, adoptUnknownBaselines, progress ?? (s => { }), edited);
                }
                catch (Exception e)
                {
                    //Only reachable before any game file is written - once they are, failures put everything back themselves
                    return new TransactionResult() { Error = Explain(e) };
                }
                finally
                {
                    _sources = null;
                }
            }
        }

        /* A failure in words a player can act on */
        private static string Explain(Exception e)
        {
            if (e is IOException && e.Message.Contains("being used by another process"))
                return "Something else is using the game's files. Close Alien: Isolation (and anything else that might have its files open), then try again.\n\n" + e.Message;
            if (e is UnauthorizedAccessException)
                return "OpenCAGE isn't allowed to change the game's files. Check the game folder isn't read-only, or run OpenCAGE as an administrator if the game is installed somewhere protected.\n\n" + e.Message;
            return e.Message;
        }

        private TransactionResult ApplyConfigurationLocked(List<string> enabledIdsInOrder, bool adoptUnknownBaselines, Action<string> progress, EditedFilesChoice edited)
        {
            TransactionResult result = new TransactionResult();

            List<ModState.InstalledMod> desired = new List<ModState.InstalledMod>();
            foreach (string id in enabledIdsInOrder)
            {
                ModState.InstalledMod mod = _state.FindMod(id);
                if (mod == null)
                {
                    result.Error = "No mod with id " + id + " in the library.";
                    return result;
                }
                if (!desired.Contains(mod))
                    desired.Add(mod);
            }

            //Work done on installed mods' files since they went in is never replaced without being asked
            progress("Checking for changes made since mods were installed...");
            List<string> editedFiles = EditedSinceApplied();
            if (editedFiles.Count != 0)
            {
                if (edited == EditedFilesChoice.Ask)
                {
                    result.EditedFiles = editedFiles;
                    result.Error = editedFiles.Count + " file" + (editedFiles.Count == 1 ? " that mods changed has" : "s that mods changed have")
                        + " been changed again since the mods were installed. Choose whether to keep those changes (as a mod of their own) or throw them away.";
                    return result;
                }
                if (edited == EditedFilesChoice.KeepAsMod)
                {
                    progress("Keeping your changes as a mod...");
                    try
                    {
                        ModState.InstalledMod kept = KeepEditsAsMod(editedFiles);
                        desired.Remove(kept);
                        desired.Add(kept);
                        result.KeptAsMod = kept.Name;
                    }
                    catch (Exception e)
                    {
                        result.Error = "Your changes couldn't be kept as a mod, so nothing was changed: " + e.Message;
                        return result;
                    }
                }
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

            progress("Working out what changes...");
            Plan plan = BuildPlan(desired, packages);

            //Everything the last apply wrote, plus everything this one will
            HashSet<string> affected = new HashSet<string>(_state.Composition.Keys);
            foreach (ModState.InstalledMod mod in _state.Mods)
                foreach (string path in mod.Applied.Keys)
                    affected.Add(path);
            foreach (string path in plan.Files.Keys)
                affected.Add(path);
            foreach (KeyValuePair<string, List<List<Planned>>> level in plan.Levels)
                foreach (List<Planned> perMod in level.Value)
                    foreach (Planned planned in perMod)
                        affected.Add(ModToolkit.Normalise(planned.Entry.Target));

            /* A level only ever goes in whole and consistent. One mod's files are laid over the level as it is with no mods,
             * so the rest of it must be just that - files changed without OpenCAGE keeping the originals can't be mixed with
             * a mod's. Several versions of a level (mods, or a mod and the user's own work) are rebuilt from all of them
             * together, from the level's originals. A level the game doesn't ship has nothing to combine from: the last
             * mod adding it is used whole. */
            progress("Checking the levels mods change...");
            HashSet<string> ownLevels = new HashSet<string>(), wholeLevels = new HashSet<string>();
            foreach (KeyValuePair<string, List<List<Planned>>> level in plan.Levels)
            {
                List<string> levelFiles = LevelFiles(level.Key).ToList();
                if (!ShipsLevel(level.Key))
                {
                    wholeLevels.Add(level.Key);
                    affected.UnionWith(levelFiles);
                    continue;
                }
                RecordOwn(OwnChangesOnDisk(levelFiles));
                bool unknown = AdoptedLevelFiles(level.Key).Any() || levelFiles.Any(o => !_state.Baseline.ContainsKey(o) && !_state.Composition.ContainsKey(o) && IsChangedShippedFile(o));
                List<string> ownFiles = levelFiles.Where(_state.Own.ContainsKey).ToList();
                if (unknown || ownFiles.Any(o => !VanillaKnown(o)))
                {
                    _state.Save();
                    result.Error = CantCombineOwnLevel(level.Key, level.Value.Select(o => o[0].Mod.Name).ToList());
                    return result;
                }
                if (ownFiles.Count != 0)
                    ownLevels.Add(level.Key);
                if (ownFiles.Count != 0 || level.Value.Count > 1)
                    affected.UnionWith(levelFiles);
            }

            bool regenerateTrees = plan.TreesAffected || plan.Levels.Count != 0 || _state.Composition.Keys.Any(o => o.EndsWith("/WORLD/BEHAVIOR_TREE.DB"));
            if (regenerateTrees)
                foreach (string path in TreeListFiles())
                    affected.Add(path);
            //The level list is put back with the rest, and only written again when a mod adds a level - writing it reformats it
            bool regenerateLevelList = plan.NewLevels;
            if (regenerateLevelList || _state.Composition.ContainsKey(ModToolkit.LevelListPath))
                affected.Add(ModToolkit.LevelListPath);

            //Baselines for everything we're about to touch
            progress("Keeping copies of the original files...");
            List<string> blocked = PrepareBaselines(affected, adoptUnknownBaselines);
            if (blocked.Count != 0)
            {
                _state.Save();
                result.Error = "These files have been changed, and OpenCAGE has no copy of the originals, so they couldn't be put back later:\n  "
                    + string.Join("\n  ", blocked.Take(10).ToArray())
                    + (blocked.Count > 10 ? "\n  ...and " + (blocked.Count - 10) + " more" : "")
                    + "\n\nUse 'Keep those changes safe...' at the top of the Mod Manager first, or let OpenCAGE treat the files as they are now as the originals.";
                return result;
            }

            //The user's own work on files no mod had changed: mods are combined onto it, and removing them puts it back
            progress("Keeping copies of your own changes...");
            RecordOwn(OwnChangesOnDisk(affected));
            _state.Save();

            //Whatever the last apply made from exactly the same mods and files, and is still on disk just as it was written,
            //is left alone: turning one mod on or off only remakes what that mod touches
            HashSet<string> keptFiles = new HashSet<string>(), keptLevels = new HashSet<string>();
            Dictionary<string, string> fileSignatures = new Dictionary<string, string>(), levelSignatures = new Dictionary<string, string>();
            foreach (KeyValuePair<string, List<Planned>> file in plan.Files)
            {
                string signature = fileSignatures[file.Key] = FileSignature(file.Key, file.Value);
                if (_state.Composition.TryGetValue(file.Key, out ModState.ComposedFile composed) && composed.Signature == signature && OnDiskAsComposed(file.Key, composed))
                    keptFiles.Add(file.Key);
            }
            foreach (KeyValuePair<string, List<List<Planned>>> level in plan.Levels)
            {
                string signature = levelSignatures[level.Key] = LevelSignature(level.Key, level.Value, ownLevels.Contains(level.Key), wholeLevels.Contains(level.Key));
                if (_state.LevelSignatures.TryGetValue(level.Key, out string last) && last == signature
                    && _state.Composition.Where(o => LevelUnitOf(o.Key) == level.Key && !IsDerived(o.Key)).All(o => OnDiskAsComposed(o.Key, o.Value)))
                    keptLevels.Add(level.Key);
            }
            affected.RemoveWhere(o => !IsDerived(o) && (keptFiles.Contains(o) || keptLevels.Contains(LevelUnitOf(o) ?? "")));

            //Journal the current bytes so any failure can put things back exactly
            progress("Making a restore point...");
            string journalId = CreateJournal(affected);

            try
            {
                //Files about to be written whole anyway (files outside levels, a single mod's level files) needn't be put back
                //first; a level being combined or replaced is, as combining starts from it
                progress("Putting the original files back...");
                HashSet<string> rewritten = new HashSet<string>(plan.Files.Keys);
                foreach (KeyValuePair<string, List<List<Planned>>> level in plan.Levels)
                    if (level.Value.Count == 1 && !ownLevels.Contains(level.Key) && !wholeLevels.Contains(level.Key))
                        foreach (Planned planned in level.Value[0])
                            rewritten.Add(ModToolkit.Normalise(planned.Entry.Target));
                foreach (string path in affected)
                {
                    if (rewritten.Contains(path))
                        continue;
                    string unit = LevelUnitOf(path) ?? "";
                    RestoreUnmodded(path, ownLevels.Contains(unit) || wholeLevels.Contains(unit));
                }

                foreach (ModState.InstalledMod mod in _state.Mods)
                {
                    mod.Applied.Clear();
                    mod.Enabled = false;
                }
                foreach (ModState.InstalledMod mod in desired)
                    mod.Enabled = true;
                //The list keeps the order the user gave it: the enabled mods take the places enabled mods have in it, in the
                //order asked for, and the rest stay where they are
                List<ModState.InstalledMod> order = _state.ModsInPriorityOrder();
                List<int> slots = Enumerable.Range(0, order.Count).Where(i => desired.Contains(order[i])).ToList();
                for (int i = 0; i < slots.Count; i++)
                    order[slots[i]] = desired[i];
                for (int i = 0; i < order.Count; i++)
                    order[i].Priority = i;
                foreach (string path in _state.Composition.Keys.ToList())
                    if (IsDerived(path) || !(keptFiles.Contains(path) || keptLevels.Contains(LevelUnitOf(path) ?? "")))
                        _state.Composition.Remove(path);
                foreach (string level in _state.LevelSignatures.Keys.ToList())
                    if (!keptLevels.Contains(level))
                        _state.LevelSignatures.Remove(level);
                //What was left alone keeps its clashes, to report again - a clash inside a PAK ("<file> › <entry>") or in one of
                //a level's files belongs to that file or level
                bool LeftAlone(string target)
                {
                    int inside = (target ?? "").IndexOf(" › ", StringComparison.Ordinal);
                    string file = inside > 0 ? target.Substring(0, inside) : target ?? "";
                    return keptFiles.Contains(file) || keptLevels.Contains(file) || keptLevels.Contains(LevelUnitOf(file) ?? "");
                }
                List<ModState.RecordedConflict> carried = _state.Conflicts.Where(o => LeftAlone(o.Target)).ToList();
                _state.Conflicts.Clear();
                foreach (ModState.RecordedConflict conflict in carried)
                    result.Conflicts.Add(new MergeConflict() { Target = conflict.Target, Where = conflict.Where, Kind = conflict.Kind, Kept = conflict.Kept, Lost = conflict.Lost, Detail = conflict.Detail });

                //Files outside levels: each one built from every mod that changes it
                int done = 0;
                foreach (KeyValuePair<string, List<Planned>> file in plan.Files.OrderBy(o => o.Key))
                {
                    ++done;
                    if (keptFiles.Contains(file.Key))
                        continue;
                    progress("Installing " + Path.GetFileName(file.Key) + " (" + done + " of " + plan.Files.Count + ")...");
                    ComposeFile(file.Key, file.Value, result);
                    _state.Composition[file.Key].Signature = fileSignatures[file.Key];
                }
                _sources?.Clear();

                //Levels: one mod's exactly as it ships, several versions (mods, or a mod and the user's own) combined into one
                foreach (KeyValuePair<string, List<List<Planned>>> level in plan.Levels.OrderBy(o => o.Key))
                {
                    if (keptLevels.Contains(level.Key))
                    {
                        progress(level.Key + " is unchanged since last time - leaving it as it is...");
                        continue;
                    }
                    _state.LevelSignatures[level.Key] = levelSignatures[level.Key];
                    bool withOwn = ownLevels.Contains(level.Key);
                    if (wholeLevels.Contains(level.Key))
                    {
                        progress("Installing " + level.Key + "...");
                        string used = level.Value.Last()[0].Mod.Name;
                        if (LevelFiles(level.Key).Any(_state.Own.ContainsKey))
                            result.Conflicts.Add(new MergeConflict() { Target = level.Key, Where = "the whole level", Kind = ConflictKind.WholeFile, Kept = used, Lost = MergeConflict.OwnChanges, Detail = "a level the game doesn't ship can't be combined; yours comes back when the mod is removed" });
                        for (int i = 0; i < level.Value.Count - 1; i++)
                            result.Conflicts.Add(new MergeConflict() { Target = level.Key, Where = "the whole level", Kind = ConflictKind.WholeFile, Kept = used, Lost = level.Value[i][0].Mod.Name, Detail = "both add a level of that name, which can't be combined" });
                        foreach (Planned planned in level.Value.Last())
                            ComposeFile(ModToolkit.Normalise(planned.Entry.Target), new List<Planned>() { planned }, result, false);
                    }
                    else if (level.Value.Count == 1 && !withOwn)
                    {
                        progress("Installing " + level.Key + "...");
                        foreach (Planned planned in level.Value[0])
                            ComposeFile(ModToolkit.Normalise(planned.Entry.Target), new List<Planned>() { planned }, result);
                    }
                    else
                    {
                        progress("Combining " + (level.Value.Count + (withOwn ? 1 : 0)) + " versions of " + level.Key + "...");
                        ComposeLevel(level.Key, level.Value, withOwn, result, progress);
                    }
                    //One level's originals at a time - a mod changing every level would otherwise hold all of them
                    _sources?.Clear();
                }

                //What's worked out from the rest
                if (regenerateTrees)
                {
                    progress("Updating each level's behaviour tree list...");
                    RegenerateTreeLists(result);
                }
                if (regenerateLevelList)
                {
                    progress("Updating the game's level list...");
                    PatchManager.UpdateLevelListInPackages(_manifest.Platform, _gameRoot);
                    RecordDerived(ModToolkit.LevelListPath);
                }

                //For the install scan: each mod's share of what's on disk
                foreach (KeyValuePair<string, ModState.ComposedFile> composed in _state.Composition)
                    foreach (string modId in composed.Value.Mods)
                        _state.FindMod(modId)?.Applied.Add(composed.Key, composed.Value.Sha256Hex);
                foreach (MergeConflict conflict in result.Conflicts)
                    _state.Conflicts.Add(new ModState.RecordedConflict() { Target = conflict.Target, Where = conflict.Where, Kept = conflict.Kept, Lost = conflict.Lost, Text = conflict.Describe(), Kind = conflict.Kind, Detail = conflict.Detail });

                _state.Save();
                _cache.Save();
                File.Delete(ModToolkit.JournalFile(_gameRoot));
                DeleteSnapshot(journalId);
                progress("Tidying up...");
                PruneStore();
                result.Success = true;
                return result;
            }
            catch (Exception e)
            {
                progress("Something went wrong - putting everything back...");
                try
                {
                    RestoreSnapshot(journalId, true);
                    File.Delete(ModToolkit.JournalFile(_gameRoot));
                    //The state went with the files: reload what was saved before this started
                    ModState saved = ModState.Load(_gameRoot);
                    _state.Mods = saved.Mods;
                    _state.Composition = saved.Composition;
                    _state.Conflicts = saved.Conflicts;
                    _state.Own = saved.Own;
                    _state.LevelSignatures = saved.LevelSignatures;
                }
                catch (Exception restoreError)
                {
                    result.Error = "Applying mods failed (" + e.Message + "), and putting things back also failed: " + restoreError.Message
                        + "\nOpenCAGE keeps what it needs to finish putting things back: open the Mod Manager again to do it.";
                    return result;
                }
                result.Error = Explain(e);
                result.Conflicts.Clear();
                return result;
            }
        }

        /* Put a file back to how it is with no mods: the user's own version, else its baseline. Files of a level about to
           be combined with the user's own changes go back to their originals instead - the combining starts from those. */
        private void RestoreUnmodded(string path, bool toOriginal)
        {
            string sha;
            if (toOriginal)
                sha = _state.Baseline[path].Sha256Hex;
            else
                sha = UnmoddedSha(path);
            string fullPath = ModToolkit.Denormalise(_gameRoot, path);
            if (sha == null)
            {
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
                //Also when the journal already moved it aside: the folders a mod's file needed go with it
                RemoveEmptyFolders(fullPath);
            }
            else
            {
                byte[] hash = _cache.Hash(path);
                if (hash == null || ModToolkit.ToHex(hash) != sha)
                {
                    byte[] content = _store.Retrieve(sha);
                    if (content == null)
                        throw new Exception("OpenCAGE's copy of the original " + path + " is missing, so it can't be put back.");
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                    File.WriteAllBytes(fullPath, content);
                }
            }
            _cache.Invalidate(path);
        }

        /// <summary>
        /// A mod's version of one file, as it ships: whole, a patch from vanilla, or (format 1) a config's changed
        /// values applied to vanilla. Null for a file the mod deletes.
        /// </summary>
        private byte[] Resolve(Planned planned)
        {
            ModPackageEntry entry = planned.Entry;
            string target = ModToolkit.Normalise(entry.Target);
            byte[] produced;
            switch (entry.Kind)
            {
                case ModPackageEntry.KindDelete:
                    return null;

                case ModPackageEntry.KindFile:
                    produced = ReadPayload(planned);
                    break;

                case ModPackageEntry.KindDelta:
                    {
                        //The patch was measured against the unmodified file: vanilla, or a baseline with exactly those bytes. Mods
                        //patching the same big file (every level mod patches its level's texture pack) share one read of it.
                        byte[] source = CachedSource(target + "|" + entry.SourceShaHex + "|" + entry.SourceAlsoTarget, () =>
                        {
                            byte[] original = VanillaBytes(target);
                            if (original == null || (entry.SourceShaHex != null && entry.SourceAlsoTarget == null && ModToolkit.ToHex(ModToolkit.Sha256(original)) != entry.SourceShaHex))
                            {
                                byte[] baseline = BaselineBytes(target);
                                if (baseline != null && (entry.SourceShaHex == null || ModToolkit.ToHex(ModToolkit.Sha256(baseline)) == entry.SourceShaHex))
                                    original = baseline;
                            }
                            if (original == null)
                                throw new Exception("'" + planned.Mod.Name + "' changes " + target + ", but this game's copy of that file isn't the original "
                                    + "and no original is stored to apply the change to. Verify the game files in Steam, then try again.");
                            if (entry.SourceAlsoTarget != null)
                            {
                                string alsoPath = ModToolkit.Normalise(entry.SourceAlsoTarget);
                                CaptureVanillaBaseline(alsoPath);
                                byte[] also = VanillaBytes(alsoPath);
                                if (also == null)
                                    throw new Exception("'" + planned.Mod.Name + "' needs the original " + alsoPath + ", and this game's copy has been changed. Verify the game files in Steam, then try again.");
                                original = ModExportBuilder.Concat(original, also);
                            }
                            return original;
                        });
                        byte[] patch = ReadPayload(planned);
                        try
                        {
                            produced = DeltaCodec.Apply(patch, source);
                        }
                        catch (Exception e)
                        {
                            throw new Exception("'" + planned.Mod.Name + "' could not be applied to " + target + ": " + e.Message
                                + " (it was made against a different version of that file)");
                        }
                        break;
                    }

                case ModPackageEntry.KindBml:
                    {
                        byte[] vanilla = BaseBytes(target);
                        if (vanilla == null)
                            throw new Exception("'" + planned.Mod.Name + "' changes config values in " + target + ", which this install doesn't have.");
                        BML bml = new BML(vanilla);
                        XmlDocument document = bml.Content;
                        BmlPatch.Apply(document, BmlPatch.Deserialise(System.Text.Encoding.UTF8.GetString(ReadPayload(planned))));
                        return FileMerger.WriteBml(vanilla, document);
                    }

                default:
                    throw new Exception("'" + planned.Mod.Name + "' contains a change of an unknown kind ('" + entry.Kind + "'). Update OpenCAGE.");
            }

            if (entry.TargetShaHex != null && ModToolkit.ToHex(ModToolkit.Sha256(produced)) != entry.TargetShaHex)
                throw new Exception("'" + planned.Mod.Name + "' is damaged: its copy of " + target + " doesn't match its own checksum. Download it again.");
            return produced;
        }

        /* Originals read while one apply runs, by what they were read for - several mods patching one big file read it once */
        private Dictionary<string, byte[]> _sources;

        private byte[] CachedSource(string key, Func<byte[]> read)
        {
            if (_sources == null)
                return read();
            if (!_sources.TryGetValue(key, out byte[] bytes))
                _sources[key] = bytes = read();
            return bytes;
        }

        /* A change's bytes out of its mod's file: a failure here is the file being damaged, whatever the reason */
        private static byte[] ReadPayload(Planned planned)
        {
            try
            {
                return planned.Package.ReadPayload(planned.Entry);
            }
            catch (Exception e)
            {
                throw new Exception("'" + planned.Mod.Name + "' is damaged - part of its file can't be read (" + e.Message + "). Download it again, then add it to the Mod Manager again.");
            }
        }

        /* One file from the user's own version of it (if they have one) and every mod that changes it, in list order */
        private void ComposeFile(string target, List<Planned> contributors, TransactionResult result, bool withOwn = true)
        {
            List<(string Id, string Name, byte[] Bytes)> sources = new List<(string, string, byte[])>();
            (bool hasOwn, byte[] ownBytes) = withOwn ? OwnVersion(target) : (false, null);
            if (hasOwn)
                sources.Add((null, MergeConflict.OwnChanges, ownBytes));
            foreach (Planned planned in contributors)
                sources.Add((planned.Mod.Id, planned.Mod.Name, Resolve(planned)));

            //A deletion is the last word on everything before it
            int lastDelete = sources.FindLastIndex(o => o.Bytes == null);
            List<Contribution> versions = sources.Skip(lastDelete + 1).Select(o => new Contribution() { ModId = o.Id, ModName = o.Name, Bytes = o.Bytes }).ToList();
            if (lastDelete >= 0 && sources.Count > 1)
            {
                string deleter = sources[lastDelete].Name;
                if (versions.Count == 0)
                    result.Conflicts.Add(new MergeConflict() { Target = target, Where = "the whole file", Kind = ConflictKind.RemovedVsChanged, Kept = deleter, Lost = sources.First(o => o.Bytes != null).Name, Detail = "it's deleted" });
                else
                    result.Conflicts.Add(new MergeConflict() { Target = target, Where = "the whole file", Kind = ConflictKind.RemovedVsChanged, Kept = versions.Last().ModName, Lost = deleter, Detail = "one deletes it, a later one changes it" });
            }

            string fullPath = ModToolkit.Denormalise(_gameRoot, target);
            byte[] written;
            if (versions.Count == 0)
            {
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
                RemoveEmptyFolders(fullPath);
                written = null;
            }
            else
            {
                //Changed before OpenCAGE kept its original: nothing to combine a mod's version with, so it's used as it is
                if (!hasOwn && _state.Baseline.TryGetValue(target, out ModState.BaselineRecord adopted) && !adopted.IsVanilla && adopted.Sha256Hex != null && _manifest.Contains(target))
                    result.Warnings.Add("OpenCAGE has no original copy of " + MergeConflict.Place(target) + ", so '" + versions.Last().ModName + "'s version replaces your own changes to it while it's installed (they come back when it's removed).");
                if (versions.Count == 1)
                    written = versions[0].Bytes; //one version: exactly as it ships, no original needed
                else
                {
                    MergeOutcome outcome = FileMerger.Merge(target, BaseBytes(target), versions);
                    result.Conflicts.AddRange(outcome.Conflicts);
                    if (outcome.FellBack)
                        result.Warnings.Add(char.ToUpperInvariant(MergeConflict.Place(target)[0]) + MergeConflict.Place(target).Substring(1) + " couldn't be combined, so '" + versions.Last().ModName + "''s version was used.");
                    written = outcome.Bytes;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllBytes(fullPath, written);
            }
            _cache.Invalidate(target);
            _state.Composition[target] = new ModState.ComposedFile()
            {
                Sha256Hex = written == null ? null : ModToolkit.ToHex(ModToolkit.Sha256(written)),
                Mods = contributors.Select(o => o.Mod.Id).Distinct().ToList(),
            };
        }

        /* Several versions of one level - mods', and the user's own first if they have one - combined. The level folder
           holds the level unmodified (its original files, when the user's own version is one of those combined). */
        private void ComposeLevel(string level, List<List<Planned>> perMod, bool withOwn, TransactionResult result, Action<string> progress)
        {
            List<LevelVersion> versions = new List<LevelVersion>();
            if (withOwn)
            {
                LevelVersion own = new LevelVersion() { ModId = null, ModName = MergeConflict.OwnChanges };
                foreach (string path in LevelFiles(level).Where(_state.Own.ContainsKey))
                    own.Files[path] = OwnVersion(path).Bytes;
                versions.Add(own);
            }
            foreach (List<Planned> entries in perMod)
            {
                LevelVersion version = new LevelVersion() { ModId = entries[0].Mod.Id, ModName = entries[0].Mod.Name };
                foreach (Planned planned in entries)
                    version.Files[ModToolkit.Normalise(planned.Entry.Target)] = Resolve(planned);
                versions.Add(version);
            }

            if (LevelMerger == null)
            {
                //Nothing here can combine levels: the last mod's version wins, whole
                LevelVersion last = versions.Last();
                foreach (KeyValuePair<string, byte[]> file in last.Files)
                    WriteLevelFile(file.Key, file.Value);
                for (int i = 0; i < versions.Count - 1; i++)
                    result.Conflicts.Add(new MergeConflict() { Target = level, Where = "the whole level", Kind = ConflictKind.WholeFile, Kept = last.ModName, Lost = versions[i].ModName, Detail = "levels can't be combined in this build" });
            }
            else
            {
                LevelMergeResult merged = LevelMerger.Merge(_gameRoot, level, versions, progress);
                result.Conflicts.AddRange(merged.Conflicts);
                result.Warnings.AddRange(merged.Warnings);
                result.CombinedLevels.Add(level);
            }

            //Every file of the level that isn't how it is without mods any more is this composition's
            List<string> modIds = versions.Where(o => o.ModId != null).Select(o => o.ModId).ToList();
            foreach (string path in LevelFiles(level))
            {
                _cache.Invalidate(path);
                byte[] hash = _cache.Hash(path);
                string sha = hash == null ? null : ModToolkit.ToHex(hash);
                if (!_state.Baseline.ContainsKey(path))
                {
                    //A file the merge made that nothing knew about: its baseline is "not there"
                    _state.Baseline[path] = new ModState.BaselineRecord() { Sha256Hex = null, IsVanilla = !_manifest.Contains(path) };
                }
                if (sha == UnmoddedSha(path))
                    continue;
                _state.Composition[path] = new ModState.ComposedFile() { Sha256Hex = sha, Mods = new List<string>(modIds) };
            }
        }

        /* The folders a deleted file leaves empty - a level a mod added, once it's removed - go too, up to DATA */
        private void RemoveEmptyFolders(string deletedFile)
        {
            try
            {
                string data = Path.GetFullPath(Path.Combine(_gameRoot, "DATA")).TrimEnd(Path.DirectorySeparatorChar);
                string folder = Path.GetDirectoryName(Path.GetFullPath(deletedFile));
                while (folder != null && folder.Length > data.Length && folder.StartsWith(data, StringComparison.OrdinalIgnoreCase)
                    && Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                {
                    Directory.Delete(folder);
                    folder = Path.GetDirectoryName(folder);
                }
            }
            catch { }
        }

        private void WriteLevelFile(string path, byte[] bytes)
        {
            string fullPath = ModToolkit.Denormalise(_gameRoot, path);
            if (bytes == null)
            {
                if (File.Exists(fullPath)) File.Delete(fullPath);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllBytes(fullPath, bytes);
            }
            _cache.Invalidate(path);
        }

        /* Each level's behaviour tree list, from the trees and character configs now installed. A list that
           comes out the same as its baseline is left alone. */
        private void RegenerateTreeLists(TransactionResult result)
        {
            string data = Path.Combine(_gameRoot, "DATA");
            BehaviorTreeDB.Requirements requirements = BehaviorTreeDB.ReadRequirements(data);
            foreach (string path in TreeListFiles().ToList())
            {
                try
                {
                    string fullPath = ModToolkit.Denormalise(_gameRoot, path);
                    BehaviorTreeDB db = new BehaviorTreeDB(fullPath);
                    if (db.Regenerate(requirements))
                        db.Save();
                    RecordDerived(path);
                }
                catch (Exception e)
                {
                    result.Warnings.Add("Couldn't update the behaviour tree list of " + ModToolkit.LevelOf(path) + ": " + e.Message);
                }
            }
            foreach (string problem in requirements.Problems)
                result.Warnings.Add(problem);
        }

        /* A file worked out from the others: part of the composition only when it differs from its baseline */
        private void RecordDerived(string path)
        {
            _cache.Invalidate(path);
            byte[] hash = _cache.Hash(path);
            string sha = hash == null ? null : ModToolkit.ToHex(hash);
            if (_state.Baseline.TryGetValue(path, out ModState.BaselineRecord baseline) && baseline.Sha256Hex == sha)
                return;
            if (baseline == null)
                _state.Baseline[path] = new ModState.BaselineRecord() { Sha256Hex = null, IsVanilla = !_manifest.Contains(path) };
            _state.Composition[path] = new ModState.ComposedFile() { Sha256Hex = sha };
        }

        /// <summary>
        /// Do any files the last apply wrote no longer hold those bytes? True after a Steam verify wiped them, or
        /// after something else overwrote them.
        /// </summary>
        public List<string> PathsNeedingRepair()
        {
            List<string> stale = new List<string>();
            lock (_mutex)
            {
                foreach (KeyValuePair<string, ModState.ComposedFile> composed in _state.Composition)
                {
                    byte[] hash = _cache.Hash(composed.Key);
                    string sha = hash == null ? null : ModToolkit.ToHex(hash);
                    if (sha != composed.Value.Sha256Hex)
                        stale.Add(composed.Key);
                }
                _cache.Save();
            }
            return stale;
        }
        #endregion
    }
}
#endif

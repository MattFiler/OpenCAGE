using CathodeLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace OpenCAGE.Modding
{
    /* The app-side face of the mod system: one lazily-built set of services bound to the current
     * game directory, plus the capture hooks the editors call before overwriting game files.
     *
     * Everything here must be safe to call from any editor at any time - a capture that fails or
     * has no manifest to work with does nothing rather than getting in the way of a save.
     *
     * The mod packaging system as a whole sits behind ENABLE_MOD_PACKAGES. This class keeps its
     * shape either way: with the macro undefined the capture hooks below compile to nothing and
     * no pristine copy is ever taken, so the editors calling them need no conditionals of their
     * own. Everything else in the class - the services, the state, the scanner - is gated. */
    public static class ModServices
    {
#if ENABLE_MOD_PACKAGES
        private static readonly object _lock = new object();
        private static string _gameRoot;
        private static VanillaManifest _manifest;
        private static HashCache _cache;
        private static ModState _state;
        private static BaselineStore _store;
        private static ModInstaller _installer;

        private static HashSet<string> _capturedLevels = new HashSet<string>();
        private static bool _capturedSmallFiles = false;

        public static string GameRoot { get { EnsureBuilt(); return _gameRoot; } }
        public static VanillaManifest Manifest { get { EnsureBuilt(); return _manifest; } }
        public static HashCache Cache { get { EnsureBuilt(); return _cache; } }
        public static ModState State { get { EnsureBuilt(); return _state; } }
        public static ModInstaller Installer { get { EnsureBuilt(); return _installer; } }

        /// <summary>
        /// True when a game directory is set and the embedded manifest covers it. Without this
        /// nothing can be told apart from vanilla, so mods can't be installed or made
        /// (<see cref="VanillaManifest.Unsupported"/> says why).
        /// </summary>
        public static bool ManifestAvailable { get { EnsureBuilt(); return _manifest != null && _manifest.Available; } }

        private static void EnsureBuilt()
        {
            lock (_lock)
            {
                //What OpenCAGE adds to game files for its own use (the Launch Game UI option movies in UI.PAK) is looked past by
                //every hash, original, own version and package, and put back after every write (ModToolkit.WithoutOwnContent)
                if (ModToolkit.WithoutOwnContent == null)
                {
                    ModToolkit.WithoutOwnContent = (path, bytes) => UiMovieOverrides.WithoutOwnedEntries(bytes);
                    ModToolkit.WithOwnContent = (path, bytes) => UiMovieOverrides.WithOwnedEntries(bytes);
                }

                string root = Singleton.PathToAI;
                if (string.IsNullOrEmpty(root))
                    return;
                if (_installer != null && _gameRoot == root)
                    return;

                _gameRoot = root;
                _manifest = new VanillaManifest(PatchManager.GetPlatform(root));
                _cache = new HashCache(root);
                _state = ModState.Load(root);
                _store = new BaselineStore(root);
                _installer = new ModInstaller(root, _manifest, _cache, _state, _store);
                //Several mods changing one level are combined into it; their pages start from the ones OpenCAGE ships
                _installer.LevelMerger = new ModLevelMerger()
                {
                    VanillaPages = (composite, level) => composite == null ? null
                        : FlowgraphLayoutManager.GetLayoutsForPort(composite, null, FlowgraphLayoutManager.BundledLevelName(null, level)),
                };
                _capturedLevels.Clear();
                _capturedSmallFiles = false;
            }
        }

        public static InstallScanner NewScanner()
        {
            EnsureBuilt();
            return _installer == null ? null : new InstallScanner(_gameRoot, _manifest, _cache, _state);
        }
#endif

        /// <summary>
        /// Whether the file on disk is one the Mod Manager put together from installed mods. False when it isn't, when that
        /// can't be told, or without ENABLE_MOD_PACKAGES.
        /// </summary>
        public static bool MadeByMods(string fullPath)
        {
#if ENABLE_MOD_PACKAGES
            try
            {
                EnsureBuilt();
                if (_installer == null || string.IsNullOrEmpty(fullPath))
                    return false;
                string normalised = ModToolkit.NormaliseFull(_gameRoot, fullPath);
                return normalised != null && _state.Composition.TryGetValue(normalised, out ModState.ComposedFile composed) && composed.Mods.Count != 0;
            }
            catch
            {
                return false;
            }
#else
            return false;
#endif
        }

        #region CAPTURE HOOKS
        /// <summary>
        /// Call before overwriting any game file: if its bytes are still vanilla, a pristine copy
        /// is kept so mods can be diffed against it and uninstalls can restore it. Cheap when
        /// already captured, silent on any failure. Does nothing without ENABLE_MOD_PACKAGES.
        /// </summary>
        public static void CaptureBeforeWrite(string fullPath, bool usersEdit = true)
        {
#if ENABLE_MOD_PACKAGES
            try
            {
                EnsureBuilt();
                if (_installer == null || !ManifestAvailable)
                    return;
                string normalised = ModToolkit.NormaliseFull(_gameRoot, fullPath);
                if (normalised == null || ModToolkit.IsExcluded(normalised))
                    return;
                _installer.CaptureVanillaBaseline(normalised);
                //When the user last changed it, for Create a Mod
                //(not when it's OpenCAGE writing its own content, which the mod system looks past)
                if (usersEdit)
                    EditHistory.SharedFileChanged(_gameRoot, normalised);
            }
            catch { }
#endif
        }

        /// <summary>
        /// Change a game file the same way on disk and in everything the Mod Manager keeps of it (ModInstaller.RewriteEverywhere),
        /// so the change reads as nobody's edit - for OpenCAGE taking back what it once wrote into a game file of its own accord.
        /// Without the Mod Manager set up for this game, the file on disk alone. <paramref name="fix"/> gets the file's bytes and
        /// returns them changed, or null for no change. True when anything changed.
        /// </summary>
        public static bool RewriteEverywhere(string fullPath, Func<byte[], byte[]> fix)
        {
            try
            {
#if ENABLE_MOD_PACKAGES
                EnsureBuilt();
                string normalised = _installer != null && ManifestAvailable ? ModToolkit.NormaliseFull(_gameRoot, fullPath) : null;
                if (normalised != null && !ModToolkit.IsExcluded(normalised))
                    return _installer.RewriteEverywhere(normalised, fix);
#endif
                if (!File.Exists(fullPath))
                    return false;
                byte[] content = fix(File.ReadAllBytes(fullPath));
                if (content == null)
                    return false;
                File.WriteAllBytes(fullPath, content);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Whether <see cref="CaptureBeforeWrite"/> may have this file's original still to keep - read, checked and stored whole,
        /// which for a big file takes a while (once: before the first write of a file as the game shipped it). False when it's
        /// kept already, and without ENABLE_MOD_PACKAGES.
        /// </summary>
        public static bool MayCaptureBeforeWrite(string fullPath)
        {
#if ENABLE_MOD_PACKAGES
            try
            {
                EnsureBuilt();
                if (_installer == null || !ManifestAvailable)
                    return false;
                string normalised = ModToolkit.NormaliseFull(_gameRoot, fullPath);
                if (normalised == null || ModToolkit.IsExcluded(normalised))
                    return false;
                return !_installer.HasVanillaBaseline(normalised);
            }
            catch
            {
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>
        /// Call before saving a level: captures pristine copies of every still-vanilla file in the
        /// level's folder. The first call for a level does real hashing work; later ones are free.
        /// Does nothing without ENABLE_MOD_PACKAGES.
        /// </summary>
        public static void CaptureLevelBeforeSave(string levelName)
        {
#if ENABLE_MOD_PACKAGES
            try
            {
                EnsureBuilt();
                if (_installer == null || !ManifestAvailable || levelName == null)
                    return;
                lock (_lock)
                {
                    if (_capturedLevels.Contains(levelName))
                        return;
                    _capturedLevels.Add(levelName);
                }

                string levelDir = Path.Combine(_gameRoot, "DATA", "ENV", levelName.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(levelDir))
                    return;
                List<string> files = new List<string>();
                foreach (string file in Directory.GetFiles(levelDir, "*", SearchOption.AllDirectories))
                {
                    string normalised = ModToolkit.NormaliseFull(_gameRoot, file);
                    if (normalised != null && !ModToolkit.IsExcluded(normalised))
                        files.Add(normalised);
                }
                //Read as many at once as the drive reads well (one on a spinning disk), compressed four at once at most: each is
                //held whole to be stored, and a level's biggest files are a good part of a gigabyte between them
                _installer.CaptureVanillaBaselines(files, ModToolkit.ReadersFor(_gameRoot), Math.Min(4, Environment.ProcessorCount));
            }
            catch { }
#endif
        }

        /// <summary>
        /// One-off background capture of every small still-vanilla file outside the level and sound
        /// folders (configs, text, UI definitions) - so config edits never need their own hooks.
        /// Does nothing without ENABLE_MOD_PACKAGES.
        /// </summary>
        public static void CaptureSmallFilesInBackground()
        {
#if ENABLE_MOD_PACKAGES
            try
            {
                EnsureBuilt();
                if (_installer == null || !ManifestAvailable)
                    return;
                lock (_lock)
                {
                    if (_capturedSmallFiles)
                        return;
                    _capturedSmallFiles = true;
                }

                Thread thread = new Thread(() =>
                {
                    try
                    {
                        foreach (FileHashTable.Entry entry in _manifest.Entries)
                        {
                            if (entry.Path.StartsWith("DATA/ENV/") || entry.Path.StartsWith("DATA/SOUND/"))
                                continue;
                            if (entry.Size > 8 * 1024 * 1024)
                                continue;
                            _installer.CaptureVanillaBaseline(entry.Path, false);
                        }
                        _installer.SaveState();
                    }
                    catch { }
                });
                thread.IsBackground = true;
                thread.Priority = ThreadPriority.Lowest;
                thread.Start();
            }
            catch { }
#endif
        }
        #endregion
    }
}

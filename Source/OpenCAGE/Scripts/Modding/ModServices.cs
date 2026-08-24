using CathodeLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace OpenCAGE.Modding
{
    /* The app-side face of the mod system: one lazily-built set of services bound to the current
     * game directory, plus the capture hooks the editors call before overwriting game files.
     *
     * Everything here must be safe to call from any editor at any time - a capture that fails or
     * has no manifest to work with does nothing rather than getting in the way of a save. */
    public static class ModServices
    {
        private static readonly object _lock = new object();
        private static string _gameRoot;
        private static VanillaManifest _manifest;
        private static HashCache _cache;
        private static ModState _state;
        private static BaselineStore _store;
        private static ModInstaller _installer;

        private static HashSet<string> _capturedLevels = new HashSet<string>();
        private static bool _capturedSmallFiles = false;

        /// <summary>
        /// A package path handed over on the command line (a double-clicked .opencage file);
        /// consumed by CommandsEditor, which opens the Mod Manager on it.
        /// </summary>
        public static string PendingPackageImport = null;

        public static string GameRoot { get { EnsureBuilt(); return _gameRoot; } }
        public static VanillaManifest Manifest { get { EnsureBuilt(); return _manifest; } }
        public static HashCache Cache { get { EnsureBuilt(); return _cache; } }
        public static ModState State { get { EnsureBuilt(); return _state; } }
        public static ModInstaller Installer { get { EnsureBuilt(); return _installer; } }

        /// <summary>
        /// True when a game directory is set and the embedded manifest covers it. Without this the
        /// mod manager still works (file-level, whole files), but nothing can be told apart from
        /// vanilla.
        /// </summary>
        public static bool ManifestAvailable { get { EnsureBuilt(); return _manifest != null && _manifest.Available; } }

        private static void EnsureBuilt()
        {
            lock (_lock)
            {
                string root = Singleton.PathToAI;
                if (string.IsNullOrEmpty(root))
                    return;
                if (_installer != null && _gameRoot == root)
                    return;

                _gameRoot = root;
                _manifest = new VanillaManifest();
                _cache = new HashCache(root);
                _state = ModState.Load(root);
                _store = new BaselineStore(root);
                _installer = new ModInstaller(root, _manifest, _cache, _state, _store);
                _capturedLevels.Clear();
                _capturedSmallFiles = false;
            }
        }

        public static InstallScanner NewScanner()
        {
            EnsureBuilt();
            return _installer == null ? null : new InstallScanner(_gameRoot, _manifest, _cache, _state);
        }

        #region CAPTURE HOOKS
        /// <summary>
        /// Call before overwriting any game file: if its bytes are still vanilla, a pristine copy
        /// is kept so mods can be diffed against it and uninstalls can restore it. Cheap when
        /// already captured, silent on any failure.
        /// </summary>
        public static void CaptureBeforeWrite(string fullPath)
        {
            try
            {
                EnsureBuilt();
                if (_installer == null || !ManifestAvailable)
                    return;
                string normalised = ModToolkit.NormaliseFull(_gameRoot, fullPath);
                if (normalised == null || ModToolkit.IsExcluded(normalised))
                    return;
                _installer.CaptureVanillaBaseline(normalised);
            }
            catch { }
        }

        /// <summary>
        /// Call before saving a level: captures pristine copies of every still-vanilla file in the
        /// level's folder. The first call for a level does real hashing work; later ones are free.
        /// </summary>
        public static void CaptureLevelBeforeSave(string levelName)
        {
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
                foreach (string file in Directory.GetFiles(levelDir, "*", SearchOption.AllDirectories))
                {
                    string normalised = ModToolkit.NormaliseFull(_gameRoot, file);
                    if (normalised != null && !ModToolkit.IsExcluded(normalised))
                        _installer.CaptureVanillaBaseline(normalised, false);
                }
                _installer.SaveState();
            }
            catch { }
        }

        /// <summary>
        /// One-off background capture of every small still-vanilla file outside the level and sound
        /// folders (configs, text, UI definitions) - so config edits never need their own hooks.
        /// </summary>
        public static void CaptureSmallFilesInBackground()
        {
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
        }
        #endregion
    }
}

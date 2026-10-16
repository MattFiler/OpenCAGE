using CATHODE;
using CATHODE.Scripting;
using CathodeLib;
using Newtonsoft.Json;
using OpenCAGE.Undo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AssetKind = CathodeLib.AssetModificationInfoTable.AssetKind;

namespace OpenCAGE
{
    /// <summary>
    /// When the user last changed each part of the game in OpenCAGE: a level's composites and assets (models, textures,
    /// materials, its galaxy...) in the level's own tables, saved with it - and the game's shared files (animations, UI,
    /// text, settings) in the install's mod tools folder. Create a Mod lists the user's changes by it, newest first.
    /// </summary>
    /// <remarks>
    /// Only the user's own work is recorded: what OpenCAGE changes by itself (dead links removed when a composite is
    /// first shown, defaults filled in when an entity is first inspected, links recompiled from the pages) never is,
    /// and neither is where nodes sit on a page. Recording never gets in the way of the edit it records.
    /// </remarks>
    public static class EditHistory
    {
        /// <summary>This build, as the history notes which build made a change: 0.18.0.41 is 0x00120029.</summary>
        public static int EditorVersion
        {
            get
            {
                if (_editorVersion == null)
                {
                    int packed = 0;
                    try
                    {
                        Version version = Assembly.GetExecutingAssembly().GetName().Version;
                        packed = (Byte(version.Major) << 24) | (Byte(version.Minor) << 16) | (Byte(version.Build) << 8) | Byte(version.Revision);
                    }
                    catch { }
                    _editorVersion = packed;
                }
                return _editorVersion.Value;
            }
        }
        private static int? _editorVersion;
        private static int Byte(int part) => Math.Max(0, Math.Min(255, part));

        private static int Now => (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>A unix time from the history, as a local time.</summary>
        public static DateTime ToLocal(int unixSeconds) => DateTimeOffset.FromUnixTimeSeconds(unixSeconds).LocalDateTime;

        #region COMPOSITES
        /// <summary>The composites an edit changes the scripts of (none for one to editor data only).</summary>
        public static IEnumerable<ShortGuid> CompositesChangedBy(IEdit edit)
        {
            if (edit is IChangesComposites changes)
                return changes.ChangedComposites ?? Enumerable.Empty<ShortGuid>();
            return edit == null || edit.CompositeId.IsInvalid ? Enumerable.Empty<ShortGuid>() : new[] { edit.CompositeId };
        }

        /// <summary>Record an edit just made, undone or done again.</summary>
        public static void Record(Commands commands, IEdit edit)
        {
            try { CompositesChanged(commands, CompositesChangedBy(edit)); }
            catch { }
        }

        /// <summary>Record that the user changed these composites of a level now - brought them in, made them, changed them.</summary>
        public static void CompositesChanged(Commands commands, IEnumerable<ShortGuid> composites)
        {
            if (commands?.Utils == null || composites == null)
                return;
            int now = Now;
            foreach (ShortGuid id in composites.Distinct().ToList())
                if (!id.IsInvalid)
                    commands.Utils.SetModificationInfo(new CompositeModificationInfoTable.ModificationInfo() { composite_id = id, editor_version = EditorVersion, modification_date = now });
        }

        public static void CompositesChanged(Commands commands, IEnumerable<Composite> composites)
        {
            if (composites != null)
                CompositesChanged(commands, composites.Where(o => o != null).Select(o => o.shortGUID));
        }
        #endregion

        #region LEVEL ASSETS
        /// <summary>Record that the user changed one of a level's assets now. Saved with the level.</summary>
        public static void AssetChanged(Level level, AssetKind kind, string name)
        {
            try { level?.Commands?.Utils?.SetAssetModificationInfo(kind, name, EditorVersion); }
            catch { }
        }
        #endregion

        #region SHARED FILES
#if ENABLE_MOD_PACKAGES
        /* The files OpenCAGE writes outside a level save - the game's shared files, and a level's own text and the like - by
           game-relative path, with when the user last changed each: DATA/MODTOOLS/EDITHISTORY.json - per install, and never
           part of a mod */
        private static readonly object _sharedLock = new object();
        private static string _sharedRoot;
        private static Dictionary<string, int> _shared;

        private static string SharedPath(string gameRoot) => Path.Combine(gameRoot, "DATA", "MODTOOLS", "EDITHISTORY.json");

        private static Dictionary<string, int> Shared(string gameRoot)
        {
            if (_shared == null || _sharedRoot != gameRoot)
            {
                _sharedRoot = gameRoot;
                _shared = null;
                try
                {
                    string path = SharedPath(gameRoot);
                    if (File.Exists(path))
                        _shared = JsonConvert.DeserializeObject<Dictionary<string, int>>(File.ReadAllText(path));
                }
                catch { }
                if (_shared == null)
                    _shared = new Dictionary<string, int>();
            }
            return _shared;
        }

        /// <summary>
        /// Record that the user is changing one of the game's files now, outside a level save (which records what in the
        /// level changed instead). Called just before OpenCAGE writes one.
        /// </summary>
        public static void SharedFileChanged(string gameRoot, string normalisedPath)
        {
            if (string.IsNullOrEmpty(gameRoot) || string.IsNullOrEmpty(normalisedPath))
                return;
            try
            {
                lock (_sharedLock)
                {
                    Dictionary<string, int> shared = Shared(gameRoot);
                    shared[normalisedPath] = Now;
                    string path = SharedPath(gameRoot);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    string temp = path + ".tmp";
                    File.WriteAllText(temp, JsonConvert.SerializeObject(shared, Formatting.Indented));
                    if (File.Exists(path)) File.Delete(path);
                    File.Move(temp, path);
                }
            }
            catch { }
        }

        /// <summary>When the user last changed one of the game's shared files in OpenCAGE, or null if it isn't known.</summary>
        public static DateTime? SharedFileEdited(string gameRoot, string normalisedPath)
        {
            try
            {
                lock (_sharedLock)
                    return Shared(gameRoot).TryGetValue(normalisedPath ?? "", out int when) ? ToLocal(when) : (DateTime?)null;
            }
            catch { return null; }
        }
#endif
        #endregion
    }
}

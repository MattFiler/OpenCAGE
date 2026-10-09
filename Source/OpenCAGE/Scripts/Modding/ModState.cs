#if ENABLE_MOD_PACKAGES
using CathodeLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenCAGE.Modding
{
    /* The install's mod bookkeeping, persisted at DATA/MODTOOLS/MODS/state.json: which packages are
     * in the library, which are enabled and in what order, what bytes we last wrote where, and
     * which baseline bytes the store holds for each path. */
    public class ModState
    {
        public class BaselineRecord
        {
            /* sha256 hex of the bytes held in the BaselineStore for this path, or null when the
             * baseline is "this file did not exist" (a mod added it; uninstall deletes it) */
            [JsonProperty("sha")] public string Sha256Hex;
            /* true when the stored bytes match the vanilla manifest - the difference between a real
             * pristine baseline and adopted unknown bytes */
            [JsonProperty("vanilla")] public bool IsVanilla;
        }

        public class InstalledMod
        {
            [JsonProperty("id")] public string Id;
            [JsonProperty("name")] public string Name;
            [JsonProperty("version")] public string Version;
            [JsonProperty("author")] public string Author;
            [JsonProperty("description")] public string Description;
            [JsonProperty("enabled")] public bool Enabled;
            [JsonProperty("priority")] public int Priority;
            [JsonProperty("package")] public string PackageFileName;
            /* PATH -> sha256 hex of the bytes this mod's application last put there (only the paths
             * where this mod was the final writer). Empty when the mod isn't currently applied. */
            [JsonProperty("applied")] public Dictionary<string, string> Applied = new Dictionary<string, string>();
        }

        /// <summary>A file the last apply wrote: what it should hold now, and which mods it was made from.</summary>
        public class ComposedFile
        {
            [JsonProperty("sha")] public string Sha256Hex;
            /* Mod ids in list order; empty for a file regenerated from the others (a level's behaviour tree list) */
            [JsonProperty("mods")] public List<string> Mods = new List<string>();
            /* What it was made from (the mods' versions of it, the user's own, the original): the same again, and the file
               still as it was written, means the next apply can leave it be */
            [JsonProperty("sig")] public string Signature;
        }

        /// <summary>A clash the last apply settled, kept for the Mod Manager to show.</summary>
        public class RecordedConflict
        {
            [JsonProperty("target")] public string Target;
            [JsonProperty("where")] public string Where;
            [JsonProperty("kept")] public string Kept;
            [JsonProperty("lost")] public string Lost;
            [JsonProperty("text")] public string Text;
            [JsonProperty("kind")] public Merging.ConflictKind Kind;
            [JsonProperty("detail")] public string Detail;
        }

        [JsonProperty("version")] public int Version = 2;
        /* Every file the last apply wrote (null bytes = deleted), so the next apply knows what to put back */
        [JsonProperty("composition")] public Dictionary<string, ComposedFile> Composition = new Dictionary<string, ComposedFile>();
        [JsonProperty("conflicts")] public List<RecordedConflict> Conflicts = new List<RecordedConflict>();
        /* Level -> what its installed version was made from (see ComposedFile.Signature) */
        [JsonProperty("levels")] public Dictionary<string, string> LevelSignatures = new Dictionary<string, string>();
        // which build's vanilla bytes this was measured against - the deltas only apply on top of
        // those exact hashes. Serialized by name so the JSON does not depend on the enum's order.
        [JsonProperty("hashSet")] [JsonConverter(typeof(StringEnumConverter))] public PatchManager.Platform HashSet = PatchManager.Platform.STEAM;
        [JsonProperty("lastScanUtc")] public DateTime? LastScanUtc;
        [JsonProperty("baseline")] public Dictionary<string, BaselineRecord> Baseline = new Dictionary<string, BaselineRecord>();
        /* PATH -> the user's own version of a file mods are installed onto: sha256 hex of bytes in the BaselineStore, or
         * null when their version is "no file". Their own work - changes made in OpenCAGE, not by a mod - is what mods
         * are combined with (mods win clashes), and what removing every mod puts back. No entry: the baseline is it. */
        [JsonProperty("own")] public Dictionary<string, string> Own = new Dictionary<string, string>();
        /* Trees of the animation tree layouts ("SETHASH/TREEHASH", hex) the user laid out while mods' layouts were
         * installed: their own layout of each (in Own) goes over the mods' as well as under them, so a hand edit made on
         * a mod's layout stays. See ModInstaller.FoldLayoutEdits. */
        [JsonProperty("layoutsOnTop")] public List<string> LayoutsOnTop = new List<string>();
        [JsonProperty("mods")] public List<InstalledMod> Mods = new List<InstalledMod>();

        [JsonIgnore] private string _path;

        public static ModState Load(string gameRoot)
        {
            string path = ModToolkit.StateFile(gameRoot);
            ModState state = null;
            try
            {
                if (File.Exists(path))
                    state = JsonConvert.DeserializeObject<ModState>(File.ReadAllText(path));
            }
            catch { }
            if (state == null)
                state = new ModState();
            if (state.Baseline == null) state.Baseline = new Dictionary<string, BaselineRecord>();
            if (state.Mods == null) state.Mods = new List<InstalledMod>();
            if (state.Composition == null) state.Composition = new Dictionary<string, ComposedFile>();
            if (state.Conflicts == null) state.Conflicts = new List<RecordedConflict>();
            if (state.Own == null) state.Own = new Dictionary<string, string>();
            if (state.LevelSignatures == null) state.LevelSignatures = new Dictionary<string, string>();
            if (state.LayoutsOnTop == null) state.LayoutsOnTop = new List<string>();
            //A version 1 state knew only each mod's own writes: those are what the next apply must put back
            foreach (InstalledMod mod in state.Mods)
                foreach (KeyValuePair<string, string> applied in mod.Applied ?? new Dictionary<string, string>())
                    if (!state.Composition.ContainsKey(applied.Key))
                        state.Composition[applied.Key] = new ComposedFile() { Sha256Hex = applied.Value, Mods = new List<string>() { mod.Id } };
            state._path = path;
            return state;
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            string temp = _path + ".tmp";
            File.WriteAllText(temp, JsonConvert.SerializeObject(this, Formatting.Indented));
            if (File.Exists(_path)) File.Delete(_path);
            File.Move(temp, _path);
        }

        public InstalledMod FindMod(string id)
        {
            return Mods.FirstOrDefault(o => o.Id == id);
        }

        public List<InstalledMod> ModsInPriorityOrder()
        {
            return Mods.OrderBy(o => o.Priority).ToList();
        }

        /// <summary>
        /// The last recorded writer of a path among applied mods, or null.
        /// </summary>
        public InstalledMod AppliedOwner(string normalisedPath)
        {
            InstalledMod owner = null;
            foreach (InstalledMod mod in ModsInPriorityOrder())
                if (mod.Applied.ContainsKey(normalisedPath))
                    owner = mod;
            return owner;
        }
    }
}
#endif

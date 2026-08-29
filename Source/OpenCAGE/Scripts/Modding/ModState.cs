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

        [JsonProperty("version")] public int Version = 1;
        // which build's vanilla bytes this was measured against - the deltas only apply on top of
        // those exact hashes. Serialized by name so the JSON does not depend on the enum's order.
        [JsonProperty("hashSet")] [JsonConverter(typeof(StringEnumConverter))] public PatchManager.Platform HashSet = PatchManager.Platform.STEAM;
        [JsonProperty("lastScanUtc")] public DateTime? LastScanUtc;
        [JsonProperty("baseline")] public Dictionary<string, BaselineRecord> Baseline = new Dictionary<string, BaselineRecord>();
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

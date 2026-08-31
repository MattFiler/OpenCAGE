using CATHODE;
using CATHODE.ShaderTypes;
using CathodeLib.ObjectExtensions;
using CathodeLib.Ubershaders;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenCAGE.Modding
{
    /* The harvested ubershader permutation database: every (family, feature mask) combination that
     * ships anywhere in the user's game data, with its full metadata and bytecode, keyed for the
     * material editor to pull from when feature checkboxes change.
     *
     * Built once from pristine level data (each level's three shader paks must hash-match the
     * vanilla manifest - a modified pak may contain retargeted entries whose mask no longer
     * describes its bytecode). Mask -> bytecode is stable game-wide (measured: 6 exceptional
     * masks out of 4,820 vary per level; first-seen wins and the collision is counted), so one
     * entry per (family, mask) is the right shape.
     *
     * Lives in DATA/MODTOOLS/SHADERS - inside the modding exclusion zone, so it can never leak
     * into a mod package. */
    public class ShaderDatabase
    {
        public const int FormatVersion = 1;

        public class Entry
        {
            public long Mask;
            public string Technique;
            public long RequirementFlags;
            public byte ShaderModel;
            public int CycleCount;
            public int RegisterCount;
            public uint PermutationHash;
            public Shaders.StateBlock RenderStates;
            public List<Shaders.StateBlock> Samplers = new List<Shaders.StateBlock>();
            public List<int> SamplerStageBindings = new List<int>();
            public List<int> SamplerRemaps = new List<int>();
            public List<int> EngineParameterRemaps = new List<int>();
            public List<int> VertexShaderParameterRemaps = new List<int>();
            public List<int> PixelShaderParameterRemaps = new List<int>();
            public List<int> HullShaderParameterRemaps = new List<int>();
            public List<int> DomainShaderParameterRemaps = new List<int>();
            public byte[] VertexShader;
            public byte[] PixelShader;
            public byte[] HullShader;
            public byte[] DomainShader;
            public byte[] GeometryShader;
            public byte[] ComputeShader;

            /// <summary>
            /// Materialise as a CathodeLib shader entry (fresh lists; blob arrays are shared with
            /// the database - callers add the entry to a level, they don't mutate bytecode).
            /// </summary>
            public Shaders.Shader ToShader(SHADER_LIST family)
            {
                Shaders.Shader shader = new Shaders.Shader()
                {
                    Technique = Technique,
                    Ubershader = family,
                    UbershaderFeatureFlags = Mask,
                    UbershaderRequirementFlags = RequirementFlags,
                    RequiredShaderModel = (Shaders.SHADER_MODEL)ShaderModel,
                    CycleCount = CycleCount,
                    RegisterCount = RegisterCount,
                    PermutationHash = PermutationHash,
                    RenderStates = RenderStates.Copy(),
                    VertexShader = VertexShader,
                    PixelShader = PixelShader,
                    HullShader = HullShader,
                    DomainShader = DomainShader,
                    GeometryShader = GeometryShader,
                    ComputeShader = ComputeShader,
                };
                foreach (Shaders.StateBlock sampler in Samplers)
                    shader.Samplers.Add(sampler.Copy());
                shader.SamplerStageBindings.AddRange(SamplerStageBindings);
                shader.SamplerRemaps.AddRange(SamplerRemaps);
                shader.EngineParameterRemaps.AddRange(EngineParameterRemaps);
                shader.VertexShaderParameterRemaps.AddRange(VertexShaderParameterRemaps);
                shader.PixelShaderParameterRemaps.AddRange(PixelShaderParameterRemaps);
                shader.HullShaderParameterRemaps.AddRange(HullShaderParameterRemaps);
                shader.DomainShaderParameterRemaps.AddRange(DomainShaderParameterRemaps);
                return shader;
            }
        }

        public class BuildReport
        {
            [JsonProperty("version")] public int Version = FormatVersion;
            [JsonProperty("builtUtc")] public string BuiltUtc;
            [JsonProperty("manifestSet")] public string ManifestSet;
            [JsonProperty("levelsHarvested")] public List<string> LevelsHarvested = new List<string>();
            [JsonProperty("levelsSkipped")] public List<string> LevelsSkipped = new List<string>();
            [JsonProperty("familyMaskCounts")] public Dictionary<string, int> FamilyMaskCounts = new Dictionary<string, int>();
            [JsonProperty("maskCollisions")] public int MaskCollisions; //same (family, mask) with different bytecode across levels
        }

        private readonly string _gameRoot;
        private readonly Dictionary<SHADER_LIST, Dictionary<long, Entry>> _families = new Dictionary<SHADER_LIST, Dictionary<long, Entry>>();
        private readonly HashSet<SHADER_LIST> _loadAttempted = new HashSet<SHADER_LIST>();

        public ShaderDatabase(string gameRoot)
        {
            _gameRoot = gameRoot;
        }

        public static string DatabaseDir(string gameRoot) { return Path.Combine(gameRoot, "DATA", "MODTOOLS", "SHADERS"); }
        public static string IndexFile(string gameRoot) { return Path.Combine(DatabaseDir(gameRoot), "index.json"); }
        private static string FamilyFile(string gameRoot, SHADER_LIST family) { return Path.Combine(DatabaseDir(gameRoot), family.ToString() + ".shaderdb"); }

        public static bool IsBuilt(string gameRoot)
        {
            return File.Exists(IndexFile(gameRoot));
        }

        #region AUTO BUILD
        /* The database is only ever read to widen what the material editor can offer, so there is no
         * reason to make anyone ask for it - it builds itself once, on a background thread, the first
         * time OpenCAGE runs against an install that has no database yet. It writes only into
         * DATA/MODTOOLS/SHADERS.
         *
         * Failure is not fatal and is not shouted about: the editor falls back to the permutations
         * the level itself carries, which is what happened before the database existed at all. The
         * state is exposed so the editor can say which of those two worlds it is in. */
        public enum AutoBuildState { NotStarted, Running, Done, Failed }

        private static int _autoBuild = (int)AutoBuildState.NotStarted;
        public static AutoBuildState AutoBuild => (AutoBuildState)System.Threading.Volatile.Read(ref _autoBuild);
        public static string AutoBuildError { get; private set; }
        public static string AutoBuildProgress { get; private set; }

        /// <summary>
        /// Kick off a background build if this install has no database yet. Returns immediately, and
        /// does nothing at all on a second call - including while the first is still running.
        /// </summary>
        /// <param name="progress">Raised on the worker thread, so marshal before touching UI.</param>
        public static void EnsureBuiltInBackground(string gameRoot, Action<string> progress = null, Action onFinished = null)
        {
            if (string.IsNullOrEmpty(gameRoot) || !Directory.Exists(gameRoot))
                return;

            //one attempt per process, whoever gets here first
            if (System.Threading.Interlocked.CompareExchange(
                    ref _autoBuild, (int)AutoBuildState.Running, (int)AutoBuildState.NotStarted) != (int)AutoBuildState.NotStarted)
                return;

            if (IsBuilt(gameRoot))
            {
                System.Threading.Volatile.Write(ref _autoBuild, (int)AutoBuildState.Done);
                return;
            }

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    Build(gameRoot, msg =>
                    {
                        AutoBuildProgress = msg;
                        try { progress?.Invoke(msg); } catch { }
                    });
                    AutoBuildProgress = null;
                    System.Threading.Volatile.Write(ref _autoBuild, (int)AutoBuildState.Done);

                    //anything already holding a stale "no database" answer needs to re-ask
                    ShaderPermutationService.InvalidateDatabase();
                }
                catch (Exception e)
                {
                    AutoBuildError = e.Message;
                    AutoBuildProgress = null;
                    System.Threading.Volatile.Write(ref _autoBuild, (int)AutoBuildState.Failed);
                }
                finally
                {
                    try { onFinished?.Invoke(); } catch { }
                }
            });
        }

        /// <summary>Allow another background attempt after a failure (the editor's retry).</summary>
        public static void ResetAutoBuild()
        {
            if (AutoBuild == AutoBuildState.Failed)
            {
                AutoBuildError = null;
                System.Threading.Volatile.Write(ref _autoBuild, (int)AutoBuildState.NotStarted);
            }
        }
        #endregion

        public static BuildReport ReadReport(string gameRoot)
        {
            try
            {
                if (File.Exists(IndexFile(gameRoot)))
                    return JsonConvert.DeserializeObject<BuildReport>(File.ReadAllText(IndexFile(gameRoot)));
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Which families the harvest holds and how many permutations each has, read from the index
        /// alone - no family file is touched, so this is cheap enough to call when a menu opens.
        /// </summary>
        public static Dictionary<SHADER_LIST, int> HarvestedFamilies(string gameRoot)
        {
            Dictionary<SHADER_LIST, int> result = new Dictionary<SHADER_LIST, int>();
            BuildReport report = ReadReport(gameRoot);
            if (report == null || report.FamilyMaskCounts == null)
                return result;
            foreach (KeyValuePair<string, int> kv in report.FamilyMaskCounts)
            {
                SHADER_LIST family;
                if (!Enum.TryParse(kv.Key, out family)) continue;
                if (!Enum.IsDefined(typeof(SHADER_LIST), family)) continue;
                if (!File.Exists(FamilyFile(gameRoot, family))) continue;
                result[family] = kv.Value;
            }
            return result;
        }

        public bool TryGet(SHADER_LIST family, long mask, out Entry entry)
        {
            Dictionary<long, Entry> entries = LoadFamily(family);
            if (entries == null)
            {
                entry = null;
                return false;
            }
            return entries.TryGetValue(mask, out entry);
        }

        public IEnumerable<Entry> FamilyEntries(SHADER_LIST family)
        {
            Dictionary<long, Entry> entries = LoadFamily(family);
            return entries == null ? (IEnumerable<Entry>)new Entry[0] : entries.Values;
        }

        public HashSet<long> FamilyMasks(SHADER_LIST family)
        {
            Dictionary<long, Entry> entries = LoadFamily(family);
            return entries == null ? new HashSet<long>() : new HashSet<long>(entries.Keys);
        }

        /* Families load lazily: the editor only ever needs the family of the material on screen */
        private Dictionary<long, Entry> LoadFamily(SHADER_LIST family)
        {
            Dictionary<long, Entry> entries;
            if (_families.TryGetValue(family, out entries))
                return entries;
            if (_loadAttempted.Contains(family))
                return null;
            _loadAttempted.Add(family);

            string path = FamilyFile(_gameRoot, family);
            if (!File.Exists(path))
                return null;

            try
            {
                entries = ReadFamilyFile(path, family);
            }
            catch
            {
                return null;
            }
            _families[family] = entries;
            return entries;
        }

        #region FILE_FORMAT
        private const uint Magic = 0x4453434F; //"OCSD"

        private static Dictionary<long, Entry> ReadFamilyFile(string path, SHADER_LIST family)
        {
            Dictionary<long, Entry> entries = new Dictionary<long, Entry>();
            using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
            {
                if (reader.ReadUInt32() != Magic) throw new Exception("Bad magic");
                if (reader.ReadInt32() != FormatVersion) throw new Exception("Bad version");
                if (reader.ReadInt16() != (short)family) throw new Exception("Family mismatch");
                int entryCount = reader.ReadInt32();

                int blobCount = reader.ReadInt32();
                byte[][] blobs = new byte[blobCount][];
                for (int i = 0; i < blobCount; i++)
                    blobs[i] = reader.ReadBytes(reader.ReadInt32());

                for (int i = 0; i < entryCount; i++)
                {
                    Entry entry = new Entry();
                    entry.Mask = reader.ReadInt64();
                    entry.Technique = reader.ReadString();
                    entry.RequirementFlags = reader.ReadInt64();
                    entry.ShaderModel = reader.ReadByte();
                    entry.CycleCount = reader.ReadInt16();
                    entry.RegisterCount = reader.ReadByte();
                    entry.PermutationHash = reader.ReadUInt32();
                    entry.RenderStates = new Shaders.StateBlock(reader);
                    int samplerCount = reader.ReadByte();
                    for (int x = 0; x < samplerCount; x++)
                        entry.Samplers.Add(new Shaders.StateBlock(reader));
                    for (int x = 0; x < samplerCount; x++)
                        entry.SamplerStageBindings.Add(reader.ReadByte());
                    ReadByteList(reader, entry.SamplerRemaps);
                    ReadByteList(reader, entry.EngineParameterRemaps);
                    ReadByteList(reader, entry.VertexShaderParameterRemaps);
                    ReadByteList(reader, entry.PixelShaderParameterRemaps);
                    ReadByteList(reader, entry.HullShaderParameterRemaps);
                    ReadByteList(reader, entry.DomainShaderParameterRemaps);
                    entry.VertexShader = ReadBlobRef(reader, blobs);
                    entry.PixelShader = ReadBlobRef(reader, blobs);
                    entry.HullShader = ReadBlobRef(reader, blobs);
                    entry.DomainShader = ReadBlobRef(reader, blobs);
                    entry.GeometryShader = ReadBlobRef(reader, blobs);
                    entry.ComputeShader = ReadBlobRef(reader, blobs);
                    entries[entry.Mask] = entry;
                }
            }
            return entries;
        }

        private static void ReadByteList(BinaryReader reader, List<int> list)
        {
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
                list.Add(reader.ReadByte());
        }

        private static byte[] ReadBlobRef(BinaryReader reader, byte[][] blobs)
        {
            int index = reader.ReadInt32();
            return index == -1 ? null : blobs[index];
        }

        private static void WriteFamilyFile(string path, SHADER_LIST family, List<Entry> entries)
        {
            //Content-dedupe the blobs: many masks share bytecode
            List<byte[]> blobs = new List<byte[]>();
            Dictionary<string, int> blobIndex = new Dictionary<string, int>();
            int IndexOfBlob(byte[] blob)
            {
                if (blob == null) return -1;
                string key = ModToolkit.ToHex(ModToolkit.Sha256(blob));
                int index;
                if (blobIndex.TryGetValue(key, out index)) return index;
                index = blobs.Count;
                blobs.Add(blob);
                blobIndex[key] = index;
                return index;
            }
            foreach (Entry entry in entries)
            {
                IndexOfBlob(entry.VertexShader);
                IndexOfBlob(entry.PixelShader);
                IndexOfBlob(entry.HullShader);
                IndexOfBlob(entry.DomainShader);
                IndexOfBlob(entry.GeometryShader);
                IndexOfBlob(entry.ComputeShader);
            }

            using (BinaryWriter writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(Magic);
                writer.Write(FormatVersion);
                writer.Write((short)family);
                writer.Write(entries.Count);
                writer.Write(blobs.Count);
                for (int i = 0; i < blobs.Count; i++)
                {
                    writer.Write(blobs[i].Length);
                    writer.Write(blobs[i]);
                }
                foreach (Entry entry in entries)
                {
                    writer.Write(entry.Mask);
                    writer.Write(entry.Technique ?? "");
                    writer.Write(entry.RequirementFlags);
                    writer.Write(entry.ShaderModel);
                    writer.Write((short)entry.CycleCount);
                    writer.Write((byte)entry.RegisterCount);
                    writer.Write(entry.PermutationHash);
                    entry.RenderStates.Write(writer);
                    writer.Write((byte)entry.Samplers.Count);
                    for (int x = 0; x < entry.Samplers.Count; x++)
                        entry.Samplers[x].Write(writer);
                    for (int x = 0; x < entry.Samplers.Count; x++)
                        writer.Write((byte)entry.SamplerStageBindings[x]);
                    WriteByteList(writer, entry.SamplerRemaps);
                    WriteByteList(writer, entry.EngineParameterRemaps);
                    WriteByteList(writer, entry.VertexShaderParameterRemaps);
                    WriteByteList(writer, entry.PixelShaderParameterRemaps);
                    WriteByteList(writer, entry.HullShaderParameterRemaps);
                    WriteByteList(writer, entry.DomainShaderParameterRemaps);
                    writer.Write(IndexOfBlob(entry.VertexShader));
                    writer.Write(IndexOfBlob(entry.PixelShader));
                    writer.Write(IndexOfBlob(entry.HullShader));
                    writer.Write(IndexOfBlob(entry.DomainShader));
                    writer.Write(IndexOfBlob(entry.GeometryShader));
                    writer.Write(IndexOfBlob(entry.ComputeShader));
                }
            }
        }

        private static void WriteByteList(BinaryWriter writer, List<int> list)
        {
            writer.Write(list.Count);
            for (int i = 0; i < list.Count; i++)
                writer.Write((byte)list[i]);
        }
        #endregion

        #region BUILD
        /// <summary>
        /// Scan every level's shader paks (pristine ones only) and write the database. Safe to
        /// re-run; replaces any previous build. Reports progress as human-readable lines.
        /// </summary>
        public static BuildReport Build(string gameRoot, Action<string> progress = null)
        {
            VanillaManifest manifest = new VanillaManifest();
            HashCache hashCache = new HashCache(gameRoot);

            BuildReport report = new BuildReport()
            {
                BuiltUtc = DateTime.UtcNow.ToString("o"),
                ManifestSet = manifest.Platform.ToString(),
            };

            Dictionary<SHADER_LIST, Dictionary<long, Entry>> families = new Dictionary<SHADER_LIST, Dictionary<long, Entry>>();

            List<string> levelDirs = FindLevelDirs(gameRoot);
            for (int i = 0; i < levelDirs.Count; i++)
            {
                string levelDir = levelDirs[i];
                string levelName = LevelDisplayName(gameRoot, levelDir);
                progress?.Invoke("Scanning " + levelName + " (" + (i + 1) + "/" + levelDirs.Count + ")...");

                string metaPak = Path.Combine(levelDir, "RENDERABLE", "LEVEL_SHADERS_DX11.PAK");
                string binPak = Path.Combine(levelDir, "RENDERABLE", "LEVEL_SHADERS_DX11_BIN.PAK");
                string remapPak = Path.Combine(levelDir, "RENDERABLE", "LEVEL_SHADERS_DX11_IDX_REMAP.PAK");
                if (!File.Exists(metaPak) || !File.Exists(binPak))
                {
                    report.LevelsSkipped.Add(levelName + " (no shader paks)");
                    continue;
                }

                //Only harvest byte-verified vanilla paks: a modified pak may hold entries whose
                //mask no longer matches its bytecode
                if (!FileIsVanilla(gameRoot, manifest, hashCache, metaPak) ||
                    !FileIsVanilla(gameRoot, manifest, hashCache, binPak) ||
                    !FileIsVanilla(gameRoot, manifest, hashCache, remapPak))
                {
                    report.LevelsSkipped.Add(levelName + " (modified or unrecognised shader paks)");
                    continue;
                }

                Shaders shaders;
                try
                {
                    shaders = new Shaders(metaPak);
                }
                catch
                {
                    report.LevelsSkipped.Add(levelName + " (failed to parse)");
                    continue;
                }

                foreach (Shaders.Shader shader in shaders.Entries)
                {
                    if (shader == null || shader.Ubershader == SHADER_LIST.BESPOKE_SHADER)
                        continue;

                    Dictionary<long, Entry> familyEntries;
                    if (!families.TryGetValue(shader.Ubershader, out familyEntries))
                    {
                        familyEntries = new Dictionary<long, Entry>();
                        families[shader.Ubershader] = familyEntries;
                    }

                    Entry existing;
                    if (familyEntries.TryGetValue(shader.UbershaderFeatureFlags, out existing))
                    {
                        //First-seen wins; count the rare per-level bytecode variants (6 known game-wide)
                        if (!BlobsEqual(existing.PixelShader, shader.PixelShader) || !BlobsEqual(existing.VertexShader, shader.VertexShader))
                            report.MaskCollisions++;
                        continue;
                    }

                    familyEntries[shader.UbershaderFeatureFlags] = HarvestEntry(shader);
                }

                report.LevelsHarvested.Add(levelName);
            }
            hashCache.Save();

            progress?.Invoke("Writing database...");
            Directory.CreateDirectory(DatabaseDir(gameRoot));
            foreach (KeyValuePair<SHADER_LIST, Dictionary<long, Entry>> family in families)
            {
                List<Entry> ordered = family.Value.Values.OrderBy(o => o.Mask).ToList();
                WriteFamilyFile(FamilyFile(gameRoot, family.Key), family.Key, ordered);
                report.FamilyMaskCounts[family.Key.ToString()] = ordered.Count;
            }
            File.WriteAllText(IndexFile(gameRoot), JsonConvert.SerializeObject(report, Formatting.Indented));
            return report;
        }

        private static bool FileIsVanilla(string gameRoot, VanillaManifest manifest, HashCache hashCache, string fullPath)
        {
            if (!File.Exists(fullPath))
                return false;
            string normalised = ModToolkit.NormaliseFull(gameRoot, fullPath);
            if (normalised == null)
                return false;
            byte[] hash = hashCache.Hash(normalised);
            return hash != null && manifest.IsVanilla(normalised, hash);
        }

        private static List<string> FindLevelDirs(string gameRoot)
        {
            List<string> dirs = new List<string>();
            string production = Path.Combine(gameRoot, "DATA", "ENV", "PRODUCTION");
            if (!Directory.Exists(production))
                return dirs;
            foreach (string dir in Directory.GetDirectories(production))
            {
                if (Path.GetFileName(dir).ToUpper() == "DLC")
                {
                    foreach (string dlcDir in Directory.GetDirectories(dir))
                        dirs.Add(dlcDir);
                    continue;
                }
                dirs.Add(dir);
            }
            return dirs;
        }

        private static string LevelDisplayName(string gameRoot, string levelDir)
        {
            string production = Path.Combine(gameRoot, "DATA", "ENV", "PRODUCTION");
            string full = Path.GetFullPath(levelDir);
            string root = Path.GetFullPath(production);
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return full.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
            return Path.GetFileName(levelDir);
        }

        /* Clone everything out of the loaded pak: CathodeLib's Shaders finalizer nulls the blob
         * references on its entries, so harvested bytes must be our own copies */
        private static Entry HarvestEntry(Shaders.Shader shader)
        {
            Entry entry = new Entry()
            {
                Mask = shader.UbershaderFeatureFlags,
                Technique = shader.Technique,
                RequirementFlags = shader.UbershaderRequirementFlags,
                ShaderModel = (byte)shader.RequiredShaderModel,
                CycleCount = shader.CycleCount,
                RegisterCount = shader.RegisterCount,
                PermutationHash = shader.PermutationHash,
                RenderStates = shader.RenderStates.Copy(),
                VertexShader = (byte[])shader.VertexShader?.Clone(),
                PixelShader = (byte[])shader.PixelShader?.Clone(),
                HullShader = (byte[])shader.HullShader?.Clone(),
                DomainShader = (byte[])shader.DomainShader?.Clone(),
                GeometryShader = (byte[])shader.GeometryShader?.Clone(),
                ComputeShader = (byte[])shader.ComputeShader?.Clone(),
            };
            foreach (Shaders.StateBlock sampler in shader.Samplers)
                entry.Samplers.Add(sampler.Copy());
            entry.SamplerStageBindings.AddRange(shader.SamplerStageBindings);
            entry.SamplerRemaps.AddRange(shader.SamplerRemaps);
            entry.EngineParameterRemaps.AddRange(shader.EngineParameterRemaps);
            entry.VertexShaderParameterRemaps.AddRange(shader.VertexShaderParameterRemaps);
            entry.PixelShaderParameterRemaps.AddRange(shader.PixelShaderParameterRemaps);
            entry.HullShaderParameterRemaps.AddRange(shader.HullShaderParameterRemaps);
            entry.DomainShaderParameterRemaps.AddRange(shader.DomainShaderParameterRemaps);
            return entry;
        }

        private static bool BlobsEqual(byte[] a, byte[] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
        #endregion
    }
}

using CATHODE;
using CathodeLib;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using Newtonsoft.Json.Linq;
using OpenCAGE.MCP;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenCAGE
{
    /// <summary>
    /// The level's streamed textures against the game's streaming texture heap (DATA\ENGINE_SETTINGS.XML "Streaming Texture Heap Size",
    /// 300 MB as shipped). What doesn't fit draws from its 128 px resident copy ("blocky textures"). A zone's textures are freed when it
    /// unloads (nothing in a single-zone level), so what must fit is the always-loaded set plus the current zone. Each texture counts as
    /// its stored streamed part x 1.19 (the game's own streamable_bytes figure); textures no mover draws count everywhere, to be safe.
    /// </summary>
    public static class TextureBudget
    {
        /// <summary>Stored streamed bytes to the game's streamable_bytes.</summary>
        public const double GameFactor = 1.19;
        public const int RetailHeapMB = 300;
        /// <summary>The tested raise: 768 takes the 32-bit game past 2 GiB of its own memory.</summary>
        public const int RaisedHeapMB = 512;

        public sealed class Zone
        {
            public string Name;
            public ShortGuid Id;
            /// <summary>The game's estimate for this zone with the always-loaded set: bytes.</summary>
            public long Bytes;
            public int Textures;
        }

        public sealed class Item
        {
            public Textures.TEX4 Texture;
            public long Bytes;
            public string Where;
        }

        public sealed class Estimate
        {
            /// <summary>Every level texture's streamed part, as the game counts it.</summary>
            public long LevelBytes;
            /// <summary>What is drawn everywhere: textures of unzoned / global-zone movers.</summary>
            public long AlwaysBytes;
            /// <summary>Textures no placed model draws but something can (characters, weapons, spawned assets): counted in the peak of a one-zone level only.</summary>
            public long UnplacedBytes;
            /// <summary>Textures nothing in the level draws (no renderable element's material uses them): never streamed, not counted.</summary>
            public long UnusedBytes;
            /// <summary>The most that has to fit at once: the always-loaded set plus the biggest zone (or the whole level, with no zones).</summary>
            public long PeakBytes;
            public string PeakWhere;
            public List<Zone> Zones = new List<Zone>();
            /// <summary>The biggest textures in the peak set.</summary>
            public List<Item> Top = new List<Item>();
            public int LevelTextures;
            public int? HeapDefaultMB, HeapUltraMB;
            public string SettingsPath;

            /// <summary>The heap the game uses on a PC with 2.2 GB free (any PC that runs it): Ultra's, else Default's.</summary>
            public int HeapMB => HeapUltraMB ?? HeapDefaultMB ?? RetailHeapMB;
            public double PeakMB => PeakBytes / 1048576.0;

            public JObject ToJson(int top = 10) => new JObject()
            {
                ["level_streamed_mb"] = Math.Round(LevelBytes / 1048576.0, 1),
                ["always_loaded_mb"] = Math.Round(AlwaysBytes / 1048576.0, 1),
                ["not_drawn_by_placed_models_mb"] = Math.Round(UnplacedBytes / 1048576.0, 1),
                ["unused_mb"] = Math.Round(UnusedBytes / 1048576.0, 1),
                ["peak_mb"] = Math.Round(PeakMB, 1),
                ["peak_where"] = PeakWhere,
                ["heap_mb"] = HeapMB,
                ["heap_default_mb"] = HeapDefaultMB,
                ["heap_ultra_mb"] = HeapUltraMB,
                ["level_textures"] = LevelTextures,
                ["zones"] = new JArray(Zones.OrderByDescending(o => o.Bytes).Take(20).Select(o => new JObject() { ["zone"] = o.Name, ["mb"] = Math.Round(o.Bytes / 1048576.0, 1), ["textures"] = o.Textures })),
                ["top"] = new JArray(Top.Take(top).Select(o => new JObject()
                {
                    ["texture"] = o.Texture.Name,
                    ["mb"] = Math.Round(o.Bytes / 1048576.0, 2),
                    ["format"] = o.Texture.Format.ToString(),
                    ["size"] = (o.Texture.TextureStreamed?.Width ?? 0) + "x" + (o.Texture.TextureStreamed?.Height ?? 0),
                    ["where"] = o.Where,
                })),
            };
        }

        private static long Streamed(Textures.TEX4 texture) => (long)Math.Round((texture?.TextureStreamed?.Content?.Length ?? 0) * GameFactor);

        private static bool AlwaysLoadedZone(ShortGuid zone) => zone == ShortGuid.Invalid || zone == new ShortGuid("01-00-00-00");

        /// <summary>Work out the estimate for a loaded level (any thread; the level is only read).</summary>
        public static Estimate Of(Level level, Func<ShortGuid, string> zoneName = null)
        {
            Estimate estimate = new Estimate();
            List<Textures.TEX4> all = level.Textures?.Entries?.Where(o => o != null).ToList() ?? new List<Textures.TEX4>();
            estimate.LevelTextures = all.Count;
            estimate.LevelBytes = all.Sum(Streamed);
            HashSet<Textures.TEX4> levelTextures = new HashSet<Textures.TEX4>(all);

            //Each level texture's zones, through the movers that draw it
            Dictionary<Textures.TEX4, HashSet<ShortGuid>> zonesOf = new Dictionary<Textures.TEX4, HashSet<ShortGuid>>();
            Dictionary<Textures.TEX4, string> firstMaterial = new Dictionary<Textures.TEX4, string>();
            foreach (Movers.MOVER_DESCRIPTOR mover in level.Movers?.Entries ?? new List<Movers.MOVER_DESCRIPTOR>())
            {
                foreach (RenderableElements.Element element in mover.RenderableElements)
                {
                    Materials.Material material = element?.Material;
                    if (material?.TextureReferences == null) continue;
                    foreach (TexturePtr pointer in material.TextureReferences)
                    {
                        Textures.TEX4 texture = pointer?.Texture;
                        if (texture == null || !levelTextures.Contains(texture)) continue;
                        if (!zonesOf.TryGetValue(texture, out HashSet<ShortGuid> zones)) zonesOf[texture] = zones = new HashSet<ShortGuid>();
                        zones.Add(mover.PrimaryZoneID);
                        if (mover.SecondaryZoneID != ShortGuid.Invalid) zones.Add(mover.SecondaryZoneID);
                        if (!firstMaterial.ContainsKey(texture)) firstMaterial[texture] = material.Name;
                    }
                }
            }

            //Drawn everywhere: by an unzoned / global-zone mover. Drawn by no mover: a character's, a weapon's, an effect's - or nothing's
            HashSet<Textures.TEX4> always = new HashSet<Textures.TEX4>(all.Where(o => zonesOf.TryGetValue(o, out HashSet<ShortGuid> zones) && zones.Any(AlwaysLoadedZone)));
            //What anything can draw: a material in the level's renderable elements (movers, and the resources characters, weapons and
            //spawned assets draw with). A texture no material there uses is never drawn, so never streamed
            HashSet<Textures.TEX4> drawable = new HashSet<Textures.TEX4>();
            foreach (RenderableElements.Element element in level.RenderableElements?.Entries ?? new List<RenderableElements.Element>())
                if (element?.Material?.TextureReferences != null)
                    foreach (TexturePtr pointer in element.Material.TextureReferences)
                        if (pointer?.Texture != null && levelTextures.Contains(pointer.Texture)) drawable.Add(pointer.Texture);
            HashSet<Textures.TEX4> unplaced = new HashSet<Textures.TEX4>(all.Where(o => !zonesOf.ContainsKey(o) && drawable.Contains(o)));
            estimate.AlwaysBytes = always.Sum(Streamed);
            estimate.UnplacedBytes = unplaced.Sum(Streamed);
            estimate.UnusedBytes = all.Where(o => !zonesOf.ContainsKey(o) && !drawable.Contains(o)).Sum(Streamed);
            Dictionary<ShortGuid, HashSet<Textures.TEX4>> byZone = new Dictionary<ShortGuid, HashSet<Textures.TEX4>>();
            foreach (KeyValuePair<Textures.TEX4, HashSet<ShortGuid>> entry in zonesOf)
            {
                if (always.Contains(entry.Key)) continue;
                foreach (ShortGuid zone in entry.Value)
                {
                    if (!byZone.TryGetValue(zone, out HashSet<Textures.TEX4> set)) byZone[zone] = set = new HashSet<Textures.TEX4>();
                    set.Add(entry.Key);
                }
            }
            foreach (KeyValuePair<ShortGuid, HashSet<Textures.TEX4>> zone in byZone)
                estimate.Zones.Add(new Zone() { Id = zone.Key, Name = zoneName?.Invoke(zone.Key) ?? zone.Key.ToByteString(), Bytes = estimate.AlwaysBytes + zone.Value.Sum(Streamed), Textures = always.Count + zone.Value.Count });

            /* A zoned level releases a zone's textures as it unloads, and what no placed model draws only streams in while drawn, so the
               peak is the always-loaded set plus the biggest zone. A level with one zone releases nothing, so everything has to fit */
            Zone biggest = estimate.Zones.OrderByDescending(o => o.Bytes).FirstOrDefault();
            HashSet<Textures.TEX4> peak = new HashSet<Textures.TEX4>(always);
            if (biggest != null)
            {
                estimate.PeakBytes = biggest.Bytes;
                estimate.PeakWhere = "in zone " + biggest.Name + " (with what is drawn everywhere)";
                peak.UnionWith(byZone[biggest.Id]);
            }
            else
            {
                estimate.PeakBytes = estimate.AlwaysBytes + estimate.UnplacedBytes;
                estimate.PeakWhere = "everywhere (the level has one zone, so nothing is ever released: all its textures count, characters' and effects' too)";
                peak.UnionWith(unplaced);
            }
            estimate.Top = peak.OrderByDescending(Streamed).Take(25).Select(o => new Item()
            {
                Texture = o,
                Bytes = Streamed(o),
                Where = firstMaterial.TryGetValue(o, out string material) ? "material " + material : "no placed model draws it (a character, weapon or spawned asset can)",
            }).ToList();

            ReadHeap(estimate);
            return estimate;
        }

        /// <summary>
        /// After an import that generated textures (MCP import_model): the level's estimate in <paramref name="result"/>
        /// ("texture_memory"), and a note when the textures that stream at once are over or near the heap. Never throws.
        /// </summary>
        public static void NoteAfterImport(Level level, JObject result, List<string> notes)
        {
            try
            {
                Estimate estimate = Of(level);
                int heap = estimate.HeapMB;
                result["texture_memory"] = new JObject() { ["peak_mb"] = Math.Round(estimate.PeakMB, 1), ["heap_mb"] = heap, ["peak_where"] = estimate.PeakWhere };
                if (estimate.PeakMB > heap * 0.85)
                    notes.Add("Texture memory: the level now needs about " + estimate.PeakMB.ToString("0") + " MB of the game's " + heap + " MB texture heap at once (" + estimate.PeakWhere + ")" +
                              (estimate.PeakMB > heap ? ": textures past it draw blocky" : ": close to full") + ". Import big texture sets with generate.compact_textures (half the memory), " +
                              "or see check_level {checks: ['texture_budget']} for the biggest; set_texture_heap raises the heap (global).");
            }
            catch (Exception e) { Debug.Log("LevelCheck", "Could not estimate the texture memory after an import: " + e.Message); }
        }

        #region The heap (DATA\ENGINE_SETTINGS.XML, global)
        private static readonly Regex HeapBlock = new Regex("<Setting name=\"Streaming Texture Heap Size\">[\\s\\S]*?</Setting>");
        private static readonly Regex HeapQuality = new Regex("(<Quality name=\"(Default|Ultra)\"\\s+int=\")(\\d+)(\")");

        public static string SettingsPath(string gameFolder) => Path.Combine(gameFolder ?? "", "DATA", "ENGINE_SETTINGS.XML");

        private static void ReadHeap(Estimate estimate)
        {
            try
            {
                estimate.SettingsPath = SettingsPath(Singleton.PathToAI);
                if (!File.Exists(estimate.SettingsPath)) return;
                (int? def, int? ultra) = ReadHeap(File.ReadAllText(estimate.SettingsPath));
                estimate.HeapDefaultMB = def;
                estimate.HeapUltraMB = ultra;
            }
            catch (Exception e) { Debug.Log("LevelCheck", "Could not read the texture heap size: " + e.Message); }
        }

        public static (int? def, int? ultra) ReadHeap(string xml)
        {
            Match block = HeapBlock.Match(xml ?? "");
            if (!block.Success) return (null, null);
            int? def = null, ultra = null;
            foreach (Match quality in HeapQuality.Matches(block.Value))
            {
                int mb = int.Parse(quality.Groups[3].Value);
                if (quality.Groups[2].Value == "Default") def = mb; else ultra = mb;
            }
            return (def, ultra);
        }

        /// <summary>
        /// Set the streaming texture heap (Default and Ultra) in the game's DATA\ENGINE_SETTINGS.XML - GLOBAL: every level, and every
        /// player of a mod exported with its config changes. Only the two numbers change (the file's layout is kept: the Mod Manager
        /// merges it value by value). Returns what it did; throws when the file can't be read or written.
        /// </summary>
        public static string SetHeap(string gameFolder, int mb)
        {
            if (mb < 64 || mb > 1024) throw new ArgumentOutOfRangeException(nameof(mb), "Choose 64 to 1024 MB (512 is the tested raise; 768 takes the 32-bit game past 2 GiB of memory).");
            string path = SettingsPath(gameFolder);
            if (!File.Exists(path)) throw new FileNotFoundException("The game's ENGINE_SETTINGS.XML is missing: " + path);
            byte[] raw = File.ReadAllBytes(path);
            bool bom = raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF;
            string text = new UTF8Encoding(false).GetString(raw, bom ? 3 : 0, raw.Length - (bom ? 3 : 0));
            Match block = HeapBlock.Match(text);
            if (!block.Success) throw new InvalidDataException("ENGINE_SETTINGS.XML has no Streaming Texture Heap Size setting: nothing was changed.");
            (int? before, int? beforeUltra) = ReadHeap(text);
            string replaced = HeapQuality.Replace(block.Value, m => m.Groups[1].Value + mb + m.Groups[4].Value);
            if (replaced == block.Value)
                return "The streaming texture heap is already " + mb + " MB.";
            text = text.Substring(0, block.Index) + replaced + text.Substring(block.Index + block.Length);
            File.WriteAllBytes(path, (bom ? new byte[] { 0xEF, 0xBB, 0xBF } : new byte[0]).Concat(new UTF8Encoding(false).GetBytes(text)).ToArray());
            return "Streaming Texture Heap Size set to " + mb + " MB (was " + (beforeUltra ?? before)?.ToString() + " MB) in " + path + ". This is GLOBAL: every level uses it, " +
                   "and a mod exported with its config changes carries it to its players. Reset Configs > Graphics puts the shipped value back.";
        }
        #endregion

        /// <summary>Zone ids to names: each placed Zone entity's id (its instance path's zone id) to its name.</summary>
        internal static Func<ShortGuid, string> ZoneNames(LevelCheckContext context)
        {
            Dictionary<ShortGuid, string> names = new Dictionary<ShortGuid, string>();
            foreach (McpPlacements.Placement placement in context.Placements(FunctionType.Zone))
            {
                try
                {
                    ShortGuid id = new EntityPath(placement.Chain.Select(o => o.shortGUID).Concat(new[] { ShortGuid.Invalid }).ToArray()).GenerateZoneID();
                    if (!names.ContainsKey(id))
                        names[id] = context.EntityName(context.HolderOf(placement), placement.Chain[placement.Chain.Count - 1]);
                }
                catch { }
            }
            return id => names.TryGetValue(id, out string name) ? name : null;
        }
    }

    /// <summary>The level's streamed textures against the heap, with the top offenders, and a one-click (global) heap raise.</summary>
    internal sealed class TextureBudgetRule : LevelCheckRule
    {
        public override string Id => "texture_budget";
        public override string Name => "Texture memory";
        public override string About => "Estimates the streamed textures that have to fit in the game's texture heap at once (per zone); past it, textures draw blurry/blocky from their small copies.";
        public override IEnumerable<FunctionType> Walks => new[] { FunctionType.Zone };

        public override void Run(LevelCheckContext context, List<LevelCheckFinding> found)
        {
            TextureBudget.Estimate estimate = TextureBudget.Of(context.Level, TextureBudget.ZoneNames(context));
            context.Report.Figures["textures"] = estimate.ToJson();
            int heap = estimate.HeapMB;
            double peak = estimate.PeakMB;
            //Over the heap: textures draw from their resident copies. Within 15%: fragmentation and what nothing placed draws
            //(characters, effects) can take the rest, so warn
            if (peak <= heap * 0.85) return;
            bool over = peak > heap;
            string top = string.Join(", ", estimate.Top.Take(5).Select(o => o.Texture.Name + " " + (o.Bytes / 1048576.0).ToString("0.0") + " MB " + o.Texture.Format));
            LevelCheckFinding finding = new LevelCheckFinding()
            {
                Severity = over ? LevelCheckSeverity.Broken : LevelCheckSeverity.Warning,
                Title = (estimate.Zones.Count == 0 ? "The level's textures can need up to about " + peak.ToString("0") + " MB at once (it has one zone, so none are released)" : "About " + peak.ToString("0") + " MB of textures stream at once " + estimate.PeakWhere) + ", " + (over ? "more than" : "close to") + " the game's " + heap + " MB texture heap",
                Text = (over ? "Textures that don't fit are drawn from their small 128 px copies: blurry, blocky surfaces. " : "Little is left for fragmentation and for what no placed model draws (characters, weapons, effects), so some textures may draw blocky. ") +
                       "The biggest: " + top + ". Import big texture sets with Compact Textures (DXT1 instead of BC7: half the memory), use smaller images, split the level into zones, " +
                       (heap < TextureBudget.RaisedHeapMB ? "or raise the heap to " + TextureBudget.RaisedHeapMB + " MB (global: every level and every player of your mod)." : "or cut the textures down - " + heap + " MB is already as far as the 32-bit game's memory allows (768 took it past 2 GiB)."),
            };
            foreach (TextureBudget.Item item in estimate.Top.Take(10))
                finding.Sources.Add(new LevelCheckSource() { Text = item.Texture.Name + ": " + (item.Bytes / 1048576.0).ToString("0.0") + " MB, " + item.Texture.Format + " " + (item.Texture.TextureStreamed?.Width ?? 0) + "x" + (item.Texture.TextureStreamed?.Height ?? 0) + ", " + item.Where });
            if (heap < TextureBudget.RaisedHeapMB)
            {
                finding.Fix = new LevelCheckFix()
                {
                    Global = true,
                    Label = "Raise the texture heap to " + TextureBudget.RaisedHeapMB + " MB (GLOBAL)",
                    Detail = "Sets Streaming Texture Heap Size (Default and Ultra) to " + TextureBudget.RaisedHeapMB + " MB in the game's DATA\\ENGINE_SETTINGS.XML. This is not part of the level: " +
                             "every level uses it, and a mod exported with its config changes carries it to everyone who plays it. 512 MB is tested; Reset Configs > Graphics puts 300 back.",
                    Apply = fromTool => TextureBudget.SetHeap(Singleton.PathToAI, TextureBudget.RaisedHeapMB),
                };
            }
            found.Add(finding);
        }
    }
}

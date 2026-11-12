using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.MCP;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE
{
    /// <summary>
    /// Level Check: how many streamed textures the level can ask for at once, against the game's ~1,020 (TextureStreaming: the texture
    /// heap's 1,024 block handles, shared with the gaps between textures). Over it, textures stay blurry, for the whole level when it has
    /// one zone. Byte limits are texture_budget's. Broken past <see cref="TextureStreaming.SafeStreamedAtOnce"/>, Warning past
    /// <see cref="WarnAt"/> (GLOBAL textures drawn by the player, weapons and effects also take handles). Zoned levels count the busiest
    /// zone with its linked zones (the game streams a linked zone in while the link is open) plus what is drawn everywhere.
    /// </summary>
    internal sealed class TextureSlotsRule : LevelCheckRule
    {
        public const int WarnAt = 850;

        public override string Id => "texture_slots";
        public override string Name => "Texture streaming slots";
        public override string About => "Counts the streamed textures that can be asked for at once (per zone with its linked zones; everything in a one-zone level); the game holds about 1,020 at full size, whatever the heap's size, and the rest stay blurry.";
        public override IEnumerable<FunctionType> Walks => new[] { FunctionType.Zone };

        public override void Run(LevelCheckContext context, List<LevelCheckFinding> found)
        {
            TextureStreaming.SlotEstimate e = TextureStreaming.Estimate(context.Level, TextureBudget.ZoneNames(context), false, ZoneNeighbours(context));
            context.Report.Figures["texture_slots"] = new JObject()
            {
                ["streamed"] = e.Streamed,
                ["kept_loaded"] = e.KeptLoaded,
                ["peak_at_once"] = e.Peak,
                ["peak_where"] = e.PeakWhere,
                ["game_slots"] = TextureStreaming.StreamerBlocks,
                ["broken_over"] = TextureStreaming.SafeStreamedAtOnce,
                ["warn_over"] = WarnAt,
                ["small_256"] = e.Small,
                ["small_256_mb"] = Math.Round(e.SmallBytes / 1048576.0, 1),
                ["zones"] = new JArray(e.Zones.Take(10).Select(o => new JObject() { ["zone"] = o.zone, ["textures"] = o.textures, ["with_linked_zones"] = o.withNeighbours })),
            };
            if (e.Peak <= WarnAt) return;
            bool over = e.Peak > TextureStreaming.SafeStreamedAtOnce;
            LevelCheckFinding finding = new LevelCheckFinding()
            {
                Severity = over ? LevelCheckSeverity.Broken : LevelCheckSeverity.Warning,
                Title = e.Peak + " streamed textures can be asked for at once " + e.PeakWhere + (over ? ", more than" : ", close to") + " the about " + (TextureStreaming.StreamerBlocks - 4) + " the game can hold",
                Text = (over ? "The game streams at most about " + (TextureStreaming.StreamerBlocks - 4) + " textures at full size at once, however much of its texture heap is free; the rest are drawn from their small resident copies, blurry" + (e.OneZone ? " - for the whole level, as a level with one zone never lets any go. " : " while that zone is in. ")
                             : "Textures the player, weapons and effects draw from the global set take some of the game's ~" + (TextureStreaming.StreamerBlocks - 4) + " streaming slots too, so some textures may stay blurry. ") +
                       "Keep small textures loaded (Texture Editor > Texture Streaming, or the fix below: the " + e.Small + " streamed textures of " + TextureStreaming.DefaultKeepLoadedEdge + " px or less, " +
                       (e.SmallBytes / 1048576.0).ToString("0") + " MB kept in memory), keep a sky or other always-seen texture loaded, merge identical textures, or " + (e.OneZone ? "split the level into zones." : "split the zone."),
            };
            foreach ((string zone, int textures, int withNeighbours) in e.Zones.Take(6))
                finding.Sources.Add(new LevelCheckSource() { Text = "zone " + zone + ": " + textures + " streamed textures, " + withNeighbours + " with the zones linked to it" });
            if (e.OneZone)
                finding.Sources.Add(new LevelCheckSource() { Text = e.Always + " drawn by placed models, " + e.Unplaced + " by characters, weapons or effects (not placed)" });
            if (e.Small > 0)
            {
                finding.Fix = new LevelCheckFix()
                {
                    Label = "Keep the " + e.Small + " textures of " + TextureStreaming.DefaultKeepLoadedEdge + " px or less loaded (+" + (e.SmallBytes / 1048576.0).ToString("0") + " MB)",
                    Detail = "Moves each one's full image into its persistent part, the way the game stores its effect textures: they never stream and never draw blurry, and free " + e.Small +
                             " streaming slots; their memory stays taken for the whole level. One undo step; Save & Build takes it into the game.",
                    Apply = fromTool =>
                    {
                        Level level = context.Level;
                        List<Textures.TEX4> small = TextureStreaming.Select(level.Textures.Entries, null, null, TextureStreaming.DefaultKeepLoadedEdge, true);
                        int done = TextureStreaming.SetKeptLoaded(level, small, true, (fromTool ? "AI: " : "") + "Level Check: keep " + small.Count + " small textures loaded", out List<string> problems);
                        return done + " textures kept loaded" + (problems.Count != 0 ? " (" + problems.Count + " could not be)" : "") + ". Save & Build to take it into the game (Undo puts it back).";
                    },
                };
            }
            found.Add(finding);
        }

        /// <summary>The estimate with the zones' names and links read from the placed script (for the MCP report and the Texture Streaming window).</summary>
        internal static TextureStreaming.SlotEstimate EstimateWithZones(Level level, bool duplicates)
        {
            Func<ShortGuid, string> names = null;
            Func<ShortGuid, IEnumerable<ShortGuid>> neighbours = null;
            try
            {
                LevelCheckContext context = new LevelCheckContext(level, new LevelCheckReport() { Level = level.Name });
                context.Walk(new[] { FunctionType.Zone });
                names = TextureBudget.ZoneNames(context);
                neighbours = ZoneNeighbours(context);
            }
            catch (Exception e) { Debug.Log("TextureStreaming", "Reading the level's zones failed: " + e.Message); }
            return TextureStreaming.Estimate(level, names, duplicates, neighbours);
        }

        /// <summary>The zones each zone's ZoneLinks / ZoneExclusionLinks join it to, by zone id (as movers carry it).</summary>
        internal static Func<ShortGuid, IEnumerable<ShortGuid>> ZoneNeighbours(LevelCheckContext context)
        {
            Commands commands = context.Level.Commands;
            //Each placed zone's id, by the composite holding it, the instance path above it and the zone entity
            Dictionary<(Composite holder, string above, Entity zone), ShortGuid> ids = new Dictionary<(Composite, string, Entity), ShortGuid>();
            foreach (McpPlacements.Placement placement in context.Placements(FunctionType.Zone))
            {
                try
                {
                    Composite holder = context.HolderOf(placement);
                    if (holder == null) continue;
                    ShortGuid id = new EntityPath(placement.Chain.Select(o => o.shortGUID).Concat(new[] { ShortGuid.Invalid }).ToArray()).GenerateZoneID();
                    string above = string.Join("/", placement.Chain.Take(placement.Chain.Count - 1).Select(o => o.shortGUID.ToByteString()));
                    ids[(holder, above, placement.Chain[placement.Chain.Count - 1])] = id;
                }
                catch { }
            }
            Dictionary<ShortGuid, HashSet<ShortGuid>> near = new Dictionary<ShortGuid, HashSet<ShortGuid>>();
            foreach (IGrouping<(Composite holder, string above), KeyValuePair<(Composite holder, string above, Entity zone), ShortGuid>> group in ids.GroupBy(o => (o.Key.holder, o.Key.above)))
            {
                foreach (FunctionEntity link in group.Key.holder.functions.Where(McpZoneTools.IsZoneLink))
                {
                    (Entity a, Entity b) sides;
                    try { sides = McpZoneTools.SidesOf(commands, group.Key.holder, link); } catch { continue; }
                    if (sides.a == null || sides.b == null) continue;
                    if (!ids.TryGetValue((group.Key.holder, group.Key.above, sides.a), out ShortGuid a) || !ids.TryGetValue((group.Key.holder, group.Key.above, sides.b), out ShortGuid b)) continue;
                    if (!near.TryGetValue(a, out HashSet<ShortGuid> na)) near[a] = na = new HashSet<ShortGuid>();
                    if (!near.TryGetValue(b, out HashSet<ShortGuid> nb)) near[b] = nb = new HashSet<ShortGuid>();
                    na.Add(b);
                    nb.Add(a);
                }
            }
            return zone => near.TryGetValue(zone, out HashSet<ShortGuid> set) ? set : Enumerable.Empty<ShortGuid>();
        }
    }
}

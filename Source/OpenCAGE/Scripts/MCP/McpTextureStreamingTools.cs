using CATHODE;
using CathodeLib;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using AssetKind = CathodeLib.AssetModificationInfoTable.AssetKind;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// Texture streaming by COUNT (Scripts\Textures\TextureStreaming.cs): texture_streaming (how many stream at once against the game's ~1,020),
    /// set_texture_streaming (keep textures loaded / stream them again) and merge_identical_textures. Registered from McpGameDataTools.Tools();
    /// each tool names its own toolset, 'assets'.
    /// </summary>
    internal static class McpTextureStreamingTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "texture_streaming",
                Title = "How many textures stream at once",
                Description = "The open level's streamed textures by COUNT, against the game's texture streaming: it holds at most about " + (TextureStreaming.StreamerBlocks - 4) + " textures " +
                    "at full size at once (1,024 block handles, shared with the gaps between them), however much of the heap is free; past that, textures stay on their small " +
                    "resident copies (blurry) - for good in a level with one zone, which never lets any go. Gives: streamed, kept_loaded, the peak that can be asked for at once " +
                    "(always-drawn + the biggest zone, or everything drawable in a one-zone level) with the busiest zones, how many are " + TextureStreaming.DefaultKeepLoadedEdge + " px or smaller " +
                    "(set_texture_streaming max_edge keeps those loaded) and the memory that costs, and identical copies (merge_identical_textures). The heap's BYTES are " +
                    "check_level's texture_budget. Read only.",
                InputSchema = McpSchema.Object(
                    McpSchema.Integer("max_edge", "Also count the streamed textures at most this many pixels on their longest edge, and what keeping them loaded costs (default " + TextureStreaming.DefaultKeepLoadedEdge + ")."),
                    McpSchema.Boolean("duplicates", "Also look for identical textures (hashes every image: a few seconds on a big level). Default true.")),
                ReadOnly = true,
                Idempotent = true,
                Toolset = "assets",
                Run = Report,
            };

            yield return new McpTool()
            {
                Name = "set_texture_streaming",
                Title = "Keep textures loaded (never streamed), or stream them again",
                Description = "keep_loaded true (default): the chosen textures never stream - their full image (every mip) moves into the persistent part, the way the game " +
                    "stores its effect flipbooks and lens textures, so they need none of the game's ~" + (TextureStreaming.StreamerBlocks - 4) + " streaming slots and are never drawn blurry " +
                    "(a sky, a texture that must always be sharp, or many small ones to free slots). The cost: their full size stays in memory for the whole level (the result " +
                    "says how much; the game is 32-bit - keep it to tens of MB). keep_loaded false streams kept-loaded textures again (a 128 px resident copy is cut from the image). " +
                    "Choose by names (exact, always taken) and/or by max_edge (every streamed texture at most that many px on its longest edge) and/or pattern (a regex on the " +
                    "name; with max_edge, both must match). Cubemaps are left out (the game streams every one). One undo step; Save & Build takes it into the game. " +
                    "Example for a big one-zone level: {names:['my_sky'], max_edge:256}. dry_run lists what would change.",
                InputSchema = McpSchema.Object(
                    McpSchema.Strings("names", "Textures by name (as list_textures / the Texture Editor show them)."),
                    McpSchema.Integer("max_edge", "Every streamed texture whose full-size longest edge is at most this many pixels (" + TextureStreaming.DefaultKeepLoadedEdge + " frees the most slots per MB)."),
                    McpSchema.String("pattern", "A regex on the texture name (case-insensitive)."),
                    McpSchema.Boolean("keep_loaded", "true (default): keep loaded; false: stream again."),
                    McpSchema.Boolean("dry_run", "List what would change and what it costs, changing nothing."),
                    McpSchema.Integer("max_listed", "List at most this many of the textures changed (default 25).")),
                Destructive = true,
                Toolset = "assets",
                Run = Set,
            };

            yield return new McpTool()
            {
                Name = "merge_identical_textures",
                Title = "Point materials at one copy of identical textures",
                Description = "Textures with the same image (format, flags, sizes and bytes all equal) imported under different names each stream on their own and each take one " +
                    "of the game's ~" + (TextureStreaming.StreamerBlocks - 4) + " streaming slots. This points every material slot that uses a copy at one of them (the one most used), so the " +
                    "copies are never drawn and never stream; the copies stay in the level (nothing else that might name one - an environment map, a Flash movie - changes). " +
                    "Cubemaps and Flash textures are left out. One undo step; Save & Build takes it into the game. dry_run (default true) lists the groups.",
                InputSchema = McpSchema.Object(
                    McpSchema.Boolean("dry_run", "List the groups only (default true). Pass false to merge."),
                    McpSchema.Integer("max_listed", "List at most this many groups (default 25).")),
                Destructive = true,
                Toolset = "assets",
                Run = Merge,
            };
        }

        private static string Mb(long bytes) => (bytes / 1048576.0).ToString("0.0") + " MB";

        private static object Report(McpCall call)
        {
            int maxEdge = Math.Max(1, call.Int("max_edge", TextureStreaming.DefaultKeepLoadedEdge));
            bool duplicates = call.Bool("duplicates", true);
            call.Progress("Counting the streamed textures");
            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel(false).Level;
                //The zones' names and links from the placed script, as the Level Check's texture_slots reads them
                TextureStreaming.SlotEstimate e = TextureSlotsRule.EstimateWithZones(level, duplicates);
                List<Textures.TEX4> small = TextureStreaming.Select(level.Textures.Entries, null, null, maxEdge, true);
                JObject result = new JObject()
                {
                    ["level_textures"] = e.LevelTextures,
                    ["streamed"] = e.Streamed,
                    ["kept_loaded"] = e.KeptLoaded,
                    ["cubemaps_streamed"] = e.Cubemaps,
                    ["peak_at_once"] = e.Peak,
                    ["peak_where"] = e.PeakWhere,
                    ["game_slots"] = TextureStreaming.StreamerBlocks,
                    ["safe_at_once"] = TextureStreaming.SafeStreamedAtOnce,
                    ["drawn_everywhere"] = e.Always,
                    ["not_drawn_by_placed_models"] = e.Unplaced,
                    ["unused"] = e.Unused,
                    ["zones"] = new JArray(e.Zones.Take(12).Select(o => new JObject() { ["zone"] = o.zone, ["textures"] = o.textures, ["with_linked_zones"] = o.withNeighbours })),
                    ["small"] = new JObject() { ["max_edge"] = maxEdge, ["textures"] = small.Count, ["keep_loaded_mb"] = Math.Round(small.Sum(TextureStreaming.KeepLoadedCost) / 1048576.0, 1) },
                };
                if (duplicates) result["identical_copies_streamed"] = e.Duplicates;
                if (e.Peak > TextureStreaming.SafeStreamedAtOnce)
                    call.Note(e.Peak + " streamed textures can be asked for at once (" + e.PeakWhere + "); the game holds about " + (TextureStreaming.StreamerBlocks - 4) + ", so " +
                              "the rest draw blurry" + (e.OneZone ? " for the whole level" : " in that zone") + ". Keep small ones loaded (set_texture_streaming max_edge " + maxEdge + ": " +
                              small.Count + " textures, " + Mb(small.Sum(TextureStreaming.KeepLoadedCost)) + ")" + (duplicates && e.Duplicates > 0 ? ", merge " + e.Duplicates + " identical copies (merge_identical_textures)" : "") +
                              (e.OneZone ? ", or split the level into zones." : ", or split the zone."));
                return result;
            });
        }

        private static object Set(McpCall call)
        {
            bool keep = call.Bool("keep_loaded", true);
            bool dry = call.Bool("dry_run");
            List<string> names = call.Has("names") ? call.StrList("names") : null;
            int maxEdge = call.Int("max_edge", 0);
            string pattern = call.Str("pattern");
            int maxListed = Math.Max(0, call.Int("max_listed", 25));
            if ((names == null || names.Count == 0) && maxEdge <= 0 && string.IsNullOrEmpty(pattern))
                throw McpError.Invalid("Choose textures: names, max_edge and/or pattern.");
            try { if (!string.IsNullOrEmpty(pattern)) new System.Text.RegularExpressions.Regex(pattern); }
            catch (ArgumentException e) { throw McpError.Invalid("pattern is not a valid regex: " + e.Message); }

            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel(!dry).Level;
                List<Textures.TEX4> all = level.Textures.Entries;
                List<Textures.TEX4> chosen = new List<Textures.TEX4>();
                JArray skipped = new JArray();
                if (names != null && names.Count != 0)
                {
                    foreach (string name in names)
                    {
                        Textures.TEX4 texture = all.FirstOrDefault(o => TextureStreaming.Normalise(o?.Name) == TextureStreaming.Normalise(name));
                        if (texture == null) throw McpError.NotFound("texture", name, all.Select(o => o.Name).Where(o => o.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0).Take(10));
                        string why = keep ? TextureStreaming.CannotKeepLoaded(texture) : (TextureStreaming.IsKeptLoaded(texture) ? null : "it isn't kept loaded");
                        if (why != null) skipped.Add(texture.Name + ": " + why);
                        else chosen.Add(texture);
                    }
                }
                if (maxEdge > 0 || !string.IsNullOrEmpty(pattern))
                    chosen.AddRange(TextureStreaming.Select(all, null, pattern, maxEdge, keep).Where(o => !chosen.Contains(o)));

                int streamedBefore = all.Count(TextureStreaming.Streams);
                long cost = keep ? chosen.Sum(TextureStreaming.KeepLoadedCost) : 0;
                JObject result = new JObject()
                {
                    ["keep_loaded"] = keep,
                    ["textures"] = chosen.Count,
                    ["streamed_before"] = streamedBefore,
                    ["streamed_after"] = streamedBefore + (keep ? -chosen.Count : chosen.Count),
                    ["resident_mb_added"] = keep ? Math.Round(cost / 1048576.0, 1) : 0,
                    ["changed"] = new JArray(chosen.Take(maxListed).Select(o => o.Name + " (" + TextureStreaming.Edge(o) + " px" + (keep ? ", +" + Mb(TextureStreaming.KeepLoadedCost(o)) : "") + ")")),
                };
                if (chosen.Count > maxListed) result["changed_total"] = chosen.Count;
                if (skipped.Count != 0) result["skipped"] = skipped;
                if (dry || chosen.Count == 0)
                {
                    result["dry_run"] = dry;
                    if (chosen.Count == 0) call.Note("Nothing to change.");
                    return result;
                }

                McpEditor.RequireUndoIdle();
                int done = TextureStreaming.SetKeptLoaded(level, chosen, keep, "AI: " + (keep ? "Keep " : "Stream ") + chosen.Count + (chosen.Count == 1 ? " texture" : " textures") + (keep ? " loaded" : " again"), out List<string> problems);
                result["textures"] = done;
                if (problems.Count != 0) result["problems"] = new JArray(problems);
                result["streamed_after"] = all.Count(TextureStreaming.Streams);
                call.Note("Save & Build takes it into the game.");
                return result;
            });
        }

        private static object Merge(McpCall call)
        {
            bool dry = call.Bool("dry_run", true);
            int maxListed = Math.Max(0, call.Int("max_listed", 25));
            call.Progress("Comparing the textures' images");
            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel(!dry).Level;
                List<TextureStreaming.DuplicateGroup> groups = TextureStreaming.FindDuplicates(level);
                JObject result = new JObject()
                {
                    ["groups"] = groups.Count,
                    ["copies"] = groups.Sum(o => o.Copies.Count),
                    ["copies_streamed"] = groups.Sum(o => o.Copies.Count(TextureStreaming.Streams)),
                    ["material_slots"] = groups.Sum(o => o.Slots),
                    ["streamed_mb"] = Math.Round(groups.Sum(o => o.StreamedBytes * o.Copies.Count) / 1048576.0, 1),
                    ["list"] = new JArray(groups.Take(maxListed).Select(o => new JObject() { ["keep"] = o.Keep.Name, ["copies"] = new JArray(o.Copies.Select(c => c.Name)), ["slots"] = o.Slots })),
                };
                if (dry || groups.Count == 0)
                {
                    result["dry_run"] = dry;
                    if (groups.Count == 0) call.Note("No identical textures that materials use.");
                    return result;
                }
                McpEditor.RequireUndoIdle();
                result["moved_slots"] = TextureStreaming.MergeDuplicates(level, groups, "AI: Merge " + groups.Sum(o => o.Copies.Count) + " identical textures");
                call.Note("Save & Build takes it into the game.");
                return result;
            });
        }
    }
}

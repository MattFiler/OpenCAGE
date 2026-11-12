using CATHODE;
using CATHODE.Scripting;
using Newtonsoft.Json.Linq;
using OpenCAGE.Surfaces;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// Z-fighting: find_z_fighting (surfaces drawn over each other at the same depth - between placed models, inside one
    /// model, decals on what they lie on) and fix_z_fighting (push the one on top out or sink the one underneath, checked by searching again,
    /// one undo step). Registered from McpViewportTools.Tools(); toolset 'world'. The Find Z-Fighting window does the same
    /// (<see cref="ZFightScene"/>, <see cref="ZFightRepair"/>).
    /// </summary>
    internal static class McpSurfaceTools
    {
        /// <summary>The last search (fix_z_fighting works on its numbers).</summary>
        internal static ZScan LastScan;

        private const string FightHelp = "Two surfaces drawn over each other at (nearly) the same depth flicker as the camera moves: the GPU picks either from pixel to pixel. ";

        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "find_z_fighting",
                Title = "Find z-fighting",
                Description = FightHelp +
                    "Searches placed models as the editor has them now (unsaved edits included; LOD 0; models hidden at the start and skinned meshes left out unless asked) for surfaces that face the same way, lie within 'gap_mm' of each other and within 'max_angle' of parallel, and overlap - measured exactly (each pair of triangles cut against each other), with which one lies on the other (a decal, a panel, the one most covered), how far apart they are and where. " +
                    "Or one model's own meshes ('model': what the Model Editor's Find Z-Fighting searches - a model imported with decals or ground baked in). " +
                    "Mesh decals (materials with DECAL, the 52-55 passes) lying on a surface are retail's way and do not fight it; they are reported only when 'decals' is true. Changes nothing; fix_z_fighting fixes what it found (by the ids here).",
                InputSchema = McpSchema.Object(
                    McpSchema.Nested("region", "Where to search (default the whole level). " + McpRegion.Help, McpRegion.Schema),
                    McpSchema.String("model", "Instead: one model's own meshes, in its own space (list_models)."),
                    McpSchema.Number("gap_mm", "Surfaces closer than this fight (default " + Defaults.Gap * 1000 + " mm)."),
                    McpSchema.Number("max_angle", "Degrees from parallel still counted (default " + Defaults.MaxAngle + ")."),
                    McpSchema.Number("min_area_cm2", "Leave out overlaps smaller than this (default " + Defaults.MinArea * 10000 + ")."),
                    McpSchema.Boolean("inside_models", "Also pairs inside one placed model (default true)."),
                    McpSchema.Boolean("include_hidden", "Also models hidden at the start (show_on_reset off), which a script may show later (default false)."),
                    McpSchema.Boolean("decals", "Also mesh decals lying on what they decorate (default false)."),
                    McpSchema.Integer("limit", "How many fights to list, largest first (default 40)."),
                    McpSchema.String("picture", "An absolute .png path: also write a picture of a fight (straight on, and a cut through it)."),
                    McpSchema.Integer("picture_fight", "Which fight the picture shows (default the largest).")),
                ReadOnly = true,
                Toolset = "world",
                Run = Find,
            };

            yield return new McpTool()
            {
                Name = "fix_z_fighting",
                Title = "Fix z-fighting",
                Description = FightHelp +
                    "Fixes fights the last find_z_fighting found: the one lying on top is pushed out along the shared normal (or, with action 'sink', the one underneath sunk back) until it is 'clearance_mm' in front everywhere over the overlap. " +
                    "Between two placements the placement moves: its own position, the alias already moving it, or a new alias in the root for that one placement (a fight inside a composite placed several times moves the entity in its composite, fixing every placement). Inside one model its vertices move on the model's own position grid (every copy of a point together, so nothing tears; a mesh mostly in the overlap moves whole) - every placement of that model changes. " +
                    "Before anything changes the fix is checked by searching again with the moves made: what is left of each fight and any new fight a move would make are reported. dry_run reports only. One undo step.",
                InputSchema = McpSchema.Object(
                    McpSchema.Any("fights", "The ids to fix (from find_z_fighting), or \"all\".", required: true),
                    McpSchema.String("action", "auto (default: push the one on top out), push, or sink (the one underneath goes back).", options: new[] { "auto", "push", "sink" }),
                    McpSchema.Number("clearance_mm", "How far apart they end up, at least (default " + Defaults.Clearance * 1000 + " mm)."),
                    McpSchema.Boolean("whole_mesh", "Move a mesh that lies mostly in the overlap whole, not just its overlapping triangles (default true)."),
                    McpSchema.Boolean("in_composite", "Inside a composite placed several times, with both surfaces in it: move the entity in the composite (every placement; default true). False: an alias for each placement."),
                    McpSchema.Boolean("dry_run", "Plan and check it, changing nothing."),
                    McpSchema.String("picture", "An absolute .png path: also write a picture of the first fight and its fix.")),
                Destructive = true,
                Toolset = "world",
                Run = Fix,
            };
        }

        /// <summary>The defaults the tools and the window share.</summary>
        public static class Defaults
        {
            //In game, surfaces 0 and 0.1 mm apart fight and 0.25 mm or more draw clean (to 28 m); retail lays
            //its alpha-tested overlays ~0.8 mm and its mesh decals 0-3 mm off what they decorate. Searched within 0.5 mm, fixed to 2 mm.
            public static float Gap = 0.0005f;
            public static float MaxAngle = 3f;
            public static double MinArea = 0.0004;
            public static float Clearance = 0.002f;
        }

        #region find_z_fighting
        private static object Find(McpCall call)
        {
            ZFightOptions options = new ZFightOptions()
            {
                Gap = (float)call.Num("gap_mm", Defaults.Gap * 1000) / 1000f,
                MaxAngle = (float)call.Num("max_angle", Defaults.MaxAngle),
                MinArea = call.Num("min_area_cm2", Defaults.MinArea * 10000) / 10000.0,
                InsideGroups = call.Bool("inside_models", true),
                IncludeHidden = call.Bool("include_hidden"),
            };
            if (options.Gap <= 0 || options.Gap > 0.2f) throw new McpError("'gap_mm' is more than 0 and at most 200.");
            if (options.MaxAngle < 0 || options.MaxAngle > 30) throw new McpError("'max_angle' is 0 to 30 degrees.");
            bool decals = call.Bool("decals");
            int limit = Math.Max(1, call.Int("limit", 40));
            ZScan scan = McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: false);
                if (call.Has("model"))
                {
                    if (call.Has("region")) throw new McpError("Give 'model' or 'region', not both.");
                    Models.CS2 model = McpAssets.FindModel(content.Level, call.Str("model"));
                    ZScan s = ZFightScene.FromModel(model, options);
                    s.Level = content.Level;
                    s.Version = McpCollision.Version;
                    return s;
                }
                McpRegion region = call.Has("region") ? McpRegion.Resolve(call, content.Level.Commands, content.Level, call.Token("region")) : null;
                return ZFightScene.FromRegion(call, content.Level, region, options);
            });
            call.Progress("Searching " + scan.Surfaces.Count + " surfaces");
            ZFightScene.Run(scan, call.Cancel, p => call.Progress("Searching", p, 1));
            if (!decals) Filter(scan);
            LastScan = scan;
            JObject result = Describe(scan, limit);
            if (call.Has("picture")) result["picture"] = Picture(call, scan, call.Has("picture_fight") ? scan.Fight(call.Int("picture_fight")) : scan.Fights.FirstOrDefault(), null);
            return result;
        }

        /// <summary>Leave out mesh decals lying on what they decorate (retail's own way; the decal passes do not fight it).</summary>
        public static int Filter(ZScan scan)
        {
            int before = scan.Fights.Count;
            scan.Fights = scan.Fights.Where(o => !IsDecalOnSurface(o)).ToList();
            for (int i = 0; i < scan.Fights.Count; i++) scan.Fights[i].Id = i + 1;
            int dropped = before - scan.Fights.Count;
            if (dropped != 0) scan.Notes.Add(dropped + " mesh decal(s) lying on what they decorate left out: the decal passes draw over it as retail's do.");
            return dropped;
        }

        /// <summary>A mesh decal on (or just in front of) what it decorates - a surface or another decal: the decal passes blend it over in draw
        /// order and write no depth, so it never fights (retail stacks decals 0-3 mm off surfaces and on each other).</summary>
        public static bool IsDecalOnSurface(ZFight fight) => fight.A.Pass == ZPass.Decal && fight.OffsetMean >= -0.0005f;

        internal static JObject Describe(ZScan scan, int limit)
        {
            JObject result = new JObject()
            {
                ["searched"] = scan.Scope,
                ["surfaces"] = scan.Surfaces.Count,
                ["triangles"] = scan.Stats?.Triangles ?? 0,
                ["fights"] = scan.Fights.Count,
                ["overlap_m2"] = R(scan.FightArea, 3),
                ["seconds"] = R(scan.Stats?.Seconds ?? 0, 2),
                ["gap_mm"] = R(scan.Options.Gap * 1000, 2),
            };
            if (scan.LevelMode) result["placements"] = scan.Placements.Count;
            JArray list = new JArray();
            foreach (ZFight f in scan.Fights.Take(limit)) list.Add(DescribeFight(scan, f));
            result["list"] = list;
            if (scan.Fights.Count > limit) result["more"] = scan.Fights.Count - limit;
            JObject kinds = new JObject();
            foreach (var g in scan.Fights.GroupBy(Category)) kinds[g.Key] = new JObject() { ["fights"] = g.Count(), ["m2"] = R(g.Sum(o => o.Area), 3) };
            result["by_kind"] = kinds;
            if (scan.Notes.Count != 0) result["notes"] = new JArray(scan.Notes);
            if (scan.Fights.Count == 0) result["clean"] = "Nothing fights within " + R(scan.Options.Gap * 1000, 2) + " mm.";
            return result;
        }

        /// <summary>"two placements", "inside one model", "copies" (one model over a copy of itself), "decal".</summary>
        public static string Category(ZFight f)
        {
            if (f.Copies) return "copies";
            if (f.A.Pass == ZPass.Decal) return "decal";
            if (f.Kind == "crossing") return "crossing";
            if (f.SameGroup) return "inside one model";
            return "two placements";
        }

        private static JObject DescribeFight(ZScan scan, ZFight f)
        {
            JObject o = new JObject()
            {
                ["id"] = f.Id,
                ["area_m2"] = R(f.Area, 4),
                ["apart_mm"] = new JArray(R(f.OffsetMin * 1000, 2), R(f.OffsetMax * 1000, 2)),
                ["kind"] = Category(f),
                ["lie"] = f.Kind,
                ["on_top"] = Side(f.A),
                ["underneath"] = Side(f.B),
                ["cover"] = new JArray(R(f.CoverA, 2), R(f.CoverB, 2)),
                ["centre"] = McpCollision.V(f.Centre),
                ["normal"] = McpCollision.V(f.Normal),
            };
            if (f.Kind == "crossing") o["crosses_mm"] = new JArray(R(f.DeepestMin * 1000, 2), R(f.DeepestMax * 1000, 2));
            if (f.BackToBack) o["back_to_back"] = true;
            if (Category(f) == "copies") o["note"] = "Both lie wholly on each other: the same thing twice in one place (or two that a script shows one at a time).";
            return o;
        }

        private static JObject Side(ZSurface s)
        {
            ZSurfaceSource src = ZFightScene.SourceOf(s);
            JObject o = new JObject() { ["surface"] = s.Label, ["pass"] = ZFightScene.PassName(s.Pass) };
            if (src?.Placement != null) o["placement"] = src.Placement.Path;
            if (src?.Model != null) o["model"] = src.Model.Name;
            if (src != null && src.SubmeshIndex >= 0) o["mesh"] = src.SubmeshIndex;
            if (s.Material != null) o["material"] = s.Material;
            return o;
        }
        #endregion

        #region fix_z_fighting
        private static object Fix(McpCall call)
        {
            ZScan scan = LastScan;
            if (scan == null) throw new McpError("Search first: find_z_fighting.");
            List<ZFight> fights;
            JToken which = call.Token("fights");
            if (which.Type == JTokenType.String && string.Equals((string)which, "all", StringComparison.OrdinalIgnoreCase)) fights = scan.Fights.ToList();
            else
            {
                List<int> ids = which.Type == JTokenType.Array ? which.Select(o => (int)o).ToList() : new List<int>() { (int)which };
                fights = new List<ZFight>();
                foreach (int id in ids)
                {
                    ZFight f = scan.Fight(id);
                    if (f == null) throw new McpError("No fight " + id + " in the last search (1-" + scan.Fights.Count + ").");
                    fights.Add(f);
                }
            }
            if (fights.Count == 0) throw new McpError("The last search found nothing to fix.");
            string action = (call.Str("action") ?? "auto").Trim().ToLowerInvariant();
            ZFixOptions options = new ZFixOptions()
            {
                Action = action == "sink" ? ZFixAction.Sink : action == "push" ? ZFixAction.Push : action == "auto" ? ZFixAction.Auto : throw new McpError("'action' is auto, push or sink."),
                Clearance = (float)call.Num("clearance_mm", Defaults.Clearance * 1000) / 1000f,
                WholeMesh = call.Bool("whole_mesh", true),
                InComposite = call.Bool("in_composite", true),
            };
            if (options.Clearance <= 0 || options.Clearance > 0.5f) throw new McpError("'clearance_mm' is more than 0 and at most 500.");
            bool dry = call.Bool("dry_run");

            ZFixPlan plan = McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: !dry);
                if (scan.Level != null && content.Level != scan.Level) throw new McpError("The search was made in another level: search again.");
                if (scan.LevelMode && McpCollision.Version != scan.Version) throw new McpError("The level has changed since the search: search again (find_z_fighting) so the fix starts from where things are now.");
                if (!scan.LevelMode && !content.Level.Models.Entries.Contains(scan.Model)) throw new McpError("That model is no longer in the level: search again.");
                return ZFightRepair.Plan(scan, fights, options);
            });
            JObject result = DescribePlan(plan);
            if (call.Has("picture")) result["picture"] = Picture(call, scan, fights[0], plan);
            if (dry)
            {
                result["dry_run"] = true;
                return result;
            }
            if (plan.Moves.All(o => o.Problem != null)) throw new McpError("Nothing can be moved: " + string.Join("; ", plan.Moves.Select(o => o.Label + " - " + o.Problem)) + ".");
            ZFightRepair.Outcome outcome = McpEditor.UI(() => ZFightRepair.Apply(plan, "AI: "));
            result["fixed"] = outcome.Changed;
            result["undo"] = "One undo step: '" + outcome.Label + "'.";
            if (outcome.Aliases.Count != 0) result["aliases_made"] = outcome.Aliases;
            if (outcome.Notes.Count != 0) result["edit_notes"] = new JArray(outcome.Notes);
            //The numbers belong to the level as it was
            LastScan = null;
            result["next"] = "Search again (find_z_fighting) to see the level as it is now; save_level build=true puts it in the game.";
            return result;
        }

        internal static JObject DescribePlan(ZFixPlan plan)
        {
            JArray moves = new JArray();
            foreach (ZMove m in plan.Moves)
            {
                JObject o = new JObject() { ["move"] = m.Label, ["fights"] = new JArray(m.Fights), ["distance_mm"] = R(m.Distance * 1000, 2) };
                if (m.IsPlacement)
                {
                    o["placement"] = m.Placement.Path;
                    o["by"] = McpCollision.V(m.Delta);
                    o["written_to"] = m.WrittenTo == "override" ? "the alias already moving it" : m.WrittenTo == "composite" ? "its position in its composite (every placement of the composite)" : m.WrittenTo == "alias" ? "an alias in the root (this placement only)" : "its position";
                }
                else
                {
                    o["model"] = m.Model.Name;
                    o["mesh"] = m.SubmeshLabel;
                    o["vertices"] = m.Vertices.Count;
                    o["whole_mesh"] = m.WholeMesh;
                    o["grid_step_mm"] = R(ZFightRepair.Step(m.Submesh) * 1000, 3);
                    if (m.ModelPlacements > 1) o["placements_of_model"] = m.ModelPlacements;
                }
                if (m.Problem != null) o["problem"] = m.Problem;
                moves.Add(o);
            }
            JObject result = new JObject()
            {
                ["fights"] = plan.Fights.Count,
                ["moves"] = moves,
                ["overlap_before_m2"] = R(plan.AreaBefore, 4),
            };
            if (plan.Verified)
            {
                result["checked"] = new JObject()
                {
                    ["overlap_left_m2"] = R(plan.AreaLeft, 4),
                    ["left_per_fight"] = new JObject(plan.AreaAfter.Where(o => o.Value > 1e-6).Select(o => new JProperty(o.Key.ToString(), R(o.Value, 4)))),
                    ["new_fights"] = new JArray(plan.Made.Take(10).Select(o => o.A.Label + " on " + o.B.Label + " (" + R(o.Area, 4) + " m2)")),
                };
            }
            if (plan.Notes.Count != 0) result["notes"] = new JArray(plan.Notes);
            return result;
        }
        #endregion

        private static string Picture(McpCall call, ZScan scan, ZFight fight, ZFixPlan plan)
        {
            string path = McpAssets.AbsolutePath(call.Str("picture"), "picture");
            if (!string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase)) throw new McpError("'picture' has to be a .png path.");
            if (fight != null && plan == null) plan = ZFightRepair.Plan(scan, new[] { fight }, new ZFixOptions());
            using (Bitmap bitmap = ZFightPreview.Draw(scan, fight, plan, 900, 560))
                bitmap.Save(path, ImageFormat.Png);
            return path;
        }

        private static double R(double v, int digits) => Math.Round(v, digits);
    }
}

using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.Breakables;
using OpenCAGE.Brushwork;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// Fracture into debris: make_breakable (a placed model that breaks into physics pieces when shot or told to),
    /// fracture_model (a model's pieces as spawnable physics assets only) and list_breakables. Each names its own toolset ('assets').
    /// The work is <see cref="BreakableBuilder"/>'s; the Make Breakable window does the same.
    /// </summary>
    internal static class McpFractureTools
    {
        private static readonly string[] Interiors = { "dark", "projected" };

        private static IEnumerable<McpSchema.Prop> CutProps() => new[]
        {
            McpSchema.Integer("pieces", "How many pieces, " + FractureGeometry.MinPieces + " to 16 (default 6). Each is a live physics body once broken."),
            McpSchema.Integer("seed", "Which way it breaks: the same seed always cuts the same pieces (default 7)."),
            McpSchema.String("interior", "The cut faces: 'dark' (default: one dark texel of the model's own texture, a dark inside) or 'projected' (the texture carried flat across the cut: solid wood, concrete).", options: Interiors),
            McpSchema.Boolean("inner_skin", "A reversed copy of the surface a few millimetres in, so a piece never shows its hollow inside where a cut could not be closed (default true)."),
            McpSchema.Number("mass", "All the pieces together, kg, shared by volume (default: each piece's hull at 300 kg/m3, as import_physics_system suggests)."),
            McpSchema.Number("friction", "0 to 10 (default 0.6)."),
            McpSchema.Number("restitution", "Bounciness, 0 to 1 (default 0.25)."),
            McpSchema.Number("kick_speed", "How fast each piece leaves as it spawns, m/s outwards from the middle and up (a PhysicsApplyImpulse of its mass times this; default 2.5, 0 = they drop)."),
            McpSchema.Boolean("is_debris", "Mark the pieces' ModelReferences is_debris, as retail's broken-bottle pieces are (default true)."),
            McpSchema.String("preview", "An absolute .png path: also write a picture of the pieces (exploded, from four sides)."),
            McpSchema.Boolean("dry_run", "Cut it and report the pieces (triangles, cut faces, hull corners, masses), changing nothing."),
        };

        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "make_breakable",
                Title = "Make a model breakable",
                Description = "Make a placed model (a ModelReference) break into physics debris when it is shot or hit, or when a script calls its 'Break' pin. " +
                    "Its model is cut into chunky pieces (Voronoi plane cuts with closed, one-sided cut faces), each a spawnable physics asset of its own as a moving prop must be: a composite '<breakable>_Debris\\<name>_NN' holding a ModelReference keyed by its body's name, collision from its triangles (BALLISTIC|FIXED|RESOURCE|GHOSTED|PRE_GHOSTED|FROZEN|PRE_FROZEN, as retail's dynamic props) and a one-body PhysicsSystem from its convex hull (at most 64 corners), kicked outwards as it spawns. " +
                    "The model then goes into a composite of its own (default 'Breakables\\<name>', placed where it was; links into it kept) with one AssetSpawner per piece over an is_template instance of it placed at the piece's place, and the break: a LogicOnce fed by the 'Break' method pin, by the model's on_damaged through a WEAPON_DamageFilter (retail's breakable bottle) and by an AreaHitMonitor round it (pistol and shotgun hits); on success the model hides and disables (its collision goes with it), the pieces spawn and the 'Broken' pin fires. " +
                    "Pieces cost nothing until the break. The models, proxies and physics systems cannot be undone (as any import); the script changes are undo steps. Reaches the game after save_level build=true. dry_run reports the cut first.",
                InputSchema = McpSchema.Object(new[]
                {
                    McpSchema.String("composite", "The composite holding the model (path or id).", required: true),
                    McpSchema.String("entity", "The ModelReference (id or name).", required: true),
                    McpSchema.Boolean("break_on_damage", "Break when shot or hit (default true); false = only the 'Break' pin breaks it."),
                    McpSchema.Integer("damage_threshold", "Damage one hit must do to break it (WEAPON_DamageFilter; retail's bottle uses 30; default 20, 0 = any hit)."),
                    McpSchema.Boolean("hit_monitor", "Also break on pistol and shotgun hits anywhere in its bounding sphere (an AreaHitMonitor; default true)."),
                    McpSchema.Number("despawn_after", "Seconds after the break the pieces are removed (default 0: they stay)."),
                    McpSchema.String("name", "The breakable composite's path (default Breakables\\<entity name>)."),
                }.Concat(CutProps()).ToArray()),
                Destructive = true,
                Toolset = "assets",
                Run = MakeBreakable,
            };

            yield return new McpTool()
            {
                Name = "fracture_model",
                Title = "Fracture a model into debris",
                Description = "Cut a model into chunky pieces, each made a spawnable physics asset (a composite '<folder>\\<name>_NN' holding the piece's ModelReference keyed by its body's name, its collision and a one-body PhysicsSystem, kicked outwards as it spawns) - for scripts of your own: place an is_template instance of a piece at the piece's 'pivot' relative to where the whole model sits, and an AssetSpawner (allow_physics) whose asset is that instance spawns it there (a spawned asset appears where its template instance is placed, not where the spawner is). " +
                    "Nothing is placed (make_breakable does the whole breakable). Give 'model' (list_models), or 'composite' + 'entity' for the model a ModelReference draws. The models, proxies and physics systems cannot be undone. dry_run reports the cut first.",
                InputSchema = McpSchema.Object(new[]
                {
                    McpSchema.String("model", "A model the level holds (list_models): its first LOD is cut."),
                    McpSchema.String("composite", "Or the composite holding a ModelReference (path or id)..."),
                    McpSchema.String("entity", "...and the ModelReference whose model is cut (id or name)."),
                    McpSchema.String("folder", "Where the piece composites go (default 'Breakables\\<model name>_Debris')."),
                }.Concat(CutProps()).ToArray()),
                Destructive = true,
                Toolset = "assets",
                Run = FractureModel,
            };

            yield return new McpTool()
            {
                Name = "list_breakables",
                Title = "List breakables",
                Description = "The open level's breakables (made by make_breakable or the editor's Make Breakable): each composite, how many times it is placed, its model, its pieces (debris composites with their physics systems), and what breaks it.",
                InputSchema = McpSchema.Object(),
                ReadOnly = true,
                Idempotent = true,
                Toolset = "assets",
                Run = ListBreakables,
            };
        }

        /// <summary>The cut and physics options a call gives (defaults where it gives none).</summary>
        internal static BreakableOptions Options(McpCall call)
        {
            BreakableOptions o = new BreakableOptions();
            if (call.Has("pieces"))
            {
                int pieces = call.Int("pieces");
                if (pieces < FractureGeometry.MinPieces || pieces > 16) throw McpError.Invalid("'pieces' takes " + FractureGeometry.MinPieces + " to 16.");
                o.Pieces = pieces;
            }
            if (call.Has("seed")) o.Seed = call.Int("seed");
            string interior = call.Str("interior");
            if (interior != null)
            {
                if (!Interiors.Contains(interior.Trim().ToLowerInvariant())) throw McpError.Invalid("'interior' is dark or projected.");
                o.Interior = interior.Trim().ToLowerInvariant() == "projected" ? FractureInterior.Projected : FractureInterior.Dark;
            }
            o.InnerSkin = call.Bool("inner_skin", true);
            if (call.Has("mass"))
            {
                double mass = call.Num("mass");
                if (mass < 0.05 || mass > 10000) throw McpError.Invalid("'mass' takes 0.05 to 10000 kg.");
                o.Mass = mass;
            }
            o.Friction = call.Num("friction", 0.6);
            o.Restitution = call.Num("restitution", 0.25);
            if (o.Friction < 0 || o.Friction > 10) throw McpError.Invalid("'friction' takes 0 to 10.");
            if (o.Restitution < 0 || o.Restitution > 1) throw McpError.Invalid("'restitution' takes 0 to 1.");
            o.KickSpeed = call.Num("kick_speed", 2.5);
            if (o.KickSpeed < 0 || o.KickSpeed > 30) throw McpError.Invalid("'kick_speed' takes 0 to 30 m/s.");
            o.IsDebris = call.Bool("is_debris", true);
            o.BreakOnDamage = call.Bool("break_on_damage", true);
            o.DamageThreshold = call.Int("damage_threshold", 20);
            if (o.DamageThreshold < 0 || o.DamageThreshold > 10000) throw McpError.Invalid("'damage_threshold' takes 0 to 10000.");
            o.HitMonitor = call.Bool("hit_monitor", true);
            o.DespawnAfter = call.Num("despawn_after", 0);
            if (o.DespawnAfter < 0 || o.DespawnAfter > 3600) throw McpError.Invalid("'despawn_after' takes 0 to 3600 seconds.");
            o.Name = call.Str("name");
            o.DebrisFolder = call.Str("folder");
            return o;
        }

        private static Action<BreakablePlan> Preview(McpCall call, JObject[] holder)
        {
            string path = call.Str("preview");
            if (path == null) return null;
            string full = McpAssets.AbsolutePath(path, "preview");
            if (!full.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) throw McpError.Invalid("'preview' is a .png path.");
            return plan =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                using (Bitmap sheet = FracturePreview.Sheet(plan.Pieces))
                    sheet.Save(full, ImageFormat.Png);
                holder[0] = new JObject() { ["preview"] = full };
            };
        }

        private static object MakeBreakable(McpCall call)
        {
            BreakableOptions options = Options(call);
            JObject[] picture = new JObject[1];
            BreakableHost host = new BreakableHost() { Call = call, Progress = s => call.Progress(s) };
            JObject result = BreakableBuilder.MakeBreakable(host, call.Str("composite", required: true), call.Str("entity", required: true), options, call.Bool("dry_run"), Preview(call, picture));
            if (picture[0] != null) result["preview"] = picture[0]["preview"];
            foreach (string note in host.Notes) call.Note(note);
            return result;
        }

        private static object FractureModel(McpCall call)
        {
            BreakableOptions options = Options(call);
            JObject[] picture = new JObject[1];
            string modelArg = call.Str("model");
            if (modelArg == null && !(call.Has("composite") && call.Has("entity"))) throw McpError.Invalid("Give 'model', or 'composite' and 'entity' (a ModelReference).");
            Models.CS2 model = McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel(forEditing: false).Level;
                if (modelArg != null) return McpAssets.FindModel(level, modelArg);
                Composite composite = McpScript.FindComposite(level.Commands, call.Str("composite"));
                Entity entity = McpScript.FindEntity(level.Commands, composite, call.Str("entity"));
                Models.CS2 drawn = entity is FunctionEntity f ? CollisionMatch.DrawnModel(level, f, out _) : null;
                return drawn ?? throw McpError.Invalid(McpScript.EntityName(level.Commands, composite, entity) + " draws no model the level holds.");
            });
            if (options.Name == null) options.Name = BreakableBuilder.DefaultFolder + "\\" + BreakableBuilder.Stem(model.Name);
            BreakableHost host = new BreakableHost() { Call = call, Progress = s => call.Progress(s) };
            JObject result = BreakableBuilder.MakeDebris(host, model, options, call.Bool("dry_run"), Preview(call, picture));
            if (picture[0] != null) result["preview"] = picture[0]["preview"];
            foreach (string note in host.Notes) call.Note(note);
            return result;
        }

        private static object ListBreakables(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel(forEditing: false).Level;
                Commands commands = level.Commands;
                JArray found = new JArray();
                foreach (BreakableInfo info in BreakableInfo.All(level))
                    found.Add(info.ToJson(commands));
                return new JObject() { ["count"] = found.Count, ["breakables"] = found };
            });
        }
    }

    /// <summary>A breakable as it stands in a level (list_breakables, the window's status).</summary>
    internal sealed class BreakableInfo
    {
        public Composite Composite;
        public FunctionEntity Once, Intact;
        public List<(FunctionEntity spawner, Composite piece)> Pieces = new List<(FunctionEntity, Composite)>();
        public int Placements;
        public int? DamageThreshold;
        public bool HitMonitor, OnDamaged;

        private static readonly ShortGuid Asset = ShortGuidUtils.Generate("asset"), OnSuccess = ShortGuidUtils.Generate("on_success"), Hide = ShortGuidUtils.Generate("hide"),
            OnDamagedPin = ShortGuidUtils.Generate("on_damaged");

        private static bool Draws(FunctionEntity model) => (CollisionMatch.RunOf(model)?.Count ?? 0) != 0;

        public static IEnumerable<BreakableInfo> All(Level level)
        {
            Commands commands = level.Commands;
            foreach (Composite composite in commands.Entries)
            {
                if (composite == null) continue;
                FunctionEntity once = composite.functions.FirstOrDefault(o => o.function == FunctionType.LogicOnce && CommandsUtils.GetEntityNameParameter(o) == BreakableBuilder.OnceName);
                if (once == null || !composite.functions.Any(o => o.function == FunctionType.AssetSpawner)) continue;
                BreakableInfo info = new BreakableInfo() { Composite = composite, Once = once };
                foreach (EntityConnector link in once.childLinks.Where(o => o.thisParamID == OnSuccess))
                {
                    Entity target = composite.GetEntityByID(link.linkedEntityID);
                    if (link.linkedParamID == Hide && target is FunctionEntity model && model.function == FunctionType.ModelReference && Draws(model) && info.Intact == null) info.Intact = model;
                }
                //Every spawner of the composite (the break reaches them through its SpawnDelay)
                foreach (FunctionEntity spawner in composite.functions.Where(o => o.function == FunctionType.AssetSpawner))
                    foreach (EntityConnector asset in spawner.childLinks.Where(o => o.thisParamID == Asset))
                        if (composite.GetEntityByID(asset.linkedEntityID) is FunctionEntity instance && !instance.function.IsFunctionType)
                            info.Pieces.Add((spawner, commands.GetComposite(instance.function)));
                FunctionEntity filter = composite.functions.FirstOrDefault(o => o.function == FunctionType.WEAPON_DamageFilter);
                if (filter?.GetParameter("damage_threshold")?.content is cInteger threshold) info.DamageThreshold = threshold.value;
                info.HitMonitor = composite.functions.Any(o => o.function == FunctionType.AreaHitMonitor);
                info.OnDamaged = composite.functions.Any(o => o.function == FunctionType.ModelReference && o.childLinks.Any(l => l.thisParamID == OnDamagedPin));
                info.Placements = McpScriptEdit.PlacementCount(commands, composite);
                yield return info;
            }
        }

        public JObject ToJson(Commands commands)
        {
            JObject o = new JObject()
            {
                ["composite"] = Composite.name,
                ["placements"] = Placements,
                ["model_entity"] = Intact == null ? null : commands.Utils.GetEntityName(Composite, Intact),
                ["pieces"] = new JArray(Pieces.Select(p => new JObject()
                {
                    ["spawner"] = commands.Utils.GetEntityName(Composite, p.spawner),
                    ["debris"] = p.piece?.name,
                    ["physics_system"] = p.piece == null ? -1 : p.piece.functions.Where(f => f.function == FunctionType.PhysicsSystem).Select(PhysicsBodyKeys.SystemIndexOf).DefaultIfEmpty(-1).First(),
                })),
                ["breaks_on"] = new JArray(new[] { "the 'Break' pin", OnDamaged ? "damage" + (DamageThreshold.HasValue ? " >= " + DamageThreshold.Value : "") : null, HitMonitor ? "pistol / shotgun hits in its sphere" : null }.Where(s => s != null)),
            };
            return o;
        }
    }
}

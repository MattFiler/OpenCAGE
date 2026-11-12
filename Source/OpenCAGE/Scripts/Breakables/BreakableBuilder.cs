using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.Brushwork;
using OpenCAGE.MCP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using AssetKind = CathodeLib.AssetModificationInfoTable.AssetKind;
using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;

namespace OpenCAGE.Breakables
{
    /// <summary>What a break asks for, from the dialog or the tools.</summary>
    public sealed class BreakableOptions
    {
        public int Pieces = 6;
        public int Seed = 7;
        public FractureInterior Interior = FractureInterior.Dark;
        public bool InnerSkin = true;
        /// <summary>All the pieces together, kg; null = each its hull's volume at 300 kg/m3.</summary>
        public double? Mass;
        public double Friction = 0.6, Restitution = 0.25;
        /// <summary>How fast each piece leaves (m/s, outwards and up): its PhysicsApplyImpulse is its mass times this. 0 = they just drop.</summary>
        public double KickSpeed = 2.5;
        /// <summary>Retail's is_debris flag, as on its breakable pieces.</summary>
        public bool IsDebris = true;
        /// <summary>Breaks when shot or hit (ModelReference on_damaged through a WEAPON_DamageFilter, as retail's bottles).</summary>
        public bool BreakOnDamage = true;
        /// <summary>WEAPON_DamageFilter damage_threshold (retail's bottle: 30); 0 = any hit.</summary>
        public int DamageThreshold = 20;
        /// <summary>Also an AreaHitMonitor round it (pistol and shotgun hits in its sphere).</summary>
        public bool HitMonitor = true;
        /// <summary>Seconds after the break the pieces are despawned; 0 = they stay.</summary>
        public double DespawnAfter = 0;
        /// <summary>The breakable composite's path (default Breakables\&lt;name&gt;).</summary>
        public string Name;
        /// <summary>Where the piece composites go (default: the breakable's path + "_Debris").</summary>
        public string DebrisFolder;
    }

    /// <summary>One piece as it will be made: its composite, body, model and physics.</summary>
    public sealed class PiecePlan
    {
        public FracturePiece Piece;
        public string Composite, Body, ModelName;
        public Models.CS2 Model;
        public int Submeshes, Triangles, HullCorners, SurfaceCorners, FullHullCorners;
        public double Volume, Mass, Force;
        public Vector3 Direction;
        public List<string> Warnings = new List<string>();
    }

    /// <summary>The whole break, worked out before anything in the level changes (a dry run returns it).</summary>
    public sealed class BreakablePlan
    {
        public string Stem;
        public FractureInput Input;
        public List<FracturePiece> Pieces;
        public List<PiecePlan> Plans = new List<PiecePlan>();
        public string BreakablePath, DebrisFolder;
        /// <summary>The physics material the pieces' collision gets ("name#index"): the source's collision row's, when it has one.</summary>
        public string CollisionMaterial;
        public List<string> Notes = new List<string>();
        public double Seconds;
    }

    /// <summary>Where a build runs: from a tool (its call, "AI: " undo labels) or from the editor's window (no call).</summary>
    internal sealed class BreakableHost
    {
        public McpCall Call;
        public string LabelPrefix = "AI: ";
        public List<string> Notes = new List<string>();
        public Action<string> Progress;

        public JObject Nested(string tool, JObject arguments)
        {
            Progress?.Invoke(tool);
            if (Call != null) return McpEditorTools.Nested(Call, tool, arguments);
            McpTool found = McpTools.Find(tool) ?? throw new McpError(McpErrorCodes.Failed, "The " + tool + " tool is missing from this build.");
            McpCall call = new McpCall(found, arguments, CancellationToken.None, null);
            object result = found.Run(call);
            Notes.AddRange(call.Notes);
            return result as JObject ?? (result == null ? new JObject() : JObject.FromObject(result));
        }
    }

    /// <summary>
    /// Makes a placed ModelReference breakable. Its model is cut into pieces (<see cref="FractureGeometry"/>), each a spawnable physics asset:
    /// a composite holding a ModelReference keyed by its body's name, its own collision, a one-body PhysicsSystem and a ThinkOnce impulse.
    /// The model is grouped with one AssetSpawner per piece (a spawned body wakes when pushed, a placed one never does) and a LogicOnce break
    /// that hides it and fires 'Broken'. Each piece's material is on its model: the ModelReference 'material' parameter is not applied in game.
    /// </summary>
    public static class BreakableBuilder
    {
        public const string DefaultFolder = "Breakables";
        public const string BreakPin = "Break", BrokenPin = "Broken", OnceName = "BreakOnce";
        /// <summary>Seconds between the model going (hidden, its collision off) and its pieces spawning.</summary>
        public const double SpawnDelaySeconds = 0.1;

        #region Planning
        /// <summary>A name made safe for composites, entities and Havok (ASCII letters, digits, underscores; never TEMPLATE or PHYSICS).</summary>
        public static string Stem(string name)
        {
            string leaf = (name ?? "").Replace('/', '\\');
            leaf = leaf.Substring(leaf.LastIndexOf('\\') + 1);
            if (leaf.EndsWith(".CS2", StringComparison.OrdinalIgnoreCase)) leaf = leaf.Substring(0, leaf.Length - 4);
            StringBuilder sb = new StringBuilder();
            foreach (char c in leaf) sb.Append(c < 128 && (char.IsLetterOrDigit(c) || c == '_') ? c : '_');
            string stem = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "(?i)template|physics", "").Trim('_');
            stem = System.Text.RegularExpressions.Regex.Replace(stem, "_+", "_");
            if (stem.Length == 0) stem = "Breakable";
            return stem.Length > 40 ? stem.Substring(0, 40) : stem;
        }

        /// <summary>Why this entity cannot be made breakable, or null. UI thread.</summary>
        public static string WhyNot(Level level, Composite composite, Entity entity)
        {
            if (!(entity is FunctionEntity function) || function.function != FunctionType.ModelReference) return "Only a ModelReference (a placed model) can be broken.";
            List<RenderableElements.Element> run = CollisionMatch.RunOf(function);
            if (run == null || !run.Any(o => o?.Model != null)) return "It draws nothing to break.";
            if (CollisionPairs.FindOriginal(level.Commands, composite, function) != null) return "It is the ballistic half of a collision pair: break the model it belongs to.";
            if (BrushService.IsBrush(level, composite, entity)) return "A brush is level geometry: it stays whole (convert it to a model first).";
            if (composite.functions.Any(o => o.function == FunctionType.PhysicsSystem)) return composite.name + " is a physics prop (it has a PhysicsSystem): break a static model.";
            if (composite.functions.Any(o => o.function == FunctionType.AssetSpawner) && composite.functions.Any(o => o.function == FunctionType.LogicOnce && CommandsUtils.GetEntityNameParameter(o) == OnceName))
                return composite.name + " is already a breakable.";
            return null;
        }

        /// <summary>
        /// The collision-only ModelReferences that are this model's collision (retail props carry a 'Collision01_COL' beside the drawn model),
        /// which would otherwise stay as an invisible block: named &lt;entity&gt;_COL..., placed where it is, or all of them when it is its
        /// composite's only drawn model. Its own ballistic half is not included (grouping takes that).
        /// </summary>
        public static List<FunctionEntity> CollisionPartners(Level level, Composite composite, FunctionEntity entity)
        {
            Commands commands = level.Commands;
            string own = commands.Utils.GetEntityName(composite, entity) ?? "";
            bool Draws(FunctionEntity o) => (CollisionMatch.RunOf(o)?.Count ?? 0) != 0;
            List<FunctionEntity> drawn = composite.functions.Where(o => o.function == FunctionType.ModelReference && Draws(o)).ToList();
            bool onlyModel = drawn.Count == 1 && drawn[0] == entity;
            cTransform at = entity.GetParameter("position")?.content as cTransform;
            //At the same point, whatever its rotation: a retail prop's model is often turned round from its collision
            bool SamePlace(FunctionEntity o)
            {
                cTransform t = o.GetParameter("position")?.content as cTransform;
                Vector3 p0 = at == null ? Vector3.Zero : new Vector3(at.position.X, at.position.Y, at.position.Z), p1 = t == null ? Vector3.Zero : new Vector3(t.position.X, t.position.Y, t.position.Z);
                return Vector3.Distance(p0, p1) < 0.01f;
            }
            FunctionEntity half = CollisionPairs.FindSibling(commands, composite, entity);
            return composite.functions.Where(o => o != entity && o != half && o.function == FunctionType.ModelReference && !Draws(o) && CollisionPairs.RowOf(o) != null
                    && CollisionPairs.FindOriginal(commands, composite, o) == null
                    && (onlyModel || SamePlace(o) || (commands.Utils.GetEntityName(composite, o) ?? "").StartsWith(own + "_COL", StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        /// <summary>Read the source and cut it, building every piece's model (nothing in the level changes). Reads the level on the UI thread.</summary>
        public static BreakablePlan Plan(Level level, Composite composite, FunctionEntity entity, BreakableOptions options)
        {
            return McpEditor.UI(() => PlanCore(level, CollisionMatch.RunOf(entity), entity.GetResource(ResourceType.COLLISION_MAPPING, true)?.CollisionMapping,
                composite == null ? null : level.Commands.Utils.GetEntityName(composite, entity), options));
        }

        /// <summary>A model's first LOD, as a source (the debris-only path: no entity). Reads the level on the UI thread.</summary>
        public static BreakablePlan PlanModel(Level level, Models.CS2 model, BreakableOptions options)
        {
            return McpEditor.UI(() => PlanCore(level, RunOf(model), null, model.Name, options));
        }

        /// <summary>Every submesh of a model's first LOD with its own material, as a run.</summary>
        public static List<RenderableElements.Element> RunOf(Models.CS2 model) =>
            model.Components.SelectMany(c => c.LODs.Take(1)).SelectMany(l => l.Submeshes).Where(s => s != null)
                .Select(s => new RenderableElements.Element() { Model = s, Material = s.Material }).ToList();

        /// <summary>
        /// The plan without the editor: cut <paramref name="run"/> and build each piece's model and hull. <paramref name="row"/>'s physics
        /// material goes to the pieces. Throws <see cref="InvalidOperationException"/> with the reason when it cannot be broken.
        /// </summary>
        public static BreakablePlan PlanCore(Level level, List<RenderableElements.Element> run, CollisionMaps.COLLISION_MAPPING row, string name, BreakableOptions options)
        {
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            BreakablePlan plan = new BreakablePlan();
            FractureInput input = FractureModels.Read(level, run);
            if (row?.Material != null)
            {
                int index = level.Materials.Entries.IndexOf(row.Material);
                if (index >= 0) plan.CollisionMaterial = (level.Materials.GetMaterialName(row.Material) ?? "") + "#" + index;
            }
            plan.Input = input;
            plan.Notes.AddRange(input.Notes);
            plan.Stem = Stem(name ?? input.Model?.Name);
            string folder = McpScript.NormalisePath(string.IsNullOrWhiteSpace(options.Name) ? DefaultFolder + "\\" + plan.Stem : options.Name);
            plan.BreakablePath = folder;
            plan.DebrisFolder = string.IsNullOrWhiteSpace(options.DebrisFolder) ? folder + "_Debris" : McpScript.NormalisePath(options.DebrisFolder);
            if (input.Size.Length() < 0.05f) throw new InvalidOperationException("It is too small to break (" + Math.Round(input.Size.Length() * 100, 1) + " cm across).");

            FractureOptions cut = new FractureOptions() { Pieces = options.Pieces, Seed = options.Seed, Interior = options.Interior, SkinDepth = options.InnerSkin ? Math.Min(0.003, input.Size.Length() * 0.005) : 0 };
            plan.Pieces = FractureGeometry.Break(input.Source, cut, plan.Notes);

            //Every piece's model and hull, before the level changes
            List<string> taken = new List<string>();
            double totalVolume = 0;
            foreach (FracturePiece piece in plan.Pieces)
            {
                PiecePlan p = new PiecePlan() { Piece = piece };
                p.Body = plan.Stem + "_" + (piece.Index + 1).ToString("00");
                p.Composite = plan.DebrisFolder + "\\" + p.Body;
                p.ModelName = FractureModels.PieceModelName(level, plan.Stem, piece.Index, taken);
                taken.Add(p.ModelName);
                p.Model = FractureModels.BuildPieceModel(input, piece, p.ModelName, out List<string> warnings);
                p.Warnings.AddRange(warnings.Where(w => !AlienPAK.ModelIO.IsPutRight(w)));
                List<Models.CS2.Component.LOD.Submesh> submeshes = FractureModels.Submeshes(p.Model);
                p.Submeshes = submeshes.Count;
                p.Triangles = submeshes.Sum(s => s.IndexCount / 3);
                CathodeLib.Havok.ConvexBody hull = PhysicsSystemImporter.BuildShape(FractureModels.MeshOf(piece, p.Body));
                p.HullCorners = hull.Vertices.Count;
                p.SurfaceCorners = hull.SurfaceVertices.Count;
                p.FullHullCorners = hull.FullHullVertices;
                p.Volume = hull.Volume;
                totalVolume += hull.Volume;
                p.Mass = PhysicsSystemImporter.SuggestedMass(hull);
                Vector3 o = piece.Outward;
                Vector3 d = new Vector3(o.X, 0.65f + Math.Max(0f, o.Y), o.Z);
                p.Direction = d.LengthSquared() > 1e-8f ? Vector3.Normalize(d) : Vector3.UnitY;
                plan.Plans.Add(p);
            }
            if (options.Mass.HasValue && totalVolume > 0)
                foreach (PiecePlan p in plan.Plans)
                    p.Mass = Math.Max(0.05, Math.Round(options.Mass.Value * p.Volume / totalVolume, 2));
            foreach (PiecePlan p in plan.Plans)
                p.Force = Math.Round(p.Mass * Math.Max(0, options.KickSpeed), 3);
            if (plan.Plans.Count > 12)
                plan.Notes.Add(plan.Plans.Count + " pieces: every one is a live physics body once broken; a dozen per breakable keeps a room of them cheap.");
            plan.Seconds = sw.Elapsed.TotalSeconds;
            return plan;
        }
        #endregion

        #region Reports
        public static JObject Describe(BreakablePlan plan, bool pieces = true)
        {
            JObject o = new JObject()
            {
                ["source_model"] = plan.Input.Model?.Name,
                ["source_triangles"] = plan.Input.Triangles,
                ["size"] = new JArray(Math.Round(plan.Input.Size.X, 3), Math.Round(plan.Input.Size.Y, 3), Math.Round(plan.Input.Size.Z, 3)),
                ["breakable"] = plan.BreakablePath,
                ["debris_folder"] = plan.DebrisFolder,
                ["piece_count"] = plan.Plans.Count,
                ["seconds"] = Math.Round(plan.Seconds, 2),
            };
            if (plan.CollisionMaterial != null) o["collision_material"] = plan.CollisionMaterial;
            if (pieces)
                o["pieces"] = new JArray(plan.Plans.Select(p => new JObject()
                {
                    ["composite"] = p.Composite,
                    ["body"] = p.Body,
                    ["model"] = p.ModelName,
                    ["pivot"] = new JArray(Math.Round(p.Piece.Pivot.X, 4), Math.Round(p.Piece.Pivot.Y, 4), Math.Round(p.Piece.Pivot.Z, 4)),
                    ["size"] = new JArray(Math.Round(p.Piece.Size.X, 3), Math.Round(p.Piece.Size.Y, 3), Math.Round(p.Piece.Size.Z, 3)),
                    ["triangles"] = p.Triangles,
                    ["body_triangles"] = p.Piece.BodyTriangles,
                    ["cap_triangles"] = p.Piece.CapTriangles,
                    ["skin_triangles"] = p.Piece.SkinTriangles,
                    ["caps"] = p.Piece.CapsMade,
                    ["open_cuts"] = p.Piece.OpenCuts,
                    ["submeshes"] = p.Submeshes,
                    ["hull_corners"] = p.HullCorners,
                    ["hull_surface_corners"] = p.SurfaceCorners,
                    ["full_hull_corners"] = p.FullHullCorners,
                    ["volume_litres"] = Math.Round(p.Volume * 1000.0, 3),
                    ["mass"] = p.Mass,
                    ["kick_force"] = p.Force,
                    ["kick_direction"] = new JArray(Math.Round(p.Direction.X, 3), Math.Round(p.Direction.Y, 3), Math.Round(p.Direction.Z, 3)),
                }));
            List<string> warnings = plan.Plans.SelectMany(p => p.Warnings.Select(w => p.Body + ": " + w)).Distinct().ToList();
            if (warnings.Count != 0) o["model_warnings"] = new JArray(warnings.Take(10));
            if (plan.Notes.Count != 0) o["notes"] = new JArray(plan.Notes.Distinct());
            return o;
        }
        #endregion

        #region Making it
        /// <summary>
        /// The pieces as level content: their models (not undoable, as any import), their composites (one undo step), collision and
        /// physics (each its own step; the systems and proxies cannot be undone). Returns what each became. Any thread.
        /// </summary>
        internal static JArray MakePieces(BreakableHost host, Level level, BreakablePlan plan, BreakableOptions options, Composite focus)
        {
            Commands commands = null;
            McpEditor.UI(() =>
            {
                McpEditor.RequireUndoIdle();
                commands = level.Commands;
                foreach (PiecePlan p in plan.Plans)
                {
                    string problem = CATHODE.Scripting.Refactor.CreateCompositePlan.CheckName(commands, p.Composite);
                    if (problem != null) throw new McpError(McpErrorCodes.Conflict, p.Composite + ": " + problem);
                }
                foreach (PiecePlan p in plan.Plans)
                {
                    level.Models.Entries.Add(p.Model);
                    EditHistory.AssetChanged(level, AssetKind.MODEL, p.Model.Name);
                }
            });

            host.Progress?.Invoke("piece composites");
            McpScriptEdit.Run("Make debris of " + plan.Stem, focus ?? McpEditor.UI(() => commands.EntryPoints.FirstOrDefault()), edit =>
            {
                foreach (PiecePlan p in plan.Plans)
                {
                    Composite piece = edit.AddComposite(p.Composite);
                    FunctionEntity model = edit.AddFunction(piece, FunctionType.ModelReference, p.Body);
                    //Keyed by the body's name: a spawned body finds its model and collision through that key
                    ShortGuid key = ShortGuidUtils.Generate(p.Body);
                    cResource resource = new cResource(key);
                    ResourceReference renderable = resource.AddResource(ResourceType.RENDERABLE_INSTANCE);
                    renderable.resource_id = key;
                    renderable.RenderableInstance = level.RenderableElements.EnsureRegistered(FractureModels.Run(p.Model));
                    Parameter existing = model.GetParameter(ShortGuids.resource);
                    if (existing != null) existing.content = resource;
                    else model.AddParameter("resource", resource);
                    if (options.IsDebris) edit.SetParameter(piece, model, "is_debris", new JValue(true), allowCustom: true);
                    edit.AddFunction(piece, FunctionType.PhysicsSystem, "PhysicsSystem");
                    if (p.Force > 0)
                    {
                        FunctionEntity kick = edit.AddFunction(piece, FunctionType.ThinkOnce, "Kick");
                        FunctionEntity impulse = edit.AddFunction(piece, FunctionType.PhysicsApplyImpulse, "Impulse");
                        edit.SetParameter(piece, impulse, "direction", new JArray(Math.Round(p.Direction.X, 4), Math.Round(p.Direction.Y, 4), Math.Round(p.Direction.Z, 4)), allowCustom: true);
                        edit.SetParameter(piece, impulse, "force", new JValue(p.Force), allowCustom: true);
                        edit.SetParameter(piece, impulse, "can_damage", new JValue(false), allowCustom: true);
                        edit.AddLink(piece, kick, "on_think", impulse, "trigger");
                        edit.AddLink(piece, impulse, "objects", model, "reference");
                    }
                    edit.Changed(piece);
                }
            }, new McpScriptEdit.RunOptions() { Show = false, LabelPrefix = host.LabelPrefix });

            JArray made = new JArray();
            foreach (PiecePlan p in plan.Plans)
            {
                host.Progress?.Invoke("collision and physics of " + p.Body);
                JObject collision = new JObject() { ["composite"] = p.Composite, ["entity"] = p.Body, ["proxy"] = "from_model", ["flags"] = new JArray(PhysicsPropSpawner.PropCollisionFlags) };
                if (plan.CollisionMaterial != null) collision["material"] = plan.CollisionMaterial;
                JObject col = host.Nested("set_collision", collision);
                JObject physics = host.Nested("import_physics_system", new JObject()
                {
                    ["composite"] = p.Composite,
                    ["mass"] = Math.Max(0.01, Math.Min(10000, p.Mass)),
                    ["friction"] = options.Friction,
                    ["restitution"] = options.Restitution,
                    ["key_by_name"] = true,
                });
                made.Add(new JObject()
                {
                    ["composite"] = p.Composite,
                    ["body"] = p.Body,
                    ["model"] = p.ModelName,
                    ["proxy"] = col["imported_proxy"]?["proxy"] ?? col["collision"]?["proxy"],
                    ["physics_system"] = physics["system"],
                    ["hull_corners"] = physics["hull_corners"],
                    ["mass"] = physics["mass"],
                });
            }
            return made;
        }

        /// <summary>
        /// Make a placed ModelReference breakable (see the class): plan, pieces, then the breakable composite and its wiring. Returns
        /// the whole result. Any thread (a tool's, or a worker the window starts).
        /// </summary>
        internal static JObject MakeBreakable(BreakableHost host, string compositeArg, string entityArg, BreakableOptions options, bool dryRun, Action<BreakablePlan> planned = null)
        {
            Level level = null;
            Composite holder = null;
            FunctionEntity entity = null;
            string entityName = null;
            McpEditor.UI(() =>
            {
                level = McpEditor.RequireLevel(forEditing: !dryRun).Level;
                holder = McpScript.FindComposite(level.Commands, compositeArg);
                Entity found = McpScript.FindEntity(level.Commands, holder, entityArg);
                string why = WhyNot(level, holder, found);
                if (why != null) throw new McpError(McpErrorCodes.Refused, McpScript.EntityName(level.Commands, holder, found) + ": " + why);
                entity = (FunctionEntity)found;
                entityName = level.Commands.Utils.GetEntityName(holder, entity);
            });
            host.Progress?.Invoke("cutting");
            BreakablePlan plan;
            try { plan = Plan(level, holder, entity, options); }
            catch (InvalidOperationException e) { throw new McpError(McpErrorCodes.Refused, entityName + " cannot be broken: " + e.Message); }
            planned?.Invoke(plan);
            JObject result = Describe(plan);
            if (dryRun)
            {
                McpEditor.UI(() =>
                {
                    string problem = CATHODE.Scripting.Refactor.CreateCompositePlan.CheckName(level.Commands, plan.BreakablePath);
                    if (problem != null) result["would_fail"] = plan.BreakablePath + ": " + problem;
                });
                result["dry_run"] = true;
                return result;
            }
            McpEditor.UI(() =>
            {
                string problem = CATHODE.Scripting.Refactor.CreateCompositePlan.CheckName(level.Commands, plan.BreakablePath);
                if (problem != null) throw new McpError(McpErrorCodes.Conflict, plan.BreakablePath + ": " + problem + " (give 'name').");
            });

            //The kick is a direction in the world (PhysicsApplyImpulse): the model's own outward directions turned as it is placed
            McpEditor.UI(() =>
            {
                if (!(entity.GetParameter("position")?.content is cTransform placed)) return;
                const float toRadians = (float)Math.PI / 180f;
                Quaternion turn = Quaternion.CreateFromYawPitchRoll(placed.rotation.Y * toRadians, placed.rotation.X * toRadians, placed.rotation.Z * toRadians);
                foreach (PiecePlan p in plan.Plans) p.Direction = Vector3.Normalize(Vector3.Transform(p.Direction, turn));
            });
            result["made"] = MakePieces(host, level, plan, options, holder);

            //The model into a composite of its own, its ballistic half with it, an instance left where it was
            host.Progress?.Invoke("grouping");
            List<ShortGuid> partners = McpEditor.UI(() => CollisionPartners(level, holder, entity).Select(o => o.shortGUID).ToList());
            JArray grouping = new JArray(McpScript.Id(entity.shortGUID));
            foreach (ShortGuid partner in partners) grouping.Add(McpScript.Id(partner));
            JObject grouped = host.Nested("group_into_composite", new JObject() { ["composite"] = holder.name, ["entities"] = grouping, ["name"] = plan.BreakablePath });
            result["placed"] = grouped["instance"];
            if (partners.Count != 0) result["collision_partners"] = new JArray(partners.Select(McpScript.Id));

            host.Progress?.Invoke("wiring the break");
            JObject wiring = null;
            Composite breakable = McpEditor.UI(() => level.Commands.Entries.FirstOrDefault(o => o != null && string.Equals(o.name, plan.BreakablePath, StringComparison.OrdinalIgnoreCase)))
                ?? throw new McpError(McpErrorCodes.Failed, "Grouping did not make " + plan.BreakablePath + ".");
            McpScriptEdit.Run("Make " + entityName + " breakable", breakable, edit => wiring = Wire(edit, level, breakable, entity.shortGUID, entityName, plan, options, partners),
                new McpScriptEdit.RunOptions() { Show = host.Call != null, LabelPrefix = host.LabelPrefix });
            result["wiring"] = wiring;
            host.Notes.Add("save_level with build=true puts the breakable in the game. Its pieces are spawned by the break, so they cost nothing until then.");
            return result;
        }

        /// <summary>The break, inside the breakable composite (one undo step).</summary>
        private static JObject Wire(McpScriptEdit edit, Level level, Composite breakable, ShortGuid intactId, string intactName, BreakablePlan plan, BreakableOptions options, List<ShortGuid> partners)
        {
            Commands commands = edit.Commands;
            FunctionEntity intact = breakable.GetEntityByID(intactId) as FunctionEntity
                ?? breakable.functions.FirstOrDefault(o => o.function == FunctionType.ModelReference && string.Equals(commands.Utils.GetEntityName(breakable, o), intactName, StringComparison.OrdinalIgnoreCase))
                ?? throw new McpError(McpErrorCodes.Failed, intactName + " is not in " + breakable.name + " after grouping.");
            FunctionEntity half = CollisionPairs.FindSibling(commands, breakable, intact);

            //Where the model sits in the breakable: each piece spawns at its pivot carried by that placement
            cTransform at = intact.GetParameter("position")?.content as cTransform ?? new cTransform();
            const float toRadians = (float)Math.PI / 180f;
            Quaternion turn = Quaternion.CreateFromYawPitchRoll(at.rotation.Y * toRadians, at.rotation.X * toRadians, at.rotation.Z * toRadians);
            Vector3 origin = new Vector3(at.position.X, at.position.Y, at.position.Z);
            Vector3 Place(Vector3 local) => origin + Vector3.Transform(local, turn);
            JArray V(Vector3 v) => new JArray(Math.Round(v.X, 4), Math.Round(v.Y, 4), Math.Round(v.Z, 4));
            JArray R() => new JArray(Math.Round(at.rotation.X, 3), Math.Round(at.rotation.Y, 3), Math.Round(at.rotation.Z, 3));

            VariableEntity breakPin = edit.AddVariable(breakable, BreakPin, CompositePinType.CompositeMethodPin, null);
            VariableEntity brokenPin = edit.AddVariable(breakable, BrokenPin, CompositePinType.CompositeTargetPin, null);
            FunctionEntity once = edit.AddFunction(breakable, FunctionType.LogicOnce, OnceName);
            edit.AddLink(breakable, breakPin, BreakPin, once, "trigger");
            edit.AddLink(breakable, once, "on_success", brokenPin, BrokenPin);
            //The whole model goes: drawn and colliding (its ballistic half follows hide/disable through the pair's relays)
            edit.AddLink(breakable, once, "on_success", intact, "hide");
            edit.AddLink(breakable, once, "on_success", intact, "disable");
            JObject wired = new JObject() { ["break_pin"] = BreakPin, ["broken_pin"] = BrokenPin, ["once"] = OnceName };
            //...and its collision-only partners (a retail prop's Collision01_COL), which would stay as an invisible block
            JArray partnersOff = new JArray();
            foreach (ShortGuid id in partners ?? new List<ShortGuid>())
            {
                if (!(breakable.GetEntityByID(id) is FunctionEntity partner)) continue;
                edit.AddLink(breakable, once, "on_success", partner, "hide");
                edit.AddLink(breakable, once, "on_success", partner, "disable");
                partnersOff.Add(commands.Utils.GetEntityName(breakable, partner));
            }
            if (partnersOff.Count != 0) wired["collision_partners_disabled"] = partnersOff;

            if (options.BreakOnDamage)
            {
                List<FunctionEntity> shot = new List<FunctionEntity>() { intact };
                if (half != null) shot.Add(half);
                if (options.DamageThreshold > 0)
                {
                    FunctionEntity filter = edit.AddFunction(breakable, FunctionType.WEAPON_DamageFilter, "DamageFilter");
                    edit.SetParameter(breakable, filter, "damage_threshold", new JValue(options.DamageThreshold), allowCustom: true);
                    foreach (FunctionEntity model in shot) edit.AddLink(breakable, model, "on_damaged", filter, "impact");
                    edit.AddLink(breakable, filter, "passed", once, "trigger");
                    wired["damage_filter"] = options.DamageThreshold;
                }
                else
                    foreach (FunctionEntity model in shot) edit.AddLink(breakable, model, "on_damaged", once, "trigger");
                wired["on_damaged"] = new JArray(shot.Select(o => commands.Utils.GetEntityName(breakable, o)));
            }
            if (options.HitMonitor)
            {
                Vector3 centre = (plan.Input.Min + plan.Input.Max) * 0.5f;
                float radius = Math.Max(0.3f, plan.Input.Size.Length() * 0.5f);
                FunctionEntity marker = edit.AddFunction(breakable, FunctionType.PositionMarker, "HitCentre");
                edit.SetParameter(breakable, marker, "position", new JObject() { ["position"] = V(Place(centre)), ["rotation"] = R() }, allowCustom: true);
                FunctionEntity monitor = edit.AddFunction(breakable, FunctionType.AreaHitMonitor, "HitMonitor");
                edit.SetParameter(breakable, monitor, "SphereRadius", new JValue(Math.Round(radius, 3)), allowCustom: true);
                edit.SetParameter(breakable, monitor, "start_on_reset", new JValue(true), allowCustom: true);
                edit.AddLink(breakable, monitor, "SpherePos", marker, "reference");
                edit.AddLink(breakable, monitor, "on_pistol_hit", once, "trigger");
                edit.AddLink(breakable, monitor, "on_shotgun_hit", once, "trigger");
                edit.AddLink(breakable, once, "on_success", monitor, "stop");
                wired["hit_monitor_radius"] = Math.Round(radius, 3);
            }

            FunctionEntity cleanup = null;
            if (options.DespawnAfter > 0)
            {
                cleanup = edit.AddFunction(breakable, FunctionType.LogicDelay, "DespawnDelay");
                edit.SetParameter(breakable, cleanup, "delay", new JValue(Math.Round(options.DespawnAfter, 2)), allowCustom: true);
                edit.AddLink(breakable, once, "on_success", cleanup, "trigger");
                wired["despawn_after"] = options.DespawnAfter;
            }

            //The pieces come a moment after the model goes: spawned in the same frame, they start inside its collision while the game
            //is still switching it off and are thrown out far harder than their kick
            FunctionEntity spawnDelay = edit.AddFunction(breakable, FunctionType.LogicDelay, "SpawnDelay");
            edit.SetParameter(breakable, spawnDelay, "delay", new JValue(SpawnDelaySeconds), allowCustom: true);
            edit.AddLink(breakable, once, "on_success", spawnDelay, "trigger");
            wired["spawn_delay"] = SpawnDelaySeconds;
            JArray spawners = new JArray();
            foreach (PiecePlan p in plan.Plans)
            {
                Composite piece = commands.Entries.FirstOrDefault(o => o != null && string.Equals(o.name, p.Composite, StringComparison.OrdinalIgnoreCase))
                    ?? throw new McpError(McpErrorCodes.Failed, p.Composite + " is missing.");
                FunctionEntity asset = edit.AddInstance(breakable, piece, p.Body + "_Asset");
                edit.SetParameter(breakable, asset, "is_template", new JValue(true), allowCustom: true);
                //A spawned asset appears where its template instance is placed, not where the spawner is (retail's vending machines
                //place their template instances where the snack drops and leave the spawners at the origin)
                edit.SetParameter(breakable, asset, "position", new JObject() { ["position"] = V(Place(p.Piece.Pivot)), ["rotation"] = R() }, allowCustom: true);
                string spawnName = "Spawn_" + p.Body;
                FunctionEntity spawner = edit.AddFunction(breakable, FunctionType.AssetSpawner, spawnName);
                edit.SetParameter(breakable, spawner, "position", new JObject() { ["position"] = V(Place(p.Piece.Pivot)), ["rotation"] = R() }, allowCustom: true);
                edit.SetParameter(breakable, spawner, "spawn_on_load", new JValue(false), allowCustom: true);
                edit.SetParameter(breakable, spawner, "spawn_on_reset", new JValue(false), allowCustom: true);
                foreach (string flag in new[] { "allow_physics", "allow_forced_despawn", "persist_on_callback" })
                    edit.SetParameter(breakable, spawner, flag, new JValue(true), allowCustom: true);
                edit.AddLink(breakable, spawner, "asset", asset, "reference");
                edit.AddLink(breakable, spawnDelay, "on_delay_finished", spawner, "spawn");
                if (cleanup != null) edit.AddLink(breakable, cleanup, "on_delay_finished", spawner, "despawn");
                spawners.Add(spawnName);
            }
            wired["spawners"] = spawners;
            return wired;
        }

        /// <summary>The debris-only path (the Model Editor's Fracture, MCP fracture_model): pieces as spawnable assets, nothing placed. Any thread.</summary>
        internal static JObject MakeDebris(BreakableHost host, Models.CS2 model, BreakableOptions options, bool dryRun, Action<BreakablePlan> planned = null)
        {
            Level level = McpEditor.UI(() => McpEditor.RequireLevel(forEditing: !dryRun).Level);
            host.Progress?.Invoke("cutting");
            BreakablePlan plan;
            try { plan = PlanModel(level, model, options); }
            catch (InvalidOperationException e) { throw new McpError(McpErrorCodes.Refused, model.Name + " cannot be broken: " + e.Message); }
            planned?.Invoke(plan);
            JObject result = Describe(plan);
            result.Remove("breakable");
            if (dryRun) { result["dry_run"] = true; return result; }
            result["made"] = MakePieces(host, level, plan, options, null);
            host.Notes.Add("Each piece is a spawnable physics asset: place an is_template instance of one at the piece's pivot relative to where the whole model sits (a spawned asset appears where its template instance is, not where the spawner is) and an AssetSpawner (allow_physics) whose asset is that instance spawns it there; save_level with build=true puts them in the game.");
            return result;
        }
        #endregion
    }
}

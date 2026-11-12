using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.Undo;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.Linq;
using AssetKind = CathodeLib.AssetModificationInfoTable.AssetKind;
using Submesh = CATHODE.Models.CS2.Component.LOD.Submesh;

namespace OpenCAGE.Brushwork
{
    /// <summary>Why a brush could not be made or changed (shown to the user as it is).</summary>
    public sealed class BrushException : Exception
    {
        public BrushException(string message) : base(message) { }
    }

    /// <summary>
    /// Hammer-style brushes: boxes and planes made and resized in the editor, each drawn by a ModelReference entity with a model
    /// generated from it (<see cref="BrushModelBuilder"/>) and collision made from its triangles (<see cref="BrushCollision"/>).
    /// What each brush is lives in the level's BRUSHES custom table (by composite and entity), so it stays editable after a save.
    /// Every change is one undo step. UI thread.
    /// </summary>
    public static class BrushService
    {
        /// <summary>A brush was made, changed, or put back by undo/redo: (composite, entity).</summary>
        public static event Action<ShortGuid, ShortGuid> Changed;

        static BrushService()
        {
            //Brush rows hold proxy ordinals: they follow a collision compaction (also hooked at start, see BrushHandleSync)
            BrushCollision.Initialise();
        }

        internal static void RaiseChanged(ShortGuid composite, ShortGuid entity) => Changed?.Invoke(composite, entity);

        #region Reading
        /// <summary>
        /// The brush an entity draws, or null. An entity with no row of its own that draws a brush's model - a pasted, duplicated
        /// or copied brush - is that brush (it gets its own row, and its own model, the first time it is changed).
        /// </summary>
        public static BrushDefinition Get(Level level, Composite composite, Entity entity)
        {
            if (level?.Commands == null || composite == null || !(entity is FunctionEntity function) || function.function != FunctionType.ModelReference)
                return null;
            BrushDefinition own = BrushDefinition.FromJson(level.Commands.Utils.GetBrush(composite.shortGUID, entity.shortGUID));
            if (own != null)
                return own;

            Models.CS2 model = DrawnModel(level, function);
            if (model == null || !BrushModelBuilder.IsBrushModel(model.Name))
                return null;
            foreach (BrushTable.Brush row in level.Commands.Utils.GetBrushes())
            {
                BrushDefinition other = BrushDefinition.FromJson(row.definition);
                if (other != null && string.Equals(other.Model, model.Name, StringComparison.OrdinalIgnoreCase))
                    return other;
            }
            return null;
        }

        public static bool IsBrush(Level level, Composite composite, Entity entity) => Get(level, composite, entity) != null;

        /// <summary>Whether the entity's own row is in the table (false for a copy not yet changed).</summary>
        public static bool HasOwnRow(Level level, Composite composite, Entity entity) => level?.Commands?.Utils.GetBrush(composite.shortGUID, entity.shortGUID) != null;

        /// <summary>Every brush: its composite and entity (those still in the level), with its definition.</summary>
        public static List<(Composite composite, FunctionEntity entity, BrushDefinition brush)> All(Level level)
        {
            List<(Composite, FunctionEntity, BrushDefinition)> found = new List<(Composite, FunctionEntity, BrushDefinition)>();
            if (level?.Commands == null) return found;
            HashSet<(ShortGuid, ShortGuid)> seen = new HashSet<(ShortGuid, ShortGuid)>();
            foreach (BrushTable.Brush row in level.Commands.Utils.GetBrushes())
            {
                Composite composite = level.Commands.GetComposite(row.composite);
                if (!(composite?.GetEntityByID(row.entity) is FunctionEntity entity)) continue;
                BrushDefinition brush = BrushDefinition.FromJson(row.definition);
                if (brush == null) continue;
                found.Add((composite, entity, brush));
                seen.Add((row.composite, row.entity));
            }
            //Copies of brushes that have not been changed yet (no row of their own)
            HashSet<string> models = new HashSet<string>(found.Select(o => o.Item3.Model).Where(o => o != null), StringComparer.OrdinalIgnoreCase);
            if (models.Count == 0) return found;
            foreach (Composite composite in level.Commands.Entries)
            {
                if (composite == null) continue;
                foreach (FunctionEntity entity in composite.functions)
                {
                    if (entity.function != FunctionType.ModelReference || seen.Contains((composite.shortGUID, entity.shortGUID))) continue;
                    Models.CS2 model = DrawnModel(level, entity);
                    if (model == null || !models.Contains(model.Name)) continue;
                    BrushDefinition copy = Get(level, composite, entity);
                    if (copy != null) found.Add((composite, entity, copy));
                }
            }
            return found;
        }

        /// <summary>The model an entity's renderable draws (the one holding its run's first submesh), or null.</summary>
        public static Models.CS2 DrawnModel(Level level, FunctionEntity entity)
        {
            Submesh first = entity?.GetResource(ResourceType.RENDERABLE_INSTANCE, true)?.RenderableInstance?.FirstOrDefault(o => o?.Model != null)?.Model;
            if (first == null || level?.Models == null) return null;
            foreach (Models.CS2 model in level.Models.Entries)
            {
                if (model == null || !BrushModelBuilder.IsBrushModel(model.Name)) continue;
                foreach (Models.CS2.Component component in model.Components)
                    foreach (Models.CS2.Component.LOD lod in component.LODs)
                        if (lod.Submeshes.Contains(first))
                            return model;
            }
            return null;
        }

        public static Models.CS2 FindModel(Level level, string name)
        {
            if (string.IsNullOrEmpty(name) || level?.Models == null) return null;
            return level.Models.Entries.FirstOrDefault(o => o != null && string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Whether anything but this entity draws the model (a pasted copy, a placement by hand): then it is not rebuilt in place.</summary>
        public static bool DrawnByOthers(Level level, Models.CS2 model, Composite composite, Entity entity) => OtherDrawers(level, model, composite, entity).Count != 0;

        /// <summary>The entities other than this one that draw the model.</summary>
        public static List<(Composite composite, FunctionEntity entity)> OtherDrawers(Level level, Models.CS2 model, Composite composite, Entity entity)
        {
            List<(Composite, FunctionEntity)> found = new List<(Composite, FunctionEntity)>();
            HashSet<Submesh> submeshes = new HashSet<Submesh>(model.Components.SelectMany(c => c.LODs).SelectMany(l => l.Submeshes));
            foreach (Composite holder in level.Commands.Entries)
            {
                if (holder == null) continue;
                foreach (FunctionEntity other in holder.functions)
                {
                    if (holder == composite && other == entity) continue;
                    List<RenderableElements.Element> run = other.GetResource(ResourceType.RENDERABLE_INSTANCE, true)?.RenderableInstance;
                    if (run != null && run.Any(o => o?.Model != null && submeshes.Contains(o.Model)))
                        found.Add((holder, other));
                }
            }
            return found;
        }
        #endregion

        #region Making and changing
        /// <summary>The level material each face name resolves to (null = the grid, made now if needed).</summary>
        private static Func<string, Materials.Material> Resolver(Level level, BrushDefinition brush, List<string> notes)
        {
            Materials.Material grid = null;
            if (brush.FaceTextures.Any(o => string.IsNullOrEmpty(o.Material) || o.Material == BrushMaterials.GridName))
            {
                grid = BrushMaterials.EnsureGrid(level, out string error);
                if (grid == null)
                {
                    notes?.Add("The grid material could not be made (" + (error ?? "no reason given") + "), so the fallback material is used.");
                    grid = Singleton.FallbackMaterial;
                }
            }
            foreach (BrushFace face in brush.FaceTextures)
                if (!string.IsNullOrEmpty(face.Material) && face.Material != BrushMaterials.GridName && BrushMaterials.Find(level, face.Material) == null)
                    throw new BrushException("The level has no material called '" + face.Material + "'.");
            return name => string.IsNullOrEmpty(name) || name == BrushMaterials.GridName ? grid : BrushMaterials.Find(level, name);
        }

        /// <summary>The material the brush's collision sounds like: the one most of its faces use.</summary>
        private static Materials.Material MainMaterial(BrushDefinition brush, Func<string, Materials.Material> resolve)
        {
            return brush.FaceTextures.GroupBy(o => o.Material ?? "").OrderByDescending(o => o.Count()).Select(o => resolve(o.Key)).FirstOrDefault(o => o != null);
        }

        /// <summary>
        /// The world origin in the space of a brush placed at <paramref name="placement"/> in <paramref name="composite"/> (through the
        /// composite's first placement in the level, when it is not the root): the texture anchor that lines its grid up with the
        /// world's, and so with every other brush aligned the same way.
        /// </summary>
        public static System.Numerics.Vector3 WorldAlignedOrigin(Commands commands, Composite composite, cTransform placement)
        {
            cTransform world = placement ?? new cTransform();
            try
            {
                if (commands != null && composite != commands.EntryPoints[0])
                {
                    cTransform frame = MCP.McpSpatialTools.FrameOf(commands, composite, null, "'composite'", out Newtonsoft.Json.Linq.JObject _);
                    if (frame != null) world = InstanceTransform.Compose(frame, world);
                }
            }
            catch (Exception)
            {
                //A composite placed nowhere: its own space is as good as any
            }
            return BrushEditing.WorldOriginInBrushSpace(world);
        }

        /// <summary>A new brush's starting shape: a 2 m box standing on its origin, or a 4 m square plane centred on it.</summary>
        public static BrushDefinition NewDefault(BrushKind kind)
        {
            return kind == BrushKind.Plane
                ? BrushDefinition.NewPlane(new System.Numerics.Vector3(-2, 0, -2), new System.Numerics.Vector3(2, 0, 2), null, false)
                : BrushDefinition.NewBox(new System.Numerics.Vector3(-1, 0, -1), new System.Numerics.Vector3(1, 2, 1), null);
        }

        /// <summary>A name for a new brush entity in the composite: Brush_Box_1, Brush_Plane_2...</summary>
        public static string NewName(Commands commands, Composite composite, BrushKind kind)
        {
            HashSet<string> taken = new HashSet<string>(composite.GetEntities().Select(o => commands.Utils.GetEntityName(composite, o)), StringComparer.OrdinalIgnoreCase);
            string stem = "Brush_" + kind;
            for (int i = 1; ; i++)
                if (!taken.Contains(stem + "_" + i))
                    return stem + "_" + i;
        }

        /// <summary>
        /// Make a brush: a ModelReference in <paramref name="composite"/> at <paramref name="placement"/>, drawing a model made
        /// from it, with its collision, recorded in the BRUSHES table. One undo step (the model and any proxy stay in the level
        /// after an undo, unused). Throws <see cref="BrushException"/> with the reason when it cannot be made.
        /// </summary>
        public static FunctionEntity Create(Level level, Composite composite, BrushDefinition brush, cTransform placement, string name, string label, List<string> notes)
        {
            Commands commands = level?.Commands ?? throw new BrushException("No level is loaded.");
            if (composite == null || !commands.Entries.Contains(composite)) throw new BrushException("That composite is not in the level.");
            string problem = brush.Problem();
            if (problem != null) throw new BrushException(problem);
            if (!BrushCollision.IsMode(brush.Collision)) throw new BrushException("Collision is solid, world, ballistic or none.");
            Func<string, Materials.Material> resolve = Resolver(level, brush, notes);
            placement = placement ?? new cTransform();
            name = string.IsNullOrWhiteSpace(name) ? NewName(commands, composite, brush.Kind) : name.Trim();
            label = label ?? "Create " + name;

            using (UndoStack.Current.BeginGroup(label))
            {
                Singleton.OnEntityAddPending?.Invoke();
                FunctionEntity entity = composite.AddFunction(FunctionType.ModelReference);
                commands.Utils.SetEntityName(composite, entity, name);
                Parameter position = entity.AddParameter("position", new cTransform(placement.position, placement.rotation));
                ParameterModificationTracker.SetParameterModified(composite.shortGUID, entity.shortGUID, position.name);

                Models.CS2 model;
                List<string> warnings;
                try { model = BrushModelBuilder.Build(brush, BrushModelBuilder.NewModelName(level, entity.shortGUID), resolve, out warnings); }
                catch (Exception e) when (!(e is BrushException))
                {
                    composite.RemoveFunction(entity.shortGUID);
                    throw new BrushException(e.Message);
                }
                notes?.AddRange(warnings.Where(o => !AlienPAK.ModelIO.IsPutRight(o)));
                level.Models.Entries.Add(model);
                EditHistory.AssetChanged(level, AssetKind.MODEL, model.Name);
                brush.Model = model.Name;

                cResource resource = new cResource(entity.shortGUID);
                ResourceReference renderable = resource.AddResource(ResourceType.RENDERABLE_INSTANCE);
                renderable.RenderableInstance = level.RenderableElements.EnsureRegistered(BrushModelBuilder.Run(model, BrushModelBuilder.Submeshes(model).Count));
                entity.AddParameter("resource", resource);

                UndoStack.Current.Record(new EntityAddEdit(composite, entity, label));
                Singleton.OnEntityAdded?.Invoke(entity);

                BrushCollision.Apply(level, composite, entity, brush, BrushGeometry.Build(brush), MainMaterial(brush, resolve), label, notes);
                UndoStack.Current.Apply(new BrushRowEdit(composite, entity, null, brush.ToJson(), label));
                Singleton.OnResourceModified?.Invoke();
                ResendAfterSync(entity);
                return entity;
            }
        }

        /// <summary>
        /// Change a brush to <paramref name="brush"/>: its model rebuilt (in place, unless something else draws it too, when it
        /// gets a model of its own), its run and collision brought in step, its row replaced. One undo step.
        /// </summary>
        public static void Apply(Level level, Composite composite, FunctionEntity entity, BrushDefinition brush, string label, List<string> notes)
        {
            Commands commands = level?.Commands ?? throw new BrushException("No level is loaded.");
            if (composite == null || composite.GetEntityByID(entity.shortGUID) != entity) throw new BrushException("That entity is not in the composite.");
            string beforeJson = commands.Utils.GetBrush(composite.shortGUID, entity.shortGUID);
            BrushDefinition before = beforeJson != null ? BrushDefinition.FromJson(beforeJson) : Get(level, composite, entity);
            if (before == null) throw new BrushException(commands.Utils.GetEntityName(composite, entity) + " is not a brush.");
            //Nothing changed: no rebuild, and no undo step
            if (beforeJson != null && beforeJson == brush.ToJson())
                return;
            string problem = brush.Problem();
            if (problem != null) throw new BrushException(problem);
            if (!BrushCollision.IsMode(brush.Collision)) throw new BrushException("Collision is solid, world, ballistic or none.");
            Func<string, Materials.Material> resolve = Resolver(level, brush, notes);
            label = label ?? "Edit " + commands.Utils.GetEntityName(composite, entity);

            using (UndoStack.Current.BeginGroup(label))
            {
                Models.CS2 model = FindModel(level, brush.Model ?? before.Model);
                Models.CS2 built;
                List<string> warnings;
                try { built = BrushModelBuilder.Build(brush, model?.Name ?? BrushModelBuilder.NewModelName(level, entity.shortGUID), resolve, out warnings); }
                catch (Exception e) when (!(e is BrushException)) { throw new BrushException(e.Message); }
                notes?.AddRange(warnings.Where(o => !AlienPAK.ModelIO.IsPutRight(o)));
                int count = BrushModelBuilder.Submeshes(built).Count;

                List<(Composite composite, FunctionEntity entity)> others = model == null ? new List<(Composite, FunctionEntity)>() : OtherDrawers(level, model, composite, entity);
                if (model == null || others.Count != 0)
                {
                    /* Copies not changed yet know they are brushes by drawing this brush's model, found through the row naming it.
                       That row is about to name another model, so each copy gets a row of its own first, as the brush is now. */
                    if (beforeJson != null)
                        foreach ((Composite holder, FunctionEntity other) in others)
                            if (!HasOwnRow(level, holder, other))
                                UndoStack.Current.Apply(new BrushRowEdit(holder, other, null, before.ToJson(), label));

                    //A copy's first change, or a model gone: its own model from here on
                    if (model != null) built.Name = BrushModelBuilder.NewModelName(level, entity.shortGUID);
                    level.Models.Entries.Add(built);
                    EditHistory.AssetChanged(level, AssetKind.MODEL, built.Name);
                    model = built;
                    //The proxies it names are the ones the brush it was copied from uses: it gets its own
                    brush.CollisionHash = null;
                    brush.WorldProxy = brush.BallisticProxy = -1;
                    brush.SupersededProxies.Clear();
                }
                else
                {
                    BrushModelBuilder.ModelState was = BrushModelBuilder.ModelState.Of(model);
                    BrushModelBuilder.CopyInto(built, model);
                    UndoStack.Current.Record(new BrushModelEdit(model, was, BrushModelBuilder.ModelState.Of(model), composite, entity, label));
                    EditHistory.AssetChanged(level, AssetKind.MODEL, model.Name);
                }
                brush.Model = model.Name;

                List<RenderableElements.Element> run = level.RenderableElements.EnsureRegistered(BrushModelBuilder.Run(model, count));
                SetRun(composite, entity, run, label);

                BrushCollision.Apply(level, composite, entity, brush, BrushGeometry.Build(brush), MainMaterial(brush, resolve), label, notes);
                UndoStack.Current.Apply(new BrushRowEdit(composite, entity, beforeJson, brush.ToJson(), label));
                Singleton.OnResourceModified?.Invoke();
                ResendAfterSync(entity);
            }
        }

        /// <summary>The entity's renderable made <paramref name="run"/> (one undo step of the caller's group), unless it already is.</summary>
        private static void SetRun(Composite composite, FunctionEntity entity, List<RenderableElements.Element> run, string label)
        {
            Parameter existing = entity.GetParameter(ShortGuids.resource);
            cResource current = existing?.content as cResource;
            ResourceReference live = current?.value.FirstOrDefault(o => o != null && o.resource_type == ResourceType.RENDERABLE_INSTANCE);
            if (live?.RenderableInstance != null && live.RenderableInstance.Count == run.Count && live.RenderableInstance.Zip(run, (a, b) => a == b).All(o => o))
                return;

            bool had = existing != null;
            int index = had ? entity.parameters.IndexOf(existing) : entity.parameters.Count;
            cResource after = current != null ? (cResource)ParameterValues.Clone(current) : new cResource(entity.shortGUID);
            ResourceReference renderable = after.value.FirstOrDefault(o => o != null && o.resource_type == ResourceType.RENDERABLE_INSTANCE);
            if (renderable == null)
            {
                renderable = new ResourceReference(ResourceType.RENDERABLE_INSTANCE) { resource_id = after.shortGUID };
                after.value.Insert(0, renderable);
            }
            renderable.RenderableInstance = run;
            Parameter parameter = existing ?? new Parameter(ShortGuids.resource, new cResource(entity.shortGUID), ParameterVariant.INTERNAL);
            UndoStack.Current.Apply(new ResourceSessionEdit(composite, entity, parameter, had, index, ParameterValues.Clone(existing?.content), existing?.variant ?? ParameterVariant.INTERNAL, after, label));
        }

        /// <summary>
        /// Once the viewer has the level's tables as they now stand (a new or rebuilt model), send it the entity's renderable
        /// again: what it drew before named the old tables' indexes.
        /// </summary>
        internal static void ResendAfterSync(Entity entity)
        {
            ViewerResourceSync.AfterNextSync(() =>
            {
                Parameter resource = entity?.GetParameter(ShortGuids.resource);
                if (resource != null)
                    Singleton.OnEntityParameterModified?.Invoke(entity, resource, false);
            });
        }
        #endregion
    }

    /// <summary>A brush's row in the BRUSHES table: its definition before and after (null = no row).</summary>
    public sealed class BrushRowEdit : IEdit
    {
        private readonly string _before, _after;
        public string Label { get; }
        public ShortGuid CompositeId { get; }
        public ShortGuid EntityId { get; }

        public BrushRowEdit(Composite composite, Entity entity, string before, string after, string label)
        {
            CompositeId = composite.shortGUID;
            EntityId = entity.shortGUID;
            _before = before;
            _after = after;
            Label = label;
        }

        public void Apply(UndoContext context) => Set(context, _after);
        public void Revert(UndoContext context) => Set(context, _before);

        private void Set(UndoContext context, string definition)
        {
            Commands commands = context?.Commands ?? Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;
            if (commands == null) return;
            commands.Utils.SetBrush(CompositeId, EntityId, definition);
            DirtyTracker.MarkLevelDataModified();
            BrushService.RaiseChanged(CompositeId, EntityId);
        }

        public bool TryMerge(IEdit next) => false;
    }

    /// <summary>A brush's model rebuilt in place: its submeshes' contents before and after.</summary>
    public sealed class BrushModelEdit : IEdit, IChangesComposites
    {
        private readonly Models.CS2 _model;
        private readonly BrushModelBuilder.ModelState _before, _after;
        public string Label { get; }
        public ShortGuid CompositeId { get; }
        public ShortGuid EntityId { get; }
        public IEnumerable<ShortGuid> ChangedComposites => Enumerable.Empty<ShortGuid>();

        public BrushModelEdit(Models.CS2 model, BrushModelBuilder.ModelState before, BrushModelBuilder.ModelState after, Composite composite, Entity entity, string label)
        {
            _model = model;
            _before = before;
            _after = after;
            CompositeId = composite.shortGUID;
            EntityId = entity.shortGUID;
            Label = label;
        }

        public void Apply(UndoContext context) => Set(context, _after);
        public void Revert(UndoContext context) => Set(context, _before);

        private void Set(UndoContext context, BrushModelBuilder.ModelState state)
        {
            state.ApplyTo(_model);
            DirtyTracker.MarkLevelDataModified();
            Singleton.OnResourceModified?.Invoke();
            Entity entity = context?.Entity(CompositeId, EntityId);
            if (entity != null) BrushService.ResendAfterSync(entity);
        }

        public bool TryMerge(IEdit next) => false;
    }
}

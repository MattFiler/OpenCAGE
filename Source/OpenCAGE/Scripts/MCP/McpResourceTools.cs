using CATHODE;
using CATHODE.Enums;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using CathodeLib.Havok;
using Newtonsoft.Json.Linq;
using OpenCAGE.DockPanels;
using OpenCAGE.Undo;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// Entity resources - what an entity draws, its collision, its physics system, its animated model - and the
    /// level's collision proxies and physics systems they point at, and a Character's appearance.
    /// </summary>
    /// <remarks>
    /// Resource changes go through the editor's own undo record for the Edit Resources dialog
    /// (<see cref="ResourceSessionEdit"/>), so each call is one step exactly as a dialog session is. A changed
    /// collision mapping is written as a new row rather than into the shared one, so the step undoes cleanly and
    /// other entities sharing the old row keep it. New collision proxies and physics systems go into the level's
    /// Havok files in memory; like the editor's own import, that part cannot be undone.
    /// </remarks>
    internal static class McpResourceTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "get_entity_resources",
                Title = "Get entity resources",
                Description = "An entity's resources: the model and per-submesh materials it draws (RENDERABLE_INSTANCE), its collision (proxy, flags, physics material, mapping), physics system and animated model, from its 'resource' parameter and its own resource list. Says whether they can be edited (generated and marker-only types cannot). Aliases and proxies are followed to their target.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite holding the entity (path or id; 'root' for the level's root).", required: true),
                    McpSchema.String("entity", "The entity's id or name.", required: true)),
                ReadOnly = true,
                Idempotent = true,
                Run = GetEntityResources,
            };

            yield return new McpTool()
            {
                Name = "set_renderable",
                Title = "Set renderable",
                Description = "Change the model an entity draws (a ModelReference or another renderable), and/or its submeshes' materials, or remove its renderable. The model's LOD0 submeshes are drawn, lower LODs hung off the first. One undo step, like the inspector's Edit Resources; saved with save_level. Generated renderables (fog, particles) are refused.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite holding the entity (path or id).", required: true),
                    McpSchema.String("entity", "The entity's id or name.", required: true),
                    McpSchema.String("model", "The model to draw, by name (list_models). Leave out to keep the current one."),
                    McpSchema.Integer("part", "Which part (component) of the model, from 0 (default: the current part, or the model's first part with geometry)."),
                    McpSchema.Map("materials", "Materials to use: keys are a LOD0 submesh index, a current material name, or '*' for all; values are level material names (list_materials)."),
                    McpSchema.Boolean("remove", "Remove the renderable instead.")),
                Destructive = true,
                Idempotent = true,
                Run = SetRenderable,
            };

            yield return new McpTool()
            {
                Name = "set_collision",
                Title = "Set collision",
                Description = "Give an entity collision or change it: the collision proxy it uses (index from list_collision_proxies, 'none', or 'from_model' to import one from the entity's own renderable), its physics material, material mapping and flags; or remove it. One undo step (an imported proxy stays in the level); other entities sharing the old mapping keep it. Saved with save_level.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite holding the entity (path or id).", required: true),
                    McpSchema.String("entity", "The entity's id or name.", required: true),
                    McpSchema.Any("proxy", "A proxy index, 'none' to clear it, or 'from_model' to make a new proxy from the entity's own renderable (LOD0 triangles)."),
                    McpSchema.String("material", "The physics material (footsteps, impacts): a level material name (list_materials)."),
                    McpSchema.String("material_mapping", "A material mapping name from the level, or 'none'."),
                    McpSchema.Any("flags", "Collision flags replacing the current ones: names such as ['WORLD','BALLISTIC'] (the default for new collision), or a number."),
                    McpSchema.Boolean("remove", "Remove the entity's collision instead.")),
                Destructive = true,
                Run = SetCollision,
            };

            yield return new McpTool()
            {
                Name = "set_physics_system",
                Title = "Set physics system",
                Description = "Bind a PhysicsSystem entity to one of the level's physics systems (index or name, from list_physics_systems). One undo step, like the inspector's Edit Resources; saved with save_level. It cannot be left unbound: saving binds every PhysicsSystem entity to its system_index parameter's system (0 without one), so 'none' and remove are refused; delete the entity to drop it. import_physics_system makes a new system from a mesh.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite holding the PhysicsSystem entity (path or id).", required: true),
                    McpSchema.String("entity", "The PhysicsSystem entity's id or name (default: the composite's only PhysicsSystem)."),
                    McpSchema.Any("system", "The physics system: its index or its name ('none' is refused: saving would bind it again)."),
                    McpSchema.Boolean("remove", "Refused: saving puts the DYNAMIC_PHYSICS_SYSTEM reference back. Delete the entity instead.")),
                Destructive = true,
                Idempotent = true,
                Run = SetPhysicsSystem,
            };

            yield return new McpTool()
            {
                Name = "list_collision_proxies",
                Title = "List collision proxies",
                Description = "The open level's collision proxies (Havok compound shapes in COLLISION.HKX): index, instance count, shape classes, bounds and world-host role; include_users also lists the entities using each. Give 'proxy' for one, with its triangle count. set_collision assigns one; import_collision_proxy makes one.",
                InputSchema = McpSchema.Object(
                    McpSchema.Integer("proxy", "Describe just this proxy (with triangles, bounds and users)."),
                    McpSchema.String("filter", "Text the index, instance count, data offset or role must contain."),
                    McpSchema.Boolean("include_users", "Also list the entities whose collision uses each proxy."),
                    McpSchema.Integer("limit", "At most this many proxies (default 100).")),
                ReadOnly = true,
                Idempotent = true,
                Run = ListCollisionProxies,
            };

            yield return new McpTool()
            {
                Name = "import_collision_proxy",
                Title = "Import collision proxy",
                Description = "Make a new collision proxy from a mesh file (absolute path: FBX, glTF, OBJ, DAE) or a model the level holds, in COLLISION.HKX and HKX64 (in memory; saved with save_level). Not undoable and cannot be deleted: reload without saving to drop it. dry_run only reads the mesh. PC levels only. Returns the index for set_collision.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("path", "Absolute path of a mesh file."),
                    McpSchema.String("model", "Or a model the level holds (list_models)."),
                    McpSchema.Integer("part", "With 'model': just this part (default: every part)."),
                    McpSchema.Number("scale", "Scale applied to the mesh (default 1)."),
                    McpSchema.String("material", "The physics material its triangles carry (a level material name), as the collision row that will use it."),
                    McpSchema.String("collision_type", "'world' (walkable, the default) or 'ballistic' (bullets only).", options: new[] { "world", "ballistic" }),
                    McpSchema.Boolean("dry_run", "Read the mesh and report it, without writing anything.")),
                Run = ImportCollisionProxy,
            };

            yield return new McpTool()
            {
                Name = "list_physics_systems",
                Title = "List physics systems",
                Description = "The open level's physics systems (PHYSICS.HKX) by index and name; bodies:true adds each rigid body's shape, motion type, mass, friction, restitution, damping, gravity and filter. Give 'system' for one, with its preview triangle count and the PhysicsSystem entities bound to it.",
                InputSchema = McpSchema.Object(
                    McpSchema.Any("system", "Describe just this system (its index or name)."),
                    McpSchema.String("filter", "Text the index or name must contain."),
                    McpSchema.Boolean("bodies", "Include each system's rigid bodies."),
                    McpSchema.Integer("limit", "At most this many systems (default 100).")),
                ReadOnly = true,
                Idempotent = true,
                Run = ListPhysicsSystems,
            };

            yield return new McpTool()
            {
                Name = "import_physics_system",
                Title = "Import physics system",
                Description = "Make a new one-body dynamic physics system (convex hull, up to 64 corners) from a mesh file (absolute path) or a level model, in PHYSICS.HKX/HKX64 (in memory; saved with save_level; not undoable). With 'composite', names, placement and the default model come from its ModelReference, and its PhysicsSystem entity is bound to the new system (that binding is one undo step).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("path", "Absolute path of a mesh file."),
                    McpSchema.String("model", "Or a model the level holds (list_models); default: the composite's ModelReference model."),
                    McpSchema.Integer("part", "With 'model': just this part (default: every part)."),
                    McpSchema.Number("scale", "Scale applied to the mesh (default 1)."),
                    McpSchema.String("composite", "The composite the PhysicsSystem entity is in (path or id): supplies names, placement and the default model."),
                    McpSchema.String("system_name", "The system's name (ASCII; default from the composite's path)."),
                    McpSchema.String("body_name", "The rigid body's name (ASCII; default: the composite's ModelReference name)."),
                    McpSchema.Number("mass", "Kilograms, 0.01 to 10000 (default: from the hull's volume)."),
                    McpSchema.Number("friction", "0 to 10 (default 0.5)."),
                    McpSchema.Number("restitution", "Bounciness, 0 to 1 (default 0.4)."),
                    McpSchema.Boolean("place_at_model", "Put the body where the composite's ModelReference sits (default true when there is one)."),
                    McpSchema.Boolean("bind", "Bind the composite's PhysicsSystem entity to the new system (default true when it has one)."),
                    McpSchema.Boolean("dry_run", "Build the hull and report it, without writing anything.")),
                Run = ImportPhysicsSystem,
            };

            yield return new McpTool()
            {
                Name = "export_collision_mesh",
                Title = "Export collision mesh",
                Description = "Write the triangles of a collision proxy or a physics system, as the editor previews them, to an OBJ file at an absolute path, for inspection. The file is in OpenCAGE's OBJ convention, so import_collision_proxy reads it back at the same size. Refuses to replace a file unless overwrite is true. Changes nothing in the level.",
                InputSchema = McpSchema.Object(
                    McpSchema.Integer("proxy", "A collision proxy index (list_collision_proxies)."),
                    McpSchema.Integer("physics_system", "Or a physics system index (list_physics_systems)."),
                    McpSchema.String("path", "Absolute path of the .obj file to write.", required: true),
                    McpSchema.Boolean("overwrite", "Replace the file if it exists.")),
                Destructive = true,
                Idempotent = true,
                Run = ExportCollisionMesh,
            };

            yield return new McpTool()
            {
                Name = "get_character_appearance",
                Title = "Get character appearance",
                Description = "A Character entity's appearance at each place it is instanced: its accessory set (torso, legs, shoes, head, arms and collision composites), skeletons, asset type, voice actor, gender, ethnicity, build and foley. options:true adds the values each attribute takes.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite holding the Character (path or id).", required: true),
                    McpSchema.String("entity", "The Character entity's id or name.", required: true),
                    McpSchema.Boolean("options", "Also list the skeletons and enum values the attributes take."),
                    McpSchema.Integer("limit", "At most this many placements (default 50).")),
                ReadOnly = true,
                Idempotent = true,
                Run = GetCharacterAppearance,
            };

            yield return new McpTool()
            {
                Name = "set_character_appearance",
                Title = "Set character appearance",
                Description = "Change a Character's appearance (accessory set) at one placement, adding the set if it has none (another Character's set in the same composite instance is left alone), or remove it. 'components' and 'attributes' are maps; unknown keys and values are refused. One undo step; saved with save_level. get_character_appearance lists placements and allowed values.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite holding the Character (path or id).", required: true),
                    McpSchema.String("entity", "The Character entity's id or name.", required: true),
                    McpSchema.Any("placement", "Which placement: its index or instance id from get_character_appearance (default: the only one)."),
                    McpSchema.Map("components", "Keys torso, legs, shoes, head, arms, collision; values composite paths or ids, or 'none'."),
                    McpSchema.Map("attributes", "Keys gender_skeleton, face_skeleton, asset_type, voice_actor, gender, ethnicity, build, foley_torso, foley_leg, foley_footwear."),
                    McpSchema.Boolean("remove", "Remove this placement's accessory set instead.")),
                Destructive = true,
                Idempotent = true,
                Run = SetCharacterAppearance,
            };
        }

        #region Entities and their resource lists
        /// <summary>A function entity whose resources a tool reads or changes, and where the editor keeps them.</summary>
        private sealed class Target
        {
            public Level Level;
            public Commands Commands;
            public Composite Composite;
            public FunctionEntity Entity;
            public FunctionType Function;
            /// <summary>The Edit Resources dialog edits the "resource" parameter for types that declare one, the entity's own list otherwise.</summary>
            public bool ParameterMode;
            /// <summary>Why the editor would not let these resources be edited, or null.</summary>
            public string Blocked;

            public string Name => McpScript.EntityName(Commands, Composite, Entity);
            public string Label => UndoLabels.Entity(Composite, Entity);
            public cResource Parameter => Entity.GetParameter(ShortGuids.resource)?.content as cResource;
            public List<ResourceReference> Live => ParameterMode ? (Parameter?.value ?? new List<ResourceReference>()) : (Entity.resources ?? new List<ResourceReference>());
            /// <summary>What a new reference is keyed by: the dialog uses the parameter's id, or the entity's.</summary>
            public ShortGuid ResourceId => ParameterMode ? (Parameter?.shortGUID ?? Entity.shortGUID) : Entity.shortGUID;
            public ResourceReference Find(ResourceType type) => Live.FirstOrDefault(o => o != null && o.resource_type == type);

            /// <summary>
            /// The same entity, looked at where a reference of this type is kept: the usual place, unless the entity
            /// holds its reference in the other one (so a change edits that reference instead of adding a second).
            /// </summary>
            public Target For(ResourceType type)
            {
                if (Find(type) != null)
                    return this;
                bool elsewhere = ParameterMode
                    ? (Entity.resources ?? new List<ResourceReference>()).Any(o => o != null && o.resource_type == type)
                    : (Parameter?.value ?? new List<ResourceReference>()).Any(o => o != null && o.resource_type == type);
                if (!elsewhere)
                    return this;
                Target other = (Target)MemberwiseClone();
                other.ParameterMode = !ParameterMode;
                return other;
            }

            public Target InList()
            {
                if (!ParameterMode)
                    return this;
                Target other = (Target)MemberwiseClone();
                other.ParameterMode = false;
                return other;
            }
        }

        /// <summary>
        /// The function entity the call names. Aliases and proxies are followed when <paramref name="follow"/>;
        /// otherwise the caller is sent to what they point at.
        /// </summary>
        private static Target Resolve(Level level, McpCall call, bool write, bool follow = false, string entityArgument = "entity")
        {
            Commands commands = level.Commands;
            Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
            Entity entity = McpScript.FindEntity(commands, composite, call.Str(entityArgument, required: true));
            return Resolve(level, composite, entity, write, follow, call);
        }

        private static Target Resolve(Level level, Composite composite, Entity entity, bool write, bool follow, McpCall call)
        {
            Commands commands = level.Commands;
            if (entity is AliasEntity || entity is ProxyEntity)
            {
                (Composite c, Entity e) = commands.Utils.GetResolvedTarget(entity is AliasEntity alias ? commands.Utils.ResolveAlias(alias, composite) : commands.Utils.ResolveProxy((ProxyEntity)entity));
                string what = entity is AliasEntity ? "an alias" : "a proxy";
                if (e == null)
                    throw new McpError(McpScript.EntityName(commands, composite, entity) + " is " + what + " that points at nothing.");
                if (!follow)
                    throw new McpError(McpScript.EntityName(commands, composite, entity) + " is " + what + ": resources belong to what it points at, " + McpScript.EntityName(commands, c, e) + " (" + McpScript.Id(e.shortGUID) + ") in " + c.name + ". Change that entity (it changes every placement of " + c.name + ").");
                call?.Note(McpScript.EntityName(commands, composite, entity) + " is " + what + "; these are the resources of what it points at, " + McpScript.EntityName(commands, c, e) + " in " + c.name + ".");
                composite = c;
                entity = e;
            }

            FunctionEntity function = entity as FunctionEntity;
            if (function == null || !function.function.IsFunctionType)
            {
                Composite instanced = McpScript.InstancedComposite(commands, entity);
                throw new McpError(McpScript.EntityName(commands, composite, entity) + " is " + (instanced != null ? "an instance of " + instanced.name + ": resources belong to the function entities inside it (get_composite lists them)." : "a " + McpScript.Kind(entity) + ": only function entities (such as ModelReference) have resources."));
            }

            FunctionType type = function.function.AsFunctionType;
            bool parameterMode = HasResourceParameter(commands, type);
            Target target = new Target()
            {
                Level = level,
                Commands = commands,
                Composite = composite,
                Entity = function,
                Function = type,
                ParameterMode = parameterMode,
            };
            //The same rules the inspector's Resources button follows
            if (EntityInspector.FunctionIsMarkerResourceOnly(type))
                target.Blocked = type + "'s only resource is a marker the editor manages itself when the level is saved.";
            else if (EntityInspector.FunctionResourcesAreGenerated(type))
                target.Blocked = type + "'s renderable is built from its parameters when the level is saved: change those instead.";
            else if (!parameterMode && !EntityInspector.FunctionUsesEntityResourceList(type) && !(function.resources ?? new List<ResourceReference>()).Any(o => o != null && !EntityInspector.IsMarkerOnlyResourceType(o.resource_type)))
                target.Blocked = type + " takes no resources.";
            if (write && target.Blocked != null)
                throw new McpError(target.Name + " (" + type + ") has no editable resources: " + target.Blocked);
            return target;
        }

        private static bool HasResourceParameter(Commands commands, FunctionType function)
        {
            foreach ((ShortGuid name, ParameterVariant variant, DataType type) in commands.Utils.GetAllParameters(function))
                if (name == ShortGuids.resource && variant == ParameterVariant.INTERNAL && type == DataType.RESOURCE)
                    return true;
            return false;
        }

        /// <summary>
        /// Change the entity's resource references as one undo step, through the Edit Resources dialog's own record.
        /// <paramref name="change"/> edits a copy of the list; <paramref name="first"/> are level-list changes that go
        /// in the same step, ahead of it. UI thread; call <see cref="McpEditor.RequireUndoIdle"/> first.
        /// </summary>
        private static void Commit(Target target, string label, Action<List<ResourceReference>> change, params IEdit[] first)
        {
            IEdit edit;
            if (target.ParameterMode)
            {
                Parameter existing = target.Entity.GetParameter(ShortGuids.resource);
                bool had = existing != null;
                int index = had ? target.Entity.parameters.IndexOf(existing) : target.Entity.parameters.Count;
                ParameterData beforeContent = ParameterValues.Clone(existing?.content);
                ParameterVariant beforeVariant = existing?.variant ?? ParameterVariant.INTERNAL;
                cResource after = existing?.content is cResource current ? (cResource)ParameterValues.Clone(current) : new cResource(target.Entity.shortGUID);
                change(after.value);
                Parameter parameter = existing ?? new Parameter(ShortGuids.resource, new cResource(target.Entity.shortGUID), ParameterVariant.INTERNAL);
                edit = new ResourceSessionEdit(target.Composite, target.Entity, parameter, had, index, beforeContent, beforeVariant, after, label);
            }
            else
            {
                List<ResourceReference> before = ResourceSessionEdit.CloneReferences(target.Entity.resources);
                List<ResourceReference> after = ResourceSessionEdit.CloneReferences(target.Entity.resources);
                change(after);
                edit = new ResourceSessionEdit(target.Composite, target.Entity, before, after, label);
            }

            if (first == null || first.Length == 0)
            {
                UndoStack.Current.Apply(edit);
                return;
            }
            using (UndoStack.Current.BeginGroup(label))
            {
                foreach (IEdit step in first)
                    UndoStack.Current.Apply(step);
                UndoStack.Current.Apply(edit);
            }
        }

        /// <summary>Every resource reference in the level's script: (composite, entity, reference), from both places they are kept.</summary>
        private static IEnumerable<(Composite, FunctionEntity, ResourceReference)> AllReferences(Commands commands)
        {
            foreach (Composite composite in commands.Entries)
            {
                if (composite == null) continue;
                foreach (FunctionEntity function in composite.functions)
                {
                    if (function.resources != null)
                        foreach (ResourceReference reference in function.resources)
                            if (reference != null) yield return (composite, function, reference);
                    if (function.GetParameter(ShortGuids.resource)?.content is cResource resource && resource.value != null)
                        foreach (ResourceReference reference in resource.value)
                            if (reference != null) yield return (composite, function, reference);
                }
            }
        }

        private static JObject EntityBrief(Target target) => McpScript.Brief(target.Commands, target.Composite, target.Entity);
        #endregion

        #region get_entity_resources
        private static object GetEntityResources(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel(forEditing: false).Level;
                Target target = Resolve(level, call, write: false, follow: true);
                Dictionary<CollisionMaps.COLLISION_MAPPING, int> sharing = null;
                bool hasCollision = target.Entity.GetResource(ResourceType.COLLISION_MAPPING, true) != null;
                if (hasCollision)
                {
                    sharing = new Dictionary<CollisionMaps.COLLISION_MAPPING, int>(new RefComparer<CollisionMaps.COLLISION_MAPPING>());
                    foreach ((Composite _, FunctionEntity __, ResourceReference reference) in AllReferences(level.Commands))
                    {
                        if (reference.resource_type != ResourceType.COLLISION_MAPPING || reference.CollisionMapping == null) continue;
                        sharing.TryGetValue(reference.CollisionMapping, out int count);
                        sharing[reference.CollisionMapping] = count + 1;
                    }
                }

                JObject result = new JObject()
                {
                    ["composite"] = target.Composite.name,
                    ["entity"] = EntityBrief(target),
                    ["editable"] = target.Blocked == null,
                    ["edited_in"] = target.ParameterMode ? "the 'resource' parameter" : "the entity's own resource list",
                };
                if (target.Blocked != null)
                    result["not_editable_because"] = target.Blocked;

                cResource parameter = target.Parameter;
                if (parameter != null)
                    result["resource_parameter"] = new JObject()
                    {
                        ["id"] = McpScript.Id(parameter.shortGUID),
                        ["references"] = new JArray(parameter.value.Where(o => o != null).Select(o => DescribeReference(level, o, sharing))),
                    };
                if (target.Entity.resources != null && target.Entity.resources.Count != 0)
                    result["entity_resources"] = new JArray(target.Entity.resources.Where(o => o != null).Select(o => DescribeReference(level, o, sharing)));
                if (parameter == null && (target.Entity.resources == null || target.Entity.resources.Count == 0))
                    result["references"] = new JArray();
                return result;
            });
        }

        private static JObject DescribeReference(Level level, ResourceReference reference, Dictionary<CollisionMaps.COLLISION_MAPPING, int> sharing)
        {
            JObject item = new JObject()
            {
                ["type"] = reference.resource_type.ToString(),
                ["resource_id"] = McpScript.Id(reference.resource_id),
            };
            switch (reference.resource_type)
            {
                case ResourceType.RENDERABLE_INSTANCE:
                    item["renderable"] = DescribeRenderable(level, reference.RenderableInstance);
                    break;
                case ResourceType.COLLISION_MAPPING:
                    item["collision"] = DescribeMapping(reference.CollisionMapping, sharing);
                    break;
                case ResourceType.DYNAMIC_PHYSICS_SYSTEM:
                    item["physics_system"] = new JObject()
                    {
                        ["index"] = reference.PhysicsSystem != null || reference.PhysicsSystemIndex >= 0 ? (JToken)reference.PhysicsSystemIndex : JValue.CreateNull(),
                        ["name"] = reference.PhysicsSystem?.Name,
                        ["bound"] = reference.PhysicsSystem != null,
                    };
                    break;
                case ResourceType.ANIMATED_MODEL:
                    item["animated_model"] = DescribeAnimation(reference.AnimatedModel);
                    break;
                default:
                    if (EntityInspector.IsMarkerOnlyResourceType(reference.resource_type))
                        item["marker"] = "managed by the editor on save; nothing to edit";
                    break;
            }
            return item;
        }

        private static JToken DescribeRenderable(Level level, List<RenderableElements.Element> run)
        {
            if (run == null || run.Count == 0)
                return new JObject() { ["elements"] = 0 };
            Models models = level.Models;
            RenderableElements.Element first = run.FirstOrDefault(o => o?.Model != null);
            Models.CS2 model = SafeFindModel(models, first?.Model);
            Models.CS2.Component component = SafeFindComponent(models, first?.Model);

            JObject result = new JObject()
            {
                ["model"] = model?.Name ?? "?",
                ["part"] = model != null && component != null ? IndexOfReference(model.Components, component) : -1,
                ["parts"] = model?.Components.Count ?? 0,
                ["lods"] = component?.LODs.Count ?? 0,
                ["elements"] = run.Count,
                ["lower_lod_elements"] = run.Where(o => o != null).Sum(o => CountLods(o)),
            };
            JArray submeshes = new JArray();
            for (int i = 0; i < run.Count && i < 64; i++)
            {
                RenderableElements.Element element = run[i];
                if (element == null) continue;
                JObject entry = new JObject() { ["index"] = i };
                Models.CS2.Component.LOD lod = null;
                try { lod = element.Model == null ? null : models.FindModelLOD(element.Model); } catch { }
                Models.CS2.Component owner = SafeFindComponent(models, element.Model);
                Models.CS2 elementModel = SafeFindModel(models, element.Model);
                if (elementModel != null && !ReferenceEquals(elementModel, model))
                    entry["model"] = elementModel.Name;
                if (owner != null && lod != null)
                {
                    entry["lod"] = IndexOfReference(owner.LODs, lod);
                    entry["submesh"] = IndexOfReference(lod.Submeshes, element.Model);
                }
                entry["material"] = element.Material?.Name;
                if (element.Model?.Material != null && !ReferenceEquals(element.Model.Material, element.Material))
                    entry["default_material"] = element.Model.Material.Name;
                submeshes.Add(entry);
            }
            result["submeshes"] = submeshes;
            if (run.Count > 64)
                result["submeshes_shown"] = 64;
            return result;
        }

        private static int CountLods(RenderableElements.Element element)
        {
            int count = 0;
            if (element?.LODs == null) return 0;
            foreach (RenderableElements.Element lod in element.LODs)
                count += 1 + CountLods(lod);
            return count;
        }

        private static JToken DescribeMapping(CollisionMaps.COLLISION_MAPPING mapping, Dictionary<CollisionMaps.COLLISION_MAPPING, int> sharing)
        {
            if (mapping == null)
                return JValue.CreateNull();
            JObject result = new JObject()
            {
                ["proxy"] = mapping.CollisionProxy == null ? JValue.CreateNull() : (JToken)mapping.CollisionProxy.ProxyIndex,
            };
            if (mapping.CollisionProxy != null)
                result["proxy_instances"] = mapping.CollisionProxy.Instances?.Count ?? 0;
            result["flags"] = FlagNames(mapping.Flags);
            result["flags_value"] = "0x" + ((uint)mapping.Flags).ToString("X");
            result["material"] = mapping.Material?.Name;
            result["material_mapping"] = mapping.MaterialMapping?.Name;
            if (sharing != null && sharing.TryGetValue(mapping, out int users) && users > 1)
                result["shared_by_references"] = users;
            return result;
        }

        private static JToken DescribeAnimation(EnvironmentAnimations.EnvironmentAnimation animation)
        {
            if (animation == null)
                return JValue.CreateNull();
            return new JObject()
            {
                ["id"] = animation.ID,
                ["skeleton"] = animation.SkeletonName,
                ["animation_set"] = AnimationName(animation.AnimationSet),
                ["bones"] = animation.BoneMappings?.Count ?? 0,
                ["meshes"] = animation.MeshMappings?.Count ?? 0,
            };
        }

        private static JToken AnimationName(uint id)
        {
            if (id == 0) return JValue.CreateNull();
            string name = null;
            if (Singleton.AnimationStrings_Debug?.Entries != null && Singleton.AnimationStrings_Debug.Entries.TryGetValue(id, out name)) return name;
            if (Singleton.AnimationStrings?.Entries != null && Singleton.AnimationStrings.Entries.TryGetValue(id, out name)) return name;
            return id;
        }

        private static Models.CS2 SafeFindModel(Models models, Models.CS2.Component.LOD.Submesh submesh)
        {
            if (submesh == null || models == null) return null;
            try { return models.FindModel(submesh); } catch { return null; }
        }

        private static Models.CS2.Component SafeFindComponent(Models models, Models.CS2.Component.LOD.Submesh submesh)
        {
            if (submesh == null || models == null) return null;
            try { return models.FindModelComponent(submesh); } catch { return null; }
        }

        private static int IndexOfReference<T>(IList<T> list, T item) where T : class
        {
            if (list == null) return -1;
            for (int i = 0; i < list.Count; i++)
                if (ReferenceEquals(list[i], item)) return i;
            return -1;
        }
        #endregion

        #region set_renderable
        private static object SetRenderable(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel().Level;
                McpEditor.RequireUndoIdle();
                Target target = Resolve(level, call, write: true).For(ResourceType.RENDERABLE_INSTANCE);
                ResourceReference existing = target.Find(ResourceType.RENDERABLE_INSTANCE);
                bool changes = call.Has("model") || call.Has("part") || call.Has("materials");

                if (call.Bool("remove"))
                {
                    if (changes)
                        throw new McpError("'remove' cannot be combined with model, part or materials.");
                    if (existing == null)
                        throw new McpError(target.Name + " has no renderable to remove.");
                    Commit(target, "AI: Remove renderable of " + target.Label, list => list.RemoveAll(o => o != null && o.resource_type == ResourceType.RENDERABLE_INSTANCE));
                    return new JObject() { ["entity"] = EntityBrief(target), ["removed"] = "RENDERABLE_INSTANCE" };
                }
                if (!changes)
                    throw new McpError("Say what to change: 'model', 'part', 'materials', or remove: true.");

                Models models = level.Models;
                Models.CS2.Component.LOD.Submesh firstNow = existing?.RenderableInstance?.FirstOrDefault(o => o?.Model != null)?.Model;
                Models.CS2 currentModel = SafeFindModel(models, firstNow);
                Models.CS2.Component currentComponent = SafeFindComponent(models, firstNow);

                Models.CS2 model;
                if (call.Has("model"))
                    model = FindModel(level, call.Str("model"));
                else if (currentModel != null)
                    model = currentModel;
                else
                    throw new McpError(target.Name + " does not draw a model the level holds: give 'model' (list_models shows them).");

                int part;
                if (call.Has("part"))
                {
                    part = call.Int("part");
                    if (part < 0 || part >= model.Components.Count)
                        throw new McpError(model.Name + " has " + model.Components.Count + " part(s): 'part' takes 0 to " + (model.Components.Count - 1) + ".");
                }
                else if (ReferenceEquals(model, currentModel) && currentComponent != null)
                    part = IndexOfReference(model.Components, currentComponent);
                else
                {
                    part = model.Components.FindIndex(o => o.LODs.Count != 0 && o.LODs[0].Submeshes.Count != 0);
                    int withGeometry = model.Components.Count(o => o.LODs.Count != 0 && o.LODs[0].Submeshes.Count != 0);
                    if (withGeometry > 1)
                        call.Note(model.Name + " has " + withGeometry + " parts with geometry; part " + part + " is used. Give 'part' for another (place_model places one entity per part).");
                }
                Models.CS2.Component component = part >= 0 && part < model.Components.Count ? model.Components[part] : null;
                if (component == null || component.LODs.Count == 0 || component.LODs[0].Submeshes.Count == 0)
                    throw new McpError(model.Name + (part >= 0 ? " part " + part : "") + " has no geometry to draw.");

                //What each submesh is drawn with now, kept when the model stays the same
                bool sameComponent = ReferenceEquals(component, currentComponent);
                Dictionary<Models.CS2.Component.LOD.Submesh, Materials.Material> now = new Dictionary<Models.CS2.Component.LOD.Submesh, Materials.Material>(new RefComparer<Models.CS2.Component.LOD.Submesh>());
                if (sameComponent && existing?.RenderableInstance != null)
                    CollectMaterials(existing.RenderableInstance, now);
                List<Models.CS2.Component.LOD.Submesh> lod0 = component.LODs[0].Submeshes;
                List<Models.CS2.Component.LOD.Submesh> all = component.LODs.SelectMany(o => o.Submeshes).ToList();
                Dictionary<Models.CS2.Component.LOD.Submesh, Materials.Material> start = new Dictionary<Models.CS2.Component.LOD.Submesh, Materials.Material>(new RefComparer<Models.CS2.Component.LOD.Submesh>());
                foreach (Models.CS2.Component.LOD.Submesh submesh in all)
                    start[submesh] = now.TryGetValue(submesh, out Materials.Material kept) ? kept : submesh.Material;
                Dictionary<Models.CS2.Component.LOD.Submesh, Materials.Material> chosen = new Dictionary<Models.CS2.Component.LOD.Submesh, Materials.Material>(start, new RefComparer<Models.CS2.Component.LOD.Submesh>());

                JObject overrides = call.Object("materials");
                if (overrides != null)
                {
                    //'*' first, then by old material name, then by submesh index: the most specific wins
                    List<JProperty> entries = overrides.Properties().ToList();
                    foreach (JProperty entry in entries.Where(o => o.Name.Trim() == "*"))
                    {
                        Materials.Material to = FindMaterial(level, ValueText(entry.Value, "materials['*']"));
                        foreach (Models.CS2.Component.LOD.Submesh submesh in all) chosen[submesh] = to;
                    }
                    foreach (JProperty entry in entries.Where(o => o.Name.Trim() != "*" && !int.TryParse(o.Name.Trim(), out int _)))
                    {
                        string old = entry.Name.Trim();
                        List<Models.CS2.Component.LOD.Submesh> matching = all.Where(o => string.Equals(start[o]?.Name, old, StringComparison.OrdinalIgnoreCase)).ToList();
                        if (matching.Count == 0)
                            throw new McpError("No submesh of " + model.Name + " part " + part + " uses a material called '" + old + "'. Its materials are: " + string.Join(", ", lod0.Select((o, i) => i + ": " + (start[o]?.Name ?? "(none)"))) + ".");
                        Materials.Material to = FindMaterial(level, ValueText(entry.Value, "materials['" + old + "']"));
                        foreach (Models.CS2.Component.LOD.Submesh submesh in matching) chosen[submesh] = to;
                    }
                    foreach (JProperty entry in entries.Where(o => int.TryParse(o.Name.Trim(), out int _)))
                    {
                        int index = int.Parse(entry.Name.Trim(), CultureInfo.InvariantCulture);
                        if (index < 0 || index >= lod0.Count)
                            throw new McpError(model.Name + " part " + part + " has " + lod0.Count + " submesh(es): material indexes run 0 to " + (lod0.Count - 1) + ".");
                        Materials.Material to = FindMaterial(level, ValueText(entry.Value, "materials['" + index + "']"));
                        Materials.Material was = start[lod0[index]];
                        chosen[lod0[index]] = to;
                        //The same surface on the lower LODs: the submeshes there that had the same material
                        foreach (Models.CS2.Component.LOD.Submesh lower in component.LODs.Skip(1).SelectMany(o => o.Submeshes))
                            if (ReferenceEquals(start[lower], was)) chosen[lower] = to;
                    }
                }

                //As the model importer and place_model build them: LOD0's submeshes, lower LODs hung off the first
                List<RenderableElements.Element> run = lod0.Select(o => new RenderableElements.Element() { Model = o, Material = chosen[o] }).ToList();
                for (int l = 1; l < component.LODs.Count; l++)
                    foreach (Models.CS2.Component.LOD.Submesh submesh in component.LODs[l].Submeshes)
                        run[0].LODs.Add(new RenderableElements.Element() { Model = submesh, Material = chosen[submesh] });
                run = level.RenderableElements.EnsureRegistered(run);

                ShortGuid resourceId = target.ResourceId;
                Commit(target, "AI: Set model of " + target.Label, list =>
                {
                    ResourceReference reference = list.FirstOrDefault(o => o != null && o.resource_type == ResourceType.RENDERABLE_INSTANCE);
                    if (reference == null)
                    {
                        reference = new ResourceReference(ResourceType.RENDERABLE_INSTANCE) { resource_id = resourceId };
                        list.Add(reference);
                    }
                    reference.RenderableInstance = run;
                });
                if (target.Function != FunctionType.ModelReference && existing == null)
                    call.Note(target.Function + " did not draw anything before; not every type shows a renderable in game.");
                return new JObject()
                {
                    ["entity"] = EntityBrief(target),
                    ["renderable"] = DescribeRenderable(level, target.Find(ResourceType.RENDERABLE_INSTANCE)?.RenderableInstance),
                };
            });
        }

        private static void CollectMaterials(IEnumerable<RenderableElements.Element> run, Dictionary<Models.CS2.Component.LOD.Submesh, Materials.Material> into)
        {
            foreach (RenderableElements.Element element in run)
            {
                if (element == null) continue;
                if (element.Model != null && !into.ContainsKey(element.Model))
                    into[element.Model] = element.Material;
                if (element.LODs != null)
                    CollectMaterials(element.LODs, into);
            }
        }

        private static string ValueText(JToken value, string name)
        {
            if (value == null || value.Type == JTokenType.Null)
                throw new McpError("'" + name + "' needs a material name.");
            return value.Type == JTokenType.String ? (string)value : value.ToString(Newtonsoft.Json.Formatting.None);
        }
        #endregion

        #region set_collision
        private static readonly CollisionMaps.CollisionFlags DefaultFlags = CollisionMaps.CollisionFlags.WORLD | CollisionMaps.CollisionFlags.BALLISTIC;

        private static object SetCollision(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel().Level;
                McpEditor.RequireUndoIdle();
                Target target = Resolve(level, call, write: true).For(ResourceType.COLLISION_MAPPING);
                ResourceReference existing = target.Find(ResourceType.COLLISION_MAPPING);
                CollisionMaps.COLLISION_MAPPING current = existing?.CollisionMapping;
                bool changes = call.Has("proxy") || call.Has("material") || call.Has("material_mapping") || call.Has("flags");

                if (call.Bool("remove"))
                {
                    if (changes)
                        throw new McpError("'remove' cannot be combined with proxy, material, material_mapping or flags.");
                    if (existing == null)
                        throw new McpError(target.Name + " has no collision to remove.");
                    Commit(target, "AI: Remove collision of " + target.Label, list => list.RemoveAll(o => o != null && o.resource_type == ResourceType.COLLISION_MAPPING));
                    return new JObject() { ["entity"] = EntityBrief(target), ["removed"] = "COLLISION_MAPPING" };
                }
                if (!changes && existing != null)
                    throw new McpError(target.Name + " already has collision. Say what to change: proxy, material, material_mapping, flags - or remove: true.");

                CollisionMaps.CollisionFlags flags = call.Has("flags") ? ParseFlags(call.Token("flags")) : current?.Flags ?? DefaultFlags;
                Materials.Material material = call.Has("material") ? FindMaterial(level, call.Str("material")) : current?.Material;
                MaterialMappings.MaterialMapping mapping = current?.MaterialMapping;
                if (call.Has("material_mapping"))
                    mapping = IsNone(call.Str("material_mapping")) ? null : FindMaterialMapping(level, call.Str("material_mapping"));

                HavokPackfile.StaticCompoundShape proxy = current?.CollisionProxy;
                bool proxyChanged = false;
                CollisionProxyImporter.MeshSource fromModel = null;
                if (call.Has("proxy"))
                {
                    JToken token = call.Token("proxy");
                    string text = token.Type == JTokenType.String ? ((string)token).Trim() : token.ToString();
                    proxyChanged = true;
                    if (IsNone(text))
                        proxy = null;
                    else if (string.Equals(text, "from_model", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "from_renderable", StringComparison.OrdinalIgnoreCase))
                    {
                        RequireCollision(level, forImport: true);
                        List<RenderableElements.Element> run = target.Entity.GetResource(ResourceType.RENDERABLE_INSTANCE, true)?.RenderableInstance;
                        if (run == null || run.Count == 0)
                            throw new McpError(target.Name + " draws nothing to make a collision proxy from. Give it a model first (set_renderable), or import one with import_collision_proxy.");
                        string name = SafeFindModel(level.Models, run.FirstOrDefault(o => o?.Model != null)?.Model)?.Name ?? target.Name;
                        try { fromModel = CollisionProxyImporter.FromRenderableRun(run, name); }
                        catch (Exception e) when (!(e is McpError)) { throw new McpError("No collision mesh could be made from " + target.Name + "'s renderable: " + e.Message); }
                    }
                    else if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
                    {
                        HavokPackfile hkx = RequireCollision(level, forImport: false);
                        proxy = hkx.GetCompound(index);
                        if (proxy == null)
                            throw new McpError("There is no collision proxy " + index + " (the level has " + hkx.StaticCompoundShapes.Count + "; list_collision_proxies shows them).");
                        if (ReferenceEquals(proxy, hkx.WorldHostPrimary) || ReferenceEquals(proxy, hkx.WorldHostSecondary))
                            throw new McpError("Proxy " + index + " is a world host (it holds every placed collider in the level), not a shape a mapping can use.");
                    }
                    else
                        throw new McpError("'proxy' takes a proxy index, 'none', or 'from_model'.");
                }

                //Everything is checked: only now make the new proxy, which cannot be taken back
                JObject imported = null;
                if (fromModel != null)
                {
                    uint userData = UserData(level, material);
                    uint filterInfo = (flags & CollisionMaps.CollisionFlags.WORLD) != 0 ? 3u : 9u;
                    try
                    {
                        using (McpEditorTools.Heartbeat(call, "Building a collision proxy from " + fromModel.Name))
                            proxy = CollisionProxyImporter.Import(level, fromModel, userData, filterInfo);
                    }
                    catch (Exception e) when (!(e is McpError)) { throw new McpError("The collision proxy could not be created: " + e.Message); }
                    Singleton.OnResourceModified?.Invoke();
                    imported = new JObject() { ["proxy"] = proxy.ProxyIndex, ["triangles"] = fromModel.TriangleCount, ["from"] = fromModel.Name };
                }

                //A new row for this entity, rather than writing into one other entities may share: the undo step then
                //holds the old row untouched, and the others keep it
                CollisionMaps.COLLISION_MAPPING row = new CollisionMaps.COLLISION_MAPPING()
                {
                    Flags = flags,
                    CollisionInstance = proxyChanged ? null : current?.CollisionInstance,
                    ResourceGUID = current?.ResourceGUID ?? ShortGuid.Invalid,
                    Entity = current?.Entity == null ? new EntityHandle() : new EntityHandle() { entity_id = current.Entity.entity_id, composite_instance_id = current.Entity.composite_instance_id },
                    Material = material,
                    CollisionProxy = proxy,
                    MaterialMapping = mapping,
                    ZoneID = current?.ZoneID ?? ShortGuid.Invalid,
                };
                int sharers = current == null ? 0 : AllReferences(level.Commands).Count(o => ReferenceEquals(o.Item3.CollisionMapping, current)) - 1;

                string label = (existing == null ? "AI: Add collision to " : "AI: Set collision of ") + target.Label;
                ShortGuid resourceId = target.ResourceId;
                McpLevelListEdit<CollisionMaps.COLLISION_MAPPING> addRow = new McpLevelListEdit<CollisionMaps.COLLISION_MAPPING>(l => l.CollisionMaps?.Entries, row, level.CollisionMaps.Entries.Count, true, label, target.Composite, target.Entity);
                Commit(target, label, list =>
                {
                    ResourceReference reference = list.FirstOrDefault(o => o != null && o.resource_type == ResourceType.COLLISION_MAPPING);
                    if (reference == null)
                    {
                        reference = new ResourceReference(ResourceType.COLLISION_MAPPING) { resource_id = resourceId };
                        list.Add(reference);
                    }
                    reference.CollisionMapping = row;
                }, addRow);

                JObject result = new JObject()
                {
                    ["entity"] = EntityBrief(target),
                    ["collision"] = DescribeMapping(target.Find(ResourceType.COLLISION_MAPPING)?.CollisionMapping, null),
                };
                if (imported != null)
                {
                    result["imported_proxy"] = imported;
                    call.Note("The new proxy is in the level's collision files in memory: undo takes the assignment back but not the proxy, which stays unused until the level is reloaded without saving.");
                }
                if (sharers > 0)
                    call.Note(sharers + " other reference(s) shared the old collision row; they keep it unchanged.");
                if (proxy == null)
                    call.Note("The mapping has no collision proxy, so it collides with nothing: give 'proxy'.");
                if (material == null)
                    call.Note("The mapping has no physics material: give 'material'.");
                return result;
            });
        }

        private static bool IsNone(string text) => text == null || string.Equals(text.Trim(), "none", StringComparison.OrdinalIgnoreCase) || string.Equals(text.Trim(), "null", StringComparison.OrdinalIgnoreCase) || text.Trim().Length == 0;

        private static JArray FlagNames(CollisionMaps.CollisionFlags flags)
        {
            JArray names = new JArray();
            uint rest = (uint)flags;
            foreach (CollisionMaps.CollisionFlags flag in Enum.GetValues(typeof(CollisionMaps.CollisionFlags)))
            {
                string name = flag.ToString();
                uint value = (uint)flag;
                if (value == 0 || name.EndsWith("_MASK", StringComparison.Ordinal)) continue;
                if (((uint)flags & value) == value)
                {
                    names.Add(name);
                    rest &= ~value;
                }
            }
            if (rest != 0)
                names.Add("0x" + rest.ToString("X"));
            return names;
        }

        private static CollisionMaps.CollisionFlags ParseFlags(JToken token)
        {
            string[] known = Enum.GetNames(typeof(CollisionMaps.CollisionFlags)).Where(o => !o.EndsWith("_MASK", StringComparison.Ordinal)).ToArray();
            uint value = 0;
            IEnumerable<JToken> parts = token is JArray array ? (IEnumerable<JToken>)array : new[] { token };
            foreach (JToken part in parts)
            {
                if (part.Type == JTokenType.Integer)
                {
                    value |= (uint)(long)part;
                    continue;
                }
                string text = (part.Type == JTokenType.String ? (string)part : part.ToString()).Trim();
                foreach (string piece in text.Split(new[] { '|', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (piece.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && uint.TryParse(piece.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hex))
                        value |= hex;
                    else if (uint.TryParse(piece, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint number))
                        value |= number;
                    else
                    {
                        string name = known.FirstOrDefault(o => string.Equals(o, piece, StringComparison.OrdinalIgnoreCase));
                        if (name == null)
                            throw new McpError("'" + piece + "' is not a collision flag. Flags: " + string.Join(", ", known) + " (or a number).");
                        value |= (uint)(CollisionMaps.CollisionFlags)Enum.Parse(typeof(CollisionMaps.CollisionFlags), name);
                    }
                }
            }
            return (CollisionMaps.CollisionFlags)value;
        }

        /// <summary>What retail stores as a new proxy's Havok userData: the write index of the row's physics material.</summary>
        private static uint UserData(Level level, Materials.Material material)
        {
            int index = material != null && level.Materials != null ? level.Materials.GetWriteIndex(material) : -1;
            return index >= 0 ? (uint)index : 0u;
        }
        #endregion

        #region set_physics_system
        private static object SetPhysicsSystem(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel().Level;
                McpEditor.RequireUndoIdle();
                Commands commands = level.Commands;
                Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                Entity entity;
                if (call.Has("entity"))
                    entity = McpScript.FindEntity(commands, composite, call.Str("entity"));
                else
                {
                    List<FunctionEntity> systems = composite.functions.Where(o => o.function == FunctionType.PhysicsSystem).ToList();
                    if (systems.Count != 1)
                        throw new McpError(composite.name + " has " + (systems.Count == 0 ? "no PhysicsSystem entity (create_entities can make one)" : systems.Count + " PhysicsSystem entities") + ": give 'entity'.");
                    entity = systems[0];
                }
                Target target = Resolve(level, composite, entity, write: true, follow: false, call: call).InList();
                if (target.Function != FunctionType.PhysicsSystem)
                    throw new McpError(target.Name + " is a " + target.Function + ": a physics system is bound to a PhysicsSystem entity (create_entities can make one in the composite whose models it moves).");

                //Commands' save gives every PhysicsSystem entity a DYNAMIC_PHYSICS_SYSTEM reference and binds an
                //unbound one to its system_index parameter (0 without one), so a clear or removal would not be saved
                JToken token = call.Token("system");
                string text = token == null ? null : token.Type == JTokenType.String ? ((string)token).Trim() : token.ToString();
                if (call.Bool("remove") || (token != null && IsNone(text)))
                    throw new McpError(target.Name + " cannot be left without a physics system: saving the level binds every PhysicsSystem entity to the system its system_index parameter names (system 0 without one), putting back a cleared or removed reference. Bind it to another system, or delete the entity (delete_entities) to drop its physics.");
                if (token == null)
                    throw new McpError("Give 'system': an index or name from list_physics_systems.");

                HavokPackfile.PhysicsSystem system = FindPhysicsSystem(RequirePhysics(level, forImport: false), token);
                Commit(target, "AI: Set physics system of " + target.Label, list => BindPhysics(list, system));
                return new JObject()
                {
                    ["entity"] = EntityBrief(target),
                    ["physics_system"] = DescribeReference(level, target.Find(ResourceType.DYNAMIC_PHYSICS_SYSTEM), null)["physics_system"],
                };
            });
        }

        /// <summary>What the Dynamic Physics System panel's Set does, on a copy of the entity's list (adding the reference as the loader does if it is missing).</summary>
        private static void BindPhysics(List<ResourceReference> list, HavokPackfile.PhysicsSystem system)
        {
            ResourceReference reference = list.FirstOrDefault(o => o != null && o.resource_type == ResourceType.DYNAMIC_PHYSICS_SYSTEM);
            if (reference == null)
            {
                reference = new ResourceReference(ResourceType.DYNAMIC_PHYSICS_SYSTEM) { resource_id = ShortGuids.DYNAMIC_PHYSICS_SYSTEM };
                list.Add(reference);
            }
            reference.PhysicsSystem = system;
            reference.PhysicsSystemIndex = system?.SystemIndex ?? -1;
        }

        private static HavokPackfile.PhysicsSystem FindPhysicsSystem(HavokPackfile hkx, JToken token)
        {
            string text = token.Type == JTokenType.String ? ((string)token).Trim() : token.ToString();
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
            {
                HavokPackfile.PhysicsSystem byIndex = hkx.GetPhysicsSystem(index);
                if (byIndex == null)
                    throw new McpError("There is no physics system " + index + " (the level has " + hkx.PhysicsSystems.Count + "; list_physics_systems shows them).");
                return byIndex;
            }
            string wanted = text.Replace('/', '\\');
            List<HavokPackfile.PhysicsSystem> exact = hkx.PhysicsSystems.Where(o => string.Equals((o.Name ?? "").Replace('/', '\\'), wanted, StringComparison.OrdinalIgnoreCase)).ToList();
            if (exact.Count == 0)
                exact = hkx.PhysicsSystems.Where(o => (o.Name ?? "").Replace('/', '\\').EndsWith("\\" + wanted, StringComparison.OrdinalIgnoreCase)).ToList();
            if (exact.Count == 1)
                return exact[0];
            if (exact.Count > 1)
                throw new McpError("'" + text + "' could be systems " + string.Join(", ", exact.Take(10).Select(o => o.SystemIndex)) + ": give the index.");
            List<HavokPackfile.PhysicsSystem> near = hkx.PhysicsSystems.Where(o => (o.Name ?? "").IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0).Take(10).ToList();
            throw new McpError("No physics system is called '" + text + "'." + (near.Count != 0 ? " Similar: " + string.Join("; ", near.Select(o => o.SystemIndex + " " + o.Name)) + "." : " list_physics_systems shows them."));
        }
        #endregion

        #region Collision proxies
        private static HavokPackfile RequireCollision(Level level, bool forImport)
        {
            HavokPackfile hkx = level.Collision;
            if (hkx == null || !hkx.Loaded)
                throw new McpError("This level has no COLLISION.HKX loaded.");
            if (forImport && ((level.CollisionHKX != null && level.CollisionHKX.IsTagfile) || (level.CollisionHKX64 != null && level.CollisionHKX64.IsTagfile)))
                throw new McpError("New collision meshes can only be written to the PC collision files, not a mobile or Switch level's.");
            return hkx;
        }

        private static string HostRole(HavokPackfile hkx, HavokPackfile.StaticCompoundShape compound)
        {
            bool primary = ReferenceEquals(compound, hkx.WorldHostPrimary);
            bool secondary = ReferenceEquals(compound, hkx.WorldHostSecondary);
            if (primary && secondary) return "world host";
            if (primary) return "world host (primary: placed ballistic colliders)";
            if (secondary) return "world host (secondary: placed walkable colliders)";
            return null;
        }

        private static Dictionary<HavokPackfile.StaticCompoundShape, List<(Composite, FunctionEntity, CollisionMaps.COLLISION_MAPPING)>> CollisionUsers(Commands commands)
        {
            Dictionary<HavokPackfile.StaticCompoundShape, List<(Composite, FunctionEntity, CollisionMaps.COLLISION_MAPPING)>> users = new Dictionary<HavokPackfile.StaticCompoundShape, List<(Composite, FunctionEntity, CollisionMaps.COLLISION_MAPPING)>>(new RefComparer<HavokPackfile.StaticCompoundShape>());
            foreach ((Composite composite, FunctionEntity entity, ResourceReference reference) in AllReferences(commands))
            {
                HavokPackfile.StaticCompoundShape proxy = reference.resource_type == ResourceType.COLLISION_MAPPING ? reference.CollisionMapping?.CollisionProxy : null;
                if (proxy == null) continue;
                if (!users.TryGetValue(proxy, out var list))
                    users[proxy] = list = new List<(Composite, FunctionEntity, CollisionMaps.COLLISION_MAPPING)>();
                list.Add((composite, entity, reference.CollisionMapping));
            }
            return users;
        }

        private static JObject DescribeProxy(Commands commands, HavokPackfile hkx, HavokPackfile.StaticCompoundShape compound, Dictionary<HavokPackfile.StaticCompoundShape, List<(Composite, FunctionEntity, CollisionMaps.COLLISION_MAPPING)>> users, int userLimit)
        {
            JObject item = new JObject()
            {
                ["proxy"] = compound.ProxyIndex,
                ["instances"] = compound.Instances?.Count ?? 0,
                ["data_offset"] = "0x" + compound.DataOffset.ToString("X"),
            };
            string role = HostRole(hkx, compound);
            if (role != null) item["role"] = role;
            if (compound.Instances != null && compound.Instances.Count != 0)
            {
                JObject classes = new JObject();
                foreach (var group in compound.Instances.GroupBy(o => ShortClass(o.ShapeClassName)).OrderByDescending(o => o.Count()).Take(6))
                    classes[group.Key] = group.Count();
                item["shape_classes"] = classes;
            }
            if (compound.DomainMin.X <= compound.DomainMax.X && !float.IsInfinity(compound.DomainMin.X) && !float.IsInfinity(compound.DomainMax.X))
                item["domain"] = new JObject() { ["min"] = Vector(compound.DomainMin), ["max"] = Vector(compound.DomainMax) };
            if (users != null)
            {
                List<(Composite, FunctionEntity, CollisionMaps.COLLISION_MAPPING)> list = users.TryGetValue(compound, out var found) ? found : new List<(Composite, FunctionEntity, CollisionMaps.COLLISION_MAPPING)>();
                item["users"] = list.Count;
                if (list.Count != 0)
                    item["used_by"] = new JArray(list.Take(userLimit).Select(o => new JObject()
                    {
                        ["composite"] = o.Item1.name,
                        ["entity"] = McpScript.EntityName(commands, o.Item1, o.Item2),
                        ["id"] = McpScript.Id(o.Item2.shortGUID),
                        ["flags"] = FlagNames(o.Item3.Flags),
                        ["material"] = o.Item3.Material?.Name,
                    }));
            }
            return item;
        }

        private static string ShortClass(string className)
        {
            if (string.IsNullOrEmpty(className)) return "?";
            return className.StartsWith("hkp", StringComparison.Ordinal) ? className.Substring(3) : className;
        }

        private static JArray Vector(Vector4 v) => new JArray(Math.Round((double)v.X, 4), Math.Round((double)v.Y, 4), Math.Round((double)v.Z, 4));
        private static JArray Vector(Vector3 v) => new JArray(Math.Round((double)v.X, 4), Math.Round((double)v.Y, 4), Math.Round((double)v.Z, 4));

        private static JObject MeshStats(HavokPackfile.PreviewMesh mesh)
        {
            JObject stats = new JObject() { ["triangles"] = mesh.TriangleCount, ["shapes"] = mesh.ShapeCount };
            if (mesh.Positions.Count != 0)
            {
                Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
                foreach (Vector3 p in mesh.Positions)
                {
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
                stats["bounds"] = new JObject() { ["min"] = Vector(min), ["max"] = Vector(max) };
            }
            return stats;
        }

        private static object ListCollisionProxies(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel(forEditing: false).Level;
                HavokPackfile hkx = RequireCollision(level, forImport: false);
                if (call.Has("proxy"))
                {
                    int index = call.Int("proxy");
                    HavokPackfile.StaticCompoundShape compound = hkx.GetCompound(index);
                    if (compound == null)
                        throw new McpError("There is no collision proxy " + index + " (the level has " + hkx.StaticCompoundShapes.Count + ").");
                    JObject one = DescribeProxy(level.Commands, hkx, compound, CollisionUsers(level.Commands), 50);
                    JObject stats = MeshStats(hkx.BuildPreviewMesh(compound));
                    foreach (JProperty property in stats.Properties().ToList())
                        one[property.Name] = property.Value;
                    if (HostRole(hkx, compound) != null)
                        call.Note("A world host's preview is capped at 2048 shapes / 250,000 triangles.");
                    return one;
                }

                string filter = (call.Str("filter") ?? "").Trim();
                int limit = Math.Max(1, call.Int("limit", 100));
                var users = call.Bool("include_users") ? CollisionUsers(level.Commands) : null;
                List<HavokPackfile.StaticCompoundShape> matching = hkx.StaticCompoundShapes.OrderBy(o => o.ProxyIndex).Where(o =>
                {
                    if (filter.Length == 0) return true;
                    string haystack = o.ProxyIndex + " " + (o.Instances?.Count ?? 0) + " 0x" + o.DataOffset.ToString("X") + " " + (HostRole(hkx, o) ?? "");
                    return haystack.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
                }).ToList();
                return new JObject()
                {
                    ["total"] = hkx.StaticCompoundShapes.Count,
                    ["count"] = matching.Count,
                    ["files"] = hkx.IsTagfile ? "tagfile (read only: imports refused)" : "PC packfile",
                    ["proxies"] = new JArray(matching.Take(limit).Select(o => DescribeProxy(level.Commands, hkx, o, users, 10))),
                };
            });
        }

        private static object ImportCollisionProxy(McpCall call)
        {
            string path = call.Str("path");
            string modelName = call.Str("model");
            if ((path == null) == (modelName == null))
                throw new McpError("Give one of 'path' (a mesh file) or 'model' (a model the level holds, from list_models).");
            if (call.Has("part") && modelName == null)
                throw new McpError("'part' goes with 'model'.");
            float scale = ReadScale(call);
            string type = (call.Str("collision_type") ?? "world").Trim().ToLowerInvariant();
            if (type != "world" && type != "ballistic")
                throw new McpError("'collision_type' is 'world' or 'ballistic'.");
            bool dryRun = call.Bool("dry_run");

            CollisionProxyImporter.MeshSource source = path == null ? null : ReadMeshFile(call, path, scale);
            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel().Level;
                RequireCollision(level, forImport: true);
                Materials.Material material = call.Has("material") ? FindMaterial(level, call.Str("material")) : null;
                if (source == null)
                    source = MeshFromModel(level, modelName, call, scale);
                uint userData = UserData(level, material);
                uint filterInfo = type == "world" ? 3u : 9u;
                JObject result = new JObject()
                {
                    ["source"] = source.Name,
                    ["triangles"] = source.TriangleCount,
                    ["vertices"] = source.Positions.Count,
                    ["collision_type"] = type,
                    ["user_data"] = userData,
                    ["filter_info"] = filterInfo,
                };
                if (source.Positions.Count != 0)
                {
                    Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
                    foreach (Vector3 p in source.Positions) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
                    result["bounds"] = new JObject() { ["min"] = Vector(min), ["max"] = Vector(max) };
                }
                if (dryRun)
                {
                    result["dry_run"] = true;
                    return result;
                }

                HavokPackfile.StaticCompoundShape created;
                try
                {
                    using (McpEditorTools.Heartbeat(call, "Writing the collision proxy"))
                        created = CollisionProxyImporter.Import(level, source, userData, filterInfo);
                }
                catch (Exception e) when (!(e is McpError)) { throw new McpError("The proxy could not be created: " + e.Message); }
                Singleton.OnResourceModified?.Invoke();
                result["proxy"] = created.ProxyIndex;
                call.Note("Proxy " + created.ProxyIndex + " is in COLLISION.HKX/HKX64 in memory and is written by save_level. It cannot be undone or deleted; set_collision assigns it to an entity.");
                return result;
            });
        }

        private static float ReadScale(McpCall call)
        {
            double scale = call.Num("scale", 1.0);
            if (!(scale > 0) || double.IsInfinity(scale))
                throw new McpError("'scale' has to be above 0.");
            return (float)scale;
        }

        private static void RequireAbsolute(string path, string argument)
        {
            bool drive = path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' && (path[2] == '\\' || path[2] == '/');
            bool unc = path.StartsWith(@"\\", StringComparison.Ordinal);
            if (!drive && !unc)
                throw new McpError("'" + argument + "' must be an absolute path (e.g. C:\\Models\\crate.fbx), not '" + path + "'.");
        }

        /// <summary>A mesh file's triangles, read off the UI thread (Assimp only).</summary>
        private static CollisionProxyImporter.MeshSource ReadMeshFile(McpCall call, string path, float scale)
        {
            RequireAbsolute(path, "path");
            if (!File.Exists(path))
                throw new McpError("There is no file at " + path + ".");
            try
            {
                using (McpEditorTools.Heartbeat(call, "Reading " + Path.GetFileName(path)))
                    return CollisionProxyImporter.FromModelFile(path, scale);
            }
            catch (Exception e) when (!(e is McpError))
            {
                throw new McpError("Could not read the mesh in " + Path.GetFileName(path) + ": " + e.Message);
            }
        }

        /// <summary>A level model's triangles at LOD0: one part, or every part together. UI thread.</summary>
        private static CollisionProxyImporter.MeshSource MeshFromModel(Level level, string name, McpCall call, float scale)
        {
            Models.CS2 model = FindModel(level, name);
            CollisionProxyImporter.MeshSource source;
            try
            {
                if (call.Has("part"))
                {
                    int part = call.Int("part");
                    if (part < 0 || part >= model.Components.Count)
                        throw new McpError(model.Name + " has " + model.Components.Count + " part(s): 'part' takes 0 to " + (model.Components.Count - 1) + ".");
                    source = CollisionProxyImporter.FromComponent(model.Components[part], model.Name + " part " + part);
                }
                else
                    source = FromComponents(model);
            }
            catch (Exception e) when (!(e is McpError))
            {
                throw new McpError("No mesh could be read from " + model.Name + ": " + e.Message);
            }
            if (scale != 1f)
                for (int i = 0; i < source.Positions.Count; i++)
                    source.Positions[i] = source.Positions[i] * scale;
            return source;
        }

        private static CollisionProxyImporter.MeshSource FromComponents(Models.CS2 model)
        {
            CollisionProxyImporter.MeshSource all = new CollisionProxyImporter.MeshSource() { Name = model.Name };
            foreach (Models.CS2.Component component in model.Components)
            {
                if (component.LODs.Count == 0 || component.LODs[0].Submeshes.Count == 0) continue;
                CollisionProxyImporter.MeshSource part;
                try { part = CollisionProxyImporter.FromComponent(component); }
                catch (InvalidDataException) { continue; }
                all.Append(part.Positions, part.Indices);
            }
            if (all.TriangleCount == 0)
                throw new McpError(model.Name + " holds no triangles at LOD0.");
            return all;
        }

        private static object ExportCollisionMesh(McpCall call)
        {
            string path = call.Str("path", required: true).Trim();
            RequireAbsolute(path, "path");
            if (!string.Equals(Path.GetExtension(path), ".obj", StringComparison.OrdinalIgnoreCase))
                throw new McpError("The file has to be an .obj.");
            if (File.Exists(path) && !call.Bool("overwrite"))
                throw new McpError(path + " already exists: pass overwrite: true to replace it.");
            string folder = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                throw new McpError("The folder " + folder + " does not exist.");
            if (call.Has("proxy") == call.Has("physics_system"))
                throw new McpError("Give one of 'proxy' or 'physics_system'.");

            HavokPackfile.PreviewMesh mesh = null;
            string what = null;
            string level = null;
            McpEditor.UI(() =>
            {
                Level open = McpEditor.RequireLevel(forEditing: false).Level;
                level = open.Name;
                if (call.Has("proxy"))
                {
                    HavokPackfile hkx = RequireCollision(open, forImport: false);
                    int index = call.Int("proxy");
                    HavokPackfile.StaticCompoundShape compound = hkx.GetCompound(index) ?? throw new McpError("There is no collision proxy " + index + ".");
                    mesh = hkx.BuildPreviewMesh(compound);
                    what = "collision proxy " + index;
                }
                else
                {
                    HavokPackfile hkx = RequirePhysics(open, forImport: false);
                    int index = call.Int("physics_system");
                    HavokPackfile.PhysicsSystem system = hkx.GetPhysicsSystem(index) ?? throw new McpError("There is no physics system " + index + ".");
                    mesh = hkx.BuildPreviewMesh(system);
                    what = "physics system " + index + (string.IsNullOrEmpty(system.Name) ? "" : " (" + system.Name + ")");
                }
            });
            if (mesh == null || mesh.TriangleCount == 0)
                throw new McpError("The " + what + " has no geometry the editor can show.");

            //OpenCAGE's OBJ convention (see ModelExporter): centimetres, and mirrored on Z with the winding turned,
            //which the model importer's left-handed conversion undoes - so the file imports back where it was
            StringBuilder obj = new StringBuilder();
            obj.AppendLine("# " + what + " of " + level + ", exported by OpenCAGE: " + mesh.TriangleCount + " triangles, " + mesh.ShapeCount + " shapes.");
            obj.AppendLine("# Centimetres, Z mirrored as OpenCAGE writes OBJ, so it re-imports at the same size and place.");
            obj.AppendLine("o " + what.Replace(' ', '_'));
            foreach (Vector3 p in mesh.Positions)
                obj.Append("v ").Append((p.X * 100f).ToString("0.####", CultureInfo.InvariantCulture)).Append(' ')
                    .Append((p.Y * 100f).ToString("0.####", CultureInfo.InvariantCulture)).Append(' ')
                    .Append((-p.Z * 100f).ToString("0.####", CultureInfo.InvariantCulture)).AppendLine();
            for (int i = 0; i + 2 < mesh.Indices.Count; i += 3)
                obj.Append("f ").Append(mesh.Indices[i] + 1).Append(' ').Append(mesh.Indices[i + 2] + 1).Append(' ').Append(mesh.Indices[i + 1] + 1).AppendLine();
            try
            {
                File.WriteAllText(path, obj.ToString());
            }
            catch (Exception e)
            {
                throw new McpError("Could not write " + path + ": " + e.Message);
            }
            JObject result = new JObject()
            {
                ["file"] = Path.GetFullPath(path),
                ["what"] = what,
                ["triangles"] = mesh.TriangleCount,
                ["vertices"] = mesh.Positions.Count,
                ["shapes"] = mesh.ShapeCount,
            };
            if (mesh.ShapeCount >= 2048 || mesh.TriangleCount >= 250000)
                call.Note("The editor's preview stops at 2048 shapes / 250,000 triangles, so this may not be all of it.");
            return result;
        }
        #endregion

        #region Physics systems
        private static HavokPackfile RequirePhysics(Level level, bool forImport)
        {
            HavokPackfile hkx = level.Physics;
            if (hkx == null || !hkx.Loaded)
                throw new McpError("This level has no PHYSICS.HKX loaded.");
            if (forImport && ((level.PhysicsHKX != null && level.PhysicsHKX.IsTagfile) || (level.PhysicsHKX64 != null && level.PhysicsHKX64.IsTagfile)))
                throw new McpError("New physics systems can only be written to the PC physics files, not a mobile or Switch level's.");
            return hkx;
        }

        private static JObject DescribeBody(HavokPackfile.RigidBodyInfo body)
        {
            return new JObject()
            {
                ["name"] = string.IsNullOrEmpty(body.Name) ? null : body.Name,
                ["shape"] = ShortClass(body.ShapeClassName),
                ["motion"] = body.MotionTypeName,
                ["mass"] = float.IsInfinity(body.Mass) ? (JToken)"infinite" : Math.Round((double)body.Mass, 3),
                ["friction"] = Math.Round((double)body.Friction, 3),
                ["restitution"] = Math.Round((double)body.Restitution, 3),
                ["linear_damping"] = Math.Round((double)body.LinearDamping, 3),
                ["angular_damping"] = Math.Round((double)body.AngularDamping, 3),
                ["gravity_factor"] = Math.Round((double)body.GravityFactor, 3),
                ["radius"] = Math.Round((double)body.ObjectRadius, 3),
                ["filter_info"] = "0x" + body.CollisionFilterInfo.ToString("X"),
            };
        }

        private static object ListPhysicsSystems(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel(forEditing: false).Level;
                HavokPackfile hkx = RequirePhysics(level, forImport: false);
                if (call.Has("system"))
                {
                    HavokPackfile.PhysicsSystem system = FindPhysicsSystem(hkx, call.Token("system"));
                    JObject one = new JObject() { ["system"] = system.SystemIndex, ["name"] = system.Name };
                    List<HavokPackfile.RigidBodyInfo> bodies = hkx.GetRigidBodies(system);
                    one["bodies"] = new JArray(bodies.Take(64).Select(DescribeBody));
                    JObject stats = MeshStats(hkx.BuildPreviewMesh(system));
                    one["preview"] = stats;
                    JArray users = new JArray();
                    foreach (Composite composite in level.Commands.Entries)
                    {
                        if (composite == null) continue;
                        foreach (FunctionEntity function in composite.functions)
                        {
                            if (function.function != FunctionType.PhysicsSystem) continue;
                            ResourceReference reference = function.GetResource(ResourceType.DYNAMIC_PHYSICS_SYSTEM);
                            if (reference == null || !(ReferenceEquals(reference.PhysicsSystem, system) || (reference.PhysicsSystem == null && reference.PhysicsSystemIndex == system.SystemIndex))) continue;
                            if (users.Count < 50)
                                users.Add(new JObject() { ["composite"] = composite.name, ["entity"] = McpScript.EntityName(level.Commands, composite, function), ["id"] = McpScript.Id(function.shortGUID) });
                        }
                    }
                    one["bound_by"] = users;
                    return one;
                }

                string filter = (call.Str("filter") ?? "").Trim();
                int limit = Math.Max(1, call.Int("limit", 100));
                bool withBodies = call.Bool("bodies");
                List<HavokPackfile.PhysicsSystem> matching = hkx.PhysicsSystems.Where(o => filter.Length == 0 || (o.SystemIndex + " " + (o.Name ?? "")).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                return new JObject()
                {
                    ["total"] = hkx.PhysicsSystems.Count,
                    ["count"] = matching.Count,
                    ["files"] = hkx.IsTagfile ? "tagfile (read only: imports refused)" : "PC packfile",
                    ["systems"] = new JArray(matching.Take(limit).Select(o =>
                    {
                        JObject item = new JObject() { ["system"] = o.SystemIndex, ["name"] = o.Name };
                        if (withBodies)
                            item["bodies"] = new JArray(hkx.GetRigidBodies(o).Take(16).Select(DescribeBody));
                        return item;
                    })),
                };
            });
        }

        private static object ImportPhysicsSystem(McpCall call)
        {
            string path = call.Str("path");
            string modelName = call.Str("model");
            if (path != null && modelName != null)
                throw new McpError("Give 'path' or 'model', not both.");
            if (call.Has("part") && modelName == null)
                throw new McpError("'part' goes with 'model'.");
            float scale = ReadScale(call);
            bool dryRun = call.Bool("dry_run");
            double friction = call.Num("friction", 0.5);
            double restitution = call.Num("restitution", 0.4);
            if (friction < 0 || friction > 10) throw new McpError("'friction' takes 0 to 10.");
            if (restitution < 0 || restitution > 1) throw new McpError("'restitution' takes 0 to 1.");
            if (call.Has("mass") && (call.Num("mass") < 0.01 || call.Num("mass") > 10000))
                throw new McpError("'mass' takes 0.01 to 10000 kg.");

            CollisionProxyImporter.MeshSource source = path == null ? null : ReadMeshFile(call, path, scale);
            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel().Level;
                RequirePhysics(level, forImport: true);
                Composite host = call.Has("composite") ? McpScript.FindComposite(level.Commands, call.Str("composite")) : null;
                List<FunctionEntity> physicsEntities = host == null ? new List<FunctionEntity>() : host.functions.Where(o => o.function == FunctionType.PhysicsSystem).ToList();
                FunctionEntity physicsEntity = physicsEntities.Count == 1 ? physicsEntities[0] : null;
                PhysicsSystemImporter.HostDefaults defaults = PhysicsSystemImporter.DefaultsFor(host, physicsEntity);

                bool bind = call.Bool("bind", physicsEntity != null);
                if (bind && physicsEntity == null)
                    throw new McpError(host == null ? "'bind' needs 'composite': the composite whose PhysicsSystem entity takes the new system." : host.name + " has " + (physicsEntities.Count == 0 ? "no PhysicsSystem entity (create_entities can make one)" : physicsEntities.Count + " PhysicsSystem entities") + " to bind; pass bind: false to only make the system.");
                if (bind && !dryRun)
                    McpEditor.RequireUndoIdle();

                string origin;
                if (source != null)
                    origin = path;
                else if (modelName != null)
                {
                    source = MeshFromModel(level, modelName, call, scale);
                    origin = source.Name;
                }
                else if (defaults.Model != null)
                {
                    Models.CS2.Component component = SafeFindComponent(level.Models, defaults.Model);
                    Models.CS2 model = SafeFindModel(level.Models, defaults.Model);
                    if (component == null)
                        throw new McpError("The model " + (defaults.PlacementSource ?? "in the composite") + " draws is not one the level holds: give 'path' or 'model'.");
                    try { source = CollisionProxyImporter.FromComponent(component, model?.Name); }
                    catch (Exception e) when (!(e is McpError)) { throw new McpError("No mesh could be read from " + (model?.Name ?? "the composite's model") + ": " + e.Message); }
                    if (scale != 1f)
                        for (int i = 0; i < source.Positions.Count; i++) source.Positions[i] = source.Positions[i] * scale;
                    origin = (model?.Name ?? "the model") + " (drawn by " + defaults.PlacementSource + ")";
                }
                else
                    throw new McpError("Give 'path' (a mesh file) or 'model' (list_models)" + (host != null ? ": " + host.name + " has no ModelReference with a model to take one from." : ", or 'composite' to use the model its ModelReference draws."));

                ConvexBody shape;
                try
                {
                    using (McpEditorTools.Heartbeat(call, "Building the hull"))
                        shape = PhysicsSystemImporter.BuildShape(source);
                }
                catch (Exception e) when (!(e is McpError)) { throw new McpError("No physics shape could be made from " + source.Name + ": " + e.Message); }

                string systemName = (call.Str("system_name") ?? defaults.SystemName ?? "").Trim();
                string bodyName = (call.Str("body_name") ?? defaults.BodyName ?? "").Trim();
                if (systemName.Length == 0 || !IsAscii(systemName)) throw new McpError("'system_name' has to be plain ASCII text.");
                if (bodyName.Length == 0 || !IsAscii(bodyName)) throw new McpError("'body_name' has to be plain ASCII text.");
                bool place = call.Bool("place_at_model", defaults.PlacementSource != null);
                if (place && defaults.PlacementSource == null)
                    throw new McpError("There is no ModelReference to place the body at" + (host == null ? " (give 'composite')" : " in " + host.name) + "; pass place_at_model: false.");
                float suggested = PhysicsSystemImporter.SuggestedMass(shape);
                PhysicsBodySettings body = new PhysicsBodySettings()
                {
                    Name = bodyName,
                    Mass = call.Has("mass") ? (float)call.Num("mass") : suggested,
                    Friction = (float)friction,
                    Restitution = (float)restitution,
                    Position = place ? defaults.Position : Vector3.Zero,
                    Rotation = place ? defaults.Rotation : Quaternion.Identity,
                };

                JObject result = new JObject()
                {
                    ["source"] = origin,
                    ["hull_corners"] = shape.Vertices.Count,
                    ["full_hull_corners"] = shape.FullHullVertices,
                    ["volume_litres"] = Math.Round(shape.Volume * 1000.0, 3),
                    ["convex_radius_cm"] = Math.Round(shape.ConvexRadius * 100.0, 3),
                    ["system_name"] = systemName,
                    ["body_name"] = bodyName,
                    ["mass"] = Math.Round((double)body.Mass, 3),
                    ["suggested_mass"] = suggested,
                    ["friction"] = body.Friction,
                    ["restitution"] = body.Restitution,
                    ["placement"] = place
                        ? new JObject() { ["at"] = defaults.PlacementSource, ["position"] = Vector(body.Position), ["rotation_quaternion"] = new JArray(Math.Round((double)body.Rotation.X, 5), Math.Round((double)body.Rotation.Y, 5), Math.Round((double)body.Rotation.Z, 5), Math.Round((double)body.Rotation.W, 5)) }
                        : (JToken)"the system's origin",
                };
                Target bindTarget = bind ? Resolve(level, host, physicsEntity, write: true, follow: false, call: call).InList() : null;
                if (dryRun)
                {
                    result["dry_run"] = true;
                    if (bindTarget != null) result["would_bind"] = EntityBrief(bindTarget);
                    return result;
                }

                HavokPackfile.PhysicsSystem created;
                try
                {
                    using (McpEditorTools.Heartbeat(call, "Writing the physics system"))
                        created = PhysicsSystemImporter.Import(level, systemName, shape, body);
                }
                catch (Exception e) when (!(e is McpError)) { throw new McpError("The physics system could not be created: " + e.Message); }
                Singleton.OnResourceModified?.Invoke();
                result["system"] = created.SystemIndex;
                call.Note("System " + created.SystemIndex + " is in PHYSICS.HKX/HKX64 in memory and is written by save_level. It cannot be undone or deleted.");

                if (bindTarget != null)
                {
                    Commit(bindTarget, "AI: Set physics system of " + bindTarget.Label, list => BindPhysics(list, created));
                    result["bound"] = EntityBrief(bindTarget);
                    call.Note("Binding " + bindTarget.Name + " to it is one undo step.");
                }
                return result;
            });
        }

        private static bool IsAscii(string text)
        {
            foreach (char c in text)
                if (c < 32 || c > 126) return false;
            return true;
        }
        #endregion

        #region Characters
        private static readonly string[] PartNames = { "torso", "legs", "shoes", "head", "arms", "collision" };

        private static CharacterAccessorySets.CharacterAttributes.Components.Component Part(CharacterAccessorySets.CharacterAttributes attributes, int part)
        {
            CharacterAccessorySets.CharacterAttributes.Components components = attributes.components;
            switch (part)
            {
                case 0: return components.Torso;
                case 1: return components.Legs;
                case 2: return components.Shoes;
                case 3: return components.Head;
                case 4: return components.Arms;
                default: return components.Collision;
            }
        }

        /// <summary>Every field of an accessory set, so an undo step can put one back exactly.</summary>
        internal sealed class Appearance
        {
            public ShortGuid EntityId, InstanceId;
            public ShortGuid[] Composites = new ShortGuid[6];
            public int[] Accessories = new int[6];
            public CUSTOM_CHARACTER_ASSETS AssetType;
            public DIALOGUE_VOICE_ACTOR VoiceActor;
            public CUSTOM_CHARACTER_GENDER Gender;
            public CUSTOM_CHARACTER_ETHNICITY Ethnicity;
            public CUSTOM_CHARACTER_BUILD Build;
            public string FaceSkeleton, GenderSkeleton;
            public CHARACTER_FOLEY_SOUND FoleyTorso, FoleyLeg, FoleyFootwear;

            public static Appearance Of(CharacterAccessorySets.CharacterAttributes attributes)
            {
                Appearance a = new Appearance()
                {
                    EntityId = attributes.character?.entity_id ?? ShortGuid.Invalid,
                    InstanceId = attributes.character?.composite_instance_id ?? ShortGuid.Invalid,
                    AssetType = attributes.asset_type,
                    VoiceActor = attributes.voice_actor,
                    Gender = attributes.gender,
                    Ethnicity = attributes.ethnicity,
                    Build = attributes.build,
                    FaceSkeleton = attributes.face_skeleton,
                    GenderSkeleton = attributes.gender_skeleton,
                    FoleyTorso = attributes.foley.Torso,
                    FoleyLeg = attributes.foley.Leg,
                    FoleyFootwear = attributes.foley.Footwear,
                };
                for (int i = 0; i < 6; i++)
                {
                    a.Composites[i] = Part(attributes, i).Composite;
                    a.Accessories[i] = Part(attributes, i).AccessoryIndex;
                }
                return a;
            }

            public void ApplyTo(CharacterAccessorySets.CharacterAttributes attributes)
            {
                attributes.character = new EntityHandle() { entity_id = EntityId, composite_instance_id = InstanceId };
                attributes.asset_type = AssetType;
                attributes.voice_actor = VoiceActor;
                attributes.gender = Gender;
                attributes.ethnicity = Ethnicity;
                attributes.build = Build;
                attributes.face_skeleton = FaceSkeleton;
                attributes.gender_skeleton = GenderSkeleton;
                attributes.foley.Torso = FoleyTorso;
                attributes.foley.Leg = FoleyLeg;
                attributes.foley.Footwear = FoleyFootwear;
                for (int i = 0; i < 6; i++)
                {
                    Part(attributes, i).Composite = Composites[i];
                    Part(attributes, i).AccessoryIndex = Accessories[i];
                }
            }
        }

        private static (Composite, FunctionEntity) ResolveCharacter(Commands commands, McpCall call)
        {
            Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
            Entity entity = McpScript.FindEntity(commands, composite, call.Str("entity", required: true));
            if (!(entity is FunctionEntity function) || !function.function.IsFunctionType || function.function.AsFunctionType != FunctionType.Character)
                throw new McpError(McpScript.EntityName(commands, composite, entity) + " is a " + McpScript.TypeName(commands, composite, entity) + ", not a Character (find_entities with type 'Character' finds them).");
            return (composite, function);
        }

        /// <summary>Where the entity is placed in the level, from the root, as the Character Editor lists them.</summary>
        private static List<EntityPath> Placements(Composite composite, Entity entity)
        {
            LevelContent content = McpEditor.RequireLevel(forEditing: false);
            return content.EditorUtils.GetHierarchiesForEntity(composite, entity);
        }

        /// <summary>
        /// The accessory set for one placement: by entity and instance, else (as the Character Editor looks) by instance
        /// alone, but not a set stored for another Character in the composite: that one is its own, returned as <paramref name="owner"/>.
        /// </summary>
        private static CharacterAccessorySets.CharacterAttributes FindSet(Level level, Composite composite, Entity entity, ShortGuid instance, out bool byInstanceOnly, out FunctionEntity owner)
        {
            List<CharacterAccessorySets.CharacterAttributes> entries = level.AccessorySets?.Entries ?? new List<CharacterAccessorySets.CharacterAttributes>();
            List<CharacterAccessorySets.CharacterAttributes> inInstance = entries.Where(o => o?.character != null && o.character.composite_instance_id == instance).ToList();
            CharacterAccessorySets.CharacterAttributes exact = inInstance.FirstOrDefault(o => o.character.entity_id == entity.shortGUID);
            byInstanceOnly = false;
            owner = null;
            if (exact != null) return exact;
            Dictionary<ShortGuid, FunctionEntity> characters = composite.functions.Where(o => !ReferenceEquals(o, entity) && o.function == FunctionType.Character).ToDictionary(o => o.shortGUID);
            CharacterAccessorySets.CharacterAttributes loose = inInstance.FirstOrDefault(o => !characters.ContainsKey(o.character.entity_id));
            byInstanceOnly = loose != null;
            if (loose == null && inInstance.Count != 0)
                owner = characters[inInstance[0].character.entity_id];
            return loose;
        }

        private static JObject DescribeAppearance(Commands commands, CharacterAccessorySets.CharacterAttributes attributes)
        {
            JObject components = new JObject();
            for (int i = 0; i < 6; i++)
            {
                ShortGuid id = Part(attributes, i).Composite;
                Composite composite = id == ShortGuid.Invalid ? null : commands.GetComposite(id);
                components[PartNames[i]] = id == ShortGuid.Invalid ? JValue.CreateNull() : (JToken)(composite?.name ?? "missing composite " + McpScript.Id(id));
            }
            return new JObject()
            {
                ["components"] = components,
                ["attributes"] = new JObject()
                {
                    ["gender_skeleton"] = attributes.gender_skeleton,
                    ["face_skeleton"] = attributes.face_skeleton,
                    ["asset_type"] = attributes.asset_type.ToString(),
                    ["voice_actor"] = attributes.voice_actor.ToString(),
                    ["gender"] = attributes.gender.ToString(),
                    ["ethnicity"] = attributes.ethnicity.ToString(),
                    ["build"] = attributes.build.ToString(),
                    ["foley_torso"] = attributes.foley.Torso.ToString(),
                    ["foley_leg"] = attributes.foley.Leg.ToString(),
                    ["foley_footwear"] = attributes.foley.Footwear.ToString(),
                },
            };
        }

        private static object GetCharacterAppearance(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel(forEditing: false).Level;
                (Composite composite, FunctionEntity entity) = ResolveCharacter(level.Commands, call);
                List<EntityPath> placements = Placements(composite, entity);
                int limit = Math.Max(1, call.Int("limit", 50));
                JArray list = new JArray();
                for (int i = 0; i < placements.Count && i < limit; i++)
                {
                    ShortGuid instance = placements[i].GenerateCompositeInstanceID();
                    JObject item = new JObject()
                    {
                        ["placement"] = i,
                        ["instance_id"] = McpScript.Id(instance),
                        ["path"] = level.Commands.Utils.GetResolvedAsString(level.Commands.Utils.ResolveHierarchy(placements[i]), false),
                    };
                    CharacterAccessorySets.CharacterAttributes set = FindSet(level, composite, entity, instance, out bool loose, out FunctionEntity owner);
                    item["appearance"] = set == null ? JValue.CreateNull() : (JToken)DescribeAppearance(level.Commands, set);
                    if (loose)
                        item["note"] = "This set is stored for entity " + McpScript.Id(set.character.entity_id) + " in the same composite instance; the Character Editor shows it for this Character too.";
                    else if (owner != null)
                        item["note"] = "No set of its own: the one in this composite instance is Character " + McpScript.EntityName(level.Commands, composite, owner) + "'s (the Character Editor, which looks by instance, shows that one for this Character too).";
                    list.Add(item);
                }
                JObject result = new JObject()
                {
                    ["entity"] = McpScript.Brief(level.Commands, composite, entity),
                    ["placements"] = placements.Count,
                    ["appearances"] = list,
                };
                if (placements.Count == 0)
                    call.Note(composite.name + " is not placed anywhere in the level, so this Character has no placements to give an appearance to.");
                if (call.Bool("options"))
                {
                    JObject skeletons = new JObject();
                    foreach (KeyValuePair<string, HashSet<string>> gender in Singleton.GenderedSkeletons)
                        skeletons[gender.Key] = new JArray(gender.Value.OrderBy(o => o));
                    result["options"] = new JObject()
                    {
                        ["skeletons"] = skeletons,
                        ["asset_type"] = new JArray(Enum.GetNames(typeof(CUSTOM_CHARACTER_ASSETS))),
                        ["voice_actor"] = new JArray(Enum.GetNames(typeof(DIALOGUE_VOICE_ACTOR))),
                        ["gender"] = new JArray(Enum.GetNames(typeof(CUSTOM_CHARACTER_GENDER))),
                        ["ethnicity"] = new JArray(Enum.GetNames(typeof(CUSTOM_CHARACTER_ETHNICITY))),
                        ["build"] = new JArray(Enum.GetNames(typeof(CUSTOM_CHARACTER_BUILD))),
                        ["foley"] = new JArray(Enum.GetNames(typeof(CHARACTER_FOLEY_SOUND))),
                    };
                }
                return result;
            });
        }

        private static object SetCharacterAppearance(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel().Level;
                McpEditor.RequireUndoIdle();
                Commands commands = level.Commands;
                (Composite composite, FunctionEntity entity) = ResolveCharacter(commands, call);
                if (level.AccessorySets == null)
                    throw new McpError("This level has no character accessory sets file loaded.");
                List<EntityPath> placements = Placements(composite, entity);
                if (placements.Count == 0)
                    throw new McpError(composite.name + " is not placed anywhere in the level, so the Character has no placement to give an appearance to.");

                //Which placement
                int chosen;
                if (!call.Has("placement"))
                {
                    if (placements.Count != 1)
                        throw new McpError(McpScript.EntityName(commands, composite, entity) + " is placed " + placements.Count + " times: give 'placement' (get_character_appearance lists them).");
                    chosen = 0;
                }
                else
                {
                    JToken token = call.Token("placement");
                    string text = token.Type == JTokenType.String ? ((string)token).Trim() : token.ToString();
                    ShortGuid? id = McpScript.ParseId(text);
                    if (id != null)
                        chosen = placements.FindIndex(o => o.GenerateCompositeInstanceID() == id.Value);
                    else if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out chosen))
                        chosen = -1;
                    if (chosen < 0 || chosen >= placements.Count)
                        throw new McpError("'placement' takes an index from 0 to " + (placements.Count - 1) + " or an instance id from get_character_appearance.");
                }
                ShortGuid instance = placements[chosen].GenerateCompositeInstanceID();
                CharacterAccessorySets.CharacterAttributes existing = FindSet(level, composite, entity, instance, out bool _, out FunctionEntity owner);
                string name = UndoLabels.Entity(composite, entity);
                string ownerName = owner == null ? null : McpScript.EntityName(commands, composite, owner);

                if (call.Bool("remove"))
                {
                    if (call.Has("components") || call.Has("attributes"))
                        throw new McpError("'remove' cannot be combined with components or attributes.");
                    if (existing == null)
                        throw new McpError("Placement " + chosen + " has no appearance to remove" + (owner != null ? ": the set in this composite instance is Character " + ownerName + "'s (remove it there)." : "."));
                    UndoStack.Current.Apply(new McpCharacterAppearanceEdit(composite, entity, existing, IndexOfReference(level.AccessorySets.Entries, existing), Appearance.Of(existing), null, "AI: Remove appearance of " + name));
                    return new JObject() { ["entity"] = McpScript.Brief(commands, composite, entity), ["placement"] = chosen, ["removed"] = true };
                }

                bool creating = existing == null;
                CharacterAccessorySets.CharacterAttributes target = existing ?? new CharacterAccessorySets.CharacterAttributes()
                {
                    character = new EntityHandle() { entity_id = entity.shortGUID, composite_instance_id = instance },
                };
                Appearance before = creating ? null : Appearance.Of(target);
                Appearance after = Appearance.Of(target);
                JObject components = call.Object("components");
                JObject attributes = call.Object("attributes");
                if (!creating && (components == null || components.Count == 0) && (attributes == null || attributes.Count == 0))
                    throw new McpError("Placement " + chosen + " already has an appearance: give 'components' or 'attributes' to change, or remove: true.");

                if (components != null)
                {
                    foreach (JProperty property in components.Properties())
                    {
                        int part = Array.FindIndex(PartNames, o => string.Equals(o, property.Name.Trim(), StringComparison.OrdinalIgnoreCase));
                        if (part < 0)
                            throw new McpError("'" + property.Name + "' is not a component: they are " + string.Join(", ", PartNames) + ".");
                        string value = property.Value.Type == JTokenType.Null ? null : (property.Value.Type == JTokenType.String ? (string)property.Value : property.Value.ToString());
                        after.Composites[part] = IsNone(value) ? ShortGuid.Invalid : McpScript.FindComposite(commands, value).shortGUID;
                    }
                }
                if (attributes != null)
                    ApplyAttributes(after, attributes, call);

                string label = (creating ? "AI: Add appearance to " : "AI: Change appearance of ") + name;
                UndoStack.Current.Apply(new McpCharacterAppearanceEdit(composite, entity, target, creating ? level.AccessorySets.Entries.Count : IndexOfReference(level.AccessorySets.Entries, target), before, after, label));
                if (creating && owner != null)
                    call.Note("The set already in this composite instance is Character " + ownerName + "'s and is unchanged; this Character now has its own. The Character Editor, which looks by instance alone, still shows " + ownerName + "'s.");
                return new JObject()
                {
                    ["entity"] = McpScript.Brief(commands, composite, entity),
                    ["placement"] = chosen,
                    ["instance_id"] = McpScript.Id(instance),
                    ["created"] = creating,
                    ["appearance"] = DescribeAppearance(commands, target),
                };
            });
        }

        private static void ApplyAttributes(Appearance after, JObject attributes, McpCall call)
        {
            string genderSkeleton = null, faceSkeleton = null;
            foreach (JProperty property in attributes.Properties())
            {
                string key = property.Name.Trim().ToLowerInvariant();
                JToken value = property.Value;
                if (value == null || value.Type == JTokenType.Null)
                    throw new McpError("'" + property.Name + "' needs a value.");
                string text = (value.Type == JTokenType.String ? (string)value : value.ToString()).Trim();
                switch (key)
                {
                    case "gender_skeleton": genderSkeleton = text; break;
                    case "face_skeleton": faceSkeleton = text; break;
                    case "asset_type": after.AssetType = ParseEnum<CUSTOM_CHARACTER_ASSETS>(text, key); break;
                    case "voice_actor": after.VoiceActor = ParseEnum<DIALOGUE_VOICE_ACTOR>(text, key); break;
                    case "gender": after.Gender = ParseEnum<CUSTOM_CHARACTER_GENDER>(text, key); break;
                    case "ethnicity": after.Ethnicity = ParseEnum<CUSTOM_CHARACTER_ETHNICITY>(text, key); break;
                    case "build": after.Build = ParseEnum<CUSTOM_CHARACTER_BUILD>(text, key); break;
                    case "foley_torso": after.FoleyTorso = ParseEnum<CHARACTER_FOLEY_SOUND>(text, key); break;
                    case "foley_leg": after.FoleyLeg = ParseEnum<CHARACTER_FOLEY_SOUND>(text, key); break;
                    case "foley_footwear": after.FoleyFootwear = ParseEnum<CHARACTER_FOLEY_SOUND>(text, key); break;
                    default:
                        throw new McpError("'" + property.Name + "' is not an attribute: they are gender_skeleton, face_skeleton, asset_type, voice_actor, gender, ethnicity, build, foley_torso, foley_leg, foley_footwear.");
                }
            }

            //Skeletons: the face skeleton has to be one the gender skeleton offers, as the editor's two lists are
            Dictionary<string, HashSet<string>> skeletons = Singleton.GenderedSkeletons;
            if (genderSkeleton == null && faceSkeleton == null)
                return;
            if (skeletons == null || skeletons.Count == 0)
            {
                call.Note("OpenCAGE has no skeleton list loaded, so the skeleton names were not checked.");
                if (genderSkeleton != null) after.GenderSkeleton = genderSkeleton;
                if (faceSkeleton != null) after.FaceSkeleton = faceSkeleton;
                return;
            }
            if (genderSkeleton != null)
            {
                string key = skeletons.Keys.FirstOrDefault(o => string.Equals(o, genderSkeleton, StringComparison.OrdinalIgnoreCase));
                if (key == null)
                    throw new McpError("'" + genderSkeleton + "' is not a gender skeleton: they are " + string.Join(", ", skeletons.Keys) + ".");
                after.GenderSkeleton = key;
            }
            HashSet<string> faces = after.GenderSkeleton != null && skeletons.TryGetValue(after.GenderSkeleton, out HashSet<string> found) ? found : null;
            if (faceSkeleton != null)
            {
                string face = faces?.FirstOrDefault(o => string.Equals(o, faceSkeleton, StringComparison.OrdinalIgnoreCase));
                if (face == null)
                    throw new McpError("'" + faceSkeleton + "' is not a face skeleton for " + after.GenderSkeleton + (faces != null ? ": it takes " + string.Join(", ", faces.OrderBy(o => o)) : "") + ".");
                after.FaceSkeleton = face;
            }
            else if (faces != null && !faces.Contains(after.FaceSkeleton ?? ""))
                throw new McpError("The face skeleton '" + after.FaceSkeleton + "' is not one " + after.GenderSkeleton + " offers: give face_skeleton too (" + string.Join(", ", faces.OrderBy(o => o)) + ").");
        }

        private static T ParseEnum<T>(string text, string key) where T : struct
        {
            if (Enum.TryParse(text, true, out T parsed) && Enum.IsDefined(typeof(T), parsed))
                return parsed;
            throw new McpError("'" + text + "' is not a value of " + key + ": it takes " + string.Join(", ", Enum.GetNames(typeof(T))) + ".");
        }
        #endregion

        #region Lookups
        private static Models.CS2 FindModel(Level level, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new McpError("Name a model (list_models shows them).");
            name = name.Trim();
            List<Models.CS2> named = level.Models.Entries.Where(o => o != null && (string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase) || string.Equals(Leaf(o.Name), Leaf(name), StringComparison.OrdinalIgnoreCase))).ToList();
            if (named.Count == 0)
            {
                List<string> near = level.Models.Entries.Where(o => o != null && (o.Name ?? "").IndexOf(Leaf(name), StringComparison.OrdinalIgnoreCase) >= 0).Select(o => o.Name).Take(8).ToList();
                throw new McpError("The open level has no model '" + name + "'." + (near.Count != 0 ? " Similar: " + string.Join("; ", near) + "." : " list_models shows them."));
            }
            Models.CS2 exact = named.FirstOrDefault(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;
            if (named.Count > 1)
                throw new McpError("'" + name + "' could be: " + string.Join("; ", named.Take(10).Select(o => o.Name)) + ". Give the full name.");
            return named[0];
        }

        private static string Leaf(string name)
        {
            string leaf = (name ?? "").Replace('/', '\\');
            if (leaf.EndsWith(".CS2", StringComparison.OrdinalIgnoreCase)) leaf = leaf.Substring(0, leaf.Length - 4);
            int at = leaf.LastIndexOf('\\');
            return at >= 0 ? leaf.Substring(at + 1) : leaf;
        }

        private static Materials.Material FindMaterial(Level level, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new McpError("Name a material (list_materials shows them).");
            name = name.Trim();
            Materials.Material found = level.Materials.Entries.FirstOrDefault(o => o != null && string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));
            if (found != null) return found;
            List<string> near = level.Materials.Entries.Where(o => o != null && (o.Name ?? "").IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0).Select(o => o.Name).Distinct().Take(10).ToList();
            throw new McpError("The level has no material '" + name + "'." + (near.Count != 0 ? " Similar: " + string.Join("; ", near) + "." : " list_materials shows them."));
        }

        private static MaterialMappings.MaterialMapping FindMaterialMapping(Level level, string name)
        {
            List<MaterialMappings.MaterialMapping> entries = level.MaterialMappings?.Entries ?? new List<MaterialMappings.MaterialMapping>();
            MaterialMappings.MaterialMapping found = entries.FirstOrDefault(o => o != null && string.Equals(o.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
            if (found != null) return found;
            List<string> near = entries.Where(o => o != null && (o.Name ?? "").IndexOf(name.Trim(), StringComparison.OrdinalIgnoreCase) >= 0).Select(o => o.Name).Take(10).ToList();
            throw new McpError("The level has no material mapping '" + name + "'." + (near.Count != 0 ? " Similar: " + string.Join("; ", near) + "." : entries.Count == 0 ? " It has none." : " Some it has: " + string.Join("; ", entries.Where(o => o != null).Select(o => o.Name).Take(10)) + "."));
        }

        private sealed class RefComparer<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T x, T y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
        #endregion
    }

    /// <summary>
    /// An object that came into, or left, one of the open level's lists (a COLLISION.MAP row, say), as one undo
    /// step or a piece of one. Named by the object itself: the lists live as long as the level.
    /// </summary>
    internal sealed class McpLevelListEdit<T> : IEdit where T : class
    {
        private readonly Func<Level, List<T>> _list;
        private readonly T _item;
        private readonly int _index;
        private readonly bool _presentAfter;

        public string Label { get; }
        public ShortGuid CompositeId { get; }
        public ShortGuid EntityId { get; }

        public McpLevelListEdit(Func<Level, List<T>> list, T item, int index, bool presentAfter, string label, Composite composite, Entity entity)
        {
            _list = list;
            _item = item;
            _index = index;
            _presentAfter = presentAfter;
            Label = label;
            CompositeId = composite?.shortGUID ?? ShortGuid.Invalid;
            EntityId = entity?.shortGUID ?? ShortGuid.Invalid;
        }

        public void Apply(UndoContext context) => Set(context, _presentAfter);
        public void Revert(UndoContext context) => Set(context, !_presentAfter);

        private void Set(UndoContext context, bool present)
        {
            Level level = context.Content?.Level;
            List<T> list = level == null ? null : _list(level);
            if (list == null)
                throw new InvalidOperationException("The level list this change belongs to is not loaded");
            int at = list.FindIndex(o => ReferenceEquals(o, _item));
            if (present && at < 0)
                list.Insert(Math.Min(Math.Max(_index, 0), list.Count), _item);
            else if (!present && at >= 0)
                list.RemoveAt(at);
            DirtyTracker.MarkLevelDataModified();
        }

        public bool TryMerge(IEdit next) => false;
    }

    /// <summary>
    /// A Character's accessory set for one placement: added, changed or removed. The Character Editor edits these in
    /// place with no undo record; this is the record a tool's change makes. The set object itself is kept, so
    /// re-adding it restores the same object the level held.
    /// </summary>
    internal sealed class McpCharacterAppearanceEdit : IEdit
    {
        private readonly CharacterAccessorySets.CharacterAttributes _set;
        private readonly int _index;
        private readonly McpResourceTools.Appearance _before;
        private readonly McpResourceTools.Appearance _after;

        public string Label { get; }
        public ShortGuid CompositeId { get; }
        public ShortGuid EntityId { get; }

        /// <param name="before">The set as it was, or null if it was not in the level.</param>
        /// <param name="after">The set as it should be, or null to take it out.</param>
        public McpCharacterAppearanceEdit(Composite composite, Entity entity, CharacterAccessorySets.CharacterAttributes set, int index, McpResourceTools.Appearance before, McpResourceTools.Appearance after, string label)
        {
            _set = set;
            _index = index;
            _before = before;
            _after = after;
            Label = label;
            CompositeId = composite.shortGUID;
            EntityId = entity.shortGUID;
        }

        public void Apply(UndoContext context) => Set(context, _after);
        public void Revert(UndoContext context) => Set(context, _before);

        private void Set(UndoContext context, McpResourceTools.Appearance state)
        {
            List<CharacterAccessorySets.CharacterAttributes> entries = context.Content?.Level?.AccessorySets?.Entries;
            if (entries == null)
                throw new InvalidOperationException("The level's accessory sets are not loaded");
            int at = entries.FindIndex(o => ReferenceEquals(o, _set));
            if (state == null)
            {
                if (at >= 0) entries.RemoveAt(at);
            }
            else
            {
                state.ApplyTo(_set);
                if (at < 0)
                    entries.Insert(Math.Min(Math.Max(_index, 0), entries.Count), _set);
            }
            DirtyTracker.MarkLevelDataModified();
            Entity entity = context.Entity(CompositeId, EntityId);
            if (entity != null)
                context.Ui?.ReloadEntity(entity);
        }

        public bool TryMerge(IEdit next) => false;
    }
}

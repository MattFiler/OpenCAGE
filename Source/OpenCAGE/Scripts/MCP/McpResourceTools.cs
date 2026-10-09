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
                Description = "An entity's resources: the model and per-submesh materials it draws (RENDERABLE_INSTANCE), its collision (proxy, flags, physics material, mapping), physics system and animated model, from its 'resource' parameter and its own resource list. Says whether they can be edited (generated and marker-only types cannot). Aliases and proxies are followed to their target. " +
                    "Give 'path' (one placement, from the root) instead of composite+entity to also see the materials it really draws there: a material mapping set on the instance that directly places it, then a 'material' override, applied as Save & Build applies them. Materials are named as list_materials names them (name#index when repeated), so they can be passed back.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite holding the entity (path or id; 'root' for the level's root)."),
                    McpSchema.String("entity", "The entity's id or name."),
                    McpSchema.Any("path", "Or one placement: entity ids/names from the root composite down to the entity (an array, or one string split on '/'), as find_entities and get_placements give them.")),
                ReadOnly = true,
                Idempotent = true,
                Run = GetEntityResources,
            };

            yield return new McpTool()
            {
                Name = "set_renderable",
                Title = "Set renderable",
                Description = "Change the model an entity draws (a ModelReference or another renderable), and/or its submeshes' materials, or remove its renderable - for one entity, several ('entities'), or every ModelReference in the composite. The model's LOD0 submeshes are drawn, lower LODs hung off the first (at most 255 per entity: the game draws no more). One undo step, like the inspector's Edit Resources. " +
                    "It edits the entity in its composite, so every placement of that composite changes (the result says how many): for one placement, set a material mapping on the instance placing it (edit_material_mapping, then set_parameters 'mapping' on that instance or an alias of it), or a ModelReference's 'material' parameter for a one-submesh model. Reaches the game after save_level build=true (the viewport shows it at once). Generated renderables (fog, particles) are refused.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite holding the entity (path or id).", required: true),
                    McpSchema.String("entity", "The entity's id or name."),
                    McpSchema.Strings("entities", "Several entities of the composite (ids or names), changed as one undo step."),
                    McpSchema.Boolean("all_model_references", "Every ModelReference of the composite."),
                    McpSchema.String("model", "The model to draw, by name (list_models). Leave out to keep the current one."),
                    McpSchema.Integer("part", "Which part of the model (a component, as describe_model numbers them), from 0 (default: the current part, or the model's first part with geometry)."),
                    McpSchema.Deprecated(McpSchema.Integer("component", "The same as 'part'.")),
                    McpSchema.Map("materials", "Materials to use: keys are a LOD0 submesh index, a material it draws now, or '*' for all; values are level materials (list_materials' names, name#index when repeated)."),
                    McpSchema.Boolean("remove", "Remove the renderable instead.")),
                Destructive = true,
                Idempotent = true,
                Run = SetRenderable,
            };

            yield return new McpTool()
            {
                Name = "set_collision",
                Title = "Set collision",
                Description = "Give an entity collision or change it - one entity, several ('entities'), or every ModelReference in the composite: the collision proxy it uses (index from list_collision_proxies; 'model' for the proxy its model's submeshes name, as retail pairs them; 'from_model' to import one from its own renderable; 'none'), its physics material (footsteps, impacts), material mapping and flags; or remove it. One undo step (an imported proxy stays in the level); other entities sharing the old mapping keep it. Every placement of the composite changes (the result says how many). Reaches the game after save_level build=true.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite holding the entity (path or id).", required: true),
                    McpSchema.String("entity", "The entity's id or name."),
                    McpSchema.Strings("entities", "Several entities of the composite (ids or names), changed as one undo step."),
                    McpSchema.Boolean("all_model_references", "Every ModelReference of the composite."),
                    McpSchema.Any("proxy", "A proxy index, 'model' (the proxy the drawn model's submeshes name), 'from_model' (make a new proxy from the entity's own renderable, LOD0 triangles), or 'none' to clear it."),
                    McpSchema.String("material", "The physics material (footsteps, impacts): a level material (list_materials' names, name#index when repeated)."),
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
                Description = "Bind a PhysicsSystem entity to one of the level's physics systems (index or name, from list_physics_systems). One undo step, like the inspector's Edit Resources; reaches the game after save_level build=true. It cannot be left unbound: saving binds every PhysicsSystem entity to its system_index parameter's system (0 without one), so 'none' and remove are refused; delete the entity to drop it. import_physics_system makes a new system from a mesh.",
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
                Description = "The open level's collision proxies (Havok compound shapes in COLLISION.HKX): index, instance count, shape classes, bounds (metres, the proxy's own space) and world-host role; include_users also lists the entities using each, unused:true only those no entity uses (left by an import that was not assigned, say). Give 'proxy' for one, with its triangle count. set_collision assigns one; import_collision_proxy makes one.",
                InputSchema = McpSchema.Object(
                    McpSchema.Integer("proxy", "Describe just this proxy (with triangles, bounds and users)."),
                    McpSchema.String("filter", "Text the index, instance count, data offset or role must contain."),
                    McpSchema.Boolean("include_users", "Also list the entities whose collision uses each proxy."),
                    McpSchema.Boolean("unused", "Only proxies no entity's collision uses (world hosts excluded)."),
                    McpSchema.Limit(100, "proxies"),
                    McpSchema.Offset("proxies")),
                ReadOnly = true,
                Idempotent = true,
                Run = ListCollisionProxies,
            };

            yield return new McpTool()
            {
                Name = "import_collision_proxy",
                Title = "Import collision proxy",
                Description = "Make a new collision proxy from a mesh file (absolute path: FBX, glTF, OBJ, DAE) or a model the level holds, in COLLISION.HKX and HKX64 (HKX64 alone on mobile/Switch; in memory; saved with save_level). Not undoable and cannot be deleted (reload without saving to drop it), so the same mesh imported again this session gets the proxy it made before rather than another (reused: true). dry_run only reads the mesh and says which files it would write. Returns the index for set_collision.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("path", "Absolute path of a mesh file."),
                    McpSchema.String("model", "Or a model the level holds (list_models)."),
                    McpSchema.Integer("part", "With 'model': just this part (a component, as describe_model numbers them; default: every part)."),
                    McpSchema.Deprecated(McpSchema.Integer("component", "The same as 'part'.")),
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
                    McpSchema.Limit(100, "systems"),
                    McpSchema.Offset("systems")),
                ReadOnly = true,
                Idempotent = true,
                Run = ListPhysicsSystems,
            };

            yield return new McpTool()
            {
                Name = "import_physics_system",
                Title = "Import physics system",
                Description = "Make a new one-body dynamic physics system (convex hull, up to 64 corners) from a mesh file (absolute path) or a level model, in PHYSICS.HKX/HKX64 (HKX64 alone on mobile/Switch; in memory; saved with save_level; not undoable). With 'composite', names, placement and the default model come from its ModelReference, and its PhysicsSystem entity is bound to the new system (that binding is one undo step).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("path", "Absolute path of a mesh file."),
                    McpSchema.String("model", "Or a model the level holds (list_models); default: the composite's ModelReference model."),
                    McpSchema.Integer("part", "With 'model': just this part (a component, as describe_model numbers them; default: every part)."),
                    McpSchema.Deprecated(McpSchema.Integer("component", "The same as 'part'.")),
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
                Description = "A Character's appearance at each place it is placed: its accessory set (torso, legs, shoes, head, arms and collision composites, with accessory indexes), skeletons, asset type, voice actor, gender, ethnicity, build and foley, with each placement's path, ids and world position. " +
                    "Name the Character by 'path' (one placement from the root, to the Character or to an NPC instance holding one) or by composite + entity (a Character, or an NPC archetype instance: its placements); near + radius keeps those within reach of a point. An NPC archetype's one Character serves every NPC of that kind, so its list can be long. options:true adds the values each attribute takes and the part composites this level's sets use, by slot.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite holding the Character or NPC instance (path or id)."),
                    McpSchema.String("entity", "The Character's (or NPC archetype instance's) id or name."),
                    McpSchema.Any("path", "Or one placement: ids/names from the root composite to the Character or to the NPC instance holding it (an array, or one string split on '/')."),
                    McpSchema.Position("near", "Only placements within 'radius' of this world point"),
                    McpSchema.Number("radius", "With near: metres (default 10)."),
                    McpSchema.Boolean("options", "Also list the skeletons, enum values and part composites by slot."),
                    McpSchema.Limit(50, "placements"),
                    McpSchema.Offset("placements")),
                ReadOnly = true,
                Idempotent = true,
                Run = GetCharacterAppearance,
            };

            yield return new McpTool()
            {
                Name = "set_character_appearance",
                Title = "Set character appearance",
                Description = "Change a Character's appearance (accessory set) at one placement, adding the set if it has none (another Character's set in the same composite instance is left alone), or remove it. 'components', 'attributes' and 'accessory_indexes' are maps; unknown keys and values are refused. " +
                    "A new set needs all five visible parts (torso, legs, shoes, head, arms) unless allow_partial; from_placement starts from another placement's whole set. A changed head brings the face_skeleton retail pairs with it, and its slot's accessory index. The set is keyed by the placement's path: group_into_composite, deinstance and move_into_composite (and the editor's own refactors) carry it along; copying or porting an NPC does not, and check_character_appearances finds and repairs such orphans. One undo step; written by save_level. get_character_appearance lists placements and allowed values.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite holding the Character or NPC instance (path or id)."),
                    McpSchema.String("entity", "The Character's (or NPC archetype instance's) id or name."),
                    McpSchema.Any("path", "Or the placement itself: ids/names from the root to the Character or the NPC instance holding it."),
                    McpSchema.Any("placement", "With composite + entity: which placement, its index or instance id from get_character_appearance (default: the only one)."),
                    McpSchema.Any("from_placement", "Start from this placement's set: an index or instance id of the same Character, or a path from the root to any Character."),
                    McpSchema.Map("components", "Keys torso, legs, shoes, head, arms, collision; values composite paths or ids, or 'none'."),
                    McpSchema.Map("accessory_indexes", "Keys as components; values the part's accessory index (retail: each slot's own index, 0-5; heads also other numbers)."),
                    McpSchema.Map("attributes", "Keys gender_skeleton, face_skeleton, asset_type, voice_actor, gender, ethnicity, build, foley_torso, foley_leg, foley_footwear."),
                    McpSchema.Boolean("allow_partial", "Allow a set missing visible parts (they then show nothing)."),
                    McpSchema.Boolean("remove", "Remove this placement's accessory set instead.")),
                Destructive = true,
                Idempotent = true,
                Run = SetCharacterAppearance,
            };

            yield return new McpTool()
            {
                Name = "check_character_appearances",
                Title = "Check character appearances",
                Description = "Check the open level's character accessory sets (NPC looks): sets no placement matches any more (orphaned by copying or porting an NPC, or by an older edit that moved one, which loses its look), duplicates, parts not in the level, sets missing visible parts or with unset accessory indexes. repair:true points each orphan at the one placement its Character now has without a set, and fills unset indexes, as one undo step; the rest is reported for set_character_appearance.",
                InputSchema = McpSchema.Object(
                    McpSchema.Boolean("repair", "Fix what can be fixed without guessing (one undo step). Default false: report only.")),
                Idempotent = true,
                Run = CheckCharacterAppearances,
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
                Commands commands = level.Commands;
                List<Entity> chain = null;
                Target target;
                if (call.Has("path"))
                {
                    if (call.Has("composite") || call.Has("entity"))
                        throw McpError.Invalid("Give 'path' (one placement from the root), or 'composite' + 'entity', not both.");
                    chain = McpRegion.ChainFromNames(commands, ReadSteps(call.Token("path"), "path"));
                    Composite holder = McpValueSource.CompositeOf(commands, commands.EntryPoints[0], chain);
                    target = Resolve(level, holder, chain[chain.Count - 1], write: false, follow: true, call: call);
                }
                else
                {
                    if (!call.Has("composite") || !call.Has("entity"))
                        throw McpError.Invalid("Give 'composite' and 'entity', or 'path' (one placement from the root, as find_entities gives it).");
                    target = Resolve(level, call, write: false, follow: true);
                }
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

                int placements = McpAssets.PlacementCount(commands, target.Composite);
                result["composite_placed"] = placements;
                if (chain != null && chain.Count != 0 && ReferenceEquals(chain[chain.Count - 1], target.Entity))
                    result["at_this_placement"] = Effective(level, chain, target);
                else if (placements > 1 && target.Find(ResourceType.RENDERABLE_INSTANCE) != null)
                    call.Note(target.Composite.name + " is placed " + placements + " times; a placement can draw other materials (a mapping on the instance placing it, or a 'material' override): pass 'path' for one placement to see what it really draws.");
                return result;
            });
        }

        /// <summary>
        /// What one placement of a renderable really draws: the mapping set on the instance directly placing it (its effective
        /// 'mapping', aliases included), then its own effective 'material' override, as instancing applies them.
        /// </summary>
        private static JObject Effective(Level level, List<Entity> chain, Target target)
        {
            Commands commands = level.Commands;
            McpValueSource placement = new McpValueSource(commands, commands.EntryPoints[0], chain);
            int k = chain.Count - 1;
            JObject result = new JObject() { ["path"] = McpRegion.ChainJson(commands, chain)["path"] };
            cTransform world = McpRegion.Walker(commands).Evaluate(commands.EntryPoints[0], chain).World;
            if (world != null) { result["position"] = McpCollision.V(world.position); result["space"] = "world"; }
            List<RenderableElements.Element> run = target.Entity.GetResource(ResourceType.RENDERABLE_INSTANCE, true)?.RenderableInstance;
            if (run == null || run.Count == 0)
            {
                result["renderable"] = "none";
                return result;
            }

            MaterialMappings.MaterialMapping mapping = null;
            if (k > 0)
            {
                McpValueSource.Source source = placement.Of(k - 1, chain[k - 1], ShortGuids.mapping);
                if (source.Value is cResource resource && resource.shortGUID != ShortGuid.Invalid)
                {
                    mapping = MaterialRemappingUtils.TryResolveMaterialMapping(level, resource);
                    JObject described = new JObject() { ["set"] = mapping?.Name ?? McpScript.Id(resource.shortGUID), ["on"] = McpRegion.DescribeChain(commands, chain.Take(k).ToList()), ["from"] = source.Kind };
                    if (source.Kind == "alias") described["alias"] = McpScript.EntityName(commands, source.HolderComposite, source.Holder) + " in " + source.HolderComposite.name;
                    if (mapping == null) described["note"] = "no mapping set of the level has that id";
                    result["mapping"] = described;
                }
            }
            List<RenderableElements.Element> drawn = mapping == null ? run : MaterialRemappingUtils.ApplyMapping(level, mapping, run);
            McpValueSource.Source material = placement.Of(k, target.Entity, ShortGuidUtils.Generate("material"));
            string overrideName = (material.Value as cString)?.value;
            if (!string.IsNullOrWhiteSpace(overrideName) && material.Kind != "default")
            {
                List<RenderableElements.Element> overridden = MaterialRemappingUtils.ApplyMaterialParameterOverride(level, overrideName, drawn);
                result["material_override"] = new JObject()
                {
                    ["value"] = overrideName,
                    ["from"] = material.Kind,
                    ["applied"] = !ReferenceEquals(overridden, drawn),
                };
                if (ReferenceEquals(overridden, drawn))
                    ((JObject)result["material_override"])["why_not"] = drawn.Count != 1 ? "it only applies to a one-submesh renderable" : "it names a material of another slot, or the one already drawn";
                drawn = overridden;
            }
            McpAssets.MaterialNames names = McpAssets.Names(level);
            JArray submeshes = new JArray();
            for (int i = 0; i < drawn.Count && i < 64; i++)
            {
                RenderableElements.Element element = drawn[i];
                if (element == null) continue;
                JObject entry = new JObject() { ["index"] = i, ["material"] = names.Ref(element.Material) };
                Materials.Material authored = i < run.Count ? run[i]?.Material : null;
                if (!ReferenceEquals(authored, element.Material)) entry["instead_of"] = names.Ref(authored);
                submeshes.Add(entry);
            }
            result["draws"] = submeshes;
            if (mapping == null && material.Kind == "default")
                result["note"] = "No mapping or material override at this placement: it draws what the entity's renderable names.";
            return result;
        }

        private static List<string> ReadSteps(JToken token, string name)
        {
            if (token is JArray array && array.Count != 0 && array.All(o => o.Type == JTokenType.String || o.Type == JTokenType.Integer))
                return array.Select(o => McpValues.ReadString(o).Trim()).ToList();
            if (token != null && token.Type == JTokenType.String && ((string)token).Trim().Length != 0)
                return ((string)token).Split(new[] { '/', '>' }, StringSplitOptions.RemoveEmptyEntries).Select(o => o.Trim()).ToList();
            throw McpError.Invalid("'" + name + "' is a path from the root composite: entity ids or names (an array, or one string split on '/').");
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
                    item["collision"] = DescribeMapping(level, reference.CollisionMapping, sharing);
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
                    //One shape for an animated model entry everywhere (set_animated_model and describe_animated_prop show the same)
                    item["animated_model"] = reference.AnimatedModel == null ? JValue.CreateNull() : (JToken)McpAnimationAssetTools.DescribeEntry(reference.AnimatedModel, level.Commands);
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
            McpAssets.MaterialNames names = McpAssets.Names(level);
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
                entry["material"] = names.Ref(element.Material);
                if (element.Model?.Material != null && !ReferenceEquals(element.Model.Material, element.Material))
                    entry["default_material"] = names.Ref(element.Model.Material);
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

        private static JToken DescribeMapping(Level level, CollisionMaps.COLLISION_MAPPING mapping, Dictionary<CollisionMaps.COLLISION_MAPPING, int> sharing)
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
            result["material"] = McpAssets.MaterialRef(level, mapping.Material);
            result["material_mapping"] = mapping.MaterialMapping?.Name;
            if (sharing != null && sharing.TryGetValue(mapping, out int users) && users > 1)
                result["shared_by_references"] = users;
            return result;
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
        /// <summary>
        /// The entities a set_renderable / set_collision call is for - 'entity', 'entities', or every ModelReference of the
        /// composite - each looked at where its references of this type are kept. Aliases and proxies are refused (pointed at their target).
        /// </summary>
        private static List<Target> Targets(Level level, McpCall call, ResourceType type)
        {
            Commands commands = level.Commands;
            Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
            int ways = (call.Has("entity") ? 1 : 0) + (call.Has("entities") ? 1 : 0) + (call.Bool("all_model_references") ? 1 : 0);
            if (ways != 1)
                throw McpError.Invalid("Give one of 'entity', 'entities' (several) or all_model_references: true.");
            List<Entity> entities;
            if (call.Has("entity"))
                entities = new List<Entity>() { McpScript.FindEntity(commands, composite, call.Str("entity")) };
            else if (call.Has("entities"))
                entities = call.StrList("entities").Select(o => McpScript.FindEntity(commands, composite, o)).Distinct().ToList();
            else
            {
                entities = composite.functions.Where(o => o.function == FunctionType.ModelReference).Cast<Entity>().ToList();
                if (entities.Count == 0)
                    throw new McpError(McpErrorCodes.NotFound, composite.name + " has no ModelReference entities (get_composite lists what it holds).");
            }
            if (entities.Count == 0)
                throw McpError.Invalid("'entities' is empty.");
            return entities.Select(o => Resolve(level, composite, o, write: true, follow: false, call: call).For(type)).ToList();
        }

        private static string LabelFor(List<Target> targets) => targets.Count == 1 ? targets[0].Label : targets.Count + " entities in " + McpScript.CompositeLeaf(targets[0].Composite);

        /// <summary>What an edit of entities in their composite reaches: how many placements change, and that it needs a build.</summary>
        private static void Reach(McpCall call, JObject result, Level level, Composite composite, string instead)
        {
            result["placements_affected"] = McpAssets.PlacementCount(level.Commands, composite);
            result["needs_build"] = true;
            call.Note(McpAssets.SharedEditNote(level.Commands, composite, instead));
            call.Note("It reaches the game when the level is saved with build=true (save_level); the viewport shows it at once.");
        }

        private const string OnePlacementMaterials = "For one placement: a material mapping set on the instance placing it (edit_material_mapping, then set_parameters 'mapping' on that instance or an alias of it; get_entity_resources with 'path' shows the result).";

        private static object SetRenderable(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel().Level;
                McpEditor.RequireUndoIdle();
                if (call.Has("part") && call.Has("component"))
                    throw McpError.Invalid("'part' and 'component' mean the same; give one.");
                List<Target> targets = Targets(level, call, ResourceType.RENDERABLE_INSTANCE);
                bool changes = call.Has("model") || call.Has("part") || call.Has("component") || call.Has("materials");
                string label = LabelFor(targets);

                if (call.Bool("remove"))
                {
                    if (changes)
                        throw McpError.Invalid("'remove' cannot be combined with model, part or materials.");
                    List<Target> having = targets.Where(o => o.Find(ResourceType.RENDERABLE_INSTANCE) != null).ToList();
                    if (having.Count == 0)
                        throw new McpError(McpErrorCodes.NotFound, (targets.Count == 1 ? targets[0].Name : "None of them") + " has no renderable to remove.");
                    using (UndoStack.Current.BeginGroup("AI: Remove renderable of " + label))
                        foreach (Target target in having)
                            Commit(target, "AI: Remove renderable of " + target.Label, list => list.RemoveAll(o => o != null && o.resource_type == ResourceType.RENDERABLE_INSTANCE));
                    JObject removed = new JObject() { ["removed"] = "RENDERABLE_INSTANCE", ["entities"] = new JArray(having.Select(EntityBrief)) };
                    if (having.Count < targets.Count) removed["had_none"] = new JArray(targets.Except(having).Select(o => o.Name));
                    Reach(call, removed, level, targets[0].Composite, null);
                    return removed;
                }
                if (!changes)
                    throw McpError.Invalid("Say what to change: 'model', 'part', 'materials', or remove: true.");

                //Several entities with only materials to change: those that draw no model of the level are left out (a collision-only ModelReference, say)
                if (targets.Count > 1 && !call.Has("model"))
                {
                    List<Target> drawing = targets.Where(o => SafeFindModel(level.Models, o.Find(ResourceType.RENDERABLE_INSTANCE)?.RenderableInstance?.FirstOrDefault(e => e?.Model != null)?.Model) != null).ToList();
                    if (drawing.Count == 0)
                        throw McpError.Invalid("None of them draws a model the level holds: give 'model'.");
                    if (drawing.Count < targets.Count)
                        call.Note((targets.Count - drawing.Count) + " of them draw nothing and were left alone: " + string.Join(", ", targets.Except(drawing).Take(10).Select(o => o.Name)) + ".");
                    targets = drawing;
                    label = LabelFor(targets);
                }
                //Every entity is worked out before anything changes, so a refusal for one leaves them all as they were
                List<(Target target, List<RenderableElements.Element> run)> plans = targets.Select(o => (o, PlanRenderable(level, call, o))).ToList();
                using (UndoStack.Current.BeginGroup("AI: Set model of " + label))
                {
                    foreach ((Target target, List<RenderableElements.Element> run) in plans)
                    {
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
                        if (target.Function != FunctionType.ModelReference && target.Find(ResourceType.RENDERABLE_INSTANCE) == null)
                            call.Note(target.Function + " did not draw anything before; not every type shows a renderable in game.");
                    }
                }

                JObject result = new JObject();
                if (targets.Count == 1)
                {
                    result["entity"] = EntityBrief(targets[0]);
                    result["renderable"] = DescribeRenderable(level, targets[0].Find(ResourceType.RENDERABLE_INSTANCE)?.RenderableInstance);
                }
                else
                    result["entities"] = new JArray(targets.Select(o => new JObject()
                    {
                        ["entity"] = EntityBrief(o),
                        ["renderable"] = DescribeRenderable(level, o.Find(ResourceType.RENDERABLE_INSTANCE)?.RenderableInstance),
                    }));
                Reach(call, result, level, targets[0].Composite, OnePlacementMaterials);
                return result;
            });
        }

        /// <summary>The run an entity will draw after a set_renderable call (registered with the level's renderable runs). UI thread.</summary>
        private static List<RenderableElements.Element> PlanRenderable(Level level, McpCall call, Target target)
        {
            ResourceReference existing = target.Find(ResourceType.RENDERABLE_INSTANCE);
            Models models = level.Models;
            McpAssets.MaterialNames names = McpAssets.Names(level);
            Models.CS2.Component.LOD.Submesh firstNow = existing?.RenderableInstance?.FirstOrDefault(o => o?.Model != null)?.Model;
            Models.CS2 currentModel = SafeFindModel(models, firstNow);
            Models.CS2.Component currentComponent = SafeFindComponent(models, firstNow);

            Models.CS2 model;
            if (call.Has("model"))
                model = McpAssets.FindModel(level, call.Str("model"));
            else if (currentModel != null)
                model = currentModel;
            else
                throw McpError.Invalid(target.Name + " does not draw a model the level holds: give 'model' (list_models shows them).");

            int part;
            string partArgument = call.Has("component") ? "component" : "part";
            if (call.Has(partArgument))
            {
                part = call.Int(partArgument);
                if (part < 0 || part >= model.Components.Count)
                    throw McpError.Invalid(model.Name + " has " + model.Components.Count + " part(s): '" + partArgument + "' takes 0 to " + (model.Components.Count - 1) + ".");
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
                throw McpError.Invalid(model.Name + (part >= 0 ? " part " + part : "") + " has no geometry to draw.");
            List<Models.CS2.Component.LOD.Submesh> lod0 = component.LODs[0].Submeshes;
            if (lod0.Count > RenderableElements.MaxElementsPerInstance)
                throw new McpError(McpErrorCodes.Refused, model.Name + " part " + part + " has " + lod0.Count + " submeshes at LOD0, but the game draws at most " + RenderableElements.MaxElementsPerInstance + " for one entity (the rest would not show). Split it into components of " + RenderableElements.MaxElementsPerInstance + " or fewer (import_model splits on the way in) and place each one.");

            //What each submesh is drawn with now, kept when the model stays the same
            bool sameComponent = ReferenceEquals(component, currentComponent);
            Dictionary<Models.CS2.Component.LOD.Submesh, Materials.Material> now = new Dictionary<Models.CS2.Component.LOD.Submesh, Materials.Material>(new RefComparer<Models.CS2.Component.LOD.Submesh>());
            if (sameComponent && existing?.RenderableInstance != null)
                CollectMaterials(existing.RenderableInstance, now);
            List<Models.CS2.Component.LOD.Submesh> all = component.LODs.SelectMany(o => o.Submeshes).ToList();
            Dictionary<Models.CS2.Component.LOD.Submesh, Materials.Material> start = new Dictionary<Models.CS2.Component.LOD.Submesh, Materials.Material>(new RefComparer<Models.CS2.Component.LOD.Submesh>());
            foreach (Models.CS2.Component.LOD.Submesh submesh in all)
                start[submesh] = now.TryGetValue(submesh, out Materials.Material kept) ? kept : submesh.Material;
            Dictionary<Models.CS2.Component.LOD.Submesh, Materials.Material> chosen = new Dictionary<Models.CS2.Component.LOD.Submesh, Materials.Material>(start, new RefComparer<Models.CS2.Component.LOD.Submesh>());

            JObject overrides = call.Object("materials");
            if (overrides != null)
            {
                //'*' first, then by the material drawn now, then by submesh index: the most specific wins
                List<JProperty> entries = overrides.Properties().ToList();
                foreach (JProperty entry in entries.Where(o => o.Name.Trim() == "*"))
                {
                    Materials.Material to = McpAssets.FindMaterial(level, ValueText(entry.Value, "materials['*']"), "materials['*']");
                    foreach (Models.CS2.Component.LOD.Submesh submesh in all) chosen[submesh] = to;
                }
                foreach (JProperty entry in entries.Where(o => o.Name.Trim() != "*" && !int.TryParse(o.Name.Trim(), out int _)))
                {
                    string old = entry.Name.Trim();
                    //The key as any material argument takes it (name#index, a display or stored name), matched by identity; else by name
                    Materials.Material keyed = null;
                    try { keyed = McpAssets.FindMaterial(level, old, "materials key"); } catch (McpError) { }
                    List<Models.CS2.Component.LOD.Submesh> matching = keyed == null ? new List<Models.CS2.Component.LOD.Submesh>() : all.Where(o => ReferenceEquals(start[o], keyed)).ToList();
                    if (matching.Count == 0)
                        matching = all.Where(o => start[o] != null && (string.Equals(start[o].Name, old, StringComparison.OrdinalIgnoreCase) || string.Equals(names.Display(start[o]), old, StringComparison.OrdinalIgnoreCase) || string.Equals(names.Ref(start[o]), old, StringComparison.OrdinalIgnoreCase))).ToList();
                    if (matching.Count == 0)
                        throw new McpError(McpErrorCodes.NotFound, "No submesh of " + model.Name + " part " + part + " draws with '" + old + "'. It draws: " + string.Join(", ", lod0.Select((o, i) => i + ": " + (names.Ref(start[o]) ?? "(none)"))) + ". Key by submesh index, or '*' for all.");
                    Materials.Material to = McpAssets.FindMaterial(level, ValueText(entry.Value, "materials['" + old + "']"), "materials['" + old + "']");
                    foreach (Models.CS2.Component.LOD.Submesh submesh in matching) chosen[submesh] = to;
                }
                foreach (JProperty entry in entries.Where(o => int.TryParse(o.Name.Trim(), out int _)))
                {
                    int index = int.Parse(entry.Name.Trim(), CultureInfo.InvariantCulture);
                    if (index < 0 || index >= lod0.Count)
                        throw McpError.Invalid(model.Name + " part " + part + " has " + lod0.Count + " submesh(es): material indexes run 0 to " + (lod0.Count - 1) + ".");
                    Materials.Material to = McpAssets.FindMaterial(level, ValueText(entry.Value, "materials['" + index + "']"), "materials['" + index + "']");
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
            return level.RenderableElements.EnsureRegistered(run);
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
                throw McpError.Invalid("'" + name + "' needs a material name.");
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
                List<Target> targets = Targets(level, call, ResourceType.COLLISION_MAPPING);
                bool changes = call.Has("proxy") || call.Has("material") || call.Has("material_mapping") || call.Has("flags");
                string label = LabelFor(targets);

                if (call.Bool("remove"))
                {
                    if (changes)
                        throw McpError.Invalid("'remove' cannot be combined with proxy, material, material_mapping or flags.");
                    List<Target> having = targets.Where(o => o.Find(ResourceType.COLLISION_MAPPING) != null).ToList();
                    if (having.Count == 0)
                        throw new McpError(McpErrorCodes.NotFound, (targets.Count == 1 ? targets[0].Name : "None of them") + " has no collision to remove.");
                    using (UndoStack.Current.BeginGroup("AI: Remove collision of " + label))
                        foreach (Target target in having)
                            Commit(target, "AI: Remove collision of " + target.Label, list => list.RemoveAll(o => o != null && o.resource_type == ResourceType.COLLISION_MAPPING));
                    JObject removed = new JObject() { ["removed"] = "COLLISION_MAPPING", ["entities"] = new JArray(having.Select(EntityBrief)) };
                    Reach(call, removed, level, targets[0].Composite, null);
                    return removed;
                }

                //Every entity is checked first; only then is anything (a new proxy above all, which cannot be taken back) made
                CollisionRequest request = CollisionRequest.From(call);
                List<CollisionPlan> plans = targets.Select(o => PlanCollision(level, request, o, existingIsError: targets.Count == 1)).Where(o => o != null).ToList();
                if (plans.Count == 0)
                    throw new McpError(McpErrorCodes.Conflict, "They all have collision already. Say what to change: proxy, material, material_mapping, flags - or remove: true.");
                JArray imported = new JArray();
                using (UndoStack.Current.BeginGroup((targets.Count == 1 && plans[0].Existing == null ? "AI: Add collision to " : "AI: Set collision of ") + label))
                    foreach (CollisionPlan plan in plans)
                    {
                        JObject made = ApplyCollision(call, level, plan);
                        if (made != null) imported.Add(made);
                    }

                JObject result = new JObject();
                if (plans.Count == 1)
                {
                    result["entity"] = EntityBrief(plans[0].Target);
                    result["collision"] = DescribeMapping(level, plans[0].Target.Find(ResourceType.COLLISION_MAPPING)?.CollisionMapping, null);
                }
                else
                    result["entities"] = new JArray(plans.Select(o => new JObject() { ["entity"] = EntityBrief(o.Target), ["collision"] = DescribeMapping(level, o.Target.Find(ResourceType.COLLISION_MAPPING)?.CollisionMapping, null) }));
                if (plans.Count < targets.Count)
                    call.Note((targets.Count - plans.Count) + " of them already had collision and nothing to change was given for them; they were left alone.");
                if (imported.Count != 0)
                {
                    result["imported_proxy"] = imported.Count == 1 ? imported[0] : imported;
                    call.Note("A new proxy is in the level's collision files in memory: undo takes the assignment back but not the proxy, which stays unused (list_collision_proxies unused:true) until the level is reloaded without saving.");
                }
                int sharers = plans.Sum(o => o.Sharers);
                if (sharers > 0)
                    call.Note(sharers + " other reference(s) shared the old collision row(s); they keep them unchanged.");
                foreach (string note in plans.SelectMany(o => o.Notes).Distinct())
                    call.Note(note);
                Reach(call, result, level, targets[0].Composite, null);
                return result;
            });
        }

        /// <summary>What a set_collision call (or place_model's 'collision') asks for, read once.</summary>
        internal sealed class CollisionRequest
        {
            public JToken Proxy;
            public string Material, Mapping;
            public JToken Flags;
            public bool HasProxy => Proxy != null && Proxy.Type != JTokenType.Null;
            public bool HasMaterial => Material != null;
            public bool HasMapping => Mapping != null;
            public bool HasFlags => Flags != null && Flags.Type != JTokenType.Null;
            public bool Any => HasProxy || HasMaterial || HasMapping || HasFlags;

            public static CollisionRequest From(McpCall call) => new CollisionRequest()
            {
                Proxy = call.Token("proxy"),
                Material = call.Str("material"),
                Mapping = call.Str("material_mapping"),
                Flags = call.Token("flags"),
            };
        }

        /// <summary>One entity's new collision row, worked out before anything changes.</summary>
        private sealed class CollisionPlan
        {
            public Target Target;
            public ResourceReference Existing;
            public CollisionMaps.COLLISION_MAPPING Row;
            public CollisionProxyImporter.MeshSource FromModel;
            public int Sharers;
            public string Label;
            public List<string> Notes = new List<string>();
        }

        /// <summary>
        /// The row an entity gets. Null when it has collision already and nothing to change was asked for (an error instead
        /// when <paramref name="existingIsError"/>). UI thread; nothing changes.
        /// </summary>
        private static CollisionPlan PlanCollision(Level level, CollisionRequest request, Target target, bool existingIsError)
        {
            ResourceReference existing = target.Find(ResourceType.COLLISION_MAPPING);
            CollisionMaps.COLLISION_MAPPING current = existing?.CollisionMapping;
            if (!request.Any && existing != null)
            {
                if (!existingIsError) return null;
                throw new McpError(McpErrorCodes.Conflict, target.Name + " already has collision. Say what to change: proxy, material, material_mapping, flags - or remove: true.");
            }

            //A proxy its model names comes with the settings retail placements of that proxy use, unless told otherwise
            HavokPackfile.StaticCompoundShape proxy = current?.CollisionProxy;
            bool proxyChanged = false;
            CollisionProxyImporter.MeshSource fromModel = null;
            CollisionMaps.COLLISION_MAPPING template = null;
            List<string> notes = new List<string>();
            if (request.HasProxy || existing == null)
            {
                string text = !request.HasProxy ? "model" : request.Proxy.Type == JTokenType.String ? ((string)request.Proxy).Trim() : request.Proxy.ToString();
                proxyChanged = true;
                List<RenderableElements.Element> run = target.Entity.GetResource(ResourceType.RENDERABLE_INSTANCE, true)?.RenderableInstance;
                if (IsNone(text))
                    proxy = null;
                else if (string.Equals(text, "model", StringComparison.OrdinalIgnoreCase))
                {
                    HavokPackfile hkx = RequireCollision(level);
                    Models.CS2.Component component = SafeFindComponent(level.Models, run?.FirstOrDefault(o => o?.Model != null)?.Model);
                    int index = McpAssets.CollisionProxyOf(component);
                    if (index < 0)
                    {
                        if (!request.HasProxy)
                            throw McpError.Invalid(target.Name + " has no collision yet, and " + (component == null ? "it draws no model the level holds" : "its model's submeshes name no collision proxy") + ": give 'proxy' (an index from list_collision_proxies, or 'from_model' to make one from what it draws).");
                        throw new McpError(McpErrorCodes.NotFound, (component == null ? target.Name + " draws no model the level holds" : "The submeshes of the model " + target.Name + " draws name no collision proxy") + ". Use 'from_model' to make one from it, or give a proxy index (list_collision_proxies).");
                    }
                    proxy = hkx.GetCompound(index);
                    if (proxy == null)
                        throw new McpError(McpErrorCodes.NotFound, "The model names collision proxy " + index + ", which this level does not have (a port from another level, perhaps). Use 'from_model' to make one.");
                    template = AllReferences(level.Commands).Select(o => o.Item3).FirstOrDefault(o => o.resource_type == ResourceType.COLLISION_MAPPING && ReferenceEquals(o.CollisionMapping?.CollisionProxy, proxy))?.CollisionMapping;
                }
                else if (string.Equals(text, "from_model", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "from_renderable", StringComparison.OrdinalIgnoreCase))
                {
                    RequireCollision(level);
                    if (run == null || run.Count == 0)
                        throw McpError.Invalid(target.Name + " draws nothing to make a collision proxy from. Give it a model first (set_renderable), or import one with import_collision_proxy.");
                    string name = SafeFindModel(level.Models, run.FirstOrDefault(o => o?.Model != null)?.Model)?.Name ?? target.Name;
                    try { fromModel = CollisionProxyImporter.FromRenderableRun(run, name); }
                    catch (Exception e) when (!(e is McpError)) { throw new McpError(McpErrorCodes.Failed, "No collision mesh could be made from " + target.Name + "'s renderable: " + e.Message); }
                }
                else if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
                {
                    HavokPackfile hkx = RequireCollision(level);
                    proxy = hkx.GetCompound(index);
                    if (proxy == null)
                        throw new McpError(McpErrorCodes.NotFound, "There is no collision proxy " + index + " (the level has " + hkx.StaticCompoundShapes.Count + "; list_collision_proxies shows them).");
                    if (ReferenceEquals(proxy, hkx.WorldHostPrimary) || ReferenceEquals(proxy, hkx.WorldHostSecondary))
                        throw McpError.Invalid("Proxy " + index + " is a world host (it holds every placed collider in the level), not a shape a mapping can use.");
                }
                else
                    throw McpError.Invalid("'proxy' takes a proxy index, 'model', 'from_model' or 'none'.");
            }

            CollisionMaps.CollisionFlags flags = request.HasFlags ? ParseFlags(request.Flags) : current?.Flags ?? template?.Flags ?? DefaultFlags;
            Materials.Material material = request.HasMaterial ? McpAssets.FindMaterial(level, request.Material) : current?.Material ?? template?.Material;
            MaterialMappings.MaterialMapping mapping = current?.MaterialMapping ?? template?.MaterialMapping;
            if (request.HasMapping)
                mapping = IsNone(request.Mapping) ? null : FindMaterialMapping(level, request.Mapping);
            if (template != null && !request.HasMaterial && current == null)
                notes.Add("Flags, physics material and material mapping were copied from a placement that already uses proxy " + proxy.ProxyIndex + ", as the game's own placements of that model have them.");
            if (proxy == null && fromModel == null)
                notes.Add("The mapping has no collision proxy, so it collides with nothing: give 'proxy'.");
            if (material == null)
                notes.Add("The mapping has no physics material (footsteps and impacts sound default): give 'material'.");

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
            return new CollisionPlan()
            {
                Target = target,
                Existing = existing,
                Row = row,
                FromModel = fromModel,
                Sharers = current == null ? 0 : AllReferences(level.Commands).Count(o => ReferenceEquals(o.Item3.CollisionMapping, current)) - 1,
                Label = (existing == null ? "AI: Add collision to " : "AI: Set collision of ") + target.Label,
                Notes = notes,
            };
        }

        /// <summary>Make the planned change: the proxy first when one is to be imported (not undoable), then the row and reference as one step. UI thread.</summary>
        private static JObject ApplyCollision(McpCall call, Level level, CollisionPlan plan)
        {
            JObject imported = null;
            if (plan.FromModel != null)
            {
                uint userData = UserData(level, plan.Row.Material);
                uint filterInfo = (plan.Row.Flags & CollisionMaps.CollisionFlags.WORLD) != 0 ? 3u : 9u;
                HavokPackfile.StaticCompoundShape proxy;
                bool reused;
                try
                {
                    using (McpEditorTools.Heartbeat(call, "Building a collision proxy from " + plan.FromModel.Name))
                        proxy = ImportProxyOnce(level, plan.FromModel, userData, filterInfo, out reused);
                }
                catch (Exception e) when (!(e is McpError)) { throw new McpError(McpErrorCodes.Failed, "The collision proxy could not be created: " + e.Message); }
                plan.Row.CollisionProxy = proxy;
                imported = new JObject() { ["proxy"] = proxy.ProxyIndex, ["triangles"] = plan.FromModel.TriangleCount, ["from"] = plan.FromModel.Name };
                if (reused) imported["reused"] = true;
            }

            Target target = plan.Target;
            ShortGuid resourceId = target.ResourceId;
            CollisionMaps.COLLISION_MAPPING row = plan.Row;
            McpLevelListEdit<CollisionMaps.COLLISION_MAPPING> addRow = new McpLevelListEdit<CollisionMaps.COLLISION_MAPPING>(l => l.CollisionMaps?.Entries, row, level.CollisionMaps.Entries.Count, true, plan.Label, target.Composite, target.Entity);
            Commit(target, plan.Label, list =>
            {
                ResourceReference reference = list.FirstOrDefault(o => o != null && o.resource_type == ResourceType.COLLISION_MAPPING);
                if (reference == null)
                {
                    reference = new ResourceReference(ResourceType.COLLISION_MAPPING) { resource_id = resourceId };
                    list.Add(reference);
                }
                reference.CollisionMapping = row;
            }, addRow);
            return imported;
        }

        /// <summary>
        /// Give a ModelReference collision (place_model's 'collision'): proxy 'model' (what its model names, with the settings
        /// retail uses), an index, 'from_model' or 'none'. One undo step of its own; group it with the placement. UI thread.
        /// </summary>
        internal static JObject GiveCollision(McpCall call, Level level, Composite composite, FunctionEntity entity, JToken proxy, string material, JToken flags)
        {
            Target target = Resolve(level, composite, entity, write: true, follow: false, call: call).For(ResourceType.COLLISION_MAPPING);
            CollisionRequest request = new CollisionRequest() { Proxy = proxy, Material = material, Flags = flags };
            CollisionPlan plan = PlanCollision(level, request, target, existingIsError: false);
            if (plan == null) return null;
            JObject imported = ApplyCollision(call, level, plan);
            foreach (string note in plan.Notes) call.Note(note);
            JObject described = (JObject)DescribeMapping(level, target.Find(ResourceType.COLLISION_MAPPING)?.CollisionMapping, null);
            if (imported != null) described["imported_proxy"] = imported;
            return described;
        }

        /// <summary>Check collision flags before anything changes (throws as set_collision would).</summary>
        internal static void CheckCollisionFlags(JToken flags)
        {
            if (flags != null && flags.Type != JTokenType.Null) ParseFlags(flags);
        }

        /// <summary>Imported proxies of this session by what they were made from, so the same mesh imported again reuses its proxy rather than leaving another behind.</summary>
        private static readonly Dictionary<string, (WeakReference level, int proxy)> _importedProxies = new Dictionary<string, (WeakReference, int)>();

        private static HavokPackfile.StaticCompoundShape ImportProxyOnce(Level level, CollisionProxyImporter.MeshSource source, uint userData, uint filterInfo, out bool reused)
        {
            reused = false;
            string key = MeshKey(source) + "|" + userData + "|" + filterInfo;
            HavokPackfile hkx = RequireCollision(level);
            if (_importedProxies.TryGetValue(key, out (WeakReference level, int proxy) known) && ReferenceEquals(known.level.Target, level))
            {
                HavokPackfile.StaticCompoundShape existing = hkx.GetCompound(known.proxy);
                if (existing != null)
                {
                    reused = true;
                    return existing;
                }
            }
            HavokPackfile.StaticCompoundShape created = CollisionProxyImporter.Import(level, source, userData, filterInfo);
            Singleton.OnResourceModified?.Invoke();
            _importedProxies[key] = (new WeakReference(level), created.ProxyIndex);
            return created;
        }

        /// <summary>A hash of a mesh's triangles (positions to the tenth of a millimetre).</summary>
        private static string MeshKey(CollisionProxyImporter.MeshSource source)
        {
            unchecked
            {
                long hash = 1469598103934665603L;
                foreach (Vector3 p in source.Positions)
                {
                    hash = (hash ^ (long)Math.Round(p.X * 10000.0)) * 1099511628211L;
                    hash = (hash ^ (long)Math.Round(p.Y * 10000.0)) * 1099511628211L;
                    hash = (hash ^ (long)Math.Round(p.Z * 10000.0)) * 1099511628211L;
                }
                foreach (int i in source.Indices)
                    hash = (hash ^ i) * 1099511628211L;
                return source.Positions.Count + ":" + source.Indices.Count + ":" + hash.ToString("X16");
            }
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

                HavokPackfile.PhysicsSystem system = FindPhysicsSystem(RequirePhysics(level), token);
                Commit(target, "AI: Set physics system of " + target.Label, list => BindPhysics(list, system));
                JObject result = new JObject()
                {
                    ["entity"] = EntityBrief(target),
                    ["physics_system"] = DescribeReference(level, target.Find(ResourceType.DYNAMIC_PHYSICS_SYSTEM), null)["physics_system"],
                };
                Reach(call, result, level, target.Composite, null);
                return result;
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
        private static HavokPackfile RequireCollision(Level level)
        {
            HavokPackfile hkx = level.Collision;
            if (hkx == null || !hkx.Loaded)
                throw new McpError("This level has no COLLISION.HKX loaded.");
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

        private static JObject DescribeProxy(Level level, Commands commands, HavokPackfile hkx, HavokPackfile.StaticCompoundShape compound, Dictionary<HavokPackfile.StaticCompoundShape, List<(Composite, FunctionEntity, CollisionMaps.COLLISION_MAPPING)>> users, int userLimit)
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
                item["domain"] = McpAssets.BoxJson(new Vector3(compound.DomainMin.X, compound.DomainMin.Y, compound.DomainMin.Z), new Vector3(compound.DomainMax.X, compound.DomainMax.Y, compound.DomainMax.Z));
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
                        ["material"] = McpAssets.MaterialRef(level, o.Item3.Material),
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
                stats["bounds"] = McpAssets.BoxJson(min, max);
            }
            return stats;
        }

        private static object ListCollisionProxies(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel(forEditing: false).Level;
                HavokPackfile hkx = RequireCollision(level);
                if (call.Has("proxy"))
                {
                    int index = call.Int("proxy");
                    HavokPackfile.StaticCompoundShape compound = hkx.GetCompound(index);
                    if (compound == null)
                        throw new McpError("There is no collision proxy " + index + " (the level has " + hkx.StaticCompoundShapes.Count + ").");
                    JObject one = DescribeProxy(level, level.Commands, hkx, compound, CollisionUsers(level.Commands), 50);
                    JObject stats = MeshStats(hkx.BuildPreviewMesh(compound));
                    foreach (JProperty property in stats.Properties().ToList())
                        one[property.Name] = property.Value;
                    if (HostRole(hkx, compound) != null)
                        call.Note("A world host's preview is capped at 2048 shapes / 250,000 triangles.");
                    return one;
                }

                string filter = (call.Str("filter") ?? "").Trim();
                bool unused = call.Bool("unused");
                var users = call.Bool("include_users") || unused ? CollisionUsers(level.Commands) : null;
                List<HavokPackfile.StaticCompoundShape> matching = hkx.StaticCompoundShapes.OrderBy(o => o.ProxyIndex).Where(o =>
                {
                    if (unused && (users.ContainsKey(o) || HostRole(hkx, o) != null)) return false;
                    if (filter.Length == 0) return true;
                    string haystack = o.ProxyIndex + " " + (o.Instances?.Count ?? 0) + " 0x" + o.DataOffset.ToString("X") + " " + (HostRole(hkx, o) ?? "");
                    return haystack.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
                }).ToList();
                JObject result = new JObject()
                {
                    ["proxies_in_level"] = hkx.StaticCompoundShapes.Count,
                    ["files"] = hkx.IsTagfile ? "Havok 2018 tagfile (mobile/Switch; imports write " + CollisionProxyImporter.FilesWritten(level) + ")" : "PC packfile",
                };
                var shown = call.Bool("include_users") ? users : null;
                McpPaging.Page(call, matching, result, "proxies", o => DescribeProxy(level, level.Commands, hkx, o, shown, 10), 100);
                if (unused && matching.Count != 0)
                    call.Note("No entity uses these; they stay in the level's collision files (a proxy cannot be deleted). set_collision can assign one.");
                return result;
            });
        }

        private static object ImportCollisionProxy(McpCall call)
        {
            string path = call.Str("path");
            string modelName = call.Str("model");
            if ((path == null) == (modelName == null))
                throw new McpError("Give one of 'path' (a mesh file) or 'model' (a model the level holds, from list_models).");
            if ((call.Has("part") || call.Has("component")) && modelName == null)
                throw McpError.Invalid("'part' goes with 'model'.");
            float scale = ReadScale(call);
            string type = (call.Str("collision_type") ?? "world").Trim().ToLowerInvariant();
            if (type != "world" && type != "ballistic")
                throw new McpError("'collision_type' is 'world' or 'ballistic'.");
            bool dryRun = call.Bool("dry_run");

            CollisionProxyImporter.MeshSource source = path == null ? null : ReadMeshFile(call, path, scale);
            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel().Level;
                Materials.Material material = call.Has("material") ? McpAssets.FindMaterial(level, call.Str("material")) : null;
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
                    result["bounds"] = McpAssets.BoxJson(min, max);
                }
                //A dry run is answered whatever the level can take, saying which files the import would write
                string files = CollisionProxyImporter.FilesWritten(level);
                if (dryRun)
                {
                    result["dry_run"] = true;
                    if (files != null) result["would_write"] = files;
                    else result["would_fail"] = new JArray("This level has no COLLISION.HKX loaded.");
                    return result;
                }
                RequireCollision(level);

                HavokPackfile.StaticCompoundShape created;
                bool reused;
                try
                {
                    using (McpEditorTools.Heartbeat(call, "Writing the collision proxy"))
                        created = ImportProxyOnce(level, source, userData, filterInfo, out reused);
                }
                catch (Exception e) when (!(e is McpError)) { throw new McpError(McpErrorCodes.Failed, "The proxy could not be created: " + e.Message); }
                result["proxy"] = created.ProxyIndex;
                if (reused)
                {
                    result["reused"] = true;
                    call.Note("The same mesh was imported earlier this session as proxy " + created.ProxyIndex + ", so that one is returned rather than another copy.");
                }
                else
                    call.Note("Proxy " + created.ProxyIndex + " is in " + files + " in memory and is written by save_level. It cannot be undone or deleted; set_collision assigns it to an entity.");
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

        /// <summary>A mesh file's triangles, read off the UI thread (Assimp only).</summary>
        private static CollisionProxyImporter.MeshSource ReadMeshFile(McpCall call, string path, float scale)
        {
            path = McpAssets.AbsolutePath(path, "path");
            if (!File.Exists(path))
                throw new McpError(McpErrorCodes.NotFound, "There is no file at " + path + ".");
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
            Models.CS2 model = McpAssets.FindModel(level, name);
            CollisionProxyImporter.MeshSource source;
            string partArgument = call.Has("component") ? "component" : "part";
            try
            {
                if (call.Has(partArgument))
                {
                    int part = call.Int(partArgument);
                    if (part < 0 || part >= model.Components.Count)
                        throw McpError.Invalid(model.Name + " has " + model.Components.Count + " part(s): '" + partArgument + "' takes 0 to " + (model.Components.Count - 1) + ".");
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
            string path = McpAssets.AbsolutePath(call.Str("path", required: true), "path");
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
                    HavokPackfile hkx = RequireCollision(open);
                    int index = call.Int("proxy");
                    HavokPackfile.StaticCompoundShape compound = hkx.GetCompound(index) ?? throw new McpError("There is no collision proxy " + index + ".");
                    mesh = hkx.BuildPreviewMesh(compound);
                    what = "collision proxy " + index;
                }
                else
                {
                    HavokPackfile hkx = RequirePhysics(open);
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
        private static HavokPackfile RequirePhysics(Level level)
        {
            HavokPackfile hkx = level.Physics;
            if (hkx == null || !hkx.Loaded)
                throw new McpError("This level has no PHYSICS.HKX loaded.");
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
                HavokPackfile hkx = RequirePhysics(level);
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
                bool withBodies = call.Bool("bodies");
                List<HavokPackfile.PhysicsSystem> matching = hkx.PhysicsSystems.Where(o => filter.Length == 0 || (o.SystemIndex + " " + (o.Name ?? "")).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(o => o.SystemIndex).ToList();
                JObject result = new JObject()
                {
                    ["systems_in_level"] = hkx.PhysicsSystems.Count,
                    ["files"] = hkx.IsTagfile ? "Havok 2018 tagfile (mobile/Switch; imports write " + PhysicsSystemImporter.FilesWritten(level) + ")" : "PC packfile",
                };
                McpPaging.Page(call, matching, result, "systems", o =>
                {
                    JObject item = new JObject() { ["system"] = o.SystemIndex, ["name"] = o.Name };
                    if (withBodies)
                        item["bodies"] = new JArray(hkx.GetRigidBodies(o).Take(16).Select(DescribeBody));
                    return item;
                }, 100);
                return result;
            });
        }

        private static object ImportPhysicsSystem(McpCall call)
        {
            string path = call.Str("path");
            string modelName = call.Str("model");
            if (path != null && modelName != null)
                throw new McpError("Give 'path' or 'model', not both.");
            if ((call.Has("part") || call.Has("component")) && modelName == null)
                throw McpError.Invalid("'part' goes with 'model'.");
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
                //A dry run is answered whatever the level can take, saying which files the import would write
                string files = PhysicsSystemImporter.FilesWritten(level);
                if (dryRun)
                {
                    result["dry_run"] = true;
                    if (bindTarget != null) result["would_bind"] = EntityBrief(bindTarget);
                    if (files != null) result["would_write"] = files;
                    else result["would_fail"] = new JArray("This level has no PHYSICS.HKX loaded.");
                    return result;
                }
                RequirePhysics(level);

                HavokPackfile.PhysicsSystem created;
                try
                {
                    using (McpEditorTools.Heartbeat(call, "Writing the physics system"))
                        created = PhysicsSystemImporter.Import(level, systemName, shape, body);
                }
                catch (Exception e) when (!(e is McpError)) { throw new McpError("The physics system could not be created: " + e.Message); }
                Singleton.OnResourceModified?.Invoke();
                result["system"] = created.SystemIndex;
                call.Note("System " + created.SystemIndex + " is in " + files + " in memory and is written by save_level. It cannot be undone or deleted.");

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

        /// <summary>A Character and its placements in the level (in the Character Editor's order), maybe narrowed to one or some.</summary>
        private sealed class CharacterTarget
        {
            public Composite Composite;
            public FunctionEntity Character;
            /// <summary>Every placement of the Character from the root.</summary>
            public List<EntityPath> All;
            /// <summary>The placements the call is about (indexes into All).</summary>
            public List<int> Wanted;
            /// <summary>The one a path picked out.</summary>
            public int? Chosen;
        }

        private static bool IsCharacter(Entity entity) => entity is FunctionEntity function && function.function.IsFunctionType && function.function.AsFunctionType == FunctionType.Character;

        /// <summary>Paths from a composite down to the Characters in it, through instances (an NPC archetype holds one).</summary>
        private static List<List<Entity>> CharactersIn(Commands commands, Composite composite, int depth = 0)
        {
            List<List<Entity>> found = new List<List<Entity>>();
            if (composite == null || depth > 6) return found;
            foreach (FunctionEntity function in composite.functions)
            {
                if (IsCharacter(function)) { found.Add(new List<Entity>() { function }); continue; }
                Composite child = McpScript.InstancedComposite(commands, function);
                if (child == null) continue;
                foreach (List<Entity> below in CharactersIn(commands, child, depth + 1))
                {
                    below.Insert(0, function);
                    found.Add(below);
                }
            }
            return found;
        }

        private static bool SameIds(ShortGuid[] path, IList<ShortGuid> ids)
        {
            int length = path.Length > 0 && path[path.Length - 1] == ShortGuid.Invalid ? path.Length - 1 : path.Length;
            if (length != ids.Count) return false;
            for (int i = 0; i < length; i++)
                if (path[i] != ids[i]) return false;
            return true;
        }

        /// <summary>
        /// The Character a call names: 'path' (from the root to the Character, or to an NPC instance holding one), or
        /// 'composite' + 'entity' (a Character, or an instance of an NPC archetype: that instance's placements). UI thread.
        /// </summary>
        private static CharacterTarget ResolveCharacter(Level level, McpCall call)
        {
            Commands commands = level.Commands;
            Composite root = commands.EntryPoints[0];
            if (call.Has("path"))
            {
                if (call.Has("composite") || call.Has("entity"))
                    throw McpError.Invalid("Give 'path' (one placement from the root) or 'composite' + 'entity', not both.");
                List<Entity> chain = McpRegion.ChainFromNames(commands, ReadSteps(call.Token("path"), "path"));
                if (!IsCharacter(chain[chain.Count - 1]))
                {
                    List<List<Entity>> inside = CharactersIn(commands, McpScript.InstancedComposite(commands, chain[chain.Count - 1]));
                    if (inside.Count == 0)
                        throw new McpError(McpErrorCodes.NotFound, McpRegion.DescribeChain(commands, chain) + " is not a Character and holds none (find_entities type 'Character' finds them).");
                    if (inside.Count > 1)
                        throw McpError.Ambiguous("Characters", McpRegion.DescribeChain(commands, chain), inside.Select(o => new JObject() { ["name"] = McpScript.EntityName(commands, McpValueSource.CompositeOf(commands, root, chain.Concat(o).ToList()), o[o.Count - 1]), ["path"] = McpRegion.DescribeChain(commands, chain.Concat(o).ToList()) }), "Give 'path' down to the one you mean (an NPC's instance is enough).");
                    chain.AddRange(inside[0]);
                }
                Composite holder = McpValueSource.CompositeOf(commands, root, chain);
                FunctionEntity character = (FunctionEntity)chain[chain.Count - 1];
                List<EntityPath> all = Placements(holder, character);
                ShortGuid[] ids = chain.Select(o => o.shortGUID).ToArray();
                int index = all.FindIndex(o => SameIds(o.path, ids));
                if (index < 0)
                {
                    all.Add(new EntityPath(ids));
                    index = all.Count - 1;
                }
                return new CharacterTarget() { Composite = holder, Character = character, All = all, Wanted = new List<int>() { index }, Chosen = index };
            }

            if (!call.Has("composite") || !call.Has("entity"))
                throw McpError.Invalid("Give 'composite' + 'entity' (a Character, or an NPC archetype instance), or 'path' from the root (find_entities and get_placements give one).");
            Composite composite = McpScript.FindComposite(commands, call.Str("composite"));
            Entity entity = McpScript.FindEntity(commands, composite, call.Str("entity"));
            if (IsCharacter(entity))
            {
                List<EntityPath> all = Placements(composite, entity);
                return new CharacterTarget() { Composite = composite, Character = (FunctionEntity)entity, All = all, Wanted = Enumerable.Range(0, all.Count).ToList() };
            }
            List<List<Entity>> held = CharactersIn(commands, McpScript.InstancedComposite(commands, entity));
            if (held.Count == 0)
                throw McpError.Invalid(McpScript.EntityName(commands, composite, entity) + " is a " + McpScript.TypeName(commands, composite, entity) + ", not a Character, and holds none (find_entities with type 'Character' finds them).");
            if (held.Count > 1)
                throw McpError.Ambiguous("Characters inside " + McpScript.EntityName(commands, composite, entity), McpScript.EntityName(commands, composite, entity), held.Select(o => new JObject() { ["path"] = string.Join(" > ", o.Select(e => McpScript.Id(e.shortGUID))) }), "Give 'path' down to the one you mean.");
            //The archetype's Character, at the placements that go through this instance
            List<Entity> inner = held[0];
            Composite characterComposite = McpValueSource.CompositeOf(commands, McpScript.InstancedComposite(commands, entity), inner);
            List<EntityPath> placements = Placements(characterComposite, inner[inner.Count - 1]);
            ShortGuid[] tail = new[] { entity.shortGUID }.Concat(inner.Select(o => o.shortGUID)).ToArray();
            List<int> through = new List<int>();
            for (int i = 0; i < placements.Count; i++)
            {
                ShortGuid[] path = placements[i].path;
                int length = path.Length > 0 && path[path.Length - 1] == ShortGuid.Invalid ? path.Length - 1 : path.Length;
                if (length < tail.Length) continue;
                bool ends = true;
                for (int j = 0; j < tail.Length && ends; j++)
                    ends = path[length - tail.Length + j] == tail[j];
                if (ends) through.Add(i);
            }
            return new CharacterTarget() { Composite = characterComposite, Character = (FunctionEntity)inner[inner.Count - 1], All = placements, Wanted = through };
        }

        /// <summary>Where the entity is placed in the level, from the root, as the Character Editor lists them.</summary>
        private static List<EntityPath> Placements(Composite composite, Entity entity)
        {
            LevelContent content = McpEditor.RequireLevel(forEditing: false);
            return content.EditorUtils.GetHierarchiesForEntity(composite, entity);
        }

        private static List<Entity> ChainOf(Commands commands, EntityPath path) =>
            McpRegion.ChainFromIds(commands, commands.EntryPoints[0], path.path.Where(o => o != ShortGuid.Invalid).Select(o => o.AsUInt32));

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
            Dictionary<ShortGuid, FunctionEntity> characters = new Dictionary<ShortGuid, FunctionEntity>();
            foreach (FunctionEntity other in composite.functions.Where(o => !ReferenceEquals(o, entity) && o.function == FunctionType.Character))
                characters[other.shortGUID] = other;
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
                ["accessory_indexes"] = new JObject(PartNames.Select((o, i) => new JProperty(o, Part(attributes, i).AccessoryIndex))),
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
                Commands commands = level.Commands;
                CharacterTarget target = ResolveCharacter(level, call);
                Vector3? near = call.Has("near") ? McpValues.ReadVector(call.Token("near"), "near", null) : (Vector3?)null;
                double radius = call.Num("radius", 10);
                if (call.Has("radius") && near == null) throw McpError.Invalid("'radius' goes with 'near'.");
                McpPlacements walker = McpRegion.Walker(commands);
                Composite root = commands.EntryPoints[0];

                List<JObject> rows = new List<JObject>();
                foreach (int i in target.Wanted)
                {
                    ShortGuid instance = target.All[i].GenerateCompositeInstanceID();
                    List<Entity> chain = ChainOf(commands, target.All[i]);
                    cTransform world = chain == null || chain.Count == 0 ? null : walker.Evaluate(root, chain).World;
                    if (near != null && (world == null || Vector3.Distance(world.position, near.Value) > radius)) continue;
                    JObject item = new JObject()
                    {
                        ["placement"] = i,
                        ["instance_id"] = McpScript.Id(instance),
                        ["path"] = commands.Utils.GetResolvedAsString(commands.Utils.ResolveHierarchy(target.All[i]), false),
                    };
                    if (chain != null) item["ids"] = new JArray(chain.Select(o => McpScript.Id(o.shortGUID)));
                    if (world != null) item["world_position"] = McpCollision.V(world.position);
                    if (chain != null && chain.Count > 1) item["placed_by"] = McpRegion.NameOf(commands, McpValueSource.CompositeOf(commands, root, chain.Take(chain.Count - 1).ToList()), chain[chain.Count - 2]) + " (" + target.Composite.name + ")";
                    CharacterAccessorySets.CharacterAttributes set = FindSet(level, target.Composite, target.Character, instance, out bool loose, out FunctionEntity owner);
                    item["appearance"] = set == null ? JValue.CreateNull() : (JToken)DescribeAppearance(commands, set);
                    if (loose)
                        item["note"] = "This set is stored for entity " + McpScript.Id(set.character.entity_id) + " in the same composite instance; the Character Editor shows it for this Character too.";
                    else if (owner != null)
                        item["note"] = "No set of its own: the one in this composite instance is Character " + McpScript.EntityName(commands, target.Composite, owner) + "'s (the Character Editor, which looks by instance, shows that one for this Character too).";
                    rows.Add(item);
                }
                JObject result = new JObject()
                {
                    ["entity"] = McpScript.Brief(commands, target.Composite, target.Character),
                    ["composite"] = target.Composite.name,
                    ["placements_in_level"] = target.All.Count,
                    ["space"] = "world",
                };
                McpPaging.Page(call, rows, result, "appearances", o => o, 50);
                if (target.All.Count == 0)
                    call.Note(target.Composite.name + " is not placed anywhere in the level, so this Character has no placements to give an appearance to.");
                else if (rows.Count == 0 && near != null)
                    call.Note("None of its " + target.Wanted.Count + " placements is within " + radius + " m of " + McpRegion.Format(near.Value) + ".");
                if (call.Bool("options"))
                    result["options"] = Options(level);
                return result;
            });
        }

        /// <summary>What the appearance attributes take, and the part composites the level's own sets use, by slot.</summary>
        private static JObject Options(Level level)
        {
            Commands commands = level.Commands;
            JObject skeletons = new JObject();
            foreach (KeyValuePair<string, HashSet<string>> gender in Singleton.GenderedSkeletons ?? new Dictionary<string, HashSet<string>>())
                skeletons[gender.Key] = new JArray(gender.Value.OrderBy(o => o));
            List<CharacterAccessorySets.CharacterAttributes> sets = level.AccessorySets?.Entries ?? new List<CharacterAccessorySets.CharacterAttributes>();
            JObject parts = new JObject();
            for (int slot = 0; slot < 6; slot++)
            {
                int s = slot;
                parts[PartNames[slot]] = new JArray(sets.Where(o => o != null).GroupBy(o => Part(o, s).Composite).Where(o => o.Key != ShortGuid.Invalid)
                    .OrderByDescending(o => o.Count()).Take(40).Select(o => (JToken)(commands.GetComposite(o.Key)?.name ?? McpScript.Id(o.Key))));
            }
            return new JObject()
            {
                ["parts_used_in_this_level"] = parts,
                ["face_skeleton_by_head"] = new JObject(HeadFaces(level).OrderBy(o => o.Key).Take(80).Select(o => new JProperty(commands.GetComposite(o.Key)?.name ?? McpScript.Id(o.Key), o.Value))),
                ["skeletons"] = skeletons,
                ["asset_type"] = new JArray(Enum.GetNames(typeof(CUSTOM_CHARACTER_ASSETS))),
                ["voice_actor"] = new JArray(Enum.GetNames(typeof(DIALOGUE_VOICE_ACTOR))),
                ["gender"] = new JArray(Enum.GetNames(typeof(CUSTOM_CHARACTER_GENDER))),
                ["ethnicity"] = new JArray(Enum.GetNames(typeof(CUSTOM_CHARACTER_ETHNICITY))),
                ["build"] = new JArray(Enum.GetNames(typeof(CUSTOM_CHARACTER_BUILD))),
                ["foley"] = new JArray(Enum.GetNames(typeof(CHARACTER_FOLEY_SOUND))),
            };
        }

        /// <summary>The face skeleton each head composite is paired with in the level's own sets (one each in retail).</summary>
        private static Dictionary<ShortGuid, string> HeadFaces(Level level)
        {
            return (level.AccessorySets?.Entries ?? new List<CharacterAccessorySets.CharacterAttributes>())
                .Where(o => o != null && o.components.Head.Composite != ShortGuid.Invalid && !string.IsNullOrEmpty(o.face_skeleton))
                .GroupBy(o => o.components.Head.Composite)
                .ToDictionary(o => o.Key, o => o.GroupBy(s => s.face_skeleton).OrderByDescending(s => s.Count()).First().Key);
        }

        /// <summary>Which placement of a Character: an index or instance id from get_character_appearance.</summary>
        private static int PlacementIndex(Commands commands, CharacterTarget target, JToken token, string argument)
        {
            string text = token.Type == JTokenType.String ? ((string)token).Trim() : token.ToString();
            ShortGuid? id = McpScript.ParseId(text);
            int chosen = -1;
            if (id != null)
                chosen = target.All.FindIndex(o => o.GenerateCompositeInstanceID() == id.Value);
            else if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out chosen))
                chosen = -1;
            if (chosen < 0 || chosen >= target.All.Count)
                throw McpError.Invalid("'" + argument + "' takes a placement index from 0 to " + (target.All.Count - 1) + " or an instance id, from get_character_appearance.");
            return chosen;
        }

        private static readonly int[] VisibleParts = { 0, 1, 2, 3, 4 };

        private static object SetCharacterAppearance(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel().Level;
                McpEditor.RequireUndoIdle();
                Commands commands = level.Commands;
                if (level.AccessorySets == null)
                    throw new McpError(McpErrorCodes.NotFound, "This level has no character accessory sets file loaded.");
                CharacterTarget target = ResolveCharacter(level, call);
                Composite composite = target.Composite;
                FunctionEntity entity = target.Character;
                if (target.All.Count == 0)
                    throw new McpError(McpErrorCodes.NotFound, composite.name + " is not placed anywhere in the level, so the Character has no placement to give an appearance to.");

                //Which placement
                int chosen;
                if (call.Has("placement"))
                    chosen = PlacementIndex(commands, target, call.Token("placement"), "placement");
                else if (target.Chosen != null)
                    chosen = target.Chosen.Value;
                else if (target.Wanted.Count == 1)
                    chosen = target.Wanted[0];
                else
                    throw McpError.Invalid(McpScript.EntityName(commands, composite, entity) + " is placed " + target.Wanted.Count + " times: give 'placement' or 'path' (get_character_appearance lists them, with world positions).");
                ShortGuid instance = target.All[chosen].GenerateCompositeInstanceID();
                CharacterAccessorySets.CharacterAttributes existing = FindSet(level, composite, entity, instance, out bool _, out FunctionEntity owner);
                string name = UndoLabels.Entity(composite, entity);
                string ownerName = owner == null ? null : McpScript.EntityName(commands, composite, owner);

                if (call.Bool("remove"))
                {
                    if (call.Has("components") || call.Has("attributes") || call.Has("from_placement") || call.Has("accessory_indexes"))
                        throw McpError.Invalid("'remove' cannot be combined with components, attributes, accessory_indexes or from_placement.");
                    if (existing == null)
                        throw new McpError(McpErrorCodes.NotFound, "Placement " + chosen + " has no appearance to remove" + (owner != null ? ": the set in this composite instance is Character " + ownerName + "'s (remove it there)." : "."));
                    UndoStack.Current.Apply(new McpCharacterAppearanceEdit(composite, entity, existing, IndexOfReference(level.AccessorySets.Entries, existing), Appearance.Of(existing), null, "AI: Remove appearance of " + name));
                    return new JObject() { ["entity"] = McpScript.Brief(commands, composite, entity), ["placement"] = chosen, ["removed"] = true };
                }

                bool creating = existing == null;
                CharacterAccessorySets.CharacterAttributes set = existing ?? new CharacterAccessorySets.CharacterAttributes()
                {
                    character = new EntityHandle() { entity_id = entity.shortGUID, composite_instance_id = instance },
                };
                Appearance before = creating ? null : Appearance.Of(set);
                Appearance after = Appearance.Of(set);
                JObject components = call.Object("components");
                JObject attributes = call.Object("attributes");
                JObject indexes = call.Object("accessory_indexes");
                List<string> notes = new List<string>();

                //A template: another placement's whole set (an NPC that looks right), then any changes on top
                if (call.Has("from_placement"))
                {
                    CharacterAccessorySets.CharacterAttributes template = TemplateSet(level, target, call.Token("from_placement"));
                    Appearance copied = Appearance.Of(template);
                    copied.EntityId = after.EntityId;
                    copied.InstanceId = after.InstanceId;
                    after = copied;
                }
                else if (creating)
                {
                    //CathodeLib starts a new set's parts at accessory index -1, which no retail set has: each slot's own index, as retail's are
                    for (int i = 0; i < 6; i++) after.Accessories[i] = i;
                }
                if (!creating && !call.Has("from_placement") && (components == null || components.Count == 0) && (attributes == null || attributes.Count == 0) && (indexes == null || indexes.Count == 0))
                    throw McpError.Invalid("Placement " + chosen + " already has an appearance: give 'components', 'attributes', 'accessory_indexes' or 'from_placement' to change it, or remove: true.");

                bool headChanged = false;
                if (components != null)
                {
                    foreach (JProperty property in components.Properties())
                    {
                        int part = Array.FindIndex(PartNames, o => string.Equals(o, property.Name.Trim(), StringComparison.OrdinalIgnoreCase));
                        if (part < 0)
                            throw McpError.Invalid("'" + property.Name + "' is not a component: they are " + string.Join(", ", PartNames) + ".");
                        string value = property.Value.Type == JTokenType.Null ? null : (property.Value.Type == JTokenType.String ? (string)property.Value : property.Value.ToString());
                        ShortGuid composed = IsNone(value) ? ShortGuid.Invalid : McpScript.FindComposite(commands, value).shortGUID;
                        if (part == 3 && composed != after.Composites[3]) headChanged = true;
                        after.Composites[part] = composed;
                        if (after.Accessories[part] < 0) after.Accessories[part] = part;
                    }
                }
                //A new head starts at the head slot's own index (most retail heads), unless told otherwise
                if (headChanged && (indexes == null || indexes.Properties().All(o => !string.Equals(o.Name.Trim(), "head", StringComparison.OrdinalIgnoreCase))))
                    after.Accessories[3] = 3;
                if (indexes != null)
                    foreach (JProperty property in indexes.Properties())
                    {
                        int part = Array.FindIndex(PartNames, o => string.Equals(o, property.Name.Trim(), StringComparison.OrdinalIgnoreCase));
                        if (part < 0)
                            throw McpError.Invalid("accessory_indexes['" + property.Name + "'] is not a component: they are " + string.Join(", ", PartNames) + ".");
                        int index = McpValues.ReadInt(property.Value, "accessory_indexes['" + property.Name + "']");
                        if (index < 0) throw McpError.Invalid("accessory_indexes['" + property.Name + "'] is 0 or more.");
                        after.Accessories[part] = index;
                    }
                if (attributes != null)
                    ApplyAttributes(after, attributes, call);

                //The face rig follows the head, as every retail set pairs them, unless face_skeleton was given
                bool faceGiven = attributes != null && attributes.Properties().Any(o => string.Equals(o.Name.Trim(), "face_skeleton", StringComparison.OrdinalIgnoreCase));
                if (headChanged && !faceGiven && after.Composites[3] != ShortGuid.Invalid)
                {
                    string face = FaceFor(level, after.Composites[3], after.GenderSkeleton);
                    if (face != null && !string.Equals(face, after.FaceSkeleton, StringComparison.OrdinalIgnoreCase))
                    {
                        notes.Add("face_skeleton set to " + face + " to match the head (give face_skeleton to choose another).");
                        after.FaceSkeleton = face;
                    }
                    else if (face == null)
                        notes.Add("No face skeleton is known for that head (no set in this level uses it, and its name names none): face_skeleton was left as " + after.FaceSkeleton + ".");
                }

                //A set missing visible parts shows a character with holes
                List<string> missing = VisibleParts.Where(o => after.Composites[o] == ShortGuid.Invalid).Select(o => PartNames[o]).ToList();
                if (missing.Count != 0 && !call.Bool("allow_partial"))
                    throw McpError.Invalid("The appearance would have no " + string.Join(", ", missing) + ": every retail set has all five visible parts (torso, legs, shoes, head, arms). Give them in 'components', start from a set that looks right with 'from_placement', or pass allow_partial: true. get_character_appearance options:true lists the parts this level's sets use.");
                foreach (int i in Enumerable.Range(0, 6))
                    if (after.Composites[i] != ShortGuid.Invalid && commands.GetComposite(after.Composites[i]) == null)
                        notes.Add("The " + PartNames[i] + " composite (" + McpScript.Id(after.Composites[i]) + ") is not in this level: port it (port_composites) or pick another.");
                notes.AddRange(GenderMismatches(commands, after));

                string label = (creating ? "AI: Add appearance to " : "AI: Change appearance of ") + name;
                UndoStack.Current.Apply(new McpCharacterAppearanceEdit(composite, entity, set, creating ? level.AccessorySets.Entries.Count : IndexOfReference(level.AccessorySets.Entries, set), before, after, label));
                if (creating && owner != null)
                    notes.Add("The set already in this composite instance is Character " + ownerName + "'s and is unchanged; this Character now has its own. The Character Editor, which looks by instance alone, still shows " + ownerName + "'s.");
                foreach (string note in notes) call.Note(note);
                return new JObject()
                {
                    ["entity"] = McpScript.Brief(commands, composite, entity),
                    ["placement"] = chosen,
                    ["instance_id"] = McpScript.Id(instance),
                    ["created"] = creating,
                    ["appearance"] = DescribeAppearance(commands, set),
                    ["undo"] = "One undo step: '" + label + "'.",
                };
            });
        }

        /// <summary>The set of another placement to copy: an index or instance id of this Character's placements, or a path from the root to any Character.</summary>
        private static CharacterAccessorySets.CharacterAttributes TemplateSet(Level level, CharacterTarget target, JToken token)
        {
            Commands commands = level.Commands;
            if (token is JArray || (token.Type == JTokenType.String && ((string)token).IndexOfAny(new[] { '/', '>' }) >= 0))
            {
                List<Entity> chain = McpRegion.ChainFromNames(commands, ReadSteps(token, "from_placement"));
                if (!IsCharacter(chain[chain.Count - 1]))
                {
                    List<List<Entity>> inside = CharactersIn(commands, McpScript.InstancedComposite(commands, chain[chain.Count - 1]));
                    if (inside.Count != 1) throw McpError.Invalid("'from_placement' has to lead to one Character.");
                    chain.AddRange(inside[0]);
                }
                Composite holder = McpValueSource.CompositeOf(commands, commands.EntryPoints[0], chain);
                ShortGuid instance = new EntityPath(chain.Select(o => o.shortGUID).ToArray()).GenerateCompositeInstanceID();
                return FindSet(level, holder, chain[chain.Count - 1], instance, out bool _, out FunctionEntity _)
                    ?? throw new McpError(McpErrorCodes.NotFound, McpRegion.DescribeChain(commands, chain) + " has no appearance to copy.");
            }
            int index = PlacementIndex(commands, target, token, "from_placement");
            ShortGuid id = target.All[index].GenerateCompositeInstanceID();
            return FindSet(level, target.Composite, target.Character, id, out bool _, out FunctionEntity _)
                ?? throw new McpError(McpErrorCodes.NotFound, "Placement " + index + " has no appearance to copy (get_character_appearance shows which do).");
        }

        /// <summary>The face skeleton to go with a head: the one the level's own sets pair it with, else the HEAD_&lt;NAME&gt; name when the gender skeleton offers it.</summary>
        private static string FaceFor(Level level, ShortGuid head, string genderSkeleton)
        {
            if (HeadFaces(level).TryGetValue(head, out string paired)) return paired;
            Composite headComposite = level.Commands.GetComposite(head);
            if (headComposite == null) return null;
            string leaf = McpScript.CompositeLeaf(headComposite) ?? "";
            int at = leaf.IndexOf("HEAD_", StringComparison.OrdinalIgnoreCase);
            if (at < 0) return null;
            string name = leaf.Substring(at + 5).Split('_')[0];
            Dictionary<string, HashSet<string>> skeletons = Singleton.GenderedSkeletons;
            HashSet<string> faces = genderSkeleton != null && skeletons != null && skeletons.TryGetValue(genderSkeleton, out HashSet<string> found) ? found : null;
            return faces?.FirstOrDefault(o => string.Equals(o, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Parts whose folder says the other gender than the set's (NPC\MALE parts on a FEMALE set, say), and a gender skeleton at odds with the gender.</summary>
        private static IEnumerable<string> GenderMismatches(Commands commands, Appearance appearance)
        {
            bool female = appearance.Gender.ToString().IndexOf("FEMALE", StringComparison.OrdinalIgnoreCase) >= 0;
            for (int i = 0; i < 5; i++)
            {
                string path = (commands.GetComposite(appearance.Composites[i])?.name ?? "").ToUpperInvariant().Replace('/', '\\');
                if (path.Length == 0) continue;
                bool saysFemale = path.Contains("FEMALE");
                bool saysMale = !saysFemale && (path.Contains("\\MALE") || path.Contains("_MALE"));
                if (female && saysMale) yield return "The " + PartNames[i] + " (" + path + ") looks like a male part on a female set.";
                if (!female && saysFemale) yield return "The " + PartNames[i] + " (" + path + ") looks like a female part on a male set.";
            }
            if (appearance.GenderSkeleton != null && female != (appearance.GenderSkeleton.IndexOf("FEMALE", StringComparison.OrdinalIgnoreCase) >= 0))
                yield return "gender is " + appearance.Gender + " but gender_skeleton is " + appearance.GenderSkeleton + ".";
        }

        private static void ApplyAttributes(Appearance after, JObject attributes, McpCall call)
        {
            string genderSkeleton = null, faceSkeleton = null;
            foreach (JProperty property in attributes.Properties())
            {
                string key = property.Name.Trim().ToLowerInvariant();
                JToken value = property.Value;
                if (value == null || value.Type == JTokenType.Null)
                    throw McpError.Invalid("'" + property.Name + "' needs a value.");
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
                        throw McpError.Invalid("'" + property.Name + "' is not an attribute: they are gender_skeleton, face_skeleton, asset_type, voice_actor, gender, ethnicity, build, foley_torso, foley_leg, foley_footwear.");
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
                    throw McpError.NotFound("gender skeleton", genderSkeleton, skeletons.Keys, "They are " + string.Join(", ", skeletons.Keys) + ".");
                after.GenderSkeleton = key;
            }
            HashSet<string> faces = after.GenderSkeleton != null && skeletons.TryGetValue(after.GenderSkeleton, out HashSet<string> found) ? found : null;
            if (faceSkeleton != null)
            {
                string face = faces?.FirstOrDefault(o => string.Equals(o, faceSkeleton, StringComparison.OrdinalIgnoreCase));
                if (face == null)
                    throw McpError.NotFound("face skeleton for " + after.GenderSkeleton, faceSkeleton, faces ?? new HashSet<string>(), faces != null ? "It takes " + string.Join(", ", faces.OrderBy(o => o)) + "." : null);
                after.FaceSkeleton = face;
            }
            else if (faces != null && !faces.Contains(after.FaceSkeleton ?? ""))
                throw McpError.Invalid("The face skeleton '" + after.FaceSkeleton + "' is not one " + after.GenderSkeleton + " offers: give face_skeleton too (" + string.Join(", ", faces.OrderBy(o => o)) + ").");
        }

        private static object CheckCharacterAppearances(McpCall call)
        {
            bool repair = call.Bool("repair");
            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel(forEditing: repair).Level;
                Commands commands = level.Commands;
                if (repair) McpEditor.RequireUndoIdle();
                List<CharacterAccessorySets.CharacterAttributes> sets = level.AccessorySets?.Entries;
                if (sets == null)
                    throw new McpError(McpErrorCodes.NotFound, "This level has no character accessory sets file loaded.");

                //Every Character placement in the level, by its instance id
                Dictionary<ShortGuid, List<(Composite composite, FunctionEntity character, EntityPath path)>> byInstance = new Dictionary<ShortGuid, List<(Composite, FunctionEntity, EntityPath)>>();
                Dictionary<ShortGuid, List<(Composite composite, FunctionEntity character, EntityPath path)>> byEntity = new Dictionary<ShortGuid, List<(Composite, FunctionEntity, EntityPath)>>();
                int characters = 0;
                foreach (Composite composite in commands.Entries)
                {
                    if (composite == null) continue;
                    foreach (FunctionEntity character in composite.functions.Where(o => o.function == FunctionType.Character))
                    {
                        characters++;
                        foreach (EntityPath path in Placements(composite, character))
                        {
                            ShortGuid instance = path.GenerateCompositeInstanceID();
                            if (!byInstance.TryGetValue(instance, out var list)) byInstance[instance] = list = new List<(Composite, FunctionEntity, EntityPath)>();
                            list.Add((composite, character, path));
                            if (!byEntity.TryGetValue(character.shortGUID, out var own)) byEntity[character.shortGUID] = own = new List<(Composite, FunctionEntity, EntityPath)>();
                            own.Add((composite, character, path));
                        }
                    }
                }

                HashSet<ShortGuid> claimed = new HashSet<ShortGuid>(sets.Where(o => o?.character != null).Select(o => o.character.composite_instance_id));
                JArray orphans = new JArray(), duplicates = new JArray(), unindexed = new JArray(), missingParts = new JArray(), partial = new JArray();
                List<(CharacterAccessorySets.CharacterAttributes set, Composite composite, FunctionEntity character, ShortGuid instance)> remaps = new List<(CharacterAccessorySets.CharacterAttributes, Composite, FunctionEntity, ShortGuid)>();
                List<CharacterAccessorySets.CharacterAttributes> reindex = new List<CharacterAccessorySets.CharacterAttributes>();
                HashSet<(ShortGuid, ShortGuid)> seen = new HashSet<(ShortGuid, ShortGuid)>();
                for (int i = 0; i < sets.Count; i++)
                {
                    CharacterAccessorySets.CharacterAttributes set = sets[i];
                    if (set?.character == null) continue;
                    (ShortGuid, ShortGuid) handle = (set.character.entity_id, set.character.composite_instance_id);
                    if (!seen.Add(handle))
                        duplicates.Add(new JObject() { ["set"] = i, ["entity_id"] = McpScript.Id(handle.Item1), ["instance_id"] = McpScript.Id(handle.Item2) });
                    if (!byInstance.ContainsKey(set.character.composite_instance_id))
                    {
                        //No placement has that instance id any more: a refactor, copy or port moved the Character
                        List<(Composite composite, FunctionEntity character, EntityPath path)> candidates = byEntity.TryGetValue(set.character.entity_id, out var same)
                            ? same.Where(o => !claimed.Contains(o.path.GenerateCompositeInstanceID())).ToList() : new List<(Composite, FunctionEntity, EntityPath)>();
                        JObject orphan = new JObject() { ["set"] = i, ["entity_id"] = McpScript.Id(set.character.entity_id), ["instance_id"] = McpScript.Id(set.character.composite_instance_id), ["head"] = commands.GetComposite(set.components.Head.Composite)?.name };
                        if (candidates.Count == 1)
                        {
                            orphan["remap_to"] = commands.Utils.GetResolvedAsString(commands.Utils.ResolveHierarchy(candidates[0].path), false);
                            remaps.Add((set, candidates[0].composite, candidates[0].character, candidates[0].path.GenerateCompositeInstanceID()));
                        }
                        else if (candidates.Count > 1)
                            orphan["could_be"] = candidates.Count + " unclaimed placements of a Character with that id (set it per placement with set_character_appearance)";
                        orphans.Add(orphan);
                    }
                    for (int p = 0; p < 6; p++)
                    {
                        ShortGuid part = Part(set, p).Composite;
                        if (part != ShortGuid.Invalid && commands.GetComposite(part) == null)
                            missingParts.Add(new JObject() { ["set"] = i, ["part"] = PartNames[p], ["composite_id"] = McpScript.Id(part) });
                    }
                    if (VisibleParts.Any(o => Part(set, o).Composite == ShortGuid.Invalid))
                        partial.Add(new JObject() { ["set"] = i, ["missing"] = new JArray(VisibleParts.Where(o => Part(set, o).Composite == ShortGuid.Invalid).Select(o => PartNames[o])) });
                    if (Enumerable.Range(0, 6).Any(o => Part(set, o).AccessoryIndex < 0 && Part(set, o).Composite != ShortGuid.Invalid))
                    {
                        unindexed.Add(i);
                        if (byInstance.ContainsKey(set.character.composite_instance_id)) reindex.Add(set);
                    }
                }
                int withoutSet = byInstance.Keys.Count(o => !claimed.Contains(o));

                JObject result = new JObject()
                {
                    ["sets"] = sets.Count,
                    ["characters"] = characters,
                    ["character_placements"] = byInstance.Values.Sum(o => o.Count),
                    ["placements_without_a_set"] = withoutSet,
                };
                if (orphans.Count != 0) result["orphaned_sets"] = new JArray(orphans.Take(50));
                if (duplicates.Count != 0) result["duplicate_sets"] = new JArray(duplicates.Take(50));
                if (missingParts.Count != 0) result["parts_not_in_level"] = new JArray(missingParts.Take(50));
                if (partial.Count != 0) result["sets_missing_visible_parts"] = new JArray(partial.Take(50));
                if (unindexed.Count != 0) result["sets_with_unset_accessory_index"] = new JArray(unindexed.Take(50));
                bool clean = orphans.Count == 0 && duplicates.Count == 0 && missingParts.Count == 0 && partial.Count == 0 && unindexed.Count == 0;
                result["ok"] = clean;
                if (withoutSet != 0)
                    call.Note(withoutSet + " Character placements have no set of their own; that is normal for characters the game dresses itself (a set only matters where a custom look is wanted).");

                if (!repair)
                {
                    //A report changes nothing: get_undo_history leaves it out of not_undoable
                    result["changed"] = false;
                    if (remaps.Count != 0 || reindex.Count != 0)
                        call.Note("repair: true would " + (remaps.Count != 0 ? "point " + remaps.Count + " orphaned set(s) at the one placement each now matches" : "") + (remaps.Count != 0 && reindex.Count != 0 ? " and " : "") + (reindex.Count != 0 ? "give " + reindex.Count + " set(s) their slots' accessory indexes" : "") + ", as one undo step.");
                    if (orphans.Count != 0)
                        call.Note("Orphaned sets come from changing where an NPC is placed (copying, porting, or a refactor made before refactors carried looks along): the set is keyed by the placement's path, so the NPC loses its look. Re-apply it with set_character_appearance where repair cannot.");
                    return result;
                }
                if (remaps.Count == 0 && reindex.Count == 0)
                {
                    result["repaired"] = 0;
                    result["changed"] = false;
                    call.Note("Nothing to repair automatically.");
                    return result;
                }
                using (UndoStack.Current.BeginGroup("AI: Repair character appearances"))
                {
                    foreach ((CharacterAccessorySets.CharacterAttributes set, Composite composite, FunctionEntity character, ShortGuid instance) in remaps)
                    {
                        Appearance before = Appearance.Of(set), after = Appearance.Of(set);
                        after.EntityId = character.shortGUID;
                        after.InstanceId = instance;
                        for (int p = 0; p < 6; p++) if (after.Accessories[p] < 0) after.Accessories[p] = p;
                        UndoStack.Current.Apply(new McpCharacterAppearanceEdit(composite, character, set, IndexOfReference(sets, set), before, after, "AI: Repair character appearances"));
                    }
                    foreach (CharacterAccessorySets.CharacterAttributes set in reindex.Where(o => !remaps.Any(r => ReferenceEquals(r.set, o))))
                    {
                        Appearance before = Appearance.Of(set), after = Appearance.Of(set);
                        for (int p = 0; p < 6; p++) if (after.Accessories[p] < 0) after.Accessories[p] = p;
                        var placed = byInstance[set.character.composite_instance_id][0];
                        UndoStack.Current.Apply(new McpCharacterAppearanceEdit(placed.composite, placed.character, set, IndexOfReference(sets, set), before, after, "AI: Repair character appearances"));
                    }
                }
                result["repaired"] = remaps.Count + reindex.Count(o => !remaps.Any(r => ReferenceEquals(r.set, o)));
                result["undo"] = "One undo step: 'AI: Repair character appearances'.";
                return result;
            });
        }

        private static T ParseEnum<T>(string text, string key) where T : struct
        {
            if (Enum.TryParse(text, true, out T parsed) && Enum.IsDefined(typeof(T), parsed))
                return parsed;
            throw McpError.NotFound("value of " + key, text, Enum.GetNames(typeof(T)), "It takes " + string.Join(", ", Enum.GetNames(typeof(T))) + ".");
        }
        #endregion

        #region Lookups
        private static MaterialMappings.MaterialMapping FindMaterialMapping(Level level, string name)
        {
            List<MaterialMappings.MaterialMapping> entries = level.MaterialMappings?.Entries ?? new List<MaterialMappings.MaterialMapping>();
            MaterialMappings.MaterialMapping found = entries.FirstOrDefault(o => o != null && string.Equals(o.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
            if (found != null) return found;
            throw McpError.NotFound("material mapping", name, entries.Where(o => o != null).Select(o => o.Name), entries.Count == 0 ? "The level has none: edit_material_mapping create: true makes one." : "list_material_mappings lists them.");
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

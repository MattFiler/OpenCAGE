using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.MCP;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE
{
    /// <summary>
    /// Level Check: physics props whose bodies won't find their model. A body binds to its display model and collision through
    /// ShortGuid(body name): a spawned copy (an AssetSpawner's asset) whose ModelReference is keyed by anything else logs "Unable to create
    /// physics system" and never appears. Looks at the user's own physics composites. Fix: key the ModelReferences by their bodies' names.
    /// </summary>
    internal sealed class PhysicsBodyKeysRule : LevelCheckRule
    {
        public override string Id => "physics_body_keys";
        public override string Name => "Physics props that won't move";
        public override string About => "A moving physics prop has to be spawned (an AssetSpawner's asset, as every retail dynamic prop is: placed directly it stays asleep where it is put), and a spawned body finds its model and collision through the hash of its name, so its ModelReference must be keyed by that name and carry collision of its own - otherwise \"Unable to create physics system\".";

        public override void Run(LevelCheckContext context, List<LevelCheckFinding> found)
        {
            Level level = context.Level;
            if (level?.Physics == null) return;
            HashSet<Composite> spawned = new HashSet<Composite>(PhysicsBodyKeys.SpawnedAssets(context.Commands).Where(o => context.Placed.Contains(o.spawnerComposite)).Select(o => o.spawned));
            foreach (Composite composite in context.Commands.Entries)
            {
                if (composite == null || !context.IsUsers(composite) || PhysicsBodyKeys.IsRetailException(composite)) continue;
                if (!spawned.Contains(composite) && !context.Placed.Contains(composite)) continue;
                if (!composite.functions.Any(o => LevelCheckContext.Is(o, FunctionType.PhysicsSystem))) continue;
                bool isSpawned = spawned.Contains(composite);
                List<PhysicsBodyKeys.Body> bodies = PhysicsBodyKeys.Inspect(level, composite).Where(o => o.State != PhysicsBodyKeys.State.Keyed || (isSpawned && !o.HasCollision)).ToList();
                if (bodies.Count == 0) continue;
                LevelCheckFinding finding = new LevelCheckFinding()
                {
                    Key = composite.shortGUID.ToByteString(),
                    Severity = isSpawned ? LevelCheckSeverity.Broken : LevelCheckSeverity.Warning,
                    Title = EditorUtils.GetCompositeName(composite) + (isSpawned ? " is spawned, but its physics bodies won't find their model or collision" : ": physics bodies not keyed by name as retail's are"),
                    Text = string.Join("; ", bodies.Select(PhysicsBodyKeys.Describe)) + ". " +
                           (isSpawned ? "An AssetSpawner spawns it: the game binds each body to the ModelReference resource keyed by the hash of the body's name, which also needs collision of its own (retail's: its ballistic shape, ghosted and frozen), and missing either logs \"Unable to create physics system\" - the prop never moves. "
                                      : "Retail keys every physics prop's ModelReference by its body's name; a copy of this one spawned by an AssetSpawner would not appear. ") +
                           "The Fix (or MCP key_physics_bodies / spawn_physics_prop) does it.",
                };
                foreach (PhysicsBodyKeys.Body body in bodies)
                    finding.Sources.Add(context.Source(composite, (Entity)body.Model ?? body.PhysicsEntity, "body '" + body.BodyName + "'"));
                List<PhysicsBodyKeys.Body> fixable = bodies.Where(o => o.State == PhysicsBodyKeys.State.NotKeyed || (o.Model != null && !o.HasCollision && o.State != PhysicsBodyKeys.State.DrawsNothing)).ToList();
                if (fixable.Count != 0)
                {
                    Composite target = composite;
                    bool collision = isSpawned && fixable.Any(o => !o.HasCollision);
                    finding.Fix = new LevelCheckFix()
                    {
                        Label = collision ? "Give " + fixable.Count + " body model" + (fixable.Count == 1 ? "" : "s") + " retail's collision and key by name" : "Key " + fixable.Count + " ModelReference" + (fixable.Count == 1 ? "" : "s") + " by body name",
                        Detail = "Gives " + string.Join(", ", fixable.Select(o => "'" + o.ModelName + "'")) + " the resource id ShortGuid(" + string.Join(", ", fixable.Select(o => o.BodyName)) + "), as retail's are" +
                                 (collision ? ", and collision from its own triangles with retail's dynamic-prop flags (BALLISTIC, FIXED, RESOURCE, GHOSTED, PRE_GHOSTED, FROZEN, PRE_FROZEN)" : "") + ".",
                        Apply = fromTool =>
                        {
                            if (collision)
                            {
                                JArray given = PhysicsPropSpawner.GiveCollisionAndKey(null, target.name, fromTool ? "AI: Level Check: " : "Level Check: ");
                                return "Gave " + given.Count + " body model" + (given.Count == 1 ? "" : "s") + " collision and keyed the bodies by name. Save & Build to take it into the game.";
                            }
                            return LevelCheckFix.Script("Key physics bodies of " + EditorUtils.GetCompositeName(target) + " by name", target, fromTool, edit =>
                            {
                                List<PhysicsBodyKeys.Body> keyed = PhysicsBodyKeys.Key(edit, PhysicsBodyKeys.Inspect(context.Level, target));
                                return "Keyed " + keyed.Count + " ModelReference" + (keyed.Count == 1 ? "" : "s") + " by body name.";
                            });
                        },
                    };
                }
                found.Add(finding);
            }

            //Dynamic props placed directly: asleep where they are put, as no retail dynamic prop is placed
            foreach ((Composite holder, FunctionEntity instance, Composite prop) in PhysicsPropSpawner.PlacedDynamic(level))
            {
                if (!context.Placed.Contains(holder) || (!context.IsUsers(holder) && !context.IsUsers(prop)) || PhysicsBodyKeys.IsRetailException(prop)) continue;
                Composite where = holder;
                string id = McpScript.Id(instance.shortGUID);
                found.Add(new LevelCheckFinding()
                {
                    Key = "placed:" + LevelCheckScript.Key(holder, instance),
                    Severity = LevelCheckSeverity.Warning,
                    Title = context.Where(holder, instance) + " is a moving physics prop placed directly: it will hang asleep where it is put",
                    Text = "A dynamic physics prop placed straight into the level stays where it was put, asleep, however hard it is pushed. Every retail one is spawned at load instead, by an AssetSpawner whose asset is an instance of the prop (is_template). The Fix (or MCP spawn_physics_prop) places it that way: its bodies keyed by name with collision of their own, a '" + prop.name + PhysicsPropSpawner.Suffix + "' composite made once, and this placement replaced by an instance of it with the same name and transform.",
                    Sources = { context.Source(holder, instance) },
                    Fix = new LevelCheckFix()
                    {
                        Label = "Spawn it, as retail's are",
                        Detail = "Wraps " + prop.name + " in an AssetSpawner composite ('" + prop.name + PhysicsPropSpawner.Suffix + "') and places that instead, same name and transform.",
                        Apply = fromTool =>
                        {
                            JObject done = PhysicsPropSpawner.MakeSpawned(null, where.name, id, fromTool ? "AI: Level Check: " : "Level Check: ");
                            return "Placed as " + (string)done["wrapper"] + ". Save & Build to take it into the game.";
                        },
                    },
                });
            }
        }
    }
    /// <summary>
    /// Level Check: a big SimpleWater the size culling will hide far out unless it carries disable_size_culling. Fix: tick it.
    /// </summary>
    internal sealed class WaterCullingRule : LevelCheckRule
    {
        /// <summary>Wider than this (metres, either side) and it is a sea, not a puddle.</summary>
        public const float Wide = 60f;
        public override string Id => "water_culling";
        public override string Name => "SimpleWater: no model, or culled far away";
        public override string About => "A SimpleWater with no model (one made with Add Entity) crashes the game loading the level; and one is culled once it is small on screen, so a sea or lake vanishes far out unless 'disable_size_culling' (Don't cull when far away) is ticked.";

        private static bool Draws(FunctionEntity water) => (water?.GetResource(ResourceType.RENDERABLE_INSTANCE, true)?.RenderableInstance?.Count ?? 0) != 0;

        public override void Run(LevelCheckContext context, List<LevelCheckFinding> found)
        {
            //A SimpleWater that draws nothing crashes the game as the level loads (SIMPLE_WATER::update_collision_hulls on a null
            //renderable: GOG AI+0xdc906); one made with Add Entity or create_entities has no model until it is given one
            List<(Composite composite, FunctionEntity entity)> empty = context.InPlaced(FunctionType.SimpleWater)
                .Where(o => !Draws(o.Entity) && !LevelCheckContext.Bool(o.Entity, "deleted").GetValueOrDefault())
                .Select(o => (o.Composite, o.Entity)).ToList();
            if (empty.Count != 0)
            {
                (Composite composite, FunctionEntity entity) model = context.InPlaced(FunctionType.SimpleWater).Where(o => Draws(o.Entity)).Select(o => (o.Composite, o.Entity)).FirstOrDefault();
                LevelCheckFinding crash = new LevelCheckFinding()
                {
                    Key = "no_model:" + string.Join(",", empty.Select(o => LevelCheckScript.Key(o.composite, o.entity))),
                    Severity = LevelCheckSeverity.Crash,
                    Title = empty.Count + " SimpleWater" + (empty.Count == 1 ? " has" : "s have") + " no model: the game crashes loading the level",
                    Text = "A SimpleWater draws the game's water plane with a CA_SIMPLEWATER material, given to it as its renderable. One without (a SimpleWater made with Add Entity or create_entities) crashes the game as the level loads. " +
                           (model.entity != null ? "Give it the renderable another SimpleWater in the level has (the Fix), or set one with the Resources button." : "This level has no SimpleWater with a model to copy: port a retail water (SCI_HOSPITALLOWER's AYZ\\FX_Library\\Location_Comp\\SCI_HospitalLower\\SCI_HospitalLower_St_3, or ENG_ALIEN_NEST's) and copy its SimpleWater instead, or delete this one."),
                };
                foreach ((Composite composite, FunctionEntity entity) in empty)
                    crash.Sources.Add(context.Source(composite, entity));
                if (model.entity != null)
                {
                    List<(Composite composite, FunctionEntity entity)> toGive = empty;
                    (Composite composite, FunctionEntity entity) from = model;
                    crash.Fix = new LevelCheckFix()
                    {
                        Label = "Give " + toGive.Count + " water" + (toGive.Count == 1 ? "" : "s") + " the model of " + context.EntityName(from.composite, from.entity),
                        Detail = "Copies the renderable (the water plane and its CA_SIMPLEWATER material) of " + context.Where(from.composite, from.entity) + " onto each; their own parameters (size, colours, speed) still decide how it looks.",
                        Apply = fromTool => LevelCheckFix.Script("Give " + toGive.Count + " SimpleWater a model", toGive[0].composite, fromTool, edit =>
                        {
                            ResourceReference source = from.entity.GetResource(ResourceType.RENDERABLE_INSTANCE, true);
                            foreach ((Composite composite, FunctionEntity entity) in toGive)
                            {
                                LevelCheckScript.RequireStill(edit, composite, entity);
                                edit.Touch(composite, entity);
                                cResource resource = entity.GetParameter(ShortGuids.resource)?.content as cResource;
                                cResource next = resource == null ? new cResource(entity.shortGUID) : PhysicsBodyKeys.Rekeyed(resource, resource.shortGUID);
                                next.value = next.value.Where(o => o != null && o.resource_type != ResourceType.RENDERABLE_INSTANCE).ToList();
                                ResourceReference copy = new ResourceReference(ResourceType.RENDERABLE_INSTANCE) { resource_id = next.shortGUID };
                                copy.RenderableInstance = new List<RenderableElements.Element>(source.RenderableInstance);
                                next.value.Add(copy);
                                entity.AddParameter(ShortGuids.resource, next, ParameterVariant.INTERNAL, true, bindResourceToEntity: false);
                                edit.Changed(composite);
                            }
                            return "Gave " + toGive.Count + " SimpleWater the model of " + context.EntityName(from.composite, from.entity) + ".";
                        }),
                    };
                }
                found.Add(crash);
            }

            List<(Composite composite, FunctionEntity entity)> waters = context.InUsers(FunctionType.SimpleWater)
                .Where(o => !BuildParameters.IsOn(o.Entity, ShortGuids.disable_size_culling) && Width(o.Entity) >= Wide)
                .Select(o => (o.Composite, o.Entity)).ToList();
            if (waters.Count == 0) return;
            LevelCheckFinding finding = new LevelCheckFinding()
            {
                Key = string.Join(",", waters.Select(o => LevelCheckScript.Key(o.composite, o.entity))),
                Severity = LevelCheckSeverity.Warning,
                Title = waters.Count + " big SimpleWater" + (waters.Count == 1 ? "" : "s") + " will vanish far away",
                Text = "The game culls what is small on screen; a SimpleWater " + Wide + " m or more across disappears in the distance (a sea can vanish ~220 m out). Tick 'disable_size_culling' (Don't cull when far away) on it - OpenCAGE's Save & Build applies it.",
            };
            foreach ((Composite composite, FunctionEntity entity) in waters)
                finding.Sources.Add(context.Source(composite, entity, Width(entity).ToString("0") + " m across"));
            List<(Composite composite, FunctionEntity entity)> toFix = waters;
            finding.Fix = new LevelCheckFix()
            {
                Label = "Tick disable_size_culling on " + toFix.Count + " water" + (toFix.Count == 1 ? "" : "s"),
                Detail = "Sets the SimpleWater's 'disable_size_culling' (Don't cull when far away) to true; Save & Build then draws it however far away it is.",
                Apply = fromTool => LevelCheckFix.Script("disable_size_culling on " + toFix.Count + " water" + (toFix.Count == 1 ? "" : "s"), toFix[0].composite, fromTool, edit =>
                {
                    foreach ((Composite composite, FunctionEntity entity) in toFix)
                    {
                        LevelCheckScript.RequireStill(edit, composite, entity);
                        edit.SetParameter(composite, entity, "disable_size_culling", new Newtonsoft.Json.Linq.JValue(true), allowCustom: true);
                    }
                    return "Ticked disable_size_culling on " + toFix.Count + " water" + (toFix.Count == 1 ? "" : "s") + ".";
                }),
            };
            found.Add(finding);
        }

        /// <summary>Its width: twice the larger of its half_dimensions' X and Z.</summary>
        public static float Width(Entity entity)
        {
            if (!(entity?.GetParameter("half_dimensions")?.content is cVector3 half)) return 0f;
            return 2f * Math.Max(Math.Abs(half.value.X), Math.Abs(half.value.Z));
        }
    }
}

using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.MCP;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE
{
    /// <summary>
    /// Physics props that also work when an AssetSpawner spawns them. The game binds each body of a spawned physics system to its display
    /// model and collision through ShortGuid(body name): DYNAMIC_PHYSICS_SYSTEM::create_system looks for the ModelReference resource keyed
    /// by that hash ("Havok physics object [X] has no matching display model / collision"). Retail keys nearly every body that way; a
    /// resource OpenCAGE makes is keyed by the entity's id, so keying by name is the default for every physics import.
    /// </summary>
    public static class PhysicsBodyKeys
    {
        public enum State
        {
            /// <summary>A ModelReference's resource is keyed by the body's name and draws something.</summary>
            Keyed,
            /// <summary>The body's ModelReference draws something but its resource is keyed by something else: <see cref="Key"/> fixes it.</summary>
            NotKeyed,
            /// <summary>The ModelReference named after the body draws nothing (no resource, or an empty one).</summary>
            DrawsNothing,
            /// <summary>No ModelReference is named after the body, and none can be picked for it.</summary>
            NoModel,
        }

        public sealed class Body
        {
            public Composite Composite;
            public FunctionEntity PhysicsEntity;
            public int SystemIndex;
            public string SystemName;
            public string BodyName;
            public State State;
            /// <summary>The ModelReference that is, or will be, keyed by the body's name.</summary>
            public FunctionEntity Model;
            public string ModelName;
            public ShortGuid KeyNow;
            /// <summary>Its ModelReference has a collision mapping of its own (retail's: BALLISTIC, FIXED, RESOURCE, GHOSTED, PRE_GHOSTED, FROZEN, PRE_FROZEN on the prop's ballistic proxy) - without one a spawned body logs "no matching collision".</summary>
            public bool HasCollision;

            public ShortGuid Key => ShortGuidUtils.Generate(BodyName ?? "");
        }

        /// <summary>Retail's own templates whose bodies have no keyed model (they ship that way): left out of warnings.</summary>
        public static readonly string[] RetailExceptions =
        {
            "AYZ\\_Props_\\Inventory\\9mm_Pistol_Ammo",
            "Required_Assets\\Alien_Impact_VFX\\FaceHuggerLegDebris",
            "AYZ\\_Props_\\Physics\\Toy_Viewman_Template",
        };

        public static bool IsRetailException(Composite composite) =>
            composite != null && RetailExceptions.Any(o => string.Equals(o, (composite.name ?? "").Replace('/', '\\'), StringComparison.OrdinalIgnoreCase));

        /// <summary>The physics system a PhysicsSystem entity is bound to: its own resource's system, else its system_index; -1 when neither.</summary>
        public static int SystemIndexOf(FunctionEntity physicsEntity)
        {
            if (physicsEntity == null) return -1;
            ResourceReference reference = physicsEntity.resources?.FirstOrDefault(o => o != null && o.resource_type == ResourceType.DYNAMIC_PHYSICS_SYSTEM)
                ?? (physicsEntity.GetParameter(ShortGuids.resource)?.content as cResource)?.value?.FirstOrDefault(o => o != null && o.resource_type == ResourceType.DYNAMIC_PHYSICS_SYSTEM);
            if (reference != null)
            {
                if (reference.PhysicsSystem != null) return reference.PhysicsSystem.SystemIndex;
                if (reference.PhysicsSystemIndex >= 0) return reference.PhysicsSystemIndex;
            }
            if (physicsEntity.GetParameter("system_index")?.content is cInteger index && index.value >= 0)
                return index.value;
            return -1;
        }

        private static bool Draws(FunctionEntity model) => ((model?.GetParameter(ShortGuids.resource)?.content as cResource)?.value?.Count ?? 0) != 0;

        /// <summary>
        /// Every body of the physics systems the composite's PhysicsSystem entities are bound to, with the ModelReference it binds to
        /// and whether that one is keyed by the body's name. Empty for a composite with no bound PhysicsSystem. <paramref name="bound"/>:
        /// the system to take the bodies from instead of the one the entity is bound to now (a binding being made).
        /// </summary>
        public static List<Body> Inspect(Level level, Composite composite, HavokPackfile.PhysicsSystem bound = null)
        {
            List<Body> found = new List<Body>();
            HavokPackfile physics = level?.Physics;
            if (composite == null || physics == null || !physics.Loaded)
                return found;
            List<FunctionEntity> models = composite.functions.Where(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.ModelReference).ToList();
            foreach (FunctionEntity physicsEntity in composite.functions.Where(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.PhysicsSystem))
            {
                int index = bound?.SystemIndex ?? SystemIndexOf(physicsEntity);
                HavokPackfile.PhysicsSystem system = index < 0 ? null : physics.GetPhysicsSystem(index);
                if (system == null) continue;
                List<HavokPackfile.RigidBodyInfo> bodies = physics.GetRigidBodies(system) ?? new List<HavokPackfile.RigidBodyInfo>();
                foreach (HavokPackfile.RigidBodyInfo info in bodies)
                {
                    Body body = new Body() { Composite = composite, PhysicsEntity = physicsEntity, SystemIndex = index, SystemName = system.Name, BodyName = info.Name ?? "" };
                    ShortGuid key = body.Key;
                    FunctionEntity keyed = models.FirstOrDefault(o => (o.GetParameter(ShortGuids.resource)?.content as cResource)?.shortGUID == key);
                    FunctionEntity named = models.FirstOrDefault(o => string.Equals(CommandsUtils.GetEntityNameParameter(o), body.BodyName, StringComparison.OrdinalIgnoreCase));
                    if (keyed != null && Draws(keyed))
                    {
                        body.State = State.Keyed;
                        body.Model = keyed;
                    }
                    else if (named != null)
                    {
                        body.Model = named;
                        body.State = Draws(named) ? State.NotKeyed : State.DrawsNothing;
                    }
                    else
                    {
                        //No ModelReference named after it: with one body and one drawn model there is only one it can be
                        List<FunctionEntity> drawn = models.Where(Draws).ToList();
                        if (bodies.Count == 1 && drawn.Count == 1)
                        {
                            body.Model = drawn[0];
                            body.State = State.NotKeyed;
                        }
                        else
                            body.State = State.NoModel;
                    }
                    body.ModelName = body.Model == null ? null : CommandsUtils.GetEntityNameParameter(body.Model);
                    body.KeyNow = (body.Model?.GetParameter(ShortGuids.resource)?.content as cResource)?.shortGUID ?? ShortGuid.Invalid;
                    body.HasCollision = body.Model?.GetResource(ResourceType.COLLISION_MAPPING, true) != null;
                    found.Add(body);
                }
            }
            return found;
        }

        /// <summary>Every composite of the level holding a bound PhysicsSystem, with its bodies.</summary>
        public static List<Body> InspectLevel(Level level)
        {
            List<Body> all = new List<Body>();
            if (level?.Commands == null) return all;
            foreach (Composite composite in level.Commands.Entries)
            {
                if (composite == null || !composite.functions.Any(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.PhysicsSystem)) continue;
                all.AddRange(Inspect(level, composite));
            }
            return all;
        }

        /// <summary>A copy of the resource keyed by <paramref name="key"/>, each reference too (an undo record holds the old one).</summary>
        public static cResource Rekeyed(cResource resource, ShortGuid key)
        {
            cResource copy = new cResource() { shortGUID = key, value = new List<ResourceReference>() };
            foreach (ResourceReference reference in resource.value ?? new List<ResourceReference>())
            {
                if (reference == null) { copy.value.Add(null); continue; }
                ResourceReference clone = (ResourceReference)reference.Clone();
                clone.resource_id = key;
                copy.value.Add(clone);
            }
            return copy;
        }

        /// <summary>Key the bodies' ModelReferences inside an MCP script edit (one undo step with whatever else it does). Returns those it keyed.</summary>
        internal static List<Body> Key(McpScriptEdit edit, IEnumerable<Body> bodies)
        {
            List<Body> keyed = new List<Body>();
            foreach (Body body in bodies.Where(o => o.State == State.NotKeyed && o.Model != null))
            {
                Parameter parameter = body.Model.GetParameter(ShortGuids.resource);
                if (!(parameter?.content is cResource resource)) continue;
                edit.Touch(body.Composite, body.Model, look: false);
                //A new object, not the old one changed: the transaction's snapshot keeps the old one
                parameter.content = Rekeyed(resource, body.Key);
                //Changed directly, not through the edit's own methods: say so, or the edit counts as nothing done and is put back
                edit.Changed(body.Composite);
                keyed.Add(body);
            }
            return keyed;
        }

        /// <summary>
        /// Key every body of the composite that isn't yet, as one undo step labelled <paramref name="label"/> (the GUI's prefix, or the
        /// tools' "AI: "). Returns what each body is now; nothing changes when all are keyed already.
        /// </summary>
        public static List<Body> KeyComposite(Level level, Composite composite, string label, string labelPrefix = "", HavokPackfile.PhysicsSystem bound = null)
        {
            List<Body> bodies = Inspect(level, composite, bound);
            if (!bodies.Any(o => o.State == State.NotKeyed))
                return bodies;
            McpScriptEdit.Run(label, composite, edit => Key(edit, bodies), new McpScriptEdit.RunOptions() { Show = false, LabelPrefix = labelPrefix });
            return Inspect(level, composite, bound);
        }

        /// <summary>
        /// The GUI's binding of a composite's PhysicsSystem entity to <paramref name="system"/>: its bodies' ModelReferences keyed by the
        /// bodies' names (one undo step), so the prop also works spawned. Says nothing and changes nothing when they already are; a failure
        /// is logged, never shown (the binding itself stands).
        /// </summary>
        public static void KeyForBinding(Composite composite, HavokPackfile.PhysicsSystem system)
        {
            Level level = Singleton.Editor?.CompositeBrowser?.Content?.Level;
            if (level == null || composite == null || system == null) return;
            try
            {
                KeyComposite(level, composite, "Key physics bodies of " + composite.name + " by name", "", system);
            }
            catch (Exception e)
            {
                Debug.Log("PhysicsBodyKeys", "Could not key the bodies of " + composite.name + ": " + e.Message);
            }
        }

        /// <summary>One line for a body, for notes and the GUI.</summary>
        public static string Describe(Body body)
        {
            string model = body.ModelName == null ? "" : " (ModelReference '" + body.ModelName + "')";
            switch (body.State)
            {
                case State.Keyed: return "body '" + body.BodyName + "' finds its model" + model + (body.HasCollision ? "" : ", but has no collision of its own: spawned, it logs \"no matching collision\" and doesn't move (spawn_physics_prop gives it retail's)");
                case State.NotKeyed: return "body '" + body.BodyName + "'" + model + " is keyed by " + (body.KeyNow == body.Model?.shortGUID ? "its entity id" : body.KeyNow.ToByteString()) + ", not the body's name: spawned, it would find no model";
                case State.DrawsNothing: return "body '" + body.BodyName + "'" + model + ": that ModelReference draws nothing, so a spawned copy has no model to move";
                default: return "body '" + body.BodyName + "': no ModelReference in " + body.Composite.name + " is named after it, so a spawned copy has no model to move (name the ModelReference it moves '" + body.BodyName + "')";
            }
        }

        /// <summary>
        /// The composites an AssetSpawner spawns: the composite of each instance linked into an AssetSpawner's 'asset' (retail:
        /// AYZ\_Props_\Dynamic\X's spawner -> an instance of AYZ\_Props_\Physics\X_Template), with the spawner's composite.
        /// </summary>
        public static List<(Composite spawnerComposite, FunctionEntity spawner, Composite spawned)> SpawnedAssets(Commands commands)
        {
            List<(Composite, FunctionEntity, Composite)> found = new List<(Composite, FunctionEntity, Composite)>();
            ShortGuid asset = ShortGuidUtils.Generate("asset");
            foreach (Composite composite in commands.Entries)
            {
                if (composite == null) continue;
                foreach (FunctionEntity spawner in composite.functions.Where(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.AssetSpawner))
                {
                    foreach (EntityConnector link in spawner.childLinks.Where(o => o.thisParamID == asset))
                    {
                        if (!(composite.GetEntityByID(link.linkedEntityID) is FunctionEntity instance) || instance.function.IsFunctionType) continue;
                        Composite spawned = commands.GetComposite(instance.function);
                        if (spawned != null) found.Add((composite, spawner, spawned));
                    }
                }
            }
            return found;
        }
    }
}

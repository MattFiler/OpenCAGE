using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE
{
    /// <summary>
    /// What Save &amp; Build reads from a level's script, composite by composite: the placed content each composite holds (the
    /// entities of the types the build reads, the instances of composites carrying any, aliases, and a zone composite's
    /// sequences), fingerprinted so two looks at it can be compared. A change to any of it shows in game only after a build;
    /// a change to anything else (logic) needs only a save. <see cref="BuildTracker"/> uses it for the open level, and a mod
    /// export to tell whether the part of a level it carries needs building.
    /// </summary>
    public static class BuildFingerprint
    {
        /// <summary>A built entity as the build reads it: a hash of that, and what it is in words.</summary>
        public sealed class Item
        {
            public long Hash;
            public string Type;
        }

        /// <summary>What has been worked out about the composites of one level's script: which carry built content, and which pins of each feed it. Any thread.</summary>
        public sealed class Cache
        {
            internal readonly Dictionary<Composite, bool> Carries = new Dictionary<Composite, bool>();
            internal readonly Dictionary<Composite, HashSet<ShortGuid>> BuiltPins = new Dictionary<Composite, HashSet<ShortGuid>>();

            public void Clear()
            {
                lock (this)
                {
                    Carries.Clear();
                    BuiltPins.Clear();
                }
            }
        }

        //The types the build (instancing and its bakers) reads from the script
        private static readonly HashSet<ShortGuid> _builtTypes = BuiltTypes();
        private static readonly ShortGuid _zone = new ShortGuid((uint)FunctionType.Zone);
        private static readonly ShortGuid _triggerSequence = new ShortGuid((uint)FunctionType.TriggerSequence);

        private static HashSet<ShortGuid> BuiltTypes()
        {
            string[] names =
            {
                "ModelReference", "EnvironmentModelReference", "LightReference", "ParticleEmitterReference", "RibbonEmitterReference", "ProjectiveDecal",
                "FogBox", "FogPlane", "FogSphere", "SimpleWater", "SimpleRefraction", "SurfaceEffectBox", "SurfaceEffectSphere", "ExclusiveMaster",
                "CollisionBarrier", "PhysicsSystem", "RadiosityProxy", "RadiosityIsland", "Zone",
                "NavMeshArea", "NavMeshBarrier", "NavMeshExclusionArea", "NavMeshReachabilitySeedPoint", "NavMeshWalkablePlatform",
                "PathfindingAlienBackstageNode", "PathfindingManualNode", "PathfindingTeleportNode", "PathfindingWaitNode",
                "CoverExclusionArea", "CoverLine", "SpottingExclusionArea", "JOB_Assault", "JOB_SpottingPosition",
                "SoundBarrier", "SoundEnvironmentMarker", "SoundNetworkNode", "SoundLevelInitialiser",
            };
            HashSet<ShortGuid> types = new HashSet<ShortGuid>();
            foreach (string name in names)
                if (Enum.TryParse(name, out FunctionType type))
                    types.Add(new ShortGuid((uint)type));
            //Traversals become navmesh links
            foreach (FunctionType type in Enum.GetValues(typeof(FunctionType)))
                if (type.ToString().StartsWith("TRAV_", StringComparison.Ordinal))
                    types.Add(new ShortGuid((uint)type));
            return types;
        }

        /// <summary>The build-relevant entities of one composite: id -> a hash of what the build reads from it.</summary>
        public static Dictionary<ShortGuid, Item> Of(Commands commands, Composite composite, Cache cache)
        {
            Dictionary<ShortGuid, Item> items = new Dictionary<ShortGuid, Item>();
            bool zones = composite.functions.Any(o => o.function == _zone);
            foreach (FunctionEntity function in composite.functions.ToList())
            {
                string type;
                if (function.function.IsFunctionType)
                {
                    if (!_builtTypes.Contains(function.function) && !(zones && function.function == _triggerSequence)) continue;
                    type = function.function.AsFunctionType.ToString();
                }
                else
                {
                    Composite instanced = commands.GetComposite(function.function);
                    if (instanced == null || !Carries(commands, instanced, cache)) continue;
                    type = "instance of " + instanced.name;
                }
                long hash = function.function.GetHashCode();
                //An instance's own parameters are mostly its pins - logic - unless a pin feeds what the build reads
                hash = Mix(hash, function.function.IsFunctionType ? ParametersHash(function) : ParametersHash(function, BuiltPins(commands, commands.GetComposite(function.function), cache)));
                foreach (ResourceReference resource in function.resources)
                    hash = Mix(hash, resource?.GetHashCode() ?? 0);
                if (function.function == _zone || function.function == _triggerSequence)
                    foreach (EntityConnector link in function.childLinks)
                        hash = Mix(Mix(Mix(hash, link.thisParamID.GetHashCode()), link.linkedEntityID.GetHashCode()), link.linkedParamID.GetHashCode());
                items[function.shortGUID] = new Item() { Hash = hash, Type = type };
            }
            //Aliases override what instances place (positions, models, flags)
            foreach (AliasEntity alias in composite.aliases.ToList())
            {
                long hash = 31;
                if (alias.alias?.path != null)
                    foreach (ShortGuid step in alias.alias.path)
                        hash = Mix(hash, step.GetHashCode());
                hash = Mix(hash, ParametersHash(alias));
                items[alias.shortGUID] = new Item() { Hash = hash, Type = "alias" };
            }
            return items;
        }

        /// <summary>What differs between two fingerprints of a composite, in words ("Root: 1 added (ModelReference)"), or null when nothing does.</summary>
        public static string Describe(Composite composite, Dictionary<ShortGuid, Item> before, Dictionary<ShortGuid, Item> now)
        {
            List<Item> added = now.Where(o => !before.ContainsKey(o.Key)).Select(o => o.Value).ToList();
            List<Item> removed = before.Where(o => !now.ContainsKey(o.Key)).Select(o => o.Value).ToList();
            List<Item> changed = now.Where(o => before.TryGetValue(o.Key, out Item old) && old.Hash != o.Value.Hash).Select(o => o.Value).ToList();
            if (added.Count + removed.Count + changed.Count == 0)
                return null;
            List<string> parts = new List<string>();
            if (added.Count != 0) parts.Add(added.Count + " added");
            if (changed.Count != 0) parts.Add(changed.Count + " changed");
            if (removed.Count != 0) parts.Add(removed.Count + " removed");
            List<string> types = added.Concat(changed).Concat(removed).Select(o => o.Type).Distinct().Take(4).ToList();
            return composite.name + ": " + string.Join(", ", parts) + " (" + string.Join(", ", types) + ")";
        }

        /// <summary>
        /// Why the composites given need a build to show in game, comparing two copies of a level's script by composite: what
        /// the build reads from them added, changed or removed in <paramref name="after"/> - or null when it reads nothing
        /// different, and a plain save is all they need. Judged as the open level's changes are. Any thread.
        /// </summary>
        public static string NeedsBuild(Commands before, Commands after, IEnumerable<ShortGuid> composites)
        {
            Cache beforeCache = new Cache(), afterCache = new Cache();
            List<string> reasons = new List<string>();
            foreach (ShortGuid id in composites)
            {
                //A composite made or deleted changes nothing in game on its own: placing (or unplacing) it changes the one placing it
                Composite was = before.GetComposite(id), now = after.GetComposite(id);
                if (was == null || now == null) continue;
                string reason = Describe(now, Of(before, was, beforeCache), Of(after, now, afterCache));
                if (reason != null) reasons.Add(reason);
            }
            return reasons.Count == 0 ? null : string.Join("; ", reasons);
        }

        private static long ParametersHash(Entity entity, HashSet<ShortGuid> only = null)
        {
            long hash = 17;
            foreach (Parameter parameter in entity.parameters.ToList())
            {
                if (parameter == null || (only != null && !only.Contains(parameter.name))) continue;
                hash = Mix(Mix(hash, parameter.name.GetHashCode()), parameter.content?.GetHashCode() ?? 0);
            }
            return hash;
        }

        //What an instance's placement is made of: where it is and the flags that keep its contents out of the game
        private static readonly string[] _placementParameters = { "position", "deleted", "delete_me", "is_template", "is_shared", "delete_standard_collision", "delete_ballistic_collision" };

        /// <summary>
        /// The parameters of an instance of <paramref name="composite"/> the build reads: its placement, and the pins (variables)
        /// the composite links straight into an entity of a built type, or into such a pin of an instance inside it.
        /// </summary>
        private static HashSet<ShortGuid> BuiltPins(Commands commands, Composite composite, Cache cache)
        {
            HashSet<ShortGuid> pins = new HashSet<ShortGuid>(_placementParameters.Select(o => ShortGuidUtils.Generate(o)));
            if (composite == null) return pins;
            lock (cache)
                if (cache.BuiltPins.TryGetValue(composite, out HashSet<ShortGuid> known))
                    return known;
            lock (cache) cache.BuiltPins[composite] = pins; //placement only while it is worked out (a composite inside itself)
            foreach (VariableEntity variable in composite.variables.ToList())
            {
                foreach (EntityConnector link in variable.childLinks)
                {
                    if (!(composite.GetEntityByID(link.linkedEntityID) is FunctionEntity target)) continue;
                    bool feeds = target.function.IsFunctionType ? _builtTypes.Contains(target.function)
                        : BuiltPins(commands, commands.GetComposite(target.function), cache).Contains(link.linkedParamID);
                    if (feeds) { pins.Add(variable.name); break; }
                }
            }
            return pins;
        }

        private static long Mix(long hash, long value) => unchecked(hash * 31 + value);

        /// <summary>Whether a composite, or anything it places, holds content the build reads.</summary>
        private static bool Carries(Commands commands, Composite composite, Cache cache)
        {
            lock (cache)
                if (cache.Carries.TryGetValue(composite, out bool known))
                    return known;
            HashSet<Composite> seen = new HashSet<Composite>();
            bool carries = Carries(commands, composite, seen, cache);
            lock (cache)
                cache.Carries[composite] = carries;
            return carries;
        }

        private static bool Carries(Commands commands, Composite composite, HashSet<Composite> seen, Cache cache)
        {
            if (!seen.Add(composite)) return false;
            lock (cache)
                if (cache.Carries.TryGetValue(composite, out bool known))
                    return known;
            foreach (FunctionEntity function in composite.functions)
            {
                if (function.function.IsFunctionType)
                {
                    if (_builtTypes.Contains(function.function)) return true;
                    continue;
                }
                Composite instanced = commands.GetComposite(function.function);
                if (instanced != null && Carries(commands, instanced, seen, cache)) return true;
            }
            return false;
        }
    }
}

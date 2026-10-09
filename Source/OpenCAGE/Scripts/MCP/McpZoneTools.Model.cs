using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// The zone tools' model of a level, apart from the editor: the engine's names for zone pins and parameters, what
    /// links join and drive, a composite's placed content as the planner sees it, and what each door and pair of zones
    /// needs. Nothing here touches the editor's windows or undo history, so it runs over a level's script outside
    /// OpenCAGE too - which is how it is measured against real levels.
    /// </summary>
    internal static partial class McpZoneTools
    {
        internal static readonly ShortGuid ZoneA = ShortGuidUtils.Generate("ZoneA");

        internal static readonly ShortGuid ZoneB = ShortGuidUtils.Generate("ZoneB");

        internal static readonly ShortGuid ZoneLinkPin = ShortGuidUtils.Generate("zone_link");

        internal static readonly ShortGuid OpenOnReset = ShortGuidUtils.Generate("open_on_reset");

        internal static readonly ShortGuid LockOnReset = ShortGuidUtils.Generate("lock_on_reset");

        internal static readonly ShortGuid Cost = ShortGuidUtils.Generate("cost");

        internal static readonly ShortGuid ExcludeStreaming = ShortGuidUtils.Generate("exclude_streaming");

        internal static readonly ShortGuid OpenMethod = ShortGuidUtils.Generate("open");

        internal static readonly ShortGuid CloseMethod = ShortGuidUtils.Generate("close");

        internal static readonly HashSet<ShortGuid> GateMethods = new HashSet<ShortGuid>()
        {
            ShortGuidUtils.Generate("open"), ShortGuidUtils.Generate("close"), ShortGuidUtils.Generate("lock"), ShortGuidUtils.Generate("unlock"),
        };

        /// <summary>A Zone's methods for loading it from script: a zone something calls these on is loaded by script, not by where the player is.</summary>
        internal static readonly HashSet<ShortGuid> LoadMethods = new HashSet<ShortGuid>()
        {
            ShortGuidUtils.Generate("request_load"), ShortGuidUtils.Generate("cancel_load"), ShortGuidUtils.Generate("request_unload"), ShortGuidUtils.Generate("cancel_unload"),
        };

        //What the player sees or stands on: the function types instancing turns into movers or collision, which is what zones stream
        internal static readonly HashSet<ShortGuid> _content = new HashSet<ShortGuid>(new[]
        {
            FunctionType.ModelReference, FunctionType.EnvironmentModelReference, FunctionType.LightReference,
            FunctionType.ParticleEmitterReference, FunctionType.RibbonEmitterReference, FunctionType.FogBox, FunctionType.FogPlane,
            FunctionType.FogSphere, FunctionType.ProjectiveDecal, FunctionType.SimpleWater, FunctionType.SimpleRefraction,
            FunctionType.SurfaceEffectBox, FunctionType.SurfaceEffectSphere, FunctionType.CollisionBarrier,
        }.Select(o => new ShortGuid((uint)o)));

        internal static bool IsZone(Entity entity) => entity is FunctionEntity function && function.function == FunctionType.Zone;

        internal static bool IsZoneLink(Entity entity) => entity is FunctionEntity function && (function.function == FunctionType.ZoneLink || function.function == FunctionType.ZoneExclusionLink);

        internal static bool Flag(Entity entity, ShortGuid parameter) => (entity.GetParameter(parameter)?.content as cBool)?.value ?? false;

        /// <summary>A composite's input pins that take a zone link (a door composite's zone_link).</summary>
        internal static List<VariableEntity> ZoneLinkInputs(Commands commands, Composite composite)
        {
            return composite.variables.Where(o => o.type == DataType.ZONE_LINK && !(commands.Utils.GetPinInfo(composite, o) is CompositePinInfoTable.PinInfo info && info.PinTypeGUID == CompositePinType.CompositeOutputZoneLinkPtrVariablePin)).ToList();
        }

        /// <summary>The pin an instance of <paramref name="composite"/> takes its zone link on, or Invalid when it has none.</summary>
        internal static ShortGuid ZoneLinkPinOf(Commands commands, Composite composite)
        {
            List<VariableEntity> inputs = ZoneLinkInputs(commands, composite);
            return inputs.Count == 0 ? ShortGuid.Invalid : (inputs.FirstOrDefault(o => o.name == ZoneLinkPin) ?? inputs[0]).name;
        }

        /// <summary>
        /// Whether an entity placed in <paramref name="holder"/> passes its zone link pin up to a pin of <paramref name="holder"/>
        /// (Door_Package inside a Door_SML variant, say): then the instance of the holder is the door, not this.
        /// </summary>
        internal static bool HandsPinUp(Composite holder, Entity entity, ShortGuid pin)
        {
            foreach (EntityConnector link in entity.childLinks)
                if (link.thisParamID == pin && holder.GetEntityByID(link.linkedEntityID) is VariableEntity)
                    return true;
            return false;
        }

        /// <summary>Whether a composite runs zoning of its own (holds zones or takes them on pins): what is inside it is its business.</summary>
        internal static bool HoldsZones(Composite composite) => composite.functions.Any(o => o.function == FunctionType.Zone) || composite.variables.Any(o => o.type == DataType.ZONE);

        internal static bool SamePath(ShortGuid[] stored, IList<ShortGuid> ids)
        {
            if (stored == null) return false;
            int length = stored.Length;
            if (length > 0 && stored[length - 1] == ShortGuid.Invalid) length--;
            if (length != ids.Count) return false;
            for (int i = 0; i < length; i++)
                if (stored[i] != ids[i]) return false;
            return true;
        }

        /// <summary>The zones a ZoneLink or ZoneExclusionLink joins, read without describing it (null for a side that is not linked).</summary>
        internal static (Entity a, Entity b) SidesOf(Commands commands, Composite composite, FunctionEntity link)
        {
            Entity Side(ShortGuid pin)
            {
                foreach (EntityConnector connector in link.childLinks)
                {
                    if (connector.thisParamID != pin) continue;
                    Entity target = composite.GetEntityByID(connector.linkedEntityID);
                    if (target is AliasEntity alias)
                    {
                        Entity resolved = commands.Utils.GetResolvedTarget(commands.Utils.ResolveAlias(alias, composite)).Item2;
                        return IsZone(resolved) ? resolved : target;
                    }
                    return target;
                }
                return null;
            }
            return (Side(ZoneA), Side(ZoneB));
        }

        #region Links into entities
        /// <summary>A composite's links indexed by the entity they go to, so what reaches an entity is found without reading every entity.</summary>
        internal sealed class Incoming
        {
            private static readonly List<(Entity from, EntityConnector link)> None = new List<(Entity, EntityConnector)>();
            private readonly Dictionary<ShortGuid, List<(Entity from, EntityConnector link)>> _to = new Dictionary<ShortGuid, List<(Entity, EntityConnector)>>();

            public Incoming(Composite composite)
            {
                foreach (Entity entity in composite.GetEntities())
                    foreach (EntityConnector link in entity.childLinks)
                    {
                        if (!_to.TryGetValue(link.linkedEntityID, out List<(Entity, EntityConnector)> list))
                            _to[link.linkedEntityID] = list = new List<(Entity, EntityConnector)>();
                        list.Add((entity, link));
                    }
            }

            public List<(Entity from, EntityConnector link)> To(ShortGuid entity) => _to.TryGetValue(entity, out List<(Entity, EntityConnector)> list) ? list : None;
        }

        /// <summary>What opens, closes and points at a ZoneLink or ZoneExclusionLink.</summary>
        internal sealed class LinkState
        {
            public bool Exclusion;
            public bool OpenOnReset;
            public bool LockOnReset;
            /// <summary>Entities pointing at its reference: doors' zone link pins (or aliases of them).</summary>
            public List<Entity> Drivers = new List<Entity>();
            /// <summary>Links into its open, close, lock or unlock methods.</summary>
            public List<(Entity from, ShortGuid method)> Methods = new List<(Entity, ShortGuid)>();
            /// <summary>Entities whose output sets its open_on_reset (a gate saying whether a lift is at this floor, say).</summary>
            public List<Entity> OpenFrom = new List<Entity>();
            /// <summary>Links into its ZoneA or ZoneB, which run the wrong way.</summary>
            public List<(Entity from, ShortGuid pin)> Backwards = new List<(Entity, ShortGuid)>();

            /// <summary>Whether anything ever opens it. Only 'open' opens: a link that is only closed or locked never opens.</summary>
            public bool Opened => Exclusion || OpenOnReset || OpenFrom.Count != 0 || Drivers.Count != 0 || Methods.Any(o => o.method == OpenMethod);

            /// <summary>Whether it never closes: an exclusion link, or a ZoneLink open on reset that nothing drives, closes or locks.</summary>
            public bool AlwaysOpen => Exclusion || (OpenOnReset && !LockOnReset && Drivers.Count == 0 && OpenFrom.Count == 0 && Methods.All(o => o.method == OpenMethod));

            /// <summary>Whether nothing in the script works it, so it is free to be given to a door or opened on reset.</summary>
            public bool Untouched => Drivers.Count == 0 && Methods.Count == 0 && OpenFrom.Count == 0;
        }

        internal static LinkState StateOf(Composite composite, FunctionEntity link, Incoming incoming)
        {
            LinkState state = new LinkState()
            {
                Exclusion = link.function == FunctionType.ZoneExclusionLink,
                OpenOnReset = Flag(link, OpenOnReset),
                LockOnReset = Flag(link, LockOnReset),
            };
            foreach (EntityConnector connector in link.childLinks)
                if (connector.thisParamID == OpenOnReset && composite.GetEntityByID(connector.linkedEntityID) is Entity source && !state.OpenFrom.Contains(source))
                    state.OpenFrom.Add(source);
            foreach ((Entity from, EntityConnector connector) in incoming.To(link.shortGUID))
            {
                if (connector.linkedParamID == ShortGuids.reference) { if (!state.Drivers.Contains(from)) state.Drivers.Add(from); }
                else if (GateMethods.Contains(connector.linkedParamID)) state.Methods.Add((from, connector.linkedParamID));
                else if (connector.linkedParamID == OpenOnReset) { if (!state.OpenFrom.Contains(from)) state.OpenFrom.Add(from); }
                else if (connector.linkedParamID == ZoneA || connector.linkedParamID == ZoneB) state.Backwards.Add((from, connector.linkedParamID));
            }
            return state;
        }

        /// <summary>
        /// Whether a Zone is loaded by script rather than by where the player is: something calls its load methods (the animation
        /// zones of a hiding place, say) and no link joins it to another zone. Such a zone is not a room, and what it claims is not
        /// zoned by it.
        /// </summary>
        internal static bool IsScriptZone(Entity zone, Incoming incoming)
        {
            bool loaded = false, linked = false;
            foreach ((Entity from, EntityConnector link) in incoming.To(zone.shortGUID))
            {
                if (LoadMethods.Contains(link.linkedParamID)) loaded = true;
                else if (link.linkedParamID == ShortGuids.reference && IsZoneLink(from)) linked = true;
            }
            return loaded && !linked;
        }
        #endregion

        #region Scene
        /// <summary>A ZoneLink or ZoneExclusionLink inside an instance the zoning composite places.</summary>
        internal sealed class NestedLink
        {
            /// <summary>The instances stepped through from the zoning composite to the one holding the link.</summary>
            public List<Entity> Chain;
            /// <summary>The composites along the chain: the zoning composite, then each instance's.</summary>
            public List<Composite> Holders;
            public FunctionEntity Link;
        }

        /// <summary>A composite's placed content as the planner sees it, with the zones that already claim it.</summary>
        internal sealed class Scene
        {
            public Commands Commands;
            public Composite Composite;
            /// <summary>Where the composite is placed in the level (its first placement); positions in results are composed with it.</summary>
            public cTransform Origin;
            public int Placements;
            /// <summary>
            /// Each placement of the composite (up to <see cref="MaxPlacements"/>): the path from the root to its instance, as a key
            /// ("" for the root itself), and its world transform. An area test looks at every placement, so content of a composite
            /// placed twice is found in either copy's area.
            /// </summary>
            public List<(string key, cTransform world)> AllPlacements = new List<(string, cTransform)>();
            public const int MaxPlacements = 32;
            public List<McpZonePlanner.Item> Items = new List<McpZonePlanner.Item>();
            /// <summary>Every model, light, effect and piece of collision under the composite, those inside doors included.</summary>
            public List<McpZonePlanner.Leaf> Leaves = new List<McpZonePlanner.Leaf>();
            public List<McpZonePlanner.Door> Doors = new List<McpZonePlanner.Door>();
            /// <summary>Every composite instance placed under the composite, at any depth: where the planner reads kit types (walls, windows, vents) that sit inside larger composites.</summary>
            public List<McpZonePlanner.Item> Instances = new List<McpZonePlanner.Item>();
            /// <summary>Each door's pin that takes its zone link, by door path.</summary>
            public Dictionary<string, ShortGuid> DoorPins = new Dictionary<string, ShortGuid>();
            public HashSet<string> DoorPaths = new HashSet<string>();
            /// <summary>The zones of this composite itself - those a link here can join - by label.</summary>
            public Dictionary<string, Entity> Zones = new Dictionary<string, Entity>();
            /// <summary>Every zone claiming content here (this composite's, and those inside instances it places), by label.</summary>
            public Dictionary<string, string> ZoneNames = new Dictionary<string, string>();
            /// <summary>The zones loaded by script (see <see cref="IsScriptZone"/>), by label: not rooms, and left out of <see cref="Claims"/>.</summary>
            public HashSet<string> ScriptZones = new HashSet<string>();
            /// <summary>The room zones claiming each leaf, door and item, by path.</summary>
            public Dictionary<string, List<string>> Claims = new Dictionary<string, List<string>>();
            /// <summary>The script-loaded zones claiming each path that any claims.</summary>
            public Dictionary<string, List<string>> ScriptClaims = new Dictionary<string, List<string>>();
            public List<NestedLink> NestedLinks = new List<NestedLink>();
            public bool Truncated;

            private McpZonePlanner.Geometry _geometry;
            /// <summary>The content prepared for the planner: door frames, walls, units (built on first use).</summary>
            public McpZonePlanner.Geometry Geometry => _geometry ?? (_geometry = new McpZonePlanner.Geometry(Items, Leaves, Doors, Instances));

            private McpZonePlanner.LeafIndex _index;
            public McpZonePlanner.LeafIndex Index => _index ?? (_index = new McpZonePlanner.LeafIndex(Leaves));

            public Vector3 World(Vector3 local) => Origin == null ? local : InstanceTransform.Compose(Origin, new cTransform(local, Vector3.Zero)).position;

            /// <summary>The one room zone claiming a leaf; null when none does, or several do (a doorframe two rooms share).</summary>
            public string ZoneOf(McpZonePlanner.Leaf leaf) => Claims.TryGetValue(leaf.Path, out List<string> zones) && zones.Count == 1 ? zones[0] : null;

            public List<string> ClaimsOf(string path) => Claims.TryGetValue(path, out List<string> zones) ? zones : new List<string>();

            public string NameOf(string label) => label == null ? "(nothing)" : (ZoneNames.TryGetValue(label, out string name) ? name : label);

            /// <summary>Whether a zone can be linked from this composite: one of its own room zones.</summary>
            public bool Linkable(string label) => label != null && Zones.ContainsKey(label) && !ScriptZones.Contains(label);

            /// <summary>The door a path is inside (or is), or null.</summary>
            public string DoorOf(string path)
            {
                int cut = -1;
                do
                {
                    cut = path.IndexOf('/', cut + 1);
                    string prefix = cut < 0 ? path : path.Substring(0, cut);
                    if (DoorPaths.Contains(prefix)) return prefix;
                } while (cut >= 0);
                return null;
            }
        }

        internal static string Key(IEnumerable<Entity> chain) => string.Join("/", chain.Select(o => McpScript.Id(o.shortGUID)));

        internal static ShortGuid[] PathOf(string key) => key.Split('/').Select(o => new ShortGuid(o)).Concat(new[] { ShortGuid.Invalid }).ToArray();

        /// <summary>
        /// The composite to zone when none is named: the root, or the composite it places that holds the most - a vanilla
        /// level keeps its geometry and zones in one such composite (ENVIRONMENT_...), a level made from scratch in its root.
        /// </summary>
        internal static Composite DefaultZoningComposite(Commands commands)
        {
            Composite root = commands.EntryPoints[0];
            //Composite ids up front: looking up a built-in function type as a composite searches every composite
            HashSet<ShortGuid> composites = new HashSet<ShortGuid>(commands.Entries.Where(o => o != null).Select(o => o.shortGUID));
            Composite best = root;
            int bestScore = Score(root);
            foreach (FunctionEntity instance in root.functions)
            {
                if (!composites.Contains(instance.function)) continue;
                Composite child = commands.GetComposite(instance.function);
                if (child == null || child == root) continue;
                int score = Score(child);
                if (score > bestScore) { best = child; bestScore = score; }
            }
            return best;

            //What a composite holds directly: zones count most (that is where zoning already lives), then placed instances and content
            int Score(Composite composite) => composite.functions.Count(o => o.function == FunctionType.Zone) * 100000
                + composite.functions.Count(o => _content.Contains(o.function) || composites.Contains(o.function));
        }

        /// <summary>Walk a composite's placed content into a <see cref="Scene"/>. On the UI thread in the editor (it reads the live script).</summary>
        internal static Scene BuildScene(Commands commands, Composite composite, CancellationToken cancel)
        {
            Composite root = commands.EntryPoints[0];
            Scene scene = new Scene() { Commands = commands, Composite = composite };
            McpPlacements walker = new McpPlacements(commands);
            if (composite != root)
            {
                List<McpPlacements.Placement> found = new List<McpPlacements.Placement>();
                scene.Placements = walker.PlacementsOf(root, composite, null, found, Scene.MaxPlacements, cancel);
                scene.Origin = found.Count != 0 ? found[0].World : null;
                foreach (McpPlacements.Placement placement in found)
                    scene.AllPlacements.Add((Key(placement.Chain), placement.World ?? new cTransform(Vector3.Zero, Vector3.Zero)));
            }
            else
            {
                scene.Placements = 1;
                scene.AllPlacements.Add(("", new cTransform(Vector3.Zero, Vector3.Zero)));
            }

            //Per function id: the composite it places (null for a built-in type); per composite: its zone link pin, and whether it zones itself
            Dictionary<ShortGuid, Composite> placed = new Dictionary<ShortGuid, Composite>();
            Dictionary<ShortGuid, ShortGuid> doorPin = new Dictionary<ShortGuid, ShortGuid>();
            Dictionary<ShortGuid, bool> zoning = new Dictionary<ShortGuid, bool>();
            Composite Placed(ShortGuid function)
            {
                if (!placed.TryGetValue(function, out Composite found))
                    placed[function] = found = function.IsFunctionType ? null : commands.GetComposite(function);
                return found;
            }
            ShortGuid PinOf(Composite door)
            {
                if (!doorPin.TryGetValue(door.shortGUID, out ShortGuid pin))
                    doorPin[door.shortGUID] = pin = ZoneLinkPinOf(commands, door);
                return pin;
            }
            bool Zoning(Composite holder)
            {
                if (!zoning.TryGetValue(holder.shortGUID, out bool result))
                    zoning[holder.shortGUID] = result = HoldsZones(holder);
                return result;
            }
            List<Composite> HoldersOf(List<Entity> chain, int count)
            {
                List<Composite> holders = new List<Composite>() { composite };
                for (int i = 0; i < count; i++)
                    holders.Add(Placed(((FunctionEntity)chain[i]).function));
                return holders;
            }

            walker.Walk(composite, step =>
            {
                if (!(step.Entity is FunctionEntity function)) return true;
                Composite instanced = Placed(function.function);
                int depth = step.Chain.Count;
                cTransform world = step.World;
                string key = null;

                if (depth > 1 && IsZoneLink(function))
                    scene.NestedLinks.Add(new NestedLink() { Chain = step.Chain.Take(depth - 1).ToList(), Holders = HoldersOf(step.Chain, depth - 1), Link = function });

                //A door: an instance of a composite with a zone link pin, or a Door function, that does not hand its pin up to its
                //holder's (that holder's instance is the door), is not inside another door, and is not inside a composite that does
                //its own zoning (a lift's doors are the lift's business)
                ShortGuid pin = instanced != null ? PinOf(instanced) : (function.function == FunctionType.Door ? ZoneLinkPin : ShortGuid.Invalid);
                if (pin != ShortGuid.Invalid && HandsPinUp(step.Composite, function, pin))
                    pin = ShortGuid.Invalid;
                if (pin != ShortGuid.Invalid && depth > 1)
                {
                    string prefix = null;
                    for (int i = 0; i < depth - 1 && pin != ShortGuid.Invalid; i++)
                    {
                        prefix = prefix == null ? McpScript.Id(step.Chain[i].shortGUID) : prefix + "/" + McpScript.Id(step.Chain[i].shortGUID);
                        Composite holder = Placed(((FunctionEntity)step.Chain[i]).function);
                        if (scene.DoorPaths.Contains(prefix) || (holder != null && Zoning(holder)))
                            pin = ShortGuid.Invalid;
                    }
                }
                if (pin != ShortGuid.Invalid)
                {
                    cTransform at = world ?? step.Origin;
                    key = Key(step.Chain);
                    scene.Doors.Add(new McpZonePlanner.Door()
                    {
                        Path = key,
                        Name = McpScript.EntityName(commands, step.Composite, function),
                        Type = instanced?.name ?? "Door",
                        Position = at?.position ?? Vector3.Zero,
                        Rotation = at?.rotation ?? Vector3.Zero,
                    });
                    scene.DoorPins[key] = pin;
                    scene.DoorPaths.Add(key);
                }
                if (instanced != null && world != null)
                {
                    key = key ?? Key(step.Chain);
                    scene.Instances.Add(new McpZonePlanner.Item() { Path = key, Type = instanced.name, Position = world.position, Rotation = world.rotation });
                }
                if (depth == 1 && world != null && (instanced != null || _content.Contains(function.function)))
                {
                    key = key ?? Key(step.Chain);
                    scene.Items.Add(new McpZonePlanner.Item()
                    {
                        Path = key,
                        Name = McpScript.EntityName(commands, step.Composite, function),
                        Type = instanced?.name ?? function.function.AsFunctionType.ToString(),
                        Position = world.position,
                        Rotation = world.rotation,
                    });
                }
                if (world != null && _content.Contains(function.function))
                    scene.Leaves.Add(new McpZonePlanner.Leaf() { Path = key ?? Key(step.Chain), Type = function.function.AsFunctionType.ToString(), Position = world.position });
                return true;
            }, null, cancel);
            scene.Truncated = walker.Truncated;

            //Who claims what, as the viewport's zone overlay works it out from here: each zone entry as (path prefix, zone label)
            Dictionary<Composite, Incoming> incoming = new Dictionary<Composite, Incoming>();
            Incoming IncomingOf(Composite holder)
            {
                if (!incoming.TryGetValue(holder, out Incoming found)) incoming[holder] = found = new Incoming(holder);
                return found;
            }
            List<(string prefix, string label)> claimedRoots = new List<(string, string)>();
            foreach (SyncedZone zone in ZoneMembership.CalculateFrom(commands, composite))
            {
                Composite holder = commands.GetComposite(new ShortGuid(zone.zone_composite));
                Entity entity = holder?.GetEntityByID(new ShortGuid(zone.zone_entity));
                string label = string.Join("/", zone.zone_path.Select(o => McpScript.Id(new ShortGuid(o))).Concat(new[] { McpScript.Id(new ShortGuid(zone.zone_entity)) }));
                string name = holder == null || entity == null ? zone.name : McpScript.EntityName(commands, holder, entity);
                scene.ZoneNames[label] = zone.zone_path.Count == 0 ? name : name + " (in " + McpScript.CompositeLeaf(holder) + ")";
                if (entity != null && IsScriptZone(entity, IncomingOf(holder)))
                    scene.ScriptZones.Add(label);
                if (zone.zone_path.Count == 0 && holder == composite && entity != null)
                    scene.Zones[label] = entity;
                foreach (List<uint> root2 in zone.roots)
                {
                    if (root2 == null || root2.Count == 0) continue;
                    string prefix = string.Join("/", root2.Select(o => McpScript.Id(new ShortGuid(o))));
                    claimedRoots.Add((prefix, label));
                }
            }
            //The overlay leaves out zones that claim nothing yet; they can still be linked
            foreach (FunctionEntity zone in composite.functions.Where(o => o.function == FunctionType.Zone))
            {
                string label = McpScript.Id(zone.shortGUID);
                if (scene.Zones.ContainsKey(label)) continue;
                scene.Zones[label] = zone;
                scene.ZoneNames[label] = McpScript.EntityName(commands, composite, zone);
                if (IsScriptZone(zone, IncomingOf(composite)))
                    scene.ScriptZones.Add(label);
            }
            //A zone may list one entry and another a parent of it: a path takes every zone whose entry is one of its prefixes
            Dictionary<string, List<string>> byPrefix = new Dictionary<string, List<string>>();
            foreach ((string prefix, string label) in claimedRoots)
            {
                if (!byPrefix.TryGetValue(prefix, out List<string> labels)) byPrefix[prefix] = labels = new List<string>();
                if (!labels.Contains(label)) labels.Add(label);
            }
            foreach (string path in scene.Leaves.Select(o => o.Path).Concat(scene.Doors.Select(o => o.Path)).Concat(scene.Items.Select(o => o.Path)))
            {
                if (scene.Claims.ContainsKey(path)) continue;
                List<string> zones = new List<string>();
                List<string> scripted = null;
                int cut = -1;
                do
                {
                    cut = path.IndexOf('/', cut + 1);
                    string prefix = cut < 0 ? path : path.Substring(0, cut);
                    if (byPrefix.TryGetValue(prefix, out List<string> labels))
                        foreach (string label in labels)
                        {
                            if (scene.ScriptZones.Contains(label)) { if (scripted == null) scripted = new List<string>(); if (!scripted.Contains(label)) scripted.Add(label); }
                            else if (!zones.Contains(label)) zones.Add(label);
                        }
                } while (cut >= 0);
                scene.Claims[path] = zones;
                if (scripted != null) scene.ScriptClaims[path] = scripted;
            }
            return scene;
        }
        #endregion

        #region Analysis
        /// <summary>A ZoneLink or ZoneExclusionLink as the analysis sees it: the zones it joins (by label) and what works it.</summary>
        internal sealed class LinkInfo
        {
            public FunctionEntity Link;
            /// <summary>The composite holding it: the zoning composite, or one placed under it.</summary>
            public Composite Holder;
            /// <summary>The path of the instance holding it ("" for the zoning composite's own).</summary>
            public string Path;
            /// <summary>The zones it joins, by label; null for a side that is not linked to a zone that can be found.</summary>
            public string A, B;
            public LinkState State;
            public bool Own => Path.Length == 0;
            public bool Joins(string a, string b) => (A == a && B == b) || (A == b && B == a);
            public bool HalfWired => A == null || B == null;
        }

        /// <summary>A door as the analysis sees it: what lists it, what it joins, what it drives.</summary>
        internal sealed class DoorState
        {
            public McpZonePlanner.Door Door;
            /// <summary>The room zones listing it.</summary>
            public List<string> ListedBy;
            /// <summary>The two zones it stands between; null for a dead end, or when that cannot be told.</summary>
            public (string a, string b)? Joins;
            /// <summary>How <see cref="Joins"/> was found: "listing" (the two zones listing it) or "position" (the content either side).</summary>
            public string JoinsFrom;
            /// <summary>It opens onto nothing on one side (a door kept shut, a cupboard).</summary>
            public bool DeadEnd;
            /// <summary>The zone of a dead end's open side.</summary>
            public string Side;
            /// <summary>It has a window: the rooms see each other with it shut, so they need an always-open link and it drives none.</summary>
            public bool SeeThrough;
            /// <summary>The ZoneLinks its pin drives (in the zoning composite or the composites along its path).</summary>
            public List<LinkInfo> Drives = new List<LinkInfo>();
            /// <summary>The ZoneLinks it opens or closes through method links (a door's started_opening -> open).</summary>
            public List<LinkInfo> Works = new List<LinkInfo>();
            /// <summary>ZoneExclusionLinks its pin points at: always open, so a door cannot drive one.</summary>
            public List<LinkInfo> DrivesExclusion = new List<LinkInfo>();
            /// <summary>Other things its pin points at, by name.</summary>
            public List<string> DrivesOther = new List<string>();
            /// <summary>The entities of the zoning composite its zone link goes from: the door, or aliases reaching it.</summary>
            public List<Entity> Owners = new List<Entity>();
        }

        /// <summary>Everything the zone tools decide from: the scene, the links already made, and what each door and pair needs.</summary>
        internal sealed class Analysis
        {
            public Scene Scene;
            public List<DoorState> Doors = new List<DoorState>();
            public List<McpZonePlanner.Pair> Pairs = new List<McpZonePlanner.Pair>();
            /// <summary>Links of the zoning composite and of the instances it places, with the zones each joins.</summary>
            public List<LinkInfo> Links = new List<LinkInfo>();
            public Func<McpZonePlanner.Leaf, string> ZoneOf;

            public List<LinkInfo> Joining(string a, string b) => Links.Where(o => o.Joins(a, b)).ToList();

            /// <summary>An always-open link joining two zones, or null: with one, the zones see each other whatever a door between them does.</summary>
            public LinkInfo OpenLinkBetween(string a, string b) => Links.FirstOrDefault(o => o.Joins(a, b) && o.State.AlwaysOpen);

            public DoorState DoorAt(string path) => Doors.FirstOrDefault(o => o.Door.Path == path);
        }

        /// <summary>
        /// The label of the zone a link's side stands for, followed through aliases and up through zone pins to the zone the
        /// placement feeds them (a lift's zone_bottom is the room it stands in); null when it leads to no zone.
        /// </summary>
        private static string SideLabel(Commands commands, List<Entity> chain, List<Composite> holders, int depth, Entity side)
        {
            for (int guard = 0; guard < 32 && side != null; guard++)
            {
                string prefix = depth == 0 ? "" : Key(chain.Take(depth)) + "/";
                if (IsZone(side))
                    return prefix + McpScript.Id(side.shortGUID);
                if (side is AliasEntity alias)
                {
                    List<Tuple<Composite, Entity>> resolved = commands.Utils.ResolveAlias(alias, holders[depth]);
                    if (resolved == null || resolved.Count == 0 || !IsZone(resolved[resolved.Count - 1].Item2)) return null;
                    return prefix + string.Join("/", resolved.Select(o => McpScript.Id(o.Item2.shortGUID)));
                }
                if (!(side is VariableEntity pin) || pin.type != DataType.ZONE || depth == 0)
                    return null;
                //A zone pin: whatever the placement one composite up links to it
                Entity placement = chain[depth - 1];
                Composite parent = holders[depth - 1];
                Entity next = null;
                foreach (EntityConnector link in placement.childLinks)
                    if (link.thisParamID == pin.name || link.thisParamID == pin.shortGUID) { next = parent.GetEntityByID(link.linkedEntityID); break; }
                side = next;
                depth--;
            }
            return null;
        }

        internal static Analysis Analyse(Commands commands, Scene scene, Func<McpZonePlanner.Leaf, string> zoneOf = null)
        {
            Analysis analysis = new Analysis() { Scene = scene, ZoneOf = zoneOf ?? scene.ZoneOf };
            zoneOf = analysis.ZoneOf;
            Composite composite = scene.Composite;
            Dictionary<Composite, Incoming> incoming = new Dictionary<Composite, Incoming>();
            Incoming IncomingOf(Composite holder)
            {
                if (!incoming.TryGetValue(holder, out Incoming found)) incoming[holder] = found = new Incoming(holder);
                return found;
            }

            List<Entity> none = new List<Entity>();
            List<Composite> top = new List<Composite>() { composite };
            foreach (FunctionEntity function in composite.functions)
            {
                if (!IsZoneLink(function)) continue;
                //The raw side entities: an alias side is labelled by the path it resolves through, as the scene labels nested zones
                Entity a = null, b = null;
                foreach (EntityConnector connector in function.childLinks)
                {
                    if (connector.thisParamID == ZoneA && a == null) a = composite.GetEntityByID(connector.linkedEntityID);
                    else if (connector.thisParamID == ZoneB && b == null) b = composite.GetEntityByID(connector.linkedEntityID);
                }
                analysis.Links.Add(new LinkInfo()
                {
                    Link = function, Holder = composite, Path = "",
                    A = SideLabel(commands, none, top, 0, a), B = SideLabel(commands, none, top, 0, b),
                    State = StateOf(composite, function, IncomingOf(composite)),
                });
            }
            foreach (NestedLink nested in scene.NestedLinks)
            {
                Composite holder = nested.Holders[nested.Holders.Count - 1];
                Entity a = null, b = null;
                foreach (EntityConnector connector in nested.Link.childLinks)
                {
                    if (connector.thisParamID == ZoneA && a == null) a = holder.GetEntityByID(connector.linkedEntityID);
                    else if (connector.thisParamID == ZoneB && b == null) b = holder.GetEntityByID(connector.linkedEntityID);
                }
                int depth = nested.Chain.Count;
                analysis.Links.Add(new LinkInfo()
                {
                    Link = nested.Link, Holder = holder, Path = Key(nested.Chain),
                    A = SideLabel(commands, nested.Chain, nested.Holders, depth, a), B = SideLabel(commands, nested.Chain, nested.Holders, depth, b),
                    State = StateOf(holder, nested.Link, IncomingOf(holder)),
                });
            }
            Dictionary<(string, ShortGuid), LinkInfo> linkAt = new Dictionary<(string, ShortGuid), LinkInfo>();
            foreach (LinkInfo info in analysis.Links)
                linkAt[(info.Path, info.Link.shortGUID)] = info;

            McpZonePlanner.Geometry geometry = scene.Geometry;
            foreach (McpZonePlanner.Door door in scene.Doors)
            {
                DoorState state = new DoorState()
                {
                    Door = door,
                    ListedBy = scene.ClaimsOf(door.Path),
                    DeadEnd = geometry.IsDeadEnd(door),
                    SeeThrough = geometry.IsSeeThrough(door),
                };
                Join(scene, state, zoneOf);

                //What its pin drives: links from the door (in its own composite) or from aliases reaching it, in each composite along its path
                List<ShortGuid> ids = door.Path.Split('/').Select(o => new ShortGuid(o)).ToList();
                List<Composite> holders = new List<Composite>() { composite };
                for (int i = 0; i < ids.Count - 1; i++)
                    holders.Add(holders[i] == null ? null : McpScript.InstancedComposite(commands, holders[i].GetEntityByID(ids[i])));
                ShortGuid pin = scene.DoorPins[door.Path];
                for (int depth = 0; depth < ids.Count; depth++)
                {
                    Composite holder = holders[depth];
                    if (holder == null) break;
                    string at = depth == 0 ? "" : string.Join("/", door.Path.Split('/').Take(depth));
                    List<Entity> owners = new List<Entity>();
                    if (depth == ids.Count - 1)
                    {
                        Entity self = holder.GetEntityByID(ids[depth]);
                        if (self != null) owners.Add(self);
                    }
                    List<ShortGuid> rest = ids.Skip(depth).ToList();
                    owners.AddRange(holder.aliases.Where(o => SamePath(o.alias?.path, rest)));
                    if (depth == 0) state.Owners.AddRange(owners);
                    foreach (Entity owner in owners)
                        foreach (EntityConnector connector in owner.childLinks)
                        {
                            bool viaPin = connector.thisParamID == pin;
                            bool viaMethod = !viaPin && (connector.linkedParamID == OpenMethod || connector.linkedParamID == CloseMethod);
                            if (!viaPin && !viaMethod) continue;
                            if (!linkAt.TryGetValue((at, connector.linkedEntityID), out LinkInfo info))
                            {
                                if (viaPin && holder.GetEntityByID(connector.linkedEntityID) is Entity target && !(target is VariableEntity))
                                    state.DrivesOther.Add(McpScript.EntityName(commands, holder, target) + " (" + McpScript.TypeName(commands, holder, target) + ")");
                                continue;
                            }
                            if (info.State.Exclusion) { if (viaPin && !state.DrivesExclusion.Contains(info)) state.DrivesExclusion.Add(info); }
                            else if (viaPin) { if (!state.Drives.Contains(info)) state.Drives.Add(info); }
                            else if (!state.Works.Contains(info)) state.Works.Add(info);
                        }
                }
                analysis.Doors.Add(state);
            }
            analysis.Pairs = geometry.Neighbours(zoneOf);
            return analysis;
        }

        /// <summary>
        /// Which zones a door stands between: the two room zones listing it when there are exactly two (the level says so), and
        /// otherwise the content either side of it. A dead end joins nothing; its open side's zone is noted.
        /// </summary>
        internal static void Join(Scene scene, DoorState state, Func<McpZonePlanner.Leaf, string> zoneOf)
        {
            state.Joins = null;
            state.Side = null;
            if (state.ListedBy.Count == 2)
            {
                state.Joins = (state.ListedBy[0], state.ListedBy[1]);
                state.JoinsFrom = "listing";
                return;
            }
            if (state.DeadEnd)
            {
                state.Side = state.ListedBy.Count == 1 ? state.ListedBy[0] : NearestZone(scene, state.Door, zoneOf);
                return;
            }
            state.Joins = scene.Geometry.Bridge(state.Door, zoneOf, out string both);
            state.JoinsFrom = "position";
            //Both sides one zone's (zones merged across it): it belongs to that zone. Unzoned content on a side counts as a side of its own,
            //so a door facing a room not zoned yet keeps Side null (and is reported as such)
            if (state.Joins == null && both != null)
            {
                scene.Geometry.SideWinners(state.Door, zoneOf, "", out string plus, out string minus);
                if (!string.IsNullOrEmpty(plus) && plus == minus)
                    state.Side = state.ListedBy.Count == 1 ? state.ListedBy[0] : plus;
            }
        }

        /// <summary>The zone of the content nearest a door on its storey (a dead end's open side), or null.</summary>
        internal static string NearestZone(Scene scene, McpZonePlanner.Door door, Func<McpZonePlanner.Leaf, string> zoneOf)
        {
            string best = null;
            float bestDistance = float.MaxValue;
            foreach (McpZonePlanner.Leaf leaf in scene.Index.Near(door.Position, 4f))
            {
                float dy = leaf.Position.Y - door.Position.Y;
                if (dy < -1f || dy > 3.5f || scene.DoorOf(leaf.Path) != null) continue;
                string zone = zoneOf(leaf);
                if (zone == null) continue;
                float distance = Vector2.Distance(new Vector2(leaf.Position.X, leaf.Position.Z), new Vector2(door.Position.X, door.Position.Z));
                if (distance < bestDistance) { best = zone; bestDistance = distance; }
            }
            return best;
        }

        internal static List<string> DoorProblems(Analysis analysis, DoorState state)
        {
            Scene scene = analysis.Scene;
            List<string> problems = new List<string>();
            foreach (LinkInfo exclusion in state.DrivesExclusion)
                problems.Add("Its " + McpScript.ParamName(scene.DoorPins[state.Door.Path]) + " points at " + McpScript.EntityName(scene.Commands, exclusion.Holder, exclusion.Link) + ", a ZoneExclusionLink - always open, so no door can drive it. A door drives a ZoneLink.");
            foreach (string other in state.DrivesOther)
                problems.Add("Its " + McpScript.ParamName(scene.DoorPins[state.Door.Path]) + " points at " + other + ", which is not a ZoneLink.");
            if (state.Joins == null)
            {
                if (state.DeadEnd)
                {
                    //Vanilla's dead-end doors are listed by the zone they open from and drive nothing
                    if (state.ListedBy.Count == 0 && state.Side != null)
                        problems.Add("A dead end (nothing behind it) listed by no zone: list it in " + scene.NameOf(state.Side) + ", the room it opens from.");
                    foreach (LinkInfo link in state.Drives)
                        problems.Add("A dead end (nothing behind it), yet it drives " + McpScript.EntityName(scene.Commands, link.Holder, link.Link)
                            + (link.HalfWired ? ", which is half-wired (" + (link.A == null ? "ZoneA" : "ZoneB") + " leads to no zone)" : " (" + scene.NameOf(link.A) + " - " + scene.NameOf(link.B) + "), so opening and closing it opens and closes that link")
                            + ": a door with nothing behind it needs no link - " + (link.Own ? "link_zones with replace_door_links unhooks it (or delete_entities the link)" : "its pin is wired inside " + link.Holder.name + ", which links from here cannot change") + ", unless there is a room behind after all.");
                }
                else if (state.Side != null)
                {
                    //Both sides are one zone's content (zones merged across it): it belongs to that zone
                    if (state.ListedBy.Count == 0)
                        problems.Add("Both sides of it are " + scene.NameOf(state.Side) + "'s, but no zone lists it: list it in " + scene.NameOf(state.Side) + " (link_zones does).");
                    foreach (LinkInfo link in state.Drives)
                        problems.Add("Both sides of it are " + scene.NameOf(state.Side) + "'s, yet it drives " + McpScript.EntityName(scene.Commands, link.Holder, link.Link) + ": it needs no link" + (link.Own ? " - link_zones with replace_door_links unhooks it." : " (its pin is wired inside " + link.Holder.name + ")."));
                }
                else
                    problems.Add("Fewer than two zones' content lies either side of it, so which zones it joins cannot be told (is the content near it unzoned?).");
                return problems;
            }
            (string a, string b) = state.Joins.Value;
            LinkInfo open = analysis.OpenLinkBetween(a, b);
            //With an always-open link the two zones are loaded together, so the door is drawn whichever lists it (vanilla often lists it once)
            bool listedEnough = open != null ? (state.ListedBy.Contains(a) || state.ListedBy.Contains(b)) : (state.ListedBy.Contains(a) && state.ListedBy.Contains(b));
            if (!listedEnough)
                problems.Add("Not listed by " + (open != null ? "either zone" : "both zones") + " it joins (" + scene.NameOf(a) + ", " + scene.NameOf(b) + "): it is streamed with " + (state.ListedBy.Count == 0 ? "neither" : "only " + string.Join(", ", state.ListedBy.Select(scene.NameOf))) + ".");
            if (state.SeeThrough)
            {
                //A window door's rooms see each other with it shut: vanilla joins them with an open link and the door drives none
                if (open == null)
                    problems.Add("It has a window, so " + scene.NameOf(a) + " and " + scene.NameOf(b) + " see each other with it shut, but no always-open link joins them: the room behind the window unloads whenever the door closes. Join them with a ZoneLink open on reset that the door does not drive (link_zones does).");
                return problems;
            }
            List<LinkInfo> drives = state.Drives.Concat(state.Works).ToList();
            if (drives.Count == 0)
            {
                //Zones an always-open link joins see each other whatever the door does (an opening beside it, say): it needs no link of its own
                if (open == null)
                    problems.Add("Its zone link pin drives nothing, so opening it opens no ZoneLink between " + scene.NameOf(a) + " and " + scene.NameOf(b) + ".");
                return problems;
            }
            bool hasRight = drives.Any(o => o.Joins(a, b));
            foreach (LinkInfo link in drives.Where(o => o.HalfWired))
                problems.Add("It drives " + McpScript.EntityName(scene.Commands, link.Holder, link.Link) + ", which is half-wired (" + (link.A == null ? "ZoneA" : "ZoneB") + " leads to no zone): " + (hasRight
                    ? "it already drives the link between its zones, so this one is extra - link_zones with replace_door_links unhooks it (or create_zone_link with this door and replace_door_link)."
                    : "link_zones or create_zone_link with this door repairs it."));
            if (!drives.Any(o => o.Joins(a, b)) && !drives.All(o => o.HalfWired))
                problems.Add("It drives a link between other zones (" + string.Join("; ", drives.Where(o => !o.HalfWired).Select(o => scene.NameOf(o.A) + " - " + scene.NameOf(o.B))) + ") than the two it stands between (" + scene.NameOf(a) + ", " + scene.NameOf(b) + ").");
            if (state.Drives.Count > 1)
                problems.Add("Its zone link pin drives " + state.Drives.Count + " links; a door drives the one between the zones either side of it" + (hasRight ? " (link_zones with replace_door_links unhooks the others)." : "."));
            return problems;
        }
        #endregion

        #region Choosing content
        /// <summary>
        /// The units of a scene's content inside a region: each top-level entity whose content's middle is inside, or - for one
        /// spread over more than <paramref name="split"/> metres (a set dressing a whole floor, say) - each of its children whose
        /// content's middle is. Doors are left out (each belongs to two rooms), and so is content a zone already claims unless
        /// <paramref name="includeZoned"/>. Paths are the scene's, in order. <paramref name="inside"/> is asked about a unit's path
        /// and the middle of its content, in the composite's own space (<see cref="AreaHas"/> answers for every placement).
        /// </summary>
        internal static List<string> UnitsIn(Scene scene, Func<string, Vector3, bool> inside, bool includeZoned, out int alreadyZoned, float split = 12f)
        {
            alreadyZoned = 0;
            List<string> units = new List<string>();
            Dictionary<string, List<McpZonePlanner.Leaf>> byTop = new Dictionary<string, List<McpZonePlanner.Leaf>>();
            foreach (McpZonePlanner.Leaf leaf in scene.Leaves)
            {
                if (scene.DoorOf(leaf.Path) != null) continue;
                int cut = leaf.Path.IndexOf('/');
                string top = cut < 0 ? leaf.Path : leaf.Path.Substring(0, cut);
                if (!byTop.TryGetValue(top, out List<McpZonePlanner.Leaf> list)) byTop[top] = list = new List<McpZonePlanner.Leaf>();
                list.Add(leaf);
            }
            int zonedCount = 0;
            void Consider(string path, List<McpZonePlanner.Leaf> leaves, int depth)
            {
                Vector3 min = leaves.Select(o => o.Position).Aggregate(Vector3.Min), max = leaves.Select(o => o.Position).Aggregate(Vector3.Max);
                bool mixed = !includeZoned && leaves.Any(o => scene.ClaimsOf(o.Path).Count != 0) && leaves.Any(o => scene.ClaimsOf(o.Path).Count == 0);
                if ((Math.Max(max.X - min.X, max.Z - min.Z) > split && depth < 3) || mixed)
                {
                    //Too big to be in one room, or partly claimed already: its children are placed one by one
                    Dictionary<string, List<McpZonePlanner.Leaf>> children = new Dictionary<string, List<McpZonePlanner.Leaf>>();
                    foreach (McpZonePlanner.Leaf leaf in leaves)
                    {
                        if (leaf.Path.Length == path.Length) continue;
                        int cut = leaf.Path.IndexOf('/', path.Length + 1);
                        string child = cut < 0 ? leaf.Path : leaf.Path.Substring(0, cut);
                        if (!children.TryGetValue(child, out List<McpZonePlanner.Leaf> list)) children[child] = list = new List<McpZonePlanner.Leaf>();
                        list.Add(leaf);
                    }
                    if (children.Count > 1 || (mixed && children.Count == 1))
                    {
                        foreach (KeyValuePair<string, List<McpZonePlanner.Leaf>> child in children.OrderBy(o => o.Key, StringComparer.Ordinal))
                            Consider(child.Key, child.Value, depth + 1);
                        return;
                    }
                }
                if (!inside(path, (min + max) / 2f)) return;
                if (!includeZoned && leaves.Any(o => scene.ClaimsOf(o.Path).Count != 0)) { zonedCount++; return; }
                units.Add(path);
            }
            foreach (KeyValuePair<string, List<McpZonePlanner.Leaf>> top in byTop.OrderBy(o => o.Key, StringComparer.Ordinal))
                Consider(top.Key, top.Value, 1);
            alreadyZoned = zonedCount;
            return units;
        }
        #endregion

        #region Planning links
        /// <summary>One change link_zones or auto_zone makes.</summary>
        internal sealed class LinkAction
        {
            /// <summary>
            /// "door": a ZoneLink the door drives (made, reused, or <see cref="Repair"/>ed), the door listed by both zones;
            /// "open": a ZoneLink open on reset that nothing drives (a see-through door's or an opening's), with <see cref="Door"/> listed by both if given;
            /// "exclusion": a ZoneExclusionLink; "list": only list <see cref="Door"/> in the zones (B null for a dead end's one zone);
            /// "unhook": only take <see cref="Unhook"/> off <see cref="Door"/>'s zone link pin (a dead end driving a link).
            /// </summary>
            public string Kind;
            public string A, B;            //zone labels
            public McpZonePlanner.Door Door;
            /// <summary>A half-wired link the door already drives, to be given the right zones rather than making another.</summary>
            public LinkInfo Repair;
            /// <summary>Links the door drives that it should not, to be unhooked from it (replace_door_links).</summary>
            public List<LinkInfo> Unhook = new List<LinkInfo>();
            /// <summary>Clear everything else off the door's zone link pin (replace_door_links): links it should not drive, or things that are not ZoneLinks.</summary>
            public bool ReplaceDoorLink;
            public string Reason;
        }

        /// <summary>How link_zones and auto_zone decide what to make.</summary>
        internal sealed class LinkOptions
        {
            public bool Doors = true;
            /// <summary>
            /// What joins neighbouring zones with no door between: "open_link" (a ZoneLink open on reset), "exclusion" (a ZoneExclusionLink), or
            /// "none". Vanilla joins zones whose content touches with open ZoneLinks (ChallengeMap4: 10 of 10; ChallengeMap16: 42 of 54) and
            /// never with ZoneExclusionLinks (0 of 7 and 0 of 123, which join zones a median 15-19 m apart).
            /// </summary>
            public string OpenPairs = "open_link";
            public bool AddDoorsToZones = true;
            public bool ReplaceDoorLinks;
            /// <summary>Only pairs touching one of these zones (null: any).</summary>
            public HashSet<string> Touching;
            /// <summary>Only this pair (null: any).</summary>
            public (string a, string b)? Between;
            public HashSet<string> SkipDoors = new HashSet<string>();

            public bool Wanted(string a, string b)
            {
                if (Touching != null && !Touching.Contains(a) && !Touching.Contains(b)) return false;
                if (Between != null && !((Between.Value.a == a && Between.Value.b == b) || (Between.Value.a == b && Between.Value.b == a))) return false;
                return true;
            }
        }

        /// <summary>Whether an 'open' pair (zones that see each other with no door shut between) is joined as it needs: by an always-open link.</summary>
        internal static bool OpenPairJoined(List<LinkInfo> joining) => joining.Any(o => o.State.AlwaysOpen);

        /// <summary>Whether a link joining a pair is worked by script (its methods or open_on_reset are linked): link_zones leaves such a pair as it is.</summary>
        internal static bool ScriptWorked(List<LinkInfo> joining) => joining.Any(o => o.State.Methods.Count != 0 || o.State.OpenFrom.Count != 0);

        /// <summary>
        /// The links the zoning is missing, from an analysis: for each door between two zones a ZoneLink it drives (a see-through
        /// door: an always-open one it does not), the door listed by both; for neighbours with no door between, the always-open
        /// link <see cref="LinkOptions.OpenPairs"/> asks for. <paramref name="isNew"/> marks zones being made in the same step
        /// (auto_zone's): pairs of two existing zones are then left alone. What cannot be done goes in <paramref name="skipped"/>.
        /// </summary>
        internal static List<LinkAction> PlanLinks(Analysis analysis, LinkOptions options, List<string> skipped, Func<string, bool> isNew = null)
        {
            Scene scene = analysis.Scene;
            List<LinkAction> actions = new List<LinkAction>();
            bool Linkable(string label) => scene.Linkable(label) || (isNew != null && isNew(label));
            bool Touches(string a, string b) => isNew == null || isNew(a) || (b != null && isNew(b));
            (string, string) Ordered(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);
            string LinkName(LinkInfo link) => McpScript.EntityName(scene.Commands, link.Holder, link.Link);

            //The pairs a door stands between, and those an always-open link joins or is planned to
            HashSet<(string, string)> doorPairs = new HashSet<(string, string)>();
            foreach (DoorState state in analysis.Doors)
                if (state.Joins != null && !options.SkipDoors.Contains(state.Door.Path))
                    doorPairs.Add(Ordered(state.Joins.Value.a, state.Joins.Value.b));
            Dictionary<(string, string), string> openBy = new Dictionary<(string, string), string>();

            //Neighbours with no always-open link joining them yet. Pairs with a door are left to the door, unless they also meet away
            //from it; a see-through door's pair is left to the door, which plans its own open link
            if (options.OpenPairs != "none")
            {
                foreach (McpZonePlanner.Pair pair in analysis.Pairs)
                {
                    if (pair.Kind != "open" || pair.A == pair.B || !options.Wanted(pair.A, pair.B) || !Touches(pair.A, pair.B)) continue;
                    if (!Linkable(pair.A) || !Linkable(pair.B)) continue;
                    (string, string) key = Ordered(pair.A, pair.B);
                    if (openBy.ContainsKey(key)) continue;
                    bool door = doorPairs.Contains(key);
                    if (door && pair.Reason == "window_door") continue;
                    List<LinkInfo> joining = analysis.Joining(pair.A, pair.B);
                    if (OpenPairJoined(joining)) continue;
                    if (!door && ScriptWorked(joining))
                    {
                        skipped.Add(scene.NameOf(pair.A) + " and " + scene.NameOf(pair.B) + ": their content meets (" + pair.Contacts + " places) with no door between, but only " + string.Join(", ", joining.Select(LinkName)) + " joins them, which the script opens and closes - left as it is. Make it open on reset if the two should never unload apart.");
                        continue;
                    }
                    string why = pair.Reason == "beside_door" ? "they also meet away from the door between them (" + pair.Contacts + " places)"
                        : pair.Reason == "window_wall" ? "a window in the wall between them"
                        : "their content meets (" + pair.Contacts + " places) with no door between";
                    string kind = options.OpenPairs == "open_link" ? "open" : "exclusion";
                    actions.Add(new LinkAction() { Kind = kind, A = pair.A, B = pair.B, Reason = why });
                    openBy[key] = "the " + (kind == "open" ? "open link" : "exclusion link") + " planned between them";
                }
            }

            foreach (DoorState state in analysis.Doors)
            {
                if (options.SkipDoors.Contains(state.Door.Path)) continue;
                List<LinkInfo> half = state.Drives.Where(o => o.HalfWired && o.Own).ToList();
                //What else its pin points at: an exclusion link (no door can drive one) or something that is not a link
                bool strays = state.DrivesExclusion.Any(o => o.Own) || state.DrivesOther.Count != 0;
                string strayText = string.Join(", ", state.DrivesExclusion.Select(LinkName).Concat(state.DrivesOther));
                if (state.Joins == null)
                {
                    if (state.Side == null || !options.Doors || !Linkable(state.Side) || !Touches(state.Side, null) || !options.Wanted(state.Side, state.Side)) continue;
                    //A dead end, or a door with one zone's content both sides, goes in that zone
                    if (state.ListedBy.Count == 0 && options.AddDoorsToZones)
                        actions.Add(new LinkAction() { Kind = "list", A = state.Side, Door = state.Door, Reason = state.DeadEnd ? "a dead end: listed by the zone it opens from" : "both sides of it are this zone's: listed by it" });
                    //...and needs no link: whatever its pin drives is unhooked with replace_door_links
                    List<LinkInfo> needless = state.Drives.Where(o => o.Own).Concat(state.DrivesExclusion.Where(o => o.Own)).ToList();
                    if (needless.Count != 0 || state.DrivesOther.Count != 0)
                    {
                        string what = string.Join(", ", needless.Select(LinkName).Concat(state.DrivesOther));
                        if (options.ReplaceDoorLinks && state.Drives.All(o => o.Own))
                        {
                            LinkAction unhook = new LinkAction() { Kind = "unhook", A = state.Side, Door = state.Door, ReplaceDoorLink = true, Reason = (state.DeadEnd ? "a dead end" : "one zone both sides") + ": it needs no link, so its pin is cleared of " + what };
                            unhook.Unhook.AddRange(needless);
                            actions.Add(unhook);
                        }
                        else
                            skipped.Add(state.Door.Name + ": " + (state.DeadEnd ? "a dead end" : "one zone's content is both sides of it") + ", yet its pin drives " + what + "." + (state.Drives.All(o => o.Own) ? " Pass replace_door_links to unhook it." : ""));
                    }
                    continue;
                }
                (string a, string b) = state.Joins.Value;
                if (a == b || !Touches(a, b) || !options.Doors || !options.Wanted(a, b)) continue;
                if (!Linkable(a) || !Linkable(b))
                {
                    skipped.Add(state.Door.Name + ": joins " + scene.NameOf(a) + " and " + scene.NameOf(b) + ", and " + (scene.ScriptZones.Contains(a) || scene.ScriptZones.Contains(b) ? "one is loaded by script, not a room" : "one of them is inside an instance, out of reach of links here") + ".");
                    continue;
                }
                LinkInfo existingOpen = analysis.OpenLinkBetween(a, b);
                string open = existingOpen != null ? LinkName(existingOpen) : (openBy.TryGetValue(Ordered(a, b), out string planned) ? planned : null);
                //With an always-open link (there or planned) the zones load together, so one listing is enough; otherwise the door goes in both
                bool listed = open != null ? state.ListedBy.Contains(a) || state.ListedBy.Contains(b) : state.ListedBy.Contains(a) && state.ListedBy.Contains(b);
                bool needsListing = !listed && options.AddDoorsToZones;
                string evidence = state.JoinsFrom == "position" && state.ListedBy.Count == 1
                    ? " (it is listed only by " + scene.NameOf(state.ListedBy[0]) + "; the content behind it is " + scene.NameOf(state.ListedBy[0] == a ? b : a) + "'s - skip_doors it if it is kept shut)" : "";

                if (state.SeeThrough)
                {
                    if (open == null)
                    {
                        actions.Add(new LinkAction() { Kind = "open", A = a, B = b, Door = options.AddDoorsToZones ? state.Door : null, Reason = "the door has a window: the rooms see each other with it shut, so they need a link open on reset that the door does not drive" + evidence });
                        openBy[Ordered(a, b)] = "the open link planned for " + state.Door.Name;
                    }
                    else if (needsListing)
                        actions.Add(new LinkAction() { Kind = "list", A = a, B = b, Door = state.Door, Reason = "list the door in both zones (" + open + " keeps them joined)" });
                    continue;
                }

                List<LinkInfo> right = state.Drives.Where(o => o.Joins(a, b)).ToList();
                List<LinkInfo> wrong = state.Drives.Where(o => !o.Joins(a, b) && !o.HalfWired).ToList();
                bool allOwn = state.Drives.All(o => o.Own);
                if (right.Count != 0 || (state.Drives.Count == 0 && state.Works.Any(o => o.Joins(a, b))))
                {
                    //It already drives its link: anything else its pin drives is extra
                    List<LinkInfo> extra = wrong.Concat(half).Concat(right.Skip(1)).ToList();
                    if ((extra.Count != 0 || strays) && options.ReplaceDoorLinks && right.Count != 0 && right[0].Own && allOwn)
                    {
                        LinkAction tidy = new LinkAction() { Kind = "door", A = a, B = b, Door = state.Door, ReplaceDoorLink = true, Reason = "the door already drives " + LinkName(right[0]) + ": its pin is cleared of " + string.Join(", ", extra.Select(LinkName).Concat(state.DrivesExclusion.Select(LinkName)).Concat(state.DrivesOther)) };
                        tidy.Unhook.AddRange(extra);
                        actions.Add(tidy);
                        continue;
                    }
                    if (needsListing) actions.Add(new LinkAction() { Kind = "list", A = a, B = b, Door = state.Door, Reason = "list the door in both zones (it already drives their link)" });
                    if (extra.Count != 0 || strays)
                        skipped.Add(state.Door.Name + ": also drives " + string.Join(", ", extra.Select(o => LinkName(o) + (o.HalfWired ? " (half-wired)" : " (" + scene.NameOf(o.A) + " - " + scene.NameOf(o.B) + ")")).Concat(strayText.Length != 0 ? new[] { strayText } : new string[0])) + ", as well as its own link." + (allOwn ? (options.ReplaceDoorLinks ? "" : " Pass replace_door_links to unhook " + (extra.Count + (strays ? 1 : 0) == 1 ? "it." : "them.")) : " Its pin is wired inside an instance, which links from here cannot change."));
                    continue;
                }
                if (state.Drives.Count == 0 && state.Works.Count == 0 && open != null && !strays)
                {
                    //An always-open link joins them (an opening beside the door): a link the door closes would change nothing
                    if (needsListing)
                        actions.Add(new LinkAction() { Kind = "list", A = a, B = b, Door = state.Door, Reason = "list the door in both zones (" + open + " keeps them joined, so it needs no link of its own)" });
                    continue;
                }
                if ((wrong.Count != 0 || strays) && !options.ReplaceDoorLinks)
                {
                    skipped.Add(state.Door.Name + ": its pin drives " + string.Join(", ", wrong.Select(o => LinkName(o) + " (" + scene.NameOf(o.A) + " - " + scene.NameOf(o.B) + ")").Concat(strayText.Length != 0 ? new[] { strayText + " (not a ZoneLink it can drive)" } : new string[0])) + ", not a link between the zones it stands between (" + scene.NameOf(a) + ", " + scene.NameOf(b) + "). Pass replace_door_links to re-point it.");
                    continue;
                }
                if (!allOwn)
                {
                    skipped.Add(state.Door.Name + ": its pin is wired inside " + string.Join(", ", state.Drives.Where(o => !o.Own).Select(o => o.Holder.name).Distinct()) + ", which links from here cannot change.");
                    continue;
                }
                LinkAction action = new LinkAction() { Kind = "door", A = a, B = b, Door = state.Door };
                action.Unhook.AddRange(wrong);
                action.Unhook.AddRange(half.Skip(1));
                action.ReplaceDoorLink = wrong.Count != 0 || strays || half.Count > 1;
                if (half.Count != 0)
                    action.Reason = "repair " + LinkName(half[0]) + ", which the door drives but is half-wired" + (action.ReplaceDoorLink ? ", and clear the rest off its pin" : "");
                else if (action.ReplaceDoorLink)
                    action.Reason = "re-point the door at a link between the zones it stands between";
                else
                    action.Reason = "a ZoneLink for the door to drive" + evidence;
                action.Repair = half.FirstOrDefault();
                actions.Add(action);
            }
            return actions;
        }
        #endregion

        #region Auto zoning
        /// <summary>auto_zone's plan: the new zones (numbered buckets), what each lists, and the links to make.</summary>
        internal sealed class AutoPlan
        {
            /// <summary>Each entry path handed out, and the bucket it goes in.</summary>
            public Dictionary<string, int> Buckets = new Dictionary<string, int>();
            /// <summary>The buckets in the plan's order (west to east, then north to south).</summary>
            public List<int> Order = new List<int>();
            public Dictionary<int, List<string>> Members = new Dictionary<int, List<string>>();
            /// <summary>The doors each new zone lists as well (a door goes in both zones it joins).</summary>
            public Dictionary<int, List<McpZonePlanner.Door>> DoorsOf = new Dictionary<int, List<McpZonePlanner.Door>>();
            public Dictionary<int, List<McpZonePlanner.Leaf>> Content = new Dictionary<int, List<McpZonePlanner.Leaf>>();
            /// <summary>The links touching a new zone, as link_zones would make them once the zones exist.</summary>
            public List<LinkAction> Links = new List<LinkAction>();
            public List<string> Skipped = new List<string>();
            public Func<McpZonePlanner.Leaf, string> ZoneOf;
            public Analysis Analysis;
            /// <summary>Unzoned content the plan does not hand out.</summary>
            public List<McpZonePlanner.Leaf> LeftOut = new List<McpZonePlanner.Leaf>();
            /// <summary>Unzoned content lying among an existing zone's (by the partition), to be added to it: entries by zone label.</summary>
            public Dictionary<string, List<string>> Extend = new Dictionary<string, List<string>>();
            public Dictionary<string, List<McpZonePlanner.Leaf>> ExtendContent = new Dictionary<string, List<McpZonePlanner.Leaf>>();
            /// <summary>Unzoned content lying among an existing zone's, left out because extending was turned off.</summary>
            public int NearExisting;
            /// <summary>A hash of what the plan hands out, for checking at apply time that the plan is the one shown.</summary>
            public string Id;
        }

        internal static string NewLabel(int bucket) => "new:" + bucket;

        internal static int NewBucket(string label) => label != null && label.StartsWith("new:") ? int.Parse(label.Substring(4)) : -1;

        /// <summary>
        /// Plan zones for a scene's unzoned content. <see cref="McpZonePlanner.Geometry.Partition"/> splits the whole scene into
        /// buckets cut at doors and walls; unzoned content in a bucket that is mostly one existing zone's goes to that zone (with
        /// <paramref name="extendExisting"/>), and the rest makes new zones, one per bucket. Each door goes in both zones it joins,
        /// and links are planned for every door and neighbouring pair touching a new zone. Content already zoned stays where it is.
        /// <paramref name="merge"/> and <paramref name="drop"/> edit the plan by zone number (1-based, in plan order).
        /// </summary>
        internal static AutoPlan PlanAutoZones(Commands commands, Scene scene, LinkOptions options, List<List<int>> merge = null, List<int> drop = null, bool extendExisting = true)
        {
            AutoPlan plan = new AutoPlan();
            //Every prefix with claimed content under it: an entry there would claim that content twice
            HashSet<string> mixed = new HashSet<string>();
            List<McpZonePlanner.Leaf> free = new List<McpZonePlanner.Leaf>();
            foreach (McpZonePlanner.Leaf leaf in scene.Leaves)
            {
                if (scene.DoorOf(leaf.Path) != null) continue;
                if (scene.ClaimsOf(leaf.Path).Count == 0) { free.Add(leaf); continue; }
                int cut = -1;
                do
                {
                    cut = leaf.Path.IndexOf('/', cut + 1);
                    mixed.Add(cut < 0 ? leaf.Path : leaf.Path.Substring(0, cut));
                } while (cut >= 0);
            }
            if (free.Count == 0)
                return plan;

            //The whole scene partitioned, so unzoned content is judged among its zoned surroundings
            Dictionary<string, int> partition = scene.Geometry.Partition();
            Dictionary<int, int> freeIn = new Dictionary<int, int>();
            Dictionary<int, Dictionary<string, int>> zonedIn = new Dictionary<int, Dictionary<string, int>>();
            foreach (McpZonePlanner.Leaf leaf in scene.Leaves)
            {
                if (scene.DoorOf(leaf.Path) != null) continue;
                int bucket = BucketOf(partition, leaf.Path, out string _);
                if (bucket < 0) continue;
                List<string> claims = scene.ClaimsOf(leaf.Path);
                if (claims.Count == 0) { freeIn[bucket] = freeIn.TryGetValue(bucket, out int n) ? n + 1 : 1; continue; }
                if (claims.Count != 1) continue;
                if (!zonedIn.TryGetValue(bucket, out Dictionary<string, int> counts)) zonedIn[bucket] = counts = new Dictionary<string, int>();
                counts[claims[0]] = counts.TryGetValue(claims[0], out int m) ? m + 1 : 1;
            }
            //A bucket is an existing zone's when that zone has at least as much content in it as is unzoned there
            Dictionary<int, string> owner = new Dictionary<int, string>();
            foreach (KeyValuePair<int, Dictionary<string, int>> pair in zonedIn)
            {
                KeyValuePair<string, int> top = pair.Value.OrderByDescending(o => o.Value).ThenBy(o => o.Key, StringComparer.Ordinal).First();
                if (top.Value >= (freeIn.TryGetValue(pair.Key, out int n) ? n : 0) && scene.Linkable(top.Key))
                    owner[pair.Key] = top.Key;
            }

            Dictionary<string, int> found = new Dictionary<string, int>();
            Dictionary<string, string> extend = new Dictionary<string, string>();
            HashSet<McpZonePlanner.Leaf> leftOut = new HashSet<McpZonePlanner.Leaf>();
            foreach (McpZonePlanner.Leaf leaf in free)
            {
                int bucket = BucketOf(partition, leaf.Path, out string unit);
                if (bucket < 0) { leftOut.Add(leaf); continue; }
                //A unit with claimed content under it is handed out by its largest wholly free parts instead
                string entry = unit;
                if (mixed.Contains(unit))
                {
                    int cut = unit.Length;
                    do
                    {
                        cut = leaf.Path.IndexOf('/', cut + 1);
                        entry = cut < 0 ? leaf.Path : leaf.Path.Substring(0, cut);
                    } while (mixed.Contains(entry) && cut >= 0);
                }
                if (owner.TryGetValue(bucket, out string zone))
                {
                    if (extendExisting) extend[entry] = zone;
                    else { leftOut.Add(leaf); plan.NearExisting++; }
                }
                else
                    found[entry] = bucket;
            }

            //Number the new zones' buckets in plan order, then apply the caller's edits
            Dictionary<int, List<McpZonePlanner.Leaf>> content = new Dictionary<int, List<McpZonePlanner.Leaf>>();
            foreach (McpZonePlanner.Leaf leaf in free)
            {
                if (leftOut.Contains(leaf)) continue;
                int bucket = BucketOf(found, leaf.Path, out string _);
                if (bucket >= 0)
                {
                    if (!content.TryGetValue(bucket, out List<McpZonePlanner.Leaf> list)) content[bucket] = list = new List<McpZonePlanner.Leaf>();
                    list.Add(leaf);
                    continue;
                }
                string zone = BucketOf(extend, leaf.Path);
                if (zone == null) continue;
                if (!plan.ExtendContent.TryGetValue(zone, out List<McpZonePlanner.Leaf> grown)) plan.ExtendContent[zone] = grown = new List<McpZonePlanner.Leaf>();
                grown.Add(leaf);
            }
            foreach (KeyValuePair<string, string> entry in extend.OrderBy(o => o.Key, StringComparer.Ordinal))
            {
                if (!plan.Extend.TryGetValue(entry.Value, out List<string> list)) plan.Extend[entry.Value] = list = new List<string>();
                list.Add(entry.Key);
            }
            List<int> order = content.Keys
                .OrderBy(o => Math.Round(content[o].Average(p => p.Position.X)))
                .ThenBy(o => Math.Round(content[o].Average(p => p.Position.Z)))
                .ThenBy(o => o).ToList();
            Dictionary<int, int> number = new Dictionary<int, int>();
            for (int i = 0; i < order.Count; i++) number[order[i]] = i + 1;

            //What the plan hands out before the caller's edits, hashed: a dry run's plan_id still holds when merge/drop come with the apply
            ulong hash = 14695981039346656037UL;
            void Mix(string text) { foreach (char c in text) { hash ^= c; hash *= 1099511628211UL; } hash ^= 0xFF; hash *= 1099511628211UL; }
            foreach (int bucket in order)
            {
                Mix("#" + number[bucket]);
                foreach (string member in found.Where(o => o.Value == bucket).Select(o => o.Key).OrderBy(o => o, StringComparer.Ordinal)) Mix(member);
            }
            foreach (KeyValuePair<string, string> grown in extend.OrderBy(o => o.Key, StringComparer.Ordinal)) Mix("+" + grown.Value + " " + grown.Key);
            plan.Id = hash.ToString("x16");

            Dictionary<int, int> into = order.ToDictionary(o => number[o], o => number[o]);
            if (merge != null)
                foreach (List<int> group in merge)
                {
                    foreach (int n in group)
                        if (!into.ContainsKey(n)) throw new McpError("'merge' names zone " + n + ", but the plan has zones 1 to " + order.Count + ".");
                    int target = into[group[0]];
                    foreach (int n in group)
                        foreach (int k in into.Keys.Where(o => into[o] == into[n]).ToList()) into[k] = target;
                }
            HashSet<int> dropped = new HashSet<int>();
            if (drop != null)
                foreach (int n in drop)
                {
                    if (!into.ContainsKey(n)) throw new McpError("'drop' names zone " + n + ", but the plan has zones 1 to " + order.Count + ".");
                    dropped.Add(into[n]);
                }
            foreach (KeyValuePair<string, int> entry in found)
            {
                int n = into[number[entry.Value]];
                if (dropped.Contains(n)) continue;
                plan.Buckets[entry.Key] = n;
            }
            foreach (McpZonePlanner.Leaf leaf in free)
            {
                if (leftOut.Contains(leaf)) { plan.LeftOut.Add(leaf); continue; }
                int bucket = BucketOf(plan.Buckets, leaf.Path, out string _);
                if (bucket < 0) continue; //in a dropped zone, or going to an existing one
                if (!plan.Content.TryGetValue(bucket, out List<McpZonePlanner.Leaf> list)) plan.Content[bucket] = list = new List<McpZonePlanner.Leaf>();
                list.Add(leaf);
            }
            plan.Order = plan.Content.Keys.OrderBy(o => o).ToList();
            foreach (int bucket in plan.Order)
            {
                plan.Members[bucket] = plan.Buckets.Where(o => o.Value == bucket).Select(o => o.Key).OrderBy(o => o, StringComparer.Ordinal).ToList();
                plan.DoorsOf[bucket] = new List<McpZonePlanner.Door>();
            }

            plan.ZoneOf = leaf =>
            {
                string existing = scene.ZoneOf(leaf);
                if (existing != null) return existing;
                int bucket = BucketOf(plan.Buckets, leaf.Path, out string _);
                if (bucket >= 0) return NewLabel(bucket);
                return scene.ClaimsOf(leaf.Path).Count == 0 ? BucketOf(extend, leaf.Path) : null;
            };

            //Doors and links as link_zones would plan them once the zones exist; a door joining a new zone is listed by it
            plan.Analysis = Analyse(commands, scene, plan.ZoneOf);
            plan.Links = PlanLinks(plan.Analysis, options, plan.Skipped, label => NewBucket(label) >= 0);
            //Every door goes in the new zones either side of it (a dead end, or one with a zone both sides, in that zone), linked or not
            if (options.AddDoorsToZones)
                foreach (DoorState state in plan.Analysis.Doors)
                {
                    if (options.SkipDoors.Contains(state.Door.Path)) continue;
                    string[] sides = state.Joins != null ? new[] { state.Joins.Value.a, state.Joins.Value.b }
                        : state.ListedBy.Count == 0 ? new[] { state.Side ?? (state.DeadEnd ? null : NearestZone(scene, state.Door, plan.ZoneOf)) } : new string[0];
                    foreach (string side in sides)
                        if (NewBucket(side) >= 0 && plan.DoorsOf.TryGetValue(NewBucket(side), out List<McpZonePlanner.Door> list) && !list.Contains(state.Door)) list.Add(state.Door);
                }

            return plan;
        }

        /// <summary>The value at a path's longest listed prefix, or null.</summary>
        internal static string BucketOf(Dictionary<string, string> buckets, string path)
        {
            int cut = path.Length;
            while (true)
            {
                string prefix = cut == path.Length ? path : path.Substring(0, cut);
                if (buckets.TryGetValue(prefix, out string value)) return value;
                cut = path.LastIndexOf('/', cut - 1);
                if (cut <= 0) return null;
            }
        }

        /// <summary>The bucket of a path's longest listed prefix (and that prefix), or -1.</summary>
        internal static int BucketOf(Dictionary<string, int> buckets, string path, out string unit)
        {
            int cut = path.Length;
            while (true)
            {
                string prefix = cut == path.Length ? path : path.Substring(0, cut);
                if (buckets.TryGetValue(prefix, out int bucket)) { unit = prefix; return bucket; }
                cut = path.LastIndexOf('/', cut - 1);
                if (cut <= 0) { unit = null; return -1; }
            }
        }
        #endregion
    }
}

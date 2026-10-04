using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// Zones and the ZoneLinks between them: reading back how they are wired, making them, and finding out why
    /// part of a level pops in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A Zone claims part of the level through its 'composites' pin: Zone.composites -> TriggerSequence.reference,
    /// the sequence's entries naming instances and functions, each with everything inside it. Save &amp; Build
    /// writes each claimed model's zone onto its mover, and each piece of collision's onto COLLISION.MAP, which is
    /// how the game tells which zone the player is standing in; whatever no zone claims goes in the global zone.
    /// </para>
    /// <para>
    /// A ZoneLink is a gate (GateInterface: open, close, lock, unlock, open_on_reset) between two zones, named by
    /// its ZoneA and ZoneB pointer pins, each linked to a Zone's reference pin (ZoneLink.ZoneA -> Zone.reference).
    /// While it is open the game draws and streams each zone from the other. A Door drives one through its own
    /// zone_link pin (Door.zone_link -> ZoneLink.reference; a door composite hands it in through an
    /// input_zone_link pin), opening it with the door. Links only join entities of one composite, so a ZoneLink
    /// only reaches zones in its own composite. A door deeper down is reached through an alias: links from an
    /// alias are added to the entity it points at when the level is instanced.
    /// </para>
    /// </remarks>
    internal static partial class McpZoneTools
    {
        private static readonly JObject PathSchema = new JObject()
        {
            ["description"] = "Entity ids/names through instances to the entity, as an array (or one string split on '/').",
            ["anyOf"] = new JArray(new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "string" } }, new JObject() { ["type"] = "string" }),
        };

        private static readonly JObject RegionSchema = new JObject()
        {
            ["type"] = "object",
            ["properties"] = new JObject()
            {
                ["box_min"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" } },
                ["box_max"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" } },
                ["near"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" } },
                ["radius"] = new JObject() { ["type"] = "number" },
            },
        };

        private const string CompositeHelp = "The composite holding the zones (path or id; 'root' for the level's root). Default: the one under the root that holds the zones and most of the placed content (a vanilla level's ENVIRONMENT composite).";

        /// <summary>A region of the level from a {box_min, box_max} or {near, radius} argument, as a test on world positions.</summary>
        private static Func<Vector3, bool> ReadRegion(JObject region, string what)
        {
            bool box = region["box_min"] != null || region["box_max"] != null, near = region["near"] != null || region["radius"] != null;
            if (box == near)
                throw new McpError("'" + what + "' is {\"box_min\": [x,y,z], \"box_max\": [x,y,z]} or {\"near\": [x,y,z], \"radius\": metres}.");
            if (box)
            {
                if (region["box_min"] == null || region["box_max"] == null) throw new McpError("'" + what + "' needs both box_min and box_max.");
                Vector3 a = McpValues.ReadVector(region["box_min"], what + ".box_min", null), b = McpValues.ReadVector(region["box_max"], what + ".box_max", null);
                Vector3 min = Vector3.Min(a, b), max = Vector3.Max(a, b);
                return p => p.X >= min.X && p.Y >= min.Y && p.Z >= min.Z && p.X <= max.X && p.Y <= max.Y && p.Z <= max.Z;
            }
            if (region["near"] == null || region["radius"] == null) throw new McpError("'" + what + "' needs both near and radius.");
            Vector3 centre = McpValues.ReadVector(region["near"], what + ".near", null);
            double radius = McpValues.ReadDouble(region["radius"], what + ".radius");
            if (radius <= 0) throw new McpError("'" + what + ".radius' must be more than zero.");
            return p => Vector3.Distance(p, centre) <= radius;
        }

        /// <summary>The unzoned content of a composite inside a region, as entry paths. UI thread.</summary>
        private static List<ShortGuid[]> ContentIn(McpCall call, Commands commands, Composite composite, JObject region, string what, List<string> notes)
        {
            Func<Vector3, bool> inside = ReadRegion(region, what);
            Scene scene = BuildScene(commands, composite, call.Cancel);
            List<string> units = UnitsIn(scene, inside, false, out int zoned);
            if (zoned != 0) notes.Add(zoned + " entit" + (zoned == 1 ? "y" : "ies") + " in the region already belong to a zone and were left there (name them in 'add' with on_claimed 'move' to take them over).");
            if (units.Count == 0) notes.Add("Nothing unzoned of " + composite.name + " is placed in that region.");
            if (scene.Truncated) notes.Add("The walk of " + composite.name + " stopped early (it is very large): some content in the region may have been missed.");
            return units.Select(PathOf).ToList();
        }

        private const string BuildNote = "Zone membership reaches the game at Save & Build (save_level with build=true): it is written onto each model and onto the collision the game uses to tell which zone the player is in.";

        /// <summary>The composite a zone tool works in: the one named, or the level's zoning composite.</summary>
        private static Composite ZoningComposite(McpCall call, Commands commands)
        {
            return call.Has("composite") ? McpScript.FindComposite(commands, call.Str("composite")) : DefaultZoningComposite(commands);
        }

        /// <summary>The composite on screen keeps its links in its pages until they are compiled: compile them before reading.</summary>
        private static void CompileShown()
        {
            Composite shown = Singleton.Editor?.CompositeDisplay?.Composite;
            if (shown != null) McpBrowseTools.CompileIfShown(shown);
        }

        //What root held when the level was loaded (or when the tools were first asked for): content under those entities was left
        //in the global zone by the level itself (mission, character and UI content), so only what was put in root since is a problem
        private static readonly ConditionalWeakTable<Commands, HashSet<ShortGuid>> _rootAtLoad = new ConditionalWeakTable<Commands, HashSet<ShortGuid>>();

        static McpZoneTools()
        {
            try
            {
                Singleton.OnLevelLoaded += content =>
                {
                    try { Commands loaded = content?.Level?.Commands; if (loaded != null) RootAtLoad(loaded); }
                    catch (Exception) { }
                };
                Commands open = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;
                if (open != null) RootAtLoad(open);
            }
            catch (Exception) { }
        }

        private static HashSet<ShortGuid> RootAtLoad(Commands commands) => _rootAtLoad.GetValue(commands, loaded =>
        {
            Composite root = loaded.EntryPoints?.FirstOrDefault();
            return root == null ? new HashSet<ShortGuid>() : new HashSet<ShortGuid>(root.GetEntities().Select(o => o.shortGUID));
        });

        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "get_zone_links",
                Title = "Get zone links",
                Description = "The level's ZoneLinks (and ZoneExclusionLinks): the two zones each joins, whether it is open or locked on reset, what opens it (a door's zone_link pin, or links to its open/close/lock/unlock methods), and the problems with how it is wired. " +
                    "A ZoneLink is a gate between two zones: while it is open the game draws and streams each zone from the other; while it is closed, or missing, the room on the far side of a doorway is not drawn and pops in as the player crosses. " +
                    "check_zones looks at one area (a doorway, say); create_zone_link makes one or repairs a half-wired one; analyse_zones shows every door and pair at once.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("zone", "Only links touching a zone whose name contains this (or whose id it is)."),
                    McpSchema.String("composite", "Only links in this composite (path or id)."),
                    McpSchema.Boolean("problems_only", "Only links with problems."),
                    McpSchema.Integer("limit", "At most this many (default 100).")),
                ReadOnly = true,
                Idempotent = true,
                Run = GetZoneLinks,
            };

            yield return new McpTool()
            {
                Name = "create_zone",
                Title = "Create zone",
                Description = "Make a Zone - a part of the level the game streams and draws as one - claiming the content given: instances of room geometry, models, lights, collision (each entry with everything inside it). " +
                    "The Zone (suspend_on_unload on, as vanilla zones have it), a TriggerSequence listing the content, and the link between them (Zone.composites -> sequence.reference) are made as one undo step. " +
                    "A name in 'contents' that is not an entity of the composite itself is looked for under it: an instance called that, or one of a composite called that (\"the room called X\"). " +
                    "Make it in the composite that holds the zones it will be linked to: a ZoneLink only joins zones in its own composite. With link, its doors and neighbours are linked in the same step; otherwise create_zone_link (door 'auto') or link_zones does it after. " + BuildNote,
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", CompositeHelp),
                    McpSchema.String("name", "Its name.", required: true),
                    McpSchema.Array("contents", "What it claims: entity paths from 'composite' (or from the level root with from_root), or names - e.g. [\"Canteen_Room\"] for the instance of a room called that.", PathSchema),
                    McpSchema.Nested("region", "Also claim the content of 'composite' placed in this part of the level: {\"box_min\": [x,y,z], \"box_max\": [x,y,z]} or {\"near\": [x,y,z], \"radius\": metres}, in world positions. Each top-level entity whose content's middle is inside is claimed (a large one spread across rooms is taken child by child); doors are left out, and content another zone claims is skipped.", RegionSchema),
                    McpSchema.Boolean("from_root", "The content paths start at the level's root composite (they are rewritten to start at 'composite', as the build reads them)."),
                    McpSchema.String("on_claimed", "Content another zone of 'composite' already claims: 'share' (default: claim it too, and say so) or 'move' (take it out of the other zones in the same step).", options: new[] { "share", "move" }),
                    McpSchema.Boolean("link", "Also link it: a ZoneLink for each door between it and another zone (the door listed by both), and an always-open link to each neighbour it meets with no door between."),
                    McpSchema.Map("parameters", "Zone parameters, e.g. suspend_on_unload, space_visible, force_visible_on_load (describe_function_type Zone lists them)."),
                    McpSchema.String("page", "The flowgraph page the new nodes go on (default 'Zones').")),
                Run = CreateZone,
            };

            yield return new McpTool()
            {
                Name = "set_zone_contents",
                Title = "Set zone contents",
                Description = "Change what a Zone claims: 'add' and 'remove' entity paths (instances or functions; each brings everything inside it). Edits the TriggerSequence the zone's 'composites' pin lists, making and linking one if it has none. " +
                    "Every model, light and piece of collision of a room belongs in that room's zone: content no zone claims is in the global zone, and collision in no zone leaves the player in no room's zone while standing on it. " +
                    "Something two zones claim belongs to both - right for a doorframe shared by two rooms, otherwise it is drawn whenever either is (on_claimed 'move' takes it from the other zone). One undo step. " + BuildNote,
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", CompositeHelp),
                    McpSchema.String("zone", "The Zone (id or name).", required: true),
                    McpSchema.Array("add", "Entity paths (or names) to claim, from 'composite' (or from the root with from_root).", PathSchema),
                    McpSchema.Nested("add_region", "Also claim the unzoned content placed in this part of the level, as create_zone's region: {\"box_min\", \"box_max\"} or {\"near\", \"radius\"} in world positions.", RegionSchema),
                    McpSchema.Array("remove", "Entity paths to stop claiming (every entry naming that entity goes).", PathSchema),
                    McpSchema.Boolean("from_root", "The paths start at the level's root composite."),
                    McpSchema.String("on_claimed", "Added content another zone already claims: 'share' (default) or 'move' (take it out of the other zones in the same step).", options: new[] { "share", "move" }),
                    McpSchema.String("page", "The flowgraph page new nodes go on (default 'Zones').")),
                Run = SetZoneContents,
            };

            yield return new McpTool()
            {
                Name = "create_zone_link",
                Title = "Create zone link",
                Description = "Join two zones, wired as the game expects: ZoneLink.ZoneA -> zone A's reference and ZoneLink.ZoneB -> zone B's reference. " +
                    "kind 'link' (default) makes a ZoneLink, the gate for a doorway: with 'door' (the door variant instance between the rooms, e.g. an AYZ\\Doors\\Door_SML instance - its zonelink pin is wired to the link, so the door opens and closes it, and the door is listed by both zones); door 'auto' finds the door(s) between the two zones and wires each (a door with a window gets a link open on reset that it does not drive, as vanilla does); without a door the link is made open on reset, for an open archway. " +
                    "kind 'exclusion' makes a ZoneExclusionLink, always open: vanilla levels use these between zones further apart (never between zones whose content touches), and join touching neighbours with a ZoneLink open on reset (as this tool does without a door). " +
                    "An existing link between the two zones is reused when it suits (for a door: one this door drives, or one nothing works - each door has its own link); a half-wired link the door already drives is repaired rather than another made. " +
                    "Both zones must be in 'composite': a link only reaches zones in its own composite. The door may be deeper: give its path and an alias is made to reach it. " +
                    "One undo step; returns the link as get_zone_links describes it. link_zones does this for every door and neighbouring pair at once. Then save_level with build=true.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite holding both zones; the ZoneLink goes here. " + CompositeHelp.Substring(CompositeHelp.IndexOf("Default", StringComparison.Ordinal))),
                    McpSchema.String("zone_a", "One zone: a Zone entity of 'composite' (id or name), or one of its zone pins.", required: true),
                    McpSchema.String("zone_b", "The other zone.", required: true),
                    McpSchema.Nested("door", "The door between them: a path from 'composite' through instances to a door variant instance (one with a zone_link pin) or a Door function, a single name/id for one in 'composite' itself - or \"auto\" to find the doors between the two zones. A door with a window gets a ZoneLink open on reset that it does not drive (as vanilla has it) unless replace_door_link is passed.", PathSchema),
                    McpSchema.Boolean("open_on_reset", "Open the link when the level starts (default: true with no door, otherwise left to the door)."),
                    McpSchema.Boolean("lock_on_reset", "Lock the link when the level starts (not for a door with a window, whose link stays open)."),
                    McpSchema.Integer("cost", "The link's cost (default 6)."),
                    McpSchema.String("kind", "'link' (default): a ZoneLink - a gate a door or script opens and closes, or one open on reset for an open doorway. 'exclusion': a ZoneExclusionLink - always open; vanilla levels use these between zones further apart (never between zones whose content touches), and join touching neighbours with a ZoneLink open on reset.", options: new[] { "link", "exclusion" }),
                    McpSchema.Boolean("add_door_to_zones", "With 'door': also list the door in both zones' contents, so it is streamed with either room (default true)."),
                    McpSchema.Boolean("exclude_streaming", "kind 'exclusion' only: the link's exclude_streaming parameter."),
                    McpSchema.String("name", "Its name (default ZoneLink_<zone a>_<zone b>, or ZoneExclusionLink_...)."),
                    McpSchema.Boolean("replace_door_link", "If the door's zone link pin already drives a ZoneLink between other zones, point it at this one instead (a door drives the one link between the zones either side of it)."),
                    McpSchema.String("page", "The flowgraph page new nodes go on (default 'Zones').")),
                Run = CreateZoneLink,
            };

            yield return new McpTool()
            {
                Name = "delete_zone",
                Title = "Delete zone",
                Description = "Delete Zones cleanly, as one undo step: each Zone, the TriggerSequence listing its content (unless something else uses it), and the ZoneLinks and ZoneExclusionLinks that join it to another zone (doors driving them are unhooked). " +
                    "Deleting a zone with delete_entities instead leaves its list and half-wired links behind. What it claimed goes back to the global zone at the next Save & Build. dry_run lists what would go.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", CompositeHelp),
                    McpSchema.Strings("zones", "The zones to delete (names or ids).", required: true),
                    McpSchema.String("links", "'delete' (default): delete the links joining them; 'keep': leave those links (half-wired).", options: new[] { "delete", "keep" }),
                    McpSchema.Boolean("dry_run", "Only say what would be deleted.")),
                Run = DeleteZone,
            };

            yield return new McpTool()
            {
                Name = "check_zones",
                Title = "Check zones in an area",
                Description = "Find why part of a level pops in - around a doorway between two rooms, say. For the models, lights, effects and collision placed in the area (near + radius, or box_min + box_max, in world positions): the zones that claim them and what no zone claims; " +
                    "the links joining those zones and the problems with them; neighbouring zones here that nothing joins; and the doors here - which zones each stands between, what lists it, what its zone_link drives, and what is wrong (a dead-end door, or one with a window, needs no link of its own). " +
                    "Membership is worked out from the script as the viewport's zone overlay does; the game sees it once the level is saved with build=true.",
                InputSchema = McpSchema.Object(
                    McpSchema.Vector("near", "Look within 'radius' metres of this world position (a doorway's, say)."),
                    McpSchema.Number("radius", "Metres, for 'near'."),
                    McpSchema.Vector("box_min", "Or look inside the box from box_min to box_max."),
                    McpSchema.Vector("box_max", "The box's other corner."),
                    McpSchema.String("composite", "The composite whose zones to check (path or id). Default: the one whose zones claim most of what is in the area."),
                    McpSchema.Integer("limit", "At most this many examples per list (default 15).")),
                ReadOnly = true,
                Idempotent = true,
                Run = CheckZones,
            };

            foreach (McpTool tool in PlanTools())
                yield return tool;
        }

        #region Reading zones and links
        /// <summary>For a zone pin of a composite: what each placement of the composite links to it, by name.</summary>
        private static List<string> PinFeeds(Commands commands, Composite composite, VariableEntity pin)
        {
            List<string> fed = new List<string>();
            foreach (Composite other in commands.Entries)
            {
                if (other == null) continue;
                foreach (FunctionEntity instance in other.functions)
                {
                    if (instance.function != composite.shortGUID) continue;
                    foreach (EntityConnector link in instance.childLinks)
                    {
                        if (link.thisParamID != pin.name && link.thisParamID != pin.shortGUID) continue;
                        Entity target = other.GetEntityByID(link.linkedEntityID);
                        fed.Add((target == null ? McpScript.Id(link.linkedEntityID) + " (missing)" : McpScript.EntityName(commands, other, target)) + " in " + other.name + " (where " + McpScript.EntityName(commands, other, instance) + " is placed)");
                    }
                }
            }
            return fed;
        }

        /// <summary>What a ZoneLink's ZoneA or ZoneB pin is linked to; <paramref name="zone"/> is the zone it stands for (null if none).</summary>
        private static JObject DescribeSide(Commands commands, Composite composite, Entity link, ShortGuid pin, List<string> problems, out Entity zone)
        {
            zone = null;
            string pinName = McpScript.ParamName(pin);
            string linkName = McpScript.EntityName(commands, composite, link);
            List<EntityConnector> links = link.childLinks.Where(o => o.thisParamID == pin).ToList();
            if (links.Count == 0)
            {
                problems.Add(pinName + " is not linked to a zone (link " + linkName + "." + pinName + " -> <the Zone>.reference): the link is half-wired and joins nothing.");
                return null;
            }
            if (links.Count > 1)
                problems.Add(pinName + " has " + links.Count + " links; it takes one zone.");

            Entity target = composite.GetEntityByID(links[0].linkedEntityID);
            if (target == null)
            {
                problems.Add(pinName + " is linked to an entity that is no longer in " + composite.name + " (" + McpScript.Id(links[0].linkedEntityID) + ").");
                return new JObject() { ["id"] = McpScript.Id(links[0].linkedEntityID), ["missing"] = true };
            }
            string targetName = McpScript.EntityName(commands, composite, target);
            JObject described = new JObject()
            {
                ["id"] = McpScript.Id(target.shortGUID),
                ["name"] = targetName,
                ["type"] = McpScript.TypeName(commands, composite, target),
            };
            //A pin variable of the composite is linked to by its own name (ZoneLink.ZoneA -> zone_top.zone_top); anything else by its reference
            ShortGuid expected = target is VariableEntity pinVariable ? pinVariable.name : ShortGuids.reference;
            if (links[0].linkedParamID != expected && links[0].linkedParamID != ShortGuids.reference)
                problems.Add(pinName + " is linked to '" + McpScript.ParamName(links[0].linkedParamID) + "' on " + targetName + "; it should be linked to its '" + McpScript.ParamName(expected) + "' pin.");

            if (IsZone(target))
                zone = target;
            else if (target is VariableEntity variable && variable.type == DataType.ZONE)
            {
                zone = target;
                List<string> fed = PinFeeds(commands, composite, variable);
                described["note"] = "A zone pin of " + composite.name + ": the zone comes from whatever is linked to that pin where the composite is placed.";
                if (fed.Count != 0) described["fed_by"] = new JArray(fed.Take(5));
                else problems.Add(pinName + " stands for the zone pin " + targetName + ", but no placement of " + composite.name + " links a zone to that pin, so this side joins nothing.");
            }
            else if (target is AliasEntity || target is ProxyEntity)
            {
                (Composite targetComposite, Entity resolved) = commands.Utils.GetResolvedTarget(target is AliasEntity alias ? commands.Utils.ResolveAlias(alias, composite) : commands.Utils.ResolveProxy((ProxyEntity)target));
                if (IsZone(resolved))
                {
                    zone = resolved;
                    described["stands_for"] = McpScript.EntityName(commands, targetComposite, resolved) + " in " + targetComposite.name;
                }
                else
                    problems.Add(pinName + " is linked to " + targetName + ", which does not stand for a Zone.");
            }
            else
                problems.Add(pinName + " is linked to " + targetName + " (" + McpScript.TypeName(commands, composite, target) + "), which is not a Zone.");
            return described;
        }

        /// <summary>
        /// A ZoneLink or ZoneExclusionLink as get_zone_links reports it: the zones it joins (out), its state, what
        /// drives it, and what is wrong with it. <paramref name="placed"/>, when given, is the composites the level places.
        /// </summary>
        private static JObject DescribeLink(Commands commands, Composite composite, FunctionEntity link, out Entity zoneA, out Entity zoneB, out List<string> problems, HashSet<Composite> placed = null, Incoming incoming = null)
        {
            problems = new List<string>();
            bool exclusion = link.function == FunctionType.ZoneExclusionLink;
            string linkName = McpScript.EntityName(commands, composite, link);
            JObject result = new JObject()
            {
                ["composite"] = composite.name,
                ["id"] = McpScript.Id(link.shortGUID),
                ["name"] = linkName,
                ["type"] = exclusion ? "ZoneExclusionLink" : "ZoneLink",
            };
            JObject a = DescribeSide(commands, composite, link, ZoneA, problems, out zoneA);
            JObject b = DescribeSide(commands, composite, link, ZoneB, problems, out zoneB);
            if (a != null) result["zone_a"] = a;
            if (b != null) result["zone_b"] = b;
            if (zoneA != null && zoneA == zoneB)
                problems.Add("ZoneA and ZoneB are the same zone.");

            LinkState state = StateOf(composite, link, incoming ?? new Incoming(composite));
            foreach ((Entity from, ShortGuid pin) in state.Backwards)
                problems.Add(McpScript.EntityName(commands, composite, from) + " -> " + linkName + "." + McpScript.ParamName(pin) + " runs the wrong way: the link goes from the ZoneLink's " + McpScript.ParamName(pin) + " pin to the zone's reference pin.");
            JArray drivenBy = new JArray(state.Drivers.Select(o => new JObject() { ["entity"] = McpScript.EntityName(commands, composite, o), ["id"] = McpScript.Id(o.shortGUID), ["type"] = McpScript.TypeName(commands, composite, o) }));
            JArray triggeredBy = new JArray(state.Methods.Select(o => McpScript.EntityName(commands, composite, o.from) + " -> " + McpScript.ParamName(o.method)));

            if (exclusion)
            {
                result["exclude_streaming"] = Flag(link, ExcludeStreaming);
                if (drivenBy.Count != 0)
                {
                    result["driven_by"] = drivenBy;
                    problems.Add("A door's zone link points at it (" + string.Join(", ", state.Drivers.Select(o => McpScript.EntityName(commands, composite, o))) + "), but a ZoneExclusionLink is always open and no door can drive it: give the door a ZoneLink (create_zone_link with the door).");
                }
            }
            else
            {
                result["open_on_reset"] = state.OpenOnReset;
                result["lock_on_reset"] = state.LockOnReset;
                if (link.GetParameter(Cost)?.content is cInteger cost)
                    result["cost"] = cost.value;
                //open_on_reset can itself come down a link from script (a gate saying whether a lift is at this floor, say)
                if (state.OpenFrom.Count != 0) result["open_on_reset_from"] = new JArray(state.OpenFrom.Select(o => McpScript.EntityName(commands, composite, o)));
                if (drivenBy.Count != 0) result["driven_by"] = drivenBy;
                if (triggeredBy.Count != 0) result["triggered_by"] = triggeredBy;
                result["always_open"] = state.AlwaysOpen;
                if (state.LockOnReset)
                    problems.Add("It is locked on reset: the zones do not see each other until something unlocks and opens it.");
                else if (!state.Opened)
                    problems.Add("Nothing opens it: open_on_reset is false, no door's zone_link points at it and nothing calls its open method" + (state.Methods.Count != 0 ? " (it is only ever closed or locked)" : "") + ". While it is closed neither zone is drawn from the other, so each pops in as the player crosses. Set open_on_reset for an open doorway, or give it the door (create_zone_link with door).");
                if (state.Drivers.Count > 1)
                    problems.Add(state.Drivers.Count + " doors drive it: each door has its own link, or the first to close shuts the others' view too.");
            }

            if (placed != null && !placed.Contains(composite))
                problems.Add(composite.name + " is not placed in the level, so this link does nothing.");
            if (problems.Count != 0)
                result["problems"] = new JArray(problems);
            return result;
        }

        private static object GetZoneLinks(McpCall call)
        {
            string zoneFilter = call.Str("zone");
            bool problemsOnly = call.Bool("problems_only");
            int limit = Math.Max(1, call.Int("limit", 100));
            return McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: false);
                CompileShown();
                List<Composite> scope = call.Has("composite") ? new List<Composite>() { McpScript.FindComposite(commands, call.Str("composite")) } : commands.Entries.Where(o => o != null).ToList();
                HashSet<Composite> placed = McpBrowseTools.Reachable(commands);
                bool Matches(Composite composite, Entity zone) => zone != null && (McpScript.EntityName(commands, composite, zone).IndexOf(zoneFilter, StringComparison.OrdinalIgnoreCase) >= 0
                    || string.Equals(McpScript.Id(zone.shortGUID), zoneFilter.Trim(), StringComparison.OrdinalIgnoreCase));

                JArray links = new JArray();
                int total = 0, withProblems = 0;
                foreach (Composite composite in scope)
                {
                    Incoming incoming = null;
                    foreach (FunctionEntity function in composite.functions)
                    {
                        if (!IsZoneLink(function)) continue;
                        if (incoming == null) incoming = new Incoming(composite);
                        JObject described = DescribeLink(commands, composite, function, out Entity zoneA, out Entity zoneB, out List<string> problems, placed, incoming);
                        if (zoneFilter != null && !Matches(composite, zoneA) && !Matches(composite, zoneB)) continue;
                        if (problemsOnly && problems.Count == 0) continue;
                        total++;
                        if (problems.Count != 0) withProblems++;
                        if (links.Count < limit) links.Add(described);
                    }
                }
                JObject result = new JObject() { ["count"] = total, ["with_problems"] = withProblems, ["zone_links"] = links };
                if (total > links.Count)
                    result["note"] = "Showing " + links.Count + " of " + total + ": narrow with 'zone' or 'composite', or raise 'limit'.";
                else if (total == 0)
                    result["note"] = zoneFilter == null ? (problemsOnly ? "No zone link has a problem." : "There are no zone links here.") : "No zone link touches a zone matching '" + zoneFilter + "'. get_zones lists the zones.";
                return result;
            });
        }

        /// <summary>The zone links in a zone's composite that join it to another zone, briefly (for get_zones).</summary>
        internal static JArray LinksOfZone(Commands commands, Composite composite, Entity zone)
        {
            JArray links = new JArray();
            if (composite == null || zone == null)
                return links;
            Incoming incoming = null;
            foreach (FunctionEntity function in composite.functions)
            {
                if (!IsZoneLink(function)) continue;
                //Describing a link reads the whole composite's links: only those that could name this zone (or an alias or proxy of it) are
                if (!function.childLinks.Any(o => (o.thisParamID == ZoneA || o.thisParamID == ZoneB) && (o.linkedEntityID == zone.shortGUID || composite.GetEntityByID(o.linkedEntityID) is AliasEntity || composite.GetEntityByID(o.linkedEntityID) is ProxyEntity)))
                    continue;
                if (incoming == null) incoming = new Incoming(composite);
                JObject described = DescribeLink(commands, composite, function, out Entity zoneA, out Entity zoneB, out List<string> problems, null, incoming);
                if (zoneA != zone && zoneB != zone) continue;
                Entity other = zoneA == zone ? zoneB : zoneA;
                JObject brief = new JObject()
                {
                    ["link"] = McpScript.EntityName(commands, composite, function) + " (" + McpScript.Id(function.shortGUID) + ")",
                    ["type"] = described["type"],
                    ["to"] = other == null ? "(nothing)" : McpScript.EntityName(commands, composite, other),
                };
                if (described["open_on_reset"] != null) brief["open_on_reset"] = described["open_on_reset"];
                if (described["driven_by"] is JArray drivers) brief["driven_by"] = new JArray(drivers.Select(o => o["entity"]));
                if (problems.Count != 0) brief["problems"] = problems.Count;
                links.Add(brief);
            }
            return links;
        }
        #endregion

        #region Names
        //The names taken in each composite during an edit, so making many entities does not read every name each time
        private static readonly ConditionalWeakTable<McpScriptEdit, Dictionary<Composite, HashSet<string>>> _taken = new ConditionalWeakTable<McpScriptEdit, Dictionary<Composite, HashSet<string>>>();

        /// <summary>
        /// The names entities of a composite have. An alias or proxy without a name of its own shows its target's, which is no
        /// clash (and reading it resolves the alias), so only their own names count.
        /// </summary>
        private static HashSet<string> TakenNames(Commands commands, Composite composite)
        {
            HashSet<string> taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (FunctionEntity function in composite.functions) taken.Add(commands.Utils.GetEntityName(composite, function));
            foreach (VariableEntity variable in composite.variables) taken.Add(ShortGuidUtils.FindString(variable.name));
            foreach (Entity entity in composite.aliases.Cast<Entity>().Concat(composite.proxies))
            {
                string own = CommandsUtils.GetEntityNameParameter(entity);
                if (own != null) taken.Add(own);
            }
            return taken;
        }

        /// <summary>A name not taken in the composite: <paramref name="wanted"/>, or it with _2, _3... Within an edit, names handed out are remembered.</summary>
        private static string FreeName(McpScriptEdit edit, Composite composite, string wanted)
        {
            Dictionary<Composite, HashSet<string>> byComposite = _taken.GetOrCreateValue(edit);
            if (!byComposite.TryGetValue(composite, out HashSet<string> taken))
                byComposite[composite] = taken = TakenNames(edit.Commands, composite);
            string name = wanted;
            for (int i = 2; taken.Contains(name); i++)
                name = wanted + "_" + i;
            taken.Add(name);
            return name;
        }

        /// <summary>
        /// A Zone of the composite (or one of its zone pins), by name or id. Zones are looked for first, so finding one does not
        /// read the name of every entity.
        /// </summary>
        private static Entity ZoneIn(Commands commands, Composite composite, string reference, string what, bool allowPin)
        {
            string wanted = (reference ?? "").Trim();
            List<Entity> quick = new List<Entity>();
            foreach (FunctionEntity zone in composite.functions)
                if (zone.function == FunctionType.Zone && (string.Equals(McpScript.Id(zone.shortGUID), wanted, StringComparison.OrdinalIgnoreCase) || string.Equals(commands.Utils.GetEntityName(composite, zone), wanted, StringComparison.OrdinalIgnoreCase)))
                    quick.Add(zone);
            if (allowPin)
                foreach (VariableEntity pin in composite.variables)
                    if (pin.type == DataType.ZONE && string.Equals(ShortGuidUtils.FindString(pin.name), wanted, StringComparison.OrdinalIgnoreCase))
                        quick.Add(pin);
            if (quick.Count == 1)
                return quick[0];
            if (quick.Count > 1)
                throw new McpError(quick.Count + " zones in " + composite.name + " are called '" + wanted + "': use an id (" + string.Join(", ", quick.Select(o => McpScript.Id(o.shortGUID))) + ").");

            Entity entity;
            try
            {
                entity = McpScript.FindEntity(commands, composite, reference);
            }
            catch (McpError)
            {
                List<string> elsewhere = new List<string>();
                foreach (Composite other in commands.Entries)
                {
                    if (other == null || other == composite) continue;
                    foreach (FunctionEntity zone in other.functions)
                        if (zone.function == FunctionType.Zone && (string.Equals(commands.Utils.GetEntityName(other, zone), wanted, StringComparison.OrdinalIgnoreCase) || string.Equals(McpScript.Id(zone.shortGUID), wanted, StringComparison.OrdinalIgnoreCase)))
                            elsewhere.Add(other.name);
                }
                if (elsewhere.Count == 0)
                    throw;
                throw new McpError("There is no zone '" + reference + "' in " + composite.name + ", but there is one in " + string.Join("; ", elsewhere.Distinct().Take(5)) + ". A ZoneLink only joins zones in its own composite: give that composite (and keep both zones in it).");
            }

            string name = McpScript.EntityName(commands, composite, entity);
            if (entity is AliasEntity || entity is ProxyEntity)
            {
                (Composite targetComposite, Entity target) = commands.Utils.GetResolvedTarget(entity is AliasEntity alias ? commands.Utils.ResolveAlias(alias, composite) : commands.Utils.ResolveProxy((ProxyEntity)entity));
                if (IsZone(target))
                    throw new McpError(what + " '" + name + "' stands for the Zone " + McpScript.EntityName(commands, targetComposite, target) + " in " + targetComposite.name + ". Link the zone itself: work in " + targetComposite.name + ", where it is.");
            }
            throw new McpError(what + " '" + name + "' is a " + McpScript.TypeName(commands, composite, entity) + ", not a Zone. get_zones lists the zones.");
        }
        #endregion

        #region Content paths
        /// <summary>The TriggerSequences a zone's 'composites' pin lists.</summary>
        private static List<TriggerSequence> SequencesOf(Composite composite, Entity zone)
        {
            List<TriggerSequence> sequences = new List<TriggerSequence>();
            foreach (EntityConnector link in zone.childLinks)
                if (link.thisParamID == ShortGuids.composites && composite.GetEntityByID(link.linkedEntityID) is TriggerSequence sequence && !sequences.Contains(sequence))
                    sequences.Add(sequence);
            return sequences;
        }

        /// <summary>The other entities of the composite that link to a sequence: an edit to it reaches them too.</summary>
        private static List<string> AlsoUsing(Commands commands, Composite composite, Entity sequence, Entity zone)
        {
            List<string> users = new List<string>();
            foreach (Entity other in composite.GetEntities())
                if (other != zone && other.childLinks.Any(o => o.linkedEntityID == sequence.shortGUID))
                    users.Add(McpScript.EntityName(commands, composite, other));
            return users;
        }

        /// <summary>Content paths for a zone of <paramref name="composite"/>, resolved before the edit (names can mean a long search). UI thread.</summary>
        private static List<ShortGuid[]> ReadPaths(McpCall call, Commands commands, Composite composite, JArray tokens, bool fromRoot)
        {
            return tokens.Select(o => ContentPath(call, commands, composite, o, fromRoot)).ToList();
        }

        /// <summary>
        /// A zone entry's path, from <paramref name="composite"/> as the build reads it. With <paramref name="fromRoot"/> the path
        /// given starts at the level root, and is rewritten to start at <paramref name="composite"/> (the build only reads entries
        /// relative to the zone's composite). A single name that is not an entity of the composite itself is looked for further
        /// down, through the instances it places ("the room called X" is often inside a group): an instance with that name, or an
        /// instance of a composite with that name, when there is exactly one.
        /// </summary>
        private static ShortGuid[] ContentPath(McpCall call, Commands commands, Composite composite, JToken token, bool fromRoot)
        {
            Composite root = commands.EntryPoints[0];
            if (fromRoot)
            {
                ShortGuid[] path = McpScriptTools.SequencePath(commands, composite, token, true);
                if (composite == root)
                    return path;
                //Find where the path steps into the composite, and keep what follows
                List<ShortGuid> ids = path.Where(o => o != ShortGuid.Invalid).ToList();
                Composite at = root;
                for (int i = 0; i < ids.Count - 1; i++)
                {
                    Composite next = McpScript.InstancedComposite(commands, at.GetEntityByID(ids[i]));
                    if (next == null) break;
                    if (next == composite)
                        return ids.Skip(i + 1).Concat(new[] { ShortGuid.Invalid }).ToArray();
                    at = next;
                }
                throw new McpError("'" + string.Join("/", McpScriptTools.ReadPath(token)) + "' (from the root) is not inside " + composite.name + ", so a zone there cannot claim it: the build only reads a zone's entries from the zone's own composite. Place the content inside " + composite.name + " (create it there, not in root) to zone it with " + composite.name + "'s zones.");
            }
            try
            {
                return McpScriptTools.SequencePath(commands, composite, token, false);
            }
            catch (McpError) when (BorrowedAliasPath(commands, composite, token) is ShortGuid[] aliased)
            {
                return aliased;
            }
            catch (McpError) when (McpScriptTools.ReadPath(token).Count == 1 && !NamesEntityOf(commands, composite, McpScriptTools.ReadPath(token)[0]))
            {
                string wanted = McpScriptTools.ReadPath(token)[0].Trim();
                //An instance here of a composite called that (instances are named Room_1, Room_2...) comes before anything deeper
                List<FunctionEntity> top = composite.functions.Where(o => !o.function.IsFunctionType && commands.GetComposite(o.function) is Composite placedHere
                    && (string.Equals(McpScript.CompositeLeaf(placedHere), wanted, StringComparison.OrdinalIgnoreCase) || string.Equals(placedHere.name, wanted, StringComparison.OrdinalIgnoreCase))).ToList();
                if (top.Count == 1)
                    return new[] { top[0].shortGUID, ShortGuid.Invalid };
                if (top.Count > 1)
                    throw new McpError(top.Count + " instances in " + composite.name + " are of a composite called '" + wanted + "': give one by name or id (" + string.Join(", ", top.Take(10).Select(o => commands.Utils.GetEntityName(composite, o) + " " + McpScript.Id(o.shortGUID))) + "), or list each.");
                List<List<Entity>> found = new List<List<Entity>>();
                List<string> similar = new List<string>();
                Dictionary<ShortGuid, Composite> placed = new Dictionary<ShortGuid, Composite>();
                McpPlacements walker = new McpPlacements(commands);
                using (McpEditorTools.Heartbeat(call, "Looking for '" + wanted + "' under " + McpScript.CompositeLeaf(composite)))
                    walker.Walk(composite, step =>
                    {
                        if (step.Chain.Count < 2 || !(step.Entity is FunctionEntity function)) return true;
                        //Rooms are instances: compare an instance's name, and its composite's
                        if (function.function.IsFunctionType) return true;
                        if (!placed.TryGetValue(function.function, out Composite instanced))
                            placed[function.function] = instanced = commands.GetComposite(function.function);
                        string name = commands.Utils.GetEntityName(step.Composite, function);
                        string compositeName = instanced == null ? null : McpScript.CompositeLeaf(instanced);
                        if (string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase) || (compositeName != null && (string.Equals(compositeName, wanted, StringComparison.OrdinalIgnoreCase) || string.Equals(instanced.name, wanted, StringComparison.OrdinalIgnoreCase))))
                            found.Add(new List<Entity>(step.Chain));
                        else if (similar.Count < 8 && (name.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0 || (compositeName != null && compositeName.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0)))
                            similar.Add(name + (compositeName != null ? " (" + compositeName + ")" : ""));
                        return found.Count <= 10;
                    }, null, call.Cancel);
                if (found.Count == 1)
                    return found[0].Select(o => o.shortGUID).Concat(new[] { ShortGuid.Invalid }).ToArray();
                if (found.Count > 1)
                    throw new McpError("'" + wanted + "' is not in " + composite.name + " itself, and " + (found.Count > 10 ? "more than 10" : found.Count.ToString()) + " instances under it have that name (or are of a composite called that): give the path, e.g. " +
                        string.Join("; ", found.Take(5).Select(chain => "[\"" + string.Join("\", \"", chain.Select(o => McpScript.Id(o.shortGUID))) + "\"]")) + ".");
                if (walker.Truncated)
                    throw new McpError("'" + wanted + "' is not in " + composite.name + " itself, and the search under it stopped after " + walker.Visited + " entities without finding it: give its path.");
                if (similar.Count != 0)
                    throw new McpError("No entity '" + wanted + "' in or under " + composite.name + ". Similar, under it: " + string.Join("; ", similar) + ".");
                throw;
            }
        }

        /// <summary>
        /// An alias with no name of its own shows its target's name: a name that picks one out means the entity it points at (its
        /// path, from the composite). Null when the name does not pick out such an alias, or its target is not an entity a zone
        /// can hold. Proxies are left out: their paths start at the level root.
        /// </summary>
        private static ShortGuid[] BorrowedAliasPath(Commands commands, Composite composite, JToken token)
        {
            List<string> steps = McpScriptTools.ReadPath(token);
            if (steps.Count != 1) return null;
            Entity entity;
            try { entity = McpScript.FindEntity(commands, composite, steps[0]); }
            catch (McpError) { return null; }
            if (!(entity is AliasEntity alias) || CommandsUtils.GetEntityNameParameter(alias) != null || alias.alias?.path == null) return null;
            //Strictly relative to the composite (this overload has no root or proxy readings), ending in an entity a sequence can hold
            List<Tuple<Composite, Entity>> chain = commands.Utils.ResolveAlias(alias.alias.path, composite);
            if (chain.Count == 0 || !(chain[chain.Count - 1].Item2 is FunctionEntity)) return null;
            ShortGuid[] ids = alias.alias.path.Where(o => o != ShortGuid.Invalid).ToArray();
            return ids.Length == 0 ? null : ids.Concat(new[] { ShortGuid.Invalid }).ToArray();
        }

        /// <summary>Whether a reference picks out an entity of the composite itself, by id or by name (as FindEntity reads it): then FindEntity's error is the answer.</summary>
        private static bool NamesEntityOf(Commands commands, Composite composite, string reference)
        {
            string wanted = (reference ?? "").Trim();
            if (wanted.Length == 0) return false;
            ShortGuid? id = McpScript.ParseId(wanted);
            if (id != null && composite.GetEntityByID(id.Value) != null) return true;
            return composite.GetEntities().Any(o => string.Equals(McpScript.EntityName(commands, composite, o), wanted, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>A chain of entities as seen from <paramref name="composite"/>: one read from the root through an instance of it loses that prefix.</summary>
        private static List<Entity> UnderComposite(Commands commands, Composite composite, List<Entity> chain)
        {
            if (composite == commands.EntryPoints[0]) return chain;
            for (int i = 0; i < chain.Count - 1; i++)
            {
                Composite next = McpScript.InstancedComposite(commands, chain[i]);
                if (next == null) break;
                if (next == composite) return chain.Skip(i + 1).ToList();
            }
            return chain;
        }

        private static string PathText(Commands commands, Composite composite, ShortGuid[] path)
        {
            return string.Join("/", McpScript.DescribePath(commands, commands.Utils.ResolveEntityPath(path, composite), path).Select(o => (string)o));
        }

        /// <summary>Close the sequence editors open on these: they hold the lists an edit replaces, and record their own changes as a step when closed.</summary>
        private static void CloseSequenceEditors(IEnumerable<Entity> sequences)
        {
            HashSet<Entity> wanted = new HashSet<Entity>(sequences);
            foreach (TriggerSequenceEditor editor in Application.OpenForms.OfType<TriggerSequenceEditor>().ToList())
                if (wanted.Contains(editor.Entity)) editor.Close();
        }

        private static bool Prefix(ShortGuid[] prefix, ShortGuid[] path)
        {
            int a = prefix.Length, b = path.Length;
            if (a > 0 && prefix[a - 1] == ShortGuid.Invalid) a--;
            if (b > 0 && path[b - 1] == ShortGuid.Invalid) b--;
            if (a == 0 || a > b) return false;
            for (int i = 0; i < a; i++)
                if (prefix[i] != path[i]) return false;
            return true;
        }

        /// <summary>The other zones of the composite whose lists name <paramref name="path"/>, a parent of it, or something inside it.</summary>
        private static List<(Entity zone, ShortGuid[] entry)> OtherClaims(Composite composite, Entity zone, ShortGuid[] path)
        {
            List<(Entity, ShortGuid[])> claims = new List<(Entity, ShortGuid[])>();
            foreach (FunctionEntity other in composite.functions)
            {
                if (other == zone || other.function != FunctionType.Zone) continue;
                foreach (TriggerSequence sequence in SequencesOf(composite, other))
                    foreach (TriggerSequence.SequenceEntry entry in sequence.sequence)
                    {
                        ShortGuid[] stored = entry.connectedEntity?.path;
                        if (stored != null && (Prefix(stored, path) || Prefix(path, stored)))
                            claims.Add((other, stored));
                    }
            }
            return claims;
        }

        /// <summary>
        /// Within an edit, after content was added to <paramref name="zone"/>: say which other zones claim it too, and with
        /// <paramref name="move"/> take their entries for it (or for things inside it) out. An entry naming a parent of it stays
        /// (removing it would unzone the rest), and is reported.
        /// </summary>
        private static void OtherZones(McpScriptEdit edit, Composite composite, Entity zone, List<ShortGuid[]> added, bool move, ContentChange report)
        {
            Commands commands = edit.Commands;
            Dictionary<Entity, List<ShortGuid[]>> taking = new Dictionary<Entity, List<ShortGuid[]>>();
            foreach (ShortGuid[] path in added)
            {
                List<(Entity zone, ShortGuid[] entry)> claims = OtherClaims(composite, zone, path);
                if (claims.Count == 0) continue;
                string text = PathText(commands, composite, path);
                foreach ((Entity other, ShortGuid[] entry) in claims)
                {
                    string otherName = McpScript.EntityName(commands, composite, other);
                    bool parent = Prefix(entry, path) && !Prefix(path, entry);
                    if (!move || parent)
                    {
                        report.AlsoClaimed.Add(text + ": also claimed by " + otherName + (parent ? " (through " + PathText(commands, composite, entry) + ", which holds it" + (move ? "; left there, since taking it would unzone the rest" : "") + ")" : ""));
                        continue;
                    }
                    if (!taking.TryGetValue(other, out List<ShortGuid[]> list)) taking[other] = list = new List<ShortGuid[]>();
                    list.Add(entry);
                }
            }
            foreach (KeyValuePair<Entity, List<ShortGuid[]>> pair in taking)
            {
                ContentChange change = new ContentChange();
                ChangeContents(edit, composite, pair.Key, new List<ShortGuid[]>(), pair.Value, change);
                foreach (JToken removed in change.Removed)
                    report.Moved.Add((string)removed + " (from " + McpScript.EntityName(commands, composite, pair.Key) + ")");
            }
        }
        #endregion

        #region Making zones
        private static object CreateZone(McpCall call)
        {
            string name = call.Str("name", required: true).Trim();
            bool fromRoot = call.Bool("from_root");
            bool move = ReadOnClaimed(call), link = call.Bool("link");
            JArray contents = call.Array("contents");
            JObject parameters = call.Object("parameters");
            string page = call.Str("page") ?? "Zones";
            List<string> notes = new List<string>();
            List<ShortGuid[]> paths = null;
            Composite composite = null;
            using (McpEditorTools.Heartbeat(call, "Finding the zone's content"))
                McpEditor.UI(() =>
                {
                    Commands commands = McpEditor.RequireCommands();
                    CompileShown();
                    composite = ZoningComposite(call, commands);
                    if (composite.functions.Any(o => o.function == FunctionType.Zone && string.Equals(commands.Utils.GetEntityName(composite, o), name, StringComparison.OrdinalIgnoreCase)))
                        throw new McpError(composite.name + " already has a zone called '" + name + "': set_zone_contents changes what it claims.");
                    paths = ReadPaths(call, commands, composite, contents, fromRoot);
                    if (call.Has("region")) paths.AddRange(ContentIn(call, commands, composite, call.Object("region"), "region", notes));
                    //'move' takes entries from other zones and 'link' lists doors in neighbouring zones: an editor open on one holds the list the edit replaces
                    if (move || link) CloseSequenceEditors(composite.functions.OfType<TriggerSequence>());
                });

            FunctionEntity zone = null;
            Entity sequence = null;
            JArray claimed = new JArray();
            JArray skipped = new JArray();
            ContentChange others = new ContentChange();
            JArray linksMade = new JArray();
            List<string> linkSkipped = new List<string>();
            using (McpEditorTools.Heartbeat(call, "Making the zone"))
                McpScriptEdit.Run("Create zone " + name, composite, edit =>
                {
                    Commands commands = edit.Commands;
                    edit.UsePage(composite, page);
                    zone = edit.AddFunction(composite, FunctionType.Zone, name);
                    //As vanilla zones have it: what an unloaded zone holds is suspended rather than left running
                    if (parameters == null || parameters["suspend_on_unload"] == null)
                        edit.SetParameter(composite, zone, "suspend_on_unload", new JValue(true));
                    if (parameters != null)
                        foreach (JProperty parameter in parameters.Properties())
                            McpScriptTools.WriteParameter(edit, composite, zone, parameter.Name, parameter.Value, false, notes);
                    sequence = edit.AddFunction(composite, FunctionType.TriggerSequence, FreeName(edit, composite, name + "_Contents"));
                    edit.AddLink(composite, zone, "composites", sequence, "reference");

                    List<(ShortGuid[] path, float delay)> entries = new List<(ShortGuid[], float)>();
                    List<List<Entity>> chains = new List<List<Entity>>();
                    List<ShortGuid[]> kept = new List<ShortGuid[]>();
                    foreach (ShortGuid[] path in paths)
                    {
                        List<Entity> chain = McpScriptTools.ChainOf(commands, composite, path);
                        if (chains.Any(o => McpScriptTools.SameChain(o, chain)))
                        {
                            skipped.Add("given twice: " + PathText(commands, composite, path));
                            continue;
                        }
                        chains.Add(chain);
                        entries.Add((path, 0f));
                        kept.Add(path);
                        claimed.Add(PathText(commands, composite, path));
                    }
                    if (entries.Count != 0)
                        edit.SetTriggerSequence(composite, sequence, entries, null, append: false);
                    OtherZones(edit, composite, zone, kept, move, others);

                    if (link)
                    {
                        //The new zone's doors and neighbours, worked out on the script as it now stands
                        Scene scene = BuildScene(commands, composite, call.Cancel);
                        string label = McpScript.Id(zone.shortGUID);
                        List<LinkAction> actions = PlanLinks(Analyse(commands, scene), new LinkOptions() { Touching = new HashSet<string>() { label } }, linkSkipped);
                        linksMade = ApplyLinks(edit, scene, actions, o => scene.Zones.TryGetValue(o, out Entity found) ? found : null, false, true, notes, linkSkipped);
                        //The planner places only content one zone claims: a zone whose content is all shared has no doors or neighbours it can find
                        if (actions.Count == 0 && !scene.Leaves.Any(o => scene.DoorOf(o.Path) == null && scene.ZoneOf(o) == label))
                        {
                            List<string> sharers = scene.Leaves.Where(o => scene.ClaimsOf(o.Path).Contains(label)).SelectMany(o => scene.ClaimsOf(o.Path)).Where(o => o != label).Distinct().Select(scene.NameOf).Take(5).ToList();
                            linkSkipped.Add(sharers.Count != 0
                                ? name + ": everything it holds is also claimed by " + string.Join(", ", sharers) + ", so its doors and neighbours cannot be told and nothing was linked. Make it with on_claimed 'move' to take that content over, or link it by hand (create_zone_link with the door's path)."
                                : name + ": it holds no placed models, lights or collision, so it has no doors or neighbours to link.");
                        }
                    }
                    edit.Select.Add(zone);
                });
            foreach (string note in notes) call.Note(note);

            JObject result = McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: false);
                return new JObject()
                {
                    ["composite"] = composite.name,
                    ["zone"] = McpScript.Brief(commands, composite, zone),
                    ["sequence"] = McpScript.Brief(commands, composite, sequence),
                };
            });
            result["claims"] = claimed;
            if (skipped.Count != 0) result["skipped"] = skipped;
            if (others.AlsoClaimed.Count != 0) result["also_claimed"] = others.AlsoClaimed;
            if (others.Moved.Count != 0) result["moved_from_other_zones"] = others.Moved;
            if (link)
            {
                result["links"] = linksMade;
                if (linkSkipped.Count != 0) result["links_skipped"] = new JArray(linkSkipped);
            }
            result["next"] = (claimed.Count == 0 ? "It claims nothing yet: set_zone_contents adds to it. " : "") +
                (link ? "" : "Join it to each neighbouring zone: create_zone_link with door 'auto', or link_zones with zones [\"" + name + "\"]. ") + BuildNote;
            return result;
        }

        private static bool ReadOnClaimed(McpCall call)
        {
            string onClaimed = (call.Str("on_claimed") ?? "share").Trim().ToLowerInvariant();
            if (onClaimed != "share" && onClaimed != "move")
                throw new McpError("'on_claimed' is 'share' (claim it as well) or 'move' (take it from the other zones).");
            return onClaimed == "move";
        }

        /// <summary>What changing a zone's contents did, for the result.</summary>
        private sealed class ContentChange
        {
            public JArray Added = new JArray();
            public JArray Removed = new JArray();
            public JArray Skipped = new JArray();
            public JArray AlsoClaimed = new JArray();
            public JArray Moved = new JArray();
            public List<string> Notes = new List<string>();
        }

        /// <summary>
        /// Within an edit: make <paramref name="zone"/> claim the paths in <paramref name="toAdd"/> and stop claiming those in
        /// <paramref name="toRemove"/> (entity paths from its composite, ending in Invalid). Additions go on the first
        /// TriggerSequence its 'composites' pin lists, one being made and linked if it lists none; anything it already claims
        /// (through any of its sequences, or linked straight to it) is skipped.
        /// </summary>
        private static void ChangeContents(McpScriptEdit edit, Composite composite, Entity zone, List<ShortGuid[]> toAdd, List<ShortGuid[]> toRemove, ContentChange report, bool fromRoot = false)
        {
            Commands commands = edit.Commands;
            List<TriggerSequence> sequences = SequencesOf(composite, zone);
            List<List<Entity>> removing = toRemove.Select(o => UnderComposite(commands, composite, McpScriptTools.ChainOf(commands, composite, o))).ToList();

            //Each sequence's entries as they will be: what removal leaves
            Dictionary<TriggerSequence, List<(ShortGuid[] path, float delay)>> rows = new Dictionary<TriggerSequence, List<(ShortGuid[], float)>>();
            List<List<Entity>> kept = new List<List<Entity>>();
            bool[] matched = new bool[toRemove.Count];
            foreach (TriggerSequence sequence in sequences)
            {
                List<(ShortGuid[] path, float delay)> list = new List<(ShortGuid[], float)>();
                foreach (TriggerSequence.SequenceEntry entry in sequence.sequence)
                {
                    ShortGuid[] path = entry.connectedEntity?.path ?? new ShortGuid[0];
                    List<Entity> chain = UnderComposite(commands, composite, McpScriptTools.ChainOf(commands, composite, path));
                    int hit = removing.FindIndex(o => McpScriptTools.SameChain(o, chain));
                    if (hit >= 0)
                    {
                        matched[hit] = true;
                        report.Removed.Add(PathText(commands, composite, path));
                        continue;
                    }
                    list.Add(((ShortGuid[])path.Clone(), entry.timing));
                    kept.Add(chain);
                }
                rows[sequence] = list;
            }

            //A zone can also list an entity of its own composite straight from its pin
            for (int i = 0; i < toRemove.Count; i++)
            {
                if (removing[i].Count != 1) continue;
                Entity direct = removing[i][0];
                if (zone.childLinks.Any(o => o.thisParamID == ShortGuids.composites && o.linkedEntityID == direct.shortGUID))
                {
                    edit.RemoveLinks(composite, zone, "composites", direct, null);
                    report.Removed.Add(McpScript.EntityName(commands, composite, direct) + " (linked straight to the zone)");
                    matched[i] = true;
                }
            }
            for (int i = 0; i < toRemove.Count; i++)
                if (!matched[i]) report.Skipped.Add("not claimed by this zone's list: " + PathText(commands, composite, toRemove[i]) + " (give removals from the zone's composite, or from the root with from_root)");
            foreach (EntityConnector link in zone.childLinks.Where(o => o.thisParamID == ShortGuids.composites))
            {
                Entity direct = composite.GetEntityByID(link.linkedEntityID);
                if (direct is AliasEntity alias)
                    kept.Add(commands.Utils.ResolveAlias(alias, composite).Select(o => o.Item2).ToList());
                else if (direct != null && !(direct is TriggerSequence) && !(direct is VariableEntity))
                    kept.Add(new List<Entity>() { direct });
            }

            //Additions go on the first sequence, made (and linked to the zone) if it has none
            TriggerSequence target = sequences.FirstOrDefault();
            foreach (ShortGuid[] path in toAdd)
            {
                List<Entity> chain = UnderComposite(commands, composite, McpScriptTools.ChainOf(commands, composite, path));
                if (kept.Any(o => McpScriptTools.SameChain(o, chain)))
                {
                    report.Skipped.Add("already claimed: " + PathText(commands, composite, path));
                    continue;
                }
                if (target == null)
                {
                    target = (TriggerSequence)edit.AddFunction(composite, FunctionType.TriggerSequence, FreeName(edit, composite, McpScript.EntityName(commands, composite, zone) + "_Contents"));
                    edit.AddLink(composite, zone, "composites", target, "reference");
                    rows[target] = new List<(ShortGuid[], float)>();
                    report.Notes.Add("The zone listed nothing through a TriggerSequence, so " + McpScript.EntityName(commands, composite, target) + " was made to list its contents.");
                }
                rows[target].Add((path, 0f));
                kept.Add(chain);
                report.Added.Add(PathText(commands, composite, path));
            }

            foreach (KeyValuePair<TriggerSequence, List<(ShortGuid[] path, float delay)>> pair in rows)
            {
                List<TriggerSequence.SequenceEntry> current = pair.Key.sequence;
                List<(ShortGuid[] path, float delay)> wanted = pair.Value;
                bool changed = wanted.Count != current.Count
                    || wanted.Where((o, i) => o.delay != current[i].timing || !o.path.SequenceEqual(current[i].connectedEntity?.path ?? new ShortGuid[0])).Any();
                if (!changed) continue;
                edit.SetTriggerSequence(composite, pair.Key, wanted, null, append: false);
                List<string> users = AlsoUsing(commands, composite, pair.Key, zone);
                if (users.Count != 0)
                    report.Notes.Add(McpScript.EntityName(commands, composite, pair.Key) + " is also linked from " + string.Join(", ", users) + ", which see this change too.");
            }
        }

        private static object SetZoneContents(McpCall call)
        {
            JArray adds = call.Array("add");
            JArray removes = call.Array("remove");
            bool fromRoot = call.Bool("from_root");
            bool move = ReadOnClaimed(call);
            string page = call.Str("page") ?? "Zones";
            if (adds.Count == 0 && removes.Count == 0 && !call.Has("add_region"))
                throw new McpError("Give 'add', 'add_region' and/or 'remove': what the zone is to claim or stop claiming.");

            Composite composite = null;
            List<ShortGuid[]> adding = null, removing = null;
            List<string> regionNotes = new List<string>();
            using (McpEditorTools.Heartbeat(call, "Finding the content"))
                McpEditor.UI(() =>
                {
                    Commands commands = McpEditor.RequireCommands();
                    CompileShown();
                    composite = ZoningComposite(call, commands);
                    Entity found = ZoneIn(commands, composite, call.Str("zone", required: true), "zone", allowPin: false);
                    adding = ReadPaths(call, commands, composite, adds, fromRoot);
                    removing = removes.Select(o => McpScriptTools.SequencePath(commands, composite, o, fromRoot)).ToList();
                    if (call.Has("add_region")) adding.AddRange(ContentIn(call, commands, composite, call.Object("add_region"), "add_region", regionNotes));
                    CloseSequenceEditors(move ? composite.functions.OfType<TriggerSequence>().Cast<Entity>() : SequencesOf(composite, found));
                });

            Entity zone = null;
            ContentChange change = new ContentChange();
            McpScriptEdit.Outcome outcome = McpScriptEdit.Run("Set zone contents", composite, edit =>
            {
                Commands commands = edit.Commands;
                edit.UsePage(composite, page);
                zone = ZoneIn(commands, composite, call.Str("zone", required: true), "zone", allowPin: false);
                ChangeContents(edit, composite, zone, adding, removing, change, fromRoot);
                List<ShortGuid[]> added = adding.Where(path => change.Added.Any(o => (string)o == PathText(commands, composite, path))).ToList();
                OtherZones(edit, composite, zone, added, move, change);
                edit.Select.Add(zone);
            });
            List<string> notes = regionNotes.Concat(change.Notes).ToList();
            foreach (string note in notes) call.Note(note);

            JObject result = new JObject() { ["zone"] = McpEditor.UI(() => McpScript.Brief(McpEditor.RequireCommands(forEditing: false), composite, zone)) };
            if (change.Added.Count != 0) result["added"] = change.Added;
            if (change.Removed.Count != 0) result["removed"] = change.Removed;
            if (change.Skipped.Count != 0) result["skipped"] = change.Skipped;
            if (change.AlsoClaimed.Count != 0) result["also_claimed"] = change.AlsoClaimed;
            if (change.Moved.Count != 0) result["moved_from_other_zones"] = change.Moved;
            if (!outcome.Changed) result["unchanged"] = "The zone already claimed exactly that.";
            else result["next"] = BuildNote;
            return result;
        }

        private static object DeleteZone(McpCall call)
        {
            List<string> wanted = call.StrList("zones");
            if (wanted.Count == 0)
                throw new McpError("Say which zones to delete in 'zones'.");
            string links = (call.Str("links") ?? "delete").Trim().ToLowerInvariant();
            if (links != "delete" && links != "keep")
                throw new McpError("'links' is 'delete' or 'keep'.");
            bool dryRun = call.Bool("dry_run");

            Composite composite = null;
            JArray report = new JArray();
            List<string> notes = new List<string>();
            //What goes, worked out the same way for the dry run and the edit
            void Plan(Commands commands, Action<Entity> delete)
            {
                report.RemoveAll();
                List<Entity> zones = wanted.Select(o => ZoneIn(commands, composite, o, "zone", allowPin: false)).Distinct().ToList();
                HashSet<Entity> going = new HashSet<Entity>(zones);
                foreach (Entity zone in zones)
                {
                    JObject entry = new JObject() { ["zone"] = McpScript.EntityName(commands, composite, zone) };
                    JArray sequences = new JArray(), kept = new JArray(), linksGoing = new JArray(), linksLeft = new JArray();
                    foreach (TriggerSequence sequence in SequencesOf(composite, zone))
                    {
                        if (going.Contains(sequence)) continue; //going already, with an earlier zone
                        //Users that are going too do not keep it
                        List<string> users = composite.GetEntities().Where(o => !going.Contains(o) && o.childLinks.Any(l => l.linkedEntityID == sequence.shortGUID)).Select(o => McpScript.EntityName(commands, composite, o)).ToList();
                        if (users.Count == 0) { going.Add(sequence); sequences.Add(McpScript.EntityName(commands, composite, sequence)); delete?.Invoke(sequence); }
                        else kept.Add(McpScript.EntityName(commands, composite, sequence) + " (also used by " + string.Join(", ", users) + ")");
                    }
                    foreach (FunctionEntity link in composite.functions.Where(IsZoneLink).ToList())
                    {
                        (Entity a, Entity b) = SidesOf(commands, composite, link);
                        if (a != zone && b != zone) continue;
                        string text = McpScript.EntityName(commands, composite, link) + " (to " + McpScript.EntityName(commands, composite, a == zone ? b : a) + ")";
                        if (links == "delete") { if (going.Add(link)) { linksGoing.Add(text); delete?.Invoke(link); } }
                        else linksLeft.Add(text);
                    }
                    if (sequences.Count != 0) entry["sequences"] = sequences;
                    if (kept.Count != 0) entry["sequences_kept"] = kept;
                    if (linksGoing.Count != 0) entry["links"] = linksGoing;
                    if (linksLeft.Count != 0) entry["links_left_half_wired"] = linksLeft;
                    delete?.Invoke(zone);
                    report.Add(entry);
                }
            }

            McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: !dryRun);
                CompileShown();
                composite = ZoningComposite(call, commands);
                if (dryRun) Plan(commands, null);
                else CloseSequenceEditors(composite.functions.OfType<TriggerSequence>());
            });
            if (dryRun)
                return new JObject() { ["composite"] = composite.name, ["dry_run"] = true, ["would_delete"] = report, ["next"] = "Call again without dry_run to delete these as one undo step." };

            McpScriptEdit.Run("Delete " + wanted.Count + " zone" + (wanted.Count == 1 ? "" : "s"), composite, edit =>
            {
                Plan(edit.Commands, entity => notes.AddRange(edit.Delete(composite, entity)));
            }, show: false);
            foreach (string note in notes.Distinct()) call.Note("Also removed: " + note);
            return new JObject()
            {
                ["composite"] = composite.name,
                ["deleted"] = report,
                ["next"] = "What they claimed is in no zone now: auto_zone or create_zone zones it again. " + BuildNote,
            };
        }
        #endregion

        #region Linking zones
        /// <summary>The door a ZoneLink follows, as reached from the ZoneLink's composite.</summary>
        private sealed class DoorSpec
        {
            /// <summary>The entity the link goes from when it is in the ZoneLink's composite: the Door, or the door composite's instance.</summary>
            public Entity Owner;
            /// <summary>When the door is deeper: the path an alias in the ZoneLink's composite takes to it (ending in Invalid).</summary>
            public ShortGuid[] AliasPath;
            /// <summary>Its pin that takes the zone link.</summary>
            public ShortGuid Pin;
            public string Name;
            public string Note;
            /// <summary>The door as named, from the ZoneLink's composite (ending in Invalid): what the zones either side list.</summary>
            public ShortGuid[] ContentPath;
            /// <summary>The door variant's composite name ("Door" for a Door function), as analyse_zones types doors: says whether it has a window.</summary>
            public string Type;
        }

        /// <summary>
        /// The door at a path from <paramref name="composite"/>. A piece that hands its zone link pin up to the composite it is in
        /// (the Door_Package inside a Door_SML, the Door inside that) stands for that composite's instance, so the path is walked up
        /// to the door variant - the instance the rooms list and the link is given to.
        /// </summary>
        private static DoorSpec FindDoor(Commands commands, Composite composite, List<string> steps)
        {
            ShortGuid[] path = McpScript.ResolvePath(commands, composite, steps, out Composite _, out Entity _);
            List<Entity> chain = McpScriptTools.ChainOf(commands, composite, path);
            if (chain.Count == 0)
                throw new McpError("'" + string.Join("/", steps) + "' does not lead to an entity of " + composite.name + ".");
            List<Composite> holders = new List<Composite>() { composite };
            for (int i = 0; i < chain.Count - 1; i++)
                holders.Add(McpScript.InstancedComposite(commands, chain[i]));
            ShortGuid PinFor(Entity entity)
            {
                if (entity is FunctionEntity doorFunction && doorFunction.function == FunctionType.Door) return ZoneLinkPin;
                Composite instanced = McpScript.InstancedComposite(commands, entity);
                return instanced == null ? ShortGuid.Invalid : ZoneLinkPinOf(commands, instanced);
            }
            string asNamed = McpScript.EntityName(commands, holders[chain.Count - 1], chain[chain.Count - 1]);
            while (chain.Count > 1)
            {
                int last = chain.Count - 1;
                ShortGuid pin = PinFor(chain[last]);
                if (pin == ShortGuid.Invalid || !HandsPinUp(holders[last], chain[last], pin)) break;
                chain.RemoveAt(last);
                holders.RemoveAt(last);
            }

            Entity target = chain[chain.Count - 1];
            Composite holder = holders[chain.Count - 1];
            List<ShortGuid> ids = chain.Select(o => o.shortGUID).ToList();
            DoorSpec door = new DoorSpec() { Name = McpScript.EntityName(commands, holder, target), ContentPath = ids.Concat(new[] { ShortGuid.Invalid }).ToArray() };
            door.Type = target is FunctionEntity typed && typed.function == FunctionType.Door ? "Door" : (McpScript.InstancedComposite(commands, target)?.name ?? "Door");
            if (door.Name != asNamed)
                door.Note = "'" + asNamed + "' is part of the door '" + door.Name + "' (it hands its zone link up to it), so that was used: it is what the rooms list and what takes the link.";

            ShortGuid doorPin = PinFor(target);
            if (target is FunctionEntity function && function.function == FunctionType.Door)
            {
                if (HandsPinUp(holder, target, ZoneLinkPin))
                    throw new McpError("'" + door.Name + "' hands its zone link up to " + composite.name + "'s own zone link pin: the link is given where " + composite.name + " is placed, not here.");
                //Reached from outside its composite, a Door already wired in there would be given a second link
                if (ids.Count > 1 && function.childLinks.Any(o => o.thisParamID == ZoneLinkPin))
                    throw new McpError("'" + door.Name + "' already has its zone_link linked inside " + holder.name + " (get_composite with links shows it), so a link from outside would give it two.");
                door.Pin = ZoneLinkPin;
            }
            else
            {
                Composite instanced = McpScript.InstancedComposite(commands, target);
                if (instanced == null)
                    throw new McpError("'" + door.Name + "' is a " + McpScript.TypeName(commands, holder, target) + ", not a door: give a door variant instance (one with a zone_link pin) or a Door function.");
                if (doorPin != ShortGuid.Invalid)
                {
                    if (HandsPinUp(holder, target, doorPin))
                        throw new McpError("'" + door.Name + "' hands its zone link up to " + composite.name + "'s own zone link pin: the link is given where " + composite.name + " is placed, not here.");
                    if (ids.Count > 1 && target.childLinks.Any(o => o.thisParamID == doorPin))
                        throw new McpError("'" + door.Name + "' already has its " + McpScript.ParamName(doorPin) + " linked inside " + holder.name + " (get_composite with links shows it), so a link from outside would give it two.");
                    door.Pin = doorPin;
                }
                else
                {
                    //No pin to hand a zone link in through: reach the Door inside it, if there is just the one and nothing in there drives its zone link
                    List<FunctionEntity> inner = instanced.functions.Where(o => o.function == FunctionType.Door).ToList();
                    if (inner.Count != 1)
                        throw new McpError(instanced.name + " has no zone_link pin and " + (inner.Count == 0 ? "no Door" : inner.Count + " Doors") + " inside it, so '" + door.Name + "' cannot be given a zone link from here. Give the path to the Door itself.");
                    if (inner[0].childLinks.Any(o => o.thisParamID == ZoneLinkPin))
                        throw new McpError("The Door inside " + instanced.name + " already has its zone_link linked inside that composite (get_composite with links shows it), so a link from outside would give it two.");
                    ids.Add(inner[0].shortGUID);
                    door.Pin = ZoneLinkPin;
                    door.Note = instanced.name + " has no zone_link pin, so the Door inside it was reached through an alias.";
                }
            }

            if (ids.Count == 1)
                door.Owner = composite.GetEntityByID(ids[0]);
            else
                door.AliasPath = ids.Concat(new[] { ShortGuid.Invalid }).ToArray();
            return door;
        }

        /// <summary>How a link joining two zones is to be set up.</summary>
        private sealed class JoinOptions
        {
            public string Name;
            public bool? OpenOnReset;
            public bool? LockOnReset;
            public int? Cost;
            public bool? ExcludeStreaming;
            public bool ReplaceDoorLink;
            public bool AddDoorToZones = true;
            /// <summary>Only for an always-open link: also list this door (a see-through one) in both zones.</summary>
            public DoorSpec ListDoor;
            /// <summary>What a link made without a <see cref="Name"/> is called, before the zones' names (default ZoneLink_ or ZoneExclusionLink_).</summary>
            public string DefaultPrefix;
            /// <summary>In a batch: links a door was unhooked from are collected here, to be judged orphaned once every change is made.</summary>
            public List<FunctionEntity> Orphans;
        }

        /// <summary>The entity in the ZoneLink's composite that a door's zone link goes from: the door itself, or an alias reaching it (found or made).</summary>
        private static Entity DoorOwner(McpScriptEdit edit, Composite composite, DoorSpec door, bool make)
        {
            if (door.Owner != null)
                return door.Owner;
            List<ShortGuid> wanted = door.AliasPath.Take(door.AliasPath.Length - 1).ToList();
            //An alias already giving the door a link is the one to use
            List<AliasEntity> aliases = composite.aliases.Where(o => SamePath(o.alias?.path, wanted)).ToList();
            Entity owner = aliases.FirstOrDefault(o => o.childLinks.Any(l => l.thisParamID == door.Pin)) ?? aliases.FirstOrDefault();
            if (owner == null && make)
                owner = edit.AddAlias(composite, door.AliasPath, null);
            return owner;
        }

        /// <summary>Note the links in <paramref name="candidates"/> that nothing opens any more (a door was unhooked from them).</summary>
        private static void NoteOrphans(Commands commands, Composite composite, IEnumerable<FunctionEntity> candidates, List<string> notes)
        {
            Incoming after = new Incoming(composite);
            foreach (FunctionEntity orphan in candidates.Distinct())
                if (composite.GetEntityByID(orphan.shortGUID) == orphan && !StateOf(composite, orphan, after).Opened)
                    notes.Add(McpScript.EntityName(commands, composite, orphan) + " is now opened by nothing: delete it (delete_entities) unless something is to drive it.");
        }

        /// <summary>The entities of the composite a door's zone link goes from: the door, or every alias reaching it.</summary>
        private static List<Entity> DoorOwners(Composite composite, DoorSpec door)
        {
            if (door.Owner != null) return new List<Entity>() { door.Owner };
            List<ShortGuid> wanted = door.AliasPath.Take(door.AliasPath.Length - 1).ToList();
            return composite.aliases.Where(o => SamePath(o.alias?.path, wanted)).Cast<Entity>().ToList();
        }

        /// <summary>Whether a side entity of a link stands for a zone (a Zone, a zone pin, or an alias of a Zone).</summary>
        private static bool IsZoneSide(Entity side) => IsZone(side) || (side is VariableEntity pin && pin.type == DataType.ZONE);

        /// <summary>
        /// Within an edit: join two zones of <paramref name="composite"/> with a ZoneLink (or, with <paramref name="exclusion"/>,
        /// a ZoneExclusionLink), wired as the game expects, and with <paramref name="door"/> give it the door: the door's zone
        /// link pin drives it, and the door is listed by both zones. A link that already joins the two zones is used rather than
        /// making another when it suits: for a door, one this door drives or one nothing works (each door has its own link, even
        /// between the same two zones); without one, one nothing works. A half-wired link the door already drives is given the
        /// two zones. Everything that can refuse is checked before anything is changed.
        /// </summary>
        private static FunctionEntity JoinZones(McpScriptEdit edit, Composite composite, Entity zoneA, Entity zoneB, bool exclusion, DoorSpec door, JoinOptions options, List<string> notes, out bool reused)
        {
            Commands commands = edit.Commands;
            if (zoneA == zoneB)
                throw new McpError("A zone cannot be linked to itself.");
            if (exclusion && door != null)
                throw new McpError("A ZoneExclusionLink is always open, so a door cannot drive one: link the zones with a ZoneLink (kind 'link') to give it the door.");
            Incoming incoming = new Incoming(composite);
            Entity owner = door == null ? null : DoorOwner(edit, composite, door, make: false);
            FunctionType type = exclusion ? FunctionType.ZoneExclusionLink : FunctionType.ZoneLink;
            string nameA = McpScript.EntityName(commands, composite, zoneA), nameB = McpScript.EntityName(commands, composite, zoneB);
            bool Joins(FunctionEntity candidate)
            {
                (Entity a, Entity b) = SidesOf(commands, composite, candidate);
                return (a == zoneA && b == zoneB) || (a == zoneB && b == zoneA);
            }

            //What the door already drives: the right link, a half-wired one to repair, or others
            FunctionEntity link = null, repair = null;
            List<string> others = new List<string>();
            if (owner != null)
            {
                foreach (EntityConnector connector in owner.childLinks.Where(o => o.thisParamID == door.Pin).ToList())
                {
                    Entity target = composite.GetEntityByID(connector.linkedEntityID);
                    if (target is FunctionEntity driven && driven.function == FunctionType.ZoneLink && connector.linkedParamID == ShortGuids.reference)
                    {
                        if (link == null && Joins(driven)) { link = driven; continue; }
                        (Entity a, Entity b) = SidesOf(commands, composite, driven);
                        if (repair == null && (!IsZoneSide(a) || !IsZoneSide(b))) { repair = driven; continue; }
                    }
                    others.Add(target == null ? McpScript.Id(connector.linkedEntityID) + " (missing)" : McpScript.EntityName(commands, composite, target));
                }
            }
            if (link != null && repair != null) { others.Add(McpScript.EntityName(commands, composite, repair)); repair = null; }
            if (link == null && repair == null && others.Count != 0 && !options.ReplaceDoorLink)
                throw new McpError("The door '" + door.Name + "' already has its " + McpScript.ParamName(door.Pin) + " linked to " + string.Join(", ", others) + ". A door drives the one link between the zones either side of it: pass replace_door_link (replace_door_links for link_zones) to point it at a link between " + nameA + " and " + nameB + " instead, or check that link with get_zone_links.");

            List<string> alsoJoining = new List<string>();
            if (link == null && repair == null)
            {
                //An open join wants a link that ends up always open: one locked on reset stays shut, so it is left as it is
                bool wantsOpen = door == null && options.OpenOnReset != false && options.LockOnReset != true;
                foreach (FunctionEntity existing in composite.functions)
                {
                    if (!IsZoneLink(existing) || !Joins(existing)) continue;
                    if (existing.function != type) { alsoJoining.Add(McpScript.EntityName(commands, composite, existing) + " (" + existing.function.AsFunctionType + ")"); continue; }
                    if (exclusion) { link = existing; break; }
                    LinkState state = StateOf(composite, existing, incoming);
                    bool locked = wantsOpen && state.Untouched && state.LockOnReset;
                    bool suits = door != null
                        ? (owner != null && state.Drivers.Contains(owner)) || (state.Untouched && !state.OpenOnReset)
                        : state.Untouched && !locked;
                    if (suits) { link = existing; break; }
                    alsoJoining.Add(McpScript.EntityName(commands, composite, existing) + (state.Drivers.Count != 0 ? " (driven by " + string.Join(", ", state.Drivers.Select(o => McpScript.EntityName(commands, composite, o))) + ")" : state.Methods.Count != 0 || state.OpenFrom.Count != 0 ? " (worked by script)" : locked ? " (locked on reset)" : ""));
                }
            }
            if (alsoJoining.Count != 0)
                notes.Add("Also joining these zones: " + string.Join("; ", alsoJoining) + ".");

            reused = link != null || repair != null;
            if (repair != null)
            {
                //Keep a side that is already right, and give the link the zones it is missing
                link = repair;
                (Entity a, Entity b) = SidesOf(commands, composite, link);
                Entity wantA = zoneA, wantB = zoneB;
                if (a == zoneB || b == zoneA) { wantA = zoneB; wantB = zoneA; }
                if (a != wantA) { edit.RemoveLinks(composite, link, "ZoneA", null, null); edit.AddLink(composite, link, "ZoneA", wantA, wantA is VariableEntity pinA ? McpScript.ParamName(pinA.name) : "reference"); }
                if (b != wantB) { edit.RemoveLinks(composite, link, "ZoneB", null, null); edit.AddLink(composite, link, "ZoneB", wantB, wantB is VariableEntity pinB ? McpScript.ParamName(pinB.name) : "reference"); }
                notes.Add(McpScript.EntityName(commands, composite, link) + ", which the door already drove, was half-wired: it now joins " + nameA + " and " + nameB + ".");
            }
            else if (link == null)
            {
                string name = options.Name ?? FreeName(edit, composite, (options.DefaultPrefix ?? (exclusion ? "ZoneExclusionLink_" : "ZoneLink_")) + nameA + "_" + nameB);
                link = edit.AddFunction(composite, type, name);
                edit.AddLink(composite, link, "ZoneA", zoneA, zoneA is VariableEntity pinA ? McpScript.ParamName(pinA.name) : "reference");
                edit.AddLink(composite, link, "ZoneB", zoneB, zoneB is VariableEntity pinB ? McpScript.ParamName(pinB.name) : "reference");
            }
            else
                notes.Add(McpScript.EntityName(commands, composite, link) + " already joined " + nameA + " and " + nameB + ", so it was used rather than making another.");
            if (options.Name != null && reused && !string.Equals(McpScript.EntityName(commands, composite, link), options.Name.Trim(), StringComparison.Ordinal))
                edit.Rename(composite, link, options.Name);

            //The door belongs to both rooms: listed by both zones, it is streamed and drawn with either
            void List(DoorSpec listed)
            {
                if (!options.AddDoorToZones || listed?.ContentPath == null) return;
                foreach (Entity zone in new[] { zoneA, zoneB })
                {
                    if (!IsZone(zone)) continue;
                    ContentChange change = new ContentChange();
                    ChangeContents(edit, composite, zone, new List<ShortGuid[]>() { listed.ContentPath }, new List<ShortGuid[]>(), change);
                    if (change.Added.Count != 0) notes.Add("Added the door " + listed.Name + " to " + McpScript.EntityName(commands, composite, zone) + "'s contents.");
                    notes.AddRange(change.Notes);
                }
            }

            if (door != null)
            {
                owner = DoorOwner(edit, composite, door, make: true);
                string pinName = McpScript.ParamName(door.Pin);
                if (others.Count != 0)
                {
                    if (options.ReplaceDoorLink)
                    {
                        //Every link off the pin goes, then this one is made fresh
                        List<FunctionEntity> left = owner.childLinks.Where(o => o.thisParamID == door.Pin && o.linkedEntityID != link.shortGUID).Select(o => composite.GetEntityByID(o.linkedEntityID) as FunctionEntity).Where(o => o != null && o.function == FunctionType.ZoneLink).ToList();
                        edit.RemoveLinks(composite, owner, pinName, null, null);
                        notes.Add("The door's " + pinName + " was linked to " + string.Join(", ", others) + "; it now drives " + McpScript.EntityName(commands, composite, link) + ".");
                        if (options.Orphans != null)
                            options.Orphans.AddRange(left);
                        else
                            NoteOrphans(commands, composite, left, notes);
                    }
                    else
                        notes.Add("The door's " + pinName + " also drives " + string.Join(", ", others) + ": a door drives the one link between the zones either side of it.");
                }
                bool already = owner.childLinks.Any(o => o.thisParamID == door.Pin && o.linkedEntityID == link.shortGUID && o.linkedParamID == ShortGuids.reference);
                if (!already)
                    edit.AddLink(composite, owner, pinName, link, "reference");
                if (door.Note != null) notes.Add(door.Note);
                List(door);
            }
            List(options.ListDoor);

            if (exclusion)
            {
                if (options.ExcludeStreaming != null && Flag(link, ExcludeStreaming) != options.ExcludeStreaming.Value)
                    edit.SetParameter(composite, link, "exclude_streaming", new JValue(options.ExcludeStreaming.Value));
                return link;
            }

            //No door and nothing else to open it: an open doorway, so open from the start
            bool? open = options.OpenOnReset;
            if (open == null && door == null)
            {
                LinkState state = StateOf(composite, link, new Incoming(composite));
                if (state.Untouched && !state.OpenOnReset)
                {
                    open = true;
                    if (options.ListDoor == null)
                        notes.Add("No door was given and nothing opens " + McpScript.EntityName(commands, composite, link) + ", so it was set open_on_reset (an open doorway). Give the door instead if there is one (door 'auto' finds it).");
                }
            }
            if (open != null && Flag(link, OpenOnReset) != open.Value)
                edit.SetParameter(composite, link, "open_on_reset", new JValue(open.Value));
            if (options.LockOnReset != null && Flag(link, LockOnReset) != options.LockOnReset.Value)
                edit.SetParameter(composite, link, "lock_on_reset", new JValue(options.LockOnReset.Value));
            if (options.Cost != null && !(link.GetParameter(Cost)?.content is cInteger cost && cost.value == options.Cost.Value))
                edit.SetParameter(composite, link, "cost", new JValue(options.Cost.Value));
            return link;
        }

        /// <summary>A window door's link is open from the start and the door does not drive it: asked for it shut or locked, say so rather than ignore it.</summary>
        private static void RequireOpenForWindow(DoorSpec door, JoinOptions options)
        {
            if (options.OpenOnReset == false || options.LockOnReset == true)
                throw new McpError("'" + door.Name + "' has a window: its rooms see each other with it shut, so they are joined by a ZoneLink open on reset that the door does not drive, and open_on_reset false or lock_on_reset true would leave the far room unloaded in plain view. Leave those out, or pass replace_door_link to have the door drive a link anyway.");
        }

        /// <summary>The options for a window door's always-open link: those asked for that still apply (cost), with the door listed by both zones.</summary>
        private static JoinOptions WindowOptions(DoorSpec door, JoinOptions options, string name) => new JoinOptions()
        {
            Name = name,
            OpenOnReset = true,
            LockOnReset = options.LockOnReset,
            Cost = options.Cost,
            AddDoorToZones = options.AddDoorToZones,
            ListDoor = door,
            DefaultPrefix = "ZoneLink_Open_",
        };

        private static object CreateZoneLink(McpCall call)
        {
            if (call.Has("cost") && call.Int("cost") < 0)
                throw new McpError("'cost' cannot be negative.");
            string kind = (call.Str("kind") ?? "link").Trim().ToLowerInvariant();
            if (kind != "link" && kind != "exclusion")
                throw new McpError("'kind' is 'link' (a ZoneLink: a gate that opens and closes, driven by a door or script, or open on reset) or 'exclusion' (a ZoneExclusionLink: always open, between neighbouring parts of one space).");
            bool exclusion = kind == "exclusion";
            if (exclusion && (call.Has("open_on_reset") || call.Has("lock_on_reset") || call.Has("cost") || call.Has("door") || call.Has("replace_door_link")))
                throw new McpError("A ZoneExclusionLink is always open: door, open_on_reset, lock_on_reset, cost and replace_door_link are for kind 'link'.");
            if (!exclusion && call.Has("exclude_streaming"))
                throw new McpError("'exclude_streaming' is for kind 'exclusion'.");
            List<string> doorSteps = call.Has("door") ? McpScriptTools.ReadPath(call.Token("door")) : null;
            bool autoDoor = doorSteps != null && doorSteps.Count == 1 && string.Equals(doorSteps[0].Trim(), "auto", StringComparison.OrdinalIgnoreCase);
            if (autoDoor && call.Has("name"))
                throw new McpError("With door 'auto' each door found gets its own link, so 'name' cannot be given.");
            string page = call.Str("page") ?? "Zones";
            Composite composite = null;
            McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                CompileShown();
                composite = ZoningComposite(call, commands);
                //The door is added to the zones' sequences: an editor open on one holds the list the edit replaces
                CloseSequenceEditors(composite.functions.OfType<TriggerSequence>());
            });
            JoinOptions options = new JoinOptions()
            {
                Name = call.Str("name"),
                OpenOnReset = call.Has("open_on_reset") ? call.Bool("open_on_reset") : (bool?)null,
                LockOnReset = call.Has("lock_on_reset") ? call.Bool("lock_on_reset") : (bool?)null,
                Cost = call.Has("cost") ? call.Int("cost") : (int?)null,
                ExcludeStreaming = call.Has("exclude_streaming") ? call.Bool("exclude_streaming") : (bool?)null,
                ReplaceDoorLink = call.Bool("replace_door_link"),
                AddDoorToZones = call.Bool("add_door_to_zones", true),
            };

            List<FunctionEntity> made = new List<FunctionEntity>();
            bool reused = false;
            List<string> notes = new List<string>();
            JArray doorsFound = null;
            McpScriptEdit.Outcome outcome;
            using (McpEditorTools.Heartbeat(call, autoDoor ? "Finding the doors and linking" : "Linking zones"))
                outcome = McpScriptEdit.Run(exclusion ? "Link neighbouring zones" : "Link zones", composite, edit =>
                {
                    Commands commands = edit.Commands;
                    edit.UsePage(composite, page);
                    Entity zoneA = ZoneIn(commands, composite, call.Str("zone_a", required: true), "zone_a", allowPin: true);
                    Entity zoneB = ZoneIn(commands, composite, call.Str("zone_b", required: true), "zone_b", allowPin: true);
                    if (zoneA == zoneB)
                        throw new McpError("zone_a and zone_b are the same zone.");
                    if (!autoDoor)
                    {
                        DoorSpec door = doorSteps != null ? FindDoor(commands, composite, doorSteps) : null;
                        //A door with a window, as analyse_zones finds them (by its type)
                        bool seeThrough = door != null && !options.ReplaceDoorLink && McpZonePlanner.Geometry.IsWindowType(door.Type);
                        if (seeThrough)
                        {
                            //A window door's rooms see each other with it shut: an always-open link the door does not drive, as vanilla has it
                            RequireOpenForWindow(door, options);
                            made.Add(JoinZones(edit, composite, zoneA, zoneB, false, null, WindowOptions(door, options, options.Name), notes, out reused));
                            notes.Add("'" + door.Name + "' has a window: its rooms see each other with it shut, so they were joined by a ZoneLink open on reset that the door does not drive (as vanilla has it)" + (options.AddDoorToZones ? ", and the door was listed in both zones" : "") + ". Pass replace_door_link to make the door drive a link anyway.");
                        }
                        else
                            made.Add(JoinZones(edit, composite, zoneA, zoneB, exclusion, door, options, notes, out reused));
                        edit.Select.Add(made[0]);
                        return;
                    }

                    //The doors standing between the two zones, as analyse_zones finds them
                    Scene scene = BuildScene(commands, composite, call.Cancel);
                    Analysis analysis = Analyse(commands, scene);
                    string labelA = McpScript.Id(zoneA.shortGUID), labelB = McpScript.Id(zoneB.shortGUID);
                    List<DoorState> between = analysis.Doors.Where(o => o.Joins != null && ((o.Joins.Value.a == labelA && o.Joins.Value.b == labelB) || (o.Joins.Value.a == labelB && o.Joins.Value.b == labelA))).ToList();
                    if (between.Count == 0)
                    {
                        List<string> near = analysis.Doors.Where(o => o.Joins != null && (o.Joins.Value.a == labelA || o.Joins.Value.b == labelA || o.Joins.Value.a == labelB || o.Joins.Value.b == labelB))
                            .Take(8).Select(o => o.Door.Name + " (" + scene.NameOf(o.Joins.Value.a) + " - " + scene.NameOf(o.Joins.Value.b) + ")").ToList();
                        throw new McpError("No door stands between " + scene.NameOf(labelA) + " and " + scene.NameOf(labelB) + " (by the zones listing doors, then by the content either side of each)." +
                            (near.Count != 0 ? " Doors touching either: " + string.Join("; ", near) + "." : "") + " Give the door's path, or leave 'door' out for an open archway (a ZoneLink open on reset).");
                    }
                    doorsFound = new JArray();
                    foreach (DoorState state in between)
                    {
                        DoorSpec door = FindDoor(commands, composite, state.Door.Path.Split('/').ToList());
                        JObject found = new JObject() { ["door"] = state.Door.Name, ["found_by"] = state.JoinsFrom };
                        if (state.SeeThrough)
                        {
                            //A window door's rooms see each other with it shut: an always-open link the door does not drive, as vanilla has it
                            RequireOpenForWindow(door, options);
                            FunctionEntity link = JoinZones(edit, composite, zoneA, zoneB, false, null, WindowOptions(door, options, null), notes, out bool wasThere);
                            found["link"] = McpScript.EntityName(commands, composite, link);
                            found["note"] = "It has a window: given an always-open link it does not drive.";
                            reused |= wasThere;
                            if (!made.Contains(link)) made.Add(link);
                        }
                        else
                        {
                            FunctionEntity link = JoinZones(edit, composite, zoneA, zoneB, false, door, new JoinOptions() { OpenOnReset = options.OpenOnReset, LockOnReset = options.LockOnReset, Cost = options.Cost, ReplaceDoorLink = options.ReplaceDoorLink, AddDoorToZones = options.AddDoorToZones }, notes, out bool wasThere);
                            found["link"] = McpScript.EntityName(commands, composite, link);
                            reused |= wasThere;
                            if (!made.Contains(link)) made.Add(link);
                        }
                        doorsFound.Add(found);
                    }
                    foreach (FunctionEntity link in made) edit.Select.Add(link);
                });
            foreach (string note in notes.Distinct()) call.Note(note);

            JObject result = McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: false);
                HashSet<Composite> placed = McpBrowseTools.Reachable(commands);
                if (made.Count == 1 && doorsFound == null)
                    return DescribeLink(commands, composite, made[0], out Entity _, out Entity _, out List<string> _, placed);
                return new JObject() { ["links"] = new JArray(made.Select(o => DescribeLink(commands, composite, o, out Entity _, out Entity _, out List<string> _, placed))) };
            });
            if (doorsFound != null) result["doors"] = doorsFound;
            result["reused"] = reused;
            if (!outcome.Changed) result["unchanged"] = "The link was already wired exactly like that.";
            result["next"] = "save_level with build=true, then check_zones near the doorway. " + BuildNote;
            return result;
        }
        #endregion

        #region Checking an area
        private static object CheckZones(McpCall call)
        {
            bool near = call.Has("near");
            bool box = call.Has("box_min") || call.Has("box_max");
            if (!near && !box)
                throw new McpError("Say where to look: near + radius (metres), or box_min + box_max, in world positions. A doorway's position is a good centre (find_entities or get_placements give positions).");
            if (near && box)
                throw new McpError("Look near a point or inside a box, not both.");
            if (near && !call.Has("radius"))
                throw new McpError("'near' needs 'radius' (metres).");
            if (call.Has("radius") && !near)
                throw new McpError("'radius' goes with 'near'.");
            if (box && !(call.Has("box_min") && call.Has("box_max")))
                throw new McpError("A box needs both 'box_min' and 'box_max'.");
            Vector3 centre = near ? McpValues.ReadVector(call.Token("near"), "near", null) : Vector3.Zero;
            double radius = near ? call.Num("radius") : 0;
            if (radius < 0)
                throw new McpError("'radius' cannot be negative.");
            Vector3 min = Vector3.Zero, max = Vector3.Zero;
            if (box)
            {
                Vector3 a = McpValues.ReadVector(call.Token("box_min"), "box_min", null);
                Vector3 b = McpValues.ReadVector(call.Token("box_max"), "box_max", null);
                min = Vector3.Min(a, b);
                max = Vector3.Max(a, b);
            }
            bool Inside(Vector3 at)
            {
                if (near) return Vector3.Distance(at, centre) <= radius;
                return at.X >= min.X && at.Y >= min.Y && at.Z >= min.Z && at.X <= max.X && at.Y <= max.Y && at.Z <= max.Z;
            }
            int limit = Math.Max(1, call.Int("limit", 15));

            using (McpEditorTools.Heartbeat(call, "Checking zones"))
            return McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: false);
                Commands commands = content.Level.Commands;
                CompileShown();
                Composite root = commands.EntryPoints[0];
                Composite composite = call.Has("composite") ? McpScript.FindComposite(commands, call.Str("composite")) : AreaComposite(call, commands, Inside);
                Scene scene = BuildScene(commands, composite, call.Cancel);
                Analysis analysis = Analyse(commands, scene);
                List<string> problems = new List<string>();
                JObject result = new JObject() { ["composite"] = composite.name };
                if (near) result["area"] = new JObject() { ["near"] = McpValues.Vector(centre), ["radius"] = radius };
                else result["area"] = new JObject() { ["box_min"] = McpValues.Vector(min), ["box_max"] = McpValues.Vector(max) };
                if (scene.Truncated) result["truncated"] = "The walk of " + composite.name + " stopped early (it is very large): some content was not looked at.";
                if (scene.Placements > 1) result["placements_note"] = composite.name + " is placed " + scene.Placements + " times; positions are those of its first placement.";

                Dictionary<string, McpZonePlanner.Item> itemAt = new Dictionary<string, McpZonePlanner.Item>();
                foreach (McpZonePlanner.Item item in scene.Items) itemAt[item.Path] = item;
                string Describe(McpZonePlanner.Leaf leaf)
                {
                    int cut = leaf.Path.IndexOf('/');
                    string top = cut < 0 ? leaf.Path : leaf.Path.Substring(0, cut);
                    return leaf.Type + (itemAt.TryGetValue(top, out McpZonePlanner.Item item) ? " in " + item.Name : "") + " at " + McpValues.Vector(scene.World(leaf.Position)).ToString(Newtonsoft.Json.Formatting.None);
                }

                Dictionary<string, (int count, JArray examples)> here = new Dictionary<string, (int, JArray)>();
                int contentCount = 0, unzoned = 0, inTwo = 0;
                JArray unzonedExamples = new JArray(), inTwoExamples = new JArray();
                HashSet<string> scriptHere = new HashSet<string>();
                foreach (McpZonePlanner.Leaf leaf in scene.Leaves)
                {
                    if (!Inside(scene.World(leaf.Position))) continue;
                    contentCount++;
                    bool inDoor = scene.DoorOf(leaf.Path) != null;
                    List<string> claims = scene.ClaimsOf(leaf.Path);
                    if (scene.ScriptClaims.TryGetValue(leaf.Path, out List<string> scripted)) scriptHere.UnionWith(scripted);
                    if (claims.Count == 0)
                    {
                        unzoned++;
                        if (unzonedExamples.Count < limit) unzonedExamples.Add(Describe(leaf) + (inDoor ? " (part of a door)" : ""));
                        continue;
                    }
                    //A door belongs to both rooms: its parts in two zones are right
                    if (claims.Count > 1 && !inDoor)
                    {
                        inTwo++;
                        if (inTwoExamples.Count < limit) inTwoExamples.Add(Describe(leaf) + ": " + string.Join(", ", claims.Select(scene.NameOf)));
                    }
                    foreach (string zone in claims)
                    {
                        if (!here.TryGetValue(zone, out (int count, JArray examples) tally)) tally = (0, new JArray());
                        tally.count++;
                        if (tally.examples.Count < 5) tally.examples.Add(Describe(leaf));
                        here[zone] = tally;
                    }
                }
                result["content_here"] = contentCount;

                JArray zoneList = new JArray();
                foreach (KeyValuePair<string, (int count, JArray examples)> pair in here.OrderByDescending(o => o.Value.count))
                {
                    JObject zone = new JObject() { ["name"] = scene.NameOf(pair.Key), ["id"] = pair.Key, ["content_here"] = pair.Value.count, ["examples"] = pair.Value.examples };
                    if (!scene.Zones.ContainsKey(pair.Key)) zone["note"] = "Inside an instance " + composite.name + " places: its links are made in there, or reach it through that instance's zone pins.";
                    zoneList.Add(zone);
                }
                result["zones"] = zoneList;
                if (scriptHere.Count != 0)
                    result["script_zones"] = new JObject() { ["zones"] = new JArray(scriptHere.Select(scene.NameOf)), ["note"] = "Loaded by script (animations, say), not by where the player is: not rooms, and not what decides streaming here." };

                //The links touching those zones
                HashSet<string> zonesHere = new HashSet<string>(here.Keys);
                JArray linkList = new JArray();
                HashSet<Composite> placed = McpBrowseTools.Reachable(commands);
                Incoming incoming = new Incoming(composite);
                foreach (LinkInfo link in analysis.Links)
                {
                    if (!(link.A != null && zonesHere.Contains(link.A)) && !(link.B != null && zonesHere.Contains(link.B))) continue;
                    if (link.Own)
                    {
                        JObject described = DescribeLink(commands, composite, link.Link, out Entity _, out Entity _, out List<string> linkProblems, placed, incoming);
                        linkList.Add(described);
                        foreach (string problem in linkProblems) problems.Add((string)described["name"] + ": " + problem);
                    }
                    else
                    {
                        linkList.Add(new JObject()
                        {
                            ["name"] = McpScript.EntityName(commands, link.Holder, link.Link),
                            ["type"] = link.Link.function.AsFunctionType.ToString(),
                            ["composite"] = link.Holder.name,
                            ["joins"] = new JArray(scene.NameOf(link.A), scene.NameOf(link.B)),
                            ["always_open"] = link.State.AlwaysOpen,
                        });
                        if (!link.State.Opened) problems.Add(McpScript.EntityName(commands, link.Holder, link.Link) + " (in " + link.Holder.name + "): nothing opens it.");
                    }
                }
                result["zone_links"] = linkList;

                //Pairs of zones here that neighbour each other (or a door joins) with nothing joining them
                List<string> zoneOrder = here.Keys.ToList();
                for (int i = 0; i < zoneOrder.Count; i++)
                    for (int j = i + 1; j < zoneOrder.Count; j++)
                    {
                        string a = zoneOrder[i], b = zoneOrder[j];
                        if (analysis.Joining(a, b).Count != 0) continue;
                        DoorState door = analysis.Doors.FirstOrDefault(o => o.Joins != null && ((o.Joins.Value.a == a && o.Joins.Value.b == b) || (o.Joins.Value.a == b && o.Joins.Value.b == a)));
                        McpZonePlanner.Pair pair = analysis.Pairs.FirstOrDefault(o => (o.A == a && o.B == b) || (o.A == b && o.B == a));
                        if (door == null && pair == null) continue;
                        string why = door != null ? "the door " + door.Door.Name + " stands between them" : "their content meets (" + pair.Contacts + " places) with no door between";
                        if (!scene.Linkable(a) || !scene.Linkable(b))
                            problems.Add("Nothing joins '" + scene.NameOf(a) + "' and '" + scene.NameOf(b) + "' (" + why + "), and one of them is inside an instance: link it in there, or through that instance's zone pin.");
                        else
                            problems.Add("Nothing joins '" + scene.NameOf(a) + "' and '" + scene.NameOf(b) + "' (" + why + "), so each pops in as the player crosses: " + (door != null ? "create_zone_link with door 'auto'" : "create_zone_link without a door (a ZoneLink open on reset)") + " (or link_zones for every missing link).");
                    }

                if (unzoned != 0)
                {
                    result["unzoned"] = new JObject() { ["count"] = unzoned, ["examples"] = unzonedExamples };
                    if (here.Count != 0)
                        problems.Add(unzoned + " of the " + contentCount + " models, lights, effects and collision here are in no zone. They go in the global zone: drawn from everywhere, and collision in no zone leaves the player in no room's zone while standing on it, so the rooms around may not be streamed. Add each to the zone of the room it is in with set_zone_contents.");
                    else
                        problems.Add("Nothing here is in a zone, so zone streaming is not what decides what is drawn here: everything is in the always-loaded global zone.");
                }
                if (inTwo != 0)
                    result["in_two_zones"] = new JObject() { ["count"] = inTwo, ["examples"] = inTwoExamples, ["note"] = "Drawn while either zone is: right for something two rooms share, otherwise usually a mistake (set_zone_contents with on_claimed 'move' gives it to one)." };

                JArray doorList = new JArray();
                List<DoorState> doors = analysis.Doors.Where(o => Inside(scene.World(o.Door.Position))).ToList();
                foreach (DoorState door in doors)
                {
                    List<string> doorProblems = DoorProblems(analysis, door);
                    if (doorList.Count < Math.Max(limit, 10)) doorList.Add(DescribeDoorState(commands, analysis, door, doorProblems));
                    foreach (string problem in doorProblems) problems.Add("Door " + door.Door.Name + ": " + problem);
                }
                result["doors"] = doorList;
                if (doors.Count > doorList.Count) result["doors_note"] = "Showing " + doorList.Count + " of " + doors.Count + " doors.";

                //Content placed outside the composite is in the global zone and out of reach of its zones (a room built in root, say)
                JArray outsideExamples = new JArray();
                int outside = OutsideContent(call, commands, composite, Inside, limit, outsideExamples, out string outsideHolder);
                if (outside != 0)
                {
                    result["outside_composite"] = new JObject() { ["count"] = outside, ["examples"] = outsideExamples };
                    problems.Add(outside + " models, lights, effects and collision here are placed outside " + composite.name + " (e.g. in " + outsideHolder + ") and no zone claims them: they are in the global zone, drawn from everywhere. A zone of " + composite.name + " only claims what is placed inside it: place them inside " + composite.name + " (create them there rather than in root), then add them to a room's zone with set_zone_contents or auto_zone.");
                }

                result["problems"] = new JArray(problems);
                result["note"] = (problems.Count == 0 ? "Nothing wrong found with the zones here. " : "") + BuildNote + " Changes made since the last build are not in the game yet.";
                return result;
            });
        }

        /// <summary>
        /// Content placed outside <paramref name="composite"/> (inside <paramref name="inside"/>) that no zone claims: in the global zone, and out
        /// of reach of the composite's zones, which only claim what is placed inside it. Counts it, with a few examples. Content the
        /// level itself leaves there (under an instance root already held when it was loaded: its mission, character and UI logic)
        /// is not counted; content placed straight in root always is.
        /// </summary>
        internal static int OutsideContent(McpCall call, Commands commands, Composite composite, Func<Vector3, bool> inside, int limit, JArray examples, out string holder)
        {
            holder = null;
            Composite root = commands.EntryPoints[0];
            if (composite == root) return 0;
            HashSet<ShortGuid> atLoad = RootAtLoad(commands);
            HashSet<string> claimed = new HashSet<string>();
            foreach (SyncedZone zone in ZoneMembership.CalculateFrom(commands, root))
                foreach (List<uint> path in zone.roots)
                    if (path != null && path.Count != 0) claimed.Add(string.Join("/", path));
            int count = 0;
            string firstHolder = null;
            McpPlacements walker = new McpPlacements(commands);
            walker.Walk(root, step =>
            {
                if (!(step.Entity is FunctionEntity function) || !_content.Contains(function.function)) return true;
                if (step.World == null || !inside(step.World.position)) return true;
                if (step.Chain.Count > 1 && atLoad.Contains(step.Chain[0].shortGUID)) return true;
                string key = null;
                foreach (Entity stepped in step.Chain)
                {
                    key = key == null ? stepped.shortGUID.AsUInt32.ToString() : key + "/" + stepped.shortGUID.AsUInt32;
                    if (claimed.Contains(key)) return true;
                }
                count++;
                if (firstHolder == null) firstHolder = step.Composite.name;
                if (examples.Count < limit)
                    examples.Add(McpScript.EntityName(commands, step.Composite, function) + " (" + function.function.AsFunctionType + " in " + McpScript.CompositeLeaf(step.Composite) + ") at " + McpValues.Vector(step.World.position).ToString(Newtonsoft.Json.Formatting.None));
                return true;
            }, c => c != composite, call.Cancel);
            holder = firstHolder;
            return count;
        }

        /// <summary>
        /// The composite whose zones claim most of the content in an area: each zone counts for the composite under the root it
        /// is in (a lift's zones count for the environment composite placing the lift). The zoning composite when nothing is zoned.
        /// </summary>
        private static Composite AreaComposite(McpCall call, Commands commands, Func<Vector3, bool> inside)
        {
            Composite root = commands.EntryPoints[0];
            List<SyncedZone> zones = ZoneMembership.CalculateFrom(commands, root);
            Dictionary<string, List<Composite>> claimedAt = new Dictionary<string, List<Composite>>();
            foreach (SyncedZone zone in zones)
            {
                Composite owner = zone.zone_path.Count == 0 ? root : McpScript.InstancedComposite(commands, root.GetEntityByID(new ShortGuid(zone.zone_path[0])));
                if (owner == null) continue;
                foreach (List<uint> claimed in zone.roots)
                {
                    if (claimed == null || claimed.Count == 0) continue;
                    string key = string.Join("/", claimed);
                    if (!claimedAt.TryGetValue(key, out List<Composite> list)) claimedAt[key] = list = new List<Composite>();
                    if (!list.Contains(owner)) list.Add(owner);
                }
            }
            Dictionary<Composite, int> votes = new Dictionary<Composite, int>();
            McpPlacements walker = new McpPlacements(commands);
            walker.Walk(root, step =>
            {
                if (!(step.Entity is FunctionEntity function) || !_content.Contains(function.function)) return true;
                if (step.World == null || !inside(step.World.position)) return true;
                string key = null;
                foreach (Entity stepped in step.Chain)
                {
                    key = key == null ? stepped.shortGUID.AsUInt32.ToString() : key + "/" + stepped.shortGUID.AsUInt32;
                    if (claimedAt.TryGetValue(key, out List<Composite> owners))
                        foreach (Composite owner in owners) votes[owner] = votes.TryGetValue(owner, out int count) ? count + 1 : 1;
                }
                return true;
            }, null, call.Cancel);
            return votes.Count == 0 ? DefaultZoningComposite(commands) : votes.OrderByDescending(o => o.Value).First().Key;
        }
        #endregion
    }
}

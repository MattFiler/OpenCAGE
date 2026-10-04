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

namespace OpenCAGE.MCP
{
    /// <summary>
    /// Zoning a level wholesale: seeing how a composite's content, zones, doors and links fit together, linking every
    /// door and neighbouring pair of zones, and splitting unzoned content into new zones.
    /// </summary>
    /// <remarks>
    /// Zones and the links between them live in one composite - the one holding the level's geometry (a vanilla level's
    /// ENVIRONMENT composite, or the root of a level made from scratch) - since a link only reaches zones in its own
    /// composite. These tools read that composite's placed content through <see cref="McpPlacements"/> and hand it to
    /// <see cref="McpZonePlanner.Geometry"/>, which decides from positions alone; what they write goes through the same
    /// <see cref="JoinZones"/> and <see cref="ChangeContents"/> the single-link tools use, as one undo step. The edits plan
    /// inside the step, on the script as the step sees it.
    /// </remarks>
    internal static partial class McpZoneTools
    {
        private static readonly string[] OpenPairOptions = { "exclusion", "open_link", "none" };

        private static IEnumerable<McpTool> PlanTools()
        {
            yield return new McpTool()
            {
                Name = "analyse_zones",
                Title = "Analyse zones",
                Description = "How a level's zoning fits together, in the composite that holds its zones and geometry: each zone with how much content it claims and where; content no zone claims; every placed door variant (an instance of a door composite with a zone link pin, e.g. AYZ\\Doors\\Door_SML) with the zones listing it, the two zones it stands between (from the zones listing it, else from the content either side), whether it is a dead end or has a window, and the ZoneLink its pin drives; " +
                    "every pair of neighbouring zones, as 'door' (a door between them: needs a ZoneLink the door drives) or 'open' (they see each other with no door shut between: needs an always-open link - vanilla uses a ZoneLink open on reset for this), with what already joins it; and what is missing. link_zones makes what is missing; auto_zone zones unzoned content.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", CompositeHelp),
                    McpSchema.Integer("limit", "At most this many entries per list (default 100).")),
                ReadOnly = true,
                Idempotent = true,
                Run = AnalyseZones,
            };

            yield return new McpTool()
            {
                Name = "link_zones",
                Title = "Link zones",
                Description = "Make every link the zoning is missing, as analyse_zones finds it: for each door variant between two zones, a ZoneLink its zone link pin drives (each door its own; a door already driving the right link is left alone, and a half-wired link it drives is repaired), with the door listed by both zones; a door with a window instead gets a ZoneLink open on reset that it does not drive, as vanilla has it; a dead-end door is listed by the zone it opens from. " +
                    "For each pair of zones that see each other with no door between and nothing joining them yet: the always-open link 'open_pairs' asks for. Links only join zones of 'composite'. dry_run lists what it would do. One undo step. Then save_level with build=true.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", CompositeHelp),
                    McpSchema.Boolean("doors", "Link the doors (default true)."),
                    McpSchema.String("open_pairs", "What joins neighbouring zones with no door between: 'open_link' (default: a ZoneLink open on reset, as vanilla joins zones whose content touches), 'exclusion' (a ZoneExclusionLink - vanilla levels use these only between zones further apart), or 'none'.", options: OpenPairOptions),
                    McpSchema.Boolean("exclusions", "false is the same as open_pairs 'none'; true the same as open_pairs 'exclusion'."),
                    McpSchema.Strings("zones", "Only pairs involving any of these zones (names or ids)."),
                    McpSchema.Strings("between", "Only the pair of these two zones (names or ids)."),
                    McpSchema.Array("skip_doors", "Doors to leave alone (paths or names, as analyse_zones lists them).", PathSchema),
                    McpSchema.Boolean("add_doors_to_zones", "List each door in both zones it joins (default true)."),
                    McpSchema.Boolean("replace_door_links", "Re-point a door whose pin drives a link between other zones (default false: such doors are reported)."),
                    McpSchema.Boolean("dry_run", "Only say what would be made."),
                    McpSchema.Integer("limit", "At most this many entries per list in the result (default 200).")),
                Run = LinkZones,
            };

            yield return new McpTool()
            {
                Name = "auto_zone",
                Title = "Zone a level automatically",
                Description = "Split a composite's unzoned content into zones by where it is placed - rooms cut at their doors and walls, vent ducts apart from the rooms they pass, large spaces split up; unzoned content among an existing zone's goes to that zone - then make a Zone (suspend_on_unload, as vanilla's) and a TriggerSequence listing its content for each, list each door variant in both zones it joins, and (with link) make the links as link_zones would: a ZoneLink each door drives, open links for doors with windows, always-open links between neighbouring zones. " +
                    "dry_run (the default) returns the plan: each proposed zone's number, size, place, content, doors and neighbours, to check, merge or drop and name before making it. Content already in a zone is left where it is, and new zones are linked to existing ones too. One undo step. Then save_level with build=true and check_zones where it matters.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", CompositeHelp),
                    McpSchema.Boolean("dry_run", "Only return the plan (default true). Pass false to make it."),
                    McpSchema.Strings("names", "Names for the zones, in number order of the zones left after merge/drop (default <prefix>N, numbered past names already taken)."),
                    McpSchema.String("prefix", "Name prefix for zones without a name in 'names' (default 'Zone_')."),
                    McpSchema.Any("merge", "Zones of the plan to make one, by number: [[3, 4], [7, 8, 9]] (the first number's zone takes the others')."),
                    McpSchema.Any("drop", "Zones of the plan not to make, by number: [5, 6] (their content stays unzoned)."),
                    McpSchema.String("plan_id", "The plan_id a dry run returned (merge/drop may be added): the zones are made only if the level still plans the same."),
                    McpSchema.Boolean("link", "Also make the links (default true)."),
                    McpSchema.Boolean("extend_existing", "Unzoned content lying among an existing zone's goes to that zone (default true); false leaves it unzoned."),
                    McpSchema.String("open_pairs", "As link_zones: 'open_link' (default), 'exclusion' or 'none'.", options: OpenPairOptions),
                    McpSchema.Boolean("replace_door_links", "Re-point doors whose pin drives a link between other zones (default false: reported)."),
                    McpSchema.Integer("limit", "At most this many entries per list in the result (default 100).")),
                Run = AutoZone,
            };
        }

        #region Analysis
        /// <summary>The middle and size of the box around some positions, in the world, or null for none.</summary>
        private static JArray Extent(Scene scene, IEnumerable<Vector3> positions)
        {
            List<Vector3> list = positions.ToList();
            if (list.Count == 0) return null;
            Vector3 min = list.Aggregate(Vector3.Min), max = list.Aggregate(Vector3.Max);
            return new JArray(McpValues.Vector(scene.World((min + max) / 2f)), McpValues.Vector(max - min));
        }

        private static string LinkText(Scene scene, LinkInfo link) =>
            McpScript.EntityName(scene.Commands, link.Holder, link.Link) + " (" + scene.NameOf(link.A) + " - " + scene.NameOf(link.B) + (link.Own ? "" : ", in " + link.Holder.name) + ")";

        private static JObject DescribeDoorState(Commands commands, Analysis analysis, DoorState state, List<string> problems = null)
        {
            Scene scene = analysis.Scene;
            JObject described = new JObject()
            {
                ["name"] = state.Door.Name,
                ["path"] = state.Door.Path,
                ["type"] = state.Door.Type,
                ["position"] = McpValues.Vector(scene.World(state.Door.Position)),
                ["listed_by"] = new JArray(state.ListedBy.Select(scene.NameOf)),
            };
            if (state.Joins != null)
            {
                described["joins"] = new JArray(scene.NameOf(state.Joins.Value.a), scene.NameOf(state.Joins.Value.b));
                described["joins_from"] = state.JoinsFrom == "listing" ? "the two zones listing it" : "the content either side of it";
            }
            if (state.DeadEnd)
                described["dead_end"] = "Nothing lies behind it" + (state.Side != null ? ": it opens from " + scene.NameOf(state.Side) + "." : ".") + " It is listed by that one zone and drives no link.";
            if (state.SeeThrough)
                described["see_through"] = "It has a window: its rooms see each other with it shut, so they are joined by an always-open link and it drives none.";
            if (state.Drives.Count != 0)
                described["drives"] = new JArray(state.Drives.Select(o => LinkText(scene, o)));
            if (state.Works.Count != 0)
                described["opens_and_closes"] = new JArray(state.Works.Select(o => LinkText(scene, o)));
            problems = problems ?? DoorProblems(analysis, state);
            if (problems.Count != 0) described["problems"] = new JArray(problems);
            return described;
        }

        private static object AnalyseZones(McpCall call)
        {
            int limit = Math.Max(1, call.Int("limit", 100));
            using (McpEditorTools.Heartbeat(call, "Analysing zones"))
            return McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: false);
                Commands commands = content.Level.Commands;
                CompileShown();
                Composite composite = ZoningComposite(call, commands);
                Scene scene = BuildScene(commands, composite, call.Cancel);
                Analysis analysis = Analyse(commands, scene);

                JObject result = new JObject() { ["composite"] = composite.name };
                if (!call.Has("composite")) result["composite_note"] = "Picked as the composite under the root holding the zones and most placed content; pass 'composite' to look at another.";
                if (scene.Placements > 1) result["placements_note"] = composite.name + " is placed " + scene.Placements + " times; positions are those of its first placement.";
                if (scene.Truncated) result["truncated"] = "The walk stopped early on a very large composite: some content was not looked at.";

                Dictionary<string, List<Vector3>> byZone = new Dictionary<string, List<Vector3>>();
                List<McpZonePlanner.Leaf> unzoned = new List<McpZonePlanner.Leaf>();
                foreach (McpZonePlanner.Leaf leaf in scene.Leaves)
                {
                    List<string> zones = scene.ClaimsOf(leaf.Path);
                    if (zones.Count == 0) { unzoned.Add(leaf); continue; }
                    foreach (string zone in zones)
                    {
                        if (!byZone.TryGetValue(zone, out List<Vector3> list)) byZone[zone] = list = new List<Vector3>();
                        list.Add(leaf.Position);
                    }
                }
                result["zones"] = new JArray(scene.ZoneNames.Keys.Take(limit).Select(label =>
                {
                    List<Vector3> at = byZone.TryGetValue(label, out List<Vector3> list) ? list : new List<Vector3>();
                    JArray where = Extent(scene, at);
                    JObject zone = new JObject() { ["name"] = scene.NameOf(label), ["id"] = label, ["content"] = at.Count, ["links"] = analysis.Links.Count(o => o.A == label || o.B == label) };
                    if (where != null) { zone["centre"] = where[0]; zone["size"] = where[1]; }
                    if (scene.ScriptZones.Contains(label)) zone["script_loaded"] = "Loaded by script (its load methods are called; no link joins it), not by where the player is: not a room. What it claims is not counted as zoned here.";
                    else if (!scene.Zones.ContainsKey(label)) zone["note"] = "Inside an instance this composite places: links here reach it only through that instance's zone pins.";
                    return zone;
                }));
                if (scene.ZoneNames.Count > limit) result["zones_note"] = "Showing " + limit + " of " + scene.ZoneNames.Count + " zones.";
                result["content"] = scene.Leaves.Count;
                result["unzoned"] = new JObject()
                {
                    ["count"] = unzoned.Count,
                    ["examples"] = new JArray(unzoned.Take(Math.Min(limit, 15)).Select(o => o.Path + " (" + o.Type + ")")),
                };

                List<List<string>> doorProblems = analysis.Doors.Select(o => DoorProblems(analysis, o)).ToList();
                result["doors"] = new JArray(analysis.Doors.Take(limit).Select((o, i) => DescribeDoorState(commands, analysis, o, doorProblems[i])));
                if (analysis.Doors.Count > limit) result["doors_note"] = "Showing " + limit + " of " + analysis.Doors.Count + " doors.";

                JArray pairs = new JArray();
                int missingDoor = 0, missingOpen = 0;
                //As link_zones decides: a doorless pair joined only by a link the script works is left as it is
                (string, string) Ordered(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);
                HashSet<(string, string)> doorPairs = new HashSet<(string, string)>(analysis.Doors.Where(o => o.Joins != null).Select(o => Ordered(o.Joins.Value.a, o.Joins.Value.b)));
                foreach (McpZonePlanner.Pair pair in analysis.Pairs)
                {
                    if (scene.ScriptZones.Contains(pair.A) || scene.ScriptZones.Contains(pair.B)) continue;
                    List<LinkInfo> joining = analysis.Joining(pair.A, pair.B);
                    bool own = scene.Linkable(pair.A) && scene.Linkable(pair.B);
                    JObject described = new JObject()
                    {
                        ["a"] = scene.NameOf(pair.A),
                        ["b"] = scene.NameOf(pair.B),
                        ["kind"] = pair.Kind,
                        ["contacts"] = pair.Contacts,
                    };
                    if (pair.Reason != null) described["why"] = pair.Reason == "window_door" ? "a door with a window between them" : pair.Reason == "window_wall" ? "a window in the wall between them" : pair.Reason == "beside_door" ? "they also meet away from the door" : "their content meets";
                    if (pair.Door != null) described["door"] = pair.Door.Name;
                    bool satisfied = pair.Kind == "door" ? joining.Count != 0 : OpenPairJoined(joining);
                    bool scripted = !satisfied && pair.Kind == "open" && !doorPairs.Contains(Ordered(pair.A, pair.B)) && ScriptWorked(joining);
                    if (joining.Count != 0) described["joined_by"] = new JArray(joining.Select(o => LinkText(scene, o) + (o.State.AlwaysOpen ? " [always open]" : "")));
                    if (!satisfied)
                    {
                        if (!own) described["note"] = "One of these zones is inside an instance: link it from the composite that holds it, or through that instance's zone pins.";
                        else if (scripted) described["note"] = "Only a link the script opens and closes joins them, so link_zones leaves it as it is. Make it open on reset if the two should never unload apart.";
                        else { described["missing"] = pair.Kind == "door" ? "a ZoneLink the door drives" : "an always-open link (a ZoneLink open on reset)"; if (pair.Kind == "door") missingDoor++; else missingOpen++; }
                    }
                    if (pairs.Count < limit) pairs.Add(described);
                }
                result["pairs"] = pairs;
                int withProblems = doorProblems.Count(o => o.Count != 0);
                result["summary"] = new JObject()
                {
                    ["zones"] = scene.ZoneNames.Count,
                    ["script_loaded_zones"] = scene.ScriptZones.Count,
                    ["doors"] = analysis.Doors.Count,
                    ["dead_end_doors"] = analysis.Doors.Count(o => o.DeadEnd),
                    ["see_through_doors"] = analysis.Doors.Count(o => o.SeeThrough),
                    ["doors_with_problems"] = withProblems,
                    ["pairs"] = analysis.Pairs.Count,
                    ["door_pairs_unlinked"] = missingDoor,
                    ["open_pairs_unlinked"] = missingOpen,
                    ["unzoned_content"] = unzoned.Count,
                };
                List<string> next = new List<string>();
                if (scene.Zones.Count == 0) next.Add("There are no zones here: auto_zone plans and makes them.");
                else if (unzoned.Count != 0) next.Add("auto_zone zones the unzoned content (dry run first), or set_zone_contents adds it to existing zones.");
                if (missingDoor + missingOpen + withProblems != 0) next.Add("link_zones makes the missing links and lists doors in their zones (dry_run first).");
                next.Add(BuildNote);
                result["next"] = string.Join(" ", next);
                return result;
            });
        }
        #endregion

        #region Linking
        /// <summary>
        /// Make planned links, within an edit. <paramref name="zoneFor"/> maps a label to its Zone entity. Each door is found
        /// first; an action that cannot be made (its door gone, its pin taken) is put in <paramref name="skipped"/> rather than
        /// refusing the rest, and nothing of it is changed.
        /// </summary>
        private static JArray ApplyLinks(McpScriptEdit edit, Scene scene, List<LinkAction> actions, Func<string, Entity> zoneFor, bool replaceDoorLinks, bool addDoorsToZones, List<string> notes, List<string> skipped)
        {
            JArray made = new JArray();
            Commands commands = edit.Commands;
            Composite composite = scene.Composite;

            //Validate: every door and zone, before anything changes
            List<(LinkAction action, DoorSpec door, Entity a, Entity b)> ready = new List<(LinkAction, DoorSpec, Entity, Entity)>();
            foreach (LinkAction action in actions)
            {
                Entity a = zoneFor(action.A), b = action.B == null ? null : zoneFor(action.B);
                if (a == null || (action.B != null && b == null)) { skipped.Add(DescribeActionText(scene, action) + ": a zone it needs is not there."); continue; }
                DoorSpec door = null;
                if (action.Door != null)
                {
                    try { door = FindDoor(commands, composite, action.Door.Path.Split('/').ToList()); }
                    catch (McpError error) { skipped.Add(action.Door.Name + ": " + error.Message); continue; }
                }
                ready.Add((action, door, a, b));
            }

            //Doors being re-pointed let go of their wrong links first, so crossed links can swap doors rather than leave one orphaned
            List<FunctionEntity> orphans = new List<FunctionEntity>();
            Dictionary<LinkAction, JArray> released = new Dictionary<LinkAction, JArray>();
            foreach ((LinkAction action, DoorSpec door, Entity a, Entity b) in ready)
            {
                if (door == null || action.Unhook.Count == 0 || !(action.ReplaceDoorLink || replaceDoorLinks)) continue;
                foreach (Entity owner in DoorOwners(composite, door))
                    foreach (LinkInfo wrong in action.Unhook.Where(o => o.Own))
                        if (owner.childLinks.Any(o => o.thisParamID == door.Pin && o.linkedEntityID == wrong.Link.shortGUID))
                        {
                            if (!released.TryGetValue(action, out JArray names)) released[action] = names = new JArray();
                            string name = McpScript.EntityName(commands, composite, wrong.Link);
                            if (!names.Any(o => (string)o == name)) names.Add(name);
                            edit.RemoveLinks(composite, owner, McpScript.ParamName(door.Pin), wrong.Link, null);
                            orphans.Add(wrong.Link);
                        }
            }

            foreach ((LinkAction action, DoorSpec door, Entity a, Entity b) in ready)
            {
                try
                {
                    if (action.Kind == "list")
                    {
                        JArray listedIn = new JArray();
                        foreach (Entity zone in b == null ? new[] { a } : new[] { a, b })
                        {
                            ContentChange change = new ContentChange();
                            ChangeContents(edit, composite, zone, new List<ShortGuid[]>() { door.ContentPath }, new List<ShortGuid[]>(), change);
                            if (change.Added.Count != 0) listedIn.Add(McpScript.EntityName(commands, composite, zone));
                            notes.AddRange(change.Notes);
                        }
                        if (listedIn.Count != 0) made.Add(new JObject() { ["listed_door"] = door.Name, ["in"] = listedIn, ["why"] = action.Reason });
                        continue;
                    }
                    if (action.Kind == "unhook")
                    {
                        //A door that needs no link: everything off its pin goes (its own links went in the pass above)
                        JArray cleared = released.TryGetValue(action, out JArray before) ? before : new JArray();
                        foreach (Entity owner in DoorOwners(composite, door))
                            foreach (EntityConnector connector in owner.childLinks.Where(o => o.thisParamID == door.Pin).ToList())
                            {
                                Entity target = composite.GetEntityByID(connector.linkedEntityID);
                                cleared.Add(target == null ? McpScript.Id(connector.linkedEntityID) : McpScript.EntityName(commands, composite, target));
                                if (target is FunctionEntity linkEntity && IsZoneLink(linkEntity)) orphans.Add(linkEntity);
                            }
                        foreach (Entity owner in DoorOwners(composite, door))
                            edit.RemoveLinks(composite, owner, McpScript.ParamName(door.Pin), null, null);
                        if (cleared.Count != 0) made.Add(new JObject() { ["unhooked_door"] = door.Name, ["from"] = cleared, ["why"] = action.Reason });
                        continue;
                    }
                    List<string> linkNotes = new List<string>();
                    FunctionEntity link;
                    bool reused;
                    if (action.Kind == "open")
                        link = JoinZones(edit, composite, a, b, false, null, new JoinOptions() { OpenOnReset = true, ListDoor = door, AddDoorToZones = addDoorsToZones, DefaultPrefix = "ZoneLink_Open_" }, linkNotes, out reused);
                    else
                        link = JoinZones(edit, composite, a, b, action.Kind == "exclusion", door, new JoinOptions() { ReplaceDoorLink = replaceDoorLinks || action.ReplaceDoorLink || action.Unhook.Count != 0, AddDoorToZones = addDoorsToZones, Orphans = orphans }, linkNotes, out reused);
                    JObject described = new JObject()
                    {
                        ["kind"] = action.Kind == "exclusion" ? "ZoneExclusionLink" : action.Kind == "open" ? "ZoneLink (open on reset)" : "ZoneLink",
                        ["link"] = McpScript.EntityName(commands, composite, link),
                        ["zones"] = new JArray(McpScript.EntityName(commands, composite, a), McpScript.EntityName(commands, composite, b)),
                        ["why"] = action.Reason,
                    };
                    if (door != null) described["door"] = door.Name;
                    if (released.TryGetValue(action, out JArray replaced)) described["door_let_go_of"] = replaced;
                    if (reused) described["reused"] = true;
                    made.Add(described);
                    notes.AddRange(linkNotes.Where(o => !o.StartsWith("Added the door", StringComparison.Ordinal) && !o.Contains("already joined") && !o.StartsWith("No door was given", StringComparison.Ordinal) && !o.StartsWith("Also joining these zones", StringComparison.Ordinal)));
                }
                catch (McpError error)
                {
                    skipped.Add(DescribeActionText(scene, action) + ": " + error.Message);
                }
            }
            //Only once every change is made: a link one door let go of may be another's now
            NoteOrphans(commands, composite, orphans, notes);
            return made;
        }

        private static string DescribeActionText(Scene scene, LinkAction action) =>
            (action.Kind == "list" ? "listing " + action.Door?.Name : action.Kind == "unhook" ? "unhooking " + action.Door?.Name : action.Kind + " link " + scene.NameOf(action.A) + " - " + scene.NameOf(action.B) + (action.Door != null ? " (door " + action.Door.Name + ")" : ""));

        private static JObject DescribeAction(Scene scene, LinkAction action, Func<string, string> nameOf = null)
        {
            nameOf = nameOf ?? scene.NameOf;
            JObject described = new JObject()
            {
                ["make"] = action.Kind == "exclusion" ? "ZoneExclusionLink" : action.Kind == "open" ? "ZoneLink open on reset (driven by nothing)" : action.Kind == "list" ? "door listed by " + (action.B == null ? "its zone" : "both zones") : action.Kind == "unhook" ? "door's zone link pin cleared" : action.Repair != null ? "repair of a half-wired ZoneLink" : "ZoneLink the door drives",
                ["zones"] = action.B == null ? new JArray(nameOf(action.A)) : new JArray(nameOf(action.A), nameOf(action.B)),
                ["why"] = action.Reason,
            };
            if (action.Door != null) described["door"] = action.Door.Name;
            if (action.Repair != null) described["repairs"] = McpScript.EntityName(scene.Commands, action.Repair.Holder, action.Repair.Link);
            if (action.Unhook.Count != 0) described["unhooks"] = new JArray(action.Unhook.Select(o => McpScript.EntityName(scene.Commands, o.Holder, o.Link)));
            return described;
        }

        /// <summary>link_zones' and auto_zone's choices from the call. <paramref name="composite"/> resolves zone names (null: none given).</summary>
        private static LinkOptions ReadLinkOptions(McpCall call, Commands commands, Composite composite, Scene scene)
        {
            LinkOptions options = new LinkOptions()
            {
                Doors = call.Bool("doors", true),
                AddDoorsToZones = call.Bool("add_doors_to_zones", true),
                ReplaceDoorLinks = call.Bool("replace_door_links"),
                OpenPairs = (call.Str("open_pairs") ?? (call.Has("exclusions") ? (call.Bool("exclusions") ? "exclusion" : "none") : "open_link")).Trim().ToLowerInvariant(),
            };
            if (!OpenPairOptions.Contains(options.OpenPairs))
                throw new McpError("'open_pairs' is 'exclusion', 'open_link' or 'none'.");
            string LabelOf(string wanted)
            {
                Entity zone = ZoneIn(commands, composite, wanted, "zone", allowPin: false);
                return scene.Zones.FirstOrDefault(o => o.Value == zone).Key ?? McpScript.Id(zone.shortGUID);
            }
            if (call.Has("zones"))
                options.Touching = new HashSet<string>(call.StrList("zones").Select(LabelOf));
            if (call.Has("between"))
            {
                List<string> pair = call.StrList("between");
                if (pair.Count != 2) throw new McpError("'between' is two zones.");
                options.Between = (LabelOf(pair[0]), LabelOf(pair[1]));
            }
            if (call.Has("skip_doors"))
                foreach (JToken token in call.Array("skip_doors"))
                {
                    string text = token.Type == JTokenType.Array ? string.Join("/", token.Select(o => (string)o)) : (string)token;
                    McpZonePlanner.Door byPath = scene.Doors.FirstOrDefault(o => o.Path == text);
                    if (byPath != null) { options.SkipDoors.Add(byPath.Path); continue; }
                    List<McpZonePlanner.Door> byName = scene.Doors.Where(o => string.Equals(o.Name, text, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (byName.Count == 0) throw new McpError("'skip_doors': no door '" + text + "' (analyse_zones lists the doors with their paths).");
                    if (byName.Count > 1) throw new McpError("'skip_doors': " + byName.Count + " doors are called '" + text + "' (" + string.Join(", ", byName.Select(o => o.Path)) + "): give the path of each one to skip.");
                    options.SkipDoors.Add(byName[0].Path);
                }
            return options;
        }

        private static object LinkZones(McpCall call)
        {
            bool dryRun = call.Bool("dry_run");
            int limit = Math.Max(1, call.Int("limit", 200));
            if (!call.Bool("doors", true) && ((call.Str("open_pairs") ?? "") == "none" || (call.Has("exclusions") && !call.Bool("exclusions"))))
                throw new McpError("With doors off and open_pairs 'none' there is nothing to make.");

            Composite composite = null;
            Scene scene = null;
            List<LinkAction> actions = null;
            LinkOptions options = null;
            List<string> skipped = new List<string>();
            //The plan, on the script as it stands when it is made
            void Plan(Commands commands)
            {
                scene = BuildScene(commands, composite, call.Cancel);
                if (scene.Zones.Count(o => !scene.ScriptZones.Contains(o.Key)) < 2)
                    throw new McpError(composite.name + " has " + (scene.Zones.Count == 0 ? "no zones" : "fewer than two room zones") + ", so there is nothing to link. auto_zone makes zones (or pass the composite that holds them).");
                options = ReadLinkOptions(call, commands, composite, scene);
                skipped.Clear();
                actions = PlanLinks(Analyse(commands, scene), options, skipped);
            }

            using (McpEditorTools.Heartbeat(call, "Working out the links"))
                McpEditor.UI(() =>
                {
                    LevelContent content = McpEditor.RequireLevel(forEditing: !dryRun);
                    CompileShown();
                    composite = ZoningComposite(call, content.Level.Commands);
                    if (dryRun) Plan(content.Level.Commands);
                    else CloseSequenceEditors(composite.functions.OfType<TriggerSequence>());
                });

            JObject result = new JObject() { ["composite"] = composite.name };
            if (dryRun)
            {
                result["dry_run"] = true;
                result["would_make"] = new JArray(actions.Take(limit).Select(o => DescribeAction(scene, o)));
                result["count"] = actions.Count;
                if (skipped.Count != 0) result["skipped"] = new JArray(skipped.Take(limit));
                if (actions.Count == 0) result["note"] = "Nothing is missing: every door between zones drives a link (or has an always-open one beside it) and is listed by both, and every neighbouring pair is joined.";
                else result["next"] = "Call again without dry_run to make these, as one undo step.";
                return result;
            }

            JArray made = null;
            List<string> notes = new List<string>();
            McpScriptEdit.Outcome outcome;
            using (McpEditorTools.Heartbeat(call, "Linking zones"))
                outcome = McpScriptEdit.Run("Link zones in " + McpScript.CompositeLeaf(composite), composite, edit =>
                {
                    Plan(edit.Commands);
                    if (actions.Count == 0) return;
                    //The links go on a page of their own rather than whichever page was last shown
                    edit.UsePage(composite, "Zones");
                    made = ApplyLinks(edit, scene, actions, label => scene.Zones.TryGetValue(label, out Entity zone) ? zone : null, options.ReplaceDoorLinks, options.AddDoorsToZones, notes, skipped);
                }, show: false);
            foreach (string note in notes.Distinct().Take(20)) call.Note(note);
            if (!outcome.Changed)
            {
                result["note"] = actions != null && actions.Count != 0
                    ? "Nothing was made: each of the " + actions.Count + " planned change" + (actions.Count == 1 ? " was" : "s was") + " refused when it came to be made - 'skipped' says why."
                    : "Nothing is missing: every door between zones drives a link (or has an always-open one beside it) and is listed by both, and every neighbouring pair is joined.";
                result["count"] = 0;
                if (skipped.Count != 0) result["skipped"] = new JArray(skipped.Take(limit));
                return result;
            }
            result["made"] = new JArray(made.Take(limit));
            result["count"] = made.Count;
            if (made.Count > limit) result["made_note"] = "Showing " + limit + " of " + made.Count + ".";
            if (skipped.Count != 0) result["skipped"] = new JArray(skipped.Take(limit));
            result["next"] = "analyse_zones to check, then save_level with build=true. " + BuildNote;
            return result;
        }
        #endregion

        #region Auto zoning
        private static List<List<int>> ReadMerge(McpCall call)
        {
            if (!call.Has("merge")) return null;
            List<List<int>> groups = new List<List<int>>();
            if (!(call.Token("merge") is JArray outer))
                throw new McpError("'merge' is a list of groups of zone numbers, e.g. [[3, 4], [7, 8]].");
            foreach (JToken group in outer)
            {
                if (!(group is JArray inner) || inner.Count < 2 || inner.Any(o => o.Type != JTokenType.Integer))
                    throw new McpError("Each group in 'merge' is two or more zone numbers, e.g. [3, 4].");
                groups.Add(inner.Select(o => (int)o).ToList());
            }
            return groups;
        }

        private static List<int> ReadDrop(McpCall call)
        {
            if (!call.Has("drop")) return null;
            if (!(call.Token("drop") is JArray list) || list.Any(o => o.Type != JTokenType.Integer))
                throw new McpError("'drop' is a list of zone numbers, e.g. [5, 6].");
            return list.Select(o => (int)o).ToList();
        }

        /// <summary>The names the plan's zones get, in plan order: those given, then the prefix numbered past names already taken.</summary>
        private static List<string> ZoneNamesFor(Commands commands, Composite composite, AutoPlan plan, List<string> names, string prefix)
        {
            HashSet<string> taken = TakenNames(commands, composite);
            List<string> clashes = names.Where(taken.Contains).ToList();
            if (clashes.Count != 0)
                throw new McpError(composite.name + " already has entities called " + string.Join(", ", clashes.Select(o => "'" + o + "'")) + ": give other names in 'names'.");
            if (names.Count > plan.Order.Count)
                throw new McpError("'names' has " + names.Count + " names but the plan has " + plan.Order.Count + " zones.");
            List<string> result = new List<string>(names);
            foreach (string name in names) taken.Add(name);
            int next = 1;
            while (result.Count < plan.Order.Count)
            {
                string candidate = prefix + next++;
                if (taken.Contains(candidate)) continue;
                taken.Add(candidate);
                result.Add(candidate);
            }
            return result;
        }

        private static object AutoZone(McpCall call)
        {
            bool dryRun = call.Bool("dry_run", true), link = call.Bool("link", true), extend = call.Bool("extend_existing", true);
            int limit = Math.Max(1, call.Int("limit", 100));
            List<string> names = call.StrList("names").Select(o => o.Trim()).ToList();
            string prefix = call.Str("prefix") ?? "Zone_";
            string planId = call.Str("plan_id");
            if (names.Any(string.IsNullOrWhiteSpace))
                throw new McpError("'names' has an empty name.");
            if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count)
                throw new McpError("'names' gives the same name twice.");
            List<List<int>> merge = ReadMerge(call);
            List<int> drop = ReadDrop(call);

            Composite composite = null;
            Scene scene = null;
            AutoPlan plan = null;
            List<string> zoneNames = null;
            LinkOptions options = null;
            void Plan(Commands commands)
            {
                scene = BuildScene(commands, composite, call.Cancel);
                options = ReadLinkOptions(call, commands, composite, scene);
                if (!link) { options.Doors = false; options.OpenPairs = "none"; }
                plan = PlanAutoZones(commands, scene, options, merge, drop, extend);
                zoneNames = ZoneNamesFor(commands, composite, plan, names, prefix);
            }

            using (McpEditorTools.Heartbeat(call, "Planning zones"))
                McpEditor.UI(() =>
                {
                    LevelContent content = McpEditor.RequireLevel(forEditing: !dryRun);
                    CompileShown();
                    composite = ZoningComposite(call, content.Level.Commands);
                    if (dryRun) Plan(content.Level.Commands);
                    else CloseSequenceEditors(composite.functions.OfType<TriggerSequence>());
                });

            JObject Describe()
            {
                string NameFor(int bucket) => zoneNames[plan.Order.IndexOf(bucket)];
                string Shown(string label) => NewBucket(label) >= 0 ? NameFor(NewBucket(label)) : scene.NameOf(label);
                JArray planned = new JArray();
                foreach (int bucket in plan.Order.Take(limit))
                {
                    JArray where = Extent(scene, plan.Content[bucket].Select(o => o.Position));
                    JObject zone = new JObject()
                    {
                        ["number"] = bucket,
                        ["name"] = NameFor(bucket),
                        ["entries"] = plan.Members[bucket].Count,
                        ["content"] = plan.Content[bucket].Count,
                        ["centre"] = where[0],
                        ["size"] = where[1],
                        ["examples"] = new JArray(plan.Members[bucket].Take(6).Select(path => scene.Items.FirstOrDefault(o => o.Path == path)?.Name ?? path)),
                    };
                    if (plan.DoorsOf[bucket].Count != 0) zone["doors"] = new JArray(plan.DoorsOf[bucket].Select(o => o.Name));
                    string label = NewLabel(bucket);
                    List<string> neighbours = plan.Links.Where(o => o.B != null && (o.A == label || o.B == label))
                        .Select(o => Shown(o.A == label ? o.B : o.A) + " (" + (o.Kind == "door" ? "door " + o.Door.Name : o.Kind == "open" ? (o.Door != null ? "window door " + o.Door.Name + ", open link" : "open link") : o.Kind == "list" ? "door " + o.Door.Name + ", already joined" : "exclusion link") + ")").ToList();
                    if (neighbours.Count != 0) zone["links_to"] = new JArray(neighbours);
                    planned.Add(zone);
                }
                JObject described = new JObject() { ["composite"] = composite.name, ["zones"] = planned, ["count"] = plan.Order.Count, ["plan_id"] = plan.Id };
                if (plan.Order.Count > limit) described["zones_note"] = "Showing " + limit + " of " + plan.Order.Count + " zones.";
                if (!call.Has("composite")) described["composite_note"] = "Picked as the composite under the root holding the zones and most placed content; pass 'composite' to zone another.";
                if (plan.Extend.Count != 0)
                    described["extends"] = new JArray(plan.Extend.Take(limit).Select(o => new JObject()
                    {
                        ["zone"] = scene.NameOf(o.Key),
                        ["entries"] = o.Value.Count,
                        ["content"] = plan.ExtendContent.TryGetValue(o.Key, out List<McpZonePlanner.Leaf> grown) ? grown.Count : 0,
                        ["examples"] = new JArray(o.Value.Take(6).Select(path => scene.Items.FirstOrDefault(i => i.Path == path)?.Name ?? path)),
                    }));
                if (plan.LeftOut.Count != 0)
                    described["left_unzoned"] = new JObject()
                    {
                        ["count"] = plan.LeftOut.Count,
                        ["examples"] = new JArray(plan.LeftOut.Take(10).Select(o => o.Path + " (" + o.Type + ")")),
                        ["why"] = plan.NearExisting != 0 ? plan.NearExisting + " of them lie among existing zones' content and extend_existing is off; add them with set_zone_contents." : "No placement unit holds it, so the plan has nowhere to put it: add it with set_zone_contents.",
                    };
                if (plan.Skipped.Count != 0) described["links_skipped"] = new JArray(plan.Skipped.Take(limit));
                return described;
            }

            if (dryRun)
            {
                if (plan.Order.Count == 0 && plan.Extend.Count == 0)
                {
                    string note = plan.NearExisting != 0 ? plan.NearExisting + " unzoned entities lie among existing zones' content; extend_existing adds them to those zones." : plan.LeftOut.Count != 0 ? plan.LeftOut.Count + " unzoned entities could not be placed in a zone: add them with set_zone_contents." : "Nothing to zone: everything zone-able placed in " + composite.name + " (models, lights, effects, collision) is already in a zone.";
                    JArray outsideExamples = new JArray();
                    int outside = McpEditor.UI(() => OutsideContent(call, McpEditor.RequireCommands(forEditing: false), composite, p => true, 5, outsideExamples, out string _));
                    if (outside != 0)
                        note += " But " + outside + " zone-able entities are placed outside " + composite.name + " and are in the global zone (e.g. " + string.Join("; ", outsideExamples.Select(o => (string)o)) + "): place them inside " + composite.name + " to zone them.";
                    return new JObject() { ["composite"] = composite.name, ["note"] = note };
                }
                JObject result = Describe();
                result["dry_run"] = true;
                result["links"] = link
                    ? plan.Links.Count(o => o.Kind == "door") + " ZoneLinks driven by doors, " + plan.Links.Count(o => o.Kind == "open") + " ZoneLinks open on reset, " + plan.Links.Count(o => o.Kind == "exclusion") + " ZoneExclusionLinks, " + plan.Links.Count(o => o.Kind == "list") + " door listings"
                    : "none (link is off)";
                result["next"] = "Check the zones (sizes, places, doors, links). Then call again with dry_run: false and this plan_id to make them as one undo step - adding 'merge'/'drop' (by the numbers here) to adjust, and 'names' to name the zones left, in number order. A dry run with the same merge/drop shows the adjusted plan first.";
                return result;
            }

            //Make it all as one step: zones and their lists (doors in both zones they join), then the links
            JArray linksMade = new JArray();
            List<string> notes = new List<string>();
            List<string> skipped = new List<string>();
            Dictionary<int, Entity> made = new Dictionary<int, Entity>();
            McpScriptEdit.Outcome outcome;
            using (McpEditorTools.Heartbeat(call, "Making zones"))
                outcome = McpScriptEdit.Run("Zone " + McpScript.CompositeLeaf(composite), composite, edit =>
                {
                    Commands commands = edit.Commands;
                    Plan(commands);
                    if (plan.Order.Count == 0 && plan.Extend.Count == 0) return;
                    if (planId != null && !string.Equals(planId.Trim(), plan.Id, StringComparison.OrdinalIgnoreCase))
                        throw new McpError("The plan is no longer the one with plan_id " + planId + " (the level changed since the dry run): run the dry run again and check it.");
                    //The zones, their lists and links go on a page of their own rather than whichever page was last shown
                    edit.UsePage(composite, "Zones");
                    for (int i = 0; i < plan.Order.Count; i++)
                    {
                        int bucket = plan.Order[i];
                        string name = zoneNames[i];
                        FunctionEntity zone = edit.AddFunction(composite, FunctionType.Zone, name);
                        //As vanilla zones have it: what an unloaded zone holds is suspended rather than left running
                        edit.SetParameter(composite, zone, "suspend_on_unload", new JValue(true));
                        Entity sequence = edit.AddFunction(composite, FunctionType.TriggerSequence, FreeName(edit, composite, name + "_Contents"));
                        edit.AddLink(composite, zone, "composites", sequence, "reference");
                        List<(ShortGuid[] path, float delay)> entries = plan.Members[bucket].Select(o => (PathOf(o), 0f)).ToList();
                        entries.AddRange(plan.DoorsOf[bucket].Select(o => (PathOf(o.Path), 0f)));
                        edit.SetTriggerSequence(composite, sequence, entries, null, append: false);
                        made[bucket] = zone;
                    }
                    foreach (KeyValuePair<string, List<string>> grown in plan.Extend)
                    {
                        if (!scene.Zones.TryGetValue(grown.Key, out Entity zone)) continue;
                        ContentChange change = new ContentChange();
                        ChangeContents(edit, composite, zone, grown.Value.Select(PathOf).ToList(), new List<ShortGuid[]>(), change);
                        notes.AddRange(change.Notes);
                    }
                    if (link)
                    {
                        Entity ZoneFor(string label) => NewBucket(label) >= 0 ? (made.TryGetValue(NewBucket(label), out Entity zone) ? zone : null) : (scene.Zones.TryGetValue(label, out Entity existing) ? existing : null);
                        linksMade = ApplyLinks(edit, scene, plan.Links, ZoneFor, options.ReplaceDoorLinks, options.AddDoorsToZones, notes, skipped);
                    }
                }, show: false);
            foreach (string note in notes.Distinct().Take(20)) call.Note(note);
            if (!outcome.Changed)
                return new JObject() { ["composite"] = composite.name, ["note"] = "Nothing to zone: everything zone-able placed in " + composite.name + " (models, lights, effects, collision) is already in a zone." };

            JObject done = Describe();
            done["made"] = plan.Order.Count + " zones" + (plan.Extend.Count != 0 ? ", and " + plan.Extend.Count + " existing zones extended" : "");
            if (link) done["links"] = new JArray(linksMade.Take(limit));
            if (skipped.Count != 0) done["skipped"] = new JArray(skipped.Take(limit));
            done["next"] = "analyse_zones to check the result, then save_level with build=true. " + BuildNote;
            return done;
        }
        #endregion
    }
}

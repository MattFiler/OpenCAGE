using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CATHODE.Scripting.Refactor;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.DockPanels;
using OpenCAGE.Undo;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Windows.Forms;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// Editing beyond making entities: copying them, re-pointing proxies, organising composites, and the
    /// questions a placement answers (where it sits, which zone streams it).
    /// </summary>
    internal static class McpEditingTools
    {
        private static readonly string[] _manageActions = { "rename", "move", "rename_folder", "delete", "create_folder" };
        private static readonly string[] _referenceKinds = { "aliases", "proxies", "trigger_sequences", "animations" };
        //Function types a composite can hold only one of (McpScriptEdit.AddFunction applies the same rule)
        private static readonly HashSet<FunctionType> _onePerComposite = new HashSet<FunctionType>() { FunctionType.PhysicsSystem, FunctionType.EnvironmentModelReference };

        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "copy_entities",
                Title = "Copy entities",
                Description = "Copy entities with their parameters, resources and the links between them: duplicate them in place (leave out 'into'), or copy them into another composite, as Ctrl+C / Ctrl+V do. Copies get new ids and '_N' names; an alias copies what it points at, placed where it sits (or, with resolve_aliases false, the alias itself - reusing one already on that path in the destination). " +
                    "'count' makes several sets, each moved 'offset' further than the last (or give each set its own 'offsets'); offsets are along the destination composite's axes, or the level's with space 'world'. links 'all' (in place only) also keeps each copy's links to and from what was not copied, so copies stay on the same switch. " +
                    "A copied CAGEAnimation drives, binds and fires the copies of what it drove when they were copied with it (its tracks, marker/character/camera bindings and animation-entity events), and a copied TriggerSequence triggers them (rebase_paths); offset_keys moves a copied entity's own position keys by the offset too. " +
                    "What still points outside the copies is listed in 'references_left', and entries that would point at nothing in the destination in 'dead_paths' (OpenCAGE drops them when the composite is next opened). " +
                    "A PhysicsSystem or EnvironmentModelReference is refused where the composite already has one (a composite can only have one). One undo step; returns the new ids.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite the entities are in (path or id; 'root' for the level's root).", required: true),
                    McpSchema.Strings("entities", "The entities to copy (ids or names).", required: true),
                    McpSchema.String("into", "Copy into this composite (path or id). Leave out to duplicate in place."),
                    McpSchema.Position("offset", "Added to each copy's position, along the destination composite's axes (the level's with space 'world'; the n-th set moves n times as far)"),
                    McpSchema.Integer("count", "How many sets of copies (default 1, at most 50)."),
                    McpSchema.Array("offsets", "Instead of offset and count: one offset per set of copies, [[x, y, z], ...] metres (along the destination composite's axes, or the level's with space 'world').", new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" }, ["minItems"] = 3, ["maxItems"] = 3 }),
                    McpSchema.String("space", "What offset(s) are along: 'composite' (default: the destination composite's axes) or 'world' (the level's, turned into the destination's through one placement of it; see placement). " + McpSchema.SpaceText + ".", options: new[] { "composite", "world" }),
                    McpSchema.Any("placement", "With space 'world': which placement of the destination composite to turn the offsets through - its number (0-based, get_placements order; needed when it is placed more than once) or the path of instances from the root (a result's {ids} object too)."),
                    McpSchema.Boolean("offset_keys", "A copied CAGEAnimation's x/y/z position keys of an entity copied with it move by the offset too, so the copy plays its move where the copy stands (default false)."),
                    McpSchema.String("links", "'internal' (default): keep the links between the copied entities; 'all': those, and each copy's links to and from entities not copied (in place only); 'none': copy no links.", options: new[] { "internal", "all", "none" }),
                    McpSchema.Boolean("resolve_aliases", "An alias copies the entity it points at, placed where it sits (default true); false copies the alias itself."),
                    McpSchema.Boolean("rebase_paths", "Point a copied animation's tracks, bindings and events, and a copied sequence's entries, at the copies of what they named, when that was copied too (default true)."),
                    McpSchema.String("page", "The flowgraph page for copied links that cannot go beside what they join.")),
                Run = CopyEntities,
            };

            yield return new McpTool()
            {
                Name = "move_entities",
                Title = "Move entities",
                Description = "Move or turn entities by an amount rather than to a place - 'move it 2 m up', 'turn it 90 degrees more' - in one undo step. space 'composite' (default): along the axes of the composite they are in; 'local': along each entity's own axes (its +Z is forward); " +
                    "'world': along the level's axes, through where the composite is placed ('placement' picks one when it is placed more than once). pivot 'each' turns each about its own position; 'centre' turns the group about its middle. " +
                    "An alias moves what it points at in that placement only (it writes the alias's position override); an instance moves everything it places. " + McpSchema.PositionText + "; rotate is " + McpSchema.RotationText + ".",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite the entities are in (path or id).", required: true),
                    McpSchema.Strings("entities", "The entities to move (ids or names).", required: true),
                    McpSchema.Vector("translate", "[x, y, z] metres to move by."),
                    McpSchema.Vector("rotate", "[pitch, yaw, roll] degrees to turn by (yaw turns about up)."),
                    McpSchema.String("space", "'composite' (default), 'local' or 'world': the axes translate and rotate are along. " + McpSchema.SpaceText + ".", options: new[] { "composite", "local", "world" }),
                    McpSchema.Any("placement", "With space 'world': which placement of the composite gives its axes - its number (0-based, get_placements order) or the path of instances from the root to it (a result's {ids} object too)."),
                    McpSchema.String("pivot", "'each' (default): each turns about its own position; 'centre': the group turns about its middle.", options: new[] { "each", "centre" })),
                Run = MoveEntities,
            };

            yield return new McpTool()
            {
                Name = "clear_instance_overrides",
                Title = "Clear instance overrides",
                Description = "Take away the overrides a composite instance carries - the aliases get_instance_overrides lists - in one undo step: an alias with no links is deleted; one with links keeps them and loses only its overriding parameters. " +
                    "'aliases' picks some of them; 'parameters' clears only those overrides and keeps the rest. Give 'path' (one placement: the aliases applying to it) or composite + instance (every alias reaching it).",
                InputSchema = McpSchema.Object(
                    McpSchema.Any("path", "One placement of the instance: entity ids/names from the root composite through instances."),
                    McpSchema.String("composite", "Instead of path: the composite holding the instance (path or id)."),
                    McpSchema.String("instance", "With composite: the instance entity (id or name)."),
                    McpSchema.Strings("aliases", "Only these aliases (ids, as get_instance_overrides gives them). Default: all of them."),
                    McpSchema.Strings("parameters", "Only clear these overridden parameters (e.g. ['colour']), keeping the aliases' other overrides.")),
                Destructive = true,
                Run = ClearInstanceOverrides,
            };

            yield return new McpTool()
            {
                Name = "retarget_proxy",
                Title = "Re-point proxy",
                Description = "Point a proxy (a dead one left by port_composites, say) at another function entity or composite instance, keeping its id, links and nodes - the inspector's Change Target. The path runs from the level's root composite. One undo step.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite the proxy is in (path or id).", required: true),
                    McpSchema.String("proxy", "The proxy (id or name).", required: true),
                    McpSchema.Any("path", "Entity ids/names from the root composite, through instances, to the new target, e.g. [\"Door_Area\", \"Door_1\"] (or a result's {ids} object).", required: true)),
                Run = RetargetProxy,
            };

            yield return new McpTool()
            {
                Name = "list_dead_proxies",
                Title = "List dead proxies",
                Description = "Proxies whose target no longer exists (port_composites and import_composite_package can leave them): each with its composite, the type it pointed at, its stored path up to the break, and how many links run through it. retarget_proxy fixes one.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "Only this composite (path or id). Default: the whole level."),
                    McpSchema.String("filter", "Only proxies whose name contains this."),
                    McpSchema.Limit(100, "proxies"),
                    McpSchema.Offset("proxies")),
                ReadOnly = true,
                Idempotent = true,
                Run = ListDeadProxies,
            };

            yield return new McpTool()
            {
                Name = "manage_composites",
                Title = "Rename, move or delete composites",
                Description = "Organise composites as the Composite Browser does. rename: composite + new_path. move: composites (or folder) + to_folder. rename_folder: folder + new_path. delete: composites or folder (their instances everywhere go too; proxies into them stay, dead). create_folder: folder. One undo step each.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("action", "What to do.", required: true, options: _manageActions),
                    McpSchema.String("composite", "rename: the composite (path or id)."),
                    McpSchema.Strings("composites", "move / delete: the composites (paths or ids)."),
                    McpSchema.String("folder", "create_folder / rename_folder, or move / delete a whole folder: its path, e.g. 'MCP\\Lights'."),
                    McpSchema.String("new_path", "rename / rename_folder: the new path, or just a new name to keep it in the same folder."),
                    McpSchema.String("to_folder", "move: the folder to move into ('' for the top level).")),
                Destructive = true,
                Run = ManageComposites,
            };

            yield return new McpTool()
            {
                Name = "get_placements",
                Title = "Get placements",
                Description = "Where a composite or entity sits in the level: each instance path from the root composite down to it, with its world position and rotation in metres and degrees (instance transforms composed, alias position overrides applied; a function with no position of its own sits at its composite's origin, 'position_from' says so), and optionally the zones that claim it. " +
                    "By default only placements the game really makes are listed: those under a deleted or is_template instance, or a repeat of an is_shared composite, are left out (include 'all' lists them, flagged). An alias or proxy is resolved to the entity it stands for. Give 'path' to evaluate one placement. " +
                    "Each placement's 'ids' (and 'path') can be passed back as any tool's path. transform_points converts between world space and a placement's local space; get_bounds gives a placement's extent; get_effective_parameters gives its parameter values.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite (path or id) - or the one the entity is in."),
                    McpSchema.String("entity", "An entity of that composite (id or name). Leave out for the composite's own placements."),
                    McpSchema.Any("path", "Instead: one placement, as entity ids/names from the root composite through instances to the entity (an array, a string split on '/', or a result's {ids} object)."),
                    McpSchema.Boolean("zones", "Also list the Zone entities that claim each placement."),
                    McpSchema.String("include", "'real' (default): placements the game makes; 'all': deleted, template and shared-repeat placements too, flagged.", options: new[] { "real", "all" }),
                    McpSchema.Limit(50, "placements"),
                    McpSchema.Offset("placements")),
                ReadOnly = true,
                Idempotent = true,
                Run = GetPlacements,
            };

            yield return new McpTool()
            {
                Name = "get_zones",
                Title = "Get zones",
                Description = "The level's Zone entities (which stream and control parts of the level) with the composite each is in, the instance paths each claims, as the viewport's zone overlay works them out, and the ZoneLinks joining each to other zones. With 'path', only the zones that claim that placement. " +
                    "get_zone_links details the links; check_zones diagnoses one area (a doorway that pops in, say); create_zone, set_zone_contents and create_zone_link make and change them.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("filter", "Only zones whose name contains this."),
                    McpSchema.Any("path", "A placement: entity ids/names from the root composite through instances (an array, a string split on '/', or a result's {ids} object). Lists the zones claiming it."),
                    McpSchema.Limit(100, "zones"),
                    McpSchema.Offset("zones"),
                    McpSchema.Integer("roots_limit", "At most this many claimed paths per zone (default 10).")),
                ReadOnly = true,
                Idempotent = true,
                Run = GetZones,
            };

            yield return new McpTool()
            {
                Name = "list_enum_string_values",
                Title = "List enum-string values",
                Description = "The valid values of an enum-string parameter type - SOUND_EVENT, ANIMATION, ANIMATION_SET, DISPLAY_MODEL, MATERIAL, SOUND_BANK, STRING_UI, ... - as the inspector's pickers offer them, for set_parameters. list_enums lists the types; describe_entity shows which type a parameter takes.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("type", "The enum-string type, e.g. 'SOUND_EVENT'.", required: true),
                    McpSchema.String("filter", "Only values (or descriptions) containing this."),
                    McpSchema.String("animation_set", "ANIMATION only: just the animations of this animation set."),
                    McpSchema.Limit(200, "values"),
                    McpSchema.Offset("values")),
                ReadOnly = true,
                Idempotent = true,
                Run = ListEnumStringValues,
            };

            yield return new McpTool()
            {
                Name = "remove_references",
                Title = "Remove references",
                Description = "Take away what points at an entity, as the References window's Remove does: delete the aliases and proxies pointing at it, drop it from TriggerSequences (and proxies' sequences), and unbind it from CAGEAnimations with the tracks only it played. find_references lists them first. As the References window does, the deletions are one undo step per composite they are in, and the sequence and animation changes one more; the result's undo_steps says how many.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite the entity is in (path or id).", required: true),
                    McpSchema.String("entity", "The entity (id or name).", required: true),
                    McpSchema.Nested("kinds", "Which references to remove (default: all four).", new JObject()
                    {
                        ["type"] = "array",
                        ["items"] = new JObject() { ["type"] = "string", ["enum"] = new JArray(_referenceKinds) },
                    })),
                Destructive = true,
                Run = RemoveReferences,
            };
        }

        #region Copying
        private static object CopyEntities(McpCall call)
        {
            List<string> wanted = call.StrList("entities", required: true);
            if (wanted.Count == 0)
                throw McpError.Invalid("'entities' is empty.");
            string linkMode = (call.Str("links") ?? "internal").Trim().ToLowerInvariant();
            if (linkMode != "internal" && linkMode != "none" && linkMode != "all")
                throw McpError.Invalid("'links' is 'internal' (keep the links between the copies), 'all' (and their links to and from the rest) or 'none'.");
            bool resolveAliases = call.Bool("resolve_aliases", true);
            bool rebase = call.Bool("rebase_paths", true);
            Vector3? offset = call.Has("offset") ? McpValues.ReadVector(call.Token("offset"), "offset", null) : (Vector3?)null;
            List<Vector3> offsets = null;
            if (call.Has("offsets"))
            {
                if (call.Has("offset") || call.Has("count"))
                    throw McpError.Invalid("Give 'offsets' (one per set of copies), or 'offset' with 'count', not both.");
                JArray list = call.Array("offsets");
                if (list.Count == 0 || list.Count > 50)
                    throw McpError.Invalid("'offsets' takes 1 to 50 offsets, [[x, y, z], ...].");
                offsets = list.Select((o, i) => McpValues.ReadVector(o, "offsets[" + i + "]", null)).ToList();
            }
            int count = offsets?.Count ?? call.Int("count", 1);
            if (count < 1 || count > 50)
                throw McpError.Invalid("'count' is 1 to 50.");
            string space = (call.Str("space") ?? "composite").Trim().ToLowerInvariant();
            if (space == "local") space = "composite";
            if (space != "composite" && space != "world")
                throw McpError.Invalid("'space' is 'composite' (the destination composite's axes: the default) or 'world'.");
            bool world = space == "world";
            if (call.Has("placement") && !world)
                throw McpError.Invalid("'placement' picks the placement world-space offsets are turned through: it goes with space 'world'.");
            if (world && offset == null && offsets == null)
                throw McpError.Invalid("space 'world' is for 'offset' or 'offsets': give one.");
            bool offsetKeys = call.Bool("offset_keys");
            if (offsetKeys && offset == null && offsets == null)
                throw McpError.Invalid("offset_keys moves copied position keys by the offset: give 'offset' or 'offsets' too.");
            if (offsetKeys && !call.Bool("rebase_paths", true))
                throw McpError.Invalid("offset_keys moves the keys of tracks re-pointed at the copies: leave rebase_paths on.");

            Composite source = null, destination = null;
            JObject frameDescribed = null;
            McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                source = McpScript.FindComposite(commands, call.Str("composite", required: true));
                destination = call.Has("into") ? McpScript.FindComposite(commands, call.Str("into")) : source;
                //Offsets along the level's axes, turned into the destination's (one placement of it)
                if (world)
                {
                    cTransform frame = McpScriptTools.FrameFor(commands, destination, null, call.Token("placement"), out frameDescribed);
                    if (offset != null) offset = InstanceTransform.DirectionToLocal(frame, offset.Value);
                    if (offsets != null) offsets = offsets.Select(o => InstanceTransform.DirectionToLocal(frame, o)).ToList();
                }
            });
            Vector3? OffsetOf(int set) => offsets != null ? offsets[set] : offset == null ? (Vector3?)null : offset.Value * (set + 1);
            bool inPlace = destination == source;
            if (linkMode == "all" && !inPlace)
                throw McpError.Invalid("links 'all' keeps links to entities that were not copied, which are not in " + destination.name + ": it works when duplicating in place (leave out 'into'). Use 'internal' to copy across.");

            JArray made = new JArray();
            List<string> skipped = new List<string>();
            List<string> deadPaths = new List<string>();
            List<string> referencesLeft = new List<string>();
            List<string> characterCopies = new List<string>();
            int linksCopied = 0, rebased = 0;
            string what = wanted.Count == 1 ? wanted[0] : wanted.Count + " entities";
            string label = inPlace
                ? "Duplicate " + what + (count > 1 ? " x" + count : "")
                : "Copy " + what + (count > 1 ? " x" + count : "") + " to " + McpScript.CompositeLeaf(destination);

            McpScriptEdit.Outcome outcome = McpScriptEdit.Run(label, destination, edit =>
            {
                Commands commands = edit.Commands;
                edit.UsePage(destination, call.Str("page"));
                List<Entity> sources = wanted.Select(o => McpScript.FindEntity(commands, source, o)).Distinct().ToList();
                edit.TouchContents(destination);
                edit.Tx.TouchPins(destination);
                for (int set = 0; set < count; set++)
                    CopySet(edit, source, destination, sources, set, count, OffsetOf(set), linkMode, resolveAliases, rebase, offsetKeys, inPlace, made, skipped, deadPaths, referencesLeft, characterCopies, ref linksCopied, ref rebased);
            });

            //The edit's own notes: a destination placed more than once, the editor left where the user went
            foreach (string note in outcome.Notes)
                call.Note(note);
            JObject result = new JObject()
            {
                ["composite"] = destination.name,
                ["copies"] = made,
                ["links_copied"] = linksCopied,
            };
            if (!outcome.Changed)
                result["unchanged"] = "Nothing was copied: every entity named is already there as it would be copied (an alias on that path).";
            if (world)
            {
                result["offset_space"] = "world";
                if (frameDescribed?["placement"] != null) result["placement"] = frameDescribed["placement"];
                else if (frameDescribed?["placement_path"] != null) result["placement"] = frameDescribed["placement_path"];
            }
            if (rebased != 0) result["paths_rebased"] = rebased;
            if (referencesLeft.Count != 0)
            {
                result["references_left"] = new JArray(referencesLeft.Distinct().Take(40));
                if (inPlace)
                    call.Note("Some copied animation or sequence entries still name entities that were not copied (references_left), so the copy drives the originals there. Copy those entities with it, or re-point the entries (animate_parameters / set_animation_events / set_trigger_sequence).");
            }
            if (deadPaths.Count != 0)
            {
                result["dead_paths"] = new JArray(deadPaths.Distinct().Take(40));
                call.Note("Some copied sequence or animation entries point at nothing in " + destination.name + ": OpenCAGE drops them when the composite is next opened. Copy what they list too, or set the entries again (set_trigger_sequence / set_animation_events).");
            }
            if (skipped.Count != 0) result["not_copied"] = new JArray(skipped);
            if (characterCopies.Count != 0 && outcome.Changed)
                call.Note("A character's look (its CHARACTERACCESSORYSETS entry) is kept per placement path, so " + string.Join(", ", characterCopies.Distinct().Take(6)) + " start" + (characterCopies.Distinct().Count() == 1 ? "s" : "") + " with the default look: set_character_appearance with from_placement (a placement of the original) gives a copy the original's, and check_character_appearances lists who has none.");
            return result;
        }

        /// <summary>One set of copies (copy_entities makes 'count' of them): the entities, then the links asked for, then their entries rebased.</summary>
        private static void CopySet(McpScriptEdit edit, Composite source, Composite destination, List<Entity> sources, int set, int sets, Vector3? offset, string linkMode, bool resolveAliases, bool rebase, bool offsetKeys, bool inPlace,
            JArray made, List<string> skipped, List<string> deadPaths, List<string> referencesLeft, List<string> characterCopies, ref int linksCopied, ref int rebased)
        {
            Commands commands = edit.Commands;
            {
                //Each entity copied, keyed by what was actually copied (an alias's target, when resolved): two entries naming it share one copy
                Dictionary<Entity, Entity> copies = new Dictionary<Entity, Entity>();
                Dictionary<Entity, Entity> copyOfListed = new Dictionary<Entity, Entity>();
                //Aliases kept as aliases whose path the destination already has an alias on: that one is used, not a second
                HashSet<Entity> reusedAliases = new HashSet<Entity>();
                foreach (Entity listed in sources)
                {
                    Composite from = source;
                    Entity original = listed;
                    cTransform placement = null;
                    if (listed is AliasEntity alias)
                    {
                        List<Tuple<Composite, Entity>> path = commands.Utils.ResolveAlias(alias, source);
                        if (resolveAliases)
                        {
                            (Composite targetComposite, Entity target) = commands.Utils.GetResolvedTarget(path);
                            if (target == null)
                                throw new McpError("The alias " + McpScript.EntityName(commands, source, alias) + " points at nothing, so there is nothing to copy. Pass resolve_aliases: false to copy the alias itself.");
                            if (targetComposite != source)
                            {
                                from = targetComposite;
                                original = target;
                                //Where it sits from here: the alias's own override wins, with every instance on the way folded in (as a copy of a deep selection does)
                                cTransform local = InstanceTransform.TransformOf(alias) ?? InstanceTransform.TransformOf(target);
                                if (local != null)
                                {
                                    cTransform chain = null;
                                    for (int i = 0; i < path.Count - 1; i++)
                                        chain = InstanceTransform.Compose(chain, InstanceTransform.TransformOf(path[i].Item2));
                                    placement = InstanceTransform.Compose(chain, local);
                                }
                            }
                        }
                        else if (!inPlace && !commands.Utils.CouldResolve(commands.Utils.ResolveAlias(alias.alias?.path, destination)))
                            throw new McpError("The alias " + McpScript.EntityName(commands, source, alias) + " would point at nothing in " + destination.name + " (an alias's path is read from its own composite). Leave resolve_aliases on to copy what it points at instead.");
                    }

                    if (copies.TryGetValue(original, out Entity already))
                    {
                        copyOfListed[listed] = already;
                        continue;
                    }

                    //Two aliases on one path apply in no set order when the level is built: an alias copied as itself joins the one
                    //the destination already has on that path (in place, that is the alias itself), as the editor's Paste Reference does
                    if (original is AliasEntity keptAlias && keptAlias.alias?.path != null)
                    {
                        AliasEntity onPath = McpScriptEdit.AliasesOn(destination, keptAlias.alias.path).FirstOrDefault();
                        if (onPath != null)
                        {
                            copies[original] = onPath;
                            copyOfListed[listed] = onPath;
                            reusedAliases.Add(onPath);
                            if (set == 0)
                                skipped.Add("alias " + McpScript.EntityName(commands, from, keptAlias) + " was not copied: " + destination.name + " already has an alias on that path (" + McpScript.EntityName(commands, destination, onPath) + ", " + McpScript.Id(onPath.shortGUID) +
                                    "), which the copies use - two aliases on one path apply in no set order");
                            continue;
                        }
                    }

                    if (original is VariableEntity variable && destination.variables.Any(o => o.name == variable.name))
                        throw new McpError(destination.name + " already has a variable called '" + McpScript.EntityName(commands, from, variable) + "': a composite's pins need different names. Copy it into another composite, or make a new variable with create_entities.");
                    //As create_entities and the Add Function dialog refuse a second one (functions is live, so copies made earlier in this call count)
                    if (original is FunctionEntity single && single.function.IsFunctionType && _onePerComposite.Contains(single.function.AsFunctionType) && destination.functions.Any(o => o.function == single.function))
                        throw new McpError(destination.name + " already has a " + single.function.AsFunctionType + ", and a composite can only have one: " + McpScript.EntityName(commands, from, original) + " cannot be " + (inPlace ? "duplicated in it." : "copied into it."));
                    Composite instanced = McpScript.InstancedComposite(commands, original);
                    if (instanced != null && commands.Utils.WouldCreateCompositeInstanceCycle(destination, instanced))
                        throw new McpError("An instance of " + instanced.name + " cannot go in " + destination.name + ": " + (destination == instanced ? "a composite cannot contain itself." : instanced.name + " already contains " + destination.name + ", so it would contain itself."));

                    //The editor's own clone-paste copy: new id, resources rebound, no links, a unique name, the variable's pin type, the modified marks
                    Entity copy = CompositeDisplay.CloneEntityForPaste(commands, from, original, destination);
                    if (copy == null)
                        throw new McpError(McpScript.EntityName(commands, from, original) + " could not be copied.");
                    edit.Made(destination, copy, defaults: false);
                    copies[original] = copy;
                    copyOfListed[listed] = copy;

                    //Placed where the alias put it, then moved by the offset
                    cTransform position = placement ?? (offset != null ? InstanceTransform.TransformOf(copy) : null);
                    if (position != null && !(copy is VariableEntity) && !(copy is ProxyEntity))
                    {
                        Vector3 moved = position.position + (offset ?? Vector3.Zero);
                        edit.SetParameter(destination, copy, "position", new JObject() { ["position"] = McpValues.Vector(moved), ["rotation"] = McpValues.Vector(position.rotation) }, allowCustom: true);
                    }
                    else if (offset != null && set == 0)
                        skipped.Add(McpScript.EntityName(commands, destination, copy) + " has no position, so the offset did not move it");
                }

                //The links that ran between the copied entities, onto the copies
                bool AddCopiedLink(Entity owner, EntityConnector link, Entity target)
                {
                    try
                    {
                        return edit.AddLink(destination, owner, McpScript.ParamName(link.thisParamID), target, McpScript.ParamName(link.linkedParamID), allowCustom: true);
                    }
                    catch (McpError e)
                    {
                        skipped.Add("link " + McpScript.EntityName(commands, destination, owner) + "." + McpScript.ParamName(link.thisParamID) + " -> " + McpScript.EntityName(commands, destination, target) + "." + McpScript.ParamName(link.linkedParamID) + " not copied: " + e.Message);
                        return false;
                    }
                }
                if (linkMode != "none")
                {
                    foreach (Entity listed in sources)
                    {
                        if (!copies.TryGetValue(listed, out Entity copy))
                            continue; //an alias resolved to something elsewhere: its links were the alias's
                        foreach (EntityConnector link in listed.childLinks.ToList())
                        {
                            Entity linkedSource = source.GetEntityByID(link.linkedEntityID);
                            if (linkedSource == null) continue;
                            if (copies.TryGetValue(linkedSource, out Entity linkedCopy))
                            {
                                if (AddCopiedLink(copy, link, linkedCopy)) linksCopied++;
                            }
                            //'all': the copy also drives (or reads from) what the original did
                            else if (linkMode == "all")
                            {
                                if (AddCopiedLink(copy, link, linkedSource)) linksCopied++;
                            }
                        }
                    }
                    //'all': what drove the originals drives the copies too (the same switch)
                    if (linkMode == "all")
                    {
                        foreach (Entity other in source.GetEntities().ToList())
                        {
                            if (copies.ContainsKey(other) || copies.ContainsValue(other)) continue;
                            foreach (EntityConnector link in other.childLinks.ToList())
                            {
                                Entity original = source.GetEntityByID(link.linkedEntityID);
                                if (original == null || !copies.TryGetValue(original, out Entity copy)) continue;
                                if (AddCopiedLink(other, link, copy)) linksCopied++;
                            }
                        }
                    }
                }

                //A copied CAGEAnimation's tracks, bindings and animation-entity events, and a copied TriggerSequence's entries, onto the
                //copies of what they named (the editor's paste does the same); with offset_keys a copied entity's own position keys move with it
                Dictionary<Entity, Entity> fromSource = copies.Where(o => source.GetEntityByID(o.Key.shortGUID) == o.Key && !reusedAliases.Contains(o.Value)).ToDictionary(o => o.Key, o => o.Value);
                HashSet<ShortGuid> copyIds = new HashSet<ShortGuid>(fromSource.Values.Select(o => o.shortGUID));
                if (rebase)
                {
                    foreach (string left in CompositeDisplay.RemapCopiedReferences(commands, source, destination, fromSource, offsetKeys ? offset : null))
                        //One that points at nothing in another composite is listed with its path in dead_paths
                        if (inPlace || !left.EndsWith(" points at nothing in " + destination.name, StringComparison.Ordinal))
                            referencesLeft.Add(left);
                }

                //What the copies' entries name now, checked against the destination; the entries the helper does not rebase (a proxy's
                //own sequence, and those of what an alias pointed at elsewhere) go onto the copies here
                Dictionary<ShortGuid, ShortGuid> copiedIds = new Dictionary<ShortGuid, ShortGuid>();
                foreach (KeyValuePair<Entity, Entity> pair in copies)
                    if (!reusedAliases.Contains(pair.Value)) copiedIds[pair.Key.shortGUID] = pair.Value.shortGUID;
                foreach (KeyValuePair<Entity, Entity> pair in copies)
                {
                    Entity copy = pair.Value;
                    if (reusedAliases.Contains(copy)) continue;
                    List<EntityPath> entries = new List<EntityPath>();
                    List<Action<EntityPath>> setters = new List<Action<EntityPath>>();
                    bool helperRebased = fromSource.ContainsKey(pair.Key) && (copy is TriggerSequence || copy is CAGEAnimation);
                    if (copy is TriggerSequence copiedSequence)
                        foreach (TriggerSequence.SequenceEntry entry in copiedSequence.sequence) { entries.Add(entry.connectedEntity); setters.Add(p => entry.connectedEntity = p); }
                    else if (copy is ProxyEntity copiedProxy && copiedProxy.sequence != null)
                        foreach (TriggerSequence.SequenceEntry entry in copiedProxy.sequence) { entries.Add(entry.connectedEntity); setters.Add(p => entry.connectedEntity = p); }
                    if (copy is CAGEAnimation copiedAnimation)
                        foreach (CAGEAnimation.Connection connection in copiedAnimation.connections) { entries.Add(connection.connectedEntity); setters.Add(p => connection.connectedEntity = p); }
                    for (int i = 0; i < entries.Count; i++)
                    {
                        ShortGuid[] path = entries[i]?.path;
                        if (path == null || path.Length == 0) continue;
                        if (rebase && helperRebased && copyIds.Contains(path[0]))
                            rebased++;
                        else if (rebase && !helperRebased && copiedIds.TryGetValue(path[0], out ShortGuid moved))
                        {
                            ShortGuid[] rebasedPath = (ShortGuid[])path.Clone();
                            rebasedPath[0] = moved;
                            setters[i](new EntityPath(rebasedPath));
                            path = rebasedPath;
                            rebased++;
                        }
                        if (!inPlace && !commands.Utils.CouldResolve(commands.Utils.ResolveEntityPath(new EntityPath(path), destination)))
                        {
                            //Named as the source composite reads it, where the path came from
                            List<Tuple<Composite, Entity>> wasIn = null;
                            try { wasIn = commands.Utils.ResolveEntityPath(new EntityPath(path), source); } catch { }
                            deadPaths.Add(McpScript.EntityName(commands, destination, copy) + ": " + string.Join(" > ", McpScript.DescribePath(commands, wasIn != null && wasIn.All(o => o.Item2 != null) ? wasIn : null, path).Select(o => (string)o)));
                        }
                    }
                }

                //A character's look (CHARACTERACCESSORYSETS) is keyed by its placement path, which a copy does not have an entry for
                foreach (Entity copy in copies.Values)
                {
                    if (reusedAliases.Contains(copy) || !(copy is FunctionEntity function)) continue;
                    Composite instanced = function.function.IsFunctionType ? null : commands.GetComposite(function.function);
                    bool holdsCharacter = function.function == FunctionType.Character
                        || (instanced != null && McpBrowseTools.ReachableFrom(commands, instanced).Any(c => c.functions.Any(f => f.function == FunctionType.Character)));
                    if (holdsCharacter) characterCopies.Add(McpScript.EntityName(commands, destination, copy));
                }

                foreach (Entity listed in sources)
                {
                    if (!copyOfListed.TryGetValue(listed, out Entity copy))
                        continue;
                    JObject brief = McpScript.Brief(commands, destination, copy);
                    brief["copy_of"] = McpScript.EntityName(commands, source, listed) + " (" + McpScript.Id(listed.shortGUID) + ")";
                    if (reusedAliases.Contains(copy)) brief["reused"] = true;
                    if (sets > 1) brief["set"] = set + 1;
                    made.Add(brief);
                }
            }
        }
        #endregion

        #region Moving
        private static object MoveEntities(McpCall call)
        {
            List<string> wanted = call.StrList("entities", required: true);
            if (wanted.Count == 0)
                throw McpError.Invalid("'entities' is empty.");
            Vector3? translate = call.Has("translate") ? McpValues.ReadVector(call.Token("translate"), "translate", null) : (Vector3?)null;
            Vector3? rotate = call.Has("rotate") ? McpValues.ReadVector(call.Token("rotate"), "rotate", null) : (Vector3?)null;
            if (translate == null && rotate == null)
                throw McpError.Invalid("Give 'translate' ([x, y, z] metres) and/or 'rotate' ([pitch, yaw, roll] degrees).");
            if (rotate == Vector3.Zero) rotate = null;
            string space = (call.Str("space") ?? "composite").Trim().ToLowerInvariant();
            if (space == "parent") space = "composite";
            if (space != "composite" && space != "local" && space != "world")
                throw McpError.Invalid("'space' is 'composite' (the composite's axes), 'local' (each entity's own) or 'world' (the level's).");
            string pivot = (call.Str("pivot") ?? "each").Trim().ToLowerInvariant();
            if (pivot == "center") pivot = "centre";
            if (pivot != "each" && pivot != "centre")
                throw McpError.Invalid("'pivot' is 'each' or 'centre'.");
            if (pivot == "centre" && space == "local")
                throw McpError.Invalid("pivot 'centre' turns the group about a shared point, which has no 'local' axes: use space 'composite' or 'world'.");
            if (call.Has("placement") && space != "world")
                throw McpError.Invalid("'placement' picks the composite's placement for space 'world'.");

            Composite composite = null;
            cTransform frame = null;
            JObject placed = null;
            McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                //One placement of the composite: its number, or the path of instances from the root (a result's {ids} too)
                if (space == "world")
                    frame = McpScriptTools.FrameFor(commands, composite, null, call.Token("placement"), out placed);
            });

            JArray moved = new JArray();
            List<string> skipped = new List<string>();
            string label = "Move " + (wanted.Count == 1 ? wanted[0] : wanted.Count + " entities");
            McpScriptEdit.Run(label, composite, edit =>
            {
                Commands commands = edit.Commands;
                List<Entity> entities = wanted.Select(o => McpScript.FindEntity(commands, composite, o)).Distinct().ToList();

                //Each one: what it writes (itself, or an alias's override), its stored transform, and the frame that is stored in from here
                List<(Entity entity, cTransform stored, cTransform storedIn, string note)> items = new List<(Entity, cTransform, cTransform, string)>();
                foreach (Entity entity in entities)
                {
                    string name = McpScript.EntityName(commands, composite, entity);
                    if (entity is VariableEntity || entity is ProxyEntity)
                    {
                        skipped.Add(name + " is " + McpScript.WithArticle(McpScript.Kind(entity)) + ", which has no position");
                        continue;
                    }
                    if (entity is AliasEntity alias)
                    {
                        List<Tuple<Composite, Entity>> path = commands.Utils.ResolveAlias(alias, composite);
                        Entity target = path != null && path.Count != 0 ? path[path.Count - 1].Item2 : null;
                        if (target == null || path.Any(o => o.Item2 == null))
                        {
                            skipped.Add(name + " is an alias that points at nothing");
                            continue;
                        }
                        //The alias's position replaces its target's, in the target's composite: where that sits from here is the instances on its path
                        List<Entity> through = path.Take(path.Count - 1).Select(o => o.Item2).ToList();
                        cTransform storedIn = through.Count == 0 ? new cTransform(Vector3.Zero, Vector3.Zero) : new McpPlacements(commands) { PlaceUnpositioned = true }.Evaluate(composite, through).World ?? new cTransform(Vector3.Zero, Vector3.Zero);
                        cTransform stored = InstanceTransform.TransformOf(alias) ?? InstanceTransform.TransformOf(target) ?? new cTransform(Vector3.Zero, Vector3.Zero);
                        items.Add((alias, stored, storedIn, "an alias: moves " + McpScript.EntityName(commands, path[path.Count - 1].Item1, target) + " in this placement only"));
                        continue;
                    }
                    FunctionEntity function = entity as FunctionEntity;
                    cTransform own = InstanceTransform.TransformOf(entity);
                    if (own == null && function != null && (!function.function.IsFunctionType || UnityConnection.ViewerFunctionDrop.HasPosition(function.function.AsFunctionType)))
                        own = new cTransform(Vector3.Zero, Vector3.Zero);
                    if (own == null)
                    {
                        skipped.Add(name + " (" + McpScript.TypeName(commands, composite, entity) + ") has no position");
                        continue;
                    }
                    items.Add((entity, own, new cTransform(Vector3.Zero, Vector3.Zero), null));
                }
                if (items.Count == 0)
                    throw new McpError("Nothing to move: " + string.Join("; ", skipped) + ".");

                //Where each is in the axes asked for ('local' works on the stored transform itself)
                List<cTransform> work = items.Select(o =>
                {
                    cTransform inComposite = InstanceTransform.Compose(o.storedIn, o.stored);
                    return space == "world" ? InstanceTransform.Compose(frame, inComposite) : inComposite;
                }).ToList();
                Vector3 centre = Vector3.Zero;
                foreach (cTransform t in work) centre += t.position / work.Count;

                for (int i = 0; i < items.Count; i++)
                {
                    (Entity entity, cTransform stored, cTransform storedIn, string note) = items[i];
                    cTransform after;
                    if (space == "local")
                        after = InstanceTransform.Compose(stored, new cTransform(translate ?? Vector3.Zero, rotate ?? Vector3.Zero));
                    else
                    {
                        cTransform current = work[i];
                        Vector3 position = current.position;
                        Vector3 rotation = current.rotation;
                        if (rotate != null)
                        {
                            Vector3 about = pivot == "centre" ? centre : current.position;
                            cTransform turned = InstanceTransform.Compose(new cTransform(Vector3.Zero, rotate.Value), new cTransform(current.position - about, current.rotation));
                            position = about + turned.position;
                            rotation = turned.rotation;
                        }
                        position += translate ?? Vector3.Zero;
                        cTransform moved2 = new cTransform(position, rotation);
                        cTransform inComposite = space == "world" ? InstanceTransform.ToLocal(frame, moved2) : moved2;
                        after = InstanceTransform.ToLocal(storedIn, inComposite);
                    }
                    //Only moved: the stored angles stay exactly as they were, not a recomputed spelling of them
                    if (rotate == null)
                        after = new cTransform(after.position, stored.rotation);
                    edit.SetParameter(composite, entity, "position", new JObject() { ["position"] = McpValues.Vector(after.position), ["rotation"] = McpValues.Vector(after.rotation) }, allowCustom: true);
                    JObject row = new JObject()
                    {
                        ["id"] = McpScript.Id(entity.shortGUID),
                        ["name"] = McpScript.EntityName(commands, composite, entity),
                        ["before"] = new JObject() { ["position"] = McpValues.Vector(stored.position), ["rotation"] = McpValues.Vector(stored.rotation) },
                        ["after"] = new JObject() { ["position"] = McpValues.Vector(after.position), ["rotation"] = McpValues.Vector(after.rotation) },
                    };
                    if (space == "world")
                        row["world_after"] = McpValues.Vector(InstanceTransform.Compose(frame, InstanceTransform.Compose(storedIn, after)).position);
                    if (note != null) row["note"] = note;
                    moved.Add(row);
                }
            });

            JObject result = new JObject()
            {
                ["composite"] = composite.name,
                ["space"] = space,
                ["moved"] = moved,
                ["stored_as"] = "before/after are the position parameter as written: relative to " + composite.name + " (an alias's, to its target's composite)",
            };
            if (placed != null) result["placement"] = placed;
            if (skipped.Count != 0) result["not_moved"] = new JArray(skipped);
            return result;
        }
        #endregion

        #region Instance overrides
        private static object ClearInstanceOverrides(McpCall call)
        {
            List<string> only = call.Has("aliases") ? call.StrList("aliases") : null;
            List<string> parameters = call.Has("parameters") ? call.StrList("parameters").Select(o => McpScript.ParamName(McpScript.ParamId(o))).ToList() : null;
            if (only != null && only.Count == 0) throw McpError.Invalid("'aliases' is empty.");
            if (parameters != null && parameters.Count == 0) throw McpError.Invalid("'parameters' is empty.");

            Composite holder = null;
            Entity instance = null;
            List<McpBrowseTools.OverrideFound> found = null;
            McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                Composite root = commands.EntryPoints[0];
                List<Entity> chain = null;
                List<Composite> comps = null;
                if (call.Has("path"))
                {
                    if (call.Has("composite") || call.Has("instance"))
                        throw McpError.Invalid("Give 'path' (one placement from the root), or composite + instance, not both.");
                    chain = McpScript.ChainFrom(commands, root, McpScript.PathSteps(call.Token("path")));
                    comps = McpScript.CompositesAlong(commands, root, chain);
                    holder = comps[chain.Count - 1];
                    instance = chain[chain.Count - 1];
                }
                else
                {
                    if (!call.Has("composite") || !call.Has("instance"))
                        throw McpError.Invalid("Give 'path' (one placement of the instance from the root), or composite + instance.");
                    holder = McpScript.FindComposite(commands, call.Str("composite"));
                    instance = McpScript.FindEntity(commands, holder, call.Str("instance"));
                }
                if (McpScript.InstancedComposite(commands, instance) == null)
                    throw McpError.Invalid(McpScript.EntityName(commands, holder, instance) + " is not a composite instance: only an instance carries overrides of what is inside it.");
                found = McpBrowseTools.FindOverrides(commands, root, chain, comps, holder, instance, call.Cancel);
                if (only != null)
                {
                    //An alias by id (or "Name (id)") or by name
                    bool Names(string written, McpBrowseTools.OverrideFound o)
                    {
                        string text = McpScript.StripId(written, out ShortGuid? shown);
                        ShortGuid? id = shown ?? McpScript.ParseId(text);
                        if (id != null) return id.Value == o.Alias.shortGUID;
                        return string.Equals(text, McpScript.EntityName(commands, o.Owner, o.Alias), StringComparison.OrdinalIgnoreCase);
                    }
                    List<string> missing = only.Where(w => !found.Any(o => Names(w, o))).ToList();
                    if (missing.Count != 0)
                        throw McpError.NotFound("override alias", missing[0], found.Select(o => McpScript.Id(o.Alias.shortGUID)), "get_instance_overrides lists the ones this instance has (" + found.Count + ").");
                    found = found.Where(o => only.Any(w => Names(w, o))).ToList();
                }
            });
            if (found.Count == 0)
                return new JObject() { ["removed"] = new JArray(), ["note"] = "Nothing overrides anything inside this instance" + (only != null ? " among the aliases given." : ".") };

            JArray done = new JArray();
            McpScriptEdit.Outcome outcome = McpScriptEdit.Run("Clear overrides on " + McpEditor.UI(() => McpScript.EntityName(McpEditor.RequireCommands(), holder, instance)), holder, edit =>
            {
                Commands commands = edit.Commands;
                foreach (McpBrowseTools.OverrideFound o in found)
                {
                    if (o.Owner.GetEntityByID(o.Alias.shortGUID) != o.Alias) continue;
                    List<string> overrides = o.Alias.parameters.Where(p => p.name != ShortGuids.name).Select(p => McpScript.ParamName(p.name)).ToList();
                    List<string> clearing = parameters == null ? overrides : overrides.Where(p => parameters.Contains(p, StringComparer.OrdinalIgnoreCase)).ToList();
                    if (clearing.Count == 0) continue;
                    bool linked = o.Alias.childLinks.Count != 0 || o.Owner.GetEntities().Any(e => e.childLinks.Any(l => l.linkedEntityID == o.Alias.shortGUID));
                    JObject row = new JObject() { ["alias"] = McpScript.Id(o.Alias.shortGUID), ["composite"] = o.Owner.name, ["name"] = McpScript.EntityName(commands, o.Owner, o.Alias) };
                    if (!linked && clearing.Count == overrides.Count)
                    {
                        edit.Delete(o.Owner, o.Alias);
                        row["removed"] = "deleted (" + string.Join(", ", clearing) + ")";
                    }
                    else
                    {
                        foreach (string parameter in clearing)
                            edit.RemoveParameter(o.Owner, o.Alias, parameter);
                        row["removed"] = "cleared " + string.Join(", ", clearing) + (linked ? "; kept for its links" : "; its other overrides stay");
                    }
                    done.Add(row);
                }
            });
            JObject result = new JObject() { ["instance"] = McpEditor.UI(() => McpScript.EntityName(McpEditor.RequireCommands(forEditing: false), holder, instance)), ["removed"] = done };
            if (!outcome.Changed)
                result["note"] = "None of those overrides " + (parameters != null ? "set " + string.Join(", ", parameters) : "were found") + ": nothing changed.";
            return result;
        }
        #endregion

        #region Proxies
        private static object RetargetProxy(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                McpEditor.RequireUndoIdle();
                Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                Entity found = McpScript.FindEntity(commands, composite, call.Str("proxy", required: true));
                if (!(found is ProxyEntity proxy))
                    throw new McpError(McpScript.EntityName(commands, composite, found) + " is " + McpScript.WithArticle(McpScript.Kind(found)) + ", not a proxy.");

                Composite root = commands.EntryPoints[0];
                List<string> steps = McpScript.PathSteps(call.Token("path"));
                ShortGuid[] fromRoot = McpScript.ResolvePath(commands, root, steps, out Composite targetComposite, out Entity target);
                if (!(target is FunctionEntity targetFunction))
                    throw new McpError("A proxy points at a function entity or composite instance; '" + string.Join("/", steps) + "' is a " + McpScript.Kind(target) + ".");
                ShortGuid[] newPath = new[] { root.shortGUID }.Concat(fromRoot).ToArray();
                if (commands.Utils.GetResolvedTarget(commands.Utils.ResolveProxy(newPath)).Item2 != target)
                    throw new McpError("That path does not resolve from the root composite as a proxy path. get_placements gives paths from the root.");

                ShortGuid[] pathBefore = (ShortGuid[])(proxy.proxy?.path ?? new ShortGuid[0]).Clone();
                ShortGuid functionBefore = proxy.function;
                bool wasDead = commands.Utils.IsDeadProxy(proxy);
                string before = DescribeStoredPath(commands, proxy);
                string targetName = McpScript.EntityName(commands, targetComposite, target);
                if (pathBefore.SequenceEqual(newPath) && functionBefore == targetFunction.function)
                    return new JObject() { ["proxy"] = McpScript.Id(proxy.shortGUID), ["unchanged"] = "It already points there.", ["target"] = before };

                //As the editor's Change Target: the path and the target's type change, the links through the proxy stay
                proxy.proxy = new EntityPath(newPath);
                proxy.function = targetFunction.function;
                UndoStack.Current.Apply(new ProxyRetargetEdit(composite, proxy, pathBefore, functionBefore,
                    "AI: Re-point " + UndoLabels.Entity(composite, proxy) + " to " + targetName));

                return new JObject()
                {
                    ["proxy"] = McpScript.Id(proxy.shortGUID),
                    ["name"] = McpScript.EntityName(commands, composite, proxy),
                    ["was"] = (wasDead ? "dead: " : "") + before,
                    ["now"] = McpScript.DescribePath(commands, commands.Utils.ResolveProxy(proxy), proxy.proxy.path),
                    ["type"] = McpScript.TypeName(commands, targetComposite, target),
                    ["links"] = proxy.childLinks.Count + composite.GetEntities().Sum(o => o.childLinks.Count(l => l.linkedEntityID == proxy.shortGUID)),
                };
            });
        }

        private static object ListDeadProxies(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: false);
                IEnumerable<Composite> scope = call.Has("composite")
                    ? new List<Composite>() { McpScript.FindComposite(commands, call.Str("composite")) }
                    : commands.Entries.Where(o => o != null);
                string filter = call.Str("filter");
                int limit = McpPaging.Limit(call, 100), offset = McpPaging.Offset(call);

                JArray proxies = new JArray();
                int total = 0, composites = 0;
                foreach (KeyValuePair<Composite, List<ProxyEntity>> dead in commands.Utils.GetDeadProxies(scope).OrderBy(o => o.Key.name))
                {
                    bool counted = false;
                    foreach (ProxyEntity proxy in dead.Value)
                    {
                        string name = McpScript.EntityName(commands, dead.Key, proxy);
                        if (filter != null && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        total++;
                        if (!counted) { composites++; counted = true; }
                        if (total <= offset || proxies.Count >= limit) continue;
                        proxies.Add(new JObject()
                        {
                            ["composite"] = dead.Key.name,
                            ["id"] = McpScript.Id(proxy.shortGUID),
                            ["name"] = name,
                            ["was"] = DeadProxyTargetType(commands, proxy),
                            ["stored_path"] = DescribeStoredPath(commands, proxy),
                            ["links"] = proxy.childLinks.Count + dead.Key.GetEntities().Sum(o => o.childLinks.Count(l => l.linkedEntityID == proxy.shortGUID)),
                        });
                    }
                }
                JObject result = new JObject()
                {
                    ["count"] = total,
                    ["composites"] = composites,
                    ["proxies"] = proxies,
                };
                if (total != 0)
                    result["hint"] = "retarget_proxy re-points one; get_placements and find_entities (within 'root') give paths from the root.";
                McpPaging.Describe(result, total, offset, proxies.Count);
                return result;
            });
        }

        /// <summary>The function type or composite a dead proxy's target had, or null when this level has no name for it (as the inspector shows it).</summary>
        private static string DeadProxyTargetType(Commands commands, ProxyEntity proxy)
        {
            if (proxy.function.IsFunctionType)
                return proxy.function.AsFunctionType.ToString();
            return commands.GetComposite(proxy.function)?.name;
        }

        /// <summary>
        /// A proxy's stored path hop by hop, naming what each hop reaches and marking the first that reaches
        /// nothing - read from the level's root, as the inspector's hierarchy box shows a dead proxy.
        /// </summary>
        private static string DescribeStoredPath(Commands commands, ProxyEntity proxy)
        {
            ShortGuid[] path = proxy.proxy?.path;
            if (path == null || path.Length == 0)
                return "(no path)";
            Composite current = commands.EntryPoints[0];
            int first = 0;
            if (path.Length > 1)
            {
                //A proxy path starts with the composite it is read from: a composite id, not a hop
                Composite named = commands.GetComposite(path[0]);
                if (named != null && current?.GetEntityByID(path[1]) == null)
                    current = named;
                first = 1;
            }
            List<string> hops = new List<string>();
            for (int i = first; i < path.Length; i++)
            {
                if (path[i] == ShortGuid.Invalid)
                    break;
                if (i > first && path[i] == path[i - 1])
                    continue; //a doubled hop, which the resolver skips too
                Entity hop = current?.GetEntityByID(path[i]);
                if (hop == null)
                {
                    hops.Add("[" + McpScript.Id(path[i]) + "] MISSING" + (i != path.Length - 1 && path[i + 1] != ShortGuid.Invalid ? " -> ..." : ""));
                    break;
                }
                hops.Add(commands.Utils.GetEntityName(current, hop) + " (" + McpScript.Id(hop.shortGUID) + ")");
                current = McpScript.InstancedComposite(commands, hop);
            }
            return string.Join(" -> ", hops);
        }
        #endregion

        #region Composites and folders
        private static object ManageComposites(McpCall call)
        {
            string action = (call.Str("action", required: true) ?? "").Trim().ToLowerInvariant();
            if (!_manageActions.Contains(action))
                throw new McpError("'action' is one of: " + string.Join(", ", _manageActions) + ".");
            return McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                McpEditor.RequireUndoIdle();
                switch (action)
                {
                    case "rename": return RenameComposite(call, commands);
                    case "move": return MoveComposites(call, commands);
                    case "rename_folder": return RenameFolder(call, commands);
                    case "delete": return DeleteComposites(call, commands);
                    default: return CreateFolder(call, commands);
                }
            });
        }

        private static string Leaf(string path)
        {
            string normalised = McpScript.NormalisePath(path);
            int slash = normalised.LastIndexOf('\\');
            return slash < 0 ? normalised : normalised.Substring(slash + 1);
        }

        private static string ParentFolder(string path)
        {
            string normalised = McpScript.NormalisePath(path);
            int slash = normalised.LastIndexOf('\\');
            return slash < 0 ? "" : normalised.Substring(0, slash);
        }

        private static bool IsFolderPlaceholder(Composite composite) => (composite?.name ?? "").EndsWith("\\") || (composite?.name ?? "").EndsWith("/");

        /// <summary>Whether a path is the folder itself or lies somewhere inside it (any case, either slash).</summary>
        private static bool IsInFolder(string path, string folder)
        {
            string p = McpScript.NormalisePath(path), f = McpScript.NormalisePath(folder);
            if (f.Length == 0) return true;
            return string.Equals(p, f, StringComparison.OrdinalIgnoreCase) || p.StartsWith(f + "\\", StringComparison.OrdinalIgnoreCase);
        }

        private static List<Composite> CompositesUnder(Commands commands, string folder)
        {
            string f = McpScript.NormalisePath(folder);
            if (f.Length == 0) return new List<Composite>();
            return commands.Entries.Where(o => o != null && IsInFolder(o.name, f)).ToList();
        }

        /// <summary>Is something already at this path - a composite, or a folder holding composites - other than <paramref name="ignoring"/>?</summary>
        private static bool PathInUse(Commands commands, string path, ICollection<Composite> ignoring)
        {
            return commands.Entries.Any(o => o != null && (ignoring == null || !ignoring.Contains(o)) && IsInFolder(o.name, path));
        }

        /// <summary>Why a composite cannot be called this (the rules Create Composite applies), or null.</summary>
        private static string PathProblem(string path)
        {
            if (path.Length == 0)
                return "The new path is empty.";
            if (path.Split('\\').Any(o => o.Trim().Length == 0))
                return "The path has an empty folder in it.";
            string upper = path.ToUpperInvariant();
            if (upper.Contains("REQUIRED_ASSETS") || upper.Contains("TEMPLATE") || upper.Contains("\\PHYSICS\\") || upper.StartsWith("PHYSICS\\"))
                return "Composites named with REQUIRED_ASSETS, TEMPLATE or a PHYSICS folder are never shown in the level; pick another path.";
            return null;
        }

        private static void RefuseEntryPoints(Commands commands, IEnumerable<Composite> composites, bool allowRoot, string doing)
        {
            for (int i = 0; i < commands.EntryPoints.Length; i++)
            {
                Composite entry = commands.EntryPoints[i];
                if (entry == null || (allowRoot && i == 0) || !composites.Contains(entry)) continue;
                throw new McpError(entry.name + " is the level's " + (i == 0 ? "root" : i == 1 ? "global" : "pause menu") + " composite: the level needs it " + (doing == "delete" ? "" : "at its current path ") + "to load, so it cannot be " + (doing == "delete" ? "deleted" : "moved or renamed") + ".");
            }
        }

        private static JObject ApplyRenames(Commands commands, List<(Composite composite, string after)> renames, string label)
        {
            List<CompositeRenameEdit.Rename> edits = renames.Select(o => new CompositeRenameEdit.Rename() { Composite = o.composite.shortGUID, Before = o.composite.name, After = o.after }).ToList();
            JArray changed = new JArray(edits.Take(100).Select(o => new JObject() { ["id"] = McpScript.Id(o.Composite), ["from"] = o.Before, ["to"] = o.After }));
            //The rename edit refreshes the browser, the instance rows and the titles (CompositeBrowser.AfterCompositesRenamed)
            UndoStack.Current.Apply(new CompositeRenameEdit(edits, "AI: " + label));
            return new JObject() { ["renamed"] = changed, ["count"] = edits.Count };
        }

        private static object RenameComposite(McpCall call, Commands commands)
        {
            Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
            if (IsFolderPlaceholder(composite))
                throw new McpError("That is a folder: use rename_folder.");
            string wanted = McpScript.NormalisePath(call.Str("new_path", required: true));
            string target = wanted.Contains("\\") ? wanted : (ParentFolder(composite.name).Length == 0 ? wanted : ParentFolder(composite.name) + "\\" + wanted);
            RefuseEntryPoints(commands, new[] { composite }, allowRoot: true, doing: "rename");
            if (string.Equals(McpScript.NormalisePath(composite.name), target, StringComparison.Ordinal))
                return new JObject() { ["unchanged"] = composite.name + " already has that path." };
            string problem = PathProblem(target);
            if (problem != null)
                throw new McpError(problem);
            if (PathInUse(commands, target, new[] { composite }))
                throw new McpError("Something is already at " + target + " (a composite, or a folder holding composites).");
            return ApplyRenames(commands, new List<(Composite, string)>() { (composite, target) }, "Rename composite " + McpScript.CompositeLeaf(composite));
        }

        private static object MoveComposites(McpCall call, Commands commands)
        {
            if (!call.Has("to_folder"))
                throw new McpError("'to_folder' is required: the folder to move into ('' for the top level).");
            string folder = McpScript.NormalisePath(call.Str("to_folder") ?? "");
            if (folder.Length != 0 && PathProblem(folder) != null)
                throw new McpError(PathProblem(folder));

            if (call.Has("folder"))
            {
                if (call.Has("composites") || call.Has("composite"))
                    throw new McpError("Move either 'composites' or a 'folder', not both.");
                string from = McpScript.NormalisePath(call.Str("folder"));
                return MoveFolderContents(commands, from, (folder.Length == 0 ? "" : folder + "\\") + Leaf(from));
            }

            List<string> names = call.StrList("composites");
            if (call.Has("composite")) names.Add(call.Str("composite"));
            if (names.Count == 0)
                throw new McpError("Say what to move: 'composites', or a 'folder'.");
            List<Composite> composites = names.Select(o => McpScript.FindComposite(commands, o)).Distinct().ToList();
            RefuseEntryPoints(commands, composites, allowRoot: true, doing: "move");
            if (composites.Any(IsFolderPlaceholder))
                throw new McpError("A folder is moved with 'folder', not 'composites'.");

            List<(Composite, string)> renames = new List<(Composite, string)>();
            foreach (Composite composite in composites)
            {
                string target = (folder.Length == 0 ? "" : folder + "\\") + McpScript.CompositeLeaf(composite);
                if (!string.Equals(McpScript.NormalisePath(composite.name), target, StringComparison.OrdinalIgnoreCase))
                    renames.Add((composite, target)); //else already there
            }
            //Only what moves frees its path: one already in the folder stays there, in the way of a namesake
            List<Composite> moving = renames.Select(o => o.Item1).ToList();
            HashSet<string> taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string target in renames.Select(o => o.Item2))
                if (!taken.Add(target) || PathInUse(commands, target, moving))
                    throw new McpError("Something is already at " + target + ": nothing was moved.");
            if (renames.Count == 0)
                return new JObject() { ["unchanged"] = "They are already in " + (folder.Length == 0 ? "the top level" : folder) + "." };
            return ApplyRenames(commands, renames, "Move " + (renames.Count == 1 ? McpScript.CompositeLeaf(renames[0].Item1) : renames.Count + " composites") + " to " + (folder.Length == 0 ? "the top level" : folder));
        }

        private static object RenameFolder(McpCall call, Commands commands)
        {
            string from = McpScript.NormalisePath(call.Str("folder", required: true));
            string wanted = McpScript.NormalisePath(call.Str("new_path", required: true));
            string to = wanted.Contains("\\") ? wanted : (ParentFolder(from).Length == 0 ? wanted : ParentFolder(from) + "\\" + wanted);
            return MoveFolderContents(commands, from, to);
        }

        /// <summary>
        /// Rewrite the path of everything in a folder (renaming or moving it) as the Composite Browser does:
        /// nothing changes unless all of it can.
        /// </summary>
        private static object MoveFolderContents(Commands commands, string from, string to)
        {
            if (from.Length == 0)
                throw new McpError("Say which folder.");
            if (string.Equals(from, to, StringComparison.Ordinal))
                return new JObject() { ["unchanged"] = "The folder already has that path." };
            string problem = PathProblem(to);
            if (problem != null)
                throw new McpError(problem);
            if (IsInFolder(to, from) && !string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
                throw new McpError("A folder cannot be moved inside itself.");
            List<Composite> affected = CompositesUnder(commands, from);
            if (affected.Count == 0)
                throw new McpError("There is no folder '" + from + "' (find_composites lists paths).");
            if (affected.All(o => string.Equals(McpScript.NormalisePath(o.name), from, StringComparison.OrdinalIgnoreCase) && !IsFolderPlaceholder(o)))
                throw new McpError("'" + from + "' is a composite, not a folder: rename it with action 'rename'.");
            if (PathInUse(commands, to, affected))
                throw new McpError("Something is already at " + to + ": nothing was moved.");
            RefuseEntryPoints(commands, affected, allowRoot: false, doing: "move");

            List<(Composite, string)> renames = new List<(Composite, string)>();
            foreach (Composite composite in affected)
            {
                string normalised = McpScript.NormalisePath(composite.name);
                renames.Add((composite, to + normalised.Substring(from.Length) + (IsFolderPlaceholder(composite) ? "\\" : "")));
            }
            return ApplyRenames(commands, renames, "Move folder " + from);
        }

        private static object DeleteComposites(McpCall call, Commands commands)
        {
            List<Composite> composites;
            if (call.Has("folder"))
            {
                if (call.Has("composites") || call.Has("composite"))
                    throw new McpError("Delete either 'composites' or a 'folder', not both.");
                string folder = McpScript.NormalisePath(call.Str("folder"));
                composites = CompositesUnder(commands, folder);
                if (composites.Count == 0)
                    throw new McpError("There is no folder '" + folder + "'.");
            }
            else
            {
                List<string> names = call.StrList("composites");
                if (call.Has("composite")) names.Add(call.Str("composite"));
                if (names.Count == 0)
                    throw new McpError("Say what to delete: 'composites', or a 'folder'.");
                composites = names.Select(o => McpScript.FindComposite(commands, o)).Distinct().ToList();
            }
            RefuseEntryPoints(commands, composites, allowRoot: false, doing: "delete");

            //What goes with them, for the report: the edit removes the instances everywhere else, links into them, and aliases through them
            HashSet<ShortGuid> ids = new HashSet<ShortGuid>(composites.Select(o => o.shortGUID));
            HashSet<Composite> going = new HashSet<Composite>(composites);
            JArray instances = new JArray();
            int instanceCount = 0;
            foreach (Composite other in commands.Entries)
            {
                if (other == null || going.Contains(other)) continue;
                foreach (FunctionEntity function in other.functions)
                {
                    if (!ids.Contains(function.function)) continue;
                    instanceCount++;
                    if (instances.Count < 50) instances.Add(other.name + ": " + McpScript.EntityName(commands, other, function) + " (" + McpScript.Id(function.shortGUID) + ")");
                }
            }
            HashSet<ProxyEntity> deadBefore = new HashSet<ProxyEntity>(commands.Utils.GetDeadProxies(commands.Entries.Where(o => o != null && !going.Contains(o))).SelectMany(o => o.Value));
            int aliasesBefore = commands.Entries.Where(o => o != null && !going.Contains(o)).Sum(o => o.aliases.Count);

            int real = composites.Count(o => !IsFolderPlaceholder(o));
            string label = composites.Count == 1 ? "Delete " + McpScript.CompositeLeaf(composites[0]) : "Delete " + real + " composites";
            UndoStack.Current.Apply(new CompositeDeleteEdit(new List<Composite>(composites), "AI: " + label));
            //As the browser does after a delete: the composite on screen may have lost instances
            Singleton.Editor?.CompositeBrowser?.Reload();

            List<Composite> left = commands.Entries.Where(o => o != null).ToList();
            List<string> newlyDead = commands.Utils.GetDeadProxies(left).SelectMany(o => o.Value.Where(p => !deadBefore.Contains(p)).Select(p => o.Key.name + ": " + McpScript.EntityName(commands, o.Key, p))).ToList();
            return new JObject()
            {
                ["deleted"] = new JArray(composites.Take(100).Select(o => o.name)),
                ["deleted_count"] = real,
                ["instances_removed"] = instanceCount,
                ["instances"] = instances,
                ["aliases_removed"] = aliasesBefore - left.Sum(o => o.aliases.Count),
                ["proxies_now_dead"] = new JArray(newlyDead.Take(50)),
                ["note"] = newlyDead.Count == 0 ? null : newlyDead.Count + " proxies into what was deleted are kept, dead (list_dead_proxies, retarget_proxy). undo brings everything back.",
            };
        }

        private static object CreateFolder(McpCall call, Commands commands)
        {
            string folder = McpScript.NormalisePath(call.Str("folder", required: true));
            string problem = PathProblem(folder);
            if (problem != null)
                throw new McpError(problem);
            if (PathInUse(commands, folder, null))
                throw new McpError("Something is already at " + folder + ".");
            //An empty folder is a placeholder composite whose name ends in a separator, as the Add Folder dialog makes it
            Composite placeholder = commands.AddComposite(folder + "\\");
            UndoStack.Current.Apply(new CompositeAddEdit(placeholder, true, "AI: Add folder " + folder));
            return new JObject() { ["folder"] = folder, ["note"] = "create_composite with a path inside it puts a composite there." };
        }
        #endregion

        #region Placements and zones
        private static object GetPlacements(McpCall call)
        {
            int limit = McpPaging.Limit(call, 50), offset = McpPaging.Offset(call);
            bool withZones = call.Bool("zones");
            string include = (call.Str("include") ?? "real").Trim().ToLowerInvariant();
            if (include != "real" && include != "all")
                throw new McpError("'include' is 'real' (placements the game makes) or 'all'.");
            using (McpEditorTools.Heartbeat(call, "Finding placements"))
            return McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: false);
                Commands commands = content.Level.Commands;
                Composite root = commands.EntryPoints[0];
                McpPlacements walker = new McpPlacements(commands) { PlaceUnpositioned = true };
                List<McpPlacements.Placement> found = new List<McpPlacements.Placement>();
                int total;
                JObject result = new JObject();

                if (call.Has("path"))
                {
                    if (call.Has("composite") || call.Has("entity"))
                        throw new McpError("Give 'path', or 'composite' (and 'entity'), not both.");
                    List<Entity> chain = McpScript.ChainFrom(commands, root, McpScript.PathSteps(call.Token("path")));
                    found.Add(walker.Evaluate(root, chain));
                    total = 1;
                    offset = 0;
                }
                else
                {
                    Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                    Entity entity = call.Has("entity") ? McpScript.FindEntity(commands, composite, call.Str("entity")) : null;
                    result["composite"] = composite.name;
                    if (entity != null) result["entity"] = McpScript.Brief(commands, composite, entity);
                    total = walker.PlacementsOf(root, composite, entity, found, offset + limit, call.Cancel, realOnly: include == "real");
                    found = found.Skip(offset).ToList();
                    if (total == 0 && walker.NotReal != 0)
                        result["note"] = "Every placement (" + walker.NotReal + ") is deleted, a template (under an is_template instance) or a repeat of an is_shared composite, so the game places none of them as such: include 'all' lists them.";
                    else if (total == 0)
                        result["note"] = composite == root && entity == null ? null : "Not placed in the level: nothing reachable from the root composite instances " + composite.name + " (find_references lists its instances).";
                    else if (walker.NotReal != 0)
                        result["not_real"] = walker.NotReal + " more placement" + (walker.NotReal == 1 ? " is" : "s are") + " deleted, templates or shared repeats (include 'all' lists them).";
                    if (walker.Truncated)
                        result["truncated"] = "The walk stopped after " + walker.Visited + " entities; there may be more placements.";
                }

                List<SyncedZone> zones = withZones ? ZoneMembership.Calculate(content.Level) : null;
                JArray placements = new JArray();
                foreach (McpPlacements.Placement placement in found)
                {
                    JObject described = DescribePlacement(commands, root, placement);
                    if (zones != null)
                        described["zones"] = new JArray(ZonesClaiming(commands, zones, placement.Chain).Select(o => DescribeZone(commands, root, o, 0)));
                    placements.Add(described);
                }
                result["count"] = total;
                result["space"] = "world";
                result["placements"] = placements;
                McpPaging.Describe(result, total, offset, placements.Count);
                return result;
            });
        }

        private static object GetZones(McpCall call)
        {
            string filter = call.Str("filter");
            int rootsLimit = Math.Max(0, call.Int("roots_limit", 10));
            return McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: false);
                Commands commands = content.Level.Commands;
                Composite root = commands.EntryPoints[0];
                List<SyncedZone> zones = ZoneMembership.Calculate(content.Level);
                JObject result = new JObject();
                if (call.Has("path"))
                {
                    List<Entity> chain = McpScript.ChainFrom(commands, root, McpScript.PathSteps(call.Token("path")));
                    result["path"] = DescribePlacement(commands, root, new McpPlacements(commands) { PlaceUnpositioned = true }.Evaluate(root, chain));
                    zones = ZonesClaiming(commands, zones, chain);
                    if (zones.Count == 0)
                        result["note"] = "No zone claims this placement: it is in no zone's trigger sequences, nor inside anything that is.";
                }
                List<SyncedZone> shown = zones.Where(o => filter == null || (o.name ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                if (shown.Count == 0 && filter != null && zones.Count != 0)
                {
                    //A misspelt or reworded name: the nearest few, as find_places ranks them
                    List<string> near = zones.Select(o => McpRegion.ZoneName(commands, o)).Distinct().Select(o => (name: o, score: McpRegion.Score(filter, o))).Where(o => o.score > 0).OrderByDescending(o => o.score).Take(8).Select(o => o.name).ToList();
                    result["note"] = "No zone name contains '" + filter + "'." + (near.Count != 0 ? " Nearest: " + string.Join(", ", near) + "." : "") + " find_places searches zones, composites, instances and models by name.";
                }
                result["count"] = shown.Count;
                McpPaging.Page(call, shown, result, "zones", o =>
                {
                    JObject described = DescribeZone(commands, root, o, call.Has("path") ? 0 : rootsLimit);
                    Composite composite = commands.GetComposite(new ShortGuid(o.zone_composite));
                    described["zone_links"] = McpZoneTools.LinksOfZone(commands, composite, composite?.GetEntityByID(new ShortGuid(o.zone_entity)));
                    return described;
                }, 100);
                return result;
            });
        }

        /// <summary>The zones claiming a placement (entity ids from the root): one of their roots is it or holds it, as EditorUtils.FindZonesForEntity decides.</summary>
        private static List<SyncedZone> ZonesClaiming(Commands commands, List<SyncedZone> zones, IList<Entity> chainFromRoot)
        {
            List<uint> path = chainFromRoot.Select(o => o.shortGUID.AsUInt32).ToList();
            List<SyncedZone> claiming = new List<SyncedZone>();
            HashSet<ulong> seen = new HashSet<ulong>();
            foreach (SyncedZone zone in zones)
            {
                bool claims = zone.roots.Any(root => root != null && root.Count != 0 && root.Count <= path.Count && !root.Where((id, i) => id != path[i]).Any());
                if (claims && seen.Add(((ulong)zone.zone_composite << 32) | zone.zone_entity))
                    claiming.Add(zone);
            }
            return claiming;
        }

        internal static JObject DescribeZone(Commands commands, Composite root, SyncedZone zone, int rootsLimit)
        {
            Composite composite = commands.GetComposite(new ShortGuid(zone.zone_composite));
            Entity entity = composite?.GetEntityByID(new ShortGuid(zone.zone_entity));
            JObject described = new JObject()
            {
                ["name"] = string.IsNullOrWhiteSpace(zone.name) && composite != null && entity != null ? McpScript.EntityName(commands, composite, entity) : zone.name,
                ["composite"] = composite?.name,
                ["id"] = McpScript.Id(new ShortGuid(zone.zone_entity)),
                ["zone_path"] = NamesFromRoot(commands, root, zone.zone_path),
                ["claims"] = zone.roots.Count,
            };
            if (rootsLimit > 0)
                described["roots"] = new JArray(zone.roots.Take(rootsLimit).Select(o => NamesFromRoot(commands, root, o)));
            return described;
        }

        /// <summary>Entity ids from the root composite, as "name (id)" steps.</summary>
        private static JArray NamesFromRoot(Commands commands, Composite root, IEnumerable<uint> ids)
        {
            JArray names = new JArray();
            Composite current = root;
            foreach (uint raw in ids ?? Enumerable.Empty<uint>())
            {
                ShortGuid id = new ShortGuid(raw);
                Entity entity = current?.GetEntityByID(id);
                names.Add(entity == null ? McpScript.Id(id) : McpScript.EntityName(commands, current, entity) + " (" + McpScript.Id(id) + ")");
                current = entity == null ? null : McpScript.InstancedComposite(commands, entity);
            }
            return names;
        }

        private static readonly Dictionary<Composite, HashSet<ShortGuid>> _positionLinked = new Dictionary<Composite, HashSet<ShortGuid>>();
        private static int _positionLinkedVersion = -1;

        /// <summary>Forget the cached position links (the level is closing: its composites must not stay reachable).</summary>
        internal static void DropPositionLinked()
        {
            lock (_positionLinked)
            {
                _positionLinked.Clear();
                _positionLinkedVersion = -1;
            }
        }

        /// <summary>
        /// The entities of a composite whose position a link feeds (cached until the script changes): an entity whose own
        /// position parameter is linked (its childLinks from 'position', as instancing reads it), or one an alias in the
        /// composite stands for whose position is linked.
        /// </summary>
        private static HashSet<ShortGuid> PositionLinked(Composite composite)
        {
            McpCollision.Hook();
            lock (_positionLinked)
            {
                if (_positionLinkedVersion != McpCollision.Version)
                {
                    _positionLinked.Clear();
                    _positionLinkedVersion = McpCollision.Version;
                }
                if (_positionLinked.TryGetValue(composite, out HashSet<ShortGuid> known))
                    return known;
                bool Fed(Entity entity) => entity.childLinks.Any(o => o.thisParamID == ShortGuids.position && composite.GetEntityByID(o.linkedEntityID) != null);
                HashSet<ShortGuid> linked = new HashSet<ShortGuid>();
                foreach (Entity entity in composite.GetEntities())
                {
                    if (entity is AliasEntity alias)
                    {
                        //An alias of an entity of this composite: a link on the alias feeds that entity
                        ShortGuid[] path = alias.alias?.path;
                        int length = path?.Length ?? 0;
                        if (length > 0 && path[length - 1] == ShortGuid.Invalid) length--;
                        if (length == 1 && composite.GetEntityByID(path[0]) != null && Fed(alias)) linked.Add(path[0]);
                    }
                    else if (Fed(entity))
                        linked.Add(entity.shortGUID);
                }
                _positionLinked[composite] = linked;
                return linked;
            }
        }

        /// <summary>A placement for a result: the path as names and as ids, and where it sits.</summary>
        public static JObject DescribePlacement(Commands commands, Composite start, McpPlacements.Placement placement)
        {
            JArray names = new JArray();
            JArray ids = new JArray();
            Composite current = start;
            foreach (Entity entity in placement.Chain)
            {
                //A step with no name is given by its id, so the path can still be passed back
                string name = current == null ? null : McpScript.EntityName(commands, current, entity);
                names.Add(string.IsNullOrWhiteSpace(name) ? McpScript.Id(entity.shortGUID) : name);
                ids.Add(McpScript.Id(entity.shortGUID));
                current = McpScript.InstancedComposite(commands, entity);
            }
            JObject described = new JObject() { ["path"] = names, ["ids"] = ids };
            if (placement.World != null)
            {
                described["position"] = McpValues.Vector(placement.World.position);
                described["rotation"] = McpValues.Vector(placement.World.rotation);
            }
            Entity last = placement.Chain.Count == 0 ? null : placement.Chain[placement.Chain.Count - 1];
            if (placement.OverriddenBy != null)
                described["position_from"] = "alias " + McpScript.EntityName(commands, placement.OverrideOwner, placement.OverriddenBy) + " (" + McpScript.Id(placement.OverriddenBy.shortGUID) + ") in " + placement.OverrideOwner.name;
            else if (placement.PositionDefaulted)
                described["position_from"] = "default: it has no position, so it sits at its composite's origin";
            if (last != null && placement.Composite != null && !(last is AliasEntity) && !(last is ProxyEntity) && PositionLinked(placement.Composite).Contains(last.shortGUID))
                described["position_linked"] = "A link feeds its position, so where it ends up is set at run time; this is the stored value.";
            if (placement.Deleted) described["deleted"] = true;
            if (placement.Template) described["template"] = true;
            if (placement.SharedRepeat) described["shared_repeat"] = true;
            if (placement.DeleteStandardCollision || placement.DeleteBallisticCollision)
                described["collision_deleted"] = placement.DeleteStandardCollision && placement.DeleteBallisticCollision ? "both" : placement.DeleteStandardCollision ? "standard" : "ballistic";

            //An alias or proxy is not placed itself: say what it stands for, and where that sits through this placement
            if ((last is AliasEntity || last is ProxyEntity) && placement.Composite != null)
            {
                List<Tuple<Composite, Entity>> resolved = last is AliasEntity alias ? commands.Utils.ResolveAlias(alias, placement.Composite) : commands.Utils.ResolveProxy((ProxyEntity)last);
                (Composite targetComposite, Entity target) = commands.Utils.GetResolvedTarget(resolved);
                if (target == null)
                    described["note"] = "A " + (last is AliasEntity ? "alias" : "proxy") + " that points at nothing.";
                else
                {
                    JObject stands = new JObject()
                    {
                        ["composite"] = targetComposite?.name,
                        ["entity"] = McpScript.EntityName(commands, targetComposite, target),
                        ["id"] = McpScript.Id(target.shortGUID),
                        ["type"] = McpScript.TypeName(commands, targetComposite, target),
                    };
                    cTransform world = null;
                    if (last is AliasEntity)
                    {
                        //Through this placement: the chain to the alias, then the alias's path (the walk applies the alias's own override, unless an outer one wins)
                        List<Entity> through = placement.Chain.Take(placement.Chain.Count - 1).Concat(resolved.Select(o => o.Item2)).ToList();
                        stands["path"] = new JArray(through.Select(o => McpScript.Id(o.shortGUID)));
                        try { world = new McpPlacements(commands) { PlaceUnpositioned = true }.Evaluate(start, through).World; } catch (McpError) { }
                        if (InstanceTransform.TransformOf(last) != null)
                            stands["overrides_position"] = true;
                    }
                    else
                    {
                        //A proxy's path runs from the level root
                        List<Entity> fromRoot = resolved.Select(o => o.Item2).ToList();
                        stands["path_from_root"] = new JArray(fromRoot.Select(o => McpScript.Id(o.shortGUID)));
                        try { world = new McpPlacements(commands) { PlaceUnpositioned = true }.Evaluate(commands.EntryPoints[0], fromRoot).World; } catch (McpError) { }
                    }
                    if (world != null)
                    {
                        stands["position"] = McpValues.Vector(world.position);
                        stands["rotation"] = McpValues.Vector(world.rotation);
                    }
                    described["stands_for"] = stands;
                }
            }
            return described;
        }
        #endregion

        #region Enum strings
        private static object ListEnumStringValues(McpCall call)
        {
            string typeName = call.Str("type", required: true).Trim();
            string name = Enum.GetNames(typeof(EnumStringType)).FirstOrDefault(o => string.Equals(o, typeName, StringComparison.OrdinalIgnoreCase));
            if (name == null)
                throw new McpError("There is no enum-string type '" + typeName + "'. They are: " + string.Join(", ", Enum.GetNames(typeof(EnumStringType))) + ".");
            EnumStringType type = (EnumStringType)Enum.Parse(typeof(EnumStringType), name);
            string filter = call.Str("filter");
            string animationSet = call.Str("animation_set");
            if (animationSet != null && type != EnumStringType.ANIMATION)
                throw new McpError("'animation_set' only narrows ANIMATION values.");
            bool Matches(string value, string description) => filter == null
                || (value ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                || (description ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;

            return McpEditor.UI(() =>
            {
                LevelContent content = Singleton.Editor?.CompositeBrowser?.Content;
                bool levelOpen = content != null && content.IsLevelDataLoaded && content.Level != null;
                List<JToken> values = new List<JToken>();

                if (type == EnumStringType.MATERIAL)
                {
                    //Not in the picker lists: the inspector edits a material with its own dialog, over the open level's materials
                    if (!levelOpen)
                        throw new McpError("MATERIAL values are the open level's materials: load_level first.");
                    foreach (string material in content.Level.Materials.Entries.Where(o => o?.Name != null).Select(o => o.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(o => o, StringComparer.OrdinalIgnoreCase))
                        if (Matches(material, null)) values.Add(material);
                }
                else
                {
                    //What the picker does on opening: fill anything not filled yet (global once, the level's per level)
                    EnumStringListViewItems.PopulateGlobalEntries();
                    if (levelOpen)
                        EnumStringListViewItems.PopulateLevelSpecificEntries();
                    Tuple<ListViewItem[], bool> items = EnumStringListViewItems.GetItems(type);
                    if (items == null)
                        throw new McpError(levelOpen ? "OpenCAGE has no list of " + type + " values." : type + " values come from the open level: load_level first.");

                    if (type == EnumStringType.ANIMATION)
                    {
                        //The animations of every set, or of one set (the picker narrows it to the entity's AnimationSet)
                        Dictionary<string, List<string>> sets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                        List<string> order = new List<string>();
                        foreach (ListViewItem item in items.Item1)
                        {
                            string set = item.SubItems.Count > 1 ? item.SubItems[1].Text : "";
                            if (animationSet != null && !string.Equals(set, animationSet, StringComparison.OrdinalIgnoreCase)) continue;
                            if (!sets.TryGetValue(item.Text, out List<string> inSets))
                            {
                                sets[item.Text] = inSets = new List<string>();
                                order.Add(item.Text);
                            }
                            inSets.Add(set);
                        }
                        if (animationSet != null && order.Count == 0 && !items.Item1.Any(o => o.SubItems.Count > 1 && string.Equals(o.SubItems[1].Text, animationSet, StringComparison.OrdinalIgnoreCase)))
                            throw new McpError("There is no animation set '" + animationSet + "' (list_enum_string_values ANIMATION_SET lists them).");
                        foreach (string animation in order)
                        {
                            if (!Matches(animation, null)) continue;
                            values.Add(animationSet != null ? (JToken)animation : new JObject() { ["value"] = animation, ["sets"] = new JArray(sets[animation].Distinct().Take(6)) });
                        }
                    }
                    else
                    {
                        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                        foreach (ListViewItem item in items.Item1)
                        {
                            if (!seen.Add(item.Text)) continue;
                            string description = items.Item2 && item.SubItems.Count > 1 ? item.SubItems[1].Text : null;
                            if (!Matches(item.Text, description)) continue;
                            values.Add(string.IsNullOrEmpty(description) ? (JToken)item.Text : new JObject() { ["value"] = item.Text, ["description"] = description });
                        }
                    }
                }

                JObject result = new JObject() { ["type"] = type.ToString(), ["count"] = values.Count };
                McpPaging.Page(call, values, result, "values", o => o, 200);
                if (values.Count == 0 && filter != null)
                {
                    //A misspelt or reworded value: the nearest few of the whole list
                    List<string> all = new List<string>();
                    if (type == EnumStringType.MATERIAL)
                        all.AddRange(content.Level.Materials.Entries.Where(o => o?.Name != null).Select(o => o.Name));
                    else
                        all.AddRange(EnumStringListViewItems.GetItems(type)?.Item1.Select(o => o.Text) ?? Enumerable.Empty<string>());
                    string near = McpNames.DidYouMean(all, filter, 8);
                    result["note"] = "No " + type + " value contains '" + filter + "'." + near;
                }
                return result;
            });
        }
        #endregion

        #region References
        private static object RemoveReferences(McpCall call)
        {
            List<string> kinds = call.Has("kinds") ? call.StrList("kinds").Select(o => o.Trim().ToLowerInvariant()).ToList() : _referenceKinds.ToList();
            foreach (string kind in kinds)
                if (!_referenceKinds.Contains(kind))
                    throw new McpError("'" + kind + "' is not a kind of reference: use " + string.Join(", ", _referenceKinds) + ".");
            if (kinds.Count == 0)
                throw new McpError("'kinds' is empty.");

            return McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                McpEditor.RequireUndoIdle();
                Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                McpBrowseTools.CompileIfShown(composite);
                Entity entity = McpScript.FindEntity(commands, composite, call.Str("entity", required: true));
                string entityName = McpScript.EntityName(commands, composite, entity);
                bool PointsAt(EntityPath path, Composite from) => path?.path != null && commands.Utils.GetResolvedTarget(commands.Utils.ResolveEntityPath(path.path, from)) == (composite, entity);

                List<(Composite composite, Entity entity)> pointers = new List<(Composite, Entity)>();
                List<(Composite composite, Entity owner, List<TriggerSequence.SequenceEntry> sequence, List<TriggerSequence.MethodEntry> methods)> sequences = new List<(Composite, Entity, List<TriggerSequence.SequenceEntry>, List<TriggerSequence.MethodEntry>)>();
                List<(Composite composite, CAGEAnimation animation)> animations = new List<(Composite, CAGEAnimation)>();
                foreach (Composite other in commands.Entries)
                {
                    if (other == null) continue;
                    if (kinds.Contains("aliases"))
                        foreach (AliasEntity alias in other.aliases)
                            if (commands.Utils.GetResolvedTarget(commands.Utils.ResolveAlias(alias, other)) == (composite, entity))
                                pointers.Add((other, alias));
                    if (kinds.Contains("proxies"))
                        foreach (ProxyEntity proxy in other.proxies)
                            if (commands.Utils.GetResolvedTarget(commands.Utils.ResolveProxy(proxy)) == (composite, entity))
                                pointers.Add((other, proxy));
                    if (kinds.Contains("trigger_sequences"))
                    {
                        foreach (TriggerSequence sequence in other.functions.OfType<TriggerSequence>())
                            if (sequence.sequence.Any(o => PointsAt(o?.connectedEntity, other)))
                                sequences.Add((other, sequence, sequence.sequence, sequence.methods));
                        //A proxy of a TriggerSequence carries a sequence of its own
                        foreach (ProxyEntity proxy in other.proxies)
                            if (proxy.sequence != null && proxy.sequence.Any(o => PointsAt(o?.connectedEntity, other)) && !(kinds.Contains("proxies") && pointers.Contains((other, proxy))))
                                sequences.Add((other, proxy, proxy.sequence, proxy.methods));
                    }
                    if (kinds.Contains("animations"))
                        foreach (CAGEAnimation animation in other.functions.OfType<CAGEAnimation>())
                            if (animation.connections.Any(o => PointsAt(o?.connectedEntity, other)))
                                animations.Add((other, animation));
                }

                JArray removed = new JArray();
                if (pointers.Count == 0 && sequences.Count == 0 && animations.Count == 0)
                    return new JObject() { ["removed"] = removed, ["note"] = "Nothing of those kinds points at " + entityName + "." };

                //An editor open on a sequence holds the lists the edit replaces: it is closed first, which records its own changes as a step
                foreach (TriggerSequenceEditor editor in Application.OpenForms.OfType<TriggerSequenceEditor>().ToList())
                    if (sequences.Any(o => o.owner == editor.Entity))
                        editor.Close();

                /* Deletions are one step per composite, as the References window makes them: undo brings one composite
                   on screen per step, and a deleted entity's nodes come back through its composite's live pages */
                int steps = 0;
                foreach (IGrouping<Composite, (Composite composite, Entity entity)> inComposite in pointers.GroupBy(o => o.composite))
                {
                    Composite other = inComposite.Key;
                    bool recorded = false;
                    using (UndoStack.Current.BeginGroup("AI: Remove references to " + entityName + " in " + McpScript.CompositeLeaf(other)))
                    {
                        foreach (Entity pointer in inComposite.Select(o => o.entity))
                        {
                            if (other.GetEntityByID(pointer.shortGUID) != pointer) continue;
                            removed.Add(new JObject() { ["kind"] = McpScript.Kind(pointer), ["composite"] = other.name, ["name"] = McpScript.EntityName(commands, other, pointer), ["id"] = McpScript.Id(pointer.shortGUID), ["removed"] = "deleted" });
                            UndoStack.Current.Apply(new EntityDeleteEdit(other, pointer, "Delete " + UndoLabels.Entity(other, pointer)));
                            recorded = true;
                        }
                    }
                    if (recorded) steps++;
                }

                //Sequence and animation entries are data only: every composite's in one step, as the References window does
                int listsBefore = removed.Count;
                using (UndoStack.Current.BeginGroup("AI: Remove references to " + entityName + (steps == 0 ? "" : " in sequences and animations")))
                {
                    foreach (var found in sequences)
                    {
                        Composite other = found.composite;
                        Entity owner = found.owner;
                        //Read now: the editor closed above may have replaced the lists
                        List<TriggerSequence.SequenceEntry> sequence = owner is TriggerSequence t ? t.sequence : ((ProxyEntity)owner).sequence;
                        List<TriggerSequence.MethodEntry> methods = owner is TriggerSequence t2 ? t2.methods : ((ProxyEntity)owner).methods;
                        List<TriggerSequence.SequenceEntry> kept = sequence.Where(o => !PointsAt(o?.connectedEntity, other)).ToList();
                        if (kept.Count == sequence.Count) continue;
                        removed.Add(new JObject() { ["kind"] = "trigger_sequence", ["composite"] = other.name, ["name"] = McpScript.EntityName(commands, other, owner), ["id"] = McpScript.Id(owner.shortGUID), ["removed"] = (sequence.Count - kept.Count) + " entries" });
                        UndoStack.Current.Apply(new TriggerSequenceEdit(other, owner,
                            TriggerSequenceEdit.CloneSequence(sequence), TriggerSequenceEdit.CloneMethods(methods),
                            TriggerSequenceEdit.CloneSequence(kept), TriggerSequenceEdit.CloneMethods(methods),
                            "Remove " + entityName + " from " + UndoLabels.Entity(other, owner)));
                    }
                    foreach ((Composite other, CAGEAnimation animation) in animations)
                    {
                        List<CAGEAnimation.Connection> kept = new List<CAGEAnimation.Connection>();
                        HashSet<ShortGuid> orphaned = new HashSet<ShortGuid>();
                        foreach (CAGEAnimation.Connection connection in animation.connections)
                        {
                            if (PointsAt(connection?.connectedEntity, other)) orphaned.Add(connection.target_track);
                            else kept.Add(connection);
                        }
                        if (kept.Count == animation.connections.Count) continue;
                        //The keyframes it was driven by go with it, unless another binding still plays them; event tracks stay (as the References window does)
                        foreach (CAGEAnimation.Connection connection in kept)
                            if (connection != null) orphaned.Remove(connection.target_track);
                        CageAnimationEdit.Lists before = CageAnimationEdit.Lists.Of(animation);
                        CageAnimationEdit.Lists after = new CageAnimationEdit.Lists()
                        {
                            Connections = kept,
                            EventTracks = animation.eventTracks,
                            FloatTracks = animation.floatTracks.Where(o => !orphaned.Contains(o.shortGUID)).ToList(),
                            Parameters = animation.parameters,
                        };
                        removed.Add(new JObject() { ["kind"] = "animation", ["composite"] = other.name, ["name"] = McpScript.EntityName(commands, other, animation), ["id"] = McpScript.Id(animation.shortGUID), ["removed"] = (animation.connections.Count - kept.Count) + " bindings, " + (animation.floatTracks.Count - after.FloatTracks.Count) + " tracks" });
                        UndoStack.Current.Apply(new CageAnimationEdit(other, animation, before, after, "Remove " + entityName + " from " + UndoLabels.Entity(other, animation)));
                    }
                }
                if (removed.Count != listsBefore) steps++;
                return new JObject() { ["entity"] = entityName, ["removed"] = removed, ["undo_steps"] = steps };
            });
        }
        #endregion
    }

    /// <summary>
    /// Where entities sit: walking down from a composite through the instances it places, composing each
    /// instance's transform on the way (<see cref="InstanceTransform.Compose"/>), with the position overrides
    /// aliases make on the way - the outermost alias wins, as instancing and De-instance take it. Each step also
    /// carries what the save pass reads to decide whether the game places it at all (<see cref="Step.Real"/>): a
    /// deleted subtree is dropped, an is_template one is a template (its collision ghosted), and an is_shared
    /// composite is instanced once, by its first placement - aliases setting those flags included.
    /// </summary>
    internal sealed class McpPlacements
    {
        internal struct Override
        {
            public Composite Owner;
            public ShortGuid[] Path;     //entity ids from the owner, without the terminator
            public cTransform Transform; //null for an alias that overrides only flags
            public AliasEntity Alias;
            public int Matched;          //how many steps of the path the walk has come down so far
        }

        /// <summary>A composite's own aliases that override what the walk reads, by the first entity of their path (in alias order).</summary>
        internal sealed class OverrideSet
        {
            public static readonly OverrideSet Empty = new OverrideSet();
            public readonly Dictionary<ShortGuid, List<Override>> ByFirst = new Dictionary<ShortGuid, List<Override>>();
        }

        /// <summary>Flags inherited from the instances above a step, as the save pass passes them down.</summary>
        internal struct Inherited
        {
            public bool Template, Deleted, SharedRepeat, DeleteStandard, DeleteBallistic;
        }

        private static readonly ShortGuid DeleteMe = ShortGuidUtils.Generate("delete_me");
        private static readonly ShortGuid IsTemplate = ShortGuidUtils.Generate("is_template");
        private static readonly ShortGuid IsShared = ShortGuidUtils.Generate("is_shared");
        private static readonly ShortGuid DeleteStandardId = ShortGuidUtils.Generate("delete_standard_collision");
        private static readonly ShortGuid DeleteBallisticId = ShortGuidUtils.Generate("delete_ballistic_collision");
        /// <summary>The bool parameters the save pass reads to place (or not place) content, which an alias can set on what it points at.</summary>
        internal static readonly ShortGuid[] FlagParameters =
        {
            ShortGuids.deleted, DeleteMe, IsTemplate, IsShared, DeleteStandardId, DeleteBallisticId,
            ShortGuidUtils.Generate("disable_collision"), ShortGuidUtils.Generate("enable_on_reset"),
        };

        private static readonly List<Override> None = new List<Override>();
        private readonly Commands _commands;
        private readonly Dictionary<Composite, OverrideSet> _overrides = new Dictionary<Composite, OverrideSet>();
        //is_shared composites a placement has claimed in this walk: a later placement of one is a repeat the game does not instance
        private HashSet<ShortGuid> _claimedShared = new HashSet<ShortGuid>();
        //Whether this walk takes is_shared repeats from the level's claims (a walk from the root that skips subtrees, or starts
        //part way down, cannot see which placement claims first) rather than claiming as it goes
        private bool _levelClaims;
        private HashSet<string> _repeats;
        //Set on the walk that works the level's claims out: the repeats it meets
        private HashSet<string> _recordRepeats;

        //The level's is_shared repeats (the chains from the root of the placements the save pass skips), per script version
        private static readonly object _sharedLock = new object();
        private static Commands _sharedFor;
        private static int _sharedVersion = -1;
        private static HashSet<string> _sharedRepeats;

        /// <summary>Forget the level's is_shared claims (the level is closing: its script must not stay reachable).</summary>
        internal static void DropShared()
        {
            lock (_sharedLock)
            {
                _sharedFor = null;
                _sharedVersion = -1;
                _sharedRepeats = null;
            }
        }

        /// <summary>How many entities a walk visits before it gives up (a level's root reaches over a million).</summary>
        public int Budget = 3000000;
        public int Visited { get; private set; }
        public bool Truncated { get; private set; }

        /// <summary>
        /// Place a function entity with no <c>position</c> of its own, whose type has one, at its composite's origin (the
        /// type's default, where the game puts it) rather than leaving it unplaced; <see cref="Step.PositionDefaulted"/> says so.
        /// Off by default: the zone planner was measured on the input without them.
        /// </summary>
        public bool PlaceUnpositioned;

        public McpPlacements(Commands commands)
        {
            _commands = commands;
        }

        /// <summary>An entity as the walk reaches it. The same object is reused from entity to entity: copy what you keep (<see cref="Placement.Of"/>).</summary>
        public sealed class Step
        {
            internal List<Override> Partials;
            internal OverrideSet Own;
            internal cTransform Origin;
            internal Inherited Above;
            internal bool SharedRepeatHere;
            internal bool PlaceUnpositioned;
            public Composite Composite { get; internal set; }
            public Entity Entity { get; internal set; }
            /// <summary>The instances stepped through from the start, then the entity itself.</summary>
            public List<Entity> Chain { get; internal set; }
            /// <summary>Set while the walk asks whether to descend into an instance: outer aliases reach inside it.</summary>
            public bool OverridesBelow { get; internal set; }

            private bool _resolved, _flagsResolved;
            private cTransform _local, _world;
            private AliasEntity _by;
            private Composite _byOwner;
            private bool _defaulted;
            private Inherited _flags;

            /// <summary>Its transform within its composite, an alias's override applied; null when it has none.</summary>
            public cTransform Local { get { Resolve(); return _local; } }
            /// <summary>Its transform from the start; null for an entity with no position (an instance with none sits at its composite's origin, and so, with <see cref="PlaceUnpositioned"/>, does a function whose type has a position).</summary>
            public cTransform World { get { Resolve(); return _world; } }
            public AliasEntity OverriddenBy { get { Resolve(); return _by; } }
            public Composite OverrideOwner { get { Resolve(); return _byOwner; } }
            /// <summary>A function with no position of its own, placed at its composite's origin (the type's default).</summary>
            public bool PositionDefaulted { get { Resolve(); return _defaulted; } }

            /// <summary>Under (or is) an instance marked is_template: a template the game does not place as such (its collision is ghosted).</summary>
            public bool Template { get { ResolveFlags(); return _flags.Template; } }
            /// <summary>Deleted (deleted or delete_me), itself or an instance above it: the game does not place it.</summary>
            public bool Deleted { get { ResolveFlags(); return _flags.Deleted; } }
            /// <summary>Under (or is) a repeat placement of an is_shared composite placed first elsewhere (in the level, for a walk from the root; else in this walk): the game instances a shared composite once.</summary>
            public bool SharedRepeat { get { ResolveFlags(); return _flags.SharedRepeat; } }
            /// <summary>delete_standard_collision on it or an instance above: its standard (world) collision is not built.</summary>
            public bool DeleteStandardCollision { get { ResolveFlags(); return _flags.DeleteStandard; } }
            /// <summary>delete_ballistic_collision on it or an instance above: its ballistic collision is not built.</summary>
            public bool DeleteBallisticCollision { get { ResolveFlags(); return _flags.DeleteBallistic; } }
            /// <summary>Placed by the game as the script stands: not deleted, not a template, not a shared repeat.</summary>
            public bool Real => !Deleted && !Template && !SharedRepeat;
            internal Inherited Flags { get { ResolveFlags(); return _flags; } }

            internal void Reset()
            {
                _resolved = false;
                _flagsResolved = false;
                SharedRepeatHere = false;
                OverridesBelow = false;
            }

            internal void RefreshFlags() => _flagsResolved = false;

            /// <summary>
            /// The value a bool parameter has for this placement: the outermost alias override that sets it, or the entity's
            /// own, or null when neither does (the type's default applies).
            /// </summary>
            public bool? Flag(ShortGuid parameter)
            {
                if (Entity == null) return null;
                ShortGuid id = Entity.shortGUID;
                foreach (Override o in Partials)
                {
                    if (o.Path.Length != o.Matched + 1 || o.Path[o.Matched] != id) continue;
                    bool? value = BoolOf(o.Alias, parameter);
                    if (value != null) return value;
                }
                if (Own.ByFirst.TryGetValue(id, out List<Override> own))
                {
                    foreach (Override o in own)
                    {
                        if (o.Path.Length != 1) continue;
                        bool? value = BoolOf(o.Alias, parameter);
                        if (value != null) return value;
                    }
                }
                return BoolOf(Entity, parameter);
            }

            private static bool? BoolOf(Entity entity, ShortGuid parameter) => (entity?.GetParameter(parameter)?.content as cBool)?.value;

            private void ResolveFlags()
            {
                if (_flagsResolved) return;
                _flagsResolved = true;
                Inherited flags = Above;
                if (Flag(ShortGuids.deleted) == true || Flag(DeleteMe) == true) flags.Deleted = true;
                if (Entity is FunctionEntity function && !function.function.IsFunctionType && Flag(IsTemplate) == true) flags.Template = true;
                if (Flag(DeleteStandardId) == true) flags.DeleteStandard = true;
                if (Flag(DeleteBallisticId) == true) flags.DeleteBallistic = true;
                if (SharedRepeatHere) flags.SharedRepeat = true;
                _flags = flags;
            }

            private void Resolve()
            {
                if (_resolved) return;
                _resolved = true;
                _by = null;
                _byOwner = null;
                _local = null;
                _world = null;
                _defaulted = false;
                //An alias or proxy stands for an entity elsewhere: it is not placed itself
                if (Entity is AliasEntity || Entity is ProxyEntity)
                    return;
                ShortGuid id = Entity.shortGUID;
                foreach (Override o in Partials)
                {
                    if (o.Transform == null || o.Path.Length != o.Matched + 1 || o.Path[o.Matched] != id) continue;
                    _local = o.Transform; _by = o.Alias; _byOwner = o.Owner;
                    break;
                }
                if (_by == null && Own.ByFirst.TryGetValue(id, out List<Override> own))
                {
                    foreach (Override o in own)
                    {
                        if (o.Transform == null || o.Path.Length != 1) continue;
                        _local = o.Transform; _by = o.Alias; _byOwner = o.Owner;
                        break;
                    }
                }
                if (_by == null)
                    _local = InstanceTransform.TransformOf(Entity);
                FunctionEntity entity = Entity as FunctionEntity;
                bool instance = entity != null && !entity.function.IsFunctionType;
                if (_local != null)
                    _world = InstanceTransform.Compose(Origin, _local);
                else if (instance || (PlaceUnpositioned && entity != null && UnityConnection.ViewerFunctionDrop.HasPosition(entity.function.AsFunctionType)))
                {
                    //The game places it at its composite's origin: the position parameter's default
                    _world = Origin == null ? new cTransform(Vector3.Zero, Vector3.Zero) : new cTransform(Origin.position, Origin.rotation);
                    _defaulted = !instance;
                }
            }
        }

        /// <summary>A placement kept from a walk.</summary>
        public sealed class Placement
        {
            public Composite Composite;
            public List<Entity> Chain;
            public cTransform World;
            public AliasEntity OverriddenBy;
            public Composite OverrideOwner;
            public bool PositionDefaulted;
            public bool Template, Deleted, SharedRepeat, DeleteStandardCollision, DeleteBallisticCollision;
            public bool Real => !Deleted && !Template && !SharedRepeat;

            public static Placement Of(Step step) => new Placement()
            {
                Composite = step.Composite,
                Chain = new List<Entity>(step.Chain),
                World = step.World,
                OverriddenBy = step.OverriddenBy,
                OverrideOwner = step.OverrideOwner,
                PositionDefaulted = step.PositionDefaulted,
                Template = step.Template,
                Deleted = step.Deleted,
                SharedRepeat = step.SharedRepeat,
                DeleteStandardCollision = step.DeleteStandardCollision,
                DeleteBallisticCollision = step.DeleteBallisticCollision,
            };
        }

        private OverrideSet OverridesIn(Composite composite)
        {
            if (_overrides.TryGetValue(composite, out OverrideSet set))
                return set;
            set = null;
            foreach (AliasEntity alias in composite.aliases)
            {
                ShortGuid[] path = alias.alias?.path;
                if (path == null) continue;
                cTransform transform = InstanceTransform.TransformOf(alias);
                if (transform == null && !FlagParameters.Any(o => alias.GetParameter(o)?.content is cBool)) continue;
                int length = path.Length;
                if (length > 0 && path[length - 1] == ShortGuid.Invalid) length--;
                if (length == 0) continue;
                if (set == null) set = new OverrideSet();
                if (!set.ByFirst.TryGetValue(path[0], out List<Override> list))
                    set.ByFirst[path[0]] = list = new List<Override>();
                list.Add(new Override() { Owner = composite, Path = path.Take(length).ToArray(), Transform = transform, Alias = alias });
            }
            _overrides[composite] = set ?? OverrideSet.Empty;
            return set ?? OverrideSet.Empty;
        }

        /// <summary>The overrides still in play one instance further down: those that continue through it, outermost first.</summary>
        private List<Override> Advance(List<Override> partials, OverrideSet own, Entity instance)
        {
            List<Override> next = null;
            foreach (Override o in partials)
            {
                if (o.Path.Length <= o.Matched + 1 || o.Path[o.Matched] != instance.shortGUID) continue;
                if (next == null) next = new List<Override>();
                Override advanced = o;
                advanced.Matched++;
                next.Add(advanced);
            }
            if (own.ByFirst.TryGetValue(instance.shortGUID, out List<Override> list))
            {
                foreach (Override o in list)
                {
                    if (o.Path.Length <= 1) continue;
                    if (next == null) next = new List<Override>();
                    Override started = o;
                    started.Matched = 1;
                    next.Add(started);
                }
            }
            return next ?? None;
        }

        /// <summary>
        /// Visit every entity in <paramref name="start"/> and in everything placed under it, depth first (a composite
        /// is not entered again inside itself). <paramref name="descend"/> picks the instanced composites to go into
        /// (null: all); <paramref name="visit"/> returns false to stop the walk.
        /// </summary>
        public void Walk(Composite start, Func<Step, bool> visit, Func<Composite, bool> descend = null, CancellationToken cancel = default(CancellationToken))
        {
            Visited = 0;
            Truncated = false;
            _claimedShared = new HashSet<ShortGuid>();
            //A whole walk from the root claims as the save pass does; one that skips subtrees takes the level's claims
            _levelClaims = descend != null && IsRoot(start);
            WalkIn(start, null, None, default(Inherited), new HashSet<Composite>(), new Step() { Chain = new List<Entity>(), PlaceUnpositioned = PlaceUnpositioned }, visit, descend, cancel);
        }

        private bool IsRoot(Composite start) => _recordRepeats == null && start != null && _commands.EntryPoints != null && _commands.EntryPoints.Length != 0 && start == _commands.EntryPoints[0];

        /// <summary>A chain's key in the level's claims: its entity ids from the root.</summary>
        private static string ChainKey(List<Entity> chain)
        {
            System.Text.StringBuilder key = new System.Text.StringBuilder(chain.Count * 9);
            foreach (Entity entity in chain) key.Append(entity.shortGUID.AsUInt32.ToString("X8")).Append('/');
            return key.ToString();
        }

        /// <summary>
        /// The placements (chains from the root) that are repeats of an is_shared composite the game already instanced
        /// elsewhere: one walk of the level in the save pass's order, through every composite that can hold an is_shared
        /// placement, cached until the script changes. UI thread.
        /// </summary>
        private HashSet<string> SharedRepeats()
        {
            if (_repeats != null) return _repeats;
            //Only cached while the version follows edits: start it following them, on the UI thread walks run on
            if (!McpCollision.Hooked)
            {
                CommandsEditor editor = Singleton.Editor;
                if (editor != null && !editor.IsDisposed && !editor.InvokeRequired) McpCollision.Hook();
            }
            bool cache = McpCollision.Hooked;
            int version = McpCollision.Version;
            if (cache)
            {
                lock (_sharedLock)
                {
                    if (_sharedRepeats != null && _sharedFor == _commands && _sharedVersion == version)
                        return _repeats = _sharedRepeats;
                }
            }
            HashSet<string> repeats = new HashSet<string>();
            McpPlacements claims = new McpPlacements(_commands) { Budget = Budget, _recordRepeats = repeats };
            Dictionary<Composite, bool> mayClaim = new Dictionary<Composite, bool>();
            Step current = null;
            //Subtrees that are deleted or repeats claim nothing (the save pass skips a repeat whole); nor do ones with no is_shared placement in them
            claims.Walk(_commands.EntryPoints[0], step => { current = step; return true; },
                child => current == null || (!current.Deleted && !current.SharedRepeat && (current.OverridesBelow || MayClaim(child, mayClaim, new HashSet<Composite>()))));
            if (cache)
            {
                lock (_sharedLock)
                {
                    _sharedRepeats = repeats;
                    _sharedFor = _commands;
                    _sharedVersion = version;
                }
            }
            return _repeats = repeats;
        }

        /// <summary>Whether a composite, or anything placed in it, has an instance that can be is_shared (its own flag, or an alias setting one).</summary>
        private bool MayClaim(Composite composite, Dictionary<Composite, bool> memo, HashSet<Composite> stack)
        {
            if (memo.TryGetValue(composite, out bool known)) return known;
            if (!stack.Add(composite)) return false;
            bool may = composite.aliases.Any(o => o.GetParameter(IsShared)?.content is cBool);
            foreach (FunctionEntity function in composite.functions)
            {
                if (may) break;
                if (function.function.IsFunctionType) continue;
                Composite child = _commands.GetComposite(function.function);
                if (child == null) continue;
                if ((function.GetParameter(IsShared)?.content as cBool)?.value == true || MayClaim(child, memo, stack)) may = true;
            }
            stack.Remove(composite);
            memo[composite] = may;
            return may;
        }

        /// <summary>
        /// Whether the instance <paramref name="step"/> stands on is a placement of an is_shared composite the game already
        /// instanced elsewhere, by the level's claims (the step's chain runs from the root).
        /// </summary>
        private bool RepeatByLevel(Step step, Composite child)
        {
            if (child == null || step.Flag(IsShared) != true || step.Deleted || step.SharedRepeat)
                return false;
            return SharedRepeats().Contains(ChainKey(step.Chain));
        }

        /// <summary>
        /// Visit the entity <paramref name="chain"/> leads to from <paramref name="start"/> and, when it is an instance,
        /// everything placed under it - with the transforms, alias overrides and flags of the steps above applied, so
        /// positions come out as a walk from <paramref name="start"/> gives them. Each step's Chain starts with
        /// <paramref name="chain"/>. An empty chain walks the whole of <paramref name="start"/>.
        /// </summary>
        public void WalkUnder(Composite start, IList<Entity> chain, Func<Step, bool> visit, Func<Composite, bool> descend = null, CancellationToken cancel = default(CancellationToken))
        {
            if (chain == null || chain.Count == 0)
            {
                Walk(start, visit, descend, cancel);
                return;
            }
            Visited = 0;
            Truncated = false;
            _claimedShared = new HashSet<ShortGuid>();
            //Under one placement from the root: what is a shared repeat comes from the level's claims, not this walk's
            _levelClaims = IsRoot(start);
            HashSet<Composite> onStack = new HashSet<Composite>();
            Step step = Position(start, chain, onStack);
            Visited = 1;
            if (!visit(step))
                return;
            Entity last = chain[chain.Count - 1];
            Composite child = McpScript.InstancedComposite(_commands, last);
            if (child == null || onStack.Contains(child))
                return;
            List<Override> below = Advance(step.Partials, step.Own, last);
            step.OverridesBelow = below.Count != 0;
            if (descend == null || descend(child))
                WalkIn(child, step.World, below, step.Flags, onStack, step, visit, descend, cancel);
        }

        private bool WalkIn(Composite composite, cTransform placement, List<Override> partials, Inherited above, HashSet<Composite> onStack, Step step, Func<Step, bool> visit, Func<Composite, bool> descend, CancellationToken cancel)
        {
            if (!onStack.Add(composite))
                return true;
            try
            {
                OverrideSet own = OverridesIn(composite);
                foreach (Entity entity in composite.GetEntities())
                {
                    if (++Visited > Budget)
                    {
                        Truncated = true;
                        return false;
                    }
                    if ((Visited & 1023) == 0)
                        cancel.ThrowIfCancellationRequested();

                    step.Chain.Add(entity);
                    step.Composite = composite;
                    step.Entity = entity;
                    step.Origin = placement;
                    step.Partials = partials;
                    step.Own = own;
                    step.Above = above;
                    step.Reset();
                    FunctionEntity function = entity as FunctionEntity;
                    Composite child = function != null && !function.function.IsFunctionType ? _commands.GetComposite(function.function) : null;
                    //The save pass instances an is_shared composite once: its first placement (that is not a template) claims it.
                    //Nothing under a repeat claims: the save pass does not go into one
                    if (_levelClaims)
                    {
                        if (RepeatByLevel(step, child))
                        {
                            step.SharedRepeatHere = true;
                            step.RefreshFlags();
                        }
                    }
                    else if (child != null && step.Flag(IsShared) == true && !step.Deleted && !step.SharedRepeat)
                    {
                        if (_claimedShared.Contains(child.shortGUID))
                        {
                            step.SharedRepeatHere = true;
                            step.RefreshFlags();
                            _recordRepeats?.Add(ChainKey(step.Chain));
                        }
                        else if (!step.Template)
                            _claimedShared.Add(child.shortGUID);
                    }
                    bool keepGoing = visit(step);
                    if (keepGoing && child != null)
                    {
                        List<Override> below = Advance(partials, own, entity);
                        step.OverridesBelow = below.Count != 0;
                        if (descend == null || descend(child))
                        {
                            cTransform childPlacement = step.World;
                            keepGoing = WalkIn(child, childPlacement, below, step.Flags, onStack, step, visit, descend, cancel);
                        }
                    }
                    step.Chain.RemoveAt(step.Chain.Count - 1);
                    if (!keepGoing)
                        return false;
                }
                return true;
            }
            finally
            {
                onStack.Remove(composite);
            }
        }

        /// <summary>One placement: <paramref name="chain"/> is the entities stepped through from <paramref name="start"/>, each an instance but the last.</summary>
        public Placement Evaluate(Composite start, IList<Entity> chain)
        {
            return Placement.Of(Position(start, chain, new HashSet<Composite>()));
        }

        /// <summary>A step standing on the last entity of <paramref name="chain"/>, with everything above it applied; <paramref name="composites"/> gets the composites stepped through.</summary>
        private Step Position(Composite start, IList<Entity> chain, HashSet<Composite> composites)
        {
            Step step = new Step() { Chain = new List<Entity>(), PlaceUnpositioned = PlaceUnpositioned };
            cTransform placement = null;
            List<Override> partials = None;
            Inherited above = default(Inherited);
            Composite composite = start;
            bool levelClaims = IsRoot(start);
            for (int i = 0; i < chain.Count; i++)
            {
                composites.Add(composite);
                OverrideSet own = OverridesIn(composite);
                step.Chain.Add(chain[i]);
                step.Composite = composite;
                step.Entity = chain[i];
                step.Origin = placement;
                step.Partials = partials;
                step.Own = own;
                step.Above = above;
                step.Reset();
                //A placement of an is_shared composite the game instanced elsewhere first: a repeat, and so is everything under it
                FunctionEntity function = chain[i] as FunctionEntity;
                if (levelClaims && RepeatByLevel(step, function != null && !function.function.IsFunctionType ? _commands.GetComposite(function.function) : null))
                {
                    step.SharedRepeatHere = true;
                    step.RefreshFlags();
                }
                if (i == chain.Count - 1)
                    break;
                placement = step.World;
                above = step.Flags;
                partials = Advance(partials, own, chain[i]);
                composite = McpScript.InstancedComposite(_commands, chain[i]);
                if (composite == null)
                    throw new McpError(McpScript.Id(chain[i].shortGUID) + " is not a composite instance, so the path cannot go through it.");
            }
            return step;
        }

        /// <summary>The composites that can reach <paramref name="target"/> through instances, and it.</summary>
        public HashSet<Composite> Reaching(Composite target)
        {
            Dictionary<ShortGuid, List<Composite>> parents = new Dictionary<ShortGuid, List<Composite>>();
            foreach (Composite composite in _commands.Entries)
            {
                if (composite == null) continue;
                foreach (FunctionEntity function in composite.functions)
                {
                    if (function.function.IsFunctionType) continue;
                    if (!parents.TryGetValue(function.function, out List<Composite> list))
                        parents[function.function] = list = new List<Composite>();
                    if (!list.Contains(composite)) list.Add(composite);
                }
            }
            HashSet<Composite> reach = new HashSet<Composite>() { target };
            Queue<Composite> pending = new Queue<Composite>();
            pending.Enqueue(target);
            while (pending.Count != 0)
            {
                Composite current = pending.Dequeue();
                if (!parents.TryGetValue(current.shortGUID, out List<Composite> list)) continue;
                foreach (Composite parent in list)
                    if (reach.Add(parent)) pending.Enqueue(parent);
            }
            return reach;
        }

        /// <summary>
        /// Every placement under <paramref name="start"/> of <paramref name="entity"/> in <paramref name="composite"/>
        /// (or, with no entity, of the composite itself: each instance of it). Keeps up to <paramref name="limit"/>; returns how many there are.
        /// With <paramref name="realOnly"/>, only the placements the game really makes (<see cref="Step.Real"/>) count; <see cref="NotReal"/> says how many others there were.
        /// </summary>
        public int PlacementsOf(Composite start, Composite composite, Entity entity, List<Placement> found, int limit, CancellationToken cancel, bool realOnly = false)
        {
            NotReal = 0;
            if (entity == null && composite == start)
            {
                found.Add(new Placement() { Composite = start, Chain = new List<Entity>(), World = new cTransform(Vector3.Zero, Vector3.Zero) });
                return 1;
            }
            HashSet<Composite> reach = Reaching(composite);
            if (!reach.Contains(start))
                return 0;
            int total = 0;
            Walk(start, step =>
            {
                bool hit = entity != null
                    ? step.Composite == composite && step.Entity == entity
                    : step.Entity is FunctionEntity function && function.function == composite.shortGUID;
                if (hit && realOnly && !step.Real)
                    NotReal++;
                else if (hit)
                {
                    total++;
                    if (found.Count < limit) found.Add(Placement.Of(step));
                }
                return true;
            }, child => reach.Contains(child) && (entity != null || child != composite), cancel);
            return total;
        }

        /// <summary>How many placements the last <see cref="PlacementsOf"/> with realOnly left out (deleted, templates, shared repeats).</summary>
        public int NotReal { get; private set; }
    }
}

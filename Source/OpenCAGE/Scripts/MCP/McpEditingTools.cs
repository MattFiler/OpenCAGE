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
                Description = "Copy entities with their parameters, resources and the links between them: duplicate them in place (leave out 'into'), or copy them into another composite, as Ctrl+C / Ctrl+V do. Copies get new ids and '_N' names; an alias copies what it points at, placed where it sits. 'offset' moves the copies. A PhysicsSystem or EnvironmentModelReference is refused where the composite already has one (a composite can only have one). One undo step; returns the new ids.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite the entities are in (path or id; 'root' for the level's root).", required: true),
                    McpSchema.Strings("entities", "The entities to copy (ids or names).", required: true),
                    McpSchema.String("into", "Copy into this composite (path or id). Leave out to duplicate in place."),
                    McpSchema.Vector("offset", "[x, y, z] metres added to each copy's position."),
                    McpSchema.String("links", "'internal' (default): keep the links between the copied entities; 'none': copy no links.", options: new[] { "internal", "none" }),
                    McpSchema.Boolean("resolve_aliases", "An alias copies the entity it points at, placed where it sits (default true); false copies the alias itself."),
                    McpSchema.String("page", "The flowgraph page for copied links that cannot go beside what they join.")),
                Run = CopyEntities,
            };

            yield return new McpTool()
            {
                Name = "retarget_proxy",
                Title = "Re-point proxy",
                Description = "Point a proxy (a dead one left by port_composites, say) at another function entity or composite instance, keeping its id, links and nodes - the inspector's Change Target. The path runs from the level's root composite. One undo step.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite the proxy is in (path or id).", required: true),
                    McpSchema.String("proxy", "The proxy (id or name).", required: true),
                    McpSchema.Strings("path", "Entity ids/names from the root composite, through instances, to the new target, e.g. [\"Door_Area\", \"Door_1\"].", required: true)),
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
                    McpSchema.Integer("limit", "At most this many (default 100).")),
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
                Description = "Where a composite or entity sits in the level: each instance path from the root composite down to it, with its world position and rotation (instance transforms composed, alias position overrides applied), and optionally the zones that claim it. Give 'path' to evaluate one placement.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite (path or id) - or the one the entity is in."),
                    McpSchema.String("entity", "An entity of that composite (id or name). Leave out for the composite's own placements."),
                    McpSchema.Strings("path", "Instead: one placement, as entity ids/names from the root composite through instances to the entity."),
                    McpSchema.Boolean("zones", "Also list the Zone entities that claim each placement."),
                    McpSchema.Integer("limit", "At most this many placements (default 50).")),
                ReadOnly = true,
                Idempotent = true,
                Run = GetPlacements,
            };

            yield return new McpTool()
            {
                Name = "get_zones",
                Title = "Get zones",
                Description = "The level's Zone entities (which stream and control parts of the level) and the instance paths each claims, as the viewport's zone overlay works them out. With 'path', only the zones that claim that placement.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("filter", "Only zones whose name contains this."),
                    McpSchema.Strings("path", "A placement: entity ids/names from the root composite through instances. Lists the zones claiming it."),
                    McpSchema.Integer("limit", "At most this many zones (default 100)."),
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
                    McpSchema.Integer("limit", "At most this many (default 200).")),
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
                throw new McpError("'entities' is empty.");
            string linkMode = (call.Str("links") ?? "internal").Trim().ToLowerInvariant();
            if (linkMode != "internal" && linkMode != "none")
                throw new McpError("'links' is 'internal' (keep the links between the copies) or 'none'.");
            bool resolveAliases = call.Bool("resolve_aliases", true);
            Vector3? offset = call.Has("offset") ? McpValues.ReadVector(call.Token("offset"), "offset", null) : (Vector3?)null;

            Composite source = null, destination = null;
            McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                source = McpScript.FindComposite(commands, call.Str("composite", required: true));
                destination = call.Has("into") ? McpScript.FindComposite(commands, call.Str("into")) : source;
            });
            bool inPlace = destination == source;

            JArray made = new JArray();
            List<string> skipped = new List<string>();
            int linksCopied = 0;
            string label = inPlace
                ? "Duplicate " + (wanted.Count == 1 ? wanted[0] : wanted.Count + " entities")
                : "Copy " + (wanted.Count == 1 ? wanted[0] : wanted.Count + " entities") + " to " + McpScript.CompositeLeaf(destination);

            McpScriptEdit.Run(label, destination, edit =>
            {
                Commands commands = edit.Commands;
                edit.UsePage(destination, call.Str("page"));
                List<Entity> sources = wanted.Select(o => McpScript.FindEntity(commands, source, o)).Distinct().ToList();

                //Each entity copied, keyed by what was actually copied (an alias's target, when resolved): two entries naming it share one copy
                Dictionary<Entity, Entity> copies = new Dictionary<Entity, Entity>();
                Dictionary<Entity, Entity> copyOfListed = new Dictionary<Entity, Entity>();
                edit.TouchContents(destination);
                edit.Tx.TouchPins(destination);
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
                    else if (offset != null)
                        skipped.Add(McpScript.EntityName(commands, destination, copy) + " has no position, so the offset did not move it");
                }

                //The links that ran between the copied entities, onto the copies
                if (linkMode == "internal")
                {
                    foreach (Entity listed in sources)
                    {
                        if (!copies.TryGetValue(listed, out Entity copy))
                            continue; //an alias resolved to something elsewhere: its links were the alias's
                        foreach (EntityConnector link in listed.childLinks)
                        {
                            Entity linkedSource = source.GetEntityByID(link.linkedEntityID);
                            if (linkedSource == null || !copies.TryGetValue(linkedSource, out Entity linkedCopy))
                                continue;
                            try
                            {
                                if (edit.AddLink(destination, copy, McpScript.ParamName(link.thisParamID), linkedCopy, McpScript.ParamName(link.linkedParamID), allowCustom: true))
                                    linksCopied++;
                            }
                            catch (McpError e)
                            {
                                skipped.Add("link " + McpScript.EntityName(commands, destination, copy) + "." + McpScript.ParamName(link.thisParamID) + " -> " + McpScript.EntityName(commands, destination, linkedCopy) + "." + McpScript.ParamName(link.linkedParamID) + " not copied: " + e.Message);
                            }
                        }
                    }
                }

                foreach (Entity listed in sources)
                {
                    if (!copyOfListed.TryGetValue(listed, out Entity copy))
                        continue;
                    JObject brief = McpScript.Brief(commands, destination, copy);
                    brief["copy_of"] = McpScript.EntityName(commands, source, listed) + " (" + McpScript.Id(listed.shortGUID) + ")";
                    made.Add(brief);
                }
            });

            JObject result = new JObject()
            {
                ["composite"] = destination.name,
                ["copies"] = made,
                ["links_copied"] = linksCopied,
            };
            if (skipped.Count != 0) result["not_copied"] = new JArray(skipped);
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
                    throw new McpError(McpScript.EntityName(commands, composite, found) + " is a " + McpScript.Kind(found) + ", not a proxy.");

                Composite root = commands.EntryPoints[0];
                List<string> steps = McpScriptTools.ReadPath(call.Token("path"));
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
                int limit = Math.Max(1, call.Int("limit", 100));

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
                        if (proxies.Count >= limit) continue;
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
                return new JObject()
                {
                    ["count"] = total,
                    ["composites"] = composites,
                    ["proxies"] = proxies,
                    ["hint"] = total == 0 ? null : "retarget_proxy re-points one; get_placements and find_entities (within 'root') give paths from the root.",
                };
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
            int limit = Math.Max(1, call.Int("limit", 50));
            bool withZones = call.Bool("zones");
            using (McpEditorTools.Heartbeat(call, "Finding placements"))
            return McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: false);
                Commands commands = content.Level.Commands;
                Composite root = commands.EntryPoints[0];
                McpPlacements walker = new McpPlacements(commands);
                List<McpPlacements.Placement> found = new List<McpPlacements.Placement>();
                int total;
                JObject result = new JObject();

                if (call.Has("path"))
                {
                    if (call.Has("composite") || call.Has("entity"))
                        throw new McpError("Give 'path', or 'composite' (and 'entity'), not both.");
                    List<Entity> chain = ResolveChain(commands, root, McpScriptTools.ReadPath(call.Token("path")));
                    found.Add(walker.Evaluate(root, chain));
                    total = 1;
                }
                else
                {
                    Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                    Entity entity = call.Has("entity") ? McpScript.FindEntity(commands, composite, call.Str("entity")) : null;
                    result["composite"] = composite.name;
                    if (entity != null) result["entity"] = McpScript.Brief(commands, composite, entity);
                    total = walker.PlacementsOf(root, composite, entity, found, limit, call.Cancel);
                    if (total == 0)
                        result["note"] = composite == root && entity == null ? null : "Not placed in the level: nothing reachable from the root composite instances " + composite.name + " (find_references lists its instances).";
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
                result["placements"] = placements;
                return result;
            });
        }

        private static object GetZones(McpCall call)
        {
            string filter = call.Str("filter");
            int limit = Math.Max(1, call.Int("limit", 100));
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
                    List<Entity> chain = ResolveChain(commands, root, McpScriptTools.ReadPath(call.Token("path")));
                    result["path"] = DescribePlacement(commands, root, new McpPlacements(commands).Evaluate(root, chain));
                    zones = ZonesClaiming(commands, zones, chain);
                    if (zones.Count == 0)
                        result["note"] = "No zone claims this placement: it is in no zone's trigger sequences, nor inside anything that is.";
                }
                List<SyncedZone> shown = zones.Where(o => filter == null || (o.name ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                result["count"] = shown.Count;
                result["zones"] = new JArray(shown.Take(limit).Select(o => DescribeZone(commands, root, o, call.Has("path") ? 0 : rootsLimit)));
                return result;
            });
        }

        /// <summary>An entity path from the root composite, read from names or ids, as the entities stepped through.</summary>
        private static List<Entity> ResolveChain(Commands commands, Composite root, List<string> steps)
        {
            McpScript.ResolvePath(commands, root, steps, out Composite _, out Entity _);
            List<Entity> chain = new List<Entity>();
            Composite current = root;
            foreach (string step in steps)
            {
                Entity entity = McpScript.FindEntity(commands, current, step);
                chain.Add(entity);
                current = McpScript.InstancedComposite(commands, entity);
            }
            return chain;
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

        private static JObject DescribeZone(Commands commands, Composite root, SyncedZone zone, int rootsLimit)
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

        /// <summary>A placement for a result: the path as names and as ids, and where it sits.</summary>
        public static JObject DescribePlacement(Commands commands, Composite start, McpPlacements.Placement placement)
        {
            JArray names = new JArray();
            JArray ids = new JArray();
            Composite current = start;
            foreach (Entity entity in placement.Chain)
            {
                names.Add(current == null ? McpScript.Id(entity.shortGUID) : McpScript.EntityName(commands, current, entity));
                ids.Add(McpScript.Id(entity.shortGUID));
                current = McpScript.InstancedComposite(commands, entity);
            }
            JObject described = new JObject() { ["path"] = names, ["ids"] = ids };
            if (placement.World != null)
            {
                described["position"] = McpValues.Vector(placement.World.position);
                described["rotation"] = McpValues.Vector(placement.World.rotation);
            }
            if (placement.OverriddenBy != null)
                described["position_from"] = "alias " + McpScript.EntityName(commands, placement.OverrideOwner, placement.OverriddenBy) + " (" + McpScript.Id(placement.OverriddenBy.shortGUID) + ") in " + placement.OverrideOwner.name;
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
            int limit = Math.Max(1, call.Int("limit", 200));
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

                return new JObject()
                {
                    ["type"] = type.ToString(),
                    ["count"] = values.Count,
                    ["values"] = new JArray(values.Take(limit)),
                    ["note"] = values.Count > limit ? "Showing " + limit + " of " + values.Count + ": narrow with 'filter' or raise 'limit'." : null,
                };
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
    /// aliases make on the way - the outermost alias wins, as instancing and De-instance take it.
    /// </summary>
    internal sealed class McpPlacements
    {
        internal struct Override
        {
            public Composite Owner;
            public ShortGuid[] Path;     //entity ids from the owner, without the terminator
            public cTransform Transform;
            public AliasEntity Alias;
            public int Matched;          //how many steps of the path the walk has come down so far
        }

        private static readonly List<Override> None = new List<Override>();
        private readonly Commands _commands;
        private readonly Dictionary<Composite, List<Override>> _overrides = new Dictionary<Composite, List<Override>>();

        /// <summary>How many entities a walk visits before it gives up (a level's root reaches over a million).</summary>
        public int Budget = 3000000;
        public int Visited { get; private set; }
        public bool Truncated { get; private set; }

        public McpPlacements(Commands commands)
        {
            _commands = commands;
        }

        /// <summary>An entity as the walk reaches it. The same object is reused from entity to entity: copy what you keep (<see cref="Placement.Of"/>).</summary>
        public sealed class Step
        {
            internal List<Override> Partials;
            internal List<Override> Own;
            internal cTransform Origin;
            public Composite Composite { get; internal set; }
            public Entity Entity { get; internal set; }
            /// <summary>The instances stepped through from the start, then the entity itself.</summary>
            public List<Entity> Chain { get; internal set; }

            private bool _resolved;
            private cTransform _local, _world;
            private AliasEntity _by;
            private Composite _byOwner;

            /// <summary>Its transform within its composite, an alias's override applied; null when it has none.</summary>
            public cTransform Local { get { Resolve(); return _local; } }
            /// <summary>Its transform from the start; null for an entity with no position (an instance with none sits at its composite's origin).</summary>
            public cTransform World { get { Resolve(); return _world; } }
            public AliasEntity OverriddenBy { get { Resolve(); return _by; } }
            public Composite OverrideOwner { get { Resolve(); return _byOwner; } }

            internal void Reset() => _resolved = false;

            private void Resolve()
            {
                if (_resolved) return;
                _resolved = true;
                _by = null;
                _byOwner = null;
                _local = null;
                _world = null;
                //An alias or proxy stands for an entity elsewhere: it is not placed itself
                if (Entity is AliasEntity || Entity is ProxyEntity)
                    return;
                foreach (Override o in Partials)
                {
                    if (o.Path.Length != o.Matched + 1 || o.Path[o.Matched] != Entity.shortGUID) continue;
                    _local = o.Transform; _by = o.Alias; _byOwner = o.Owner;
                    break;
                }
                if (_by == null)
                {
                    foreach (Override o in Own)
                    {
                        if (o.Path.Length != 1 || o.Path[0] != Entity.shortGUID) continue;
                        _local = o.Transform; _by = o.Alias; _byOwner = o.Owner;
                        break;
                    }
                }
                if (_by == null)
                    _local = InstanceTransform.TransformOf(Entity);
                bool instance = Entity is FunctionEntity function && !function.function.IsFunctionType;
                if (_local != null)
                    _world = InstanceTransform.Compose(Origin, _local);
                else if (instance)
                    _world = Origin == null ? new cTransform(Vector3.Zero, Vector3.Zero) : new cTransform(Origin.position, Origin.rotation);
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

            public static Placement Of(Step step) => new Placement()
            {
                Composite = step.Composite,
                Chain = new List<Entity>(step.Chain),
                World = step.World,
                OverriddenBy = step.OverriddenBy,
                OverrideOwner = step.OverrideOwner,
            };
        }

        private List<Override> OverridesIn(Composite composite)
        {
            if (_overrides.TryGetValue(composite, out List<Override> list))
                return list;
            list = null;
            foreach (AliasEntity alias in composite.aliases)
            {
                ShortGuid[] path = alias.alias?.path;
                cTransform transform = path == null ? null : InstanceTransform.TransformOf(alias);
                if (transform == null) continue;
                int length = path.Length;
                if (length > 0 && path[length - 1] == ShortGuid.Invalid) length--;
                if (length == 0) continue;
                if (list == null) list = new List<Override>();
                list.Add(new Override() { Owner = composite, Path = path.Take(length).ToArray(), Transform = transform, Alias = alias });
            }
            _overrides[composite] = list ?? None;
            return list ?? None;
        }

        /// <summary>The overrides still in play one instance further down: those that continue through it, outermost first.</summary>
        private List<Override> Advance(List<Override> partials, List<Override> own, Entity instance)
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
            foreach (Override o in own)
            {
                if (o.Path.Length <= 1 || o.Path[0] != instance.shortGUID) continue;
                if (next == null) next = new List<Override>();
                Override started = o;
                started.Matched = 1;
                next.Add(started);
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
            WalkIn(start, null, None, new HashSet<Composite>(), new Step() { Chain = new List<Entity>() }, visit, descend, cancel);
        }

        private bool WalkIn(Composite composite, cTransform placement, List<Override> partials, HashSet<Composite> onStack, Step step, Func<Step, bool> visit, Func<Composite, bool> descend, CancellationToken cancel)
        {
            if (!onStack.Add(composite))
                return true;
            try
            {
                List<Override> own = OverridesIn(composite);
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
                    step.Reset();
                    bool keepGoing = visit(step);
                    if (keepGoing && entity is FunctionEntity function && !function.function.IsFunctionType)
                    {
                        Composite child = _commands.GetComposite(function.function);
                        if (child != null && (descend == null || descend(child)))
                        {
                            cTransform childPlacement = step.World;
                            keepGoing = WalkIn(child, childPlacement, Advance(partials, own, entity), onStack, step, visit, descend, cancel);
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
            Step step = new Step() { Chain = new List<Entity>() };
            cTransform placement = null;
            List<Override> partials = None;
            Composite composite = start;
            for (int i = 0; i < chain.Count; i++)
            {
                List<Override> own = OverridesIn(composite);
                step.Chain.Add(chain[i]);
                step.Composite = composite;
                step.Entity = chain[i];
                step.Origin = placement;
                step.Partials = partials;
                step.Own = own;
                step.Reset();
                if (i == chain.Count - 1)
                    break;
                placement = step.World;
                partials = Advance(partials, own, chain[i]);
                composite = McpScript.InstancedComposite(_commands, chain[i]);
                if (composite == null)
                    throw new McpError(McpScript.Id(chain[i].shortGUID) + " is not a composite instance, so the path cannot go through it.");
            }
            return Placement.Of(step);
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
        /// </summary>
        public int PlacementsOf(Composite start, Composite composite, Entity entity, List<Placement> found, int limit, CancellationToken cancel)
        {
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
                if (hit)
                {
                    total++;
                    if (found.Count < limit) found.Add(Placement.Of(step));
                }
                return true;
            }, child => reach.Contains(child) && (entity != null || child != composite), cancel);
            return total;
        }
    }
}

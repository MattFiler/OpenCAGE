using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.MCP;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.MapAssets
{
    /// <summary>What a level's map is made with: null / 0 means found or chosen from the level.</summary>
    public class MapAssetsOptions
    {
        public List<MapFloor> Floors;
        public float Scale;
        public string MapName;
    }

    /// <summary>A map item's floor: the entity whose map_keyframe says which floor the icon shows on, and where the item is.</summary>
    public class MapItemFloor
    {
        public Composite Composite;
        public Entity Entity;
        public string Name;
        public string Was, Keyframe;
        public Vector3 World;
        /// <summary>Instances on more than one floor share this setting: each placement gets its own (an alias in the root composite).</summary>
        public bool Mixed;
        public List<MapItemPlacement> Placements = new List<MapItemPlacement>();

        /// <summary>Some placement shows on a floor other than the one it stands on.</summary>
        public bool Wrong => Placements.Any(o => o.Keyframe != null && o.Keyframe != o.Was);
    }

    /// <summary>One placement of a map item: the ids from the root composite to the entity that sets it, and its floor.</summary>
    public class MapItemPlacement
    {
        public ShortGuid[] Path;
        /// <summary>The root composite's alias that overrides map_keyframe for this placement, if any.</summary>
        public AliasEntity Alias;
        public string Was, Keyframe;
        public Vector3 World;
    }

    /// <summary>A level's map, worked out and not yet written.</summary>
    public class MapAssetsPlan
    {
        public string Level;
        public List<MapPolygon> Walkable = new List<MapPolygon>();
        public int NavPolygons;
        public List<MapFloor> Floors = new List<MapFloor>();
        public MapAnchorSettings Anchor;
        public List<MapPlanFloor> Drawn = new List<MapPlanFloor>();
        public List<MapItemFloor> Items = new List<MapItemFloor>();
        /// <summary>Files for the level's UI folder: path in it ("HUB.GFX", "FILES/HUB_MAP.GFX") -> content.</summary>
        public Dictionary<string, byte[]> Files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        public List<string> Notes = new List<string>();
    }

    /// <summary>
    /// Generate Map Assets: a level's Tab map from its walk mesh. The floors are found from where the walkable area is (or given),
    /// each drawn as the game's maps are; the level's own hub and map page (the game's, with the map page loaded from the level's
    /// files and its names) and the floor movies go in its UI folder (<see cref="LevelUiOverrides"/> puts them in at launch); a
    /// MapAnchor, floor boxes per floor and every map item's floor are set in the level's script as one undo step.
    /// </summary>
    public static class MapAssetGenerator
    {
        /// <summary>Names of what the generator places in the root composite (it replaces its own on the next run).</summary>
        public const string Prefix = "MapAssets_";
        public const string Page = "Map";
        /// <summary>Where the level's hub loads its map page from, and where the page loads floors from (in UI.PAK, from DATA/UI).</summary>
        public const string PageUrl = "/DATA/UI/LV/HUB_MAP.GFX", FloorFolder = "/DATA/UI/LV/";
        public const string PageFile = "HUB_MAP.GFX";
        /// <summary>The game's floor movie the floors are made from (its class and start marker are kept).</summary>
        public const string TemplateFloor = "DATA/UI/MAPS/BSP_TORRENS_1.GFX";
        public const string GamePage = "DATA/UI/HUB_MAP.GFX";
        /// <summary>The font libraries the floors and the page import (they are not beside them in UI.PAK).</summary>
        public const string FontLibrary = "/DATA/UI/MAPS/GFXFONTLIB.GFX", PageFontLibrary = "/DATA/UI/GFXFONTLIB.GFX";

        #region Working it out
        /// <summary>The level's map as it would be made: nothing is written. Throws <see cref="McpError"/> when it can't be made.</summary>
        public static MapAssetsPlan Propose(LevelContent content, MapAssetsOptions options)
        {
            options = options ?? new MapAssetsOptions();
            Level level = content?.Level ?? throw new McpError("No level is open.");
            NavigationMesh nav = level.StateResources.Count > 0 ? level.StateResources[0]?.NavMesh : null;
            if (nav == null || nav.Polygons == null || nav.Polygons.Length == 0)
                throw new McpError("The level has no walk mesh yet: Save & Build it with the navmesh baker first (the map is drawn from where you can walk).");
            MapAssetsPlan plan = new MapAssetsPlan() { Level = level.Name, NavPolygons = nav.Polygons.Length };

            //Every polygon, its links (the polygon references' low bits are the index), the walkable area = the biggest connected part
            int count = nav.Polygons.Length, bits = 1;
            while ((1 << bits) < count) bits++;
            uint mask = (1u << bits) - 1;
            MapPolygon[] all = new MapPolygon[count];
            List<int>[] links = new List<int>[count];
            for (int i = 0; i < count; i++)
            {
                NavigationMesh.dtPoly p = nav.Polygons[i];
                PointF[] outline = new PointF[p.vertCount];
                float y = 0;
                for (int k = 0; k < p.vertCount; k++) { Vector3 v = nav.Vertices[p.verts[k]]; outline[k] = new PointF(v.X, v.Z); y += v.Y; }
                all[i] = p.vertCount > 0 ? new MapPolygon(outline, y / p.vertCount) : null;
                links[i] = new List<int>();
                for (int l = p.firstLink, guard = 0; l >= 0 && nav.Links != null && l < nav.Links.Length && guard < 64; l = nav.Links[l].next, guard++)
                    links[i].Add((int)(nav.Links[l].polygonRef & mask));
            }
            bool[] best = null; float bestArea = -1;
            bool[] done = new bool[count];
            for (int i = 0; i < count; i++)
            {
                if (done[i] || all[i] == null || all[i].Outline.Length < 3) continue;
                bool[] part = MapFloors.Reachable(count, k => links[k], new[] { i });
                float area = 0;
                for (int k = 0; k < count; k++) if (part[k]) { done[k] = true; if (all[k] != null && all[k].Outline.Length >= 3) area += MapFloors.AreaOf(all[k]); }
                if (area > bestArea) { bestArea = area; best = part; }
            }
            for (int i = 0; i < count; i++) if (best != null && best[i] && all[i] != null && all[i].Outline.Length >= 3) plan.Walkable.Add(all[i]);
            if (plan.Walkable.Count == 0)
                throw new McpError("The walk mesh has no walkable area.");

            //Floors: as given, or found; the game takes six at most
            plan.Floors = options.Floors != null && options.Floors.Count != 0 ? options.Floors.OrderBy(o => o.Level).ToList() : MapFloors.Find(plan.Walkable);
            if (plan.Floors.Count > MapPlanner.MaxFloors)
                throw new McpError("The map has " + plan.Floors.Count + " floors; the game's map takes " + MapPlanner.MaxFloors + " at most. Join some (raise their heights to meet).");
            for (int i = 0; i < plan.Floors.Count; i++)
            {
                MapFloor f = plan.Floors[i];
                f.Keyframe = MapPlanner.BorrowedKeyframes[i];
                if (string.IsNullOrWhiteSpace(f.Name)) f.Name = plan.Floors.Count == 1 ? "MAP" : "LEVEL " + (i + 1);
                f.From = i == 0 ? float.NegativeInfinity : f.From;
                f.To = i == plan.Floors.Count - 1 ? float.PositiveInfinity : f.To;
                f.Area = plan.Walkable.Where(p => MapPlanner.FloorOf(plan.Floors, p.Height) == i).Sum(p => MapFloors.AreaOf(p));
            }
            plan.Notes.Add("Borrows the game's map keyframe" + (plan.Floors.Count == 1 ? " " : "s ") + string.Join(", ", plan.Floors.Select(o => o.Keyframe)) +
                ": the game only opens a map for keyframes on its own list, so the level reuses some of the game's. Only this level shows its map under them; the game's own levels are untouched.");

            plan.Anchor = MapPlanner.Anchor(plan.Walkable);
            if (options.Scale > 0) plan.Anchor.Scale = options.Scale;
            plan.Drawn = MapPlanner.Plan(plan.Walkable, plan.Floors);

            //The movies, from this install's own
            string uiPak = Path.Combine(Singleton.PathToAI ?? "", "DATA", "UI.PAK");
            PAK2 pak = File.Exists(uiPak) ? new PAK2(uiPak) : null;
            if (pak == null || !pak.Loaded)
                throw new McpError("The game's DATA/UI.PAK can't be read.");
            byte[] hub = Entry(pak, "DATA/UI/HUB.GFX"), page = Entry(pak, GamePage), template = Entry(pak, TemplateFloor);
            string mapName = string.IsNullOrWhiteSpace(options.MapName) ? Path.GetFileName((level.Name ?? "").Replace('\\', '/')).Replace('_', ' ').ToUpperInvariant() : options.MapName.Trim();
            Dictionary<string, KeyValuePair<string, string>> names = plan.Floors.ToDictionary(o => o.Keyframe, o => new KeyValuePair<string, string>(mapName, o.Name));
            plan.Files["HUB.GFX"] = MapPagePatch.Hub(hub, PageUrl);
            plan.Files[LevelUiOverrides.FilesFolderName + "/" + PageFile] = MapPagePatch.Page(page, FloorFolder, names, PageFontLibrary);
            foreach (MapPlanFloor floor in plan.Drawn)
                plan.Files[LevelUiOverrides.FilesFolderName + "/" + floor.Floor.Keyframe.ToUpperInvariant() + ".GFX"] = MapMovie.Build(template, floor.Floor.Keyframe, floor.Outlines, floor.Labels, plan.Anchor, FontLibrary);

            plan.Items = MapItems(level.Commands, plan.Floors);
            int mixed = plan.Items.Count(o => o.Mixed);
            if (mixed != 0)
                plan.Notes.Add(mixed + " map item setting" + (mixed == 1 ? " is" : "s are") + " shared by instances on different floors (inside a composite placed more than once): each placement gets its floor through an alias in the root composite.");
            return plan;
        }

        private static byte[] Entry(PAK2 pak, string name)
        {
            PAK2.File f = pak.Entries.FirstOrDefault(o => string.Equals(o.Filename, name, StringComparison.OrdinalIgnoreCase));
            if (f == null || f.Content == null) throw new McpError("The game's UI.PAK has no " + name + ".");
            return f.Content;
        }

        /// <summary>
        /// Every map item instance (a MapItem inside whatever composites hold it) by the entity its map_keyframe is set on: the
        /// outermost instance its composite's map_keyframe variable is fed through, or the MapItem itself. With floors, the floor
        /// each should be on (by where it is); an entity whose instances are on different floors is marked mixed.
        /// </summary>
        public static List<MapItemFloor> MapItems(Commands commands, IList<MapFloor> floors)
        {
            Composite root = commands.EntryPoints[0];
            ShortGuid mk = ShortGuidUtils.Generate("map_keyframe");
            Dictionary<ShortGuid, List<KeyValuePair<Composite, FunctionEntity>>> users = new Dictionary<ShortGuid, List<KeyValuePair<Composite, FunctionEntity>>>();
            foreach (Composite c in commands.Entries)
                foreach (FunctionEntity fe in c.functions)
                    if (!fe.function.IsFunctionType)
                    {
                        if (!users.TryGetValue(fe.function, out List<KeyValuePair<Composite, FunctionEntity>> l)) users[fe.function] = l = new List<KeyValuePair<Composite, FunctionEntity>>();
                        l.Add(new KeyValuePair<Composite, FunctionEntity>(c, fe));
                    }
            Dictionary<Entity, MapItemFloor> found = new Dictionary<Entity, MapItemFloor>();
            Dictionary<Entity, HashSet<string>> floorsSeen = new Dictionary<Entity, HashSet<string>>();
            foreach (Composite c in commands.Entries)
                foreach (FunctionEntity item in c.functions.Where(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.MapItem).ToList())
                {
                    bool fromVar = FedByVariable(c, item, mk);
                    int paths = 0;
                    foreach (List<KeyValuePair<Composite, FunctionEntity>> path in Paths(c, users, root, 0))
                    {
                        if (++paths > 500) break;
                        cTransform world = new cTransform(Vector3.Zero, Vector3.Zero);
                        foreach (KeyValuePair<Composite, FunctionEntity> hop in path)
                            world = InstanceTransform.Compose((hop.Value.GetParameter(ShortGuids.position)?.content as cTransform) ?? new cTransform(Vector3.Zero, Vector3.Zero), world);
                        Composite setIn = c; Entity setOn = item;
                        bool follow = fromVar;
                        int setAt = -1;
                        foreach (KeyValuePair<Composite, FunctionEntity> hop in path)
                        {
                            if (!follow) break;
                            setIn = hop.Key; setOn = hop.Value; setAt++;
                            follow = FedByVariable(hop.Key, hop.Value, mk);
                        }
                        if (!found.TryGetValue(setOn, out MapItemFloor f))
                        {
                            f = new MapItemFloor() { Composite = setIn, Entity = setOn, World = world.position, Was = (setOn.GetParameter(mk)?.content as cString)?.value ?? "" };
                            try { f.Name = McpScript.EntityName(commands, setIn, setOn); } catch { f.Name = setOn.shortGUID.ToString(); }
                            found.Add(setOn, f);
                            floorsSeen[setOn] = new HashSet<string>();
                        }

                        //The ids from the root down to the setting entity, and a root alias overriding it there
                        List<ShortGuid> ids = new List<ShortGuid>();
                        for (int i = path.Count - 1; i > setAt; i--) ids.Add(path[i].Value.shortGUID);
                        ids.Add(setOn.shortGUID);
                        MapItemPlacement placement = new MapItemPlacement() { Path = ids.ToArray(), World = world.position, Was = f.Was };
                        if (ids.Count > 1)
                        {
                            placement.Alias = McpScriptEdit.AliasesOn(root, ids.Concat(new[] { ShortGuid.Invalid }).ToArray()).FirstOrDefault(o => o.GetParameter(mk) != null);
                            if (placement.Alias != null) placement.Was = (placement.Alias.GetParameter(mk).content as cString)?.value ?? "";
                        }
                        if (floors != null && floors.Count != 0)
                        {
                            string key = floors[MapPlanner.FloorOf(floors, world.position.Y)].Keyframe;
                            floorsSeen[setOn].Add(key);
                            f.Keyframe = placement.Keyframe = key;
                        }
                        if (!f.Placements.Any(o => McpScriptEdit.SamePath(o.Path, placement.Path))) f.Placements.Add(placement);
                    }
                }
            foreach (MapItemFloor f in found.Values) f.Mixed = floorsSeen[f.Entity].Count > 1;
            return found.Values.ToList();
        }

        /// <summary>
        /// Put a map item on the floors it stands on: its own map_keyframe when its placements share one floor, or else (and where
        /// the root composite already overrides it) the map_keyframe of an alias per placement. Returns how many settings changed.
        /// </summary>
        internal static int SetFloors(McpScriptEdit edit, Composite root, MapItemFloor item)
        {
            int changed = 0;
            bool own = false;
            foreach (MapItemPlacement p in item.Placements.Where(o => o.Keyframe != null && o.Keyframe != o.Was))
            {
                if (p.Alias != null)
                    edit.SetParameter(root, p.Alias, "map_keyframe", p.Keyframe);
                else if (!item.Mixed || p.Path.Length < 2)
                {
                    if (own) continue;
                    edit.SetParameter(item.Composite, item.Entity, "map_keyframe", p.Keyframe);
                    own = true;
                }
                else
                {
                    AliasEntity alias = edit.FindOrAddAlias(root, p.Path.Concat(new[] { ShortGuid.Invalid }).ToArray(), Prefix + item.Name, out bool _);
                    edit.SetParameter(root, alias, "map_keyframe", p.Keyframe);
                }
                changed++;
            }
            return changed;
        }

        /* Whether an entity's map_keyframe comes from a variable of the composite it is in */
        private static bool FedByVariable(Composite c, Entity e, ShortGuid mk)
        {
            foreach (EntityConnector l in e.childLinks)
                if (l.thisParamID == mk && c.variables.Any(v => v.shortGUID == l.linkedEntityID))
                    return true;
            return false;
        }

        /* The instance paths from a composite up to the root, innermost first */
        private static IEnumerable<List<KeyValuePair<Composite, FunctionEntity>>> Paths(Composite c, Dictionary<ShortGuid, List<KeyValuePair<Composite, FunctionEntity>>> users, Composite root, int depth)
        {
            if (c == root) { yield return new List<KeyValuePair<Composite, FunctionEntity>>(); yield break; }
            if (depth > 32 || !users.TryGetValue(c.shortGUID, out List<KeyValuePair<Composite, FunctionEntity>> parents)) yield break;
            foreach (KeyValuePair<Composite, FunctionEntity> parent in parents)
                foreach (List<KeyValuePair<Composite, FunctionEntity>> up in Paths(parent.Key, users, root, depth + 1))
                {
                    List<KeyValuePair<Composite, FunctionEntity>> l = new List<KeyValuePair<Composite, FunctionEntity>>() { parent };
                    l.AddRange(up);
                    yield return l;
                }
        }
        #endregion

        #region Making it
        /// <summary>
        /// Write a plan: the files into the level's UI folder, then the anchor, floor boxes and map items' floors in the level's script
        /// as one undo step (what an earlier run placed is replaced). Call from a tool's thread. Returns what was done.
        /// </summary>
        public static List<string> Apply(LevelContent content, MapAssetsPlan plan, string labelPrefix)
        {
            List<string> done = new List<string>();
            Commands commands = content.Level.Commands;
            Composite root = commands.EntryPoints[0];

            //The files: the hub as the level's own movie, the page and floors as files it loads (earlier floors of ours go first)
            string folder = LevelUiOverrides.Folder(Singleton.PathToAI, content.Level.Name);
            string files = Path.Combine(folder, LevelUiOverrides.FilesFolderName);
            Directory.CreateDirectory(files);
            foreach (string old in MapPlanner.BorrowedKeyframes.Select(o => Path.Combine(files, o.ToUpperInvariant() + ".GFX")).Where(File.Exists))
                File.Delete(old);
            foreach (KeyValuePair<string, byte[]> f in plan.Files)
            {
                string path = Path.Combine(folder, f.Key.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, f.Value);
                done.Add("wrote UI\\" + f.Key.Replace('/', '\\') + " (" + f.Value.Length + " bytes)");
            }

            List<MapItemFloor> items = plan.Items.Where(o => o.Wrong).ToList();
            int settings = 0;
            McpScriptEdit.Outcome outcome = McpScriptEdit.Run("Generate Map Assets", root, edit =>
            {
                foreach (Entity old in root.functions.Where(o => SafeName(commands, root, o).StartsWith(Prefix, StringComparison.Ordinal)).Cast<Entity>().ToList())
                    edit.Delete(root, old);

                FunctionEntity pos = edit.AddFunction(root, FunctionType.PositionMarker, Prefix + "AnchorPos");
                edit.SetParameter(root, pos, "position", Transform(plan.Anchor.WorldX, plan.Anchor.WorldY, plan.Anchor.WorldZ));
                FunctionEntity anchor = edit.AddFunction(root, FunctionType.MapAnchor, Prefix + "Anchor");
                string[] slots = { "keyframe", "keyframe1", "keyframe2", "keyframe3", "keyframe4", "keyframe5" };
                for (int i = 0; i < plan.Floors.Count; i++) edit.SetParameter(root, anchor, slots[i], plan.Floors[i].Keyframe);
                edit.SetParameter(root, anchor, "map_north", new JArray(plan.Anchor.NorthX, 0, plan.Anchor.NorthZ));
                edit.SetParameter(root, anchor, "map_scale", plan.Anchor.Scale);
                edit.SetParameter(root, anchor, "is_default_for_items", true);
                edit.AddLink(root, anchor, "world_pos", pos, "reference");
                FunctionEntity start = edit.AddFunction(root, FunctionType.ThinkOnce, Prefix + "Start");
                edit.SetParameter(root, start, "start_on_reset", true);
                edit.AddLink(root, start, "on_think", anchor, "trigger");
                List<Entity> placed = new List<Entity>() { pos, anchor, start };
                for (int i = 0; i < plan.Drawn.Count; i++)
                {
                    MapPlanFloor f = plan.Drawn[i];
                    if (f.Boxes.Count == 0) continue;
                    FunctionEntity change = edit.AddFunction(root, FunctionType.Map_Floor_Change, Prefix + "Floor_" + (i + 1));
                    edit.SetParameter(root, change, "floor_name", f.Floor.Keyframe);
                    placed.Add(change);
                    for (int b = 0; b < f.Boxes.Count; b++)
                    {
                        MapBox mb = f.Boxes[b];
                        FunctionEntity box = edit.AddFunction(root, FunctionType.PlayerTriggerBox, Prefix + "FloorBox_" + (i + 1) + "_" + (b + 1));
                        edit.SetParameter(root, box, "position", Transform(mb.X, mb.Y, mb.Z));
                        edit.SetParameter(root, box, "half_dimensions", new JArray(Math.Round(mb.HalfX, 2), Math.Round(mb.HalfY, 2), Math.Round(mb.HalfZ, 2)));
                        edit.AddLink(root, box, "on_entered", change, "set_true");
                        placed.Add(box);
                    }
                }
                foreach (Entity e in placed) edit.PlaceNode(e, Page, null);
                foreach (MapItemFloor item in items)
                    settings += SetFloors(edit, root, item);
                edit.Select.Clear();
                edit.Select.Add(anchor);
            }, new McpScriptEdit.RunOptions() { Show = false, LabelPrefix = labelPrefix ?? "", AlsoTouches = items.Select(o => o.Composite).Distinct().ToList() });
            done.Add("placed " + Prefix + "* in the root composite: the anchor (" + plan.Floors.Count + " floor" + (plan.Floors.Count == 1 ? "" : "s") + ", scale " + plan.Anchor.Scale + "), " + plan.Drawn.Sum(o => o.Boxes.Count) + " floor boxes (at most " + MapPlanner.BoxCell + " m across)");
            done.Add("set " + items.Count + " map item" + (items.Count == 1 ? "'s" : "s'") + " floor (" + settings + " setting" + (settings == 1 ? "" : "s") + ")");
            done.AddRange(outcome.Notes);
            done.Add("Save the level, then launch it through OpenCAGE: the level's map shows from the start (Tab).");
            return done;
        }

        private static JObject Transform(float x, float y, float z) => new JObject()
        {
            ["position"] = new JArray(Math.Round(x, 3), Math.Round(y, 3), Math.Round(z, 3)),
            ["rotation"] = new JArray(0, 0, 0),
        };

        private static string SafeName(Commands commands, Composite composite, Entity entity)
        {
            try { return McpScript.EntityName(commands, composite, entity) ?? ""; } catch { return ""; }
        }
        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace OpenCAGE.MapAssets
{
    /// <summary>One floor of a level's map, ready to write: what it draws and the boxes that tell the game the player is on it.</summary>
    public class MapPlanFloor
    {
        public MapFloor Floor;
        public List<MapOutline> Outlines = new List<MapOutline>();
        public List<MapLabel> Labels = new List<MapLabel>();
        public List<MapBox> Boxes = new List<MapBox>();
    }

    /// <summary>A trigger box: centre and half sizes (world).</summary>
    public class MapBox
    {
        public float X, Y, Z, HalfX, HalfY, HalfZ;
    }

    /// <summary>The choices a level's map is made with: keyframes, the anchor, each floor's drawing and box.</summary>
    public static class MapPlanner
    {
        /// <summary>
        /// The map keyframes a level borrows, in floor order. The game only opens a map for a keyframe its own list (GBL_ITEM) has; these
        /// belong to the game's own levels, so a level reusing them shows them only in itself. The first is what every map item
        /// starts with, so ground floor items need nothing.
        /// </summary>
        public static readonly string[] BorrowedKeyframes = { "RnD_HzdLab_1", "RnD_HzdLab_2", "And_Lab_0", "And_Lab_1", "And_Lab_2", "And_Lab_3" };

        /// <summary>The most floors a MapAnchor takes.</summary>
        public const int MaxFloors = 6;

        /// <summary>
        /// The anchor: north up, centred on the middle of the walkable area, scaled so the middle 80% of it (by position) fits a map
        /// screen at the map page's opening zoom.
        /// </summary>
        public static MapAnchorSettings Anchor(IList<MapPolygon> polygons)
        {
            List<float> xs = polygons.Select(p => p.CentreX).OrderBy(v => v).ToList(), zs = polygons.Select(p => p.CentreZ).OrderBy(v => v).ToList();
            float x0 = xs[(int)(xs.Count * 0.1f)], x1 = xs[(int)(xs.Count * 0.9f)], z0 = zs[(int)(zs.Count * 0.1f)], z1 = zs[(int)(zs.Count * 0.9f)];
            float w = Math.Max(10, x1 - x0), h = Math.Max(10, z1 - z0);
            // the page opens at zoom 1.5 on a 960 x 540 screen
            float scale = Math.Min(MapTransform.MovieWidth / 1.5f / (w * MapTransform.MovieWidth / MapTransform.WorldMapWidth), MapTransform.MovieHeight / 1.5f / (h * MapTransform.MovieHeight / MapTransform.WorldMapHeight));
            scale = (float)Math.Round(Math.Max(0.3f, Math.Min(3f, scale)), 2);
            float y = polygons.Average(p => p.Height);
            return new MapAnchorSettings() { WorldX = (float)Math.Round((x0 + x1) / 2, 1), WorldY = (float)Math.Round(y, 1), WorldZ = (float)Math.Round((z0 + z1) / 2, 1), Scale = scale };
        }

        /// <summary>The floor a height is on (the nearest when none takes it).</summary>
        public static int FloorOf(IList<MapFloor> floors, float height)
        {
            for (int i = 0; i < floors.Count; i++) if (floors[i].Takes(height)) return i;
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < floors.Count; i++) { float d = Math.Abs(floors[i].Level - height); if (d < bd) { bd = d; best = i; } }
            return best;
        }

        /// <summary>
        /// The widest a floor's trigger box gets (m). In game a box fired for a player ~15 m from its centre but not ~28 m (well inside a
        /// 60 m one), and the game's own floor boxes are a few metres across: squares this size keep every point within ~14 m.
        /// </summary>
        public const float BoxCell = 20;

        /// <summary>
        /// Each floor's drawing: its own walkable area as rooms, and the floor above it as a faint ghost (where the next level is);
        /// trigger boxes over its area (one per BoxCell square that has some), each from its lowest height to its highest.
        /// </summary>
        public static List<MapPlanFloor> Plan(IList<MapPolygon> polygons, IList<MapFloor> floors)
        {
            MapLayerStyle rooms = new MapLayerStyle() { Name = "rooms" };
            MapLayerStyle above = new MapLayerStyle() { Name = "above", Simplify = 0.4f, MinArea = 6f, FillAlpha = 6, LineAlpha = 70 };
            List<MapPlanFloor> plan = new List<MapPlanFloor>();
            for (int i = 0; i < floors.Count; i++)
            {
                MapFloor f = floors[i];
                List<MapPolygon> mine = polygons.Where(p => FloorOf(floors, p.Height) == i).ToList();
                MapPlanFloor pf = new MapPlanFloor() { Floor = f };
                pf.Outlines.AddRange(MapFloorPlan.Trace(mine, rooms));
                if (i + 1 < floors.Count)
                    pf.Outlines.AddRange(MapFloorPlan.Trace(polygons.Where(p => FloorOf(floors, p.Height) == i + 1).ToList(), above));
                if (mine.Count != 0)
                {
                    float x0 = mine.Min(p => p.Outline.Min(q => q.X)) - 2, z0 = mine.Min(p => p.Outline.Min(q => q.Y)) - 2;
                    //Each polygon in every cell its bounds reach, so a big one leaves no gap
                    IEnumerable<KeyValuePair<Point, MapPolygon>> inCells = mine.SelectMany(p =>
                    {
                        int ix0 = (int)Math.Floor((p.Outline.Min(q => q.X) - x0) / BoxCell), ix1 = (int)Math.Floor((p.Outline.Max(q => q.X) - x0) / BoxCell);
                        int iz0 = (int)Math.Floor((p.Outline.Min(q => q.Y) - z0) / BoxCell), iz1 = (int)Math.Floor((p.Outline.Max(q => q.Y) - z0) / BoxCell);
                        List<KeyValuePair<Point, MapPolygon>> l = new List<KeyValuePair<Point, MapPolygon>>();
                        for (int ix = ix0; ix <= ix1; ix++) for (int iz = iz0; iz <= iz1; iz++) l.Add(new KeyValuePair<Point, MapPolygon>(new Point(ix, iz), p));
                        return l;
                    });
                    foreach (IGrouping<Point, MapPolygon> cell in inCells.GroupBy(o => o.Key, o => o.Value))
                    {
                        float cx0 = x0 + cell.Key.X * BoxCell, cz0 = z0 + cell.Key.Y * BoxCell;
                        float minX = Math.Max(cx0, cell.Min(p => p.Outline.Min(q => q.X)) - 2), maxX = Math.Min(cx0 + BoxCell, cell.Max(p => p.Outline.Max(q => q.X)) + 2);
                        float minZ = Math.Max(cz0, cell.Min(p => p.Outline.Min(q => q.Y)) - 2), maxZ = Math.Min(cz0 + BoxCell, cell.Max(p => p.Outline.Max(q => q.Y)) + 2);
                        float minY = cell.Min(p => p.Height) - 2, maxY = cell.Max(p => p.Height) + 3;
                        if (!float.IsInfinity(f.From)) minY = Math.Max(minY, f.From);
                        if (!float.IsInfinity(f.To)) maxY = Math.Min(maxY, f.To);
                        if (maxX - minX < 0.5f || maxZ - minZ < 0.5f || maxY - minY < 0.5f) continue;
                        pf.Boxes.Add(new MapBox() { X = (minX + maxX) / 2, Y = (minY + maxY) / 2, Z = (minZ + maxZ) / 2, HalfX = (maxX - minX) / 2, HalfY = (maxY - minY) / 2, HalfZ = (maxZ - minZ) / 2 });
                    }
                }
                plan.Add(pf);
            }
            return plan;
        }
    }
}

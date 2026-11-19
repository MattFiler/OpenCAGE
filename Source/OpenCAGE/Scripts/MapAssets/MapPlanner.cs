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

        /// <summary>The smallest a floor box square is split down to where two floors' heights meet in it (m).</summary>
        public const float BoxCellMin = 5;

        /// <summary>
        /// Each floor's drawing: its own walkable area as rooms (floorOf: each polygon's floor), and the floor above it as a faint ghost
        /// (where the next level is); trigger boxes over its area, one per BoxCell square that has some, from its lowest height to its
        /// highest. Where floors share a square they split its height between them, the square split in four (down to BoxCellMin)
        /// where their heights there overlap, so no box holds another floor's walkable area.
        /// </summary>
        public static List<MapPlanFloor> Plan(IList<MapPolygon> polygons, IList<MapFloor> floors, IList<int> floorOf)
        {
            MapLayerStyle rooms = new MapLayerStyle() { Name = "rooms" };
            MapLayerStyle above = new MapLayerStyle() { Name = "above", Simplify = 0.5f, MinArea = 6f, FillAlpha = 6, LineAlpha = 70 };
            List<MapPlanFloor> plan = new List<MapPlanFloor>();
            for (int i = 0; i < floors.Count; i++)
            {
                MapPlanFloor pf = new MapPlanFloor() { Floor = floors[i] };
                pf.Outlines.AddRange(MapFloorPlan.Trace(polygons.Where((p, k) => floorOf[k] == i).ToList(), rooms));
                if (i + 1 < floors.Count)
                    pf.Outlines.AddRange(MapFloorPlan.Trace(polygons.Where((p, k) => floorOf[k] == i + 1).ToList(), above));
                plan.Add(pf);
            }
            if (polygons.Count == 0) return plan;

            //Each polygon in every square its bounds reach, so a big one leaves no gap
            List<KeyValuePair<MapPolygon, int>> all = polygons.Select((p, k) => new KeyValuePair<MapPolygon, int>(p, floorOf[k])).ToList();
            float x0 = polygons.Min(p => p.Outline.Min(q => q.X)) - 2, z0 = polygons.Min(p => p.Outline.Min(q => q.Y)) - 2;
            IEnumerable<KeyValuePair<Point, KeyValuePair<MapPolygon, int>>> inCells = all.SelectMany(o =>
            {
                int ix0 = (int)Math.Floor((o.Key.Outline.Min(q => q.X) - x0) / BoxCell), ix1 = (int)Math.Floor((o.Key.Outline.Max(q => q.X) - x0) / BoxCell);
                int iz0 = (int)Math.Floor((o.Key.Outline.Min(q => q.Y) - z0) / BoxCell), iz1 = (int)Math.Floor((o.Key.Outline.Max(q => q.Y) - z0) / BoxCell);
                List<KeyValuePair<Point, KeyValuePair<MapPolygon, int>>> l = new List<KeyValuePair<Point, KeyValuePair<MapPolygon, int>>>();
                for (int ix = ix0; ix <= ix1; ix++) for (int iz = iz0; iz <= iz1; iz++) l.Add(new KeyValuePair<Point, KeyValuePair<MapPolygon, int>>(new Point(ix, iz), o));
                return l;
            });
            foreach (IGrouping<Point, KeyValuePair<MapPolygon, int>> cell in inCells.GroupBy(o => o.Key, o => o.Value))
                Boxes(x0 + cell.Key.X * BoxCell, z0 + cell.Key.Y * BoxCell, BoxCell, cell.ToList(), plan);
            return plan;
        }

        /* A square's floor boxes, from the polygons (and their floors) reaching it */
        private static void Boxes(float x0, float z0, float size, List<KeyValuePair<MapPolygon, int>> items, List<MapPlanFloor> plan)
        {
            var floors = items.GroupBy(o => o.Value).OrderBy(g => g.Key).Select(g => new
            {
                Floor = g.Key,
                Low = g.Min(o => o.Key.Lowest),
                High = g.Max(o => o.Key.Highest),
                MinX = g.Min(o => o.Key.Outline.Min(q => q.X)), MaxX = g.Max(o => o.Key.Outline.Max(q => q.X)),
                MinZ = g.Min(o => o.Key.Outline.Min(q => q.Y)), MaxZ = g.Max(o => o.Key.Outline.Max(q => q.Y)),
            }).ToList();
            bool overlap = false;
            for (int k = 0; k + 1 < floors.Count; k++) if (floors[k].High + 1 > floors[k + 1].Low) overlap = true;
            if (overlap && size / 2 >= BoxCellMin)
            {
                float h = size / 2;
                for (int a = 0; a < 2; a++)
                    for (int b = 0; b < 2; b++)
                    {
                        float sx = x0 + a * h, sz = z0 + b * h;
                        List<KeyValuePair<MapPolygon, int>> part = items.Where(o => o.Key.Outline.Max(q => q.X) >= sx && o.Key.Outline.Min(q => q.X) <= sx + h &&
                                                                                     o.Key.Outline.Max(q => q.Y) >= sz && o.Key.Outline.Min(q => q.Y) <= sz + h).ToList();
                        if (part.Count != 0) Boxes(sx, sz, h, part, plan);
                    }
                return;
            }
            for (int k = 0; k < floors.Count; k++)
            {
                var f = floors[k];
                float minX = Math.Max(x0, f.MinX - 2), maxX = Math.Min(x0 + size, f.MaxX + 2);
                float minZ = Math.Max(z0, f.MinZ - 2), maxZ = Math.Min(z0 + size, f.MaxZ + 2);
                float minY = f.Low - 2, maxY = f.High + 3;
                if (k > 0) minY = Split(floors[k - 1].High, f.Low);
                if (k + 1 < floors.Count) maxY = Split(f.High, floors[k + 1].Low);
                if (maxX - minX < 0.5f || maxZ - minZ < 0.5f || maxY - minY < 0.5f) continue;
                plan[f.Floor].Boxes.Add(new MapBox() { X = (minX + maxX) / 2, Y = (minY + maxY) / 2, Z = (minZ + maxZ) / 2, HalfX = (maxX - minX) / 2, HalfY = (maxY - minY) / 2, HalfZ = (maxZ - minZ) / 2 });
            }
        }

        /* The height between a floor's highest walkable surface and the next floor's lowest (or just under that, where they overlap) */
        private static float Split(float lowerHigh, float upperLow) => lowerHigh < upperLow ? (lowerHigh + upperLow) / 2 : upperLow - 0.25f;
    }
}

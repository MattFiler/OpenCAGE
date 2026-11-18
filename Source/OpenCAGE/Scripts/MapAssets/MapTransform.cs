using System;
using System.Drawing;

namespace OpenCAGE.MapAssets
{
    /// <summary>
    /// What a level's MapAnchor sets, plus where the floor movies put their start marker: together they say where a world point is
    /// drawn on the Tab map (the player arrow, map item icons and objective markers all go through the same sum).
    /// </summary>
    public class MapAnchorSettings
    {
        /// <summary>The anchor's world position (its world_pos, a PositionMarker).</summary>
        public float WorldX, WorldY, WorldZ;
        /// <summary>map_north: the world direction drawn as up (horizontal).</summary>
        public float NorthX = 0, NorthZ = 1;
        /// <summary>map_scale.</summary>
        public float Scale = 1;
        /// <summary>startmarker_mc in each floor movie, in the movie's pixels.</summary>
        public float StartX = 480, StartY = 270;
    }

    /// <summary>The game's world-to-map sum.</summary>
    public static class MapTransform
    {
        /// <summary>The world area one map screen covers at map_scale 1 (metres): the game's own constants.</summary>
        public const float WorldMapWidth = 240f, WorldMapHeight = 140f;
        /// <summary>A map screen in movie pixels.</summary>
        public const float MovieWidth = 960f, MovieHeight = 540f;

        /// <summary>
        /// A world point (x, z) on a floor movie: start marker + (-dot(P - W, B) * s / 240 * 960, -dot(P - W, N) * s / 140 * 540), where
        /// N is north and B = (-N.z, N.x). Height plays no part.
        /// </summary>
        public static PointF ToMovie(MapAnchorSettings a, float x, float z)
        {
            float len = (float)Math.Sqrt(a.NorthX * a.NorthX + a.NorthZ * a.NorthZ);
            float nx = len > 0 ? a.NorthX / len : 0, nz = len > 0 ? a.NorthZ / len : 1;
            float bx = -nz, bz = nx;
            float dx = x - a.WorldX, dz = z - a.WorldZ;
            float u = -(dx * bx + dz * bz) * a.Scale / WorldMapWidth;
            float v = -(dx * nx + dz * nz) * a.Scale / WorldMapHeight;
            return new PointF(a.StartX + u * MovieWidth, a.StartY + v * MovieHeight);
        }
    }
}

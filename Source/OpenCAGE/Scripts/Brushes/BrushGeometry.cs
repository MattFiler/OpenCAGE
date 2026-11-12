using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.Brushwork
{
    /// <summary>
    /// The triangles a brush is drawn and collided with, in the entity's own space (CATHODE's: metres, Y up), wound as the game
    /// draws a front face - cross(b - a, c - a) along the face's normal, as every shipped mesh agrees with its normals - with
    /// texture coordinates projected the way Hammer's world alignment does it.
    /// </summary>
    public sealed class BrushGeometry
    {
        /// <summary>One flat face of the brush: its corners in order round the normal, and which face settings texture it.</summary>
        public sealed class Polygon
        {
            public Vector3[] Points;
            public Vector3 Normal;
            public BrushFace Texture;
            public int FaceIndex;
        }

        /// <summary>A vertex as drawn.</summary>
        public struct Vertex
        {
            public Vector3 Position, Normal, Tangent, Bitangent;
            public Vector2 UV;
        }

        public List<Polygon> Polygons = new List<Polygon>();

        public static BrushGeometry Build(BrushDefinition brush)
        {
            BrushGeometry geometry = new BrushGeometry();
            Vector3 a = brush.Min, b = brush.Max;
            switch (brush.Kind)
            {
                case BrushKind.Box:
                    //+X -X +Y -Y +Z -Z
                    geometry.Add(0, brush.Faces[0], new Vector3(1, 0, 0), new Vector3(b.X, a.Y, a.Z), new Vector3(b.X, b.Y, a.Z), new Vector3(b.X, b.Y, b.Z), new Vector3(b.X, a.Y, b.Z));
                    geometry.Add(1, brush.Faces[1], new Vector3(-1, 0, 0), new Vector3(a.X, a.Y, a.Z), new Vector3(a.X, b.Y, a.Z), new Vector3(a.X, b.Y, b.Z), new Vector3(a.X, a.Y, b.Z));
                    geometry.Add(2, brush.Faces[2], new Vector3(0, 1, 0), new Vector3(a.X, b.Y, a.Z), new Vector3(b.X, b.Y, a.Z), new Vector3(b.X, b.Y, b.Z), new Vector3(a.X, b.Y, b.Z));
                    geometry.Add(3, brush.Faces[3], new Vector3(0, -1, 0), new Vector3(a.X, a.Y, a.Z), new Vector3(b.X, a.Y, a.Z), new Vector3(b.X, a.Y, b.Z), new Vector3(a.X, a.Y, b.Z));
                    geometry.Add(4, brush.Faces[4], new Vector3(0, 0, 1), new Vector3(a.X, a.Y, b.Z), new Vector3(b.X, a.Y, b.Z), new Vector3(b.X, b.Y, b.Z), new Vector3(a.X, b.Y, b.Z));
                    geometry.Add(5, brush.Faces[5], new Vector3(0, 0, -1), new Vector3(a.X, a.Y, a.Z), new Vector3(b.X, a.Y, a.Z), new Vector3(b.X, b.Y, a.Z), new Vector3(a.X, b.Y, a.Z));
                    break;
                case BrushKind.Plane:
                    geometry.Add(0, brush.Faces[0], new Vector3(0, 1, 0), new Vector3(a.X, a.Y, a.Z), new Vector3(b.X, a.Y, a.Z), new Vector3(b.X, a.Y, b.Z), new Vector3(a.X, a.Y, b.Z));
                    if (brush.DoubleSided && brush.Faces.Count > 1)
                        geometry.Add(1, brush.Faces[1], new Vector3(0, -1, 0), new Vector3(a.X, a.Y, a.Z), new Vector3(b.X, a.Y, a.Z), new Vector3(b.X, a.Y, b.Z), new Vector3(a.X, a.Y, b.Z));
                    break;
                case BrushKind.Mesh:
                    for (int f = 0; f < brush.Mesh.Faces.Count; f++)
                    {
                        BrushMeshFace face = brush.Mesh.Faces[f];
                        Vector3[] points = face.Vertices.Select(i => brush.Mesh.Vertices[i]).ToArray();
                        Vector3 normal = NewellNormal(points);
                        if (normal.LengthSquared() < 1e-12f) continue;
                        geometry.Polygons.Add(new Polygon() { Points = points, Normal = Vector3.Normalize(normal), Texture = face.Texture, FaceIndex = f });
                    }
                    break;
            }
            return geometry;
        }

        /* A face's corners, put in the order that runs round its outward normal (they are given round it one way or the other) */
        private void Add(int index, BrushFace texture, Vector3 normal, params Vector3[] points)
        {
            if (Vector3.Dot(NewellNormal(points), normal) < 0)
                Array.Reverse(points);
            Polygons.Add(new Polygon() { Points = points, Normal = normal, Texture = texture, FaceIndex = index });
        }

        /// <summary>A polygon's normal by Newell's method (any winding, any planar-ish polygon), not normalised.</summary>
        public static Vector3 NewellNormal(IList<Vector3> points)
        {
            Vector3 n = Vector3.Zero;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 c = points[i], d = points[(i + 1) % points.Count];
                n.X += (c.Y - d.Y) * (c.Z + d.Z);
                n.Y += (c.Z - d.Z) * (c.X + d.X);
                n.Z += (c.X - d.X) * (c.Y + d.Y);
            }
            //Points the way cross(b - a, c - a) does for a triangle: the side the game draws
            return n;
        }

        #region Texture projection
        /// <summary>
        /// The texture axes Hammer's world alignment gives a face: projected along the axis its normal is nearest, U to the
        /// right and V down as the face is seen from the front (a wall's texture upright, a floor's "up" along +Z).
        /// </summary>
        public static void WorldAxes(Vector3 normal, out Vector3 right, out Vector3 up)
        {
            Vector3 n = normal;
            float ax = Math.Abs(n.X), ay = Math.Abs(n.Y), az = Math.Abs(n.Z);
            Vector3 axis;
            if (ay >= ax && ay >= az)
            {
                axis = new Vector3(0, Math.Sign(n.Y) == 0 ? 1 : Math.Sign(n.Y), 0);
                up = new Vector3(0, 0, 1);
            }
            else if (ax >= az)
            {
                axis = new Vector3(Math.Sign(n.X), 0, 0);
                up = new Vector3(0, 1, 0);
            }
            else
            {
                axis = new Vector3(0, 0, Math.Sign(n.Z));
                up = new Vector3(0, 1, 0);
            }
            right = Vector3.Cross(axis, up);
        }

        /// <summary>A point's texture coordinates on a face (repeats), and the directions U and V grow along.</summary>
        public static Vector2 Project(Vector3 point, Vector3 normal, BrushFace face, Vector3 origin, out Vector3 tangent, out Vector3 bitangent)
        {
            WorldAxes(normal, out Vector3 right, out Vector3 up);
            Vector3 p = point - origin;
            float a = Vector3.Dot(p, right), b = -Vector3.Dot(p, up);
            double turn = (face?.Rotation ?? 0) * Math.PI / 180.0;
            float cos = (float)Math.Cos(turn), sin = (float)Math.Sin(turn);
            float u = a * cos - b * sin, v = a * sin + b * cos;
            Vector2 scale = face?.Scale ?? Vector2.One;
            Vector2 offset = face?.Offset ?? Vector2.Zero;
            tangent = Vector3.Normalize(right * cos + up * sin) * Math.Sign(scale.X);
            bitangent = Vector3.Normalize(right * sin - up * cos) * Math.Sign(scale.Y);
            return new Vector2(u / (BrushDefinition.MetresPerRepeat * scale.X) + offset.X, v / (BrushDefinition.MetresPerRepeat * scale.Y) + offset.Y);
        }
        #endregion

        /// <summary>Each polygon as a fan of triangles over its own vertices: vertices, then corner indices into them (three per triangle).</summary>
        public void Triangulate(Polygon polygon, Vector3 uvOrigin, List<Vertex> vertices, List<int> indices)
        {
            int start = vertices.Count;
            foreach (Vector3 point in polygon.Points)
            {
                Vector2 uv = Project(point, polygon.Normal, polygon.Texture, uvOrigin, out Vector3 tangent, out Vector3 bitangent);
                //Tangent made square to the normal (a mesh face's projection axis is not its normal)
                tangent = tangent - polygon.Normal * Vector3.Dot(polygon.Normal, tangent);
                tangent = tangent.LengthSquared() > 1e-12f ? Vector3.Normalize(tangent) : new Vector3(1, 0, 0);
                vertices.Add(new Vertex() { Position = point, Normal = polygon.Normal, Tangent = tangent, Bitangent = bitangent, UV = uv });
            }
            for (int i = 1; i + 1 < polygon.Points.Length; i++)
            {
                int a = start, b = start + i, c = start + i + 1;
                //Each fan triangle runs round the normal as the polygon does; a degenerate one is left out
                Vector3 cross = Vector3.Cross(polygon.Points[i] - polygon.Points[0], polygon.Points[i + 1] - polygon.Points[0]);
                if (cross.LengthSquared() < 1e-14f) continue;
                if (Vector3.Dot(cross, polygon.Normal) < 0) { int t = b; b = c; c = t; }
                indices.Add(a); indices.Add(b); indices.Add(c);
            }
        }

        /// <summary>The triangles alone, for collision: shared corners welded.</summary>
        public CollisionProxyImporter.MeshSource ToCollisionMesh(string name)
        {
            CollisionProxyImporter.MeshSource source = new CollisionProxyImporter.MeshSource() { Name = name ?? "" };
            Dictionary<(long, long, long), int> welded = new Dictionary<(long, long, long), int>();
            int Vertex(Vector3 p)
            {
                (long, long, long) key = ((long)Math.Round(p.X * 10000), (long)Math.Round(p.Y * 10000), (long)Math.Round(p.Z * 10000));
                if (!welded.TryGetValue(key, out int at))
                {
                    at = source.Positions.Count;
                    source.Positions.Add(p);
                    welded[key] = at;
                }
                return at;
            }
            foreach (Polygon polygon in Polygons)
            {
                for (int i = 1; i + 1 < polygon.Points.Length; i++)
                {
                    Vector3 cross = Vector3.Cross(polygon.Points[i] - polygon.Points[0], polygon.Points[i + 1] - polygon.Points[0]);
                    if (cross.LengthSquared() < 1e-14f) continue;
                    int a = Vertex(polygon.Points[0]), b = Vertex(polygon.Points[i]), c = Vertex(polygon.Points[i + 1]);
                    if (Vector3.Dot(cross, polygon.Normal) < 0) { int t = b; b = c; c = t; }
                    source.Indices.Add(a); source.Indices.Add(b); source.Indices.Add(c);
                }
            }
            return source;
        }

        /// <summary>A hash of the triangles (to a tenth of a millimetre): what collision is made from, so an edit that leaves them alone keeps the collision it has.</summary>
        public string TriangleHash() => HashOf(ToCollisionMesh(null));

        /// <summary>A hash of a triangle soup's positions (to a tenth of a millimetre) and indices.</summary>
        public static string HashOf(CollisionProxyImporter.MeshSource mesh)
        {
            unchecked
            {
                long hash = 1469598103934665603L;
                foreach (Vector3 p in mesh.Positions)
                {
                    hash = (hash ^ (long)Math.Round(p.X * 10000.0)) * 1099511628211L;
                    hash = (hash ^ (long)Math.Round(p.Y * 10000.0)) * 1099511628211L;
                    hash = (hash ^ (long)Math.Round(p.Z * 10000.0)) * 1099511628211L;
                }
                foreach (int i in mesh.Indices)
                    hash = (hash ^ i) * 1099511628211L;
                return mesh.Indices.Count.ToString(CultureInfo.InvariantCulture) + ":" + hash.ToString("X16", CultureInfo.InvariantCulture);
            }
        }
    }
}

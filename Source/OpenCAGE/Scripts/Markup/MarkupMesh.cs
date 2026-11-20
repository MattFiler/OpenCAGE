using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.Markup
{
    /// <summary>
    /// Triangles for composite markup, one surface per colour, in Cathode space (metres, Y up, +Z forward). Small shape
    /// helpers build the built-in markup and the shapes a level's own markup can use.
    /// </summary>
    public sealed class MarkupMesh
    {
        public sealed class Surface
        {
            public Vector3 Colour;
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<int> Indices = new List<int>();
        }

        public readonly List<Surface> Surfaces = new List<Surface>();

        public Surface In(Vector3 colour)
        {
            Surface surface = Surfaces.FirstOrDefault(o => o.Colour == colour);
            if (surface == null)
                Surfaces.Add(surface = new Surface() { Colour = colour });
            return surface;
        }

        public int TriangleCount => Surfaces.Sum(o => o.Indices.Count / 3);

        public bool TryGetBounds(out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue);
            max = new Vector3(float.MinValue);
            bool any = false;
            foreach (Surface surface in Surfaces)
                foreach (Vector3 v in surface.Vertices)
                {
                    min = Vector3.Min(min, v);
                    max = Vector3.Max(max, v);
                    any = true;
                }
            return any;
        }

        /// <summary>Everything in <paramref name="other"/>, moved by <paramref name="transform"/>.</summary>
        public void Append(MarkupMesh other, Matrix4x4 transform)
        {
            foreach (Surface source in other.Surfaces)
            {
                Surface target = In(source.Colour);
                int start = target.Vertices.Count;
                foreach (Vector3 v in source.Vertices)
                    target.Vertices.Add(Vector3.Transform(v, transform));
                foreach (int i in source.Indices)
                    target.Indices.Add(start + i);
            }
        }

        /// <summary>Raw triangles (a mesh piece), skipping indices that point nowhere.</summary>
        public void Triangles(Vector3 colour, IList<Vector3> vertices, IList<int> indices, Matrix4x4 transform)
        {
            if (vertices == null || indices == null)
                return;
            Surface surface = In(colour);
            int start = surface.Vertices.Count;
            foreach (Vector3 v in vertices)
                surface.Vertices.Add(Vector3.Transform(v, transform));
            for (int i = 0; i + 2 < indices.Count; i += 3)
            {
                if (indices[i] < 0 || indices[i + 1] < 0 || indices[i + 2] < 0 || indices[i] >= vertices.Count || indices[i + 1] >= vertices.Count || indices[i + 2] >= vertices.Count)
                    continue;
                surface.Indices.Add(start + indices[i]);
                surface.Indices.Add(start + indices[i + 1]);
                surface.Indices.Add(start + indices[i + 2]);
            }
        }

        private void Quad(Surface s, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int start = s.Vertices.Count;
            s.Vertices.Add(a); s.Vertices.Add(b); s.Vertices.Add(c); s.Vertices.Add(d);
            s.Indices.Add(start); s.Indices.Add(start + 1); s.Indices.Add(start + 2);
            s.Indices.Add(start); s.Indices.Add(start + 2); s.Indices.Add(start + 3);
        }

        /// <summary>An axis-aligned box between two corners.</summary>
        public void Box(Vector3 colour, Vector3 min, Vector3 max)
        {
            Vector3 c = (min + max) / 2, h = (max - min) / 2;
            OrientedBox(colour, c, new Vector3(h.X, 0, 0), new Vector3(0, h.Y, 0), new Vector3(0, 0, h.Z));
        }

        /// <summary>A box round a centre, its three half-axes given (they need not be axis-aligned).</summary>
        public void OrientedBox(Vector3 colour, Vector3 centre, Vector3 ax, Vector3 ay, Vector3 az)
        {
            Surface s = In(colour);
            Vector3 P(int x, int y, int z) => centre + ax * x + ay * y + az * z;
            Quad(s, P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1));
            Quad(s, P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1));
            Quad(s, P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1));
            Quad(s, P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1));
            Quad(s, P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1));
            Quad(s, P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1));
        }

        /// <summary>
        /// A flat outline drawn in a plane and given thickness: <paramref name="u"/> and <paramref name="v"/> are the plane's axes
        /// from <paramref name="origin"/>, and the outline is pushed from <paramref name="near"/> to <paramref name="far"/> along
        /// <paramref name="n"/>. The outline must be convex (a fan fills its faces).
        /// </summary>
        public void Prism(Vector3 colour, Vector3 origin, Vector3 u, Vector3 v, Vector3 n, IList<Vector2> outline, float near, float far)
        {
            if (outline == null || outline.Count < 3)
                return;
            Surface s = In(colour);
            Vector3 At(Vector2 p, float depth) => origin + u * p.X + v * p.Y + n * depth;
            int count = outline.Count;
            int front = s.Vertices.Count;
            foreach (Vector2 p in outline) s.Vertices.Add(At(p, near));
            int back = s.Vertices.Count;
            foreach (Vector2 p in outline) s.Vertices.Add(At(p, far));
            for (int i = 1; i + 1 < count; i++)
            {
                s.Indices.Add(front); s.Indices.Add(front + i + 1); s.Indices.Add(front + i);
                s.Indices.Add(back); s.Indices.Add(back + i); s.Indices.Add(back + i + 1);
            }
            for (int i = 0; i < count; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % count];
                Quad(s, At(a, near), At(b, near), At(b, far), At(a, far));
            }
        }

        /// <summary>
        /// <see cref="Prism"/> for any simple outline, concave too: its faces are ear-clipped rather than fanned.
        /// </summary>
        public void Extrude(Vector3 colour, Vector3 origin, Vector3 u, Vector3 v, Vector3 n, IList<Vector2> outline, float near, float far)
        {
            if (outline == null || outline.Count < 3)
                return;
            Surface s = In(colour);
            Vector3 At(Vector2 p, float depth) => origin + u * p.X + v * p.Y + n * depth;
            int count = outline.Count;
            int front = s.Vertices.Count;
            foreach (Vector2 p in outline) s.Vertices.Add(At(p, near));
            int back = s.Vertices.Count;
            foreach (Vector2 p in outline) s.Vertices.Add(At(p, far));
            List<int> triangles = Triangulate(outline);
            for (int i = 0; i + 2 < triangles.Count; i += 3)
            {
                //Wound as Prism winds its fan
                s.Indices.Add(front + triangles[i]); s.Indices.Add(front + triangles[i + 2]); s.Indices.Add(front + triangles[i + 1]);
                s.Indices.Add(back + triangles[i]); s.Indices.Add(back + triangles[i + 1]); s.Indices.Add(back + triangles[i + 2]);
            }
            for (int i = 0; i < count; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % count];
                Quad(s, At(a, near), At(b, near), At(b, far), At(a, far));
            }
        }

        /// <summary>Ear-clips a simple polygon: triangles as outline indices, each wound the way the outline runs.</summary>
        public static List<int> Triangulate(IList<Vector2> outline)
        {
            List<int> result = new List<int>();
            List<int> remaining = Enumerable.Range(0, outline.Count).ToList();
            float area = 0;
            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Count];
                area += a.X * b.Y - b.X * a.Y;
            }
            float sign = area >= 0 ? 1 : -1;
            float Cross(Vector2 a, Vector2 b, Vector2 c) => ((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X)) * sign;

            while (remaining.Count > 3)
            {
                bool clipped = false;
                for (int i = 0; i < remaining.Count && !clipped; i++)
                {
                    int ia = remaining[(i + remaining.Count - 1) % remaining.Count], ib = remaining[i], ic = remaining[(i + 1) % remaining.Count];
                    Vector2 a = outline[ia], b = outline[ib], c = outline[ic];
                    if (Cross(a, b, c) <= 1e-10f)
                        continue;
                    bool inside = false;
                    foreach (int ip in remaining)
                    {
                        if (ip == ia || ip == ib || ip == ic)
                            continue;
                        Vector2 p = outline[ip];
                        if (Cross(a, b, p) >= 0 && Cross(b, c, p) >= 0 && Cross(c, a, p) >= 0)
                        {
                            inside = true;
                            break;
                        }
                    }
                    if (inside)
                        continue;
                    result.Add(ia); result.Add(ib); result.Add(ic);
                    remaining.RemoveAt(i);
                    clipped = true;
                }
                //Degenerate: fan the rest rather than loop
                if (!clipped)
                {
                    for (int i = 1; i + 1 < remaining.Count; i++)
                    {
                        result.Add(remaining[0]); result.Add(remaining[i]); result.Add(remaining[i + 1]);
                    }
                    return result;
                }
            }
            result.AddRange(remaining);
            return result;
        }

        /// <summary>A bar of a given width between two points in a plane, given thickness as <see cref="Prism"/> does.</summary>
        public void Stroke(Vector3 colour, Vector3 origin, Vector3 u, Vector3 v, Vector3 n, Vector2 a, Vector2 b, float width, float near, float far)
        {
            Vector2 along = b - a;
            if (along.LengthSquared() < 1e-12f)
                return;
            Vector2 side = Vector2.Normalize(new Vector2(-along.Y, along.X)) * (width / 2);
            Prism(colour, origin, u, v, n, new[] { a - side, b - side, b + side, a + side }, near, far);
        }

        /// <summary>A regular polygon round a point, for discs and rounded ends.</summary>
        public static Vector2[] Polygon(Vector2 centre, float radius, int sides, float startDegrees = 0)
        {
            Vector2[] points = new Vector2[sides];
            for (int i = 0; i < sides; i++)
            {
                double a = (startDegrees + 360.0 * i / sides) * Math.PI / 180.0;
                points[i] = centre + new Vector2((float)Math.Cos(a), (float)Math.Sin(a)) * radius;
            }
            return points;
        }

        /// <summary>A flat ring (annulus) in a plane, its faces made of quads round it, given thickness.</summary>
        public void Annulus(Vector3 colour, Vector3 origin, Vector3 u, Vector3 v, Vector3 n, Vector2 centre, float outer, float inner, int sides, float near, float far, float startDegrees = 0)
        {
            Vector2[] o = Polygon(centre, outer, sides, startDegrees), i = Polygon(centre, inner, sides, startDegrees);
            for (int k = 0; k < sides; k++)
                Prism(colour, origin, u, v, n, new[] { i[k], o[k], o[(k + 1) % sides], i[(k + 1) % sides] }, near, far);
        }

        /// <summary>A Y-up cylinder (or cone, or frustum) standing on <paramref name="baseCentre"/>.</summary>
        public void Cylinder(Vector3 colour, Vector3 baseCentre, float bottomRadiusX, float bottomRadiusZ, float topRadiusX, float topRadiusZ, float height, int sides)
        {
            Surface s = In(colour);
            Vector3 Ring(int i, bool top)
            {
                double a = 2 * Math.PI * i / sides;
                float rx = top ? topRadiusX : bottomRadiusX, rz = top ? topRadiusZ : bottomRadiusZ;
                return baseCentre + new Vector3((float)Math.Cos(a) * rx, top ? height : 0, (float)Math.Sin(a) * rz);
            }
            int bottomCentre = s.Vertices.Count;
            s.Vertices.Add(baseCentre);
            int topCentre = s.Vertices.Count;
            s.Vertices.Add(baseCentre + new Vector3(0, height, 0));
            for (int i = 0; i < sides; i++)
            {
                Vector3 b0 = Ring(i, false), b1 = Ring(i + 1, false), t0 = Ring(i, true), t1 = Ring(i + 1, true);
                Quad(s, b0, t0, t1, b1);
                int start = s.Vertices.Count;
                s.Vertices.Add(b0); s.Vertices.Add(b1); s.Vertices.Add(t0); s.Vertices.Add(t1);
                s.Indices.Add(bottomCentre); s.Indices.Add(start); s.Indices.Add(start + 1);
                s.Indices.Add(topCentre); s.Indices.Add(start + 3); s.Indices.Add(start + 2);
            }
        }

        /// <summary>A UV sphere (ellipsoid) round a centre.</summary>
        public void Sphere(Vector3 colour, Vector3 centre, Vector3 radii, int segments = 16, int rings = 10)
        {
            Surface s = In(colour);
            int start = s.Vertices.Count;
            for (int r = 0; r <= rings; r++)
            {
                double phi = Math.PI * r / rings;
                for (int g = 0; g <= segments; g++)
                {
                    double theta = 2 * Math.PI * g / segments;
                    s.Vertices.Add(centre + new Vector3((float)(Math.Sin(phi) * Math.Cos(theta)) * radii.X, (float)Math.Cos(phi) * radii.Y, (float)(Math.Sin(phi) * Math.Sin(theta)) * radii.Z));
                }
            }
            for (int r = 0; r < rings; r++)
                for (int g = 0; g < segments; g++)
                {
                    int a = start + r * (segments + 1) + g, b = a + segments + 1;
                    s.Indices.Add(a); s.Indices.Add(b); s.Indices.Add(a + 1);
                    s.Indices.Add(a + 1); s.Indices.Add(b); s.Indices.Add(b + 1);
                }
        }
    }
}

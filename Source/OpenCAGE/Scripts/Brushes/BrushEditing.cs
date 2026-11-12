using CATHODE.Scripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.Brushwork
{
    /// <summary>Changes to a brush's shape as the tools make them (pure: they change a definition, nothing else).</summary>
    public static class BrushEditing
    {
        /// <summary>
        /// Push a box face out along its normal by <paramref name="by"/> metres (negative pulls it in), as dragging its handle does;
        /// with <paramref name="snap"/> &gt; 0 the face lands on that grid. Never past the opposite face.
        /// </summary>
        public static void MoveFace(BrushDefinition brush, int face, float by, float snap)
        {
            Vector3 min = brush.Min, max = brush.Max;
            float m = BrushDefinition.MinimumSize;
            switch (face)
            {
                case 0: max.X = Math.Max(min.X + m, Snap(max.X + by, snap)); break;
                case 1: min.X = Math.Min(max.X - m, Snap(min.X - by, snap)); break;
                case 2: max.Y = Math.Max(min.Y + m, Snap(max.Y + by, snap)); break;
                case 3: min.Y = Math.Min(max.Y - m, Snap(min.Y - by, snap)); break;
                case 4: max.Z = Math.Max(min.Z + m, Snap(max.Z + by, snap)); break;
                case 5: min.Z = Math.Min(max.Z - m, Snap(min.Z - by, snap)); break;
                default: throw new ArgumentOutOfRangeException(nameof(face));
            }
            brush.Min = min;
            brush.Max = max;
        }

        /// <summary>Set one box face's plane outright (its coordinate in the brush's space), as a handle drag ends.</summary>
        public static void SetFace(BrushDefinition brush, int face, float at)
        {
            float current = FaceCoordinate(brush, face);
            float by = (face % 2 == 0) ? at - current : current - at;
            MoveFace(brush, face, by, 0);
        }

        /// <summary>A box face's coordinate along its axis, in the brush's space.</summary>
        public static float FaceCoordinate(BrushDefinition brush, int face)
        {
            switch (face)
            {
                case 0: return brush.Max.X;
                case 1: return brush.Min.X;
                case 2: return brush.Max.Y;
                case 3: return brush.Min.Y;
                case 4: return brush.Max.Z;
                case 5: return brush.Min.Z;
            }
            throw new ArgumentOutOfRangeException(nameof(face));
        }

        private static float Snap(float value, float snap) => snap > 0 ? (float)(Math.Round(value / snap) * snap) : value;

        /// <summary>Where the world's origin is in the space of a brush placed at <paramref name="world"/>: its texture anchor for world alignment.</summary>
        public static Vector3 WorldOriginInBrushSpace(cTransform world)
        {
            if (world == null) return Vector3.Zero;
            return InstanceTransform.PointToLocal(world, Vector3.Zero);
        }

        #region Geometry editing (a brush as an editable mesh)
        /// <summary>
        /// The brush as an editable mesh: its faces as polygons over shared vertices, each keeping its texture settings. A box becomes
        /// 8 vertices and 6 faces. A mesh brush is returned as a copy. From here on vertex moves, scaling, clipping and extrusion apply.
        /// </summary>
        public static BrushDefinition ToMesh(BrushDefinition brush)
        {
            BrushDefinition result = brush.Clone();
            if (brush.Kind == BrushKind.Mesh)
                return result;
            BrushGeometry geometry = BrushGeometry.Build(brush);
            BrushMesh mesh = new BrushMesh();
            Dictionary<(long, long, long), int> welded = new Dictionary<(long, long, long), int>();
            foreach (BrushGeometry.Polygon polygon in geometry.Polygons)
            {
                BrushMeshFace face = new BrushMeshFace() { Texture = polygon.Texture?.Clone() ?? new BrushFace() };
                foreach (Vector3 point in polygon.Points)
                    face.Vertices.Add(Weld(mesh, welded, point));
                mesh.Faces.Add(face);
            }
            result.Kind = BrushKind.Mesh;
            result.Mesh = mesh;
            result.Faces = new List<BrushFace>();
            result.DoubleSided = false;
            UpdateBounds(result);
            return result;
        }

        private static int Weld(BrushMesh mesh, Dictionary<(long, long, long), int> welded, Vector3 point)
        {
            (long, long, long) key = ((long)Math.Round(point.X * 10000), (long)Math.Round(point.Y * 10000), (long)Math.Round(point.Z * 10000));
            if (!welded.TryGetValue(key, out int at))
            {
                at = mesh.Vertices.Count;
                mesh.Vertices.Add(point);
                welded[key] = at;
            }
            return at;
        }

        /// <summary>A mesh brush's bounds made its vertices' again (they are what describes it then).</summary>
        public static void UpdateBounds(BrushDefinition brush)
        {
            if (brush.Mesh == null || brush.Mesh.Vertices.Count == 0) return;
            Vector3 min = brush.Mesh.Vertices[0], max = brush.Mesh.Vertices[0];
            foreach (Vector3 v in brush.Mesh.Vertices)
            {
                min = Vector3.Min(min, v);
                max = Vector3.Max(max, v);
            }
            brush.Min = min;
            brush.Max = max;
        }

        private static BrushMesh RequireMesh(BrushDefinition brush)
        {
            if (brush.Kind != BrushKind.Mesh || brush.Mesh == null)
                throw new BrushException("That is a " + brush.Kind.ToString().ToLowerInvariant() + " brush: make it a mesh first (to_mesh).");
            return brush.Mesh;
        }

        /// <summary>Move some of a mesh brush's vertices by <paramref name="delta"/> (its own space).</summary>
        public static void MoveVertices(BrushDefinition brush, IEnumerable<int> vertices, Vector3 delta)
        {
            BrushMesh mesh = RequireMesh(brush);
            foreach (int v in vertices.Distinct())
            {
                if (v < 0 || v >= mesh.Vertices.Count) throw new BrushException("The mesh has no vertex " + v + " (it has " + mesh.Vertices.Count + ").");
                mesh.Vertices[v] += delta;
            }
            UpdateBounds(brush);
        }

        /// <summary>Scale a mesh brush about <paramref name="pivot"/> (its own space). A mirroring scale turns its faces round so they still face out.</summary>
        public static void Scale(BrushDefinition brush, Vector3 factor, Vector3 pivot)
        {
            BrushMesh mesh = RequireMesh(brush);
            if (factor.X == 0 || factor.Y == 0 || factor.Z == 0) throw new BrushException("A scale of 0 flattens the brush.");
            for (int i = 0; i < mesh.Vertices.Count; i++)
                mesh.Vertices[i] = pivot + (mesh.Vertices[i] - pivot) * factor;
            int negatives = (factor.X < 0 ? 1 : 0) + (factor.Y < 0 ? 1 : 0) + (factor.Z < 0 ? 1 : 0);
            if (negatives % 2 == 1)
                foreach (BrushMeshFace face in mesh.Faces) face.Vertices.Reverse();
            UpdateBounds(brush);
        }

        /// <summary>
        /// Push a face of a mesh brush out along its normal by <paramref name="distance"/> metres (Hammer's extrude): its vertices are
        /// copied out that far and the face moves to them, with a side face joining each of its edges to its old place.
        /// </summary>
        public static void Extrude(BrushDefinition brush, int faceIndex, float distance)
        {
            BrushMesh mesh = RequireMesh(brush);
            if (faceIndex < 0 || faceIndex >= mesh.Faces.Count) throw new BrushException("The mesh has no face " + faceIndex + " (it has " + mesh.Faces.Count + ").");
            if (Math.Abs(distance) < BrushDefinition.MinimumSize) throw new BrushException("Extrude by at least " + BrushDefinition.MinimumSize + " m.");
            BrushMeshFace face = mesh.Faces[faceIndex];
            Vector3 normal = Vector3.Normalize(BrushGeometry.NewellNormal(face.Vertices.Select(o => mesh.Vertices[o]).ToList()));
            List<int> moved = new List<int>();
            foreach (int v in face.Vertices)
            {
                moved.Add(mesh.Vertices.Count);
                mesh.Vertices.Add(mesh.Vertices[v] + normal * distance);
            }
            int count = face.Vertices.Count;
            for (int i = 0; i < count; i++)
            {
                int a = face.Vertices[i], b = face.Vertices[(i + 1) % count];
                int a2 = moved[i], b2 = moved[(i + 1) % count];
                //[a, b, b', a'] faces out for an outward extrude (its normal is cross(b - a, N)); inward, the other way
                List<int> side = distance > 0 ? new List<int> { a, b, b2, a2 } : new List<int> { a2, b2, b, a };
                mesh.Faces.Add(new BrushMeshFace() { Vertices = side, Texture = face.Texture.Clone() });
            }
            face.Vertices = moved;
            UpdateBounds(brush);
        }

        /// <summary>
        /// Cut a mesh brush with a plane (Hammer's clip tool): what is on the <paramref name="normal"/> side of the plane through
        /// <paramref name="point"/> goes (keepFront: the other side goes), and the cut is closed with a new face. Faces need to be
        /// convex for the cap to come out right, as a box's and anything clipped from one are.
        /// </summary>
        public static void Clip(BrushDefinition brush, Vector3 point, Vector3 normal, bool keepFront)
        {
            BrushMesh mesh = RequireMesh(brush);
            if (normal.LengthSquared() < 1e-10f) throw new BrushException("The clip plane needs a normal.");
            Vector3 n = Vector3.Normalize(keepFront ? -normal : normal);
            const float epsilon = 1e-4f;
            float Side(Vector3 v) => Vector3.Dot(v - point, n);

            List<(List<Vector3> points, BrushFace texture)> kept = new List<(List<Vector3>, BrushFace)>();
            List<Vector3> cut = new List<Vector3>();
            foreach (BrushMeshFace face in mesh.Faces)
            {
                List<Vector3> polygon = face.Vertices.Select(o => mesh.Vertices[o]).ToList();
                List<Vector3> output = new List<Vector3>();
                for (int i = 0; i < polygon.Count; i++)
                {
                    Vector3 a = polygon[i], b = polygon[(i + 1) % polygon.Count];
                    float sa = Side(a), sb = Side(b);
                    if (sa <= epsilon) output.Add(a);
                    if (sa <= epsilon && sb <= epsilon) { if (Math.Abs(sa) <= epsilon) cut.Add(a); continue; }
                    if ((sa < -epsilon && sb > epsilon) || (sa > epsilon && sb < -epsilon))
                    {
                        Vector3 x = a + (b - a) * (sa / (sa - sb));
                        output.Add(x);
                        cut.Add(x);
                    }
                    else if (Math.Abs(sa) <= epsilon)
                        cut.Add(a);
                }
                if (output.Count >= 3)
                    kept.Add((output, face.Texture));
            }
            if (kept.Count == mesh.Faces.Count && cut.Count < 3)
                throw new BrushException("The plane does not cut the brush.");
            if (kept.Count == 0)
                throw new BrushException("That would clip the whole brush away.");

            //The cap: the cut's points, round their centre, facing along the plane's normal (out of what is kept)
            List<Vector3> cap = new List<Vector3>();
            foreach (Vector3 c in cut)
                if (!cap.Any(o => (o - c).LengthSquared() < 1e-8f)) cap.Add(c);
            BrushMesh result = new BrushMesh();
            Dictionary<(long, long, long), int> welded = new Dictionary<(long, long, long), int>();
            foreach ((List<Vector3> points, BrushFace texture) in kept)
                result.Faces.Add(new BrushMeshFace() { Vertices = points.Select(o => Weld(result, welded, o)).ToList(), Texture = texture.Clone() });
            if (cap.Count >= 3)
            {
                Vector3 centre = cap.Aggregate(Vector3.Zero, (s, o) => s + o) / cap.Count;
                Vector3 u = Vector3.Normalize(cap[0] - centre), w = Vector3.Cross(n, u);
                cap = cap.OrderBy(o => Math.Atan2(Vector3.Dot(o - centre, w), Vector3.Dot(o - centre, u))).ToList();
                if (Vector3.Dot(BrushGeometry.NewellNormal(cap), n) < 0) cap.Reverse();
                result.Faces.Add(new BrushMeshFace() { Vertices = cap.Select(o => Weld(result, welded, o)).ToList(), Texture = kept[0].texture.Clone() });
            }
            brush.Mesh = result;
            UpdateBounds(brush);
        }
        #endregion

        #region Checking a shape
        //How far a vertex may sit off its face's plane, or in front of another face's, before the shape is called bent or dented
        private const float ShapeTolerance = 0.01f;

        /// <summary>
        /// What is wrong with a mesh brush's shape after an edit, in words (empty when nothing): faces no longer flat (a vertex moved
        /// off the plane of the others - drawn as a fan, so it creases), and a solid no longer convex (a vertex in front of a face's
        /// plane - its collision still follows the triangles, but clipping and extruding expect a convex solid). It is still built.
        /// </summary>
        public static List<string> ShapeWarnings(BrushDefinition brush)
        {
            List<string> warnings = new List<string>();
            BrushMesh mesh = brush?.Mesh;
            if (brush == null || brush.Kind != BrushKind.Mesh || mesh == null) return warnings;
            List<int> bent = new List<int>();
            List<(Vector3 normal, Vector3 point, HashSet<int> own)> planes = new List<(Vector3, Vector3, HashSet<int>)>();
            for (int f = 0; f < mesh.Faces.Count; f++)
            {
                List<Vector3> points = mesh.Faces[f].Vertices.Where(o => o >= 0 && o < mesh.Vertices.Count).Select(o => mesh.Vertices[o]).ToList();
                if (points.Count < 3) continue;
                Vector3 normal = BrushGeometry.NewellNormal(points);
                if (normal.LengthSquared() < 1e-12f) continue;
                normal = Vector3.Normalize(normal);
                Vector3 centre = points.Aggregate(Vector3.Zero, (a, b) => a + b) / points.Count;
                if (points.Any(p => Math.Abs(Vector3.Dot(p - centre, normal)) > ShapeTolerance)) bent.Add(f);
                planes.Add((normal, centre, new HashSet<int>(mesh.Faces[f].Vertices)));
            }
            if (bent.Count != 0)
                warnings.Add("Face" + (bent.Count == 1 ? " " : "s ") + string.Join(", ", bent.Take(8)) + (bent.Count > 8 ? "..." : "") + (bent.Count == 1 ? " is" : " are") +
                    " no longer flat (a vertex is off the plane of the others): drawn as a fan of triangles, so it shows a crease. Move the face's other vertices with it to keep it flat.");
            //A face's own vertices are its flatness, not the solid's convexity
            bool dented = Enumerable.Range(0, mesh.Vertices.Count).Any(v => planes.Any(p => !p.own.Contains(v) && Vector3.Dot(mesh.Vertices[v] - p.point, p.normal) > ShapeTolerance));
            if (dented)
                warnings.Add("The solid is no longer convex (a vertex sits in front of another face): it is drawn and collides as its triangles, but clip and extrude expect a convex solid.");
            return warnings;
        }
        #endregion
    }
}

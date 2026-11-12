using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.Brushwork
{
    /// <summary>What a brush is shaped as. Stored by name in the level, so names never change.</summary>
    public enum BrushKind
    {
        Box,
        Plane,
        /// <summary>An editable polyhedron: vertices and faces of its own (a primitive becomes one once edited at that level).</summary>
        Mesh,
    }

    /// <summary>How one face of a brush is textured.</summary>
    public sealed class BrushFace
    {
        /// <summary>The level material it draws with; null for the brush default (<see cref="BrushMaterials.GridName"/>).</summary>
        public string Material;
        /// <summary>Texture repeats are this many times larger (u, v): 1 = one repeat per <see cref="BrushDefinition.MetresPerRepeat"/>.</summary>
        public Vector2 Scale = Vector2.One;
        /// <summary>Shift, in repeats (u, v).</summary>
        public Vector2 Offset = Vector2.Zero;
        /// <summary>Turn of the texture on the face, degrees.</summary>
        public float Rotation;

        public BrushFace Clone() => new BrushFace() { Material = Material, Scale = Scale, Offset = Offset, Rotation = Rotation };

        public bool SameAs(BrushFace other) => other != null && string.Equals(Material ?? "", other.Material ?? "", StringComparison.Ordinal) && Scale == other.Scale && Offset == other.Offset && Rotation == other.Rotation;
    }

    /// <summary>An editable polyhedron's face: a convex or concave polygon over <see cref="BrushMesh.Vertices"/>, wound so cross(b-a, c-a) points out.</summary>
    public sealed class BrushMeshFace
    {
        public List<int> Vertices = new List<int>();
        public BrushFace Texture = new BrushFace();
    }

    /// <summary>The vertices and faces of a <see cref="BrushKind.Mesh"/> brush, in its own space.</summary>
    public sealed class BrushMesh
    {
        public List<Vector3> Vertices = new List<Vector3>();
        public List<BrushMeshFace> Faces = new List<BrushMeshFace>();

        public BrushMesh Clone() => new BrushMesh()
        {
            Vertices = new List<Vector3>(Vertices),
            Faces = Faces.Select(o => new BrushMeshFace() { Vertices = new List<int>(o.Vertices), Texture = o.Texture.Clone() }).ToList(),
        };
    }

    /// <summary>
    /// A brush as the editor keeps it (in the level's BRUSHES custom table, as JSON): a box or plane in the space of the
    /// ModelReference entity that draws it, how each face is textured, and the collision it gets. Everything drawn and collided
    /// with is generated from this (<see cref="BrushGeometry"/>, <see cref="BrushModelBuilder"/>, <see cref="BrushCollision"/>).
    /// </summary>
    public sealed class BrushDefinition
    {
        public const int CurrentVersion = 1;

        /// <summary>One texture repeat across this many metres at scale 1 - the grid's own density (a line every metre).</summary>
        public const float MetresPerRepeat = 2.0f;

        /// <summary>Smallest size along an axis a brush may have, metres.</summary>
        public const float MinimumSize = 0.01f;

        public int Version = CurrentVersion;
        public BrushKind Kind = BrushKind.Box;

        /// <summary>The box (or, for a plane, the rectangle at Min.Y facing up) in the entity's own space.</summary>
        public Vector3 Min = new Vector3(-1, 0, -1);
        public Vector3 Max = new Vector3(1, 2, 1);

        /// <summary>A plane also drawn (and collided with) from below.</summary>
        public bool DoubleSided;

        /// <summary>Where the texture projection is anchored, in the entity's space. A new brush takes the world origin, so grids line up across brushes.</summary>
        public Vector3 UvOrigin = Vector3.Zero;

        /// <summary>Box: +X, -X, +Y, -Y, +Z, -Z. Plane: top, bottom. Mesh: unused (each mesh face carries its own).</summary>
        public List<BrushFace> Faces = new List<BrushFace>();

        /// <summary>solid (WORLD + BALLISTIC pair, walkable), world, ballistic or none.</summary>
        public string Collision = "solid";

        /// <summary>The model it was last built into (the level's model name).</summary>
        public string Model;

        /// <summary>The collision proxies its rows were made with (-1 none), and the triangles they were made from (a hash), so an edit that does not move the triangles keeps them.</summary>
        public int WorldProxy = -1, BallisticProxy = -1;
        public string CollisionHash;
        /// <summary>Proxies an edit replaced: nothing of this brush names them any more (an undo can bring them back until the history moves on).</summary>
        public List<int> SupersededProxies = new List<int>();

        /// <summary>For <see cref="BrushKind.Mesh"/>.</summary>
        public BrushMesh Mesh;

        public static readonly string[] BoxFaceNames = { "+x", "-x", "+y", "-y", "+z", "-z" };
        public static readonly string[] PlaneFaceNames = { "top", "bottom" };

        public static BrushDefinition NewBox(Vector3 min, Vector3 max, string material)
        {
            BrushDefinition brush = new BrushDefinition() { Kind = BrushKind.Box, Min = Vector3.Min(min, max), Max = Vector3.Max(min, max) };
            for (int i = 0; i < 6; i++) brush.Faces.Add(new BrushFace() { Material = material });
            return brush;
        }

        public static BrushDefinition NewPlane(Vector3 min, Vector3 max, string material, bool doubleSided)
        {
            BrushDefinition brush = new BrushDefinition() { Kind = BrushKind.Plane, Min = Vector3.Min(min, max), Max = Vector3.Max(min, max), DoubleSided = doubleSided };
            brush.Max = new Vector3(brush.Max.X, brush.Min.Y, brush.Max.Z);
            for (int i = 0; i < 2; i++) brush.Faces.Add(new BrushFace() { Material = material });
            return brush;
        }

        /// <summary>The names faces go by for this kind of brush, in order.</summary>
        public string[] FaceNames => Kind == BrushKind.Plane ? PlaneFaceNames : Kind == BrushKind.Box ? BoxFaceNames : Enumerable.Range(0, Mesh?.Faces.Count ?? 0).Select(o => o.ToString(CultureInfo.InvariantCulture)).ToArray();

        /// <summary>The faces whose texture settings apply, in order: the list, or each mesh face's own.</summary>
        public List<BrushFace> FaceTextures => Kind == BrushKind.Mesh ? (Mesh?.Faces.Select(o => o.Texture).ToList() ?? new List<BrushFace>()) : Faces;

        public Vector3 Size => Max - Min;

        /// <summary>What is wrong with it, or null.</summary>
        public string Problem()
        {
            if (Kind == BrushKind.Mesh)
            {
                if (Mesh == null || Mesh.Vertices.Count < 3 || Mesh.Faces.Count == 0) return "The mesh has no faces.";
                foreach (BrushMeshFace face in Mesh.Faces)
                {
                    if (face.Vertices.Count < 3) return "A mesh face has fewer than 3 vertices.";
                    if (face.Vertices.Any(o => o < 0 || o >= Mesh.Vertices.Count)) return "A mesh face names a vertex the mesh does not have.";
                }
                return null;
            }
            Vector3 size = Size;
            if (!IsFinite(Min) || !IsFinite(Max)) return "The size is not a number.";
            if (size.X < MinimumSize || size.Z < MinimumSize || (Kind == BrushKind.Box && size.Y < MinimumSize))
                return "A brush has to be at least " + MinimumSize + " m along each side (it is " + Describe(size) + ").";
            if (Math.Max(size.X, Math.Max(size.Y, size.Z)) > 4096)
                return "A brush can be at most 4096 m along a side.";
            int faces = Kind == BrushKind.Box ? 6 : 2;
            if (Faces.Count != faces) return "A " + Kind.ToString().ToLowerInvariant() + " has " + faces + " faces, not " + Faces.Count + ".";
            foreach (BrushFace face in Faces)
                if (!(Math.Abs(face.Scale.X) > 1e-4f) || !(Math.Abs(face.Scale.Y) > 1e-4f) || !IsFinite(new Vector3(face.Scale, face.Rotation)) || !IsFinite(new Vector3(face.Offset, 0)))
                    return "A face's texture scale has to be a number other than 0.";
            return null;
        }

        private static bool IsFinite(Vector3 v) => !(float.IsNaN(v.X) || float.IsNaN(v.Y) || float.IsNaN(v.Z) || float.IsInfinity(v.X) || float.IsInfinity(v.Y) || float.IsInfinity(v.Z));

        public static string Describe(Vector3 v) => v.X.ToString("0.###", CultureInfo.InvariantCulture) + " x " + v.Y.ToString("0.###", CultureInfo.InvariantCulture) + " x " + v.Z.ToString("0.###", CultureInfo.InvariantCulture);

        public BrushDefinition Clone()
        {
            BrushDefinition copy = (BrushDefinition)MemberwiseClone();
            copy.Faces = Faces.Select(o => o.Clone()).ToList();
            copy.SupersededProxies = new List<int>(SupersededProxies);
            copy.Mesh = Mesh?.Clone();
            return copy;
        }

        #region JSON
        public string ToJson()
        {
            JObject o = new JObject()
            {
                ["v"] = Version,
                ["kind"] = Kind.ToString().ToLowerInvariant(),
                ["min"] = Vec(Min),
                ["max"] = Vec(Max),
                ["uv_origin"] = Vec(UvOrigin),
                ["collision"] = Collision ?? "solid",
            };
            if (Kind == BrushKind.Plane) o["double_sided"] = DoubleSided;
            if (Kind != BrushKind.Mesh) o["faces"] = new JArray(Faces.Select(FaceJson));
            if (Model != null) o["model"] = Model;
            if (WorldProxy >= 0) o["world_proxy"] = WorldProxy;
            if (BallisticProxy >= 0) o["ballistic_proxy"] = BallisticProxy;
            if (CollisionHash != null) o["collision_hash"] = CollisionHash;
            if (SupersededProxies.Count != 0) o["superseded_proxies"] = new JArray(SupersededProxies);
            if (Mesh != null)
            {
                o["mesh"] = new JObject()
                {
                    ["vertices"] = new JArray(Mesh.Vertices.Select(Vec)),
                    ["faces"] = new JArray(Mesh.Faces.Select(f =>
                    {
                        JObject face = FaceJson(f.Texture);
                        face["vertices"] = new JArray(f.Vertices);
                        return face;
                    })),
                };
            }
            return o.ToString(Formatting.None);
        }

        public static BrushDefinition FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            JObject o;
            try { o = JObject.Parse(json); }
            catch (JsonException) { return null; }

            BrushDefinition brush = new BrushDefinition();
            brush.Version = (int?)o["v"] ?? CurrentVersion;
            string kind = (string)o["kind"] ?? "box";
            if (!Enum.TryParse(kind, true, out brush.Kind)) return null;
            brush.Min = ReadVec(o["min"], brush.Min);
            brush.Max = ReadVec(o["max"], brush.Max);
            brush.UvOrigin = ReadVec(o["uv_origin"], Vector3.Zero);
            brush.DoubleSided = (bool?)o["double_sided"] ?? false;
            brush.Collision = (string)o["collision"] ?? "solid";
            brush.Model = (string)o["model"];
            brush.WorldProxy = (int?)o["world_proxy"] ?? -1;
            brush.BallisticProxy = (int?)o["ballistic_proxy"] ?? -1;
            brush.CollisionHash = (string)o["collision_hash"];
            if (o["superseded_proxies"] is JArray superseded) brush.SupersededProxies = superseded.Select(t => (int)t).ToList();
            if (o["faces"] is JArray faces) brush.Faces = faces.OfType<JObject>().Select(ReadFace).ToList();
            if (o["mesh"] is JObject mesh)
            {
                brush.Mesh = new BrushMesh();
                if (mesh["vertices"] is JArray vertices) brush.Mesh.Vertices = vertices.Select(t => ReadVec(t, Vector3.Zero)).ToList();
                if (mesh["faces"] is JArray meshFaces)
                    brush.Mesh.Faces = meshFaces.OfType<JObject>().Select(f => new BrushMeshFace()
                    {
                        Vertices = (f["vertices"] as JArray)?.Select(t => (int)t).ToList() ?? new List<int>(),
                        Texture = ReadFace(f),
                    }).ToList();
            }
            return brush;
        }

        private static JObject FaceJson(BrushFace face)
        {
            JObject o = new JObject();
            if (face.Material != null) o["material"] = face.Material;
            if (face.Scale != Vector2.One) o["scale"] = new JArray(face.Scale.X, face.Scale.Y);
            if (face.Offset != Vector2.Zero) o["offset"] = new JArray(face.Offset.X, face.Offset.Y);
            if (face.Rotation != 0) o["rotation"] = face.Rotation;
            return o;
        }

        private static BrushFace ReadFace(JObject o)
        {
            BrushFace face = new BrushFace() { Material = (string)o["material"] };
            if (o["scale"] is JArray scale && scale.Count >= 2) face.Scale = new Vector2((float)scale[0], (float)scale[1]);
            if (o["offset"] is JArray offset && offset.Count >= 2) face.Offset = new Vector2((float)offset[0], (float)offset[1]);
            face.Rotation = (float?)o["rotation"] ?? 0;
            return face;
        }

        private static JArray Vec(Vector3 v) => new JArray(Round(v.X), Round(v.Y), Round(v.Z));
        private static double Round(float f) => Math.Round(f, 5);

        private static Vector3 ReadVec(JToken token, Vector3 fallback)
        {
            if (!(token is JArray a) || a.Count < 3) return fallback;
            return new Vector3((float)a[0], (float)a[1], (float)a[2]);
        }
        #endregion
    }
}

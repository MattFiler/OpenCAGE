using AlienPAK;
using CATHODE;
using CathodeLib;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Matrix4x4 = System.Numerics.Matrix4x4;
using Vector3 = System.Numerics.Vector3;

namespace OpenCAGE.Popups.UserControls
{
    /// <summary>
    /// Plays an animation on a skinned mesh. The skinning runs on the CPU because HelixToolkit's WPF
    /// renderer has no shader to hand it to - which is fine at preview rates, since a character LOD0
    /// is a few tens of thousands of vertices and we only ever pose one at a time.
    /// </summary>
    public partial class GUI_AnimationViewer : UserControl
    {
        /// <summary>What stopped the preview working, or null if it did. Set by <see cref="SetModel"/>.</summary>
        public string Problem { get; private set; }

        /// <summary>Whether to draw the rig on top of the mesh.</summary>
        public bool ShowBones
        {
            get { return _showBones; }
            set { _showBones = value; Refresh(); }
        }

        /// <summary>
        /// Whether to let the clip's root bone move the character. Off by default: the root is a
        /// motion extraction bone the engine reads to move the entity, not the mesh.
        /// </summary>
        public CathodeLib.Animation.RootMotion RootMotion
        {
            get { return _rootMotion; }
            set { _rootMotion = value; Refresh(); }
        }

        /// <summary>Every part of the model that can be posed, so the caller can offer them as filters.</summary>
        public IReadOnlyList<Part> Parts { get { return _submeshes; } }

        private readonly Model3DGroup _meshes = new Model3DGroup();
        private readonly Model3DGroup _bones = new Model3DGroup();
        private readonly List<SkinnedSubmesh> _submeshes = new List<SkinnedSubmesh>();

        private Skeleton _skeleton;
        private CathodeLib.Animation.ClipReference _clip;
        private bool _showBones = true;
        private CathodeLib.Animation.RootMotion _rootMotion = CathodeLib.Animation.RootMotion.Ignore;
        private int _frame;

        /// <summary>One nameable piece of the model, which can be shown or hidden on its own.</summary>
        public interface Part
        {
            string Name { get; }
            int VertexCount { get; }
            bool Visible { get; set; }

            /// <summary>Whether this looks like a collision hull rather than something to look at.</summary>
            bool IsCollision { get; }

            /// <summary>The LOD this belongs to, named as the model names it - "JACKET", "HEAD".</summary>
            string Group { get; }

            /// <summary>Which LOD of its component, so callers can hide everything below the first.</summary>
            int Lod { get; }

            /// <summary>Where the group sits in the model, so groups can be listed in model order.</summary>
            int GroupOrder { get; }
        }

        public GUI_AnimationViewer()
        {
            InitializeComponent();
            posedModel.Content = _meshes;
            posedSkeleton.Content = _bones;
        }

        /// <summary>
        /// Bind a model to a skeleton, ready to be posed. Pass a null model to preview the rig alone,
        /// which is what an environment animation usually wants.
        /// </summary>
        public void SetModel(Models.CS2 cs2, Skeleton skeleton, bool useMaterials)
        {
            _meshes.Children.Clear();
            _submeshes.Clear();
            _skeleton = skeleton;
            Problem = null;

            if (skeleton == null)
            {
                Problem = "No skeleton has been chosen, so there's nothing to pose.";
                Refresh();
                return;
            }

            int skipped = 0, highest = -1, group = 0;
            if (cs2 != null)
            {
                for (int c = 0; c < cs2.Components.Count; c++)
                {
                    Models.CS2.Component component = cs2.Components[c];

                    /* Every LOD, grouped the way the model browser groups them - one group per LOD,
                     * named as the model names it, with everything below the first LOD switched off. */
                    for (int l = 0; l < component.LODs.Count; l++, group++)
                    {
                        Models.CS2.Component.LOD lod = component.LODs[l];
                        string part = LastSegment(lod.Name);

                        for (int s = 0; s < lod.Submeshes.Count; s++)
                        {
                            Models.CS2.Component.LOD.Submesh submesh = lod.Submeshes[s];
                            if (l == 0)
                                foreach (int bone in submesh.Bones)
                                    if (bone > highest) highest = bone;

                            SkinnedSubmesh skinned = SkinnedSubmesh.Build(submesh, skeleton, useMaterials,
                                "Submesh " + s, part, l, group);
                            if (skinned == null) { if (l == 0) skipped++; continue; }

                            /* A character's collision hull is bound to the same rig and sits right
                             * on top of the body, so it hides everything. Start with it off. */
                            skinned.Visible = l == 0 && !skinned.IsCollision;
                            _submeshes.Add(skinned);
                            if (skinned.Visible) _meshes.Children.Add(skinned.Model);
                        }
                    }
                }
            }

            if (cs2 != null && _submeshes.Count == 0)
                Problem = skipped == 0
                    ? "'" + cs2.Name + "' has no geometry to show."
                    : "'" + cs2.Name + "' isn't skinned to a skeleton, so an animation has nothing to move.";
            else if (highest >= skeleton.Bones.Count)
                Problem = "'" + cs2.Name + "' is skinned to " + (highest + 1) + " bones, but '" + skeleton.Name
                        + "' only has " + skeleton.Bones.Count + ". Parts of the mesh will stay where they are.";

            Refresh();
        }

        /// <summary>The clip to pose with. Pass null to go back to the bind pose.</summary>
        public void SetClip(CathodeLib.Animation.ClipReference clip)
        {
            _clip = clip;
            _frame = 0;
            Refresh();
        }

        /// <summary>Pose everything at one frame of the clip.</summary>
        public void ShowFrame(int frame)
        {
            _frame = frame;
            Refresh();
        }

        /// <summary>Frame the model in the viewport. Worth doing once, not every frame.</summary>
        public void ResetCamera()
        {
            myView.ModelUpDirection = new Vector3D(0, 1, 0);
            myView.Camera.UpDirection = new Vector3D(0, 1, 0);
            myView.Camera.LookDirection = new Vector3D(-0.5, -0.5, -1.0);
            myView.ZoomExtents();
        }

        private static string LastSegment(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            return path.Substring(path.LastIndexOfAny(new[] { '\\', '/' }) + 1);
        }

        private void Refresh()
        {
            if (_skeleton == null) return;

            /* Skinning wants inverse-bind-times-animated; the rig overlay wants the animated pose on
             * its own. Both fall out of the same sample, so take it once. */
            List<Matrix4x4> animated = CathodeLib.Animation.SampleModelPose(_clip, _skeleton, _frame, _rootMotion);
            if (animated == null) return;

            List<Matrix4x4> bind = _skeleton.GetBindPose();
            Matrix4x4[] skinning = new Matrix4x4[animated.Count];
            for (int i = 0; i < animated.Count; i++)
                skinning[i] = Matrix4x4.Invert(bind[i], out Matrix4x4 inverse) ? inverse * animated[i] : Matrix4x4.Identity;

            for (int i = 0; i < _submeshes.Count; i++)
                if (_submeshes[i].Visible) _submeshes[i].Pose(skinning);
            PoseBones(animated);
        }

        /// <summary>Show or hide one part of the model without rebuilding the rest.</summary>
        public void SetPartVisible(Part part, bool visible)
        {
            SkinnedSubmesh submesh = part as SkinnedSubmesh;
            if (submesh == null || submesh.Visible == visible) return;

            submesh.Visible = visible;
            if (visible)
            {
                if (!_meshes.Children.Contains(submesh.Model)) _meshes.Children.Add(submesh.Model);
                Refresh();
            }
            else _meshes.Children.Remove(submesh.Model);
        }

        /* The rig, as a thin spike from each bone to its parent. Rebuilt whole each frame - it's a
         * few hundred triangles, which is cheaper than tracking which ones moved. */
        private void PoseBones(List<Matrix4x4> pose)
        {
            _bones.Children.Clear();
            if (!_showBones) return;

            Point3DCollection points = new Point3DCollection();
            Int32Collection indices = new Int32Collection();

            for (int i = 0; i < _skeleton.Bones.Count && i < pose.Count; i++)
            {
                int parent = _skeleton.Bones[i].ParentIndex;
                if (parent < 0 || parent >= pose.Count) continue;

                Vector3 from = pose[parent].Translation, to = pose[i].Translation;
                Vector3 along = to - from;
                float length = along.Length();
                if (length < 0.0005f) continue;

                //a four sided spike, its base a square perpendicular to the bone
                along /= length;
                Vector3 side = Math.Abs(along.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY;
                Vector3 a = Vector3.Normalize(Vector3.Cross(along, side)) * Math.Min(0.015f, length * 0.12f);
                Vector3 b = Vector3.Normalize(Vector3.Cross(along, a)) * Math.Min(0.015f, length * 0.12f);
                Vector3 waist = from + (along * length * 0.15f);

                int at = points.Count;
                Add(points, from);
                Add(points, waist + a);
                Add(points, waist + b);
                Add(points, waist - a);
                Add(points, waist - b);
                Add(points, to);

                for (int side4 = 0; side4 < 4; side4++)
                {
                    int p = at + 1 + side4, q = at + 1 + ((side4 + 1) % 4);
                    indices.Add(at); indices.Add(q); indices.Add(p);
                    indices.Add(at + 5); indices.Add(p); indices.Add(q);
                }
            }

            if (points.Count == 0) return;

            SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(240, 190, 90));
            GeometryModel3D bones = new GeometryModel3D
            {
                Geometry = new MeshGeometry3D { Positions = points, TriangleIndices = indices },
                Material = new DiffuseMaterial(brush),
                BackMaterial = new DiffuseMaterial(brush),
            };
            _bones.Children.Add(bones);
        }

        //Alien Isolation is opposite-handed to WPF, so everything mirrors on the way in
        private static void Add(Point3DCollection points, Vector3 p)
        {
            points.Add(new Point3D(p.X, p.Y, -p.Z));
        }

        /// <summary>
        /// One submesh, pre-chewed into the arrays the per-frame skinning needs: rest positions and
        /// normals, and the skeleton bone each weight actually points at.
        /// </summary>
        private class SkinnedSubmesh : Part
        {
            public GeometryModel3D Model;

            public string Name { get; set; }
            public int VertexCount { get { return _positions?.Length ?? 0; } }
            public bool Visible { get; set; } = true;
            public string Group { get; set; }
            public int Lod { get; set; }
            public int GroupOrder { get; set; }

            /* CA name their collision meshes and the materials on them "COL_", e.g. the COL_MALE
             * hull on every human character. Nothing else in the shipped models uses the prefix. */
            public bool IsCollision { get; set; }

            private Vector3[] _positions;
            private Vector3[] _normals;
            private int[] _bones;      //four per vertex, already resolved through the submesh palette
            private float[] _weights;  //four per vertex
            private MeshGeometry3D _geometry;

            public static SkinnedSubmesh Build(Models.CS2.Component.LOD.Submesh submesh, Skeleton skeleton, bool useMaterials,
                                              string name, string groupName, int lod, int groupOrder)
            {
                if (submesh == null || submesh.Data.Length == 0) return null;

                cMesh mesh = ModelUtility.ToMesh(submesh);
                if (mesh.Vertices.Count == 0 || mesh.Indices.Count == 0) return null;
                if (mesh.BoneIndexes.Count != mesh.Vertices.Count || mesh.BoneWeights.Count != mesh.Vertices.Count) return null;

                SkinnedSubmesh skinned = new SkinnedSubmesh
                {
                    Name = name,
                    Group = groupName,
                    Lod = lod,
                    GroupOrder = groupOrder,
                    IsCollision = LooksLikeCollision(groupName) || LooksLikeCollision(submesh.Material?.Name),
                    _positions = mesh.Vertices.ToArray(),
                    _normals = mesh.Normals.Count == mesh.Vertices.Count ? mesh.Normals.ToArray() : null,
                    _bones = new int[mesh.Vertices.Count * 4],
                    _weights = new float[mesh.Vertices.Count * 4],
                };

                for (int v = 0; v < mesh.Vertices.Count; v++)
                {
                    System.Numerics.Vector4 indexes = mesh.BoneIndexes[v], weights = mesh.BoneWeights[v];
                    for (int slot = 0; slot < 4; slot++)
                    {
                        float weight = Component(weights, slot);
                        int local = (int)Math.Round(Component(indexes, slot));
                        int bone = local >= 0 && local < submesh.Bones.Count ? submesh.Bones[local] : local;

                        //a weight pointing past the end of the rig can't be honoured, so drop it
                        if (bone < 0 || bone >= skeleton.Bones.Count) weight = 0;

                        skinned._bones[(v * 4) + slot] = weight > 0 ? bone : 0;
                        skinned._weights[(v * 4) + slot] = weight;
                    }
                }

                //nothing is weighted to anything, so this submesh is rigid and can't be posed
                if (!skinned._weights.Any(x => x > 0)) return null;

                int[] indices = mesh.Indices.Select(x => (int)x).ToArray();
                for (int i = 0; i + 2 < indices.Length; i += 3)
                {
                    int b = indices[i + 1];
                    indices[i + 1] = indices[i + 2];
                    indices[i + 2] = b;
                }

                PointCollection uvs = new PointCollection();
                foreach (List<System.Numerics.Vector2> channel in mesh.UVs)
                {
                    if (channel == null) continue;
                    foreach (System.Numerics.Vector2 uv in channel) uvs.Add(new System.Windows.Point(uv.X, uv.Y));
                    break;
                }

                skinned._geometry = new MeshGeometry3D
                {
                    Positions = new Point3DCollection(mesh.Vertices.Count),
                    TriangleIndices = new Int32Collection(indices),
                    TextureCoordinates = uvs,
                };
                skinned.Model = new GeometryModel3D { Geometry = skinned._geometry };

                if (useMaterials) MaterialApplier.ApplyMaterial(skinned.Model, submesh.Material);
                if (skinned.Model.Material == null)
                {
                    SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(190, 190, 195));
                    skinned.Model.Material = new DiffuseMaterial(brush);
                    skinned.Model.BackMaterial = new DiffuseMaterial(brush);
                }
                return skinned;
            }

            /// <summary>Move every vertex onto the pose, weighted by the bones it belongs to.</summary>
            public void Pose(Matrix4x4[] skinning)
            {
                Point3DCollection points = new Point3DCollection(_positions.Length);
                Vector3DCollection normals = _normals == null ? null : new Vector3DCollection(_normals.Length);

                for (int v = 0; v < _positions.Length; v++)
                {
                    Vector3 position = Vector3.Zero, normal = Vector3.Zero;
                    float total = 0;

                    for (int slot = 0; slot < 4; slot++)
                    {
                        float weight = _weights[(v * 4) + slot];
                        if (weight <= 0) continue;

                        int bone = _bones[(v * 4) + slot];
                        if (bone >= skinning.Length) continue;

                        Matrix4x4 matrix = skinning[bone];
                        position += Vector3.Transform(_positions[v], matrix) * weight;
                        if (_normals != null) normal += Vector3.TransformNormal(_normals[v], matrix) * weight;
                        total += weight;
                    }

                    //an unweighted vertex belongs to nothing, so leave it where the artist put it
                    if (total <= 0) { position = _positions[v]; normal = _normals == null ? Vector3.Zero : _normals[v]; }
                    else if (Math.Abs(total - 1) > 0.001f) { position /= total; normal /= total; }

                    points.Add(new Point3D(position.X, position.Y, -position.Z));
                    if (normals != null)
                    {
                        normal = normal.LengthSquared() > 1e-12f ? Vector3.Normalize(normal) : new Vector3(0, 1, 0);
                        normals.Add(new Vector3D(normal.X, normal.Y, -normal.Z));
                    }
                }

                _geometry.Positions = points;
                if (normals != null) _geometry.Normals = normals;
            }

            private static bool LooksLikeCollision(string name)
            {
                if (string.IsNullOrEmpty(name)) return false;

                string last = name.Substring(name.LastIndexOfAny(new[] { '\\', '/' }) + 1);
                return last.StartsWith("COL_", StringComparison.OrdinalIgnoreCase)
                    || last.IndexOf("_COL_", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            private static float Component(System.Numerics.Vector4 value, int index)
            {
                switch (index)
                {
                    case 0: return value.X;
                    case 1: return value.Y;
                    case 2: return value.Z;
                    default: return value.W;
                }
            }
        }
    }
}

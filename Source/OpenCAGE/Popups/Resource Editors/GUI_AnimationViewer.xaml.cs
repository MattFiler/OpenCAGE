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
        /// Whether to draw the mesh at all. Turning it off leaves the rig on its own, which is the
        /// only way to see what a clip is doing to bones the body hides. Which parts are ticked in
        /// the parts list is left alone, so they come back as they were.
        /// </summary>
        public bool ShowMesh
        {
            get { return _showMesh; }
            set
            {
                if (_showMesh == value) return;

                _showMesh = value;
                posedModel.Content = value ? _meshes : null;
                if (value) Refresh();
            }
        }

        /// <summary>
        /// Whether this rig drives set dressing rather than a character.
        ///
        /// Havok holds a skeleton Z up and a CS2 character mesh is Y up, so a character's rig has to
        /// be turned a quarter turn to meet its own mesh. A prop's rig is already in the same space
        /// as the prop - so making the same turn there lays the rig on its side beside the geometry
        /// it is supposed to be moving, and moves the parts about an axis they don't sit on.
        /// </summary>
        public bool EnvironmentRig { get; set; }

        /// <summary>
        /// Whether to let the clip's root bone move the character. Off by default: the root is a
        /// motion extraction bone the engine reads to move the entity, not the mesh.
        /// </summary>
        public CathodeLib.Animation.RootMotion RootMotion
        {
            get { return _rootMotion; }
            set { _rootMotion = value; Refresh(); }
        }

        /// <summary>
        /// How a clip authored for another rig reaches this one, or null when it was authored for
        /// the rig it is playing on.
        /// </summary>
        public Retargeter Retarget
        {
            get { return _retarget; }
            set { _retarget = value; Refresh(); }
        }

        /// <summary>Every part of the model that can be posed, so the caller can offer them as filters.</summary>
        public IReadOnlyList<Part> Parts { get { return _submeshes; } }

        /// <summary>
        /// Whether the current model is moved a part at a time rather than a vertex at a time -
        /// the environment animation system, which plays clips on static geometry.
        /// </summary>
        public bool Rigid { get; private set; }

        /// <summary>How many of the model's parts the rig actually moves, and how many there are.</summary>
        public int DrivenParts { get; private set; }
        public int TotalParts { get; private set; }

        private readonly Model3DGroup _meshes = new Model3DGroup();
        private readonly Model3DGroup _bones = new Model3DGroup();
        private readonly List<SkinnedSubmesh> _submeshes = new List<SkinnedSubmesh>();

        private Skeleton _skeleton;
        private CathodeLib.Animation.ClipReference _clip;
        private EnvironmentRigs.Prop _prop;
        private bool _showBones = true;
        private bool _showMesh = true;
        private CathodeLib.Animation.RootMotion _rootMotion = CathodeLib.Animation.RootMotion.Ignore;
        private Retargeter _retarget;
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
            posedModel.Content = _showMesh ? _meshes : null;
            posedSkeleton.Content = _bones;
        }

        /// <summary>
        /// Bind a model to a skeleton, ready to be posed. Pass a null model to preview the rig alone.
        ///
        /// A skinned mesh is posed a vertex at a time from its weights. A static one is posed a part
        /// at a time: the environment animation system builds a prop out of separate meshes and
        /// gives the rig a bone for each, so each mesh moves rigidly with its bone and anything the
        /// rig doesn't drive - a door frame, a weapon housing - stays where the level puts it.
        ///
        /// Pass the level a static mesh belongs to and the parts are bound and placed the way the
        /// level records; without one the only thing left to go on is the parts' names.
        /// </summary>
        public void SetModel(Models.CS2 cs2, Skeleton skeleton, bool useMaterials, Level level = null)
        {
            _meshes.Children.Clear();
            _submeshes.Clear();
            _skeleton = skeleton;
            _prop = null;
            Problem = null;
            Rigid = cs2 != null && Skeleton.RequiredBoneCount(cs2) == 0;
            DrivenParts = 0;
            TotalParts = 0;

            if (skeleton == null)
            {
                Problem = "No skeleton has been chosen, so there's nothing to pose.";
                Refresh();
                return;
            }

            /* Look for the level's record whether or not the mesh has weights. Most props are made
             * of rigid parts, but a few deform, and a couple - the reactor core, a survey crane -
             * are both at once, so "does this mesh have weights" can't decide it for the model as a
             * whole. What settles it is whether the level animates this mesh with this rig. */
            if (level != null && cs2 != null)
                _prop = EnvironmentRigs.AnimatedPropFor(level, skeleton.Name, cs2);

            if (_prop != null) Rigid = true;

            /* Where each part goes and what moves it. The level's own record where there is one,
             * because a bone and the part it drives don't have to share a name - and because it is
             * the only thing that says where a part sits, each one being modelled about its own
             * origin. */
            Dictionary<Models.CS2.Component.LOD.Submesh, EnvironmentRigs.Part> placement = null;
            if (_prop != null)
            {
                placement = new Dictionary<Models.CS2.Component.LOD.Submesh, EnvironmentRigs.Part>(SameSubmesh.Instance);
                foreach (EnvironmentRigs.Part part in _prop.Parts) placement[part.Submesh] = part;
            }

            int skipped = 0, highest = -1, group = 0;
            int[][] namedBones = Rigid && _prop == null ? EnvironmentRigs.Bind(cs2, skeleton) : null;
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
                        bool counted = false;

                        for (int s = 0; s < lod.Submeshes.Count; s++)
                        {
                            Models.CS2.Component.LOD.Submesh submesh = lod.Submeshes[s];
                            if (l == 0)
                                foreach (int bone in submesh.Bones)
                                    if (bone > highest) highest = bone;

                            int? rigidBone = null;
                            Matrix4x4 rest = Matrix4x4.Identity;
                            if (Rigid)
                            {
                                if (placement != null)
                                {
                                    EnvironmentRigs.Part known = placement.TryGetValue(submesh, out EnvironmentRigs.Part found) ? found : null;

                                    /* A part with vertex weights deforms, so it goes down the same
                                     * path as a character rather than being carried about by one
                                     * bone. An alien egg's shell is one mesh over nine petal bones.
                                     *
                                     * That holds whether or not the level's record names the part.
                                     * A character caught up in an environment animation - the player
                                     * bracing a door - matches the record as a whole while most of
                                     * its submeshes are absent from it, and carrying those about on
                                     * one bone each turns the body inside out. */
                                    bool deforms = submesh.Bones != null && submesh.Bones.Count != 0;

                                    if (deforms && (known == null || known.Skinned)) rigidBone = null;
                                    else rigidBone = known == null ? -1 : known.Bone;
                                    rest = known == null ? Matrix4x4.Identity : known.Rest;

                                    if (!counted)
                                    {
                                        TotalParts++;
                                        if (deforms || (known != null && (known.Skinned || known.Bone >= 0))) DrivenParts++;
                                        counted = true;
                                    }
                                }
                                else
                                {
                                    rigidBone = namedBones[c][l];
                                    if (!counted) { TotalParts++; if (rigidBone >= 0) DrivenParts++; counted = true; }
                                }
                            }

                            SkinnedSubmesh skinned = SkinnedSubmesh.Build(submesh, skeleton, useMaterials,
                                "Submesh " + s, part, l, group, rigidBone, rest);
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
            else if (Rigid && DrivenParts == 0)
                Problem = "Nothing in '" + LastSegment(cs2.Name) + "' moves with '" + skeleton.Name + "'. "
                        + (_prop != null
                            ? "This level draws the mesh, but its record of the animation doesn't name any part of it as something this rig moves."
                            : "This level has no record of '" + skeleton.Name + "' animating this mesh, and none of the mesh's parts are named after a bone of the rig.");
            else if (!Rigid && highest >= skeleton.Bones.Count)
                Problem = "'" + cs2.Name + "' is skinned to " + (highest + 1) + " bones, but '" + skeleton.Name
                        + "' only has " + skeleton.Bones.Count + ". Parts of the mesh will stay where they are.";

            Refresh();
        }

        /// <summary>Identity, not equality - two submeshes with the same contents are still two submeshes.</summary>
        private class SameSubmesh : IEqualityComparer<Models.CS2.Component.LOD.Submesh>
        {
            public static readonly SameSubmesh Instance = new SameSubmesh();

            public bool Equals(Models.CS2.Component.LOD.Submesh x, Models.CS2.Component.LOD.Submesh y) { return ReferenceEquals(x, y); }
            public int GetHashCode(Models.CS2.Component.LOD.Submesh obj) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj); }
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

            /* A prop is drawn in the space its own rig lives in, and each part is carried whole by
             * its bone - so the bone's transform is the part's, and there is no bind pose to divide
             * out. A character is the other way round: the mesh is authored in its own space and
             * every vertex is pulled towards its bones, which needs inverse-bind-times-animated.
             *
             * A handful of props are built the character way, and those parts need the second form
             * even though the prop as a whole wants the first, so both are on hand here. */
            if (_prop != null)
            {
                Matrix4x4[] placed = EnvironmentRigs.Pose(_prop, _skeleton, _clip, _frame, _rootMotion);
                Matrix4x4[] deformed = _prop.HasSkinning
                    ? EnvironmentRigs.SkinningPose(_prop, _skeleton, _clip, _frame, _rootMotion) : null;

                for (int i = 0; i < _submeshes.Count; i++)
                    if (_submeshes[i].Visible)
                        _submeshes[i].Pose(_submeshes[i].IsRigid || deformed == null ? placed : deformed);
                PoseBones(new List<Matrix4x4>(placed));
                return;
            }

            /* No record to place the prop with, so all that's left is how far the clip has moved
             * each bone since its rest. That still has to be measured in the space the rig lives in:
             * an environment rig's is the prop's own, a character's is its mesh's. */
            List<Matrix4x4> animated = EnvironmentRig
                ? CathodeLib.Animation.SampleRigPose(_clip, _skeleton, _frame, _rootMotion, _retarget)
                : CathodeLib.Animation.SampleModelPose(_clip, _skeleton, _frame, _rootMotion, _retarget);
            if (animated == null) return;

            List<Matrix4x4> bind = EnvironmentRig ? _skeleton.GetModelSpacePose() : _skeleton.GetBindPose();
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

            /* Set only on a rigidly bound part: the one bone that carries the whole submesh, or -1
             * for a part nothing moves. Null on a skinned submesh, which uses the weights. */
            private int? _rigidBone;

            /// <summary>Whether this part is carried whole by one bone rather than deformed.</summary>
            public bool IsRigid { get { return _rigidBone.HasValue; } }

            /* Where the level puts a part nothing moves. A part is modelled about its own origin,
             * so without this the prop comes apart. */
            private Matrix4x4 _rest = Matrix4x4.Identity;
            private bool _placed;

            public static SkinnedSubmesh Build(Models.CS2.Component.LOD.Submesh submesh, Skeleton skeleton, bool useMaterials,
                                              string name, string groupName, int lod, int groupOrder, int? rigidBone = null,
                                              Matrix4x4 rest = default(Matrix4x4))
            {
                if (submesh == null || submesh.Data.Length == 0) return null;

                cMesh mesh = ModelUtility.ToMesh(submesh);
                if (mesh.Vertices.Count == 0 || mesh.Indices.Count == 0) return null;

                bool rigid = rigidBone.HasValue;
                if (!rigid && (mesh.BoneIndexes.Count != mesh.Vertices.Count || mesh.BoneWeights.Count != mesh.Vertices.Count)) return null;

                SkinnedSubmesh skinned = new SkinnedSubmesh
                {
                    Name = name,
                    Group = groupName,
                    Lod = lod,
                    GroupOrder = groupOrder,
                    IsCollision = LooksLikeCollision(groupName) || LooksLikeCollision(submesh.Material?.Name),
                    _positions = mesh.Vertices.ToArray(),
                    _normals = mesh.Normals.Count == mesh.Vertices.Count ? mesh.Normals.ToArray() : null,
                    _rigidBone = rigidBone,
                    _rest = rest == default(Matrix4x4) ? Matrix4x4.Identity : rest,
                };

                if (!rigid)
                {
                    skinned._bones = new int[mesh.Vertices.Count * 4];
                    skinned._weights = new float[mesh.Vertices.Count * 4];

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
                }

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
                if (_rigidBone.HasValue) { PoseRigid(skinning); return; }

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

            /* One matrix for the whole part: the transform of the bone carrying it, or the rest
             * placement for a part nothing moves. That one only needs applying once - which matters
             * because a prop can be a couple of dozen parts and most of them are scenery. */
            private void PoseRigid(Matrix4x4[] placed)
            {
                int bone = _rigidBone.Value;
                bool driven = bone >= 0 && bone < placed.Length;
                if (!driven && _placed) return;

                Matrix4x4 matrix = driven ? placed[bone] : _rest;

                Point3DCollection points = new Point3DCollection(_positions.Length);
                Vector3DCollection normals = _normals == null ? null : new Vector3DCollection(_normals.Length);
                for (int v = 0; v < _positions.Length; v++)
                {
                    Vector3 position = Vector3.Transform(_positions[v], matrix);
                    points.Add(new Point3D(position.X, position.Y, -position.Z));
                    if (normals == null) continue;

                    Vector3 normal = Vector3.TransformNormal(_normals[v], matrix);
                    normal = normal.LengthSquared() > 1e-12f ? Vector3.Normalize(normal) : new Vector3(0, 1, 0);
                    normals.Add(new Vector3D(normal.X, normal.Y, -normal.Z));
                }

                _geometry.Positions = points;
                if (normals != null) _geometry.Normals = normals;
                _placed = true;
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

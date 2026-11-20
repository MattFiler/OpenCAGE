using CATHODE;
using CATHODE.Scripting;
using CathodeLib;
using OpenCAGE.Undo;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.Markup
{
    /// <summary>
    /// Composite markup: editor-only shapes the viewport draws at every instance of a composite to show what it is. The
    /// built-in set ships with OpenCAGE (<see cref="BuiltInMarkup"/>, by composite path, in every level); a level can add
    /// pieces of its own to any composite, kept in its COMPOSITE_MARKUP table. Neither is ever an entity: the composites,
    /// the game and mod exports never see it. UI thread.
    /// </summary>
    public static class CompositeMarkup
    {
        /// <summary>A level's own markup changed (an edit, an undo or redo). UI thread.</summary>
        public static event Action Changed;

        internal static void RaiseChanged() => Changed?.Invoke();

        public static readonly string[] ShapeNames = Enum.GetNames(typeof(CompositeMarkupTable.Shape));

        /// <summary>The built-in markup for a composite, or null.</summary>
        public static BuiltInMarkup.Entry BuiltInFor(Composite composite) => composite == null ? null : BuiltInMarkup.Find(composite.name);

        /// <summary>The level's own pieces for a composite (copies).</summary>
        public static List<CompositeMarkupTable.Markup> Pieces(Commands commands, Composite composite)
        {
            if (commands?.Utils == null || composite == null)
                return new List<CompositeMarkupTable.Markup>();
            return commands.Utils.GetCompositeMarkup(composite.shortGUID);
        }

        /// <summary>
        /// Replace a composite's own pieces as one undo step. Steps with the same <paramref name="session"/> and label, one after
        /// another, fold into one (an editor's run of field changes).
        /// </summary>
        public static void SetPieces(Commands commands, Composite composite, List<CompositeMarkupTable.Markup> pieces, string label, object session = null)
        {
            if (commands?.Utils == null || composite == null)
                return;
            UndoStack.Current.Apply(new CompositeMarkupEdit(composite, Pieces(commands, composite), pieces, label, session));
        }

        /// <summary>A new piece with sensible defaults for its shape.</summary>
        public static CompositeMarkupTable.Markup NewPiece(CompositeMarkupTable.Shape shape)
        {
            CompositeMarkupTable.Markup piece = new CompositeMarkupTable.Markup() { shape = shape, name = shape.ToString() };
            switch (shape)
            {
                case CompositeMarkupTable.Shape.Box: piece.size = new float[] { 0.5f, 0.5f, 0.5f }; break;
                case CompositeMarkupTable.Shape.Cylinder: piece.size = new float[] { 0.4f, 0.5f, 0.4f }; break;
                case CompositeMarkupTable.Shape.Sphere: piece.size = new float[] { 0.4f, 0.4f, 0.4f }; break;
                case CompositeMarkupTable.Shape.Cone: piece.size = new float[] { 0.4f, 0.5f, 0.4f }; break;
                case CompositeMarkupTable.Shape.Arrow: piece.size = new float[] { 0.2f, 0.02f, 1f }; break;
                case CompositeMarkupTable.Shape.Ring: piece.size = new float[] { 0.42f, 0.03f, 0.42f }; break;
                default: piece.size = new float[] { 1f, 1f, 1f }; break;
            }
            Vector3 colour = shape == CompositeMarkupTable.Shape.FrontLabel ? BuiltInMarkup.TraversalBlue : BuiltInMarkup.Green;
            piece.colour = new float[] { colour.X, colour.Y, colour.Z };
            return piece;
        }

        /// <summary>The most triangles one mesh piece may hold: it is drawn at every instance and sent to the viewport whole.</summary>
        public const int MaxMeshTriangles = 20000;

        /// <summary>Make a piece a mesh of these triangles (its size back to 1). False, saying why, when there are too many or none.</summary>
        public static bool TrySetMesh(CompositeMarkupTable.Markup piece, IList<Vector3> positions, IList<int> indices, out string problem)
        {
            problem = null;
            if (positions == null || indices == null || indices.Count < 3)
            {
                problem = "The mesh has no triangles.";
                return false;
            }
            if (indices.Count / 3 > MaxMeshTriangles)
            {
                problem = "That mesh has " + (indices.Count / 3) + " triangles; markup takes at most " + MaxMeshTriangles + ". Simplify it first.";
                return false;
            }
            piece.shape = CompositeMarkupTable.Shape.Mesh;
            piece.vertices = positions.SelectMany(o => new[] { o.X, o.Y, o.Z }).ToArray();
            piece.indices = indices.ToArray();
            piece.size = new float[] { 1, 1, 1 };
            return true;
        }

        /// <summary>One piece's triangles, in the composite's space.</summary>
        public static MarkupMesh BuildPiece(CompositeMarkupTable.Markup piece)
        {
            MarkupMesh shape = new MarkupMesh();
            if (piece == null)
                return shape;
            Vector3 colour = Vec(piece.colour, Vector3.One);
            Vector3 size = Vec(piece.size, Vector3.One);
            Vector3 one = Vector3.One;
            switch (piece.shape)
            {
                case CompositeMarkupTable.Shape.Box:
                    shape.Box(colour, new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 1, 0.5f));
                    break;
                case CompositeMarkupTable.Shape.Cylinder:
                    shape.Cylinder(colour, Vector3.Zero, 0.5f, 0.5f, 0.5f, 0.5f, 1, 24);
                    break;
                case CompositeMarkupTable.Shape.Cone:
                    shape.Cylinder(colour, Vector3.Zero, 0.5f, 0.5f, 0, 0, 1, 24);
                    break;
                case CompositeMarkupTable.Shape.Sphere:
                    shape.Sphere(colour, new Vector3(0, 0.5f, 0), new Vector3(0.5f));
                    break;
                case CompositeMarkupTable.Shape.Ring:
                    shape.Annulus(colour, Vector3.Zero, Vector3.UnitX, -Vector3.UnitZ, Vector3.UnitY, Vector2.Zero, 0.5f, 0.4f, 24, 0, 1);
                    break;
                case CompositeMarkupTable.Shape.Arrow:
                    //A flat arrow, tail at the origin, tip at +Z: the head is a third of its length and twice its width
                    shape.Prism(colour, Vector3.Zero, Vector3.UnitX, Vector3.UnitZ, Vector3.UnitY,
                        new[] { new Vector2(-0.25f, 0), new Vector2(0.25f, 0), new Vector2(0.25f, 0.667f), new Vector2(-0.25f, 0.667f) }, 0, 1);
                    shape.Prism(colour, Vector3.Zero, Vector3.UnitX, Vector3.UnitZ, Vector3.UnitY,
                        new[] { new Vector2(-0.5f, 0.667f), new Vector2(0.5f, 0.667f), new Vector2(0, 1) }, 0, 1);
                    break;
                case CompositeMarkupTable.Shape.FrontLabel:
                    shape.Append(BuiltInMarkup.FrontLabel(colour), Matrix4x4.Identity);
                    break;
                case CompositeMarkupTable.Shape.Mesh:
                    if (piece.vertices != null && piece.indices != null)
                    {
                        List<Vector3> vertices = new List<Vector3>(piece.vertices.Length / 3);
                        for (int i = 0; i + 2 < piece.vertices.Length; i += 3)
                            vertices.Add(new Vector3(piece.vertices[i], piece.vertices[i + 1], piece.vertices[i + 2]));
                        shape.Triangles(colour, vertices, piece.indices, Matrix4x4.Identity);
                    }
                    break;
            }

            Matrix4x4 placement = Matrix4x4.CreateScale(size) * Rotation(Vec(piece.rotation, Vector3.Zero)) * Matrix4x4.CreateTranslation(Vec(piece.position, Vector3.Zero));
            MarkupMesh placed = new MarkupMesh();
            placed.Append(shape, placement);
            return placed;
        }

        /// <summary>A composite's markup: the built-in set and/or the level's own pieces.</summary>
        public static MarkupMesh Build(Commands commands, Composite composite, bool builtIn = true, bool own = true)
        {
            MarkupMesh mesh = new MarkupMesh();
            if (builtIn)
            {
                BuiltInMarkup.Entry entry = BuiltInFor(composite);
                if (entry != null)
                    mesh.Append(entry.Build(), Matrix4x4.Identity);
            }
            if (own)
                foreach (CompositeMarkupTable.Markup piece in Pieces(commands, composite))
                    mesh.Append(BuildPiece(piece), Matrix4x4.Identity);
            return mesh;
        }

        /// <summary>What the viewport is sent: every composite of the level with any markup, as triangles.</summary>
        public static List<SyncedCompositeMarkup> BuildSyncSet(Commands commands)
        {
            List<SyncedCompositeMarkup> set = new List<SyncedCompositeMarkup>();
            if (commands?.Entries == null)
                return set;

            HashSet<ShortGuid> own = new HashSet<ShortGuid>(commands.Utils?.GetCompositesWithMarkup() ?? new List<ShortGuid>());
            foreach (Composite composite in commands.Entries)
            {
                if (composite == null || (!own.Contains(composite.shortGUID) && BuiltInFor(composite) == null))
                    continue;
                SyncedCompositeMarkup entry = ToSynced(composite.shortGUID, Build(commands, composite));
                if (entry.surfaces.Count != 0)
                    set.Add(entry);
            }
            return set;
        }

        public static SyncedCompositeMarkup ToSynced(ShortGuid composite, MarkupMesh mesh)
        {
            SyncedCompositeMarkup synced = new SyncedCompositeMarkup() { composite = composite.AsUInt32 };
            foreach (MarkupMesh.Surface surface in mesh.Surfaces)
            {
                if (surface.Indices.Count < 3)
                    continue;
                float[] vertices = new float[surface.Vertices.Count * 3];
                for (int i = 0; i < surface.Vertices.Count; i++)
                {
                    //A tenth of a millimetre is plenty, and keeps the packet small
                    vertices[i * 3] = (float)Math.Round(surface.Vertices[i].X, 4);
                    vertices[i * 3 + 1] = (float)Math.Round(surface.Vertices[i].Y, 4);
                    vertices[i * 3 + 2] = (float)Math.Round(surface.Vertices[i].Z, 4);
                }
                synced.surfaces.Add(new SyncedMarkupSurface()
                {
                    colour_r = surface.Colour.X,
                    colour_g = surface.Colour.Y,
                    colour_b = surface.Colour.Z,
                    vertices = vertices,
                    indices = surface.Indices.ToArray(),
                });
            }
            return synced;
        }

        /// <summary>Cathode's rotation (degrees, as entity positions take it).</summary>
        public static Matrix4x4 Rotation(Vector3 degrees)
        {
            const float toRadians = (float)(Math.PI / 180.0);
            return Matrix4x4.CreateFromQuaternion(Quaternion.CreateFromYawPitchRoll(degrees.Y * toRadians, degrees.X * toRadians, degrees.Z * toRadians));
        }

        public static Vector3 Vec(float[] values, Vector3 fallback)
        {
            if (values == null || values.Length < 3)
                return fallback;
            return new Vector3(values[0], values[1], values[2]);
        }

        public static float[] Floats(Vector3 v) => new float[] { v.X, v.Y, v.Z };
    }

    /// <summary>
    /// A composite's own markup pieces replaced (added to, edited, removed): the level's COMPOSITE_MARKUP table only, so no
    /// composite counts as changed by it and the composite on screen is left as it is.
    /// </summary>
    public sealed class CompositeMarkupEdit : IEdit, IChangesComposites
    {
        private readonly ShortGuid _composite;
        private readonly List<CompositeMarkupTable.Markup> _before;
        private List<CompositeMarkupTable.Markup> _after;
        private readonly object _session;

        public string Label { get; }
        public ShortGuid CompositeId => ShortGuid.Invalid;
        public ShortGuid EntityId => ShortGuid.Invalid;
        //Editor data only: no composite counts as changed
        public IEnumerable<ShortGuid> ChangedComposites => Enumerable.Empty<ShortGuid>();

        public CompositeMarkupEdit(Composite composite, List<CompositeMarkupTable.Markup> before, List<CompositeMarkupTable.Markup> after, string label, object session = null)
        {
            _composite = composite.shortGUID;
            _before = before?.Select(o => o.Clone()).ToList() ?? new List<CompositeMarkupTable.Markup>();
            _after = after?.Select(o => o.Clone()).ToList() ?? new List<CompositeMarkupTable.Markup>();
            Label = label;
            _session = session;
        }

        public void Apply(UndoContext context) => Set(context, _after);
        public void Revert(UndoContext context) => Set(context, _before);

        private void Set(UndoContext context, List<CompositeMarkupTable.Markup> pieces)
        {
            context.Commands?.Utils?.SetCompositeMarkup(_composite, pieces);
            DirtyTracker.MarkLevelDataModified();
            CompositeMarkup.RaiseChanged();
        }

        public bool TryMerge(IEdit next)
        {
            if (_session == null || !(next is CompositeMarkupEdit edit) || !ReferenceEquals(edit._session, _session) || edit._composite != _composite || edit.Label != Label)
                return false;
            _after = edit._after;
            return true;
        }
    }
}

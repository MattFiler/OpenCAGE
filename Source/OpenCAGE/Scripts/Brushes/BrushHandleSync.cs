using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.DockPanels;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.Brushwork
{
    /// <summary>
    /// The viewport's handles on the selected brush (the viewer's LevelViewerBrushHandles): told the brush's bounds whenever a lone
    /// box or plane brush is selected - or its vertices and edges, for a mesh brush - and again when it changes or the viewer rebuilds
    /// its scene, and told to take them away when the selection moves off it. A face handle let go (BRUSH_EDIT_RESIZED) resizes the
    /// brush, a vertex handle let go (BRUSH_EDIT_VERTICES) moves that vertex: each one undo step.
    /// </summary>
    public static class BrushHandleSync
    {
        private static bool _initialised;
        private static ShortGuid _shownEntity = ShortGuid.Invalid;

        public static void Initialise()
        {
            if (_initialised) return;
            _initialised = true;
            BrushCollision.Initialise();
            Singleton.OnEntityReloaded += entity => Resend();
            Singleton.OnEntitiesReloaded += entities => Resend();
            Singleton.OnSelectionCleared += Resend;
            Singleton.OnCompositeSelected += composite => Resend();
            BrushService.Changed += (composite, entity) => Resend();
        }

        /// <summary>Send the viewer the handles the selection should have now (none unless it is a lone box or plane brush).</summary>
        public static void Resend()
        {
            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed) return;
            if (editor.InvokeRequired)
            {
                try { editor.BeginInvoke(new Action(Resend)); } catch { }
                return;
            }

            CompositeDisplay display = editor.CompositeDisplay;
            Entity entity = display?.EntityDisplay?.Entity;
            List<Entity> multi = display?.EntityDisplay?.MultiSelectedEntities;
            BrushDefinition brush = null;
            if (entity != null && (multi == null || multi.Count <= 1) && display.Content?.Level != null && display.Composite != null)
                brush = BrushService.Get(display.Content.Level, display.Composite, entity);

            if (brush == null || (brush.Kind == BrushKind.Mesh && (brush.Mesh == null || brush.Mesh.Vertices.Count == 0)))
            {
                if (_shownEntity != ShortGuid.Invalid)
                {
                    _shownEntity = ShortGuid.Invalid;
                    Send.SendBrushEdit(false, ShortGuid.Invalid, Vector3.Zero, Vector3.Zero, 0);
                }
                return;
            }
            _shownEntity = entity.shortGUID;
            if (brush.Kind == BrushKind.Mesh)
            {
                //A handle on every vertex, and the edges drawn over it
                float[] vertices = new float[brush.Mesh.Vertices.Count * 3];
                for (int i = 0; i < brush.Mesh.Vertices.Count; i++)
                {
                    vertices[i * 3] = brush.Mesh.Vertices[i].X;
                    vertices[i * 3 + 1] = brush.Mesh.Vertices[i].Y;
                    vertices[i * 3 + 2] = brush.Mesh.Vertices[i].Z;
                }
                Send.SendBrushEdit(true, entity.shortGUID, brush.Min, brush.Max, 2, vertices, Edges(brush.Mesh));
                return;
            }
            Send.SendBrushEdit(true, entity.shortGUID, brush.Min, brush.Max, brush.Kind == BrushKind.Plane ? 1 : 0);
        }

        /// <summary>A mesh's edges, each once, as vertex index pairs.</summary>
        public static int[] Edges(BrushMesh mesh)
        {
            HashSet<(int, int)> seen = new HashSet<(int, int)>();
            List<int> pairs = new List<int>();
            foreach (BrushMeshFace face in mesh.Faces)
            {
                for (int i = 0; i < face.Vertices.Count; i++)
                {
                    int a = face.Vertices[i], b = face.Vertices[(i + 1) % face.Vertices.Count];
                    if (a == b || !seen.Add((Math.Min(a, b), Math.Max(a, b)))) continue;
                    pairs.Add(a);
                    pairs.Add(b);
                }
            }
            return pairs.ToArray();
        }

        /// <summary>A mesh brush's vertex handle was let go in the viewport: move the brush's vertices to where the viewer has them.</summary>
        public static void ApplyVertices(Packet packet)
        {
            CompositeDisplay display = Singleton.Editor?.CompositeDisplay;
            Level level = display?.Content?.Level;
            Composite composite = display?.Composite;
            if (packet?.brush_edit_vertices == null || level == null || composite == null || !display.Populated) return;
            if (!(composite.GetEntityByID(new ShortGuid(packet.brush_edit_entity)) is FunctionEntity entity)) return;
            BrushDefinition brush = BrushService.Get(level, composite, entity);
            if (brush == null || brush.Kind != BrushKind.Mesh || brush.Mesh == null) return;
            string name = level.Commands.Utils.GetEntityName(composite, entity);
            //The viewer's list is of the mesh it was sent: a mesh changed since (an undo, an edit elsewhere) is not moved by it
            if (packet.brush_edit_vertices.Length != brush.Mesh.Vertices.Count * 3 || packet.brush_edit_vertices.Any(o => float.IsNaN(o) || float.IsInfinity(o)))
            {
                Debug.Log("Brushes", "Vertex move on " + name + " ignored: the viewer's vertices are not this mesh's.");
                Resend();
                return;
            }
            for (int i = 0; i < brush.Mesh.Vertices.Count; i++)
                brush.Mesh.Vertices[i] = new Vector3(packet.brush_edit_vertices[i * 3], packet.brush_edit_vertices[i * 3 + 1], packet.brush_edit_vertices[i * 3 + 2]);
            BrushEditing.UpdateBounds(brush);
            try
            {
                BrushService.Apply(level, composite, entity, brush, "Move vertex on " + name, new List<string>());
                //A face bent or the solid dented by the move: logged, the move stands (edit_brush_mesh says it in its notes)
                foreach (string warning in BrushEditing.ShapeWarnings(brush))
                    Debug.Log("Brushes", name + ": " + warning);
            }
            catch (BrushException e)
            {
                Debug.Log("Brushes", "Vertex move on " + name + " refused: " + e.Message);
                Resend();
            }
        }

        /// <summary>A face handle was let go in the viewport: resize the brush to the bounds it reached.</summary>
        public static void ApplyResize(Packet packet)
        {
            if (packet?.brush_edit_min == null || packet.brush_edit_max == null || packet.brush_edit_min.Length < 3 || packet.brush_edit_max.Length < 3)
                return;
            CompositeDisplay display = Singleton.Editor?.CompositeDisplay;
            Level level = display?.Content?.Level;
            Composite composite = display?.Composite;
            if (level == null || composite == null || !display.Populated) return;
            if (!(composite.GetEntityByID(new ShortGuid(packet.brush_edit_entity)) is FunctionEntity entity)) return;
            BrushDefinition brush = BrushService.Get(level, composite, entity);
            if (brush == null || brush.Kind == BrushKind.Mesh) return;

            brush.Min = new Vector3(packet.brush_edit_min[0], packet.brush_edit_min[1], packet.brush_edit_min[2]);
            brush.Max = new Vector3(packet.brush_edit_max[0], packet.brush_edit_max[1], packet.brush_edit_max[2]);
            if (brush.Kind == BrushKind.Plane) brush.Max = new Vector3(brush.Max.X, brush.Min.Y, brush.Max.Z);
            string name = level.Commands.Utils.GetEntityName(composite, entity);
            try
            {
                BrushService.Apply(level, composite, entity, brush, "Resize " + name, new List<string>());
            }
            catch (BrushException e)
            {
                Debug.Log("Brushes", "Resize of " + name + " refused: " + e.Message);
                //The viewer showed the new size: put the handles back where the brush is
                Resend();
            }
        }
    }
}

using CATHODE.Scripting;
using CathodeLib;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace OpenCAGE.Brushwork
{
    /// <summary>
    /// Making a brush where a viewport click lands (toolbar Create > Brush creation mode, or right-click Create Brush). The viewer
    /// is asked to place a ModelReference and the answer is made a brush here (<see cref="ViewerEntityCreateSync"/> asks
    /// <see cref="Claim"/>). A box's origin is its bottom centre, so it stands on the surface clicked; a plane lies on it.
    /// </summary>
    public static class BrushPlacement
    {
        /// <summary>The brush the viewport's creation mode makes (the mode itself is a ModelReference one), or null.</summary>
        public static BrushKind? CreateModeKind { get; set; }

        private static BrushKind? _dropKind;
        private static DateTime _dropAt;

        /// <summary>Ask the viewer where a click at (x, y) - fractions of the viewport - lands; a brush is made there when it answers.</summary>
        public static bool RequestAt(BrushKind kind, float x, float y)
        {
            if (!Send.Connected) return false;
            _dropKind = kind;
            _dropAt = DateTime.Now;
            Send.SendFunctionDropPacket(FunctionType.ModelReference, x, y);
            return true;
        }

        /// <summary>The kind of brush a viewer create request should make, or null when it is for an ordinary entity.</summary>
        public static BrushKind? Claim(Packet packet)
        {
            if (packet == null || (FunctionType)packet.entity_function != FunctionType.ModelReference) return null;
            if (packet.drop_function_type != 0)
            {
                //A palette drop of ModelReference is one; ours is the one asked for a moment ago
                if (_dropKind == null || (DateTime.Now - _dropAt).TotalSeconds > 10) return null;
                BrushKind kind = _dropKind.Value;
                _dropKind = null;
                return kind;
            }
            return CreateModeKind;
        }

        /// <summary>Make a brush of the default size at <paramref name="placement"/> in the composite on display, world-aligned. Null (after saying why) when it can't.</summary>
        public static FunctionEntity CreateAt(CompositeDisplay display, BrushKind kind, cTransform placement)
        {
            Level level = display?.Content?.Level;
            Composite composite = display?.Composite;
            if (level == null || composite == null) return null;
            BrushDefinition brush = BrushService.NewDefault(kind);
            brush.UvOrigin = BrushService.WorldAlignedOrigin(level.Commands, composite, placement);
            string name = BrushService.NewName(level.Commands, composite, kind);
            List<string> notes = new List<string>();
            try
            {
                FunctionEntity entity = BrushService.Create(level, composite, brush, placement, name, "Create " + name, notes);
                foreach (string note in notes) Debug.Log("Brushes", note);
                return entity;
            }
            catch (BrushException e)
            {
                MessageBox.Show("The brush could not be made: " + e.Message, "Create Brush", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
        }
    }
}

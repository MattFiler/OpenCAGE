using System.Collections.Generic;
using System.Drawing;

namespace OpenCAGE
{
    /// <summary>
    /// Shared entity clipboard used by both the flowgraph UI and the Level Viewer viewport.
    /// Stores references (composite + entity ids) which are resolved at paste time, so the
    /// clipboard survives page reloads and navigation. A clipboard entry exists per copied
    /// node - the same entity can appear multiple times with different offsets.
    /// </summary>
    public static class EntityClipboard
    {
        public class Entry
        {
            public uint EntityId;
            /// <summary>Node position relative to the top-left of the copied selection (zero for viewport copies).</summary>
            public Point Offset;
            /// <summary>Pins present on the copied node, so pastes recreate the same pin layout.
            /// Null when the copy didn't come from a flowgraph node (viewport / entity list).</summary>
            public List<PinMeta> Pins;
        }

        /// <summary>A pin on a copied node (PinLocation/PinStyle stored as bytes to stay UI-agnostic).</summary>
        public class PinMeta
        {
            public uint ParameterId;
            public byte Location;
            public byte Style;
        }

        /// <summary>One step of the drill path active when the copy was taken: the composite the
        /// user was in and the instance entity they stepped through to go deeper.</summary>
        public class PathStep
        {
            public uint CompositeId;
            public uint InstanceEntityId;
        }

        public static uint SourceCompositeId { get; private set; }
        public static List<Entry> Entries { get; private set; } = new List<Entry>();
        /// <summary>Drill path (root-most first) leading down to the source composite. Used to build
        /// aliases when the clipboard is reference-pasted into an ancestor composite.</summary>
        public static List<PathStep> SourcePath { get; private set; } = new List<PathStep>();

        public static bool HasContent => Entries.Count != 0;

        public static void Set(uint sourceCompositeId, List<Entry> entries, List<PathStep> sourcePath = null)
        {
            SourceCompositeId = sourceCompositeId;
            Entries = entries ?? new List<Entry>();
            SourcePath = sourcePath ?? new List<PathStep>();
        }

        public static void Clear()
        {
            SourceCompositeId = 0;
            Entries = new List<Entry>();
            SourcePath = new List<PathStep>();
        }
    }
}

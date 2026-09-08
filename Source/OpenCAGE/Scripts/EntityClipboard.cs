using CATHODE.Scripting;
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

            /* An alias is a pointer at an entity somewhere below, so a copy of one is a copy of a
               pointer: cloning it makes a second alias holding the same path, which claims the same
               overrides as the first and puts nothing new in the world. What was picked is the entity
               at the end of the path, so a copy of an alias records that too, and a clone paste copies
               it instead. The entry still names the alias itself, so a reference paste can still give
               that alias another node. */
            /// <summary>The entity an aliased entry resolves to, and the composite that owns it. Zero when
            /// the entry is not an alias, or its path no longer resolves.</summary>
            public uint ResolvedEntityId;
            public uint ResolvedCompositeId;
            /// <summary>Where the resolved entity sits as seen from the copying composite - its own
            /// position is relative to the composite that owns it, several instances down - so a clone
            /// lifted up here can be put where the original looks like it is.</summary>
            public cTransform ResolvedPlacement;

            public bool HasResolvedTarget => ResolvedEntityId != 0 && ResolvedCompositeId != 0;
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

        static EntityClipboard()
        {
            //Composite/entity ids are name hashes, so a clipboard taken in one level will happily
            //resolve against a shared library composite in the next one and paste the wrong thing.
            //Nothing here holds an Entity, but the ids still have to go when the level does.
            Singleton.OnLevelLoaded += OnLevelLoaded;
        }

        private static void OnLevelLoaded(LevelContent content)
        {
            Clear();
        }

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

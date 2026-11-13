using ST.Library.UI.NodeEditor;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using static CathodeLib.CompositeFlowgraphTable;

namespace OpenCAGE
{
    /// <summary>A flowgraph page's comment boxes, between the node editor and the page's saved layout.</summary>
    public static class FlowgraphComments
    {
        /// <summary>The colours a box can be given from its menu (Empty: the editor's own).</summary>
        public static readonly (string Name, Color Colour)[] Presets =
        {
            ("Default", Color.Empty),
            ("Grey", Color.FromArgb(128, 128, 128)),
            ("Red", Color.FromArgb(190, 70, 70)),
            ("Orange", Color.FromArgb(205, 125, 50)),
            ("Yellow", Color.FromArgb(200, 180, 60)),
            ("Green", Color.FromArgb(80, 160, 80)),
            ("Teal", Color.FromArgb(50, 150, 150)),
            ("Blue", Color.FromArgb(60, 110, 200)),
            ("Purple", Color.FromArgb(140, 90, 190)),
            ("Pink", Color.FromArgb(200, 90, 150)),
        };

        /// <summary>The page's boxes into its layout.</summary>
        public static void Save(STNodeEditor editor, FlowgraphMeta meta)
        {
            meta.Comments = editor.Comments.Select(ToMeta).ToList();
        }

        /// <summary>The layout's boxes onto the page, in place of any it had.</summary>
        public static void Apply(STNodeEditor editor, FlowgraphMeta meta)
        {
            editor.SetComments((meta?.Comments ?? new List<FlowgraphMeta.CommentMeta>()).Where(o => o != null).Select(FromMeta));
        }

        public static FlowgraphMeta.CommentMeta ToMeta(STNodeComment comment)
        {
            return new FlowgraphMeta.CommentMeta() { Bounds = comment.Bounds, Title = comment.Title ?? "", Colour = comment.Colour };
        }

        public static STNodeComment FromMeta(FlowgraphMeta.CommentMeta comment)
        {
            return new STNodeComment() { Bounds = comment.Bounds, Title = comment.Title ?? "", Colour = comment.Colour };
        }

        /// <summary>A preset's name for a colour, or null when it is none of them.</summary>
        public static string PresetName(Color colour)
        {
            foreach ((string name, Color preset) in Presets)
            {
                if (preset.IsEmpty ? colour.IsEmpty : !colour.IsEmpty && preset.ToArgb() == colour.ToArgb())
                    return name;
            }
            return null;
        }
    }
}

using System.Drawing;

namespace OpenCAGE.Theming
{
    /// <summary>
    /// The dark palette.
    ///
    /// Deliberately anchored on Visual Studio 2015 Dark, because that is the palette DockPanelSuite's
    /// VS2015DarkTheme paints the docking chrome with. The tab strips, tool window captions and menu
    /// bars all come from that theme, so if the forms inside them used a palette of their own invention
    /// the result reads as two applications stitched together - which is most of what makes a
    /// hand-rolled dark mode look bodged.
    ///
    /// Backgrounds run in tiers, and controls are assigned one by role rather than everything landing on
    /// a single flat grey: Window sits behind documents, Surface behind forms and tool windows, and
    /// Raised sits above them for command bars. Inputs are lighter still, so a text box reads as
    /// something you can type into without needing a border to say so.
    /// </summary>
    public static class ThemeColours
    {
        /// <summary>Behind documents and canvases - the darkest tier.</summary>
        public static readonly Color Window = Color.FromArgb(30, 30, 30);

        /// <summary>Form and tool window backgrounds.</summary>
        public static readonly Color Surface = Color.FromArgb(37, 37, 38);

        /// <summary>Command bars, menu strips, and panels that sit above a surface.</summary>
        public static readonly Color Raised = Color.FromArgb(45, 45, 48);

        /// <summary>Text boxes, combo boxes, lists - anything holding a value.</summary>
        public static readonly Color Input = Color.FromArgb(51, 51, 55);

        /// <summary>
        /// Every other row in a list. Deliberately a small step from <see cref="Input"/> - enough to
        /// follow a row across to a distant column, not so much that it competes with the selection.
        /// </summary>
        public static readonly Color InputAlternate = Color.FromArgb(59, 59, 64);

        /// <summary>An input that can't be edited.</summary>
        public static readonly Color InputDisabled = Color.FromArgb(42, 42, 44);

        /// <summary>The standard 1px divider and control outline.</summary>
        public static readonly Color Border = Color.FromArgb(63, 63, 70);

        /// <summary>A border that needs to carry more weight, e.g. a focused input.</summary>
        public static readonly Color BorderStrong = Color.FromArgb(84, 84, 90);

        public static readonly Color Text = Color.FromArgb(241, 241, 241);

        /// <summary>Secondary text - captions, hints, units.</summary>
        public static readonly Color TextDim = Color.FromArgb(158, 158, 158);

        public static readonly Color TextDisabled = Color.FromArgb(109, 109, 109);

        /// <summary>Selection and focus.</summary>
        public static readonly Color Accent = Color.FromArgb(0, 122, 204);

        public static readonly Color AccentHover = Color.FromArgb(28, 151, 234);

        /// <summary>Selected rows in a list, where full accent would be too loud.</summary>
        public static readonly Color Selection = Color.FromArgb(9, 71, 113);

        /// <summary>Selected rows in a list that no longer has focus.</summary>
        public static readonly Color SelectionInactive = Color.FromArgb(63, 63, 70);

        public static readonly Color Hover = Color.FromArgb(62, 62, 64);

        /// <summary>Pressed state for buttons and toggles.</summary>
        public static readonly Color Pressed = Color.FromArgb(0, 122, 204);

        /// <summary>List and grid column headers.</summary>
        public static readonly Color Header = Color.FromArgb(45, 45, 48);

        /// <summary>Links.</summary>
        public static readonly Color Link = Color.FromArgb(86, 156, 214);
    }
}

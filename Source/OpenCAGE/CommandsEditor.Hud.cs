using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>Menu entry for the HUD Overlay Builder (HudOverlayBuilder).</summary>
    public partial class CommandsEditor
    {
        /* Adds HUD Overlay Builder to the menu after Level UI Overrides (or the UI Editor), for the open level */
        private void SetUpHudBuilder()
        {
            if (!(uIToolStripMenuItem.OwnerItem is ToolStripDropDownItem parent))
                return;
            ToolStripMenuItem hud = new ToolStripMenuItem("HUD Overlay Builder")
            {
                ToolTipText = "The open level's own HUD elements (timers, bars, counters, objective text, reticles): a movie of the level's own and the script that drives it.",
            };
            hud.Click += (s, e) => HudOverlayBuilder.ShowFor(this, CompositeBrowser?.Content?.Level?.Name);
            ToolStripItem after = parent.DropDownItems.OfType<ToolStripItem>().FirstOrDefault(o => o.Text == "Level UI Overrides") ?? uIToolStripMenuItem;
            parent.DropDownItems.Insert(parent.DropDownItems.IndexOf(after) + 1, hud);
        }
    }
}

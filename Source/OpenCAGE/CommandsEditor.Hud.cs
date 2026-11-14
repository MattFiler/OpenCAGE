using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>Menu entry for the HUD Overlay Builder (HudOverlayBuilder).</summary>
    public partial class CommandsEditor
    {
        /* View > Level Config > HUD Overlay Builder, for the open level */
        private ToolStripMenuItem HudBuilderMenuItem()
        {
            ToolStripMenuItem hud = new ToolStripMenuItem("HUD Overlay Builder")
            {
                ToolTipText = "The open level's own HUD elements (timers, bars, counters, objective text, reticles): a movie of the level's own and the script that drives it.",
            };
            hud.Click += (s, e) => HudOverlayBuilder.ShowFor(this, CompositeBrowser?.Content?.Level?.Name);
            return hud;
        }
    }
}

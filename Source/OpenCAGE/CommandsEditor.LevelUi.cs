using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>Startup and menu wiring for a level's own UI movies (LevelUiOverrides).</summary>
    public partial class CommandsEditor
    {
        /* On startup, off the UI thread, removes level UI movies a previous launch left in place (OpenCAGE closed before the game);
           and adds Level UI Overrides to the menu after the UI Editor, for the open level */
        private void SetUpLevelUi()
        {
            if (Singleton.IsPrimaryInstance)
            {
                string pathToAI = Singleton.PathToAI;
                Task.Run(() => LevelUiOverrides.PutBackAtStartup(pathToAI));
            }

            if (uIToolStripMenuItem.OwnerItem is ToolStripDropDownItem parent)
            {
                ToolStripMenuItem levelUi = new ToolStripMenuItem("Level UI Overrides")
                {
                    ToolTipText = "The open level's own versions of the game's UI movies (radial menu, HUD, motion tracker, pause menu...), used while it is played.",
                };
                levelUi.Click += (s, e) => LevelUiOverridesEditor.ShowFor(this, CompositeBrowser?.Content?.Level?.Name);
                parent.DropDownItems.Insert(parent.DropDownItems.IndexOf(uIToolStripMenuItem) + 1, levelUi);
            }
        }
    }
}

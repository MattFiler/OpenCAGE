using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>Startup and menu wiring for a level's own UI movies (LevelUiOverrides), and View > Level Config.</summary>
    public partial class CommandsEditor
    {
        /* On startup, off the UI thread, removes level UI movies a previous launch left in place (OpenCAGE closed before the game);
           and adds View > Level Config after Configuration Editors */
        private void SetUpLevelUi()
        {
            if (Singleton.IsPrimaryInstance)
            {
                string pathToAI = Singleton.PathToAI;
                Task.Run(() => LevelUiOverrides.PutBackAtStartup(pathToAI));
            }
            SetUpLevelConfigMenu();
        }

        /* View > Level Config: what belongs to one level rather than the whole game - its UI movies, its HUD overlays and the text
           databases it loads */
        private void SetUpLevelConfigMenu()
        {
            if (!(configurationsToolStripMenuItem.OwnerItem is ToolStripDropDownItem view))
                return;

            ToolStripMenuItem levelConfig = new ToolStripMenuItem("Level Config") { Name = "levelConfigToolStripMenuItem" };

            ToolStripMenuItem levelUi = new ToolStripMenuItem("Level UI Overrides")
            {
                ToolTipText = "The open level's own versions of the game's UI movies (radial menu, HUD, motion tracker, pause menu...), used while it is played.",
            };
            levelUi.Click += (s, e) => LevelUiOverridesEditor.ShowFor(this, CompositeBrowser?.Content?.Level?.Name);
            levelConfig.DropDownItems.Add(levelUi);
            levelConfig.DropDownItems.Add(HudBuilderMenuItem());

            //The text databases each level loads, moved here from Configuration Editors > Core Game
            if (levelTextDBsToolStripMenuItem.OwnerItem is ToolStripDropDownItem coreGame)
                coreGame.DropDownItems.Remove(levelTextDBsToolStripMenuItem);
            levelConfig.DropDownItems.Add(levelTextDBsToolStripMenuItem);

            view.DropDownItems.Insert(view.DropDownItems.IndexOf(configurationsToolStripMenuItem) + 1, levelConfig);
        }
    }
}

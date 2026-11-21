using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>Startup and menu wiring for a level's launch settings (LevelLaunch): View > Level Config > Level Launch Settings.</summary>
    public partial class CommandsEditor
    {
        /* On startup, off the UI thread, puts back what a previous launch left in AI.exe (OpenCAGE closed before the game); and adds the
           menu item under Level Config, after Level UI Overrides */
        private void SetUpLevelLaunch()
        {
            if (Singleton.IsPrimaryInstance)
            {
                string pathToAI = Singleton.PathToAI;
                Task.Run(() => LevelLaunch.PutBackAtStartup(pathToAI));
            }
            if (!(configurationsToolStripMenuItem.OwnerItem is ToolStripDropDownItem view))
                return;
            ToolStripMenuItem levelConfig = view.DropDownItems.Find("levelConfigToolStripMenuItem", false).OfType<ToolStripMenuItem>().FirstOrDefault();
            if (levelConfig == null)
                return;
            ToolStripMenuItem launchSettings = new ToolStripMenuItem("Level Launch Settings")
            {
                ToolTipText = "Settings the open level needs from the game while it is played: texture memory, whether characters collide with each other, its loading movies, and the name its saves show.",
            };
            launchSettings.Click += (s, e) => LevelLaunchSettingsEditor.ShowFor(this, CompositeBrowser?.Content?.Level?.Name);
            levelConfig.DropDownItems.Insert(System.Math.Min(1, levelConfig.DropDownItems.Count), launchSettings);
        }
    }
}

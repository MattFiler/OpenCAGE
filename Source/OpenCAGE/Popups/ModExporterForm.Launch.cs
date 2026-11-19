#if ENABLE_MOD_PACKAGES
using OpenCAGE.Modding;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE.Popups
{
    /// <summary>
    /// Create a Mod's "Launch Mod starts it" choices (ModPackageInfo.Launch): the level the game starts in, and the launch options
    /// it plays with. Left as they start - not set, and each option the player's own - the package says nothing.
    /// </summary>
    public partial class ModExporterForm
    {
        private readonly List<CheckBox> _launchOptions = new List<CheckBox>();

        //The level picker's first two entries (listed in ModExporterForm.Designer.cs)
        private const string LaunchNotSet = "Not set (its only level, or the player picks)";
        private const string LaunchMenu = "The main menu";

        /* The levels to start in, and a tick box for each launch option. The picker is named for UI automation (launchLevel). */
        private void FillLaunchChoice()
        {
            //The open level first: most likely the mod's
            string open = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Name;
            List<string> levels = new List<string>();
            try { levels = EditorUtils.GetEditableLevels(); }
            catch { }
            if (open != null && levels.Remove(open.ToUpperInvariant()) | levels.Remove(open))
                levels.Insert(0, open);
            launchLevel.Items.AddRange(levels.Cast<object>().ToArray());
            launchLevel.SelectedIndex = 0;

            foreach (ModLaunch.Option option in ModLaunch.Options)
            {
                CheckBox box = new CheckBox() { Text = option.Label, AutoSize = true, ThreeState = true, CheckState = CheckState.Indeterminate, Tag = option.Name };
                _tips.SetToolTip(box, "Launch Mod plays it with " + option.Label + " on (ticked) or off (unticked); the square: as the player has it in Launch Game.");
                _launchOptionsRow.Controls.Add(box);
                _launchOptions.Add(box);
            }
        }

        /* What the package says about launching it: null when nothing was chosen */
        private ModLaunchInfo LaunchChoice()
        {
            if (launchLevel == null)
                return null;
            string picked = launchLevel.SelectedItem as string;
            Dictionary<string, bool> options = _launchOptions.Where(o => o.CheckState != CheckState.Indeterminate)
                .ToDictionary(o => (string)o.Tag, o => o.CheckState == CheckState.Checked);
            string level = picked == null || picked == LaunchNotSet ? null : picked == LaunchMenu ? ModLaunch.Menu : picked;
            if (level == null && options.Count == 0)
                return null;
            return new ModLaunchInfo() { Level = level, Options = options.Count != 0 ? options : null };
        }

        /* A new version of an installed mod launches as the old one did */
        private void ChooseLaunchOf(ModState.InstalledMod mod)
        {
            if (launchLevel == null)
                return;
            ModLaunchInfo launch = null;
            try { launch = ModServices.Installer.OpenPackage(mod).Info.Launch; }
            catch { }
            int index = 0;
            if (launch?.Level != null)
            {
                if (ModLaunch.IsMenu(launch.Level))
                    index = 1;
                else
                {
                    index = launchLevel.Items.Cast<object>().ToList().FindIndex(o => string.Equals(o as string, launch.Level, StringComparison.OrdinalIgnoreCase));
                    if (index < 0)
                        index = launchLevel.Items.Add(launch.Level);
                }
            }
            launchLevel.SelectedIndex = index;
            foreach (CheckBox box in _launchOptions)
                box.CheckState = launch?.Options != null && launch.Options.TryGetValue((string)box.Tag, out bool on) ? (on ? CheckState.Checked : CheckState.Unchecked) : CheckState.Indeterminate;
        }
    }
}
#endif

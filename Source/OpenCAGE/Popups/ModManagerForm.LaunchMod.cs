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
    /// Launch Mod: start the game for the selected mod (ModLaunch) - into the level it starts in, with that level's own UI movies
    /// and the launch options the mod asks for. Only for a mod that's installed as listed (ticked, applied, its files as installed).
    /// </summary>
    public partial class ModManagerForm
    {
        private Button _launchButton;

        private void AddLaunchButton(FlowLayoutPanel right)
        {
            _launchButton = MakeButton(right, "Launch Mod", (s, e) => LaunchSelectedMod());
            _launchButton.Enabled = false;
            //Named for UI automation
            _launchButton.Name = "launchModButton";
            if (string.IsNullOrEmpty(_modList.Name))
                _modList.Name = "modList";
            ToolTip tips = new ToolTip() { AutoPopDelay = 15000 };
            tips.SetToolTip(_launchButton, "Start the game for the selected mod: into the level it starts in (as its author set it, or its only level), with that level's own UI " +
                "(radial menu, HUD... - only while it's played) and the launch options the mod asks for. Your other Launch Game settings apply as usual.");
            FormClosed += (s, e) => tips.Dispose();
            _modList.SelectedIndexChanged += (s, e) => UpdateLaunchButton();
            _modList.EnabledChanged += (s, e) => UpdateLaunchButton();
        }

        /* Launchable: the selected mod installed as the list shows it, with nothing left to apply */
        private void UpdateLaunchButton()
        {
            if (_launchButton == null || IsDisposed)
                return;
            ModState.InstalledMod mod = SelectedMod;
            _launchButton.Enabled = mod != null && _modList.Enabled && mod.Enabled && mod.Applied.Count != 0 && !_pending;
        }

        private void LaunchSelectedMod()
        {
            ModState.InstalledMod mod = SelectedMod;
            if (mod == null)
                return;
            if (_pending)
            {
                MessageBox.Show("You have changes that aren't applied yet: click Apply first, then Launch Mod.", "Launch Mod", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string notReady = ModLaunch.NotReady(mod);
            if (notReady != null)
            {
                MessageBox.Show("The game can't be launched for this mod yet: " + notReady + ".", "Launch Mod", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ModPackageInfo info;
            try
            {
                info = ModServices.Installer.OpenPackage(mod).Info;
            }
            catch (Exception e)
            {
                MessageBox.Show("The mod's package couldn't be read: " + e.Message, "Launch Mod", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string level = ModLaunch.StartLevel(info, out List<string> choices) ?? PickLevel(mod.Name, choices);
            if (level == null)
                return;

            ModLaunch.Result result;
            Cursor = Cursors.WaitCursor;
            try
            {
                result = ModLaunch.Launch(level, info.Launch?.Options);
            }
            catch (Exception e)
            {
                MessageBox.Show("The game couldn't be launched for '" + mod.Name + "': " + e.Message, "Launch Mod", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            finally
            {
                Cursor = Cursors.Default;
            }
            _statusLabel.Text = "Launched '" + mod.Name + "' " + (ModLaunch.IsMenu(level) ? "at the main menu" : "into " + result.Level) +
                (result.LevelUi.Count != 0 ? ", with its own " + string.Join(", ", result.LevelUi.Select(o => o.Name)) : "") + ".";
            if (result.Problems.Count != 0)
                MessageBox.Show("The game was started, but not everything could be set up:\n\n" + string.Join("\n\n", result.Problems), "Launch Mod", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        /* The level to start a mod in, when it has several and didn't say: the player picks (null: cancelled) */
        private string PickLevel(string modName, List<string> levels)
        {
            using (Form dialog = new Form()
            {
                Text = "Launch Mod",
                Icon = SharedFormIcon.Icon,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                ClientSize = new Size(380, 112),
            })
            {
                Label text = new Label() { Text = "'" + modName + "' has several levels. Which one should the game start in?", Location = new Point(12, 10), Size = new Size(356, 32) };
                ComboBox picker = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(12, 44), Width = 356 };
                picker.Items.AddRange(levels.Cast<object>().ToArray());
                picker.Items.Add("The main menu");
                picker.SelectedIndex = 0;
                Button ok = new Button() { Text = "Launch", DialogResult = DialogResult.OK, Location = new Point(212, 76), Width = 75 };
                Button cancel = new Button() { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(293, 76), Width = 75 };
                dialog.Controls.AddRange(new Control[] { text, picker, ok, cancel });
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;
                Theming.ThemeManager.ApplyToForm(dialog);
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return null;
                return picker.SelectedIndex < levels.Count ? levels[picker.SelectedIndex] : ModLaunch.Menu;
            }
        }
    }
}
#endif

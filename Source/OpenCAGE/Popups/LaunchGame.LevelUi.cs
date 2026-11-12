using System;
using System.Drawing;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Launch Game's line about the launched level's own UI movies (LevelUiOverrides), under the UI tweaks: which movies it
    /// brings, and which tweak each is used over; Edit opens the level's UI overrides.
    /// </summary>
    public partial class LaunchGame
    {
        private Label _levelUiLine;
        private LinkLabel _levelUiEdit;
        private readonly ToolTip _levelUiTips = new ToolTip() { AutoPopDelay = 20000 };

        private const string LevelUiTip = "A level can bring its own versions of the game's UI movies - the radial menu, the HUD, the motion tracker's screen, " +
            "the pause menu and more - kept with it in DATA\\ENV\\<level>\\UI\\. A launch into the level uses them, and they come out again once the game " +
            "closes or anything else is launched, so every other level keeps the game's own UI. Where the level brings a movie a UI tweak above also " +
            "replaces, the level's is used.";

        /* A row added below the UI tweaks' two rows, the group and the window grown by one row (measured from the rows the
           designer laid out, so it scales as they do) */
        private void AddLevelUiLine()
        {
            int row = debugLoadingScreen.Top - debugCheckpoints.Top;
            _levelUiEdit = new LinkLabel() { AutoSize = true, Text = "Edit..." };
            _levelUiEdit.Location = new Point(uiTweaksGroup.ClientSize.Width - _levelUiEdit.PreferredWidth - debugCheckpoints.Left, debugLoadingScreen.Top + row);
            _levelUiLine = new Label()
            {
                AutoSize = false,
                AutoEllipsis = true,
                Location = new Point(debugCheckpoints.Left, debugLoadingScreen.Top + row),
                Size = new Size(_levelUiEdit.Left - debugCheckpoints.Left - 6, debugLoadingScreen.Height),
            };
            _levelUiEdit.LinkClicked += (s, e) => LevelUiOverridesEditor.ShowFor(this, LaunchedLevel());
            uiTweaksGroup.Height += row;
            OpenGame.Top += row;
            ClientSize = new Size(ClientSize.Width, ClientSize.Height + row);
            uiTweaksGroup.Controls.Add(_levelUiLine);
            uiTweaksGroup.Controls.Add(_levelUiEdit);
            Theming.ThemeManager.ApplyCurrent(_levelUiLine);
            Theming.ThemeManager.ApplyCurrent(_levelUiEdit);

            _levelUiTips.SetToolTip(_levelUiEdit, "The launched level's own UI movies: add one, export the game's as a starting point, or take one out.");
            FormClosed += (s, e) => _levelUiTips.Dispose();

            levelList.SelectedIndexChanged += (s, e) => UpdateLevelUiLine();
            loadToLevel.CheckedChanged += (s, e) => UpdateLevelUiLine();
            foreach (CheckBox tweak in new CheckBox[] { debugCheckpoints, levelSelect, debugLoadingScreen, quitToMenuOnDeath })
                tweak.CheckedChanged += (s, e) => UpdateLevelUiLine();
            //Back from the editor (or from changing the level's files by hand)
            Activated += (s, e) => UpdateLevelUiLine();
            UpdateLevelUiLine();
        }

        /* The level a launch from here starts in: the one picked, or the main menu's */
        private string LaunchedLevel()
        {
            return loadToLevel.Checked && levelList.SelectedIndex >= 0 ? levelList.Items[levelList.SelectedIndex].ToString() : EditorUtils.FrontendLevel;
        }

        private void UpdateLevelUiLine()
        {
            if (_levelUiLine == null || IsDisposed)
                return;
            string text = null;
            try
            {
                text = LevelUiOverrides.Describe(Singleton.PathToAI, LaunchedLevel());
            }
            catch { }
            _levelUiLine.Text = text ?? (loadToLevel.Checked ? "Level UI: the game's own (this level brings none)" : "Level UI: the game's own");
            _levelUiTips.SetToolTip(_levelUiLine, _levelUiLine.Text + "\n\n" + LevelUiTip);
        }
    }
}

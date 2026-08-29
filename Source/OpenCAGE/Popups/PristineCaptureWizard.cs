#if ENABLE_MOD_PACKAGES
using CathodeLib;
using OpenCAGE.Modding;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE.Popups
{
    /* The way out of a modified install without losing the modifications:
     *
     *   1. snapshot every non-vanilla file
     *   2. the user verifies their game files through their store, restoring true vanilla data
     *   3. OpenCAGE captures pristine copies of those files into the baseline store
     *   4. the snapshot is put back
     *
     * After this the install looks exactly as it did, but every modified file has a pristine
     * counterpart on disk - so mods can be exported as small patches against vanilla, and
     * everything can always be cleanly restored. */
    public class PristineCaptureWizard : Form
    {
        private readonly ScanResult _scan;
        private readonly List<string> _paths;
        private string _snapshotId;

        private Label _headline;
        private TextBox _body;
        private ListView _fileList;
        private Button _actionButton;
        private Button _secondaryButton;
        private Button _closeButton;

        private int _step = 0;

        public PristineCaptureWizard(ScanResult scan)
        {
            _scan = scan;
            _paths = scan.WithStatus(FileStatus.Modified).Concat(scan.WithStatus(FileStatus.Foreign)).OrderBy(o => o).ToList();

            Text = "Capture Pristine Data";
            Icon = SharedFormIcon.Icon;
            Size = new Size(760, 560);
            MinimumSize = new Size(620, 420);
            StartPosition = FormStartPosition.CenterParent;

            _headline = new Label() { Dock = DockStyle.Top, Height = 30, Font = new Font(Font, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 10, 0) };
            _body = new TextBox() { Dock = DockStyle.Top, Height = 110, Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None };
            _fileList = new ListView() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.Nonclickable };
            _fileList.Columns.Add("File", 480);
            _fileList.Columns.Add("State", 140);

            FlowLayoutPanel buttons = new FlowLayoutPanel() { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
            _closeButton = new Button() { Text = "Cancel", AutoSize = true, Height = 28 };
            _secondaryButton = new Button() { Text = "", AutoSize = true, Height = 28, Visible = false };
            _actionButton = new Button() { Text = "", AutoSize = true, Height = 28 };
            _closeButton.Click += (s, e) => Close();
            _actionButton.Click += (s, e) => Advance(false);
            _secondaryButton.Click += (s, e) => Advance(true);
            buttons.Controls.Add(_closeButton);
            buttons.Controls.Add(_secondaryButton);
            buttons.Controls.Add(_actionButton);

            Padding = new Padding(10);
            Controls.Add(_fileList);
            Controls.Add(_body);
            Controls.Add(_headline);
            Controls.Add(buttons);
            Theming.ThemeManager.ApplyToForm(this);

            ShowStep();
        }

        private void FillFileList(Func<string, string> stateOf)
        {
            _fileList.BeginUpdate();
            _fileList.Items.Clear();
            foreach (string path in _paths)
            {
                ListViewItem item = new ListViewItem(path);
                item.SubItems.Add(stateOf == null ? "" : stateOf(path));
                _fileList.Items.Add(item);
            }
            _fileList.EndUpdate();
        }

        private void ShowStep()
        {
            switch (_step)
            {
                case 0:
                    _headline.Text = "Step 1 of 4 - what will happen";
                    _body.Text = _paths.Count == 0
                        ? "Every shipped file already matches vanilla - there is nothing to capture. You can close this window."
                        : "These " + _paths.Count + " files differ from the shipped game (your edits, or hand-installed mods).\r\n\r\n"
                        + "OpenCAGE will snapshot them all, then ask you to verify your game files through your store - which restores true vanilla data. "
                        + "Pristine copies are then captured, and your snapshot is put straight back. Your install ends up exactly as it is now, plus a stored vanilla baseline for every one of these files.";
                    FillFileList(o => _scan.Files.ContainsKey(o) ? _scan.Files[o].ToString() : "");
                    _actionButton.Text = "Snapshot my files && continue";
                    _actionButton.Enabled = _paths.Count != 0;
                    _secondaryButton.Visible = false;
                    break;

                case 1:
                    _headline.Text = "Step 2 of 4 - verify your game files";
                    _body.Text = "Your files are snapshotted (kept under DATA/MODTOOLS/MODS).\r\n\r\n" + VerifyInstructions()
                        + "\r\n\r\nWhen the verification has finished downloading, come back here and continue.";
                    _actionButton.Text = "I've verified - check my files";
                    _secondaryButton.Text = PlatformIsSteam() ? "Open Steam verification" : "";
                    _secondaryButton.Visible = PlatformIsSteam();
                    break;

                case 2:
                    //handled inline by Advance (does work, then moves to 3)
                    break;

                case 3:
                    _headline.Text = "Step 4 of 4 - done";
                    _actionButton.Text = "Close";
                    _secondaryButton.Visible = false;
                    _closeButton.Visible = false;
                    break;
            }
        }

        private void Advance(bool secondary)
        {
            switch (_step)
            {
                case 0:
                    {
                        Exception error = null;
                        using (BusyDialog busy = new BusyDialog("Snapshotting " + _paths.Count + " files..."))
                        {
                            busy.Work = () =>
                            {
                                try { _snapshotId = ModServices.Installer.CreateSnapshot("pre-verify snapshot", _paths); }
                                catch (Exception e) { error = e; }
                            };
                            busy.ShowDialog(this);
                        }
                        if (error != null)
                        {
                            MessageBox.Show("Snapshot failed: " + error.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                        _step = 1;
                        ShowStep();
                        break;
                    }

                case 1:
                    if (secondary)
                    {
                        try { Process.Start("steam://validate/214490"); }
                        catch (Exception e) { MessageBox.Show("Couldn't ask Steam to verify: " + e.Message); }
                        return;
                    }
                    RunCheckAndCapture();
                    break;

                case 3:
                    Close();
                    break;
            }
        }

        private void RunCheckAndCapture()
        {
            int nowVanilla = 0, captured = 0, restored = 0;
            List<string> stillModified = new List<string>();
            Exception error = null;

            using (BusyDialog busy = new BusyDialog("Checking files and capturing pristine copies..."))
            {
                busy.Work = () =>
                {
                    try
                    {
                        ModInstaller installer = ModServices.Installer;
                        HashCache cache = ModServices.Cache;
                        VanillaManifest manifest = ModServices.Manifest;

                        foreach (string path in _paths)
                        {
                            cache.Invalidate(path);
                            byte[] hash = cache.Hash(path);
                            bool vanilla = hash != null && manifest.IsVanilla(path, hash);
                            if (!vanilla && hash == null && !manifest.Contains(path))
                                vanilla = true; //a foreign file the verify removed: vanilla state is absence
                            if (vanilla)
                            {
                                nowVanilla++;
                                installer.CaptureVanillaBaseline(path, false);
                                if (ModServices.State.Baseline.ContainsKey(path) && ModServices.State.Baseline[path].IsVanilla)
                                    captured++;
                            }
                            else
                                stillModified.Add(path);
                        }
                        installer.SaveState();

                        //Their bytes go straight back
                        installer.RestoreSnapshot(_snapshotId);
                        restored = _paths.Count;
                    }
                    catch (Exception e) { error = e; }
                };
                busy.ShowDialog(this);
            }

            if (error != null)
            {
                MessageBox.Show("Something went wrong: " + error.Message + "\n\nYour snapshot is kept at DATA/MODTOOLS/MODS/SNAPSHOTS and nothing has been lost.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (stillModified.Count == _paths.Count)
            {
                MessageBox.Show("None of the files came back vanilla - it doesn't look like the verification ran (or it hasn't finished). "
                    + "Your files are untouched. Verify through your store and try again.", "Nothing captured", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _step = 3;
            ShowStep();
            _body.Text = "Captured pristine copies of " + captured + " file" + (captured == 1 ? "" : "s") + " and restored your "
                + restored + " snapshotted file" + (restored == 1 ? "" : "s") + " - your install is exactly as it was."
                + (stillModified.Count != 0
                    ? "\r\n\r\n" + stillModified.Count + " file" + (stillModified.Count == 1 ? "" : "s") + " still didn't match vanilla after the verify (listed below) - these may be files your store version simply doesn't ship."
                    : "\r\n\r\nEvery modified file now has a pristine baseline: your work can be exported as small patches, and mods can always be cleanly removed.");
            _paths.Clear();
            _paths.AddRange(stillModified);
            FillFileList(o => "still modified");
        }

        private static bool PlatformIsSteam()
        {
            return Singleton.Platform == PatchManager.Platform.STEAM;
        }

        private static string VerifyInstructions()
        {
            switch (Singleton.Platform)
            {
                case PatchManager.Platform.STEAM:
                    return "Use the button below (or Steam: right-click Alien: Isolation -> Properties -> Installed Files -> Verify integrity of game files).";
                case PatchManager.Platform.EPIC_GAMES_STORE:
                    return "In the Epic Games launcher: click the '...' next to Alien: Isolation and choose Verify.";
                case PatchManager.Platform.GOG:
                    return "In GOG Galaxy: select Alien: Isolation -> settings icon -> Manage installation -> Verify / Repair.";
                default:
                    return "Verify / repair the game's files through the store you installed it from.";
            }
        }
    }
}
#endif

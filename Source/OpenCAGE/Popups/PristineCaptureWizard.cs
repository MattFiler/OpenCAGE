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
    /* The way out of a changed install without losing the changes, for files OpenCAGE has no original of:
     *
     *   1. keep a copy of every changed file (the user's own and those installed mods wrote)
     *   2. the user verifies their game files through their store, which puts the shipped files back
     *   3. OpenCAGE keeps copies of those originals
     *   4. the copies from step 1 go straight back
     *
     * After this the install looks exactly as it did, but OpenCAGE holds the original of every changed file - so the
     * user's changes can be combined with mods, exported as small patches, and always cleanly put back. */
    public class PristineCaptureWizard : Form
    {
        private readonly ScanResult _scan;
        /* Files without an original: what this is for */
        private readonly List<string> _paths;
        /* Everything a store's file check would undo, so all of it goes back afterwards */
        private readonly List<string> _keep;
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
            ModInstaller installer = ModServices.Installer;
            List<string> changed = scan.WithStatus(FileStatus.Modified).Concat(scan.WithStatus(FileStatus.Missing)).ToList();
            _paths = changed.Where(o => !ModToolkit.IsRegenerated(o) && (installer == null || !installer.HasOriginal(o))).OrderBy(o => o).ToList();
            _keep = changed.Concat(scan.WithStatus(FileStatus.Managed)).Distinct().OrderBy(o => o).ToList();

            Text = "Keep Your Changes Safe";
            Icon = SharedFormIcon.Icon;
            Size = new Size(760, 560);
            MinimumSize = new Size(620, 420);
            StartPosition = FormStartPosition.CenterParent;

            _headline = new Label() { Dock = DockStyle.Top, Height = 30, Font = new Font(Font, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 10, 0) };
            _body = new TextBox() { Dock = DockStyle.Top, Height = 150, Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None };
            _fileList = new ListView() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.Nonclickable };
            _fileList.Columns.Add("File", 480);
            _fileList.Columns.Add("", 140);

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
                    _headline.Text = "Step 1 of 3 - what this does";
                    _body.Text = _paths.Count == 0
                        ? "OpenCAGE already has the original of every game file you've changed - there's nothing to do. You can close this window."
                        : (_paths.Count == 1 ? "This game file was" : "These " + _paths.Count + " game files were") + " changed before OpenCAGE started keeping the originals (by you, or by a mod installed by hand). "
                        + "Without the originals, mods can't be combined with these changes, and removing mods can't put everything back exactly.\r\n\r\n"
                        + "This fixes that in a few minutes:\r\n"
                        + "  1. OpenCAGE keeps a copy of your game files as they are now.\r\n"
                        + "  2. You ask " + StoreName() + " to check your game files, which puts the original versions back.\r\n"
                        + "  3. OpenCAGE keeps copies of the originals, then puts your files straight back.\r\n\r\n"
                        + "Your game ends up exactly as it is now. Nothing is lost.";
                    FillFileList(o => _scan.Files.TryGetValue(o, out FileStatus status) && status == FileStatus.Missing ? "deleted" : "changed");
                    _actionButton.Text = "Copy my files && continue";
                    _actionButton.Enabled = _paths.Count != 0;
                    _secondaryButton.Visible = false;
                    break;

                case 1:
                    _headline.Text = "Step 2 of 3 - check your game files";
                    _body.Text = "Your files are copied.\r\n\r\nNow " + VerifyInstructions()
                        + "\r\n\r\nWhen it has finished (it may download a few files), come back here and click Continue.";
                    _actionButton.Text = "It's finished - continue";
                    _secondaryButton.Text = PlatformIsSteam() ? "Ask Steam to check them" : "";
                    _secondaryButton.Visible = PlatformIsSteam();
                    break;

                case 3:
                    _headline.Text = "Step 3 of 3 - done";
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
                        using (BusyDialog busy = new BusyDialog("Copying your files..."))
                        {
                            busy.Work = report =>
                            {
                                try { _snapshotId = ModServices.Installer.CreateSnapshot("your files before checking the game", _keep); }
                                catch (Exception e) { error = e; }
                            };
                            busy.ShowDialog(this);
                        }
                        if (error != null)
                        {
                            MessageBox.Show("Your files couldn't be copied, so nothing else was done: " + error.Message, "Keep Your Changes Safe", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                        catch (Exception e) { MessageBox.Show("Steam couldn't be asked to check the files: " + e.Message, "Keep Your Changes Safe"); }
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
            int captured = 0;
            List<string> stillChanged = new List<string>();
            Exception error = null, restoreError = null;

            using (BusyDialog busy = new BusyDialog("Keeping copies of the originals..."))
            {
                busy.Work = report =>
                {
                    ModInstaller installer = ModServices.Installer;
                    try
                    {
                        HashCache cache = ModServices.Cache;
                        VanillaManifest manifest = ModServices.Manifest;
                        foreach (string path in _paths)
                        {
                            cache.Invalidate(path);
                            byte[] hash = cache.Hash(path);
                            if (hash != null && manifest.IsVanilla(path, hash))
                            {
                                installer.CaptureVanillaBaseline(path, false);
                                if (installer.HasOriginal(path))
                                    captured++;
                            }
                            else
                                stillChanged.Add(path);
                        }
                        installer.SaveState();
                    }
                    catch (Exception e) { error = e; }

                    //Whatever happened, their files go straight back
                    report("Putting your files back...");
                    try { installer.RestoreSnapshot(_snapshotId, error == null && captured != 0); }
                    catch (Exception e) { restoreError = e; }
                };
                busy.ShowDialog(this);
            }

            if (restoreError != null)
            {
                MessageBox.Show("Your files couldn't be put back: " + restoreError.Message + "\n\nThey're still copied safely - close this, then open Keep Your Changes Safe from the Mod Manager and try again.",
                    "Keep Your Changes Safe", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (error != null)
            {
                MessageBox.Show("Something went wrong: " + error.Message + "\n\nYour files have been put back as they were - nothing was lost.", "Keep Your Changes Safe", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (captured == 0)
            {
                MessageBox.Show("None of the files were the originals yet - it looks like the check didn't run, or hasn't finished. "
                    + "Your files are back as they were. Check your game files and click Continue again.", "Not finished", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _step = 3;
            ShowStep();
            _body.Text = "OpenCAGE now has the original" + (captured == 1 ? "" : "s") + " of " + captured + " file" + (captured == 1 ? "" : "s") + ", and your game is back exactly as it was."
                + "\r\n\r\nMods are now combined with your changes, you can share your changes as a mod, and removing mods always puts things back."
                + (stillChanged.Count != 0
                    ? "\r\n\r\n" + stillChanged.Count + " file" + (stillChanged.Count == 1 ? " still wasn't the original" : "s still weren't the originals") + " after the check (listed below). "
                      + "Your version of the game may not include " + (stillChanged.Count == 1 ? "it" : "them") + ". You can run this again later."
                    : "");
            _paths.Clear();
            _paths.AddRange(stillChanged);
            FillFileList(o => "not the original");
        }

        private static bool PlatformIsSteam()
        {
            return Singleton.Platform == PatchManager.Platform.STEAM;
        }

        private static string StoreName()
        {
            switch (Singleton.Platform)
            {
                case PatchManager.Platform.STEAM: return "Steam";
                case PatchManager.Platform.EPIC_GAMES_STORE: return "the Epic Games launcher";
                case PatchManager.Platform.GOG: return "GOG Galaxy";
                default: return "the store you installed the game from";
            }
        }

        private static string VerifyInstructions()
        {
            switch (Singleton.Platform)
            {
                case PatchManager.Platform.STEAM:
                    return "click 'Ask Steam to check them' below (or in Steam: right-click Alien: Isolation > Properties > Installed Files > Verify integrity of game files).";
                case PatchManager.Platform.EPIC_GAMES_STORE:
                    return "in the Epic Games launcher, click the '...' next to Alien: Isolation and choose Verify.";
                case PatchManager.Platform.GOG:
                    return "in GOG Galaxy, select Alien: Isolation, click the settings icon, then Manage installation > Verify / Repair.";
                default:
                    return "verify (or repair) the game's files through the store you installed it from.";
            }
        }
    }
}
#endif

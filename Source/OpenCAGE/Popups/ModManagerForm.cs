#if ENABLE_MOD_PACKAGES
using OpenCAGE.Modding;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace OpenCAGE.Popups
{
    /* The mod library: import packages, tick the ones that should be active, order them, apply.
     *
     * Nothing touches the game files until Apply: it restores every affected file to its pristine
     * baseline and re-applies the ticked mods in list order, so enabling, disabling, reordering and
     * uninstalling are all the same safe operation. */
    public class ModManagerForm : Form
    {
        private Label _bannerLabel;
        private Button _pristineButton;
        private Button _rescanButton;
        private ListView _modList;
        private TextBox _details;
        private Button _importButton;
        private Button _exportButton;
        private Button _removeButton;
        private Button _upButton;
        private Button _downButton;
        private Button _applyButton;
        private Button _repairButton;
        private Label _statusLabel;

        private ScanResult _scan;
        private bool _scanning;
        private bool _refreshingList;

        public ModManagerForm()
        {
            Text = "Mod Manager";
            Icon = SharedFormIcon.Icon;
            Size = new Size(940, 620);
            MinimumSize = new Size(720, 480);
            StartPosition = FormStartPosition.CenterParent;
            AllowDrop = true;

            BuildLayout();
            Theming.ThemeManager.ApplyToForm(this);

            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            Shown += OnFirstShown;
        }

        private void BuildLayout()
        {
            //Banner: install state + the way to fix it
            Panel banner = new Panel() { Dock = DockStyle.Top, Height = 40, Padding = new Padding(8, 6, 8, 6) };
            _bannerLabel = new Label() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Text = "Checking install..." };
            _pristineButton = new Button() { Dock = DockStyle.Right, Width = 160, Text = "Capture pristine data..." };
            _rescanButton = new Button() { Dock = DockStyle.Right, Width = 80, Text = "Rescan" };
            _pristineButton.Click += (s, e) => RunPristineWizard();
            _rescanButton.Click += (s, e) => StartScan(true);
            banner.Controls.Add(_bannerLabel);
            banner.Controls.Add(_pristineButton);
            banner.Controls.Add(_rescanButton);

            //Mod list on the left, details on the right
            _modList = new ListView()
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                CheckBoxes = true,
                FullRowSelect = true,
                HideSelection = false,
                MultiSelect = false,
            };
            _modList.Columns.Add("Mod", 240);
            _modList.Columns.Add("Version", 70);
            _modList.Columns.Add("Author", 120);
            _modList.Columns.Add("Status", 160);
            _modList.SelectedIndexChanged += (s, e) => ShowDetails();
            _modList.ItemChecked += (s, e) => { if (!_refreshingList) UpdateApplyState(); };

            _details = new TextBox()
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
            };

            SplitContainer split = new SplitContainer()
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                
            };
            split.Panel1.Controls.Add(_modList);
            split.Panel2.Controls.Add(_details);

            //Buttons
            FlowLayoutPanel buttons = new FlowLayoutPanel()
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(6, 4, 6, 4),
            };
            _importButton = MakeButton(buttons, "Import Package...", (s, e) => ImportPackage());
            _exportButton = MakeButton(buttons, "Export Mod...", (s, e) => OpenExporter());
            _upButton = MakeButton(buttons, "Move Up", (s, e) => MoveSelected(-1));
            _downButton = MakeButton(buttons, "Move Down", (s, e) => MoveSelected(1));
            _removeButton = MakeButton(buttons, "Remove", (s, e) => RemoveSelected());
            _repairButton = MakeButton(buttons, "Repair", (s, e) => Repair());
            _applyButton = MakeButton(buttons, "Apply Changes", (s, e) => ApplyChanges());
            _applyButton.Width = 120;

            _statusLabel = new Label() { Dock = DockStyle.Bottom, Height = 22, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };

            Controls.Add(split);
            Controls.Add(_statusLabel);
            Controls.Add(buttons);
            Controls.Add(banner);
        }

        private Button MakeButton(FlowLayoutPanel parent, string text, EventHandler onClick)
        {
            Button button = new Button() { Text = text, AutoSize = true, Height = 28 };
            button.Click += onClick;
            parent.Controls.Add(button);
            return button;
        }

        private void OnFirstShown(object sender, EventArgs e)
        {
            Shown -= OnFirstShown;

            if (!ModServices.ManifestAvailable)
            {
                _bannerLabel.Text = "No vanilla file manifest is available for this build of OpenCAGE - mods still install, but nothing can be told apart from vanilla.";
                _pristineButton.Enabled = false;
            }

            ModInstaller installer = ModServices.Installer;
            if (installer != null && installer.HasCrashJournal())
            {
                if (MessageBox.Show(
                    "A previous mod operation was interrupted before it finished. Restore the game files it was changing to their prior state?",
                    "Interrupted operation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    try { installer.RecoverCrashJournal(); }
                    catch (Exception ex) { MessageBox.Show("Recovery failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                }
            }

            RefreshModList();
            StartScan(false);
        }

        #region SCANNING
        private void StartScan(bool force)
        {
            if (_scanning || ModServices.Installer == null || !ModServices.ManifestAvailable)
                return;
            _scanning = true;
            _rescanButton.Enabled = false;
            _bannerLabel.Text = "Scanning install against the vanilla manifest...";

            Thread thread = new Thread(() =>
            {
                ScanResult result = null;
                string error = null;
                try
                {
                    InstallScanner scanner = ModServices.NewScanner();
                    result = scanner.Scan(null, (done, total) =>
                    {
                        int captured = done;
                        SafeInvoke(() => _bannerLabel.Text = "Scanning install... " + captured + "/" + total);
                    });
                }
                catch (Exception e) { error = e.Message; }

                SafeInvoke(() =>
                {
                    _scanning = false;
                    _rescanButton.Enabled = true;
                    _scan = result;
                    if (error != null)
                        _bannerLabel.Text = "Scan failed: " + error;
                    else
                        ShowScanSummary();
                    RefreshModList();
                });
            });
            thread.IsBackground = true;
            thread.Start();
        }

        /* The scan outlives the form if the user closes it - updates must never throw off-thread */
        private void SafeInvoke(Action action)
        {
            try
            {
                if (!IsDisposed && IsHandleCreated)
                    BeginInvoke(action);
            }
            catch { }
        }

        private void ShowScanSummary()
        {
            if (_scan == null)
                return;
            int modified = _scan.CountOf(FileStatus.Modified);
            int foreign = _scan.CountOf(FileStatus.Foreign);
            int missing = _scan.CountOf(FileStatus.Missing);
            if (modified == 0 && missing == 0)
            {
                _bannerLabel.Text = "Install is clean: every shipped file is vanilla or managed by a mod."
                    + (foreign > 0 ? " (" + foreign + " extra file" + (foreign == 1 ? "" : "s") + ")" : "");
            }
            else
            {
                List<string> levels = _scan.LevelsWith(FileStatus.Modified);
                _bannerLabel.Text = modified + " file" + (modified == 1 ? "" : "s") + " differ" + (modified == 1 ? "s" : "") + " from vanilla and aren't managed by any mod"
                    + (levels.Count > 0 ? " (levels: " + string.Join(", ", levels.Take(4).ToArray()) + (levels.Count > 4 ? ", ..." : "") + ")" : "")
                    + (missing > 0 ? "; " + missing + " shipped files are missing" : "")
                    + ". Use 'Capture pristine data...' to keep your work and store pristine copies.";
            }
        }
        #endregion

        #region MOD LIST
        private void RefreshModList()
        {
            _refreshingList = true;
            ModState state = ModServices.State;
            List<string> stale = new List<string>();
            try { if (ModServices.Installer != null) stale = ModServices.Installer.PathsNeedingRepair(); }
            catch { }

            _modList.Items.Clear();
            if (state != null)
            {
                foreach (ModState.InstalledMod mod in state.ModsInPriorityOrder())
                {
                    string status;
                    if (!mod.Enabled)
                        status = mod.Applied.Count != 0 ? "disabling pending" : "";
                    else if (mod.Applied.Count == 0)
                        status = "enabled, not applied";
                    else if (mod.Applied.Keys.Any(o => stale.Contains(o)))
                        status = "needs repair";
                    else
                        status = "active";

                    ListViewItem item = new ListViewItem(mod.Name ?? mod.Id) { Tag = mod, Checked = mod.Enabled };
                    item.SubItems.Add(mod.Version ?? "");
                    item.SubItems.Add(mod.Author ?? "");
                    item.SubItems.Add(status);
                    _modList.Items.Add(item);
                }
            }
            _refreshingList = false;
            _repairButton.Enabled = stale.Count != 0;
            UpdateApplyState();
            ShowDetails();
        }

        private ModState.InstalledMod SelectedMod
        {
            get { return _modList.SelectedItems.Count == 0 ? null : (ModState.InstalledMod)_modList.SelectedItems[0].Tag; }
        }

        private List<string> DesiredEnabledIds()
        {
            List<string> ids = new List<string>();
            foreach (ListViewItem item in _modList.Items)
                if (item.Checked)
                    ids.Add(((ModState.InstalledMod)item.Tag).Id);
            return ids;
        }

        private void UpdateApplyState()
        {
            ModState state = ModServices.State;
            if (state == null)
            {
                _applyButton.Enabled = false;
                return;
            }
            List<string> desired = DesiredEnabledIds();
            List<string> current = state.ModsInPriorityOrder().Where(o => o.Enabled).Select(o => o.Id).ToList();
            bool pending = !desired.SequenceEqual(current);
            _applyButton.Enabled = pending;
            _statusLabel.Text = pending ? "Changes not yet applied - click Apply Changes." : "";
        }

        private void ShowDetails()
        {
            ModState.InstalledMod mod = SelectedMod;
            if (mod == null)
            {
                _details.Text = _modList.Items.Count == 0
                    ? "No mods in the library yet.\r\n\r\nImport a package (drag one in, or click Import Package...), or export your own changes with Export Mod..."
                    : "";
                return;
            }

            List<string> lines = new List<string>();
            lines.Add(mod.Name + " " + (mod.Version ?? ""));
            if (!string.IsNullOrEmpty(mod.Author)) lines.Add("by " + mod.Author);
            lines.Add("");
            if (!string.IsNullOrEmpty(mod.Description)) { lines.Add(mod.Description); lines.Add(""); }

            try
            {
                ModPackage package = ModServices.Installer.OpenPackage(mod);
                List<string> levels = package.Info.Levels;
                if (levels.Count != 0)
                    lines.Add("Levels: " + string.Join(", ", levels.ToArray()));
                int configs = package.Info.Entries.Count(o => o.Kind == ModPackageEntry.KindBml);
                int files = package.Info.Entries.Count - configs;
                lines.Add(files + " file change" + (files == 1 ? "" : "s") + ", " + configs + " config change" + (configs == 1 ? "" : "s"));

                //Conflicts with the other ticked mods
                List<ModState.InstalledMod> ticked = new List<ModState.InstalledMod>();
                foreach (ListViewItem item in _modList.Items)
                    if (item.Checked || item.Tag == mod)
                        ticked.Add((ModState.InstalledMod)item.Tag);
                List<ModConflict> conflicts = ModServices.Installer.FindConflicts(ticked)
                    .Where(o => o.ModA == mod.Name || o.ModB == mod.Name).ToList();
                if (conflicts.Count != 0)
                {
                    lines.Add("");
                    lines.Add("CONFLICTS:");
                    foreach (ModConflict conflict in conflicts.Take(12))
                        lines.Add("  " + (conflict.ModA == mod.Name ? conflict.ModB : conflict.ModA) + ": " + conflict.Target + " (" + conflict.Detail + ")");
                }
            }
            catch (Exception e)
            {
                lines.Add("Package unreadable: " + e.Message);
            }

            _details.Text = string.Join("\r\n", lines.ToArray());
        }

        private void MoveSelected(int direction)
        {
            if (_modList.SelectedIndices.Count == 0)
                return;
            int index = _modList.SelectedIndices[0];
            int target = index + direction;
            if (target < 0 || target >= _modList.Items.Count)
                return;

            _refreshingList = true;
            ListViewItem item = _modList.Items[index];
            _modList.Items.RemoveAt(index);
            _modList.Items.Insert(target, item);
            item.Selected = true;
            _refreshingList = false;

            //Persist the new order as priorities (harmless without an apply; order matters at apply)
            ModState state = ModServices.State;
            for (int i = 0; i < _modList.Items.Count; i++)
                ((ModState.InstalledMod)_modList.Items[i].Tag).Priority = i;
            state.Save();
            UpdateApplyState();
        }
        #endregion

        #region OPERATIONS
        private void ImportPackage()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "OpenCAGE mod packages (*" + ModToolkit.PackageExtension + ")|*" + ModToolkit.PackageExtension + "|All files (*.*)|*.*";
                dialog.Title = "Import mod package";
                if (dialog.ShowDialog() != DialogResult.OK)
                    return;
                ImportPackageFile(dialog.FileName);
            }
        }

        public void ImportPackageFile(string path)
        {
            try
            {
                ModState.InstalledMod mod = ModServices.Installer.ImportPackage(path);
                RefreshModList();
                foreach (ListViewItem item in _modList.Items)
                    if (item.Tag == mod)
                        item.Selected = true;
                _statusLabel.Text = "'" + mod.Name + "' added to the library. Tick it and click Apply Changes to install.";
            }
            catch (Exception e)
            {
                MessageBox.Show("Could not import the package:\n" + e.Message, "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RemoveSelected()
        {
            ModState.InstalledMod mod = SelectedMod;
            if (mod == null)
                return;
            if (mod.Applied.Count != 0 || mod.Enabled)
            {
                MessageBox.Show("'" + mod.Name + "' is enabled. Untick it and Apply Changes first, then remove it.", "Still enabled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show("Remove '" + mod.Name + "' from the library? The package file is deleted.", "Remove mod", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            try
            {
                ModServices.Installer.RemoveFromLibrary(mod.Id);
                RefreshModList();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Remove failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyChanges()
        {
            List<string> desired = DesiredEnabledIds();

            //Surface conflicts before anything happens
            List<ModState.InstalledMod> desiredMods = desired.Select(o => ModServices.State.FindMod(o)).Where(o => o != null).ToList();
            List<ModConflict> conflicts = ModServices.Installer.FindConflicts(desiredMods);
            if (conflicts.Count != 0)
            {
                string message = "These mods change the same things - the one lower in the list wins:\n\n"
                    + string.Join("\n", conflicts.Take(10).Select(o => "  " + o.ModA + "  +  " + o.ModB + "\n      " + o.Target + " - " + o.Detail).ToArray())
                    + (conflicts.Count > 10 ? "\n  ...and " + (conflicts.Count - 10) + " more" : "")
                    + "\n\nApply anyway?";
                if (MessageBox.Show(message, "Mod conflicts", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }

            TransactionResult result = RunTransaction(desired, false);
            if (result == null)
                return;

            if (!result.Success && result.Error != null && result.Error.Contains("aren't vanilla"))
            {
                if (MessageBox.Show(result.Error + "\n\nAdopt the current bytes of those files as their restore point instead? "
                    + "(Disabling mods will put back what's there right now, not shipped vanilla data.)",
                    "Files aren't vanilla", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    result = RunTransaction(desired, true);
                    if (result == null)
                        return;
                }
                else
                {
                    RefreshModList();
                    return;
                }
            }

            if (!result.Success)
                MessageBox.Show(result.Error, "Apply failed - no changes were made", MessageBoxButtons.OK, MessageBoxIcon.Error);
            else if (result.Warnings.Count != 0)
                MessageBox.Show("Applied, with notes:\n\n" + string.Join("\n", result.Warnings.Take(15).ToArray()), "Applied", MessageBoxButtons.OK, MessageBoxIcon.Information);

            RefreshModList();
            if (result.Success)
                _statusLabel.Text = "Applied.";
        }

        private TransactionResult RunTransaction(List<string> desired, bool adopt)
        {
            TransactionResult result = null;
            Exception error = null;
            using (BusyDialog busy = new BusyDialog("Applying mod changes..."))
            {
                busy.Work = () =>
                {
                    try { result = ModServices.Installer.ApplyConfiguration(desired, adopt); }
                    catch (Exception e) { error = e; }
                };
                busy.ShowDialog(this);
            }
            if (error != null)
            {
                MessageBox.Show(error.Message, "Apply failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
            return result;
        }

        private void Repair()
        {
            //Reapply the current enabled set: restores baselines and reapplies everything
            List<string> enabled = ModServices.State.ModsInPriorityOrder().Where(o => o.Enabled).Select(o => o.Id).ToList();
            TransactionResult result = RunTransaction(enabled, false);
            if (result != null && !result.Success)
                MessageBox.Show(result.Error, "Repair failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            RefreshModList();
            StartScan(true);
        }

        private void OpenExporter()
        {
            using (ModExporterForm exporter = new ModExporterForm(_scan))
                exporter.ShowDialog(this);
            StartScan(true);
        }

        private void RunPristineWizard()
        {
            if (_scan == null)
            {
                MessageBox.Show("Wait for the install scan to finish first.", "Scan in progress", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (PristineCaptureWizard wizard = new PristineCaptureWizard(_scan))
                wizard.ShowDialog(this);
            StartScan(true);
        }
        #endregion

        #region DRAG DROP
        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)
                && ((string[])e.Data.GetData(DataFormats.FileDrop)).Any(o => o.ToLowerInvariant().EndsWith(ModToolkit.PackageExtension)))
                e.Effect = DragDropEffects.Copy;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            foreach (string file in (string[])e.Data.GetData(DataFormats.FileDrop))
                if (file.ToLowerInvariant().EndsWith(ModToolkit.PackageExtension))
                    ImportPackageFile(file);
        }
        #endregion
    }

    /* Tiny modal "working..." shell: runs Work on a thread, closes itself when done */
    public class BusyDialog : Form
    {
        public Action Work;

        public BusyDialog(string message)
        {
            Text = "OpenCAGE";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ControlBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(360, 110);

            Label label = new Label() { Text = message, Dock = DockStyle.Top, Height = 30, TextAlign = ContentAlignment.MiddleCenter, Padding = new Padding(8) };
            ProgressBar bar = new ProgressBar() { Style = ProgressBarStyle.Marquee, Dock = DockStyle.Top, Height = 20, MarqueeAnimationSpeed = 30 };
            Padding = new Padding(10);
            Controls.Add(bar);
            Controls.Add(label);

            Theming.ThemeManager.ApplyToForm(this);

            Shown += (s, e) =>
            {
                Thread thread = new Thread(() =>
                {
                    try { Work?.Invoke(); }
                    finally { BeginInvoke((Action)Close); }
                });
                thread.IsBackground = true;
                thread.Start();
            };
        }
    }
}
#endif

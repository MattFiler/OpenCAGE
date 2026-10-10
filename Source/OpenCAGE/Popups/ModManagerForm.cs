#if ENABLE_MOD_PACKAGES
using OpenCAGE.Modding;
using OpenCAGE.Modding.Merging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace OpenCAGE.Popups
{
    /* The mod library: add mods, tick the ones to play with, put them in order, apply.
     *
     * Nothing touches the game files until Apply. Then every file the mods change is rebuilt from the original
     * game files and the ticked mods, in list order: mods changing the same file are combined, and where two
     * change the very same thing the one lower in the list wins. So turning a mod off, reordering, and removing
     * one are all the same safe operation, and turning everything off gives back the original game. */
    public class ModManagerForm : Form
    {
        private Label _headline;
        private Label _installState;
        private LinkLabel _installAction;
        private ListView _modList;
        private PictureBox _preview;
        private Label _detailTitle;
        private Label _detailByline;
        private TextBox _details;
        private Button _addButton;
        private Button _removeButton;
        private Button _upButton;
        private Button _downButton;
        private Button _createButton;
        private Button _repairButton;
        private Button _applyButton;
        private Label _statusLabel;
        private SplitContainer _split;

        private ScanResult _scan;
        private bool _scanning;
        private bool _refreshingList;
        private List<string> _stale = new List<string>();

        public ModManagerForm()
        {
            Text = "Mod Manager";
            Icon = SharedFormIcon.Icon;
            Size = new Size(1040, 660);
            MinimumSize = new Size(820, 500);
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
            //Top: what this is, and the state of the game files
            Panel header = new Panel() { Dock = DockStyle.Top, Height = 64, Padding = new Padding(12, 8, 12, 6) };
            _headline = new Label()
            {
                Dock = DockStyle.Top,
                Height = 24,
                Text = "Tick the mods you want to play with, put them in order, then click Apply.",
                Font = new Font(Font.FontFamily, 10f, FontStyle.Bold),
            };
            FlowLayoutPanel stateRow = new FlowLayoutPanel() { Dock = DockStyle.Top, Height = 24, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            _installState = new Label() { AutoSize = true, Text = "Checking your game files...", Margin = new Padding(0, 4, 6, 0) };
            _installAction = new LinkLabel() { AutoSize = true, Visible = false, Margin = new Padding(0, 4, 0, 0) };
            _installAction.LinkClicked += (s, e) => RunPristineWizard();
            stateRow.Controls.Add(_installState);
            stateRow.Controls.Add(_installAction);
            header.Controls.Add(stateRow);
            header.Controls.Add(_headline);

            //The list on the left
            _modList = new ListView()
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                CheckBoxes = true,
                FullRowSelect = true,
                HideSelection = false,
                MultiSelect = false,
                AllowDrop = true,
            };
            _modList.Columns.Add("Mod", 230);
            _modList.Columns.Add("Version", 64);
            _modList.Columns.Add("Author", 110);
            _modList.Columns.Add("Status", 140);
            _modList.SelectedIndexChanged += (s, e) => ShowDetails();
            _modList.ItemChecked += (s, e) => { if (!_refreshingList) { UpdatePending(); ShowDetails(); } };
            _modList.ItemDrag += (s, e) => _modList.DoDragDrop(e.Item, DragDropEffects.Move);
            _modList.DragEnter += OnListDragEnter;
            _modList.DragOver += OnListDragOver;
            _modList.DragDrop += OnListDragDrop;

            Label orderTip = new Label()
            {
                Dock = DockStyle.Bottom,
                Height = 34,
                Text = "Mods lower in the list win when two of them change the very same thing. Drag a mod, or use Move Up / Move Down, to change the order.",
                Padding = new Padding(2, 4, 2, 0),
            };
            Panel listPanel = new Panel() { Dock = DockStyle.Fill };
            listPanel.Controls.Add(_modList);
            listPanel.Controls.Add(orderTip);

            //The selected mod on the right
            _preview = new PictureBox() { Dock = DockStyle.Top, Height = 170, SizeMode = PictureBoxSizeMode.Zoom, Visible = false };
            _detailTitle = new Label() { Dock = DockStyle.Top, Height = 28, Font = new Font(Font.FontFamily, 12f, FontStyle.Bold), AutoEllipsis = true };
            _detailByline = new Label() { Dock = DockStyle.Top, Height = 20, AutoEllipsis = true };
            _details = new TextBox()
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
            };
            Panel detailPanel = new Panel() { Dock = DockStyle.Fill, Padding = new Padding(10, 4, 6, 4) };
            detailPanel.Controls.Add(_details);
            detailPanel.Controls.Add(_detailByline);
            detailPanel.Controls.Add(_detailTitle);
            detailPanel.Controls.Add(_preview);

            //The details keep their width as the window grows: the list takes the rest
            _split = new SplitContainer() { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, FixedPanel = FixedPanel.Panel2 };
            _split.Panel1.Controls.Add(listPanel);
            _split.Panel2.Controls.Add(detailPanel);

            //Buttons
            FlowLayoutPanel left = new FlowLayoutPanel() { Dock = DockStyle.Left, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(6, 6, 0, 6) };
            _addButton = MakeButton(left, "Add Mod...", (s, e) => AddMods());
            _removeButton = MakeButton(left, "Remove", (s, e) => RemoveSelected());
            _upButton = MakeButton(left, "Move Up", (s, e) => MoveSelected(-1));
            _downButton = MakeButton(left, "Move Down", (s, e) => MoveSelected(1));
            FlowLayoutPanel right = new FlowLayoutPanel() { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 6, 6, 6) };
            _applyButton = MakeButton(right, "Apply", (s, e) => ApplyChanges());
            _applyButton.Font = new Font(Font, FontStyle.Bold);
            _applyButton.MinimumSize = new Size(110, 30);
            _repairButton = MakeButton(right, "Repair", (s, e) => Repair());
            _createButton = MakeButton(right, "Create a Mod...", (s, e) => OpenExporter());
            Panel buttons = new Panel() { Dock = DockStyle.Bottom, Height = 46 };
            buttons.Controls.Add(left);
            buttons.Controls.Add(right);

            _statusLabel = new Label() { Dock = DockStyle.Bottom, Height = 22, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0) };

            Controls.Add(_split);
            Controls.Add(_statusLabel);
            Controls.Add(buttons);
            Controls.Add(header);
        }

        private Button MakeButton(FlowLayoutPanel parent, string text, EventHandler onClick)
        {
            Button button = new Button() { Text = text, AutoSize = true, MinimumSize = new Size(0, 30), Margin = new Padding(3) };
            button.Click += onClick;
            parent.Controls.Add(button);
            return button;
        }

        private void OnFirstShown(object sender, EventArgs e)
        {
            Shown -= OnFirstShown;
            try { _split.SplitterDistance = Math.Max(_split.Panel1MinSize, _split.Width - 420); } catch { }

            ModInstaller installer = ModServices.Installer;
            if (installer == null)
            {
                _installState.Text = "Set your game folder first (File > Game Directory) - the Mod Manager works on that copy of the game.";
                foreach (Control control in new Control[] { _addButton, _applyButton, _repairButton, _createButton })
                    control.Enabled = false;
                return;
            }
            //Half-applied mods aren't a state anyone wants to play: put back what was there before, every time
            if (installer.HasCrashJournal())
            {
                try
                {
                    installer.RecoverCrashJournal();
                    MessageBox.Show("OpenCAGE was closed part-way through applying mods last time. Your game files have been put back the way they were before that started."
                        + (AnimationPakWrite.ChangedOnDisk(Singleton.Animations?.PAK?.Filepath) != null ? "\n\nANIMATION.PAK was one of them: restart OpenCAGE before working on animations, animation trees or blend sets, which still show it as it was." : ""),
                        "Mod Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("OpenCAGE was closed part-way through applying mods last time, and putting your game files back didn't work: " + ex.Message
                        + "\n\nClose anything that might be using the game's files, then open the Mod Manager again to try again.", "Mod Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Close();
                    return;
                }
            }

            RefreshModList();
            StartScan();
        }

        #region GAME FILES
        private void StartScan()
        {
            if (_scanning || ModServices.Installer == null)
                return;
            if (!ModServices.ManifestAvailable)
            {
                _installState.Text = "This version of the game can't be checked against its original files, so mods are installed without combining.";
                return;
            }
            _scanning = true;
            _installState.Text = "Checking your game files...";
            _installAction.Visible = false;

            Thread thread = new Thread(() =>
            {
                ScanResult result = null;
                string error = null;
                try
                {
                    result = ModServices.NewScanner().Scan(null, (done, total) =>
                    {
                        int at = done;
                        SafeInvoke(() => _installState.Text = "Checking your game files... " + (at * 100 / Math.Max(1, total)) + "%");
                    });
                }
                catch (Exception e) { error = e.Message; }

                SafeInvoke(() =>
                {
                    _scanning = false;
                    _scan = result;
                    if (error != null)
                        _installState.Text = "Your game files couldn't be checked: " + error;
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
            int missing = _scan.CountOf(FileStatus.Missing);
            _installAction.Visible = false;
            if (modified == 0 && missing == 0)
            {
                _installState.Text = _stale.Count != 0
                    ? "Some installed mod files have been changed or replaced since they were installed (a game update or a file check does this)."
                    : "Your game files are in good shape.";
                return;
            }
            //Changes OpenCAGE has the originals of are safe: mods are combined with them. Ones it hasn't need keeping safe first.
            List<string> changed = _scan.WithStatus(FileStatus.Modified).Concat(_scan.WithStatus(FileStatus.Missing)).Where(o => !ModToolkit.IsRegenerated(o)).ToList();
            List<string> unsafeChanges = changed.Where(o => !ModServices.Installer.HasOriginal(o)).ToList();
            if (unsafeChanges.Count == 0)
            {
                //Counted the way a person thinks of them: a level is one thing, however many files it has
                List<string> ownLevels = changed.Select(ModInstaller.LevelUnitOf).Where(o => o != null).Distinct().OrderBy(o => o).ToList();
                int ownFiles = changed.Count(o => ModInstaller.LevelUnitOf(o) == null);
                List<string> what = new List<string>();
                if (ownLevels.Count != 0) what.Add(ownLevels.Count <= 3 ? string.Join(", ", ownLevels.Select(o => o.Replace("DLC/", ""))) : ownLevels.Count + " levels");
                if (ownFiles != 0) what.Add((ownLevels.Count != 0 ? ownFiles + " other" : ownFiles.ToString()) + " game file" + (ownFiles == 1 ? "" : "s"));
                _installState.Text = changed.Count == 0 ? "Your game files are in good shape."
                    : "You've changed " + string.Join(" and ", what) + " yourself. OpenCAGE keeps the originals, so mods are combined with your changes.";
                return;
            }
            List<string> levels = unsafeChanges.Select(ModToolkit.LevelOf).Where(o => o != null).Distinct().OrderBy(o => o).ToList();
            _installState.Text = unsafeChanges.Count + " game file" + (unsafeChanges.Count == 1 ? " has" : "s have") + " been changed without OpenCAGE keeping the original"
                + (levels.Count > 0 ? " (in " + string.Join(", ", levels.Take(3)) + (levels.Count > 3 ? " and more" : "") + ")" : "") + ". Mods can't be combined with those changes until it has one.";
            _installAction.Text = "Keep those changes safe...";
            _installAction.Visible = true;
        }
        #endregion

        #region MOD LIST
        private void RefreshModList()
        {
            _refreshingList = true;
            ModState state = ModServices.State;
            try { _stale = ModServices.Installer != null ? ModServices.Installer.PathsNeedingRepair() : new List<string>(); }
            catch { _stale = new List<string>(); }

            string selectedId = SelectedMod?.Id;
            _modList.BeginUpdate();
            _modList.Items.Clear();
            if (state != null)
            {
                foreach (ModState.InstalledMod mod in state.ModsInPriorityOrder())
                {
                    ListViewItem item = new ListViewItem(mod.Name ?? mod.Id) { Tag = mod, Checked = mod.Enabled };
                    item.SubItems.Add(mod.Version ?? "");
                    item.SubItems.Add(mod.Author ?? "");
                    item.SubItems.Add("");
                    _modList.Items.Add(item);
                    if (mod.Id == selectedId) item.Selected = true;
                }
            }
            _modList.EndUpdate();
            _refreshingList = false;
            UpdatePending();
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

        /* What each mod's row says, and whether there's anything for Apply to do */
        private void UpdatePending()
        {
            ModState state = ModServices.State;
            if (state == null)
            {
                _applyButton.Enabled = false;
                return;
            }
            foreach (ListViewItem item in _modList.Items)
            {
                ModState.InstalledMod mod = (ModState.InstalledMod)item.Tag;
                string status;
                if (item.Checked && !mod.Enabled) status = "Will be installed";
                else if (!item.Checked && mod.Enabled) status = "Will be removed";
                else if (!item.Checked) status = "";
                else if (mod.Applied.Keys.Any(o => _stale.Contains(o))) status = "Needs repair";
                else status = "Installed";
                item.SubItems[3].Text = status;
            }
            List<string> desired = DesiredEnabledIds();
            List<string> current = state.ModsInPriorityOrder().Where(o => o.Enabled).Select(o => o.Id).ToList();
            bool pending = !desired.SequenceEqual(current);
            _applyButton.Enabled = pending;
            _repairButton.Enabled = !pending && _stale.Count != 0;
            _statusLabel.Text = pending ? "You have changes that aren't applied yet - click Apply." : (_stale.Count != 0 ? "Some mod files need repairing - click Repair." : "");
        }

        private void ShowDetails()
        {
            ModState.InstalledMod mod = SelectedMod;
            _preview.Image?.Dispose();
            _preview.Image = null;
            _preview.Visible = false;
            if (mod == null)
            {
                _detailTitle.Text = _modList.Items.Count == 0 ? "No mods yet" : "";
                _detailByline.Text = "";
                _details.Text = _modList.Items.Count == 0
                    ? "Drag a mod file (.omp) onto this window, or click Add Mod..., to add it to your list. Then tick it and click Apply to install it.\r\n\r\n"
                      + "Want to make your own? Make your changes in OpenCAGE, then click Create a Mod..."
                    : "Select a mod to see what it changes.";
                return;
            }

            _detailTitle.Text = mod.Name ?? mod.Id;
            _detailByline.Text = (string.IsNullOrEmpty(mod.Author) ? "" : "by " + mod.Author) + (string.IsNullOrEmpty(mod.Version) ? "" : (string.IsNullOrEmpty(mod.Author) ? "" : "  ·  ") + "version " + mod.Version);
            List<string> lines = new List<string>();
            try
            {
                ModPackage package = ModServices.Installer.OpenPackage(mod);
                byte[] png = package.ReadPreview();
                if (png != null)
                {
                    try
                    {
                        using (MemoryStream stream = new MemoryStream(png))
                            _preview.Image = new Bitmap(Image.FromStream(stream));
                        _preview.Visible = true;
                    }
                    catch { }
                }
                if (!string.IsNullOrWhiteSpace(mod.Description)) { lines.Add(mod.Description.Trim()); lines.Add(""); }
                lines.Add("What it changes:");
                lines.Add("  " + (string.IsNullOrWhiteSpace(package.Info.Summary) ? DescribeEntries(package) : package.Info.Summary));

                //What it clashes with among the ticked mods (and itself, if it isn't ticked)
                List<string> ids = DesiredEnabledIds();
                if (!ids.Contains(mod.Id)) ids.Add(mod.Id);
                if (ids.Count > 1)
                {
                    ModAnalysis analysis = ModServices.Installer.Analyze(ids);
                    List<MergeConflict> conflicts = analysis.Conflicts.Where(o => o.Kept == mod.Name || o.Lost == mod.Name).ToList();
                    List<string> shared = analysis.CombinedLevels.Where(level => SharesLevel(level, mod, ids)).ToList();
                    if (shared.Count != 0)
                    {
                        lines.Add("");
                        lines.Add("Shares " + string.Join(", ", shared) + " with other ticked mods (or your own changes) - they're combined into one level when you apply.");
                    }
                    if (conflicts.Count != 0)
                    {
                        lines.Add("");
                        lines.Add("Changes the same things as other ticked mods (the one lower in the list wins):");
                        foreach (MergeConflict conflict in conflicts.Take(12))
                            lines.Add("  • " + conflict.Describe());
                        if (conflicts.Count > 12)
                            lines.Add("  ...and " + (conflicts.Count - 12) + " more");
                    }
                }
            }
            catch (Exception e)
            {
                lines.Add("This mod's file can't be read: " + e.Message);
            }
            _details.Text = string.Join("\r\n", lines);
        }

        private static string DescribeEntries(ModPackage package)
        {
            List<string> levels = package.Info.Levels;
            int other = package.Info.Entries.Count(o => ModToolkit.LevelOf(o.Target) == null);
            return (levels.Count != 0 ? "the level" + (levels.Count == 1 ? " " : "s ") + string.Join(", ", levels) : "")
                + (levels.Count != 0 && other != 0 ? ", and " : "")
                + (other != 0 ? other + " other game file" + (other == 1 ? "" : "s") : "") + ".";
        }

        private bool SharesLevel(string level, ModState.InstalledMod mod, List<string> ids)
        {
            try { return ModServices.Installer.OpenPackage(mod).Info.Entries.Any(o => ModInstaller.LevelUnitOf(ModToolkit.Normalise(o.Target)) == level); }
            catch { return false; }
        }

        private void MoveSelected(int direction)
        {
            if (_modList.SelectedIndices.Count == 0)
                return;
            int index = _modList.SelectedIndices[0];
            MoveItem(index, index + direction);
        }

        private void MoveItem(int from, int to)
        {
            if (from < 0 || to < 0 || from >= _modList.Items.Count || to >= _modList.Items.Count || from == to)
                return;
            _refreshingList = true;
            ListViewItem item = _modList.Items[from];
            _modList.Items.RemoveAt(from);
            _modList.Items.Insert(to, item);
            item.Selected = true;
            item.EnsureVisible();
            _refreshingList = false;

            //The order is kept now; it takes effect at the next Apply
            for (int i = 0; i < _modList.Items.Count; i++)
                ((ModState.InstalledMod)_modList.Items[i].Tag).Priority = i;
            ModServices.Installer.SaveState();
            UpdatePending();
            ShowDetails();
        }

        private void OnListDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(ListViewItem))) e.Effect = DragDropEffects.Move;
            else OnDragEnter(sender, e);
        }

        private void OnListDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(ListViewItem))) e.Effect = DragDropEffects.Move;
        }

        private void OnListDragDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(ListViewItem)))
            {
                OnDragDrop(sender, e);
                return;
            }
            ListViewItem dragged = (ListViewItem)e.Data.GetData(typeof(ListViewItem));
            Point point = _modList.PointToClient(new Point(e.X, e.Y));
            ListViewItem over = _modList.GetItemAt(point.X, point.Y);
            int to = over == null ? _modList.Items.Count - 1 : over.Index;
            MoveItem(dragged.Index, to);
        }
        #endregion

        #region ADDING AND REMOVING
        private void AddMods()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "OpenCAGE mods (*" + ModToolkit.PackageExtension + ")|*" + ModToolkit.PackageExtension + "|All files (*.*)|*.*";
                dialog.Title = "Add mods";
                dialog.Multiselect = true;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                foreach (string file in dialog.FileNames)
                    ImportPackageFile(file);
            }
        }

        /// <summary>Add a mod file to the list (unticked), or update the copy already there.</summary>
        public void ImportPackageFile(string path)
        {
            try
            {
                ModState.InstalledMod existing = null;
                try { existing = ModServices.State.FindMod(ModPackage.Read(path).Info.Id); } catch { }
                ModState.InstalledMod mod = ModServices.Installer.ImportPackage(path);
                RefreshModList();
                foreach (ListViewItem item in _modList.Items)
                    if (item.Tag == mod)
                    {
                        item.Selected = true;
                        item.EnsureVisible();
                    }
                _statusLabel.Text = existing != null
                    ? "'" + mod.Name + "' was updated to version " + (mod.Version ?? "?") + "." + (mod.Enabled ? " Click Apply to install the new version." : "")
                    : "'" + mod.Name + "' was added to your list. Tick it and click Apply to install it.";
                if (existing != null && mod.Enabled)
                {
                    //An installed mod's file changed under it: Apply puts the new version in
                    _applyButton.Enabled = true;
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("'" + Path.GetFileName(path) + "' couldn't be added:\n\n" + e.Message, "Add Mod", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RemoveSelected()
        {
            ModState.InstalledMod mod = SelectedMod;
            if (mod == null)
                return;
            if (mod.Enabled || mod.Applied.Count != 0)
            {
                MessageBox.Show("'" + mod.Name + "' is installed. Untick it and click Apply first, then you can remove it from the list.", "Remove", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show("Remove '" + mod.Name + "' from your list? You'd need the mod file again to add it back.", "Remove", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            try
            {
                ModServices.Installer.RemoveFromLibrary(mod.Id);
                RefreshModList();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Remove", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion

        #region APPLYING
        private void ApplyChanges()
        {
            List<string> desired = DesiredEnabledIds();

            if (System.Diagnostics.Process.GetProcessesByName("AI").Length != 0)
            {
                MessageBox.Show("Close Alien: Isolation before changing mods - its files are in use while it runs.", "Game is running", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            //Say what will happen before anything does
            ModAnalysis analysis = ModServices.Installer.Analyze(desired);
            if (analysis.Problems.Count != 0)
            {
                MessageBox.Show("Some mods can't be used:\n\n" + string.Join("\n\n", analysis.Problems.Take(8)), "Mod Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            EditedFilesChoice edited = EditedFilesChoice.Ask;
            if (analysis.EditedFiles.Count != 0)
            {
                EditedFilesChoice? choice = AskAboutEditedFiles(analysis.EditedFiles);
                if (choice == null)
                    return;
                edited = choice.Value;
            }
            List<MergeConflict> conflicts = analysis.Conflicts;
            //Only the levels that will be put together again: one already combined from these very mods is left as it is
            List<string> combinedLevels = analysis.CombinedLevels.Except(analysis.UnchangedLevels).ToList();
            if (conflicts.Count != 0 || combinedLevels.Count != 0 || analysis.OwnChanges.Count != 0)
            {
                List<string> lines = new List<string>();
                if (analysis.OwnChanges.Count != 0)
                {
                    lines.Add("You've changed some of these files yourself. Your changes are kept, and the mods are combined with them (where both change the very same thing, the mod is used while it's installed):");
                    foreach (string what in Friendly(analysis.OwnChanges).Take(12))
                        lines.Add("  • " + what);
                    lines.Add("");
                }
                if (combinedLevels.Count != 0)
                {
                    HashSet<string> withYours = new HashSet<string>(analysis.OwnChanges.Select(ModInstaller.LevelUnitOf).Where(o => o != null));
                    lines.Add((combinedLevels.All(withYours.Contains) ? "These levels are changed by you and by your mods" : combinedLevels.Any(withYours.Contains) ? "These levels are changed by more than one of your mods (or by you and a mod)" : "These levels are changed by more than one of your mods")
                        + ", so they'll be put together into one level with every change in it, which takes a minute or so each:");
                    foreach (string level in combinedLevels)
                        lines.Add("  • " + level.Replace("DLC/", ""));
                    lines.Add("");
                }
                if (conflicts.Count != 0)
                {
                    lines.Add("Some of your mods change the very same things. For each one, the mod lower in the list will be used:");
                    foreach (MergeConflict conflict in conflicts.Take(20))
                        lines.Add("  • " + conflict.Describe());
                    if (conflicts.Count > 20)
                        lines.Add("  ...and " + (conflicts.Count - 20) + " more");
                    lines.Add("");
                    lines.Add("Everything else every mod changes is kept. To use a different mod's version, move it lower in the list.");
                }
                if (ModReportDialog.Show(this, "Before applying", string.Join("\r\n", lines), "Apply", "Cancel") != DialogResult.OK)
                    return;
            }

            TransactionResult result = RunTransaction(desired, false, edited);
            if (result == null)
                return;

            if (!result.Success && result.Error != null && result.Error.Contains("no copy of the originals"))
            {
                if (MessageBox.Show("Some of the game files these mods change have already been changed outside the Mod Manager, and OpenCAGE has no original copy of them to go back to.\n\n"
                    + "Keep what's in them now as the version to go back to when mods are removed? (If you'd rather go back to the original game, choose No and use 'Keep those changes safe...' first.)",
                    "Changed game files", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    result = RunTransaction(desired, true, edited);
                    if (result == null)
                        return;
                }
                else
                {
                    RefreshModList();
                    return;
                }
            }

            RefreshModList();
            if (!result.Success)
            {
                MessageBox.Show(result.Error + "\n\nNothing was changed: your game files are as they were before you clicked Apply.", "Couldn't apply", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            int enabled = ModServices.State.Mods.Count(o => o.Enabled);
            _statusLabel.Text = enabled == 0 ? "All mods removed." : "Done - " + enabled + " mod" + (enabled == 1 ? " is" : "s are") + " installed.";
            if (result.KeptAsMod != null)
                _statusLabel.Text += " Your changes were kept as '" + result.KeptAsMod + "'.";
            if (result.Conflicts.Count != 0 || result.Warnings.Count != 0 || result.CombinedLevels.Count != 0 || result.KeptAsMod != null)
                ShowResults(result);
            StartScan();
        }

        private void ShowResults(TransactionResult result)
        {
            List<string> lines = new List<string>();
            lines.Add("Your mods are installed.");
            if (result.KeptAsMod != null)
            {
                lines.Add("");
                lines.Add("The changes you'd made since were kept as a new mod, '" + result.KeptAsMod + "', at the bottom of your list.");
            }
            if (result.CombinedLevels.Count != 0)
            {
                lines.Add("");
                lines.Add("Combined from several mods: " + string.Join(", ", result.CombinedLevels) + ".");
            }
            if (result.Conflicts.Count != 0)
            {
                lines.Add("");
                lines.Add("Where mods changed the very same thing, this is what was used:");
                foreach (MergeConflict conflict in result.Conflicts.Take(40))
                    lines.Add("  • " + conflict.Describe());
                if (result.Conflicts.Count > 40)
                    lines.Add("  ...and " + (result.Conflicts.Count - 40) + " more");
            }
            if (result.Warnings.Count != 0)
            {
                lines.Add("");
                lines.Add("Also worth knowing:");
                foreach (string warning in result.Warnings.Take(20))
                    lines.Add("  • " + warning);
            }
            ModReportDialog.Show(this, "Mods applied", string.Join("\r\n", lines), "OK", null);
        }

        private TransactionResult RunTransaction(List<string> desired, bool adopt, EditedFilesChoice edited)
        {
            TransactionResult result = null;
            Exception error = null;
            using (BusyDialog busy = new BusyDialog("Applying your mods..."))
            {
                busy.Work = report =>
                {
                    try { result = ModServices.Installer.ApplyConfiguration(desired, adopt, report, edited); }
                    catch (Exception e) { error = e; }
                };
                busy.ShowDialog(this);
            }
            if (error != null)
            {
                MessageBox.Show(error.Message, "Couldn't apply", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
            //OpenCAGE holds the animations in memory as they were loaded, and won't write them back over the changed file
            if (result != null && result.Success && AnimationPakWrite.ChangedOnDisk(Singleton.Animations?.PAK?.Filepath) != null)
                result.Warnings.Add("ANIMATION.PAK has changed: restart OpenCAGE before working on animations, animation trees or blend sets, which still show it as it was (and won't save over the new one).");
            return result;
        }

        private void Repair()
        {
            //The same mods again: everything rebuilt from the original files and the mods
            List<string> enabled = ModServices.State.ModsInPriorityOrder().Where(o => o.Enabled).Select(o => o.Id).ToList();
            EditedFilesChoice edited = EditedFilesChoice.Ask;
            List<string> editedFiles = ModServices.Installer.EditedSinceApplied();
            if (editedFiles.Count != 0)
            {
                EditedFilesChoice? choice = AskAboutEditedFiles(editedFiles);
                if (choice == null)
                    return;
                edited = choice.Value;
            }
            TransactionResult result = RunTransaction(enabled, false, edited);
            RefreshModList();
            if (result != null && !result.Success)
                MessageBox.Show(result.Error, "Couldn't repair", MessageBoxButtons.OK, MessageBoxIcon.Error);
            else if (result != null)
                _statusLabel.Text = "Repaired - your mods are installed again.";
            StartScan();
        }

        /* Files installed mods had changed that were changed again since: never replaced without asking */
        private EditedFilesChoice? AskAboutEditedFiles(List<string> edited)
        {
            List<string> lines = new List<string>();
            lines.Add("You've changed some files since your mods were installed:");
            foreach (string what in Friendly(edited).Take(12))
                lines.Add("  • " + what);
            lines.Add("");
            lines.Add("Applying rebuilds these from your mods, which would replace what you changed.");
            lines.Add("");
            lines.Add("Keep my changes: they're saved as a new mod called 'My changes' at the bottom of your list, so they stay installed and win wherever they clash with other mods. "
                + "It holds those files exactly as they are now - including what your other mods had changed in them.");
            lines.Add("");
            lines.Add("Throw them away: the files are rebuilt from your mods as if you'd never changed them.");
            DialogResult answer = ModReportDialog.Show(this, "Keep your changes?", string.Join("\r\n", lines), "Keep my changes", "Cancel", "Throw them away");
            if (answer == DialogResult.OK)
                return EditedFilesChoice.KeepAsMod;
            if (answer == DialogResult.No)
            {
                if (MessageBox.Show("Throw away the changes you made to those files? This can't be undone.", "Throw them away", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                    return EditedFilesChoice.Discard;
            }
            return null;
        }

        /* Game files in words: a level's files as the level, anything else by its name */
        private static List<string> Friendly(IEnumerable<string> paths)
        {
            List<string> result = new List<string>();
            foreach (string path in paths)
            {
                string level = ModInstaller.LevelUnitOf(path);
                string what = level != null ? "the level " + level.Replace("DLC/", "") : MergeConflict.Place(path);
                if (!result.Contains(what))
                    result.Add(what);
            }
            return result;
        }

        private void OpenExporter()
        {
            using (ModExporterForm exporter = new ModExporterForm(_scan))
                exporter.ShowDialog(this);
            RefreshModList();
            StartScan();
        }

        private void RunPristineWizard()
        {
            if (_scan == null)
            {
                MessageBox.Show("Wait for the check of your game files to finish first.", "Mod Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (PristineCaptureWizard wizard = new PristineCaptureWizard(_scan))
                wizard.ShowDialog(this);
            StartScan();
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
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;
            foreach (string file in (string[])e.Data.GetData(DataFormats.FileDrop))
                if (file.ToLowerInvariant().EndsWith(ModToolkit.PackageExtension))
                    ImportPackageFile(file);
        }
        #endregion
    }

    /* A modal "working..." window: runs Work on a thread, shows each step it reports, closes itself when done */
    public class BusyDialog : Form
    {
        /// <summary>The work. It's handed a way to report what it's doing now.</summary>
        public Action<Action<string>> Work;

        private readonly Label _label;

        public BusyDialog(string message)
        {
            Text = "OpenCAGE";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ControlBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(460, 130);

            _label = new Label() { Text = message, Dock = DockStyle.Top, Height = 44, TextAlign = ContentAlignment.MiddleCenter, Padding = new Padding(8), AutoEllipsis = true };
            ProgressBar bar = new ProgressBar() { Style = ProgressBarStyle.Marquee, Dock = DockStyle.Top, Height = 20, MarqueeAnimationSpeed = 30 };
            Padding = new Padding(12);
            Controls.Add(bar);
            Controls.Add(_label);

            Theming.ThemeManager.ApplyToForm(this);

            Shown += (s, e) =>
            {
                Thread thread = new Thread(() =>
                {
                    try { Work?.Invoke(Report); }
                    finally { BeginInvoke((Action)Close); }
                });
                thread.IsBackground = true;
                thread.Start();
            };
        }

        private void Report(string step)
        {
            try
            {
                if (!IsDisposed && IsHandleCreated)
                    BeginInvoke((Action)(() => _label.Text = step));
            }
            catch { }
        }
    }

    /* A message too long for a message box: a scrolling text with one or two buttons */
    public class ModReportDialog : Form
    {
        /// <summary>OK for the first button, Cancel for the cancel one, No for the other one (shown between them) if there is one.</summary>
        public static DialogResult Show(IWin32Window owner, string title, string text, string okText, string cancelText, string otherText = null)
        {
            using (ModReportDialog dialog = new ModReportDialog(title, text, okText, cancelText, otherText))
                return dialog.ShowDialog(owner);
        }

        private ModReportDialog(string title, string text, string okText, string cancelText, string otherText)
        {
            Text = title;
            Icon = SharedFormIcon.Icon;
            Size = new Size(720, 460);
            MinimumSize = new Size(480, 300);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;

            TextBox body = new TextBox() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Text = text, BorderStyle = BorderStyle.None };
            FlowLayoutPanel buttons = new FlowLayoutPanel() { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
            if (cancelText != null)
            {
                Button cancel = new Button() { Text = cancelText, AutoSize = true, MinimumSize = new Size(90, 30), DialogResult = DialogResult.Cancel };
                buttons.Controls.Add(cancel);
                CancelButton = cancel;
            }
            if (otherText != null)
                buttons.Controls.Add(new Button() { Text = otherText, AutoSize = true, MinimumSize = new Size(90, 30), DialogResult = DialogResult.No });
            Button ok = new Button() { Text = okText, AutoSize = true, MinimumSize = new Size(90, 30), DialogResult = DialogResult.OK };
            buttons.Controls.Add(ok);
            AcceptButton = ok;
            if (cancelText == null) CancelButton = ok;

            Padding = new Padding(12, 12, 12, 0);
            Controls.Add(body);
            Controls.Add(buttons);
            Theming.ThemeManager.ApplyToForm(this);
            Shown += (s, e) => { body.SelectionStart = 0; body.SelectionLength = 0; ok.Focus(); };
        }
    }
}
#endif

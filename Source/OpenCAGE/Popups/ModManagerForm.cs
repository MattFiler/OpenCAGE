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
        private Panel _header;
        private Label _headline;
        private Label _installState;
        private LinkLabel _installAction;
        private LinkLabel _recheck;
        private Label _copiesNote;
        private readonly ToolTip _copiesNoteTip = new ToolTip() { AutoPopDelay = 30000 };
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
        private bool _scanAgain;
        private bool _rereading;
        private bool _refreshingList;
        private bool _pending;
        private List<string> _stale = new List<string>();

        /* What the selected mod clashes with among the ticked ones is worked out on a thread of its own - it can mean combining
           big files (ModInstaller.Analyze) - and so is reading its file. Every change of the ticks, the order or the selection
           asks again: the newest ask replaces any waiting, and stops the one being worked on. */
        private readonly object _checkLock = new object();
        private DetailsCheck _checkWanted;
        private DetailsCheck _checkRunning;
        private Thread _checkThread;
        private bool _checksPaused;
        private bool _checkMissed;
        private int _detailsAsked;
        private readonly Dictionary<string, ModFacts> _facts = new Dictionary<string, ModFacts>();
        private byte[] _previewShown;

        public ModManagerForm()
        {
            Text = "Mod Manager";
            Icon = SharedFormIcon.Icon;
            Size = new Size(1040, 660);
            MinimumSize = new Size(820, 500);
            StartPosition = FormStartPosition.CenterScreen;
            AllowDrop = true;

            BuildLayout();
            Theming.ThemeManager.ApplyToForm(this);
            OpenCAGE.Popups.Base.BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/installing-mods/");

            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            Shown += OnFirstShown;
            //A check still running when the window closes stops at its next step, and its answer goes nowhere
            FormClosed += (s, e) => PauseChecks();
        }

        private void BuildLayout()
        {
            //Top: what this is, and the state of the game files
            Panel header = _header = new Panel() { Dock = DockStyle.Top, Height = 64, Padding = new Padding(12, 8, 12, 6) };
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
            _installAction.LinkClicked += (s, e) =>
            {
                if (ModServices.State?.Unreadable != null) StartAfresh();
                else RunPristineWizard();
            };
            //Every file read again, whatever its size and time say (a tool can change a file and put both back)
            _recheck = new LinkLabel() { AutoSize = true, Visible = false, Text = "Check every file again", Margin = new Padding(6, 4, 0, 0) };
            _recheck.LinkClicked += (s, e) => CheckEveryFileAgain();
            stateRow.Controls.Add(_installState);
            stateRow.Controls.Add(_installAction);
            stateRow.Controls.Add(_recheck);
            //Copies of the game's own files (a folder renamed and put back, a backup) aren't changes: said apart, so the space
            //they take isn't a mystery (shown when there are any)
            _copiesNote = new Label() { Dock = DockStyle.Top, Height = 20, AutoEllipsis = true, Visible = false, Padding = new Padding(0, 2, 0, 0) };
            header.Controls.Add(_copiesNote);
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
            _modList.Columns.Add("Type", 90);
            _modList.Columns.Add("Status", 140);
            //Once for a run of changes: moving the selection deselects the old row first, and one action can tick many rows
            _modList.SelectedIndexChanged += (s, e) => ShowListChanges(false);
            _modList.ItemChecked += (s, e) => { if (!_refreshingList) ShowListChanges(true); };
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
                _installState.Text = "Set your game folder first (Options > Manage Game Directories) - the Mod Manager works on that copy of the game.";
                foreach (Control control in new Control[] { _addButton, _applyButton, _repairButton, _createButton })
                    control.Enabled = false;
                return;
            }
            //Half-applied mods aren't a state anyone wants to play: put back what was there before, every time
            if (installer.HasCrashJournal())
            {
                //What OpenCAGE has open of the files being put back is loaded again after (saving it first would only be undone)
                LoadedGameFiles.Change change = LoadedGameFiles.BeforeChange(this, installer.CrashJournalFiles(), out string refused, false, false);
                if (change == null)
                {
                    if (refused == "")
                        MessageBox.Show("OpenCAGE was closed part-way through applying mods last time. Your game files need putting back the way they were before that started, "
                            + "before mods can be used: open the Mod Manager again to do it.", "Mod Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Close();
                    return;
                }
                try
                {
                    installer.RecoverCrashJournal();
                    LoadedGameFiles.AfterChange(change, true);
                    MessageBox.Show("OpenCAGE was closed part-way through applying mods last time. Your game files have been put back the way they were before that started."
                        + (change.Reloaded.Count != 0 ? " OpenCAGE loaded " + Sentence(change.Reloaded) + " again." : "")
                        + (change.Problems.Count != 0 ? "\n\n" + string.Join("\n\n", change.Problems) : ""),
                        "Mod Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    LoadedGameFiles.AfterChange(change, true);
                    MessageBox.Show("OpenCAGE was closed part-way through applying mods last time, and putting your game files back didn't work: " + ex.Message
                        + "\n\nClose anything that might be using the game's files, then open the Mod Manager again to try again.", "Mod Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Close();
                    return;
                }
            }

            //A version whose original files OpenCAGE has no record of (mobile, Mac/Linux): mods couldn't be combined with
            //its files or taken out again, and every one of them would look like a mod's
            string unsupported = ModServices.Manifest.Unsupported;
            if (unsupported != null)
            {
                _headline.Text = "Mods can't be used with this version of the game yet.";
                _installState.Text = unsupported;
                foreach (Control control in new Control[] { _modList, _addButton, _removeButton, _upButton, _downButton, _applyButton, _repairButton, _createButton })
                    control.Enabled = false;
                return;
            }

            //Its record of what's installed can't be read: nothing is changed until the user chooses to start afresh
            if (ModServices.State.Unreadable != null)
            {
                _headline.Text = "OpenCAGE can't read its record of the mods installed in this game.";
                _installState.Text = "Nothing will be changed until you choose what to do.";
                _installAction.Text = "Start afresh...";
                _installAction.Visible = true;
                _detailTitle.Text = "";
                _detailByline.Text = "";
                _details.Text = ModServices.State.Unreadable;
                foreach (Control control in new Control[] { _modList, _addButton, _removeButton, _upButton, _downButton, _applyButton, _repairButton, _createButton })
                    control.Enabled = false;
                return;
            }

            RefreshModList();
            StartScan();
        }

        /* A new record in place of one that can't be read, once the user knows what that means: the mods in the library are
           listed again, unticked */
        private void StartAfresh()
        {
            if (MessageBox.Show("Start the Mod Manager afresh?\n\nIt forgets which mods were installed and what they changed. Game files your mods changed stay as they are now - "
                + "verify the game files in Steam (or your store) to put them back as they shipped.\n\nThe mods in your library are listed again, unticked. A copy of the record that "
                + "can't be read is kept beside it.", "Start afresh", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
            try
            {
                ModServices.State.StartAfresh();
                string library = ModToolkit.LibraryDir(ModServices.GameRoot);
                if (Directory.Exists(library))
                    foreach (string package in Directory.GetFiles(library, "*" + ModToolkit.PackageExtension))
                    {
                        try { ModServices.Installer.ImportPackage(package); }
                        catch { }
                    }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Start afresh", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            _headline.Text = "Tick the mods you want to play with, put them in order, then click Apply.";
            _installAction.Visible = false;
            foreach (Control control in new Control[] { _modList, _addButton, _removeButton, _upButton, _downButton, _applyButton, _repairButton, _createButton })
                control.Enabled = true;
            RefreshModList();
            StartScan();
        }

        #region GAME FILES
        private void StartScan()
        {
            if (ModServices.Installer == null || !ModServices.ManifestAvailable)
                return;
            if (_scanning)
            {
                _scanAgain = true;
                return;
            }
            _installState.Text = "Checking your game files...";
            _installAction.Visible = false;
            _recheck.Visible = false;
            _scanning = true;
            bool rereading = _rereading;
            _rereading = false;

            Thread thread = new Thread(() =>
            {
                ScanResult result = null;
                List<string> stale = null;
                string error = null;
                try
                {
                    result = ModServices.NewScanner().Scan(null, (done, total) =>
                    {
                        //Files are only read when they've changed since they were last read: reading them all is the first check's
                        string text = "Checking your game files... " + (done * 100 / Math.Max(1, total)) + "%"
                            + (rereading ? " (reading every file again)" : total > 2L * 1024 * 1024 * 1024 ? " (the first check reads every file - later ones only read the files that have changed)" : "");
                        SafeInvoke(() => _installState.Text = text);
                    });
                    //What's kept as the original of a file found back as the game shipped it, where that says otherwise (a folder
                    //renamed while a mod went in, and put back since), is put right now it can be - or left for the next check
                    try { ModServices.Installer.HealOriginals(result); }
                    catch { }
                }
                catch (Exception e) { error = e.Message; }
                //Which of the mods' files aren't as they were installed: quick once the check has read every file
                try { stale = ModServices.Installer.PathsNeedingRepair(); }
                catch { }

                SafeInvoke(() =>
                {
                    _scanning = false;
                    if (stale != null)
                        _stale = stale;
                    _scan = result;
                    if (error != null)
                        _installState.Text = "Your game files couldn't be checked: " + error;
                    else
                        ShowScanSummary();
                    _recheck.Visible = !_scanAgain;
                    RefreshModList();
                    //Asked for while this one ran (it started before an apply, say): it says how things were, not how they are
                    if (_scanAgain)
                    {
                        _scanAgain = false;
                        StartScan();
                    }
                });
            });
            thread.IsBackground = true;
            thread.Start();
        }

        /* Every game file read again, not only those whose size or time changed since they were last read: the check is
           only as good as those, and a tool can change a file and put both back */
        private void CheckEveryFileAgain()
        {
            if (_scanning || ModServices.Cache == null)
                return;
            ModServices.Cache.ForgetAll();
            _rereading = true;
            StartScan();
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
            _installAction.Visible = false;
            ShowCopies();
            //Changes OpenCAGE has the originals of are safe: mods are combined with them. Ones it hasn't need keeping safe first.
            List<string> changed = _scan.WithStatus(FileStatus.Modified).Concat(_scan.WithStatus(FileStatus.Missing)).Where(o => !ModToolkit.IsRegenerated(o)).ToList();
            List<string> unsafeChanges = changed.Where(o => !ModServices.Installer.HasOriginal(o)).ToList();
            if (unsafeChanges.Count == 0)
            {
                /* What Create a Mod lists, counted the way a person thinks of it: a level is one thing however many files it
                 * has. Files the game never shipped count too - a level made in OpenCAGE is nothing but those - but they have
                 * no original to keep, so they're never what makes a change unsafe. */
                List<string> added = _scan.WithStatus(FileStatus.Foreign).Where(o => !ModToolkit.IsRegenerated(o)).ToList();
                List<string> yours = changed.Concat(added).Select(ModInstaller.LevelUnitOf).Where(o => o != null).Distinct().OrderBy(o => o).ToList();
                List<string> newLevels = yours.Where(IsNewLevel).ToList();
                List<string> ownLevels = yours.Except(newLevels).ToList();
                int ownFiles = changed.Concat(added).Count(o => ModInstaller.LevelUnitOf(o) == null);
                if (ownLevels.Count == 0 && ownFiles == 0 && newLevels.Count == 0)
                {
                    _installState.Text = _stale.Count != 0
                        ? "Some installed mod files have been changed or replaced since they were installed (a game update or a file check does this)."
                        : "Your game files are in good shape.";
                    return;
                }

                List<string> what = new List<string>();
                if (ownLevels.Count != 0) what.Add(ownLevels.Count <= 3 ? string.Join(", ", ownLevels.Select(ModToolkit.LevelName)) : ownLevels.Count + " levels");
                if (ownFiles != 0) what.Add((ownLevels.Count != 0 ? ownFiles + " other" : ownFiles.ToString()) + " game file" + (ownFiles == 1 ? "" : "s"));
                List<string> did = new List<string>();
                if (what.Count != 0) did.Add("changed " + string.Join(" and ", what));
                if (newLevels.Count != 0) did.Add("made " + (newLevels.Count > 3 ? newLevels.Count + " new levels"
                    : (newLevels.Count == 1 ? "the new level " : "the new levels ") + string.Join(", ", newLevels.Select(ModToolkit.LevelName))));
                _installState.Text = "You've " + string.Join(what.Count > 1 ? ", and " : " and ", did) + " yourself."
                    + (what.Count != 0 ? " OpenCAGE keeps the originals, so mods are combined with your changes." : "");
                return;
            }
            List<string> levels = unsafeChanges.Select(ModToolkit.LevelOf).Where(o => o != null).Distinct().OrderBy(o => o).ToList();
            _installState.Text = unsafeChanges.Count + " game file" + (unsafeChanges.Count == 1 ? " has" : "s have") + " been changed without OpenCAGE keeping the original"
                + (levels.Count > 0 ? " (in " + string.Join(", ", levels.Take(3).Select(ModToolkit.LevelName)) + (levels.Count > 3 ? " and more" : "") + ")" : "") + ". Mods can't be combined with those changes until it has one.";
            _installAction.Text = "Keep those changes safe...";
            _installAction.Visible = true;
        }

        /* Copies of the game's own files where the game doesn't use them - a folder renamed and put back by the store's file
           check, a backup - are no change of the user's, and aren't counted as one: said apart, by folder, so a few GB of them
           aren't a mystery */
        private void ShowCopies()
        {
            List<string> copies = _scan.WithStatus(FileStatus.Copy).ToList();
            _copiesNote.Visible = copies.Count != 0;
            _header.Height = copies.Count != 0 ? 84 : 64;
            if (copies.Count == 0)
                return;
            List<string> folders = copies.Select(o => o.Substring(0, o.LastIndexOf('/'))).Distinct().OrderBy(o => o).ToList();
            //A folder inside another one listed is said with it
            folders = folders.Where(o => !folders.Any(other => other != o && o.StartsWith(other + "/"))).ToList();
            long bytes = 0;
            foreach (string copy in copies)
            {
                try { bytes += new FileInfo(ModToolkit.Denormalise(ModServices.GameRoot, copy)).Length; }
                catch { }
            }
            string where = folders.Count <= 2 ? string.Join(" and ", folders) : folders[0] + " and " + (folders.Count - 1) + " other folders";
            _copiesNote.Text = "Not counted as changes: " + copies.Count + " cop" + (copies.Count == 1 ? "y" : "ies") + " of the game's own files (" + ModExportBuilder.PrettySize(bytes)
                + ") in " + where + " - a folder renamed and put back, or a backup.";
            //Which, where it's cut short
            _copiesNoteTip.SetToolTip(_copiesNote, string.Join("\n", copies.Take(30).Select(o => o + "  (the same as " + _scan.Copies[o] + ")")) + (copies.Count > 30 ? "\n..." : "")
                + "\n\nThe game doesn't use " + (copies.Count == 1 ? "it" : "them") + " where " + (copies.Count == 1 ? "it is" : "they are") + ", and mods leave " + (copies.Count == 1 ? "it" : "them")
                + " alone. Delete " + (copies.Count == 1 ? "it" : "them") + " if you don't need " + (copies.Count == 1 ? "it." : "them."));
        }

        /* A level the game doesn't ship anything of - one made in OpenCAGE - the same test Create a Mod uses */
        private static bool IsNewLevel(string level)
        {
            string folder = ModToolkit.LevelFolder(level);
            return !ModServices.Manifest.Entries.Any(o => o.Path.StartsWith(folder));
        }
        #endregion

        #region MOD LIST
        private void RefreshModList()
        {
            _refreshingList = true;
            ModState state = ModServices.State;
            //Which of the mods' files need repairing is the check of the game files' to say (StartScan): it may have to read them

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
                    item.SubItems.Add(mod.Replaces ? "Replacement" : "Combinable");
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
                item.SubItems[4].Text = status;
            }
            //The list's order is kept as soon as it changes, so what's installed is compared in the order it was installed in
            List<string> desired = DesiredEnabledIds();
            List<string> current = state.EnabledInAppliedOrder().Select(o => o.Id).ToList();
            _pending = !desired.SequenceEqual(current);
            bool reorderOnly = _pending && desired.Count == current.Count && !desired.Except(current).Any();
            _applyButton.Enabled = _pending;
            _repairButton.Enabled = !_pending && _stale.Count != 0;
            _statusLabel.Text = reorderOnly ? "Your mods' new order isn't applied yet - click Apply to use it."
                : _pending ? "You have changes that aren't applied yet - click Apply."
                : (_stale.Count != 0 ? "Some mod files need repairing - click Repair." : "");
        }

        /* A tick or a selection: shown once the list's events for it are through (posted, so a run of them is shown once) */
        private bool _listChangesQueued;
        private bool _ticksChanged;
        private void ShowListChanges(bool ticks)
        {
            _ticksChanged |= ticks;
            if (_listChangesQueued || IsDisposed || !IsHandleCreated)
                return;
            _listChangesQueued = true;
            BeginInvoke((Action)(() =>
            {
                _listChangesQueued = false;
                if (IsDisposed)
                    return;
                if (_ticksChanged)
                    UpdatePending();
                _ticksChanged = false;
                ShowDetails();
            }));
        }

        /* The selected mod: what's known at once now, the rest (what its file says, what it clashes with) as it's worked out */
        private void ShowDetails()
        {
            ModState.InstalledMod mod = SelectedMod;
            int asked = ++_detailsAsked;
            if (mod == null)
            {
                CancelCheck();
                ShowPreview(null);
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

            //What it clashes with among the ticked mods (and itself, if it isn't ticked)
            List<string> ids = DesiredEnabledIds();
            if (!ids.Contains(mod.Id)) ids.Add(mod.Id);
            ModInstaller installer = ModServices.Installer;
            ModFacts facts = KnownFacts(installer, mod, out string packagePath);
            bool clashes = ids.Count > 1 && (facts == null || facts.Error == null);
            ShowDetailText(mod, facts, clashes ? CheckingLines() : null);
            if (facts != null && !clashes)
            {
                CancelCheck();
                return;
            }
            StartCheck(new DetailsCheck() { Asked = asked, Installer = installer, Mod = mod, Name = mod.Name, PackagePath = packagePath, NeedFacts = facts == null, Ids = ids.Count > 1 ? ids : null });
        }

        private static List<string> CheckingLines()
        {
            return new List<string>() { "", "Checking what it clashes with among the ticked mods..." };
        }

        /* The details text: what the mod's file says once it's read, then what it clashes with (the lines given, or none) */
        private void ShowDetailText(ModState.InstalledMod mod, ModFacts facts, List<string> clashLines)
        {
            List<string> lines = new List<string>();
            if (facts != null && facts.Error != null)
                lines.Add("This mod's file can't be read: " + facts.Error);
            else
            {
                if (!string.IsNullOrWhiteSpace(mod.Description)) { lines.Add(mod.Description.Trim()); lines.Add(""); }
                if (facts == null)
                    lines.Add("Reading the mod's file...");
                else
                {
                    lines.Add("What it changes:");
                    lines.Add("  " + facts.Summary);
                    lines.Add("");
                    lines.Add(facts.Replaces
                        ? "A replacement mod: it installs exactly as it was made, with nothing rebuilt, and no other mod can change the same levels or files while it's on."
                        : "A combinable mod: it's combined with any other mods (and your own changes) that change the same levels and files.");
                    if (clashLines != null)
                        lines.AddRange(clashLines);
                }
            }
            ShowPreview(facts != null && facts.Error == null ? facts.Preview : null);
            _details.Text = string.Join("\r\n", lines);
        }

        /* What the selected mod clashes with, from the check of the ticked mods */
        private List<string> ClashLines(ModState.InstalledMod mod, ModFacts facts, ModAnalysis analysis)
        {
            List<string> lines = new List<string>();
            List<ReplacementClash> clashes = analysis.Clashes.Where(o => o.Replacing == mod.Name || o.Others.Contains(mod.Name)).ToList();
            if (clashes.Count != 0)
            {
                lines.Add("");
                lines.Add("Can't be on at the same time as other ticked mods - untick one of each:");
                foreach (ReplacementClash clash in clashes.Take(12))
                    lines.Add("  • " + clash.Describe());
                if (clashes.Count > 12)
                    lines.Add("  ...and " + (clashes.Count - 12) + " more");
            }
            List<MergeConflict> conflicts = analysis.Conflicts.Where(o => o.Kept == mod.Name || o.Lost == mod.Name).ToList();
            List<string> shared = analysis.CombinedLevels.Where(facts.Levels.Contains).ToList();
            if (shared.Count != 0)
            {
                lines.Add("");
                lines.Add("Shares " + string.Join(", ", shared.Select(ModToolkit.LevelName)) + " with other ticked mods (or your own changes) - they're combined into one level when you apply.");
            }
            if (analysis.CombinedFiles.Count != 0)
            {
                lines.Add("");
                lines.Add("Shares " + string.Join(", ", analysis.CombinedFiles.Select(MergeConflict.Place)) + " with other ticked mods (or your own changes) - they're combined into one when you apply, and anything they both change is listed then.");
            }
            if (conflicts.Count != 0)
            {
                lines.Add("");
                //The list as it stands: with changes not applied yet, what's installed may still have another mod's version
                lines.Add(_pending ? "Changes the same things as other ticked mods (once you click Apply, the one lower in the list wins):"
                    : "Changes the same things as other ticked mods (the one lower in the list wins):");
                foreach (MergeConflict conflict in conflicts.Take(12))
                    lines.Add("  • " + conflict.Describe());
                if (conflicts.Count > 12)
                    lines.Add("  ...and " + (conflicts.Count - 12) + " more");
            }
            return lines;
        }

        private void ShowPreview(byte[] png)
        {
            if (png == _previewShown)
                return;
            _previewShown = png;
            _preview.Image?.Dispose();
            _preview.Image = null;
            _preview.Visible = false;
            if (png == null)
                return;
            try
            {
                using (MemoryStream stream = new MemoryStream(png))
                    _preview.Image = new Bitmap(Image.FromStream(stream));
                _preview.Visible = true;
            }
            catch { }
        }

        private static string DescribeEntries(ModPackage package)
        {
            List<string> levels = package.Info.Levels;
            int other = package.Info.Entries.Count(o => ModToolkit.LevelOf(o.Target) == null);
            return (levels.Count != 0 ? "the level" + (levels.Count == 1 ? " " : "s ") + string.Join(", ", levels.Select(ModToolkit.LevelName)) : "")
                + (levels.Count != 0 && other != 0 ? ", and " : "")
                + (other != 0 ? other + " other game file" + (other == 1 ? "" : "s") : "") + ".";
        }

        /* What the details show of a mod's file: read on the check's thread (a mod's file is read whole, and can be big), kept
           while the file stays as it was */
        private class ModFacts
        {
            public long Length;
            public DateTime Written;
            public string Error;
            public string Summary;
            public bool Replaces;
            public byte[] Preview;
            /* The levels its files are in, as levels are combined (ModInstaller.LevelUnitOf) */
            public HashSet<string> Levels = new HashSet<string>();
        }

        /* One ask for the selected mod's details: its file read (if it isn't yet), then what it clashes with among Ids (none: no
           other mod is ticked) */
        private class DetailsCheck
        {
            public int Asked;
            public ModInstaller Installer;
            public ModState.InstalledMod Mod;
            public string Name;
            public string PackagePath;
            public bool NeedFacts;
            public List<string> Ids;
            public CancellationTokenSource Cancel = new CancellationTokenSource();
        }

        /* The mod's file as it is now (size and time): -1 when it isn't there */
        private static (long Length, DateTime Written) Stamp(string path)
        {
            try
            {
                FileInfo info = new FileInfo(path);
                if (info.Exists)
                    return (info.Length, info.LastWriteTimeUtc);
            }
            catch { }
            return (-1, DateTime.MinValue);
        }

        /* What was read of a mod's file before, if the file is still as it was then */
        private ModFacts KnownFacts(ModInstaller installer, ModState.InstalledMod mod, out string path)
        {
            path = null;
            try { path = Path.Combine(ModToolkit.LibraryDir(installer.GameRoot), mod.PackageFileName); }
            catch { }
            if (path == null || !_facts.TryGetValue(mod.Id, out ModFacts facts))
                return null;
            (long length, DateTime written) = Stamp(path);
            return facts.Length == length && facts.Written == written ? facts : null;
        }

        private static ModFacts ReadFacts(DetailsCheck check)
        {
            ModFacts facts = new ModFacts();
            if (check.PackagePath != null)
                (facts.Length, facts.Written) = Stamp(check.PackagePath);
            try
            {
                ModPackage package = check.Installer.OpenPackage(check.Mod);
                facts.Summary = string.IsNullOrWhiteSpace(package.Info.Summary) ? DescribeEntries(package) : package.Info.Summary;
                facts.Replaces = package.Info.Replaces;
                facts.Preview = package.ReadPreview();
                facts.Levels = new HashSet<string>(package.Info.Entries.Select(o => ModInstaller.LevelUnitOf(ModToolkit.Normalise(o.Target))).Where(o => o != null));
            }
            catch (Exception e)
            {
                facts.Error = e.Message;
            }
            return facts;
        }

        /* Ask for the details on the check's thread, in place of whatever was asked before */
        private void StartCheck(DetailsCheck check)
        {
            lock (_checkLock)
            {
                _checkRunning?.Cancel.Cancel();
                if (_checksPaused)
                {
                    //Asked again once applying's done (ResumeChecks)
                    _checkWanted = null;
                    _checkMissed = true;
                    return;
                }
                _checkWanted = check;
                if (_checkThread == null)
                {
                    _checkThread = new Thread(CheckLoop) { IsBackground = true, Name = "Mod Manager details", Priority = ThreadPriority.BelowNormal };
                    _checkThread.Start();
                }
            }
        }

        private void CancelCheck()
        {
            lock (_checkLock)
            {
                _checkRunning?.Cancel.Cancel();
                _checkWanted = null;
            }
        }

        /* Applying writes what a check reads: the check stops, and no other starts until ResumeChecks. Its thread is handed back
           to wait for - not on the UI thread, as it can be part-way through combining a big file. */
        private Thread PauseChecks()
        {
            lock (_checkLock)
            {
                _checksPaused = true;
                if (_checkWanted != null || _checkRunning != null)
                    _checkMissed = true;
                _checkWanted = null;
                _checkRunning?.Cancel.Cancel();
                return _checkThread;
            }
        }

        private void ResumeChecks()
        {
            bool missed;
            lock (_checkLock)
            {
                _checksPaused = false;
                missed = _checkMissed;
                _checkMissed = false;
            }
            //What was stopped, or asked for meanwhile, is asked for again
            if (missed && !IsDisposed)
                ShowDetails();
        }

        /* The check's thread: works on the newest ask until there's none */
        private void CheckLoop()
        {
            while (true)
            {
                DetailsCheck check;
                lock (_checkLock)
                {
                    check = _checkWanted;
                    _checkWanted = null;
                    _checkRunning = check;
                    if (check == null)
                    {
                        _checkThread = null;
                        return;
                    }
                }
                try { RunCheck(check); }
                catch { }
                lock (_checkLock)
                    _checkRunning = null;
            }
        }

        private void RunCheck(DetailsCheck check)
        {
            CancellationToken cancel = check.Cancel.Token;
            if (check.NeedFacts)
            {
                ModFacts facts = ReadFacts(check);
                SafeInvoke(() => OnFactsRead(check, facts));
                if (facts.Error != null)
                    return;
            }
            if (check.Ids == null || cancel.IsCancellationRequested)
                return;
            ModAnalysis analysis = null;
            string error = null;
            try { analysis = check.Installer.Analyze(check.Ids, check.Name, cancel); }
            catch (OperationCanceledException) { return; }
            catch (Exception e) { error = e.Message; }
            SafeInvoke(() => OnChecked(check, analysis, error));
        }

        private void OnFactsRead(DetailsCheck check, ModFacts facts)
        {
            if (IsDisposed)
                return;
            _facts[check.Mod.Id] = facts;
            if (check.Asked == _detailsAsked)
                ShowDetailText(check.Mod, facts, check.Ids != null && facts.Error == null ? CheckingLines() : null);
        }

        private void OnChecked(DetailsCheck check, ModAnalysis analysis, string error)
        {
            //An answer to an older ask (the ticks or the selection changed since) isn't shown
            if (IsDisposed || check.Asked != _detailsAsked || !_facts.TryGetValue(check.Mod.Id, out ModFacts facts) || facts.Error != null)
                return;
            ShowDetailText(check.Mod, facts, error != null ? new List<string>() { "", "What it clashes with couldn't be worked out: " + error } : ClashLines(check.Mod, facts, analysis));
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
            HashSet<string> installedBefore = new HashSet<string>(ModServices.State.Mods.Where(o => o.Enabled).Select(o => o.Id));

            //This install's game only: another install's (or another game called AI) doesn't hold these files
            if (EditorUtils.ThisInstallsGameRunning())
            {
                MessageBox.Show("Close Alien: Isolation before changing mods - its files are in use while it runs.", "Game is running", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            //What OpenCAGE has open of the files this may change: saved first (or let go) - before anything is worked out, as saving
            //changes the files - and loaded again once they've changed
            LoadedGameFiles.Change change = LoadedGameFiles.BeforeChange(this, ModServices.Installer.MayChange(desired), out _);
            if (change == null)
                return;
            //The check of the selected mod stops: this works out all of it, then writes what it reads
            Thread check = PauseChecks();
            TransactionResult result = null;
            bool declined = false;
            try
            {
                //Say what will happen before anything does (worked out off the UI thread: it can mean combining big files)
                ModAnalysis analysis = Busy("Working out what applying changes...", () =>
                {
                    check?.Join();
                    return ModServices.Installer.Analyze(desired);
                });
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
                if (conflicts.Count != 0 || combinedLevels.Count != 0 || analysis.OwnChanges.Count != 0 || analysis.OwnSetAside.Count != 0)
                {
                    List<string> lines = new List<string>();
                    if (analysis.OwnSetAside.Count != 0)
                    {
                        lines.Add("You've changed some of these yourself, but a replacement mod replaces them whole. Your changes are set aside while it's on, and come back when it's turned off:");
                        foreach (string what in Friendly(analysis.OwnSetAside).Take(12))
                            lines.Add("  • " + what);
                        lines.Add("");
                    }
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
                            lines.Add("  • " + ModToolkit.LevelName(level));
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
                        lines.Add("Everything else every mod changes is kept. To use a different mod's version, click Cancel, move that mod lower in the list, and click Apply again.");
                    }
                    if (ModReportDialog.Show(this, "Before applying", string.Join("\r\n", lines), "Apply", "Cancel") != DialogResult.OK)
                        return;
                }

                result = RunTransaction(desired, false, edited);
                if (result != null && !result.Success && result.Error != null && result.Error.Contains("no copy of the originals"))
                {
                    if (MessageBox.Show("Some of the game files these mods change have already been changed outside the Mod Manager, and OpenCAGE has no original copy of them to go back to.\n\n"
                        + "Keep what's in them now as the version to go back to when mods are removed? (If you'd rather go back to the original game, choose No and use 'Keep those changes safe...' first.)",
                        "Changed game files", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                        result = RunTransaction(desired, true, edited);
                    else
                        declined = true;
                }
            }
            finally
            {
                LoadedGameFiles.AfterChange(change, FilesChanged(result));
                ResumeChecks();
            }
            if (result == null)
                return;
            if (declined)
            {
                RefreshModList();
                return;
            }

            //Every mod file was just put in as it should be: none needs repairing until the next check says so
            if (result.Success)
                _stale = new List<string>();
            RefreshModList();
            if (!result.Success)
            {
                MessageBox.Show(result.Error + "\n\nNothing was changed: your game files are as they were before you clicked Apply.", "Couldn't apply", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            int enabled = ModServices.State.Mods.Count(o => o.Enabled);
            if (enabled != 0)
                Steam.UnlockAchievement(Steam.Achievements.MOD_INSTALLED);
            //Only the mods this put in count: applying the same set again (or in another order) installs nothing new, and your own kept changes aren't a mod you installed
            Steam.CountTowards(Steam.Achievements.TEN_MODS_INSTALLED, ModServices.State.Mods.Count(o => o.Enabled && !installedBefore.Contains(o.Id) && o.Name != result.KeptAsMod));
            _statusLabel.Text = enabled == 0 ? "All mods removed." : "Done - " + enabled + " mod" + (enabled == 1 ? " is" : "s are") + " installed.";
            if (result.KeptAsMod != null)
                _statusLabel.Text += " Your changes were kept as '" + result.KeptAsMod + "'.";
            if (change.Reloaded.Count != 0)
                _statusLabel.Text += " OpenCAGE loaded " + Sentence(change.Reloaded) + " again.";
            result.Warnings.AddRange(change.Problems);
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
                lines.Add("Combined from several mods: " + string.Join(", ", result.CombinedLevels.Select(ModToolkit.LevelName)) + ".");
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

        /* Callers pause the check of the selected mod first (PauseChecks), and resume it after */
        private TransactionResult RunTransaction(List<string> desired, bool adopt, EditedFilesChoice edited)
        {
            TransactionResult result = null;
            Exception error = null;
            Thread check = PauseChecks();
            using (BusyDialog busy = new BusyDialog("Applying your mods..."))
            {
                busy.Work = report =>
                {
                    //Nothing reads what applying writes: a check part-way through combining a file finishes that first
                    if (check != null && check.IsAlive)
                    {
                        report("Waiting for the check of the ticked mods to stop...");
                        check.Join();
                    }
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
            return result;
        }

        /* Work that can take a while, off the UI thread so the window keeps drawing - with a "working..." window once it's taken
           long enough to notice. What it throws is thrown here. */
        private T Busy<T>(string message, Func<T> work)
        {
            T result = default(T);
            Exception error = null;
            Thread thread = new Thread(() =>
            {
                try { result = work(); }
                catch (Exception e) { error = e; }
            });
            thread.IsBackground = true;
            thread.Start();
            if (!thread.Join(300))
            {
                using (BusyDialog busy = new BusyDialog(message))
                {
                    busy.Work = report => thread.Join();
                    busy.ShowDialog(this);
                }
            }
            if (error != null)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
            return result;
        }

        /* Whether applying changed the game's files: it did when it went in - or when it failed part-way and couldn't put
           them all back (its journal is kept, to finish that next time) */
        private static bool FilesChanged(TransactionResult result)
        {
            return (result != null && result.Success) || ModServices.Installer.HasCrashJournal();
        }

        /* "the level BSP_TORRENS and the animations" */
        private static string Sentence(List<string> things)
        {
            return things.Count == 1 ? things[0] : string.Join(", ", things.Take(things.Count - 1)) + " and " + things.Last();
        }

        private void Repair()
        {
            //As for applying changes: the files are in use while this install's game runs
            if (EditorUtils.ThisInstallsGameRunning())
            {
                MessageBox.Show("Close Alien: Isolation before repairing mods - its files are in use while it runs.", "Game is running", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            //The same mods again: everything rebuilt from the original files and the mods
            List<string> enabled = ModServices.State.ModsInPriorityOrder().Where(o => o.Enabled).Select(o => o.Id).ToList();
            //What OpenCAGE has open of the files: saved (or let go) before the edited ones are worked out, as saving changes them
            LoadedGameFiles.Change change = LoadedGameFiles.BeforeChange(this, ModServices.Installer.MayChange(enabled), out _);
            if (change == null)
                return;
            PauseChecks();
            TransactionResult result = null;
            try
            {
                EditedFilesChoice edited = EditedFilesChoice.Ask;
                List<string> editedFiles = ModServices.Installer.EditedSinceApplied();
                if (editedFiles.Count != 0)
                {
                    EditedFilesChoice? choice = AskAboutEditedFiles(editedFiles);
                    if (choice == null)
                        return;
                    edited = choice.Value;
                }
                result = RunTransaction(enabled, false, edited);
            }
            finally
            {
                LoadedGameFiles.AfterChange(change, FilesChanged(result));
                ResumeChecks();
            }
            if (result != null && result.Success)
                _stale = new List<string>();
            RefreshModList();
            if (result != null && !result.Success)
                MessageBox.Show(result.Error, "Couldn't repair", MessageBoxButtons.OK, MessageBoxIcon.Error);
            else if (result != null)
            {
                _statusLabel.Text = "Repaired - your mods are installed again." + (change.Reloaded.Count != 0 ? " OpenCAGE loaded " + Sentence(change.Reloaded) + " again." : "");
                if (change.Problems.Count != 0)
                    MessageBox.Show(string.Join("\n\n", change.Problems), "Repaired", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
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
                string what = level != null ? "the level " + ModToolkit.LevelName(level) : MergeConflict.Place(path);
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

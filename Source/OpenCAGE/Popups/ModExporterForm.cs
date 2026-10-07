#if ENABLE_MOD_PACKAGES
using OpenCAGE.Modding;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;

namespace OpenCAGE.Popups
{
    /* Package up what the user changed: pick levels, individual config values and loose files from
     * everything that differs from vanilla, give it a name, get a distributable .acmod.
     *
     * Files whose pristine bytes are held in the baseline store ship as small patches; the rest
     * ship whole. Config files ship as just the values that changed. */
    public class ModExporterForm : Form
    {
        private class ConfigChange
        {
            public string Path;
            public List<BmlPatchOp> Ops;
            public byte[] VanillaBytes;
            public XmlDocument Original;
        }

        /* Changes that go in together or not at all (one behaviour tree's) */
        private class OpGroup
        {
            public ConfigChange Config;
            public List<BmlPatchOp> Ops;
        }

        private const string TreeDirectory = "DATA/BINARY_BEHAVIOR/_DIRECTORY_CONTENTS.BML";

        /* What a level's changed files say about what changed in it, in words */
        private static string DescribeLevel(List<string> files)
        {
            List<string> parts = new List<string>();
            if (files.Any(o => o.EndsWith("/WORLD/COMMANDS.PAK"))) parts.Add("scripts");
            if (files.Any(ModLevelMerger.IsBuildOutput)) parts.Add("rebuilt lighting and navigation");
            if (files.Any(o => o.Contains("/TEXT/"))) parts.Add("level text");
            string what = parts.Count == 0 ? "level files" : string.Join(", ", parts) + " and the files saved with them";
            return what + " (" + files.Count + " file" + (files.Count == 1 ? "" : "s") + ")";
        }

        /* A settings file by what it's for: "The alien (DEFAULT.BML)" */
        private static string ConfigTitle(string path)
        {
            string file = path.Substring(path.LastIndexOf('/') + 1);
            string topic = ModExportBuilder.FriendlyConfigName(path);
            return topic == file ? file : Capitalise(topic) + " (" + file + ")";
        }

        private static string Capitalise(string text)
        {
            return string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        private readonly ScanResult _scan;
        private HashSet<string> _deleted = new HashSet<string>();

        /* A row's size, or that the mod deletes the file */
        private string Describe(string normalisedPath)
        {
            return _deleted.Contains(normalisedPath) ? "deleted" : ModExportBuilder.PrettySize(FileSize(normalisedPath));
        }

        private TreeView _tree;
        private ComboBox _updates;
        private TextBox _name;
        private TextBox _author;
        private TextBox _version;
        private TextBox _description;
        private TextBox _changes;
        private PictureBox _picture;
        private Button _exportButton;
        private Label _summary;

        /* The mod this export is a new version of (its id is kept, so it replaces the old one in people's lists), or null */
        private ModState.InstalledMod _updating;
        private byte[] _preview;
        private bool _changesEdited;

        public ModExporterForm(ScanResult scan)
        {
            _scan = scan;

            Text = "Create a Mod";
            Icon = SharedFormIcon.Icon;
            Size = new Size(980, 680);
            MinimumSize = new Size(760, 520);
            StartPosition = FormStartPosition.CenterParent;

            BuildLayout();
            Theming.ThemeManager.ApplyToForm(this);

            Shown += (s, e) => PopulateTree();
        }

        private void BuildLayout()
        {
            _tree = new TreeView() { Dock = DockStyle.Fill, CheckBoxes = true, ShowNodeToolTips = true };
            _tree.AfterCheck += OnAfterCheck;
            Label treeTip = new Label() { Dock = DockStyle.Top, Height = 36, Text = "Everything you've changed in the game is listed here. Untick anything that shouldn't go in the mod.", Padding = new Padding(2, 4, 2, 0) };
            if (ModServices.State?.Mods.Any(o => o.Enabled) == true)
                { treeTip.Text += " Changes your installed mods made aren't listed - only your own work goes in."; treeTip.Height = 52; }
            Panel treePanel = new Panel() { Dock = DockStyle.Fill };
            treePanel.Controls.Add(_tree);
            treePanel.Controls.Add(treeTip);

            //Details on the right
            TableLayoutPanel meta = new TableLayoutPanel() { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(8), AutoScroll = true };
            meta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            meta.Controls.Add(new Label() { Text = "This is", AutoSize = true });
            _updates = new ComboBox() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
            _updates.Items.Add("A new mod");
            foreach (ModState.InstalledMod mod in ModServices.State?.ModsInPriorityOrder() ?? new List<ModState.InstalledMod>())
                _updates.Items.Add(new UpdateChoice(mod));
            _updates.SelectedIndex = 0;
            _updates.SelectedIndexChanged += (s, e) => ChooseUpdate();
            meta.Controls.Add(_updates);
            meta.Controls.Add(new Label() { Text = "Mod name", AutoSize = true, Margin = new Padding(0, 8, 0, 0) });
            _name = new TextBox() { Dock = DockStyle.Top };
            meta.Controls.Add(_name);
            meta.Controls.Add(new Label() { Text = "Author", AutoSize = true, Margin = new Padding(0, 8, 0, 0) });
            _author = new TextBox() { Dock = DockStyle.Top };
            meta.Controls.Add(_author);
            meta.Controls.Add(new Label() { Text = "Version", AutoSize = true, Margin = new Padding(0, 8, 0, 0) });
            _version = new TextBox() { Dock = DockStyle.Top, Text = "1.0" };
            meta.Controls.Add(_version);
            meta.Controls.Add(new Label() { Text = "Description (what players should know)", AutoSize = true, Margin = new Padding(0, 8, 0, 0) });
            _description = new TextBox() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, Height = 110 };
            meta.Controls.Add(_description);
            meta.Controls.Add(new Label() { Text = "What it changes (filled in for you - edit it if you like)", AutoSize = true, Margin = new Padding(0, 8, 0, 0) });
            _changes = new TextBox() { Dock = DockStyle.Top, Multiline = true, Height = 46 };
            _changes.TextChanged += (s, e) => { if (_changes.Focused) _changesEdited = true; };
            meta.Controls.Add(_changes);
            FlowLayoutPanel pictureRow = new FlowLayoutPanel() { Dock = DockStyle.Top, AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
            Button choosePicture = new Button() { Text = "Picture...", AutoSize = true };
            choosePicture.Click += (s, e) => ChoosePicture();
            Button clearPicture = new Button() { Text = "No picture", AutoSize = true };
            clearPicture.Click += (s, e) => { _preview = null; _picture.Image?.Dispose(); _picture.Image = null; };
            pictureRow.Controls.Add(choosePicture);
            pictureRow.Controls.Add(clearPicture);
            meta.Controls.Add(pictureRow);
            _picture = new PictureBox() { Dock = DockStyle.Top, Height = 120, SizeMode = PictureBoxSizeMode.Zoom };
            meta.Controls.Add(_picture);
            meta.RowStyles.Clear();
            for (int i = 0; i < 9; i++) meta.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            meta.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            for (int i = 0; i < 6; i++) meta.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            //The details keep their width as the window grows: the list of changes takes the rest
            SplitContainer split = new SplitContainer() { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel2 };
            split.Panel1.Controls.Add(treePanel);
            split.Panel2.Controls.Add(meta);
            Shown += (s, e) => { try { split.SplitterDistance = Math.Max(split.Panel1MinSize, split.Width - 400); } catch { } };

            _summary = new Label() { Dock = DockStyle.Bottom, Height = 22, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };

            FlowLayoutPanel buttons = new FlowLayoutPanel() { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
            _exportButton = new Button() { Text = "Save Mod...", AutoSize = true, Height = 28 };
            _exportButton.Click += (s, e) => Export();
            Button close = new Button() { Text = "Close", AutoSize = true, Height = 28 };
            close.Click += (s, e) => Close();
            buttons.Controls.Add(close);
            buttons.Controls.Add(_exportButton);

            Controls.Add(split);
            Controls.Add(_summary);
            Controls.Add(buttons);
        }

        #region DETAILS
        private class UpdateChoice
        {
            public readonly ModState.InstalledMod Mod;
            public UpdateChoice(ModState.InstalledMod mod) { Mod = mod; }
            public override string ToString() { return "A new version of '" + Mod.Name + "'"; }
        }

        /* A new version of a mod keeps its id (so it replaces the old one wherever it's installed) and suggests the next version number */
        private void ChooseUpdate()
        {
            _updating = (_updates.SelectedItem as UpdateChoice)?.Mod;
            if (_updating == null)
                return;
            _name.Text = _updating.Name ?? "";
            _author.Text = _updating.Author ?? "";
            _description.Text = _updating.Description ?? "";
            _version.Text = NextVersion(_updating.Version);
            try
            {
                ModPackage package = ModServices.Installer.OpenPackage(_updating);
                byte[] png = package.ReadPreview();
                if (png != null)
                    SetPicture(png);
            }
            catch { }
        }

        private static string NextVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return "1.1";
            string[] parts = version.Trim().Split('.');
            if (int.TryParse(parts[parts.Length - 1], out int last))
            {
                parts[parts.Length - 1] = (last + 1).ToString();
                return string.Join(".", parts);
            }
            return version + ".1";
        }

        private void ChoosePicture()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Pictures (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp";
                dialog.Title = "A picture for the mod";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    //Shown at a few hundred pixels: anything bigger only makes the mod's file bigger
                    using (Image source = Image.FromFile(dialog.FileName))
                    {
                        double scale = Math.Min(1.0, 640.0 / Math.Max(source.Width, source.Height));
                        using (Bitmap scaled = new Bitmap(source, Math.Max(1, (int)(source.Width * scale)), Math.Max(1, (int)(source.Height * scale))))
                        using (MemoryStream png = new MemoryStream())
                        {
                            scaled.Save(png, System.Drawing.Imaging.ImageFormat.Png);
                            SetPicture(png.ToArray());
                        }
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show("That picture couldn't be read: " + e.Message, "Picture", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void SetPicture(byte[] png)
        {
            _preview = png;
            _picture.Image?.Dispose();
            using (MemoryStream stream = new MemoryStream(png))
                _picture.Image = new Bitmap(Image.FromStream(stream));
        }
        #endregion

        #region TREE
        private void PopulateTree()
        {
            _tree.BeginUpdate();
            _tree.Nodes.Clear();

            if (_scan == null)
            {
                _tree.Nodes.Add("The install hasn't been scanned yet - close this window, let the scan finish, and try again.");
                _exportButton.Enabled = false;
                _tree.EndUpdate();
                return;
            }

            //Files worked out again after every install (the level list, each level's behaviour tree list) never ship
            //Your own work under installed mods counts too: it's exported as your version, not the combined file
            List<string> ownUnderMods = ModServices.Installer?.OwnVersionsUnderMods() ?? new List<string>();
            List<string> changed = _scan.WithStatus(FileStatus.Modified).Concat(_scan.WithStatus(FileStatus.Foreign)).Concat(_scan.WithStatus(FileStatus.Missing))
                .Concat(ownUnderMods).Distinct().Where(o => !ModToolkit.IsRegenerated(o)).ToList();
            _deleted = new HashSet<string>(_scan.WithStatus(FileStatus.Missing));
            foreach (string path in ownUnderMods)
                if (ModServices.Installer.TryGetOwnVersion(path, out byte[] own) && own == null)
                    _deleted.Add(path);

            /* A .META sidecar always ships with the file it belongs to (ModExportBuilder.AddFile),
             * so listing it as its own tickable row would offer a choice that isn't real. Fold it
             * into its parent - its bytes are counted there. A sidecar whose parent is unchanged
             * has nothing to fold into and stays a row of its own. */
            HashSet<string> changedSet = new HashSet<string>(changed);
            changed = changed
                .Where(o => !ModExportBuilder.IsSidecar(o) || !changedSet.Contains(ModExportBuilder.SidecarParent(o)))
                .ToList();

            //Group: levels (each whole - a level only works with all its files) / settings (value by value) / behaviour
            //trees (tree by tree) / everything else
            Dictionary<string, List<string>> byLevel = new Dictionary<string, List<string>>();
            List<ConfigChange> configs = new List<ConfigChange>();
            List<string> other = new List<string>();

            foreach (string path in changed.OrderBy(o => o))
            {
                string level = ModInstaller.LevelUnitOf(path);
                if (level != null)
                {
                    List<string> files;
                    if (!byLevel.TryGetValue(level, out files))
                        byLevel[level] = files = new List<string>();
                    files.Add(path);
                    continue;
                }

                if (path.EndsWith(".BML"))
                {
                    byte[] vanilla = VanillaConfigs.GetBest(path);
                    List<BmlPatchOp> ops = vanilla == null ? null : ConfigDiff.Diff(ModServices.GameRoot, path, vanilla, ModServices.Installer);
                    if (ops != null && ops.Count != 0)
                    {
                        XmlDocument original = null;
                        try { original = new CATHODE.BML(vanilla).Content; } catch { }
                        configs.Add(new ConfigChange() { Path = path, Ops = ops, VanillaBytes = vanilla, Original = original });
                        continue;
                    }
                }
                other.Add(path);
            }

            if (byLevel.Count != 0)
            {
                TreeNode levels = _tree.Nodes.Add("Levels");
                foreach (KeyValuePair<string, List<string>> level in byLevel.OrderBy(o => o.Key))
                {
                    TreeNode levelNode = levels.Nodes.Add(level.Key + "  -  " + DescribeLevel(level.Value));
                    levelNode.Tag = level.Value;
                    levelNode.ToolTipText = "Every changed file of the level goes in together:\n" + string.Join("\n", level.Value.Select(o => ShortName(o) + "  (" + Describe(o) + ")"));
                }
                levels.Expand();
            }

            List<ConfigChange> trees = configs.Where(o => o.Path == TreeDirectory).ToList();
            List<ConfigChange> settings = configs.Except(trees).ToList();
            if (settings.Count != 0)
            {
                TreeNode settingsRoot = _tree.Nodes.Add("Settings");
                foreach (ConfigChange config in settings)
                {
                    TreeNode fileNode = settingsRoot.Nodes.Add(ConfigTitle(config.Path) + "  -  " + config.Ops.Count + " change" + (config.Ops.Count == 1 ? "" : "s"));
                    fileNode.Tag = config;
                    foreach (BmlPatchOp op in config.Ops)
                        fileNode.Nodes.Add(Capitalise(BmlPatch.Describe(op, config.Original))).Tag = op;
                    fileNode.Expand();
                }
                settingsRoot.Expand();
            }

            //A behaviour tree changes as a whole: half a tree's edits could leave it broken
            if (trees.Count != 0)
            {
                TreeNode treeRoot = _tree.Nodes.Add("Behaviour trees");
                foreach (ConfigChange config in trees)
                    foreach (IGrouping<string, BmlPatchOp> tree in config.Ops.GroupBy(o => BmlPatch.TreeOf(o) ?? "(the list of trees)").OrderBy(o => o.Key))
                    {
                        List<BmlPatchOp> ops = tree.ToList();
                        string text = ops.Count == 1 && ops[0].Kind == "add" && ops[0].Path.IndexOf('/') < 0 ? "New tree: " + tree.Key
                            : ops.Count == 1 && ops[0].Kind == "remove" && ops[0].Path.Count(c => c == '/') == 1 ? "Removes the tree " + tree.Key
                            : tree.Key + "  -  " + ops.Count + " change" + (ops.Count == 1 ? "" : "s");
                        TreeNode node = treeRoot.Nodes.Add(text);
                        node.Tag = new OpGroup() { Config = config, Ops = ops };
                        node.ToolTipText = string.Join("\n", ops.Take(20).Select(o => Capitalise(BmlPatch.Describe(o, config.Original)))) + (ops.Count > 20 ? "\n..." : "");
                    }
                treeRoot.Expand();
            }

            if (other.Count != 0)
            {
                TreeNode otherRoot = _tree.Nodes.Add("Other files");
                foreach (string file in other)
                {
                    TreeNode node = otherRoot.Nodes.Add(Capitalise(Modding.Merging.MergeConflict.Place(file)) + "  -  " + Describe(file));
                    node.Tag = file;
                    node.ToolTipText = file;
                }
                otherRoot.Expand();
            }
            if (_tree.Nodes.Count == 0)
            {
                _tree.Nodes.Add("You haven't changed anything in the game yet - make your changes in OpenCAGE, then come back here.");
                _exportButton.Enabled = false;
            }
            else
            {
                //Most mods are everything the author changed: it all starts ticked
                foreach (TreeNode root in _tree.Nodes)
                    root.Checked = true;
            }

            _tree.EndUpdate();
            UpdateSummary();
        }

        private static string DescribeOp(BmlPatchOp op)
        {
            switch (op.Kind)
            {
                case "set": return op.Claim + " = " + Truncate(op.Value, 60);
                case "settext": return op.Path + " text = " + Truncate(op.Value, 60);
                case "removeattr": return "remove " + op.Claim;
                case "add": return "add " + Truncate(op.Xml, 70);
                case "remove": return "remove " + op.Path;
                case "replace": return "replace " + op.Path;
                default: return op.Kind + " " + op.Claim;
            }
        }

        private static string Truncate(string text, int length)
        {
            if (text == null) return "";
            return text.Length <= length ? text : text.Substring(0, length) + "...";
        }

        /// <summary>
        /// What this row contributes to the package: the file, plus the .META sidecar that ships
        /// with it (which has no row of its own - see PopulateTree).
        /// </summary>
        private long FileSize(string normalisedPath)
        {
            return SizeOnDisk(normalisedPath) + SizeOnDisk(ModExportBuilder.SidecarFor(normalisedPath));
        }

        private long SizeOnDisk(string normalisedPath)
        {
            if (normalisedPath == null)
                return 0;
            try
            {
                FileInfo info = new FileInfo(ModToolkit.Denormalise(ModServices.GameRoot, normalisedPath));
                return info.Exists ? info.Length : 0;
            }
            catch { return 0; }
        }

        private static string ShortName(string normalisedPath)
        {
            int at = normalisedPath.IndexOf("/PRODUCTION/");
            if (at < 0) return normalisedPath;
            int levelSlash = normalisedPath.IndexOf('/', at + "/PRODUCTION/".Length);
            return levelSlash < 0 ? normalisedPath : normalisedPath.Substring(levelSlash + 1);
        }

        private bool _cascading;
        private void OnAfterCheck(object sender, TreeViewEventArgs e)
        {
            if (_cascading) return;
            _cascading = true;
            SetChildren(e.Node, e.Node.Checked);
            //A checked child means the parent is (at least partially) in
            for (TreeNode parent = e.Node.Parent; parent != null; parent = parent.Parent)
                if (e.Node.Checked && !parent.Checked)
                    parent.Checked = true;
            _cascading = false;
            UpdateSummary();
        }

        private void SetChildren(TreeNode node, bool value)
        {
            foreach (TreeNode child in node.Nodes)
            {
                child.Checked = value;
                SetChildren(child, value);
            }
        }

        private void UpdateSummary()
        {
            List<string> files = SelectedFiles();
            List<KeyValuePair<ConfigChange, List<BmlPatchOp>>> configs = SelectedConfigs();
            //What's ticked, counted the way the list shows it
            int levelCount = AllNodes(_tree.Nodes).Count(o => o.Checked && o.Tag is List<string>);
            int settingCount = configs.Where(o => o.Key.Path != TreeDirectory).Sum(o => o.Value.Count);
            int treeCount = AllNodes(_tree.Nodes).Count(o => o.Checked && o.Tag is OpGroup);
            int otherCount = AllNodes(_tree.Nodes).Count(o => o.Checked && o.Tag is string);
            List<string> counts = new List<string>();
            if (levelCount != 0) counts.Add(levelCount + " level" + (levelCount == 1 ? "" : "s"));
            if (settingCount != 0) counts.Add(settingCount + " setting" + (settingCount == 1 ? "" : "s"));
            if (treeCount != 0) counts.Add(treeCount + " behaviour tree" + (treeCount == 1 ? "" : "s"));
            if (otherCount != 0) counts.Add(otherCount + " other file" + (otherCount == 1 ? "" : "s"));
            _summary.Text = counts.Count == 0 ? "Nothing ticked yet." : "In your mod: " + string.Join(", ", counts) + ".";
            _exportButton.Enabled = counts.Count != 0;

            //What the mod changes, in words, until the author writes their own
            if (!_changesEdited && ModServices.Installer != null)
            {
                try
                {
                    ModExportBuilder preview = new ModExportBuilder(ModServices.GameRoot, ModServices.Manifest, ModServices.Cache, ModServices.Installer);
                    preview.AddFiles(files);
                    foreach (KeyValuePair<ConfigChange, List<BmlPatchOp>> config in configs)
                        preview.AddConfigPatch(config.Key.Path, config.Value, config.Key.VanillaBytes);
                    _changes.Text = preview.Summarise();
                }
                catch { }
            }
        }
        #endregion

        #region SELECTION
        private List<string> SelectedFiles()
        {
            List<string> files = new List<string>();
            foreach (TreeNode node in AllNodes(_tree.Nodes))
            {
                if (!node.Checked) continue;
                if (node.Tag is string file) files.Add(file);
                else if (node.Tag is List<string> level) files.AddRange(level);
            }
            return files.Distinct().ToList();
        }

        private List<KeyValuePair<ConfigChange, List<BmlPatchOp>>> SelectedConfigs()
        {
            Dictionary<ConfigChange, List<BmlPatchOp>> chosen = new Dictionary<ConfigChange, List<BmlPatchOp>>();
            void Choose(ConfigChange config, IEnumerable<BmlPatchOp> ops)
            {
                if (!chosen.TryGetValue(config, out List<BmlPatchOp> list))
                    chosen[config] = list = new List<BmlPatchOp>();
                list.AddRange(ops);
            }
            foreach (TreeNode node in AllNodes(_tree.Nodes))
            {
                if (node.Tag is ConfigChange config)
                    Choose(config, node.Nodes.Cast<TreeNode>().Where(o => o.Checked && o.Tag is BmlPatchOp).Select(o => (BmlPatchOp)o.Tag));
                else if (node.Tag is OpGroup group && node.Checked)
                    Choose(group.Config, group.Ops);
            }
            //In the order the file had them, which is the order they apply in
            return chosen.Where(o => o.Value.Count != 0)
                .Select(o => new KeyValuePair<ConfigChange, List<BmlPatchOp>>(o.Key, o.Key.Ops.Where(o.Value.Contains).ToList())).ToList();
        }

        private IEnumerable<TreeNode> AllNodes(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                yield return node;
                foreach (TreeNode child in AllNodes(node.Nodes))
                    yield return child;
            }
        }
        #endregion

        #region EXPORT
        private void Export()
        {
            List<string> files = SelectedFiles();
            List<KeyValuePair<ConfigChange, List<BmlPatchOp>>> configs = SelectedConfigs();
            if (files.Count == 0 && configs.Count == 0)
            {
                MessageBox.Show("Tick the changes to include first.", "Nothing selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_name.Text.Trim().Length == 0)
            {
                MessageBox.Show("Give the mod a name.", "No name", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            //Work someone else's mod would be baked in? Say so before it ships.
            List<string> overlaps = new List<string>();
            foreach (ModState.InstalledMod mod in ModServices.State.Mods.Where(o => o.Enabled))
                foreach (string path in files)
                    if (mod.Applied.ContainsKey(path) && !ModServices.Installer.TryGetOwnVersion(path, out _))
                        overlaps.Add(path + " (from '" + mod.Name + "')");
            if (overlaps.Count != 0)
            {
                if (MessageBox.Show("Some selected files are currently supplied by other enabled mods - their content would be baked into your package:\n\n  "
                    + string.Join("\n  ", overlaps.Take(8).ToArray()) + (overlaps.Count > 8 ? "\n  ..." : "")
                    + "\n\nExport anyway?", "Other mods' work included", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }

            string output;
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "OpenCAGE mod packages (*" + ModToolkit.PackageExtension + ")|*" + ModToolkit.PackageExtension;
                dialog.FileName = SafeFileName(_name.Text.Trim()) + ModToolkit.PackageExtension;
                dialog.Title = "Export mod package";
                if (dialog.ShowDialog() != DialogResult.OK)
                    return;
                output = dialog.FileName;
            }

            ExportResult result = null;
            Exception error = null;
            using (BusyDialog busy = new BusyDialog("Building the package..."))
            {
                busy.Work = report =>
                {
                    try
                    {
                        ModExportBuilder builder = new ModExportBuilder(ModServices.GameRoot, ModServices.Manifest, ModServices.Cache, ModServices.Installer);
                        if (_updating != null)
                            builder.Info.Id = _updating.Id;
                        builder.Info.Name = _name.Text.Trim();
                        builder.Info.Author = _author.Text.Trim();
                        builder.Info.Version = _version.Text.Trim();
                        builder.Info.Description = _description.Text;
                        builder.Info.Summary = _changes.Text.Trim();
                        builder.Info.OpenCageVersion = Singleton.Version;
                        builder.Preview = _preview;
                        builder.AddFiles(files);
                        foreach (KeyValuePair<ConfigChange, List<BmlPatchOp>> config in configs)
                            builder.AddConfigPatch(config.Key.Path, config.Value, config.Key.VanillaBytes);
                        result = builder.Write(output);
                    }
                    catch (Exception e) { error = e; }
                };
                busy.ShowDialog(this);
            }

            if (error != null)
            {
                MessageBox.Show("Export failed: " + error.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string message = "Your mod is saved as " + Path.GetFileName(output) + " (" + ModExportBuilder.PrettySize(result.PackageSize) + ").\n\n"
                + "Share that file: anyone with OpenCAGE can add it to their Mod Manager (or just double-click it) and install it alongside their other mods."
                + (result.Warnings.Count != 0 ? "\n\n" + string.Join("\n", result.Warnings.Take(8).ToArray()) : "");
            MessageBox.Show(message, "Mod saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static string SafeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
        #endregion
    }
}
#endif

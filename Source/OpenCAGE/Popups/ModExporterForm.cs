#if ENABLE_MOD_PACKAGES
using OpenCAGE.Modding;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

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
        }

        private readonly ScanResult _scan;

        private TreeView _tree;
        private TextBox _name;
        private TextBox _author;
        private TextBox _version;
        private TextBox _description;
        private Button _exportButton;
        private Label _summary;

        public ModExporterForm(ScanResult scan)
        {
            _scan = scan;

            Text = "Export Mod";
            Icon = SharedFormIcon.Icon;
            Size = new Size(900, 640);
            MinimumSize = new Size(700, 480);
            StartPosition = FormStartPosition.CenterParent;

            BuildLayout();
            Theming.ThemeManager.ApplyToForm(this);

            Shown += (s, e) => PopulateTree();
        }

        private void BuildLayout()
        {
            _tree = new TreeView() { Dock = DockStyle.Fill, CheckBoxes = true };
            _tree.AfterCheck += OnAfterCheck;

            //Metadata on the right
            TableLayoutPanel meta = new TableLayoutPanel() { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(8) };
            meta.Controls.Add(new Label() { Text = "Mod name", AutoSize = true });
            _name = new TextBox() { Dock = DockStyle.Top };
            meta.Controls.Add(_name);
            meta.Controls.Add(new Label() { Text = "Author", AutoSize = true, Margin = new Padding(0, 8, 0, 0) });
            _author = new TextBox() { Dock = DockStyle.Top };
            meta.Controls.Add(_author);
            meta.Controls.Add(new Label() { Text = "Version", AutoSize = true, Margin = new Padding(0, 8, 0, 0) });
            _version = new TextBox() { Dock = DockStyle.Top, Text = "1.0" };
            meta.Controls.Add(_version);
            meta.Controls.Add(new Label() { Text = "Description", AutoSize = true, Margin = new Padding(0, 8, 0, 0) });
            _description = new TextBox() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, Height = 180 };
            meta.Controls.Add(_description);
            meta.RowStyles.Clear();
            for (int i = 0; i < 7; i++) meta.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            meta.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            SplitContainer split = new SplitContainer() { Dock = DockStyle.Fill };
            split.Panel1.Controls.Add(_tree);
            split.Panel2.Controls.Add(meta);

            _summary = new Label() { Dock = DockStyle.Bottom, Height = 22, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };

            FlowLayoutPanel buttons = new FlowLayoutPanel() { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
            _exportButton = new Button() { Text = "Export...", AutoSize = true, Height = 28 };
            _exportButton.Click += (s, e) => Export();
            Button close = new Button() { Text = "Close", AutoSize = true, Height = 28 };
            close.Click += (s, e) => Close();
            buttons.Controls.Add(close);
            buttons.Controls.Add(_exportButton);

            Controls.Add(split);
            Controls.Add(_summary);
            Controls.Add(buttons);
        }

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

            List<string> changed = _scan.WithStatus(FileStatus.Modified).Concat(_scan.WithStatus(FileStatus.Foreign)).ToList();

            /* A .META sidecar always ships with the file it belongs to (ModExportBuilder.AddFile),
             * so listing it as its own tickable row would offer a choice that isn't real. Fold it
             * into its parent - its bytes are counted there. A sidecar whose parent is unchanged
             * has nothing to fold into and stays a row of its own. */
            HashSet<string> changedSet = new HashSet<string>(changed);
            changed = changed
                .Where(o => !ModExportBuilder.IsSidecar(o) || !changedSet.Contains(ModExportBuilder.SidecarParent(o)))
                .ToList();

            //Group: levels / configs (value level) / everything else
            Dictionary<string, List<string>> byLevel = new Dictionary<string, List<string>>();
            List<ConfigChange> configs = new List<ConfigChange>();
            List<string> other = new List<string>();

            foreach (string path in changed.OrderBy(o => o))
            {
                string level = ModToolkit.LevelOf(path);
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
                    List<BmlPatchOp> ops = vanilla == null ? null : ConfigDiff.Diff(ModServices.GameRoot, path, vanilla);
                    if (ops != null && ops.Count != 0)
                    {
                        configs.Add(new ConfigChange() { Path = path, Ops = ops, VanillaBytes = vanilla });
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
                    long size = level.Value.Sum(o => FileSize(o));
                    TreeNode levelNode = levels.Nodes.Add(level.Key + "   (" + level.Value.Count + " changed files, " + ModExportBuilder.PrettySize(size) + " before patching)");
                    levelNode.Tag = level.Value;
                    foreach (string file in level.Value)
                        levelNode.Nodes.Add(ShortName(file) + "  (" + ModExportBuilder.PrettySize(FileSize(file)) + ")").Tag = file;
                }
                levels.Expand();
            }

            if (configs.Count != 0)
            {
                TreeNode configRoot = _tree.Nodes.Add("Config values");
                foreach (ConfigChange config in configs)
                {
                    TreeNode fileNode = configRoot.Nodes.Add(config.Path + "   (" + config.Ops.Count + " change" + (config.Ops.Count == 1 ? "" : "s") + ")");
                    fileNode.Tag = config;
                    foreach (BmlPatchOp op in config.Ops)
                        fileNode.Nodes.Add(DescribeOp(op)).Tag = op;
                }
                configRoot.Expand();
            }

            if (other.Count != 0)
            {
                TreeNode otherRoot = _tree.Nodes.Add("Other files");
                foreach (string file in other)
                    otherRoot.Nodes.Add(file + "  (" + ModExportBuilder.PrettySize(FileSize(file)) + ")").Tag = file;
            }

            if (_tree.Nodes.Count == 0)
            {
                _tree.Nodes.Add("Nothing differs from vanilla - there is nothing to export.");
                _exportButton.Enabled = false;
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
            long size = files.Sum(o => FileSize(o));
            _summary.Text = files.Count + " file" + (files.Count == 1 ? "" : "s") + " (" + ModExportBuilder.PrettySize(size) + " before patching), "
                + configs.Sum(o => o.Value.Count) + " config value" + (configs.Sum(o => o.Value.Count) == 1 ? "" : "s");
        }
        #endregion

        #region SELECTION
        private List<string> SelectedFiles()
        {
            List<string> files = new List<string>();
            foreach (TreeNode node in AllNodes(_tree.Nodes))
                if (node.Checked && node.Tag is string)
                    files.Add((string)node.Tag);
            return files.Distinct().ToList();
        }

        private List<KeyValuePair<ConfigChange, List<BmlPatchOp>>> SelectedConfigs()
        {
            List<KeyValuePair<ConfigChange, List<BmlPatchOp>>> result = new List<KeyValuePair<ConfigChange, List<BmlPatchOp>>>();
            foreach (TreeNode node in AllNodes(_tree.Nodes))
            {
                ConfigChange config = node.Tag as ConfigChange;
                if (config == null)
                    continue;
                List<BmlPatchOp> ops = new List<BmlPatchOp>();
                foreach (TreeNode child in node.Nodes)
                    if (child.Checked && child.Tag is BmlPatchOp)
                        ops.Add((BmlPatchOp)child.Tag);
                if (ops.Count != 0)
                    result.Add(new KeyValuePair<ConfigChange, List<BmlPatchOp>>(config, ops));
            }
            return result;
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
                    if (mod.Applied.ContainsKey(path))
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
                busy.Work = () =>
                {
                    try
                    {
                        ModExportBuilder builder = new ModExportBuilder(ModServices.GameRoot, ModServices.Manifest, ModServices.Cache, ModServices.Installer);
                        builder.Info.Name = _name.Text.Trim();
                        builder.Info.Author = _author.Text.Trim();
                        builder.Info.Version = _version.Text.Trim();
                        builder.Info.Description = _description.Text;
                        builder.Info.OpenCageVersion = Singleton.Version;
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

            int deltas = result.Entries.Count(o => o.Kind == ModPackageEntry.KindDelta);
            string message = "Exported " + result.Entries.Count + " entries (" + deltas + " as patches against vanilla).\n"
                + "Package size: " + ModExportBuilder.PrettySize(result.PackageSize)
                + (result.Warnings.Count != 0 ? "\n\n" + string.Join("\n", result.Warnings.Take(8).ToArray()) : "");
            MessageBox.Show(message, "Export complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

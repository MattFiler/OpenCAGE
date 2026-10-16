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

        /* A level the user changed, and the parts of it the list offers by its edit history */
        private class LevelChanges
        {
            public string Level;
            public List<string> Files = new List<string>(); //Every changed file of it: what goes when it goes whole
            public bool New; //The game doesn't ship it: it goes whole
            public bool UnderMods; //Installed mods change it too: the user's own version goes, whole
            public bool Built; //Its lighting and navigation were rebuilt: a part of it is built too
            public List<CompositeRow> Composites = new List<CompositeRow>();
            public List<AssetRow> Assets = new List<AssetRow>();
            public List<string> LooseFiles = new List<string>(); //Files its save doesn't write (its text)
            public bool HasHistory => Composites.Count != 0 || Assets.Count != 0;
            public bool Whole => New || UnderMods;
        }

        /* A composite of a level the user changed, from its edit history */
        private class CompositeRow
        {
            public LevelChanges Level;
            public CATHODE.Scripting.ShortGuid Id;
            public string Name; //Its path, as the composite browser shows it - null for one since deleted
            public DateTime Edited;
        }

        /* An asset of a level the user changed in place, from its edit history */
        private class AssetRow
        {
            public LevelChanges Level;
            public CathodeLib.AssetModificationInfoTable.AssetKind Kind;
            public string Name;
            public DateTime Edited;
            public bool WholeOnly; //Can't be taken out of the level on its own: it goes with everything else in the level
        }

        /* One of a level's files its save doesn't write (its text), taken as it is */
        private class LooseRow
        {
            public LevelChanges Level;
            public string Path;
        }

        /* Everything else the user changed in a level - with the rest of its rows, the whole level as it is now */
        private class RestRow
        {
            public LevelChanges Level;
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
        private bool _summarising;
        //Working out whether an animation PAK changes only its tree layouts (once a form): the export waits for it rather than doing it again
        private System.Threading.Thread _examiningAnimations;

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
            OpenCAGE.Popups.Base.BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/making-mods/");

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
            _changes.TextChanged += (s, e) => { if (_changes.Focused && !_summarising) _changesEdited = true; };
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

            /* A sidecar (a .META) always ships with the
             * file it belongs to (ModExportBuilder.AddFile), so listing it as its own tickable row
             * would offer a choice that isn't real. Fold it into its parent - its bytes are counted
             * there. A sidecar whose parent is unchanged has nothing to fold into and stays a row
             * of its own. */
            HashSet<string> changedSet = new HashSet<string>(changed);
            changed = changed
                .Where(o => !ModExportBuilder.ShipsWithParent(o, changedSet))
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
                //What in each level the user changed, by its edit history: reading the names of the composites takes a moment
                List<LevelChanges> levels = new List<LevelChanges>();
                using (BusyDialog busy = new BusyDialog("Reading what you've changed in your levels..."))
                {
                    busy.Work = report =>
                    {
                        foreach (KeyValuePair<string, List<string>> level in byLevel.OrderBy(o => ModToolkit.LevelName(o.Key)))
                        {
                            report("Reading what you've changed in " + ModToolkit.LevelName(level.Key) + "...");
                            levels.Add(ReadLevel(level.Key, level.Value));
                        }
                    };
                    busy.ShowDialog(this);
                }
                AddLevelRows(levels);
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
                    DateTime? edited = EditHistory.SharedFileEdited(ModServices.GameRoot, file);
                    TreeNode node = otherRoot.Nodes.Add(Capitalise(Modding.Merging.MergeConflict.Place(file)) + "  -  " + Describe(file) + (edited.HasValue ? ", " + EditedText(edited.Value) : ""));
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
                //Most mods are everything the author changed: it all starts ticked - except the rest of a level whose edit
                //history says what in it the user changed: what they changed starts ticked, the rest of the level doesn't
                foreach (TreeNode root in _tree.Nodes)
                    root.Checked = true;
                _cascading = true;
                foreach (KeyValuePair<LevelChanges, TreeNode> rest in _restNodes)
                    if (rest.Key.HasHistory && !rest.Key.Assets.Any(o => o.WholeOnly))
                        rest.Value.Checked = false;
                _cascading = false;
            }

            _tree.EndUpdate();
            UpdateSummary();
        }

        /* The kinds of asset a mod can take out of a level on their own; the rest go with everything else in it */
        private static readonly HashSet<CathodeLib.AssetModificationInfoTable.AssetKind> _partialKinds = new HashSet<CathodeLib.AssetModificationInfoTable.AssetKind>()
        {
            CathodeLib.AssetModificationInfoTable.AssetKind.TEXTURE,
            CathodeLib.AssetModificationInfoTable.AssetKind.MODEL,
            CathodeLib.AssetModificationInfoTable.AssetKind.MATERIAL,
            CathodeLib.AssetModificationInfoTable.AssetKind.CHARACTER_ACCESSORIES,
            CathodeLib.AssetModificationInfoTable.AssetKind.GALAXY,
        };

        /* A level's changes: whole where it can only go whole, else what its edit history says the user changed in it. Off the
           UI thread; never throws (a level whose history can't be read goes whole) */
        private static LevelChanges ReadLevel(string level, List<string> files)
        {
            LevelChanges changes = new LevelChanges() { Level = level, Files = files };
            try
            {
                string folder = ModToolkit.LevelFolder(level);
                changes.New = !ModServices.Manifest.Entries.Any(o => o.Path.StartsWith(folder));
                //A level installed mods change holds their changes as well as the user's, and only its files tell them apart
                changes.UnderMods = files.Any(o => ModServices.Installer.TryGetOwnVersion(o, out _))
                    || ModServices.State.Composition.Keys.Any(o => ModInstaller.LevelUnitOf(o) == level);
                changes.Built = files.Any(ModLevelMerger.IsBuildOutput);
                if (changes.Whole)
                    return changes;
                changes.LooseFiles = files.Where(o => o.Contains("/TEXT/")).ToList();

                //A Nostromo level keeps its scripts (and their history) in its _PATCH folder
                string commandsPath = ModToolkit.Denormalise(ModServices.GameRoot, ModToolkit.LevelFolder(level + "_PATCH") + "WORLD/COMMANDS.PAK");
                if (!File.Exists(commandsPath))
                    commandsPath = ModToolkit.Denormalise(ModServices.GameRoot, folder + "WORLD/COMMANDS.PAK");
                if (!File.Exists(commandsPath))
                    return changes;
                CathodeLib.CompositeModificationInfoTable composites = CathodeLib.CustomTable.ReadTable(commandsPath, CATHODE.Scripting.Internal.CustomTableType.COMPOSITE_MODIFICATION_INFO) as CathodeLib.CompositeModificationInfoTable;
                CathodeLib.AssetModificationInfoTable assets = CathodeLib.CustomTable.ReadTable(commandsPath, CATHODE.Scripting.Internal.CustomTableType.ASSET_MODIFICATION_INFO) as CathodeLib.AssetModificationInfoTable;
                if (composites != null && composites.modification_info.Count != 0)
                {
                    //For their names: the scripts alone, the level's tables their resources point into left empty
                    string none = Path.Combine(Path.GetTempPath(), "OpenCAGE-no-such-file");
                    CATHODE.Commands commands = new CATHODE.Commands(commandsPath, new CATHODE.EnvironmentAnimations(none, null), new CATHODE.CollisionMaps(none, null, null), new CATHODE.RenderableElements(none, null, null));
                    foreach (CathodeLib.CompositeModificationInfoTable.ModificationInfo info in composites.modification_info)
                        changes.Composites.Add(new CompositeRow() { Level = changes, Id = info.composite_id, Name = commands.GetComposite(info.composite_id)?.name, Edited = EditHistory.ToLocal(info.modification_date) });
                }
                if (assets != null)
                    foreach (CathodeLib.AssetModificationInfoTable.ModificationInfo info in assets.modification_info)
                        changes.Assets.Add(new AssetRow() { Level = changes, Kind = info.kind, Name = info.name, Edited = EditHistory.ToLocal(info.modification_date), WholeOnly = !_partialKinds.Contains(info.kind) });
            }
            catch
            {
                changes.Composites.Clear();
                changes.Assets.Clear();
            }
            return changes;
        }

        /* Each level's "everything else" row, and the rows of the parts of it */
        private readonly Dictionary<LevelChanges, TreeNode> _restNodes = new Dictionary<LevelChanges, TreeNode>();
        private readonly Dictionary<LevelChanges, List<TreeNode>> _partNodes = new Dictionary<LevelChanges, List<TreeNode>>();

        /* What the user changed in their levels: the composites and assets they edited (newest first; a composite changed in
           several levels is one row, ticking it in each), then each level - a new one whole, else its other changes */
        private void AddLevelRows(List<LevelChanges> levels)
        {
            _restNodes.Clear();
            _partNodes.Clear();

            List<CompositeRow> composites = levels.SelectMany(o => o.Composites).ToList();
            if (composites.Count != 0)
            {
                TreeNode root = _tree.Nodes.Add("Scripts you've changed");
                foreach (IGrouping<CATHODE.Scripting.ShortGuid, CompositeRow> group in composites.GroupBy(o => o.Id).OrderByDescending(g => g.Max(o => o.Edited)))
                {
                    List<CompositeRow> rows = group.OrderByDescending(o => o.Edited).ToList();
                    string path = rows.Select(o => o.Name).FirstOrDefault(o => o != null);
                    string title = path == null ? "A composite you deleted" : CompositeLeaf(path);
                    TreeNode node = root.Nodes.Add(title + "  -  " + (rows.Count == 1 ? LevelTitle(rows[0].Level) + ", " + EditedText(rows[0].Edited) : rows.Count + " levels, last " + EditedText(rows[0].Edited)));
                    node.ToolTipText = path ?? "";
                    if (rows.Count == 1)
                        AddPart(node, rows[0], rows[0].Level);
                    else
                        foreach (CompositeRow row in rows)
                            AddPart(node.Nodes.Add(LevelTitle(row.Level) + ", " + EditedText(row.Edited)), row, row.Level);
                }
                root.Expand();
            }

            List<AssetRow> assets = levels.SelectMany(o => o.Assets).OrderByDescending(o => o.Edited).ToList();
            if (assets.Count != 0)
            {
                TreeNode root = _tree.Nodes.Add("Models, textures and materials you've changed");
                foreach (AssetRow asset in assets)
                {
                    TreeNode node = root.Nodes.Add(AssetTitle(asset) + "  -  " + LevelTitle(asset.Level) + ", " + EditedText(asset.Edited)
                        + (asset.WholeOnly ? " (goes in with everything else in the level)" : ""));
                    AddPart(node, asset, asset.Level);
                }
                root.Expand();
            }

            TreeNode levelsRoot = _tree.Nodes.Add("Levels");
            foreach (LevelChanges level in levels)
            {
                string count = DescribeLevel(level.Files);
                if (level.Whole || (!level.HasHistory && level.LooseFiles.Count == 0))
                {
                    string what = level.New ? "a new level (" + level.Files.Count + " file" + (level.Files.Count == 1 ? "" : "s") + ")"
                        : level.UnderMods ? "your own version of it, whole (mods you've installed change it too): " + count
                        : "changed before OpenCAGE kept a history of edits, so the whole level as it is now: " + count;
                    TreeNode whole = levelsRoot.Nodes.Add(LevelTitle(level) + "  -  " + what);
                    whole.ToolTipText = "Every changed file of the level goes in together:\n" + string.Join("\n", level.Files.Select(o => ShortName(o) + "  (" + Describe(o) + ")"));
                    whole.Tag = new RestRow() { Level = level };
                    _restNodes[level] = whole;
                    continue;
                }
                TreeNode levelNode = levelsRoot.Nodes.Add(LevelTitle(level));
                foreach (string loose in level.LooseFiles)
                {
                    DateTime? edited = EditHistory.SharedFileEdited(ModServices.GameRoot, loose);
                    AddPart(levelNode.Nodes.Add("Text: " + ShortName(loose) + (edited.HasValue ? ", " + EditedText(edited.Value) : "")), new LooseRow() { Level = level, Path = loose }, level);
                }
                TreeNode rest = levelNode.Nodes.Add(level.HasHistory ? "Everything else you've changed in it (the whole level)" : "Everything else, from before OpenCAGE kept a history of edits (the whole level)");
                rest.ToolTipText = "Ticked, the whole level goes in as it is now - every change in it, listed or not: " + count + "\n" + string.Join("\n", level.Files.Select(o => ShortName(o) + "  (" + Describe(o) + ")"));
                rest.Tag = new RestRow() { Level = level };
                _restNodes[level] = rest;
                levelNode.Expand();
            }
            levelsRoot.Expand();
        }

        private void AddPart(TreeNode node, object row, LevelChanges level)
        {
            node.Tag = row;
            if (!_partNodes.TryGetValue(level, out List<TreeNode> nodes))
                _partNodes[level] = nodes = new List<TreeNode>();
            nodes.Add(node);
        }

        private static string LevelTitle(LevelChanges level) => ModToolkit.LevelName(level.Level);

        private static string CompositeLeaf(string path)
        {
            string name = path.Replace('/', '\\').TrimEnd('\\');
            int slash = name.LastIndexOf('\\');
            return slash < 0 ? name : name.Substring(slash + 1) + " (" + name.Substring(0, slash) + ")";
        }

        private static string AssetTitle(AssetRow asset)
        {
            switch (asset.Kind)
            {
                case CathodeLib.AssetModificationInfoTable.AssetKind.TEXTURE: return "Texture " + asset.Name;
                case CathodeLib.AssetModificationInfoTable.AssetKind.MODEL: return "Model " + asset.Name;
                case CathodeLib.AssetModificationInfoTable.AssetKind.MATERIAL: return "Material " + asset.Name;
                case CathodeLib.AssetModificationInfoTable.AssetKind.MATERIAL_MAPPING: return "Material mapping " + asset.Name;
                case CathodeLib.AssetModificationInfoTable.AssetKind.COLLISION: return "Collision " + asset.Name;
                case CathodeLib.AssetModificationInfoTable.AssetKind.PHYSICS_SYSTEM: return "Physics " + asset.Name;
                default: return Capitalise(asset.Name);
            }
        }

        private static string EditedText(DateTime when)
        {
            return "edited " + when.ToString(when.Year == DateTime.Now.Year ? "d MMM HH:mm" : "d MMM yyyy");
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
        /// What this row contributes to the package: the file, plus the sidecars that ship
        /// with it (which have no row of their own - see PopulateTree).
        /// </summary>
        private long FileSize(string normalisedPath)
        {
            return SizeOnDisk(normalisedPath) + ModExportBuilder.SidecarsFor(normalisedPath).Sum(o => SizeOnDisk(o));
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

        /* A level's file by its place in the level folder */
        private static string ShortName(string normalisedPath)
        {
            string level = ModToolkit.LevelOf(normalisedPath);
            return level == null ? normalisedPath : normalisedPath.Substring(ModToolkit.LevelFolder(level).Length);
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

            /* A level's "everything else" is the whole level as it is now: ticked, every row of the level is in with it;
               any row of it taken out takes the whole level out - just what's ticked goes. A change that can't be taken
               out of its level on its own brings the whole level. */
            foreach (TreeNode node in new[] { e.Node }.Concat(AllNodes(e.Node.Nodes)).ToList())
            {
                bool wholeOnly = node.Tag is AssetRow asset && asset.WholeOnly;
                if ((node.Tag is RestRow || wholeOnly) && node.Checked)
                    SetLevel(LevelOfPart(node.Tag) ?? ((RestRow)node.Tag).Level, true);
                else if (!node.Checked && (LevelOfPart(node.Tag) ?? (node.Tag as RestRow)?.Level) is LevelChanges level)
                    NotWhole(level);
            }
            _cascading = false;
            UpdateSummary();
        }

        private static LevelChanges LevelOfPart(object tag)
        {
            switch (tag)
            {
                case CompositeRow composite: return composite.Level;
                case AssetRow asset: return asset.Level;
                case LooseRow loose: return loose.Level;
                default: return null;
            }
        }

        /* The level no longer goes whole: its "everything else" unticked, and with it what can only go with the whole level */
        private void NotWhole(LevelChanges level)
        {
            if (_restNodes.TryGetValue(level, out TreeNode rest))
                Check(rest, false);
            if (_partNodes.TryGetValue(level, out List<TreeNode> parts))
                foreach (TreeNode part in parts)
                    if (part.Tag is AssetRow asset && asset.WholeOnly)
                        Check(part, false);
        }

        /* Tick (or untick) a level's every row, its "everything else" included */
        private void SetLevel(LevelChanges level, bool value)
        {
            if (_restNodes.TryGetValue(level, out TreeNode rest))
                Check(rest, value);
            if (_partNodes.TryGetValue(level, out List<TreeNode> parts))
                foreach (TreeNode part in parts)
                    Check(part, value);
        }

        private static void Check(TreeNode node, bool value)
        {
            node.Checked = value;
            if (value)
                for (TreeNode parent = node.Parent; parent != null; parent = parent.Parent)
                    if (!parent.Checked)
                        parent.Checked = true;
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
            Dictionary<LevelChanges, LevelPart> parts = SelectedParts();
            //What's ticked, counted the way the list shows it
            int levelCount = AllNodes(_tree.Nodes).Count(o => o.Checked && o.Tag is RestRow);
            int compositeCount = parts.Values.Sum(o => o.Composites.Count);
            int assetCount = parts.Values.Sum(o => o.Textures.Count + o.Models.Count + o.Materials.Count + o.StandaloneData.Count + o.LooseFiles.Count);
            int settingCount = configs.Where(o => o.Key.Path != TreeDirectory).Sum(o => o.Value.Count);
            int treeCount = AllNodes(_tree.Nodes).Count(o => o.Checked && o.Tag is OpGroup);
            int otherCount = AllNodes(_tree.Nodes).Count(o => o.Checked && o.Tag is string);
            List<string> counts = new List<string>();
            if (compositeCount != 0) counts.Add(compositeCount + " composite" + (compositeCount == 1 ? "" : "s"));
            if (assetCount != 0) counts.Add(assetCount + " other level change" + (assetCount == 1 ? "" : "s"));
            if (levelCount != 0) counts.Add(levelCount + " whole level" + (levelCount == 1 ? "" : "s"));
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
                    //Part of a level is said as a change to the level, as the whole of it is
                    foreach (LevelChanges level in parts.Keys)
                        preview.AddFiles(level.Files.Where(o => o.EndsWith("/WORLD/COMMANDS.PAK")));
                    foreach (KeyValuePair<ConfigChange, List<BmlPatchOp>> config in configs)
                        preview.AddConfigPatch(config.Key.Path, config.Value, config.Key.VanillaBytes);
                    //Said by OpenCAGE, not written by the author, even with the box focused
                    _summarising = true;
                    try { _changes.Text = preview.Summarise(); }
                    finally { _summarising = false; }

                    //Whether an animation PAK changes more than its tree layouts takes reading it, which takes seconds: said once that's done
                    if (_examiningAnimations == null && files.Any(ModExportBuilder.IsAnimationPak))
                    {
                        _examiningAnimations = new System.Threading.Thread(() =>
                        {
                            try { preview.ExamineAnimationPaks(); } catch { }
                            try { if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)UpdateSummary); } catch { }
                        }) { IsBackground = true, Name = "Animation PAK changes" };
                        _examiningAnimations.Start();
                    }
                }
                catch { }
            }
        }
        #endregion

        #region SELECTION
        /* The files that go in as they are: the other files ticked, and every file of each level going in whole */
        private List<string> SelectedFiles()
        {
            List<string> files = new List<string>();
            foreach (TreeNode node in AllNodes(_tree.Nodes))
            {
                if (!node.Checked) continue;
                if (node.Tag is string file) files.Add(file);
                else if (node.Tag is RestRow rest) files.AddRange(rest.Level.Files);
            }
            return files.Distinct().ToList();
        }

        /* Each level only part of which goes in (its "everything else" unticked), and that part, from its ticked rows */
        private Dictionary<LevelChanges, LevelPart> SelectedParts()
        {
            Dictionary<LevelChanges, LevelPart> parts = new Dictionary<LevelChanges, LevelPart>();
            foreach (TreeNode node in AllNodes(_tree.Nodes))
            {
                LevelChanges level = LevelOfPart(node.Tag);
                if (!node.Checked || level == null || (_restNodes.TryGetValue(level, out TreeNode rest) && rest.Checked))
                    continue;
                if (!parts.TryGetValue(level, out LevelPart part))
                    parts[level] = part = new LevelPart() { Build = level.Built };
                switch (node.Tag)
                {
                    case CompositeRow composite:
                        part.Composites.Add(composite.Id);
                        break;
                    case LooseRow loose:
                        part.LooseFiles.Add(loose.Path);
                        break;
                    case AssetRow asset:
                        switch (asset.Kind)
                        {
                            case CathodeLib.AssetModificationInfoTable.AssetKind.TEXTURE: part.Textures.Add(asset.Name); break;
                            case CathodeLib.AssetModificationInfoTable.AssetKind.MODEL: part.Models.Add(asset.Name); break;
                            case CathodeLib.AssetModificationInfoTable.AssetKind.MATERIAL: part.Materials.Add(asset.Name); break;
                            case CathodeLib.AssetModificationInfoTable.AssetKind.CHARACTER_ACCESSORIES:
                            case CathodeLib.AssetModificationInfoTable.AssetKind.GALAXY: part.StandaloneData.Add(asset.Name); break;
                            //(The rest only go with the whole level: ticked, so is its "everything else")
                        }
                        break;
                }
            }
            return parts;
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
            Dictionary<LevelChanges, LevelPart> parts = SelectedParts();
            if (files.Count == 0 && configs.Count == 0 && parts.Count == 0)
            {
                MessageBox.Show("Tick the changes to include first.", "Nothing selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_name.Text.Trim().Length == 0)
            {
                MessageBox.Show("Give the mod a name.", "No name", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            //Work someone else's mod would be baked in? Say so before it ships. The sidecars that go with the files count too.
            List<string> overlaps = new List<string>();
            List<string> shipped = files.SelectMany(o => new[] { o }.Concat(ModExportBuilder.SidecarsFor(o))).Distinct().ToList();
            foreach (ModState.InstalledMod mod in ModServices.State.Mods.Where(o => o.Enabled))
                foreach (string path in shipped)
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
            List<string> notes = new List<string>();
            using (BusyDialog busy = new BusyDialog("Building the package..."))
            {
                System.Threading.Thread examining = _examiningAnimations;
                busy.Work = report =>
                {
                    //Each level only part of which goes in is built as that part, in a folder of its own
                    List<string> partFolders = new List<string>();
                    try
                    {
                        //What an animation PAK changes is being worked out already: the package's summary uses that rather than doing it again
                        examining?.Join();
                        ModExportBuilder builder = new ModExportBuilder(ModServices.GameRoot, ModServices.Manifest, ModServices.Cache, ModServices.Installer);
                        if (_updating != null)
                            builder.Info.Id = _updating.Id;
                        builder.Info.Name = _name.Text.Trim();
                        builder.Info.Author = _author.Text.Trim();
                        builder.Info.Version = _version.Text.Trim();
                        builder.Info.Description = _description.Text;
                        //Left to the package to say unless the author wrote it: it can say for sure what an animation PAK changes
                        builder.Info.Summary = _changesEdited ? _changes.Text.Trim() : "";
                        builder.Info.OpenCageVersion = Singleton.Version;
                        builder.Preview = _preview;
                        builder.AddFiles(files);
                        ModLevelMerger merger = ModServices.Installer.LevelMerger as ModLevelMerger ?? new ModLevelMerger();
                        foreach (KeyValuePair<LevelChanges, LevelPart> part in parts.OrderBy(o => o.Key.Level))
                        {
                            string level = part.Key.Level;
                            string folder = merger.Extract(ModServices.GameRoot, level, root => ModServices.Installer.WriteOriginalLevel(level, root), part.Value, report, notes);
                            if (folder == null)
                                continue;
                            partFolders.Add(folder);
                            builder.AddLevelPart(folder, level);
                        }
                        if (files.Count == 0 && configs.Count == 0 && partFolders.Count == 0)
                            throw new Exception("nothing you ticked differs from the game as it shipped, so there's nothing to put in a mod.");
                        foreach (KeyValuePair<ConfigChange, List<BmlPatchOp>> config in configs)
                            builder.AddConfigPatch(config.Key.Path, config.Value, config.Key.VanillaBytes);
                        report("Writing the package...");
                        result = builder.Write(output);
                        result.Warnings.InsertRange(0, notes);
                    }
                    catch (Exception e) { error = e; }
                    finally
                    {
                        foreach (string folder in partFolders)
                            try { Directory.Delete(folder, true); } catch { }
                    }
                };
                busy.ShowDialog(this);
            }

            if (error != null)
            {
                MessageBox.Show("Export failed: " + error.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Steam.CountTowards(Steam.Achievements.TEN_MODS_CREATED);

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

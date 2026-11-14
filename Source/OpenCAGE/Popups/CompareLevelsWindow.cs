using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.Backups;
using OpenCAGE.LevelDiff;
using OpenCAGE.MCP;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE.Popups
{
    /// <summary>
    /// File > Compare Levels: compares two copies of a level (as saved or now, a backup, another level, any level folder) beyond the
    /// script - models, materials, placements, lights, collision, navigation, sound, configs and more. The compare runs in the background.
    /// </summary>
    public class CompareLevelsWindow : BaseWindow
    {
        private static CompareLevelsWindow _open;

        private readonly ComboBox _before, _after;
        private readonly CheckedListBox _categories;
        private readonly Button _compare, _copy, _save, _goTo;
        private readonly TreeView _tree;
        private readonly TextBox _details;
        private readonly Label _status;
        private LevelDiffReport _report;
        private CancellationTokenSource _running;

        private sealed class SideChoice
        {
            public string Text, Spec;
            public override string ToString() => Text;
        }
        private const string BrowseSpec = "<browse>";

        public CompareLevelsWindow() : base(WindowClosesOn.NONE)
        {
            Text = "Compare Levels";
            Icon = SharedFormIcon.Icon;
            Size = new Size(1100, 720);
            MinimumSize = new Size(760, 480);
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/compare-levels/");

            Panel top = new Panel() { Dock = DockStyle.Top, Height = 104, Padding = new Padding(8) };
            Label beforeLabel = new Label() { Text = "Before:", AutoSize = true, Location = new Point(8, 13) };
            _before = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(64, 9), Width = 430, Anchor = AnchorStyles.Left | AnchorStyles.Top };
            Label afterLabel = new Label() { Text = "After:", AutoSize = true, Location = new Point(8, 43) };
            _after = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(64, 39), Width = 430, Anchor = AnchorStyles.Left | AnchorStyles.Top };
            foreach (ComboBox box in new[] { _before, _after }) box.SelectedIndexChanged += SideChanged;
            _categories = new CheckedListBox() { Location = new Point(510, 8), Size = new Size(380, 88), CheckOnClick = true, MultiColumn = true, ColumnWidth = 125, Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right };
            foreach ((string key, string title) in LevelComparer.Categories) _categories.Items.Add(title, true);
            _compare = new Button() { Text = "Compare", AutoSize = true, Location = new Point(64, 70) };
            _compare.Click += (s, e) => RunCompare();
            top.Controls.Add(beforeLabel); top.Controls.Add(_before); top.Controls.Add(afterLabel); top.Controls.Add(_after); top.Controls.Add(_categories); top.Controls.Add(_compare);

            SplitContainer split = new SplitContainer() { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 420 };
            _tree = new TreeView() { Dock = DockStyle.Fill, HideSelection = false, ShowNodeToolTips = true };
            _tree.AfterSelect += (s, e) => ShowSelected();
            _tree.NodeMouseDoubleClick += (s, e) => GoTo();
            split.Panel1.Controls.Add(_tree);
            Panel buttons = new Panel() { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(0, 4, 0, 0) };
            _copy = new Button() { Text = "Copy Report", AutoSize = true, Location = new Point(0, 6), Enabled = false };
            _copy.Click += (s, e) => { if (_report != null) Clipboard.SetText(_report.ToText(9, 5000)); };
            _save = new Button() { Text = "Save Report...", AutoSize = true, Location = new Point(110, 6), Enabled = false };
            _save.Click += (s, e) => SaveReport();
            _goTo = new Button() { Text = "Go To", AutoSize = true, Location = new Point(230, 6), Enabled = false };
            _goTo.Click += (s, e) => GoTo();
            buttons.Controls.Add(_copy); buttons.Controls.Add(_save); buttons.Controls.Add(_goTo);
            _details = new TextBox() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font(FontFamily.GenericMonospace, 9f) };
            split.Panel2.Controls.Add(_details);
            split.Panel2.Controls.Add(buttons);
            split.Panel2.Padding = new Padding(8, 4, 8, 4);

            _status = new Label() { Dock = DockStyle.Bottom, Height = 22, Padding = new Padding(8, 4, 8, 0), Text = "Pick two copies of a level and Compare. Loading each takes a few seconds (big levels longer)." };
            Controls.Add(split);
            Controls.Add(top);
            Controls.Add(_status);
            FillSides();
            FormClosed += (s, e) => _running?.Cancel();
            Theming.ThemeManager.ApplyCurrent(this);
        }

        #region Opening it
        /// <summary>The window, open or brought forward; with sides given, set to them (and compared).</summary>
        public static CompareLevelsWindow Open(string before = null, string after = null, bool compare = false)
        {
            if (_open == null || _open.IsDisposed)
            {
                _open = new CompareLevelsWindow();
                _open.FormClosed += (s, e) => _open = null;
                if (Singleton.Editor != null) _open.Show(Singleton.Editor); else _open.Show();
            }
            else
            {
                if (_open.WindowState == FormWindowState.Minimized) _open.WindowState = FormWindowState.Normal;
                _open.Activate();
            }
            if (before != null) _open.Select(_open._before, before);
            if (after != null) _open.Select(_open._after, after);
            if (compare) _open.RunCompare();
            return _open;
        }

        /// <summary>
        /// File > Compare Levels... and File > Record Edits..., put in after the Level Check (or <paramref name="after"/>, Save and Build
        /// Level) in its menu: enabled while a level is open. Called once from the editor's constructor.
        /// </summary>
        public static void AttachMenu(ToolStripMenuItem after)
        {
            ToolStripDropDownItem menu = after?.OwnerItem as ToolStripDropDownItem;
            ToolStrip owner = after?.Owner;
            if (owner == null) return;
            ToolStripItem anchor = owner.Items.Cast<ToolStripItem>().FirstOrDefault(o => o.Name == "levelCheckToolStripMenuItem") ?? after;
            ToolStripMenuItem compare = new ToolStripMenuItem("Compare Levels...") { Name = "compareLevelsToolStripMenuItem", ToolTipText = "What differs between two copies of a level: script, models, materials, collision, navigation, sound, files..." };
            compare.Click += (s, e) => Open();
            ToolStripMenuItem record = new ToolStripMenuItem("Record Edits...") { Name = "recordEditsToolStripMenuItem", ToolTipText = "Record your edits as a recipe, and replay a recipe onto another copy of the level or another level" };
            record.Click += (s, e) => RecordEditsWindow.Open();
            int at = owner.Items.IndexOf(anchor) + 1;
            owner.Items.Insert(at, compare);
            owner.Items.Insert(at + 1, record);
            if (menu != null)
                menu.DropDownOpening += (s, e) =>
                {
                    bool open = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands != null;
                    record.Enabled = open;
                    compare.Enabled = true; //two folders on disk need no open level
                    record.Text = Recipes.RecipeRecorder.Recording ? "Record Edits... (recording)" : "Record Edits...";
                };
        }
        #endregion

        #region Sides
        private void FillSides()
        {
            string open = LevelSides.OpenLevelName;
            List<SideChoice> choices = new List<SideChoice>();
            if (open != null)
            {
                choices.Add(new SideChoice() { Text = open + " as saved", Spec = "saved" });
                choices.Add(new SideChoice() { Text = open + " now (unsaved edits; script and assets only)", Spec = "open" });
                try
                {
                    AlienLevel archive = new AlienLevel(open);
                    foreach (AlienLevel.AlienBackup backup in archive.Backups.AsEnumerable().Reverse())
                        choices.Add(new SideChoice() { Text = "Backup of " + open + ": " + backup.Name + " (" + AlienLevel.DateUtc(backup).ToLocalTime().ToString("yyyy-MM-dd HH:mm") + ")", Spec = "backup:" + backup.ID });
                }
                catch { }
            }
            try
            {
                foreach (string level in Level.GetLevels(Singleton.PathToAI).OrderBy(o => o, StringComparer.OrdinalIgnoreCase))
                    if (!string.Equals(level, open, StringComparison.OrdinalIgnoreCase))
                        choices.Add(new SideChoice() { Text = "Level: " + level, Spec = "level:" + level });
            }
            catch { }
            choices.Add(new SideChoice() { Text = "A level folder (another install or a copy)...", Spec = BrowseSpec });
            foreach (ComboBox box in new[] { _before, _after })
            {
                box.Items.Clear();
                box.Items.AddRange(choices.ToArray());
            }
            //Default: the newest backup (else another choice) before, this level as saved after
            int backupIndex = choices.FindIndex(o => o.Spec.StartsWith("backup:"));
            if (choices.Count > 1) _before.SelectedIndex = backupIndex >= 0 ? backupIndex : Math.Min(2, choices.Count - 1);
            if (choices.Count > 0) _after.SelectedIndex = 0;
        }

        private void Select(ComboBox box, string spec)
        {
            for (int i = 0; i < box.Items.Count; i++)
                if (string.Equals(((SideChoice)box.Items[i]).Spec, spec, StringComparison.OrdinalIgnoreCase)) { box.SelectedIndex = i; return; }
            SideChoice extra = new SideChoice() { Text = spec, Spec = spec };
            box.Items.Insert(box.Items.Count - 1, extra);
            box.SelectedItem = extra;
        }

        private void SideChanged(object sender, EventArgs e)
        {
            ComboBox box = (ComboBox)sender;
            if (!(box.SelectedItem is SideChoice choice) || choice.Spec != BrowseSpec) return;
            using (FolderBrowserDialog dialog = new FolderBrowserDialog() { Description = "A level's folder (inside a game's DATA\\ENV, e.g. ...\\DATA\\ENV\\PRODUCTION\\BSP_TORRENS)", ShowNewFolderButton = false })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK && Directory.Exists(dialog.SelectedPath))
                {
                    SideChoice folder = new SideChoice() { Text = dialog.SelectedPath, Spec = dialog.SelectedPath };
                    box.Items.Insert(box.Items.Count - 1, folder);
                    box.SelectedItem = folder;
                }
                else box.SelectedIndex = 0;
            }
        }
        #endregion

        #region Comparing
        private void RunCompare()
        {
            if (_running != null) { _running.Cancel(); return; }
            SideChoice before = _before.SelectedItem as SideChoice, after = _after.SelectedItem as SideChoice;
            if (before == null || after == null || before.Spec == BrowseSpec || after.Spec == BrowseSpec) { _status.Text = "Pick both sides first."; return; }
            if (before.Spec == after.Spec) { _status.Text = "Both sides are the same."; return; }
            HashSet<string> only = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _categories.Items.Count; i++) if (_categories.GetItemChecked(i)) only.Add(LevelComparer.Categories[i].Key);
            if (only.Count == 0) { _status.Text = "Tick at least one category."; return; }

            LevelSide a, b;
            try
            {
                a = LevelSides.Parse(before.Spec);
                b = LevelSides.Parse(after.Spec);
            }
            catch (Exception ex) { _status.Text = ex.Message; return; }
            CompareOptions options = new CompareOptions() { Only = only };
            //The open level is read here, on the UI thread, before the rest goes to the background
            try
            {
                Cursor = Cursors.WaitCursor;
                foreach (LevelSide side in new[] { a, b })
                    if (side.Live != null) side.LiveData = LevelComparer.ReadLive(side.Live, new CompareOptions() { Only = only, Global = Singleton.Global }, side.Label);
            }
            finally { Cursor = Cursors.Default; }

            _running = new CancellationTokenSource();
            options.Cancel = _running.Token;
            options.Progress = text => BeginInvoke(new Action(() => _status.Text = text + "..."));
            _compare.Text = "Cancel";
            _tree.Nodes.Clear();
            _details.Text = "";
            Task.Run(() => LevelSides.Compare(a, b, options, read => read())).ContinueWith(t => BeginInvoke(new Action(() =>
            {
                _running = null;
                _compare.Text = "Compare";
                if (IsDisposed) return;
                if (t.IsCanceled || (t.Exception?.InnerException is OperationCanceledException)) { _status.Text = "Cancelled."; return; }
                if (t.Exception != null) { _status.Text = "Could not compare: " + (t.Exception.InnerException ?? t.Exception).Message; return; }
                ShowReport(t.Result);
            })));
        }

        private void ShowReport(LevelDiffReport report)
        {
            _report = report;
            _tree.BeginUpdate();
            _tree.Nodes.Clear();
            foreach (DiffCategory c in report.Categories)
            {
                TreeNode cat = new TreeNode(c.Title + (c.EditorOnly ? " (editor only)" : "") + " - " + c.Line()) { Tag = c };
                foreach (string note in c.Notes) cat.Nodes.Add(new TreeNode("note: " + note) { ForeColor = SystemColors.GrayText });
                foreach (DiffNode item in c.Items.Take(5000)) cat.Nodes.Add(NodeOf(item, 0));
                if (c.Items.Count > 5000) cat.Nodes.Add("... and " + (c.Items.Count - 5000) + " more (Save Report has them all)");
                _tree.Nodes.Add(cat);
                if (c.HasDifferences && !c.EditorOnly && c.Items.Count <= 50) cat.Expand();
            }
            _tree.EndUpdate();
            _copy.Enabled = _save.Enabled = true;
            string files = report.FilesDiffering == 0 ? "" : " " + report.FilesDiffering + " files differ byte for byte (see Files: a save rewrites some with no change in what they hold).";
            _status.Text = report.CategoriesDiffering == 0 ? "No differences in game data (" + report.Took.TotalSeconds.ToString("0") + " s)." + files :
                report.CategoriesDiffering + " categories differ (" + report.Took.TotalSeconds.ToString("0") + " s). Double-click a script item of the open level to go to it.";
            if (report.Notes.Count != 0) _status.Text += " " + string.Join(" ", report.Notes);
        }

        private static TreeNode NodeOf(DiffNode item, int depth)
        {
            TreeNode node = new TreeNode(DiffNode.Mark(item.Kind) + " " + item.Text) { Tag = item, ToolTipText = item.Ref };
            if (depth < 6)
                foreach (DiffNode child in item.Children.Take(2000)) node.Nodes.Add(NodeOf(child, depth + 1));
            return node;
        }

        private void ShowSelected()
        {
            TreeNode node = _tree.SelectedNode;
            List<string> lines = new List<string>();
            void Add(TreeNode n, int indent)
            {
                lines.Add(new string(' ', indent * 2) + n.Text);
                if (indent < 8) foreach (TreeNode c in n.Nodes) Add(c, indent + 1);
            }
            if (node != null)
            {
                Add(node, 0);
                if (node.Tag is DiffNode d && d.Ref != null) lines.Insert(1, "(" + d.Ref + ")");
            }
            _details.Text = string.Join(Environment.NewLine, lines.Take(3000));
            _goTo.Enabled = node?.Tag is DiffNode n2 && TargetOf(n2) != null;
        }

        private void SaveReport()
        {
            if (_report == null) return;
            using (SaveFileDialog dialog = new SaveFileDialog() { Filter = "Text report (*.txt)|*.txt|JSON report (*.json)|*.json", FileName = "compare_levels_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".txt" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (dialog.FilterIndex == 2 || dialog.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    File.WriteAllText(dialog.FileName, _report.ToJson(null, 10, int.MaxValue, int.MaxValue).ToString(Newtonsoft.Json.Formatting.Indented));
                else File.WriteAllText(dialog.FileName, _report.ToText(10, int.MaxValue));
                _status.Text = "Saved " + dialog.FileName;
            }
        }

        /// <summary>The open level's composite (and entity) a script item names, if it is in the open level.</summary>
        private static (Composite composite, Entity entity)? TargetOf(DiffNode node)
        {
            string r = node?.Ref;
            Level level = LevelSides.OpenLevel;
            if (r == null || level?.Commands == null) return null;
            try
            {
                if (r.StartsWith("entity:"))
                {
                    string[] ids = r.Substring(7).Split('/');
                    Composite c = level.Commands.GetComposite(new ShortGuid(ids[0]));
                    Entity e = c?.GetEntityByID(new ShortGuid(ids[1]));
                    if (c != null) return (c, e);
                }
                else if (r.StartsWith("composite:"))
                {
                    Composite c = level.Commands.GetComposite(new ShortGuid(r.Substring(10)));
                    if (c != null) return (c, null);
                }
            }
            catch { }
            return null;
        }

        private void GoTo()
        {
            if (!(_tree.SelectedNode?.Tag is DiffNode node)) return;
            (Composite composite, Entity entity)? target = TargetOf(node);
            if (target == null) return;
            try { McpScriptEdit.Show(target.Value.composite, target.Value.entity == null ? new List<Entity>() : new List<Entity>() { target.Value.entity }); }
            catch (Exception e) { _status.Text = "Could not go there: " + e.Message; }
        }
        #endregion
    }
}

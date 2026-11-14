using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.MCP;
using OpenCAGE.Popups.Base;
using OpenCAGE.Weapons;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Vector3 = System.Numerics.Vector3;

namespace OpenCAGE.Popups
{
    /// <summary>
    /// Replace Weapon Model: put a model file on a weapon or held item, choosing what happens to each part and lining it up against the
    /// retail model. Everything goes through the replace_weapon_model MCP tool (Scripts\Weapons\WeaponModelReplace.cs).
    /// </summary>
    public class ReplaceWeaponModel : BaseWindow
    {
        private static ReplaceWeaponModel _open;

        private readonly ComboBox _weapon = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
        private readonly TextBox _path = new TextBox() { Width = 420 };
        private readonly Button _browse = new Button() { Text = "Browse...", AutoSize = true };
        private readonly Button _reference = new Button() { Text = "Export Reference...", AutoSize = true };
        private readonly NumericUpDown _ox = Number(-2, 2, 4, 0.005m), _oy = Number(-2, 2, 4, 0.005m), _oz = Number(-2, 2, 4, 0.005m);
        private readonly NumericUpDown _rx = Number(-360, 360, 2, 1), _ry = Number(-360, 360, 2, 1), _rz = Number(-360, 360, 2, 1);
        private readonly NumericUpDown _scale = Number(0.0001m, 1000, 4, 0.01m);
        private readonly DataGridView _parts = new DataGridView()
        {
            Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, EditMode = DataGridViewEditMode.EditOnEnter,
        };
        private readonly CheckedListBox _targets = new CheckedListBox() { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false };
        private readonly CheckedListBox _npcTypes = new CheckedListBox() { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false };
        private readonly ComboBox _muzzle = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
        private readonly ComboBox _physics = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
        private readonly TextBox _prefix = new TextBox() { Width = 200 };
        private readonly TextBox _suffix = new TextBox() { Width = 90, Text = "CUSTOM" };
        private readonly GhostView _view = new GhostView() { Dock = DockStyle.Fill };
        private readonly SplitContainer _middle = new SplitContainer() { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };
        private readonly TextBox _output = new TextBox() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font(FontFamily.GenericMonospace, 8.5f) };
        private readonly Button _check = new Button() { Text = "Check Plan", AutoSize = true };
        private readonly Button _apply = new Button() { Text = "Replace", AutoSize = true };
        private readonly Button _visibility = new Button() { Text = "Apply Visibility Only", AutoSize = true };
        private readonly Button _close = new Button() { Text = "Close", AutoSize = true };
        private readonly Label _status = new Label() { AutoSize = true };

        private WeaponInfo _info;
        private WeaponModelSource _source;
        private string _sourceKey;
        private bool _loading, _busy;

        public static void ShowFor(string weapon)
        {
            if (_open == null || _open.IsDisposed) { _open = new ReplaceWeaponModel(); _open.Show(Singleton.Editor); }
            else _open.Activate();
            if (!string.IsNullOrEmpty(weapon)) _open.SelectWeapon(weapon);
        }

        /// <summary>A menu entry after <paramref name="after"/> (the Model Editor's): CommandsEditor attaches it once at start, as the Level Check's.</summary>
        public static void AttachMenu(ToolStripMenuItem after)
        {
            ToolStripDropDownItem menu = after?.OwnerItem as ToolStripDropDownItem;
            ToolStrip owner = after?.Owner;
            if (owner == null) return;
            ToolStripMenuItem item = new ToolStripMenuItem("Replace Weapon Model...") { Name = "replaceWeaponModelToolStripMenuItem", ToolTipText = "Put a model of your own on a weapon or held item: first person, third person and pickups, keeping its animation" };
            item.Click += (s, e) => ShowFor(null);
            owner.Items.Insert(owner.Items.IndexOf(after) + 1, item);
            if (menu != null)
                menu.DropDownOpening += (s, e) => item.Enabled = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands != null;
        }
        /// <summary>Right-clicking a weapon's composite in the composite browser offers the window (the browser calls this before showing its menu).</summary>
        public static void PrepareMenu(ContextMenuStrip menu, Composite composite)
        {
            if (menu == null) return;
            ToolStripMenuItem item = menu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(o => (o.Name ?? "") == "replaceWeaponModelMenuItem");
            if (item == null)
            {
                item = new ToolStripMenuItem("Replace Weapon Model...") { Name = "replaceWeaponModelMenuItem" };
                item.Click += (o, e) => ShowFor(item.Tag as string);
                menu.Items.Add(item);
            }
            string key = null;
            try
            {
                Commands commands = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;
                if (commands != null && composite?.name != null && composite.name.StartsWith(WeaponModelReplace.WeaponsFolder, StringComparison.OrdinalIgnoreCase))
                    key = WeaponModelReplace.List(commands).FirstOrDefault(o => o.Item2 == composite || o.Item3 == composite)?.Item1;
            }
            catch { key = null; }
            item.Visible = key != null;
            item.Tag = key;
        }

        public ReplaceWeaponModel() : base(WindowClosesOn.COMMANDS_RELOAD)
        {
            Text = "Replace Weapon Model";
            Icon = SharedFormIcon.Icon;
            Size = new Size(1280, 860);
            MinimumSize = new Size(900, 600);
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            BuildLayout();
            _muzzle.Items.AddRange(new object[] { "auto", "keep" });
            _muzzle.SelectedIndex = 0;
            _physics.Items.AddRange(new object[] { "auto", "hull", "keep" });
            _physics.SelectedIndex = 0;
            _scale.Value = 1;
            FillWeapons();
            _weapon.SelectedIndexChanged += (o, e) => { if (!_loading) LoadWeapon(); };
            _browse.Click += (o, e) => Browse();
            _reference.Click += (o, e) => ExportReference();
            _path.Leave += (o, e) => LoadSource();
            _path.KeyDown += (o, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; LoadSource(); } };
            foreach (NumericUpDown n in new[] { _ox, _oy, _oz, _rx, _ry, _rz }) n.ValueChanged += (o, e) => RefreshGhost();
            _scale.ValueChanged += (o, e) => LoadSource();
            _check.Click += async (o, e) => await RunTool(true);
            _apply.Click += async (o, e) => await RunTool(false);
            _visibility.Click += async (o, e) => await RunVisibility();
            _close.Click += (o, e) => Close();
            KeyDown += (o, e) => { if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; Close(); } };
            FormClosed += (o, e) => { if (ReferenceEquals(_open, this)) _open = null; };
            //The split can only be placed once the window has its size (a SplitContainer refuses a distance wider than itself)
            Load += (o, e) => { try { _middle.SplitterDistance = Math.Max(200, Math.Min(_middle.Width - 260, (int)(_middle.Width * 0.55))); } catch (Exception) { } };
            //The list was filled before its handler was attached: load the weapon it shows
            if (_weapon.SelectedItem != null) LoadWeapon();
            BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/replace-weapon-model/");
            Theming.ThemeManager.ApplyToForm(this);
        }

        private static NumericUpDown Number(decimal min, decimal max, int decimals, decimal step) => new NumericUpDown()
        {
            Minimum = min, Maximum = max, DecimalPlaces = decimals, Increment = step, Width = 72,
        };

        private static Label Caption(string text) => new Label() { Text = text, AutoSize = true, Margin = new Padding(6, 7, 2, 0) };

        private void BuildLayout()
        {
            TableLayoutPanel root = new TableLayoutPanel() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(6) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            FlowLayoutPanel top = new FlowLayoutPanel() { AutoSize = true, WrapContents = false, Dock = DockStyle.Fill };
            top.Controls.AddRange(new Control[] { Caption("Weapon"), _weapon, Caption("Model (in weapon space)"), _path, _browse, _reference });
            root.Controls.Add(top, 0, 0);

            FlowLayoutPanel fit = new FlowLayoutPanel() { AutoSize = true, WrapContents = true, Dock = DockStyle.Fill };
            fit.Controls.AddRange(new Control[] { Caption("Offset (m)  X"), _ox, Caption("Y"), _oy, Caption("Z"), _oz, Caption("Rotation  X"), _rx, Caption("Y"), _ry, Caption("Z"), _rz, Caption("Scale"), _scale,
                Caption("Muzzle"), _muzzle, Caption("Physics"), _physics, Caption("Prefix"), _prefix });
            root.Controls.Add(fit, 0, 1);

            SplitContainer middle = _middle;
            TableLayoutPanel left = new TableLayoutPanel() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
            left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
            left.Controls.Add(_parts, 0, 0);
            left.SetColumnSpan(_parts, 2);
            left.Controls.Add(new Label() { Text = "Change these composites", AutoSize = true }, 0, 1);
            FlowLayoutPanel npcCaption = new FlowLayoutPanel() { AutoSize = true, WrapContents = false };
            npcCaption.Controls.AddRange(new Control[] { new Label() { Text = "Third person only for (none = every NPC); copies named", AutoSize = true, Margin = new Padding(0, 4, 2, 0) }, _suffix });
            left.Controls.Add(npcCaption, 1, 1);
            left.Controls.Add(_targets, 0, 2);
            left.Controls.Add(_npcTypes, 1, 2);
            middle.Panel1.Controls.Add(left);
            middle.Panel2.Controls.Add(_view);
            root.Controls.Add(middle, 0, 2);
            root.Controls.Add(_output, 0, 3);

            FlowLayoutPanel buttons = new FlowLayoutPanel() { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
            buttons.Controls.AddRange(new Control[] { _check, _apply, _visibility, _close, _status });
            root.Controls.Add(buttons, 0, 4);
            Controls.Add(root);

            _parts.Columns.Add(new DataGridViewTextBoxColumn() { Name = "part", HeaderText = "Part", ReadOnly = true, FillWeight = 18 });
            _parts.Columns.Add(new DataGridViewTextBoxColumn() { Name = "bone", HeaderText = "Bone", ReadOnly = true, FillWeight = 14 });
            _parts.Columns.Add(new DataGridViewTextBoxColumn() { Name = "moved", HeaderText = "Moved by (clips)", ReadOnly = true, FillWeight = 26 });
            _parts.Columns.Add(new DataGridViewTextBoxColumn() { Name = "script", HeaderText = "Script", ReadOnly = true, FillWeight = 12 });
            _parts.Columns.Add(new DataGridViewTextBoxColumn() { Name = "shown", HeaderText = "Shown now", ReadOnly = true, FillWeight = 10 });
            DataGridViewComboBoxColumn action = new DataGridViewComboBoxColumn() { Name = "action", HeaderText = "Action", FillWeight = 12, FlatStyle = FlatStyle.Flat };
            action.Items.AddRange("auto", "replace", "keep", "hide", "show");
            _parts.Columns.Add(action);
            _parts.Columns.Add(new DataGridViewTextBoxColumn() { Name = "meshes", HeaderText = "Meshes (replace)", FillWeight = 18 });
            _parts.CellValueChanged += (o, e) => { if (!_loading) RefreshGhost(); };
            _parts.CurrentCellDirtyStateChanged += (o, e) => { if (_parts.IsCurrentCellDirty) _parts.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            _parts.DataError += (o, e) => { e.ThrowException = false; };
            //The selected part shows in blue in the views: which piece of the gun each bone carries
            _parts.SelectionChanged += (o, e) => { _view.Highlight = (_parts.CurrentRow?.Tag as WeaponPart) == null ? null : WeaponModelReplace.RestCorners((WeaponPart)_parts.CurrentRow.Tag); _view.Invalidate(); };
        }

        private Level Level => Singleton.Editor?.CompositeBrowser?.Content?.Level;

        private void FillWeapons()
        {
            _loading = true;
            _weapon.Items.Clear();
            if (Level?.Commands != null)
                foreach (Tuple<string, Composite, Composite> weapon in WeaponModelReplace.List(Level.Commands)) _weapon.Items.Add(weapon.Item1);
            _loading = false;
            if (_weapon.Items.Count != 0) { _weapon.SelectedIndex = Math.Max(0, _weapon.Items.IndexOf("Shotgun")); }
            else Say("This level has no weapons under " + WeaponModelReplace.WeaponsFolder + ".", true);
        }

        private void SelectWeapon(string weapon)
        {
            int index = _weapon.Items.Cast<string>().ToList().FindIndex(o => string.Equals(o, weapon, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) _weapon.SelectedIndex = index;
        }

        /// <summary>Fill the window as a user would and press Check Plan (an offline check of the window); the output it shows.</summary>
        internal async Task<string> FillAndCheck(string weapon, string path, Vector3 offset, Dictionary<string, string> actions)
        {
            SelectWeapon(weapon);
            _path.Text = path;
            LoadSource();
            _ox.Value = (decimal)offset.X; _oy.Value = (decimal)offset.Y; _oz.Value = (decimal)offset.Z;
            foreach (DataGridViewRow row in _parts.Rows)
                if (actions != null && actions.TryGetValue(((WeaponPart)row.Tag).Name, out string action)) row.Cells["action"].Value = action;
            await RunTool(true);
            return _status.Text + Environment.NewLine + _output.Text;
        }

        private void LoadWeapon()
        {
            if (Level == null || _weapon.SelectedItem == null) return;
            try
            {
                Cursor = Cursors.WaitCursor;
                _info = WeaponModelReplace.Analyse(Level, (string)_weapon.SelectedItem);
            }
            catch (Exception e) { _info = null; Say(e.Message, true); return; }
            finally { Cursor = Cursors.Default; }
            _loading = true;
            _parts.Rows.Clear();
            foreach (WeaponPart part in _info.FirstPerson.Parts.Where(o => o.Lod0.Count != 0))
            {
                int row = _parts.Rows.Add(part.Name, part.Bone >= 0 ? (part.BoneName ?? part.Bone.ToString(CultureInfo.InvariantCulture)) : "-",
                    part.MovedBy.Count == 0 ? (part.Bone >= 0 ? "(none)" : "-") : string.Join(", ", part.MovedBy) + (part.Travel > 0 ? "  [" + WeaponModelReplace.Cm(part.Travel) + "]" : ""),
                    part.Scripted.Count == 0 ? "" : "shown/hidden", part.ShowOnReset ? "yes" : "HIDDEN", "auto", "");
                if (!part.ShowOnReset) _parts.Rows[row].Cells["shown"].ToolTipText = "show_on_reset is off: the part is hidden (an earlier replace, or retail). Set Action to show and press Apply Visibility Only to bring it back - nothing is imported.";
                _parts.Rows[row].Tag = part;
                if (part.Scripted.Count != 0) _parts.Rows[row].Cells["script"].ToolTipText = string.Join("\n", part.Scripted);
            }
            _targets.Items.Clear();
            foreach (WeaponComposite target in _info.All)
            {
                string fit = target.Role == "first_person" ? "" : target.Fit.SameFrame ? "  (same frame)" : "  (fit " + WeaponModelReplace.Mm(target.Fit.Residual) + ")";
                _targets.Items.Add(new TargetItem(target, target.Role.Replace('_', ' ') + ":  " + target.Path + fit), target.Role == "first_person" || target.Selected);
            }
            _npcTypes.Items.Clear();
            foreach (IGrouping<Composite, WeaponNpcType> type in _info.NpcTypes.GroupBy(o => o.Composite))
                _npcTypes.Items.Add(new NpcItem(type.Key.name), false);
            _prefix.Text = "WEAPONS\\" + WeaponModelReplace.Norm(_info.FirstPerson.Path.Split('\\')[2]);
            _loading = false;
            Say(_info.Key + ": " + _info.FirstPerson.Parts.Count(o => o.Lod0.Count != 0) + " parts" + (_info.FirstPerson.RigName != null ? " on " + _info.FirstPerson.RigName : "") + (_info.Notes.Count != 0 ? ". " + _info.Notes[0] : "."), false);
            RefreshGhost();
        }

        private sealed class TargetItem
        {
            public readonly WeaponComposite Target; private readonly string _text;
            public TargetItem(WeaponComposite target, string text) { Target = target; _text = text; }
            public override string ToString() => _text;
        }

        private sealed class NpcItem
        {
            public readonly string Path;
            public NpcItem(string path) { Path = path; }
            public override string ToString() => Path;
        }

        private void Browse()
        {
            using (OpenFileDialog dialog = new OpenFileDialog() { Filter = ModelExport.ModelExporter.ImportFilter(false), Title = "Model in weapon space" })
                if (dialog.ShowDialog(this) == DialogResult.OK) { _path.Text = dialog.FileName; LoadSource(); }
        }

        private void ExportReference()
        {
            if (_info == null || Level == null) return;
            using (SaveFileDialog dialog = new SaveFileDialog() { Filter = ModelExport.ModelExporter.Filter(false), FilterIndex = 3, FileName = WeaponModelReplace.Norm(_info.Key) + "_REFERENCE.gltf", Title = "Export the retail " + _info.Key + " in weapon space" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    JObject written = WeaponModelReplace.ExportReference(Level, _info, dialog.FileName, true, true);
                    Say("Wrote " + written["meshes"] + " reference meshes and " + ((JArray)written["markers"]).Count + " markers to " + dialog.FileName + ". Model over them; meshes named REF_* are left out when the model is read.", false);
                }
                catch (Exception e) { Say(e.Message, true); }
            }
        }

        private void LoadSource()
        {
            string path = _path.Text.Trim();
            string key = path + "|" + _scale.Value.ToString(CultureInfo.InvariantCulture);
            if (key == _sourceKey) return;
            _sourceKey = key;
            _source = null;
            if (path.Length == 0 || !System.IO.File.Exists(path)) { RefreshGhost(); return; }
            try
            {
                Cursor = Cursors.WaitCursor;
                _source = WeaponModelReplace.ReadModel(path, (float)_scale.Value);
                Say(System.IO.Path.GetFileName(path) + ": " + string.Join(", ", _source.Meshes.Select(o => o.Name + (o.Reference ? " (reference, left out)" : " (" + o.Corners.Count / 3 + " tris)"))), false);
            }
            catch (Exception e) { Say(e.Message, true); }
            finally { Cursor = Cursors.Default; }
            RefreshGhost();
        }

        private WeaponOptions Options()
        {
            WeaponOptions options = new WeaponOptions()
            {
                Weapon = (string)_weapon.SelectedItem,
                ModelPath = _path.Text.Trim(),
                Scale = (float)_scale.Value,
                Offset = new Vector3((float)_ox.Value, (float)_oy.Value, (float)_oz.Value),
                Rotation = new Vector3((float)_rx.Value, (float)_ry.Value, (float)_rz.Value),
                Muzzle = (string)_muzzle.SelectedItem,
                Physics = (string)_physics.SelectedItem,
                Prefix = _prefix.Text.Trim(),
                NpcSuffix = _suffix.Text.Trim(),
            };
            foreach (DataGridViewRow row in _parts.Rows)
            {
                WeaponPart part = row.Tag as WeaponPart;
                string action = (row.Cells["action"].Value as string) ?? "auto";
                string meshes = (row.Cells["meshes"].Value as string ?? "").Trim();
                if (action == "keep" || action == "hide" || action == "show") options.PartActions[part.Name] = action;
                else if (meshes.Length != 0) options.PartMeshes[part.Name] = meshes.Split(',').Select(o => o.Trim()).Where(o => o.Length != 0).ToList();
            }
            List<TargetItem> chosen = _targets.CheckedItems.Cast<TargetItem>().ToList();
            if (_info != null && chosen.Count != _info.All.Count(o => o.Role == "first_person" || o.Selected) || chosen.Any(o => !o.Target.Selected && o.Target.Role != "first_person"))
                options.Targets = chosen.Select(o => o.Target.Path).ToList();
            options.NpcTypes = _npcTypes.CheckedItems.Cast<NpcItem>().Select(o => o.Path).ToList();
            return options;
        }

        private JObject Arguments(bool dryRun)
        {
            WeaponOptions o = Options();
            JObject args = new JObject() { ["weapon"] = o.Weapon, ["path"] = o.ModelPath, ["dry_run"] = dryRun, ["muzzle"] = o.Muzzle, ["physics"] = o.Physics };
            if (o.Scale != 1) args["scale"] = o.Scale;
            if (o.Offset != Vector3.Zero) args["offset"] = new JArray(o.Offset.X, o.Offset.Y, o.Offset.Z);
            if (o.Rotation != Vector3.Zero) args["rotation"] = new JArray(o.Rotation.X, o.Rotation.Y, o.Rotation.Z);
            if (!string.IsNullOrEmpty(o.Prefix)) args["prefix"] = o.Prefix;
            JObject parts = new JObject();
            foreach (KeyValuePair<string, string> a in o.PartActions) parts[a.Key] = a.Value;
            foreach (KeyValuePair<string, List<string>> m in o.PartMeshes) parts[m.Key] = new JArray(m.Value);
            if (parts.Count != 0) args["parts"] = parts;
            if (o.Targets != null) args["targets"] = new JArray(o.Targets);
            if (o.NpcTypes.Count != 0) { args["npc_types"] = new JArray(o.NpcTypes); args["npc_suffix"] = o.NpcSuffix; }
            return args;
        }

        /// <summary>Show / hide parts only (no model, nothing imported): the parts whose Action is show or hide. One undo step.</summary>
        private async Task RunVisibility()
        {
            if (_busy || _info == null) return;
            JObject parts = new JObject();
            foreach (DataGridViewRow row in _parts.Rows)
            {
                string action = (row.Cells["action"].Value as string) ?? "auto";
                if (action == "show" || action == "hide") parts[((WeaponPart)row.Tag).Name] = action;
            }
            if (parts.Count == 0) { Say("Set a part's Action to show or hide first.", true); return; }
            await RunArgs(new JObject() { ["weapon"] = (string)_weapon.SelectedItem, ["parts"] = parts }, false, "Visibility changed (one undo step).");
        }

        private async Task RunTool(bool dryRun)
        {
            if (_busy || _info == null) return;
            if (_path.Text.Trim().Length == 0) { Say("Choose a model file first.", true); return; }
            if (!dryRun && MessageBox.Show(this, "Replace the model of " + _info.Key + " in this level?\n\nThe imports cannot be undone (the part changes can). Save & Build afterwards to see it in game.", "Replace Weapon Model", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                return;
            await RunArgs(Arguments(dryRun), dryRun, dryRun ? "Plan checked: nothing was changed." : "Done. Save & Build to take it to the game.");
        }

        private async Task RunArgs(JObject args, bool dryRun, string doneText)
        {
            _busy = true;
            _check.Enabled = _apply.Enabled = _visibility.Enabled = false;
            Say(dryRun ? "Checking the plan..." : "Replacing...", false);
            JObject response;
            try
            {
                McpTool tool = McpTools.Find("replace_weapon_model");
                response = await Task.Run(() => tool.Invoke(new McpCall(tool, args, CancellationToken.None, null)));
            }
            catch (Exception e) { response = new JObject() { ["isError"] = true, ["content"] = new JArray(new JObject() { ["type"] = "text", ["text"] = e.Message }) }; }
            finally { _busy = false; _check.Enabled = _apply.Enabled = _visibility.Enabled = true; }
            bool error = response["isError"] != null && (bool)response["isError"];
            string text = string.Concat(((JArray)response["content"] ?? new JArray()).Where(c => (string)c["type"] == "text").Select(c => (string)c["text"]));
            JObject result = null;
            try { result = JObject.Parse(text); } catch { }
            _output.Text = error || result == null ? text : Summary(result, dryRun);
            Say(error ? "Failed - see below." : doneText, error);
            if (result != null) ShowMuzzle(result);
            if (!dryRun && !error) LoadWeapon();
        }

        /// <summary>The plan or result, readable: parts, markers, clearance, notes; the steps last.</summary>
        private static string Summary(JObject result, bool dryRun)
        {
            StringBuilder s = new StringBuilder();
            s.AppendLine((dryRun ? "PLAN for " : "DONE: ") + result["weapon"] + "  (model " + result["model"] + ")");
            foreach (JObject target in (result["targets"] as JArray ?? new JArray()).OfType<JObject>())
            {
                s.AppendLine();
                s.AppendLine(((string)target["role"]).ToUpperInvariant().Replace('_', ' ') + "  " + target["composite"] + (target["copy_of"] != null ? "  (copy of " + target["copy_of"] + ")" : "") + (target["fit"] != null ? "  [" + target["fit"] + "]" : "") + "  placed " + target["placements"] + "x");
                foreach (JObject part in (target["parts"] as JArray ?? new JArray()).OfType<JObject>())
                    s.AppendLine("  " + ((string)part["entity"]).PadRight(22) + ((string)part["action"]).PadRight(9) + (part["bone"] != null ? ("bone " + part["bone"]).PadRight(22) : "".PadRight(22)) +
                        (part["meshes"] != null ? "meshes " + string.Join(", ", part["meshes"]) + " -> " + part["model"] + "   " : "") + "(" + part["reason"] + ")");
                if (target["physics"] != null) s.AppendLine("  physics: " + target["physics"]);
            }
            if (result["muzzle"] != null) { s.AppendLine(); s.AppendLine("MUZZLE " + result["muzzle"]["from"].ToString(Newtonsoft.Json.Formatting.None) + " -> " + result["muzzle"]["to"].ToString(Newtonsoft.Json.Formatting.None) + " (" + result["muzzle"]["moved_cm"] + " cm)"); }
            foreach (JObject marker in (result["markers"] as JArray ?? new JArray()).OfType<JObject>())
                s.AppendLine("  move " + marker["entity"] + " in " + marker["composite"] + "  " + marker["from"].ToString(Newtonsoft.Json.Formatting.None) + " -> " + marker["to"].ToString(Newtonsoft.Json.Formatting.None) + "  (" + marker["why"] + ")");
            if (result["clearance"] is JArray clearance && clearance.Count != 0)
            {
                s.AppendLine();
                s.AppendLine("CLEARANCE (kept parts during the weapon's clips)");
                foreach (JObject row in clearance.OfType<JObject>()) s.AppendLine("  " + row["part"] + ": " + row["verdict"]);
            }
            if (result["check"] != null)
            {
                s.AppendLine();
                s.AppendLine("CHECK  resource ids kept: " + result["check"]["resource_ids_kept"]);
                foreach (JObject row in (result["check"]["parts"] as JArray ?? new JArray()).OfType<JObject>())
                    s.AppendLine("  " + row["composite"] + "  " + row["entity"] + " draws " + row["draws"] + "  material " + row["material"] + " (priority " + row["priority"] + ", mask " + row["mask"] + ")");
            }
            if (result["notes"] is JArray notes && notes.Count != 0) { s.AppendLine(); s.AppendLine("NOTES"); foreach (JToken note in notes) s.AppendLine("  " + note); }
            JArray steps = result["steps"] as JArray ?? result["done"] as JArray;
            if (steps != null) { s.AppendLine(); s.AppendLine(dryRun ? "STEPS" : "STEPS MADE"); int i = 0; foreach (JObject step in steps.OfType<JObject>()) s.AppendLine("  " + (++i) + ". " + step["step"] + (step["tool"] != null ? "  [" + step["tool"] + "]" : "") + (step["model"] != null ? "  " + step["model"] : "")); }
            return s.ToString().Replace("\n", Environment.NewLine).Replace("\r\r", "\r");
        }

        private void ShowMuzzle(JObject result)
        {
            if (result["muzzle"] == null) { _view.NewMuzzle = null; _view.Invalidate(); return; }
            JArray to = (JArray)result["muzzle"]["to"];
            _view.NewMuzzle = new Vector3((float)to[0], (float)to[1], (float)to[2]);
            _view.Invalidate();
        }

        private void RefreshGhost()
        {
            if (_info == null) return;
            Dictionary<string, string> actions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataGridViewRow row in _parts.Rows) actions[((WeaponPart)row.Tag).Name] = (row.Cells["action"].Value as string) ?? "auto";
            _view.Retail = _info.FirstPerson.Parts.Where(o => o.Lod0.Count != 0).Select(o => Tuple.Create(WeaponModelReplace.RestCorners(o), actions.TryGetValue(o.Name, out string a) && a == "hide")).ToList();
            WeaponOptions options = Options();
            System.Numerics.Matrix4x4 transform = WeaponModelReplace.UserTransform(options);
            _view.Model = _source == null ? null : _source.Usable.SelectMany(o => o.Corners).Select(o => Vector3.Transform(o, transform)).ToList();
            _view.Markers = _info.FirstPerson.Positioned.Where(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.PositionMarker)
                .Select(o => Tuple.Create(((cTransform)o.GetParameter(ShortGuids.position).content).position, MCP.McpScript.EntityName(Level.Commands, _info.FirstPerson.Composite, o))).ToList();
            _view.NewMuzzle = null;
            _view.Invalidate();
        }

        private void Say(string text, bool problem)
        {
            _status.Text = text;
            _status.ForeColor = problem ? (Theming.ThemeManager.IsDark ? Color.FromArgb(255, 120, 110) : Color.Firebrick) : ForeColor;
        }

        /// <summary>The retail first-person weapon (grey; hidden parts faint) and the new model (orange), in three views of weapon space, with the markers.</summary>
        private sealed class GhostView : Control
        {
            public List<Tuple<List<Vector3>, bool>> Retail = new List<Tuple<List<Vector3>, bool>>();
            public List<Vector3> Model;
            public List<Tuple<Vector3, string>> Markers = new List<Tuple<Vector3, string>>();
            public Vector3? NewMuzzle;
            public List<Vector3> Highlight;

            public GhostView()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                bool dark = Theming.ThemeManager.IsDark;
                g.Clear(dark ? Color.FromArgb(30, 30, 32) : Color.FromArgb(244, 244, 246));
                g.SmoothingMode = SmoothingMode.HighSpeed;
                List<Vector3> all = Retail.SelectMany(o => o.Item1).Concat(Model ?? new List<Vector3>()).ToList();
                if (all.Count == 0) { TextRenderer.DrawText(g, "Pick a weapon and a model", Font, ClientRectangle, ForeColor); return; }
                WeaponGeometry.Bounds(all, out Vector3 min, out Vector3 max);
                Vector3 pad = (max - min) * 0.04f + new Vector3(0.01f);
                min -= pad; max += pad;
                int w = Width / 2, h = Height / 2;
                //Side: X right, Z up. Top: X right, Y down. Front (from the muzzle): Y right, Z up.
                View(g, new Rectangle(0, 0, w, h), "Side (X, Z)", v => new PointF(v.X, v.Z), new PointF(min.X, min.Z), new PointF(max.X, max.Z), dark);
                View(g, new Rectangle(0, h, w, Height - h), "Top (X, Y)", v => new PointF(v.X, -v.Y), new PointF(min.X, -max.Y), new PointF(max.X, -min.Y), dark);
                View(g, new Rectangle(w, 0, Width - w, h), "Front (Y, Z)", v => new PointF(v.Y, v.Z), new PointF(min.Y, min.Z), new PointF(max.Y, max.Z), dark);
                Rectangle legend = new Rectangle(w + 10, h + 10, Width - w - 20, Height - h - 20);
                using (Brush grey = new SolidBrush(Color.FromArgb(160, 150, 150, 150))) g.FillRectangle(grey, legend.X, legend.Y + 2, 12, 12);
                TextRenderer.DrawText(g, "retail first person (faint: hidden)", Font, new Point(legend.X + 18, legend.Y), ForeColor);
                using (Brush orange = new SolidBrush(Color.FromArgb(200, 255, 140, 0))) g.FillRectangle(orange, legend.X, legend.Y + 22, 12, 12);
                TextRenderer.DrawText(g, "new model (offset / rotation applied)", Font, new Point(legend.X + 18, legend.Y + 20), ForeColor);
                g.FillEllipse(Brushes.Red, legend.X + 2, legend.Y + 44, 8, 8);
                TextRenderer.DrawText(g, "markers (MUZZLE_POS_MARKER labelled)", Font, new Point(legend.X + 18, legend.Y + 40), ForeColor);
                g.FillEllipse(Brushes.LimeGreen, legend.X + 2, legend.Y + 64, 8, 8);
                TextRenderer.DrawText(g, "new muzzle (after Check Plan)", Font, new Point(legend.X + 18, legend.Y + 60), ForeColor);
                using (Brush blue = new SolidBrush(Color.FromArgb(200, 40, 120, 255))) g.FillRectangle(blue, legend.X, legend.Y + 82, 12, 12);
                TextRenderer.DrawText(g, "the part selected in the table", Font, new Point(legend.X + 18, legend.Y + 80), ForeColor);
            }

            private void View(Graphics g, Rectangle area, string title, Func<Vector3, PointF> project, PointF lo, PointF hi, bool dark)
            {
                area.Inflate(-6, -6);
                using (Pen frame = new Pen(dark ? Color.FromArgb(70, 70, 75) : Color.FromArgb(200, 200, 205))) g.DrawRectangle(frame, area);
                TextRenderer.DrawText(g, title, Font, new Point(area.X + 4, area.Y + 2), ForeColor);
                float sx = (area.Width - 10) / Math.Max(1e-5f, hi.X - lo.X), sy = (area.Height - 24) / Math.Max(1e-5f, hi.Y - lo.Y);
                float s = Math.Min(sx, sy);
                float ox = area.X + 5 + ((area.Width - 10) - (hi.X - lo.X) * s) / 2, oy = area.Bottom - 5 - ((area.Height - 24) - (hi.Y - lo.Y) * s) / 2;
                Func<Vector3, PointF> screen = v => { PointF p = project(v); return new PointF(ox + (p.X - lo.X) * s, oy - (p.Y - lo.Y) * s); };
                Region clip = g.Clip;
                g.SetClip(area);
                using (Brush shown = new SolidBrush(Color.FromArgb(110, 95, 95, 100)))
                using (Brush hidden = new SolidBrush(Color.FromArgb(28, 120, 120, 125)))
                    foreach (Tuple<List<Vector3>, bool> part in Retail) Fill(g, part.Item1, screen, part.Item2 ? hidden : shown);
                if (Model != null)
                    using (Brush orange = new SolidBrush(Color.FromArgb(55, 255, 130, 0))) Fill(g, Model, screen, orange);
                if (Highlight != null)
                    using (Brush blue = new SolidBrush(Color.FromArgb(150, 40, 120, 255))) Fill(g, Highlight, screen, blue);
                foreach (Tuple<Vector3, string> marker in Markers)
                {
                    PointF p = screen(marker.Item1);
                    g.FillEllipse(Brushes.Red, p.X - 3, p.Y - 3, 6, 6);
                    if (marker.Item2 != null && marker.Item2.IndexOf("MUZZLE_POS", StringComparison.OrdinalIgnoreCase) >= 0)
                        TextRenderer.DrawText(g, "muzzle", Font, new Point((int)p.X + 4, (int)p.Y - 14), Color.Red);
                }
                if (NewMuzzle != null)
                {
                    PointF p = screen(NewMuzzle.Value);
                    g.FillEllipse(Brushes.LimeGreen, p.X - 4, p.Y - 4, 8, 8);
                }
                g.Clip = clip;
            }

            private static void Fill(Graphics g, List<Vector3> corners, Func<Vector3, PointF> screen, Brush brush)
            {
                PointF[] triangle = new PointF[3];
                int step = corners.Count > 90000 ? 6 : corners.Count > 45000 ? 3 : 1;
                for (int i = 0; i + 2 < corners.Count; i += 3 * step)
                {
                    triangle[0] = screen(corners[i]); triangle[1] = screen(corners[i + 1]); triangle[2] = screen(corners[i + 2]);
                    g.FillPolygon(brush, triangle);
                }
            }
        }
    }
}

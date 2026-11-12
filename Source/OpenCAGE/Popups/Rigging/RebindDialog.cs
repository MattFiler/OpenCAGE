using Assimp;
using CATHODE;
using OpenCAGE.Rigging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE.Popups.Rigging
{
    /// <summary>
    /// "Rebind onto a game skeleton": moves a model skinned to a foreign skeleton onto one of the game's character rigs
    /// (<see cref="SkinRebind"/>), with the joint mapping editable. Rebind writes into the import window's scene, which then
    /// imports it as a mesh on that rig.
    /// </summary>
    public class RebindDialog : Form
    {
        private readonly Scene _scene;
        private readonly float _unitScale;
        private readonly SourceRig _source;
        private readonly Dictionary<string, string> _overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private readonly ComboBox _rig = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
        private readonly CheckBox _fitSize = new CheckBox() { Text = "Size it to the rig", Checked = true, AutoSize = true };
        private readonly NumericUpDown _scale = new NumericUpDown() { Minimum = 0.05m, Maximum = 20m, DecimalPlaces = 3, Increment = 0.05m, Value = 1m, Width = 70 };
        private readonly CheckBox _keepHead = new CheckBox() { Text = "Keep the head's shape", Checked = true, AutoSize = true };
        private readonly CheckBox _headHeight = new CheckBox() { Text = "Keep the head's own height over the shoulders", Checked = true, AutoSize = true };
        private readonly CheckBox _stretchTail = new CheckBox() { Text = "Stretch a tail to the rig's length", Checked = false, AutoSize = true };
        private readonly DataGridView _grid = new DataGridView();
        private readonly PictureBox _picture = new PictureBox() { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(40, 40, 40) };
        private readonly TextBox _report = new TextBox() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true };
        private readonly Button _ok = new Button() { Text = "Rebind", AutoSize = true };
        private readonly Button _cancel = new Button() { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        private readonly Button _load = new Button() { Text = "Load mapping...", AutoSize = true };
        private readonly Button _save = new Button() { Text = "Save mapping...", AutoSize = true };
        private readonly Button _reset = new Button() { Text = "Reset mapping", AutoSize = true };
        private readonly Timer _debounce = new Timer() { Interval = 250 };

        private List<Skeleton> _rigs;
        private SkinRebind.Result _result;
        private bool _filling;

        /// <summary>The rig the model was rebound onto, once Rebind has been pressed.</summary>
        public Skeleton Rig { get; private set; }
        public SkinRebind.Result Result { get { return _result; } }

        public RebindDialog(Scene scene, float unitScale, string fileName, string preferredRig = null)
        {
            _scene = scene;
            _unitScale = unitScale > 0 ? unitScale : 1;
            _source = SourceRig.Read(scene, _unitScale);
            Text = "Rebind onto a game skeleton: " + fileName;
            Icon = SharedFormIcon.Icon;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1280, 860);
            MinimumSize = new Size(980, 640);
            AcceptButton = _ok;
            CancelButton = _cancel;
            BuildLayout();

            _rigs = RebindImport.CharacterRigs(Singleton.Animations);
            foreach (Skeleton s in _rigs) _rig.Items.Add(s.Name);
            int pick = _rigs.FindIndex(o => string.Equals(o.Name, preferredRig ?? "", StringComparison.OrdinalIgnoreCase));
            if (pick < 0) pick = _rigs.FindIndex(o => string.Equals(o.Name, "MALE", StringComparison.OrdinalIgnoreCase));
            if (_rigs.Count != 0) _rig.SelectedIndex = Math.Max(0, pick);

            _rig.SelectedIndexChanged += (s, e) => Changed();
            _fitSize.CheckedChanged += (s, e) => Changed();
            _scale.ValueChanged += (s, e) => Changed();
            _keepHead.CheckedChanged += (s, e) => Changed();
            _headHeight.CheckedChanged += (s, e) => Changed();
            _stretchTail.CheckedChanged += (s, e) => Changed();
            _debounce.Tick += (s, e) => { _debounce.Stop(); Rebuild(); };
            _ok.Click += (s, e) => Commit();
            _load.Click += (s, e) => LoadMapping();
            _save.Click += (s, e) => SaveMapping();
            _reset.Click += (s, e) => { _overrides.Clear(); Rebuild(); };
            FormClosed += (s, e) => { _debounce.Dispose(); _picture.Image?.Dispose(); };

            OpenCAGE.Theming.ThemeManager.ApplyToForm(this);
            if (_source == null)
            {
                _report.Text = "Nothing in this file is skinned to a skeleton, so there is nothing to rebind.";
                _ok.Enabled = false;
            }
            else Rebuild();
        }

        private void BuildLayout()
        {
            FlowLayoutPanel top = new FlowLayoutPanel() { Dock = DockStyle.Top, Height = 64, Padding = new Padding(8, 8, 8, 0), WrapContents = true };
            top.Controls.Add(new Label() { Text = "Rig:", AutoSize = true, Margin = new Padding(0, 6, 2, 0) });
            top.Controls.Add(_rig);
            top.Controls.Add(new Label() { Text = "   ", AutoSize = true });
            top.Controls.Add(_fitSize);
            top.Controls.Add(new Label() { Text = "then x", AutoSize = true, Margin = new Padding(0, 6, 2, 0) });
            top.Controls.Add(_scale);
            top.Controls.Add(_keepHead);
            top.Controls.Add(_headHeight);
            top.Controls.Add(_stretchTail);
            Label hint = new Label()
            {
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Text = "Each part of the body is a joint of the file, found from its name and the hierarchy: pick another in the right-hand column to fix one, or (none) to leave it out. The spine, neck and tail follow the joints between the parts.",
                MaximumSize = new Size(1200, 0),
            };
            top.Controls.Add(hint);

            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.EditMode = DataGridViewEditMode.EditOnEnter;
            _grid.Columns.Add(new DataGridViewTextBoxColumn() { HeaderText = "Part", ReadOnly = true, FillWeight = 34 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn() { HeaderText = "Rig bone", ReadOnly = true, FillWeight = 22 });
            _grid.Columns.Add(new DataGridViewComboBoxColumn() { HeaderText = "Joint in the file", FillWeight = 30, FlatStyle = FlatStyle.Flat, DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton });
            _grid.Columns.Add(new DataGridViewTextBoxColumn() { HeaderText = "Found by", ReadOnly = true, FillWeight = 14 });
            _grid.CurrentCellDirtyStateChanged += (s, e) => { if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            _grid.CellValueChanged += GridValueChanged;
            _grid.DataError += (s, e) => e.ThrowException = false;

            SplitContainer split = new SplitContainer() { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 520 };
            split.Panel1.Controls.Add(_grid);
            SplitContainer right = new SplitContainer() { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
            _picture.Dock = DockStyle.Fill;
            _report.Dock = DockStyle.Fill;
            right.Panel1.Controls.Add(_picture);
            right.Panel2.Controls.Add(_report);
            split.Panel2.Controls.Add(right);

            FlowLayoutPanel buttons = new FlowLayoutPanel() { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8, 4, 8, 4) };
            buttons.Controls.Add(_cancel);
            buttons.Controls.Add(_ok);
            buttons.Controls.Add(_reset);
            buttons.Controls.Add(_save);
            buttons.Controls.Add(_load);

            Controls.Add(split);
            Controls.Add(buttons);
            Controls.Add(top);
            Shown += (s, e) => { split.SplitterDistance = Math.Max(420, ClientSize.Width * 2 / 5); right.SplitterDistance = Math.Max(200, right.Height * 3 / 5); };
        }

        private Skeleton CurrentRig => _rig.SelectedIndex >= 0 && _rig.SelectedIndex < _rigs.Count ? _rigs[_rig.SelectedIndex] : null;

        private void Changed()
        {
            if (_filling) return;
            _debounce.Stop();
            _debounce.Start();
        }

        private SkinRebind.Options Options()
        {
            return new SkinRebind.Options()
            {
                FitSize = _fitSize.Checked,
                Scale = (float)_scale.Value,
                KeepHeadShape = _keepHead.Checked,
                HeadAtOwnHeight = _headHeight.Checked,
                StretchTail = _stretchTail.Checked,
                Map = new Dictionary<string, string>(_overrides, StringComparer.OrdinalIgnoreCase),
            };
        }

        private void Rebuild()
        {
            Skeleton rig = CurrentRig;
            if (_source == null || rig == null) return;
            Cursor = Cursors.WaitCursor;
            try
            {
                _result = SkinRebind.Build(_source, rig, Options());
                FillGrid();
                List<string> lines = new List<string>();
                if (!_result.Ok) lines.Add("CAN'T REBIND: " + _result.Problem);
                lines.AddRange(_result.Warnings.Select(o => "! " + o));
                lines.AddRange(_result.Report);
                if (_result.Ok)
                {
                    lines.Add("");
                    lines.Add("Rebind moves the mesh onto " + rig.Name + " and imports it skinned to it, as a DisplayModel composite a Character can wear (display_model). "
                        + "It then plays " + rig.Name + "'s animations - and every set that is retargeted onto " + rig.Name + ".");
                    lines.Add("A character also needs a hit-collision part before bullets can hit it, and a CA_CHARACTER material: import_skinned_onto_rig (MCP) or the docs' Rebind section say how.");
                }
                _report.Text = string.Join(Environment.NewLine, lines);
                Image old = _picture.Image;
                _picture.Image = _result.Ok ? RebindImport.Picture(_result, _scene, RebindImport.ShowcaseClip(Singleton.Animations, rig)) : null;
                old?.Dispose();
                _ok.Enabled = _result.Ok;
            }
            catch (Exception e)
            {
                _report.Text = "The rebind failed: " + e.Message;
                _ok.Enabled = false;
            }
            finally { Cursor = Cursors.Default; }
        }

        private void FillGrid()
        {
            _filling = true;
            try
            {
                Skeleton rig = CurrentRig;
                List<string> joints = new List<string>() { "(none)" };
                joints.AddRange(_source.Joints.Select(o => o.Name).Where(o => !string.IsNullOrEmpty(o)).Distinct());
                DataGridViewComboBoxColumn column = (DataGridViewComboBoxColumn)_grid.Columns[2];
                if (column.Items.Count != joints.Count) { column.Items.Clear(); column.Items.AddRange(joints.ToArray()); }

                List<string> roles = RigRoles.AllRoles();
                if (_grid.Rows.Count != roles.Count)
                {
                    _grid.Rows.Clear();
                    foreach (string role in roles) _grid.Rows.Add(RigRoles.Describe(role), "", "(none)", "");
                }
                for (int i = 0; i < roles.Count; i++)
                {
                    string role = roles[i];
                    int bone = RigRoles.TargetBone(rig, role);
                    DataGridViewRow row = _grid.Rows[i];
                    row.Tag = role;
                    row.Cells[1].Value = bone >= 0 ? RigRoles.Short(rig.Bones[bone].Name) : (role == "TAIL" ? "(no tail)" : role.EndsWith("LEG2") ? "(laid along the leg)" : "(rig has none)");
                    int joint = _result?.Roles != null ? _result.Roles[role] : -1;
                    row.Cells[2].Value = joint >= 0 ? _source.Joints[joint].Name : "(none)";
                    row.Cells[3].Value = _result?.Roles != null && _result.Roles.How.TryGetValue(role, out string how) ? how : "";
                    row.DefaultCellStyle.ForeColor = joint < 0 ? SystemColors.GrayText : _grid.DefaultCellStyle.ForeColor;
                }
            }
            finally { _filling = false; }
        }

        private void GridValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_filling || e.RowIndex < 0 || e.ColumnIndex != 2) return;
            string role = _grid.Rows[e.RowIndex].Tag as string;
            if (role == null) return;
            string joint = _grid.Rows[e.RowIndex].Cells[2].Value as string;
            _overrides[role] = joint == null || joint == "(none)" ? "" : joint;
            Changed();
        }

        private void LoadMapping()
        {
            using (OpenFileDialog open = new OpenFileDialog() { Filter = "Rebind mapping (*.json)|*.json|All files|*.*" })
            {
                if (open.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    _overrides.Clear();
                    foreach (KeyValuePair<string, string> kv in RebindImport.FromJson(File.ReadAllText(open.FileName))) _overrides[kv.Key] = kv.Value;
                    Rebuild();
                }
                catch (Exception ex) { MessageBox.Show(this, "That mapping could not be read: " + ex.Message, "Load mapping", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
        }

        /* Saves the whole mapping as used, not only the changes, so it reads on its own and carries to another file of the same rig */
        private void SaveMapping()
        {
            if (_result?.Roles == null) return;
            using (SaveFileDialog save = new SaveFileDialog() { Filter = "Rebind mapping (*.json)|*.json", FileName = "rebind_mapping.json" })
            {
                if (save.ShowDialog(this) != DialogResult.OK) return;
                Dictionary<string, string> map = RigRoles.AllRoles().ToDictionary(r => r, r => _result.Roles.Has(r) ? _source.Joints[_result.Roles[r]].Name : "");
                File.WriteAllText(save.FileName, RebindImport.ToJson(map));
            }
        }

        private void Commit()
        {
            if (_debounce.Enabled) { _debounce.Stop(); Rebuild(); }
            if (_result == null || !_result.Ok) return;
            RebindImport.Apply(_result, _scene);
            Rig = CurrentRig;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}

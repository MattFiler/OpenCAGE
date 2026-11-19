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
    public partial class RebindDialog : Form
    {
        private readonly Scene _scene;
        private readonly float _unitScale;
        private readonly SourceRig _source;
        private readonly Dictionary<string, string> _overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
            InitializeComponent();
            Text = "Rebind onto a game skeleton: " + fileName;
            Icon = SharedFormIcon.Icon;

            _rigs = RebindImport.CharacterRigs(Singleton.Animations);
            foreach (Skeleton s in _rigs) _rig.Items.Add(s.Name);
            int pick = _rigs.FindIndex(o => string.Equals(o.Name, preferredRig ?? "", StringComparison.OrdinalIgnoreCase));
            if (pick < 0) pick = _rigs.FindIndex(o => string.Equals(o.Name, "MALE", StringComparison.OrdinalIgnoreCase));
            if (_rigs.Count != 0) _rig.SelectedIndex = Math.Max(0, pick);
            //attached once the rig is picked, so picking it doesn't queue a second rebuild
            _rig.SelectedIndexChanged += Option_Changed;

            OpenCAGE.Theming.ThemeManager.ApplyToForm(this);
            if (_source == null)
            {
                _report.Text = "Nothing in this file is skinned to a skeleton, so there is nothing to rebind.";
                _ok.Enabled = false;
            }
            else Rebuild();
        }

        private void RebindDialog_Shown(object sender, EventArgs e)
        {
            _split.SplitterDistance = Math.Max(420, ClientSize.Width * 2 / 5);
            _right.SplitterDistance = Math.Max(200, _right.Height * 3 / 5);
        }

        private void RebindDialog_FormClosed(object sender, FormClosedEventArgs e)
        {
            _debounce.Dispose();
            _picture.Image?.Dispose();
        }

        private void Option_Changed(object sender, EventArgs e)
        {
            Changed();
        }

        private void Debounce_Tick(object sender, EventArgs e)
        {
            _debounce.Stop();
            Rebuild();
        }

        private void Ok_Click(object sender, EventArgs e)
        {
            Commit();
        }

        private void Load_Click(object sender, EventArgs e)
        {
            LoadMapping();
        }

        private void Save_Click(object sender, EventArgs e)
        {
            SaveMapping();
        }

        private void Reset_Click(object sender, EventArgs e)
        {
            _overrides.Clear();
            Rebuild();
        }

        private void Grid_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void Grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
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

using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.Brushwork;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// A brush's settings: its size, how each face is textured (material, scale, offset, rotation) and its collision. Edits go to a
    /// working copy; Apply commits them as one undo step that rebuilds the brush's model and collision.
    /// </summary>
    public partial class BrushEditor : BaseWindow
    {
        private readonly ShortGuid _compositeId, _entityId;
        private BrushDefinition _working;
        private bool _loading;
        private bool _dirty;

        private const string GridItem = "(grey grid - the brush default)";

        public BrushEditor(Composite composite, Entity entity) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            _compositeId = composite.shortGUID;
            _entityId = entity.shortGUID;
            InitializeComponent();
            Icon = SharedFormIcon.Icon;
            StayAboveEditor = true;
            _collision.Items.AddRange(BrushCollision.Modes);
            LoadBrush();
            BrushService.Changed += OnBrushChanged;
            FormClosed += (o, e) => BrushService.Changed -= OnBrushChanged;
            Theming.ThemeManager.ApplyToForm(this);
        }

        private void BrushEditor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !(ActiveControl is ComboBox combo && combo.DroppedDown)) { e.SuppressKeyPress = true; ApplyChanges(); }
            else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; Close(); }
        }

        private void _size_ValueChanged(object sender, EventArgs e) => SizeChanged();
        private void _faceSetting_Changed(object sender, EventArgs e) => FaceChanged();
        private void _faces_SelectedIndexChanged(object sender, EventArgs e) => ShowFace();
        private void _allFaces_Click(object sender, EventArgs e) => CopyFaceToAll();
        private void _align_Click(object sender, EventArgs e) => AlignToWorld();
        private void _apply_Click(object sender, EventArgs e) => ApplyChanges();
        private void _close_Click(object sender, EventArgs e) => Close();

        private void _collision_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_loading && _working != null) { _working.Collision = (string)_collision.SelectedItem; Dirty(); }
        }

        private void _doubleSided_CheckedChanged(object sender, EventArgs e)
        {
            if (!_loading && _working != null) { _working.DoubleSided = _doubleSided.Checked; Dirty(); }
        }

        private bool TryGetTarget(out Level level, out Composite composite, out FunctionEntity entity)
        {
            level = Content?.Level;
            composite = level?.Commands?.GetComposite(_compositeId);
            entity = composite?.GetEntityByID(_entityId) as FunctionEntity;
            return level != null && composite != null && entity != null;
        }

        /* The brush as the level has it now, into the controls (and a fresh working copy) */
        private void LoadBrush()
        {
            if (!TryGetTarget(out Level level, out Composite composite, out FunctionEntity entity))
            {
                Close();
                return;
            }
            BrushDefinition brush = BrushService.Get(level, composite, entity);
            if (brush == null)
            {
                Close();
                return;
            }
            _loading = true;
            try
            {
                _working = brush.Clone();
                string name = level.Commands.Utils.GetEntityName(composite, entity);
                Text = "Brush - " + name;
                _title.Text = (brush.Kind == BrushKind.Plane ? "Plane" : brush.Kind == BrushKind.Box ? "Box" : "Mesh") + " brush \"" + name + "\"" + (BrushService.HasOwnRow(level, composite, entity) ? "" : " (a copy: it gets its own model when changed)");
                ShowBounds();
                _sizeX.Value = Clamp(_sizeX, (decimal)brush.Size.X);
                _sizeY.Value = Clamp(_sizeY, (decimal)Math.Max(brush.Size.Y, BrushDefinition.MinimumSize));
                _sizeZ.Value = Clamp(_sizeZ, (decimal)brush.Size.Z);
                _sizeY.Enabled = brush.Kind == BrushKind.Box;
                _sizeX.Enabled = _sizeZ.Enabled = brush.Kind != BrushKind.Mesh;
                _collision.SelectedItem = BrushCollision.Modes.Contains(brush.Collision) ? brush.Collision : "solid";
                _doubleSided.Visible = brush.Kind == BrushKind.Plane;
                _doubleSided.Checked = brush.DoubleSided;

                _material.BeginUpdate();
                _material.Items.Clear();
                _material.Items.Add(GridItem);
                _material.Items.AddRange(level.Materials.Entries.Where(o => o != null && !string.IsNullOrEmpty(o.Name)).Select(o => o.Name).Distinct().OrderBy(o => o, StringComparer.OrdinalIgnoreCase).Cast<object>().ToArray());
                _material.EndUpdate();

                int selected = Math.Max(0, _faces.SelectedIndex);
                _faces.Items.Clear();
                string[] names = brush.FaceNames;
                List<BrushFace> faces = _working.FaceTextures;
                for (int i = 0; i < faces.Count && i < names.Length; i++)
                    _faces.Items.Add(FaceLabel(names[i], faces[i]));
                if (_faces.Items.Count != 0) _faces.SelectedIndex = Math.Min(selected, _faces.Items.Count - 1);
                _status.Text = "";
                _dirty = false;
                _apply.Enabled = false;
            }
            finally
            {
                _loading = false;
            }
            ShowFace();
        }

        private static decimal Clamp(NumericUpDown control, decimal value) => Math.Max(control.Minimum, Math.Min(control.Maximum, value));

        private static string FaceLabel(string name, BrushFace face) => name + "  " + (string.IsNullOrEmpty(face.Material) ? "(grid)" : face.Material);

        private void ShowBounds()
        {
            _bounds.Text = "From " + Vec(_working.Min) + " to " + Vec(_working.Max) + " in its own space" + (_working.Kind == BrushKind.Box ? " (resized about its bottom centre)" : "");
        }

        private static string Vec(System.Numerics.Vector3 v) => "(" + v.X.ToString("0.###", CultureInfo.InvariantCulture) + ", " + v.Y.ToString("0.###", CultureInfo.InvariantCulture) + ", " + v.Z.ToString("0.###", CultureInfo.InvariantCulture) + ")";

        private BrushFace SelectedFace => _working != null && _faces.SelectedIndex >= 0 && _faces.SelectedIndex < _working.FaceTextures.Count ? _working.FaceTextures[_faces.SelectedIndex] : null;

        private void ShowFace()
        {
            BrushFace face = SelectedFace;
            bool any = face != null;
            foreach (Control control in new Control[] { _material, _scaleU, _scaleV, _offsetU, _offsetV, _rotation, _allFaces })
                control.Enabled = any;
            if (!any) return;
            bool was = _loading;
            _loading = true;
            try
            {
                _material.Text = string.IsNullOrEmpty(face.Material) ? GridItem : face.Material;
                _scaleU.Value = Clamp(_scaleU, (decimal)face.Scale.X);
                _scaleV.Value = Clamp(_scaleV, (decimal)face.Scale.Y);
                _offsetU.Value = Clamp(_offsetU, (decimal)face.Offset.X);
                _offsetV.Value = Clamp(_offsetV, (decimal)face.Offset.Y);
                _rotation.Value = Clamp(_rotation, (decimal)face.Rotation);
            }
            finally
            {
                _loading = was;
            }
        }

        private void SizeChanged()
        {
            if (_loading || _working == null || _working.Kind == BrushKind.Mesh) return;
            System.Numerics.Vector3 centre = (_working.Min + _working.Max) / 2;
            float x = (float)_sizeX.Value, y = (float)_sizeY.Value, z = (float)_sizeZ.Value;
            _working.Min = new System.Numerics.Vector3(centre.X - x / 2, _working.Min.Y, centre.Z - z / 2);
            _working.Max = new System.Numerics.Vector3(centre.X + x / 2, _working.Kind == BrushKind.Plane ? _working.Min.Y : _working.Min.Y + y, centre.Z + z / 2);
            ShowBounds();
            Dirty();
        }

        private void FaceChanged()
        {
            BrushFace face = SelectedFace;
            if (_loading || face == null) return;
            string text = (_material.Text ?? "").Trim();
            face.Material = text.Length == 0 || text == GridItem || text == BrushMaterials.GridName ? null : text;
            face.Scale = new System.Numerics.Vector2((float)_scaleU.Value, (float)_scaleV.Value);
            face.Offset = new System.Numerics.Vector2((float)_offsetU.Value, (float)_offsetV.Value);
            face.Rotation = (float)_rotation.Value;
            _faces.Items[_faces.SelectedIndex] = FaceLabel(_working.FaceNames[_faces.SelectedIndex], face);
            Dirty();
        }

        private void CopyFaceToAll()
        {
            BrushFace face = SelectedFace;
            if (face == null) return;
            List<BrushFace> faces = _working.FaceTextures;
            for (int i = 0; i < faces.Count; i++)
            {
                faces[i].Material = face.Material;
                faces[i].Scale = face.Scale;
                faces[i].Offset = face.Offset;
                faces[i].Rotation = face.Rotation;
                _faces.Items[i] = FaceLabel(_working.FaceNames[i], faces[i]);
            }
            Dirty();
        }

        private void AlignToWorld()
        {
            if (!TryGetTarget(out Level level, out Composite composite, out FunctionEntity entity)) return;
            cTransform placement = entity.GetParameter(ShortGuids.position)?.content as cTransform ?? new cTransform();
            _working.UvOrigin = BrushService.WorldAlignedOrigin(level.Commands, composite, placement);
            foreach (BrushFace face in _working.FaceTextures)
                face.Offset = System.Numerics.Vector2.Zero;
            ShowFace();
            Dirty();
        }

        private void Dirty()
        {
            if (_loading) return;
            _dirty = true;
            _apply.Enabled = true;
            _status.Text = "";
        }

        private void ApplyChanges()
        {
            if (!_dirty || _working == null) return;
            if (!TryGetTarget(out Level level, out Composite composite, out FunctionEntity entity))
            {
                Close();
                return;
            }
            //Typed material names have to be the level's
            foreach (BrushFace face in _working.FaceTextures)
            {
                if (face.Material != null && BrushMaterials.Find(level, face.Material) == null)
                {
                    _status.Text = "The level has no material called '" + face.Material + "'.";
                    return;
                }
                if (face.Material != null)
                    face.Material = BrushMaterials.Find(level, face.Material).Name;
            }
            List<string> notes = new List<string>();
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                BrushService.Apply(level, composite, entity, _working.Clone(), "Edit " + level.Commands.Utils.GetEntityName(composite, entity), notes);
            }
            catch (BrushException ex)
            {
                _status.Text = ex.Message;
                return;
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
            LoadBrush();
            if (notes.Count != 0) _status.Text = string.Join(" ", notes.Take(3));
        }

        /* Undo / redo of this brush (or a change made by a tool) shows here - unless there are changes not applied yet */
        private void OnBrushChanged(ShortGuid composite, ShortGuid entity)
        {
            if (composite != _compositeId || entity != _entityId || IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action(() => OnBrushChanged(composite, entity))); return; }
            if (!_dirty) LoadBrush();
            else _status.Text = "The brush was changed elsewhere: Apply replaces that with what is shown here.";
        }
    }
}

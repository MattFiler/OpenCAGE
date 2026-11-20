using CATHODE;
using CATHODE.Scripting;
using CathodeLib;
using OpenCAGE.Markup;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE.Popups
{
    /// <summary>
    /// A composite's markup: the built-in markup OpenCAGE draws for it (read-only), and the level's own pieces, which are
    /// added, edited and removed here. Every change is an undo step (a run of field edits folds into one) and shows in the
    /// viewport straight away. Markup is never an entity: the composite itself is left alone.
    /// </summary>
    public partial class CompositeMarkupEditor : BaseWindow
    {
        private static CompositeMarkupEditor _open;

        private readonly ShortGuid _compositeId;
        private List<CompositeMarkupTable.Markup> _working = new List<CompositeMarkupTable.Markup>();
        private bool _loading;
        //Field edits made in this window fold into one undo step until something else is done
        private object _session = new object();

        public static void ShowFor(Composite composite)
        {
            if (composite == null)
                return;
            if (_open != null && !_open.IsDisposed)
            {
                if (_open._compositeId == composite.shortGUID)
                {
                    _open.Activate();
                    return;
                }
                _open.Close();
            }
            _open = new CompositeMarkupEditor(composite);
            _open.Show(Singleton.Editor);
        }

        /// <summary>The Composite Browser's right-click on a composite offers this window (the browser calls this before showing its menu).</summary>
        public static void PrepareMenu(ContextMenuStrip menu, Composite composite)
        {
            if (menu == null) return;
            ToolStripMenuItem item = menu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(o => (o.Name ?? "") == "compositeMarkupMenuItem");
            if (item == null)
            {
                item = new ToolStripMenuItem("Edit Markup") { Name = "compositeMarkupMenuItem", ToolTipText = "Shapes drawn in the viewport at every instance of this composite, to show what it is. Editor-only: never part of the level's scripts or the game." };
                item.Click += (o, e) =>
                {
                    Commands commands = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;
                    if (item.Tag is ShortGuid id && commands?.GetComposite(id) is Composite target)
                        ShowFor(target);
                };
                menu.Items.Add(item);
            }
            item.Visible = composite != null;
            item.Tag = composite?.shortGUID;
        }

        public CompositeMarkupEditor(Composite composite) : base(WindowClosesOn.COMMANDS_RELOAD)
        {
            _compositeId = composite.shortGUID;
            InitializeComponent();
            Icon = SharedFormIcon.Icon;
            StayAboveEditor = true;
            BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/composite-markup/");

            _newShape.Items.AddRange(CompositeMarkup.ShapeNames.Where(o => o != nameof(CompositeMarkupTable.Shape.Mesh)).Cast<object>().ToArray());
            _newShape.SelectedIndex = 0;
            _shape.Items.AddRange(CompositeMarkup.ShapeNames.Cast<object>().ToArray());

            CompositeMarkup.Changed += OnMarkupChanged;
            FormClosed += (o, e) =>
            {
                CompositeMarkup.Changed -= OnMarkupChanged;
                if (_open == this) _open = null;
            };

            Theming.ThemeManager.ApplyToForm(this);
            LoadMarkup();
        }

        private bool TryGetTarget(out Commands commands, out Composite composite)
        {
            commands = Content?.Level?.Commands;
            composite = commands?.GetComposite(_compositeId);
            return composite != null;
        }

        private void OnMarkupChanged()
        {
            if (!IsDisposed && !_applying)
                LoadMarkup();
        }

        /* What the level has now, into the list (the selection kept where it can be) */
        private void LoadMarkup()
        {
            if (!TryGetTarget(out Commands commands, out Composite composite))
            {
                Close();
                return;
            }
            _loading = true;
            try
            {
                Text = "Markup - " + Path.GetFileName(composite.name ?? "");
                _title.Text = composite.name;
                BuiltInMarkup.Entry builtIn = CompositeMarkup.BuiltInFor(composite);
                _builtIn.Text = builtIn != null
                    ? "Built in (always drawn, can't be edited): " + builtIn.Description + Environment.NewLine + "Add pieces of this level's own below."
                    : "No built-in markup. Add pieces of this level's own below: they are drawn at every instance of the composite.";

                int selected = _pieces.SelectedIndex;
                _working = CompositeMarkup.Pieces(commands, composite);
                _pieces.BeginUpdate();
                _pieces.Items.Clear();
                foreach (CompositeMarkupTable.Markup piece in _working)
                    _pieces.Items.Add(Describe(piece));
                _pieces.EndUpdate();
                if (_pieces.Items.Count != 0)
                    _pieces.SelectedIndex = Math.Max(0, Math.Min(selected, _pieces.Items.Count - 1));
            }
            finally
            {
                _loading = false;
            }
            ShowPiece();
        }

        private static string Describe(CompositeMarkupTable.Markup piece) =>
            (string.IsNullOrWhiteSpace(piece.name) ? piece.shape.ToString() : piece.name) + "  (" + piece.shape + ")";

        private CompositeMarkupTable.Markup Selected => _pieces.SelectedIndex >= 0 && _pieces.SelectedIndex < _working.Count ? _working[_pieces.SelectedIndex] : null;

        private void _pieces_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_loading) ShowPiece();
        }

        /* The selected piece into the fields */
        private void ShowPiece()
        {
            CompositeMarkupTable.Markup piece = Selected;
            _loading = true;
            try
            {
                foreach (Control control in _properties.Controls)
                    if (!(control is Label)) control.Enabled = piece != null;
                _duplicate.Enabled = _remove.Enabled = piece != null;
                if (piece == null)
                {
                    _sizeHint.Text = _working.Count == 0 ? "This level adds no markup of its own to this composite." : "Select a piece to edit it.";
                    return;
                }
                _name.Text = piece.name ?? "";
                _shape.SelectedItem = piece.shape.ToString();
                //A mesh is its triangles: it can't become another shape, nor another shape a mesh, here
                _shape.Enabled = piece.shape != CompositeMarkupTable.Shape.Mesh;
                SetVector(_positionX, _positionY, _positionZ, piece.position);
                SetVector(_rotationX, _rotationY, _rotationZ, piece.rotation);
                SetVector(_sizeX, _sizeY, _sizeZ, piece.size);
                _colour.BackColor = ToColor(piece.colour);
                _sizeHint.Text = SizeHint(piece.shape);
            }
            finally
            {
                _loading = false;
            }
        }

        private static string SizeHint(CompositeMarkupTable.Shape shape)
        {
            switch (shape)
            {
                case CompositeMarkupTable.Shape.Box: return "Size: width, height, depth (m). It stands on its position.";
                case CompositeMarkupTable.Shape.Cylinder: return "Size: diameter across X, height, diameter across Z (m). It stands on its position.";
                case CompositeMarkupTable.Shape.Cone: return "Size: base diameter across X, height, base diameter across Z (m). It stands on its position.";
                case CompositeMarkupTable.Shape.Sphere: return "Size: diameters (m). It rests on its position.";
                case CompositeMarkupTable.Shape.Arrow: return "Size: width, thickness, length (m). It lies flat from its position, pointing along the composite's +Z (turn it with the rotation).";
                case CompositeMarkupTable.Shape.Ring: return "Size: outer diameter across X, thickness, outer diameter across Z (m). It lies flat round its position.";
                case CompositeMarkupTable.Shape.FrontLabel: return "The word FRONT standing on the floor, 0.43 m long, reading the right way round from the composite's -Z side. Size scales it.";
                default: return "An imported mesh, in metres. Size scales it.";
            }
        }

        private static void SetVector(NumericUpDown x, NumericUpDown y, NumericUpDown z, float[] values)
        {
            x.Value = Clamp(x, values != null && values.Length > 0 ? values[0] : 0);
            y.Value = Clamp(y, values != null && values.Length > 1 ? values[1] : 0);
            z.Value = Clamp(z, values != null && values.Length > 2 ? values[2] : 0);
        }

        private static decimal Clamp(NumericUpDown control, float value)
        {
            decimal v = float.IsNaN(value) ? 0 : (decimal)value;
            return Math.Max(control.Minimum, Math.Min(control.Maximum, v));
        }

        private static float[] GetVector(NumericUpDown x, NumericUpDown y, NumericUpDown z) => new float[] { (float)x.Value, (float)y.Value, (float)z.Value };

        private static Color ToColor(float[] colour)
        {
            int C(int i) => colour != null && colour.Length > i ? (int)Math.Round(Math.Max(0, Math.Min(1, colour[i])) * 255) : 255;
            return Color.FromArgb(C(0), C(1), C(2));
        }

        /* A field changed: the selected piece takes it, and the level with it (folded into this window's last step) */
        private void _field_Changed(object sender, EventArgs e)
        {
            CompositeMarkupTable.Markup piece = Selected;
            if (_loading || piece == null)
                return;
            piece.name = _name.Text.Trim();
            if (piece.shape != CompositeMarkupTable.Shape.Mesh && _shape.SelectedItem is string shapeName && Enum.TryParse(shapeName, out CompositeMarkupTable.Shape shape) && shape != CompositeMarkupTable.Shape.Mesh)
                piece.shape = shape;
            piece.position = GetVector(_positionX, _positionY, _positionZ);
            piece.rotation = GetVector(_rotationX, _rotationY, _rotationZ);
            piece.size = GetVector(_sizeX, _sizeY, _sizeZ);
            _sizeHint.Text = SizeHint(piece.shape);
            _pieces.Items[_pieces.SelectedIndex] = Describe(piece);
            Commit("Edit Markup", fold: true);
        }

        private void _colour_Click(object sender, EventArgs e)
        {
            CompositeMarkupTable.Markup piece = Selected;
            if (piece == null)
                return;
            using (ColorDialog dialog = new ColorDialog() { Color = ToColor(piece.colour), FullOpen = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                piece.colour = new float[] { dialog.Color.R / 255f, dialog.Color.G / 255f, dialog.Color.B / 255f };
                _colour.BackColor = dialog.Color;
            }
            Commit("Edit Markup", fold: true);
        }

        private void _add_Click(object sender, EventArgs e)
        {
            if (!(_newShape.SelectedItem is string shapeName) || !Enum.TryParse(shapeName, out CompositeMarkupTable.Shape shape))
                return;
            AddPiece(CompositeMarkup.NewPiece(shape), "Add Markup (" + shape + ")");
        }

        private void _import_Click(object sender, EventArgs e)
        {
            string path;
            using (OpenFileDialog dialog = new OpenFileDialog() { Title = "Import a mesh as markup", Filter = "Models|*.fbx;*.obj;*.gltf;*.glb;*.dae|All files|*.*" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                path = dialog.FileName;
            }

            CompositeMarkupTable.Markup piece = CompositeMarkup.NewPiece(CompositeMarkupTable.Shape.Mesh);
            try
            {
                CollisionProxyImporter.MeshSource source = CollisionProxyImporter.FromModelFile(path);
                if (!CompositeMarkup.TrySetMesh(piece, source.Positions, source.Indices, out string problem))
                {
                    _status.Text = problem;
                    return;
                }
                piece.name = source.Name;
            }
            catch (Exception ex)
            {
                _status.Text = "Could not read " + Path.GetFileName(path) + ": " + ex.Message;
                return;
            }
            AddPiece(piece, "Add Markup (" + piece.name + ")");
        }

        private void _duplicate_Click(object sender, EventArgs e)
        {
            CompositeMarkupTable.Markup piece = Selected;
            if (piece != null)
                AddPiece(piece.Clone(), "Duplicate Markup (" + Describe(piece) + ")");
        }

        private void AddPiece(CompositeMarkupTable.Markup piece, string label)
        {
            _working.Add(piece);
            _loading = true;
            _pieces.Items.Add(Describe(piece));
            _pieces.SelectedIndex = _pieces.Items.Count - 1;
            _loading = false;
            Commit(label, fold: false);
            ShowPiece();
        }

        private void _remove_Click(object sender, EventArgs e)
        {
            int index = _pieces.SelectedIndex;
            if (index < 0 || index >= _working.Count)
                return;
            string label = "Remove Markup (" + Describe(_working[index]) + ")";
            _working.RemoveAt(index);
            _loading = true;
            _pieces.Items.RemoveAt(index);
            if (_pieces.Items.Count != 0) _pieces.SelectedIndex = Math.Min(index, _pieces.Items.Count - 1);
            _loading = false;
            Commit(label, fold: false);
            ShowPiece();
        }

        private bool _applying;

        /* The working pieces into the level as an undo step */
        private void Commit(string label, bool fold)
        {
            if (!TryGetTarget(out Commands commands, out Composite composite))
                return;
            //A step that isn't a field edit starts a new run: the next field edit gets a step of its own
            if (!fold) _session = new object();
            _applying = true;
            try
            {
                CompositeMarkup.SetPieces(commands, composite, _working, label + " - " + Path.GetFileName(composite.name ?? ""), fold ? _session : null);
            }
            finally
            {
                _applying = false;
            }
            _status.Text = _working.Count + " piece(s) of this level's own. Saved with the level.";
        }

        private void _close_Click(object sender, EventArgs e) => Close();
    }
}

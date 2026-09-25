using CATHODE;
using CathodeLib.Havok;
using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// The choices for a new physics system, asked once its hull is built: the names, the body's mass and surface,
    /// and whether it sits where the composite's model does.
    /// </summary>
    public class PhysicsImportSettings : Form
    {
        private readonly TextBox _systemName;
        private readonly TextBox _bodyName;
        private readonly NumericUpDown _mass;
        private readonly NumericUpDown _friction;
        private readonly NumericUpDown _restitution;
        private readonly CheckBox _placeAtModel;
        private readonly PhysicsSystemImporter.HostDefaults _defaults;

        public PhysicsImportSettings(ConvexBody shape, string sourceName, PhysicsSystemImporter.HostDefaults defaults)
        {
            _defaults = defaults ?? new PhysicsSystemImporter.HostDefaults();

            Text = "New Physics System";
            Icon = SharedFormIcon.Icon;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(440, 290);
            Padding = new Padding(12);

            Label summary = new Label
            {
                Dock = DockStyle.Top,
                Height = 48,
                Text = shape.Vertices.Count + "-corner convex hull of " + sourceName
                    + (shape.FullHullVertices > shape.Vertices.Count ? " (simplified from " + shape.FullHullVertices + ")" : "")
                    + "\nVolume " + (shape.Volume * 1000f).ToString("0.###", CultureInfo.InvariantCulture) + " L, convex radius "
                    + (shape.ConvexRadius * 100f).ToString("0.##", CultureInfo.InvariantCulture) + " cm. One dynamic body; it collides as the hull shown.",
            };

            TableLayoutPanel grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6, AutoSize = false };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _systemName = new TextBox { Dock = DockStyle.Fill, Text = _defaults.SystemName };
            _bodyName = new TextBox { Dock = DockStyle.Fill, Text = _defaults.BodyName };
            _mass = Number(0.01m, 10000m, 2, (decimal)PhysicsSystemImporter.SuggestedMass(shape));
            _friction = Number(0m, 10m, 2, 0.5m);
            _restitution = Number(0m, 1m, 2, 0.4m);
            _placeAtModel = new CheckBox
            {
                Dock = DockStyle.Fill,
                Checked = _defaults.PlacementSource != null,
                Enabled = _defaults.PlacementSource != null,
                Text = _defaults.PlacementSource != null ? "Where '" + _defaults.PlacementSource + "' sits in the composite" : "No model in the composite to place it at",
            };

            AddRow(grid, 0, "System name", _systemName);
            AddRow(grid, 1, "Body name", _bodyName);
            AddRow(grid, 2, "Mass (kg)", _mass);
            AddRow(grid, 3, "Friction", _friction);
            AddRow(grid, 4, "Restitution", _restitution);
            AddRow(grid, 5, "Placement", _placeAtModel);

            FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36, FlowDirection = FlowDirection.RightToLeft };
            Button cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            Button create = new Button { Text = "Create System", DialogResult = DialogResult.OK, AutoSize = true };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(create);
            AcceptButton = create;
            CancelButton = cancel;

            Controls.Add(grid);
            Controls.Add(summary);
            Controls.Add(buttons);
            Theming.ThemeManager.ApplyToForm(this);
        }

        public string SystemName => _systemName.Text.Trim();

        /// <summary>The body the dialog describes, placed at the composite's model if that was left ticked.</summary>
        public PhysicsBodySettings Body => new PhysicsBodySettings
        {
            Name = _bodyName.Text.Trim(),
            Mass = (float)_mass.Value,
            Friction = (float)_friction.Value,
            Restitution = (float)_restitution.Value,
            Position = _placeAtModel.Checked ? _defaults.Position : System.Numerics.Vector3.Zero,
            Rotation = _placeAtModel.Checked ? _defaults.Rotation : System.Numerics.Quaternion.Identity,
        };

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            //The names go into the packfile as ASCII strings
            if (DialogResult == DialogResult.OK && (!IsAscii(SystemName) || !IsAscii(_bodyName.Text.Trim())))
            {
                MessageBox.Show(this, "Names can only use plain ASCII characters.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                e.Cancel = true;
            }
            base.OnFormClosing(e);
        }

        static bool IsAscii(string s)
        {
            foreach (char c in s) if (c < 32 || c > 126) return false;
            return true;
        }

        static NumericUpDown Number(decimal min, decimal max, int decimals, decimal value)
        {
            return new NumericUpDown
            {
                Dock = DockStyle.Left,
                Width = 110,
                Minimum = min,
                Maximum = max,
                DecimalPlaces = decimals,
                Increment = decimals > 0 ? 0.1m : 1m,
                Value = Math.Max(min, Math.Min(max, value)),
            };
        }

        static void AddRow(TableLayoutPanel grid, int row, string label, Control control)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            grid.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            grid.Controls.Add(control, 1, row);
        }
    }
}

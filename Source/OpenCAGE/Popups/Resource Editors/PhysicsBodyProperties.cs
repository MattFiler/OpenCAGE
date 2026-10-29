using CATHODE;
using CathodeLib.Havok;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// A rigid body's values that can be changed where it stands: its mass, its surface, how its movement dies away and
    /// how strongly gravity pulls it. Opens on the body's current values and gives back only the ones changed.
    /// </summary>
    public class PhysicsBodyProperties : Form
    {
        private readonly HavokPackfile.RigidBodyInfo _body;
        private readonly NumericUpDown _mass;
        private readonly NumericUpDown _friction;
        private readonly NumericUpDown _restitution;
        private readonly NumericUpDown _linearDamping;
        private readonly NumericUpDown _angularDamping;
        private readonly NumericUpDown _gravityFactor;
        private readonly bool _hasMass;

        public PhysicsBodyProperties(HavokPackfile.RigidBodyInfo body)
        {
            _body = body ?? throw new ArgumentNullException(nameof(body));
            //A keyframed or fixed body is moved by the game, not by physics: it has no mass (an inverse mass of 0) to change
            _hasMass = body.MassInv > 0f && !float.IsInfinity(body.Mass);

            Text = "Edit Rigid Body";
            Icon = SharedFormIcon.Icon;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(440, 300);
            Padding = new Padding(12);

            Label summary = new Label
            {
                Dock = DockStyle.Top,
                Height = 48,
                Text = (string.IsNullOrEmpty(body.Name) ? "(unnamed body)" : body.Name) + "  ·  " + (body.MotionTypeName ?? "") + "\n"
                    + (_hasMass ? "A new mass keeps the body's shape: its inertia scales with it." : "This body is moved by the game rather than by physics, so it has no mass to change."),
            };

            TableLayoutPanel grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6, AutoSize = false };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _mass = Number(0.001m, 100000m, 3, _hasMass ? (decimal)body.Mass : 0.001m);
            _mass.Enabled = _hasMass;
            _friction = Number(0m, 255m, 2, (decimal)body.Friction);
            _restitution = Number(0m, 1m, 2, (decimal)body.Restitution);
            _linearDamping = Number(0m, 1000m, 3, (decimal)body.LinearDamping);
            _angularDamping = Number(0m, 1000m, 3, (decimal)body.AngularDamping);
            _gravityFactor = Number(-100m, 100m, 2, (decimal)body.GravityFactor);

            AddRow(grid, 0, "Mass (kg)", _mass);
            AddRow(grid, 1, "Friction", _friction);
            AddRow(grid, 2, "Restitution", _restitution);
            AddRow(grid, 3, "Linear damping", _linearDamping);
            AddRow(grid, 4, "Angular damping", _angularDamping);
            AddRow(grid, 5, "Gravity factor", _gravityFactor);
            //The last row would take up any height left over, centring its label away from its box: give that to a spare row
            grid.RowCount = 7;
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36, FlowDirection = FlowDirection.RightToLeft };
            Button cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            Button apply = new Button { Text = "Apply", DialogResult = DialogResult.OK, AutoSize = true };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(apply);
            AcceptButton = apply;
            CancelButton = cancel;

            Controls.Add(grid);
            Controls.Add(summary);
            Controls.Add(buttons);
            Theming.ThemeManager.ApplyToForm(this);
        }

        /// <summary>The values the user changed (a box shows its value rounded, so a box left alone is not a change).</summary>
        public RigidBodyChange Change => new RigidBodyChange
        {
            Mass = _hasMass ? Changed(_mass, _body.Mass) : null,
            Friction = Changed(_friction, _body.Friction),
            Restitution = Changed(_restitution, _body.Restitution),
            LinearDamping = Changed(_linearDamping, _body.LinearDamping),
            AngularDamping = Changed(_angularDamping, _body.AngularDamping),
            GravityFactor = Changed(_gravityFactor, _body.GravityFactor),
        };

        static float? Changed(NumericUpDown box, float original)
        {
            decimal shown = Clamp(box, (decimal)original);
            return box.Value == shown ? (float?)null : (float)box.Value;
        }

        static decimal Clamp(NumericUpDown box, decimal value)
        {
            value = Math.Round(value, box.DecimalPlaces, MidpointRounding.AwayFromZero);
            return Math.Max(box.Minimum, Math.Min(box.Maximum, value));
        }

        static NumericUpDown Number(decimal min, decimal max, int decimals, decimal value)
        {
            NumericUpDown box = new NumericUpDown
            {
                Dock = DockStyle.Left,
                Width = 110,
                Minimum = min,
                Maximum = max,
                DecimalPlaces = decimals,
                Increment = decimals > 0 ? 0.1m : 1m,
            };
            box.Value = Clamp(box, value);
            return box;
        }

        static void AddRow(TableLayoutPanel grid, int row, string label, Control control)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            grid.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            grid.Controls.Add(control, 1, row);
        }
    }
}

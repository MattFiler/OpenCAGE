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
    public partial class PhysicsBodyProperties : Form
    {
        private readonly HavokPackfile.RigidBodyInfo _body;
        private readonly bool _hasMass;

        public PhysicsBodyProperties(HavokPackfile.RigidBodyInfo body)
        {
            _body = body ?? throw new ArgumentNullException(nameof(body));
            //A keyframed or fixed body is moved by the game, not by physics: it has no mass (an inverse mass of 0) to change
            _hasMass = body.MassInv > 0f && !float.IsInfinity(body.Mass);

            //The grid's last row is a spare that takes any height left over, which on the last real row would centre its label away from its box
            InitializeComponent();

            Icon = SharedFormIcon.Icon;

            _summary.Text = (string.IsNullOrEmpty(body.Name) ? "(unnamed body)" : body.Name) + "  ·  " + (body.MotionTypeName ?? "") + "\n"
                + (_hasMass ? "A new mass keeps the body's shape: its inertia scales with it." : "This body is moved by the game rather than by physics, so it has no mass to change.");

            _mass.Value = Clamp(_mass, _hasMass ? (decimal)body.Mass : 0.001m);
            _mass.Enabled = _hasMass;
            _friction.Value = Clamp(_friction, (decimal)body.Friction);
            _restitution.Value = Clamp(_restitution, (decimal)body.Restitution);
            _linearDamping.Value = Clamp(_linearDamping, (decimal)body.LinearDamping);
            _angularDamping.Value = Clamp(_angularDamping, (decimal)body.AngularDamping);
            _gravityFactor.Value = Clamp(_gravityFactor, (decimal)body.GravityFactor);

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
    }
}

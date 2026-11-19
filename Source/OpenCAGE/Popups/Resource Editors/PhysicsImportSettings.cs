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
    public partial class PhysicsImportSettings : Form
    {
        private readonly PhysicsSystemImporter.HostDefaults _defaults;

        public PhysicsImportSettings(ConvexBody shape, string sourceName, PhysicsSystemImporter.HostDefaults defaults)
        {
            _defaults = defaults ?? new PhysicsSystemImporter.HostDefaults();

            InitializeComponent();

            Icon = SharedFormIcon.Icon;

            _summary.Text = shape.SurfaceVertices.Count + "-corner convex hull of " + sourceName
                + (shape.FullHullVertices > shape.SurfaceVertices.Count ? " (simplified from " + shape.FullHullVertices + ")" : "")
                + "\nVolume " + (shape.Volume * 1000f).ToString("0.###", CultureInfo.InvariantCulture) + " L, convex radius "
                + (shape.ConvexRadius * 100f).ToString("0.##", CultureInfo.InvariantCulture) + " cm. One dynamic body; it collides as the hull shown.";

            _systemName.Text = _defaults.SystemName;
            _bodyName.Text = _defaults.BodyName;
            _mass.Value = Math.Max(_mass.Minimum, Math.Min(_mass.Maximum, (decimal)PhysicsSystemImporter.SuggestedMass(shape)));
            _placeAtModel.Checked = _defaults.PlacementSource != null;
            _placeAtModel.Enabled = _defaults.PlacementSource != null;
            _placeAtModel.Text = _defaults.PlacementSource != null ? "Where '" + _defaults.PlacementSource + "' sits in the composite" : "No model in the composite to place it at";

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
    }
}

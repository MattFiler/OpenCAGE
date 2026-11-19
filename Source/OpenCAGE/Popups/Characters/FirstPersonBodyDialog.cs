using CATHODE;
using CATHODE.Scripting;
using Newtonsoft.Json.Linq;
using OpenCAGE.Characters;
using OpenCAGE.MCP;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE.Popups.Characters
{
    /// <summary>
    /// "Make First-Person Body": make_first_person_body with a preview - the body front and side with what the cut takes in red - the rule's
    /// options, the new display model's name, and whether the player wears it.
    /// </summary>
    public partial class FirstPersonBodyDialog : Form
    {
        private readonly string _displayModel, _model;

        public FirstPersonBodyDialog(string displayModel, string model)
        {
            _displayModel = displayModel;
            _model = model;
            InitializeComponent();
            Text = "Make a first-person body from " + (displayModel ?? model);
            Icon = SharedFormIcon.Icon;
            string leaf = displayModel != null ? displayModel.Substring(DisplayModelShape.Prefix.Length) : Path.GetFileNameWithoutExtension((model ?? "").Replace('\\', '/'));
            _name.Text = DisplayModelShape.Prefix + leaf + "_FP";
            OpenCAGE.Theming.ThemeManager.ApplyToForm(this);
        }

        private async void FirstPersonBodyDialog_Shown(object sender, EventArgs e)
        {
            await Go(true);
        }

        private async void Preview_Click(object sender, EventArgs e)
        {
            await Go(true);
        }

        private async void Make_Click(object sender, EventArgs e)
        {
            await Go(false);
        }

        private async void Cut_CheckedChanged(object sender, EventArgs e)
        {
            await Go(true);
        }

        private async Task Go(bool dryRun)
        {
            JObject args = new JObject() { ["lenient"] = _lenient.Checked, ["keep_torso"] = _keepTorso.Checked, ["dry_run"] = dryRun, ["picture"] = dryRun };
            if (_displayModel != null) args["display_model"] = _displayModel;
            if (_model != null) args["model"] = _model;
            if (_rig.Text.Trim().Length != 0) args["rig"] = _rig.Text.Trim();
            if (!dryRun) { args["name"] = _name.Text.Trim(); args["set_player_model"] = _player.Checked; }
            _preview.Enabled = _make.Enabled = false;
            UseWaitCursor = true;
            _report.Text = dryRun ? "Working out the cut..." : "Making the body...";
            try
            {
                var r = await CharacterToolRunner.Run("make_first_person_body", args);
                _report.Text = (r.error ? "FAILED: " : "") + r.text;
                if (r.image != null) { _picture.Image?.Dispose(); _picture.Image = r.image; }
            }
            catch (Exception e) { _report.Text = "Failed: " + e.Message; }
            finally { _preview.Enabled = _make.Enabled = true; UseWaitCursor = false; }
        }

        /// <summary>The Model Editor's right-click: "Make First-Person Body..." on the model shown (as FractureWindow.AttachToModelEditor).</summary>
        public static void AttachToModelEditor(TreeView tree, Func<Models.CS2> model)
        {
            if (tree == null) return;
            ContextMenuStrip menu = tree.ContextMenuStrip ?? new ContextMenuStrip();
            ToolStripMenuItem item = new ToolStripMenuItem("Make First-Person Body...") { ToolTipText = "A copy of this body without the head, neck and upper torso, for the player's first-person view" };
            menu.Items.Add(item);
            menu.Opening += (s, e) =>
            {
                Models.CS2 m = model?.Invoke();
                item.Visible = m != null && m.Components.Any(c => c.LODs.Any(l => l.Submeshes.Any(sm => sm.Bones != null && sm.Bones.Count != 0)));
            };
            item.Click += (s, e) =>
            {
                Models.CS2 m = model?.Invoke();
                if (m != null) new FirstPersonBodyDialog(null, m.Name).Show();
            };
            tree.ContextMenuStrip = menu;
        }
    }
}

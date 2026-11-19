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
    /// "Make Variant": create_display_variant with its choices on screen - a tint picked with the colour dialog, a held part from a file on
    /// a bone, a name. Check runs it as a dry run. With heldOnly, add_held_part on the display model itself.
    /// </summary>
    public partial class DisplayVariantDialog : Form
    {
        private readonly string _source;
        private readonly bool _heldOnly;

        public DisplayVariantDialog(string source, bool heldOnly)
        {
            _source = source;
            _heldOnly = heldOnly;
            InitializeComponent();
            Text = heldOnly ? "Add a part skinned to " + source : "Make a variant of " + source;
            Icon = SharedFormIcon.Icon;
            _introLabel.Text = heldOnly
                ? "A part that follows one bone of " + source + "'s rig in every animation: the file is modelled about the bone (a weapon about the weapon bone, X down the barrel, as the game's own), placed by the offset below, weighted 100% to the bone and listed in the display model's animated model."
                : "A copy of " + source + " that works in game (the id its name hashes to, its own skeleton id and animated-model entry). Tinting copies the body parts' materials with DIFFUSE_TINT multiplied by the colour (the hit collision is left alone). A held part follows one bone of the rig. Wear it with display_model = the name after DisplayModel:.";
            //a part on the display model itself has no new name, tint or choice of holding it
            if (heldOnly)
            {
                _root.Controls.Remove(_nameRow);
                _root.Controls.Remove(_tintRow);
                _root.Controls.Remove(_heldOn);
                //an auto-sized panel only grows by itself: fit it to the rows left
                _root.Size = _root.PreferredSize;
            }
            _bone.Items.AddRange(HeldPart.CommonBones);
            _bone.Text = "RIGHTWEAPONBONE";
            string leaf = (source ?? "").StartsWith(DisplayModelShape.Prefix) ? source.Substring(DisplayModelShape.Prefix.Length) : source;
            _name.Text = DisplayModelShape.Prefix + leaf + "_VARIANT";
            _heldOn.Checked = heldOnly;
            //the materials the body parts draw with (never the hit collision's), all ticked: untick the skin to tint only the clothes
            try
            {
                CathodeLib.Level level = Singleton.Editor?.CompositeBrowser?.Content?.Level;
                Composite composite = level?.Commands.Entries.FirstOrDefault(o => o != null && o.name == source);
                if (composite != null)
                {
                    McpAssets.MaterialNames names = McpAssets.Names(level);
                    foreach (string material in DisplayModelShape.Parts(composite)
                        .Where(p => p.Renderable.RenderableInstance?.Any(e => e?.Model?.WeightedCollision != null) != true)
                        .SelectMany(p => p.Renderable.RenderableInstance ?? new System.Collections.Generic.List<RenderableElements.Element>())
                        .Where(e => e?.Material != null).Select(e => names.Ref(e.Material)).Distinct())
                        _materials.Items.Add(material, true);
                }
            }
            catch { }
            OpenCAGE.Theming.ThemeManager.ApplyToForm(this);
        }

        private void Tint_Click(object sender, EventArgs e)
        {
            using (ColorDialog pick = new ColorDialog() { Color = _tint.BackColor, FullOpen = true })
                if (pick.ShowDialog(this) == DialogResult.OK) _tint.BackColor = pick.Color;
        }

        private void Browse_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog open = new OpenFileDialog() { Filter = "Models (*.fbx;*.gltf;*.glb;*.obj;*.dae)|*.fbx;*.gltf;*.glb;*.obj;*.dae|All files|*.*" })
                if (open.ShowDialog(this) == DialogResult.OK) { _file.Text = open.FileName; _heldOn.Checked = true; }
        }

        private async void Check_Click(object sender, EventArgs e)
        {
            await Go(true);
        }

        private async void Make_Click(object sender, EventArgs e)
        {
            await Go(false);
        }

        private JObject HeldArgs()
        {
            return new JObject()
            {
                ["path"] = _file.Text.Trim(),
                ["bone"] = _bone.Text.Trim(),
                ["part_name"] = _partName.Text.Trim(),
                ["scale"] = (double)_scale.Value,
                ["position"] = new JArray((double)_px.Value, (double)_py.Value, (double)_pz.Value),
                ["rotation"] = new JArray((double)_rx.Value, (double)_ry.Value, (double)_rz.Value),
            };
        }

        private async Task Go(bool dryRun)
        {
            string tool;
            JObject args;
            if (_heldOnly)
            {
                tool = "add_held_part";
                args = HeldArgs();
                args["display_model"] = _source;
                if (dryRun) args["dry_run"] = true;
            }
            else
            {
                tool = "create_display_variant";
                args = new JObject() { ["source"] = _source, ["name"] = _name.Text.Trim(), ["dry_run"] = dryRun };
                if (_tintOn.Checked) args["tint"] = new JArray(_tint.BackColor.R / 255.0, _tint.BackColor.G / 255.0, _tint.BackColor.B / 255.0);
                if (_tintOn.Checked && _materials.Items.Count != 0 && _materials.CheckedItems.Count != _materials.Items.Count) args["tint_materials"] = new JArray(_materials.CheckedItems.Cast<object>().Select(o => o.ToString()));
                if (_heldOn.Checked && _file.Text.Trim().Length != 0) args["held_part"] = HeldArgs();
            }
            _check.Enabled = _make.Enabled = false;
            UseWaitCursor = true;
            _report.Text = dryRun ? "Checking..." : "Making...";
            try
            {
                var r = await CharacterToolRunner.Run(tool, args);
                _report.Text = (r.error ? "FAILED: " : "") + r.text;
            }
            catch (Exception e) { _report.Text = "Failed: " + e.Message; }
            finally { _check.Enabled = _make.Enabled = true; UseWaitCursor = false; }
        }
    }
}

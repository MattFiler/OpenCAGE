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
    /// Runs one of the character tools (McpDisplayModelTools) off the UI thread, as any tool call runs, and hands back its text and image.
    /// </summary>
    internal static class CharacterToolRunner
    {
        public static async Task<(bool error, string text, Image image)> Run(string toolName, JObject args)
        {
            McpTool tool = McpTools.Find(toolName);
            if (tool == null) return (true, "The " + toolName + " tool is missing from this build.", null);
            JObject result = await Task.Run(() =>
            {
                using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMinutes(20)))
                    return tool.Invoke(new McpCall(tool, args, cts.Token, null));
            });
            JArray content = (JArray)result["content"] ?? new JArray();
            string text = string.Concat(content.Where(c => (string)c["type"] == "text").Select(c => (string)c["text"]));
            try { text = JToken.Parse(text).ToString(Newtonsoft.Json.Formatting.Indented); } catch { }
            Image image = null;
            JToken png = content.FirstOrDefault(c => (string)c["type"] == "image");
            if (png != null)
            {
                try { image = Image.FromStream(new MemoryStream(Convert.FromBase64String((string)png["data"]))); } catch { image = null; }
            }
            return ((bool?)result["isError"] == true, text.Replace("\n", Environment.NewLine), image);
        }

        public static FlowLayoutPanel Row(params Control[] controls)
        {
            FlowLayoutPanel row = new FlowLayoutPanel() { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false };
            row.Controls.AddRange(controls);
            return row;
        }

        public static Label Caption(string text) => new Label() { Text = text, AutoSize = true, Margin = new Padding(3, 6, 3, 0) };

        public static NumericUpDown Number(decimal min, decimal max, decimal value, int decimals, decimal step) =>
            new NumericUpDown() { Minimum = min, Maximum = max, DecimalPlaces = decimals, Increment = step, Value = value, Width = 64 };
    }

    /// <summary>
    /// The Composite Browser's right-click on a "DisplayModel:" composite: the character tools for it. Call from the browser's menu
    /// opening, as ReplaceWeaponModel.PrepareMenu is: the items show only for a display model.
    /// </summary>
    public static class DisplayModelMenu
    {
        public static void PrepareMenu(ContextMenuStrip menu, Composite composite)
        {
            if (menu == null) return;
            ToolStripMenuItem root = menu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(o => (o.Name ?? "") == "displayModelToolsMenuItem");
            if (root == null)
            {
                root = new ToolStripMenuItem("Character Look") { Name = "displayModelToolsMenuItem" };
                ToolStripMenuItem variant = new ToolStripMenuItem("Make Variant (tint, held part)...") { ToolTipText = "A copy of this look that works in game: tinted, and/or holding a part skinned to its rig" };
                ToolStripMenuItem held = new ToolStripMenuItem("Add Part Skinned To This Rig...") { ToolTipText = "A prop that follows one bone - a gun in the hand, a lamp on the head" };
                ToolStripMenuItem firstPerson = new ToolStripMenuItem("Make First-Person Body...") { ToolTipText = "Cut the head, neck and upper torso off for the player's view" };
                ToolStripMenuItem player = new ToolStripMenuItem("Use As Player Model") { ToolTipText = "Global's FlightSuit registers this display model (the player wears it in first person)" };
                ToolStripMenuItem check = new ToolStripMenuItem("Check / Fix Shape") { ToolTipText = "Ids, skeleton and animated-model entry as the game needs them" };
                variant.Click += (o, e) => new DisplayVariantDialog(root.Tag as string, false).Show();
                held.Click += (o, e) => new DisplayVariantDialog(root.Tag as string, true).Show();
                firstPerson.Click += (o, e) => new FirstPersonBodyDialog(root.Tag as string, null).Show();
                player.Click += async (o, e) =>
                {
                    var r = await CharacterToolRunner.Run("set_player_model", new JObject() { ["display_model"] = root.Tag as string });
                    MessageBox.Show(r.text, r.error ? "Not set" : "Player model set", MessageBoxButtons.OK, r.error ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                };
                check.Click += async (o, e) =>
                {
                    var r = await CharacterToolRunner.Run("check_display_models", new JObject() { ["composite"] = root.Tag as string });
                    if (!r.error && r.text.Contains("\"ok\": false") && MessageBox.Show(r.text + Environment.NewLine + Environment.NewLine + "Put it right? (one undo step)", "Display model shape", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        r = await CharacterToolRunner.Run("check_display_models", new JObject() { ["composite"] = root.Tag as string, ["fix"] = true });
                    MessageBox.Show(r.text, "Display model shape", MessageBoxButtons.OK, MessageBoxIcon.Information);
                };
                root.DropDownItems.AddRange(new ToolStripItem[] { variant, held, firstPerson, new ToolStripSeparator(), player, check });
                menu.Items.Add(root);
            }
            bool show = DisplayModelShape.IsDisplayModel(composite);
            root.Visible = show;
            root.Tag = show ? composite.name : null;
        }
    }

    /// <summary>
    /// "Make Variant": create_display_variant with its choices on screen - a tint picked with the colour dialog, a held part from a file on
    /// a bone, a name. Check runs it as a dry run. With heldOnly, add_held_part on the display model itself.
    /// </summary>
    public class DisplayVariantDialog : Form
    {
        private readonly string _source;
        private readonly bool _heldOnly;
        private readonly TextBox _name = new TextBox() { Width = 300 };
        private readonly CheckBox _tintOn = new CheckBox() { Text = "Tint it", AutoSize = true, Checked = true };
        private readonly Button _tint = new Button() { Width = 60, BackColor = Color.FromArgb(255, 64, 51), FlatStyle = FlatStyle.Flat };
        private readonly CheckBox _heldOn = new CheckBox() { Text = "Hold a part skinned to its rig", AutoSize = true };
        private readonly CheckedListBox _materials = new CheckedListBox() { Width = 300, Height = 64, CheckOnClick = true };
        private readonly TextBox _file = new TextBox() { Width = 380 };
        private readonly Button _browse = new Button() { Text = "...", Width = 30 };
        private readonly ComboBox _bone = new ComboBox() { Width = 170 };
        private readonly TextBox _partName = new TextBox() { Width = 120, Text = "HELD", CharacterCasing = CharacterCasing.Upper };
        private readonly NumericUpDown _px = CharacterToolRunner.Number(-2, 2, 0, 3, 0.01m), _py = CharacterToolRunner.Number(-2, 2, 0, 3, 0.01m), _pz = CharacterToolRunner.Number(-2, 2, 0, 3, 0.01m);
        private readonly NumericUpDown _rx = CharacterToolRunner.Number(-360, 360, 0, 1, 5), _ry = CharacterToolRunner.Number(-360, 360, 0, 1, 5), _rz = CharacterToolRunner.Number(-360, 360, 0, 1, 5);
        private readonly NumericUpDown _scale = CharacterToolRunner.Number(0.01m, 100, 1, 3, 0.05m);
        private readonly TextBox _report = new TextBox() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Width = 680, Height = 220 };
        private readonly Button _check = new Button() { Text = "Check", AutoSize = true };
        private readonly Button _make = new Button() { Text = "Make", AutoSize = true };
        private readonly Button _close = new Button() { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel };

        public DisplayVariantDialog(string source, bool heldOnly)
        {
            _source = source;
            _heldOnly = heldOnly;
            Text = heldOnly ? "Add a part skinned to " + source : "Make a variant of " + source;
            Icon = SharedFormIcon.Icon;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            CancelButton = _close;
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

            FlowLayoutPanel root = new FlowLayoutPanel() { FlowDirection = FlowDirection.TopDown, AutoSize = true, Padding = new Padding(10), WrapContents = false };
            root.Controls.Add(new Label() { AutoSize = true, MaximumSize = new Size(680, 0), Text = heldOnly
                ? "A part that follows one bone of " + source + "'s rig in every animation: the file is modelled about the bone (a weapon about the weapon bone, X down the barrel, as the game's own), placed by the offset below, weighted 100% to the bone and listed in the display model's animated model."
                : "A copy of " + source + " that works in game (the id its name hashes to, its own skeleton id and animated-model entry). Tinting copies the body parts' materials with DIFFUSE_TINT multiplied by the colour (the hit collision is left alone). A held part follows one bone of the rig. Wear it with display_model = the name after DisplayModel:." });
            if (!heldOnly)
            {
                root.Controls.Add(CharacterToolRunner.Row(CharacterToolRunner.Caption("New display model:"), _name));
                root.Controls.Add(CharacterToolRunner.Row(_tintOn, _tint, CharacterToolRunner.Caption("  materials to tint:"), _materials));
                root.Controls.Add(_heldOn);
            }
            root.Controls.Add(CharacterToolRunner.Row(CharacterToolRunner.Caption("Part file:"), _file, _browse));
            root.Controls.Add(CharacterToolRunner.Row(CharacterToolRunner.Caption("Bone:"), _bone, CharacterToolRunner.Caption("  Part name:"), _partName, CharacterToolRunner.Caption("  Scale:"), _scale));
            root.Controls.Add(CharacterToolRunner.Row(CharacterToolRunner.Caption("Offset (m):"), _px, _py, _pz, CharacterToolRunner.Caption("  Rotation (deg):"), _rx, _ry, _rz));
            root.Controls.Add(_report);
            root.Controls.Add(CharacterToolRunner.Row(_check, _make, _close));
            Controls.Add(root);

            _tint.Click += (s, e) =>
            {
                using (ColorDialog pick = new ColorDialog() { Color = _tint.BackColor, FullOpen = true })
                    if (pick.ShowDialog(this) == DialogResult.OK) _tint.BackColor = pick.Color;
            };
            _browse.Click += (s, e) =>
            {
                using (OpenFileDialog open = new OpenFileDialog() { Filter = "Models (*.fbx;*.gltf;*.glb;*.obj;*.dae)|*.fbx;*.gltf;*.glb;*.obj;*.dae|All files|*.*" })
                    if (open.ShowDialog(this) == DialogResult.OK) { _file.Text = open.FileName; _heldOn.Checked = true; }
            };
            _check.Click += async (s, e) => await Go(true);
            _make.Click += async (s, e) => await Go(false);
            OpenCAGE.Theming.ThemeManager.ApplyToForm(this);
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

    /// <summary>
    /// "Make First-Person Body": make_first_person_body with a preview - the body front and side with what the cut takes in red - the rule's
    /// options, the new display model's name, and whether the player wears it.
    /// </summary>
    public class FirstPersonBodyDialog : Form
    {
        private readonly string _displayModel, _model;
        private readonly PictureBox _picture = new PictureBox() { Width = 900, Height = 620, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(28, 30, 34) };
        private readonly TextBox _name = new TextBox() { Width = 300 };
        private readonly TextBox _rig = new TextBox() { Width = 120, CharacterCasing = CharacterCasing.Upper };
        private readonly CheckBox _lenient = new CheckBox() { Text = "Keep triangles with one corner in the cut (low-polygon bodies)", AutoSize = true };
        private readonly CheckBox _keepTorso = new CheckBox() { Text = "Keep the whole torso", AutoSize = true };
        private readonly CheckBox _player = new CheckBox() { Text = "Make it the player's model (Global's FlightSuit)", AutoSize = true, Checked = true };
        private readonly TextBox _report = new TextBox() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Width = 900, Height = 150 };
        private readonly Button _preview = new Button() { Text = "Preview", AutoSize = true };
        private readonly Button _make = new Button() { Text = "Make", AutoSize = true };
        private readonly Button _close = new Button() { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel };

        public FirstPersonBodyDialog(string displayModel, string model)
        {
            _displayModel = displayModel;
            _model = model;
            Text = "Make a first-person body from " + (displayModel ?? model);
            Icon = SharedFormIcon.Icon;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            CancelButton = _close;
            string leaf = displayModel != null ? displayModel.Substring(DisplayModelShape.Prefix.Length) : Path.GetFileNameWithoutExtension((model ?? "").Replace('\\', '/'));
            _name.Text = DisplayModelShape.Prefix + leaf + "_FP";
            FlowLayoutPanel root = new FlowLayoutPanel() { FlowDirection = FlowDirection.TopDown, AutoSize = true, Padding = new Padding(10), WrapContents = false };
            root.Controls.Add(new Label() { AutoSize = true, MaximumSize = new Size(900, 0), Text = "A copy of the body without what the player's camera (the HEAD bone, green) would see from inside: the head, neck and clavicles, and the upper torso except a front panel - as the game's own first-person bodies are cut. Red is what goes. The player's rig is FEMALEFP: rebind a body onto it first (Model import > Rebind onto a game skeleton)." });
            root.Controls.Add(_picture);
            root.Controls.Add(CharacterToolRunner.Row(CharacterToolRunner.Caption("New display model:"), _name, CharacterToolRunner.Caption("  Rig (empty = the display model's):"), _rig));
            root.Controls.Add(CharacterToolRunner.Row(_lenient, _keepTorso, _player));
            root.Controls.Add(_report);
            root.Controls.Add(CharacterToolRunner.Row(_preview, _make, _close));
            Controls.Add(root);
            _preview.Click += async (s, e) => await Go(true);
            _make.Click += async (s, e) => await Go(false);
            _lenient.CheckedChanged += async (s, e) => await Go(true);
            _keepTorso.CheckedChanged += async (s, e) => await Go(true);
            Shown += async (s, e) => await Go(true);
            OpenCAGE.Theming.ThemeManager.ApplyToForm(this);
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

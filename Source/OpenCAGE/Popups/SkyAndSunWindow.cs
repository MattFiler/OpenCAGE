using Newtonsoft.Json.Linq;
using OpenCAGE.Building;
using OpenCAGE.MCP;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE.Popups
{
    /// <summary>
    /// Galaxy Editor > Sky and Outdoor Lighting: a sky dome from a panorama, and a sun, sky fill and bounce for an outdoor area. Each button
    /// runs the matching MCP tool (add_sky_dome, add_outdoor_lighting) on a worker thread; Check / Plan change nothing.
    /// </summary>
    public class SkyAndSunWindow : BaseWindow
    {
        private static SkyAndSunWindow _open;

        public static SkyAndSunWindow Open()
        {
            if (_open == null || _open.IsDisposed)
            {
                _open = new SkyAndSunWindow();
                _open.Show();
            }
            else
                _open.BringToFront();
            return _open;
        }

        // ---- Sky dome
        private readonly TextBox _picture = new TextBox() { Width = 380 };
        private readonly Button _browse = new Button() { Text = "Browse...", AutoSize = true };
        private readonly Button _faces = new Button() { Text = "Six Faces...", AutoSize = true };
        private readonly ComboBox _layout = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        private readonly ComboBox _resolution = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
        private readonly CheckBox _autoCentre = new CheckBox() { Text = "Middle of the play area", AutoSize = true, Checked = true };
        private readonly NumericUpDown _cx = Num(-100000, 100000, 2), _cy = Num(-100000, 100000, 2), _cz = Num(-100000, 100000, 2);
        private readonly CheckBox _autoRadius = new CheckBox() { Text = "As big as the far plane allows", AutoSize = true, Checked = true };
        private readonly NumericUpDown _radius = Num(20, 5000, 1, 900);
        private readonly TrackBar _yawBar = new TrackBar() { Minimum = -180, Maximum = 180, TickFrequency = 45, Width = 260 };
        private readonly NumericUpDown _yaw = Num(-180, 180, 1);
        private readonly TrackBar _brightBar = new TrackBar() { Minimum = 0, Maximum = 100, TickFrequency = 10, Width = 260, Value = 50 };
        private readonly NumericUpDown _brightness = Num(0, 20, 3, 0.3m);
        private readonly Button _tint = new Button() { Text = "Tint", Width = 80, BackColor = Color.White };
        private readonly ComboBox _below = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
        private readonly CheckBox _domeReflections = new CheckBox() { Text = "Show in reflections (water, puddles)", AutoSize = true, Checked = true };
        private readonly CheckBox _unlit = new CheckBox() { Text = "Draw it unlit, after the lighting (no shading, no screen-space effects on it)", AutoSize = true, Checked = true };
        private readonly CheckBox _removeFog = new CheckBox() { Text = "Remove the level's distance fog (it paints over the dome)", AutoSize = true };
        private readonly PictureBox _preview = new PictureBox() { Width = 512, Height = 256, SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.Black };
        private readonly Button _check = new Button() { Text = "Check", AutoSize = true };
        private readonly Button _addDome = new Button() { Text = "Add / Update Sky Dome", AutoSize = true };
        private readonly Button _removeDome = new Button() { Text = "Remove Sky Dome", AutoSize = true };
        private readonly TextBox _domeReport = Report();
        private Dictionary<string, string> _faceFiles;

        // ---- Outdoor lighting
        private readonly TextBox _name = new TextBox() { Width = 120, Text = "Main" };
        private readonly ComboBox _area = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        private readonly NumericUpDown _x0 = Num(-100000, 100000, 1, -50), _y0 = Num(-100000, 100000, 1, -5), _z0 = Num(-100000, 100000, 1, -50);
        private readonly NumericUpDown _x1 = Num(-100000, 100000, 1, 50), _y1 = Num(-100000, 100000, 1, 30), _z1 = Num(-100000, 100000, 1, 50);
        private readonly CheckBox _sunFromSky = new CheckBox() { Text = "From the sky dome's sun", AutoSize = true, Checked = true };
        private readonly NumericUpDown _azimuth = Num(-360, 360, 1, 150), _elevation = Num(1, 90, 1, 40);
        private readonly Button _sunColour = new Button() { Text = "Colour", Width = 80, BackColor = Color.FromArgb(255, 244, 222) };
        private readonly CheckBox _sunColourFromSky = new CheckBox() { Text = "from the sky", AutoSize = true, Checked = true };
        private readonly NumericUpDown _sunIntensity = Num(0, 100, 2, 6);
        private readonly ComboBox _sunMode = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
        private readonly Button _skyColour = new Button() { Text = "Colour", Width = 80, BackColor = Color.FromArgb(170, 196, 255) };
        private readonly CheckBox _skyColourFromSky = new CheckBox() { Text = "from the sky", AutoSize = true, Checked = true };
        private readonly NumericUpDown _skyIntensity = Num(0, 50, 2, 1.2m);
        private readonly NumericUpDown _spacing = Num(4, 100, 0, 12);
        private readonly NumericUpDown _leak = Num(0.01m, 1, 2, 0.1m);
        private readonly NumericUpDown _reach = Num(5, 200, 0, 30);
        private readonly NumericUpDown _bounce = Num(0, 5, 2, 0.5m);
        private readonly CheckBox _lightReflections = new CheckBox() { Text = "Light reflections too (water, puddles)", AutoSize = true, Checked = true };
        private readonly PictureBox _map = new PictureBox() { Width = 360, Height = 360, SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.Black };
        private readonly Button _plan = new Button() { Text = "Plan", AutoSize = true };
        private readonly Button _applyLights = new Button() { Text = "Apply", AutoSize = true };
        private readonly Button _removeLights = new Button() { Text = "Remove", AutoSize = true };
        private readonly TextBox _lightReport = Report();

        //sized once the window has its own size (a SplitterDistance set before that is clamped to the default 150 px)
        private readonly SplitContainer _skySplit = new SplitContainer() { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        private readonly SplitContainer _lightColumns = new SplitContainer() { Dock = DockStyle.Fill };
        private readonly SplitContainer _lightSplit = new SplitContainer() { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        private readonly Label _status = new Label() { AutoSize = true, Padding = new Padding(6, 4, 0, 4) };
        private bool _busy;

        private static NumericUpDown Num(decimal min, decimal max, int decimals, decimal value = 0) =>
            new NumericUpDown() { Minimum = min, Maximum = max, DecimalPlaces = decimals, Value = Math.Max(min, Math.Min(max, value)), Width = 80, Increment = decimals == 0 ? 1 : (decimal)Math.Pow(10, -Math.Min(decimals, 2)) * 10 };

        private static TextBox Report() => new TextBox() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font(FontFamily.GenericMonospace, 8.5f), WordWrap = true };

        public SkyAndSunWindow() : base(WindowClosesOn.COMMANDS_RELOAD)
        {
            Text = "Sky and Outdoor Lighting";
            Icon = SharedFormIcon.Icon;
            Size = new Size(1120, 900);
            MinimumSize = new Size(820, 640);
            StartPosition = FormStartPosition.CenterScreen;
            EnableDocumentation("https://opencage.co.uk/docs/sky-and-outdoor-lighting/");

            TabControl tabs = new TabControl() { Dock = DockStyle.Fill };
            TabPage skyTab = new TabPage("Sky Dome");
            TabPage lightTab = new TabPage("Outdoor Lighting");
            tabs.TabPages.Add(skyTab);
            tabs.TabPages.Add(lightTab);
            BuildSkyTab(skyTab);
            BuildLightTab(lightTab);
            Controls.Add(tabs);
            Controls.Add(_status);
            _status.Dock = DockStyle.Bottom;
            void SizeSplits()
            {
                foreach (SplitContainer split in new[] { _skySplit, _lightSplit })
                    if (split.Height > 420) try { split.SplitterDistance = split.Height - 210; } catch (InvalidOperationException) { }
                if (_lightColumns.Width > 600) try { _lightColumns.SplitterDistance = Math.Max(560, _lightColumns.Width - 380); } catch (InvalidOperationException) { }
            }
            Load += (s, e) => SizeSplits();
            tabs.SelectedIndexChanged += (s, e) => SizeSplits();
            _status.Text = "Check and Plan only report; Add and Apply change the level (one undo step each, Save and Build to see it in game).";
        }

        #region Layout
        private static FlowLayoutPanel Row(params Control[] controls)
        {
            FlowLayoutPanel row = new FlowLayoutPanel() { AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
            foreach (Control c in controls) { c.Margin = new Padding(3, 3, 3, 3); row.Controls.Add(c); }
            return row;
        }

        private static Label L(string text, int width = 0) => width > 0 ? new Label() { Text = text, Width = width, Padding = new Padding(0, 4, 0, 0) } : new Label() { Text = text, AutoSize = true, Padding = new Padding(0, 4, 0, 0) };

        private static Label Note(string text) => new Label() { Text = text, AutoSize = true, MaximumSize = new Size(900, 0), ForeColor = SystemColors.GrayText, Padding = new Padding(3, 0, 0, 2) };

        private void BuildSkyTab(TabPage tab)
        {
            _layout.Items.AddRange(new object[] { "Auto (from its shape)", "Equirectangular (2:1)", "Cross (4:3)", "Vertical cross (3:4)", "Strip of six (6:1)", "Column of six (1:6)" });
            _layout.SelectedIndex = 0;
            _resolution.Items.AddRange(new object[] { "2048", "4096", "8192" });
            _resolution.SelectedIndex = 1;
            _below.Items.AddRange(new object[] { "Keep the picture below the horizon", "Mirror the sky (a sea to the horizon)" });
            _below.SelectedIndex = 0;

            FlowLayoutPanel left = new FlowLayoutPanel() { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Dock = DockStyle.Fill, Padding = new Padding(6) };
            left.Controls.Add(Note("A panorama picture becomes a dome round the level, glowing with its picture: equirectangular (2:1), a cube map as a cross or a strip, a DDS cube map, or six face pictures. Its middle faces +Z."));
            left.Controls.Add(Row(L("Picture:", 90), _picture, _browse, _faces));
            left.Controls.Add(Row(L("Layout:", 90), _layout, L("Width:"), _resolution));
            left.Controls.Add(Row(L("Centre:", 90), _autoCentre, _cx, _cy, _cz));
            left.Controls.Add(Row(L("Radius (m):", 90), _autoRadius, _radius));
            left.Controls.Add(Note("The dome must sit inside the camera's 1000 m far plane from wherever the camera goes (its far side draws black past it): the automatic radius is the largest that does."));
            left.Controls.Add(Row(L("Turn (deg):", 90), _yawBar, _yaw));
            left.Controls.Add(Row(L("Brightness:", 90), _brightBar, _brightness, _tint));
            left.Controls.Add(Row(L("Below:", 90), _below));
            left.Controls.Add(Row(_domeReflections));
            left.Controls.Add(Row(_unlit));
            left.Controls.Add(Row(_removeFog));
            left.Controls.Add(Row(_check, _addDome, _removeDome));
            left.Controls.Add(_preview);
            SplitContainer split = _skySplit;
            split.Panel1.Controls.Add(left);
            split.Panel2.Controls.Add(_domeReport);
            tab.Controls.Add(split);

            _autoCentre.CheckedChanged += (s, e) => { _cx.Enabled = _cy.Enabled = _cz.Enabled = !_autoCentre.Checked; };
            _autoRadius.CheckedChanged += (s, e) => _radius.Enabled = !_autoRadius.Checked;
            _cx.Enabled = _cy.Enabled = _cz.Enabled = false;
            _radius.Enabled = false;
            _yawBar.ValueChanged += (s, e) => { if (_yaw.Value != _yawBar.Value) _yaw.Value = _yawBar.Value; };
            _yaw.ValueChanged += (s, e) => { int v = (int)Math.Round(_yaw.Value); if (_yawBar.Value != v) _yawBar.Value = Math.Max(-180, Math.Min(180, v)); };
            //brightness on a log slider: 0 -> 0.01, 50 -> 0.3, 100 -> 5
            _brightBar.ValueChanged += (s, e) => { decimal v = (decimal)Math.Round(BrightOf(_brightBar.Value), 3); if (Math.Abs(_brightness.Value - v) > 0.0005m) _brightness.Value = Math.Max(_brightness.Minimum, Math.Min(_brightness.Maximum, v)); };
            _brightness.ValueChanged += (s, e) => { int v = SliderOf((double)_brightness.Value); if (_brightBar.Value != v) _brightBar.Value = v; };
            _tint.Click += (s, e) => PickColour(_tint);
            _browse.Click += (s, e) => BrowsePicture();
            _faces.Click += (s, e) => BrowseFaces();
            _check.Click += (s, e) => RunDome(preview: true);
            _addDome.Click += (s, e) => RunDome(preview: false);
            _removeDome.Click += (s, e) => Run("add_sky_dome", new JObject() { ["remove"] = true }, _domeReport, null);
        }

        private static double BrightOf(int slider) => slider <= 50 ? 0.01 * Math.Pow(30, slider / 50.0) : 0.3 * Math.Pow(5 / 0.3, (slider - 50) / 50.0);
        private static int SliderOf(double v) => v <= 0.01 ? 0 : v <= 0.3 ? (int)Math.Round(50 * Math.Log(v / 0.01) / Math.Log(30)) : Math.Min(100, (int)Math.Round(50 + 50 * Math.Log(v / 0.3) / Math.Log(5 / 0.3)));

        private void BuildLightTab(TabPage tab)
        {
            _area.Items.AddRange(new object[] { "The whole level", "A box (world, m)" });
            _area.SelectedIndex = 0;
            _sunMode.Items.AddRange(new object[] { "Automatic (spot, or patches by rooms in a big area)", "Shadowed sun spot (roofs shadow it)", "Sun patches on the floors (no shadow maps)", "No sun" });
            _sunMode.SelectedIndex = 0;

            FlowLayoutPanel left = new FlowLayoutPanel() { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Dock = DockStyle.Fill, Padding = new Padding(6) };
            left.Controls.Add(Note("A sun, a sky fill that keeps out of rooms, and sun bounce for an outdoor area, read from the level's collision. All of it goes in the composite Lighting\\Outdoor_<name>; applying the same name again replaces it."));
            left.Controls.Add(Row(L("Name:", 90), _name, L("Area:"), _area));
            left.Controls.Add(Row(L("Box min:", 90), _x0, _y0, _z0, L("max:"), _x1, _y1, _z1));
            left.Controls.Add(Row(L("Sun:", 90), _sunFromSky, L("or azimuth"), _azimuth, L("elevation"), _elevation));
            left.Controls.Add(Row(L("", 90), _sunColour, _sunColourFromSky, L("intensity"), _sunIntensity));
            left.Controls.Add(Row(L("Sun light:", 90), _sunMode));
            left.Controls.Add(Row(L("Sky fill:", 90), _skyColour, _skyColourFromSky, L("on open ground"), _skyIntensity));
            left.Controls.Add(Row(L("", 90), L("one light per"), _spacing, L("m, reach up to"), _reach, L("m, at most"), _leak, L("into rooms")));
            left.Controls.Add(Row(L("Bounce:", 90), _bounce, L("(sun off sunlit ground, in the fill's colour)")));
            left.Controls.Add(Row(_lightReflections));
            left.Controls.Add(Row(_plan, _applyLights, _removeLights));
            left.Controls.Add(Note("Map: rooms red, covered yellow / teal, open green (lighter where sunlit), fills white with their reach, sun patches yellow squares, the sun gold."));
            SplitContainer top = _lightColumns;
            top.Panel1.Controls.Add(left);
            top.Panel2.Controls.Add(_map);
            _map.Dock = DockStyle.Fill;
            SplitContainer split = _lightSplit;
            split.Panel1.Controls.Add(top);
            split.Panel2.Controls.Add(_lightReport);
            tab.Controls.Add(split);

            void Gate()
            {
                bool box = _area.SelectedIndex == 1;
                _x0.Enabled = _y0.Enabled = _z0.Enabled = _x1.Enabled = _y1.Enabled = _z1.Enabled = box;
                _azimuth.Enabled = _elevation.Enabled = !_sunFromSky.Checked;
                _sunColour.Enabled = !_sunColourFromSky.Checked;
                _skyColour.Enabled = !_skyColourFromSky.Checked;
            }
            _area.SelectedIndexChanged += (s, e) => Gate();
            _sunFromSky.CheckedChanged += (s, e) => Gate();
            _sunColourFromSky.CheckedChanged += (s, e) => Gate();
            _skyColourFromSky.CheckedChanged += (s, e) => Gate();
            Gate();
            _sunColour.Click += (s, e) => PickColour(_sunColour);
            _skyColour.Click += (s, e) => PickColour(_skyColour);
            _plan.Click += (s, e) => RunLights(preview: true);
            _applyLights.Click += (s, e) => RunLights(preview: false);
            _removeLights.Click += (s, e) => Run("add_outdoor_lighting", new JObject() { ["name"] = _name.Text.Trim(), ["remove"] = true }, _lightReport, null);
        }

        private void PickColour(Button button)
        {
            using (ColorDialog dialog = new ColorDialog() { Color = button.BackColor, FullOpen = true })
                if (dialog.ShowDialog(this) == DialogResult.OK) button.BackColor = dialog.Color;
        }

        private static JArray Rgb(Color c) => new JArray(c.R, c.G, c.B);
        #endregion

        #region Picture choice
        private void BrowsePicture()
        {
            using (OpenFileDialog dialog = new OpenFileDialog() { Title = "Panorama picture", Filter = "Pictures|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.tga;*.hdr;*.dds|All files|*.*" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                _picture.Text = dialog.FileName;
                _faceFiles = null;
            }
        }

        /// <summary>Six files, sorted onto the faces by the words in their names (right/left/up/down/front/back, posx/negx..., px/nx..., rt/lf/up/dn/ft/bk).</summary>
        private void BrowseFaces()
        {
            using (OpenFileDialog dialog = new OpenFileDialog() { Title = "Six cube face pictures", Multiselect = true, Filter = "Pictures|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.tga;*.hdr;*.dds|All files|*.*" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                Dictionary<string, string[]> words = new Dictionary<string, string[]>()
                {
                    ["right"] = new[] { "right", "posx", "px", "rt", "+x" },
                    ["left"] = new[] { "left", "negx", "nx", "lf", "-x" },
                    ["up"] = new[] { "up", "top", "posy", "py", "+y" },
                    ["down"] = new[] { "down", "bottom", "negy", "ny", "dn", "-y" },
                    ["front"] = new[] { "front", "posz", "pz", "ft", "+z" },
                    ["back"] = new[] { "back", "negz", "nz", "bk", "-z" },
                };
                Dictionary<string, string> found = new Dictionary<string, string>();
                foreach (string file in dialog.FileNames)
                {
                    string stem = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                    string[] parts = stem.Split('_', '-', ' ', '.');
                    foreach (var face in words)
                        if (!found.ContainsKey(face.Key) && face.Value.Any(w => parts.Contains(w) || stem.EndsWith(w)))
                        {
                            found[face.Key] = file;
                            break;
                        }
                }
                if (found.Count != 6)
                {
                    MessageBox.Show(this, "Could not tell which face each picture is (found " + string.Join(", ", found.Keys) + "). Name them with right/left/up/down/front/back (or posx/negx/posy/negy/posz/negz).", "Six Faces", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                _faceFiles = found;
                _picture.Text = "(six faces) " + string.Join(", ", found.Select(o => o.Key + ": " + Path.GetFileName(o.Value)));
            }
        }
        #endregion

        #region Running
        private void RunDome(bool preview)
        {
            JObject args = new JObject();
            if (_faceFiles != null) args["faces"] = new JObject(_faceFiles.Select(o => new JProperty(o.Key, o.Value)));
            else if (_picture.Text.Trim().Length != 0) args["path"] = _picture.Text.Trim();
            string[] layouts = { "auto", "equirect", "cross", "vertical_cross", "strip", "vertical_strip" };
            if (_faceFiles == null) args["layout"] = layouts[_layout.SelectedIndex];
            args["resolution"] = int.Parse((string)_resolution.SelectedItem);
            if (!_autoCentre.Checked) args["centre"] = new JArray((double)_cx.Value, (double)_cy.Value, (double)_cz.Value);
            if (!_autoRadius.Checked) args["radius"] = (double)_radius.Value;
            args["yaw"] = (double)_yaw.Value;
            args["brightness"] = (double)_brightness.Value;
            args["tint"] = Rgb(_tint.BackColor);
            args["below_horizon"] = _below.SelectedIndex == 1 ? "mirror" : "keep";
            args["reflections"] = _domeReflections.Checked;
            args["fog"] = _removeFog.Checked ? "remove" : "keep";
            args["draw"] = _unlit.Checked ? "unlit" : "lit";
            if (preview) args["preview"] = true;
            Run("add_sky_dome", args, _domeReport, result =>
            {
                string file = (string)result["preview_picture"];
                if (file != null && File.Exists(file)) ShowPicture(_preview, file, result["sky"]?["sun"], result["sky"]?["sun_found"]?.Value<bool>() == true, (double?)result["yaw"] ?? 0);
                if (result["centre"] is JArray c && c.Count == 3 && _autoCentre.Checked) { _cx.Value = Clamp(_cx, (double)c[0]); _cy.Value = Clamp(_cy, (double)c[1]); _cz.Value = Clamp(_cz, (double)c[2]); }
                if (result["radius"] != null && _autoRadius.Checked) _radius.Value = Clamp(_radius, (double)result["radius"]);
            });
        }

        private static decimal Clamp(NumericUpDown box, double v) => Math.Max(box.Minimum, Math.Min(box.Maximum, (decimal)v));

        private void RunLights(bool preview)
        {
            JObject args = new JObject() { ["name"] = _name.Text.Trim() };
            if (_area.SelectedIndex == 1)
                args["region"] = new JObject() { ["box_min"] = new JArray((double)_x0.Value, (double)_y0.Value, (double)_z0.Value), ["box_max"] = new JArray((double)_x1.Value, (double)_y1.Value, (double)_z1.Value) };
            if (_sunFromSky.Checked) args["sun"] = "sky";
            else args["sun"] = new JObject() { ["azimuth"] = (double)_azimuth.Value, ["elevation"] = (double)_elevation.Value };
            if (!_sunColourFromSky.Checked) args["sun_colour"] = Rgb(_sunColour.BackColor);
            args["sun_intensity"] = (double)_sunIntensity.Value;
            args["sun_mode"] = new[] { "auto", "spot", "patches", "none" }[_sunMode.SelectedIndex];
            if (!_skyColourFromSky.Checked) args["sky_colour"] = Rgb(_skyColour.BackColor);
            args["sky_intensity"] = (double)_skyIntensity.Value;
            args["fill_spacing"] = (double)_spacing.Value;
            args["leak"] = (double)_leak.Value;
            args["max_reach"] = (double)_reach.Value;
            args["bounce"] = (double)_bounce.Value;
            args["reflections"] = _lightReflections.Checked;
            if (preview) args["preview"] = true;
            Run("add_outdoor_lighting", args, _lightReport, result =>
            {
                string file = (string)result["map"];
                if (file != null && File.Exists(file)) ShowFile(_map, file);
            });
        }

        private void Run(string toolName, JObject args, TextBox report, Action<JObject> after)
        {
            if (_busy) return;
            McpTool tool = McpTools.Find(toolName);
            if (tool == null) { report.Text = "The " + toolName + " tool is missing from this build."; return; }
            _busy = true;
            UseWaitCursor = true;
            foreach (Button b in new[] { _check, _addDome, _removeDome, _plan, _applyLights, _removeLights }) b.Enabled = false;
            _status.Text = "Working...";
            report.Text = "Working...";
            Task.Run(() =>
            {
                McpBuildingTools.FromWindow = true;
                try
                {
                    McpCall call = new McpCall(tool, args, CancellationToken.None, (text, d, total) => { try { BeginInvoke(new Action(() => _status.Text = text)); } catch { } });
                    return tool.Invoke(call);
                }
                finally { McpBuildingTools.FromWindow = false; }
            }).ContinueWith(t =>
            {
                _busy = false;
                UseWaitCursor = false;
                foreach (Button b in new[] { _check, _addDome, _removeDome, _plan, _applyLights, _removeLights }) b.Enabled = true;
                if (t.Exception != null)
                {
                    report.Text = "Failed: " + t.Exception.InnerException?.Message;
                    _status.Text = "Failed.";
                    return;
                }
                JObject response = t.Result;
                string text = string.Concat(((JArray)response["content"] ?? new JArray()).Where(c => (string)c["type"] == "text").Select(c => (string)c["text"]));
                if (response["isError"]?.Value<bool>() == true)
                {
                    report.Text = text;
                    _status.Text = "Not done: see the report.";
                    return;
                }
                JObject result = null;
                try { result = JObject.Parse(text); } catch { }
                report.Text = result != null ? Describe(result) : text;
                _status.Text = args["preview"]?.Value<bool>() == true ? "Checked: nothing was changed." : "Done: Save and Build to see it in game.";
                if (result != null) after?.Invoke(result);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        /// <summary>A result as text: warnings first, then everything else as indented lines.</summary>
        private static string Describe(JObject result)
        {
            List<string> lines = new List<string>();
            if (result["warnings"] is JArray warnings && warnings.Count != 0)
            {
                lines.Add("WARNINGS");
                foreach (JToken w in warnings) lines.Add("  ! " + (string)w);
                lines.Add("");
            }
            void Walk(JToken token, string indent, string key)
            {
                if (key == "warnings") return;
                if (token is JObject o)
                {
                    if (key != null) lines.Add(indent + key + ":");
                    foreach (JProperty p in o.Properties()) Walk(p.Value, key == null ? indent : indent + "  ", p.Name);
                }
                else if (token is JArray a && a.Any(x => x is JObject || (x.Type == JTokenType.String && ((string)x).Length > 30)))
                {
                    lines.Add(indent + key + ":");
                    foreach (JToken x in a) { if (x is JObject) Walk(x, indent + "  ", "-"); else lines.Add(indent + "  - " + x.ToString()); }
                }
                else lines.Add(indent + key + ": " + (token is JArray arr ? "[" + string.Join(", ", arr.Select(x => x.ToString())) + "]" : token.ToString()));
            }
            Walk(result, "", null);
            return string.Join(Environment.NewLine, lines);
        }

        private static void ShowFile(PictureBox box, string file)
        {
            try
            {
                using (Bitmap loaded = new Bitmap(file))
                {
                    Image old = box.Image;
                    box.Image = new Bitmap(loaded);
                    old?.Dispose();
                }
            }
            catch { }
        }

        /// <summary>The dome's picture with its horizon, the middle (+Z, after the turn) and the sun found marked.</summary>
        private static void ShowPicture(PictureBox box, string file, JToken sun, bool sunFound, double yaw)
        {
            try
            {
                using (Bitmap loaded = new Bitmap(file))
                {
                    Bitmap marked = new Bitmap(loaded);
                    using (Graphics g = Graphics.FromImage(marked))
                    using (Pen horizon = new Pen(Color.FromArgb(160, 255, 255, 255), 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                    {
                        g.DrawLine(horizon, 0, marked.Height / 2f, marked.Width, marked.Height / 2f);
                        g.DrawLine(horizon, marked.Width / 2f, 0, marked.Width / 2f, marked.Height);
                        if (sunFound && sun != null)
                        {
                            //the sun's world angles back into the picture: take the turn off again
                            double az = (double)sun["azimuth"] - yaw, el = (double)sun["elevation"];
                            float x = (float)((0.5 + az / 360.0) % 1.0 + 1.0) % 1.0f * marked.Width, y = (float)((0.5 - el / 180.0) * marked.Height);
                            using (Pen ring = new Pen(Color.Gold, 3)) g.DrawEllipse(ring, x - 14, y - 14, 28, 28);
                        }
                    }
                    Image old = box.Image;
                    box.Image = marked;
                    old?.Dispose();
                }
            }
            catch { }
        }
        #endregion

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            _preview.Image?.Dispose();
            _map.Image?.Dispose();
            if (_open == this) _open = null;
        }
    }
}

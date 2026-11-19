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
    public partial class SkyAndSunWindow : BaseWindow
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

        private Dictionary<string, string> _faceFiles;
        private bool _busy;

        public SkyAndSunWindow() : base(WindowClosesOn.COMMANDS_RELOAD)
        {
            InitializeComponent();
            Icon = SharedFormIcon.Icon;
            EnableDocumentation("https://opencage.co.uk/docs/sky-and-outdoor-lighting/");

            _layout.SelectedIndex = 0;
            _resolution.SelectedIndex = 1;
            _below.SelectedIndex = 0;
            _area.SelectedIndex = 0;
            _sunMode.SelectedIndex = 0;
            Gate();
        }

        #region Layout
        //The splits are sized once the window has its own size (a SplitterDistance set before that is clamped to the default 150 px)
        private void SizeSplits()
        {
            foreach (SplitContainer split in new[] { _skySplit, _lightSplit })
                if (split.Height > 420) try { split.SplitterDistance = split.Height - 210; } catch (InvalidOperationException) { }
            if (_lightColumns.Width > 600) try { _lightColumns.SplitterDistance = Math.Max(560, _lightColumns.Width - 380); } catch (InvalidOperationException) { }
        }

        private void SkyAndSunWindow_Load(object sender, EventArgs e) => SizeSplits();
        private void tabs_SelectedIndexChanged(object sender, EventArgs e) => SizeSplits();

        private void _autoCentre_CheckedChanged(object sender, EventArgs e) { _cx.Enabled = _cy.Enabled = _cz.Enabled = !_autoCentre.Checked; }
        private void _autoRadius_CheckedChanged(object sender, EventArgs e) => _radius.Enabled = !_autoRadius.Checked;
        private void _yawBar_ValueChanged(object sender, EventArgs e) { if (_yaw.Value != _yawBar.Value) _yaw.Value = _yawBar.Value; }
        private void _yaw_ValueChanged(object sender, EventArgs e) { int v = (int)Math.Round(_yaw.Value); if (_yawBar.Value != v) _yawBar.Value = Math.Max(-180, Math.Min(180, v)); }

        //brightness on a log slider: 0 -> 0.01, 50 -> 0.3, 100 -> 5
        private void _brightBar_ValueChanged(object sender, EventArgs e) { decimal v = (decimal)Math.Round(BrightOf(_brightBar.Value), 3); if (Math.Abs(_brightness.Value - v) > 0.0005m) _brightness.Value = Math.Max(_brightness.Minimum, Math.Min(_brightness.Maximum, v)); }
        private void _brightness_ValueChanged(object sender, EventArgs e) { int v = SliderOf((double)_brightness.Value); if (_brightBar.Value != v) _brightBar.Value = v; }

        private void _tint_Click(object sender, EventArgs e) => PickColour(_tint);
        private void _browse_Click(object sender, EventArgs e) => BrowsePicture();
        private void _faces_Click(object sender, EventArgs e) => BrowseFaces();
        private void _check_Click(object sender, EventArgs e) => RunDome(preview: true);
        private void _addDome_Click(object sender, EventArgs e) => RunDome(preview: false);
        private void _removeDome_Click(object sender, EventArgs e) => Run("add_sky_dome", new JObject() { ["remove"] = true }, _domeReport, null);

        private static double BrightOf(int slider) => slider <= 50 ? 0.01 * Math.Pow(30, slider / 50.0) : 0.3 * Math.Pow(5 / 0.3, (slider - 50) / 50.0);
        private static int SliderOf(double v) => v <= 0.01 ? 0 : v <= 0.3 ? (int)Math.Round(50 * Math.Log(v / 0.01) / Math.Log(30)) : Math.Min(100, (int)Math.Round(50 + 50 * Math.Log(v / 0.3) / Math.Log(5 / 0.3)));

        /* The outdoor lighting options that only apply in some modes */
        private void Gate()
        {
            bool box = _area.SelectedIndex == 1;
            _x0.Enabled = _y0.Enabled = _z0.Enabled = _x1.Enabled = _y1.Enabled = _z1.Enabled = box;
            _azimuth.Enabled = _elevation.Enabled = !_sunFromSky.Checked;
            _sunColour.Enabled = !_sunColourFromSky.Checked;
            _skyColour.Enabled = !_skyColourFromSky.Checked;
        }

        private void Gate_Changed(object sender, EventArgs e) => Gate();
        private void _sunColour_Click(object sender, EventArgs e) => PickColour(_sunColour);
        private void _skyColour_Click(object sender, EventArgs e) => PickColour(_skyColour);
        private void _plan_Click(object sender, EventArgs e) => RunLights(preview: true);
        private void _applyLights_Click(object sender, EventArgs e) => RunLights(preview: false);
        private void _removeLights_Click(object sender, EventArgs e) => Run("add_outdoor_lighting", new JObject() { ["name"] = _name.Text.Trim(), ["remove"] = true }, _lightReport, null);

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

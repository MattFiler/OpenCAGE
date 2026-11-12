using Newtonsoft.Json.Linq;
using OpenCAGE.MCP;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE.Popups.Rigging
{
    /// <summary>
    /// "Import animated prop": a moving prop from an FBX/glTF, built as the game's own animated props are - a prop rig and animation set
    /// in ANIMATION.PAK, and a composite with an EnvironmentModelReference and a PlayEnvironmentAnimation in the open level. Runs the
    /// import_environment_animation tool; Check is its dry run.
    /// </summary>
    public class PropImportDialog : Form
    {
        private readonly TextBox _file = new TextBox() { Width = 420 };
        private readonly Button _browse = new Button() { Text = "...", Width = 30 };
        private readonly TextBox _name = new TextBox() { Width = 200, CharacterCasing = CharacterCasing.Upper };
        private readonly TextBox _composite = new TextBox() { Width = 260 };
        private readonly NumericUpDown _rate = new NumericUpDown() { Minimum = 1, Maximum = 240, Value = 30, Width = 60 };
        private readonly TextBox _play = new TextBox() { Width = 160 };
        private readonly CheckBox _loop = new CheckBox() { Text = "Loop it", Checked = true, AutoSize = true };
        private readonly CheckBox _place = new CheckBox() { Text = "Also place it at the level's origin", Checked = false, AutoSize = true };
        private readonly TextBox _report = new TextBox() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Width = 640, Height = 240 };
        private readonly Button _check = new Button() { Text = "Check", AutoSize = true };
        private readonly Button _import = new Button() { Text = "Import", AutoSize = true };
        private readonly Button _close = new Button() { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel };

        public PropImportDialog()
        {
            Text = "Import animated prop";
            Icon = SharedFormIcon.Icon;
            StartPosition = FormStartPosition.CenterParent;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            CancelButton = _close;
            FlowLayoutPanel root = new FlowLayoutPanel() { FlowDirection = FlowDirection.TopDown, AutoSize = true, Padding = new Padding(10), WrapContents = false };
            root.Controls.Add(new Label() { AutoSize = true, MaximumSize = new Size(640, 0), Text = "An FBX or glTF whose parts are animated as objects (each part its own node, keyed), or skinned to an armature. The prop gets a rig and an animation set of its own in ANIMATION.PAK (shared by every level, written at once), and comes into the open level as a composite that plays a clip." });
            root.Controls.Add(Row(Caption("File:"), _file, _browse));
            root.Controls.Add(Row(Caption("Rig and set name:"), _name, Caption("  frames per second:"), _rate));
            root.Controls.Add(Row(Caption("Composite:"), _composite));
            root.Controls.Add(Row(Caption("Play from the start (clip, empty = the first, 'none'):"), _play, _loop));
            root.Controls.Add(_place);
            root.Controls.Add(_report);
            root.Controls.Add(Row(_check, _import, _close));
            Controls.Add(root);
            _browse.Click += (s, e) =>
            {
                using (OpenFileDialog open = new OpenFileDialog() { Filter = AnimationImport.FileFilter })
                    if (open.ShowDialog(this) == DialogResult.OK)
                    {
                        _file.Text = open.FileName;
                        if (_name.Text.Length == 0) _name.Text = "PROP_" + new string(Path.GetFileNameWithoutExtension(open.FileName).ToUpperInvariant().Select(c => char.IsLetterOrDigit(c) && c < 128 ? c : '_').ToArray());
                    }
            };
            _name.TextChanged += (s, e) => _composite.Text = "PROPS\\" + _name.Text;
            _check.Click += async (s, e) => await Run(true);
            _import.Click += async (s, e) => await Run(false);
            OpenCAGE.Theming.ThemeManager.ApplyToForm(this);
        }

        private static FlowLayoutPanel Row(params Control[] controls)
        {
            FlowLayoutPanel row = new FlowLayoutPanel() { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false };
            row.Controls.AddRange(controls);
            return row;
        }
        private static Label Caption(string text) => new Label() { Text = text, AutoSize = true, Margin = new Padding(3, 6, 3, 0) };

        private async Task Run(bool dryRun)
        {
            JObject args = new JObject()
            {
                ["path"] = _file.Text.Trim(),
                ["name"] = _name.Text.Trim(),
                ["composite_name"] = _composite.Text.Trim(),
                ["frame_rate"] = (double)_rate.Value,
                ["loop"] = _loop.Checked,
                ["dry_run"] = dryRun,
            };
            if (_play.Text.Trim().Length != 0) args["play"] = _play.Text.Trim();
            if (_place.Checked && !dryRun) args["place_in"] = "root";
            McpTool tool = McpTools.Find("import_environment_animation");
            if (tool == null) { _report.Text = "The import tool is missing from this build."; return; }
            _check.Enabled = _import.Enabled = false;
            UseWaitCursor = true;
            _report.Text = dryRun ? "Reading the file..." : "Importing...";
            try
            {
                JObject result = await Task.Run(() =>
                {
                    using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMinutes(20)))
                        return tool.Invoke(new McpCall(tool, args, cts.Token, null));
                });
                string text = string.Concat(((JArray)result["content"] ?? new JArray()).Where(c => (string)c["type"] == "text").Select(c => (string)c["text"]));
                try { text = JToken.Parse(text).ToString(Newtonsoft.Json.Formatting.Indented); } catch { }
                _report.Text = ((bool?)result["isError"] == true ? "FAILED: " : "") + text.Replace("\n", Environment.NewLine);
            }
            catch (Exception e) { _report.Text = "Failed: " + e.Message; }
            finally { _check.Enabled = _import.Enabled = true; UseWaitCursor = false; }
        }
    }
}

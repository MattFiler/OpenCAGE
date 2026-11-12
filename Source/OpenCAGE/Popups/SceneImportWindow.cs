using Newtonsoft.Json.Linq;
using OpenCAGE.MCP;
using OpenCAGE.Popups.Base;
using OpenCAGE.SceneImport;
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
    /// File > Import Scene: a whole scene file into the open level (models, materials, collision, physics props, lights). Dry Run and
    /// Import run the MCP tool import_scene on a worker thread; Import is one undo step.
    /// </summary>
    public class SceneImportWindow : BaseWindow
    {
        private static SceneImportWindow _open;

        /// <summary>Adds "Import Scene..." after <paramref name="after"/> (File > Import Composites) in its menu.</summary>
        public static void AttachMenu(ToolStripMenuItem after)
        {
            ToolStrip owner = after?.Owner;
            ToolStripDropDownItem menu = after?.OwnerItem as ToolStripDropDownItem;
            if (owner == null) return;
            ToolStripMenuItem item = new ToolStripMenuItem("Import Scene...") { Name = "importSceneToolStripMenuItem", ToolTipText = "Import a whole scene file (glTF, FBX...) - its models, placements, materials, collision and lights - into the open level" };
            item.Click += (s, e) => Open();
            owner.Items.Insert(owner.Items.IndexOf(after) + 1, item);
            if (menu != null)
                menu.DropDownOpening += (s, e) => item.Enabled = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands != null;
        }

        public static SceneImportWindow Open()
        {
            if (_open == null || _open.IsDisposed)
            {
                _open = new SceneImportWindow();
                _open.Show();
            }
            else
                _open.BringToFront();
            return _open;
        }

        private readonly TextBox _file = new TextBox() { Width = 420 };
        private readonly Button _browse = new Button() { Text = "Browse...", AutoSize = true };
        private readonly TextBox _name = new TextBox() { Width = 200 };
        private readonly NumericUpDown _scale = Num(0.0001m, 10000, 4, 1);
        private readonly NumericUpDown _px = Num(-100000, 100000, 2), _py = Num(-100000, 100000, 2), _pz = Num(-100000, 100000, 2), _yaw = Num(-360, 360, 1);
        private readonly ComboBox _group = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
        private readonly ComboBox _collision = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
        private readonly TextBox _physics = new TextBox() { Width = 300, Text = string.Join(", ", SceneOptions.DefaultPhysics) };
        private readonly TextBox _exclude = new TextBox() { Width = 300 };
        private readonly CheckBox _lights = new CheckBox() { Text = "Import the file's lights", AutoSize = true, Checked = true };
        private readonly NumericUpDown _lightScale = Num(0, 1000, 2, 1);
        private readonly ComboBox _shadows = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        private readonly CheckBox _sun = new CheckBox() { Text = "Build a sun from a directional light (Outdoor Lighting)", AutoSize = true };
        private readonly CheckBox _merge = new CheckBox() { Text = "Meshes with the same content share a model", AutoSize = true, Checked = true };
        private readonly CheckBox _compact = new CheckBox() { Text = "Compact textures (DXT1/DXT5: half the texture memory)", AutoSize = true };
        private readonly Button _dryRun = new Button() { Text = "Dry Run", AutoSize = true };
        private readonly Button _import = new Button() { Text = "Import", AutoSize = true };
        private readonly TextBox _report = new TextBox() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font(FontFamily.GenericMonospace, 8.5f), WordWrap = true };
        private readonly Label _status = new Label() { AutoSize = true, Padding = new Padding(6, 4, 0, 4) };
        private readonly SplitContainer _split = new SplitContainer() { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        private bool _busy;

        private static NumericUpDown Num(decimal min, decimal max, int decimals, decimal value = 0) =>
            new NumericUpDown() { Minimum = min, Maximum = max, DecimalPlaces = decimals, Value = Math.Max(min, Math.Min(max, value)), Width = 80, Increment = decimals == 0 ? 1 : 0.1m };

        public SceneImportWindow() : base(WindowClosesOn.COMMANDS_RELOAD)
        {
            Text = "Import Scene";
            Icon = SharedFormIcon.Icon;
            Size = new Size(940, 860);
            MinimumSize = new Size(760, 600);
            StartPosition = FormStartPosition.CenterScreen;
            EnableDocumentation("https://opencage.co.uk/docs/model-editor/#import-scene");

            _group.Items.AddRange(new object[] { "A composite per top-level node of the file", "One composite for the whole scene" });
            _group.SelectedIndex = 0;
            _collision.Items.AddRange(new object[] { "Solid (walked on and shot at)", "Walls and floors only (blocks movement)", "Bullets only (and footsteps, zones)", "None" });
            _collision.SelectedIndex = 0;
            _shadows.Items.AddRange(new object[] { "Spot lights cast shadows", "Every light casts shadows", "No shadows" });
            _shadows.SelectedIndex = 0;

            FlowLayoutPanel top = new FlowLayoutPanel() { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Dock = DockStyle.Fill, Padding = new Padding(6) };
            top.Controls.Add(Note("A whole scene file - glTF / GLB, FBX, COLLADA or OBJ - into the open level: every node with meshes is placed, and nodes drawing the same meshes at the same scale share one model. Each model gets generated materials, a composite and collision made to match it; nodes named like a physics pattern become physics props; the file's lights come in too. Dry Run lists everything with the budgets it runs into and changes nothing; Import is one undo step."));
            top.Controls.Add(Row(L("File:", 110), _file, _browse));
            top.Controls.Add(Row(L("Name:", 110), _name, L("Scale:"), _scale));
            top.Controls.Add(Row(L("Place at:", 110), _px, _py, _pz, L("Turn (deg):"), _yaw));
            top.Controls.Add(Row(L("Group:", 110), _group));
            top.Controls.Add(Row(L("Collision:", 110), _collision));
            top.Controls.Add(Row(L("Physics props:", 110), _physics, L("(node names, * and ?)")));
            top.Controls.Add(Row(L("Leave out:", 110), _exclude, L("(node names)")));
            top.Controls.Add(Row(L("Lights:", 110), _lights, L("Brightness x"), _lightScale, _shadows));
            top.Controls.Add(Row(L("", 110), _sun));
            top.Controls.Add(Row(L("", 110), _merge));
            top.Controls.Add(Row(L("", 110), _compact));
            _split.Panel1.Controls.Add(top);
            _split.Panel2.Controls.Add(_report);
            Controls.Add(_split);
            //the buttons under the report, always in view whatever the window's size
            FlowLayoutPanel buttons = Row(_dryRun, _import);
            buttons.Dock = DockStyle.Bottom;
            buttons.Padding = new Padding(6, 2, 0, 2);
            Controls.Add(buttons);
            Controls.Add(_status);
            _status.Dock = DockStyle.Bottom;
            Load += (s, e) => { try { _split.SplitterDistance = Math.Max(120, Math.Min(top.PreferredSize.Height + 8, _split.Height - 160)); } catch (InvalidOperationException) { } };

            _browse.Click += (s, e) => Browse();
            _file.TextChanged += (s, e) => { if (_name.Tag == null) _name.Text = DefaultName(_file.Text); };
            _name.KeyPress += (s, e) => _name.Tag = "typed";
            _lights.CheckedChanged += (s, e) => _lightScale.Enabled = _shadows.Enabled = _sun.Enabled = _lights.Checked;
            _dryRun.Click += (s, e) => RunTool(dryRun: true);
            _import.Click += (s, e) => RunTool(dryRun: false);
            _status.Text = "Dry Run only reports; Import changes the level (one undo step; Save and Build to see it in game).";
        }

        private static string DefaultName(string path)
        {
            try { return string.IsNullOrWhiteSpace(path) ? "" : ScenePlan.EntityName(Path.GetFileNameWithoutExtension(path), "Scene"); }
            catch { return ""; }
        }

        private static FlowLayoutPanel Row(params Control[] controls)
        {
            FlowLayoutPanel row = new FlowLayoutPanel() { AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
            foreach (Control c in controls) { c.Margin = new Padding(3, 3, 3, 3); row.Controls.Add(c); }
            return row;
        }

        private static Label L(string text, int width = 0) => width > 0 ? new Label() { Text = text, Width = width, Padding = new Padding(0, 4, 0, 0) } : new Label() { Text = text, AutoSize = true, Padding = new Padding(0, 4, 0, 0) };

        private static Label Note(string text) => new Label() { Text = text, AutoSize = true, MaximumSize = new Size(880, 0), ForeColor = SystemColors.GrayText, Padding = new Padding(3, 0, 0, 4) };

        private void Browse()
        {
            using (OpenFileDialog dialog = new OpenFileDialog() { Title = "Import Scene", Filter = "Scenes (*.gltf;*.glb;*.fbx;*.dae;*.obj)|*.gltf;*.glb;*.fbx;*.dae;*.obj|All files (*.*)|*.*" })
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    _file.Text = dialog.FileName;
        }

        /// <summary>The window's settings as import_scene's arguments.</summary>
        internal JObject Arguments(bool dryRun)
        {
            JObject args = new JObject()
            {
                ["path"] = _file.Text.Trim(),
                ["scale"] = (double)_scale.Value,
                ["group_by"] = _group.SelectedIndex == 1 ? "none" : "top",
                ["collision"] = new[] { "solid", "world", "ballistic", "none" }[Math.Max(0, _collision.SelectedIndex)],
                ["physics"] = new JArray(Split(_physics.Text)),
                ["lights"] = _lights.Checked,
                ["light_scale"] = (double)_lightScale.Value,
                ["shadows"] = new[] { "spots", "all", "none" }[Math.Max(0, _shadows.SelectedIndex)],
                ["directional"] = _lights.Checked && _sun.Checked ? "outdoor_lighting" : "note",
                ["merge_identical"] = _merge.Checked,
                ["compact_textures"] = _compact.Checked,
                ["dry_run"] = dryRun,
            };
            if (!string.IsNullOrWhiteSpace(_name.Text)) args["name"] = _name.Text.Trim();
            if (_exclude.Text.Trim().Length != 0) args["exclude"] = new JArray(Split(_exclude.Text));
            if (_px.Value != 0 || _py.Value != 0 || _pz.Value != 0) args["position"] = new JArray((double)_px.Value, (double)_py.Value, (double)_pz.Value);
            if (_yaw.Value != 0) args["rotation"] = new JArray(0, (double)_yaw.Value, 0);
            return args;
        }

        private static IEnumerable<string> Split(string text) => (text ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(o => o.Trim()).Where(o => o.Length != 0);

        /// <summary>Set the window up from a file and run the dry run (used by tests, and by whatever hands the window a file).</summary>
        public void UseFile(string path)
        {
            _file.Text = path;
        }

        internal void RunTool(bool dryRun)
        {
            if (_busy) return;
            if (string.IsNullOrWhiteSpace(_file.Text) || !File.Exists(_file.Text.Trim()))
            {
                _report.Text = "Pick a scene file first.";
                return;
            }
            McpTool tool = McpTools.Find("import_scene");
            if (tool == null) { _report.Text = "The import_scene tool is missing from this build."; return; }
            JObject args = Arguments(dryRun);
            _busy = true;
            UseWaitCursor = true;
            _dryRun.Enabled = _import.Enabled = false;
            _status.Text = dryRun ? "Planning..." : "Importing...";
            _report.Text = _status.Text;
            Task.Run(() =>
            {
                SceneImporter.FromWindow = true;
                try
                {
                    McpCall call = new McpCall(tool, args, CancellationToken.None, (text, d, total) => { try { BeginInvoke(new Action(() => _status.Text = text)); } catch { } });
                    return tool.Invoke(call);
                }
                finally { SceneImporter.FromWindow = false; }
            }).ContinueWith(t =>
            {
                _busy = false;
                UseWaitCursor = false;
                _dryRun.Enabled = _import.Enabled = true;
                if (t.Exception != null)
                {
                    _report.Text = "Failed: " + t.Exception.InnerException?.Message;
                    _status.Text = "Failed.";
                    return;
                }
                JObject response = t.Result;
                string text = string.Concat(((JArray)response["content"] ?? new JArray()).Where(c => (string)c["type"] == "text").Select(c => (string)c["text"]));
                if (response["isError"]?.Value<bool>() == true)
                {
                    _report.Text = text;
                    _status.Text = "Not imported: see the report.";
                    return;
                }
                JObject result = null;
                try { result = JObject.Parse(text); } catch { }
                _report.Text = result != null ? Describe(result) : text;
                _status.Text = dryRun ? "Dry run: nothing was changed." : "Imported (one undo step): Save and Build to see it in game.";
                LastResult = result;
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        /// <summary>The last result shown (for tests).</summary>
        internal JObject LastResult { get; private set; }
        internal bool Busy => _busy;
        internal string ReportText => _report.Text;

        /// <summary>A result as text: warnings first, then the summary and budgets, then everything else as indented lines.</summary>
        internal static string Describe(JObject result)
        {
            List<string> lines = new List<string>();
            if (result["warnings"] is JArray warnings && warnings.Count != 0)
            {
                lines.Add("WARNINGS");
                foreach (JToken w in warnings) lines.Add("  ! " + (string)w);
                lines.Add("");
            }
            if (result["notes"] is JArray notes && notes.Count != 0)
            {
                lines.Add("NOTES");
                foreach (JToken n in notes) lines.Add("  - " + (string)n);
                lines.Add("");
            }
            void Walk(JToken token, string indent, string key)
            {
                if (token is JObject obj)
                {
                    if (key != null) lines.Add(indent + key + ":");
                    foreach (JProperty p in obj.Properties())
                        if (p.Name != "warnings" && p.Name != "notes") Walk(p.Value, key == null ? indent : indent + "  ", p.Name);
                }
                else if (token is JArray array && array.Any(o => o is JObject))
                {
                    lines.Add(indent + key + ": (" + array.Count + ")");
                    foreach (JToken item in array)
                    {
                        if (item is JObject o && (o["name"] ?? o["composite"] ?? o["source"]) is JToken title)
                        {
                            lines.Add(indent + "  - " + (string)title);
                            foreach (JProperty p in o.Properties())
                                if (!ReferenceEquals(p.Value, title)) Walk(p.Value, indent + "      ", p.Name);
                        }
                        else Walk(item, indent + "  ", "-");
                    }
                }
                else if (token is JArray list)
                    lines.Add(indent + key + ": " + string.Join(", ", list.Select(o => o.Type == JTokenType.String ? (string)o : o.ToString(Newtonsoft.Json.Formatting.None))));
                else
                    lines.Add(indent + key + ": " + (token.Type == JTokenType.String ? (string)token : token.ToString(Newtonsoft.Json.Formatting.None)));
            }
            foreach (string section in new[] { "summary", "budgets", "placement_check", "made", "undo_step", "groups", "models", "materials", "lights" })
                if (result[section] != null)
                {
                    lines.Add(section.ToUpperInvariant().Replace('_', ' '));
                    Walk(result[section], "  ", result[section] is JObject ? null : "");
                    lines.Add("");
                }
            foreach (JProperty rest in result.Properties().Where(o => !new[] { "summary", "budgets", "placement_check", "made", "undo_step", "groups", "models", "materials", "lights", "warnings", "notes" }.Contains(o.Name)))
                Walk(rest.Value, "", rest.Name);
            return string.Join(Environment.NewLine, lines);
        }
    }
}

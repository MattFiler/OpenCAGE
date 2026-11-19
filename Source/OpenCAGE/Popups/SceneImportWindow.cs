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
    public partial class SceneImportWindow : BaseWindow
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

        private bool _busy;

        public SceneImportWindow() : base(WindowClosesOn.COMMANDS_RELOAD)
        {
            InitializeComponent();
            Icon = SharedFormIcon.Icon;
            EnableDocumentation("https://opencage.co.uk/docs/model-editor/#import-scene");

            _physics.Text = string.Join(", ", SceneOptions.DefaultPhysics);
            _group.SelectedIndex = 0;
            _collision.SelectedIndex = 0;
            _shadows.SelectedIndex = 0;
        }

        //The options get the room they need and the report the rest; the buttons stay under the report whatever the window's size
        private void SceneImportWindow_Load(object sender, EventArgs e)
        {
            try { _split.SplitterDistance = Math.Max(120, Math.Min(optionsPanel.PreferredSize.Height + 8, _split.Height - 160)); } catch (InvalidOperationException) { }
        }

        private void _browse_Click(object sender, EventArgs e) => Browse();

        private void _file_TextChanged(object sender, EventArgs e)
        {
            if (_name.Tag == null) _name.Text = DefaultName(_file.Text);
        }

        private void _name_KeyPress(object sender, KeyPressEventArgs e) => _name.Tag = "typed";
        private void _lights_CheckedChanged(object sender, EventArgs e) => _lightScale.Enabled = _shadows.Enabled = _sun.Enabled = _lights.Checked;
        private void _dryRun_Click(object sender, EventArgs e) => RunTool(dryRun: true);
        private void _import_Click(object sender, EventArgs e) => RunTool(dryRun: false);

        private static string DefaultName(string path)
        {
            try { return string.IsNullOrWhiteSpace(path) ? "" : ScenePlan.EntityName(Path.GetFileNameWithoutExtension(path), "Scene"); }
            catch { return ""; }
        }

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

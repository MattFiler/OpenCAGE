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
    public partial class PropImportDialog : Form
    {
        public PropImportDialog()
        {
            InitializeComponent();
            Icon = SharedFormIcon.Icon;
            OpenCAGE.Theming.ThemeManager.ApplyToForm(this);
        }

        private void Browse_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog open = new OpenFileDialog() { Filter = AnimationImport.FileFilter })
                if (open.ShowDialog(this) == DialogResult.OK)
                {
                    _file.Text = open.FileName;
                    if (_name.Text.Length == 0) _name.Text = "PROP_" + new string(Path.GetFileNameWithoutExtension(open.FileName).ToUpperInvariant().Select(c => char.IsLetterOrDigit(c) && c < 128 ? c : '_').ToArray());
                }
        }

        private void Name_TextChanged(object sender, EventArgs e)
        {
            _composite.Text = "PROPS\\" + _name.Text;
        }

        private async void Check_Click(object sender, EventArgs e)
        {
            await Run(true);
        }

        private async void Import_Click(object sender, EventArgs e)
        {
            await Run(false);
        }

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

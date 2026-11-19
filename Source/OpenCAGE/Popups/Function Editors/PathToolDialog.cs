using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using Newtonsoft.Json.Linq;
using OpenCAGE.MCP;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>A small tool window: rows of settings, Check / Apply, and the result.</summary>
    internal sealed partial class PathToolDialog : Form
    {
        public Func<(string tool, JObject args)> OnApply;
        public Func<(string tool, JObject args)> OnCheck;
        public Func<string, JObject, Task<(bool ok, string text, JObject result)>> Run;

        public PathToolDialog(string title, string about)
        {
            InitializeComponent();
            Text = title;
            Icon = SharedFormIcon.Icon;
            introLabel.Text = about;
        }

        private void _close_Click(object sender, EventArgs e) => Close();
        private void _apply_Click(object sender, EventArgs e) => Go(OnApply);
        private void _check_Click(object sender, EventArgs e) => Go(OnCheck);
        private void PathToolDialog_Shown(object sender, EventArgs e) => _check.Visible = OnCheck != null;

        /* The callers' settings: a label and its control per row of the table */
        private void Row(string label, Control control)
        {
            control.Dock = DockStyle.Fill;
            _rows.Controls.Add(new Label() { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 4, 0, 0) });
            _rows.Controls.Add(control);
        }

        public ComboBox Combo(string label, IEnumerable<string> items, int selected)
        {
            ComboBox box = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (string item in items) box.Items.Add(item);
            if (box.Items.Count != 0) box.SelectedIndex = Math.Max(0, Math.Min(box.Items.Count - 1, selected));
            Row(label, box);
            return box;
        }

        public NumericUpDown Number(string label, decimal value, decimal min, decimal max, int decimals)
        {
            NumericUpDown box = new NumericUpDown() { Minimum = min, Maximum = max, DecimalPlaces = decimals, Increment = decimals == 0 ? 1 : (decimal)Math.Pow(10, -Math.Min(decimals, 2)) };
            box.Value = Math.Max(min, Math.Min(max, value));
            Row(label, box);
            return box;
        }

        public TextBox TextRow(string label, string value)
        {
            TextBox box = new TextBox() { Text = value ?? "" };
            Row(label, box);
            return box;
        }

        public CheckBox Check(string label, bool value)
        {
            CheckBox box = new CheckBox() { Checked = value, Text = "" };
            Row(label, box);
            return box;
        }

        /// <summary>Last result text (for UI automation).</summary>
        public string ReportText => _report.Text;
        public bool Busy { get; private set; }

        /// <summary>Press Apply (or Check) from code, for UI automation.</summary>
        public void Press(bool check) => Go(check ? OnCheck : OnApply);

        private void Go(Func<(string tool, JObject args)> make)
        {
            if (make == null || Run == null || Busy) return;
            (string tool, JObject args) job;
            try { job = make(); }
            catch (Exception e) { _report.Text = e.Message; _status.Text = "Not done."; return; }
            Busy = true;
            _apply.Enabled = _check.Enabled = false;
            UseWaitCursor = true;
            _status.Text = "Working...";
            Run(job.tool, job.args).ContinueWith(t =>
            {
                Busy = false;
                UseWaitCursor = false;
                _apply.Enabled = _check.Enabled = true;
                if (t.Exception != null) { _report.Text = t.Exception.InnerException?.Message; _status.Text = "Failed."; return; }
                _report.Text = t.Result.text;
                bool dry = job.args["dry_run"]?.Value<bool>() == true || job.args["preview"]?.Value<bool>() == true;
                _status.Text = t.Result.ok ? (dry ? "Checked: nothing changed." : "Done (one undo step).") : "Not done: see below.";
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
    }
}

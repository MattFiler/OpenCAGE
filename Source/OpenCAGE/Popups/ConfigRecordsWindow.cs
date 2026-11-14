using CATHODE;
using CATHODE.Scripting;
using CathodeLib;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// "Used by" and "spare records" for a global configuration table: what uses each ammo type or character attribute set, and which
    /// records nothing uses (free for a new weapon or character). Opened from the Ammo and Attributes editors' banner.
    /// </summary>
    public class ConfigRecordsWindow : BaseWindow
    {
        private readonly ConfigRecords.Kind _kind;
        private readonly ListView _records = new ListView() { View = View.Details, FullRowSelect = true, HideSelection = false, Width = 470, Height = 330, MultiSelect = false };
        private readonly TextBox _detail = new TextBox() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Width = 470, Height = 330, WordWrap = true };
        private readonly ComboBox _scope = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
        private readonly Button _scan = new Button() { Text = "Find Uses", AutoSize = true };
        private readonly CheckBox _spareOnly = new CheckBox() { Text = "Spare records only", AutoSize = true };
        private readonly Label _status = new Label() { AutoSize = true, MaximumSize = new Size(940, 0) };
        private ConfigRecords.Report _report;
        private CancellationTokenSource _cancel;
        private string _select;

        public ConfigRecordsWindow(ConfigRecords.Kind kind, string select = null) : base(WindowClosesOn.COMMANDS_RELOAD)
        {
            _kind = kind;
            _select = select;
            Text = kind == ConfigRecords.Kind.Ammo ? "Ammo Types: Used By / Spare" : "Character Attributes: Used By / Spare";
            Icon = SharedFormIcon.Icon;
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            EnableDocumentation(kind == ConfigRecords.Kind.Ammo ? "https://opencage.co.uk/docs/configs/ammo/#spare-records" : "https://opencage.co.uk/docs/configs/attributes/#spare-records");

            _records.Columns.Add("Record", 170);
            _records.Columns.Add("Built on", 110);
            _records.Columns.Add("Uses", 60);
            _records.Columns.Add("", 110);
            _scope.Items.AddRange(new object[] { "Every level on disk", "The game's own levels", "The open level only" });
            _scope.SelectedIndex = 0;

            FlowLayoutPanel root = new FlowLayoutPanel() { FlowDirection = FlowDirection.TopDown, AutoSize = true, Padding = new Padding(10), WrapContents = false };
            root.Controls.Add(ConfigRecordsBanner.MakeLabel(kind, 940));
            FlowLayoutPanel top = new FlowLayoutPanel() { AutoSize = true, WrapContents = false };
            top.Controls.Add(new Label() { Text = "Look in:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            top.Controls.AddRange(new Control[] { _scope, _scan, _spareOnly });
            root.Controls.Add(top);
            FlowLayoutPanel lists = new FlowLayoutPanel() { AutoSize = true, WrapContents = false };
            lists.Controls.AddRange(new Control[] { _records, _detail });
            root.Controls.Add(lists);
            root.Controls.Add(_status);
            Controls.Add(root);

            _scan.Click += (s, e) => Scan();
            _spareOnly.CheckedChanged += (s, e) => Fill();
            _records.SelectedIndexChanged += (s, e) => ShowDetail();
            FormClosing += (s, e) => _cancel?.Cancel();
            Shown += (s, e) => Scan();
        }

        private void Scan()
        {
            _cancel?.Cancel();
            _cancel = new CancellationTokenSource();
            CancellationToken token = _cancel.Token;
            string root = Singleton.PathToAI;
            LevelContent content = Singleton.Editor?.CompositeBrowser?.Content;
            Commands open = content?.Level?.Commands;
            string openLevel = content?.Level == null ? null : NormaliseLevel(content.Level.Name);
            List<string> levels;
            switch (_scope.SelectedIndex)
            {
                case 1: levels = Level.GetLevels(root).Select(NormaliseLevel).Where(o => o.StartsWith("PRODUCTION/", StringComparison.OrdinalIgnoreCase)).ToList(); break;
                case 2: levels = openLevel == null ? new List<string>() : new List<string>() { openLevel }; break;
                default: levels = Level.GetLevels(root).Select(NormaliseLevel).ToList(); break;
            }
            if (openLevel != null && !levels.Contains(openLevel, StringComparer.OrdinalIgnoreCase) && _scope.SelectedIndex != 1) levels.Add(openLevel);
            _scan.Enabled = false;
            _status.Text = "Reading " + levels.Count + " level" + (levels.Count == 1 ? "" : "s") + " (the first time takes a while; they are kept after)...";
            //The open level's script is read on this thread, as it is now; the others on a worker
            ConfigRecords.LevelUses openUses = null;
            try { if (open != null && openLevel != null) openUses = ConfigRecords.Scan(open); } catch { }
            Task.Run(() =>
            {
                ConfigRecords.Report report = ConfigRecords.Find(root, _kind, levels.Where(o => !string.Equals(o, openLevel, StringComparison.OrdinalIgnoreCase)), null, null,
                    message => { try { BeginInvoke(new Action(() => _status.Text = message + "...")); } catch { } }, token);
                if (openUses != null)
                {
                    ConfigRecords.Report openReport = ConfigRecords.FindIn(root, _kind, openLevel, openUses);
                    foreach (KeyValuePair<string, List<ConfigRecords.Use>> uses in openReport.Uses.Where(o => o.Value.Any(u => u.Where == openLevel)))
                    {
                        if (!report.Uses.TryGetValue(uses.Key, out List<ConfigRecords.Use> list)) report.Uses[uses.Key] = list = new List<ConfigRecords.Use>();
                        list.AddRange(uses.Value.Where(u => u.Where == openLevel).Select(u => { u.Where += " (open, as it is now)"; return u; }));
                    }
                    report.LevelsRead.Add(openLevel);
                }
                return report;
            }, token).ContinueWith(task =>
            {
                if (IsDisposed) return;
                BeginInvoke(new Action(() =>
                {
                    _scan.Enabled = true;
                    if (task.IsCanceled) return;
                    if (task.IsFaulted) { _status.Text = "Could not finish: " + task.Exception?.GetBaseException().Message; return; }
                    _report = task.Result;
                    _status.Text = _report.Records.Count + " records, " + _report.Spare.Count + " spare; " + _report.LevelsRead.Count + " level" + (_report.LevelsRead.Count == 1 ? "" : "s") + " read" +
                        (_report.LevelsFailed.Count == 0 ? "" : ", " + _report.LevelsFailed.Count + " could not be read") + ". " + ConfigRecords.EngineNote(_kind);
                    Fill();
                }));
            });
        }

        private static string NormaliseLevel(string level) => (level ?? "").Replace('\\', '/').Trim('/').ToUpperInvariant();

        private void Fill()
        {
            if (_report == null) return;
            string selected = _records.SelectedItems.Count == 0 ? _select : _records.SelectedItems[0].Text;
            _records.BeginUpdate();
            _records.Items.Clear();
            foreach (ConfigRecords.Record record in _report.Records)
            {
                bool spare = _report.IsSpare(record);
                if (_spareOnly.Checked && !spare) continue;
                int uses = _report.UsesOf(record.Name).Count(o => o.Effective);
                ListViewItem item = new ListViewItem(new[] { record.Name, record.Template ?? "", uses.ToString(), spare ? "spare" : record.InheritedBy.Count != 0 ? record.InheritedBy.Count + " built on it" : "" });
                if (spare) item.ForeColor = Color.SeaGreen;
                _records.Items.Add(item);
                if (string.Equals(record.Name, selected, StringComparison.OrdinalIgnoreCase)) item.Selected = true;
            }
            _records.EndUpdate();
            ShowDetail();
        }

        private void ShowDetail()
        {
            if (_report == null || _records.SelectedItems.Count == 0) { _detail.Text = ""; return; }
            ConfigRecords.Record record = _report.Records.FirstOrDefault(o => o.Name == _records.SelectedItems[0].Text);
            if (record == null) return;
            List<string> lines = new List<string>() { record.Name + (record.Kind == ConfigRecords.Kind.Ammo ? " (AMMO_TYPE " + record.Id + ")" : "") + " - DATA\\" + record.File };
            if (record.Template != null) lines.Add("Built on " + record.Template + ": it takes every value it doesn't set itself from there.");
            if (record.InheritedBy.Count != 0) lines.Add("Built on it: " + string.Join(", ", record.InheritedBy) + " - a change here reaches them too, wherever they don't set the value themselves.");
            lines.Add(_report.IsSpare(record) ? "SPARE: nothing in the game's data or the levels read uses it." : "Used:");
            foreach (IGrouping<string, ConfigRecords.Use> where in _report.UsesOf(record.Name).Where(o => o.Where != "template").GroupBy(o => o.Where))
            {
                lines.Add("  " + where.Key + ":");
                foreach (ConfigRecords.Use use in where.Take(40))
                    lines.Add("    " + (use.Composite == null ? use.Note : use.Composite + " / " + use.Entity + " (" + use.Type + ")" + (use.Parameter == null ? "" : "." + use.Parameter) + (use.Note == null ? "" : " - " + use.Note)) + (use.Effective ? "" : " [reaches nothing]"));
                if (where.Count() > 40) lines.Add("    ... and " + (where.Count() - 40) + " more");
            }
            _detail.Text = string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>The banner a global configuration editor carries: what it changes, and a way to the records' uses.</summary>
    public static class ConfigRecordsBanner
    {
        public static Label MakeLabel(ConfigRecords.Kind kind, int width)
        {
            return new Label()
            {
                AutoSize = true,
                MaximumSize = new Size(width, 0),
                ForeColor = Color.DarkGoldenrod,
                Text = "GLOBAL: these records are shared by every level of the game, and by every mod's levels. A change here changes the base game" +
                       (kind == ConfigRecords.Kind.Ammo ? " for every weapon firing that ammo" : " for every character using that set") +
                       ", and every record built on the one you change. For a level of your own, take a spare record instead.",
            };
        }

        /// <summary>
        /// Put the banner across the top of a configuration editor (its controls moved down to make room), with "Used By..." for the
        /// record <paramref name="current"/> names and "Spare Records...".
        /// </summary>
        public static void Attach(Form editor, ConfigRecords.Kind kind, Func<string> current)
        {
            if (editor == null) return;
            Panel banner = new Panel() { Left = 0, Top = 0, Width = editor.ClientSize.Width, Height = 46, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            Label label = MakeLabel(kind, Math.Max(200, editor.ClientSize.Width - 200));
            label.Location = new Point(8, 4);
            Button usedBy = new Button() { Text = "Used By...", AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            Button spare = new Button() { Text = "Spare Records...", AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            usedBy.Location = new Point(banner.Width - 190, 4);
            spare.Location = new Point(banner.Width - 108, 4);
            usedBy.Click += (s, e) => new ConfigRecordsWindow(kind, current?.Invoke()).Show();
            spare.Click += (s, e) => { ConfigRecordsWindow window = new ConfigRecordsWindow(kind); window.Show(); };
            banner.Controls.Add(label);
            banner.Controls.Add(usedBy);
            banner.Controls.Add(spare);
            foreach (Control control in editor.Controls.Cast<Control>().ToList())
                if (control.Dock == DockStyle.None) control.Top += banner.Height;
            editor.ClientSize = new Size(editor.ClientSize.Width, editor.ClientSize.Height + banner.Height);
            editor.Controls.Add(banner);
            banner.BringToFront();
        }
    }
}

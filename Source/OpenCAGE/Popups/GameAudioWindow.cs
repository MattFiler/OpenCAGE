using OpenCAGE.Audio;
using OpenCAGE.Popups.Base;
using OpenCAGE.Theming;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE.Popups
{
    /// <summary>
    /// Game Audio (Launch Game > Recordings...): the game audio recordings (<see cref="GameAudioRecorder"/>), each measured by
    /// <see cref="AudioAnalysis"/> into a loudness graph and a list of its sounds. Record / Stop captures a game already running.
    /// </summary>
    public class GameAudioWindow : BaseWindow
    {
        private static GameAudioWindow _open;

        private readonly ComboBox _files;
        private readonly Button _browse, _folder, _record;
        private readonly Label _status, _summary;
        private readonly Graph _graph;
        private readonly ListView _list;
        private readonly Timer _timer = new Timer() { Interval = 500 };
        private AudioAnalysis.Result _result;
        private string _analysing;
        private string _wasRecording;

        /// <summary>Opens the window (or brings the open one forward), on a recording or the newest.</summary>
        public static void Open(string path = null)
        {
            if (_open == null || _open.IsDisposed)
            {
                _open = new GameAudioWindow();
                if (Singleton.Editor != null) _open.Show(Singleton.Editor); else _open.Show();
            }
            else
            {
                _open.WindowState = FormWindowState.Normal;
                _open.Activate();
            }
            _open.FillFiles(path);
        }

        public GameAudioWindow() : base(WindowClosesOn.NONE)
        {
            Text = "Game Audio";
            Icon = SharedFormIcon.Icon;
            Size = new Size(980, 640);
            MinimumSize = new Size(640, 420);
            StartPosition = FormStartPosition.CenterScreen;
            BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/launch-game/#record-game-audio");

            Panel top = new Panel() { Dock = DockStyle.Top, Height = 64, Padding = new Padding(8) };
            _files = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(8, 8), Width = 520, Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right };
            _files.SelectedIndexChanged += (s, e) => { if (_files.SelectedItem is FileItem item) Analyse(item.Path); };
            _browse = new Button() { Text = "Open...", AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _browse.Click += (s, e) => Browse();
            _folder = new Button() { Text = "Open Folder", AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _folder.Click += (s, e) => OpenFolder();
            _record = new Button() { Text = "Record Game", AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _record.Click += (s, e) => RecordOrStop();
            _status = new Label() { AutoSize = false, AutoEllipsis = true, Location = new Point(8, 38), Height = 18, Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right };
            top.Controls.Add(_files);
            top.Controls.Add(_browse);
            top.Controls.Add(_folder);
            top.Controls.Add(_record);
            top.Controls.Add(_status);
            top.Resize += (s, e) => LayoutTop(top);

            SplitContainer split = new SplitContainer() { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
            _graph = new Graph() { Dock = DockStyle.Fill };
            split.Panel1.Controls.Add(_graph);
            split.Panel1.Padding = new Padding(8, 0, 8, 4);
            _summary = new Label() { Dock = DockStyle.Top, Height = 20, AutoEllipsis = true };
            _list = new ListView() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false };
            _list.Columns.Add("Sound", 60);
            _list.Columns.Add("Starts", 80, HorizontalAlignment.Right);
            _list.Columns.Add("Ends", 80, HorizontalAlignment.Right);
            _list.Columns.Add("Length", 70, HorizontalAlignment.Right);
            _list.Columns.Add("Peak", 70, HorizontalAlignment.Right);
            _list.Columns.Add("Held at", 70, HorizontalAlignment.Right);
            _list.Columns.Add("Fade in", 70, HorizontalAlignment.Right);
            _list.Columns.Add("Fade out", 70, HorizontalAlignment.Right);
            _list.Columns.Add("After mark", 240);
            _list.SelectedIndexChanged += (s, e) => { _graph.Selected = _list.SelectedItems.Count == 0 ? null : _list.SelectedItems[0].Tag as AudioAnalysis.Sound; _graph.Invalidate(); };
            split.Panel2.Controls.Add(_list);
            split.Panel2.Controls.Add(_summary);
            split.Panel2.Padding = new Padding(8, 4, 8, 8);

            Controls.Add(split);
            Controls.Add(top);
            ThemeManager.ApplyCurrent(this);
            Load += (s, e) => { split.SplitterDistance = (int)(split.Height * 0.55); LayoutTop(top); };

            GameAudioRecorder.Changed += OnRecorderChanged;
            _timer.Tick += (s, e) => ShowStatus();
            _timer.Start();
            FormClosed += (s, e) =>
            {
                GameAudioRecorder.Changed -= OnRecorderChanged;
                _timer.Dispose();
                if (_open == this) _open = null;
            };
            ShowStatus();
        }

        private void LayoutTop(Panel top)
        {
            int right = top.ClientSize.Width - 8;
            foreach (Button button in new[] { _record, _folder, _browse })
            {
                button.Location = new Point(right - button.Width, 6);
                right = button.Left - 6;
            }
            _files.Width = Math.Max(120, right - 8 - _files.Left);
            _status.Width = top.ClientSize.Width - 16;
        }

        private sealed class FileItem
        {
            public string Path;
            public string Text;
            public override string ToString() => Text;
        }

        private void FillFiles(string select)
        {
            List<string> paths = GameAudioRecorder.Recordings(100).Select(o => o.FullName).ToList();
            foreach (string extra in new[] { GameAudioRecorder.Current?.Path, GameAudioRecorder.Last?.Path, select })
                if (extra != null && File.Exists(extra) && !paths.Contains(extra, StringComparer.OrdinalIgnoreCase)) paths.Insert(0, extra);
            _files.BeginUpdate();
            _files.Items.Clear();
            foreach (string path in paths)
            {
                FileInfo info = new FileInfo(path);
                string when = info.Exists ? info.LastWriteTime.ToString(info.LastWriteTime.Date == DateTime.Today ? "HH:mm" : "d MMM HH:mm") : "";
                _files.Items.Add(new FileItem() { Path = path, Text = info.Name + "   (" + when + (info.Exists ? ", " + (info.Length / 1048576.0).ToString("0.0") + " MB" : "") + ")" });
            }
            _files.EndUpdate();
            string want = select ?? paths.FirstOrDefault();
            int index = _files.Items.Cast<FileItem>().ToList().FindIndex(o => string.Equals(o.Path, want, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) _files.SelectedIndex = index;
            else { _result = null; _graph.Result = null; _list.Items.Clear(); _summary.Text = "No recordings yet in " + GameAudioRecorder.Folder + "."; _graph.Invalidate(); }
        }

        private void Analyse(string path)
        {
            _analysing = path;
            _summary.Text = "Measuring " + Path.GetFileName(path) + "...";
            Task.Run(() =>
            {
                AudioAnalysis.Result result = null;
                string problem = null;
                try { result = AudioAnalysis.Analyze(path, new AudioAnalysis.Options()); }
                catch (Exception e) { problem = e.Message; }
                try
                {
                    if (!IsDisposed && IsHandleCreated)
                        BeginInvoke((Action)(() => Show(path, result, problem)));
                }
                catch (Exception) { }
            });
        }

        private void Show(string path, AudioAnalysis.Result result, string problem)
        {
            if (IsDisposed || _analysing != path) return;
            _result = result;
            _graph.Result = result;
            _graph.Selected = null;
            _list.BeginUpdate();
            _list.Items.Clear();
            if (result == null)
            {
                _summary.Text = "Could not read " + Path.GetFileName(path) + ": " + problem;
            }
            else
            {
                _summary.Text = Seconds(result.Duration) + " long" + (result.StartUtc.HasValue ? ", recorded " + result.StartUtc.Value.ToLocalTime().ToString("d MMM HH:mm:ss") : "") +
                    "; peak " + Db(result.PeakDb) + ", quiet level " + Db(result.FloorDb) + "; " + result.Sounds.Count + " sound" + (result.Sounds.Count == 1 ? "" : "s") + " above " + Db(result.ThresholdDb) +
                    (result.Marks.Count != 0 ? ", " + result.Marks.Count + " mark" + (result.Marks.Count == 1 ? "" : "s") : "") + (result.PeakDb <= -150 ? " - SILENT (was the game muted in Windows' Volume mixer?)" : "");
                int n = 0;
                foreach (AudioAnalysis.Sound sound in result.Sounds.Take(2000))
                {
                    AudioAnalysis.Mark mark = result.MarkBefore(sound.Start);
                    ListViewItem item = new ListViewItem(new[]
                    {
                        (++n).ToString(), Seconds(sound.Start), Seconds(sound.End), Seconds(sound.Length), Db(sound.PeakDb), Db(sound.SustainDb), Seconds(sound.FadeIn), Seconds(sound.FadeOut),
                        mark == null ? "" : Seconds(sound.Start - mark.Seconds) + " after \"" + mark.Label + "\"",
                    }) { Tag = sound };
                    _list.Items.Add(item);
                }
            }
            _list.EndUpdate();
            _graph.Invalidate();
        }

        private static string Seconds(double s) => s.ToString("0.000", CultureInfo.InvariantCulture) + " s";
        private static string Db(double db) => db <= -150 ? "silence" : db.ToString("0.0", CultureInfo.InvariantCulture) + " dBFS";

        private void OnRecorderChanged()
        {
            try { if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)ShowStatus); }
            catch (Exception) { }
        }

        private void ShowStatus()
        {
            if (IsDisposed) return;
            GameAudioCapture capture = GameAudioRecorder.Current;
            if (capture != null)
            {
                _record.Text = "Stop";
                _status.Text = "Recording " + Path.GetFileName(capture.Path) + ": " + Math.Floor(capture.Seconds) + " s, level now " + Db(capture.RecentDb) +
                    (capture.Session.Sessions != 0 && capture.Session.Muted && !capture.Quiet ? " - Windows has the game MUTED in its Volume mixer, so this records silence" : "");
                _wasRecording = capture.Path;
                return;
            }
            _record.Text = "Record Game";
            if (_wasRecording != null)
            {
                //It just ended: listed and measured
                string ended = _wasRecording;
                _wasRecording = null;
                FillFiles(ended);
            }
            GameAudioRecorder.Summary last = GameAudioRecorder.Last;
            if (GameAudioRecorder.Armed != null)
                _status.Text = "The next game launched from OpenCAGE will be recorded.";
            else if (last?.Warning != null || last?.Problem != null)
                _status.Text = "Last recording: " + (last.Problem ?? last.Warning);
            else
                _status.Text = GameAudioRecorder.RecordLaunches ? "Every game launched from OpenCAGE is recorded until it closes (Launch Game > Record game audio)." : "Record Game records the game while it runs; Launch Game > Record game audio records every launch.";
        }

        private void RecordOrStop()
        {
            if (GameAudioRecorder.Current != null)
            {
                _record.Enabled = false;
                Task.Run(() => GameAudioRecorder.Stop()).ContinueWith(t => BeginInvoke((Action)(() => { _record.Enabled = true; ShowStatus(); })));
                return;
            }
            (int pid, string exe) = GameAudioRecorder.FindGame();
            if (pid == -1)
            {
                MessageBox.Show(this, "The game is not running. Start it first (Launch Game), or tick Record game audio in Launch Game to record every launch from when it starts.", "Record Game", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string level = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Name;
            try
            {
                GameAudioRecorder.Start(pid, new GameAudioRecorder.Request() { Level = level ?? "game", By = "Game Audio window" });
            }
            catch (GameAudioException e)
            {
                MessageBox.Show(this, "The game's audio could not be recorded: " + e.Message, "Record Game", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            ShowStatus();
        }

        private void Browse()
        {
            using (OpenFileDialog picker = new OpenFileDialog() { Filter = "WAV files (*.wav)|*.wav", Title = "Measure a recording" })
            {
                try { picker.InitialDirectory = Directory.Exists(GameAudioRecorder.Folder) ? GameAudioRecorder.Folder : null; } catch (Exception) { }
                if (picker.ShowDialog(this) == DialogResult.OK)
                    FillFiles(picker.FileName);
            }
        }

        private void OpenFolder()
        {
            try
            {
                Directory.CreateDirectory(GameAudioRecorder.Folder);
                string selected = (_files.SelectedItem as FileItem)?.Path;
                if (selected != null && File.Exists(selected)) Process.Start("explorer.exe", "/select,\"" + selected + "\"");
                else Process.Start("explorer.exe", "\"" + GameAudioRecorder.Folder + "\"");
            }
            catch (Exception) { }
        }

        /// <summary>The loudness over time (dBFS, 0 at the top, -80 at the bottom), sounds shaded, marks and onsets drawn on it.</summary>
        private sealed class Graph : Control
        {
            public AudioAnalysis.Result Result;
            public AudioAnalysis.Sound Selected;
            private Point? _mouse;

            public Graph()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                MouseMove += (s, e) => { _mouse = e.Location; Invalidate(); };
                MouseLeave += (s, e) => { _mouse = null; Invalidate(); };
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                bool dark = ThemeManager.IsDark;
                Color back = dark ? ThemeColours.Surface : Color.White, text = dark ? ThemeColours.TextDim : Color.DimGray, grid = dark ? ThemeColours.Border : Color.Gainsboro;
                Color curve = dark ? ThemeColours.Link : Color.FromArgb(0, 90, 170), shade = dark ? Color.FromArgb(60, 0, 122, 204) : Color.FromArgb(40, 0, 122, 204);
                Graphics g = e.Graphics;
                g.Clear(back);
                Rectangle plot = new Rectangle(44, 8, Math.Max(10, Width - 52), Math.Max(10, Height - 30));
                using (Pen gridPen = new Pen(grid))
                using (Brush textBrush = new SolidBrush(text))
                {
                    for (int db = 0; db >= -80; db -= 20)
                    {
                        int y = Y(plot, db);
                        g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
                        g.DrawString(db + " dB", Font, textBrush, 2, y - 7);
                    }
                    AudioAnalysis.Result r = Result;
                    if (r == null || r.FrameDb.Length == 0)
                    {
                        g.DrawString(r == null ? "No recording" : "No audio", Font, textBrush, plot.Left + 8, plot.Top + 8);
                        return;
                    }
                    double span = Math.Max(1e-3, r.To - r.From);
                    double step = NiceStep(span / Math.Max(1, plot.Width / 90));
                    for (double t = Math.Ceiling(r.From / step) * step; t <= r.To; t += step)
                    {
                        int x = X(plot, r, t);
                        g.DrawLine(gridPen, x, plot.Top, x, plot.Bottom);
                        g.DrawString(t.ToString(step < 1 ? "0.0" : "0", CultureInfo.InvariantCulture) + " s", Font, textBrush, x - 10, plot.Bottom + 3);
                    }
                    using (Brush shadeBrush = new SolidBrush(shade))
                    using (Brush selectedBrush = new SolidBrush(Color.FromArgb(70, 255, 170, 0)))
                        foreach (AudioAnalysis.Sound sound in r.Sounds)
                        {
                            int a = X(plot, r, sound.Start), b = Math.Max(a + 1, X(plot, r, sound.End));
                            g.FillRectangle(sound == Selected ? selectedBrush : shadeBrush, a, plot.Top, b - a, plot.Height);
                        }
                    //The curve: each pixel column's loudest window
                    List<PointF> points = new List<PointF>();
                    for (int x = 0; x < plot.Width; x++)
                    {
                        int a = (int)((double)x / plot.Width * r.FrameDb.Length), b = Math.Max(a + 1, (int)((double)(x + 1) / plot.Width * r.FrameDb.Length));
                        float max = -200;
                        for (int i = a; i < b && i < r.FrameDb.Length; i++) max = Math.Max(max, r.FrameDb[i]);
                        points.Add(new PointF(plot.Left + x, Y(plot, max)));
                    }
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (Pen curvePen = new Pen(curve, 1.3f))
                        if (points.Count > 1) g.DrawLines(curvePen, points.ToArray());
                    g.SmoothingMode = SmoothingMode.None;
                    using (Pen threshold = new Pen(Color.FromArgb(160, 200, 120, 0)) { DashStyle = DashStyle.Dash })
                        g.DrawLine(threshold, plot.Left, Y(plot, r.ThresholdDb), plot.Right, Y(plot, r.ThresholdDb));
                    using (Pen onset = new Pen(Color.FromArgb(0, 160, 60), 1.5f))
                        foreach (AudioAnalysis.Onset o in r.Onsets)
                        {
                            int x = X(plot, r, o.Time);
                            g.DrawLine(onset, x, plot.Bottom - 10, x, plot.Bottom);
                        }
                    using (Pen markPen = new Pen(Color.FromArgb(200, 40, 40), 1.5f))
                    using (Brush markBrush = new SolidBrush(Color.FromArgb(200, 40, 40)))
                        foreach (AudioAnalysis.Mark mark in r.Marks)
                        {
                            int x = X(plot, r, mark.Seconds);
                            g.DrawLine(markPen, x, plot.Top, x, plot.Bottom);
                            g.DrawString(mark.Label, Font, markBrush, x + 2, plot.Top + 2);
                        }
                    if (_mouse.HasValue && plot.Contains(_mouse.Value))
                    {
                        double t = r.From + (_mouse.Value.X - plot.Left) / (double)plot.Width * span;
                        AudioAnalysis.Mark mark = r.MarkBefore(t);
                        string tip = t.ToString("0.000", CultureInfo.InvariantCulture) + " s, " + Math.Round(Math.Max(-200, r.LevelDb(t - 0.025, t + 0.025)), 1) + " dBFS" +
                            (mark != null ? ", " + (t - mark.Seconds).ToString("0.000", CultureInfo.InvariantCulture) + " s after \"" + mark.Label + "\"" : "");
                        using (Pen cursor = new Pen(text) { DashStyle = DashStyle.Dot })
                            g.DrawLine(cursor, _mouse.Value.X, plot.Top, _mouse.Value.X, plot.Bottom);
                        SizeF size = g.MeasureString(tip, Font);
                        float left = Math.Min(_mouse.Value.X + 6, plot.Right - size.Width);
                        using (Brush tipBack = new SolidBrush(Color.FromArgb(220, back)))
                            g.FillRectangle(tipBack, left, plot.Bottom - 30, size.Width, size.Height);
                        g.DrawString(tip, Font, textBrush, left, plot.Bottom - 30);
                    }
                }
            }

            private static int Y(Rectangle plot, double db) => plot.Top + (int)Math.Round(Math.Min(1, Math.Max(0, -db / 80.0)) * plot.Height);
            private static int X(Rectangle plot, AudioAnalysis.Result r, double t) => plot.Left + (int)Math.Round((t - r.From) / Math.Max(1e-3, r.To - r.From) * plot.Width);

            private static double NiceStep(double raw)
            {
                foreach (double step in new[] { 0.1, 0.2, 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 1800 })
                    if (step >= raw) return step;
                return 3600;
            }
        }
    }
}

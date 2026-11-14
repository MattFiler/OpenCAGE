using Newtonsoft.Json.Linq;
using OpenCAGE.MCP;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE.Popups
{
    /// <summary>
    /// Create Entity > Create Cinematic... (Composite Display): a shot list (camera, move, lens, focus, timing) built into an in-game
    /// cutscene. Each button runs the MCP tool for the same job (build_cinematic, get_cinematic, preview_cage_animation). Shot lists are
    /// kept per level.
    /// </summary>
    public class CinematicShotBuilder : BaseWindow
    {
        private static CinematicShotBuilder _open;

        /// <summary>The window (one at a time), shown. A new one builds into <paramref name="composite"/> when it is given.</summary>
        public static CinematicShotBuilder Open(string composite = null)
        {
            if (_open == null || _open.IsDisposed)
            {
                _open = new CinematicShotBuilder();
                if (!string.IsNullOrEmpty(composite))
                    _open._composite.Text = composite;
                _open.Show();
            }
            else
                _open.BringToFront();
            return _open;
        }

        /// <summary>The Create Entity menu's entry: the window, building into <paramref name="composite"/>'s composite.</summary>
        public static ToolStripMenuItem MenuItem(Func<CATHODE.Scripting.Composite> composite)
        {
            ToolStripMenuItem item = new ToolStripMenuItem("Create Cinematic...") { ToolTipText = "Build a multi-shot cutscene in this composite from a shot list: cameras, cuts, lens, focus and timing, wired the way the game needs them (the Cinematic Shot Builder)." };
            item.Click += (s, e) => Open(composite?.Invoke()?.name);
            return item;
        }

        #region Shots as the property grid shows them
        public enum MoveKind { Static, Dolly, Path, Orbit }
        public enum EaseKind { none, @in, @out, in_out }
        public enum ClipKind { MACRO, CLOSE, MID, WIDE, ULTRA }

        /// <summary>One shot in the grid. Points are "x, y, z" or an entity's name (its resting position).</summary>
        public class ShotRow
        {
            [Category("1. Shot"), Description("A label for the shot.")] public string Name { get; set; } = "Shot";
            [Category("1. Shot"), Description("Seconds (at least 0.1).")] public float Duration { get; set; } = 3f;
            [Category("1. Shot"), Description("Events inside the shot: \"time name; time name\" (seconds into the shot). Each is an output pin on the timeline to link from.")] public string Events { get; set; } = "";

            [Category("2. Camera"), Description("Static (held), Dolly (Position -> To), Path (Position then the Path points), Orbit (round Orbit Centre).")] public MoveKind Move { get; set; } = MoveKind.Static;
            [Category("2. Camera"), Description("Where the camera starts: x, y, z.")] public string Position { get; set; } = "0, 1.7, -5";
            [Category("2. Camera"), Description("Dolly: where it ends.")] public string To { get; set; } = "";
            [Category("2. Camera"), Description("Path: the points after Position, \"x, y, z; x, y, z; ...\".")] public string Path { get; set; } = "";
            [Category("2. Camera"), Description("Path: bank into the turns like a flying camera.")] public bool Bank { get; set; }
            [Category("2. Camera"), Description("Orbit: the point (or entity) circled.")] public string OrbitCentre { get; set; } = "";
            [Category("2. Camera"), Description("Orbit: radius (m).")] public float OrbitRadius { get; set; } = 5f;
            [Category("2. Camera"), Description("Orbit: start bearing from the centre (degrees; 0 = on its +Z side).")] public float OrbitFrom { get; set; } = 0f;
            [Category("2. Camera"), Description("Orbit: end bearing (degrees).")] public float OrbitTo { get; set; } = 90f;
            [Category("2. Camera"), Description("Orbit: height above the centre (m).")] public float OrbitHeight { get; set; } = 1.5f;
            [Category("2. Camera"), Description("What it looks at: x, y, z or an entity's name. Empty: along its move (orbit: the centre; static: Rotation).")] public string LookAt { get; set; } = "0, 1.5, 0";
            [Category("2. Camera"), Description("Pan: the look point moves to this over the shot.")] public string LookTo { get; set; } = "";
            [Category("2. Camera"), Description("A fixed rotation \"pitch, yaw, roll\" when it looks at nothing.")] public string Rotation { get; set; } = "";
            [Category("2. Camera"), Description("Roll in degrees (a Dutch angle).")] public float Roll { get; set; }
            [Category("2. Camera"), Description("How the move, pan, zoom and focus pull start and stop.")] public EaseKind Ease { get; set; } = EaseKind.in_out;
            [Category("2. Camera"), Description("Handheld wander in degrees (0 = locked off; 0.5-1.5 reads as handheld).")] public float Shake { get; set; }

            [Category("3. Lens"), Description("Vertical field of view (degrees).")] public float Fov { get; set; } = 45f;
            [Category("3. Lens"), Description("Zoom: the fov at the shot's end (empty: no zoom).")] public string FovTo { get; set; } = "";
            [Category("3. Lens"), Description("Near/far clipping planes: MACRO .001/10, CLOSE .02/100, MID .05/1000, WIDE .25/5000, ULTRA 1/10000 m.")] public ClipKind ClipPlanes { get; set; } = ClipKind.MID;

            [Category("4. Focus"), Description("What is in focus: x, y, z, an entity's name, or a distance in metres. Empty: the look point.")] public string Focus { get; set; } = "";
            [Category("4. Focus"), Description("Rack focus: the focus moves to this point over the shot.")] public string FocusTo { get; set; } = "";
            [Category("4. Focus"), Description("Aperture (2 shallow, 8 deep).")] public float FNum { get; set; } = 4f;
            [Category("4. Focus"), Description("Depth-of-field lens in mm (empty: the one matching the fov; longer = more blur).")] public string FocalLength { get; set; } = "";

            public override string ToString() => Name + "  (" + Duration.ToString("0.##", CultureInfo.InvariantCulture) + " s, " + Move.ToString().ToLowerInvariant() + ")";

            public ShotRow Clone() => (ShotRow)MemberwiseClone();

            private static double[] Numbers(string text)
            {
                string[] parts = (text ?? "").Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                List<double> values = new List<double>();
                foreach (string p in parts)
                {
                    if (!double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) return null;
                    values.Add(v);
                }
                return values.ToArray();
            }

            /// <summary>"x, y, z" -> [x, y, z]; anything else -> {entity: text}; empty -> null.</summary>
            private static JToken Point(string text)
            {
                if (string.IsNullOrWhiteSpace(text)) return null;
                double[] v = Numbers(text);
                if (v != null && v.Length == 3) return new JArray(v[0], v[1], v[2]);
                return new JObject() { ["entity"] = text.Trim() };
            }

            private static string Text(JToken token)
            {
                if (token == null) return "";
                if (token is JArray a) return string.Join(", ", a.Select(o => ((double)o).ToString("0.###", CultureInfo.InvariantCulture)));
                if (token is JObject o2 && o2["entity"] != null) return (string)o2["entity"];
                if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer) return ((double)token).ToString("0.###", CultureInfo.InvariantCulture);
                return (string)token ?? "";
            }

            public JObject ToJson()
            {
                JObject s = new JObject() { ["name"] = Name, ["duration"] = Duration, ["move"] = Move.ToString().ToLowerInvariant() };
                if (Move != MoveKind.Orbit) s["position"] = Point(Position) ?? throw new InvalidOperationException(Name + ": Position is 'x, y, z'.");
                if (Move == MoveKind.Dolly) s["to"] = Point(To) ?? throw new InvalidOperationException(Name + ": a dolly needs To.");
                if (Move == MoveKind.Path)
                {
                    JArray path = new JArray();
                    foreach (string p in (Path ?? "").Split(';').Where(o => !string.IsNullOrWhiteSpace(o)))
                    {
                        double[] v = Numbers(p);
                        if (v == null || v.Length != 3) throw new InvalidOperationException(Name + ": Path is \"x, y, z; x, y, z\" (not '" + p.Trim() + "').");
                        path.Add(new JArray(v[0], v[1], v[2]));
                    }
                    s["path"] = path;
                    if (Bank) s["bank"] = true;
                }
                if (Move == MoveKind.Orbit)
                    s["orbit"] = new JObject() { ["centre"] = Point(OrbitCentre) ?? throw new InvalidOperationException(Name + ": an orbit needs Orbit Centre."), ["radius"] = OrbitRadius, ["from"] = OrbitFrom, ["to"] = OrbitTo, ["height"] = OrbitHeight };
                if (Point(LookAt) != null) s["look_at"] = Point(LookAt);
                if (Point(LookTo) != null) s["look_to"] = Point(LookTo);
                if (!string.IsNullOrWhiteSpace(Rotation)) { double[] r = Numbers(Rotation); if (r == null || r.Length != 3) throw new InvalidOperationException(Name + ": Rotation is 'pitch, yaw, roll'."); s["rotation"] = new JArray(r[0], r[1], r[2]); }
                if (Roll != 0) s["roll"] = Roll;
                s["ease"] = Ease.ToString();
                if (Shake > 0) s["shake"] = Shake;
                s["fov"] = Fov;
                if (!string.IsNullOrWhiteSpace(FovTo)) { double[] f = Numbers(FovTo); if (f == null || f.Length != 1) throw new InvalidOperationException(Name + ": Fov To is a number."); s["fov_to"] = f[0]; }
                s["clip_planes"] = ClipPlanes.ToString();
                if (!string.IsNullOrWhiteSpace(Focus))
                {
                    double[] d = Numbers(Focus);
                    s["focus"] = d != null && d.Length == 1 ? (JToken)d[0] : Point(Focus);
                }
                if (Point(FocusTo) != null) s["focus_to"] = Point(FocusTo);
                s["fnum"] = FNum;
                if (!string.IsNullOrWhiteSpace(FocalLength)) { double[] f = Numbers(FocalLength); if (f == null || f.Length != 1) throw new InvalidOperationException(Name + ": Focal Length is a number of mm."); s["focal_length"] = f[0]; }
                JArray events = new JArray();
                foreach (string e in (Events ?? "").Split(';').Where(o => !string.IsNullOrWhiteSpace(o)))
                {
                    string[] parts = e.Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2 || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double t)) throw new InvalidOperationException(Name + ": Events are \"time name; time name\".");
                    events.Add(new JObject() { ["time"] = t, ["name"] = parts[1].Trim() });
                }
                if (events.Count != 0) s["events"] = events;
                return s;
            }

            public static ShotRow FromJson(JObject s)
            {
                ShotRow r = new ShotRow();
                r.Name = (string)s["name"] ?? r.Name;
                r.Duration = (float?)s["duration"] ?? r.Duration;
                string move = (string)s["move"] ?? (s["orbit"] != null ? "orbit" : s["path"] != null ? "path" : s["to"] != null ? "dolly" : "static");
                r.Move = (MoveKind)Enum.Parse(typeof(MoveKind), move, true);
                r.Position = Text(s["position"]);
                r.To = Text(s["to"]);
                r.Path = s["path"] is JArray p ? string.Join("; ", p.Select(Text)) : "";
                r.Bank = (bool?)s["bank"] ?? false;
                if (s["orbit"] is JObject o)
                {
                    r.OrbitCentre = Text(o["centre"]);
                    r.OrbitRadius = (float?)o["radius"] ?? r.OrbitRadius;
                    r.OrbitFrom = (float?)o["from"] ?? r.OrbitFrom;
                    r.OrbitTo = (float?)o["to"] ?? r.OrbitTo;
                    r.OrbitHeight = (float?)o["height"] ?? r.OrbitHeight;
                }
                r.LookAt = Text(s["look_at"]);
                r.LookTo = Text(s["look_to"]);
                r.Rotation = Text(s["rotation"]);
                r.Roll = (float?)s["roll"] ?? 0;
                if (s["ease"] != null) r.Ease = (EaseKind)Enum.Parse(typeof(EaseKind), (string)s["ease"], true);
                r.Shake = (float?)s["shake"] ?? 0;
                r.Fov = (float?)s["fov"] ?? r.Fov;
                r.FovTo = Text(s["fov_to"]);
                if (s["clip_planes"] != null) r.ClipPlanes = (ClipKind)Enum.Parse(typeof(ClipKind), (string)s["clip_planes"], true);
                r.Focus = Text(s["focus"]);
                r.FocusTo = Text(s["focus_to"]);
                r.FNum = (float?)s["fnum"] ?? r.FNum;
                r.FocalLength = Text(s["focal_length"]);
                r.Events = s["events"] is JArray ev ? string.Join("; ", ev.OfType<JObject>().Select(x => ((double)x["time"]).ToString("0.###", CultureInfo.InvariantCulture) + " " + (string)x["name"])) : "";
                return r;
            }
        }
        #endregion

        private readonly TextBox _name = new TextBox() { Text = "Cinematic", Width = 160 };
        private readonly TextBox _composite = new TextBox() { Text = "root", Width = 260 };
        private readonly ComboBox _start = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDown, Width = 230 };
        private readonly NumericUpDown _startDelay = new NumericUpDown() { DecimalPlaces = 2, Minimum = 0, Maximum = 600, Value = 1, Width = 60, Increment = 0.25m };
        private readonly ComboBox _end = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
        private readonly NumericUpDown _blendOut = new NumericUpDown() { DecimalPlaces = 2, Minimum = 0, Maximum = 60, Width = 60, Increment = 0.25m };
        private readonly NumericUpDown _hold = new NumericUpDown() { DecimalPlaces = 2, Minimum = 0, Maximum = 600, Width = 60, Increment = 0.25m };
        private readonly CheckBox _hideHud = new CheckBox() { Text = "Hide HUD", Checked = true, AutoSize = true };
        private readonly CheckBox _noBlur = new CheckBox() { Text = "Motion blur off", Checked = true, AutoSize = true };
        private readonly CheckBox _dof = new CheckBox() { Text = "Depth of field", Checked = true, AutoSize = true };
        private readonly TextBox _cues = new TextBox() { Width = 300 };
        private readonly TextBox _clips = new TextBox() { Width = 300 };
        private readonly ListBox _shots = new ListBox() { Dock = DockStyle.Fill, IntegralHeight = false };
        private readonly PropertyGrid _grid = new PropertyGrid() { Dock = DockStyle.Fill, ToolbarVisible = false, PropertySort = PropertySort.Categorized };
        private readonly TextBox _report = new TextBox() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, Font = new Font(FontFamily.GenericMonospace, 8.25f) };
        private readonly PictureBox _picture = new PictureBox() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black };
        private readonly Label _status = new Label() { Dock = DockStyle.Bottom, Height = 22, Padding = new Padding(6, 4, 0, 0) };
        private readonly List<Button> _buttons = new List<Button>();
        private readonly List<ShotRow> _rows = new List<ShotRow>();
        private bool _busy;

        public CinematicShotBuilder() : base(WindowClosesOn.COMMANDS_RELOAD)
        {
            Text = "Cinematic Shot Builder";
            Icon = SharedFormIcon.Icon;
            Size = new Size(1180, 880);
            MinimumSize = new Size(900, 640);
            StartPosition = FormStartPosition.CenterScreen;
            EnableDocumentation("https://opencage.co.uk/docs/cinematic-shot-builder/");

            _start.Items.AddRange(new object[] { "level_start", "none" });
            _start.Text = "level_start";
            _end.Items.AddRange(new object[] { "return", "hold", "loop" });
            _end.SelectedIndex = 0;
            ToolTip tip = new ToolTip();
            tip.SetToolTip(_start, "level_start: after the level has loaded (and the delay). none: link something to <name>_Begin.trigger yourself. Or type 'Entity.pin', e.g. PlayerTriggerBox_1.on_entered.");
            tip.SetToolTip(_cues, "Things to call at times, as JSON: [{\"time\": 2.5, \"call\": \"Door_1.open\"}]");
            tip.SetToolTip(_clips, "Character clips locked to the timeline, as JSON: [{\"entity\": \"Anim_1\", \"at\": 1.0, \"start\": \"Bind_1.trigger\"}] (a CMD_PlayAnimation; clip time 0 at 'at').");

            FlowLayoutPanel top = new FlowLayoutPanel() { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6) };
            top.Controls.AddRange(new Control[] { Caption("Name"), _name, Caption("In composite"), _composite, Caption("Starts"), _start, Caption("after (s)"), _startDelay, Caption("End"), _end, Caption("blend out (s)"), _blendOut, Caption("hold (s)"), _hold });
            FlowLayoutPanel options = new FlowLayoutPanel() { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6, 0, 6, 4) };
            options.Controls.AddRange(new Control[] { _hideHud, _noBlur, _dof, Caption("Cues"), _cues, Caption("Clip sync"), _clips });

            //Shots: list + buttons | grid
            Panel listPanel = new Panel() { Dock = DockStyle.Fill };
            FlowLayoutPanel listButtons = new FlowLayoutPanel() { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            listButtons.Controls.AddRange(new Control[] { Btn("Add", AddShot), Btn("Copy", CopyShot), Btn("Remove", RemoveShot), Btn("Up", () => MoveShot(-1)), Btn("Down", () => MoveShot(1)), Btn("From Viewport", FromViewport, 110) });
            listPanel.Controls.Add(_shots);
            listPanel.Controls.Add(listButtons);
            SplitContainer shotsSplit = new SplitContainer() { Dock = DockStyle.Fill, SplitterDistance = 320 };
            shotsSplit.Panel1.Controls.Add(listPanel);
            shotsSplit.Panel2.Controls.Add(_grid);

            //Report | preview picture
            SplitContainer resultSplit = new SplitContainer() { Dock = DockStyle.Fill };
            resultSplit.Panel1.Controls.Add(_report);
            resultSplit.Panel2.Controls.Add(_picture);
            SplitContainer main = new SplitContainer() { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
            main.Panel1.Controls.Add(shotsSplit);
            main.Panel2.Controls.Add(resultSplit);

            FlowLayoutPanel actions = new FlowLayoutPanel() { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(6) };
            actions.Controls.AddRange(new Control[] { Btn("Plan", () => Build(true), 90), Btn("Build", () => Build(false), 90), Btn("Preview Shot", PreviewShot, 110), Btn("Read Back", ReadBack, 100), Btn("Open...", OpenFile, 80), Btn("Save As...", SaveFile, 90) });

            Controls.Add(main);
            Controls.Add(actions);
            Controls.Add(options);
            Controls.Add(top);
            Controls.Add(_status);
            Load += (s, e) =>
            {
                try { main.SplitterDistance = Math.Max(240, main.Height - 260); shotsSplit.SplitterDistance = 320; resultSplit.SplitterDistance = Math.Max(300, resultSplit.Width - 460); } catch (InvalidOperationException) { }
            };

            _shots.SelectedIndexChanged += (s, e) => _grid.SelectedObject = _shots.SelectedIndex >= 0 ? _rows[_shots.SelectedIndex] : null;
            _grid.PropertyValueChanged += (s, e) => RefreshList(_shots.SelectedIndex);
            LoadKept();
            if (_rows.Count == 0) { AddShot(); }
            _status.Text = "Plan only reports; Build changes the level (one undo step - Save and Build to see it in game). Preview Shot needs the viewport on.";
        }

        private static Label Caption(string text) => new Label() { Text = text, AutoSize = true, Padding = new Padding(4, 6, 0, 0) };

        private Button Btn(string text, Action click, int width = 70)
        {
            Button b = new Button() { Text = text, Width = width, Height = 26, UseVisualStyleBackColor = true };
            b.Click += (s, e) => { if (!_busy) click(); };
            _buttons.Add(b);
            return b;
        }

        #region The list
        private void RefreshList(int select)
        {
            _shots.BeginUpdate();
            _shots.Items.Clear();
            for (int i = 0; i < _rows.Count; i++) _shots.Items.Add((i + 1) + ". " + _rows[i]);
            _shots.EndUpdate();
            if (_rows.Count != 0) _shots.SelectedIndex = Math.Max(0, Math.Min(_rows.Count - 1, select));
            else _grid.SelectedObject = null;
        }

        private void AddShot()
        {
            ShotRow row = _shots.SelectedIndex >= 0 ? _rows[_shots.SelectedIndex].Clone() : new ShotRow();
            row.Name = "Shot " + (_rows.Count + 1);
            int at = _shots.SelectedIndex >= 0 ? _shots.SelectedIndex + 1 : _rows.Count;
            _rows.Insert(at, row);
            RefreshList(at);
        }

        private void CopyShot()
        {
            if (_shots.SelectedIndex < 0) return;
            ShotRow row = _rows[_shots.SelectedIndex].Clone();
            row.Name += " copy";
            _rows.Insert(_shots.SelectedIndex + 1, row);
            RefreshList(_shots.SelectedIndex + 1);
        }

        private void RemoveShot()
        {
            if (_shots.SelectedIndex < 0 || _rows.Count <= 1) return;
            int at = _shots.SelectedIndex;
            _rows.RemoveAt(at);
            RefreshList(at);
        }

        private void MoveShot(int by)
        {
            int at = _shots.SelectedIndex, to = at + by;
            if (at < 0 || to < 0 || to >= _rows.Count) return;
            ShotRow row = _rows[at];
            _rows.RemoveAt(at);
            _rows.Insert(to, row);
            RefreshList(to);
        }

        /// <summary>The selected shot's camera at the viewport camera: its position, looking where the viewport looks.</summary>
        private void FromViewport()
        {
            if (_shots.SelectedIndex < 0) return;
            ShotRow row = _rows[_shots.SelectedIndex];
            Busy(true, "Reading the viewport camera...");
            Task.Run(() =>
            {
                McpTool any = McpTools.Find("build_cinematic");
                McpViewportTools.ViewportCamera camera = McpViewportTools.QueryCamera(new McpCall(any, new JObject(), CancellationToken.None, null));
                return camera;
            }).ContinueWith(t =>
            {
                Busy(false, null);
                if (t.Exception != null) { _status.Text = "No viewport camera: " + t.Exception.InnerException?.Message; return; }
                McpViewportTools.ViewportCamera c = t.Result;
                System.Numerics.Vector3 look = c.Position + c.Forward * 10f;
                row.Position = V(c.Position);
                row.LookAt = V(look);
                row.Fov = (float)Math.Round(c.Fov, 1);
                RefreshList(_shots.SelectedIndex);
                _grid.Refresh();
                _status.Text = "Shot " + (_shots.SelectedIndex + 1) + " takes the viewport camera (" + c.Space + ")" + (c.InLevelSpace ? "." : ": the viewport shows a composite, so those are that composite's points - set the space with 'In composite'.");
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private static string V(System.Numerics.Vector3 v) => string.Format(CultureInfo.InvariantCulture, "{0:0.###}, {1:0.###}, {2:0.###}", v.X, v.Y, v.Z);
        #endregion

        #region Building
        private JObject Args(bool dry)
        {
            JObject args = new JObject()
            {
                ["composite"] = string.IsNullOrWhiteSpace(_composite.Text) ? "root" : _composite.Text.Trim(),
                ["name"] = _name.Text.Trim(),
                ["shots"] = new JArray(_rows.Select(o => o.ToJson())),
                ["start"] = _start.Text.Trim(),
                ["start_delay"] = (double)_startDelay.Value,
                ["end"] = _end.Text,
                ["hide_hud"] = _hideHud.Checked,
                ["motion_blur"] = _noBlur.Checked ? "off" : "keep",
                ["dof"] = _dof.Checked,
            };
            if (_blendOut.Value > 0) args["blend_out"] = (double)_blendOut.Value;
            if (_hold.Value > 0) args["hold"] = (double)_hold.Value;
            if (!string.IsNullOrWhiteSpace(_cues.Text)) args["cues"] = JArray.Parse(_cues.Text);
            if (!string.IsNullOrWhiteSpace(_clips.Text)) args["sync_clips"] = JArray.Parse(_clips.Text);
            if (dry) args["dry_run"] = true;
            return args;
        }

        private void Build(bool dry)
        {
            JObject args;
            try { args = Args(dry); }
            catch (Exception e) { _report.Text = e.Message; _status.Text = "Not built: fix the shot list."; return; }
            Run("build_cinematic", args, dry ? "Planned: nothing changed." : "Built (one undo step): Save and Build to see it in game.", null);
        }

        private void ReadBack() => Run("get_cinematic", new JObject() { ["composite"] = _composite.Text.Trim(), ["name"] = _name.Text.Trim() }, "Read back.", null);

        private void PreviewShot()
        {
            int shot = Math.Max(0, _shots.SelectedIndex);
            float from = _rows.Take(shot).Sum(o => o.Duration);
            float mid = from + _rows[shot].Duration * 0.5f;
            JObject args = new JObject()
            {
                ["composite"] = _composite.Text.Trim(),
                ["entity"] = _name.Text.Trim() + "_Timeline",
                ["view"] = "camera",
                ["camera"] = new JArray(_name.Text.Trim() + "_Cam" + (shot + 1).ToString("00")),
                ["times"] = new JArray(Math.Round(from + 0.05, 3), Math.Round(mid, 3), Math.Round(from + _rows[shot].Duration - 0.05, 3)),
            };
            Run("preview_cage_animation", args, "Shot " + (shot + 1) + " at its start, middle and end (as built; Build again after changes).", null);
        }

        private void Busy(bool busy, string text)
        {
            _busy = busy;
            UseWaitCursor = busy;
            foreach (Button b in _buttons) b.Enabled = !busy;
            if (text != null) _status.Text = text;
        }

        private void Run(string toolName, JObject args, string done, Action<JObject> after)
        {
            McpTool tool = McpTools.Find(toolName);
            if (tool == null) { _report.Text = "The " + toolName + " tool is missing from this build."; return; }
            Busy(true, "Working...");
            Task.Run(() =>
            {
                McpCinematicTools.FromWindow = true;
                try { return tool.Invoke(new McpCall(tool, args, CancellationToken.None, (text, d, total) => { try { BeginInvoke(new Action(() => _status.Text = text)); } catch { } })); }
                finally { McpCinematicTools.FromWindow = false; }
            }).ContinueWith(t =>
            {
                Busy(false, null);
                if (t.Exception != null) { _report.Text = "Failed: " + t.Exception.InnerException?.Message; _status.Text = "Failed."; return; }
                JObject response = t.Result;
                JArray content = (JArray)response["content"] ?? new JArray();
                string text = string.Concat(content.Where(c => (string)c["type"] == "text").Select(c => (string)c["text"]));
                JObject image = content.OfType<JObject>().FirstOrDefault(c => (string)c["type"] == "image");
                if (image != null)
                {
                    try
                    {
                        using (MemoryStream ms = new MemoryStream(Convert.FromBase64String((string)image["data"])))
                            _picture.Image = new Bitmap(Image.FromStream(ms));
                    }
                    catch { }
                }
                if (response["isError"]?.Value<bool>() == true) { _report.Text = text; _status.Text = "Not done: see the report."; return; }
                JObject result = null;
                try { result = JObject.Parse(text); } catch { }
                _report.Text = result != null ? CAGEAnimationEditor.Describe(result) : text;
                _status.Text = done;
                if (result != null) after?.Invoke(result);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
        #endregion

        #region Shot list files
        private void Apply(JObject list)
        {
            if (list["name"] != null) _name.Text = (string)list["name"];
            if (list["composite"] != null) _composite.Text = (string)list["composite"];
            if (list["start"] != null) _start.Text = (string)list["start"];
            if (list["start_delay"] != null) _startDelay.Value = Math.Max(_startDelay.Minimum, Math.Min(_startDelay.Maximum, (decimal)(double)list["start_delay"]));
            if (list["end"] != null) _end.Text = (string)list["end"];
            _blendOut.Value = list["blend_out"] != null ? (decimal)(double)list["blend_out"] : 0;
            _hold.Value = list["hold"] != null ? (decimal)(double)list["hold"] : 0;
            _hideHud.Checked = (bool?)list["hide_hud"] ?? true;
            _noBlur.Checked = ((string)list["motion_blur"] ?? "off") == "off";
            _dof.Checked = (bool?)list["dof"] ?? true;
            _cues.Text = list["cues"] != null ? list["cues"].ToString(Newtonsoft.Json.Formatting.None) : "";
            _clips.Text = list["sync_clips"] != null ? list["sync_clips"].ToString(Newtonsoft.Json.Formatting.None) : "";
            _rows.Clear();
            foreach (JObject s in (list["shots"] as JArray ?? new JArray()).OfType<JObject>()) _rows.Add(ShotRow.FromJson(s));
            if (_rows.Count == 0) _rows.Add(new ShotRow() { Name = "Shot 1" });
            RefreshList(0);
        }

        /// <summary>The last shot list built on this level (kept beside OpenCAGE's settings), if any.</summary>
        private void LoadKept()
        {
            try
            {
                string level = Content?.Level?.Name;
                if (level == null) return;
                string file = McpCinematicTools.StoreFile(level, "", "x");
                string folder = System.IO.Path.GetDirectoryName(file);
                if (!Directory.Exists(folder)) return;
                FileInfo latest = new DirectoryInfo(folder).GetFiles("*.json").OrderByDescending(o => o.LastWriteTimeUtc).FirstOrDefault();
                if (latest == null) return;
                Apply(JObject.Parse(File.ReadAllText(latest.FullName)));
                _status.Text = "Opened the last shot list built on this level (" + latest.Name + ").";
            }
            catch { }
        }

        private void OpenFile()
        {
            using (OpenFileDialog dialog = new OpenFileDialog() { Filter = "Shot list (*.json)|*.json" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try { Apply(JObject.Parse(File.ReadAllText(dialog.FileName))); }
                catch (Exception e) { _report.Text = "That file is not a shot list: " + e.Message; }
            }
        }

        private void SaveFile()
        {
            JObject args;
            try { args = Args(false); }
            catch (Exception e) { _report.Text = e.Message; return; }
            using (SaveFileDialog dialog = new SaveFileDialog() { Filter = "Shot list (*.json)|*.json", FileName = _name.Text.Trim() + ".json" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                File.WriteAllText(dialog.FileName, args.ToString());
                _status.Text = "Saved " + dialog.FileName + ".";
            }
        }
        #endregion
    }
}

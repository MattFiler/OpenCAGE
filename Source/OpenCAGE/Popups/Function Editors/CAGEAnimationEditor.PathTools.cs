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
    /// <summary>
    /// CAGEAnimation editor path tools: Fly Along a Path, Ride Along With and Simplify Keys. Each runs the MCP tool for the same job
    /// (animate_camera_path 'fly', ride_along, simplify_keys) as one undo step.
    /// </summary>
    public partial class CAGEAnimationEditor
    {
        private Button _pathToolsBtn;

        private void SetupPathTools()
        {
            ContextMenuStrip tools = new ContextMenuStrip();
            tools.Items.Add(new ToolStripMenuItem("Fly Along a Path…", null, (s, e) => OpenFlyDialog(ContextPath())) { ToolTipText = "Key an entity flying through points (a SplinePath's, or its own position keys) with banking, a speed plan and sparse keys." });
            tools.Items.Add(new ToolStripMenuItem("Ride Along With…", null, (s, e) => OpenRideDialog(ContextPath())) { ToolTipText = "Key an entity with another entity's motion, so it moves, turns and banks with it on the same clock." });
            tools.Items.Add(new ToolStripMenuItem("Simplify Keys…", null, (s, e) => OpenSimplifyDialog(ContextPath())) { ToolTipText = "Thin the keys to the fewest that play within a tolerance of how the animation plays now." });

            _pathToolsBtn = new Button()
            {
                Text = "Path Tools ▾",
                Size = new Size(104, 26),
                UseVisualStyleBackColor = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
            };
            _pathToolsBtn.Location = new Point(bezierMode.Right + 16, bezierMode.Top - 3);
            _pathToolsBtn.Click += (s, e) => tools.Show(_pathToolsBtn, new Point(0, _pathToolsBtn.Height));
            Controls.Add(_pathToolsBtn);
            new ToolTip().SetToolTip(_pathToolsBtn, "Fly along a path with banking, ride along with another entity, simplify keys.");

            //The same three on the track list's right-click, for the entity clicked
            ContextMenuStrip menu = trackTree.ContextMenuStrip;
            if (menu != null)
            {
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(new ToolStripMenuItem("Fly Along a Path…", null, (s, e) => OpenFlyDialog(ContextPath())));
                menu.Items.Add(new ToolStripMenuItem("Ride Along With…", null, (s, e) => OpenRideDialog(ContextPath())));
                menu.Items.Add(new ToolStripMenuItem("Simplify Keys…", null, (s, e) => OpenSimplifyDialog(ContextPath())));
            }
        }

        /// <summary>The entity the right-click (or selection) is on, or null.</summary>
        private EntityPath ContextPath()
        {
            TreeNode node = _trackTreeContextNode ?? trackTree.SelectedNode;
            if (node?.Tag is EntityNodeTag entity) return entity.Path;
            if (node?.Tag is ParamNodeTag param) return param.Path;
            return null;
        }

        #region Targets
        private static JArray PathIds(EntityPath path) => new JArray((path?.path ?? new ShortGuid[0]).Where(o => !o.IsInvalid).Select(o => (object)McpScript.Id(o)).ToArray());

        private string PathLabel(EntityPath path)
        {
            try { return Content.Level.Commands.Utils.GetResolvedAsString(Content.Level.Commands.Utils.ResolveEntityPath(path, _animComposite), false); }
            catch { return string.Join("/", PathIds(path)); }
        }

        private static readonly ShortGuid PositionParam = ShortGuidUtils.Generate("position");

        /// <summary>Everything this animation moves (its position tracks), by path.</summary>
        private List<(EntityPath path, string label)> TransformTargets()
        {
            List<(EntityPath, string)> found = new List<(EntityPath, string)>();
            foreach (CAGEAnimation.Connection connection in animEntity.connections)
            {
                if (connection?.connectedEntity?.path == null || connection.target_param != PositionParam) continue;
                if (found.Any(o => McpScriptEdit.SamePath(o.Item1.path, connection.connectedEntity.path))) continue;
                found.Add((connection.connectedEntity, PathLabel(connection.connectedEntity)));
            }
            return found;
        }

        /// <summary>The target's x / y / z position tracks in this window's copy.</summary>
        private Dictionary<string, CAGEAnimation.FloatTrack> PositionTracks(EntityPath path)
        {
            Dictionary<string, CAGEAnimation.FloatTrack> tracks = new Dictionary<string, CAGEAnimation.FloatTrack>();
            foreach (CAGEAnimation.Connection connection in animEntity.connections)
            {
                if (connection?.connectedEntity?.path == null || connection.target_param != PositionParam || !McpScriptEdit.SamePath(connection.connectedEntity.path, path.path)) continue;
                string component = McpCinematicTools.ComponentOf(connection.target_sub_param);
                CAGEAnimation.FloatTrack track = animEntity.floatTracks.FirstOrDefault(o => o.shortGUID == connection.target_track);
                if (track != null && component.Length != 0) tracks[component] = track;
            }
            return tracks;
        }

        /// <summary>Entities directly in the animation's composite that have a place (anything with a position): riders.</summary>
        private List<(Entity entity, string label)> PlacedEntities()
        {
            List<(Entity, string)> list = new List<(Entity, string)>();
            Commands commands = Content.Level.Commands;
            foreach (Entity entity in _animComposite.GetEntities())
            {
                if (!(entity is FunctionEntity function) || entity is CAGEAnimation) continue;
                bool placed = entity.GetParameter(PositionParam) != null;
                if (!placed)
                    try { placed = commands.Utils.GetAllParameters(entity, _animComposite).Any(o => o.Item1 == PositionParam); } catch { }
                if (placed) list.Add((entity, commands.Utils.GetEntityName(_animComposite, entity)));
                if (list.Count > 3000) break;
            }
            return list.OrderBy(o => o.Item2, StringComparer.OrdinalIgnoreCase).ToList();
        }
        #endregion

        #region Fly
        private void OpenFlyDialog(EntityPath preferred)
        {
            List<(EntityPath path, string label)> targets = TransformTargets();
            if (targets.Count == 0)
            {
                MessageBox.Show("This animation moves nothing yet. Add the entity to fly (Add Entity…, then its position), key a few rough positions for it (Animation Mode makes that quick), then fly it through them - or through a SplinePath's points.", "Fly Along a Path", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            List<FunctionEntity> splines = _animComposite.functions.Where(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.SplinePath).ToList();
            PathToolDialog d = new PathToolDialog("Fly Along a Path", "Keys the entity flying through the points: nose along the path, banked into turns like an aircraft, at a steady or planned speed, with keys only where the curve needs one. One undo step.");
            ComboBox target = d.Combo("Entity", targets.Select(o => o.label), Math.Max(0, targets.FindIndex(o => preferred != null && McpScriptEdit.SamePath(o.path.path, preferred.path))));
            List<string> sources = new List<string>() { "Its own position keys (fly through them)" };
            sources.AddRange(splines.Select(o => "SplinePath " + Content.Level.Commands.Utils.GetEntityName(_animComposite, o)));
            ComboBox source = d.Combo("Through", sources, 0);
            NumericUpDown duration = d.Number("Duration (s, 0 = from the keys / speed)", 0, 0, 3600, 2);
            NumericUpDown speed = d.Number("Speed (m/s, with no duration)", 10, 0.1m, 1000, 1);
            NumericUpDown start = d.Number("Starts at (s)", 0, 0, 3600, 2);
            CheckBox closed = d.Check("Closed loop", false);
            ComboBox ease = d.Combo("Ease", new[] { "none", "in", "out", "in_out" }, 3);
            CheckBox bank = d.Check("Bank into turns", true);
            NumericUpDown maxBank = d.Number("Max bank (degrees)", 60, 0, 89, 0);
            NumericUpDown bankScale = d.Number("Bank scale", 1, 0, 5, 2);
            CheckBox level = d.Check("Keep the nose level", false);
            NumericUpDown lag = d.Number("Attitude lag (s, heavy craft ~0.45)", 0, 0, 10, 2);
            NumericUpDown climb = d.Number("Speed gained per m of dive (m/s)", 0, -10, 10, 2);
            NumericUpDown tolMm = d.Number("Key tolerance (mm)", 2, 0.1m, 1000, 1);
            NumericUpDown tolDeg = d.Number("Key tolerance (degrees)", 0.02m, 0.001m, 5, 3);
            //From the target's own keys: the span and start time they already have
            Action fromKeys = () =>
            {
                if (source.SelectedIndex != 0 || target.SelectedIndex < 0) return;
                List<float> times = KeyTimes(targets[target.SelectedIndex].path);
                if (times.Count >= 2) { start.Value = (decimal)Math.Round(times[0], 2); }
            };
            target.SelectedIndexChanged += (s, e) => fromKeys();
            source.SelectedIndexChanged += (s, e) =>
            {
                fromKeys();
                if (source.SelectedIndex > 0)
                {
                    FunctionEntity spline = splines[source.SelectedIndex - 1];
                    if (spline.GetParameter("loop")?.content is cBool loop) closed.Checked = loop.value;
                }
            };
            fromKeys();
            d.OnApply = () =>
            {
                EntityPath path = targets[target.SelectedIndex].path;
                JArray points = new JArray();
                float? span = null;
                if (source.SelectedIndex == 0)
                {
                    Dictionary<string, CAGEAnimation.FloatTrack> tracks = PositionTracks(path);
                    List<float> times = KeyTimes(path);
                    if (times.Count < 2) throw new InvalidOperationException("Its position has fewer than 2 keys to fly through: key a few positions first, or pick a SplinePath.");
                    foreach (float t in times)
                    {
                        float V(string c) => tracks.TryGetValue(c, out CAGEAnimation.FloatTrack tr) && tr.keyframes.Count != 0 ? CageAnimationCurves.ValueAt(tr, t, bezierMode.Checked) : 0f;
                        points.Add(new JArray(V("x"), V("y"), V("z")));
                    }
                    span = times[times.Count - 1] - times[0];
                }
                else
                {
                    if (path.path.Count(o => !o.IsInvalid) > 1) throw new InvalidOperationException("A SplinePath's points are in this composite's space: fly an entity that sits directly in it (not inside an instance), or fly through its own keys.");
                    FunctionEntity spline = splines[source.SelectedIndex - 1];
                    if (!(spline.GetParameter("points")?.content is cSpline cs) || cs.splinePoints.Count < 2) throw new InvalidOperationException("That SplinePath has fewer than 2 points.");
                    cTransform frame = InstanceTransform.TransformOf(spline) ?? new cTransform(Vector3.Zero, Vector3.Zero);
                    foreach (cTransform p in cs.splinePoints)
                    {
                        Vector3 at = InstanceTransform.PointToWorld(frame, p.position);
                        points.Add(new JArray(at.X, at.Y, at.Z));
                    }
                }
                JObject args = Base();
                args["camera"] = PathIds(path);
                args["points"] = points;
                args["space"] = "composite";
                args["start_time"] = (double)start.Value;
                args["closed"] = closed.Checked;
                args["ease"] = ease.Text;
                args["clearance"] = 0;
                if (duration.Value > 0) args["duration"] = (double)duration.Value;
                else if (span != null && span.Value > 0.05f) args["duration"] = Math.Round(span.Value, 4);
                else args["speed"] = (double)speed.Value;
                args["fly"] = new JObject()
                {
                    ["bank"] = bank.Checked,
                    ["max_bank"] = (double)maxBank.Value,
                    ["bank_scale"] = (double)bankScale.Value,
                    ["level"] = level.Checked,
                    ["attitude_lag"] = (double)lag.Value,
                    ["climb_trade"] = (double)climb.Value,
                    ["position_tolerance"] = (double)tolMm.Value / 1000.0,
                    ["rotation_tolerance"] = (double)tolDeg.Value,
                };
                return ("animate_camera_path", args);
            };
            ShowTool(d);
        }

        /// <summary>The distinct times of a target's x / y / z keys, in order.</summary>
        private List<float> KeyTimes(EntityPath path)
        {
            List<float> times = new List<float>();
            foreach (KeyValuePair<string, CAGEAnimation.FloatTrack> track in PositionTracks(path).Where(o => o.Key == "x" || o.Key == "y" || o.Key == "z"))
                foreach (CAGEAnimation.FloatTrack.Keyframe key in track.Value.keyframes)
                    if (!times.Any(o => Math.Abs(o - key.time) < 0.001f)) times.Add(key.time);
            times.Sort();
            return times;
        }
        #endregion

        #region Ride
        private void OpenRideDialog(EntityPath preferred)
        {
            List<(EntityPath path, string label)> leaders = TransformTargets();
            if (leaders.Count == 0)
            {
                MessageBox.Show("This animation moves nothing yet: key the leader (the vehicle) first, then make things ride along with it.", "Ride Along With", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            List<(Entity entity, string label)> riders = PlacedEntities();
            PathToolDialog d = new PathToolDialog("Ride Along With", "Keys the rider with the leader's motion and where the rider sits relative to it, so they move together frame for frame on this animation's clock (a camera cannot be attached by a link, and an animated body trails a keyed vehicle). One undo step.");
            int riderIndex = -1;
            if (preferred != null)
            {
                ShortGuid[] ids = preferred.path.Where(o => !o.IsInvalid).ToArray();
                if (ids.Length == 1) riderIndex = riders.FindIndex(o => o.entity.shortGUID == ids[0]);
            }
            ComboBox rider = d.Combo("Rider", riders.Select(o => o.label), Math.Max(0, riderIndex));
            int leaderIndex = leaders.FindIndex(o => preferred != null && !McpScriptEdit.SamePath(o.path.path, preferred.path));
            ComboBox leader = d.Combo("Rides with", leaders.Select(o => o.label), Math.Max(0, leaderIndex));
            ComboBox offset = d.Combo("Offset", new[] { "Keep where it is now, relative to the leader", "None (it was modelled in the leader's own space)" }, 0);
            NumericUpDown attach = d.Number("Offset taken at (s)", 0, 0, 3600, 2);
            NumericUpDown tolMm = d.Number("Key tolerance (mm)", 2, 0.1m, 1000, 1);
            NumericUpDown tolDeg = d.Number("Key tolerance (degrees)", 0.02m, 0.001m, 5, 3);
            Action firstKey = () =>
            {
                if (leader.SelectedIndex < 0) return;
                List<float> times = KeyTimes(leaders[leader.SelectedIndex].path);
                if (times.Count != 0) attach.Value = (decimal)Math.Round(times[0], 2);
            };
            leader.SelectedIndexChanged += (s, e) => firstKey();
            firstKey();
            d.OnApply = () =>
            {
                if (rider.SelectedIndex < 0) throw new InvalidOperationException("Pick the rider.");
                JObject args = Base();
                args["rider"] = new JArray(McpScript.Id(riders[rider.SelectedIndex].entity.shortGUID));
                args["leader"] = PathIds(leaders[leader.SelectedIndex].path);
                args["offset"] = offset.SelectedIndex == 0 ? "keep" : "none";
                args["attach_time"] = (double)attach.Value;
                args["position_tolerance"] = (double)tolMm.Value / 1000.0;
                args["rotation_tolerance"] = (double)tolDeg.Value;
                return ("ride_along", args);
            };
            ShowTool(d);
        }
        #endregion

        #region Simplify
        private void OpenSimplifyDialog(EntityPath preferred)
        {
            List<(EntityPath path, string label)> targets = new List<(EntityPath, string)>();
            foreach (CAGEAnimation.Connection connection in animEntity.connections)
                if (connection?.connectedEntity?.path != null && !targets.Any(o => McpScriptEdit.SamePath(o.Item1.path, connection.connectedEntity.path)))
                    targets.Add((connection.connectedEntity, PathLabel(connection.connectedEntity)));
            if (animEntity.floatTracks.Sum(o => o?.keyframes?.Count ?? 0) == 0)
            {
                MessageBox.Show("This animation has no keys to simplify.", "Simplify Keys", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            PathToolDialog d = new PathToolDialog("Simplify Keys", "Keeps the fewest of the keys that play within the tolerance of how the animation plays now (checked 240 times a second). Untouched stretches play exactly as before; cuts are kept. One undo step - Check first to see what would go.");
            List<string> scopes = new List<string>() { "Every track" };
            scopes.AddRange(targets.Select(o => o.label));
            ComboBox scope = d.Combo("Tracks of", scopes, Math.Max(0, 1 + targets.FindIndex(o => preferred != null && McpScriptEdit.SamePath(o.path.path, preferred.path))));
            NumericUpDown tolMm = d.Number("Positions (mm)", 2, 0.01m, 1000, 2);
            NumericUpDown tolDeg = d.Number("Angles (degrees)", 0.02m, 0.001m, 10, 3);
            NumericUpDown tolValue = d.Number("Other values (0 = 0.1% of each track's range)", 0, 0, 100000, 4);
            Func<bool, (string, JObject)> make = dry =>
            {
                JObject args = Base();
                if (scope.SelectedIndex > 0) args["targets"] = new JArray(PathIds(targets[scope.SelectedIndex - 1].path));
                args["position_tolerance"] = (double)tolMm.Value / 1000.0;
                args["rotation_tolerance"] = (double)tolDeg.Value;
                if (tolValue.Value > 0) args["value_tolerance"] = (double)tolValue.Value;
                if (dry) args["dry_run"] = true;
                return ("simplify_keys", args);
            };
            d.OnApply = () => make(false);
            d.OnCheck = () => make(true);
            ShowTool(d);
        }
        #endregion

        #region Running
        private JObject Base() => new JObject() { ["composite"] = _animComposite.name, ["entity"] = McpScript.Id(animEntity.shortGUID) };

        private void ShowTool(PathToolDialog d)
        {
            d.Run = (tool, args) =>
            {
                //Whatever this window holds goes in first, so the tool works on it
                CommitLiveStructural(null);
                return RunToolAsync(tool, args);
            };
            d.Show(this);
        }

        /// <summary>Run an MCP tool off the UI thread, as the user's own edit; the result as text, or the error.</summary>
        internal static Task<(bool ok, string text, JObject result)> RunToolAsync(string toolName, JObject args)
        {
            McpTool tool = McpTools.Find(toolName);
            if (tool == null) return Task.FromResult((false, "The " + toolName + " tool is missing from this build.", (JObject)null));
            return Task.Run(() =>
            {
                McpCinematicTools.FromWindow = true;
                try
                {
                    JObject response = tool.Invoke(new McpCall(tool, args, CancellationToken.None, null));
                    string text = string.Concat(((JArray)response["content"] ?? new JArray()).Where(c => (string)c["type"] == "text").Select(c => (string)c["text"]));
                    if (response["isError"]?.Value<bool>() == true) return (false, text, (JObject)null);
                    JObject result = null;
                    try { result = JObject.Parse(text); } catch { }
                    return (true, result != null ? Describe(result) : text, result);
                }
                catch (Exception e) { return (false, e.Message, (JObject)null); }
                finally { McpCinematicTools.FromWindow = false; }
            });
        }

        /// <summary>A tool's result as readable lines: warnings first, then the rest indented.</summary>
        internal static string Describe(JObject result)
        {
            List<string> lines = new List<string>();
            if (result["warnings"] is JArray warnings && warnings.Count != 0)
            {
                foreach (JToken w in warnings) lines.Add("! " + (string)w);
                lines.Add("");
            }
            void Walk(JToken token, string indent, string key)
            {
                if (key == "warnings" || lines.Count > 400) return;
                if (token is JObject o)
                {
                    if (key != null) lines.Add(indent + key + ":");
                    foreach (JProperty p in o.Properties()) Walk(p.Value, key == null ? indent : indent.Replace("-", " ") + "  ", p.Name);
                }
                else if (token is JArray a && a.Any(x => x is JObject))
                {
                    lines.Add(indent + key + ":");
                    foreach (JToken x in a.Take(60))
                    {
                        if (!(x is JObject item)) { Walk(x, indent + "  - ", null); continue; }
                        bool first = true;
                        foreach (JProperty p in item.Properties())
                        {
                            Walk(p.Value, indent + (first ? "  - " : "    "), p.Name);
                            first = false;
                        }
                    }
                    if (a.Count > 60) lines.Add(indent + "  ... " + (a.Count - 60) + " more");
                }
                else
                    lines.Add(indent + (key != null ? key + ": " : "") + token.ToString(Newtonsoft.Json.Formatting.None).Trim('"'));
            }
            Walk(result, "", null);
            return string.Join(Environment.NewLine, lines);
        }
        #endregion

        /// <summary>A small tool window: rows of settings, Check / Apply, and the result.</summary>
        internal sealed class PathToolDialog : Form
        {
            private readonly TableLayoutPanel _rows = new TableLayoutPanel() { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(8) };
            private readonly TextBox _report = new TextBox() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, Font = new Font(FontFamily.GenericMonospace, 8.25f) };
            private readonly Button _apply = new Button() { Text = "Apply", Width = 90 };
            private readonly Button _check = new Button() { Text = "Check", Width = 90, Visible = false };
            private readonly Button _close = new Button() { Text = "Close", Width = 90 };
            private readonly Label _status = new Label() { AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
            public Func<(string tool, JObject args)> OnApply;
            public Func<(string tool, JObject args)> OnCheck;
            public Func<string, JObject, Task<(bool ok, string text, JObject result)>> Run;

            public PathToolDialog(string title, string about)
            {
                Text = title;
                Icon = SharedFormIcon.Icon;
                StartPosition = FormStartPosition.CenterParent;
                Size = new Size(560, 640);
                MinimumSize = new Size(460, 420);
                ShowInTaskbar = false;
                _rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
                _rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
                Label intro = new Label() { Text = about, Dock = DockStyle.Top, AutoSize = false, Height = 48, Padding = new Padding(8, 8, 8, 0) };
                FlowLayoutPanel buttons = new FlowLayoutPanel() { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 38, Padding = new Padding(6) };
                buttons.Controls.Add(_close);
                buttons.Controls.Add(_apply);
                buttons.Controls.Add(_check);
                buttons.Controls.Add(_status);
                Panel reportPanel = new Panel() { Dock = DockStyle.Fill, Padding = new Padding(8) };
                reportPanel.Controls.Add(_report);
                Controls.Add(reportPanel);
                Controls.Add(buttons);
                Controls.Add(_rows);
                Controls.Add(intro);
                _close.Click += (s, e) => Close();
                _apply.Click += (s, e) => Go(OnApply);
                _check.Click += (s, e) => Go(OnCheck);
                Shown += (s, e) => _check.Visible = OnCheck != null;
            }

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
}

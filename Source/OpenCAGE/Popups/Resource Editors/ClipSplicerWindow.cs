using CATHODE;
using CathodeLib;
using OpenCAGE.ClipTools;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// The Clip Splicer, opened from the Animation Editor: a new character clip assembled from frame ranges of the game's own clips,
    /// each carrying on from the last and crossfading in. Save adds it to a set and writes ANIMATION.PAK. The work is
    /// <see cref="ClipSplice"/>, shared with the MCP tool splice_clips.
    /// </summary>
    public partial class ClipSplicerWindow : BaseWindow
    {
        private static ClipSplicerWindow _open;

        /// <summary>
        /// Adds "Add to the Clip Splicer" and "Clip Splicer..." to the Animation Editor's clip list menu.
        /// <paramref name="saved"/> runs after a spliced clip is written, to list it.
        /// </summary>
        public static void Attach(Form editor, ListView clipList, Func<CathodeLib.Animation> animations, Func<CathodeLib.Animation.AnimationSet> set,
                                  Func<CathodeLib.Animation.ClipReference> selected, Action saved)
        {
            ContextMenuStrip menu = clipList?.ContextMenuStrip;
            if (menu == null) return;
            ToolStripMenuItem add = new ToolStripMenuItem("Add to the Clip Splicer") { ToolTipText = "Add this animation as the next piece of a new spliced animation" };
            ToolStripMenuItem open = new ToolStripMenuItem("Clip Splicer...") { ToolTipText = "Make a new animation from pieces of others: frame ranges, speeds, crossfades, travel carried on" };
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(add);
            menu.Items.Add(open);
            menu.Opening += (s, e) =>
            {
                CathodeLib.Animation.ClipReference clip = selected?.Invoke();
                add.Enabled = clip != null && clip.Playable && !clip.Additive && set?.Invoke()?.Kind != CathodeLib.Animation.AnimationKind.Environment;
                add.Text = clip != null && clip.Additive ? "Add to the Clip Splicer (not additive clips)" : "Add to the Clip Splicer";
                open.Enabled = animations?.Invoke() != null;
            };
            add.Click += (s, e) =>
            {
                ClipSplicerWindow window = Open(animations(), set?.Invoke(), saved);
                window.AddSegment(set?.Invoke(), selected?.Invoke());
            };
            open.Click += (s, e) => Open(animations(), set?.Invoke(), saved);
        }

        public static ClipSplicerWindow Open(CathodeLib.Animation animations, CathodeLib.Animation.AnimationSet set, Action saved = null)
        {
            if (_open == null || _open.IsDisposed)
            {
                _open = new ClipSplicerWindow(animations, set);
                _open.Show();
            }
            else _open.BringToFront();
            if (saved != null) _open._saved = saved;
            return _open;
        }

        private readonly CathodeLib.Animation _animations;
        private Action _saved;
        private readonly List<ClipSplice.Segment> _segments = new List<ClipSplice.Segment>();
        private readonly List<string> _segmentSets = new List<string>();
        private ClipSplice.Result _result;
        private AnimationPreview _preview;
        private bool _filling;

        //Made here rather than in the designer, which can't make this class: it fills the designer's _timelineHost
        private readonly Panel _timeline = new DoubleBufferedPanel();

        private sealed class DoubleBufferedPanel : Panel { public DoubleBufferedPanel() { DoubleBuffered = true; ResizeRedraw = true; } }

        private ClipSplicerWindow(CathodeLib.Animation animations, CathodeLib.Animation.AnimationSet set) : base(WindowClosesOn.NONE)
        {
            InitializeComponent();

            _animations = animations;
            Icon = SharedFormIcon.Icon;

            _timeline.Dock = DockStyle.Fill;
            _timeline.Paint += PaintTimeline;
            _timeline.Resize += (s, e) => _timeline.Invalidate();
            _timelineHost.Controls.Add(_timeline);

            foreach (CathodeLib.Animation.AnimationSet s in _animations.Sets.Where(o => o.Kind != CathodeLib.Animation.AnimationKind.Environment).OrderBy(o => o.Name))
                _setBox.Items.Add(s.Name);
            _setBox.SelectedItem = set != null && set.Kind != CathodeLib.Animation.AnimationKind.Environment ? set.Name : (_setBox.Items.Contains("MALE") ? "MALE" : null);
            if (_setBox.SelectedIndex < 0 && _setBox.Items.Count != 0) _setBox.SelectedIndex = 0;
            FillContexts();

            Report("Add pieces with Add Clip... (or right-click a clip in the Animation Editor: Add to the Clip Splicer), then Check, Preview and Save.\r\n"
                + "All pieces must be authored on one rig (MALE's own clips, say). Clips made to follow one another (leap_in_2_5m, leap_2_5m, leap_2_5m_out) meet exactly: give those a blend of 0.");
        }

        private void _setBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            FillContexts();
        }

        private void _addBtn_Click(object sender, EventArgs e)
        {
            PickClip();
        }

        private void _upBtn_Click(object sender, EventArgs e)
        {
            MoveRow(-1);
        }

        private void _downBtn_Click(object sender, EventArgs e)
        {
            MoveRow(1);
        }

        private void _removeBtn_Click(object sender, EventArgs e)
        {
            int at = Row;
            if (at < 0) return;
            ReadGrid();
            _segments.RemoveAt(at);
            _segmentSets.RemoveAt(at);
            FillGrid(Math.Min(at, _segments.Count - 1));
        }

        private void _checkBtn_Click(object sender, EventArgs e)
        {
            Build(true);
        }

        private void _previewBtn_Click(object sender, EventArgs e)
        {
            Preview();
        }

        private void _saveBtn_Click(object sender, EventArgs e)
        {
            Save();
        }

        private void _grid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (!_filling) _result = null;
        }

        private void ClipSplicerWindow_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (_preview != null && !_preview.IsDisposed) _preview.Close();
        }

        private int Row { get { return _grid.CurrentRow?.Index ?? -1; } }

        private CathodeLib.Animation.AnimationSet OutputSet { get { return _animations.GetSet(_setBox.SelectedItem as string ?? ""); } }

        private void FillContexts()
        {
            _contextBox.Items.Clear();
            _contextBox.Items.Add("(the set's own lines)");
            CathodeLib.Animation.AnimationSet set = OutputSet;
            if (set != null)
                foreach (CathodeLib.Animation.AnimationContext c in set.Contexts.Where(o => o.Name.Length != 0)) _contextBox.Items.Add(c.Name);
            _contextBox.SelectedIndex = 0;
        }

        /// <summary>Add a clip as the next piece.</summary>
        public void AddSegment(CathodeLib.Animation.AnimationSet set, CathodeLib.Animation.ClipReference clip)
        {
            if (clip == null || !clip.Playable) return;
            if (clip.Additive)
            {
                MessageBox.Show("'" + clip.Name + "' is additive: it holds changes to lay over another pose, so it can't be a piece of one (additive clips play on CHR_PlaySecondaryAnimation).", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            ReadGrid();
            _segments.Add(new ClipSplice.Segment { Clip = clip, Blend = _segments.Count == 0 ? 0 : 0.2f });
            _segmentSets.Add(set?.Name ?? clip.Context?.Set?.Name ?? "");
            if (_segments.Count == 1 && (_nameBox.Text == "spliced_clip" || _nameBox.Text.Length == 0)) _nameBox.Text = clip.Name + "_spliced";
            FillGrid(_segments.Count - 1);
            BringToFront();
        }

        private void PickClip()
        {
            EditAnimations picker = new EditAnimations(EditAnimations.PickMode.Animation, _segmentSets.LastOrDefault() ?? (_setBox.SelectedItem as string));
            picker.OnPickedInSet = (clipName, setName) =>
            {
                CathodeLib.Animation.AnimationSet set = _animations.GetSet(setName ?? "");
                CathodeLib.Animation.ClipReference clip = set?.Contexts.SelectMany(o => o.Clips).FirstOrDefault(o => string.Equals(o.Name, clipName, StringComparison.OrdinalIgnoreCase));
                AddSegment(set, clip);
            };
            picker.Show();
        }

        private void MoveRow(int by)
        {
            int at = Row, to = at + by;
            if (at < 0 || to < 0 || to >= _segments.Count) return;
            ReadGrid();
            ClipSplice.Segment s = _segments[at]; _segments[at] = _segments[to]; _segments[to] = s;
            string n = _segmentSets[at]; _segmentSets[at] = _segmentSets[to]; _segmentSets[to] = n;
            FillGrid(to);
        }

        private void FillGrid(int select)
        {
            _filling = true;
            _grid.Rows.Clear();
            for (int i = 0; i < _segments.Count; i++)
            {
                ClipSplice.Segment s = _segments[i];
                _grid.Rows.Add(_segmentSets[i] + " \\ " + s.Clip.Name + "  [" + s.Clip.Skeleton + "]", (s.Clip.Animation?.FrameCount ?? 0) + " (" + s.Clip.Duration.ToString("0.00") + " s)",
                    s.From, s.To, F(s.Speed), F(s.Blend), F(s.Hold), s.Continue, F(s.Turn), F(s.Carry.Z));
            }
            if (select >= 0 && select < _grid.Rows.Count) _grid.CurrentCell = _grid.Rows[select].Cells[2];
            _filling = false;
            _result = null;
        }

        private static string F(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }

        /// <summary>The grid's edits back into the segments; false (and said) when one doesn't read.</summary>
        private bool ReadGrid()
        {
            _grid.EndEdit();
            for (int i = 0; i < _segments.Count && i < _grid.Rows.Count; i++)
            {
                DataGridViewCellCollection c = _grid.Rows[i].Cells;
                ClipSplice.Segment s = _segments[i];
                if (!Int(c[2].Value, out s.From) || !Int(c[3].Value, out s.To) || !Num(c[4].Value, out s.Speed) || !Num(c[5].Value, out s.Blend)
                    || !Num(c[6].Value, out s.Hold) || !Num(c[8].Value, out s.Turn) || !Num(c[9].Value, out float carry))
                {
                    Report("Row " + (i + 1) + " has a value that isn't a number.");
                    return false;
                }
                s.Continue = c[7].Value is bool b ? b : true;
                s.Carry = new Vector3(0, 0, carry);
            }
            return true;
        }

        private static bool Int(object value, out int result) { return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out result); }
        private static bool Num(object value, out float result) { return float.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out result); }

        private bool Build(bool report)
        {
            if (!ReadGrid()) return false;
            if (_segments.Count == 0) { Report("Add at least one piece."); return false; }
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                _result = ClipSplice.Build(_animations, _segments, new ClipSplice.Options { Travel = _travel.Checked, FromFirstFrame = _fromStart.Checked, LockFeet = _lockFeet.Checked });
            }
            catch (Exception e) { _result = new ClipSplice.Result { Problem = e.Message }; }
            finally { Cursor.Current = Cursors.Default; }
            if (!_result.Ok) { Report("It can't be spliced:\r\n" + _result.Problem); return false; }
            if (report) Report(Describe(_result));
            return true;
        }

        private static string Describe(ClipSplice.Result r)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine(r.Frames + " frames, " + r.Duration.ToString("0.00") + " s at " + (1 / r.FrameDuration).ToString("0.##") + " fps, on " + r.RigName + ".");
            Matrix4x4 end = r.Capsule[r.Capsule.Count - 1];
            text.AppendLine("It ends " + end.Translation.Z.ToString("0.00") + " m forward, " + end.Translation.X.ToString("0.00") + " m across, " + end.Translation.Y.ToString("0.00")
                + " m up from where it starts, facing " + ClipFrames.YawOf(end).ToString("0") + " degrees round.");
            text.AppendLine();
            foreach (ClipSplice.Piece p in r.Pieces)
                text.AppendLine(("  " + p.Segment.Clip.Name).PadRight(36) + " frames " + (p.FirstFrame + "-" + p.LastFrame).PadRight(9) + (p.Start.ToString("0.00") + "-" + p.End.ToString("0.00") + " s").PadRight(14)
                    + (p.BlendFrames > 0 ? "blend " + p.BlendFrames + " f, " : "") + "travels " + p.Travel.ToString("0.00") + " m"
                    + (p == r.Pieces[0] ? "" : "; join: pose pop " + (p.Pop * 100).ToString("0") + " cm -> worst step " + (p.JoinStep * 100).ToString("0") + " cm, a planted foot slides " + (p.FootDrift * 100).ToString("0") + " cm"));
            text.AppendLine();
            text.AppendLine("Worst step at a join " + (r.WorstJoinStep * 100).ToString("0") + " cm a frame; the motion itself moves up to " + (r.WorstOwnStep * 100).ToString("0") + " cm a frame elsewhere.");
            if (r.LockedFeet != 0) text.AppendLine(r.LockedFeet + " planted feet held through the crossfades.");
            foreach (string note in r.Notes) text.AppendLine("* " + note);
            return text.ToString();
        }

        private void Report(string text) { _report.Text = text; _timeline.Invalidate(); }

        /* The timeline: each piece a bar across the result's time, in turn, its crossfade with the one before hatched, a tick a second */
        private void PaintTimeline(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            ClipSplice.Result r = _result;
            Rectangle area = new Rectangle(6, 4, Math.Max(10, _timeline.Width - 12), _timeline.Height - 18);
            using (Pen frame = new Pen(SystemColors.ControlDark)) g.DrawRectangle(frame, area);
            if (r == null || !r.Ok || r.Duration <= 0)
            {
                TextRenderer.DrawText(g, "Check builds the timeline", Font, area, SystemColors.GrayText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }
            Color[] colours = { Color.FromArgb(86, 156, 214), Color.FromArgb(214, 157, 86), Color.FromArgb(120, 190, 110), Color.FromArgb(190, 110, 170), Color.FromArgb(200, 190, 90), Color.FromArgb(100, 190, 190) };
            float X(float seconds) { return area.Left + area.Width * seconds / r.Duration; }
            for (int i = 0; i < r.Pieces.Count; i++)
            {
                ClipSplice.Piece p = r.Pieces[i];
                bool upper = i % 2 == 0;
                RectangleF bar = new RectangleF(X(p.Start), upper ? area.Top + 2 : area.Top + area.Height / 2 + 1, Math.Max(2, X(p.End) - X(p.Start)), area.Height / 2 - 3);
                using (Brush fill = new SolidBrush(colours[i % colours.Length])) g.FillRectangle(fill, bar);
                if (p.BlendFrames > 0)
                    using (System.Drawing.Drawing2D.HatchBrush hatch = new System.Drawing.Drawing2D.HatchBrush(System.Drawing.Drawing2D.HatchStyle.BackwardDiagonal, Color.White, Color.Transparent))
                        g.FillRectangle(hatch, new RectangleF(bar.Left, bar.Top, Math.Min(bar.Width, p.BlendFrames * r.FrameDuration * area.Width / r.Duration), bar.Height));
                TextRenderer.DrawText(g, p.Segment.Clip.Name, Font, Rectangle.Round(bar), Color.Black, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
            using (Pen tick = new Pen(SystemColors.ControlDarkDark))
                for (int s = 0; s <= (int)r.Duration; s++)
                {
                    float x = X(s);
                    g.DrawLine(tick, x, area.Bottom, x, area.Bottom + 4);
                    TextRenderer.DrawText(g, s + "s", Font, new Point((int)x - 6, area.Bottom + 3), SystemColors.ControlText);
                }
        }

        private string StoredPath { get { return AnimationImport.PathFor(OutputSet, _nameBox.Text.Trim()); } }

        private void Preview()
        {
            if (!Build(true)) return;
            CathodeLib.Animation.AnimationSet set = OutputSet;
            if (set == null) return;
            CathodeLib.Animation.ClipReference clip;
            try
            {
                string name = _nameBox.Text.Trim().Length == 0 ? "spliced_clip" : _nameBox.Text.Trim();
                AnimClipDBSec section = _animations.BuildSection(_result.Poses, Enumerable.Range(0, _result.Poses[0].Count).Select(i => (short)i).ToList(), _result.RigName,
                    StoredPath, set.Name + "\\" + name.ToUpperInvariant(), _result.FrameDuration, false);
                clip = section == null ? null : new CathodeLib.Animation.ClipReference
                {
                    Name = name, Path = StoredPath, Section = section, Index = 0,
                    Context = set.Contexts.FirstOrDefault(o => o.Name.Length == 0) ?? set.Contexts.FirstOrDefault(),
                };
            }
            catch (Exception e) { Report("It couldn't be built for preview: " + e.Message); return; }
            if (clip == null || !clip.Playable) { Report("It couldn't be built for preview."); return; }
            if (_preview == null || _preview.IsDisposed)
            {
                _preview = new AnimationPreview(_animations) { Owner = this };
                _preview.FormClosed += (s, e) => _preview = null;
                _preview.Show();
                _preview.Update();
            }
            _preview.Show(clip);
            _preview.BringToFront();
        }

        private void Save()
        {
            if (!Build(true)) return;
            CathodeLib.Animation.AnimationSet set = OutputSet;
            string name = _nameBox.Text.Trim(), path = StoredPath;
            string context = _contextBox.SelectedIndex > 0 ? _contextBox.SelectedItem as string : null;
            if (set == null) return;
            if (name.Length == 0 || AnimationImport.Sanitise(name) != name) { MessageBox.Show("Give it a name of letters, digits and underscores.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (set.Contexts.SelectMany(o => o.Clips).Any(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase)))
            { MessageBox.Show("'" + name + "' is already the name of an animation in " + set.Name + ".", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (_animations.GetSection(path, out int _) != null)
            { MessageBox.Show("Something is already stored at '" + path + "': pick another name.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            string stale = AnimationPakWrite.Stale(_animations);
            if (stale != null) { MessageBox.Show(stale, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            if (MessageBox.Show("Add '" + name + "' (" + _result.Duration.ToString("0.00") + " s) to " + set.Name + (context != null ? "'s " + context + " context" : "") + " and write ANIMATION.PAK?\r\n\r\nANIMATION.PAK is shared by every level.",
                Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;

            Cursor.Current = Cursors.WaitCursor;
            try
            {
                string rig = _animations.SkeletonIndex?.GetSkeleton(_result.RigName)?.Name ?? _result.RigName;
                bool added = _animations.AddClip(set, name, path, rig, Enumerable.Range(0, _result.Poses[0].Count).Select(i => (short)i).ToList(), _result.Poses, _result.FrameDuration, false, context);
                if (!added) { Report("It could not be added to the animation database."); return; }
                if (context == null) Singleton.RegisterAnimation(set.Name, name);
                Singleton.OnAnimationsModified?.Invoke();
                string refused = AnimationPakWrite.SaveWhole(_animations, this);
                if (refused != null) { MessageBox.Show(refused, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
                Report(Describe(_result) + "\r\nSaved: " + set.Name + " \\ " + name + (context != null ? " (" + context + " context)" : "") + ", ANIMATION.PAK written. "
                    + (context == null ? "Play it with CMD_PlayAnimation (AnimationSet " + set.Name + ", Animation " + name + "); at a marker it starts on the marker and the capsule follows." : "The animation trees play it in that context's slot of the same name."));
                _saved?.Invoke();
            }
            catch (Exception e) { MessageBox.Show(e.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { Cursor.Current = Cursors.Default; }
        }
    }
}

using CathodeLib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE.Popups.UserControls
{
    /// <summary>
    /// The events tagged on a clip, laid out against time: a lane per property, a marker per moment
    /// it fires, and a playhead that follows the preview. Scrubbing here drives playback.
    /// </summary>
    public class AnimationTimeline : Control
    {
        /// <summary>Raised when the user scrubs, with <see cref="Frame"/> already updated.</summary>
        public event EventHandler FrameChanged;

        /// <summary>Raised when the selection changes, with the marker or null.</summary>
        public event EventHandler<Marker> MarkerSelected;

        private const int GutterWidth = 168;
        private const int RulerHeight = 26;
        private const int LaneHeight = 20;
        private const int RightPadding = 12;
        private const int MarkerRadius = 5;

        private readonly List<Lane> _lanes = new List<Lane>();
        private readonly VScrollBar _scroll = new VScrollBar { Dock = DockStyle.Right, Width = 15, Visible = false };
        private readonly ToolTip _tip = new ToolTip { InitialDelay = 220, ReshowDelay = 120 };

        private float _duration;
        private float _clipDuration;
        private int _frameCount;
        private float _frameDuration = 1 / 30.0f;
        private int _frame;
        private Marker _selected;
        private Marker _hovered;
        private string _tipShownFor;
        private bool _scrubbing;

        public AnimationTimeline()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Controls.Add(_scroll);
            _scroll.Scroll += (s, e) => Invalidate();
            BackColor = SystemColors.ControlDark;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tip.Dispose();
            base.Dispose(disposing);
        }

        /// <summary>Which frame the playhead sits on.</summary>
        public int Frame
        {
            get { return _frame; }
            set
            {
                int clamped = Math.Max(0, Math.Min(Math.Max(0, _frameCount - 1), value));
                if (clamped == _frame) return;
                _frame = clamped;
                Invalidate();
            }
        }

        /// <summary>The marker the user last clicked, or null.</summary>
        public Marker Selected { get { return _selected; } }

        /// <summary>How many lanes the clip needs, so the caller can size the control sensibly.</summary>
        public int LaneCount { get { return _lanes.Count; } }

        /// <summary>The height at which every lane is visible without scrolling.</summary>
        public int PreferredHeight { get { return RulerHeight + (Math.Max(1, _lanes.Count) * LaneHeight) + 6; } }

        /// <summary>
        /// Load a clip's markers. Markers identical but for which use of the clip tagged them are
        /// merged - a clip used five ways usually carries five copies of the same footstep.
        /// </summary>
        public void SetClip(Animation.ClipReference clip)
        {
            _lanes.Clear();
            _selected = null;
            _hovered = null;
            _frame = 0;

            _frameCount = clip?.Animation?.FrameCount ?? 0;
            _frameDuration = clip?.Animation?.FrameDuration > 0 ? clip.Animation.FrameDuration : 1 / 30.0f;
            _clipDuration = clip?.Animation?.Duration ?? 0;
            if (_clipDuration <= 0 && _frameCount > 0) _clipDuration = (_frameCount - 1) * _frameDuration;

            if (clip != null)
            {
                foreach (IGrouping<string, Animation.ClipMarker> group in clip.Markers.GroupBy(x => x.Property))
                {
                    Lane lane = new Lane { Name = group.Key };

                    /* Everything that agrees on time and on what it fires is one moment in the clip,
                     * however many of its uses happen to tag it. */
                    foreach (IGrouping<string, Animation.ClipMarker> same in group.GroupBy(x => x.Time.ToString("0.#####") + "|" + (x.Event ?? "")))
                    {
                        List<Animation.ClipMarker> copies = same.ToList();
                        lane.Markers.Add(new Marker
                        {
                            Source = copies[0],
                            Time = copies[0].Time,
                            Uses = copies.Count,
                            Lane = lane,
                        });
                    }
                    lane.Markers.Sort((a, b) => a.Time.CompareTo(b.Time));
                    _lanes.Add(lane);
                }

                //sounds first, they're what people are usually looking for, then alphabetical
                _lanes.Sort((a, b) =>
                {
                    bool audioA = a.Markers.Any(x => x.Source.IsAudio), audioB = b.Markers.Any(x => x.Source.IsAudio);
                    if (audioA != audioB) return audioA ? -1 : 1;
                    return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                });
            }

            //a handful of markers sit past the end of their clip, so make room rather than clip them
            float latest = _lanes.SelectMany(x => x.Markers).Select(x => x.Time).DefaultIfEmpty(0).Max();
            _duration = Math.Max(_clipDuration, latest);
            if (_duration <= 0) _duration = 1;

            UpdateScrollbar();
            MarkerSelected?.Invoke(this, null);
            Invalidate();
        }

        private void UpdateScrollbar()
        {
            int visible = Math.Max(0, Height - RulerHeight);
            int needed = _lanes.Count * LaneHeight;
            _scroll.Visible = needed > visible;
            if (!_scroll.Visible) { _scroll.Value = 0; return; }

            _scroll.Minimum = 0;
            _scroll.Maximum = Math.Max(0, needed - 1);
            _scroll.LargeChange = Math.Max(1, visible);
            _scroll.SmallChange = LaneHeight;
            _scroll.Value = Math.Min(_scroll.Value, Math.Max(0, _scroll.Maximum - _scroll.LargeChange + 1));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScrollbar();
        }

        #region GEOMETRY
        //the plot starts a little clear of the gutter, so a marker at zero doesn't straddle the divider
        private int PlotLeft { get { return GutterWidth + 10; } }

        private int PlotWidth { get { return Math.Max(1, Width - PlotLeft - RightPadding - (_scroll.Visible ? _scroll.Width : 0)); } }

        private float TimeToX(float time) { return PlotLeft + (time / _duration * PlotWidth); }

        private float XToTime(int x) { return Math.Max(0, Math.Min(_duration, (x - PlotLeft) / (float)PlotWidth * _duration)); }

        private int LaneTop(int index) { return RulerHeight + (index * LaneHeight) - _scroll.Value; }

        private Marker HitTest(Point at)
        {
            for (int i = 0; i < _lanes.Count; i++)
            {
                int top = LaneTop(i);
                if (at.Y < top || at.Y >= top + LaneHeight) continue;

                foreach (Marker marker in _lanes[i].Markers)
                    if (Math.Abs(TimeToX(marker.Time) - at.X) <= MarkerRadius + 2) return marker;
                return null;
            }
            return null;
        }
        #endregion

        #region INPUT
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            Marker hit = HitTest(e.Location);
            if (hit != null)
            {
                _selected = hit;
                MarkerSelected?.Invoke(this, hit);
                ScrubTo(hit.Time);
                Invalidate();
                return;
            }

            if (e.X < GutterWidth) return;
            _scrubbing = true;
            ScrubTo(XToTime(e.X));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_scrubbing) { ScrubTo(XToTime(e.X)); return; }

            Marker hit = HitTest(e.Location);
            Cursor = hit != null ? Cursors.Hand : e.X >= GutterWidth ? Cursors.VSplit : Cursors.Default;
            if (hit == _hovered) return;

            _hovered = hit;
            string text = hit == null ? null : Describe(hit, true);
            if (text != _tipShownFor)
            {
                _tipShownFor = text;
                if (text == null) _tip.Hide(this); else _tip.Show(text, this, e.X + 14, e.Y + 18, 6000);
            }
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _scrubbing = false;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = null;
            _tipShownFor = null;
            _tip.Hide(this);
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!_scroll.Visible) return;

            int value = _scroll.Value - (Math.Sign(e.Delta) * LaneHeight * 2);
            _scroll.Value = Math.Max(_scroll.Minimum, Math.Min(Math.Max(0, _scroll.Maximum - _scroll.LargeChange + 1), value));
            Invalidate();
        }

        private void ScrubTo(float time)
        {
            int frame = _frameDuration > 0 ? (int)Math.Round(time / _frameDuration) : 0;
            frame = Math.Max(0, Math.Min(Math.Max(0, _frameCount - 1), frame));
            if (frame == _frame) return;

            _frame = frame;
            Invalidate();
            FrameChanged?.Invoke(this, EventArgs.Empty);
        }
        #endregion

        #region DESCRIPTION
        /// <summary>A one-line account of what a marker does, for the tooltip and the detail bar.</summary>
        public string Describe(Marker marker, bool multiline)
        {
            if (marker == null) return "";

            string separator = multiline ? Environment.NewLine : "   ·   ";
            Animation.ClipMarker source = marker.Source;
            List<string> parts = new List<string>
            {
                marker.Time.ToString("0.###") + "s  (frame " + FrameOf(marker.Time) + ")",
                marker.Lane.Name,
            };

            if (source.Audio != null)
            {
                parts.Add("plays \"" + (source.Audio.Event ?? "?") + "\""
                    + (string.IsNullOrEmpty(source.Audio.Bone) ? "" : " from the " + source.Audio.Bone + " bone"));
                if (!string.IsNullOrEmpty(source.Audio.Offset) && source.Audio.Offset != "0,0,0")
                    parts.Add("offset " + source.Audio.Offset);
                if (source.Audio.UsesArguments && !string.IsNullOrEmpty(source.Audio.Arguments))
                    parts.Add("arguments " + source.Audio.Arguments);
                parts.Add("via " + source.Event);
            }
            else if (source.Event != null)
            {
                parts.Add(source.Type == CATHODE.Animations.MetadataValueType.PROPERTY_REFERENCE
                    ? "triggers " + source.Event + (source.Argument == null ? " (no such argument)" : " = " + source.Argument.Value)
                    : source.Type + " " + source.Event);
            }
            else parts.Add("a marker with nothing attached");

            if (marker.Uses > 1) parts.Add("tagged by " + marker.Uses + " of this clip's uses");
            else if (source.Instance >= 0) parts.Add("on use " + (source.Instance + 1));

            if (_clipDuration > 0 && marker.Time > _clipDuration + 0.001f)
                parts.Add("NOTE: this is " + (marker.Time - _clipDuration).ToString("0.###") + "s past the end of the animation");

            return string.Join(separator, parts);
        }

        private int FrameOf(float time)
        {
            return _frameDuration > 0 ? (int)Math.Round(time / _frameDuration) : 0;
        }
        #endregion

        #region PAINT
        private bool Dark { get { return BackColor.GetBrightness() < 0.5f; } }
        private Color Mix(Color a, Color b, float t) => Color.FromArgb(
            (int)(a.R + ((b.R - a.R) * t)), (int)(a.G + ((b.G - a.G) * t)), (int)(a.B + ((b.B - a.B) * t)));

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            Color line = Mix(BackColor, Dark ? Color.White : Color.Black, 0.22f);
            Color faint = Mix(BackColor, Dark ? Color.White : Color.Black, 0.10f);
            Color text = ForeColor;

            PaintRuler(g, line, text);
            PaintLanes(g, line, faint, text);
            PaintPastTheEnd(g);
            PaintPlayhead(g);

            if (_lanes.Count == 0)
            {
                string message = _frameCount == 0
                    ? "No animation loaded."
                    : "This animation has no events tagged on it.";
                using (SolidBrush brush = new SolidBrush(Mix(BackColor, text, 0.55f)))
                    g.DrawString(message, Font, brush, new PointF(GutterWidth + 8, RulerHeight + 8));
            }
        }

        private void PaintRuler(Graphics g, Color line, Color text)
        {
            using (SolidBrush fill = new SolidBrush(Mix(BackColor, Dark ? Color.Black : Color.White, 0.35f)))
                g.FillRectangle(fill, 0, 0, Width, RulerHeight);
            using (Pen pen = new Pen(line))
                g.DrawLine(pen, 0, RulerHeight - 1, Width, RulerHeight - 1);

            //a tick spacing that keeps labels about 70px apart, on a 1/2/5 scale
            float step = 0.01f;
            while (step / _duration * PlotWidth < 70 && step < 1e6f)
            {
                float mantissa = step / (float)Math.Pow(10, Math.Floor(Math.Log10(step)));
                step *= mantissa < 1.5f ? 2 : mantissa < 3.5f ? 2.5f : 2;
            }

            using (Pen pen = new Pen(line))
            using (SolidBrush brush = new SolidBrush(Mix(BackColor, text, 0.75f)))
            {
                for (float t = 0; t <= _duration + (step / 2); t += step)
                {
                    float x = TimeToX(t);
                    if (x > Width) break;
                    g.DrawLine(pen, x, RulerHeight - 7, x, RulerHeight - 1);
                    g.DrawString(t.ToString(step < 0.1f ? "0.00" : step < 1 ? "0.0" : "0") + "s", Font, brush, x + 2, 3);
                }
            }

        }

        /* A few dozen clips tag moments past their own last frame. Rather than hide them, the
         * timeline runs on to the last one and shades everything the animation doesn't cover. */
        private void PaintPastTheEnd(Graphics g)
        {
            if (_clipDuration <= 0 || _duration <= _clipDuration + 0.001f) return;

            float from = TimeToX(_clipDuration);
            using (SolidBrush shade = new SolidBrush(Color.FromArgb(52, Dark ? Color.White : Color.Black)))
                g.FillRectangle(shade, from, 0, Width - from, Height);
            using (Pen edge = new Pen(Color.FromArgb(120, Dark ? Color.White : Color.Black)) { DashStyle = DashStyle.Dot })
                g.DrawLine(edge, from, 0, from, Height);
        }

        private void PaintLanes(Graphics g, Color line, Color faint, Color text)
        {
            Rectangle body = new Rectangle(0, RulerHeight, Width, Height - RulerHeight);
            g.SetClip(body);

            using (SolidBrush stripe = new SolidBrush(Mix(BackColor, Dark ? Color.White : Color.Black, 0.05f)))
            using (Pen divider = new Pen(faint))
            using (SolidBrush label = new SolidBrush(text))
            using (SolidBrush dim = new SolidBrush(Mix(BackColor, text, 0.6f)))
            using (StringFormat format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                for (int i = 0; i < _lanes.Count; i++)
                {
                    int top = LaneTop(i);
                    if (top + LaneHeight < RulerHeight || top > Height) continue;

                    if (i % 2 == 1) g.FillRectangle(stripe, 0, top, Width, LaneHeight);
                    g.DrawLine(divider, GutterWidth, top + LaneHeight, Width, top + LaneHeight);

                    Lane lane = _lanes[i];
                    bool audio = lane.Markers.Any(x => x.Source.IsAudio);
                    g.DrawString(lane.Name, Font, audio ? label : dim,
                        new RectangleF(6, top + 3, GutterWidth - 12, LaneHeight - 4), format);
                }

                using (Pen edge = new Pen(line))
                    g.DrawLine(edge, GutterWidth, RulerHeight, GutterWidth, Height);

                for (int i = 0; i < _lanes.Count; i++)
                {
                    int top = LaneTop(i);
                    if (top + LaneHeight < RulerHeight || top > Height) continue;
                    foreach (Marker marker in _lanes[i].Markers) PaintMarker(g, marker, top);
                }
            }
            g.ResetClip();
        }

        /* Sounds are warm, plain flags are cool, and anything carrying a literal value is green.
         * A marker several of the clip's uses share gets a ring to say so, and one the playhead has
         * just gone past lights up so you can see what fired as the animation plays. */
        private void PaintMarker(Graphics g, Marker marker, int laneTop)
        {
            Animation.ClipMarker source = marker.Source;
            Color colour = source.IsAudio ? Color.FromArgb(232, 150, 60)
                : source.Event != null ? Color.FromArgb(110, 190, 130)
                : Color.FromArgb(120, 160, 220);

            float x = TimeToX(marker.Time);
            float y = laneTop + (LaneHeight / 2f);
            bool active = marker == _selected || marker == _hovered;
            float radius = MarkerRadius + (active ? 2 : 0);

            //fades out over a third of a second, which is long enough to catch at playback speed
            float since = (_frame * _frameDuration) - marker.Time;
            if (since >= 0 && since < 0.33f)
            {
                float strength = 1 - (since / 0.33f);
                float glow = radius + 3 + (strength * 5);
                using (SolidBrush halo = new SolidBrush(Color.FromArgb((int)(110 * strength), colour)))
                    g.FillEllipse(halo, x - glow, y - glow, glow * 2, glow * 2);
            }

            //a diamond reads as an instant, where a circle would read as a duration
            PointF[] diamond =
            {
                new PointF(x, y - radius), new PointF(x + radius, y),
                new PointF(x, y + radius), new PointF(x - radius, y),
            };

            using (SolidBrush fill = new SolidBrush(active ? ControlPaint.Light(colour, 0.4f) : colour))
                g.FillPolygon(fill, diamond);
            using (Pen outline = new Pen(marker == _selected ? (Dark ? Color.White : Color.Black) : ControlPaint.Dark(colour, 0.2f), marker == _selected ? 2 : 1))
                g.DrawPolygon(outline, diamond);

            if (marker.Uses > 1)
                using (Pen ring = new Pen(Color.FromArgb(140, colour)))
                    g.DrawEllipse(ring, x - radius - 2.5f, y - radius - 2.5f, (radius + 2.5f) * 2, (radius + 2.5f) * 2);
        }

        private void PaintPlayhead(Graphics g)
        {
            if (_frameCount == 0) return;

            float x = TimeToX(_frame * _frameDuration);
            using (Pen pen = new Pen(Color.FromArgb(235, 70, 70), 1.5f))
                g.DrawLine(pen, x, 2, x, Height);
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(235, 70, 70)))
                g.FillPolygon(brush, new[]
                {
                    new PointF(x - 5, 1), new PointF(x + 5, 1), new PointF(x, 9),
                });
        }
        #endregion

        #region STRUCTURES
        /// <summary>One row of the timeline: everything a single property fires.</summary>
        internal class Lane
        {
            public string Name = "";
            public List<Marker> Markers = new List<Marker>();
        }

        /// <summary>
        /// One moment drawn on the timeline. A clip used several ways normally repeats its markers
        /// once per use, so identical ones are folded together and counted.
        /// </summary>
        public class Marker
        {
            public Animation.ClipMarker Source;
            public float Time;

            /// <summary>How many of the clip's uses tag this same moment.</summary>
            public int Uses = 1;

            internal Lane Lane;

            /// <summary>The property this fires under.</summary>
            public string Property { get { return Lane?.Name ?? Source?.Property ?? ""; } }

            public override string ToString() => Time.ToString("0.###") + "s " + Property;
        }
        #endregion
    }
}

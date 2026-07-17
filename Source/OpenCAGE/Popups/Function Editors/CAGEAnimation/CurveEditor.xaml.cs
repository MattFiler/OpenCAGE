using CATHODE.Scripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OpenCAGE
{
    /// <summary>
    /// A value-over-time graph editor for CAGEAnimation FloatTracks.
    /// Renders each track as a curve that respects the keyframe interpolation
    /// mode (Flat/Linear/Bezier) and lets the user drag keyframes and bezier
    /// tangent handles directly on the graph.
    /// </summary>
    public partial class CurveEditor : UserControl
    {
        public class CurveInfo
        {
            public CAGEAnimation.FloatTrack Track;
            public string Name;
            public Color Color;
        }

        public class EventInfo
        {
            public CAGEAnimation.EventTrack Track;
            public CAGEAnimation.EventTrack.Keyframe Key;
            public string Label;
        }

        private readonly List<CurveInfo> _curves = new List<CurveInfo>();
        private readonly List<EventInfo> _events = new List<EventInfo>();
        private float _start = 0f;          // visible window start
        private float _end = 1f;            // visible window end
        private float _contentStart = 0f;   // full animation start (clamp)
        private float _contentEnd = 1f;     // full animation end (clamp)
        private float _minV = 0f;
        private float _maxV = 1f;

        private CurveInfo _selectedCurve;
        private CAGEAnimation.FloatTrack.Keyframe _selectedKey;
        private EventInfo _selectedEvent;

        public CAGEAnimation.FloatTrack.Keyframe SelectedKeyframe { get { return _selectedKey; } }
        public CAGEAnimation.FloatTrack SelectedTrack { get { return _selectedCurve == null ? null : _selectedCurve.Track; } }
        public CAGEAnimation.EventTrack.Keyframe SelectedEvent { get { return _selectedEvent == null ? null : _selectedEvent.Key; } }
        public CAGEAnimation.EventTrack SelectedEventTrack { get { return _selectedEvent == null ? null : _selectedEvent.Track; } }

        /// <summary>Raised when a keyframe becomes selected or its data changes via a drag.</summary>
        public event Action<CAGEAnimation.FloatTrack.Keyframe> KeyframeSelected;
        /// <summary>Raised when the selection is cleared (empty area clicked or keyframe deleted).</summary>
        public event Action SelectionCleared;
        /// <summary>Raised after any edit to the underlying data (move, tangent, add, delete).</summary>
        public event Action DataChanged;
        /// <summary>Raised when an event marker is selected.</summary>
        public event Action<CAGEAnimation.EventTrack.Keyframe> EventSelected;
        /// <summary>Raised when event selection is cleared.</summary>
        public event Action EventSelectionCleared;
        /// <summary>Raised on Shift+click in empty plot space - host should create an event at this time.</summary>
        public event Action<float> EventAddRequested;

        private const double MARGIN_LEFT = 50;
        private const double MARGIN_RIGHT = 14;
        private const double MARGIN_TOP = 28;
        private const double MARGIN_BOTTOM = 24;

        private const double HIT_RADIUS = 8.0;
        private const double EVENT_HIT_X = 8.0;

        private enum DragMode { None, Keyframe, TanIn, TanOut, Event, Pan }
        private DragMode _drag = DragMode.None;
        private bool _dragging = false;
        private Point _panOrigin;
        private float _panStartView;
        private float _panEndView;

        private class KeyHit
        {
            public CurveInfo Curve;
            public CAGEAnimation.FloatTrack.Keyframe Key;
            public Point Screen;
            public Ellipse Dot;
            public Brush CurveBrush;
        }
        private class EventHit
        {
            public EventInfo Info;
            public double ScreenX;
            public Line Line;
            public Polygon Tip;
        }
        private readonly List<KeyHit> _keyHits = new List<KeyHit>();
        private readonly List<EventHit> _eventHits = new List<EventHit>();
        private EventInfo _hoveredEvent;
        private CAGEAnimation.FloatTrack.Keyframe _hoveredKey;
        private Point _tanInScreen;
        private Point _tanOutScreen;
        private bool _hasTanHandles = false;

        private static readonly Brush EventBrush = CreateFrozenBrush(0xE6, 0xC2, 0x00);
        private static readonly Brush EventHoverBrush = CreateFrozenBrush(0xF0, 0xC8, 0x00);
        private static readonly Brush EventSelectedBrush = CreateFrozenBrush(0x8A, 0x6A, 0x00);
        private static readonly Brush EventLabelBrush = CreateFrozenBrush(0x8A, 0x6E, 0x00);
        private static readonly DoubleCollection EventDash = CreateEventDash();

        private static Brush CreateFrozenBrush(byte r, byte g, byte b)
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        private static DoubleCollection CreateEventDash()
        {
            DoubleCollection dash = new DoubleCollection(new double[] { 4, 2 });
            dash.Freeze();
            return dash;
        }

        private static readonly Color[] PALETTE = new Color[]
        {
            Color.FromRgb(0x1f, 0x77, 0xb4),
            Color.FromRgb(0xd6, 0x27, 0x28),
            Color.FromRgb(0x2c, 0xa0, 0x2c),
            Color.FromRgb(0xff, 0x7f, 0x0e),
            Color.FromRgb(0x94, 0x67, 0xbd),
            Color.FromRgb(0x8c, 0x56, 0x4b),
            Color.FromRgb(0xe3, 0x77, 0xc2),
            Color.FromRgb(0x17, 0xbe, 0xcf),
        };

        public CurveEditor(int w, int h)
        {
            InitializeComponent();

            Width = w;
            Height = h;
            border.Width = w;
            border.Height = h;
            Focusable = true;

            this.SizeChanged += (s, e) =>
            {
                if (ActualWidth > 1) border.Width = ActualWidth;
                if (ActualHeight > 1) border.Height = ActualHeight;
                Rebuild();
            };
            this.Loaded += (s, e) =>
            {
                if (ActualWidth > 1) border.Width = ActualWidth;
                if (ActualHeight > 1) border.Height = ActualHeight;
                Rebuild();
            };

            mainCanvas.MouseLeftButtonDown += Canvas_MouseLeftButtonDown;
            mainCanvas.MouseMove += Canvas_MouseMove;
            mainCanvas.MouseLeftButtonUp += Canvas_MouseLeftButtonUp;
            mainCanvas.MouseWheel += Canvas_MouseWheel;
            mainCanvas.MouseDown += Canvas_MouseDown;
            mainCanvas.MouseUp += Canvas_MouseUp;
            mainCanvas.MouseEnter += (s, e) => Focus();
            mainCanvas.MouseLeave += Canvas_MouseLeave;
        }

        public void Setup(float start, float end)
        {
            _contentStart = start;
            _contentEnd = end <= start ? start + 1f : end;
            _start = _contentStart;
            _end = _contentEnd;
        }

        /// <summary>Reset the visible time window to the full animation length.</summary>
        public void ResetZoom()
        {
            _start = _contentStart;
            _end = _contentEnd;
            Render();
        }

        public void AddCurve(CAGEAnimation.FloatTrack track, string name)
        {
            _curves.Add(new CurveInfo()
            {
                Track = track,
                Name = name,
                Color = PALETTE[_curves.Count % PALETTE.Length]
            });
        }

        public void AddEvent(CAGEAnimation.EventTrack track, CAGEAnimation.EventTrack.Keyframe key, string label = "")
        {
            _events.Add(new EventInfo()
            {
                Track = track,
                Key = key,
                Label = label ?? ""
            });
        }

        /// <summary>Recompute the value range then redraw everything.</summary>
        public void Rebuild()
        {
            ComputeValueRange();
            Render();
        }

        /// <summary>Redraw without recomputing the value range (used mid-drag to avoid a jumping axis).</summary>
        public void RefreshSelectedKeyframeVisual()
        {
            Rebuild();
        }

        public void RemoveSelectedKeyframe()
        {
            if (_selectedKey == null || _selectedCurve == null) return;
            _selectedCurve.Track.keyframes.Remove(_selectedKey);
            _selectedKey = null;
            Rebuild();
            if (SelectionCleared != null) SelectionCleared();
            if (DataChanged != null) DataChanged();
        }

        public void RemoveSelectedEvent()
        {
            if (_selectedEvent == null) return;
            _selectedEvent.Track.keyframes.Remove(_selectedEvent.Key);
            _events.Remove(_selectedEvent);
            _selectedEvent = null;
            Rebuild();
            if (EventSelectionCleared != null) EventSelectionCleared();
            if (DataChanged != null) DataChanged();
        }

        public void SelectEvent(CAGEAnimation.EventTrack.Keyframe key)
        {
            EventInfo match = _events.FirstOrDefault(o => o.Key == key);
            if (match == null) return;
            ClearKeyframeSelectionSilent();
            _selectedEvent = match;
            Rebuild();
            RaiseEventSelected();
        }

        public void ClearKeyframeSelection()
        {
            if (_selectedKey == null) return;
            _selectedKey = null;
            _selectedCurve = null;
            Render();
            if (SelectionCleared != null) SelectionCleared();
        }

        public void ClearEventSelection()
        {
            if (_selectedEvent == null) return;
            _selectedEvent = null;
            Render();
            if (EventSelectionCleared != null) EventSelectionCleared();
        }

        #region GEOMETRY

        private Rect Plot()
        {
            double w = ActualWidth > 0 ? ActualWidth : Width;
            double h = ActualHeight > 0 ? ActualHeight : Height;
            double pw = Math.Max(10.0, w - MARGIN_LEFT - MARGIN_RIGHT);
            double ph = Math.Max(10.0, h - MARGIN_TOP - MARGIN_BOTTOM);
            return new Rect(MARGIN_LEFT, MARGIN_TOP, pw, ph);
        }

        private double ToX(double time, Rect p)
        {
            return p.Left + (time - _start) / (_end - _start) * p.Width;
        }
        private double ToY(double value, Rect p)
        {
            double range = _maxV - _minV;
            if (range <= 0) range = 1;
            return p.Top + (1.0 - (value - _minV) / range) * p.Height;
        }
        private double TimeAt(double x, Rect p)
        {
            return _start + (x - p.Left) / p.Width * (_end - _start);
        }
        private double ValueAt(double y, Rect p)
        {
            double range = _maxV - _minV;
            return _minV + (1.0 - (y - p.Top) / p.Height) * range;
        }

        private void ComputeValueRange()
        {
            bool any = false;
            float mn = float.MaxValue;
            float mx = float.MinValue;
            foreach (CurveInfo c in _curves)
            {
                foreach (CAGEAnimation.FloatTrack.Keyframe k in c.Track.keyframes)
                {
                    float v = k.value.Y;
                    if (v < mn) mn = v;
                    if (v > mx) mx = v;
                    any = true;
                }
            }
            if (!any) { mn = 0f; mx = 1f; }
            if (mx - mn < 1e-4f) { mn -= 1f; mx += 1f; }
            float pad = (mx - mn) * 0.15f;
            _minV = mn - pad;
            _maxV = mx + pad;
        }

        private float HandleTimeLen()
        {
            float l = (_end - _start) / 10f;
            return l <= 0f ? 0.1f : l;
        }

        // Effective outgoing slope (value units per second) at the left key of a segment.
        private float SlopeOut(CAGEAnimation.FloatTrack.Keyframe a, CAGEAnimation.FloatTrack.Keyframe b)
        {
            switch (a.mode)
            {
                case CAGEAnimation.InterpolationMode.Bezier:
                    return a.tan_out.X == 0f ? 0f : a.tan_out.Y / a.tan_out.X;
                case CAGEAnimation.InterpolationMode.Flat:
                    return 0f;
                default:
                    return LineSlope(a, b);
            }
        }
        // Effective incoming slope at the right key of a segment.
        private float SlopeIn(CAGEAnimation.FloatTrack.Keyframe a, CAGEAnimation.FloatTrack.Keyframe b)
        {
            switch (b.mode)
            {
                case CAGEAnimation.InterpolationMode.Bezier:
                    return b.tan_in.X == 0f ? 0f : b.tan_in.Y / b.tan_in.X;
                case CAGEAnimation.InterpolationMode.Flat:
                    return 0f;
                default:
                    return LineSlope(a, b);
            }
        }
        private float LineSlope(CAGEAnimation.FloatTrack.Keyframe a, CAGEAnimation.FloatTrack.Keyframe b)
        {
            float dt = b.time - a.time;
            return dt == 0f ? 0f : (b.value.Y - a.value.Y) / dt;
        }

        private List<CAGEAnimation.FloatTrack.Keyframe> Sorted(CAGEAnimation.FloatTrack track)
        {
            return track.keyframes.OrderBy(o => o.time).ToList();
        }

        // Approximate value of a curve at a given time (linear between keys) - used for curve picking only.
        private float ApproxValueAt(CurveInfo c, float time)
        {
            List<CAGEAnimation.FloatTrack.Keyframe> keys = Sorted(c.Track);
            if (keys.Count == 0) return 0f;
            if (time <= keys[0].time) return keys[0].value.Y;
            if (time >= keys[keys.Count - 1].time) return keys[keys.Count - 1].value.Y;
            for (int i = 0; i < keys.Count - 1; i++)
            {
                if (time >= keys[i].time && time <= keys[i + 1].time)
                {
                    float dt = keys[i + 1].time - keys[i].time;
                    float f = dt == 0f ? 0f : (time - keys[i].time) / dt;
                    return keys[i].value.Y + (keys[i + 1].value.Y - keys[i].value.Y) * f;
                }
            }
            return keys[keys.Count - 1].value.Y;
        }

        #endregion

        #region RENDER

        private static readonly Brush GridBrush = new SolidColorBrush(Color.FromRgb(0xE1, 0xE1, 0xE1));
        private static readonly Brush AxisBrush = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A));
        private static readonly Brush LabelBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
        private static readonly Brush PlotBg = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));

        /// <summary>Clipped drawing surface for curves/events so zoom/pan cannot spill past the plot edges.</summary>
        private Canvas _plotLayer;

        private void Render()
        {
            if (mainCanvas == null) return;
            mainCanvas.Children.Clear();
            _keyHits.Clear();
            _eventHits.Clear();
            _hasTanHandles = false;

            Rect p = Plot();

            // Plot background
            Rectangle bg = new Rectangle() { Width = p.Width, Height = p.Height, Fill = PlotBg, Stroke = AxisBrush, StrokeThickness = 1 };
            Canvas.SetLeft(bg, p.Left);
            Canvas.SetTop(bg, p.Top);
            mainCanvas.Children.Add(bg);

            DrawGrid(p);

            // Clipped layer for graph content (curves, events, handles) so zoomed geometry can't bleed out
            _plotLayer = new Canvas()
            {
                Width = p.Width,
                Height = p.Height,
                ClipToBounds = true,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(_plotLayer, p.Left);
            Canvas.SetTop(_plotLayer, p.Top);
            mainCanvas.Children.Add(_plotLayer);

            DrawCurves(p);
            DrawEvents(p);
            DrawSelection(p);
            DrawLegend(p);

            if (_curves.Count == 0 && _events.Count == 0)
                AddText("No animated parameters for this entity - use \"Add Animation Track\".", p.Left + 8, p.Top + 8, LabelBrush, 11, false);

            DrawViewScrollbar(p);
        }

        private void AddPlotChild(UIElement el)
        {
            _plotLayer.Children.Add(el);
        }

        private void DrawViewScrollbar(Rect p)
        {
            float contentSpan = _contentEnd - _contentStart;
            if (contentSpan <= 0f) return;

            // Thin strip under the plot showing the visible window within the full anim length
            double barY = p.Top + p.Height + 14;
            double barH = 4;
            Rectangle track = new Rectangle()
            {
                Width = p.Width,
                Height = barH,
                Fill = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD))
            };
            Canvas.SetLeft(track, p.Left);
            Canvas.SetTop(track, barY);
            mainCanvas.Children.Add(track);

            double thumbX = p.Left + ((_start - _contentStart) / contentSpan) * p.Width;
            double thumbW = Math.Max(6.0, ((_end - _start) / contentSpan) * p.Width);
            // Clamp thumb into the track for display when padded outside content
            if (thumbX < p.Left) { thumbW -= (p.Left - thumbX); thumbX = p.Left; }
            if (thumbX + thumbW > p.Left + p.Width) thumbW = p.Left + p.Width - thumbX;
            if (thumbW <= 0) return;

            Rectangle thumb = new Rectangle()
            {
                Width = thumbW,
                Height = barH,
                Fill = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x90)),
                RadiusX = 1,
                RadiusY = 1
            };
            Canvas.SetLeft(thumb, thumbX);
            Canvas.SetTop(thumb, barY);
            mainCanvas.Children.Add(thumb);

            bool zoomed = (_end - _start) < contentSpan * 0.999f;
            if (zoomed)
                AddText("scroll zoom · shift+scroll / middle-drag pan", p.Left + p.Width - 250, 6, LabelBrush, 9, false);
        }

        private void DrawEvents(Rect p)
        {
            foreach (EventInfo ev in _events)
            {
                double screenX = ToX(ev.Key.time, p);
                double x = screenX - p.Left;
                // Skip markers completely outside the visible window (small pad for partial edge visibility)
                if (x < -2 || x > p.Width + 2) continue;

                Line line = new Line()
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = p.Height
                };
                AddPlotChild(line);

                // Triangular marker at the top of the line (inside the clipped plot)
                Polygon tip = new Polygon()
                {
                    Points = new PointCollection(new Point[]
                    {
                        new Point(x, 0),
                        new Point(x - 5, 8),
                        new Point(x + 5, 8)
                    })
                };
                AddPlotChild(tip);

                EventHit hit = new EventHit() { Info = ev, ScreenX = screenX, Line = line, Tip = tip };
                ApplyEventVisual(hit);
                _eventHits.Add(hit);

                string label = ev.Key.forward.ToString();
                if (string.IsNullOrWhiteSpace(label))
                    label = string.IsNullOrEmpty(ev.Label) ? "event" : ev.Label;
                // Labels live on the main canvas; only show when the marker is on-screen
                if (screenX >= p.Left && screenX <= p.Left + p.Width)
                    AddText(label, screenX + 4, p.Top + 10, EventLabelBrush, 9, ev == _selectedEvent);
            }
        }

        private void ApplyEventVisual(EventHit hit)
        {
            if (hit == null || hit.Line == null) return;
            bool selected = hit.Info == _selectedEvent;
            bool hovered = hit.Info == _hoveredEvent;

            Brush stroke = selected ? EventSelectedBrush : (hovered ? EventHoverBrush : EventBrush);
            hit.Line.Stroke = stroke;
            hit.Line.StrokeThickness = selected ? 3.0 : (hovered ? 2.5 : 2.0);
            // Default = dotted; hover / selected = solid
            hit.Line.StrokeDashArray = (selected || hovered) ? null : EventDash;

            if (hit.Tip != null)
            {
                hit.Tip.Fill = stroke;
                hit.Tip.Stroke = stroke;
            }
        }

        private void RefreshEventVisuals()
        {
            foreach (EventHit hit in _eventHits)
                ApplyEventVisual(hit);
        }

        private void DrawGrid(Rect p)
        {
            // Vertical (time) grid + labels
            int vLines = 8;
            for (int i = 0; i <= vLines; i++)
            {
                double t = _start + (_end - _start) * i / vLines;
                double x = ToX(t, p);
                Line l = new Line() { X1 = x, Y1 = p.Top, X2 = x, Y2 = p.Top + p.Height, Stroke = GridBrush, StrokeThickness = 1 };
                mainCanvas.Children.Add(l);
                AddText(t.ToString("0.##") + "s", x - 12, p.Top + p.Height + 4, LabelBrush, 10, false);
            }

            // Horizontal (value) grid + labels
            int hLines = 5;
            for (int i = 0; i <= hLines; i++)
            {
                double v = _minV + (_maxV - _minV) * i / hLines;
                double y = ToY(v, p);
                Line l = new Line() { X1 = p.Left, Y1 = y, X2 = p.Left + p.Width, Y2 = y, Stroke = GridBrush, StrokeThickness = 1 };
                mainCanvas.Children.Add(l);
                AddText(v.ToString("0.###"), 4, y - 8, LabelBrush, 10, false);
            }
        }

        private void DrawCurves(Rect p)
        {
            foreach (CurveInfo c in _curves)
            {
                List<CAGEAnimation.FloatTrack.Keyframe> keys = Sorted(c.Track);
                SolidColorBrush stroke = new SolidColorBrush(c.Color);
                stroke.Freeze();

                if (keys.Count >= 2)
                {
                    PathFigure fig = new PathFigure();
                    fig.StartPoint = PlotLocal(keys[0].time, keys[0].value.Y, p);
                    fig.IsFilled = false;
                    for (int i = 0; i < keys.Count - 1; i++)
                    {
                        CAGEAnimation.FloatTrack.Keyframe a = keys[i];
                        CAGEAnimation.FloatTrack.Keyframe b = keys[i + 1];
                        float dt = (b.time - a.time) / 3f;
                        float c1v = a.value.Y + SlopeOut(a, b) * dt;
                        float c2v = b.value.Y - SlopeIn(a, b) * dt;
                        Point c1 = PlotLocal(a.time + dt, c1v, p);
                        Point c2 = PlotLocal(b.time - dt, c2v, p);
                        Point end = PlotLocal(b.time, b.value.Y, p);
                        fig.Segments.Add(new BezierSegment(c1, c2, end, true));
                    }
                    PathGeometry geo = new PathGeometry();
                    geo.Figures.Add(fig);
                    Path path = new Path() { Data = geo, Stroke = stroke, StrokeThickness = 2 };
                    AddPlotChild(path);
                }

                // Keyframe points
                foreach (CAGEAnimation.FloatTrack.Keyframe k in keys)
                {
                    double screenX = ToX(k.time, p);
                    double screenY = ToY(k.value.Y, p);
                    double kx = screenX - p.Left;
                    double ky = screenY - p.Top;
                    if (kx < -8 || kx > p.Width + 8 || ky < -8 || ky > p.Height + 8) continue;

                    Ellipse dot = new Ellipse();
                    AddPlotChild(dot);

                    KeyHit hit = new KeyHit()
                    {
                        Curve = c,
                        Key = k,
                        Screen = new Point(screenX, screenY),
                        Dot = dot,
                        CurveBrush = stroke
                    };
                    ApplyKeyVisual(hit);
                    _keyHits.Add(hit);
                }
            }
        }

        private void ApplyKeyVisual(KeyHit hit)
        {
            if (hit == null || hit.Dot == null) return;
            bool selected = hit.Key == _selectedKey;
            bool hovered = hit.Key == _hoveredKey;

            double r = selected ? 7 : (hovered ? 6 : 4);
            hit.Dot.Width = r * 2;
            hit.Dot.Height = r * 2;
            hit.Dot.Stroke = hit.CurveBrush;
            hit.Dot.StrokeThickness = selected ? 3 : (hovered ? 2.5 : 1);
            // Default = track colour fill; hover/selected = white fill with coloured ring (solid emphasis)
            hit.Dot.Fill = (selected || hovered) ? Brushes.White : hit.CurveBrush;

            Canvas.SetLeft(hit.Dot, (hit.Screen.X - Plot().Left) - r);
            Canvas.SetTop(hit.Dot, (hit.Screen.Y - Plot().Top) - r);
        }

        private void RefreshKeyVisuals()
        {
            foreach (KeyHit hit in _keyHits)
                ApplyKeyVisual(hit);
        }

        private Point PlotLocal(double time, double value, Rect p)
        {
            return new Point(ToX(time, p) - p.Left, ToY(value, p) - p.Top);
        }

        private void DrawSelection(Rect p)
        {
            if (_selectedKey == null) return;
            if (_selectedKey.mode != CAGEAnimation.InterpolationMode.Bezier) return;

            double kx = ToX(_selectedKey.time, p);
            double ky = ToY(_selectedKey.value.Y, p);
            float hlen = HandleTimeLen();

            float inSlope = _selectedKey.tan_in.X == 0f ? 0f : _selectedKey.tan_in.Y / _selectedKey.tan_in.X;
            float outSlope = _selectedKey.tan_out.X == 0f ? 0f : _selectedKey.tan_out.Y / _selectedKey.tan_out.X;

            _tanInScreen = new Point(ToX(_selectedKey.time - hlen, p), ToY(_selectedKey.value.Y - inSlope * hlen, p));
            _tanOutScreen = new Point(ToX(_selectedKey.time + hlen, p), ToY(_selectedKey.value.Y + outSlope * hlen, p));
            _hasTanHandles = true;

            Brush handleBrush = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20));

            Point originLocal = new Point(kx - p.Left, ky - p.Top);
            Point inLocal = new Point(_tanInScreen.X - p.Left, _tanInScreen.Y - p.Top);
            Point outLocal = new Point(_tanOutScreen.X - p.Left, _tanOutScreen.Y - p.Top);
            DrawHandle(originLocal, inLocal, handleBrush);
            DrawHandle(originLocal, outLocal, handleBrush);
        }

        private void DrawHandle(Point origin, Point handle, Brush brush)
        {
            Line l = new Line() { X1 = origin.X, Y1 = origin.Y, X2 = handle.X, Y2 = handle.Y, Stroke = brush, StrokeThickness = 1 };
            AddPlotChild(l);
            Rectangle knob = new Rectangle() { Width = 8, Height = 8, Fill = Brushes.White, Stroke = brush, StrokeThickness = 2 };
            Canvas.SetLeft(knob, handle.X - 4);
            Canvas.SetTop(knob, handle.Y - 4);
            AddPlotChild(knob);
        }

        private void DrawLegend(Rect p)
        {
            double x = p.Left + 6;
            double y = 6;
            foreach (CurveInfo c in _curves)
            {
                Rectangle swatch = new Rectangle() { Width = 11, Height = 11, Fill = new SolidColorBrush(c.Color) };
                Canvas.SetLeft(swatch, x);
                Canvas.SetTop(swatch, y);
                mainCanvas.Children.Add(swatch);

                bool active = _selectedCurve == c;
                double textW = AddText(c.Name, x + 15, y - 2, LabelBrush, 10, active);
                x += 15 + textW + 16;
            }
        }

        private double AddText(string text, double x, double y, Brush brush, double size, bool bold)
        {
            TextBlock tb = new TextBlock()
            {
                Text = text,
                FontFamily = new FontFamily("Consolas"),
                FontSize = size,
                Foreground = brush,
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal
            };
            Canvas.SetLeft(tb, x);
            Canvas.SetTop(tb, y);
            mainCanvas.Children.Add(tb);
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return tb.DesiredSize.Width;
        }

        #endregion

        #region INTERACTION

        private float ClampContentTime(float t)
        {
            return Math.Max(_contentStart, Math.Min(_contentEnd, t));
        }

        private void ClampView()
        {
            float contentSpan = _contentEnd - _contentStart;
            float pad = Math.Max(contentSpan * 0.02f, 0.01f);
            float minBound = _contentStart - pad;
            float maxBound = _contentEnd + pad;

            float span = _end - _start;
            if (span >= maxBound - minBound)
            {
                _start = minBound;
                _end = maxBound;
                return;
            }
            if (_start < minBound) { _end += minBound - _start; _start = minBound; }
            if (_end > maxBound) { _start -= (_end - maxBound); _end = maxBound; }
        }

        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Rect p = Plot();
            Point pos = e.GetPosition(mainCanvas);
            float span = _end - _start;
            if (span <= 0f) return;

            // Shift+wheel pans horizontally when zoomed in
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                float pan = span * (e.Delta > 0 ? -0.1f : 0.1f);
                _start += pan;
                _end += pan;
                ClampView();
                Render();
                e.Handled = true;
                return;
            }

            float anchor = (float)TimeAt(pos.X, p);
            float factor = e.Delta > 0 ? 0.8f : 1.25f;
            float newSpan = span * factor;

            float contentSpan = Math.Max(_contentEnd - _contentStart, 0.01f);
            float minSpan = Math.Max(contentSpan * 0.002f, 0.05f);
            float maxSpan = contentSpan * 1.05f;
            newSpan = Math.Max(minSpan, Math.Min(maxSpan, newSpan));

            float frac = (anchor - _start) / span;
            _start = anchor - newSpan * frac;
            _end = _start + newSpan;
            ClampView();
            Render();
            e.Handled = true;
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Middle-mouse drag pans the time window
            if (e.ChangedButton != MouseButton.Middle) return;
            _drag = DragMode.Pan;
            _dragging = true;
            _panOrigin = e.GetPosition(mainCanvas);
            _panStartView = _start;
            _panEndView = _end;
            mainCanvas.CaptureMouse();
            mainCanvas.Cursor = Cursors.SizeWE;
            e.Handled = true;
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Middle || _drag != DragMode.Pan) return;
            _dragging = false;
            _drag = DragMode.None;
            mainCanvas.ReleaseMouseCapture();
            mainCanvas.Cursor = Cursors.Cross;
            e.Handled = true;
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point pos = e.GetPosition(mainCanvas);
            Rect p = Plot();

            // Shift+click adds an event marker at this time
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                float t = ClampContentTime((float)TimeAt(pos.X, p));
                if (EventAddRequested != null) EventAddRequested(t);
                return;
            }

            if (e.ClickCount == 2)
            {
                AddKeyframeAt(pos, p);
                return;
            }

            // Tangent handles take priority (they sit on top of the selected keyframe)
            if (_hasTanHandles)
            {
                if (Dist(pos, _tanOutScreen) <= HIT_RADIUS) { _drag = DragMode.TanOut; BeginDrag(); return; }
                if (Dist(pos, _tanInScreen) <= HIT_RADIUS) { _drag = DragMode.TanIn; BeginDrag(); return; }
            }

            // Event marker hit test (nearest vertical line within X tolerance, inside plot Y)
            EventInfo hitEvent = HitTestEvent(pos, p);
            if (hitEvent != null)
            {
                ClearKeyframeSelectionSilent();
                _selectedEvent = hitEvent;
                _hoveredEvent = hitEvent;
                _drag = DragMode.Event;
                Render();
                RaiseEventSelected();
                mainCanvas.Cursor = Cursors.SizeWE;
                BeginDrag();
                return;
            }

            // Keyframe hit test (nearest within radius)
            KeyHit best = null;
            double bestDist = HIT_RADIUS + 1;
            foreach (KeyHit kh in _keyHits)
            {
                double d = Dist(pos, kh.Screen);
                if (d <= HIT_RADIUS && d < bestDist) { best = kh; bestDist = d; }
            }

            if (best != null)
            {
                ClearEventSelectionSilent();
                _selectedCurve = best.Curve;
                _selectedKey = best.Key;
                _hoveredKey = best.Key;
                _drag = DragMode.Keyframe;
                Render();
                RaiseSelected();
                mainCanvas.Cursor = Cursors.SizeAll;
                BeginDrag();
                return;
            }

            // Empty area -> clear selection
            bool cleared = false;
            if (_selectedKey != null)
            {
                _selectedKey = null;
                _selectedCurve = null;
                cleared = true;
                if (SelectionCleared != null) SelectionCleared();
            }
            if (_selectedEvent != null)
            {
                _selectedEvent = null;
                cleared = true;
                if (EventSelectionCleared != null) EventSelectionCleared();
            }
            if (cleared) Render();
        }

        private void BeginDrag()
        {
            _dragging = true;
            mainCanvas.CaptureMouse();
        }

        private void Canvas_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_dragging) return;
            if (_hoveredEvent != null)
            {
                _hoveredEvent = null;
                RefreshEventVisuals();
            }
            if (_hoveredKey != null)
            {
                _hoveredKey = null;
                RefreshKeyVisuals();
            }
            mainCanvas.Cursor = Cursors.Cross;
        }

        private EventInfo HitTestEvent(Point pos, Rect p)
        {
            if (pos.Y < p.Top || pos.Y > p.Top + p.Height) return null;
            EventInfo best = null;
            double bestDist = EVENT_HIT_X + 1;
            foreach (EventHit eh in _eventHits)
            {
                double d = Math.Abs(pos.X - eh.ScreenX);
                if (d <= EVENT_HIT_X && d < bestDist)
                {
                    best = eh.Info;
                    bestDist = d;
                }
            }
            return best;
        }

        private KeyHit HitTestKey(Point pos)
        {
            KeyHit best = null;
            double bestDist = HIT_RADIUS + 1;
            foreach (KeyHit kh in _keyHits)
            {
                double d = Dist(pos, kh.Screen);
                if (d <= HIT_RADIUS && d < bestDist)
                {
                    best = kh;
                    bestDist = d;
                }
            }
            return best;
        }

        private void UpdateHover(Point pos, Rect p)
        {
            // Prefer keyframe markers when the cursor is near both
            KeyHit keyHit = HitTestKey(pos);
            EventInfo eventHit = keyHit == null ? HitTestEvent(pos, p) : null;

            CAGEAnimation.FloatTrack.Keyframe nextKey = keyHit == null ? null : keyHit.Key;
            if (nextKey != _hoveredKey)
            {
                _hoveredKey = nextKey;
                RefreshKeyVisuals();
            }

            EventInfo nextEvent = eventHit;
            if (nextEvent != _hoveredEvent)
            {
                _hoveredEvent = nextEvent;
                RefreshEventVisuals();
            }

            if (nextKey != null)
                mainCanvas.Cursor = Cursors.SizeAll;
            else if (nextEvent != null)
                mainCanvas.Cursor = Cursors.SizeWE;
            else
                mainCanvas.Cursor = Cursors.Cross;
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            Point pos = e.GetPosition(mainCanvas);
            Rect p = Plot();

            if (!_dragging)
            {
                UpdateHover(pos, p);
                return;
            }

            switch (_drag)
            {
                case DragMode.Pan:
                    {
                        double dx = pos.X - _panOrigin.X;
                        float span = _panEndView - _panStartView;
                        float dt = (float)(-dx / p.Width * span);
                        _start = _panStartView + dt;
                        _end = _panEndView + dt;
                        ClampView();
                        Render();
                        break;
                    }
                case DragMode.Keyframe:
                    {
                        if (_selectedKey == null) return;
                        float t = ClampContentTime((float)TimeAt(pos.X, p));
                        float v = (float)ValueAt(pos.Y, p);
                        _selectedKey.time = t;
                        _selectedKey.value.Y = v;
                        mainCanvas.Cursor = Cursors.SizeAll;
                        Render();
                        RaiseSelected();
                        if (DataChanged != null) DataChanged();
                        break;
                    }
                case DragMode.Event:
                    {
                        if (_selectedEvent == null) return;
                        _selectedEvent.Key.time = ClampContentTime((float)TimeAt(pos.X, p));
                        mainCanvas.Cursor = Cursors.SizeWE;
                        Render();
                        RaiseEventSelected();
                        if (DataChanged != null) DataChanged();
                        break;
                    }
                case DragMode.TanOut:
                    {
                        if (_selectedKey == null) return;
                        double dtime = TimeAt(pos.X, p) - _selectedKey.time;
                        if (dtime < 1e-3) dtime = 1e-3;
                        double slope = (ValueAt(pos.Y, p) - _selectedKey.value.Y) / dtime;
                        _selectedKey.tan_out = new Vector2(1f, (float)slope);
                        Render();
                        if (DataChanged != null) DataChanged();
                        break;
                    }
                case DragMode.TanIn:
                    {
                        if (_selectedKey == null) return;
                        double dtime = _selectedKey.time - TimeAt(pos.X, p);
                        if (dtime < 1e-3) dtime = 1e-3;
                        double slope = (_selectedKey.value.Y - ValueAt(pos.Y, p)) / dtime;
                        _selectedKey.tan_in = new Vector2(1f, (float)slope);
                        Render();
                        if (DataChanged != null) DataChanged();
                        break;
                    }
            }
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_dragging) return;
            DragMode finished = _drag;
            _dragging = false;
            _drag = DragMode.None;
            mainCanvas.ReleaseMouseCapture();

            // Keep the underlying keyframe list ordered by time after a move.
            if (finished == DragMode.Keyframe && _selectedCurve != null)
                SortTrack(_selectedCurve.Track);
            if (finished == DragMode.Event && _selectedEvent != null)
                _selectedEvent.Track.keyframes.Sort((a, b) => a.time.CompareTo(b.time));

            Rebuild(); // recompute the value range now the drag has settled
            if (finished == DragMode.Event) RaiseEventSelected();
            else RaiseSelected();

            // Restore hover cursor after a drag
            UpdateHover(e.GetPosition(mainCanvas), Plot());
        }

        private void SortTrack(CAGEAnimation.FloatTrack track)
        {
            track.keyframes.Sort((a, b) => a.time.CompareTo(b.time));
        }

        private void AddKeyframeAt(Point pos, Rect p)
        {
            if (_curves.Count == 0) return;

            float t = ClampContentTime((float)TimeAt(pos.X, p));
            float v = (float)ValueAt(pos.Y, p);

            CurveInfo target = _selectedCurve;
            if (target == null)
            {
                // Pick the curve whose value is closest to the click position.
                double best = double.MaxValue;
                foreach (CurveInfo c in _curves)
                {
                    double dy = Math.Abs(ToY(ApproxValueAt(c, t), p) - pos.Y);
                    if (dy < best) { best = dy; target = c; }
                }
            }
            if (target == null) return;

            ClearEventSelectionSilent();

            CAGEAnimation.FloatTrack.Keyframe key = new CAGEAnimation.FloatTrack.Keyframe();
            key.time = t;
            key.value.Y = v;
            key.mode = CAGEAnimation.InterpolationMode.Bezier;
            key.tan_in = new Vector2(1f, 0f);
            key.tan_out = new Vector2(1f, 0f);
            target.Track.keyframes.Add(key);
            SortTrack(target.Track);

            _selectedCurve = target;
            _selectedKey = key;
            Rebuild();
            RaiseSelected();
            if (DataChanged != null) DataChanged();
        }

        private void RaiseSelected()
        {
            if (KeyframeSelected != null && _selectedKey != null) KeyframeSelected(_selectedKey);
        }

        private void RaiseEventSelected()
        {
            if (EventSelected != null && _selectedEvent != null) EventSelected(_selectedEvent.Key);
        }

        private void ClearKeyframeSelectionSilent()
        {
            if (_selectedKey == null) return;
            _selectedKey = null;
            _selectedCurve = null;
            if (SelectionCleared != null) SelectionCleared();
        }

        private void ClearEventSelectionSilent()
        {
            if (_selectedEvent == null) return;
            _selectedEvent = null;
            if (EventSelectionCleared != null) EventSelectionCleared();
        }

        private static double Dist(Point a, Point b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        #endregion
    }
}

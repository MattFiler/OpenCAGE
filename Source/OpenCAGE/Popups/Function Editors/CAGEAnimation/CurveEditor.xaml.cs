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
    /// Interpolation is global: Bezier (tangent handles) or Linear (straight segments).
    /// Tangent data is always stored; handles are only shown in bezier mode.
    /// </summary>
    public partial class CurveEditor : UserControl
    {
        public class CurveInfo
        {
            public CAGEAnimation.FloatTrack Track;
            public string Name;
            public Color Color;
            public bool Visible = true;
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
        private float _contentStart = 0f;   // full scrollable start (clamp)
        private float _contentEnd = 1f;     // full scrollable end (clamp)
        private float _animLength = 1f;     // playable length (0 → length), independent of key times
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
        /// <summary>Raised when the playable animation length is changed via the timeline handle.</summary>
        public event Action<float> AnimLengthChanged;

        public float AnimLength { get { return _animLength; } }

        /// <summary>When enabled, dragged times snap to multiples of <see cref="SnapInterval"/>.</summary>
        public bool SnapEnabled
        {
            get { return _snapEnabled; }
            set { _snapEnabled = value; }
        }

        /// <summary>Snap step in seconds (e.g. 0.25 = quarter second).</summary>
        public float SnapInterval
        {
            get { return _snapInterval; }
            set { _snapInterval = value > 0f ? value : 0.25f; }
        }

        /// <summary>
        /// When true, curves use control-point tangents (InterpolationMode.Bezier).
        /// When false, straight lines between keys (InterpolationMode.Linear). Handles stay in data but are hidden.
        /// </summary>
        public bool BezierMode
        {
            get { return _bezierMode; }
            set { _bezierMode = value; }
        }

        private const double MARGIN_LEFT = 50;
        private const double MARGIN_RIGHT = 14;
        private const double MARGIN_TOP = 28;
        private const double MARGIN_BOTTOM = 36;
        private const double TIME_SCROLL_BAR_H = 10;
        private const double ANIM_LENGTH_HANDLE_HIT = 7.0;

        private const double HIT_RADIUS = 8.0;
        private const double EVENT_HIT_X = 8.0;

        private enum DragMode { None, Keyframe, TanIn, TanOut, Event, Pan, AnimLength }
        private DragMode _drag = DragMode.None;
        private bool _dragging = false;
        private Point _panOrigin;
        private float _panStartView;
        private float _panEndView;
        private float _panMinV;
        private float _panMaxV;
        private bool _valueRangeFitted = false;
        private double _animLengthHandleX = -1;
        private bool _hoveredAnimLengthHandle = false;
        private bool _snapEnabled = false;
        private float _snapInterval = 0.25f;
        private bool _bezierMode = true;

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
        private static readonly Brush AnimLengthFill = CreateFrozenBrush(0x40, 0xC0, 0x60, 0x55);
        private static readonly Brush AnimLengthPlotFill = CreateFrozenBrush(0x40, 0xC0, 0x60, 0x18);
        private static readonly Brush AnimLengthHandleBrush = CreateFrozenBrush(0x2E, 0x9A, 0x4A);
        private static readonly Brush AnimLengthHandleHoverBrush = CreateFrozenBrush(0x1E, 0x7A, 0x38);
        private static readonly DoubleCollection EventDash = CreateEventDash();

        private static Brush CreateFrozenBrush(byte r, byte g, byte b)
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        private static Brush CreateFrozenBrush(byte r, byte g, byte b, byte a)
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
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

        /// <summary>Same colour used for curve index i (matches AddCurve order).</summary>
        public static Color GetPaletteColor(int index)
        {
            if (PALETTE.Length == 0) return Colors.Black;
            int i = index % PALETTE.Length;
            if (i < 0) i += PALETTE.Length;
            return PALETTE[i];
        }

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
                // Keep the fitted/scrolled value range stable across resizes
                Render();
            };
            this.Loaded += (s, e) =>
            {
                if (ActualWidth > 1) border.Width = ActualWidth;
                if (ActualHeight > 1) border.Height = ActualHeight;
                if (!_valueRangeFitted) FitValueRange();
                Render();
            };

            mainCanvas.MouseLeftButtonDown += Canvas_MouseLeftButtonDown;
            mainCanvas.MouseMove += Canvas_MouseMove;
            mainCanvas.MouseLeftButtonUp += Canvas_MouseLeftButtonUp;
            mainCanvas.MouseWheel += Canvas_MouseWheel;
            mainCanvas.MouseDown += Canvas_MouseDown;
            mainCanvas.MouseUp += Canvas_MouseUp;
            mainCanvas.MouseEnter += (s, e) => Focus();
            mainCanvas.MouseLeave += Canvas_MouseLeave;
            this.PreviewKeyDown += CurveEditor_PreviewKeyDown;
        }

        public void Setup(float animLength)
        {
            _animLength = Math.Max(0.01f, animLength);
            _contentStart = 0f;
            _contentEnd = Math.Max(_animLength, 0.01f);
            _start = _contentStart;
            _end = _contentEnd;
        }

        /// <summary>Update the playable length without recreating the editor. Keyframes may lie outside this range.</summary>
        public void SetAnimLength(float animLength)
        {
            _animLength = QuantizeAnimLength(animLength);
            RefreshContentBounds();
            ClampView();
            Render();
        }

        /// <summary>Reset the visible time window to the full scrollable range and re-fit the value axis.</summary>
        public void ResetZoom()
        {
            RefreshContentBounds();
            _start = _contentStart;
            _end = _contentEnd;
            FitValueRange();
            Render();
        }

        /// <summary>Frame the view on the given tracks' keyframes (time + value).</summary>
        public void FitToTracks(IEnumerable<CAGEAnimation.FloatTrack> tracks)
        {
            List<CAGEAnimation.FloatTrack> list = tracks == null
                ? new List<CAGEAnimation.FloatTrack>()
                : tracks.Where(t => t != null).ToList();
            if (list.Count == 0)
            {
                ResetZoom();
                return;
            }

            float mnT = float.MaxValue;
            float mxT = float.MinValue;
            float mnV = float.MaxValue;
            float mxV = float.MinValue;
            bool any = false;
            foreach (CAGEAnimation.FloatTrack track in list)
            {
                foreach (CAGEAnimation.FloatTrack.Keyframe k in track.keyframes)
                {
                    if (k.time < mnT) mnT = k.time;
                    if (k.time > mxT) mxT = k.time;
                    if (k.value.Y < mnV) mnV = k.value.Y;
                    if (k.value.Y > mxV) mxV = k.value.Y;
                    any = true;
                }
            }
            if (!any)
            {
                ResetZoom();
                return;
            }

            if (mxT - mnT < 1e-4f)
            {
                mnT -= 0.25f;
                mxT += 0.25f;
            }
            float tPad = Math.Max((mxT - mnT) * 0.1f, 0.05f);
            _start = Math.Max(0f, mnT - tPad);
            _end = mxT + tPad;

            if (mxV - mnV < 1e-4f)
            {
                mnV -= 1f;
                mxV += 1f;
            }
            float vPad = (mxV - mnV) * 0.15f;
            _minV = mnV - vPad;
            _maxV = mxV + vPad;
            _valueRangeFitted = true;

            RefreshContentBounds();
            ClampView();
            Render();
        }

        public void AddCurve(CAGEAnimation.FloatTrack track, string name, bool visible = true)
        {
            _curves.Add(new CurveInfo()
            {
                Track = track,
                Name = name,
                Color = PALETTE[_curves.Count % PALETTE.Length],
                Visible = visible
            });
        }

        /// <summary>Show/hide a float track without rebuilding the editor (keeps zoom/pan).</summary>
        public void SetCurveVisible(CAGEAnimation.FloatTrack track, bool visible)
        {
            CurveInfo info = _curves.FirstOrDefault(c => c.Track == track);
            if (info == null || info.Visible == visible) return;
            info.Visible = visible;
            Render();
        }

        public void SetCurvesVisible(IEnumerable<CAGEAnimation.FloatTrack> visibleTracks)
        {
            HashSet<CAGEAnimation.FloatTrack> set = new HashSet<CAGEAnimation.FloatTrack>(visibleTracks);
            bool changed = false;
            foreach (CurveInfo c in _curves)
            {
                bool next = set.Contains(c.Track);
                if (c.Visible != next) { c.Visible = next; changed = true; }
            }
            if (changed) Render();
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

        /// <summary>Fit axes to current data, then redraw. Call when tracks are (re)loaded.</summary>
        public void Rebuild()
        {
            RefreshContentBounds();
            _start = _contentStart;
            _end = _contentEnd;
            FitValueRange();
            Render();
        }

        /// <summary>Redraw without changing the value range (edits must not re-scale the graph).</summary>
        public void RefreshSelectedKeyframeVisual()
        {
            Render();
        }

        public void RemoveSelectedKeyframe()
        {
            if (_selectedKey == null || _selectedCurve == null) return;
            _selectedCurve.Track.keyframes.Remove(_selectedKey);
            _selectedKey = null;
            Render();
            if (SelectionCleared != null) SelectionCleared();
            if (DataChanged != null) DataChanged();
        }

        public void RemoveSelectedEvent()
        {
            if (_selectedEvent == null) return;
            _selectedEvent.Track.keyframes.Remove(_selectedEvent.Key);
            _events.Remove(_selectedEvent);
            _selectedEvent = null;
            Render();
            if (EventSelectionCleared != null) EventSelectionCleared();
            if (DataChanged != null) DataChanged();
        }

        public void SelectEvent(CAGEAnimation.EventTrack.Keyframe key)
        {
            EventInfo match = _events.FirstOrDefault(o => o.Key == key);
            if (match == null) return;
            ClearKeyframeSelectionSilent();
            _selectedEvent = match;
            Render();
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

        /// <summary>Hit region under the plot used for time zoom / timeline scroll.</summary>
        private Rect TimeScrollArea(Rect p)
        {
            double h = ActualHeight > 0 ? ActualHeight : Height;
            double bottom = Math.Max(p.Top + p.Height, h - 2);
            return new Rect(p.Left, p.Top + p.Height, p.Width, Math.Max(TIME_SCROLL_BAR_H + 8, bottom - (p.Top + p.Height)));
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

        /// <summary>Centre the value axis on the current keyframe extents. Does not run during edits.</summary>
        public void FitValueRange()
        {
            bool any = false;
            float mn = float.MaxValue;
            float mx = float.MinValue;
            foreach (CurveInfo c in _curves)
            {
                if (!c.Visible) continue;
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
            _valueRangeFitted = true;
        }

        /// <summary>Scrollable time range covers play length and any keyframes/events beyond it.</summary>
        private void RefreshContentBounds()
        {
            float maxT = _animLength;
            foreach (CurveInfo c in _curves)
            {
                if (!c.Visible) continue;
                foreach (CAGEAnimation.FloatTrack.Keyframe k in c.Track.keyframes)
                    if (k.time > maxT) maxT = k.time;
            }
            foreach (EventInfo ev in _events)
            {
                if (ev.Key.time > maxT) maxT = ev.Key.time;
            }
            _contentStart = 0f;
            float pad = Math.Max(maxT * 0.05f, 0.25f);
            _contentEnd = Math.Max(maxT + pad, _animLength + pad);
            if (_contentEnd <= _contentStart) _contentEnd = _contentStart + 1f;
        }

        private float HandleTimeLen()
        {
            float l = (_end - _start) / 10f;
            return l <= 0f ? 0.1f : l;
        }

        // Effective outgoing slope (value units per second) at the left key of a segment.
        private float SlopeOut(CAGEAnimation.FloatTrack.Keyframe a, CAGEAnimation.FloatTrack.Keyframe b)
        {
            if (_bezierMode)
                return a.tan_out.X == 0f ? 0f : a.tan_out.Y / a.tan_out.X;
            return LineSlope(a, b);
        }
        // Effective incoming slope at the right key of a segment.
        private float SlopeIn(CAGEAnimation.FloatTrack.Keyframe a, CAGEAnimation.FloatTrack.Keyframe b)
        {
            if (_bezierMode)
                return b.tan_in.X == 0f ? 0f : b.tan_in.Y / b.tan_in.X;
            return LineSlope(a, b);
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

            DrawPlayableRange(p);
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

            if (_curves.Count == 0 && _events.Count == 0)
                AddText("No animated parameters for this entity - use \"Add Animation Track\".", p.Left + 8, p.Top + 8, LabelBrush, 11, false);

            DrawViewScrollbar(p);
        }

        private void AddPlotChild(UIElement el)
        {
            _plotLayer.Children.Add(el);
        }

        private void DrawPlayableRange(Rect p)
        {
            double x0 = ToX(0f, p);
            double x1 = ToX(_animLength, p);
            double left = Math.Max(p.Left, Math.Min(p.Left + p.Width, x0));
            double right = Math.Max(p.Left, Math.Min(p.Left + p.Width, x1));
            if (right <= left) return;

            Rectangle shade = new Rectangle()
            {
                Width = right - left,
                Height = p.Height,
                Fill = AnimLengthPlotFill,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(shade, left);
            Canvas.SetTop(shade, p.Top);
            mainCanvas.Children.Add(shade);
        }

        private void DrawViewScrollbar(Rect p)
        {
            float contentSpan = _contentEnd - _contentStart;
            if (contentSpan <= 0f) return;

            Rect scrollArea = TimeScrollArea(p);
            _animLengthHandleX = ToX(_animLength, p);

            // Green playable range (0 → anim length) — no grey overview bar
            double playLeft = Math.Max(scrollArea.Left, Math.Min(scrollArea.Left + scrollArea.Width, ToX(0f, p)));
            double playRight = Math.Max(scrollArea.Left, Math.Min(scrollArea.Left + scrollArea.Width, _animLengthHandleX));
            if (playRight > playLeft)
            {
                Rectangle playShade = new Rectangle()
                {
                    Width = playRight - playLeft,
                    Height = Math.Max(1, scrollArea.Height - 2),
                    Fill = AnimLengthFill,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(playShade, playLeft);
                Canvas.SetTop(playShade, scrollArea.Top + 1);
                mainCanvas.Children.Add(playShade);
            }

            // Time labels
            int vLines = 8;
            for (int i = 0; i <= vLines; i++)
            {
                double t = _start + (_end - _start) * i / vLines;
                double x = ToX(t, p);
                AddText(t.ToString("0.##") + "s", x - 12, scrollArea.Top + 1, LabelBrush, 9, true);
            }

            // Length handle at the end of the playable range
            if (_animLengthHandleX >= scrollArea.Left - 2 && _animLengthHandleX <= scrollArea.Left + scrollArea.Width + 2)
            {
                Brush handleBrush = _hoveredAnimLengthHandle || _drag == DragMode.AnimLength
                    ? AnimLengthHandleHoverBrush
                    : AnimLengthHandleBrush;
                double handleW = 6;
                Rectangle handle = new Rectangle()
                {
                    Width = handleW,
                    Height = scrollArea.Height - 2,
                    Fill = handleBrush,
                    RadiusX = 1,
                    RadiusY = 1
                };
                Canvas.SetLeft(handle, _animLengthHandleX - handleW * 0.5);
                Canvas.SetTop(handle, scrollArea.Top + 1);
                mainCanvas.Children.Add(handle);

                for (int i = 0; i < 3; i++)
                {
                    Line notch = new Line()
                    {
                        X1 = _animLengthHandleX - 1.5,
                        X2 = _animLengthHandleX + 1.5,
                        Y1 = scrollArea.Top + 8 + i * 5,
                        Y2 = scrollArea.Top + 8 + i * 5,
                        Stroke = Brushes.White,
                        StrokeThickness = 1
                    };
                    mainCanvas.Children.Add(notch);
                }
            }

            AddText("Z = frame all · green = play length",
                p.Left + p.Width - 210, 6, LabelBrush, 9, false);
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
            // Vertical (time) grid lines only — second labels live in the timeline strip
            int vLines = 8;
            for (int i = 0; i <= vLines; i++)
            {
                double t = _start + (_end - _start) * i / vLines;
                double x = ToX(t, p);
                Line l = new Line() { X1 = x, Y1 = p.Top, X2 = x, Y2 = p.Top + p.Height, Stroke = GridBrush, StrokeThickness = 1 };
                mainCanvas.Children.Add(l);
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
                if (!c.Visible) continue;
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
            // Handles only in bezier mode; tangent data is still stored on the keyframe
            if (!_bezierMode) return;

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
            // Keyframes/events may sit beyond play length; only prevent negative times
            if (t < 0f) t = 0f;
            if (t > _contentEnd)
            {
                _contentEnd = t + Math.Max(t * 0.05f, 0.25f);
            }
            return t;
        }

        private float SnapTime(float t)
        {
            if (!_snapEnabled || _snapInterval <= 0f) return t;
            return (float)(Math.Round(t / _snapInterval) * _snapInterval);
        }

        /// <summary>Snap (if enabled) then clamp — used for keyframes, events, and play length.</summary>
        private float QuantizeTime(float t)
        {
            return ClampContentTime(SnapTime(t));
        }

        private float QuantizeAnimLength(float t)
        {
            t = SnapTime(t);
            float min = _snapEnabled ? Math.Max(_snapInterval, 0.01f) : 0.01f;
            if (t < min) t = min;
            if (t > _contentEnd)
                _contentEnd = t + Math.Max(t * 0.05f, 0.25f);
            return t;
        }

        private bool HitTestAnimLengthHandle(Point pos, Rect p)
        {
            if (_animLengthHandleX < 0) return false;
            Rect scrollArea = TimeScrollArea(p);
            if (pos.Y < scrollArea.Top || pos.Y > scrollArea.Top + scrollArea.Height) return false;
            return Math.Abs(pos.X - _animLengthHandleX) <= ANIM_LENGTH_HANDLE_HIT;
        }

        private const double MAX_ZOOM_OUT_FACTOR = 4.0;

        private void ClampView()
        {
            float contentSpan = Math.Max(_contentEnd - _contentStart, 0.01f);
            float span = _end - _start;
            float maxViewSpan = contentSpan * (float)MAX_ZOOM_OUT_FACTOR;
            if (span > maxViewSpan)
            {
                float mid = (_start + _end) * 0.5f;
                _start = mid - maxViewSpan * 0.5f;
                _end = mid + maxViewSpan * 0.5f;
                span = maxViewSpan;
            }

            // Allow empty padding around content when zoomed out past the data range
            float pad = Math.Max((span - contentSpan) * 0.5f, Math.Max(contentSpan * 0.02f, 0.01f));
            float minBound = _contentStart - pad;
            float maxBound = _contentEnd + pad;

            if (span >= maxBound - minBound)
            {
                _start = minBound;
                _end = maxBound;
                return;
            }
            if (_start < minBound) { _end += minBound - _start; _start = minBound; }
            if (_end > maxBound) { _start -= (_end - maxBound); _end = maxBound; }
        }

        private void CurveEditor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.None)
            {
                ResetZoom();
                e.Handled = true;
            }
        }

        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Rect p = Plot();
            Point pos = e.GetPosition(mainCanvas);
            bool overTimeline = TimeScrollArea(p).Contains(pos);
            bool overPlot = p.Contains(pos) || (!overTimeline && pos.Y < p.Top + p.Height);

            // Timeline strip: zoom / pan time (previous whole-canvas behaviour)
            if (overTimeline)
            {
                float span = _end - _start;
                if (span <= 0f) return;

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
                float maxSpan = contentSpan * (float)MAX_ZOOM_OUT_FACTOR;
                newSpan = Math.Max(minSpan, Math.Min(maxSpan, newSpan));

                float frac = (anchor - _start) / span;
                _start = anchor - newSpan * frac;
                _end = _start + newSpan;
                ClampView();
                Render();
                e.Handled = true;
                return;
            }

            // Plot area: wheel adjusts the value window height (edits never re-fit the axis)
            if (overPlot)
            {
                float vSpan = _maxV - _minV;
                if (vSpan <= 0f) vSpan = 1f;

                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                {
                    // Shift+wheel pans the value window
                    float pan = vSpan * (e.Delta > 0 ? 0.1f : -0.1f);
                    _minV += pan;
                    _maxV += pan;
                    Render();
                    e.Handled = true;
                    return;
                }

                // Wheel zooms (scales) the value range around the cursor — this is how graph height changes
                float anchor = (float)ValueAt(pos.Y, p);
                float factor = e.Delta > 0 ? 0.8f : 1.25f;
                float newSpan = Math.Max(vSpan * factor, 1e-3f);
                float frac = (anchor - _minV) / vSpan;
                _minV = anchor - newSpan * frac;
                _maxV = _minV + newSpan;
                Render();
                e.Handled = true;
            }
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Middle-mouse drag pans both time and value windows
            if (e.ChangedButton != MouseButton.Middle) return;
            _drag = DragMode.Pan;
            _dragging = true;
            _panOrigin = e.GetPosition(mainCanvas);
            _panStartView = _start;
            _panEndView = _end;
            _panMinV = _minV;
            _panMaxV = _maxV;
            mainCanvas.CaptureMouse();
            mainCanvas.Cursor = Cursors.SizeAll;
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

            // Animation length handle on the timeline strip
            if (HitTestAnimLengthHandle(pos, p))
            {
                _drag = DragMode.AnimLength;
                _hoveredAnimLengthHandle = true;
                mainCanvas.Cursor = Cursors.SizeWE;
                BeginDrag();
                e.Handled = true;
                return;
            }

            // Shift+click adds an event marker at this time
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                float t = QuantizeTime((float)TimeAt(pos.X, p));
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
                bool alreadySelected = (_selectedEvent == hitEvent);
                ClearKeyframeSelectionSilent();
                _selectedEvent = hitEvent;
                _hoveredEvent = hitEvent;
                Render();
                RaiseEventSelected();
                // First click selects only; drag starts on a later click while already selected
                if (alreadySelected)
                {
                    _drag = DragMode.Event;
                    mainCanvas.Cursor = Cursors.SizeWE;
                    BeginDrag();
                }
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
                bool alreadySelected = (_selectedKey == best.Key);
                ClearEventSelectionSilent();
                _selectedCurve = best.Curve;
                _selectedKey = best.Key;
                _hoveredKey = best.Key;
                Render();
                RaiseSelected();
                // First click selects only; drag starts on a later click while already selected
                if (alreadySelected)
                {
                    _drag = DragMode.Keyframe;
                    mainCanvas.Cursor = Cursors.SizeAll;
                    BeginDrag();
                }
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
            if (_hoveredAnimLengthHandle)
            {
                _hoveredAnimLengthHandle = false;
            }
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
            bool overLengthHandle = HitTestAnimLengthHandle(pos, p);
            _hoveredAnimLengthHandle = overLengthHandle;
            if (overLengthHandle)
            {
                mainCanvas.Cursor = Cursors.SizeWE;
                return;
            }

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
                        double dy = pos.Y - _panOrigin.Y;
                        float tSpan = _panEndView - _panStartView;
                        float vSpan = _panMaxV - _panMinV;
                        if (vSpan <= 0f) vSpan = 1f;

                        float dt = (float)(-dx / p.Width * tSpan);
                        // Screen Y grows downward; dragging down should reveal higher values (scroll content up)
                        float dv = (float)(dy / p.Height * vSpan);

                        _start = _panStartView + dt;
                        _end = _panEndView + dt;
                        ClampView();

                        _minV = _panMinV + dv;
                        _maxV = _panMaxV + dv;
                        mainCanvas.Cursor = Cursors.SizeAll;
                        Render();
                        break;
                    }
                case DragMode.AnimLength:
                    {
                        float t = QuantizeAnimLength((float)TimeAt(pos.X, p));
                        _animLength = t;
                        mainCanvas.Cursor = Cursors.SizeWE;
                        Render();
                        if (AnimLengthChanged != null) AnimLengthChanged(_animLength);
                        break;
                    }
                case DragMode.Keyframe:
                    {
                        if (_selectedKey == null) return;
                        float t = QuantizeTime((float)TimeAt(pos.X, p));
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
                        _selectedEvent.Key.time = QuantizeTime((float)TimeAt(pos.X, p));
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

            if (finished == DragMode.Keyframe || finished == DragMode.Event || finished == DragMode.AnimLength)
                RefreshContentBounds();

            // Do not re-fit the value axis after edits — only scrolling / FitValueRange changes height
            Render();
            if (finished == DragMode.Event) RaiseEventSelected();
            else if (finished == DragMode.AnimLength)
            {
                if (AnimLengthChanged != null) AnimLengthChanged(_animLength);
            }
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

            float t = QuantizeTime((float)TimeAt(pos.X, p));
            float v = (float)ValueAt(pos.Y, p);

            CurveInfo target = _selectedCurve;
            if (target == null)
            {
                // Pick the curve whose value is closest to the click position.
                double best = double.MaxValue;
                foreach (CurveInfo c in _curves)
                {
                    if (!c.Visible) continue;
                    double dy = Math.Abs(ToY(ApproxValueAt(c, t), p) - pos.Y);
                    if (dy < best) { best = dy; target = c; }
                }
            }
            if (target == null) return;

            ClearEventSelectionSilent();

            CAGEAnimation.FloatTrack.Keyframe key = new CAGEAnimation.FloatTrack.Keyframe();
            key.time = t;
            key.value.Y = v;
            // Always store tangent handles; shown only when BezierMode is on.
            key.mode = _bezierMode ? CAGEAnimation.InterpolationMode.Bezier : CAGEAnimation.InterpolationMode.Linear;
            key.tan_in = new Vector2(1f, 0f);
            key.tan_out = new Vector2(1f, 0f);
            target.Track.keyframes.Add(key);
            SortTrack(target.Track);

            _selectedCurve = target;
            _selectedKey = key;
            Render();
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

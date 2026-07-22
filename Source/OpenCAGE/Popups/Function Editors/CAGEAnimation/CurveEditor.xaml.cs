using CATHODE.Scripting;
using CATHODE.Enums;
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

        public class EventLaneInfo
        {
            public CAGEAnimation.EventTrack Track;
            public string Label;
            public ANIM_TRACK_TYPE TrackType;
            public Rect Bounds; // screen space
        }

        private readonly List<CurveInfo> _curves = new List<CurveInfo>();
        private readonly List<EventInfo> _events = new List<EventInfo>();
        private readonly List<EventLaneInfo> _eventLanes = new List<EventLaneInfo>();
        private readonly Dictionary<CAGEAnimation.EventTrack, string> _eventTrackLabels = new Dictionary<CAGEAnimation.EventTrack, string>();
        private readonly List<CAGEAnimation.EventTrack> _eventTrackOrder = new List<CAGEAnimation.EventTrack>();
        private readonly Dictionary<CAGEAnimation.EventTrack, ANIM_TRACK_TYPE> _eventTrackPreferredTypes = new Dictionary<CAGEAnimation.EventTrack, ANIM_TRACK_TYPE>();
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
        private CAGEAnimation.EventTrack _selectedEventTrack;

        public CAGEAnimation.FloatTrack.Keyframe SelectedKeyframe { get { return _selectedKey; } }
        public CAGEAnimation.FloatTrack SelectedTrack { get { return _selectedCurve == null ? null : _selectedCurve.Track; } }
        public CAGEAnimation.EventTrack.Keyframe SelectedEvent { get { return _selectedEvent == null ? null : _selectedEvent.Key; } }
        public CAGEAnimation.EventTrack SelectedEventTrack { get { return _selectedEventTrack ?? (_selectedEvent == null ? null : _selectedEvent.Track); } }

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
        /// <summary>Raised when an event-track lane is selected (connections UI).</summary>
        public event Action<CAGEAnimation.EventTrack> EventTrackSelected;
        /// <summary>Raised when event-track lane selection is cleared.</summary>
        public event Action EventTrackSelectionCleared;
        /// <summary>Raised on Shift+click on an event lane — host prompts for a new event at this time on the given track.</summary>
        public event Action<float, CAGEAnimation.EventTrack> EventAddRequested;
        /// <summary>Raised when the user requests a new empty event track (STRING or GUID).</summary>
        public event Action<ANIM_TRACK_TYPE> EventTrackAddRequested;
        /// <summary>Raised when the user requests deleting an entire event track from the graph context menu.</summary>
        public event Action<CAGEAnimation.EventTrack> EventTrackDeleteRequested;
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

        private const double MARGIN_LEFT = 54;
        private const double MARGIN_RIGHT = 14;
        private const double MARGIN_TOP = 28;
        private const double TIMELINE_STRIP_H = 36;
        private const double EVENT_LANE_H_DEFAULT = 24;
        private const double EVENT_LANE_H_MIN = 18;
        private const double EVENT_LANE_H_MAX = 96;
        private const double EVENT_LANE_RESIZE_H = 7;
        private const double MIN_PLOT_H = 48;
        private const double TIME_SCROLL_BAR_H = 14;
        private const double ANIM_LENGTH_HANDLE_HIT = 7.0;

        private const double HIT_RADIUS = 8.0;
        private const double EVENT_HIT_X = 8.0;
        private const double CURVE_HIT_Y = 10.0;

        private enum DragMode { None, Keyframe, TanIn, TanOut, Event, Pan, AnimLength, EventLaneResize }
        private DragMode _drag = DragMode.None;
        private bool _dragging = false;
        private Point _panOrigin;
        private float _panStartView;
        private float _panEndView;
        private float _panMinV;
        private float _panMaxV;
        private bool _panTimeOnly;
        private bool _valueRangeFitted = false;
        private double _animLengthHandleX = -1;
        private bool _hoveredAnimLengthHandle = false;
        private bool _snapEnabled = false;
        private float _snapInterval = 0.25f;
        private bool _bezierMode = true;
        private CAGEAnimation.EventTrack _hoveredEventTrack;
        private double _eventLaneH = EVENT_LANE_H_DEFAULT;
        private Rect _eventLaneResizeGrip = Rect.Empty;
        private bool _hoveredEventLaneResize = false;
        private double _laneResizeOriginY;
        private double _laneResizeStartH;

        private class KeyHit
        {
            public CurveInfo Curve;
            public CAGEAnimation.FloatTrack.Keyframe Key;
            public Point Screen;
            public Ellipse Dot;
            public Brush CurveBrush;
        }
        private class CurvePathHit
        {
            public CurveInfo Curve;
            public Path Path;
        }
        private class EventHit
        {
            public EventInfo Info;
            public double ScreenX;
            public Line Line;
            public Polygon Tip;
            public double LaneMidY;
            public double LaneTop;
            public double LaneBottom;
        }
        private readonly List<KeyHit> _keyHits = new List<KeyHit>();
        private readonly List<CurvePathHit> _curvePathHits = new List<CurvePathHit>();
        private readonly List<EventHit> _eventHits = new List<EventHit>();
        private EventInfo _hoveredEvent;
        private CAGEAnimation.FloatTrack.Keyframe _hoveredKey;
        private CurveInfo _hoveredCurve;
        private Point _tanInScreen;
        private Point _tanOutScreen;
        private bool _hasTanHandles = false;

        private static readonly Brush EventBrush = CreateFrozenBrush(0xE6, 0xC2, 0x00);
        private static readonly Brush EventHoverBrush = CreateFrozenBrush(0xF0, 0xC8, 0x00);
        private static readonly Brush EventSelectedBrush = CreateFrozenBrush(0x8A, 0x6A, 0x00);
        private static readonly Brush EventLabelBrush = CreateFrozenBrush(0x8A, 0x6E, 0x00);
        private static readonly Brush GuidEventBrush = CreateFrozenBrush(0x2E, 0x7D, 0xD8);
        private static readonly Brush GuidEventHoverBrush = CreateFrozenBrush(0x4A, 0x9A, 0xF0);
        private static readonly Brush GuidEventSelectedBrush = CreateFrozenBrush(0x1A, 0x4F, 0x9A);
        private static readonly Brush GuidEventLabelBrush = CreateFrozenBrush(0x1A, 0x55, 0xA8);
        private static readonly Brush StringLaneFill = CreateFrozenBrush(0xE6, 0xC2, 0x00, 0x28);
        private static readonly Brush StringLaneFillHover = CreateFrozenBrush(0xE6, 0xC2, 0x00, 0x40);
        private static readonly Brush StringLaneFillSelected = CreateFrozenBrush(0xE6, 0xC2, 0x00, 0x55);
        private static readonly Brush GuidLaneFill = CreateFrozenBrush(0x2E, 0x7D, 0xD8, 0x28);
        private static readonly Brush GuidLaneFillHover = CreateFrozenBrush(0x2E, 0x7D, 0xD8, 0x40);
        private static readonly Brush GuidLaneFillSelected = CreateFrozenBrush(0x2E, 0x7D, 0xD8, 0x55);
        private static readonly Brush AnimLengthPlotFill = CreateFrozenBrush(0x40, 0xC0, 0x60, 0x18);
        private static readonly Brush AnimLengthHandleBrush = CreateFrozenBrush(0x2E, 0x9A, 0x4A);
        private static readonly Brush AnimLengthHandleHoverBrush = CreateFrozenBrush(0x1E, 0x7A, 0x38);
        private static readonly Brush LaneResizeFill = CreateFrozenBrush(0xD0, 0xD0, 0xD0);
        private static readonly Brush LaneResizeFillHover = CreateFrozenBrush(0xB0, 0xB0, 0xB0);
        private static readonly Brush LaneResizeGrip = CreateFrozenBrush(0x78, 0x78, 0x78);
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
            mainCanvas.MouseRightButtonUp += Canvas_MouseRightButtonUp;
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
            RegisterEventTrack(track, label);
        }

        /// <summary>Refresh bounds and redraw after events were added/removed without recreating the editor.</summary>
        public void NotifyEventsChanged()
        {
            RefreshContentBounds();
            ClampView();
            SanitizeViewState();
            Render();
        }

        /// <summary>Register an event track so its lane appears (even with no keyframes yet).</summary>
        public void SetEventTrackLabel(CAGEAnimation.EventTrack track, string label)
        {
            RegisterEventTrack(track, label);
        }

        private void RegisterEventTrack(CAGEAnimation.EventTrack track, string label)
        {
            if (track == null) return;
            _eventTrackLabels[track] = label ?? "";
            if (!_eventTrackOrder.Contains(track))
                _eventTrackOrder.Add(track);
        }

        /// <summary>Preferred type for empty tracks (until the first keyframe sets the real type).</summary>
        public void SetEventTrackPreferredType(CAGEAnimation.EventTrack track, ANIM_TRACK_TYPE type)
        {
            if (track == null) return;
            _eventTrackPreferredTypes[track] = type;
        }

        /// <summary>Resolve the single type for a track (all keyframes should match). Empty tracks use preferred type or T_STRING.</summary>
        public ANIM_TRACK_TYPE GetEventTrackType(CAGEAnimation.EventTrack track)
        {
            if (track == null) return ANIM_TRACK_TYPE.T_STRING;
            if (track.keyframes != null && track.keyframes.Count > 0)
            {
                for (int i = 0; i < track.keyframes.Count; i++)
                {
                    if (track.keyframes[i].track_type == ANIM_TRACK_TYPE.T_GUID)
                        return ANIM_TRACK_TYPE.T_GUID;
                }
                return ANIM_TRACK_TYPE.T_STRING;
            }
            ANIM_TRACK_TYPE preferred;
            if (_eventTrackPreferredTypes.TryGetValue(track, out preferred))
                return preferred;
            return ANIM_TRACK_TYPE.T_STRING;
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
            RemoveFloatKeyframe(_selectedCurve, _selectedKey);
        }

        public void RemoveSelectedEvent()
        {
            if (_selectedEvent == null) return;
            RemoveEventKeyframe(_selectedEvent);
        }

        private void RemoveFloatKeyframe(CurveInfo curve, CAGEAnimation.FloatTrack.Keyframe key)
        {
            if (curve == null || key == null) return;
            curve.Track.keyframes.Remove(key);
            if (_selectedKey == key)
            {
                _selectedKey = null;
                _selectedCurve = null;
                if (SelectionCleared != null) SelectionCleared();
            }
            if (_hoveredKey == key) _hoveredKey = null;
            Render();
            if (DataChanged != null) DataChanged();
        }

        private void RemoveEventKeyframe(EventInfo info)
        {
            if (info == null || info.Track == null || info.Key == null) return;
            info.Track.keyframes.Remove(info.Key);
            _events.Remove(info);
            if (_selectedEvent == info)
            {
                _selectedEvent = null;
                if (EventSelectionCleared != null) EventSelectionCleared();
            }
            if (_hoveredEvent == info) _hoveredEvent = null;
            Render();
            if (DataChanged != null) DataChanged();
        }

        public void SelectEvent(CAGEAnimation.EventTrack.Keyframe key)
        {
            EventInfo match = _events.FirstOrDefault(o => o.Key == key);
            if (match == null) return;
            ClearKeyframeSelectionSilent();
            _selectedEvent = match;
            _selectedEventTrack = match.Track;
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
            bool cleared = false;
            if (_selectedEvent != null)
            {
                _selectedEvent = null;
                cleared = true;
                if (EventSelectionCleared != null) EventSelectionCleared();
            }
            if (_selectedEventTrack != null)
            {
                _selectedEventTrack = null;
                cleared = true;
                if (EventTrackSelectionCleared != null) EventTrackSelectionCleared();
            }
            if (cleared) Render();
        }

        public void SelectEventTrack(CAGEAnimation.EventTrack track)
        {
            if (track == null) return;
            ClearKeyframeSelectionSilent();
            _selectedEvent = null;
            _selectedEventTrack = track;
            Render();
            if (EventTrackSelected != null) EventTrackSelected(track);
        }

        #region GEOMETRY

        private double EventLanesHeight()
        {
            int count = CountEventTracks();
            return count * _eventLaneH;
        }

        private int CountEventTracks()
        {
            return _eventTrackOrder.Count;
        }

        private double EventLaneResizeBarHeight()
        {
            return CountEventTracks() > 0 ? EVENT_LANE_RESIZE_H : 0;
        }

        private double BottomMargin()
        {
            return TIMELINE_STRIP_H + EventLaneResizeBarHeight() + EventLanesHeight() + 4;
        }

        private Rect Plot()
        {
            double w = ActualWidth > 1 ? ActualWidth : Width;
            double h = ActualHeight > 1 ? ActualHeight : Height;
            if (!IsFinite(w) || w < 1) w = 100;
            if (!IsFinite(h) || h < 1) h = 100;
            double pw = Math.Max(10.0, w - MARGIN_LEFT - MARGIN_RIGHT);
            double ph = Math.Max(10.0, h - MARGIN_TOP - BottomMargin());
            if (!IsFinite(pw) || pw < 10) pw = 10;
            if (!IsFinite(ph) || ph < 10) ph = 10;
            return new Rect(MARGIN_LEFT, MARGIN_TOP, pw, ph);
        }

        private static bool IsFinite(double v)
        {
            return !double.IsNaN(v) && !double.IsInfinity(v);
        }

        private void SanitizeViewState()
        {
            if (!IsFinite(_start) || !IsFinite(_end) || _end <= _start)
            {
                _start = 0f;
                _end = Math.Max(_animLength, 1f);
            }
            if (!IsFinite(_minV) || !IsFinite(_maxV) || _maxV <= _minV)
            {
                _minV = 0f;
                _maxV = 1f;
            }
            if (!IsFinite(_animLength) || _animLength <= 0f)
                _animLength = 1f;
        }

        /// <summary>Hit region under the event lanes used for time zoom / timeline scroll.</summary>
        private Rect TimeScrollArea(Rect p)
        {
            double lanesH = EventLaneResizeBarHeight() + EventLanesHeight();
            double top = p.Top + p.Height + lanesH + 2;
            double h = ActualHeight > 0 ? ActualHeight : Height;
            double bottom = Math.Max(top + TIMELINE_STRIP_H, h - 2);
            return new Rect(p.Left, top, p.Width, Math.Max(TIME_SCROLL_BAR_H + 8, bottom - top));
        }

        private Rect EventLaneResizeGripBounds(Rect p)
        {
            if (CountEventTracks() == 0) return Rect.Empty;
            return new Rect(p.Left, p.Top + p.Height, p.Width, EVENT_LANE_RESIZE_H);
        }

        private Rect EventLaneBounds(int laneIndex, Rect p)
        {
            double y = p.Top + p.Height + EventLaneResizeBarHeight() + laneIndex * _eventLaneH;
            return new Rect(p.Left, y, p.Width, Math.Max(2, _eventLaneH - 2));
        }

        private double MaxEventLaneHeight()
        {
            int count = CountEventTracks();
            if (count <= 0) return EVENT_LANE_H_DEFAULT;

            double h = ActualHeight > 0 ? ActualHeight : Height;
            double available = h - MARGIN_TOP - TIMELINE_STRIP_H - EVENT_LANE_RESIZE_H - 4 - MIN_PLOT_H;
            if (available <= 0) return EVENT_LANE_H_MIN;
            return Math.Min(EVENT_LANE_H_MAX, available / count);
        }

        private void ClampEventLaneHeight()
        {
            double maxH = MaxEventLaneHeight();
            if (_eventLaneH > maxH) _eventLaneH = maxH;
            if (_eventLaneH < EVENT_LANE_H_MIN) _eventLaneH = EVENT_LANE_H_MIN;
        }

        private double ToX(double time, Rect p)
        {
            if (!IsFinite(time) || !IsFinite(p.Left) || !IsFinite(p.Width) || p.Width <= 0)
                return IsFinite(p.Left) ? p.Left : MARGIN_LEFT;
            double span = _end - _start;
            if (span <= 1e-8 || !IsFinite(span))
                span = 1.0;
            double x = p.Left + (time - _start) / span * p.Width;
            return IsFinite(x) ? x : p.Left;
        }
        private double ToY(double value, Rect p)
        {
            if (!IsFinite(value) || !IsFinite(p.Top) || !IsFinite(p.Height) || p.Height <= 0)
                return IsFinite(p.Top) ? p.Top : MARGIN_TOP;
            double range = _maxV - _minV;
            if (range <= 0 || !IsFinite(range)) range = 1;
            double y = p.Top + (1.0 - (value - _minV) / range) * p.Height;
            return IsFinite(y) ? y : p.Top;
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

        // Value of a curve at a given time — matches DrawCurves bezier evaluation.
        private float ApproxValueAt(CurveInfo c, float time)
        {
            List<CAGEAnimation.FloatTrack.Keyframe> keys = Sorted(c.Track);
            if (keys.Count == 0) return 0f;
            if (time <= keys[0].time) return keys[0].value.Y;
            if (time >= keys[keys.Count - 1].time) return keys[keys.Count - 1].value.Y;
            for (int i = 0; i < keys.Count - 1; i++)
            {
                CAGEAnimation.FloatTrack.Keyframe a = keys[i];
                CAGEAnimation.FloatTrack.Keyframe b = keys[i + 1];
                if (time < a.time || time > b.time) continue;

                float span = b.time - a.time;
                if (span <= 1e-8f) return a.value.Y;
                float u = (time - a.time) / span;
                float dt = span / 3f;
                float y0 = a.value.Y;
                float y1 = a.value.Y + SlopeOut(a, b) * dt;
                float y2 = b.value.Y - SlopeIn(a, b) * dt;
                float y3 = b.value.Y;
                float omu = 1f - u;
                return omu * omu * omu * y0
                    + 3f * omu * omu * u * y1
                    + 3f * omu * u * u * y2
                    + u * u * u * y3;
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
            SanitizeViewState();
            mainCanvas.Children.Clear();
            _keyHits.Clear();
            _curvePathHits.Clear();
            _eventHits.Clear();
            _eventLanes.Clear();
            _hasTanHandles = false;
            ClampEventLaneHeight();

            Rect p = Plot();
            if (!IsFinite(p.Width) || !IsFinite(p.Height) || p.Width < 1 || p.Height < 1)
                return;

            // Plot background
            Rectangle bg = new Rectangle() { Width = p.Width, Height = p.Height, Fill = PlotBg, Stroke = AxisBrush, StrokeThickness = 1 };
            Canvas.SetLeft(bg, p.Left);
            Canvas.SetTop(bg, p.Top);
            mainCanvas.Children.Add(bg);

            DrawPlayableRange(p);
            DrawGrid(p);

            // Clipped layer for graph content (curves + handles) so zoomed geometry can't bleed out
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
            DrawSelection(p);
            DrawEventLaneResizeGrip(p);
            DrawEventLanes(p);

            if (_curves.Count == 0 && _events.Count == 0 && _eventTrackLabels.Count == 0)
                AddText("No curves yet — add an entity from the track list, or right-click to add an event track.", p.Left + 8, p.Top + 8, LabelBrush, 11, false);

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
                    Fill = AnimLengthPlotFill,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(playShade, playLeft);
                Canvas.SetTop(playShade, scrollArea.Top + 1);
                mainCanvas.Children.Add(playShade);
            }

            // Time labels
            int vLines = 8;
            double labelY = scrollArea.Top + Math.Max(2, (scrollArea.Height - 14) * 0.5);
            for (int i = 0; i <= vLines; i++)
            {
                double t = _start + (_end - _start) * i / vLines;
                double x = ToX(t, p);
                AddText(t.ToString("0.##") + "s", x - 14, labelY, LabelBrush, 11, true);
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
                        Y1 = scrollArea.Top + scrollArea.Height * 0.28 + i * (scrollArea.Height * 0.18),
                        Y2 = scrollArea.Top + scrollArea.Height * 0.28 + i * (scrollArea.Height * 0.18),
                        Stroke = Brushes.White,
                        StrokeThickness = 1
                    };
                    mainCanvas.Children.Add(notch);
                }
            }
        }

        private void DrawEventLaneResizeGrip(Rect p)
        {
            _eventLaneResizeGrip = EventLaneResizeGripBounds(p);
            if (_eventLaneResizeGrip.IsEmpty) return;

            bool active = _hoveredEventLaneResize || _drag == DragMode.EventLaneResize;
            Rectangle bar = new Rectangle()
            {
                Width = _eventLaneResizeGrip.Width,
                Height = _eventLaneResizeGrip.Height,
                Fill = active ? LaneResizeFillHover : LaneResizeFill,
                Stroke = AxisBrush,
                StrokeThickness = 1,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(bar, _eventLaneResizeGrip.Left);
            Canvas.SetTop(bar, _eventLaneResizeGrip.Top);
            mainCanvas.Children.Add(bar);

            // Center grip notches
            double midX = _eventLaneResizeGrip.Left + _eventLaneResizeGrip.Width * 0.5;
            double midY = _eventLaneResizeGrip.Top + _eventLaneResizeGrip.Height * 0.5;
            for (int i = -1; i <= 1; i++)
            {
                Line notch = new Line()
                {
                    X1 = midX - 10,
                    X2 = midX + 10,
                    Y1 = midY + i * 2,
                    Y2 = midY + i * 2,
                    Stroke = LaneResizeGrip,
                    StrokeThickness = 1,
                    IsHitTestVisible = false
                };
                mainCanvas.Children.Add(notch);
            }
        }

        private void DrawEventLanes(Rect p)
        {
            // Preserve a stable lane order from registration
            List<CAGEAnimation.EventTrack> tracks = new List<CAGEAnimation.EventTrack>(_eventTrackOrder);
            foreach (EventInfo ev in _events)
            {
                if (ev.Track != null && !tracks.Contains(ev.Track))
                    tracks.Add(ev.Track);
            }

            for (int i = 0; i < tracks.Count; i++)
            {
                CAGEAnimation.EventTrack track = tracks[i];
                ANIM_TRACK_TYPE type = GetEventTrackType(track);
                bool guid = type == ANIM_TRACK_TYPE.T_GUID;
                Rect lane = EventLaneBounds(i, p);
                bool selected = track == _selectedEventTrack || (_selectedEvent != null && _selectedEvent.Track == track);
                bool hovered = track == _hoveredEventTrack;

                Brush laneFill;
                if (guid)
                    laneFill = selected ? GuidLaneFillSelected : (hovered ? GuidLaneFillHover : GuidLaneFill);
                else
                    laneFill = selected ? StringLaneFillSelected : (hovered ? StringLaneFillHover : StringLaneFill);
                Brush laneBorder = guid
                    ? (selected ? GuidEventSelectedBrush : GuidEventBrush)
                    : (selected ? EventSelectedBrush : EventBrush);

                Rectangle bar = new Rectangle()
                {
                    Width = lane.Width,
                    Height = lane.Height,
                    Fill = laneFill,
                    Stroke = laneBorder,
                    StrokeThickness = selected ? 2 : 1
                };
                Canvas.SetLeft(bar, lane.Left);
                Canvas.SetTop(bar, lane.Top);
                mainCanvas.Children.Add(bar);

                // Soft playable-range tint within the lane
                double playLeft = Math.Max(lane.Left, Math.Min(lane.Left + lane.Width, ToX(0f, p)));
                double playRight = Math.Max(lane.Left, Math.Min(lane.Left + lane.Width, ToX(_animLength, p)));
                if (playRight > playLeft)
                {
                    Rectangle playShade = new Rectangle()
                    {
                        Width = playRight - playLeft,
                        Height = lane.Height - 2,
                        Fill = AnimLengthPlotFill,
                        IsHitTestVisible = false
                    };
                    Canvas.SetLeft(playShade, playLeft);
                    Canvas.SetTop(playShade, lane.Top + 1);
                    mainCanvas.Children.Add(playShade);
                }

                string laneLabel;
                if (!_eventTrackLabels.TryGetValue(track, out laneLabel))
                    laneLabel = "";

                _eventLanes.Add(new EventLaneInfo()
                {
                    Track = track,
                    Label = laneLabel,
                    TrackType = type,
                    Bounds = lane
                });

                // Markers for this track
                foreach (EventInfo ev in _events)
                {
                    if (ev.Track != track) continue;
                    double screenX = ToX(ev.Key.time, p);
                    if (screenX < lane.Left - 2 || screenX > lane.Left + lane.Width + 2) continue;

                    bool keySelected = ev == _selectedEvent;
                    double midY = lane.Top + lane.Height * 0.5;
                    Line line = new Line()
                    {
                        X1 = screenX,
                        Y1 = lane.Top + 3,
                        X2 = screenX,
                        Y2 = lane.Top + lane.Height - 3
                    };
                    mainCanvas.Children.Add(line);

                    Polygon tip = new Polygon()
                    {
                        Points = new PointCollection(new Point[]
                        {
                            new Point(screenX, lane.Top + 2),
                            new Point(screenX - 4, lane.Top + 8),
                            new Point(screenX + 4, lane.Top + 8)
                        })
                    };
                    mainCanvas.Children.Add(tip);

                    if (!string.IsNullOrWhiteSpace(ev.Label) || (ev.Key != null && ev.Key.track_type == ANIM_TRACK_TYPE.T_STRING))
                    {
                        string displayLabel = ev.Label;
                        if (ev.Key != null && ev.Key.track_type == ANIM_TRACK_TYPE.T_STRING)
                        {
                            displayLabel = ev.Key.forward.ToString();
                            if (string.IsNullOrWhiteSpace(displayLabel))
                                displayLabel = "event";
                        }
                        if (!string.IsNullOrWhiteSpace(displayLabel))
                        {
                            string shortLabel = displayLabel.Length > 22 ? displayLabel.Substring(0, 20) + "…" : displayLabel;
                            Brush textBrush = guid
                                ? (keySelected ? GuidEventSelectedBrush : GuidEventLabelBrush)
                                : (keySelected ? EventSelectedBrush : EventLabelBrush);
                            double textY = lane.Top + Math.Max(2, (lane.Height - 12) * 0.5);
                            AddText(shortLabel, screenX + 6, textY, textBrush, 9, keySelected);
                        }
                    }

                    EventHit hit = new EventHit() { Info = ev, ScreenX = screenX, Line = line, Tip = tip, LaneMidY = midY, LaneTop = lane.Top, LaneBottom = lane.Top + lane.Height };
                    ApplyEventVisual(hit);
                    _eventHits.Add(hit);
                }
            }
        }

        private static bool IsGuidEvent(EventInfo ev)
        {
            return ev != null && ev.Key != null && ev.Key.track_type == ANIM_TRACK_TYPE.T_GUID;
        }

        private void ApplyEventVisual(EventHit hit)
        {
            if (hit == null || hit.Line == null) return;
            bool selected = hit.Info == _selectedEvent;
            bool hovered = hit.Info == _hoveredEvent;
            bool guid = IsGuidEvent(hit.Info);

            Brush stroke;
            if (guid)
                stroke = selected ? GuidEventSelectedBrush : (hovered ? GuidEventHoverBrush : GuidEventBrush);
            else
                stroke = selected ? EventSelectedBrush : (hovered ? EventHoverBrush : EventBrush);

            hit.Line.Stroke = stroke;
            hit.Line.StrokeThickness = selected ? 3.0 : (hovered ? 2.5 : 2.0);
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
                AddText(v.ToString("0.###"), 4, y - 8, LabelBrush, 11, true);
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
                    Path path = new Path() { Data = geo, Stroke = stroke, StrokeLineJoin = PenLineJoin.Round };
                    ApplyCurvePathVisual(c, path);
                    AddPlotChild(path);
                    _curvePathHits.Add(new CurvePathHit() { Curve = c, Path = path });
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

        private void ApplyCurvePathVisual(CurveInfo curve, Path path)
        {
            if (path == null) return;
            bool hovered = curve == _hoveredCurve;
            bool selected = _selectedCurve == curve && _selectedKey != null;
            path.StrokeThickness = hovered ? 4.0 : (selected ? 2.75 : 2.0);
        }

        private void RefreshCurvePathVisuals()
        {
            foreach (CurvePathHit hit in _curvePathHits)
                ApplyCurvePathVisual(hit.Curve, hit.Path);
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
            bool overLanes = !overTimeline && pos.Y >= p.Top + p.Height && pos.Y < TimeScrollArea(p).Top;
            bool overPlot = p.Contains(pos);

            // Timeline strip + event lanes: zoom / pan time
            if (overTimeline || overLanes)
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
            // Middle-mouse drag pans the view (Ctrl or timeline strip = time only)
            if (e.ChangedButton != MouseButton.Middle) return;
            Rect p = Plot();
            Point pos = e.GetPosition(mainCanvas);
            bool overTimeline = TimeScrollArea(p).Contains(pos)
                || (pos.Y >= p.Top + p.Height && pos.Y < TimeScrollArea(p).Top);
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

            _drag = DragMode.Pan;
            _dragging = true;
            _panTimeOnly = ctrl || overTimeline;
            _panOrigin = pos;
            _panStartView = _start;
            _panEndView = _end;
            _panMinV = _minV;
            _panMaxV = _maxV;
            mainCanvas.CaptureMouse();
            mainCanvas.Cursor = _panTimeOnly ? Cursors.SizeWE : Cursors.SizeAll;
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

        private void Canvas_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragging) return;

            Point pos = e.GetPosition(mainCanvas);
            Rect p = Plot();
            ContextMenu menu = BuildContextMenu(pos, p);
            if (menu == null || menu.Items.Count == 0) return;

            mainCanvas.ContextMenu = menu;
            menu.IsOpen = true;
            e.Handled = true;
        }

        private ContextMenu BuildContextMenu(Point pos, Rect p)
        {
            ContextMenu menu = new ContextMenu();

            EventInfo hitEvent = HitTestEvent(pos, p);
            EventLaneInfo laneHit = hitEvent == null ? HitTestEventLane(pos) : null;
            KeyHit hitKey = HitTestKey(pos);
            CurveInfo nearCurve = null;
            double curveDist = double.MaxValue;
            if (hitKey == null && hitEvent == null && laneHit == null && p.Contains(pos))
                nearCurve = HitTestNearestCurve(pos, p, out curveDist);

            if (hitEvent != null)
            {
                ClearKeyframeSelectionSilent();
                _selectedEvent = hitEvent;
                _selectedEventTrack = hitEvent.Track;
                Render();
                RaiseEventSelected();

                float t = QuantizeTime((float)TimeAt(pos.X, p));
                CAGEAnimation.EventTrack track = hitEvent.Track;

                MenuItem addEvt = new MenuItem() { Header = "Add Event Here" };
                addEvt.Click += (s, ev) =>
                {
                    if (EventAddRequested != null) EventAddRequested(t, track);
                };
                menu.Items.Add(addEvt);

                MenuItem delEvt = new MenuItem() { Header = "Delete Event" };
                delEvt.Click += (s, ev) => RemoveEventKeyframe(hitEvent);
                menu.Items.Add(delEvt);

                menu.Items.Add(new Separator());

                MenuItem delTrack = new MenuItem() { Header = "Delete Event Track" };
                delTrack.Click += (s, ev) =>
                {
                    if (EventTrackDeleteRequested != null) EventTrackDeleteRequested(track);
                };
                menu.Items.Add(delTrack);
                AppendAddEventTrackMenuItems(menu, true);
                return menu;
            }

            if (laneHit != null)
            {
                ClearKeyframeSelectionSilent();
                if (_selectedEvent != null)
                {
                    _selectedEvent = null;
                    if (EventSelectionCleared != null) EventSelectionCleared();
                }
                _selectedEventTrack = laneHit.Track;
                Render();
                if (EventTrackSelected != null) EventTrackSelected(laneHit.Track);

                float t = QuantizeTime((float)TimeAt(pos.X, p));
                CAGEAnimation.EventTrack track = laneHit.Track;

                MenuItem addEvt = new MenuItem() { Header = "Add Event Here" };
                addEvt.Click += (s, ev) =>
                {
                    if (EventAddRequested != null) EventAddRequested(t, track);
                };
                menu.Items.Add(addEvt);

                MenuItem delTrack = new MenuItem() { Header = "Delete Event Track" };
                delTrack.Click += (s, ev) =>
                {
                    if (EventTrackDeleteRequested != null) EventTrackDeleteRequested(track);
                };
                menu.Items.Add(delTrack);
                AppendAddEventTrackMenuItems(menu, true);
                return menu;
            }

            if (hitKey != null)
            {
                ClearEventSelectionSilent();
                _selectedCurve = hitKey.Curve;
                _selectedKey = hitKey.Key;
                Render();
                RaiseSelected();

                MenuItem delKey = new MenuItem() { Header = "Delete Keyframe" };
                delKey.Click += (s, ev) => RemoveFloatKeyframe(hitKey.Curve, hitKey.Key);
                menu.Items.Add(delKey);
                AppendAddEventTrackMenuItems(menu, true);
                return menu;
            }

            // Only offer "Add Keyframe Here" when the cursor is on a curve line
            if (nearCurve != null && curveDist <= CURVE_HIT_Y)
            {
                MenuItem addKey = new MenuItem() { Header = "Add Keyframe Here" };
                CurveInfo curve = nearCurve;
                addKey.Click += (s, ev) => AddKeyframeOnCurve(curve, pos, p);
                menu.Items.Add(addKey);
                AppendAddEventTrackMenuItems(menu, true);
                return menu;
            }

            // Empty plot / timeline / background — still allow adding event tracks
            AppendAddEventTrackMenuItems(menu, false);
            return menu.Items.Count > 0 ? menu : null;
        }

        private void AppendAddEventTrackMenuItems(ContextMenu menu, bool leadingSeparator)
        {
            if (menu == null) return;
            if (leadingSeparator && menu.Items.Count > 0)
                menu.Items.Add(new Separator());

            MenuItem addString = new MenuItem() { Header = "Add Event Track" };
            addString.Click += (s, ev) =>
            {
                if (EventTrackAddRequested != null) EventTrackAddRequested(ANIM_TRACK_TYPE.T_STRING);
            };
            menu.Items.Add(addString);

            MenuItem addGuid = new MenuItem() { Header = "Add Animation Entity Track" };
            addGuid.Click += (s, ev) =>
            {
                if (EventTrackAddRequested != null) EventTrackAddRequested(ANIM_TRACK_TYPE.T_GUID);
            };
            menu.Items.Add(addGuid);
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

            // Event-lane height resize grip (between plot and lanes)
            if (HitTestEventLaneResize(pos))
            {
                _drag = DragMode.EventLaneResize;
                _hoveredEventLaneResize = true;
                _laneResizeOriginY = pos.Y;
                _laneResizeStartH = _eventLaneH;
                mainCanvas.Cursor = Cursors.SizeNS;
                BeginDrag();
                e.Handled = true;
                return;
            }

            // Shift+click on an event lane → host prompts for a new event keyframe
            EventLaneInfo laneHit = HitTestEventLane(pos);
            if (laneHit != null && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                float t = QuantizeTime((float)TimeAt(pos.X, p));
                if (EventAddRequested != null) EventAddRequested(t, laneHit.Track);
                e.Handled = true;
                return;
            }

            // Shift+click on a curve → add a float keyframe on the nearest line
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift && p.Contains(pos))
            {
                if (TryAddKeyframeNearCurve(pos, p))
                {
                    e.Handled = true;
                    return;
                }
            }

            // Tangent handles take priority (they sit on top of the selected keyframe)
            if (_hasTanHandles)
            {
                if (Dist(pos, _tanOutScreen) <= HIT_RADIUS) { _drag = DragMode.TanOut; BeginDrag(); return; }
                if (Dist(pos, _tanInScreen) <= HIT_RADIUS) { _drag = DragMode.TanIn; BeginDrag(); return; }
            }

            // Event marker hit test (within its lane)
            EventInfo hitEvent = HitTestEvent(pos, p);
            if (hitEvent != null)
            {
                bool alreadySelected = (_selectedEvent == hitEvent);
                ClearKeyframeSelectionSilent();
                _selectedEvent = hitEvent;
                _selectedEventTrack = hitEvent.Track;
                _hoveredEvent = hitEvent;
                Render();
                RaiseEventSelected();
                if (EventTrackSelected != null && hitEvent.Track != null)
                    EventTrackSelected(hitEvent.Track);
                // First click selects only; drag starts on a later click while already selected
                if (alreadySelected)
                {
                    _drag = DragMode.Event;
                    mainCanvas.Cursor = Cursors.SizeWE;
                    BeginDrag();
                }
                return;
            }

            // Event lane bar click (connections UI)
            if (laneHit != null)
            {
                ClearKeyframeSelectionSilent();
                if (_selectedEvent != null)
                {
                    _selectedEvent = null;
                    if (EventSelectionCleared != null) EventSelectionCleared();
                }
                _selectedEventTrack = laneHit.Track;
                Render();
                if (EventTrackSelected != null) EventTrackSelected(laneHit.Track);
                return;
            }

            // Keyframe hit test (nearest within radius)
            KeyHit best = HitTestKey(pos);
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
            if (_selectedEventTrack != null)
            {
                _selectedEventTrack = null;
                cleared = true;
                if (EventTrackSelectionCleared != null) EventTrackSelectionCleared();
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
            if (_hoveredEventTrack != null)
            {
                _hoveredEventTrack = null;
                Render();
            }
            if (_hoveredEventLaneResize)
            {
                _hoveredEventLaneResize = false;
                Render();
            }
            if (_hoveredKey != null)
            {
                _hoveredKey = null;
                RefreshKeyVisuals();
            }
            if (_hoveredCurve != null)
            {
                _hoveredCurve = null;
                RefreshCurvePathVisuals();
            }
            mainCanvas.Cursor = Cursors.Cross;
        }

        private EventInfo HitTestEvent(Point pos, Rect p)
        {
            EventInfo best = null;
            double bestDist = EVENT_HIT_X + 1;
            foreach (EventHit eh in _eventHits)
            {
                if (pos.Y < eh.LaneTop || pos.Y > eh.LaneBottom) continue;
                double d = Math.Abs(pos.X - eh.ScreenX);
                if (d <= EVENT_HIT_X && d < bestDist)
                {
                    best = eh.Info;
                    bestDist = d;
                }
            }
            return best;
        }

        private EventLaneInfo HitTestEventLane(Point pos)
        {
            foreach (EventLaneInfo lane in _eventLanes)
            {
                if (lane.Bounds.Contains(pos))
                    return lane;
            }
            return null;
        }

        private bool HitTestEventLaneResize(Point pos)
        {
            if (_eventLaneResizeGrip.IsEmpty) return false;
            // Slightly expand hit area vertically for easier grabbing
            Rect hit = new Rect(
                _eventLaneResizeGrip.Left,
                _eventLaneResizeGrip.Top - 2,
                _eventLaneResizeGrip.Width,
                _eventLaneResizeGrip.Height + 4);
            return hit.Contains(pos);
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
            if (overLengthHandle != _hoveredAnimLengthHandle)
            {
                _hoveredAnimLengthHandle = overLengthHandle;
                if (overLengthHandle)
                {
                    mainCanvas.Cursor = Cursors.SizeWE;
                    return;
                }
            }
            if (overLengthHandle)
            {
                mainCanvas.Cursor = Cursors.SizeWE;
                return;
            }

            bool overLaneResize = HitTestEventLaneResize(pos);
            if (overLaneResize != _hoveredEventLaneResize)
            {
                _hoveredEventLaneResize = overLaneResize;
                Render();
            }
            if (overLaneResize)
            {
                mainCanvas.Cursor = Cursors.SizeNS;
                return;
            }

            // Prefer keyframe markers when the cursor is near both
            KeyHit keyHit = HitTestKey(pos);
            EventInfo eventHit = keyHit == null ? HitTestEvent(pos, p) : null;
            EventLaneInfo laneHit = (keyHit == null && eventHit == null) ? HitTestEventLane(pos) : null;

            CurveInfo nextCurve = null;
            if (keyHit == null && eventHit == null && laneHit == null && p.Contains(pos))
            {
                double curveDist;
                CurveInfo near = HitTestNearestCurve(pos, p, out curveDist);
                if (near != null && curveDist <= CURVE_HIT_Y)
                    nextCurve = near;
            }

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

            CAGEAnimation.EventTrack nextLane = laneHit == null ? null : laneHit.Track;
            if (nextLane != _hoveredEventTrack)
            {
                _hoveredEventTrack = nextLane;
                Render();
            }

            if (nextCurve != _hoveredCurve)
            {
                _hoveredCurve = nextCurve;
                RefreshCurvePathVisuals();
            }

            if (nextKey != null)
                mainCanvas.Cursor = Cursors.SizeAll;
            else if (nextEvent != null)
                mainCanvas.Cursor = Cursors.SizeWE;
            else if (nextLane != null)
                mainCanvas.Cursor = Cursors.Hand;
            else if (nextCurve != null)
                mainCanvas.Cursor = Cursors.Hand;
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
                        float tSpan = _panEndView - _panStartView;
                        float dt = (float)(-dx / p.Width * tSpan);

                        _start = _panStartView + dt;
                        _end = _panEndView + dt;
                        ClampView();

                        if (!_panTimeOnly)
                        {
                            double dy = pos.Y - _panOrigin.Y;
                            float vSpan = _panMaxV - _panMinV;
                            if (vSpan <= 0f) vSpan = 1f;
                            // Screen Y grows downward; dragging down should reveal higher values
                            float dv = (float)(dy / p.Height * vSpan);
                            _minV = _panMinV + dv;
                            _maxV = _panMaxV + dv;
                        }

                        mainCanvas.Cursor = _panTimeOnly ? Cursors.SizeWE : Cursors.SizeAll;
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
                case DragMode.EventLaneResize:
                    {
                        // Dragging up (smaller Y) expands lanes into the plot
                        double delta = _laneResizeOriginY - pos.Y;
                        _eventLaneH = _laneResizeStartH + delta;
                        ClampEventLaneHeight();
                        mainCanvas.Cursor = Cursors.SizeNS;
                        Render();
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

        private CurveInfo HitTestNearestCurve(Point pos, Rect p, out double distY)
        {
            float t = QuantizeTime((float)TimeAt(pos.X, p));
            CurveInfo bestCurve = null;
            distY = double.MaxValue;
            foreach (CurveInfo c in _curves)
            {
                if (!c.Visible) continue;
                List<CAGEAnimation.FloatTrack.Keyframe> keys = Sorted(c.Track);
                if (keys.Count < 2) continue;
                if (t < keys[0].time || t > keys[keys.Count - 1].time) continue;

                double dy = Math.Abs(ToY(ApproxValueAt(c, t), p) - pos.Y);
                if (dy < distY)
                {
                    distY = dy;
                    bestCurve = c;
                }
            }
            return bestCurve;
        }

        /// <summary>Add a keyframe on the visible curve nearest to the click, if within hit distance.</summary>
        private bool TryAddKeyframeNearCurve(Point pos, Rect p)
        {
            double dist;
            CurveInfo target = HitTestNearestCurve(pos, p, out dist);
            if (target == null || dist > CURVE_HIT_Y) return false;
            AddKeyframeOnCurve(target, pos, p);
            return true;
        }

        private void AddKeyframeOnCurve(CurveInfo target, Point pos, Rect p)
        {
            if (target == null) return;

            float t = QuantizeTime((float)TimeAt(pos.X, p));
            float v = ApproxValueAt(target, t);
            float clickV = (float)ValueAt(pos.Y, p);
            if (Math.Abs(ToY(clickV, p) - ToY(v, p)) <= CURVE_HIT_Y)
                v = clickV;

            ClearEventSelectionSilent();

            CAGEAnimation.FloatTrack.Keyframe key = new CAGEAnimation.FloatTrack.Keyframe();
            key.time = t;
            key.value.Y = v;
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

        private void AddKeyframeAt(Point pos, Rect p)
        {
            TryAddKeyframeNearCurve(pos, p);
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
            if (_selectedEvent != null)
            {
                _selectedEvent = null;
                if (EventSelectionCleared != null) EventSelectionCleared();
            }
            if (_selectedEventTrack != null)
            {
                _selectedEventTrack = null;
                if (EventTrackSelectionCleared != null) EventTrackSelectionCleared();
            }
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

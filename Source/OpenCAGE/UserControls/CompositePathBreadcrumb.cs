using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace OpenCAGE.UserControls
{
    public class CompositePathBreadcrumb : UserControl
    {
        public event Action<int> SegmentClicked;

        private readonly Panel _viewport;
        private readonly FlowLayoutPanel _flow;
        private readonly MouseWheelMessageFilter _wheelFilter;

        private int _scrollX;
        private bool _dragArmed;
        private bool _dragging;
        private int _dragStartX;
        private int _dragScrollStart;

        public CompositePathBreadcrumb()
        {
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
            BackColor = SystemColors.Window;
            BorderStyle = BorderStyle.FixedSingle;

            _viewport = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = false,
                Padding = new Padding(2, 0, 2, 0),
                BackColor = SystemColors.Window,
            };
            _viewport.Resize += (s, e) => ApplyScroll();
            WirePanEvents(_viewport);

            _flow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = Padding.Empty,
                Padding = new Padding(2, 1, 2, 1),
                BackColor = SystemColors.Window,
                Location = Point.Empty,
            };
            WirePanEvents(_flow);
            _flow.SizeChanged += (s, e) => ApplyScroll();

            _viewport.Controls.Add(_flow);
            Controls.Add(_viewport);

            _wheelFilter = new MouseWheelMessageFilter(this);
            Application.AddMessageFilter(_wheelFilter);
            Disposed += (s, e) => Application.RemoveMessageFilter(_wheelFilter);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ScrollBy(-e.Delta);
            if (e is HandledMouseEventArgs handled)
                handled.Handled = true;
            base.OnMouseWheel(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Left
                || keyData == Keys.Right
                || keyData == Keys.Home
                || keyData == Keys.End
                || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            int step = Math.Max(24, _viewport.ClientSize.Width / 4);
            switch (e.KeyCode)
            {
                case Keys.Left:
                    ScrollBy(-step);
                    e.Handled = true;
                    break;
                case Keys.Right:
                    ScrollBy(step);
                    e.Handled = true;
                    break;
                case Keys.Home:
                    _scrollX = 0;
                    ApplyScroll();
                    e.Handled = true;
                    break;
                case Keys.End:
                    _scrollX = GetMaxScrollX();
                    ApplyScroll();
                    e.Handled = true;
                    break;
            }

            base.OnKeyDown(e);
        }

        public void SetPath(CompositePath path, Composite currentComposite)
        {
            _flow.SuspendLayout();
            _flow.Controls.Clear();

            if (path == null || currentComposite == null)
            {
                _flow.ResumeLayout(true);
                _scrollX = 0;
                ApplyScroll();
                return;
            }

            bool showShortGuids = SettingsManager.GetBool(Settings.ShowShortGuids);
            var segments = path.GetPathRich(currentComposite);

            for (int i = 0; i < segments.Count; i++)
            {
                if (i > 0)
                {
                    Label separator = CreateSeparator();
                    WirePanEvents(separator);
                    _flow.Controls.Add(separator);
                }

                bool isCurrent = i == segments.Count - 1;
                string label = FormatCompositeLabel(segments[i].Composite, showShortGuids);

                if (isCurrent)
                {
                    Label current = CreateCurrentSegmentLabel(label);
                    WirePanEvents(current);
                    _flow.Controls.Add(current);
                }
                else
                {
                    // Do not attach pan handlers to links - that breaks LinkClicked
                    _flow.Controls.Add(CreateLinkSegment(i, label));
                }
            }

            _flow.ResumeLayout(true);
            _flow.PerformLayout();

            // Keep the current (last) segment visible without showing a scrollbar
            _scrollX = GetMaxScrollX();
            ApplyScroll();
        }

        internal void ScrollBy(int delta)
        {
            if (GetMaxScrollX() <= 0)
                return;

            // Mouse wheel deltas are typically ±120; keep a usable pan speed.
            int pixels = delta;
            if (Math.Abs(delta) >= 120)
                pixels = delta / 2;

            _scrollX = Math.Max(0, Math.Min(GetMaxScrollX(), _scrollX + pixels));
            ApplyScroll();
        }

        private int GetMaxScrollX()
        {
            return Math.Max(0, _flow.PreferredSize.Width - _viewport.ClientSize.Width);
        }

        private void ApplyScroll()
        {
            int max = GetMaxScrollX();
            _scrollX = Math.Max(0, Math.Min(max, _scrollX));

            int y = Math.Max(0, (_viewport.ClientSize.Height - _flow.Height) / 2);
            _flow.Location = new Point(-_scrollX, y);
            Cursor cursor = max > 0 ? Cursors.SizeWE : Cursors.Default;
            _viewport.Cursor = cursor;
            _flow.Cursor = cursor;
        }

        private bool IsMouseOverBreadcrumb()
        {
            if (!IsHandleCreated || !Visible)
                return false;

            return RectangleToScreen(ClientRectangle).Contains(Control.MousePosition);
        }

        private void WirePanEvents(Control control)
        {
            control.MouseDown += Pan_MouseDown;
            control.MouseMove += Pan_MouseMove;
            control.MouseUp += Pan_MouseUp;
        }

        private void Pan_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || GetMaxScrollX() <= 0)
                return;

            _dragArmed = true;
            _dragging = false;
            _dragStartX = Control.MousePosition.X;
            _dragScrollStart = _scrollX;
        }

        private void Pan_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragArmed || e.Button != MouseButtons.Left)
                return;

            int delta = _dragStartX - Control.MousePosition.X;
            if (!_dragging && Math.Abs(delta) > 4)
            {
                _dragging = true;
                Capture = true;
            }

            if (!_dragging)
                return;

            _scrollX = Math.Max(0, Math.Min(GetMaxScrollX(), _dragScrollStart + delta));
            ApplyScroll();
        }

        private void Pan_MouseUp(object sender, MouseEventArgs e)
        {
            _dragArmed = false;
            _dragging = false;
            Capture = false;
        }

        private static string FormatCompositeLabel(Composite composite, bool showShortGuids)
        {
            if (composite == null)
                return "?";

            string name = EditorUtils.GetCompositeName(composite);
            if (!showShortGuids)
                return name;

            return "[" + composite.shortGUID.ToByteString() + "] " + name;
        }

        private static Label CreateSeparator()
        {
            return new Label
            {
                AutoSize = true,
                Margin = new Padding(2, 0, 2, 2),
                ForeColor = SystemColors.GrayText,
                Text = "›",
                TextAlign = ContentAlignment.MiddleCenter,
            };
        }

        private Label CreateCurrentSegmentLabel(string text)
        {
            return new Label
            {
                AutoSize = true,
                Margin = new Padding(2, 1, 2, 0),
                Font = new Font(Font, FontStyle.Bold),
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft,
            };
        }

        private LinkLabel CreateLinkSegment(int segmentIndex, string text)
        {
            LinkLabel link = new LinkLabel
            {
                AutoSize = true,
                Margin = new Padding(2, 1, 2, 0),
                LinkBehavior = LinkBehavior.HoverUnderline,
                LinkColor = SystemColors.HotTrack,
                ActiveLinkColor = SystemColors.HotTrack,
                VisitedLinkColor = SystemColors.HotTrack,
                Text = text,
                Tag = segmentIndex,
                Cursor = Cursors.Hand,
            };
            // Ensure the whole label text is a clickable link area
            link.Links.Clear();
            link.Links.Add(0, link.Text.Length, segmentIndex);
            link.LinkClicked += OnSegmentLinkClicked;
            return link;
        }

        private void OnSegmentLinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            int segmentIndex;
            if (e.Link?.LinkData is int fromLink)
                segmentIndex = fromLink;
            else if (sender is LinkLabel link && link.Tag is int fromTag)
                segmentIndex = fromTag;
            else
                return;

            SegmentClicked?.Invoke(segmentIndex);
        }

        private sealed class MouseWheelMessageFilter : IMessageFilter
        {
            private const int WM_MOUSEWHEEL = 0x020A;
            private readonly CompositePathBreadcrumb _owner;

            public MouseWheelMessageFilter(CompositePathBreadcrumb owner)
            {
                _owner = owner;
            }

            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg != WM_MOUSEWHEEL || !_owner.IsMouseOverBreadcrumb())
                    return false;

                int delta = (short)((m.WParam.ToInt64() >> 16) & 0xffff);
                _owner.ScrollBy(-delta);
                return true;
            }
        }
    }
}

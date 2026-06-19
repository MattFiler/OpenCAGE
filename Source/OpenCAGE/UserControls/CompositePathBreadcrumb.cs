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

        private readonly Panel _scrollHost;
        private readonly FlowLayoutPanel _flow;

        public CompositePathBreadcrumb()
        {
            BackColor = SystemColors.Window;
            BorderStyle = BorderStyle.FixedSingle;

            _scrollHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(2, 0, 2, 0),
            };

            _flow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = Padding.Empty,
                Padding = new Padding(2, 1, 2, 1),
                BackColor = SystemColors.Window,
            };

            _scrollHost.Controls.Add(_flow);
            Controls.Add(_scrollHost);
        }

        public void SetPath(CompositePath path, Composite currentComposite)
        {
            _flow.SuspendLayout();
            _flow.Controls.Clear();

            if (path == null || currentComposite == null)
            {
                _flow.ResumeLayout(true);
                return;
            }

            bool showShortGuids = SettingsManager.GetBool(Settings.ShowShortGuids);
            var segments = path.GetPathRich(currentComposite);

            for (int i = 0; i < segments.Count; i++)
            {
                if (i > 0)
                    _flow.Controls.Add(CreateSeparator());

                bool isCurrent = i == segments.Count - 1;
                string label = FormatCompositeLabel(segments[i].Composite, showShortGuids);

                if (isCurrent)
                    _flow.Controls.Add(CreateCurrentSegmentLabel(label));
                else
                    _flow.Controls.Add(CreateLinkSegment(i, label));
            }

            _flow.ResumeLayout(true);

            if (_flow.Controls.Count > 0)
                _scrollHost.ScrollControlIntoView(_flow.Controls[_flow.Controls.Count - 1]);
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
            };
            link.LinkClicked += OnSegmentLinkClicked;
            return link;
        }

        private void OnSegmentLinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (sender is LinkLabel link && link.Tag is int segmentIndex)
                SegmentClicked?.Invoke(segmentIndex);
        }
    }
}

using System;
using System.Windows.Controls;
using System.Windows.Input;

namespace CommandsEditor
{
    public partial class Track : UserControl
    {
        public int TrackIndex { get { return _trackIndex; } }
        int _trackIndex;

        public string TrackName { get { return _trackName; } }
        string _trackName = "";

        public Action<Track> OnNewKeyframe;

        public Track(double width, string name, int track)
        {
            InitializeComponent();
            _trackIndex = track;
            _trackName = name;

            rectOuter.Width = width;
            tracknameui.Text = name;
            addkeyframebutton.Click += Addkeyframebutton_Click;
        }

        private void Addkeyframebutton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            OnNewKeyframe?.Invoke(this);
        }
    }
}

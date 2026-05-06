using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CommandsEditor
{
    public partial class Timeline : UserControl
    {
        List<TimeMarker> _timeMarkers = new List<TimeMarker>();
        List<Keyframe> _keyframes = new List<Keyframe>();
        List<Track> _tracks = new List<Track>();
        Border _border;

        public Action<Keyframe> OnNewKeyframe;

        int width;
        int height;
        int elementTop;
        float startSeconds;
        float endSeconds;
        internal int pixelDistance;

        public Timeline(int w, int h)
        {
            InitializeComponent();

            width = w;
            height = h;
            border.Width = w;
            border.Height = h;
        }

        public Keyframe AddKeyframe(float seconds, string trackName)
        {
            int trackIndex = -1;
            for (int i = 0; i < _tracks.Count; i++)
            {
                if (_tracks[i].TrackName == trackName)
                {
                    trackIndex = i;
                    break;
                }
            }
            Track track;
            if (trackIndex == -1)
            {
                track = new Track(_border.Width, trackName, _tracks.Count);
                track.OnNewKeyframe += OnTrackAddedKeyframe;
                trackIndex = _tracks.Count;
                _tracks.Add(track);
                mainCanvas.Children.Add(track);
            }
            else
            {
                track = _tracks[trackIndex];
            }
            trackIndex += 1;

            double trackOffset = elementTop + (trackIndex * 40.0f) - 15.0f;
            Canvas.SetTop(track, trackOffset + 5);
            Canvas.SetLeft(track, 0);
            Canvas.SetZIndex(track, 5);

            if (trackIndex == _tracks.Count)
            {
                _border.Height = trackOffset + 12.0f;
                mainCanvas.Height = _border.Height + 30;
            }

            Keyframe key = new Keyframe(this, seconds, track);
            _keyframes.Add(key);
            mainCanvas.Children.Add(key);
            Canvas.SetTop(key, trackOffset);
            Canvas.SetLeft(key, (pixelDistance * (seconds - startSeconds) / (endSeconds - startSeconds)) - 2);
            Canvas.SetZIndex(track, 10);

            return key;
        }

        public void RemoveKeyframe(Keyframe key)
        {
            _keyframes.Remove(key);
            mainCanvas.Children.Remove(key);
            key.Visibility = Visibility.Hidden;
        }

        private void OnTrackAddedKeyframe(Track track)
        {
            Keyframe key = AddKeyframe(endSeconds, track.TrackName);
            OnNewKeyframe?.Invoke(key);
        }

        public void RefreshElement(Keyframe key)
        {
            key.SetSeconds((float)((Canvas.GetLeft(key) + 2) * (endSeconds - startSeconds) / pixelDistance) + startSeconds);
        }

        public void Setup(float startSeconds, float endSeconds, float intervalSeconds, int spacing)
        {
            this. startSeconds = startSeconds;
            this. endSeconds = endSeconds;

            float boxHeight = height - 46; // To account for TimelineMark height & scrollbar height. This value assumes the height of the Aero-style scrollbar.

            // Create first mark
            TimeMarker tmStart = new TimeMarker(startSeconds, boxHeight);
            _timeMarkers.Add(tmStart);
            mainCanvas.Children.Add(tmStart);

            // Create middle marks
            int intervalCount = (int)(((endSeconds - startSeconds) / intervalSeconds) - 1);
            for (int i = 1; i <= intervalCount; i++)
            {
                TimeMarker tm = new TimeMarker(startSeconds + (intervalSeconds * i), boxHeight);
                _timeMarkers.Add(tm);
                mainCanvas.Children.Add(tm);
            }

            // Create last mark
            TimeMarker tmEnd = new TimeMarker(endSeconds, boxHeight);
            _timeMarkers.Add(tmEnd);
            mainCanvas.Children.Add(tmEnd);

            // Setup spacing
            for (int k = 0; k < _timeMarkers.Count; k++)
            {
                Canvas.SetLeft(_timeMarkers[k], spacing * k);
                Canvas.SetTop(_timeMarkers[k], 1);
                Canvas.SetZIndex(_timeMarkers[k], 1);
            }

            // Size & place the controls
            Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Arrange(new Rect(0, 0, width, height));

            _border = new Border();
            _border.BorderThickness = new Thickness(1);
            _border.BorderBrush = new SolidColorBrush(Color.FromRgb(12, 12, 12));
            _border.Background = new SolidColorBrush(Color.FromRgb(248, 248, 248));
            mainCanvas.Children.Add(_border);
            Canvas.SetTop(_border, 1 + tmStart.ActualHeight - boxHeight);
            Canvas.SetZIndex(_border, 0);
            elementTop = 1 + (int)tmStart.ActualHeight - (int)boxHeight + 1; // Canvas.Top value for TimelineElements
            _border.Width = 1 + Canvas.GetLeft(tmEnd);
            _border.Height = boxHeight; 

            pixelDistance = (int)_border.Width - 1; // Region of the border aka the timeline's length in pixels
            mainCanvas.Width = (spacing * (_timeMarkers.Count - 1)) + (int)tmEnd.ActualWidth; // Set the canvas's width so the ScrollViewer knows how big it is
            mainCanvas.Height = boxHeight + 40;
        }
    }
}

using OpenCAGE.Audio;
using OpenCAGE.Theming;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE.UserControls
{
    /// <summary>
    /// Plays back the audio behind a sound event.
    ///
    /// An event rarely maps to a single file - most of the interesting ones sit behind a random or
    /// switch container and pick between several takes at runtime - so the panel exposes every variation
    /// it resolved and lets you step or shuffle through them, which is the only way to hear what the
    /// event will actually sound like in the game.
    /// </summary>
    public class SoundPreviewPanel : UserControl
    {
        public const int PreferredHeight = 58;

        private readonly Button _play = new Button();
        private readonly Button _stop = new Button();
        private readonly SoundSeekBar _seek = new SoundSeekBar();
        private readonly Label _time = new Label();
        private readonly Label _status = new Label();
        private readonly ComboBox _variations = new ComboBox();
        private readonly Button _shuffle = new Button();
        private readonly Button _export = new Button();
        private readonly Button _replace = new Button();
        private readonly CheckBox _autoPlay = new CheckBox();
        private readonly Timer _tick = new Timer();

        private readonly Random _random = new Random();

        private string _eventName;
        private List<WwiseSoundVariation> _resolved = new List<WwiseSoundVariation>();
        private WavePlayer _player;
        private DecodedAudio _audio;
        private int _generation;
        private bool _updatingVariations;

        public SoundPreviewPanel()
        {
            Height = PreferredHeight;

            _play.Text = "Play";
            _play.Width = 62;
            _play.FlatStyle = FlatStyle.System;
            _play.Click += (s, e) => TogglePlay();

            _stop.Text = "Stop";
            _stop.Width = 52;
            _stop.FlatStyle = FlatStyle.System;
            _stop.Click += (s, e) => StopPlayback();

            _seek.Seeked += OnSeeked;

            _time.Text = "";
            _time.TextAlign = ContentAlignment.MiddleRight;
            _time.AutoSize = false;
            _time.Width = 84;

            _status.AutoSize = false;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.AutoEllipsis = true;

            _variations.DropDownStyle = ComboBoxStyle.DropDownList;
            _variations.SelectedIndexChanged += OnVariationChanged;

            _shuffle.Text = "Shuffle";
            _shuffle.Width = 62;
            _shuffle.FlatStyle = FlatStyle.System;
            _shuffle.Click += (s, e) => Shuffle();

            _export.Text = "Export";
            _export.Width = 62;
            _export.FlatStyle = FlatStyle.System;
            _export.Click += (s, e) => Export();

            _replace.Text = "Replace";
            _replace.Width = 68;
            _replace.FlatStyle = FlatStyle.System;
            _replace.Click += (s, e) => Replace();

            _autoPlay.Text = "Auto-play";
            _autoPlay.Width = 72;
            _autoPlay.Checked = SettingsManager.GetBool(Settings.SoundPreviewAutoPlay);
            _autoPlay.CheckedChanged += (s, e) => SettingsManager.SetBool(Settings.SoundPreviewAutoPlay, _autoPlay.Checked);

            Controls.AddRange(new Control[] { _play, _stop, _seek, _time, _status, _variations, _shuffle, _export, _replace, _autoPlay });

            _tick.Interval = 50;
            _tick.Tick += (s, e) => UpdateProgress();
            _tick.Start();

            SetIdle("");
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            //The window themes itself in its base constructor, which runs before this panel exists, so
            //the panel has to ask for the same treatment once it has been added
            ThemeEngine.Apply(this, ThemeManager.IsDark);
            ApplyTheme();
        }

        /// <summary>Point the panel at a sound event, or at nothing.</summary>
        public void SetEvent(string eventName)
        {
            if (_eventName == eventName)
                return;

            _eventName = eventName;
            LoadResolved();
        }

        #region LOADING

        private void LoadResolved()
        {
            int generation = ++_generation;
            ReleasePlayer();
            _resolved = new List<WwiseSoundVariation>();
            UpdateVariationList();

            if (string.IsNullOrEmpty(_eventName))
            {
                SetIdle("Select a sound event to preview it.");
                return;
            }

            if (!SoundPreviewLibrary.IsAvailable)
            {
                SetIdle("The game's sound folder could not be found.");
                return;
            }

            SetIdle(SoundPreviewLibrary.IsReady ? "Loading..." : "Indexing the game's soundbanks...");

            string wanted = _eventName;
            Task.Factory.StartNew(() =>
            {
                try
                {
                    WwiseSoundLibrary library = SoundPreviewLibrary.Get();
                    WwiseEventResolution resolved = library.Resolve(wanted);
                    Report(generation, () => OnResolved(resolved));
                }
                catch (Exception e)
                {
                    Report(generation, () => SetIdle(e.Message));
                }
            });
        }

        /// <summary>
        /// Hop back to the UI thread, unless the selection has already moved on.
        ///
        /// A result that arrives too late still has to be tidied up - a decode that lost the race owns
        /// a running thread and a buffer of samples - so <paramref name="discarded"/> is called on every
        /// path where the result is thrown away.
        /// </summary>
        private bool Report(int generation, Action action, Action discarded = null)
        {
            if (IsDisposed || !IsHandleCreated || generation != _generation)
            {
                if (discarded != null)
                    discarded();

                return false;
            }

            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (!IsDisposed && generation == _generation)
                        action();
                    else if (discarded != null)
                        discarded();
                });

                return true;
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            if (discarded != null)
                discarded();

            return false;
        }

        private void OnResolved(WwiseEventResolution resolved)
        {
            _resolved = resolved.Variations;
            UpdateVariationList();

            if (_resolved.Count == 0)
            {
                //Around a quarter of the game's events genuinely play nothing, for several different
                //reasons, so say which one rather than reporting them all as a failure
                SetIdle(resolved.Explanation);
                return;
            }

            LoadVariation(0);
        }

        private void LoadVariation(int index)
        {
            if (index < 0 || index >= _resolved.Count)
                return;

            int generation = ++_generation;
            ReleasePlayer();

            _updatingVariations = true;
            _variations.SelectedIndex = index;
            _updatingVariations = false;

            WwiseSoundVariation variation = _resolved[index];
            SetIdle("Decoding...");

            Task.Factory.StartNew(() =>
            {
                DecodedAudio audio = null;
                try
                {
                    byte[] wem = WwiseSoundLibrary.ReadMedia(variation.Media);
                    audio = WwiseAudioDecoder.Decode(wem);

                    DecodedAudio decoded = audio;
                    Report(generation, () => OnDecoded(variation, decoded), () => decoded.Dispose());
                }
                catch (Exception e)
                {
                    if (audio != null)
                        audio.Dispose();

                    Report(generation, () => SetIdle(e.Message));
                }
            });
        }

        private void OnDecoded(WwiseSoundVariation variation, DecodedAudio audio)
        {
            _audio = audio;

            try
            {
                _player = new WavePlayer(audio);
                _player.PlaybackEnded += OnPlaybackEnded;
            }
            catch (Exception e)
            {
                SetIdle(e.Message);
                return;
            }

            _seek.Enabled = true;
            _seek.Maximum = audio.Duration.TotalSeconds;
            _seek.Value = 0;
            _play.Enabled = true;
            _stop.Enabled = true;
            _export.Enabled = true;
            _play.Text = "Play";

            _status.Text = Describe(_eventName, variation, audio);
            UpdateProgress();

            if (_autoPlay.Checked)
                _player.Play();
        }

        private static string Describe(string eventName, WwiseSoundVariation variation, DecodedAudio audio)
        {
            List<string> parts = new List<string>();

            //Which soundbank the event belongs to comes from the level's own data, not from whichever
            //bank the object happened to be read out of - the same event is repeated across many of them
            List<string> banks = SoundEventMetadata.BanksFor(eventName);
            if (banks.Count == 1)
                parts.Add(banks[0]);
            else if (banks.Count > 1)
                parts.Add(banks[0] + " +" + (banks.Count - 1));

            if (!string.IsNullOrEmpty(variation.Path))
                parts.Add(variation.Path);

            string channels;
            switch (audio.SourceChannels)
            {
                case 1: channels = "mono"; break;
                case 2: channels = "stereo"; break;
                case 6: channels = "5.1 to stereo"; break;
                default: channels = audio.SourceChannels + "ch to stereo"; break;
            }

            parts.Add(audio.SampleRate + "Hz " + channels);
            parts.Add(variation.SourceId + " in " + variation.Media.Origin);

            if (audio.Truncated)
                parts.Add("first " + (DecodedAudio.MaxPreviewSeconds / 60) + " minutes only");

            return string.Join("  |  ", parts.ToArray());
        }

        #endregion

        #region PLAYBACK

        private void TogglePlay()
        {
            if (_player == null)
                return;

            if (_player.IsPlaying)
            {
                _player.Pause();
                _play.Text = "Play";
            }
            else
            {
                _player.Play();
                _play.Text = "Pause";
            }
        }

        private void StopPlayback()
        {
            if (_player == null)
                return;

            _player.Stop();
            _play.Text = "Play";
            UpdateProgress();
        }

        private void OnPlaybackEnded(object sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (!IsDisposed)
                        _play.Text = "Play";
                });
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void OnSeeked(object sender, EventArgs e)
        {
            if (_player == null)
                return;

            _player.Seek(TimeSpan.FromSeconds(_seek.Value));
            UpdateProgress();
        }

        private void UpdateProgress()
        {
            if (_player == null || _audio == null)
                return;

            //The total settles once decoding finishes, and the buffered mark trails the decoder
            _seek.Maximum = _audio.Duration.TotalSeconds;
            _seek.Buffered = _audio.Complete ? _audio.Duration.TotalSeconds : _audio.Decoded.TotalSeconds;

            if (!_seek.Dragging)
                _seek.Value = _player.Position.TotalSeconds;

            _time.Text = Format(_player.Position) + " / " + Format(_audio.Duration);

            string wanted = _player.IsPlaying ? "Pause" : "Play";
            if (_play.Text != wanted)
                _play.Text = wanted;
        }

        private static string Format(TimeSpan time)
        {
            if (time.TotalSeconds < 0)
                time = TimeSpan.Zero;

            return ((int)time.TotalMinutes) + ":" + time.Seconds.ToString("00") + "." + (time.Milliseconds / 100);
        }

        private void ReleasePlayer()
        {
            if (_player != null)
            {
                _player.PlaybackEnded -= OnPlaybackEnded;
                _player.Dispose();
                _player = null;
            }

            if (_audio != null)
            {
                //Stops the decode thread, which may still be running ahead of playback
                _audio.Dispose();
                _audio = null;
            }

            _seek.Value = 0;
            _seek.Maximum = 0;
            _time.Text = "";
        }

        #endregion

        #region VARIATIONS

        private void UpdateVariationList()
        {
            _updatingVariations = true;
            _variations.Items.Clear();

            for (int i = 0; i < _resolved.Count; i++)
            {
                string label = "Variation " + (i + 1) + " of " + _resolved.Count;
                if (!string.IsNullOrEmpty(_resolved[i].Path))
                    label += "  (" + _resolved[i].Path + ")";

                _variations.Items.Add(label);
            }

            bool many = _resolved.Count > 1;
            _variations.Visible = many;
            _shuffle.Visible = many;
            _variations.Enabled = many;

            //Replacing works on the variation that is selected, so it needs one to exist
            _replace.Visible = _resolved.Count > 0;

            if (_resolved.Count > 0)
                _variations.SelectedIndex = 0;

            _updatingVariations = false;
            PerformLayout();
        }

        private void OnVariationChanged(object sender, EventArgs e)
        {
            if (_updatingVariations)
                return;

            LoadVariation(_variations.SelectedIndex);
        }

        private void Shuffle()
        {
            if (_resolved.Count < 2)
                return;

            //Never land on the take that is already loaded, or shuffling looks broken
            int next = _variations.SelectedIndex;
            while (next == _variations.SelectedIndex)
                next = _random.Next(_resolved.Count);

            LoadVariation(next);
        }

        #endregion

        private void Export()
        {
            if (_audio == null)
                return;

            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "Wave File|*.wav";
                dialog.FileName = (_eventName ?? "sound") + ".wav";

                if (dialog.ShowDialog() != DialogResult.OK)
                    return;

                //Writing waits for the decode to finish, which for a long sound is a few seconds
                DecodedAudio audio = _audio;
                string path = dialog.FileName;
                _export.Enabled = false;

                Task.Factory.StartNew(() =>
                {
                    string failure = null;
                    try
                    {
                        File.WriteAllBytes(path, audio.ToWave());
                    }
                    catch (Exception e)
                    {
                        failure = e.Message;
                    }

                    Report(_generation, () =>
                    {
                        _export.Enabled = _audio != null;
                        if (failure != null)
                            MessageBox.Show("Could not save the audio:\n\n" + failure, "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    });
                });
            }
        }

        /// <summary>
        /// Swap the audio behind the selected variation for a .wav.
        ///
        /// Replacing one take of a random container replaces that take alone, which is why this sits
        /// beside the variation list rather than acting on the event as a whole. Nothing is written until
        /// the summary has been read and accepted, because the file being edited is the game's own.
        /// </summary>
        private void Replace()
        {
            int index = _variations.SelectedIndex;
            if (index < 0 || index >= _resolved.Count)
                return;

            WwiseSoundVariation variation = _resolved[index];
            if (variation.Media == null)
            {
                MessageBox.Show("This sound has no audio of its own to replace.", "Replace",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Wave File|*.wav";
                dialog.Title = "Replace " + variation.SourceId + " with";
                if (dialog.ShowDialog() != DialogResult.OK)
                    return;

                SoundImport.Reading reading;
                Cursor.Current = Cursors.WaitCursor;
                try
                {
                    //The same audio is often shipped in several banks at once, and all of them have to
                    //change or the old sound comes back in whichever level carries a different copy
                    IList<WwiseMediaLocation> copies = SoundPreviewLibrary.Get().AllCopies(variation.SourceId);
                    reading = SoundImport.Read(dialog.FileName, variation.Media, copies, null);
                }
                finally { Cursor.Current = Cursors.Default; }

                if (!reading.Ok)
                {
                    MessageBox.Show("That audio can't be imported:\n\n" + reading.Problem, "Replace",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (MessageBox.Show(SoundImport.Describe(reading) + "\n\nReplace the sound?", "Replace",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                StopPlayback();
                ReleasePlayer();

                string problem;
                Cursor.Current = Cursors.WaitCursor;
                try
                {
                    if (!SoundImport.Apply(reading, variation.Media, out problem))
                    {
                        MessageBox.Show("The sound could not be replaced:\n\n" + problem, "Replace",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
                finally { Cursor.Current = Cursors.Default; }
            }

            //Play the new audio straight back, which is the only way to see that it worked
            LoadVariation(index);
        }

        private void SetIdle(string status)
        {
            _status.Text = status;
            _play.Enabled = false;
            _stop.Enabled = false;
            _export.Enabled = false;
            _seek.Enabled = false;
            _play.Text = "Play";
            _time.Text = "";
        }

        private void ApplyTheme()
        {
            if (!ThemeManager.IsDark)
                return;

            BackColor = ThemeColours.Surface;
            _status.ForeColor = ThemeColours.TextDim;
            _time.ForeColor = ThemeColours.TextDim;
            _autoPlay.ForeColor = ThemeColours.Text;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);

            const int gap = 4;
            int rowHeight = 24;

            //Transport on the top row, everything about which sound is playing on the bottom
            int x = 0;
            _play.SetBounds(x, 0, _play.Width, rowHeight);
            x += _play.Width + gap;
            _stop.SetBounds(x, 0, _stop.Width, rowHeight);
            x += _stop.Width + gap;

            int right = ClientSize.Width;
            _time.SetBounds(right - _time.Width, 0, _time.Width, rowHeight);

            int seekWidth = right - _time.Width - gap - x;
            _seek.SetBounds(x, 0, Math.Max(20, seekWidth), rowHeight);

            int y = rowHeight + gap;
            int cursor = right;

            _export.SetBounds(cursor - _export.Width, y, _export.Width, rowHeight);
            cursor -= _export.Width + gap;

            if (_replace.Visible)
            {
                _replace.SetBounds(cursor - _replace.Width, y, _replace.Width, rowHeight);
                cursor -= _replace.Width + gap;
            }

            if (_shuffle.Visible)
            {
                _shuffle.SetBounds(cursor - _shuffle.Width, y, _shuffle.Width, rowHeight);
                cursor -= _shuffle.Width + gap;
            }

            if (_variations.Visible)
            {
                int width = Math.Min(240, Math.Max(120, cursor / 2));
                _variations.SetBounds(cursor - width, y + 2, width, rowHeight - 4);
                cursor -= width + gap;
            }

            _autoPlay.SetBounds(cursor - _autoPlay.Width, y + 3, _autoPlay.Width, rowHeight - 4);
            cursor -= _autoPlay.Width + gap;

            _status.SetBounds(0, y, Math.Max(20, cursor), rowHeight);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _generation++;
                _tick.Stop();
                _tick.Dispose();
                ReleasePlayer();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// A scrubber. A TrackBar can't be drawn dark and quantises to whole units, neither of which
        /// suits a bar that has to represent a couple of seconds of audio to a tenth.
        /// </summary>
        private class SoundSeekBar : Control
        {
            private double _value;
            private double _maximum;
            private double _buffered;
            private bool _dragging;

            public event EventHandler Seeked;

            public SoundSeekBar()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                Cursor = Cursors.Hand;
            }

            public bool Dragging
            {
                get { return _dragging; }
            }

            public double Maximum
            {
                get { return _maximum; }
                set
                {
                    if (Math.Abs(value - _maximum) < 0.001)
                        return;

                    _maximum = value;
                    Invalidate();
                }
            }

            /// <summary>How far the decoder has got, which is as far as the bar can be dragged.</summary>
            public double Buffered
            {
                get { return _buffered; }
                set
                {
                    if (Math.Abs(value - _buffered) < 0.05)
                        return;

                    _buffered = value;
                    Invalidate();
                }
            }

            public double Value
            {
                get { return _value; }
                set
                {
                    double clamped = value < 0 ? 0 : (value > _maximum ? _maximum : value);
                    if (Math.Abs(clamped - _value) < 0.001)
                        return;

                    _value = clamped;
                    Invalidate();
                }
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (!Enabled || e.Button != MouseButtons.Left)
                    return;

                _dragging = true;
                ValueFrom(e.X);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (_dragging)
                    ValueFrom(e.X);
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                if (!_dragging)
                    return;

                _dragging = false;
                ValueFrom(e.X);

                EventHandler handler = Seeked;
                if (handler != null)
                    handler(this, EventArgs.Empty);
            }

            private void ValueFrom(int x)
            {
                if (_maximum <= 0 || Width <= 1)
                    return;

                double fraction = (double)x / (Width - 1);
                Value = fraction * _maximum;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                bool dark = ThemeManager.IsDark;
                Color background = dark ? ThemeColours.Surface : SystemColors.Control;
                Color trough = Enabled
                    ? (dark ? ThemeColours.Input : SystemColors.ControlLight)
                    : (dark ? ThemeColours.InputDisabled : SystemColors.ControlLight);
                Color fill = Enabled
                    ? (dark ? ThemeColours.Accent : SystemColors.Highlight)
                    : (dark ? ThemeColours.Border : SystemColors.ControlDark);
                Color border = dark ? ThemeColours.Border : SystemColors.ControlDark;

                e.Graphics.Clear(background);

                int height = 6;
                Rectangle bar = new Rectangle(0, (Height - height) / 2, Width, height);

                using (SolidBrush brush = new SolidBrush(trough))
                    e.Graphics.FillRectangle(brush, bar);

                if (_maximum > 0 && _buffered < _maximum)
                {
                    //A quiet marker for how much has decoded, so a long sound doesn't look stuck
                    int buffered = (int)(bar.Width * (_buffered / _maximum));
                    if (buffered > 0)
                    {
                        using (SolidBrush brush = new SolidBrush(dark ? ThemeColours.BorderStrong : SystemColors.ControlDark))
                            e.Graphics.FillRectangle(brush, new Rectangle(bar.X, bar.Y, Math.Min(buffered, bar.Width), bar.Height));
                    }
                }

                if (_maximum > 0)
                {
                    int width = (int)(bar.Width * (_value / _maximum));
                    if (width > 0)
                    {
                        using (SolidBrush brush = new SolidBrush(fill))
                            e.Graphics.FillRectangle(brush, new Rectangle(bar.X, bar.Y, Math.Min(width, bar.Width), bar.Height));
                    }

                    if (Enabled)
                    {
                        //A handle, so it reads as something you can grab rather than a progress bar
                        int x = Math.Min(bar.Right - 3, Math.Max(bar.X + 3, bar.X + width));
                        Rectangle handle = new Rectangle(x - 3, bar.Y - 4, 6, bar.Height + 8);

                        using (SolidBrush brush = new SolidBrush(fill))
                            e.Graphics.FillRectangle(brush, handle);
                        using (Pen pen = new Pen(border))
                            e.Graphics.DrawRectangle(pen, handle);
                    }
                }

                using (Pen pen = new Pen(border))
                    e.Graphics.DrawRectangle(pen, bar.X, bar.Y, bar.Width - 1, bar.Height - 1);
            }
        }
    }
}

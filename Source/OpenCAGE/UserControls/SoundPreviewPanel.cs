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

        //Replace asks where the new audio goes: the open level's own sound package, or the game's shared files
        private readonly ContextMenuStrip _replaceMenu = new ContextMenuStrip();
        private readonly ToolStripMenuItem _replaceForLevel = new ToolStripMenuItem("For This Level Only...");
        private readonly ToolStripMenuItem _replaceEverywhere = new ToolStripMenuItem("For Every Level...");
        private readonly ToolStripMenuItem _revertForLevel = new ToolStripMenuItem("Use the Base Game's Audio on This Level");

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
            _replace.Click += (s, e) => ShowReplaceMenu();

            _replaceForLevel.ToolTipText = "Put the new audio in this level's own sound package (WORLD\\" + LevelSoundOverride.FileName + "). Every other level keeps the original.";
            _replaceForLevel.Click += (s, e) => Replace(true);
            _replaceEverywhere.ToolTipText = "Write the new audio into the game's shared sound files, for every level that plays this sound.";
            _replaceEverywhere.Click += (s, e) => Replace(false);
            _revertForLevel.ToolTipText = "Take this sound out of this level's sound package, so the level plays the base game's audio for it again.";
            _revertForLevel.Click += (s, e) => RevertForLevel();
            _replaceMenu.Items.AddRange(new ToolStripItem[] { _replaceForLevel, _replaceEverywhere, new ToolStripSeparator(), _revertForLevel });
            _replaceMenu.ShowItemToolTips = true;

            LevelSoundOverride.Changed += OnLevelOverrideChanged;

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

        /// <summary>
        /// Resolve the event again - after its audio has moved in or out of the level's sound package, where it is read
        /// from has changed - and go back to the variation that was showing.
        /// </summary>
        private void OnLevelOverrideChanged()
        {
            if (IsDisposed || string.IsNullOrEmpty(_eventName))
                return;

            LoadResolved(Math.Max(0, _variations.SelectedIndex));
        }

        private void LoadResolved(int variation = 0)
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
                    Report(generation, () => OnResolved(resolved, variation));
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

        private void OnResolved(WwiseEventResolution resolved, int variation)
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

            LoadVariation(variation < _resolved.Count ? variation : 0);
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
                    //Read through the index as it is now: a Replace (here, through the MCP, or by a mod) can
                    //rewrite a whole soundbank and move the rest of its audio after this list was resolved
                    WwiseMediaLocation media = SoundPreviewLibrary.Get().MediaFor(variation.SourceId) ?? variation.Media;
                    byte[] wem = WwiseSoundLibrary.ReadMedia(media);
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

        private void ShowReplaceMenu()
        {
            int index = _variations.SelectedIndex;
            WwiseSoundVariation variation = index >= 0 && index < _resolved.Count ? _resolved[index] : null;
            string package = LevelSoundOverride.PathForOpenLevel();

            //Audio in a soundbank the game keeps loaded from start-up is in memory before any level's package is read
            string permanent = null;
            if (variation != null && package != null && SoundPreviewLibrary.IsReady)
            {
                try { permanent = LevelSoundOverride.PermanentBankHolding(SoundPreviewLibrary.Get(), variation.SourceId); }
                catch { }
            }

            _replaceForLevel.Enabled = package != null && permanent == null;
            _replaceForLevel.Text = package == null ? "For This Level Only... (open a level first)"
                : permanent != null ? "For This Level Only... (not possible: in " + permanent + ")"
                : "For This Level Only...";
            _replaceForLevel.ToolTipText = permanent != null
                ? permanent + " is a permanent soundbank: the game loads it as it starts, before any level's sound package is read, and plays its copy of this sound on every level. Replace it for every level instead."
                : "Put the new audio in this level's own sound package (WORLD\\" + LevelSoundOverride.FileName + "). Every other level keeps the original.";
            _revertForLevel.Enabled = variation != null && package != null && InLevelPackage(variation, package);

            _replaceMenu.Show(_replace, new Point(0, _replace.Height));
        }

        /// <summary>Whether the level's own sound package holds a copy of this variation's audio.</summary>
        private static bool InLevelPackage(WwiseSoundVariation variation, string package)
        {
            if (!File.Exists(package))
                return false;

            //Without the index to hand, offered anyway: going back finds out for itself, and asking would stall the menu
            if (!SoundPreviewLibrary.IsReady)
                return true;

            try
            {
                return SoundPreviewLibrary.Get().AllCopies(variation.SourceId).Any(o => string.Equals(Path.GetFullPath(o.File), package, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// Swap the audio behind the selected variation for a .wav - in the game's shared sound files, or for the open
        /// level alone, in its own sound package.
        ///
        /// Replacing one take of a random container replaces that take alone, which is why this sits
        /// beside the variation list rather than acting on the event as a whole. Nothing is written until
        /// the summary has been read and accepted, because the file being edited is the game's own.
        /// </summary>
        private void Replace(bool forLevel)
        {
            int index = _variations.SelectedIndex;
            if (index < 0 || index >= _resolved.Count)
                return;

            string title = forLevel ? "Replace for This Level" : "Replace";
            WwiseSoundVariation variation = _resolved[index];
            if (variation.Media == null)
            {
                MessageBox.Show("This sound has no audio of its own to replace.", title,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string package = forLevel ? LevelSoundOverride.PathForOpenLevel() : null;
            if (forLevel && package == null)
                return;

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Wave File|*.wav";
                dialog.Title = "Replace " + variation.SourceId + " with";
                if (dialog.ShowDialog() != DialogResult.OK)
                    return;

                SoundImport.Reading reading;
                WwiseMediaLocation target;
                Cursor.Current = Cursors.WaitCursor;
                try
                {
                    //Where the audio is by the index as it is now, since the list may predate a write that
                    //moved it. The same audio is often shipped in several banks at once, and all of them have
                    //to change or the old sound comes back in whichever level carries a different copy
                    WwiseSoundLibrary library = SoundPreviewLibrary.Get();
                    target = library.MediaFor(variation.SourceId) ?? variation.Media;
                    IList<WwiseMediaLocation> copies = library.AllCopies(variation.SourceId);
                    reading = forLevel
                        ? SoundImport.ReadForLevel(dialog.FileName, variation.SourceId, target, library, package, null)
                        : SoundImport.Read(dialog.FileName, target, copies, null);
                }
                finally { Cursor.Current = Cursors.Default; }

                if (!reading.Ok)
                {
                    MessageBox.Show("That audio can't be imported:\n\n" + reading.Problem, title,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (MessageBox.Show(SoundImport.Describe(reading) + (forLevel ? "\n\nReplace the sound on this level?" : "\n\nReplace the sound?"), title,
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                StopPlayback();
                ReleasePlayer();

                if (forLevel)
                {
                    LevelSoundOverride.Plan written;
                    Cursor.Current = Cursors.WaitCursor;
                    try
                    {
                        written = LevelSoundOverride.Replace(package, SoundPreviewLibrary.Get(), new Dictionary<uint, byte[]> { { variation.SourceId, reading.Wem } }, true);
                    }
                    finally { Cursor.Current = Cursors.Default; }

                    if (!written.Ok)
                        MessageBox.Show("The sound could not be replaced:\n\n" + written.Problem, title,
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);

                    //Resolves the event again, now its audio is read from the package, and plays the new audio straight back
                    LevelSoundOverride.RaiseChanged();
                    return;
                }

                //Pristine copies of every container this write touches, while they're still pristine
                Modding.ModServices.CaptureBeforeWrite(target.File);
                foreach (WwiseMediaLocation copy in SoundPreviewLibrary.Get().AllCopies(variation.SourceId))
                    Modding.ModServices.CaptureBeforeWrite(copy.File);

                string problem;
                bool replaced;
                Cursor.Current = Cursors.WaitCursor;
                try
                {
                    replaced = SoundImport.Apply(reading, target, out problem);
                }
                finally
                {
                    //Writing into a soundbank lays all of its audio out again, so the other sounds in it move
                    //too. The index is pointed at where they are now - even after a write that failed part
                    //way - or they read the wrong bytes, and a second Replace can't find them
                    SoundPreviewLibrary.Refresh(reading.Copies.Select(c => c.File).Concat(new[] { target.File }));
                    Cursor.Current = Cursors.Default;
                }

                if (!replaced)
                    MessageBox.Show("The sound could not be replaced:\n\n" + problem, "Replace",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            //Play the new audio straight back, which is the only way to see that it worked (or, after a
            //failure, what the file holds now)
            LoadVariation(index);
        }

        /// <summary>
        /// Take the selected variation's audio back out of the level's own sound package, so the level plays what the
        /// base game ships for it. A bank copied into the package for it goes too, once it matches the base game's again.
        /// </summary>
        private void RevertForLevel()
        {
            const string title = "Use the Base Game's Audio";
            int index = _variations.SelectedIndex;
            string package = LevelSoundOverride.PathForOpenLevel();
            if (index < 0 || index >= _resolved.Count || package == null)
                return;

            WwiseSoundVariation variation = _resolved[index];
            LevelSoundOverride.Plan plan;
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                plan = LevelSoundOverride.Revert(package, SoundPreviewLibrary.Get(), new[] { variation.SourceId }, false);
            }
            finally { Cursor.Current = Cursors.Default; }

            if (!plan.Ok)
            {
                MessageBox.Show(plan.Problem, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(string.Join("\n\n", plan.Notes) + "\n\nPlay the base game's audio for this sound on this level?", title,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            StopPlayback();
            ReleasePlayer();

            Cursor.Current = Cursors.WaitCursor;
            try
            {
                plan = LevelSoundOverride.Revert(package, SoundPreviewLibrary.Get(), new[] { variation.SourceId }, true);
            }
            finally { Cursor.Current = Cursors.Default; }

            if (!plan.Ok)
                MessageBox.Show("The level's sound package could not be changed:\n\n" + plan.Problem, title,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);

            LevelSoundOverride.RaiseChanged();
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
                LevelSoundOverride.Changed -= OnLevelOverrideChanged;
                _tick.Stop();
                _tick.Dispose();
                _replaceMenu.Dispose();
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

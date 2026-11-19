using OpenCAGE.Audio;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Tune a sound event (volume, pitch, delay, fades, looping, range) for the open level alone, written into the level's own copies
    /// of the soundbanks that carry it, never into DATA/SOUND. Only changed settings are written; Put Back restores the base game's.
    /// See <see cref="LevelSoundOverride.Tune"/>.
    /// </summary>
    public partial class LevelSoundTuneDialog : BaseWindow
    {
        private readonly string _package;

        private LevelSoundOverride.Tuning _read;
        private bool _filling;
        private int _generation;

        public LevelSoundTuneDialog(string package, string startEvent)
        {
            InitializeComponent();
            _package = package;
            Icon = SharedFormIcon.Icon;
            BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/sound-events/#tune-event");

            stops.SelectedIndex = 0;
            loop.SelectedIndex = 0;
            //Wired after the first picks, which are not edits
            stops.SelectedIndexChanged += stops_SelectedIndexChanged;
            loop.SelectedIndexChanged += loop_SelectedIndexChanged;

            Load += (s, e) =>
            {
                AutoCompleteStringCollection names = new AutoCompleteStringCollection();
                names.AddRange(SoundEventMetadata.AllEvents().ToArray());
                eventName.AutoCompleteCustomSource = names;
                if (!string.IsNullOrEmpty(startEvent))
                {
                    eventName.Text = startEvent;
                    ReadEvent();
                }
                else
                    Clear("Name a sound event and press Read. Events in permanent soundbanks (weapons, foley, UI...) can only be changed for every level.");
            };
        }

        private void LevelSoundTuneDialog_FormClosed(object sender, FormClosedEventArgs e)
        {
            _generation++;
            tips.Dispose();
        }

        private void readBtn_Click(object sender, EventArgs e) => ReadEvent();
        private void applyBtn_Click(object sender, EventArgs e) => Apply();
        private void revertBtn_Click(object sender, EventArgs e) => Revert();
        private void closeBtn_Click(object sender, EventArgs e) => Close();

        private void eventName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                ReadEvent();
            }
        }

        //Editing a value ticks its Change box
        private void volume_ValueChanged(object sender, EventArgs e) { if (!_filling) volumeOn.Checked = true; }
        private void pitch_ValueChanged(object sender, EventArgs e) { if (!_filling) pitchOn.Checked = true; }
        private void delay_ValueChanged(object sender, EventArgs e) { if (!_filling) delayOn.Checked = true; }
        private void fadeIn_ValueChanged(object sender, EventArgs e) { if (!_filling) fadeInOn.Checked = true; }
        private void fadeOut_ValueChanged(object sender, EventArgs e) { if (!_filling) fadeOutOn.Checked = true; }
        private void reach_ValueChanged(object sender, EventArgs e) { if (!_filling) reachOn.Checked = true; }
        private void loopCount_ValueChanged(object sender, EventArgs e) { if (!_filling) loopOn.Checked = true; }
        private void stops_SelectedIndexChanged(object sender, EventArgs e) { if (!_filling) stopsOn.Checked = true; }

        private void loop_SelectedIndexChanged(object sender, EventArgs e)
        {
            loopCount.Enabled = loop.SelectedIndex == 2;
            if (!_filling) loopOn.Checked = true;
        }

        private void Change_CheckedChanged(object sender, EventArgs e) => UpdateButtons();

        private void Clear(string message)
        {
            _read = null;
            about.Text = message;
            foreach (Label l in new[] { volumeShip, pitchShip, delayShip, fadeInShip, fadeOutShip, stopsShip, loopShip, reachShip })
                l.Text = "";
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            bool ready = _read != null && _read.Plan.Ok;
            applyBtn.Enabled = ready && new[] { volumeOn, pitchOn, delayOn, fadeInOn, fadeOutOn, stopsOn, loopOn, reachOn }.Any(o => o.Checked && o.Enabled);
            revertBtn.Enabled = ready && _read.Tuned;
        }

        /// <summary>Read the event as the level plays it, off the UI thread (the first look indexes the game's soundbanks).</summary>
        private void ReadEvent()
        {
            string name = eventName.Text.Trim();
            if (name.Length == 0)
                return;
            int generation = ++_generation;
            Clear("Reading " + name + "...");
            Cursor = Cursors.AppStarting;
            Task.Factory.StartNew(() => LevelSoundOverride.Inspect(_package, name)).ContinueWith(task =>
            {
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (IsDisposed || generation != _generation)
                            return;
                        Cursor = Cursors.Default;
                        ShowReading(task.IsFaulted ? null : task.Result, task.IsFaulted ? task.Exception.GetBaseException().Message : null);
                    });
                }
                catch (InvalidOperationException)
                {
                }
            });
        }

        private void ShowReading(LevelSoundOverride.Tuning read, string failure)
        {
            if (read == null || !read.Plan.Ok)
            {
                Clear(failure ?? read.Plan.Problem);
                return;
            }
            _read = read;
            SoundEventTuning.EventState now = read.After, shipped = read.Shipped;

            _filling = true;
            try
            {
                SoundEventTuning.TargetState target = now.Targets.FirstOrDefault(o => !o.NotTunable);
                SoundEventTuning.TargetState shippedTarget = shipped == null ? null : shipped.Targets.FirstOrDefault(o => !o.NotTunable);
                SoundEventTuning.ActionState play = now.Plays.FirstOrDefault(), shippedPlay = shipped == null ? null : shipped.Plays.FirstOrDefault();
                SoundEventTuning.EventState stopNow = now.Stops.Any() ? now : read.StopAfter, stopShipped = now.Stops.Any() ? shipped : read.StopShipped;
                SoundEventTuning.ActionState stop = stopNow == null ? null : stopNow.Stops.FirstOrDefault();
                SoundEventTuning.ActionState stopShip = stopShipped == null ? null : stopShipped.Stops.FirstOrDefault();

                Set(volume, target == null ? 0 : (decimal)target.VolumeDb, volumeOn, target != null, volumeShip, shippedTarget == null ? null : Fmt(shippedTarget.VolumeDb, "dB"));
                Set(pitch, target == null ? 0 : (decimal)target.PitchCents, pitchOn, target != null, pitchShip, shippedTarget == null ? null : Fmt(shippedTarget.PitchCents, "cents"));
                SoundEventTuning.ActionState delayed = play ?? now.Actions.FirstOrDefault(o => !o.Missing);
                SoundEventTuning.ActionState delayedShip = shippedPlay ?? (shipped == null ? null : shipped.Actions.FirstOrDefault(o => !o.Missing));
                Set(delay, delayed == null ? 0 : delayed.DelayMs ?? 0, delayOn, delayed != null, delayShip, delayedShip == null ? null : (delayedShip.DelayMs ?? 0) + " ms" + (delayedShip.DelayRange != null && (delayedShip.DelayRange[0] != 0 || delayedShip.DelayRange[1] != 0) ? " (+" + delayedShip.DelayRange[0] + ".." + delayedShip.DelayRange[1] + " random)" : ""));
                Set(fadeIn, play == null ? 0 : play.FadeMs ?? 0, fadeInOn, play != null, fadeInShip, shippedPlay == null ? null : (shippedPlay.FadeMs ?? 0) + " ms");
                fadeOutLabel.Text = now.Stops.Any() || read.StopEvent == null ? "Fade out (ms)" : "Fade out (ms) - " + read.StopEvent;
                Set(fadeOut, stop == null ? 0 : stop.FadeMs ?? 0, fadeOutOn, stop != null, fadeOutShip, stopShip == null ? null : (stopShip.FadeMs ?? 0) + " ms");
                stops.SelectedIndex = stop == null ? 0 : stop.TargetIsBus && (stopShip == null || !stopShip.TargetIsBus) ? 2 : stopShip != null && stop.Target == stopShip.Mixer && stop.Target != stopShip.Target ? 1 : 0;
                stops.Enabled = stopsOn.Enabled = stop != null;
                stopsOn.Checked = false;
                stopsShip.Text = stopShip == null ? "" : "shipped: " + (stopShip.TargetIsBus ? "a bus" : "its own sound");

                int? loops = now.Loop;
                loop.SelectedIndex = loops == 0 ? 1 : loops.HasValue && loops > 1 ? 2 : 0;
                if (loops.HasValue && loops > 1) loopCount.Value = Math.Min(loopCount.Maximum, loops.Value);
                loop.Enabled = loopOn.Enabled = target != null;
                loopOn.Checked = false;
                int? shippedLoops = shipped == null ? null : shipped.Loop;
                loopShip.Text = shipped == null ? "" : "shipped: " + (shippedLoops == -1 ? "mixed" : shippedLoops == 0 ? "for ever" : shippedLoops.HasValue && shippedLoops > 1 ? shippedLoops + " times" : "once");

                float? distance = now.MaxDistance;
                Set(reach, distance.HasValue ? (decimal)Math.Max(0.5f, distance.Value) : 0.5m, reachOn, distance.HasValue, reachShip, shipped == null || !shipped.MaxDistance.HasValue ? "plays in 2D" : Fmt(shipped.MaxDistance.Value, "m"));
            }
            finally
            {
                _filling = false;
            }

            List<string> lines = new List<string>();
            lines.Add("Soundbanks: " + string.Join(", ", read.Banks) + ".");
            List<string> changes = read.Changes;
            lines.Add(changes.Count == 0 ? "Plays as the base game ships it on this level." : "Changed on this level: " + string.Join("; ", changes) + ".");
            foreach (SoundEventTuning.TargetState t in now.Targets)
                lines.Add("Plays " + t.TypeName + " " + t.Id + (t.NotTunable ? " (not tunable here)" : " - " + t.Sounds.Count + " sound" + (t.Sounds.Count == 1 ? "" : "s") + ", positioning " + t.Positioning));
            if (read.StopEvent != null)
                lines.Add("Stopped by " + read.StopEvent + ": its fade out and reach are set here.");
            if (read.AlsoPlay.Count != 0)
                lines.Add("Also played by " + string.Join(", ", read.AlsoPlay.Take(6)) + (read.AlsoPlay.Count > 6 ? " and " + (read.AlsoPlay.Count - 6) + " more" : "") + ": volume, pitch, loop and reach change for those too.");
            lines.AddRange(read.Plan.Notes);
            about.Text = string.Join(Environment.NewLine, lines);
            UpdateButtons();
        }

        private void Set(NumericUpDown box, decimal value, CheckBox on, bool enabled, Label shipped, string shippedText)
        {
            box.Value = Math.Max(box.Minimum, Math.Min(box.Maximum, value));
            box.Enabled = on.Enabled = enabled;
            on.Checked = false;
            shipped.Text = shippedText == null ? "" : "shipped: " + shippedText;
        }

        private static string Fmt(float value, string unit)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture) + " " + unit;
        }

        private SoundEventTuning.Settings Settings()
        {
            SoundEventTuning.Settings s = new SoundEventTuning.Settings();
            if (volumeOn.Checked) s.VolumeDb = (float)volume.Value;
            if (pitchOn.Checked) s.PitchCents = (float)pitch.Value;
            if (delayOn.Checked) s.DelayMs = (int)delay.Value;
            if (fadeInOn.Checked) s.FadeInMs = (int)fadeIn.Value;
            if (fadeOutOn.Checked) s.FadeOutMs = (int)fadeOut.Value;
            if (stopsOn.Checked) s.Stops = (SoundEventTuning.StopReach)stops.SelectedIndex;
            if (loopOn.Checked) s.Loop = loop.SelectedIndex == 0 ? 1 : loop.SelectedIndex == 1 ? 0 : (int)loopCount.Value;
            if (reachOn.Checked) s.MaxDistance = (float)reach.Value;
            return s;
        }

        private void Apply()
        {
            if (_read == null)
                return;
            string name = _read.Event;
            LevelSoundOverride.Tuning done;
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                done = LevelSoundOverride.Tune(_package, name, Settings(), true);
            }
            finally { Cursor.Current = Cursors.Default; }

            LevelSoundOverride.RaiseChanged();
            if (!done.Plan.Ok)
            {
                MessageBox.Show(this, name + " wasn't tuned:\n\n" + done.Plan.Problem, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ReadEvent();
        }

        private void Revert()
        {
            if (_read == null)
                return;
            string name = _read.Event;
            if (MessageBox.Show(this, "Give " + name + (_read.StopEvent != null ? " and " + _read.StopEvent : "") + " the base game's settings again on this level?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            LevelSoundOverride.Tuning done;
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                done = LevelSoundOverride.Untune(_package, name, true);
            }
            finally { Cursor.Current = Cursors.Default; }

            LevelSoundOverride.RaiseChanged();
            if (!done.Plan.Ok)
                MessageBox.Show(this, done.Plan.Problem, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            ReadEvent();
        }
    }
}

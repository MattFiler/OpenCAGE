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
    public class LevelSoundTuneDialog : BaseWindow
    {
        private readonly string _package;

        private readonly TextBox eventName = new TextBox { Dock = DockStyle.Fill, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.CustomSource };
        private readonly Button readBtn = new Button { Text = "Read", Width = 70, Height = 24 };
        private readonly TextBox about = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };

        private readonly NumericUpDown volume = Number(-96, 24, 1, 0.5m);
        private readonly NumericUpDown pitch = Number(-2400, 2400, 0, 50);
        private readonly NumericUpDown delay = Number(0, 600000, 0, 100);
        private readonly NumericUpDown fadeIn = Number(0, 600000, 0, 100);
        private readonly NumericUpDown fadeOut = Number(0, 600000, 0, 100);
        private readonly ComboBox stops = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
        private readonly ComboBox loop = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        private readonly NumericUpDown loopCount = Number(2, 32767, 0, 1);
        private readonly NumericUpDown reach = Number(0.5m, 10000, 1, 1);

        private readonly CheckBox volumeOn = Change(), pitchOn = Change(), delayOn = Change(), fadeInOn = Change(), fadeOutOn = Change(), stopsOn = Change(), loopOn = Change(), reachOn = Change();
        private readonly Label volumeShip = Shipped(), pitchShip = Shipped(), delayShip = Shipped(), fadeInShip = Shipped(), fadeOutShip = Shipped(), stopsShip = Shipped(), loopShip = Shipped(), reachShip = Shipped();
        private readonly Label fadeOutLabel = new Label { Text = "Fade out (ms)", AutoSize = true, Anchor = AnchorStyles.Left };

        private readonly Button applyBtn = new Button { Text = "Apply", Width = 90, Height = 26, Enabled = false };
        private readonly Button revertBtn = new Button { Text = "Put Back", Width = 90, Height = 26, Enabled = false };
        private readonly Button closeBtn = new Button { Text = "Close", Width = 90, Height = 26 };
        private readonly ToolTip tips = new ToolTip();

        private LevelSoundOverride.Tuning _read;
        private bool _filling;
        private int _generation;

        public LevelSoundTuneDialog(string package, string startEvent)
        {
            _package = package;
            Text = "Tune Sound Event";
            Icon = SharedFormIcon.Icon;
            ClientSize = new Size(760, 600);
            MinimumSize = new Size(640, 540);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/sound-events/#tune-event");

            stops.Items.AddRange(new object[] { "Its own sound", "Everything under its actor-mixer", "Everything on its bus" });
            loop.Items.AddRange(new object[] { "Once", "For ever", "Times:" });
            stops.SelectedIndex = 0;
            loop.SelectedIndex = 0;

            tips.SetToolTip(eventName, "The sound event to tune, as a Sound entity's sound_event names it.");
            tips.SetToolTip(volume, "The volume of what the event plays, in dB - its own: the containers above it add theirs. Negative is quieter; 6 dB is about half or double.");
            tips.SetToolTip(pitch, "Pitch in cents: 100 is a semitone, 1200 an octave.");
            tips.SetToolTip(delay, "How long after the event is played its sound starts, in ms. A random delay range the game gave it is cleared.");
            tips.SetToolTip(fadeIn, "How long the sound takes to fade in as it starts, in ms.");
            tips.SetToolTip(fadeOut, "How long the sound takes to fade out when its stop event stops it, in ms (a Play event's goes on its Stop_ event). A Sound entity fades only when it has that stop_event: Sound.stop on its own is instant.");
            tips.SetToolTip(stops, "What the stop event stops: the sound it was made for, everything under the same actor-mixer (a music system's other tracks, say), or everything playing on the same bus.");
            tips.SetToolTip(loop, "Whether every sound the event plays repeats: once (no loop), for ever (until stopped), or a number of times.");
            tips.SetToolTip(reach, "How far away the event can be heard, in metres. Its distance curves are copied for this event alone and stretched to end here - the curve other sounds share is left as it is.");
            tips.SetToolTip(applyBtn, "Write the ticked settings into the level's sound package (copying in the soundbanks that carry the event the first time).");
            tips.SetToolTip(revertBtn, "Give the event (and its Stop_ event) the base game's settings again on this level.");

            BuildLayout();

            readBtn.Click += (s, e) => ReadEvent();
            eventName.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ReadEvent(); } };
            applyBtn.Click += (s, e) => Apply();
            revertBtn.Click += (s, e) => Revert();
            closeBtn.Click += (s, e) => Close();
            loop.SelectedIndexChanged += (s, e) => loopCount.Enabled = loop.SelectedIndex == 2;
            Tick(volume, volumeOn); Tick(pitch, pitchOn); Tick(delay, delayOn); Tick(fadeIn, fadeInOn); Tick(fadeOut, fadeOutOn); Tick(reach, reachOn); Tick(loopCount, loopOn);
            stops.SelectedIndexChanged += (s, e) => { if (!_filling) stopsOn.Checked = true; };
            loop.SelectedIndexChanged += (s, e) => { if (!_filling) loopOn.Checked = true; };
            foreach (CheckBox box in new[] { volumeOn, pitchOn, delayOn, fadeInOn, fadeOutOn, stopsOn, loopOn, reachOn })
                box.CheckedChanged += (s, e) => UpdateButtons();
            CancelButton = closeBtn;

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
            FormClosed += (s, e) => { _generation++; tips.Dispose(); };
        }

        private static NumericUpDown Number(decimal min, decimal max, int decimals, decimal step)
        {
            return new NumericUpDown { Minimum = min, Maximum = max, DecimalPlaces = decimals, Increment = step, Width = 110, Anchor = AnchorStyles.Left };
        }

        private static CheckBox Change()
        {
            return new CheckBox { Text = "Change", AutoSize = true, Anchor = AnchorStyles.Left };
        }

        private static Label Shipped()
        {
            return new Label { AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = SystemColors.GrayText };
        }

        /// <summary>Editing a value ticks its Change box.</summary>
        private void Tick(NumericUpDown box, CheckBox on)
        {
            box.ValueChanged += (s, e) => { if (!_filling) on.Checked = true; };
        }

        private void BuildLayout()
        {
            TableLayoutPanel top = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 3, RowCount = 1, Height = 32, Padding = new Padding(6, 6, 6, 0) };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.Controls.Add(new Label { Text = "Sound event:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            top.Controls.Add(eventName, 1, 0);
            top.Controls.Add(readBtn, 2, 0);

            TableLayoutPanel grid = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 4, AutoSize = true, Padding = new Padding(8, 6, 8, 6) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            int row = 0;
            Action<Control, Control, CheckBox, Label> add = (label, editor, on, shipped) =>
            {
                grid.Controls.Add(label, 0, row);
                grid.Controls.Add(editor, 1, row);
                grid.Controls.Add(on, 2, row);
                grid.Controls.Add(shipped, 3, row);
                row++;
            };
            add(Text_("Volume (dB)"), volume, volumeOn, volumeShip);
            add(Text_("Pitch (cents)"), pitch, pitchOn, pitchShip);
            add(Text_("Start delay (ms)"), delay, delayOn, delayShip);
            add(Text_("Fade in (ms)"), fadeIn, fadeInOn, fadeInShip);
            add(fadeOutLabel, fadeOut, fadeOutOn, fadeOutShip);
            add(Text_("Stop reaches"), stops, stopsOn, stopsShip);
            FlowLayoutPanel loopRow = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0), WrapContents = false };
            loopRow.Controls.Add(loop);
            loopRow.Controls.Add(loopCount);
            loopCount.Enabled = false;
            add(Text_("Loop"), loopRow, loopOn, loopShip);
            add(Text_("Reach (m)"), reach, reachOn, reachShip);

            FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 38, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(4) };
            buttons.Controls.AddRange(new Control[] { closeBtn, revertBtn, applyBtn });

            GroupBox info = new GroupBox { Text = "The event on this level", Dock = DockStyle.Fill, Padding = new Padding(6) };
            info.Controls.Add(about);

            Controls.Add(info);
            Controls.Add(buttons);
            Controls.Add(grid);
            Controls.Add(top);
        }

        private static Label Text_(string text)
        {
            return new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left };
        }

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

using OpenCAGE.Audio;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Makes a soundbank of the level's own - new sound events, each playing a .wav - without Wwise. Name the bank, then add
    /// events: a name, a .wav, and a sound of the game's for it to sound like (its mixer, output bus, 3D positioning and
    /// attenuation are borrowed - see <see cref="WwiseBankBuilder"/>), and whether it loops. Saving puts the bank in the level's
    /// sound package and its events in the level's sound event table, where scripts find them: a SoundLoadBank loads the bank
    /// (a VariableString naming it, linked to its sound_bank pin) and a Sound entity plays an event by name. Opened on a bank made
    /// here before, it lists that bank's events, to add to or take out of it.
    /// </summary>
    public class LevelSoundBankDialog : BaseWindow
    {
        private readonly string _package;
        private readonly uint? _existing;
        private byte[] _existingBytes;
        private Dictionary<uint, string> _existingNames = new Dictionary<uint, string>();

        private readonly TextBox bankName = new TextBox { Dock = DockStyle.Fill };
        private readonly ListView events = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = true };
        private readonly Button removeEventBtn = new Button { Text = "Remove Event", Width = 110, Height = 26, Enabled = false };
        private readonly TextBox eventName = new TextBox { Dock = DockStyle.Fill };
        private readonly TextBox wavPath = new TextBox { ReadOnly = true, Dock = DockStyle.Fill };
        private readonly Button browseBtn = new Button { Text = "Browse...", Width = 90, Height = 24 };
        private readonly TextBox search = new TextBox { Dock = DockStyle.Fill };
        private readonly ListView sounds = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false };
        private readonly CheckBox loop = new CheckBox { Text = "Loop until stopped", AutoSize = true };
        private readonly Button playBtn = new Button { Text = "Play Sound", Width = 90, Height = 26, Enabled = false };
        private readonly Button stopBtn = new Button { Text = "Stop", Width = 60, Height = 26, Enabled = false };
        private readonly Button addEventBtn = new Button { Text = "Add Event", Width = 100, Height = 26 };
        private readonly TextBox summary = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
        private readonly Button saveBtn = new Button { Text = "Create", Width = 90, Height = 26, Enabled = false };
        private readonly Button cancelBtn = new Button { Text = "Cancel", Width = 90, Height = 26 };
        private readonly ToolTip tips = new ToolTip();

        private WwiseSoundLibrary _library;
        private List<LevelSoundImportDialog.Candidate> _candidates = new List<LevelSoundImportDialog.Candidate>();
        private readonly List<Row> _rows = new List<Row>();
        private readonly HashSet<uint> _dropped = new HashSet<uint>();
        private bool _working;

        private WavePlayer _player;
        private DecodedAudio _playing;

        private sealed class Row
        {
            public string Name;
            public string Wav;
            public uint Template;
            public string TemplateName;
            public bool Loop;

            /// <summary>An event already in the bank (kept as it is), rather than one to make.</summary>
            public bool Existing;
        }

        /// <param name="package">The open level's sound package.</param>
        /// <param name="existing">A soundbank of the level's own in the package to add to, by id; null for a new one.</param>
        /// <param name="existingName">That soundbank's name.</param>
        public LevelSoundBankDialog(string package, uint? existing, string existingName)
        {
            _package = package;
            _existing = existing;

            Text = existing == null ? "New Soundbank" : "Soundbank " + existingName;
            Icon = SharedFormIcon.Icon;
            ClientSize = new Size(880, 720);
            MinimumSize = new Size(680, 600);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/sound-events/#new-soundbank");

            if (existing != null)
            {
                bankName.Text = existingName;
                bankName.ReadOnly = true;
                saveBtn.Text = "Save";
            }

            events.Columns.Add("Event", 260);
            events.Columns.Add(".wav", 200);
            events.Columns.Add("Sounds like", 300);
            events.Columns.Add("Loops", 60);
            sounds.Columns.Add("Sound", 330);
            sounds.Columns.Add("Id", 85);
            sounds.Columns.Add("Kept in", 220);

            tips.SetToolTip(bankName, "The soundbank's name: letters, digits and underscores. A SoundLoadBank loads it by this name (a VariableString holding it, linked to the loader's sound_bank pin).");
            tips.SetToolTip(eventName, "The sound event's name, as a Sound entity's sound_event names it: letters, digits and underscores, and not a name the game already uses.");
            tips.SetToolTip(sounds, "The sound of the game's this one is made to sound like: its actor-mixer, output bus, 3D positioning and distance attenuation, reverb sends and volume are borrowed. The .wav is laid out like it (channels and sample rate).");
            tips.SetToolTip(loop, "Repeat the sound until something stops it - the whole .wav, or its own loop region if it has one (a smpl chunk, as audio editors write).");
            tips.SetToolTip(playBtn, "Play the selected sound of the game's.");

            BuildLayout();

            browseBtn.Click += (s, e) => Browse();
            search.TextChanged += (s, e) => FillSounds();
            sounds.SelectedIndexChanged += (s, e) => playBtn.Enabled = SelectedSound() != null;
            events.SelectedIndexChanged += (s, e) => removeEventBtn.Enabled = events.SelectedItems.Count != 0 && !_working;
            addEventBtn.Click += (s, e) => AddEvent();
            removeEventBtn.Click += (s, e) => RemoveEvents();
            playBtn.Click += (s, e) => PlaySound();
            stopBtn.Click += (s, e) => StopPlaying();
            saveBtn.Click += (s, e) => Save();
            cancelBtn.Click += (s, e) => Close();
            bankName.TextChanged += (s, e) => Describe();

            CancelButton = cancelBtn;
            Load += (s, e) => LoadSounds();
            FormClosed += (s, e) =>
            {
                StopPlaying();
                tips.Dispose();
            };
        }

        #region LAYOUT

        private void BuildLayout()
        {
            TableLayoutPanel nameRow = Row3(new Label { Text = "Soundbank name:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 3, 3) }, bankName, null);

            FlowLayoutPanel eventButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Margin = new Padding(0) };
            eventButtons.Controls.Add(removeEventBtn);

            TableLayoutPanel newEvent = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Margin = new Padding(0, 6, 0, 0) };
            newEvent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            newEvent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            newEvent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            newEvent.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            newEvent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            newEvent.Controls.Add(Row3(new Label { Text = "New event's name:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 3, 3) }, eventName, null), 0, 0);
            newEvent.Controls.Add(Row3(new Label { Text = "Its audio (.wav):", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 3, 3) }, wavPath, browseBtn), 0, 1);
            newEvent.Controls.Add(Row3(new Label { Text = "Sounds like (search):", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 3, 3) }, search, null), 0, 2);
            sounds.Margin = new Padding(3, 3, 3, 3);
            newEvent.Controls.Add(sounds, 0, 3);
            FlowLayoutPanel addRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0) };
            loop.Margin = new Padding(3, 7, 12, 3);
            addRow.Controls.AddRange(new Control[] { loop, playBtn, stopBtn, addEventBtn });
            newEvent.Controls.Add(addRow, 0, 4);

            GroupBox group = new GroupBox { Text = "Add an event", Dock = DockStyle.Fill, Padding = new Padding(6) };
            group.Controls.Add(newEvent);

            FlowLayoutPanel buttonRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Margin = new Padding(0, 4, 0, 0) };
            buttonRow.Controls.AddRange(new Control[] { cancelBtn, saveBtn });

            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, Padding = new Padding(8) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 70));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(nameRow, 0, 0);
            root.Controls.Add(new Label { Text = "Its events:", AutoSize = true, Margin = new Padding(3, 8, 3, 0) }, 0, 1);
            root.Controls.Add(events, 0, 2);
            root.Controls.Add(eventButtons, 0, 3);
            root.Controls.Add(group, 0, 4);
            summary.Margin = new Padding(3, 6, 3, 3);
            root.Controls.Add(summary, 0, 5);
            root.Controls.Add(buttonRow, 0, 6);
            Controls.Add(root);
        }

        private static TableLayoutPanel Row3(Control label, Control field, Control button)
        {
            TableLayoutPanel row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, AutoSize = true, Margin = new Padding(0) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.Controls.Add(label, 0, 0);
            row.Controls.Add(field, 1, 0);
            if (button != null)
                row.Controls.Add(button, 2, 0);
            return row;
        }

        #endregion

        #region LOADING

        private void LoadSounds()
        {
            summary.Text = SoundPreviewLibrary.IsReady ? "Listing the game's sounds..." : "Indexing the game's soundbanks...";
            Task.Factory.StartNew(() =>
            {
                try
                {
                    WwiseSoundLibrary library = SoundPreviewLibrary.Get();
                    List<LevelSoundImportDialog.Candidate> candidates = LevelSoundImportDialog.Candidates(library, _package)
                        .Where(o => o.KeptIn.IndexOf("Voiced", StringComparison.Ordinal) < 0).ToList();

                    //A bank made here before: its events, named from the level's sound event table
                    byte[] existing = null;
                    if (_existing != null && File.Exists(_package))
                    {
                        WwiseFilePackageBuilder package = WwiseFilePackageBuilder.Load(_package);
                        WwiseFilePackageBuilder.Item item = package.Banks.FirstOrDefault(o => o.Id == _existing.Value);
                        existing = item == null ? null : item.Read();
                    }

                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (IsDisposed)
                            return;
                        _library = library;
                        _candidates = candidates;
                        _existingBytes = existing;
                        if (existing != null)
                        {
                            _existingNames = LevelSoundOverride.LevelEventNames(_existing.Value);
                            foreach (uint id in WwiseBankBuilder.EventsIn(existing))
                            {
                                string name;
                                _rows.Add(new Row { Existing = true, Name = _existingNames.TryGetValue(id, out name) ? name : "Event " + id, Wav = "(in the bank)", TemplateName = "" });
                            }
                        }
                        FillEvents();
                        FillSounds();
                        Describe();
                    });
                }
                catch (Exception e)
                {
                    try { BeginInvoke((MethodInvoker)delegate { if (!IsDisposed) summary.Text = "The game's sounds could not be listed: " + e.Message; }); }
                    catch (InvalidOperationException) { }
                }
            });
        }

        private void FillSounds()
        {
            string wanted = search.Text.Trim().ToUpperInvariant();
            LevelSoundImportDialog.Candidate before = SelectedSound();
            sounds.BeginUpdate();
            sounds.Items.Clear();
            List<ListViewItem> items = new List<ListViewItem>();
            foreach (LevelSoundImportDialog.Candidate candidate in _candidates)
            {
                if (wanted.Length != 0 && candidate.Search.IndexOf(wanted, StringComparison.Ordinal) < 0)
                    continue;
                if (candidate.Item == null)
                    candidate.Item = new ListViewItem(new[] { candidate.Name, candidate.SourceId.ToString(), candidate.KeptIn }) { Tag = candidate };
                items.Add(candidate.Item);
            }
            sounds.Items.AddRange(items.ToArray());
            if (before != null && items.Contains(before.Item))
            {
                before.Item.Selected = true;
                before.Item.EnsureVisible();
            }
            sounds.EndUpdate();
        }

        private void FillEvents()
        {
            events.BeginUpdate();
            events.Items.Clear();
            foreach (Row row in _rows)
                events.Items.Add(new ListViewItem(new[] { row.Name, row.Existing ? row.Wav : Path.GetFileName(row.Wav), row.Existing ? "(in the bank)" : row.TemplateName, row.Existing ? "" : row.Loop ? "Yes" : "No" }) { Tag = row });
            events.EndUpdate();
            removeEventBtn.Enabled = false;
        }

        private LevelSoundImportDialog.Candidate SelectedSound()
        {
            return sounds.SelectedItems.Count == 0 ? null : sounds.SelectedItems[0].Tag as LevelSoundImportDialog.Candidate;
        }

        #endregion

        #region EDITING

        private void Browse()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Wave File|*.wav";
                dialog.Title = "The new event's audio";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                SetWav(dialog.FileName);
            }
        }

        /// <summary>Take a .wav for the event being added - named for it, if it has no name yet.</summary>
        public void SetWav(string file)
        {
            wavPath.Text = file;
            if (eventName.Text.Trim().Length == 0)
                eventName.Text = new string(Path.GetFileNameWithoutExtension(file).Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        }

        /// <summary>Pick the sound of the game's the event being added sounds like, by source id.</summary>
        public bool SetTemplate(uint sourceId)
        {
            LevelSoundImportDialog.Candidate candidate = _candidates.FirstOrDefault(o => o.SourceId == sourceId);
            if (candidate == null)
                return false;
            search.Text = "";
            FillSounds();
            candidate.Item.Selected = true;
            candidate.Item.Focused = true;
            candidate.Item.EnsureVisible();
            return true;
        }

        private static bool ValidName(string name)
        {
            return name.Length != 0 && name.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_');
        }

        private void AddEvent()
        {
            string name = eventName.Text.Trim();
            LevelSoundImportDialog.Candidate template = SelectedSound();
            string problem =
                !ValidName(name) ? "Give the event a name of letters, digits and underscores."
                : _rows.Any(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase)) ? "The soundbank already has an event called " + name + "."
                : _library != null && _library.HasEvent(name) && !_existingNames.ContainsValue(name) ? "The game already has an event called " + name + ": give the new one a name of its own."
                : string.IsNullOrEmpty(wavPath.Text) || !File.Exists(wavPath.Text) ? "Choose the event's audio: a .wav."
                : template == null ? "Choose the sound of the game's it sounds like."
                : null;
            if (problem != null)
            {
                MessageBox.Show(this, problem, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _rows.Add(new Row { Name = name, Wav = wavPath.Text, Template = template.SourceId, TemplateName = template.Name + " (" + template.KeptIn + ")", Loop = loop.Checked });
            eventName.Text = "";
            wavPath.Text = "";
            loop.Checked = false;
            FillEvents();
            Describe();
        }

        private void RemoveEvents()
        {
            foreach (Row row in events.SelectedItems.Cast<ListViewItem>().Select(o => (Row)o.Tag).ToList())
            {
                _rows.Remove(row);
                if (row.Existing)
                    _dropped.Add(CathodeLib.Utilities.SoundHashedString(row.Name));
            }
            FillEvents();
            Describe();
        }

        /// <summary>What saving would do, and whether it can.</summary>
        private void Describe()
        {
            string name = bankName.Text.Trim();
            uint id = CathodeLib.Utilities.SoundHashedString(name);
            string problem =
                !ValidName(name) ? "Name the soundbank: letters, digits and underscores."
                : _existing == null && (LevelSoundOverride.BaseBankFile(id, null) != null || LevelSoundOverride.IsPermanent(id, SoundPreviewLibrary.SoundDirectory)) ? "The game already has a soundbank called " + name + ": give the new one a name of its own."
                : _existing == null && PackageHasBank(id) ? "The level's package already has a soundbank called " + name + " - select it in the Level Sound Override window and choose Edit Soundbank to add to it."
                : _rows.Count == 0 ? "Add an event: a name, a .wav and a sound of the game's for it to sound like."
                : null;

            saveBtn.Enabled = problem == null && !_working;
            if (problem != null)
            {
                summary.Text = problem;
                return;
            }

            int made = _rows.Count(o => !o.Existing);
            summary.Text = (_existing == null ? "Makes the soundbank " : "Saves the soundbank ") + name + " with " + _rows.Count + " event" + (_rows.Count == 1 ? "" : "s")
                + (made != _rows.Count ? " (" + made + " new)" : "") + ", in the level's sound package, and lists its events in the level's sound event table."
                + Environment.NewLine + Environment.NewLine + "To play them: add a SoundLoadBank to the level's script (trigger_via_pin off, to load it as the level loads) and link a VariableString holding \"" + name
                + "\" to its sound_bank pin; then a Sound entity plays an event by naming it in its sound_event (" + string.Join(", ", _rows.Take(3).Select(o => o.Name)) + (_rows.Count > 3 ? "..." : "") + ")."
                + (_rows.Any(o => o.Loop) ? " A looping event repeats until its Sound entity's stop pin is triggered - leave that entity's multi_trigger off, or stop does nothing." : "");
        }

        private bool PackageHasBank(uint id)
        {
            try
            {
                return File.Exists(_package) && WwiseFilePackageBuilder.Load(_package).Banks.Any(o => o.Id == id);
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region SAVING

        /// <summary>Encode the new events' .wav files, build the bank, then put it and its events in the level.</summary>
        private void Save()
        {
            string name = bankName.Text.Trim();
            List<Row> made = _rows.Where(o => !o.Existing).ToList();
            WwiseSoundLibrary library = _library;
            byte[] existing = _existingBytes;
            Dictionary<uint, string> names = _existingNames;
            HashSet<uint> dropped = new HashSet<uint>(_dropped);

            _working = true;
            saveBtn.Enabled = false;
            addEventBtn.Enabled = false;
            summary.Text = "Encoding " + made.Count + " .wav file" + (made.Count == 1 ? "" : "s") + " and building the soundbank...";
            StopPlaying();

            Task.Factory.StartNew(() =>
            {
                string problem = null;
                WwiseBankBuilder.Result built = null;
                List<string> notes = new List<string>();
                try
                {
                    List<WwiseBankBuilder.NewEvent> add = new List<WwiseBankBuilder.NewEvent>();
                    foreach (Row row in made)
                    {
                        SoundImport.Reading reading = SoundImport.ReadForBank(row.Wav, library.MediaFor(row.Template), new SoundImport.Options { MatchOriginalChannels = true, MatchOriginalRate = true });
                        if (!reading.Ok)
                            throw new InvalidDataException(row.Name + ": " + reading.Problem);
                        notes.AddRange(reading.Notes.Select(o => row.Name + ": " + o));
                        add.Add(new WwiseBankBuilder.NewEvent { Name = row.Name, Wem = reading.Wem, TemplateSourceId = row.Template, Loop = row.Loop });
                    }
                    built = WwiseBankBuilder.Build(name, add, library, existing, names, dropped);
                    notes.AddRange(built.Notes);
                }
                catch (Exception e)
                {
                    problem = e.Message;
                }

                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (IsDisposed)
                            return;
                        _working = false;
                        addEventBtn.Enabled = true;
                        if (problem != null)
                        {
                            summary.Text = "The soundbank could not be made: " + problem;
                            Describe();
                            summary.Text = "The soundbank could not be made: " + problem;
                            return;
                        }

                        LevelSoundOverride.Plan plan;
                        Cursor.Current = Cursors.WaitCursor;
                        try { plan = LevelSoundOverride.SaveBank(_package, name, built, true); }
                        finally { Cursor.Current = Cursors.Default; }
                        LevelSoundOverride.RaiseChanged();

                        if (!plan.Ok)
                        {
                            MessageBox.Show(this, "The soundbank could not be saved:\n\n" + plan.Problem, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            Describe();
                            return;
                        }
                        if (notes.Count != 0)
                            MessageBox.Show(this, string.Join("\n\n", plan.Notes.Concat(notes)), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        DialogResult = DialogResult.OK;
                        Close();
                    });
                }
                catch (InvalidOperationException)
                {
                }
            });
        }

        #endregion

        #region LISTENING

        private void PlaySound()
        {
            LevelSoundImportDialog.Candidate candidate = SelectedSound();
            if (candidate == null || _library == null)
                return;
            try
            {
                StopPlaying();
                _playing = WwiseAudioDecoder.Decode(WwiseSoundLibrary.ReadMedia(_library.MediaFor(candidate.SourceId)));
                _player = new WavePlayer(_playing);
                _player.PlaybackEnded += (s, e) =>
                {
                    try { BeginInvoke((MethodInvoker)delegate { if (!IsDisposed) stopBtn.Enabled = false; }); }
                    catch (InvalidOperationException) { }
                };
                _player.Play();
                stopBtn.Enabled = true;
            }
            catch (Exception e)
            {
                MessageBox.Show(this, "That sound can't be played: " + e.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void StopPlaying()
        {
            if (_player != null)
            {
                _player.Stop();
                _player.Dispose();
                _player = null;
            }
            if (_playing != null)
            {
                _playing.Dispose();
                _playing = null;
            }
            stopBtn.Enabled = false;
        }

        #endregion
    }
}

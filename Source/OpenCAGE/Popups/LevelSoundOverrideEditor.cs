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
    /// What the open level's own sound package (WORLD/LEVEL_SOUND_OVERRIDE.PCK) holds: each soundbank and stream in it, the
    /// sounds changed inside each soundbank, and what in the base game each stands in for on this level - every one named by
    /// the sound events that play it, or by the soundbank's own name, rather than by the number it is filed under. Opened
    /// from the Sound Editor. A .wav goes in with Import WAV (made into the game's own format here, in place of a sound of
    /// the game's or as a stream of its own); whole files can be added (a soundbank built with Wwise, or a stream named for
    /// its id), exported, or taken out - a soundbank or stream taken out is read from DATA/SOUND again on this level, and a
    /// sound changed in a soundbank goes back to the base game's audio. Built in code: a summary, a search box, the list and
    /// a row of buttons.
    /// </summary>
    public class LevelSoundOverrideEditor : BaseWindow
    {
        private readonly string _package;

        private readonly Label summary = new Label { Dock = DockStyle.Top, Height = 54, Padding = new Padding(6, 6, 6, 0) };
        private readonly TextBox search = new TextBox { Dock = DockStyle.Fill };
        private readonly ListView list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = true, ShowItemToolTips = true };
        private readonly FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 38, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(4) };
        private readonly Button closeBtn = new Button { Text = "Close", Width = 90, Height = 26 };
        private readonly Button removeBtn = new Button { Text = "Remove", Width = 90, Height = 26 };
        private readonly Button exportBtn = new Button { Text = "Export...", Width = 90, Height = 26 };
        private readonly Button addBtn = new Button { Text = "Add Files...", Width = 100, Height = 26 };
        private readonly Button importBtn = new Button { Text = "Import WAV...", Width = 110, Height = 26 };
        private readonly Button bankBtn = new Button { Text = "New Soundbank...", Width = 120, Height = 26 };
        private readonly ToolTip tips = new ToolTip();

        private enum RowKind { Bank, BankSound, Stream }

        private sealed class Row
        {
            public RowKind Kind;

            /// <summary>The soundbank or stream in the package - for a sound in a soundbank, the soundbank.</summary>
            public WwiseFilePackageBuilder.Item Item;

            /// <summary>A stream's id, or a sound's in a soundbank; 0 for a soundbank.</summary>
            public uint SourceId;

            /// <summary>For a soundbank: one only this level has (none of that id in DATA/SOUND) - one made here, say.</summary>
            public bool Own;

            /// <summary>The sound's bytes, for a sound in a soundbank (cut out of it as the list was built).</summary>
            public byte[] Media;

            /// <summary>For a soundbank: its sounds, by source id.</summary>
            public List<uint> Sounds = new List<uint>();

            public string Name, File, KindText, Language, OnThisLevel;
            public int Length;
            public string Search;
        }

        private List<Row> _rows = new List<Row>();
        private int _building;

        public LevelSoundOverrideEditor(string package)
        {
            _package = package;

            Text = "Level Sound Override";
            Icon = SharedFormIcon.Icon;
            ClientSize = new Size(1000, 460);
            MinimumSize = new Size(620, 300);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/sound-events/#level-sound-override");

            list.Columns.Add("Name", 270);
            list.Columns.Add("File", 175);
            list.Columns.Add("Kind", 115);
            list.Columns.Add("Size", 65, HorizontalAlignment.Right);
            list.Columns.Add("On this level", 350);

            tips.SetToolTip(importBtn, "Make game audio from a .wav: pick the sound it replaces on this level (by the sound events that play it), or give it a stream id of its own. Encoded the way the game's own audio is - no Wwise needed.");
            tips.SetToolTip(addBtn, "Put a soundbank (.bnk, built with the game's Wwise version) or a stream (.wem named for its id) in the package as it is. One with the same id as the base game's is read instead of it on this level - except a permanent soundbank, which the game loads before any level.");
            tips.SetToolTip(exportBtn, "Save the selected soundbank or stream - or a sound from inside a soundbank - out of the package, as Wwise audio (.wem) or as a .wav.");
            tips.SetToolTip(removeBtn, "Take the selected files out of the package (on this level the game reads the base game's instead, or nothing for a file only this level has), and put the base game's audio back in place of the selected sounds in soundbanks.");
            importBtn.Click += (s, e) => ImportWav();
            bankBtn.Click += (s, e) => EditBank();
            tips.SetToolTip(bankBtn, "Make a soundbank of the level's own: new sound events, each playing a .wav and sounding like a sound of the game's you pick. With a soundbank of the level's own selected, add events to it or take them out.");
            addBtn.Click += (s, e) => AddFiles();
            exportBtn.Click += (s, e) => Export();
            removeBtn.Click += (s, e) => RemoveSelected();
            closeBtn.Click += (s, e) => Close();
            search.TextChanged += (s, e) => Fill();
            list.SelectedIndexChanged += (s, e) => UpdateButtons();
            list.DoubleClick += (s, e) =>
            {
                List<Row> rows = Selected();
                if (rows.Count == 1 && rows[0].Kind == RowKind.Bank && rows[0].Own)
                    EditBank();
                else
                    ImportWav();
            };
            list.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Delete) return;
                e.Handled = true;
                RemoveSelected();
            };

            //Right to left: Close at the far right, then what acts on the selection, then what adds
            buttons.Controls.AddRange(new Control[] { closeBtn, removeBtn, exportBtn, addBtn, bankBtn, importBtn });

            TableLayoutPanel searchRow = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 1, Height = 28, Padding = new Padding(3, 0, 3, 2) };
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            searchRow.Controls.Add(new Label { Text = "Search:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 5, 3, 3) }, 0, 0);
            searchRow.Controls.Add(search, 1, 0);

            //Docked from the edges in: the summary, search and buttons first, then the list in what is left
            Controls.Add(list);
            Controls.Add(buttons);
            Controls.Add(searchRow);
            Controls.Add(summary);

            CancelButton = closeBtn;
            Load += (s, e) => Rebuild();
            LevelSoundOverride.Changed += OnChanged;
            FormClosed += (s, e) =>
            {
                _building++;
                LevelSoundOverride.Changed -= OnChanged;
                tips.Dispose();
            };
        }

        private void OnChanged()
        {
            if (!IsDisposed)
                Rebuild();
        }

        private string Relative()
        {
            if (!string.IsNullOrEmpty(Singleton.PathToAI) && _package.StartsWith(Path.GetFullPath(Singleton.PathToAI), StringComparison.OrdinalIgnoreCase))
                return _package.Substring(Path.GetFullPath(Singleton.PathToAI).Length).TrimStart('\\', '/');
            return _package;
        }

        /// <summary>
        /// Read the package again and list it. Off the UI thread: naming the sounds needs the sound index, and finding the
        /// sounds changed inside a soundbank means comparing it with the base game's copy, tens of megabytes for some banks.
        /// </summary>
        private void Rebuild()
        {
            int generation = ++_building;
            string relative = Relative();

            if (!File.Exists(_package))
            {
                _rows = new List<Row>();
                summary.Text = "This level has no sound package yet (" + relative + ").\nImport a .wav, add a soundbank or stream here, or replace a sound with Replace > For This Level Only in the Sound Editor to start one.";
                Fill();
                return;
            }

            summary.Text = relative + "\nReading...";
            Task.Factory.StartNew(() =>
            {
                string problem = null;
                List<Row> rows = null;
                WwiseFilePackageBuilder package = null;
                try
                {
                    package = WwiseFilePackageBuilder.Load(_package);
                    rows = Read(package);
                }
                catch (Exception e)
                {
                    problem = e.Message;
                }

                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (IsDisposed || generation != _building)
                            return;

                        if (problem != null)
                        {
                            _rows = new List<Row>();
                            summary.Text = relative + " can't be read: " + problem;
                            Fill();
                            return;
                        }

                        _rows = rows;
                        //Sounds changed in copies of the game's soundbanks, and those in soundbanks only this level has (made here, say)
                        int sounds = rows.Count(o => o.Kind == RowKind.BankSound && !rows.Any(b => b.Kind == RowKind.Bank && b.Own && b.Item == o.Item));
                        int own = rows.Count(o => o.Kind == RowKind.Bank && o.Own);
                        List<string> about = new List<string>();
                        if (sounds != 0) about.Add(sounds + " sound" + (sounds == 1 ? "" : "s") + " changed in them");
                        if (own != 0) about.Add(own + " of the level's own");
                        summary.Text = relative + "  -  " + LevelSoundOverride.Describe(new FileInfo(_package).Length) + "\n"
                            + package.Banks.Count + " soundbank" + (package.Banks.Count == 1 ? "" : "s") + (about.Count != 0 ? " (" + string.Join(", ", about) + ")" : "")
                            + " and " + package.Streams.Count + " stream" + (package.Streams.Count == 1 ? "" : "s")
                            + ". The game reads these instead of the base game's files of the same id, on this level only.";
                        Fill();
                    });
                }
                catch (InvalidOperationException)
                {
                }
            });
        }

        /// <summary>The package as rows: each soundbank followed by the sounds changed inside it, then the streams.</summary>
        private static List<Row> Read(WwiseFilePackageBuilder package)
        {
            WwiseSoundLibrary library = null;
            try { library = SoundPreviewLibrary.IsAvailable ? SoundPreviewLibrary.Get() : null; }
            catch { }

            string soundDirectory = SoundPreviewLibrary.SoundDirectory;
            List<Row> rows = new List<Row>();
            foreach (WwiseFilePackageBuilder.Item bank in package.Banks)
            {
                byte[] bytes = bank.Read();
                string baseFile = LevelSoundOverride.BaseBankFile(bank.Id, library);
                string name = SoundNames.BankName(bank.Id, bytes);
                Row bankRow = new Row
                {
                    Kind = RowKind.Bank,
                    Item = bank,
                    Name = name,
                    File = baseFile != null ? Path.GetFileName(baseFile) : bank.Id + ".bnk",
                    KindText = "Soundbank",
                    Language = Language(package, bank),
                    Length = bank.Length,
                    Own = baseFile == null,
                };
                rows.Add(bankRow);

                //The sounds that differ from the base game's copy of the bank - all of them, for a bank only this level has
                Dictionary<uint, KeyValuePair<long, int>> baseMedia = new Dictionary<uint, KeyValuePair<long, int>>();
                byte[] baseBytes = null;
                if (baseFile != null)
                {
                    try
                    {
                        baseBytes = File.ReadAllBytes(baseFile);
                        foreach (KeyValuePair<uint, KeyValuePair<long, int>> media in WwiseMediaInjector.MediaIn(baseBytes))
                            baseMedia[media.Key] = media.Value;
                    }
                    catch
                    {
                        baseBytes = null;
                    }
                }

                List<string> changedNames = new List<string>();
                foreach (KeyValuePair<uint, KeyValuePair<long, int>> media in WwiseMediaInjector.MediaIn(bytes))
                {
                    bankRow.Sounds.Add(media.Key);

                    KeyValuePair<long, int> original = default(KeyValuePair<long, int>);
                    bool inBase = baseBytes != null && baseMedia.TryGetValue(media.Key, out original);
                    if (inBase && Same(bytes, media.Value.Key, media.Value.Value, baseBytes, original.Key, original.Value))
                        continue;

                    byte[] audio = new byte[media.Value.Value];
                    Array.Copy(bytes, media.Value.Key, audio, 0, audio.Length);
                    string sound = (library != null ? SoundNames.Describe(library, media.Key) : null) ?? "Sound " + media.Key;
                    changedNames.Add(sound);
                    rows.Add(new Row
                    {
                        Kind = RowKind.BankSound,
                        Item = bank,
                        SourceId = media.Key,
                        Media = audio,
                        Name = "    " + sound,
                        File = media.Key + ".wem",
                        KindText = "Sound in soundbank",
                        Language = bankRow.Language,
                        Length = audio.Length,
                        OnThisLevel = baseFile == null ? "In a soundbank only this level has"
                            : inBase ? "Plays instead of the base game's audio"
                            : "Added to this level's copy of the soundbank",
                    });
                }

                bankRow.OnThisLevel = LevelSoundOverride.IsPermanent(bank.Id, soundDirectory) ? "Never read: a permanent soundbank, loaded before any level"
                    : baseFile != null ? "Read instead of DATA\\SOUND\\" + Path.GetFileName(baseFile)
                        + (changedNames.Count != 0 ? " - " + changedNames.Count + " sound" + (changedNames.Count == 1 ? "" : "s") + " changed" : baseBytes != null && bytes.Length == baseBytes.Length && bytes.SequenceEqual(baseBytes) ? " - the same as it" : "")
                    : "Only this level has it";
                bankRow.Search = (bankRow.Name + " " + bankRow.File + " " + bank.Id + " " + string.Join(" ", changedNames)).ToUpperInvariant();
            }

            foreach (WwiseFilePackageBuilder.Item stream in package.Streams)
            {
                bool shipped = soundDirectory != null && File.Exists(Path.Combine(soundDirectory, stream.Id + ".wem"));
                string name = library != null ? SoundNames.Describe(library, stream.Id) : null;
                rows.Add(new Row
                {
                    Kind = RowKind.Stream,
                    Item = stream,
                    SourceId = stream.Id,
                    Name = name ?? "Stream " + stream.Id,
                    File = stream.Id + ".wem",
                    KindText = "Stream",
                    Language = Language(package, stream),
                    Length = stream.Length,
                    OnThisLevel = shipped ? "Read instead of DATA\\SOUND\\" + stream.Id + ".wem" : "Streamed audio this level supplies",
                });
            }

            foreach (Row row in rows.Where(o => o.Search == null))
                row.Search = (row.Name + " " + row.File).ToUpperInvariant();
            return rows;
        }

        private static string Language(WwiseFilePackageBuilder package, WwiseFilePackageBuilder.Item item)
        {
            string language;
            return package.Languages.TryGetValue(item.LanguageId, out language) ? language : item.LanguageId.ToString();
        }

        private static bool Same(byte[] a, long aAt, int aLength, byte[] b, long bAt, int bLength)
        {
            if (aLength != bLength)
                return false;
            for (int i = 0; i < aLength; i++)
                if (a[aAt + i] != b[bAt + i])
                    return false;
            return true;
        }

        /// <summary>The rows matching the search: a soundbank shows when it or any sound changed in it matches, and its sounds with it.</summary>
        private void Fill()
        {
            string wanted = search.Text.Trim().ToUpperInvariant();
            HashSet<string> selected = new HashSet<string>(Selected().Select(Key));

            list.BeginUpdate();
            list.Items.Clear();
            foreach (Row row in _rows)
            {
                if (wanted.Length != 0 && row.Search.IndexOf(wanted, StringComparison.Ordinal) < 0)
                {
                    //A sound in a soundbank stays with its bank when the bank matched by name
                    Row bank = row.Kind == RowKind.BankSound ? _rows.FirstOrDefault(o => o.Kind == RowKind.Bank && o.Item == row.Item) : null;
                    if (bank == null || (bank.Name + " " + bank.File).ToUpperInvariant().IndexOf(wanted, StringComparison.Ordinal) < 0)
                        continue;
                }

                ListViewItem item = new ListViewItem(new[] { row.Name, row.File, row.KindText, LevelSoundOverride.Describe(row.Length), row.OnThisLevel });
                item.Tag = row;
                item.ToolTipText = row.Kind == RowKind.Bank ? row.Name : row.Name.Trim() + " (" + row.SourceId + ")";
                if (selected.Contains(Key(row)))
                    item.Selected = true;
                list.Items.Add(item);
            }
            list.EndUpdate();
            UpdateButtons();
        }

        /// <summary>A row by what it is rather than by the object, so the selection survives the list being read again.</summary>
        private static string Key(Row row)
        {
            return row.Kind + ":" + row.Item.Id + ":" + row.SourceId;
        }

        private List<Row> Selected()
        {
            return list.SelectedItems.Cast<ListViewItem>().Select(o => (Row)o.Tag).ToList();
        }

        private void UpdateButtons()
        {
            int selected = list.SelectedItems.Count;
            removeBtn.Enabled = selected != 0;
            exportBtn.Enabled = selected == 1;
            bankBtn.Text = OwnBankSelected() != null ? "Edit Soundbank..." : "New Soundbank...";
        }

        /// <summary>The soundbank of the level's own selected (or the one a selected sound is in), if one is.</summary>
        private Row OwnBankSelected()
        {
            List<Row> selected = Selected();
            if (selected.Count != 1)
                return null;
            Row row = selected[0];
            if (row.Kind == RowKind.BankSound)
                row = _rows.FirstOrDefault(o => o.Kind == RowKind.Bank && o.Item == row.Item);
            return row != null && row.Kind == RowKind.Bank && row.Own ? row : null;
        }

        /// <summary>Make a soundbank of the level's own, or - with one selected - add events to it or take them out.</summary>
        private void EditBank()
        {
            Row bank = OwnBankSelected();
            using (LevelSoundBankDialog dialog = new LevelSoundBankDialog(_package, bank == null ? (uint?)null : bank.Item.Id, bank == null ? null : bank.Name))
                dialog.ShowDialog(this);
        }

        /// <summary>
        /// Make game audio from a .wav. Starts on the selected stream or sound; with a soundbank selected, on that soundbank's
        /// sounds.
        /// </summary>
        private void ImportWav()
        {
            List<Row> selected = Selected();
            Row row = selected.Count == 1 ? selected[0] : null;

            uint? sourceId = row != null && row.Kind != RowKind.Bank ? row.SourceId : (uint?)null;
            List<uint> bankSounds = row != null && row.Kind == RowKind.Bank && row.Sounds.Count != 0 ? row.Sounds : null;
            using (LevelSoundImportDialog dialog = new LevelSoundImportDialog(_package, sourceId, bankSounds, row != null ? row.Name : null))
                dialog.ShowDialog(this);
        }

        private void AddFiles()
        {
            string[] files;
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Wwise Soundbanks and Streams|*.bnk;*.wem|Wwise Soundbank|*.bnk|Wwise Stream (named for its id)|*.wem";
                dialog.Title = "Add to the level's sound package";
                dialog.Multiselect = true;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                files = dialog.FileNames;
            }

            List<string> problems = new List<string>();
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                foreach (string file in files)
                {
                    LevelSoundOverride.Plan plan = LevelSoundOverride.Add(_package, file, true);
                    if (!plan.Ok)
                        problems.Add(Path.GetFileName(file) + ": " + plan.Problem);
                }
            }
            finally { Cursor.Current = Cursors.Default; }

            LevelSoundOverride.RaiseChanged();
            if (problems.Count != 0)
                MessageBox.Show(this, "Not everything could be added:\n\n" + string.Join("\n", problems), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void Export()
        {
            List<Row> selected = Selected();
            if (selected.Count != 1)
                return;

            Row row = selected[0];
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                bool bank = row.Kind == RowKind.Bank;
                dialog.Filter = bank ? "Wwise Soundbank|*.bnk" : "Wwise Audio|*.wem|Wave File|*.wav";
                dialog.FileName = bank ? Path.GetFileNameWithoutExtension(row.File) + ".bnk" : row.File;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    byte[] bytes = row.Kind == RowKind.BankSound ? row.Media : row.Item.Read();
                    if (!bank && string.Equals(Path.GetExtension(dialog.FileName), ".wav", StringComparison.OrdinalIgnoreCase))
                    {
                        using (DecodedAudio audio = WwiseAudioDecoder.Decode(bytes))
                            bytes = audio.ToWave();
                    }
                    File.WriteAllBytes(dialog.FileName, bytes);
                }
                catch (Exception e)
                {
                    MessageBox.Show(this, "Could not save it:\n\n" + e.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void RemoveSelected()
        {
            List<Row> selected = Selected();
            if (selected.Count == 0)
                return;

            //A sound in a soundbank that is itself being taken out goes with it
            List<Row> files = selected.Where(o => o.Kind != RowKind.BankSound).ToList();
            List<Row> sounds = selected.Where(o => o.Kind == RowKind.BankSound && !files.Any(f => f.Kind == RowKind.Bank && f.Item == o.Item)).ToList();

            List<string> what = new List<string>();
            if (files.Count != 0)
                what.Add("Take " + (files.Count == 1 ? files[0].Name.Trim() : files.Count + " files") + " out of the level's sound package? On this level the game will read the base game's "
                    + (files.Count == 1 ? "file" : "files") + " of the same id instead - or nothing, for a file only this level has."
                    + (files.Any(o => o.Kind == RowKind.Bank && o.Own) ? " A soundbank only this level has takes its events out of the level's sound event table with it." : ""));
            if (sounds.Count != 0)
                what.Add("Put the base game's audio back in place of " + (sounds.Count == 1 ? sounds[0].Name.Trim() : sounds.Count + " sounds") + " in the level's copy of "
                    + (sounds.Select(o => o.Item).Distinct().Count() == 1 ? "its soundbank" : "their soundbanks") + "? A soundbank that ends up the same as the base game's is taken out of the package.");
            if (MessageBox.Show(this, string.Join("\n\n", what) + "\n\nThe level's package is the only copy of anything changed in it.", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            List<string> problems = new List<string>();
            List<string> notes = new List<string>();
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                if (files.Count != 0)
                {
                    LevelSoundOverride.Plan plan = LevelSoundOverride.Remove(_package, files.Select(o => new KeyValuePair<bool, uint>(o.Kind == RowKind.Bank, o.Item.Id)), true);
                    if (!plan.Ok)
                        problems.Add(plan.Problem);
                }
                if (sounds.Count != 0)
                {
                    LevelSoundOverride.Plan plan = LevelSoundOverride.Revert(_package, SoundPreviewLibrary.Get(), sounds.Select(o => o.SourceId), true);
                    if (!plan.Ok)
                        problems.Add(plan.Problem);
                    else
                        notes.AddRange(plan.Notes.Where(o => o.StartsWith("Kept", StringComparison.Ordinal)));
                }
            }
            finally { Cursor.Current = Cursors.Default; }

            LevelSoundOverride.RaiseChanged();
            if (problems.Count != 0 || notes.Count != 0)
                MessageBox.Show(this, string.Join("\n\n", problems.Concat(notes)), Text, MessageBoxButtons.OK, problems.Count != 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
    }
}

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
    /// Makes game audio from a .wav for the open level's sound package (WORLD/LEVEL_SOUND_OVERRIDE.PCK), so a level's sounds
    /// can be changed without the Wwise authoring tool. Pick the .wav and the sound it replaces - listed by the sound events
    /// that play it, searchable by name or id - and it is encoded the way the game's own audio is (Wwise Vorbis, at a setup
    /// the game's decoder knows), laid out like the sound it replaces (its channel count and sample rate, unless asked
    /// otherwise), and put where the game reads that sound from on this level: a stream of its own in the package for
    /// streamed audio, or the package's copy of the soundbank that holds it, the audio swapped inside with the bank's
    /// directory and object hierarchy kept pointing at it. Or the .wav can go in as a stream of its own, by id, for a
    /// soundbank of the user's own (added beside it) to stream.
    ///
    /// Nothing is written until Import: what it will do - the format, the size against the original's, which files of the
    /// package change - is worked out as the choices change, and both the original and the new audio can be played first
    /// (the new one decoded from the very .wem that will go in).
    /// </summary>
    public partial class LevelSoundImportDialog : BaseWindow
    {
        private readonly string _package;
        private uint? _preselect;
        private readonly HashSet<uint> _bankMedia;

        private WwiseSoundLibrary _library;
        private List<Candidate> _candidates = new List<Candidate>();
        private SoundImport.Reading _reading;
        private int _listing, _examining;
        private bool _filling;

        private WavePlayer _player;
        private DecodedAudio _playing;

        //The .wav's own layout, from its header, for the "keep" choices
        private int _wavChannels, _wavRate;

        internal sealed class Candidate
        {
            public uint SourceId;
            public string Name;
            public List<string> Events;
            public string KeptIn;
            public string Others;
            public string Search;
            public ListViewItem Item;
        }

        /// <param name="package">The open level's sound package, which need not exist yet.</param>
        /// <param name="preselect">A sound to start on, by source id.</param>
        /// <param name="bankMedia">A soundbank's audio, by source id, to list on its own at first (null for everything).</param>
        /// <param name="bankName">That soundbank's name, for the box that narrows the list to it.</param>
        public LevelSoundImportDialog(string package, uint? preselect, IEnumerable<uint> bankMedia, string bankName)
        {
            InitializeComponent();
            _package = package;
            _preselect = preselect;
            _bankMedia = bankMedia == null ? null : new HashSet<uint>(bankMedia);

            Icon = SharedFormIcon.Icon;
            BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/sound-events/#import-wav");

            if (_bankMedia != null)
            {
                onlyBank.Text = "Only " + bankName + "'s sounds";
                onlyBank.Checked = true;
                onlyBank.Visible = true;
            }

            channels.Items.AddRange(new object[] { "", "", "Mono", "Stereo" });
            rate.Items.AddRange(new object[] { "", "", "48000 Hz", "44100 Hz", "32000 Hz", "24000 Hz", "22050 Hz" });
            channels.SelectedIndex = 0;
            rate.SelectedIndex = 0;
            UpdateLayoutChoices(null);

            //Wired after the choices above are filled in, which are not changes
            onlyBank.CheckedChanged += onlyBank_CheckedChanged;
            channels.SelectedIndexChanged += Option_Changed;
            rate.SelectedIndexChanged += Option_Changed;

            AcceptDrops(this);
            UpdateEnabled();
        }

        private void LevelSoundImportDialog_Load(object sender, EventArgs e) => LoadCandidates();

        private void LevelSoundImportDialog_FormClosed(object sender, FormClosedEventArgs e)
        {
            _listing++;
            _examining++;
            StopPlaying();
            debounce.Dispose();
            tips.Dispose();
        }

        private void browseBtn_Click(object sender, EventArgs e) => Browse();
        private void search_TextChanged(object sender, EventArgs e) => Fill();
        private void onlyBank_CheckedChanged(object sender, EventArgs e) => Fill();

        private void list_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_filling)
                return;
            UpdateLayoutChoices(SelectedOriginal());
            UpdateEnabled();
            Changed();
        }

        //The two choices sit in different rows of the layout, so they don't group themselves: each clears the other
        private void replaceRadio_CheckedChanged(object sender, EventArgs e)
        {
            if (replaceRadio.Checked) streamRadio.Checked = false;
            UpdateLayoutChoices(SelectedOriginal());
            UpdateEnabled();
            Changed();
        }

        private void streamRadio_CheckedChanged(object sender, EventArgs e) { if (streamRadio.Checked) replaceRadio.Checked = false; }
        private void streamId_TextChanged(object sender, EventArgs e) { if (streamRadio.Checked) Changed(); }
        private void streamId_Enter(object sender, EventArgs e) => streamRadio.Checked = true;

        /// <summary>Channels, sample rate, quality, gain or normalise changed.</summary>
        private void Option_Changed(object sender, EventArgs e) => Changed();

        private void playOriginalBtn_Click(object sender, EventArgs e) => PlayOriginal();
        private void playNewBtn_Click(object sender, EventArgs e) => PlayNew();
        private void stopBtn_Click(object sender, EventArgs e) => StopPlaying();
        private void importBtn_Click(object sender, EventArgs e) => Import();
        private void cancelBtn_Click(object sender, EventArgs e) => Close();

        private void debounce_Tick(object sender, EventArgs e)
        {
            debounce.Stop();
            Examine();
        }

        /// <summary>A .wav dropped anywhere on the window is the one to import.</summary>
        private void AcceptDrops(Control control)
        {
            control.AllowDrop = true;
            control.DragEnter += (s, e) => e.Effect = WavIn(e.Data) != null ? DragDropEffects.Copy : DragDropEffects.None;
            control.DragDrop += (s, e) =>
            {
                string dropped = WavIn(e.Data);
                if (dropped != null)
                    SetWav(dropped);
            };
            foreach (Control child in control.Controls)
                AcceptDrops(child);
        }

        #region LAYOUT

        /// <summary>The channel and rate choices, naming the layout each "match" or "keep" comes to for this .wav and sound.</summary>
        private void UpdateLayoutChoices(SoundImport.MediaLayout original)
        {
            int channelChoice = channels.SelectedIndex, rateChoice = rate.SelectedIndex;
            bool hasOriginal = original != null && replaceRadio.Checked;

            channels.Items[0] = "Match the sound it replaces" + (hasOriginal ? " (" + (original.Channels == 1 ? "mono" : "stereo") + ")" : "");
            channels.Items[1] = "Keep the .wav's" + (_wavChannels > 0 ? " (" + (_wavChannels == 1 ? "mono" : _wavChannels == 2 ? "stereo" : _wavChannels + " channels, mixed to stereo") + ")" : "");
            rate.Items[0] = "Match the sound it replaces" + (hasOriginal ? " (" + original.SampleRate + " Hz)" : "");
            rate.Items[1] = "Keep the .wav's" + (_wavRate > 0 ? " (" + _wavRate + " Hz)" : "");

            //Reassigning an item can drop the selection: put it back
            if (channels.SelectedIndex != channelChoice) channels.SelectedIndex = Math.Max(0, channelChoice);
            if (rate.SelectedIndex != rateChoice) rate.SelectedIndex = Math.Max(0, rateChoice);
        }

        private void UpdateEnabled()
        {
            bool replacing = replaceRadio.Checked;
            list.Enabled = replacing;
            search.Enabled = replacing;
            onlyBank.Enabled = replacing;
            streamId.Enabled = true;
            playOriginalBtn.Enabled = replacing && Selected() != null;
        }

        #endregion

        #region THE SOUNDS

        /// <summary>Every sound the level's events play, named by them - built off the UI thread from the sound index.</summary>
        private void LoadCandidates()
        {
            summary.Text = SoundPreviewLibrary.IsReady ? "Listing the game's sounds..." : "Indexing the game's soundbanks...";
            int generation = ++_listing;
            Task.Factory.StartNew(() =>
            {
                try
                {
                    WwiseSoundLibrary library = SoundPreviewLibrary.Get();
                    List<Candidate> candidates = Candidates(library, _package);

                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (IsDisposed || generation != _listing)
                            return;
                        _library = library;
                        _candidates = candidates;
                        Fill();
                        Changed();
                    });
                }
                catch (Exception e)
                {
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate { if (!IsDisposed) summary.Text = "The game's sounds could not be listed: " + e.Message; });
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }
            });
        }

        /// <summary>
        /// Every sound the level's events play, named by them and sorted - built off the UI thread from the sound index. Shared with
        /// the New Soundbank window, which lists the same sounds to pick one to sound like.
        /// </summary>
        internal static List<Candidate> Candidates(WwiseSoundLibrary library, string package)
        {
            Dictionary<uint, List<SoundNames.Use>> named = SoundNames.All(library);
            HashSet<string> permanent = LevelSoundOverride.PermanentBankFiles(library);
            string sound = library.SoundDirectory == null ? null : Path.GetFullPath(library.SoundDirectory);
            Dictionary<string, string> bankNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            List<Candidate> candidates = new List<Candidate>(named.Count);
            foreach (KeyValuePair<uint, List<SoundNames.Use>> source in named)
            {
                WwiseMediaLocation media = library.MediaFor(source.Key);
                if (media == null)
                    continue;

                List<string> events = source.Value.Select(o => o.Event).Distinct().ToList();
                Candidate candidate = new Candidate
                {
                    SourceId = source.Key,
                    Name = source.Value[0].ToString(),
                    Events = events,
                    Others = events.Count == 1 ? "" : string.Join(", ", events.Skip(1).Take(4)) + (events.Count > 5 ? "..." : ""),
                    KeptIn = KeptIn(library, package, source.Key, media, permanent, sound, bankNames),
                };
                candidate.Search = (string.Join(" ", events) + " " + source.Key + " " + candidate.KeptIn).ToUpperInvariant();
                candidates.Add(candidate);
            }
            candidates.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return candidates;
        }

        /// <summary>Where the game reads a sound from - and, where it can't be replaced on one level, why.</summary>
        private static string KeptIn(WwiseSoundLibrary library, string package, uint sourceId, WwiseMediaLocation media, HashSet<string> permanent, string sound, Dictionary<string, string> bankNames)
        {
            string file = Path.GetFullPath(media.File);
            if (string.Equals(file, Path.GetFullPath(package), StringComparison.OrdinalIgnoreCase))
                return "This level's package";

            //Any copy in a permanent bank wins over the level's, whichever copy plays elsewhere
            foreach (WwiseMediaLocation copy in library.AllCopies(sourceId))
                if (permanent.Contains(Path.GetFullPath(copy.File)))
                    return Path.GetFileNameWithoutExtension(copy.File) + " (permanent: every level only)";

            string extension = Path.GetExtension(file).ToLowerInvariant();
            if (extension == ".pck" || (sound != null && !string.Equals(Path.GetDirectoryName(file), sound, StringComparison.OrdinalIgnoreCase)))
                return "Voiced (" + media.Origin + "): every level only";
            if (extension == ".wem")
                return "Stream";

            //The bank by the name the level's sound data gives it, read from its header once per bank
            string name;
            if (!bankNames.TryGetValue(file, out name))
            {
                try
                {
                    byte[] header = new byte[16];
                    using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                        stream.Read(header, 0, header.Length);
                    name = SoundNames.BankName(LevelSoundOverride.BankId(header), null);
                }
                catch
                {
                    name = Path.GetFileNameWithoutExtension(file);
                }
                bankNames[file] = name;
            }
            return name + " (soundbank)";
        }

        private void Fill()
        {
            string wanted = search.Text.Trim().ToUpperInvariant();
            bool narrow = _bankMedia != null && onlyBank.Checked;
            Candidate before = Selected();
            uint? keep = _preselect ?? (before != null ? before.SourceId : (uint?)null);

            _filling = true;
            list.BeginUpdate();
            list.Items.Clear();
            List<ListViewItem> items = new List<ListViewItem>();
            foreach (Candidate candidate in _candidates)
            {
                if (narrow && !_bankMedia.Contains(candidate.SourceId))
                    continue;
                if (wanted.Length != 0 && candidate.Search.IndexOf(wanted, StringComparison.Ordinal) < 0)
                    continue;

                if (candidate.Item == null)
                    candidate.Item = new ListViewItem(new[] { candidate.Name, candidate.SourceId.ToString(), candidate.KeptIn, candidate.Others }) { Tag = candidate };
                items.Add(candidate.Item);
            }
            list.Items.AddRange(items.ToArray());

            if (keep != null)
            {
                ListViewItem found = items.FirstOrDefault(o => ((Candidate)o.Tag).SourceId == keep.Value);
                if (found != null)
                {
                    found.Selected = true;
                    found.Focused = true;
                    found.EnsureVisible();
                }
            }
            list.EndUpdate();
            _filling = false;

            _preselect = null;
            UpdateEnabled();

            //Narrowing the list by a search leaves the sound picked alone; only a different pick needs working out again
            if (Selected() != before)
            {
                UpdateLayoutChoices(SelectedOriginal());
                Changed();
            }
        }

        private Candidate Selected()
        {
            return list.SelectedItems.Count == 0 ? null : list.SelectedItems[0].Tag as Candidate;
        }

        private SoundImport.MediaLayout SelectedOriginal()
        {
            Candidate candidate = Selected();
            if (candidate == null || _library == null)
                return null;

            try
            {
                return SoundImport.Layout(WwiseSoundLibrary.ReadMedia(_library.MediaFor(candidate.SourceId)));
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region THE WAV

        private void Browse()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Wave File|*.wav";
                dialog.Title = "Import audio";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                SetWav(dialog.FileName);
            }
        }

        private static string WavIn(IDataObject data)
        {
            string[] files = data == null ? null : data.GetData(DataFormats.FileDrop) as string[];
            return files != null && files.Length == 1 && string.Equals(Path.GetExtension(files[0]), ".wav", StringComparison.OrdinalIgnoreCase) ? files[0] : null;
        }

        /// <summary>
        /// Take a .wav, read its layout from its header, and - when nothing is picked yet - pick the sound it is named for:
        /// a file named for a source id or for a sound event goes to that sound.
        /// </summary>
        public void SetWav(string file)
        {
            wavPath.Text = file;
            ReadWavHeader(file);

            string name = Path.GetFileNameWithoutExtension(file);
            if (Selected() == null && replaceRadio.Checked)
            {
                uint id;
                Candidate match = uint.TryParse(name, out id)
                    ? _candidates.FirstOrDefault(o => o.SourceId == id)
                    : _candidates.FirstOrDefault(o => o.Events.Contains(name, StringComparer.OrdinalIgnoreCase));
                if (match != null)
                {
                    search.Text = "";
                    _preselect = match.SourceId;
                    Fill();
                }
            }

            UpdateLayoutChoices(SelectedOriginal());
            Changed();
        }

        private void ReadWavHeader(string file)
        {
            _wavChannels = _wavRate = 0;
            try
            {
                byte[] header = new byte[4096];
                int read;
                using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                    read = stream.Read(header, 0, header.Length);

                int position = 12;
                while (position + 24 <= read)
                {
                    string tag = System.Text.Encoding.ASCII.GetString(header, position, 4);
                    int size = BitConverter.ToInt32(header, position + 4);
                    if (tag == "fmt ")
                    {
                        _wavChannels = BitConverter.ToUInt16(header, position + 10);
                        _wavRate = (int)BitConverter.ToUInt32(header, position + 12);
                        return;
                    }
                    if (size < 0)
                        return;
                    position += 8 + size + (size & 1);
                }
            }
            catch
            {
            }
        }

        #endregion

        #region WORKING IT OUT

        private void Changed()
        {
            _reading = null;
            importBtn.Enabled = false;
            playNewBtn.Enabled = false;
            debounce.Stop();
            debounce.Start();
        }

        private SoundImport.Options Options()
        {
            SoundImport.Options options = new SoundImport.Options
            {
                Quality = (float)quality.Value,
                GainDb = (double)gain.Value,
                NormalizeDb = normalise.Checked ? (double?)-1.0 : null,
            };

            switch (channels.SelectedIndex)
            {
                case 0: options.MatchOriginalChannels = true; break;
                case 2: options.Channels = 1; break;
                case 3: options.Channels = 2; break;
            }
            switch (rate.SelectedIndex)
            {
                case 0: options.MatchOriginalRate = true; break;
                case 1: break;
                default: options.SampleRate = int.Parse(((string)rate.SelectedItem).Split(' ')[0]); break;
            }
            return options;
        }

        /// <summary>Encode the .wav for the chosen sound off the UI thread, and say what importing it would do.</summary>
        private void Examine()
        {
            int generation = ++_examining;
            string file = wavPath.Text;
            if (string.IsNullOrEmpty(file))
            {
                summary.Text = "Choose a .wav to import (Browse, or drop one on this window)" + (_candidates.Count == 0 ? "." : ", and the sound it replaces.");
                return;
            }
            if (!File.Exists(file))
            {
                summary.Text = file + " doesn't exist.";
                return;
            }

            SoundImport.Options options = Options();
            WwiseSoundLibrary library = _library;
            Candidate target = Selected();
            uint stream = 0;
            if (replaceRadio.Checked)
            {
                if (target == null || library == null)
                {
                    summary.Text = library == null ? "Listing the game's sounds..." : "Choose the sound the .wav replaces.";
                    return;
                }
            }
            else if (!uint.TryParse(streamId.Text.Trim(), out stream) || stream == 0)
            {
                summary.Text = "Type the stream's id: the number a soundbank of your own streams this audio by.";
                return;
            }

            summary.Text = "Encoding...";
            bool replacing = replaceRadio.Checked;
            Task.Factory.StartNew(() =>
            {
                SoundImport.Reading reading;
                try
                {
                    reading = replacing
                        ? SoundImport.ReadForLevel(file, target.SourceId, library.MediaFor(target.SourceId), library, _package, options)
                        : SoundImport.ReadForLevelStream(file, stream, _package, options);
                }
                catch (Exception e)
                {
                    reading = new SoundImport.Reading { Problem = e.Message };
                }

                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (IsDisposed || generation != _examining)
                            return;

                        _reading = reading;
                        string text = reading.Ok ? SoundImport.Describe(reading) : "That can't be imported: " + reading.Problem;
                        summary.Text = text.Replace("\r\n", "\n").Replace("\n", "\r\n"); //the notes mix both line endings
                        importBtn.Enabled = reading.Ok;
                        playNewBtn.Enabled = reading.Wem != null;
                    });
                }
                catch (InvalidOperationException)
                {
                }
            });
        }

        private void Import()
        {
            SoundImport.Reading reading = _reading;
            if (reading == null || !reading.Ok)
                return;

            StopPlaying();

            //One piece of audio several events play changes for all of them: ask before replacing it
            if (replaceRadio.Checked && Selected() != null)
            {
                List<string> sharing = SoundNames.Playing(_library, Selected().SourceId).Select(o => o.Event).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (sharing.Count > 1 && MessageBox.Show(this, sharing.Count + " sound events play this same audio:\n\n" + string.Join("\n", sharing.Take(12)) + (sharing.Count > 12 ? "\n... and " + (sharing.Count - 12) + " more" : "")
                    + "\n\nIt is one piece of audio, so on this level every one of them will play the new sound. Replace it anyway?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }

            LevelSoundOverride.Plan written;
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                if (replaceRadio.Checked)
                {
                    Candidate target = Selected();
                    if (target == null)
                        return;
                    written = LevelSoundOverride.Replace(_package, _library, new Dictionary<uint, byte[]> { { target.SourceId, reading.Wem } }, true);
                }
                else
                    written = LevelSoundOverride.AddStream(_package, uint.Parse(streamId.Text.Trim()), reading.Wem, true);
            }
            finally { Cursor.Current = Cursors.Default; }

            LevelSoundOverride.RaiseChanged();
            if (!written.Ok)
            {
                MessageBox.Show(this, "The sound could not be imported:\n\n" + written.Problem, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        #endregion

        #region LISTENING

        private void PlayOriginal()
        {
            Candidate candidate = Selected();
            if (candidate == null || _library == null)
                return;

            try
            {
                Play(WwiseSoundLibrary.ReadMedia(_library.MediaFor(candidate.SourceId)));
            }
            catch (Exception e)
            {
                MessageBox.Show(this, "That sound can't be played: " + e.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void PlayNew()
        {
            if (_reading == null || _reading.Wem == null)
                return;

            try
            {
                Play(_reading.Wem);
            }
            catch (Exception e)
            {
                MessageBox.Show(this, "The new audio can't be played: " + e.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void Play(byte[] wem)
        {
            StopPlaying();
            _playing = WwiseAudioDecoder.Decode(wem);
            _player = new WavePlayer(_playing);
            _player.PlaybackEnded += (s, e) =>
            {
                try { BeginInvoke((MethodInvoker)delegate { if (!IsDisposed) stopBtn.Enabled = false; }); }
                catch (InvalidOperationException) { }
            };
            _player.Play();
            stopBtn.Enabled = true;
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

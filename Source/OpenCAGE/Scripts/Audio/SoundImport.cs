using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// Replaces a sound in the game with audio from a .wav.
    ///
    /// The work splits in two: turning the audio into the exact form the game's decoder expects, and
    /// putting it where that sound's audio already lives. Both are checked before anything is written,
    /// so a file that cannot be imported says so rather than leaving a half-edited soundbank.
    /// </summary>
    internal static class SoundImport
    {
        public sealed class Options
        {
            /// <summary>0 to 1. Raised automatically if the game's decoder isn't set up for it.</summary>
            public float Quality = 0.6f;

            /// <summary>Seconds cut off the start and the end of the .wav before anything else.</summary>
            public double TrimStart = 0, TrimEnd = 0;

            /// <summary>Decibels added to the audio (after trimming); negative makes it quieter.</summary>
            public double GainDb = 0;

            /// <summary>Scale the audio so its loudest sample peaks at this many dBFS (e.g. -1), or null to leave it. Applied after the gain.</summary>
            public double? NormalizeDb = null;

            /// <summary>
            /// 1 or 2 to mix the audio to mono or stereo; 0 to keep the .wav's channels (or, with <see cref="MatchOriginalChannels"/>,
            /// the sound's it replaces). Anything wider than stereo is always mixed down: the game's codebooks only cover two.
            /// </summary>
            public int Channels = 0;

            /// <summary>
            /// The sample rate to encode at; 0 to keep the .wav's (or, with <see cref="MatchOriginalRate"/>, the sound's it
            /// replaces). A rate the game's decoder isn't set up for at that channel count moves to the nearest one it is.
            /// </summary>
            public int SampleRate = 0;

            /// <summary>With <see cref="Channels"/> 0: mix to the channel count of the sound it replaces (mono or stereo).</summary>
            public bool MatchOriginalChannels = false;

            /// <summary>With <see cref="SampleRate"/> 0: resample to the rate of the sound it replaces.</summary>
            public bool MatchOriginalRate = false;
        }

        /// <summary>How a piece of the game's audio is laid out, read from its header.</summary>
        public sealed class MediaLayout
        {
            public ushort Format;
            public int Channels;
            public int SampleRate;

            /// <summary>Seconds, or 0 when the header doesn't say.</summary>
            public double Duration;

            /// <summary>A loop region narrower than the whole sound, in seconds, from its smpl chunk; LoopEnd is 0 for none.</summary>
            public double LoopStart, LoopEnd;

            /// <summary>Markers (a cue chunk) the sound carries for the game to be told about as it plays.</summary>
            public int Markers;

            public string Codec
            {
                get { return Format == WwiseAdpcmReader.FormatAdpcm ? "Wwise ADPCM" : Format == WwisePcmReader.FormatPcm ? "PCM" : "Wwise Vorbis"; }
            }

            public override string ToString()
            {
                string channels = Channels == 1 ? "mono" : Channels == 2 ? "stereo" : Channels == 6 ? "5.1" : Channels + " channels";
                return channels + ", " + SampleRate + " Hz" + (Duration > 0 ? ", " + Duration.ToString("0.0") + " s" : "");
            }
        }

        /// <summary>
        /// The layout of a .wem from its header - which works on the opening of a streamed sound too (the head a soundbank
        /// prefetches), since everything but the audio comes first. Null when it isn't one.
        /// </summary>
        public static MediaLayout Layout(byte[] wem)
        {
            try
            {
                if (wem == null || wem.Length < 12 || System.Text.Encoding.ASCII.GetString(wem, 0, 4) != "RIFF")
                    return null;

                MediaLayout layout = new MediaLayout();
                int formatAt = -1, formatLength = 0, dataLength = 0;
                int smplAt = -1, smplLength = 0;
                int position = 12;
                while (position + 8 <= wem.Length)
                {
                    string tag = System.Text.Encoding.ASCII.GetString(wem, position, 4);
                    int size = BitConverter.ToInt32(wem, position + 4);
                    if (size < 0)
                        break;

                    if (tag == "fmt " && position + 8 + size <= wem.Length) { formatAt = position + 8; formatLength = size; }
                    else if (tag == "smpl" && position + 8 + size <= wem.Length) { smplAt = position + 8; smplLength = size; }
                    else if (tag == "cue " && position + 12 <= wem.Length) layout.Markers = BitConverter.ToInt32(wem, position + 8);
                    else if (tag == "data") { dataLength = size; break; }

                    position += 8 + size + (size & 1);
                }

                if (formatAt < 0 || formatLength < 16)
                    return null;

                layout.Format = BitConverter.ToUInt16(wem, formatAt);
                layout.Channels = BitConverter.ToUInt16(wem, formatAt + 2);
                layout.SampleRate = (int)BitConverter.ToUInt32(wem, formatAt + 4);
                if (layout.SampleRate <= 0)
                    return null;

                //Vorbis keeps its length in its own header; PCM's follows from the data's size
                if (layout.Format == WwiseVorbisConverter.FormatVorbis && formatLength >= 0x18 + 4)
                    layout.Duration = BitConverter.ToUInt32(wem, formatAt + 0x18) / (double)layout.SampleRate;
                else if (layout.Format == WwisePcmReader.FormatPcm && layout.Channels > 0)
                    layout.Duration = dataLength / (2.0 * layout.Channels) / layout.SampleRate;

                //One loop: its start and end, in samples, sit 8 and 12 bytes into the first loop record. Most of the game's
                //loops cover the whole sound (to its last sample or the one before) and say nothing a missing loop wouldn't
                if (smplAt >= 0 && smplLength >= 36 + 24 && BitConverter.ToInt32(wem, smplAt + 28) > 0)
                {
                    uint first = BitConverter.ToUInt32(wem, smplAt + 36 + 8), last = BitConverter.ToUInt32(wem, smplAt + 36 + 12);
                    double frames = layout.Duration * layout.SampleRate;
                    if (first > 0 || (frames > 0 && last + 2 < frames - 0.5))
                    {
                        layout.LoopStart = first / (double)layout.SampleRate;
                        layout.LoopEnd = (last + 1) / (double)layout.SampleRate;
                    }
                }

                return layout;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Trim, gain and normalise the samples as the options ask, in place of the originals. What was done is
        /// noted; a trim that leaves nothing, or a gain that clips, says so.
        /// </summary>
        private static void Shape(WaveFile.Audio audio, Options options, Reading reading)
        {
            if (options.TrimStart < 0 || options.TrimEnd < 0)
                throw new InvalidDataException("A trim can't be negative.");
            if (options.TrimStart > 0 || options.TrimEnd > 0)
            {
                int from = (int)Math.Round(options.TrimStart * audio.SampleRate);
                int to = audio.Frames - (int)Math.Round(options.TrimEnd * audio.SampleRate);
                if (to - from < 1)
                    throw new InvalidDataException("Trimming " + options.TrimStart + " s from the start and " + options.TrimEnd + " s from the end leaves nothing of a " + audio.Duration.ToString("0.###") + " s sound.");
                for (int c = 0; c < audio.Channels; c++)
                {
                    float[] kept = new float[to - from];
                    Array.Copy(audio.Samples[c], from, kept, 0, kept.Length);
                    audio.Samples[c] = kept;
                }
                reading.Notes.Add("Trimmed to " + audio.Duration.ToString("0.###") + " s.");

                //A loop region moves with the audio, and goes if the trim cut into it
                if (audio.LoopStart >= 0)
                {
                    audio.LoopStart -= from;
                    audio.LoopEnd = Math.Min(audio.LoopEnd - from, to - from - 1);
                    if (audio.LoopStart < 0 || audio.LoopEnd <= audio.LoopStart)
                    {
                        audio.LoopStart = audio.LoopEnd = -1;
                        reading.Notes.Add("The trim cuts into the .wav's loop region, so its loop points are dropped.");
                    }
                }
            }

            double scale = options.GainDb == 0 ? 1 : Math.Pow(10, options.GainDb / 20.0);
            if (options.NormalizeDb != null)
            {
                float peak = 0;
                foreach (float[] channel in audio.Samples)
                    foreach (float sample in channel)
                        peak = Math.Max(peak, Math.Abs(sample));
                if (peak <= 0)
                    reading.Notes.Add("The audio is silent, so it could not be normalised.");
                else
                {
                    scale = Math.Pow(10, options.NormalizeDb.Value / 20.0) / peak;
                    reading.Notes.Add("Normalised: its peak was " + (20 * Math.Log10(peak)).ToString("0.0") + " dBFS, now " + options.NormalizeDb.Value.ToString("0.0") + " dBFS (scaled by " + (20 * Math.Log10(scale)).ToString("+0.0;-0.0") + " dB)"
                        + (options.GainDb == 0 ? "." : "; the gain was folded into it."));
                }
            }
            else if (options.GainDb != 0)
                reading.Notes.Add("Gain " + options.GainDb.ToString("+0.0;-0.0") + " dB applied.");
            if (scale == 1)
                return;

            int clipped = 0;
            foreach (float[] channel in audio.Samples)
                for (int i = 0; i < channel.Length; i++)
                {
                    float value = (float)(channel[i] * scale);
                    if (value > 1f) { value = 1f; clipped++; }
                    else if (value < -1f) { value = -1f; clipped++; }
                    channel[i] = value;
                }
            if (clipped != 0)
                reading.Notes.Add(clipped + " samples clipped at full scale: lower the gain (or normalise to -1 dBFS) if it crackles.");
        }

        public sealed class Reading
        {
            public byte[] Wem;

            public int Channels;
            public int SampleRate;
            public double Duration;
            public float Quality;

            /// <summary>The .wav as it was read, before it was mixed or resampled.</summary>
            public int SourceChannels;
            public int SourceSampleRate;

            /// <summary>The sound being replaced, as the game ships it - null when there is none (a new stream).</summary>
            public MediaLayout Original;

            /// <summary>What the audio was encoded as - always whatever the original sound used.</summary>
            public string Codec = "Wwise Vorbis";

            public int OriginalBytes;
            public int NewBytes;

            /// <summary>What the replacement would do to the file holding the sound.</summary>
            public WwiseMediaInjector.Plan Plan;

            /// <summary>Every copy of this audio the game ships, all of which get replaced.</summary>
            public IList<WwiseMediaLocation> Copies = new List<WwiseMediaLocation>();

            /// <summary>For a replacement on one level only: what it does to that level's sound package.</summary>
            public LevelSoundOverride.Plan Level;

            public List<string> Notes = new List<string>();
            public string Problem;

            public bool Ok
            {
                get { return Problem == null && Wem != null && Plan != null && Plan.Supported; }
            }
        }

        /// <summary>
        /// Read a .wav and encode it, working out what replacing <paramref name="target"/> with it would
        /// involve. Nothing is written.
        /// </summary>
        public static Reading Read(string file, WwiseMediaLocation target, Options options)
        {
            return Read(file, target, new WwiseMediaLocation[0], options);
        }

        /// <summary>
        /// As above, but replacing every copy of the audio rather than just the one that plays.
        ///
        /// A piece of audio can be shipped many times over - embedded in several banks, or embedded and
        /// streamed both. Leaving the others alone means the old sound comes back wherever the game
        /// loads a different copy, so they all go together or the import is only half done.
        /// </summary>
        public static Reading Read(string file, WwiseMediaLocation target, IList<WwiseMediaLocation> copies, Options options)
        {
            Reading reading = Encode(file, target, options);
            if (reading.Problem != null)
                return reading;

            reading.Plan = WwiseMediaInjector.Examine(target, reading.NewBytes);
            if (!reading.Plan.Supported)
            {
                reading.Problem = reading.Plan.Problem;
                return reading;
            }

            //Everywhere else the same audio is shipped, so it can be replaced there too
            List<WwiseMediaLocation> all = new List<WwiseMediaLocation> { target };
            List<string> unreachable = new List<string>();
            foreach (WwiseMediaLocation copy in copies ?? new WwiseMediaLocation[0])
            {
                if (copy == target || (copy.File == target.File && copy.Offset == target.Offset))
                    continue;

                WwiseMediaInjector.Plan plan = WwiseMediaInjector.Examine(copy, reading.NewBytes);
                if (plan.Supported)
                    all.Add(copy);
                else
                    unreachable.Add(Path.GetFileName(copy.File));
            }
            reading.Copies = all;

            if (all.Count > 1)
                reading.Notes.Add("The game ships this audio " + all.Count + " times over - "
                    + "in " + Distinct(all) + ". All of them are replaced, because the game plays whichever "
                    + "copy the level it is loading happens to carry.");
            if (unreachable.Count != 0)
                reading.Notes.Add("One copy could not be reached and is left as it was, so this sound may "
                    + "still play its original audio somewhere: " + string.Join(", ", unreachable.Distinct()) + ".");

            long growth = 0;
            foreach (WwiseMediaLocation copy in all)
                growth += WwiseMediaInjector.Examine(copy, reading.NewBytes).Growth;

            if (growth > 0)
                reading.Notes.Add("This is larger than the audio it replaces, so the files holding it grow "
                    + "by " + Describe(growth) + " in total.");

            return reading;
        }

        /// <summary>
        /// As <see cref="Read(string, WwiseMediaLocation, IList{WwiseMediaLocation}, Options)"/>, but for one level: the
        /// audio goes into the level's own sound package (<see cref="LevelSoundOverride"/>) and the game's shared sound
        /// files are left as they are. Nothing is written; <see cref="Reading.Level"/> says what would be.
        /// </summary>
        public static Reading ReadForLevel(string file, uint sourceId, WwiseMediaLocation target, WwiseSoundLibrary library, string package, Options options)
        {
            Reading reading = Encode(file, target, options);
            if (reading.Problem != null)
                return reading;

            LevelSoundOverride.Plan level = LevelSoundOverride.Replace(package, library, new Dictionary<uint, byte[]> { { sourceId, reading.Wem } }, false);
            reading.Level = level;
            if (!level.Ok)
            {
                reading.Problem = level.Problem;
                return reading;
            }

            reading.Plan = new WwiseMediaInjector.Plan
            {
                File = package,
                Kind = "this level's sound package",
                Supported = true,
                Growth = Math.Max(0, level.SizeAfter - level.SizeBefore),
            };
            reading.Notes.AddRange(level.Notes);
            return reading;
        }

        /// <summary>
        /// The .wav read, shaped and encoded to the codec of the sound it replaces, with nothing yet worked out about where
        /// it goes. <see cref="Reading.Plan"/> is left empty.
        /// </summary>
        private static Reading Encode(string file, WwiseMediaLocation target, Options options)
        {
            if (options == null)
                options = new Options();

            Reading reading = new Reading();
            reading.OriginalBytes = target == null ? 0 : target.Length;

            try
            {
                byte[] original = null;
                try { original = target == null ? null : WwiseSoundLibrary.ReadMedia(target); }
                catch { }
                reading.Original = Layout(original);

                WaveFile.Audio audio = WaveFile.Read(file);
                reading.SourceChannels = audio.Channels;
                reading.SourceSampleRate = audio.SampleRate;

                /* The game picks its decoder from the sound object in the bank, not from the media, so
                 * the replacement has to be the codec the original already was. Everything on PC (and on
                 * Mac, Linux and iOS) is Vorbis; the Switch build also stores some sounds as Wwise ADPCM
                 * and a few as PCM. The iOS and Switch builds are a later Wwise, whose Vorbis header
                 * differs slightly and which ships setups of its own - the encoder writes for whichever
                 * this game is. */
                ushort codec = ExistingCodec(original);
                bool modern = SoundPreviewLibrary.Modern;
                Conform(audio, options, reading, codec == WwiseVorbisConverter.FormatVorbis, modern);
                Shape(audio, options, reading);
                reading.Channels = audio.Channels;
                reading.SampleRate = audio.SampleRate;
                reading.Duration = audio.Duration;

                switch (codec)
                {
                    case WwiseAdpcmReader.FormatAdpcm:
                        reading.Codec = "Wwise ADPCM";
                        reading.Wem = WwiseAdpcmWriter.Build(audio.Samples, audio.SampleRate);
                        if (audio.LoopStart >= 0)
                            reading.Notes.Add("The .wav's loop points aren't carried into Wwise ADPCM: where the game loops this sound it repeats the whole of it.");
                        break;

                    case WwisePcmReader.FormatPcm:
                        reading.Codec = "PCM";
                        reading.Wem = WwiseAdpcmWriter.BuildPcm(audio.Samples, audio.SampleRate);
                        if (audio.LoopStart >= 0)
                            reading.Notes.Add("The .wav's loop points aren't carried into PCM: where the game loops this sound it repeats the whole of it.");
                        break;

                    default:
                        VorbisEncoder.Result encoded = VorbisEncoder.Encode(audio.Samples, audio.SampleRate, options.Quality, modern);
                        reading.Quality = encoded.Quality;
                        if (encoded.Note != null)
                            reading.Notes.Add(encoded.Note);

                        //The .wav's own loop region, written the way the game's looping music has it
                        if (audio.LoopStart >= 0)
                        {
                            encoded.Stream.LoopStart = audio.LoopStart;
                            encoded.Stream.LoopEnd = audio.LoopEnd;
                            reading.Notes.Add("The .wav's loop points are carried over: where the game loops this sound, it plays to "
                                + ((audio.LoopEnd + 1) / (double)audio.SampleRate).ToString("0.00") + " s and goes back to " + (audio.LoopStart / (double)audio.SampleRate).ToString("0.00") + " s.");
                        }

                        reading.Wem = WwiseVorbisWriter.Build(encoded.Stream);
                        break;
                }

                reading.NewBytes = reading.Wem.Length;

                //Audio held in a soundbank sits in memory for as long as the bank is loaded; a stream is read as it plays
                bool inBank = target != null && string.Equals(Path.GetExtension(target.File), ".bnk", StringComparison.OrdinalIgnoreCase);
                if (reading.OriginalBytes > 0 && reading.NewBytes > 2L * reading.OriginalBytes && reading.NewBytes > 256 * 1024)
                    reading.Notes.Add("This is " + (reading.NewBytes / (double)reading.OriginalBytes).ToString("0.0") + " times the size of the audio it replaces"
                        + (inBank ? ", and audio in a soundbank is held in memory for as long as the bank is loaded" : "")
                        + ". A shorter sound, mono, a lower sample rate or a lower quality makes it smaller.");
                else if (reading.NewBytes > (inBank ? 2 : 16) * 1024 * 1024)
                    reading.Notes.Add("This is " + Describe(reading.NewBytes) + " of audio" + (inBank ? ", held in memory for as long as its soundbank is loaded" : "")
                        + ". A shorter sound, mono, a lower sample rate or a lower quality makes it smaller.");
            }
            catch (Exception e)
            {
                reading.Problem = e.Message;
                return reading;
            }

            return reading;
        }

        /// <summary>
        /// A .wav encoded for a new event in a soundbank of the level's own (<see cref="WwiseBankBuilder"/>), laid out like the
        /// sound of the game's it's made to sound like when the options ask. Nothing is written.
        /// </summary>
        public static Reading ReadForBank(string file, WwiseMediaLocation template, Options options)
        {
            Reading reading = Encode(file, template, options);
            if (reading.Problem != null)
                return reading;

            //Not a replacement: what the template's audio weighs says nothing about this one
            reading.OriginalBytes = 0;
            reading.Notes.RemoveAll(o => o.Contains("times the size of the audio it replaces"));
            for (int i = 0; i < reading.Notes.Count; i++)
                reading.Notes[i] = reading.Notes[i].Replace("the sound it replaces", "the sound it's made to sound like");
            reading.Plan = new WwiseMediaInjector.Plan { Kind = "a soundbank of the level's own", Supported = true };
            return reading;
        }

        /// <summary>
        /// A .wav encoded as a stream of its own, by id, for the level's sound package - audio a soundbank of the user's own
        /// streams (one built with the Wwise authoring tool, added beside it). There is no sound of the game's to match, so
        /// the .wav's layout is kept unless the options say otherwise. Nothing is written.
        /// </summary>
        public static Reading ReadForLevelStream(string file, uint streamId, string package, Options options)
        {
            Reading reading = Encode(file, null, options);
            if (reading.Problem != null)
                return reading;

            LevelSoundOverride.Plan level = LevelSoundOverride.AddStream(package, streamId, reading.Wem, false);
            reading.Level = level;
            if (!level.Ok)
            {
                reading.Problem = level.Problem;
                return reading;
            }

            reading.Plan = new WwiseMediaInjector.Plan
            {
                File = package,
                Kind = "this level's sound package, as stream " + streamId,
                Supported = true,
                Growth = Math.Max(0, level.SizeAfter - level.SizeBefore),
            };
            reading.Notes.AddRange(level.Notes);
            return reading;
        }

        /// <summary>Write the replacement out. Only call this with a reading that came back Ok.</summary>
        public static bool Apply(Reading reading, WwiseMediaLocation target, out string problem)
        {
            problem = null;
            if (reading == null || !reading.Ok)
            {
                problem = reading == null ? "There is nothing to import." : reading.Problem;
                return false;
            }

            List<string> failures = new List<string>();
            int done = 0;

            foreach (WwiseMediaLocation copy in reading.Copies.Count == 0 ? new[] { target } : reading.Copies)
            {
                try
                {
                    WwiseMediaInjector.Replace(copy, reading.Wem);
                    done++;
                }
                catch (Exception e)
                {
                    failures.Add(Path.GetFileName(copy.File) + ": " + e.Message);
                }
            }

            if (failures.Count == 0)
                return true;

            //A partial replacement is worth saying out loud - the sound is changed in some places and not
            //others, which sounds like a bug in the game rather than an unfinished import
            problem = done == 0
                ? string.Join(Environment.NewLine, failures)
                : "Replaced in " + done + " of " + (done + failures.Count) + " places. The rest still hold the "
                    + "original audio:" + Environment.NewLine + string.Join(Environment.NewLine, failures);
            return false;
        }

        /// <summary>
        /// The wave format the target's current media uses. Anything unreadable (or no target at all: a new
        /// stream) falls back to Vorbis, which is what everything was before the Switch build existed.
        /// </summary>
        private static ushort ExistingCodec(byte[] original)
        {
            try
            {
                if (original != null)
                    return WemChunks.FormatTag(original);
            }
            catch
            {
            }

            return WwiseVorbisConverter.FormatVorbis;
        }

        /// <summary>
        /// Mix and resample the .wav to the layout asked for - its own, or the sound's it replaces - moving to the nearest
        /// layout the game's Vorbis decoder is set up for where that one isn't. What was done is noted, along with anything
        /// of the original's (a loop region, markers) the new audio can't carry.
        /// </summary>
        private static void Conform(WaveFile.Audio audio, Options options, Reading reading, bool vorbis, bool modern)
        {
            MediaLayout original = reading.Original;
            bool matchChannels = options.Channels <= 0 && options.MatchOriginalChannels && original != null;
            bool matchRate = options.SampleRate <= 0 && options.MatchOriginalRate && original != null;

            int channels = options.Channels > 0 ? Math.Min(2, options.Channels)
                : matchChannels ? Math.Min(2, original.Channels)
                : Math.Min(2, audio.Channels);
            int wanted = options.SampleRate > 0 ? options.SampleRate
                : matchRate ? original.SampleRate
                : audio.SampleRate;
            int rate = vorbis ? AudioConform.EncodableRate(channels, wanted, modern) : wanted;

            if (audio.Channels != channels)
            {
                audio.Samples = AudioConform.Mix(audio.Samples, channels);
                reading.Notes.Add("Mixed from " + ChannelWords(reading.SourceChannels) + " to " + ChannelWords(channels)
                    + (matchChannels ? " to match the sound it replaces" : reading.SourceChannels > 2 && options.Channels <= 0 ? ", the most the game's codebooks cover" : "") + ".");
            }
            if (matchChannels && original.Channels > 2)
                reading.Notes.Add("The sound it replaces is " + ChannelWords(original.Channels) + "; the game's codebooks only cover mono and stereo, so the new audio is " + ChannelWords(channels) + ".");

            if (audio.SampleRate != rate)
            {
                int from = audio.SampleRate;
                audio.Samples = AudioConform.Resample(audio.Samples, from, rate);
                audio.SampleRate = rate;
                if (audio.LoopStart >= 0)
                {
                    //The loop region at the new rate: the same moments, in the new rate's samples
                    audio.LoopStart = (long)Math.Round(audio.LoopStart * (double)rate / from);
                    audio.LoopEnd = Math.Min(audio.Frames - 1, (long)Math.Round((audio.LoopEnd + 1) * (double)rate / from) - 1);
                }
                reading.Notes.Add("Resampled from " + from + " Hz to " + rate + " Hz"
                    + (rate != wanted ? " - the nearest rate to " + wanted + " Hz the game's decoder is set up for at " + ChannelWords(channels) + "."
                    : matchRate ? " to match the sound it replaces." : "."));
            }
            else if (rate != wanted)
                reading.Notes.Add("Kept at " + rate + " Hz: the game's decoder isn't set up for " + wanted + " Hz at " + ChannelWords(channels) + ".");

            if (original != null && original.LoopEnd > 0 && audio.LoopStart < 0)
                reading.Notes.Add("The sound it replaces loops a part of itself (" + original.LoopStart.ToString("0.00") + " s to " + original.LoopEnd.ToString("0.00")
                    + " s) once it has played through. The .wav carries no loop points, so where the game loops this sound it repeats the whole of it - give the .wav a loop region (a smpl chunk, as audio editors write one) to loop a part.");
            if (original != null && original.Markers > 0)
                reading.Notes.Add("The sound it replaces carries " + original.Markers + " marker" + (original.Markers == 1 ? "" : "s") + " (cue points); the new audio carries none.");
        }

        private static string ChannelWords(int channels)
        {
            return channels == 1 ? "mono" : channels == 2 ? "stereo" : channels == 6 ? "5.1" : channels + " channels";
        }

        /// <summary>The containers a set of copies live in, listed for the user.</summary>
        private static string Distinct(IEnumerable<WwiseMediaLocation> copies)
        {
            List<string> names = new List<string>();
            foreach (WwiseMediaLocation copy in copies)
            {
                string name = Path.GetFileName(copy.File);
                if (!names.Contains(name))
                    names.Add(name);
            }

            if (names.Count <= 3)
                return string.Join(", ", names);

            return string.Join(", ", names.GetRange(0, 3)) + " and " + (names.Count - 3) + " more";
        }

        /// <summary>A one paragraph summary of what is about to happen, for the user to read.</summary>
        public static string Describe(Reading reading)
        {
            if (reading == null)
                return "";
            if (!reading.Ok)
                return reading.Problem ?? "";

            string text = reading.Channels == 1 ? "Mono" : "Stereo";
            text += " audio, " + reading.Duration.ToString("0.0") + " seconds at " + reading.SampleRate + " Hz"
                + (reading.Original != null ? " (the sound it replaces: " + reading.Original + ")" : "") + ".";
            text += Environment.NewLine + "Encoded to " + reading.Codec
                + (reading.Codec == "Wwise Vorbis" ? " at quality " + reading.Quality.ToString("0.0") : ", matching the sound it replaces")
                + ", " + Describe(reading.NewBytes) + (reading.OriginalBytes > 0 ? " against the " + Describe(reading.OriginalBytes) + " it replaces" : "")
                + ", in " + reading.Plan.Kind + ".";

            foreach (string note in reading.Notes)
                text += Environment.NewLine + Environment.NewLine + note;

            return text;
        }

        private static string Describe(long bytes)
        {
            if (bytes >= 1024 * 1024)
                return (bytes / (1024.0 * 1024.0)).ToString("0.0") + " MB";
            if (bytes >= 1024)
                return (bytes / 1024.0).ToString("0") + " KB";
            return bytes + " bytes";
        }
    }
}

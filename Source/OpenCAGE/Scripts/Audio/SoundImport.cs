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
        }

        public sealed class Reading
        {
            public byte[] Wem;

            public int Channels;
            public int SampleRate;
            public double Duration;
            public float Quality;

            /// <summary>What the audio was encoded as - always whatever the original sound used.</summary>
            public string Codec = "Wwise Vorbis";

            public int OriginalBytes;
            public int NewBytes;

            /// <summary>What the replacement would do to the file holding the sound.</summary>
            public WwiseMediaInjector.Plan Plan;

            /// <summary>Every copy of this audio the game ships, all of which get replaced.</summary>
            public IList<WwiseMediaLocation> Copies = new List<WwiseMediaLocation>();

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
            if (options == null)
                options = new Options();

            Reading reading = new Reading();
            reading.OriginalBytes = target == null ? 0 : target.Length;

            try
            {
                WaveFile.Audio audio = WaveFile.Read(file);
                reading.Channels = audio.Channels;
                reading.SampleRate = audio.SampleRate;
                reading.Duration = audio.Duration;

                if (audio.Channels > 2)
                {
                    reading.Problem = "That file has " + audio.Channels + " channels. The game's codebooks only "
                        + "cover mono and stereo at these sample rates, so surround audio cannot be encoded - "
                        + "mix it down to stereo first.";
                    return reading;
                }

                /* The game picks its decoder from the sound object in the bank, not from the media, so
                 * the replacement has to be the codec the original already was. Everything on PC is
                 * Vorbis; the Switch build also stores some sounds as Wwise ADPCM and a few as PCM. */
                switch (ExistingCodec(target))
                {
                    case WwiseAdpcmReader.FormatAdpcm:
                        reading.Codec = "Wwise ADPCM";
                        reading.Wem = WwiseAdpcmWriter.Build(audio.Samples, audio.SampleRate);
                        break;

                    case WwisePcmReader.FormatPcm:
                        reading.Codec = "PCM";
                        reading.Wem = WwiseAdpcmWriter.BuildPcm(audio.Samples, audio.SampleRate);
                        break;

                    default:
                        VorbisEncoder.Result encoded = VorbisEncoder.Encode(audio.Samples, audio.SampleRate, options.Quality);
                        reading.Quality = encoded.Quality;
                        if (encoded.Note != null)
                            reading.Notes.Add(encoded.Note);

                        reading.Wem = WwiseVorbisWriter.Build(encoded.Stream);
                        break;
                }

                reading.NewBytes = reading.Wem.Length;
            }
            catch (Exception e)
            {
                reading.Problem = e.Message;
                return reading;
            }

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
        /// The wave format the target's current media uses. Anything unreadable falls back to
        /// Vorbis, which is what everything was before the Switch build existed.
        /// </summary>
        private static ushort ExistingCodec(WwiseMediaLocation target)
        {
            try
            {
                if (target != null)
                    return WemChunks.FormatTag(WwiseSoundLibrary.ReadMedia(target));
            }
            catch
            {
            }

            return WwiseVorbisConverter.FormatVorbis;
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
            text += " audio, " + reading.Duration.ToString("0.0") + " seconds at " + reading.SampleRate + " Hz.";
            text += Environment.NewLine + "Encoded to " + reading.Codec
                + (reading.Codec == "Wwise Vorbis" ? " at quality " + reading.Quality.ToString("0.0") : ", matching the sound it replaces")
                + ", " + Describe(reading.NewBytes) + " against the " + Describe(reading.OriginalBytes)
                + " it replaces, in " + reading.Plan.Kind + ".";

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

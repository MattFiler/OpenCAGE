using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// Analyses an uncompressed WAV in one pass: loudness over time, the sounds in it (start, end, peak, fades), onsets where a new
    /// sound rises over others, and optionally where given tones are. Times are seconds from the file start, and also from the last
    /// mark when the file has marks (a game audio recording also stores its wall-clock start).
    /// </summary>
    internal static class AudioAnalysis
    {
        public sealed class Options
        {
            /// <summary>Loudness is measured over windows this long (1-100 ms).</summary>
            public double FrameMs = 10;
            /// <summary>A sound is louder than this (dBFS); NaN: picked from the recording (12 dB over its quiet level, -100 at least).</summary>
            public double ThresholdDb = double.NaN;
            /// <summary>Two sounds closer than this are one.</summary>
            public double MinSilenceMs = 250;
            /// <summary>A jump at least this big over the level of the quarter second before is a new sound starting (dB).</summary>
            public double RiseDb = 10;
            /// <summary>Only this part of the file (seconds; To &lt;= 0: to the end).</summary>
            public double From, To;
            /// <summary>Tones to look for (Hz), in 50 ms windows.</summary>
            public double[] TonesHz = new double[0];
        }

        public sealed class Mark
        {
            public string Label;
            public double Seconds;
        }

        public sealed class Sound
        {
            public double Start, End;
            /// <summary>The loudest sample, and the level it holds (90th percentile of its third-of-a-second loudness), in dBFS.</summary>
            public double PeakDb, SustainDb;
            /// <summary>When it reached its held level (within 3 dB), and when it last was at it before ending.</summary>
            public double SustainFrom, SustainTo;
            public double Length => End - Start;
            public double FadeIn => SustainFrom - Start;
            public double FadeOut => End - SustainTo;
        }

        public sealed class Onset
        {
            public double Time;
            /// <summary>"start" (out of quiet) or "rise" (over other sound).</summary>
            public string Kind;
            public double FromDb, ToDb;
        }

        public sealed class ToneRun
        {
            public double Hz, Start, End, Share, LevelDb;
        }

        public sealed class Result
        {
            public string Path;
            public int SampleRate, Channels, Bits;
            public bool Float;
            public double Duration;
            public string Comment;
            public DateTime? StartUtc;
            public double From, To;
            public double FrameSeconds;
            /// <summary>The level of every window (dBFS), from <see cref="From"/>.</summary>
            public float[] FrameDb;
            public double PeakDb, RmsDb, FloorDb, ThresholdDb;
            public List<Mark> Marks = new List<Mark>();
            public List<Sound> Sounds = new List<Sound>();
            public List<Onset> Onsets = new List<Onset>();
            public List<ToneRun> Tones = new List<ToneRun>();
            public Dictionary<double, double> ToneSeconds = new Dictionary<double, double>();

            /// <summary>The level (dBFS) over [from, to) seconds, power-averaged.</summary>
            public double LevelDb(double from, double to)
            {
                int a = Math.Max(0, (int)Math.Floor((from - From) / FrameSeconds));
                int b = Math.Min(FrameDb.Length, (int)Math.Ceiling((to - From) / FrameSeconds));
                if (b <= a) return -200;
                double sum = 0;
                for (int i = a; i < b; i++) sum += Math.Pow(10, FrameDb[i] / 10.0);
                return GameAudioCapture.ToDb(sum / (b - a));
            }

            /// <summary>The last mark at or before a time (a hair of slack for rounding), or null.</summary>
            public Mark MarkBefore(double seconds) => Marks.Where(o => o.Seconds <= seconds + 0.0005).OrderBy(o => o.Seconds).LastOrDefault();

            public JObject ToJson(int limit = 100, bool envelope = true, double envelopeStep = 0, int maxEnvelopePoints = 300)
            {
                JObject json = new JObject()
                {
                    ["path"] = Path,
                    ["format"] = SampleRate + " Hz, " + Channels + " channel" + (Channels == 1 ? "" : "s") + ", " + Bits + "-bit " + (Float ? "float" : "PCM"),
                    ["duration_s"] = R(Duration),
                };
                if (StartUtc.HasValue) json["start_utc"] = Utc(StartUtc.Value);
                if (From > 0 || To < Duration - 1e-6) json["range_s"] = new JArray(R(From), R(To));
                json["peak_dbfs"] = Db(PeakDb);
                json["rms_dbfs"] = Db(RmsDb);
                json["quiet_level_dbfs"] = Db(FloorDb);
                json["threshold_dbfs"] = Db(ThresholdDb);
                if (Marks.Count != 0)
                    json["marks"] = new JArray(Marks.Select(o => Timed(new JObject() { ["label"] = o.Label }, o.Seconds, false)));
                json["sound_count"] = Sounds.Count;
                json["sounds"] = new JArray(Sounds.Take(limit).Select(o =>
                {
                    JObject sound = Timed(new JObject(), o.Start, true);
                    sound["end_s"] = R(o.End);
                    Mark mark = MarkBefore(o.Start);
                    if (mark != null) sound["end_after_mark_s"] = R(o.End - mark.Seconds);
                    sound["length_s"] = R(o.Length);
                    sound["peak_dbfs"] = Db(o.PeakDb);
                    sound["held_dbfs"] = Db(o.SustainDb);
                    sound["fade_in_s"] = R(o.FadeIn);
                    sound["fade_out_s"] = R(o.FadeOut);
                    sound["fade_out_from_s"] = R(o.SustainTo);
                    return sound;
                }));
                if (Sounds.Count > limit) json["sounds_trimmed"] = Sounds.Count - limit;
                json["onsets"] = new JArray(Onsets.Take(limit * 2).Select(o =>
                {
                    JObject onset = Timed(new JObject() { ["kind"] = o.Kind }, o.Time, true);
                    onset["from_dbfs"] = Db(o.FromDb);
                    onset["to_dbfs"] = Db(o.ToDb);
                    return onset;
                }));
                if (Tones.Count != 0 || ToneSeconds.Count != 0)
                {
                    json["tones"] = new JArray(Tones.Take(limit).Select(o =>
                    {
                        JObject tone = Timed(new JObject() { ["hz"] = o.Hz }, o.Start, true);
                        tone["end_s"] = R(o.End);
                        tone["share"] = Math.Round(o.Share, 2);
                        tone["level_dbfs"] = Db(o.LevelDb);
                        return tone;
                    }));
                    json["tone_seconds"] = new JObject(ToneSeconds.Select(o => new JProperty(o.Key.ToString(CultureInfo.InvariantCulture), R(o.Value))));
                }
                if (envelope && FrameDb.Length != 0)
                {
                    double step = envelopeStep > 0 ? envelopeStep : NiceStep((To - From) / Math.Max(10, maxEnvelopePoints));
                    step = Math.Max(step, FrameSeconds);
                    JArray levels = new JArray();
                    for (double t = From; t < To - 1e-9 && levels.Count < 5000; t += step)
                        levels.Add(Db(LevelDb(t, Math.Min(To, t + step))));
                    json["loudness"] = new JObject() { ["from_s"] = R(From), ["step_s"] = R(step), ["dbfs"] = levels };
                }
                return json;
            }

            private JObject Timed(JObject json, double seconds, bool afterMark)
            {
                json["at_s"] = R(seconds);
                if (StartUtc.HasValue) json["at_utc"] = Utc(StartUtc.Value.AddTicks((long)Math.Round(seconds * 1e7)));
                if (afterMark)
                {
                    Mark mark = MarkBefore(seconds);
                    if (mark != null)
                    {
                        json["after_mark"] = mark.Label;
                        json["after_mark_s"] = R(seconds - mark.Seconds);
                    }
                }
                return json;
            }
        }

        private static double R(double seconds) => Math.Round(seconds, 3);
        private static double Db(double db) => db <= -199 ? -200 : Math.Round(db, 1);
        private static string Utc(DateTime utc) => utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

        private static double NiceStep(double raw)
        {
            foreach (double step in new[] { 0.01, 0.02, 0.05, 0.1, 0.2, 0.25, 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300, 600 })
                if (step >= raw) return step;
            return 600;
        }

        // ---------------------------------------------------------------------------------------------------------------------

        public static Result Analyze(string path, Options options, CancellationToken cancel = default(CancellationToken))
        {
            options = options ?? new Options();
            Result result = new Result() { Path = path };
            using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16))
            {
                (long dataAt, long dataBytes, int format) = ReadChunks(file, result);
                int blockAlign = result.Channels * (result.Bits / 8);
                long frames = dataBytes / blockAlign;
                result.Duration = frames / (double)result.SampleRate;
                result.From = Math.Max(0, Math.Min(options.From, result.Duration));
                result.To = options.To > 0 ? Math.Min(options.To, result.Duration) : result.Duration;
                if (result.To <= result.From) throw new InvalidDataException("The range asked for (" + options.From + " to " + options.To + " s) holds no audio: the file is " + R(result.Duration) + " s long.");

                double frameMs = Math.Max(1, Math.Min(100, options.FrameMs));
                int frameLength = Math.Max(1, (int)Math.Round(result.SampleRate * frameMs / 1000.0));
                result.FrameSeconds = frameLength / (double)result.SampleRate;
                long first = (long)Math.Floor(result.From * result.SampleRate), last = (long)Math.Floor(result.To * result.SampleRate);
                int frameCount = (int)((last - first + frameLength - 1) / frameLength);
                result.FrameDb = new float[frameCount];
                float[] framePeak = new float[frameCount];

                //Tones: Goertzel over 50 ms windows of the channels' mix
                double[] tones = (options.TonesHz ?? new double[0]).Where(o => o > 0 && o < result.SampleRate / 2.0).Distinct().ToArray();
                int toneLength = result.SampleRate / 20;
                double[] coefficient = tones.Select(o => 2 * Math.Cos(2 * Math.PI * o / result.SampleRate)).ToArray();
                double[] s1 = new double[tones.Length], s2 = new double[tones.Length];
                List<double[]> toneShares = new List<double[]>();
                List<double> toneLevels = new List<double>();
                double toneSum = 0;
                int toneFill = 0;

                file.Position = dataAt + first * blockAlign;
                byte[] buffer = new byte[blockAlign * 8192];
                long done = first;
                double frameSum = 0, totalSum = 0;
                float peak = 0, totalPeak = 0;
                int inFrame = 0, frameIndex = 0;
                long totalSamples = 0;
                while (done < last)
                {
                    cancel.ThrowIfCancellationRequested();
                    int want = (int)Math.Min(buffer.Length / blockAlign, last - done) * blockAlign;
                    int got = ReadFully(file, buffer, want);
                    if (got < blockAlign) break;
                    int n = got / blockAlign;
                    for (int f = 0; f < n; f++)
                    {
                        int at = f * blockAlign;
                        double mix = 0, squares = 0;
                        for (int c = 0; c < result.Channels; c++)
                        {
                            float value = Sample(buffer, at + c * (result.Bits / 8), format, result.Bits);
                            mix += value;
                            squares += value * (double)value;
                            float magnitude = Math.Abs(value);
                            if (magnitude > peak) peak = magnitude;
                        }
                        frameSum += squares;
                        totalSum += squares;
                        totalSamples += result.Channels;
                        if (++inFrame == frameLength)
                        {
                            if (frameIndex < frameCount)
                            {
                                result.FrameDb[frameIndex] = (float)GameAudioCapture.ToDb(frameSum / (frameLength * result.Channels));
                                framePeak[frameIndex] = peak;
                                frameIndex++;
                            }
                            if (peak > totalPeak) totalPeak = peak;
                            frameSum = 0; peak = 0; inFrame = 0;
                        }
                        if (tones.Length != 0)
                        {
                            mix /= result.Channels;
                            toneSum += mix * mix;
                            for (int t = 0; t < tones.Length; t++)
                            {
                                double s0 = mix + coefficient[t] * s1[t] - s2[t];
                                s2[t] = s1[t];
                                s1[t] = s0;
                            }
                            if (++toneFill == toneLength)
                            {
                                double[] shares = new double[tones.Length];
                                for (int t = 0; t < tones.Length; t++)
                                {
                                    double power = s1[t] * s1[t] + s2[t] * s2[t] - coefficient[t] * s1[t] * s2[t];
                                    shares[t] = toneSum <= 1e-20 ? 0 : (2 * power / toneLength) / toneSum;
                                    s1[t] = s2[t] = 0;
                                }
                                toneShares.Add(shares);
                                toneLevels.Add(GameAudioCapture.ToDb(toneSum / toneLength));
                                toneSum = 0;
                                toneFill = 0;
                            }
                        }
                    }
                    done += n;
                }
                if (inFrame > 0 && frameIndex < frameCount)
                {
                    result.FrameDb[frameIndex] = (float)GameAudioCapture.ToDb(frameSum / (inFrame * result.Channels));
                    framePeak[frameIndex] = peak;
                    if (peak > totalPeak) totalPeak = peak;
                    frameIndex++;
                }
                if (frameIndex < frameCount)
                {
                    Array.Resize(ref result.FrameDb, frameIndex);
                    Array.Resize(ref framePeak, frameIndex);
                }
                result.PeakDb = totalPeak <= 0 ? -200 : 20 * Math.Log10(totalPeak);
                result.RmsDb = GameAudioCapture.ToDb(totalSum / Math.Max(1, totalSamples));

                FindSounds(result, framePeak, options);
                FindRises(result, options);
                if (tones.Length != 0) FindTones(result, tones, toneShares, toneLevels, toneLength / (double)result.SampleRate);
            }
            return result;
        }

        /* Quiet level = the 10th percentile of the windows. Unless set, the threshold is max(-100, min(quiet + 12, loudest - 20)), so a
           recording that is all sound still splits at its quiet parts. The game's silence is exact zeros, so the -100 floor keeps
           fade-ins and tails around -80 dBFS (as the game's music has) inside the sound. */
        private static void FindSounds(Result result, float[] framePeak, Options options)
        {
            float[] db = result.FrameDb;
            if (db.Length == 0) { result.FloorDb = result.ThresholdDb = -200; return; }
            float[] sorted = db.Select(o => Math.Max(o, -120f)).OrderBy(o => o).ToArray();
            result.FloorDb = sorted[(int)(sorted.Length * 0.1)];
            double loudest = sorted[sorted.Length - 1];
            result.ThresholdDb = !double.IsNaN(options.ThresholdDb) ? options.ThresholdDb : Math.Max(-100, Math.Min(result.FloorDb + 12, loudest - 20));

            double fs = result.FrameSeconds;
            int gapFrames = Math.Max(1, (int)Math.Round(options.MinSilenceMs / 1000.0 / fs));
            List<(int A, int B)> runs = new List<(int, int)>();
            int i = 0;
            while (i < db.Length)
            {
                if (db[i] < result.ThresholdDb) { i++; continue; }
                int a = i;
                int b = i;
                int quiet = 0;
                for (i++; i < db.Length; i++)
                {
                    if (db[i] >= result.ThresholdDb) { b = i; quiet = 0; }
                    else if (++quiet >= gapFrames) break;
                }
                runs.Add((a, b));
            }

            //A third of a second of loudness, to tell a held level from a fade
            int smooth = Math.Max(1, (int)Math.Round(0.3 / fs));
            foreach ((int a, int b) in runs)
            {
                if (b - a + 1 < 2) continue;
                float peak = 0;
                for (int f = a; f <= b; f++) peak = Math.Max(peak, framePeak[f]);
                double[] smoothed = new double[b - a + 1];
                for (int f = a; f <= b; f++)
                {
                    double sum = 0; int count = 0;
                    for (int k = Math.Max(a, f - smooth / 2); k <= Math.Min(b, f + smooth / 2); k++) { sum += Math.Pow(10, db[k] / 10.0); count++; }
                    smoothed[f - a] = GameAudioCapture.ToDb(sum / count);
                }
                //Its held level: the 90th percentile of that (a long fade doesn't drag it down; a brief burst doesn't set it)
                double[] order = smoothed.OrderBy(o => o).ToArray();
                double held = order[Math.Min(order.Length - 1, (int)(order.Length * 0.9))];
                int from = 0, to = smoothed.Length - 1;
                while (from < smoothed.Length - 1 && smoothed[from] < held - 3) from++;
                while (to > from && smoothed[to] < held - 3) to--;
                result.Sounds.Add(new Sound()
                {
                    Start = result.From + a * fs,
                    End = result.From + (b + 1) * fs,
                    PeakDb = peak <= 0 ? -200 : 20 * Math.Log10(peak),
                    SustainDb = held,
                    SustainFrom = result.From + (a + from) * fs,
                    SustainTo = result.From + (a + to + 1) * fs,
                });
                result.Onsets.Add(new Onset() { Time = result.From + a * fs, Kind = "start", FromDb = a > 0 ? db[a - 1] : -200, ToDb = db[a] });
            }
        }

        /* A jump: the next 30 ms at least RiseDb over the quarter second that ended 50 ms before, inside a sound (a new sound starting
           over others). One per 200 ms; none within 100 ms of a sound's start. */
        private static void FindRises(Result result, Options options)
        {
            float[] db = result.FrameDb;
            double fs = result.FrameSeconds;
            int before = Math.Max(1, (int)Math.Round(0.25 / fs)), skip = Math.Max(1, (int)Math.Round(0.05 / fs)), now = Math.Max(1, (int)Math.Round(0.03 / fs));
            int hold = Math.Max(1, (int)Math.Round(0.2 / fs));
            List<Onset> rises = new List<Onset>();
            for (int i = before + skip; i + now <= db.Length; i++)
            {
                double reference = Mean(db, i - skip - before, i - skip), current = Mean(db, i, i + now);
                if (current < result.ThresholdDb || current - reference < options.RiseDb) continue;
                double time = result.From + i * fs;
                if (!result.Onsets.Any(o => Math.Abs(o.Time - time) < 0.1))
                    rises.Add(new Onset() { Time = time, Kind = "rise", FromDb = reference, ToDb = current });
                i += hold;
            }
            result.Onsets.AddRange(rises);
            result.Onsets.Sort((x, y) => x.Time.CompareTo(y.Time));
        }

        private static double Mean(float[] db, int a, int b)
        {
            double sum = 0;
            for (int i = a; i < b; i++) sum += Math.Pow(10, db[i] / 10.0);
            return GameAudioCapture.ToDb(sum / Math.Max(1, b - a));
        }

        private static void FindTones(Result result, double[] tones, List<double[]> shares, List<double> levels, double window)
        {
            for (int t = 0; t < tones.Length; t++)
            {
                double seconds = 0;
                int w = 0;
                while (w < shares.Count)
                {
                    bool on = shares[w][t] > 0.5 && levels[w] >= result.ThresholdDb;
                    if (!on) { w++; continue; }
                    int a = w;
                    double share = 0, power = 0;
                    for (; w < shares.Count && shares[w][t] > 0.5 && levels[w] >= result.ThresholdDb; w++) { share += shares[w][t]; power += Math.Pow(10, levels[w] / 10.0); }
                    int n = w - a;
                    seconds += n * window;
                    result.Tones.Add(new ToneRun() { Hz = tones[t], Start = result.From + a * window, End = result.From + w * window, Share = share / n, LevelDb = GameAudioCapture.ToDb(power / n) });
                }
                result.ToneSeconds[tones[t]] = seconds;
            }
            result.Tones.Sort((x, y) => x.Start.CompareTo(y.Start));
        }

        // ---------------------------------------------------------------------------------------------------------------------

        /* Reads fmt, data (to the end of the file when it is still being written), LIST/INFO ICMT (a game audio recording's start
           time) and cue + LIST/adtl labl (marks). Returns the data offset and size, and the format tag. */
        private static (long, long, int) ReadChunks(FileStream file, Result result)
        {
            BinaryReader reader = new BinaryReader(file, Encoding.ASCII, true);
            if (file.Length < 12 || Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF")
                throw new InvalidDataException("That is not a WAV file.");
            reader.ReadUInt32();
            if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE")
                throw new InvalidDataException("That is not a WAV file.");
            long dataAt = -1, dataBytes = 0;
            int format = -1;
            Dictionary<int, long> cues = new Dictionary<int, long>();
            Dictionary<int, string> labels = new Dictionary<int, string>();
            while (file.Position + 8 <= file.Length)
            {
                string tag = Encoding.ASCII.GetString(reader.ReadBytes(4));
                uint size = reader.ReadUInt32();
                long body = file.Position;
                long available = file.Length - body;
                if (tag == "fmt ")
                {
                    format = reader.ReadUInt16();
                    result.Channels = reader.ReadUInt16();
                    result.SampleRate = (int)reader.ReadUInt32();
                    reader.ReadUInt32();
                    reader.ReadUInt16();
                    result.Bits = reader.ReadUInt16();
                    if (format == 0xFFFE && size >= 40)
                    {
                        file.Position = body + 24;
                        format = reader.ReadUInt16();
                    }
                }
                else if (tag == "data")
                {
                    dataAt = body;
                    //0, too big or 0xFFFFFFFF: a file still being written (or cut short), so read to its end
                    dataBytes = size == 0 || size > available ? available : size;
                }
                else if (tag == "LIST" && size >= 4)
                {
                    string kind = Encoding.ASCII.GetString(reader.ReadBytes(4));
                    long end = body + Math.Min(size, available);
                    while (file.Position + 8 <= end)
                    {
                        string sub = Encoding.ASCII.GetString(reader.ReadBytes(4));
                        uint length = reader.ReadUInt32();
                        long subBody = file.Position;
                        if (kind == "INFO" && sub == "ICMT")
                            result.Comment = Text(reader.ReadBytes((int)Math.Min(length, 4096)));
                        else if (kind == "adtl" && (sub == "labl" || sub == "note") && length >= 4)
                        {
                            int id = reader.ReadInt32();
                            labels[id] = Text(reader.ReadBytes((int)Math.Min(length - 4, 4096)));
                        }
                        file.Position = subBody + length + (length & 1);
                    }
                }
                else if (tag == "cue " && size >= 4)
                {
                    int count = reader.ReadInt32();
                    for (int i = 0; i < count && file.Position + 24 <= body + size; i++)
                    {
                        int id = reader.ReadInt32();
                        reader.ReadUInt32();
                        reader.ReadBytes(4);
                        reader.ReadUInt32();
                        reader.ReadUInt32();
                        cues[id] = reader.ReadUInt32();
                    }
                }
                if (tag == "data" && dataBytes == available) break;
                file.Position = body + size + (size & 1);
            }
            if (format < 0 || dataAt < 0) throw new InvalidDataException("That WAV has no format or audio data.");
            if (format != 1 && format != 3) throw new NotSupportedException("That WAV is compressed (format 0x" + format.ToString("X4") + ").");
            if (format == 3 && result.Bits != 32 && result.Bits != 64) throw new NotSupportedException("That WAV stores " + result.Bits + "-bit floating point samples.");
            if (format == 1 && result.Bits != 8 && result.Bits != 16 && result.Bits != 24 && result.Bits != 32) throw new NotSupportedException("That WAV stores " + result.Bits + " bits per sample.");
            if (result.Channels < 1 || result.SampleRate < 1000) throw new InvalidDataException("That WAV's format chunk is broken.");
            result.Float = format == 3;

            if (result.Comment != null)
            {
                foreach (string part in result.Comment.Split(';'))
                {
                    string[] pair = part.Split(new[] { '=' }, 2);
                    if (pair.Length == 2 && pair[0].Trim() == "start_utc" && DateTime.TryParse(pair[1].Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime start))
                        result.StartUtc = DateTime.SpecifyKind(start, DateTimeKind.Utc);
                }
            }
            foreach (KeyValuePair<int, long> cue in cues.OrderBy(o => o.Value))
                result.Marks.Add(new Mark() { Label = labels.TryGetValue(cue.Key, out string label) ? label : "cue " + cue.Key, Seconds = cue.Value / (double)result.SampleRate });
            return (dataAt, dataBytes, format);
        }

        private static string Text(byte[] bytes)
        {
            int end = Array.IndexOf(bytes, (byte)0);
            return Encoding.UTF8.GetString(bytes, 0, end < 0 ? bytes.Length : end).Trim();
        }

        private static int ReadFully(Stream stream, byte[] buffer, int count)
        {
            int read = 0;
            while (read < count)
            {
                int got = stream.Read(buffer, read, count - read);
                if (got <= 0) break;
                read += got;
            }
            return read;
        }

        private static float Sample(byte[] b, int at, int format, int bits)
        {
            if (format == 3)
                return bits == 32 ? BitConverter.ToSingle(b, at) : (float)BitConverter.ToDouble(b, at);
            switch (bits)
            {
                case 8: return (b[at] - 128) / 128f;
                case 16: return BitConverter.ToInt16(b, at) / 32768f;
                case 24:
                    {
                        int value = b[at] | (b[at + 1] << 8) | (b[at + 2] << 16);
                        if ((value & 0x800000) != 0) value |= unchecked((int)0xFF000000);
                        return value / 8388608f;
                    }
                default: return BitConverter.ToInt32(b, at) / 2147483648f;
            }
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// Records one process (the game) and its child processes to a WAV with Windows process loopback capture, so no other audio is
    /// included. Needs Windows 10 build 20348 or later.
    ///
    /// The WAV is 48 kHz stereo 32-bit float and follows the wall clock: packets are placed by capture time and gaps are filled with
    /// silence, so N seconds into the file is <see cref="StartUtc"/> + N. The start time is stored in a LIST/INFO comment, and marks
    /// (<see cref="AddMark"/>) are written as labelled cue points.
    /// </summary>
    internal sealed class GameAudioCapture
    {
        public const int SampleRate = 48000;
        public const int Channels = 2;
        private const int BytesPerFrame = Channels * 4;

        /// <summary>The Windows volume the game is turned down to during a quiet recording (-66 dB, inaudible).</summary>
        public const float QuietVolume = 0.0005f;

        /// <summary>Windows applies the program's Volume Mixer level to its loopback capture, so a quiet recording is scaled back up.</summary>
        public static bool CaptureFollowsSessionVolume = true;

        //WAV data is limited to 4 GB: stop just short of it (about 3 hours at this rate)
        private const long MaxDataBytes = 4000000000L;

        public sealed class Options
        {
            /// <summary>What the recording is of (the level), kept in the file's comment.</summary>
            public string Label;
            /// <summary>Turn the game down in Windows while recording so it isn't heard (the recording is scaled back up).</summary>
            public bool Quiet;
            /// <summary>Stop by itself after this long (0: until stopped or the game closes).</summary>
            public double MaxSeconds;
        }

        public sealed class Mark
        {
            public string Label;
            public long Frame;
            public DateTime Utc;
            public double Seconds => Frame / (double)SampleRate;
        }

        public int ProcessId { get; }
        public string Path { get; }
        public string Label { get; }
        public bool Quiet { get; }
        public double MaxSeconds { get; }

        /// <summary>The wall-clock time of the first sample (null until the first audio arrives).</summary>
        public DateTime? StartUtc { get; private set; }
        public DateTime RequestedUtc { get; } = PreciseUtcNow();

        /// <summary>Frames on the time line so far (including silence put in for gaps).</summary>
        public long Frames => Interlocked.Read(ref _frames);
        public double Seconds => Frames / (double)SampleRate;
        /// <summary>Of those, frames of silence put in where Windows delivered nothing.</summary>
        public long FilledFrames => Interlocked.Read(ref _filled);
        /// <summary>Of those, frames filled next to sound (on either side of the gap): audio that was lost.</summary>
        public long FilledDuringSoundFrames => Interlocked.Read(ref _filledDuringSound);
        public float Peak => _peak;
        /// <summary>The recent level (smoothed over about 0.3 s), in dBFS.</summary>
        public double RecentDb => ToDb(_recentPower);
        public double TotalPower => _sumSquares / Math.Max(1, (double)Interlocked.Read(ref _samplesSeen));
        /// <summary>The factor every sample was scaled by (above 1 when a quiet recording is scaled back up).</summary>
        public float Gain { get; private set; } = 1f;
        public bool Running => !_ended.WaitOne(0);
        /// <summary>Why it ended: "stopped", "the game closed", "max_seconds", "4 GB", or an error.</summary>
        public string EndedBecause { get; private set; }
        public string Problem { get; private set; }
        /// <summary>
        /// The game's Windows audio session as first found (before any quiet turn-down). Re-read until one exists, since a newly
        /// started game creates it a little later; Sessions = 0: none yet.
        /// </summary>
        public GameAudioSessions.State Session { get; private set; }
        /// <summary>The clock drift seen between the audio and the wall clock (ms; audio behind is positive), for the record.</summary>
        public double DriftMs { get; private set; }
        /// <summary>The buffer Windows gave the recording, in frames.</summary>
        public uint BufferFrames { get; private set; }

        public event Action<GameAudioCapture> Ended;

        /// <summary>For tests: every packet as it arrives (its capture time in 100 ns units, frames, flags, frames on the time line before it).</summary>
        internal static Action<long, uint, uint, long> PacketTrace = null;
        /// <summary>For tests: a packet this says yes to is thrown away as if Windows had never delivered it (by its capture time).</summary>
        internal static Func<long, bool> DropPacketForTest = null;
        /// <summary>Windows signals each packet (true), or the recording polls for them (false).</summary>
        internal static bool EventDriven = true;
        /// <summary>For tests: called after each packet is written (a slow disk).</summary>
        internal static Action WriteDelayForTest = null;

        private sealed class Packet
        {
            public long Qpc;
            public int Count;
            public bool Silent;
            public float[] Samples;
        }

        private readonly List<Mark> _marks = new List<Mark>();
        private readonly ManualResetEvent _ended = new ManualResetEvent(false);
        private readonly ManualResetEvent _started = new ManualResetEvent(false);
        private volatile bool _stop;
        private long _frames, _filled, _filledDuringSound, _samplesSeen;
        private bool _lastSilent = true;
        private float _peak;
        private double _recentPower, _sumSquares;
        private long _qpcStart = -1;   //100 ns units, the first packet's capture time
        private Thread _thread;
        private Exception _startError;

        private GameAudioCapture(int pid, string path, Options options)
        {
            ProcessId = pid;
            Path = path;
            Label = options?.Label;
            Quiet = options?.Quiet == true;
            MaxSeconds = Math.Max(0, options?.MaxSeconds ?? 0);
        }

        /// <summary>
        /// Starts recording process <paramref name="pid"/> (and its children) to <paramref name="path"/>. Returns once Windows has
        /// started the capture; throws <see cref="GameAudioException"/> saying why it could not.
        /// </summary>
        public static GameAudioCapture Start(int pid, string path, Options options)
        {
            GameAudioCapture capture = new GameAudioCapture(pid, path, options);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)));
            capture._thread = new Thread(capture.Run) { IsBackground = true, Name = "Game audio capture" };
            capture._thread.SetApartmentState(ApartmentState.MTA);
            capture._thread.Start();
            if (!capture._started.WaitOne(15000))
            {
                capture._stop = true;
                throw new GameAudioException("Windows did not start the recording within 15 seconds.");
            }
            if (capture._startError != null)
                throw capture._startError as GameAudioException ?? new GameAudioException(capture._startError.Message, capture._startError);
            return capture;
        }

        /// <summary>A labelled point at this moment of the recording (a cue point in the WAV).</summary>
        public Mark AddMark(string label)
        {
            DateTime now = PreciseUtcNow();
            long qpc = Qpc100ns();
            long frame = 0;
            long start = Interlocked.Read(ref _qpcStart);
            if (start >= 0)
                frame = Math.Max(0, (long)Math.Round((qpc - start) * (SampleRate / 1e7)));
            Mark mark = new Mark() { Label = string.IsNullOrWhiteSpace(label) ? "mark " + (_marks.Count + 1) : label.Trim(), Frame = frame, Utc = now };
            lock (_marks)
                _marks.Add(mark);
            return mark;
        }

        public List<Mark> Marks
        {
            get { lock (_marks) return _marks.ToList(); }
        }

        /// <summary>Stops (if it still runs) and waits for the file to be finished.</summary>
        public void Stop()
        {
            _stop = true;
            _ended.WaitOne(10000);
        }

        public bool WaitEnded(int milliseconds) => _ended.WaitOne(milliseconds);

        // ---------------------------------------------------------------------------------------------------------------------
        // The capture thread
        // ---------------------------------------------------------------------------------------------------------------------

        private void Run()
        {
            IAudioClient client = null;
            IAudioCaptureClient captureClient = null;
            FileStream file = null;
            AutoResetEvent ready = new AutoResetEvent(false);
            Process process = null;
            GameAudioSessions.Keeper keeper = null;
            bool started = false;
            try
            {
                try { process = Process.GetProcessById(ProcessId); }
                catch (Exception) { throw new GameAudioException("There is no process " + ProcessId + " (the game has closed?)."); }

                Session = GameAudioSessions.Read(ProcessId);
                client = Activate(ProcessId);

                IntPtr format = Marshal.AllocHGlobal(18);
                try
                {
                    Marshal.WriteInt16(format, 0, 3);                    //WAVE_FORMAT_IEEE_FLOAT
                    Marshal.WriteInt16(format, 2, Channels);
                    Marshal.WriteInt32(format, 4, SampleRate);
                    Marshal.WriteInt32(format, 8, SampleRate * BytesPerFrame);
                    Marshal.WriteInt16(format, 12, BytesPerFrame);
                    Marshal.WriteInt16(format, 14, 32);
                    Marshal.WriteInt16(format, 16, 0);
                    const int LOOPBACK = 0x00020000, EVENTCALLBACK = 0x00040000, AUTOCONVERTPCM = unchecked((int)0x80000000), SRC_DEFAULT_QUALITY = 0x08000000;
                    //Windows gives process loopback a 10 ms buffer (480 frames) whatever is asked: a reader later than that loses
                    //audio with no flag. Hence the dedicated reader thread below; any gap left is filled with silence.
                    Check(client.Initialize(0 /*shared*/, LOOPBACK | (EventDriven ? EVENTCALLBACK : 0) | AUTOCONVERTPCM | SRC_DEFAULT_QUALITY, 20000000 /*2 s*/, 0, format, IntPtr.Zero), "set up the recording");
                }
                finally { Marshal.FreeHGlobal(format); }
                if (client.GetBufferSize(out uint bufferFrames) == 0) BufferFrames = bufferFrames;
                if (EventDriven)
                    Check(client.SetEventHandle(ready.SafeWaitHandle.DangerousGetHandle()), "set up the recording");
                Guid captureId = typeof(IAudioCaptureClient).GUID;
                Check(client.GetService(ref captureId, out object service), "set up the recording");
                captureClient = (IAudioCaptureClient)service;

                file = new FileStream(Path, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16);
                WriteHeader(file, 0, Comment());

                //Quiet: turn the game down (and unmute it) in Windows while recording; restored at the end
                if (Quiet)
                {
                    keeper = new GameAudioSessions.Keeper(ProcessId, QuietVolume);
                    keeper.Begin();
                    Gain = CaptureFollowsSessionVolume ? 1f / QuietVolume : 1f;
                }
                Check(client.Start(), "start the recording");
                started = true;
                _started.Set();

                //Reader: a high-priority thread that only copies each packet out, releases it and queues it. Slow work (file
                //writes, session checks) stays on this thread.
                BlockingCollection<Packet> queue = new BlockingCollection<Packet>(new ConcurrentQueue<Packet>());
                ConcurrentBag<float[]> pool = new ConcurrentBag<float[]>();
                bool stopReading = false;
                Exception readError = null;
                IAudioCaptureClient reading = captureClient;
                Thread reader = new Thread(() =>
                {
                    IntPtr mmcss = IntPtr.Zero;
                    try
                    {
                        try { uint task = 0; mmcss = AvSetMmThreadCharacteristics("Pro Audio", ref task); } catch (Exception) { }
                        while (true)
                        {
                            bool last = Volatile.Read(ref stopReading);
                            ready.WaitOne(EventDriven ? 50 : 10);
                            while (true)
                            {
                                Check(reading.GetNextPacketSize(out uint next), "read the recording");
                                if (next == 0) break;
                                Check(reading.GetBuffer(out IntPtr data, out uint got, out uint flags, out ulong devicePosition, out ulong qpcPosition), "read the recording");
                                //Copy out and release at once: a packet held too long gets overwritten
                                int count = (int)got * Channels;
                                Packet packet = new Packet() { Qpc = (long)qpcPosition, Count = count };
                                try
                                {
                                    PacketTrace?.Invoke((long)qpcPosition, got, flags, Frames);
                                    if (!pool.TryTake(out packet.Samples) || packet.Samples.Length < count) packet.Samples = new float[Math.Max(count, 960)];
                                    packet.Silent = (flags & 2) != 0 || data == IntPtr.Zero; //AUDCLNT_BUFFERFLAGS_SILENT
                                    if (packet.Silent)
                                        Array.Clear(packet.Samples, 0, count);
                                    else
                                        Marshal.Copy(data, packet.Samples, 0, count);
                                }
                                finally
                                {
                                    reading.ReleaseBuffer(got);
                                }
                                if (DropPacketForTest != null && DropPacketForTest((long)qpcPosition))
                                {
                                    pool.Add(packet.Samples);
                                    continue;
                                }
                                queue.Add(packet);
                            }
                            if (last) break;
                        }
                    }
                    catch (Exception e) { readError = e; }
                    finally
                    {
                        queue.CompleteAdding();
                        if (mmcss != IntPtr.Zero) { try { AvRevertMmThreadCharacteristics(mmcss); } catch (Exception) { } }
                    }
                }) { IsBackground = true, Name = "Game audio capture (reader)", Priority = ThreadPriority.Highest };
                reader.SetApartmentState(ApartmentState.MTA);
                reader.Start();

                byte[] bytes = new byte[0];
                Stopwatch sinceHeader = Stopwatch.StartNew();
                Stopwatch running = Stopwatch.StartNew();
                Stopwatch sinceSlow = Stopwatch.StartNew();
                Stopwatch sinceSession = Stopwatch.StartNew();
                if (keeper != null && keeper.Original != null) Session = keeper.Original;
                while (!queue.IsCompleted)
                {
                    //Write queued packets, then do the slow work
                    while (queue.TryTake(out Packet packet, 50))
                    {
                        if (bytes.Length < packet.Count * 4) bytes = new byte[packet.Count * 4];
                        bool silent = packet.Silent || AllZero(packet.Samples, packet.Count);
                        Place(packet.Qpc, silent, file);
                        Append(packet.Samples, packet.Count, bytes, file);
                        _lastSilent = silent;
                        pool.Add(packet.Samples);
                        WriteDelayForTest?.Invoke();
                        if (sinceSlow.ElapsedMilliseconds > 50) break;
                    }
                    sinceSlow.Restart();
                    if (sinceHeader.ElapsedMilliseconds > 2000)
                    {
                        file.Flush();
                        WriteHeader(file, Frames * BytesPerFrame, Comment());
                        sinceHeader.Restart();
                    }
                    keeper?.Tick();
                    //The game's session as it was before any quiet change, once it has one
                    if (Session.Sessions == 0 && sinceSession.ElapsedMilliseconds > 500)
                    {
                        Session = keeper != null ? (keeper.Original ?? Session) : GameAudioSessions.Read(ProcessId);
                        sinceSession.Restart();
                    }
                    if (EndedBecause == null)
                    {
                        if (_stop) EndedBecause = "stopped";
                        else if (process.HasExited) EndedBecause = "the game closed";
                        else if (MaxSeconds > 0 && running.Elapsed.TotalSeconds >= MaxSeconds) EndedBecause = "max_seconds";
                        else if (Frames * (long)BytesPerFrame >= MaxDataBytes) { EndedBecause = "4 GB"; Problem = "The WAV reached 4 GB (about 3 hours): the recording stopped there."; }
                        else if (readError != null) EndedBecause = "error: " + readError.Message;
                        //The reader drains once more and stops; its queue is written before finishing
                        if (EndedBecause != null) Volatile.Write(ref stopReading, true);
                    }
                }
                reader.Join(5000);
                if (EndedBecause == null) EndedBecause = readError != null ? "error: " + readError.Message : "stopped";
                if (readError != null) Problem = (Problem != null ? Problem + " " : "") + readError.Message;
                try { client.Stop(); } catch (Exception) { }
            }
            catch (Exception e)
            {
                if (!started)
                {
                    _startError = e;
                    _started.Set();
                }
                Problem = e.Message;
                EndedBecause = "error: " + e.Message;
            }
            finally
            {
                if (keeper != null)
                {
                    string putBack = keeper.End();
                    if (putBack != null) Problem = (Problem != null ? Problem + " " : "") + putBack;
                }
                if (file != null)
                {
                    try
                    {
                        Finish(file);
                    }
                    catch (Exception e) { Problem = (Problem != null ? Problem + " " : "") + "The WAV could not be finished: " + e.Message; }
                    file.Dispose();
                    //A recording that never started leaves no file behind
                    if (!started)
                    {
                        try { File.Delete(Path); } catch (Exception) { }
                    }
                }
                Release(captureClient);
                Release(client);
                process?.Dispose();
                ready.Dispose();
                _ended.Set();
                if (started)
                {
                    try { Ended?.Invoke(this); }
                    catch (Exception e) { Debug.Log("GameAudio", "an Ended listener failed: " + e); }
                }
            }
        }

        /* The first packet anchors the time line to the wall clock; later ones are placed by capture time, so a gap where Windows
           delivered nothing becomes silence of the same length (Windows sometimes skips 10 ms engine periods around a stream starting
           or stopping, with no discontinuity flag). Gaps under 5 ms are ignored. A gap next to sound counts as lost audio
           (FilledDuringSoundFrames); one in silence lost nothing. */
        private void Place(long qpc, bool silentNow, FileStream file)
        {
            if (Interlocked.Read(ref _qpcStart) < 0)
            {
                long now = Qpc100ns();
                DateTime utc = PreciseUtcNow();
                long at = qpc > 0 ? qpc : now;
                StartUtc = utc - TimeSpan.FromTicks(Math.Max(0, now - at));
                Interlocked.Exchange(ref _qpcStart, at);
                //Marks placed before the first audio sit at its start
                return;
            }
            if (qpc <= 0)
                return;
            long expected = (long)Math.Round((qpc - Interlocked.Read(ref _qpcStart)) * (SampleRate / 1e7));
            long gap = expected - Frames;
            DriftMs = gap * 1000.0 / SampleRate;
            if (gap > SampleRate / 200)
            {
                byte[] zeros = new byte[Math.Min(gap, SampleRate) * BytesPerFrame];
                for (long left = gap; left > 0;)
                {
                    long now = Math.Min(left, SampleRate);
                    file.Write(zeros, 0, (int)(now * BytesPerFrame));
                    left -= now;
                }
                Interlocked.Add(ref _frames, gap);
                Interlocked.Add(ref _filled, gap);
                if (!(silentNow && _lastSilent))
                    Interlocked.Add(ref _filledDuringSound, gap);
                DriftMs = 0;
            }
        }

        private void Append(float[] samples, int count, byte[] bytes, FileStream file)
        {
            float gain = Gain;
            double sum = 0;
            float peak = _peak;
            for (int i = 0; i < count; i++)
            {
                float value = samples[i] * gain;
                samples[i] = value;
                sum += value * (double)value;
                float magnitude = value < 0 ? -value : value;
                if (magnitude > peak) peak = magnitude;
            }
            _peak = peak;
            _sumSquares += sum;
            Interlocked.Add(ref _samplesSeen, count);
            //Smoothed over about a third of a second
            int frames = count / Channels;
            if (frames > 0)
            {
                double keep = Math.Exp(-frames / (SampleRate * 0.3));
                _recentPower = _recentPower * keep + (sum / count) * (1 - keep);
            }
            Buffer.BlockCopy(samples, 0, bytes, 0, count * 4);
            file.Write(bytes, 0, count * 4);
            Interlocked.Add(ref _frames, frames);
        }

        private string Comment()
        {
            StringBuilder text = new StringBuilder("OpenCAGE game audio");
            if (StartUtc.HasValue) text.Append("; start_utc=").Append(StartUtc.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"));
            text.Append("; pid=").Append(ProcessId);
            if (!string.IsNullOrEmpty(Label)) text.Append("; level=").Append(Label.Replace(";", ","));
            if (Gain != 1f) text.Append("; gain=").Append(Gain.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            return text.ToString();
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // WAV layout: RIFF, fmt (float, 18 bytes), fact, LIST/INFO/ICMT (fixed 256 bytes so it can be rewritten), data; then the
        // marks as a cue chunk and a LIST/adtl of labels.
        // ---------------------------------------------------------------------------------------------------------------------

        public const int CommentBytes = 256;
        public const int HeaderBytes = 12 + (8 + 18) + (8 + 4) + (8 + 4 + 8 + CommentBytes) + 8;

        private static void WriteHeader(FileStream file, long dataBytes, string comment)
        {
            long at = file.Position;
            file.Position = 0;
            BinaryWriter w = new BinaryWriter(file, Encoding.ASCII, true);
            long riff = Math.Max(file.Length, HeaderBytes + dataBytes) - 8;
            w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write((uint)Math.Min(riff, uint.MaxValue)); w.Write(Encoding.ASCII.GetBytes("WAVE"));
            w.Write(Encoding.ASCII.GetBytes("fmt ")); w.Write(18);
            w.Write((short)3); w.Write((short)Channels); w.Write(SampleRate); w.Write(SampleRate * BytesPerFrame); w.Write((short)BytesPerFrame); w.Write((short)32); w.Write((short)0);
            w.Write(Encoding.ASCII.GetBytes("fact")); w.Write(4); w.Write((uint)(dataBytes / BytesPerFrame));
            w.Write(Encoding.ASCII.GetBytes("LIST")); w.Write(4 + 8 + CommentBytes); w.Write(Encoding.ASCII.GetBytes("INFO"));
            w.Write(Encoding.ASCII.GetBytes("ICMT")); w.Write(CommentBytes);
            byte[] text = new byte[CommentBytes];
            byte[] written = Encoding.UTF8.GetBytes(comment ?? "");
            Array.Copy(written, text, Math.Min(written.Length, CommentBytes - 1));
            w.Write(text);
            w.Write(Encoding.ASCII.GetBytes("data")); w.Write((uint)Math.Min(dataBytes, uint.MaxValue));
            w.Flush();
            file.Position = Math.Max(at, HeaderBytes);
        }

        private void Finish(FileStream file)
        {
            file.Flush();
            long dataBytes = Frames * BytesPerFrame;
            file.Position = HeaderBytes + dataBytes;
            List<Mark> marks = Marks;
            if (marks.Count != 0)
            {
                BinaryWriter w = new BinaryWriter(file, Encoding.ASCII, true);
                if ((dataBytes & 1) != 0) w.Write((byte)0);
                w.Write(Encoding.ASCII.GetBytes("cue ")); w.Write(4 + 24 * marks.Count); w.Write(marks.Count);
                for (int i = 0; i < marks.Count; i++)
                {
                    uint position = (uint)Math.Min(marks[i].Frame, uint.MaxValue);
                    w.Write(i + 1); w.Write(position); w.Write(Encoding.ASCII.GetBytes("data")); w.Write(0); w.Write(0); w.Write(position);
                }
                List<byte[]> labels = marks.Select(o => Encoding.UTF8.GetBytes(o.Label + "\0")).ToList();
                int listSize = 4 + labels.Sum(o => 8 + 4 + o.Length + ((4 + o.Length) & 1));
                w.Write(Encoding.ASCII.GetBytes("LIST")); w.Write(listSize); w.Write(Encoding.ASCII.GetBytes("adtl"));
                for (int i = 0; i < labels.Count; i++)
                {
                    w.Write(Encoding.ASCII.GetBytes("labl")); w.Write(4 + labels[i].Length); w.Write(i + 1); w.Write(labels[i]);
                    if (((4 + labels[i].Length) & 1) != 0) w.Write((byte)0);
                }
                w.Flush();
            }
            file.SetLength(file.Position);
            WriteHeader(file, dataBytes, Comment());
            file.Flush();
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // Windows
        // ---------------------------------------------------------------------------------------------------------------------

        private static IAudioClient Activate(int pid)
        {
            //AUDIOCLIENT_ACTIVATION_PARAMS { ActivationType = PROCESS_LOOPBACK, { TargetProcessId, INCLUDE_TARGET_PROCESS_TREE } }
            IntPtr parameters = Marshal.AllocHGlobal(12);
            IntPtr variant = Marshal.AllocHGlobal(24);
            try
            {
                Marshal.WriteInt32(parameters, 0, 1);
                Marshal.WriteInt32(parameters, 4, pid);
                Marshal.WriteInt32(parameters, 8, 0);
                for (int i = 0; i < 24; i++) Marshal.WriteByte(variant, i, 0);
                Marshal.WriteInt16(variant, 0, 65); //VT_BLOB { cbSize, pBlobData }
                Marshal.WriteInt32(variant, 8, 12);
                Marshal.WriteIntPtr(variant, IntPtr.Size == 8 ? 16 : 12, parameters);

                CompletionHandler handler = new CompletionHandler();
                int hr;
                IActivateAudioInterfaceAsyncOperation operation;
                try
                {
                    hr = ActivateAudioInterfaceAsync("VAD\\Process_Loopback", typeof(IAudioClient).GUID, variant, handler, out operation);
                }
                catch (EntryPointNotFoundException)
                {
                    throw new GameAudioException(NeedsWindows("this version of Windows has no ActivateAudioInterfaceAsync"));
                }
                if (hr != 0)
                    throw new GameAudioException(NeedsWindows("Windows refused to record one program (" + Hex(hr) + ")"));
                if (!handler.Done.WaitOne(10000))
                    throw new GameAudioException("Windows never finished setting up the recording.");
                operation.GetActivateResult(out int result, out object activated);
                Release(operation);
                if (result != 0 || activated == null)
                    throw new GameAudioException(NeedsWindows("Windows could not record the game's audio (" + Hex(result) + ")"));
                return (IAudioClient)activated;
            }
            finally
            {
                Marshal.FreeHGlobal(variant);
                Marshal.FreeHGlobal(parameters);
            }
        }

        private static string NeedsWindows(string what)
        {
            int build = WindowsBuild();
            return what + (build > 0 && build < 20348 ? ". Recording one program's audio needs Windows 11 (or Windows 10 build 20348 or later); this PC runs build " + build + "." : ".");
        }

        public static int WindowsBuild()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                    return int.TryParse(key?.GetValue("CurrentBuildNumber") as string, out int build) ? build : 0;
            }
            catch (Exception) { return 0; }
        }

        private static void Check(int hr, string what)
        {
            if (hr < 0)
            {
                //AUDCLNT_E_DEVICE_INVALIDATED and friends: the audio device went away
                if ((uint)hr == 0x88890004) throw new GameAudioException("The audio device went away while recording (could not " + what + ").");
                throw new GameAudioException("Windows could not " + what + " (" + Hex(hr) + ").");
            }
        }

        private static string Hex(int hr) => "0x" + ((uint)hr).ToString("X8");

        private static void Release(object com)
        {
            try { if (com != null && Marshal.IsComObject(com)) Marshal.ReleaseComObject(com); }
            catch (Exception) { }
        }

        private static bool AllZero(float[] samples, int count)
        {
            for (int i = 0; i < count; i++) if (samples[i] != 0f) return false;
            return true;
        }

        public static double ToDb(double power) => power <= 1e-20 ? -200 : 10 * Math.Log10(power);

        [DllImport("avrt.dll", CharSet = CharSet.Unicode)] private static extern IntPtr AvSetMmThreadCharacteristics(string taskName, ref uint taskIndex);
        [DllImport("avrt.dll")] private static extern bool AvRevertMmThreadCharacteristics(IntPtr handle);
        [DllImport("kernel32.dll")] private static extern bool QueryPerformanceCounter(out long count);
        [DllImport("kernel32.dll")] private static extern bool QueryPerformanceFrequency(out long frequency);
        [DllImport("kernel32.dll")] private static extern void GetSystemTimePreciseAsFileTime(out long fileTime);

        private static readonly long _qpcFrequency = QpcFrequency();
        private static long QpcFrequency() { QueryPerformanceFrequency(out long f); return f; }

        /// <summary>The performance counter in 100 ns units, as Windows stamps captured audio.</summary>
        public static long Qpc100ns()
        {
            QueryPerformanceCounter(out long count);
            return (long)(count * (1e7 / _qpcFrequency));
        }

        /// <summary>The wall clock to well under a millisecond (DateTime.UtcNow can be 15 ms coarse).</summary>
        public static DateTime PreciseUtcNow()
        {
            try
            {
                GetSystemTimePreciseAsFileTime(out long fileTime);
                return DateTime.FromFileTimeUtc(fileTime);
            }
            catch (EntryPointNotFoundException)
            {
                return DateTime.UtcNow;
            }
        }

        [DllImport("Mmdevapi.dll", ExactSpelling = true)]
        private static extern int ActivateAudioInterfaceAsync([MarshalAs(UnmanagedType.LPWStr)] string path, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, IntPtr activationParams, IActivateAudioInterfaceCompletionHandler handler, out IActivateAudioInterfaceAsyncOperation operation);

        [ComVisible(true)]
        private sealed class CompletionHandler : IActivateAudioInterfaceCompletionHandler, IAgileObject
        {
            public readonly ManualResetEvent Done = new ManualResetEvent(false);
            public void ActivateCompleted(IActivateAudioInterfaceAsyncOperation operation) { Done.Set(); }
        }

        [ComImport, Guid("41D949AB-9862-444A-80F6-C261334DA5EB"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IActivateAudioInterfaceCompletionHandler
        {
            void ActivateCompleted(IActivateAudioInterfaceAsyncOperation operation);
        }

        [ComImport, Guid("72A22D78-CDE4-431D-B8CC-843A71199B6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IActivateAudioInterfaceAsyncOperation
        {
            void GetActivateResult(out int hr, [MarshalAs(UnmanagedType.IUnknown)] out object activated);
        }

        [ComImport, Guid("94ea2b94-e9cc-49e0-c0ff-ee64ca8f5b90"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IAgileObject { }

        [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IAudioClient
        {
            [PreserveSig] int Initialize(int shareMode, int flags, long bufferDuration, long periodicity, IntPtr format, IntPtr session);
            [PreserveSig] int GetBufferSize(out uint frames);
            [PreserveSig] int GetStreamLatency(out long latency);
            [PreserveSig] int GetCurrentPadding(out uint frames);
            [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closest);
            [PreserveSig] int GetMixFormat(out IntPtr format);
            [PreserveSig] int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
            [PreserveSig] int Start();
            [PreserveSig] int Stop();
            [PreserveSig] int Reset();
            [PreserveSig] int SetEventHandle(IntPtr handle);
            [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
        }

        [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IAudioCaptureClient
        {
            [PreserveSig] int GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong devicePosition, out ulong qpcPosition);
            [PreserveSig] int ReleaseBuffer(uint frames);
            [PreserveSig] int GetNextPacketSize(out uint frames);
        }
    }

    internal sealed class GameAudioException : Exception
    {
        public GameAudioException(string message) : base(message) { }
        public GameAudioException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// The game's Windows audio sessions (as the Volume Mixer shows them): volume and mute. Windows keeps both per program, so a game
    /// muted once stays muted and records as silence. <see cref="Keeper"/> turns them down for a quiet recording and restores them; a
    /// restore that could not happen is kept on disk and done when that game next runs (<see cref="RestorePending"/>).
    /// </summary>
    internal static class GameAudioSessions
    {
        public sealed class State
        {
            public int Sessions;
            public float Volume = 1f;
            public bool Muted;
            public string Describe()
            {
                if (Sessions == 0) return "no audio session yet";
                return (Muted ? "muted in Windows' volume mixer" : "not muted") + ", volume " + Math.Round(Volume * 100, 1) + "%";
            }
        }

        /// <summary>The process's audio sessions on every active output device (the first one's volume and mute; Sessions = 0: none).</summary>
        public static State Read(int pid)
        {
            State state = new State();
            ForEach(pid, (volume, device) =>
            {
                volume.GetMasterVolume(out float level);
                volume.GetMute(out int muted);
                if (state.Sessions == 0) { state.Volume = level; state.Muted = muted != 0; }
                else state.Muted |= muted != 0;
                state.Sessions++;
            });
            return state;
        }

        /// <summary>Every session of the process set to this volume and mute; returns how many there were.</summary>
        public static int Set(int pid, float level, bool mute)
        {
            int count = 0;
            ForEach(pid, (volume, device) =>
            {
                Guid none = Guid.Empty;
                volume.GetMasterVolume(out float now);
                if (Math.Abs(now - level) > 1e-6f) volume.SetMasterVolume(level, ref none);
                volume.GetMute(out int muted);
                if ((muted != 0) != mute) volume.SetMute(mute ? 1 : 0, ref none);
                count++;
            });
            return count;
        }

        private static void ForEach(int pid, Action<ISimpleAudioVolume, int> act)
        {
            object enumeratorObject = null;
            try
            {
                enumeratorObject = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")));
                IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)enumeratorObject;
                if (enumerator.EnumAudioEndpoints(0 /*render*/, 1 /*active*/, out IMMDeviceCollection devices) != 0) return;
                devices.GetCount(out int count);
                for (int d = 0; d < count; d++)
                {
                    if (devices.Item(d, out IMMDevice device) != 0) continue;
                    Guid iid = typeof(IAudioSessionManager2).GUID;
                    if (device.Activate(ref iid, 1 /*CLSCTX_INPROC_SERVER*/, IntPtr.Zero, out object managerObject) != 0) { Marshal.ReleaseComObject(device); continue; }
                    IAudioSessionManager2 manager = (IAudioSessionManager2)managerObject;
                    if (manager.GetSessionEnumerator(out IAudioSessionEnumerator sessions) == 0)
                    {
                        sessions.GetCount(out int n);
                        for (int i = 0; i < n; i++)
                        {
                            if (sessions.GetSession(i, out IAudioSessionControl2 session) != 0) continue;
                            try
                            {
                                if (session.GetProcessId(out uint owner) == 0 && owner == pid)
                                    act((ISimpleAudioVolume)session, d);
                            }
                            catch (Exception) { }
                            Marshal.ReleaseComObject(session);
                        }
                        Marshal.ReleaseComObject(sessions);
                    }
                    Marshal.ReleaseComObject(manager);
                    Marshal.ReleaseComObject(device);
                }
                Marshal.ReleaseComObject(devices);
            }
            catch (Exception e)
            {
                Debug.Log("GameAudio", "audio sessions: " + e.Message);
            }
            finally
            {
                if (enumeratorObject != null) Marshal.ReleaseComObject(enumeratorObject);
            }
        }

        /// <summary>
        /// Holds a process's sessions at a volume (unmuted) during a quiet recording, checking often at first so a session created later
        /// is caught before it is heard, and restores the original at the end. The pending restore is kept on disk until done
        /// (<see cref="RestorePending"/>).
        /// </summary>
        public sealed class Keeper
        {
            private readonly int _pid;
            private readonly float _level;
            private State _original;
            /// <summary>What the session was before it was turned down (null until the program has one).</summary>
            public State Original => _original;
            private string _exe;
            private readonly Stopwatch _since = Stopwatch.StartNew();
            private long _lastTick = -1000;

            public Keeper(int pid, float level)
            {
                _pid = pid;
                _level = level;
            }

            public void Begin()
            {
                try { using (Process p = Process.GetProcessById(_pid)) _exe = p.MainModule.FileName; } catch (Exception) { }
                Tick();
            }

            /// <summary>Called often: the first two minutes every call, then once a second.</summary>
            public void Tick()
            {
                long now = _since.ElapsedMilliseconds;
                if (now > 120000 && now - _lastTick < 1000) return;
                _lastTick = now;
                if (_original == null)
                {
                    State found = Read(_pid);
                    if (found.Sessions == 0) return;
                    _original = found;
                    SavePending(_exe, found);
                }
                Set(_pid, _level, false);
            }

            /// <summary>Puts back what it found. Returns a sentence for the user when that could not be done now, else null.</summary>
            public string End()
            {
                if (_original == null) return null;
                if (Set(_pid, _original.Volume, _original.Muted) != 0)
                {
                    ClearPending(_exe);
                    return null;
                }
                return "The game closed before its Windows volume could be put back (" + Math.Round(_original.Volume * 100) + "%" + (_original.Muted ? ", muted" : "") + "): OpenCAGE puts it back the next time it starts it.";
            }
        }

        /// <summary>The file holding pending volume restores (tests set their own).</summary>
        internal static string PendingFileOverride = null;
        private static string PendingFile => PendingFileOverride ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenCAGE", "game_audio_volume.txt");

        //One line a program: volume|muted|exe path
        private static Dictionary<string, State> LoadPending()
        {
            Dictionary<string, State> pending = new Dictionary<string, State>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(PendingFile)) return pending;
                foreach (string line in File.ReadAllLines(PendingFile))
                {
                    string[] parts = line.Split(new[] { '|' }, 3);
                    if (parts.Length == 3 && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float volume))
                        pending[parts[2]] = new State() { Sessions = 1, Volume = volume, Muted = parts[1] == "1" };
                }
            }
            catch (Exception) { }
            return pending;
        }

        private static void SavePending(Dictionary<string, State> pending)
        {
            try
            {
                if (pending.Count == 0) { if (File.Exists(PendingFile)) File.Delete(PendingFile); return; }
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PendingFile));
                File.WriteAllLines(PendingFile, pending.Select(o => o.Value.Volume.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" + (o.Value.Muted ? "1" : "0") + "|" + o.Key));
            }
            catch (Exception) { }
        }

        private static readonly object _pendingLock = new object();

        private static void SavePending(string exe, State original)
        {
            if (string.IsNullOrEmpty(exe)) return;
            lock (_pendingLock)
            {
                Dictionary<string, State> pending = LoadPending();
                //A restore still pending from before holds the real original, so keep it
                if (!pending.ContainsKey(exe)) pending[exe] = original;
                SavePending(pending);
            }
        }

        private static void ClearPending(string exe)
        {
            if (string.IsNullOrEmpty(exe)) return;
            lock (_pendingLock)
            {
                Dictionary<string, State> pending = LoadPending();
                if (pending.Remove(exe)) SavePending(pending);
            }
        }

        /// <summary>The volume a quiet recording took from this program and could not give back yet (null: none).</summary>
        public static State Pending(string exe)
        {
            lock (_pendingLock)
                return exe != null && LoadPending().TryGetValue(exe, out State state) ? state : null;
        }

        /// <summary>
        /// Puts back the volume a quiet recording owed this program, on its running process <paramref name="pid"/> (once it has an
        /// audio session). Returns whether it did.
        /// </summary>
        public static bool RestorePending(string exe, int pid)
        {
            State owed = Pending(exe);
            if (owed == null) return false;
            if (Set(pid, owed.Volume, owed.Muted) == 0) return false;
            ClearPending(exe);
            return true;
        }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
        }

        [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceCollection
        {
            [PreserveSig] int GetCount(out int count);
            [PreserveSig] int Item(int index, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        }

        [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionManager2
        {
            [PreserveSig] int GetAudioSessionControl(IntPtr guid, int flags, out IntPtr control);
            [PreserveSig] int GetSimpleAudioVolume(IntPtr guid, int flags, out IntPtr volume);
            [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessions);
        }

        [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionEnumerator
        {
            [PreserveSig] int GetCount(out int count);
            [PreserveSig] int GetSession(int index, out IAudioSessionControl2 session);
        }

        [ComImport, Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionControl2
        {
            [PreserveSig] int GetState(out int state);
            [PreserveSig] int GetDisplayName(out IntPtr name);
            [PreserveSig] int SetDisplayName(IntPtr name, IntPtr context);
            [PreserveSig] int GetIconPath(out IntPtr path);
            [PreserveSig] int SetIconPath(IntPtr path, IntPtr context);
            [PreserveSig] int GetGroupingParam(out Guid group);
            [PreserveSig] int SetGroupingParam(IntPtr group, IntPtr context);
            [PreserveSig] int RegisterAudioSessionNotification(IntPtr client);
            [PreserveSig] int UnregisterAudioSessionNotification(IntPtr client);
            [PreserveSig] int GetSessionIdentifier(out IntPtr id);
            [PreserveSig] int GetSessionInstanceIdentifier(out IntPtr id);
            [PreserveSig] int GetProcessId(out uint pid);
        }

        [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ISimpleAudioVolume
        {
            [PreserveSig] int SetMasterVolume(float level, ref Guid context);
            [PreserveSig] int GetMasterVolume(out float level);
            [PreserveSig] int SetMute(int mute, ref Guid context);
            [PreserveSig] int GetMute(out int mute);
        }
    }
}

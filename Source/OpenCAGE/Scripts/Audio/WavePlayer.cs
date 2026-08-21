using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// Plays decoded samples through the default output device.
    ///
    /// Built on waveOut rather than SoundPlayer because a preview needs to pause, scrub and report
    /// where it has got to, none of which SoundPlayer offers. Buffers are queued a fifth of a second at
    /// a time from a worker thread and polled for completion, which avoids taking a callback from the
    /// audio thread into managed code.
    /// </summary>
    public sealed class WavePlayer : IDisposable
    {
        private const int BufferCount = 4;
        private const int MillisecondsPerBuffer = 200;

        private const int WaveMapper = -1;
        private const int CallbackNull = 0x00000000;
        private const int WhdrDone = 0x00000001;
        private const int WhdrPrepared = 0x00000002;
        private const int TimeBytes = 0x0004;

        [StructLayout(LayoutKind.Sequential)]
        private struct WaveFormat
        {
            public ushort FormatTag;
            public ushort Channels;
            public uint SamplesPerSecond;
            public uint AverageBytesPerSecond;
            public ushort BlockAlign;
            public ushort BitsPerSample;
            public ushort Size;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WaveHeader
        {
            public IntPtr Data;
            public uint BufferLength;
            public uint BytesRecorded;
            public IntPtr User;
            public uint Flags;
            public uint Loops;
            public IntPtr Next;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MmTime
        {
            public uint Type;
            public uint Value;
            public uint Padding;
        }

        [DllImport("winmm.dll")]
        private static extern int waveOutOpen(out IntPtr device, int deviceId, ref WaveFormat format, IntPtr callback, IntPtr instance, int flags);

        [DllImport("winmm.dll")]
        private static extern int waveOutClose(IntPtr device);

        [DllImport("winmm.dll")]
        private static extern int waveOutPrepareHeader(IntPtr device, IntPtr header, int size);

        [DllImport("winmm.dll")]
        private static extern int waveOutUnprepareHeader(IntPtr device, IntPtr header, int size);

        [DllImport("winmm.dll")]
        private static extern int waveOutWrite(IntPtr device, IntPtr header, int size);

        [DllImport("winmm.dll")]
        private static extern int waveOutPause(IntPtr device);

        [DllImport("winmm.dll")]
        private static extern int waveOutRestart(IntPtr device);

        [DllImport("winmm.dll")]
        private static extern int waveOutReset(IntPtr device);

        [DllImport("winmm.dll")]
        private static extern int waveOutSetVolume(IntPtr device, uint volume);

        [DllImport("winmm.dll")]
        private static extern int waveOutGetPosition(IntPtr device, ref MmTime time, int size);

        private readonly DecodedAudio _audio;
        private readonly int _channels;
        private readonly int _sampleRate;
        private readonly int _blockAlign;
        private readonly int _bufferBytes;
        private readonly int _headerSize;
        private readonly object _lock = new object();
        private readonly byte[] _staging;

        private IntPtr _device;
        private IntPtr[] _headers;
        private IntPtr[] _buffers;
        private Thread _worker;

        private volatile bool _disposed;
        private volatile bool _playing;
        private int _readPosition;
        private int _basePosition;
        private bool _finished;

        /// <summary>Raised on a worker thread when the sound reaches its end.</summary>
        public event EventHandler PlaybackEnded;

        public WavePlayer(DecodedAudio audio)
        {
            if (audio == null || audio.Capacity == 0 || audio.Channels == 0 || audio.SampleRate == 0)
                throw new ArgumentException("There is nothing to play.", "audio");

            _audio = audio;
            _channels = audio.Channels;
            _sampleRate = audio.SampleRate;
            _blockAlign = _channels * 2;

            int bytes = _sampleRate * _blockAlign * MillisecondsPerBuffer / 1000;
            _bufferBytes = Math.Max(_blockAlign, bytes - bytes % _blockAlign);
            _staging = new byte[_bufferBytes];
            _headerSize = Marshal.SizeOf(typeof(WaveHeader));

            Open();
        }

        public TimeSpan Duration
        {
            get { return _audio.Duration; }
        }

        public bool IsPlaying
        {
            get { return _playing; }
        }

        /// <summary>Where the sound has actually got to, as opposed to how much has been queued.</summary>
        public TimeSpan Position
        {
            get
            {
                lock (_lock)
                {
                    int position = _basePosition + DevicePosition();
                    if (position > _audio.Length)
                        position = _audio.Length;

                    return TimeSpan.FromSeconds((double)position / (_sampleRate * _blockAlign));
                }
            }
        }

        /// <summary>0 to 1.</summary>
        public float Volume
        {
            set
            {
                if (_device == IntPtr.Zero)
                    return;

                float clamped = value < 0 ? 0 : (value > 1 ? 1 : value);
                uint level = (uint)(clamped * 0xFFFF);
                waveOutSetVolume(_device, level | (level << 16));
            }
        }

        private void Open()
        {
            WaveFormat format = new WaveFormat
            {
                FormatTag = 1, //PCM
                Channels = (ushort)_channels,
                SamplesPerSecond = (uint)_sampleRate,
                AverageBytesPerSecond = (uint)(_sampleRate * _blockAlign),
                BlockAlign = (ushort)_blockAlign,
                BitsPerSample = 16,
                Size = 0,
            };

            int result = waveOutOpen(out _device, WaveMapper, ref format, IntPtr.Zero, IntPtr.Zero, CallbackNull);
            if (result != 0)
                throw new InvalidOperationException("Could not open an audio device (error " + result + ").");

            _headers = new IntPtr[BufferCount];
            _buffers = new IntPtr[BufferCount];
            for (int i = 0; i < BufferCount; i++)
            {
                _buffers[i] = Marshal.AllocHGlobal(_bufferBytes);
                _headers[i] = Marshal.AllocHGlobal(_headerSize);

                //Zeroed so the first pass sees an unprepared, not-done header
                for (int b = 0; b < _headerSize; b++)
                    Marshal.WriteByte(_headers[i], b, 0);
            }
        }

        public void Play()
        {
            lock (_lock)
            {
                if (_disposed || _device == IntPtr.Zero)
                    return;

                if (_playing)
                    return;

                if (_finished)
                    SeekLocked(TimeSpan.Zero);

                _playing = true;
                waveOutRestart(_device);

                if (_worker == null)
                {
                    _worker = new Thread(Pump) { IsBackground = true, Name = "Sound preview" };
                    _worker.Start();
                }
            }
        }

        public void Pause()
        {
            lock (_lock)
            {
                if (_disposed || _device == IntPtr.Zero || !_playing)
                    return;

                _playing = false;
                waveOutPause(_device);
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (_disposed || _device == IntPtr.Zero)
                    return;

                _playing = false;
                SeekLocked(TimeSpan.Zero);
            }
        }

        public void Seek(TimeSpan position)
        {
            lock (_lock)
            {
                if (_disposed || _device == IntPtr.Zero)
                    return;

                bool wasPlaying = _playing;
                SeekLocked(position);

                if (wasPlaying)
                {
                    _playing = true;
                    waveOutRestart(_device);
                }
            }
        }

        private void SeekLocked(TimeSpan position)
        {
            int bytes = (int)(position.TotalSeconds * _sampleRate * _blockAlign);
            if (bytes < 0) bytes = 0;

            //Seeking into audio that hasn't decoded yet would play silence, so stop at the decoded edge
            if (bytes > _audio.Length) bytes = _audio.Length;
            bytes -= bytes % _blockAlign;

            //Reset drops everything queued and returns the device's own clock to zero, so the offset it
            //is measured from has to move with it
            waveOutReset(_device);
            ReleaseHeaders();

            _readPosition = bytes;
            _basePosition = bytes;
            _finished = false;
        }

        private void Pump()
        {
            while (!_disposed)
            {
                bool ended = false;

                lock (_lock)
                {
                    if (_playing && _device != IntPtr.Zero)
                    {
                        Queue();

                        //Only really finished once the decoder has stopped producing too - running dry
                        //mid-sound just means playback has caught up with the decode
                        if (_audio.Complete && _readPosition >= _audio.Length && !AnyQueued() && !_finished)
                        {
                            _finished = true;
                            _playing = false;
                            ended = true;
                        }
                    }
                }

                if (ended)
                {
                    EventHandler handler = PlaybackEnded;
                    if (handler != null)
                        handler(this, EventArgs.Empty);
                }

                Thread.Sleep(20);
            }
        }

        private void Queue()
        {
            for (int i = 0; i < BufferCount; i++)
            {
                WaveHeader header = (WaveHeader)Marshal.PtrToStructure(_headers[i], typeof(WaveHeader));

                bool prepared = (header.Flags & WhdrPrepared) != 0;
                bool done = (header.Flags & WhdrDone) != 0;

                if (prepared && !done)
                    continue; //still playing

                if (prepared)
                {
                    waveOutUnprepareHeader(_device, _headers[i], _headerSize);
                    for (int b = 0; b < _headerSize; b++)
                        Marshal.WriteByte(_headers[i], b, 0);
                }

                int length = _audio.Read(_readPosition, _staging, 0, _bufferBytes);
                if (length <= 0)
                    return; //Either the end, or the decoder hasn't got this far yet

                //Hold back a partial buffer while there is still more to come, so a slow decode doesn't
                //queue up a sliver and click
                if (length < _bufferBytes && !_audio.Complete)
                    return;

                Marshal.Copy(_staging, 0, _buffers[i], length);
                _readPosition += length;

                WaveHeader fresh = new WaveHeader
                {
                    Data = _buffers[i],
                    BufferLength = (uint)length,
                };

                Marshal.StructureToPtr(fresh, _headers[i], false);

                if (waveOutPrepareHeader(_device, _headers[i], _headerSize) != 0)
                    return;

                if (waveOutWrite(_device, _headers[i], _headerSize) != 0)
                    return;
            }
        }

        private bool AnyQueued()
        {
            for (int i = 0; i < BufferCount; i++)
            {
                WaveHeader header = (WaveHeader)Marshal.PtrToStructure(_headers[i], typeof(WaveHeader));
                if ((header.Flags & WhdrPrepared) != 0 && (header.Flags & WhdrDone) == 0)
                    return true;
            }

            return false;
        }

        private int DevicePosition()
        {
            if (_device == IntPtr.Zero)
                return 0;

            MmTime time = new MmTime { Type = TimeBytes };
            if (waveOutGetPosition(_device, ref time, Marshal.SizeOf(typeof(MmTime))) != 0)
                return 0;

            return time.Type == TimeBytes ? (int)time.Value : 0;
        }

        private void ReleaseHeaders()
        {
            if (_headers == null)
                return;

            for (int i = 0; i < BufferCount; i++)
            {
                WaveHeader header = (WaveHeader)Marshal.PtrToStructure(_headers[i], typeof(WaveHeader));
                if ((header.Flags & WhdrPrepared) != 0)
                    waveOutUnprepareHeader(_device, _headers[i], _headerSize);

                for (int b = 0; b < _headerSize; b++)
                    Marshal.WriteByte(_headers[i], b, 0);
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                    return;

                _disposed = true;
                _playing = false;

                if (_device != IntPtr.Zero)
                {
                    waveOutReset(_device);
                    ReleaseHeaders();
                    waveOutClose(_device);
                    _device = IntPtr.Zero;
                }

                if (_buffers != null)
                {
                    for (int i = 0; i < BufferCount; i++)
                    {
                        if (_buffers[i] != IntPtr.Zero) Marshal.FreeHGlobal(_buffers[i]);
                        if (_headers[i] != IntPtr.Zero) Marshal.FreeHGlobal(_headers[i]);
                    }

                    _buffers = null;
                    _headers = null;
                }
            }
        }
    }
}

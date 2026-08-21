using System;
using System.IO;
using System.Runtime.InteropServices;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// Reads samples out of an Ogg Vorbis stream held in memory, using libvorbis.
    ///
    /// Deliberately narrow: open, ask for the format, pull interleaved float frames until the stream
    /// ends. That is all the preview needs, and it keeps the amount of hand-written interop small.
    /// </summary>
    internal sealed class VorbisReader : IDisposable
    {
        private readonly byte[] _data;
        private readonly IntPtr _handle;
        private readonly GCHandle _self;

        //Held as fields for the lifetime of the reader. If these are allowed to be collected while
        //libvorbis still holds the function pointers, the next read walks into freed memory.
        private readonly VorbisNative.OvCallbacks _callbacks;
        private readonly VorbisNative.ReadFunc _read;
        private readonly VorbisNative.SeekFunc _seek;
        private readonly VorbisNative.CloseFunc _close;
        private readonly VorbisNative.TellFunc _tell;

        private int _position;
        private bool _disposed;

        public int Channels { get; private set; }
        public int SampleRate { get; private set; }

        /// <summary>Frames in the stream, or 0 if it doesn't say.</summary>
        public long TotalSamples { get; private set; }

        public VorbisReader(byte[] ogg)
        {
            VorbisNative.EnsureLoaded();

            _data = ogg;
            _self = GCHandle.Alloc(this);

            _read = Read;
            _seek = Seek;
            _close = Close;
            _tell = Tell;
            _callbacks = new VorbisNative.OvCallbacks { Read = _read, Seek = _seek, Close = _close, Tell = _tell };

            _handle = Marshal.AllocHGlobal(VorbisNative.FileHandleSize);
            for (int i = 0; i < VorbisNative.FileHandleSize; i++)
                Marshal.WriteByte(_handle, i, 0);

            int result = VorbisNative.ov_open_callbacks(GCHandle.ToIntPtr(_self), _handle, IntPtr.Zero, IntPtr.Zero, _callbacks);
            if (result < 0)
            {
                Cleanup(false);
                throw new InvalidDataException("Could not open the rebuilt audio stream (libvorbis error " + result + ").");
            }

            IntPtr info = VorbisNative.ov_info(_handle, -1);
            if (info == IntPtr.Zero)
            {
                Cleanup(true);
                throw new InvalidDataException("The rebuilt audio stream has no format information.");
            }

            int channels, rate;
            VorbisNative.ReadInfo(info, out channels, out rate);
            Channels = channels;
            SampleRate = rate;
            TotalSamples = VorbisNative.ov_pcm_total(_handle, -1);

            if (Channels <= 0 || Channels > 8 || SampleRate <= 0)
            {
                Cleanup(true);
                throw new InvalidDataException("The rebuilt audio stream declares " + Channels + " channels at " + SampleRate + "Hz.");
            }
        }

        /// <summary>
        /// Fill <paramref name="buffer"/> with interleaved samples. Returns the number of floats
        /// written, always a whole number of frames, and 0 at the end of the stream.
        /// </summary>
        public int ReadSamples(float[] buffer, int offset, int count)
        {
            if (_disposed || count <= 0)
                return 0;

            int frames = count / Channels;
            if (frames <= 0)
                return 0;

            IntPtr channels;
            int bitstream;
            int read = VorbisNative.ov_read_float(_handle, out channels, frames, out bitstream);

            //Negative is a recoverable stream error; libvorbis expects the caller to just ask again, but
            //for a preview it is better to stop than to spin
            if (read <= 0)
                return 0;

            //ov_read_float hands back one pointer per channel, each to that channel's own block, so the
            //interleaving the rest of the pipeline wants has to be done here. Each plane is copied in
            //one go and then scattered, rather than marshalling a sample at a time.
            if (_plane == null || _plane.Length < read)
                _plane = new float[read];

            for (int channel = 0; channel < Channels; channel++)
            {
                IntPtr plane = Marshal.ReadIntPtr(channels, channel * IntPtr.Size);
                Marshal.Copy(plane, _plane, 0, read);

                int target = offset + channel;
                for (int frame = 0; frame < read; frame++)
                {
                    buffer[target] = _plane[frame];
                    target += Channels;
                }
            }

            return read * Channels;
        }

        private float[] _plane;

        #region CALLBACKS

        private static VorbisReader From(IntPtr source)
        {
            return (VorbisReader)GCHandle.FromIntPtr(source).Target;
        }

        private static UIntPtr Read(IntPtr buffer, UIntPtr size, UIntPtr count, IntPtr source)
        {
            VorbisReader reader = From(source);
            ulong itemSize = (ulong)size;
            ulong items = (ulong)count;
            if (itemSize == 0 || items == 0)
                return UIntPtr.Zero;

            long wanted = (long)(itemSize * items);
            long remaining = reader._data.Length - reader._position;
            if (wanted <= 0 || remaining <= 0)
                return UIntPtr.Zero;

            //Only whole items may be reported, so trim the request down to a multiple of the item size
            long take = Math.Min(wanted, remaining);
            take -= take % (long)itemSize;
            if (take <= 0)
                return UIntPtr.Zero;

            Marshal.Copy(reader._data, reader._position, buffer, (int)take);
            reader._position += (int)take;

            //The return is a count of items, not of bytes
            return (UIntPtr)((ulong)take / itemSize);
        }

        private static int Seek(IntPtr source, long offset, int whence)
        {
            VorbisReader reader = From(source);

            long position;
            switch (whence)
            {
                case 0: position = offset; break;                       //SEEK_SET
                case 1: position = reader._position + offset; break;    //SEEK_CUR
                case 2: position = reader._data.Length + offset; break; //SEEK_END
                default: return -1;
            }

            if (position < 0 || position > reader._data.Length)
                return -1;

            reader._position = (int)position;
            return 0;
        }

        private static int Close(IntPtr source)
        {
            return 0;
        }

        private static int Tell(IntPtr source)
        {
            return From(source)._position;
        }

        #endregion

        private void Cleanup(bool clearVorbis)
        {
            if (clearVorbis && _handle != IntPtr.Zero)
                VorbisNative.ov_clear(_handle);

            if (_handle != IntPtr.Zero)
                Marshal.FreeHGlobal(_handle);

            if (_self.IsAllocated)
                _self.Free();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Cleanup(true);
        }
    }
}

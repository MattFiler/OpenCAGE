using System;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// Reads Vorbis' bit packing: values are packed least significant bit first, and run across byte
    /// boundaries without padding. Wwise keeps the same convention, which is what makes rebuilding a
    /// stream a matter of moving bits rather than re-encoding anything.
    ///
    /// Values are assembled a byte at a time rather than a bit at a time. Rebuilding a long ambience
    /// moves a couple of million values, and the difference between the two is seconds.
    /// </summary>
    internal sealed class VorbisBitReader
    {
        private readonly byte[] _data;
        private readonly int _start;
        private readonly int _end;
        private int _position;
        private int _bit;

        public VorbisBitReader(byte[] data, int offset, int length)
        {
            _data = data;
            _start = offset;
            _position = offset;
            _end = offset + length;
        }

        /// <summary>How many bits have been consumed, used to check a codebook was read exactly.</summary>
        public long BitsRead
        {
            get { return (long)(_position - _start) * 8 + _bit; }
        }

        /// <summary>True when the next read starts on a byte boundary.</summary>
        public bool Aligned
        {
            get { return _bit == 0; }
        }

        /// <summary>Where in the backing array the next read would start.</summary>
        public int Position
        {
            get { return _position; }
        }

        public uint Read(int bits)
        {
            if (bits <= 0)
                return 0;
            if (bits > 32)
                throw new ArgumentOutOfRangeException("bits");

            uint value = 0;
            int produced = 0;

            while (produced < bits)
            {
                if (_position >= _end)
                    throw new VorbisEndOfStreamException();

                int available = 8 - _bit;
                int take = Math.Min(available, bits - produced);

                uint chunk = (uint)((_data[_position] >> _bit) & ((1 << take) - 1));
                value |= chunk << produced;

                produced += take;
                _bit += take;
                if (_bit == 8)
                {
                    _bit = 0;
                    _position++;
                }
            }

            return value;
        }
    }

    /// <summary>Thrown when a stream ends mid-value; reported to the user as an unreadable sound.</summary>
    internal sealed class VorbisEndOfStreamException : Exception
    {
        public VorbisEndOfStreamException() : base("Ran off the end of the Vorbis bitstream.")
        {
        }
    }

    /// <summary>The writing half of <see cref="VorbisBitReader"/>.</summary>
    internal sealed class VorbisBitWriter
    {
        private byte[] _output;
        private int _count;

        //Pending bits, least significant first. Held in 64 bits so a 32 bit value can be shifted up by
        //the seven bits that may already be waiting without overflowing.
        private ulong _accumulator;
        private int _bits;

        public VorbisBitWriter(int capacity = 256)
        {
            _output = new byte[Math.Max(16, capacity)];
        }

        public void Write(uint value, int bits)
        {
            if (bits <= 0)
                return;

            ulong masked = bits >= 32 ? value : (value & ((1u << bits) - 1));
            _accumulator |= masked << _bits;
            _bits += bits;

            while (_bits >= 8)
            {
                Append((byte)_accumulator);
                _accumulator >>= 8;
                _bits -= 8;
            }
        }

        /// <summary>
        /// Copy whole bytes through. Falls back to the bit path when the output is mid-byte, which is
        /// the usual case for an audio packet - its header gains a bit or three during the rebuild.
        /// </summary>
        public void WriteBytes(byte[] source, int offset, int count)
        {
            if (count <= 0)
                return;

            if (_bits == 0)
            {
                Reserve(count);
                Buffer.BlockCopy(source, offset, _output, _count, count);
                _count += count;
                return;
            }

            for (int i = 0; i < count; i++)
                Write(source[offset + i], 8);
        }

        /// <summary>Pad out to the next byte boundary, which is where a Vorbis packet ends.</summary>
        public void Flush()
        {
            if (_bits == 0)
                return;

            Append((byte)_accumulator);
            _accumulator = 0;
            _bits = 0;
        }

        public byte[] ToArray()
        {
            Flush();

            byte[] result = new byte[_count];
            Buffer.BlockCopy(_output, 0, result, 0, _count);
            return result;
        }

        private void Append(byte value)
        {
            Reserve(1);
            _output[_count++] = value;
        }

        private void Reserve(int extra)
        {
            if (_count + extra <= _output.Length)
                return;

            int capacity = _output.Length * 2;
            while (capacity < _count + extra)
                capacity *= 2;

            Array.Resize(ref _output, capacity);
        }
    }
}

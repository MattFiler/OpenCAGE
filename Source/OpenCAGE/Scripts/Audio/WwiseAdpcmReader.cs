using System;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// Decodes the Switch build's Wwise ADPCM .wems (wave format 0x8311, introduced in Wwise 2019.1
    /// as the platform ADPCM replacement).
    ///
    /// The format is frame-based: each channel packs 0x24-byte frames, interleaved frame-by-frame
    /// across channels, and a frame is two literal history samples followed by 62 four-bit codes,
    /// low nibble first. Each code indexes a step table for a delta that is added to a second-order
    /// prediction (2*previous - the one before), and carries the next step index with it. Because
    /// every frame opens with its real history, any frame decodes independently - there is no
    /// cross-frame state to get wrong.
    ///
    /// The step table is the reverse-engineered one vgmstream ships for this codec: a bespoke first
    /// row, then a row-doubling law up to the widest pair of rows, which repeat because the deltas
    /// cannot double again inside 16-bit range. Validated by decoding every 0x8311 file in the
    /// Switch build - all of them tile into whole frames and produce clean, sanely-scaled audio.
    /// </summary>
    internal sealed class WwiseAdpcmReader : ISampleReader
    {
        public const ushort FormatAdpcm = 0x8311;

        private readonly byte[] _wem;
        private readonly int _dataOffset;
        private readonly int _frameSize;
        private readonly int _samplesPerFrame;
        private readonly int _blockCount;

        private int _block;
        private float[] _pending;
        private int _pendingPosition;
        private int _pendingLength;

        public int Channels { get; private set; }
        public int SampleRate { get; private set; }
        public long TotalSamples { get; private set; }

        public WwiseAdpcmReader(byte[] wem)
        {
            _wem = wem;

            int formatOffset, formatLength, dataLength;
            WemChunks.Find(wem, out formatOffset, out formatLength, out _dataOffset, out dataLength);

            Channels = BitConverter.ToUInt16(wem, formatOffset + 2);
            SampleRate = (int)BitConverter.ToUInt32(wem, formatOffset + 4);
            int blockAlign = BitConverter.ToUInt16(wem, formatOffset + 12);

            if (Channels <= 0 || Channels > 8 || SampleRate <= 0 || blockAlign <= 0 || blockAlign % Channels != 0)
                throw new InvalidDataException("This ADPCM sound declares " + Channels + " channels with block size " + blockAlign + ".");

            _frameSize = blockAlign / Channels;
            if (_frameSize < 6)
                throw new InvalidDataException("This ADPCM sound's frames are too small to hold a header.");

            //Every full file divides exactly - measured across all of them - so a remainder means a
            //truncated prefetch head, and the whole frames it does hold are still worth playing
            dataLength -= dataLength % blockAlign;

            _samplesPerFrame = 2 + (_frameSize - 5) * 2;
            _blockCount = dataLength / blockAlign;
            TotalSamples = (long)_blockCount * _samplesPerFrame;

            _pending = new float[_samplesPerFrame * Channels];
        }

        public int ReadSamples(float[] buffer, int offset, int count)
        {
            count -= count % Channels;

            int written = 0;
            while (written < count)
            {
                if (_pendingPosition >= _pendingLength)
                {
                    if (_block >= _blockCount)
                        break;

                    DecodeBlock();
                }

                //Whole frames only, so the downmix never sees a torn one
                int take = Math.Min(count - written, _pendingLength - _pendingPosition);
                take -= take % Channels;
                if (take <= 0)
                    break;

                Array.Copy(_pending, _pendingPosition, buffer, offset + written, take);
                _pendingPosition += take;
                written += take;
            }

            return written;
        }

        /// <summary>Decode one frame per channel and interleave them into the pending buffer.</summary>
        private void DecodeBlock()
        {
            int blockOffset = _dataOffset + _block * _frameSize * Channels;

            for (int channel = 0; channel < Channels; channel++)
            {
                int position = blockOffset + channel * _frameSize;

                int hist2 = (short)(_wem[position] | _wem[position + 1] << 8);
                int hist1 = (short)(_wem[position + 2] | _wem[position + 3] << 8);
                int index = _wem[position + 4];
                if (index > 12)
                    index = 12;

                int target = channel;
                _pending[target] = hist2 / 32768f;
                target += Channels;
                _pending[target] = hist1 / 32768f;
                target += Channels;

                for (int i = 0; i < _samplesPerFrame - 2; i++)
                {
                    byte pair = _wem[position + 5 + i / 2];
                    int nibble = (i & 1) == 0 ? pair & 0xF : (pair >> 4) & 0xF;

                    int sample = Delta[index][nibble] + 2 * hist1 - hist2;
                    if (sample > short.MaxValue) sample = short.MaxValue;
                    else if (sample < short.MinValue) sample = short.MinValue;

                    index = Next[index][nibble];
                    hist2 = hist1;
                    hist1 = sample;

                    _pending[target] = sample / 32768f;
                    target += Channels;
                }
            }

            _block++;
            _pendingPosition = 0;
            _pendingLength = _samplesPerFrame * Channels;
        }

        public void Dispose()
        {
        }

        //[stepIndex][nibble] -> delta to add, and the step index the next code uses
        private static readonly int[][] Delta;
        private static readonly int[][] Next;

        static WwiseAdpcmReader()
        {
            Delta = new int[13][];
            Next = new int[13][];

            Delta[0] = new[] { -14, -10, -7, -5, -3, -2, -1, 0, 0, 1, 2, 3, 5, 7, 10, 14 };
            Next[0] = new[] { 2, 2, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 2, 2 };

            int[] deltas = { -28, -20, -14, -10, -7, -5, -3, -1, 1, 3, 5, 7, 10, 14, 20, 28 };
            int[] nexts = { 3, 3, 2, 2, 1, 1, 1, 0, 0, 1, 1, 1, 2, 2, 3, 3 };
            for (int row = 1; row <= 12; row++)
            {
                int shift = Math.Min(row - 1, 10);
                Delta[row] = deltas.Select(d => d << shift).ToArray();
                Next[row] = nexts.Select(n => Math.Min(n + shift, 11)).ToArray();
            }
        }
    }

    /// <summary>
    /// Plain signed-16-bit PCM in a .wem (wave format 0xFFFE). A handful of the Switch build's
    /// bank-embedded sounds use it; no PC media does.
    /// </summary>
    internal sealed class WwisePcmReader : ISampleReader
    {
        public const ushort FormatPcm = 0xFFFE;

        private readonly byte[] _wem;
        private readonly int _dataOffset;
        private readonly int _dataLength;
        private int _position;

        public int Channels { get; private set; }
        public int SampleRate { get; private set; }
        public long TotalSamples { get; private set; }

        public WwisePcmReader(byte[] wem)
        {
            _wem = wem;

            int formatOffset, formatLength;
            WemChunks.Find(wem, out formatOffset, out formatLength, out _dataOffset, out _dataLength);

            Channels = BitConverter.ToUInt16(wem, formatOffset + 2);
            SampleRate = (int)BitConverter.ToUInt32(wem, formatOffset + 4);
            int bits = BitConverter.ToUInt16(wem, formatOffset + 14);

            if (Channels <= 0 || Channels > 8 || SampleRate <= 0 || bits != 16)
                throw new InvalidDataException("This PCM sound declares " + Channels + " channels at " + bits + " bits.");

            _dataLength -= _dataLength % (Channels * 2);
            TotalSamples = _dataLength / (Channels * 2);
        }

        public int ReadSamples(float[] buffer, int offset, int count)
        {
            count -= count % Channels;

            int available = (_dataLength - _position) / 2;
            int take = Math.Min(count, available - available % Channels);

            for (int i = 0; i < take; i++)
            {
                short sample = (short)(_wem[_dataOffset + _position] | _wem[_dataOffset + _position + 1] << 8);
                buffer[offset + i] = sample / 32768f;
                _position += 2;
            }

            return take;
        }

        public void Dispose()
        {
        }
    }

    /// <summary>The two chunks every .wem variant shares, wherever they sit in the file.</summary>
    internal static class WemChunks
    {
        public static ushort FormatTag(byte[] wem)
        {
            int formatOffset, formatLength, dataOffset, dataLength;
            Find(wem, out formatOffset, out formatLength, out dataOffset, out dataLength);
            return BitConverter.ToUInt16(wem, formatOffset);
        }

        public static void Find(byte[] wem, out int formatOffset, out int formatLength, out int dataOffset, out int dataLength)
        {
            formatOffset = -1;
            formatLength = 0;
            dataOffset = -1;
            dataLength = 0;

            if (wem.Length < 12 || Encoding.ASCII.GetString(wem, 0, 4) != "RIFF")
                throw new InvalidDataException("This sound is not a RIFF file.");

            int position = 12;
            while (position + 8 <= wem.Length)
            {
                string id = Encoding.ASCII.GetString(wem, position, 4);
                int size = BitConverter.ToInt32(wem, position + 4);
                if (size < 0)
                    break;

                //A prefetch entry is the head of a streamed file: the data chunk declares its full
                //length, but the bytes stop early. Keep what is actually there.
                bool truncated = position + 8 + size > wem.Length;

                if (id == "fmt " && !truncated)
                {
                    formatOffset = position + 8;
                    formatLength = size;
                }
                else if (id == "data")
                {
                    dataOffset = position + 8;
                    dataLength = Math.Min(size, wem.Length - dataOffset);
                }

                if (truncated)
                    break;

                position += 8 + size + (size & 1);
            }

            if (formatOffset < 0 || formatLength < 16 || dataOffset < 0)
                throw new InvalidDataException("This sound is missing its format or data chunk.");
        }
    }
}

using System;
using System.IO;
using System.Text;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// Reads an ordinary .wav into per-channel floating point samples.
    ///
    /// Covers what audio tools actually write: integer PCM at 8, 16, 24 or 32 bits, and 32 or 64 bit
    /// floating point, in either a plain or an extensible format chunk. Anything compressed is refused
    /// by name rather than misread.
    /// </summary>
    internal static class WaveFile
    {
        private const ushort FormatPcm = 0x0001;
        private const ushort FormatFloat = 0x0003;
        private const ushort FormatExtensible = 0xFFFE;

        public sealed class Audio
        {
            /// <summary>One array per channel, each the same length, in -1..1.</summary>
            public float[][] Samples;

            public int SampleRate;

            public int Channels
            {
                get { return Samples == null ? 0 : Samples.Length; }
            }

            public int Frames
            {
                get { return Samples == null || Samples.Length == 0 ? 0 : Samples[0].Length; }
            }

            public double Duration
            {
                get { return SampleRate <= 0 ? 0 : Frames / (double)SampleRate; }
            }
        }

        public static Audio Read(string path)
        {
            return Read(File.ReadAllBytes(path));
        }

        public static Audio Read(byte[] file)
        {
            if (file == null || file.Length < 44)
                throw new InvalidDataException("That file is too small to be a WAV.");
            if (Encoding.ASCII.GetString(file, 0, 4) != "RIFF" || Encoding.ASCII.GetString(file, 8, 4) != "WAVE")
                throw new InvalidDataException("That is not a WAV file.");

            int formatAt = -1, formatLength = 0, dataAt = -1, dataLength = 0;
            int position = 12;
            while (position + 8 <= file.Length)
            {
                string tag = Encoding.ASCII.GetString(file, position, 4);
                int size = BitConverter.ToInt32(file, position + 4);
                if (size < 0)
                    break;

                if (tag == "fmt ") { formatAt = position + 8; formatLength = size; }
                else if (tag == "data") { dataAt = position + 8; dataLength = Math.Min(size, file.Length - position - 8); }

                position += 8 + size + (size & 1); //chunks are word aligned
            }

            if (formatAt < 0 || formatLength < 16 || dataAt < 0)
                throw new InvalidDataException("That WAV has no format or audio data.");

            ushort format = BitConverter.ToUInt16(file, formatAt);
            int channels = BitConverter.ToUInt16(file, formatAt + 2);
            int sampleRate = (int)BitConverter.ToUInt32(file, formatAt + 4);
            int bits = BitConverter.ToUInt16(file, formatAt + 14);

            //The extensible form says what it really is in a GUID whose first two bytes are the tag
            if (format == FormatExtensible && formatLength >= 40)
                format = BitConverter.ToUInt16(file, formatAt + 24);

            if (format != FormatPcm && format != FormatFloat)
                throw new NotSupportedException("That WAV is compressed (format 0x" + format.ToString("X4")
                    + "). Save it as uncompressed PCM and try again.");
            if (channels < 1 || channels > 8)
                throw new NotSupportedException("That WAV has " + channels + " channels.");
            if (sampleRate < 8000 || sampleRate > 192000)
                throw new NotSupportedException("That WAV runs at " + sampleRate + " Hz.");

            int bytesPerSample = bits / 8;
            if (bytesPerSample < 1 || bits % 8 != 0)
                throw new NotSupportedException("That WAV stores " + bits + " bits per sample.");

            int frameSize = bytesPerSample * channels;
            int frames = dataLength / frameSize;
            if (frames == 0)
                throw new InvalidDataException("That WAV holds no audio.");

            float[][] samples = new float[channels][];
            for (int channel = 0; channel < channels; channel++)
                samples[channel] = new float[frames];

            for (int frame = 0; frame < frames; frame++)
            {
                int at = dataAt + frame * frameSize;
                for (int channel = 0; channel < channels; channel++)
                    samples[channel][frame] = ReadSample(file, at + channel * bytesPerSample, format, bits);
            }

            return new Audio { Samples = samples, SampleRate = sampleRate };
        }

        private static float ReadSample(byte[] file, int offset, ushort format, int bits)
        {
            if (format == FormatFloat)
            {
                if (bits == 32)
                    return BitConverter.ToSingle(file, offset);
                if (bits == 64)
                    return (float)BitConverter.ToDouble(file, offset);
                throw new NotSupportedException("That WAV stores " + bits + " bit floating point samples.");
            }

            switch (bits)
            {
                case 8:
                    //Eight bit PCM is unsigned, with silence at 128
                    return (file[offset] - 128) / 128f;
                case 16:
                    return BitConverter.ToInt16(file, offset) / 32768f;
                case 24:
                {
                    int value = file[offset] | (file[offset + 1] << 8) | (file[offset + 2] << 16);
                    if ((value & 0x800000) != 0)
                        value |= unchecked((int)0xFF000000); //sign extend
                    return value / 8388608f;
                }
                case 32:
                    return BitConverter.ToInt32(file, offset) / 2147483648f;
                default:
                    throw new NotSupportedException("That WAV stores " + bits + " bits per sample.");
            }
        }
    }
}

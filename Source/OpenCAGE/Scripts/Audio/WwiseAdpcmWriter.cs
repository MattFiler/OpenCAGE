using System;
using System.IO;
using System.Text;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// Encodes audio as a Wwise ADPCM .wem (wave format 0x8311), for importing over the Switch
    /// build's ADPCM sounds.
    ///
    /// The codec matters here: the game picks its decoder from the sound object's plugin id in the
    /// bank, not from the media itself, so audio imported over an ADPCM sound has to BE ADPCM -
    /// handing the ADPCM decoder a Vorbis stream would preview fine in the editor and play garbage
    /// in the game.
    ///
    /// Encoding mirrors <see cref="WwiseAdpcmReader"/>: independent 0x24-byte frames per channel,
    /// each opening with its first two samples stored literally, then 62 four-bit codes chosen
    /// greedily against the same delta table the decoder walks. Every frame tries all thirteen
    /// starting step indexes and keeps the one with the least squared error - frames re-anchor on
    /// literal samples, so an imperfect choice never propagates past the frame it was made in.
    ///
    /// The header mirrors the shipped files: block align of 0x24 per channel, average byte rate of
    /// rate * align / 64 (each block yielding 64 frames of audio), and the six extra format bytes
    /// whose observed pattern is a zero, a channel-layout code, and a zero. A JUNK chunk pads the
    /// data to a sixteen-byte boundary, as the shipped files do.
    /// </summary>
    internal static class WwiseAdpcmWriter
    {
        private const int FrameSize = 0x24;
        private const int SamplesPerFrame = 2 + (FrameSize - 5) * 2;

        public static byte[] Build(float[][] samples, int sampleRate)
        {
            int channels = samples.Length;
            if (channels < 1 || channels > 2)
                throw new NotSupportedException("Wwise ADPCM in this game is mono or stereo only.");

            int frames = samples[0].Length;
            int blocks = Math.Max(1, (frames + SamplesPerFrame - 1) / SamplesPerFrame);

            //The last block is padded by holding the final sample, which the decoder plays as at
            //most a millisecond of flat signal - the same thing a retail file's tail does
            short[][] pcm = new short[channels][];
            for (int channel = 0; channel < channels; channel++)
            {
                pcm[channel] = new short[blocks * SamplesPerFrame];
                float[] source = samples[channel];
                short last = 0;
                for (int i = 0; i < pcm[channel].Length; i++)
                {
                    if (i < source.Length)
                    {
                        float value = source[i];
                        if (value > 1f) value = 1f;
                        else if (value < -1f) value = -1f;
                        last = (short)Math.Round(value * short.MaxValue);
                    }
                    pcm[channel][i] = last;
                }
            }

            byte[] data = new byte[blocks * FrameSize * channels];
            byte[] frame = new byte[FrameSize];
            for (int block = 0; block < blocks; block++)
            {
                for (int channel = 0; channel < channels; channel++)
                {
                    EncodeFrame(pcm[channel], block * SamplesPerFrame, frame);
                    Array.Copy(frame, 0, data, (block * channels + channel) * FrameSize, FrameSize);
                }
            }

            int blockAlign = FrameSize * channels;
            using (MemoryStream stream = new MemoryStream(data.Length + 128))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(0); //patched below
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));

                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(24);
                writer.Write((ushort)WwiseAdpcmReader.FormatAdpcm);
                writer.Write((ushort)channels);
                writer.Write((uint)sampleRate);
                writer.Write((uint)((long)sampleRate * blockAlign / SamplesPerFrame));
                writer.Write((ushort)blockAlign);
                writer.Write((ushort)4); //bits per sample
                writer.Write((ushort)6); //extra format bytes
                writer.Write((ushort)0);
                writer.Write((ushort)(channels == 1 ? 0x4101 : 0x3102));
                writer.Write((ushort)0);

                WriteJunkAndData(writer, stream, data);
                return stream.ToArray();
            }
        }

        /// <summary>
        /// Plain PCM in a .wem (wave format 0xFFFE), for the few bank-embedded sounds stored that way.
        /// </summary>
        public static byte[] BuildPcm(float[][] samples, int sampleRate)
        {
            int channels = samples.Length;
            if (channels < 1 || channels > 2)
                throw new NotSupportedException("PCM sounds in this game are mono or stereo only.");

            int frames = samples[0].Length;
            byte[] data = new byte[frames * channels * 2];
            for (int i = 0; i < frames; i++)
            {
                for (int channel = 0; channel < channels; channel++)
                {
                    float value = samples[channel][i];
                    if (value > 1f) value = 1f;
                    else if (value < -1f) value = -1f;

                    short sample = (short)Math.Round(value * short.MaxValue);
                    int at = (i * channels + channel) * 2;
                    data[at] = (byte)sample;
                    data[at + 1] = (byte)(sample >> 8);
                }
            }

            using (MemoryStream stream = new MemoryStream(data.Length + 128))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(0); //patched below
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));

                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(24);
                writer.Write((ushort)WwisePcmReader.FormatPcm);
                writer.Write((ushort)channels);
                writer.Write((uint)sampleRate);
                writer.Write((uint)(sampleRate * channels * 2));
                writer.Write((ushort)(channels * 2));
                writer.Write((ushort)16);
                writer.Write((ushort)6);
                writer.Write((ushort)0);
                writer.Write((ushort)(channels == 1 ? 0x4101 : 0x3102));
                writer.Write((ushort)0);

                WriteJunkAndData(writer, stream, data);
                return stream.ToArray();
            }
        }

        /// <summary>Pad the data body to a sixteen-byte boundary, write it, and fix the RIFF size.</summary>
        private static void WriteJunkAndData(BinaryWriter writer, MemoryStream stream, byte[] data)
        {
            //A JUNK chunk sized so the data BODY - past both chunk headers - lands on the boundary,
            //which with this fixed header layout is the same four bytes the shipped files carry
            int junk = (16 - (int)(stream.Position + 8 + 8) % 16) % 16;
            writer.Write(Encoding.ASCII.GetBytes("JUNK"));
            writer.Write(junk);
            writer.Write(new byte[junk]);

            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(data.Length);
            writer.Write(data);

            long end = stream.Position;
            stream.Position = 4;
            writer.Write((int)(end - 8));
            stream.Position = end;
        }

        /// <summary>
        /// Encode one channel's frame: 64 samples starting at <paramref name="offset"/>, the first
        /// two stored literally and the rest as greedy nibble choices, best starting index kept.
        /// </summary>
        private static void EncodeFrame(short[] pcm, int offset, byte[] frame)
        {
            int bestIndex = 0;
            long bestError = long.MaxValue;

            for (int start = 0; start <= 12; start++)
            {
                long error = TryEncode(pcm, offset, start, null);
                if (error < bestError)
                {
                    bestError = error;
                    bestIndex = start;
                    if (error == 0)
                        break;
                }
            }

            frame[0] = (byte)pcm[offset];
            frame[1] = (byte)(pcm[offset] >> 8);
            frame[2] = (byte)pcm[offset + 1];
            frame[3] = (byte)(pcm[offset + 1] >> 8);
            frame[4] = (byte)bestIndex;
            Array.Clear(frame, 5, FrameSize - 5);

            TryEncode(pcm, offset, bestIndex, frame);
        }

        /// <summary>
        /// Run the greedy encode from one starting step index, returning the total squared error.
        /// When <paramref name="frame"/> is given the chosen nibbles are packed into it as well,
        /// low nibble first - exactly the order the decoder unpacks.
        /// </summary>
        private static long TryEncode(short[] pcm, int offset, int startIndex, byte[] frame)
        {
            int hist2 = pcm[offset];
            int hist1 = pcm[offset + 1];
            int index = startIndex;
            long error = 0;

            for (int i = 0; i < SamplesPerFrame - 2; i++)
            {
                int target = pcm[offset + 2 + i];
                int predicted = 2 * hist1 - hist2;

                //The delta rows are sorted, so the best nibble could be searched - but sixteen
                //candidates is small enough that trying them all is simpler and just as fast
                int bestNibble = 0;
                int bestSample = 0;
                long bestDistance = long.MaxValue;
                int[] deltas = Delta[index];
                for (int nibble = 0; nibble < 16; nibble++)
                {
                    int sample = deltas[nibble] + predicted;
                    if (sample > short.MaxValue) sample = short.MaxValue;
                    else if (sample < short.MinValue) sample = short.MinValue;

                    long distance = (long)(sample - target) * (sample - target);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestNibble = nibble;
                        bestSample = sample;
                    }
                }

                error += bestDistance;
                if (frame != null)
                    frame[5 + i / 2] |= (byte)((i & 1) == 0 ? bestNibble : bestNibble << 4);

                index = Next[index][bestNibble];
                hist2 = hist1;
                hist1 = bestSample;
            }

            return error;
        }

        //The same table the reader walks - see WwiseAdpcmReader for its provenance
        private static readonly int[][] Delta;
        private static readonly int[][] Next;

        static WwiseAdpcmWriter()
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
                Delta[row] = new int[16];
                Next[row] = new int[16];
                for (int i = 0; i < 16; i++)
                {
                    Delta[row][i] = deltas[i] << shift;
                    Next[row][i] = Math.Min(nexts[i] + shift, 11);
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// Encodes audio to Vorbis with Xiph's own encoder - the same library already used to decode, which
    /// carries the encoder in the same DLL.
    ///
    /// The encoder is steered rather than just run. Wwise streams do not carry their own codebooks, so
    /// a stream can only be written if every book it uses is in the table the runtime has, and the
    /// decoder figures in the header can only be filled in for a setup the game already uses. Both are
    /// decided by the quality setting, and both can be checked before a single sample is encoded - so
    /// the quality is chosen by asking the encoder what it would do and keeping the first answer the
    /// game can play.
    /// </summary>
    internal static class VorbisEncoder
    {
        /// <summary>
        /// Qualities to try, in the order they are tried. Higher is better; the search starts at the
        /// requested quality and works up, because a stream the game cannot play is no use at any size.
        /// </summary>
        private static readonly float[] Ladder = { 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1.0f };

        public sealed class Result
        {
            public WwiseVorbisWriter.Stream Stream;

            /// <summary>The quality actually used, which may be above the one asked for.</summary>
            public float Quality;

            /// <summary>Set when the quality had to be raised to land on a setup the game knows.</summary>
            public string Note;
        }

        /// <summary>
        /// Encode interleaved audio. <paramref name="samples"/> holds one array per channel, each the
        /// same length, with values in -1..1.
        /// </summary>
        public static Result Encode(float[][] samples, int sampleRate, float quality)
        {
            if (samples == null || samples.Length == 0 || samples[0] == null || samples[0].Length == 0)
                throw new InvalidDataException("There are no samples to encode.");
            if (sampleRate < 8000 || sampleRate > 192000)
                throw new NotSupportedException("A sample rate of " + sampleRate + " Hz is outside what Vorbis carries.");

            VorbisNative.EnsureLoaded();

            int channels = samples.Length;
            float chosen;
            string note;
            if (!ChooseQuality(channels, sampleRate, quality, out chosen, out note))
                throw new NotSupportedException(
                    "This audio cannot be encoded into a form the game can play - no quality setting produced "
                    + "a Vorbis setup it knows. " + channels + " channel audio at " + sampleRate + " Hz is the "
                    + "problem; 44100 or 48000 Hz, mono or stereo, always works.");

            Result result = new Result { Quality = chosen, Note = note };
            result.Stream = Run(samples, sampleRate, channels, chosen);
            return result;
        }

        /// <summary>
        /// Find the lowest quality at or above <paramref name="wanted"/> whose setup the game can play.
        /// Nothing is encoded here - the setup is settled by the encoder's own configuration, which is
        /// available as soon as it has been initialised.
        /// </summary>
        private static bool ChooseQuality(int channels, int sampleRate, float wanted, out float chosen, out string note)
        {
            chosen = wanted;
            note = null;

            List<float> order = new List<float>();
            foreach (float step in Ladder)
                if (step >= wanted - 0.0001f)
                    order.Add(step);

            foreach (float step in order)
            {
                byte[] setup;
                try { setup = PackedSetupFor(channels, sampleRate, step); }
                catch { continue; }

                if (WwiseVorbisSetups.Find(setup) == null)
                    continue;

                chosen = step;
                if (step > wanted + 0.0001f)
                    note = "Encoded at quality " + step.ToString("0.0") + " rather than " + wanted.ToString("0.0")
                        + ", which is the nearest setting the game's decoder is set up for.";
                return true;
            }

            return false;
        }

        /// <summary>The setup the encoder would use, in the packed form a .wem carries.</summary>
        private static byte[] PackedSetupFor(int channels, int sampleRate, float quality)
        {
            IntPtr info = Blob(InfoSize);
            IntPtr dsp = Blob(DspSize);
            IntPtr comment = Blob(CommentSize);
            try
            {
                Native.vorbis_info_init(info);
                Check(Native.vorbis_encode_init_vbr(info, channels, sampleRate, quality), "set up the encoder");
                Native.vorbis_comment_init(comment);
                Check(Native.vorbis_analysis_init(dsp, info), "start the encoder");

                OggPacket identification, comments, setup;
                Check(Native.vorbis_analysis_headerout(dsp, comment, out identification, out comments, out setup), "read the encoder's headers");

                byte[] header = Copy(setup);
                bool[] blockflags;
                int modeBits;
                return WwiseVorbisWriter.PackSetupHeader(header, channels, out blockflags, out modeBits);
            }
            finally
            {
                Native.vorbis_dsp_clear(dsp);
                Native.vorbis_comment_clear(comment);
                Native.vorbis_info_clear(info);
                Marshal.FreeHGlobal(info);
                Marshal.FreeHGlobal(dsp);
                Marshal.FreeHGlobal(comment);
            }
        }

        /// <summary>Encode for real, collecting the packets the writer needs.</summary>
        private static WwiseVorbisWriter.Stream Run(float[][] samples, int sampleRate, int channels, float quality)
        {
            const int Chunk = 1024;

            IntPtr info = Blob(InfoSize);
            IntPtr dsp = Blob(DspSize);
            IntPtr block = Blob(BlockSize);
            IntPtr comment = Blob(CommentSize);

            try
            {
                Native.vorbis_info_init(info);
                Check(Native.vorbis_encode_init_vbr(info, channels, sampleRate, quality), "set up the encoder");
                Native.vorbis_comment_init(comment);
                Check(Native.vorbis_analysis_init(dsp, info), "start the encoder");
                Check(Native.vorbis_block_init(dsp, block), "start the encoder");

                OggPacket identification, comments, setup;
                Check(Native.vorbis_analysis_headerout(dsp, comment, out identification, out comments, out setup), "read the encoder's headers");

                int blocksize0Pow, blocksize1Pow;
                ReadBlocksizes(Copy(identification), out blocksize0Pow, out blocksize1Pow);

                WwiseVorbisWriter.Stream stream = new WwiseVorbisWriter.Stream
                {
                    Channels = channels,
                    SampleRate = sampleRate,
                    SampleCount = (uint)samples[0].Length,
                    SetupHeader = Copy(setup),
                    Blocksize0Pow = blocksize0Pow,
                    Blocksize1Pow = blocksize1Pow,
                };

                int written = 0;
                int total = samples[0].Length;
                while (true)
                {
                    int count = Math.Min(Chunk, total - written);

                    //The encoder hands out its own buffers to write into - one per channel - and is then
                    //told how much was filled. A count of zero is how it is told the audio has ended.
                    IntPtr buffers = Native.vorbis_analysis_buffer(dsp, count == 0 ? 1 : count);
                    if (count > 0)
                    {
                        for (int channel = 0; channel < channels; channel++)
                        {
                            IntPtr target = Marshal.ReadIntPtr(buffers, channel * IntPtr.Size);
                            Marshal.Copy(samples[channel], written, target, count);
                        }
                    }

                    Check(Native.vorbis_analysis_wrote(dsp, count), "hand samples to the encoder");
                    Drain(dsp, block, stream);

                    if (count == 0)
                        break;
                    written += count;
                }

                if (stream.Audio.Count == 0)
                    throw new InvalidDataException("The encoder produced no audio.");

                return stream;
            }
            finally
            {
                Native.vorbis_block_clear(block);
                Native.vorbis_dsp_clear(dsp);
                Native.vorbis_comment_clear(comment);
                Native.vorbis_info_clear(info);
                Marshal.FreeHGlobal(info);
                Marshal.FreeHGlobal(dsp);
                Marshal.FreeHGlobal(block);
                Marshal.FreeHGlobal(comment);
            }
        }

        private static void Drain(IntPtr dsp, IntPtr block, WwiseVorbisWriter.Stream stream)
        {
            while (Native.vorbis_analysis_blockout(dsp, block) == 1)
            {
                Check(Native.vorbis_analysis(block, IntPtr.Zero), "encode a block");
                Check(Native.vorbis_bitrate_addblock(block), "encode a block");

                OggPacket packet;
                while (Native.vorbis_bitrate_flushpacket(dsp, out packet) == 1)
                {
                    byte[] data = Copy(packet);
                    stream.Audio.Add(new WwiseVorbisWriter.Packet { Data = data, Length = data.Length });
                }
            }
        }

        /// <summary>The two window sizes, which only the identification header carries.</summary>
        private static void ReadBlocksizes(byte[] identification, out int blocksize0Pow, out int blocksize1Pow)
        {
            VorbisBitReader reader = new VorbisBitReader(identification, 7, identification.Length - 7);
            reader.Read(32); //version
            reader.Read(8);  //channels
            reader.Read(32); //rate
            reader.Read(32); //maximum bitrate
            reader.Read(32); //nominal
            reader.Read(32); //minimum
            blocksize0Pow = (int)reader.Read(4);
            blocksize1Pow = (int)reader.Read(4);
        }

        private static void Check(int result, string what)
        {
            if (result != 0)
                throw new InvalidOperationException("The encoder failed to " + what + " (error " + result + ").");
        }

        private static byte[] Copy(OggPacket packet)
        {
            if (packet.packet == IntPtr.Zero || packet.bytes <= 0)
                throw new InvalidDataException("The encoder produced an empty packet.");

            byte[] data = new byte[packet.bytes];
            Marshal.Copy(packet.packet, data, 0, packet.bytes);
            return data;
        }

        /// <summary>
        /// Zeroed scratch for one of libvorbis' structures. The sizes are generous on purpose - the
        /// library only ever writes its own fields, and the layouts differ between builds, so there is
        /// nothing to gain by matching them exactly and a lot to lose by getting one wrong.
        /// </summary>
        private static IntPtr Blob(int size)
        {
            IntPtr memory = Marshal.AllocHGlobal(size);
            for (int i = 0; i < size; i++)
                Marshal.WriteByte(memory, i, 0);
            return memory;
        }

        private const int InfoSize = 512;
        private const int CommentSize = 512;
        private const int DspSize = 16384;
        private const int BlockSize = 8192;

        [StructLayout(LayoutKind.Sequential)]
        internal struct OggPacket
        {
            public IntPtr packet;
            public int bytes;
            public int b_o_s;
            public int e_o_s;
            public long granulepos;
            public long packetno;
        }

        private static class Native
        {
            private const string Library = "vorbis.dll";

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern void vorbis_info_init(IntPtr vi);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern void vorbis_info_clear(IntPtr vi);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern int vorbis_encode_init_vbr(IntPtr vi, int channels, int rate, float quality);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern void vorbis_comment_init(IntPtr vc);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern void vorbis_comment_clear(IntPtr vc);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern int vorbis_analysis_init(IntPtr v, IntPtr vi);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern void vorbis_dsp_clear(IntPtr v);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern int vorbis_block_init(IntPtr v, IntPtr vb);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern int vorbis_block_clear(IntPtr vb);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern int vorbis_analysis_headerout(IntPtr v, IntPtr vc, out OggPacket op, out OggPacket comm, out OggPacket code);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr vorbis_analysis_buffer(IntPtr v, int vals);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern int vorbis_analysis_wrote(IntPtr v, int vals);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern int vorbis_analysis_blockout(IntPtr v, IntPtr vb);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern int vorbis_analysis(IntPtr vb, IntPtr op);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern int vorbis_bitrate_addblock(IntPtr vb);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            public static extern int vorbis_bitrate_flushpacket(IntPtr v, out OggPacket op);
        }
    }
}

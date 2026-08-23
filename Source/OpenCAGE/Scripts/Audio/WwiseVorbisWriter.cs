using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// Turns ordinary Vorbis packets into a Wwise .wem the game can play - the exact inverse of
    /// <see cref="WwiseVorbisConverter"/>.
    ///
    /// Everything the runtime can supply from elsewhere is taken back out: the Ogg container, the
    /// identification and comment headers, the codebooks (replaced by ten bit references into the
    /// shared table), and each audio packet's type and window flags. Nothing is re-encoded, so a stream
    /// that came out of the game and goes back through here is byte for byte what it started as.
    ///
    /// The one thing that cannot be derived from the Vorbis stream is the pair of decoder allocation
    /// sizes the runtime reads out of the header, plus the hash identifying the setup. Those depend on
    /// the decoder rather than the audio, so they are looked up from the game's own files - see
    /// <see cref="WwiseVorbisSetups"/>.
    /// </summary>
    internal static class WwiseVorbisWriter
    {
        /// <summary>One Vorbis audio packet, as the encoder produced it.</summary>
        public sealed class Packet
        {
            public byte[] Data;
            public int Length;
        }

        public sealed class Stream
        {
            public int Channels;
            public int SampleRate;

            /// <summary>PCM frames the sound is worth - the decoder stops here and drops the overhang.</summary>
            public uint SampleCount;

            /// <summary>The setup header the encoder produced, magic and all.</summary>
            public byte[] SetupHeader;

            /// <summary>The two window sizes, as powers of two - from the identification header.</summary>
            public int Blocksize0Pow;
            public int Blocksize1Pow;

            public List<Packet> Audio = new List<Packet>();
        }

        /// <summary>
        /// Build a complete .wem. Throws if the stream uses anything the format cannot carry, rather
        /// than writing a file the game would choke on.
        /// </summary>
        public static byte[] Build(Stream stream)
        {
            if (stream == null || stream.SetupHeader == null || stream.Audio.Count == 0)
                throw new InvalidDataException("There is no audio to write.");
            if (stream.Channels < 1 || stream.Channels > 8)
                throw new NotSupportedException("Wwise cannot carry " + stream.Channels + " channels.");

            int blocksize0Pow = stream.Blocksize0Pow, blocksize1Pow = stream.Blocksize1Pow;
            if (blocksize0Pow < 6 || blocksize0Pow > 13 || blocksize1Pow < blocksize0Pow || blocksize1Pow > 13)
                throw new InvalidDataException("The stream declares an impossible block size.");

            bool[] modeBlockflag;
            int modeBits;
            byte[] setupPacket = PackSetupHeader(stream.SetupHeader, stream.Channels, out modeBlockflag, out modeBits);

            //Each packet needs to know whether its neighbours are long, exactly as rebuilding does, so
            //the modes come out in a pass of their own first
            int[] modes = new int[stream.Audio.Count];
            for (int i = 0; i < stream.Audio.Count; i++)
            {
                Packet packet = stream.Audio[i];
                if (packet.Length == 0)
                    throw new InvalidDataException("The encoder produced an empty audio packet.");

                VorbisBitReader reader = new VorbisBitReader(packet.Data, 0, packet.Length);
                if (reader.Read(1) != 0)
                    throw new InvalidDataException("A Vorbis header packet turned up among the audio.");

                modes[i] = (int)reader.Read(modeBits);
                if (modes[i] >= modeBlockflag.Length)
                    throw new InvalidDataException("An audio packet references a mode that does not exist.");
            }

            //Written with the game's own bytes for this setup rather than ours. The two are equivalent -
            //they differ only where the codebook table repeats a book and we picked a different index for
            //it - but writing the bytes the game already ships means anything the runtime checks against
            //them, the setup hash included, still holds.
            WwiseVorbisSetups.Decoder decoder = WwiseVorbisSetups.For(setupPacket, stream.Channels, blocksize0Pow, blocksize1Pow);
            setupPacket = decoder.Setup;

            //--- the data chunk: setup packet, then the audio, each behind its own length ---
            MemoryStream data = new MemoryStream(EstimateSize(stream));
            WritePacket(data, setupPacket);

            int firstAudioOffset = (int)data.Position;
            int largestPacket = 0;
            long granule = 0;
            int previousBlocksize = 0;

            for (int i = 0; i < stream.Audio.Count; i++)
            {
                byte[] stripped = StripPacket(stream.Audio[i], modes[i], modeBlockflag, modeBits);
                if (stripped.Length > ushort.MaxValue)
                    throw new NotSupportedException("A packet is " + stripped.Length + " bytes, which is more than a .wem can hold.");

                if (stripped.Length > largestPacket)
                    largestPacket = stripped.Length;
                WritePacket(data, stripped);

                int blocksize = 1 << (modeBlockflag[modes[i]] ? blocksize1Pow : blocksize0Pow);
                if (i > 0)
                    granule += (previousBlocksize + blocksize) / 4;
                previousBlocksize = blocksize;
            }

            byte[] dataChunk = data.ToArray();

            //The decoder runs past the end of the sound and the header says by how much, so that the
            //overhang can be dropped rather than played
            long overhang = granule - stream.SampleCount;
            if (overhang < 0 || overhang > ushort.MaxValue)
                throw new InvalidDataException("The sound's length and its packets disagree by " + overhang + " samples.");

            return Wrap(stream, dataChunk, firstAudioOffset, largestPacket, (ushort)overhang,
                blocksize0Pow, blocksize1Pow, decoder);
        }

        private static int EstimateSize(Stream stream)
        {
            int size = 4096;
            foreach (Packet packet in stream.Audio)
                size += packet.Length + 2;
            return size;
        }

        private static void WritePacket(MemoryStream output, byte[] packet)
        {
            output.WriteByte((byte)(packet.Length & 0xFF));
            output.WriteByte((byte)(packet.Length >> 8));
            output.Write(packet, 0, packet.Length);
        }

        #region RIFF

        /// <summary>
        /// Wrap the packets in the RIFF the game expects. The format chunk carries Wwise's own extension
        /// inline rather than in a chunk of its own, which is why it is 66 bytes rather than 18.
        /// </summary>
        private static byte[] Wrap(Stream stream, byte[] dataChunk, int firstAudioOffset, int largestPacket,
            ushort overhang, int blocksize0Pow, int blocksize1Pow, WwiseVorbisSetups.Decoder decoder)
        {
            const int FormatLength = 66;
            byte[] format = new byte[FormatLength];

            //Measured against every sound in the game: the rate is the Vorbis stream's own size over its
            //playing time, truncated rather than rounded
            double seconds = stream.SampleCount / (double)stream.SampleRate;
            uint averageBytesPerSecond = seconds > 0 ? (uint)(dataChunk.Length / seconds) : 0;

            Put16(format, 0x00, WwiseVorbisConverter.FormatVorbis);
            Put16(format, 0x02, (ushort)stream.Channels);
            Put32(format, 0x04, (uint)stream.SampleRate);
            Put32(format, 0x08, averageBytesPerSecond);
            Put16(format, 0x0C, 0); //block align - not meaningful for a variable rate codec
            Put16(format, 0x0E, 0); //bits per sample - likewise
            Put16(format, 0x10, FormatLength - 18); //the extension's own length
            Put16(format, 0x12, 0);
            Put32(format, 0x14, ChannelMask(stream.Channels));

            int vorb = 0x18;
            Put32(format, vorb + 0x00, stream.SampleCount);
            //The loop covers the whole sound, and both offsets are counted from the setup packet
            Put32(format, vorb + 0x04, (uint)firstAudioOffset);
            Put32(format, vorb + 0x08, (uint)dataChunk.Length);
            Put16(format, vorb + 0x0C, 0);        //samples to drop at the loop point
            Put16(format, vorb + 0x0E, overhang); //...and at its end, which for us is the end of the sound
            Put32(format, vorb + 0x10, 0);        //setup packet offset: we write no seek table
            Put32(format, vorb + 0x14, (uint)firstAudioOffset);
            Put16(format, vorb + 0x18, (ushort)largestPacket);
            Put16(format, vorb + 0x1A, overhang);
            Put32(format, vorb + 0x1C, decoder.AllocSize32);
            Put32(format, vorb + 0x20, decoder.AllocSize64);
            Put32(format, vorb + 0x24, decoder.SetupHash);
            format[vorb + 0x28] = (byte)blocksize0Pow;
            format[vorb + 0x29] = (byte)blocksize1Pow;

            using (MemoryStream output = new MemoryStream(dataChunk.Length + 128))
            using (BinaryWriter writer = new BinaryWriter(output, Encoding.ASCII))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(4 + (8 + FormatLength) + (8 + dataChunk.Length));
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));

                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(FormatLength);
                writer.Write(format);

                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataChunk.Length);
                writer.Write(dataChunk);

                return output.ToArray();
            }
        }

        /// <summary>The speaker layout, in the same bits WAVEFORMATEXTENSIBLE uses.</summary>
        private static uint ChannelMask(int channels)
        {
            switch (channels)
            {
                case 1: return 0x4;  //front centre
                case 2: return 0x3;  //front left and right
                case 4: return 0x33; //quad
                case 6: return 0x3F; //5.1
                case 8: return 0x63F;//7.1
                default: return 0;
            }
        }

        private static void Put16(byte[] target, int offset, ushort value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
        }

        private static void Put32(byte[] target, int offset, uint value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
            target[offset + 2] = (byte)(value >> 16);
            target[offset + 3] = (byte)(value >> 24);
        }

        #endregion

        #region SETUP

        /// <summary>
        /// Narrow the setup header back down to the form Wwise stores: codebooks become references into
        /// the shared table, the fields Vorbis pads out to sixteen bits shrink to the few they need, and
        /// everything the runtime already knows - the time domain transforms, the window and transform
        /// types, the framing bit - is dropped.
        /// </summary>
        public static byte[] PackSetupHeader(byte[] header, int channels, out bool[] modeBlockflag, out int modeBits)
        {
            if (header == null || header.Length < 8 || header[0] != 0x05 || Encoding.ASCII.GetString(header, 1, 6) != "vorbis")
                throw new InvalidDataException("That is not a Vorbis setup header.");

            VorbisBitReader input = new VorbisBitReader(header, 7, header.Length - 7);
            VorbisBitWriter output = new VorbisBitWriter(header.Length);

            //--- codebooks ---
            uint codebookCount = input.Read(8) + 1;
            output.Write(codebookCount - 1, 8);
            for (uint i = 0; i < codebookCount; i++)
                output.Write((uint)WwiseCodebooks.Instance.IndexOf(input), 10);

            //--- time domain transforms: one placeholder, which Wwise leaves out entirely ---
            if (input.Read(6) != 0 || input.Read(16) != 0)
                throw new InvalidDataException("The stream declares a time domain transform, which Wwise cannot carry.");

            uint floorCount = ReadWrite(input, output, 6) + 1;
            for (uint i = 0; i < floorCount; i++)
                PackFloor(input, output);

            uint residueCount = ReadWrite(input, output, 6) + 1;
            for (uint i = 0; i < residueCount; i++)
                PackResidue(input, output);

            uint mappingCount = ReadWrite(input, output, 6) + 1;
            for (uint i = 0; i < mappingCount; i++)
                PackMapping(input, output, channels);

            //--- modes ---
            uint modeCount = ReadWrite(input, output, 6) + 1;
            modeBlockflag = new bool[modeCount];
            modeBits = WwiseCodebooks.ILog(modeCount - 1);

            for (uint i = 0; i < modeCount; i++)
            {
                uint blockflag = input.Read(1);
                modeBlockflag[i] = blockflag != 0;
                output.Write(blockflag, 1);

                if (input.Read(16) != 0 || input.Read(16) != 0)
                    throw new InvalidDataException("The stream uses a window or transform type Wwise cannot carry.");

                ReadWrite(input, output, 8); //mapping
            }

            return output.ToArray();
        }

        private static uint ReadWrite(VorbisBitReader input, VorbisBitWriter output, int bits)
        {
            uint value = input.Read(bits);
            output.Write(value, bits);
            return value;
        }

        private static void PackFloor(VorbisBitReader input, VorbisBitWriter output)
        {
            if (input.Read(16) != 1)
                throw new InvalidDataException("Wwise only carries floor type 1.");

            uint partitions = ReadWrite(input, output, 5);
            uint[] partitionClasses = new uint[partitions];
            uint maximumClass = 0;

            for (uint i = 0; i < partitions; i++)
            {
                partitionClasses[i] = ReadWrite(input, output, 4);
                if (partitionClasses[i] > maximumClass)
                    maximumClass = partitionClasses[i];
            }

            uint[] classDimensions = new uint[maximumClass + 1];
            for (uint i = 0; partitions > 0 && i <= maximumClass; i++)
            {
                classDimensions[i] = ReadWrite(input, output, 3) + 1;
                uint subclasses = ReadWrite(input, output, 2);

                if (subclasses != 0)
                    ReadWrite(input, output, 8); //master book

                for (uint j = 0; j < (1u << (int)subclasses); j++)
                    ReadWrite(input, output, 8);
            }

            ReadWrite(input, output, 2); //multiplier
            int rangeBits = (int)ReadWrite(input, output, 4);

            for (uint i = 0; i < partitions; i++)
                for (uint j = 0; j < classDimensions[partitionClasses[i]]; j++)
                    ReadWrite(input, output, rangeBits);
        }

        private static void PackResidue(VorbisBitReader input, VorbisBitWriter output)
        {
            uint type = input.Read(16);
            if (type > 2)
                throw new InvalidDataException("Wwise only carries residue types 0 to 2.");
            output.Write(type, 2);

            ReadWrite(input, output, 24); //begin
            ReadWrite(input, output, 24); //end
            ReadWrite(input, output, 24); //partition size
            uint classifications = ReadWrite(input, output, 6) + 1;
            ReadWrite(input, output, 8); //class book

            uint[] cascade = new uint[classifications];
            for (uint i = 0; i < classifications; i++)
            {
                uint low = ReadWrite(input, output, 3);
                uint high = 0;
                if (ReadWrite(input, output, 1) != 0)
                    high = ReadWrite(input, output, 5);

                cascade[i] = high * 8 + low;
            }

            for (uint i = 0; i < classifications; i++)
                for (int j = 0; j < 8; j++)
                    if ((cascade[i] & (1u << j)) != 0)
                        ReadWrite(input, output, 8);
        }

        private static void PackMapping(VorbisBitReader input, VorbisBitWriter output, int channels)
        {
            if (input.Read(16) != 0)
                throw new InvalidDataException("Wwise only carries mapping type 0.");

            uint submaps = 1;
            if (ReadWrite(input, output, 1) != 0)
                submaps = ReadWrite(input, output, 4) + 1;

            if (ReadWrite(input, output, 1) != 0)
            {
                uint couplingSteps = ReadWrite(input, output, 8) + 1;
                int bits = WwiseCodebooks.ILog((uint)(channels - 1));

                for (uint i = 0; i < couplingSteps; i++)
                {
                    ReadWrite(input, output, bits); //magnitude
                    ReadWrite(input, output, bits); //angle
                }
            }

            if (ReadWrite(input, output, 2) != 0)
                throw new InvalidDataException("The stream's mapping uses a reserved field.");

            if (submaps > 1)
                for (int i = 0; i < channels; i++)
                    ReadWrite(input, output, 4);

            for (uint i = 0; i < submaps; i++)
            {
                ReadWrite(input, output, 8); //unused in this version of the format
                ReadWrite(input, output, 8); //floor
                ReadWrite(input, output, 8); //residue
            }
        }

        #endregion

        #region AUDIO

        /// <summary>
        /// Take the bits back off an audio packet: the leading type bit, which is always zero, and for a
        /// long window the two flags saying what the neighbouring windows look like. The runtime works
        /// all three out for itself from the modes either side.
        /// </summary>
        private static byte[] StripPacket(Packet packet, int mode, bool[] modeBlockflag, int modeBits)
        {
            VorbisBitReader input = new VorbisBitReader(packet.Data, 0, packet.Length);
            VorbisBitWriter output = new VorbisBitWriter(packet.Length);

            input.Read(1); //packet type, already checked
            uint modeNumber = input.Read(modeBits);
            output.Write(modeNumber, modeBits);

            if (modeBlockflag[mode])
            {
                input.Read(1); //previous window
                input.Read(1); //next window
            }

            long remaining = (long)packet.Length * 8 - input.BitsRead;
            while (remaining >= 8)
            {
                output.Write(input.Read(8), 8);
                remaining -= 8;
            }
            if (remaining > 0)
                output.Write(input.Read((int)remaining), (int)remaining);

            /* Taking bits off the front pushes the tail into one more byte than the packet needs, and
             * whatever padding the packet already carried ends up in it. When that byte is empty it is
             * dropped - a Vorbis decoder reads zeros past the end of a packet, which is the whole reason
             * Wwise can strip these bits at all, so nothing is lost. Only ever one byte: a packet whose
             * own last byte is zero has to keep it, or a stream taken out of a .wem would not go back in
             * the same size it came out. */
            byte[] result = output.ToArray();
            if (result.Length > 1 && result[result.Length - 1] == 0)
                Array.Resize(ref result, result.Length - 1);

            return result;
        }

        #endregion
    }
}

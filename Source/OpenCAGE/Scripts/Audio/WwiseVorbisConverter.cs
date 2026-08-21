using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// Turns a Wwise Vorbis .wem into a playable Ogg Vorbis stream.
    ///
    /// Wwise doesn't invent a codec - the audio inside a .wem is ordinary Vorbis - but it strips
    /// everything the runtime can supply from elsewhere. There is no Ogg container, no identification
    /// or comment header, the setup header references a shared codebook table instead of carrying its
    /// own books, and each audio packet has its type and window flags removed because the decoder can
    /// work them out. Rebuilding is therefore lossless: no audio is re-encoded, only the scaffolding
    /// around it is put back.
    ///
    /// This is the same transformation ww2ogg performs, written from the Vorbis specification against
    /// the layout measured from the game's own files.
    /// </summary>
    internal static class WwiseVorbisConverter
    {
        /// <summary>The only wave format tag the game uses - everything is Vorbis.</summary>
        public const ushort FormatVorbis = 0xFFFF;

        private sealed class Wem
        {
            public int FormatOffset;
            public int FormatLength;
            public int DataOffset;
            public int DataLength;

            public ushort FormatTag;
            public ushort Channels;
            public uint SampleRate;
            public uint AverageBytesPerSecond;

            public uint SampleCount;
            public uint SetupPacketOffset;
            public uint FirstAudioPacketOffset;
            public int Blocksize0Pow;
            public int Blocksize1Pow;
        }

        public static bool IsVorbis(byte[] wem)
        {
            try
            {
                Wem parsed = Parse(wem);
                return parsed.FormatTag == FormatVorbis;
            }
            catch
            {
                return false;
            }
        }

        public static byte[] ToOgg(byte[] wem)
        {
            Wem file = Parse(wem);
            if (file.FormatTag != FormatVorbis)
                throw new NotSupportedException("This sound is stored as format 0x" + file.FormatTag.ToString("X4") + ", which the preview cannot decode.");

            //The setup packet has to be rebuilt before the audio, because it defines the modes and
            //their window sizes, and every audio packet needs those to be reassembled
            bool[] modeBlockflag;
            int modeBits;
            byte[] setupHeader = BuildSetupHeader(wem, file, out modeBlockflag, out modeBits);

            //The Ogg comes out a little larger than the packets that went in - a page header per packet,
            //plus the headers on the front - so start the buffer there rather than growing into it
            using (MemoryStream output = new MemoryStream(file.DataLength + file.DataLength / 8 + 8192))
            {
                OggWriter ogg = new OggWriter(output, 1);
                ogg.WritePacket(BuildIdentificationHeader(file), 0, true, false);
                ogg.WritePacket(BuildCommentHeader(), 0, false, false);
                ogg.WritePacket(setupHeader, 0, false, false);

                WriteAudio(wem, file, modeBlockflag, modeBits, ogg);
                return output.ToArray();
            }
        }

        #region PARSING

        private static Wem Parse(byte[] wem)
        {
            if (wem == null || wem.Length < 12)
                throw new InvalidDataException("Sound data is empty.");

            if (Encoding.ASCII.GetString(wem, 0, 4) != "RIFF")
                throw new InvalidDataException("Sound data is not a RIFF file.");

            Wem file = new Wem { FormatOffset = -1, DataOffset = -1 };

            int position = 12;
            while (position + 8 <= wem.Length)
            {
                string tag = Encoding.ASCII.GetString(wem, position, 4);
                int size = BitConverter.ToInt32(wem, position + 4);
                if (size < 0 || position + 8 + size > wem.Length)
                    break;

                if (tag == "fmt ")
                {
                    file.FormatOffset = position + 8;
                    file.FormatLength = size;
                }
                else if (tag == "data")
                {
                    file.DataOffset = position + 8;
                    file.DataLength = size;
                }

                position += 8 + size + (size & 1); //chunks are word aligned
            }

            if (file.FormatOffset < 0 || file.DataOffset < 0)
                throw new InvalidDataException("Sound data has no format or data chunk.");

            int fmt = file.FormatOffset;
            file.FormatTag = BitConverter.ToUInt16(wem, fmt);
            file.Channels = BitConverter.ToUInt16(wem, fmt + 2);
            file.SampleRate = BitConverter.ToUInt32(wem, fmt + 4);
            file.AverageBytesPerSecond = BitConverter.ToUInt32(wem, fmt + 8);

            if (file.FormatTag != FormatVorbis)
                return file;

            //Every Vorbis .wem in the game uses a 66 byte format chunk with the Wwise extension packed
            //into its tail rather than in a chunk of its own
            if (file.FormatLength < 0x42)
                throw new NotSupportedException("Unsupported Wwise Vorbis layout (format chunk is " + file.FormatLength + " bytes).");

            int vorb = fmt + 0x18;
            file.SampleCount = BitConverter.ToUInt32(wem, vorb + 0x00);
            file.SetupPacketOffset = BitConverter.ToUInt32(wem, vorb + 0x10);
            file.FirstAudioPacketOffset = BitConverter.ToUInt32(wem, vorb + 0x14);
            file.Blocksize0Pow = wem[vorb + 0x28];
            file.Blocksize1Pow = wem[vorb + 0x29];

            if (file.Blocksize0Pow < 6 || file.Blocksize0Pow > 13 || file.Blocksize1Pow < 6 || file.Blocksize1Pow > 13)
                throw new InvalidDataException("Sound declares an impossible block size.");

            return file;
        }

        #endregion

        #region HEADERS

        private static void WriteHeaderMagic(VorbisBitWriter writer, byte type)
        {
            writer.Write(type, 8);
            foreach (char c in "vorbis")
                writer.Write(c, 8);
        }

        private static byte[] BuildIdentificationHeader(Wem file)
        {
            VorbisBitWriter writer = new VorbisBitWriter();
            WriteHeaderMagic(writer, 1);

            writer.Write(0, 32); //vorbis version
            writer.Write(file.Channels, 8);
            writer.Write(file.SampleRate, 32);
            writer.Write(0, 32); //maximum bitrate
            writer.Write(file.AverageBytesPerSecond * 8, 32); //nominal bitrate
            writer.Write(0, 32); //minimum bitrate
            writer.Write((uint)file.Blocksize0Pow, 4);
            writer.Write((uint)file.Blocksize1Pow, 4);
            writer.Write(1, 1); //framing

            return writer.ToArray();
        }

        private static byte[] BuildCommentHeader()
        {
            VorbisBitWriter writer = new VorbisBitWriter();
            WriteHeaderMagic(writer, 3);

            byte[] vendor = Encoding.ASCII.GetBytes("OpenCAGE");
            writer.Write((uint)vendor.Length, 32);
            foreach (byte b in vendor)
                writer.Write(b, 8);

            writer.Write(0, 32); //no user comments
            writer.Write(1, 1); //framing

            return writer.ToArray();
        }

        /// <summary>
        /// Rebuild the setup header, widening every field Wwise narrowed and expanding the codebook
        /// references back into whole codebooks.
        /// </summary>
        private static byte[] BuildSetupHeader(byte[] wem, Wem file, out bool[] modeBlockflag, out int modeBits)
        {
            int packetOffset = file.DataOffset + (int)file.SetupPacketOffset;
            if (packetOffset + 2 > wem.Length)
                throw new InvalidDataException("Sound has no setup packet.");

            int packetSize = BitConverter.ToUInt16(wem, packetOffset);
            if (packetOffset + 2 + packetSize > wem.Length)
                throw new InvalidDataException("Sound's setup packet runs past the end of the file.");

            VorbisBitReader input = new VorbisBitReader(wem, packetOffset + 2, packetSize);
            VorbisBitWriter output = new VorbisBitWriter();
            WriteHeaderMagic(output, 5);

            WwiseCodebooks codebooks = WwiseCodebooks.Instance;

            //--- codebooks: ten bit references into the shared table ---
            uint codebookCount = input.Read(8) + 1;
            output.Write(codebookCount - 1, 8);
            for (uint i = 0; i < codebookCount; i++)
                codebooks.Rebuild((int)input.Read(10), output);

            //--- time domain transforms: Wwise drops them, Vorbis requires a single placeholder ---
            output.Write(0, 6);
            output.Write(0, 16);

            uint floorCount = ReadWrite(input, output, 6) + 1;
            for (uint i = 0; i < floorCount; i++)
                CopyFloor(input, output, codebookCount);

            uint residueCount = ReadWrite(input, output, 6) + 1;
            for (uint i = 0; i < residueCount; i++)
                CopyResidue(input, output, codebookCount);

            uint mappingCount = ReadWrite(input, output, 6) + 1;
            for (uint i = 0; i < mappingCount; i++)
                CopyMapping(input, output, file.Channels, floorCount, residueCount);

            //--- modes: also the thing every audio packet needs in order to be rebuilt ---
            uint modeCount = ReadWrite(input, output, 6) + 1;
            modeBlockflag = new bool[modeCount];
            modeBits = WwiseCodebooks.ILog(modeCount - 1);

            for (uint i = 0; i < modeCount; i++)
            {
                uint blockflag = input.Read(1);
                modeBlockflag[i] = blockflag != 0;
                output.Write(blockflag, 1);

                output.Write(0, 16); //window type
                output.Write(0, 16); //transform type

                uint mapping = ReadWrite(input, output, 8);
                if (mapping >= mappingCount)
                    throw new InvalidDataException("Sound's mode references a mapping that does not exist.");
            }

            output.Write(1, 1); //framing
            return output.ToArray();
        }

        private static uint ReadWrite(VorbisBitReader input, VorbisBitWriter output, int bits)
        {
            uint value = input.Read(bits);
            output.Write(value, bits);
            return value;
        }

        private static void CopyFloor(VorbisBitReader input, VorbisBitWriter output, uint codebookCount)
        {
            //Wwise leaves the type out because only floor 1 is ever used
            output.Write(1, 16);

            uint partitions = ReadWrite(input, output, 5);
            uint[] partitionClasses = new uint[partitions];
            uint maximumClass = 0;
            bool any = false;

            for (uint i = 0; i < partitions; i++)
            {
                partitionClasses[i] = ReadWrite(input, output, 4);
                if (!any || partitionClasses[i] > maximumClass)
                {
                    maximumClass = partitionClasses[i];
                    any = true;
                }
            }

            uint[] classDimensions = new uint[maximumClass + 1];
            for (uint i = 0; any && i <= maximumClass; i++)
            {
                classDimensions[i] = ReadWrite(input, output, 3) + 1;
                uint subclasses = ReadWrite(input, output, 2);

                if (subclasses != 0)
                {
                    uint masterbook = ReadWrite(input, output, 8);
                    if (masterbook >= codebookCount)
                        throw new InvalidDataException("Sound's floor references a codebook that does not exist.");
                }

                for (uint j = 0; j < (1u << (int)subclasses); j++)
                {
                    uint book = ReadWrite(input, output, 8);
                    if (book > codebookCount)
                        throw new InvalidDataException("Sound's floor references a codebook that does not exist.");
                }
            }

            ReadWrite(input, output, 2); //multiplier
            int rangeBits = (int)ReadWrite(input, output, 4);

            for (uint i = 0; i < partitions; i++)
            {
                uint dimensions = classDimensions[partitionClasses[i]];
                for (uint j = 0; j < dimensions; j++)
                    ReadWrite(input, output, rangeBits);
            }
        }

        private static void CopyResidue(VorbisBitReader input, VorbisBitWriter output, uint codebookCount)
        {
            //Two bits are enough for the three residue types, but Vorbis spends sixteen on it
            uint type = input.Read(2);
            output.Write(type, 16);
            if (type > 2)
                throw new InvalidDataException("Sound uses an unknown residue type.");

            ReadWrite(input, output, 24); //begin
            ReadWrite(input, output, 24); //end
            ReadWrite(input, output, 24); //partition size
            uint classifications = ReadWrite(input, output, 6) + 1;

            uint classbook = ReadWrite(input, output, 8);
            if (classbook >= codebookCount)
                throw new InvalidDataException("Sound's residue references a codebook that does not exist.");

            uint[] cascade = new uint[classifications];
            for (uint i = 0; i < classifications; i++)
            {
                uint high = 0;
                uint low = ReadWrite(input, output, 3);
                uint flag = ReadWrite(input, output, 1);
                if (flag != 0)
                    high = ReadWrite(input, output, 5);

                cascade[i] = high * 8 + low;
            }

            for (uint i = 0; i < classifications; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    if ((cascade[i] & (1u << j)) == 0)
                        continue;

                    uint book = ReadWrite(input, output, 8);
                    if (book >= codebookCount)
                        throw new InvalidDataException("Sound's residue references a codebook that does not exist.");
                }
            }
        }

        private static void CopyMapping(VorbisBitReader input, VorbisBitWriter output, int channels, uint floorCount, uint residueCount)
        {
            output.Write(0, 16); //mapping type

            uint submaps = 1;
            if (ReadWrite(input, output, 1) != 0)
                submaps = ReadWrite(input, output, 4) + 1;

            if (ReadWrite(input, output, 1) != 0)
            {
                uint couplingSteps = ReadWrite(input, output, 8) + 1;
                int bits = WwiseCodebooks.ILog((uint)(channels - 1));

                for (uint i = 0; i < couplingSteps; i++)
                {
                    uint magnitude = ReadWrite(input, output, bits);
                    uint angle = ReadWrite(input, output, bits);
                    if (magnitude == angle || magnitude >= channels || angle >= channels)
                        throw new InvalidDataException("Sound's channel coupling is invalid.");
                }
            }

            if (ReadWrite(input, output, 2) != 0)
                throw new InvalidDataException("Sound's mapping uses a reserved field.");

            if (submaps > 1)
            {
                for (int i = 0; i < channels; i++)
                {
                    if (ReadWrite(input, output, 4) >= submaps)
                        throw new InvalidDataException("Sound maps a channel to a submap that does not exist.");
                }
            }

            for (uint i = 0; i < submaps; i++)
            {
                ReadWrite(input, output, 8); //unused in this version of the format

                if (ReadWrite(input, output, 8) >= floorCount)
                    throw new InvalidDataException("Sound's mapping references a floor that does not exist.");

                if (ReadWrite(input, output, 8) >= residueCount)
                    throw new InvalidDataException("Sound's mapping references a residue that does not exist.");
            }
        }

        #endregion

        #region AUDIO

        /// <summary>
        /// Rebuild and page the audio packets.
        ///
        /// Wwise removes the leading packet type bit - it is always zero for audio - and, for long
        /// windows, the two flags saying whether the neighbouring packets are long or short. The window
        /// flags are what force a look at the following packet's mode before the current one can be
        /// written, so the modes are read in a pass of their own first.
        /// </summary>
        private static void WriteAudio(byte[] wem, Wem file, bool[] modeBlockflag, int modeBits, OggWriter ogg)
        {
            List<int> offsets = new List<int>();
            List<int> sizes = new List<int>();

            int position = file.DataOffset + (int)file.FirstAudioPacketOffset;
            int end = file.DataOffset + file.DataLength;
            while (position + 2 <= end)
            {
                int size = BitConverter.ToUInt16(wem, position);
                if (position + 2 + size > end)
                    break;

                offsets.Add(position + 2);
                sizes.Add(size);
                position += 2 + size;
            }

            if (offsets.Count == 0)
                throw new InvalidDataException("Sound contains no audio packets.");

            //Modes first, so each packet knows what the next one's window looks like
            int[] modes = new int[offsets.Count];
            for (int i = 0; i < offsets.Count; i++)
            {
                if (sizes[i] == 0)
                {
                    modes[i] = 0;
                    continue;
                }

                modes[i] = (int)new VorbisBitReader(wem, offsets[i], sizes[i]).Read(modeBits);
                if (modes[i] >= modeBlockflag.Length)
                    throw new InvalidDataException("Sound's audio references a mode that does not exist.");
            }

            int blocksize0 = 1 << file.Blocksize0Pow;
            int blocksize1 = 1 << file.Blocksize1Pow;

            bool previousBlockflag = false;
            long granule = 0;
            int previousBlocksize = 0;

            for (int i = 0; i < offsets.Count; i++)
            {
                bool last = i == offsets.Count - 1;
                byte[] packet = RebuildPacket(
                    wem, offsets[i], sizes[i], modes[i], modeBlockflag, modeBits,
                    previousBlockflag,
                    last ? false : modeBlockflag[modes[i + 1]]);

                previousBlockflag = modeBlockflag[modes[i]];

                //A Vorbis packet emits the second half of the previous window overlapped with the first
                //half of its own, so the sample count depends on both
                int blocksize = modeBlockflag[modes[i]] ? blocksize1 : blocksize0;
                if (i > 0)
                    granule += (previousBlocksize + blocksize) / 4;

                previousBlocksize = blocksize;

                long pageGranule = granule;
                if (last && file.SampleCount != 0 && file.SampleCount < granule)
                    pageGranule = file.SampleCount; //trim the encoder's padding off the end

                ogg.WritePacket(packet, pageGranule, false, last);
            }
        }

        private static byte[] RebuildPacket(byte[] wem, int offset, int size, int mode, bool[] modeBlockflag, int modeBits, bool previousBlockflag, bool nextBlockflag)
        {
            VorbisBitWriter output = new VorbisBitWriter(size + 4);
            if (size == 0)
                return output.ToArray();

            VorbisBitReader input = new VorbisBitReader(wem, offset, size);

            output.Write(0, 1); //packet type: audio

            uint modeNumber = input.Read(modeBits);
            output.Write(modeNumber, modeBits);

            //The rest of the first byte has to be held back until the window flags are in place
            uint remainder = input.Read(8 - modeBits);

            if (modeBlockflag[mode])
            {
                output.Write(previousBlockflag ? 1u : 0u, 1);
                output.Write(nextBlockflag ? 1u : 0u, 1);
            }

            output.Write(remainder, 8 - modeBits);

            //Exactly one byte was consumed above, so the body can go through whole rather than a bit at
            //a time - which for a long sound is the difference between milliseconds and seconds
            output.WriteBytes(wem, offset + 1, size - 1);

            return output.ToArray();
        }

        #endregion
    }
}

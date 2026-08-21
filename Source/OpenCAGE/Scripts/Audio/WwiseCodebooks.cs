using System;
using System.IO;
using System.Reflection;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// The external codebook table Wwise streams are encoded against.
    ///
    /// A Wwise Vorbis stream doesn't carry its own codebooks - it carries ten-bit indices into a table
    /// the encoder and the runtime both already have, which is how it saves a couple of hundred bytes on
    /// every sound in the game. That table is the standard aoTuV 6.03 set, and it cannot be recovered
    /// from the game files, so it ships with the editor.
    ///
    /// The codebooks are also stored more tightly than Vorbis allows: no sync pattern, narrower fields,
    /// and codeword lengths packed to the smallest width that fits. Rebuilding one is a matter of
    /// widening each field back to its specified size and putting the sync pattern back on the front.
    ///
    /// Licensing: aoTuV is derived from the Xiph.Org Foundation's libvorbis and carries its BSD
    /// 3-clause licence, which permits redistribution in binary form with the notice reproduced. See
    /// the third-party notices file in the repository root.
    /// </summary>
    internal sealed class WwiseCodebooks
    {
        private const string ResourceName = "OpenCAGE.Resources.packed_codebooks_aoTuV_603.bin";

        private static WwiseCodebooks _instance;
        private static readonly object _lock = new object();

        private byte[] _data;
        private int[] _offsets;

        public static WwiseCodebooks Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = Load();

                    return _instance;
                }
            }
        }

        public int Count
        {
            get { return _offsets == null ? 0 : _offsets.Length - 1; }
        }

        private static WwiseCodebooks Load()
        {
            WwiseCodebooks library = new WwiseCodebooks();

            byte[] file;
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName))
            {
                if (stream == null)
                    throw new InvalidOperationException("The Wwise codebook table is missing from this build.");

                file = new byte[stream.Length];
                int read = 0;
                while (read < file.Length)
                {
                    int got = stream.Read(file, read, file.Length - read);
                    if (got <= 0)
                        break;
                    read += got;
                }
            }

            //The table ends with the offset of its own index; everything before that is codebook data,
            //and the index runs one entry past the last book so that sizes can be subtracted
            int indexOffset = BitConverter.ToInt32(file, file.Length - 4);
            if (indexOffset <= 0 || indexOffset > file.Length - 4)
                throw new InvalidDataException("The Wwise codebook table is corrupt.");

            int entries = (file.Length - indexOffset) / 4;
            library._data = file;
            library._offsets = new int[entries];
            for (int i = 0; i < entries; i++)
                library._offsets[i] = BitConverter.ToInt32(file, indexOffset + i * 4);

            return library;
        }

        /// <summary>
        /// Expand one packed codebook into the standard Vorbis form, writing it to <paramref name="output"/>.
        /// </summary>
        public void Rebuild(int index, VorbisBitWriter output)
        {
            if (index < 0 || index >= Count)
                throw new InvalidDataException("Sound references codebook " + index + ", which is outside the table.");

            int offset = _offsets[index];
            int size = _offsets[index + 1] - offset;
            VorbisBitReader input = new VorbisBitReader(_data, offset, size);

            uint dimensions = input.Read(4);
            uint entries = input.Read(14);

            output.Write(0x564342, 24); //"BCV", the codebook sync pattern
            output.Write(dimensions, 16);
            output.Write(entries, 24);

            uint ordered = input.Read(1);
            output.Write(ordered, 1);

            if (ordered != 0)
            {
                //Lengths given as runs, which Vorbis stores the same way
                output.Write(input.Read(5), 5);

                uint current = 0;
                while (current < entries)
                {
                    uint number = input.Read(ILog(entries - current));
                    output.Write(number, ILog(entries - current));
                    current += number;
                }

                if (current > entries)
                    throw new InvalidDataException("Codebook " + index + " overruns its entry count.");
            }
            else
            {
                //Wwise packs each length in the fewest bits the book needs; Vorbis always uses five
                int lengthBits = (int)input.Read(3);
                uint sparse = input.Read(1);
                if (lengthBits == 0 || lengthBits > 5)
                    throw new InvalidDataException("Codebook " + index + " has a nonsensical codeword length.");

                output.Write(sparse, 1);

                for (uint i = 0; i < entries; i++)
                {
                    bool present = true;
                    if (sparse != 0)
                    {
                        uint flag = input.Read(1);
                        output.Write(flag, 1);
                        present = flag != 0;
                    }

                    if (present)
                        output.Write(input.Read(lengthBits), 5);
                }
            }

            uint lookupType = input.Read(1) != 0 ? 1u : 0u;
            output.Write(lookupType, 4);

            if (lookupType == 1)
            {
                output.Write(input.Read(32), 32); //minimum
                output.Write(input.Read(32), 32); //delta
                uint valueLength = input.Read(4);
                output.Write(valueLength, 4);
                output.Write(input.Read(1), 1); //sequence flag

                uint quantised = QuantisedValues(entries, dimensions);
                for (uint i = 0; i < quantised; i++)
                    output.Write(input.Read((int)valueLength + 1), (int)valueLength + 1);
            }

            //Every codebook should be consumed to its last byte; anything else means the table and the
            //stream disagree, and carrying on would produce noise
            if (input.BitsRead / 8 + 1 != size)
                throw new InvalidDataException("Codebook " + index + " did not decode to its stored size.");
        }

        /// <summary>
        /// The number of quantised values a lookup-type-1 codebook stores - the largest n where
        /// n^dimensions still fits inside the entry count. Taken from the Vorbis specification.
        /// </summary>
        private static uint QuantisedValues(uint entries, uint dimensions)
        {
            if (dimensions == 0)
                return 0;

            int bits = ILog(entries);
            uint values = entries >> (int)((bits - 1) * (dimensions - 1) / dimensions);

            while (true)
            {
                ulong acc = 1, accNext = 1;
                for (uint i = 0; i < dimensions; i++)
                {
                    acc *= values;
                    accNext *= values + 1;
                }

                if (acc <= entries && accNext > entries)
                    return values;

                if (acc > entries)
                    values--;
                else
                    values++;
            }
        }

        internal static int ILog(uint value)
        {
            int result = 0;
            while (value != 0)
            {
                result++;
                value >>= 1;
            }

            return result;
        }
    }
}

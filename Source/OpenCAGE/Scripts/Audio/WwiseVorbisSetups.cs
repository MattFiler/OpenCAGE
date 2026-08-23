using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// The decoder figures that go with a Vorbis setup, which cannot be worked out from the audio.
    ///
    /// Three fields in a .wem's header describe the decoder rather than the sound: how much memory the
    /// runtime has to give the decoder on a 32 and a 64 bit build, and a hash identifying the setup it
    /// was built for. They follow from the setup - the codebooks, floors and residues in use - not from
    /// the samples, so there is nothing in an encoded stream to derive them from.
    ///
    /// A stream whose setup isn't in the table cannot be written safely: the runtime would size its
    /// decoder from a guess. That is reported rather than approximated, and the encoder is steered at a
    /// setup that is in the table - see <see cref="VorbisEncoder"/>.
    /// </summary>
    internal static class WwiseVorbisSetups
    {
        private const string ResourceName = "OpenCAGE.Resources.wwise_vorbis_setups.bin";

        /// <summary>What the runtime needs to be told about the decoder for a given setup.</summary>
        public sealed class Decoder
        {
            public uint AllocSize32;
            public uint AllocSize64;
            public uint SetupHash;

            /// <summary>
            /// The setup packet these figures were harvested against - the game's own bytes, which is
            /// what gets written, so that anything the runtime checks against them still holds.
            /// </summary>
            public byte[] Setup;

            /// <summary>The same setup with repeated codebooks folded onto their first index.</summary>
            internal byte[] Canonical;
        }

        private static readonly object _lock = new object();
        private static List<Decoder> _known;

        /// <summary>Every setup the game uses, most common first.</summary>
        public static IList<Decoder> All
        {
            get
            {
                lock (_lock)
                {
                    if (_known == null)
                        _known = Load();

                    return _known;
                }
            }
        }

        /// <summary>
        /// The decoder figures for a packed setup packet, or null when the game has never seen it.
        /// </summary>
        public static Decoder Find(byte[] setupPacket)
        {
            if (setupPacket == null)
                return null;

            //Compared in canonical form: the same setup can be written with different codebook indices
            //where the table repeats a book, and those are the same setup as far as the decoder cares
            byte[] wanted = Canonicalise(setupPacket);
            if (wanted == null)
                return null;

            foreach (Decoder candidate in All)
            {
                if (candidate.Canonical == null || candidate.Canonical.Length != wanted.Length)
                    continue;

                bool same = true;
                for (int i = 0; i < wanted.Length && same; i++)
                    same = candidate.Canonical[i] == wanted[i];

                if (same)
                    return candidate;
            }

            return null;
        }

        /// <summary>
        /// Rewrite a packed setup's codebook references so that repeated books all use the first index
        /// they appear at. Everything after the references is copied through untouched.
        /// </summary>
        private static byte[] Canonicalise(byte[] setupPacket)
        {
            try
            {
                VorbisBitReader input = new VorbisBitReader(setupPacket, 0, setupPacket.Length);
                VorbisBitWriter output = new VorbisBitWriter(setupPacket.Length);

                uint count = input.Read(8);
                output.Write(count, 8);

                for (uint i = 0; i <= count; i++)
                    output.Write((uint)WwiseCodebooks.Instance.Canonical((int)input.Read(10)), 10);

                //The rest is opaque here - floors, residues, mappings and modes - and goes through as is
                long remaining = (long)setupPacket.Length * 8 - input.BitsRead;
                while (remaining >= 8)
                {
                    output.Write(input.Read(8), 8);
                    remaining -= 8;
                }
                if (remaining > 0)
                    output.Write(input.Read((int)remaining), (int)remaining);

                return output.ToArray();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>As <see cref="Find"/>, but refuses to carry on rather than guessing.</summary>
        public static Decoder For(byte[] setupPacket, int channels, int blocksize0Pow, int blocksize1Pow)
        {
            Decoder found = Find(setupPacket);
            if (found != null)
                return found;

            throw new NotSupportedException(
                "This audio encodes to a Vorbis setup the game has never used (" + channels + " channels, "
                + "blocks " + blocksize0Pow + "/" + blocksize1Pow + "), so the decoder could not be sized. "
                + "Importing it at a different quality or sample rate normally lands on one it knows.");
        }

        private static List<Decoder> Load()
        {
            byte[] file;
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName))
            {
                if (stream == null)
                    throw new InvalidOperationException("The Wwise setup table is missing from this build.");

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

            List<Decoder> result = new List<Decoder>();
            using (BinaryReader reader = new BinaryReader(new MemoryStream(file)))
            {
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    Decoder decoder = new Decoder
                    {
                        AllocSize32 = reader.ReadUInt32(),
                        AllocSize64 = reader.ReadUInt32(),
                        SetupHash = reader.ReadUInt32(),
                    };
                    decoder.Setup = reader.ReadBytes(reader.ReadInt32());
                    decoder.Canonical = Canonicalise(decoder.Setup);
                    result.Add(decoder);
                }
            }

            return result;
        }
    }
}

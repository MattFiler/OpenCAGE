using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// Puts a piece of audio back where the game will find it.
    ///
    /// A sound's media lives in one of three places, and each takes a different kind of edit: a loose
    /// .wem is just a file; a stream inside a .pck is a table entry pointing at a byte range, so it can
    /// be repointed without moving anything else; media held in a .bnk is inside a chunk whose entries
    /// are all relative to it, so the bank is rewritten around the replacement.
    ///
    /// The game's files are edited in place and nothing is kept behind: a soundbank runs to tens of
    /// megabytes and a dialogue package to hundreds, so a copy per file touched is not a reasonable
    /// price for an undo. Verifying the game's files puts them back.
    /// </summary>
    internal static class WwiseMediaInjector
    {
        public sealed class Plan
        {
            /// <summary>The file that would be written to.</summary>
            public string File;

            /// <summary>What kind of container it is, for the user to read.</summary>
            public string Kind;

            public bool Supported;

            /// <summary>Why it can't be done, when it can't.</summary>
            public string Problem;

            /// <summary>How much bigger the file gets - zero when the audio fits where the old audio was.</summary>
            public long Growth;
        }

        /// <summary>Work out what replacing this media would involve, without doing it.</summary>
        public static Plan Examine(WwiseMediaLocation media, int length)
        {
            Plan plan = new Plan();
            if (media == null || string.IsNullOrEmpty(media.File))
            {
                plan.Problem = "This sound has no media to replace.";
                return plan;
            }

            plan.File = media.File;

            string extension = Path.GetExtension(media.File).ToLowerInvariant();
            switch (extension)
            {
                case ".wem":
                    plan.Kind = "a loose stream";
                    plan.Supported = true;
                    plan.Growth = Math.Max(0, length - media.Length);
                    return plan;

                case ".pck":
                {
                    plan.Kind = "a file package";
                    WwiseFilePackage package = WwiseFilePackage.Load(media.File);
                    WwiseFilePackage.Entry entry = FindStream(package, media);
                    if (entry == null)
                    {
                        plan.Problem = "This sound is inside a soundbank that is itself packed into "
                            + Path.GetFileName(media.File) + ", which cannot be edited in place yet.";
                        return plan;
                    }

                    plan.Supported = true;
                    plan.Growth = length <= entry.Length ? 0 : length;
                    return plan;
                }

                case ".bnk":
                    plan.Kind = "a soundbank";
                    plan.Supported = true;
                    plan.Growth = Math.Max(0, length - media.Length);
                    return plan;

                default:
                    plan.Problem = "This sound lives in a " + extension + " file, which is not something that can be edited.";
                    return plan;
            }
        }

        /// <summary>Replace the audio at <paramref name="media"/> with <paramref name="wem"/>.</summary>
        public static void Replace(WwiseMediaLocation media, byte[] wem)
        {
            Plan plan = Examine(media, wem.Length);
            if (!plan.Supported)
                throw new NotSupportedException(plan.Problem);

            switch (Path.GetExtension(media.File).ToLowerInvariant())
            {
                case ".wem":
                    File.WriteAllBytes(media.File, wem);
                    media.Offset = 0;
                    media.Length = wem.Length;
                    break;

                case ".pck":
                    ReplaceInPackage(media, wem);
                    break;

                case ".bnk":
                    ReplaceInBank(media, wem);
                    break;
            }
        }

        private static WwiseFilePackage.Entry FindStream(WwiseFilePackage package, WwiseMediaLocation media)
        {
            foreach (WwiseFilePackage.Entry entry in package.Streams)
                if (entry.Offset == media.Offset)
                    return entry;

            return null;
        }

        #region FILE PACKAGE

        /// <summary>
        /// A package's directory holds each stream's start block and length, so replacing one is a matter
        /// of writing the bytes somewhere and pointing the entry at them. Audio that still fits goes back
        /// where it was; anything larger is appended, which leaves the old bytes stranded but means a 300
        /// megabyte package is touched in two places rather than rewritten.
        /// </summary>
        private static void ReplaceInPackage(WwiseMediaLocation media, byte[] wem)
        {
            using (FileStream stream = new FileStream(media.File, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            using (BinaryReader reader = new BinaryReader(stream, Encoding.ASCII, true))
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.ASCII, true))
            {
                if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "AKPK")
                    throw new InvalidDataException("That is not a Wwise file package.");

                reader.ReadUInt32(); //header size
                if (reader.ReadUInt32() != 1)
                    throw new InvalidDataException("That file package is a version this cannot edit.");

                uint languageMapSize = reader.ReadUInt32();
                uint banksTableSize = reader.ReadUInt32();
                reader.ReadUInt32(); //streams table size
                reader.ReadUInt32(); //externals

                long streamsTable = stream.Position + languageMapSize + banksTableSize;
                stream.Position = streamsTable;
                uint count = reader.ReadUInt32();

                for (uint i = 0; i < count; i++)
                {
                    long entryAt = stream.Position;
                    reader.ReadUInt32(); //id
                    uint blockSize = Math.Max(1u, reader.ReadUInt32());
                    uint fileSize = reader.ReadUInt32();
                    uint startBlock = reader.ReadUInt32();
                    reader.ReadUInt32(); //language

                    if ((long)startBlock * blockSize != media.Offset)
                        continue;

                    long offset;
                    if (wem.Length <= fileSize)
                    {
                        //Fits where it was: only the length changes
                        offset = media.Offset;
                    }
                    else
                    {
                        offset = Align(stream.Length, blockSize);
                        if (offset / blockSize > uint.MaxValue)
                            throw new NotSupportedException("That package is too large to add to.");

                        stream.Position = entryAt + 12;
                        writer.Write((uint)(offset / blockSize));
                    }

                    stream.Position = entryAt + 8;
                    writer.Write((uint)wem.Length);

                    stream.Position = offset;
                    writer.Write(wem);
                    stream.SetLength(Math.Max(stream.Length, offset + wem.Length));

                    media.Offset = offset;
                    media.Length = wem.Length;
                    return;
                }

                throw new InvalidDataException("That sound is no longer in the package where it was found.");
            }
        }

        private static long Align(long value, uint alignment)
        {
            long remainder = value % alignment;
            return remainder == 0 ? value : value + (alignment - remainder);
        }

        #endregion

        #region SOUNDBANK

        /// <summary>
        /// A bank's media directory gives each piece of audio an offset inside the bank's data chunk, so
        /// replacing one with something a different size moves everything after it. The bank is rebuilt
        /// with a fresh directory and data chunk; every other chunk is copied through untouched.
        /// </summary>
        private static void ReplaceInBank(WwiseMediaLocation media, byte[] wem)
        {
            byte[] bank = File.ReadAllBytes(media.File);

            long didxAt = -1, dataAt = -1;
            int didxLength = 0, dataLength = 0;
            List<KeyValuePair<string, byte[]>> chunks = new List<KeyValuePair<string, byte[]>>();

            int position = 0;
            while (position + 8 <= bank.Length)
            {
                string tag = Encoding.ASCII.GetString(bank, position, 4);
                int size = BitConverter.ToInt32(bank, position + 4);
                if (size < 0 || position + 8 + size > bank.Length)
                    break;

                if (tag == "DIDX") { didxAt = position + 8; didxLength = size; }
                else if (tag == "DATA") { dataAt = position + 8; dataLength = size; }

                byte[] body = new byte[size];
                Array.Copy(bank, position + 8, body, 0, size);
                chunks.Add(new KeyValuePair<string, byte[]>(tag, body));

                position += 8 + size;
            }

            if (didxAt < 0 || dataAt < 0)
                throw new InvalidDataException("That soundbank holds no audio.");

            //--- read the directory, swap in the new audio, and lay the data chunk out again ---
            int entries = didxLength / 12;
            List<uint> ids = new List<uint>();
            List<byte[]> bodies = new List<byte[]>();
            int replacedIndex = -1;

            for (int i = 0; i < entries; i++)
            {
                int at = (int)didxAt + i * 12;
                uint id = BitConverter.ToUInt32(bank, at);
                uint offset = BitConverter.ToUInt32(bank, at + 4);
                uint length = BitConverter.ToUInt32(bank, at + 8);

                ids.Add(id);
                if (dataAt + offset == media.Offset)
                {
                    bodies.Add(wem);
                    replacedIndex = i;
                }
                else
                {
                    byte[] body = new byte[length];
                    Array.Copy(bank, dataAt + offset, body, 0, length);
                    bodies.Add(body);
                }
            }

            if (replacedIndex < 0)
                throw new InvalidDataException("That sound is no longer in the soundbank where it was found.");

            //Wwise pads each piece of audio up to a sixteen byte boundary; matching that keeps the bank
            //looking like the ones the game shipped
            const int Alignment = 16;
            byte[] didx = new byte[entries * 12];
            Dictionary<uint, KeyValuePair<uint, uint>> placed = new Dictionary<uint, KeyValuePair<uint, uint>>();
            using (MemoryStream data = new MemoryStream(dataLength + wem.Length))
            {
                for (int i = 0; i < entries; i++)
                {
                    while (data.Position % Alignment != 0)
                        data.WriteByte(0);

                    int offset = (int)data.Position;
                    data.Write(bodies[i], 0, bodies[i].Length);

                    BitConverter.GetBytes(ids[i]).CopyTo(didx, i * 12);
                    BitConverter.GetBytes((uint)offset).CopyTo(didx, i * 12 + 4);
                    BitConverter.GetBytes((uint)bodies[i].Length).CopyTo(didx, i * 12 + 8);
                    placed[ids[i]] = new KeyValuePair<uint, uint>((uint)offset, (uint)bodies[i].Length);
                }

                byte[] newData = data.ToArray();

                /* The directory is not the only place the bank says where its audio is: an in-bank source
                 * record inside the object hierarchy carries the same media's position and size as well,
                 * and its position is absolute within the bank file rather than relative to the data
                 * chunk. Both have to be put right, so the layout is worked out first. */
                long dataBody = 0, cursor = 0;
                foreach (KeyValuePair<string, byte[]> chunk in chunks)
                {
                    int length = chunk.Key == "DIDX" ? didx.Length : chunk.Key == "DATA" ? newData.Length : chunk.Value.Length;
                    if (chunk.Key == "DATA")
                        dataBody = cursor + 8;
                    cursor += 8 + length;
                }

                //--- write the bank back out, chunk for chunk ---
                using (MemoryStream output = new MemoryStream(bank.Length + wem.Length))
                {
                    foreach (KeyValuePair<string, byte[]> chunk in chunks)
                    {
                        byte[] body = chunk.Value;
                        if (chunk.Key == "DIDX") body = didx;
                        else if (chunk.Key == "DATA") body = newData;
                        else if (chunk.Key == "HIRC") body = PatchSourceRecords(chunk.Value, placed, dataBody);

                        output.Write(Encoding.ASCII.GetBytes(chunk.Key), 0, 4);
                        output.Write(BitConverter.GetBytes(body.Length), 0, 4);
                        output.Write(body, 0, body.Length);
                    }

                    File.WriteAllBytes(media.File, output.ToArray());
                }
            }

            //Everything in the bank has moved, so where the audio ended up is read back out of the file
            //it was just written to rather than tracked through the rebuild
            media.Length = wem.Length;
            WwiseMediaLocation moved;
            if (WwiseSoundBank.Load(media.File).EmbeddedMedia.TryGetValue(ids[replacedIndex], out moved))
                media.Offset = moved.Offset;
        }

        /// <summary>
        /// Point every in-bank source record at where its audio has ended up.
        ///
        /// A source that lives in the bank records the media's position and size inside the object
        /// itself, on top of the directory entry - the position counted from the start of the bank file.
        /// A prefetched source is the opening of a file that streams from elsewhere: the game stores
        /// zero for its position and only its size matters.
        /// </summary>
        private static byte[] PatchSourceRecords(byte[] hirc, Dictionary<uint, KeyValuePair<uint, uint>> media, long dataBody)
        {
            byte[] result = (byte[])hirc.Clone();
            if (result.Length < 4)
                return result;

            uint count = BitConverter.ToUInt32(result, 0);
            int cursor = 4;

            for (uint i = 0; i < count; i++)
            {
                if (cursor + 9 > result.Length)
                    break;

                byte type = result[cursor];
                uint sectionSize = BitConverter.ToUInt32(result, cursor + 1);
                int body = cursor + 9;
                int bodyLength = (int)sectionSize - 4;
                cursor = cursor + 5 + (int)sectionSize;

                if (sectionSize < 4 || body + bodyLength > result.Length)
                    break;

                if (type == (byte)WwiseObjectType.Sound)
                {
                    PatchSource(result, body, bodyLength, media, dataBody);
                }
                else if (type == (byte)WwiseObjectType.MusicTrack)
                {
                    //A track carries its sources inline, behind a flag byte and a count
                    if (bodyLength < 5)
                        continue;

                    uint sources = BitConverter.ToUInt32(result, body + 1);
                    if (sources > 256)
                        continue;

                    int at = body + 5;
                    for (uint s = 0; s < sources; s++)
                    {
                        int length = PatchSource(result, at, body + bodyLength - at, media, dataBody);
                        if (length == 0)
                            break;
                        at += length;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Fix one AkBankSourceData record in place, returning how long it was so a run of them can be
        /// walked. Returns zero when the record doesn't fit, which stops the walk.
        /// </summary>
        private static int PatchSource(byte[] hirc, int at, int available, Dictionary<uint, KeyValuePair<uint, uint>> media, long dataBody)
        {
            if (available < 16)
                return 0;

            uint plugin = BitConverter.ToUInt32(hirc, at);
            uint streamType = BitConverter.ToUInt32(hirc, at + 4);
            uint sourceId = BitConverter.ToUInt32(hirc, at + 8);

            if ((plugin & 0x0F) != 1)
                return 11; //a tone or silence source, which has no media at all
            if (streamType == (uint)WwiseStreamType.Streamed)
                return 17; //played straight off disk: the bank holds none of it

            if (available < 25)
                return 0;

            KeyValuePair<uint, uint> entry;
            if (media.TryGetValue(sourceId, out entry))
            {
                if (streamType != (uint)WwiseStreamType.PrefetchStreamed)
                    BitConverter.GetBytes((uint)(dataBody + entry.Key)).CopyTo(hirc, at + 16);

                BitConverter.GetBytes(entry.Value).CopyTo(hirc, at + 20);
            }

            return 25;
        }

        #endregion
    }
}

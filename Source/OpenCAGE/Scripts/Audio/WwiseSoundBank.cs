using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// A parsed Wwise soundbank (.bnk).
    ///
    /// Only the index is kept: the object graph, the embedded media directory, and the bank name table.
    /// The DATA chunk - which is nearly all of the file - is never read in, just located, so that
    /// indexing all 270 of the game's banks stays cheap and audio is pulled off disk on demand.
    ///
    /// Containers are walked by inverting every object's parent id rather than by reading each
    /// container's own child list. The child list sits at the end of NodeBaseParams, behind a run of
    /// version-dependent variable-length structures (positioning, aux sends, state chunks, RTPCs) that
    /// are easy to get subtly wrong; the parent id sits a fixed few bytes into the same structure, in
    /// front of all of it. Measured across every bank in the game, the parent id resolves for 100% of
    /// containers and 100% of sounds, so the inverted map is a complete tree with none of the risk.
    /// </summary>
    public sealed class WwiseSoundBank
    {
        /// <summary>The file the bank was read from - a .bnk, or the .pck it is packed inside.</summary>
        public string FilePath { get; private set; }

        /// <summary>Where the bank starts within <see cref="FilePath"/>.</summary>
        public long BaseOffset { get; private set; }

        public string Name { get; private set; }
        public uint Id { get; private set; }
        public uint Version { get; private set; }

        public List<WwiseObject> Objects = new List<WwiseObject>();

        /// <summary>Audio held in this bank's DATA chunk, by source id.</summary>
        public Dictionary<uint, WwiseMediaLocation> EmbeddedMedia = new Dictionary<uint, WwiseMediaLocation>();

        /// <summary>Bank id to name, as listed in the bank's STID chunk.</summary>
        public Dictionary<uint, string> BankNames = new Dictionary<uint, string>();

        private WwiseSoundBank()
        {
        }

        public static WwiseSoundBank Load(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                return Load(stream, 0, stream.Length, path, Path.GetFileNameWithoutExtension(path));
        }

        public static WwiseSoundBank Load(Stream stream, long offset, long length, string backingFile, string fallbackName)
        {
            WwiseSoundBank bank = new WwiseSoundBank();
            bank.FilePath = backingFile;
            bank.BaseOffset = offset;
            bank.Name = fallbackName;

            long end = offset + length;
            long didxOffset = -1;
            int didxLength = 0;
            long dataOffset = -1;

            BinaryReader reader = new BinaryReader(stream, Encoding.ASCII, true);
            long position = offset;
            while (position + 8 <= end)
            {
                stream.Position = position;
                string tag = Encoding.ASCII.GetString(reader.ReadBytes(4));
                uint size = reader.ReadUInt32();
                long body = position + 8;
                if (size > end - body)
                    break;

                switch (tag)
                {
                    case "BKHD":
                        bank.Version = reader.ReadUInt32();
                        bank.Id = reader.ReadUInt32();
                        break;
                    case "DIDX":
                        //Held until DATA is located, since the entries are relative to it
                        didxOffset = body;
                        didxLength = (int)size;
                        break;
                    case "DATA":
                        dataOffset = body;
                        break;
                    case "HIRC":
                        bank.ReadHirc(reader, body, (int)size);
                        break;
                    case "STID":
                        bank.ReadStid(reader, body, (int)size);
                        break;
                }

                position = body + size;
            }

            if (didxOffset >= 0 && dataOffset >= 0)
                bank.ReadDidx(reader, didxOffset, didxLength, dataOffset);

            string ownName;
            if (bank.BankNames.TryGetValue(bank.Id, out ownName) && !string.IsNullOrEmpty(ownName))
                bank.Name = ownName;

            return bank;
        }

        private void ReadDidx(BinaryReader reader, long offset, int length, long dataOffset)
        {
            reader.BaseStream.Position = offset;
            int count = length / 12;
            for (int i = 0; i < count; i++)
            {
                uint id = reader.ReadUInt32();
                uint mediaOffset = reader.ReadUInt32();
                uint mediaLength = reader.ReadUInt32();

                EmbeddedMedia[id] = new WwiseMediaLocation
                {
                    File = FilePath,
                    Offset = dataOffset + mediaOffset,
                    Length = (int)mediaLength,
                    Origin = Name,
                };
            }
        }

        private void ReadStid(BinaryReader reader, long offset, int length)
        {
            reader.BaseStream.Position = offset;
            long end = offset + length;

            reader.ReadUInt32(); //type - always 1 here
            uint count = reader.ReadUInt32();
            for (uint i = 0; i < count; i++)
            {
                if (reader.BaseStream.Position + 5 > end)
                    break;

                uint id = reader.ReadUInt32();
                byte nameLength = reader.ReadByte();
                if (reader.BaseStream.Position + nameLength > end)
                    break;

                BankNames[id] = Encoding.ASCII.GetString(reader.ReadBytes(nameLength));
            }
        }

        private void ReadHirc(BinaryReader reader, long offset, int length)
        {
            reader.BaseStream.Position = offset;
            long end = offset + length;

            uint count = reader.ReadUInt32();
            for (uint i = 0; i < count; i++)
            {
                if (reader.BaseStream.Position + 9 > end)
                    break;

                long start = reader.BaseStream.Position;
                byte type = reader.ReadByte();
                uint sectionSize = reader.ReadUInt32();

                //The section size is counted from the object id onwards, so the body is four bytes shorter
                long next = start + 5 + sectionSize;
                if (sectionSize < 4 || next > end)
                    break;

                uint id = reader.ReadUInt32();
                byte[] body = reader.ReadBytes((int)sectionSize - 4);

                WwiseObject parsed = ParseObject((WwiseObjectType)type, id, body);
                if (parsed != null)
                {
                    parsed.Bank = this;
                    Objects.Add(parsed);
                }

                reader.BaseStream.Position = next;
            }
        }

        private static WwiseObject ParseObject(WwiseObjectType type, uint id, byte[] body)
        {
            switch (type)
            {
                case WwiseObjectType.Event:
                    return ParseEvent(id, body);
                case WwiseObjectType.Action:
                    return ParseAction(id, body);
                case WwiseObjectType.Sound:
                    return ParseSound(id, body);
                case WwiseObjectType.RandomSequenceContainer:
                case WwiseObjectType.SwitchContainer:
                case WwiseObjectType.ActorMixer:
                case WwiseObjectType.BlendContainer:
                    //Containers put NodeBaseParams first, so the parent id is right at the front
                    return new WwiseObject
                    {
                        Type = type,
                        Id = id,
                        ParentId = ReadParentId(body, 0),
                    };
                case WwiseObjectType.MusicTrack:
                    return ParseMusicTrack(id, body);
                default:
                    //Still indexed, so that parent and target lookups can resolve against it
                    return new WwiseObject { Type = type, Id = id };
            }
        }

        private static WwiseObject ParseEvent(uint id, byte[] body)
        {
            WwiseEvent result = new WwiseEvent { Type = WwiseObjectType.Event, Id = id };
            if (body.Length < 4)
                return result;

            uint count = BitConverter.ToUInt32(body, 0);
            if (count > 1024 || 4 + count * 4 > body.Length)
                return result;

            result.ActionIds = new uint[count];
            for (int i = 0; i < count; i++)
                result.ActionIds[i] = BitConverter.ToUInt32(body, 4 + i * 4);

            return result;
        }

        private static WwiseObject ParseAction(uint id, byte[] body)
        {
            WwiseAction result = new WwiseAction { Type = WwiseObjectType.Action, Id = id };
            if (body.Length < 6)
                return result;

            result.ActionType = BitConverter.ToUInt16(body, 0);
            result.TargetId = BitConverter.ToUInt32(body, 2);
            return result;
        }

        private static WwiseObject ParseSound(uint id, byte[] body)
        {
            WwiseSound result = new WwiseSound { Type = WwiseObjectType.Sound, Id = id };
            if (body.Length < 16)
                return result;

            result.PluginId = BitConverter.ToUInt32(body, 0);
            result.StreamType = (WwiseStreamType)BitConverter.ToUInt32(body, 4);
            result.SourceId = BitConverter.ToUInt32(body, 8);
            result.FileId = BitConverter.ToUInt32(body, 12);

            //AkBankSourceData runs ahead of NodeBaseParams and is not a fixed size. A streamed codec
            //source stops after the file id and its flag byte; an in-bank one also carries the offset
            //and size of its media. Source plugins - tone and silence - carry no media information at
            //all. Measured over every sound object in the game, these three lengths place the parent id
            //correctly 100% of the time.
            int sourceDataLength;
            if ((result.PluginId & 0x0F) != 1)
                sourceDataLength = 11;
            else if (result.StreamType == WwiseStreamType.Streamed)
                sourceDataLength = 17;
            else
                sourceDataLength = 25;

            result.ParentId = ReadParentId(body, sourceDataLength);
            return result;
        }

        private static WwiseObject ParseMusicTrack(uint id, byte[] body)
        {
            WwiseMusicTrack result = new WwiseMusicTrack { Type = WwiseObjectType.MusicTrack, Id = id };
            if (body.Length < 5)
                return result;

            //A track holds its sources inline: a flag byte, a count, then the same AkBankSourceData
            //records a sound would use. Anything that doesn't parse cleanly is dropped rather than
            //guessed at - the caller checks the ids against the media index anyway.
            int position = 1;
            uint count = BitConverter.ToUInt32(body, position);
            position += 4;
            if (count > 256)
                return result;

            for (uint i = 0; i < count; i++)
            {
                if (position + 16 > body.Length)
                    break;

                uint pluginId = BitConverter.ToUInt32(body, position);
                WwiseStreamType streamType = (WwiseStreamType)BitConverter.ToUInt32(body, position + 4);
                result.SourceIds.Add(BitConverter.ToUInt32(body, position + 8));

                if ((pluginId & 0x0F) != 1)
                    position += 11;
                else if (streamType == WwiseStreamType.Streamed)
                    position += 17;
                else
                    position += 25;
            }

            return result;
        }

        /// <summary>
        /// Read the parent id out of NodeBaseParams, which begins at <paramref name="offset"/>.
        ///
        /// The structure opens with the effect list - an override flag, a count, and then, only if the
        /// count is non-zero, a bypass bitfield and seven bytes per effect - followed by the bus id and
        /// then the parent id. Everything variable-length is behind those two fields, which is what
        /// makes this worth reading and the child list not.
        /// </summary>
        private static uint ReadParentId(byte[] body, int offset)
        {
            int position = offset;
            if (position + 2 > body.Length)
                return 0;

            position += 1; //override parent effects
            byte effectCount = body[position];
            position += 1;
            if (effectCount > 4)
                return 0;

            if (effectCount > 0)
                position += 1 + effectCount * 7; //bypass bits, then one record each

            position += 4; //bus id
            if (position + 4 > body.Length)
                return 0;

            return BitConverter.ToUInt32(body, position);
        }
    }
}

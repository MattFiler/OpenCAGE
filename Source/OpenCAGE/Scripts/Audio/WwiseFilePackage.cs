using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// A Wwise file package (.pck) - the AKPK container.
    ///
    /// Alien Isolation ships the localised dialogue this way, one package per language, and some levels
    /// add a level_sound_override.pck of their own next to their world data. A package holds two
    /// directories, one of banks and one of loose streams; both point at byte ranges inside the package
    /// itself, so nothing needs extracting to be read.
    /// </summary>
    public sealed class WwiseFilePackage
    {
        public sealed class Entry
        {
            public uint Id;
            public long Offset;
            public int Length;
            public uint LanguageId;
            public string Language;
        }

        public string FilePath { get; private set; }

        public List<Entry> Banks = new List<Entry>();
        public List<Entry> Streams = new List<Entry>();

        /// <summary>Language id to name. "sfx" is the entry used for everything not voiced.</summary>
        public Dictionary<uint, string> Languages = new Dictionary<uint, string>();

        private WwiseFilePackage()
        {
        }

        public static bool IsFilePackage(string path)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    byte[] magic = new byte[4];
                    return stream.Read(magic, 0, 4) == 4 && Encoding.ASCII.GetString(magic) == "AKPK";
                }
            }
            catch
            {
                return false;
            }
        }

        public static WwiseFilePackage Load(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader reader = new BinaryReader(stream, Encoding.ASCII))
            {
                if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "AKPK")
                    throw new InvalidDataException("Not a Wwise file package: " + path);

                WwiseFilePackage package = new WwiseFilePackage();
                package.FilePath = path;

                reader.ReadUInt32(); //header size
                uint version = reader.ReadUInt32();
                if (version != 1)
                    throw new InvalidDataException("Unsupported file package version " + version + ": " + path);

                uint languageMapSize = reader.ReadUInt32();
                uint banksTableSize = reader.ReadUInt32();
                uint streamsTableSize = reader.ReadUInt32();
                reader.ReadUInt32(); //externals table size - never used by this game

                long languageMap = stream.Position;
                package.ReadLanguages(reader, languageMap);

                stream.Position = languageMap + languageMapSize;
                package.ReadTable(reader, banksTableSize, package.Banks);

                stream.Position = languageMap + languageMapSize + banksTableSize;
                package.ReadTable(reader, streamsTableSize, package.Streams);

                return package;
            }
        }

        private void ReadLanguages(BinaryReader reader, long mapStart)
        {
            uint count = reader.ReadUInt32();
            if (count > 64)
                return;

            uint[] offsets = new uint[count];
            uint[] ids = new uint[count];
            for (uint i = 0; i < count; i++)
            {
                offsets[i] = reader.ReadUInt32();
                ids[i] = reader.ReadUInt32();
            }

            for (uint i = 0; i < count; i++)
            {
                //Offsets are relative to the start of the map, and the names are null terminated UTF-16
                reader.BaseStream.Position = mapStart + offsets[i];

                StringBuilder name = new StringBuilder();
                while (true)
                {
                    char c = (char)reader.ReadUInt16();
                    if (c == '\0')
                        break;
                    name.Append(c);
                }

                Languages[ids[i]] = name.ToString();
            }
        }

        private void ReadTable(BinaryReader reader, uint tableSize, List<Entry> into)
        {
            if (tableSize < 4)
                return;

            uint count = reader.ReadUInt32();
            if (count > (tableSize - 4) / 20)
                return;

            for (uint i = 0; i < count; i++)
            {
                uint id = reader.ReadUInt32();
                uint blockSize = reader.ReadUInt32();
                uint length = reader.ReadUInt32();
                uint startBlock = reader.ReadUInt32();
                uint languageId = reader.ReadUInt32();

                string language;
                Languages.TryGetValue(languageId, out language);

                into.Add(new Entry
                {
                    Id = id,
                    //Blocks are counted from the start of the package, not from the end of the header
                    Offset = (long)startBlock * Math.Max(1u, blockSize),
                    Length = (int)length,
                    LanguageId = languageId,
                    Language = language,
                });
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// A Wwise file package (.pck) to change and write back out - a level's own sound override.
    ///
    /// What is written is the layout of every package the game ships, measured across the challenge maps' overrides
    /// and the dialogue packages: the magic; the size of everything from there to the first file; version 1; the sizes
    /// of the language map and the three tables; the language map; the bank and stream tables, each sorted by id and
    /// then language (the game finds its entries by binary search, so an unsorted table loses files); an empty
    /// externals table; then the files themselves, banks first, back to back in table order at a block size of one.
    /// Reading a shipped package and writing it back out unchanged reproduces it byte for byte.
    /// </summary>
    internal sealed class WwiseFilePackageBuilder
    {
        /// <summary>One file in the package: bytes in memory, or a range of the package it was read from.</summary>
        public sealed class Item
        {
            public uint Id;
            public uint LanguageId;

            /// <summary>The file's bytes, once they have been changed or added. Null while it is still where it was read.</summary>
            public byte[] Data;

            public string SourceFile;
            public long SourceOffset;
            public int SourceLength;

            public int Length
            {
                get { return Data != null ? Data.Length : SourceLength; }
            }

            public byte[] Read()
            {
                if (Data != null)
                    return Data;

                using (FileStream stream = new FileStream(SourceFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                    return ReadRange(stream, SourceOffset, SourceLength);
            }
        }

        public List<Item> Banks = new List<Item>();
        public List<Item> Streams = new List<Item>();

        /// <summary>Language id to name, as the package lists them. "sfx" is everything that isn't voiced.</summary>
        public Dictionary<uint, string> Languages = new Dictionary<uint, string>();

        /// <summary>
        /// The language map, written back exactly as it was read: the entries' language ids index it, and the shipped
        /// packages that carry voiced audio list every language whether or not they use it.
        /// </summary>
        private byte[] _languageMap;

        private WwiseFilePackageBuilder()
        {
        }

        /// <summary>A package with nothing in it yet, listing "sfx" alone - as the shipped overrides that carry no dialogue do.</summary>
        public static WwiseFilePackageBuilder Create()
        {
            WwiseFilePackageBuilder package = new WwiseFilePackageBuilder();

            //One entry - the name's offset from the start of the map, and its id - then the name, null terminated UTF-16
            using (MemoryStream map = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(map))
            {
                writer.Write(1u);
                writer.Write(12u);
                writer.Write(0u);
                writer.Write(Encoding.Unicode.GetBytes("sfx\0"));
                package._languageMap = map.ToArray();
            }

            package.Languages[0] = "sfx";
            return package;
        }

        public static WwiseFilePackageBuilder Load(string path)
        {
            WwiseFilePackage read = WwiseFilePackage.Load(path);
            WwiseFilePackageBuilder package = new WwiseFilePackageBuilder();
            package.Languages = new Dictionary<uint, string>(read.Languages);

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                stream.Position = 12;
                uint languageMapSize = reader.ReadUInt32();
                uint banksTableSize = reader.ReadUInt32();
                uint streamsTableSize = reader.ReadUInt32();
                uint externalsTableSize = reader.ReadUInt32();
                package._languageMap = ReadRange(stream, 28, (int)languageMapSize);

                //Nothing in this game uses externals; one that did would lose them, so it isn't touched
                stream.Position = 28 + languageMapSize + banksTableSize + streamsTableSize;
                if (externalsTableSize >= 4 && reader.ReadUInt32() != 0)
                    throw new InvalidDataException(Path.GetFileName(path) + " lists external files, which can't be kept when it is written again.");
            }

            foreach (WwiseFilePackage.Entry entry in read.Banks)
                package.Banks.Add(FromEntry(entry, path));
            foreach (WwiseFilePackage.Entry entry in read.Streams)
                package.Streams.Add(FromEntry(entry, path));

            return package;
        }

        private static Item FromEntry(WwiseFilePackage.Entry entry, string path)
        {
            return new Item
            {
                Id = entry.Id,
                LanguageId = entry.LanguageId,
                SourceFile = path,
                SourceOffset = entry.Offset,
                SourceLength = entry.Length,
            };
        }

        /// <summary>The language id the package uses for audio that isn't voiced.</summary>
        public uint SfxLanguage
        {
            get
            {
                foreach (KeyValuePair<uint, string> language in Languages)
                    if (string.Equals(language.Value, "sfx", StringComparison.OrdinalIgnoreCase))
                        return language.Key;

                throw new InvalidDataException("The package has no \"sfx\" language to put sound effects under.");
            }
        }

        public bool IsEmpty
        {
            get { return Banks.Count == 0 && Streams.Count == 0; }
        }

        /// <summary>Every byte the package would take up once written.</summary>
        public long Size
        {
            get { return HeaderSize() + Banks.Sum(o => (long)o.Length) + Streams.Sum(o => (long)o.Length); }
        }

        /// <summary>Put a file in one of the tables, in place of the one with the same id and language if there is one.</summary>
        public Item Set(List<Item> table, uint id, uint languageId, byte[] data)
        {
            Item item = table.FirstOrDefault(o => o.Id == id && o.LanguageId == languageId);
            if (item == null)
            {
                item = new Item { Id = id, LanguageId = languageId };
                table.Add(item);
            }

            item.Data = data;
            return item;
        }

        private long HeaderSize()
        {
            return 8 + 4 + 16 + _languageMap.Length + (4 + 20L * Banks.Count) + (4 + 20L * Streams.Count) + 4;
        }

        /// <summary>
        /// Write the package to <paramref name="path"/>, which may be the file it was read from: everything goes to a
        /// file beside it first, which then takes its place, so a write that fails part way leaves the old package as
        /// it was. Afterwards every item is read from where it now is in the new file.
        /// </summary>
        public void Save(string path)
        {
            Sort(Banks);
            Sort(Streams);

            long size = Size;
            if (size > uint.MaxValue)
                throw new NotSupportedException("A sound package can't be larger than 4 GB.");

            string temporary = path + ".writing";
            List<long> offsets = new List<long>();

            using (FileStream output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            using (BinaryWriter writer = new BinaryWriter(output))
            {
                long header = HeaderSize();
                writer.Write(Encoding.ASCII.GetBytes("AKPK"));
                writer.Write((uint)(header - 8));
                writer.Write(1u);
                writer.Write((uint)_languageMap.Length);
                writer.Write((uint)(4 + 20 * Banks.Count));
                writer.Write((uint)(4 + 20 * Streams.Count));
                writer.Write(4u);
                writer.Write(_languageMap);

                //Files follow the header in table order, banks first, with nothing between them
                long cursor = header;
                foreach (List<Item> table in new[] { Banks, Streams })
                {
                    writer.Write((uint)table.Count);
                    foreach (Item item in table)
                    {
                        writer.Write(item.Id);
                        writer.Write(1u); //block size
                        writer.Write((uint)item.Length);
                        writer.Write((uint)cursor); //start block - with blocks of one byte, the offset itself
                        writer.Write(item.LanguageId);

                        offsets.Add(cursor);
                        cursor += item.Length;
                    }
                }
                writer.Write(0u); //externals

                //The files, copied a source at a time so a package hundreds of megabytes long is never all in memory
                Dictionary<string, FileStream> sources = new Dictionary<string, FileStream>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    byte[] buffer = new byte[1 << 20];
                    foreach (Item item in Banks.Concat(Streams))
                    {
                        if (item.Data != null)
                        {
                            writer.Write(item.Data);
                            continue;
                        }

                        FileStream source;
                        if (!sources.TryGetValue(item.SourceFile, out source))
                        {
                            source = new FileStream(item.SourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                            sources.Add(item.SourceFile, source);
                        }

                        source.Position = item.SourceOffset;
                        int remaining = item.SourceLength;
                        while (remaining > 0)
                        {
                            int read = source.Read(buffer, 0, Math.Min(buffer.Length, remaining));
                            if (read <= 0)
                                throw new EndOfStreamException(Path.GetFileName(item.SourceFile) + " ends before file " + item.Id + " does.");

                            output.Write(buffer, 0, read);
                            remaining -= read;
                        }
                    }
                }
                finally
                {
                    foreach (FileStream source in sources.Values)
                        source.Dispose();
                }
            }

            try
            {
                if (File.Exists(path))
                    File.Replace(temporary, path, null, true);
                else
                    File.Move(temporary, path);
            }
            catch
            {
                try { File.Delete(temporary); } catch { }
                throw;
            }

            int index = 0;
            foreach (Item item in Banks.Concat(Streams))
            {
                item.SourceFile = path;
                item.SourceOffset = offsets[index++];
                item.SourceLength = item.Length;
                item.Data = null;
            }
        }

        private static void Sort(List<Item> table)
        {
            table.Sort((a, b) => a.Id != b.Id ? a.Id.CompareTo(b.Id) : a.LanguageId.CompareTo(b.LanguageId));
        }

        private static byte[] ReadRange(Stream stream, long offset, int length)
        {
            byte[] bytes = new byte[length];
            stream.Position = offset;

            int read = 0;
            while (read < length)
            {
                int got = stream.Read(bytes, read, length - read);
                if (got <= 0)
                    throw new EndOfStreamException();
                read += got;
            }

            return bytes;
        }
    }
}

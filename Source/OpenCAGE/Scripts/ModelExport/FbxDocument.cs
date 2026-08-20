using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace OpenCAGE.ModelExport
{
    /// <summary>
    /// A tree of FBX node records, and the writer that puts it on disk in the binary format.
    ///
    /// FBX is a tree of records - a name, a list of typed properties, and a list of children. That
    /// shape is the whole format; everything else is a convention about which names to use and what
    /// to put in them, which <see cref="FbxExporter"/> deals with.
    ///
    /// This is our own writer rather than assimp's because assimp's FBX exporter resamples animation
    /// down to a handful of keys. Writing the container ourselves is the only way to get a clip out
    /// with the frames it went in with.
    /// </summary>
    public class FbxNode
    {
        public readonly string Name;
        public readonly List<object> Properties = new List<object>();
        public readonly List<FbxNode> Children = new List<FbxNode>();

        /// <summary>
        /// Write the braces even with nothing inside them. Readers tell "no block" from "empty
        /// block" by whether the record's bytes run past its properties, and some records - an
        /// animation layer with default settings, say - are required to have a block regardless.
        /// </summary>
        public bool ForceScope;

        public FbxNode(string name, params object[] properties)
        {
            Name = name ?? "";
            if (properties != null) Properties.AddRange(properties);
        }

        public FbxNode Add(FbxNode child)
        {
            Children.Add(child);
            return child;
        }

        public FbxNode Add(string name, params object[] properties)
        {
            return Add(new FbxNode(name, properties));
        }

        /// <summary>
        /// One entry in a Properties70 block. FBX describes every property with a type name, a
        /// "label" type, a flags string and then the values.
        /// </summary>
        public FbxNode Property(string name, string type, string subType, string flags, params object[] values)
        {
            FbxNode property = new FbxNode("P", name, type, subType, flags);
            if (values != null) property.Properties.AddRange(values);
            return Add(property);
        }
    }

    /// <summary>Writes an <see cref="FbxNode"/> tree as binary FBX 7.4.</summary>
    public static class FbxBinary
    {
        private const int Version = 7400;

        /* The 7.4 record header is three 32 bit fields and a length prefixed name. 7.5 widens the
         * offsets to 64 bit; we stay on 7.4, which every tool in use reads. */
        private const int NullRecordLength = 13;

        /// <summary>Arrays above this many entries are worth deflating; below it the header costs more than it saves.</summary>
        private const int CompressAbove = 64;

        public static void Write(string path, IEnumerable<FbxNode> roots)
        {
            using (FileStream file = File.Create(path))
            using (BinaryWriter writer = new BinaryWriter(file, Encoding.ASCII, true))
            {
                //"Kaydara FBX Binary  " with two trailing spaces, then a nul, then 0x1A 0x00
                writer.Write(Encoding.ASCII.GetBytes("Kaydara FBX Binary  "));
                writer.Write((byte)0);
                writer.Write((byte)0x1A);
                writer.Write((byte)0);
                writer.Write((uint)Version);

                foreach (FbxNode node in roots) WriteNode(writer, node);
                writer.Write(new byte[NullRecordLength]);

                /* Everything past the records is optional padding and a footer. Nothing reads the
                 * footer's contents, but several tools expect the file not to end on the last
                 * record, so write the conventional shape. */
                WriteFooter(writer);
            }
        }

        private static void WriteNode(BinaryWriter writer, FbxNode node)
        {
            long header = writer.BaseStream.Position;
            writer.Write((uint)0);   //EndOffset, patched once we know it
            writer.Write((uint)node.Properties.Count);
            writer.Write((uint)0);   //PropertyListLen, patched below
            byte[] name = Encoding.UTF8.GetBytes(node.Name);
            writer.Write((byte)name.Length);
            writer.Write(name);

            long propertiesStart = writer.BaseStream.Position;
            foreach (object property in node.Properties) WriteProperty(writer, property);
            long propertiesEnd = writer.BaseStream.Position;

            /* A record with children ends with a null record; one without must not have it, or
             * readers that trust EndOffset walk into the next record. */
            if (node.Children.Count != 0 || node.ForceScope)
            {
                foreach (FbxNode child in node.Children) WriteNode(writer, child);
                writer.Write(new byte[NullRecordLength]);
            }

            long end = writer.BaseStream.Position;
            writer.BaseStream.Position = header;
            writer.Write((uint)end);
            writer.BaseStream.Position = header + 8;
            writer.Write((uint)(propertiesEnd - propertiesStart));
            writer.BaseStream.Position = end;
        }

        private static void WriteProperty(BinaryWriter writer, object value)
        {
            if (value is int i) { writer.Write((byte)'I'); writer.Write(i); }
            else if (value is long l) { writer.Write((byte)'L'); writer.Write(l); }
            else if (value is double d) { writer.Write((byte)'D'); writer.Write(d); }
            else if (value is float f) { writer.Write((byte)'F'); writer.Write(f); }
            else if (value is short s) { writer.Write((byte)'Y'); writer.Write(s); }
            else if (value is bool b) { writer.Write((byte)'C'); writer.Write((byte)(b ? 1 : 0)); }
            else if (value is string text)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                writer.Write((byte)'S');
                writer.Write((uint)bytes.Length);
                writer.Write(bytes);
            }
            else if (value is byte[] raw)
            {
                writer.Write((byte)'R');
                writer.Write((uint)raw.Length);
                writer.Write(raw);
            }
            else if (value is double[] doubles) WriteArray(writer, 'd', doubles.Length, 8, w => { foreach (double x in doubles) w.Write(x); });
            else if (value is float[] floats) WriteArray(writer, 'f', floats.Length, 4, w => { foreach (float x in floats) w.Write(x); });
            else if (value is int[] ints) WriteArray(writer, 'i', ints.Length, 4, w => { foreach (int x in ints) w.Write(x); });
            else if (value is long[] longs) WriteArray(writer, 'l', longs.Length, 8, w => { foreach (long x in longs) w.Write(x); });
            else if (value is bool[] bools) WriteArray(writer, 'b', bools.Length, 1, w => { foreach (bool x in bools) w.Write((byte)(x ? 1 : 0)); });
            else throw new NotSupportedException("Nothing knows how to write an FBX property of type " + (value?.GetType().Name ?? "null"));
        }

        private static void WriteArray(BinaryWriter writer, char code, int count, int stride, Action<BinaryWriter> write)
        {
            byte[] payload;
            using (MemoryStream buffer = new MemoryStream(count * stride))
            using (BinaryWriter into = new BinaryWriter(buffer))
            {
                write(into);
                into.Flush();
                payload = buffer.ToArray();
            }

            bool compress = count > CompressAbove;
            byte[] stored = compress ? Deflate(payload) : payload;

            //a compressed array that came out bigger is not worth having
            if (compress && stored.Length >= payload.Length) { compress = false; stored = payload; }

            writer.Write((byte)code);
            writer.Write((uint)count);
            writer.Write((uint)(compress ? 1 : 0));
            writer.Write((uint)stored.Length);
            writer.Write(stored);
        }

        /* FBX stores compressed arrays as zlib, which is a two byte header, a raw deflate stream and
         * an Adler-32 of the original. .NET gives us the middle part only, so bookend it by hand. */
        private static byte[] Deflate(byte[] data)
        {
            using (MemoryStream output = new MemoryStream())
            {
                output.WriteByte(0x78);
                output.WriteByte(0x9C);
                using (DeflateStream deflate = new DeflateStream(output, CompressionMode.Compress, true))
                    deflate.Write(data, 0, data.Length);

                uint adler = Adler32(data);
                output.WriteByte((byte)(adler >> 24));
                output.WriteByte((byte)(adler >> 16));
                output.WriteByte((byte)(adler >> 8));
                output.WriteByte((byte)adler);
                return output.ToArray();
            }
        }

        private static uint Adler32(byte[] data)
        {
            uint a = 1, b = 0;
            foreach (byte value in data)
            {
                a = (a + value) % 65521;
                b = (b + a) % 65521;
            }
            return (b << 16) | a;
        }

        /* The footer is fixed content followed by a version and a run of zeroes, with the whole file
         * padded to a 16 byte boundary before the final block. Autodesk's own reader ignores it,
         * but writing it keeps the file recognisable to tools that check. */
        private static void WriteFooter(BinaryWriter writer)
        {
            writer.Write(new byte[16]);
            writer.Write(new byte[4]);

            long pad = 16 - (writer.BaseStream.Position % 16);
            writer.Write(new byte[pad == 16 ? 16 : pad]);

            writer.Write((uint)Version);
            writer.Write(new byte[120]);
            writer.Write(new byte[]
            {
                0xF8, 0x5A, 0x8C, 0x6A, 0xDE, 0xF5, 0xD9, 0x7E,
                0xEC, 0xE9, 0x0C, 0xE3, 0x75, 0x8F, 0x29, 0x0B
            });
        }
    }
}

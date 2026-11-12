using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace OpenCAGE.Hud
{
    /// <summary>
    /// A Scaleform movie (.gfx) or the Flash movie it is made from (.swf), as a header and a list of tags. Reads the plain and the
    /// zlib-compressed forms (GFX / CFX, FWS / CWS); writes the plain form. Tag bodies are kept as they are, so a movie read and
    /// written again is the same movie.
    /// </summary>
    public sealed class SwfFile
    {
        public const int TagEnd = 0, TagShowFrame = 1, TagSetBackgroundColor = 9, TagFileAttributes = 69, TagMetadata = 77,
            TagDoAbc = 82, TagSymbolClass = 76, TagDoAbcOld = 72, TagExporterInfo = 1000, TagDefineBitsLossless2 = 36;

        /// <summary>"GFX", "CFX", "FWS" or "CWS" as read.</summary>
        public string Signature = "GFX";
        public byte Version = 10;
        /// <summary>The frame size rectangle, frame rate and frame count, as stored.</summary>
        public byte[] FrameHeader;
        public List<SwfTag> Tags = new List<SwfTag>();

        /// <summary>The length the file's header gives (of the uncompressed movie) and the length it really has.</summary>
        public int DeclaredLength, ActualLength;
        /// <summary>Bytes after the End tag.</summary>
        public int TrailingBytes;
        /// <summary>The tag list ended without an End tag (cut short).</summary>
        public bool Truncated;

        public bool Compressed => Signature == "CFX" || Signature == "CWS";
        public bool IsScaleform => Signature == "GFX" || Signature == "CFX";

        /// <summary>Whether bytes start as a movie this reads.</summary>
        public static bool LooksLikeMovie(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 8) return false;
            string magic = Encoding.ASCII.GetString(bytes, 0, 3);
            return magic == "GFX" || magic == "CFX" || magic == "FWS" || magic == "CWS";
        }

        /// <summary>Read a movie; throws <see cref="InvalidDataException"/> on one that can't be read.</summary>
        public static SwfFile Read(byte[] file)
        {
            if (!LooksLikeMovie(file))
                throw new InvalidDataException("not a Scaleform or Flash movie (it doesn't start GFX, CFX, FWS or CWS)");
            SwfFile swf = new SwfFile() { Signature = Encoding.ASCII.GetString(file, 0, 3), Version = file[3] };
            swf.DeclaredLength = BitConverter.ToInt32(file, 4);
            byte[] b = file;
            if (swf.Compressed)
            {
                if (file.Length < 10)
                    throw new InvalidDataException("compressed movie cut short");
                try
                {
                    using (MemoryStream source = new MemoryStream(file, 10, file.Length - 10))
                    using (DeflateStream inflate = new DeflateStream(source, CompressionMode.Decompress))
                    using (MemoryStream output = new MemoryStream())
                    {
                        output.Write(file, 0, 8);
                        inflate.CopyTo(output);
                        b = output.ToArray();
                    }
                }
                catch (Exception e)
                {
                    throw new InvalidDataException("the compressed movie can't be unpacked: " + e.Message);
                }
            }
            swf.ActualLength = b.Length;
            if (b.Length < 9)
                throw new InvalidDataException("movie cut short in its header");
            int p = 8;
            int nbits = b[p] >> 3;
            int rectBytes = (5 + nbits * 4 + 7) / 8;
            if (p + rectBytes + 4 > b.Length)
                throw new InvalidDataException("movie cut short in its header");
            swf.FrameHeader = new byte[rectBytes + 4];
            Array.Copy(b, p, swf.FrameHeader, 0, rectBytes + 4);
            p += rectBytes + 4;
            swf.Truncated = true;
            while (p + 2 <= b.Length)
            {
                int header = b[p] | (b[p + 1] << 8);
                p += 2;
                int type = header >> 6, length = header & 0x3F;
                bool longForm = false;
                if (length == 0x3F)
                {
                    if (p + 4 > b.Length)
                        throw new InvalidDataException("tag " + type + " cut short");
                    length = BitConverter.ToInt32(b, p);
                    p += 4;
                    longForm = true;
                }
                if (length < 0 || p + length > b.Length)
                    throw new InvalidDataException("tag " + type + " runs past the end of the movie");
                byte[] body = new byte[length];
                Array.Copy(b, p, body, 0, length);
                swf.Tags.Add(new SwfTag() { Type = type, Body = body, LongForm = longForm });
                p += length;
                if (type == TagEnd)
                {
                    swf.Truncated = false;
                    break;
                }
            }
            swf.TrailingBytes = b.Length - p;
            return swf;
        }

        /// <summary>The movie as a plain (uncompressed) file; the header's length is set.</summary>
        public byte[] Write()
        {
            using (MemoryStream o = new MemoryStream())
            {
                string signature = Signature == "CFX" ? "GFX" : Signature == "CWS" ? "FWS" : Signature;
                o.Write(Encoding.ASCII.GetBytes(signature), 0, 3);
                o.WriteByte(Version);
                o.Write(new byte[4], 0, 4);
                o.Write(FrameHeader, 0, FrameHeader.Length);
                foreach (SwfTag tag in Tags)
                {
                    bool longForm = tag.LongForm || tag.Body.Length >= 0x3F;
                    int header = (tag.Type << 6) | (longForm ? 0x3F : tag.Body.Length);
                    o.WriteByte((byte)header);
                    o.WriteByte((byte)(header >> 8));
                    if (longForm)
                        o.Write(BitConverter.GetBytes(tag.Body.Length), 0, 4);
                    o.Write(tag.Body, 0, tag.Body.Length);
                }
                byte[] result = o.ToArray();
                BitConverter.GetBytes(result.Length).CopyTo(result, 4);
                return result;
            }
        }

        /// <summary>A frame header: a stage of this many pixels, a frame rate and one frame.</summary>
        public static byte[] MakeFrameHeader(int widthPixels, int heightPixels, int frameRate, int frameCount = 1)
        {
            int xmax = widthPixels * 20, ymax = heightPixels * 20;
            int nbits = Math.Max(BitsFor(xmax), BitsFor(ymax));
            BitWriter bits = new BitWriter();
            bits.Write(nbits, 5);
            bits.Write(0, nbits);
            bits.Write(xmax, nbits);
            bits.Write(0, nbits);
            bits.Write(ymax, nbits);
            List<byte> header = new List<byte>(bits.ToArray());
            header.Add(0);
            header.Add((byte)frameRate);
            header.Add((byte)frameCount);
            header.Add((byte)(frameCount >> 8));
            return header.ToArray();
        }

        /// <summary>The stage size in pixels from the frame header.</summary>
        public void StageSize(out double width, out double height)
        {
            BitReader bits = new BitReader(FrameHeader);
            int nbits = bits.Read(5);
            int xmin = bits.ReadSigned(nbits), xmax = bits.ReadSigned(nbits), ymin = bits.ReadSigned(nbits), ymax = bits.ReadSigned(nbits);
            width = (xmax - xmin) / 20.0;
            height = (ymax - ymin) / 20.0;
        }

        private static int BitsFor(int value)
        {
            //Signed fields: the bits of the value and one for the sign
            int n = 0;
            while (n < 30 && (value >> n) != 0) n++;
            return n + 1;
        }

        /// <summary>A DoABC tag's body split into its flags, name and the ABC bytes.</summary>
        public static void SplitDoAbc(byte[] body, out uint flags, out string name, out byte[] abc)
        {
            if (body.Length < 5)
                throw new InvalidDataException("DoABC tag cut short");
            flags = BitConverter.ToUInt32(body, 0);
            int p = 4;
            while (p < body.Length && body[p] != 0) p++;
            if (p >= body.Length)
                throw new InvalidDataException("DoABC tag's name has no end");
            name = Encoding.UTF8.GetString(body, 4, p - 4);
            p++;
            abc = new byte[body.Length - p];
            Array.Copy(body, p, abc, 0, abc.Length);
        }

        public static byte[] JoinDoAbc(uint flags, string name, byte[] abc)
        {
            List<byte> body = new List<byte>(BitConverter.GetBytes(flags));
            body.AddRange(Encoding.UTF8.GetBytes(name ?? ""));
            body.Add(0);
            body.AddRange(abc);
            return body.ToArray();
        }

        /// <summary>A SymbolClass tag's (character id, class name) pairs.</summary>
        public static List<KeyValuePair<int, string>> ReadSymbolClass(byte[] body)
        {
            List<KeyValuePair<int, string>> symbols = new List<KeyValuePair<int, string>>();
            if (body.Length < 2)
                throw new InvalidDataException("SymbolClass tag cut short");
            int count = body[0] | (body[1] << 8), p = 2;
            for (int i = 0; i < count; i++)
            {
                if (p + 2 > body.Length)
                    throw new InvalidDataException("SymbolClass tag cut short");
                int id = body[p] | (body[p + 1] << 8);
                p += 2;
                int start = p;
                while (p < body.Length && body[p] != 0) p++;
                if (p >= body.Length)
                    throw new InvalidDataException("SymbolClass name has no end");
                symbols.Add(new KeyValuePair<int, string>(id, Encoding.UTF8.GetString(body, start, p - start)));
                p++;
            }
            return symbols;
        }

        public static byte[] MakeSymbolClass(IList<KeyValuePair<int, string>> symbols)
        {
            List<byte> body = new List<byte>() { (byte)symbols.Count, (byte)(symbols.Count >> 8) };
            foreach (KeyValuePair<int, string> symbol in symbols)
            {
                body.Add((byte)symbol.Key);
                body.Add((byte)(symbol.Key >> 8));
                body.AddRange(Encoding.UTF8.GetBytes(symbol.Value));
                body.Add(0);
            }
            return body.ToArray();
        }

        /// <summary>A zero-ended string tag body (Metadata).</summary>
        public static byte[] MakeStringTag(string text)
        {
            List<byte> body = new List<byte>(Encoding.UTF8.GetBytes(text ?? ""));
            body.Add(0);
            return body.ToArray();
        }

        public static string ReadStringTag(byte[] body)
        {
            int end = Array.IndexOf(body, (byte)0);
            return Encoding.UTF8.GetString(body, 0, end < 0 ? body.Length : end);
        }

        private sealed class BitWriter
        {
            private readonly List<byte> _bytes = new List<byte>();
            private int _bit;
            public void Write(int value, int count)
            {
                for (int i = count - 1; i >= 0; i--)
                {
                    if (_bit == 0) _bytes.Add(0);
                    if (((value >> i) & 1) != 0)
                        _bytes[_bytes.Count - 1] |= (byte)(0x80 >> _bit);
                    _bit = (_bit + 1) & 7;
                }
            }
            public byte[] ToArray() => _bytes.ToArray();
        }

        private sealed class BitReader
        {
            private readonly byte[] _bytes;
            private int _position;
            public BitReader(byte[] bytes) { _bytes = bytes; }
            public int Read(int count)
            {
                int value = 0;
                for (int i = 0; i < count; i++)
                {
                    int bit = (_bytes[_position >> 3] >> (7 - (_position & 7))) & 1;
                    value = (value << 1) | bit;
                    _position++;
                }
                return value;
            }
            public int ReadSigned(int count)
            {
                int value = Read(count);
                if (count > 0 && (value & (1 << (count - 1))) != 0)
                    value |= -1 << count;
                return value;
            }
        }
    }

    public sealed class SwfTag
    {
        public int Type;
        public byte[] Body;
        /// <summary>Stored with the long header even when short (kept on write).</summary>
        public bool LongForm;
    }
}

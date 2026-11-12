using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace OpenCAGE.Hud
{
    /// <summary>
    /// Reads a PNG into RGBA pixels (non-interlaced; greyscale, RGB, palette, with or without alpha; 8 or 16 bits, palettes 1-8),
    /// and packs pixels as a Flash lossless bitmap (DefineBitsLossless2: premultiplied ARGB, zlib). For image slots of an overlay.
    /// </summary>
    public static class HudPng
    {
        public const int MaxSide = 1024;

        /// <summary>The image's size and RGBA pixels (row by row); throws <see cref="InvalidDataException"/> for what it can't read.</summary>
        public static byte[] Decode(byte[] png, out int width, out int height)
        {
            if (png == null || png.Length < 33 || png[0] != 0x89 || png[1] != 'P' || png[2] != 'N' || png[3] != 'G')
                throw new InvalidDataException("not a PNG image");
            int p = 8, bitDepth = 0, colourType = 0, interlace = 0;
            width = height = 0;
            byte[] palette = null, alphas = null;
            MemoryStream data = new MemoryStream();
            while (p + 8 <= png.Length)
            {
                int length = (png[p] << 24) | (png[p + 1] << 16) | (png[p + 2] << 8) | png[p + 3];
                string type = Encoding.ASCII.GetString(png, p + 4, 4);
                int body = p + 8;
                if (length < 0 || body + length > png.Length) throw new InvalidDataException("PNG cut short");
                switch (type)
                {
                    case "IHDR":
                        width = (png[body] << 24) | (png[body + 1] << 16) | (png[body + 2] << 8) | png[body + 3];
                        height = (png[body + 4] << 24) | (png[body + 5] << 16) | (png[body + 6] << 8) | png[body + 7];
                        bitDepth = png[body + 8]; colourType = png[body + 9]; interlace = png[body + 12];
                        break;
                    case "PLTE": palette = new byte[length]; Array.Copy(png, body, palette, 0, length); break;
                    case "tRNS": alphas = new byte[length]; Array.Copy(png, body, alphas, 0, length); break;
                    case "IDAT": data.Write(png, body, length); break;
                }
                p = body + length + 4;
                if (type == "IEND") break;
            }
            if (width <= 0 || height <= 0 || width > MaxSide || height > MaxSide)
                throw new InvalidDataException("the image is " + width + "x" + height + "; up to " + MaxSide + "x" + MaxSide + " pixels");
            if (interlace != 0) throw new InvalidDataException("interlaced PNGs aren't read: save it without interlacing");
            int channels;
            switch (colourType)
            {
                case 0: channels = 1; break;
                case 2: channels = 3; break;
                case 3: channels = 1; if (palette == null) throw new InvalidDataException("a palette PNG with no palette"); break;
                case 4: channels = 2; break;
                case 6: channels = 4; break;
                default: throw new InvalidDataException("PNG colour type " + colourType + " isn't read");
            }
            if (!(bitDepth == 8 || bitDepth == 16 || (colourType == 3 && (bitDepth == 1 || bitDepth == 2 || bitDepth == 4)) || (colourType == 0 && bitDepth < 8 && (bitDepth == 1 || bitDepth == 2 || bitDepth == 4))))
                throw new InvalidDataException("PNG bit depth " + bitDepth + " isn't read");

            byte[] raw;
            data.Position = 2; //the zlib header
            using (DeflateStream inflate = new DeflateStream(data, CompressionMode.Decompress))
            using (MemoryStream o = new MemoryStream())
            {
                inflate.CopyTo(o);
                raw = o.ToArray();
            }
            int bitsPerPixel = channels * bitDepth;
            int stride = (width * bitsPerPixel + 7) / 8, bpp = Math.Max(1, bitsPerPixel / 8);
            if (raw.Length < (stride + 1) * height) throw new InvalidDataException("PNG image data cut short");
            byte[] lines = new byte[stride * height];
            for (int y = 0; y < height; y++)
            {
                int filter = raw[y * (stride + 1)];
                int src = y * (stride + 1) + 1, dst = y * stride;
                for (int x = 0; x < stride; x++)
                {
                    int a = x >= bpp ? lines[dst + x - bpp] : 0, b = y > 0 ? lines[dst - stride + x] : 0, c = x >= bpp && y > 0 ? lines[dst - stride + x - bpp] : 0;
                    int v = raw[src + x];
                    switch (filter)
                    {
                        case 1: v += a; break;
                        case 2: v += b; break;
                        case 3: v += (a + b) >> 1; break;
                        case 4: { int pp = a + b - c, pa = Math.Abs(pp - a), pb = Math.Abs(pp - b), pc = Math.Abs(pp - c); v += pa <= pb && pa <= pc ? a : pb <= pc ? b : c; break; }
                    }
                    lines[dst + x] = (byte)v;
                }
            }

            byte[] rgba = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int o = (y * width + x) * 4, line = y * stride;
                    byte r, g, b, al = 255;
                    if (bitDepth < 8)
                    {
                        int bit = x * bitDepth, value = (lines[line + bit / 8] >> (8 - bitDepth - bit % 8)) & ((1 << bitDepth) - 1);
                        if (colourType == 3)
                        {
                            if (value * 3 + 2 >= palette.Length) throw new InvalidDataException("a palette index past the palette");
                            r = palette[value * 3]; g = palette[value * 3 + 1]; b = palette[value * 3 + 2];
                            if (alphas != null && value < alphas.Length) al = alphas[value];
                        }
                        else { r = g = b = (byte)(value * 255 / ((1 << bitDepth) - 1)); }
                    }
                    else
                    {
                        int step = bitDepth / 8, at = line + x * channels * step;
                        Func<int, byte> ch = k => lines[at + k * step];
                        switch (colourType)
                        {
                            case 0: r = g = b = ch(0); break;
                            case 2: r = ch(0); g = ch(1); b = ch(2); break;
                            case 3:
                                {
                                    int value = ch(0);
                                    if (value * 3 + 2 >= palette.Length) throw new InvalidDataException("a palette index past the palette");
                                    r = palette[value * 3]; g = palette[value * 3 + 1]; b = palette[value * 3 + 2];
                                    if (alphas != null && value < alphas.Length) al = alphas[value];
                                    break;
                                }
                            case 4: r = g = b = ch(0); al = ch(1); break;
                            default: r = ch(0); g = ch(1); b = ch(2); al = ch(3); break;
                        }
                    }
                    rgba[o] = r; rgba[o + 1] = g; rgba[o + 2] = b; rgba[o + 3] = al;
                }
            return rgba;
        }

        /// <summary>A DefineBitsLossless2 tag body for RGBA pixels: 32-bit premultiplied ARGB, zlib-packed.</summary>
        public static byte[] LosslessTag(int characterId, int width, int height, byte[] rgba)
        {
            byte[] argb = new byte[width * height * 4];
            for (int i = 0; i < width * height; i++)
            {
                int a = rgba[i * 4 + 3];
                argb[i * 4] = (byte)a;
                argb[i * 4 + 1] = (byte)(rgba[i * 4] * a / 255);
                argb[i * 4 + 2] = (byte)(rgba[i * 4 + 1] * a / 255);
                argb[i * 4 + 3] = (byte)(rgba[i * 4 + 2] * a / 255);
            }
            using (MemoryStream o = new MemoryStream())
            {
                o.WriteByte((byte)characterId); o.WriteByte((byte)(characterId >> 8));
                o.WriteByte(5);
                o.WriteByte((byte)width); o.WriteByte((byte)(width >> 8));
                o.WriteByte((byte)height); o.WriteByte((byte)(height >> 8));
                byte[] z = Zlib(argb);
                o.Write(z, 0, z.Length);
                return o.ToArray();
            }
        }

        /// <summary>zlib: header, deflate, Adler-32.</summary>
        public static byte[] Zlib(byte[] data)
        {
            using (MemoryStream o = new MemoryStream())
            {
                o.WriteByte(0x78); o.WriteByte(0x9C);
                using (DeflateStream z = new DeflateStream(o, CompressionLevel.Optimal, true))
                    z.Write(data, 0, data.Length);
                uint s1 = 1, s2 = 0;
                foreach (byte b in data) { s1 = (s1 + b) % 65521; s2 = (s2 + s1) % 65521; }
                uint adler = (s2 << 16) | s1;
                o.WriteByte((byte)(adler >> 24)); o.WriteByte((byte)(adler >> 16)); o.WriteByte((byte)(adler >> 8)); o.WriteByte((byte)adler);
                return o.ToArray();
            }
        }
    }
}

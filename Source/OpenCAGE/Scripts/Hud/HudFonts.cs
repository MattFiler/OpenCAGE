using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;

namespace OpenCAGE.Hud
{
    /// <summary>One of the game's embedded fonts (a DefineFont3 or DefineFont2 tag): glyph outlines, character codes and layout.</summary>
    public sealed class HudFont
    {
        public int Id;
        public string Name;
        public bool Bold, Italic;
        /// <summary>Font units per em: 20480 for DefineFont3, 1024 for DefineFont2 (outlines, advances and ascent share them).</summary>
        public float Em;
        public float Ascent, Descent, Leading;

        internal readonly Dictionary<char, int> Glyphs = new Dictionary<char, int>();
        internal PointF[][] Points;
        internal byte[][] Types;
        internal float[] Advances;

        public int GlyphCount => Advances?.Length ?? 0;
        public bool Has(char c) => Glyphs.ContainsKey(c);
        public IEnumerable<char> Characters => Glyphs.Keys.OrderBy(o => o);
        public override string ToString() => Name + (Bold ? " bold" : "") + (Italic ? " italic" : "") + " (" + GlyphCount + " glyphs)";
    }

    /// <summary>
    /// The fonts the game draws HUD text with, read from its files: DATA/FONT_CONFIG.XML maps a font id ("$Isolation") to a name and
    /// style per language in a font library movie (font_lib, e.g. DATA/UI/FONTS_EN.GFX in UI.PAK, else loose), whose DefineFont3 tags
    /// hold the outlines. Read once per game folder and language.
    /// </summary>
    public static class HudFonts
    {
        public const int TagDefineFont2 = 48, TagDefineFont3 = 75;

        /// <summary>The FONT_CONFIG.XML language to take fonts for.</summary>
        public static string Language = "english";

        /// <summary>A TextField's gutter: its text starts 2 pixels in from each edge.</summary>
        public const float Gutter = 2;

        private sealed class Library
        {
            public readonly Dictionary<string, HudFont> Fonts = new Dictionary<string, HudFont>(StringComparer.Ordinal);
            public string Problem;
        }
        private static readonly Dictionary<string, Library> _libraries = new Dictionary<string, Library>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _lock = new object();

        /// <summary>The font the game draws a font id with, or null when the game's files don't give one.</summary>
        public static HudFont Get(string pathToAI, string fontId)
        {
            if (string.IsNullOrEmpty(fontId)) return null;
            Library library = LibraryFor(pathToAI);
            return library.Fonts.TryGetValue(fontId, out HudFont font) ? font : null;
        }

        /// <summary>Why no fonts could be read for a game folder (null when they were).</summary>
        public static string Problem(string pathToAI) => LibraryFor(pathToAI).Problem;

        private static Library LibraryFor(string pathToAI)
        {
            string key = (pathToAI ?? "") + "|" + Language;
            lock (_lock)
            {
                if (_libraries.TryGetValue(key, out Library known)) return known;
                Library library = new Library();
                try { Load(pathToAI, library); }
                catch (Exception e) { library.Fonts.Clear(); library.Problem = e.Message; }
                _libraries[key] = library;
                return library;
            }
        }

        /// <summary>The Windows face standing in for a font id when the game's font, or one of its characters, isn't there.</summary>
        public static void StandIn(string fontId, out string family, out bool bold)
        {
            fontId = fontId ?? "";
            bold = fontId.EndsWith("Bold", StringComparison.Ordinal) || fontId == "$Isolation" || fontId == "$NostromoBoldCond";
            family = fontId == "$NostromoBoldCond" || fontId == "$JectRexBold" ? "Bahnschrift SemiCondensed" : "Bahnschrift";
        }

        #region Laying out text
        /// <summary>
        /// Text as an overlay's TextField draws it, as one outline in the box's pixels: the size in design pixels (TextFormat.size),
        /// first baseline at the gutter plus the font's ascent, lines spaced ascent + descent, each line aligned left / center / right
        /// within the gutters (a line wider than that starts at the left), no kerning. Characters the font lacks are drawn from the
        /// stand-in face. <see cref="DrawTextField"/> clips it to the box.
        /// </summary>
        public static GraphicsPath Layout(HudFont font, string fontId, string text, double size, RectangleF box, float scale, string align, out bool standIns)
        {
            standIns = false;
            GraphicsPath path = new GraphicsPath(FillMode.Alternate);
            if (font == null || string.IsNullOrEmpty(text) || size <= 0 || scale <= 0) return path;
            float px = (float)size * scale, unit = px / font.Em, gutter = Gutter * scale;
            float ascent = (font.Ascent > 0 ? font.Ascent : font.Em * 0.8f) * unit, descent = Math.Max(0, font.Descent) * unit;
            float baseline = box.Y + gutter + ascent, room = box.Width - 2 * gutter;
            StandIn(fontId, out string family, out bool bold);

            foreach (string line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                List<float> advances = new List<float>(line.Length);
                float width = 0;
                foreach (char c in line)
                {
                    float advance = font.Glyphs.TryGetValue(c, out int glyph) ? font.Advances[glyph] * unit : StandInAdvance(family, bold, px, c);
                    advances.Add(advance);
                    width += advance;
                }
                float x = box.X + gutter;
                if (align == "center") x += Math.Max(0, (room - width) / 2);
                else if (align == "right") x += Math.Max(0, room - width);

                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];
                    if (font.Glyphs.TryGetValue(c, out int glyph))
                        AddGlyph(path, font, glyph, x, baseline, unit);
                    else if (!char.IsWhiteSpace(c))
                    {
                        AddStandIn(path, family, bold, px, c, x, baseline);
                        standIns = true;
                    }
                    x += advances[i];
                }
                baseline += ascent + descent;
            }
            //Flash fills glyphs even-odd; stand-in outlines can overlap themselves and need nonzero
            if (standIns) path.FillMode = FillMode.Winding;
            return path;
        }

        private static void AddGlyph(GraphicsPath path, HudFont font, int glyph, float x, float baseline, float unit)
        {
            PointF[] points = font.Points[glyph];
            if (points.Length == 0) return;
            PointF[] placed = new PointF[points.Length];
            for (int i = 0; i < points.Length; i++)
                placed[i] = new PointF(x + points[i].X * unit, baseline + points[i].Y * unit);
            using (GraphicsPath one = new GraphicsPath(placed, font.Types[glyph], FillMode.Alternate))
                path.AddPath(one, false);
        }

        private static readonly StringFormat _measure = MakeMeasureFormat();
        private static StringFormat MakeMeasureFormat()
        {
            StringFormat format = (StringFormat)StringFormat.GenericTypographic.Clone();
            format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;
            return format;
        }

        //Stand-in advances per pixel of size, measured once at 100 px
        private static readonly Dictionary<string, float> _standInAdvances = new Dictionary<string, float>();
        private static float StandInAdvance(string family, bool bold, float px, char c)
        {
            string key = family + (bold ? "|b|" : "|r|") + c;
            lock (_lock)
            {
                if (!_standInAdvances.TryGetValue(key, out float perPixel))
                {
                    perPixel = 0.5f;
                    try
                    {
                        using (Font face = new Font(family, 100f, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
                        using (Bitmap scratch = new Bitmap(1, 1))
                        using (Graphics g = Graphics.FromImage(scratch))
                            perPixel = g.MeasureString(c.ToString(), face, PointF.Empty, _measure).Width / 100f;
                    }
                    catch { }
                    _standInAdvances[key] = perPixel;
                }
                return perPixel * px;
            }
        }

        private static void AddStandIn(GraphicsPath path, string family, bool bold, float px, char c, float x, float baseline)
        {
            try
            {
                using (FontFamily face = new FontFamily(family))
                {
                    FontStyle style = bold && face.IsStyleAvailable(FontStyle.Bold) ? FontStyle.Bold : FontStyle.Regular;
                    float top = baseline - px * face.GetCellAscent(style) / face.GetEmHeight(style);
                    path.AddString(c.ToString(), face, (int)style, Math.Max(1f, px), new PointF(x, top), _measure);
                }
            }
            catch { }
        }
        #endregion

        #region Drawing a text field
        //The overlay's shadow, as HudMovieBuilder's ocText makes it: GlowFilter(0x000000, 0.85, 3, 3, 4)
        private const float GlowAlpha = 0.85f, GlowBlur = 3f, GlowStrength = 4f;

        /// <summary>
        /// A laid out text field drawn as the movie draws it: the text clipped to its box; with a shadow, a glow under it (the text's
        /// coverage box-blurred, times the strength, in design pixels); the alpha over the whole field.
        /// </summary>
        public static void DrawTextField(Graphics g, GraphicsPath text, RectangleF box, Color colour, bool glow, float scale, double alpha)
        {
            float opacity = (float)Math.Max(0, Math.Min(1, alpha));
            RectangleF bounds = text.GetBounds();
            bounds.Intersect(box);
            if (opacity <= 0 || bounds.Width <= 0 || bounds.Height <= 0) return;
            if (!glow)
            {
                GraphicsState state = g.Save();
                try
                {
                    g.SetClip(box, CombineMode.Intersect);
                    using (Brush brush = new SolidBrush(Color.FromArgb((int)(opacity * 255), colour)))
                        g.FillPath(brush, text);
                }
                finally
                {
                    g.Restore(state);
                }
                return;
            }

            float blur = GlowBlur * scale;
            int pad = (int)Math.Ceiling(blur) + 2;
            Rectangle area = Rectangle.FromLTRB((int)Math.Floor(bounds.Left) - pad, (int)Math.Floor(bounds.Top) - pad, (int)Math.Ceiling(bounds.Right) + pad, (int)Math.Ceiling(bounds.Bottom) + pad);
            area.Intersect(Rectangle.Ceiling(g.VisibleClipBounds));
            if (area.Width <= 0 || area.Height <= 0) return;
            using (Bitmap layer = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppArgb))
            {
                using (Graphics lg = Graphics.FromImage(layer))
                {
                    lg.SmoothingMode = SmoothingMode.AntiAlias;
                    lg.TranslateTransform(-area.X, -area.Y);
                    lg.SetClip(box);
                    lg.FillPath(Brushes.White, text);
                }
                int w = area.Width, h = area.Height;
                BitmapData data = layer.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                try
                {
                    int stride = data.Stride;
                    byte[] pixels = new byte[stride * h];
                    Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                    float[] cover = new float[w * h];
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                            cover[y * w + x] = pixels[y * stride + x * 4 + 3] / 255f;
                    float[] spread = BoxBlur(cover, w, h, blur);
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            //The text over its glow (black)
                            float t = cover[y * w + x], glowA = Math.Min(1, spread[y * w + x] * GlowStrength) * GlowAlpha, a = t + glowA * (1 - t);
                            float k = a > 0 ? t / a : 0;
                            int at = y * stride + x * 4;
                            pixels[at] = (byte)(colour.B * k + 0.5f);
                            pixels[at + 1] = (byte)(colour.G * k + 0.5f);
                            pixels[at + 2] = (byte)(colour.R * k + 0.5f);
                            pixels[at + 3] = (byte)(a * 255 + 0.5f);
                        }
                    Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
                }
                finally
                {
                    layer.UnlockBits(data);
                }
                using (ImageAttributes faded = new ImageAttributes())
                {
                    faded.SetColorMatrix(new ColorMatrix() { Matrix33 = opacity });
                    g.DrawImage(layer, area, 0, 0, w, h, GraphicsUnit.Pixel, faded);
                }
            }
        }

        /* A box blur width pixels wide (a fractional width weighs the end pixels), across then down */
        private static float[] BoxBlur(float[] values, int w, int h, float width)
        {
            width = Math.Max(0.01f, width);
            float[] across = new float[values.Length], result = new float[values.Length];
            float[] line = new float[Math.Max(w, h)], blurred = new float[Math.Max(w, h)], sums = new float[Math.Max(w, h) + 1];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++) line[x] = values[y * w + x];
                BoxBlur(line, w, width, sums, blurred);
                for (int x = 0; x < w; x++) across[y * w + x] = blurred[x];
            }
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++) line[y] = across[y * w + x];
                BoxBlur(line, h, width, sums, blurred);
                for (int y = 0; y < h; y++) result[y * w + x] = blurred[y];
            }
            return result;
        }

        private static void BoxBlur(float[] line, int n, float width, float[] sums, float[] blurred)
        {
            sums[0] = 0;
            for (int i = 0; i < n; i++) sums[i + 1] = sums[i] + line[i];
            for (int i = 0; i < n; i++)
                blurred[i] = (Integral(line, sums, n, i + 0.5f + width / 2) - Integral(line, sums, n, i + 0.5f - width / 2)) / width;
        }

        private static float Integral(float[] line, float[] sums, int n, float at)
        {
            if (at <= 0) return 0;
            if (at >= n) return sums[n];
            int whole = (int)at;
            return sums[whole] + (at - whole) * line[whole];
        }
        #endregion

        #region Reading the game's files
        private static void Load(string pathToAI, Library library)
        {
            if (string.IsNullOrEmpty(pathToAI) || !Directory.Exists(Path.Combine(pathToAI, "DATA")))
            {
                library.Problem = "no game folder";
                return;
            }

            //FONT_CONFIG.XML: <language name font_lib><font id name style/></language>
            string fontLib = "fonts_en.gfx";
            Dictionary<string, KeyValuePair<string, string>> mapped = new Dictionary<string, KeyValuePair<string, string>>(StringComparer.Ordinal);
            string config = Path.Combine(pathToAI, "DATA", "FONT_CONFIG.XML");
            if (File.Exists(config))
            {
                XmlDocument xml = new XmlDocument();
                xml.Load(config);
                List<XmlElement> languages = xml.DocumentElement?.ChildNodes.OfType<XmlElement>().Where(o => o.Name == "language").ToList() ?? new List<XmlElement>();
                XmlElement language = languages.FirstOrDefault(o => string.Equals(o.GetAttribute("name"), Language, StringComparison.OrdinalIgnoreCase))
                    ?? languages.FirstOrDefault(o => string.Equals(o.GetAttribute("name"), "english", StringComparison.OrdinalIgnoreCase))
                    ?? languages.FirstOrDefault();
                if (language != null)
                {
                    if (!string.IsNullOrWhiteSpace(language.GetAttribute("font_lib"))) fontLib = language.GetAttribute("font_lib");
                    foreach (XmlElement font in language.ChildNodes.OfType<XmlElement>().Where(o => o.Name == "font"))
                        mapped[font.GetAttribute("id")] = new KeyValuePair<string, string>(font.GetAttribute("name"), font.GetAttribute("style"));
                }
            }

            //The movie as the game opens it: UI.PAK first, then a loose file
            string entry = "DATA/UI/" + fontLib.Replace('\\', '/').TrimStart('/');
            byte[] movie = ReadPakEntry(Path.Combine(pathToAI, "DATA", "UI.PAK"), entry);
            string loose = Path.Combine(pathToAI, entry.Replace('/', Path.DirectorySeparatorChar));
            if (movie == null && File.Exists(loose)) movie = File.ReadAllBytes(loose);
            if (movie == null)
            {
                library.Problem = entry + " is in neither UI.PAK nor the game's folder";
                return;
            }

            SwfFile swf = SwfFile.Read(movie);
            List<HudFont> fonts = new List<HudFont>();
            Dictionary<string, int> exported = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (SwfTag tag in swf.Tags)
            {
                if (tag.Type == TagDefineFont3 || tag.Type == TagDefineFont2)
                {
                    HudFont font = ReadFont(tag.Body, tag.Type == TagDefineFont3);
                    if (font != null) fonts.Add(font);
                }
                else if (tag.Type == SwfFile.TagSymbolClass)
                    foreach (KeyValuePair<int, string> symbol in SwfFile.ReadSymbolClass(tag.Body))
                        exported[symbol.Value] = symbol.Key;
            }

            //Each id by the name and style the language maps it to; else the font the library exports as the id (its class name)
            foreach (string id in HudDesign.Fonts.Concat(mapped.Keys).Distinct())
            {
                HudFont found = null;
                if (mapped.TryGetValue(id, out KeyValuePair<string, string> to))
                {
                    string style = (to.Value ?? "").ToLowerInvariant();
                    bool bold = style.Contains("bold"), italic = style.Contains("italic");
                    List<HudFont> named = fonts.Where(o => string.Equals(o.Name, to.Key, StringComparison.OrdinalIgnoreCase)).ToList();
                    found = named.FirstOrDefault(o => o.Bold == bold && o.Italic == italic) ?? named.FirstOrDefault(o => o.Bold == bold) ?? named.FirstOrDefault();
                }
                if (found == null && exported.TryGetValue(id, out int exportId))
                    found = fonts.FirstOrDefault(o => o.Id == exportId);
                if (found == null && mapped.TryGetValue(id, out to) && !string.IsNullOrEmpty(to.Key))
                {
                    bool bold = (to.Value ?? "").ToLowerInvariant().Contains("bold");
                    List<HudFont> near = fonts.Where(o => o.Name.StartsWith(to.Key, StringComparison.OrdinalIgnoreCase)).ToList();
                    found = near.FirstOrDefault(o => o.Bold == bold) ?? near.FirstOrDefault();
                }
                if (found != null)
                    library.Fonts[id] = found;
            }
            if (library.Fonts.Count == 0)
                library.Problem = entry + " has none of the fonts FONT_CONFIG.XML names";
        }

        /* PAK2: "PAK2", the end-offset table's position - 16, the entry count, the alignment; the entries' zero-ended names; the table of
           each entry's end; each entry starts where the one before ends (the first where the table ends), rounded up to the alignment */
        private static byte[] ReadPakEntry(string pak, string name)
        {
            if (!File.Exists(pak)) return null;
            using (FileStream stream = new FileStream(pak, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                if (stream.Length < 16 || Encoding.ASCII.GetString(reader.ReadBytes(4)) != "PAK2") return null;
                long table = reader.ReadInt32() + 16L;
                int count = reader.ReadInt32(), alignment = Math.Max(1, reader.ReadInt32());
                if (count < 0 || table < 16 || table + count * 4L > stream.Length) return null;
                int index = -1;
                List<byte> chars = new List<byte>(64);
                for (int i = 0; i < count && index < 0; i++)
                {
                    chars.Clear();
                    for (int b = reader.ReadByte(); b != 0; b = reader.ReadByte()) chars.Add((byte)b);
                    if (string.Equals(Encoding.ASCII.GetString(chars.ToArray()).Replace('\\', '/'), name, StringComparison.OrdinalIgnoreCase)) index = i;
                }
                if (index < 0) return null;
                long start = table + count * 4L;
                if (index != 0)
                {
                    stream.Position = table + (index - 1) * 4L;
                    start = reader.ReadInt32();
                }
                stream.Position = table + index * 4L;
                long end = reader.ReadInt32();
                start = (start + alignment - 1) / alignment * alignment;
                if (start > end || end > stream.Length) return null;
                stream.Position = start;
                return reader.ReadBytes((int)(end - start));
            }
        }

        /* DefineFont2 / DefineFont3: id, flags (layout, ShiftJIS, small, ANSI, wide offsets, wide codes, italic, bold), language, name,
           glyph count, the offset table (from its own start) and the code table's offset, the glyph SHAPEs, the code table, then the
           layout: ascent, descent, leading, an advance and a bounds RECT per glyph, the kerning pairs */
        private static HudFont ReadFont(byte[] b, bool font3)
        {
            if (b.Length < 7) return null;
            int flags = b[2];
            bool layout = (flags & 0x80) != 0, wideOffsets = (flags & 0x08) != 0, wideCodes = (flags & 0x04) != 0;
            int nameLength = b[4];
            if (5 + nameLength + 2 > b.Length) return null;
            HudFont font = new HudFont()
            {
                Id = b[0] | (b[1] << 8),
                Name = Encoding.UTF8.GetString(b, 5, nameLength).TrimEnd('\0').Trim(),
                Bold = (flags & 0x01) != 0,
                Italic = (flags & 0x02) != 0,
                Em = font3 ? 20480 : 1024,
            };
            int p = 5 + nameLength;
            int count = b[p] | (b[p + 1] << 8);
            p += 2;
            if (count == 0) return null;
            int tableStart = p, offsetSize = wideOffsets ? 4 : 2;
            if (tableStart + (count + 1) * offsetSize > b.Length) return null;
            int[] offsets = new int[count + 1];
            for (int i = 0; i <= count; i++)
                offsets[i] = wideOffsets ? BitConverter.ToInt32(b, tableStart + i * 4) : b[tableStart + i * 2] | (b[tableStart + i * 2 + 1] << 8);

            font.Points = new PointF[count][];
            font.Types = new byte[count][];
            font.Advances = new float[count];
            for (int i = 0; i < count; i++)
            {
                int start = tableStart + offsets[i], end = tableStart + offsets[i + 1];
                ReadGlyph(b, start, Math.Min(end, b.Length), out font.Points[i], out font.Types[i]);
            }

            int codes = tableStart + offsets[count];
            for (int i = 0; i < count; i++)
            {
                int at = codes + i * (wideCodes ? 2 : 1);
                if (at + (wideCodes ? 1 : 0) >= b.Length) break;
                char c = (char)(wideCodes ? b[at] | (b[at + 1] << 8) : b[at]);
                if (!font.Glyphs.ContainsKey(c)) font.Glyphs[c] = i;
            }

            p = codes + count * (wideCodes ? 2 : 1);
            if (layout && p + 6 + count * 2 <= b.Length)
            {
                font.Ascent = b[p] | (b[p + 1] << 8);
                font.Descent = b[p + 2] | (b[p + 3] << 8);
                font.Leading = (short)(b[p + 4] | (b[p + 5] << 8));
                p += 6;
                for (int i = 0; i < count; i++)
                    font.Advances[i] = (short)(b[p + i * 2] | (b[p + i * 2 + 1] << 8));
            }
            else
            {
                //No layout: each glyph's width as its advance
                for (int i = 0; i < count; i++)
                    font.Advances[i] = font.Points[i].Length == 0 ? font.Em / 4 : font.Points[i].Max(o => o.X) + font.Em / 20;
                font.Ascent = font.Em * 0.8f;
                font.Descent = font.Em * 0.2f;
            }
            return font;
        }

        /* A glyph SHAPE: fill and line bit counts, then records - a style change (move-to, fill 0 / 1, line style) or an edge (straight,
           or a quadratic curve: control and anchor deltas) - to an all-zero style change. Curves become cubic Beziers for GDI+. */
        private static void ReadGlyph(byte[] b, int start, int end, out PointF[] points, out byte[] types)
        {
            List<PointF> pts = new List<PointF>();
            List<byte> kinds = new List<byte>();
            try
            {
                Bits bits = new Bits(b, start, end);
                int fillBits = bits.Read(4), lineBits = bits.Read(4);
                int x = 0, y = 0;
                bool open = false;
                while (true)
                {
                    if (bits.Read(1) == 0)
                    {
                        int state = bits.Read(5);
                        if (state == 0) break;
                        if ((state & 0x10) != 0) break; //new styles: never in a glyph
                        if ((state & 0x01) != 0)
                        {
                            int n = bits.Read(5);
                            x = bits.ReadSigned(n);
                            y = bits.ReadSigned(n);
                            Close(kinds);
                            open = false;
                        }
                        if ((state & 0x02) != 0) bits.Read(fillBits);
                        if ((state & 0x04) != 0) bits.Read(fillBits);
                        if ((state & 0x08) != 0) bits.Read(lineBits);
                        continue;
                    }
                    if (!open)
                    {
                        pts.Add(new PointF(x, y));
                        kinds.Add((byte)PathPointType.Start);
                        open = true;
                    }
                    bool straight = bits.Read(1) == 1;
                    int count = bits.Read(4) + 2;
                    if (straight)
                    {
                        int dx = 0, dy = 0;
                        if (bits.Read(1) == 1) { dx = bits.ReadSigned(count); dy = bits.ReadSigned(count); }
                        else if (bits.Read(1) == 1) dy = bits.ReadSigned(count);
                        else dx = bits.ReadSigned(count);
                        x += dx; y += dy;
                        pts.Add(new PointF(x, y));
                        kinds.Add((byte)PathPointType.Line);
                    }
                    else
                    {
                        int cx = x + bits.ReadSigned(count), cy = y + bits.ReadSigned(count);
                        int ax = cx + bits.ReadSigned(count), ay = cy + bits.ReadSigned(count);
                        pts.Add(new PointF(x + (cx - x) * 2f / 3f, y + (cy - y) * 2f / 3f));
                        pts.Add(new PointF(ax + (cx - ax) * 2f / 3f, ay + (cy - ay) * 2f / 3f));
                        pts.Add(new PointF(ax, ay));
                        kinds.Add((byte)PathPointType.Bezier3);
                        kinds.Add((byte)PathPointType.Bezier3);
                        kinds.Add((byte)PathPointType.Bezier3);
                        x = ax; y = ay;
                    }
                }
            }
            catch (IndexOutOfRangeException) { }
            Close(kinds);
            points = pts.ToArray();
            types = kinds.ToArray();
        }

        private static void Close(List<byte> kinds)
        {
            if (kinds.Count != 0 && (kinds[kinds.Count - 1] & (byte)PathPointType.PathTypeMask) != (byte)PathPointType.Start)
                kinds[kinds.Count - 1] |= (byte)PathPointType.CloseSubpath;
        }

        private sealed class Bits
        {
            private readonly byte[] _bytes;
            private readonly int _end;
            private int _position;
            public Bits(byte[] bytes, int start, int end) { _bytes = bytes; _position = start * 8; _end = end * 8; }
            public int Read(int count)
            {
                int value = 0;
                for (int i = 0; i < count; i++)
                {
                    if (_position >= _end) throw new IndexOutOfRangeException();
                    value = (value << 1) | ((_bytes[_position >> 3] >> (7 - (_position & 7))) & 1);
                    _position++;
                }
                return value;
            }
            public int ReadSigned(int count)
            {
                int value = Read(count);
                if (count > 0 && count < 32 && (value & (1 << (count - 1))) != 0)
                    value |= -1 << count;
                return value;
            }
        }
        #endregion
    }
}

using OpenCAGE.Hud;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenCAGE.MapAssets
{
    /// <summary>A label drawn on a floor (world x, z).</summary>
    public class MapLabel
    {
        public string Text;
        public float X, Z;
        /// <summary>The floor it is drawn on (a floor's name or keyframe), when it isn't the floor walkable there.</summary>
        public string Floor;
        public MapLabel(string text, float x, float z, string floor = null) { Text = text; X = x; Z = z; Floor = floor; }
    }

    /// <summary>
    /// A floor movie for the Tab map, made from one of the game's own floor movies (its class, start marker and font import kept, its
    /// drawing replaced): one frame labelled with the floor's map keyframe, the traced outlines drawn as the game's maps are (teal
    /// tint, hairline outline) and the labels, every piece named decoration_* so the map page shows it from the start.
    /// </summary>
    public static class MapMovie
    {
        public const int TagEnd = 0, TagShowFrame = 1, TagDefineShape = 2, TagPlaceObject2 = 26, TagDefineShape2 = 22, TagDefineShape3 = 32,
            TagDefineShape4 = 83, TagDefineSprite = 39, TagDefineEditText = 37, TagFrameLabel = 43, TagCsmTextSettings = 74, TagPlaceObject3 = 70,
            TagSceneAndFrameLabels = 86, TagImportAssets2 = 71;

        /// <summary>The game's map colour.</summary>
        public static readonly Color Tint = Color.FromArgb(131, 247, 251);
        public const string FontClass = "$JixellationBold";

        /// <summary>
        /// The floor movie. template: one of the game's floor movies (DATA/UI/MAPS/*.GFX); keyframe: the frame label the map page goes
        /// to; fontLibrary: the URL the font import is pointed at (null keeps the template's).
        /// </summary>
        public static byte[] Build(byte[] template, string keyframe, IList<MapOutline> outlines, IList<MapLabel> labels, MapAnchorSettings anchor, string fontLibrary)
        {
            SwfFile swf = SwfFile.Read(template);
            // the start marker: the PlaceObject3 named startmarker_mc, the sprite it places and what that sprite places
            int markerSprite = -1;
            foreach (SwfTag t in swf.Tags.Where(o => o.Type == TagPlaceObject3 || o.Type == TagPlaceObject2))
                if (IndexOf(t.Body, Encoding.ASCII.GetBytes("startmarker_mc\0")) >= 0) markerSprite = PlacedCharacter(t);
            if (markerSprite < 0) throw new InvalidDataException("the template has no startmarker_mc");
            SwfTag markerDef = swf.Tags.First(o => o.Type == TagDefineSprite && Id(o) == markerSprite);
            HashSet<int> markerParts = new HashSet<int>(SpriteTags(markerDef.Body).Where(o => o.Type == TagPlaceObject2 || o.Type == TagPlaceObject3).Select(PlacedCharacter));

            List<SwfTag> kept = new List<SwfTag>();
            foreach (SwfTag t in swf.Tags)
            {
                switch (t.Type)
                {
                    case TagDefineShape: case TagDefineShape2: case TagDefineShape3: case TagDefineShape4: case TagDefineEditText:
                        if (markerParts.Contains(Id(t))) kept.Add(t); break;
                    case TagCsmTextSettings:
                        if (markerParts.Contains(Id(t))) kept.Add(t); break;
                    case TagDefineSprite:
                        if (Id(t) == markerSprite) kept.Add(t); break;
                    case TagPlaceObject2: case TagPlaceObject3:
                        break;
                    case TagFrameLabel:
                        kept.Add(new SwfTag() { Type = TagFrameLabel, Body = Str(keyframe) }); break;
                    case TagSceneAndFrameLabels:
                        kept.Add(new SwfTag() { Type = TagSceneAndFrameLabels, Body = Concat(EU32(1), EU32(0), Str("Scene 1"), EU32(1), EU32(0), Str(keyframe)) }); break;
                    case TagImportAssets2:
                        kept.Add(fontLibrary == null ? t : new SwfTag() { Type = TagImportAssets2, Body = ImportUrl(t.Body, fontLibrary), LongForm = t.LongForm }); break;
                    default:
                        kept.Add(t); break;
                }
            }
            // ours, after the font import (labels need it), then the start marker placed where the anchor maths put it
            List<SwfTag> ours = new List<SwfTag>();
            int id = 200, depth = 1;
            foreach (IGrouping<MapLayerStyle, MapOutline> layer in outlines.GroupBy(o => o.Style))
            {
                ours.Add(new SwfTag() { Type = TagDefineShape4, Body = Shape(id, layer.ToList(), layer.Key, anchor) });
                ours.Add(Sprite(id + 1, new List<SwfTag>() { Place(id, 1, null, 0, 0) }));
                ours.Add(Place(id + 1, depth++, "decoration_" + layer.Key.Name, 0, 0));
                id += 2;
            }
            if (labels != null && labels.Count != 0)
            {
                List<SwfTag> sub = new List<SwfTag>();
                int d = 1;
                foreach (MapLabel l in labels)
                {
                    PointF m = MapTransform.ToMovie(anchor, l.X, l.Z);
                    int w = 20 * 7 * l.Text.Length + 400;
                    ours.Add(new SwfTag() { Type = TagDefineEditText, Body = EditText(id, l.Text, 160, w, 240, 210) });
                    sub.Add(Place(id, d++, null, (int)Math.Round(m.X * 20) - w / 2, (int)Math.Round(m.Y * 20) - 120));
                    id++;
                }
                ours.Add(Sprite(id, sub));
                ours.Add(Place(id, depth++, "decoration_labels", 0, 0));
                id++;
            }
            ours.Add(Place(markerSprite, 1000, "startmarker_mc", (int)Math.Round(anchor.StartX * 20), (int)Math.Round(anchor.StartY * 20)));
            int at = kept.FindIndex(o => o.Type == TagImportAssets2);
            if (at < 0) at = kept.FindIndex(o => o.Type == TagFrameLabel);
            // the start marker's own definitions go before it is placed
            List<SwfTag> markerTags = kept.Where(o => o.Type != TagShowFrame && o.Type != TagEnd && (o.Type == TagDefineSprite || markerParts.Contains(DefinedId(o)))).ToList();
            foreach (SwfTag t in markerTags) kept.Remove(t);
            at = Math.Min(at, kept.Count - 1);
            kept.InsertRange(at + 1, markerTags.Concat(ours));
            swf.Tags = kept;
            return swf.Write();
        }

        #region Reading tags
        private static int Id(SwfTag t) { return t.Body[0] | (t.Body[1] << 8); }
        private static int DefinedId(SwfTag t)
        {
            switch (t.Type)
            {
                case TagDefineShape: case TagDefineShape2: case TagDefineShape3: case TagDefineShape4: case TagDefineEditText: case TagCsmTextSettings: case TagDefineSprite: return Id(t);
                default: return -1;
            }
        }
        /* The character a PlaceObject2 / 3 places (-1 when none) */
        private static int PlacedCharacter(SwfTag t)
        {
            byte[] b = t.Body;
            int flags = b[0], p = 1;
            bool hasClassName = false;
            if (t.Type == TagPlaceObject3) { hasClassName = (b[1] & 0x08) != 0 || ((b[1] & 0x10) != 0 && (flags & 0x02) != 0); p = 2; }
            p += 2; // depth
            if (hasClassName) { while (b[p] != 0) p++; p++; }
            if ((flags & 0x02) == 0) return -1;
            return b[p] | (b[p + 1] << 8);
        }
        private static List<SwfTag> SpriteTags(byte[] body)
        {
            List<SwfTag> tags = new List<SwfTag>();
            int p = 4;
            while (p + 2 <= body.Length)
            {
                int h = body[p] | (body[p + 1] << 8); p += 2;
                int type = h >> 6, len = h & 0x3F;
                if (len == 0x3F) { len = BitConverter.ToInt32(body, p); p += 4; }
                byte[] b = new byte[len]; Array.Copy(body, p, b, 0, len); p += len;
                tags.Add(new SwfTag() { Type = type, Body = b });
                if (type == TagEnd) break;
            }
            return tags;
        }
        private static int IndexOf(byte[] hay, byte[] needle)
        {
            for (int i = 0; i + needle.Length <= hay.Length; i++)
            {
                int k = 0; while (k < needle.Length && hay[i + k] == needle[k]) k++;
                if (k == needle.Length) return i;
            }
            return -1;
        }
        private static byte[] ImportUrl(byte[] body, string url)
        {
            int end = Array.IndexOf(body, (byte)0);
            return Concat(Str(url), body.Skip(end + 1).ToArray());
        }
        #endregion

        #region Writing tags
        private static byte[] Str(string s) { return Concat(Encoding.UTF8.GetBytes(s ?? ""), new byte[] { 0 }); }
        private static byte[] U16(int v) { return new byte[] { (byte)v, (byte)(v >> 8) }; }
        private static byte[] EU32(uint v) { List<byte> b = new List<byte>(); do { byte x = (byte)(v & 0x7F); v >>= 7; if (v != 0) x |= 0x80; b.Add(x); } while (v != 0); return b.ToArray(); }
        private static byte[] Concat(params byte[][] parts) { return parts.SelectMany(o => o).ToArray(); }

        private static SwfTag Place(int character, int depth, string name, int tx, int ty)
        {
            Bits m = new Bits();
            m.U(0, 1); m.U(0, 1); // no scale, no rotate
            int n = (tx == 0 && ty == 0) ? 0 : Math.Max(Bits.SBits(tx), Bits.SBits(ty));
            m.U(n, 5); m.S(tx, n); m.S(ty, n);
            byte flags = (byte)(0x02 | 0x04 | (name != null ? 0x20 : 0));
            return new SwfTag() { Type = TagPlaceObject2, Body = Concat(new[] { flags }, U16(depth), U16(character), m.ToArray(), name != null ? Str(name) : new byte[0]) };
        }

        private static SwfTag Sprite(int id, List<SwfTag> tags)
        {
            List<byte> b = new List<byte>(); b.AddRange(U16(id)); b.AddRange(U16(1));
            foreach (SwfTag t in tags.Concat(new[] { new SwfTag() { Type = TagShowFrame, Body = new byte[0] }, new SwfTag() { Type = TagEnd, Body = new byte[0] } }))
            {
                bool longForm = t.Body.Length >= 0x3F;
                int h = (t.Type << 6) | (longForm ? 0x3F : t.Body.Length);
                b.Add((byte)h); b.Add((byte)(h >> 8));
                if (longForm) b.AddRange(BitConverter.GetBytes(t.Body.Length));
                b.AddRange(t.Body);
            }
            return new SwfTag() { Type = TagDefineSprite, Body = b.ToArray() };
        }

        private static byte[] Rect(int x0, int x1, int y0, int y1)
        {
            Bits r = new Bits();
            int n = new[] { Bits.SBits(x0), Bits.SBits(x1), Bits.SBits(y0), Bits.SBits(y1) }.Max();
            r.U(n, 5); r.S(x0, n); r.S(x1, n); r.S(y0, n); r.S(y1, n);
            return r.ToArray();
        }

        /* DefineShape4: one fill (the tint at the layer's alpha) on the loops' left in movie space, one hairline */
        private static byte[] Shape(int id, List<MapOutline> loops, MapLayerStyle style, MapAnchorSettings anchor)
        {
            List<List<Point>> pts = loops.Select(o => o.Points.Select(p => { PointF m = MapTransform.ToMovie(anchor, p.X, p.Y); return new Point((int)Math.Round(m.X * 20), (int)Math.Round(m.Y * 20)); }).ToList()).ToList();
            List<Point> flat = pts.SelectMany(l => l).ToList();
            int x0 = flat.Min(p => p.X) - 20, x1 = flat.Max(p => p.X) + 20, y0 = flat.Min(p => p.Y) - 20, y1 = flat.Max(p => p.Y) + 20;
            bool fill = style.FillAlpha > 0;
            List<byte> b = new List<byte>();
            b.AddRange(U16(id));
            b.AddRange(Rect(x0, x1, y0, y1)); // shape bounds
            b.AddRange(Rect(x0, x1, y0, y1)); // edge bounds
            b.Add(0x01);                      // uses scaling strokes
            if (fill) { b.Add(1); b.Add(0x00); b.Add(Tint.R); b.Add(Tint.G); b.Add(Tint.B); b.Add((byte)style.FillAlpha); }
            else b.Add(0);
            b.Add(1);                                     // one line style (LINESTYLE2)
            b.AddRange(U16(1));                           // width: a hairline
            b.Add(0x00); b.Add(0x00);                     // round caps / joins, nothing else
            b.Add(Tint.R); b.Add(Tint.G); b.Add(Tint.B); b.Add((byte)style.LineAlpha);
            int fillBits = fill ? 1 : 0, lineBits = 1;
            b.Add((byte)((fillBits << 4) | lineBits));
            Bits s = new Bits();
            foreach (List<Point> l in pts)
            {
                // style change: move to the first point, fill 0 and the line set
                s.U(0, 1); s.U(0, 1); s.U(1, 1); s.U(0, 1); s.U(fill ? 1 : 0, 1); s.U(1, 1);
                int mb = Math.Max(Bits.SBits(l[0].X), Bits.SBits(l[0].Y));
                s.U(mb, 5); s.S(l[0].X, mb); s.S(l[0].Y, mb);
                if (fill) s.U(1, fillBits);
                s.U(1, lineBits);
                for (int k = 1; k <= l.Count; k++)
                {
                    Point a = l[k - 1], c = l[k % l.Count];
                    int dx = c.X - a.X, dy = c.Y - a.Y;
                    if (dx == 0 && dy == 0) continue;
                    int nb = Math.Max(2, Math.Max(Bits.SBits(dx), Bits.SBits(dy)));
                    s.U(1, 1); s.U(1, 1); s.U(nb - 2, 4); s.U(1, 1); s.S(dx, nb); s.S(dy, nb);
                }
            }
            s.U(0, 6); // end of shape
            b.AddRange(s.ToArray());
            return b.ToArray();
        }

        private static byte[] EditText(int id, string text, int height, int width, int boxHeight, int alpha)
        {
            List<byte> b = new List<byte>();
            b.AddRange(U16(id));
            b.AddRange(Rect(-40, width, -40, boxHeight));
            b.Add(0x80 | 0x08 | 0x04);              // has text, read only, has colour
            b.Add(0x80 | 0x20 | 0x10 | 0x01);       // has font class, has layout, no select, outlines
            b.AddRange(Str(FontClass));
            b.AddRange(U16(height));
            b.Add(Tint.R); b.Add(Tint.G); b.Add(Tint.B); b.Add((byte)alpha);
            b.Add(2); b.AddRange(U16(0)); b.AddRange(U16(0)); b.AddRange(U16(0)); b.AddRange(U16(40)); // centred
            b.AddRange(Str(""));
            b.AddRange(Str(text));
            return b.ToArray();
        }

        private sealed class Bits
        {
            private readonly List<byte> _b = new List<byte>();
            private int _bit;
            public static int SBits(int v) { int n = 1; while (v < -(1 << (n - 1)) || v > (1 << (n - 1)) - 1) n++; return n; }
            public void U(int value, int count)
            {
                for (int i = count - 1; i >= 0; i--)
                {
                    if (_bit == 0) _b.Add(0);
                    if (((value >> i) & 1) != 0) _b[_b.Count - 1] |= (byte)(0x80 >> _bit);
                    _bit = (_bit + 1) & 7;
                }
            }
            public void S(int value, int count) { U(value & (count >= 32 ? -1 : (1 << count) - 1), count); }
            public byte[] ToArray() { return _b.ToArray(); }
        }
        #endregion
    }
}

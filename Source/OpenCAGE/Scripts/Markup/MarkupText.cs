using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.Markup
{
    /// <summary>
    /// Block-letter text for markup: strokes on a 4 x 6 grid per character (A-Z, 0-9, space, hyphen), and signs made of it.
    /// </summary>
    public static class MarkupText
    {
        //Each glyph: polylines of grid points "x,y x,y ..." separated by '|'
        private static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string>()
        {
            ['A'] = "0,0 0,4 2,6 4,4 4,0|0,3 4,3",
            ['B'] = "0,0 0,6 3,6 4,5 4,4 3,3 0,3|3,3 4,2 4,1 3,0 0,0",
            ['C'] = "4,5 3,6 1,6 0,5 0,1 1,0 3,0 4,1",
            ['D'] = "0,0 0,6 3,6 4,5 4,1 3,0 0,0",
            ['E'] = "4,6 0,6 0,0 4,0|0,3 3,3",
            ['F'] = "4,6 0,6 0,0|0,3 3,3",
            ['G'] = "4,5 3,6 1,6 0,5 0,1 1,0 3,0 4,1 4,3 2,3",
            ['H'] = "0,0 0,6|4,0 4,6|0,3 4,3",
            ['I'] = "1,6 3,6|2,6 2,0|1,0 3,0",
            ['J'] = "4,6 4,1 3,0 1,0 0,1",
            ['K'] = "0,0 0,6|4,6 0,2|1,3 4,0",
            ['L'] = "0,6 0,0 4,0",
            ['M'] = "0,0 0,6 2,3 4,6 4,0",
            ['N'] = "0,0 0,6 4,0 4,6",
            ['O'] = "1,0 0,1 0,5 1,6 3,6 4,5 4,1 3,0 1,0",
            ['P'] = "0,0 0,6 3,6 4,5 4,4 3,3 0,3",
            ['Q'] = "1,0 0,1 0,5 1,6 3,6 4,5 4,1 3,0 1,0|2,2 4,0",
            ['R'] = "0,0 0,6 3,6 4,5 4,4 3,3 0,3|2,3 4,0",
            ['S'] = "4,5 3,6 1,6 0,5 0,4 1,3 3,3 4,2 4,1 3,0 1,0 0,1",
            ['T'] = "0,6 4,6|2,6 2,0",
            ['U'] = "0,6 0,1 1,0 3,0 4,1 4,6",
            ['V'] = "0,6 2,0 4,6",
            ['W'] = "0,6 1,0 2,4 3,0 4,6",
            ['X'] = "0,6 4,0|0,0 4,6",
            ['Y'] = "0,6 2,3 4,6|2,3 2,0",
            ['Z'] = "0,6 4,6 0,0 4,0",
            ['0'] = "1,0 0,1 0,5 1,6 3,6 4,5 4,1 3,0 1,0|0,1 4,5",
            ['1'] = "1,5 2,6 2,0|1,0 3,0",
            ['2'] = "0,5 1,6 3,6 4,5 4,4 0,0 4,0",
            ['3'] = "0,5 1,6 3,6 4,5 4,4 3,3 4,2 4,1 3,0 1,0 0,1|1,3 3,3",
            ['4'] = "3,0 3,6 0,2 4,2",
            ['5'] = "4,6 0,6 0,3 3,3 4,2 4,1 3,0 0,0",
            ['6'] = "4,5 3,6 1,6 0,5 0,1 1,0 3,0 4,1 4,2 3,3 0,3",
            ['7'] = "0,6 4,6 1,0",
            ['8'] = "1,3 0,4 0,5 1,6 3,6 4,5 4,4 3,3 1,3 0,2 0,1 1,0 3,0 4,1 4,2 3,3",
            ['9'] = "0,1 1,0 3,0 4,1 4,5 3,6 1,6 0,5 0,4 1,3 4,3",
            ['-'] = "1,3 3,3",
            [' '] = "",
        };

        private const float GridHeight = 6f, Advance = 6f, StrokeUnits = 0.95f;

        /// <summary>How wide <paramref name="text"/> is at a cap height of <paramref name="height"/>.</summary>
        public static float Width(string text, float height) => text.Length == 0 ? 0 : ((text.Length - 1) * Advance + 4f) * height / GridHeight;

        /// <summary>
        /// Text in a plane: along <paramref name="u"/>, up <paramref name="v"/>, from depth <paramref name="near"/> to
        /// <paramref name="far"/> along <paramref name="n"/>, centred on <paramref name="centre"/> (u, v).
        /// </summary>
        public static void Write(MarkupMesh m, Vector3 colour, string text, Vector2 centre, float height, Vector3 u, Vector3 v, Vector3 n, float near, float far)
        {
            text = (text ?? "").ToUpperInvariant();
            float scale = height / GridHeight, width = Width(text, height), stroke = StrokeUnits * scale;
            Vector2 start = centre - new Vector2(width / 2, height / 2);
            for (int c = 0; c < text.Length; c++)
            {
                if (!Glyphs.TryGetValue(text[c], out string glyph) || glyph.Length == 0)
                    continue;
                Vector2 origin = start + new Vector2(c * Advance * scale, 0);
                foreach (string line in glyph.Split('|'))
                {
                    Vector2[] points = line.Split(' ').Select(p => p.Split(',')).Select(p => origin + new Vector2(float.Parse(p[0]), float.Parse(p[1])) * scale).ToArray();
                    for (int i = 0; i + 1 < points.Length; i++)
                        m.Stroke(colour, Vector3.Zero, u, v, n, points[i], points[i + 1], stroke, near, far);
                    //Square caps where strokes meet (a closed outline's first point too) so the joins close up
                    bool closed = points.Length > 2 && points[0] == points[points.Length - 1];
                    for (int i = closed ? 0 : 1; i < points.Length - 1; i++)
                    {
                        Vector2 p = points[i];
                        m.Prism(colour, Vector3.Zero, u, v, n, new[] { p + new Vector2(-stroke / 2, -stroke / 2), p + new Vector2(stroke / 2, -stroke / 2), p + new Vector2(stroke / 2, stroke / 2), p + new Vector2(-stroke / 2, stroke / 2) }, near, far);
                    }
                }
            }
        }

        /// <summary>
        /// One or two lines of text lying on the floor facing up, their top edge <paramref name="startZ"/> along the composite's
        /// Z and running back from there: readable from above, the letters' tops towards +Z.
        /// </summary>
        public static void FloorText(MarkupMesh m, Vector3 colour, string title, string subtitle, float startZ)
        {
            const float titleSize = 0.10f, subtitleSize = 0.065f, gap = 0.03f, bottom = 0.003f, ink = 0.008f;
            Write(m, colour, title, new Vector2(0, startZ - titleSize / 2), titleSize, Vector3.UnitX, Vector3.UnitZ, Vector3.UnitY, bottom, bottom + ink);
            if (!string.IsNullOrEmpty(subtitle))
                Write(m, colour, subtitle, new Vector2(0, startZ - titleSize - gap - subtitleSize / 2), subtitleSize, Vector3.UnitX, Vector3.UnitZ, Vector3.UnitY, bottom, bottom + ink);
        }
    }
}

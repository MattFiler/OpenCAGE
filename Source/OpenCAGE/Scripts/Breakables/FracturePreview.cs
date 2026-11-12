using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using Vector3 = System.Numerics.Vector3;

namespace OpenCAGE.Breakables
{
    /// <summary>
    /// A picture of the pieces for the Fracture window: each piece in its own colour, pushed out from the model's centre by
    /// <c>explode</c> of the model's size, its cut faces darker. Back faces are drawn dimmer, so a hollow left open shows.
    /// </summary>
    public static class FracturePreview
    {
        public static Bitmap Draw(IList<FracturePiece> pieces, int width, int height, double explode = 0.35, double yawDegrees = 35, double pitchDegrees = 25, Color? background = null)
        {
            Bitmap bitmap = new Bitmap(Math.Max(16, width), Math.Max(16, height), PixelFormat.Format32bppArgb);
            Color back = background ?? Color.FromArgb(32, 34, 38);
            int w = bitmap.Width, h = bitmap.Height;
            int[] pixels = Enumerable.Repeat(back.ToArgb(), w * h).ToArray();
            float[] depth = Enumerable.Repeat(float.MaxValue, w * h).ToArray();
            if (pieces != null && pieces.Count != 0)
            {
                //The whole model's bounds, for the scale and the explode distance
                Vector3 mn = new Vector3(float.MaxValue), mx = new Vector3(float.MinValue);
                foreach (FracturePiece p in pieces) foreach (Vector3 q in p.PointsInModel) { mn = Vector3.Min(mn, q); mx = Vector3.Max(mx, q); }
                Vector3 centre = (mn + mx) * 0.5f;
                float size = Math.Max(1e-3f, (mx - mn).Length());
                float push = (float)(explode * size);

                double yaw = yawDegrees * Math.PI / 180, pitch = pitchDegrees * Math.PI / 180;
                Vector3 Cam(Vector3 v)
                {
                    v -= centre;
                    double x = v.X * Math.Cos(yaw) - v.Z * Math.Sin(yaw), z = v.X * Math.Sin(yaw) + v.Z * Math.Cos(yaw);
                    double y = v.Y * Math.Cos(pitch) - z * Math.Sin(pitch), z2 = v.Y * Math.Sin(pitch) + z * Math.Cos(pitch);
                    return new Vector3((float)x, (float)y, (float)z2);
                }
                float scale = Math.Min(w, h) / (size * (1.0f + 2.2f * (float)explode)) * 0.95f;
                Vector3 light = Vector3.Normalize(new Vector3(-0.4f, 0.7f, -0.6f));
                for (int i = 0; i < pieces.Count; i++)
                {
                    FracturePiece piece = pieces[i];
                    Color hue = Hue(i, pieces.Count);
                    Vector3 offset = piece.Pivot + piece.Outward * push;
                    foreach (FractureTriangle t in piece.Triangles)
                    {
                        if (t.Kind == FractureTriangleKind.Skin) continue;
                        Vector3 a = Cam(t.A.P + offset), b = Cam(t.B.P + offset), c = Cam(t.C.P + offset);
                        Vector3 n = Vector3.Cross(b - a, c - a);
                        if (n.LengthSquared() < 1e-20f) continue;
                        n = Vector3.Normalize(n);
                        //Looking down +Z: a front face's normal points back at the viewer (-Z)
                        bool front = n.Z < 0;
                        float lit = 0.35f + 0.65f * Math.Max(0f, Vector3.Dot(front ? n : -n, light));
                        float tone = t.Kind == FractureTriangleKind.Cap ? 0.45f : 1f;
                        if (!front) tone *= 0.35f;
                        Color col = Color.FromArgb(255, Clamp(hue.R * lit * tone), Clamp(hue.G * lit * tone), Clamp(hue.B * lit * tone));
                        Raster(pixels, depth, w, h, Screen(a, w, h, scale), Screen(b, w, h, scale), Screen(c, w, h, scale), col.ToArgb());
                    }
                }
            }
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            bitmap.UnlockBits(data);
            return bitmap;
        }

        /// <summary>A contact sheet: the pieces exploded from four sides, one tile each.</summary>
        public static Bitmap Sheet(IList<FracturePiece> pieces, int tile = 360, double explode = 0.35)
        {
            Bitmap sheet = new Bitmap(tile * 2, tile * 2, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(sheet))
            {
                double[][] views = { new[] { 35.0, 25.0 }, new[] { 215.0, 25.0 }, new[] { 125.0, -20.0 }, new[] { 305.0, 60.0 } };
                for (int i = 0; i < 4; i++)
                    using (Bitmap one = Draw(pieces, tile, tile, i == 3 ? 0.0 : explode, views[i][0], views[i][1]))
                        g.DrawImage(one, (i % 2) * tile, (i / 2) * tile);
            }
            return sheet;
        }

        private static PointF3 Screen(Vector3 v, int w, int h, float scale) => new PointF3(w * 0.5f + v.X * scale, h * 0.5f - v.Y * scale, v.Z);

        private struct PointF3
        {
            public float X, Y, Z;
            public PointF3(float x, float y, float z) { X = x; Y = y; Z = z; }
        }

        private static void Raster(int[] pixels, float[] depth, int w, int h, PointF3 a, PointF3 b, PointF3 c, int argb)
        {
            int minX = Math.Max(0, (int)Math.Floor(Math.Min(a.X, Math.Min(b.X, c.X)))), maxX = Math.Min(w - 1, (int)Math.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X))));
            int minY = Math.Max(0, (int)Math.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y)))), maxY = Math.Min(h - 1, (int)Math.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y))));
            float area = (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);
            if (Math.Abs(area) < 1e-6f) return;
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float w0 = ((b.X - px) * (c.Y - py) - (c.X - px) * (b.Y - py)) / area;
                    float w1 = ((c.X - px) * (a.Y - py) - (a.X - px) * (c.Y - py)) / area;
                    float w2 = 1 - w0 - w1;
                    if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                    float z = w0 * a.Z + w1 * b.Z + w2 * c.Z;
                    int at = y * w + x;
                    if (z >= depth[at]) continue;
                    depth[at] = z;
                    pixels[at] = argb;
                }
        }

        private static Color Hue(int i, int n)
        {
            double h = (i * 0.618033988749895) % 1.0;
            double s = 0.55, v = 0.95;
            int hi = (int)(h * 6) % 6;
            double f = h * 6 - Math.Floor(h * 6), p = v * (1 - s), q = v * (1 - f * s), t = v * (1 - (1 - f) * s);
            double r, g, b;
            switch (hi) { case 0: r = v; g = t; b = p; break; case 1: r = q; g = v; b = p; break; case 2: r = p; g = v; b = t; break; case 3: r = p; g = q; b = v; break; case 4: r = t; g = p; b = v; break; default: r = v; g = p; b = q; break; }
            return Color.FromArgb(255, (int)(r * 255), (int)(g * 255), (int)(b * 255));
        }

        private static int Clamp(float v) => Math.Max(0, Math.Min(255, (int)v));
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;

namespace OpenCAGE.Rigging
{
    /// <summary>
    /// A small software rasteriser for the rig tools' pictures: orthographic views of triangle meshes, with
    /// lines drawn over them. Nothing here needs the viewport, so the rebind dialog and the MCP tools can show
    /// a result before anything is imported.
    /// </summary>
    public class RigPicture
    {
        public readonly int Width, Height;
        private readonly byte[] _rgb;
        private readonly float[] _depth;

        public RigPicture(int width, int height, byte background = 40)
        {
            Width = width; Height = height;
            _rgb = new byte[width * height * 3];
            _depth = new float[width * height];
            for (int i = 0; i < _rgb.Length; i++) _rgb[i] = background;
            for (int i = 0; i < _depth.Length; i++) _depth[i] = float.NegativeInfinity;
        }

        public class View
        {
            public Vector3 Centre, Right, Up, Toward;     //Toward points at the camera
            public float PixelsPerMetre;
            public int X, Y, W, H;
            public string Caption;

            public Vector3 Project(Vector3 p)
            {
                Vector3 d = p - Centre;
                return new Vector3(X + W / 2f + Vector3.Dot(d, Right) * PixelsPerMetre, Y + H / 2f - Vector3.Dot(d, Up) * PixelsPerMetre, Vector3.Dot(d, Toward));
            }

            /// <summary>A character's front (it faces -Z in CATHODE's mesh space, so the camera looks along +Z) or side.</summary>
            public static View Character(bool front, Vector3 centre, float ppm, int x, int y, int w, int h, string caption)
            {
                return front
                    ? new View { Centre = centre, Right = new Vector3(-1, 0, 0), Up = Vector3.UnitY, Toward = new Vector3(0, 0, -1), PixelsPerMetre = ppm, X = x, Y = y, W = w, H = h, Caption = caption }
                    : new View { Centre = centre, Right = new Vector3(0, 0, -1), Up = Vector3.UnitY, Toward = new Vector3(1, 0, 0), PixelsPerMetre = ppm, X = x, Y = y, W = w, H = h, Caption = caption };
            }
        }

        public void Triangle(View v, Vector3 a, Vector3 b, Vector3 c, Color colour)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.LengthSquared() < 1e-14f) return;
            n = Vector3.Normalize(n);
            float light = 0.25f + 0.75f * Math.Abs(Vector3.Dot(n, Vector3.Normalize(v.Toward + 0.4f * v.Up + 0.3f * v.Right)));
            Vector3 pa = v.Project(a), pb = v.Project(b), pc = v.Project(c);
            int minX = (int)Math.Max(v.X, Math.Floor(Math.Min(pa.X, Math.Min(pb.X, pc.X))));
            int maxX = (int)Math.Min(v.X + v.W - 1, Math.Ceiling(Math.Max(pa.X, Math.Max(pb.X, pc.X))));
            int minY = (int)Math.Max(v.Y, Math.Floor(Math.Min(pa.Y, Math.Min(pb.Y, pc.Y))));
            int maxY = (int)Math.Min(v.Y + v.H - 1, Math.Ceiling(Math.Max(pa.Y, Math.Max(pb.Y, pc.Y))));
            float area = (pb.X - pa.X) * (pc.Y - pa.Y) - (pb.Y - pa.Y) * (pc.X - pa.X);
            if (Math.Abs(area) < 1e-9f) return;
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float w0 = ((pb.X - px) * (pc.Y - py) - (pb.Y - py) * (pc.X - px)) / area;
                    float w1 = ((pc.X - px) * (pa.Y - py) - (pc.Y - py) * (pa.X - px)) / area;
                    float w2 = 1 - w0 - w1;
                    if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                    float z = w0 * pa.Z + w1 * pb.Z + w2 * pc.Z;
                    int i = y * Width + x;
                    if (z <= _depth[i]) continue;
                    _depth[i] = z;
                    _rgb[i * 3] = (byte)(colour.R * light); _rgb[i * 3 + 1] = (byte)(colour.G * light); _rgb[i * 3 + 2] = (byte)(colour.B * light);
                }
        }

        /// <summary>A line drawn over everything, with an optional dot at its start.</summary>
        public void Line(View v, Vector3 a, Vector3 b, Color colour, int dot = 0)
        {
            Vector3 pa = v.Project(a), pb = v.Project(b);
            int steps = (int)Math.Max(1, Math.Max(Math.Abs(pb.X - pa.X), Math.Abs(pb.Y - pa.Y)));
            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)steps;
                Plot((int)(pa.X + (pb.X - pa.X) * t), (int)(pa.Y + (pb.Y - pa.Y) * t), colour, v);
            }
            for (int dx = -dot; dx <= dot; dx++) for (int dy = -dot; dy <= dot; dy++) Plot((int)pa.X + dx, (int)pa.Y + dy, colour, v);
        }

        private void Plot(int x, int y, Color c, View v)
        {
            if (x < v.X || y < v.Y || x >= v.X + v.W || y >= v.Y + v.H || x < 0 || y < 0 || x >= Width || y >= Height) return;
            int i = (y * Width + x) * 3;
            _rgb[i] = c.R; _rgb[i + 1] = c.G; _rgb[i + 2] = c.B;
        }

        /// <summary>A colour per bone, stable and distinct enough to tell neighbours apart.</summary>
        public static Color BoneColour(int bone)
        {
            uint h = (uint)(bone * 2654435761u);
            return Color.FromArgb((int)(80 + (h & 0x7F)), (int)(80 + ((h >> 8) & 0x7F)), (int)(80 + ((h >> 16) & 0x7F)));
        }

        public Bitmap ToBitmap(IEnumerable<View> captions = null)
        {
            Bitmap bitmap = new Bitmap(Width, Height, PixelFormat.Format24bppRgb);
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            byte[] row = new byte[data.Stride];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    //GDI's 24 bit rows are BGR
                    row[x * 3] = _rgb[(y * Width + x) * 3 + 2];
                    row[x * 3 + 1] = _rgb[(y * Width + x) * 3 + 1];
                    row[x * 3 + 2] = _rgb[(y * Width + x) * 3];
                }
                Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, data.Stride);
            }
            bitmap.UnlockBits(data);
            if (captions != null)
                using (Graphics g = Graphics.FromImage(bitmap))
                using (Font font = new Font("Segoe UI", 9f))
                    foreach (View v in captions)
                        if (!string.IsNullOrEmpty(v.Caption)) g.DrawString(v.Caption, font, System.Drawing.Brushes.Gainsboro, v.X + 6, v.Y + 4);
            return bitmap;
        }
    }
}

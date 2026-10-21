using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace OpenCAGE.TextureTools
{
    /// <summary>
    /// The mip chain of a cubemap the way the game's own environment maps are made. Their levels are not plain halvings:
    /// level k is the top level blurred over the whole sphere with a cosine-power lobe that widens level by level (about 2
    /// degrees at level 1, 45 by level 6), so a rough surface, which reads a lower level, sees a blurred reflection -
    /// box-filtered levels make rough metals look like chrome. Measured on shipped cubemaps from five levels: the lobe is
    /// cos^e of the angle from the texel's direction, divided by cos^(e-1) summed the same way (which is what darkens the
    /// smallest levels, as the game's are), on sRGB-decoded values, with the exponents in <see cref="Exponents"/>; built
    /// that way the levels match the game's to within what DXT1 compression changes anyway. The blur goes across face
    /// edges, so the faces still meet without seams.
    /// </summary>
    public static class CubemapFilter
    {
        /// <summary>The lobe exponent for each level below the top (level 1 first), measured on shipped cubemaps.</summary>
        public static readonly float[] Exponents = { 768f, 96f, 24f, 6f, 1.5f, 0.5f };

        /// <summary>How many directions each texel's blur samples: rings out from its direction, spokes round each ring.</summary>
        private const int RingCount = 16, SpokeCount = 24;

        /// <summary>
        /// Every level of six faces (+X, -X, +Y, -Y, +Z, -Z; size x size RGBA, sRGB), from the faces themselves down to
        /// <paramref name="smallest"/> pixels: [level][face].
        /// </summary>
        public static List<byte[][]> MipChain(byte[][] faces, int size, int smallest)
        {
            List<byte[][]> chain = new List<byte[][]> { faces };
            //The top level and plain halvings of it, in linear light: each level is sampled from the one twice its size, which
            //holds everything finer than the level can show
            List<float[][]> pyramid = new List<float[][]> { ToLinear(faces) };
            for (int s = size / 2; s >= 1; s /= 2)
                pyramid.Add(Halve(pyramid[pyramid.Count - 1], s * 2));
            int level = 1;
            for (int s = size / 2; s >= Math.Max(1, smallest); s /= 2, level++)
            {
                float exponent = Exponents[Math.Min(level - 1, Exponents.Length - 1)];
                chain.Add(ToEncoded(Blur(pyramid[level - 1], s * 2, s, exponent), s));
            }
            return chain;
        }

        #region SRGB
        private static float[] _toLinear;

        private static float[][] ToLinear(byte[][] faces)
        {
            if (_toLinear == null)
            {
                float[] table = new float[256];
                for (int i = 0; i < 256; i++)
                {
                    double c = i / 255.0;
                    table[i] = (float)(c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4));
                }
                _toLinear = table;
            }
            float[][] linear = new float[6][];
            for (int f = 0; f < 6; f++)
            {
                byte[] face = faces[f];
                float[] o = new float[face.Length / 4 * 3];
                for (int p = 0, q = 0; p < face.Length; p += 4, q += 3)
                {
                    o[q] = _toLinear[face[p]];
                    o[q + 1] = _toLinear[face[p + 1]];
                    o[q + 2] = _toLinear[face[p + 2]];
                }
                linear[f] = o;
            }
            return linear;
        }

        private static byte[][] ToEncoded(float[][] linear, int size)
        {
            byte[][] faces = new byte[6][];
            for (int f = 0; f < 6; f++)
            {
                byte[] o = new byte[size * size * 4];
                float[] l = linear[f];
                for (int p = 0, q = 0; q < l.Length; p += 4, q += 3)
                {
                    o[p] = Encode(l[q]);
                    o[p + 1] = Encode(l[q + 1]);
                    o[p + 2] = Encode(l[q + 2]);
                    o[p + 3] = 255;
                }
                faces[f] = o;
            }
            return faces;
        }

        private static byte Encode(float value)
        {
            if (!(value > 0f)) return 0;
            if (value >= 1f) return 255;
            double c = value <= 0.0031308 ? value * 12.92 : 1.055 * Math.Pow(value, 1.0 / 2.4) - 0.055;
            return (byte)Math.Max(0, Math.Min(255, Math.Round(c * 255.0)));
        }
        #endregion

        /// <summary>Each face averaged 2 x 2 down to <paramref name="to"/> (from twice that).</summary>
        private static float[][] Halve(float[][] source, int from)
        {
            int to = Math.Max(1, from / 2);
            float[][] target = new float[6][];
            for (int f = 0; f < 6; f++)
            {
                float[] s = source[f], o = new float[to * to * 3];
                for (int y = 0; y < to; y++)
                    for (int x = 0; x < to; x++)
                        for (int c = 0; c < 3; c++)
                        {
                            int a = ((y * 2) * from + x * 2) * 3 + c;
                            o[(y * to + x) * 3 + c] = (s[a] + s[a + 3] + s[a + from * 3] + s[a + from * 3 + 3]) * 0.25f;
                        }
                target[f] = o;
            }
            return target;
        }

        /// <summary>
        /// The source (faces of size <paramref name="from"/>, linear RGB) blurred into faces of size <paramref name="to"/>:
        /// each target texel is sum(L cos^e) / sum(cos^(e-1)) over the hemisphere round its direction, taken over rings of
        /// spokes out to where the lobe has all but gone, sampling the source bilinearly - across faces.
        /// </summary>
        private static float[][] Blur(float[][] source, int from, int to, float exponent)
        {
            //The lobe out to where cos^e has fallen to 1/1000, never wider than a hemisphere
            double cutoff = Math.Pow(1e-3, 1.0 / exponent);
            double maxAngle = Math.Min(Math.PI / 2, Math.Acos(Math.Max(-1.0, Math.Min(1.0, cutoff))));
            //Rings at the middles of equal steps of angle, each standing for its band of the sphere (sin theta d theta)
            double step = maxAngle / RingCount;
            double[] cos = new double[RingCount], sin = new double[RingCount], numerator = new double[RingCount];
            double denominator = 0;
            for (int r = 0; r < RingCount; r++)
            {
                double angle = (r + 0.5) * step;
                cos[r] = Math.Cos(angle);
                sin[r] = Math.Sin(angle);
                double band = sin[r] * step;
                numerator[r] = Math.Pow(cos[r], exponent) * band;
                denominator += Math.Pow(cos[r], exponent - 1) * band;
            }
            float[] weight = new float[RingCount];
            for (int r = 0; r < RingCount; r++)
                weight[r] = (float)(numerator[r] / denominator);

            float[][] target = new float[6][];
            for (int f = 0; f < 6; f++)
                target[f] = new float[to * to * 3];

            Parallel.For(0, 6 * to, row =>
            {
                int f = row / to, y = row % to;
                for (int x = 0; x < to; x++)
                {
                    Vector3 d = Direction(f, (x + 0.5f) / to, (y + 0.5f) / to);
                    //Two axes square to the direction, to turn the spokes round it
                    Vector3 helper = Math.Abs(d.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX;
                    Vector3 t1 = Vector3.Normalize(Vector3.Cross(helper, d));
                    Vector3 t2 = Vector3.Cross(d, t1);
                    Vector3 sum = Vector3.Zero;
                    for (int r = 0; r < RingCount; r++)
                    {
                        float c = (float)cos[r], s = (float)sin[r];
                        Vector3 ring = Vector3.Zero;
                        for (int k = 0; k < SpokeCount; k++)
                        {
                            double phi = (k + (r % 2) * 0.5) * 2 * Math.PI / SpokeCount;
                            ring += Sample(source, from, d * c + (t1 * (float)Math.Cos(phi) + t2 * (float)Math.Sin(phi)) * s);
                        }
                        sum += ring * (weight[r] / SpokeCount);
                    }
                    float[] o = target[f];
                    int i = (y * to + x) * 3;
                    o[i] = sum.X;
                    o[i + 1] = sum.Y;
                    o[i + 2] = sum.Z;
                }
            });
            return target;
        }

        private static Vector3 Texel(float[] face, int size, int x, int y)
        {
            x = Math.Max(0, Math.Min(size - 1, x));
            y = Math.Max(0, Math.Min(size - 1, y));
            int i = (y * size + x) * 3;
            return new Vector3(face[i], face[i + 1], face[i + 2]);
        }

        /// <summary>The source along a direction, bilinearly within the face it lands on.</summary>
        private static Vector3 Sample(float[][] source, int size, Vector3 dir)
        {
            Locate(dir, out int f, out float u, out float v);
            float fx = u * size - 0.5f, fy = v * size - 0.5f;
            int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
            float ax = fx - x0, ay = fy - y0;
            float[] face = source[f];
            return Vector3.Lerp(
                Vector3.Lerp(Texel(face, size, x0, y0), Texel(face, size, x0 + 1, y0), ax),
                Vector3.Lerp(Texel(face, size, x0, y0 + 1), Texel(face, size, x0 + 1, y0 + 1), ax), ay);
        }

        /// <summary>
        /// The direction through a point of a face (u, v 0..1 from the top left), as Direct3D lays a cube out: +X looks
        /// along +X with u towards -Z and v towards -Y; -X: u +Z; +Y: u +X, v +Z; -Y: u +X, v -Z; +Z: u +X; -Z: u -X.
        /// </summary>
        public static Vector3 Direction(int face, float u, float v)
        {
            float a = u * 2 - 1, b = v * 2 - 1;
            Vector3 d;
            switch (face)
            {
                case 0: d = new Vector3(1, -b, -a); break;
                case 1: d = new Vector3(-1, -b, a); break;
                case 2: d = new Vector3(a, 1, b); break;
                case 3: d = new Vector3(a, -1, -b); break;
                case 4: d = new Vector3(a, -b, 1); break;
                default: d = new Vector3(-a, -b, -1); break;
            }
            return Vector3.Normalize(d);
        }

        /// <summary>The face a direction lands on, and where (u, v 0..1 from the top left): the inverse of <see cref="Direction"/>.</summary>
        public static void Locate(Vector3 d, out int face, out float u, out float v)
        {
            float ax = Math.Abs(d.X), ay = Math.Abs(d.Y), az = Math.Abs(d.Z);
            float a, b, m;
            if (ax >= ay && ax >= az)
            {
                m = ax;
                if (d.X > 0) { face = 0; a = -d.Z; b = -d.Y; }
                else { face = 1; a = d.Z; b = -d.Y; }
            }
            else if (ay >= az)
            {
                m = ay;
                if (d.Y > 0) { face = 2; a = d.X; b = d.Z; }
                else { face = 3; a = d.X; b = -d.Z; }
            }
            else
            {
                m = az;
                if (d.Z > 0) { face = 4; a = d.X; b = -d.Y; }
                else { face = 5; a = -d.X; b = -d.Y; }
            }
            u = (a / m + 1) * 0.5f;
            v = (b / m + 1) * 0.5f;
        }
    }
}

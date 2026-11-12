using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAGE.Building
{
    /// <summary>
    /// A picture as 8-bit sRGB, three bytes a pixel in Bitmap order (blue, green, red), rows top to bottom with no padding.
    /// </summary>
    public sealed class RgbImage
    {
        public int Width, Height;
        public byte[] Pixels;

        public RgbImage(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new byte[width * height * 3];
        }

        /// <summary>Bilinear sample at pixel coordinates (centres at +0.5); x wraps round when <paramref name="wrapX"/>, else clamps; y clamps. Out: blue, green, red 0-255.</summary>
        public void Sample(double x, double y, bool wrapX, out double b, out double g, out double r)
        {
            double fx = x - 0.5, fy = y - 0.5;
            int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
            double ax = fx - x0, ay = fy - y0;
            int x1 = x0 + 1, y1 = y0 + 1;
            if (wrapX)
            {
                x0 = ((x0 % Width) + Width) % Width;
                x1 = ((x1 % Width) + Width) % Width;
            }
            else
            {
                x0 = Math.Max(0, Math.Min(Width - 1, x0));
                x1 = Math.Max(0, Math.Min(Width - 1, x1));
            }
            y0 = Math.Max(0, Math.Min(Height - 1, y0));
            y1 = Math.Max(0, Math.Min(Height - 1, y1));
            int i00 = (y0 * Width + x0) * 3, i10 = (y0 * Width + x1) * 3, i01 = (y1 * Width + x0) * 3, i11 = (y1 * Width + x1) * 3;
            double w00 = (1 - ax) * (1 - ay), w10 = ax * (1 - ay), w01 = (1 - ax) * ay, w11 = ax * ay;
            b = Pixels[i00] * w00 + Pixels[i10] * w10 + Pixels[i01] * w01 + Pixels[i11] * w11;
            g = Pixels[i00 + 1] * w00 + Pixels[i10 + 1] * w10 + Pixels[i01 + 1] * w01 + Pixels[i11 + 1] * w11;
            r = Pixels[i00 + 2] * w00 + Pixels[i10 + 2] * w10 + Pixels[i01 + 2] * w01 + Pixels[i11 + 2] * w11;
        }

        public RgbImage Crop(int x, int y, int width, int height)
        {
            RgbImage cut = new RgbImage(width, height);
            for (int row = 0; row < height; row++)
                Buffer.BlockCopy(Pixels, ((y + row) * Width + x) * 3, cut.Pixels, row * width * 3, width * 3);
            return cut;
        }

        /// <summary>Turned half a turn (both axes flipped).</summary>
        public RgbImage Rotate180()
        {
            RgbImage turned = new RgbImage(Width, Height);
            int n = Width * Height;
            for (int i = 0; i < n; i++)
            {
                int j = n - 1 - i;
                turned.Pixels[j * 3] = Pixels[i * 3];
                turned.Pixels[j * 3 + 1] = Pixels[i * 3 + 1];
                turned.Pixels[j * 3 + 2] = Pixels[i * 3 + 2];
            }
            return turned;
        }

        public RgbImage FlipHorizontal()
        {
            RgbImage flipped = new RgbImage(Width, Height);
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    int a = (y * Width + x) * 3, b = (y * Width + (Width - 1 - x)) * 3;
                    flipped.Pixels[b] = Pixels[a];
                    flipped.Pixels[b + 1] = Pixels[a + 1];
                    flipped.Pixels[b + 2] = Pixels[a + 2];
                }
            return flipped;
        }
    }

    /// <summary>How the faces of a cube map, or a sphere, are laid out in a picture.</summary>
    public enum PanoramaLayout
    {
        Auto,
        /// <summary>Equirectangular (latitude-longitude), twice as wide as it is high.</summary>
        Equirect,
        /// <summary>A cube unfolded as a cross, 4 faces wide and 3 high: up over front; left, front, right, back; down under front.</summary>
        HorizontalCross,
        /// <summary>A cube unfolded as a cross, 3 wide and 4 high: up; left, front, right; down; back (turned half a turn).</summary>
        VerticalCross,
        /// <summary>Six faces in a row, in DDS order: right (+X), left (-X), up (+Y), down (-Y), front (+Z), back (-Z).</summary>
        HorizontalStrip,
        /// <summary>Six faces in a column, in the same order.</summary>
        VerticalStrip,
        /// <summary>Six separate face pictures, or a DDS cube map.</summary>
        Faces,
    }

    /// <summary>What a sky picture says about the light it would give: where its sun is, and the colours of its sky and ground.</summary>
    public sealed class SkyAnalysis
    {
        /// <summary>A clear sun: the brightest spot is well above the rest of the sky.</summary>
        public bool SunFound;
        /// <summary>The sun's direction in the dome's own frame: azimuth from +Z towards +X, elevation above the horizon (degrees).</summary>
        public double SunAzimuth, SunElevation;
        /// <summary>The sun's spot against the sky's median (linear luminance); over ~3 counts as a sun.</summary>
        public double SunStrength;
        /// <summary>Colours as sRGB 0-255 with the brightest lane at 255 (a light's hue): the sun, the sky above (cosine-weighted, the sun's spot left out), the ground below the horizon, the horizon band.</summary>
        public Vector3 SunColour, SkyColour, GroundColour, HorizonColour;
        /// <summary>Mean linear luminance (0-1) of the upper and lower halves.</summary>
        public double SkyLuminance, GroundLuminance;
        public int SourceWidth, SourceHeight;
    }

    /// <summary>
    /// Sky dome pictures: a panorama of any common layout made into the equirectangular picture an inside-out dome is drawn with, and the
    /// dome itself as a glTF for the model importer.
    /// Direction law (CATHODE axes: left handed, +Y up, yaw 0 faces +Z, yaw 90 faces +X): u = 0.5 + azimuth / 2pi, v = 0.5 - elevation / pi
    /// (v = 0 at the top), azimuth from +Z towards +X; direction = (cos el sin az, sin el, cos el cos az). The dome's vertices use the same law.
    /// Cube faces follow Direct3D's cube map convention (as the game's environment maps): +X right (u -> -Z, v -> -Y), -X left (u -> +Z),
    /// +Y up (u -> +X, v -> +Z), -Y down (u -> +X, v -> -Z), +Z front (u -> +X), -Z back (u -> -X).
    /// </summary>
    public static class Panorama
    {
        #region The direction law
        public static Vector3 DirectionOf(double u, double v)
        {
            double az = (u - 0.5) * 2 * Math.PI, el = (0.5 - v) * Math.PI;
            return new Vector3((float)(Math.Cos(el) * Math.Sin(az)), (float)Math.Sin(el), (float)(Math.Cos(el) * Math.Cos(az)));
        }

        public static void UVOf(Vector3 d, out double u, out double v)
        {
            double length = Math.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z);
            double el = Math.Asin(Math.Max(-1, Math.Min(1, d.Y / Math.Max(length, 1e-12))));
            double az = Math.Atan2(d.X, d.Z);
            u = 0.5 + az / (2 * Math.PI);
            v = 0.5 - el / Math.PI;
        }

        public static Vector3 DirectionOfAngles(double azimuthDegrees, double elevationDegrees)
        {
            double az = azimuthDegrees * Math.PI / 180, el = elevationDegrees * Math.PI / 180;
            return new Vector3((float)(Math.Cos(el) * Math.Sin(az)), (float)Math.Sin(el), (float)(Math.Cos(el) * Math.Cos(az)));
        }

        public static void AnglesOf(Vector3 d, out double azimuthDegrees, out double elevationDegrees)
        {
            double length = Math.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z);
            elevationDegrees = Math.Asin(Math.Max(-1, Math.Min(1, d.Y / Math.Max(length, 1e-12)))) * 180 / Math.PI;
            azimuthDegrees = Math.Atan2(d.X, d.Z) * 180 / Math.PI;
            if (azimuthDegrees < 0) azimuthDegrees += 360;
        }

        public const int Right = 0, Left = 1, Up = 2, Down = 3, Front = 4, Back = 5;
        public static readonly string[] FaceNames = { "right", "left", "up", "down", "front", "back" };

        /// <summary>The direction through a face at (s, t): s -1..1 left to right across the face picture, t -1..1 bottom to top.</summary>
        public static Vector3 FaceDirection(int face, double s, double t)
        {
            switch (face)
            {
                case Right: return Vector3.Normalize(new Vector3(1, (float)t, (float)-s));
                case Left: return Vector3.Normalize(new Vector3(-1, (float)t, (float)s));
                case Up: return Vector3.Normalize(new Vector3((float)s, 1, (float)-t));
                case Down: return Vector3.Normalize(new Vector3((float)s, -1, (float)t));
                case Front: return Vector3.Normalize(new Vector3((float)s, (float)t, 1));
                default: return Vector3.Normalize(new Vector3((float)-s, (float)t, -1));
            }
        }

        /// <summary>Which face a direction goes through, and where on it (as <see cref="FaceDirection"/>).</summary>
        public static int FaceOf(Vector3 d, out double s, out double t)
        {
            double ax = Math.Abs(d.X), ay = Math.Abs(d.Y), az = Math.Abs(d.Z);
            if (ax >= ay && ax >= az)
            {
                if (d.X > 0) { s = -d.Z / ax; t = d.Y / ax; return Right; }
                s = d.Z / ax; t = d.Y / ax; return Left;
            }
            if (ay >= az)
            {
                if (d.Y > 0) { s = d.X / ay; t = -d.Z / ay; return Up; }
                s = d.X / ay; t = d.Z / ay; return Down;
            }
            if (d.Z > 0) { s = d.X / az; t = d.Y / az; return Front; }
            s = -d.X / az; t = d.Y / az; return Back;
        }
        #endregion

        #region Layouts
        /// <summary>The layout a picture's shape suggests (null when it fits none).</summary>
        public static PanoramaLayout? Detect(int width, int height)
        {
            double ratio = width / (double)height;
            if (Math.Abs(ratio - 2) < 0.1) return PanoramaLayout.Equirect;
            if (width % 4 == 0 && height % 3 == 0 && width / 4 == height / 3) return PanoramaLayout.HorizontalCross;
            if (width % 3 == 0 && height % 4 == 0 && width / 3 == height / 4) return PanoramaLayout.VerticalCross;
            if (width == height * 6) return PanoramaLayout.HorizontalStrip;
            if (height == width * 6) return PanoramaLayout.VerticalStrip;
            return null;
        }

        /// <summary>A cube picture's six faces, in DDS order (right, left, up, down, front, back).</summary>
        public static RgbImage[] SplitFaces(RgbImage picture, PanoramaLayout layout)
        {
            RgbImage[] faces = new RgbImage[6];
            switch (layout)
            {
                case PanoramaLayout.HorizontalCross:
                    {
                        int n = picture.Width / 4;
                        faces[Up] = picture.Crop(n, 0, n, n);
                        faces[Left] = picture.Crop(0, n, n, n);
                        faces[Front] = picture.Crop(n, n, n, n);
                        faces[Right] = picture.Crop(2 * n, n, n, n);
                        faces[Back] = picture.Crop(3 * n, n, n, n);
                        faces[Down] = picture.Crop(n, 2 * n, n, n);
                        break;
                    }
                case PanoramaLayout.VerticalCross:
                    {
                        int n = picture.Width / 3;
                        faces[Up] = picture.Crop(n, 0, n, n);
                        faces[Left] = picture.Crop(0, n, n, n);
                        faces[Front] = picture.Crop(n, n, n, n);
                        faces[Right] = picture.Crop(2 * n, n, n, n);
                        faces[Down] = picture.Crop(n, 2 * n, n, n);
                        //Folded on under the down face, so it is upside down as it lies in the cross
                        faces[Back] = picture.Crop(n, 3 * n, n, n).Rotate180();
                        break;
                    }
                case PanoramaLayout.HorizontalStrip:
                    {
                        int n = picture.Height;
                        for (int f = 0; f < 6; f++) faces[f] = picture.Crop(f * n, 0, n, n);
                        break;
                    }
                case PanoramaLayout.VerticalStrip:
                    {
                        int n = picture.Width;
                        for (int f = 0; f < 6; f++) faces[f] = picture.Crop(0, f * n, n, n);
                        break;
                    }
                default:
                    throw new ArgumentException("Not a cube layout: " + layout);
            }
            return faces;
        }

        /// <summary>A horizontal cross from six faces (the reverse of <see cref="SplitFaces"/>).</summary>
        public static RgbImage MakeCross(RgbImage[] faces)
        {
            int n = faces[0].Width;
            RgbImage cross = new RgbImage(4 * n, 3 * n);
            void Put(RgbImage face, int cx, int cy)
            {
                for (int row = 0; row < n; row++)
                    Buffer.BlockCopy(face.Pixels, row * n * 3, cross.Pixels, ((cy * n + row) * cross.Width + cx * n) * 3, n * 3);
            }
            Put(faces[Up], 1, 0);
            Put(faces[Left], 0, 1);
            Put(faces[Front], 1, 1);
            Put(faces[Right], 2, 1);
            Put(faces[Back], 3, 1);
            Put(faces[Down], 1, 2);
            return cross;
        }

        private static void SampleFace(RgbImage[] faces, Vector3 d, out double b, out double g, out double r)
        {
            int face = FaceOf(d, out double s, out double t);
            RgbImage image = faces[face];
            image.Sample((s + 1) * 0.5 * image.Width, (1 - t) * 0.5 * image.Height, false, out b, out g, out r);
        }

        /// <summary>Six faces made into an equirectangular picture <paramref name="width"/> wide (2x2 samples a pixel).</summary>
        public static RgbImage FacesToEquirect(RgbImage[] faces, int width)
        {
            if (faces == null || faces.Length != 6 || faces.Any(o => o == null)) throw new ArgumentException("A cube needs all six faces.");
            int size = faces[0].Width;
            if (faces.Any(o => o.Width != size || o.Height != size)) throw new ArgumentException("The six faces must be square and the same size.");
            int height = width / 2;
            RgbImage result = new RgbImage(width, height);
            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    double sb = 0, sg = 0, sr = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        double u = (x + 0.25 + 0.5 * (k & 1)) / width, v = (y + 0.25 + 0.5 * (k >> 1)) / height;
                        SampleFace(faces, DirectionOf(u, v), out double b, out double g, out double r);
                        sb += b; sg += g; sr += r;
                    }
                    int i = (y * width + x) * 3;
                    result.Pixels[i] = ToByte(sb / 4);
                    result.Pixels[i + 1] = ToByte(sg / 4);
                    result.Pixels[i + 2] = ToByte(sr / 4);
                }
            });
            return result;
        }

        /// <summary>An equirectangular picture made into six faces <paramref name="size"/> square (for tests and previews).</summary>
        public static RgbImage[] EquirectToFaces(RgbImage equirect, int size)
        {
            RgbImage[] faces = new RgbImage[6];
            for (int f = 0; f < 6; f++)
            {
                RgbImage face = new RgbImage(size, size);
                int fi = f;
                Parallel.For(0, size, y =>
                {
                    for (int x = 0; x < size; x++)
                    {
                        double s = (x + 0.5) / size * 2 - 1, t = 1 - (y + 0.5) / size * 2;
                        UVOf(FaceDirection(fi, s, t), out double u, out double v);
                        equirect.Sample(u * equirect.Width, v * equirect.Height, true, out double b, out double g, out double r);
                        int i = (y * size + x) * 3;
                        face.Pixels[i] = ToByte(b);
                        face.Pixels[i + 1] = ToByte(g);
                        face.Pixels[i + 2] = ToByte(r);
                    }
                });
                faces[f] = face;
            }
            return faces;
        }

        /// <summary>An equirectangular picture resampled to <paramref name="width"/> (area-averaged when it shrinks).</summary>
        public static RgbImage ResizeEquirect(RgbImage source, int width)
        {
            int height = width / 2;
            if (source.Width == width && source.Height == height) return source;
            RgbImage result = new RgbImage(width, height);
            int k = Math.Max(1, (int)Math.Ceiling(source.Width / (double)width));
            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    double sb = 0, sg = 0, sr = 0;
                    for (int j = 0; j < k; j++)
                        for (int i = 0; i < k; i++)
                        {
                            double u = (x + (i + 0.5) / k) / width, v = (y + (j + 0.5) / k) / height;
                            source.Sample(u * source.Width, v * source.Height, true, out double b, out double g, out double r);
                            sb += b; sg += g; sr += r;
                        }
                    int n = k * k, o = (y * width + x) * 3;
                    result.Pixels[o] = ToByte(sb / n);
                    result.Pixels[o + 1] = ToByte(sg / n);
                    result.Pixels[o + 2] = ToByte(sr / n);
                }
            });
            return result;
        }
        #endregion

        #region Adjustments
        /// <summary>Multiply every pixel by a tint (0-255 a lane; 255 keeps it), in linear light.</summary>
        public static void Tint(RgbImage image, Vector3 tint)
        {
            if (tint.X >= 254.5f && tint.Y >= 254.5f && tint.Z >= 254.5f) return;
            double[] lin = LinearTable();
            byte[][] table = new byte[3][];
            float[] lanes = { tint.Z, tint.Y, tint.X }; //blue, green, red
            for (int c = 0; c < 3; c++)
            {
                table[c] = new byte[256];
                double k = SrgbToLinear(Math.Max(0, Math.Min(255, lanes[c])) / 255.0);
                for (int i = 0; i < 256; i++) table[c][i] = (byte)Math.Round(LinearToSrgb(lin[i] * k) * 255);
            }
            for (int i = 0; i < image.Pixels.Length; i++) image.Pixels[i] = table[i % 3][image.Pixels[i]];
        }

        /// <summary>
        /// Repaint everything below the horizon (elevation <paramref name="horizonDegrees"/>) as the sky above mirrored in calm water,
        /// darker the steeper it looks down (a water surface's fresnel: reflectance 1 - sin(depression), the rest a dark sea): for a
        /// level whose sea runs out to the dome, so the far water and the dome's lower half meet without a line.
        /// </summary>
        public static void MirrorBelowHorizon(RgbImage equirect, double horizonDegrees, Vector3 seaColour)
        {
            int w = equirect.Width, h = equirect.Height;
            double rim = horizonDegrees * Math.PI / 180;
            double[] lin = LinearTable();
            double seaB = SrgbToLinear(seaColour.Z / 255.0), seaG = SrgbToLinear(seaColour.Y / 255.0), seaR = SrgbToLinear(seaColour.X / 255.0);
            byte[] original = (byte[])equirect.Pixels.Clone();
            RgbImage source = new RgbImage(w, h) { Pixels = original };
            Parallel.For(0, h, y =>
            {
                double v = (y + 0.5) / h, el = (0.5 - v) * Math.PI;
                if (el >= rim) return;
                double mirrored = 2 * rim - el, dep = rim - el;
                double sv = (0.5 - mirrored / Math.PI) * h;
                double reflect = Math.Max(0, 1 - Math.Sin(dep));
                for (int x = 0; x < w; x++)
                {
                    source.Sample(x + 0.5, sv, true, out double b, out double g, out double r);
                    int i = (y * w + x) * 3;
                    equirect.Pixels[i] = ToByte(LinearToSrgb(SrgbToLinear(b / 255) * reflect + seaB * (1 - reflect)) * 255);
                    equirect.Pixels[i + 1] = ToByte(LinearToSrgb(SrgbToLinear(g / 255) * reflect + seaG * (1 - reflect)) * 255);
                    equirect.Pixels[i + 2] = ToByte(LinearToSrgb(SrgbToLinear(r / 255) * reflect + seaR * (1 - reflect)) * 255);
                }
            });
        }
        #endregion

        #region Analysis
        /// <summary>The sun, the sky and the ground of an equirectangular sky picture (in the dome's own frame).</summary>
        public static SkyAnalysis Analyse(RgbImage equirect)
        {
            const int W = 256, H = 128;
            RgbImage small = ResizeEquirect(equirect, W);
            double[] lin = LinearTable();
            double[] lum = new double[W * H], lr = new double[W * H], lg = new double[W * H], lb = new double[W * H];
            for (int i = 0; i < W * H; i++)
            {
                lb[i] = lin[small.Pixels[i * 3]];
                lg[i] = lin[small.Pixels[i * 3 + 1]];
                lr[i] = lin[small.Pixels[i * 3 + 2]];
                lum[i] = 0.2126 * lr[i] + 0.7152 * lg[i] + 0.0722 * lb[i];
            }
            double El(int y) => (0.5 - (y + 0.5) / H) * Math.PI;

            //The sun. Where: the peak of the sky's light blurred over ~5 degrees (a sun's glow; a bright cloud is spread out and blurs
            //down), at or above the horizon (5 degrees under it allowed: a setting sun). Exactly: the clipped-white blob - pixels as
            //bright as the picture's brightest, on a 1024-wide copy max-pooled from the full picture so a small disc survives - nearest
            //that peak within 10 degrees, else the peak itself
            Vector3[] cellDir = new Vector3[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++) cellDir[y * W + x] = DirectionOf((x + 0.5) / W, (y + 0.5) / H);
            double[] blurred = new double[W * H];
            double sigma = 5 * Math.PI / 180, cosCut = Math.Cos(12 * Math.PI / 180);
            int reachRows = (int)Math.Ceiling(12.0 / 180 * H) + 1;
            Parallel.For(0, H, y =>
            {
                if (El(y) < -5 * Math.PI / 180) return;
                for (int x = 0; x < W; x++)
                {
                    Vector3 d0 = cellDir[y * W + x];
                    double s = 0, sw = 0;
                    for (int yy = Math.Max(0, y - reachRows); yy <= Math.Min(H - 1, y + reachRows); yy++)
                    {
                        double ce = Math.Cos(El(yy));
                        for (int xx = 0; xx < W; xx++)
                        {
                            float c = Vector3.Dot(cellDir[yy * W + xx], d0);
                            if (c < cosCut) continue;
                            double ang = Math.Acos(Math.Min(1, c)), w = Math.Exp(-ang * ang / (2 * sigma * sigma)) * ce;
                            s += lum[yy * W + xx] * w; sw += w;
                        }
                    }
                    blurred[y * W + x] = sw > 0 ? s / sw : 0;
                }
            });
            int peakCell = 0;
            for (int i = 1; i < W * H; i++) if (blurred[i] > blurred[peakCell]) peakCell = i;
            Vector3 glowDir = cellDir[peakCell];
            Vector3 peakDir = BrightestBlobNear(equirect, glowDir, out double brightest) ?? glowDir;
            //The disc's brightness: the 256-wide cells within 3 degrees of it. Its colour: a clipped disc is white whatever the sun's
            //colour, so the hue of its glow instead - the light 1.5 to 8 degrees out over the sky further out (10 to 20 degrees): a
            //sunset's orange halo, a midday sun's white one
            double peak = 0, sunR = 0, sunG = 0, sunB = 0;
            double cos3 = Math.Cos(3 * Math.PI / 180), cos1 = Math.Cos(1.5 * Math.PI / 180), cos8 = Math.Cos(8 * Math.PI / 180), cos10s = Math.Cos(10 * Math.PI / 180), cos20 = Math.Cos(20 * Math.PI / 180);
            double bgR = 0, bgG = 0, bgB = 0, bgW = 0;
            for (int i = 0; i < W * H; i++)
            {
                double c = Vector3.Dot(cellDir[i], peakDir);
                if (c >= cos3) peak = Math.Max(peak, lum[i]);
                if (c <= cos10s && c >= cos20) { bgR += lr[i]; bgG += lg[i]; bgB += lb[i]; bgW++; }
            }
            if (bgW > 0) { bgR /= bgW; bgG /= bgW; bgB /= bgW; }
            for (int i = 0; i < W * H; i++)
            {
                double c = Vector3.Dot(cellDir[i], peakDir);
                if (c > cos1 || c < cos8) continue;
                sunR += Math.Max(0, lr[i] - bgR); sunG += Math.Max(0, lg[i] - bgG); sunB += Math.Max(0, lb[i] - bgB);
            }
            peak = Math.Max(peak, brightest);
            Vector3 centroid = peakDir;

            //The sky's median for the sun's strength, and the colours: the sky above weighted as a level floor sees it (sin el x
            //cos el, the solid angle) with the sun's spot left out; the ground below weighted the same way; the horizon band +-10
            List<double> upper = new List<double>();
            double skyR = 0, skyG = 0, skyB = 0, skyW = 0, grR = 0, grG = 0, grB = 0, grW = 0, hoR = 0, hoG = 0, hoB = 0, hoW = 0, upL = 0, upW = 0, loL = 0, loW = 0;
            double cos15 = Math.Cos(15 * Math.PI / 180);
            for (int y = 0; y < H; y++)
            {
                double el = El(y), ce = Math.Cos(el);
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (el > 0)
                    {
                        upper.Add(lum[i]);
                        upL += lum[i] * ce; upW += ce;
                        Vector3 d = DirectionOf((x + 0.5) / W, (y + 0.5) / H);
                        if (Vector3.Dot(d, centroid) < cos15)
                        {
                            double w = Math.Sin(el) * ce;
                            skyR += lr[i] * w; skyG += lg[i] * w; skyB += lb[i] * w; skyW += w;
                        }
                    }
                    else
                    {
                        loL += lum[i] * ce; loW += ce;
                        double w = -Math.Sin(el) * ce;
                        grR += lr[i] * w; grG += lg[i] * w; grB += lb[i] * w; grW += w;
                    }
                    if (Math.Abs(el) < 10 * Math.PI / 180) { hoR += lr[i] * ce; hoG += lg[i] * ce; hoB += lb[i] * ce; hoW += ce; }
                }
            }
            upper.Sort();
            double median = upper.Count == 0 ? 0 : upper[upper.Count / 2];
            SkyAnalysis result = new SkyAnalysis()
            {
                SunStrength = median > 1e-5 ? peak / median : (peak > 0.01 ? 1000 : 0),
                SkyLuminance = upW > 0 ? upL / upW : 0,
                GroundLuminance = loW > 0 ? loL / loW : 0,
                SourceWidth = equirect.Width,
                SourceHeight = equirect.Height,
            };
            result.SunFound = result.SunStrength >= 3 && peak > 0.05;
            AnglesOf(centroid, out result.SunAzimuth, out result.SunElevation);
            //a halo is more saturated than the light it stands for (sky and haze tint it): 70% of the way from white
            Vector3 halo = Hue(sunR, sunG, sunB);
            result.SunColour = new Vector3(255, 255, 255) - (new Vector3(255, 255, 255) - halo) * 0.7f;
            result.SunColour = new Vector3((float)Math.Round(result.SunColour.X), (float)Math.Round(result.SunColour.Y), (float)Math.Round(result.SunColour.Z));
            result.SkyColour = Hue(skyR, skyG, skyB);
            result.GroundColour = Hue(grR, grG, grB);
            result.HorizonColour = Hue(hoR, hoG, hoB);
            return result;
        }

        /// <summary>
        /// The middle of the clipped-white blob nearest <paramref name="near"/> (within 10 degrees): pixels as bright as the picture's
        /// brightest (at or above 5 degrees under the horizon), on a 1024-wide copy max-pooled from the full picture, joined into blobs.
        /// A sun's disc is usually clipped white, often run together with clipped cloud beside it: its middle is the blob's deepest
        /// part (furthest from the blob's edge), not the blob's centroid.
        /// </summary>
        private static Vector3? BrightestBlobNear(RgbImage image, Vector3 near, out double brightest)
        {
            const int W = 1024, H = 512;
            double[] lin = LinearTable();
            double[] pooled = new double[W * H];
            for (int y = 0; y < image.Height; y++)
            {
                int cy = Math.Min(H - 1, (int)((long)y * H / image.Height));
                for (int x = 0; x < image.Width; x++)
                {
                    int i = (y * image.Width + x) * 3, c = cy * W + Math.Min(W - 1, (int)((long)x * W / image.Width));
                    double l = 0.2126 * lin[image.Pixels[i + 2]] + 0.7152 * lin[image.Pixels[i + 1]] + 0.0722 * lin[image.Pixels[i]];
                    if (l > pooled[c]) pooled[c] = l;
                }
            }
            int lowest = (int)Math.Floor((0.5 + 5.0 / 180) * H);
            brightest = 0;
            for (int i = 0; i < lowest * W; i++) brightest = Math.Max(brightest, pooled[i]);
            if (brightest <= 0) return null;
            double cut = brightest * 0.97;
            int[] label = new int[W * H];
            double cos10 = Math.Cos(10 * Math.PI / 180), bestAngle = double.MaxValue;
            Vector3? bestMiddle = null;
            double bestDepth = -1;
            int next = 0;
            Stack<int> stack = new Stack<int>();
            for (int start = 0; start < lowest * W; start++)
            {
                if (label[start] != 0 || pooled[start] < cut) continue;
                next++;
                label[start] = next;
                stack.Push(start);
                List<int> blob = new List<int>();
                Vector3 sum = Vector3.Zero;
                while (stack.Count > 0)
                {
                    int p = stack.Pop(), py = p / W, px = p % W;
                    blob.Add(p);
                    sum += DirectionOf((px + 0.5) / W, (py + 0.5) / H) * (float)Math.Cos((0.5 - (py + 0.5) / H) * Math.PI);
                    int[] around = { py * W + (px + 1) % W, py * W + (px + W - 1) % W, py > 0 ? p - W : -1, py < lowest - 1 ? p + W : -1 };
                    foreach (int q in around)
                        if (q >= 0 && label[q] == 0 && pooled[q] >= cut) { label[q] = next; stack.Push(q); }
                }
                if (sum.LengthSquared() <= 0 || blob.Count > W * H / 8) continue;
                //the blob counts as near when any of it is within 10 degrees; nearest by its nearest pixel
                double nearest = double.MaxValue;
                foreach (int p in blob)
                {
                    double c = Vector3.Dot(DirectionOf((p % W + 0.5) / W, (p / W + 0.5) / H), near);
                    if (c >= cos10) nearest = Math.Min(nearest, Math.Acos(Math.Min(1, c)));
                }
                if (nearest == double.MaxValue) continue;
                var (depth, middle) = Middle(blob, W, H);
                if (middle == null) continue;
                //the fattest blob near the glow: a round disc beats a thin strip of clipped cloud beside it
                if (depth > bestDepth + 0.5 || (Math.Abs(depth - bestDepth) <= 0.5 && nearest < bestAngle)) { bestDepth = depth; bestAngle = nearest; bestMiddle = middle; }
            }
            return bestMiddle;
        }

        /// <summary>A blob's depth (pixels to its edge at its deepest; x scaled by cos el, the picture's stretch) and the direction of its deepest part.</summary>
        private static (double depth, Vector3? middle) Middle(List<int> bestBlob, int W, int H)
        {
            //Its deepest part: city-block distance to the nearest pixel outside it (x scaled by cos el, the picture's stretch)
            int minX = bestBlob.Min(p => p % W), maxX = bestBlob.Max(p => p % W), minY = bestBlob.Min(p => p / W), maxY = bestBlob.Max(p => p / W);
            if (maxX - minX > W / 2) return (0, null); //across the seam: rare enough to leave to the glow's peak
            int bw = maxX - minX + 3, bh = maxY - minY + 3;
            double[] dist = new double[bw * bh];
            HashSet<int> inBlob = new HashSet<int>(bestBlob);
            for (int y = 0; y < bh; y++)
                for (int x = 0; x < bw; x++)
                {
                    int gx = minX - 1 + x, gy = minY - 1 + y;
                    bool inside = gx >= 0 && gx < W && gy >= 0 && gy < H && inBlob.Contains(gy * W + gx);
                    dist[y * bw + x] = inside ? 1e9 : 0;
                }
            double Stretch(int y) => Math.Max(0.05, Math.Cos((0.5 - (minY - 1 + y + 0.5) / H) * Math.PI));
            for (int y = 1; y < bh; y++)
                for (int x = 1; x < bw; x++)
                {
                    int i = y * bw + x;
                    dist[i] = Math.Min(dist[i], Math.Min(dist[i - 1] + Stretch(y), dist[i - bw] + 1));
                }
            for (int y = bh - 2; y >= 0; y--)
                for (int x = bw - 2; x >= 0; x--)
                {
                    int i = y * bw + x;
                    dist[i] = Math.Min(dist[i], Math.Min(dist[i + 1] + Stretch(y), dist[i + bw] + 1));
                }
            double deepest = dist.Max();
            Vector3 middle = Vector3.Zero;
            for (int y = 0; y < bh; y++)
                for (int x = 0; x < bw; x++)
                    if (dist[y * bw + x] >= deepest * 0.8 && dist[y * bw + x] > 0)
                        middle += DirectionOf((minX - 1 + x + 0.5) / W, (minY - 1 + y + 0.5) / H);
            return (deepest, middle.LengthSquared() > 0 ? Vector3.Normalize(middle) : (Vector3?)null);
        }

        /// <summary>A linear colour as sRGB 0-255 with its brightest lane at 255 (a light's colour; the strength goes in its intensity).</summary>
        public static Vector3 Hue(double r, double g, double b)
        {
            double m = Math.Max(r, Math.Max(g, b));
            if (m <= 1e-9) return new Vector3(255, 255, 255);
            return new Vector3((float)Math.Round(LinearToSrgb(r / m) * 255), (float)Math.Round(LinearToSrgb(g / m) * 255), (float)Math.Round(LinearToSrgb(b / m) * 255));
        }
        #endregion

        #region Files
        /// <summary>A picture System.Drawing reads (PNG, JPEG, BMP, GIF, TIFF).</summary>
        public static RgbImage LoadBitmap(string path)
        {
            using (Bitmap loaded = new Bitmap(path))
                return FromBitmap(loaded);
        }

        public static RgbImage FromBitmap(Bitmap bitmap)
        {
            RgbImage image = new RgbImage(bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                for (int y = 0; y < image.Height; y++)
                    Marshal.Copy(data.Scan0 + y * data.Stride, image.Pixels, y * image.Width * 3, image.Width * 3);
            }
            finally { bitmap.UnlockBits(data); }
            return image;
        }

        public static Bitmap ToBitmap(RgbImage image)
        {
            Bitmap bitmap = new Bitmap(image.Width, image.Height, PixelFormat.Format24bppRgb);
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            try
            {
                for (int y = 0; y < image.Height; y++)
                    Marshal.Copy(image.Pixels, y * image.Width * 3, data.Scan0 + y * data.Stride, image.Width * 3);
            }
            finally { bitmap.UnlockBits(data); }
            return bitmap;
        }

        public static void SavePng(RgbImage image, string path)
        {
            using (Bitmap bitmap = ToBitmap(image))
                bitmap.Save(path, ImageFormat.Png);
        }

        /// <summary>
        /// The pixels of an uncompressed 32-bit DDS (B8G8R8A8 / A8R8G8B8, as texconv writes for any input), one picture or the six
        /// faces of a cube map (top mip of each). Null when the file is not that.
        /// </summary>
        public static RgbImage[] FromBgraDds(byte[] dds, out bool cube)
        {
            cube = false;
            if (dds == null || dds.Length < 128 || dds[0] != 'D' || dds[1] != 'D' || dds[2] != 'S' || dds[3] != ' ') return null;
            int height = BitConverter.ToInt32(dds, 12), width = BitConverter.ToInt32(dds, 16);
            int mips = Math.Max(1, BitConverter.ToInt32(dds, 28));
            int caps2 = BitConverter.ToInt32(dds, 112);
            bool dx10 = dds[84] == 'D' && dds[85] == 'X' && dds[86] == '1' && dds[87] == '0';
            int offset = dx10 ? 148 : 128;
            if (dx10)
            {
                int dxgi = BitConverter.ToInt32(dds, 128), miscFlag = BitConverter.ToInt32(dds, 136), arraySize = BitConverter.ToInt32(dds, 140);
                if (dxgi != 87 && dxgi != 91 && dxgi != 88 && dxgi != 93) return null; //B8G8R8A8 / _SRGB / B8G8R8X8 / _SRGB
                cube = (miscFlag & 0x4) != 0 && arraySize >= 1;
            }
            else
            {
                int bits = BitConverter.ToInt32(dds, 88);
                if (bits != 32) return null;
                cube = (caps2 & 0x200) != 0;
            }
            int faces = cube ? 6 : 1;
            long mipBytes = 0;
            for (int m = 0, w = width, h = height; m < mips; m++, w = Math.Max(1, w / 2), h = Math.Max(1, h / 2)) mipBytes += (long)w * h * 4;
            if (offset + mipBytes * (faces - 1) + (long)width * height * 4 > dds.Length) return null;
            RgbImage[] result = new RgbImage[faces];
            for (int f = 0; f < faces; f++)
            {
                RgbImage image = new RgbImage(width, height);
                long start = offset + mipBytes * f;
                for (int i = 0; i < width * height; i++)
                {
                    image.Pixels[i * 3] = dds[start + i * 4];
                    image.Pixels[i * 3 + 1] = dds[start + i * 4 + 1];
                    image.Pixels[i * 3 + 2] = dds[start + i * 4 + 2];
                }
                result[f] = image;
            }
            return result;
        }

        /// <summary>
        /// The inside-facing dome as a glTF (written beside <paramref name="picture"/>, which it names as its texture): a UV sphere of
        /// <paramref name="radius"/> m, <paramref name="columns"/> x <paramref name="rows"/> quads, UVs by the direction law, normals in.
        /// Positions go into the file with Z negated (glTF is right handed; the importer negates it back); every triangle is wound
        /// counter-clockwise seen from inside. Returns the .gltf path.
        /// </summary>
        public static string WriteDomeGltf(string folder, string baseName, string picture, double radius, int columns = 128, int rows = 64, string materialName = "skydome")
        {
            List<float> pos = new List<float>(), nrm = new List<float>(), uv = new List<float>();
            for (int j = 0; j <= rows; j++)
            {
                double v = (double)j / rows;
                for (int i = 0; i <= columns; i++)
                {
                    //the pole rows take each column's centre, so the pole triangles don't smear the whole top row of the picture
                    double u = (j == 0 || j == rows) ? (i + 0.5) / columns : (double)i / columns;
                    Vector3 d = DirectionOf(u, v);
                    pos.Add((float)(d.X * radius)); pos.Add((float)(d.Y * radius)); pos.Add((float)(-d.Z * radius));
                    nrm.Add(-d.X); nrm.Add(-d.Y); nrm.Add(d.Z);
                    uv.Add((float)u); uv.Add((float)v);
                }
            }
            List<uint> idx = new List<uint>();
            int stride = columns + 1;
            Vector3 P(uint k) => new Vector3(pos[(int)k * 3], pos[(int)k * 3 + 1], pos[(int)k * 3 + 2]);
            void Tri(int a, int b, int c)
            {
                Vector3 pa = P((uint)a), pb = P((uint)b), pc = P((uint)c);
                Vector3 face = Vector3.Cross(pb - pa, pc - pa);
                if (face.LengthSquared() < 1e-12f) return;
                //counter-clockwise from inside = the face normal (right-hand rule) pointing at the centre
                if (Vector3.Dot(face, pa + pb + pc) > 0) { int t = b; b = c; c = t; }
                idx.Add((uint)a); idx.Add((uint)b); idx.Add((uint)c);
            }
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < columns; i++)
                {
                    int a = j * stride + i, b = a + 1, c = a + stride, d = c + 1;
                    if (j != 0) Tri(a, b, c);
                    if (j != rows - 1) Tri(b, d, c);
                }
            bool wide = pos.Count / 3 > 65535;

            byte[] bin;
            int posBytes = pos.Count * 4, nrmBytes = nrm.Count * 4, uvBytes = uv.Count * 4, idxBytes = idx.Count * (wide ? 4 : 2);
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter w = new BinaryWriter(ms))
            {
                foreach (float f in pos) w.Write(f);
                foreach (float f in nrm) w.Write(f);
                foreach (float f in uv) w.Write(f);
                foreach (uint s in idx) { if (wide) w.Write(s); else w.Write((ushort)s); }
                while (ms.Length % 4 != 0) w.Write((byte)0);
                bin = ms.ToArray();
            }
            Directory.CreateDirectory(folder);
            string binName = baseName + ".bin";
            File.WriteAllBytes(Path.Combine(folder, binName), bin);
            int vcount = pos.Count / 3;
            CultureInfo ic = CultureInfo.InvariantCulture;
            string r = radius.ToString("0.###", ic);
            StringBuilder j2 = new StringBuilder();
            j2.Append("{\n");
            j2.Append("  \"asset\": { \"version\": \"2.0\", \"generator\": \"OpenCAGE sky dome\" },\n");
            j2.Append("  \"scene\": 0, \"scenes\": [ { \"nodes\": [ 0 ] } ],\n");
            j2.Append("  \"nodes\": [ { \"name\": \"" + baseName + "\", \"mesh\": 0 } ],\n");
            j2.Append("  \"meshes\": [ { \"name\": \"" + baseName + "\", \"primitives\": [ { \"attributes\": { \"POSITION\": 0, \"NORMAL\": 1, \"TEXCOORD_0\": 2 }, \"indices\": 3, \"material\": 0 } ] } ],\n");
            j2.Append("  \"materials\": [ { \"name\": \"" + materialName + "\", \"pbrMetallicRoughness\": { \"baseColorTexture\": { \"index\": 0 }, \"metallicFactor\": 0.0, \"roughnessFactor\": 1.0 } } ],\n");
            j2.Append("  \"textures\": [ { \"source\": 0, \"sampler\": 0 } ],\n");
            j2.Append("  \"samplers\": [ { \"magFilter\": 9729, \"minFilter\": 9987, \"wrapS\": 10497, \"wrapT\": 33071 } ],\n");
            j2.Append("  \"images\": [ { \"uri\": \"" + Path.GetFileName(picture) + "\" } ],\n");
            j2.Append("  \"buffers\": [ { \"uri\": \"" + binName + "\", \"byteLength\": " + bin.Length + " } ],\n");
            j2.Append("  \"bufferViews\": [\n");
            j2.Append("    { \"buffer\": 0, \"byteOffset\": 0, \"byteLength\": " + posBytes + ", \"target\": 34962 },\n");
            j2.Append("    { \"buffer\": 0, \"byteOffset\": " + posBytes + ", \"byteLength\": " + nrmBytes + ", \"target\": 34962 },\n");
            j2.Append("    { \"buffer\": 0, \"byteOffset\": " + (posBytes + nrmBytes) + ", \"byteLength\": " + uvBytes + ", \"target\": 34962 },\n");
            j2.Append("    { \"buffer\": 0, \"byteOffset\": " + (posBytes + nrmBytes + uvBytes) + ", \"byteLength\": " + idxBytes + ", \"target\": 34963 }\n");
            j2.Append("  ],\n");
            j2.Append("  \"accessors\": [\n");
            j2.Append("    { \"bufferView\": 0, \"componentType\": 5126, \"count\": " + vcount + ", \"type\": \"VEC3\", \"min\": [ -" + r + ", -" + r + ", -" + r + " ], \"max\": [ " + r + ", " + r + ", " + r + " ] },\n");
            j2.Append("    { \"bufferView\": 1, \"componentType\": 5126, \"count\": " + vcount + ", \"type\": \"VEC3\" },\n");
            j2.Append("    { \"bufferView\": 2, \"componentType\": 5126, \"count\": " + vcount + ", \"type\": \"VEC2\" },\n");
            j2.Append("    { \"bufferView\": 3, \"componentType\": " + (wide ? 5125 : 5123) + ", \"count\": " + idx.Count + ", \"type\": \"SCALAR\" }\n");
            j2.Append("  ]\n");
            j2.Append("}\n");
            string gltf = Path.Combine(folder, baseName + ".gltf");
            File.WriteAllText(gltf, j2.ToString());
            return gltf;
        }
        #endregion

        #region Colour
        public static double SrgbToLinear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        public static double LinearToSrgb(double c) { c = Math.Max(0, Math.Min(1, c)); return c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055; }

        private static double[] _linear;
        public static double[] LinearTable()
        {
            if (_linear == null)
            {
                double[] t = new double[256];
                for (int i = 0; i < 256; i++) t[i] = SrgbToLinear(i / 255.0);
                _linear = t;
            }
            return _linear;
        }

        public static byte ToByte(double v) => (byte)Math.Max(0, Math.Min(255, Math.Round(v)));
        #endregion
    }
}

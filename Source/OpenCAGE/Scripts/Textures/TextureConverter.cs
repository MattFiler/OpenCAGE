using CATHODE;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using static CATHODE.Textures;
using static DirectXTex.DirectXTexUtility;

namespace OpenCAGE.TextureTools
{
    /// <summary>
    /// Turning an image the user has on disk into the DDS a texture slot wants, in whichever of the
    /// game's formats they ask for.
    ///
    /// Two tools do the work, both carried inside OpenCAGE and unpacked on first use:
    ///
    /// - Microsoft's <b>texconv</b> (DirectXTex) for everything with a DXGI name - the block formats
    ///   the PC game uses and the uncompressed ones - reading DDS, PNG, JPG, TGA, BMP, TIFF and HDR.
    /// - Arm's <b>astcenc</b> for the three ASTC formats, which DirectXTex has no encoder for at all.
    ///   Those only appear in the Feral mobile ports; nothing in the PC data uses them.
    ///
    /// A file that is already a DDS in the format being asked for is passed straight through without
    /// running anything, which is the common case when someone exports from a texture tool.
    /// </summary>
    public static class TextureConverter
    {
        /// <summary>Everything texconv will read, as an OpenFileDialog filter.</summary>
        public const string ImportFilter =
            "All supported images|*.dds;*.png;*.jpg;*.jpeg;*.tga;*.bmp;*.tif;*.tiff;*.hdr;*.astc"
            + "|DDS|*.dds|PNG|*.png|JPEG|*.jpg;*.jpeg|Targa|*.tga|Bitmap|*.bmp|TIFF|*.tif;*.tiff|Radiance HDR|*.hdr"
            + "|ASTC|*.astc|All files|*.*";

        /// <summary>How long either tool is given before it's assumed to have hung, in milliseconds.</summary>
        private const int ToolTimeout = 10 * 60 * 1000;

        #region FORMATS

        /// <summary>
        /// The formats worth offering for the build that's open, in the order the enum declares them.
        ///
        /// Everything the game can read except CTX1, an Xbox 360 format with no DXGI name and no
        /// encoder. ASTC is offered on the mobile build alone - no other port reads it, and offering it
        /// elsewhere is offering a way to make a texture the game can't load.
        /// </summary>
        public static IEnumerable<TextureFormat> ImportFormats(PatchManager.Platform platform)
        {
            bool astc = ReadsAstc(platform);
            foreach (TextureFormat format in DdsFile.WritableFormats())
                if (astc || !IsAstc(format)) yield return format;
        }

        /// <summary>
        /// Whether this build of the game reads ASTC at all. The iOS and Android port is the only one
        /// that does - the Switch build ships block-compressed textures like the desktop ones.
        /// </summary>
        public static bool ReadsAstc(PatchManager.Platform platform)
        {
            return platform == PatchManager.Platform.IOS_ANDROID;
        }

        /// <summary>Whether this format is ASTC, and so goes to astcenc rather than texconv.</summary>
        public static bool IsAstc(TextureFormat format)
        {
            return format == TextureFormat.ASTC4X4 || format == TextureFormat.ASTC8X8 || format == TextureFormat.ASTC12X12;
        }

        /// <summary>The block footprint astcenc names, e.g. "8x8".</summary>
        private static string AstcBlock(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.ASTC4X4: return "4x4";
                case TextureFormat.ASTC8X8: return "8x8";
                case TextureFormat.ASTC12X12: return "12x12";
                default: return null;
            }
        }

        /// <summary>
        /// How a format reads in the dropdown - the game's own name, what it's for, and a warning on
        /// the ones that only work outside the PC build.
        /// </summary>
        public static string Describe(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.BC7: return "BC7  —  best quality, colour or colour + alpha";
                case TextureFormat.DXN: return "DXN  —  BC5, two channels, for normal maps";
                case TextureFormat.DXT1: return "DXT1  —  BC1, small, colour with 1-bit alpha";
                case TextureFormat.DXT3: return "DXT3  —  BC2, colour with sharp alpha";
                case TextureFormat.DXT5: return "DXT5  —  BC3, colour with smooth alpha";
                case TextureFormat.BC6H: return "BC6H  —  high dynamic range colour";
                case TextureFormat.A8R8G8B8: return "A8R8G8B8  —  uncompressed, 32-bit";
                case TextureFormat.X8R8G8B8: return "X8R8G8B8  —  uncompressed, 32-bit, no alpha";
                case TextureFormat.A16R16G16B16: return "A16R16G16B16  —  uncompressed, 64-bit";
                case TextureFormat.A32R32G32B32F: return "A32R32G32B32F  —  uncompressed, floating point";
                case TextureFormat.A4R4G4B4: return "A4R4G4B4  —  uncompressed, 16-bit";
                case TextureFormat.A8: return "A8  —  single channel, alpha";
                case TextureFormat.L8: return "L8  —  single channel, luminance";
                case TextureFormat.R16F: return "R16F  —  single channel, floating point";
                case TextureFormat.ASTC4X4: return "ASTC 4x4  —  iOS and Android builds";
                case TextureFormat.ASTC8X8: return "ASTC 8x8  —  iOS and Android builds";
                case TextureFormat.ASTC12X12: return "ASTC 12x12  —  iOS and Android builds";
                default: return format.ToString();
            }
        }

        #endregion

        #region CONVERSION

        /// <summary>
        /// Convert a file on disk into the DDS bytes for a texture in <paramref name="target"/>
        /// format, or null with a reason. The result always carries a DX10 header, so it can be read
        /// straight back by <see cref="DdsFile.Read"/>.
        /// </summary>
        /// <param name="mipLevels">0 for a full chain, 1 for the top level alone.</param>
        public static byte[] Convert(string path, TextureFormat target, int mipLevels, out string problem)
        {
            problem = null;
            byte[] source;
            try { source = File.ReadAllBytes(path); }
            catch (Exception e) { problem = "'" + Path.GetFileName(path) + "' could not be read: " + e.Message; return null; }

            /* A DDS already in the format being asked for, with the mip chain being asked for, needs
             * nothing doing to it. Worth checking because it is the ordinary case - somebody
             * exporting BC7 from their texture tool and bringing it straight back in - and it keeps
             * their exact bytes rather than putting the image through a second lossy compression. */
            if (DdsFile.Describe(source, out DXGI_FORMAT dxgi, out int width, out int height, out int mips, out bool _))
            {
                DXGI_FORMAT? wanted = DdsFile.DxgiFor(target);
                bool sameFormat = wanted != null && dxgi == wanted.Value;

                /* The chain has to be the one asked for, not merely present. Asking for a full one
                 * accepts whatever chain the file already has, because that is what texconv would
                 * hand back too - it keeps an input's mips rather than rebuilding them. */
                bool mipsSuit = mipLevels <= 0
                    ? mips > 1 || FullChain(width, height) == 1
                    : mips == mipLevels || (mipLevels == 1 && mips == 0);

                if (sameFormat && mipsSuit) return source;
            }

            try
            {
                if (IsAstc(target)) return ToAstc(path, source, target, mipLevels, out problem);
                return RunTexconv(path, target, mipLevels, out problem);
            }
            catch (Exception e)
            {
                problem = "The conversion failed: " + e.Message;
                return null;
            }
        }

        /* texconv reads everything and writes a DDS beside itself in a folder of our choosing. */
        private static byte[] RunTexconv(string path, TextureFormat target, int mipLevels, out string problem)
        {
            problem = null;

            DXGI_FORMAT? dxgi = DdsFile.DxgiFor(target);
            if (dxgi == null)
            {
                problem = target + " has no DDS equivalent, so nothing can be converted to it.";
                return null;
            }

            using (Scratch scratch = new Scratch())
            {
                string arguments = "-nologo -y -dx10"
                    + " -f " + TexconvName(dxgi.Value)
                    + " -m " + (mipLevels <= 0 ? 0 : mipLevels)
                    + " -o \"" + scratch.Directory + "\""
                    + " -- \"" + path + "\"";

                if (!Run(Texconv(), arguments, out string output))
                {
                    problem = "texconv could not convert '" + Path.GetFileName(path) + "' to " + target + ".\n\n" + Tail(output);
                    return null;
                }

                string written = Directory.GetFiles(scratch.Directory, "*.dds").FirstOrDefault();
                if (written == null)
                {
                    problem = "texconv reported success but wrote nothing.\n\n" + Tail(output);
                    return null;
                }
                return File.ReadAllBytes(written);
            }
        }

        /// <summary>
        /// How many levels a complete mip chain has, down to a single pixel.
        ///
        /// Note this isn't what a conversion necessarily produces: texconv keeps whatever chain an
        /// input DDS already carries rather than throwing away mips somebody authored by hand, and
        /// only builds one when the source has none. A shipped BC7 texture often stops at 4x4.
        /// </summary>
        public static int FullChain(int width, int height)
        {
            int levels = 1;
            for (int edge = Math.Max(width, height); edge > 1; edge /= 2) levels++;
            return levels;
        }

        /* texconv names a format the way DXGI does without the prefix - BC7_UNORM, B8G8R8A8_UNORM. */
        private static string TexconvName(DXGI_FORMAT format)
        {
            string name = format.ToString();
            return name.StartsWith("DXGI_FORMAT_") ? name.Substring("DXGI_FORMAT_".Length) : name;
        }

        #endregion

        #region MIP CHAINS

        /// <summary>
        /// The tail of a mip chain, starting <paramref name="drop"/> levels in. Null if the format's
        /// layout isn't known or there wouldn't be a level left.
        ///
        /// This is all a persistent copy is. Checked against the shipped data: of the 15,345
        /// textures carrying both a streamed and a persistent part, the persistent one is byte for
        /// byte the tail of the streamed chain in every single case - not a separately compressed
        /// image - so producing one is a matter of slicing, never of compressing again.
        ///
        /// Only for a plain 2D texture. A cubemap holds six chains one after another and never has a
        /// persistent copy in the first place; a volume texture's levels shrink in depth too.
        /// </summary>
        public static TEX4.Texture Slice(TEX4.Texture part, TextureFormat format, int drop)
        {
            if (part?.Content == null || drop <= 0) return part;
            if (part.Depth > 1) return null;

            int levels = Math.Max(1, (int)part.MipLevels);
            if (drop >= levels) return null;

            int offset = DdsFile.ChainBytes(format, part.Width, part.Height, drop);
            if (offset <= 0 || offset >= part.Content.Length) return null;

            int width = DdsFile.MipSize(part.Width, drop);
            int height = DdsFile.MipSize(part.Height, drop);
            int length = DdsFile.ChainBytes(format, width, height, levels - drop);
            if (length <= 0 || offset + length > part.Content.Length) return null;

            byte[] content = new byte[length];
            Buffer.BlockCopy(part.Content, offset, content, 0, length);

            return new TEX4.Texture
            {
                Width = (short)width,
                Height = (short)height,
                Depth = part.Depth,
                MipLevels = (short)(levels - drop),
                Content = content,
            };
        }

        /// <summary>
        /// One level of a mip chain on its own, for previewing it. Null if it can't be isolated.
        /// Always a new texture - the one passed in is never touched.
        /// </summary>
        public static TEX4.Texture Level(TEX4.Texture part, TextureFormat format, int level)
        {
            if (part?.Content == null) return null;

            TEX4.Texture tail = level <= 0 ? part : Slice(part, format, level);
            if (tail?.Content == null) return null;

            int surface = DdsFile.SurfaceBytes(format, tail.Width, tail.Height);
            if (surface <= 0 || surface > tail.Content.Length) surface = tail.Content.Length;

            byte[] content = new byte[surface];
            Buffer.BlockCopy(tail.Content, 0, content, 0, surface);

            return new TEX4.Texture
            {
                Width = tail.Width,
                Height = tail.Height,
                Depth = tail.Depth,
                MipLevels = 1,
                Content = content,
            };
        }

        /// <summary>
        /// How many levels to drop so a persistent copy comes out at or under <paramref name="maxEdge"/>,
        /// always dropping at least one so it is genuinely smaller than the streamed copy.
        /// </summary>
        public static int DropForEdge(int width, int height, int maxEdge, int availableLevels)
        {
            int drop = 1;
            while (drop + 1 < availableLevels
                   && Math.Max(DdsFile.MipSize(width, drop), DdsFile.MipSize(height, drop)) > maxEdge)
                drop++;
            return Math.Min(drop, Math.Max(0, availableLevels - 1));
        }

        #endregion

        #region ASTC

        /* DirectXTex has no ASTC encoder, so this goes the long way round: texconv builds the mip
         * chain uncompressed, astcenc compresses each level on its own - it knows nothing about mip
         * chains - and the blocks are stitched back into one DDS. */
        private static byte[] ToAstc(string path, byte[] source, TextureFormat target, int mipLevels, out string problem)
        {
            problem = null;

            /* An .astc file is already a block payload; it just needs a DDS around it. Only usable
             * when it was compressed at the footprint being asked for. */
            if (IsAstcFile(source))
                return FromAstcFile(source, target, out problem);

            byte[] uncompressed = RunTexconv(path, TextureFormat.A8R8G8B8, mipLevels, out problem);
            if (uncompressed == null) return null;

            TEX4.Texture levels = DdsFile.Read(uncompressed, out TextureFormat _, out TextureStateFlag state, out TextureUsageFlag _);
            if (levels == null) { problem = "The uncompressed image texconv produced could not be read back."; return null; }

            if (state.HasFlag(TextureStateFlag.CUBE) || levels.Depth > 1)
            {
                problem = "ASTC conversion only handles a plain 2D texture - this one is a cubemap or a volume.";
                return null;
            }

            string block = AstcBlock(target);
            List<byte> blocks = new List<byte>(uncompressed.Length / 2);

            using (Scratch scratch = new Scratch())
            {
                int offset = 0;
                int width = levels.Width, height = levels.Height;

                for (int level = 0; level < Math.Max(1, (int)levels.MipLevels); level++)
                {
                    int size = width * height * 4;
                    if (offset + size > levels.Content.Length) break;

                    string image = Path.Combine(scratch.Directory, "level" + level + ".png");
                    SaveBgra(levels.Content, offset, width, height, image);

                    string compressed = Path.Combine(scratch.Directory, "level" + level + ".astc");
                    string arguments = "-cl \"" + image + "\" \"" + compressed + "\" " + block + " -medium -silent";

                    if (!Run(Astcenc(), arguments, out string output))
                    {
                        problem = "astcenc could not compress mip " + level + " to ASTC " + block + ".\n\n" + Tail(output);
                        return null;
                    }

                    byte[] written = File.ReadAllBytes(compressed);
                    if (written.Length <= AstcHeaderSize) { problem = "astcenc wrote an empty file for mip " + level + "."; return null; }
                    blocks.AddRange(written.Skip(AstcHeaderSize));

                    offset += size;
                    width = Math.Max(1, width / 2);
                    height = Math.Max(1, height / 2);
                }
            }

            TEX4 wrapper = new TEX4 { Format = target };
            return DdsFile.Write(wrapper, new TEX4.Texture
            {
                Width = levels.Width,
                Height = levels.Height,
                Depth = levels.Depth,
                MipLevels = levels.MipLevels,
                Content = blocks.ToArray(),
            });
        }

        /// <summary>
        /// Decompress an ASTC surface to a bitmap. Nothing else in OpenCAGE can read ASTC - the DDS
        /// decoder behind the previews has no support for it - so a mobile port's textures only show
        /// up at all by going back out through astcenc.
        /// </summary>
        public static Bitmap DecodeAstc(TEX4 texture, TEX4.Texture part)
        {
            if (texture == null || part?.Content == null || !IsAstc(texture.Format)) return null;
            if (part.Width <= 0 || part.Height <= 0) return null;

            try
            {
                using (Scratch scratch = new Scratch())
                {
                    /* Only the top level goes out - a mip chain has no place in an .astc file, and a
                     * preview wants the biggest one anyway. */
                    string compressed = Path.Combine(scratch.Directory, "texture.astc");
                    File.WriteAllBytes(compressed, WrapAstc(part.Content, TopLevelSize(texture.Format, part.Width, part.Height),
                                                            texture.Format, part.Width, part.Height));

                    string image = Path.Combine(scratch.Directory, "texture.png");
                    if (!Run(Astcenc(), "-dl \"" + compressed + "\" \"" + image + "\" -silent", out string _)) return null;
                    if (!File.Exists(image)) return null;

                    //load through a copy so the file can be deleted with the scratch folder
                    using (Bitmap loaded = new Bitmap(image))
                        return new Bitmap(loaded);
                }
            }
            catch { return null; }
        }

        private const int AstcHeaderSize = 16;
        private static readonly byte[] AstcMagic = { 0x13, 0xAB, 0xA1, 0x5C };

        private static bool IsAstcFile(byte[] content)
        {
            return content != null && content.Length > AstcHeaderSize
                && content[0] == AstcMagic[0] && content[1] == AstcMagic[1]
                && content[2] == AstcMagic[2] && content[3] == AstcMagic[3];
        }

        /* An .astc file the user picked themselves: read its footprint and size out of the header
         * and wrap the blocks in a DDS, provided it matches the format they asked for. */
        private static byte[] FromAstcFile(byte[] content, TextureFormat target, out string problem)
        {
            problem = null;

            int blockX = content[4], blockY = content[5], blockZ = content[6];
            int width = content[7] | (content[8] << 8) | (content[9] << 16);
            int height = content[10] | (content[11] << 8) | (content[12] << 16);
            int depth = content[13] | (content[14] << 8) | (content[15] << 16);

            string footprint = blockX + "x" + blockY;
            if (blockZ != 1 || depth != 1)
            {
                problem = "This ASTC file is a 3D texture, which CATHODE has no format for.";
                return null;
            }
            if (footprint != AstcBlock(target))
            {
                problem = "This file is ASTC " + footprint + ", but " + AstcBlock(target) + " was asked for. "
                        + "Choose the matching format, or import an ordinary image and let OpenCAGE compress it.";
                return null;
            }

            TEX4 wrapper = new TEX4 { Format = target };
            return DdsFile.Write(wrapper, new TEX4.Texture
            {
                Width = (short)width,
                Height = (short)height,
                Depth = 1,
                MipLevels = 1,
                Content = content.Skip(AstcHeaderSize).ToArray(),
            });
        }

        private static byte[] WrapAstc(byte[] blocks, int length, TextureFormat format, int width, int height)
        {
            string[] footprint = AstcBlock(format).Split('x');
            byte[] file = new byte[AstcHeaderSize + length];

            Buffer.BlockCopy(AstcMagic, 0, file, 0, 4);
            file[4] = byte.Parse(footprint[0]);
            file[5] = byte.Parse(footprint[1]);
            file[6] = 1;
            file[7] = (byte)width; file[8] = (byte)(width >> 8); file[9] = (byte)(width >> 16);
            file[10] = (byte)height; file[11] = (byte)(height >> 8); file[12] = (byte)(height >> 16);
            file[13] = 1; file[14] = 0; file[15] = 0;

            Buffer.BlockCopy(blocks, 0, file, AstcHeaderSize, Math.Min(length, blocks.Length));
            return file;
        }

        /// <summary>How many bytes one ASTC surface takes - 16 per block, however many blocks it takes to cover it.</summary>
        private static int TopLevelSize(TextureFormat format, int width, int height)
        {
            string[] footprint = AstcBlock(format).Split('x');
            int blockX = int.Parse(footprint[0]), blockY = int.Parse(footprint[1]);
            return ((width + blockX - 1) / blockX) * ((height + blockY - 1) / blockY) * 16;
        }

        /* texconv writes B8G8R8A8, which is the order GDI+ keeps 32-bit pixels in, so a level can go
         * straight into a bitmap without shuffling channels. */
        private static void SaveBgra(byte[] content, int offset, int width, int height, string path)
        {
            using (Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                BitmapData locked = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    for (int row = 0; row < height; row++)
                        Marshal.Copy(content, offset + (row * width * 4), locked.Scan0 + (row * locked.Stride), width * 4);
                }
                finally { bitmap.UnlockBits(locked); }

                bitmap.Save(path, ImageFormat.Png);
            }
        }

        #endregion

        #region TOOLS

        private static string Texconv() { return NativeAssets.Unpack("tools", "texconv.exe"); }
        private static string Astcenc() { return NativeAssets.Unpack("tools", "astcenc.exe"); }

        /* Run one of the tools with no window, collecting everything it says so a failure can be
         * reported with the tool's own words rather than just an exit code. */
        private static bool Run(string tool, string arguments, out string output)
        {
            StringBuilder said = new StringBuilder();

            using (Process process = new Process())
            {
                process.StartInfo = new ProcessStartInfo(tool, arguments)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = Path.GetDirectoryName(tool),
                };
                process.OutputDataReceived += (s, e) => { if (e.Data != null) lock (said) said.AppendLine(e.Data); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (said) said.AppendLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(ToolTimeout))
                {
                    try { process.Kill(); } catch { }
                    output = Path.GetFileName(tool) + " did not finish within " + (ToolTimeout / 60000) + " minutes.";
                    return false;
                }

                lock (said) output = said.ToString().Trim();
                return process.ExitCode == 0;
            }
        }

        /* The tools say a lot on the way past; only the end of it explains a failure. */
        private static string Tail(string output)
        {
            if (string.IsNullOrEmpty(output)) return "(the tool said nothing)";

            string[] lines = output.Split('\n');
            return string.Join("\n", lines.Skip(Math.Max(0, lines.Length - 6)).Select(x => x.TrimEnd()));
        }

        /* A folder of our own to hand the tools, removed whatever happens. */
        private sealed class Scratch : IDisposable
        {
            public readonly string Directory;

            public Scratch()
            {
                Directory = Path.Combine(Path.GetTempPath(), "OpenCAGE-texture-" + Guid.NewGuid().ToString("N"));
                System.IO.Directory.CreateDirectory(Directory);
            }

            public void Dispose()
            {
                try { System.IO.Directory.Delete(Directory, true); } catch { }
            }
        }

        #endregion
    }
}

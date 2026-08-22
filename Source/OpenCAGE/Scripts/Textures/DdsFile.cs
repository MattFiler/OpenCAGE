using CATHODE;
using CathodeLib;
using DirectXTex;
using System;
using System.Collections.Generic;
using System.IO;
using static CATHODE.Textures;
using static DirectXTex.DirectXTexUtility;

namespace OpenCAGE.TextureTools
{
    /// <summary>
    /// The DDS wrapper around a texture's raw surface, in both directions.
    ///
    /// CATHODE stores a texture as a bare block of pixels plus its dimensions and format, which is
    /// exactly what sits after a DDS header - so getting one out of the other is a matter of writing
    /// or reading the header, never of touching the pixels. Everything here is written as the DX10
    /// extended header: the game's formats include several a legacy header can't name, and one shape
    /// on the way out is one shape to read back.
    ///
    /// Deliberately free of any UI, so the conversion path can be exercised outside the editor.
    /// </summary>
    public static class DdsFile
    {
        /// <summary>
        /// How a CATHODE texture format is written in a DX10 header. Null for the formats that have
        /// no DXGI equivalent at all, which is only CTX1 - an Xbox 360 format the PC game never uses.
        ///
        /// A8 and L8 both go out as A8_UNORM, so this doesn't round-trip on its own; reading is by
        /// <see cref="FormatFor"/>, and a caller that knows which of the two it wants says so.
        /// </summary>
        public static DXGI_FORMAT? DxgiFor(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.A32R32G32B32F: return DXGI_FORMAT.DXGI_FORMAT_R32G32B32A32_FLOAT;
                case TextureFormat.A16R16G16B16: return DXGI_FORMAT.DXGI_FORMAT_R16G16B16A16_UNORM;
                case TextureFormat.A8R8G8B8: return DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM;
                case TextureFormat.X8R8G8B8: return DXGI_FORMAT.DXGI_FORMAT_B8G8R8X8_UNORM;
                case TextureFormat.A8: return DXGI_FORMAT.DXGI_FORMAT_A8_UNORM;
                case TextureFormat.L8: return DXGI_FORMAT.DXGI_FORMAT_A8_UNORM;
                case TextureFormat.A4R4G4B4: return DXGI_FORMAT.DXGI_FORMAT_B4G4R4A4_UNORM;
                case TextureFormat.DXT1: return DXGI_FORMAT.DXGI_FORMAT_BC1_UNORM;
                case TextureFormat.DXT3: return DXGI_FORMAT.DXGI_FORMAT_BC2_UNORM;
                case TextureFormat.DXT5: return DXGI_FORMAT.DXGI_FORMAT_BC3_UNORM;
                case TextureFormat.DXN: return DXGI_FORMAT.DXGI_FORMAT_BC5_UNORM;
                case TextureFormat.BC6H: return DXGI_FORMAT.DXGI_FORMAT_BC6H_UF16;
                case TextureFormat.BC7: return DXGI_FORMAT.DXGI_FORMAT_BC7_UNORM;
                case TextureFormat.R16F: return DXGI_FORMAT.DXGI_FORMAT_R16_FLOAT;
                case TextureFormat.ASTC4X4: return DXGI_FORMAT.DXGI_FORMAT_ASTC_4X4_UNORM;
                case TextureFormat.ASTC8X8: return DXGI_FORMAT.DXGI_FORMAT_ASTC_8X8_UNORM;
                case TextureFormat.ASTC12X12: return DXGI_FORMAT.DXGI_FORMAT_ASTC_12X12_UNORM;
                default: return null;
            }
        }

        /// <summary>
        /// Which CATHODE format a DX10 header describes, or null for one the game has no place for.
        /// The typeless and sRGB spellings of the ASTC formats are read as the plain ones - CATHODE
        /// has a single format for each block size and carries sRGB as a state flag of its own.
        /// </summary>
        public static TextureFormat? FormatFor(DXGI_FORMAT dxgi)
        {
            switch (dxgi)
            {
                case DXGI_FORMAT.DXGI_FORMAT_R32G32B32A32_FLOAT: return TextureFormat.A32R32G32B32F;
                case DXGI_FORMAT.DXGI_FORMAT_R16G16B16A16_UNORM: return TextureFormat.A16R16G16B16;
                case DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM: return TextureFormat.A8R8G8B8;
                case DXGI_FORMAT.DXGI_FORMAT_B8G8R8X8_UNORM: return TextureFormat.X8R8G8B8;
                case DXGI_FORMAT.DXGI_FORMAT_A8_UNORM: return TextureFormat.A8; //A8 and L8 share this
                case DXGI_FORMAT.DXGI_FORMAT_B4G4R4A4_UNORM: return TextureFormat.A4R4G4B4;
                case DXGI_FORMAT.DXGI_FORMAT_BC1_UNORM: return TextureFormat.DXT1;
                case DXGI_FORMAT.DXGI_FORMAT_BC2_UNORM: return TextureFormat.DXT3;
                case DXGI_FORMAT.DXGI_FORMAT_BC3_UNORM: return TextureFormat.DXT5;
                case DXGI_FORMAT.DXGI_FORMAT_BC5_UNORM: return TextureFormat.DXN;
                case DXGI_FORMAT.DXGI_FORMAT_BC6H_UF16: return TextureFormat.BC6H;
                case DXGI_FORMAT.DXGI_FORMAT_BC7_UNORM: return TextureFormat.BC7;
                case DXGI_FORMAT.DXGI_FORMAT_R16_FLOAT: return TextureFormat.R16F;

                case DXGI_FORMAT.DXGI_FORMAT_ASTC_4X4_UNORM:
                case DXGI_FORMAT.DXGI_FORMAT_ASTC_4X4_UNORM_SRGB:
                case DXGI_FORMAT.DXGI_FORMAT_ASTC_4X4_TYPELESS: return TextureFormat.ASTC4X4;

                case DXGI_FORMAT.DXGI_FORMAT_ASTC_8X8_UNORM:
                case DXGI_FORMAT.DXGI_FORMAT_ASTC_8X8_UNORM_SRGB:
                case DXGI_FORMAT.DXGI_FORMAT_ASTC_8X8_TYPELESS: return TextureFormat.ASTC8X8;

                case DXGI_FORMAT.DXGI_FORMAT_ASTC_12X12_UNORM:
                case DXGI_FORMAT.DXGI_FORMAT_ASTC_12X12_UNORM_SRGB:
                case DXGI_FORMAT.DXGI_FORMAT_ASTC_12X12_TYPELESS: return TextureFormat.ASTC12X12;

                default: return null;
            }
        }

        /// <summary>
        /// How many bytes one surface of a format takes at a given size. Block formats round up to
        /// whole blocks; the rest are a flat number of bytes per pixel. Zero for a format with no
        /// known layout, which callers should treat as "can't measure this".
        /// </summary>
        public static int SurfaceBytes(TextureFormat format, int width, int height)
        {
            if (width <= 0 || height <= 0) return 0;

            int blockWidth = 1, blockHeight = 1, blockBytes;
            switch (format)
            {
                case TextureFormat.A32R32G32B32F: blockBytes = 16; break;
                case TextureFormat.A16R16G16B16: blockBytes = 8; break;
                case TextureFormat.A8R8G8B8:
                case TextureFormat.X8R8G8B8: blockBytes = 4; break;
                case TextureFormat.A4R4G4B4:
                case TextureFormat.R16F: blockBytes = 2; break;
                case TextureFormat.A8:
                case TextureFormat.L8: blockBytes = 1; break;

                case TextureFormat.DXT1: blockWidth = blockHeight = 4; blockBytes = 8; break;
                case TextureFormat.DXT3:
                case TextureFormat.DXT5:
                case TextureFormat.DXN:
                case TextureFormat.BC6H:
                case TextureFormat.BC7: blockWidth = blockHeight = 4; blockBytes = 16; break;

                case TextureFormat.ASTC4X4: blockWidth = blockHeight = 4; blockBytes = 16; break;
                case TextureFormat.ASTC8X8: blockWidth = blockHeight = 8; blockBytes = 16; break;
                case TextureFormat.ASTC12X12: blockWidth = blockHeight = 12; blockBytes = 16; break;

                default: return 0;
            }

            return ((width + blockWidth - 1) / blockWidth) * ((height + blockHeight - 1) / blockHeight) * blockBytes;
        }

        /// <summary>
        /// How many bytes the first <paramref name="levels"/> mips of a chain take, which is also
        /// where the level after them starts. Zero if the format has no known layout.
        /// </summary>
        public static int ChainBytes(TextureFormat format, int width, int height, int levels)
        {
            int total = 0;
            for (int level = 0; level < levels; level++)
            {
                int surface = SurfaceBytes(format, MipSize(width, level), MipSize(height, level));
                if (surface == 0) return 0;
                total += surface;
            }
            return total;
        }

        /// <summary>One edge of a mip level - halved per level, never smaller than a pixel.</summary>
        public static int MipSize(int edge, int level)
        {
            for (int i = 0; i < level; i++) edge = Math.Max(1, edge / 2);
            return Math.Max(1, edge);
        }

        /// <summary>Every format that can be written as a DDS, in the order the enum declares them.</summary>
        public static IEnumerable<TextureFormat> WritableFormats()
        {
            foreach (TextureFormat format in Enum.GetValues(typeof(TextureFormat)))
                if (format != TextureFormat.AUTO && DxgiFor(format) != null) yield return format;
        }

        /// <summary>
        /// Wrap a texture's surface in a DDS header. Null if the format has no DXGI equivalent or
        /// the part carries no pixels.
        /// </summary>
        public static byte[] Write(TEX4 texture, TEX4.Texture part)
        {
            if (texture == null || part?.Content == null) return null;

            DXGI_FORMAT? dxgi = DxgiFor(texture.Format);
            if (dxgi == null) return null;

            DDSHeader header = new DDSHeader
            {
                mHeight = (uint)part.Height,
                mWidth = (uint)part.Width,
                mDepth = (uint)part.Depth,
                mMipMapCount = (uint)part.MipLevels,
                mCaps1 = DDSCaps.DDSCAPS_TEXTURE,
            };
            DX10Header dx10 = new DX10Header
            {
                mDXGIFormat = dxgi.Value,
                mArraySize = 1,
                mResourceDimension = part.Depth > 1
                    ? D3D10_RESOURCE_DIMENSION.D3D10_RESOURCE_DIMENSION_TEXTURE3D
                    : D3D10_RESOURCE_DIMENSION.D3D10_RESOURCE_DIMENSION_TEXTURE2D,
            };

            if (header.mDepth > 1)
            {
                header.mFlags |= DDSFlags.DDSD_DEPTH;
                header.mCaps1 |= DDSCaps.DDSCAPS_COMPLEX;
                header.mCaps2 |= DDSCaps2.DDSCAPS2_VOLUME;
            }
            if (header.mMipMapCount > 0)
            {
                header.mFlags |= DDSFlags.DDSD_MIPMAPCOUNT;
                header.mCaps1 |= DDSCaps.DDSCAPS_COMPLEX;
            }
            if (texture.StateFlags.HasFlag(TextureStateFlag.CUBE))
            {
                header.mCaps2 |= DDSCaps2.DDSCAPS2_FULLCUBEMAP;
                dx10.mMiscFlags |= DDSMiscFlag.DDS_RESOURCE_MISC_TEXTURECUBE;
            }

            MemoryStream stream = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(new char[4] { 'D', 'D', 'S', ' ' });
                Utilities.Write(writer, header);
                Utilities.Write(writer, dx10);
                writer.Write(part.Content);
            }
            return stream.ToArray();
        }

        /// <summary>
        /// Read a DDS back into a texture surface. Null when it carries no DX10 header, or one
        /// naming a format the game has no place for - a legacy DDS has to be converted first, which
        /// is what <see cref="TextureConverter"/> is for.
        /// </summary>
        public static TEX4.Texture Read(byte[] content, out TextureFormat format, out TextureStateFlag state, out TextureUsageFlag usage)
        {
            TEX4.Texture part = new TEX4.Texture();
            format = TextureFormat.AUTO;
            state = TextureStateFlag.ALLOW_SRGB;
            usage = TextureUsageFlag.DEFAULT | TextureUsageFlag.IS_LEVEL_PACK;

            if (content == null || content.Length < 4) return null;

            using (MemoryStream stream = new MemoryStream(content))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                reader.BaseStream.Position += 4;
                DDSHeader header = Utilities.Consume<DDSHeader>(reader);

                if (header.mPixelFormat.mFlags != DDSPixelFormat.DDPF_FOURCC) return null;
                if (header.mPixelFormat.mFourCC[0] != 'D' || header.mPixelFormat.mFourCC[1] != 'X'
                 || header.mPixelFormat.mFourCC[2] != '1' || header.mPixelFormat.mFourCC[3] != '0') return null;

                DX10Header dx10 = Utilities.Consume<DX10Header>(reader);

                TextureFormat? read = FormatFor(dx10.mDXGIFormat);
                if (read == null) return null;
                format = read.Value;

                if (header.mCaps2.HasFlag(DDSCaps2.DDSCAPS2_CUBEMAP)) state |= TextureStateFlag.CUBE;
                if (header.mCaps2.HasFlag(DDSCaps2.DDSCAPS2_VOLUME)) state |= TextureStateFlag.VOLUME;
                if (header.mPixelFormat.mFlags.HasFlag(DDSPixelFormat.DDPF_ALPHAPIXELS)) state |= TextureStateFlag.NON_SOLID;

                part.Depth = (short)header.mDepth;
                part.MipLevels = (short)header.mMipMapCount;
                part.Width = (short)header.mWidth;
                part.Height = (short)header.mHeight;
                part.Content = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));
            }
            return part;
        }

        /// <summary>
        /// What a DDS says it holds, without reading the pixels - enough to decide whether it can be
        /// used as it stands or has to go through a converter. False for anything that isn't a DDS.
        /// </summary>
        public static bool Describe(byte[] content, out DXGI_FORMAT dxgi, out int width, out int height, out int mips, out bool cube)
        {
            dxgi = DXGI_FORMAT.DXGI_FORMAT_UNKNOWN;
            width = height = mips = 0;
            cube = false;

            if (content == null || content.Length < 128) return false;
            if (content[0] != 'D' || content[1] != 'D' || content[2] != 'S' || content[3] != ' ') return false;

            using (MemoryStream stream = new MemoryStream(content))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                reader.BaseStream.Position += 4;
                DDSHeader header = Utilities.Consume<DDSHeader>(reader);

                width = (int)header.mWidth;
                height = (int)header.mHeight;
                mips = (int)header.mMipMapCount;
                cube = header.mCaps2.HasFlag(DDSCaps2.DDSCAPS2_CUBEMAP);

                bool dx10Header = header.mPixelFormat.mFlags == DDSPixelFormat.DDPF_FOURCC
                    && header.mPixelFormat.mFourCC[0] == 'D' && header.mPixelFormat.mFourCC[1] == 'X'
                    && header.mPixelFormat.mFourCC[2] == '1' && header.mPixelFormat.mFourCC[3] == '0';
                if (dx10Header) dxgi = Utilities.Consume<DX10Header>(reader).mDXGIFormat;
            }
            return true;
        }
    }
}

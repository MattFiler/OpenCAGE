using Assimp;
using Assimp.Configs;
using CATHODE;
using CathodeLib;
using DirectXTex;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using static CATHODE.Materials.Material;
using static CATHODE.Models;
using static CATHODE.Models.CS2.Component.LOD;
using static CATHODE.Textures;
using static DirectXTex.DirectXTexUtility;
using Color = System.Windows.Media.Color;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Vector3D = System.Windows.Media.Media3D.Vector3D;

namespace AlienPAK
{
    public static class CathodeLibExtensions
    {
        /* Convert a TEX4 to DDS */
        public static byte[] ToDDS(this Textures.TEX4 texture)
        {
            if (texture == null) return null;
            Textures.TEX4.Texture part = texture.TextureStreamed?.Content != null ? texture.TextureStreamed
                : texture.TexturePersistent?.Content != null ? texture.TexturePersistent : null;
            return ToDDSFromPart(texture, part);
        }
        public static byte[] ToDDS(this Textures.TEX4 texture, Textures.TEX4.Texture part)
        {
            return ToDDSFromPart(texture, part);
        }

        private static byte[] ToDDSFromPart(Textures.TEX4 texture, Textures.TEX4.Texture part)
        {
            if (texture == null || part?.Content == null) return null;
            DDSHeader theDDSHeader = new DDSHeader();
            DX10Header theDX10Header = new DX10Header();
            MemoryStream ms = new MemoryStream();
            switch (texture.Format)
            {
                case Textures.TextureFormat.A32R32G32B32F:
                case Textures.TextureFormat.A16R16G16B16:
                case Textures.TextureFormat.A8R8G8B8:
                case Textures.TextureFormat.X8R8G8B8:
                case Textures.TextureFormat.A8:
                case Textures.TextureFormat.L8:
                case Textures.TextureFormat.A4R4G4B4:
                case Textures.TextureFormat.DXT1:
                case Textures.TextureFormat.DXT3:
                case Textures.TextureFormat.DXN:
                case Textures.TextureFormat.DXT5:
                case Textures.TextureFormat.BC6H:
                case Textures.TextureFormat.BC7:
                case Textures.TextureFormat.R16F:
                case Textures.TextureFormat.ASTC4X4:
                case Textures.TextureFormat.ASTC8X8:
                case Textures.TextureFormat.ASTC12X12:
                    theDDSHeader.mHeight = (uint)part.Height;
                    theDDSHeader.mWidth = (uint)part.Width;
                    theDDSHeader.mDepth = (uint)part.Depth;
                    theDDSHeader.mMipMapCount = (uint)part.MipLevels;

                    theDDSHeader.mCaps1 = DDSCaps.DDSCAPS_TEXTURE;
                    if (theDDSHeader.mDepth > 1) { theDDSHeader.mFlags |= DDSFlags.DDSD_DEPTH; theDDSHeader.mCaps1 |= DDSCaps.DDSCAPS_COMPLEX; theDDSHeader.mCaps2 |= DDSCaps2.DDSCAPS2_VOLUME; }
                    if (theDDSHeader.mMipMapCount > 0) { theDDSHeader.mFlags |= DDSFlags.DDSD_MIPMAPCOUNT; theDDSHeader.mCaps1 |= DDSCaps.DDSCAPS_COMPLEX; }
                    if (texture.StateFlags.HasFlag(TextureStateFlag.CUBE)) { theDDSHeader.mCaps2 |= DDSCaps2.DDSCAPS2_FULLCUBEMAP; theDX10Header.mMiscFlags |= DDSMiscFlag.DDS_RESOURCE_MISC_TEXTURECUBE; }
                    theDX10Header.mResourceDimension = part.Depth > 1 ? D3D10_RESOURCE_DIMENSION.D3D10_RESOURCE_DIMENSION_TEXTURE3D : D3D10_RESOURCE_DIMENSION.D3D10_RESOURCE_DIMENSION_TEXTURE2D;
                    theDX10Header.mArraySize = 1;

                    switch (texture.Format)
                    {
                        case Textures.TextureFormat.A32R32G32B32F: theDX10Header.mDXGIFormat = DXGI_FORMAT.DXGI_FORMAT_R32G32B32A32_FLOAT; break;
                        case Textures.TextureFormat.A16R16G16B16: theDX10Header.mDXGIFormat = DXGI_FORMAT.DXGI_FORMAT_R16G16B16A16_UNORM; break;
                        case Textures.TextureFormat.A8R8G8B8: theDX10Header.mDXGIFormat = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM; break;
                        case Textures.TextureFormat.X8R8G8B8: theDX10Header.mDXGIFormat = DXGI_FORMAT.DXGI_FORMAT_B8G8R8X8_UNORM; break;
                        case Textures.TextureFormat.A8:
                        case Textures.TextureFormat.L8: theDX10Header.mDXGIFormat = DXGI_FORMAT.DXGI_FORMAT_A8_UNORM; break;
                        case Textures.TextureFormat.A4R4G4B4: theDX10Header.mDXGIFormat = DXGI_FORMAT.DXGI_FORMAT_B4G4R4A4_UNORM; break;
                        case Textures.TextureFormat.DXT1: theDX10Header.mDXGIFormat = DXGI_FORMAT.DXGI_FORMAT_BC1_UNORM; break;
                        case Textures.TextureFormat.DXT3: theDX10Header.mDXGIFormat = DXGI_FORMAT.DXGI_FORMAT_BC2_UNORM; break;
                        case Textures.TextureFormat.DXN: theDX10Header.mDXGIFormat = DXGI_FORMAT.DXGI_FORMAT_BC5_UNORM; break;
                        case Textures.TextureFormat.DXT5: theDX10Header.mDXGIFormat = DXGI_FORMAT.DXGI_FORMAT_BC3_UNORM; break;
                        case Textures.TextureFormat.BC6H: theDX10Header.mDXGIFormat = DXGI_FORMAT.DXGI_FORMAT_BC6H_UF16; break;
                        case Textures.TextureFormat.BC7: theDX10Header.mDXGIFormat = DXGI_FORMAT.DXGI_FORMAT_BC7_UNORM; break;
                        case Textures.TextureFormat.R16F: theDX10Header.mDXGIFormat = DXGI_FORMAT.DXGI_FORMAT_R16_FLOAT; break;
                        case Textures.TextureFormat.ASTC4X4: theDX10Header.mDXGIFormat = DXGI_FORMAT.DXGI_FORMAT_ASTC_4X4_UNORM; break;
                        case Textures.TextureFormat.ASTC8X8: theDX10Header.mDXGIFormat = DXGI_FORMAT.DXGI_FORMAT_ASTC_8X8_UNORM; break;
                        case Textures.TextureFormat.ASTC12X12: theDX10Header.mDXGIFormat = DXGI_FORMAT.DXGI_FORMAT_ASTC_12X12_UNORM; break;

                        default:
                            throw new Exception("Unsupported");
                    }
                    using (BinaryWriter bw = new BinaryWriter(ms))
                    {
                        bw.Write(new char[4] { 'D', 'D', 'S', ' ' });
                        Utilities.Write(bw, theDDSHeader);
                        Utilities.Write(bw, theDX10Header);
                        bw.Write(part.Content);
                    }
                    break;

                default:
                    throw new Exception("Unsupported");
            }
            return ms.ToArray();
        }

        /* Convert DDS to a TEX4 Part */
        public static Textures.TEX4.Texture ToTEX4Part(this byte[] content, out Textures.TextureFormat format, out Textures.TextureStateFlag state, out Textures.TextureUsageFlag usage)
        {
            Textures.TEX4.Texture part = new TEX4.Texture();
            format = TextureFormat.AUTO;
            state = TextureStateFlag.ALLOW_SRGB;
            usage = TextureUsageFlag.DEFAULT | TextureUsageFlag.IS_LEVEL_PACK;

            using (MemoryStream stream = new MemoryStream(content))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                reader.BaseStream.Position += 4;
                DDSHeader ddsHeader = Utilities.Consume<DDSHeader>(reader);
                DX10Header dx10Header = null;
                if (ddsHeader.mPixelFormat.mFlags == DDSPixelFormat.DDPF_FOURCC &&
                    ddsHeader.mPixelFormat.mFourCC[0] == 'D' && ddsHeader.mPixelFormat.mFourCC[1] == 'X' && ddsHeader.mPixelFormat.mFourCC[2] == '1' && ddsHeader.mPixelFormat.mFourCC[3] == '0')
                    dx10Header = Utilities.Consume<DX10Header>(reader);

                if (dx10Header == null)
                    return null;

                if (ddsHeader.mCaps2.HasFlag(DDSCaps2.DDSCAPS2_CUBEMAP))
                    state |= TextureStateFlag.CUBE;
                if (ddsHeader.mCaps2.HasFlag(DDSCaps2.DDSCAPS2_VOLUME))
                    state |= TextureStateFlag.VOLUME;
                if (ddsHeader.mPixelFormat.mFlags.HasFlag(DDSPixelFormat.DDPF_ALPHAPIXELS))
                    state |= TextureStateFlag.NON_SOLID;

                part.Depth = (short)ddsHeader.mDepth;
                part.MipLevels = (short)ddsHeader.mMipMapCount;
                part.Width = (short)ddsHeader.mWidth;
                part.Height = (short)ddsHeader.mHeight;
                part.Content = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));

                switch (dx10Header.mDXGIFormat)
                {
                    case DXGI_FORMAT.DXGI_FORMAT_R32G32B32A32_FLOAT:
                        format = Textures.TextureFormat.A32R32G32B32F;
                        break;
                    case DXGI_FORMAT.DXGI_FORMAT_R16G16B16A16_UNORM:
                        format = Textures.TextureFormat.A16R16G16B16;
                        break;
                    case DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM:
                        format = Textures.TextureFormat.A8R8G8B8;
                        break;
                    case DXGI_FORMAT.DXGI_FORMAT_B8G8R8X8_UNORM:
                        format = Textures.TextureFormat.X8R8G8B8;
                        break;
                    case DXGI_FORMAT.DXGI_FORMAT_A8_UNORM:
                        format = Textures.TextureFormat.A8; //A8 and L8 both map to A8_UNORM
                        break;
                    case DXGI_FORMAT.DXGI_FORMAT_B4G4R4A4_UNORM:
                        format = Textures.TextureFormat.A4R4G4B4;
                        break;
                    case DXGI_FORMAT.DXGI_FORMAT_BC1_UNORM:
                        format = Textures.TextureFormat.DXT1;
                        break;
                    case DXGI_FORMAT.DXGI_FORMAT_BC2_UNORM:
                        format = Textures.TextureFormat.DXT3;
                        break;
                    case DXGI_FORMAT.DXGI_FORMAT_BC5_UNORM:
                        format = Textures.TextureFormat.DXN;
                        break;
                    case DXGI_FORMAT.DXGI_FORMAT_BC3_UNORM:
                        format = Textures.TextureFormat.DXT5;
                        break;
                    case DXGI_FORMAT.DXGI_FORMAT_BC6H_UF16:
                        format = Textures.TextureFormat.BC6H;
                        break;
                    case DXGI_FORMAT.DXGI_FORMAT_BC7_UNORM:
                        format = Textures.TextureFormat.BC7;
                        break;
                    case DXGI_FORMAT.DXGI_FORMAT_R16_FLOAT:
                        format = Textures.TextureFormat.R16F;
                        break;
                    case DXGI_FORMAT.DXGI_FORMAT_ASTC_4X4_UNORM:
                    case DXGI_FORMAT.DXGI_FORMAT_ASTC_4X4_UNORM_SRGB:
                    case DXGI_FORMAT.DXGI_FORMAT_ASTC_4X4_TYPELESS:
                        format = Textures.TextureFormat.ASTC4X4;
                        break;
                    case DXGI_FORMAT.DXGI_FORMAT_ASTC_8X8_UNORM:
                    case DXGI_FORMAT.DXGI_FORMAT_ASTC_8X8_UNORM_SRGB:
                    case DXGI_FORMAT.DXGI_FORMAT_ASTC_8X8_TYPELESS:
                        format = Textures.TextureFormat.ASTC8X8;
                        break;
                    case DXGI_FORMAT.DXGI_FORMAT_ASTC_12X12_UNORM:
                    case DXGI_FORMAT.DXGI_FORMAT_ASTC_12X12_UNORM_SRGB:
                    case DXGI_FORMAT.DXGI_FORMAT_ASTC_12X12_TYPELESS:
                        format = Textures.TextureFormat.ASTC12X12;
                        break;
                    default:
                        return null;
                }
            }
            return part;
        }

        /* Convert a TEX4 to Bitmap */
        public static Bitmap ToBitmap(this Textures.TEX4 texture)
        {
            byte[] content = texture?.ToDDS();
            return content?.ToBitmap();
        }
        public static Bitmap ToBitmap(this Textures.TEX4 texture, Textures.TEX4.Texture part)
        {
            if (texture == null) return null;
            byte[] content = texture.ToDDS(part);
            return content?.ToBitmap();
        }
        public static Bitmap ToBitmap(this byte[] content)
        {
            if (content == null) return null;
            try
            {
                TryParseDdsCubemap(content, out uint width, out uint height, out uint mipCount, out DXGI_FORMAT dxgiFormat, out int pixelDataOffset, out bool isCubemap);
                if (isCubemap)
                {
                    return ToBitmapCubemapStrip(content, width, height, mipCount, dxgiFormat, pixelDataOffset);
                }
                else
                {
                    return DecodeDdsToBitmap(content);
                }
            }
            catch
            {
                return null;
            }
        }

        /* Parse DDS header to get dimensions, format, and whether it is a cubemap. Returns true if valid DX10 DDS. */
        private static bool TryParseDdsCubemap(byte[] content, out uint width, out uint height, out uint mipCount, out DXGI_FORMAT dxgiFormat, out int pixelDataOffset, out bool isCubemap)
        {
            width = height = mipCount = 0;
            dxgiFormat = 0;
            pixelDataOffset = 0;
            isCubemap = false;
            if (content == null || content.Length < 148) return false;
            try
            {
                using (var ms = new MemoryStream(content))
                using (var reader = new BinaryReader(ms))
                {
                    if (reader.ReadUInt32() != 0x20534444) return false; // "DDS "
                    DDSHeader ddsHeader = Utilities.Consume<DDSHeader>(reader);
                    if (ddsHeader.mPixelFormat.mFourCC[0] != 'D' || ddsHeader.mPixelFormat.mFourCC[1] != 'X' ||
                        ddsHeader.mPixelFormat.mFourCC[2] != '1' || ddsHeader.mPixelFormat.mFourCC[3] != '0')
                        return false;
                    DX10Header dx10Header = Utilities.Consume<DX10Header>(reader);
                    pixelDataOffset = (int)reader.BaseStream.Position;
                    width = ddsHeader.mWidth;
                    height = ddsHeader.mHeight;
                    mipCount = ddsHeader.mMipMapCount > 0 ? ddsHeader.mMipMapCount : 1;
                    dxgiFormat = (DXGI_FORMAT)dx10Header.mDXGIFormat;
                    isCubemap = ddsHeader.mCaps2.HasFlag(DDSCaps2.DDSCAPS2_CUBEMAP);
                    return true;
                }
            }
            catch { return false; }
        }

        /* ASTC LDR: one 128-bit (16-byte) block per footprint blockW�blockH texels. */
        private static int GetAstcCompressedSurfaceSize(uint width, uint height, uint blockW, uint blockH)
        {
            uint blocksW = (width + blockW - 1) / blockW;
            uint blocksH = (height + blockH - 1) / blockH;
            return (int)(blocksW * blocksH * 16);
        }

        /* Work out the total bytes for one face of a 2D texture (all mip levels). */
        private static int GetDdsFaceSize(uint width, uint height, uint mipCount, DXGI_FORMAT format)
        {
            int total = 0;
            uint w = width, h = height;
            for (uint m = 0; m < mipCount; m++)
            {
                if (w == 0) w = 1;
                if (h == 0) h = 1;
                total += GetDdsSurfaceSize(w, h, format);
                w /= 2; h /= 2;
            }
            return total;
        }
        private static int GetDdsSurfaceSize(uint width, uint height, DXGI_FORMAT format)
        {
            switch (format)
            {
                case DXGI_FORMAT.DXGI_FORMAT_BC1_UNORM:
                case DXGI_FORMAT.DXGI_FORMAT_BC1_UNORM_SRGB:
                    return (int)(((width + 3) / 4) * ((height + 3) / 4) * 8);
                case DXGI_FORMAT.DXGI_FORMAT_BC2_UNORM:
                case DXGI_FORMAT.DXGI_FORMAT_BC2_UNORM_SRGB:
                case DXGI_FORMAT.DXGI_FORMAT_BC3_UNORM:
                case DXGI_FORMAT.DXGI_FORMAT_BC3_UNORM_SRGB:
                case DXGI_FORMAT.DXGI_FORMAT_BC5_UNORM:
                    return (int)(((width + 3) / 4) * ((height + 3) / 4) * 16);
                case DXGI_FORMAT.DXGI_FORMAT_BC6H_UF16:
                case DXGI_FORMAT.DXGI_FORMAT_BC7_UNORM:
                case DXGI_FORMAT.DXGI_FORMAT_BC7_UNORM_SRGB:
                    return (int)(((width + 3) / 4) * ((height + 3) / 4) * 16);
                case DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM:
                case DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM:
                    return (int)(width * height * 4);
                case DXGI_FORMAT.DXGI_FORMAT_B8G8R8X8_UNORM:
                    //case DXGI_FORMAT.DXGI_FORMAT_R8G8B8_UNORM:
                    return (int)(width * height * 4);
                case DXGI_FORMAT.DXGI_FORMAT_A8_UNORM:
                case DXGI_FORMAT.DXGI_FORMAT_R8_UNORM:
                    return (int)(width * height);
                case DXGI_FORMAT.DXGI_FORMAT_R16_FLOAT:
                    return (int)(width * height * 2);
                case DXGI_FORMAT.DXGI_FORMAT_R32G32B32A32_FLOAT:
                    return (int)(width * height * 16);
                case DXGI_FORMAT.DXGI_FORMAT_R16G16B16A16_UNORM:
                    return (int)(width * height * 8);
                case DXGI_FORMAT.DXGI_FORMAT_ASTC_4X4_UNORM:
                case DXGI_FORMAT.DXGI_FORMAT_ASTC_4X4_UNORM_SRGB:
                case DXGI_FORMAT.DXGI_FORMAT_ASTC_4X4_TYPELESS:
                    return GetAstcCompressedSurfaceSize(width, height, 4, 4);
                case DXGI_FORMAT.DXGI_FORMAT_ASTC_8X8_UNORM:
                case DXGI_FORMAT.DXGI_FORMAT_ASTC_8X8_UNORM_SRGB:
                case DXGI_FORMAT.DXGI_FORMAT_ASTC_8X8_TYPELESS:
                    return GetAstcCompressedSurfaceSize(width, height, 8, 8);
                case DXGI_FORMAT.DXGI_FORMAT_ASTC_12X12_UNORM:
                case DXGI_FORMAT.DXGI_FORMAT_ASTC_12X12_UNORM_SRGB:
                case DXGI_FORMAT.DXGI_FORMAT_ASTC_12X12_TYPELESS:
                    return GetAstcCompressedSurfaceSize(width, height, 12, 12);
                default:
                    return 0;
            }
        }

        /* Decode all 6 cubemap faces and stitch into a horizontal strip bitmap */
        private static Bitmap ToBitmapCubemapStrip(byte[] content, uint width, uint height, uint mipCount, DXGI_FORMAT dxgiFormat, int pixelDataOffset)
        {
            int faceSize = GetDdsFaceSize(width, height, mipCount, dxgiFormat);
            if (faceSize <= 0) return null;
            int totalFacesSize = faceSize * 6;
            if (pixelDataOffset + totalFacesSize > content.Length) return null;

            var faceBitmaps = new List<Bitmap>(6);
            try
            {
                for (int face = 0; face < 6; face++)
                {
                    byte[] faceData = new byte[faceSize];
                    Buffer.BlockCopy(content, pixelDataOffset + face * faceSize, faceData, 0, faceSize);
                    byte[] singleFaceDds = BuildSingleFaceDds((int)width, (int)height, (int)mipCount, dxgiFormat, faceData);
                    if (singleFaceDds == null) return null;
                    Bitmap bmp = DecodeDdsToBitmap(singleFaceDds);
                    if (bmp == null) return null;
                    faceBitmaps.Add(bmp);
                }
                int stripWidth = (int)width * 6;
                int stripHeight = (int)height;
                var strip = new Bitmap(stripWidth, stripHeight, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(strip))
                {
                    for (int i = 0; i < 6; i++)
                        g.DrawImage(faceBitmaps[i], i * (int)width, 0, (int)width, (int)height);
                }
                foreach (var b in faceBitmaps) b?.Dispose();
                return strip;
            }
            catch
            {
                foreach (var b in faceBitmaps) b?.Dispose();
                return null;
            }
        }

        /* Build a minimal DX10 DDS containing a single 2D face (no cubemap) */
        private static byte[] BuildSingleFaceDds(int width, int height, int mipCount, DXGI_FORMAT dxgiFormat, byte[] facePixelData)
        {
            var ddsHeader = new DDSHeader();
            ddsHeader.mWidth = (uint)width;
            ddsHeader.mHeight = (uint)height;
            ddsHeader.mDepth = 1;
            ddsHeader.mMipMapCount = (uint)mipCount;
            ddsHeader.mFlags = DDSFlags.DDSD_CAPS | DDSFlags.DDSD_HEIGHT | DDSFlags.DDSD_WIDTH | DDSFlags.DDSD_PIXELFORMAT;
            if (mipCount > 1) { ddsHeader.mFlags |= DDSFlags.DDSD_MIPMAPCOUNT; ddsHeader.mCaps1 |= DDSCaps.DDSCAPS_COMPLEX | DDSCaps.DDSCAPS_MIPMAP; }
            ddsHeader.mCaps1 |= DDSCaps.DDSCAPS_TEXTURE;
            ddsHeader.mCaps2 = 0;

            var dx10Header = new DX10Header();
            dx10Header.mDXGIFormat = dxgiFormat;
            dx10Header.mResourceDimension = D3D10_RESOURCE_DIMENSION.D3D10_RESOURCE_DIMENSION_TEXTURE2D;
            dx10Header.mMiscFlags = 0;
            dx10Header.mArraySize = 1;

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(new char[] { 'D', 'D', 'S', ' ' });
                Utilities.Write(bw, ddsHeader);
                Utilities.Write(bw, dx10Header);
                bw.Write(facePixelData);
                return ms.ToArray();
            }
        }

        /*  Decode DDS bytes to a Bitmap */
        private static Bitmap DecodeDdsToBitmap(byte[] ddsContent)
        {
            if (ddsContent == null) return null;
            try
            {
                using (var imageStream = new MemoryStream(ddsContent))
                using (var image = Pfim.Pfim.FromStream(imageStream))
                {
                    PixelFormat format = PixelFormat.DontCare;
                    switch (image.Format)
                    {
                        case Pfim.ImageFormat.Rgba32: format = PixelFormat.Format32bppArgb; break;
                        case Pfim.ImageFormat.Rgb24: format = PixelFormat.Format24bppRgb; break;
                        case Pfim.ImageFormat.Rgb8: format = PixelFormat.Format8bppIndexed; break;
                        default: return null;
                    }
                    if (format == PixelFormat.DontCare) return null;
                    Bitmap temp;
                    var handle = GCHandle.Alloc(image.Data, GCHandleType.Pinned);
                    try
                    {
                        var data = Marshal.UnsafeAddrOfPinnedArrayElement(image.Data, 0);
                        temp = new Bitmap(image.Width, image.Height, image.Stride, format, data);
                    }
                    finally { handle.Free(); }
                    using (temp)
                        return new Bitmap(temp);
                }
            }
            catch { return null; }
        }

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);

        /* Convert a Bitmap to ImageSource */
        public static ImageSource ToImageSource(this Bitmap bmp)
        {
            var handle = bmp.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            }
            finally { DeleteObject(handle); }
        }

        public static GeometryModel3D ToGeometryModel3D(this CS2.Component.LOD.Submesh submesh, bool applyMaterials = true)
        {
            if (submesh.Data.Length == 0)
                return new GeometryModel3D();

            cMesh cathodeMesh = ModelUtility.ToMesh(submesh);
            if (cathodeMesh.Vertices.Count == 0) return new GeometryModel3D();

            int[] indices = cathodeMesh.Indices.Select(x => (int)x).ToArray();
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                indices[i] = a; indices[i + 1] = c; indices[i + 2] = b;
            }

            Point3DCollection vertices = new Point3DCollection();
            PointCollection uvs = new PointCollection();
            for (int i = 0; i < cathodeMesh.Vertices.Count; i++)
            {
                vertices.Add(new Point3D((float)cathodeMesh.Vertices[i].X, (float)cathodeMesh.Vertices[i].Y, -(float)cathodeMesh.Vertices[i].Z));
            }
            for (int i = 0; i < cathodeMesh.UVs.Length; i++)
            {
                if (cathodeMesh.UVs[i] == null) continue;

                for (int x = 0; x < cathodeMesh.UVs[i].Count; x++)
                {
                    uvs.Add(new System.Windows.Point(cathodeMesh.UVs[i][x].X, cathodeMesh.UVs[i][x].Y));
                }
                break;
            }

            GeometryModel3D geometry = new GeometryModel3D()
            {
                Geometry = new MeshGeometry3D
                {
                    Positions = vertices,
                    TriangleIndices = new Int32Collection(indices),
                    TextureCoordinates = uvs,
                }
            };
            if (applyMaterials)
            {
                MaterialApplier.ApplyMaterial(geometry, submesh.Material);
            }
            else
            {
                geometry.Material = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(255, 255, 0)));
                geometry.BackMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(255, 255, 0)));
            }
            return geometry;
        }

        public static GeometryModel3D ToGeometryModel3D(this Assimp.Mesh mesh)
        {
            if (mesh == null || mesh.VertexCount == 0) return new GeometryModel3D();
            int[] indices = mesh.GetIndices();
            if (indices == null || indices.Length == 0) return new GeometryModel3D();
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                indices[i] = a; indices[i + 1] = c; indices[i + 2] = b;
            }
            var vertices = new Point3DCollection();
            for (int i = 0; i < mesh.Vertices.Count; i++)
            {
                var v = mesh.Vertices[i];
                vertices.Add(new Point3D((double)v.X, (double)v.Y, -(double)v.Z));
            }
            var uvs = new PointCollection();
            if (mesh.TextureCoordinateChannelCount > 0 && mesh.TextureCoordinateChannels[0].Count == mesh.VertexCount)
            {
                for (int i = 0; i < mesh.TextureCoordinateChannels[0].Count; i++)
                {
                    var uv = mesh.TextureCoordinateChannels[0][i];
                    uvs.Add(new System.Windows.Point((double)uv.X, (double)uv.Y));
                }
            }
            var geometry = new GeometryModel3D
            {
                Geometry = new MeshGeometry3D
                {
                    Positions = vertices,
                    TriangleIndices = new Int32Collection(indices),
                    TextureCoordinates = uvs,
                },
                Material = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(200, 200, 200))),
            };
            return geometry;
        }

        public static Assimp.Material ToAssimpMaterial(
            this Materials.Material cathodeMaterial,
            int materialIndex,
            string diffuseTextureFileName = null,
            string normalMapTextureFileName = null,
            string specularMapTextureFileName = null,
            string dirtMapTextureFileName = null,
            string secondarySpecularMapTextureFileName = null)
        {
            Assimp.Material mat = new Assimp.Material();
            if (cathodeMaterial == null) return mat;

            mat.Name = cathodeMaterial.Name;
            float r, g, b;
            MaterialApplier.GetDiffuseTintForExport(cathodeMaterial, out r, out g, out b);
            mat.ColorDiffuse = new Assimp.Color4D(r, g, b, 1.0f);
            AddAssimpTextureSlot(mat, diffuseTextureFileName, Assimp.TextureType.Diffuse, 0);
            AddAssimpTextureSlot(mat, normalMapTextureFileName, Assimp.TextureType.Normals, 0);
            AddAssimpTextureSlot(mat, specularMapTextureFileName, Assimp.TextureType.Specular, 0);
            AddAssimpTextureSlot(mat, dirtMapTextureFileName, Assimp.TextureType.Unknown, 0);
            AddAssimpTextureSlot(mat, secondarySpecularMapTextureFileName, Assimp.TextureType.Specular, 1);
            return mat;
        }

        private static void AddAssimpTextureSlot(Assimp.Material mat, string filePath, Assimp.TextureType textureType, int textureIndex)
        {
            if (string.IsNullOrEmpty(filePath))
                return;

            Assimp.TextureSlot slot = new Assimp.TextureSlot();
            slot.FilePath = filePath;
            slot.TextureType = textureType;
            slot.TextureIndex = textureIndex;
            mat.AddMaterialTexture(slot);
        }

        public static Mesh ToMesh(this CS2.Component.LOD.Submesh submesh, int materialIndex = 0)
        {
            cMesh cathodeMesh = ModelUtility.ToMesh(submesh);
            Mesh assimpMesh = new Mesh();
            assimpMesh.MaterialIndex = materialIndex;

            int[] indices = cathodeMesh.Indices.Select(x => (int)x).ToArray();
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                indices[i] = a; indices[i + 1] = c; indices[i + 2] = b;
            }
            if (!assimpMesh.SetIndices(indices, 3))
            {
                return assimpMesh;
            }

            for (int i = 0; i < cathodeMesh.Vertices.Count; i++)
            {
                assimpMesh.Vertices.Add(new Assimp.Vector3D((float)cathodeMesh.Vertices[i].X, (float)cathodeMesh.Vertices[i].Y, -(float)cathodeMesh.Vertices[i].Z));
            }
            for (int i = 0; i < cathodeMesh.Normals.Count; i++)
            {
                //assimpMesh.Normals.Add(new Assimp.Vector3D((float)cathodeMesh.Normals[i].X, (float)cathodeMesh.Normals[i].Y, (float)cathodeMesh.Normals[i].Z));
            }
            //binormals?
            for (int i = 0; i < cathodeMesh.Tangents.Count; i++)
            {
                assimpMesh.Tangents.Add(new Assimp.Vector3D((float)cathodeMesh.Tangents[i].X, (float)cathodeMesh.Tangents[i].Y, -(float)cathodeMesh.Tangents[i].Z));
            }
            int exportedUVs = 0;
            for (int i = 0; i < cathodeMesh.UVs.Length; i++)
            {
                if (cathodeMesh.UVs[i] == null) continue;

                for (int x = 0; x < cathodeMesh.UVs[i].Count; x++)
                {
                    assimpMesh.TextureCoordinateChannels[exportedUVs].Add(new Assimp.Vector3D((float)cathodeMesh.UVs[i][x].X, 1.0f - (float)cathodeMesh.UVs[i][x].Y, 0));
                }
                assimpMesh.HasTextureCoords(exportedUVs);
                assimpMesh.UVComponentCount[exportedUVs] = 2;
                exportedUVs++;
            }

            /*
            bool hasBoneData = cathodeMesh.BoneWeights.Count == cathodeMesh.Vertices.Count && cathodeMesh.BoneIndexes.Count == cathodeMesh.Vertices.Count;
            if (hasBoneData)
            {
                List<List<Tuple<float, int>>> vertexBoneWeights = new List<List<Tuple<float, int>>>(cathodeMesh.Vertices.Count);
                for (int v = 0; v < cathodeMesh.Vertices.Count; v++)
                    vertexBoneWeights.Add(new List<Tuple<float, int>>());
                for (int vertexIndex = 0; vertexIndex < cathodeMesh.Vertices.Count; vertexIndex++)
                {
                    Vector4 weights = cathodeMesh.BoneWeights[vertexIndex];
                    Vector4 indices = cathodeMesh.BoneIndexes[vertexIndex];

                    if (indices.X != 0 && weights.X != 0)
                        vertexBoneWeights[vertexIndex].Add(new Tuple<float, int>(weights.X, Convert.ToInt32(indices.X)));
                    if (indices.Y != 0 && weights.Y != 0)
                        vertexBoneWeights[vertexIndex].Add(new Tuple<float, int>(weights.Y, Convert.ToInt32(indices.Y)));
                    if (indices.Z != 0 && weights.Z != 0)
                        vertexBoneWeights[vertexIndex].Add(new Tuple<float, int>(weights.Z, Convert.ToInt32(indices.Z)));
                    if (indices.W != 0 && weights.W != 0)
                        vertexBoneWeights[vertexIndex].Add(new Tuple<float, int>(weights.W, Convert.ToInt32(indices.W)));
                }

                Dictionary<int, List<Assimp.VertexWeight>> boneVertexWeights = new Dictionary<int, List<Assimp.VertexWeight>>();
                for (int vertexIndex = 0; vertexIndex < vertexBoneWeights.Count; vertexIndex++)
                {
                    foreach (var entry in vertexBoneWeights[vertexIndex])
                    {
                        List<Assimp.VertexWeight> list;
                        if (!boneVertexWeights.TryGetValue(entry.Item2, out list))
                        {
                            list = new List<Assimp.VertexWeight>();
                            boneVertexWeights[entry.Item2] = list;
                        }
                        list.Add(new Assimp.VertexWeight(vertexIndex, entry.Item1));
                    }
                }

                foreach (var kvp in boneVertexWeights.OrderBy(x => x.Key))
                {
                    var bone = new Assimp.Bone();
                    bone.Name = "Bone_" + kvp.Key;
                    bone.VertexWeights.AddRange(kvp.Value);
                    assimpMesh.Bones.Add(bone);
                }
            }
            */

            return assimpMesh;
        }

        public static void ExportMesh(this Models.CS2 cs2, string filename)
        {
            string modelDir = Path.GetDirectoryName(filename);
            string modelBase = Path.GetFileNameWithoutExtension(filename);
            if (string.IsNullOrEmpty(modelBase)) modelBase = cs2.Name ?? "model";

            List<Materials.Material> materials = new List<Materials.Material>();
            Dictionary<Materials.Material, int> materialIndexes = new Dictionary<Materials.Material, int>();
            foreach (var component in cs2.Components)
            {
                foreach (var lod in component.LODs)
                {
                    foreach (var submesh in lod.Submeshes)
                    {
                        if (submesh.Material != null && !materialIndexes.ContainsKey(submesh.Material))
                        {
                            materialIndexes[submesh.Material] = materials.Count;
                            materials.Add(submesh.Material);
                        }
                    }
                }
            }

            string[] diffuseFileNames = new string[materials.Count];
            string[] normalMapFileNames = new string[materials.Count];
            string[] specularMapFileNames = new string[materials.Count];
            string[] dirtMapFileNames = new string[materials.Count];
            string[] secondarySpecularMapFileNames = new string[materials.Count];
            for (int i = 0; i < materials.Count; i++)
            {
                ExportModelSampler(MaterialApplier.GetDiffuseTexture(materials[i]), ref diffuseFileNames, i);
                ExportModelSampler(MaterialApplier.GetNormalMapTexture(materials[i]), ref normalMapFileNames, i);
                ExportModelSampler(MaterialApplier.GetSpecularMapTexture(materials[i]), ref specularMapFileNames, i);
                ExportModelSampler(MaterialApplier.GetDirtMapTexture(materials[i]), ref dirtMapFileNames, i);
                ExportModelSampler(MaterialApplier.GetSecondarySpecularMapTexture(materials[i]), ref secondarySpecularMapFileNames, i);
            }

            Scene scene = new Scene();
            for (int matIdx = 0; matIdx < materials.Count; matIdx++)
            {
                scene.Materials.Add(materials[matIdx].ToAssimpMaterial(
                    matIdx,
                    diffuseFileNames[matIdx],
                    normalMapFileNames[matIdx],
                    specularMapFileNames[matIdx],
                    dirtMapFileNames[matIdx],
                    secondarySpecularMapFileNames[matIdx]));
            }
            if (scene.Materials.Count == 0)
                scene.Materials.Add(new Assimp.Material());

            scene.RootNode = new Node(cs2.Name);
            for (int i = 0; i < cs2.Components.Count; i++)
            {
                Node componentNode = new Node(i.ToString());
                scene.RootNode.Children.Add(componentNode);
                for (int x = 0; x < cs2.Components[i].LODs.Count; x++)
                {
                    Node lodNode = new Node(cs2.Components[i].LODs[x].Name);
                    componentNode.Children.Add(lodNode);
                    for (int y = 0; y < cs2.Components[i].LODs[x].Submeshes.Count; y++)
                    {
                        Node submeshNode = new Node(y.ToString());
                        lodNode.Children.Add(submeshNode);
                        Materials.Material submeshMat = cs2.Components[i].LODs[x].Submeshes[y].Material;
                        int meshMatIndex = (submeshMat != null && materialIndexes.ContainsKey(submeshMat)) ? materialIndexes[submeshMat] : 0;
                        Mesh mesh = cs2.Components[i].LODs[x].Submeshes[y].ToMesh(meshMatIndex);
                        mesh.Name = cs2.Name + " [" + x + "] -> " + lodNode.Name + " [" + i + "]";
                        scene.Meshes.Add(mesh);
                        submeshNode.MeshIndices.Add(scene.Meshes.Count - 1);
                    }
                }
            }

            using (AssimpContext exp = new AssimpContext())
            {
                exp.ExportFile(scene, filename, Path.GetExtension(filename).TrimStart('.').ToLowerInvariant());
            }

            void ExportModelSampler(Textures.TEX4 texture, ref string[] filenames, int index)
            {
                if (texture == null) return;

                byte[] dds = texture.ToDDS();
                if (dds != null && dds.Length > 0)
                {
                    string file = Path.GetFileName(texture.Name);
                    string dir = modelDir + "/" + modelBase + " Textures/" + texture.Name.Substring(0, texture.Name.Length - file.Length);
                    filenames[index] = dir + file + ".dds";
                    Directory.CreateDirectory(dir);
                    File.WriteAllBytes(filenames[index], dds);
                }
            }
        }

        public static CS2.Component.LOD.Submesh ToSubmesh(this Mesh mesh, ushort? customScaleFactor = null)
        {
            //We can't have more vertices than Int16.MaxValue as we won't be able to point to them
            if (mesh.VertexCount > Int16.MaxValue) return null;

            //All faces must be triangulated
            foreach (Assimp.Face face in mesh.Faces) if (face.Indices.Count != 3) return null;

            //Mesh must have some content
            if (mesh.BoundingBox.Max == new Assimp.Vector3D(0, 0, 0)) return null;

            CS2.Component.LOD.Submesh submesh = new CS2.Component.LOD.Submesh();
            submesh.VertexCount = mesh.VertexCount;
            int[] indices = mesh.GetIndices();
            submesh.IndexCount = indices.Length;

            submesh.RenderFlags = RenderingFlag.IS_FIRST_PERSON_LOD | RenderingFlag.HAS_FIRST_PERSON_LOD | RenderingFlag.IS_THIRD_PERSON_LOD | RenderingFlag.HAS_THIRD_PERSON_LOD | RenderingFlag.IS_SHADOW_CASTING | RenderingFlag.HAS_SHADOW_CASTING | RenderingFlag.IS_LEVEL_PACK;

            //meshes must not exceed 1 unit in any direction -> TODO: we should validate customScaleFactor here...
            submesh.VertexScale = customScaleFactor == null ? mesh.CalculateScaleFactor() : (ushort)customScaleFactor;

            submesh.MaxBounds = new Vector3(mesh.BoundingBox.Max.X, mesh.BoundingBox.Max.Y, mesh.BoundingBox.Max.Z);
            submesh.MinBounds = new Vector3(mesh.BoundingBox.Min.X, mesh.BoundingBox.Min.Y, mesh.BoundingBox.Min.Z);

            submesh.MaxLODRange = 10000;
            submesh.MinLODRange = 0;

            submesh.VertexFormatFull = new VertexFormat();
            submesh.VertexFormatFull.Attributes.Add(new List<VertexFormat.Attribute>() { new VertexFormat.Attribute(VertexFormat.Type.S16_4N, VertexFormat.Usage.Position) });
            for (int i = 0; i < mesh.TextureCoordinateChannels.Length; i++)
                if (mesh.TextureCoordinateChannels[i].Count > 0)
                    submesh.VertexFormatFull.Attributes[0].Add(new VertexFormat.Attribute(VertexFormat.Type.S16_2N, VertexFormat.Usage.TexCoord, i));
            if (mesh.Normals.Count > 0)
                submesh.VertexFormatFull.Attributes.Add(new List<VertexFormat.Attribute>() { new VertexFormat.Attribute(VertexFormat.Type.FP32_3, VertexFormat.Usage.Normal) });
            submesh.VertexFormatFull.Attributes.Add(new List<VertexFormat.Attribute>() { new VertexFormat.Attribute(VertexFormat.Type.Unused) });

            //is this right?
            submesh.VertexFormatPartial = new VertexFormat();
            submesh.VertexFormatPartial.Attributes.Add(new List<VertexFormat.Attribute>() { new VertexFormat.Attribute(VertexFormat.Type.S16_4N, VertexFormat.Usage.Position) });
            for (int i = 0; i < mesh.TextureCoordinateChannels.Length; i++)
                if (mesh.TextureCoordinateChannels[i].Count > 0)
                    submesh.VertexFormatPartial.Attributes[0].Add(new VertexFormat.Attribute(VertexFormat.Type.S16_2N, VertexFormat.Usage.TexCoord, i));
            submesh.VertexFormatPartial.Attributes.Add(new List<VertexFormat.Attribute>() { new VertexFormat.Attribute(VertexFormat.Type.Unused) });

            MemoryStream ms = new MemoryStream();
            using (BinaryWriter reader = new BinaryWriter(ms))
            {
                for (int i = 0; i < submesh.VertexFormatFull.Attributes.Count; ++i)
                {
                    if (i == submesh.VertexFormatFull.Attributes.Count - 1)
                    {
                        for (int x = 0; x < indices.Length; x++)
                            reader.Write((UInt16)indices[x]);

                        Utilities.Align(reader, 16);
                        continue;
                    }

                    //TEMP!! This should be reworked to the new logic

                    for (int x = 0; x < submesh.VertexCount; ++x)
                    {
                        for (int y = 0; y < submesh.VertexFormatFull.Attributes[i].Count; ++y)
                        {
                            VertexFormat.Attribute format = submesh.VertexFormatFull.Attributes[i][y];
                            switch (format.Type)
                            {
                                case VertexFormat.Type.FP32_3:
                                    {
                                        switch (format.Usage)
                                        {
                                            case VertexFormat.Usage.Normal:
                                                reader.Write((float)mesh.Normals[x].X);
                                                reader.Write((float)mesh.Normals[x].Y);
                                                reader.Write((float)mesh.Normals[x].Z);
                                                break;
                                        }
                                        ;
                                        break;
                                    }
                                case VertexFormat.Type.S16_2N:
                                    {
                                        switch (format.Usage)
                                        {
                                            case VertexFormat.Usage.TexCoord:
                                                Vector2 v = new Vector2(mesh.TextureCoordinateChannels[format.Index][x].X, mesh.TextureCoordinateChannels[format.Index][x].Y);
                                                v *= 2048.0f;
                                                reader.Write((Int16)v.X);
                                                reader.Write((Int16)v.Y);
                                                break;
                                        }
                                        break;
                                    }
                                case VertexFormat.Type.S16_4N:
                                    {
                                        switch (format.Usage)
                                        {
                                            case VertexFormat.Usage.Position:
                                                Vector4 v = new Vector4(mesh.Vertices[x].X, mesh.Vertices[x].Y, mesh.Vertices[x].Z, 0);
                                                v /= submesh.VertexScale;
                                                v *= (float)Int16.MaxValue;
                                                reader.Write((Int16)v.X);
                                                reader.Write((Int16)v.Y);
                                                reader.Write((Int16)v.Z);
                                                reader.Write((Int16)v.W); //-1,0,1
                                                break;
                                        }
                                        break;
                                    }
                            }
                        }
                    }
                    Utilities.Align(reader, 16);
                }
            }
            submesh.Data = ms.ToArray();

            return submesh;
        }

        public static ushort CalculateScaleFactor(this Mesh mesh)
        {
            float x = Math.Max(Math.Abs(mesh.BoundingBox.Min.X), Math.Abs(mesh.BoundingBox.Max.X));
            float y = Math.Max(Math.Abs(mesh.BoundingBox.Min.Y), Math.Abs(mesh.BoundingBox.Max.Y));
            float z = Math.Max(Math.Abs(mesh.BoundingBox.Min.Z), Math.Abs(mesh.BoundingBox.Max.Z));
            ushort scaleFactor = 1;
            int i = 1;
            while (true)
            {
                if (x / (float)scaleFactor < 0.99f && y / (float)scaleFactor < 0.99f && z / (float)scaleFactor < 0.99f) break;
                if (i == 1) scaleFactor = 4;
                else scaleFactor = (ushort)(4 * i);
                i++;
            }
            return scaleFactor;
        }

        public static string ForceStringNumeric(this string str, bool allowDots = false)
        {
            string editedText = "";
            bool hasIncludedDot = false;
            bool hasIncludedMinus = false;
            for (int i = 0; i < str.Length; i++)
            {
                if (Char.IsNumber(str[i]) || (str[i] == '.' && allowDots) || (str[i] == '-'))
                {
                    if (str[i] == '-' && hasIncludedMinus) continue;
                    if (str[i] == '-' && i != 0) continue;
                    if (str[i] == '-') hasIncludedMinus = true;
                    if (str[i] == '.' && hasIncludedDot) continue;
                    if (str[i] == '.') hasIncludedDot = true;
                    editedText += str[i];
                }
            }
            if (editedText == "") editedText = "0";
            if (editedText == "-") editedText = "-0";
            if (editedText == ".") editedText = "0";
            return editedText;
        }
    }
}

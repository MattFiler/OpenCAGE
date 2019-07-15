using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlienPAK
{
    public enum TextureCapability : uint
    {
        NONE = 0x0, // not part of DX, added for convenience
        COMPLEX = 0x8, // should be set for any DDS file with more than one main surface
        TEXTURE = 0x1000, // should always be set
        MIPMAP = 0x400000 // only for files with MipMaps
    }
    
    public enum TextureType : uint
    {
        UNCOMPRESSED_GENERAL,
        Unknown,
        Dxt1,
        Dxt3,
        Dxt5,
        R8G8B8,
        B8G8R8,
        Bgra5551,
        Bgra4444,
        Bgr565,
        Alpha8,
        X8R8G8B8,
        A8R8G8B8,
        A8B8G8R8,
        X8B8G8R8,
        RGB555,
        R32F,
        R16F,
        A32B32G32R32F,
        A16B16G16R16F,
        Q8W8V8U8,
        CxV8U8,
        G16R16F,
        G32R32F,
        G16R16,
        A2B10G10R10,
        A16B16G16R16,
        ATI2N,
        BC7_UNORM
    }

    public enum FourCC : uint
    {
        D3DFMT_DXT1 = 0x31545844,
        D3DFMT_DXT2 = 0x32545844,
        D3DFMT_DXT3 = 0x33545844,
        D3DFMT_DXT4 = 0x34545844,
        D3DFMT_DXT5 = 0x35545844,
        D3DFMT_ATI2N = 0x32495441,
        DX10 = 0x30315844,
        DXGI_FORMAT_BC4_UNORM = 0x55344342,
        DXGI_FORMAT_BC4_SNORM = 0x53344342,
        DXGI_FORMAT_BC5_UNORM = 0x32495441,
        DXGI_FORMAT_BC5_SNORM = 0x53354342,

        //DXGI_FORMAT_R8G8_B8G8_UNORM
        D3DFMT_R8G8_B8G8 = 0x47424752,

        //DXGI_FORMAT_G8R8_G8B8_UNORM
        D3DFMT_G8R8_G8B8 = 0x42475247,

        //DXGI_FORMAT_R16G16B16A16_UNORM
        D3DFMT_A16B16G16R16 = 36,

        //DXGI_FORMAT_R16G16B16A16_SNORM
        D3DFMT_Q16W16V16U16 = 110,

        //DXGI_FORMAT_R16_FLOAT
        D3DFMT_R16F = 111,

        //DXGI_FORMAT_R16G16_FLOAT
        D3DFMT_G16R16F = 112,

        //DXGI_FORMAT_R16G16B16A16_FLOAT
        D3DFMT_A16B16G16R16F = 113,

        //DXGI_FORMAT_R32_FLOAT
        D3DFMT_R32F = 114,

        //DXGI_FORMAT_R32G32_FLOAT
        D3DFMT_G32R32F = 115,

        //DXGI_FORMAT_R32G32B32A32_FLOAT
        D3DFMT_A32B32G32R32F = 116,

        D3DFMT_UYVY = 0x59565955,
        D3DFMT_YUY2 = 0x32595559,
        D3DFMT_CxV8U8 = 117,
        D3DFMT_Q8W8V8U8 = 63
    }

    class DDS
    {
        public uint DDS_MAGIC = 0x44445320; // "DDS "
        public uint dwSize = 0x7C000000; // 124 (little endian)
        public uint dwFlags; // (comes in 2 parts) first short is 4013 2nd short is 8
        public uint dwHeight; // 720
        public uint dwWidth; // 1280
        public uint dwPitchOrLinearSize; // For compressed formats, this is the total number of bytes for the main image.
        public uint dwDepth = 0x0; // For volume textures, this is the depth of the volume.
        public uint dwMipMapCount; // total number of levels in the mipmap chain of the main image.
        
        public UInt32[] dwReserved1 = new UInt32[11]; // 11 UInt32s 11- 1 = 10 because the 0th element is also counted

        //Pixelformat sub-struct, 32 bytes
        public UInt32 pfSize = 32; // Size of Pixelformat structure. This member must be set to 32.
        public UInt32 pfFlags; // Flags to indicate valid fields.
        public UInt32 pfFourCC; // This is the four-character code for compressed formats.

        public UInt32 pfRGBBitCount = 0x0; // For RGB formats, this is the total number of bits in the format. dwFlags should include DDpf_RGB in this case. This value is usually 16, 24, or 32. For A8R8G8B8, this value would be 32.
        public UInt32 pfRBitMask = 0x0; // For RGB formats, these three fields contain the masks for the red, green, and blue channels. For A8R8G8B8, these values would be 0x00ff0000, 0x0000ff00, and 0x000000ff respectively.
        public UInt32 pfGBitMask = 0x0; // ..
        public UInt32 pfBBitMask = 0x0; // ..
        public UInt32 pfABitMask = 0x0; // For RGB formats, this contains the mask for the alpha channel, if any. dwFlags should include DDpf_ALPHAPIXELS in this case. For A8R8G8B8, this value would be 0xff000000.

        //Capabilities sub-struct, 16 bytes
        public UInt32 dwCaps1; // always includes DDSCAPS_TEXTURE. with more than one main surface DDSCAPS_COMPLEX should also be set.
        public UInt32 dwCaps2; // For cubic environment maps, DDSCAPS2_CUBEMAP should be included as well as one or more faces of the map
        public UInt32 dwCaps3;
        public UInt32 dwCaps4;
        public UInt32 dwReserved2; // reserverd
    }
}

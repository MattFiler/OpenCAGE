using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlienPAK
{
    public enum TextureFormat : int
    {
        DXGI_FORMAT_B8G8R8A8_UNORM = 0x2,
        DXGI_FORMAT_B8G8R8_UNORM = 0x5,
        DXGI_FORMAT_BC1_UNORM = 0x6,
        DXGI_FORMAT_BC3_UNORM = 0x9,
        DXGI_FORMAT_BC5_UNORM = 0x8,
        DXGI_FORMAT_BC7_UNORM = 0xD
    }

    //The Tex4 Entry
    class TEX4
    {
        //The filename and path
        public string FileName = "";

        //Misc metadata
        public TextureFormat Format;
        public int HeaderPos = -1;

        //Actual texture content
        public TEX4_Part Texture_V1 = new TEX4_Part();
        public TEX4_Part Texture_V2 = new TEX4_Part(); //V2 is the largest, unless we don't have a V2 in which case V1 is.
    }

    //The Tex4 Sub-Parts
    class TEX4_Part
    {
        public Int16 Width = -1;
        public Int16 Height = -1;
        
        public bool Saved = false;
        public int HeaderPos = -1;

        public int StartPos = -1;
        public int Length = -1;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlienPAK
{
    /*
     *
     * Our DDS reader.
     * Created by Matt Filer: http://www.mattfiler.co.uk
     * 
     * This parses imported DDS files to remove the header and pull required information.
     *
    */
    class DDSReader
    {
        public TextureFormat Format;
        public int Width = -1;
        public int Height = -1;
        public byte[] DataBlock;

        //Allow import of other formats? PNG/JPG? https://github.com/Microsoft/DirectXTex/wiki/DirectXTex

        /* Read the DDS header info and main chunk */
        public DDSReader(string FileName)
        {
            BinaryReader TextureReader = new BinaryReader(File.OpenRead(FileName));

            //Width/Height
            TextureReader.BaseStream.Position = 12;
            Height = TextureReader.ReadInt32();
            Width = TextureReader.ReadInt32();

            //Format
            TextureReader.BaseStream.Position = 128;
            switch(TextureReader.ReadInt32())
            {
                case 83:
                    Format = TextureFormat.DXGI_FORMAT_BC5_UNORM;
                    break;
                case 71:
                    Format = TextureFormat.DXGI_FORMAT_BC1_UNORM;
                    break;
                case 77:
                    Format = TextureFormat.DXGI_FORMAT_BC3_UNORM;
                    break;
                case 87:
                    Format = TextureFormat.DXGI_FORMAT_B8G8R8A8_UNORM;
                    break;
                case 98:
                    Format = TextureFormat.DXGI_FORMAT_BC7_UNORM;
                    break;
                default:
                    Format = TextureFormat.DXGI_FORMAT_B8G8R8_UNORM; //Fingers crossed
                    break;
            }

            //Content
            TextureReader.BaseStream.Position = 148;
            DataBlock = TextureReader.ReadBytes((int)TextureReader.BaseStream.Length - 148);

            TextureReader.Close();
        }
    }
}

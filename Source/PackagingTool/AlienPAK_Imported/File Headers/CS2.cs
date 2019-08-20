using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlienPAK
{
    class CS2
    {
        /* BIN */
        public string Filename = "";
        public string ModelPartName = "";
        public string MaterialName = ""; //Pulled from MTL with MateralLibraryIndex

        public int FilenameOffset = 0;
        public int ModelPartNameOffset = 0;
        public int MaterialLibaryIndex = 0;
        public int BlockSize = 0;
        public int ScaleFactor = 0;
        public int VertCount = 0;
        public int FaceCount = 0;
        public int BoneCount = 0;

        /* PAK */
        public int PakOffset = 0;
        public int PakSize = 0;
    }
}

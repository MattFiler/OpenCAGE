using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlienPAK
{
    class CathodeShaderHeader
    {
        public string FileName = ""; //The name of the file in the shader archive (unsure how to get this right now with the weird _BIN/PAK way of working)

        public int FileLength = 0; //The length of the file in the archive for this header
        public int FileLengthWithPadding = 0; //The length of the file in the archive for this header, with any padding at the end of the file included

        public int FileOffset = 0; //Position in archive from end of header list
        public int FileIndex = 0; //The index of the file

        public byte[] FileContent; //The content for the file
        
        public byte[] StringPart1; //4 bytes that look like they're part of a filepath
        public byte[] StringPart2; //4 bytes that look like they're part of a filepath
    }
}

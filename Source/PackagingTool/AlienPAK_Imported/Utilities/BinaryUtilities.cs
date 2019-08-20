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
     * Misc binary tools: e.g. handling big endians, etc.
     * Created by Matt Filer: http://www.mattfiler.co.uk
     * 
     * This will probably be expanded over time as required.
     *
    */
    class BigEndianUtils
    {
        public int ReadInt32(BinaryReader Reader)
        {
            byte[] data = Reader.ReadBytes(4);
            Array.Reverse(data);
            return BitConverter.ToInt32(data, 0);
        }
        public Int16 ReadInt16(BinaryReader Reader)
        {
            var data = Reader.ReadBytes(2);
            Array.Reverse(data);
            return BitConverter.ToInt16(data, 0);
        }
        public Int64 ReadInt64(BinaryReader Reader)
        {
            var data = Reader.ReadBytes(8);
            Array.Reverse(data);
            return BitConverter.ToInt64(data, 0);
        }
        public UInt32 ReadUInt32(BinaryReader Reader)
        {
            var data = Reader.ReadBytes(4);
            Array.Reverse(data);
            return BitConverter.ToUInt32(data, 0);
        }
        
        public byte[] FlipEndian(byte[] ThisEndian)
        {
            Array.Reverse(ThisEndian);
            return ThisEndian;
        }
    }
    class ExtraBinaryUtils
    {
        //Gets a string from a byte array (at position) by reading chars until a null is hit
        public string GetStringFromByteArray(byte[] byte_array, int position)
        {
            string to_return = "";
            for (int i = 0; i < 999999999; i++)
            {
                byte this_byte = byte_array[position + i];
                if (this_byte == 0x00)
                {
                    break;
                }
                to_return += (char)this_byte;
            }
            return to_return;
        }

        //Removes the leading nulls from a byte array, useful for cleaning byte-aligned file extracts
        public byte[] RemoveLeadingNulls(byte[] extracted_file)
        {
            //Remove from leading
            int start_offset = 0;
            for (int i = 0; i < 4; i++)
            {
                if (extracted_file[i] == 0x00)
                {
                    start_offset = i+1;
                    continue;
                }
                break;
            }
            byte[] to_return = new byte[extracted_file.Length - start_offset];
            Array.Copy(extracted_file, start_offset, to_return, 0, to_return.Length);
            return to_return;
        }

        //Writes a string without a leading length value (C# BinaryWriter default)
        public void WriteString(string string_to_write, BinaryWriter writer)
        {
            foreach (char character in string_to_write)
            {
                writer.Write(character);
            }
        }
    }
}

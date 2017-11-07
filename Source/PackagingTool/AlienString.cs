// Alien Isolation (Binary XML converter)
// Written by WRS (xentax.com)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace PackagingTool
{
    // fake typedefs
    using u32 = UInt32;
    using u16 = UInt16;
    using u8 = Byte;

    class AlienString
    {
#region Common methods for reading strings from a BinaryReader

        static public string MakeCleanString(u8[] bytes)
        {
            return Encoding.Default.GetString(bytes).TrimEnd('\0');
        }

        static public string ReadInlineNullTerminatedString(BinaryReader br)
        {
            List<byte> buf = new List<byte>();

            for (byte b = br.ReadByte(); b != 0x0; b = br.ReadByte())
            {
                buf.Add(b);
            }

            return MakeCleanString(buf.ToArray());
        }

        static public string ReadNullTerminatedStringAt(BinaryReader br, u32 targetpos)
        {
            br.BaseStream.Position = targetpos;
            return ReadInlineNullTerminatedString(br);
        }

        static public string ReadNullTerminatedString(BinaryReader br, u32 targetpos)
        {
#if DEBUG
            if (targetpos >= br.BaseStream.Length)
            {
                return "";
            }
#endif

            long pos = br.BaseStream.Position;
            string str = ReadNullTerminatedStringAt(br, targetpos);
            br.BaseStream.Position = pos;

            return str;
        }

#endregion

        static private void FixupXmlEntityInternal(ref string str, string src, string dest)
        {
            if (str.IndexOf(src) >= 0)
            {
                str = str.Replace(src, dest);
            }
        }

        static public string EncodeXml(string str)
        {
            string xml_str = str;

            FixupXmlEntityInternal(ref xml_str, "\"", "&quot;");
            FixupXmlEntityInternal(ref xml_str, "&", "&amp;");
            FixupXmlEntityInternal(ref xml_str, "'", "&apos;");
            FixupXmlEntityInternal(ref xml_str, "<", "&lt;");
            FixupXmlEntityInternal(ref xml_str, ">", "&gt;");

            return xml_str;
        }

        static public string DecodeXml(string xml_str)
        {
            string str = xml_str;

            FixupXmlEntityInternal(ref str, "&quot;", "\"");
            FixupXmlEntityInternal(ref str, "&amp;", "&");
            FixupXmlEntityInternal(ref str, "&apos;", "'");
            FixupXmlEntityInternal(ref str, "&lt;", "<");
            FixupXmlEntityInternal(ref str, "&gt;", ">");

            return str;
        }

        struct Inst
        {
            public string Value;
            public u32 Offset;

            public Inst(string str_value)
            {
                Value = str_value;
                Offset = 0;
            }
        }

        public class Cache
        {
            private List<Inst> Strings;

            public Cache()
            {
                Strings = new List<Inst>();
            }

            public void Clear()
            {
                Strings.Clear();
            }

            public void AddString(string str)
            {
                if( !Strings.Exists(i => i.Value == str) )
                {
                    Strings.Add(new Inst(str));
                }
            }

            public void PrepareForExport()
            {
                // xxxxx refactor asap

                Inst[] items = Strings.OrderBy(x => x.Value).ToArray();

                u32 InternalOffset = 0;

                for (int i = 0; i < items.Count(); i++)
                {
                    items[i].Offset = InternalOffset;
                    InternalOffset += Convert.ToUInt32(items[i].Value.Length + 1);
                }

                Strings = items.ToList();
            }

            public u32 GetOffset(string str)
            {
                int idx = Strings.FindIndex(i => i.Value == str);
                if (idx != -1)
                {
                    return Strings[idx].Offset;
                }

                return 0;
            }

            public MemoryStream Export()
            {
                // Sorts cache
                PrepareForExport();

                MemoryStream ms = new MemoryStream();

                foreach(Inst i in Strings)
                {
                    u8[] data = Encoding.Default.GetBytes(i.Value + "\0");
                    ms.Write(data, 0, data.Length);
                }

                return ms;
            }
        }

        public static Cache StringPool1 = new Cache(); // node and attribute names
        public static Cache StringPool2 = new Cache(); // preserved spacing, inner text

        public class Ref
        {
            public string value { get; private set; }
            public u32 offset { get; private set; }

            bool main_pool;

            void AddToCache()
            {
                if (main_pool)  StringPool1.AddString(value);
                else            StringPool2.AddString(value);
            }

            public Ref(string raw_string, bool coreString)
            {
                offset = 0;
                value = raw_string;
                main_pool = coreString;

                AddToCache();
            }

            public Ref(BinaryReader br, bool coreString)
            {
                offset = 0;
                value = ReadNullTerminatedString(br, br.ReadUInt32());
                main_pool = coreString;

                AddToCache();
            }

            public void Fixup(u32 block_1, u32 block_2)
            {
                offset = main_pool ? block_1 : block_2;
                offset += main_pool ? StringPool1.GetOffset(value) : StringPool2.GetOffset(value);
            }
        }
    }
}

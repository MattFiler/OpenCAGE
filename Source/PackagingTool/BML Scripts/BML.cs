// Alien Isolation (Binary XML converter)
// Written by WRS (xentax.com)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;
using System.Collections;

namespace PackagingTool
{
    // fake typedefs
    using u32 = UInt32;
    using u16 = UInt16;
    using u8 = Byte;

    class BML
    {
        // complete.
        struct Header
        {
            const string XML_FLAG = "xml\0";

            public u32 blockData { get; private set; }
            public u32 blockStrings { get; private set; }
            public u32 blockLineEndings { get; private set; }

            public bool Read(BinaryReader br)
            {
                bool valid = true;

                string magic = Encoding.Default.GetString(br.ReadBytes(XML_FLAG.Length));
                valid &= (magic == XML_FLAG);

                blockData = br.ReadUInt32();
                blockStrings = br.ReadUInt32();
                blockLineEndings = br.ReadUInt32();

                valid &= (blockLineEndings < br.BaseStream.Length);
                valid &= (blockStrings < blockLineEndings);
                valid &= (blockData < blockStrings);

                return valid;
            }

            public bool Write(BinaryWriter bw)
            {
                bool valid = true;

                bw.Write(Encoding.Default.GetBytes(XML_FLAG), 0, XML_FLAG.Length);

                valid &= blockLineEndings != 0;
                valid &= blockStrings != 0;
                valid &= blockData != 0;

                bw.Write(blockData);
                bw.Write(blockStrings);
                bw.Write(blockLineEndings);

                return valid;
            }

            public void Fixup(u32 of1, u32 of2, u32 of3)
            {
                blockData = of1;
                blockStrings = of2;
                blockLineEndings = of3;
            }

            static public u32 Size()
            {
                return 16;
            }
        }

        // complete.
        struct Attribute
        {
            public AlienString.Ref Name { get; private set; }
            public AlienString.Ref Value { get; private set; }

            public bool ReadXML(string str_name, string str_value)
            {
                Name = new AlienString.Ref(str_name, true);
                Value = new AlienString.Ref(AlienString.DecodeXml(str_value), true);

                return true;
            }

            public bool Read(BinaryReader br)
            {
                Name = new AlienString.Ref(br, true);
                Value = new AlienString.Ref(br, true);

                return true;
            }

            public bool Write(BinaryWriter bw)
            {
                bw.Write(Name.offset);
                bw.Write(Value.offset);

                return true;
            }

            static public u32 Size()
            {
                return 8;
            }
        }

        class NodeFlags
        {
            public u8 Attributes { get; set; }

            public u8 RawInfo { get; set; }

            public bool unknown_1
            {
                // 1 << 0
                get { return GetFlag(0x1); }
                set { SetFlag(0x1, value); }
            }

            public bool unknown_2
            {
                // 1 << 1
                get { return GetFlag(0x2); }
                set { SetFlag(0x2, value); }
            }

            public bool ContinueSequence
            {
                // 1 << 2
                get { return GetFlag(0x4); }
                set { SetFlag(0x4, value); }
            }

            bool GetFlag(u8 mask)
            {
                return (RawInfo & mask) != 0;
            }

            void SetFlag(u8 mask, bool value)
            {
                RawInfo &= Convert.ToByte((~mask) & 0xFF);
                if( value )
                {
                    RawInfo |= mask;
                }
            }

            public u16 Children { get; set; }

            public NodeFlags()
            {
                Attributes = 0;
                RawInfo = 0;
                Children = 0;
            }

            public bool Read(BinaryReader br)
            {
                u32 bytes = br.ReadUInt32();

                // bit format:
                // aaaa aaaa iiic cccc cccc cccc cccc cccc

                // 8-bits : number of attributes
                Attributes = Convert.ToByte(bytes & 0xFF);

                // 3-bits : info flags
                RawInfo = Convert.ToByte((bytes >> 8) & 0x7);

                // 21-bits : number of child nodes
                u32 raw_children = (bytes >> 11) & 0x1FFFFF;

                // note: we store raw_children as u16 for alignment purposes
                // aaaa aaaa iiic cccc cccc cccc ccc- ---- (- = ignored)

#if DEBUG
                if (raw_children > 0xFFFF)
                {
                    //Console.WriteLine("Warning: huge number of child nodes");
                }
#endif

                Children = Convert.ToUInt16(raw_children & 0xFFFF);

                return true;
            }

            public bool Write(BinaryWriter bw)
            {
                u32 bytes = 0;

                u32 tmp = Children;
                bytes |= tmp << 11;

                tmp = RawInfo;
                bytes |= (tmp & 0x7) << 8;

                tmp = Attributes;
                bytes |= (tmp & 0xFF);

                bw.Write(bytes);

                return true;
            }

            static public u32 Size()
            {
                return 4;
            }
        }

        class Node
        {
            public List<Node> Nodes { get; private set; }
            public List<Attribute> Attributes { get; private set; }

            public AlienString.Ref End2 { get; private set; }
            public AlienString.Ref Text { get; private set; }
            public AlienString.Ref End { get; private set; }
            public AlienString.Ref Inner { get; private set; }

            public u32 Offset { get; set; } // public modifier
            public u32 Depth { get; private set; }

            public NodeFlags Flags { get; private set; }

            public Node()
            {
                Nodes = new List<Node>();
                Attributes = new List<Attribute>();
                Flags = new NodeFlags();
                Depth = 0;
            }

            public void SetDeclaration()
            {
                Text = new AlienString.Ref("?xml", true);
                Flags.Children = 0;
            }

            bool HasElementSibling(XmlElement ele)
            {
                XmlNode sibling = ele.NextSibling;

                while( sibling != null )
                {
                    if( sibling.NodeType == XmlNodeType.Element )
                    {
                        return true;
                    }

                    sibling = sibling.NextSibling;
                }

                return false;
            }

            public bool ReadXML(XmlElement ele, u32 depth)
            {
                bool valid = true;

                Depth = depth;
                Text = new AlienString.Ref(ele.Name, true);

                if (ele.HasAttributes)
                {
                    if( ele.Attributes.Count > 0xFF )
                    {
                        //Console.WriteLine("Too many attributes for {0}", Text.value);
                        valid = false;
                        return valid;
                    }

                    foreach (XmlAttribute attr in ele.Attributes)
                    {
                        Attribute a = new Attribute();
                        a.ReadXML(attr.Name, attr.Value);
                        Attributes.Add(a);
                    }
                }

                if( ele.HasChildNodes )
                {
                    // inner text is treated as a special text node, so it has children.. (YIKES)

                    foreach( XmlNode xnode in ele.ChildNodes )
                    {
                        // special parser requirements

                        switch( xnode.NodeType )
                        {
                            case XmlNodeType.Element:

                                XmlElement child = (xnode as XmlElement);

                                Node nchild = new Node();

                                valid &= nchild.ReadXML(child, depth + 1);

                                if( valid )
                                {
                                    Nodes.Add(nchild);
                                }

                                break;

                            case XmlNodeType.Text:

                                Inner = new AlienString.Ref(AlienString.DecodeXml(xnode.Value),false);
                                End2 = new AlienString.Ref("\r\n", false);
                                
                                break;

                            case XmlNodeType.Comment:

                                // Could be added as Inner/End2, but not required
                                //Console.WriteLine("Found XML comment - skipping it");

                                break;

                            default:
                               // Console.WriteLine("XmlNodeType not handled - skipping it");
                                break;
                        }
                    }
                }

                bool last_child = !HasElementSibling(ele);

                Fixup(last_child);

                return valid;
            }

            public bool Read(BinaryReader br, u32 depth)
            {
                bool valid = true;

                Depth = depth;
                Text = new AlienString.Ref(br, true);

                valid &= Flags.Read(br);
                
                // get attributes

                if( Flags.Attributes > 0 )
                {
#if DEBUG
                    if( Flags.Attributes > 100 )
                    {
                        //Console.WriteLine("Possible large number of attributes -> {0} (node={1})", Flags.Attributes, Text);
                    }
#endif
                    for (u32 attribs = 0; attribs < (u32)Flags.Attributes; attribs++)
                    {
                        Attribute a = new Attribute();
                        valid &= a.Read(br);

                        if (!valid)
                        {
                            return false;
                        }

                        Attributes.Add(a);
                    }
                }

                switch (Flags.RawInfo)
                {
                    case 0: // 000

                        Offset = br.ReadUInt32();

#if DEBUG
                        if( Offset != 28 )
                        {
                           // Console.WriteLine("Interesting offset {0}", Offset);
                        }
#endif

                        break;

                    case 1: // 001
                        End = new AlienString.Ref(br, false);
                        Offset = br.ReadUInt32();

                        break;

                    case 2: // 010 -> last in sequence
                    case 6: // 110 -> continued sequence
                        End = new AlienString.Ref(br, false);

                        if (Flags.Children > 0)
                        {
                            Offset = br.ReadUInt32();
                        }

                        break;

                    case 3: // 011 -> last in sequence
                    case 7: // 111 -> continued sequence

                        // note: inner text is stored in the second pool

                        Inner = new AlienString.Ref(br, false); // inner text or line diff
                        End2 = new AlienString.Ref(br, false); // line ending

                        if (Flags.Children > 0)
                        {
                            Offset = br.ReadUInt32();
                        }

                        break;

                    default:
                        // flags may need sorting out
                        break;
                }

                return valid;
            }

            public u32 Size()
            {
                u32 my_size = 0;

                // text offset
                my_size += 4;
                // flags
                my_size += NodeFlags.Size();
                // attribute entries (read from flags)
                my_size += Attribute.Size() * Flags.Attributes;
                // check against info
                switch( Flags.RawInfo )
                {
                    case 0:
                        my_size += 4;
                        break;
                    case 1:
                        my_size += 4 + 4;
                        break;
                    case 2:
                    case 6:
                        my_size += 4;
                        if (Flags.Children > 0) my_size += 4;
                        break;
                    case 3:
                    case 7:
                        my_size += 4 + 4;
                        if (Flags.Children > 0) my_size += 4;
                        break;
                    default:
                        //Console.WriteLine("Unsupported info");
                        break;
                }

                return my_size;
            }

            public void Fixup(bool last_child)
            {
                Flags.RawInfo = 0;
                Flags.Attributes = Convert.ToByte(Attributes.Count & 0xFF);
                Flags.Children = Convert.ToUInt16(Nodes.Count & 0xFFFF);

                if( Text.value == "?xml" )
                {
                    if( Flags.Attributes == 0 )
                    {
                        // just offsets to child

                        Flags.unknown_1 = false;
                        Flags.unknown_2 = false;
                        Flags.ContinueSequence = false;

                        // raw flags are now 000
                    }
                    else
                    {
                        if (End == null)
                        {
                            End = new AlienString.Ref("\r\n", false);
                        }

                        // declaration kept - child mandatory
                        Flags.unknown_1 = true;
                        Flags.unknown_2 = false;
                        Flags.ContinueSequence = false;

                        // raw flags are now 001
                    }
                }
                else
                {
                    if( Inner != null )
                    {
                        // has inner kept; child optional
                        Flags.unknown_1 = true;
                        Flags.unknown_2 = true;
                        Flags.ContinueSequence = !last_child;

                        // raw flags are now either 011 or 111

                        if( End2 == null )
                        {
                            End2 = new AlienString.Ref("\r\n", false);
                        }
                    }
                    else
                    {
                        // end spacing, child optional
                        Flags.unknown_1 = false;
                        Flags.unknown_2 = true;
                        Flags.ContinueSequence = !last_child;

                        // raw flags are now either 010 or 110

                        if (End == null)
                        {
                            End = new AlienString.Ref("\r\n", false);
                        }
                    }
                }
            }

            public bool Write(BinaryWriter bw, u32 of1, u32 of2)
            {
                // text offset
                Text.Fixup(of1, of2);
                bw.Write(Text.offset);
                // flags
                Flags.Write(bw);
                foreach(Attribute a in Attributes)
                {
                    a.Name.Fixup(of1, of2);
                    a.Value.Fixup(of1, of2);
                    a.Write(bw);
                }

                switch (Flags.RawInfo)
                {
                    case 0:
                        bw.Write(Offset);
                        break;
                    case 1:
                        End.Fixup(of1, of2);
                        bw.Write(End.offset);
                        bw.Write(Offset);
                        break;
                    case 2:
                    case 6:
                        End.Fixup(of1, of2);
                        bw.Write(End.offset);
                        if (Flags.Children > 0) bw.Write(Offset);
                        break;
                    case 3:
                    case 7:
                        Inner.Fixup(of1, of2);
                        End2.Fixup(of1, of2);
                        bw.Write(Inner.offset);
                        bw.Write(End2.offset);
                        if (Flags.Children > 0) bw.Write(Offset);
                        break;
                    default:
                        //Console.WriteLine("Unsupported info");
                        break;
                }

                return true;
            }
        }

        Header hdr;
        Node root;

        public BML()
        {
            root = new Node();
        }

        private bool ReadWrapper(BinaryReader br, ref Node owner, u32 depth)
        {
            bool success = true;

            Node n = new Node();
            success &= n.Read(br, depth);

            // try to parse other blocks
            if( success )
            {
                if( n.Offset != 0 )
                {
                    long pos = br.BaseStream.Position;

#if DEBUG
                    //Console.WriteLine("Parsing child at {0}", n.Offset);
#endif

                    // seek to child pos
                    br.BaseStream.Position = n.Offset;

                    for (u32 i = 0; i < n.Flags.Children; i++)
                    {
                        success &= ReadWrapper(br, ref n, depth + 1);
                    }

                    // seek back
                    br.BaseStream.Position = pos;
                }

                owner.Nodes.Add(n);
            }

            return success;
        }

        private bool ReadAllNodes(BinaryReader br)
        {
            bool success = root.Read(br, 0);

            // this is always the initial node
            success &= (root.Text.value == "?xml");

            // there should be at least 1 child node
            success &= (root.Flags.Children > 0);
            
            if( !success )
            {
                //Console.WriteLine("Unexpected XML data");
                return false;
            }
            
            success &= ReadWrapper(br, ref root, root.Depth +1);

            return success;
        }

        public bool ReadBML(BinaryReader br)
        {
            bool valid = true;

            valid &= hdr.Read(br);

            if (!valid)
            {
                //Console.WriteLine("Failed to read header");
                return valid;
            }

            AlienString.StringPool1.Clear();
            AlienString.StringPool2.Clear();

            valid &= ReadAllNodes(br);

            return valid;
        }

        public bool ReadXML(BinaryReader br)
        {
            XmlDocument doc = new XmlDocument();

            try
            {
                doc.Load(br.BaseStream);
            }
            catch
            {
               // Console.WriteLine("Failed to parse XML - exiting");
                return false;
            }

            bool valid = true;

            AlienString.StringPool1.Clear();
            AlienString.StringPool2.Clear();
            
            // fake setup for declaration (always root node)
            root.SetDeclaration();

            foreach (XmlNode xnode in doc.ChildNodes)
            {
                switch (xnode.NodeType)
                {
                    case XmlNodeType.XmlDeclaration:

                        XmlDeclaration decl = (xnode as XmlDeclaration);

                        // these are treated as root node attributes
                        // we also have to check the declaration has all of them

                        if (decl.Version != null && decl.Version.Length > 0)
                        {
                            Attribute ver = new Attribute();
                            ver.ReadXML("version", decl.Version);
                            root.Attributes.Add(ver);
                        }

                        if (decl.Encoding != null && decl.Encoding.Length > 0)
                        {
                            Attribute enc = new Attribute();
                            enc.ReadXML("encoding", decl.Encoding);
                            root.Attributes.Add(enc);
                        }

                        if (decl.Standalone != null && decl.Standalone.Length > 0)
                        {
                            Attribute sta = new Attribute();
                            sta.ReadXML("standalone", decl.Standalone);
                            root.Attributes.Add(sta);
                        }

                        root.Flags.Attributes = Convert.ToByte(root.Attributes.Count & 0xFF);
                        
                        break;

                    case XmlNodeType.Comment:

                       // Console.WriteLine("Found XML comment - skipping it");
                        break;

                    case XmlNodeType.Element:

                        Node actual_root = new Node();
                        valid &= actual_root.ReadXML(xnode as XmlElement, root.Depth +1);
                        root.Nodes.Add(actual_root);

                        break;

                    default:
                        //Console.WriteLine("XmlNodeType not handled - skipping it");
                        break;
                }
            }
                        
            return valid;
        }

        // xxxx todo refactor
        private string DumpNode(Node n, int depth = 0)
        {
            string d = "";
            bool ignored = (depth == 0 && n.Attributes.Count == 0 );
            
            if( !ignored )
            {
                d += String.Format("<{0}", n.Text.value);

                foreach (Attribute a in n.Attributes)
                {
                    // Now encodes XML entities (value)
                    d += String.Format(" {0}=\"{1}\"", a.Name.value, AlienString.EncodeXml(a.Value.value));
                }
            }

            if (n.Nodes.Count > 0)
            {
                if (!ignored)
                {
                    if( depth == 0 )
                    {
                        // first xml tag must end in matching <? tags ?>
                        d += "?";
                    }

                    d += ">";

                    if( n.End != null ) d += n.End.value;
                }

                if (n.Inner != null && n.Inner.value.Length != 0)
                {
                    // Now encodes XML entities
                    d += AlienString.EncodeXml(n.Inner.value);
                }

                foreach (Node node in n.Nodes)
                {
                    d += DumpNode(node, depth + 1);
                }

                // uh, the first xml tag doesn't need to close
                if (depth != 0)
                {
                    d += String.Format("</{0}>", n.Text.value);

                    if( n.End2 != null ) d += n.End2.value;
                }
            }
            else if( !ignored )
            {
                if (n.Inner != null && n.Inner.value.Length != 0)
                {
                    d += ">";
                    d += n.Inner.value;
                    d += String.Format("</{0}>", n.Text.value);
                    d += n.End2.value;
                }
                else
                {
                    // <tag /> and <tag a="b" />
                    if( n.Attributes.Count > 0 )
                    {
                        d += " ";
                    }

                    d += "/>";

                    if (n.End != null) d += n.End.value;
                    if (n.End2 != null) d += n.End2.value;
                }
            }

            return d;
        }

        public bool ExportXML(ref string xml)
        {
            FixupAllNodes(root, true);

            xml = DumpNode(root);

#if DEBUG
            Console.WriteLine(xml);
#endif

            return true;
        }

        void FixupAllNodes(Node n, bool last_node)
        {
            n.Fixup(last_node);

            int last = n.Nodes.Count - 1;
            int count = 0;
            foreach (Node c in n.Nodes)
            {
                FixupAllNodes(c, count == last);
                ++count;
            }
        }

        // horrible. but we need all child nodes ordered by depth in a 1d array
        Node[] GetNodesAtDepth(List<Node> nodes, u32 depth)
        {
            List<Node> local_nodes = new List<Node>();

            foreach( Node n in nodes )
            {
                if( n.Depth == depth )
                {
                    local_nodes.Add(n);
                }
                else if( n.Depth < depth )
                {
                    Node[] ns = GetNodesAtDepth(n.Nodes, depth);
                    if( ns.Length > 0 )
                    {
                        local_nodes.AddRange(ns);
                    }
                }
            }

            return local_nodes.ToArray();
        }

        Node[] GetNodeArray()
        {
            List<Node> nodes = new List<Node>();

            // level 0
            nodes.Add(root);
            // level 1+
            u32 depth = 1;
            while ( true )
            {
                Node[] ns = GetNodesAtDepth(root.Nodes, depth);

                if( ns.Length == 0 )
                {
                    break;
                }
                else
                {
                    nodes.AddRange(ns);
                    ++depth;
                }
            }

            return nodes.ToArray();
        }

        public bool ExportBML(BinaryWriter bw)
        {
            // the nodes are exported as a single-dimensional blob (instead of a tree using recursion)

            // pass 1; recursively fixup all nodes (mainly the flags)
            FixupAllNodes(root, true);

            // pass 2; strip the node relationships and fetch our node blob
            Node[] nodearray = GetNodeArray();

            // pass 3; calculate offsets from known data
            u32 node_size = 0;

            foreach (Node n in nodearray)
            {
                node_size += n.Size();
            }

            MemoryStream p1 = AlienString.StringPool1.Export();
            MemoryStream p2 = AlienString.StringPool2.Export();

            u32 block1 = Header.Size()
                + node_size
                + 1; // extra null byte

            u32 block2 = block1
                + 1 // extra null byte
                + (u32)p1.Length;

            u32 block3 = block2
                + (u32)p2.Length;

            u32 file_size = block3
                + 1; // extra null byte

            // pass 4; fixup and export this horrible mess
            hdr.Fixup(block1, block2, block3);

            // we know the size so lets preallocate the buffer
            bw.BaseStream.SetLength(file_size);

            u32 of1 = block1 + 1;
            u32 of2 = block2;

            // -- header
            hdr.Write(bw);

            // -- node blob
            u32 cur_depth = 0;
            u32 cur_depth_offset = 0;
            foreach (Node n in nodearray)
            {
                // here we fixup the child offsets

                // to do this, we need to calculate the starting position of the next depth. sigh.
                if( n.Flags.Children > 0 )
                {
                    if( cur_depth != n.Depth +1 )
                    {
                        // we need to loop through the ENTIRE array just to set cur_depth_offset
                        // only do this when the depth changes
                        // xxxxx even better, do this outside of the loop

                        // -- start from the start of the node pool
                        cur_depth_offset = Header.Size();

                        foreach( Node nn in nodearray)
                        {
                            if( nn.Depth == n.Depth +1 )
                            {
                                break;
                            }

                            // -- count all nodes up to this point
                            cur_depth_offset += nn.Size();
                        }

                        cur_depth = n.Depth + 1;
                    }

                    // now we have an offset

                    n.Offset = cur_depth_offset;

                    // next, we need to update the cur_depth_offset anyway

                    foreach( Node child in n.Nodes )
                    {
                        cur_depth_offset += child.Size();
                    }
                }

                // now we can write it out
                n.Write(bw, of1, of2);
            }

            // -- string pools
            bw.BaseStream.Seek(block1 + 1, SeekOrigin.Begin);
            p1.WriteTo(bw.BaseStream);
            p2.WriteTo(bw.BaseStream);

            return true;
        }
    }
}

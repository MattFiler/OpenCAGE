using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alien_Isolation_Mod_Tools
{
    public class CommandsPAK
    {
        /* Load and parse the COMMANDS.PAK */
        public CommandsPAK(string pathToPak)
        {
            path_to_pak = pathToPak;

            BinaryReader reader = new BinaryReader(File.OpenRead(path_to_pak));

            ReadEntryPoints(reader);
            ReadPrimaryOffsets(reader);
            ReadParameters(reader);
            ReadFlowgraphs(reader);

            reader.Close();
        }

        /* Save all changes back out */
        public void Save()
        {
            BinaryWriter writer = new BinaryWriter(File.OpenWrite(path_to_pak));

            //Update all selected parameter offsets
            foreach (CathodeFlowgraph flowgraph in flowgraphs)
            {
                foreach (CathodeNodeEntity node in flowgraph.nodes)
                {
                    foreach (CathodeParameterReference param_ref in node.nodeParameterReferences)
                    {
                        writer.BaseStream.Position = param_ref.editOffset;
                        writer.Write((int)(param_ref.offset/4));
                    }
                }
            }

            writer.Close();
        }

        /* Return a list of filenames for flowgraphs in the CommandsPAK archive */
        public List<string> GetFlowgraphNames()
        {
            List<string> toReturn = new List<string>();
            foreach (CathodeFlowgraph flowgraph in flowgraphs) toReturn.Add(flowgraph.name);
            return toReturn;
        }

        /* Find the a script entry object by name */
        public int GetFileIndex(string FileName)
        {
            for (int i = 0; i < flowgraphs.Count; i++) if (flowgraphs[i].name == FileName || flowgraphs[i].name == FileName.Replace('/', '\\')) return i;
            throw new Exception("ERROR! Could not find the requested file index.");
        }

        /* Get flowgraph/parameter */
        public CathodeFlowgraph GetFlowgraph(byte[] id)
        {
            if (id == null) return null;
            foreach (CathodeFlowgraph flowgraph in flowgraphs) if (flowgraph.nodeID.SequenceEqual(id)) return flowgraph;
            return null;
        }
        public CathodeParameter GetParameter(int offset)
        {
            foreach (CathodeParameter parameter in parameters) if (parameter.offset == offset) return parameter;
            return null;
        }

        /* Get all flowgraphs/parameters */
        public List<CathodeFlowgraph> AllFlowgraphs { get { return flowgraphs; } }
        public List<CathodeParameter> AllParameters { get { return parameters; } }

        /* Get entry points */
        public List<CathodeFlowgraph> EntryPoints { get
            {
                List<CathodeFlowgraph> entry_points_CF = new List<CathodeFlowgraph>();
                foreach (byte[] flow_id in entry_points) entry_points_CF.Add(GetFlowgraph(flow_id));
                return entry_points_CF;
            } 
        }

        /* Parse the three entry flowgraphs for this COMMANDS.PAK */
        private void ReadEntryPoints(BinaryReader reader)
        {
            for (int i = 0; i < 3; i++) entry_points.Add(reader.ReadBytes(4));
        }

        /* Read the parameter and flowgraph offsets */
        private void ReadPrimaryOffsets(BinaryReader reader)
        {
            /* Initial parameter/flowgraph offset and count info */
            int parameter_offset_pos = reader.ReadInt32() * 4;
            parameter_count = reader.ReadInt32();
            int flowgraph_offset_pos = reader.ReadInt32() * 4;
            flowgraph_count = reader.ReadInt32();

            /* Archive offsets for parameters */
            parameter_offsets = new int[parameter_count];
            reader.BaseStream.Position = parameter_offset_pos;
            for (int i = 0; i < parameter_count; i++)
            {
                parameter_offsets[i] = reader.ReadInt32() * 4;
            }

            /* Archive offsets for flowgraphs */
            flowgraph_offsets = new int[flowgraph_count];
            reader.BaseStream.Position = flowgraph_offset_pos;
            for (int i = 0; i < flowgraph_count; i++)
            {
                flowgraph_offsets[i] = reader.ReadInt32() * 4;
            }
        }

        /* Read all parameters from the PAK */
        private void ReadParameters(BinaryReader reader)
        {
            reader.BaseStream.Position = parameter_offsets[0];
            for (int i = 0; i < parameter_count; i++)
            {
                int length = (i == parameter_count - 1) ? flowgraph_offsets[0] - parameter_offsets[i] : parameter_offsets[i + 1] - parameter_offsets[i];
                CathodeParameter this_parameter = new CathodeParameter();
                CathodeDataType this_datatype = GetDataType(reader.ReadBytes(4));
                switch (this_datatype)
                {
                    case CathodeDataType.TRANSFORM:
                        this_parameter = new CathodeTransform();
                        //TODO: are these X/Y/Zs the right way around?
                        ((CathodeTransform)this_parameter).position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        ((CathodeTransform)this_parameter).rotation = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        break;
                    case CathodeDataType.INTEGER:
                        this_parameter = new CathodeInteger();
                        ((CathodeInteger)this_parameter).value = reader.ReadInt32();
                        break;
                    case CathodeDataType.STRING:
                        this_parameter = new CathodeString();
                        ((CathodeString)this_parameter).unk0 = reader.ReadBytes(4); // some kind of ID sometimes referenced in script and resource id
                        ((CathodeString)this_parameter).unk1 = reader.ReadBytes(4); // sometimes flowgraph id ?!
                        bool shouldStop = false;
                        for (int x = 0; x < length - 8; x++)
                        {
                            byte thisByte = reader.ReadByte();
                            if (thisByte == 0x00) shouldStop = true;
                            if (shouldStop && thisByte != 0x00) break;
                            ((CathodeString)this_parameter).value += (char)thisByte;
                        }
                        reader.BaseStream.Position -= 1;
                        break;
                    case CathodeDataType.BOOL:
                        this_parameter = new CathodeBool();
                        ((CathodeBool)this_parameter).value = (reader.ReadInt32() == 1);
                        break;
                    case CathodeDataType.FLOAT:
                        this_parameter = new CathodeFloat();
                        ((CathodeFloat)this_parameter).value = reader.ReadSingle();
                        break;
                    case CathodeDataType.RESOURCE_ID:
                        this_parameter = new CathodeResource();
                        ((CathodeResource)this_parameter).resourceID = reader.ReadBytes(4);
                        break;
                    case CathodeDataType.VECTOR3:
                        this_parameter = new CathodeVector3();
                        ((CathodeVector3)this_parameter).value = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        break;
                    case CathodeDataType.ENUM:
                        this_parameter = new CathodeEnum();
                        ((CathodeEnum)this_parameter).enumID = reader.ReadBytes(4);
                        ((CathodeEnum)this_parameter).enumIndex = reader.ReadInt32();
                        break;
                    default:
                        this_parameter.unknownContent = reader.ReadBytes(length - 4);
                        break;
                }

                this_parameter.offset = parameter_offsets[i];
                this_parameter.dataType = this_datatype;

                parameters.Add(this_parameter);
            }
        }

        /* Read all flowgraphs from the PAK */
        private void ReadFlowgraphs(BinaryReader reader)
        {
            int scriptStart = parameter_offsets[parameter_offsets.Length - 1] + 8; //Relies on the last param always being 4 in length
            for (int i = 0; i < flowgraph_count; i++)
            {
                CathodeFlowgraph flowgraph = new CathodeFlowgraph();

                //Game doesn't parse the script name, so there's no real nice way of grabbing it!!
                reader.BaseStream.Position = scriptStart;
                flowgraph.globalID = reader.ReadBytes(4);
                string name = "";
                while (true)
                {
                    byte thisByte = reader.ReadByte();
                    if (thisByte == 0x00) break;
                    name += (char)thisByte;
                }
                flowgraph.name = name;
                scriptStart = flowgraph_offsets[i] + 116;
                //End of crappy namegrab

                reader.BaseStream.Position = flowgraph_offsets[i];
                reader.BaseStream.Position += 4; //Skip 0x00,0x00,0x00,0x00

                //Read the offsets and counts
                List<OffsetPair> offsetPairs = new List<OffsetPair>();
                for (int x = 0; x < 13; x++)
                {
                    if (x == 0) flowgraph.uniqueID = reader.ReadBytes(4);
                    if (x == 1) flowgraph.nodeID = reader.ReadBytes(4);
                    OffsetPair newPair = new OffsetPair();
                    newPair.GlobalOffset = reader.ReadInt32() * 4;
                    newPair.EntryCount = reader.ReadInt32();
                    offsetPairs.Add(newPair);
                }

                //Pull data from those offsets
                for (int x = 0; x < offsetPairs.Count; x++)
                {
                    reader.BaseStream.Position = offsetPairs[x].GlobalOffset;
                    for (int y = 0; y < offsetPairs[x].EntryCount; y++)
                    {
                        switch ((CathodeScriptBlocks)x)
                        {
                            //THIS CASE IS COMPLETE: ALL PARSED / ALL KNOWN
                            case CathodeScriptBlocks.DEFINE_NODE_LINKS:
                            {
                                reader.BaseStream.Position = offsetPairs[x].GlobalOffset + (y * 12);
                                byte[] parentID = reader.ReadBytes(4);

                                int OffsetToFindParams = reader.ReadInt32() * 4;
                                int NumberOfParams = reader.ReadInt32();

                                for (int z = 0; z < NumberOfParams; z++)
                                {
                                    reader.BaseStream.Position = OffsetToFindParams + (z * 16);
                                    CathodeNodeLink newLink = new CathodeNodeLink();
                                    newLink.connectionID = reader.ReadBytes(4);
                                    newLink.parentParamID = reader.ReadBytes(4);
                                    newLink.childParamID = reader.ReadBytes(4);
                                    newLink.childID = reader.ReadBytes(4);
                                    newLink.parentID = parentID;
                                    flowgraph.links.Add(newLink);
                                }
                                break;
                            }
                            //THIS CASE IS COMPLETE: ALL PARSED / ALL KNOWN
                            case CathodeScriptBlocks.DEFINE_NODE_PARAMETERS:
                            {
                                reader.BaseStream.Position = offsetPairs[x].GlobalOffset + (y * 12);
                                CathodeNodeEntity thisNode = flowgraph.GetNodeByID(reader.ReadBytes(4));

                                int OffsetToFindParams = reader.ReadInt32() * 4;
                                int NumberOfParams = reader.ReadInt32();

                                for (int z = 0; z < NumberOfParams; z++)
                                {
                                    reader.BaseStream.Position = OffsetToFindParams + (z * 8);
                                    CathodeParameterReference thisParamRef = new CathodeParameterReference();
                                    thisParamRef.paramID = reader.ReadBytes(4);
                                    thisParamRef.editOffset = (int)reader.BaseStream.Position;
                                    thisParamRef.offset = reader.ReadInt32() * 4;
                                    thisNode.nodeParameterReferences.Add(thisParamRef);
                                }
                                break;
                            }
                            case CathodeScriptBlocks.UNKNOWN_2:
                            {
                                break;
                                //Not all scripts have content in this block, but some scripts have a LOT of entries (sometimes like 400)
                                //No clue what the content here is though, it appears to be lists of IDs at separate offsets.

                                reader.BaseStream.Position = offsetPairs[x].GlobalOffset + (y * 12);

                                //This block defines some kind of ID, then an offset and a count of data at that offset
                                byte[] thisID = reader.ReadBytes(4);
                                int OffsetToFindParams = reader.ReadInt32() * 4;
                                int NumberOfParams = reader.ReadInt32();

                                //If we jump to the offset, we can read up to the count in blocks of four bytes.
                                //The last four bytes are always 0x00.
                                //I don't think there are any counts of 1 or below.
                                //The four byte content at this location is unknown, it appears to be some kind of ID system - no integers/floats.
                                reader.BaseStream.Position = OffsetToFindParams;
                                List<byte[]> content = new List<byte[]>();
                                for (int z = 0; z < NumberOfParams; z++)
                                {
                                    content.Add(reader.ReadBytes(4)); //TODO: work this out.
                                }
                                break;
                            }
                            case CathodeScriptBlocks.UNKNOWN_3:
                            {
                                break;
                                reader.BaseStream.Position = offsetPairs[x].GlobalOffset + (y * 8);

                                byte[] unk1 = reader.ReadBytes(4); //Sometimes this is a node ID
                                byte[] unk2 = reader.ReadBytes(4); //Dunno what this is

                                break;
                            }
                            //THIS CASE IS COMPLETE: ALL PARSED / ALL KNOWN
                            case CathodeScriptBlocks.DEFINE_NODE_DATATYPES:
                            {
                                CathodeNodeEntity thisNode = flowgraph.GetNodeByID(reader.ReadBytes(4));
                                thisNode.dataType = GetDataType(reader.ReadBytes(4));
                                thisNode.dataTypeParam = reader.ReadBytes(4);
                                break;
                            }
                            case CathodeScriptBlocks.DEFINE_LINKED_NODES:
                            {
                                break;
                                //This block appears to be populated mainly in mission flowgraphs, rather than other ones like archetypes or model placement
                                //It defines a node from another flowgraph, which is referenced by executation hierarchy
                                reader.BaseStream.Position = offsetPairs[x].GlobalOffset + (y * 20);

                                byte[] unk1 = reader.ReadBytes(4); //flowgraph id?
                                int unk2 = reader.ReadInt32() * 4; //offset
                                int unk3 = reader.ReadInt32(); //count
                                int resetPos = (int)reader.BaseStream.Position;
                                reader.BaseStream.Position = unk2;
                                //For some reason, the last ID is always seemingly 0x00,0x00,0x00,0x00 - similar to block CathodeScriptBlocks.UNKNOWN_2
                                for (int p = 0; p < unk3; p++)
                                {

                                    byte[] unk69 = reader.ReadBytes(4); //i think this might be some kind of recursive thing 
                                }
                                reader.BaseStream.Position = resetPos;
                                byte[] unk4 = reader.ReadBytes(4); //flowgraph id again
                                byte[] unk5 = reader.ReadBytes(4); //another id for something else

                                break;
                            }
                            //THIS CASE IS COMPLETE: ALL PARSED / ALL KNOWN
                            case CathodeScriptBlocks.DEFINE_NODE_NODETYPES:
                            {
                                CathodeNodeEntity thisNode = flowgraph.GetNodeByID(reader.ReadBytes(4));
                                thisNode.nodeType = reader.ReadBytes(4);
                                break;
                            }
                            case CathodeScriptBlocks.UNKNOWN_7:
                            {
                                break;
                                reader.BaseStream.Position = offsetPairs[x].GlobalOffset + (y * 40);

                                //The content of this block seems to change (but always 40 length?) - there are a number of types.
                                //One type has 0xDC, 0x53, 0xD1, 0x45 at position 28, and then two integers (an offset and count?)
                                //Since this varies so much some more work needs to be done to figure out exactly what it contains. First bit appears to be a bunch of IDs.

                                break;
                            }
                            case CathodeScriptBlocks.UNKNOWN_8:
                            {
                                break;
                                //This block is only four bytes - which translates to a pointer to another location... so read that
                                reader.BaseStream.Position = offsetPairs[x].GlobalOffset + (y * 4);
                                int offsetPos = reader.ReadInt32() * 4;

                                //Jump to the pointer location - this defines a node ID and another offset with count
                                reader.BaseStream.Position = offsetPos;
                                CathodeNodeEntity thisNode = flowgraph.GetNodeByID(reader.ReadBytes(4)); //These always seem to be animation related nodes
                                int OffsetToFindParams = reader.ReadInt32() * 4;
                                int NumberOfParams = reader.ReadInt32();

                                //Now we jump to THAT pointer's location, and iterate by count. The blocks here are of length 32.
                                for (int z = 0; z < NumberOfParams; z++)
                                {
                                    reader.BaseStream.Position = OffsetToFindParams + (z * 32);

                                    //First 4: unknown id
                                    byte[] unk1 = reader.ReadBytes(4);

                                    //Second 4: datatype
                                    byte[] datatype1 = reader.ReadBytes(4);
                                    CathodeDataType datatype1_converted = GetDataType(datatype1);

                                    //Third 4: unknown id
                                    byte[] unk2 = reader.ReadBytes(4);

                                    //Fourth 4: parameter id
                                    byte[] unk3 = reader.ReadBytes(4);
                                    string unk3_paramname_string = NodeDB.GetParameterName(unk3);

                                    //Fifth 4: datatype
                                    byte[] datatype2 = reader.ReadBytes(4);
                                    CathodeDataType datatype2_converted = GetDataType(datatype2);

                                    //Sixth 4: unknown ID
                                    byte[] unk4 = reader.ReadBytes(4);

                                    //Now we get ANOTHER offset. This is a pointer to a location and a count of IDs at that location.
                                    int offset1 = reader.ReadInt32() * 4;
                                    int count1 = reader.ReadInt32();
                                    //For some reason, the last ID is always seemingly 0x00,0x00,0x00,0x00 - similar to block CathodeScriptBlocks.UNKNOWN_2

                                    for (int p = 0; p < count1; p++)
                                    {
                                        reader.BaseStream.Position = offset1 + (p * 4);
                                        byte[] unk69 = reader.ReadBytes(4);
                                    }
                                }
                                break;
                            }
                            case CathodeScriptBlocks.DEFINE_ZONE_CONTENT:
                            {
                                break;
                                //This block appears to define zone content... i think

                                reader.BaseStream.Position = offsetPairs[x].GlobalOffset + (y * 4);
                                int offsetPos = reader.ReadInt32() * 4;

                                reader.BaseStream.Position = offsetPos;
                                CathodeNodeEntity thisNode = flowgraph.GetNodeByID(reader.ReadBytes(4));
                                string test0 = NodeDB.GetFriendlyName(thisNode.nodeID);
                                int OffsetToFindParams = reader.ReadInt32() * 4;
                                int NumberOfParams = reader.ReadInt32();

                                //Sometimes this cross-references other scripts I think (E.G. if the p count below is greater than 1, it's probably a script ref then the nodes in that script).
                                //Do some more digging - as we might end up putting nodes into the wrong flowgraph using the current GetNodeByID function here.

                                int goTo = OffsetToFindParams;
                                for (int m = 0; m < NumberOfParams; m++)
                                {
                                    reader.BaseStream.Position = goTo;

                                    byte[] firstFour = reader.ReadBytes(4);
                                    int offset1 = -1;
                                    if (m > 0) //for some reason only the first entry is 8 bytes long, the rest are 12, with four bytes of something at the start (some kind of ID, zone id maybe?)
                                    {
                                        offset1 = reader.ReadInt32() * 4;
                                    }
                                    else
                                    {
                                        offset1 = BitConverter.ToInt32(firstFour, 0) * 4;
                                    }
                                    int count1 = reader.ReadInt32();

                                    goTo = (int)reader.BaseStream.Position;

                                    reader.BaseStream.Position = offset1;
                                    for (int p = 0; p < count1; p++)
                                    {
                                        byte[] unk55 = reader.ReadBytes(4); //node id?
                                        CathodeNodeEntity thisNode2 = flowgraph.GetNodeByID(unk55);
                                        string test1 = NodeDB.GetFriendlyName(thisNode2.nodeID);
                                        if (count1 - 1 == p) continue;
                                    }
                                    //For some reason, the last ID is always seemingly 0x00,0x00,0x00,0x00 - similar to block CathodeScriptBlocks.UNKNOWN_2
                                }
                                break;
                            }
                        }
                    }
                }

                flowgraphs.Add(flowgraph);
            }
        }

        private CathodeDataType GetDataType(byte[] tag)
        {
            if (tag.SequenceEqual(new byte[] { 0xDA, 0x6B, 0xD7, 0x02 })) return CathodeDataType.TRANSFORM;
            else if (tag.SequenceEqual(new byte[] { 0xDC, 0x72, 0x74, 0xFD })) return CathodeDataType.FLOAT; //hmm
            else if (tag.SequenceEqual(new byte[] { 0x84, 0x11, 0xCD, 0x38 })) return CathodeDataType.STRING;
            else if (tag.SequenceEqual(new byte[] { 0x5E, 0x8E, 0x8E, 0x5A })) return CathodeDataType.UNKNOWN_2; //unknown long block (pointer, then count, then sets of 24 bytes * count, then 16 bytes), related to splines
            else if (tag.SequenceEqual(new byte[] { 0xBF, 0xA7, 0x62, 0x8C })) return CathodeDataType.ENUM;
            else if (tag.SequenceEqual(new byte[] { 0xF6, 0xAF, 0x08, 0x93 })) return CathodeDataType.RESOURCE_ID;
            else if (tag.SequenceEqual(new byte[] { 0xF0, 0x0B, 0x76, 0x96 })) return CathodeDataType.BOOL;
            else if (tag.SequenceEqual(new byte[] { 0x38, 0x43, 0xFF, 0xBF })) return CathodeDataType.VECTOR3;
            else if (tag.SequenceEqual(new byte[] { 0x87, 0xC1, 0x25, 0xE7 })) return CathodeDataType.INTEGER;
            else if (tag.SequenceEqual(new byte[] { 0xC7, 0x6E, 0xC8, 0x05 })) return CathodeDataType.UNKNOWN_6;
            else if (tag.SequenceEqual(new byte[] { 0x25, 0x16, 0x14, 0x8C })) return CathodeDataType.UNKNOWN_7;
            else if (tag.SequenceEqual(new byte[] { 0x7E, 0x39, 0xA1, 0xDD })) return CathodeDataType.UNKNOWN_8;
            else if (tag.SequenceEqual(new byte[] { 0xD1, 0xEA, 0x7E, 0x5E })) return CathodeDataType.UNKNOWN_9;
            else if (tag.SequenceEqual(new byte[] { 0x93, 0xE9, 0xE9, 0x37 })) return CathodeDataType.UNKNOWN_10;
            else if (tag.SequenceEqual(new byte[] { 0x8A, 0x79, 0x61, 0xC5 })) return CathodeDataType.UNKNOWN_11;
            else if (tag.SequenceEqual(new byte[] { 0x4F, 0x2A, 0x35, 0x5B })) return CathodeDataType.UNKNOWN_12;
            else
            {
                throw new Exception("ERROR! GetDataType couldn't match any CathodeDataType values.");
            }
        }

        private string path_to_pak = "";

        private List<byte[]> entry_points = new List<byte[]>();

        private int[] parameter_offsets;
        private int parameter_count;
        private int[] flowgraph_offsets;
        private int flowgraph_count;

        private List<CathodeFlowgraph> flowgraphs = new List<CathodeFlowgraph>();
        private List<CathodeParameter> parameters = new List<CathodeParameter>();
    }
}

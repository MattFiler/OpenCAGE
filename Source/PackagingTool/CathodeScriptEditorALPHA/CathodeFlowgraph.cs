using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alien_Isolation_Mod_Tools
{
    /* Blocks of data in each compiled flowgraph */
    public enum CathodeScriptBlocks
    {
        //These are +1 comapred to the ones in isolation_testground because we pull the block that skips first.

        DEFINE_NODE_LINKS = 1,      //This defines the logic links between nodes
        DEFINE_NODE_PARAMETERS = 2, //This defines executable nodes with parameters 
        UNKNOWN_2 = 3,              //
        UNKNOWN_3 = 4,              //
        DEFINE_NODE_DATATYPES = 5,  //This defines variable nodes which connect to other executable nodes to provide parameters: these seem to be exposed to other flowgraphs as parameters if the flowgraph is used as a type
        DEFINE_LINKED_NODES = 6,    //This defines a connected node through the flowgraph hierarchy 
        DEFINE_NODE_NODETYPES = 7,  //This defines the type ID for all executable nodes (completes the list from the parameter population in step 2) 
        UNKNOWN_7 = 8,              //
        UNKNOWN_8 = 9,              //
        DEFINE_ZONE_CONTENT = 10    //This defines zone content data for Zone nodes
    }

    /* Defines a link between parent and child IDs, with a connection ID */
    class CathodeNodeLink
    {
        public byte[] connectionID;  //The unique ID for this connection
        public byte[] parentID;      //The ID of the node we're connecting from, providing the value
        public byte[] parentParamID; //The ID of the parameter we're providing out of this node
        public byte[] childID;       //The ID of the node we're linking to to provide the value for
        public byte[] childParamID;  //The ID of the parameter we're providing into the child
    }

    /* A reference to a parameter in a flowgraph */
    class CathodeParameterReference
    {
        public byte[] paramID; //The ID of the param in the node
        public int offset;     //The offset of the param this reference points to
        public int editOffset; //The offset in the PAK that this reference is
    }

    /* A node in a flowgraph */
    class CathodeNodeEntity
    {
        public bool HasNodeType { get { return nodeType != null; } }
        public bool HasDataType { get { return dataType != CathodeDataType.NONE; } }

        public byte[] nodeID;   //Nodes always have a unique ID
        public byte[] nodeType; //Some nodes are of a node type

        public CathodeDataType dataType = CathodeDataType.NONE; //If nodes have no type, they're of a data type
        public byte[] dataTypeParam;                            //Data type nodes have a parameter ID

        public List<CathodeParameterReference> nodeParameterReferences = new List<CathodeParameterReference>();

        public CathodeParameterReference GetParameterReferenceByID(byte[] id)
        {
            foreach (CathodeParameterReference paramRef in nodeParameterReferences)
            {
                if (paramRef.paramID.SequenceEqual(id)) return paramRef;
            }
            return null;
        }
    }

    /* A script flowgraph containing nodes with parameters */
    class CathodeFlowgraph
    {
        public byte[] globalID;  //The four byte identifier code of the flowgraph global to all commands.paks
        public byte[] uniqueID;  //The four byte identifier code of the flowgraph unique to commands.pak
        public byte[] nodeID;    //The id when this flowgraph is used as a prefab node in another flowgraph
        public string name = ""; //The string name of the flowgraph

        public List<CathodeNodeEntity> nodes = new List<CathodeNodeEntity>();
        public List<CathodeNodeLink> links = new List<CathodeNodeLink>();

        /* If a node exists in the flowgraph, return it - otherwise create it, and return it */
        public CathodeNodeEntity GetNodeByID(byte[] id)
        {
            foreach (CathodeNodeEntity node in nodes)
            {
                if (node.nodeID.SequenceEqual(id)) return node;
            }
            CathodeNodeEntity newNode = new CathodeNodeEntity();
            newNode.nodeID = id;
            nodes.Add(newNode);
            return newNode;
        }

        /* Get all child links for a node */
        public List<CathodeNodeLink> GetChildLinksByID(byte[] id)
        {
            List<CathodeNodeLink> allLinks = new List<CathodeNodeLink>();
            foreach (CathodeNodeLink link in links)
            {
                if (link.parentID.SequenceEqual(id)) allLinks.Add(link);
            }
            return allLinks;
        }

        /* Get all parent links for a node */
        public List<CathodeNodeLink> GetParentLinksByID(byte[] id)
        {
            List<CathodeNodeLink> allLinks = new List<CathodeNodeLink>();
            foreach (CathodeNodeLink link in links)
            {
                if (link.childID.SequenceEqual(id)) allLinks.Add(link);
            }
            return allLinks;
        }
    }

    /* Temp holder for offset pairs */
    class OffsetPair
    {
        public int GlobalOffset = 0;
        public int EntryCount = 0;
    }
}

#pragma once

#include "CathodeParameter.h"
#include <string>
#include <vector>

/* Blocks of data in each compiled flowgraph */
enum class CathodeScriptBlocks
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
};

/* Defines a link between parent and child IDs, with a connection ID */
struct CathodeNodeLink
{
public:
    CathodeID connectionID;  //The unique ID for this connection
    CathodeID parentID;      //The ID of the node we're connecting from, providing the value
    CathodeID parentParamID; //The ID of the parameter we're providing out of this node
    CathodeID childID;       //The ID of the node we're linking to to provide the value for
    CathodeID childParamID;  //The ID of the parameter we're providing into the child
};

/* A reference to a parameter in a flowgraph */
struct CathodeParameterReference
{
public:
    CathodeID paramID; //The ID of the param in the node
    int offset;     //The offset of the param this reference points to
    int editOffset; //The offset in the PAK that this reference is
};

/* A node in a flowgraph */
struct CathodeNodeEntity
{
public:
    bool HasNodeType() { return nodeType.Get() != nullptr; }
    bool HasDataType() { return dataType != CathodeDataType::NONE; }

    CathodeID nodeID;   //Nodes always have a unique ID
    CathodeID nodeType; //Some nodes are of a node type

    CathodeDataType dataType = CathodeDataType::NONE; //If nodes have no type, they're of a data type
    CathodeID dataTypeParam;                            //Data type nodes have a parameter ID

    std::vector<CathodeParameterReference> nodeParameterReferences = std::vector<CathodeParameterReference>();
};

/* A script flowgraph containing nodes with parameters */
struct CathodeFlowgraph
{
public:
    CathodeID globalID;  //The four byte identifier code of the flowgraph global to all commands.paks
    CathodeID uniqueID;  //The four byte identifier code of the flowgraph unique to commands.pak
    CathodeID nodeID;    //The id when this flowgraph is used as a prefab node in another flowgraph
    std::string name = ""; //The string name of the flowgraph

    std::vector<CathodeNodeEntity*> nodes = std::vector<CathodeNodeEntity*>();
    std::vector<CathodeNodeLink> links = std::vector<CathodeNodeLink>();

    /* If a node exists in the flowgraph, return it - otherwise create it, and return it */
    CathodeNodeEntity* GetNodeByID(CathodeID id)
    {
        for (int i = 0; i < nodes.size(); i++) {
            if (nodes[i]->nodeID.Equals(id)) return nodes[i];
        }

        CathodeNodeEntity* newNode = new CathodeNodeEntity();
        newNode->nodeID = id;
        nodes.push_back(newNode);
        return newNode;
    }

    /* Get all child links for a node */
    std::vector<CathodeNodeLink> GetChildLinksByID(CathodeID id)
    {
        std::vector<CathodeNodeLink> allLinks = std::vector<CathodeNodeLink>();
        for (int i = 0; i < links.size(); i++) {
            if (links[i].parentID.Equals(id)) allLinks.push_back(links[i]);
        }
        return allLinks;
    }

    /* Get all parent links for a node */
    std::vector<CathodeNodeLink> GetParentLinksByID(CathodeID id)
    {
        std::vector<CathodeNodeLink> allLinks = std::vector<CathodeNodeLink>();
        for (int i = 0; i < links.size(); i++) {
            if (links[i].childID.Equals(id)) allLinks.push_back(links[i]);
        }
        return allLinks;
    }
};

/* Temp holder for offset pairs */
struct OffsetPair
{
public:
    int GlobalOffset = 0;
    int EntryCount = 0;
};
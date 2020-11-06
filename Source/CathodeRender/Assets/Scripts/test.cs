using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Alien_Isolation_Mod_Tools;
using System.Linq;
using AlienPAK;
using System.IO;
using System;

public class test : MonoBehaviour
{
    private CommandsPAK commandsPAK = null;
    private RenderableElementsBIN redsBIN = null;
    private ModelPAK modelPAK = null;
    private TexturePAK texturePAK = null;

    List<CS2> allModelsInLevel;
    BinaryReader modelPAKReader;

    //Test code to load in everything that has a position: note, the hierarchy of objects needs to be considered here
    void Start()
    {
        string basePath = @"G:\SteamLibrary\steamapps\common\Alien Isolation\DATA\ENV\PRODUCTION\TECH_COMMS\";
        commandsPAK = new CommandsPAK(basePath + @"WORLD\COMMANDS.PAK");
        redsBIN = new RenderableElementsBIN(basePath + @"WORLD\REDS.BIN");
        modelPAK = new ModelPAK(basePath + @"RENDERABLE\LEVEL_MODELS.PAK"); modelPAK.Load();
        allModelsInLevel = modelPAK.GetCS2s();
        //texturePAK = new TexturePAK(path_to_ENV + "/RENDERABLE/LEVEL_TEXTURES.ALL.PAK"); texturePAK.Load();
        modelPAKReader = new BinaryReader(File.OpenRead(basePath + @"RENDERABLE\LEVEL_MODELS.PAK"));

        RecursiveLoad(commandsPAK.EntryPoints[0], new PosAndRot());

        //CS2 submesh = allModelsInLevel[100];
        //LoadModel(submesh, new PosAndRot());

        modelPAKReader.Close();
    }

    private void RecursiveLoad(CathodeFlowgraph flowgraph, PosAndRot stackedTransform)
    {
        List<CathodeNodeEntity> models = GetAllOfType(flowgraph, new string[] { "ModelReference", "EnvironmentModelReference" });
        foreach (CathodeNodeEntity node in models)
        {
            PosAndRot thisNodePos = GetTransform(node) + stackedTransform;
            GameObject newCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            newCube.name = NodeDB.GetFriendlyName(node.nodeID);
            newCube.transform.position = new Vector3(thisNodePos.position.x, thisNodePos.position.y, thisNodePos.position.z);
            newCube.transform.eulerAngles = new Vector3(thisNodePos.rotation.x, thisNodePos.rotation.y, thisNodePos.rotation.z);
            continue;
            LoadREDS(node, flowgraph, thisNodePos);
        }

        /*
        List<CathodeNodeEntity> sounds = GetAllOfType(flowgraph, new string[] { "SoundPlaybackBaseClass", "SoundObject", "Sound" });
        foreach (CathodeNodeEntity node in sounds)
        {
            PosAndRot thisNodePos = GetTransform(node) + stackedTransform;
            GameObject newCube = new GameObject(NodeDB.GetName(node.nodeType));
            newCube.transform.position = new Vector3(thisNodePos.position.x, thisNodePos.position.y, thisNodePos.position.z);
            newCube.transform.eulerAngles = new Vector3(thisNodePos.rotation.x, thisNodePos.rotation.y, thisNodePos.rotation.z);
            newCube.AddComponent<AudioSource>();
            continue;
        }

        List<CathodeNodeEntity> particles = GetAllOfType(flowgraph, new string[] { "ParticleEmitterReference", "RibbonEmitterReference", "GPU_PFXEmitterReference" });
        foreach (CathodeNodeEntity node in particles)
        {
            PosAndRot thisNodePos = GetTransform(node) + stackedTransform;
            GameObject newCube = new GameObject(NodeDB.GetName(node.nodeType));
            newCube.transform.position = new Vector3(thisNodePos.position.x, thisNodePos.position.y, thisNodePos.position.z);
            newCube.transform.eulerAngles = new Vector3(thisNodePos.rotation.x, thisNodePos.rotation.y, thisNodePos.rotation.z);
            newCube.AddComponent<ParticleSystem>().emissionRate = 0;
            continue;
        }

        List<CathodeNodeEntity> lights = GetAllOfType(flowgraph, new string[] { "LightReference" });
        foreach (CathodeNodeEntity node in lights)
        {
            PosAndRot thisNodePos = GetTransform(node) + stackedTransform;
            GameObject newCube = new GameObject(NodeDB.GetName(node.nodeType));
            newCube.transform.position = new Vector3(thisNodePos.position.x, thisNodePos.position.y, thisNodePos.position.z);
            newCube.transform.eulerAngles = new Vector3(thisNodePos.rotation.x, thisNodePos.rotation.y, thisNodePos.rotation.z);
            newCube.AddComponent<Light>();
            continue;
        }
        */

        foreach (CathodeNodeEntity node in flowgraph.nodes)
        {
            CathodeFlowgraph nextCall = commandsPAK.GetFlowgraph(node.nodeType);
            if (nextCall == null) continue;
            Debug.Log(GetTransform(node).position);
            RecursiveLoad(nextCall, stackedTransform + GetTransform(node));
        }
    }

    private List<CathodeNodeEntity> GetAllOfType(CathodeFlowgraph flowgraph, string[] typeMatch)
    {
        List<CathodeNodeEntity> matchingNodes = new List<CathodeNodeEntity>();
        foreach (CathodeNodeEntity node in flowgraph.nodes)
        {
            if (!node.HasNodeType) continue;
            if (!typeMatch.ToList<string>().Contains(NodeDB.GetName(node.nodeType))) continue;
            matchingNodes.Add(node);
        }
        return matchingNodes;
    }

    private void LoadModel(CS2 submesh, PosAndRot thisNodePos)
    {
        try { 
            MemoryStream submeshEntry = null;
            BinaryReader submeshReader = null;
            int startOffset = 48;
            int entry_length = -1;

            if (submeshEntry == null)
            {
                modelPAKReader.BaseStream.Position = modelPAK._hle + submesh.PakOffset;
                submeshEntry = new MemoryStream(modelPAKReader.ReadBytes(submesh.PakSize));
                submeshReader = new BinaryReader(submeshEntry);

                submeshReader.BaseStream.Position = startOffset;
                bool foundPos = false;
                while (submeshReader.BaseStream.Position < (submeshReader.BaseStream.Length - (submesh.FaceCount * 2)))
                {
                    if (submeshReader.ReadBytes(2).SequenceEqual(new byte[] { 0x01, 0x80 }))
                    {
                        if (!foundPos)
                        {
                            foundPos = true;
                            entry_length = (int)submeshReader.BaseStream.Position;
                            startOffset = entry_length - 8;
                        }
                        else
                        {
                            entry_length = (int)submeshReader.BaseStream.Position - entry_length;
                            break;
                        }
                    }
                }
                if (entry_length == -1) return; //error
            }
            submeshReader.BaseStream.Position = startOffset;

            if (startOffset >= submeshReader.BaseStream.Length) return; //hmm

            Vector3[] vertices = new Vector3[submesh.VertCount];
            for (int i = 0; i < submesh.VertCount; i++)
            {
                vertices[i].x = (submeshReader.ReadInt16() / -2048.0f);
                vertices[i].y = (submeshReader.ReadInt16() / 2048.0f);
                vertices[i].z = (submeshReader.ReadInt16() / 2048.0f);

                int unk1 = submeshReader.ReadInt16();
                submeshReader.BaseStream.Position += entry_length - 8;
            }
            for (int i = 0; i < submesh.VertCount; i++)
            {
                //TODO: pull normals
            }
            for (int i = 0; i < submesh.VertCount; i++)
            {
                //TODO: there is sometimes another block here
            }
            startOffset += submesh.BlockSize;
            submeshReader.BaseStream.Position = (startOffset - (submesh.FaceCount * 2) -8); //sometimes this is -8, sometimes not :/
            int[] indicies = new int[submesh.FaceCount];
            for (int i = 0; i < submesh.FaceCount; i += 3)
            {
                int index_x = submeshReader.ReadInt16();
                int index_y = submeshReader.ReadInt16();
                int index_z = submeshReader.ReadInt16();

                indicies[i] = index_x;
                indicies[i + 1] = index_y;
                indicies[i + 2] = index_z;
            }

            GameObject newCube = new GameObject(submesh.Filename + " | " + submesh.ModelPartName);
            newCube.transform.position = thisNodePos.position;
            newCube.transform.eulerAngles = new Vector3(thisNodePos.rotation.y, thisNodePos.rotation.x, thisNodePos.rotation.z); //PARSING THIS WRONG IN THE DLL!

            Debug.Log(vertices.Length + " - " + submesh.Filename + " | " + submesh.ModelPartName);
            //Debug.Log(vertices);
            //Debug.Log(indicies);

            Mesh mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetIndices(indicies, MeshTopology.Triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            newCube.AddComponent<MeshFilter>().mesh = mesh;
            newCube.AddComponent<MeshRenderer>().material = new Material(Shader.Find("Diffuse"));
        }
        catch { }
    }

    private PosAndRot GetTransform(CathodeNodeEntity node)
    {
        PosAndRot toReturn = new PosAndRot();
        foreach (CathodeParameterReference paramRef in node.nodeParameterReferences)
        {
            CathodeParameter param = commandsPAK.GetParameter(paramRef.offset);
            if (param == null) continue;
            if (param.dataType != CathodeDataType.POSITION) continue;
            Vec3 position = ((CathodeTransform)param).position;
            Vec3 rotation = ((CathodeTransform)param).rotation;
            toReturn.position = new Vector3(position.x, position.y, position.z);
            toReturn.rotation = new Vector3(rotation.x, rotation.y, rotation.z);
        }
        return toReturn;
    }

    private byte[] GetResource(CathodeNodeEntity node)
    {
        foreach (CathodeParameterReference paramRef in node.nodeParameterReferences)
        {
            CathodeParameter param = commandsPAK.GetParameter(paramRef.offset);
            if (param == null) continue;
            if (param.dataType != CathodeDataType.SHORT_GUID) continue;
            return ((CathodeResource)param).resourceID;
        }
        return null;
    }

    private void LoadREDS(CathodeNodeEntity node, CathodeFlowgraph flowgraph, PosAndRot thisNodePos)
    {
        //If has a renderable element, try create it
        byte[] resourceID = GetResource(node);
        if (resourceID == null) return;
        CathodeResourceReference resRef = flowgraph.GetResourceReferenceByID(resourceID);
        if (resRef == null || resRef.entryType != CathodeResourceReferenceType.RENDERABLE_INSTANCE) return;
        List<RenderableElement> redsList = new List<RenderableElement>();
        for (int p = 0; p < resRef.entryCountREDS; p++) redsList.Add(redsBIN.GetRenderableElement(resRef.entryIndexREDS + p));
        if (resRef.entryCountREDS != redsList.Count || redsList.Count == 0) return; //TODO: handle this nicer
        foreach (RenderableElement renderable in redsList)
        {
            CS2 submesh = allModelsInLevel[renderable.model_index];
            LoadModel(submesh, thisNodePos);
        }
    }
}

public class PosAndRot
{
    public static PosAndRot operator+ (PosAndRot a, PosAndRot b)
    {
        PosAndRot newTrans = new PosAndRot();
        newTrans.position = a.position + b.position;
        newTrans.rotation = a.rotation + b.rotation;
        return newTrans;
    }

    public Vector3 position = new Vector3();
    public Vector3 rotation = new Vector3();
}